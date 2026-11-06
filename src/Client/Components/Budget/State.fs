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
open Components.Budget.TargetEdit
open Components.Budget.History
open Components.Budget.TargetCheck
open Components.Budget.MonthStart
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

// Budget-Entscheidungen tragen eine Befehls-Id je Absicht (ADR 0084 Punkt 8) und laufen über
// `Retry.command`: nach Status 0 höchstens ein zweiter Versuch mit derselben Id, den der Server als
// Wiederholung erkennt. Scheitert auch er, bleibt der Weg über die Fehlernachricht. Die `*CmdWith`
// bekommen den Remoting-Aufruf als Parameter, damit ein .NET-Test sie mit einem Fake ablaufen lassen
// kann; die Produktionsvariante reicht `Api.budget` in einem Lambda durch.
let applyCmdWith
    (call: string -> TargetPreview -> Async<Result<ApplyResult, string>>)
    (preview: TargetPreview)
    : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.command (fun id -> call preview.Month { preview with CommandId = Some id }))
        ()
        TargetsApplied
        (fun ex -> TargetsApplied (Error ex.Message))

let private applyCmd (preview: TargetPreview) : Cmd<Msg> =
    applyCmdWith (fun month preview -> Api.budget.applyTargets month preview) preview

let private retryCmd (applicationId: string) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun id -> Api.budget.retryFailedTargets id)
        applicationId
        TargetsRetried
        (fun ex -> TargetsRetried (Error ex.Message))

// Wie `applyCmd` mit Befehls-Id; der Server rechnet relativ, erst die Id verhindert, dass ein zweiter
// Versuch doppelt verschiebt.
let moveCmdWith
    (call: string -> MoveRequest -> Async<Result<ApplyReport, string>>)
    (monthKey: string)
    (request: MoveRequest)
    : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.command (fun id -> call monthKey { request with CommandId = Some id }))
        ()
        MoveAnswered
        (fun ex -> MoveCallLost ex.Message)

let private moveCmd (monthKey: string) (request: MoveRequest) : Cmd<Msg> =
    moveCmdWith (fun month request -> Api.budget.moveMoney month request) monthKey request

let private retryMoveCmd (applicationId: string) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun id -> Api.budget.retryFailedTargets id)
        applicationId
        (fun result -> MoveRetried (applicationId, result))
        (fun ex -> MoveRetried (applicationId, Error ex.Message))

// ADR 0078: absolut. Jeder Eintrag der Schlange ist eine eigene Absicht mit eigener Befehls-Id, so setzt
// ein verspäteter zweiter Versuch keinen späteren Wert zurück. Der Ausgang geht an die Schlange.
let assignCmdWith
    (call: string -> AssignRequest -> Async<Result<ApplyReport, string>>)
    (write: AssignWrite)
    : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () ->
            Retry.command (fun id ->
                call write.Month { CategoryId = write.CategoryId; Assigned = write.Assigned; CommandId = Some id }))
        ()
        AssignAnswered
        (fun ex -> AssignCallLost ex.Message)

let private assignCmd (write: AssignWrite) : Cmd<Msg> =
    assignCmdWith (fun month request -> Api.budget.assignMoney month request) write

// Schreibt in BB, mit Befehls-Id wie `applyCmd`. Die Antwort trägt Kategorie und Monat des Aufrufs. Ein
// Aufruf, der auch im zweiten Versuch abbricht, ist keine Ablehnung, er hat eine eigene Nachricht
// (budgeting-r3wq8).
let pauseCmdWith
    (call: string -> TargetPauseRequest -> Async<Result<PauseSource option, string>>)
    (monthKey: string)
    (request: TargetPauseRequest)
    : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.command (fun id -> call monthKey { request with CommandId = Some id }))
        ()
        (fun result -> TargetPauseAnswered (request.CategoryId, monthKey, result))
        (fun ex -> TargetPauseCallLost (request.CategoryId, monthKey, ex.Message))

let private pauseCmd (monthKey: string) (request: TargetPauseRequest) : Cmd<Msg> =
    pauseCmdWith (fun month request -> Api.budget.setTargetPause month request) monthKey request

/// Target ändern mit Befehls-Id (budgeting-eh8xe): eine Id je Absicht, nach Status 0 einmal wiederholt
/// (`Retry.command`). Die Antwort trägt Kategorie und Monat des Aufrufs. Ein Aufruf, der auch im zweiten
/// Versuch abbricht, ist keine Ablehnung, er hat eine eigene Nachricht (wie `pauseCmdWith`).
let targetChangeCmdWith
    (call: string -> TargetChangeRequest -> Async<Result<TargetChangeResult, string>>)
    (monthKey: string)
    (request: TargetChangeRequest)
    : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.command (fun id -> call monthKey { request with CommandId = Some id }))
        ()
        (fun result -> TargetChangeAnswered (request.CategoryId, monthKey, result))
        (fun ex -> TargetChangeCallLost (request.CategoryId, monthKey, ex.Message))

let private targetChangeCmd (monthKey: string) (request: TargetChangeRequest) : Cmd<Msg> =
    targetChangeCmdWith (fun month request -> Api.budget.changeCategoryTarget month request) monthKey request

let private recentCmd () : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.reading Api.budget.getRecentMoveCategories ())
        ()
        RecentCounterpartsLoaded
        (fun ex -> RecentCounterpartsLoaded (Error ex.Message))

/// Die allgemeine Historie (budgeting-h7q4v): alle Bewegungen, lesend.
/// Die Antwort trägt die Generation beim Start: eine späte Antwort eines früheren Ladens ersetzt nichts.
let private generalHistoryCmd (generation: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.reading Api.budget.getBudgetHistory ())
        ()
        (fun result -> GeneralHistoryLoaded (generation, result))
        (fun ex -> GeneralHistoryLoaded (generation, Error ex.Message))

/// Die Seite Targets prüfen (budgeting-q4v8n): die Rohdaten des laufenden Monats, lesend. Die Antwort trägt
/// die Generation beim Start, `answer` wählt zwischen Laden und stillem Neuladen.
let private targetCheckCmd (answer: int * Result<TargetCheckData, string> -> Msg) (generation: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.reading Api.budget.getTargetCheck ())
        ()
        (fun result -> answer (generation, result))
        (fun ex -> answer (generation, Error ex.Message))

/// Das Feld Targets prüfen der Leiste (budgeting-m3x7f): dieselben Rohdaten im Hintergrund, mit der Generation beim Start.
let private monthStartCheckCmd (generation: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.reading Api.budget.getTargetCheck ())
        ()
        (fun result -> MonthStartCheckLoaded (generation, result))
        (fun ex -> MonthStartCheckLoaded (generation, Error ex.Message))

/// "Ausblenden" an der Leiste auf dem Server festhalten.
let private hideMonthStartCmd (month: string) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun key -> Api.budget.hideMonthStart key)
        month
        MonthStartHideAnswered
        (fun ex -> MonthStartHideAnswered (Error ex.Message))

/// Ein Übergang, der einen Target-Aufruf starten kann (Tab und Seite Targets prüfen): mit Auftrag geht er los.
let private sendTargetChange (model: Model, request: (string * TargetChangeRequest) option) : Model * Cmd<Msg> * ExternalMsg =
    match request with
    | Some (monthKey, request) -> model, targetChangeCmd monthKey request, NoOp
    | None -> model, Cmd.none, NoOp

/// Das Neulesen der Prüfung nach einer Target-Antwort oder einem Abbruch (`CheckRead`), mit ihrer Generation.
let private checkReadCmd (read: CheckRead option) (model: Model) : Cmd<Msg> =
    match read with
    | Some CheckLoad -> targetCheckCmd TargetCheckLoaded model.TargetCheck.Generation
    | Some CheckQuietReload -> targetCheckCmd TargetCheckReloaded model.TargetCheck.Generation
    | None -> Cmd.none

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

/// Nach einer Antwort auf Pausieren oder Aufheben (auch einem Abbruch): den Monat neu lesen, wenn
/// `reread` es verlangt, und die Historie ist wieder fällig, wenn die Antwort der gezeigten Kategorie gilt.
let private afterPauseAnswered
    (categoryId: System.Guid)
    (month: string)
    (reread: bool)
    (model: Model, external: ExternalMsg)
    : Model * Cmd<Msg> * ExternalMsg =
    let rereading, cmd =
        if reread then
            let rereading, how = rereadMonth model
            rereading, rereadCmd how rereading
        else
            model, Cmd.none
    if pauseAnswerShown categoryId month rereading then afterMovement (rereading, cmd, external)
    else rereading, cmd, external

/// Nach einer Antwort auf Target ändern (auch einem Abbruch): bei `reread` still neu lesen, bis dahin ist der
/// Fehlbetrag-Knopf gesperrt; gilt die Antwort der gezeigten Kategorie (`shown`), ist die Historie wieder fällig.
let private afterTargetAnswer (shown: bool) (reread: bool) (model: Model, external: ExternalMsg) : Model * Cmd<Msg> * ExternalMsg =
    if reread then
        let rereading, how = rereadMonth model
        let cmd = rereadCmd how rereading
        if shown then afterMovement (rereading, cmd, external) else rereading, cmd, external
    else
        model, Cmd.none, external

/// `afterMovement` nur, wenn die Antwort den Monat neu liest: In der Zuweisungs-Schlange ist das die
/// letzte Antwort, eine abgelehnte Verschiebung ändert nichts.
let private afterMovementWhen (reload: bool) (model: Model, cmd: Cmd<Msg>, external: ExternalMsg) : Model * Cmd<Msg> * ExternalMsg =
    if reload then afterMovement (model, cmd, external) else model, cmd, external

/// Ein einziges `match` ohne Sammelzweig: eine neue Nachricht ohne Zweig meldet der Compiler.
let private transition (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let quiet (model': Model, cmd: Cmd<Msg>) = model', cmd, NoOp
    match msg with
    | PageEntered ->
        // Das Feld Targets prüfen der Leiste liest bei jedem Betreten neu: nach einem Schreiben auf der Seite Targets
        // prüfen stimmt die Zahl so bei der Rückkehr.
        let checking = beginMonthStartCheck model
        let checkCmd = monthStartCheckCmd checking.MonthStartCheckGeneration
        match enterPage checking with
        | model', FreshEntry -> beginLoad model', Cmd.batch [ frameCmd (); checkCmd ], NoOp
        | model', KeptPage ->
            let rereading, reread = rereadMonth model'
            let withRequest, tabDataCmd = withTabData rereading
            withRequest, Cmd.batch [ rereadCmd reread withRequest; tabDataCmd; checkCmd ], NoOp
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
        leaveMonthStart { model with MoveInFlight = away }, Cmd.none, NoOp
    | OpenGeneralHistory -> openGeneralHistory model, Cmd.none, ShowGeneralHistory
    | GeneralHistoryEntered ->
        let entered = enterGeneralHistory model
        entered, generalHistoryCmd entered.GeneralHistory.Generation, NoOp
    | RetryGeneralHistory ->
        let retried = retryGeneralHistory model
        retried, generalHistoryCmd retried.GeneralHistory.Generation, NoOp
    | GeneralHistoryLoaded (generation, result) -> applyGeneralHistoryLoaded generation result model, Cmd.none, NoOp
    | SelectRunFilter filter -> selectRunFilter filter model, Cmd.none, NoOp
    | ToggleRun runId -> toggleRun runId model, Cmd.none, NoOp
    | LeaveGeneralHistory -> model, Cmd.none, leaveGeneralHistory model
    | GeneralHistoryAbandoned -> abandonGeneralHistory model, Cmd.none, NoOp
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
    | ToggleTargetLine categoryId -> toggleTargetLine categoryId model, Cmd.none, NoOp
    | ToggleMonthStart -> toggleMonthStart model, Cmd.none, NoOp
    | HideMonthStart ->
        match hideMonthStart model with
        | model', Some month -> model', hideMonthStartCmd month, NoOp
        | model', None -> model', Cmd.none, NoOp
    | MonthStartHideAnswered (Ok ()) -> model, Cmd.none, NoOp
    | MonthStartHideAnswered (Error message) ->
        // Auf dieser Seite bleibt die Leiste ausgeblendet; auf einem anderen Gerät stünde sie wieder.
        // Der Server meldet schon den ganzen Satz ("Ausblenden nicht gespeichert."), ein Netzfehler nur seinen Grund.
        let text = if message.StartsWith "Ausblenden" then message else $"Ausblenden nicht gespeichert: {message}"
        model, Cmd.none, ShowToast (text, ToastWarning)
    | CoverOverdrawn panel -> quiet (withTabData (coverOverdrawn panel model))
    | MonthStartCheckLoaded (generation, result) -> applyMonthStartCheck generation result model, Cmd.none, NoOp
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
    | SetTargetPause intent ->
        match startPause intent model with
        | model', Some (monthKey, request) -> model', pauseCmd monthKey request, NoOp
        | model', None -> model', Cmd.none, NoOp
    | TargetPauseAnswered (categoryId, month, result) ->
        let started = pauseStartGeneration categoryId month model
        let answered = applyPauseAnswered categoryId month result model
        match result with
        | Error message ->
            // Steht der Pause-Knopf nicht im offenen Tab, sähe niemand die Meldung: dann als Toast.
            let external =
                if pauseAnswerInTab categoryId month model then NoOp
                else ShowToast (pauseRejectedAwayText message, ToastWarning)
            answered, Cmd.none, external
        | Ok _ ->
            // Ein Lesen, das die Pause verpasst haben kann, nähme sie zurück: dann noch einmal lesen.
            afterPauseAnswered categoryId month (pauseNeedsReread month started answered) (answered, NoOp)
    | TargetPauseCallLost (categoryId, month, message) ->
        let inTab = pauseAnswerInTab categoryId month model
        let reread = pauseCallLostRereads month model
        let lost = applyPauseCallLost categoryId month message model
        // Steht der Pause-Knopf im offenen Tab, steht die Meldung dort, sonst als Toast. Der Monat wird
        // still neu gelesen, wenn er gewählt ist (`pauseCallLostRereads`).
        let external = if inTab then NoOp else ShowToast (pauseCallLostText reread message, ToastError)
        afterPauseAnswered categoryId month reread (lost, external)
    | TargetAmountChanged text -> typeTargetAmount text model, Cmd.none, NoOp
    | TargetDateChanged entry -> typeTargetDate entry model, Cmd.none, NoOp
    | TargetRhythmChanged frequency -> chooseTargetRhythm frequency model, Cmd.none, NoOp
    | TargetWeekdayChanged day -> chooseTargetWeekday day model, Cmd.none, NoOp
    | SaveTarget -> sendTargetChange (startTargetSave model)
    | ConfirmTargetChange -> sendTargetChange (confirmTargetChange model)
    | CancelTargetChange -> cancelTargetChange model, Cmd.none, NoOp
    | AskRemoveTarget -> askRemoveTarget model, Cmd.none, NoOp
    | CancelRemoveTarget -> cancelRemoveTarget model, Cmd.none, NoOp
    | ConfirmRemoveTarget -> sendTargetChange (startTargetRemove model)
    | TargetChangeAnswered (categoryId, month, result) ->
        // Die Herkunft entscheidet, welches Formular die Antwort einnimmt und wo eine Meldung steht
        // (budgeting-q4v8n). Das Target setzt jede Antwort, im geladenen Monat und auf der Seite Targets prüfen.
        let origin = targetCallOrigin categoryId month model
        let formOpen = answerFormOpen origin categoryId month model
        let shown = targetAnswerShown categoryId month model
        let name = checkCategoryName categoryId model
        let answered, reread = applyTargetAnswered categoryId month result model
        // Lief noch ein Laden der Prüfung, liest sie neu: seine Antwort kann älter sein als diese.
        let checked, checkRead = applyCheckAnswered origin categoryId month result answered
        let model', cmd, external = afterTargetAnswer shown reread (checked, targetAnswerToast origin formOpen name result)
        model', Cmd.batch [ cmd; checkReadCmd checkRead model' ], external
    | TargetChangeCallLost (categoryId, month, message) ->
        let origin = targetCallOrigin categoryId month model
        let formOpen = answerFormOpen origin categoryId month model
        let shown = targetAnswerShown categoryId month model
        let lost, reread = applyTargetCallLost categoryId month message model
        // Der Ausgang ist offen: die Prüfung liest neu, wenn sie zum Monat des Aufrufs steht oder noch lädt.
        let checked, checkRead = applyCheckCallLost origin categoryId month message lost
        // Steht das Formular offen (Tab oder Panel), steht der Satz dort, sonst als Toast.
        let external = targetCallLostToast origin formOpen checkRead.IsSome reread message
        let model', cmd, external' = afterTargetAnswer shown reread (checked, external)
        model', Cmd.batch [ cmd; checkReadCmd checkRead model' ], external'
    | OpenTargetCheck -> openTargetCheck model, Cmd.none, ShowTargetCheck
    | TargetCheckEntered ->
        let entered = enterTargetCheck model
        entered, targetCheckCmd TargetCheckLoaded entered.TargetCheck.Generation, NoOp
    | RetryTargetCheck ->
        let retried = retryTargetCheck model
        retried, targetCheckCmd TargetCheckLoaded retried.TargetCheck.Generation, NoOp
    | TargetCheckLoaded (generation, result) -> applyTargetCheckLoaded generation result model, Cmd.none, NoOp
    | TargetCheckReloaded (generation, result) ->
        let reloaded, external = applyTargetCheckReloaded generation result model
        reloaded, Cmd.none, external
    | ToggleCheckGroup group -> toggleCheckGroup group model, Cmd.none, NoOp
    | LeaveTargetCheck -> model, Cmd.none, leaveTargetCheck model
    | TargetCheckAbandoned -> abandonTargetCheck model, Cmd.none, NoOp
    | TakeSuggestion categoryId -> sendTargetChange (startTake categoryId model)
    | AdjustTarget categoryId -> openPanel categoryId model, Cmd.none, NoOp
    | CloseCheckPanel -> closePanel model, Cmd.none, NoOp
    | CheckAmountChanged text -> typeCheckAmount text model, Cmd.none, NoOp
    | CheckDateChanged entry -> typeCheckDate entry model, Cmd.none, NoOp
    | CheckRhythmChanged frequency -> chooseCheckRhythm frequency model, Cmd.none, NoOp
    | CheckWeekdayChanged day -> chooseCheckWeekday day model, Cmd.none, NoOp
    | SaveCheckTarget -> sendTargetChange (startCheckSave model)
    | ConfirmCheckTargetChange -> sendTargetChange (confirmCheckChange model)
    | CancelCheckTargetChange -> cancelCheckChange model, Cmd.none, NoOp
    | AskRemoveCheckTarget -> askRemoveCheck model, Cmd.none, NoOp
    | CancelRemoveCheckTarget -> cancelRemoveCheck model, Cmd.none, NoOp
    | ConfirmRemoveCheckTarget -> sendTargetChange (startCheckRemove model)
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
    // Am PC wählt das Feld die erste überzogene Zeile, das verlässt den Inspektor.
    | CoverOverdrawn true
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
    | OpenGeneralHistory
    | GeneralHistoryEntered
    | RetryGeneralHistory
    | GeneralHistoryLoaded _
    | SelectRunFilter _
    | ToggleRun _
    | LeaveGeneralHistory
    | GeneralHistoryAbandoned
    | Load
    | FrameLoaded _
    | FrameRefreshed _
    | Loaded _
    | Reloaded _
    | OpenTargets
    | ToggleTargetLine _
    | ToggleMonthStart
    | HideMonthStart
    | MonthStartHideAnswered _
    | CoverOverdrawn false
    | MonthStartCheckLoaded _
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
    | TargetPauseCallLost _
    | TargetAmountChanged _
    | TargetDateChanged _
    | TargetRhythmChanged _
    | TargetWeekdayChanged _
    | ConfirmTargetChange
    | CancelTargetChange
    | SaveTarget
    | AskRemoveTarget
    | ConfirmRemoveTarget
    | CancelRemoveTarget
    | TargetChangeAnswered _
    | TargetChangeCallLost _
    | OpenTargetCheck
    | TargetCheckEntered
    | RetryTargetCheck
    | TargetCheckLoaded _
    | TargetCheckReloaded _
    | ToggleCheckGroup _
    | LeaveTargetCheck
    | TargetCheckAbandoned
    | TakeSuggestion _
    | AdjustTarget _
    | CloseCheckPanel
    | CheckAmountChanged _
    | CheckDateChanged _
    | CheckRhythmChanged _
    | CheckWeekdayChanged _
    | SaveCheckTarget
    | ConfirmCheckTargetChange
    | CancelCheckTargetChange
    | AskRemoveCheckTarget
    | ConfirmRemoveCheckTarget
    | CancelRemoveCheckTarget
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
    let synced = syncTargetForm (syncMoneyTab (syncPause model'))
    let external' = if entryDropped model' synced && external = NoOp then droppedEntryToast else external
    let flushed, sendCmd = flushAssign synced
    flushed, Cmd.batch [ cmd; sendCmd ], external'
