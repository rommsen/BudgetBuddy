/// Die allgemeine Budget-Historie `#/budget/history` (budgeting-h7q4v): alle Durchläufe, Pausen und
/// Target-Änderungen (budgeting-r5h2n), neueste zuerst, eine Zeile je Durchlauf, Pause oder Änderung nach §7.11
/// (`HistoryList`, budgeting-yarty). Oben das Feld „Zeigen“ mit der Auswahl „Historie zeigen“. Ein Durchlauf Targets
/// anwenden klappt zu seinen Kategorien auf. Lesend, keine YNAB-Anfrage. Eine ruhige Liste (ADR 0049): Beträge
/// neutral, das Stand-Badge ist der einzige Farbakzent. Ausschließlich Design-System-Bausteine.
module Components.Budget.HistoryView

open Feliz
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Inspector
open Components.Budget.History
open Client.DesignSystem
open Client.DesignSystem.Tokens
open Components.Budget.HistoryRowsView

/// Was die Seite über §7.11 hinaus zeigt, als Zusatz unter dem Text (`HistoryList.rowWithNote`): der Monat, am
/// Handy kurz („Nov. 2026“), und der Stand mit dem Grund eines Fehlschlags.
let private noteParts (note: RowNote) : ReactElement list =
    [ Html.span [
          prop.custom ("data-testid", "general-history-month")
          prop.className "shrink-0 whitespace-nowrap"
          prop.children [
              Html.span [ prop.className "md:hidden"; prop.text note.ShortMonth ]
              Html.span [ prop.className "hidden md:inline"; prop.text note.Month ]
          ]
      ]
      match note.Badge with
      | StatusBadge status ->
          Html.span [ prop.className "shrink-0"; prop.children [ historyStatusView status ] ]
          historyFailureWrapped status
      | PartlyFailedBadge (short, long) ->
          Html.span [
              prop.custom ("data-testid", "general-history-partly-failed")
              prop.className "shrink-0"
              prop.children [
                  Html.span [ prop.className "md:hidden"; prop.children [ Badge.errorSmall short ] ]
                  Html.span [ prop.className "hidden md:inline"; prop.children [ Badge.errorSmall long ] ]
              ]
          ]
      | NoBadge -> () ]

/// Eine Kategorie eines aufgeklappten Durchlaufs: Name, Betrag mit Vorzeichen, Stand mit Grund.
let private runLineView (alignWith: string) (line: HistoryRunLine) =
    HistoryList.subRow
        (string line.MovementId)
        alignWith
        line.CategoryName
        (fst (historyChangeAmount line.Change))
        [ Html.span [ prop.className "shrink-0"; prop.children [ historyStatusView line.Status ] ]
          historyFailureWrapped line.Status ]

/// Eine Zeile nach §7.11 (`HistoryList`). Ein Durchlauf Targets anwenden klappt zu seinen Kategorien auf.
let private rowView (history: GeneralHistory) (dispatch: Msg -> unit) (row: GeneralHistoryRow) =
    let item = rowItem row
    let note = noteParts (rowNote row)
    match row with
    | RunRow run when expandable run ->
        HistoryList.disclosureRow
            item
            note
            $"general-history-lines-{run.RunId}"
            (isRunOpen history run)
            (fun () -> dispatch (ToggleRun run.RunId))
            (run.Lines |> List.map (runLineView item.When))
    | RunRow _
    | TargetRow _ -> HistoryList.rowWithNote item note

[<Literal>]
let private FilterFieldSelector = "[data-testid=\"general-history-filter-field\"]"

/// Die Arten der Auswahl „Historie zeigen“ in einer Karte, die gewählte mit Haken (Prototyp Sheet `histf`).
let private filterOptions (history: GeneralHistory) (inSheet: bool) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "rounded-2xl border border-border-subtle bg-surface-card overflow-clip"
        prop.children [
            PickerParts.ShortChoiceFor "general-history-filter-options" (Some FilterFieldSelector) [
                for filter in runFilters ->
                    React.keyedFragment (
                        runFilterLabel filter,
                        [ PickerParts.shortChoiceRow
                              (runFilterLabel filter)
                              None
                              (history.Filter = filter)
                              (fun () ->
                                  // Im Sheet steht danach die Liste an derselben Stelle: ein später synthetischer
                                  // Klick soll dort keinen Durchlauf aufklappen (ADR 0005).
                                  if inSheet then Viewport.swallowNextClick 350
                                  dispatch (SelectRunFilter filter)) ]
                    )
            ]
        ]
    ]

[<Literal>]
let private FilterChoiceTitle = "Historie zeigen"

/// Das Feld „Zeigen“ mit der gewählten Art (budgeting-yarty, ersetzt die Chipleiste). Es öffnet die kurze Auswahl
/// „Historie zeigen“: am Handy im Sheet, ab 768 px als Dialog (§7.6, eine Auswahl ist ein Dialog). Gerendert wird
/// genau eine Hülle (`Viewport.isDesktopViewport`), ein Sheet portalt sonst trotz verstecktem Rahmen.
let private filterField (history: GeneralHistory) (dispatch: Msg -> unit) =
    let close () = dispatch CloseRunFilterChoice
    React.fragment [
        Input.selectField
            "general-history-filter-field"
            "Zeigen"
            (runFilterLabel history.Filter)
            None
            history.FilterChoiceOpen
            (fun () -> dispatch OpenRunFilterChoice)
        if Viewport.isDesktopViewport () then
            if history.FilterChoiceOpen then
                Modal.view
                    { Modal.defaultProps with
                        IsOpen = true
                        OnClose = close
                        Size = Modal.Large
                        Title = Some FilterChoiceTitle }
                    [ Modal.body [ filterOptions history false dispatch ] ]
        else
            // Die Auswahl steht nur offen im DOM (sonst setzte `ShortChoiceFor` beim Laden der Seite den Fokus), beim
            // Ausfahren hält `viewHeld` sie noch.
            BottomSheet.viewHeld
                { IsOpen = history.FilterChoiceOpen
                  OnClose = close
                  Title = FilterChoiceTitle
                  Subtitle = None
                  Footer = None }
                [ if history.FilterChoiceOpen then filterOptions history true dispatch ]
    ]

let private rowList (history: GeneralHistory) (rows: GeneralHistoryRow list) (dispatch: Msg -> unit) =
    let shown = shownRows history.Filter rows
    Html.div [
        prop.custom ("data-testid", "general-history-list")
        prop.className "rounded-xl border border-border-subtle bg-surface-card px-3 py-1"
        prop.children [
            if List.isEmpty shown then
                Html.p [
                    prop.custom ("data-testid", "general-history-empty")
                    prop.className $"{ListRoles.muted} px-1 py-8 text-center"
                    prop.text (emptyText history.Filter)
                ]
            else
                HistoryList.listOf "general-history-rows" (shown |> List.map (rowView history dispatch))
        ]
    ]

let view (history: GeneralHistory) (dispatch: Msg -> unit) =
    Primitives.Container.full [
        Html.div [
            prop.custom ("data-testid", "general-history-page")
            prop.className "flex flex-col gap-4 py-2"
            prop.children [
                PageHeader.screenReaderTitle "Budget-Historie"
                Html.div [
                    prop.className "flex items-center gap-3"
                    prop.children [
                        PageHeader.backButtonFor (Some "Zurück zum Budget") (fun () -> dispatch LeaveGeneralHistory)
                        Html.span [ prop.className ListRoles.primary; prop.text "Historie" ]
                    ]
                ]
                filterField history dispatch
                match history.Rows with
                | NotAsked
                | Loading -> Loading.txListSkeleton 5
                | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch RetryGeneralHistory))
                | Success rows -> rowList history rows dispatch
            ]
        ]
    ]
