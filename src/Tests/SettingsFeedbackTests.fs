module Tests.SettingsFeedbackTests

// banking-import-vwhfm (Regel R3, ADR 0098): die Seite Einstellungen meldet jedes Ergebnis am Ort, auf allen
// Breiten, ohne Toast. Je Zeile der Tabelle aus der Task der neue Platz und Text, dazu die „nächste Aktion“, die
// die Rückmeldung wegnimmt, und das Leuchten.

open System
open Expecto
open Shared.Domain
open Types
open Components.Settings.Types

module SettingsState = Components.Settings.State

let private loadedView : AppSettingsView =
    { Comdirect = Some { HasClientId = true; HasClientSecret = true; HasUsername = true; HasPassword = true; AccountId = Some "konto" }
      Ynab = Some { HasToken = true; DefaultBudgetId = Some (YnabBudgetId "budget-alt"); DefaultAccountId = None; QuickAddAccountId = None }
      Sync = { DaysToFetch = 14; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 120 } }

let private loaded () : Model =
    let model, _ = SettingsState.init ()
    let next, _, _ = SettingsState.update (SettingsLoaded (Ok loadedView)) model
    next

let private run (msg: Msg) (model: Model) =
    SettingsState.update msg model

let private budget (name: string) : YnabBudgetWithAccounts =
    { Budget = { Id = YnabBudgetId name; Name = name }; Accounts = []; Categories = [] }

let private accountId = YnabAccountId (Guid.Parse "00000000-0000-0000-0003-000000000001")

/// Der Ort und der Text der einen Rückmeldung.
let private expectFeedback (model: Model) (place: FeedbackPlace) (note: SettingsNote) =
    Expect.equal model.Feedback (Some (place, note)) $"Rückmeldung an {place}"

[<Tests>]
let settingsFeedbackTests =
    testList "Einstellungen: Rückmeldung am Ort statt Toast" [

        test "Einstellungen nicht geladen: kein Toast, die Seite zeigt die Karte mit dem Grund" {
            let model, _ = SettingsState.init ()
            let failed, _, external = run (SettingsLoaded (Error "weg")) model
            Expect.equal failed.Settings (Failure "weg") "die Karte auf der Seite liest den Grund"
            Expect.isNone failed.Feedback "keine Meldung"
            Expect.equal external NoOp "kein Toast"
        }

        // Speichern über ein Formular: der Abschnitt schließt, der Kopf zeigt „✓ Gespeichert“, die Zeile leuchtet.
        for label, section, answer in
            [ "Token", YnabSection, YnabTokenSaved (Ok ())
              "Zugangsdaten", ComdirectSection, ComdirectCredentialsSaved (Ok ())
              "Sync-Zeitraum", SyncSection, SyncSettingsSaved (Ok ())
              "Lena-Farbe", LenaSection, LenaFlagColorSaved (Some "purple", Ok ()) ] do
            test $"{label} gespeichert: der Abschnitt schließt, im Kopf „✓ Gespeichert“, die Zeile leuchtet" {
                let editing = { loaded () with EditingSection = Some section }
                let saved, cmd, external = run answer editing
                Expect.isNone saved.EditingSection "Abschnitt geschlossen"
                expectFeedback saved (CardHead section) (SettingsNote.Done "✓ Gespeichert")
                Expect.equal saved.Flash (Some (SectionRow section)) "die geänderte Zeile leuchtet"
                Expect.equal external NoOp "kein Toast"
                Tests.CmdTiming.expectAfter SettingsState.flashDurationMs (FlashEnded (SectionRow section)) cmd
            }

        // Fehler beim Speichern über ein Formular: rot am Feld, Abschnitt und Eingaben bleiben.
        for label, section, answer, text in
            [ "Token", YnabSection,
              YnabTokenSaved (Error (SettingsError.YnabTokenInvalid "YNAB-Token ist erforderlich")),
              "Nicht gespeichert: YNAB-Token ungültig: YNAB-Token ist erforderlich"
              "Zugangsdaten", ComdirectSection,
              ComdirectCredentialsSaved (Error (SettingsError.ComdirectCredentialsInvalid ("general", "PIN ist erforderlich"))),
              "Nicht gespeichert: Comdirect-Zugangsdaten ungültig: PIN ist erforderlich"
              "Sync-Zeitraum", SyncSection,
              SyncSettingsSaved (Error (SettingsError.DatabaseError ("validation", "Tage muss zwischen 7 und 90 liegen"))),
              "Nicht gespeichert: Tage muss zwischen 7 und 90 liegen"
              "Lena-Farbe", LenaSection,
              LenaFlagColorSaved (Some "pink", Error (SettingsError.DatabaseError ("save_lena_flag_color", "voll"))),
              "Nicht gespeichert: Datenbankfehler beim Speichern: voll" ] do
            test $"{label} nicht gespeichert: rot am Feld, der Abschnitt bleibt offen, die Eingaben bleiben" {
                let editing =
                    { loaded () with
                        EditingSection = Some section
                        YnabTokenInput = "token-entwurf"
                        ComdirectClientIdInput = "id-entwurf"
                        SyncDaysInput = 45
                        LenaFlagColorInput = "pink" }
                let failed, _, external = run answer editing
                expectFeedback failed (FormField section) (SettingsNote.Failed text)
                Expect.equal failed.EditingSection (Some section) "Abschnitt offen"
                Expect.equal
                    (failed.YnabTokenInput, failed.ComdirectClientIdInput, failed.SyncDaysInput, failed.LenaFlagColorInput)
                    ("token-entwurf", "id-entwurf", 45, "pink")
                    "Eingaben bleiben"
                Expect.isNone failed.Flash "nichts leuchtet"
                Expect.equal external NoOp "kein Toast"
            }

        // Felder, die beim Ändern speichern: „✓ Gespeichert“ oder rot darunter, das Feld zeigt den gespeicherten Stand.
        let loadedYnab = loadedView.Ynab.Value
        for label, field, ok, rejected, stored in
            [ "Standard-Budget", DefaultBudgetField,
              DefaultBudgetSet (YnabBudgetId "budget-neu", Ok ()),
              DefaultBudgetSet (YnabBudgetId "budget-neu", Error (YnabError.NetworkError "weg")),
              { loadedYnab with DefaultBudgetId = Some (YnabBudgetId "budget-neu") }
              "Standard-Konto", DefaultAccountField,
              DefaultAccountSet (accountId, Ok ()),
              DefaultAccountSet (accountId, Error (YnabError.NetworkError "weg")),
              { loadedYnab with DefaultAccountId = Some accountId }
              "Quick-Add-Konto", QuickAddAccountField,
              QuickAddAccountSet (accountId, Ok ()),
              QuickAddAccountSet (accountId, Error (YnabError.NetworkError "weg")),
              { loadedYnab with QuickAddAccountId = Some accountId } ] do
            test $"{label}: gespeichert steht darunter „✓ Gespeichert“, abgelehnt rot der Grund und der alte Wert" {
                let saved, _, external = run ok (loaded ())
                expectFeedback saved (Field field) (SettingsNote.Done "✓ Gespeichert")
                Expect.equal external NoOp "kein Toast"
                Expect.equal saved.Settings (Success { loadedView with Ynab = Some stored }) "der neue Wert gilt, die übrigen bleiben"
                let failed, _, external = run rejected (loaded ())
                expectFeedback failed (Field field) (SettingsNote.Failed "Nicht gespeichert: Netzwerkfehler: weg")
                Expect.equal failed.Settings (loaded ()).Settings "das Feld zeigt wieder den gespeicherten Wert"
                Expect.equal external NoOp "kein Toast"
            }

        test "Automatisch abrufen: der Schalter bestätigt unter sich selbst" {
            let off, _, _ = run (SetAutoFetchEnabled false) (loaded ())
            let saved, _, external = run (AutoFetchSaved (autoFetchSettingsToSave off, Ok ())) off
            expectFeedback saved (Field (AutoFetchField AutoFetchSwitch)) (SettingsNote.Done "✓ Gespeichert")
            Expect.equal external NoOp "kein Toast"
        }

        // YNAB „Verbindung testen“: das Ergebnis steht unter den Knöpfen, in Lesen und Bearbeiten.
        for count, text in [ 2, "✓ Verbunden, 2 Budgets gefunden"; 1, "✓ Verbunden, 1 Budget gefunden" ] do
            test $"YNAB-Verbindung mit {count} Budgets: „{text}“ unter „Verbindung testen“" {
                let budgets = [ for i in 1 .. count -> budget $"Budget {i}" ]
                let tested, _, external = run (YnabConnectionTested (Ok budgets)) (loaded ())
                expectFeedback tested YnabTestResult (SettingsNote.Done text)
                Expect.equal tested.YnabBudgets (Success budgets) "die Auswahl im Formular"
                Expect.equal external NoOp "kein Toast"
            }

        test "YNAB-Verbindung gescheitert: rot mit dem Grund unter „Verbindung testen“" {
            let failed, _, external =
                run (YnabConnectionTested (Error (SettingsError.YnabConnectionFailed (0, "YNAB-Token ungültig (HTTP 401)")))) (loaded ())
            expectFeedback failed YnabTestResult (SettingsNote.Failed "YNAB-Verbindung gescheitert: YNAB-Token ungültig (HTTP 401)")
            Expect.equal external NoOp "kein Toast"
        }

        // Comdirect: das Gelingen zeigt die Plakette, die Zeile leuchtet, sobald der neue Stand gelesen ist.
        for label, msg in
            [ "Bankverbindung aktiv", ComdirectAuthStarted (Ok BankConnectionActive)
              "TAN bestätigt", ComdirectTanConfirmed (Ok ())
              "Bank getrennt", BankDisconnectFinished (Ok ()) ] do
            test $"{label}: kein Text, die Zeile Bankverbindung leuchtet mit dem neuen Stand" {
                let answered, _, external = run msg (loaded ())
                Expect.isNone answered.Feedback "kein Text"
                Expect.equal external NoOp "kein Toast"
                Expect.isNone answered.Flash "der Stand ist noch nicht gelesen"
                let reading = beginBankStatusRead answered
                let read, cmd, _ = run (BankStatusLoaded (reading.BankStatusReadVersion, Ok BankConnected)) reading
                Expect.equal read.Flash (Some BankRow) "die Zeile leuchtet"
                Expect.isFalse read.BankFlashPending "nur einmal"
                Tests.CmdTiming.expectAfter SettingsState.flashDurationMs (FlashEnded BankRow) cmd
                let again, _, _ = run (BankStatusLoaded (reading.BankStatusReadVersion, Ok BankConnected)) { read with Flash = None }
                Expect.isNone again.Flash "ein späteres Lesen leuchtet nicht"
            }

        test "Push-TAN gestartet: kein Toast, der gelbe Hinweis im Abschnitt steht über `isTanPending`" {
            let started, _, external = run (ComdirectAuthStarted (Ok (TanChallengeStarted "c"))) (loaded ())
            Expect.isTrue (isTanPending started) "der Hinweis mit „TAN bestätigt“"
            Expect.isNone started.Feedback "kein Text"
            Expect.equal external NoOp "kein Toast"
        }

        for label, msg, text in
            [ "Verbindungstest", ComdirectAuthStarted (Error (SettingsError.ComdirectConnectionTestFailed "Bank antwortet nicht")),
              "Verbindungstest gescheitert: Bank antwortet nicht"
              "TAN", ComdirectTanConfirmed (Error (SettingsError.ComdirectCredentialsInvalid ("tan", "TAN abgelaufen"))),
              "Comdirect-Zugangsdaten ungültig: TAN abgelaufen" ] do
            test $"{label} gescheitert: rot im Block Bankverbindung, auch im Lesen-Modus" {
                let failed, _, external = run msg (loaded ())
                Expect.isNone failed.EditingSection "Lesen-Modus"
                expectFeedback failed BankBlock (SettingsNote.Failed text)
                Expect.equal external NoOp "kein Toast"
            }

        test "Trennen gescheitert: kein Toast, der Grund steht schon im Block" {
            let failed, _, external =
                run (BankDisconnectFinished (Error (SyncError.DatabaseError ("disconnect_bank", "Wert liegt noch")))) (loaded ())
            Expect.equal failed.BankDisconnect (Failure "Wert liegt noch") "im Block"
            Expect.isNone failed.Feedback "keine zweite Meldung"
            Expect.equal external NoOp "kein Toast"
        }

        // Kann-Sammlung ledger Punkt 152: „Failed to load Lena flag colour: …“ stand englisch im Toast.
        test "Lena-Farbe nicht geladen: deutsch in der Karte, bis sie geladen ist" {
            let failed, _, external = run (LenaFlagSettingsLoaded (Error "Http error (0)")) (loaded ())
            Expect.equal failed.LenaFlagLoadError (Some "Lena-Farbe nicht geladen: Http error (0)") "in der Karte"
            Expect.equal external NoOp "kein Toast"
            let stays, _, _ = run (StartEditing YnabSection) failed
            Expect.isSome stays.LenaFlagLoadError "ein Zustand der Karte, keine Meldung"
            let fixedUp, _, _ = run (LenaFlagSettingsLoaded (Ok { SavedColor = Some "blue"; SuggestedColor = None })) failed
            Expect.isNone fixedUp.LenaFlagLoadError "geladen"
        }

        test "ein überholtes Ende des Leuchtens ändert nichts" {
            let flashing = { loaded () with Flash = Some BankRow }
            let kept, _, _ = run (FlashEnded (SectionRow YnabSection)) flashing
            Expect.equal kept.Flash (Some BankRow) "das andere leuchtet weiter"
            let ended, _, _ = run (FlashEnded BankRow) flashing
            Expect.isNone ended.Flash "abgenommen"
        }
    ]

// R3: die Rückmeldung bleibt bis zur nächsten Aktion. Über das echte `update`, je Nachricht, mit der Liste
// der Aktionen als Orakel. Nachrichten, deren Cmd `Client.Api` direkt berührt, prüft `isNextAction` allein.
[<Tests>]
let settingsNextActionTests =
    let standing = { loaded () with Feedback = Some (CardHead YnabSection, SettingsNote.Done "✓ Gespeichert") }
    testList "Einstellungen: die nächste Aktion nimmt die Rückmeldung weg" [
        for label, msg in
            [ "Tippen im Token", UpdateYnabTokenInput "a"
              "Tippen in der Client-ID", UpdateComdirectClientIdInput "a"
              "Schieben der Tage", UpdateSyncDaysInput 20
              "Wahl der Lena-Farbe", UpdateLenaFlagColorInput "red"
              "Bearbeiten", StartEditing SyncSection
              "Abbrechen", CancelEditing
              "Schalter", SetAutoFetchEnabled false
              "Intervall", SetAutoFetchInterval 240
              "Speichern der Tage", SaveSyncSettings
              "Bank trennen", RequestBankDisconnect ] do
            test $"{label} ist eine Aktion" {
                Expect.isTrue (isNextAction msg) "Aktion"
                let next, _, _ = run msg standing
                Expect.isNone next.Feedback "weg"
            }

        for label, msg in
            [ "Ende des Leuchtens", FlashEnded BankRow
              "Rückfall der Rückfrage", AbortBankDisconnect
              "Stand der Bank gelesen", BankStatusLoaded (standing.BankStatusReadVersion, Ok BankConnected)
              "Lena-Farbe geladen", LenaFlagSettingsLoaded (Ok { SavedColor = None; SuggestedColor = None }) ] do
            test $"{label} ist keine Aktion" {
                Expect.isFalse (isNextAction msg) "keine Aktion"
                let next, _, _ = run msg standing
                Expect.equal next.Feedback standing.Feedback "bleibt"
            }

        // Diese Nachrichten rufen `Client.Api` direkt; unter .NET geht nur die reine Funktion.
        test "Aktualisieren, Speichern, Testen und Auswählen sind Aktionen, Laden im Hintergrund nicht" {
            for msg in
                [ LoadSettings; SaveYnabToken; TestYnabConnection; SaveComdirectCredentials; SaveLenaFlagColor
                  SetDefaultBudget (YnabBudgetId "b"); SetDefaultAccount accountId; SetQuickAddAccount accountId
                  TestComdirectConnection; ConfirmComdirectTan; ConfirmBankDisconnect ] do
                Expect.isTrue (isNextAction msg) $"%A{msg}"
            for msg in [ LoadBankStatus; LoadLenaFlagSettings ] do
                Expect.isFalse (isNextAction msg) $"%A{msg}"
        }
    ]

// §8: kein englisches Wort mehr in einem Fehler, den die Seite zeigt.
[<Tests>]
let settingsErrorTextTests =
    testList "Einstellungen: Fehlertexte deutsch" [
        test "jeder Fall von SettingsError ist ein deutscher Satz ohne technischen Vorsatz" {
            let cases =
                [ SettingsError.YnabTokenInvalid "x", "YNAB-Token ungültig: x"
                  SettingsError.YnabConnectionFailed (0, "x"), "YNAB-Verbindung gescheitert: x"
                  SettingsError.YnabConnectionFailed (503, "x"), "YNAB antwortet nicht (HTTP 503): x"
                  SettingsError.ComdirectCredentialsInvalid ("general", "x"), "Comdirect-Zugangsdaten ungültig: x"
                  SettingsError.ComdirectConnectionTestFailed "x", "Verbindungstest gescheitert: x"
                  SettingsError.EncryptionFailed "x", "Verschlüsselung gescheitert: x"
                  SettingsError.DatabaseError ("validation", "x"), "x"
                  SettingsError.DatabaseError ("save_sync_settings", "x"), "Datenbankfehler beim Speichern: x"
                  SettingsError.NotConfigured "YNAB-Token", "YNAB-Token nicht gespeichert" ]
            for error, text in cases do
                Expect.equal (SettingsState.settingsErrorToString error) text $"%A{error}"
        }
    ]
