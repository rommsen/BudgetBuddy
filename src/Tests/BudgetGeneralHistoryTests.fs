module Tests.BudgetGeneralHistoryTests

// budgeting-h7q4v: die allgemeine Budget-Historie. Reine Regeln (Bewegungen zu Durchlauf-Zeilen) und die
// Abfrage aller Bewegungen gegen In-Memory-SQLite über das Record-Feld der API. Das Mischen der Historie
// je Kategorie steht in `BudgetHistoryTests.fs`.

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
        test "Zuweisen steht mit Art und Kategorie, der Betrag mit Vorzeichen getrennt, ohne Einzelzeilen" {
            let up = single [ movement (Guid.NewGuid()) MoneyAssigned "Urlaub" None 0m 20m (Some Written) ]
            Expect.equal (up.Text, up.Amount) ("Zuweisen, Urlaub", eur 20m) "aus Zu verteilen"
            Expect.equal up.Origin MoneyAssigned "Art"
            Expect.isEmpty up.Lines "keine Einzelzeilen"
            let down = single [ movement (Guid.NewGuid()) MoneyAssigned "Urlaub" None 50m 30m (Some Written) ]
            Expect.equal down.Amount (eur -20m) "zurück nach Zu verteilen"
        }
        test "Eine Verschiebung ist eine Zeile mit Quelle und Ziel, dem Betrag ohne Vorzeichen, Monat und Stand" {
            let run = single (move 20m (Some Written) (Some Written))
            Expect.equal (run.Text, run.Amount) ("Verschieben, Lebensmittel → Urlaub", eur 20m) "Text und Betrag"
            Expect.equal run.Month "2026-10-01" "Monat"
            Expect.equal run.Status RunWritten "Stand"
            Expect.isEmpty run.Lines "keine Einzelzeilen"
        }
        test "Fehlt eine Seite der Verschiebung in der Abfrage, kommt ihr Name aus der Gegenseite" {
            let onlySource = move 20m (Some Written) (Some Written) |> List.filter (fun m -> m.CategoryName = "Lebensmittel")
            Expect.equal (single onlySource).Text "Verschieben, Lebensmittel → Urlaub" "Ziel aus der Gegenseite"
        }
        // budgeting-c6vn4 (A7): verhindert den stillen Platzhalter "?" für eine Seite ohne Namen.
        test "Fehlt eine Seite und kennt die andere keine Gegenseite, heißt sie unbekannte Kategorie" {
            let lonely = [ movement (Guid.NewGuid()) MoneyMoved "Urlaub" None 10m 30m (Some Written) ]
            Expect.equal (single lonely).Text "Verschieben, unbekannte Kategorie → Urlaub" "Quelle unbekannt"
        }
        test "Scheitert die Quelle einer Verschiebung, ist der Durchlauf fehlgeschlagen mit ihrem Grund" {
            let run = single (move 20m (Some (Failed "Rate-Limit")) (Some (Failed "Quelle fehlgeschlagen")))
            Expect.equal run.Status (RunFailed "Rate-Limit") "Grund der Quelle"
        }
        test "Targets anwenden: eine Zeile mit Kategorienzahl, die Summe als Betrag, aufklappbar in der Reihenfolge des Durchlaufs" {
            let run =
                single (targets [ "Miete", 800m, Some Written; "Strom", 60m, Some Written; "Urlaub", 374m, Some Written ])
            Expect.equal (run.Text, run.Amount) ("Targets anwenden, 3 Kategorien", eur 1234m) "Text und Summe"
            Expect.equal run.Status RunWritten "Stand"
            Expect.equal (run.Lines |> List.map (fun l -> l.CategoryName, l.Change)) [ "Miete", eur 800m; "Strom", eur 60m; "Urlaub", eur 374m ] "Einzelzeilen"
            Expect.equal (single (targets [ "Miete", 800m, Some Written ])).Text "Targets anwenden, 1 Kategorie" "Einzahl"
        }
        test "Targets anwenden mit einer fehlgeschlagenen von drei Zeilen: teilweise fehlgeschlagen, die Zeile trägt ihren Grund" {
            let run =
                single (targets [ "Miete", 800m, Some Written; "Strom", 60m, Some (Failed "Rate-Limit"); "Urlaub", 374m, Some Written ])
            Expect.equal run.Status (RunPartlyFailed (1, 3)) "Durchlauf"
            Expect.equal
                (run.Lines |> List.map (fun l -> l.Status))
                [ HistoryWritten; HistoryFailed "Rate-Limit"; HistoryWritten ]
                "Einzelzeilen"
        }
        // budgeting-b5n8q: scheitern alle Kategorien, hieß der Durchlauf "3 von 3" teilweise fehlgeschlagen.
        test "Targets anwenden mit drei von drei fehlgeschlagenen Zeilen ist gescheitert mit dem ersten Grund" {
            let run =
                single (targets [ "Miete", 800m, Some (Failed "Rate-Limit"); "Strom", 60m, Some (Failed "Netzwerkfehler"); "Urlaub", 374m, Some (Failed "Netzwerkfehler") ])
            Expect.equal run.Status (RunFailed "Rate-Limit") "Durchlauf"
            Expect.equal
                (run.Lines |> List.map (fun l -> l.Status))
                [ HistoryFailed "Rate-Limit"; HistoryFailed "Netzwerkfehler"; HistoryFailed "Netzwerkfehler" ]
                "die Gründe je Kategorie bleiben in den Einzelzeilen"
        }
        // budgeting-c6vn4 (Q3): eine einzige Kategorie nennt ihren Grund, nicht "1 von 1".
        test "Targets anwenden mit einer einzigen, fehlgeschlagenen Kategorie zeigt deren Grund" {
            let run = single (targets [ "Strom", 60m, Some (Failed "Rate-Limit") ])
            Expect.equal run.Status (RunFailed "Rate-Limit") "Grund statt Zahl"
        }
        test "Ohne Fehlschlag gilt noch offen vor in YNAB geändert vor geschrieben" {
            let statusOf outcomes =
                (single (targets (outcomes |> List.mapi (fun i o -> $"K{i}", 10m, o)))).Status
            Expect.equal (statusOf [ Some Written; Some ChangedInYnab; None ]) RunOpen "offen"
            Expect.equal (statusOf [ Some Written; Some ChangedInYnab ]) RunChangedInYnab "in YNAB geändert"
            Expect.equal (statusOf [ Some Written; Some Written ]) RunWritten "geschrieben"
        }
        // budgeting-c6vn4 (M2): die Eingabe kommt älteste zuerst, damit das Sortieren in `historyRuns` und der
        // jüngste Zeitpunkt je Durchlauf (`List.max`) wirklich geprüft sind.
        test "Eine Zeile je Gruppen-Id, neueste zuerst, ein Durchlauf zählt mit seiner jüngsten Bewegung" {
            let assigned = { movement (Guid.NewGuid()) MoneyAssigned "Urlaub" None 0m 5m (Some Written) with OccurredAt = at 11 }
            let moved = move 20m (Some Written) (Some Written) |> List.map (fun m -> { m with OccurredAt = at 10 })
            let applied =
                match targets [ "Miete", 800m, Some Written; "Strom", 60m, Some Written ] with
                | [ later; earlier ] -> [ { later with OccurredAt = at 12 }; { earlier with OccurredAt = at 8 } ]
                | other -> failtest $"zwei Zeilen erwartet, kamen {List.length other}"
            let runs = historyRuns (applied @ moved @ [ assigned ])
            Expect.equal (runs |> List.map (fun r -> r.Origin)) [ TargetsApplied; MoneyAssigned; MoneyMoved ] "drei Durchläufe"
            Expect.equal
                (runs |> List.map (fun r -> r.RunId))
                [ applied.Head.ApplicationId; assigned.ApplicationId; moved.Head.ApplicationId ]
                "Gruppen-Id"
            Expect.equal runs.Head.OccurredAt (at 12) "jüngste Bewegung des Durchlaufs"
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
    let initialize = fun () ->
        Persistence.initializeDatabase ()
        Ledger.Persistence.initializeLedgerSchema ()
        Budgeting.Persistence.initializeBudgetingSchema ()

    testSequenced
    <| testList "Allgemeine Historie: Abfrage (SQLite)" [
        test "Die allgemeine Historie liefert die Bewegungen aller Monate, Kategorien und Arten, neueste zuerst, mit jüngstem Ergebnis" {
            initialize ()
            let time hour = DateTime(2026, 9, 14, hour, 0, 0, DateTimeKind.Utc)
            let september = stored (Guid.NewGuid()) MoneyAssigned "2026-09-01" "H7 Urlaub" (time 8) 0L 20000L
            let moveId = Guid.NewGuid()
            let moveOut = stored moveId MoneyMoved "2026-10-01" "H7 Lebensmittel" (time 9) 50000L 30000L
            let moveIn = stored moveId MoneyMoved "2026-10-01" "H7 Urlaub" (time 9) 0L 20000L
            let applied = stored (Guid.NewGuid()) TargetsApplied "2026-11-01" "H7 Miete" (time 10) 0L 800000L
            let september, moveOut, moveIn, applied =
                match BudgetEventsFixture.recordGroups [ september; moveOut; moveIn; applied ] with
                | [ a; b; c; d ] -> a, b, c, d
                | other -> failtest $"vier Zeilen erwartet, kamen {List.length other}"
            BudgetEventsFixture.recordResult moveOut (time 9) (Failed "Rate-Limit")
            let mine = Set.ofList [ september.Id; moveOut.Id; moveIn.Id; applied.Id ]
            match budget.getBudgetHistory () |> run with
            | Error message -> failtest message
            | Ok found ->
                let own = found |> List.filter (fun m -> mine.Contains m.MovementId)
                Expect.equal (own |> List.map (fun m -> m.MovementId)) [ applied.Id; moveIn.Id; moveOut.Id; september.Id ] "alle, neueste zuerst"
                let runs = historyRuns own
                Expect.equal
                    (runs |> List.map (fun r -> r.Text, r.Status))
                    [ "Targets anwenden, 1 Kategorie", RunOpen
                      "Verschieben, H7 Lebensmittel → H7 Urlaub", RunFailed "Rate-Limit"
                      "Zuweisen, H7 Urlaub", RunOpen ]
                    "Durchläufe"
        }
    ]
