module Server.ComdirectAuthSession

open System
open FsToolkit.ErrorHandling
open Shared.Domain
open Server.ComdirectClient

// ============================================
// In-Memory Session Storage
// ============================================

/// Mutable reference to the current auth session (single-user app)
let private currentSession: AuthSession option ref = ref None

/// Mutable reference to API keys (loaded from settings)
let private apiKeys: ApiKeys option ref = ref None

// ============================================
// Session Management Functions
// ============================================

/// Start authentication flow and return TAN challenge info
let startAuth (credentials: ComdirectSettings) : Async<ComdirectResult<Challenge>> =
    asyncResult {
        // Create API keys from credentials
        let keys = {
            ClientId = credentials.ClientId
            ClientSecret = credentials.ClientSecret
        }

        // Store API keys for later use
        apiKeys := Some keys

        // Start the auth flow
        let! session = startAuthFlow credentials keys

        // Store session
        currentSession := Some session

        // Return challenge for UI display
        match session.Challenge with
        | Some challenge -> return challenge
        | None -> return! Error (ComdirectError.AuthenticationFailed "No challenge received")
    }

/// Confirm TAN and complete authentication (call after user confirms on phone)
let confirmTan () : Async<ComdirectResult<Tokens>> =
    asyncResult {
        match !currentSession, !apiKeys with
        | None, _ -> return! Error (ComdirectError.AuthenticationFailed "No active session")
        | _, None -> return! Error (ComdirectError.AuthenticationFailed "No API keys configured")
        | Some session, Some keys ->
            // Complete auth flow
            let! tokens = completeAuthFlow session keys

            // Update session with new tokens
            currentSession := Some { session with Tokens = tokens }

            return tokens
    }

/// Get current tokens if authenticated
let getTokens () : Tokens option =
    !currentSession |> Option.map (fun s -> s.Tokens)

/// Get current request info if session exists
let getRequestInfo () : RequestInfo option =
    !currentSession |> Option.map (fun s -> s.RequestInfo)

/// Get current session if it exists
let getCurrentSession () : AuthSession option =
    !currentSession

/// Clear the current session (logout)
let clearSession () : unit =
    currentSession := None
    apiKeys := None

/// Check if there is an active authenticated session
let isAuthenticated () : bool =
    !currentSession |> Option.isSome

/// Get session status as a string for debugging
let getSessionStatus () : string =
    match !currentSession with
    | None -> "No active session"
    | Some session ->
        match session.Challenge with
        | None -> "Session active, no challenge"
        | Some challenge -> sprintf "Waiting for TAN confirmation (Challenge: %s)" challenge.Id

// ============================================
// Transaction Fetching
// ============================================

/// Fetch transactions using the current session
let fetchTransactions (accountId: string) (days: int) : Async<ComdirectResult<BankTransaction list>> =
    asyncResult {
        match !currentSession with
        | None -> return! Error (ComdirectError.SessionExpired)
        | Some session ->
            return! getTransactions session.RequestInfo session.Tokens accountId days
    }

/// Fetch available accounts using the current session (requires completed TAN auth)
let fetchAccounts () : Async<ComdirectResult<ComdirectAccount list>> =
    asyncResult {
        match !currentSession with
        | None -> return! Error (ComdirectError.SessionExpired)
        | Some session ->
            return! getAccounts session.RequestInfo session.Tokens
    }
