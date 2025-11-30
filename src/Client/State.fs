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
    IsNewRule: bool
    Categories: YnabCategory list

    // Rule form state
    RuleFormName: string
    RuleFormPattern: string
    RuleFormPatternType: PatternType
    RuleFormTargetField: TargetField
    RuleFormCategoryId: YnabCategoryId option
    RuleFormPayeeOverride: string
    RuleFormEnabled: bool
    RuleFormTestInput: string
    RuleFormTestResult: string option
    RuleSaving: bool

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

    // Rule form
    | UpdateRuleFormName of string
    | UpdateRuleFormPattern of string
    | UpdateRuleFormPatternType of PatternType
    | UpdateRuleFormTargetField of TargetField
    | UpdateRuleFormCategoryId of YnabCategoryId option
    | UpdateRuleFormPayeeOverride of string
    | UpdateRuleFormEnabled of bool
    | UpdateRuleFormTestInput of string
    | TestRulePattern
    | RulePatternTested of Result<bool, RulesError>
    | SaveRule
    | RuleSaved of Result<Rule, RulesError>
    | ExportRules
    | RulesExported of Result<string, RulesError>
    | ImportRulesStart
    | ImportRules of string
    | RulesImported of Result<int, RulesError>

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

let private emptyRuleForm () = {|
    Name = ""
    Pattern = ""
    PatternType = Contains
    TargetField = Combined
    CategoryId = None
    PayeeOverride = ""
    Enabled = true
    TestInput = ""
    TestResult = None
|}

let init () : Model * Cmd<Msg> =
    let emptyForm = emptyRuleForm ()
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
        IsNewRule = false
        Categories = []
        RuleFormName = emptyForm.Name
        RuleFormPattern = emptyForm.Pattern
        RuleFormPatternType = emptyForm.PatternType
        RuleFormTargetField = emptyForm.TargetField
        RuleFormCategoryId = emptyForm.CategoryId
        RuleFormPayeeOverride = emptyForm.PayeeOverride
        RuleFormEnabled = emptyForm.Enabled
        RuleFormTestInput = emptyForm.TestInput
        RuleFormTestResult = emptyForm.TestResult
        RuleSaving = false
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
        let emptyForm = emptyRuleForm ()
        let nextPriority =
            match model.Rules with
            | Success rules -> (rules |> List.map (fun r -> r.Priority) |> List.fold max 0) + 1
            | _ -> 1
        { model with
            EditingRule = None
            IsNewRule = true
            RuleFormName = emptyForm.Name
            RuleFormPattern = emptyForm.Pattern
            RuleFormPatternType = emptyForm.PatternType
            RuleFormTargetField = emptyForm.TargetField
            RuleFormCategoryId = emptyForm.CategoryId
            RuleFormPayeeOverride = emptyForm.PayeeOverride
            RuleFormEnabled = emptyForm.Enabled
            RuleFormTestInput = emptyForm.TestInput
            RuleFormTestResult = emptyForm.TestResult
            RuleSaving = false
        }, Cmd.ofMsg LoadCategories

    | EditRule ruleId ->
        match model.Rules with
        | Success rules ->
            match rules |> List.tryFind (fun r -> r.Id = ruleId) with
            | Some rule ->
                { model with
                    EditingRule = Some rule
                    IsNewRule = false
                    RuleFormName = rule.Name
                    RuleFormPattern = rule.Pattern
                    RuleFormPatternType = rule.PatternType
                    RuleFormTargetField = rule.TargetField
                    RuleFormCategoryId = Some rule.CategoryId
                    RuleFormPayeeOverride = rule.PayeeOverride |> Option.defaultValue ""
                    RuleFormEnabled = rule.Enabled
                    RuleFormTestInput = ""
                    RuleFormTestResult = None
                    RuleSaving = false
                }, Cmd.ofMsg LoadCategories
            | None -> model, Cmd.none
        | _ -> model, Cmd.none

    | CloseRuleModal ->
        let emptyForm = emptyRuleForm ()
        { model with
            EditingRule = None
            IsNewRule = false
            RuleFormName = emptyForm.Name
            RuleFormPattern = emptyForm.Pattern
            RuleFormPatternType = emptyForm.PatternType
            RuleFormTargetField = emptyForm.TargetField
            RuleFormCategoryId = emptyForm.CategoryId
            RuleFormPayeeOverride = emptyForm.PayeeOverride
            RuleFormEnabled = emptyForm.Enabled
            RuleFormTestInput = emptyForm.TestInput
            RuleFormTestResult = emptyForm.TestResult
            RuleSaving = false
        }, Cmd.none

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
    // Rule Form
    // ============================================
    | UpdateRuleFormName value ->
        { model with RuleFormName = value }, Cmd.none

    | UpdateRuleFormPattern value ->
        { model with RuleFormPattern = value; RuleFormTestResult = None }, Cmd.none

    | UpdateRuleFormPatternType value ->
        { model with RuleFormPatternType = value; RuleFormTestResult = None }, Cmd.none

    | UpdateRuleFormTargetField value ->
        { model with RuleFormTargetField = value }, Cmd.none

    | UpdateRuleFormCategoryId value ->
        { model with RuleFormCategoryId = value }, Cmd.none

    | UpdateRuleFormPayeeOverride value ->
        { model with RuleFormPayeeOverride = value }, Cmd.none

    | UpdateRuleFormEnabled value ->
        { model with RuleFormEnabled = value }, Cmd.none

    | UpdateRuleFormTestInput value ->
        { model with RuleFormTestInput = value; RuleFormTestResult = None }, Cmd.none

    | TestRulePattern ->
        if String.IsNullOrWhiteSpace(model.RuleFormPattern) || String.IsNullOrWhiteSpace(model.RuleFormTestInput) then
            addToast "Please enter both a pattern and test input" ToastWarning model
        else
            let cmd =
                Cmd.OfAsync.either
                    Api.rules.testRule
                    (model.RuleFormPattern, model.RuleFormPatternType, model.RuleFormTargetField, model.RuleFormTestInput)
                    (Ok >> RulePatternTested)
                    (fun ex -> Error (RulesError.InvalidPattern (model.RuleFormPattern, ex.Message)) |> RulePatternTested)
            model, cmd

    | RulePatternTested (Ok matches) ->
        let resultText = if matches then "✅ Pattern matches!" else "❌ Pattern does not match"
        { model with RuleFormTestResult = Some resultText }, Cmd.none

    | RulePatternTested (Error err) ->
        { model with RuleFormTestResult = Some $"⚠️ {rulesErrorToString err}" }, Cmd.none

    | SaveRule ->
        match model.RuleFormCategoryId with
        | None ->
            addToast "Please select a category" ToastWarning model
        | Some categoryId ->
            if String.IsNullOrWhiteSpace(model.RuleFormName) then
                addToast "Please enter a rule name" ToastWarning model
            elif String.IsNullOrWhiteSpace(model.RuleFormPattern) then
                addToast "Please enter a pattern" ToastWarning model
            else
                let payeeOverride = if String.IsNullOrWhiteSpace(model.RuleFormPayeeOverride) then None else Some model.RuleFormPayeeOverride
                if model.IsNewRule then
                    let nextPriority =
                        match model.Rules with
                        | Success rules -> (rules |> List.map (fun r -> r.Priority) |> List.fold max 0) + 1
                        | _ -> 1
                    let request : RuleCreateRequest = {
                        Name = model.RuleFormName
                        Pattern = model.RuleFormPattern
                        PatternType = model.RuleFormPatternType
                        TargetField = model.RuleFormTargetField
                        CategoryId = categoryId
                        PayeeOverride = payeeOverride
                        Priority = nextPriority
                    }
                    let cmd =
                        Cmd.OfAsync.either
                            Api.rules.createRule
                            request
                            RuleSaved
                            (fun ex -> Error (RulesError.DatabaseError ("create", ex.Message)) |> RuleSaved)
                    { model with RuleSaving = true }, cmd
                else
                    match model.EditingRule with
                    | Some rule ->
                        let request : RuleUpdateRequest = {
                            Id = rule.Id
                            Name = Some model.RuleFormName
                            Pattern = Some model.RuleFormPattern
                            PatternType = Some model.RuleFormPatternType
                            TargetField = Some model.RuleFormTargetField
                            CategoryId = Some categoryId
                            PayeeOverride = payeeOverride
                            Priority = None
                            Enabled = Some model.RuleFormEnabled
                        }
                        let cmd =
                            Cmd.OfAsync.either
                                Api.rules.updateRule
                                request
                                RuleSaved
                                (fun ex -> Error (RulesError.DatabaseError ("update", ex.Message)) |> RuleSaved)
                        { model with RuleSaving = true }, cmd
                    | None -> model, Cmd.none

    | RuleSaved (Ok _) ->
        let action = if model.IsNewRule then "created" else "updated"
        let model', cmd = addToast $"Rule {action} successfully" ToastSuccess model
        let emptyForm = emptyRuleForm ()
        { model' with
            EditingRule = None
            IsNewRule = false
            RuleFormName = emptyForm.Name
            RuleFormPattern = emptyForm.Pattern
            RuleFormPatternType = emptyForm.PatternType
            RuleFormTargetField = emptyForm.TargetField
            RuleFormCategoryId = emptyForm.CategoryId
            RuleFormPayeeOverride = emptyForm.PayeeOverride
            RuleFormEnabled = emptyForm.Enabled
            RuleFormTestInput = emptyForm.TestInput
            RuleFormTestResult = emptyForm.TestResult
            RuleSaving = false
        }, Cmd.batch [ cmd; Cmd.ofMsg LoadRules ]

    | RuleSaved (Error err) ->
        let model', cmd = addToast (rulesErrorToString err) ToastError model
        { model' with RuleSaving = false }, cmd

    | ExportRules ->
        let cmd =
            Cmd.OfAsync.either
                Api.rules.exportRules
                ()
                (Ok >> RulesExported)
                (fun ex -> Error (RulesError.DatabaseError ("export", ex.Message)) |> RulesExported)
        model, cmd

    | RulesExported (Ok json) ->
        // Trigger browser download using direct JS interop
        Fable.Core.JS.eval(sprintf """
            (function() {
                var blob = new Blob([%s], {type: 'application/json'});
                var url = URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = url;
                a.download = 'budgetbuddy-rules.json';
                a.click();
                URL.revokeObjectURL(url);
            })();
        """ (Fable.Core.JS.JSON.stringify json)) |> ignore
        addToast "Rules exported successfully" ToastSuccess model

    | RulesExported (Error err) ->
        addToast (rulesErrorToString err) ToastError model

    | ImportRulesStart ->
        // This message triggers file input click from the view
        model, Cmd.none

    | ImportRules json ->
        let cmd =
            Cmd.OfAsync.either
                Api.rules.importRules
                json
                RulesImported
                (fun ex -> Error (RulesError.DatabaseError ("import", ex.Message)) |> RulesImported)
        model, cmd

    | RulesImported (Ok count) ->
        let model', cmd = addToast $"Imported {count} rule(s) successfully" ToastSuccess model
        model', Cmd.batch [ cmd; Cmd.ofMsg LoadRules ]

    | RulesImported (Error err) ->
        addToast (rulesErrorToString err) ToastError model

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
