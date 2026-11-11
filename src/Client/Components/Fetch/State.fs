module Components.Fetch.State

open Elmish
open Components.Fetch.Types
open Types
open Shared.Domain
open Client

/// Die Meldung eines Fehlers im Sheet. Der Server liefert deutsche Sätze, hier kommt nichts davor.
let syncErrorText (error: SyncError) : string =
    match error with
    | SyncError.SessionNotFound _ -> "Der Abruf ist nicht mehr offen. Bitte neu abrufen."
    | SyncError.ComdirectAuthFailed reason -> reason
    | SyncError.TanTimeout -> "Die TAN wurde nicht rechtzeitig bestätigt. Bitte neu abrufen."
    | SyncError.TransactionFetchFailed message -> message
    | SyncError.YnabImportFailed (_, message) -> message
    | SyncError.InvalidSessionState _ -> "Der Abruf steht nicht mehr an dieser Stelle. Bitte neu abrufen."
    | SyncError.DatabaseError (_, message) -> message

/// Pause zwischen zwei Lesevorgängen, solange der Server nach verlorener Antwort noch abruft.
let interruptedStartPollMs = 3000

/// Frist für das Nachlesen: 40 Pausen sind 2 Minuten. Ein normaler Abruf endet weit davor.
let interruptedStartReadLimit = 40

/// Fehlertext, wenn nach verlorener Antwort kein Ergebnis auf dem Server liegt.
let interruptedStartMessage (reason: string) : string =
    $"Die Verbindung zum Server brach ab ({reason}). Bitte neu abrufen."

/// Fehlertext, wenn der Server bis zum Ablauf der Frist kein Ergebnis hat.
let interruptedStartTimeoutMessage (reason: string) : string =
    $"Die Verbindung zum Server brach ab ({reason}), und der Server hat auch nach 2 Minuten kein Ergebnis. Bitte neu abrufen."

/// Die Aufrufe des Sheets als Funktionswerte (Doktrin Punkt 1): unter .NET wirft der Remoting-Proxy im
/// Typinitialisierer. `update` schließt `livePorts` an, Tests zählen die Aufrufe. Ein Port wird schon
/// beim Bauen des Cmd aufgerufen; das Async startet erst mit dem Cmd.
type FetchPorts = {
    GetCurrentSession: unit -> Async<SyncSession option>
    StartSync: unit -> Async<Result<SyncStart, SyncError>>
    ConfirmTan: SyncSessionId -> Async<Result<SyncSession, SyncError>>
    CancelSync: SyncSessionId -> Async<Result<unit, SyncError>>
}

let livePorts : FetchPorts = {
    GetCurrentSession = fun () -> Retry.reading Api.sync.getCurrentSession ()
    StartSync = fun () -> Api.sync.startSync ()
    ConfirmTan = fun sessionId -> Api.sync.confirmTan sessionId
    CancelSync = fun sessionId -> Api.sync.cancelSync sessionId
}

let private currentSessionId (model: Model) : SyncSessionId option =
    match model.Session with
    | Success (Some session) -> Some session.Id
    | _ -> None

/// Ein Lesen der Sitzung. Ältere laufende Lesevorgänge verfallen.
let private readSession (ports: FetchPorts) (model: Model) : Model * Cmd<Msg> =
    let version = model.SessionReadVersion + 1
    let call = ports.GetCurrentSession ()
    { model with SessionReadVersion = version },
    Cmd.OfAsync.either
        (fun () -> call)
        ()
        (fun session -> SessionReadAnswered (version, Ok session))
        (fun ex -> SessionReadAnswered (version, Error ex.Message))

/// Der eine Weg zu `startSync`: Lesevorgänge von davor verfallen, ein zweiter Start wartet auf die Antwort.
let private startWith (ports: FetchPorts) (model: Model) : Model * Cmd<Msg> =
    let call = ports.StartSync ()
    { model with
        Start = StartInFlight
        StartedOver = currentSessionId model
        SessionReadVersion = model.SessionReadVersion + 1
        InterruptedStart = None },
    Cmd.OfAsync.either (fun () -> call) () SyncStarted (fun ex -> SyncStartInterrupted ex.Message)

/// Die Sitzung ist beendet: das Sheet zeigt das Ergebnis, der Root lädt nach.
let private settled (session: SyncSession) (model: Model) : Model * ExternalMsg =
    let model = { model with Session = Success (Some session); InterruptedStart = None; IsTanConfirming = false }
    match session.Status with
    | Completed | Failed _ -> model, FetchFinished
    | _ -> model, NoOp

/// Eine Antwort beim Nachlesen nach verlorener Antwort.
let private recover (recovery: StartRecovery) (session: SyncSession option) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let isOurs (s: SyncSession) = Some s.Id <> recovery.Before
    match session with
    | Some s when isOurs s && (match s.Status with Completed | Failed _ | AwaitingTan -> true | _ -> false) ->
        let model', external = settled s model
        model', Cmd.none, external
    | Some s when isOurs s ->
        // Der Server arbeitet noch. Nur lesen, nie neu starten (ADR 0071: keine Challenge ohne Roman).
        if recovery.ReadPlanned then
            model, Cmd.none, NoOp
        elif recovery.ReadsLeft <= 0 then
            { model with Session = Failure (interruptedStartTimeoutMessage recovery.Reason); InterruptedStart = None }, Cmd.none, NoOp
        else
            let readsLeft = recovery.ReadsLeft - 1
            { model with InterruptedStart = Some { recovery with ReadsLeft = readsLeft; ReadPlanned = true } },
            Debounce.delayed interruptedStartPollMs (InterruptedStartReadDue readsLeft),
            NoOp
    | _ ->
        // Der Start kam nie an, oder die Sitzung stammt aus einem früheren Abruf.
        { model with Session = Failure (interruptedStartMessage recovery.Reason); InterruptedStart = None }, Cmd.none, NoOp

let updateWith (ports: FetchPorts) (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | Opened ->
        let model = { model with IsOpen = true }
        // Ein Auftrag, ein Start, eine TAN-Bestätigung oder ein Nachlesen läuft schon: das Sheet zeigt ihn.
        if model.Start <> StartIdle || model.InterruptedStart.IsSome || model.IsTanConfirming then
            model, Cmd.none, NoOp
        else
            let reading, cmd = readSession ports { model with Start = StartOrdered; Session = Loading }
            reading, cmd, NoOp

    | Closed -> { model with IsOpen = false }, Cmd.none, NoOp

    | SessionReadAnswered (version, _) when version <> model.SessionReadVersion -> model, Cmd.none, NoOp

    | SessionReadAnswered (_, answer) ->
        match model.InterruptedStart, answer with
        | Some recovery, Ok session -> recover recovery session model
        | Some recovery, Error _ ->
            { model with Session = Failure (interruptedStartMessage recovery.Reason); InterruptedStart = None }, Cmd.none, NoOp
        | None, Error reason ->
            { model with Session = Failure (interruptedStartMessage reason); Start = StartIdle }, Cmd.none, NoOp
        | None, Ok session ->
            match model.Start, session with
            // Der Auftrag „Abrufen“ startet erst jetzt, und nur ohne laufende Sitzung (ADR 0071).
            | StartOrdered, (None | Some { Status = (Completed | Failed _) }) ->
                let started, cmd = startWith ports { model with Session = Success session }
                started, cmd, NoOp
            | _ -> { model with Start = StartIdle; Session = Success session }, Cmd.none, NoOp

    | SyncStarted answer ->
        let model = { model with Start = StartIdle; SessionReadVersion = model.SessionReadVersion + 1 }
        match answer with
        | Ok (TanRequired session) -> { model with Session = Success (Some session) }, Cmd.none, NoOp
        | Ok (Fetched session) ->
            let model', external = settled session model
            model', Cmd.none, external
        | Error err -> { model with Session = Failure (syncErrorText err) }, Cmd.none, FetchFinished

    | SyncStartInterrupted reason ->
        let recovery = { Reason = reason; ReadsLeft = interruptedStartReadLimit; ReadPlanned = false; Before = model.StartedOver }
        let reading, cmd = readSession ports { model with InterruptedStart = Some recovery; Start = StartIdle }
        reading, cmd, NoOp

    | InterruptedStartReadDue readsLeft ->
        match model.InterruptedStart with
        | Some recovery when recovery.ReadPlanned && recovery.ReadsLeft = readsLeft ->
            let reading, cmd = readSession ports { model with InterruptedStart = Some { recovery with ReadPlanned = false } }
            reading, cmd, NoOp
        // Das Nachlesen endete inzwischen, oder der Takt gehört zu einem früheren Abbruch.
        | _ -> model, Cmd.none, NoOp

    | ConfirmTan ->
        match model.Session with
        | Success (Some session) when not model.IsTanConfirming && session.Status = AwaitingTan ->
            let call = ports.ConfirmTan session.Id
            { model with
                IsTanConfirming = true
                SessionReadVersion = model.SessionReadVersion + 1
                Session = Success (Some { session with Status = FetchingTransactions }) },
            Cmd.OfAsync.either (fun () -> call) () TanConfirmed (fun ex -> TanConfirmInterrupted ex.Message),
            NoOp
        | _ -> model, Cmd.none, NoOp

    | TanConfirmed (Ok session) ->
        let model', external = settled session model
        model', Cmd.none, external

    | TanConfirmed (Error err) ->
        { model with IsTanConfirming = false; Session = Failure (syncErrorText err) }, Cmd.none, FetchFinished

    | TanConfirmInterrupted reason ->
        // Die Bestätigung beendet dieselbe Sitzung: jede gelesene Sitzung ist das Ergebnis. Wartet sie
        // noch auf die TAN, kam die Bestätigung nicht an, und das Sheet zeigt die TAN wieder.
        let recovery = { Reason = reason; ReadsLeft = interruptedStartReadLimit; ReadPlanned = false; Before = None }
        let reading, cmd = readSession ports { model with InterruptedStart = Some recovery; IsTanConfirming = false }
        reading, cmd, NoOp

    | CancelSync ->
        match model.Session with
        | Success (Some session) ->
            let call = ports.CancelSync session.Id
            { model with IsOpen = false; Session = Success None; SessionReadVersion = model.SessionReadVersion + 1 },
            Cmd.OfAsync.either (fun () -> call) () SyncCancelled (fun ex -> SyncCancelled (Error (SyncError.DatabaseError ("cancel_sync", ex.Message)))),
            NoOp
        | _ -> { model with IsOpen = false }, Cmd.none, NoOp

    | SyncCancelled (Ok ()) -> model, Cmd.none, NoOp
    | SyncCancelled (Error err) -> model, Cmd.none, ShowToast (syncErrorText err, ToastError)

    | ReviewRequested -> { model with IsOpen = false }, Cmd.none, OpenReview

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    updateWith livePorts msg model
