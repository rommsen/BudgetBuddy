/// Die Seite Targets prüfen `#/budget/target-check` (budgeting-q4v8n, ADR 0082): die Kategorien des laufenden
/// Monats in fünf Gruppen, je Zeile Werte, Balken der gezählten Monate, "<Betrag> übernehmen" und "Anpassen …".
/// "Anpassen …" öffnet das Target-Formular ab 1024 px als Panel rechts (340 px wie der Inspektor), darunter als
/// `BottomSheet`. Den Inspektor rendert die Seite nicht, die festen ids des Formulars bleiben eindeutig. Was
/// erscheint und was gesperrt ist, sagen `TargetCheck` und der Kern `TargetFormCore`. Ausschließlich
/// Design-System-Bausteine.
module Components.Budget.TargetCheckView

open Feliz
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Inspector
open Components.Budget.TargetFormCore
open Components.Budget.TargetCheck
open Client.DesignSystem
open Client.DesignSystem.Tokens

/// `last:border-b-0`: die letzte Zeile trägt keine Linie, die Karte hat schon ihren Rand.
let private rowClass = $"{ListRoles.mobileRowPadding} flex flex-col gap-2 border-b border-border-subtle last:border-b-0"

[<Literal>]
let private BarsLabel = "Ausgaben der gezählten Monate"

/// Was eine Zeile zeigt, einmal vor der Schleife gerechnet (CLAUDE.md, große Listen).
type private RowView = {
    Row: TargetCheckRow
    Facts: CheckRowFacts
    Bars: Stats.MonthBar list
    Take: (string * TargetButtonState) option
}

let private rowViewOf (model: Model) (data: TargetCheckData) (row: TargetCheckRow) : RowView =
    let facts = checkRowFacts row
    { Row = row
      Facts = facts
      Bars = if facts.Note.IsSome then [] else checkRowBars data row |> List.map spendingMonthBar
      Take =
        match takeText row, takeButton model data row with
        | Some text, Some state -> Some (text, state)
        | _ -> None }

/// Target als Monatswert, der gelesene Betrag mit Rhythmus klein dahinter, Ø und die Zahl der Monate.
let private factsView (facts: CheckRowFacts) =
    Html.div [
        prop.custom ("data-testid", "target-check-facts")
        prop.className "flex flex-wrap items-baseline gap-x-3 gap-y-0.5"
        prop.children [
            match facts.Monthly with
            | Some text ->
                Html.span [
                    prop.className "inline-flex items-baseline gap-1.5 min-w-0"
                    prop.children [
                        Html.span [ prop.className ListRoles.secondary; prop.text text ]
                        match facts.Read with
                        | Some read -> Html.span [ prop.className ListRoles.muted; prop.text read ]
                        | None -> ()
                    ]
                ]
            | None -> ()
            match facts.Average with
            | Some text -> Html.span [ prop.className $"{ListRoles.secondary} whitespace-nowrap"; prop.text text ]
            | None -> ()
            Html.span [ prop.className $"{ListRoles.muted} whitespace-nowrap"; prop.text facts.Count ]
        ]
    ]

let private rowView (dispatch: Msg -> unit) (view: RowView) =
    let row = view.Row
    Html.div [
        prop.key (string row.CategoryId)
        prop.custom ("data-testid", "target-check-row")
        prop.className rowClass
        prop.children [
            Html.div [
                prop.className "flex flex-col min-w-0"
                prop.children [
                    Html.span [ prop.custom ("data-testid", "target-check-name"); prop.className $"{ListRoles.primary} truncate"; prop.text row.Name ]
                    Html.span [ prop.className $"{ListRoles.muted} truncate"; prop.text row.GroupName ]
                ]
            ]
            match view.Facts.Note with
            | Some note ->
                Html.p [ prop.custom ("data-testid", "target-check-note"); prop.className ListRoles.secondary; prop.text note ]
            | None ->
                factsView view.Facts
                Stats.monthBarsCompact BarsLabel view.Bars
            Html.div [
                prop.className "flex flex-wrap gap-2"
                prop.children [
                    match view.Take with
                    | Some (text, state) ->
                        Html.div [
                            prop.custom ("data-testid", "target-check-take")
                            prop.children [
                                Button.view
                                    { Button.defaultProps with
                                        Text = text
                                        Variant = Button.Secondary
                                        IsBusy = state.Busy
                                        IsDisabled = state.Disabled
                                        ClassName = Some TouchTargets.sheetMinHeight
                                        OnClick = fun () -> dispatch (TakeSuggestion row.CategoryId) }
                            ]
                        ]
                    | None -> ()
                    Html.div [
                        prop.custom ("data-testid", "target-check-adjust")
                        prop.children [
                            Button.view
                                { Button.defaultProps with
                                    Text = "Anpassen …"
                                    Variant = Button.Secondary
                                    ClassName = Some TouchTargets.sheetMinHeight
                                    OnClick = fun () -> dispatch (AdjustTarget row.CategoryId) }
                        ]
                    ]
                ]
            ]
        ]
    ]

/// Satz und Zahl im Kopf einer Gruppe.
let private groupHead (group: CheckGroup) (count: int) =
    Html.span [
        prop.className "flex items-center justify-between gap-3"
        prop.children [
            Html.span [ prop.className ListRoles.primary; prop.text (checkGroupText group) ]
            Html.span [ prop.className $"{ListRoles.secondary} font-mono tabular-nums"; prop.text (string count) ]
        ]
    ]

let private groupCard (testId: string) (children: ReactElement list) =
    Html.section [
        prop.custom ("data-testid", testId)
        prop.className "rounded-xl border border-border-subtle bg-surface-card overflow-clip"
        prop.children children
    ]

/// Eine offene Gruppe: Kopf und Zeilen.
let private openGroup (model: Model) (data: TargetCheckData) (dispatch: Msg -> unit) (group: CheckGroup) (rows: TargetCheckRow list) =
    groupCard "target-check-group" [
        Html.div [
            prop.className $"{ListRoles.mobileRowPadding} border-b border-border-subtle"
            prop.children [ groupHead group (List.length rows) ]
        ]
        yield! rows |> List.map (rowViewOf model data >> rowView dispatch)
    ]

/// "Passt": klappt auf und zu, zu rendert sie ihre Zeilen nicht.
let private fitsGroup (model: Model) (data: TargetCheckData) (dispatch: Msg -> unit) (rows: TargetCheckRow list) =
    let isOpen = model.TargetCheck.FitsOpen
    let rowsId = "target-check-fits-rows"
    groupCard "target-check-group" [
        Button.disclosureListRow "fits" "target-check-fits-toggle" rowsId isOpen (fun () -> dispatch ToggleFits) [
            groupHead Fits (List.length rows)
        ]
        Html.div [
            prop.id rowsId
            prop.hidden (not isOpen)
            prop.className "border-t border-border-subtle"
            prop.children (if isOpen then rows |> List.map (rowViewOf model data >> rowView dispatch) else [])
        ]
    ]

let private groupsView (model: Model) (data: TargetCheckData) (dispatch: Msg -> unit) =
    let groups = checkGroups data
    Html.div [
        prop.custom ("data-testid", "target-check-list")
        prop.className "flex flex-col gap-4 min-w-0"
        prop.children [
            if nothingToCheck groups then
                Html.p [
                    prop.custom ("data-testid", "target-check-nothing")
                    prop.className $"{ListRoles.muted} px-4 py-4 text-center"
                    prop.text NothingToCheckText
                ]
            for group, rows in groups do
                match group with
                | Fits -> fitsGroup model data dispatch rows
                | _ -> openGroup model data dispatch group rows
        ]
    ]

/// Das Formular des Panels, angebunden an die Nachrichten der Seite. Abbrechen steht am Ende.
let private panelForm (model: Model) (row: TargetCheckRow) (form: TargetForm) (isPanel: bool) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "target-check-form")
        prop.className "flex flex-col gap-4"
        prop.children [
            TargetFormView.view
                { Shown = row.Target
                  Form = form
                  Locks = panelLocks model form
                  IsPanel = isPanel
                  Status = Html.none
                  Pause = Html.none
                  OnAmountChanged = CheckAmountChanged >> dispatch
                  OnDateChanged = CheckDateChanged >> dispatch
                  OnRhythmChanged = CheckRhythmChanged >> dispatch
                  OnWeekdayChanged = CheckWeekdayChanged >> dispatch
                  OnSave = fun () -> dispatch SaveCheckTarget
                  OnConfirmChange = fun () -> dispatch ConfirmCheckTargetChange
                  OnCancelChange = fun () -> dispatch CancelCheckTargetChange
                  OnAskRemove = fun () -> dispatch AskRemoveCheckTarget
                  OnConfirmRemove = fun () -> dispatch ConfirmRemoveCheckTarget
                  OnCancelRemove = fun () -> dispatch CancelRemoveCheckTarget }
            Html.div [
                prop.custom ("data-testid", "target-check-cancel")
                prop.children [
                    Button.view
                        { Button.defaultProps with
                            Text = "Abbrechen"
                            Variant = Button.Secondary
                            ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                            OnClick = fun () -> dispatch CloseCheckPanel }
                ]
            ]
        ]
    ]

/// Das Panel ab 1024 px, wie der Inspektor: Name, Gruppe und Monat oben, darunter das Formular.
let private sidePanel (model: Model) (dispatch: Msg -> unit) =
    match panelRow model with
    | Some (row, form) ->
        Html.aside [
            prop.custom ("data-testid", "target-check-panel")
            prop.className "budget-inspector-panel flex flex-col rounded-xl border border-border-subtle bg-surface-card overflow-hidden"
            prop.children [
                Html.div [
                    prop.className "flex flex-col gap-1 p-4 shrink-0"
                    prop.children [
                        Html.h2 [ prop.className $"{ListRoles.primary} truncate"; prop.text row.Name ]
                        Html.span [ prop.className $"{ListRoles.secondary} truncate"; prop.text $"{row.GroupName} · {monthLabel form.Month}" ]
                    ]
                ]
                Html.div [
                    prop.className "flex-1 min-h-0 overflow-y-auto px-4 pb-4"
                    prop.children [ panelForm model row form true dispatch ]
                ]
            ]
        ]
    | None -> Html.none

/// Das Sheet unter 1024 px. Immer gerendert, damit es ein- und ausfährt.
let private sheet (model: Model) (dispatch: Msg -> unit) =
    let props title subtitle isOpen : BottomSheet.BottomSheetProps =
        { IsOpen = isOpen
          OnClose = fun () -> dispatch CloseCheckPanel
          Title = title
          Subtitle = subtitle
          Footer = None }
    match panelRow model with
    | Some (row, form) ->
        BottomSheet.inspector
            (props row.Name (Some $"{row.GroupName} · {monthLabel form.Month}") true)
            []
            []
            [ Html.div [
                  prop.custom ("data-testid", "target-check-sheet")
                  prop.className "py-4"
                  prop.children [ panelForm model row form false dispatch ]
              ] ]
    | None -> BottomSheet.inspector (props "" None false) [] [] []

let view (model: Model) (dispatch: Msg -> unit) =
    let isPanel = Viewport.isInspectorPanelViewport ()
    let check = model.TargetCheck
    let content =
        match check.Data with
        | NotAsked
        | Loading -> Loading.txListSkeleton 5
        | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch RetryTargetCheck))
        | Success data -> groupsView model data dispatch
    // Die Klasse `budget-page` setzt die Maße unter der Navbar, an denen das Panel haftet.
    Primitives.Container.full [
        Html.div [
            prop.custom ("data-testid", "target-check-page")
            prop.className "budget-page flex flex-col gap-4 py-2"
            prop.children [
                PageHeader.screenReaderTitle "Targets prüfen"
                Html.div [
                    prop.className "flex flex-wrap items-center gap-x-3 gap-y-1"
                    prop.children [
                        Button.secondary "Zurück" (fun () -> dispatch LeaveTargetCheck)
                        Html.span [ prop.className ListRoles.primary; prop.text "Targets prüfen" ]
                        match check.Data with
                        | Success data ->
                            Html.span [
                                prop.custom ("data-testid", "target-check-month")
                                prop.className ListRoles.secondary
                                prop.text (monthLabel data.Month)
                            ]
                        | _ -> ()
                    ]
                ]
                // ADR 0043: zur Renderzeit genau eine Hülle. Ohne "Anpassen …" ist das Panel zu.
                if isPanel then
                    match panelRow model with
                    | Some _ ->
                        Html.div [
                            prop.className "grid grid-cols-[minmax(0,1fr)_340px] gap-4 items-start"
                            prop.children [ content; sidePanel model dispatch ]
                        ]
                    | None -> content
                else
                    content
                    sheet model dispatch
            ]
        ]
    ]
