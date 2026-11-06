/// Die allgemeine Budget-Historie `#/budget/history` (budgeting-h7q4v): reine Übergänge. Die Durchläufe
/// bildet `historyRuns` in `Shared.Budgeting`, hier liegen Filter, Aufklappen und die Rückkehr.
module Components.Budget.History

open Shared.Budgeting
open Types
open Client.DesignSystem
open Components.Budget.Types
open Components.Budget.Inspector

/// Die Filter nach Art in ihrer Reihenfolge.
let runFilters : RunFilter list =
    [ AllRuns; RunsOf MovementOrigin.TargetsApplied; RunsOf MovementOrigin.MoneyMoved; RunsOf MovementOrigin.MoneyAssigned ]

let runFilterLabel (filter: RunFilter) : string =
    match filter with
    | AllRuns -> "Alle"
    | RunsOf MovementOrigin.TargetsApplied -> "Targets anwenden"
    | RunsOf MovementOrigin.MoneyMoved -> "Verschieben"
    | RunsOf MovementOrigin.MoneyAssigned -> "Zuweisen"
    | RunsOf MovementOrigin.ObservedInYnab -> "In YNAB zugewiesen"
    | RunsOf MovementOrigin.TakenOverFromYnab -> "Übernahme"

/// Die Durchläufe, die der Filter zeigt.
let shownRuns (filter: RunFilter) (runs: HistoryRun list) : HistoryRun list =
    match filter with
    | AllRuns -> runs
    | RunsOf origin -> runs |> List.filter (fun r -> r.Origin = origin)

/// Nur ein Durchlauf Targets anwenden klappt auf: Verschieben und Zuweisen stehen ganz in ihrer Zeile.
let expandable (run: HistoryRun) : bool =
    run.Origin = MovementOrigin.TargetsApplied && not (List.isEmpty run.Lines)

/// Steht der Durchlauf aufgeklappt? Nur ein aufklappbarer kann offen sein.
let isRunOpen (history: GeneralHistory) (run: HistoryRun) : bool =
    expandable run && history.Expanded.Contains run.RunId

/// Der Leertext der Liste (budgeting-c6vn4, A9): ohne Filter nennt er, was hier erscheint.
let emptyText (filter: RunFilter) : string =
    match filter with
    | AllRuns -> "Noch keine Durchläufe. Targets anwenden, Verschieben, Zuweisen und Zuweisungen in YNAB erscheinen hier."
    | RunsOf _ -> "Keine Durchläufe dieser Art"

/// Text und Klassen des Betrags rechts in der Zeile (budgeting-c6vn4, A5), über dieselbe Funktion wie die
/// Einzelzeilen. Eine Verschiebung trägt kein Vorzeichen: ihre Richtung steht im Text.
let runAmount (run: HistoryRun) : string * string =
    match run.Origin with
    | MovementOrigin.MoneyMoved -> Money.formatAmountText false true run.Amount.Amount run.Amount.Currency, Money.signedListAmountClass
    | MovementOrigin.TakenOverFromYnab -> Money.formatAmountText false false run.Amount.Amount run.Amount.Currency, Money.signedListAmountClass
    | MovementOrigin.TargetsApplied
    | MovementOrigin.MoneyAssigned
    | MovementOrigin.ObservedInYnab -> historyChangeAmount run.Amount

/// Was als Stand eines Durchlaufs dasteht.
type RunBadge =
    /// Dasselbe Badge wie bei einer Bewegung, daneben ein Grund.
    | StatusBadge of HistoryStatus
    /// Ein Teil-Fehlschlag: ein rotes Badge, am Handy kurz ("1 von 3"), am PC lang ("1 von 3
    /// fehlgeschlagen"). Die Gründe stehen in den Einzelzeilen.
    | PartlyFailedBadge of short: string * long: string
    /// Kein Badge: der Durchlauf kam aus YNAB und wurde nicht geschrieben (budgeting-dgrp7).
    | NoBadge

/// Der Stand eines Durchlaufs in den Worten einer Bewegung (budgeting-c6vn4, A6). Ein Teil-Fehlschlag
/// steht ganz im Badge: neben Zeit und Monat passt am Handy kein zweiter Text.
let runBadge (status: RunStatus) : RunBadge =
    match status with
    | RunWritten -> StatusBadge HistoryWritten
    | RunOpen -> StatusBadge HistoryOpen
    | RunChangedInYnab -> StatusBadge HistoryChangedInYnab
    | RunReplacedInBb -> StatusBadge HistoryReplacedInBb
    | RunFromYnab -> NoBadge
    | RunFailed reason -> StatusBadge (HistoryFailed reason)
    | RunPartlyFailed (failed, total) -> PartlyFailedBadge ($"{failed} von {total}", $"{failed} von {total} fehlgeschlagen")

let private shortMonthNames =
    [| "Jan."; "Feb."; "März"; "Apr."; "Mai"; "Juni"; "Juli"; "Aug."; "Sept."; "Okt."; "Nov."; "Dez." |]

/// "2026-11-01" als "Nov. 2026", für die zweite Zeile am Handy. Ein unlesbarer Wert bleibt, wie er ist.
let shortMonthYearLabel (month: string) : string =
    match month.Split '-' with
    | [| year; mm; _ |] ->
        match System.Int32.TryParse mm with
        | true, m when m >= 1 && m <= 12 -> $"{shortMonthNames.[m - 1]} {year}"
        | _ -> month
    | _ -> month

let private withHistory (model: Model) (change: GeneralHistory -> GeneralHistory) : Model =
    { model with GeneralHistory = change model.GeneralHistory }

/// Der Knopf Historie: die Budget-Seite merkt sich die Rückkehr.
let openGeneralHistory (model: Model) : Model =
    withHistory model (fun h -> { h with ReturnPending = true })

/// Die Seite wird betreten: die Bewegungen kommen neu, Filter und Aufklappen beginnen von vorn.
let enterGeneralHistory (model: Model) : Model =
    withHistory model (fun h ->
        { h with Runs = Loading; Filter = AllRuns; Expanded = Set.empty; Generation = h.Generation + 1 })

/// "Wiederholen": nur neu laden, Filter und Aufklappen bleiben stehen.
let retryGeneralHistory (model: Model) : Model =
    withHistory model (fun h -> { h with Runs = Loading; Generation = h.Generation + 1 })

/// Eine Antwort mit fremder Generation ist überholt: ein späteres Betreten oder Wiederholen lädt schon.
let applyGeneralHistoryLoaded (generation: int) (result: Result<HistoryMovement list, string>) (model: Model) : Model =
    withHistory model (fun h ->
        if generation <> h.Generation then h
        else
            match result with
            | Ok movements -> { h with Runs = Success (historyRuns movements) }
            | Error message -> { h with Runs = Failure message })

let selectRunFilter (filter: RunFilter) (model: Model) : Model =
    withHistory model (fun h -> { h with Filter = filter })

/// Ein Tipp klappt auf, ein zweiter zu.
let toggleRun (runId: System.Guid) (model: Model) : Model =
    withHistory model (fun h ->
        let expanded =
            if h.Expanded.Contains runId then h.Expanded.Remove runId else h.Expanded.Add runId
        { h with Expanded = expanded })

/// Der Knopf Zurück: kam die Historie über den Knopf Historie, liegt die Budget-Seite im Browser-Verlauf
/// direkt davor und Zurück geht einen Schritt zurück, statt einen neuen Eintrag anzulegen. Per Link
/// geöffnet, führt es zur Budget-Seite.
let leaveGeneralHistory (model: Model) : ExternalMsg =
    if model.GeneralHistory.ReturnPending then StepBackToBudgetPage else ShowBudgetPage

/// Die Historie wurde woandershin verlassen: das nächste Betreten der Budget-Seite beginnt von vorn.
let abandonGeneralHistory (model: Model) : Model =
    withHistory model (fun h -> { h with ReturnPending = false })
