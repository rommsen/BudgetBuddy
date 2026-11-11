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
open Components.Budget.Month
open Components.Budget.Targets
open Components.Budget.Moves
open Components.Budget.Inspector
open Client.DesignSystem
open Client.DesignSystem.Tokens
open Components.Budget.MonthView
open Components.Budget.TargetsView
open Components.Budget.MovesView
open Components.Budget.InspectorView

/// Wo der Fokus steht, für `pageKeyAction`. Das Suchfeld erkennt sie an seiner Hülle.
let private keyFocus (target: obj) : KeyFocus =
    let tag: string = target?tagName
    if tag = "INPUT" || tag = "TEXTAREA" || tag = "SELECT" then
        if isNull (target?closest ("[data-testid='budget-category-search']")) then TextField else SearchField
    else
        OtherFocus

/// Die Tasten der Seite ab 1024 px. Der Handler hängt am Dokument: nach einem Klick auf eine
/// Tabellenzeile steht der Fokus auf `body` und erreicht kein Element der Seite. `defaultPrevented`
/// heißt, `Tabs` hat die Taste schon genommen, so wechselt ein Druck genau einen Tab. Mit
/// Zusatztaste gehört der Pfeil dem Browser (Alt und Links: zurück) oder dem Feld (Umschalt: markieren).
/// Ein Zeichen ohne Feldfokus setzt den Fokus in die Suche; der Browser schreibt es dann dorthin,
/// Umschalt ist dabei erlaubt (Großbuchstaben).
[<ReactComponent>]
let private PageKeys (model: Model, dispatch: Msg -> unit) =
    let latest = React.useRef (model, dispatch)
    latest.current <- (model, dispatch)
    React.useEffect (
        (fun () ->
            let onKey (event: Browser.Types.Event) =
                let e = event :?> Browser.Types.KeyboardEvent
                if not (e.defaultPrevented || e.altKey || e.ctrlKey || e.metaKey) then
                    let current, send = latest.current
                    let focus = keyFocus e.target
                    if typesIntoSearch current e.key focus then
                        let search: obj = Browser.Dom.document.querySelector "[data-testid='budget-category-search'] input"
                        if not (isNull search) then search?focus ()
                    elif not e.shiftKey then
                        match pageKeyActionFor current e.key focus with
                        | LeaveKey -> ()
                        | action ->
                            e.preventDefault ()
                            send (PageKeyPressed action)
            Browser.Dom.document.addEventListener ("keydown", onKey)
            React.createDisposable (fun () -> Browser.Dom.document.removeEventListener ("keydown", onKey))),
        [||]
    )
    Html.none

// ============================================
// Seite
// ============================================

/// Der Knopf Historie: öffnet die allgemeine Historie `#/budget/history` (budgeting-h7q4v). Ein Icon-Knopf
/// (budgeting-c6vn4, M1): am Handy steht er im Kopf neben Zu verteilen, 48 px, und nimmt der Suche keinen
/// Platz; am PC hält er den Kopf ab 1024 px in einer Zeile.
let private historyButton (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "budget-history-button")
        prop.className "shrink-0"
        prop.children [
            Button.view
                { Button.defaultProps with
                    Variant = Button.Ghost
                    Size = Button.Small
                    Icon = Some (Icons.clock Icons.SM Icons.Default)
                    OnClick = fun () -> dispatch OpenGeneralHistory
                    Title = Some "Historie"
                    AriaLabel = Some "Historie" }
        ]
    ]

/// Die Farbe eines Gruppenkopfs im Bericht: Rot Fehler, Gelb braucht dich, Grün gelungen (ADR 0087).
type private ReportTone =
    | ToneError
    | ToneAttention
    | ToneSuccess

/// Eine Gruppe des Berichts: Kopf und je Kategorie eine Zeile, leer fällt sie weg.
let private reportGroup (testId: string) (title: string) (tone: ReportTone) (rows: (string * string option) list) =
    let titleClass =
        match tone with
        | ToneError -> $"{Tokens.FontSizes.xs} font-medium {Tokens.Colors.error}"
        | ToneAttention -> $"{Tokens.FontSizes.xs} font-medium {Tokens.Colors.attention}"
        | ToneSuccess -> $"{Tokens.FontSizes.xs} font-medium {Tokens.Colors.success}"
    if List.isEmpty rows then
        Html.none
    else
        Html.div [
            prop.custom ("data-testid", testId)
            prop.className "flex flex-col gap-1"
            prop.children [
                Html.p [ prop.className titleClass; prop.text $"{title} ({List.length rows})" ]
                // Der Name allein ist kein Schlüssel: zwei Gruppen können eine Kategorie gleichen Namens haben.
                for index, (name, detail) in List.indexed rows do
                    Html.div [
                        prop.key $"{index}-{name}"
                        prop.className "flex flex-col"
                        prop.children [
                            Html.span [ prop.className ListRoles.primary; prop.text name ]
                            match detail with
                            | Some text -> Html.span [ prop.className ListRoles.muted; prop.text text ]
                            | None -> ()
                        ]
                    ]
            ]
        ]

/// Der einmalige Lauf "Targets nach YNAB schreiben" (budgeting-e2z7v): Bestätigung, Warten, Bericht mit "Geschrieben",
/// "Mit Abweichung" und "Fehlgeschlagen", nach einem 429 auch die offenen Kategorien. Die Gruppen bildet
/// `targetsToYnabGroups`.
let private targetsToYnabDialog (model: Model) (dispatch: Msg -> unit) =
    let close () = dispatch CloseWriteAllTargets
    match model.TargetsToYnab with
    | TargetsToYnabClosed -> Html.none
    | TargetsToYnabAsking ->
        Modal.confirm
            true
            "Targets nach YNAB schreiben"
            "BB schreibt jedes Target nach YNAB, das dort abweicht. Wochentag und Tag im Monat kann BB in YNAB nicht setzen: Ändern sich Art oder Rhythmus, gehen sie dort verloren."
            "Schreiben"
            (fun () -> dispatch ConfirmWriteAllTargets)
            close
    | TargetsToYnabRunning -> Modal.loading true "Targets werden nach YNAB geschrieben …"
    | TargetsToYnabFailed message -> Modal.alert true "Targets nach YNAB schreiben" $"Nichts geschrieben: {message}" "Schließen" close
    | TargetsToYnabDone report ->
        let groups = Components.Budget.TargetEdit.targetsToYnabGroups report
        Modal.view
            { Modal.defaultProps with IsOpen = true; Title = Some "Targets nach YNAB"; OnClose = close; Size = Modal.Small }
            [ Modal.body [
                  Html.div [
                      prop.custom ("data-testid", "targets-to-ynab-report")
                      prop.className "flex flex-col gap-4"
                      prop.children [
                          if report.RateLimited then
                              Html.p [
                                  prop.className ListRoles.missing
                                  prop.text "YNAB hat zu viele Anfragen gemeldet, der Lauf brach ab. Die offenen Kategorien später erneut senden."
                              ]
                          reportGroup "targets-to-ynab-failed" "Fehlgeschlagen" ToneError (groups.Failed |> List.map (fun (n, r) -> n, Some r))
                          reportGroup "targets-to-ynab-open" "Nicht versucht" ToneAttention (groups.NotAttempted |> List.map (fun n -> n, None))
                          reportGroup "targets-to-ynab-gaps" "Mit Abweichung" ToneAttention (groups.WithGaps |> List.map (fun (n, gaps) -> n, Some (String.concat ", " gaps)))
                          reportGroup "targets-to-ynab-written" "Geschrieben" ToneSuccess (groups.Written |> List.map (fun n -> n, None))
                          if groups.Unchanged > 0 then
                              Html.p [ prop.className ListRoles.muted; prop.text $"{groups.Unchanged} schon gleich in YNAB" ]
                      ]
                  ]
              ]
              Modal.footer [ Button.primary "Schließen" close ] ]

/// Das Menü ⋮ im Seitenkopf (budgeting-pscgb, Prototyp Version 17 "⋯"): am PC `FilterPopover`, am Handy
/// `BottomSheet` (ADR 0043, Vorbild das Menü der Regeln). Einträge: "Kategorien verwalten" und "Targets nach YNAB
/// schreiben" (budgeting-e2z7v).
let private budgetMenu (model: Model) (dispatch: Msg -> unit) =
    let close = fun () -> dispatch CloseBudgetMenu
    let open' inSheet =
        fun () ->
            dispatch OpenCategoryAdmin
            if inSheet then Viewport.swallowNextClick 350
    let items inSheet =
        let first = if Viewport.isFinePointer () then FilterPopover.menuItemAutoFocus else FilterPopover.menuItem
        [ first "Kategorien verwalten" (open' inSheet)
          FilterPopover.menuItem "Targets nach YNAB schreiben" (fun () ->
              dispatch AskWriteAllTargets
              if inSheet then Viewport.swallowNextClick 350) ]
    Html.div [
        prop.className "relative shrink-0"
        prop.onKeyDown (fun e -> if e.key = "Escape" then close ())
        prop.children [
            Html.span [
                prop.custom ("data-testid", "budget-menu-button")
                // Ohne das wertet das Popover den Mausdruck als Klick außerhalb und öffnete gleich wieder.
                prop.onMouseDown (fun e -> e.stopPropagation ())
                prop.children [
                    Button.view
                        { Button.defaultProps with
                            Variant = Button.Ghost
                            Size = Button.Small
                            Icon = Some (Icons.ellipsisVertical Icons.MD Icons.Default)
                            AriaLabel = Some "Weitere Aktionen"
                            Title = Some "Weitere Aktionen"
                            AriaExpanded = Some model.MenuOpen
                            OnClick = fun () -> dispatch ToggleBudgetMenu }
                ]
            ]
            if Viewport.isDesktopViewport () then
                FilterPopover.viewEnd
                    { IsOpen = model.MenuOpen; OnClose = close; WidthClassName = FilterPopover.menuWidthClass }
                    (items false)
            // Am Handy nur offen gerendert: das zu Sheet trüge sonst einen zweiten Kopf "Budget" und ein zweites
            // "Schließen" auf der Seite.
            elif model.MenuOpen then
                BottomSheet.view
                    { IsOpen = true; OnClose = close; Title = "Budget"; Subtitle = None; Footer = None }
                    (items true)
        ]
    ]

/// Der Kopf wie die Skizze (budgeting-m3x7f): `‹ Monat ›`, Historie und Monatserster links, Zu verteilen rechts.
/// Suche und Chips stehen darunter in eigener Zeile (`searchRow`). Am Handy der kurze Monatsname, damit alles in eine
/// Zeile passt (Zu verteilen dort ohne €); erst schmaler oder mit fünfstelligem Betrag bricht Zu verteilen um.
let private head (wide: bool) (barStands: bool) (model: Model) (dispatch: Msg -> unit) =
    headBar [
        prop.className (
            if wide then "budget-month-bar flex flex-wrap items-center gap-x-3 gap-y-1 py-1 -mx-1 px-1"
            else "budget-month-bar flex flex-wrap items-center py-2 -mx-1 px-1"
        )
        prop.children [
            headStepper (not wide) model dispatch
            historyButton dispatch
            budgetMenu model dispatch
            monthStartButton barStands model dispatch
            assignFigure (not wide) model
        ]
    ]

/// Suche und Filter unter Kopf und Leiste wie die Skizze (`leiste-pc.png`, `mitte-pc.png`). Am PC in einer Zeile, am
/// Handy nur die Suche: sie scrollt mit, die Chips liegen in der Liste und haften dort.
let private searchRow (model: Model) (month: BudgetMonth) (wide: bool) (dispatch: Msg -> unit) =
    Html.div [
        prop.className $"flex items-center gap-2 md:gap-3 {monthChangeClass model}"
        prop.children [
            categorySearchField model.CategorySearch dispatch
            if wide then filterChips model month dispatch
        ]
    ]

/// Leiste Monatserster und darunter die Suchzeile, 10 px Abstand wie die Skizze. Ohne geladenen Monat fällt der Block
/// weg (die Leiste steht nur mit geladenem Monat).
let private belowHead (bar: Components.Budget.MonthStart.MonthStartFieldView list option) (model: Model) (panel: bool) (wide: bool) (dispatch: Msg -> unit) =
    match model.Month with
    | Success month ->
        Html.div [
            prop.className "flex flex-col gap-2.5"
            prop.children [ monthStartBarView bar model panel dispatch; searchRow model month wide dispatch ]
        ]
    | _ -> Html.none

let private listArea (model: Model) (shown: BudgetGroup list) (wide: bool) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "budget-list-area")
        prop.custom ("aria-busy", monthChanging model)
        prop.className $"@container flex flex-col gap-4 min-w-0 {monthChangeClass model}"
        prop.children [
            match model.Month with
            | NotAsked
            | Loading -> Loading.centered "Lade Budget ..."
            | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch Load))
            | Success month ->
                if not wide then filterChips model month dispatch
                categories model month shown dispatch
        ]
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    let isPanel = Viewport.isInspectorPanelViewport ()
    // ADR 0043: zur Renderzeit genau ein Kopf, ein Suchfeld kann nicht doppelt stehen.
    let wide = Viewport.isDesktopViewport ()
    // Einmal je Render: Kopfknopf und Leiste lesen dieselben Felder.
    let bar = Components.Budget.MonthStart.monthStartBar model
    // Einmal je Render: Liste, Pfeile und Zahlen nutzen dieselben Zeilen.
    let shown =
        match model.Month with
        | Success month -> shownGroups model.Filter model.CategorySearch month.Groups
        | _ -> []

    // Die Seite setzt ihre Ränder selbst, sie läuft über die volle Breite (`pageContentFull`).
    Primitives.Container.full [
        Html.div [
            prop.custom ("data-testid", "budget-page")
            prop.className "budget-page flex flex-col gap-4 md:gap-2"
            prop.children [
                PageHeader.screenReaderTitle "Budget"
                targetsDialog model dispatch
                targetsToYnabDialog model dispatch
                head wide bar.IsSome model dispatch
                // Die Leiste Monatserster steht zwischen Kopf und Suche (budgeting-m3x7f), an PC und Handy wie die Skizze.
                belowHead bar model isPanel wide dispatch
                // Steht das Sheet offen, trägt es den Hinweis. Genau eine Stelle, nie beide. Der Platz bleibt
                // auch leer belegt: fiele er weg, rückten die Geschwister auf, React baute Liste und Sheet
                // neu auf, und das Sheet führe nicht mehr ein.
                moveRetries model (pageRetries isPanel model) dispatch
                // ADR 0043: zur Renderzeit genau eine Hülle um denselben Inhalt.
                if isPanel then
                    Html.div [
                        prop.className "grid grid-cols-[minmax(0,1fr)_340px] gap-4 items-start"
                        prop.children [
                            PageKeys (model, dispatch)
                            listArea model shown wide dispatch
                            inspectorPanel model dispatch
                        ]
                    ]
                else
                    listArea model shown wide dispatch
                    inspectorSheet model shown dispatch
            ]
        ]
    ]
