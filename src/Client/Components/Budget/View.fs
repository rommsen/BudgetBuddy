/// Die Budget-Seite (budgeting-k7wmd): Monat, Umschalter dieser/nächster Monat, Zu verteilen
/// als große Zahl und die Kategorien nach Gruppe mit Zugewiesen, Aktivität und Verfügbar.
/// Am Handy zweizeilige Zeilen ohne horizontales Scrollen, ab md eine Tabelle.
///
/// Ausschließlich Design-System-Bausteine und Tokens (Styleguide-Gate `design-system-001`).
/// Ein negatives Zu verteilen erscheint einfach negativ, ohne Hinweis und ohne Sperre.
module Components.Budget.View

open Feliz
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Client.DesignSystem
open Client.DesignSystem.Tokens

// ============================================
// Beträge
// ============================================

/// Ein Betrag in Listenschrift, neutral eingefärbt: Zugewiesen und Aktivität sind keine
/// Warnsignale, nur Verfügbar färbt ein Minus (`Money.available`).
let private plainAmount (m: Money) =
    Html.span [
        prop.className $"font-mono font-medium tabular-nums whitespace-nowrap {ListRoles.primarySize} {Colors.textSecondary}"
        prop.text (Money.formatAmountText false true m.Amount m.Currency)
    ]

let private availableAmount (m: Money) = Money.available m.Amount m.Currency

// ============================================
// Zu verteilen
// ============================================

let private toBeBudgetedCard (m: Money) =
    Card.standard [
        Card.body [
            Html.div [
                prop.custom ("data-testid", "budget-to-be-budgeted")
                prop.className "flex flex-col gap-1"
                prop.children [
                    Html.span [
                        prop.className ListRoles.groupHeader
                        prop.text "Zu verteilen"
                    ]
                    Money.view
                        { Money.defaultProps with
                            Amount = m.Amount
                            Currency = m.Currency
                            Size = Money.Hero
                            Glow = Money.NoGlow
                            ShowSign = false }
                ]
            ]
        ]
    ]

// ============================================
// Umschalter
// ============================================

let private monthSwitcher (model: Model) (dispatch: Msg -> unit) =
    let chip label choice =
        ChipBar.chip
            { Label = label
              IsActive = (model.Choice = choice)
              OnClick = fun () -> dispatch (SelectMonth choice)
              OnRemove = None }
    Html.div [
        prop.custom ("data-testid", "budget-month-switcher")
        prop.children [
            ChipBar.bar None [
                chip "Dieser Monat" CurrentMonth
                chip "Nächster Monat" NextMonth
            ]
        ]
    ]

// ============================================
// Kategorien: Handy (Zeilen) und PC (Tabelle)
// ============================================

let private mobileGroup (group: BudgetGroup) =
    Html.section [
        prop.key group.Name
        prop.custom ("data-testid", "budget-group")
        prop.className "mb-4"
        prop.children [
            Html.h2 [
                prop.className $"{ListRoles.groupHeader} px-4 pt-4 pb-1"
                prop.text group.Name
            ]
            for c in group.Categories do
                Html.div [
                    prop.key (string c.Id)
                    prop.custom ("data-testid", "budget-category-row")
                    prop.className $"{ListRoles.mobileRowPadding} border-b border-border-subtle"
                    prop.children [
                        Html.div [
                            prop.className ListRoles.mobileLine1
                            prop.children [
                                Html.span [
                                    prop.className $"{ListRoles.primary} truncate min-w-0"
                                    prop.text c.Name
                                ]
                                availableAmount c.Available
                            ]
                        ]
                        Html.div [
                            prop.className ListRoles.mobileLine2
                            prop.children [
                                Html.span [
                                    prop.className $"{ListRoles.muted} truncate"
                                    prop.text
                                        $"Zugewiesen {Money.formatAmountText false true c.Assigned.Amount c.Assigned.Currency} · Aktivität {Money.formatAmountText false true c.Activity.Amount c.Activity.Currency}"
                                ]
                            ]
                        ]
                    ]
                ]
        ]
    ]

let private desktopTable (groups: BudgetGroup list) =
    Table.view { Table.defaultTableProps with Variant = Table.Default; Responsive = false } [
        Table.thead [
            Table.calmHeaderCell "Kategorie" None
            Table.calmHeaderCell "Zugewiesen" (Some "right")
            Table.calmHeaderCell "Aktivität" (Some "right")
            Table.calmHeaderCell "Verfügbar" (Some "right")
        ]
        Table.tbody [
            for group in groups do
                Html.tr [
                    prop.key $"group-{group.Name}"
                    prop.children [
                        Html.td [
                            prop.colSpan 4
                            prop.className $"{ListRoles.cellPaddingX} pt-4 pb-1 {ListRoles.groupHeader}"
                            prop.text group.Name
                        ]
                    ]
                ]
                for c in group.Categories do
                    Html.tr [
                        prop.key (string c.Id)
                        prop.custom ("data-testid", "budget-category-row")
                        prop.className $"{ListRoles.rowHeight} border-b border-border-subtle"
                        prop.children [
                            Table.cell Table.defaultCellProps [
                                Html.span [ prop.className ListRoles.primary; prop.text c.Name ]
                            ]
                            Table.cell { Table.defaultCellProps with Align = Some "right" } [ plainAmount c.Assigned ]
                            Table.cell { Table.defaultCellProps with Align = Some "right" } [ plainAmount c.Activity ]
                            Table.cell { Table.defaultCellProps with Align = Some "right" } [ availableAmount c.Available ]
                        ]
                    ]
        ]
    ]

let private categories (month: BudgetMonth) =
    if categoryCount month = 0 then
        Card.emptyState (Icons.banknotes Icons.XL Icons.Default) "Keine Kategorien" "YNAB liefert für diesen Monat keine sichtbaren Kategorien." None
    else
        React.fragment [
            Html.div [
                prop.className "md:hidden"
                prop.children (month.Groups |> List.map mobileGroup)
            ]
            Html.div [
                prop.className "hidden md:block rounded-xl border border-border-subtle bg-surface-card overflow-hidden"
                prop.children [ desktopTable month.Groups ]
            ]
        ]

// ============================================
// Seite
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    let subtitle =
        match model.Month with
        | Success month -> Some (monthLabel month.Month)
        | _ -> None

    Html.div [
        prop.custom ("data-testid", "budget-page")
        prop.className "flex flex-col gap-4"
        prop.children [
            PageHeader.calm "Budget" subtitle []
            monthSwitcher model dispatch
            match model.Month with
            | NotAsked
            | Loading -> Loading.centered "Lade Budget ..."
            | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch Load))
            | Success month ->
                React.fragment [
                    toBeBudgetedCard month.ToBeBudgeted
                    categories month
                ]
        ]
    ]
