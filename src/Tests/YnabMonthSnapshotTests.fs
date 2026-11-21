/// Die Budget-Seite liest nur BB (budgeting-m5pqn, ADR 0101): der YNAB-Stand je Monat, das Lesen ohne YNAB-Aufruf
/// und die abgeleiteten Werte Verfügbar und Zu verteilen.
module YnabMonthSnapshotTests

open System
open Expecto
open Dapper
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.Domain

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private run x = Async.RunSynchronously x

let private now = DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc)

let private category (id: Guid) (name: string) (assigned: decimal) (activity: decimal) (available: decimal) : YnabMonthCategory =
    { Id = id
      Name = name
      GroupName = "Alltag"
      GroupId = None
      Assigned = eur assigned
      Activity = eur activity
      Available = eur available
      Target = None
      Hidden = false
      Deleted = false
      Internal = false }

/// Die Faltung, in der das Soll gleich YNABs Zugewiesen ist und nichts offen.
let private agreeing (snapshot: YnabMonth) : Map<Guid, AssignmentState> =
    snapshot.Categories
    |> List.map (fun c -> c.Id, { Assigned = toMilliunits c.Assigned; Confirmed = toMilliunits c.Assigned; Open = None })
    |> Map.ofList

let private withSoll (id: Guid) (milliunits: int64) (fold: Map<Guid, AssignmentState>) =
    fold |> Map.add id { (fold |> Map.tryFind id |> Option.defaultValue unassigned) with Assigned = milliunits }

let private availableIn (month: YnabMonth) (id: Guid) : Money =
    (month.Categories |> List.find (fun c -> c.Id = id)).Available

let private ynabActivity (snapshot: YnabMonth) : Map<Guid, Money> =
    snapshot.Categories |> List.map (fun c -> c.Id, c.Activity) |> Map.ofList

// ============================================
// Reine Funktionen (ADR 0101 Punkt 2 und 4)
// ============================================

let private domainTests =
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    let snapshot : YnabMonth =
        { Month = "2026-11-01"
          ToBeBudgeted = eur 200m
          Categories = [ category a "Lebensmittel" 30m -10m 45m; category b "Urlaub" 100m 0m 160m ] }
    testList "Abgeleitete Werte aus dem YNAB-Stand" [
        testProperty "Ohne offene Rückschreibung und mit Kassenbuch gleich YNAB sind Verfügbar und Zu verteilen die Werte des Stands" <|
            fun (rows: (int16 * int16 * int16) list) (toBeBudgeted: int16) ->
                // Beträge auf 10 Milliunits wie bei YNAB.
                let money (v: int16) = eur (decimal v / 100m)
                let raw : YnabMonth =
                    { Month = "2026-11-01"
                      ToBeBudgeted = money toBeBudgeted
                      Categories =
                        rows
                        |> List.map (fun (assigned, activity, available) ->
                            category (Guid.NewGuid()) "K" (decimal assigned / 100m) (decimal activity / 100m) (decimal available / 100m)) }
                let derived = bbMonthOf (Some (agreeing raw)) (ynabActivity raw) raw
                derived.ToBeBudgeted = raw.ToBeBudgeted
                && List.forall2 (fun (d: YnabMonthCategory) (r: YnabMonthCategory) -> d.Available = r.Available) derived.Categories raw.Categories

        testCase "Auch über die Seite: ohne Abweichung zeigt jede Kategorie YNABs Verfügbar und den Vortrag des Stands" <| fun () ->
            let page =
                bbMonthOf (Some (agreeing snapshot)) (ynabActivity snapshot) snapshot
                |> toBudgetMonthWith (Some (agreeing snapshot)) (Some Map.empty) Map.empty Set.empty
            let shown = page.Groups |> List.collect (fun g -> g.Categories)
            Expect.equal (shown |> List.map (fun c -> c.Id, c.Available)) [ a, eur 45m; b, eur 160m ] "YNABs Verfügbar"
            Expect.equal page.ToBeBudgeted (eur 200m) "YNABs Zu verteilen"
            Expect.equal
                (shown |> List.map (fun c -> c.Id, (availableBuildup c).CarryOver))
                [ a, eur 25m; b, eur 60m ]
                "Vortrag des Stands: 45 − 30 − (−10) und 160 − 100"

        testCase "Soll 50 bei Stand-Zugewiesen 30 ergibt Verfügbar +20 und Zu verteilen −20" <| fun () ->
            let fold = agreeing snapshot |> withSoll a 50000L
            let derived = bbMonthOf (Some fold) (ynabActivity snapshot) snapshot
            Expect.equal (availableIn derived a) (eur 65m) "45 + 20"
            Expect.equal derived.ToBeBudgeted (eur 180m) "200 − 20"
            Expect.equal (availableIn derived b) (eur 160m) "die andere Kategorie bleibt"

        testCase "Eine Verschiebung zwischen zwei Kategorien lässt Zu verteilen unverändert" <| fun () ->
            // 20 von b nach a: Soll a 50, Soll b 80.
            let fold = agreeing snapshot |> withSoll a 50000L |> withSoll b 80000L
            let derived = bbMonthOf (Some fold) (ynabActivity snapshot) snapshot
            Expect.equal derived.ToBeBudgeted (eur 200m) "unverändert"
            Expect.equal (availableIn derived a, availableIn derived b) (eur 65m, eur 140m) "Verfügbar wandert mit"

        testCase "Eine Kategorie nur in der Faltung zählt in Zu verteilen mit ihrem Soll" <| fun () ->
            let elsewhere = Guid.NewGuid()
            let fold = agreeing snapshot |> withSoll elsewhere 5000L
            Expect.equal (derivedToBeBudgeted fold snapshot) (eur 195m) "200 − 5"

        testCase "Der Vortrag ist bei jedem Soll gleich" <| fun () ->
            let carryovers =
                [ 0L; 30000L; 50000L; -12340L ]
                |> List.map (fun soll ->
                    let derived = bbMonthOf (Some (agreeing snapshot |> withSoll a soll)) (ynabActivity snapshot) snapshot
                    carryoverOf (derived.Categories |> List.find (fun c -> c.Id = a)))
            Expect.equal (List.distinct carryovers) [ eur 25m ] "45 − 30 − (−10)"

        testCase "Eine Kassenbuch-Buchung von −12 ändert Verfügbar um −12" <| fun () ->
            let before = bbMonthOf (Some (agreeing snapshot)) (ynabActivity snapshot) snapshot
            let after = bbMonthOf (Some (agreeing snapshot)) (ynabActivity snapshot |> Map.add a (eur -22m)) snapshot
            Expect.equal (availableIn after a).Amount ((availableIn before a).Amount - 12m) "−12"
            Expect.equal after.ToBeBudgeted before.ToBeBudgeted "Zu verteilen hängt nicht an der Aktivität"

        testCase "Für einen Monat vor der ersten Kassenbuch-Buchung kommt die Aktivität aus dem Stand" <| fun () ->
            let ledger = Map.ofList [ a, eur -99m ]
            Expect.equal (monthActivity (Some (DateTime(2026, 12, 3))) ledger snapshot) (Map.ofList [ a, eur -10m; b, eur 0m ]) "aus dem Stand"
            Expect.equal (monthActivity None ledger snapshot) (Map.ofList [ a, eur -10m; b, eur 0m ]) "ohne Buchung aus dem Stand"
            Expect.equal
                (monthActivity (Some (DateTime(2026, 11, 28))) ledger snapshot)
                (Map.ofList [ a, eur -99m; b, eur 0m ])
                "Gegenprobe: im Monat der ersten Buchung aus dem Kassenbuch, ohne Buchung 0"

        testCase "Ohne Faltung gelten YNABs Zugewiesen und Zu verteilen" <| fun () ->
            let derived = bbMonthOf None (ynabActivity snapshot) snapshot
            Expect.equal (derived.ToBeBudgeted, availableIn derived a) (eur 200m, eur 45m) "wie der Stand"
    ]

// ============================================
// Handler (ADR 0101 Punkt 1, 5 und 7)
// ============================================

/// Ein Port, der jeden Aufruf zählt und den Monat aus `monthOf` liefert.
let private countingPort (monthOf: string -> YnabMonth) : CommandHandlers.YnabPort * ResizeArray<string> =
    let calls = ResizeArray<string>()
    { FetchMonth = fun key -> async { calls.Add $"FetchMonth {key}"; return Ok (monthOf key) }
      SetAssigned = fun key id value -> async { calls.Add $"SetAssigned {key} {id} {value}"; return Ok () }
      WriteGoal = fun id _ -> async { calls.Add $"WriteGoal {id}"; return Ok () }
      FetchCategoryGoals = fun () -> async { calls.Add "FetchCategoryGoals"; return Ok [] } },
    calls

/// Ein Port, der bei jedem Aufruf wirft.
let private throwingPort : CommandHandlers.YnabPort =
    { FetchMonth = fun _ -> failwith "YNAB darf nicht gefragt werden"
      SetAssigned = fun _ _ _ -> failwith "YNAB darf nicht gefragt werden"
      WriteGoal = fun _ _ -> failwith "YNAB darf nicht gefragt werden"
      FetchCategoryGoals = fun () -> failwith "YNAB darf nicht gefragt werden" }

let private noLedger : CommandHandlers.LedgerActivityPort =
    { LoadMonthlyActivity = fun _ _ -> async { return Map.empty }
      LoadFirstEntryDate = fun () -> async { return None } }

let private monthCounter = ref 0

/// Ein eigener Monat je Test: `MonthGuard` gilt prozessweit je Monat.
let private freshMonth () : string =
    let i = Threading.Interlocked.Increment monthCounter
    $"{3000 + i / 12}-{i % 12 + 1:D2}-01"

let private monthWith (categories: YnabMonthCategory list) (key: string) : YnabMonth =
    { Month = key; ToBeBudgeted = eur 100m; Categories = categories }

let private read ledger connect port key =
    QueryHandlers.budgetMonthWithLedger ledger connect port now key |> run

let private shownIn (result: YnabResult<BudgetMonth>) (id: Guid) : BudgetCategory =
    match result with
    | Ok m -> m.Groups |> List.collect (fun g -> g.Categories) |> List.find (fun c -> c.Id = id)
    | Error e -> failtest $"Monat erwartet: {e}"

let private snapshotOn (conn: Microsoft.Data.Sqlite.SqliteConnection) (key: string) : YnabMonthSnapshot option =
    match Budgeting.Persistence.loadYnabMonthSnapshotOn conn key with
    | Ok found -> found
    | Error e -> failtest $"Stand lesbar erwartet: {e}"

let private handlerTests =
    testList "Lesen aus dem YNAB-Stand (Handler)" [
        testCase "Ein Monat mit Stand macht null YNAB-Aufrufe und kommt auch, wenn jeder Aufruf wirft" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, calls = countingPort (monthWith [ category a "Lebensmittel" 30m -10m 45m ])
            match CommandHandlers.observeMonthWith connect port now key |> run with
            | Ok _ -> ()
            | Error e -> failtest $"Beobachtung erwartet: {e}"
            calls.Clear()

            let counted = read noLedger connect port key
            Expect.isEmpty calls "null Aufrufe"
            Expect.equal (shownIn counted a).Available (eur 45m) "Verfügbar aus Stand und Soll"

            let thrown = read noLedger connect throwingPort key
            Expect.equal ((shownIn thrown a).Assigned, (shownIn thrown a).Available) (eur 30m, eur 45m) "der Monat kommt"
            match thrown with
            | Ok m -> Expect.equal m.CopiedAt (Some now) "Zeitpunkt des Stands"
            | Error _ -> ()

        testCase "Ein Monat ohne Stand wird genau einmal beobachtet, das zweite Lesen macht null Aufrufe" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, calls = countingPort (monthWith [ category a "Lebensmittel" 30m 0m 30m ])

            Expect.isOk (read noLedger connect port key) "erstes Lesen"
            Expect.equal (List.ofSeq calls) [ $"FetchMonth {key}" ] "ein FetchMonth"
            Expect.isSome (snapshotOn anchor key) "der Stand steht"
            calls.Clear()
            Expect.isOk (read noLedger connect port key) "zweites Lesen"
            Expect.isEmpty calls "null Aufrufe"

        testCase "Nach dem Beobachten und nach einem Zuweisen steht der Stand in der Datenbank" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let observedMonth, assignedMonth = freshMonth (), freshMonth ()
            let raw key = monthWith [ category a "Lebensmittel" 30m -10m 45m ] key
            let port, _ = countingPort raw

            CommandHandlers.observeMonthWith connect port now observedMonth |> run |> ignore
            Expect.equal (snapshotOn anchor observedMonth) (Some { Raw = raw observedMonth; FetchedAt = now }) "nach dem Beobachten"

            Expect.isNone (snapshotOn anchor assignedMonth) "vorher kein Stand"
            match CommandHandlers.assignMoneyWith connect port now assignedMonth { CategoryId = a; Assigned = eur 40m; CommandId = None } |> run with
            | Ok (Ok _) -> ()
            | other -> failtest $"Bericht erwartet: {other}"
            Expect.equal (snapshotOn anchor assignedMonth) (Some { Raw = raw assignedMonth; FetchedAt = now }) "nach dem Zuweisen, gelesen vor dem PATCH"

        testCase "Scheitert das Festhalten des Stands, wird auch keine Beobachtung geschrieben" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            anchor.Execute("CREATE TRIGGER no_snapshot BEFORE INSERT ON ynab_month_snapshot BEGIN SELECT RAISE(ABORT, 'Platte voll'); END")
            |> ignore
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, _ = countingPort (monthWith [ category a "Lebensmittel" 30m 0m 30m ])

            match CommandHandlers.observeMonthWith connect port now key |> run with
            | Error (CommandHandlers.NotRecorded (_, reason)) -> Expect.stringContains reason "Platte voll" "Grund"
            | other -> failtest $"NotRecorded erwartet: {other}"
            Expect.equal (Budgeting.Persistence.loadMonthEventsOn anchor key) (Ok []) "keine Übernahme, keine Beobachtung"
            Expect.isNone (snapshotOn anchor key) "kein Stand"

            // Gegenprobe: ohne Sperre stehen beide.
            anchor.Execute("DROP TRIGGER no_snapshot") |> ignore
            CommandHandlers.observeMonthWith connect port now key |> run |> ignore
            Expect.isNonEmpty (Budgeting.Persistence.loadMonthEventsOn anchor key |> Result.defaultValue []) "Übernahme steht"
            Expect.isSome (snapshotOn anchor key) "Stand steht"

        testCase "Lässt sich der Stand beim ersten Lesen nicht festhalten, kommt der Monat ohne Zeitpunkt des Stands" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            anchor.Execute("CREATE TRIGGER no_snapshot BEFORE INSERT ON ynab_month_snapshot BEGIN SELECT RAISE(ABORT, 'Platte voll'); END")
            |> ignore
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, _ = countingPort (monthWith [ category a "Lebensmittel" 30m 0m 30m ])

            match read noLedger connect port key with
            | Ok m -> Expect.isNone m.CopiedAt "kein Stand gespeichert, also kein Zeitpunkt"
            | Error e -> failtest $"Monat erwartet: {e}"

            // Gegenprobe: mit festgehaltenem Stand trägt der Monat dessen Zeitpunkt.
            anchor.Execute("DROP TRIGGER no_snapshot") |> ignore
            match read noLedger connect port key with
            | Ok m -> Expect.equal m.CopiedAt (Some now) "Zeitpunkt des Stands"
            | Error e -> failtest $"Monat erwartet: {e}"

        testCase "Der Fehlbetrag rechnet mit dem Vortrag aus dem Stand" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            // Auffüllen bis 100: Fehlbetrag = 100 − (Vortrag + Soll).
            let refill : Target =
                { Goal = eur 100m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = Some false; Day = None; CreatedIn = None }
            match CommandHandlers.takeOverTargetsWith connect (fun () -> async { return Ok [ { Id = a; Name = "Rücklage"; Target = Some refill } ] }) now |> run with
            | Ok report -> Expect.equal report.TakenOver [ a ] "Target übernommen"
            | Error e -> failtest $"Übernahme erwartet: {e}"
            // Vortrag 30 (Verfügbar 30, nichts zugewiesen, keine Aktivität).
            let port, _ = countingPort (monthWith [ { category a "Rücklage" 0m 0m 30m with Target = Some refill } ])
            CommandHandlers.observeMonthWith connect port now key |> run |> ignore
            // Eine Ausgabe im Kassenbuch ändert Verfügbar, nicht den Vortrag.
            let ledger : CommandHandlers.LedgerActivityPort =
                { LoadMonthlyActivity =
                    fun ids months -> async { return ids |> List.map (fun id -> id, months |> List.map (fun m -> m, eur -10m)) |> Map.ofList }
                  LoadFirstEntryDate = fun () -> async { return Some (DateTime(2000, 1, 1)) } }

            let row = shownIn (read ledger connect throwingPort key) a

            Expect.equal row.Underfunded (Some (eur 70m)) "100 − (30 + 0)"
            Expect.equal (row.Activity, row.Available) (eur -10m, eur 20m) "Aktivität aus dem Kassenbuch, Verfügbar 30 + 0 − 10"

        // Regression (budgeting-m5pqn, Iteration 3): seit die Aktivität der Seite aus dem Kassenbuch kommt, verglich
        // der Hinweis „Abweichung Kassenbuch/YNAB“ Kassenbuch mit Kassenbuch und verschwand. Die Seite trägt deshalb
        // YNABs Aktivität aus dem Stand mit.
        testCase "Die Seite trägt neben der Aktivität aus dem Kassenbuch YNABs Aktivität aus dem Stand" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let a = Guid.NewGuid()
            let key = freshMonth ()
            let port, _ = countingPort (monthWith [ category a "Lebensmittel" 30m -10m 45m ])
            let ledger : CommandHandlers.LedgerActivityPort =
                { LoadMonthlyActivity =
                    fun ids months -> async { return ids |> List.map (fun id -> id, months |> List.map (fun m -> m, eur -12m)) |> Map.ofList }
                  LoadFirstEntryDate = fun () -> async { return Some (DateTime(2000, 1, 1)) } }

            // Erstes Lesen ohne Stand: YNABs Aktivität aus dem gerade gelesenen Monat.
            let first = shownIn (read ledger connect port key) a
            Expect.equal (first.Activity, first.ActivityInYnab) (eur -12m, Some (eur -10m)) "erstes Lesen"

            // Lesen mit Stand, ohne YNAB.
            let fromSnapshot = shownIn (read ledger connect throwingPort key) a
            Expect.equal (fromSnapshot.Activity, fromSnapshot.ActivityInYnab) (eur -12m, Some (eur -10m)) "aus dem Stand"
    ]

// ============================================
// Live-Port-Stellen (Doktrin Punkt 3): `Api.budgetMonthFor`, `budgetApiWith`, die Übersicht
// ============================================

/// Ein Port, der zählt und dann wirft.
let private countingThrowingPort () : CommandHandlers.YnabPort * int ref =
    let calls = ref 0
    let fail () =
        calls.Value <- calls.Value + 1
        failwith "YNAB darf nicht gefragt werden"
    { FetchMonth = fun _ -> fail ()
      SetAssigned = fun _ _ _ -> fail ()
      WriteGoal = fun _ _ -> fail ()
      FetchCategoryGoals = fun () -> fail () },
    calls

/// Schema wie beim Start: der API-Weg liest auch das Kassenbuch.
let private initialize () =
    global.Persistence.initializeDatabase ()
    Ledger.Persistence.initializeLedgerSchema ()
    Budgeting.Persistence.initializeBudgetingSchema ()

let private apiTests =
    // Geteilt: die In-Memory-DB (der API-Weg liest die Datenbank der Anwendung). Isolation über eigene Monate (Jahre
    // ab 3500) und neue Kategorie-Ids.
    testSequencedGroup TestGroups.InMemoryDb
    <| testList "Lesen aus dem YNAB-Stand über den API-Weg" [
        // `Api.budgetMonthFor`: die einzige Stelle, an der getBudgetMonth den YNAB-Port anschließt.
        testCase "budgetMonthFor liest einen Monat mit Stand ohne YNAB, einen ohne Stand mit YNAB" <| fun () ->
            initialize ()
            let a = Guid.NewGuid()
            let withSnapshot, without = "3500-01-01", "3500-02-01"
            let port, _ = countingPort (monthWith [ category a "Lebensmittel" 30m 0m 30m ])
            CommandHandlers.observeMonth port now withSnapshot |> run |> ignore
            let throwing, calls = countingThrowingPort ()
            let portFor _ _ = throwing

            match Server.Api.budgetMonthFor (Some "t") (Some "b") withSnapshot portFor now |> run with
            | Ok m -> Expect.equal (m.Groups |> List.collect (fun g -> g.Categories) |> List.map (fun c -> c.Id)) [ a ] "aus dem Stand"
            | Error e -> failtest e
            Expect.equal calls.Value 0 "kein Aufruf"

            // Gegenbeweis: derselbe Port wird für einen Monat ohne Stand gefragt.
            Expect.throws (fun () -> Server.Api.budgetMonthFor (Some "t") (Some "b") without portFor now |> run |> ignore) "ohne Stand fragt es YNAB"
            Expect.equal calls.Value 1 "ein Aufruf"

        // `budgetApiWith.getBudgetMonth`: der Endpoint der Budget-Seite und der Übersicht (`Overview/State.fs` ruft
        // denselben `Api.budget.getBudgetMonth`).
        testCase "getBudgetMonth aus budgetApiWith liest einen Monat mit Stand ohne YNAB" <| fun () ->
            initialize ()
            let a = Guid.NewGuid()
            let withSnapshot, without = "3500-03-01", "3500-04-01"
            let port, _ = countingPort (monthWith [ category a "Lebensmittel" 30m 0m 30m ])
            CommandHandlers.observeMonth port now withSnapshot |> run |> ignore
            let throwing, calls = countingThrowingPort ()
            let setting (key: string) = async { return Some $"wert-{key}" }
            let framePortFor _ _ : QueryHandlers.BudgetFramePort =
                BudgetEventsFixture.framePortWith (fun () -> failwith "kein Rahmen") (fun () -> failwith "kein Rahmen")
            let api = Server.Api.budgetApiWith setting (fun _ _ -> throwing) framePortFor (fun () -> now) TimeZoneInfo.Utc

            Expect.isOk (api.getBudgetMonth withSnapshot |> run) "aus dem Stand"
            Expect.equal calls.Value 0 "kein Aufruf"
            Expect.throws (fun () -> api.getBudgetMonth without |> run |> ignore) "Gegenbeweis: ohne Stand fragt es YNAB"
            Expect.equal calls.Value 1 "ein Aufruf"
    ]

// budgeting-m5pqn
[<Tests>]
let tests = testList "YNAB-Stand und Budget aus BB" [ domainTests; handlerTests; apiTests ]
