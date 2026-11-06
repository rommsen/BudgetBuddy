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
        prop.className $"flex flex-col gap-4 min-w-0 {monthChangeClass model}"
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
