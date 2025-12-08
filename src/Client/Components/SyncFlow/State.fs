module Components.SyncFlow.State

open Elmish
open Components.SyncFlow.Types
open Types
open Shared.Domain

let private syncErrorToString (error: SyncError) : string =
    match error with
    | SyncError.SessionNotFound sessionId -> $"Session not found: {sessionId}"
    | SyncError.ComdirectAuthFailed reason -> $"Comdirect authentication failed: {reason}"
    | SyncError.TanTimeout -> "TAN confirmation timed out"
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

let init () : Model * Cmd<Msg> =
    let model = {
        CurrentSession = NotAsked
        SyncTransactions = NotAsked
        Categories = []
        SplitEdit = None
        DuplicateTransactionIds = []
        IsTanConfirming = false
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
                let cmd =
                    Cmd.OfAsync.either
                        Api.sync.confirmTan
                        session.Id
                        TanConfirmed
                        (fun _ -> Error SyncError.TanTimeout |> TanConfirmed)
                { model with IsTanConfirming = true }, cmd, NoOp
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

            let cmd =
                Cmd.OfAsync.either
                    Api.sync.categorizeTransaction
                    (session.Id, txId, categoryId, None)
                    TransactionCategorized
                    (fun ex -> Error (SyncError.DatabaseError ("categorize", ex.Message)) |> TransactionCategorized)

            { model with SyncTransactions = Success updatedTransactions }, cmd, NoOp
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
            { model with SyncTransactions = Success newTxs }, Cmd.none, NoOp
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
