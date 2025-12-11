module Components.SyncFlow.State

open Elmish
open Components.SyncFlow.Types
open Types
open Shared.Domain

let private syncErrorToString (error: SyncError) : string =
    match error with
    | SyncError.SessionNotFound sessionId -> $"Session not found: {sessionId}"
    | SyncError.ComdirectAuthFailed reason ->
        // The backend now parses Comdirect JSON errors and returns user-friendly messages
        // So we just pass through the reason without adding a redundant prefix
        reason
    | SyncError.TanTimeout -> "TAN confirmation timed out. Please start a new sync."
    | SyncError.TransactionFetchFailed msg -> $"Failed to fetch transactions: {msg}"
    | SyncError.YnabImportFailed (count, msg) -> $"Failed to import {count} transactions: {msg}"
    | SyncError.InvalidSessionState (expected, actual) -> $"Invalid session state. Expected: {expected}, Actual: {actual}"
    | SyncError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"

let private ynabErrorToString (error: YnabError) : string =
    match error with
    | YnabError.Unauthorized msg -> $"YNAB authorization failed: {msg}"
    | YnabError.BudgetNotFound budgetId -> $"Budget not found: {budgetId}"
    | YnabError.AccountNotFound accountId -> $"Account not found: {accountId}"
    | YnabError.CategoryNotFound categoryId -> $"Category not found: {categoryId}"
    | YnabError.RateLimitExceeded retryAfter -> $"YNAB rate limit exceeded. Retry after {retryAfter} seconds"
    | YnabError.NetworkError msg -> $"YNAB network error: {msg}"
    | YnabError.InvalidResponse msg -> $"Invalid YNAB response: {msg}"

let private rulesErrorToString (error: RulesError) : string =
    match error with
    | RulesError.RuleNotFound ruleId -> $"Rule not found: {ruleId}"
    | RulesError.InvalidPattern (pattern, reason) -> $"Invalid pattern '{pattern}': {reason}"
    | RulesError.CategoryNotFound categoryId -> $"Category not found: {categoryId}"
    | RulesError.DuplicateRule pattern -> $"A rule with pattern '{pattern}' already exists"
    | RulesError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"

/// Check if a rule matches a transaction (client-side approximation for auto-apply)
let private matchesRule (rule: Rule) (tx: BankTransaction) : bool =
    let textToMatch =
        match rule.TargetField with
        | Payee -> tx.Payee |> Option.defaultValue ""
        | Memo -> tx.Memo
        | Combined ->
            let payee = tx.Payee |> Option.defaultValue ""
            $"{payee} {tx.Memo}"

    let textLower = textToMatch.ToLowerInvariant()
    let patternLower = rule.Pattern.ToLowerInvariant()

    match rule.PatternType with
    | Contains -> textLower.Contains patternLower
    | Exact -> textLower = patternLower
    | PatternType.Regex ->
        try
            System.Text.RegularExpressions.Regex.IsMatch(textToMatch, rule.Pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        with _ -> false

let init () : Model * Cmd<Msg> =
    let model = {
        CurrentSession = NotAsked
        SyncTransactions = NotAsked
        Categories = []
        SplitEdit = None
        DuplicateTransactionIds = []
        IsTanConfirming = false
        ExpandedTransactionIds = Set.empty
        InlineRuleForm = None
        ManuallyCategorizedIds = Set.empty
        ActiveFilter = AllTransactions
    }
    let cmd = Cmd.batch [
        Cmd.ofMsg LoadCurrentSession
        Cmd.ofMsg LoadCategories
    ]
    model, cmd

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | LoadCurrentSession ->
        let cmd =
            Cmd.OfAsync.perform
                Api.sync.getCurrentSession
                ()
                CurrentSessionLoaded
        { model with CurrentSession = Loading }, cmd, NoOp

    | CurrentSessionLoaded session ->
        let updatedModel = { model with CurrentSession = Success session }
        match session with
        | Some s when s.Status = ReviewingTransactions ->
            updatedModel, Cmd.ofMsg LoadTransactions, NoOp
        | _ -> updatedModel, Cmd.none, NoOp

    | StartSync ->
        let cmd =
            Cmd.OfAsync.either
                Api.sync.startSync
                ()
                SyncStarted
                (fun ex -> Error (SyncError.DatabaseError ("start", ex.Message)) |> SyncStarted)
        { model with CurrentSession = Loading }, cmd, NoOp

    | SyncStarted (Ok session) ->
        let model' = { model with CurrentSession = Success (Some session) }
        model', Cmd.ofMsg InitiateComdirectAuth, NoOp

    | SyncStarted (Error err) ->
        { model with CurrentSession = Failure (syncErrorToString err) }, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | InitiateComdirectAuth ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.initiateComdirectAuth
                    session.Id
                    ComdirectAuthInitiated
                    (fun ex -> Error (SyncError.ComdirectAuthFailed ex.Message) |> ComdirectAuthInitiated)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | ComdirectAuthInitiated (Ok _challengeId) ->
        model, Cmd.ofMsg LoadCurrentSession, ShowToast ("Please confirm the TAN on your phone", ToastInfo)

    | ComdirectAuthInitiated (Error err) ->
        { model with CurrentSession = Failure (syncErrorToString err) }, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | ConfirmTan ->
        // Prevent double-clicks: ignore if already confirming
        if model.IsTanConfirming then
            model, Cmd.none, NoOp
        else
            match model.CurrentSession with
            | Success (Some session) ->
                // Optimistic UI: Show FetchingTransactions immediately while API call runs
                // The API call does: TAN confirmation + fetch transactions + apply rules
                // This can take a while, so we show the fetching state right away
                let updatedSession = { session with Status = FetchingTransactions }
                let cmd =
                    Cmd.OfAsync.either
                        Api.sync.confirmTan
                        session.Id
                        TanConfirmed
                        (fun _ -> Error SyncError.TanTimeout |> TanConfirmed)
                { model with
                    IsTanConfirming = true
                    CurrentSession = Success (Some updatedSession) }, cmd, NoOp
            | _ -> model, Cmd.none, NoOp

    | TanConfirmed (Ok _) ->
        { model with IsTanConfirming = false }, Cmd.batch [ Cmd.ofMsg LoadCurrentSession; Cmd.ofMsg LoadTransactions ], ShowToast ("TAN confirmed, fetching transactions...", ToastSuccess)

    | TanConfirmed (Error err) ->
        { model with IsTanConfirming = false }, Cmd.ofMsg LoadCurrentSession, ShowToast (syncErrorToString err, ToastError)

    | LoadTransactions ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.getTransactions
                    session.Id
                    TransactionsLoaded
                    (fun ex -> Error (SyncError.DatabaseError ("load", ex.Message)) |> TransactionsLoaded)
            { model with SyncTransactions = Loading }, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | TransactionsLoaded (Ok transactions) ->
        { model with SyncTransactions = Success transactions }, Cmd.none, NoOp

    | TransactionsLoaded (Error err) ->
        { model with SyncTransactions = Failure (syncErrorToString err) }, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | CategorizeTransaction (txId, categoryId) ->
        match model.CurrentSession, model.SyncTransactions with
        | Success (Some session), Success transactions ->
            // Optimistic UI: Update locally first for instant feedback
            let updatedTransactions =
                transactions
                |> List.map (fun tx ->
                    if tx.Transaction.Id = txId then
                        // Find category name for display
                        let categoryName =
                            categoryId
                            |> Option.bind (fun catId ->
                                model.Categories |> List.tryFind (fun c -> c.Id = catId))
                            |> Option.map (fun c -> $"{c.GroupName}: {c.Name}")
                        // Update status based on category selection
                        let newStatus =
                            match tx.Status, categoryId with
                            | Skipped, _ -> Skipped  // Keep skipped status
                            | _, Some _ -> ManualCategorized
                            | _, None -> Pending
                        { tx with
                            CategoryId = categoryId
                            CategoryName = categoryName
                            Status = newStatus
                            Splits = None }  // Clear splits when changing category
                    else tx)

            // Optimistic UI: Also update ManuallyCategorizedIds immediately
            // so the "Create Rule" button appears instantly without waiting for server
            let newManuallyCategorized =
                match categoryId with
                | Some _ -> model.ManuallyCategorizedIds.Add txId
                | None -> model.ManuallyCategorizedIds.Remove txId

            let cmd =
                Cmd.OfAsync.either
                    Api.sync.categorizeTransaction
                    (session.Id, txId, categoryId, None)
                    TransactionCategorized
                    (fun ex -> Error (SyncError.DatabaseError ("categorize", ex.Message)) |> TransactionCategorized)

            { model with
                SyncTransactions = Success updatedTransactions
                ManuallyCategorizedIds = newManuallyCategorized }, cmd, NoOp
        | Success (Some session), _ ->
            // Transactions not loaded yet, just make the API call
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.categorizeTransaction
                    (session.Id, txId, categoryId, None)
                    TransactionCategorized
                    (fun ex -> Error (SyncError.DatabaseError ("categorize", ex.Message)) |> TransactionCategorized)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | TransactionCategorized (Ok updatedTx) ->
        match model.SyncTransactions with
        | Success transactions ->
            let newTxs = transactions |> List.map (fun tx ->
                if tx.Transaction.Id = updatedTx.Transaction.Id then updatedTx else tx)
            // Track this as manually categorized (for showing "Create Rule" button)
            let newManuallyCategorized =
                if updatedTx.Status = ManualCategorized && updatedTx.CategoryId.IsSome then
                    model.ManuallyCategorizedIds.Add updatedTx.Transaction.Id
                else
                    model.ManuallyCategorizedIds.Remove updatedTx.Transaction.Id
            { model with
                SyncTransactions = Success newTxs
                ManuallyCategorizedIds = newManuallyCategorized }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | TransactionCategorized (Error err) ->
        // Rollback: reload transactions from server to restore correct state
        model, Cmd.ofMsg LoadTransactions, ShowToast (syncErrorToString err, ToastError)

    | SkipTransaction txId ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.skipTransaction
                    (session.Id, txId)
                    TransactionSkipped
                    (fun ex -> Error (SyncError.DatabaseError ("skip", ex.Message)) |> TransactionSkipped)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | TransactionSkipped (Ok updatedTx) ->
        match model.SyncTransactions with
        | Success transactions ->
            let newTxs = transactions |> List.map (fun tx ->
                if tx.Transaction.Id = updatedTx.Transaction.Id then updatedTx else tx)
            { model with SyncTransactions = Success newTxs }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | TransactionSkipped (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | UnskipTransaction txId ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.unskipTransaction
                    (session.Id, txId)
                    TransactionUnskipped
                    (fun ex -> Error (SyncError.DatabaseError ("unskip", ex.Message)) |> TransactionUnskipped)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | TransactionUnskipped (Ok updatedTx) ->
        match model.SyncTransactions with
        | Success transactions ->
            let newTxs = transactions |> List.map (fun tx ->
                if tx.Transaction.Id = updatedTx.Transaction.Id then updatedTx else tx)
            { model with SyncTransactions = Success newTxs }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | TransactionUnskipped (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    // Split transaction handlers
    | StartSplitEdit txId ->
        match model.SyncTransactions with
        | Success transactions ->
            match transactions |> List.tryFind (fun tx -> tx.Transaction.Id = txId) with
            | Some tx ->
                let initialSplits =
                    tx.Splits
                    |> Option.defaultValue []
                let splitEdit = {
                    TransactionId = txId
                    Splits = initialSplits
                    RemainingAmount = tx.Transaction.Amount.Amount - (initialSplits |> List.sumBy (fun s -> s.Amount.Amount))
                    Currency = tx.Transaction.Amount.Currency
                }
                { model with SplitEdit = Some splitEdit }, Cmd.none, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | CancelSplitEdit ->
        { model with SplitEdit = None }, Cmd.none, NoOp

    | AddSplit (categoryId, categoryName, amount) ->
        match model.SplitEdit with
        | Some splitEdit ->
            let newSplit = {
                CategoryId = categoryId
                CategoryName = categoryName
                Amount = { Amount = amount; Currency = splitEdit.Currency }
                Memo = None
            }
            let newSplits = splitEdit.Splits @ [ newSplit ]
            let remaining = splitEdit.RemainingAmount - amount
            let updated = { splitEdit with Splits = newSplits; RemainingAmount = remaining }
            { model with SplitEdit = Some updated }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | RemoveSplit index ->
        match model.SplitEdit with
        | Some splitEdit when index >= 0 && index < splitEdit.Splits.Length ->
            let removedAmount = splitEdit.Splits.[index].Amount.Amount
            let newSplits = splitEdit.Splits |> List.indexed |> List.filter (fun (i, _) -> i <> index) |> List.map snd
            let updated = { splitEdit with Splits = newSplits; RemainingAmount = splitEdit.RemainingAmount + removedAmount }
            { model with SplitEdit = Some updated }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | UpdateSplitAmount (index, amount) ->
        match model.SplitEdit with
        | Some splitEdit when index >= 0 && index < splitEdit.Splits.Length ->
            let oldAmount = splitEdit.Splits.[index].Amount.Amount
            let newSplits =
                splitEdit.Splits
                |> List.indexed
                |> List.map (fun (i, s) ->
                    if i = index then { s with Amount = { s.Amount with Amount = amount } }
                    else s
                )
            let diff = oldAmount - amount
            let updated = { splitEdit with Splits = newSplits; RemainingAmount = splitEdit.RemainingAmount + diff }
            { model with SplitEdit = Some updated }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | UpdateSplitMemo (index, memo) ->
        match model.SplitEdit with
        | Some splitEdit when index >= 0 && index < splitEdit.Splits.Length ->
            let newSplits =
                splitEdit.Splits
                |> List.indexed
                |> List.map (fun (i, s) ->
                    if i = index then { s with Memo = memo }
                    else s
                )
            let updated = { splitEdit with Splits = newSplits }
            { model with SplitEdit = Some updated }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | SaveSplits ->
        match model.SplitEdit, model.CurrentSession with
        | Some splitEdit, Success (Some session) when splitEdit.Splits.Length >= 2 ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.splitTransaction
                    (session.Id, splitEdit.TransactionId, splitEdit.Splits)
                    SplitsSaved
                    (fun ex -> Error (SyncError.DatabaseError ("split", ex.Message)) |> SplitsSaved)
            model, cmd, NoOp
        | Some splitEdit, _ when splitEdit.Splits.Length < 2 ->
            model, Cmd.none, ShowToast ("At least 2 splits are required", ToastWarning)
        | _ -> model, Cmd.none, NoOp

    | SplitsSaved (Ok updatedTx) ->
        match model.SyncTransactions with
        | Success transactions ->
            let newTxs = transactions |> List.map (fun tx ->
                if tx.Transaction.Id = updatedTx.Transaction.Id then updatedTx else tx)
            { model with SyncTransactions = Success newTxs; SplitEdit = None }, Cmd.none, ShowToast ("Transaction split saved", ToastSuccess)
        | _ -> { model with SplitEdit = None }, Cmd.none, ShowToast ("Transaction split saved", ToastSuccess)

    | SplitsSaved (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | ClearSplit txId ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.clearSplit
                    (session.Id, txId)
                    SplitCleared
                    (fun ex -> Error (SyncError.DatabaseError ("clear_split", ex.Message)) |> SplitCleared)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | SplitCleared (Ok updatedTx) ->
        match model.SyncTransactions with
        | Success transactions ->
            let newTxs = transactions |> List.map (fun tx ->
                if tx.Transaction.Id = updatedTx.Transaction.Id then updatedTx else tx)
            { model with SyncTransactions = Success newTxs }, Cmd.none, ShowToast ("Split cleared", ToastInfo)
        | _ -> model, Cmd.none, ShowToast ("Split cleared", ToastInfo)

    | SplitCleared (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | ImportToYnab ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.importToYnab
                    session.Id
                    ImportCompleted
                    (fun ex -> Error (SyncError.DatabaseError ("import", ex.Message)) |> ImportCompleted)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | ImportCompleted (Ok result) ->
        let model' = { model with DuplicateTransactionIds = result.DuplicateTransactionIds }
        let toastMsg =
            if result.DuplicateTransactionIds.IsEmpty then
                $"Successfully imported {result.CreatedCount} transaction(s) to YNAB!"
            else
                $"Imported {result.CreatedCount} transaction(s). {result.DuplicateTransactionIds.Length} already exist in YNAB."
        let toastType = if result.DuplicateTransactionIds.IsEmpty then ToastSuccess else ToastWarning
        model', Cmd.ofMsg LoadCurrentSession, ShowToast (toastMsg, toastType)

    | ImportCompleted (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | ForceImportDuplicates ->
        match model.CurrentSession with
        | Success (Some session) when not model.DuplicateTransactionIds.IsEmpty ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.forceImportDuplicates
                    (session.Id, model.DuplicateTransactionIds)
                    ForceImportCompleted
                    (fun ex -> Error (SyncError.DatabaseError ("force_import", ex.Message)) |> ForceImportCompleted)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | ForceImportCompleted (Ok count) ->
        let model' = { model with DuplicateTransactionIds = [] }
        model', Cmd.batch [Cmd.ofMsg LoadCurrentSession; Cmd.ofMsg LoadTransactions], ShowToast ($"Successfully force-imported {count} transaction(s)!", ToastSuccess)

    | ForceImportCompleted (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | CancelSync ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.cancelSync
                    session.Id
                    SyncCancelled
                    (fun ex -> Error (SyncError.DatabaseError ("cancel", ex.Message)) |> SyncCancelled)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | SyncCancelled (Ok _) ->
        let model' = {
            model with
                CurrentSession = Success None
                SyncTransactions = NotAsked
        }
        model', Cmd.none, ShowToast ("Sync cancelled", ToastInfo)

    | SyncCancelled (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | LoadCategories ->
        // Load categories by first getting settings to obtain budget ID
        let loadCategoriesAsync () = async {
            let! settings = Api.settings.getSettings()
            match settings.Ynab with
            | Some ynab ->
                match ynab.DefaultBudgetId with
                | Some budgetId ->
                    return! Api.ynab.getCategories budgetId
                | None -> return Error (YnabError.Unauthorized "No default budget configured")
            | None -> return Error (YnabError.Unauthorized "YNAB not configured")
        }
        let cmd =
            Cmd.OfAsync.either
                loadCategoriesAsync
                ()
                CategoriesLoaded
                (fun ex -> CategoriesLoaded (Error (YnabError.NetworkError ex.Message)))
        model, cmd, NoOp

    | CategoriesLoaded (Ok categories) ->
        { model with Categories = categories }, Cmd.none, NoOp

    | CategoriesLoaded (Error _) ->
        model, Cmd.none, NoOp

    // UI interactions
    | ToggleTransactionExpand txId ->
        let newExpandedIds =
            if model.ExpandedTransactionIds.Contains txId then
                model.ExpandedTransactionIds.Remove txId
            else
                model.ExpandedTransactionIds.Add txId
        { model with ExpandedTransactionIds = newExpandedIds }, Cmd.none, NoOp

    | SetFilter filter ->
        { model with ActiveFilter = filter }, Cmd.none, NoOp

    // Inline rule creation handlers
    | OpenInlineRuleForm txId ->
        match model.SyncTransactions with
        | Success transactions ->
            match transactions |> List.tryFind (fun tx -> tx.Transaction.Id = txId) with
            | Some tx when tx.CategoryId.IsSome ->
                let categoryId = tx.CategoryId.Value
                let categoryName = tx.CategoryName |> Option.defaultValue "Unknown"
                let payee = tx.Transaction.Payee |> Option.defaultValue ""

                // Auto-generate a rule name from the payee
                let ruleName =
                    if System.String.IsNullOrWhiteSpace payee then "New Rule"
                    else
                        let truncated = if payee.Length > 30 then payee.Substring(0, 30) + "..." else payee
                        $"Auto: {truncated}"

                let formState : InlineRuleFormState = {
                    TransactionId = txId
                    Pattern = payee
                    PatternType = Contains
                    TargetField = Combined
                    CategoryId = categoryId
                    CategoryName = categoryName
                    PayeeOverride = ""
                    RuleName = ruleName
                    IsSaving = false
                }
                { model with InlineRuleForm = Some formState }, Cmd.none, NoOp
            | _ -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | CloseInlineRuleForm ->
        { model with InlineRuleForm = None }, Cmd.none, NoOp

    | UpdateInlineRulePattern value ->
        match model.InlineRuleForm with
        | Some form ->
            { model with InlineRuleForm = Some { form with Pattern = value } }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | UpdateInlineRulePatternType patternType ->
        match model.InlineRuleForm with
        | Some form ->
            { model with InlineRuleForm = Some { form with PatternType = patternType } }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | UpdateInlineRuleTargetField targetField ->
        match model.InlineRuleForm with
        | Some form ->
            { model with InlineRuleForm = Some { form with TargetField = targetField } }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | UpdateInlineRulePayeeOverride value ->
        match model.InlineRuleForm with
        | Some form ->
            { model with InlineRuleForm = Some { form with PayeeOverride = value } }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | UpdateInlineRuleName value ->
        match model.InlineRuleForm with
        | Some form ->
            { model with InlineRuleForm = Some { form with RuleName = value } }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | SaveInlineRule ->
        match model.InlineRuleForm with
        | Some form when not (System.String.IsNullOrWhiteSpace form.Pattern) ->
            let payeeOverride =
                if System.String.IsNullOrWhiteSpace form.PayeeOverride then None
                else Some form.PayeeOverride

            let request : RuleCreateRequest = {
                Name = form.RuleName
                Pattern = form.Pattern
                PatternType = form.PatternType
                TargetField = form.TargetField
                CategoryId = form.CategoryId
                PayeeOverride = payeeOverride
                Priority = 1  // Highest priority for new rules
            }

            let cmd =
                Cmd.OfAsync.either
                    Api.rules.createRule
                    request
                    InlineRuleSaved
                    (fun ex -> Error (RulesError.DatabaseError ("create", ex.Message)) |> InlineRuleSaved)

            { model with InlineRuleForm = Some { form with IsSaving = true } }, cmd, NoOp
        | Some _ -> model, Cmd.none, ShowToast ("Please enter a pattern", ToastWarning)
        | None -> model, Cmd.none, NoOp

    | InlineRuleSaved (Ok rule) ->
        // Close form and apply rule to pending transactions
        { model with InlineRuleForm = None },
        Cmd.ofMsg (ApplyNewRuleToTransactions rule),
        ShowToast ($"Rule '{rule.Name}' created!", ToastSuccess)

    | InlineRuleSaved (Error err) ->
        let errorMsg = rulesErrorToString err
        match model.InlineRuleForm with
        | Some form ->
            { model with InlineRuleForm = Some { form with IsSaving = false } },
            Cmd.none,
            ShowToast (errorMsg, ToastError)
        | None -> model, Cmd.none, ShowToast (errorMsg, ToastError)

    | ApplyNewRuleToTransactions rule ->
        match model.CurrentSession, model.SyncTransactions with
        | Success (Some session), Success transactions ->
            // Find pending transactions that match the new rule
            let matchingTxIds =
                transactions
                |> List.filter (fun tx ->
                    tx.Status = Pending &&
                    tx.CategoryId.IsNone &&
                    matchesRule rule tx.Transaction)
                |> List.map (fun tx -> tx.Transaction.Id)

            if matchingTxIds.IsEmpty then
                model, Cmd.none, NoOp
            else
                // Bulk categorize matching transactions
                let cmd =
                    Cmd.OfAsync.either
                        Api.sync.bulkCategorize
                        (session.Id, matchingTxIds, rule.CategoryId)
                        TransactionsUpdatedByRule
                        (fun ex -> Error (SyncError.DatabaseError ("bulk_categorize", ex.Message)) |> TransactionsUpdatedByRule)
                model, cmd, ShowToast ($"Applying rule to {matchingTxIds.Length} transaction(s)...", ToastInfo)
        | _ -> model, Cmd.none, NoOp

    | TransactionsUpdatedByRule (Ok updatedTxs) ->
        match model.SyncTransactions with
        | Success transactions ->
            let txMap = updatedTxs |> List.map (fun tx -> tx.Transaction.Id, tx) |> Map.ofList
            let newTxs = transactions |> List.map (fun tx ->
                match Map.tryFind tx.Transaction.Id txMap with
                | Some updated -> updated
                | None -> tx)
            { model with SyncTransactions = Success newTxs },
            Cmd.none,
            ShowToast ($"Rule applied to {updatedTxs.Length} transaction(s)", ToastSuccess)
        | _ -> model, Cmd.none, NoOp

    | TransactionsUpdatedByRule (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)
