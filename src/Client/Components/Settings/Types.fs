module Components.Settings.Types

open Shared.Domain
open Types

/// Settings-specific model state
type Model = {
    Settings: RemoteData<AppSettings>
    YnabBudgets: RemoteData<YnabBudgetWithAccounts list>

    // Form inputs
    YnabTokenInput: string
    ComdirectClientIdInput: string
    ComdirectClientSecretInput: string
    ComdirectUsernameInput: string
    ComdirectPasswordInput: string
    ComdirectAccountIdInput: string
    SyncDaysInput: int

    // Comdirect connection test state
    ComdirectConnectionValid: RemoteData<unit>
    ComdirectAuthPending: bool
}

/// Settings-specific messages
type Msg =
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
    | DefaultBudgetSet of YnabBudgetId * Result<unit, YnabError>
    | SetDefaultAccount of YnabAccountId
    | DefaultAccountSet of YnabAccountId * Result<unit, YnabError>
    // Comdirect connection test
    | TestComdirectConnection
    | ComdirectAuthStarted of Result<string, SettingsError>
    | ConfirmComdirectTan
    | ComdirectTanConfirmed of Result<unit, SettingsError>

/// External message to notify parent of events (like showing toasts)
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
