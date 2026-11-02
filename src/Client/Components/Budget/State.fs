/// Der Reduzierer der Budget-Seite (budgeting-k7wmd, Targets anwenden budgeting-h2zfn, Verschieben
/// budgeting-afd8h, Liste und Inspektor budgeting-g8s3w). Die Übergänge selbst sind reine Funktionen
/// in `Components.Budget.Types` (`select`, `applyLoaded`); hier hängen nur die Seiteneffekte
/// (Aufruf an den Server) daran.
module Components.Budget.State

open Elmish
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Types
open Types
open Client

let init () : Model = Components.Budget.Types.init ()

let private frameCmd () : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.budget.getBudgetFrame)
        ()
        FrameLoaded
        (fun ex -> FrameLoaded (Error ex.Message))

/// Das stille Nachlesen, wenn das Fenster gewachsen sein kann. Kein `Loading`, kein Toast.
let private frameRefreshCmd (due: bool) : Cmd<Msg> =
    if due then
        Cmd.OfAsync.either
            (Retry.reading Api.budget.getBudgetFrame)
            ()
            FrameRefreshed
            (fun ex -> FrameRefreshed (Error ex.Message))
    else
        Cmd.none

let private monthCmd (model: Model) (answer: int * Result<BudgetMonth, string> -> Msg) : Cmd<Msg> =
    let generation = model.MonthGeneration
    match shownMonth model with
    | Some month ->
        Cmd.OfAsync.either
            (Retry.reading Api.budget.getBudgetMonth)
            month
            (fun result -> answer (generation, result))
            (fun ex -> answer (generation, Error ex.Message))
    | None -> Cmd.none

let private loadCmd (model: Model) : Cmd<Msg> = monthCmd model Loaded

let private previewCmd (month: string) : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.budget.previewTargets)
        month
        (fun result -> PreviewLoaded (month, result))
        (fun ex -> PreviewLoaded (month, Error ex.Message))

// Schreibende Aufrufe laufen nie über `Retry`: ein abgebrochener Aufruf kann beim Server
// angekommen sein.
let private applyCmd (preview: Shared.Budgeting.TargetPreview) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Api.budget.applyTargets preview.Month preview)
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
let private reloadCmd (model: Model) : Cmd<Msg> = monthCmd model Reloaded

let private categoryEntriesCmd (categoryId: System.Guid) (month: string) (generation: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.ledger.getCategoryMonthEntries)
        { CategoryId = categoryId; Month = month }
        (fun result -> CategoryEntriesLoaded (generation, result))
        (fun ex -> CategoryEntriesLoaded (generation, Error ex.Message))

/// Holt die Buchungen, wenn der Übergang sie fällig gemacht hat (`entriesDue`).
let private withEntries (model: Model) : Model * Cmd<Msg> =
    match requestEntries model with
    | model', Some (categoryId, month, generation) -> model', categoryEntriesCmd categoryId month generation
    | model', None -> model', Cmd.none

let private updatePage (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    // Ein offenes Sheet überlebt den Seitenwechsel nicht: es spränge sonst beim Betreten wieder auf.
    | PageEntered ->
        match enterPage model with
        | model', FreshEntry ->
            beginLoad model', frameCmd ()
        | model', ReturnedFromLedger ->
            let loading = requestMonth { model' with Month = Loading }
            loading, loadCmd loading
    | Load -> beginLoad model, frameCmd ()
    | FrameRefreshed result ->
        match applyFrameRefreshed result model with
        | model', true -> model', loadCmd model'
        | model', false -> model', Cmd.none
    | SelectMonth month ->
        match select month model with
        | model', true -> model', loadCmd model'
        | model', false -> model', Cmd.none
    | SelectCategory categoryId -> withEntries (selectCategory categoryId model)
    | StepCategory direction -> withEntries (stepSelection direction model)
    | SelectInspectorTab tab -> withEntries { model with Tab = tab }
    | CloseInspector
    | ReturnAbandoned -> { model with Selected = None }, Cmd.none
    | LoadCategoryEntries -> withEntries (reloadCategoryEntries model)
    | CategoryEntriesLoaded (generation, result) -> applyCategoryEntries generation result model, Cmd.none
    | CategorySearchChanged search -> { model with CategorySearch = search }, Cmd.none
    | ClearCategorySearch -> { model with CategorySearch = "" }, Cmd.none
    | SelectFilter filter -> { model with Filter = filter }, Cmd.none
    | Loaded (generation, result) -> withEntries (applyLoaded generation result model)
    | OpenTargets ->
        match openTargets model, shownMonth model with
        | (model', true), Some month -> model', previewCmd month
        | (model', _), _ -> model', Cmd.none
    | PreviewLoaded (month, result) -> applyPreviewLoaded month result model, Cmd.none
    | ConfirmTargets ->
        match confirmTargets model with
        | model', Some preview -> model', applyCmd preview
        | model', None -> model', Cmd.none
    | TargetsApplied result ->
        let due =
            match result with
            | Ok (Applied report) -> frameDueAfterReport report model
            | Ok (PreviewChanged _) -> false
            | Error _ -> frameDueAfterLostCall model
        applyApplied result model, frameRefreshCmd due
    | RetryFailed ->
        match retryFailed model with
        | model', Some applicationId -> model', retryCmd applicationId
        | model', None -> model', Cmd.none
    | TargetsRetried result ->
        applyRetried result model, frameRefreshCmd (frameDueAfterRetry result model)
    | CloseTargets ->
        match closeTargets model with
        | model', true ->
            let reloading = { model' with Month = Loading }
            reloading, loadCmd reloading
        | model', false -> model', Cmd.none
    | _ -> model, Cmd.none

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | FrameLoaded result ->
        let model', external, loadMonth = applyFrameLoaded result model
        model', (if loadMonth then loadCmd model' else Cmd.none), external
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
        let due =
            match result with
            | Ok report -> frameDueAfterReport report model
            | Error _ -> false
        model', Cmd.batch [ (if reload then reloadCmd model' else Cmd.none); frameRefreshCmd due ], external
    | MoveCallLost message ->
        let model', external = applyMoveCallLost message model
        model', Cmd.batch [ reloadCmd model'; frameRefreshCmd (frameDueAfterLostCall model) ], external
    | RetryMove applicationId ->
        match startMoveRetry applicationId model with
        | model', true -> model', retryMoveCmd applicationId, NoOp
        | model', false -> model', Cmd.none, NoOp
    | MoveRetried (applicationId, result) ->
        let model', external = applyMoveRetried applicationId result model
        model', Cmd.batch [ reloadCmd model'; frameRefreshCmd (frameDueAfterRetry result model) ], external
    // Gewinnt `Reloaded` den Lauf gegen `Loaded`, ist nur hier die Stelle, die Buchungen zu holen.
    | Reloaded (generation, result) ->
        let model', external = applyReloaded generation result model
        let withRequest, cmd = withEntries model'
        withRequest, cmd, external
    | _ ->
        let model', cmd = updatePage msg model
        model', cmd, NoOp
