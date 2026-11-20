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

let private ruleTest : Components.Rules.Types.TestRequest =
    { Pattern = "rewe"; PatternType = Exact; TargetField = Payee; Input = "REWE Markt" }

[<Tests>]
let tests =
    testList "Ausgehende Aufrufe im Client" [
        testList "Muster testen im Regel-Formular" [
            // categorization-n3sxw: die Antwort trägt ihre Anfrage, damit eine späte Antwort nichts aus ihr zeigt.
            test "der Aufruf bekommt Muster, Art, Feld und Text, die Antwort kommt mit ihrer Anfrage an" {
                let seen = ref None
                let call args = async { seen.Value <- Some args; return Ok false }
                let msg = firstDispatched (Components.Rules.State.testPatternCmd call ruleTest)
                Expect.equal seen.Value (Some ("rewe", Exact, Payee, "REWE Markt")) "Argumente"
                Expect.equal msg (Components.Rules.Types.PatternTested (ruleTest, Ok false)) "Urteil"
            }
            // Bug categorization-f955d: ein Netzfehler erschien als ungültiges Muster.
            test "bleibt die Antwort aus, ist das ein Netzfehler und kein Urteil über das Muster" {
                let msg = firstDispatched (Components.Rules.State.testPatternCmd lost ruleTest)
                Expect.equal msg (Components.Rules.Types.PatternTestLost ruleTest) "Netzfehler"
            }
        ]
        // Review-Fund categorization-n3sxw: Löschen, Rückgängig und Umordnen riefen `Client.Api` direkt, die
        // übergebene Stelle und Reihenfolge waren ungetestet. Jetzt über `updateWith` mit einem Ersatz.
        testList "Regeln löschen, zurückholen, umordnen" [
            let rule (n: int) : Rule =
                { Id = RuleId (System.Guid($"20000000-0000-0000-0000-{n:D12}"))
                  Name = $"Regel {n}"; Pattern = $"m{n}"; PatternType = Contains; TargetField = Payee
                  CategoryId = YnabCategoryId (System.Guid($"30000000-0000-0000-0000-{n:D12}")); CategoryName = "K"
                  PayeeOverride = None; Priority = 10 - n; Enabled = true
                  CreatedAt = System.DateTime(2026, 1, 1); UpdatedAt = System.DateTime(2026, 1, 1) }
            let a, b, c = rule 1, rule 2, rule 3
            let shown = { fst (Components.Rules.State.init ()) with Rules = Success [ a; b; c ]; LoadVersion = 1 }
            let unused _ = async { return failtest "nicht erwartet" }
            let portsWith (seen: obj option ref) : Components.Rules.State.RulesPorts =
                { GetAll = unused
                  Test = unused
                  Create = unused
                  Change = unused
                  Delete = fun id -> async { seen.Value <- Some (box id); return Ok () }
                  Restore = fun request -> async { seen.Value <- Some (box request); return Ok [ a; b; c ] }
                  Reorder = fun ids -> async { seen.Value <- Some (box ids); return Ok () } }
            let run msg model =
                let seen = ref None
                let _, cmd, _ = Components.Rules.State.updateWith (portsWith seen) msg model
                let answer = firstDispatched cmd
                seen.Value, answer
            test "Löschen schickt die Id der offenen Regel, die Antwort kommt mit ihrer Stelle" {
                let opened, _, _ = Components.Rules.State.update (Components.Rules.Types.RuleChosen b.Id) shown
                let seen, answer = run Components.Rules.Types.DeleteRequested opened
                Expect.equal seen (Some (box b.Id)) "Id"
                Expect.equal answer (Components.Rules.Types.RuleDeleted (b, 1, Ok ())) "Stelle 1"
            }
            test "Rückgängig schickt Regel und Stelle der Zeile „gelöscht“, die Antwort ist die Liste des Servers" {
                let deleted : Components.Rules.Types.RemovedRule = { Rule = c; Position = 2; Phase = Components.Rules.Types.Removed }
                let seen, answer = run Components.Rules.Types.RestoreRequested { shown with Removed = Some deleted }
                Expect.equal seen (Some (box ({ Rule = c; Position = 2 } : RuleRestore))) "Regel und Stelle"
                Expect.equal answer (Components.Rules.Types.RuleRestored (c, Ok [ a; b; c ])) "Liste"
            }
            test "Ein Pfeil schickt die neue Reihenfolge" {
                let seen, answer = run (Components.Rules.Types.MoveRequested (b.Id, Components.Rules.Types.Up)) shown
                Expect.equal seen (Some (box [ b.Id; a.Id; c.Id ])) "neue Reihenfolge"
                Expect.equal answer (Components.Rules.Types.RulesReordered (Ok ())) "Antwort"
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
            // banking-import-vwhfm (R3): „✓ Gespeichert“ unter dem Auswahlfeld, kein Toast.
            Expect.equal
                (Components.Settings.Types.doneAt
                    (Components.Settings.Types.Field (Components.Settings.Types.AutoFetchField Components.Settings.Types.AutoFetchIntervalField))
                    saved)
                (Some "Gespeichert")
                "unter „Alle“"
            Expect.equal external Components.Settings.Types.NoOp "kein Toast"
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
            Expect.equal
                (Components.Settings.Types.autoFetchErrorAt Components.Settings.Types.AutoFetchIntervalField rejected)
                (Some "Intervall muss zwischen 60 und 1440 Minuten liegen")
                "am Feld, ohne technischen Vorsatz"
            Expect.isNone (Components.Settings.Types.autoFetchErrorAt Components.Settings.Types.AutoFetchSwitch rejected) "nicht am Schalter"
            Expect.equal rejected.AutoFetchIntervalInput 120 "das Feld zeigt wieder den gespeicherten Wert"
            Expect.isFalse rejected.AutoFetchSaving "wieder bedienbar"
            Expect.equal external Components.Settings.Types.NoOp "kein Toast"
            let retried, _ = update (Components.Settings.Types.SetAutoFetchInterval 360) rejected
            Expect.isNone retried.Feedback "der nächste Versuch räumt die Meldung"
        }

        // Review-Fund zu banking-import-t4raw: nach einer Ablehnung blieb der Schalter auf Aus, BB rief aber weiter
        // ab, und die Meldung stand am Feld „Alle“.
        test "lehnt der Server den Schalter ab, steht er wieder an und die Meldung steht am Schalter" {
            let off, _ = update (Components.Settings.Types.SetAutoFetchEnabled false) loaded
            let sent = Components.Settings.Types.autoFetchSettingsToSave off
            let rejected, _ = update (Components.Settings.Types.AutoFetchSaved (sent, Error (SettingsError.DatabaseError ("save", "weg")))) off
            Expect.isTrue rejected.AutoFetchEnabledInput "wieder an, wie gespeichert"
            Expect.isSome (Components.Settings.Types.autoFetchErrorAt Components.Settings.Types.AutoFetchSwitch rejected) "am Schalter"
            Expect.isNone (Components.Settings.Types.autoFetchErrorAt Components.Settings.Types.AutoFetchIntervalField rejected) "nicht am Feld"
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

// Review-Fund zu banking-import-t4raw (Doktrin Punkt 20): die Sperre beim Speichern von „Automatisch abrufen“
// und das Speichern der Karte Sync-Einstellungen über alle Reihenfolgen. Beide schreiben alle drei Werte; liefe
// das Speichern der Karte, während „Automatisch abrufen“ speichert, überschriebe es Schalter und Intervall mit dem
// alten Stand.

/// Was der Server gespeichert hat, was „Automatisch abrufen“ gerade schickt und welche Tage eingegeben sind, wie
/// die Aufzählung es kennt. Das Orakel ist eigen: es glaubt weder `AutoFetchSaving` noch `syncSaveLocked`.
type private AutoFetchWalk = { Saved: SyncSettings; Sent: SyncSettings option; Days: int }

[<Tests>]
let autoFetchSaveWalkTests =
    let open' = { DaysToFetch = 14; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 120 }
    let start =
        let model, _ = Components.Settings.State.init ()
        let view : AppSettingsView = { Comdirect = None; Ynab = None; Sync = open' }
        let loaded, _, _ = Components.Settings.State.update (Components.Settings.Types.SettingsLoaded (Ok view)) model
        loaded
    let rejection = SettingsError.DatabaseError ("validation", "Intervall muss zwischen 60 und 1440 Minuten liegen")
    /// Alle Ereignisse in diesem Zustand mit dem Stand danach. `bool` sagt, ob ein Aufruf erwartet wird.
    let events (walk: AutoFetchWalk) : (string * Components.Settings.Types.Msg * AutoFetchWalk * bool) list =
        let idle = walk.Sent.IsNone
        let sending change = if idle then { walk with Sent = Some (change walk.Saved) } else walk
        [ for enabled in [ true; false ] do
              $"Schalter {enabled}",
              Components.Settings.Types.SetAutoFetchEnabled enabled,
              sending (fun saved -> { saved with AutoFetchEnabled = enabled }),
              idle
          for minutes in [ 120; 480 ] do
              $"Intervall {minutes}",
              Components.Settings.Types.SetAutoFetchInterval minutes,
              sending (fun saved -> { saved with AutoFetchIntervalMinutes = minutes }),
              idle
          for days in [ 14; 30 ] do
              $"Tage {days}", Components.Settings.Types.UpdateSyncDaysInput days, { walk with Days = days }, false
          "Sync speichern", Components.Settings.Types.SaveSyncSettings, walk, idle
          match walk.Sent with
          | Some sent ->
              "Gespeichert", Components.Settings.Types.AutoFetchSaved (sent, Ok ()), { walk with Saved = sent; Sent = None }, false
              "Abgelehnt", Components.Settings.Types.AutoFetchSaved (sent, Error rejection), { walk with Sent = None }, false
          | None -> () ]
    let check (path: string list) (model: Components.Settings.Types.Model) (walk: AutoFetchWalk) =
        let where = String.concat " > " (List.rev path)
        Expect.equal model.AutoFetchSaving walk.Sent.IsSome $"speichert: {where}"
        let shown = walk.Sent |> Option.defaultValue walk.Saved
        Expect.equal (model.AutoFetchEnabledInput, model.AutoFetchIntervalInput) (shown.AutoFetchEnabled, shown.AutoFetchIntervalMinutes) $"Eingabe: {where}"
        Expect.equal model.SyncDaysInput walk.Days $"Tage: {where}"
        match model.Settings with
        | Success settings ->
            Expect.equal
                (settings.Sync.AutoFetchEnabled, settings.Sync.AutoFetchIntervalMinutes)
                (walk.Saved.AutoFetchEnabled, walk.Saved.AutoFetchIntervalMinutes)
                $"gespeicherter Stand: {where}"
        | other -> failtestf "geladen erwartet: %A (%s)" other where
    let rec walkFrom (depth: int) (path: string list) (model: Components.Settings.Types.Model) (walk: AutoFetchWalk) : int =
        if depth = 0 then 0
        else
            events walk
            |> List.sumBy (fun (name, msg, after, callExpected) ->
                let path' = name :: path
                let next, cmd, _ = Components.Settings.State.update msg model
                let where = String.concat " > " (List.rev path')
                Expect.equal (not (List.isEmpty cmd)) callExpected $"Aufruf: {where}"
                if name = "Sync speichern" && callExpected then
                    // Die Karte schickt die eingegebenen Tage, Schalter und Intervall wie gespeichert.
                    Expect.equal
                        (Components.Settings.Types.syncDaysSettingsToSave model)
                        { walk.Saved with DaysToFetch = walk.Days }
                        $"Sync schickt: {where}"
                check path' next after
                1 + walkFrom (depth - 1) path' next after)
    testList "Einstellungen: Speichern von automatisch abrufen in jeder Reihenfolge" [
        test "Schalter, Intervall, Antworten, Tage und Speichern der Karte halten Sperre und gespeicherten Stand" {
            let initial = { Saved = open'; Sent = None; Days = 14 }
            check [] start initial
            let steps = walkFrom 5 [] start initial
            Expect.isGreaterThan steps 10000 "die Aufzählung erreicht genug Reihenfolgen"
        }
    ]
