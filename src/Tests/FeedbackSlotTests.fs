module Tests.FeedbackSlotTests

// Der eine Meldungsplatz der App (Regel R3, ADR 0098, design-system-gkded). Vorher hielt die App eine Liste von
// Toasts (`Model.Toasts`), mehrere standen zugleich und verdeckten am Handy Liste und „+ Buchung“. Jetzt gibt es
// höchstens eine Meldung (`Model.Feedback`): eine neue ersetzt die alte, gleiche Wische fassen sich zu „{n} geprüft“
// zusammen. Geprüft werden die reinen Funktionen (`FeedbackSlot.post`, `splitText`, `ofToast`) und das Wurzel-`update`
// für Klick, Abgang des Toasts am PC, Seitenwechsel und PWA-Hinweis (kein Cmd berührt hier `Client.Api`).

open System
open Expecto
open Types

let private idA = Guid.Parse "00000000-0000-0000-0000-00000000000a"
let private idB = Guid.Parse "00000000-0000-0000-0000-00000000000b"
let private idC = Guid.Parse "00000000-0000-0000-0000-00000000000c"
let private entry1 = Guid.Parse "0195f900-0000-7000-8000-000000000001"
let private entry2 = Guid.Parse "0195f900-0000-7000-8000-000000000002"
let private entry3 = Guid.Parse "0195f900-0000-7000-8000-000000000003"

let private previous : Shared.Domain.ReviewMarkState =
    { IsApproved = false; ClearedState = Shared.Domain.ReviewUncleared }

/// Ein Wisch „geprüft“ mit Rückgängig, wie ihn das Kassenbuch meldet (`SwipeReviewedDone`).
let private swipe (id: Guid) (entryId: Guid) (payee: string) : FeedbackMessage =
    FeedbackSlot.ofToast id $"{payee} geprüft" ToastSuccess (Some { Label = "Rückgängig"; Kind = UndoReviewMark (entryId, previous) })

let private plain (id: Guid) (text: string) : FeedbackMessage = FeedbackSlot.ofToast id text ToastSuccess None

let private restore (entryId: Guid) : Shared.Domain.ReviewMarkRestoreItem = { EntryId = entryId; Previous = previous }

let private rootModel (page: Page) : State.Model =
    let settings, _ = Components.Settings.State.init ()
    let rules, _ = Components.Rules.State.init ()
    let ledger, _ = Components.Ledger.State.init ()
    { CurrentPage = page
      Feedback = None
      Pwa = Pwa.initialState
      Settings = settings
      Fetch = Components.Fetch.Types.init
      Rules = rules
      Ledger = ledger
      Budget = Components.Budget.State.init ()
      Overview = Components.Overview.Types.init
      SettingsReturn = Page.Overview }

let private withFeedback (message: FeedbackMessage) (model: State.Model) = { model with Feedback = Some message }

/// Die sofort abgeschickten Nachrichten eines Cmd (ohne Zeitgeber).
let private dispatchedNow (cmd: Elmish.Cmd<State.Msg>) : State.Msg list =
    let mutable sent = []
    cmd |> List.iter (fun effect -> effect (fun msg -> sent <- msg :: sent))
    List.rev sent

let private mirrorSummary : Shared.Domain.MirrorRunSummary =
    { NewEntries = 1; ChangedEntries = 0; DeletedEntries = 0; ServerKnowledge = 1L }

/// Ein Lauf-Status älter als die Frische-Schwelle (ADR 0014): `MirrorStatusLoaded` startet den Lauf im Hintergrund.
let private staleMirror : Shared.Domain.MirrorRunStatus =
    { RanAt = DateTime.UtcNow - TimeSpan.FromMinutes 6.0; Outcome = Shared.Domain.MirrorRunSucceeded mirrorSummary }

/// Eine Nachricht des Kassenbuchs durch die Wurzel; was sie an den Meldungsplatz schickt, läuft gleich mit. Die
/// Folgenachrichten des Kassenbuchs selbst (Aufrufe) bleiben liegen.
let private mirrorStep (msg: Components.Ledger.Types.Msg) (model: State.Model) : State.Model =
    let after, cmd = State.update (State.LedgerMsg msg) model
    dispatchedNow cmd
    |> List.filter (fun m -> match m with State.LedgerMsg _ -> false | _ -> true)
    |> List.fold (fun m next -> fst (State.update next m)) after

[<Tests>]
let postTests =
    testList "FeedbackSlot.post" [
        testCase "Eine neue Meldung ersetzt die alte" <| fun () ->
            let posted = FeedbackSlot.post (Some (plain idA "Regel gespeichert")) (plain idB "Memo gespeichert")
            Expect.equal posted (plain idB "Memo gespeichert") "nur die neue steht"

        testCase "Ohne alte Meldung steht die neue unverändert" <| fun () ->
            Expect.equal (FeedbackSlot.post None (swipe idA entry1 "REWE")) (swipe idA entry1 "REWE") "erste Meldung"

        testCase "Gleiche Wische fassen sich zu einer Meldung zusammen, 1 → 2 → 3, mit einem Rückgängig für alle" <| fun () ->
            let one = FeedbackSlot.post None (swipe idA entry1 "REWE")
            let two = FeedbackSlot.post (Some one) (swipe idB entry2 "Bäckerei")
            Expect.equal two.Action "2 geprüft" "Text der Gruppe"
            Expect.isNone two.Detail "ohne Detail"
            Expect.equal two.Group (Some (ReviewedBySwipe, 2)) "Zahl 2"
            Expect.equal
                (two.Act |> Option.map (fun a -> a.Kind))
                (Some (UndoReviewMarkBatch [ restore entry1; restore entry2 ]))
                "beide Rückgängig-Schritte in einem"
            let three = FeedbackSlot.post (Some two) (swipe idC entry3 "Tankstelle")
            Expect.equal three.Action "3 geprüft" "Text der Gruppe"
            Expect.equal three.Group (Some (ReviewedBySwipe, 3)) "Zahl 3"
            Expect.equal
                (three.Act |> Option.map (fun a -> a.Kind))
                (Some (UndoReviewMarkBatch [ restore entry1; restore entry2; restore entry3 ]))
                "der dritte Wisch hängt an"
            Expect.equal (three.Act |> Option.map (fun a -> a.Label)) (Some "Rückgängig") "Beschriftung"

        testCase "Die zusammengefasste Meldung trägt die Id der neuen, damit Zeitgeber der alten nichts tun" <| fun () ->
            let shownOnPc = { swipe idA entry1 "REWE" with ToastHidden = true }
            let two = FeedbackSlot.post (Some shownOnPc) (swipe idB entry2 "Bäckerei")
            Expect.equal two.Id idB "Id der neuen"
            Expect.isFalse two.ToastHidden "der Toast am PC steht wieder"

        testCase "Eine andere Aktion nach einer Gruppe ersetzt sie" <| fun () ->
            let two = FeedbackSlot.post (Some (swipe idA entry1 "REWE")) (swipe idB entry2 "Bäckerei")
            let other = plain idC "Memo gespeichert"
            Expect.equal (FeedbackSlot.post (Some two) other) other "die Gruppe ist weg"

        testCase "Eine Gruppe nach einer anderen Aktion beginnt bei 1" <| fun () ->
            let posted = FeedbackSlot.post (Some (plain idA "Memo gespeichert")) (swipe idB entry1 "REWE")
            Expect.equal posted.Group (Some (ReviewedBySwipe, 1)) "Zahl 1"
            Expect.equal posted.Action "REWE geprüft" "Text des Wischs"

        testCase "Ein Wisch ohne Rückgängig (Rückschreiben gescheitert) hat keine Gruppe und ersetzt" <| fun () ->
            let failed = FeedbackSlot.ofToast idB "Prüfvermerk nicht in YNAB: Zeitüberschreitung" ToastError None
            let posted = FeedbackSlot.post (Some (swipe idA entry1 "REWE")) failed
            Expect.equal posted failed "der Fehler ersetzt"
            Expect.isNone posted.Group "keine Gruppe"
    ]

[<Tests>]
let textTests =
    testList "FeedbackSlot.splitText und ofToast" [
        testCase "Am Doppelpunkt geteilt: Aktion und Grund" <| fun () ->
            Expect.equal
                (FeedbackSlot.splitText "Prüfvermerk nicht in YNAB: Grund")
                ("Prüfvermerk nicht in YNAB", Some "Grund")
                "Doppelpunkt"

        testCase "Am Mittelpunkt geteilt: der Rest bleibt das Detail" <| fun () ->
            Expect.equal
                (FeedbackSlot.splitText "Buchung angelegt · Konto · Betrag")
                ("Buchung angelegt", Some "Konto · Betrag")
                "Mittelpunkt"

        testCase "Der erste Trenner gewinnt" <| fun () ->
            Expect.equal (FeedbackSlot.splitText "A · B: C") ("A", Some "B: C") "Mittelpunkt zuerst"
            Expect.equal (FeedbackSlot.splitText "A: B · C") ("A", Some "B · C") "Doppelpunkt zuerst"

        testCase "Ohne Trenner ist alles die Aktion" <| fun () ->
            Expect.equal (FeedbackSlot.splitText "REWE geprüft") ("REWE geprüft", None) "ganz"

        testCase "Der Ton folgt dem Toast-Typ" <| fun () ->
            let tone t = (FeedbackSlot.ofToast idA "x" t None).Tone
            Expect.equal
                [ tone ToastSuccess; tone ToastWarning; tone ToastError; tone ToastInfo ]
                [ FeedbackOk; FeedbackWarning; FeedbackError; FeedbackInfo ]
                "Ok, Warnung, Fehler, Info"

        testCase "Nur der Wisch mit Rückgängig bekommt die Gruppe" <| fun () ->
            Expect.equal (swipe idA entry1 "REWE").Group (Some (ReviewedBySwipe, 1)) "Wisch"
            let batch =
                FeedbackSlot.ofToast idA "2 geprüft" ToastSuccess (Some { Label = "Rückgängig"; Kind = UndoReviewMarkBatch [ restore entry1 ] })
            Expect.isNone batch.Group "Auswahl-Leiste: keine Gruppe"
            Expect.isNone (plain idA "Memo gespeichert").Group "ohne Aktion"

        testCase "Der Toast am PC zeigt den Text wie heute, mit seinem Trenner" <| fun () ->
            let text t = FeedbackSlot.toastText (FeedbackSlot.ofToast idA t ToastSuccess None)
            Expect.equal (text "Prüfvermerk nicht in YNAB: Grund") "Prüfvermerk nicht in YNAB: Grund" "Doppelpunkt"
            Expect.equal (text "Buchung angelegt · Konto · Betrag") "Buchung angelegt · Konto · Betrag" "Mittelpunkt"
            Expect.equal (text "REWE geprüft") "REWE geprüft" "ohne Trenner"
    ]

[<Tests>]
let rootTests =
    testList "Meldungsplatz in der Wurzel" [
        testCase "Zwei Meldungen nacheinander: es steht nur die zweite" <| fun () ->
            let first, _ = State.update (State.ShowToast ("Regel gespeichert", ToastSuccess)) (rootModel Page.Budget)
            let second, _ = State.update (State.ShowToast ("Fehler: weg", ToastError)) first
            Expect.equal (second.Feedback |> Option.map (fun m -> m.Action, m.Detail)) (Some ("Fehler", Some "weg")) "die zweite"

        testCase "ShowToastWithAction fasst Wische über post zusammen" <| fun () ->
            let action e = { Label = "Rückgängig"; Kind = UndoReviewMark (e, previous) }
            let one, _ = State.update (State.ShowToastWithAction ("REWE geprüft", ToastSuccess, action entry1)) (rootModel Page.Ledger)
            let two, _ = State.update (State.ShowToastWithAction ("Bäckerei geprüft", ToastSuccess, action entry2)) one
            Expect.equal (two.Feedback |> Option.map (fun m -> m.Action)) (Some "2 geprüft") "zusammengefasst"

        testCase "ShowFeedback setzt Aktion und Detail getrennt, ohne splitText" <| fun () ->
            let message = { plain idA "x" with Action = "Abgerufen: 3 neu"; Detail = Some "Girokonto" }
            let after, _ = State.update (State.ShowFeedback message) (rootModel Page.Ledger)
            Expect.equal after.Feedback (Some message) "unverändert im Platz"

        testCase "Läuft plant keinen Abgang des Toasts, Ok schon" <| fun () ->
            let running = { plain idA "Abrufen läuft …" with Tone = FeedbackRunning }
            let _, runningCmd = State.update (State.ShowFeedback running) (rootModel Page.Ledger)
            Expect.isEmpty runningCmd "kein Zeitgeber"
            let _, okCmd = State.update (State.ShowFeedback (plain idB "Abgerufen")) (rootModel Page.Ledger)
            Expect.equal (List.length okCmd) 1 "Zeitgeber nach 5 s"

        testCase "Der Abgang des Toasts am PC: ✕ markiert, nach 220 ms ausgeblendet, die Leiste behält die Meldung" <| fun () ->
            let model = rootModel Page.Ledger |> withFeedback (plain idA "Gespeichert")
            let closing, cmd = State.update (State.StartDismissToast idA) model
            Expect.isTrue (closing.Feedback |> Option.exists (fun m -> m.ToastExiting)) "Abgang läuft"
            CmdTiming.expectAfter Toast.exitDurationMs (State.ToastExited idA) cmd
            let hidden, _ = State.update (State.ToastExited idA) closing
            Expect.equal
                (hidden.Feedback |> Option.map (fun m -> m.Action, m.ToastHidden, m.ToastExiting))
                (Some ("Gespeichert", true, false))
                "Toast aus, Meldung bleibt für die Leiste"

        testCase "Ein doppeltes ✕ plant nichts doppelt" <| fun () ->
            let model = rootModel Page.Ledger |> withFeedback (plain idA "Gespeichert")
            let closing, _ = State.update (State.StartDismissToast idA) model
            let again, cmd = State.update (State.StartDismissToast idA) closing
            Expect.equal again closing "unverändert"
            Expect.isEmpty cmd "kein zweiter Zeitgeber"

        testCase "Der Zeitgeber einer ersetzten Meldung tut nichts" <| fun () ->
            let model = rootModel Page.Ledger |> withFeedback (plain idB "Neu")
            let dismissed, dismissCmd = State.update (State.StartDismissToast idA) model
            Expect.equal dismissed model "Abgang der alten ändert nichts"
            Expect.isEmpty dismissCmd "nichts geplant"
            let exited, _ = State.update (State.ToastExited idA) model
            Expect.equal exited model "Ausblenden der alten ändert nichts"

        testCase "Klick auf die Aktion der aktuellen Meldung leert den Platz und löst die Aktion aus" <| fun () ->
            let model = rootModel Page.Ledger |> withFeedback (swipe idA entry1 "REWE")
            let after, cmd = State.update (State.FeedbackActClicked idA) model
            Expect.isNone after.Feedback "Platz leer"
            Expect.equal (dispatchedNow cmd) [ State.toastActionMsg (UndoReviewMark (entry1, previous)) ] "Rückgängig genau einmal"
            let again, cmd2 = State.update (State.FeedbackActClicked idA) after
            Expect.equal again after "zweiter Klick ändert nichts"
            Expect.isEmpty cmd2 "zweiter Klick löst nichts aus"

        testCase "Klick auf die Aktion einer alten Id tut nichts" <| fun () ->
            let model = rootModel Page.Ledger |> withFeedback (swipe idB entry2 "Bäckerei")
            let after, cmd = State.update (State.FeedbackActClicked idA) model
            Expect.equal after model "unverändert"
            Expect.isEmpty cmd "keine Aktion"

        testCase "Ein Seitenwechsel leert den Platz" <| fun () ->
            let model = rootModel Page.Settings |> withFeedback (plain idA "Gespeichert")
            let after, _ = State.update (State.UrlChanged [ "styleguide" ]) model
            Expect.isNone after.Feedback "leer"

        testCase "Gleicher pageKey (Buchungs-Detail zurück zur Liste) behält die Meldung" <| fun () ->
            let model = rootModel (Page.LedgerEntry entry1) |> withFeedback (plain idA "Buchung gelöscht")
            let after, _ = State.update (State.UrlChanged [ "ledger" ]) model
            Expect.equal after.CurrentPage Page.Ledger "Seite gewechselt"
            Expect.equal after.Feedback model.Feedback "Meldung bleibt"

        testCase "Ersetzt eine Meldung den PWA-Hinweis, gilt er als geschlossen" <| fun () ->
            let busy = rootModel Page.Ledger |> fun m -> { m with Ledger = { m.Ledger with SortSheetOpen = true } }
            let shown, noticeCmd = State.update State.PwaUpdateWaiting busy
            Expect.isTrue shown.Pwa.NoticeShown "Hinweis steht"
            Expect.isEmpty noticeCmd "ohne Zeitlimit, auch am PC"
            let replaced, _ = State.update (State.ShowToast ("Gespeichert", ToastSuccess)) shown
            Expect.isFalse replaced.Pwa.NoticeShown "geschlossen"
            Expect.equal (replaced.Feedback |> Option.map (fun m -> m.Action)) (Some "Gespeichert") "die neue steht"

        testCase "Ein Seitenwechsel schließt den PWA-Hinweis" <| fun () ->
            let busy = rootModel Page.Settings |> fun m -> { m with Ledger = { m.Ledger with SortSheetOpen = true } }
            let shown, _ = State.update State.PwaUpdateWaiting busy
            let left, _ = State.update (State.UrlChanged [ "styleguide" ]) shown
            Expect.isFalse left.Pwa.NoticeShown "geschlossen"
            Expect.isNone left.Feedback "leer"

        // Review-Fund zu ledger-gwr9j: der Spiegellauf im Hintergrund (Vordergrund holen, `VisibilityChanged`) ersetzte
        // eine stehende Meldung mit „Rückgängig“. Die Meldung bleibt bis zur nächsten Aktion; Vordergrund holen ist keine.
        testCase "Der Spiegellauf im Hintergrund ersetzt keine Meldung mit Rückgängig, „Gespiegelt“ danach auch nicht" <| fun () ->
            let busy = rootModel Page.Ledger |> withFeedback (swipe idA entry1 "REWE")
            let afterStart = mirrorStep (Components.Ledger.Types.MirrorStatusLoaded (0, Ok (Some staleMirror))) busy
            Expect.isTrue afterStart.Ledger.MirrorRunning "der Lauf ist gestartet"
            Expect.equal afterStart.Feedback busy.Feedback "„Spiegeln läuft …“ ersetzt sie nicht"
            let afterDone = mirrorStep (Components.Ledger.Types.MirrorFinished (Ok mirrorSummary)) afterStart
            Expect.equal afterDone.Feedback busy.Feedback "„Gespiegelt“ ersetzt sie nicht"

        testCase "Ist der Platz frei oder steht eine Meldung ohne Knopf, meldet sich der Hintergrund-Lauf bis zum Ergebnis" <| fun () ->
            for start in [ rootModel Page.Ledger; rootModel Page.Ledger |> withFeedback (plain idA "Gespeichert") ] do
                let running = mirrorStep (Components.Ledger.Types.MirrorStatusLoaded (0, Ok (Some staleMirror))) start
                Expect.equal (running.Feedback |> Option.map (fun m -> m.Action)) (Some "Spiegeln läuft …") "läuft"
                let finished = mirrorStep (Components.Ledger.Types.MirrorFinished (Ok mirrorSummary)) running
                Expect.equal (finished.Feedback |> Option.map (fun m -> m.Action)) (Some "Gespiegelt") "Ergebnis"

        testCase "„Jetzt spiegeln“ ist eine Aktion und ersetzt die Meldung mit Rückgängig" <| fun () ->
            let busy = rootModel Page.Ledger |> withFeedback (swipe idA entry1 "REWE")
            let running = mirrorStep Components.Ledger.Types.StartMirror busy
            Expect.equal (running.Feedback |> Option.map (fun m -> m.Action)) (Some "Spiegeln läuft …") "läuft"
            let finished = mirrorStep (Components.Ledger.Types.MirrorFinished (Ok mirrorSummary)) running
            Expect.equal (finished.Feedback |> Option.map (fun m -> m.Action)) (Some "Gespiegelt") "Ergebnis"

        testCase "Erneut senden über die Meldung bei laufender Schlange meldet die Sperre" <| fun () ->
            // budgeting-c5w2n, Verifier-Fund Iteration 4: der Klick schloss den Toast und nichts geschah.
            let write : Components.Budget.Types.AssignWrite =
                { CategoryId = idB; Month = "2026-11-01"; Assigned = { Amount = 1m; Currency = "EUR" }
                  State = Components.Budget.Types.WriteRunning }
            let retry : Components.Budget.Types.OpenMoveRetry =
                { ApplicationId = "app-1"; Headline = "Nicht zugewiesen"; MonthLabel = "November 2026"; Summary = "s"; Reason = "r"
                  State = Components.Budget.Types.RetryReady; AssignTarget = None }
            let message = FeedbackSlot.ofToast idA "Teilweise verschoben" ToastWarning (Some { Label = "Erneut senden"; Kind = RetryBudgetMove "app-1" })
            let model =
                { rootModel Page.Budget with
                    Feedback = Some message
                    Budget = { Components.Budget.State.init () with AssignQueue = [ write ]; OpenRetries = [ retry ] } }
            let clicked, _ = State.update (State.FeedbackActClicked idA) model
            let after, cmd = State.update (State.toastActionMsg (RetryBudgetMove "app-1")) clicked
            Expect.equal after.Budget.OpenRetries [ retry ] "bleibt sendbar"
            Expect.equal (dispatchedNow cmd) [ State.ShowToast (Components.Budget.Moves.moveBlockedText, ToastWarning) ] "die Meldung nennt es"

        testCase "Die Wurzel übersetzt die Aktion in die Rückgängig-Nachricht mit genau diesem Vorzustand" <| fun () ->
            let reconciled : Shared.Domain.ReviewMarkState =
                { IsApproved = true; ClearedState = Shared.Domain.ReviewReconciled }
            match State.toastActionMsg (UndoReviewMark (idB, reconciled)) with
            | State.LedgerMsg (Components.Ledger.Types.StartUndoReviewMark (id, p)) ->
                Expect.equal id idB "Buchung"
                Expect.equal p reconciled "Vorzustand unverändert, auch Reconciled"
            | other -> failtestf "unerwartete Nachricht: %A" other
    ]
