/// Bausteine der Budget-Seite: Beträge, Monatsleiste und Kategorie-Liste.
module Components.Budget.MonthView

open Feliz
open Fable.Core.JsInterop
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Targets
open Components.Budget.Moves
open Components.Budget.Inspector
open Components.Budget.MonthStart
open Client.DesignSystem
open Client.DesignSystem.Tokens

// ============================================
// Beträge
// ============================================

/// Ein Knopf darin nimmt dem Feld den Fokus nicht (ADR 0005 Punkt 2): der Fehlbetrag-Knopf und der
/// Pause-Knopf im Modus Zuweisen. Die Schritte tragen das selbst.
let keepsFocus (children: ReactElement list) =
    Html.div [
        prop.onMouseDown (fun e -> e.preventDefault ())
        prop.children children
    ]

/// Die Klasse eines Betragsfelds, rot umrandet bei ungültiger Eingabe. Zugewiesen, Holen und Abgeben und
/// das Target teilen sie.
let amountFieldClass (invalid: bool) : string =
    if invalid then "w-full input-amount input-invalid tabular-nums" else "w-full input-amount tabular-nums"

/// Der Hinweis unter einem Betragsfeld mit ungültiger Eingabe. Das Feld verweist per `aria-describedby` auf
/// `id` (budgeting-g2ty9, F5).
let invalidHint (id: string) (text: string) =
    Html.span [ prop.id id; prop.className $"{ListRoles.primarySize} {Colors.error}"; prop.text text ]

/// Ein Betrag in Listenschrift, neutral eingefärbt: Zugewiesen, Aktivität, Target und der neue
/// Betrag der Vorschau sind keine Warnsignale, nur Verfügbar färbt ein Minus (`Money.available`).
let plainAmount (m: Money) =
    Html.span [
        prop.className $"font-mono font-medium tabular-nums whitespace-nowrap {ListRoles.primarySize} {Colors.textSecondary}"
        prop.text (moneyText m)
    ]

let availableAmount (m: Money) = Money.available m.Amount m.Currency

/// Der Inhalt, solange ein Monatswechsel läuft: gedimmt, ohne Spinner und ohne Sprung. Liste, Suche,
/// Filter und Tabs bleiben bedienbar, Fokus und Scrollposition überstehen den Wechsel. Die Aufträge
/// im Tab Geld und "Targets anwenden" sind gesperrt, bis der neue Monat da ist (`monthChanging`).
let monthChangeClass (model: Model) =
    if monthChanging model then "opacity-60 transition-opacity" else "transition-opacity"

/// Schreibt die gemessene Höhe einer sticky Leiste als CSS-Variable auf die Seite, `styles.css` leitet
/// daraus die Abstände darunter ab. Die Leisten brechen um, eine feste Höhe passte dann nicht. Der
/// Callback-Ref bindet auch einen neuen DOM-Knoten (Grund in `Ledger.View.FilterBarColumn`).
[<ReactComponent>]
let MeasuredBar (variable: string, attributes: IReactProperty list) =
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
let detailAmount (m: Money) =
    Html.span [
        prop.className $"font-mono font-medium tabular-nums whitespace-nowrap {ListRoles.primarySize} {Colors.textPrimary}"
        prop.text (moneyText m)
    ]

// ============================================
// Monatsleiste
// ============================================

/// Zu verteilen rechts in der Leiste, die Karte entfällt.
/// `compact`: am Handy ohne € wie die Skizze (`leiste-handy.png`), so passt der Kopf bei 390 px in eine Zeile.
let toBeBudgeted (compact: bool) (m: Money) =
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
                    ShowSign = false
                    ShowCurrency = not compact }
        ]
    ]

/// `‹ Monat ›`. Am Rand fehlt der Pfeil, sein Platz bleibt frei. Der Name führt zum laufenden Monat
/// und trägt den Akzent, sobald ein anderer gezeigt wird. Bleibt auch stehen, wenn der Monat nicht offen ist.
/// "2026-11-01" als "Nov 2026" wie im Kopf der Skizze am Handy. Ein unlesbarer Wert bleibt, wie er ist.
let compactMonthLabel (month: string) : string =
    match month.Split '-' with
    | [| year; _; _ |] when shortMonthLabel month <> month -> $"{shortMonthLabel month} {year}"
    | _ -> month

/// `compact`: am Handy der kurze Name ("Nov 2026"), damit der Kopf in eine Zeile passt (Skizze `leiste-handy.png`).
let monthStepper (compact: bool) (frame: BudgetFrame) (month: string) (dispatch: Msg -> unit) =
    let arrow testId label arrowText target =
        match target with
        | Some m -> Button.stepArrow testId label arrowText (fun () -> dispatch (SelectMonth m))
        | None -> Button.stepArrowGap ()
    let stepper = monthStepperFor frame month
    let label = if compact then Button.homeLabelCompact else Button.homeLabel
    Html.div [
        prop.className "flex shrink-0 items-center"
        prop.children [
            arrow "budget-month-previous" "Vorheriger Monat" "‹" stepper.Previous
            label
                "budget-month-name"
                $"{monthLabel month}, zum laufenden Monat"
                stepper.Elsewhere
                (if compact then compactMonthLabel month else monthLabel month)
                (fun () -> dispatch (SelectMonth frame.CurrentMonth))
            arrow "budget-month-next" "Nächster Monat" "›" stepper.Next
        ]
    ]

/// Zu verteilen, solange der Monat steht. `ml-auto` rückt es in einer eigenen Zeile nach rechts.
/// Der Name zeigt beim Blättern sofort das Ziel, Zu verteilen bleibt bis zur Antwort beim alten Monat.
let assignFigure (compact: bool) (model: Model) =
    match model.Month with
    | Success month ->
        Html.div [
            prop.className $"ml-auto {monthChangeClass model}"
            prop.children [ toBeBudgeted compact month.ToBeBudgeted ]
        ]
    | _ -> Html.none

/// Der Kopfknopf Monatserster (budgeting-m3x7f): ein Icon-Knopf wie Historie, öffnet und schließt die Leiste und schreibt
/// nichts. Im vergangenen Monat unsichtbar statt weg: sonst änderte der Kopf beim Blättern seine Breite und die Liste sprang.
/// `expanded`: die Leiste steht.
let monthStartButton (expanded: bool) (model: Model) (dispatch: Msg -> unit) =
    match model.Month with
    | Success _ ->
        Html.div [
            prop.custom ("data-testid", "budget-month-start-button")
            prop.className (if monthStartButtonVisible model then "shrink-0" else "shrink-0 invisible")
            prop.children [
                Button.view
                    { Button.defaultProps with
                        Variant = Button.Ghost
                        Size = Button.Small
                        Icon = Some (Icons.calendar Icons.SM Icons.Default)
                        OnClick = fun () -> dispatch ToggleMonthStart
                        Title = Some "Monatserster"
                        AriaLabel = Some "Monatserster"
                        AriaExpanded = Some expanded }
            ]
        ]
    | _ -> Html.none

let headStepper (compact: bool) (model: Model) (dispatch: Msg -> unit) =
    match model.Frame, shownMonth model with
    | Success frame, Some month -> monthStepper compact frame month dispatch
    | _ -> Html.none

/// Der Kopf der Seite als sticky Leiste.
let headBar (attributes: IReactProperty list) =
    MeasuredBar ("--budget-month-bar-height", prop.custom ("data-testid", "budget-month-switcher") :: attributes)

// ============================================
// Kategorien: Handy (Zeilen) und PC (Tabelle)
// ============================================

/// Fehlt in Gelb, klein: offen, kein Fehler (ADR 0075).
let missingBadge (testId: string) (text: string) =
    Html.span [
        prop.custom ("data-testid", testId)
        prop.className $"{ListRoles.missing} shrink-0 whitespace-nowrap"
        prop.text text
    ]

/// Die Markierung einer offenen Rückschreibung, PC und Handy gleich: neutral, damit Gelb in der Zeile
/// `fehlt` bleibt. Sie kürzt nie (budgeting-dgrp7).
let private writeBackBadge (testId: string) (c: BudgetCategory) =
    match writeBackMark c with
    | Some text ->
        Html.span [
            prop.custom ("data-testid", testId)
            prop.className "shrink-0"
            prop.children [ Badge.neutralSmall text ]
        ]
    | None -> Html.none

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
                    Truncate.text ListRoles.secondary $"Target {targetText target}"
                | None -> ()
                match listStatusText c with
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
                                  yield! Truncate.textProps ListRoles.primary c.Name
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
                              Truncate.text $"{ListRoles.muted} tabular-nums" (mobileFiguresText c)
                              writeBackBadge "budget-not-in-ynab-mobile" c
                          ]
                      ]
                      mobileStatusLine c ]
        ]
    ]

/// Target am PC in einer Zeile: Betrag in Mono, Rhythmus in Inter, dahinter pausiert.
let private targetCell (c: BudgetCategory) =
    match c.Target with
    | None -> Html.none
    | Some target ->
        Html.span [
            prop.custom ("data-testid", "budget-target")
            // Darf unter xl zwischen Betrag und Rhythmus umbrechen: neben dem Panel bei 1024 px schob ein einzeiliges
            // Target mit fünfstelligen Beträgen Verfügbar unter das Panel (infrastructure-nx14s). Ab xl bleibt die
            // Zeile einzeilig.
            prop.className "xl:whitespace-nowrap"
            prop.children [
                plainAmount target.Goal
                Html.span [
                    prop.className $"font-sans {ListRoles.primarySize} {Colors.textSecondary}"
                    prop.text (rhythmText target.Rhythm)
                ]
                match listStatusText c with
                | Some status -> Html.span [ prop.className ListRoles.secondary; prop.text $" · {status}" ]
                | None -> ()
            ]
        ]

let private desktopRow (selectedId: System.Guid option) (dispatch: Msg -> unit) (c: BudgetCategory) =
    let isSelected = selectedId = Some c.Id
    let rowColor = if isSelected then Backgrounds.accentSubtle else "hover:bg-surface-hover"
    Html.tr [
        prop.key (string c.Id)
        prop.custom ("data-testid", "budget-category-row")
        if isSelected then prop.custom ("aria-current", "true")
        // `budget-row-anchor`: die sticky Leisten verdecken sonst die Zeile, die per Pfeiltaste ins Bild rückt.
        prop.className $"{ListRoles.denseRowHeight} border-b border-border-subtle cursor-pointer budget-row-anchor {rowColor}"
        prop.onClick (fun _ -> dispatch (SelectCategory c.Id))
        prop.children [
            // max-w-0 und w-full: der Name nimmt den Restplatz und kürzt, statt die Tabelle aus
            // der Karte zu drücken (sonst fehlt Verfügbar neben dem Panel).
            Table.cell { Table.defaultCellProps with ClassName = Some "w-full max-w-0" } [
                Html.div [
                    // Der Name gibt zuerst nach und kürzt. Erst unter 80 px Rest rückt fehlt in eine
                    // zweite Zeile: dann bleiben beide lesbar, die Zeile wird höher.
                    prop.className "flex flex-wrap items-baseline gap-x-2 min-w-0"
                    prop.children [
                        Html.div [
                            prop.className "flex grow basis-20 min-w-0"
                            prop.children [
                                Button.listName "budget-category-name" $"{c.Name} wählen" false c.Name (fun () ->
                                    dispatch (SelectCategory c.Id))
                            ]
                        ]
                        match missingText c with
                        | Some text -> missingBadge "budget-missing" text
                        | None -> ()
                        writeBackBadge "budget-not-in-ynab" c
                    ]
                ]
            ]
            Table.cell { Table.defaultCellProps with Align = Some "right" } [ targetCell c ]
            for column in amountColumns do
                Table.cell { Table.defaultCellProps with Align = Some "right" } [
                    if column.IsAvailable then
                        Html.span [
                            prop.custom ("data-testid", "budget-available")
                            prop.children [ availableAmount (column.Value c) ]
                        ]
                    else
                        plainAmount (column.Value c)
                ]
        ]
    ]

let private desktopTable (selectedId: System.Guid option) (dispatch: Msg -> unit) (groups: BudgetGroup list) =
    Table.view { Table.defaultTableProps with Variant = Table.Default; Responsive = false; Sticky = true } [
        Table.thead [
            Table.calmHeaderCell "Kategorie" None
            Table.calmHeaderCell "Target" (Some "right")
            for column in amountColumns do
                Table.calmHeaderCell column.Header (Some "right")
        ]
        Table.tbody [
            for group in groups do
                Html.tr [
                    prop.key $"group-{group.Name}"
                    prop.children [
                        Html.td [
                            prop.colSpan (2 + List.length amountColumns)
                            prop.className $"{ListRoles.cellPaddingX} pt-2 pb-1 {ListRoles.groupHeader}"
                            prop.text group.Name
                        ]
                    ]
                ]
                for c in group.Categories do
                    desktopRow selectedId dispatch c
        ]
    ]

let categorySearchField (search: string) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "budget-category-search")
        // Am PC 256 px in der eigenen Zeile unter Kopf und Leiste wie die Skizze (budgeting-m3x7f), am Handy die volle Zeile.
        prop.className "relative flex-1 min-w-0 md:flex-none md:w-64"
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

/// Alle, Fehlt, Überzogen neben der Suche. Die Zahlen zählen den ganzen Monat, unabhängig von der Suche.
let filterChips (model: Model) (month: BudgetMonth) (dispatch: Msg -> unit) =
    let chip filter =
        ChipBar.chip
            { Label = filterChipLabel filter month.Groups
              IsActive = (model.Filter = filter)
              OnClick = fun () -> dispatch (SelectFilter filter)
              OnRemove = None }
    Html.div [
        prop.custom ("data-testid", "budget-filter")
        prop.className "budget-filter-chips flex shrink-0 items-center gap-2"
        prop.children [
            chip AllCategories
            chip MissingOnly
            chip OverdrawnOnly
        ]
    ]

/// Die gewählte Zeile rückt ins Bild, wenn sich die Auswahl ändert, nicht bei jedem Render: wer mit
/// dem Mausrad scrollt, soll nicht zurückspringen.
[<ReactComponent>]
let DesktopCategoryList (selectedId: System.Guid option, focusable: bool, content: ReactElement) =
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
        if focusable then
            prop.tabIndex 0
            prop.ariaLabel "Kategorien"
        prop.children [ content ]
    ]

let categories (model: Model) (month: BudgetMonth) (shown: BudgetGroup list) (dispatch: Msg -> unit) =
    let selectedId = model.Selected |> Option.map (fun s -> s.CategoryId)
    if categoryCount month = 0 then
        Card.emptyState (Icons.banknotes Icons.XL Icons.Default) "Keine Kategorien" "YNAB liefert für diesen Monat keine sichtbaren Kategorien." None
    elif List.isEmpty shown then
        Html.p [
            prop.custom ("data-testid", "budget-search-empty")
            prop.className $"{ListRoles.muted} px-4 py-8 text-center"
            prop.text (emptyListHint model.CategorySearch)
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
                desktopTable selectedId dispatch shown
            )
        ]

// ============================================
// Leiste Monatserster (budgeting-m3x7f)
// ============================================

let private tileState (state: FieldState) =
    match state with
    | FieldActive -> Button.TileActive
    | FieldDone -> Button.TileDone
    | FieldOpen -> Button.TileOpen

let private fieldTestId (field: MonthStartField) =
    match field with
    | CheckField -> "month-start-check"
    | ApplyField -> "month-start-apply"
    | OverdrawnField -> "month-start-overdrawn"
    | DistributeField -> "month-start-distribute"

let private monthStartFieldButton (panel: bool) (dispatch: Msg -> unit) (f: MonthStartFieldView) =
    Html.div [
        prop.key (fieldTestId f.Field)
        prop.className "grid min-w-0"
        prop.children [
            Button.tile (fieldTestId f.Field) f.Value f.Label f.Title f.AriaLabel (tileState f.State) (fun () -> dispatch (fieldMsg panel f.Field))
        ]
    ]

/// Die Leiste nach der Skizze (Zeile 45 bis 52): Karte mit Kopfzeile "Monatserster" und "Ausblenden", darunter vier
/// gleich breite Felder, an PC und Handy gleich. `bar`: die Felder aus `monthStartBar`, `None` wenn die Leiste nicht steht.
/// `panel`: der Inspektor steht als Panel daneben (PC).
let monthStartBarView (bar: MonthStartFieldView list option) (model: Model) (panel: bool) (dispatch: Msg -> unit) =
    match bar with
    | None -> Html.none
    | Some fields ->
        Html.section [
            prop.custom ("data-testid", "month-start-bar")
            prop.ariaLabel "Monatserster"
            prop.className
                $"flex flex-col gap-1.5 bg-surface-card border border-border-subtle rounded-[12px] pt-1.5 px-2.5 pb-2.5 {monthChangeClass model}"
            prop.children [
                Html.div [
                    prop.className "flex items-center justify-between gap-3"
                    prop.children [
                        Html.span [ prop.className $"{FontSizes.compact} font-semibold text-text-primary"; prop.text "Monatserster" ]
                        Button.quietText "month-start-hide" "Ausblenden" (fun () -> dispatch HideMonthStart)
                    ]
                ]
                Html.div [
                    prop.className "grid grid-cols-4 gap-1.5"
                    prop.children (fields |> List.map (monthStartFieldButton panel dispatch))
                ]
            ]
        ]
