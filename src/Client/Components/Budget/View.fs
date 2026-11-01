/// Die Budget-Seite (budgeting-k7wmd): Monat, Umschalter letzter/dieser/nächster Monat, Zu verteilen
/// als große Zahl und die Kategorien nach Gruppe mit Zugewiesen, Aktivität und Verfügbar.
/// Am Handy zwei Zeilen ohne horizontales Scrollen, mit Target eine dritte; ab md eine Tabelle
/// mit der Spalte Target (budgeting-kjsht).
///
/// Das Suchfeld über der Liste filtert die Kategorien (budgeting-rxxb4). Ein Tipp auf Verfügbar
/// öffnet "Verschieben" (budgeting-afd8h), ein Tipp auf den Kategorie-Namen das Kategorie-Detail
/// (budgeting-m1bce).
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

/// Ein Betrag in Listenschrift, neutral eingefärbt: Zugewiesen, Aktivität, Target und der neue
/// Betrag der Vorschau sind keine Warnsignale, nur Verfügbar färbt ein Minus (`Money.available`).
let private plainAmount (m: Money) =
    Html.span [
        prop.className $"font-mono font-medium tabular-nums whitespace-nowrap {ListRoles.primarySize} {Colors.textSecondary}"
        prop.text (moneyText m)
    ]

let private availableAmount (m: Money) = Money.available m.Amount m.Currency

/// Kopfzahl im Kategorie-Detail: ruhig, ohne Warnfarbe und Glow.
let private detailAmount (m: Money) =
    Html.span [
        prop.className $"font-mono font-medium tabular-nums whitespace-nowrap {ListRoles.primarySize} {Colors.textPrimary}"
        prop.text (moneyText m)
    ]

/// Der Verfügbar-Betrag öffnet "Verschieben". `sizeClass` trägt die Trefferfläche der Liste.
let private availableButton (sizeClass: string) (c: BudgetCategory) (dispatch: Msg -> unit) =
    Money.availableButton $"{c.Name} verschieben" sizeClass c.Available.Amount c.Available.Currency (fun () ->
        dispatch (OpenMove c.Id))

/// Der Kategorie-Name öffnet das Detail.
let private categoryNameButton (touchTarget: bool) (c: BudgetCategory) (dispatch: Msg -> unit) =
    Button.listName "budget-category-name" $"{c.Name}, Details" touchTarget c.Name (fun () ->
        dispatch (OpenCategoryDetail c.Id))

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
    ChipBar.barWithTestId "budget-month-switcher" [
        chip "Letzter Monat" LastMonth
        chip "Dieser Monat" CurrentMonth
        chip "Nächster Monat" NextMonth
    ]

// ============================================
// Kategorien: Handy (Zeilen) und PC (Tabelle)
// ============================================

let private mobileGroup (dispatch: Msg -> unit) (group: BudgetGroup) =
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
                                categoryNameButton true c dispatch
                                // -my-3: die 48 px Trefferfläche sollen die Zeile nicht höher machen.
                                availableButton $"{TouchTargets.minSize} -my-3" c dispatch
                            ]
                        ]
                        Html.div [
                            prop.className ListRoles.mobileLine2
                            prop.children [
                                Html.span [
                                    prop.className $"{ListRoles.muted} truncate"
                                    prop.text
                                        $"Zugewiesen {moneyText c.Assigned} · Aktivität {moneyText c.Activity}"
                                ]
                            ]
                        ]
                        match c.Target with
                        | Some target ->
                            // Wird es eng, kürzt das Target, nie der Status: er ist die Aussage der Zeile.
                            Html.div [
                                prop.custom ("data-testid", "budget-target")
                                prop.className ListRoles.mobileLine2
                                prop.children [
                                    Html.span [
                                        prop.className $"{ListRoles.secondary} truncate min-w-0"
                                        prop.text $"Target {targetText target}"
                                    ]
                                    match targetStatusText c with
                                    | Some status ->
                                        Html.span [
                                            prop.className $"{ListRoles.secondary} shrink-0 whitespace-nowrap"
                                            prop.text $"· {status}"
                                        ]
                                    | None -> ()
                                ]
                            ]
                        | None -> ()
                    ]
                ]
        ]
    ]

/// Target am PC: Betrag in Mono, Rhythmus in Inter, darunter pausiert, fehlt oder erfüllt.
let private targetCell (c: BudgetCategory) =
    match c.Target with
    | None -> Html.none
    | Some target ->
        Html.div [
            prop.custom ("data-testid", "budget-target")
            prop.className "flex flex-col items-end"
            prop.children [
                Html.span [
                    prop.className "whitespace-nowrap"
                    prop.children [
                        plainAmount target.Goal
                        Html.span [
                            prop.className $"font-sans {ListRoles.primarySize} {Colors.textSecondary}"
                            prop.text (rhythmText target.Rhythm)
                        ]
                    ]
                ]
                match targetStatusText c with
                | Some status -> Html.span [ prop.className $"{ListRoles.secondary} whitespace-nowrap"; prop.text status ]
                | None -> ()
            ]
        ]

let private desktopTable (dispatch: Msg -> unit) (groups: BudgetGroup list) =
    Table.view { Table.defaultTableProps with Variant = Table.Default; Responsive = false } [
        Table.thead [
            Table.calmHeaderCell "Kategorie" None
            Table.calmHeaderCell "Target" (Some "right")
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
                            prop.colSpan 5
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
                            // max-w-0 und w-full: der Name nimmt den Restplatz und kürzt, statt die
                            // Tabelle aus der Karte zu drücken (sonst fehlt Verfügbar bei 768 px).
                            Table.cell { Table.defaultCellProps with ClassName = Some "w-full max-w-0" } [
                                Html.div [
                                    prop.className "flex min-w-0"
                                    prop.children [ categoryNameButton false c dispatch ]
                                ]
                            ]
                            Table.cell { Table.defaultCellProps with Align = Some "right" } [ targetCell c ]
                            Table.cell { Table.defaultCellProps with Align = Some "right" } [ plainAmount c.Assigned ]
                            Table.cell { Table.defaultCellProps with Align = Some "right" } [ plainAmount c.Activity ]
                            Table.cell { Table.defaultCellProps with Align = Some "right" } [ availableButton "" c dispatch ]
                        ]
                    ]
        ]
    ]

let private categorySearchField (search: string) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "budget-category-search")
        prop.className "relative"
        prop.children [
            Input.text
                { Input.textInputDefaults with
                    Value = search
                    OnChange = CategorySearchChanged >> dispatch
                    Placeholder = "Kategorie suchen…"
                    Size = Input.Small
                    ClassName = Some "w-full pr-12" }
            if search <> "" then
                Button.view
                    { Button.defaultProps with
                        Variant = Button.Ghost
                        Size = Button.Small
                        Icon = Some (Icons.x Icons.SM Icons.Default)
                        OnClick = fun () -> dispatch ClearCategorySearch
                        Title = Some "Suche leeren"
                        AriaLabel = Some "Suche leeren"
                        ClassName = Some "absolute right-0 top-1/2 -translate-y-1/2" }
        ]
    ]

let private categories (search: string) (month: BudgetMonth) (dispatch: Msg -> unit) =
    let groups = filterGroups search month.Groups
    if categoryCount month = 0 then
        Card.emptyState (Icons.banknotes Icons.XL Icons.Default) "Keine Kategorien" "YNAB liefert für diesen Monat keine sichtbaren Kategorien." None
    elif List.isEmpty groups then
        Html.p [
            prop.custom ("data-testid", "budget-search-empty")
            prop.className $"{ListRoles.muted} px-4 py-8 text-center"
            prop.text $"Keine Kategorie passt zu „{search.Trim()}“"
        ]
    else
        React.fragment [
            Html.div [
                prop.className "md:hidden"
                prop.children (groups |> List.map (mobileGroup dispatch))
            ]
            Html.div [
                prop.className "hidden md:block rounded-xl border border-border-subtle bg-surface-card overflow-hidden"
                prop.children [ desktopTable dispatch groups ]
            ]
        ]

// ============================================
// Targets anwenden (budgeting-h2zfn)
// ============================================

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
                        prop.text (moneyText preview.Total)
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
                    plainAmount l.NewAssigned
                ]
            ]
            // Bricht um statt abzuschneiden: mit Target passt die Zeile am Handy nicht in eine Zeile.
            Html.p [
                prop.custom ("data-testid", "targets-line-detail")
                prop.className $"{ListRoles.secondary} mt-0.5"
                prop.text (previewDetailText l)
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
        | Written -> $"{moneyText l.OldAssigned} auf {moneyText l.NewAssigned}", Badge.success "geschrieben"
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
                            ErrorDisplay.warning $"Zu verteilen weicht von der Vorschau ab (Vorschau: {moneyText preview.ToBeBudgetedAfter})." None
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
// Verschieben (budgeting-afd8h)
// ============================================

let private moveAfterLine (line: MoveAfterLine) =
    Html.div [
        prop.key line.Label
        prop.custom ("data-testid", "move-after-line")
        prop.className ListRoles.mobileLine1
        prop.children [
            Html.span [ prop.className $"{ListRoles.secondary} truncate min-w-0"; prop.text line.Label ]
            availableAmount line.After
        ]
    ]

/// Der Inhalt, am Handy im Sheet und am PC im Dialog.
let private moveFields (month: BudgetMonth) (form: MoveForm) (dispatch: Msg -> unit) =
    let pickerProps : CategoryPicker.CategoryPickerProps =
        { Categories = pickerCategories month form.CategoryId
          Current = None
          Suggested = []
          Recent = []
          Accounts = []
          CurrentAccount = None
          OfferWithout = false
          ToBeBudgeted = Some month.ToBeBudgeted
          StandLabel = None
          LoadState = CategoryPicker.PickerReady
          OnRetry = ignore
          OnSelect = fun choice -> counterpartOf choice |> Option.iter (PickMoveCounterpart >> dispatch)
          OnClose = fun () -> dispatch CloseMovePicker }
    let counterpartLabel =
        form.Counterpart |> Option.map (counterpartName month) |> Option.defaultValue "Gegenseite wählen"
    React.fragment [
        Html.div [
            prop.className "flex items-center justify-between gap-3"
            prop.children [
                Html.p [
                    prop.custom ("data-testid", "move-headline")
                    prop.className $"{ListRoles.primary} min-w-0"
                    prop.text (moveHeadline month form)
                ]
                Html.span [
                    prop.custom ("data-testid", "move-toggle")
                    prop.className "shrink-0"
                    prop.children [ Button.ghost "Richtung tauschen" (fun () -> dispatch ToggleMoveDirection) ]
                ]
            ]
        ]
        Html.div [
            prop.className "relative"
            prop.children [
                Html.span [
                    prop.custom ("data-testid", "move-counterpart")
                    prop.className "block"
                    // Das Popover schließt beim Mausdruck außerhalb, der Klick öffnete es sonst
                    // gleich wieder. So bleibt der Auslöser ein Umschalter.
                    prop.onMouseDown (fun e -> e.stopPropagation ())
                    prop.children [
                        Button.secondaryFullWidth counterpartLabel (fun () ->
                            dispatch (if form.PickerOpen then CloseMovePicker else OpenMovePicker))
                    ]
                ]
                // ADR 0043: zur Renderzeit genau eine Hülle.
                if Viewport.isDesktopViewport () then
                    CategoryPicker.popoverInDialog form.PickerOpen pickerProps
                else
                    CategoryPicker.sheetLayered form.PickerOpen None pickerProps
            ]
        ]
        Input.groupSimple "Betrag" (
            Html.div [
                prop.custom ("data-testid", "move-amount")
                prop.children [
                    Input.text
                        { Input.textInputDefaults with
                            Value = moveAmountText month form
                            OnChange = MoveAmountChanged >> dispatch
                            Placeholder = "0,00"
                            InputMode = Some "decimal"
                            ClassName = Some "w-full font-mono tabular-nums" }
                ]
            ]
        )
        Html.div [
            prop.custom ("data-testid", "move-after")
            prop.className "flex flex-col gap-1"
            prop.children [
                Html.span [ prop.className ListRoles.groupHeader; prop.text "Verfügbar danach" ]
                yield! moveAfter month form |> List.map moveAfterLine
            ]
        ]
        match form.Submission with
        | Rejected message ->
            Html.div [
                prop.custom ("data-testid", "move-error")
                prop.children [ ErrorDisplay.cardCompact message None ]
            ]
        | Editing
        | Sending -> ()
    ]

let private moveButton (month: BudgetMonth) (form: MoveForm) (dispatch: Msg -> unit) =
    Html.span [
        prop.custom ("data-testid", "move-submit")
        prop.className "block w-full md:w-auto"
        prop.children [
            Button.view
                { Button.defaultProps with
                    Text = "Verschieben"
                    IsLoading = (form.Submission = Sending)
                    IsDisabled = not (canMove month form)
                    FullWidth = true
                    OnClick = fun () -> dispatch ConfirmMove }
        ]
    ]

/// Am Handy `BottomSheet`, am PC `Modal` (ADR 0043), nie beide. Am PC steht der Inhalt in
/// `Modal.body`, das Popover rückt beim Öffnen ins Bild (`popoverInDialog`) und passt in die Breite
/// von `Modal.Large`.
let private moveDialog (model: Model) (dispatch: Msg -> unit) =
    match model.Move, model.Month with
    | Some form, Success month ->
        let close = fun () -> dispatch CloseMove
        let fields =
            Html.div [
                prop.custom ("data-testid", "move-dialog")
                prop.className "flex flex-col gap-4"
                prop.children [ moveFields month form dispatch ]
            ]
        if Viewport.isDesktopViewport () then
            Modal.view
                { Modal.defaultProps with
                    IsOpen = true
                    Title = Some "Verschieben"
                    Subtitle = Some (monthLabel month.Month)
                    Size = Modal.Large
                    OnClose = close }
                [ Modal.body [ fields ]
                  Modal.footer [ moveButton month form dispatch ] ]
        else
            BottomSheet.view
                { IsOpen = true
                  OnClose = close
                  Title = "Verschieben"
                  Subtitle = Some (monthLabel month.Month)
                  Footer = Some [ moveButton month form dispatch ] }
                [ fields ]
    | _ -> Html.none

/// Verschiebungen mit fehlgeschlagener Zeile. Der Toast läuft ab, dieser Hinweis nicht.
let private moveRetries (retries: OpenMoveRetry list) (dispatch: Msg -> unit) =
    React.fragment (
        retries
        |> List.map (fun retry ->
            Html.div [
                prop.key retry.ApplicationId
                prop.custom ("data-testid", "move-retry")
                prop.className "flex items-center justify-between gap-3 rounded-xl border border-border-subtle bg-surface-card p-4"
                prop.children [
                    Html.div [
                        prop.className "flex flex-col gap-1 min-w-0"
                        prop.children [
                            Html.span [ prop.className ListRoles.primary; prop.text $"Nicht vollständig verschoben ({retry.MonthLabel}): {retry.Summary}" ]
                            Html.span [ prop.className ListRoles.muted; prop.text retry.Reason ]
                        ]
                    ]
                    Html.span [
                        prop.className "shrink-0"
                        prop.children [
                            Button.secondaryLoading retryMoveLabel (retry.State = RetryRunning) (fun () ->
                                dispatch (RetryMove retry.ApplicationId))
                        ]
                    ]
                ]
            ])
    )

// ============================================
// Kategorie-Detail (budgeting-m1bce)
// ============================================

let private detailFigure (testId: string) (label: string) (value: ReactElement) =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className "flex flex-col gap-1 min-w-0"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text label ]
            value
        ]
    ]

let private detailFigures (category: BudgetCategory) =
    Html.div [
        prop.className "grid grid-cols-3 gap-3"
        prop.children [
            detailFigure "detail-assigned" "Zugewiesen" (detailAmount category.Assigned)
            detailFigure "detail-activity" "Aktivität" (detailAmount category.Activity)
            detailFigure "detail-available" "Verfügbar" (availableAmount category.Available)
        ]
    ]

let private detailEntryLine (entry: CategoryMonthEntry) (dispatch: Msg -> unit) =
    let payee = entry.Payee |> Option.defaultValue "Ohne Empfänger"
    let date = entry.Date.ToString "dd.MM.yyyy"
    let label = $"{payee}, {moneyText entry.Amount}, {date}, im Kassenbuch öffnen"
    Button.listRow (string entry.EntryId) "detail-entry" label (fun () -> dispatch (OpenEntryInLedger entry.EntryId)) [
        Html.span [
            prop.className ListRoles.mobileLine1
            prop.children [
                Html.span [ prop.className $"{ListRoles.primary} truncate min-w-0"; prop.text payee ]
                Money.listAmount entry.Amount.Amount entry.Amount.Currency
            ]
        ]
        Html.span [
            prop.className ListRoles.mobileLine2
            prop.children [
                Html.span [ prop.className ListRoles.date; prop.text date ]
                match entry.AccountName with
                | Some account ->
                    Html.span [ prop.className $"{ListRoles.secondary} truncate"; prop.text $"· {account}" ]
                | None -> ()
                if entry.IsSplitShare then
                    Badge.neutral "Split"
            ]
        ]
    ]

let private detailTotal (activity: Money) (entries: CategoryMonthEntry list) =
    let total = entriesTotal activity entries
    Html.div [
        prop.className "flex flex-col gap-2 px-4 py-3"
        prop.children [
            Html.div [
                prop.className ListRoles.mobileLine1
                prop.children [
                    Html.span [ prop.className ListRoles.groupHeader; prop.text "Summe" ]
                    Html.span [ prop.custom ("data-testid", "detail-total"); prop.children [ Money.listAmount total.Amount total.Currency ] ]
                ]
            ]
            match deviationHint total activity with
            | Some hint ->
                Html.p [
                    prop.custom ("data-testid", "detail-deviation")
                    prop.className ListRoles.secondary
                    prop.text hint
                ]
            | None -> ()
        ]
    ]

let private detailEntries (category: BudgetCategory) (detail: CategoryDetail) (dispatch: Msg -> unit) =
    match detail.Entries with
    | NotAsked
    | Loading -> Loading.txListSkeleton 3
    | Failure message ->
        Html.div [
            prop.className "px-4"
            prop.children [ ErrorDisplay.card message (Some (fun () -> dispatch LoadCategoryEntries)) ]
        ]
    | Success entries ->
        React.fragment [
            if List.isEmpty entries then
                Html.p [
                    prop.custom ("data-testid", "detail-empty")
                    prop.className $"{ListRoles.muted} px-4 py-8 text-center"
                    prop.text "Keine Buchungen in diesem Monat"
                ]
            else
                Html.div [ prop.children (entries |> List.map (fun e -> detailEntryLine e dispatch)) ]
            detailTotal category.Activity entries
        ]

/// Am Handy `BottomSheet`, am PC `Modal` (ADR 0043), wie "Verschieben". Solange "Verschieben" offen
/// ist, tritt das Detail zurück: zwei Dialoge übereinander gibt es nicht.
let private categoryDetail (model: Model) (dispatch: Msg -> unit) =
    match model.Detail, model.Month, model.Move with
    | Some detail, Success month, None ->
        match detailCategory month detail with
        | Some (category, groupName) ->
            let close = fun () -> dispatch CloseCategoryDetail
            let subtitle = $"{groupName} · {monthLabel month.Month}"
            let head =
                Html.div [
                    prop.className "flex flex-col gap-4 p-4"
                    prop.children [
                        detailFigures category
                        Html.span [
                            prop.custom ("data-testid", "detail-move")
                            prop.children [ Button.secondary "Verschieben" (fun () -> dispatch (OpenMove category.Id)) ]
                        ]
                    ]
                ]
            // Die Zeilen tragen ihren Rand selbst (`px-4`), `-mx-5` hebt den von `.sheet-body` auf.
            let body (inSheet: bool) =
                Html.div [
                    prop.custom ("data-testid", "category-detail")
                    prop.className (if inSheet then "-mx-5" else "")
                    prop.children [
                        head
                        Html.h3 [ prop.className $"{ListRoles.groupHeader} px-4 pt-2 pb-1"; prop.text "Buchungen" ]
                        detailEntries category detail dispatch
                    ]
                ]
            if Viewport.isDesktopViewport () then
                Modal.view
                    { Modal.defaultProps with
                        IsOpen = true
                        Title = Some category.Name
                        Subtitle = Some subtitle
                        Size = Modal.Large
                        OnClose = close }
                    [ Modal.bodyNoPadding [ body false ] ]
            else
                BottomSheet.view
                    { IsOpen = true
                      OnClose = close
                      Title = category.Name
                      Subtitle = Some subtitle
                      Footer = None }
                    [ body true ]
        | None -> Html.none
    | _ -> Html.none

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
                | Success _ when targetsAllowed model.Choice ->
                    [ Button.secondary "Targets anwenden" (fun () -> dispatch OpenTargets) ]
                | _ -> []
            )
            targetsDialog model dispatch
            categoryDetail model dispatch
            moveDialog model dispatch
            monthSwitcher model dispatch
            moveRetries model.OpenRetries dispatch
            match model.Month with
            | NotAsked
            | Loading -> Loading.centered "Lade Budget ..."
            | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch Load))
            | Success month ->
                React.fragment [
                    toBeBudgetedCard month.ToBeBudgeted
                    categorySearchField model.CategorySearch dispatch
                    categories model.CategorySearch month dispatch
                ]
        ]
    ]
