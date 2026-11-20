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

/// Ein Feld, das beim Ändern selbst speichert. „✓ Gespeichert“ und Fehler stehen darunter (banking-import-vwhfm).
type SettingsField =
    | DefaultBudgetField
    | DefaultAccountField
    | QuickAddAccountField
    | AutoFetchField of AutoFetchControl

/// Wo eine Rückmeldung der Einstellungen steht (Regel R3, ADR 0098): auf allen Breiten am Ort, nie als Toast.
type FeedbackPlace =
    /// „✓ Gespeichert“ im Kopf der Karte statt der Statusanzeige, nach dem Speichern eines Formulars.
    | CardHead of SettingsSection
    /// Fehler beim Speichern eines Formulars: unter dem Feld (Comdirect: über „Zugangsdaten speichern“).
    | FormField of SettingsSection
    /// Unter einem Feld, das selbst speichert.
    | Field of SettingsField
    /// Unter „Verbindung testen“ der Karte YNAB, in Lesen und Bearbeiten.
    | YnabTestResult
    /// Im Block Bankverbindung, in Lesen und Bearbeiten.
    | BankBlock

/// Gelungen (Erfolgsrolle) oder gescheitert (rot); der Text steht fertig darin.
[<RequireQualifiedAccess>]
type SettingsNote =
    | Done of string
    | Failed of string

/// Was nach einem Speichern kurz leuchtet.
type FlashTarget =
    /// Die geänderte Zeile im Lesen-Modus der Karte.
    | SectionRow of SettingsSection
    /// Die Zeile „Bankverbindung“ mit der Plakette.
    | BankRow

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

    /// Die eine Rückmeldung der Seite, bis zur nächsten Aktion (`isNextAction`) oder bis man die Seite verlässt.
    Feedback: (FeedbackPlace * SettingsNote) option
    /// Was gerade leuchtet; `FlashEnded` nimmt es ab.
    Flash: FlashTarget option
    /// Ein Verbindungstest, TAN oder Trennen gelang: die Zeile Bankverbindung leuchtet, sobald der neue Stand da ist.
    BankFlashPending: bool

    // Lena fragen (ledger-xfjcp, ADR 0056): die Farbe im Auswahlfeld ("" = keine) und der
    // geladene Stand mit Vorschlag.
    LenaFlagColorInput: string
    LenaFlag: LenaFlagSettings
    /// Die Lena-Farbe ließ sich nicht laden: ein Zustand der Karte, keine Meldung. Bleibt bis zum nächsten Laden.
    LenaFlagLoadError: string option

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
    | FlashEnded of FlashTarget

/// Nachricht an die Wurzel. Die Einstellungen melden alles am Ort (Regel R3, ADR 0098), deshalb gibt es nur `NoOp`.
type ExternalMsg =
    | NoOp

/// Was der Nutzer auf der Seite tut. Jede dieser Nachrichten nimmt die Rückmeldung weg (R3: bis zur nächsten
/// Aktion). Antworten des Servers, Zeitgeber und das Lesen der Bankverbindung sind keine Aktion. `LoadSettings`
/// zählt: der Knopf „Einstellungen aktualisieren“ und das Öffnen der Seite senden es, so ist die Rückmeldung nach
/// dem Verlassen weg.
let isNextAction (msg: Msg) : bool =
    match msg with
    | LoadSettings
    | UpdateYnabTokenInput _
    | SaveYnabToken
    | TestYnabConnection
    | UpdateComdirectClientIdInput _
    | UpdateComdirectClientSecretInput _
    | UpdateComdirectUsernameInput _
    | UpdateComdirectPasswordInput _
    | UpdateComdirectAccountIdInput _
    | SaveComdirectCredentials
    | UpdateSyncDaysInput _
    | SaveSyncSettings
    | SetAutoFetchEnabled _
    | SetAutoFetchInterval _
    | UpdateLenaFlagColorInput _
    | SaveLenaFlagColor
    | SetDefaultBudget _
    | SetDefaultAccount _
    | SetQuickAddAccount _
    | TestComdirectConnection
    | ConfirmComdirectTan
    | RequestBankDisconnect
    | ConfirmBankDisconnect
    | StartEditing _
    | CancelEditing -> true
    | SettingsLoaded _
    | YnabTokenSaved _
    | YnabConnectionTested _
    | ComdirectCredentialsSaved _
    | SyncSettingsSaved _
    | AutoFetchSaved _
    | LoadLenaFlagSettings
    | LenaFlagSettingsLoaded _
    | LenaFlagColorSaved _
    | DefaultBudgetSet _
    | DefaultAccountSet _
    | QuickAddAccountSet _
    | ComdirectAuthStarted _
    | ComdirectTanConfirmed _
    | LoadBankStatus
    | BankStatusLoaded _
    | AbortBankDisconnect
    | BankDisconnectFinished _
    | FlashEnded _ -> false

/// Die Rückmeldung an diesem Platz.
let feedbackAt (place: FeedbackPlace) (model: Model) : SettingsNote option =
    match model.Feedback with
    | Some (at, note) when at = place -> Some note
    | _ -> None

/// Der rote Text an diesem Platz.
let faultAt (place: FeedbackPlace) (model: Model) : string option =
    match feedbackAt place model with
    | Some (SettingsNote.Failed text) -> Some text
    | _ -> None

/// Der Text in der Erfolgsrolle an diesem Platz.
let doneAt (place: FeedbackPlace) (model: Model) : string option =
    match feedbackAt place model with
    | Some (SettingsNote.Done text) -> Some text
    | _ -> None

/// Die Bestätigung nach dem Speichern, im Kopf der Karte oder unter dem Feld. Das „✓“ setzt `Feedback.saved`.
let savedText = "Gespeichert"

/// Das Ergebnis von „Verbindung testen“ bei YNAB. Das „✓“ setzt `Feedback.saved`.
let budgetsFoundText (count: int) : string =
    if count = 1 then "Verbunden, 1 Budget gefunden" else $"Verbunden, {count} Budgets gefunden"

/// Der Satz vor dem Grund, wenn ein Speichern scheitert (Skizze: „Nicht gespeichert: YNAB antwortet nicht“).
let notSavedText (reason: string) : string = $"Nicht gespeichert: {reason}"

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
    faultAt (Field (AutoFetchField control)) model

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
