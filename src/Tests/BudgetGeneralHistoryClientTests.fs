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
                  Pause = None } ] } ] }

let private frame : BudgetFrame =
    { CurrentMonth = "2026-10-01"
      Months = [ "2026-09-01"; "2026-10-01"; "2026-11-01" ]
      CategoryOrder = [ categoryA ] }

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
      Latest = Some Written }

let private targetsGroup = Guid.NewGuid()
let private moveGroup = Guid.NewGuid()
let private assignGroup = Guid.NewGuid()

let private movements : HistoryMovement list =
    [ movement assignGroup MovementOrigin.MoneyAssigned "Urlaub" 0m 20m
      movement moveGroup MovementOrigin.MoneyMoved "Urlaub" 0m 20m
      movement moveGroup MovementOrigin.MoneyMoved "Lebensmittel" 50m 30m
      movement targetsGroup MovementOrigin.TargetsApplied "Miete" 0m 800m
      movement targetsGroup MovementOrigin.TargetsApplied "Strom" 0m 60m ]

let private loaded () : Model =
    page |> step GeneralHistoryEntered |> step (GeneralHistoryLoaded (Ok movements))

let private shownOrigins (model: Model) =
    match model.GeneralHistory.Runs with
    | Success runs -> shownRuns model.GeneralHistory.Filter runs |> List.map (fun r -> r.Origin)
    | other -> failtest $"Durchläufe erwartet, kam {other}"

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
            Expect.equal entering.GeneralHistory.Runs Loading "lädt"
            Expect.equal (List.length cmd) 1 "eine Abfrage"
            Expect.equal (shownOrigins (loaded ())) [ MovementOrigin.MoneyAssigned; MovementOrigin.MoneyMoved; MovementOrigin.TargetsApplied ] "drei Durchläufe"
            let failed = entering |> step (GeneralHistoryLoaded (Error "kaputt"))
            Expect.equal failed.GeneralHistory.Runs (Failure "kaputt") "Fehlerkarte"
        }
        test "Der Filter nach Art zeigt nur Durchläufe dieser Herkunft, Alle wieder alle" {
            for origin in [ MovementOrigin.TargetsApplied; MovementOrigin.MoneyMoved; MovementOrigin.MoneyAssigned ] do
                Expect.equal (shownOrigins (loaded () |> step (SelectRunFilter (RunsOf origin)))) [ origin ] $"{origin}"
            let back = loaded () |> step (SelectRunFilter (RunsOf MovementOrigin.MoneyMoved)) |> step (SelectRunFilter AllRuns)
            Expect.equal (List.length (shownOrigins back)) 3 "Alle"
            Expect.equal (runFilters |> List.map runFilterLabel) [ "Alle"; "Targets anwenden"; "Verschieben"; "Zuweisen" ] "Chips"
        }
        test "Ein Tipp auf einen Targets-Durchlauf klappt seine Einzelzeilen auf, ein zweiter klappt zu" {
            let opened = loaded () |> step (ToggleRun targetsGroup)
            Expect.isTrue (opened.GeneralHistory.Expanded.Contains targetsGroup) "auf"
            Expect.isFalse ((opened |> step (ToggleRun targetsGroup)).GeneralHistory.Expanded.Contains targetsGroup) "zu"
        }
        test "Nur ein Durchlauf Targets anwenden klappt auf" {
            match (loaded ()).GeneralHistory.Runs with
            | Success runs ->
                Expect.equal (runs |> List.map expandable) [ false; false; true ] "Zuweisen, Verschieben, Targets"
            | other -> failtest $"{other}"
        }
        test "Der Knopf Historie im Seitenkopf führt zu #/budget/history" {
            let root, cmd = State.update (State.BudgetMsg OpenGeneralHistory) (rootModel Page.Budget page)
            Expect.contains (dispatched cmd) (State.NavigateTo BudgetHistory) "zur Historie"
            Expect.isTrue root.Budget.GeneralHistory.ReturnPending "die Seite wartet auf die Rückkehr"
        }
        test "Zurück führt zur Budget-Seite, Monat und Auswahl bleiben erhalten" {
            let _, cmd = State.update (State.BudgetMsg LeaveGeneralHistory) (rootModel Page.BudgetHistory page)
            Expect.contains (dispatched cmd) (State.NavigateTo Page.Budget) "zur Budget-Seite"
            let selected = page |> step (SelectCategory categoryA)
            let back =
                selected
                |> step OpenGeneralHistory
                |> step PageLeft
                |> step GeneralHistoryEntered
                |> step (GeneralHistoryLoaded (Ok movements))
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
        test "Die Budget-Seite erfährt, dass sie für die Historie verlassen wird" {
            let ledger, _ = Components.Ledger.State.init ()
            Expect.contains (dispatched (State.urlChangedCmd ledger Page.Budget Page.BudgetHistory)) (State.BudgetMsg PageLeft) "PageLeft"
        }
    ]
