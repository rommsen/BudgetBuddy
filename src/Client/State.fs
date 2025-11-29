module State

open System
open Elmish
open Shared.Domain
open Types

// ============================================
// Model
// ============================================

type Model = {
    // Navigation
    CurrentPage: Page

    // Toast notifications
    Toasts: Toast list

    // Dashboard
    CurrentSession: RemoteData<SyncSession option>
    RecentSessions: RemoteData<SyncSession list>

    // Settings
    Settings: RemoteData<AppSettings>
    YnabBudgets: RemoteData<YnabBudgetWithAccounts list>

    // Settings form state
    YnabTokenInput: string
    ComdirectClientIdInput: string
    ComdirectClientSecretInput: string
    ComdirectUsernameInput: string
    ComdirectPasswordInput: string
    ComdirectAccountIdInput: string
    SyncDaysInput: int

    // Rules
    Rules: RemoteData<Rule list>
    EditingRule: Rule option
    Categories: YnabCategory list

    // Sync Flow
    SyncTransactions: RemoteData<SyncTransaction list>
    SelectedTransactions: Set<TransactionId>
}

// ============================================
// Messages
// ============================================

type Msg =
    // Navigation
    | NavigateTo of Page

    // Toast
    | ShowToast of string * ToastType
    | DismissToast of Guid
    | AutoDismissToast of Guid

    // Settings
    | LoadSettings
    | SettingsLoaded of Result<AppSettings, string>
    | UpdateYnabTokenInput of string
    | SaveYnabToken
    | YnabTokenSaved of Result<unit, SettingsError>
    | TestYnabConnection
    | YnabConnectionTested of Result<YnabBudgetWithAccounts list, SettingsError>
    | UpdateComdirectClientIdInput of string
    | UpdateComdirectClientSecretInput of string
    | UpdateComdirectUsernameInput of string
    | UpdateComdirectPasswordInput of string
    | UpdateComdirectAccountIdInput of string
    | SaveComdirectCredentials
    | ComdirectCredentialsSaved of Result<unit, SettingsError>
    | UpdateSyncDaysInput of int
    | SaveSyncSettings
    | SyncSettingsSaved of Result<unit, SettingsError>
    | SetDefaultBudget of YnabBudgetId
    | DefaultBudgetSet of Result<unit, YnabError>
    | SetDefaultAccount of YnabAccountId
    | DefaultAccountSet of Result<unit, YnabError>

    // Rules
    | LoadRules
    | RulesLoaded of Rule list
    | OpenNewRuleModal
    | EditRule of RuleId
    | CloseRuleModal
    | DeleteRule of RuleId
    | RuleDeleted of Result<unit, RulesError>
    | ToggleRuleEnabled of RuleId
    | RuleToggled of Result<Rule, RulesError>
    | LoadCategories
    | CategoriesLoaded of Result<YnabCategory list, YnabError>

    // Sync Flow
    | LoadCurrentSession
    | CurrentSessionLoaded of SyncSession option
    | LoadRecentSessions
    | RecentSessionsLoaded of SyncSession list
    | StartSync
    | SyncStarted of Result<SyncSession, SyncError>
    | InitiateComdirectAuth
    | ComdirectAuthInitiated of Result<string, SyncError>
    | ConfirmTan
    | TanConfirmed of Result<unit, SyncError>
    | LoadTransactions
    | TransactionsLoaded of Result<SyncTransaction list, SyncError>
    | ToggleTransactionSelection of TransactionId
    | SelectAllTransactions
    | DeselectAllTransactions
    | CategorizeTransaction of TransactionId * YnabCategoryId option
    | TransactionCategorized of Result<SyncTransaction, SyncError>
    | SkipTransaction of TransactionId
    | TransactionSkipped of Result<SyncTransaction, SyncError>
    | BulkCategorize of YnabCategoryId
    | BulkCategorized of Result<SyncTransaction list, SyncError>
    | ImportToYnab
    | ImportCompleted of Result<int, SyncError>
    | CancelSync
    | SyncCancelled of Result<unit, SyncError>

// ============================================
// Helper Functions
// ============================================

let private settingsErrorToString (error: SettingsError) : string =
    match error with
    | SettingsError.YnabTokenInvalid msg -> $"Invalid YNAB token: {msg}"
    | SettingsError.YnabConnectionFailed (status, msg) -> $"YNAB connection failed (HTTP {status}): {msg}"
    | SettingsError.ComdirectCredentialsInvalid (field, reason) -> $"Invalid Comdirect credentials ({field}): {reason}"
    | SettingsError.EncryptionFailed msg -> $"Encryption failed: {msg}"
    | SettingsError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"

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
    | RulesError.DuplicateRule pattern -> $"Duplicate rule pattern: {pattern}"
    | RulesError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"

let private syncErrorToString (error: SyncError) : string =
    match error with
    | SyncError.SessionNotFound sessionId -> $"Session not found: {sessionId}"
    | SyncError.ComdirectAuthFailed reason -> $"Comdirect authentication failed: {reason}"
    | SyncError.TanTimeout -> "TAN confirmation timed out"
    | SyncError.TransactionFetchFailed msg -> $"Failed to fetch transactions: {msg}"
    | SyncError.YnabImportFailed (count, msg) -> $"Failed to import {count} transactions: {msg}"
    | SyncError.InvalidSessionState (expected, actual) -> $"Invalid session state. Expected: {expected}, Actual: {actual}"
    | SyncError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"

let private addToast (message: string) (toastType: ToastType) (model: Model) : Model * Cmd<Msg> =
    let toast = { Id = Guid.NewGuid(); Message = message; Type = toastType }
    let dismissCmd =
        Cmd.OfAsync.perform
            (fun () -> async { do! Async.Sleep 5000 })
            ()
            (fun _ -> AutoDismissToast toast.Id)
    { model with Toasts = toast :: model.Toasts }, dismissCmd

// ============================================
// Init
// ============================================

let init () : Model * Cmd<Msg> =
    let model = {
        CurrentPage = Dashboard
        Toasts = []
        CurrentSession = NotAsked
        RecentSessions = NotAsked
        Settings = NotAsked
        YnabBudgets = NotAsked
        YnabTokenInput = ""
        ComdirectClientIdInput = ""
        ComdirectClientSecretInput = ""
        ComdirectUsernameInput = ""
        ComdirectPasswordInput = ""
        ComdirectAccountIdInput = ""
        SyncDaysInput = 30
        Rules = NotAsked
        EditingRule = None
        Categories = []
        SyncTransactions = NotAsked
        SelectedTransactions = Set.empty
    }
    let cmd = Cmd.batch [
        Cmd.ofMsg LoadSettings
        Cmd.ofMsg LoadRecentSessions
        Cmd.ofMsg LoadCurrentSession
    ]
    model, cmd

// ============================================
// Update
// ============================================

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    // ============================================
    // Navigation
    // ============================================
    | NavigateTo page ->
        let extraCmds =
            match page with
            | Dashboard ->
                Cmd.batch [
                    Cmd.ofMsg LoadRecentSessions
                    Cmd.ofMsg LoadCurrentSession
                ]
            | SyncFlow ->
                Cmd.batch [
                    Cmd.ofMsg LoadCurrentSession
                    Cmd.ofMsg LoadCategories
                ]
            | Rules ->
                Cmd.batch [
                    Cmd.ofMsg LoadRules
                    Cmd.ofMsg LoadCategories
                ]
            | Settings ->
                Cmd.ofMsg LoadSettings
        { model with CurrentPage = page }, extraCmds

    // ============================================
    // Toast
    // ============================================
    | ShowToast (message, toastType) ->
        addToast message toastType model

    | DismissToast id ->
        { model with Toasts = model.Toasts |> List.filter (fun t -> t.Id <> id) }, Cmd.none

    | AutoDismissToast id ->
        { model with Toasts = model.Toasts |> List.filter (fun t -> t.Id <> id) }, Cmd.none

    // ============================================
    // Settings
    // ============================================
    | LoadSettings ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.getSettings
                ()
                (Ok >> SettingsLoaded)
                (fun ex -> Error ex.Message |> SettingsLoaded)
        { model with Settings = Loading }, cmd

    | SettingsLoaded (Ok settings) ->
        let updatedModel = {
            model with
                Settings = Success settings
                YnabTokenInput = settings.Ynab |> Option.map (fun y -> y.PersonalAccessToken) |> Option.defaultValue ""
                ComdirectClientIdInput = settings.Comdirect |> Option.map (fun c -> c.ClientId) |> Option.defaultValue ""
                ComdirectClientSecretInput = settings.Comdirect |> Option.map (fun c -> c.ClientSecret) |> Option.defaultValue ""
                ComdirectUsernameInput = settings.Comdirect |> Option.map (fun c -> c.Username) |> Option.defaultValue ""
                ComdirectPasswordInput = settings.Comdirect |> Option.map (fun c -> c.Password) |> Option.defaultValue ""
                ComdirectAccountIdInput = settings.Comdirect |> Option.bind (fun c -> c.AccountId) |> Option.defaultValue ""
                SyncDaysInput = settings.Sync.DaysToFetch
        }
        updatedModel, Cmd.none

    | SettingsLoaded (Error err) ->
        let model', cmd = addToast $"Failed to load settings: {err}" ToastError model
        { model' with Settings = Failure err }, cmd

    | UpdateYnabTokenInput value ->
        { model with YnabTokenInput = value }, Cmd.none

    | SaveYnabToken ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.saveYnabToken
                model.YnabTokenInput
                YnabTokenSaved
                (fun ex -> Error (SettingsError.DatabaseError ("save", ex.Message)) |> YnabTokenSaved)
        model, cmd

    | YnabTokenSaved (Ok _) ->
        let model', cmd = addToast "YNAB token saved successfully" ToastSuccess model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadSettings ]

    | YnabTokenSaved (Error err) ->
        addToast (settingsErrorToString err) ToastError model

    | TestYnabConnection ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.testYnabConnection
                ()
                YnabConnectionTested
                (fun ex -> Error (SettingsError.YnabConnectionFailed (0, ex.Message)) |> YnabConnectionTested)
        { model with YnabBudgets = Loading }, cmd

    | YnabConnectionTested (Ok budgets) ->
        let model', cmd = addToast $"Connected! Found {budgets.Length} budget(s)" ToastSuccess model
        { model' with YnabBudgets = Success budgets }, cmd

    | YnabConnectionTested (Error err) ->
        let model', cmd = addToast (settingsErrorToString err) ToastError model
        { model' with YnabBudgets = Failure (settingsErrorToString err) }, cmd

    | UpdateComdirectClientIdInput value ->
        { model with ComdirectClientIdInput = value }, Cmd.none

    | UpdateComdirectClientSecretInput value ->
        { model with ComdirectClientSecretInput = value }, Cmd.none

    | UpdateComdirectUsernameInput value ->
        { model with ComdirectUsernameInput = value }, Cmd.none

    | UpdateComdirectPasswordInput value ->
        { model with ComdirectPasswordInput = value }, Cmd.none

    | UpdateComdirectAccountIdInput value ->
        { model with ComdirectAccountIdInput = value }, Cmd.none

    | SaveComdirectCredentials ->
        let credentials : ComdirectSettings = {
            ClientId = model.ComdirectClientIdInput
            ClientSecret = model.ComdirectClientSecretInput
            Username = model.ComdirectUsernameInput
            Password = model.ComdirectPasswordInput
            AccountId = if String.IsNullOrWhiteSpace(model.ComdirectAccountIdInput) then None else Some model.ComdirectAccountIdInput
        }
        let cmd =
            Cmd.OfAsync.either
                Api.settings.saveComdirectCredentials
                credentials
                ComdirectCredentialsSaved
                (fun ex -> Error (SettingsError.DatabaseError ("save", ex.Message)) |> ComdirectCredentialsSaved)
        model, cmd

    | ComdirectCredentialsSaved (Ok _) ->
        let model', cmd = addToast "Comdirect credentials saved successfully" ToastSuccess model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadSettings ]

    | ComdirectCredentialsSaved (Error err) ->
        addToast (settingsErrorToString err) ToastError model

    | UpdateSyncDaysInput value ->
        { model with SyncDaysInput = value }, Cmd.none

    | SaveSyncSettings ->
        let settings : SyncSettings = { DaysToFetch = model.SyncDaysInput }
        let cmd =
            Cmd.OfAsync.either
                Api.settings.saveSyncSettings
                settings
                SyncSettingsSaved
                (fun ex -> Error (SettingsError.DatabaseError ("save", ex.Message)) |> SyncSettingsSaved)
        model, cmd

    | SyncSettingsSaved (Ok _) ->
        let model', cmd = addToast "Sync settings saved successfully" ToastSuccess model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadSettings ]

    | SyncSettingsSaved (Error err) ->
        addToast (settingsErrorToString err) ToastError model

    | SetDefaultBudget budgetId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ynab.setDefaultBudget
                budgetId
                DefaultBudgetSet
                (fun ex -> Error (YnabError.NetworkError ex.Message) |> DefaultBudgetSet)
        model, cmd

    | DefaultBudgetSet (Ok _) ->
        let model', cmd = addToast "Default budget set" ToastSuccess model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadSettings ]

    | DefaultBudgetSet (Error err) ->
        addToast (ynabErrorToString err) ToastError model

    | SetDefaultAccount accountId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ynab.setDefaultAccount
                accountId
                DefaultAccountSet
                (fun ex -> Error (YnabError.NetworkError ex.Message) |> DefaultAccountSet)
        model, cmd

    | DefaultAccountSet (Ok _) ->
        let model', cmd = addToast "Default account set" ToastSuccess model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadSettings ]

    | DefaultAccountSet (Error err) ->
        addToast (ynabErrorToString err) ToastError model

    // ============================================
    // Rules
    // ============================================
    | LoadRules ->
        let cmd =
            Cmd.OfAsync.perform
                Api.rules.getAllRules
                ()
                RulesLoaded
        { model with Rules = Loading }, cmd

    | RulesLoaded rules ->
        { model with Rules = Success rules }, Cmd.none

    | OpenNewRuleModal ->
        // For now, just show a toast - full implementation in Milestone 9
        addToast "Rule creation form will be implemented in Milestone 9" ToastInfo model

    | EditRule ruleId ->
        match model.Rules with
        | Success rules ->
            let rule = rules |> List.tryFind (fun r -> r.Id = ruleId)
            { model with EditingRule = rule }, Cmd.none
        | _ -> model, Cmd.none

    | CloseRuleModal ->
        { model with EditingRule = None }, Cmd.none

    | DeleteRule ruleId ->
        let cmd =
            Cmd.OfAsync.either
                Api.rules.deleteRule
                ruleId
                RuleDeleted
                (fun ex -> Error (RulesError.DatabaseError ("delete", ex.Message)) |> RuleDeleted)
        model, cmd

    | RuleDeleted (Ok _) ->
        let model', cmd = addToast "Rule deleted" ToastSuccess model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadRules ]

    | RuleDeleted (Error err) ->
        addToast (rulesErrorToString err) ToastError model

    | ToggleRuleEnabled ruleId ->
        match model.Rules with
        | Success rules ->
            match rules |> List.tryFind (fun r -> r.Id = ruleId) with
            | Some rule ->
                let updateRequest : RuleUpdateRequest = {
                    Id = ruleId
                    Name = None
                    Pattern = None
                    PatternType = None
                    TargetField = None
                    CategoryId = None
                    PayeeOverride = None
                    Priority = None
                    Enabled = Some (not rule.Enabled)
                }
                let cmd =
                    Cmd.OfAsync.either
                        Api.rules.updateRule
                        updateRequest
                        RuleToggled
                        (fun ex -> Error (RulesError.DatabaseError ("toggle", ex.Message)) |> RuleToggled)
                model, cmd
            | None -> model, Cmd.none
        | _ -> model, Cmd.none

    | RuleToggled (Ok _) ->
        model, Cmd.ofMsg LoadRules

    | RuleToggled (Error err) ->
        addToast (rulesErrorToString err) ToastError model

    | LoadCategories ->
        match model.Settings with
        | Success settings ->
            match settings.Ynab with
            | Some ynab ->
                match ynab.DefaultBudgetId with
                | Some budgetId ->
                    let cmd =
                        Cmd.OfAsync.either
                            Api.ynab.getCategories
                            budgetId
                            CategoriesLoaded
                            (fun ex -> Error (YnabError.NetworkError ex.Message) |> CategoriesLoaded)
                    model, cmd
                | None -> model, Cmd.none
            | None -> model, Cmd.none
        | _ -> model, Cmd.none

    | CategoriesLoaded (Ok categories) ->
        { model with Categories = categories }, Cmd.none

    | CategoriesLoaded (Error _) ->
        model, Cmd.none

    // ============================================
    // Sync Flow
    // ============================================
    | LoadCurrentSession ->
        let cmd =
            Cmd.OfAsync.perform
                Api.sync.getCurrentSession
                ()
                CurrentSessionLoaded
        { model with CurrentSession = Loading }, cmd

    | CurrentSessionLoaded session ->
        let updatedModel = { model with CurrentSession = Success session }
        match session with
        | Some s when s.Status = ReviewingTransactions ->
            updatedModel, Cmd.ofMsg LoadTransactions
        | _ -> updatedModel, Cmd.none

    | LoadRecentSessions ->
        let cmd =
            Cmd.OfAsync.perform
                Api.sync.getSyncHistory
                10
                RecentSessionsLoaded
        { model with RecentSessions = Loading }, cmd

    | RecentSessionsLoaded sessions ->
        { model with RecentSessions = Success sessions }, Cmd.none

    | StartSync ->
        let cmd =
            Cmd.OfAsync.either
                Api.sync.startSync
                ()
                SyncStarted
                (fun ex -> Error (SyncError.DatabaseError ("start", ex.Message)) |> SyncStarted)
        { model with CurrentSession = Loading }, cmd

    | SyncStarted (Ok session) ->
        let model' = { model with CurrentSession = Success (Some session) }
        model', Cmd.ofMsg InitiateComdirectAuth

    | SyncStarted (Error err) ->
        let model', cmd = addToast (syncErrorToString err) ToastError model
        { model' with CurrentSession = Failure (syncErrorToString err) }, cmd

    | InitiateComdirectAuth ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.initiateComdirectAuth
                    session.Id
                    ComdirectAuthInitiated
                    (fun ex -> Error (SyncError.ComdirectAuthFailed ex.Message) |> ComdirectAuthInitiated)
            model, cmd
        | _ -> model, Cmd.none

    | ComdirectAuthInitiated (Ok _challengeId) ->
        let model', cmd = addToast "Please confirm the TAN on your phone" ToastInfo model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadCurrentSession ]

    | ComdirectAuthInitiated (Error err) ->
        let model', cmd = addToast (syncErrorToString err) ToastError model
        { model' with CurrentSession = Failure (syncErrorToString err) }, cmd

    | ConfirmTan ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.confirmTan
                    session.Id
                    TanConfirmed
                    (fun ex -> Error (SyncError.TanTimeout) |> TanConfirmed)
            model, cmd
        | _ -> model, Cmd.none

    | TanConfirmed (Ok _) ->
        let model', cmd = addToast "TAN confirmed, fetching transactions..." ToastSuccess model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadCurrentSession; Cmd.ofMsg LoadTransactions ]

    | TanConfirmed (Error err) ->
        let model', cmd = addToast (syncErrorToString err) ToastError model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadCurrentSession ]

    | LoadTransactions ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.getTransactions
                    session.Id
                    TransactionsLoaded
                    (fun ex -> Error (SyncError.DatabaseError ("load", ex.Message)) |> TransactionsLoaded)
            { model with SyncTransactions = Loading }, cmd
        | _ -> model, Cmd.none

    | TransactionsLoaded (Ok transactions) ->
        { model with SyncTransactions = Success transactions }, Cmd.none

    | TransactionsLoaded (Error err) ->
        let model', cmd = addToast (syncErrorToString err) ToastError model
        { model' with SyncTransactions = Failure (syncErrorToString err) }, cmd

    | ToggleTransactionSelection txId ->
        let newSelection =
            if model.SelectedTransactions.Contains(txId) then
                model.SelectedTransactions.Remove(txId)
            else
                model.SelectedTransactions.Add(txId)
        { model with SelectedTransactions = newSelection }, Cmd.none

    | SelectAllTransactions ->
        match model.SyncTransactions with
        | Success transactions ->
            let allIds = transactions |> List.map (fun tx -> tx.Transaction.Id) |> Set.ofList
            { model with SelectedTransactions = allIds }, Cmd.none
        | _ -> model, Cmd.none

    | DeselectAllTransactions ->
        { model with SelectedTransactions = Set.empty }, Cmd.none

    | CategorizeTransaction (txId, categoryId) ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.categorizeTransaction
                    (session.Id, txId, categoryId, None)
                    TransactionCategorized
                    (fun ex -> Error (SyncError.DatabaseError ("categorize", ex.Message)) |> TransactionCategorized)
            model, cmd
        | _ -> model, Cmd.none

    | TransactionCategorized (Ok _) ->
        model, Cmd.ofMsg LoadTransactions

    | TransactionCategorized (Error err) ->
        addToast (syncErrorToString err) ToastError model

    | SkipTransaction txId ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.skipTransaction
                    (session.Id, txId)
                    TransactionSkipped
                    (fun ex -> Error (SyncError.DatabaseError ("skip", ex.Message)) |> TransactionSkipped)
            model, cmd
        | _ -> model, Cmd.none

    | TransactionSkipped (Ok _) ->
        model, Cmd.ofMsg LoadTransactions

    | TransactionSkipped (Error err) ->
        addToast (syncErrorToString err) ToastError model

    | BulkCategorize categoryId ->
        match model.CurrentSession with
        | Success (Some session) ->
            let txIds = model.SelectedTransactions |> Set.toList
            if txIds.IsEmpty then
                addToast "No transactions selected" ToastWarning model
            else
                let cmd =
                    Cmd.OfAsync.either
                        Api.sync.bulkCategorize
                        (session.Id, txIds, categoryId)
                        BulkCategorized
                        (fun ex -> Error (SyncError.DatabaseError ("bulk", ex.Message)) |> BulkCategorized)
                model, cmd
        | _ -> model, Cmd.none

    | BulkCategorized (Ok _) ->
        let model' = { model with SelectedTransactions = Set.empty }
        model', Cmd.ofMsg LoadTransactions

    | BulkCategorized (Error err) ->
        addToast (syncErrorToString err) ToastError model

    | ImportToYnab ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.importToYnab
                    session.Id
                    ImportCompleted
                    (fun ex -> Error (SyncError.DatabaseError ("import", ex.Message)) |> ImportCompleted)
            model, cmd
        | _ -> model, Cmd.none

    | ImportCompleted (Ok count) ->
        let model', cmd = addToast $"Successfully imported {count} transaction(s) to YNAB!" ToastSuccess model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadCurrentSession; Cmd.ofMsg LoadRecentSessions ]

    | ImportCompleted (Error err) ->
        addToast (syncErrorToString err) ToastError model

    | CancelSync ->
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.cancelSync
                    session.Id
                    SyncCancelled
                    (fun ex -> Error (SyncError.DatabaseError ("cancel", ex.Message)) |> SyncCancelled)
            model, cmd
        | _ -> model, Cmd.none

    | SyncCancelled (Ok _) ->
        let model' = {
            model with
                CurrentSession = Success None
                SyncTransactions = NotAsked
                SelectedTransactions = Set.empty
        }
        let model'', cmd = addToast "Sync cancelled" ToastInfo model'
        model'', cmd

    | SyncCancelled (Error err) ->
        addToast (syncErrorToString err) ToastError model
