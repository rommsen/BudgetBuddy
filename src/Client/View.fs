module View

open Feliz
open State
open Types

// ============================================
// Navigation Components (using emoji icons for simplicity)
// ============================================

let private navIcon (page: Page) =
    let icon =
        match page with
        | Dashboard -> "🏠"
        | SyncFlow -> "🔄"
        | Rules -> "📋"
        | Settings -> "⚙️"
    Html.span [
        prop.className "text-lg"
        prop.text icon
    ]

let private navItem (text: string) (page: Page) (currentPage: Page) (dispatch: Msg -> unit) =
    let isActive = page = currentPage
    Html.a [
        prop.className (
            "flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 cursor-pointer " +
            if isActive then
                "bg-primary text-primary-content shadow-lg"
            else
                "hover:bg-base-200 text-base-content/70 hover:text-base-content"
        )
        prop.onClick (fun _ -> dispatch (NavigateTo page))
        prop.children [
            navIcon page
            Html.span [
                prop.className "font-medium"
                prop.text text
            ]
        ]
    ]

let private mobileNavItem (text: string) (page: Page) (currentPage: Page) (dispatch: Msg -> unit) =
    let isActive = page = currentPage
    Html.a [
        prop.className (
            "flex flex-col items-center gap-1 py-2 px-3 rounded-lg transition-all cursor-pointer " +
            if isActive then
                "text-primary"
            else
                "text-base-content/60"
        )
        prop.onClick (fun _ -> dispatch (NavigateTo page))
        prop.children [
            navIcon page
            Html.span [
                prop.className "text-xs font-medium"
                prop.text text
            ]
        ]
    ]

let private navbar (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.children [
            // Desktop navbar
            Html.nav [
                prop.className "hidden md:flex fixed top-0 left-0 right-0 z-50 navbar bg-base-100/80 backdrop-blur-xl border-b border-base-200 px-6"
                prop.children [
                    // Logo/Brand
                    Html.div [
                        prop.className "flex-1"
                        prop.children [
                            Html.a [
                                prop.className "flex items-center gap-3 cursor-pointer group"
                                prop.onClick (fun _ -> dispatch (NavigateTo Dashboard))
                                prop.children [
                                    Html.div [
                                        prop.className "w-10 h-10 rounded-xl bg-gradient-to-br from-primary to-secondary flex items-center justify-center shadow-lg group-hover:shadow-xl transition-shadow text-white font-bold"
                                        prop.children [
                                            Html.span [
                                                prop.className "text-xl"
                                                prop.text "B"
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "flex flex-col"
                                        prop.children [
                                            Html.span [
                                                prop.className "text-lg font-bold gradient-text"
                                                prop.text "BudgetBuddy"
                                            ]
                                            Html.span [
                                                prop.className "text-xs text-base-content/50 -mt-1"
                                                prop.text "Smart Finance Sync"
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                    // Navigation links - Desktop
                    Html.div [
                        prop.className "flex-none"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-2"
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

            // Mobile header
            Html.nav [
                prop.className "md:hidden fixed top-0 left-0 right-0 z-50 navbar bg-base-100/90 backdrop-blur-xl border-b border-base-200 px-4"
                prop.children [
                    Html.a [
                        prop.className "flex items-center gap-2 cursor-pointer"
                        prop.onClick (fun _ -> dispatch (NavigateTo Dashboard))
                        prop.children [
                            Html.div [
                                prop.className "w-8 h-8 rounded-lg bg-gradient-to-br from-primary to-secondary flex items-center justify-center"
                                prop.children [
                                    Html.span [
                                        prop.className "text-sm font-bold text-white"
                                        prop.text "B"
                                    ]
                                ]
                            ]
                            Html.span [
                                prop.className "text-lg font-bold"
                                prop.text "BudgetBuddy"
                            ]
                        ]
                    ]
                ]
            ]

            // Mobile bottom navigation
            Html.nav [
                prop.className "md:hidden fixed bottom-0 left-0 right-0 z-50 bg-base-100/90 backdrop-blur-xl border-t border-base-200 safe-area-pb"
                prop.children [
                    Html.div [
                        prop.className "flex justify-around items-center py-2 px-2"
                        prop.children [
                            mobileNavItem "Home" Dashboard model.CurrentPage dispatch
                            mobileNavItem "Sync" SyncFlow model.CurrentPage dispatch
                            mobileNavItem "Rules" Rules model.CurrentPage dispatch
                            mobileNavItem "Settings" Settings model.CurrentPage dispatch
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
                        "alert shadow-lg " +
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
        prop.className "min-h-screen bg-gradient-to-br from-base-200 via-base-100 to-base-200"
        prop.children [
            // Navbar
            navbar model dispatch

            // Main content with padding for fixed navbar
            Html.main [
                prop.className "container mx-auto px-4 pt-20 pb-24 md:pb-8 animate-fade-in"
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
