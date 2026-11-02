/// Die Budget-Seite (budgeting-k7wmd, neu aufgebaut in budgeting-g8s3w). Eine Seite mit zwei Ebenen:
/// Die Liste zeigt, wie es steht. Der Inspektor enthält alles zu einer Kategorie, ab 1024 px als
/// Panel neben der Liste, darunter als `BottomSheet`, beide mit fester Höhe. Eine Zeile wählt die
/// Kategorie, alle Aktionen liegen im Inspektor.
///
/// Am Handy Zeilen ohne horizontales Scrollen, ab md eine Tabelle mit fünf Spalten.
///
/// Ausschließlich Design-System-Bausteine und Tokens (Styleguide-Gate `design-system-001`).
/// Ein negatives Zu verteilen erscheint einfach negativ, ohne Hinweis und ohne Sperre.
module Components.Budget.View

open Feliz
open Fable.Core.JsInterop
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

/// Der Inhalt, solange ein Monatswechsel läuft: gedimmt, ohne Spinner und ohne Sprung. Liste, Suche,
/// Filter und Tabs bleiben bedienbar, Fokus und Scrollposition überstehen den Wechsel. "Verschieben"
/// und "Targets anwenden" sind gesperrt, bis der neue Monat da ist (`monthChanging`).
let private monthChangeClass (model: Model) =
    if monthChanging model then "opacity-60 transition-opacity" else "transition-opacity"

/// Schreibt die gemessene Höhe einer sticky Leiste als CSS-Variable auf die Seite, `styles.css` leitet
/// daraus die Abstände darunter ab. Die Leisten brechen um, eine feste Höhe passte dann nicht. Der
/// Callback-Ref bindet auch einen neuen DOM-Knoten (Grund in `Ledger.View.FilterBarColumn`).
[<ReactComponent>]
let private MeasuredBar (variable: string, attributes: IReactProperty list) =
    let disconnect = React.useRef<(unit -> unit) option> None
    let measure =
        React.useCallback (
            (fun (element: Browser.Types.Element) ->
                disconnect.current |> Option.iter (fun stop -> stop ())
                disconnect.current <- None
                if not (isNull (box element)) then
                    let page: obj = element?closest (".budget-page")
                    let stopObserving =
                        Viewport.observeHeight element (fun height ->
                            page?style?setProperty (variable, $"{height}px"))
                    disconnect.current <-
                        Some (fun () ->
                            stopObserving ()
                            page?style?removeProperty (variable) |> ignore)),
            [||]
        )
    Html.div (prop.ref measure :: attributes)

/// Kopfzahl im Inspektor: ruhig, ohne Warnfarbe und Glow.
let private detailAmount (m: Money) =
    Html.span [
        prop.className $"font-mono font-medium tabular-nums whitespace-nowrap {ListRoles.primarySize} {Colors.textPrimary}"
        prop.text (moneyText m)
    ]

// ============================================
// Monatsleiste
// ============================================

/// Zu verteilen rechts in der Leiste, die Karte entfällt.
let private toBeBudgeted (m: Money) =
    Html.div [
        prop.custom ("data-testid", "budget-to-be-budgeted")
        prop.className "flex flex-col items-end"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text "Zu verteilen" ]
            Money.view
                { Money.defaultProps with
                    Amount = m.Amount
                    Currency = m.Currency
                    Size = Money.Large
                    Glow = Money.NoGlow
                    ShowSign = false }
        ]
    ]

/// `‹ Monat ›`. Am Rand fehlt der Pfeil, sein Platz bleibt frei. Der Name führt zum laufenden Monat
/// und ist teal, sobald ein anderer gezeigt wird. Bleibt auch stehen, wenn der Monat nicht offen ist.
let private monthStepper (frame: BudgetFrame) (month: string) (dispatch: Msg -> unit) =
    let arrow testId label arrowText target =
        match target with
        | Some m -> Button.stepArrow testId label arrowText (fun () -> dispatch (SelectMonth m))
        | None -> Button.stepArrowGap ()
    Html.div [
        prop.className "flex items-center"
        prop.children [
            arrow "budget-month-previous" "Vorheriger Monat" "‹" (previousMonth frame.Months month)
            Button.homeLabel
                "budget-month-name"
                $"{monthLabel month}, zum laufenden Monat"
                (month <> frame.CurrentMonth)
                (monthLabel month)
                (fun () -> dispatch (SelectMonth frame.CurrentMonth))
            arrow "budget-month-next" "Nächster Monat" "›" (nextMonth frame.Months month)
        ]
    ]

/// Die sticky Leiste: Monat links, Zu verteilen rechts. Bricht bei 320 px um, statt seitlich zu laufen.
/// Der Name zeigt beim Blättern sofort das Ziel, Zu verteilen bleibt bis zur Antwort beim alten Monat.
let private monthBar (model: Model) (dispatch: Msg -> unit) =
    MeasuredBar (
        "--budget-month-bar-height",
        [ prop.custom ("data-testid", "budget-month-switcher")
          prop.className "budget-month-bar flex flex-wrap items-center gap-x-3 py-2 -mx-1 px-1"
          prop.children [
              match model.Frame, shownMonth model with
              | Success frame, Some month -> monthStepper frame month dispatch
              | _ -> ()
              match model.Month with
              | Success month ->
                  Html.div [
                      prop.className $"ml-auto {monthChangeClass model}"
                      prop.children [ toBeBudgeted month.ToBeBudgeted ]
                  ]
              | _ -> ()
          ] ]
    )

// ============================================
// Kategorien: Handy (Zeilen) und PC (Tabelle)
// ============================================

/// Fehlt in Gelb, klein: offen, kein Fehler (ADR 0075).
let private missingBadge (testId: string) (text: string) =
    Html.span [
        prop.custom ("data-testid", testId)
        prop.className $"{ListRoles.missing} shrink-0 whitespace-nowrap"
        prop.text text
    ]

/// Die dritte Zeile am Handy: Target und Status, fehlt im Statusplatz. Wird es eng, kürzt das
/// Target, nie der Status: er ist die Aussage der Zeile.
let private mobileStatusLine (c: BudgetCategory) =
    let missing = missingText c
    if c.Target.IsNone && missing.IsNone then
        Html.none
    else
        Html.span [
            prop.custom ("data-testid", "budget-target")
            prop.className ListRoles.mobileLine2
            prop.children [
                match c.Target with
                | Some target ->
                    Html.span [
                        prop.className $"{ListRoles.secondary} truncate min-w-0"
                        prop.text $"Target {targetText target}"
                    ]
                | None -> ()
                match targetStatusText c with
                | Some status ->
                    Html.span [
                        prop.className $"{ListRoles.secondary} shrink-0 whitespace-nowrap"
                        prop.text $"· {status}"
                    ]
                | None -> ()
                match missing with
                | Some text -> missingBadge "budget-missing" text
                | None -> ()
            ]
        ]

let private mobileGroup (selectedId: System.Guid option) (dispatch: Msg -> unit) (group: BudgetGroup) =
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
                Button.selectableListRow
                    (string c.Id)
                    "budget-category-row"
                    $"{c.Name} wählen"
                    (selectedId = Some c.Id)
                    (fun () -> dispatch (SelectCategory c.Id))
                    [ Html.span [
                          prop.className ListRoles.mobileLine1
                          prop.children [
                              Html.span [
                                  prop.custom ("data-testid", "budget-category-name")
                                  prop.className $"{ListRoles.primary} truncate min-w-0"
                                  prop.text c.Name
                              ]
                              Html.span [
                                  prop.custom ("data-testid", "budget-available")
                                  prop.children [ availableAmount c.Available ]
                              ]
                          ]
                      ]
                      Html.span [
                          prop.className ListRoles.mobileLine2
                          prop.children [
                              Html.span [
                                  prop.className $"{ListRoles.muted} truncate tabular-nums"
                                  prop.text $"Zugewiesen {moneyText c.Assigned} · Aktivität {moneyText c.Activity}"
                              ]
                          ]
                      ]
                      mobileStatusLine c ]
        ]
    ]

/// Target am PC: Betrag in Mono, Rhythmus in Inter, darunter pausiert oder erfüllt.
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

let private desktopRow (selectedId: System.Guid option) (dispatch: Msg -> unit) (c: BudgetCategory) =
    let isSelected = selectedId = Some c.Id
    let rowColor = if isSelected then Backgrounds.tealSubtle else "hover:bg-surface-hover"
    Html.tr [
        prop.key (string c.Id)
        prop.custom ("data-testid", "budget-category-row")
        if isSelected then prop.custom ("aria-current", "true")
        // `budget-row-anchor`: die sticky Leisten verdecken sonst die Zeile, die per Pfeiltaste ins Bild rückt.
        prop.className $"{ListRoles.rowHeight} border-b border-border-subtle cursor-pointer budget-row-anchor {rowColor}"
        prop.onClick (fun _ -> dispatch (SelectCategory c.Id))
        prop.children [
            // max-w-0 und w-full: der Name nimmt den Restplatz und kürzt, statt die Tabelle aus
            // der Karte zu drücken (sonst fehlt Verfügbar neben dem Panel).
            Table.cell { Table.defaultCellProps with ClassName = Some "w-full max-w-0" } [
                Html.div [
                    // Wird es eng, rückt fehlt unter den Namen: beide bleiben lesbar.
                    prop.className "flex flex-wrap items-baseline gap-x-2 min-w-0"
                    prop.children [
                        Button.listName "budget-category-name" $"{c.Name} wählen" false c.Name (fun () ->
                            dispatch (SelectCategory c.Id))
                        match missingText c with
                        | Some text -> missingBadge "budget-missing" text
                        | None -> ()
                    ]
                ]
            ]
            Table.cell { Table.defaultCellProps with Align = Some "right" } [ targetCell c ]
            Table.cell { Table.defaultCellProps with Align = Some "right" } [ plainAmount c.Assigned ]
            Table.cell { Table.defaultCellProps with Align = Some "right" } [ plainAmount c.Activity ]
            Table.cell { Table.defaultCellProps with Align = Some "right" } [
                Html.span [
                    prop.custom ("data-testid", "budget-available")
                    prop.children [ availableAmount c.Available ]
                ]
            ]
        ]
    ]

let private desktopTable (selectedId: System.Guid option) (dispatch: Msg -> unit) (groups: BudgetGroup list) =
    Table.view { Table.defaultTableProps with Variant = Table.Default; Responsive = false; Sticky = true } [
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
                    desktopRow selectedId dispatch c
        ]
    ]

let private categorySearchField (search: string) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "budget-category-search")
        prop.className "relative flex-1 min-w-0"
        prop.children [
            Input.text
                { Input.textInputDefaults with
                    Value = search
                    OnChange = CategorySearchChanged >> dispatch
                    Placeholder = "Kategorie suchen…"
                    AriaLabel = Some "Kategorie suchen"
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

/// Alle, Fehlt, Rot neben der Suche. Die Zahlen zählen den ganzen Monat, unabhängig von der Suche.
let private filterChips (model: Model) (month: BudgetMonth) (dispatch: Msg -> unit) =
    let chip label filter =
        ChipBar.chip
            { Label = label
              IsActive = (model.Filter = filter)
              OnClick = fun () -> dispatch (SelectFilter filter)
              OnRemove = None }
    Html.div [
        prop.custom ("data-testid", "budget-filter")
        prop.className "budget-filter-chips flex items-center gap-2"
        prop.children [
            chip "Alle" AllCategories
            chip $"Fehlt ({filterCount MissingOnly month.Groups})" MissingOnly
            chip $"Rot ({filterCount RedOnly month.Groups})" RedOnly
        ]
    ]

/// Die gewählte Zeile rückt ins Bild, wenn sich die Auswahl ändert, nicht bei jedem Render: wer mit
/// dem Mausrad scrollt, soll nicht zurückspringen.
[<ReactComponent>]
let private DesktopCategoryList (selectedId: System.Guid option, withKeyboard: bool, dispatch: Msg -> unit, content: ReactElement) =
    let listRef = React.useElementRef ()
    React.useEffect (
        (fun () ->
            match listRef.current with
            | Some element ->
                let row: obj = element?querySelector ("[aria-current='true']")
                if not (isNull row) then
                    row?scrollIntoView (createObj [ "block" ==> "nearest" ])
            | None -> ()),
        [| box (selectedId |> Option.map string) |]
    )
    Html.div [
        prop.ref listRef
        // `overflow-clip` statt `overflow-hidden`: sonst haftet der Spaltenkopf an dieser Karte statt an der Seite.
        prop.className "hidden md:block rounded-xl border border-border-subtle bg-surface-card overflow-clip"
        if withKeyboard then
            // Tasten in Inspektor oder Suchfeld erreichen die Liste nicht, beide liegen außerhalb.
            prop.tabIndex 0
            prop.ariaLabel "Kategorien"
            prop.onKeyDown (fun e ->
                match e.key with
                | "ArrowDown" ->
                    e.preventDefault ()
                    dispatch (StepCategory Next)
                | "ArrowUp" ->
                    e.preventDefault ()
                    dispatch (StepCategory Previous)
                | _ -> ())
        prop.children [ content ]
    ]

let private categories (model: Model) (month: BudgetMonth) (shown: BudgetGroup list) (dispatch: Msg -> unit) =
    let selectedId = model.Selected |> Option.map (fun s -> s.CategoryId)
    if categoryCount month = 0 then
        Card.emptyState (Icons.banknotes Icons.XL Icons.Default) "Keine Kategorien" "YNAB liefert für diesen Monat keine sichtbaren Kategorien." None
    elif List.isEmpty shown then
        let search = model.CategorySearch.Trim()
        Html.p [
            prop.custom ("data-testid", "budget-search-empty")
            prop.className $"{ListRoles.muted} px-4 py-8 text-center"
            prop.text (
                if search = "" then "Keine Kategorie in dieser Ansicht."
                else $"Keine Kategorie passt zu „{search}“"
            )
        ]
    else
        React.fragment [
            Html.div [
                prop.className "md:hidden"
                prop.children (shown |> List.map (mobileGroup selectedId dispatch))
            ]
            DesktopCategoryList (
                selectedId,
                Viewport.isInspectorPanelViewport (),
                dispatch,
                desktopTable selectedId dispatch shown
            )
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

let private previewBody (order: System.Guid list) (preview: TargetPreview) (changed: bool) =
    Modal.bodyNoPadding [
        if changed then
            Html.div [
                prop.custom ("data-testid", "targets-changed")
                prop.className "p-4"
                prop.children [
                    ErrorDisplay.notice "Stand hat sich geändert. Die Vorschau ist neu geladen, es wurde nichts geschrieben."
                ]
            ]
        if List.isEmpty preview.Lines then
            Html.p [
                prop.custom ("data-testid", "targets-empty")
                prop.className "p-4 text-sm text-text-secondary"
                prop.text "Keine Kategorie hat einen Fehlbetrag. Es gibt nichts anzuwenden."
            ]
        else
            Html.div [ prop.children (orderedPreviewLines order preview.Lines |> List.map previewLine) ]
        skippedLines preview.Skipped
    ]

let private outcomeLine (l: ApplyLineResult) =
    let detail, badge =
        match l.Outcome with
        | Written -> $"{moneyText l.OldAssigned} auf {moneyText l.NewAssigned}", Badge.success "geschrieben"
        | Failed reason -> reason, Badge.error "fehlgeschlagen"
        | ChangedInYnab -> "YNAB steht anders als erwartet, nichts überschrieben", Badge.notice "in YNAB geändert"
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
                            ErrorDisplay.notice $"Zu verteilen weicht von der Vorschau ab (Vorschau: {moneyText preview.ToBeBudgetedAfter})."
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
            previewBody (categoryOrder model) preview changed
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
// Inspektor (budgeting-g8s3w)
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

let private detailEntries (category: BudgetCategory) (selected: SelectedCategory) (dispatch: Msg -> unit) =
    match selected.Entries with
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

/// Tab Geld: die Beträge, darunter fehlt und "Verschieben". Zuweisen kommt in budgeting-c5w2n.
let private moneyTab (category: BudgetCategory) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "inspector-money")
        prop.className "flex flex-col gap-4 p-4"
        prop.children [
            detailFigures category
            match missingText category with
            | Some text -> missingBadge "inspector-missing" text
            | None -> ()
            Html.span [
                prop.custom ("data-testid", "detail-move")
                prop.children [ Button.secondary "Verschieben" (fun () -> dispatch (OpenMove category.Id)) ]
            ]
        ]
    ]

/// Tab Target, nur lesend: Pflege kommt in budgeting-t6p8z.
let private targetTab (category: BudgetCategory) =
    Html.div [
        prop.custom ("data-testid", "inspector-target")
        prop.className "flex flex-col gap-4 p-4"
        prop.children [
            match category.Target with
            | None -> Html.p [ prop.className ListRoles.muted; prop.text "Kein Target" ]
            | Some target ->
                detailFigure "target-goal" "Target" (Html.span [ prop.className ListRoles.primary; prop.text (targetText target) ])
                match targetStatusText category with
                | Some status -> detailFigure "target-status" "Status" (Html.span [ prop.className ListRoles.secondary; prop.text status ])
                | None -> ()
                match category.Underfunded with
                | Some under when not category.Snoozed -> detailFigure "target-underfunded" "Fehlbetrag" (detailAmount under)
                | _ -> ()
        ]
    ]

let private inspectorTabs =
    [ "Geld", MoneyTab
      "Buchungen", EntriesTab
      "Historie", HistoryTab
      "Target", TargetTab ]

/// Der Inhalt des gewählten Tabs, in Panel und Sheet derselbe.
let private inspectorContent (model: Model) (selected: SelectedCategory) (category: BudgetCategory) (dispatch: Msg -> unit) =
    let activeIndex = inspectorTabs |> List.findIndex (fun (_, tab) -> tab = model.Tab)
    Tabs.panel "inspector-tabs" activeIndex [
        match model.Tab with
        | MoneyTab -> moneyTab category dispatch
        | EntriesTab ->
            Html.div [
                prop.custom ("data-testid", "category-detail")
                prop.children [ detailEntries category selected dispatch ]
            ]
        | HistoryTab ->
            Html.p [
                prop.custom ("data-testid", "inspector-history")
                prop.className $"{ListRoles.muted} p-4"
                prop.text "Die Historie dieser Kategorie folgt."
            ]
        | TargetTab -> targetTab category
    ]

let private inspectorTabBar (model: Model) (dispatch: Msg -> unit) =
    Tabs.bar
        "inspector-tabs"
        "Bereiche der Kategorie"
        (inspectorTabs
         |> List.map (fun (label, value) ->
             { Label = label
               IsActive = (model.Tab = value)
               OnClick = fun () -> dispatch (SelectInspectorTab value) }))

/// `‹ ›` im Sheet: wechselt die Kategorie ohne Schließen, am Rand fehlt der Pfeil.
let private sheetStepper (model: Model) (shown: BudgetGroup list) (subtitle: string) (dispatch: Msg -> unit) =
    let arrow testId label arrowText direction =
        match neighbourCategory direction shown model with
        | Some _ -> Button.stepArrow testId label arrowText (fun () -> dispatch (StepCategory direction))
        | None -> Button.stepArrowGap ()
    Html.div [
        prop.className "flex items-center justify-between gap-2"
        prop.children [
            arrow "inspector-previous" "Vorherige Kategorie" "‹" Previous
            Html.span [ prop.className $"{ListRoles.secondary} truncate min-w-0"; prop.text subtitle ]
            arrow "inspector-next" "Nächste Kategorie" "›" Next
        ]
    ]

/// Offene Verschiebungen im festen Bereich des Sheets. Es deckt die Seite ab, der Hinweis dort wäre
/// nach Ablauf des Toasts nicht mehr bedienbar.
let private sheetRetries (retries: OpenMoveRetry list) (dispatch: Msg -> unit) =
    if List.isEmpty retries then
        Html.none
    else
        Html.div [
            prop.className "flex flex-col gap-2 pb-2"
            prop.children [ moveRetries retries dispatch ]
        ]

/// Die Kategorie, die das Sheet zeigt. `None`, solange "Verschieben" offen ist: es tritt dann
/// zurück, zwei Sheets übereinander gibt es nicht. Danach steht es im selben Tab wieder da.
let private sheetCategory (model: Model) =
    match model.Selected, model.Month, model.Move with
    | Some selected, Success month, None ->
        selectedCategory month selected |> Option.map (fun found -> selected, month, found)
    | _ -> None

/// Das Sheet unter 1024 px. Immer gerendert, damit es ein- und ausfährt. Beim Ausfahren hält
/// `BottomSheet.inspector` den letzten Inhalt.
let private inspectorSheet (model: Model) (shown: BudgetGroup list) (dispatch: Msg -> unit) =
    let props title isOpen : BottomSheet.BottomSheetProps =
        { IsOpen = isOpen
          OnClose = fun () -> dispatch CloseInspector
          Title = title
          Subtitle = None
          Footer = None }
    match sheetCategory model with
    | Some (selected, month, (category, groupName)) ->
        BottomSheet.inspector
            (props category.Name true)
            [ sheetRetries model.OpenRetries dispatch
              sheetStepper model shown $"{groupName} · {monthLabel month.Month}" dispatch
              inspectorTabBar model dispatch ]
            // `-mx-5` hebt den Rand von `.sheet-body` auf, die Zeilen tragen ihn selbst.
            [ Html.div [
                  prop.custom ("data-testid", "budget-inspector")
                  prop.className "-mx-5"
                  prop.children [ inspectorContent model selected category dispatch ]
              ] ]
    | None -> BottomSheet.inspector (props "" false) [] []

/// Das Panel ab 1024 px. Der Hinweis steht ohne Auswahl da, die Höhe bleibt gleich. Beim Blättern
/// bleibt der Inhalt stehen. Lädt der Monat bei bestehender Auswahl, steht ein Skeleton da. Scheitert
/// das Laden, bleibt das Panel neben der Fehlerkarte leer: es kommt kein Inhalt nach.
let private inspectorPanel (model: Model) (dispatch: Msg -> unit) =
    let chosen =
        match model.Selected, model.Month with
        | Some selected, Success month ->
            selectedCategory month selected |> Option.map (fun found -> selected, month, found)
        | _ -> None
    Html.aside [
        prop.custom ("data-testid", "budget-inspector")
        prop.className $"budget-inspector-panel flex flex-col rounded-xl border border-border-subtle bg-surface-card overflow-hidden {monthChangeClass model}"
        prop.children [
            match chosen with
            | Some (selected, month, (category, groupName)) ->
                Html.div [
                    prop.className "flex flex-col gap-1 p-4 shrink-0"
                    prop.children [
                        Html.h2 [ prop.className $"{ListRoles.primary} truncate"; prop.text category.Name ]
                        Html.span [ prop.className $"{ListRoles.secondary} truncate"; prop.text $"{groupName} · {monthLabel month.Month}" ]
                    ]
                ]
                Html.div [ prop.className "shrink-0"; prop.children [ inspectorTabBar model dispatch ] ]
                Html.div [
                    prop.className "flex-1 min-h-0 overflow-y-auto"
                    prop.children [ inspectorContent model selected category dispatch ]
                ]
            | None when model.Selected.IsSome && model.Month = Loading -> Loading.txListSkeleton 3
            | None when model.Selected.IsSome -> ()
            | None ->
                Html.p [
                    prop.className $"{ListRoles.muted} p-4 m-auto text-center"
                    prop.text "Kategorie wählen"
                ]
        ]
    ]

// ============================================
// Seite
// ============================================

let private listArea (model: Model) (shown: BudgetGroup list) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "budget-list-area")
        prop.custom ("aria-busy", monthChanging model)
        prop.className $"flex flex-col gap-4 min-w-0 {monthChangeClass model}"
        prop.children [
            match model.Month with
            | NotAsked
            | Loading -> Loading.centered "Lade Budget ..."
            | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch Load))
            | Success month ->
                // PC: Suche und Chips haften zusammen. Handy: `display: contents`, nur die Chips haften.
                MeasuredBar (
                    "--budget-filter-bar-height",
                    [ prop.className "budget-filter-bar md:flex-row md:items-center gap-2 py-1"
                      prop.children [
                          categorySearchField model.CategorySearch dispatch
                          filterChips model month dispatch
                      ] ]
                )
                categories model month shown dispatch
        ]
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    let isPanel = Viewport.isInspectorPanelViewport ()
    // Einmal je Render: Liste, Pfeile und Zahlen nutzen dieselben Zeilen.
    let shown =
        match model.Month with
        | Success month -> shownGroups model.Filter model.CategorySearch month.Groups
        | _ -> []

    Html.div [
        prop.custom ("data-testid", "budget-page")
        prop.className "budget-page flex flex-col gap-4"
        prop.children [
            PageHeader.calm "Budget" None (
                // Unsichtbar statt weg: sonst änderte der Kopf beim Blättern seine Höhe und die Liste sprang.
                match model.Month, displayedMonthKind model |> Option.exists targetsAllowed with
                | Success _, allowed ->
                    [ Html.div [
                          prop.className (if allowed then "" else "invisible")
                          prop.children [ Button.secondary "Targets anwenden" (fun () -> dispatch OpenTargets) ]
                      ] ]
                | _ -> []
            )
            targetsDialog model dispatch
            moveDialog model dispatch
            monthBar model dispatch
            // Steht das Sheet offen, trägt es den Hinweis. Genau eine Stelle, nie beide. Der Platz bleibt
            // auch leer belegt: fiele er weg, rückten die Geschwister auf, React baute Liste und Sheet
            // neu auf, und das Sheet führe nicht mehr ein.
            moveRetries (if isPanel || (sheetCategory model).IsNone then model.OpenRetries else []) dispatch
            // ADR 0043: zur Renderzeit genau eine Hülle um denselben Inhalt.
            if isPanel then
                Html.div [
                    prop.className "grid grid-cols-[minmax(0,1fr)_340px] gap-4 items-start"
                    prop.children [
                        listArea model shown dispatch
                        inspectorPanel model dispatch
                    ]
                ]
            else
                listArea model shown dispatch
                inspectorSheet model shown dispatch
        ]
    ]
