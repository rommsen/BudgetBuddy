/// Die Übersicht (design-system-k6kfw, ledger-s1weh; Prototyp Version 17 `homeHead`, `todoList`, `monthBlock`, `home`
/// Variante A, `homeDesk`): Kopf mit „Abrufen“ und am PC „Buchung“, zuerst „Zu tun“, dann der Monat als Karte fester
/// Höhe, am Handy die Konten, ab 768 px die neuesten Buchungen. Ab 1024 px zwei Spalten.
module Components.Overview.View

open System
open Feliz
open Shared.Domain
open Types
open Client.DesignSystem
open Client.DesignSystem.Tokens
open Components.Overview.Types

/// Was die Übersicht auslöst. Die Wurzel übersetzt es in ihre Nachrichten.
type Actions = {
    Fetch: unit -> unit
    OpenSettings: unit -> unit
    OpenBudget: unit -> unit
    OpenOverdrawn: unit -> unit
    OpenAccount: Components.Ledger.Types.AccountFilter -> unit
    OpenEntry: Guid -> unit
    Retry: unit -> unit
    /// Ein Eintrag aus „Zu tun“ (ledger-s1weh).
    OpenTodo: TodoTarget -> unit
    /// „+ Buchung“: das Erfassen aus dem Kassenbuch (ledger-at63w), die Übersicht bleibt stehen.
    Capture: unit -> unit
}

/// Die Beschriftung von „+ Buchung“ und der Name für den Screenreader.
let captureLabel = "Buchung"
let captureAriaLabel = "Neue Buchung"

/// Abschnittskopf (Prototyp `.sec-h`): Mono 11 px, Großbuchstaben, gedämpft, rechts optional ein Wert.
let private sectionHead (title: string) (right: ReactElement option) =
    Html.div [
        prop.className $"flex items-baseline justify-between px-0.5 pt-2 {Fonts.mono} {FontSizes.microPlus} uppercase tracking-[0.14em] {Colors.textMuted}"
        prop.children [
            Html.span [ prop.text title ]
            match right with
            | Some r -> r
            | None -> Html.none
        ]
    ]

let private cardClass = $"{Backgrounds.dark} border {Borders.subtle} rounded-[14px]"

/// Überfahren am PC und Tippen am Handy wie in den neuesten Buchungen.
let private rowHover = "hover:bg-surface-hover active:bg-surface-hover"

let private head (actions: Actions) (importStand: string option) =
    PageHeader.top
        None
        "Übersicht"
        // „Comdirect · Import <Stand>“ am Handy und am PC (Prototyp `homeHead`, 12 px gedämpft); ohne bekannten Import
        // keine Zeile.
        (importLine importStand)
        [
            Html.div [
                prop.className $"flex items-center {Spacing.sm} md:hidden"
                prop.children [
                    PageHeader.iconButton "Abrufen" Icons.refresh actions.Fetch
                    PageHeader.iconButton "Einstellungen" Icons.gear actions.OpenSettings
                ]
            ]
            Html.div [
                prop.className $"hidden md:flex items-center {Spacing.sm}"
                prop.children [
                    Button.view {
                        Button.defaultProps with
                            Text = "Abrufen"
                            Icon = Some (Icons.refresh Icons.XS Icons.Default)
                            Variant = Button.Secondary
                            Size = Button.Small
                            OnClick = actions.Fetch
                    }
                    // Am PC „Buchung“ als kleiner Knopf neben „Abrufen“, am Handy schwebt „+ Buchung“ (ledger-s1weh).
                    Button.view {
                        Button.defaultProps with
                            Text = captureLabel
                            Icon = Some (Icons.plus Icons.XS Icons.Default)
                            Variant = Button.Secondary
                            Size = Button.Small
                            AriaLabel = Some captureAriaLabel
                            OnClick = actions.Capture
                    }
                ]
            ]
        ]

let private amountText (amount: decimal) = Money.formatAmountText false true amount "EUR"

/// Die Karte „Monat“ hat je Breite genau eine Höhe, geladen wie beim Laden und mit oder ohne „Im Minus“ (Styleguide
/// §7.14, feste Höhe vor variabler): Kopf 24 px, Ausgegeben samt Balken und Marke 46 px, untere Zeile am Handy 48 px
/// (Trefferfläche, §7.15), am PC 24 px, dazwischen 8 px, innen 12 px, Rand 1 px. Alle Höhen in rem.
/// In rem, damit die Karte mit einer größeren Grundschrift mitwächst (160 und 136 px bei 16 px).
let private monthCardHeight = "h-[10rem] md:h-[8.5rem]"

let private monthCardClass = $"{cardClass} {monthCardHeight} px-3.5 py-3 flex flex-col gap-2 overflow-hidden"

/// Die untere Zeile: am Handy 48 px hoch, am PC 24 px.
let private bottomRowHeight = "h-12 md:h-6"

let private monthCard (actions: Actions) (summary: MonthSummary) =
    let pct (share: float) = sprintf "%.2f%%" (share * 100.0)
    let spentText = $"{amountText summary.Spent} von {amountText summary.Assigned}"
    let toBeBudgeted =
        Money.formatAmountText false true summary.ToBeBudgeted.Amount summary.ToBeBudgeted.Currency
    Html.div [
        prop.custom ("data-testid", "overview-month")
        prop.className monthCardClass
        prop.children [
            Html.div [
                prop.className "h-6 shrink-0 flex items-baseline justify-between gap-3"
                prop.children [
                    // Schriftgrößen (Roman, 2026-11-11, §7.14): der Monatsname in der Größe der Zeilen, „noch N Tage“ auf 11 px
                    // wie die Abschnittsköpfe.
                    Html.span [ prop.className $"{ListRoles.primarySize} {FontWeights.semibold} {Colors.textPrimary}"; prop.text summary.Name ]
                    Html.span [ prop.className $"{FontSizes.microPlus} whitespace-nowrap {Colors.textMuted}"; prop.text (daysLeftText summary.DaysLeft) ]
                ]
            ]
            Html.div [
                prop.className "h-[2.875rem] shrink-0"
                prop.children [
                    Html.div [
                        prop.className $"h-5 flex items-baseline justify-between gap-3 {FontSizes.compact}"
                        prop.children [
                            Html.span [ prop.className $"shrink-0 {Colors.textSecondary}"; prop.text "Ausgegeben" ]
                            Html.span [
                                yield! Truncate.props $"{Fonts.mono} tabular-nums {Colors.textPrimary}" spentText
                                prop.children [
                                    Html.text (amountText summary.Spent)
                                    Html.span [ prop.className Colors.textMuted; prop.text $" von {amountText summary.Assigned}" ]
                                ]
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "relative h-2 mt-1.5 rounded overflow-hidden bg-border-default"
                        prop.children [
                            Html.div [
                                prop.className "absolute inset-y-0 left-0 rounded bg-text-secondary"
                                prop.style [ style.custom ("width", pct summary.SpentShare) ]
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "relative h-3"
                        prop.children [
                            // Die Marke steht am Ende des heutigen Tags. Die Verschiebung um denselben Anteil hält
                            // sie am Monatsersten und am Monatsletzten im Bild.
                            Html.span [
                                prop.className $"absolute top-0 whitespace-nowrap {FontSizes.micro} {Colors.textMuted}"
                                prop.style [
                                    style.custom ("left", pct summary.TodayShare)
                                    style.custom ("transform", sprintf "translateX(-%.2f%%)" (summary.TodayShare * 100.0))
                                ]
                                prop.children [
                                    Html.span [ prop.ariaHidden true; prop.text "▲" ]
                                    Html.text " heute"
                                ]
                            ]
                        ]
                    ]
                ]
            ]
            Html.div [
                prop.className $"{bottomRowHeight} shrink-0 flex items-stretch gap-4 min-w-0 {FontSizes.compact}"
                prop.children [
                    Html.button [
                        prop.type' "button"
                        prop.className $"inline-flex min-w-0 items-center gap-1.5 -mx-1.5 px-1.5 {Radius.sm} {rowHover}"
                        prop.onClick (fun _ -> actions.OpenBudget ())
                        prop.children [
                            Html.span [ prop.className $"shrink-0 {Colors.textMuted}"; prop.text "Zu verteilen" ]
                            Html.span [
                                yield!
                                    Truncate.textProps
                                        ($"{Fonts.mono} tabular-nums "
                                         + if summary.ToBeBudgeted.Amount < 0m then Colors.error else Colors.textPrimary)
                                        toBeBudgeted
                            ]
                        ]
                    ]
                    if summary.OverdrawnCount > 0 then
                        Html.button [
                            prop.type' "button"
                            prop.className $"inline-flex shrink-0 items-center gap-1.5 px-1.5 {Radius.sm} {rowHover}"
                            prop.onClick (fun _ -> actions.OpenOverdrawn ())
                            prop.children [
                                Html.span [ prop.className Colors.textMuted; prop.text "Im Minus" ]
                                Html.span [ prop.className $"{Fonts.mono} {Colors.error}"; prop.text (string summary.OverdrawnCount) ]
                            ]
                        ]
                ]
            ]
        ]
    ]

/// Platzhalter in derselben Höhe wie die geladene Karte, damit beim Laden nichts springt.
let private monthSkeleton =
    let bar (className: string) =
        Loading.skeleton { Loading.defaultSkeleton with Shape = Loading.Custom; ClassName = Some className }
    Html.div [
        prop.custom ("data-testid", "overview-month-loading")
        prop.role "status"
        prop.ariaLabel "Monat wird geladen"
        prop.className monthCardClass
        prop.children [
            Html.div [ prop.className "h-6 shrink-0 flex items-center"; prop.children [ bar "w-24 h-4" ] ]
            Html.div [
                prop.className "h-[2.875rem] shrink-0 flex flex-col justify-center gap-2"
                prop.children [ bar "w-full h-4"; bar "w-full h-2" ]
            ]
            Html.div [ prop.className $"{bottomRowHeight} shrink-0 flex items-center"; prop.children [ bar "w-40 h-4" ] ]
        ]
    ]

/// Ein Ladefehler des Monats in derselben festen Höhe wie die Karte: darunter springt nichts.
let private monthError (actions: Actions) (message: string) =
    Html.div [
        prop.custom ("data-testid", "overview-month-error")
        prop.className $"{monthCardHeight} overflow-hidden"
        prop.children [
            ErrorDisplay.cardCompactWith "Erneut laden" $"Der Monat konnte nicht geladen werden: {message}" (Some actions.Retry)
        ]
    ]

let private accountsBlock (actions: Actions) (model: Model) =
    match accountsListState model.Accounts with
    | AccountsListFailed message -> ErrorDisplay.cardCompact $"Konten konnten nicht geladen werden: {message}" (Some actions.Retry)
    | AccountsListLoading -> Loading.txListSkeleton 3
    | AccountsListEmpty ->
        Html.section [
            prop.className "flex flex-col gap-2.5"
            prop.children [
                sectionHead "Konten" None
                Card.emptyState (Icons.book Icons.LG Icons.Muted) noAccountsTitle noAccountsText None
            ]
        ]
    | AccountsListShown accounts ->
        let cards = Components.Ledger.View.buildAccountCards accounts model.Balances
        let valueOf (balance: Components.Ledger.View.CardBalance) (bold: bool) =
            match balance with
            | Components.Ledger.View.BalanceShown money ->
                Html.span [
                    prop.className (
                        $"{Fonts.mono} tabular-nums whitespace-nowrap "
                        + (if money.Amount < 0m then Colors.error elif bold then "" else Colors.textSecondary)
                    )
                    prop.text (Money.formatAmountText false true money.Amount money.Currency)
                ]
            | Components.Ledger.View.BalancePending -> Loading.amountSkeleton ()
            | Components.Ledger.View.BalanceUnavailable -> Money.balanceUnavailable ()
        match cards with
        | total :: rows ->
            Html.section [
                prop.className "flex flex-col gap-2.5"
                prop.children [
                    sectionHead "Konten" (Some (valueOf total.Balance true))
                    Html.div [
                        prop.className cardClass
                        prop.children [
                            for card in rows ->
                                Html.button [
                                    prop.key (Components.Ledger.State.encodeAccountFilter card.Filter)
                                    prop.type' "button"
                                    prop.custom ("data-testid", "overview-account")
                                    prop.className $"w-full flex items-center justify-between gap-2.5 px-3 min-h-[48px] text-left border-b {Borders.subtle} last:border-b-0 {rowHover}"
                                    prop.onClick (fun _ -> actions.OpenAccount card.Filter)
                                    prop.children [
                                        Truncate.text $"min-w-0 {Colors.textPrimary}" card.Label
                                        valueOf card.Balance false
                                    ]
                                ]
                        ]
                    ]
                ]
            ]
        // `buildAccountCards` liefert immer „Alle Konten“; ohne Konto greift oben der leere Zustand.
        | [] -> Html.none

let private recentBlock (actions: Actions) (today: DateTime) (recent: RemoteData<LedgerEntryView list>) =
    Html.section [
        prop.className "flex flex-col gap-2.5"
        prop.children [
            sectionHead "Neueste Buchungen" None
            match recent with
            | Failure message -> ErrorDisplay.cardCompact $"Buchungen konnten nicht geladen werden: {message}" (Some actions.Retry)
            | NotAsked
            | Loading -> Loading.txListSkeleton recentCount
            | Success [] ->
                Card.emptyState (Icons.book Icons.LG Icons.Muted) "Keine Buchungen" "Buchungen erscheinen hier nach dem nächsten Import." None
            | Success entries ->
                Html.div [
                    prop.className cardClass
                    prop.children [
                        for entry in entries ->
                            Html.button [
                                prop.key (string entry.Id)
                                prop.type' "button"
                                prop.custom ("data-testid", "overview-recent")
                                prop.className $"w-full flex items-center gap-2.5 px-3 py-2 min-h-11 text-left border-b {Borders.subtle} last:border-b-0 {rowHover}"
                                prop.onClick (fun _ -> actions.OpenEntry entry.Id)
                                prop.children [
                                    Html.div [
                                        prop.className "flex-1 min-w-0"
                                        prop.children [
                                            Truncate.text $"block {ListRoles.primary}" (Components.Ledger.View.payeeLabel entry)
                                            Html.div [
                                                prop.className $"{FontSizes.compact} {Colors.textSecondary} truncate"
                                                prop.children [
                                                    Html.text $"{Components.Ledger.View.formatEntryDate today entry.Date} · "
                                                    Html.span [
                                                        prop.className (if Components.Ledger.View.isWithoutCategory entry then Colors.attention else "")
                                                        prop.text (Components.Ledger.View.categoryLabel entry)
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Money.listAmount entry.Amount.Amount entry.Amount.Currency
                                ]
                            ]
                    ]
                ]
        ]
    ]

// ============================================
// Zu tun (ledger-s1weh, Prototyp Version 17 `todoList`)
// ============================================

/// Die Marke vor einem Eintrag, 9 px: „braucht dich“ Gelb, Fehler Rot. Rückfragen an Lena tragen das gefüllte
/// Flag-Symbol (15 px, Prototyp `I.flag`) in der Flag-Farbe (Styleguide §2: die Flag-Farbe nur im Symbol); die Farbe
/// setzt `lenaFlagScope` am Block. Jede Marke ist `aria-hidden`: der Text des Eintrags trägt die Bedeutung („Rückfrage
/// an Lena“, „zu prüfen“, „im Minus“), ein Name an der Marke ginge sonst doppelt in den Namen des Knopfs ein.
let private todoMarkView (mark: TodoMark) =
    Html.span [
        prop.ariaHidden true
        prop.className "w-4 shrink-0 grid place-items-center"
        prop.children [
            match mark with
            | MarkLenaFlag -> Html.span [ prop.className "inline-flex"; prop.children [ Icons.flagSolid Icons.Mark Icons.LenaFlag ] ]
            | MarkAttention -> Html.span [ prop.className "w-[9px] h-[9px] rounded-full bg-attention" ]
            | MarkError -> Html.span [ prop.className "w-[9px] h-[9px] rounded-full bg-error" ]
        ]
    ]

let private todoDetailClass = $"{FontSizes.compact} {Colors.textSecondary}"

/// Der erste Eintrag steht groß: Text 16 px halbfett, Zeile 2, darunter die eine Hauptaktion der Ansicht (§7.14).
let private firstTodo (actions: Actions) (todo: Todo) (more: bool) =
    Html.div [
        prop.custom ("data-testid", "overview-todo-first")
        prop.className (
            "flex flex-col gap-2.5 px-3.5 pt-3.5 pb-3"
            + if more then $" border-b {Borders.subtle}" else ""
        )
        prop.children [
            Html.div [
                prop.className "flex items-center gap-2.5"
                prop.children [
                    todoMarkView (todoMark todo.Target)
                    Html.div [
                        prop.className "min-w-0"
                        prop.children [
                            Html.div [ prop.className $"{FontSizes.emphasis} {FontWeights.semibold} {Colors.textPrimary}"; prop.text todo.Title ]
                            Html.div [ prop.className todoDetailClass; prop.text todo.Detail ]
                        ]
                    ]
                ]
            ]
            Button.view {
                Button.defaultProps with
                    Text = todoActionLabel todo.Target
                    Variant = Button.Primary
                    FullWidth = true
                    // Prototyp `.btn.primary.block`: gefüllt im Akzent, ohne Glow.
                    Glow = false
                    OnClick = fun () -> actions.OpenTodo todo.Target
            }
        ]
    ]

/// Jeder weitere Eintrag: eine Zeile mit Pfeil rechts, ein Klick führt zum selben Ziel.
let private todoRow (actions: Actions) (todo: Todo) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", "overview-todo")
        prop.className $"w-full flex items-center gap-2.5 px-3 py-2.5 min-h-12 text-left border-b {Borders.subtle} last:border-b-0 {rowHover}"
        prop.onClick (fun _ -> actions.OpenTodo todo.Target)
        prop.children [
            todoMarkView (todoMark todo.Target)
            Html.div [
                prop.className "flex-1 min-w-0"
                prop.children [
                    Truncate.text $"block {ListRoles.primary}" todo.Title
                    Truncate.text $"block {todoDetailClass}" todo.Detail
                ]
            ]
            Icons.chevronRight Icons.XS Icons.Muted
        ]
    ]

let private todoBlock (actions: Actions) (model: Model) =
    match todoState model with
    // In der Form des Ziels: großer erster Eintrag mit Knopf und eine Zeile, damit Monat und Konten darunter beim
    // Laden nicht springen (Layout-Invariante 9).
    | TodosLoading -> Loading.firstItemListSkeleton 1
    | TodosFailed message ->
        ErrorDisplay.cardCompact $"Was zu tun ist, konnte nicht geladen werden: {message}" (Some actions.Retry)
    | TodosShown [] -> Card.allDone "overview-all-done" "Alles erledigt" "Neue Aufgaben erscheinen nach dem nächsten Import."
    | TodosShown (first :: rest) ->
        Html.div [
            prop.custom ("data-testid", "overview-todos")
            yield! Badge.lenaFlagScope model.LenaFlagColor
            prop.className cardClass
            prop.children [
                firstTodo actions first (not rest.IsEmpty)
                for todo in rest do
                    React.keyedFragment (todoKey todo.Target, [ todoRow actions todo ])
            ]
        ]

/// Steht in „Zu tun“ etwas? Dann ist „+ Buchung“ ruhig, damit es bei einer Hauptaktion je Ansicht bleibt (§7.14).
/// Beim Laden auch ruhig: sonst wechselte der Knopf, sobald die Liste kommt.
let captureQuiet (model: Model) : bool =
    match todoState model with
    | TodosShown [] -> false
    | _ -> true

/// „+ Buchung“ am Handy unten rechts über der Leiste. `fixed`, deshalb rendert ihn der Haupt-`View.fs` außerhalb der
/// Seitenhülle.
let captureFab (model: Model) (onCapture: unit -> unit) =
    Html.div [
        prop.className "md:hidden overview-capture-fab"
        prop.children [ Button.fab "overview-capture-fab" captureLabel captureAriaLabel (captureQuiet model) onCapture ]
    ]

let view (model: Model) (actions: Actions) =
    Primitives.Container.full [
        Html.div [
            prop.custom ("data-testid", "overview-page")
            prop.className "flex flex-col gap-2.5 max-w-[640px] lg:max-w-[1240px]"
            prop.children [
                head actions model.ImportStand
                // Unter 1024 px eine Spalte: Zu tun, Monat, Konten. Ab 1024 px zwei (Prototyp `.dgrid`): links Zu tun,
                // rechts Monat und Neueste Buchungen.
                Html.div [
                    prop.className "flex flex-col gap-2.5 lg:grid lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] lg:gap-6 lg:items-start"
                    prop.children [
                        Html.section [
                            prop.custom ("data-testid", "overview-todo-column")
                            prop.className "flex flex-col gap-2.5 min-w-0"
                            prop.children [ sectionHead "Zu tun" None; todoBlock actions model ]
                        ]
                        Html.div [
                            prop.className "flex flex-col gap-2.5 min-w-0"
                            prop.children [
                                sectionHead "Monat" None
                                match model.Month with
                                | NotAsked
                                | Loading -> monthSkeleton
                                | Failure message -> monthError actions message
                                | Success month -> monthCard actions (monthSummary month model.Today)
                                // Die Konten stehen unter 1024 px hier, darüber in der Seitenleiste (Styleguide §7.15).
                                Html.div [ prop.className "lg:hidden"; prop.children [ accountsBlock actions model ] ]
                                // Ab 768 px die neuesten Buchungen, ab 1024 px rechts unter dem Monat.
                                Html.div [ prop.className "hidden md:block"; prop.children [ recentBlock actions model.Today model.Recent ] ]
                            ]
                        ]
                    ]
                ]
                // Am Handy Platz unter der letzten Zeile für „+ Buchung“ (52 px) und seinen Abstand.
                Html.div [ prop.className "md:hidden h-[4.5rem]"; prop.ariaHidden true ]
            ]
        ]
    ]
