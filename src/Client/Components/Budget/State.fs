/// Der Reduzierer der Budget-Seite (budgeting-k7wmd, Targets anwenden budgeting-h2zfn, Verschieben
/// budgeting-afd8h, Liste und Inspektor budgeting-g8s3w). Die Übergänge selbst sind reine Funktionen
/// in `Components.Budget.Month` und den Nachbarmodulen (`select`, `applyLoaded`); hier hängen nur die Seiteneffekte
/// (Aufruf an den Server) daran.
module Components.Budget.State

open Elmish
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Targets
open Components.Budget.Moves
open Components.Budget.Inspector
open Types
open Client

let init () : Model = Components.Budget.Types.init ()

// Die Aufrufe liegen hinter einem Lambda: `update` läuft so auch unter .NET, wo der Proxy beim ersten
// Zugriff wirft. Ausgeführt werden die Cmds dort nie.
let private frameCmd () : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.reading Api.budget.getBudgetFrame ())
        ()
        FrameLoaded
        (fun ex -> FrameLoaded (Error ex.Message))

/// Das stille Nachlesen, wenn das Fenster gewachsen sein kann. Kein `Loading`, kein Toast.
let private frameRefreshCmd (due: bool) : Cmd<Msg> =
    if due then
        Cmd.OfAsync.either
            (fun () -> Retry.reading Api.budget.getBudgetFrame ())
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
            (fun key -> Retry.reading Api.budget.getBudgetMonth key)
            month
            (fun result -> answer (generation, result))
            (fun ex -> answer (generation, Error ex.Message))
    | None -> Cmd.none

let private loadCmd (model: Model) : Cmd<Msg> = monthCmd model Loaded

let private previewCmd (month: string) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun key -> Retry.reading Api.budget.previewTargets key)
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
        (fun id -> Api.budget.retryFailedTargets id)
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
        (fun id -> Api.budget.retryFailedTargets id)
        applicationId
        (fun result -> MoveRetried (applicationId, result))
        (fun ex -> MoveRetried (applicationId, Error ex.Message))

// ADR 0078: absolut, aber trotzdem nie über `Retry`. Der Ausgang geht an die Schlange.
let private assignCmd (write: AssignWrite) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Api.budget.assignMoney write.Month { CategoryId = write.CategoryId; Assigned = write.Assigned })
        ()
        (fun result -> AssignAnswered (write, result))
        (fun ex -> AssignCallLost ex.Message)

let private recentCmd () : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.reading Api.budget.getRecentMoveCategories ())
        ()
        RecentCounterpartsLoaded
        (fun ex -> RecentCounterpartsLoaded (Error ex.Message))

/// Startet den Kopf der Schlange, wenn keiner läuft.
let private flushAssign (model: Model) : Model * Cmd<Msg> =
    match startNextAssign model with
    | model', Some write -> model', assignCmd write
    | model', None -> model', Cmd.none

/// Nach einer Verschiebung: Monat neu lesen, ohne `Loading`. Die Liste bleibt stehen, die Zeilen
/// springen nicht nach oben, und die neuen Beträge erscheinen, sobald sie da sind. Scheitert es,
/// bleibt die Liste auch stehen (`applyReloaded`).
let private reloadCmd (model: Model) : Cmd<Msg> = monthCmd model Reloaded

let private categoryEntriesCmd (categoryId: System.Guid) (month: string) (generation: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun query -> Retry.reading Api.ledger.getCategoryMonthEntries query)
        { CategoryId = categoryId; Month = month }
        (fun result -> CategoryEntriesLoaded (generation, result))
        (fun ex -> CategoryEntriesLoaded (generation, Error ex.Message))

/// Holt die Buchungen, wenn der Übergang sie fällig gemacht hat (`entriesDue`).
let private withEntries (model: Model) : Model * Cmd<Msg> =
    match requestEntries model with
    | model', Some (categoryId, month, generation) -> model', categoryEntriesCmd categoryId month generation
    | model', None -> model', Cmd.none

/// Ein einziges `match` ohne Sammelzweig: eine neue Nachricht ohne Zweig meldet der Compiler.
let private transition (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let quiet (model': Model, cmd: Cmd<Msg>) = model', cmd, NoOp
    match msg with
    // Ein offenes Sheet überlebt den Seitenwechsel nicht: es spränge sonst beim Betreten wieder auf.
    | PageEntered ->
        match enterPage model with
        | model', FreshEntry -> beginLoad model', frameCmd (), NoOp
        | model', ReturnedFromLedger ->
            let loading = requestMonth { model' with Month = Loading }
            loading, loadCmd loading, NoOp
    | Load -> beginLoad model, frameCmd (), NoOp
    | FrameLoaded result ->
        let model', external, loadMonth = applyFrameLoaded result model
        model', (if loadMonth then loadCmd model' else Cmd.none), external
    | FrameRefreshed result ->
        match applyFrameRefreshed result model with
        | model', true -> model', loadCmd model', NoOp
        | model', false -> model', Cmd.none, NoOp
    | SelectMonth month ->
        match select month model with
        | model', true -> model', loadCmd model', NoOp
        | model', false -> model', Cmd.none, NoOp
    | SelectCategory categoryId -> quiet (withEntries (selectCategory categoryId model))
    | StepCategory direction -> quiet (withEntries (stepSelection direction model))
    | PageKeyPressed action -> quiet (withEntries (applyPageKey action model))
    | SelectInspectorTab tab -> quiet (withEntries { model with Tab = tab })
    | CloseInspector
    | ReturnAbandoned -> { model with Selected = None }, Cmd.none, NoOp
    | PageLeft -> model, Cmd.none, NoOp
    | SwitchMoneyMode mode -> switchMode mode model, Cmd.none, NoOp
    | AssignTextChanged text -> typeAssign text model, Cmd.none, NoOp
    | AssignStepped delta -> stepAssign delta model, Cmd.none, NoOp
    | AssignFieldLeft -> saveEntry model, Cmd.none, NoOp
    | AssignDiscarded -> discardEntry model, Cmd.none, NoOp
    | CoverMissing ->
        match confirmMissing model with
        | model', Some (monthKey, request) -> model', moveCmd monthKey request, NoOp
        | model', None -> model', Cmd.none, NoOp
    | AssignAnswered (_, result) ->
        let model', external, reload = applyAssignAnswered result model
        let due =
            match result with
            | Ok report -> frameDueAfterReport report model
            | Error _ -> false
        model', Cmd.batch [ (if reload then reloadCmd model' else Cmd.none); frameRefreshCmd due ], external
    | AssignCallLost message ->
        let model', external, reload = applyAssignCallLost message model
        model',
        Cmd.batch [ (if reload then reloadCmd model' else Cmd.none); frameRefreshCmd (frameDueAfterLostCall model) ],
        external
    | TransferAmountChanged text -> updateTransfer (fun f -> { f with AmountInput = Some text }) model, Cmd.none, NoOp
    | TransferAmountReset -> updateTransfer (fun f -> { f with AmountInput = None }) model, Cmd.none, NoOp
    | OpenPicker ->
        if inspectorBusy model then
            model, Cmd.none, NoOp
        else
            updateTransfer (fun f -> { f with PickerOpen = true }) (requestRecentCounterparts model), recentCmd (), NoOp
    | ClosePicker -> updateTransfer (fun f -> { f with PickerOpen = false }) model, Cmd.none, NoOp
    | PickCounterpart side ->
        updateTransfer (fun f -> { f with Counterpart = Some side; PickerOpen = false }) model, Cmd.none, NoOp
    | RecentCounterpartsLoaded result -> applyRecentCounterparts result model, Cmd.none, NoOp
    | ConfirmTransfer ->
        match confirmTransfer model with
        | model', Some (monthKey, request) -> model', moveCmd monthKey request, NoOp
        | model', None -> model', Cmd.none, NoOp
    | LoadCategoryEntries -> quiet (withEntries (reloadCategoryEntries model))
    | CategoryEntriesLoaded (generation, result) -> applyCategoryEntries generation result model, Cmd.none, NoOp
    | CategorySearchChanged search -> { model with CategorySearch = search }, Cmd.none, NoOp
    | ClearCategorySearch -> { model with CategorySearch = "" }, Cmd.none, NoOp
    | SelectFilter filter -> { model with Filter = filter }, Cmd.none, NoOp
    | Loaded (generation, result) -> quiet (withEntries (applyLoaded generation result model))
    | OpenTargets ->
        match openTargets model, shownMonth model with
        | (model', true), Some month -> model', previewCmd month, NoOp
        | (model', _), _ -> model', Cmd.none, NoOp
    | PreviewLoaded (month, result) -> applyPreviewLoaded month result model, Cmd.none, NoOp
    | ConfirmTargets ->
        match confirmTargets model with
        | model', Some preview -> model', applyCmd preview, NoOp
        | model', None -> model', Cmd.none, NoOp
    | TargetsApplied result ->
        let due =
            match result with
            | Ok (Applied report) -> frameDueAfterReport report model
            | Ok (PreviewChanged _) -> false
            | Error _ -> frameDueAfterLostCall model
        applyApplied result model, frameRefreshCmd due, NoOp
    | RetryFailed ->
        match retryFailed model with
        | model', Some applicationId -> model', retryCmd applicationId, NoOp
        | model', None -> model', Cmd.none, NoOp
    | TargetsRetried result ->
        applyRetried result model, frameRefreshCmd (frameDueAfterRetry result model), NoOp
    | CloseTargets ->
        match closeTargets model with
        | model', true ->
            let reloading = { model' with Month = Loading }
            reloading, loadCmd reloading, NoOp
        | model', false -> model', Cmd.none, NoOp
    | OpenEntryInLedger entryId -> leaveForEntry model, Cmd.none, JumpToEntry entryId
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

/// Verlässt die Nachricht den Inspektor? Dann wird vorher gespeichert (`leaveInspector`).
let private leavesInspector (msg: Msg) (model: Model) : bool =
    match msg with
    | SelectMonth _
    | SelectCategory _
    | StepCategory _
    | SelectInspectorTab _
    | CloseInspector
    | ReturnAbandoned
    | PageLeft -> true
    | PageKeyPressed action -> action <> LeaveKey
    | SwitchMoneyMode mode -> model.TabMoney |> Option.exists (fun tab -> tab.Mode <> mode)
    | _ -> false

/// Ändert die Nachricht das Feld Zugewiesen? Dann wartet sie, bis der Aufruf aus dem Tab zurück ist.
let private touchesAssignField (msg: Msg) : bool =
    match msg with
    | AssignTextChanged _
    | AssignStepped _
    | AssignFieldLeft
    | AssignDiscarded -> true
    | _ -> false

/// Startet die Nachricht eine Bewegung? Der Fehlbetrag-Knopf gehört nicht dazu: Er verwirft die
/// offene Eingabe und sperrt über `canSend`.
let private startsMove (msg: Msg) : bool =
    match msg with
    | RetryMove _
    | ConfirmTargets
    | RetryFailed
    | ConfirmTransfer -> true
    | _ -> false

/// Eine Bewegung startet nur, wenn Zuweisen ruht (`assignSettled`). Sonst ist der Tipp ein Verlassen:
/// Eine offene Eingabe geht in die Schlange und die Bewegung startet nicht, auch nicht von selbst
/// danach. Wer sie will, sieht die neue Lage und tippt noch einmal. Fiel die Eingabe ohne Änderung
/// weg, hat sich nichts geändert, und die Bewegung startet. Bleibt der Tipp ohne sichtbare Wirkung,
/// nennt ein Toast die Sperre: Der Weg über den Toast "Erneut senden" hat keinen gesperrten Knopf.
let private startMove (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let blocked = ShowToast (moveBlockedText, ToastWarning)
    if moveRunning model then
        model, Cmd.none, blocked
    else
        let left, external = if assignSettled model then model, NoOp else leaveInspector model
        if assignSettled left && external = NoOp then
            transition msg left
        elif external = NoOp && left.AssignQueue = model.AssignQueue then
            left, Cmd.none, blocked
        else
            left, Cmd.none, external

/// Der Warn-Toast, wenn die gewählte Kategorie beim Neu-Lesen wegfiel.
let private droppedEntryToast =
    ShowToast ("Zugewiesen nicht gespeichert: die Kategorie gibt es nicht mehr", ToastWarning)

/// Der Einstieg. Eine Bewegung startet über `startMove`. Solange ein Aufruf aus dem Tab läuft, tun alle Wege aus dem Inspektor und das Feld
/// Zugewiesen nichts (ADR 0078 Punkt 5). Vor einem Verlassen wird gespeichert. Nach jeder Nachricht
/// folgt der Tab dem Schlüssel und die Schlange läuft weiter.
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let leaving = leavesInspector msg model
    let model', cmd, external =
        if startsMove msg then
            startMove msg model
        elif inspectorBusy model && (leaving || touchesAssignField msg) then
            model, Cmd.none, NoOp
        elif leaving then
            let left, leaveExternal = leaveInspector model
            let moved, cmd, moveExternal = transition msg left
            moved, cmd, (if leaveExternal = NoOp then moveExternal else leaveExternal)
        else
            transition msg model
    let synced = syncMoneyTab model'
    let external' = if entryDropped model' synced && external = NoOp then droppedEntryToast else external
    let flushed, sendCmd = flushAssign synced
    flushed, Cmd.batch [ cmd; sendCmd ], external'
