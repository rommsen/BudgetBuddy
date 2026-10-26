/// Der Reduzierer der Budget-Seite (budgeting-k7wmd, Targets anwenden budgeting-h2zfn). Die Übergänge selbst sind reine
/// Funktionen in `Components.Budget.Types` (`select`, `applyLoaded`); hier hängen nur die
/// Seiteneffekte (Aufruf an den Server) daran.
module Components.Budget.State

open Elmish
open Shared.Budgeting
open Components.Budget.Types
open Types
open Client

let init () : Model = Components.Budget.Types.init ()

let private loadCmd (choice: BudgetMonthChoice) : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.budget.getBudgetMonth)
        (monthChoiceKey choice)
        (fun result -> Loaded (choice, result))
        (fun ex -> Loaded (choice, Error ex.Message))

let private previewCmd (choice: BudgetMonthChoice) : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.budget.previewTargets)
        (monthChoiceKey choice)
        (fun result -> PreviewLoaded (choice, result))
        (fun ex -> PreviewLoaded (choice, Error ex.Message))

// Schreibende Aufrufe laufen nie über `Retry`: ein abgebrochener Aufruf kann beim Server
// angekommen sein.
let private applyCmd (choice: BudgetMonthChoice) (preview: Shared.Budgeting.TargetPreview) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Api.budget.applyTargets (monthChoiceKey choice) preview)
        ()
        TargetsApplied
        (fun ex -> TargetsApplied (Error ex.Message))

let private retryCmd (applicationId: string) : Cmd<Msg> =
    Cmd.OfAsync.either
        Api.budget.retryFailedTargets
        applicationId
        TargetsRetried
        (fun ex -> TargetsRetried (Error ex.Message))

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | Load -> { model with Month = Loading }, loadCmd model.Choice
    | SelectMonth choice ->
        match select choice model with
        | model', true -> model', loadCmd choice
        | model', false -> model', Cmd.none
    | Loaded (choice, result) -> applyLoaded choice result model, Cmd.none
    | OpenTargets ->
        match openTargets model with
        | model', true -> model', previewCmd model'.Choice
        | model', false -> model', Cmd.none
    | PreviewLoaded (choice, result) -> applyPreviewLoaded choice result model, Cmd.none
    | ConfirmTargets ->
        match confirmTargets model with
        | model', Some preview -> model', applyCmd model'.Choice preview
        | model', None -> model', Cmd.none
    | TargetsApplied result -> applyApplied result model, Cmd.none
    | RetryFailed ->
        match retryFailed model with
        | model', Some applicationId -> model', retryCmd applicationId
        | model', None -> model', Cmd.none
    | TargetsRetried result -> applyRetried result model, Cmd.none
    | CloseTargets ->
        match closeTargets model with
        | model', true -> { model' with Month = Loading }, loadCmd model'.Choice
        | model', false -> model', Cmd.none
