module Tests.BudgetHistoryRowsClientTests

// budgeting-yarty: die Zeilen der Budget-Historie und des Tabs Historie nach Styleguide §7.11 (`HistoryList`). Kürzel,
// Text, Betrag und Zusatz (Monat, Stand) je Zeilenart kommen aus reinen Funktionen; die View setzt sie nur zusammen.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Inspector
open Components.Budget.History

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

/// 3. Oktober 2026, 09:05 UTC: in Berlin 11:05 (Sommerzeit).
let private at = DateTime(2026, 10, 3, 9, 5, 0, DateTimeKind.Utc)

let private run origin (amount: decimal) (status: RunStatus) : HistoryRun =
    { RunId = Guid.NewGuid()
      OccurredAt = at
      Origin = origin
      Month = "2026-10-01"
      Text = "Zuweisen, Urlaub"
      Amount = eur amount
      Status = status
      Lines = [] }

let private targetRow (status: HistoryStatus option) : HistoryTargetRow =
    { EntryId = Guid.NewGuid()
      OccurredAt = at
      Month = "2026-11-01"
      Text = "Target pausiert, Urlaub"
      Status = status }

let private movement origin (latest: LineOutcome option) : HistoryMovement =
    { MovementId = Guid.NewGuid()
      ApplicationId = Guid.NewGuid()
      OccurredAt = at
      Origin = origin
      Month = "2026-10-01"
      CategoryId = Guid.NewGuid()
      CategoryName = "Urlaub"
      OldAssigned = eur 10m
      NewAssigned = eur 30m
      Counterpart = Some "Lebensmittel"
      Latest = latest
      ReplacedBy = None }

let private pause : HistoryPause =
    { EventId = Guid.NewGuid()
      OccurredAt = at
      Month = "2026-10-01"
      CategoryId = Guid.NewGuid()
      CategoryName = "Urlaub" }

let private targetChange : HistoryTargetChange =
    { ChangeId = Guid.NewGuid()
      OccurredAt = at
      Month = "2026-10-01"
      CategoryId = Guid.NewGuid()
      CategoryName = "Urlaub"
      ChangeKind = TargetEdited
      GoalBefore = Some (eur 100m)
      GoalAfter = Some (eur 120m)
      DateBefore = None
      DateAfter = None
      DayBefore = None
      DayAfter = None
      Latest = None
      DateStep = None
      SentToYnab = false }

[<Tests>]
let tests =
    testList "Budget-Historie: Zeilen nach §7.11" [
        test "Das Kürzel: BB für Entscheidungen in BB, YNAB für Beobachtetes und Übernommenes" {
            for origin in [ MovementOrigin.TargetsApplied; MovementOrigin.MoneyMoved; MovementOrigin.MoneyAssigned ] do
                Expect.equal (rowItem (RunRow (run (Decision origin) 20m RunWritten))).Who "BB" $"{origin}"
            Expect.equal (rowItem (RunRow (run ObservedInYnab 20m RunFromYnab))).Who "YNAB" "in YNAB zugewiesen"
            Expect.equal (rowItem (RunRow (run TakenOverFromYnab 20m RunFromYnab))).Who "YNAB" "aus YNAB übernommen"
            Expect.equal (rowItem (TargetRow (targetRow None))).Who "BB" "Pause und Target-Änderung"
        }
        test "Eine Zeile der Seite: Zeit in Berliner Zeit, Text des Durchlaufs, Betrag wie bisher; eine Target-Zeile ohne Betrag" {
            let moved = rowItem (RunRow (run (Decision MovementOrigin.MoneyMoved) 20m RunWritten))
            Expect.equal moved.When "03.10. 11:05" "Zeit"
            Expect.equal moved.Amount (Some "20,00 €") "Verschieben ohne Vorzeichen"
            Expect.equal (rowItem (RunRow (run (Decision MovementOrigin.TargetsApplied) 860m RunWritten))).Amount (Some "+860,00 €") "Targets mit Vorzeichen"
            let target = rowItem (TargetRow (targetRow None))
            Expect.isNone target.Amount "kein Betrag"
        }
        test "Der Zusatz einer Zeile: der Monat lang und kurz, dazu der Stand je Status" {
            let note status = (rowNote (RunRow (run (Decision MovementOrigin.MoneyAssigned) 20m status))).Badge
            let written = rowNote (RunRow (run (Decision MovementOrigin.MoneyAssigned) 20m RunWritten))
            Expect.equal written.Month "Oktober 2026" "lang"
            Expect.equal written.ShortMonth "Okt. 2026" "kurz"
            Expect.equal (note RunWritten) (StatusBadge HistoryWritten) "geschrieben"
            Expect.equal (note (RunFailed "Rate-Limit")) (StatusBadge (HistoryFailed "Rate-Limit")) "fehlgeschlagen mit Grund"
            Expect.equal (note (RunPartlyFailed (1, 2))) (PartlyFailedBadge ("1 von 2", "1 von 2 fehlgeschlagen")) "teilweise"
            Expect.equal (note RunFromYnab) NoBadge "aus YNAB ohne Stand"
            let pauseNote = rowNote (TargetRow (targetRow None))
            Expect.equal pauseNote.Month "November 2026" "Monat der Target-Zeile"
            Expect.equal pauseNote.Badge NoBadge "nur in BB: kein Stand"
            Expect.equal
                (rowNote (TargetRow (targetRow (Some (HistoryPartlyWritten "Rate-Limit"))))).Badge
                (StatusBadge (HistoryPartlyWritten "Rate-Limit"))
                "Altbestand mit Stand"
        }
        test "Ein Eintrag im Tab Historie: Kürzel je Herkunft, Text und Betrag der Bewegung, Stand als Zusatz" {
            let assigned = entryItem (Movement (movement (Decision MovementOrigin.MoneyMoved) (Some (Failed "Rate-Limit"))))
            Expect.equal assigned.Who "BB" "Entscheidung in BB"
            Expect.equal assigned.When "03.10. 11:05" "Zeit"
            Expect.equal assigned.Text "von Lebensmittel" "Text"
            Expect.equal assigned.Amount (Some "+20,00 €") "Betrag mit Vorzeichen"
            Expect.equal (entryStatus (Movement (movement (Decision MovementOrigin.MoneyMoved) (Some (Failed "Rate-Limit"))))) (Some (HistoryFailed "Rate-Limit")) "Stand"
            let observed = Movement (movement ObservedInYnab None)
            Expect.equal (entryItem observed).Who "YNAB" "in YNAB zugewiesen"
            Expect.isNone (entryStatus observed) "aus YNAB ohne Stand"
            let takenOver = entryItem (Movement (movement TakenOverFromYnab None))
            Expect.equal takenOver.Who "YNAB" "Übernahme"
            Expect.isNone takenOver.Amount "die Übernahme nennt den Stand im Text"
            let paused = entryItem (TargetPaused pause)
            Expect.equal paused.Who "BB" "Pause"
            Expect.equal paused.Text "Target pausiert" "Text der Pause"
            Expect.isNone paused.Amount "Pause ohne Betrag"
            Expect.equal (entryItem (PauseLifted pause)).Who "BB" "Aufhebung"
            Expect.equal (entryItem (TargetChanged targetChange)).Who "BB" "Target-Änderung"
        }
    ]
