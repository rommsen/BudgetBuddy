module Server.Api

open System
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api
open Shared.Domain
open Persistence
open Server.YnabClient
open Server.ComdirectClient
open Server.ComdirectAuthSession
open Server.RulesEngine
open Server.Validation
open Server.SyncSessionManager
open Server.DuplicateDetection
open Thoth.Json.Net

module RuleCommands = Server.Rules.CommandHandlers

// ============================================
// Helper Functions
// ============================================

/// Converts Result<'T, 'E list> to Result<'T, string>
let inline private combineErrors (result: Result<'T, string list>) : Result<'T, string> =
    result |> Result.mapError (String.concat "; ")

/// Converts SettingsError to string
let private settingsErrorToString (error: SettingsError) : string =
    match error with
    | SettingsError.YnabTokenInvalid msg -> $"Invalid YNAB token: {msg}"
    | SettingsError.YnabConnectionFailed (status, msg) -> $"YNAB connection failed (HTTP {status}): {msg}"
    | SettingsError.ComdirectCredentialsInvalid (field, reason) -> $"Invalid Comdirect credentials ({field}): {reason}"
    | SettingsError.EncryptionFailed msg -> $"Encryption failed: {msg}"
    | SettingsError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"

/// Converts YnabError to string
let private ynabErrorToString (error: YnabError) : string =
    match error with
    | YnabError.Unauthorized msg -> $"YNAB authorization failed: {msg}"
    | YnabError.BudgetNotFound budgetId -> $"Budget not found: {budgetId}"
    | YnabError.AccountNotFound accountId -> $"Account not found: {accountId}"
    | YnabError.CategoryNotFound categoryId -> $"Category not found: {categoryId}"
    | YnabError.RateLimitExceeded retryAfter -> $"YNAB rate limit exceeded. Retry after {retryAfter} seconds"
    | YnabError.NetworkError msg -> $"YNAB network error: {msg}"
    | YnabError.InvalidResponse msg -> $"Invalid YNAB response: {msg}"

/// Converts SyncError to string
let private syncErrorToString (error: SyncError) : string =
    match error with
    | SyncError.SessionNotFound sessionId -> $"Session not found: {sessionId}"
    | SyncError.ComdirectAuthFailed reason -> $"Comdirect authentication failed: {reason}"
    | SyncError.TanTimeout -> "TAN confirmation timed out"
    | SyncError.TransactionFetchFailed msg -> $"Failed to fetch transactions: {msg}"
    | SyncError.YnabImportFailed (count, msg) -> $"Failed to import {count} transactions: {msg}"
    | SyncError.InvalidSessionState (expected, actual) -> $"Invalid session state. Expected: {expected}, Actual: {actual}"
    | SyncError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"
    | SyncError.AskLenaRejected reason -> reason

/// Feste Meldung zu bekannten Comdirect-Fehlercodes. Der Code kommt aus `ComdirectClient`,
/// der Antwortrumpf erreicht diese Stelle nicht.
let private knownComdirectCodeMessage (code: string) : string option =
    match code with
    | "TAN_UNGUELTIG" -> Some "Please confirm the TAN in your banking app first."
    | "SESSION_EXPIRED" -> Some "Your session has expired. Please start a new sync."
    | "UNAUTHORIZED" -> Some "Authorization failed. Please check your credentials."
    | _ -> None

/// Converts ComdirectError to a message for the client: status and error code, never a response body.
let internal comdirectErrorToString (error: ComdirectError) : string =
    match error with
    | ComdirectError.AuthenticationFailed msg -> $"Authentication failed: {msg}"
    | ComdirectError.TanChallengeExpired -> "TAN challenge expired. Please start a new sync and confirm within the time limit."
    | ComdirectError.TanRejected -> "TAN was rejected. Please try again."
    | ComdirectError.SessionExpired -> "Session expired. Please start a new sync."
    | ComdirectError.InvalidCredentials -> "Invalid credentials. Please check your Comdirect settings."
    | ComdirectError.NetworkError (0, _) -> "Network error: Comdirect did not answer."
    | ComdirectError.NetworkError (status, code) ->
        match knownComdirectCodeMessage code with
        | Some message -> message
        | None when ComdirectClient.isSafeErrorCode code -> $"Network error (HTTP {status}): {code}"
        | None -> $"Network error (HTTP {status})"
    | ComdirectError.InvalidResponse msg -> $"Invalid response: {msg}"

// ============================================
// Settings API Implementation
// ============================================

/// Lädt die Einstellungen samt Secrets. Nur für den Server: startSync und der Comdirect-Import
/// brauchen die echten Zugangsdaten. An den Client geht ausschließlich `readSettingsForClient`.
let loadAppSettingsFrom (getSetting: string -> Async<string option>) : Async<AppSettings> = async {
    try
        // Load all settings from database
        let! ynabToken = getSetting "ynab_token"
        let! ynabDefaultBudgetId = getSetting "ynab_default_budget_id"
        let! ynabDefaultAccountId = getSetting "ynab_default_account_id"
        let! ynabQuickAddAccountId = getSetting "ynab_quickadd_account_id"
        let! comdirectClientId = getSetting "comdirect_client_id"
        let! comdirectClientSecret = getSetting "comdirect_client_secret"
        let! comdirectUsername = getSetting "comdirect_username"
        let! comdirectPassword = getSetting "comdirect_password"
        let! comdirectAccountId = getSetting "comdirect_account_id"
        let! syncDaysToFetch = getSetting "sync_days_to_fetch"

        let ynabSettings =
            match ynabToken with
            | Some token ->
                Some {
                    PersonalAccessToken = token
                    DefaultBudgetId = ynabDefaultBudgetId |> Option.map YnabBudgetId
                    DefaultAccountId = ynabDefaultAccountId |> Option.bind (fun id -> match Guid.TryParse(id: string) with | true, g -> Some (YnabAccountId g) | _ -> None)
                    QuickAddAccountId = ynabQuickAddAccountId |> Option.bind (fun id -> match Guid.TryParse(id: string) with | true, g -> Some (YnabAccountId g) | _ -> None)
                }
            | None -> None

        let comdirectSettings =
            match comdirectClientId, comdirectClientSecret, comdirectUsername, comdirectPassword with
            | Some clientId, Some clientSecret, Some username, Some password ->
                Some {
                    ClientId = clientId
                    ClientSecret = clientSecret
                    Username = username
                    Password = password
                    AccountId = comdirectAccountId
                }
            | _ -> None

        let syncSettings = {
            DaysToFetch = syncDaysToFetch |> Option.bind (fun s -> match Int32.TryParse(s: string) with | true, i -> Some i | _ -> None) |> Option.defaultValue 30
        }

        return {
            Ynab = ynabSettings
            Comdirect = comdirectSettings
            Sync = syncSettings
        }
    with ex ->
        printfn "Error loading settings: %s" ex.Message
        // Return empty settings on error
        return {
            Ynab = None
            Comdirect = None
            Sync = { DaysToFetch = 30 }
        }
}

let private loadAppSettings () = loadAppSettingsFrom Persistence.Settings.getSetting

/// Die Sicht für den Client: kein Secret, nur ob es gesetzt ist.
let private toSettingsView (settings: AppSettings) : AppSettingsView =
    { Ynab =
        settings.Ynab
        |> Option.map (fun ynab ->
            { HasToken = not (String.IsNullOrEmpty ynab.PersonalAccessToken)
              DefaultBudgetId = ynab.DefaultBudgetId
              DefaultAccountId = ynab.DefaultAccountId
              QuickAddAccountId = ynab.QuickAddAccountId })
      Comdirect =
        settings.Comdirect
        |> Option.map (fun comdirect ->
            { ClientId = comdirect.ClientId
              HasClientSecret = not (String.IsNullOrEmpty comdirect.ClientSecret)
              Username = comdirect.Username
              HasPassword = not (String.IsNullOrEmpty comdirect.Password)
              AccountId = comdirect.AccountId })
      Sync = settings.Sync }

let readSettingsForClient (getSetting: string -> Async<string option>) : Async<AppSettingsView> = async {
    let! settings = loadAppSettingsFrom getSetting
    return toSettingsView settings
}

/// Leere Secret-Felder bedeuten "alten Wert behalten" (infrastructure-s1r95): der Client kennt die
/// gespeicherten Secrets nicht und schickt sie deshalb ohne neue Eingabe leer.
let mergeStoredComdirectSecrets (getSetting: string -> Async<string option>) (credentials: ComdirectSettings) : Async<ComdirectSettings> = async {
    let keepStored key (entered: string) = async {
        if String.IsNullOrWhiteSpace entered then
            let! stored = getSetting key
            return stored |> Option.defaultValue entered
        else
            return entered
    }
    let! clientSecret = keepStored "comdirect_client_secret" credentials.ClientSecret
    let! password = keepStored "comdirect_password" credentials.Password
    return { credentials with ClientSecret = clientSecret; Password = password }
}

/// Speichert die Comdirect-Zugangsdaten. Validiert wird nach dem Zusammenführen mit den
/// gespeicherten Secrets: ist nichts gespeichert, bleibt es beim Validierungsfehler. Geänderte
/// Zugangsdaten trennen die Bankverbindung (ADR 0071 Punkt 2), eine andere Konto-Id nicht.
let saveComdirectCredentialsWith
    (getSetting: string -> Async<string option>)
    (setSetting: string -> string -> bool -> Async<unit>)
    (disconnect: unit -> Async<unit>)
    (credentials: ComdirectSettings)
    : Async<SettingsResult<unit>> = async {
    try
        let! merged = mergeStoredComdirectSecrets getSetting credentials
        match validateComdirectSettings merged with
        | Error errors -> return Error (SettingsError.ComdirectCredentialsInvalid ("general", String.concat "; " errors))
        | Ok valid ->
            let! storedClientId = getSetting "comdirect_client_id"
            let! storedClientSecret = getSetting "comdirect_client_secret"
            let! storedUsername = getSetting "comdirect_username"
            let! storedPassword = getSetting "comdirect_password"
            let credentialsChanged =
                (storedClientId, storedClientSecret, storedUsername, storedPassword)
                <> (Some valid.ClientId, Some valid.ClientSecret, Some valid.Username, Some valid.Password)

            do! setSetting "comdirect_client_id" valid.ClientId false
            do! setSetting "comdirect_client_secret" valid.ClientSecret true
            do! setSetting "comdirect_username" valid.Username false
            do! setSetting "comdirect_password" valid.Password true
            match valid.AccountId with
            | Some accountId -> do! setSetting "comdirect_account_id" accountId false
            | None -> ()
            if credentialsChanged then
                do! disconnect ()
            return Ok ()
    with ex ->
        return Error (SettingsError.DatabaseError ("save_comdirect_credentials", ex.Message))
}

/// Verbindungstest (ADR 0071 Punkt 2): bei `Connected` liest er die Konten ohne Challenge, sonst
/// startet er als Handlung von Roman eine Push-TAN.
let testComdirectConnectionWith
    (getSetting: string -> Async<string option>)
    (ports: ComdirectAuthSession.ConnectionPorts)
    (now: DateTimeOffset)
    : Async<SettingsResult<ComdirectConnectionTest>> = async {
    match ComdirectAuthSession.connectionState ports with
    | BankConnection.Connected _ ->
        match! ComdirectAuthSession.fetchAccountsWith ports now with
        | Ok _ -> return Ok BankConnectionActive
        | Error comdirectError ->
            return Error (SettingsError.ComdirectCredentialsInvalid ("accounts", comdirectErrorToString comdirectError))
    | BankConnection.Disconnected
    | BankConnection.ChallengePending _ ->
        // Get Comdirect credentials from settings
        let! clientId = getSetting "comdirect_client_id"
        let! clientSecret = getSetting "comdirect_client_secret"
        let! username = getSetting "comdirect_username"
        let! password = getSetting "comdirect_password"
        let! accountId = getSetting "comdirect_account_id"

        match clientId, clientSecret, username, password with
        | Some cid, Some cs, Some un, Some pw ->
            let credentials: ComdirectSettings = {
                ClientId = cid
                ClientSecret = cs
                Username = un
                Password = pw
                AccountId = accountId
            }
            match! ComdirectAuthSession.startChallengeWith ports credentials with
            | Error comdirectError ->
                return Error (SettingsError.ComdirectCredentialsInvalid ("auth", comdirectErrorToString comdirectError))
            | Ok challenge ->
                return Ok (TanChallengeStarted challenge.Id)
        | _ ->
            return Error (SettingsError.ComdirectCredentialsInvalid ("credentials", "Comdirect credentials not configured"))
}

let settingsApi : SettingsApi = {
    getLenaFlagSettings = fun () -> Ledger.QueryHandlers.getLenaFlagSettings ()

    saveLenaFlagColor = fun color -> async {
        try
            match! Ledger.CommandHandlers.saveLenaFlagColor color with
            | Ok () -> return Ok ()
            | Error msg -> return Error (SettingsError.DatabaseError ("validation", msg))
        with ex ->
            return Error (SettingsError.DatabaseError ("save_lena_flag_color", ex.Message))
    }

    getSettings = fun () -> readSettingsForClient Persistence.Settings.getSetting

    saveYnabToken = fun token -> async {
        // Validate
        match validateYnabToken token with
        | Error errors -> return Error (SettingsError.YnabTokenInvalid (String.concat "; " errors))
        | Ok validToken ->
            // Test token by fetching budgets
            match! YnabClient.getBudgets validToken with
            | Error ynabError ->
                return Error (SettingsError.YnabConnectionFailed (0, ynabErrorToString ynabError))
            | Ok _ ->
                // Token is valid, save it
                try
                    do! Persistence.Settings.setSetting "ynab_token" validToken true
                    return Ok ()
                with ex ->
                    return Error (SettingsError.DatabaseError ("save_ynab_token", ex.Message))
    }

    saveComdirectCredentials =
        saveComdirectCredentialsWith
            Persistence.Settings.getSetting
            Persistence.Settings.setSetting
            ComdirectAuthSession.disconnect

    saveSyncSettings = fun settings -> async {
        // Validate
        match validateSyncSettings settings with
        | Error errors -> return Error (SettingsError.DatabaseError ("validation", String.concat "; " errors))
        | Ok valid ->
            try
                do! Persistence.Settings.setSetting "sync_days_to_fetch" (string valid.DaysToFetch) false
                return Ok ()
            with ex ->
                return Error (SettingsError.DatabaseError ("save_sync_settings", ex.Message))
    }

    testYnabConnection = fun () -> async {
        // Get token from settings
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        match tokenOpt with
        | None ->
            return Error (SettingsError.YnabTokenInvalid "No YNAB token configured")
        | Some token ->
            // Get all budgets with details
            match! YnabClient.getBudgets token with
            | Error ynabError ->
                return Error (SettingsError.YnabConnectionFailed (0, ynabErrorToString ynabError))
            | Ok budgets ->
                // Fetch details for each budget
                let! budgetsWithDetails = async {
                    let mutable results = []
                    for budget in budgets do
                        match! YnabClient.getBudgetWithAccounts token budget.Id with
                        | Ok details ->
                            results <- details :: results
                        | Error _ ->
                            () // Skip budgets that fail to load
                    return results |> List.rev
                }
                return Ok budgetsWithDetails
    }

    testComdirectConnection = fun () ->
        testComdirectConnectionWith
            Persistence.Settings.getSetting
            ComdirectAuthSession.livePorts
            DateTimeOffset.UtcNow

    confirmComdirectTan = fun () -> async {
        // Complete TAN flow
        match! ComdirectAuthSession.confirmTan() with
        | Error comdirectError ->
            return Error (SettingsError.ComdirectCredentialsInvalid ("tan", comdirectErrorToString comdirectError))
        | Ok _ ->
            // TAN confirmed successfully, credentials are valid
            return Ok ()
    }
}

// ============================================
// YNAB API Implementation
// ============================================

let ynabApi : YnabApi = {
    getBudgets = fun () -> async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        match tokenOpt with
        | None -> return Error (YnabError.Unauthorized "No YNAB token configured")
        | Some token ->
            return! YnabClient.getBudgets token
    }

    getBudgetDetails = fun budgetId -> async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        match tokenOpt with
        | None -> return Error (YnabError.Unauthorized "No YNAB token configured")
        | Some token ->
            return! YnabClient.getBudgetWithAccounts token budgetId
    }

    getCategories = fun budgetId -> async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        match tokenOpt with
        | None -> return Error (YnabError.Unauthorized "No YNAB token configured")
        | Some token ->
            match! YnabClient.getCategories token budgetId with
            | Error e -> return Error e
            | Ok categories ->
                // The internal "Inflow: Ready to Assign" category's own balance is a
                // garbage accumulator; override it with the month's real to_be_budgeted.
                let! toBeBudgeted = YnabClient.getReadyToAssign token budgetId
                return Ok (categoriesForPicker toBeBudgeted categories)
    }

    getPayees = fun budgetId -> async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        match tokenOpt with
        | None -> return Error (YnabError.Unauthorized "No YNAB token configured")
        | Some token ->
            return! YnabClient.getPayees token budgetId
    }

    setDefaultBudget = fun budgetId -> async {
        try
            let (YnabBudgetId id) = budgetId
            do! Persistence.Settings.setSetting "ynab_default_budget_id" id false
            return Ok ()
        with ex ->
            return Error (YnabError.NetworkError $"Failed to save default budget: {ex.Message}")
    }

    setDefaultAccount = fun accountId -> async {
        try
            let (YnabAccountId id) = accountId
            do! Persistence.Settings.setSetting "ynab_default_account_id" (id.ToString()) false
            return Ok ()
        with ex ->
            return Error (YnabError.NetworkError $"Failed to save default account: {ex.Message}")
    }

    setQuickAddAccount = fun accountId -> async {
        try
            let (YnabAccountId id) = accountId
            do! Persistence.Settings.setSetting "ynab_quickadd_account_id" (id.ToString()) false
            return Ok ()
        with ex ->
            return Error (YnabError.NetworkError $"Failed to save Quick Add account: {ex.Message}")
    }

    addManualTransaction = fun request -> async {
        match Validation.validateManualTransaction request with
        | Error errors ->
            return Error (YnabError.InvalidResponse (String.concat ", " errors))
        | Ok validRequest ->
            let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
            let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
            // Deliberately NOT the bank-import default account: Quick Add is for
            // cash expenses, which belong in their own (configurable) account.
            let! accountOpt = Persistence.Settings.getSetting "ynab_quickadd_account_id"

            match tokenOpt, budgetOpt, accountOpt with
            | None, _, _ ->
                return Error (YnabError.Unauthorized "No YNAB token configured")
            | _, None, _ ->
                return Error (YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)")
            | _, _, None ->
                return Error (YnabError.AccountNotFound "Kein Quick-Add-Konto konfiguriert. Wähle es in den Settings unter YNAB aus.")
            | Some token, Some budgetId, Some accountIdStr ->
                match System.Guid.TryParse accountIdStr with
                | false, _ ->
                    return Error (YnabError.AccountNotFound accountIdStr)
                | true, accountGuid ->
                    let ynabAccountId = YnabAccountId accountGuid

                    // ADR 0021: erst Kassenbuch, dann Push. Ohne ein gespiegeltes
                    // Kassenbuch-Konto fuer das Quick-Add-Konto (der Spiegel ist noch nie
                    // gelaufen) kann die Buchung nicht zuerst im Kassenbuch entstehen; kein
                    // Fallback auf ein zweites, unverknuepftes "Bar"-Konto (ADR 0016), kein
                    // stiller Push ohne Kassenbuch-Eintrag, sondern eine klare Fehlermeldung
                    // an Roman.
                    match! Ledger.Persistence.findAccountByYnabAccountId ynabAccountId with
                    | None ->
                        return
                            Error (
                                YnabError.AccountNotFound
                                    "Das Quick-Add-Konto ist im Kassenbuch noch nicht bekannt. Einmal \"Jetzt spiegeln\" ausführen, dann erneut versuchen."
                            )
                    | Some ledgerAccount ->
                        let! category = async {
                            match validRequest.CategoryId with
                            | None -> return None
                            | Some categoryId ->
                                match! YnabClient.getCategories token (YnabBudgetId budgetId) with
                                | Ok categories ->
                                    return
                                        categories
                                        |> List.tryFind (fun cat -> cat.Id = categoryId)
                                        |> Option.map (fun cat ->
                                            ({ CategoryId = cat.Id; CategoryName = cat.Name } : Ledger.Events.CategoryRef))
                                | Error _ -> return None
                        }

                        let signedAmount = if validRequest.IsOutflow then -validRequest.Amount else validRequest.Amount
                        let payee =
                            if String.IsNullOrWhiteSpace validRequest.PayeeName then None
                            else Some (validRequest.PayeeName.Trim())
                        let memo =
                            validRequest.Memo
                            |> Option.filter (fun m -> not (String.IsNullOrWhiteSpace m))
                            |> Option.map (fun m -> m.Trim())

                        let input : Ledger.CommandHandlers.CashExpenseInput = {
                            AccountId = ledgerAccount.Id
                            Date = validRequest.Date
                            Amount = { Amount = signedAmount; Currency = "EUR" }
                            Payee = payee
                            Memo = memo
                            Category = category
                        }

                        let push () =
                            YnabClient.createManualTransaction token (YnabBudgetId budgetId) ynabAccountId validRequest

                        match!
                            Ledger.CommandHandlers.addCashExpense
                                Ledger.Events.QuickAdd
                                DateTime.UtcNow
                                push
                                Ledger.CommandHandlers.triggerMirrorInBackground
                                input
                        with
                        | Ok _entryId -> return Ok ()
                        | Error err -> return Error err
    }

    getRecentQuickAddTemplates = fun () -> async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
        let! accountOpt = Persistence.Settings.getSetting "ynab_quickadd_account_id"

        match tokenOpt, budgetOpt, accountOpt with
        | Some token, Some budgetId, Some accountIdStr ->
            match System.Guid.TryParse accountIdStr with
            // Misconfigured account id → no templates, but never crash the page.
            | false, _ -> return Ok []
            | true, accountGuid ->
                // One read per page visit; ~90 days back is plenty of history for
                // recurring cash bookings while staying well within YNAB's rate
                // limit (~200 req/h). The dedup/projection is pure (Domain).
                match! YnabClient.getAccountTransactions token (YnabBudgetId budgetId) (YnabAccountId accountGuid) 90 with
                | Ok transactions -> return Ok (recentQuickAddTemplates 5 transactions)
                | Error err -> return Error err
        | _ ->
            // Not configured (no token / budget / Quick-Add account) → no
            // templates. Mirror ADR 0004's no-fallback stance: never invent an
            // account; the form stays fully usable without templates.
            return Ok []
    }
}

// ============================================
// Rules API Implementation
// ============================================

let private liveCategoryPort : RuleCommands.YnabPort =
    RuleCommands.ynabPort Persistence.Settings.getSetting YnabClient.getCategories

/// Der Kategoriename für die Sync-Abläufe: ein gescheitertes Nachschlagen ergibt keinen Namen.
let private categoryNameIfKnown (categoryId: YnabCategoryId) : Async<string option> =
    async {
        let! name = RuleCommands.lookupCategoryName liveCategoryPort categoryId
        return Result.toOption name
    }

let rulesApi : RulesApi = {
    getAllRules = fun () -> async {
        return! Persistence.Rules.getAllRules()
    }

    getRule = fun ruleId -> async {
        try
            match! Persistence.Rules.getRuleById ruleId with
            | Some rule -> return Ok rule
            | None ->
                let (RuleId id) = ruleId
                return Error (RulesError.RuleNotFound id)
        with ex ->
            return Error (RulesError.DatabaseError ("get_rule", ex.Message))
    }

    createRule = fun request -> RuleCommands.createRule liveCategoryPort DateTime.UtcNow request

    updateRule = fun request -> RuleCommands.changeRule liveCategoryPort DateTime.UtcNow request

    deleteRule = fun ruleId -> async {
        try
            do! Persistence.Rules.deleteRule ruleId
            return Ok ()
        with ex ->
            return Error (RulesError.DatabaseError ("delete_rule", ex.Message))
    }

    reorderRules = fun ruleIds -> async {
        try
            do! Persistence.Rules.updatePriorities ruleIds
            return Ok ()
        with ex ->
            return Error (RulesError.DatabaseError ("reorder_rules", ex.Message))
    }

    exportRules = fun () -> async {
        let! rules = Persistence.Rules.getAllRules()

        // Serialize to JSON
        let json = Encode.Auto.toString(4, rules)
        return json
    }

    importRules = fun json -> RuleCommands.importRules Guid.NewGuid DateTime.UtcNow json

    testRule = fun (pattern, patternType, targetField, testInput) -> async {
        let testRule = {
            Id = RuleId Guid.Empty
            Name = "Test"
            Pattern = pattern
            PatternType = patternType
            TargetField = targetField
            CategoryId = YnabCategoryId Guid.Empty
            CategoryName = "Test"
            PayeeOverride = None
            Priority = 0
            Enabled = true
            CreatedAt = DateTime.UtcNow
            UpdatedAt = DateTime.UtcNow
        }

        match compileRule testRule with
        | Error reason -> return Error (RulesError.InvalidPattern (pattern, reason))
        | Ok compiled ->
            // Create a test transaction
            let testTransaction = {
                Id = TransactionId "test"
                BookingDate = DateTime.UtcNow
                Amount = { Amount = 0m; Currency = "EUR" }
                Payee = Some testInput
                Memo = testInput
                Reference = "test"
                RawData = ""
            }

            let matchText = getMatchText testTransaction targetField
            return Ok (compiled.Regex.IsMatch(matchText))
    }
}

// ============================================
// Comdirect-Import-Kandidaten (ledger-w6tbq)
// ============================================

/// Baut aus einem `prepareTransactions`-Ergebnis die Kassenbuch-Kandidaten für einen
/// Comdirect-Import-Push. Einzige Stelle, die `ExistingEntryImportId` setzt: vorher
/// stand dieser gut 20-zeilige Block doppelt im normalen Import-Push und in
/// Force-Import und unterschied sich in genau diesem einen Feld (Verifier-Fund).
/// `forceImport = false` (normaler Push): Suche und Versand teilen sich dieselbe
/// ImportId (`ExistingEntryImportId = None`, ledger-fq3kd). `forceImport = true`:
/// `prepared` trägt schon die von `prepareTransactions ... true` neu erzeugte
/// ImportId zum Versand (YNABs Dedup); die Suche nach der schon bestehenden
/// Kassenbuch-Buchung muss aber über die alte, deterministische ImportId laufen
/// (`Shared.Domain.generateImportId`), denn genau darüber hat der normale Push sie
/// ursprünglich angelegt (ADR 0021, ADR 0015: die Buchung ist nur über ihre
/// Fremd-Id erreichbar, solange keine YNAB-Id verknüpft ist).
/// Payee/Memo kommen aus dem tatsächlich vorbereiteten Request (`p.Request`), nicht
/// aus den rohen Comdirect-Feldern (Verifier-Fund, ledger-qvt84 Iteration 3):
/// YNAB bekommt PayeeName (Override-Fallback "Unknown") und Memo über
/// `buildMemoWithReference` (immer mit ", Ref: <Referenz>"-Suffix); schreibt die
/// Entstehung stattdessen die rohen Werte, sieht der nächste Spiegel-Lauf ein
/// falsches Kopfdaten-Ereignis. Die rohe Comdirect-Reference bleibt unverändert in
/// ihrem eigenen Feld (`ComdirectReference`). Pure, kein I/O.
let buildComdirectImportCandidates
    (forceImport: bool)
    (transactions: SyncTransaction list)
    (prepared: PrepareResult)
    : Ledger.CommandHandlers.ComdirectImportCandidate list =
    let byTransactionId =
        transactions |> List.map (fun tx -> tx.Transaction.Id, tx) |> Map.ofList
    prepared.ToSend
    |> List.map (fun p ->
        let tx = Map.find p.TransactionId byTransactionId
        ({
            Prepared = p
            ComdirectReference = tx.Transaction.Reference
            Date = tx.Transaction.BookingDate
            Amount = tx.Transaction.Amount
            Payee = Some p.Request.PayeeName
            // Der rohe Bank-Payee, unabhängig von einem Override (ledger-jkb0f):
            // `p.Request.PayeeName` hat einen zu diesem Zeitpunkt aktiven Override
            // schon eingebacken, taugt also nicht als Fallback, wenn ein späterer
            // Review-Schritt genau diesen Override wieder entfernt.
            RawPayee = tx.Transaction.Payee
            Memo = Some p.Request.Memo
            ReviewSteps = tx.ReviewSteps
            ExistingEntryImportId =
                if forceImport then
                    Some (Shared.Domain.generateImportId p.TransactionId)
                else
                    None
        } : Ledger.CommandHandlers.ComdirectImportCandidate))

// ============================================
// Sync API Implementation
// ============================================

/// Bricht den Sync ab. Eine offene Challenge verfällt, eine aktive Bankverbindung bleibt stehen
/// (ADR 0071 Punkt 2).
let cancelSyncWith
    (validateSession: SyncSessionId -> Result<'Session, SyncError>)
    (discardPendingChallenge: unit -> Async<unit>)
    (clearSyncSession: unit -> unit)
    (sessionId: SyncSessionId)
    : Async<SyncResult<unit>> = async {
    match validateSession sessionId with
    | Error err -> return Error err
    | Ok _ ->
        try
            do! discardPendingChallenge ()
            clearSyncSession ()
            return Ok ()
        with ex ->
            return Error (SyncError.DatabaseError ("cancel_sync", ex.Message))
}

let syncApi : SyncApi = {
    // ============================================
    // Session management
    // ============================================

    startSync = fun () -> async {
        try
            // First check if Comdirect credentials are configured
            let! settings = loadAppSettings ()
            match settings.Comdirect with
            | None ->
                return Error (SyncError.ComdirectAuthFailed "Comdirect credentials not configured. Please configure them in Settings first.")
            | Some _ ->
                // Create new session
                let session = SyncSessionManager.startNewSession()

                // Persist session to database
                do! Persistence.SyncSessions.createSession session

                return Ok session
        with ex ->
            return Error (SyncError.DatabaseError ("start_sync", ex.Message))
    }

    getCurrentSession = fun () -> async {
        return SyncSessionManager.getCurrentSession()
    }

    cancelSync =
        cancelSyncWith
            SyncSessionManager.validateSession
            ComdirectAuthSession.discardPendingChallenge
            SyncSessionManager.clearSession

    // ============================================
    // Comdirect auth flow
    // ============================================

    initiateComdirectAuth = fun sessionId -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            // Get Comdirect credentials from settings
            let! settings = loadAppSettings ()

            match settings.Comdirect with
            | None ->
                return Error (SyncError.ComdirectAuthFailed "Comdirect credentials not configured")
            | Some credentials ->
                // Update session status
                SyncSessionManager.updateSessionStatus AwaitingTan

                // Start auth flow
                match! ComdirectAuthSession.startAuth credentials with
                | Error comdirectError ->
                    let errorMsg = comdirectErrorToString comdirectError
                    SyncSessionManager.failSession errorMsg
                    return Error (SyncError.ComdirectAuthFailed errorMsg)
                | Ok challenge ->
                    // Return challenge info
                    return Ok challenge.Id
    }

    confirmTan = fun sessionId -> async {
        match SyncSessionManager.validateSessionStatus sessionId AwaitingTan with
        | Error err -> return Error err
        | Ok _ ->
            // Complete TAN flow
            match! ComdirectAuthSession.confirmTan() with
            | Error comdirectError ->
                let errorMsg = comdirectErrorToString comdirectError
                SyncSessionManager.failSession errorMsg
                return Error (SyncError.ComdirectAuthFailed errorMsg)
            | Ok _ ->
                // Update session status
                SyncSessionManager.updateSessionStatus FetchingTransactions

                // Get sync settings
                let! settings = loadAppSettings ()

                // Get account ID from settings
                let accountId =
                    settings.Comdirect
                    |> Option.bind (fun c -> c.AccountId)
                    |> Option.defaultValue ""

                // Fetch transactions
                match! ComdirectAuthSession.fetchTransactions accountId settings.Sync.DaysToFetch DateTime.Today with
                | Error comdirectError ->
                    let errorMsg = comdirectErrorToString comdirectError
                    SyncSessionManager.failSession errorMsg
                    return Error (SyncError.TransactionFetchFailed errorMsg)
                | Ok bankTransactions ->
                    // Apply rules engine
                    let! allRules = Persistence.Rules.getAllRules()
                    match classifyTransactions DateTime.UtcNow allRules bankTransactions with
                    | Error errors ->
                        let errorMsg = String.concat "; " errors
                        SyncSessionManager.failSession errorMsg
                        return Error (SyncError.TransactionFetchFailed errorMsg)
                    | Ok syncTransactions ->
                        // Detect duplicates and apply Order-ID suggestions by fetching existing YNAB transactions
                        let! syncTransactionsWithDuplicatesAndSuggestions = async {
                            let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
                            let! budgetIdOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
                            let! accountIdOpt = Persistence.Settings.getSetting "ynab_default_account_id"

                            match tokenOpt, budgetIdOpt, accountIdOpt with
                            | Some token, Some budgetId, Some accountIdStr ->
                                match Guid.TryParse(accountIdStr: string) with
                                | true, accountIdGuid ->
                                    // Fetch existing YNAB transactions for duplicate detection
                                    match! YnabClient.getAccountTransactions token (YnabBudgetId budgetId) (YnabAccountId accountIdGuid) (settings.Sync.DaysToFetch + 7) with
                                    | Ok ynabTransactions ->
                                        // Mark duplicates
                                        let withDuplicates = DuplicateDetection.markDuplicates ynabTransactions syncTransactions
                                        // Apply Order-ID based category suggestions from YNAB history
                                        let orderIdMap = OrderIdMatcher.buildYnabOrderIdMap ynabTransactions
                                        return OrderIdMatcher.applySuggestions DateTime.UtcNow orderIdMap withDuplicates
                                    | Error _ ->
                                        // If we can't fetch YNAB transactions, proceed without duplicate detection
                                        return syncTransactions
                                | false, _ ->
                                    return syncTransactions
                            | _ ->
                                // YNAB not configured, proceed without duplicate detection
                                return syncTransactions
                        }

                        // Auto-skip confirmed duplicates
                        let syncTransactionsWithAutoSkip =
                            syncTransactionsWithDuplicatesAndSuggestions
                            |> List.map (fun tx ->
                                match tx.DuplicateStatus with
                                | ConfirmedDuplicate _ -> { tx with Status = Skipped }
                                | _ -> tx
                            )

                        // Add transactions to session
                        SyncSessionManager.addTransactions syncTransactionsWithAutoSkip

                        // Update session status
                        SyncSessionManager.updateSessionStatus ReviewingTransactions

                        // Update session in database
                        match SyncSessionManager.getCurrentSession() with
                        | Some session ->
                            do! Persistence.SyncSessions.updateSession session
                            return Ok ()
                        | None ->
                            return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
    }

    // ============================================
    // Transaction operations
    // ============================================

    getTransactions = fun sessionId -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            let transactions = SyncSessionManager.getTransactions()
            // Re-generate ExternalLinks if empty (lost after DB reload)
            let enriched =
                transactions
                |> List.map (fun tx ->
                    if tx.ExternalLinks.IsEmpty then
                        { tx with ExternalLinks = RulesEngine.detectSpecialTransaction tx.Transaction }
                    else tx)
            return Ok enriched
    }

    categorizeTransaction = fun (sessionId, txId, categoryId, payeeOverride) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            match SyncSessionManager.getTransaction txId with
            | None -> return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
            | Some tx ->
                // Get category name if category ID provided
                let! categoryName = async {
                    match categoryId with
                    | Some catId -> return! categoryNameIfKnown catId
                    | None -> return None
                }

                // Same timestamp for the source transaction's own step and for anything
                // propagated from it below: both are part of this one categorization action.
                let now = DateTime.UtcNow

                let updated =
                    { tx with
                        Status = ManualCategorized
                        CategoryId = categoryId
                        CategoryName = categoryName
                        PayeeOverride = payeeOverride
                        SuggestedByOrderId = None  // Manual override clears any suggestion
                    }
                    |> appendReviewStep now ByUser

                SyncSessionManager.updateTransaction updated

                // Propagate category to other transactions with the same Amazon Order ID
                let allTransactions = SyncSessionManager.getTransactions()
                let propagated = OrderIdMatcher.propagateInSession now updated allTransactions
                for propagatedTx in propagated do
                    SyncSessionManager.updateTransaction propagatedTx

                return Ok (updated :: propagated)
    }

    skipTransaction = fun (sessionId, txId) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            match SyncSessionManager.getTransaction txId with
            | None -> return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
            | Some tx ->
                // Überspringen hebt eine gesetzte Rückfrage Lena auf (ynab-sync-d9e51).
                let updated = ImportAskLena.skip tx
                SyncSessionManager.updateTransaction updated
                return Ok updated
    }

    unskipTransaction = fun (sessionId, txId) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            match SyncSessionManager.getTransaction txId with
            | None -> return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
            | Some tx ->
                let newStatus =
                    if tx.CategoryId.IsSome then ManualCategorized
                    else Pending
                let updated = { tx with Status = newStatus }
                SyncSessionManager.updateTransaction updated
                return Ok updated
    }

    bulkCategorize = fun (sessionId, txIds, categoryId) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            // Get category name
            let! categoryName = categoryNameIfKnown categoryId

            // Update all transactions
            let mutable updatedTransactions = []
            for txId in txIds do
                match SyncSessionManager.getTransaction txId with
                | Some tx ->
                    let updated =
                        { tx with
                            Status = ManualCategorized
                            CategoryId = Some categoryId
                            CategoryName = categoryName
                            SuggestedByOrderId = None  // Manual override clears any suggestion
                        }
                        |> appendReviewStep DateTime.UtcNow ByUser
                    SyncSessionManager.updateTransaction updated
                    updatedTransactions <- updated :: updatedTransactions
                | None -> ()

            return Ok (List.rev updatedTransactions)
    }

    splitTransaction = fun (sessionId, txId, splits) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            match SyncSessionManager.getTransaction txId with
            | None -> return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
            | Some tx ->
                // Re-validate the structural split invariants server-side via the
                // shared smart-constructor (ADR 0006): >= 2 lines, one currency, and
                // the sign-correct sum (transfer lines included) equals the total.
                match mkSplits tx.Transaction.Amount splits with
                | Error (TooFewLines _) ->
                    return Error (SyncError.InvalidSessionState ("split", "Eine Aufteilung braucht mindestens 2 Positionen"))
                | Error CurrencyMismatch ->
                    return Error (SyncError.InvalidSessionState ("split", "Alle Positionen müssen die Währung der Buchung haben"))
                | Error (SumMismatch (expected, actual)) ->
                    return Error (SyncError.InvalidSessionState ("split", $"Die Positionen ergeben {actual}, die Buchung beträgt aber {expected}"))
                | Ok validatedSplits ->
                    let updated =
                        { tx with
                            Status = ManualCategorized
                            CategoryId = None  // No single category for split transactions
                            CategoryName = None
                            Splits = Some validatedSplits
                        }
                        |> appendReviewStep DateTime.UtcNow ByUser
                    SyncSessionManager.updateTransaction updated
                    return Ok updated
    }

    clearSplit = fun (sessionId, txId) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            match SyncSessionManager.getTransaction txId with
            | None -> return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
            | Some tx ->
                let updated =
                    { tx with
                        Status = Pending  // Reset to pending so user can categorize
                        Splits = None
                    }
                    |> appendReviewStep DateTime.UtcNow ByUser
                SyncSessionManager.updateTransaction updated
                return Ok updated
    }

    // "Lena fragen" an der Importzeile (ynab-sync-d9e51, ADR 0056): der Zustand hängt an
    // der Sitzungs-Transaktion, die Flag-Farbe kommt aus den Settings.
    setAskLena = fun (sessionId, txId, askLena) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            match SyncSessionManager.getTransaction txId with
            | None -> return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
            | Some tx ->
                let! lenaColor = Persistence.Settings.getSetting Ledger.CommandHandlers.lenaFlagColorSettingKey
                match ImportAskLena.setAskLena lenaColor askLena tx with
                | Error reason -> return Error (SyncError.AskLenaRejected reason)
                | Ok updated ->
                    SyncSessionManager.updateTransaction updated
                    return Ok updated
    }

    // ============================================
    // Import
    // ============================================

    importToYnab = fun sessionId -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            // Get all transactions
            let transactions = SyncSessionManager.getTransactions()

            // Filter to transactions ready for import (category is optional)
            let toImport =
                transactions
                |> List.filter (fun tx ->
                    match tx.Status with
                    | AutoCategorized | ManualCategorized | NeedsAttention | Pending ->
                        true  // Category is optional - uncategorized will appear in YNAB's Uncategorized view
                    | _ -> false
                )

            if toImport.IsEmpty then
                return Ok { CreatedCount = 0; DuplicateTransactionIds = []; LenaFlagNotSentCount = 0 }
            else
                // Get YNAB settings
                let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
                let! defaultBudgetIdOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
                let! defaultAccountIdOpt = Persistence.Settings.getSetting "ynab_default_account_id"

                match tokenOpt, defaultBudgetIdOpt, defaultAccountIdOpt with
                | None, _, _ | _, None, _ | _, _, None ->
                    return Error (SyncError.YnabImportFailed (0, "YNAB not fully configured"))
                | Some token, Some budgetId, Some accountIdText ->
                    let ynabAccountId = YnabAccountId (Guid.Parse(accountIdText: string))

                    // ADR 0021: erst Kassenbuch, dann Push. Ohne ein gespiegeltes
                    // Kassenbuch-Konto für das Import-Konto (der Spiegel ist noch nie
                    // gelaufen) kann keine Buchung zuerst im Kassenbuch entstehen; kein
                    // Fallback auf ein erfundenes Konto, sondern eine klare Fehlermeldung
                    // an Roman (konsistent mit Quick Add, ledger-tdnpk).
                    match! Ledger.Persistence.findAccountByYnabAccountId ynabAccountId with
                    | None ->
                        return
                            Error (
                                SyncError.YnabImportFailed (
                                    toImport.Length,
                                    "Kein gespiegeltes Kassenbuch-Konto für das Import-Konto. Einmal \"Jetzt spiegeln\" ausführen, dann erneut versuchen."
                                )
                            )
                    | Some ledgerAccount ->
                        match! resolveTransferPayees token (YnabBudgetId budgetId) toImport with
                        | Error ynabError ->
                            return Error (SyncError.YnabImportFailed (toImport.Length, ynabErrorToString ynabError))
                        | Ok transferPayees ->
                            let today = DateTime.Today
                            let prepared0 =
                                prepareTransactions today ynabAccountId transferPayees false toImport

                            // Rückfrage Lena (ynab-sync-d9e51): die Farbe wird erst jetzt aus
                            // den Settings gelesen, nicht beim Setzen der Rückfrage. Fehlt sie
                            // inzwischen, geht die Zeile ohne Flag und der Bericht nennt das.
                            let! storedLenaColor =
                                Persistence.Settings.getSetting Ledger.CommandHandlers.lenaFlagColorSettingKey
                            let lenaColor = ImportAskLena.validColor storedLenaColor
                            let prepared, lenaFlagNotSent = applyLenaFlag lenaColor toImport prepared0

                            // Teil 2 (ADR 0021): je vorbereiteter Buchung die Rohdaten und
                            // die Review-Spur (categorization-gdrx8) mitgeben, aus der das
                            // Kassenbuch die Kategorisierungs-Ereignisse baut. Normaler
                            // Import-Push (kein Force-Import): Suche und Versand teilen
                            // sich dieselbe ImportId (`buildComdirectImportCandidates`,
                            // ledger-w6tbq, ehemals dieser Block doppelt).
                            let candidates =
                                buildComdirectImportCandidates false toImport prepared

                            let now = DateTime.UtcNow
                            let push preparedList = sendPrepared token (YnabBudgetId budgetId) preparedList

                            // Kassenbuch zuerst, dann Push, dann Verknüpfung (ADR 0021,
                            // Task-Notes Schritt 7/8): die eigentliche Reihenfolge lebt in
                            // `Ledger.CommandHandlers.pushComdirectImports`, injiziert und
                            // ohne Netz testbar (Muster wie `addCashExpense`).
                            let! pushResult =
                                Ledger.CommandHandlers.pushComdirectImports
                                    now
                                    push
                                    Ledger.CommandHandlers.triggerMirrorInBackground
                                    ledgerAccount.Id
                                    candidates

                            match pushResult.SendResult with
                            | Some (Error ynabError) ->
                                // ADR 0021 Punkt 2: kein Rollback. Die schon geschriebenen
                                // Kassenbuch-Buchungen bleiben unverändert und unverknüpft
                                // stehen; der API-Aufruf meldet den Push-Fehler trotzdem.
                                return
                                    Error (SyncError.YnabImportFailed (candidates.Length, ynabErrorToString ynabError))
                            | _ ->
                                // Outcome-Klassifikation (welche Buchung importiert,
                                // Duplikat oder gescheitert ist, und ob die Session
                                // abgeschlossen werden darf) liegt in der reinen, unter
                                // `dotnet test` erreichbaren Funktion
                                // `Server.PushOutcomeClassification.classifyPushOutcomes`
                                // (ledger-na3rq), dieselbe wie in `forceImportDuplicates`.
                                // `forceImport = false`, `otherSessionTransactions = []`:
                                // `toImport` ist bereits "alles gerade Offene" der Session
                                // (siehe Filter oben), die session-weite Force-Regel greift
                                // hier nicht (siehe Doc-Kommentar an `classifyPushOutcomes`).
                                let classification =
                                    Server.PushOutcomeClassification.classifyPushOutcomes toImport prepared pushResult false []

                                SyncSessionManager.updateTransactions classification.UpdatedTransactions

                                // Session darf nur abschließen, wenn dieser Versuch weder
                                // ein Duplikat noch ein gescheitertes Kassenbuch-Schreiben
                                // hinterlassen hat (`SessionMayComplete`, ein Feld der
                                // Klassifikation, keine hier wiederholte Bedingung).
                                if classification.SessionMayComplete then
                                    SyncSessionManager.completeSession()

                                // Update session in database. Persisted regardless of a
                                // ledger write failure: transactions that DID succeed
                                // (Linked/AlreadyImported/duplicates) must not lose their
                                // status just because another transaction in the same
                                // batch failed to write.
                                match SyncSessionManager.getCurrentSession() with
                                | Some session ->
                                    do! Persistence.SyncSessions.updateSession session

                                    // Save all transactions to database
                                    for tx in transactions do
                                        do! Persistence.SyncTransactions.saveTransaction session.Id tx

                                    let createdCount =
                                        pushResult.Outcomes
                                        |> Map.toList
                                        |> List.filter (fun (_, outcome) ->
                                            match outcome with
                                            | Ledger.CommandHandlers.Linked _ -> true
                                            | _ -> false)
                                        |> List.length

                                    // Kassenbuch-Scheitern und nicht auswertbare YNAB-Antwort
                                    // sichtbar machen statt still schlucken (Verifier-Fund,
                                    // Iteration 3; ledger-fgw2q): die erfolgreich importierten
                                    // Buchungen sind oben schon gespeichert.
                                    match Server.PushOutcomeClassification.pushFailureError classification with
                                    | Some err -> return Error err
                                    | None ->
                                        // Return ImportResult with created count and duplicate IDs
                                        return Ok {
                                            CreatedCount = createdCount
                                            DuplicateTransactionIds = classification.DuplicateTransactionIds
                                            LenaFlagNotSentCount = lenaFlagNotSent
                                        }
                                | None ->
                                    return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
    }

    // ============================================
    // Force Import Duplicates
    // ============================================

    forceImportDuplicates = fun (sessionId, transactionIds) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            // Get all transactions and filter to only the specified ones
            let allTransactions = SyncSessionManager.getTransactions()
            let transactionsToForce =
                allTransactions
                |> List.filter (fun tx -> transactionIds |> List.contains tx.Transaction.Id)

            if transactionsToForce.IsEmpty then
                return Ok 0
            else
                // Get YNAB settings
                let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
                let! defaultBudgetIdOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
                let! defaultAccountIdOpt = Persistence.Settings.getSetting "ynab_default_account_id"

                match tokenOpt, defaultBudgetIdOpt, defaultAccountIdOpt with
                | None, _, _ | _, None, _ | _, _, None ->
                    return Error (SyncError.YnabImportFailed (0, "YNAB not fully configured"))
                | Some token, Some budgetId, Some accountIdText ->
                    let ynabAccountId = YnabAccountId (Guid.Parse(accountIdText: string))

                    // Wie der normale Import-Push (ADR 0021): ohne ein gespiegeltes
                    // Kassenbuch-Konto für das Import-Konto kann Force-Import die schon
                    // bestehende Buchung weder finden noch verknüpfen.
                    match! Ledger.Persistence.findAccountByYnabAccountId ynabAccountId with
                    | None ->
                        return
                            Error (
                                SyncError.YnabImportFailed (
                                    transactionsToForce.Length,
                                    "Kein gespiegeltes Kassenbuch-Konto für das Import-Konto. Einmal \"Jetzt spiegeln\" ausführen, dann erneut versuchen."
                                )
                            )
                    | Some ledgerAccount ->
                        match! resolveTransferPayees token (YnabBudgetId budgetId) transactionsToForce with
                        | Error ynabError ->
                            return Error (SyncError.YnabImportFailed (transactionsToForce.Length, ynabErrorToString ynabError))
                        | Ok transferPayees ->
                            let today = DateTime.Today
                            // Force import with NEW import_ids (forceNewImportId = true):
                            // nur für YNABs Dedup. Die Kassenbuch-Suche läuft über die
                            // alte, deterministische ImportId
                            // (`buildComdirectImportCandidates true`, ledger-w6tbq), nicht
                            // über diese neue (ledger-fq3kd).
                            let prepared0 = prepareTransactions today ynabAccountId transferPayees true transactionsToForce
                            // Eine erzwungene Buchung mit Rückfrage Lena geht wieder mit der Flag-Farbe
                            // (ynab-sync-d9e51); die Zahl der ohne Flag gesendeten ist hier nicht Teil
                            // des Ergebnisses.
                            let! storedLenaColor =
                                Persistence.Settings.getSetting Ledger.CommandHandlers.lenaFlagColorSettingKey
                            let prepared, _ =
                                applyLenaFlag (ImportAskLena.validColor storedLenaColor) transactionsToForce prepared0

                            let candidates =
                                buildComdirectImportCandidates true transactionsToForce prepared

                            let now = DateTime.UtcNow
                            let push preparedList = sendPrepared token (YnabBudgetId budgetId) preparedList

                            let! pushResult =
                                Ledger.CommandHandlers.pushComdirectImports
                                    now
                                    push
                                    Ledger.CommandHandlers.triggerMirrorInBackground
                                    ledgerAccount.Id
                                    candidates

                            match pushResult.SendResult with
                            | Some (Error ynabError) ->
                                // ADR 0021 Punkt 2: kein Rollback. Schon geschriebene
                                // Kassenbuch-Buchungen bleiben unverändert und
                                // unverknüpft stehen; der API-Aufruf meldet den
                                // Push-Fehler trotzdem.
                                return
                                    Error (SyncError.YnabImportFailed (transactionsToForce.Length, ynabErrorToString ynabError))
                            | _ ->
                                // Dieselbe Outcome-Klassifikation wie beim normalen
                                // Import-Push (`Server.PushOutcomeClassification`,
                                // ledger-na3rq). Bug-Fix: vorher prüfte dieser Zweig nur,
                                // ob keine kategorisierte Buchung mehr offen ist
                                // (`allCategorizedImported`), ohne ein gescheitertes
                                // Kassenbuch-Schreiben dieses Versuchs zu berücksichtigen
                                // (`LedgerWriteFailed`). Die Session konnte dadurch
                                // fälschlich abgeschlossen werden, obwohl eine Buchung
                                // offen blieb (ADR 0021).
                                //
                                // `forceImport = true`, `otherSessionTransactions` sind alle
                                // Session-Buchungen außerhalb dieses Force-Batches: Roman
                                // wählt `transactionIds` gezielt aus, deshalb muss sowohl
                                // eine anderswo offen gebliebene Buchung als auch eine
                                // Buchung dieses Batches, die trotz Force-Versuch offen
                                // bleibt (zukunftsdatiert, nicht auswertbar), den Abschluss
                                // blockieren, wie es `allCategorizedImported` vor der
                                // Extraktion über die ganze Session tat.
                                let otherSessionTransactions =
                                    allTransactions
                                    |> List.filter (fun tx -> not (List.contains tx.Transaction.Id transactionIds))

                                let classification =
                                    Server.PushOutcomeClassification.classifyPushOutcomes
                                        transactionsToForce
                                        prepared
                                        pushResult
                                        true
                                        otherSessionTransactions

                                SyncSessionManager.updateTransactions classification.UpdatedTransactions

                                if classification.SessionMayComplete then
                                    SyncSessionManager.completeSession()

                                // Update session in database
                                match SyncSessionManager.getCurrentSession() with
                                | Some session ->
                                    do! Persistence.SyncSessions.updateSession session

                                    // Save updated transactions
                                    for tx in SyncSessionManager.getTransactions() do
                                        do! Persistence.SyncTransactions.saveTransaction session.Id tx

                                    let createdCount =
                                        pushResult.Outcomes
                                        |> Map.toList
                                        |> List.filter (fun (_, outcome) ->
                                            match outcome with
                                            | Ledger.CommandHandlers.Linked _ | Ledger.CommandHandlers.AlreadyImported _ -> true
                                            | _ -> false)
                                        |> List.length

                                    return Ok createdCount
                                | None ->
                                    return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
    }

    // ============================================
    // History
    // ============================================

    getSyncHistory = fun count -> async {
        return! Persistence.SyncSessions.getRecentSessions count
    }
}

// ============================================
// Ledger API (Kassenbuch)
// ============================================

/// Der Push des Prüfvermerks mit Token und Standard-Budget aus den Settings. Fehlt eines
/// von beiden, liefert der Push einen Fehler statt zu werfen: der Befehl hat das Ereignis
/// dann schon geschrieben und meldet den Fehlschlag als Zustand (ADR 0050, Punkt 7).
let private reviewMarkPush () : Async<Ledger.CommandHandlers.ReviewMarkPush> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

        match tokenOpt, budgetOpt with
        | Some token, Some budgetId ->
            return fun ynabTransactionId approved cleared ->
                YnabClient.updateReviewMark token (YnabBudgetId budgetId) ynabTransactionId approved cleared
        | None, _ ->
            return fun _ _ _ -> async { return Error (YnabError.Unauthorized "No YNAB token configured") }
        | _, None ->
            return fun _ _ _ ->
                async { return Error (YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)") }
    }

/// Der Push der Kategorie (ledger-k2rbn, ADR 0055) mit Token und Standard-Budget aus den
/// Settings, mit denselben Fehlerfällen wie `reviewMarkPush`.
let private categoryPush () : Async<Ledger.CommandHandlers.CategoryPush> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

        match tokenOpt, budgetOpt with
        | Some token, Some budgetId ->
            return fun ynabTransactionId categoryId ->
                YnabClient.updateCategory token (YnabBudgetId budgetId) ynabTransactionId categoryId
        | None, _ ->
            return fun _ _ -> async { return Error (YnabError.Unauthorized "No YNAB token configured") }
        | _, None ->
            return fun _ _ ->
                async { return Error (YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)") }
    }

/// Der Push des Flags (ledger-xfjcp, ADR 0056) mit Token und Standard-Budget aus den
/// Settings, mit denselben Fehlerfällen wie `categoryPush`. Sendet nur die Farbe.
let private flagPush () : Async<Ledger.CommandHandlers.FlagPush> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

        match tokenOpt, budgetOpt with
        | Some token, Some budgetId ->
            return fun ynabTransactionId flagColor ->
                YnabClient.updateFlag token (YnabBudgetId budgetId) ynabTransactionId flagColor
        | None, _ ->
            return fun _ _ -> async { return Error (YnabError.Unauthorized "No YNAB token configured") }
        | _, None ->
            return fun _ _ ->
                async { return Error (YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)") }
    }

/// Der Push des Memos (ledger-fa1fp, ADR 0062) mit Token und Standard-Budget aus den
/// Settings, mit denselben Fehlerfällen wie `flagPush`. Sendet nur `memo`.
let private memoPush () : Async<Ledger.CommandHandlers.MemoPush> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

        match tokenOpt, budgetOpt with
        | Some token, Some budgetId ->
            return fun ynabTransactionId memo ->
                YnabClient.updateMemo token (YnabBudgetId budgetId) ynabTransactionId memo
        | None, _ ->
            return fun _ _ -> async { return Error (YnabError.Unauthorized "No YNAB token configured") }
        | _, None ->
            return fun _ _ ->
                async { return Error (YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)") }
    }

/// Der Sammel-Push des Flags (ledger-y54gr), mit denselben Fehlerfällen wie `flagPush`.
let private flagBatchPush () : Async<Ledger.CommandHandlers.FlagBatchPush> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

        match tokenOpt, budgetOpt with
        | Some token, Some budgetId ->
            return fun items -> YnabClient.updateFlagBatch token (YnabBudgetId budgetId) items
        | None, _ ->
            return fun _ -> async { return Error (YnabError.Unauthorized "No YNAB token configured") }
        | _, None ->
            return fun _ ->
                async { return Error (YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)") }
    }

/// Der Einzel-Push einer nicht verknüpften Buchung (ledger-cks06) mit Token und
/// Standard-Budget aus den Settings. Die Buchung geht mit ihrem aktuellen Stand und, wenn
/// sie eine `import_id` trägt, mit dieser hinaus. Fehlt Token, Budget oder die
/// YNAB-Verknüpfung des Kontos, liefert der Push einen Fehler statt zu werfen.
let private entryPush () : Async<Ledger.CommandHandlers.EntryPush> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

        match tokenOpt, budgetOpt with
        | Some token, Some budgetId ->
            return fun entry -> async {
                let! accounts = Ledger.Persistence.loadAccounts ()
                match accounts |> List.tryFind (fun a -> a.Id = entry.AccountId) with
                | Some { Link = Ledger.Domain.YnabMirrored ynabAccountId } ->
                    let! links = Ledger.Persistence.linksForEntry entry.Id
                    let request : ManualTransactionRequest = {
                        Amount = abs entry.Amount.Amount
                        IsOutflow = entry.Amount.Amount < 0m
                        PayeeName = entry.Payee |> Option.defaultValue ""
                        CategoryId = entry.Category |> Option.map (fun c -> c.CategoryId)
                        Date = entry.Date
                        Memo = entry.Memo
                    }
                    return!
                        YnabClient.createTransactionWithFlag
                            (Ledger.CommandHandlers.currentImportId links)
                            (Ledger.CommandHandlers.pushFlagColorOf entry)
                            token
                            (YnabBudgetId budgetId)
                            ynabAccountId
                            request
                | _ ->
                    return Error (YnabError.AccountNotFound Ledger.CommandHandlers.pushNotPossibleReason)
            }
        | None, _ ->
            return fun _ -> async { return Error (YnabError.Unauthorized "No YNAB token configured") }
        | _, None ->
            return fun _ ->
                async { return Error (YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)") }
    }

/// Die YNAB-Aufrufe des Split-Schreibwegs (ledger-kxme4, ADR 0058) mit Token und
/// Standard-Budget aus den Settings, mit denselben Fehlerfällen wie `categoryPush`.
let private splitWriteTools () : Async<Ledger.CommandHandlers.SplitWriteTools> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

        match tokenOpt, budgetOpt with
        | Some token, Some budgetId ->
            let budget = YnabBudgetId budgetId
            return {
                IsTrackingAccount = fun account -> YnabClient.isTrackingAccount token budget account
                PutCategory = fun ynabTransactionId categoryId -> YnabClient.updateCategory token budget ynabTransactionId categoryId
                PutSplits = fun ynabTransactionId splits -> YnabClient.updateSplits token budget ynabTransactionId splits
                Delete = fun ynabTransactionId -> YnabClient.deleteTransaction token budget ynabTransactionId
                Create = fun replacement -> YnabClient.createReplacement token budget replacement
            }
        | _ ->
            let error =
                match tokenOpt with
                | None -> YnabError.Unauthorized "No YNAB token configured"
                | Some _ -> YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)"
            return {
                IsTrackingAccount = fun _ -> async { return Error error }
                PutCategory = fun _ _ -> async { return Error error }
                PutSplits = fun _ _ -> async { return Error error }
                Delete = fun _ -> async { return Error error }
                Create = fun _ -> async { return Error error }
            }
    }

/// Der Sammel-Push des Prüfvermerks (ledger-nhwrt), mit denselben Fehlerfällen wie
/// `reviewMarkPush`.
let private reviewMarkBatchPush () : Async<Ledger.CommandHandlers.ReviewMarkBatchPush> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

        match tokenOpt, budgetOpt with
        | Some token, Some budgetId ->
            return fun items -> YnabClient.updateReviewMarkBatch token (YnabBudgetId budgetId) items
        | None, _ ->
            return fun _ -> async { return Error (YnabError.Unauthorized "No YNAB token configured") }
        | _, None ->
            return fun _ ->
                async { return Error (YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)") }
    }

/// Die YNAB-Kontenliste des Standard-Budgets, einige Minuten gemerkt (ledger-pafkp). Sie speist
/// die Tracking-Sperre in den Buchungs-Sichten und die Übertrags-Ziele des Split-Editors; ein
/// Abruf je Seitenanfrage wäre zu teuer (Ratenbegrenzung). Fehlschläge werden nicht gemerkt.
let private budgetAccountsCache : (DateTime * YnabAccount list) option ref = ref None
let private budgetAccountsTtl = TimeSpan.FromMinutes 5.0

let private budgetAccounts () : Async<Result<YnabAccount list, string>> =
    async {
        match budgetAccountsCache.Value with
        | Some (at, accounts) when DateTime.UtcNow - at < budgetAccountsTtl -> return Ok accounts
        | _ ->
            let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
            let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

            match tokenOpt, budgetOpt with
            | Some token, Some budgetId ->
                match! YnabClient.fetchBudgetAccounts token (YnabBudgetId budgetId) with
                | Ok accounts ->
                    budgetAccountsCache.Value <- Some (DateTime.UtcNow, accounts)
                    return Ok accounts
                | Error err -> return Error (Ledger.CommandHandlers.describeSplitPushError err)
            | None, _ -> return Error "Kein YNAB-Token eingerichtet"
            | _, None -> return Error "Kein Standard-Budget konfiguriert (Settings)"
    }

/// Ergänzt die Sperre für Tracking-Konten an den Buchungs-Sichten. Ohne Kontenliste (kein Token,
/// YNAB nicht erreichbar) bleibt es bei den lokalen Sperren; der Befehl prüft beim Speichern
/// selbst noch einmal live.
let private withTrackingLocks (views: LedgerEntryView list) : Async<LedgerEntryView list> =
    async {
        if List.isEmpty views then
            return views
        else
            match! budgetAccounts () with
            | Error _ -> return views
            | Ok ynabAccounts ->
                let! ledgerAccounts = Ledger.Persistence.loadAccounts ()
                let tracking = Ledger.QueryHandlers.ReadModel.trackingLedgerAccounts ynabAccounts ledgerAccounts
                if Set.isEmpty tracking then
                    return views
                else
                    return views |> List.map (Ledger.QueryHandlers.ReadModel.withTrackingLock tracking)
    }

/// Der Sammel-Push der Kategorie (ledger-w29y6), mit denselben Fehlerfällen wie
/// `categoryPush`.
let private categoryBatchPush () : Async<Ledger.CommandHandlers.CategoryBatchPush> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

        match tokenOpt, budgetOpt with
        | Some token, Some budgetId ->
            return fun items -> YnabClient.updateCategoryBatch token (YnabBudgetId budgetId) items
        | None, _ ->
            return fun _ -> async { return Error (YnabError.Unauthorized "No YNAB token configured") }
        | _, None ->
            return fun _ ->
                async { return Error (YnabError.BudgetNotFound "Kein Standard-Budget konfiguriert (Settings)") }
    }

let ledgerApi : LedgerApi = {
    // Der manuelle Ausloeser "Jetzt spiegeln" (ADR 0014, Punkt 3). Frische-Kriterium
    // und Lauf nach Push sind Folge-Arbeit.
    mirrorYnabBudget = fun () -> Ledger.CommandHandlers.mirrorYnabBudget ()

    getLastMirrorRun = fun () -> Ledger.CommandHandlers.loadLastMirrorRun ()

    // Die Sichten der Kassenbuch-Seite (ledger-q5w9c). Sie lesen die Zustandstabellen;
    // nur die Historie einer einzelnen Buchung greift auf das Ereignis-Log zu.
    getLedgerAccounts = fun () -> Ledger.QueryHandlers.getLedgerAccounts ()

    getLedgerEntriesForAccount = fun accountId -> async {
        let! views = Ledger.QueryHandlers.getLedgerEntriesForAccount accountId
        return! withTrackingLocks views
    }

    getLedgerEntriesPage = fun query -> async {
        let! page = Ledger.QueryHandlers.getLedgerEntriesPage query
        let! entries = withTrackingLocks page.Entries
        return { page with Entries = entries }
    }

    getLedgerPayeeSuggestions = fun query -> Ledger.QueryHandlers.getLedgerPayeeSuggestions query
    getPayeeCategorySuggestions = fun payee current -> Ledger.QueryHandlers.getPayeeCategorySuggestions payee current

    getLedgerAccountBalances = fun () -> Ledger.QueryHandlers.getLedgerAccountBalances ()

    getEntryHistory = fun entryId -> Ledger.QueryHandlers.getEntryHistory entryId

    // Prüfvermerk setzen und nach YNAB zurückschreiben (ledger-r777c, ADR 0050).
    // Kassenbuch zuerst, dann der Push; der Push braucht Token und Budget aus den
    // Settings, fehlen sie, scheitert er mit Grund und der Befehl meldet den Fehlschlag.
    setReviewMark = fun entryId target -> async {
        let! push = reviewMarkPush ()
        return!
            Ledger.CommandHandlers.setReviewMark DateTime.UtcNow push (Ledger.Events.EntryId entryId) target
    }

    restoreReviewMark = fun entryId previous -> async {
        let! push = reviewMarkPush ()
        return!
            Ledger.CommandHandlers.restoreReviewMark DateTime.UtcNow push (Ledger.Events.EntryId entryId) previous
    }

    retryReviewMarkWriteBack = fun entryId -> async {
        let! push = reviewMarkPush ()
        return!
            Ledger.CommandHandlers.retryReviewMarkWriteBack DateTime.UtcNow push (Ledger.Events.EntryId entryId)
    }

    // Kategorie ändern und nach YNAB zurückschreiben (ledger-k2rbn, ADR 0055). Kassenbuch
    // zuerst, dann der Push, beides unter der Schreib-Sperre gegen den Spiegel-Lauf.
    setEntryCategory = fun entryId target -> async {
        let! push = categoryPush ()
        return!
            Ledger.CommandHandlers.setEntryCategory DateTime.UtcNow push (Ledger.Events.EntryId entryId) target
    }

    // Split setzen oder aufheben und nach YNAB zurückschreiben (ledger-kxme4, ADR 0058).
    setEntrySplits = fun entryId lines -> async {
        let! tools = splitWriteTools ()
        return!
            Ledger.CommandHandlers.setEntrySplits DateTime.UtcNow tools (Ledger.Events.EntryId entryId) lines
    }

    getBudgetAccounts = fun () -> budgetAccounts ()

    getLedgerEntry = fun entryId -> async {
        match! Ledger.QueryHandlers.getLedgerEntry entryId with
        | None -> return None
        | Some view ->
            let! locked = withTrackingLocks [ view ]
            return List.tryHead locked
    }

    // "Erneut senden" gilt für Kategorie und Split (ADR 0058, Punkt 3).
    retryCategorizationWriteBack = fun entryId -> async {
        let! push = categoryPush ()
        let! tools = splitWriteTools ()
        return!
            Ledger.CommandHandlers.retryCategorizationOrSplitWriteBack
                DateTime.UtcNow
                push
                tools
                (Ledger.Events.EntryId entryId)
    }

    // "Erneut senden" am Hinweis "Noch nicht in YNAB" (ledger-cks06): sofortiger Push
    // dieser einen Buchung, danach der Spiegel.
    retryPushToYnab = fun entryId -> async {
        let! push = entryPush ()
        return!
            Ledger.CommandHandlers.retryPushToYnab
                DateTime.UtcNow
                push
                Ledger.CommandHandlers.triggerMirrorInBackground
                (Ledger.Events.EntryId entryId)
    }

    // "Geprüft" für eine ganze Auswahl und das gemeinsame "Rückgängig" (ledger-nhwrt):
    // ein Ereignis je Buchung, ein Sammel-PATCH an YNAB.
    setReviewMarkBatch = fun selection -> async {
        let! push = reviewMarkBatchPush ()
        return! Ledger.CommandHandlers.setReviewMarkBatch DateTime.UtcNow push selection
    }

    restoreReviewMarkBatch = fun items -> async {
        let! push = reviewMarkBatchPush ()
        return! Ledger.CommandHandlers.restoreReviewMarkBatch DateTime.UtcNow push items
    }

    // Kategorie für eine ganze Auswahl und das gemeinsame "Rückgängig" (ledger-w29y6):
    // ein Ereignis je Buchung, ein Sammel-PATCH an YNAB, unter der Schreib-Sperre.
    setEntryCategoryBatch = fun selection target -> async {
        let! push = categoryBatchPush ()
        return! Ledger.CommandHandlers.setEntryCategoryBatch DateTime.UtcNow push selection target
    }

    // Lena fragen: Flag setzen, zurücknehmen und nach YNAB zurückschreiben (ledger-xfjcp,
    // ADR 0056). Kassenbuch zuerst, dann der Push (nur `flag_color`), unter der Schreib-Sperre.
    setLenaFlag = fun entryId target -> async {
        let! push = flagPush ()
        let! lenaColor = Persistence.Settings.getSetting Ledger.CommandHandlers.lenaFlagColorSettingKey
        return!
            Ledger.CommandHandlers.setLenaFlag DateTime.UtcNow push lenaColor (Ledger.Events.EntryId entryId) target
    }

    restoreFlag = fun entryId previous -> async {
        let! push = flagPush ()
        return! Ledger.CommandHandlers.restoreFlag DateTime.UtcNow push (Ledger.Events.EntryId entryId) previous
    }

    retryFlagWriteBack = fun entryId -> async {
        let! push = flagPush ()
        return! Ledger.CommandHandlers.retryFlagWriteBack DateTime.UtcNow push (Ledger.Events.EntryId entryId)
    }

    // Memo bearbeiten (ledger-fa1fp, ADR 0062): Kassenbuch zuerst, dann der Push (nur `memo`),
    // unter der Schreib-Sperre. Das Rückgängig läuft über denselben Befehl.
    setEntryMemo = fun entryId text -> async {
        let! push = memoPush ()
        return! Ledger.CommandHandlers.setEntryMemo DateTime.UtcNow push (Ledger.Events.EntryId entryId) text
    }

    retryMemoWriteBack = fun entryId -> async {
        let! push = memoPush ()
        return! Ledger.CommandHandlers.retryMemoWriteBack DateTime.UtcNow push (Ledger.Events.EntryId entryId)
    }

    // Lena fragen für eine ganze Auswahl und das gemeinsame "Rückgängig" (ledger-y54gr):
    // ein Ereignis je Buchung, ein Sammel-PATCH mit nur `flag_color`, unter der Schreib-Sperre.
    setLenaFlagBatch = fun selection target -> async {
        let! push = flagBatchPush ()
        let! lenaColor = Persistence.Settings.getSetting Ledger.CommandHandlers.lenaFlagColorSettingKey
        return! Ledger.CommandHandlers.setLenaFlagBatch DateTime.UtcNow push lenaColor selection target
    }

    restoreFlagBatch = fun items -> async {
        let! push = flagBatchPush ()
        return! Ledger.CommandHandlers.restoreFlagBatch DateTime.UtcNow push items
    }
}

// ============================================
// Budget (budgeting-k7wmd)
// ============================================

/// Der Weg der Budget-Seite ohne Settings und Netz: Schlüssel prüfen, Token und Budget
/// verlangen, Monat holen (`fetchMonth token budgetId monthKey`), Fehler lesbar machen.
let budgetMonthFor
    (tokenOpt: string option)
    (budgetOpt: string option)
    (today: DateTime)
    (monthKeyText: string)
    (fetchMonth: string -> YnabBudgetId -> string -> Async<YnabResult<Shared.Budgeting.YnabMonth>>)
    : Async<Result<Shared.Budgeting.BudgetMonth, string>> =
    async {
        match Validation.validateBudgetMonth monthKeyText with
        | Error message -> return Error message
        | Ok choice ->
            match tokenOpt, budgetOpt with
            | None, _ -> return Error (ynabErrorToString (YnabError.Unauthorized "No YNAB token configured"))
            | _, None -> return Error "Kein Standard-Budget konfiguriert (Settings)"
            | Some token, Some budgetId ->
                let key = Server.Budgeting.Domain.monthKey today choice
                match! fetchMonth token (YnabBudgetId budgetId) key with
                | Error e -> return Error (ynabErrorToString e)
                | Ok month -> return Ok (Server.Budgeting.Domain.toBudgetMonth month)
    }

/// Der Weg von "Targets anwenden" ohne Settings und Netz: Token und Budget verlangen, den
/// YNAB-Port bauen (`portFor token budgetId`) und `run` damit ausführen. YNAB-Fehler werden
/// lesbar (`ynabErrorToString`).
let targetsWith
    (tokenOpt: string option)
    (budgetOpt: string option)
    (portFor: string -> YnabBudgetId -> Server.Budgeting.CommandHandlers.YnabPort)
    (run: Server.Budgeting.CommandHandlers.YnabPort -> Async<Result<'a, YnabError>>)
    : Async<Result<'a, string>> =
    async {
        match tokenOpt, budgetOpt with
        | None, _ -> return Error (ynabErrorToString (YnabError.Unauthorized "No YNAB token configured"))
        | _, None -> return Error "Kein Standard-Budget konfiguriert (Settings)"
        | Some token, Some budgetId ->
            match! run (portFor token (YnabBudgetId budgetId)) with
            | Ok value -> return Ok value
            | Error e -> return Error (ynabErrorToString e)
    }

/// Der YNAB-Port der Server-Abläufe: echter Client für Lesen und Zuweisen.
let private livePort (token: string) (budgetId: YnabBudgetId) : Server.Budgeting.CommandHandlers.YnabPort =
    { FetchMonth = fun monthKey -> YnabClient.getMonth token budgetId monthKey
      SetAssigned = fun monthKey categoryId milliunits -> YnabClient.setAssigned token budgetId monthKey categoryId milliunits }

/// Die Budget-Seite: liest den Monat live aus YNAB. Nur "Targets anwenden" und "Verschieben"
/// schreiben (nach YNAB und in die Budget-Bewegungen).
let budgetApi : BudgetApi = {
    getBudgetMonth = fun monthKeyText -> async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
        return! budgetMonthFor tokenOpt budgetOpt DateTime.Today monthKeyText YnabClient.getMonth
    }

    previewTargets = fun monthKeyText -> async {
        match Validation.validateBudgetMonth monthKeyText with
        | Error message -> return Error message
        | Ok choice ->
            let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
            let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
            let key = Server.Budgeting.Domain.monthKey DateTime.Today choice
            return! targetsWith tokenOpt budgetOpt livePort (fun port -> Server.Budgeting.CommandHandlers.preview port key)
    }

    applyTargets = fun monthKeyText shown -> async {
        match Validation.validateBudgetMonth monthKeyText with
        | Error message -> return Error message
        | Ok choice ->
            let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
            let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
            let key = Server.Budgeting.Domain.monthKey DateTime.Today choice
            return!
                targetsWith tokenOpt budgetOpt livePort (fun port ->
                    Server.Budgeting.CommandHandlers.apply port DateTime.UtcNow key shown)
    }

    retryFailedTargets = fun applicationIdText -> async {
        match Guid.TryParse applicationIdText with
        | false, _ -> return Error $"Ungültige Anwendung: '{applicationIdText}'"
        | true, applicationId ->
            let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
            let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
            return!
                targetsWith tokenOpt budgetOpt livePort (fun port ->
                    Server.Budgeting.CommandHandlers.retryFailed port DateTime.UtcNow applicationId)
    }

    moveMoney = fun monthKeyText request -> async {
        match Validation.validateBudgetMonth monthKeyText with
        | Error message -> return Error message
        | Ok choice ->
            let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
            let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
            let key = Server.Budgeting.Domain.monthKey DateTime.Today choice
            let! result =
                targetsWith tokenOpt budgetOpt livePort (fun port ->
                    Server.Budgeting.CommandHandlers.moveMoney port DateTime.UtcNow key request)
            return result |> Result.bind id
    }
}

// ============================================
// HTTP Handler
// ============================================

open Giraffe

let private routeBuilder typeName methodName = $"/api/{typeName}/{methodName}"

let private errorHandler (ex: exn) (routeInfo: RouteInfo<'a>) =
    printfn "Fable.Remoting ERROR in %s: %s" routeInfo.methodName ex.Message
    printfn "Stack trace: %s" ex.StackTrace
    Propagate ex

/// `berlinZone` ist die beim Start aufgelöste Zeitzone Europe/Berlin (Bob-Tagesgrenze).
let webApp (berlinZone: System.TimeZoneInfo) =
    // Die Bot-Schnittstelle (ADR 0066) steht vor Fable.Remoting; Pfade außerhalb von
    // /bot/ gibt sie unverändert weiter. Ein Vorschau-Speicher je Prozess.
    let botDeps = BotApi.liveDeps berlinZone
    printfn "%s" (Server.ServerConfig.botMaxAmountLogLine (System.Environment.GetEnvironmentVariable Server.ServerConfig.botMaxAmountVariable))
    choose [
        BotApi.handler botDeps

        Remoting.createApi()
        |> Remoting.withRouteBuilder routeBuilder
        |> Remoting.withErrorHandler errorHandler
        |> Remoting.fromValue settingsApi
        |> Remoting.buildHttpHandler

        Remoting.createApi()
        |> Remoting.withRouteBuilder routeBuilder
        |> Remoting.withErrorHandler errorHandler
        |> Remoting.fromValue ynabApi
        |> Remoting.buildHttpHandler

        Remoting.createApi()
        |> Remoting.withRouteBuilder routeBuilder
        |> Remoting.withErrorHandler errorHandler
        |> Remoting.fromValue rulesApi
        |> Remoting.buildHttpHandler

        Remoting.createApi()
        |> Remoting.withRouteBuilder routeBuilder
        |> Remoting.withErrorHandler errorHandler
        |> Remoting.fromValue syncApi
        |> Remoting.buildHttpHandler

        Remoting.createApi()
        |> Remoting.withRouteBuilder routeBuilder
        |> Remoting.withErrorHandler errorHandler
        |> Remoting.fromValue ledgerApi
        |> Remoting.buildHttpHandler

        Remoting.createApi()
        |> Remoting.withRouteBuilder routeBuilder
        |> Remoting.withErrorHandler errorHandler
        |> Remoting.fromValue budgetApi
        |> Remoting.buildHttpHandler
    ]
