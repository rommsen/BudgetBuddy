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

let private fromNavPage (navPage: Navigation.NavPage) : Page =
    match navPage with
    | Navigation.Overview -> Overview
    | Navigation.Ledger -> Ledger
    | Navigation.Budget -> Budget

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

// ============================================
// Rückmeldung (Regel R3, ADR 0098, design-system-gkded)
// ============================================

/// Der Ton der Meldung als Variante des Toasts am PC (Läuft als Info).
let private toToastVariant (tone: FeedbackTone) : Toast.ToastVariant =
    match tone with
    | FeedbackOk -> Toast.Success
    | FeedbackError -> Toast.Error
    | FeedbackWarning -> Toast.Warning
    | FeedbackInfo
    | FeedbackRunning -> Toast.Info

let private toFeedbackTone (tone: FeedbackTone) : Feedback.Tone =
    match tone with
    | FeedbackOk -> Feedback.Tone.Ok
    | FeedbackWarning -> Feedback.Tone.Warning
    | FeedbackError -> Feedback.Tone.Error
    | FeedbackRunning -> Feedback.Tone.Running
    | FeedbackInfo -> Feedback.Tone.Info

/// Der Toast am PC aus dem einen Platz: keiner, wenn er nach 5 s oder ✕ ausgeblendet ist.
let private toastItem (feedback: FeedbackMessage option) : Toast.ToastItem option =
    feedback
    |> Option.filter (fun m -> not m.ToastHidden)
    |> Option.map (fun m ->
        { Id = m.Id
          Message = FeedbackSlot.toastText m
          Variant = toToastVariant m.Tone
          Exiting = m.ToastExiting
          Action = m.Act |> Option.map (fun a -> { Label = a.Label }) })

/// Die Meldung in der Leiste unter 1024 px: ohne ✕ und ohne Zeitlimit.
let private barMessage (model: Model) (dispatch: Msg -> unit) : Feedback.Message option =
    model.Feedback
    |> Option.map (fun m ->
        { Tone = toFeedbackTone m.Tone
          Action = m.Action
          Detail = m.Detail
          Act = m.Act |> Option.map (fun a -> { Label = a.Label; OnClick = fun () -> dispatch (FeedbackActClicked m.Id) }) })

/// „+ Buchung“ in der Leiste: im Kassenbuch bis 1023 px (nicht bei offenem Formular, nicht im Auswahl-Modus), auf
/// der Übersicht bis 767 px (ab 768 px trägt der Kopf „Buchung“), ruhig wie bisher, nicht bei offenem Erfassen.
let private barAdd (model: Model) (dispatch: Msg -> unit) : Feedback.Add option =
    if isLedgerPage model.CurrentPage then
        if Components.Ledger.View.captureFabVisible model.Ledger then
            Some
                { TestId = "ledger-capture-fab"
                  Label = Components.Ledger.View.captureOpenLabel
                  AriaLabel = Components.Ledger.View.captureTitle
                  // Gefüllt: am Handy hat das Kassenbuch sonst keine Hauptaktion (§7.14, ledger-gy77x).
                  Quiet = false
                  OnClick = fun () -> dispatch (LedgerMsg Components.Ledger.Types.OpenCapture)
                  HideFrom = Feedback.AddBreakpoint.FromLg }
        else
            None
    elif model.CurrentPage = Overview && model.Ledger.Capture.IsNone then
        Some
            { TestId = "overview-capture-fab"
              Label = Components.Overview.View.captureLabel
              AriaLabel = Components.Overview.View.captureAriaLabel
              Quiet = Components.Overview.View.captureQuiet model.Overview
              OnClick = fun () -> dispatch CaptureFromOverview
              HideFrom = Feedback.AddBreakpoint.FromMd }
    else
        None

/// Wo die Leiste sitzt (styles.css): über der Navigation unten, ohne sie am Rand (Durchgehen) oder über der
/// Auswahl-Leiste am Handy.
type private FeedbackDockAt =
    | AboveNav
    | AtEdge
    | AboveSelection

let private dockAt (model: Model) : FeedbackDockAt =
    if model.CurrentPage = Review then AtEdge
    elif hideBottomNav model then AboveSelection
    else AboveNav

/// Die Hülle der Leiste misst deren Höhe nach `--feedback-bar-height` (ohne Leiste fehlt die Variable, es gilt 0):
/// die Seitenhülle hält den Platz frei (`Navigation.shellOffset`). `md:hidden`, wenn ab 768 px nur ein Knopf stünde,
/// den der Kopf trägt (Übersicht ohne Meldung).
[<ReactComponent>]
let private FeedbackDock (at: FeedbackDockAt) (hideFromMd: bool) (bar: ReactElement) =
    let disconnect = React.useRef<(unit -> unit) option> None
    let measure =
        React.useCallback (
            (fun (element: Browser.Types.Element) ->
                disconnect.current |> Option.iter (fun stop -> stop ())
                disconnect.current <- None
                if not (isNull (box element)) then
                    disconnect.current <- Some (Viewport.mirrorChildHeight "--feedback-bar-height" element)),
            [||]
        )
    Html.div [
        prop.ref measure
        prop.className (
            match at, hideFromMd with
            | AboveNav, false -> "feedback-dock-nav lg:hidden"
            | AboveNav, true -> "feedback-dock-nav lg:hidden md:hidden"
            | AtEdge, false -> "feedback-dock-edge lg:hidden"
            | AtEdge, true -> "feedback-dock-edge lg:hidden md:hidden"
            | AboveSelection, false -> "feedback-dock-selection lg:hidden"
            | AboveSelection, true -> "feedback-dock-selection lg:hidden md:hidden"
        )
        prop.children [ bar ]
    ]

let private feedbackBar (model: Model) (dispatch: Msg -> unit) : ReactElement =
    let message = barMessage model dispatch
    let add = barAdd model dispatch
    if not (Feedback.barVisible message add.IsSome) then
        Html.none
    else
        let onlyAddFromMd =
            message.IsNone && (add |> Option.exists (fun a -> a.HideFrom = Feedback.AddBreakpoint.FromMd))
        // Der Schlüssel bindet bei jedem Erscheinen einen neuen Knoten, damit die Messung die neue Leiste beobachtet.
        React.keyedFragment (
            "feedback-dock",
            [ FeedbackDock (dockAt model) onlyAddFromMd (Feedback.bar Feedback.BarPlacement.Docked message add) ]
        )

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
    // Kassenbuch (mit dem Tab Regeln) und Import prüfen füllen die Breite (ADR 0049 Punkt 9, ledger-n4vte,
    // ynab-sync-aj5tv): ihr Rahmen trägt keine `container`-Begrenzung (max. 1280px), die
    // Seite setzt ihre Ränder selbst.
    // Die Tabs des Kassenbuchs: „Buchungen“ und „Regeln“ sind Routen (categorization-n3sxw).
    let ledgerTabs (active: Components.Ledger.View.LedgerTab) : Components.Ledger.View.LedgerTabs =
        { Active = active
          ShowBookings = fun () -> if not (isLedgerPage model.CurrentPage) then dispatch (NavigateTo Ledger)
          Fetch = fun () -> dispatch FetchFromBank
          OverviewReviewCount = Components.Overview.Types.reviewCount model.Overview
          StartReview = fun () -> dispatch (NavigateTo Review)
          ShowRules = fun () -> if model.CurrentPage <> LedgerRules then dispatch (NavigateTo LedgerRules) }
    // Das Regel-Formular aus einer Buchung (ledger-hg5m5): es gehört zum Tab „Regeln“, das Kassenbuch zeigt es dort,
    // wo die Buchung steht.
    let entryRuleForm : Components.Ledger.View.EntryRuleFormSlot =
        Components.Rules.View.entryForm model.Rules (RulesMsg >> dispatch)
    let pageShell =
        match model.CurrentPage with
        | Overview | Ledger | LedgerEntry _ | LedgerRules | Budget | BudgetHistory | BudgetTargetCheck | BudgetCategories -> Navigation.pageContentFull
        // Das Durchgehen blendet die Leiste unten aus und passt am Handy ohne Scrollen (ledger-s2cg3).
        | Review -> Navigation.pageContentFlush
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
                    OverviewCount = Components.Overview.Types.reviewCount model.Overview
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
                                      Retry = fun () -> dispatch (OverviewMsg Components.Overview.Types.ReloadRequested)
                                      OpenTodo = fun target -> dispatch (OpenOverviewTodo target)
                                      Capture = fun () -> dispatch CaptureFromOverview
                                      OpenPinned = fun categoryId -> dispatch (OpenPinnedCategory categoryId)
                                      Dispatch = OverviewMsg >> dispatch }
                            | Settings ->
                                Components.Settings.View.view
                                    (back ())
                                    model.Settings
                                    (SettingsMsg >> dispatch)
                            | Review ->
                                Components.Ledger.View.ReviewPage
                                    model.Ledger
                                    (LedgerMsg >> dispatch)
                                    (fun () -> dispatch (NavigateTo Overview))
                                    entryRuleForm
                            | Ledger
                            | LedgerEntry _ ->
                                Components.Ledger.View.view
                                    model.Ledger
                                    (LedgerMsg >> dispatch)
                                    (ledgerTabs Components.Ledger.View.BookingsTab)
                                    entryRuleForm
                            | LedgerRules ->
                                let rulesDispatch = RulesMsg >> dispatch
                                Components.Ledger.View.view
                                    model.Ledger
                                    (LedgerMsg >> dispatch)
                                    (ledgerTabs (
                                        Components.Ledger.View.RulesTab
                                            { Subtitle = Components.Rules.View.subtitle model.Rules
                                              Actions = Components.Rules.View.headerActions rulesDispatch
                                              Content = Components.Rules.View.content model.Rules rulesDispatch }
                                    ))
                                    Components.Ledger.View.noEntryRuleForm
                            | Budget ->
                                Components.Budget.View.view
                                    (match model.Overview.Pins with
                                     | Success _ ->
                                         Some (
                                             { IsPinned = Components.Overview.Types.isPinned model.Overview
                                               Toggle = fun id name -> dispatch (TogglePinFromInspector (id, name)) }
                                             : Components.Budget.Types.InspectorPin
                                         )
                                     | _ -> None)
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
                if isLedgerPage model.CurrentPage then
                    Components.Ledger.View.selectionBars (LedgerMsg >> dispatch) model.Ledger
                // Das Formular „+ Buchung“ der Übersicht (ledger-s1weh) ist das des Kassenbuchs, ab 1024 px als
                // Dialog in der Mitte, darunter als Sheet über der Übersicht. Der Knopf steht in der Meldungsleiste.
                if model.CurrentPage = Overview then
                    Components.Ledger.View.captureSheet (LedgerMsg >> dispatch) model.Ledger
                    // Das Sheet „Angepinnt“ (budgeting-w9yt4).
                    Components.Overview.View.pinSheet model.Overview (OverviewMsg >> dispatch)

                // „Abrufen“ (banking-import-d0fey): Sheet am Handy, Dialog ab 1024 px, über jeder Seite.
                Components.Fetch.View.view
                    model.Fetch
                    (match model.Settings.Settings with
                     | Success settings -> settings.Sync.DaysToFetch
                     | _ -> 30)
                    (FetchMsg >> dispatch)

                // Der eine Meldungsplatz (Regel R3, ADR 0098): unter 1024 px die Leiste (`fixed`, außerhalb der
                // Seitenhülle mit der Eintrittsanimation), ab 1024 px höchstens ein Toast. Getrennt per CSS.
                feedbackBar model dispatch
                Toast.single
                    (toastItem model.Feedback)
                    (fun id -> dispatch (StartDismissToast id))
                    (fun id -> dispatch (FeedbackActClicked id))
            ]
        ]
    ]
