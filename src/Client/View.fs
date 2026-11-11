module View

open Feliz
open Feliz.Router
open State
open Types
open Client.DesignSystem

// ============================================
// Type Conversions
// ============================================

/// Der Ort als Eintrag der Navigation.
let private toNavPage (place: Place) : Navigation.NavPage =
    match place with
    | OverviewPlace -> Navigation.Overview
    | LedgerPlace -> Navigation.Ledger
    | BudgetPlace -> Navigation.Budget
    | QuickAddPlace -> Navigation.QuickAdd

let private fromNavPage (navPage: Navigation.NavPage) : Page =
    match navPage with
    | Navigation.Overview -> Overview
    | Navigation.Ledger -> Ledger
    | Navigation.Budget -> Budget
    | Navigation.QuickAdd -> QuickAdd

/// Die Kontozeilen der Seitenleiste: „Alle Konten“ und jedes Konto, dieselben Karten wie im Kassenbuch.
/// Markiert ist das gewählte Konto nur, solange das Kassenbuch offen ist. Laden, Fehler und leere Liste nach
/// `accountsListState`.
let private sidebarAccounts (model: Model) (dispatch: Msg -> unit) : Navigation.SidebarAccounts =
    match Components.Overview.Types.accountsListState model.Overview.Accounts with
    | Components.Overview.Types.AccountsListLoading -> Navigation.SidebarAccountsLoading
    | Components.Overview.Types.AccountsListFailed _ ->
        Navigation.SidebarAccountsFailed (
            "Konten konnten nicht geladen werden",
            fun () -> dispatch (OverviewMsg Components.Overview.Types.SidebarNeedsAccounts)
        )
    | Components.Overview.Types.AccountsListEmpty ->
        Navigation.SidebarAccountsEmpty (Components.Overview.Types.noAccountsTitle, Components.Overview.Types.noAccountsText)
    | Components.Overview.Types.AccountsListShown accounts ->
        let selected = if isLedgerPage model.CurrentPage then model.Ledger.SelectedFilter else None
        let row (card: Components.Ledger.View.AccountCard) : Navigation.SidebarAccount =
            { Key = Components.Ledger.State.encodeAccountFilter card.Filter
              Label = card.Label
              Value =
                match card.Balance with
                | Components.Ledger.View.BalanceShown money -> Navigation.AccountAmount (money.Amount, money.Currency)
                | Components.Ledger.View.BalancePending -> Navigation.AccountPending
                | Components.Ledger.View.BalanceUnavailable -> Navigation.AccountUnavailable
              IsActive = selected = Some card.Filter
              OnClick = fun () -> dispatch (OpenLedgerAccount card.Filter) }
        Components.Ledger.View.buildAccountCards accounts model.Overview.Balances
        |> List.map row
        |> Navigation.SidebarAccountsShown

/// Convert Types.ToastType to Toast.ToastVariant
let private toToastVariant (toastType: ToastType) : Toast.ToastVariant =
    match toastType with
    | ToastSuccess -> Toast.Success
    | ToastError -> Toast.Error
    | ToastWarning -> Toast.Warning
    | ToastInfo -> Toast.Info

/// Types.Toast als Eintrag der DS-Toast-Liste (inklusive der optionalen Aktion).
let private toToastItem (t: Types.Toast) : Toast.ToastItem =
    { Id = t.Id
      Message = t.Message
      Variant = toToastVariant t.Type
      Exiting = t.Exiting
      Action = t.Action |> Option.map (fun a -> { Label = a.Label }) }

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    let bottomNavHidden = hideBottomNav model
    // „Zurück“ einer Unterseite; das Ziel rechnet `backTarget` (Types.fs).
    let back () =
        match backTarget model.CurrentPage model.SettingsReturn with
        | Some target -> PageHeader.backButtonFor (Some target.Label) (fun () -> dispatch (NavigateTo target.Target))
        | None -> Html.none
    // Kassenbuch, Import prüfen und Regeln füllen die Breite (ADR 0049 Punkt 9, ledger-n4vte,
    // ynab-sync-aj5tv): ihr Rahmen trägt keine `container`-Begrenzung (max. 1280px), die
    // Seite setzt ihre Ränder selbst.
    let pageShell =
        match model.CurrentPage with
        | Overview | Ledger | LedgerEntry _ | Rules | Budget | BudgetHistory | BudgetTargetCheck | BudgetCategories | SyncFlow -> Navigation.pageContentFull
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

                // Navigation: Leiste unten am Handy, Rail von 768 bis 1023 px, Seitenleiste ab 1024 px.
                Navigation.navigation {
                    Current = placeOf model.CurrentPage |> Option.map toNavPage
                    OnNavigate = fun navPage -> dispatch (NavigateTo (fromNavPage navPage))
                    SettingsActive = (model.CurrentPage = Settings || model.CurrentPage = Styleguide)
                    OnSettings = fun () -> dispatch (NavigateTo Settings)
                    OnBrand = fun () -> dispatch (NavigateTo Overview)
                    Accounts = sidebarAccounts model dispatch
                    HideBottomBar = bottomNavHidden
                }

                // Main content with padding for fixed navbar
                pageShell [
                    // Each page has enter animation using key for re-triggering on page change
                    Html.div [
                        prop.key (pageKey model.CurrentPage)
                        prop.className "animate-page-enter"
                        prop.children [
                            match model.CurrentPage with
                            | Overview ->
                                Components.Overview.View.view
                                    model.Overview
                                    { Fetch = fun () -> dispatch FetchFromBank
                                      OpenSettings = fun () -> dispatch (NavigateTo Settings)
                                      OpenBudget = fun () -> dispatch (NavigateTo Budget)
                                      OpenOverdrawn = fun () -> dispatch OpenBudgetOverdrawn
                                      OpenAccount = fun filter -> dispatch (OpenLedgerAccount filter)
                                      OpenEntry = fun entryId -> dispatch (NavigateTo (LedgerEntry entryId))
                                      Retry = fun () -> dispatch (OverviewMsg Components.Overview.Types.ReloadRequested) }
                            | SyncFlow ->
                                Html.div [
                                    prop.className "px-4 md:px-6"
                                    prop.children [
                                        // Der Sync-Ablauf trägt seine Titel selbst; der Kopf führt zurück zur Übersicht.
                                        back ()
                                    ]
                                ]
                                Components.SyncFlow.View.view
                                    model.SyncFlow
                                    (SyncFlowMsg >> dispatch)
                            | Rules ->
                                Components.Rules.View.view
                                    (back ())
                                    model.Rules
                                    (RulesMsg >> dispatch)
                            | Settings ->
                                Components.Settings.View.view
                                    (back ())
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
                            | Ledger
                            | LedgerEntry _ ->
                                Components.Ledger.View.view
                                    model.Ledger
                                    (LedgerMsg >> dispatch)
                            | Budget ->
                                Components.Budget.View.view
                                    model.Budget
                                    (BudgetMsg >> dispatch)
                            | BudgetHistory ->
                                Components.Budget.HistoryView.view
                                    model.Budget.GeneralHistory
                                    (BudgetMsg >> dispatch)
                            | BudgetTargetCheck ->
                                Components.Budget.TargetCheckView.view
                                    model.Budget
                                    (BudgetMsg >> dispatch)
                            | BudgetCategories ->
                                Components.Budget.CategoriesView.view
                                    model.Budget.CategoryAdmin
                                    (Components.Budget.Types.CategoryAdminMsg >> BudgetMsg >> dispatch)
                            | Styleguide ->
                                // Presentational gallery — no model/dispatch needed.
                                Components.Styleguide.View.view
                                    (back ())
                        ]
                    ]
                ]

                // Auswahl-Leiste des Kassenbuchs (ledger-r1d2f): `fixed`, deshalb außerhalb der
                // Seitenhülle mit der Eintrittsanimation.
                // Ebenso der schwebende Knopf "+ Buchung" (ledger-at63w): in der Hülle haftete er an ihr
                // statt am Fenster und stand erst am Ende der Liste im Bild.
                if isLedgerPage model.CurrentPage then
                    Components.Ledger.View.selectionBars (LedgerMsg >> dispatch) model.Ledger
                    Components.Ledger.View.captureFab (LedgerMsg >> dispatch) model.Ledger

                // Toast notifications using new Toast component
                Toast.renderItems
                    (model.Toasts |> List.map toToastItem)
                    (fun id -> dispatch (StartDismissToast id))
                    (fun id -> dispatch (ToastActionClicked id))
            ]
        ]
    ]
