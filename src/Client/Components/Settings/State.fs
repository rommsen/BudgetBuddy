module Components.Settings.State

open System
open Elmish
open Components.Settings.Types
open Types
open Shared.Domain

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

let init () : Model * Cmd<Msg> =
    let model = {
        Settings = NotAsked
        YnabBudgets = NotAsked
        YnabTokenInput = ""
        ComdirectClientIdInput = ""
        ComdirectClientSecretInput = ""
        ComdirectUsernameInput = ""
        ComdirectPasswordInput = ""
        ComdirectAccountIdInput = ""
        SyncDaysInput = 30
        ComdirectConnectionValid = NotAsked
        ComdirectAuthPending = false
    }
    model, Cmd.ofMsg LoadSettings

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | LoadSettings ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.getSettings
                ()
                (Ok >> SettingsLoaded)
                (fun ex -> Error ex.Message |> SettingsLoaded)
        { model with Settings = Loading }, cmd, NoOp

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
        updatedModel, Cmd.none, NoOp

    | SettingsLoaded (Error err) ->
        { model with Settings = Failure err }, Cmd.none, ShowToast ($"Failed to load settings: {err}", ToastError)

    | UpdateYnabTokenInput value ->
        { model with YnabTokenInput = value }, Cmd.none, NoOp

    | SaveYnabToken ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.saveYnabToken
                model.YnabTokenInput
                YnabTokenSaved
                (fun ex -> Error (SettingsError.DatabaseError ("save", ex.Message)) |> YnabTokenSaved)
        model, cmd, NoOp

    | YnabTokenSaved (Ok _) ->
        // Update local state with the saved token
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                let newYnab: YnabSettings = {
                    PersonalAccessToken = model.YnabTokenInput
                    DefaultBudgetId = settings.Ynab |> Option.bind (fun y -> y.DefaultBudgetId)
                    DefaultAccountId = settings.Ynab |> Option.bind (fun y -> y.DefaultAccountId)
                }
                Success { settings with Ynab = Some newYnab }
            | other -> other
        { model with Settings = updatedSettings }, Cmd.none, ShowToast ("YNAB token saved successfully", ToastSuccess)

    | YnabTokenSaved (Error err) ->
        model, Cmd.none, ShowToast (settingsErrorToString err, ToastError)

    | TestYnabConnection ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.testYnabConnection
                ()
                YnabConnectionTested
                (fun ex -> Error (SettingsError.YnabConnectionFailed (0, ex.Message)) |> YnabConnectionTested)
        { model with YnabBudgets = Loading }, cmd, NoOp

    | YnabConnectionTested (Ok budgets) ->
        { model with YnabBudgets = Success budgets }, Cmd.none, ShowToast ($"Connected! Found {budgets.Length} budget(s)", ToastSuccess)

    | YnabConnectionTested (Error err) ->
        { model with YnabBudgets = Failure (settingsErrorToString err) }, Cmd.none, ShowToast (settingsErrorToString err, ToastError)

    | UpdateComdirectClientIdInput value ->
        { model with ComdirectClientIdInput = value }, Cmd.none, NoOp

    | UpdateComdirectClientSecretInput value ->
        { model with ComdirectClientSecretInput = value }, Cmd.none, NoOp

    | UpdateComdirectUsernameInput value ->
        { model with ComdirectUsernameInput = value }, Cmd.none, NoOp

    | UpdateComdirectPasswordInput value ->
        { model with ComdirectPasswordInput = value }, Cmd.none, NoOp

    | UpdateComdirectAccountIdInput value ->
        { model with ComdirectAccountIdInput = value }, Cmd.none, NoOp

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
        model, cmd, NoOp

    | ComdirectCredentialsSaved (Ok _) ->
        // Update local state with the saved credentials
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                let newComdirect: ComdirectSettings = {
                    ClientId = model.ComdirectClientIdInput
                    ClientSecret = model.ComdirectClientSecretInput
                    Username = model.ComdirectUsernameInput
                    Password = model.ComdirectPasswordInput
                    AccountId = if String.IsNullOrWhiteSpace(model.ComdirectAccountIdInput) then None else Some model.ComdirectAccountIdInput
                }
                Success { settings with Comdirect = Some newComdirect }
            | other -> other
        { model with Settings = updatedSettings }, Cmd.none, ShowToast ("Comdirect credentials saved successfully", ToastSuccess)

    | ComdirectCredentialsSaved (Error err) ->
        model, Cmd.none, ShowToast (settingsErrorToString err, ToastError)

    | UpdateSyncDaysInput value ->
        { model with SyncDaysInput = value }, Cmd.none, NoOp

    | SaveSyncSettings ->
        let settings : SyncSettings = { DaysToFetch = model.SyncDaysInput }
        let cmd =
            Cmd.OfAsync.either
                Api.settings.saveSyncSettings
                settings
                SyncSettingsSaved
                (fun ex -> Error (SettingsError.DatabaseError ("save", ex.Message)) |> SyncSettingsSaved)
        model, cmd, NoOp

    | SyncSettingsSaved (Ok _) ->
        // Update local state with the saved sync settings
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                Success { settings with Sync = { DaysToFetch = model.SyncDaysInput } }
            | other -> other
        { model with Settings = updatedSettings }, Cmd.none, ShowToast ("Sync settings saved successfully", ToastSuccess)

    | SyncSettingsSaved (Error err) ->
        model, Cmd.none, ShowToast (settingsErrorToString err, ToastError)

    | SetDefaultBudget budgetId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ynab.setDefaultBudget
                budgetId
                (fun result -> DefaultBudgetSet (budgetId, result))
                (fun ex -> DefaultBudgetSet (budgetId, Error (YnabError.NetworkError ex.Message)))
        model, cmd, NoOp

    | DefaultBudgetSet (budgetId, Ok _) ->
        // Update local state with the new default budget
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                match settings.Ynab with
                | Some ynab ->
                    Success { settings with Ynab = Some { ynab with DefaultBudgetId = Some budgetId } }
                | None -> Success settings
            | other -> other
        { model with Settings = updatedSettings }, Cmd.none, ShowToast ("Default budget set", ToastSuccess)

    | DefaultBudgetSet (_, Error err) ->
        model, Cmd.none, ShowToast (ynabErrorToString err, ToastError)

    | SetDefaultAccount accountId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ynab.setDefaultAccount
                accountId
                (fun result -> DefaultAccountSet (accountId, result))
                (fun ex -> DefaultAccountSet (accountId, Error (YnabError.NetworkError ex.Message)))
        model, cmd, NoOp

    | DefaultAccountSet (accountId, Ok _) ->
        // Update local state with the new default account
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                match settings.Ynab with
                | Some ynab ->
                    Success { settings with Ynab = Some { ynab with DefaultAccountId = Some accountId } }
                | None -> Success settings
            | other -> other
        { model with Settings = updatedSettings }, Cmd.none, ShowToast ("Default account set", ToastSuccess)

    | DefaultAccountSet (_, Error err) ->
        model, Cmd.none, ShowToast (ynabErrorToString err, ToastError)

    // Comdirect connection test
    | TestComdirectConnection ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.testComdirectConnection
                ()
                ComdirectAuthStarted
                (fun ex -> Error (SettingsError.ComdirectCredentialsInvalid ("network", ex.Message)) |> ComdirectAuthStarted)
        { model with ComdirectConnectionValid = Loading }, cmd, NoOp

    | ComdirectAuthStarted (Ok _challengeId) ->
        // TAN challenge started - show waiting UI
        { model with ComdirectAuthPending = true; ComdirectConnectionValid = NotAsked }, Cmd.none, ShowToast ("Please confirm the TAN on your phone", ToastInfo)

    | ComdirectAuthStarted (Error err) ->
        { model with ComdirectConnectionValid = Failure (settingsErrorToString err); ComdirectAuthPending = false }, Cmd.none, ShowToast (settingsErrorToString err, ToastError)

    | ConfirmComdirectTan ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.confirmComdirectTan
                ()
                ComdirectTanConfirmed
                (fun ex -> Error (SettingsError.ComdirectCredentialsInvalid ("tan", ex.Message)) |> ComdirectTanConfirmed)
        { model with ComdirectConnectionValid = Loading }, cmd, NoOp

    | ComdirectTanConfirmed (Ok _) ->
        { model with
            ComdirectConnectionValid = Success ()
            ComdirectAuthPending = false
        }, Cmd.none, ShowToast ("Comdirect credentials verified successfully!", ToastSuccess)

    | ComdirectTanConfirmed (Error err) ->
        { model with
            ComdirectConnectionValid = Failure (settingsErrorToString err)
            ComdirectAuthPending = false
        }, Cmd.none, ShowToast (settingsErrorToString err, ToastError)
