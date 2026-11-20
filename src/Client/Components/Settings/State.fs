module Components.Settings.State

open System
open Elmish
open Client
open Components.Settings.Types
open Types
open Shared.Domain

/// Der Vorgang eines Datenbankfehlers als Wort nach „beim“. Unbekannte Vorgänge heißen „Speichern“: die Seite
/// liest und schreibt nur Einstellungen.
let private operationLabel (operation: string) : string =
    match operation with
    | "load" | "read" -> "Lesen"
    | _ -> "Speichern"

/// Ein Fehler der Einstellungen als deutscher Satz, ohne technischen Vorsatz (§8, banking-import-vwhfm).
let settingsErrorToString (error: SettingsError) : string =
    match error with
    | SettingsError.YnabTokenInvalid msg -> $"YNAB-Token ungültig: {msg}"
    // Ohne Status hat YNAB nicht geantwortet oder abgelehnt; der Grund sagt, was davon.
    | SettingsError.YnabConnectionFailed (0, msg) -> $"YNAB-Verbindung gescheitert: {msg}"
    | SettingsError.YnabConnectionFailed (status, msg) -> $"YNAB antwortet nicht (HTTP {status}): {msg}"
    | SettingsError.ComdirectCredentialsInvalid (_, reason) -> $"Comdirect-Zugangsdaten ungültig: {reason}"
    | SettingsError.ComdirectConnectionTestFailed reason -> $"Verbindungstest gescheitert: {reason}"
    | SettingsError.EncryptionFailed msg -> $"Verschlüsselung gescheitert: {msg}"
    // Eine Ablehnung der Validierung ist schon ein Satz für die Seite.
    | SettingsError.DatabaseError ("validation", msg) -> msg
    | SettingsError.DatabaseError (op, msg) -> $"Datenbankfehler beim {operationLabel op}: {msg}"

/// Nach so vielen Millisekunden fällt die Rückfrage "Bank trennen" von selbst zurück.
let bankDisconnectConfirmMs = 3000

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
        AutoFetchEnabledInput = defaultAutoFetchEnabled
        AutoFetchIntervalInput = defaultAutoFetchIntervalMinutes
        AutoFetchSaving = false
        Feedback = None
        Flash = None
        BankFlashPending = false
        LenaFlagColorInput = ""
        LenaFlag = { SavedColor = None; SuggestedColor = None }
        LenaFlagLoadError = None
        ComdirectConnectionValid = NotAsked
        ComdirectAuthPending = false
        BankStatus = NotAsked
        BankStatusReadVersion = 0
        ConfirmingBankDisconnect = false
        BankDisconnect = NotAsked
        EditingSection = None
    }
    model, Cmd.batch [ Cmd.ofMsg LoadSettings; Cmd.ofMsg LoadLenaFlagSettings ]

/// "Bank trennen" schreibt und wird nie wiederholt. `call` ist der Remoting-Aufruf, unter .NET ein
/// Ersatz. Geht die Antwort verloren, steht ein fester Hinweis statt des technischen Fehlers.
let disconnectBankCmd (call: unit -> Async<Result<unit, SyncError>>) : Cmd<Msg> =
    Cmd.OfAsync.either
        call
        ()
        BankDisconnectFinished
        (fun _ -> Error (SyncError.DatabaseError ("disconnect_bank", "Antwort auf das Trennen fehlt. Stand prüfen oder wiederholen.")) |> BankDisconnectFinished)

/// Speichert die Sync-Einstellungen. `call` ist der Remoting-Aufruf, unter .NET bleibt er ungerufen.
let saveAutoFetchCmd (call: SyncSettings -> Async<SettingsResult<unit>>) (settings: SyncSettings) : Cmd<Msg> =
    Cmd.OfAsync.either
        call
        settings
        (fun result -> AutoFetchSaved (settings, result))
        (fun ex -> AutoFetchSaved (settings, Error (SettingsError.DatabaseError ("save", ex.Message))))

/// Die Meldung am Auswahlfeld: eine Ablehnung der Validierung steht ohne technischen Vorsatz da.
let autoFetchErrorText (error: SettingsError) : string =
    settingsErrorToString error

/// So lange leuchtet eine Zeile (passend zu `feedbackFlash` in styles.css, wie im Tab Regeln).
let flashDurationMs = 700

/// Die Zeile leuchtet; nach `flashDurationMs` nimmt `FlashEnded` die Klasse ab (auch ohne Bewegung nötig).
let private startFlash (target: FlashTarget) (model: Model) : Model * Cmd<Msg> =
    { model with Flash = Some target }, Debounce.delayed flashDurationMs (FlashEnded target)

/// Ein Formular ist gespeichert: der Kopf zeigt „✓ Gespeichert“, die geänderte Zeile leuchtet.
let private savedForm (section: SettingsSection) (model: Model) : Model * Cmd<Msg> =
    startFlash (SectionRow section) { model with Feedback = Some (CardHead section, SettingsNote.Done savedText) }

/// Ein Formular ist nicht gespeichert: der Fehler steht am Feld, Abschnitt und Eingaben bleiben.
let private formFault (section: SettingsSection) (error: SettingsError) (model: Model) : Model =
    { model with Feedback = Some (FormField section, SettingsNote.Failed (notSavedText (settingsErrorToString error))) }

/// Ein Feld, das selbst speichert: Bestätigung oder Fehler darunter. Das Feld zeigt den gespeicherten Stand.
let private fieldNote (field: SettingsField) (result: Result<unit, YnabError>) (model: Model) : Model =
    let note =
        match result with
        | Ok () -> SettingsNote.Done savedText
        | Error err -> SettingsNote.Failed (notSavedText (ynabErrorText err))
    { model with Feedback = Some (Field field, note) }

/// Ein Fehler im Block Bankverbindung.
let private bankFault (error: SettingsError) (model: Model) : Model =
    { model with Feedback = Some (BankBlock, SettingsNote.Failed (settingsErrorToString error)) }

/// Speichert die Karte Sync-Einstellungen. `call` ist der Remoting-Aufruf, unter .NET bleibt er ungerufen.
let saveSyncDaysCmd (call: SyncSettings -> Async<SettingsResult<unit>>) (settings: SyncSettings) : Cmd<Msg> =
    Cmd.OfAsync.either
        call
        settings
        SyncSettingsSaved
        (fun ex -> Error (SettingsError.DatabaseError ("save", ex.Message)) |> SyncSettingsSaved)

/// Welches Element ein abgelehntes Speichern ausgelöst hat: gespeichert wird je Mal nur eine Änderung, ein
/// anderer Schalter als gespeichert heißt, der Schalter war es.
let private autoFetchTrigger (model: Model) (sent: SyncSettings) : AutoFetchControl =
    match model.Settings with
    | Success settings when settings.Sync.AutoFetchEnabled <> sent.AutoFetchEnabled -> AutoFetchSwitch
    | _ -> AutoFetchIntervalField

let private startAutoFetchSave (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let saving = { model with AutoFetchSaving = true }
    // Hinter einem Lambda: der Remoting-Proxy wirft unter .NET im Typinitialisierer.
    saving, saveAutoFetchCmd (fun settings -> Api.settings.saveSyncSettings settings) (autoFetchSettingsToSave saving), NoOp

let private step (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | LoadSettings ->
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.settings.getSettings)
                ()
                (Ok >> SettingsLoaded)
                (fun ex -> Error ex.Message |> SettingsLoaded)
        { model with Settings = Loading }, Cmd.batch [ cmd; Cmd.ofMsg LoadBankStatus ], NoOp

    | SettingsLoaded (Ok settings) ->
        let updatedModel = {
            model with
                Settings = Success settings
                ComdirectAccountIdInput = settings.Comdirect |> Option.bind (fun c -> c.AccountId) |> Option.defaultValue ""
                SyncDaysInput = settings.Sync.DaysToFetch
                AutoFetchEnabledInput = settings.Sync.AutoFetchEnabled
                AutoFetchIntervalInput = settings.Sync.AutoFetchIntervalMinutes
        }
        updatedModel, Cmd.none, NoOp

    | SettingsLoaded (Error err) ->
        // Kein Toast: die Seite zeigt „Einstellungen konnten nicht geladen werden“ mit „Erneut versuchen“.
        { model with Settings = Failure err }, Cmd.none, NoOp

    | UpdateYnabTokenInput value ->
        { model with YnabTokenInput = value }, Cmd.none, NoOp

    // Ohne Eingabe bleibt der gesetzte Token, ein Aufruf hätte nichts zu speichern.
    | SaveYnabToken when String.IsNullOrWhiteSpace model.YnabTokenInput && ynabTokenIsSet model ->
        { model with EditingSection = None }, Cmd.none, NoOp

    | SaveYnabToken ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.saveYnabToken
                model.YnabTokenInput
                YnabTokenSaved
                (fun ex -> Error (SettingsError.DatabaseError ("save", ex.Message)) |> YnabTokenSaved)
        model, cmd, NoOp

    | YnabTokenSaved (Ok _) ->
        // Der lokale Stand merkt sich nur, dass ein Token gesetzt ist; die Eingabe wird geleert.
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                let newYnab: YnabSettingsView = {
                    HasToken = true
                    DefaultBudgetId = settings.Ynab |> Option.bind (fun y -> y.DefaultBudgetId)
                    DefaultAccountId = settings.Ynab |> Option.bind (fun y -> y.DefaultAccountId)
                    QuickAddAccountId = settings.Ynab |> Option.bind (fun y -> y.QuickAddAccountId)
                }
                Success { settings with Ynab = Some newYnab }
            | other -> other
        let saved, cmd = savedForm YnabSection { model with Settings = updatedSettings; YnabTokenInput = ""; EditingSection = None }
        saved, cmd, NoOp

    | YnabTokenSaved (Error err) ->
        formFault YnabSection err model, Cmd.none, NoOp

    | TestYnabConnection ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.testYnabConnection
                ()
                YnabConnectionTested
                (fun ex -> Error (SettingsError.YnabConnectionFailed (0, ex.Message)) |> YnabConnectionTested)
        { model with YnabBudgets = Loading }, cmd, NoOp

    | YnabConnectionTested (Ok budgets) ->
        { model with
            YnabBudgets = Success budgets
            Feedback = Some (YnabTestResult, SettingsNote.Done (budgetsFoundText budgets.Length)) },
        Cmd.none,
        NoOp

    | YnabConnectionTested (Error err) ->
        { model with
            YnabBudgets = Failure (settingsErrorToString err)
            Feedback = Some (YnabTestResult, SettingsNote.Failed (settingsErrorToString err)) },
        Cmd.none,
        NoOp

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
        let cmd =
            Cmd.OfAsync.either
                Api.settings.saveComdirectCredentials
                (comdirectCredentialsFromForm model)
                ComdirectCredentialsSaved
                (fun ex -> Error (SettingsError.DatabaseError ("save", ex.Message)) |> ComdirectCredentialsSaved)
        model, cmd, NoOp

    | ComdirectCredentialsSaved (Ok _) ->
        // Der lokale Stand merkt sich nur, dass die Zugangsdaten gesetzt sind; die Eingaben werden geleert.
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                let newComdirect: ComdirectSettingsView = {
                    HasClientId = true
                    HasClientSecret = true
                    HasUsername = true
                    HasPassword = true
                    AccountId = (comdirectCredentialsFromForm model).AccountId
                }
                Success { settings with Comdirect = Some newComdirect }
            | other -> other
        let saved, cmd =
            savedForm
                ComdirectSection
                { model with
                    Settings = updatedSettings
                    ComdirectClientIdInput = ""
                    ComdirectClientSecretInput = ""
                    ComdirectUsernameInput = ""
                    ComdirectPasswordInput = ""
                    EditingSection = None }
        saved, cmd, NoOp

    | ComdirectCredentialsSaved (Error err) ->
        formFault ComdirectSection err model, Cmd.none, NoOp

    | UpdateSyncDaysInput value ->
        { model with SyncDaysInput = value }, Cmd.none, NoOp

    // Läuft das Speichern von „Automatisch abrufen“, überschriebe dieses Speichern es mit dem alten Stand.
    | SaveSyncSettings when syncSaveLocked model ->
        model, Cmd.none, NoOp

    | SaveSyncSettings ->
        // Hinter einem Lambda: der Remoting-Proxy wirft unter .NET im Typinitialisierer.
        model, saveSyncDaysCmd (fun settings -> Api.settings.saveSyncSettings settings) (syncDaysSettingsToSave model), NoOp

    | SyncSettingsSaved (Ok _) ->
        // Update local state with the saved sync settings
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                Success { settings with Sync = { settings.Sync with DaysToFetch = model.SyncDaysInput } }
            | other -> other
        let saved, cmd = savedForm SyncSection { model with Settings = updatedSettings; EditingSection = None }
        saved, cmd, NoOp

    | SyncSettingsSaved (Error err) ->
        formFault SyncSection err model, Cmd.none, NoOp

    | SetAutoFetchEnabled _
    | SetAutoFetchInterval _ when model.AutoFetchSaving -> model, Cmd.none, NoOp

    | SetAutoFetchEnabled enabled ->
        startAutoFetchSave { model with AutoFetchEnabledInput = enabled }

    | SetAutoFetchInterval minutes ->
        startAutoFetchSave { model with AutoFetchIntervalInput = minutes }

    | AutoFetchSaved (saved, Ok ()) ->
        let trigger = autoFetchTrigger model saved
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                Success
                    { settings with
                        Sync =
                            { settings.Sync with
                                AutoFetchEnabled = saved.AutoFetchEnabled
                                AutoFetchIntervalMinutes = saved.AutoFetchIntervalMinutes } }
            | other -> other
        { model with
            Settings = updatedSettings
            AutoFetchSaving = false
            Feedback = Some (Field (AutoFetchField trigger), SettingsNote.Done savedText) },
        Cmd.none,
        NoOp

    // Abgelehnt: Schalter und Feld zeigen wieder, was gespeichert ist und gilt, die Meldung steht am Element,
    // das gespeichert hat (Review-Fund zu banking-import-t4raw: der Schalter blieb auf Aus, BB rief weiter ab).
    | AutoFetchSaved (sent, Error err) ->
        let enabled, interval =
            match model.Settings with
            | Success settings -> settings.Sync.AutoFetchEnabled, settings.Sync.AutoFetchIntervalMinutes
            | _ -> model.AutoFetchEnabledInput, model.AutoFetchIntervalInput
        { model with
            AutoFetchEnabledInput = enabled
            AutoFetchIntervalInput = interval
            AutoFetchSaving = false
            Feedback = Some (Field (AutoFetchField (autoFetchTrigger model sent)), SettingsNote.Failed (autoFetchErrorText err)) },
        Cmd.none,
        NoOp

    | LoadLenaFlagSettings ->
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.settings.getLenaFlagSettings)
                ()
                (Ok >> LenaFlagSettingsLoaded)
                (fun ex -> Error ex.Message |> LenaFlagSettingsLoaded)
        { model with LenaFlagLoadError = None }, cmd, NoOp

    | LenaFlagSettingsLoaded (Ok settings) ->
        { model with
            LenaFlagLoadError = None
            LenaFlag = settings
            LenaFlagColorInput = lenaFlagColorPreselection settings |> Option.defaultValue "" },
        Cmd.none,
        NoOp

    | LenaFlagSettingsLoaded (Error err) ->
        // Ein Zustand der Karte (Kann-Sammlung ledger Punkt 152): rot in der Karte mit „Erneut versuchen“.
        { model with LenaFlagLoadError = Some $"Lena-Farbe nicht geladen: {err}" }, Cmd.none, NoOp

    | UpdateLenaFlagColorInput value ->
        { model with LenaFlagColorInput = value }, Cmd.none, NoOp

    | SaveLenaFlagColor ->
        let color = if String.IsNullOrWhiteSpace model.LenaFlagColorInput then None else Some model.LenaFlagColorInput
        let cmd =
            Cmd.OfAsync.either
                Api.settings.saveLenaFlagColor
                color
                (fun result -> LenaFlagColorSaved (color, result))
                (fun ex -> LenaFlagColorSaved (color, Error (SettingsError.DatabaseError ("save", ex.Message))))
        model, cmd, NoOp

    | LenaFlagColorSaved (color, Ok _) ->
        let saved, cmd =
            savedForm
                LenaSection
                { model with
                    LenaFlag = { model.LenaFlag with SavedColor = color }
                    EditingSection = None }
        saved, cmd, NoOp

    | LenaFlagColorSaved (_, Error err) ->
        formFault LenaSection err model, Cmd.none, NoOp

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
        fieldNote DefaultBudgetField (Ok ()) { model with Settings = updatedSettings }, Cmd.none, NoOp

    | DefaultBudgetSet (_, Error err) ->
        fieldNote DefaultBudgetField (Error err) model, Cmd.none, NoOp

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
        fieldNote DefaultAccountField (Ok ()) { model with Settings = updatedSettings }, Cmd.none, NoOp

    | DefaultAccountSet (_, Error err) ->
        fieldNote DefaultAccountField (Error err) model, Cmd.none, NoOp

    | SetQuickAddAccount accountId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ynab.setQuickAddAccount
                accountId
                (fun result -> QuickAddAccountSet (accountId, result))
                (fun ex -> QuickAddAccountSet (accountId, Error (YnabError.NetworkError ex.Message)))
        model, cmd, NoOp

    | QuickAddAccountSet (accountId, Ok _) ->
        let updatedSettings =
            match model.Settings with
            | Success settings ->
                match settings.Ynab with
                | Some ynab ->
                    Success { settings with Ynab = Some { ynab with QuickAddAccountId = Some accountId } }
                | None -> Success settings
            | other -> other
        fieldNote QuickAddAccountField (Ok ()) { model with Settings = updatedSettings }, Cmd.none, NoOp

    | QuickAddAccountSet (_, Error err) ->
        fieldNote QuickAddAccountField (Error err) model, Cmd.none, NoOp

    // Comdirect connection test
    | TestComdirectConnection ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.testComdirectConnection
                ()
                ComdirectAuthStarted
                (fun ex -> Error (SettingsError.ComdirectConnectionTestFailed ex.Message) |> ComdirectAuthStarted)
        { model with ComdirectConnectionValid = Loading }, cmd, NoOp

    | ComdirectAuthStarted (Ok BankConnectionActive) ->
        // Die Verbindung lebt, es gibt nichts zu bestätigen.
        // Die Plakette zeigt „Verbunden“, die Zeile leuchtet, sobald der Stand gelesen ist; kein Text.
        { model with ComdirectConnectionValid = Success (); ComdirectAuthPending = false; BankDisconnect = NotAsked; BankFlashPending = true },
        Cmd.ofMsg LoadBankStatus,
        NoOp

    | ComdirectAuthStarted (Ok (TanChallengeStarted _)) ->
        // Der gelbe Hinweis mit „TAN bestätigt“ steht im Abschnitt, mehr braucht es nicht.
        { model with ComdirectAuthPending = true; ComdirectConnectionValid = NotAsked }, Cmd.ofMsg LoadBankStatus, NoOp

    | ComdirectAuthStarted (Error err) ->
        bankFault err { model with ComdirectConnectionValid = Failure (settingsErrorToString err); ComdirectAuthPending = false },
        Cmd.ofMsg LoadBankStatus,
        NoOp

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
            BankDisconnect = NotAsked
            BankFlashPending = true
        }, Cmd.ofMsg LoadBankStatus, NoOp

    | ComdirectTanConfirmed (Error err) ->
        bankFault
            err
            { model with
                ComdirectConnectionValid = Failure (settingsErrorToString err)
                ComdirectAuthPending = false },
        Cmd.ofMsg LoadBankStatus,
        NoOp

    | LoadBankStatus ->
        let reading = beginBankStatusRead model
        let version = reading.BankStatusReadVersion
        reading, Components.BankConnectionBadge.load (fun answer -> BankStatusLoaded (version, answer)), NoOp

    | BankStatusLoaded (version, _) when version <> model.BankStatusReadVersion ->
        model, Cmd.none, NoOp

    // Nach Verbindungstest, TAN oder Trennen leuchtet die Zeile mit der neuen Plakette.
    | BankStatusLoaded (_, Ok status) when model.BankFlashPending ->
        let flashed, cmd = startFlash BankRow { model with BankStatus = Success status; BankFlashPending = false }
        flashed, cmd, NoOp

    | BankStatusLoaded (_, Ok status) ->
        { model with BankStatus = Success status }, Cmd.none, NoOp

    // Kein Toast: die Seite zeigt den unbekannten Stand selbst.
    | BankStatusLoaded (_, Error reason) ->
        { model with BankStatus = Failure reason; BankFlashPending = false }, Cmd.none, NoOp

    // Die Rückfrage fällt nach drei Sekunden zurück, wie das Löschen einer Regel.
    | RequestBankDisconnect ->
        { model with ConfirmingBankDisconnect = true }, Debounce.delayed bankDisconnectConfirmMs AbortBankDisconnect, NoOp

    | AbortBankDisconnect ->
        { model with ConfirmingBankDisconnect = false }, Cmd.none, NoOp

    | ConfirmBankDisconnect when RemoteData.isLoading model.BankDisconnect ->
        model, Cmd.none, NoOp

    | ConfirmBankDisconnect ->
        { model with ConfirmingBankDisconnect = false; BankDisconnect = Loading },
        disconnectBankCmd (fun () -> Api.sync.disconnectBank ()),
        NoOp

    | BankDisconnectFinished (Ok ()) ->
        { model with
            BankDisconnect = NotAsked
            ComdirectAuthPending = false
            ComdirectConnectionValid = NotAsked
            BankFlashPending = true
        }, Cmd.ofMsg LoadBankStatus, NoOp

    // Der Grund steht im Block Bankverbindung (`BankDisconnect`), kein Toast.
    | BankDisconnectFinished (Error error) ->
        let reason = bankDisconnectErrorText error
        { model with BankDisconnect = Failure reason },
        Cmd.ofMsg LoadBankStatus,
        NoOp

    // Edit mode toggling
    | StartEditing section ->
        { model with EditingSection = Some section; ConfirmingBankDisconnect = false }, Cmd.none, NoOp

    | CancelEditing ->
        // Ein verworfener Entwurf der Lena-Farbe fällt auf den geladenen Stand zurück. Verworfene
        // Zugangsdaten verschwinden ganz: blieben sie stehen, gingen sie beim nächsten Speichern mit.
        { model with
            EditingSection = None
            YnabTokenInput = ""
            ComdirectClientIdInput = ""
            ComdirectClientSecretInput = ""
            ComdirectUsernameInput = ""
            ComdirectPasswordInput = ""
            LenaFlagColorInput = lenaFlagColorPreselection model.LenaFlag |> Option.defaultValue "" },
        Cmd.none,
        NoOp

    | FlashEnded target when model.Flash = Some target ->
        { model with Flash = None }, Cmd.none, NoOp

    | FlashEnded _ ->
        model, Cmd.none, NoOp

/// Jede Aktion des Nutzers nimmt die Rückmeldung weg (R3), dann gilt die Nachricht.
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let model = if isNextAction msg then { model with Feedback = None } else model
    step msg model
