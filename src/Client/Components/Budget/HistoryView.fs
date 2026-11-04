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
open Components.Budget.InspectorView

let private rowClass = $"{ListRoles.mobileRowPadding} border-b border-border-subtle"

/// Zeitpunkt, Monat und Stand eines Durchlaufs.
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
                prop.text (monthLabel run.Month)
            ]
            Html.span [ prop.className "shrink-0"; prop.children [ historyStatusView run.Status ] ]
            historyFailureView run.Status
        ]
    ]

let private runHead (run: HistoryRun) =
    Html.span [
        prop.className ListRoles.mobileLine1
        prop.children [
            Html.span [
                prop.custom ("data-testid", "general-history-text")
                prop.className $"{ListRoles.primary} truncate min-w-0"
                prop.text run.Text
            ]
        ]
    ]

/// Eine Kategorie eines aufgeklappten Durchlaufs: Name, Betrag mit Vorzeichen, Stand.
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
                prop.children [
                    Html.span [ prop.className "shrink-0"; prop.children [ historyStatusView line.Status ] ]
                    historyFailureView line.Status
                ]
            ]
        ]
    ]

let private runView (history: GeneralHistory) (dispatch: Msg -> unit) (run: HistoryRun) =
    if expandable run then
        let isOpen = history.Expanded.Contains run.RunId
        let action = if isOpen then "zuklappen" else "aufklappen"
        Html.div [
            prop.key (string run.RunId)
            prop.custom ("data-testid", "general-history-run")
            prop.children [
                Button.listRow
                    (string run.RunId)
                    "general-history-toggle"
                    $"{run.Text}, {action}"
                    (fun () -> dispatch (ToggleRun run.RunId))
                    [ runHead run; runMeta run ]
                if isOpen then
                    Html.div [ prop.children (run.Lines |> List.map runLineView) ]
            ]
        ]
    else
        Html.div [
            prop.key (string run.RunId)
            prop.custom ("data-testid", "general-history-run")
            prop.className rowClass
            prop.children [ runHead run; runMeta run ]
        ]

let private filterChips (history: GeneralHistory) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "general-history-filter")
        prop.className "flex flex-wrap items-center gap-2"
        prop.children (
            runFilters
            |> List.map (fun filter ->
                ChipBar.chip
                    { Label = runFilterLabel filter
                      IsActive = (history.Filter = filter)
                      OnClick = fun () -> dispatch (SelectRunFilter filter)
                      OnRemove = None })
        )
    ]

let private emptyText (filter: RunFilter) =
    match filter with
    | AllRuns -> "Noch keine Bewegungen festgehalten"
    | RunsOf _ -> "Keine Durchläufe dieser Art"

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
                    prop.className "flex flex-wrap items-center gap-3"
                    prop.children [
                        Button.secondary "Zurück" (fun () -> dispatch LeaveGeneralHistory)
                        Html.span [ prop.className ListRoles.primary; prop.text "Historie" ]
                        filterChips history dispatch
                    ]
                ]
                match history.Runs with
                | NotAsked
                | Loading -> Loading.txListSkeleton 5
                | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch GeneralHistoryEntered))
                | Success runs -> runList history runs dispatch
            ]
        ]
    ]
