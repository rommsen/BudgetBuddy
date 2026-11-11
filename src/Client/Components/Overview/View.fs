/// Die schlichte Übersicht (design-system-k6kfw, Prototyp Version 17 `homeHead`, `monthBlock`, `home`, `homeDesk`):
/// Kopf mit „Abrufen“, der Monat als Karte fester Höhe, am Handy die Konten, am PC die neuesten Buchungen.
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
}

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

let private head (actions: Actions) =
    PageHeader.top
        None
        "Übersicht"
        // Den Stand des Imports kennt BB heute nicht ohne neue Abfrage: die Zeile entfällt (Task-Kriterium).
        None
        [
            Html.div [
                prop.className "flex items-center gap-1 md:hidden"
                prop.children [
                    PageHeader.iconButton "Abrufen" Icons.refresh actions.Fetch
                    PageHeader.iconButton "Einstellungen" Icons.gear actions.OpenSettings
                ]
            ]
            Button.view {
                Button.defaultProps with
                    Text = "Abrufen"
                    Icon = Some (Icons.refresh Icons.XS Icons.Default)
                    Variant = Button.Secondary
                    Size = Button.Small
                    ClassName = Some "hidden md:inline-flex"
                    OnClick = actions.Fetch
            }
        ]

let private amountText (amount: decimal) = Money.formatAmountText false true amount "EUR"

/// Die Karte „Monat“ hat je Breite genau eine Höhe, geladen wie beim Laden und mit oder ohne „Im Minus“ (Styleguide
/// §7.14, feste Höhe vor variabler): Kopf 24 px, Ausgegeben samt Balken und Marke 46 px, untere Zeile am Handy 48 px
/// (Trefferfläche, §7.15), am PC 24 px, dazwischen 8 px, innen 12 px, Rand 1 px.
let private monthCardHeight = "h-[160px] md:h-[136px]"

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
                    Html.span [ prop.className $"{FontWeights.semibold} {Colors.textPrimary}"; prop.text summary.Name ]
                    Html.span [ prop.className $"text-xs whitespace-nowrap {Colors.textMuted}"; prop.text (daysLeftText summary.DaysLeft) ]
                ]
            ]
            Html.div [
                prop.className "h-[46px] shrink-0"
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
                                prop.text "▲ heute"
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
                        prop.className "inline-flex min-w-0 items-center gap-1.5"
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
                            prop.className "inline-flex shrink-0 items-center gap-1.5"
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
        prop.ariaLabel "Monat wird geladen"
        prop.className monthCardClass
        prop.children [
            Html.div [ prop.className "h-6 shrink-0 flex items-center"; prop.children [ bar "w-24 h-4" ] ]
            Html.div [
                prop.className "h-[46px] shrink-0 flex flex-col justify-center gap-2"
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
                                    prop.className $"w-full flex items-center justify-between gap-2.5 px-3 min-h-[48px] text-left border-b {Borders.subtle} last:border-b-0"
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
                                prop.className $"w-full flex items-center gap-2.5 px-3 py-2 min-h-11 text-left border-b {Borders.subtle} last:border-b-0 hover:bg-surface-hover"
                                prop.onClick (fun _ -> actions.OpenEntry entry.Id)
                                prop.children [
                                    Html.div [
                                        prop.className "flex-1 min-w-0"
                                        prop.children [
                                            Truncate.text $"block {ListRoles.primary}" (entry.Payee |> Option.defaultValue "Ohne Payee")
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

let view (model: Model) (actions: Actions) =
    Primitives.Container.full [
        Html.div [
            prop.custom ("data-testid", "overview-page")
            prop.className "flex flex-col gap-2.5 max-w-[640px]"
            prop.children [
                head actions
                sectionHead "Monat" None
                match model.Month with
                | NotAsked
                | Loading -> monthSkeleton
                | Failure message -> monthError actions message
                | Success month -> monthCard actions (monthSummary month model.Today)
                // Die Konten stehen unter 1024 px hier, darüber in der Seitenleiste (Styleguide §7.15).
                Html.div [ prop.className "lg:hidden"; prop.children [ accountsBlock actions model ] ]
                // Am PC (ab 768 px) die neuesten Buchungen, einspaltig, bis `ledger-s1weh` „Zu tun“ links bringt.
                Html.div [ prop.className "hidden md:block"; prop.children [ recentBlock actions model.Today model.Recent ] ]
            ]
        ]
    ]
