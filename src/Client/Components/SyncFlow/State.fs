module Components.SyncFlow.State

open Elmish
open Components.SyncFlow.Types
open Types
open Shared.Domain
open Client

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
    | SyncError.AskLenaRejected reason -> reason

/// Der Hinweis im Bericht nach dem Import, wenn Rückfragen Lena ohne Flag gesendet wurden
/// (die Flag-Farbe wurde zwischen Setzen und Push entfernt, ynab-sync-d9e51). Leer bei 0.
let importLenaFlagNote (notSent: int) : string =
    if notSent <= 0 then ""
    elif notSent = 1 then " 1 Rückfrage Lena ging ohne Flag nach YNAB, weil die Flag-Farbe in den Settings fehlt."
    else $" {notSent} Rückfragen Lena gingen ohne Flag nach YNAB, weil die Flag-Farbe in den Settings fehlt."

// (ynabErrorToString moved with the Quick Add submit logic to the top-level
// State, the only place it was used — ynab-q7k3m.)

/// Filter transactions based on the active filter (duplicated from View.fs for state logic)
let private filterTransactions (filter: TransactionFilter) (transactions: SyncTransaction list) =
    match filter with
    | AllTransactions -> transactions
    | ToBeImported ->
        transactions
        |> List.filter (fun tx ->
            tx.Status <> Skipped &&
            tx.Status <> Imported)
    | CategorizedTransactions ->
        transactions
        |> List.filter (fun tx ->
            tx.CategoryId.IsSome &&
            tx.Status <> Skipped &&
            tx.Status <> Imported)
    | UncategorizedTransactions ->
        transactions
        |> List.filter (fun tx ->
            tx.CategoryId.IsNone &&
            tx.Status <> Skipped &&
            tx.Status <> Imported)
    | SkippedTransactions ->
        transactions
        |> List.filter (fun tx -> tx.Status = Skipped)
    | ConfirmedDuplicates ->
        transactions
        |> List.filter (fun tx ->
            match tx.DuplicateStatus with
            | ConfirmedDuplicate _ -> true
            | _ -> false)

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

/// Pause zwischen zwei Lesevorgängen, solange der Server nach verlorener Antwort noch abruft.
let private interruptedStartPollMs = 3000

/// Frist für das Nachlesen: 40 Pausen sind 2 Minuten. Ein normaler Abruf endet weit davor.
let interruptedStartReadLimit = 40

/// Fehlertext, wenn nach verlorener Antwort von `startSync` keine neue Sitzung auf dem Server liegt.
let interruptedStartMessage (reason: string) : string =
    $"Die Verbindung zum Server brach ab ({reason}). Bitte starte den Sync erneut."

/// Fehlertext, wenn der Server bis zum Ablauf der Frist kein Ergebnis hat.
let interruptedStartTimeoutMessage (reason: string) : string =
    $"Die Verbindung zum Server brach ab ({reason}), und der Server hat auch nach 2 Minuten kein Ergebnis. Bitte starte den Sync erneut."

/// Stand vor dem Lesen der Sitzung. Beim Warten auf einen laufenden Abruf bleibt der Fortschritt stehen.
let markSessionLoading (model: Model) : Model =
    match model.InterruptedStart with
    | Some _ -> model
    | None -> { model with CurrentSession = Loading }

/// Erklärt laufende Sitzungs-Lesevorgänge für veraltet: ein später eintreffender Stand von vor
/// dem Start oder Abbruch darf das Ergebnis nicht überschreiben.
let supersedeSessionRead (model: Model) : Model =
    { model with SessionReadVersion = model.SessionReadVersion + 1 }

/// Beginn eines Sitzungs-Lesevorgangs. Ältere laufende Lesevorgänge verfallen.
let beginSessionRead (model: Model) : Model =
    markSessionLoading (supersedeSessionRead model)

/// Beginn eines Buchungs-Lesevorgangs. Ältere laufende Lesevorgänge verfallen.
let beginTransactionsRead (model: Model) : Model =
    { model with
        SyncTransactions = Loading
        TransactionsReadVersion = model.TransactionsReadVersion + 1 }

/// Die laufende Buchungsabfrage gehört zu einer beendeten Sitzung: ihre Antwort verfällt.
let abandonTransactionsRead (model: Model) : Model =
    { model with
        SyncTransactions = (if RemoteData.isLoading model.SyncTransactions then NotAsked else model.SyncTransactions)
        TransactionsReadVersion = model.TransactionsReadVersion + 1 }

/// Roman startet den Sync. Lesevorgänge von davor gelten nicht mehr.
let beginSyncStart (model: Model) : Model =
    { abandonTransactionsRead (supersedeSessionRead model) with CurrentSession = Loading; InterruptedStart = None }

/// Roman bestätigt die TAN: die Sitzung zeigt sofort den Abruf, ein Lesen von davor gilt nicht mehr.
let beginTanConfirm (session: SyncSession) (model: Model) : Model =
    { supersedeSessionRead model with
        IsTanConfirming = true
        CurrentSession = Success (Some { session with Status = FetchingTransactions }) }

/// Beginn eines Konten-Abrufs. Eine schon geladene Liste bleibt beim Auffrischen sichtbar.
let beginAccountsRead (model: Model) : Model =
    { model with
        Accounts = (match model.Accounts with Success _ as loaded -> loaded | _ -> Loading)
        AccountsReadVersion = model.AccountsReadVersion + 1 }

/// Beginn eines Abrufs der Quick-Add-Id.
let beginQuickAddRead (model: Model) : Model =
    { model with QuickAddReadVersion = model.QuickAddReadVersion + 1 }

/// Das Quick-Add-Konto als Übertrag-Ziel (ADR 0004). Nur offene Budget-Konten kommen infrage.
let private quickAddTransferTarget (model: Model) : SplitTarget option =
    model.QuickAddAccountId
    |> Option.bind (fun id -> openOnBudgetAccounts (knownAccounts model) |> List.tryFind (fun a -> a.Id = id))
    |> Option.map (fun a -> ToTransfer (a.Id, a.Name))

/// Trägt das Quick-Add-Konto in die wartende Barabhebungs-Zeile ein, sobald es bekannt ist.
/// Eine Zeile, die Roman inzwischen selbst gesetzt hat, bleibt unverändert.
let private fillAwaitedCashbackLine (model: Model) : Model =
    match model.SplitEdit, quickAddTransferTarget model with
    | Some ({ AwaitedCashbackLine = Some index } as edit), Some target ->
        let lines =
            edit.Lines
            |> List.mapi (fun i line ->
                if i = index && line.Target.IsNone then { line with Target = Some target } else line)
        { model with SplitEdit = Some { edit with Lines = lines; AwaitedCashbackLine = None } }
    | _ -> model

/// Lädt beim Öffnen des Split-Sheets nach, was beim Seitenaufruf nicht ankam.
let private reloadForSplitSheet (model: Model) : Cmd<Msg> =
    Cmd.batch [
        match model.Accounts with
        | Success _ | Loading -> ()
        | NotAsked | Failure _ -> Cmd.ofMsg LoadAccounts
        if model.QuickAddAccountId.IsNone then Cmd.ofMsg LoadQuickAddAccount
    ]

/// Der Fehlertext für den Toast. `Unauthorized` trägt hier auch "nicht eingerichtet" aus dem
/// Ladeweg, die allgemeine Übersetzung nennte das einen ungültigen Token.
let private categoriesErrorText (error: YnabError) : string =
    match error with
    | YnabError.Unauthorized message -> message
    | other -> ynabErrorText other

let init () : Model * Cmd<Msg> =
    let model = {
        CurrentSession = NotAsked
        SyncTransactions = NotAsked
        SessionReadVersion = 0
        TransactionsReadVersion = 0
        AccountsReadVersion = 0
        QuickAddReadVersion = 0
        Categories = []
        CategoriesLoad = NotAsked
        Accounts = NotAsked
        QuickAddAccountId = None
        Payees = []
        SplitEdit = None
        DuplicateTransactionIds = []
        IsTanConfirming = false
        InterruptedStart = None
        ExpandedTransactionIds = Set.empty
        InlineRuleForm = None
        InlineRuleTestVersion = 0
        ManuallyCategorizedIds = Set.empty
        ActiveFilter = AllTransactions
        PendingCategoryVersions = Map.empty
        PendingPayeeVersions = Map.empty
        RecentlyUsedCategoryIds = []
        LenaFlagColor = None
    }
    let cmd = Cmd.batch [
        Cmd.ofMsg LoadCurrentSession
        Cmd.ofMsg LoadCategories
        Cmd.ofMsg LoadPayees
    ]
    model, cmd

/// Wartezeit nach der letzten Eingabe, bis das Formular das Muster gegen die Buchung testet.
[<Literal>]
let private InlineRuleTestDelayMs = 300

/// Übernimmt das geänderte Formular und startet den Muster-Test neu. Ohne Muster gibt es nichts
/// zu testen, der Stand zählt trotzdem hoch, damit eine laufende Antwort verfällt.
let private restartInlineRuleTest (model: Model) (form: InlineRuleFormState) =
    let version = model.InlineRuleTestVersion + 1
    if System.String.IsNullOrWhiteSpace form.Pattern then
        { model with InlineRuleForm = Some { form with InlineRuleTest = NotAsked }; InlineRuleTestVersion = version },
        Cmd.none,
        NoOp
    else
        { model with InlineRuleForm = Some { form with InlineRuleTest = Loading }; InlineRuleTestVersion = version },
        Debounce.delayed InlineRuleTestDelayMs (InlineRuleTestDue version),
        NoOp

/// Trägt das Testergebnis ein, wenn es zum aktuellen Stand gehört. Spätere Antworten älterer
/// Anfragen ändern nichts.
let private settleInlineRuleTest (model: Model) (version: int) (test: RemoteData<bool>) =
    match model.InlineRuleForm with
    | Some form when version = model.InlineRuleTestVersion ->
        { model with InlineRuleForm = Some { form with InlineRuleTest = test } }
    | _ -> model

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | LoadCurrentSession ->
        let reading = beginSessionRead model
        let version = reading.SessionReadVersion
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.sync.getCurrentSession)
                ()
                (fun session -> SessionReadAnswered (version, Ok session))
                (fun ex -> SessionReadAnswered (version, Error ex.Message))
        reading, cmd, NoOp

    | SessionReadAnswered (version, _) when version <> model.SessionReadVersion ->
        // Inzwischen läuft ein neuerer Lesevorgang, oder Start und Abbruch haben den Stand überholt.
        model, Cmd.none, NoOp

    | SessionReadAnswered (_, Ok session) -> model, Cmd.ofMsg (CurrentSessionLoaded session), NoOp

    | SessionReadAnswered (_, Error reason) -> model, Cmd.ofMsg (CurrentSessionLoadFailed reason), NoOp

    | CurrentSessionLoaded session ->
        let updatedModel = { model with CurrentSession = Success session }
        match model.InterruptedStart, session with
        | Some recovery, (None | Some { Status = Completed }) ->
            // Der Start kam nie an oder die Sitzung stammt aus einem früheren Sync.
            let message = interruptedStartMessage recovery.Reason
            { model with CurrentSession = Failure message; InterruptedStart = None }, Cmd.none, ShowToast (message, ToastError)
        | Some recovery, Some { Status = (AwaitingBankAuth | FetchingTransactions) } ->
            // Der Server arbeitet noch. Nur lesen, nie neu starten (ADR 0071: keine Challenge ohne Roman).
            if recovery.ReadPlanned then
                // Dieses Lesen kam von außen (Routenwechsel), die Kette läuft schon.
                updatedModel, Cmd.none, NoOp
            elif recovery.ReadsLeft <= 0 then
                let message = interruptedStartTimeoutMessage recovery.Reason
                { model with CurrentSession = Failure message; InterruptedStart = None }, Cmd.none, ShowToast (message, ToastError)
            else
                let readsLeft = recovery.ReadsLeft - 1
                { updatedModel with InterruptedStart = Some { recovery with ReadsLeft = readsLeft; ReadPlanned = true } },
                Debounce.delayed interruptedStartPollMs (InterruptedStartReadDue readsLeft),
                NoOp
        | _, Some s when s.Status = ReviewingTransactions ->
            { updatedModel with InterruptedStart = None }, Cmd.ofMsg LoadTransactions, NoOp
        | _ -> { updatedModel with InterruptedStart = None }, Cmd.none, NoOp

    | CurrentSessionLoadFailed reason ->
        let message =
            model.InterruptedStart
            |> Option.map (fun recovery -> recovery.Reason)
            |> Option.defaultValue reason
            |> interruptedStartMessage
        { model with CurrentSession = Failure message; InterruptedStart = None }, Cmd.none, ShowToast (message, ToastError)

    | StartSync ->
        let cmd =
            Cmd.OfAsync.either
                Api.sync.startSync
                ()
                SyncStarted
                (fun ex -> SyncStartInterrupted ex.Message)
        beginSyncStart model, cmd, NoOp

    | SyncStarted (Ok (TanRequired session)) ->
        { supersedeSessionRead model with CurrentSession = Success (Some session) }, Cmd.none, ShowToast ("Bitte bestätige die TAN auf deinem Handy", ToastInfo)

    | SyncStarted (Ok (ReviewReady session)) ->
        { supersedeSessionRead model with CurrentSession = Success (Some session) }, Cmd.ofMsg LoadTransactions, ShowToast ("Umsätze bereit zur Prüfung", ToastSuccess)

    | SyncStarted (Error err) ->
        { supersedeSessionRead model with CurrentSession = Failure (syncErrorToString err) }, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | SyncStartInterrupted reason ->
        let recovery = { Reason = reason; ReadsLeft = interruptedStartReadLimit; ReadPlanned = false }
        { model with InterruptedStart = Some recovery }, Cmd.ofMsg LoadCurrentSession, NoOp

    | InterruptedStartReadDue readsLeft ->
        match model.InterruptedStart with
        | Some recovery when recovery.ReadPlanned && recovery.ReadsLeft = readsLeft ->
            { model with InterruptedStart = Some { recovery with ReadPlanned = false } }, Cmd.ofMsg LoadCurrentSession, NoOp
        // Das Nachlesen endete inzwischen, oder der Takt gehört zu einem früheren Abbruch.
        | _ -> model, Cmd.none, NoOp

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
                let cmd =
                    Cmd.OfAsync.either
                        Api.sync.confirmTan
                        session.Id
                        TanConfirmed
                        (fun _ -> Error SyncError.TanTimeout |> TanConfirmed)
                beginTanConfirm session model, cmd, NoOp
            | _ -> model, Cmd.none, NoOp

    | TanConfirmed (Ok _) ->
        { model with IsTanConfirming = false }, Cmd.batch [ Cmd.ofMsg LoadCurrentSession; Cmd.ofMsg LoadTransactions ], ShowToast ("TAN confirmed, fetching transactions...", ToastSuccess)

    | TanConfirmed (Error err) ->
        { model with IsTanConfirming = false }, Cmd.ofMsg LoadCurrentSession, ShowToast (syncErrorToString err, ToastError)

    | LoadTransactions ->
        match model.CurrentSession with
        | Success (Some session) ->
            let reading = beginTransactionsRead model
            let version = reading.TransactionsReadVersion
            let cmd =
                Cmd.OfAsync.either
                    (Retry.reading Api.sync.getTransactions)
                    session.Id
                    (fun result -> TransactionsReadAnswered (version, result))
                    (fun ex -> TransactionsReadAnswered (version, Error (SyncError.DatabaseError ("load", ex.Message))))
            // Die Lena-Farbe frisch mitladen: Roman kann sie in den Settings geändert haben.
            reading, Cmd.batch [ cmd; Cmd.ofMsg LoadLenaFlagColor ], NoOp
        | _ -> model, Cmd.none, NoOp

    | TransactionsReadAnswered (version, _) when version <> model.TransactionsReadVersion ->
        // Die Antwort gehört zu einem überholten Abruf oder zu einer beendeten Sitzung.
        model, Cmd.none, NoOp

    | TransactionsReadAnswered (_, result) -> model, Cmd.ofMsg (TransactionsLoaded result), NoOp

    | TransactionsLoaded (Ok transactions) ->
        { model with SyncTransactions = Success transactions }, Cmd.none, NoOp

    | TransactionsLoaded (Error err) ->
        { model with SyncTransactions = Failure (syncErrorToString err) }, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | CategorizeTransaction (txId, categoryId) ->
        match model.CurrentSession, model.SyncTransactions with
        | Success (Some _session), Success transactions ->
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

            // Debouncing: Increment version and schedule delayed API call
            let currentVersion =
                model.PendingCategoryVersions
                |> Map.tryFind txId
                |> Option.defaultValue 0
            let newVersion = currentVersion + 1
            let newPendingVersions = model.PendingCategoryVersions |> Map.add txId newVersion

            // Delayed command - will only execute API call if version is still current
            let debouncedCmd =
                Debounce.delayedDefault (CommitCategoryChange (txId, categoryId, newVersion))

            // Track recently used category
            let updatedRecent =
                match categoryId with
                | Some catId ->
                    catId :: (model.RecentlyUsedCategoryIds |> List.filter (fun id -> id <> catId))
                    |> List.truncate 10
                | None -> model.RecentlyUsedCategoryIds

            { model with
                SyncTransactions = Success updatedTransactions
                ManuallyCategorizedIds = newManuallyCategorized
                PendingCategoryVersions = newPendingVersions
                RecentlyUsedCategoryIds = updatedRecent }, debouncedCmd, NoOp
        | Success (Some session), _ ->
            // Transactions not loaded yet, just make the API call directly (no debounce needed)
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.categorizeTransaction
                    (session.Id, txId, categoryId, None)
                    TransactionCategorized
                    (fun ex -> Error (SyncError.DatabaseError ("categorize", ex.Message)) |> TransactionCategorized)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | CommitCategoryChange (txId, categoryId, version) ->
        // Only execute API call if version is still current (not superseded by newer change)
        let currentVersion =
            model.PendingCategoryVersions
            |> Map.tryFind txId
            |> Option.defaultValue 0

        if version <> currentVersion then
            // Stale change - a newer change has been made, ignore this one
            model, Cmd.none, NoOp
        else
            // Version matches - execute the API call
            match model.CurrentSession, model.SyncTransactions with
            | Success (Some session), Success transactions ->
                // Get current payeeOverride to preserve it when updating category
                let payeeOverride =
                    transactions
                    |> List.tryFind (fun tx -> tx.Transaction.Id = txId)
                    |> Option.bind (fun tx -> tx.PayeeOverride)

                let cmd =
                    Cmd.OfAsync.either
                        Api.sync.categorizeTransaction
                        (session.Id, txId, categoryId, payeeOverride)
                        TransactionCategorized
                        (fun ex -> Error (SyncError.DatabaseError ("categorize", ex.Message)) |> TransactionCategorized)
                // Clear version after committing (change is in flight)
                let newPendingVersions = model.PendingCategoryVersions |> Map.remove txId
                { model with PendingCategoryVersions = newPendingVersions }, cmd, NoOp
            | _ -> model, Cmd.none, NoOp

    | TransactionCategorized (Ok updatedTxs) ->
        match model.SyncTransactions with
        | Success transactions ->
            // Build a map of updated transactions for efficient lookup
            let updatedMap =
                updatedTxs
                |> List.map (fun tx -> tx.Transaction.Id, tx)
                |> Map.ofList
            let newTxs = transactions |> List.map (fun tx ->
                match Map.tryFind tx.Transaction.Id updatedMap with
                | Some updated -> updated
                | None -> tx)
            // Track only the primary transaction (first element) as manually categorized
            let newManuallyCategorized =
                match updatedTxs with
                | primaryTx :: _ ->
                    if primaryTx.Status = ManualCategorized && primaryTx.CategoryId.IsSome then
                        model.ManuallyCategorizedIds.Add primaryTx.Transaction.Id
                    else
                        model.ManuallyCategorizedIds.Remove primaryTx.Transaction.Id
                | [] -> model.ManuallyCategorizedIds
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

    // Lena fragen an der Importzeile (ynab-sync-d9e51, ADR 0056)
    | LoadLenaFlagColor ->
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.settings.getLenaFlagSettings)
                ()
                (fun settings -> LenaFlagColorLoaded settings.SavedColor)
                // Ein Fehler lässt die Farbe unbekannt: die Aktion bleibt gesperrt, kein Toast.
                (fun _ -> LenaFlagColorLoaded None)
        model, cmd, NoOp

    | LenaFlagColorLoaded color ->
        { model with LenaFlagColor = color }, Cmd.none, NoOp

    | SetAskLena (txId, ask) ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.setAskLena
                    (session.Id, txId, ask)
                    AskLenaSet
                    (fun ex -> Error (SyncError.DatabaseError ("ask_lena", ex.Message)) |> AskLenaSet)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | AskLenaSet (Ok updatedTx) ->
        match model.SyncTransactions with
        | Success transactions ->
            let newTxs = transactions |> List.map (fun tx ->
                if tx.Transaction.Id = updatedTx.Transaction.Id then updatedTx else tx)
            { model with SyncTransactions = Success newTxs }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | AskLenaSet (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)

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

    // Bulk skip/unskip handlers
    | SkipAllVisible ->
        match model.CurrentSession, model.SyncTransactions with
        | Success (Some session), Success transactions ->
            // Get visible transactions that can be skipped (not already skipped or imported)
            let visibleTransactions = filterTransactions model.ActiveFilter transactions
            let toSkip =
                visibleTransactions
                |> List.filter (fun tx -> tx.Status <> Skipped && tx.Status <> Imported)
                |> List.map (fun tx -> tx.Transaction.Id)

            if toSkip.IsEmpty then
                model, Cmd.none, ShowToast ("No transactions to skip", ToastInfo)
            else
                // Optimistic UI: Update locally first
                let updatedTransactions =
                    transactions
                    |> List.map (fun tx ->
                        if List.contains tx.Transaction.Id toSkip then
                            { tx with Status = Skipped }
                        else tx)

                // Create batch of skip commands
                let cmds =
                    toSkip
                    |> List.map (fun txId ->
                        Cmd.OfAsync.either
                            Api.sync.skipTransaction
                            (session.Id, txId)
                            (fun result -> BulkSkipCompleted (result |> Result.map List.singleton))
                            (fun ex -> Error (SyncError.DatabaseError ("bulk_skip", ex.Message)) |> BulkSkipCompleted))
                    |> Cmd.batch

                { model with SyncTransactions = Success updatedTransactions },
                cmds,
                ShowToast ($"Skipping {toSkip.Length} transaction(s)...", ToastInfo)
        | _ -> model, Cmd.none, NoOp

    | UnskipAllVisible ->
        match model.CurrentSession, model.SyncTransactions with
        | Success (Some session), Success transactions ->
            // Get visible transactions that can be unskipped (currently skipped)
            let visibleTransactions = filterTransactions model.ActiveFilter transactions
            let toUnskip =
                visibleTransactions
                |> List.filter (fun tx -> tx.Status = Skipped)
                |> List.map (fun tx -> tx.Transaction.Id)

            if toUnskip.IsEmpty then
                model, Cmd.none, ShowToast ("No transactions to unskip", ToastInfo)
            else
                // Optimistic UI: Update locally first
                let updatedTransactions =
                    transactions
                    |> List.map (fun tx ->
                        if List.contains tx.Transaction.Id toUnskip then
                            // Restore to Pending or appropriate status
                            let newStatus =
                                if tx.CategoryId.IsSome then ManualCategorized
                                else Pending
                            { tx with Status = newStatus }
                        else tx)

                // Create batch of unskip commands
                let cmds =
                    toUnskip
                    |> List.map (fun txId ->
                        Cmd.OfAsync.either
                            Api.sync.unskipTransaction
                            (session.Id, txId)
                            (fun result -> BulkUnskipCompleted (result |> Result.map List.singleton))
                            (fun ex -> Error (SyncError.DatabaseError ("bulk_unskip", ex.Message)) |> BulkUnskipCompleted))
                    |> Cmd.batch

                { model with SyncTransactions = Success updatedTransactions },
                cmds,
                ShowToast ($"Unskipping {toUnskip.Length} transaction(s)...", ToastInfo)
        | _ -> model, Cmd.none, NoOp

    | BulkSkipCompleted (Ok _) ->
        // Individual skip completed - no action needed since we did optimistic update
        model, Cmd.none, NoOp

    | BulkSkipCompleted (Error err) ->
        // Rollback on error by reloading from server
        model, Cmd.ofMsg LoadTransactions, ShowToast (syncErrorToString err, ToastError)

    | BulkUnskipCompleted (Ok _) ->
        // Individual unskip completed - no action needed since we did optimistic update
        model, Cmd.none, NoOp

    | BulkUnskipCompleted (Error err) ->
        // Rollback on error by reloading from server
        model, Cmd.ofMsg LoadTransactions, ShowToast (syncErrorToString err, ToastError)

    // Split transaction handlers
    | StartSplitEdit txId ->
        match model.SyncTransactions with
        | Success transactions ->
            match transactions |> List.tryFind (fun tx -> tx.Transaction.Id = txId) with
            | Some tx ->
                // Seed from any existing splits, else start with two empty lines so
                // the editor is immediately a valid "split" shape to fill in.
                let seededLines =
                    match tx.Splits with
                    | Some splits when not splits.IsEmpty ->
                        splits
                        |> List.map (fun s ->
                            { Target = Some s.Target
                              AmountText = formatAmountForEdit s.Amount.Amount
                              Memo = s.Memo |> Option.defaultValue "" })
                    | _ ->
                        [ { Target = None; AmountText = ""; Memo = "" }
                          { Target = None; AmountText = ""; Memo = "" } ]
                let splitEdit = {
                    TransactionId = txId
                    Total = tx.Transaction.Amount
                    Lines = seededLines
                    Currency = tx.Transaction.Amount.Currency
                    ActivePicker = NoPicker
                    AwaitedCashbackLine = None
                }
                { model with SplitEdit = Some splitEdit }, reloadForSplitSheet model, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | StartCashbackSplit txId ->
        match model.SyncTransactions with
        | Success transactions ->
            match transactions |> List.tryFind (fun tx -> tx.Transaction.Id = txId) with
            | Some tx ->
                let total = tx.Transaction.Amount
                // Category line = the transaction's current category (if any), else
                // an empty target the user fills via the picker.
                let categoryTarget =
                    match tx.CategoryId with
                    | Some catId ->
                        let name =
                            tx.CategoryName
                            |> Option.orElse (
                                model.Categories
                                |> List.tryFind (fun c -> c.Id = catId)
                                |> Option.map (fun c -> c.Name))
                            |> Option.defaultValue "Kategorie"
                        Some (ToCategory (catId, name))
                    | None -> None
                // Transfer target = the configured Quick-Add account (ADR 0004),
                // overridable in the picker. Unset → no default, manual selection.
                // Fehlt es noch, trägt `fillAwaitedCashbackLine` es beim Eintreffen nach.
                let transferTarget = quickAddTransferTarget model
                // Cashback shape: pre-select the category + the cash transfer
                // target; both amounts are editable. The user types the cash
                // withdrawal amount and taps "Rest" on the category (or vice
                // versa) to balance the split — no magic read-only line (ynab-003).
                let lines =
                    [ { Target = categoryTarget; AmountText = ""; Memo = "" }
                      { Target = transferTarget; AmountText = ""; Memo = "" } ]
                let splitEdit = {
                    TransactionId = txId
                    Total = total
                    Lines = lines
                    Currency = total.Currency
                    ActivePicker = NoPicker
                    AwaitedCashbackLine = if transferTarget.IsNone then Some 1 else None
                }
                { model with SplitEdit = Some splitEdit }, reloadForSplitSheet model, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | CancelSplitEdit ->
        { model with SplitEdit = None }, Cmd.none, NoOp

    | AddSplitLine ->
        match model.SplitEdit with
        | Some splitEdit ->
            let updated = { splitEdit with Lines = splitEdit.Lines @ [ { Target = None; AmountText = ""; Memo = "" } ] }
            { model with SplitEdit = Some updated }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | FillSplitRemainder index ->
        match model.SplitEdit with
        | Some splitEdit when index >= 0 && index < splitEdit.Lines.Length ->
            // Set this line to the leftover magnitude so the split balances; the
            // sign is applied at commit time (ynab-003). Delegates the arithmetic
            // to restMagnitudeForLine (built on the shared splitRemainder).
            let rest = restMagnitudeForLine index splitEdit
            let newLines =
                splitEdit.Lines
                |> List.mapi (fun i l -> if i = index then { l with AmountText = formatAmountForEdit rest } else l)
            { model with SplitEdit = Some { splitEdit with Lines = newLines } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RemoveSplit index ->
        match model.SplitEdit with
        | Some splitEdit when index >= 0 && index < splitEdit.Lines.Length ->
            let newLines = splitEdit.Lines |> List.indexed |> List.filter (fun (i, _) -> i <> index) |> List.map snd
            // Die wartende Zeile rückt mit; ist sie selbst weg, wartet nichts mehr.
            let awaited =
                splitEdit.AwaitedCashbackLine
                |> Option.bind (fun line ->
                    if line = index then None
                    elif index < line then Some (line - 1)
                    else Some line)
            let updated = { splitEdit with Lines = newLines; AwaitedCashbackLine = awaited }
            { model with SplitEdit = Some updated }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | UpdateSplitAmountText (index, text) ->
        match model.SplitEdit with
        | Some splitEdit when index >= 0 && index < splitEdit.Lines.Length ->
            let newLines =
                splitEdit.Lines
                |> List.mapi (fun i l -> if i = index then { l with AmountText = text } else l)
            { model with SplitEdit = Some { splitEdit with Lines = newLines } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | UpdateSplitMemo (index, memo) ->
        match model.SplitEdit with
        | Some splitEdit when index >= 0 && index < splitEdit.Lines.Length ->
            let newLines =
                splitEdit.Lines
                |> List.mapi (fun i l ->
                    if i = index then { l with Memo = memo |> Option.defaultValue "" } else l)
            { model with SplitEdit = Some { splitEdit with Lines = newLines } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | OpenSplitTargetPicker index ->
        match model.SplitEdit with
        | Some splitEdit -> { model with SplitEdit = Some { splitEdit with ActivePicker = TargetPickerFor index } }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | CloseSplitPicker ->
        match model.SplitEdit with
        | Some splitEdit -> { model with SplitEdit = Some { splitEdit with ActivePicker = NoPicker } }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | SelectSplitCategory (index, categoryId) ->
        match model.SplitEdit with
        | Some splitEdit when index >= 0 && index < splitEdit.Lines.Length ->
            let name =
                model.Categories
                |> List.tryFind (fun c -> c.Id = categoryId)
                |> Option.map (fun c -> c.Name)
                |> Option.defaultValue "Kategorie"
            let newLines =
                splitEdit.Lines
                |> List.mapi (fun i l -> if i = index then { l with Target = Some (ToCategory (categoryId, name)) } else l)
            { model with SplitEdit = Some { splitEdit with Lines = newLines; ActivePicker = NoPicker } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | SelectSplitAccount (index, accountId) ->
        match model.SplitEdit with
        | Some splitEdit when index >= 0 && index < splitEdit.Lines.Length ->
            let name =
                knownAccounts model
                |> List.tryFind (fun a -> a.Id = accountId)
                |> Option.map (fun a -> a.Name)
                |> Option.defaultValue "Konto"
            let newLines =
                splitEdit.Lines
                |> List.mapi (fun i l -> if i = index then { l with Target = Some (ToTransfer (accountId, name)) } else l)
            { model with SplitEdit = Some { splitEdit with Lines = newLines; ActivePicker = NoPicker } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | SaveSplits ->
        match model.SplitEdit, model.CurrentSession with
        | Some splitEdit, Success (Some session) ->
            match validateSplitEdit splitEdit with
            | Ok splits ->
                let cmd =
                    Cmd.OfAsync.either
                        Api.sync.splitTransaction
                        (session.Id, splitEdit.TransactionId, splits)
                        SplitsSaved
                        (fun ex -> Error (SyncError.DatabaseError ("split", ex.Message)) |> SplitsSaved)
                model, cmd, NoOp
            | Error (TooFewLines _) ->
                model, Cmd.none, ShowToast ("Mindestens 2 Zeilen erforderlich", ToastWarning)
            | Error (SumMismatch _) ->
                model, Cmd.none, ShowToast ("Die Summe muss dem Gesamtbetrag entsprechen", ToastWarning)
            | Error CurrencyMismatch ->
                model, Cmd.none, ShowToast ("Alle Zeilen müssen dieselbe Währung haben", ToastWarning)
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
        // Rückfragen, die ohne Flag nach YNAB gingen, weil die Lena-Farbe bis zum Push entfernt wurde.
        let toastMsg = toastMsg + importLenaFlagNote result.LenaFlagNotSentCount
        let toastType =
            if result.DuplicateTransactionIds.IsEmpty && result.LenaFlagNotSentCount = 0 then ToastSuccess
            else ToastWarning
        model', Cmd.ofMsg LoadCurrentSession, ShowToast (toastMsg, toastType)

    | ImportCompleted (Error err) ->
        // Ein Teilfehlschlag (`YnabImportFailed` für einzelne Buchungen im Batch)
        // kann trotzdem bereits gespeicherte Buchungen hinterlassen haben
        // (`Server.Api.importToYnab`, "Persisted regardless of a ledger write
        // failure"). Ohne Neuladen zeigt der Client diese Buchungen mit ihrem
        // alten Stand an (ledger-mshd8).
        model, Cmd.ofMsg LoadCurrentSession, ShowToast (syncErrorToString err, ToastError)

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
            abandonTransactionsRead (supersedeSessionRead model) with
                CurrentSession = Success None
                SyncTransactions = NotAsked
        }
        model', Cmd.none, ShowToast ("Sync cancelled", ToastInfo)

    | SyncCancelled (Error err) ->
        model, Cmd.none, ShowToast (syncErrorToString err, ToastError)

    | LoadCategories when RemoteData.isLoading model.CategoriesLoad ->
        // Der laufende Abruf liefert dasselbe.
        model, Cmd.none, NoOp

    | LoadCategories ->
        // Load categories by first getting settings to obtain budget ID
        let loadCategoriesAsync () = async {
            let! settings = Retry.reading Api.settings.getSettings ()
            match settings.Ynab with
            | Some ynab ->
                match ynab.DefaultBudgetId with
                | Some budgetId ->
                    return! Retry.reading Api.ynab.getCategories budgetId
                | None -> return Error (YnabError.Unauthorized "Kein Standard-Budget eingerichtet")
            | None -> return Error (YnabError.Unauthorized "YNAB ist nicht eingerichtet")
        }
        let cmd =
            Cmd.OfAsync.either
                loadCategoriesAsync
                ()
                CategoriesLoaded
                (fun ex -> CategoriesLoaded (Error (YnabError.NetworkError ex.Message)))
        { model with CategoriesLoad = Loading }, cmd, NoOp

    | CategoriesLoaded (Ok categories) ->
        { model with Categories = categories; CategoriesLoad = Success () }, Cmd.none, NoOp

    | CategoriesLoaded (Error error) ->
        let text = categoriesErrorText error
        { model with CategoriesLoad = Failure text },
        Cmd.none,
        ShowLoadFailure $"Kategorien konnten nicht geladen werden: {text}"

    | LoadAccounts when RemoteData.isLoading model.Accounts ->
        // Der laufende Abruf liefert dasselbe, und ein Fehlschlag gibt nur einen Toast.
        model, Cmd.none, NoOp

    | LoadAccounts ->
        // Die Konten für den Übertrag-Picker, serverseitig gecacht (`LedgerApi`, wie das
        // Kassenbuch). Nicht `getBudgetDetails`: der holt das ganze Budget, 3 bis 17 Sekunden.
        let reading = beginAccountsRead model
        let version = reading.AccountsReadVersion
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.ledger.getBudgetAccounts)
                ()
                (fun result -> AccountsLoaded (version, result))
                (fun ex -> AccountsLoaded (version, Error ex.Message))
        reading, cmd, NoOp

    | AccountsLoaded (version, _) when version <> model.AccountsReadVersion ->
        // Ein überholtes Auffrischen: kein Fehler-Toast über die neuere Antwort.
        model, Cmd.none, NoOp

    | AccountsLoaded (_, Ok accounts) ->
        fillAwaitedCashbackLine { model with Accounts = Success accounts }, Cmd.none, NoOp

    | AccountsLoaded (_, Error message) ->
        let accounts =
            match model.Accounts with
            | Success _ as loaded -> loaded
            | _ -> Failure message
        { model with Accounts = accounts },
        Cmd.none,
        ShowLoadFailure $"Konten konnten nicht geladen werden: {message}"

    | LoadQuickAddAccount ->
        let reading = beginQuickAddRead model
        let version = reading.QuickAddReadVersion
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.settings.getSettings)
                ()
                (fun settings -> QuickAddAccountLoaded (version, Ok (settings.Ynab |> Option.bind (fun ynab -> ynab.QuickAddAccountId))))
                (fun ex -> QuickAddAccountLoaded (version, Error ex.Message))
        reading, cmd, NoOp

    | QuickAddAccountLoaded (version, _) when version <> model.QuickAddReadVersion ->
        // Roman kann das Konto in den Settings geändert haben, die neuere Antwort gilt.
        model, Cmd.none, NoOp

    | QuickAddAccountLoaded (_, Ok accountId) ->
        fillAwaitedCashbackLine { model with QuickAddAccountId = accountId }, Cmd.none, NoOp

    // Ohne die Id bleibt die Übertrag-Zeile leer und wählbar, wie im Kassenbuch ohne Toast.
    // Eine schon bekannte Id bleibt stehen, das Öffnen des Sheets versucht es sonst erneut.
    | QuickAddAccountLoaded (_, Error _) ->
        model, Cmd.none, NoOp

    | LoadPayees ->
        // Load payees by first getting settings to obtain budget ID
        let loadPayeesAsync () = async {
            let! settings = Retry.reading Api.settings.getSettings ()
            match settings.Ynab with
            | Some ynab ->
                match ynab.DefaultBudgetId with
                | Some budgetId ->
                    return! Retry.reading Api.ynab.getPayees budgetId
                | None -> return Error (YnabError.Unauthorized "No default budget configured")
            | None -> return Error (YnabError.Unauthorized "YNAB not configured")
        }
        let cmd =
            Cmd.OfAsync.either
                loadPayeesAsync
                ()
                PayeesLoaded
                (fun ex -> PayeesLoaded (Error (YnabError.NetworkError ex.Message)))
        model, cmd, NoOp

    | PayeesLoaded (Ok payees) ->
        { model with Payees = payees }, Cmd.none, NoOp

    | PayeesLoaded (Error _) ->
        model, Cmd.none, NoOp

    | SetPayeeOverride (txId, payeeOverride) ->
        // Optimistic UI update + debouncing (similar to CategorizeTransaction)
        match model.SyncTransactions with
        | Success transactions ->
            let updatedTransactions =
                transactions
                |> List.map (fun tx ->
                    if tx.Transaction.Id = txId then
                        { tx with PayeeOverride = payeeOverride }
                    else tx)

            let currentVersion =
                model.PendingPayeeVersions
                |> Map.tryFind txId
                |> Option.defaultValue 0
            let newVersion = currentVersion + 1
            let newPendingVersions = model.PendingPayeeVersions |> Map.add txId newVersion

            let debouncedCmd =
                Debounce.delayedDefault (CommitPayeeChange (txId, payeeOverride, newVersion))

            { model with
                SyncTransactions = Success updatedTransactions
                PendingPayeeVersions = newPendingVersions }, debouncedCmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | CommitPayeeChange (txId, payeeOverride, version) ->
        let currentVersion =
            model.PendingPayeeVersions
            |> Map.tryFind txId
            |> Option.defaultValue 0

        if version <> currentVersion then
            // Stale version, skip API call
            model, Cmd.none, NoOp
        else
            match model.CurrentSession, model.SyncTransactions with
            | Success (Some session), Success transactions ->
                // Get current category for this transaction
                let categoryId =
                    transactions
                    |> List.tryFind (fun tx -> tx.Transaction.Id = txId)
                    |> Option.bind (fun tx -> tx.CategoryId)

                let cmd =
                    Cmd.OfAsync.either
                        Api.sync.categorizeTransaction
                        (session.Id, txId, categoryId, payeeOverride)
                        TransactionCategorized
                        (fun ex -> TransactionCategorized (Error (SyncError.DatabaseError ("payee", ex.Message))))

                let newPendingVersions = model.PendingPayeeVersions |> Map.remove txId
                { model with PendingPayeeVersions = newPendingVersions }, cmd, NoOp
            | _ -> model, Cmd.none, NoOp

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

                // Build transaction text for auto-test (payee + memo)
                let memo = tx.Transaction.Memo
                let transactionText =
                    if System.String.IsNullOrWhiteSpace payee then memo
                    elif System.String.IsNullOrWhiteSpace memo then payee
                    else payee + " " + memo

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
                    TransactionText = transactionText
                    InlineRuleTest = NotAsked
                }
                restartInlineRuleTest model formState
            | _ -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | CloseInlineRuleForm ->
        { model with InlineRuleForm = None; InlineRuleTestVersion = model.InlineRuleTestVersion + 1 }, Cmd.none, NoOp

    | UpdateInlineRulePattern value ->
        match model.InlineRuleForm with
        | Some form ->
            restartInlineRuleTest model { form with Pattern = value }
        | None -> model, Cmd.none, NoOp

    | UpdateInlineRulePatternType patternType ->
        match model.InlineRuleForm with
        | Some form ->
            restartInlineRuleTest model { form with PatternType = patternType }
        | None -> model, Cmd.none, NoOp

    | UpdateInlineRuleTargetField targetField ->
        match model.InlineRuleForm with
        | Some form ->
            restartInlineRuleTest model { form with TargetField = targetField }
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

            let request : NewRule = {
                Name = form.RuleName
                Pattern = form.Pattern
                PatternType = form.PatternType
                TargetField = form.TargetField
                CategoryId = form.CategoryId
                PayeeOverride = payeeOverride
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

    | InlineRuleTestDue version ->
        match model.InlineRuleForm with
        | Some form when version = model.InlineRuleTestVersion ->
            let cmd =
                Cmd.OfAsync.either
                    (Retry.reading Api.rules.testRule)
                    (form.Pattern, form.PatternType, form.TargetField, form.TransactionText)
                    (fun result -> InlineRuleTested (version, result))
                    (fun _ -> InlineRuleTestLost version)
            model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | InlineRuleTested (version, result) ->
        let test =
            match result with
            | Ok matches -> Success matches
            | Error err -> RemoteData.Failure (rulesErrorToString err)
        settleInlineRuleTest model version test, Cmd.none, NoOp

    | InlineRuleTestLost version ->
        settleInlineRuleTest model version (RemoteData.Failure ruleTestLostText), Cmd.none, NoOp

    | InlineRuleSaved (Ok rule) ->
        // Close form and apply rule to pending transactions
        { model with InlineRuleForm = None; InlineRuleTestVersion = model.InlineRuleTestVersion + 1 },
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
