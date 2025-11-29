module View

open Feliz
open State
open Types

// ============================================
// Navigation Bar
// ============================================

let private navItem (text: string) (page: Page) (currentPage: Page) (dispatch: Msg -> unit) =
    let isActive = page = currentPage
    Html.li [
        Html.a [
            prop.className (if isActive then "active" else "")
            prop.text text
            prop.onClick (fun _ -> dispatch (NavigateTo page))
        ]
    ]

let private navbar (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "navbar bg-base-100 shadow-lg"
        prop.children [
            // Logo/Brand
            Html.div [
                prop.className "flex-1"
                prop.children [
                    Html.a [
                        prop.className "btn btn-ghost text-xl"
                        prop.onClick (fun _ -> dispatch (NavigateTo Dashboard))
                        prop.children [
                            Html.span [
                                prop.className "text-2xl mr-2"
                                prop.text "💰"
                            ]
                            Html.span [ prop.text "BudgetBuddy" ]
                        ]
                    ]
                ]
            ]
            // Navigation links
            Html.div [
                prop.className "flex-none"
                prop.children [
                    Html.ul [
                        prop.className "menu menu-horizontal px-1"
                        prop.children [
                            navItem "Dashboard" Dashboard model.CurrentPage dispatch
                            navItem "Sync" SyncFlow model.CurrentPage dispatch
                            navItem "Rules" Rules model.CurrentPage dispatch
                            navItem "Settings" Settings model.CurrentPage dispatch
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Toast Notifications
// ============================================

let private toasts (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "toast toast-end toast-bottom z-50"
        prop.children [
            for toast in model.Toasts do
                Html.div [
                    prop.key (toast.Id.ToString())
                    prop.className (
                        "alert " +
                        match toast.Type with
                        | ToastSuccess -> "alert-success"
                        | ToastError -> "alert-error"
                        | ToastInfo -> "alert-info"
                        | ToastWarning -> "alert-warning"
                    )
                    prop.children [
                        Html.span [ prop.text toast.Message ]
                        Html.button [
                            prop.className "btn btn-ghost btn-xs"
                            prop.text "×"
                            prop.onClick (fun _ -> dispatch (DismissToast toast.Id))
                        ]
                    ]
                ]
        ]
    ]

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "min-h-screen bg-base-200"
        prop.children [
            // Navbar
            navbar model dispatch

            // Main content
            Html.main [
                prop.className "container mx-auto p-4"
                prop.children [
                    match model.CurrentPage with
                    | Dashboard -> Views.DashboardView.view model dispatch
                    | SyncFlow -> Views.SyncFlowView.view model dispatch
                    | Rules -> Views.RulesView.view model dispatch
                    | Settings -> Views.SettingsView.view model dispatch
                ]
            ]

            // Toast notifications
            toasts model dispatch
        ]
    ]
