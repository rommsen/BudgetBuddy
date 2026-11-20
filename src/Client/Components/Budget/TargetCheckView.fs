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
    /// Die Rückmeldung der letzten Antwort, wenn sie in dieser Zeile steht (`NoteInRow`).
    Note: CheckRowNote option
    Flash: AvailableFlash
}

/// Die Rückmeldung als Zeile (Baustein der Zeile im Sheet, Skizze `.mk-msg`): ✓ grün, ! gelb, ! rot.
let private noteLine (testId: string) (tone: SheetLineTone) (text: string) =
    let feedbackTone =
        match tone with
        | LineOk -> Feedback.Tone.Ok
        | LineWarning -> Feedback.Tone.Warning
        | LineError -> Feedback.Tone.Error
    Html.div [
        prop.custom ("data-testid", testId)
        prop.children [ Feedback.sheetLine { Tone = feedbackTone; Action = text; Detail = None; Act = None } ]
    ]

let private rowViewOf (model: Model) (data: TargetCheckData) (selected: System.Guid option) (inRow: CheckRowNote option) (row: TargetCheckRow) : RowView =
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
      Selected = selected = Some row.CategoryId
      Note = inRow |> Option.filter (fun n -> n.CategoryId = row.CategoryId)
      Flash = model.TargetCheck.Flash }

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
    $"col-span-2 row-start-3 {ListRoles.secondary} @min-[720px]:col-span-1 @min-[720px]:col-start-1 @min-[720px]:row-start-2 @min-[720px]:self-start"

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
    // Nach einem geschriebenen Target leuchtet die Zeile kurz (`Feedback.flash`); der Schlüssel baut sie je Leuchten
    // neu, so beginnt die Animation auch beim zweiten Mal von vorn.
    let lit = view.Note |> Option.exists (fun n -> n.Tone = LineOk) && view.Flash.On
    Html.div [
        prop.key (if view.Note.IsSome then $"{row.CategoryId}-{view.Flash.Count}" else string row.CategoryId)
        prop.custom ("data-testid", "target-check-row")
        prop.custom ("data-flash", lit)
        prop.className (rowClass view.Selected + " " + (if lit then Feedback.flash else ""))
        prop.children [
            Html.div [
                prop.className (nameClass hasNote)
                prop.children [
                    Html.span [ prop.custom ("data-testid", "target-check-name"); yield! Truncate.textProps ListRoles.primary row.Name ]
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
            match view.Note with
            | Some note ->
                Html.div [
                    prop.className "col-span-full"
                    prop.children [ noteLine "target-check-row-line" note.Tone note.Text ]
                ]
            | None -> ()
        ]
    ]

/// Die schmale Zeile am bisherigen Platz, wenn die Kategorie ihre Gruppe verlassen hat (Muster „Gelöschtes als Zeile
/// am Platz“, design-system-ytgnc): „Essen auswärts: Target jetzt 175 € monatlich“.
let private movedNoteRow (bordered: bool) (note: CheckRowNote) =
    Html.div [
        prop.key $"moved-{note.CategoryId}"
        prop.className ("px-3 py-1 @min-[720px]:px-4" + (if bordered then " border-t border-border-subtle" else ""))
        prop.children [ noteLine "target-check-moved-line" note.Tone (checkNoteMovedText note) ]
    ]

/// Die Farbe des Titels einer Gruppe nach Farbrolle (ADR 0087): mehr Gelb (braucht dich), weniger
/// Blau (Hinweis), nie und Kein Target neutral, Passt Grün. Steht hier, damit der Klassen-Wächter
/// die Klassen in derselben Datei findet.
let checkGroupTitleClass (group: CheckGroup) : string =
    match group with
    | SpendsMore -> Colors.attention
    | SpendsLess -> Colors.info
    | NeverSpends -> Colors.textPrimary
    | WithoutTarget -> Colors.textPrimary
    | Fits -> Colors.success

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
let private groupCard
    (model: Model)
    (data: TargetCheckData)
    (selected: System.Guid option)
    (place: CheckNotePlace)
    (dispatch: Msg -> unit)
    (group: CheckGroup)
    (rows: TargetCheckRow list)
    =
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
                prop.children (
                    if isOpen then
                        let inRow = if place = NoteInRow then model.TargetCheck.Note else None
                        let shown = rows |> List.map (rowViewOf model data selected inRow >> rowView dispatch)
                        match place, model.TargetCheck.Note with
                        | NoteAt (g, index), Some note when g = group ->
                            List.take index shown @ [ movedNoteRow true note ] @ List.skip index shown
                        | _ -> shown
                    else
                        []
                )
            ]
        ]
    ]

let private groupsView (model: Model) (data: TargetCheckData) (groups: (CheckGroup * TargetCheckRow list) list) (dispatch: Msg -> unit) =
    // Einmal vor der Schleife: die Zeile mit offenem Panel (CLAUDE.md, große Listen).
    let selected = panelCategory model
    let place = model.TargetCheck.Note |> Option.map (checkNotePlace groups) |> Option.defaultValue NoteHidden
    // Fiel die bisherige Gruppe ganz weg, steht die schmale Zeile als eigene Karte an ihrem Platz.
    let standalone (before: CheckGroup option) =
        match place, model.TargetCheck.Note with
        | NoteBefore b, Some note when b = before ->
            Html.div [ prop.key "moved-note"; prop.className cardClass; prop.children [ movedNoteRow false note ] ]
        | _ -> Html.none
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
                standalone (Some group)
                groupCard model data selected place dispatch group rows
            standalone None
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

/// Das gemeinsame Target-Formular im Panel oder Sheet, angebunden an die Nachrichten der Seite.
let private formProps (model: Model) (row: TargetCheckRow) (form: TargetForm) (isPanel: bool) (dispatch: Msg -> unit) : TargetFormView.TargetFormProps =
    { TargetFormView.TargetFormProps.Shown = row.Target
      Form = form
      Locks = panelLocks model form
      IsPanel = isPanel
      Status = Html.none
      Pause = Html.none
      AmountNote = checkSuggestionHint row
      AmountFlash = None
      OnAmountChanged = CheckAmountChanged >> dispatch
      OnDateChanged = CheckDateChanged >> dispatch
      OnRhythmChanged = CheckRhythmChanged >> dispatch
      OnWeekdayChanged = CheckWeekdayChanged >> dispatch
      OnOpenWeekdayChoice = fun () -> dispatch OpenCheckWeekdayChoice
      OnCloseWeekdayChoice = fun () -> dispatch CloseCheckWeekdayChoice
      OnSave = fun () -> dispatch SaveCheckTarget
      OnConfirmChange = fun () -> dispatch ConfirmCheckTargetChange
      OnCancelChange = fun () -> dispatch CancelCheckTargetChange
      OnAskRemove = fun () -> dispatch AskRemoveCheckTarget
      OnConfirmRemove = fun () -> dispatch ConfirmRemoveCheckTarget
      OnCancelRemove = fun () -> dispatch CancelRemoveCheckTarget }

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
                        Html.span [ prop.className Colors.attention; prop.text "gelb: über dem Target" ]
                        Html.span "Linie: Target"
                    ]
                ]
            TargetFormView.view (formProps model row form isPanel dispatch)
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
        // Die Hülle wie in Kassenbuch und Budget (design-system-gcpws, §7.6): unter dem Seitenkopf, ohne Karte.
        Detail.panelWith "target-check-panel" "Target anpassen" [
          Html.div [
            prop.className "flex min-h-0 flex-1 flex-col gap-3.5 overflow-y-auto p-4"
            prop.children [
                Html.div [
                    prop.className "flex items-baseline justify-between gap-3"
                    prop.children [
                        Html.h2 (Truncate.textProps $"text-lg {FontWeights.semibold} {Colors.textPrimary}" row.Name)
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
    let panel = panelRow model
    let weekdayChoice =
        panel |> Option.bind (fun (row, form) -> TargetFormView.weekdayChoiceInSheet (formProps model row form false dispatch))
    match panel, weekdayChoice with
    // Die Auswahl des Wochentags ersetzt den Inhalt des Sheets (§7.15, budgeting-h2brj); Wahl, „Zurück“ oder
    // Schließen führen zum Formular zurück, die Eingaben stehen im Modell und bleiben.
    | Some (row, _), Some choice ->
        BottomSheet.inspector
            { IsOpen = true
              OnClose = fun () -> dispatch CloseCheckWeekdayChoice
              Title = "Wochentag"
              Subtitle = Some row.Name
              Footer = None }
            []
            []
            [ choice ]
    | Some (row, form), None ->
        BottomSheet.inspector
            (props row.Name true)
            []
            []
            [ Html.div [
                  prop.custom ("data-testid", "target-check-sheet")
                  prop.className "py-4"
                  prop.children [ panelBody model row form false dispatch ]
              ] ]
    | None, _ -> BottomSheet.inspector (props "" false) [] [] []

/// „Zurück“ mit Pfeil (Prototyp `.back`, design-system-k6kfw).
let private backButton (dispatch: Msg -> unit) =
    PageHeader.backButtonFor (Some "Zurück zum Budget") (fun () -> dispatch LeaveTargetCheck)

/// Der Kopf wie im Prototyp (`tcheck()`, budgeting-ht88a): Zurück und Titel in einer Zeile, darunter ein Satz 13 px in
/// `text-secondary` mit Hinweisen und passenden Zeilen, am Handy und am PC gleich. Die Zahlen tragen keine eigene
/// Farbe (Roman w02 a): Gelb steht nur an „Gibst du mehr aus?“ und an den Balken über dem Target.
let private header (counts: CheckCounts option) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "target-check-head")
        prop.className "flex flex-col"
        prop.children [
            PageHeader.top (Some (backButton dispatch)) "Targets prüfen" None []
            match counts with
            | Some c ->
                Html.p [
                    prop.custom ("data-testid", "target-check-intro")
                    prop.className $"{FontSizes.compact} {Colors.textSecondary}"
                    prop.text (checkHeadText c)
                ]
            | None -> ()
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
                header (loaded |> Option.map (snd >> checkCounts)) dispatch
                // Ein gescheitertes stilles Neuladen ist ein Zustand der Seite (ADR 0098): Hinweis in allen Breiten, bis
                // ein Neu-Lesen gelingt (budgeting-sx9v5, wie „Beträge nicht neu gelesen“ in budgeting-gtrgk).
                match check.Stale with
                | Some notice ->
                    let reason, running =
                        match notice with
                        | ReadFailed reason -> reason, false
                        | Rereading reason -> reason, true
                    Feedback.stateHint
                        "target-check-stale"
                        CheckStaleText
                        [ reason ]
                        (Some { Label = "Neu laden"; OnClick = fun () -> dispatch RereadTargetCheck })
                        running
                | None -> ()
                // ADR 0043: zur Renderzeit genau eine Hülle. Ohne "Anpassen …" ist das Panel zu.
                // Esc schließt am PC erst eine offene Rückfrage, dann „Anpassen …“ (F5, design-system-gcpws).
                if isPanel then
                    PageKeys.OnDocument (fun e ->
                        match targetCheckKeyMsg model (PageKeys.focusOf None e.target) (e.altKey || e.ctrlKey || e.metaKey) e.shiftKey e.defaultPrevented e.key with
                        | Some msg ->
                            e.preventDefault ()
                            dispatch msg
                        | None -> ())
                if isPanel then
                    match panelRow model with
                    | Some _ ->
                        Html.div [
                            prop.className "relative"
                            prop.children [
                                sidePanel model dispatch
                                Html.div [
                                    prop.className $"{PanelWidths.columnInsetWide} min-h-[calc(100vh-7rem)]"
                                    prop.children [ content ]
                                ]
                            ]
                        ]
                    | None -> content
                else
                    content
                    sheet model dispatch
            ]
        ]
    ]
