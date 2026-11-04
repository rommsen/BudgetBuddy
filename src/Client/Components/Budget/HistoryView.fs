/// Die allgemeine Budget-Historie `#/budget/history` (budgeting-h7q4v): alle Durchläufe, neueste zuerst,
/// eine Zeile je Durchlauf. Ein Durchlauf Targets anwenden klappt zu seinen Kategorien auf. Lesend, keine
/// YNAB-Anfrage. Eine ruhige Liste (ADR 0049): Beträge neutral, das Stand-Badge ist der einzige
/// Farbakzent. Ausschließlich Design-System-Bausteine.
module Components.Budget.HistoryView

open Feliz
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.History
open Client.DesignSystem
open Client.DesignSystem.Tokens
open Components.Budget.HistoryRowsView

/// `last:border-b-0`: die letzte Zeile trägt keine Linie, die Karte hat schon ihren Rand.
let private rowClass = $"{ListRoles.mobileRowPadding} border-b border-border-subtle last:border-b-0"

/// Zeitpunkt, Monat und Stand eines Durchlaufs. Der Monat steht am Handy kurz ("Nov. 2026").
let private runMeta (run: HistoryRun) =
    Html.span [
        prop.className ListRoles.mobileLine2
        prop.children [
            Html.span [
                prop.custom ("data-testid", "general-history-time")
                prop.className $"{ListRoles.date} shrink-0 whitespace-nowrap"
                prop.text (historyTimeText run.OccurredAt)
            ]
            Html.span [
                prop.custom ("data-testid", "general-history-month")
                prop.className $"{ListRoles.secondary} shrink-0 whitespace-nowrap"
                prop.children [
                    Html.span [ prop.className "md:hidden"; prop.text (shortMonthYearLabel run.Month) ]
                    Html.span [ prop.className "hidden md:inline"; prop.text (monthLabel run.Month) ]
                ]
            ]
            match runBadge run.Status with
            | StatusBadge status ->
                Html.span [ prop.className "shrink-0"; prop.children [ historyStatusView status ] ]
                historyFailureView status
            | PartlyFailedBadge (short, long) ->
                Html.span [
                    prop.custom ("data-testid", "general-history-partly-failed")
                    prop.className "shrink-0"
                    prop.children [
                        Html.span [ prop.className "md:hidden"; prop.children [ Badge.errorSmall short ] ]
                        Html.span [ prop.className "hidden md:inline"; prop.children [ Badge.errorSmall long ] ]
                    ]
                ]
        ]
    ]

/// Art, Kategorie oder Richtung und Anzahl links, der Betrag rechts in Mono (budgeting-c6vn4, A5).
let private runHead (run: HistoryRun) =
    let amountText, amountClass = runAmount run
    Html.span [
        prop.className ListRoles.mobileLine1
        prop.children [
            Html.span [
                prop.custom ("data-testid", "general-history-text")
                prop.className $"{ListRoles.primary} truncate min-w-0"
                prop.text run.Text
            ]
            Html.span [
                prop.custom ("data-testid", "general-history-amount")
                prop.className $"{amountClass} shrink-0 whitespace-nowrap"
                prop.text amountText
            ]
        ]
    ]

/// Eine Kategorie eines aufgeklappten Durchlaufs: Name, Betrag mit Vorzeichen, Stand. Der Grund eines
/// Fehlschlags bricht auf bis zu zwei Zeilen um.
let private runLineView (line: HistoryRunLine) =
    Html.div [
        prop.key (string line.MovementId)
        prop.custom ("data-testid", "general-history-line")
        prop.className $"{rowClass} bg-surface-elevated"
        prop.children [
            Html.div [
                prop.className ListRoles.mobileLine1
                prop.children [
                    Html.span [ prop.className $"{ListRoles.secondary} truncate min-w-0"; prop.text line.CategoryName ]
                    historyChangeView line.Change
                ]
            ]
            Html.div [
                prop.className ListRoles.mobileLine2
                prop.children [ Html.span [ prop.className "shrink-0"; prop.children [ historyStatusView line.Status ] ] ]
            ]
            historyFailureWrapped line.Status
        ]
    ]

let private runView (history: GeneralHistory) (dispatch: Msg -> unit) (run: HistoryRun) =
    if expandable run then
        let isOpen = isRunOpen history run
        let linesId = $"general-history-lines-{run.RunId}"
        Html.div [
            prop.key (string run.RunId)
            prop.custom ("data-testid", "general-history-run")
            prop.className "border-b border-border-subtle last:border-b-0"
            prop.children [
                Button.disclosureListRow
                    (string run.RunId)
                    "general-history-toggle"
                    linesId
                    isOpen
                    (fun () -> dispatch (ToggleRun run.RunId))
                    [ runHead run; runMeta run ]
                Html.div [
                    prop.id linesId
                    prop.hidden (not isOpen)
                    prop.children (run.Lines |> List.map runLineView)
                ]
            ]
        ]
    else
        Html.div [
            prop.key (string run.RunId)
            prop.custom ("data-testid", "general-history-run")
            prop.className rowClass
            prop.children [ runHead run; runMeta run ]
        ]

/// Die Filter nach Art: eine haftende Leiste in eigener Zeile, am Handy waagerecht scrollbar
/// (Styleguide 7.3, budgeting-c6vn4 A3).
let private filterChips (history: GeneralHistory) (dispatch: Msg -> unit) =
    ChipBar.barWithTestId "general-history-filter" [
        for filter in runFilters do
            Html.div [
                prop.key (runFilterLabel filter)
                prop.className "shrink-0"
                prop.children [
                    ChipBar.chip
                        { Label = runFilterLabel filter
                          IsActive = (history.Filter = filter)
                          OnClick = fun () -> dispatch (SelectRunFilter filter)
                          OnRemove = None }
                ]
            ]
    ]

let private runList (history: GeneralHistory) (runs: HistoryRun list) (dispatch: Msg -> unit) =
    let shown = shownRuns history.Filter runs
    Html.div [
        prop.className "flex flex-col gap-2"
        prop.children [
            Html.div [
                prop.custom ("data-testid", "general-history-list")
                prop.className "rounded-xl border border-border-subtle bg-surface-card overflow-clip"
                prop.children [
                    if List.isEmpty shown then
                        Html.p [
                            prop.custom ("data-testid", "general-history-empty")
                            prop.className $"{ListRoles.muted} px-4 py-8 text-center"
                            prop.text (emptyText history.Filter)
                        ]
                    else
                        yield! shown |> List.map (runView history dispatch)
                ]
            ]
            ynabAssignedNote ()
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
                        Button.secondary "Zurück" (fun () -> dispatch LeaveGeneralHistory)
                        Html.span [ prop.className ListRoles.primary; prop.text "Historie" ]
                    ]
                ]
                filterChips history dispatch
                match history.Runs with
                | NotAsked
                | Loading -> Loading.txListSkeleton 5
                | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch RetryGeneralHistory))
                | Success runs -> runList history runs dispatch
            ]
        ]
    ]
