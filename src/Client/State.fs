module State

open Elmish
open Types

/// Application model
type Model = {
    // BudgetBuddy state will be defined here
    // See docs/MILESTONE-PLAN.md for complete model definition
    Placeholder: unit
}

/// Application messages
type Msg =
    // BudgetBuddy messages will be defined here
    | NoOp

/// Initialize the model
let init () : Model * Cmd<Msg> =
    let model = {
        Placeholder = ()
    }
    model, Cmd.none

/// Update function following the MVU pattern
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | NoOp -> model, Cmd.none
