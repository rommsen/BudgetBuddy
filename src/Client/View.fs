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
    | Ledger -> Navigation.Ledger
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
    | Navigation.Ledger -> Ledger

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
    // Das Kassenbuch füllt die Breite (ADR 0049 Punkt 9, ledger-n4vte): sein Rahmen
    // trägt keine `container`-Begrenzung (max. 1280px), die Seite setzt ihre Ränder selbst.
    let pageShell =
        match model.CurrentPage with
        | Ledger -> Navigation.pageContentFull
        | _ -> Navigation.pageContent

    React.router [
        router.onUrlChanged (UrlChanged >> dispatch)
        router.children [
            Navigation.appWrapper [
                // Bereitschaftssignal für scripts/screenshot-route.mjs (design-system-yxrxb):
                // ein unsichtbares, immer vorhandenes Element, dessen Attribut
                // `data-app-ready` `State.isPageReady model` widerspiegelt. Ersetzt die
                // frühere Heuristik über CSS-Klassen ("proto-spinner"/"animate-pulse"),
                // die auf /styleguide nie auf "fertig" ging, weil deren Spinner/Skeletons
                // Demo-Inhalte sind, keine echten Ladezustände. Als eigenes Element statt
                // als Attribut auf appWrapper selbst, um dessen feste Prop-Liste
                // (`Navigation.appWrapper`) unangetastet zu lassen.
                Html.div [
                    prop.id "app-ready"
                    prop.custom ("data-app-ready", if isPageReady model then "true" else "false")
                    prop.style [ style.display.none ]
                ]

                // Navigation (desktop top + mobile header/bottom)
                Navigation.navigation
                    (toNavPage model.CurrentPage)
                    (fun navPage -> dispatch (NavigateTo (fromNavPage navPage)))
                    hideBottomNav

                // Main content with padding for fixed navbar
                pageShell [
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
                            | Ledger ->
                                Components.Ledger.View.view
                                    model.Ledger
                                    (LedgerMsg >> dispatch)
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
