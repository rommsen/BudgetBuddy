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

/// Converts RulesError to string
let private rulesErrorToString (error: RulesError) : string =
    match error with
    | RulesError.RuleNotFound ruleId -> $"Rule not found: {ruleId}"
    | RulesError.InvalidPattern (pattern, reason) -> $"Invalid pattern '{pattern}': {reason}"
    | RulesError.CategoryNotFound categoryId -> $"Category not found: {categoryId}"
    | RulesError.DuplicateRule pattern -> $"Duplicate rule pattern: {pattern}"
    | RulesError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"

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

/// Converts ComdirectError to string
let private comdirectErrorToString (error: ComdirectError) : string =
    match error with
    | ComdirectError.AuthenticationFailed msg -> $"Authentication failed: {msg}"
    | ComdirectError.TanChallengeExpired -> "TAN challenge expired"
    | ComdirectError.TanRejected -> "TAN was rejected"
    | ComdirectError.SessionExpired -> "Session expired"
    | ComdirectError.InvalidCredentials -> "Invalid credentials"
    | ComdirectError.NetworkError (status, msg) -> $"Network error (HTTP {status}): {msg}"
    | ComdirectError.InvalidResponse msg -> $"Invalid response: {msg}"

// ============================================
// Settings API Implementation
// ============================================

let settingsApi : SettingsApi = {
    getSettings = fun () -> async {
        try
            // Load all settings from database
            let! ynabToken = Persistence.Settings.getSetting "ynab_token"
            let! ynabDefaultBudgetId = Persistence.Settings.getSetting "ynab_default_budget_id"
            let! ynabDefaultAccountId = Persistence.Settings.getSetting "ynab_default_account_id"
            let! comdirectClientId = Persistence.Settings.getSetting "comdirect_client_id"
            let! comdirectClientSecret = Persistence.Settings.getSetting "comdirect_client_secret"
            let! comdirectUsername = Persistence.Settings.getSetting "comdirect_username"
            let! comdirectPassword = Persistence.Settings.getSetting "comdirect_password"
            let! comdirectAccountId = Persistence.Settings.getSetting "comdirect_account_id"
            let! syncDaysToFetch = Persistence.Settings.getSetting "sync_days_to_fetch"

            let ynabSettings =
                match ynabToken with
                | Some token ->
                    Some {
                        PersonalAccessToken = token
                        DefaultBudgetId = ynabDefaultBudgetId |> Option.map YnabBudgetId
                        DefaultAccountId = ynabDefaultAccountId |> Option.bind (fun id -> match Guid.TryParse(id: string) with | true, g -> Some (YnabAccountId g) | _ -> None)
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

    saveComdirectCredentials = fun credentials -> async {
        // Validate
        match validateComdirectSettings credentials with
        | Error errors -> return Error (SettingsError.ComdirectCredentialsInvalid ("general", String.concat "; " errors))
        | Ok valid ->
            try
                do! Persistence.Settings.setSetting "comdirect_client_id" valid.ClientId false
                do! Persistence.Settings.setSetting "comdirect_client_secret" valid.ClientSecret true
                do! Persistence.Settings.setSetting "comdirect_username" valid.Username false
                do! Persistence.Settings.setSetting "comdirect_password" valid.Password true
                match valid.AccountId with
                | Some accountId -> do! Persistence.Settings.setSetting "comdirect_account_id" accountId false
                | None -> ()
                return Ok ()
            with ex ->
                return Error (SettingsError.DatabaseError ("save_comdirect_credentials", ex.Message))
    }

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
            return! YnabClient.getCategories token budgetId
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
}

// ============================================
// Rules API Implementation
// ============================================

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

    createRule = fun request -> async {
        // Validate request
        match validateRuleCreateRequest request with
        | Error errors -> return Error (RulesError.InvalidPattern (request.Pattern, String.concat "; " errors))
        | Ok validRequest ->
            // Validate pattern compiles
            let testRule = {
                Id = RuleId Guid.Empty
                Name = validRequest.Name
                Pattern = validRequest.Pattern
                PatternType = validRequest.PatternType
                TargetField = validRequest.TargetField
                CategoryId = validRequest.CategoryId
                CategoryName = "" // Will be fetched
                PayeeOverride = validRequest.PayeeOverride
                Priority = validRequest.Priority
                Enabled = true
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }

            match compileRule testRule with
            | Error err -> return Error (RulesError.InvalidPattern (validRequest.Pattern, err))
            | Ok _ ->
                // Fetch category name from YNAB
                let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
                let! defaultBudgetIdOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

                let! categoryName = async {
                    match tokenOpt, defaultBudgetIdOpt with
                    | Some token, Some budgetId ->
                        match! YnabClient.getCategories token (YnabBudgetId budgetId) with
                        | Ok categories ->
                            let matchingCategory =
                                categories
                                |> List.tryFind (fun cat -> cat.Id = validRequest.CategoryId)
                            return matchingCategory |> Option.map (fun cat -> cat.Name) |> Option.defaultValue "Unknown"
                        | Error _ -> return "Unknown"
                    | _ -> return "Unknown"
                }

                // Create rule
                let newRule = {
                    Id = RuleId (Guid.NewGuid())
                    Name = validRequest.Name
                    Pattern = validRequest.Pattern
                    PatternType = validRequest.PatternType
                    TargetField = validRequest.TargetField
                    CategoryId = validRequest.CategoryId
                    CategoryName = categoryName
                    PayeeOverride = validRequest.PayeeOverride
                    Priority = validRequest.Priority
                    Enabled = true
                    CreatedAt = DateTime.UtcNow
                    UpdatedAt = DateTime.UtcNow
                }

                try
                    do! Persistence.Rules.insertRule newRule
                    return Ok newRule
                with ex ->
                    return Error (RulesError.DatabaseError ("create_rule", ex.Message))
    }

    updateRule = fun request -> async {
        // Validate request
        match validateRuleUpdateRequest request with
        | Error errors -> return Error (RulesError.InvalidPattern ("update", String.concat "; " errors))
        | Ok validRequest ->
            // Get existing rule
            match! Persistence.Rules.getRuleById validRequest.Id with
            | None ->
                let (RuleId id) = validRequest.Id
                return Error (RulesError.RuleNotFound id)
            | Some existing ->
                // Apply updates
                let updated = {
                    existing with
                        Name = validRequest.Name |> Option.defaultValue existing.Name
                        Pattern = validRequest.Pattern |> Option.defaultValue existing.Pattern
                        PatternType = validRequest.PatternType |> Option.defaultValue existing.PatternType
                        TargetField = validRequest.TargetField |> Option.defaultValue existing.TargetField
                        CategoryId = validRequest.CategoryId |> Option.defaultValue existing.CategoryId
                        PayeeOverride = validRequest.PayeeOverride |> Option.orElse existing.PayeeOverride
                        Priority = validRequest.Priority |> Option.defaultValue existing.Priority
                        Enabled = validRequest.Enabled |> Option.defaultValue existing.Enabled
                        UpdatedAt = DateTime.UtcNow
                }

                // Validate pattern compiles if pattern changed
                let patternValidation =
                    if validRequest.Pattern.IsSome || validRequest.PatternType.IsSome then
                        compileRule updated |> Result.map (fun _ -> ()) |> Result.mapError (fun err -> RulesError.InvalidPattern (updated.Pattern, err))
                    else
                        Ok ()

                match patternValidation with
                | Error err -> return Error err
                | Ok () ->
                    // Update category name if category changed
                    let! finalUpdated = async {
                        if validRequest.CategoryId.IsSome then
                            let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
                            let! defaultBudgetIdOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

                            match tokenOpt, defaultBudgetIdOpt with
                            | Some token, Some budgetId ->
                                match! YnabClient.getCategories token (YnabBudgetId budgetId) with
                                | Ok categories ->
                                    let matchingCategory =
                                        categories
                                        |> List.tryFind (fun cat -> cat.Id = updated.CategoryId)
                                    let categoryName = matchingCategory |> Option.map (fun cat -> cat.Name) |> Option.defaultValue "Unknown"
                                    return { updated with CategoryName = categoryName }
                                | Error _ -> return updated
                            | _ -> return updated
                        else
                            return updated
                    }

                    try
                        do! Persistence.Rules.updateRule finalUpdated
                        return Ok finalUpdated
                    with ex ->
                        return Error (RulesError.DatabaseError ("update_rule", ex.Message))
    }

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

    importRules = fun json -> async {
        try
            // Parse JSON
            match Decode.Auto.fromString<Rule list>(json) with
            | Error err -> return Error (RulesError.InvalidPattern ("import", err))
            | Ok rules ->
                // Validate all rules compile
                let validationResults =
                    rules
                    |> List.map (fun rule ->
                        match compileRule rule with
                        | Ok _ -> Ok rule
                        | Error err -> Error (rule, err)
                    )

                let errors =
                    validationResults
                    |> List.choose (function Error (rule, err) -> Some $"{rule.Name}: {err}" | Ok _ -> None)

                if not errors.IsEmpty then
                    return Error (RulesError.InvalidPattern ("import", String.concat "; " errors))
                else
                    // Import all rules with new IDs
                    let mutable importedCount = 0
                    for rule in rules do
                        let newRule = { rule with Id = RuleId (Guid.NewGuid()); CreatedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow }
                        do! Persistence.Rules.insertRule newRule
                        importedCount <- importedCount + 1

                    return Ok importedCount
        with ex ->
            return Error (RulesError.DatabaseError ("import_rules", ex.Message))
    }

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
        | Error _ -> return false
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
            let isMatch = compiled.Regex.IsMatch(matchText)
            return isMatch
    }
}

// ============================================
// Sync API Implementation
// ============================================

let syncApi : SyncApi = {
    // ============================================
    // Session management
    // ============================================

    startSync = fun () -> async {
        try
            // First check if Comdirect credentials are configured
            let! settings = settingsApi.getSettings()
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

    cancelSync = fun sessionId -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            try
                // Clear auth session
                ComdirectAuthSession.clearSession()

                // Clear sync session
                SyncSessionManager.clearSession()

                return Ok ()
            with ex ->
                return Error (SyncError.DatabaseError ("cancel_sync", ex.Message))
    }

    // ============================================
    // Comdirect auth flow
    // ============================================

    initiateComdirectAuth = fun sessionId -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            // Get Comdirect credentials from settings
            let! settings = settingsApi.getSettings()

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
                let! settings = settingsApi.getSettings()

                // Get account ID from settings
                let accountId =
                    settings.Comdirect
                    |> Option.bind (fun c -> c.AccountId)
                    |> Option.defaultValue ""

                // Fetch transactions
                match! ComdirectAuthSession.fetchTransactions accountId settings.Sync.DaysToFetch with
                | Error comdirectError ->
                    let errorMsg = comdirectErrorToString comdirectError
                    SyncSessionManager.failSession errorMsg
                    return Error (SyncError.TransactionFetchFailed errorMsg)
                | Ok bankTransactions ->
                    // Apply rules engine
                    let! allRules = Persistence.Rules.getAllRules()
                    match classifyTransactions allRules bankTransactions with
                    | Error errors ->
                        let errorMsg = String.concat "; " errors
                        SyncSessionManager.failSession errorMsg
                        return Error (SyncError.TransactionFetchFailed errorMsg)
                    | Ok syncTransactions ->
                        // Detect duplicates by fetching existing YNAB transactions
                        let! syncTransactionsWithDuplicates = async {
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
                                        return DuplicateDetection.markDuplicates ynabTransactions syncTransactions
                                    | Error _ ->
                                        // If we can't fetch YNAB transactions, proceed without duplicate detection
                                        return syncTransactions
                                | false, _ ->
                                    return syncTransactions
                            | _ ->
                                // YNAB not configured, proceed without duplicate detection
                                return syncTransactions
                        }

                        // Add transactions to session
                        SyncSessionManager.addTransactions syncTransactionsWithDuplicates

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
            return Ok transactions
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
                    | Some catId ->
                        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
                        let! defaultBudgetIdOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

                        match tokenOpt, defaultBudgetIdOpt with
                        | Some token, Some budgetId ->
                            match! YnabClient.getCategories token (YnabBudgetId budgetId) with
                            | Ok categories ->
                                let matchingCategory =
                                    categories
                                    |> List.tryFind (fun cat -> cat.Id = catId)
                                return matchingCategory |> Option.map (fun cat -> cat.Name)
                            | Error _ -> return None
                        | _ -> return None
                    | None -> return None
                }

                let updated = {
                    tx with
                        Status = ManualCategorized
                        CategoryId = categoryId
                        CategoryName = categoryName
                        PayeeOverride = payeeOverride
                }

                SyncSessionManager.updateTransaction updated
                return Ok updated
    }

    skipTransaction = fun (sessionId, txId) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            match SyncSessionManager.getTransaction txId with
            | None -> return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
            | Some tx ->
                let updated = { tx with Status = Skipped }
                SyncSessionManager.updateTransaction updated
                return Ok updated
    }

    bulkCategorize = fun (sessionId, txIds, categoryId) -> async {
        match SyncSessionManager.validateSession sessionId with
        | Error err -> return Error err
        | Ok _ ->
            // Get category name
            let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
            let! defaultBudgetIdOpt = Persistence.Settings.getSetting "ynab_default_budget_id"

            let! categoryName = async {
                match tokenOpt, defaultBudgetIdOpt with
                | Some token, Some budgetId ->
                    match! YnabClient.getCategories token (YnabBudgetId budgetId) with
                    | Ok categories ->
                        let matchingCategory =
                            categories
                            |> List.tryFind (fun cat -> cat.Id = categoryId)
                        return matchingCategory |> Option.map (fun cat -> cat.Name)
                    | Error _ -> return None
                | _ -> return None
            }

            // Update all transactions
            let mutable updatedTransactions = []
            for txId in txIds do
                match SyncSessionManager.getTransaction txId with
                | Some tx ->
                    let updated = {
                        tx with
                            Status = ManualCategorized
                            CategoryId = Some categoryId
                            CategoryName = categoryName
                    }
                    SyncSessionManager.updateTransaction updated
                    updatedTransactions <- updated :: updatedTransactions
                | None -> ()

            return Ok (List.rev updatedTransactions)
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

            // Filter to categorized transactions only
            let categorized =
                transactions
                |> List.filter (fun tx ->
                    match tx.Status with
                    | AutoCategorized | ManualCategorized | NeedsAttention -> tx.CategoryId.IsSome
                    | _ -> false
                )

            if categorized.IsEmpty then
                return Ok 0
            else
                // Get YNAB settings
                let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
                let! defaultBudgetIdOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
                let! defaultAccountIdOpt = Persistence.Settings.getSetting "ynab_default_account_id"

                match tokenOpt, defaultBudgetIdOpt, defaultAccountIdOpt with
                | None, _, _ | _, None, _ | _, _, None ->
                    return Error (SyncError.YnabImportFailed (0, "YNAB not fully configured"))
                | Some token, Some budgetId, Some accountId ->
                    // Import transactions
                    match! YnabClient.createTransactions token (YnabBudgetId budgetId) (YnabAccountId (Guid.Parse(accountId: string))) categorized with
                    | Error ynabError ->
                        return Error (SyncError.YnabImportFailed (categorized.Length, ynabErrorToString ynabError))
                    | Ok count ->
                        // Mark transactions as imported
                        let imported =
                            categorized
                            |> List.map (fun tx -> { tx with Status = Imported })

                        SyncSessionManager.updateTransactions imported

                        // Complete session
                        SyncSessionManager.completeSession()

                        // Update session in database
                        match SyncSessionManager.getCurrentSession() with
                        | Some session ->
                            do! Persistence.SyncSessions.updateSession session

                            // Save all transactions to database
                            for tx in transactions do
                                do! Persistence.SyncTransactions.saveTransaction session.Id tx

                            return Ok count
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
// HTTP Handler
// ============================================

open Giraffe

let private routeBuilder typeName methodName = $"/api/{typeName}/{methodName}"

let private errorHandler (ex: exn) (routeInfo: RouteInfo<'a>) =
    printfn "Fable.Remoting ERROR in %s: %s" routeInfo.methodName ex.Message
    printfn "Stack trace: %s" ex.StackTrace
    Propagate ex

let webApp() =
    choose [
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
    ]
