module Tests.BudgetHistoryClientTests

// budgeting-dyn8g: Laden des Tabs Historie durch das echte `update` der Budget-Seite. Buchungen und
// Historie haben je eine Generation (`EntriesGeneration`, `HistoryGeneration`, budgeting-x4k7p) zum
// Schlüssel Kategorie und Monat. Die Cmds werden unter .NET nur gebaut, nie ausgeführt: der Proxy
// liegt hinter einem Lambda.

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
      Pause = None; OpenWriteBack = None }

let private october : BudgetMonth =
    { Month = "2026-10-01"
      ToBeBudgeted = eur 0m
      Groups = [ { Name = "Alltag"; Categories = [ budgetCategory categoryA "Lebensmittel"; budgetCategory categoryB "Urlaub" ] } ] }

let private frame : BudgetFrame =
    { CurrentMonth = "2026-10-01"
      Months = [ "2026-09-01"; "2026-10-01"; "2026-11-01" ]
      CategoryOrder = [ categoryA; categoryB ]
      MonthStartHidden = [] }

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

let private emptyHistory : CategoryHistory = { Entries = []; Spending = [] }

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
    asked |> step (CategoryHistoryLoaded (asked.HistoryGeneration, Ok loadedHistory))

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
            let answered = asked |> step (CategoryHistoryLoaded (asked.HistoryGeneration, Ok loadedHistory))
            Expect.equal (historyOf answered) (Success loadedHistory) "die Antwort steht im Tab"
        }
        test "Eine Antwort von vor einem Kategoriewechsel wird verworfen" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab HistoryTab)
            let switched = asked |> step (SelectCategory categoryB)
            Expect.equal (selected switched).CategoryId categoryB "B gewählt"
            Expect.equal (historyOf switched) Loading "B wird geholt"
            let late = switched |> step (CategoryHistoryLoaded (asked.HistoryGeneration, Ok loadedHistory))
            Expect.equal (historyOf late) Loading "die Antwort zu A zählt nicht"
        }
        test "Eine Antwort von vor einem Monatswechsel wird verworfen, danach kommt die des neuen Monats" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab HistoryTab)
            let changing = asked |> step (SelectMonth "2026-09-01")
            let late = changing |> step (CategoryHistoryLoaded (asked.HistoryGeneration, Ok loadedHistory))
            Expect.notEqual (historyOf late) (Success loadedHistory) "die Antwort zum Oktober zählt nicht"
            let september = { october with Month = "2026-09-01" }
            let arrived, cmd = late |> stepCmd (Loaded (late.MonthGeneration, Ok september))
            Expect.equal (historyOf arrived) Loading "für September angefragt"
            Expect.isNonEmpty cmd "eine Anfrage"
        }
        test "Ein Fehler steht im Tab, Wiederholen fragt neu" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab HistoryTab)
            let failed = asked |> step (CategoryHistoryLoaded (asked.HistoryGeneration, Error "kaputt"))
            Expect.equal (historyOf failed) (Failure "kaputt") "Fehler"
            let retried, cmd = failed |> stepCmd LoadCategoryHistory
            Expect.equal (historyOf retried) Loading "neu angefragt"
            Expect.isNonEmpty cmd "eine Anfrage"
        }
        // budgeting-x4k7p F1: Vorher sprang die geladene Historie nach jeder Bewegung auf das Skelett.
        test "Nach jeder bestätigten Bewegung bleibt die geladene Historie stehen und kommt still neu" {
            let newer = { emptyHistory with Spending = [ { Month = "2026-10-01"; Activity = eur -50m } ] }
            for name, msg in movementAnswers do
                let shown = historyShown ()
                let answered, cmd = shown |> stepCmd msg
                match answered.Month with
                | Success _ ->
                    Expect.equal (historyOf answered) (Success loadedHistory) $"{name}: der alte Stand bleibt"
                    Expect.equal answered.HistoryGeneration (shown.HistoryGeneration + 1) $"{name}: neuer Stand"
                    let _, withoutHistory = { shown with Tab = MoneyTab } |> stepCmd msg
                    Expect.equal (List.length cmd) (List.length withoutHistory + 1) $"{name}: eine Abfrage der Historie"
                    let stale = answered |> step (CategoryHistoryReloaded (shown.HistoryGeneration, Ok emptyHistory))
                    Expect.equal (historyOf stale) (Success loadedHistory) $"{name}: die Antwort davor zählt nicht"
                    let replaced = answered |> step (CategoryHistoryReloaded (answered.HistoryGeneration, Ok newer))
                    Expect.equal (historyOf replaced) (Success newer) $"{name}: die Antwort ersetzt den Stand"
                | _ ->
                    // Ist der Ausgang unklar, lädt der Monat neu und die Historie mit ihm.
                    let after, reloadCmd = answered |> stepCmd (Reloaded (answered.MonthGeneration, Ok october))
                    Expect.equal (historyOf after) Loading $"{name}: neu angefragt"
                    Expect.isNonEmpty reloadCmd $"{name}: eine Anfrage"
        }
        test "Scheitert das stille Nachladen der Historie, bleibt der alte Stand und ein Warn-Toast nennt den Grund" {
            let answered = historyShown () |> step (AssignAnswered (Ok report))
            let failed, _, external =
                Components.Budget.State.update (CategoryHistoryReloaded (answered.HistoryGeneration, Error "kaputt")) answered
            Expect.equal (historyOf failed) (Success loadedHistory) "der alte Stand bleibt"
            match external with
            | ShowToast (text, ToastWarning) -> Expect.stringContains text "kaputt" "der Grund steht im Toast"
            | other -> failtest $"Warn-Toast erwartet, kam {other}"
            let late, _, lateExternal =
                Components.Budget.State.update (CategoryHistoryReloaded (answered.HistoryGeneration - 1, Error "alt")) answered
            Expect.equal late answered "ein Fehler zu einem alten Stand ändert nichts"
            Expect.equal lateExternal NoOp "und meldet nichts"
        }
        test "In einer Schlange von drei Zuweisungen kommt die Historie einmal neu, nach der letzten Antwort" {
            let write state = { CategoryId = categoryA; Month = "2026-10-01"; Assigned = eur 120m; State = state }
            let queued = { historyShown () with AssignQueue = [ write WriteRunning; write WriteWaiting; write WriteWaiting ] }
            let answers = List.scan (fun model _ -> model |> step (AssignAnswered (Ok report))) queued [ 1..3 ]
            Expect.equal
                (answers |> List.map (fun m -> m.HistoryGeneration - queued.HistoryGeneration))
                [ 0; 0; 0; 1 ]
                "erst die letzte Antwort fragt neu"
            Expect.isTrue (answers |> List.forall (fun m -> historyOf m = Success loadedHistory)) "der Stand bleibt stehen"
        }
        test "Nach einer Bewegung im Tab Buchungen ist die Historie fällig, die Buchungen bleiben stehen" {
            let onEntries =
                let shown = historyShown () |> step (SelectInspectorTab EntriesTab)
                shown |> step (CategoryEntriesLoaded (shown.EntriesGeneration, Ok [ entry ]))
            for name, msg in movementAnswers do
                let after = onEntries |> step msg
                Expect.equal (historyOf after) NotAsked $"{name}: fällig, aber nicht zu sehen"
                Expect.equal (entriesOf after) (Success [ entry ]) $"{name}: Buchungen unverändert"
        }
        // budgeting-x4k7p F2: Vorher drehte die Bewegung die gemeinsame Generation weiter, die laufende
        // Antwort der Buchungen fiel weg und wurde neu angefragt.
        test "Laufen die Buchungen, zählt ihre Antwort auch nach einer bestätigten Bewegung" {
            let mutable checkedWays = 0
            for name, msg in movementAnswers do
                let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab EntriesTab)
                let moved, cmd = asked |> stepCmd msg
                match moved.Month with
                | Success _ ->
                    checkedWays <- checkedWays + 1
                    Expect.equal (entriesOf moved) Loading $"{name}: läuft weiter"
                    let _, withoutEntries = { asked with Tab = MoneyTab } |> stepCmd msg
                    Expect.equal (List.length cmd) (List.length withoutEntries) $"{name}: keine zweite Abfrage"
                    let answered = moved |> step (CategoryEntriesLoaded (asked.EntriesGeneration, Ok [ entry ]))
                    Expect.equal (entriesOf answered) (Success [ entry ]) $"{name}: die Antwort zählt"
                | _ -> ()
            Expect.isGreaterThanOrEqual checkedWays 5 "Wege mit stehendem Monat"
        }
        test "Ein Probelauf oder ein Fehler beim Targets anwenden macht die Historie nicht neu fällig" {
            let preview : TargetPreview =
                { Month = "2026-10-01"; Lines = []; Skipped = []; Total = eur 0m; ToBeBudgeted = eur 0m; ToBeBudgetedAfter = eur 0m; CommandId = None }
            let applying = { historyShown () with Targets = Applying { Shown = preview; Changed = false; Excluded = Set.empty } }
            for name, result in [ "Probelauf", Ok (PreviewChanged preview); "Fehler", Error "weg" ] do
                let after = applying |> step (TargetsApplied result)
                Expect.equal after.HistoryGeneration applying.HistoryGeneration $"{name}: derselbe Stand"
                Expect.equal (historyOf after) (Success loadedHistory) $"{name}: die Historie bleibt"
            let onEntries = { applying with Tab = EntriesTab }
            for name, result in [ "Probelauf", Ok (PreviewChanged preview); "Fehler", Error "weg" ] do
                let after = onEntries |> step (TargetsApplied result)
                Expect.equal (historyOf after) (Success loadedHistory) $"{name}: im Tab Buchungen nicht fällig"
        }
        test "Wechsel auf Historie und zurück fragt die geladenen Buchungen nicht noch einmal" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab EntriesTab)
            let entriesShown = asked |> step (CategoryEntriesLoaded (asked.EntriesGeneration, Ok [ entry ]))
            let onHistory = entriesShown |> step (SelectInspectorTab HistoryTab)
            let back, cmd = onHistory |> stepCmd (SelectInspectorTab EntriesTab)
            Expect.equal (entriesOf back) (Success [ entry ]) "Buchungen stehen"
            Expect.isEmpty cmd "keine Anfrage"
        }
        test "Laufen die Buchungen noch, zählt ihre Antwort auch nach dem Wechsel auf Historie" {
            let asked = page |> step (SelectCategory categoryA) |> step (SelectInspectorTab EntriesTab)
            let onHistory = asked |> step (SelectInspectorTab HistoryTab)
            let answered = onHistory |> step (CategoryEntriesLoaded (asked.EntriesGeneration, Ok [ entry ]))
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
                shown |> step (CategoryEntriesLoaded (shown.EntriesGeneration, Ok [ entry ]))
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
                ([ HistoryWritten; HistoryFailed "Rate-Limit"; HistoryFailed ""; HistoryChangedInYnab; HistoryOpen; HistoryReplacedInBb; HistoryWithdrawnInBb ]
                 |> List.map historyStatusText)
                [ "geschrieben"; "fehlgeschlagen"; "fehlgeschlagen"; "in YNAB geändert"; "noch offen"; "in BB ersetzt"; "in BB zurückgezogen" ]
                "Texte"
        }
        test "Der Grund einer fehlgeschlagenen Bewegung steht neben dem Badge, ohne Grund nichts" {
            Expect.equal
                ([ HistoryWritten; HistoryFailed "Rate-Limit"; HistoryFailed " "; HistoryChangedInYnab; HistoryOpen; HistoryWithdrawnInBb ]
                 |> List.map historyFailureReason)
                [ None; Some "Rate-Limit"; None; None; None; None ]
                "Grund"
        }
        test "Der Betrag am Balken in ganzen Euro, mit Tausenderpunkt, ein Erstattungsüberschuss mit Plus" {
            Expect.equal (barAmountText (eur 1234.5m)) "1.235" "gerundet"
            Expect.equal (barAmountText (eur -85m)) "+85" "Erstattungsüberschuss"
            Expect.equal (barAmountText (eur -20.4m)) "+20" "Erstattungsüberschuss, gerundet"
            Expect.equal (barAmountText (eur -0.4m)) "0" "gerundet null, ohne Vorzeichen"
            Expect.equal (barAmountText (eur 0m)) "0" "null"
        }
        // budgeting-v6m3z: Die Wertzeile über den Balken zeigt das Label des gewählten Balkens.
        test "Die Wertzeile je Balken: Monat und genauer Betrag, laufender Monat mit Zusatz, Erstattungsüberschuss mit Plus" {
            let bar month spending percent : SpendingBar =
                { Month = month; Spending = eur spending; Percent = percent; Kind = monthKind "2026-10-01" month }
            let past = spendingMonthBar (bar "2026-08-01" 1234.56m 100)
            Expect.equal past.Label "August 2026 · 1.234,56 €" "vergangener Monat"
            Expect.isFalse past.Running "vergangen"
            let running = spendingMonthBar (bar "2026-10-01" 150m 12)
            Expect.equal running.Label "Oktober 2026 · 150,00 € · läuft noch" "laufender Monat"
            Expect.isTrue running.Running "läuft"
            let refund = spendingMonthBar (bar "2026-09-01" -85m 0)
            Expect.equal refund.Label "September 2026 · +85,00 €" "Erstattungsüberschuss"
            Expect.equal refund.AmountText "+85" "Betrag unter dem Balken"
            Expect.equal past.AmountText "1.235" "gerundet unter dem Balken"
        }
        // budgeting-v6m3z (ADR 0049): die Bewegungsliste ist eine ruhige Liste, Beträge neutral mit Vorzeichen.
        // budgeting-h7q4v (H2): geprüft wird `historyChangeAmount`, das die View rendert; vorher prüfte der
        // Test eine Textfunktion, die die View nicht nutzte. Die Klasse prüft RuhigeListenTests.fs
        // (Money.signedListAmountClass).
        test "Der Betrag einer Bewegungszeile trägt bei jeder Herkunft sein Vorzeichen, die Null keins" {
            let movement origin oldAssigned newAssigned : HistoryEntry =
                Movement
                    { MovementId = Guid.NewGuid()
                      ApplicationId = Guid.NewGuid()
                      OccurredAt = DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc)
                      Origin = origin
                      Month = "2026-10-01"
                      CategoryId = categoryA
                      CategoryName = "Lebensmittel"
                      OldAssigned = eur oldAssigned
                      NewAssigned = eur newAssigned
                      Counterpart = (if origin = (Decision MovementOrigin.MoneyMoved) then Some "Urlaub" else None)
                      Latest = None; ReplacedBy = None }
            for origin in [ (Decision MovementOrigin.MoneyMoved); (Decision MovementOrigin.MoneyAssigned); (Decision MovementOrigin.TargetsApplied) ] do
                let change entry = (historyEntryLine entry).Change |> Option.get
                let up = change (movement origin 30m 50m)
                let down = change (movement origin 50m 30m)
                Expect.equal (fst (historyChangeAmount up)) "+20,00 €" $"{origin}: positiv mit Plus"
                Expect.equal (fst (historyChangeAmount down)) "-20,00 €" $"{origin}: negativ mit Minus"
                Expect.equal (fst (historyChangeAmount (change (movement origin 30m 30m)))) "0,00 €" $"{origin}: Null ohne Vorzeichen"
        }
    ]

[<Tests>]
let answerTests =
    // budgeting-x4k7p Q1, Q2: Die Cmds laufen unter .NET nicht. Welche Nachricht eine Antwort wird und
    // welche Generation sie trägt, steht deshalb in reinen Funktionen, die die Cmds nutzen.
    let septemberMonth = { october with Month = "2026-09-01" }
    testList "Budget-Seite: Antworten der Abfragen" [
        test "Das stille Neu-Lesen des Monats antwortet über Reloaded, das volle über Loaded, mit der Generation beim Start" {
            let model = { page with MonthGeneration = 7 }
            Expect.equal (Components.Budget.Month.rereadAnswer Components.Budget.Month.QuietReread model (Ok october)) (Reloaded (7, Ok october)) "still, Erfolg"
            Expect.equal (Components.Budget.Month.rereadAnswer Components.Budget.Month.QuietReread model (Error "weg")) (Reloaded (7, Error "weg")) "still, Scheitern"
            Expect.equal (Components.Budget.Month.rereadAnswer Components.Budget.Month.FullReread model (Ok septemberMonth)) (Loaded (7, Ok septemberMonth)) "voll, Erfolg"
            Expect.equal (Components.Budget.Month.rereadAnswer Components.Budget.Month.FullReread model (Error "weg")) (Loaded (7, Error "weg")) "voll, Scheitern"
        }
        test "Die Abfrage der Buchungen bindet Erfolg und Scheitern an ihre Generation" {
            let onEntries = page |> step (SelectCategory categoryA) |> fun m -> { m with Tab = EntriesTab }
            let asked, request = requestTabData onEntries
            let request = Option.get request
            let answer = entriesRequestAnswer request
            Expect.equal (answer (Ok [ entry ])) (CategoryEntriesLoaded (asked.EntriesGeneration, Ok [ entry ])) "Erfolg"
            Expect.equal (answer (Error "weg")) (CategoryEntriesLoaded (asked.EntriesGeneration, Error "weg")) "Scheitern"
            let retried = asked |> step LoadCategoryEntries
            Expect.equal (retried |> step (answer (Ok [ entry ]))) retried "nach Wiederholen zählt die alte Antwort nicht"
            Expect.equal (retried |> step (answer (Error "weg"))) retried "auch ihr Scheitern nicht"
            Expect.equal (entriesOf (asked |> step (answer (Error "weg")))) (Failure "weg") "das Scheitern der laufenden steht im Tab"
        }
        test "Die Abfrage der Historie bindet Erfolg und Scheitern an ihre Generation, die stille antwortet über CategoryHistoryReloaded" {
            let onHistory = page |> step (SelectCategory categoryA) |> fun m -> { m with Tab = HistoryTab }
            let asked, request = requestTabData onHistory
            let answer = historyRequestAnswer (Option.get request)
            Expect.equal (answer (Ok loadedHistory)) (CategoryHistoryLoaded (asked.HistoryGeneration, Ok loadedHistory)) "Erfolg"
            Expect.equal (answer (Error "weg")) (CategoryHistoryLoaded (asked.HistoryGeneration, Error "weg")) "Scheitern"
            let shown = asked |> step (answer (Ok loadedHistory))
            let moved, quiet = historyAfterMovement shown
            let quietAnswer = historyRequestAnswer (Option.get quiet)
            Expect.equal (quietAnswer (Ok emptyHistory)) (CategoryHistoryReloaded (moved.HistoryGeneration, Ok emptyHistory)) "still, Erfolg"
            Expect.equal (quietAnswer (Error "weg")) (CategoryHistoryReloaded (moved.HistoryGeneration, Error "weg")) "still, Scheitern"
            let retried = moved |> step LoadCategoryHistory
            Expect.equal (retried |> step (quietAnswer (Ok emptyHistory))) retried "nach Wiederholen zählt die stille Antwort nicht"
        }
    ]

// budgeting-x4k7p Q3: Die Generationsregel der Tab-Daten als Aufzählung der Reihenfolgen durch das
// echte `update` (Doktrin Punkt 20, Vorbild `moveWays`). Das Orakel führt eigene Buchhaltung: welche
// Abfragen laufen, mit welcher Generation, und welchen Stand der Server hat (`Version` steigt mit jeder
// Bewegung). Es fragt nicht `tabDataDue` oder `requestTabData`.

/// Eine laufende Abfrage, wie das Orakel sie sah.
type private Asked =
    { Tab: InspectorTab
      Generation: int
      Quiet: bool
      Category: Guid
      Month: string
      Version: int }

/// Der Stand des Orakels: laufende Abfragen (auch überholte, ihre Antwort kommt noch), der Stand des
/// Servers, die Generation, bei der ein stilles Nachladen scheiterte, und ob der Monat gelesen wird.
type private TabWalk =
    { Asked: Asked list
      Version: int
      QuietFailedAt: int option
      /// Eine Abfrage des Monats läuft: bis zu ihrer Antwort darf ein Tab ungefragt warten.
      MonthPending: bool }

let private categoryCode (id: Guid) : decimal = if id = categoryA then 1000m else 2000m

/// Die Historie, wie der Server sie zu Kategorie, Monat und Stand liefert.
let private historyAt (category: Guid) (month: string) (version: int) : CategoryHistory =
    { Entries = []; Spending = [ { Month = month; Activity = eur (categoryCode category + decimal version) } ] }

/// Kategorie, Monat und Stand einer gelieferten Historie.
let private historyKey (history: CategoryHistory) : Guid * string * int =
    let spending = List.head history.Spending
    let code = spending.Activity.Amount
    let category = if code >= 2000m then categoryB else categoryA
    category, spending.Month, int (code - categoryCode category)

let private entriesAt (category: Guid) (month: string) : CategoryMonthEntry list =
    [ { entry with Payee = Some $"{category}|{month}" } ]

let private dataState (data: RemoteData<'T>) : string =
    match data with
    | NotAsked -> "NotAsked"
    | Loading -> "Loading"
    | Success _ -> "Success"
    | Failure _ -> "Failure"

let private tabState (tab: InspectorTab) (model: Model) : string =
    match model.Selected, tab with
    | Some s, HistoryTab -> dataState s.History
    | Some s, _ -> dataState s.Entries
    | None, _ -> "none"

let private tabGeneration (tab: InspectorTab) (model: Model) : int =
    match tab with
    | HistoryTab -> model.HistoryGeneration
    | _ -> model.EntriesGeneration

let private categoryOf (model: Model) : Guid option = model.Selected |> Option.map (fun s -> s.CategoryId)

let private loadedMonth (model: Model) : string option =
    match model.Month with
    | Success month -> Some month.Month
    | _ -> None

/// Der gezeigte Monat, eigens gerechnet.
let private shownMonthOf (model: Model) : string = model.ChosenMonth |> Option.defaultValue frame.CurrentMonth

let private changing (model: Model) : bool = loadedMonth model <> Some (shownMonthOf model)

let private monthAt (month: string) : BudgetMonth = { october with Month = month }

let private answerOf (asked: Asked) (ok: bool) : Msg =
    match asked.Tab, ok with
    | HistoryTab, true when asked.Quiet -> CategoryHistoryReloaded (asked.Generation, Ok (historyAt asked.Category asked.Month asked.Version))
    | HistoryTab, false when asked.Quiet -> CategoryHistoryReloaded (asked.Generation, Error "weg")
    | HistoryTab, true -> CategoryHistoryLoaded (asked.Generation, Ok (historyAt asked.Category asked.Month asked.Version))
    | HistoryTab, false -> CategoryHistoryLoaded (asked.Generation, Error "weg")
    | _, true -> CategoryEntriesLoaded (asked.Generation, Ok (entriesAt asked.Category asked.Month))
    | _, false -> CategoryEntriesLoaded (asked.Generation, Error "weg")

/// Was im Schritt geschieht: Name, Nachricht, ob es eine Bewegung ist, und welche Antwort.
type private TabEvent =
    { Name: string
      Msg: Msg
      Movement: bool
      Answer: (Asked * bool) option }

let private tabEvents (model: Model) (walk: TabWalk) : TabEvent list =
    let plain name msg = { Name = name; Msg = msg; Movement = false; Answer = None }
    [ plain "Tab Historie" (SelectInspectorTab HistoryTab)
      plain "Tab Buchungen" (SelectInspectorTab EntriesTab)
      plain "Kategorie A" (SelectCategory categoryA)
      plain "Kategorie B" (SelectCategory categoryB)
      plain "Anderer Monat" (SelectMonth (if shownMonthOf model = "2026-10-01" then "2026-09-01" else "2026-10-01"))
      { Name = "Zuweisung bestätigt"; Msg = AssignAnswered (Ok report); Movement = true; Answer = None }
      if walk.MonthPending then
          let month = Ok (monthAt (shownMonthOf model))
          if model.Freshness = MonthStale && not (changing model) then
              plain "Monat neu gelesen" (Reloaded (model.MonthGeneration, month))
          else
              plain "Monat geladen" (Loaded (model.MonthGeneration, month))
      match model.Tab, tabState model.Tab model with
      | HistoryTab, "Failure" -> plain "Wiederholen" LoadCategoryHistory
      | EntriesTab, "Failure" -> plain "Wiederholen" LoadCategoryEntries
      | _ -> ()
      for asked in walk.Asked do
          let what = $"{asked.Tab} {asked.Generation}"
          { Name = $"Antwort {what}"; Msg = answerOf asked true; Movement = false; Answer = Some (asked, true) }
          { Name = $"Fehler {what}"; Msg = answerOf asked false; Movement = false; Answer = Some (asked, false) } ]

/// Was die Aufzählung erreicht hat: Sie soll die Regel nicht im Leeren prüfen.
type private TabCoverage =
    { mutable QuietAsked: int
      mutable StaleAnswers: int
      mutable QuietFailures: int
      mutable Steps: int }

/// Ein Schritt durch das echte `update` mit dem Orakel. Gibt Modell und Orakel danach zurück.
let private tabStep (coverage: TabCoverage) (path: string list) (model: Model) (walk: TabWalk) (event: TabEvent) : Model * TabWalk =
    let shown = String.concat ", " (List.rev (event.Name :: path))
    let after, cmd, external = Components.Budget.State.update event.Msg model
    coverage.Steps <- coverage.Steps + 1
    let version = if event.Movement then walk.Version + 1 else walk.Version
    let remaining =
        match event.Answer with
        | Some (asked, _) -> walk.Asked |> List.filter (fun a -> a <> asked)
        | None -> walk.Asked
    // Keine Antwort einer alten Generation steht im Modell.
    let failedAt =
        match event.Answer with
        | Some (asked, ok) when asked.Generation <> tabGeneration asked.Tab model ->
            coverage.StaleAnswers <- coverage.StaleAnswers + 1
            Expect.equal after model $"alte Antwort ändert nichts: {shown}"
            Expect.equal external NoOp $"alte Antwort meldet nichts: {shown}"
            walk.QuietFailedAt
        | Some (asked, false) when asked.Quiet ->
            coverage.QuietFailures <- coverage.QuietFailures + 1
            Some asked.Generation
        | _ -> walk.QuietFailedAt
    // Welche Abfragen der Schritt gestartet hat, aus dem sichtbaren Zustand.
    let started =
        [ for tab in [ EntriesTab; HistoryTab ] do
              let generationMoved = tabGeneration tab model <> tabGeneration tab after
              let shownFetch =
                  tabState tab after = "Loading"
                  && (tabState tab model <> "Loading" || generationMoved || categoryOf model <> categoryOf after)
              let quietFetch =
                  tab = HistoryTab
                  && tabState tab model = "Success"
                  && tabState tab after = "Success"
                  && generationMoved
                  && after.Tab = HistoryTab
                  && categoryOf model = categoryOf after
                  && not (changing after)
              if shownFetch || quietFetch then
                  Expect.isNonEmpty cmd $"{tab}: Abfrage ohne Cmd: {shown}"
                  if quietFetch then coverage.QuietAsked <- coverage.QuietAsked + 1
                  { Tab = tab
                    Generation = tabGeneration tab after
                    Quiet = quietFetch
                    Category = Option.get (categoryOf after)
                    Month = Option.get (loadedMonth after)
                    Version = version } ]
    let monthAnswered =
        match event.Msg with
        | Loaded (generation, _)
        | Reloaded (generation, _) -> generation = model.MonthGeneration
        | _ -> false
    let monthPending = after.MonthGeneration <> model.MonthGeneration || (walk.MonthPending && not monthAnswered)
    let walk' = { Asked = remaining @ started; Version = version; QuietFailedAt = failedAt; MonthPending = monthPending }
    // Ein geladener Tab springt nach einer Bewegung nicht auf Loading.
    if event.Movement && model.Tab = HistoryTab && tabState HistoryTab model = "Success" && not (changing model) then
        Expect.equal (tabState HistoryTab after) "Success" $"Historie bleibt stehen: {shown}"
    match after.Selected with
    | Some selected ->
        let running tab = walk'.Asked |> List.exists (fun a -> a.Tab = tab && not a.Quiet && a.Generation = tabGeneration tab after)
        // Nie Loading ohne laufende Abfrage gleicher Generation.
        for tab in [ EntriesTab; HistoryTab ] do
            if tabState tab after = "Loading" then
                Expect.isTrue (running tab) $"{tab} Loading ohne Abfrage: {shown}"
        // Der sichtbare Tab hängt nicht ungefragt.
        if (after.Tab = EntriesTab || after.Tab = HistoryTab) && loadedMonth after |> Option.isSome && not walk'.MonthPending then
            Expect.notEqual (tabState after.Tab after) "NotAsked" $"{after.Tab} ungefragt: {shown}"
        // Was im Tab steht, gehört zu Kategorie und Monat.
        match selected.History, loadedMonth after with
        | Success history, Some month ->
            let category, forMonth, stand = historyKey history
            Expect.equal (category, forMonth) (selected.CategoryId, month) $"Historie zum Schlüssel: {shown}"
            // Nach Ruhe zeigt der Tab den Stand des Servers, außer das stille Nachladen scheiterte.
            let quietRunning = walk'.Asked |> List.exists (fun a -> a.Tab = HistoryTab && a.Generation = after.HistoryGeneration)
            if after.Tab = HistoryTab && not quietRunning && not (changing after) then
                Expect.isTrue
                    (stand = version || failedAt = Some after.HistoryGeneration)
                    $"Historie zeigt Stand {stand}, der Server {version}: {shown}"
        | _ -> ()
        match selected.Entries, loadedMonth after with
        | Success entries, Some month ->
            Expect.equal (entries |> List.map (fun e -> e.Payee)) [ Some $"{selected.CategoryId}|{month}" ] $"Buchungen zum Schlüssel: {shown}"
        | _ -> ()
    | None -> ()
    after, walk'

let private walkTabs (depth: int) : TabCoverage =
    let coverage = { QuietAsked = 0; StaleAnswers = 0; QuietFailures = 0; Steps = 0 }
    let visited = Collections.Generic.Dictionary<Model * TabWalk, int> HashIdentity.Structural
    let setup =
        [ { Name = "Kategorie A"; Msg = SelectCategory categoryA; Movement = false; Answer = None }
          { Name = "Tab Historie"; Msg = SelectInspectorTab HistoryTab; Movement = false; Answer = None } ]
    let start, startWalk =
        setup |> List.fold (fun (m, w) e -> tabStep coverage [] m w e) (page, { Asked = []; Version = 0; QuietFailedAt = None; MonthPending = false })
    let rec go (left: int) (path: string list) (model: Model) (walk: TabWalk) =
        for event in tabEvents model walk do
            let after, walk' = tabStep coverage path model walk event
            let key = after, walk'
            let seen, deepest = visited.TryGetValue key
            if left > 1 && not (seen && deepest >= left - 1) then
                visited[key] <- left - 1
                go (left - 1) (event.Name :: path) after walk'
    go depth [] start startWalk
    coverage

[<Tests>]
let generationTests =
    testList "Tab-Daten: Generationsregel über alle Reihenfolgen" [
        test "Kein Tab hängt in Loading, keine alte Antwort zählt, die geladene Historie springt nach einer Bewegung nicht" {
            let coverage = walkTabs 6
            Expect.isGreaterThan coverage.QuietAsked 0 "stilles Nachladen erreicht"
            Expect.isGreaterThan coverage.StaleAnswers 0 "alte Antworten erreicht"
            Expect.isGreaterThan coverage.QuietFailures 0 "gescheitertes stilles Nachladen erreicht"
        }
    ]
