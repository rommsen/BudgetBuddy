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

/// Fehlertext, wenn das Lesen der Sitzung vor dem Start scheitert: gestartet wurde noch nichts.
let sessionReadFailedMessage (reason: string) : string =
    $"Der Stand des Abrufs ließ sich nicht lesen ({reason}). Es wurde nichts abgerufen. Bitte neu abrufen."

/// Die Meldung nach einem Abruf, während das Sheet zu war: Aktion „Abgerufen“ (das ✓ setzt die Leiste), darunter
/// der Titel. Ohne neue Buchung, aber mit Umsätzen in der Zukunft, sagt „Nichts Neues“ allein zu wenig; der ganze
/// Satz des Ergebnisses ist für die zweite Zeile am Handy zu lang, deshalb die kurze Form.
let resultMessage (counts: FetchCounts) : string * string =
    let line =
        match counts.NewEntries, counts.FutureDated with
        | 0, 1 -> "Nichts Neues, 1 folgt später"
        | 0, n when n > 1 -> $"Nichts Neues, {n} folgen später"
        | _ -> resultTitle counts
    "Abgerufen", line

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
        Phase = FetchStarting (currentSessionId model)
        SessionReadVersion = model.SessionReadVersion + 1 },
    Cmd.OfAsync.either (fun () -> call) () SyncStarted (fun ex -> SyncStartInterrupted ex.Message)

/// Was Roman jetzt tun oder lesen muss (Push-TAN, Fehler), holt das Sheet nach vorn, auch wenn er es
/// während des Abrufs geschlossen hat: die Push-TAN läuft sonst unbemerkt ab.
let private surfaced (model: Model) : Model = { model with IsOpen = true }

/// Ein Fehler beendet den Abruf: das Sheet zeigt ihn, offen oder wieder geöffnet.
let private failed (message: string) (model: Model) : Model =
    surfaced { model with Session = Failure message; Phase = FetchIdle }

/// Die Sitzung steht still: das Sheet zeigt TAN oder Ergebnis, der Root lädt nach einem Ende nach. War das
/// Sheet zu, öffnet es für TAN und Fehler wieder, ein Ergebnis geht an den Meldungsplatz der Seite.
let private settled (session: SyncSession) (model: Model) : Model * ExternalMsg =
    let model = { model with Session = Success (Some session); Phase = FetchIdle }
    match session.Status with
    | Completed when model.IsOpen -> model, FetchFinished None
    | Completed -> model, FetchFinished (Some (resultMessage session.Counts))
    | Failed _ -> surfaced model, FetchFinished None
    | AwaitingTan -> surfaced model, NoOp
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
            failed (interruptedStartTimeoutMessage recovery.Reason) model, Cmd.none, NoOp
        else
            let readsLeft = recovery.ReadsLeft - 1
            { model with Phase = FetchRecovering { recovery with ReadsLeft = readsLeft; ReadPlanned = true } },
            Debounce.delayed interruptedStartPollMs (InterruptedStartReadDue readsLeft),
            NoOp
    | _ ->
        // Der Start kam nie an, oder die Sitzung stammt aus einem früheren Abruf.
        failed (interruptedStartMessage recovery.Reason) model, Cmd.none, NoOp

/// Nachlesen nach verlorener Antwort: das erste Lesen geht sofort los.
let private recoverFrom (ports: FetchPorts) (reason: string) (before: SyncSessionId option) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let recovery = { Reason = reason; ReadsLeft = interruptedStartReadLimit; ReadPlanned = false; Before = before }
    let reading, cmd = readSession ports { model with Phase = FetchRecovering recovery }
    reading, cmd, NoOp

let updateWith (ports: FetchPorts) (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | Opened ->
        let model = { model with IsOpen = true }
        // Ein Auftrag, ein Start, eine TAN-Bestätigung oder ein Nachlesen läuft schon: das Sheet zeigt ihn.
        if model.Phase <> FetchIdle then
            model, Cmd.none, NoOp
        else
            let reading, cmd = readSession ports { model with Phase = FetchOrdered; Session = Loading }
            reading, cmd, NoOp

    | Closed -> { model with IsOpen = false }, Cmd.none, NoOp

    | SessionReadAnswered (version, _) when version <> model.SessionReadVersion -> model, Cmd.none, NoOp

    | SessionReadAnswered (_, answer) ->
        match model.Phase, answer with
        | FetchRecovering recovery, Ok session -> recover recovery session model
        | FetchRecovering recovery, Error _ -> failed (interruptedStartMessage recovery.Reason) model, Cmd.none, NoOp
        // Das Lesen vor dem Start scheitert: es ging kein Start verloren.
        | _, Error reason -> failed (sessionReadFailedMessage reason) model, Cmd.none, NoOp
        | phase, Ok session ->
            match phase, session with
            // Der Auftrag „Abrufen“ startet erst jetzt, und nur ohne laufende Sitzung (ADR 0071).
            | FetchOrdered, (None | Some { Status = (Completed | Failed _) }) ->
                let started, cmd = startWith ports { model with Session = Success session }
                started, cmd, NoOp
            // Eine Sitzung wartet auf die TAN: das Sheet zeigt sie, auch wenn es inzwischen zu ist.
            | _, Some ({ Status = AwaitingTan } as waiting) ->
                let model', external = settled waiting model
                model', Cmd.none, external
            | _ -> { model with Phase = FetchIdle; Session = Success session }, Cmd.none, NoOp

    | SyncStarted answer ->
        let model = { model with Phase = FetchIdle; SessionReadVersion = model.SessionReadVersion + 1 }
        match answer with
        | Ok (TanRequired session) -> surfaced { model with Session = Success (Some session) }, Cmd.none, NoOp
        | Ok (Fetched session) ->
            let model', external = settled session model
            model', Cmd.none, external
        | Error err -> failed (syncErrorText err) model, Cmd.none, FetchFinished None

    | SyncStartInterrupted reason ->
        let before = match model.Phase with FetchStarting before -> before | _ -> None
        recoverFrom ports reason before model

    | InterruptedStartReadDue readsLeft ->
        match model.Phase with
        | FetchRecovering recovery when recovery.ReadPlanned && recovery.ReadsLeft = readsLeft ->
            let reading, cmd = readSession ports { model with Phase = FetchRecovering { recovery with ReadPlanned = false } }
            reading, cmd, NoOp
        // Das Nachlesen endete inzwischen, oder der Takt gehört zu einem früheren Abbruch.
        | _ -> model, Cmd.none, NoOp

    | ConfirmTan ->
        match model.Phase, model.Session with
        | FetchIdle, Success (Some session) when session.Status = AwaitingTan ->
            let call = ports.ConfirmTan session.Id
            { model with
                Phase = FetchConfirmingTan
                SessionReadVersion = model.SessionReadVersion + 1
                Session = Success (Some { session with Status = FetchingTransactions }) },
            Cmd.OfAsync.either (fun () -> call) () TanConfirmed (fun ex -> TanConfirmInterrupted ex.Message),
            NoOp
        | _ -> model, Cmd.none, NoOp

    | TanConfirmed (Ok session) ->
        let model', external = settled session model
        model', Cmd.none, external

    | TanConfirmed (Error err) -> failed (syncErrorText err) model, Cmd.none, FetchFinished None

    | TanConfirmInterrupted reason ->
        // Die Bestätigung beendet dieselbe Sitzung: jede gelesene Sitzung ist das Ergebnis. Wartet sie
        // noch auf die TAN, kam die Bestätigung nicht an, und das Sheet zeigt die TAN wieder.
        recoverFrom ports reason None model

    | CancelSync ->
        match model.Session with
        | Success (Some session) ->
            let call = ports.CancelSync session.Id
            { model with IsOpen = false; Session = Success None; SessionReadVersion = model.SessionReadVersion + 1 },
            Cmd.OfAsync.either (fun () -> call) () SyncCancelled (fun ex -> SyncCancelled (Error (SyncError.DatabaseError ("cancel_sync", ex.Message)))),
            NoOp
        | _ -> { model with IsOpen = false }, Cmd.none, NoOp

    | SyncCancelled (Ok ()) -> model, Cmd.none, NoOp
    // Das Sheet ist schon zu, die Sitzung im Modell verworfen: kein „Erneut“, ein neuer Abruf liest sie wieder (ADR 0071).
    | SyncCancelled (Error err) -> model, Cmd.none, ShowFeedback (FeedbackError, "Nicht abgebrochen", Some (syncErrorText err))

    | ReviewRequested -> { model with IsOpen = false }, Cmd.none, OpenReview

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    updateWith livePorts msg model
