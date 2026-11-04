module Tests.BudgetGeneralHistoryTests

// budgeting-h7q4v: die allgemeine Budget-Historie. Reine Regeln (Bewegungen zu Durchlauf-Zeilen, das
// Mischen der Historie je Kategorie) und die Abfrage aller Bewegungen gegen In-Memory-SQLite über das
// Record-Feld der API.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Shared.Budgeting

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }
let private run (a: Async<'T>) = Async.RunSynchronously a
let private at (hour: int) = DateTime(2026, 10, 3, hour, 0, 0, DateTimeKind.Utc)

let private movement (group: Guid) origin (name: string) counterpart oldAssigned newAssigned latest : HistoryMovement =
    { MovementId = Guid.NewGuid()
      ApplicationId = group
      OccurredAt = at 9
      Origin = origin
      Month = "2026-10-01"
      CategoryId = Guid.NewGuid()
      CategoryName = name
      OldAssigned = eur oldAssigned
      NewAssigned = eur newAssigned
      Counterpart = counterpart
      Latest = latest }

let private single (movements: HistoryMovement list) : HistoryRun =
    match historyRuns movements with
    | [ one ] -> one
    | other -> failtest $"ein Durchlauf erwartet, kamen {List.length other}"

/// Eine Verschiebung von Lebensmittel nach Urlaub, wie die Abfrage sie liefert: die zuletzt eingefügte
/// Seite (das Ziel) vorn.
let private move (amount: decimal) sourceLatest targetLatest : HistoryMovement list =
    let group = Guid.NewGuid()
    [ movement group MoneyMoved "Urlaub" (Some "Lebensmittel") 10m (10m + amount) targetLatest
      movement group MoneyMoved "Lebensmittel" (Some "Urlaub") 100m (100m - amount) sourceLatest ]

/// Targets anwenden über die Kategorien, in der Reihenfolge des Durchlaufs.
let private targets (lines: (string * decimal * LineOutcome option) list) : HistoryMovement list =
    let group = Guid.NewGuid()
    lines
    |> List.map (fun (name, amount, latest) -> movement group TargetsApplied name None 0m amount latest)
    |> List.rev

[<Tests>]
let runTests =
    testList "Allgemeine Historie: Bewegungen zu Durchlauf-Zeilen" [
        test "Zuweisen aus Zu verteilen steht mit Plus, zurück nach Zu verteilen mit Minus, ohne Einzelzeilen" {
            let up = single [ movement (Guid.NewGuid()) MoneyAssigned "Urlaub" None 0m 20m (Some Written) ]
            Expect.equal up.Text "+20,00 € Urlaub" "aus Zu verteilen"
            Expect.equal up.Origin MoneyAssigned "Art"
            Expect.isEmpty up.Lines "keine Einzelzeilen"
            let down = single [ movement (Guid.NewGuid()) MoneyAssigned "Urlaub" None 50m 30m (Some Written) ]
            Expect.equal down.Text "-20,00 € Urlaub" "zurück nach Zu verteilen"
        }
        test "Eine Verschiebung ist eine Zeile mit Betrag, Quelle und Ziel, Monat und Stand" {
            let run = single (move 20m (Some Written) (Some Written))
            Expect.equal run.Text "20,00 € Lebensmittel → Urlaub" "Text"
            Expect.equal run.Month "2026-10-01" "Monat"
            Expect.equal run.Status HistoryWritten "Stand"
            Expect.isEmpty run.Lines "keine Einzelzeilen"
        }
        test "Fehlt eine Seite der Verschiebung in der Abfrage, kommt ihr Name aus der Gegenseite" {
            let onlySource = move 20m (Some Written) (Some Written) |> List.filter (fun m -> m.CategoryName = "Lebensmittel")
            Expect.equal (single onlySource).Text "20,00 € Lebensmittel → Urlaub" "Ziel aus der Gegenseite"
        }
        test "Scheitert die Quelle einer Verschiebung, ist der Durchlauf fehlgeschlagen mit ihrem Grund" {
            let run = single (move 20m (Some (Failed "Rate-Limit")) (Some (Failed "Quelle fehlgeschlagen")))
            Expect.equal run.Status (HistoryFailed "Rate-Limit") "Grund der Quelle"
        }
        test "Targets anwenden: eine Zeile mit Kategorienzahl und Summe, aufklappbar in der Reihenfolge des Durchlaufs" {
            let run =
                single (targets [ "Miete", 800m, Some Written; "Strom", 60m, Some Written; "Urlaub", 374m, Some Written ])
            Expect.equal run.Text "Targets anwenden, 3 Kategorien, 1.234,00 €" "Text"
            Expect.equal run.Status HistoryWritten "Stand"
            Expect.equal (run.Lines |> List.map (fun l -> l.CategoryName, l.Change)) [ "Miete", eur 800m; "Strom", eur 60m; "Urlaub", eur 374m ] "Einzelzeilen"
            Expect.equal (single (targets [ "Miete", 800m, Some Written ])).Text "Targets anwenden, 1 Kategorie, 800,00 €" "Einzahl"
        }
        test "Targets anwenden mit einer fehlgeschlagenen Zeile: der Durchlauf ist fehlgeschlagen, die Zeile trägt ihren Grund" {
            let run =
                single (targets [ "Miete", 800m, Some Written; "Strom", 60m, Some (Failed "Rate-Limit"); "Urlaub", 374m, Some Written ])
            Expect.equal run.Status (HistoryFailed "1 von 3 fehlgeschlagen") "Durchlauf"
            Expect.equal
                (run.Lines |> List.map (fun l -> l.Status))
                [ HistoryWritten; HistoryFailed "Rate-Limit"; HistoryWritten ]
                "Einzelzeilen"
        }
        test "Ohne Fehlschlag gilt noch offen vor in YNAB geändert vor geschrieben" {
            let statusOf outcomes =
                (single (targets (outcomes |> List.mapi (fun i o -> $"K{i}", 10m, o)))).Status
            Expect.equal (statusOf [ Some Written; Some ChangedInYnab; None ]) HistoryOpen "offen"
            Expect.equal (statusOf [ Some Written; Some ChangedInYnab ]) HistoryChangedInYnab "in YNAB geändert"
            Expect.equal (statusOf [ Some Written; Some Written ]) HistoryWritten "geschrieben"
        }
        test "Eine Zeile je Gruppen-Id, neueste zuerst" {
            let assigned = { movement (Guid.NewGuid()) MoneyAssigned "Urlaub" None 0m 5m (Some Written) with OccurredAt = at 11 }
            let moved = move 20m (Some Written) (Some Written) |> List.map (fun m -> { m with OccurredAt = at 10 })
            let applied = targets [ "Miete", 800m, Some Written; "Strom", 60m, Some Written ]
            let runs = historyRuns (assigned :: moved @ applied)
            Expect.equal (runs |> List.map (fun r -> r.Origin)) [ MoneyAssigned; MoneyMoved; TargetsApplied ] "drei Durchläufe"
            Expect.equal (runs |> List.map (fun r -> r.RunId)) [ assigned.ApplicationId; moved.Head.ApplicationId; applied.Head.ApplicationId ] "Gruppen-Id"
        }
    ]

let private pause (time: DateTime) : HistoryEntry =
    TargetPaused
        { EventId = Guid.NewGuid()
          OccurredAt = time
          Month = "2026-10-01"
          CategoryId = Guid.NewGuid()
          CategoryName = "Urlaub" }

let private targetChange (time: DateTime) : HistoryTargetChange =
    { ChangeId = Guid.NewGuid()
      OccurredAt = time
      Month = "2026-10-01"
      CategoryId = Guid.NewGuid()
      CategoryName = "Urlaub"
      ChangeKind = TargetRemoved
      GoalBefore = Some (eur 50m)
      GoalAfter = None
      DateBefore = None
      DateAfter = None
      Latest = Some Written }

[<Tests>]
let mergeTests =
    testList "Historie je Kategorie: Bewegungen, Pausen und Target-Änderungen gemischt" [
        test "Neueste zuerst, bei gleichem Zeitpunkt die Bewegung vor der Pause und die Pause vor der Target-Änderung" {
            let m = { movement (Guid.NewGuid()) MoneyAssigned "Urlaub" None 0m 5m None with OccurredAt = at 9 }
            let p = pause (at 9)
            let c = targetChange (at 9)
            let laterChange = targetChange (at 12)
            let earlierPause = pause (at 7)
            let merged = Server.Budgeting.Domain.mergeHistory [ m ] [ p; earlierPause ] [ laterChange; c ]
            Expect.equal merged [ TargetChanged laterChange; Movement m; p; TargetChanged c; earlierPause ] "Reihenfolge"
        }
    ]

// ---------------- Abfrage gegen In-Memory-SQLite ----------------

let private budget = Server.Api.budgetApi (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")

let private stored (applicationId: Guid) origin (month: string) (name: string) (time: DateTime) (oldAssigned: int64) (newAssigned: int64) : Budgeting.Persistence.Movement =
    { Id = Guid.NewGuid()
      ApplicationId = applicationId
      OccurredAt = time
      Origin = origin
      Month = month
      CategoryId = Guid.NewGuid()
      CategoryName = name
      OldAssigned = oldAssigned
      NewAssigned = newAssigned }

[<Tests>]
let queryTests =
    // Schema-Initialisierung im Testfall (infra-sdad8), wie in `BudgetHistoryTests.fs`.
    let test name body =
        testCase name (fun () ->
            Persistence.initializeDatabase ()
            Ledger.Persistence.initializeLedgerSchema ()
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    testSequenced
    <| testList "Allgemeine Historie: Abfrage (SQLite)" [
        test "Die allgemeine Historie liefert die Bewegungen aller Monate, Kategorien und Arten, neueste zuerst, mit Gegenseite und jüngstem Ergebnis" <| fun () ->
            let time hour = DateTime(2026, 9, 14, hour, 0, 0, DateTimeKind.Utc)
            let september = stored (Guid.NewGuid()) MoneyAssigned "2026-09-01" "H7 Urlaub" (time 8) 0L 20000L
            let moveId = Guid.NewGuid()
            let moveOut = stored moveId MoneyMoved "2026-10-01" "H7 Lebensmittel" (time 9) 50000L 30000L
            let moveIn = stored moveId MoneyMoved "2026-10-01" "H7 Urlaub" (time 9) 0L 20000L
            let applied = stored (Guid.NewGuid()) TargetsApplied "2026-11-01" "H7 Miete" (time 10) 0L 800000L
            Budgeting.Persistence.appendMovements [ september; moveOut; moveIn; applied ] |> run
            Budgeting.Persistence.appendResult moveOut.Id (time 9) (Failed "Rate-Limit") |> run
            let mine = Set.ofList [ september.Id; moveOut.Id; moveIn.Id; applied.Id ]
            match budget.getBudgetHistory () |> run with
            | Error message -> failtest message
            | Ok found ->
                let own = found |> List.filter (fun m -> mine.Contains m.MovementId)
                Expect.equal (own |> List.map (fun m -> m.MovementId)) [ applied.Id; moveIn.Id; moveOut.Id; september.Id ] "alle, neueste zuerst"
                let runs = historyRuns own
                Expect.equal
                    (runs |> List.map (fun r -> r.Text, r.Status))
                    [ "Targets anwenden, 1 Kategorie, 800,00 €", HistoryOpen
                      "20,00 € H7 Lebensmittel → H7 Urlaub", HistoryFailed "Rate-Limit"
                      "+20,00 € H7 Urlaub", HistoryOpen ]
                    "Durchläufe"
    ]
