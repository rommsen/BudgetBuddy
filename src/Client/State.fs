module State

open System
open Elmish
open Shared.Domain
open Types

// ============================================
// Model - Composed from child component models
// ============================================

type Model = {
    // Navigation
    CurrentPage: Page

    // Toast notifications
    Toasts: Toast list

    // Child component models
    Dashboard: Components.Dashboard.Types.Model
    Settings: Components.Settings.Types.Model
    SyncFlow: Components.SyncFlow.Types.Model
    Rules: Components.Rules.Types.Model
}

// ============================================
// Messages - Composed from child component messages
// ============================================

type Msg =
    // Navigation
    | NavigateTo of Page

    // Toast
    | ShowToast of string * ToastType
    | DismissToast of Guid
    | AutoDismissToast of Guid

    // Child component messages
    | DashboardMsg of Components.Dashboard.Types.Msg
    | SettingsMsg of Components.Settings.Types.Msg
    | SyncFlowMsg of Components.SyncFlow.Types.Msg
    | RulesMsg of Components.Rules.Types.Msg

// ============================================
// Helper Functions
// ============================================

let private addToast (message: string) (toastType: ToastType) (model: Model) : Model * Cmd<Msg> =
    let toast = { Id = Guid.NewGuid(); Message = message; Type = toastType }
    let dismissCmd =
        Cmd.OfAsync.perform
            (fun () -> async { do! Async.Sleep 5000 })
            ()
            (fun _ -> AutoDismissToast toast.Id)
    { model with Toasts = toast :: model.Toasts }, dismissCmd

// ============================================
// Init
// ============================================

let init () : Model * Cmd<Msg> =
    let dashboardModel, dashboardCmd = Components.Dashboard.State.init ()
    let settingsModel, settingsCmd = Components.Settings.State.init ()
    let syncFlowModel, syncFlowCmd = Components.SyncFlow.State.init ()
    let rulesModel, rulesCmd = Components.Rules.State.init ()

    let model = {
        CurrentPage = Dashboard
        Toasts = []
        Dashboard = dashboardModel
        Settings = settingsModel
        SyncFlow = syncFlowModel
        Rules = rulesModel
    }

    let cmd = Cmd.batch [
        Cmd.map DashboardMsg dashboardCmd
        Cmd.map SettingsMsg settingsCmd  // Load settings on startup - needed for categories
    ]
    model, cmd

// ============================================
// Update - Delegates to child components
// ============================================

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    // ============================================
    // Navigation
    // ============================================
    | NavigateTo page ->
        let extraCmds =
            match page with
            | Dashboard ->
                Cmd.batch [
                    Cmd.map DashboardMsg (Cmd.ofMsg Components.Dashboard.Types.LoadLastSession)
                    Cmd.map DashboardMsg (Cmd.ofMsg Components.Dashboard.Types.LoadCurrentSession)
                    Cmd.map DashboardMsg (Cmd.ofMsg Components.Dashboard.Types.LoadSettings)
                ]
            | SyncFlow ->
                Cmd.batch [
                    Cmd.map SyncFlowMsg (Cmd.ofMsg Components.SyncFlow.Types.LoadCurrentSession)
                    Cmd.map SyncFlowMsg (Cmd.ofMsg Components.SyncFlow.Types.LoadCategories)
                ]
            | Rules ->
                Cmd.batch [
                    Cmd.map RulesMsg (Cmd.ofMsg Components.Rules.Types.LoadRules)
                    Cmd.map RulesMsg (Cmd.ofMsg Components.Rules.Types.LoadCategories)
                ]
            | Settings ->
                Cmd.map SettingsMsg (Cmd.ofMsg Components.Settings.Types.LoadSettings)
        { model with CurrentPage = page }, extraCmds

    // ============================================
    // Toast
    // ============================================
    | ShowToast (message, toastType) ->
        addToast message toastType model

    | DismissToast id ->
        { model with Toasts = model.Toasts |> List.filter (fun t -> t.Id <> id) }, Cmd.none

    | AutoDismissToast id ->
        { model with Toasts = model.Toasts |> List.filter (fun t -> t.Id <> id) }, Cmd.none

    // ============================================
    // Dashboard Component
    // ============================================
    | DashboardMsg dashboardMsg ->
        let dashboardModel', dashboardCmd = Components.Dashboard.State.update dashboardMsg model.Dashboard
        { model with Dashboard = dashboardModel' }, Cmd.map DashboardMsg dashboardCmd

    // ============================================
    // Settings Component
    // ============================================
    | SettingsMsg settingsMsg ->
        let settingsModel', settingsCmd, externalMsg = Components.Settings.State.update settingsMsg model.Settings

        // Handle external messages from Settings component
        let externalCmd =
            match externalMsg with
            | Components.Settings.Types.NoOp -> Cmd.none
            | Components.Settings.Types.ShowToast (message, toastType) -> Cmd.ofMsg (ShowToast (message, toastType))

        // Sync categories to other components when settings change
        let syncCategoriesCmd =
            match settingsMsg with
            | Components.Settings.Types.SettingsLoaded (Ok settings) ->
                match settings.Ynab with
                | Some ynab when ynab.DefaultBudgetId.IsSome ->
                    // Could reload categories for SyncFlow and Rules if needed
                    Cmd.none
                | _ -> Cmd.none
            | _ -> Cmd.none

        { model with Settings = settingsModel' }, Cmd.batch [ Cmd.map SettingsMsg settingsCmd; externalCmd; syncCategoriesCmd ]

    // ============================================
    // SyncFlow Component
    // ============================================
    | SyncFlowMsg syncFlowMsg ->
        let syncFlowModel', syncFlowCmd, externalMsg = Components.SyncFlow.State.update syncFlowMsg model.SyncFlow

        // Handle external messages from SyncFlow component
        let externalCmd =
            match externalMsg with
            | Components.SyncFlow.Types.NoOp -> Cmd.none
            | Components.SyncFlow.Types.ShowToast (message, toastType) -> Cmd.ofMsg (ShowToast (message, toastType))
            | Components.SyncFlow.Types.NavigateToDashboard -> Cmd.ofMsg (NavigateTo Dashboard)

        { model with SyncFlow = syncFlowModel' }, Cmd.batch [ Cmd.map SyncFlowMsg syncFlowCmd; externalCmd ]

    // ============================================
    // Rules Component
    // ============================================
    | RulesMsg rulesMsg ->
        // Special handling for LoadCategories - parent needs to load them
        match rulesMsg with
        | Components.Rules.Types.LoadCategories ->
            // Load categories from Settings context
            match model.Settings.Settings with
            | Success settings ->
                match settings.Ynab with
                | Some ynab ->
                    match ynab.DefaultBudgetId with
                    | Some budgetId ->
                        let cmd =
                            Cmd.OfAsync.either
                                Api.ynab.getCategories
                                budgetId
                                (fun result -> RulesMsg (Components.Rules.Types.CategoriesLoaded result))
                                (fun ex -> RulesMsg (Components.Rules.Types.CategoriesLoaded (Error (YnabError.NetworkError ex.Message))))
                        model, cmd
                    | None -> model, Cmd.none
                | None -> model, Cmd.none
            | _ -> model, Cmd.none
        | _ ->
            let rulesModel', rulesCmd, externalMsg = Components.Rules.State.update rulesMsg model.Rules

            // Handle external messages from Rules component
            let externalCmd =
                match externalMsg with
                | Components.Rules.Types.NoOp -> Cmd.none
                | Components.Rules.Types.ShowToast (message, toastType) -> Cmd.ofMsg (ShowToast (message, toastType))

            { model with Rules = rulesModel' }, Cmd.batch [ Cmd.map RulesMsg rulesCmd; externalCmd ]
