module Tests.BudgetHistoryClientTests

// budgeting-dyn8g: Laden des Tabs Historie durch das echte `update` der Budget-Seite. Buchungen und
// Historie teilen eine Generation (`TabGeneration`) mit dem Schlüssel Kategorie und Monat. Die Cmds
// werden unter .NET nur gebaut, nie ausgeführt: der Proxy liegt hinter einem Lambda.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Inspector

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private categoryA = Guid.Parse "00000000-0000-0000-0000-0000000d0a01"
let private categoryB = Guid.Parse "00000000-0000-0000-0000-0000000d0b01"

let private budgetCategory (id: Guid) (name: string) : BudgetCategory =
    { Id = id
      Name = name
      Assigned = eur 100m
      Activity = eur -30m
      Available = eur 70m
      Underfunded = None
      Target = None
      Snoozed = false }

let private october : BudgetMonth =
    { Month = "2026-10-01"
      ToBeBudgeted = eur 0m
      Groups = [ { Name = "Alltag"; Categories = [ budgetCategory categoryA "Lebensmittel"; budgetCategory categoryB "Urlaub" ] } ] }

let private frame : BudgetFrame =
    { CurrentMonth = "2026-10-01"
      Months = [ "2026-09-01"; "2026-10-01"; "2026-11-01" ]
      CategoryOrder = [ categoryA; categoryB ] }

let private page : Model = { Components.Budget.Types.init () with Frame = Success frame; Month = Success october }

let private step (msg: Msg) (model: Model) : Model =
    let next, _, _ = Components.Budget.State.update msg model
    next

let private stepCmd (msg: Msg) (model: Model) =
    let next, cmd, _ = Components.Budget.State.update msg model
    next, cmd

let private selected (model: Model) = model.Selected |> Option.get
let private historyOf (model: Model) = (selected model).History
let private entriesOf (model: Model) = (selected model).Entries

let private emptyHistory : CategoryHistory = { Movements = []; Spending = [] }

let private loadedHistory : CategoryHistory =
    { emptyHistory with Spending = [ { Month = "2026-10-01"; Activity = eur -30m } ] }

let private entry : CategoryMonthEntry =
    { EntryId = Guid.NewGuid()
      AccountId = Guid.NewGuid()
      AccountName = Some "Giro"
      Date = DateTime(2026, 10, 5)
      Payee = Some "Markt"
      Amount = eur -30m
      IsSplitShare = false }

/// Kategorie A gewählt, Tab Historie, die Historie ist geladen.
let private historyShown () : Model =
    let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab HistoryTab)
    asked |> step (CategoryHistoryLoaded (asked.TabGeneration, Ok loadedHistory))

let private report : ApplyReport =
    { ApplicationId = string (Guid.NewGuid())
      Month = "2026-10-01"
      Lines =
        [ { CategoryId = categoryA
            CategoryName = "Lebensmittel"
            OldAssigned = eur 100m
            NewAssigned = eur 120m
            Outcome = Written } ]
      ActualToBeBudgeted = None }

/// Jede bestätigte Bewegung: Zuweisen (Schlange), Holen und Abgeben (Verschieben), Targets anwenden,
/// erneut senden, dazu die abgebrochenen Aufrufe mit unbekanntem Ausgang.
let private movementAnswers : (string * Msg) list =
    [ "Zuweisen", AssignAnswered (Ok report)
      "Zuweisen abgebrochen", AssignCallLost "weg"
      "Holen oder Abgeben", MoveAnswered (Ok report)
      "Holen oder Abgeben abgebrochen", MoveCallLost "weg"
      "Verschieben erneut gesendet", MoveRetried (report.ApplicationId, Ok report)
      "Targets anwenden", TargetsApplied (Ok (Applied report))
      "Targets erneut gesendet", TargetsRetried (Ok report) ]

[<Tests>]
let tests =
    testList "Historie im Inspektor: Laden" [
        test "Tab Historie gewählt: die Historie wird für Kategorie und Monat angefragt" {
            let chosen = page |> step (SelectCategory categoryA)
            let asked, cmd = chosen |> stepCmd (SelectInspectorTab HistoryTab)
            Expect.equal (historyOf asked) Loading "läuft"
            Expect.isNonEmpty cmd "eine Anfrage"
            Expect.equal (entriesOf asked) NotAsked "die Buchungen bleiben ungefragt"
            let answered = asked |> step (CategoryHistoryLoaded (asked.TabGeneration, Ok loadedHistory))
            Expect.equal (historyOf answered) (Success loadedHistory) "die Antwort steht im Tab"
        }
        test "Eine Antwort von vor einem Kategoriewechsel wird verworfen" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab HistoryTab)
            let switched = asked |> step (SelectCategory categoryB)
            Expect.equal (selected switched).CategoryId categoryB "B gewählt"
            Expect.equal (historyOf switched) Loading "B wird geholt"
            let late = switched |> step (CategoryHistoryLoaded (asked.TabGeneration, Ok loadedHistory))
            Expect.equal (historyOf late) Loading "die Antwort zu A zählt nicht"
        }
        test "Eine Antwort von vor einem Monatswechsel wird verworfen, danach kommt die des neuen Monats" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab HistoryTab)
            let changing = asked |> step (SelectMonth "2026-09-01")
            let late = changing |> step (CategoryHistoryLoaded (asked.TabGeneration, Ok loadedHistory))
            Expect.notEqual (historyOf late) (Success loadedHistory) "die Antwort zum Oktober zählt nicht"
            let september = { october with Month = "2026-09-01" }
            let arrived, cmd = late |> stepCmd (Loaded (late.MonthGeneration, Ok september))
            Expect.equal (historyOf arrived) Loading "für September angefragt"
            Expect.isNonEmpty cmd "eine Anfrage"
        }
        test "Ein Fehler steht im Tab, Wiederholen fragt neu" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab HistoryTab)
            let failed = asked |> step (CategoryHistoryLoaded (asked.TabGeneration, Error "kaputt"))
            Expect.equal (historyOf failed) (Failure "kaputt") "Fehler"
            let retried, cmd = failed |> stepCmd LoadCategoryHistory
            Expect.equal (historyOf retried) Loading "neu angefragt"
            Expect.isNonEmpty cmd "eine Anfrage"
        }
        test "Nach jeder bestätigten Bewegung ist die Historie wieder fällig und kommt beim sichtbaren Tab neu" {
            for name, msg in movementAnswers do
                let answered, answerCmd = historyShown () |> stepCmd msg
                // Ist der Ausgang unbekannt, wird der Monat neu gelesen: die Historie kommt mit ihm.
                let after, cmd =
                    match answered.Month with
                    | Success _ -> answered, answerCmd
                    | _ -> answered |> stepCmd (Reloaded (answered.MonthGeneration, Ok october))
                Expect.equal (historyOf after) Loading $"{name}: neu angefragt"
                Expect.isNonEmpty cmd $"{name}: eine Anfrage"
                let stale = after |> step (CategoryHistoryLoaded (after.TabGeneration - 1, Ok emptyHistory))
                Expect.equal (historyOf stale) Loading $"{name}: die Antwort davor zählt nicht"
        }
        test "Nach einer Bewegung im Tab Buchungen ist die Historie fällig, die Buchungen bleiben stehen" {
            let onEntries =
                let shown = historyShown () |> step (SelectInspectorTab EntriesTab)
                shown |> step (CategoryEntriesLoaded (shown.TabGeneration, Ok [ entry ]))
            for name, msg in movementAnswers do
                let after = onEntries |> step msg
                Expect.equal (historyOf after) NotAsked $"{name}: fällig, aber nicht zu sehen"
                Expect.equal (entriesOf after) (Success [ entry ]) $"{name}: Buchungen unverändert"
        }
        test "Wechsel auf Historie und zurück fragt die geladenen Buchungen nicht noch einmal" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab EntriesTab)
            let entriesShown = asked |> step (CategoryEntriesLoaded (asked.TabGeneration, Ok [ entry ]))
            let onHistory = entriesShown |> step (SelectInspectorTab HistoryTab)
            let back, cmd = onHistory |> stepCmd (SelectInspectorTab EntriesTab)
            Expect.equal (entriesOf back) (Success [ entry ]) "Buchungen stehen"
            Expect.isEmpty cmd "keine Anfrage"
        }
        test "Laufen die Buchungen noch, zählt ihre Antwort auch nach dem Wechsel auf Historie" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab EntriesTab)
            let onHistory = asked |> step (SelectInspectorTab HistoryTab)
            let answered = onHistory |> step (CategoryEntriesLoaded (asked.TabGeneration, Ok [ entry ]))
            Expect.equal (entriesOf answered) (Success [ entry ]) "nicht verworfen"
            let back, cmd = answered |> stepCmd (SelectInspectorTab EntriesTab)
            Expect.equal (entriesOf back) (Success [ entry ]) "steht"
            Expect.isEmpty cmd "keine zweite Anfrage"
        }
    ]

[<Tests>]
let returnTests =
    // budgeting-dyn8g mit budgeting-r7t3k: Die Rückkehr aus dem Kassenbuch liest den Monat still neu,
    // dabei sind die Daten beider Tabs wieder fällig, geholt wird die des sichtbaren Tabs.
    let back (model: Model) =
        model
        |> step (OpenEntryInLedger entry.EntryId)
        |> stepCmd PageEntered
    testList "Historie im Inspektor: Rückkehr aus dem Kassenbuch" [
        test "Im Tab Historie kommt die Historie neu, die Buchungen sind wieder fällig" {
            let returned, cmd = back (historyShown ())
            Expect.equal (historyOf returned) Loading "Historie neu angefragt"
            Expect.equal (entriesOf returned) NotAsked "Buchungen fällig"
            Expect.isNonEmpty cmd "Anfragen"
            Expect.equal returned.Month (Success october) "der Monat bleibt stehen"
        }
        test "Im Tab Buchungen kommen die Buchungen neu, die Historie ist wieder fällig" {
            let onEntries =
                let shown = historyShown () |> step (SelectInspectorTab EntriesTab)
                shown |> step (CategoryEntriesLoaded (shown.TabGeneration, Ok [ entry ]))
            let returned, _ = back onEntries
            Expect.equal (entriesOf returned) Loading "Buchungen neu angefragt"
            Expect.equal (historyOf returned) NotAsked "Historie fällig"
        }
    ]

[<Tests>]
let textTests =
    testList "Historie im Inspektor: Texte" [
        test "Der Stand je Ergebnisart" {
            Expect.equal
                ([ HistoryWritten; HistoryFailed "Rate-Limit"; HistoryFailed ""; HistoryChangedInYnab; HistoryOpen ]
                 |> List.map historyStatusText)
                [ "geschrieben"; "fehlgeschlagen: Rate-Limit"; "fehlgeschlagen"; "in YNAB geändert"; "noch offen" ]
                "Texte"
        }
        test "Der Betrag am Balken in ganzen Euro, mit Tausenderpunkt" {
            Expect.equal (barAmountText (eur 1234.5m)) "1.235" "gerundet"
            Expect.equal (barAmountText (eur -20.4m)) "-20" "negativ"
            Expect.equal (barAmountText (eur 0m)) "0" "null"
        }
    ]
