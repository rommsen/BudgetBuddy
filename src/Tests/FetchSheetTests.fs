module Tests.FetchSheetTests

// Das Sheet „Abrufen“ (banking-import-d0fey, ADR 0093): der Ergebnissatz als reine Funktion, die Zustände
// durch das echte `update` mit Ports als Funktionswerten (Doktrin Punkt 1). Die Reihenfolgen von Start,
// Lesen, TAN und Abbruch zählt SyncStartOrderTests auf.

open Expecto
open Shared.Domain
open Types
open Components.Fetch.Types
open Components.Fetch.State

let private sessionWith (id: string) (status: SyncSessionStatus) (counts: FetchCounts) : SyncSession =
    { Id = SyncSessionId (System.Guid.Parse id)
      StartedAt = System.DateTime(2026, 11, 11)
      CompletedAt = None
      Status = status
      Trigger = Manual
      Counts = counts }

let private before = sessionWith "00000000-0000-4000-8000-0000000000b1" Completed emptyFetchCounts
let private booked = sessionWith "00000000-0000-4000-8000-0000000000b2" Completed { emptyFetchCounts with NewEntries = 2; WithoutCategory = 2 }
let private waitingTan = sessionWith "00000000-0000-4000-8000-0000000000b2" AwaitingTan emptyFetchCounts

let private never<'T> : Async<'T> = Async.FromContinuations(fun _ -> ())

type private Calls = { mutable Reads: int; mutable Starts: int; mutable Confirms: int; mutable Cancels: int }

let private portsWith (calls: Calls) : FetchPorts =
    { GetCurrentSession = fun () -> calls.Reads <- calls.Reads + 1; never
      StartSync = fun () -> calls.Starts <- calls.Starts + 1; never
      ConfirmTan = fun _ -> calls.Confirms <- calls.Confirms + 1; never
      CancelSync = fun _ -> calls.Cancels <- calls.Cancels + 1; never }

let private newCalls () = { Reads = 0; Starts = 0; Confirms = 0; Cancels = 0 }

/// Schickt Nachrichten nacheinander durch `updateWith` und liefert Modell und letzte äußere Nachricht.
let private steps (calls: Calls) (model: Model) (messages: Msg list) : Model * ExternalMsg =
    messages
    |> List.fold (fun (m, _) msg -> let m', _, ext = updateWith (portsWith calls) msg m in m', ext) (model, NoOp)

/// „Abrufen“ ohne laufende Sitzung: Lesen, Antwort, Start.
let private startedFrom (calls: Calls) (server: SyncSession option) : Model =
    let opened, _ = steps calls init [ Opened ]
    let model, _ = steps calls opened [ SessionReadAnswered (opened.SessionReadVersion, Ok server) ]
    model

[<Tests>]
let resultTests =
    testList "Abrufen: der Ergebnissatz" [

        testCase "der Kopf nennt die neuen Buchungen in Einzahl und Mehrzahl, ohne neue Nichts Neues" <| fun () ->
            Expect.equal (resultTitle emptyFetchCounts) "Nichts Neues" "0"
            Expect.equal (resultTitle { emptyFetchCounts with NewEntries = 1 }) "1 neue Buchung" "Einzahl"
            Expect.equal (resultTitle { emptyFetchCounts with NewEntries = 6 }) "6 neue Buchungen" "Mehrzahl"

        testCase "ohne neue Buchung steht der Satz des Prototyps da" <| fun () ->
            Expect.equal (resultSentence emptyFetchCounts) "Seit dem letzten Abruf gibt es keine neuen Umsätze." "nichts"
            Expect.equal
                (resultSentence { emptyFetchCounts with SafeDuplicatesSkipped = 4 })
                "Seit dem letzten Abruf gibt es keine neuen Umsätze."
                "sichere Duplikate allein sind nichts Neues"

        // Review-Fund zu banking-import-d0fey: ohne neue Buchung stand der feste Satz da, auch wenn Umsätze mit
        // Datum in der Zukunft noch folgen.
        testCase "ohne neue Buchung, aber mit Umsätzen in der Zukunft, nennt der Satz sie statt des festen Satzes" <| fun () ->
            Expect.equal
                (resultSentence { emptyFetchCounts with FutureDated = 1 })
                "1 mit Datum in der Zukunft folgt beim nächsten Abruf."
                "einer"
            Expect.equal
                (resultSentence { emptyFetchCounts with SafeDuplicatesSkipped = 1; FutureDated = 3 })
                "3 mit Datum in der Zukunft folgen beim nächsten Abruf."
                "mehrere"

        testCase "der Satz des Prototyps mit Regel, ohne Kategorie, sicheren Duplikaten und einem unsicheren" <| fun () ->
            Expect.equal
                (resultSentence { emptyFetchCounts with NewEntries = 6; ByRule = 2; WithoutCategory = 3; SafeDuplicatesSkipped = 2; Held = 1 })
                "2 per Regel kategorisiert, 3 ohne Kategorie. 2 sichere Duplikate übersprungen, 1 unsicheres wartet auf dich und geht erst danach nach YNAB."
                "Prototyp"

        for name, counts, expected in
            [ "nur per Regel", { emptyFetchCounts with NewEntries = 3; ByRule = 3 }, "3 per Regel kategorisiert."
              "nur ohne Kategorie", { emptyFetchCounts with NewEntries = 1; WithoutCategory = 1 }, "1 ohne Kategorie."
              "aus Bestellnummer", { emptyFetchCounts with NewEntries = 2; ByRule = 1; ByOrderId = 1 }, "1 per Regel kategorisiert, 1 aus Bestellnummer."
              "ein sicheres Duplikat", { emptyFetchCounts with NewEntries = 1; ByRule = 1; SafeDuplicatesSkipped = 1 }, "1 per Regel kategorisiert. 1 sicheres Duplikat übersprungen."
              "zwei unsichere", { emptyFetchCounts with NewEntries = 2; WithoutCategory = 2; Held = 2 }, "2 ohne Kategorie. 2 unsichere warten auf dich und gehen erst danach nach YNAB."
              "nur unsichere ohne sichere", { emptyFetchCounts with NewEntries = 1; WithoutCategory = 1; Held = 1 }, "1 ohne Kategorie. 1 unsicheres wartet auf dich und geht erst danach nach YNAB."
              "nicht in YNAB", { emptyFetchCounts with NewEntries = 2; ByRule = 2; NotInYnab = 2 }, "2 per Regel kategorisiert. 2 noch nicht in YNAB, sie stehen unter Zu tun."
              "ohne nicht in YNAB", { emptyFetchCounts with NewEntries = 2; ByRule = 2 }, "2 per Regel kategorisiert."
              // Review-Fund zu banking-import-d0fey: Umsätze nach dem Berliner Tag fielen still aus dem Ergebnis.
              "einer mit Datum in der Zukunft", { emptyFetchCounts with NewEntries = 1; WithoutCategory = 1; FutureDated = 1 }, "1 ohne Kategorie. 1 mit Datum in der Zukunft folgt beim nächsten Abruf."
              "zwei mit Datum in der Zukunft", { emptyFetchCounts with NewEntries = 1; ByRule = 1; NotInYnab = 1; FutureDated = 2 }, "1 per Regel kategorisiert. 1 noch nicht in YNAB, sie stehen unter Zu tun. 2 mit Datum in der Zukunft folgen beim nächsten Abruf." ] do
            testCase $"Teile mit 0 entfallen ({name})" <| fun () ->
                Expect.equal (resultSentence counts) expected name

        testCase "der Erklärsatz unter der TAN nennt die Tage des Abrufs" <| fun () ->
            Expect.equal
                (tanExplanation 30)
                "Danach ruft BB die Umsätze der letzten 30 Tage ab und gleicht sie mit dem Kassenbuch ab."
                "Tage aus den Einstellungen"
    ]

[<Tests>]
let stateTests =
    testList "Abrufen: Zustände durch das echte update" [

        testCase "Abrufen öffnet das Sheet und liest zuerst die Sitzung, gestartet wird erst danach" <| fun () ->
            let calls = newCalls ()
            let opened, _ = steps calls init [ Opened ]
            Expect.isTrue opened.IsOpen "offen"
            Expect.equal (sheetScreen opened) RunningScreen "Rufe ab…"
            Expect.equal (calls.Reads, calls.Starts) (1, 0) "erst lesen"
            let _ = startedFrom calls (Some before)
            Expect.equal calls.Starts 1 "dann starten"

        testCase "eine wartende TAN wird gezeigt, nicht neu gestartet" <| fun () ->
            let calls = newCalls ()
            let model = startedFrom calls (Some waitingTan)
            Expect.equal (sheetScreen model) TanScreen "TAN"
            Expect.equal calls.Starts 0 "kein zweiter Start"

        testCase "Start mit TanRequired zeigt die TAN" <| fun () ->
            let calls = newCalls ()
            let model, ext = steps calls (startedFrom calls None) [ SyncStarted (Ok (TanRequired waitingTan)) ]
            Expect.equal (sheetScreen model) TanScreen "TAN"
            Expect.equal ext NoOp "noch kein Ergebnis"

        testCase "Start mit Ergebnis zeigt die Zahlen und lässt den Root nachladen" <| fun () ->
            let calls = newCalls ()
            let model, ext = steps calls (startedFrom calls None) [ SyncStarted (Ok (Fetched booked)) ]
            Expect.equal (sheetScreen model) (DoneScreen booked.Counts) "Ergebnis"
            Expect.equal ext (FetchFinished None) "Zu tun und Zähler laden neu, ohne Toast bei offenem Sheet"

        testCase "ein Fehler zeigt die Meldung des Servers" <| fun () ->
            let calls = newCalls ()
            let model, ext =
                steps calls (startedFrom calls None) [ SyncStarted (Error (SyncError.YnabImportFailed (0, "YNAB ist nicht erreichbar, es wurde nichts gebucht."))) ]
            Expect.equal (sheetScreen model) (ErrorScreen "YNAB ist nicht erreichbar, es wurde nichts gebucht.") "Meldung"
            Expect.equal ext (FetchFinished None) "die Sync-Historie hat einen Eintrag mehr"

        testCase "verlorene Antwort: die beendete neue Sitzung ist das Ergebnis" <| fun () ->
            let calls = newCalls ()
            let lost, _ = steps calls (startedFrom calls (Some before)) [ SyncStartInterrupted "Http error (0)" ]
            Expect.equal calls.Reads 2 "nachgelesen"
            let model, ext = steps calls lost [ SessionReadAnswered (lost.SessionReadVersion, Ok (Some booked)) ]
            Expect.equal (sheetScreen model) (DoneScreen booked.Counts) "Ergebnis gelesen"
            Expect.equal ext (FetchFinished None) "nachladen"

        testCase "verlorene Antwort: die alte Sitzung ist kein Ergebnis, das Sheet meldet den Abbruch" <| fun () ->
            let calls = newCalls ()
            let lost, _ = steps calls (startedFrom calls (Some before)) [ SyncStartInterrupted "Http error (0)" ]
            let model, _ = steps calls lost [ SessionReadAnswered (lost.SessionReadVersion, Ok (Some before)) ]
            Expect.equal (sheetScreen model) (ErrorScreen (interruptedStartMessage "Http error (0)")) "Abbruch"
            Expect.equal calls.Starts 1 "nie neu gestartet"

        // Früher „Nachlesen ohne Grenze“ (SyncFlow): las der Client nach verlorener Antwort ewig weiter, stand
        // „Rufe ab…“ für immer da. Die Frist läuft hier echt durch, Takt für Takt.
        testCase "verlorene Antwort: solange der Server abruft, wird weiter gelesen, nach der Frist endet es" <| fun () ->
            let calls = newCalls ()
            let lost, _ = steps calls (startedFrom calls (Some before)) [ SyncStartInterrupted "Http error (0)" ]
            let running = Some { booked with Status = ImportingToYnab }
            let waiting, _ = steps calls lost [ SessionReadAnswered (lost.SessionReadVersion, Ok running) ]
            Expect.equal (sheetScreen waiting) RunningScreen "läuft noch"
            let readsBefore = calls.Reads
            let rec poll (model: Model) (rounds: int) =
                match model.Phase with
                | FetchRecovering recovery when rounds <= interruptedStartReadLimit ->
                    let read, _ = steps calls model [ InterruptedStartReadDue recovery.ReadsLeft ]
                    let answered, _ = steps calls read [ SessionReadAnswered (read.SessionReadVersion, Ok running) ]
                    poll answered (rounds + 1)
                | _ -> model, rounds
            let model, rounds = poll waiting 0
            Expect.equal (sheetScreen model) (ErrorScreen (interruptedStartTimeoutMessage "Http error (0)")) "Frist"
            Expect.equal rounds interruptedStartReadLimit "so viele Takte wie die Frist"
            Expect.equal (calls.Reads - readsBefore) interruptedStartReadLimit "je Takt ein Lesen"
            Expect.equal calls.Starts 1 "nie neu gestartet"

        testCase "der Takt beim Nachlesen läuft nach der Pause ab" <| fun () ->
            let calls = newCalls ()
            let lost, _ = steps calls (startedFrom calls (Some before)) [ SyncStartInterrupted "Http error (0)" ]
            let _, cmd, _ = updateWith (portsWith calls) (SessionReadAnswered (lost.SessionReadVersion, Ok (Some { booked with Status = FetchingTransactions }))) lost
            CmdTiming.expectAfter interruptedStartPollMs (InterruptedStartReadDue (interruptedStartReadLimit - 1)) cmd

        testCase "Ich habe bestätigt: das Sheet zeigt Rufe ab, danach das Ergebnis" <| fun () ->
            let calls = newCalls ()
            let tan, _ = steps calls (startedFrom calls None) [ SyncStarted (Ok (TanRequired waitingTan)) ]
            let confirming, _ = steps calls tan [ ConfirmTan; ConfirmTan ]
            Expect.equal calls.Confirms 1 "eine Bestätigung, auch bei zwei Tipps"
            Expect.equal (sheetScreen confirming) RunningScreen "Rufe ab…"
            let model, ext = steps calls confirming [ TanConfirmed (Ok booked) ]
            Expect.equal (sheetScreen model) (DoneScreen booked.Counts) "Ergebnis"
            Expect.equal ext (FetchFinished None) "nachladen"

        testCase "verlorene Antwort der TAN-Bestätigung: wartet die Sitzung noch, zeigt das Sheet die TAN wieder" <| fun () ->
            let calls = newCalls ()
            let tan, _ = steps calls (startedFrom calls None) [ SyncStarted (Ok (TanRequired waitingTan)); ConfirmTan ]
            let lost, _ = steps calls tan [ TanConfirmInterrupted "Http error (0)" ]
            let model, _ = steps calls lost [ SessionReadAnswered (lost.SessionReadVersion, Ok (Some waitingTan)) ]
            Expect.equal (sheetScreen model) TanScreen "TAN wieder"
            let finished, _ = steps calls lost [ SessionReadAnswered (lost.SessionReadVersion, Ok (Some booked)) ]
            Expect.equal (sheetScreen finished) (DoneScreen booked.Counts) "oder das Ergebnis"

        testCase "Abbrechen schließt das Sheet und bricht die Sitzung ab" <| fun () ->
            let calls = newCalls ()
            let tan, _ = steps calls (startedFrom calls None) [ SyncStarted (Ok (TanRequired waitingTan)) ]
            let model, _ = steps calls tan [ CancelSync ]
            Expect.isFalse model.IsOpen "zu"
            Expect.equal calls.Cancels 1 "cancelSync"

        testCase "Jetzt durchgehen schließt das Sheet und öffnet das Durchgehen" <| fun () ->
            let calls = newCalls ()
            let done', _ = steps calls (startedFrom calls None) [ SyncStarted (Ok (Fetched booked)) ]
            let model, ext = steps calls done' [ ReviewRequested ]
            Expect.isFalse model.IsOpen "zu"
            Expect.equal ext OpenReview "ins Durchgehen"

        // Review-Fund zu banking-import-d0fey: scheiterte das erste Lesen beim Öffnen, stand „Die Verbindung zum
        // Server brach ab“, als sei ein Start verloren gegangen.
        testCase "scheitert das Lesen vor dem Start, sagt das Sheet, dass nichts abgerufen wurde" <| fun () ->
            let calls = newCalls ()
            let opened, _ = steps calls init [ Opened ]
            let model, _ = steps calls opened [ SessionReadAnswered (opened.SessionReadVersion, Error "Http error (0)") ]
            Expect.equal (sheetScreen model) (ErrorScreen (sessionReadFailedMessage "Http error (0)")) "eigener Satz"
            Expect.equal calls.Starts 0 "kein Start"
            Expect.equal model.Phase FetchIdle "ein neues Abrufen geht wieder"

        // Früher „Routenwechsel startet zweite Kette“ (SyncFlow): ein Lesen von außen während des Nachlesens
        // startete eine zweite Lesekette. Heute liest nur „Abrufen“ von außen.
        testCase "Abrufen während des Nachlesens liest nicht ein zweites Mal" <| fun () ->
            let calls = newCalls ()
            let lost, _ = steps calls (startedFrom calls (Some before)) [ SyncStartInterrupted "Http error (0)"; Closed ]
            let readsBefore = calls.Reads
            let reopened, _ = steps calls lost [ Opened ]
            Expect.equal calls.Reads readsBefore "kein zweites Lesen"
            Expect.equal reopened.SessionReadVersion lost.SessionReadVersion "das laufende Lesen gilt weiter"
            Expect.equal (sheetScreen reopened) RunningScreen "Rufe ab…"

        testCase "scheitert die TAN-Bestätigung, zeigt das Sheet die Meldung und der Root lädt nach" <| fun () ->
            let calls = newCalls ()
            let tan, _ = steps calls (startedFrom calls None) [ SyncStarted (Ok (TanRequired waitingTan)); ConfirmTan ]
            let model, ext = steps calls tan [ TanConfirmed (Error SyncError.TanTimeout) ]
            Expect.equal (sheetScreen model) (ErrorScreen (syncErrorText SyncError.TanTimeout)) "Meldung"
            Expect.equal ext (FetchFinished None) "die Sync-Historie hat einen Eintrag mehr"
    ]

// Review-Fund zu banking-import-d0fey: schloss Roman das Sheet während „Rufe ab…“, gingen Push-TAN, Fehler und
// Ergebnis unbemerkt vorbei. TAN und Fehler holen das Sheet wieder nach vorn, ein Ergebnis kommt als Toast.
[<Tests>]
let closedSheetTests =
    testList "Abrufen: geschlossenes Sheet während des Abrufs" [

        let closedWhileStarting (calls: Calls) (server: SyncSession option) =
            let model, _ = steps calls (startedFrom calls server) [ Closed ]
            Expect.isFalse model.IsOpen "zu"
            model

        testCase "die Push-TAN öffnet das Sheet wieder" <| fun () ->
            let calls = newCalls ()
            let model, _ = steps calls (closedWhileStarting calls None) [ SyncStarted (Ok (TanRequired waitingTan)) ]
            Expect.isTrue model.IsOpen "wieder offen"
            Expect.equal (sheetScreen model) TanScreen "TAN"

        testCase "ein Fehler beim Start öffnet das Sheet wieder" <| fun () ->
            let calls = newCalls ()
            let model, _ = steps calls (closedWhileStarting calls None) [ SyncStarted (Error SyncError.TanTimeout) ]
            Expect.isTrue model.IsOpen "wieder offen"
            Expect.equal (sheetScreen model) (ErrorScreen (syncErrorText SyncError.TanTimeout)) "Meldung"

        testCase "ein Fehler der TAN-Bestätigung öffnet das Sheet wieder" <| fun () ->
            let calls = newCalls ()
            let tan, _ = steps calls (startedFrom calls None) [ SyncStarted (Ok (TanRequired waitingTan)); ConfirmTan; Closed ]
            let model, _ = steps calls tan [ TanConfirmed (Error SyncError.TanTimeout) ]
            Expect.isTrue model.IsOpen "wieder offen"

        testCase "eine fehlgeschlagene Sitzung öffnet das Sheet wieder" <| fun () ->
            let calls = newCalls ()
            let failedSession = { booked with Status = Failed "YNAB ist nicht erreichbar, es wurde nichts gebucht." }
            let model, ext = steps calls (closedWhileStarting calls None) [ SyncStarted (Ok (Fetched failedSession)) ]
            Expect.isTrue model.IsOpen "wieder offen"
            Expect.equal ext (FetchFinished None) "nachladen, ohne Toast"

        testCase "die verlorene Antwort ohne Ergebnis öffnet das Sheet wieder" <| fun () ->
            let calls = newCalls ()
            let lost, _ = steps calls (closedWhileStarting calls (Some before)) [ SyncStartInterrupted "Http error (0)" ]
            let model, _ = steps calls lost [ SessionReadAnswered (lost.SessionReadVersion, Ok (Some before)) ]
            Expect.isTrue model.IsOpen "wieder offen"
            Expect.equal (sheetScreen model) (ErrorScreen (interruptedStartMessage "Http error (0)")) "Abbruch"

        testCase "eine wartende TAN beim Nachlesen öffnet das Sheet wieder" <| fun () ->
            let calls = newCalls ()
            let lost, _ = steps calls (closedWhileStarting calls (Some before)) [ SyncStartInterrupted "Http error (0)" ]
            let model, _ = steps calls lost [ SessionReadAnswered (lost.SessionReadVersion, Ok (Some waitingTan)) ]
            Expect.isTrue model.IsOpen "wieder offen"
            Expect.equal (sheetScreen model) TanScreen "TAN"

        testCase "eine schon wartende TAN beim Lesen vor dem Start öffnet das Sheet wieder" <| fun () ->
            let calls = newCalls ()
            let opened, _ = steps calls init [ Opened; Closed ]
            let model, _ = steps calls opened [ SessionReadAnswered (opened.SessionReadVersion, Ok (Some waitingTan)) ]
            Expect.isTrue model.IsOpen "wieder offen"
            Expect.equal (sheetScreen model) TanScreen "TAN"
            Expect.equal calls.Starts 0 "kein Start"

        // banking-import-ns4re (R3): das Ergebnis bei geschlossenem Sheet ist eine Meldung für den Meldungsplatz,
        // am Handy in der Leiste, am PC als Toast. Aktion „Abgerufen“ (das ✓ setzt die Leiste), darunter der Titel.
        testCase "ein Ergebnis bei geschlossenem Sheet kommt als Meldung, das Sheet bleibt zu" <| fun () ->
            let calls = newCalls ()
            let model, ext = steps calls (closedWhileStarting calls None) [ SyncStarted (Ok (Fetched booked)) ]
            Expect.isFalse model.IsOpen "bleibt zu"
            Expect.equal ext (FetchFinished (Some ("Abgerufen", "2 neue Buchungen"))) "Meldung mit dem Ergebnis"

        testCase "ein Ergebnis nach der TAN bei geschlossenem Sheet kommt als Meldung" <| fun () ->
            let calls = newCalls ()
            let tan, _ = steps calls (startedFrom calls None) [ SyncStarted (Ok (TanRequired waitingTan)); ConfirmTan; Closed ]
            let model, ext = steps calls tan [ TanConfirmed (Ok { booked with Counts = emptyFetchCounts }) ]
            Expect.isFalse model.IsOpen "bleibt zu"
            Expect.equal ext (FetchFinished (Some ("Abgerufen", "Nichts Neues"))) "Meldung"

        testCase "resultMessage: Aktion „Abgerufen“, darunter der Titel, ohne neue Buchung kurz mit den späteren" <| fun () ->
            let line counts = resultMessage counts
            Expect.equal (line { emptyFetchCounts with NewEntries = 6; ByRule = 6 }) ("Abgerufen", "6 neue Buchungen") "neu"
            Expect.equal (line { emptyFetchCounts with NewEntries = 1 }) ("Abgerufen", "1 neue Buchung") "Einzahl"
            Expect.equal (line emptyFetchCounts) ("Abgerufen", "Nichts Neues") "nichts"
            Expect.equal (line { emptyFetchCounts with FutureDated = 2 }) ("Abgerufen", "Nichts Neues, 2 folgen später") "nur Zukunft"
            Expect.equal (line { emptyFetchCounts with FutureDated = 1 }) ("Abgerufen", "Nichts Neues, 1 folgt später") "nur Zukunft, Einzahl"
            Expect.equal
                (line { emptyFetchCounts with NewEntries = 3; ByRule = 3; FutureDated = 1 })
                ("Abgerufen", "3 neue Buchungen")
                "mit neuen Buchungen bleibt die Zeile kurz"

        // Das Sheet ist beim Abbrechen schon zu: ein gescheitertes Abbrechen ist eine rote Meldung ohne „Erneut“
        // (die Sitzung ist im Modell verworfen, ein neuer Abruf liest sie wieder, ADR 0071).
        testCase "ein gescheitertes Abbrechen kommt als Fehlermeldung „Nicht abgebrochen“ mit dem Grund" <| fun () ->
            let calls = newCalls ()
            let tan, _ = steps calls (startedFrom calls None) [ SyncStarted (Ok (TanRequired waitingTan)); CancelSync ]
            let err = SyncError.DatabaseError ("cancel_sync", "gesperrt")
            let model, ext = steps calls tan [ SyncCancelled (Error err) ]
            Expect.isFalse model.IsOpen "bleibt zu"
            Expect.equal ext (ShowFeedback (FeedbackError, "Nicht abgebrochen", Some (syncErrorText err))) "Fehler in der Leiste"
            let _, okExt = steps calls tan [ SyncCancelled (Ok ()) ]
            Expect.equal okExt NoOp "gelungenes Abbrechen meldet nichts"
    ]
