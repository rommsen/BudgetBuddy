module View

open Feliz
open State
open Types
open Client.DesignSystem

// ============================================
// Type Conversions
// ============================================

/// Convert Types.Page to Navigation.NavPage
let private toNavPage (page: Page) : Navigation.NavPage =
    match page with
    | Dashboard -> Navigation.Dashboard
    | SyncFlow -> Navigation.SyncFlow
    | Rules -> Navigation.Rules
    | Settings -> Navigation.Settings

/// Convert Navigation.NavPage to Types.Page
let private fromNavPage (navPage: Navigation.NavPage) : Page =
    match navPage with
    | Navigation.Dashboard -> Dashboard
    | Navigation.SyncFlow -> SyncFlow
    | Navigation.Rules -> Rules
    | Navigation.Settings -> Settings

/// Convert Types.ToastType to Toast.ToastVariant
let private toToastVariant (toastType: ToastType) : Toast.ToastVariant =
    match toastType with
    | ToastSuccess -> Toast.Success
    | ToastError -> Toast.Error
    | ToastWarning -> Toast.Warning
    | ToastInfo -> Toast.Info

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    Navigation.appWrapper [
        // Navigation (desktop top + mobile header/bottom)
        Navigation.navigation
            (toNavPage model.CurrentPage)
            (fun navPage -> dispatch (NavigateTo (fromNavPage navPage)))

        // Main content with padding for fixed navbar
        Navigation.pageContent [
            // Each page has enter animation using key for re-triggering on page change
            Html.div [
                prop.key (model.CurrentPage.ToString())
                prop.className "animate-page-enter"
                prop.children [
                    match model.CurrentPage with
                    | Dashboard ->
                        Components.Dashboard.View.view
                            model.Dashboard
                            (DashboardMsg >> dispatch)
                            (fun () -> dispatch (NavigateTo SyncFlow))
                            (fun () -> dispatch (NavigateTo Settings))
                    | SyncFlow ->
                        Components.SyncFlow.View.view
                            model.SyncFlow
                            (SyncFlowMsg >> dispatch)
                            (fun () -> dispatch (NavigateTo Dashboard))
                    | Rules ->
                        Components.Rules.View.view
                            model.Rules
                            (RulesMsg >> dispatch)
                    | Settings ->
                        Components.Settings.View.view
                            model.Settings
                            (SettingsMsg >> dispatch)
                ]
            ]
        ]

        // Toast notifications using new Toast component
        Toast.renderList
            (model.Toasts |> List.map (fun t -> (t.Id, t.Message, toToastVariant t.Type)))
            (fun id -> dispatch (DismissToast id))
    ]
