module Server.SyncSessionManager

open System
open System.Collections.Generic
open Shared.Domain

// ============================================
// In-Memory Session State (Single User App)
// ============================================

/// Active sync session state
type SessionState = {
    Session: SyncSession
    Transactions: Dictionary<TransactionId, SyncTransaction>
}

/// Current active session (mutable, single user)
let private currentSession : SessionState option ref = ref None

// ============================================
// Session Management
// ============================================

/// Creates a new sync session and sets it as current
let startNewSession () : SyncSession =
    let session = {
        Id = SyncSessionId (Guid.NewGuid())
        StartedAt = DateTime.UtcNow
        CompletedAt = None
        Status = AwaitingBankAuth
        TransactionCount = 0
        ImportedCount = 0
        SkippedCount = 0
    }

    currentSession := Some {
        Session = session
        Transactions = Dictionary<TransactionId, SyncTransaction>()
    }

    session

/// Gets the current active session
let getCurrentSession () : SyncSession option =
    currentSession.Value |> Option.map (fun state -> state.Session)

/// Updates the current session
let updateSession (updatedSession: SyncSession) : unit =
    match currentSession.Value with
    | Some state ->
        currentSession := Some { state with Session = updatedSession }
    | None ->
        failwith "No active session to update"

/// Updates session status
let updateSessionStatus (status: SyncSessionStatus) : unit =
    match currentSession.Value with
    | Some state ->
        let updated = { state.Session with Status = status }
        currentSession := Some { state with Session = updated }
    | None ->
        failwith "No active session to update"

/// Clears the current session
let clearSession () : unit =
    currentSession := None

// ============================================
// Transaction Management
// ============================================

/// Adds transactions to the current session
let addTransactions (transactions: SyncTransaction list) : unit =
    match currentSession.Value with
    | Some state ->
        for tx in transactions do
            state.Transactions.[tx.Transaction.Id] <- tx

        let updated = {
            state.Session with
                TransactionCount = state.Transactions.Count
        }
        currentSession := Some { state with Session = updated }
    | None ->
        failwith "No active session to add transactions to"

/// Gets all transactions for the current session
let getTransactions () : SyncTransaction list =
    match currentSession.Value with
    | Some state ->
        state.Transactions.Values |> Seq.toList
    | None -> []

/// Gets a specific transaction by ID
let getTransaction (txId: TransactionId) : SyncTransaction option =
    match currentSession.Value with
    | Some state ->
        match state.Transactions.TryGetValue(txId) with
        | true, tx -> Some tx
        | false, _ -> None
    | None -> None

/// Updates a transaction in the current session
let updateTransaction (updatedTx: SyncTransaction) : unit =
    match currentSession.Value with
    | Some state ->
        state.Transactions.[updatedTx.Transaction.Id] <- updatedTx
    | None ->
        failwith "No active session to update transaction in"

/// Updates multiple transactions
let updateTransactions (updatedTxs: SyncTransaction list) : unit =
    for tx in updatedTxs do
        updateTransaction tx

/// Gets count of transactions by status
let getStatusCounts () : Map<TransactionStatus, int> =
    match currentSession.Value with
    | Some state ->
        state.Transactions.Values
        |> Seq.groupBy (fun tx -> tx.Status)
        |> Seq.map (fun (status, txs) -> status, Seq.length txs)
        |> Map.ofSeq
    | None -> Map.empty

/// Updates session with final counts
let updateSessionCounts () : unit =
    match currentSession.Value with
    | Some state ->
        let counts = getStatusCounts()
        let importedCount = counts |> Map.tryFind Imported |> Option.defaultValue 0
        let skippedCount = counts |> Map.tryFind Skipped |> Option.defaultValue 0

        let updated = {
            state.Session with
                ImportedCount = importedCount
                SkippedCount = skippedCount
        }
        currentSession := Some { state with Session = updated }
    | None -> ()

/// Completes the current session
let completeSession () : unit =
    match currentSession.Value with
    | Some _ ->
        updateSessionCounts()
        // Re-read currentSession after updateSessionCounts() to get the updated counts
        match currentSession.Value with
        | Some updatedState ->
            let completed = {
                updatedState.Session with
                    CompletedAt = Some DateTime.UtcNow
                    Status = Completed
            }
            currentSession := Some { updatedState with Session = completed }
        | None -> ()
    | None -> ()

/// Fails the current session with an error message
let failSession (error: string) : unit =
    match currentSession.Value with
    | Some state ->
        let updated = {
            state.Session with
                CompletedAt = Some DateTime.UtcNow
                Status = Failed error
        }
        currentSession := Some { state with Session = updated }
    | None -> ()

// ============================================
// Validation Helpers
// ============================================

/// Validates that a session exists and matches the expected ID
let validateSession (sessionId: SyncSessionId) : Result<SessionState, SyncError> =
    match currentSession.Value with
    | None ->
        Error (SessionNotFound (let (SyncSessionId id) = sessionId in id))
    | Some state ->
        if state.Session.Id <> sessionId then
            Error (SessionNotFound (let (SyncSessionId id) = sessionId in id))
        else
            Ok state

/// Validates that the session is in the expected status
let validateSessionStatus (sessionId: SyncSessionId) (expectedStatus: SyncSessionStatus) : Result<SessionState, SyncError> =
    match validateSession sessionId with
    | Error err -> Error err
    | Ok state ->
        if state.Session.Status <> expectedStatus then
            let expected = sprintf "%A" expectedStatus
            let actual = sprintf "%A" state.Session.Status
            Error (InvalidSessionState (expected, actual))
        else
            Ok state
