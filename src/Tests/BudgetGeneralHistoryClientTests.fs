module Tests.BudgetGeneralHistoryClientTests

// budgeting-h7q4v: die allgemeine Budget-Historie `#/budget/history` durch das echte `update` der
// Budget-Seite und der App. Die Cmds werden unter .NET nur gebaut, nie ausgeführt: der Proxy liegt
// hinter einem Lambda. Ausgeführt werden nur `Cmd.ofMsg`-Nachrichten der App.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.History

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private categoryA = Guid.Parse "00000000-0000-0000-0000-0000000e7a01"

let private october : BudgetMonth =
    { Month = "2026-09-01"
      ToBeBudgeted = eur 0m
      Groups =
        [ { Name = "Alltag"
            Categories =
              [ { Id = categoryA
                  Name = "Lebensmittel"
                  Assigned = eur 100m
                  Activity = eur -30m
                  Available = eur 70m
                  Underfunded = None
                  Target = None
                  Pause = None; OpenWriteBack = None; TargetInYnab = None; Deactivated = None } ] } ] }

let private frame : BudgetFrame =
    { CurrentMonth = "2026-10-01"
      Months = [ "2026-09-01"; "2026-10-01"; "2026-11-01" ]
      CategoryOrder = [ categoryA ]
      MonthStartHidden = [] }

/// Die Budget-Seite im September statt im laufenden Oktober, Kategorie A gewählt.
let private page : Model =
    { Components.Budget.Types.init () with
        Frame = Success frame
        Month = Success october
        ChosenMonth = Some "2026-09-01" }

let private step (msg: Msg) (model: Model) : Model =
    let next, _, _ = Components.Budget.State.update msg model
    next

let private movement (group: Guid) origin (name: string) oldAssigned newAssigned : HistoryMovement =
    { MovementId = Guid.NewGuid()
      ApplicationId = group
      OccurredAt = DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc)
      Origin = origin
      Month = "2026-10-01"
      CategoryId = Guid.NewGuid()
      CategoryName = name
      OldAssigned = eur oldAssigned
      NewAssigned = eur newAssigned
      Counterpart = None
      Latest = Some Written; ReplacedBy = None }

let private targetsGroup = Guid.NewGuid()
let private moveGroup = Guid.NewGuid()
let private assignGroup = Guid.NewGuid()

let private movements : HistoryMovement list =
    [ movement assignGroup (Decision MovementOrigin.MoneyAssigned) "Urlaub" 0m 20m
      movement moveGroup (Decision MovementOrigin.MoneyMoved) "Urlaub" 0m 20m
      movement moveGroup (Decision MovementOrigin.MoneyMoved) "Lebensmittel" 50m 30m
      movement targetsGroup (Decision MovementOrigin.TargetsApplied) "Miete" 0m 800m
      movement targetsGroup (Decision MovementOrigin.TargetsApplied) "Strom" 0m 60m ]

/// Eine Pause und eine Target-Änderung neben den Bewegungen (budgeting-r5h2n), älter als die Bewegungen.
let private pauseId = Guid.NewGuid()
let private changeId = Guid.NewGuid()

let private targetEntries : HistoryEntry list =
    [ TargetPaused
        { EventId = pauseId
          OccurredAt = DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc)
          Month = "2026-10-01"
          CategoryId = Guid.NewGuid()
          CategoryName = "Urlaub" }
      TargetChanged
        { ChangeId = changeId
          OccurredAt = DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)
          Month = "2026-10-01"
          CategoryId = Guid.NewGuid()
          CategoryName = "Urlaub"
          ChangeKind = TargetRemoved
          GoalBefore = Some (eur 50m)
          GoalAfter = None
          DateBefore = None
          DateAfter = None
          DayBefore = None
          DayAfter = None
          Latest = None
          DateStep = None
          SentToYnab = false } ]

/// Die Antwort des Servers: Bewegungen, Pause und Target-Änderung, neueste zuerst.
let private entries : HistoryEntry list = (movements |> List.map Movement) @ targetEntries

/// Die Antwort auf das jüngste Laden.
let private answer (result: Result<HistoryEntry list, string>) (model: Model) : Model =
    model |> step (GeneralHistoryLoaded (model.GeneralHistory.Generation, result))

let private loaded () : Model =
    page |> step GeneralHistoryEntered |> answer (Ok entries)

/// Die gezeigten Zeilen: ein Durchlauf mit seiner Herkunft, eine Target-Zeile mit ihrer Id.
let private shownRowsOf (model: Model) : Choice<HistoryOrigin, Guid> list =
    match model.GeneralHistory.Rows with
    | Success rows ->
        shownRows model.GeneralHistory.Filter rows
        |> List.map (function RunRow r -> Choice1Of2 r.Origin | TargetRow t -> Choice2Of2 t.EntryId)
    | other -> failtest $"Zeilen erwartet, kam {other}"

let private shownOrigins (model: Model) =
    shownRowsOf model |> List.choose (function Choice1Of2 origin -> Some origin | Choice2Of2 _ -> None)

let private runsOf (model: Model) : HistoryRun list =
    match model.GeneralHistory.Rows with
    | Success rows -> rows |> List.choose (function RunRow r -> Some r | TargetRow _ -> None)
    | other -> failtest $"Zeilen erwartet, kam {other}"

let private rootModel (page: Page) (budget: Model) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let syncFlowModel, _ = Components.SyncFlow.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    { CurrentPage = page
      Toasts = []
      Pwa = Pwa.initialState
      Settings = settingsModel
      SyncFlow = syncFlowModel
      Rules = rulesModel
      Ledger = ledgerModel
      Budget = budget
      QuickAdd = None
      QuickAddTemplates = NotAsked }

let private dispatched (cmd: Elmish.Cmd<State.Msg>) : State.Msg list =
    let collected = ResizeArray<State.Msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

[<Tests>]
let tests =
    testList "Allgemeine Historie: Ansicht" [
        test "#/budget/history wird geparst und erzeugt" {
            Expect.equal (Routing.parseUrl [ "budget"; "history" ]) BudgetHistory "parse"
            Expect.equal (Routing.toUrlSegments BudgetHistory) [ "budget"; "history" ] "toUrl"
        }
        test "Betreten lädt die Bewegungen, die Antwort wird zu Durchläufen, ein Fehler zur Fehlerkarte" {
            let entering, cmd, _ = Components.Budget.State.update GeneralHistoryEntered page
            Expect.equal entering.GeneralHistory.Rows Loading "lädt"
            Expect.isFalse (List.isEmpty cmd) "das Laden ist angestoßen"
            Expect.equal (shownOrigins (loaded ())) [ (Decision MovementOrigin.MoneyAssigned); (Decision MovementOrigin.MoneyMoved); (Decision MovementOrigin.TargetsApplied) ] "drei Durchläufe"
            let failed = entering |> answer (Error "kaputt")
            Expect.equal failed.GeneralHistory.Rows (Failure "kaputt") "Fehlerkarte"
        }
        // budgeting-c6vn4 (A1): verhindert, dass eine späte Antwort eines früheren Ladens (Retry.reading
        // wiederholt verzögert) eine neuere ersetzt: öffnen, zurück, buchen, wieder öffnen.
        test "Eine alte Antwort nach einer neueren ersetzt nichts" {
            let first = page |> step GeneralHistoryEntered
            let oldGeneration = first.GeneralHistory.Generation
            let fresh = first |> step GeneralHistoryEntered |> answer (Ok entries)
            let late = fresh |> step (GeneralHistoryLoaded (oldGeneration, Ok []))
            Expect.equal late.GeneralHistory.Rows fresh.GeneralHistory.Rows "die neuere Antwort bleibt"
            let failedLate = fresh |> step (GeneralHistoryLoaded (oldGeneration, Error "alt"))
            Expect.equal failedLate.GeneralHistory.Rows fresh.GeneralHistory.Rows "ein alter Fehler auch nicht"
        }
        // budgeting-c6vn4 (A2): "Wiederholen" setzte früher über `GeneralHistoryEntered` Filter und Aufklappen zurück.
        test "Wiederholen lädt neu und lässt Filter und Aufklappen stehen" {
            let chosen =
                loaded ()
                |> step (SelectRunFilter (RunsOf (Decision MovementOrigin.TargetsApplied)))
                |> step (ToggleRun targetsGroup)
            let retrying, cmd, _ = Components.Budget.State.update RetryGeneralHistory chosen
            Expect.equal retrying.GeneralHistory.Rows Loading "lädt"
            Expect.isFalse (List.isEmpty cmd) "das Laden ist angestoßen"
            Expect.equal retrying.GeneralHistory.Filter (RunsOf (Decision MovementOrigin.TargetsApplied)) "Filter bleibt"
            Expect.isTrue (retrying.GeneralHistory.Expanded.Contains targetsGroup) "aufgeklappt bleibt"
            let stale = retrying |> step (GeneralHistoryLoaded (chosen.GeneralHistory.Generation, Ok []))
            Expect.equal stale.GeneralHistory.Rows Loading "die Antwort vor dem Wiederholen zählt nicht"
        }
        // budgeting-r5h2n: auch der neue Filter "Targets" bleibt beim Wiederholen stehen.
        test "Wiederholen mit dem Filter Targets behält ihn, erneutes Betreten setzt auf Alle zurück" {
            let chosen = loaded () |> step (SelectRunFilter TargetChanges)
            let retried = chosen |> step RetryGeneralHistory |> answer (Ok entries)
            Expect.equal retried.GeneralHistory.Filter TargetChanges "Filter bleibt"
            Expect.equal (shownRowsOf retried) [ Choice2Of2 pauseId; Choice2Of2 changeId ] "weiter nur Target-Zeilen"
            Expect.equal (chosen |> step GeneralHistoryEntered).GeneralHistory.Filter AllRuns "von vorn"
        }
        test "Erneutes Betreten setzt Filter und Aufklappen zurück" {
            let again =
                loaded ()
                |> step (SelectRunFilter (RunsOf (Decision MovementOrigin.MoneyMoved)))
                |> step (ToggleRun targetsGroup)
                |> step GeneralHistoryEntered
            Expect.equal again.GeneralHistory.Filter AllRuns "Filter von vorn"
            Expect.isEmpty again.GeneralHistory.Expanded "alles zu"
        }
        test "Der Filter nach Art zeigt nur Durchläufe dieser Herkunft, Alle wieder alle" {
            for origin in [ (Decision MovementOrigin.TargetsApplied); (Decision MovementOrigin.MoneyMoved); (Decision MovementOrigin.MoneyAssigned) ] do
                Expect.equal (shownOrigins (loaded () |> step (SelectRunFilter (RunsOf origin)))) [ origin ] $"{origin}"
            let back = loaded () |> step (SelectRunFilter (RunsOf (Decision MovementOrigin.MoneyMoved))) |> step (SelectRunFilter AllRuns)
            Expect.equal (List.length (shownOrigins back)) 3 "Alle"
        }
        // budgeting-r5h2n: Target-Änderungen und Pausen stehen unter Alle gemischt, unter "Targets" allein und
        // unter keinem Chip einer Herkunft.
        test "Der Filter Targets zeigt nur Pausen und Target-Änderungen, Alle alles gemischt, Zuweisen keine Target-Zeile" {
            let targetRows = [ Choice2Of2 pauseId; Choice2Of2 changeId ]
            Expect.equal (shownRowsOf (loaded () |> step (SelectRunFilter TargetChanges))) targetRows "nur Target-Zeilen"
            let all = shownRowsOf (loaded ())
            Expect.equal (List.length all) 5 "drei Durchläufe und zwei Target-Zeilen"
            Expect.equal (all |> List.skip 3) targetRows "gemischt nach Zeitpunkt, die älteren Target-Zeilen hinten"
            Expect.equal
                (shownRowsOf (loaded () |> step (SelectRunFilter (RunsOf (Decision MovementOrigin.MoneyAssigned)))))
                [ Choice1Of2 (Decision MovementOrigin.MoneyAssigned) ]
                "Zuweisen ohne Target-Zeile"
        }
        test "Ein Tipp auf einen Targets-Durchlauf klappt seine Einzelzeilen auf, ein zweiter klappt zu" {
            let targetsRun (model: Model) = runsOf model |> List.find (fun r -> r.RunId = targetsGroup)
            let opened = loaded () |> step (ToggleRun targetsGroup)
            Expect.isTrue (isRunOpen opened.GeneralHistory (targetsRun opened)) "auf"
            let closed = opened |> step (ToggleRun targetsGroup)
            Expect.isFalse (isRunOpen closed.GeneralHistory (targetsRun closed)) "zu"
        }
        test "Nur ein Durchlauf Targets anwenden klappt auf" {
            Expect.equal (runsOf (loaded ()) |> List.map expandable) [ false; false; true ] "Zuweisen, Verschieben, Targets"
        }
        test "Der Knopf Historie im Seitenkopf führt zu #/budget/history" {
            let root, cmd = State.update (State.BudgetMsg OpenGeneralHistory) (rootModel Page.Budget page)
            Expect.contains (dispatched cmd) (State.NavigateTo BudgetHistory) "zur Historie"
            Expect.isTrue root.Budget.GeneralHistory.ReturnPending "die Seite wartet auf die Rückkehr"
        }
        // budgeting-c6vn4 (A9): über den Knopf geöffnet geht Zurück einen Schritt im Browser-Verlauf zurück,
        // statt einen neuen Eintrag anzulegen. Per Link geöffnet, führt es zur Budget-Seite.
        // budgeting-e6p4r: der echte Ablauf zwischen Knopf und Zurück (Verlassen, Betreten, Antwort) darf den
        // Merker nicht verlieren. Wurde die Historie dazwischen woandershin verlassen, führt Zurück zur Budget-Seite.
        test "Zurück geht nach dem Knopf Historie einen Schritt zurück, per Link geöffnet zur Budget-Seite" {
            let shown =
                page
                |> step OpenGeneralHistory
                |> step PageLeft
                |> step GeneralHistoryEntered
                |> answer (Ok entries)
            let _, _, viaButton = Components.Budget.State.update LeaveGeneralHistory shown
            Expect.equal viaButton StepBackToBudgetPage "ein Schritt zurück"
            let abandoned = page |> step OpenGeneralHistory |> step GeneralHistoryAbandoned |> step GeneralHistoryEntered
            let _, _, afterAbandon = Components.Budget.State.update LeaveGeneralHistory abandoned
            Expect.equal afterAbandon ShowBudgetPage "nach dem Verlassen woandershin wie per Link"
            let _, _, viaLink = Components.Budget.State.update LeaveGeneralHistory page
            Expect.equal viaLink ShowBudgetPage "neuer Eintrag"
            let _, cmd = State.update (State.BudgetMsg LeaveGeneralHistory) (rootModel Page.BudgetHistory page)
            Expect.contains (dispatched cmd) (State.NavigateTo Page.Budget) "zur Budget-Seite"
        }
        test "Zurück zur Budget-Seite: Monat und Auswahl bleiben erhalten" {
            let selected = page |> step (SelectCategory categoryA)
            let back =
                selected
                |> step OpenGeneralHistory
                |> step PageLeft
                |> step GeneralHistoryEntered
                |> answer (Ok entries)
                |> step PageEntered
            Expect.equal back.ChosenMonth (Some "2026-09-01") "der Monat bleibt"
            Expect.equal back.Month (Success october) "der Monat steht weiter da"
            Expect.equal (back.Selected |> Option.map (fun s -> s.CategoryId)) (Some categoryA) "die Auswahl bleibt"
            Expect.isFalse back.GeneralHistory.ReturnPending "angekommen"
        }
        test "Ohne den Knopf Historie, etwa per Link, beginnt die Budget-Seite beim laufenden Monat" {
            let fresh = page |> step GeneralHistoryEntered |> step PageEntered
            Expect.isNone fresh.ChosenMonth "laufender Monat"
        }
        test "Verlässt man die Historie auf eine andere Seite, beginnt die Budget-Seite danach von vorn" {
            let ledger, _ = Components.Ledger.State.init ()
            let messages (target: Page) = dispatched (State.urlChangedCmd ledger Page.BudgetHistory target)
            Expect.contains (messages Page.Settings) (State.BudgetMsg GeneralHistoryAbandoned) "woandershin"
            Expect.isFalse (messages Page.Budget |> List.contains (State.BudgetMsg GeneralHistoryAbandoned)) "zurück"
            let later = page |> step OpenGeneralHistory |> step GeneralHistoryAbandoned |> step PageEntered
            Expect.isNone later.ChosenMonth "laufender Monat"
        }
        // budgeting-c6vn4 (Q4): ohne `GeneralHistoryEntered` bliebe die Seite beim Skelett stehen.
        test "Der Wechsel von der Budget-Seite zur Historie meldet das Verlassen und lädt die Historie" {
            let ledger, _ = Components.Ledger.State.init ()
            let messages = dispatched (State.urlChangedCmd ledger Page.Budget Page.BudgetHistory)
            Expect.contains messages (State.BudgetMsg PageLeft) "PageLeft"
            Expect.contains messages (State.BudgetMsg GeneralHistoryEntered) "die Historie lädt"
        }
        test "Der Leertext nennt ohne Filter, was hier erscheint, mit Filter die Art" {
            Expect.equal
                (emptyText AllRuns)
                "Noch keine Einträge. Targets anwenden, Verschieben, Zuweisen, Target-Änderungen, Pausen und Änderungen aus YNAB erscheinen hier."
                "ohne Filter"
            Expect.equal (emptyText TargetChanges) "Keine Target-Änderungen oder Pausen" "Targets"
            Expect.equal (emptyText (RunsOf (Decision MovementOrigin.MoneyMoved))) "Keine Durchläufe dieser Art" "mit Filter"
            Expect.equal (emptyText (RunsOf HistoryOrigin.ObservedInYnab)) "Keine Zuweisungen in YNAB" "Beobachtung"
            Expect.equal (emptyText (RunsOf HistoryOrigin.TakenOverFromYnab)) "Keine Übernahme aus YNAB" "Übernahme"
        }
        // budgeting-dgrp7, Review-Fund: Beobachtung und Übernahme fehlten in der Filterleiste.
        test "Die Filterleiste führt auch, was aus YNAB kam, mit den Worten der Zeilen" {
            Expect.equal
                (runFilters |> List.map runFilterLabel)
                [ "Alle"; "Targets anwenden"; "Verschieben"; "Zuweisen"; "Targets"; "In YNAB zugewiesen"; "Aus YNAB übernommen" ]
                "Chips"
            let run origin : HistoryRun =
                { RunId = Guid.NewGuid(); OccurredAt = DateTime.UtcNow; Origin = origin; Month = "2026-10-01"
                  Text = ""; Amount = eur 1m; Status = RunFromYnab; Lines = [] }
            let rows = [ run HistoryOrigin.ObservedInYnab; run HistoryOrigin.TakenOverFromYnab; run (Decision MovementOrigin.MoneyAssigned) ] |> List.map RunRow
            Expect.equal (shownRows (RunsOf HistoryOrigin.ObservedInYnab) rows) [ rows[0] ] "Beobachtung"
            Expect.equal (shownRows (RunsOf HistoryOrigin.TakenOverFromYnab) rows) [ rows[1] ] "Übernahme"
        }
        test "Der Betrag rechts: Verschieben ohne Vorzeichen, Zuweisen und Targets mit, eine Null ohne" {
            let run origin amount : HistoryRun =
                { RunId = Guid.NewGuid(); OccurredAt = DateTime.UtcNow; Origin = origin; Month = "2026-10-01"
                  Text = ""; Amount = eur amount; Status = RunWritten; Lines = [] }
            Expect.equal (fst (runAmount (run (Decision MovementOrigin.MoneyMoved) 20m))) "20,00 €" "Verschieben"
            Expect.equal (fst (runAmount (run (Decision MovementOrigin.MoneyAssigned) -20m))) "-20,00 €" "zurück nach Zu verteilen"
            Expect.equal (fst (runAmount (run (Decision MovementOrigin.MoneyAssigned) 0m))) "0,00 €" "Zuweisen mit 0"
            Expect.equal (fst (runAmount (run (Decision MovementOrigin.TargetsApplied) 860m))) "+860,00 €" "Targets"
            // budgeting-dgrp7, Review-Fund: die Summe der Übernahme stand ohne Währung da.
            Expect.equal (fst (runAmount (run HistoryOrigin.TakenOverFromYnab 2400m))) "2.400,00 €" "Übernahme: Summe ohne Vorzeichen"
            Expect.equal (fst (runAmount (run HistoryOrigin.ObservedInYnab -20m))) "-20,00 €" "Beobachtung: Änderung mit Vorzeichen"
        }
        test "Ein Teil-Fehlschlag steht im Badge, am Handy kurz, ein Fehlschlag mit Grund wie bei einer Bewegung" {
            Expect.equal (runBadge (RunPartlyFailed (1, 3))) (PartlyFailedBadge ("1 von 3", "1 von 3 fehlgeschlagen")) "Teil"
            Expect.equal (runBadge (RunFailed "Rate-Limit")) (StatusBadge (HistoryFailed "Rate-Limit")) "Grund"
            Expect.equal (runBadge RunOpen) (StatusBadge HistoryOpen) "offen"
            Expect.equal (runBadge RunReplacedInBb) (StatusBadge HistoryReplacedInBb) "in BB ersetzt"
            Expect.equal (runBadge RunWithdrawnInBb) (StatusBadge HistoryWithdrawnInBb) "in BB zurückgezogen"
            Expect.equal (runBadge RunFromYnab) NoBadge "aus YNAB ohne Badge"
        }
        test "Der Monat steht am Handy kurz" {
            Expect.equal (shortMonthYearLabel "2026-11-01") "Nov. 2026" "November"
            Expect.equal (shortMonthYearLabel "2026-05-01") "Mai 2026" "Mai ohne Punkt"
            Expect.equal (shortMonthYearLabel "kaputt") "kaputt" "unlesbar bleibt"
        }
    ]
