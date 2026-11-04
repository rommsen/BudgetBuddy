/// Die allgemeine Budget-Historie `#/budget/history` (budgeting-h7q4v): reine Übergänge. Die Durchläufe
/// bildet `historyRuns` in `Shared.Budgeting`, hier liegen Filter, Aufklappen und die Rückkehr.
module Components.Budget.History

open Shared.Budgeting
open Types
open Components.Budget.Types

/// Die Filter nach Art in ihrer Reihenfolge.
let runFilters : RunFilter list =
    [ AllRuns; RunsOf MovementOrigin.TargetsApplied; RunsOf MovementOrigin.MoneyMoved; RunsOf MovementOrigin.MoneyAssigned ]

let runFilterLabel (filter: RunFilter) : string =
    match filter with
    | AllRuns -> "Alle"
    | RunsOf MovementOrigin.TargetsApplied -> "Targets anwenden"
    | RunsOf MovementOrigin.MoneyMoved -> "Verschieben"
    | RunsOf MovementOrigin.MoneyAssigned -> "Zuweisen"

/// Die Durchläufe, die der Filter zeigt.
let shownRuns (filter: RunFilter) (runs: HistoryRun list) : HistoryRun list =
    match filter with
    | AllRuns -> runs
    | RunsOf origin -> runs |> List.filter (fun r -> r.Origin = origin)

/// Nur ein Durchlauf Targets anwenden klappt auf: Verschieben und Zuweisen stehen ganz in ihrer Zeile.
let expandable (run: HistoryRun) : bool =
    run.Origin = MovementOrigin.TargetsApplied && not (List.isEmpty run.Lines)

let private withHistory (model: Model) (change: GeneralHistory -> GeneralHistory) : Model =
    { model with GeneralHistory = change model.GeneralHistory }

/// Der Knopf Historie: die Budget-Seite merkt sich die Rückkehr.
let openGeneralHistory (model: Model) : Model =
    withHistory model (fun h -> { h with ReturnPending = true })

/// Die Seite wird betreten: die Bewegungen kommen neu, Filter und Aufklappen beginnen von vorn.
let enterGeneralHistory (model: Model) : Model =
    withHistory model (fun h -> { h with Runs = Loading; Filter = AllRuns; Expanded = Set.empty })

let applyGeneralHistoryLoaded (result: Result<HistoryMovement list, string>) (model: Model) : Model =
    withHistory model (fun h ->
        match result with
        | Ok movements -> { h with Runs = Success (historyRuns movements) }
        | Error message -> { h with Runs = Failure message })

let selectRunFilter (filter: RunFilter) (model: Model) : Model =
    withHistory model (fun h -> { h with Filter = filter })

/// Ein Tipp klappt auf, ein zweiter zu.
let toggleRun (runId: System.Guid) (model: Model) : Model =
    withHistory model (fun h ->
        let expanded =
            if h.Expanded.Contains runId then h.Expanded.Remove runId else h.Expanded.Add runId
        { h with Expanded = expanded })

/// Die Historie wurde woandershin verlassen: das nächste Betreten der Budget-Seite beginnt von vorn.
let abandonGeneralHistory (model: Model) : Model =
    withHistory model (fun h -> { h with ReturnPending = false })
