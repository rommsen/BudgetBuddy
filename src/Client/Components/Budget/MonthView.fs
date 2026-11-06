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
    Html.span [ prop.id id; prop.className $"{ListRoles.primarySize} {Colors.neonRed}"; prop.text text ]

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
let toBeBudgeted (m: Money) =
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
let monthStepper (frame: BudgetFrame) (month: string) (dispatch: Msg -> unit) =
    let arrow testId label arrowText target =
        match target with
        | Some m -> Button.stepArrow testId label arrowText (fun () -> dispatch (SelectMonth m))
        | None -> Button.stepArrowGap ()
    let stepper = monthStepperFor frame month
    Html.div [
        prop.className "flex items-center"
        prop.children [
            arrow "budget-month-previous" "Vorheriger Monat" "‹" stepper.Previous
            Button.homeLabel
                "budget-month-name"
                $"{monthLabel month}, zum laufenden Monat"
                stepper.Elsewhere
                (monthLabel month)
                (fun () -> dispatch (SelectMonth frame.CurrentMonth))
            arrow "budget-month-next" "Nächster Monat" "›" stepper.Next
        ]
    ]

/// Zu verteilen, solange der Monat steht. `ml-auto` rückt es in einer eigenen Zeile nach rechts.
/// Der Name zeigt beim Blättern sofort das Ziel, Zu verteilen bleibt bis zur Antwort beim alten Monat.
let assignFigure (model: Model) =
    match model.Month with
    | Success month ->
        Html.div [
            prop.className $"ml-auto {monthChangeClass model}"
            prop.children [ toBeBudgeted month.ToBeBudgeted ]
        ]
    | _ -> Html.none

/// Unsichtbar statt weg: sonst änderte der Kopf beim Blättern seine Breite und die Liste sprang.
let targetsButton (model: Model) (dispatch: Msg -> unit) =
    match model.Month with
    | Success _ ->
        let allowed = targetsButtonVisible model
        Html.div [
            prop.className (if allowed then "shrink-0" else "shrink-0 invisible")
            prop.children [ Button.secondary "Targets anwenden" (fun () -> dispatch OpenTargets) ]
        ]
    | _ -> Html.none

let headStepper (model: Model) (dispatch: Msg -> unit) =
    match model.Frame, shownMonth model with
    | Success frame, Some month -> monthStepper frame month dispatch
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
                    Html.span [
                        prop.className $"{ListRoles.secondary} truncate min-w-0"
                        prop.text $"Target {targetText target}"
                    ]
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
                                  prop.className $"{ListRoles.muted} truncate min-w-0 tabular-nums"
                                  prop.text (mobileFiguresText c)
                              ]
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
            prop.className "whitespace-nowrap"
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
    let rowColor = if isSelected then Backgrounds.tealSubtle else "hover:bg-surface-hover"
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
        // Zwischen 768 und 1280 px schmaler (budgeting-a3k7m): sonst bricht der Kopf bei 1024 px mit echten
        // Zahlen um und der Knopf Historie steht allein in Zeile 2.
        prop.className "relative flex-1 min-w-0 md:min-w-28 md:w-28 xl:min-w-32 xl:w-32"
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
