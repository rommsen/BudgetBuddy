module View

open Feliz
open Feliz.Router
open State
open Types
open Client.DesignSystem

// ============================================
// Type Conversions
// ============================================

/// Convert Types.Page to Navigation.NavPage
let private toNavPage (page: Page) : Navigation.NavPage =
    match page with
    | SyncFlow -> Navigation.SyncFlow
    | Rules -> Navigation.Rules
    | Settings -> Navigation.Settings
    | QuickAdd -> Navigation.QuickAdd
    // Styleguide has no own nav tab; it's reached from Settings, so the
    // Settings tab stays highlighted while viewing the gallery.
    | Styleguide -> Navigation.Settings

/// Convert Navigation.NavPage to Types.Page
let private fromNavPage (navPage: Navigation.NavPage) : Page =
    match navPage with
    | Navigation.SyncFlow -> SyncFlow
    | Navigation.Rules -> Rules
    | Navigation.Settings -> Settings
    | Navigation.QuickAdd -> QuickAdd

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
    let hideBottomNav = false

    React.router [
        router.onUrlChanged (UrlChanged >> dispatch)
        router.children [
            Navigation.appWrapper [
                // Navigation (desktop top + mobile header/bottom)
                Navigation.navigation
                    (toNavPage model.CurrentPage)
                    (fun navPage -> dispatch (NavigateTo (fromNavPage navPage)))
                    hideBottomNav

                // Main content with padding for fixed navbar
                Navigation.pageContent [
                    // Each page has enter animation using key for re-triggering on page change
                    Html.div [
                        prop.key (model.CurrentPage.ToString())
                        prop.className "animate-page-enter"
                        prop.children [
                            match model.CurrentPage with
                            | SyncFlow ->
                                Components.SyncFlow.View.view
                                    model.SyncFlow
                                    (SyncFlowMsg >> dispatch)
                            | Rules ->
                                Components.Rules.View.view
                                    model.Rules
                                    (RulesMsg >> dispatch)
                            | Settings ->
                                Components.Settings.View.view
                                    model.Settings
                                    (SettingsMsg >> dispatch)
                            | QuickAdd ->
                                // Standalone Quick Add page. Categories/recents are
                                // shared YNAB structures already loaded into SyncFlow;
                                // templates are the recent Quick-Add bookings (ynab-t4n8p).
                                Views.QuickAddPage.view
                                    model.QuickAdd
                                    model.SyncFlow.Categories
                                    model.SyncFlow.RecentlyUsedCategoryIds
                                    model.QuickAddTemplates
                                    dispatch
                            | Styleguide ->
                                // Presentational gallery — no model/dispatch needed.
                                Components.Styleguide.View.view ()
                        ]
                    ]
                ]

                // Toast notifications using new Toast component
                Toast.renderList
                    (model.Toasts |> List.map (fun t -> (t.Id, t.Message, toToastVariant t.Type, t.Exiting)))
                    (fun id -> dispatch (StartDismissToast id))
            ]
        ]
    ]
