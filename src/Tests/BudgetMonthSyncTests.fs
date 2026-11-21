/// Nachziehen beim Öffnen (budgeting-ffjk9, ADR 0101 Punkt 6): `syncBudgetMonth` beobachtet einen Monat, dessen Stand
/// älter als 60 s oder veraltet ist, liest Fenster und Platzierung nach 5 min nach und liefert den Monat nur, wenn sich
/// etwas geändert hat. Ein Budget-Befehl auf M markiert die Monate danach als veraltet.
module BudgetMonthSyncTests

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.Domain
open Budgeting.Events

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private run x = Async.RunSynchronously x

let private t0 = DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc)

let private monthCounter = ref 0

/// Ein eigener Monat je Test (Jahre ab 3700): `MonthGuard` gilt prozessweit je Monat. Jeder Test bekommt drei Monate
/// Abstand, damit Vor- und Folgemonat nicht der Monat eines anderen Tests sind.
let private freshMonth () : string =
    let i = Interlocked.Add(monthCounter, 4)
    $"{3700 + i / 12}-{i % 12 + 1:D2}-01"

let private previousMonthKey (monthKey: string) : string =
    let first = DateTime.ParseExact(monthKey, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
    monthKeyOf (first.AddMonths -1)

/// YNAB als Fake mit einer Kategorie, deren Zugewiesen `assigned` ist. `SetAssigned` ändert es, jeder Aufruf steht in
/// der Liste (auch aus mehreren Threads).
let private ynabWith (a: Guid) (assigned: decimal ref) : CommandHandlers.YnabPort * ResizeArray<string> =
    let calls = ResizeArray<string>()
    let note (call: string) = lock calls (fun () -> calls.Add call)
    let monthOf key : YnabMonth =
        { Month = key
          ToBeBudgeted = eur 100m
          Categories =
            [ { Id = a
                Name = "Lebensmittel"
                GroupName = "Alltag"
                GroupId = None
                Assigned = eur assigned.Value
                Activity = eur 0m
                Available = eur assigned.Value
                Target = None
                Hidden = false
                Deleted = false
                Internal = false } ] }
    { FetchMonth = fun key -> async { note $"FetchMonth {key}"; return Ok (monthOf key) }
      SetAssigned =
        fun key _ milliunits ->
            async {
                note $"SetAssigned {key}"
                assigned.Value <- decimal milliunits / 1000m
                return Ok ()
            }
      WriteGoal = fun id _ -> async { note $"WriteGoal {id}"; return Ok () }
      FetchCategoryGoals = fun () -> async { note "FetchCategoryGoals"; return Ok [] } },
    calls

let private fetches (calls: ResizeArray<string>) : string list =
    lock calls (fun () -> calls |> Seq.filter (fun c -> c.StartsWith "FetchMonth") |> List.ofSeq)

let private noLedger : CommandHandlers.LedgerActivityPort =
    { LoadMonthlyActivity = fun _ _ -> async { return Map.empty }
      LoadFirstEntryDate = fun () -> async { return None } }

/// Ein Rahmen-Port, dessen Fenster und Platzierung mit `at` festgehalten sind. Jeder YNAB-Aufruf steht in der Liste.
let private heldFramePort (at: DateTime) : QueryHandlers.BudgetFramePort * ResizeArray<string> =
    let calls = ResizeArray<string>()
    let port =
        BudgetEventsFixture.framePortWith
            (fun () -> async { lock calls (fun () -> calls.Add "FetchMonths"); return Ok [ "3700-01-01" ] })
            (fun () -> async { lock calls (fun () -> calls.Add "FetchCategoryPlacements"); return Ok [] })
    port.HoldMonthWindow { Months = []; FetchedAt = at } |> run |> ignore
    port.HoldCategoryCatalog { Placements = []; FetchedAt = at } |> run |> ignore
    port, calls

let private sync connect port framePort (at: DateTime) key =
    QueryHandlers.syncMonthWithLedger noLedger connect port framePort at key |> run

let private observe connect port (at: DateTime) key =
    match CommandHandlers.observeMonthWith connect port at key |> run with
    | Ok _ -> ()
    | Error e -> failtest $"Beobachtung erwartet: {e}"

let private snapshotOn (conn: Microsoft.Data.Sqlite.SqliteConnection) (key: string) : YnabMonthSnapshot =
    match Budgeting.Persistence.loadYnabMonthSnapshotOn conn key with
    | Ok (Some found) -> found
    | Ok None -> failtest $"Stand für {key} erwartet"
    | Error e -> failtest $"Stand lesbar erwartet: {e}"

let private observedChanges (conn: Microsoft.Data.Sqlite.SqliteConnection) (key: string) : ObservedAssignment list =
    match Budgeting.Persistence.loadMonthEventsOn conn key with
    | Ok events ->
        events
        |> List.choose (fun (_, e) ->
            match e with
            | BudgetEvent.AssignmentChangeObservedInYnab observed -> Some observed
            | _ -> None)
    | Error e -> failtest $"Stream lesbar erwartet: {e}"

let private assignedIn (month: BudgetMonth) (id: Guid) : Money =
    (month.Groups |> List.collect (fun g -> g.Categories) |> List.find (fun c -> c.Id = id)).Assigned

let private assign connect port (at: DateTime) key (a: Guid) (amount: decimal) =
    match CommandHandlers.assignMoneyWith connect port at key { CategoryId = a; Assigned = eur amount; CommandId = None } |> run with
    | Ok (Ok _) -> ()
    | other -> failtest $"Bericht erwartet: {other}"

/// Wartet, bis ein Ablauf an der Sperre des Monats steht. Die Frist ist nur die Grenze für einen roten Test.
let private atGate (monthKey: string) : bool =
    SpinWait.SpinUntil((fun () -> CommandHandlers.MonthGuard.waitingFor monthKey = 1), TimeSpan.FromSeconds 10.)

let private domainTests =
    testList "Frische des Stands (rein)" [
        testCase "Ohne Stand, veraltet oder älter als 60 s ist das Nachziehen fällig, bis 60 s nicht" <| fun () ->
            let snapshot at validity : YnabMonthSnapshot option =
                Some { Raw = { Month = "2026-11-01"; ToBeBudgeted = eur 0m; Categories = [] }; FetchedAt = at; Validity = validity }
            let current = SnapshotValidity.Current
            Expect.isTrue (monthSyncDue t0 None) "ohne Stand"
            Expect.isFalse (monthSyncDue (t0.AddSeconds 59.) (snapshot t0 current)) "59 s"
            Expect.isFalse (monthSyncDue (t0.AddSeconds 60.) (snapshot t0 current)) "genau 60 s"
            Expect.isTrue (monthSyncDue (t0.AddSeconds 61.) (snapshot t0 current)) "61 s"
            Expect.isTrue (monthSyncDue t0 (snapshot t0 SnapshotValidity.Outdated)) "veraltet, 0 s"
        testCase "Fenster und Platzierung sind ab 5 min fällig" <| fun () ->
            Expect.isTrue (frameStateDue t0 None) "ohne Stand"
            Expect.isFalse (frameStateDue (t0.AddMinutes 5.) (Some t0)) "genau 5 min"
            Expect.isTrue (frameStateDue ((t0.AddMinutes 5.).AddSeconds 1.) (Some t0)) "5 min und 1 s"
    ]

let private handlerTests =
    testList "Nachziehen (Handler)" [
        testCase "Ein Stand von 59 s macht null Aufrufe, einer von 61 s einen FetchMonth, ein veralteter einen, gleich wie alt" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, calls = ynabWith a (ref 30m)
            observe connect port t0 key
            calls.Clear()
            let frame, frameCalls = heldFramePort t0

            Expect.equal (sync connect port frame (t0.AddSeconds 59.) key) (Ok MonthUnchanged) "59 s: unverändert"
            Expect.isEmpty (List.ofSeq calls) "59 s: null Aufrufe"
            Expect.isOk (sync connect port frame (t0.AddSeconds 61.) key) "61 s"
            Expect.equal (List.ofSeq calls) [ $"FetchMonth {key}" ] "61 s: ein FetchMonth"
            calls.Clear()

            // Der Stand ist jetzt 61 s alt gestempelt (frisch): erst die Markierung macht ihn wieder fällig.
            Budgeting.Persistence.markMonthsAfterStaleOn anchor (previousMonthKey key)
            Expect.equal (snapshotOn anchor key).Validity SnapshotValidity.Outdated "veraltet markiert"
            Expect.isOk (sync connect port frame (t0.AddSeconds 62.) key) "veraltet, 1 s alt"
            Expect.equal (List.ofSeq calls) [ $"FetchMonth {key}" ] "veraltet: ein FetchMonth"
            Expect.equal (snapshotOn anchor key).Validity SnapshotValidity.Current "das Beobachten nimmt die Markierung"
            calls.Clear()
            Expect.isOk (sync connect port frame (t0.AddSeconds 63.) key) "danach frisch"
            Expect.isEmpty (List.ofSeq calls) "danach null Aufrufe"
            Expect.isEmpty (List.ofSeq frameCalls) "der Rahmen war die ganze Zeit frisch"

        // Review-Fund ffjk9: ein unbekannter Wert in `stale` wurde still als aktuell gelesen.
        testCase "Eine unbekannte Markierung im Stand wird gemeldet und nicht als aktuell gelesen" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, calls = ynabWith a (ref 30m)
            observe connect port t0 key
            Dapper.SqlMapper.Execute(anchor, "UPDATE ynab_month_snapshot SET stale = 2 WHERE month = @month", {| month = key |}) |> ignore
            calls.Clear()
            let frame, _ = heldFramePort t0

            match Budgeting.Persistence.loadYnabMonthSnapshotOn anchor key with
            | Error reason -> Expect.stringContains reason "unbekannt: 2" "Grund mit dem Wert"
            | Ok other -> failtest $"Fehler erwartet: {other}"
            Expect.equal
                (sync connect port frame (t0.AddSeconds 10.) key)
                (Error (YnabError.LocalReadFailed Budgeting.Persistence.YnabMonthSnapshotUnreadableText))
                "das Nachziehen meldet den Stand als nicht lesbar"

        testCase "Ein Monat ohne Stand wird beim Nachziehen beobachtet und kommt als geändert" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, calls = ynabWith a (ref 30m)
            let frame, _ = heldFramePort t0
            match sync connect port frame t0 key with
            | Ok (MonthChanged month) -> Expect.equal (assignedIn month a) (eur 30m) "YNABs Wert"
            | other -> failtest $"geändert erwartet: {other}"
            Expect.equal (fetches calls) [ $"FetchMonth {key}" ] "ein FetchMonth"

        testCase "Fenster und Platzierung liest das Nachziehen erst nach 5 min nach, je einmal" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, _ = ynabWith a (ref 30m)
            let frame, frameCalls = heldFramePort t0
            sync connect port frame (t0.AddMinutes 4.) key |> ignore
            Expect.isEmpty (List.ofSeq frameCalls) "4 min: kein Aufruf"
            sync connect port frame (t0.AddMinutes 6.) key |> ignore
            Expect.equal (List.ofSeq frameCalls) [ "FetchMonths"; "FetchCategoryPlacements" ] "6 min: je einer"
            match frame.LoadMonthWindow () |> run, frame.LoadCategoryCatalog () |> run with
            | Ok (Some window), Ok (Some catalog) ->
                Expect.equal (window.Months, window.FetchedAt) ([ "3700-01-01" ], t0.AddMinutes 6.) "Fenster neu festgehalten"
                Expect.equal catalog.FetchedAt (t0.AddMinutes 6.) "Platzierung neu festgehalten"
            | other -> failtest $"Rahmen erwartet: {other}"

        testCase "Scheitert das Lesen des Rahmens, kommt der Monat trotzdem" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, _ = ynabWith a (ref 30m)
            let broken =
                BudgetEventsFixture.framePortWith
                    (fun () -> async { return Error (YnabError.NetworkError "weg") })
                    (fun () -> async { return Error (YnabError.NetworkError "weg") })
            match sync connect port broken t0 key with
            | Ok (MonthChanged _) -> ()
            | other -> failtest $"geändert erwartet: {other}"

        testCase "Unveränderter YNAB-Stand ergibt unverändert, ein anderes Zugewiesen in YNAB eine Beobachtung und YNABs Wert als Soll" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let assigned = ref 30m
            let port, _ = ynabWith a assigned
            let frame, _ = heldFramePort t0
            observe connect port t0 key

            Expect.equal (sync connect port frame (t0.AddSeconds 61.) key) (Ok MonthUnchanged) "YNAB unverändert"
            Expect.isEmpty (observedChanges anchor key) "keine Beobachtung"

            // Roman weist in YNAB 50 zu.
            assigned.Value <- 50m
            match sync connect port frame (t0.AddSeconds 122.) key with
            | Ok (MonthChanged month) ->
                Expect.equal (assignedIn month a) (eur 50m) "das Soll ist YNABs Wert"
                Expect.equal month.CopiedAt (Some (t0.AddSeconds 122.)) "Zeitpunkt des neuen Stands"
            | other -> failtest $"geändert erwartet: {other}"
            Expect.equal
                (observedChanges anchor key |> List.map (fun o -> o.CategoryId, o.PreviousAssigned, o.Assigned))
                [ a, 30_000L, 50_000L ]
                "AssignmentChangeObservedInYnab im Stream"

        testCase "Scheitert YNAB beim Nachziehen, kommt der Fehler und der Stand bleibt" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, _ = ynabWith a (ref 30m)
            let frame, _ = heldFramePort t0
            observe connect port t0 key
            let failing = { port with FetchMonth = fun _ -> async { return Error (YnabError.NetworkError "weg") } }
            Expect.isError (sync connect failing frame (t0.AddSeconds 61.) key) "Fehler"
            Expect.equal (snapshotOn anchor key).FetchedAt t0 "der alte Stand bleibt"

        // Review-Fund ffjk9: nach einem YNAB-Fehler beim Monat las das Nachziehen trotzdem Fenster und Platzierung nach,
        // auch bei 429, und kostete so zwei weitere Aufrufe gegen ein YNAB, das gerade ablehnt.
        testCase "Scheitert YNAB beim Monat, liest das Nachziehen den fälligen Rahmen nicht nach" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, _ = ynabWith a (ref 30m)
            observe connect port t0 key
            let frame, frameCalls = heldFramePort t0
            for failure in [ YnabError.RateLimitExceeded 60; YnabError.NetworkError "weg" ] do
                let failing = { port with FetchMonth = fun _ -> async { return Error failure } }
                Expect.equal (sync connect failing frame (t0.AddMinutes 6.) key) (Error failure) $"{failure}: der Fehler"
                Expect.isEmpty (List.ofSeq frameCalls) $"{failure}: kein Aufruf für den Rahmen"

        testCase "Ein Zuweisen in M markiert M+1 und M+2 als veraltet, M-1 und M nicht" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let m = freshMonth ()
            let before, after1 = previousMonthKey m, nextMonthKey m
            let after2 = nextMonthKey after1
            let port, calls = ynabWith a (ref 30m)
            for key in [ before; m; after1; after2 ] do
                observe connect port t0 key
            assign connect port (t0.AddSeconds 5.) m a 40m

            Expect.equal
                ([ before; m; after1; after2 ] |> List.map (fun key -> (snapshotOn anchor key).Validity))
                [ SnapshotValidity.Current; SnapshotValidity.Current; SnapshotValidity.Outdated; SnapshotValidity.Outdated ]
                "M-1 und M aktuell, M+1 und M+2 veraltet"

            // Der veraltete Folgemonat wird beim nächsten Öffnen beobachtet, obwohl sein Stand erst 6 s alt ist.
            calls.Clear()
            let frame, _ = heldFramePort t0
            sync connect port frame (t0.AddSeconds 6.) after1 |> ignore
            Expect.equal (fetches calls) [ $"FetchMonth {after1}" ] "ein FetchMonth für M+1"

        testCase "Nachziehen zuerst, dann Befehl: der Befehl wartet, keine falsche Beobachtung, der neuere Stand bleibt" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let assigned = ref 30m
            let port, _ = ynabWith a assigned
            let frame, _ = heldFramePort t0
            observe connect port t0 key
            let entered = TaskCompletionSource<unit>()
            let gate = TaskCompletionSource<unit>()
            let gated =
                { port with
                    FetchMonth =
                        fun k ->
                            async {
                                entered.TrySetResult() |> ignore
                                do! Async.AwaitTask gate.Task
                                return! port.FetchMonth k
                            } }
            let syncing = Async.StartAsTask (QueryHandlers.syncMonthWithLedger noLedger connect gated frame (t0.AddSeconds 61.) key)
            Expect.isTrue (entered.Task.Wait(TimeSpan.FromSeconds 10.)) "das Nachziehen liest YNAB"
            let assigning =
                Async.StartAsTask (CommandHandlers.assignMoneyWith connect port (t0.AddSeconds 62.) key { CategoryId = a; Assigned = eur 40m; CommandId = None })
            Expect.isTrue (atGate key) "der Befehl wartet an der Sperre"
            Expect.equal assigned.Value 30m "noch nichts geschrieben"
            gate.SetResult()
            Expect.isOk syncing.Result "Nachziehen"
            Expect.isOk assigning.Result "Befehl"

            Expect.isEmpty (observedChanges anchor key) "keine falsche Beobachtung"
            Expect.equal (snapshotOn anchor key).FetchedAt (t0.AddSeconds 62.) "der Stand des Befehls ist der neuere und bleibt"
            // Das nächste Nachziehen sieht YNABs 40 und BBs Soll 40: keine Beobachtung.
            match sync connect port frame (t0.AddSeconds 130.) key with
            | Ok (MonthChanged month) -> Expect.equal (assignedIn month a) (eur 40m) "Soll 40"
            | other -> failtest $"geändert erwartet: {other}"
            Expect.isEmpty (observedChanges anchor key) "auch danach keine Beobachtung"

        testCase "Befehl zuerst, dann Nachziehen: das Nachziehen wartet und liest den Stand nach dem Schreiben" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let assigned = ref 30m
            let port, calls = ynabWith a assigned
            let frame, _ = heldFramePort t0
            observe connect port t0 key
            let entered = TaskCompletionSource<unit>()
            let gate = TaskCompletionSource<unit>()
            let gated =
                { port with
                    SetAssigned =
                        fun k id milliunits ->
                            async {
                                entered.TrySetResult() |> ignore
                                do! Async.AwaitTask gate.Task
                                return! port.SetAssigned k id milliunits
                            } }
            let assigning =
                Async.StartAsTask (CommandHandlers.assignMoneyWith connect gated (t0.AddSeconds 100.) key { CategoryId = a; Assigned = eur 40m; CommandId = None })
            Expect.isTrue (entered.Task.Wait(TimeSpan.FromSeconds 10.)) "der Befehl schreibt nach YNAB"
            calls.Clear()
            let syncing = Async.StartAsTask (QueryHandlers.syncMonthWithLedger noLedger connect port frame (t0.AddSeconds 200.) key)
            Expect.isTrue (atGate key) "das Nachziehen wartet an der Sperre"
            Expect.isEmpty (fetches calls) "das Nachziehen hat YNAB noch nicht gelesen"
            gate.SetResult()
            Expect.isOk assigning.Result "Befehl"
            match syncing.Result with
            | Ok (MonthChanged month) -> Expect.equal (assignedIn month a) (eur 40m) "Soll 40 nach dem Schreiben"
            | other -> failtest $"geändert erwartet: {other}"

            Expect.isEmpty (observedChanges anchor key) "keine falsche Beobachtung"
            let held = snapshotOn anchor key
            Expect.equal (held.FetchedAt, (held.Raw.Categories |> List.map (fun c -> c.Assigned))) (t0.AddSeconds 200., [ eur 40m ]) "der neuere Stand mit YNABs 40"
    ]

let private initialize () =
    global.Persistence.initializeDatabase ()
    Ledger.Persistence.initializeLedgerSchema ()
    Budgeting.Persistence.initializeBudgetingSchema ()

let private apiTests =
    // Geteilt: die In-Memory-DB (der API-Weg liest die Datenbank der Anwendung). Isolation über eigene Monate (Jahre
    // ab 3700) und neue Kategorie-Ids.
    testSequencedGroup TestGroups.InMemoryDb
    <| testList "Nachziehen über den API-Weg" [
        // `budgetApiWith.syncBudgetMonth` → `syncBudgetMonthFor` → `QueryHandlers.syncMonth`: die Stelle, an der das
        // Nachziehen YNAB-Port, Rahmen-Port und die Datenbank der Anwendung anschließt (Budget-Seite und Übersicht).
        testCase "syncBudgetMonth aus budgetApiWith fragt YNAB bei frischem Stand nicht, ohne Stand schon" <| fun () ->
            initialize ()
            let a = Guid.NewGuid()
            let fresh, without = freshMonth (), freshMonth ()
            let port, calls = ynabWith a (ref 30m)
            CommandHandlers.observeMonth port t0 fresh |> run |> ignore
            calls.Clear()
            let frame, frameCalls = heldFramePort t0
            let setting (key: string) = async { return Some $"wert-{key}" }
            let api = Server.Api.budgetApiWith setting (fun _ _ -> port) (fun _ _ -> frame) (fun () -> t0.AddSeconds 30.) TimeZoneInfo.Utc

            Expect.equal (api.syncBudgetMonth fresh |> run) (Ok MonthUnchanged) "frisch: unverändert"
            Expect.isEmpty (List.ofSeq calls) "frisch: kein Aufruf"
            Expect.isEmpty (List.ofSeq frameCalls) "Rahmen frisch: kein Aufruf"
            match api.syncBudgetMonth without |> run with
            | Ok (MonthChanged _) -> ()
            | other -> failtest $"geändert erwartet: {other}"
            Expect.equal (fetches calls) [ $"FetchMonth {without}" ] "Gegenbeweis: ohne Stand ein FetchMonth"
    ]

// budgeting-ffjk9
[<Tests>]
let tests = testList "Nachziehen beim Öffnen (ADR 0101 Punkt 6)" [ domainTests; handlerTests; apiTests ]
