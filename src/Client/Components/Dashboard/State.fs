module Components.Dashboard.State

open Elmish
open Components.Dashboard.Types
open Types

let init () : Model * Cmd<Msg> =
    let model = {
        CurrentSession = NotAsked
        RecentSessions = NotAsked
        Settings = NotAsked
    }
    let cmd = Cmd.batch [
        Cmd.ofMsg LoadSettings
        Cmd.ofMsg LoadRecentSessions
        Cmd.ofMsg LoadCurrentSession
    ]
    model, cmd

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | LoadCurrentSession ->
        let cmd =
            Cmd.OfAsync.either
                Api.sync.getCurrentSession
                ()
                (Ok >> CurrentSessionLoaded)
                (fun ex -> Error ex.Message |> CurrentSessionLoaded)
        { model with CurrentSession = Loading }, cmd

    | CurrentSessionLoaded (Ok session) ->
        { model with CurrentSession = Success session }, Cmd.none

    | CurrentSessionLoaded (Error err) ->
        { model with CurrentSession = Failure err }, Cmd.none

    | LoadRecentSessions ->
        let cmd =
            Cmd.OfAsync.either
                Api.sync.getSyncHistory
                10
                (Ok >> RecentSessionsLoaded)
                (fun ex -> Error ex.Message |> RecentSessionsLoaded)
        { model with RecentSessions = Loading }, cmd

    | RecentSessionsLoaded (Ok sessions) ->
        { model with RecentSessions = Success sessions }, Cmd.none

    | RecentSessionsLoaded (Error err) ->
        { model with RecentSessions = Failure err }, Cmd.none

    | LoadSettings ->
        let cmd =
            Cmd.OfAsync.either
                Api.settings.getSettings
                ()
                (Ok >> SettingsLoaded)
                (fun ex -> Error ex.Message |> SettingsLoaded)
        { model with Settings = Loading }, cmd

    | SettingsLoaded (Ok settings) ->
        { model with Settings = Success settings }, Cmd.none

    | SettingsLoaded (Error err) ->
        { model with Settings = Failure err }, Cmd.none
