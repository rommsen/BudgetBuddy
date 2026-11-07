module Tests.BudgetGeneralHistoryTests

// budgeting-h7q4v: die allgemeine Budget-Historie. Reine Regeln (Bewegungen zu Durchlauf-Zeilen) und die
// Abfrage aller Bewegungen gegen In-Memory-SQLite über das Record-Feld der API. Das Mischen der Historie
// je Kategorie steht in `BudgetHistoryTests.fs`. Seit budgeting-r5h2n auch Pausen und Target-Änderungen: reine
// Zeilen (`generalHistoryRows`), das Lesen über alle Streams und der Handler mit Port als Funktionswerten.

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
      Origin = Decision origin
      Month = "2026-10-01"
      CategoryId = Guid.NewGuid()
      CategoryName = name
      OldAssigned = eur oldAssigned
      NewAssigned = eur newAssigned
      Counterpart = counterpart
      Latest = latest; ReplacedBy = None }

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
            Expect.equal up.Origin (Decision MoneyAssigned) "Art"
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
        // budgeting-my8p8: eigener Stand für "in BB zurückgezogen", hinter "in YNAB geändert".
        test "Verschiebung mit Quelle in YNAB geändert und zurückgezogenem Ziel steht als in YNAB geändert" {
            Expect.equal (single (move 20m (Some ChangedInYnab) (Some WithdrawnInBb))).Status RunChangedInYnab "Quelle zeigt, wo etwas passierte"
            Expect.equal (single (move 20m (Some WithdrawnInBb) (Some ChangedInYnab))).Status RunChangedInYnab "Gegenrichtung"
        }
        test "Ein Durchlauf nur aus geschriebenen und zurückgezogenen Zeilen steht als in BB zurückgezogen" {
            let statusOf outcomes =
                (single (targets (outcomes |> List.mapi (fun i o -> $"K{i}", 10m, o)))).Status
            Expect.equal (statusOf [ Some Written; Some WithdrawnInBb ]) RunWithdrawnInBb "in BB zurückgezogen"
            Expect.equal (statusOf [ Some WithdrawnInBb; None ]) RunOpen "offen geht vor"
        }
        test "Ist das Ziel in BB ersetzt und die Quelle zurückgezogen, nennt der Durchlauf den Anlass" {
            let run =
                move 20m (Some WithdrawnInBb) (Some WithdrawnInBb)
                |> List.map (fun m -> if m.CategoryName = "Urlaub" then { m with ReplacedBy = Some (Guid.NewGuid()) } else m)
            Expect.equal (single run).Status RunReplacedInBb "in BB ersetzt"
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
            Expect.equal (runs |> List.map (fun r -> r.Origin)) [ Decision TargetsApplied; Decision MoneyAssigned; Decision MoneyMoved ] "drei Durchläufe"
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
                let own = found |> List.choose (function Movement m when mine.Contains m.MovementId -> Some m | _ -> None)
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

// ---------------- Zeilen mit Pausen und Target-Änderungen (budgeting-r5h2n) ----------------

let private pauseEntry (time: DateTime) (name: string) (lifted: bool) : HistoryEntry =
    let pause =
        { EventId = Guid.NewGuid()
          OccurredAt = time
          Month = "2026-09-01"
          CategoryId = Guid.NewGuid()
          CategoryName = name }
    if lifted then PauseLifted pause else TargetPaused pause

let private targetChange (time: DateTime) (name: string) kind before after : HistoryTargetChange =
    { ChangeId = Guid.NewGuid()
      OccurredAt = time
      Month = "2026-11-01"
      CategoryId = Guid.NewGuid()
      CategoryName = name
      ChangeKind = kind
      GoalBefore = before |> Option.map eur
      GoalAfter = after |> Option.map eur
      DateBefore = None
      DateAfter = None
      DayBefore = None
      DayAfter = None
      Latest = None
      DateStep = None
      SentToYnab = false }

/// Die Zeile als Text, Monat und Stand, ein Durchlauf als sein Text.
let private rowFacts (row: GeneralHistoryRow) : string * string * HistoryStatus option =
    match row with
    | RunRow r -> r.Text, r.Month, None
    | TargetRow t -> t.Text, t.Month, t.Status

[<Tests>]
let generalRowTests =
    testList "Allgemeine Historie: Durchläufe, Pausen und Target-Änderungen zu Zeilen" [
        // Die Eingabe kommt in fremder Reihenfolge, damit das Sortieren nach dem Gruppieren geprüft ist.
        test "Durchläufe und Target-Zeilen stehen gemischt neueste zuerst, ein Durchlauf zählt mit seiner jüngsten Bewegung, bei gleichem Zeitpunkt Durchlauf vor Pause vor Target-Änderung" {
            let applied =
                match targets [ "Miete", 800m, Some Written; "Strom", 60m, Some Written ] with
                | [ later; earlier ] -> [ { later with OccurredAt = at 12 }; { earlier with OccurredAt = at 8 } ]
                | other -> failtest $"zwei Zeilen erwartet, kamen {List.length other}"
            let assigned = { movement (Guid.NewGuid()) MoneyAssigned "Urlaub" None 0m 5m (Some Written) with OccurredAt = at 11 }
            let change = TargetChanged (targetChange (at 11) "Reise" TargetRemoved (Some 50m) None)
            let paused = pauseEntry (at 11) "Urlaub" false
            let older = pauseEntry (at 10) "Urlaub" true
            let rows = generalHistoryRows [ change; older; paused; Movement assigned; yield! applied |> List.map Movement ]
            Expect.equal
                (rows |> List.map (fun r -> let text, _, _ = rowFacts r in text))
                [ "Targets anwenden, 2 Kategorien"
                  "Zuweisen, Urlaub"
                  "Target pausiert, Urlaub"
                  "Target entfernt, Reise"
                  "Pause aufgehoben, Urlaub" ]
                "neueste zuerst"
        }
        test "Eine Target-Zeile heißt Art, Kategorie, ein leerer Name wird unbekannte Kategorie, Monat aus Pause oder Änderung, ohne Stand in BB" {
            let rows =
                generalHistoryRows
                    [ pauseEntry (at 12) "Urlaub" false
                      pauseEntry (at 11) "Urlaub" true
                      TargetChanged (targetChange (at 10) "Urlaub" TargetEdited (Some 50m) (Some 60m))
                      TargetChanged (targetChange (at 9) "Urlaub" TargetRemoved (Some 60m) None)
                      TargetChanged (targetChange (at 8) "Urlaub" (TargetCreated Monthly) None (Some 50m))
                      TargetChanged (targetChange (at 7) "" TargetRemoved (Some 50m) None) ]
            Expect.equal
                (rows |> List.map rowFacts)
                [ "Target pausiert, Urlaub", "2026-09-01", None
                  "Pause aufgehoben, Urlaub", "2026-09-01", None
                  "Target 50,00 € → 60,00 €, Urlaub", "2026-11-01", None
                  "Target entfernt, Urlaub", "2026-11-01", None
                  "Target angelegt, 50,00 € monatlich, Urlaub", "2026-11-01", None
                  "Target entfernt, unbekannte Kategorie", "2026-11-01", None ]
                "Texte, Monat, Stand"
        }
        test "Ein Altbestand mit geschriebenem Betrag und gescheitertem Datum steht teilweise geschrieben mit Grund" {
            let legacy =
                { targetChange (at 9) "Urlaub" (TargetCreated Monthly) None (Some 50m) with
                    DateAfter = Some "2026-12-01"
                    Latest = Some Written
                    DateStep = Some (Failed "Rate-Limit")
                    SentToYnab = true }
            match generalHistoryRows [ TargetChanged legacy ] with
            | [ TargetRow row ] ->
                Expect.equal row.Status (Some (HistoryPartlyWritten "Rate-Limit")) "Stand mit Grund"
                Expect.equal row.Text "Target angelegt, 50,00 € monatlich, Datum 01.12.2026 nicht gesetzt, Urlaub" "Text"
                Expect.equal row.EntryId legacy.ChangeId "Id"
            | other -> failtest $"eine Target-Zeile erwartet: {other}"
        }
    ]

// ---------------- Lesen über alle Streams gegen In-Memory-SQLite (budgeting-r5h2n) ----------------

let private takeoverTarget (goal: decimal) : Target =
    { Goal = eur goal
      Rhythm = Months 1
      Kind = Need
      Date = None
      NeedsWholeAmount = Some true
      Day = Some 15
      CreatedIn = None }

let private appendTarget (k: Guid) (events: (DateTime * Budgeting.Events.BudgetEvent) list) : unit =
    use conn = global.Persistence.getConnection ()
    Budgeting.Persistence.appendOn conn (Budgeting.Events.TargetStream k) events |> ignore

let private generalHistory () : HistoryEntry list =
    match budget.getBudgetHistory () |> run with
    | Ok entries -> entries
    | Error message -> failtest message

/// Art, Kategorie und Monat eines Eintrags.
let private entryFacts (entry: HistoryEntry) : string * Guid * string =
    match entry with
    | Movement m -> "Bewegung", m.CategoryId, m.Month
    | TargetPaused p -> "pausiert", p.CategoryId, p.Month
    | PauseLifted p -> "aufgehoben", p.CategoryId, p.Month
    | TargetChanged c -> "Target", c.CategoryId, c.Month

let private entryCategory (entry: HistoryEntry) : Guid =
    let _, category, _ = entryFacts entry
    category

[<Tests>]
let allStreamsQueryTests =
    let initialize = fun () ->
        Persistence.initializeDatabase ()
        Ledger.Persistence.initializeLedgerSchema ()
        Budgeting.Persistence.initializeBudgetingSchema ()

    testSequenced
    <| testList "Allgemeine Historie: Pausen und Target-Änderungen über alle Streams (SQLite)" [
        test "Pause und Aufhebung in zwei Monaten und eine Target-Änderung einer anderen Kategorie stehen gemischt mit den Bewegungen, neueste zuerst" {
            initialize ()
            let time hour = DateTime(2026, 9, 20, hour, 0, 0, DateTimeKind.Utc)
            let earlier, later =
                match
                    BudgetEventsFixture.recordGroups
                        [ stored (Guid.NewGuid()) MoneyAssigned "2026-09-01" "R5 Miete" (time 7) 0L 10000L
                          stored (Guid.NewGuid()) MoneyAssigned "2026-10-01" "R5 Strom" (time 11) 0L 20000L ]
                with
                | [ a; b ] -> a, b
                | other -> failtest $"zwei Zeilen erwartet, kamen {List.length other}"
            let paused = Guid.NewGuid()
            Budgeting.Persistence.recordPause None (time 8) "2026-09-01" paused "R5 Urlaub" Server.Budgeting.Domain.PauseKind.Paused |> run |> ignore
            Budgeting.Persistence.recordPause None (time 10) "2026-10-01" paused "R5 Urlaub" Server.Budgeting.Domain.PauseKind.PauseLifted |> run |> ignore
            let changed = Guid.NewGuid()
            appendTarget changed [ time 9, Budgeting.Events.BudgetEvent.TargetSet { Rule = takeoverTarget 50m; EffectiveFrom = "2026-10" } ]
            let mine = Set.ofList [ earlier.CategoryId; later.CategoryId; paused; changed ]
            Expect.equal
                (generalHistory () |> List.filter (entryCategory >> mine.Contains) |> List.map entryFacts)
                [ "Bewegung", later.CategoryId, "2026-10-01"
                  "aufgehoben", paused, "2026-10-01"
                  "Target", changed, "2026-10-01"
                  "pausiert", paused, "2026-09-01"
                  "Bewegung", earlier.CategoryId, "2026-09-01" ]
                "gemischt, neueste zuerst, Monat und Kategorie je Eintrag"
        }
        test "Nach der Übernahme mit 50 € zeigen Setzen auf 60 € und Entfernen genau zwei Target-Änderungen, die Übernahme selbst keine, der Tab Historie dieselben" {
            initialize ()
            let time hour = DateTime(2026, 10, 5, hour, 0, 0, DateTimeKind.Utc)
            let k = Guid.NewGuid()
            appendTarget
                k
                [ time 7, Budgeting.Events.BudgetEvent.TargetTakenOverFromYnab { CategoryName = "R5 Reise"; Rule = takeoverTarget 50m; YnabGoalType = "NEED" }
                  time 8, Budgeting.Events.BudgetEvent.TargetSet { Rule = takeoverTarget 60m; EffectiveFrom = "2026-10" }
                  time 9, Budgeting.Events.BudgetEvent.TargetRemoved { EffectiveFrom = "2026-10" } ]
            let changes =
                generalHistory () |> List.choose (function TargetChanged c when c.CategoryId = k -> Some c | _ -> None)
            Expect.equal
                (changes |> List.map (fun c -> c.ChangeKind, targetChangeText c, c.CategoryName))
                [ TargetRemoved, "Target entfernt", "R5 Reise"; TargetEdited, "Target 50,00 € → 60,00 €", "R5 Reise" ]
                "zwei Änderungen, vorher aus der Übernahme"
            match budget.getCategoryHistory { CategoryId = k; Month = "2026-10-01" } |> run with
            | Ok history ->
                Expect.equal
                    (history.Entries |> List.choose (function TargetChanged c -> Some c | _ -> None))
                    changes
                    "der Tab Historie zeigt dieselben im Monat"
            | Error message -> failtest message
        }
        test "Ein Altbestand mit geschriebenem Schritt 1 und gescheitertem Schritt 2 kommt mit beiden Ergebnissen, gesendet nach YNAB" {
            initialize ()
            let time hour = DateTime(2026, 8, 12, hour, 0, 0, DateTimeKind.Utc)
            let k = Guid.NewGuid()
            use conn = global.Persistence.getConnection ()
            let changeId =
                BudgetEventsFixture.recordLegacyGoalChangeOn
                    conn
                    k
                    (time 9)
                    { Month = "2026-08"
                      CategoryName = "R5 Auto"
                      ChangeKind = TargetCreated Monthly
                      OldGoalTarget = None
                      NewGoalTarget = Some 50000L
                      OldDate = None
                      NewDate = Some "2026-12-01" }
            BudgetEventsFixture.recordLegacyStepOn conn k changeId 1 (time 9) Written
            BudgetEventsFixture.recordLegacyStepOn conn k changeId 2 (time 9) (Failed "Rate-Limit")
            match generalHistory () |> List.choose (function TargetChanged c when c.CategoryId = k -> Some c | _ -> None) with
            | [ c ] ->
                Expect.equal (c.ChangeId, c.Month, c.CategoryName) (changeId, "2026-08-01", "R5 Auto") "Änderung"
                Expect.isTrue c.SentToYnab "nach YNAB gesendet"
                Expect.equal (c.Latest, c.DateStep) (Some Written, Some (Failed "Rate-Limit")) "beide Schritt-Ergebnisse"
            | other -> failtest $"eine Änderung erwartet: {other}"
        }
    ]

// ---------------- Handler mit Port als Funktionswerten (budgeting-r5h2n) ----------------

let private quietPort : Server.Budgeting.QueryHandlers.HistoryPort =
    { LoadMovements = fun _ -> async { return Ok [] }
      LoadMonthlyActivity = fun _ _ -> async { return [] }
      LoadPauseEvents = fun _ _ -> async { return Ok [] }
      LoadTargetChanges = fun _ _ -> async { return Ok [] }
      LoadAllPauseEvents = fun () -> async { return Ok [] }
      LoadAllTargetChanges = fun () -> async { return Ok [] } }

let private generalWith (port: Server.Budgeting.QueryHandlers.HistoryPort) =
    Server.Budgeting.QueryHandlers.budgetHistory port |> run

[<Tests>]
let handlerTests =
    testList "Allgemeine Historie: Lesefehler und Mischen" [
        test "Wirft ein Leser der Pausen, der Target-Änderungen oder der Bewegungen, ist die Antwort ein Fehler mit dem Text der Quelle" {
            Expect.equal
                (generalWith { quietPort with LoadAllPauseEvents = fun () -> failwith "kaputt" })
                (Error Budgeting.Persistence.PauseEventsUnreadableText)
                "Pausen"
            Expect.equal
                (generalWith { quietPort with LoadAllTargetChanges = fun () -> failwith "kaputt" })
                (Error Budgeting.Persistence.TargetChangesUnreadableText)
                "Target-Änderungen"
            Expect.equal
                (generalWith { quietPort with LoadMovements = fun _ -> failwith "kaputt" })
                (Error "Budget-Bewegungen nicht lesbar")
                "Bewegungen"
        }
        test "Ein nicht lesbares Pause- oder Target-Ereignis macht die Antwort zum Fehler mit dem Text der Quelle" {
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            BudgetEventsFixture.appendUnreadable anchor (Budgeting.Events.MonthStream "2026-10") "TargetPaused"
            BudgetEventsFixture.appendUnreadable anchor (Budgeting.Events.TargetStream (Guid.NewGuid())) "TargetSet"
            Expect.equal
                (generalWith { quietPort with LoadAllPauseEvents = fun () -> Budgeting.Persistence.loadAllPauseEventsWith connect })
                (Error Budgeting.Persistence.PauseEventsUnreadableText)
                "Pausen"
            Expect.equal
                (generalWith { quietPort with LoadAllTargetChanges = fun () -> Budgeting.Persistence.loadAllTargetChangesWith connect })
                (Error Budgeting.Persistence.TargetChangesUnreadableText)
                "Target-Änderungen"
        }
        test "Bei gleichem Zeitpunkt steht die Bewegung vor der Pause und die Pause vor der Target-Änderung" {
            let moved = movement (Guid.NewGuid()) MoneyAssigned "Urlaub" None 0m 5m (Some Written)
            let paused = pauseEntry (at 9) "Urlaub" false
            let change = targetChange (at 9) "Urlaub" TargetRemoved (Some 50m) None
            let port =
                { quietPort with
                    LoadMovements = fun _ -> async { return Ok [ moved ] }
                    LoadAllPauseEvents = fun () -> async { return Ok [ paused ] }
                    LoadAllTargetChanges = fun () -> async { return Ok [ change ] } }
            Expect.equal (generalWith port) (Ok [ Movement moved; paused; TargetChanged change ]) "Bewegung, Pause, Änderung"
        }
    ]
