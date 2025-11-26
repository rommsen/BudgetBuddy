module State

open Elmish
open Shared.Domain
open Types

/// Application model
type Model = {
    Counter: RemoteData<Counter>
    IsAnimating: bool
    PreviousValue: int option
    DataPath: RemoteData<string>
}

/// Application messages
type Msg =
    | LoadCounter
    | CounterLoaded of Result<Counter, string>
    | IncrementCounter
    | CounterIncremented of Result<Counter, string>
    | StopAnimation
    | LoadDataPath
    | DataPathLoaded of Result<string, string>

/// Initialize the model and load counter
let init () : Model * Cmd<Msg> =
    let model = {
        Counter = NotAsked
        IsAnimating = false
        PreviousValue = None
        DataPath = NotAsked
    }
    let cmd = Cmd.batch [
        Cmd.ofMsg LoadCounter
        Cmd.ofMsg LoadDataPath
    ]
    model, cmd

/// Update function following the MVU pattern
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | LoadCounter ->
        let cmd =
            Cmd.OfAsync.either
                Api.api.getCounter
                ()
                (Ok >> CounterLoaded)
                (fun ex -> Error ex.Message |> CounterLoaded)
        { model with Counter = Loading }, cmd

    | CounterLoaded (Ok counter) ->
        { model with Counter = Success counter }, Cmd.none

    | CounterLoaded (Error err) ->
        { model with Counter = Failure err }, Cmd.none

    | IncrementCounter ->
        let previousValue =
            match model.Counter with
            | Success c -> Some c.Value
            | _ -> None
        let cmd =
            Cmd.OfAsync.either
                Api.api.incrementCounter
                ()
                (Ok >> CounterIncremented)
                (fun ex -> Error ex.Message |> CounterIncremented)
        { model with Counter = Loading; PreviousValue = previousValue }, cmd

    | CounterIncremented (Ok counter) ->
        let stopAnimationCmd =
            Cmd.OfAsync.perform
                (fun () -> async { do! Async.Sleep 400 })
                ()
                (fun () -> StopAnimation)
        { model with Counter = Success counter; IsAnimating = true }, stopAnimationCmd

    | CounterIncremented (Error err) ->
        { model with Counter = Failure err; IsAnimating = false }, Cmd.none

    | StopAnimation ->
        { model with IsAnimating = false }, Cmd.none

    | LoadDataPath ->
        let cmd =
            Cmd.OfAsync.either
                Api.api.getDataPath
                ()
                (Ok >> DataPathLoaded)
                (fun ex -> Error ex.Message |> DataPathLoaded)
        { model with DataPath = Loading }, cmd

    | DataPathLoaded (Ok path) ->
        { model with DataPath = Success path }, Cmd.none

    | DataPathLoaded (Error err) ->
        { model with DataPath = Failure err }, Cmd.none
