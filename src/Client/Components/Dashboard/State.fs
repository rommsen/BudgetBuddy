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
            Cmd.OfAsync.perform
                Api.sync.getCurrentSession
                ()
                CurrentSessionLoaded
        { model with CurrentSession = Loading }, cmd

    | CurrentSessionLoaded session ->
        { model with CurrentSession = Success session }, Cmd.none

    | LoadRecentSessions ->
        let cmd =
            Cmd.OfAsync.perform
                Api.sync.getSyncHistory
                10
                RecentSessionsLoaded
        { model with RecentSessions = Loading }, cmd

    | RecentSessionsLoaded sessions ->
        { model with RecentSessions = Success sessions }, Cmd.none

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
