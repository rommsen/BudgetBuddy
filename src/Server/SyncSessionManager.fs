module Server.SyncSessionManager

open System
open Shared.Domain

// ============================================
// Die aktuelle Sitzung im Speicher (Single User)
// ============================================
// Seit ADR 0093 trägt die Sitzung nur noch Push-TAN und Abruf: die Buchungen stehen nach dem Abruf im
// Kassenbuch, nicht in der Sitzung. In `sync_sessions` steht jede Sitzung zusätzlich als Zustand.

let private currentSession : SyncSession option ref = ref None

/// Legt eine neue Sitzung an und macht sie zur aktuellen.
let startNewSession () : SyncSession =
    let session = {
        Id = SyncSessionId (Guid.NewGuid())
        StartedAt = DateTime.UtcNow
        CompletedAt = None
        Status = AwaitingBankAuth
        Trigger = Manual
        Counts = emptyFetchCounts
    }
    currentSession.Value <- Some session
    session

let getCurrentSession () : SyncSession option = currentSession.Value

let updateSessionStatus (status: SyncSessionStatus) : unit =
    match currentSession.Value with
    | Some session -> currentSession.Value <- Some { session with Status = status }
    | None -> failwith "No active session to update"

let clearSession () : unit = currentSession.Value <- None

/// Beendet die Sitzung mit den Zahlen des Abrufs.
let completeSession (counts: FetchCounts) : unit =
    match currentSession.Value with
    | Some session ->
        currentSession.Value <-
            Some { session with Status = Completed; CompletedAt = Some DateTime.UtcNow; Counts = counts }
    | None -> ()

let failSession (error: string) : unit =
    match currentSession.Value with
    | Some session ->
        currentSession.Value <- Some { session with Status = Failed error; CompletedAt = Some DateTime.UtcNow }
    | None -> ()

/// Die Sitzung existiert und hat diese Id.
let validateSession (sessionId: SyncSessionId) : Result<SyncSession, SyncError> =
    let (SyncSessionId id) = sessionId
    match currentSession.Value with
    | Some session when session.Id = sessionId -> Ok session
    | _ -> Error (SessionNotFound id)

/// Die Sitzung existiert, hat diese Id und steht im erwarteten Status.
let validateSessionStatus (sessionId: SyncSessionId) (expectedStatus: SyncSessionStatus) : Result<SyncSession, SyncError> =
    match validateSession sessionId with
    | Error err -> Error err
    | Ok session when session.Status <> expectedStatus ->
        Error (InvalidSessionState (sprintf "%A" expectedStatus, sprintf "%A" session.Status))
    | Ok session -> Ok session
