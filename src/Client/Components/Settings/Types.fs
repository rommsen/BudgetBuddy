module Components.Settings.Types

open Shared.Domain
open Types

/// Which settings section is being edited
type SettingsSection = | YnabSection | ComdirectSection | SyncSection | LenaSection

/// Der Anzeigename einer YNAB-Flag-Farbe in der Einstellung "Lena fragen" (ADR 0056, Punkt 1).
let lenaFlagColorLabel (color: string) : string =
    match color with
    | "red" -> "Rot"
    | "orange" -> "Orange"
    | "yellow" -> "Gelb"
    | "green" -> "Grün"
    | "blue" -> "Blau"
    | "purple" -> "Violett"
    | other -> other

/// Die Farbe, die das Auswahlfeld beim Laden vorbelegt: die gespeicherte, sonst der Vorschlag
/// (Farbe der jüngsten gespiegelten Buchung mit Flag-Name "Lena"), sonst keine. Ein Vorschlag
/// gilt erst, wenn Roman ihn speichert (ADR 0056, Punkt 1). Rein, testbar ohne Proxy.
let lenaFlagColorPreselection (settings: LenaFlagSettings) : string option =
    settings.SavedColor |> Option.orElse settings.SuggestedColor

/// Settings-specific model state
type Model = {
    Settings: RemoteData<AppSettingsView>
    YnabBudgets: RemoteData<YnabBudgetWithAccounts list>

    // Form inputs
    YnabTokenInput: string
    ComdirectClientIdInput: string
    ComdirectClientSecretInput: string
    ComdirectUsernameInput: string
    ComdirectPasswordInput: string
    ComdirectAccountIdInput: string
    SyncDaysInput: int

    // Lena fragen (ledger-xfjcp, ADR 0056): die Farbe im Auswahlfeld ("" = keine) und der
    // geladene Stand mit Vorschlag.
    LenaFlagColorInput: string
    LenaFlag: LenaFlagSettings

    // Comdirect connection test state
    ComdirectConnectionValid: RemoteData<unit>
    ComdirectAuthPending: bool

    // Edit mode: None = read-only, Some X = editing that section
    EditingSection: SettingsSection option
}

/// Settings-specific messages
type Msg =
    | LoadSettings
    | SettingsLoaded of Result<AppSettingsView, string>
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
    | LoadLenaFlagSettings
    | LenaFlagSettingsLoaded of Result<LenaFlagSettings, string>
    | UpdateLenaFlagColorInput of string
    | SaveLenaFlagColor
    | LenaFlagColorSaved of string option * Result<unit, SettingsError>
    | SetDefaultBudget of YnabBudgetId
    | DefaultBudgetSet of YnabBudgetId * Result<unit, YnabError>
    | SetDefaultAccount of YnabAccountId
    | DefaultAccountSet of YnabAccountId * Result<unit, YnabError>
    | SetQuickAddAccount of YnabAccountId
    | QuickAddAccountSet of YnabAccountId * Result<unit, YnabError>
    // Comdirect connection test
    | TestComdirectConnection
    | ComdirectAuthStarted of Result<string, SettingsError>
    | ConfirmComdirectTan
    | ComdirectTanConfirmed of Result<unit, SettingsError>
    // Edit mode toggling
    | StartEditing of SettingsSection
    | CancelEditing

/// External message to notify parent of events (like showing toasts)
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType

let private loadedSettings (model: Model) =
    match model.Settings with
    | Success settings -> Some settings
    | _ -> None

/// Ein gesetzter Token muss nicht neu eingegeben werden: der Server gibt ihn nicht heraus.
let ynabTokenIsSet (model: Model) : bool =
    loadedSettings model
    |> Option.bind (fun s -> s.Ynab)
    |> Option.exists (fun y -> y.HasToken)

let comdirectClientSecretIsSet (model: Model) : bool =
    loadedSettings model
    |> Option.bind (fun s -> s.Comdirect)
    |> Option.exists (fun c -> c.HasClientSecret)

let comdirectPasswordIsSet (model: Model) : bool =
    loadedSettings model
    |> Option.bind (fun s -> s.Comdirect)
    |> Option.exists (fun c -> c.HasPassword)

/// Pflichtfelder des Token-Formulars.
let ynabRequiredFields (model: Model) : (string * string) list =
    [ if not (ynabTokenIsSet model) then
          "Persönlicher Access Token", model.YnabTokenInput ]

/// Pflichtfelder des Comdirect-Formulars. Gesetzte Secrets dürfen leer bleiben, der Server behält sie.
let comdirectRequiredFields (model: Model) : (string * string) list =
    [ "Client-ID", model.ComdirectClientIdInput
      if not (comdirectClientSecretIsSet model) then
          "Client-Secret", model.ComdirectClientSecretInput
      "Benutzername", model.ComdirectUsernameInput
      if not (comdirectPasswordIsSet model) then
          "PIN", model.ComdirectPasswordInput
      "Konto-ID", model.ComdirectAccountIdInput ]
