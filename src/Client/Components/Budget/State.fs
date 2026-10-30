/// Der Reduzierer der Budget-Seite (budgeting-k7wmd, Targets anwenden budgeting-h2zfn, Verschieben budgeting-afd8h). Die Übergänge selbst sind reine
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

// Wie `applyCmd` nie über `Retry`; der Server rechnet relativ, ein zweiter Lauf verschöbe doppelt.
let private moveCmd (choice: BudgetMonthChoice) (request: MoveRequest) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Api.budget.moveMoney (monthChoiceKey choice) request)
        ()
        MoveAnswered
        (fun ex -> MoveCallLost ex.Message)

let private retryMoveCmd (applicationId: string) : Cmd<Msg> =
    Cmd.OfAsync.either
        Api.budget.retryFailedTargets
        applicationId
        MoveRetried
        (fun ex -> MoveRetried (Error ex.Message))

/// Nach einer Verschiebung: Monat neu lesen, ohne `Loading`. Die Liste bleibt stehen, die Zeilen
/// springen nicht nach oben, und die neuen Beträge erscheinen, sobald sie da sind.
let private reloadCmd (reload: bool) (model: Model) : Cmd<Msg> =
    if reload then loadCmd model.Choice else Cmd.none

let private updatePage (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | PageEntered -> { model with CategorySearch = "" }, Cmd.ofMsg Load
    | Load -> { model with Month = Loading }, loadCmd model.Choice
    | SelectMonth choice ->
        match select choice model with
        | model', true -> model', loadCmd choice
        | model', false -> model', Cmd.none
    | CategorySearchChanged search -> { model with CategorySearch = search }, Cmd.none
    | ClearCategorySearch -> { model with CategorySearch = "" }, Cmd.none
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
    | _ -> model, Cmd.none

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | OpenMove category -> openMove category model, Cmd.none, NoOp
    | CloseMove -> closeMove model, Cmd.none, NoOp
    | ToggleMoveDirection -> updateForm toggleDirection model, Cmd.none, NoOp
    | MoveAmountChanged text -> updateForm (fun f -> { f with AmountText = text }) model, Cmd.none, NoOp
    | OpenMovePicker -> updateForm (fun f -> { f with PickerOpen = true }) model, Cmd.none, NoOp
    | CloseMovePicker -> updateForm (fun f -> { f with PickerOpen = false }) model, Cmd.none, NoOp
    | PickMoveCounterpart side ->
        updateForm (fun f -> { f with Counterpart = Some side; PickerOpen = false }) model, Cmd.none, NoOp
    | ConfirmMove ->
        match confirmMove model with
        | model', Some request -> model', moveCmd model'.Choice request, NoOp
        | model', None -> model', Cmd.none, NoOp
    | MoveAnswered result ->
        let model', external, reload = applyMoveAnswered result model
        model', reloadCmd reload model', external
    | MoveCallLost message ->
        let model', external = applyMoveCallLost message model
        model', loadCmd model'.Choice, external
    | RetryMove applicationId -> model, retryMoveCmd applicationId, NoOp
    | MoveRetried (Ok report) -> model, loadCmd model.Choice, moveToast report
    | MoveRetried (Error message) -> model, Cmd.none, ShowToast ($"Erneut senden gescheitert: {message}", ToastError)
    | _ ->
        let model', cmd = updatePage msg model
        model', cmd, NoOp
