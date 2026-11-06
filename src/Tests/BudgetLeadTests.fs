module Tests.BudgetLeadTests

// budgeting-dgrp7, ADR 0084 (Nachtrag zur Verfeinerung von qm5x9 und dgrp7): BB führt das Zugewiesen. Die Abläufe
// gegen In-Memory-SQLite mit einem Fake-YNAB als Port und der Uhr als Parameter: Übernahme, Beobachten beim Lesen
// und vor jedem Befehl, erneut senden, und die Reihenfolgen von Lesen und Senden.

open System
open System.Collections.Generic
open System.Threading

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Dapper
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.CommandHandlers
open Budgeting.Events

let private eur (milliunits: int64) : Money = { Amount = decimal milliunits / 1000m; Currency = "EUR" }

let private run x = Async.RunSynchronously x

/// Die Uhr der Tests (UTC). Früher als die Uhr der übrigen Budget-Tests: "Zuletzt verwendet" liest alle Monate,
/// neueste zuerst, und soll dort die Verschiebungen dieser Tests nicht vorn sehen.
let private now = DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc)

let private monthCounter = ref 0

/// Ein eigener Monat je Test: die Übernahme gilt je Monats-Stream, ein geteilter Monat hinge von der Reihenfolge
/// der Tests ab.
let private freshMonth () : string =
    let i = Interlocked.Increment monthCounter
    $"{4000 + i / 12}-{i % 12 + 1:D2}-01"

/// Absturz zwischen Festhalten und Senden: der PATCH-Aufruf wirft, kein Ergebnis wird angehängt.
exception private Crash

/// Fake-YNAB. `Assigned` ist der Stand in YNAB; Änderungen von außen setzt der Test direkt darin.
type private FakeYnab(categories: (Guid * string * int64) list) =
    let assigned = Dictionary<Guid, int64>()
    do
        for id, _, value in categories do assigned[id] <- value
    member val Reads = 0 with get, set
    member val FailRead = false with get, set
    member val Attempts = ResizeArray<Guid * int64>()
    member val Failures = Dictionary<Guid, YnabError>()
    /// Der PATCH landet in YNAB, die Antwort geht verloren.
    member val LostAnswers = HashSet<Guid>()
    member val CrashOnWrite = false with get, set
    member val OnWrite : Guid -> int64 -> unit = (fun _ _ -> ()) with get, set
    member val OnRead : unit -> unit = ignore with get, set
    member val Underfunded = Dictionary<Guid, int64>()
    member val Hidden = HashSet<Guid>()
    member val Deleted = HashSet<Guid>()
    member val Internal = HashSet<Guid>()
    member _.Assigned = assigned
    member this.Port : YnabPort =
        { FetchMonth =
            fun monthKey ->
                async {
                    this.Reads <- this.Reads + 1
                    this.OnRead ()
                    if this.FailRead then
                        return Error (YnabError.NetworkError "YNAB weg")
                    else
                        return
                            Ok
                                { Month = monthKey
                                  ToBeBudgeted = eur 100000L
                                  Categories =
                                    categories
                                    |> List.map (fun (id, name, _) ->
                                        { Id = id
                                          Name = name
                                          GroupName = "Alltag"
                                          Assigned = eur assigned[id]
                                          Activity = eur 0L
                                          Available = eur assigned[id]
                                          Underfunded =
                                            (match this.Underfunded.TryGetValue id with
                                             | true, v -> Some (eur v)
                                             | _ -> None)
                                          Target = None
                                          Snoozed = false
                                          Hidden = this.Hidden.Contains id
                                          Deleted = this.Deleted.Contains id
                                          Internal = this.Internal.Contains id }) }
                }
          SetAssigned =
            fun _ id milliunits ->
                async {
                    this.Attempts.Add((id, milliunits))
                    this.OnWrite id milliunits
                    if this.CrashOnWrite then raise Crash
                    match this.Failures.TryGetValue id with
                    | true, error -> return Error error
                    | _ ->
                        assigned[id] <- milliunits
                        if this.LostAnswers.Contains id then return Error (YnabError.NetworkError "Antwort verloren")
                        else return Ok ()
                }
          SetTarget = fun _ _ -> async { return failwith "schreibt kein Target" } }

let private fake () =
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    FakeYnab([ a, "Lebensmittel", 100000L; b, "Urlaub", 20000L ]), a, b

// ============================================
// Stream lesen, unabhängig von der Faltung
// ============================================

let private streamOf (monthKey: string) =
    match monthIdOf monthKey with
    | Ok monthId -> streamIdOf (MonthStream monthId)
    | Error message -> failtest message

let private recordedEvents (monthKey: string) : Budgeting.Persistence.RecordedBudgetEvent list =
    use conn = global.Persistence.getConnection ()
    match Budgeting.Persistence.loadBudgetEventsOn conn (Budgeting.Persistence.InStream (streamOf monthKey)) with
    | Ok events -> events
    | Error message -> failtest message

let private eventsOf (monthKey: string) : BudgetEvent list = recordedEvents monthKey |> List.map (fun e -> e.Event)

let private typesOf (monthKey: string) = eventsOf monthKey |> List.map eventTypeName

/// Die Beobachtungen im Stream als (Kategorie, vorher, nachher).
let private observations (monthKey: string) : (Guid * int64 * int64) list =
    eventsOf monthKey
    |> List.choose (fun e ->
        match e with
        | BudgetEvent.AssignmentChangeObservedInYnab p -> Some (p.CategoryId, p.PreviousAssigned, p.Assigned)
        | _ -> None)

let private linesOf (event: BudgetEvent) : BudgetLine list =
    match event with
    | BudgetEvent.AssignmentChangedInBb p -> [ p.Line ]
    | BudgetEvent.MoneyMoved p -> [ p.From; p.To ]
    | BudgetEvent.TargetsApplied p -> p.Lines
    | _ -> []

/// Entscheidungszeilen nach der Übernahme ohne schließendes Ergebnis (geschrieben, ersetzt, zurückgezogen), je
/// Kategorie als (Entscheidung, Wert). Abgezählt aus den Ereignissen, ohne die Faltung des Servers.
let private unclosedLines (monthKey: string) : Map<Guid, (Guid * int64) list> =
    let events = recordedEvents monthKey
    let start =
        events
        |> List.tryFindIndexBack (fun e ->
            match e.Event with
            | BudgetEvent.BudgetTakenOverFromYnab _ -> true
            | _ -> false)
        |> Option.defaultValue events.Length
    let after = events |> List.skip start
    let closed =
        after
        |> List.choose (fun e ->
            match e.Event with
            | BudgetEvent.AssignmentWrittenBackToYnab r
            | BudgetEvent.AssignmentWriteBackSupersededInYnab r
            | BudgetEvent.AssignmentWriteBackWithdrawn r -> Some (r.DecisionEventId, r.CategoryId)
            | _ -> None)
        |> Set.ofList
    after
    |> List.collect (fun e -> linesOf e.Event |> List.map (fun l -> l.CategoryId, (e.EventId, l.Assigned)))
    |> List.filter (fun (category, (decision, _)) -> not (closed.Contains (decision, category)))
    |> List.groupBy fst
    |> List.map (fun (category, rows) -> category, rows |> List.map snd)
    |> Map.ofList

let private noSave = fun (_: string) (_: Domain.UnderfundedComparison list) -> async { return () }

let private readWith (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection) (f: FakeYnab) (monthKey: string) =
    QueryHandlers.budgetMonthWith noSave connect f.Port now monthKey |> run

let private read (f: FakeYnab) (monthKey: string) : BudgetMonth =
    match readWith global.Persistence.getConnection f monthKey with
    | Ok month -> month
    | Error e -> failtest $"Monat erwartet: {e}"

let private shown (month: BudgetMonth) (id: Guid) : BudgetCategory =
    month.Groups |> List.collect (fun g -> g.Categories) |> List.find (fun c -> c.Id = id)

/// O3: Vor jedem PATCH prüft der Fake den Stream. Der gesendete Wert ist der Wert der jüngsten Entscheidung der
/// Kategorie, und nach ihr liegt keine Beobachtung derselben Kategorie (sonst wäre das Soll der YNAB-Wert).
let private watchPatches (f: FakeYnab) (monthKey: string) : ResizeArray<string> =
    let violations = ResizeArray<string>()
    f.OnWrite <-
        fun category value ->
            let events = eventsOf monthKey
            let lastDecision =
                events
                |> List.indexed
                |> List.choose (fun (i, e) ->
                    linesOf e |> List.tryFind (fun l -> l.CategoryId = category) |> Option.map (fun l -> i, l.Assigned))
                |> List.tryLast
            match lastDecision with
            | None -> violations.Add $"PATCH {value} für {category} ohne Entscheidung"
            | Some (index, decided) ->
                if decided <> value then violations.Add $"PATCH {value} für {category}, entschieden {decided}"
                let observedLater =
                    events
                    |> List.skip (index + 1)
                    |> List.exists (fun e ->
                        match e with
                        | BudgetEvent.AssignmentChangeObservedInYnab p -> p.CategoryId = category
                        | _ -> false)
                if observedLater then violations.Add $"PATCH {value} für {category} nach einer Beobachtung"
    violations

/// O1 bis O3 nach einem abschließenden Lesen. `expected` sind die externen Änderungen des Fakes, deren Endwert vom
/// Soll davor abweicht, je Szenario von Hand bestimmt.
let private checkOracles (f: FakeYnab) (monthKey: string) (violations: ResizeArray<string>) (expected: (Guid * int64 * int64) list) =
    let month = read f monthKey
    Expect.equal (observations monthKey) expected "O1: genau die abweichenden externen Änderungen sind beobachtet"
    let unclosed = unclosedLines monthKey
    for KeyValue (category, value) in f.Assigned do
        let c = shown month category
        match unclosed |> Map.tryFind category |> Option.defaultValue [] with
        | [] ->
            Expect.isNone c.OpenWriteBack "O2: keine offene Rückschreibung, keine Markierung"
            Expect.equal c.Assigned (eur value) "O2: ohne offene Rückschreibung ist das Soll der YNAB-Wert"
        | [ decision, decided ] ->
            Expect.equal c.OpenWriteBack (Some decision) "O2: die eine offene Rückschreibung ist markiert"
            Expect.equal c.Assigned (eur decided) "O2: ihr Wert ist das Soll"
        | more -> failtest $"O2: {more.Length} offene Rückschreibungen für {category}"
    Expect.isEmpty (List.ofSeq violations) "O3: kein PATCH neben dem Soll"

let private assign (f: FakeYnab) (monthKey: string) (category: Guid) (value: int64) (commandId: Guid option) =
    assignMoney f.Port now monthKey { CategoryId = category; Assigned = eur value; CommandId = commandId } |> run

let private assignOk f monthKey category value =
    match assign f monthKey category value None with
    | Ok (Ok report) -> report
    | other -> failtest $"Bericht erwartet: {other}"

let private moveOk (f: FakeYnab) (monthKey: string) (source: Guid) (target: Guid) (amount: int64) =
    match moveMoney f.Port now monthKey { Source = CategorySide source; Target = CategorySide target; Amount = eur amount; CommandId = None } |> run with
    | Ok (Ok report) -> report
    | other -> failtest $"Bericht erwartet: {other}"

let private crashes (work: unit -> unit) =
    Expect.throwsT<Crash> work "Absturz beim Senden"

let private retryOk (f: FakeYnab) (applicationId: Guid) =
    match retryFailed f.Port now applicationId |> run with
    | Ok report -> report
    | Error e -> failtest $"Bericht erwartet: {e}"

/// Die Gruppen-Id der jüngsten Entscheidung im Monat.
let private lastDecision (monthKey: string) : Guid =
    recordedEvents monthKey |> List.filter (fun e -> not (List.isEmpty (linesOf e.Event))) |> List.last |> fun e -> e.EventId

let private testCase name body =
    testCase name (fun () ->
        Budgeting.Persistence.initializeBudgetingSchema ()
        body ())

// ============================================
// Reihenfolgen von Lesen und Senden
// ============================================

let private orderTests =
    testList "Reihenfolgen von Lesen und Senden" [
        testCase "Lesen vor dem Befehl, dazwischen eine Änderung in YNAB" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            read f month |> ignore
            f.Assigned[b] <- 25000L
            assignOk f month a 120000L |> ignore
            checkOracles f month watched [ b, 20000L, 25000L ]

        testCase "Befehl vor dem Lesen" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            assignOk f month a 120000L |> ignore
            checkOracles f month watched []

        testCase "observeMonth wartet an der gehaltenen Sperre eines laufenden Befehls" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            use sending = new SemaphoreSlim(0, 1)
            use release = new SemaphoreSlim(0, 1)
            let check = f.OnWrite
            f.OnWrite <-
                fun category value ->
                    check category value
                    sending.Release() |> ignore
                    release.Wait()
            let command = Async.StartAsTask(async { return assign f month a 120000L None })
            sending.Wait()
            let readsBefore = f.Reads
            let observing = Async.StartAsTask(observeMonth f.Port now month)
            Thread.Sleep 150
            Expect.equal f.Reads readsBefore "kein FetchMonth, solange der Befehl die Sperre hält"
            Expect.isFalse observing.IsCompleted "observeMonth wartet"
            release.Release() |> ignore
            Expect.isOk command.Result "Befehl"
            Expect.isOk observing.Result "Beobachten"
            Expect.equal f.Reads (readsBefore + 1) "danach genau ein FetchMonth"
            f.OnWrite <- check
            checkOracles f month watched []

        testCase "Absturz zwischen Festhalten und Senden, dann Lesen, dann erneut senden" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.CrashOnWrite <- true
            crashes (fun () -> assign f month a 120000L None |> ignore)
            f.CrashOnWrite <- false
            let page = read f month
            Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 120000L, Some (lastDecision month)) "offen, Soll mit Markierung"
            Expect.isEmpty (observations month) "keine Beobachtung"
            let report = retryOk f (lastDecision month)
            Expect.equal (report.Lines |> List.map (fun l -> l.Outcome)) [ Written ] "erneut gesendet"
            Expect.contains (typesOf month) "AssignmentWrittenBackToYnab" "geschrieben"
            checkOracles f month watched []

        testCase "Absturz, dann eine Änderung in YNAB auf einen dritten Wert" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.CrashOnWrite <- true
            crashes (fun () -> assign f month a 120000L None |> ignore)
            f.CrashOnWrite <- false
            f.Assigned[a] <- 90000L
            let page = read f month
            Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 90000L, None) "YNAB gewinnt"
            Expect.contains (typesOf month) "AssignmentWriteBackSupersededInYnab" "ersetzt"
            checkOracles f month watched [ a, 120000L, 90000L ]

        testCase "PATCH angekommen, Antwort verloren: das Lesen sieht ihn geschrieben" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.LostAnswers.Add a |> ignore
            let report = assignOk f month a 120000L
            Expect.isTrue (report.Lines |> List.forall (fun l -> l.Outcome <> Written)) "BB meldet fehlgeschlagen"
            f.LostAnswers.Clear()
            let page = read f month
            Expect.isNone (shown page a).OpenWriteBack "geschrieben"
            Expect.equal (List.last (typesOf month)) "AssignmentWrittenBackToYnab" "per Echo geschlossen"
            checkOracles f month watched []

        testCase "Verschiebung mit gescheiterter Quelle und einer Änderung am Ziel in YNAB" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.Failures[a] <- YnabError.NetworkError "weg"
            let report = moveOk f month a b 30000L
            f.Failures.Clear()
            f.Assigned[b] <- 25000L
            let page = read f month
            Expect.equal (shown page a).OpenWriteBack (Some (Guid.Parse report.ApplicationId)) "die Quelle bleibt offen"
            f.Attempts.Clear()
            retryOk f (Guid.Parse report.ApplicationId) |> ignore
            Expect.isEmpty f.Attempts "die Quelle geht nicht hinaus, das Geld käme sonst aus Zu verteilen"
            checkOracles f month watched [ b, 50000L, 25000L ]

        testCase "Verschiebung mit geschriebener Quelle und gescheitertem Ziel" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.Failures[b] <- YnabError.NetworkError "weg"
            let report = moveOk f month a b 30000L
            f.Failures.Clear()
            let page = read f month
            Expect.equal ((shown page b).Assigned, (shown page b).OpenWriteBack) (eur 50000L, Some (Guid.Parse report.ApplicationId)) "das Ziel ist offen"
            f.Attempts.Clear()
            retryOk f (Guid.Parse report.ApplicationId) |> ignore
            Expect.equal (List.ofSeq f.Attempts) [ b, 50000L ] "nur das Ziel"
            checkOracles f month watched []

        testCase "Wiederholung mit bekannter Befehls-Id nach Absturz vor dem Senden" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            let x = Guid.NewGuid()
            f.CrashOnWrite <- true
            crashes (fun () -> assign f month a 120000L (Some x) |> ignore)
            f.CrashOnWrite <- false
            let before = typesOf month
            f.Reads <- 0
            f.Attempts.Clear()
            match assign f month a 120000L (Some x) with
            | Ok (Ok report) -> Expect.equal (report.Lines |> List.map (fun l -> l.CategoryId)) [ a ] "die festgehaltene Entscheidung"
            | other -> failtest $"Bericht erwartet: {other}"
            Expect.equal f.Reads 0 "kein YNAB-Aufruf"
            Expect.isEmpty f.Attempts "kein PATCH"
            Expect.equal (typesOf month) before "keine Beobachtung, kein Ereignis"
            checkOracles f month watched []
    ]

// ============================================
// Übernahme
// ============================================

let private takeoverTests =
    testList "Übernahme" [
        testCase "Lesen und Befehl übernehmen denselben Monat gleichzeitig: ein Übernahme-Ereignis" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            f.OnRead <- fun () -> Thread.Sleep 30
            let reading = Async.StartAsTask(async { return readWith global.Persistence.getConnection f month })
            let command = Async.StartAsTask(async { return assign f month a 120000L None })
            Expect.isOk reading.Result "Lesen"
            Expect.isOk command.Result "Befehl"
            Expect.equal (typesOf month |> List.filter ((=) "BudgetTakenOverFromYnab")) [ "BudgetTakenOverFromYnab" ] "eine Übernahme"

        testCase "Ein Monat mit Altereignissen wird übernommen, offene migrierte Rückschreibungen werden zurückgezogen" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            // Vor der Übernahme festgehalten, wie die Startmigration es hinterlässt: ohne Ergebnis und fehlgeschlagen.
            let line id name oldValue newValue : Budgeting.Persistence.PlannedLine =
                { CategoryId = id; CategoryName = name; OldAssigned = oldValue; NewAssigned = newValue }
            let pending =
                Budgeting.Persistence.recordDecision None now month MoneyAssigned [ line a "Lebensmittel" 90000L 95000L ] [] [] |> run
                |> BudgetEventsFixture.recordedOrFail
            let failed =
                Budgeting.Persistence.recordDecision None now month MoneyAssigned [ line b "Urlaub" 10000L 15000L ] [] [] |> run
                |> BudgetEventsFixture.recordedOrFail
            BudgetEventsFixture.recordResult failed.Head now (Failed "Rate-Limit")
            let page = read f month
            Expect.equal
                (typesOf month |> List.skip 3)
                [ "BudgetTakenOverFromYnab"; "AssignmentWriteBackWithdrawn"; "AssignmentWriteBackWithdrawn" ]
                "Übernahme, dann beide zurückgezogen"
            Expect.equal
                (eventsOf month
                 |> List.choose (function
                     | BudgetEvent.AssignmentWriteBackWithdrawn r -> Some r.DecisionEventId
                     | _ -> None))
                [ pending.Head.ApplicationId; failed.Head.ApplicationId ]
                "die migrierten Entscheidungen"
            Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 100000L, None) "Soll ist YNABs Stand"
            Expect.isEmpty (observations month) "keine Beobachtung"

        testCase "Die Übernahme lässt internal aus, nimmt ausgeblendete und gelöschte und trägt die Uhr des Tests" <| fun () ->
            let visible, hidden, deleted, inflow = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let f = FakeYnab([ visible, "Miete", 80000L; hidden, "Alt", 1000L; deleted, "Weg", 2000L; inflow, "Inflow", 0L ])
            f.Hidden.Add hidden |> ignore
            f.Deleted.Add deleted |> ignore
            f.Internal.Add inflow |> ignore
            let month = freshMonth ()
            read f month |> ignore
            match recordedEvents month with
            | [ e ] ->
                Expect.equal e.OccurredAt now "occurred_at ist die Uhr des Tests"
                match e.Event with
                | BudgetEvent.BudgetTakenOverFromYnab p ->
                    Expect.equal
                        (p.Categories |> List.map (fun c -> c.CategoryId, c.Assigned))
                        [ visible, 80000L; hidden, 1000L; deleted, 2000L ]
                        "ohne internal"
                | other -> failtest $"Übernahme erwartet: {other}"
            | other -> failtest $"ein Ereignis erwartet, kamen {other.Length}"

        testCase "Übernahme im selben Befehl: ein FetchMonth, keine Beobachtung, vorher ist der übernommene Wert" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            assignOk f month a 120000L |> ignore
            Expect.equal f.Reads 1 "ein FetchMonth"
            Expect.equal (typesOf month) [ "BudgetTakenOverFromYnab"; "AssignmentChangedInBb"; "AssignmentWrittenBackToYnab" ] "Ereignisse"
            match eventsOf month |> List.item 1 with
            | BudgetEvent.AssignmentChangedInBb p -> Expect.equal p.Line.PreviousAssigned 100000L "vorher übernommen"
            | other -> failtest $"Zuweisen erwartet: {other}"

        testCase "YNAB nicht erreichbar an einem Monat ohne Übernahme: alle Befehle lehnen außen ab, events bleibt leer" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            f.FailRead <- true
            let isOuterError result =
                match result with
                | Error (YnabError.NetworkError _) -> true
                | _ -> false
            Expect.isTrue (assign f month a 120000L None |> isOuterError) "Zuweisen"
            Expect.isTrue
                (moveMoney f.Port now month { Source = CategorySide a; Target = CategorySide b; Amount = eur 10000L; CommandId = None }
                 |> run
                 |> isOuterError)
                "Verschieben"
            let shownPreview : TargetPreview =
                { Month = month; Lines = []; Skipped = []; Total = eur 0L; ToBeBudgeted = eur 100000L; ToBeBudgetedAfter = eur 100000L; CommandId = None }
            Expect.isTrue (apply f.Port now month shownPreview |> run |> isOuterError) "Anwenden"
            Expect.isTrue
                (setTargetPause f.Port now month month { CategoryId = a; Intent = PauseTarget; CommandId = None } |> run |> isOuterError)
                "Pause"
            Expect.isEmpty (recordedEvents month) "events unverändert"
            Expect.isEmpty f.Attempts "nichts gesendet"
    ]

// ============================================
// Targets anwenden und Lesen
// ============================================

/// Fängt, was der Server ins Fehler-Log schreibt, während `work` läuft.
let private stderrOf (work: unit -> 'a) : 'a * string =
    let original = Console.Error
    use writer = new IO.StringWriter()
    Console.SetError writer
    try
        let result = work ()
        result, writer.ToString()
    finally
        Console.SetError original

let private readTests =
    testSequenced
    <| testList "Lesen und Targets anwenden" [
        testCase "Targets anwenden mit offener Rückschreibung auf einer Kategorie mit Fehlbetrag lehnt ab und nennt sie" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            f.Underfunded[a] <- 5000L
            f.CrashOnWrite <- true
            crashes (fun () -> assign f month a 120000L None |> ignore)
            f.CrashOnWrite <- false
            let before = typesOf month
            f.Attempts.Clear()
            let shownPreview : TargetPreview =
                { Month = month; Lines = []; Skipped = []; Total = eur 0L; ToBeBudgeted = eur 100000L; ToBeBudgetedAfter = eur 100000L; CommandId = None }
            match apply f.Port now month shownPreview |> run with
            | Error (YnabError.RefusedByBb message) ->
                Expect.stringContains message "Lebensmittel" "nennt die Kategorie"
                Expect.stringContains message "erneut senden" "Weg"
            | other -> failtest $"Ablehnung erwartet: {other}"
            match preview f.Port month |> run with
            | Error (YnabError.RefusedByBb _) -> ()
            | other -> failtest $"Vorschau abgelehnt erwartet: {other}"
            Expect.equal (typesOf month) before "nichts festgehalten"
            Expect.isEmpty f.Attempts "nichts gesendet"

        testCase "Lesen, dessen Anhängen in der Datenbank scheitert, liefert den Monat und loggt den Fehler" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            anchor.Execute("CREATE TRIGGER no_events BEFORE INSERT ON events BEGIN SELECT RAISE(ABORT, 'Platte voll'); END") |> ignore
            let f, a, _ = fake ()
            let month = freshMonth ()
            let result, log = stderrOf (fun () -> readWith connect f month)
            match result with
            | Ok page -> Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 100000L, None) "aus dem YNAB-Stand"
            | Error e -> failtest $"Monat erwartet: {e}"
            Expect.stringContains log "Platte voll" "Fehler im Log"

        testCase "Lesen, dessen Verbindungs-Port beim Anhängen wirft, liefert den Monat und loggt den Fehler" <| fun () ->
            let calls = ref 0
            let connect () =
                calls.Value <- calls.Value + 1
                if calls.Value = 1 then failwith "Verbindung weg" else global.Persistence.getConnection ()
            let f, a, _ = fake ()
            let month = freshMonth ()
            let result, log = stderrOf (fun () -> readWith connect f month)
            match result with
            | Ok page -> Expect.equal (shown page a).Assigned (eur 100000L) "aus dem YNAB-Stand"
            | Error e -> failtest $"Monat erwartet: {e}"
            Expect.stringContains log "Verbindung weg" "Fehler im Log"
            Expect.isEmpty (recordedEvents month) "nichts angehängt"

        testCase "Lesen mit scheiterndem FetchMonth scheitert wie bisher" <| fun () ->
            let f, _, _ = fake ()
            f.FailRead <- true
            Expect.equal (readWith global.Persistence.getConnection f (freshMonth ())) (Error (YnabError.NetworkError "YNAB weg")) "Fehler"

        testCase "Lesen braucht genau einen FetchMonth" <| fun () ->
            let f, _, _ = fake ()
            let month = freshMonth ()
            read f month |> ignore
            Expect.equal f.Reads 1 "erstes Lesen mit Übernahme"
            read f month |> ignore
            Expect.equal f.Reads 2 "zweites Lesen"

        testCase "Lesen bei offener Rückschreibung zeigt das Soll mit Markierung, ohne sie fehlt die Markierung" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            f.Failures[a] <- YnabError.NetworkError "weg"
            let report = assignOk f month a 120000L
            let page = read f month
            Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 120000L, Some (Guid.Parse report.ApplicationId)) "Soll mit Markierung"
            Expect.equal ((shown page a).Available, page.ToBeBudgeted) (eur 100000L, eur 100000L) "Verfügbar und Zu verteilen bleiben YNABs"
            Expect.equal ((shown page b).Assigned, (shown page b).OpenWriteBack) (eur 20000L, None) "ohne offene Rückschreibung keine Markierung"

        testCase "Denselben Wert bei offener Rückschreibung noch einmal zuweisen erzeugt nichts" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            f.Failures[a] <- YnabError.NetworkError "weg"
            assignOk f month a 120000L |> ignore
            let before = typesOf month
            f.Attempts.Clear()
            let again = assignOk f month a 120000L
            Expect.isEmpty again.Lines "kein Bericht"
            Expect.isEmpty f.Attempts "kein PATCH"
            Expect.equal (typesOf month) before "kein Ereignis"

        testCase "Eine neue Entscheidung auf offener Rückschreibung zieht die alte im selben Append zurück, die Entscheidung zuerst" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            f.Failures[a] <- YnabError.NetworkError "weg"
            let first = assignOk f month a 120000L
            f.Failures.Clear()
            let x = Guid.NewGuid()
            match assign f month a 130000L (Some x) with
            | Ok (Ok _) -> ()
            | other -> failtest $"Bericht erwartet: {other}"
            let events = recordedEvents month
            let decision = events |> List.findIndex (fun e -> e.EventId = lastDecision month)
            match events |> List.item (decision + 1) |> fun e -> e.Event with
            | BudgetEvent.AssignmentWriteBackWithdrawn r -> Expect.equal r.DecisionEventId (Guid.Parse first.ApplicationId) "die alte"
            | other -> failtest $"Zurückziehen direkt hinter der Entscheidung erwartet: {other}"
            Expect.equal (events.[decision].OccurredAt, events.[decision + 1].OccurredAt) (now, now) "ein Append"
            use conn = global.Persistence.getConnection ()
            let pointed = conn.ExecuteScalar<string>("SELECT e.event_id FROM processed_commands p JOIN events e ON e.position = p.first_position WHERE p.command_id = @id", {| id = x.ToString "D" |})
            Expect.equal pointed ((lastDecision month).ToString "D") "first_position zeigt auf die Entscheidung"
    ]

// ============================================
// Historie
// ============================================

let private historyPort = { QueryHandlers.liveHistoryPort with LoadMonthlyActivity = fun _ _ -> async { return [] } }

let private categoryEntries (monthKey: string) (category: Guid) : HistoryLine list =
    let firstDay = DateTime.ParseExact(monthKey, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
    match QueryHandlers.categoryHistory historyPort category firstDay |> run with
    | Ok history -> history.Entries |> List.map historyEntryLine |> List.rev
    | Error message -> failtest message

let private generalRuns (category: Guid) : HistoryRun list =
    match QueryHandlers.budgetHistory historyPort |> run with
    | Ok movements -> movements |> List.filter (fun m -> m.CategoryId = category) |> historyRuns |> List.rev
    | Error message -> failtest message

let private historyTests =
    testList "Historie" [
        testCase "Übernahme und Beobachtung stehen in der Historie je Kategorie und der allgemeinen, eine ersetzte Zeile heißt anders als in YNAB geändert" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            read f month |> ignore
            f.Assigned[a] <- 110000L
            read f month |> ignore
            f.Failures[a] <- YnabError.NetworkError "weg"
            assignOk f month a 120000L |> ignore
            f.Failures.Clear()
            assignOk f month a 130000L |> ignore
            let lines = categoryEntries month a
            Expect.equal
                (lines |> List.map (fun l -> l.Text, l.Change |> Option.map (fun c -> c.Amount), l.Status))
                [ "aus YNAB übernommen, 100,00 €", None, None
                  "in YNAB zugewiesen", Some 10m, None
                  "zugewiesen", Some 10m, Some HistoryReplacedInBb
                  "zugewiesen", Some 10m, Some HistoryWritten ]
                "je Kategorie, älteste zuerst"
            Expect.equal
                (generalRuns a |> List.map (fun r -> r.Text, r.Status))
                [ "Aus YNAB übernommen, 1 Kategorie", RunFromYnab
                  "In YNAB zugewiesen, Lebensmittel", RunFromYnab
                  "Zuweisen, Lebensmittel", RunReplacedInBb
                  "Zuweisen, Lebensmittel", RunWritten ]
                "allgemein, älteste zuerst"

        testCase "Eine von der Paarregel zurückgezogene Seite einer Verschiebung bleibt in YNAB geändert" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            f.Failures[a] <- YnabError.NetworkError "weg"
            let report = moveOk f month a b 30000L
            f.Failures.Clear()
            f.Assigned[b] <- 25000L
            retryOk f (Guid.Parse report.ApplicationId) |> ignore
            let source = categoryEntries month a |> List.filter (fun l -> l.Status.IsSome)
            Expect.equal (source |> List.map (fun l -> l.Status)) [ Some HistoryChangedInYnab ] "nicht ersetzt in BB"
    ]

[<Tests>]
let tests =
    testList "Budget führen: Abläufe (dgrp7)" [ orderTests; takeoverTests; readTests; historyTests ]
