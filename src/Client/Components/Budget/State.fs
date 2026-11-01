/// Der Reduzierer der Budget-Seite (budgeting-k7wmd, Targets anwenden budgeting-h2zfn, Verschieben budgeting-afd8h). Die Übergänge selbst sind reine
/// Funktionen in `Components.Budget.Types` (`select`, `applyLoaded`); hier hängen nur die
/// Seiteneffekte (Aufruf an den Server) daran.
module Components.Budget.State

open Elmish
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Types
open Types
open Client

let init () : Model = Components.Budget.Types.init ()

let private loadCmd (model: Model) : Cmd<Msg> =
    let generation = model.MonthGeneration
    Cmd.OfAsync.either
        (Retry.reading Api.budget.getBudgetMonth)
        (monthChoiceKey model.Choice)
        (fun result -> Loaded (generation, result))
        (fun ex -> Loaded (generation, Error ex.Message))

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

// Wie `applyCmd` nie über `Retry`; der Server rechnet relativ, ein zweiter Lauf verschöbe doppelt.
let private moveCmd (monthKey: string) (request: MoveRequest) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Api.budget.moveMoney monthKey request)
        ()
        MoveAnswered
        (fun ex -> MoveCallLost ex.Message)

let private retryMoveCmd (applicationId: string) : Cmd<Msg> =
    Cmd.OfAsync.either
        Api.budget.retryFailedTargets
        applicationId
        (fun result -> MoveRetried (applicationId, result))
        (fun ex -> MoveRetried (applicationId, Error ex.Message))

/// Nach einer Verschiebung: Monat neu lesen, ohne `Loading`. Die Liste bleibt stehen, die Zeilen
/// springen nicht nach oben, und die neuen Beträge erscheinen, sobald sie da sind. Scheitert es,
/// bleibt die Liste auch stehen (`applyReloaded`).
let private reloadCmd (model: Model) : Cmd<Msg> =
    let generation = model.MonthGeneration
    Cmd.OfAsync.either
        (Retry.reading Api.budget.getBudgetMonth)
        (monthChoiceKey model.Choice)
        (fun result -> Reloaded (generation, result))
        (fun ex -> Reloaded (generation, Error ex.Message))

let private categoryEntriesCmd (categoryId: System.Guid) (month: string) (generation: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.ledger.getCategoryMonthEntries)
        { CategoryId = categoryId; Month = month }
        (fun result -> CategoryEntriesLoaded (generation, result))
        (fun ex -> CategoryEntriesLoaded (generation, Error ex.Message))

/// Das Kommando zur Liste, wenn der Übergang eine verlangt.
let private entriesCmdFor (model: Model, month: string option) : Cmd<Msg> =
    match model.Detail, month with
    | Some detail, Some month -> categoryEntriesCmd detail.CategoryId month model.DetailGeneration
    | _ -> Cmd.none

let private updatePage (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    // Ein offenes Sheet überlebt den Seitenwechsel nicht: es spränge sonst beim Betreten wieder auf.
    | PageEntered ->
        let model', month = enterPage model
        model', Cmd.batch [ Cmd.ofMsg Load; entriesCmdFor (model', month) ]
    | Load ->
        let model' = requestMonth { model with Month = Loading }
        model', loadCmd model'
    | SelectMonth choice ->
        match select choice model with
        | model', true -> model', loadCmd model'
        | model', false -> model', Cmd.none
    | OpenCategoryDetail categoryId ->
        let model', month = openCategoryDetail categoryId model
        model', entriesCmdFor (model', month)
    | CloseCategoryDetail
    | ReturnAbandoned -> { model with Detail = None }, Cmd.none
    | LoadCategoryEntries ->
        let model', month = reloadCategoryEntries model
        model', entriesCmdFor (model', month)
    | CategoryEntriesLoaded (generation, result) -> applyCategoryEntries generation result model, Cmd.none
    | CategorySearchChanged search -> { model with CategorySearch = search }, Cmd.none
    | ClearCategorySearch -> { model with CategorySearch = "" }, Cmd.none
    | Loaded (generation, result) -> applyLoaded generation result model, Cmd.none
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
        | model', true ->
            let reloading = { model' with Month = Loading }
            reloading, loadCmd reloading
        | model', false -> model', Cmd.none
    | _ -> model, Cmd.none

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | OpenEntryInLedger entryId -> leaveForEntry model, Cmd.none, JumpToEntry entryId
    | OpenMove categoryId -> openMove categoryId model, Cmd.none, NoOp
    | CloseMove -> closeMove model, Cmd.none, NoOp
    | ToggleMoveDirection -> updateForm toggleDirection model, Cmd.none, NoOp
    | MoveAmountChanged text -> updateForm (fun f -> { f with AmountInput = Some text }) model, Cmd.none, NoOp
    | OpenMovePicker -> updateForm (fun f -> { f with PickerOpen = true }) model, Cmd.none, NoOp
    | CloseMovePicker -> updateForm (fun f -> { f with PickerOpen = false }) model, Cmd.none, NoOp
    | PickMoveCounterpart side ->
        updateForm (fun f -> { f with Counterpart = Some side; PickerOpen = false }) model, Cmd.none, NoOp
    | ConfirmMove ->
        match confirmMove model with
        | model', Some (monthKey, request) -> model', moveCmd monthKey request, NoOp
        | model', None -> model', Cmd.none, NoOp
    | MoveAnswered result ->
        let model', external, reload = applyMoveAnswered result model
        model', (if reload then reloadCmd model' else Cmd.none), external
    | MoveCallLost message ->
        let model', external = applyMoveCallLost message model
        model', reloadCmd model', external
    | RetryMove applicationId ->
        match startMoveRetry applicationId model with
        | model', true -> model', retryMoveCmd applicationId, NoOp
        | model', false -> model', Cmd.none, NoOp
    | MoveRetried (applicationId, result) ->
        let model', external = applyMoveRetried applicationId result model
        model', reloadCmd model', external
    | Reloaded (generation, result) ->
        let model', external = applyReloaded generation result model
        model', Cmd.none, external
    | _ ->
        let model', cmd = updatePage msg model
        model', cmd, NoOp
