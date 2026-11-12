module Tests.ClientCommandTests

// infrastructure-t5m2b: was bisher nur ein Browser-Wächter belegte, weil der Fable.Remoting-Proxy
// unter .NET nicht läuft. Die Cmds nehmen den Aufruf jetzt als Funktion, hier steht ein Ersatz.
// Unter .NET startet Elmish `Cmd.OfAsync` per `Async.Start`: die erste Nachricht kommt auf einem
// anderen Thread an, deshalb mit Frist.

open System.Collections.Concurrent
open Expecto
open Shared.Domain
open Types

/// Führt die Effekte eines Cmd aus und gibt die erste abgeschickte Nachricht zurück, höchstens nach `ms`.
let private firstDispatchedWithin (ms: int) (cmd: Elmish.Cmd<'msg>) : 'msg =
    let collected = new BlockingCollection<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    let mutable msg = Unchecked.defaultof<'msg>
    if collected.TryTake(&msg, ms) then msg else failtest $"keine Nachricht innerhalb von {ms} ms"

/// Wie `firstDispatchedWithin`, höchstens nach 5 s.
let private firstDispatched (cmd: Elmish.Cmd<'msg>) : 'msg = firstDispatchedWithin 5000 cmd

/// Ein Aufruf, dessen Antwort ausbleibt (im Browser Status 0).
let private lost (_: 'a) : Async<'b> = async { return failwith "Http error (0)" }

let private ruleForm : Components.Rules.Types.RuleFormState =
    { Components.Rules.Types.RuleFormState.empty with
        Pattern = "rewe"
        PatternType = Exact
        TargetField = Payee
        TestInput = "REWE Markt" }

[<Tests>]
let tests =
    testList "Ausgehende Aufrufe im Client" [
        testList "Mustertest im Regel-Dialog" [
            test "der Aufruf bekommt Muster, Typ, Feld und Beispieltext, die Antwort kommt als Urteil an" {
                let seen = ref None
                let call args = async { seen.Value <- Some args; return Ok false }
                let msg = firstDispatched (Components.Rules.State.testPatternCmd call ruleForm)
                Expect.equal seen.Value (Some ("rewe", Exact, Payee, "REWE Markt")) "Argumente"
                Expect.equal msg (Components.Rules.Types.RulePatternTested (Ok false)) "Urteil"
            }
            // Bug categorization-f955d: ein Netzfehler erschien als ungültiges Muster.
            test "bleibt die Antwort aus, ist das ein Netzfehler und kein Urteil über das Muster" {
                let msg = firstDispatched (Components.Rules.State.testPatternCmd lost ruleForm)
                Expect.equal msg Components.Rules.Types.RulePatternTestLost "Netzfehler"
            }
        ]
        testList "Bank trennen" [
            test "geht die Antwort verloren, steht ein fester Hinweis statt des technischen Fehlers" {
                let msg = firstDispatched (Components.Settings.State.disconnectBankCmd lost)
                let model, _ = Components.Settings.State.init ()
                let settled, _, _ = Components.Settings.State.update msg { model with BankDisconnect = Loading }
                Expect.equal settled.BankDisconnect (Failure "Antwort auf das Trennen fehlt. Stand prüfen oder wiederholen.") "Hinweis"
            }
            test "ein gelungenes Trennen kommt als Erfolg an" {
                let msg = firstDispatched (Components.Settings.State.disconnectBankCmd (fun () -> async { return Ok () }))
                Expect.equal msg (Components.Settings.Types.BankDisconnectFinished (Ok ())) "Erfolg"
            }
            test "während des Trennens sind Bearbeiten und Verbindung testen gesperrt, sonst nicht" {
                let model, _ = Components.Settings.State.init ()
                Expect.isTrue (Components.Settings.Types.comdirectActionsLocked { model with BankDisconnect = Loading }) "gesperrt"
                for idle in [ NotAsked; Failure "x"; Success () ] do
                    Expect.isFalse (Components.Settings.Types.comdirectActionsLocked { model with BankDisconnect = idle }) $"%A{idle}"
            }
            test "die Rückfrage stellt scharf, ohne etwas zu senden" {
                let model, _ = Components.Settings.State.init ()
                let armed, _, _ = Components.Settings.State.update Components.Settings.Types.RequestBankDisconnect model
                Expect.isTrue armed.ConfirmingBankDisconnect "scharf"
                Expect.equal armed.BankDisconnect NotAsked "noch kein Aufruf"
            }
            // Vorher nur im Browser belegt (settings-secrets), dort mit infrastructure-t5m2b gelöscht.
            test "die Rückfrage fällt nach der Wartezeit von selbst zurück" {
                let model, _ = Components.Settings.State.init ()
                let armed, cmd, _ = Components.Settings.State.update Components.Settings.Types.RequestBankDisconnect model
                let clock = System.Diagnostics.Stopwatch.StartNew()
                let msg = firstDispatchedWithin (Components.Settings.State.bankDisconnectConfirmMs + 3000) cmd
                Expect.equal msg Components.Settings.Types.AbortBankDisconnect "der Rückfall"
                Expect.isGreaterThanOrEqual clock.ElapsedMilliseconds (int64 Components.Settings.State.bankDisconnectConfirmMs - 100L) "erst nach der Wartezeit"
                let back, _, _ = Components.Settings.State.update msg armed
                Expect.isFalse back.ConfirmingBankDisconnect "wieder zu"
            }
        ]
        testList "Verbindung testen im Formular" [
            let model, _ = Components.Settings.State.init ()
            let configured =
                { model with
                    Settings =
                        Success
                            { Comdirect = Some { HasClientId = true; HasClientSecret = true; HasUsername = true; HasPassword = true; AccountId = None }
                              Ynab = None
                              Sync = { DaysToFetch = 14; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 360 } } }
            test "mit Zugangsdaten und ohne offene TAN steht der Abschnitt" {
                Expect.isTrue (Components.Settings.Types.connectionTestVisible { configured with BankStatus = Success BankConnected }) "sichtbar"
            }
            // Der TAN-Block stand bei offener TAN zweimal da, einmal davon in der Bearbeitung.
            test "bei offener TAN fehlt er, der TAN-Block steht schon über dem Formular" {
                Expect.isFalse (Components.Settings.Types.connectionTestVisible { configured with BankStatus = Success BankChallengePending }) "laut Server"
                Expect.isFalse (Components.Settings.Types.connectionTestVisible { configured with ComdirectAuthPending = true }) "soeben gestartet"
            }
            test "ohne Zugangsdaten fehlt er" {
                Expect.isFalse (Components.Settings.Types.connectionTestVisible { model with BankStatus = Success BankConnected }) "nichts zu testen"
            }
        ]
        testList "Hinweis zur Bankverbindung" [
            test "der Stand nennt den nächsten Sync, ein Lesefehler sagt nicht lesbar, beim Laden steht nichts" {
                Expect.equal (Components.BankConnectionBadge.hintFor (Success BankDisconnected)) (Some "Nächster Sync braucht eine TAN") "getrennt"
                Expect.equal (Components.BankConnectionBadge.hintFor (Success BankConnected)) (Some "Nächster Sync ohne TAN") "verbunden"
                Expect.equal (Components.BankConnectionBadge.hintFor (Failure "500")) (Some "Stand der Bankverbindung nicht lesbar") "nicht lesbar"
                Expect.equal (Components.BankConnectionBadge.hintFor Loading) None "lädt"
                Expect.equal (Components.BankConnectionBadge.hintFor NotAsked) None "noch nicht gefragt"
            }
        ]
    ]

// banking-import-t4raw (ADR 0093 Punkt 9): Schalter und Intervall „Automatisch abrufen“ in der Karte Comdirect.
[<Tests>]
let autoFetchSettingsClientTests =
    let update msg model =
        let next, _, external = Components.Settings.State.update msg model
        next, external
    let loaded =
        let model, _ = Components.Settings.State.init ()
        let view : AppSettingsView =
            { Comdirect = Some { HasClientId = true; HasClientSecret = true; HasUsername = true; HasPassword = true; AccountId = None }
              Ynab = None
              Sync = { DaysToFetch = 14; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 120 } }
        update (Components.Settings.Types.SettingsLoaded (Ok view)) model |> fst
    testList "Einstellungen: automatisch abrufen" [
        test "geladen stehen Schalter und Intervall wie gespeichert, das Feld ist offen" {
            Expect.isTrue loaded.AutoFetchEnabledInput "an"
            Expect.equal loaded.AutoFetchIntervalInput 120 "120 Minuten"
            Expect.isFalse (Components.Settings.Types.autoFetchIntervalLocked loaded) "offen"
        }

        test "Schalter aus sperrt das Feld, behält seinen Wert und speichert beide Felder mit den Tagen" {
            let off, _ = update (Components.Settings.Types.SetAutoFetchEnabled false) loaded
            Expect.isFalse off.AutoFetchEnabledInput "aus"
            Expect.equal off.AutoFetchIntervalInput 120 "Wert bleibt"
            Expect.isTrue off.AutoFetchSaving "speichert"
            let saved, _ = update (Components.Settings.Types.AutoFetchSaved (Components.Settings.Types.autoFetchSettingsToSave off, Ok ())) off
            Expect.isTrue (Components.Settings.Types.autoFetchIntervalLocked saved) "gesperrt, auch nach dem Speichern"
            Expect.equal
                (Components.Settings.Types.autoFetchSettingsToSave off)
                { DaysToFetch = 14; AutoFetchEnabled = false; AutoFetchIntervalMinutes = 120 }
                "beide Felder, die Tage wie gespeichert"
        }

        test "ein anderes Intervall speichert es, nach der Antwort gilt es als gespeichert" {
            let changed, _ = update (Components.Settings.Types.SetAutoFetchInterval 480) loaded
            Expect.equal (Components.Settings.Types.autoFetchSettingsToSave changed).AutoFetchIntervalMinutes 480 "das neue Intervall"
            let saved, external = update (Components.Settings.Types.AutoFetchSaved (Components.Settings.Types.autoFetchSettingsToSave changed, Ok ())) changed
            Expect.isFalse saved.AutoFetchSaving "fertig"
            match saved.Settings with
            | Success settings -> Expect.equal settings.Sync { DaysToFetch = 14; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 480 } "übernommen"
            | other -> failtestf "geladen erwartet: %A" other
            match external with
            | Components.Settings.Types.ShowToast (_, ToastSuccess) -> ()
            | other -> failtestf "Erfolgsmeldung erwartet: %A" other
        }

        test "während des Speicherns ändern Schalter und Feld nichts" {
            let saving, _ = update (Components.Settings.Types.SetAutoFetchInterval 480) loaded
            let again, _ = update (Components.Settings.Types.SetAutoFetchEnabled false) saving
            Expect.isTrue again.AutoFetchEnabledInput "Schalter unverändert"
            Expect.equal again.AutoFetchIntervalInput 480 "Wert unverändert"
        }

        test "eine Ablehnung des Servers steht als Feldfehler am Auswahlfeld, nicht als Toast" {
            let changed, _ = update (Components.Settings.Types.SetAutoFetchInterval 480) loaded
            let toSave = Components.Settings.Types.autoFetchSettingsToSave changed
            let sent = ConcurrentQueue<SyncSettings>()
            let rejecting (settings: SyncSettings) = async {
                sent.Enqueue settings
                return Error (SettingsError.DatabaseError ("validation", "Intervall muss zwischen 60 und 1440 Minuten liegen")) }
            let answer = Components.Settings.State.saveAutoFetchCmd rejecting toSave |> firstDispatched
            Expect.equal (List.ofSeq sent) [ toSave ] "geschickt, was gespeichert werden soll"
            let rejected, external = update answer changed
            Expect.equal rejected.AutoFetchError (Some "Intervall muss zwischen 60 und 1440 Minuten liegen") "am Feld, ohne technischen Vorsatz"
            Expect.isFalse rejected.AutoFetchSaving "wieder bedienbar"
            Expect.equal external Components.Settings.Types.NoOp "kein Toast"
            let retried, _ = update (Components.Settings.Types.SetAutoFetchInterval 360) rejected
            Expect.isNone retried.AutoFetchError "der nächste Versuch räumt die Meldung"
        }

        test "die Karte Sync-Einstellungen speichert die Tage und lässt Schalter und Intervall wie gespeichert" {
            let edited, _ = update (Components.Settings.Types.UpdateSyncDaysInput 30) { loaded with AutoFetchEnabledInput = false }
            Expect.equal
                (Components.Settings.Types.syncDaysSettingsToSave edited)
                { DaysToFetch = 30; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 120 }
                "die Tage neu, der Rest gespeichert"
        }

        test "die Stufen reichen von 1 bis 24 Stunden, ein gespeicherter Wert dazwischen steht zusätzlich in der Liste" {
            Expect.equal
                (Components.Settings.Types.autoFetchIntervalOptions 360 |> List.map fst)
                [ 60; 120; 180; 240; 360; 480; 720; 1440 ]
                "Stufen"
            Expect.equal
                (Components.Settings.Types.autoFetchIntervalOptions 90 |> List.map fst)
                [ 60; 90; 120; 180; 240; 360; 480; 720; 1440 ]
                "90 Minuten zusätzlich"
        }
    ]
