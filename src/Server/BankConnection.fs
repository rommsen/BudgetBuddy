module Server.BankConnection

open System
open Server.ComdirectClient
open Server.BankConnectionStore

// Bankverbindung als reiner Zustandsautomat (ADR 0071 Punkt 2 und 3). Kein I/O, die Uhr kommt als
// Parameter. Den Zustand hält `ComdirectAuthSession`, dort liegen auch Semaphor und Bankaufrufe.

/// Abstand zwischen Ablauf des Access-Tokens und Refresh.
let refreshLeadTime = TimeSpan.FromSeconds 60.0

/// Wartezeit nach dem Urteil `RetryLater`.
let refreshRetryDelay = TimeSpan.FromSeconds 60.0

/// Takt des Hintergrunddiensts. Kürzer als `refreshLeadTime`, damit kein Fenster verpasst wird.
let tickPeriod = TimeSpan.FromSeconds 15.0

/// Offene Push-TAN: angemeldet, aber noch ohne Zugriff auf Daten.
type PendingChallenge = {
    Session: AuthSession
    Keys: ApiKeys
}

/// Bestätigte Verbindung. Nur aus diesem Zustand fließen Daten.
type ActiveConnection = {
    Session: AuthSession
    Keys: ApiKeys
    /// Nach `RetryLater` gesetzt: vorher kein planmäßiger Refresh.
    NextRefreshAttempt: DateTimeOffset option
}

type BankConnectionState =
    | Disconnected
    | ChallengePending of PendingChallenge
    | Connected of ActiveConnection

let challengeStarted (keys: ApiKeys) (session: AuthSession) : BankConnectionState =
    ChallengePending { Session = session; Keys = keys }

/// Überführt eine offene Challenge. Jeder andere Zustand bleibt, es gibt nichts zu bestätigen.
let tanConfirmed (tokens: Tokens) (state: BankConnectionState) : BankConnectionState =
    match state with
    | ChallengePending pending ->
        Connected {
            Session = { pending.Session with Tokens = tokens; Challenge = None }
            Keys = pending.Keys
            NextRefreshAttempt = None
        }
    | other -> other

/// Eine gescheiterte TAN beendet nur die offene Challenge.
let tanFailed (state: BankConnectionState) : BankConnectionState =
    match state with
    | ChallengePending _ -> Disconnected
    | other -> other

/// Abbruch eines Syncs: verwirft nur die offene Challenge, `Connected` bleibt.
let challengeCancelled (state: BankConnectionState) : BankConnectionState =
    tanFailed state

/// Verbindung aus dem gespeicherten Wert (ADR 0071 Punkt 4). Sie gilt ohne Ablaufzeit: so ist der
/// Refresh auch nach `RetryLater` nach 60 s fällig, egal wie lange das Access-Token noch liefe. Ein
/// Refresh-Token überlebt nur Minuten, auf den Ablauf des Access-Tokens zu warten kann es kosten.
let resumedConnection (keys: ApiKeys) (stored: StoredConnection) : ActiveConnection =
    { Session =
        { RequestInfo = stored.RequestInfo
          SessionId = stored.SessionId
          Tokens = { stored.Tokens with ExpiresAt = None }
          SessionIdentifier = stored.SessionIdentifier
          Challenge = None }
      Keys = keys
      NextRefreshAttempt = None }

/// Ohne bekannte Ablaufzeit ist der Refresh fällig: erst er liefert eine Ablaufzeit.
let refreshDue (now: DateTimeOffset) (connection: ActiveConnection) : bool =
    let paused =
        connection.NextRefreshAttempt
        |> Option.exists (fun attempt -> now < attempt)
    let expiring =
        match connection.Session.Tokens.ExpiresAt with
        | Some expiresAt -> expiresAt - now < refreshLeadTime
        | None -> true
    expiring && not paused

let refreshSucceeded (tokens: Tokens) (connection: ActiveConnection) : ActiveConnection =
    { connection with
        Session = { connection.Session with Tokens = tokens }
        NextRefreshAttempt = None }

/// Folge eines Refresh-Urteils. Der zweite Wert ist der Grund eines Verlusts: er entsteht nur beim
/// Übergang von `Connected` nach `Disconnected` und wird dadurch genau einmal gemeldet.
let refreshFailed (now: DateTimeOffset) (failure: RefreshFailure) (state: BankConnectionState) : BankConnectionState * string option =
    match state, failure with
    | Connected _, ReloginRequired reason -> Disconnected, Some reason
    | Connected connection, RetryLater _ ->
        Connected { connection with NextRefreshAttempt = Some (now + refreshRetryDelay) }, None
    | other, _ -> other, None
