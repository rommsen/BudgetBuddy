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

/// Welches Element von „Automatisch abrufen“ ein Speichern ausgelöst hat. Dort steht eine Ablehnung (§5).
type AutoFetchControl =
    | AutoFetchSwitch
    | AutoFetchIntervalField

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

    // Automatisch abrufen (banking-import-t4raw, ADR 0093 Punkt 9): Schalter und Intervall speichern sofort.
    // Solange gespeichert wird, sind beide gesperrt; eine Ablehnung setzt beide auf den gespeicherten Stand
    // zurück und steht am Element, das gespeichert hat.
    AutoFetchEnabledInput: bool
    AutoFetchIntervalInput: int
    AutoFetchSaving: bool
    AutoFetchError: (AutoFetchControl * string) option

    // Lena fragen (ledger-xfjcp, ADR 0056): die Farbe im Auswahlfeld ("" = keine) und der
    // geladene Stand mit Vorschlag.
    LenaFlagColorInput: string
    LenaFlag: LenaFlagSettings

    // Comdirect connection test state
    ComdirectConnectionValid: RemoteData<unit>
    ComdirectAuthPending: bool

    // Bankverbindung (ADR 0071): "Bank trennen" fragt einmal nach. Scheitert das Löschen, bleibt die
    // Verbindung bestehen und `BankStatus` zeigt das. Die Meldung erklärt nur, warum das Trennen nicht
    // geklappt hat, und darf beim nächsten Versuch verschwinden.
    BankStatus: RemoteData<BankConnectionStatus>
    /// `Retry.reading` hält eine Antwort zurück, nur die zum aktuellen Stand zählt.
    BankStatusReadVersion: int
    ConfirmingBankDisconnect: bool
    BankDisconnect: RemoteData<unit>

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
    | SetAutoFetchEnabled of bool
    | SetAutoFetchInterval of int
    | AutoFetchSaved of SyncSettings * Result<unit, SettingsError>
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
    | ComdirectAuthStarted of Result<ComdirectConnectionTest, SettingsError>
    | ConfirmComdirectTan
    | ComdirectTanConfirmed of Result<unit, SettingsError>
    // Bankverbindung
    | LoadBankStatus
    | BankStatusLoaded of version: int * Result<BankConnectionStatus, string>
    | RequestBankDisconnect
    | AbortBankDisconnect
    | ConfirmBankDisconnect
    | BankDisconnectFinished of SyncResult<unit>
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

/// Die Stufen im Auswahlfeld „Alle“ (Roman, 2026-11-11): am Handy ohne Tastatur bedienbar. Der Server nimmt
/// jeden Wert von 60 bis 1440 Minuten; ein gespeicherter Wert außerhalb der Stufen steht zusätzlich in der
/// Liste, damit das Feld ihn zeigt.
let autoFetchIntervalOptions (current: int) : (int * string) list =
    let steps =
        [ 1; 2; 3; 4; 6; 8; 12; 24 ]
        |> List.map (fun hours -> hours * 60, (if hours = 1 then "1 Stunde" else $"{hours} Stunden"))
    if steps |> List.exists (fun (minutes, _) -> minutes = current) then steps
    else (current, $"{current} Minuten") :: steps |> List.sortBy fst

/// Das Auswahlfeld ist gesperrt, solange der Schalter aus ist (sein Wert bleibt) oder gespeichert wird.
let autoFetchIntervalLocked (model: Model) : bool =
    not model.AutoFetchEnabledInput || model.AutoFetchSaving

/// Die Ablehnung, die an diesem Element steht.
let autoFetchErrorAt (control: AutoFetchControl) (model: Model) : string option =
    match model.AutoFetchError with
    | Some (at, message) when at = control -> Some message
    | _ -> None

/// „Speichern“ der Karte Sync-Einstellungen ist gesperrt, solange „Automatisch abrufen“ speichert: beide
/// schreiben alle drei Werte, das spätere Speichern überschriebe das frühere.
let syncSaveLocked (model: Model) : bool =
    model.AutoFetchSaving

/// Was „Automatisch abrufen“ speichert: Schalter und Intervall aus der Eingabe, die Tage wie gespeichert.
/// `saveSyncSettings` speichert alle drei, die Karte Sync-Einstellungen bleibt so unberührt.
let autoFetchSettingsToSave (model: Model) : SyncSettings =
    let savedDays =
        match model.Settings with
        | Success settings -> settings.Sync.DaysToFetch
        | _ -> model.SyncDaysInput
    { DaysToFetch = savedDays
      AutoFetchEnabled = model.AutoFetchEnabledInput
      AutoFetchIntervalMinutes = model.AutoFetchIntervalInput }

/// Was die Karte Sync-Einstellungen speichert: die Tage aus der Eingabe, Schalter und Intervall wie gespeichert.
let syncDaysSettingsToSave (model: Model) : SyncSettings =
    match model.Settings with
    | Success settings -> { settings.Sync with DaysToFetch = model.SyncDaysInput }
    | _ ->
        { DaysToFetch = model.SyncDaysInput
          AutoFetchEnabled = model.AutoFetchEnabledInput
          AutoFetchIntervalMinutes = model.AutoFetchIntervalInput }

/// Ein gesetzter Token muss nicht neu eingegeben werden: der Server gibt ihn nicht heraus.
let ynabTokenIsSet (model: Model) : bool =
    loadedSettings model
    |> Option.bind (fun s -> s.Ynab)
    |> Option.exists (fun y -> y.HasToken)

let comdirectClientIdIsSet (model: Model) : bool =
    loadedSettings model
    |> Option.bind (fun s -> s.Comdirect)
    |> Option.exists (fun c -> c.HasClientId)

let comdirectClientSecretIsSet (model: Model) : bool =
    loadedSettings model
    |> Option.bind (fun s -> s.Comdirect)
    |> Option.exists (fun c -> c.HasClientSecret)

let comdirectUsernameIsSet (model: Model) : bool =
    loadedSettings model
    |> Option.bind (fun s -> s.Comdirect)
    |> Option.exists (fun c -> c.HasUsername)

let comdirectPasswordIsSet (model: Model) : bool =
    loadedSettings model
    |> Option.bind (fun s -> s.Comdirect)
    |> Option.exists (fun c -> c.HasPassword)

/// Beginn eines Lesevorgangs. Ältere Antworten verfallen, ein bekannter Stand bleibt sichtbar.
let beginBankStatusRead (model: Model) : Model =
    { model with
        BankStatus = (match model.BankStatus with NotAsked -> Loading | known -> known)
        BankStatusReadVersion = model.BankStatusReadVersion + 1 }

/// Der Text zur Anzeige. Der Server liefert hier nur `DatabaseError` mit fertiger Meldung.
let bankDisconnectErrorText (error: SyncError) : string =
    match error with
    | SyncError.DatabaseError (_, message) -> message
    | _ -> "Bank trennen gescheitert. Stand prüfen oder wiederholen."

/// "Bank trennen" fehlt nur, wenn es nichts zu trennen gibt. Auch aus `Disconnected` kann ein Wert
/// liegen, den das Löschen nicht erreicht hat: dann bleibt der Knopf für den zweiten Versuch.
let canDisconnectBank (model: Model) : bool =
    match model.BankStatus, model.BankDisconnect with
    | Success (BankConnected | BankChallengePending), _
    | Failure _, _ -> true
    | _, (Failure _ | Loading) -> true
    | _ -> false

/// Sichtbar ist, was die Bank wirklich erwartet, auch nach einem Neuladen oder aus einem Sync.
let isTanPending (model: Model) : bool =
    model.ComdirectAuthPending || model.BankStatus = Success BankChallengePending

/// Während des Trennens sind Bearbeiten und Verbindung testen gesperrt.
let comdirectActionsLocked (model: Model) : bool =
    RemoteData.isLoading model.BankDisconnect

/// Der Abschnitt "Verbindung testen" im Formular. Bei offener TAN steht der Wartezustand schon über
/// dem Formular, ein zweiter TAN-Block entfiele sonst nicht.
let connectionTestVisible (model: Model) : bool =
    let hasCredentials =
        match model.Settings with
        | Success s -> s.Comdirect.IsSome
        | _ -> false
    hasCredentials && not (isTanPending model)

/// Was das Comdirect-Formular an den Server schickt. Rein, testbar ohne Proxy.
let comdirectCredentialsFromForm (model: Model) : ComdirectSettings =
    { ClientId = model.ComdirectClientIdInput
      ClientSecret = model.ComdirectClientSecretInput
      Username = model.ComdirectUsernameInput
      Password = model.ComdirectPasswordInput
      AccountId = if System.String.IsNullOrWhiteSpace model.ComdirectAccountIdInput then None else Some model.ComdirectAccountIdInput }

/// Pflichtfelder des Token-Formulars.
let ynabRequiredFields (model: Model) : (string * string) list =
    [ if not (ynabTokenIsSet model) then
          "Persönlicher Access Token", model.YnabTokenInput ]

/// Pflichtfelder des Comdirect-Formulars. Gesetzte Zugangsdaten dürfen leer bleiben, der Server behält sie.
let comdirectRequiredFields (model: Model) : (string * string) list =
    [ if not (comdirectClientIdIsSet model) then
          "Client-ID", model.ComdirectClientIdInput
      if not (comdirectClientSecretIsSet model) then
          "Client-Secret", model.ComdirectClientSecretInput
      if not (comdirectUsernameIsSet model) then
          "Benutzername", model.ComdirectUsernameInput
      if not (comdirectPasswordIsSet model) then
          "PIN", model.ComdirectPasswordInput
      "Konto-ID", model.ComdirectAccountIdInput ]
