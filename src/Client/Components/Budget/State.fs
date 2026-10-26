/// Der Reduzierer der Budget-Seite (budgeting-k7wmd). Die Übergänge selbst sind reine
/// Funktionen in `Components.Budget.Types` (`select`, `applyLoaded`); hier hängen nur die
/// Seiteneffekte (Aufruf an den Server) daran.
module Components.Budget.State

open Elmish
open Shared.Budgeting
open Components.Budget.Types
open Types
open Client

let init () : Model = Components.Budget.Types.init ()

let private loadCmd (choice: BudgetMonthChoice) : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.budget.getBudgetMonth)
        (monthChoiceKey choice)
        (fun result -> Loaded (choice, result))
        (fun ex -> Loaded (choice, Error ex.Message))

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | Load -> { model with Month = Loading }, loadCmd model.Choice
    | SelectMonth choice ->
        match select choice model with
        | model', true -> model', loadCmd choice
        | model', false -> model', Cmd.none
    | Loaded (choice, result) -> applyLoaded choice result model, Cmd.none
