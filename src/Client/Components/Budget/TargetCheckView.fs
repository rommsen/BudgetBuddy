/// Die Seite Targets prüfen `#/budget/target-check` (budgeting-q4v8n, Form nach der Skizze budgeting-c4ve4,
/// ADR 0082): ein Kopf mit Erklärung und Zählern, darunter die Kategorien des laufenden Monats in fünf Gruppen,
/// je Gruppe eine Karte, die auf- und zuklappt. Eine Zeile zeigt Name und Target, Mini-Balken, Ø und Monate,
/// "<Betrag> übernehmen" und "Anpassen …". Ist die Liste mindestens 720 px breit, steht die Zeile in vier Spalten
/// (Container-Abfrage auf der Liste), sonst wie am Handy untereinander. "Anpassen …" öffnet ab 1024 px ein Panel
/// rechts, darunter ein `BottomSheet`. Was erscheint und was gesperrt ist, sagen `TargetCheck` und der Kern
/// `TargetFormCore`.
module Components.Budget.TargetCheckView

open Feliz
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.TargetFormCore
open Components.Budget.TargetCheck
open Client.DesignSystem
open Client.DesignSystem.Tokens

/// Was eine Zeile zeigt, einmal vor der Schleife gerechnet (CLAUDE.md, große Listen).
type private RowView = {
    Row: TargetCheckRow
    Wide: CheckRowText
    Narrow: CheckRowText
    Line: int option
    Bars: Stats.SparkBar list
    Take: (string * TargetButtonState) option
    Selected: bool
}

let private rowViewOf (model: Model) (data: TargetCheckData) (selected: System.Guid option) (row: TargetCheckRow) : RowView =
    let line, bars = checkSparkBars row
    { Row = row
      Wide = checkRowText Wide row
      Narrow = checkRowText Narrow row
      Line = line
      Bars = bars
      Take =
        match takeText row, takeButton model data row with
        | Some text, Some state -> Some (text, state)
        | _ -> None
      Selected = selected = Some row.CategoryId }

// Die Zeile ist ein Raster. Schmal: Name und Ø in Zeile 1, die Balken über die volle Breite, darunter Zusatz und
// Knöpfe. Breit (Liste ab 720 px): vier Spalten wie in der Skizze, der Zusatz unter dem Namen.

let private rowClass (selected: bool) =
    let back = if selected then "bg-surface-hover" else ""
    $"grid grid-cols-[minmax(0,1fr)_auto] gap-x-4 gap-y-2 items-start px-3 py-3 border-t border-border-subtle @min-[720px]:grid-cols-[minmax(0,1.3fr)_132px_minmax(0,1fr)_auto] @min-[720px]:gap-x-4 @min-[720px]:items-center @min-[720px]:gap-y-0.5 @min-[720px]:px-4 @min-[720px]:py-2.5 {back}"

let private nameClass (hasNote: bool) =
    let self = if hasNote then "@min-[720px]:self-end" else ""
    $"col-start-1 row-start-1 flex min-w-0 flex-col gap-0.5 {self}"

let private averageClass (hasNote: bool) =
    let span = if hasNote then "@min-[720px]:row-span-2" else ""
    $"col-start-2 row-start-1 flex flex-col items-end gap-0.5 text-right @min-[720px]:col-start-3 @min-[720px]:items-start @min-[720px]:text-left {span}"

/// Die Balken füllen ihre Zelle: schmal 28 px hoch über die volle Breite, breit 32 px in der Spalte von 132 px.
let private barsClass (hasNote: bool) =
    let span = if hasNote then "@min-[720px]:row-span-2" else ""
    $"col-span-2 row-start-2 h-7 @min-[720px]:col-span-1 @min-[720px]:col-start-2 @min-[720px]:row-start-1 @min-[720px]:h-8 @min-[720px]:self-center {span}"

let private noteClass =
    $"col-span-2 row-start-3 {ListRoles.secondary} {Colors.neonTeal} @min-[720px]:col-span-1 @min-[720px]:col-start-1 @min-[720px]:row-start-2 @min-[720px]:self-start"

let private actionsClass (hasNote: bool) =
    let row = if hasNote then "row-start-4 @min-[720px]:row-span-2" else "row-start-3"
    $"col-span-2 {row} flex gap-2 @min-[720px]:col-span-1 @min-[720px]:col-start-4 @min-[720px]:row-start-1 @min-[720px]:justify-end"

/// Schmal sichtbar, breit verborgen, und umgekehrt.
let private narrowOnly = "@min-[720px]:hidden"
let private wideOnly = "hidden @min-[720px]:inline"

/// Ein Text in zwei Fassungen: schmal und breit.
let private twoWidths (narrow: string) (wide: string) =
    [ Html.span [ prop.className narrowOnly; prop.text narrow ]
      Html.span [ prop.className wideOnly; prop.text wide ] ]

/// Ein Knopf der Zeile: schmal halbe Breite mit 48 px, breit so breit wie sein Text.
let private rowButtonClass = $"w-full text-center {TouchTargets.sheetMinHeight} @max-[720px]:px-3 @min-[720px]:w-auto @min-[720px]:px-3.5 @min-[720px]:whitespace-nowrap"
let private rowButtonWrap = "flex min-w-0 flex-1 @min-[720px]:flex-none"

let private rowView (dispatch: Msg -> unit) (view: RowView) =
    let row = view.Row
    let hasNote = view.Wide.Note.IsSome
    Html.div [
        prop.key (string row.CategoryId)
        prop.custom ("data-testid", "target-check-row")
        prop.className (rowClass view.Selected)
        prop.children [
            Html.div [
                prop.className (nameClass hasNote)
                prop.children [
                    Html.span [ prop.custom ("data-testid", "target-check-name"); prop.className $"{ListRoles.primary} truncate"; prop.text row.Name ]
                    Html.span [ prop.className ListRoles.muted; prop.children (twoWidths view.Narrow.Target view.Wide.Target) ]
                ]
            ]
            Html.div [
                prop.custom ("data-testid", "target-check-average")
                prop.className (averageClass hasNote)
                prop.children [
                    match view.Narrow.Average, view.Wide.Average with
                    | Some narrow, Some wide ->
                        Html.span [
                            prop.className $"font-mono {FontWeights.medium} text-sm tabular-nums whitespace-nowrap {Colors.textPrimary}"
                            prop.children (twoWidths narrow wide)
                        ]
                    | _ -> ()
                    match view.Narrow.Count, view.Wide.Count with
                    | Some narrow, Some wide -> Html.span [ prop.className ListRoles.muted; prop.children (twoWidths narrow wide) ]
                    | _ -> ()
                ]
            ]
            Html.div [
                prop.className (barsClass hasNote)
                prop.children [ Stats.sparkBars (checkBarsLabel row) view.Line view.Bars ]
            ]
            match view.Wide.Note with
            | Some note -> Html.p [ prop.custom ("data-testid", "target-check-note"); prop.className noteClass; prop.text note ]
            | None -> ()
            Html.div [
                prop.className (actionsClass hasNote)
                prop.children [
                    match view.Take with
                    | Some (text, state) ->
                        Html.div [
                            prop.custom ("data-testid", "target-check-take")
                            prop.className rowButtonWrap
                            prop.children [
                                Button.view
                                    { Button.defaultProps with
                                        Text = text
                                        Variant = Button.Secondary
                                        IsBusy = state.Busy
                                        IsDisabled = state.Disabled
                                        ClassName = Some rowButtonClass
                                        OnClick = fun () -> dispatch (TakeSuggestion row.CategoryId) }
                            ]
                        ]
                    | None -> ()
                    Html.div [
                        prop.custom ("data-testid", "target-check-adjust")
                        prop.className rowButtonWrap
                        prop.children [
                            Button.view
                                { Button.defaultProps with
                                    Text = "Anpassen …"
                                    Variant = Button.Secondary
                                    AriaExpanded = Some view.Selected
                                    ClassName = Some rowButtonClass
                                    OnClick = fun () -> dispatch (AdjustTarget row.CategoryId) }
                        ]
                    ]
                ]
            ]
        ]
    ]

/// Die Farbe des Titels einer Gruppe (Nachtrag ADR 0075 vom 2026-11-06). Steht hier, damit
/// der Klassen-Wächter die Klassen in derselben Datei findet.
let checkGroupTitleClass (group: CheckGroup) : string =
    match group with
    | SpendsMore -> Colors.neonOrange
    | SpendsLess -> Colors.calmBlue
    | NeverSpends -> Colors.calmViolet
    | WithoutTarget -> Colors.textPrimary
    | Fits -> Colors.neonTeal

let private cardClass = "rounded-2xl border border-border-subtle bg-surface-card overflow-clip"

let private groupKey (group: CheckGroup) =
    match group with
    | SpendsMore -> "more"
    | SpendsLess -> "less"
    | NeverSpends -> "never"
    | WithoutTarget -> "without"
    | Fits -> "fits"

/// Eine Gruppe als Karte: der Kopf klappt auf und zu (Titel in der Farbe der Gruppe, Zahl, Regelsatz), zu
/// rendert sie ihre Zeilen nicht.
let private groupCard (model: Model) (data: TargetCheckData) (selected: System.Guid option) (dispatch: Msg -> unit) (group: CheckGroup) (rows: TargetCheckRow list) =
    let key = groupKey group
    let isOpen = model.TargetCheck.OpenGroups.Contains group
    let rowsId = $"target-check-{key}-rows"
    let headingId = $"target-check-{key}-heading"
    Html.section [
        prop.key key
        prop.custom ("data-testid", "target-check-group")
        prop.ariaLabelledBy headingId
        prop.className cardClass
        prop.children [
            // Die Überschrift trägt den Knopf, wie im Akkordeon-Muster der WAI-ARIA: eine Rolle "heading" im Knopf
            // läse der Screenreader nicht, die Kinder eines Knopfs sind nur Text.
            // Ein `div` mit Rolle "heading" statt `h2`: die globale, ungelayerte Regel für h2 in `styles.css`
            // (Display-Schrift, 600, enger Abstand) schlägt jede Tailwind-Klasse und gäbe dem Regelsatz die Schrift
            // eines Titels. Titel und Regelsatz stehen in der Schrift der Liste wie in der Skizze.
            Html.div [
                prop.role "heading"
                prop.ariaLevel 2
                prop.custom ("data-testid", "target-check-group-heading")
                prop.children [
                    Button.disclosureListRow key "target-check-group-toggle" rowsId isOpen (fun () -> dispatch (ToggleCheckGroup group)) [
                        Html.span [
                            prop.className $"{ListRoles.primarySize} {FontWeights.semibold}"
                            prop.children [
                                Html.span [ prop.id headingId; prop.custom ("data-testid", "target-check-group-title"); prop.className (checkGroupTitleClass group); prop.text (checkGroupText group) ]
                                Html.span [ prop.className $"ml-1.5 font-mono {FontWeights.medium} {Colors.textMuted}"; prop.text (string (List.length rows)) ]
                            ]
                        ]
                        Html.span [ prop.className $"{ListRoles.muted} mt-0.5"; prop.text (checkGroupRule group) ]
                    ]
                ]
            ]
            Html.div [
                prop.id rowsId
                prop.hidden (not isOpen)
                prop.children (if isOpen then rows |> List.map (rowViewOf model data selected >> rowView dispatch) else [])
            ]
        ]
    ]

let private groupsView (model: Model) (data: TargetCheckData) (groups: (CheckGroup * TargetCheckRow list) list) (dispatch: Msg -> unit) =
    // Einmal vor der Schleife: die Zeile mit offenem Panel (CLAUDE.md, große Listen).
    let selected = panelCategory model
    Html.div [
        prop.custom ("data-testid", "target-check-list")
        prop.className "@container flex flex-col gap-3 min-w-0"
        prop.children [
            if nothingToCheck groups then
                Html.p [
                    prop.custom ("data-testid", "target-check-nothing")
                    prop.className $"{ListRoles.muted} px-4 py-4 text-center"
                    prop.text NothingToCheckText
                ]
            for group, rows in groups do
                groupCard model data selected dispatch group rows
        ]
    ]

/// Beim Laden: zwei Karten mit kompakten Zeilen, so hoch wie die echten.
let private skeleton () =
    let pulse = "rounded bg-surface-elevated animate-pulse"
    Html.div [
        prop.custom ("data-testid", "target-check-skeleton")
        prop.ariaLabel "Lade die Prüfung"
        prop.className "@container flex flex-col gap-3 min-w-0"
        prop.children [
            for card in 1..2 do
                Html.div [
                    prop.key card
                    prop.className cardClass
                    prop.children [
                        Html.div [
                            prop.className $"{ListRoles.mobileRowPadding} flex flex-col gap-1.5"
                            prop.children [ Html.div [ prop.className $"w-40 h-4 {pulse}" ]; Html.div [ prop.className $"w-72 max-w-full h-3 {pulse}" ] ]
                        ]
                        for row in 1..3 do
                            Html.div [
                                prop.key row
                                prop.className (rowClass false)
                                prop.children [
                                    Html.div [ prop.className (nameClass false); prop.children [ Html.div [ prop.className $"w-32 h-4 {pulse}" ]; Html.div [ prop.className $"w-40 h-3 {pulse}" ] ] ]
                                    Html.div [ prop.className (averageClass false); prop.children [ Html.div [ prop.className $"w-20 h-4 {pulse}" ]; Html.div [ prop.className $"w-28 h-3 {pulse}" ] ] ]
                                    Html.div [ prop.className (barsClass false); prop.children [ Stats.sparkBarsPlaceholder () ] ]
                                    Html.div [ prop.className (actionsClass false); prop.children [ Html.div [ prop.className $"flex-1 h-10 @min-[720px]:flex-none @min-[720px]:w-32 {pulse}" ] ] ]
                                ]
                            ]
                    ]
                ]
        ]
    ]

/// Satz, große Balken mit Legende (nur im Panel) und das gemeinsame Target-Formular mit Abbrechen am Ende.
let private panelBody (model: Model) (row: TargetCheckRow) (form: TargetForm) (isPanel: bool) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "target-check-form")
        prop.className "flex flex-col gap-4"
        prop.children [
            Html.p [ prop.custom ("data-testid", "target-check-sentence"); prop.className $"text-sm leading-relaxed {Colors.textPrimary}"; prop.text (checkPanelSentence row) ]
            if isPanel then
                let line, bars = checkSparkBars row
                Stats.sparkBarsLabelled (checkBarsLabel row) line bars
                Html.div [
                    prop.ariaHidden true
                    prop.className $"flex flex-wrap gap-x-4 gap-y-1 {ListRoles.muted}"
                    prop.children [
                        Html.span "Balken: Ausgaben je Monat"
                        Html.span [ prop.className Colors.neonOrange; prop.text "orange: über dem Target" ]
                        Html.span "Linie: Target"
                    ]
                ]
            TargetFormView.view
                { Shown = row.Target
                  Form = form
                  Locks = panelLocks model form
                  IsPanel = isPanel
                  Status = Html.none
                  Pause = Html.none
                  AmountNote = checkSuggestionHint row
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

/// Das Panel ab 1024 px: Name und "Schließen" oben, darunter Satz, große Balken und Formular.
let private sidePanel (model: Model) (dispatch: Msg -> unit) =
    match panelRow model with
    | Some (row, form) ->
        Html.aside [
            prop.custom ("data-testid", "target-check-panel")
            prop.className "target-check-panel flex flex-col gap-3.5 rounded-2xl border border-border-default bg-surface-elevated p-4 overflow-y-auto"
            prop.children [
                Html.div [
                    prop.className "flex items-baseline justify-between gap-3"
                    prop.children [
                        Html.h2 [ prop.className $"text-lg {FontWeights.semibold} {Colors.textPrimary} truncate"; prop.title row.Name; prop.text row.Name ]
                        Html.div [
                            prop.custom ("data-testid", "target-check-close")
                            prop.children [
                                Button.view
                                    { Button.defaultProps with
                                        Text = "Schließen"
                                        Variant = Button.Ghost
                                        Size = Button.Small
                                        AriaLabel = Some "Panel schließen"
                                        OnClick = fun () -> dispatch CloseCheckPanel }
                            ]
                        ]
                    ]
                ]
                panelBody model row form true dispatch
            ]
        ]
    | None -> Html.none

/// Das Sheet unter 1024 px. Immer gerendert, damit es ein- und ausfährt.
let private sheet (model: Model) (dispatch: Msg -> unit) =
    let props title isOpen : BottomSheet.BottomSheetProps =
        { IsOpen = isOpen
          OnClose = fun () -> dispatch CloseCheckPanel
          Title = title
          Subtitle = None
          Footer = None }
    match panelRow model with
    | Some (row, form) ->
        BottomSheet.inspector
            (props row.Name true)
            []
            []
            [ Html.div [
                  prop.custom ("data-testid", "target-check-sheet")
                  prop.className "py-4"
                  prop.children [ panelBody model row form false dispatch ]
              ] ]
    | None -> BottomSheet.inspector (props "" false) [] [] []

let private backButton (dispatch: Msg -> unit) =
    Button.view
        { Button.defaultProps with
            Text = "← Budget"
            Variant = Button.Ghost
            Size = Button.Small
            AriaLabel = Some "Zurück zum Budget"
            ClassName = Some "-ml-3"
            OnClick = fun () -> dispatch LeaveTargetCheck }

let private counter (hints: bool) (count: int) =
    let testId, label = if hints then "target-check-hints", "Hinweise" else "target-check-fits", "Passt"
    let toneClass = if hints then Colors.neonOrange else Colors.neonTeal
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className "flex flex-col"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text label ]
            Html.span [ prop.className $"font-mono text-2xl {FontWeights.semibold} tabular-nums {toneClass}"; prop.text (string count) ]
        ]
    ]

/// Der Kopf: am PC eine Karte mit Zurück, Titel, Erklärung und den Zählern rechts, am Handy Zurück oben, darunter
/// der Titel groß und eine Zeile mit Monat und Zählern.
let private header (data: (TargetCheckData * CheckCounts) option) (dispatch: Msg -> unit) =
    React.fragment [
        Html.div [
            prop.custom ("data-testid", "target-check-head")
            prop.className $"hidden md:flex flex-wrap items-center justify-between gap-3 {cardClass} px-4 py-3"
            prop.children [
                Html.div [
                    prop.className "flex min-w-0 flex-col gap-1"
                    prop.children [
                        Html.div [
                            prop.className "flex items-baseline gap-3"
                            prop.children [
                                backButton dispatch
                                Html.h1 [ prop.className $"text-xl {FontWeights.semibold} {Colors.textPrimary}"; prop.text "Targets prüfen" ]
                            ]
                        ]
                        match data with
                        | Some (d, _) -> Html.p [ prop.custom ("data-testid", "target-check-intro"); prop.className ListRoles.muted; prop.text (checkIntroText d.Month) ]
                        | None -> ()
                    ]
                ]
                match data with
                | Some (_, counts) ->
                    Html.div [
                        prop.className "flex gap-4"
                        prop.children [
                            counter true counts.Hints
                            counter false counts.Fits
                        ]
                    ]
                | None -> ()
            ]
        ]
        Html.div [
            prop.custom ("data-testid", "target-check-head-narrow")
            prop.className "flex flex-col gap-0.5 md:hidden"
            prop.children [
                Html.div [ prop.className "self-start"; prop.children [ backButton dispatch ] ]
                Html.h1 [ prop.className $"text-2xl {FontWeights.semibold} {Colors.textPrimary}"; prop.text "Targets prüfen" ]
                match data with
                | Some (d, counts) ->
                    let hints, fits = checkCountsText counts
                    Html.p [
                        prop.custom ("data-testid", "target-check-intro-narrow")
                        prop.className ListRoles.muted
                        prop.children [
                            Html.text $"{monthLabel d.Month} · "
                            Html.span [ prop.className Colors.neonOrange; prop.text hints ]
                            Html.text " · "
                            Html.span [ prop.className Colors.neonTeal; prop.text fits ]
                        ]
                    ]
                | None -> ()
            ]
        ]
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    let isPanel = Viewport.isInspectorPanelViewport ()
    let check = model.TargetCheck
    let loaded =
        match check.Data with
        | Success data -> Some (data, checkGroups data)
        | _ -> None
    let content =
        match check.Data, loaded with
        | Failure message, _ -> ErrorDisplay.card message (Some (fun () -> dispatch RetryTargetCheck))
        | _, Some (data, groups) -> groupsView model data groups dispatch
        | _ -> skeleton ()
    // Die Klasse `budget-page` setzt die Maße unter der Navbar, an denen das Panel haftet.
    Primitives.Container.full [
        Html.div [
            prop.custom ("data-testid", "target-check-page")
            prop.className "budget-page flex flex-col gap-3 py-2"
            prop.children [
                header (loaded |> Option.map (fun (data, groups) -> data, checkCounts groups)) dispatch
                // ADR 0043: zur Renderzeit genau eine Hülle. Ohne "Anpassen …" ist das Panel zu.
                if isPanel then
                    match panelRow model with
                    | Some _ ->
                        Html.div [
                            prop.className "grid grid-cols-[minmax(0,1fr)_360px] gap-3 items-start"
                            prop.children [ content; sidePanel model dispatch ]
                        ]
                    | None -> content
                else
                    content
                    sheet model dispatch
            ]
        ]
    ]
