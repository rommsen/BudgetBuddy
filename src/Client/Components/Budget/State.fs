/// Der Reduzierer der Budget-Seite. Die Übergänge sind reine Funktionen in `Month`, `Targets`, `Moves`
/// und `Inspector`; hier hängen die Seiteneffekte daran, und `update` ordnet jede Nachricht ein
/// (`routeOf`), bevor sie wirkt.
module Components.Budget.State

open Elmish
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Targets
open Components.Budget.Moves
open Components.Budget.Inspector
open Components.Budget.Pause
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

let private monthCmd (reread: MonthReread) (model: Model) : Cmd<Msg> =
    let answer = rereadAnswer reread model
    match shownMonth model with
    | Some month ->
        Cmd.OfAsync.either
            (fun key -> Retry.reading Api.budget.getBudgetMonth key)
            month
            answer
            (fun ex -> answer (Error ex.Message))
    | None -> Cmd.none

let private loadCmd (model: Model) : Cmd<Msg> = monthCmd FullReread model

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
        AssignAnswered
        (fun ex -> AssignCallLost ex.Message)

// Schreibt in BB, deshalb nicht über `Retry`. Die Antwort trägt Kategorie und Monat des Aufrufs.
let private pauseCmd (monthKey: string) (request: TargetPauseRequest) : Cmd<Msg> =
    let answer result = TargetPauseAnswered (request.CategoryId, monthKey, result)
    Cmd.OfAsync.either
        (fun () -> Api.budget.setTargetPause monthKey request)
        ()
        answer
        (fun ex -> answer (Error ex.Message))

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

/// Nach einer Bewegung oder Zuweisung: Monat neu lesen, ohne `Loading`. Die Liste bleibt stehen, die
/// Zeilen springen nicht nach oben, und die neuen Beträge erscheinen, sobald sie da sind. Scheitert es,
/// bleibt die Liste auch stehen (`applyReloaded`).
let private reloadCmd (model: Model) : Cmd<Msg> = monthCmd QuietReread model

/// Liest den Monat so, wie `rereadMonth` es verlangt (`rereadAnswer`).
let private rereadCmd (reread: MonthReread) (model: Model) : Cmd<Msg> = monthCmd reread model

/// Die Antwort auf eine Schreibung: der Monat, wenn sie die letzte war, und der Rahmen, wenn das
/// Fenster gewachsen sein kann.
let private afterAnswer (reload: bool) (frameDue: bool) (model: Model) : Cmd<Msg> =
    Cmd.batch [ (if reload then reloadCmd model else Cmd.none); frameRefreshCmd frameDue ]

let private frameDueAfterAnswer (result: Result<ApplyReport, string>) (model: Model) : bool =
    match result with
    | Ok report -> frameDueAfterReport report model
    | Error _ -> false

/// Die Abfrage der Tab-Daten. Die Antwort trägt die Generation der Anfrage (`entriesRequestAnswer`,
/// `historyRequestAnswer`).
let private tabDataCmd (request: TabRequest) : Cmd<Msg> =
    let query : CategoryMonthQuery = { CategoryId = request.CategoryId; Month = request.Month }
    match request.Tab with
    | HistoryTab ->
        let answer = historyRequestAnswer request
        Cmd.OfAsync.either
            (fun q -> Retry.reading Api.budget.getCategoryHistory q)
            query
            answer
            (fun ex -> answer (Error ex.Message))
    | EntriesTab
    | MoneyTab
    | TargetTab ->
        let answer = entriesRequestAnswer request
        Cmd.OfAsync.either
            (fun q -> Retry.reading Api.ledger.getCategoryMonthEntries q)
            query
            answer
            (fun ex -> answer (Error ex.Message))

/// Holt die Daten des sichtbaren Tabs, wenn der Übergang sie fällig gemacht hat (`tabDataDue`).
let private withTabData (model: Model) : Model * Cmd<Msg> =
    match requestTabData model with
    | model', Some request -> model', tabDataCmd request
    | model', None -> model', Cmd.none

/// Nach einer Bewegung (Zuweisen, Holen, Abgeben, Targets anwenden, erneut senden): Die Historie ist
/// wieder fällig (`historyAfterMovement`). Steht sie im sichtbaren Tab, kommt sie still neu, sonst,
/// sobald ihr Tab zu sehen ist. Laufende Buchungen bleiben.
let private afterMovement (model: Model, cmd: Cmd<Msg>, external: ExternalMsg) : Model * Cmd<Msg> * ExternalMsg =
    let reread, quiet = historyAfterMovement model
    let fetched, fetch = withTabData reread
    fetched, Cmd.batch [ cmd; (quiet |> Option.map tabDataCmd |> Option.defaultValue Cmd.none); fetch ], external

/// `afterMovement` nur, wenn die Antwort den Monat neu liest: In der Zuweisungs-Schlange ist das die
/// letzte Antwort, eine abgelehnte Verschiebung ändert nichts.
let private afterMovementWhen (reload: bool) (model: Model, cmd: Cmd<Msg>, external: ExternalMsg) : Model * Cmd<Msg> * ExternalMsg =
    if reload then afterMovement (model, cmd, external) else model, cmd, external

/// Ein einziges `match` ohne Sammelzweig: eine neue Nachricht ohne Zweig meldet der Compiler.
let private transition (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let quiet (model': Model, cmd: Cmd<Msg>) = model', cmd, NoOp
    match msg with
    | PageEntered ->
        match enterPage model with
        | model', FreshEntry -> beginLoad model', frameCmd (), NoOp
        | model', KeptPage ->
            let rereading, reread = rereadMonth model'
            let withRequest, tabDataCmd = withTabData rereading
            withRequest, Cmd.batch [ rereadCmd reread withRequest; tabDataCmd ], NoOp
    | Load -> beginLoad model, frameCmd (), NoOp
    | FrameLoaded result ->
        let model', external, loadMonth = applyFrameLoaded result model
        model', (if loadMonth then loadCmd model' else Cmd.none), external
    | FrameRefreshed result ->
        // Der Monat rückt weiter: Die Eingabe gilt dem alten und wird wie beim Verlassen gesichert.
        let left, outcome = if frameMovesMonth result model then leaveInspector model else model, NothingOpen
        match applyFrameRefreshed result left with
        | model', true -> model', loadCmd model', leaveToast outcome
        | model', false -> model', Cmd.none, NoOp
    | SelectMonth month ->
        match select month model with
        | model', true -> model', loadCmd model', NoOp
        | model', false -> model', Cmd.none, NoOp
    | SelectCategory categoryId -> quiet (withTabData (selectCategory categoryId model))
    | StepCategory direction -> quiet (withTabData (stepSelection direction model))
    | PageKeyPressed action -> quiet (withTabData (applyPageKey action model))
    | SelectInspectorTab tab -> quiet (withTabData { model with Tab = tab })
    | CloseInspector
    | ReturnAbandoned -> { model with Selected = None }, Cmd.none, NoOp
    | PageLeft ->
        let away =
            match model.MoveInFlight with
            | MoveCallRunning -> MoveCallRunningAway
            | call -> call
        { model with MoveInFlight = away }, Cmd.none, NoOp
    | SwitchMoneyMode mode -> switchMode mode model, Cmd.none, NoOp
    | AssignTextChanged text -> typeAssign text model, Cmd.none, NoOp
    | AssignStepped delta -> stepAssign delta model, Cmd.none, NoOp
    | AssignFieldLeft -> saveEntry model, Cmd.none, NoOp
    | AssignDiscarded -> discardEntry model, Cmd.none, NoOp
    | CoverMissing ->
        match (if canCover model then confirmMissing model else model, None) with
        | model', Some (monthKey, request) ->
            let external =
                if coverDropsEntry model then ShowToast (coverDroppedEntryText, ToastWarning) else NoOp
            model', moveCmd monthKey request, external
        | model', None -> model', Cmd.none, NoOp
    | AssignAnswered result ->
        let model', external, reload = applyAssignAnswered result model
        afterMovementWhen reload (model', afterAnswer reload (frameDueAfterAnswer result model) model', external)
    | AssignCallLost message ->
        let model', external, reload = applyAssignCallLost message model
        afterMovementWhen reload (model', afterAnswer reload (frameDueAfterLostCall model) model', external)
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
        match (if canSend model then confirmTransfer model else model, None) with
        | model', Some (monthKey, request) -> model', moveCmd monthKey request, NoOp
        | model', None -> model', Cmd.none, NoOp
    | LoadCategoryEntries -> quiet (withTabData (resetTab EntriesTab model))
    | CategoryEntriesLoaded (generation, result) -> applyCategoryEntries generation result model, Cmd.none, NoOp
    | LoadCategoryHistory -> quiet (withTabData (resetTab HistoryTab model))
    | CategoryHistoryLoaded (generation, result) -> applyCategoryHistory generation result model, Cmd.none, NoOp
    | CategoryHistoryReloaded (generation, result) ->
        let model', external = applyCategoryHistoryReloaded generation result model
        model', Cmd.none, external
    | CategorySearchChanged search -> { model with CategorySearch = search }, Cmd.none, NoOp
    | ClearCategorySearch -> { model with CategorySearch = "" }, Cmd.none, NoOp
    | SelectFilter filter -> { model with Filter = filter }, Cmd.none, NoOp
    | Loaded (generation, result) -> quiet (withTabData (applyLoaded generation result model))
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
        let applied = applyApplied result model
        // Nur ein angewendeter Lauf bewegt Geld. Ein Probelauf ändert nichts, und nach einem Fehler
        // fragt der Dialog erneut senden an; die Historie bleibt, wie sie ist.
        match result with
        | Ok (Applied report) -> afterMovement (applied, frameRefreshCmd (frameDueAfterReport report model), NoOp)
        | Ok (PreviewChanged _) -> applied, Cmd.none, NoOp
        | Error _ -> applied, frameRefreshCmd (frameDueAfterLostCall model), NoOp
    | RetryFailed ->
        match retryFailed model with
        | model', Some applicationId -> model', retryCmd applicationId, NoOp
        | model', None -> model', Cmd.none, NoOp
    | TargetsRetried result ->
        afterMovement (applyRetried result model, frameRefreshCmd (frameDueAfterRetry result model), NoOp)
    | CloseTargets ->
        match closeTargets model with
        | model', Some reread -> model', rereadCmd reread model', NoOp
        | model', None -> model', Cmd.none, NoOp
    | OpenEntryInLedger entryId -> leaveForEntry model, Cmd.none, JumpToEntry entryId
    | SetTargetPause paused ->
        match startPause paused model with
        | model', Some (monthKey, request) -> model', pauseCmd monthKey request, NoOp
        | model', None -> model', Cmd.none, NoOp
    | TargetPauseAnswered (categoryId, month, result) ->
        let answered = applyPauseAnswered categoryId month result model
        match result with
        | Error _ -> answered, Cmd.none, NoOp
        | Ok _ ->
            // Ein Neu-Lesen, das vor der Pause begann, nähme sie zurück: dann noch einmal lesen.
            let reread, cmd =
                if pauseNeedsReread month answered then
                    let rereading, how = rereadMonth answered
                    rereading, rereadCmd how rereading
                else
                    answered, Cmd.none
            if pauseAnswerShown categoryId month reread then afterMovement (reread, cmd, NoOp)
            else reread, cmd, NoOp
    | MoveAnswered result ->
        let model', external, reload = applyMoveAnswered result model
        afterMovementWhen reload (model', afterAnswer reload (frameDueAfterAnswer result model) model', external)
    | MoveCallLost message ->
        let model', external = applyMoveCallLost message model
        afterMovement (model', afterAnswer true (frameDueAfterLostCall model) model', external)
    | RetryMove applicationId ->
        match startMoveRetry applicationId model with
        | model', true -> model', retryMoveCmd applicationId, NoOp
        | model', false -> model', Cmd.none, NoOp
    | MoveRetried (applicationId, result) ->
        let model', external = applyMoveRetried applicationId result model
        afterMovement (model', afterAnswer true (frameDueAfterRetry result model) model', external)
    // Gewinnt `Reloaded` den Lauf gegen `Loaded`, ist nur hier die Stelle, die Tab-Daten zu holen.
    | Reloaded (generation, result) ->
        let model', external = applyReloaded generation result model
        let withRequest, cmd = withTabData model'
        withRequest, cmd, external

/// Wie `update` eine Nachricht behandelt.
[<RequireQualifiedAccess>]
type MsgRoute =
    /// Startet eine Bewegung, wenn Zuweisen ruht (`startMove`). Der Fehlbetrag-Knopf gehört nicht dazu:
    /// Er verwirft die offene Eingabe, statt sie zu speichern.
    | StartsMove
    /// Verlässt den Inspektor: speichert vorher, wartet aber auf einen laufenden Aufruf.
    | Leaves
    /// Der Inspektor ist schon verlassen: speichert vorher, lässt sich aber nicht aufhalten.
    | Left
    /// Ändert das Feld Zugewiesen: wartet auf einen laufenden Aufruf.
    | TouchesField
    | Free

/// Ohne Sammelzweig: Eine neue Nachricht ordnet der Compiler hier ein.
let private routeOf (msg: Msg) (model: Model) : MsgRoute =
    match msg with
    | RetryMove _
    | ConfirmTargets
    | RetryFailed
    | ConfirmTransfer -> MsgRoute.StartsMove
    | SelectMonth _
    | SelectCategory _
    | StepCategory _
    | SelectInspectorTab _
    | CloseInspector -> MsgRoute.Leaves
    | ReturnAbandoned
    | PageLeft -> MsgRoute.Left
    | PageKeyPressed LeaveKey -> MsgRoute.Free
    | PageKeyPressed _ -> MsgRoute.Leaves
    | SwitchMoneyMode mode ->
        if model.TabMoney |> Option.exists (fun tab -> tab.Mode <> mode) then MsgRoute.Leaves else MsgRoute.Free
    | AssignTextChanged _
    | AssignStepped _
    | AssignFieldLeft
    | AssignDiscarded -> MsgRoute.TouchesField
    | PageEntered
    | Load
    | FrameLoaded _
    | FrameRefreshed _
    | Loaded _
    | Reloaded _
    | OpenTargets
    | PreviewLoaded _
    | TargetsApplied _
    | TargetsRetried _
    | CloseTargets
    | CategorySearchChanged _
    | ClearCategorySearch
    | SelectFilter _
    | CoverMissing
    | AssignAnswered _
    | AssignCallLost _
    | TransferAmountChanged _
    | TransferAmountReset
    | OpenPicker
    | ClosePicker
    | PickCounterpart _
    | RecentCounterpartsLoaded _
    | MoveAnswered _
    | MoveCallLost _
    | MoveRetried _
    | LoadCategoryEntries
    | CategoryEntriesLoaded _
    | LoadCategoryHistory
    | CategoryHistoryLoaded _
    | CategoryHistoryReloaded _
    | SetTargetPause _
    | TargetPauseAnswered _
    | OpenEntryInLedger _ -> MsgRoute.Free

/// Speichert die offene Eingabe, dann wirkt die Nachricht. Der Toast zur Eingabe geht vor.
let private leaveThen (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let left, outcome = leaveInspector model
    let moved, cmd, external = transition msg left
    moved, cmd, (match leaveToast outcome with NoOp -> external | toast -> toast)

/// Eine Bewegung startet nur, wenn Zuweisen ruht. Sonst ist der Tipp ein Verlassen: Die Eingabe geht in
/// die Schlange und die Bewegung startet nicht, auch nicht von selbst danach. Wer sie will, sieht die
/// neue Lage und tippt noch einmal. Fiel die Eingabe ohne Änderung weg und ruht Zuweisen, startet sie.
/// Bleibt der Tipp ohne sichtbare Wirkung, nennt ein Toast die Sperre: Der Weg über den Toast
/// "Erneut senden" hat keinen gesperrten Knopf.
let private startMove (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let blocked = ShowToast (moveBlockedText, ToastWarning)
    match moveGate model with
    | Blocked CallRunning -> model, Cmd.none, blocked
    | Blocked AssignQueued
    | SaveFirst ->
        match leaveInspector model with
        | left, Queued -> left, Cmd.none, NoOp
        | left, DroppedInvalid -> left, Cmd.none, leaveToast DroppedInvalid
        | left, (NothingOpen | Unchanged) ->
            if List.isEmpty left.AssignQueue then transition msg left else left, Cmd.none, blocked
    | Blocked ReportOpen
    | Blocked MonthChanging
    | Free -> transition msg model

/// Der Warn-Toast, wenn die gewählte Kategorie beim Neu-Lesen wegfiel.
let private droppedEntryToast =
    ShowToast ("Zugewiesen nicht gespeichert: die Kategorie gibt es nicht mehr", ToastWarning)

/// Der Einstieg. Jede Nachricht geht ihren Weg (`routeOf`), danach folgt der Tab dem Schlüssel und die
/// Schlange läuft weiter.
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let busy = inspectorBusy model
    let model', cmd, external =
        match routeOf msg model with
        | MsgRoute.StartsMove -> startMove msg model
        | MsgRoute.Left when busy -> transition msg model
        | MsgRoute.Leaves
        | MsgRoute.TouchesField when busy -> model, Cmd.none, NoOp
        | MsgRoute.Leaves
        | MsgRoute.Left -> leaveThen msg model
        | MsgRoute.TouchesField
        | MsgRoute.Free -> transition msg model
    let synced = syncMoneyTab (syncPause model')
    let external' = if entryDropped model' synced && external = NoOp then droppedEntryToast else external
    let flushed, sendCmd = flushAssign synced
    flushed, Cmd.batch [ cmd; sendCmd ], external'
