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
// Targets anwenden (budgeting-h2zfn)
// ============================================

let private amountText (m: Money) = Money.formatAmountText false true m.Amount m.Currency

/// Summe und Zu verteilen danach stehen oben im Dialog, außerhalb des scrollenden Teils.
let private summary (preview: TargetPreview) =
    Html.div [
        prop.custom ("data-testid", "targets-summary")
        prop.className "flex items-end justify-between gap-4 px-4 md:px-6 py-3 border-b border-border-default"
        prop.children [
            Html.div [
                prop.className "flex flex-col gap-1 min-w-0"
                prop.children [
                    Html.span [ prop.className ListRoles.groupHeader; prop.text "Summe" ]
                    Html.span [
                        prop.custom ("data-testid", "targets-total")
                        prop.className "font-mono font-medium tabular-nums text-text-primary"
                        prop.text (amountText preview.Total)
                    ]
                ]
            ]
            Html.div [
                prop.className "flex flex-col gap-1 items-end"
                prop.children [
                    Html.span [ prop.className ListRoles.groupHeader; prop.text "Zu verteilen danach" ]
                    Html.span [
                        prop.custom ("data-testid", "targets-after")
                        prop.children [
                            Money.view
                                { Money.defaultProps with
                                    Amount = preview.ToBeBudgetedAfter.Amount
                                    Currency = preview.ToBeBudgetedAfter.Currency
                                    Size = Money.Large
                                    Glow = Money.NoGlow
                                    ShowSign = false }
                        ]
                    ]
                ]
            ]
        ]
    ]

let private previewLine (l: TargetLine) =
    Html.div [
        prop.key (string l.CategoryId)
        prop.custom ("data-testid", "targets-line")
        prop.className $"{ListRoles.mobileRowPadding} border-b border-border-subtle"
        prop.children [
            Html.div [
                prop.className ListRoles.mobileLine1
                prop.children [
                    Html.span [ prop.className $"{ListRoles.primary} truncate min-w-0"; prop.text l.CategoryName ]
                    Html.span [
                        prop.className $"font-mono font-medium tabular-nums whitespace-nowrap {ListRoles.primarySize} {Colors.textSecondary}"
                        prop.text (amountText l.NewAssigned)
                    ]
                ]
            ]
            Html.div [
                prop.className ListRoles.mobileLine2
                prop.children [
                    Html.span [
                        prop.className $"{ListRoles.muted} truncate"
                        prop.text $"Zugewiesen {amountText l.OldAssigned}, Fehlbetrag {amountText l.Underfunded}"
                    ]
                ]
            ]
        ]
    ]

let private skippedLines (skipped: SkippedLine list) =
    if List.isEmpty skipped then
        Html.none
    else
        Html.div [
            prop.custom ("data-testid", "targets-skipped")
            prop.children [
                Html.h3 [
                    prop.className $"{ListRoles.groupHeader} px-4 pt-4 pb-1"
                    prop.text "Übersprungen (pausiert)"
                ]
                for s in skipped do
                    Html.div [
                        prop.key (string s.CategoryId)
                        prop.className $"{ListRoles.mobileRowPadding} border-b border-border-subtle"
                        prop.children [
                            Html.div [
                                prop.className ListRoles.mobileLine1
                                prop.children [
                                    Html.span [ prop.className $"{ListRoles.secondary} truncate min-w-0"; prop.text s.CategoryName ]
                                    Badge.neutral "pausiert"
                                ]
                            ]
                        ]
                    ]
            ]
        ]

let private previewBody (preview: TargetPreview) (changed: bool) =
    Modal.bodyNoPadding [
        if changed then
            Html.div [
                prop.custom ("data-testid", "targets-changed")
                prop.className "p-4"
                prop.children [
                    ErrorDisplay.warning "Stand hat sich geändert. Die Vorschau ist neu geladen, es wurde nichts geschrieben." None
                ]
            ]
        if List.isEmpty preview.Lines then
            Html.p [
                prop.custom ("data-testid", "targets-empty")
                prop.className "p-4 text-sm text-text-secondary"
                prop.text "Keine Kategorie hat einen Fehlbetrag. Es gibt nichts anzuwenden."
            ]
        else
            Html.div [ prop.children (preview.Lines |> List.map previewLine) ]
        skippedLines preview.Skipped
    ]

let private outcomeLine (l: ApplyLineResult) =
    let detail, badge =
        match l.Outcome with
        | Written -> $"{amountText l.OldAssigned} auf {amountText l.NewAssigned}", Badge.success "geschrieben"
        | Failed reason -> reason, Badge.error "fehlgeschlagen"
        | ChangedInYnab -> "YNAB steht anders als erwartet, nichts überschrieben", Badge.warning "in YNAB geändert"
    Html.div [
        prop.key (string l.CategoryId)
        prop.custom ("data-testid", "targets-result-line")
        prop.className $"{ListRoles.mobileRowPadding} border-b border-border-subtle"
        prop.children [
            Html.div [
                prop.className ListRoles.mobileLine1
                prop.children [
                    Html.span [ prop.className $"{ListRoles.primary} truncate min-w-0"; prop.text l.CategoryName ]
                    badge
                ]
            ]
            Html.div [
                prop.className ListRoles.mobileLine2
                prop.children [ Html.span [ prop.className $"{ListRoles.muted} truncate"; prop.text detail ] ]
            ]
        ]
    ]

let private doneBody (preview: TargetPreview) (report: ApplyReport) (retryError: string option) =
    let total = report.Lines.Length
    // Geschriebene stehen nach den Problemfällen, damit Fehlgeschlagene oben sichtbar sind.
    let ordered =
        report.Lines |> List.sortBy (fun l -> match l.Outcome with Written -> 1 | _ -> 0)
    Modal.bodyNoPadding [
        Html.div [
            prop.className "p-4 flex flex-col gap-2"
            prop.children [
                Html.p [
                    prop.custom ("data-testid", "targets-count")
                    prop.className "text-sm text-text-primary"
                    prop.text $"{writtenCount report} von {total} übernommen"
                ]
                match report.ActualToBeBudgeted with
                | Some actual ->
                    Html.div [
                        prop.custom ("data-testid", "targets-actual")
                        prop.className "flex items-center gap-2 text-sm text-text-secondary"
                        prop.children [
                            Html.span [ prop.text "Zu verteilen jetzt" ]
                            Money.view
                                { Money.defaultProps with
                                    Amount = actual.Amount
                                    Currency = actual.Currency
                                    Size = Money.Medium
                                    Glow = Money.NoGlow
                                    ShowSign = false }
                        ]
                    ]
                | None ->
                    Html.p [
                        prop.custom ("data-testid", "targets-actual")
                        prop.className "text-sm text-text-secondary"
                        prop.text "Zu verteilen konnte nach dem Lauf nicht gelesen werden."
                    ]
                match actualCheck preview report with
                | DiffersFromPreview _ ->
                    Html.div [
                        prop.custom ("data-testid", "targets-differs")
                        prop.children [
                            ErrorDisplay.warning $"Zu verteilen weicht von der Vorschau ab (Vorschau: {amountText preview.ToBeBudgetedAfter})." None
                        ]
                    ]
                | _ -> ()
                match retryError with
                | Some message -> ErrorDisplay.cardCompact message None
                | None -> ()
            ]
        ]
        Html.div [ prop.children (ordered |> List.map outcomeLine) ]
    ]

let private targetsDialog (model: Model) (dispatch: Msg -> unit) =
    let close = fun () -> dispatch CloseTargets
    let modal title children =
        Modal.view
            { Modal.defaultProps with
                IsOpen = true
                Title = Some title
                Size = Modal.Large
                OnClose = close }
            children
    let monthTitle =
        match model.Month with
        | Success month -> $"Targets anwenden, {monthLabel month.Month}"
        | _ -> "Targets anwenden"
    match model.Targets with
    | TargetsClosed -> Html.none
    | PreviewLoading ->
        modal monthTitle [ Modal.body [ Loading.centered "Lade Vorschau ..." ] ]
    | PreviewFailed message ->
        modal monthTitle [
            Modal.body [ ErrorDisplay.card message (Some (fun () -> dispatch OpenTargets)) ]
            Modal.footer [ Button.ghost "Schließen" close ]
        ]
    | PreviewReady (preview, changed) ->
        modal monthTitle [
            summary preview
            previewBody preview changed
            Modal.footer [
                Button.ghost "Abbrechen" close
                if not (List.isEmpty preview.Lines) then
                    Button.primary "Übernehmen" (fun () -> dispatch ConfirmTargets)
            ]
        ]
    | Applying preview ->
        modal monthTitle [
            summary preview
            Modal.body [ Loading.centered "Schreibe nach YNAB ..." ]
        ]
    | ApplyFailed (preview, message) ->
        modal monthTitle [
            summary preview
            Modal.body [
                ErrorDisplay.cardCompact $"{message} Ob etwas geschrieben wurde, ist unklar. Beim Schließen wird der Monat neu gelesen." None
            ]
            Modal.footer [ Button.primary "Schließen" close ]
        ]
    | ApplyDone (preview, report, retrying, retryError) ->
        modal monthTitle [
            doneBody preview report retryError
            Modal.footer [
                Button.ghost "Schließen" close
                if failedCount report > 0 then
                    Button.primaryLoading "Fehlgeschlagene erneut senden" retrying (fun () -> dispatch RetryFailed)
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
            PageHeader.calm "Budget" subtitle (
                match model.Month with
                | Success _ -> [ Button.secondary "Targets anwenden" (fun () -> dispatch OpenTargets) ]
                | _ -> []
            )
            targetsDialog model dispatch
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
