module LedgerReviewMarkClientTests

// Der Prüfvermerk an der Zeile (ledger-r777c, ADR 0050), Client-Seite: der reine Reducer
// und die reinen Bausteine der Ansicht. Kein Api-Aufruf: `StartSetReviewMark` und
// `StartRetryReviewMark` setzen nur den Zustand, der Proxy steckt hinter `Run...` (ADR 0020).

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private entryView (n: int) (needsReview: bool) : LedgerEntryView = {
    Id = Guid.Parse $"0195f100-0000-7000-8000-0000000000{n:D2}"
    AccountId = Guid.Parse "0195e100-0000-7000-8000-000000000001"
    Date = DateTime(2026, 10, 1)
    Amount = eur -10m
    Payee = Some $"Payee {n}"
    Memo = None
    CategoryName = None
    CategoryId = None
    CategoryGroupName = None
    SplitCategoryGroupNames = Map.empty
    Splits = []
    Origin = MirroredFromYnab
    DeletedInYnab = false
    NeedsReview = needsReview
    Flag = NoFlag
    IsTransfer = false
    ReviewMarkWriteBackFailure = None
    CategorizationWriteBackFailure = None
    FlagWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
    SplitLocks = None
}

let private modelWith (onlyNeedsReview: bool) (entries: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success entries
        OnlyNeedsReview = onlyNeedsReview
        TotalCount = List.length entries
        TotalAmount = eur (entries |> List.sumBy (fun e -> e.Amount.Amount))
        NeedsReviewCount = entries |> List.filter (fun e -> e.NeedsReview) |> List.length }

let private update = Components.Ledger.State.update

let private outcome (change: ReviewMarkChange) (writeBack: ReviewMarkWriteBack) : ReviewMarkOutcome = {
    Previous = { IsApproved = false; ClearedState = ReviewUncleared }
    Change = change
    WriteBack = writeBack
}

let private entriesOf (model: Model) =
    match model.Entries with
    | Success entries -> entries
    | _ -> failtest "Entries sollte geladen sein"

[<Tests>]
let reviewMarkReducerTests =
    testList "Kassenbuch-Seite Prüfvermerk (Reducer)" [

        testCase "Klick auf Geprüft setzt den Zeilenzustand auf Laden" <| fun () ->
            let e = entryView 1 true
            let model, _, _ = update (StartSetReviewMark (e.Id, Reviewed)) (modelWith false [ e ])
            Expect.equal (model.ReviewMarkRows |> Map.tryFind e.Id) (Some ReviewMarkSending) "Zeile sendet"

        testCase "Erfolg ohne aktiven Chip: Zeile bleibt, ist nicht mehr zu prüfen, Zähler sinkt" <| fun () ->
            let a, b = entryView 1 true, entryView 2 true
            let start, _, _ = update (StartSetReviewMark (a.Id, Reviewed)) (modelWith false [ a; b ])
            let model, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Ok (outcome ReviewMarkChanged ReviewMarkSent))) start
            Expect.hasLength (entriesOf model) 2 "beide Zeilen bleiben"
            Expect.isFalse (entriesOf model |> List.find (fun e -> e.Id = a.Id)).NeedsReview "geprüft"
            Expect.equal model.NeedsReviewCount 1 "Zähler sinkt um eins"
            Expect.isFalse (model.ReviewMarkRows |> Map.containsKey a.Id) "Zeilenzustand zurückgesetzt"

        testCase "Erfolg bei aktivem Chip Zu prüfen: Zeile verschwindet, Zähler und Treffer sinken" <| fun () ->
            let a, b = entryView 1 true, entryView 2 true
            let start, _, _ = update (StartSetReviewMark (a.Id, Reviewed)) (modelWith true [ a; b ])
            let model, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Ok (outcome ReviewMarkChanged ReviewMarkSent))) start
            Expect.equal (entriesOf model |> List.map (fun e -> e.Id)) [ b.Id ] "die Zeile verlässt die Liste"
            Expect.equal model.NeedsReviewCount 1 "Chipzähler sinkt um eins"
            Expect.equal model.TotalCount 1 "Trefferzahl sinkt"
            Expect.equal model.TotalAmount.Amount -10m "Summe ohne die entfernte Zeile"

        testCase "Ungeprüft macht die Zeile wieder zu prüfen und hebt den Zähler" <| fun () ->
            let a = entryView 1 false
            let model, _, _ =
                update (ReviewMarkSet (a.Id, Unreviewed, Ok (outcome ReviewMarkChanged ReviewMarkSent))) (modelWith false [ a ])
            Expect.isTrue (entriesOf model |> List.head).NeedsReview "wieder zu prüfen"
            Expect.equal model.NeedsReviewCount 1 "Zähler steigt um eins"

        testCase "Fehlschlag beim Zurückschreiben: Zeile bleibt, trägt den Grund, ist geprüft" <| fun () ->
            let a = entryView 1 true
            let result = outcome ReviewMarkChanged (ReviewMarkSendFailed "Keine Verbindung zu YNAB")
            let model, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Ok result)) (modelWith true [ a ])
            let row = entriesOf model |> List.head
            Expect.equal row.ReviewMarkWriteBackFailure (Some "Keine Verbindung zu YNAB") "Grund an der Zeile"
            Expect.isFalse row.NeedsReview "in BB geprüft"
            Expect.equal model.NeedsReviewCount 0 "der Zähler folgt dem BB-Stand"

        testCase "Fehler des Befehls: Zeile bleibt unverändert und zeigt den Fehler" <| fun () ->
            let a = entryView 1 true
            let model, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Error "Die Buchung ist in YNAB gelöscht")) (modelWith false [ a ])
            Expect.isTrue (entriesOf model |> List.head).NeedsReview "unverändert"
            Expect.equal
                (model.ReviewMarkRows |> Map.tryFind a.Id)
                (Some (ReviewMarkCommandFailed "Die Buchung ist in YNAB gelöscht"))
                "Fehler an der Zeile"
            let hint = reviewMarkHintFor (model.ReviewMarkRows |> Map.tryFind a.Id) (entriesOf model |> List.head)
            Expect.equal (hint |> Option.map (fun h -> h.Reason)) (Some "Die Buchung ist in YNAB gelöscht") "Hinweis nennt den Fehler"

        testCase "Ziel gleich Ist ohne Versand lässt einen offenen Fehlschlag stehen" <| fun () ->
            let a = { entryView 1 false with ReviewMarkWriteBackFailure = Some "Timeout" }
            let model, _, _ =
                update (ReviewMarkSet (a.Id, Reviewed, Ok (outcome ReviewMarkUnchanged ReviewMarkNotSent))) (modelWith false [ a ])
            Expect.equal (entriesOf model |> List.head).ReviewMarkWriteBackFailure (Some "Timeout") "Fehlschlag bleibt"

        testCase "Erneut senden: Laden, dann Erfolg leert den Hinweis, ein neuer Fehler ersetzt den Grund" <| fun () ->
            let a = { entryView 1 false with ReviewMarkWriteBackFailure = Some "alt" }
            let start, _, _ = update (StartRetryReviewMark a.Id) (modelWith false [ a ])
            Expect.equal (start.ReviewMarkRows |> Map.tryFind a.Id) (Some ReviewMarkSending) "sendet"
            let failed, _, _ = update (ReviewMarkRetried (a.Id, Ok (ReviewMarkSendFailed "neu"))) start
            Expect.equal (entriesOf failed |> List.head).ReviewMarkWriteBackFailure (Some "neu") "neuer Grund"
            let sent, _, _ = update (ReviewMarkRetried (a.Id, Ok ReviewMarkSent)) failed
            Expect.isNone (entriesOf sent |> List.head).ReviewMarkWriteBackFailure "Hinweis weg"
            Expect.isFalse (sent.ReviewMarkRows |> Map.containsKey a.Id) "Zeilenzustand zurückgesetzt"
    ]

[<Tests>]
let reviewMarkViewTests =
    testList "Kassenbuch-Seite Prüfvermerk (Ansicht)" [

        testCase "Zeile zu prüfen zeigt Geprüft, geprüfte Zeile zeigt Ungeprüft" <| fun () ->
            let toReview = reviewMarkActionFor (entryView 1 true)
            let reviewed = reviewMarkActionFor (entryView 2 false)
            Expect.equal (toReview.Label, toReview.Target) ("Geprüft", Reviewed) "Zeile zu prüfen"
            Expect.equal (reviewed.Label, reviewed.Target) ("Ungeprüft", Unreviewed) "geprüfte Zeile"

        testCase "Zeile mit Fehlschlag zeigt den Hinweis mit Grund und Erneut senden" <| fun () ->
            let e = { entryView 1 false with ReviewMarkWriteBackFailure = Some "Keine Verbindung zu YNAB" }
            match reviewMarkHintFor None e with
            | Some hint ->
                Expect.equal hint.Title "Prüfvermerk nicht in YNAB" "Titel"
                Expect.equal hint.Reason "Keine Verbindung zu YNAB" "Grund"
                Expect.equal hint.RetryLabel (Some "Erneut senden") "Schaltfläche"
            | None -> failtest "Hinweis erwartet"

        testCase "Zeile ohne Fehlschlag zeigt keinen Hinweis" <| fun () ->
            Expect.isNone (reviewMarkHintFor None (entryView 1 true)) "kein Hinweis"

        testCase "Die Ansicht verdrahtet die Schaltfläche mit den Start-Nachrichten" <| fun () ->
            // Unter .NET lässt sich die React-Ansicht nicht rendern (Fable-only): die
            // Verdrahtung wird am Quelltext geprüft (Muster aus RuhigeListenTests).
            let rec findRoot (dir: string) =
                if File.Exists(Path.Combine(dir, "BudgetBuddy.sln")) || Directory.Exists(Path.Combine(dir, "src", "Client")) then dir
                else findRoot (Directory.GetParent(dir).FullName)
            let root = findRoot AppContext.BaseDirectory
            let source = File.ReadAllText(Path.Combine(root, "src", "Client", "Components", "Ledger", "View.fs"))
            Expect.stringContains source "StartSetReviewMark (entry.Id, action.Target)" "Klick setzt den Vermerk"
            Expect.stringContains source "StartRetryReviewMark entryId" "Erneut senden"
            Expect.stringContains source "e.stopPropagation ()" "Klick öffnet nicht die Historie"
    ]


// ============================================
// Wisch nach rechts mit Rückgängig (ledger-th36h)
// ============================================

let private swipeOutcome (previous: ReviewMarkState) : ReviewMarkOutcome =
    { Previous = previous; Change = ReviewMarkChanged; WriteBack = ReviewMarkSent }

let private uncleared = { IsApproved = false; ClearedState = ReviewUncleared }

let private dispatchedMsgs (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

[<Tests>]
let swipeReviewedTests =
    testList "Kassenbuch-Seite Wisch geprüft und Rückgängig (ledger-th36h)" [

        testCase "Wisch auf eine zu prüfende Zeile ruft setReviewMark Reviewed auf" <| fun () ->
            let e = entryView 1 true
            let model, cmd, _ = update (StartSwipeReviewed e.Id) (modelWith false [ e ])
            Expect.equal (model.ReviewMarkRows |> Map.tryFind e.Id) (Some ReviewMarkSending) "Zeile sendet"
            Expect.equal (dispatchedMsgs cmd) [ RunSwipeReviewed e.Id ] "genau ein Aufruf folgt"

        testCase "Antwort: Toast '<Payee> geprüft' mit Rückgängig und dem Vorzustand aus der Antwort" <| fun () ->
            let e = entryView 1 true
            let previous = { IsApproved = true; ClearedState = ReviewUncleared }
            let _, _, external =
                update (SwipeReviewedDone (e.Id, swipeReviewedToastText e.Payee, Ok (swipeOutcome previous))) (modelWith false [ e ])
            match external with
            | ShowToastWithAction (text, ToastSuccess, action) ->
                Expect.equal text "Payee 1 geprüft" "Toast-Text"
                Expect.equal action.Label "Rückgängig" "Aktion"
                Expect.equal action.Kind (UndoReviewMark (e.Id, previous)) "Vorzustand aus der Antwort"
            | other -> failtestf "Toast mit Aktion erwartet, war %A" other

        testCase "Rückgängig ruft restoreReviewMark mit genau diesem Vorzustand auf (approved, Uncleared)" <| fun () ->
            let e = entryView 1 true
            let previous = { IsApproved = true; ClearedState = ReviewUncleared }
            let model, cmd, _ = update (StartUndoReviewMark (e.Id, previous)) (modelWith false [ e ])
            Expect.equal (model.ReviewMarkRows |> Map.tryFind e.Id) (Some ReviewMarkSending) "Zeile sendet"
            Expect.equal (dispatchedMsgs cmd) [ RunUndoReviewMark (e.Id, previous) ] "Vorzustand unverändert"

        testCase "Rückgängig reicht einen Reconciled-Vorzustand unverändert durch, ohne ihn zu ändern" <| fun () ->
            let e = entryView 1 true
            let previous = { IsApproved = true; ClearedState = ReviewReconciled }
            let _, cmd, _ = update (StartUndoReviewMark (e.Id, previous)) (modelWith false [ e ])
            match dispatchedMsgs cmd with
            | [ RunUndoReviewMark (_, sent) ] -> Expect.equal sent previous "Reconciled bleibt, wie die Antwort ihn meldete"
            | other -> failtestf "unerwartet: %A" other

        testCase "Wisch auf eine geprüfte Zeile: kein Aufruf, kein Toast" <| fun () ->
            let e = entryView 1 false
            let before = modelWith false [ e ]
            let model, cmd, external = update (StartSwipeReviewed e.Id) before
            Expect.equal model before "nichts ändert sich"
            Expect.isEmpty (dispatchedMsgs cmd) "kein Aufruf"
            Expect.equal external NoOp "kein Toast"

        testCase "Antwort ohne Änderung erzeugt keinen Toast mit Rückgängig" <| fun () ->
            let e = entryView 1 true
            let unchanged = { swipeOutcome uncleared with Change = ReviewMarkUnchanged; WriteBack = ReviewMarkNotSent }
            let _, _, external = update (SwipeReviewedDone (e.Id, "x", Ok unchanged)) (modelWith false [ e ])
            Expect.equal external NoOp "NoChange"

        testCase "Fehlschlag beim Zurückschreiben: Toast nennt 'Prüfvermerk nicht in YNAB' mit Grund, Zeile trägt den Hinweis" <| fun () ->
            let e = entryView 1 true
            let failed = { swipeOutcome uncleared with WriteBack = ReviewMarkSendFailed "Keine Verbindung zu YNAB" }
            let model, _, external = update (SwipeReviewedDone (e.Id, "x", Ok failed)) (modelWith true [ e ])
            Expect.equal external (ShowToast ("Prüfvermerk nicht in YNAB: Keine Verbindung zu YNAB", ToastError)) "Fehler-Toast"
            Expect.equal (entriesOf model |> List.head).ReviewMarkWriteBackFailure (Some "Keine Verbindung zu YNAB") "Hinweis an der Zeile"

        testCase "Mit aktivem Chip verschwindet die Zeile nach dem Wisch, nach Rückgängig kommt sie zurück, Zähler stimmt" <| fun () ->
            let a, b, c = entryView 1 true, entryView 2 true, entryView 3 true
            let start = modelWith true [ a; b; c ]
            let swiped, _, _ = update (SwipeReviewedDone (b.Id, "x", Ok (swipeOutcome uncleared))) start
            Expect.equal (entriesOf swiped |> List.map (fun e -> e.Id)) [ a.Id; c.Id ] "Zeile verlässt die Liste"
            Expect.equal swiped.NeedsReviewCount 2 "Zähler sinkt"
            Expect.equal swiped.TotalCount 2 "Treffer sinken"
            let restored, _, _ = update (ReviewMarkRestored (b.Id, uncleared, Ok (swipeOutcome uncleared))) swiped
            Expect.equal (entriesOf restored |> List.map (fun e -> e.Id)) [ a.Id; b.Id; c.Id ] "Zeile steht wieder an ihrem Platz"
            Expect.isTrue (entriesOf restored |> List.find (fun e -> e.Id = b.Id)).NeedsReview "wieder zu prüfen"
            Expect.equal restored.NeedsReviewCount 3 "Zähler steigt wieder"
            Expect.equal restored.TotalCount 3 "Treffer steigen wieder"
            Expect.equal restored.TotalAmount.Amount start.TotalAmount.Amount "Summe wie vorher"
            Expect.isEmpty restored.SwipedAway "gemerkte Zeile verbraucht"

        testCase "Rückgängig nimmt den lokalen Seitenversatz zurück, die nächste Seite lädt die Buchung nicht doppelt" <| fun () ->
            let a, b = entryView 1 true, entryView 2 true
            let swiped, _, _ = update (SwipeReviewedDone (a.Id, "x", Ok (swipeOutcome uncleared))) (modelWith true [ a; b ])
            Expect.equal swiped.LocallyRemoved 1 "Zeile hat die Liste verlassen"
            let restored, _, _ = update (ReviewMarkRestored (a.Id, uncleared, Ok (swipeOutcome uncleared))) swiped
            Expect.equal restored.LocallyRemoved 0 "Zeile ist zurück, Versatz zurückgenommen"
            let query = Components.Ledger.State.buildPageQuery restored None 1
            Expect.equal query.LocallyRemoved 0 "die Seitenabfrage rechnet ohne Versatz"

        testCase "Ohne aktiven Chip bleibt die Zeile stehen, Rückgängig setzt sie auf zu prüfen und hebt den Zähler" <| fun () ->
            let a, b = entryView 1 true, entryView 2 true
            let swiped, _, _ = update (SwipeReviewedDone (a.Id, "x", Ok (swipeOutcome uncleared))) (modelWith false [ a; b ])
            Expect.isFalse (entriesOf swiped |> List.head).NeedsReview "geprüft"
            Expect.equal swiped.NeedsReviewCount 1 "Zähler sinkt"
            let restored, _, _ = update (ReviewMarkRestored (a.Id, uncleared, Ok (swipeOutcome uncleared))) swiped
            Expect.isTrue (entriesOf restored |> List.head).NeedsReview "wieder zu prüfen"
            Expect.equal restored.NeedsReviewCount 2 "Zähler steigt wieder"

        testCase "Rückgängig mit Vorzustand approved und Uncleared lässt die Zeile weiter zu prüfen sein" <| fun () ->
            Expect.isTrue (needsReviewOf { IsApproved = true; ClearedState = ReviewUncleared }) "Uncleared zählt als zu prüfen"
            Expect.isFalse (needsReviewOf { IsApproved = true; ClearedState = ReviewCleared }) "bestätigt ist geprüft"
            Expect.isFalse (needsReviewOf { IsApproved = true; ClearedState = ReviewReconciled }) "Reconciled ist geprüft"

        testCase "Eine neu geladene Liste vergisst die gemerkten Zeilen" <| fun () ->
            let a = entryView 1 true
            let swiped, _, _ = update (SwipeReviewedDone (a.Id, "x", Ok (swipeOutcome uncleared))) (modelWith true [ a ])
            Expect.isNonEmpty (Map.toList swiped.SwipedAway) "Zeile gemerkt"
            let reloaded, _, _ = update LoadEntries swiped
            Expect.isEmpty reloaded.SwipedAway "nach dem Neuladen nichts mehr gemerkt"
    ]

[<Tests>]
let swipeGestureTests =
    testList "Wisch-Geste (ledger-th36h)" [

        testCase "Wisch nach rechts über der Schwelle löst aus, nach links nicht" <| fun () ->
            let commit dir dx = Client.DesignSystem.Swipe.commitsSwipe dir dx 400.0 0.0
            Expect.isTrue (commit Client.DesignSystem.Swipe.SwipeRight 200.0) "rechts löst aus"
            Expect.isFalse (commit Client.DesignSystem.Swipe.SwipeRight -200.0) "links löst nichts aus"
            Expect.isFalse (commit Client.DesignSystem.Swipe.SwipeRight 50.0) "zu kurz"
            Expect.isTrue (Client.DesignSystem.Swipe.commitsSwipe Client.DesignSystem.Swipe.SwipeRight 40.0 400.0 0.9) "schneller Wisch"

        testCase "Die bisherige Wisch-Geste nach links (SyncFlow) bleibt, rechts löst dort nichts aus" <| fun () ->
            Expect.isTrue (Client.DesignSystem.Swipe.commitsSwipe Client.DesignSystem.Swipe.SwipeLeft -200.0 400.0 0.0) "links löst aus"
            Expect.isFalse (Client.DesignSystem.Swipe.commitsSwipe Client.DesignSystem.Swipe.SwipeLeft 200.0 400.0 0.0) "rechts nicht"

        testCase "Das Gummiband folgt nur der erlaubten Richtung" <| fun () ->
            Expect.equal (Client.DesignSystem.Swipe.rubberbandOffset Client.DesignSystem.Swipe.SwipeRight -80.0 400.0) 0.0 "gegen die Richtung"
            Expect.equal (Client.DesignSystem.Swipe.rubberbandOffset Client.DesignSystem.Swipe.SwipeRight 80.0 400.0) 80.0 "mit der Richtung"
            Expect.equal (Client.DesignSystem.Swipe.rubberbandOffset Client.DesignSystem.Swipe.SwipeLeft -80.0 400.0) -80.0 "links wie bisher"

        testCase "Die Handy-Liste nutzt SwipeableRowRight nur für zu prüfende Zeilen" <| fun () ->
            let rec findRoot (dir: string) =
                if Directory.Exists(Path.Combine(dir, "src", "Client")) then dir else findRoot (Directory.GetParent(dir).FullName)
            let root = findRoot AppContext.BaseDirectory
            let source = File.ReadAllText(Path.Combine(root, "src", "Client", "Components", "Ledger", "View.fs"))
            Expect.stringContains source "Swipe.SwipeableRowRight" "Geste nach rechts"
            Expect.stringContains source "dispatch (StartSwipeReviewed entry.Id)" "Geste löst StartSwipeReviewed aus"
            Expect.stringContains source "if swipeAppliesTo entry then" "nur zu prüfende Zeilen"
            Expect.isFalse (source.Contains "SwipeableRow {") "keine Geste nach links im Kassenbuch"
    ]
