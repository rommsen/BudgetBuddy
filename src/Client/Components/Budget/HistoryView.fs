/// Die allgemeine Budget-Historie `#/budget/history` (budgeting-h7q4v): alle Durchläufe, Pausen und
/// Target-Änderungen (budgeting-r5h2n), neueste zuerst, eine Zeile je Durchlauf, Pause oder Änderung. Ein
/// Durchlauf Targets anwenden klappt zu seinen Kategorien auf. Lesend, keine YNAB-Anfrage. Eine ruhige Liste (ADR 0049): Beträge neutral, das Stand-Badge ist der einzige
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

/// Zeitpunkt, Monat und Stand einer Zeile. Der Monat steht am Handy kurz ("Nov. 2026").
let private rowMeta (occurredAt: System.DateTime) (month: string) (status: ReactElement list) =
    Html.span [
        prop.className ListRoles.mobileLine2
        prop.children [
            Html.span [
                prop.custom ("data-testid", "general-history-time")
                prop.className $"{ListRoles.date} shrink-0 whitespace-nowrap"
                prop.text (historyTimeText occurredAt)
            ]
            Html.span [
                prop.custom ("data-testid", "general-history-month")
                prop.className $"{ListRoles.secondary} shrink-0 whitespace-nowrap"
                prop.children [
                    Html.span [ prop.className "md:hidden"; prop.text (shortMonthYearLabel month) ]
                    Html.span [ prop.className "hidden md:inline"; prop.text (monthLabel month) ]
                ]
            ]
            yield! status
        ]
    ]

/// Das Stand-Badge wie bei einer Bewegung, daneben der Grund eines Fehlschlags.
let private statusParts (status: HistoryStatus) : ReactElement list =
    [ Html.span [ prop.className "shrink-0"; prop.children [ historyStatusView status ] ]
      historyFailureView status ]

/// Zeitpunkt, Monat und Stand eines Durchlaufs.
let private runMeta (run: HistoryRun) =
    rowMeta run.OccurredAt run.Month [
        match runBadge run.Status with
        | StatusBadge status -> yield! statusParts status
        | NoBadge -> ()
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

/// Art, Kategorie oder Richtung und Anzahl links, der Betrag rechts in Mono (budgeting-c6vn4, A5). Am Handy
/// bricht der Text auf höchstens zwei Zeilen um, statt Ziel oder Anzahl abzuschneiden (budgeting-a3k7m); der
/// Betrag steht dann neben der ersten Zeile. Am PC bleibt es eine Zeile mit `truncate`.
let private runHead (run: HistoryRun) =
    let amountText, amountClass = runAmount run
    Html.span [
        prop.className "flex justify-between gap-3 max-md:items-start max-md:min-h-5 md:items-center md:h-5"
        prop.children [
            Html.span [
                prop.custom ("data-testid", "general-history-text")
                prop.className $"{ListRoles.primary} min-w-0 max-md:line-clamp-2 md:truncate"
                prop.title run.Text
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
                    Truncate.text ListRoles.secondary line.CategoryName
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

/// Eine Pause oder Target-Änderung (budgeting-r5h2n): die Bausteine der Durchlauf-Zeile, ohne Betrag, ohne
/// Chevron, nicht aufklappbar. Am Handy bricht der Text ohne Grenze um, damit die Kategorie am Ende einer langen
/// Änderung ganz dasteht; am PC höchstens zwei Zeilen (`line-clamp-2`), damit der Text ohne Hover lesbar ist, der
/// ganze Text steht zusätzlich im `title`. Ohne Stand kein Badge.
let private targetRowView (row: HistoryTargetRow) =
    Html.div [
        prop.key (string row.EntryId)
        prop.custom ("data-testid", "general-history-target")
        prop.className rowClass
        prop.children [
            Html.span [
                prop.className "flex min-h-5 md:items-center"
                prop.children [
                    Html.span [
                        prop.custom ("data-testid", "general-history-text")
                        prop.className $"{ListRoles.primary} min-w-0 md:line-clamp-2"
                        prop.title row.Text
                        prop.text row.Text
                    ]
                ]
            ]
            rowMeta row.OccurredAt row.Month (row.Status |> Option.map statusParts |> Option.defaultValue [])
        ]
    ]

let private rowView (history: GeneralHistory) (dispatch: Msg -> unit) (row: GeneralHistoryRow) =
    match row with
    | RunRow run -> runView history dispatch run
    | TargetRow target -> targetRowView target

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

let private rowList (history: GeneralHistory) (rows: GeneralHistoryRow list) (dispatch: Msg -> unit) =
    let shown = shownRows history.Filter rows
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
                        yield! shown |> List.map (rowView history dispatch)
                ]
            ]
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
                match history.Rows with
                | NotAsked
                | Loading -> Loading.txListSkeleton 5
                | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch RetryGeneralHistory))
                | Success rows -> rowList history rows dispatch
            ]
        ]
    ]
