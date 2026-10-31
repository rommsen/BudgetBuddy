module Server.ComdirectAuthSession

open System
open System.Threading
open Shared.Domain
open Server.ComdirectClient
open Server.BankConnection

// Hält die Bankverbindung des Servers (Single-User) und serialisiert jeden Bankzugriff
// (ADR 0071 Punkt 3): ein zweiter Refresh mit demselben Token würde die Verbindung fälschlich
// beenden. Nur `startChallengeWith` startet eine Challenge und meldet sich mit Passwort an, und
// nur Handlungen von Roman rufen diese Funktion auf. `tick` kennt ausschließlich den Refresh.

/// Zustand samt Semaphor. Geschrieben wird nur bei gehaltenem `Gate`.
type ConnectionCell = {
    State: BankConnectionState ref
    Gate: SemaphoreSlim
}

let newConnectionCell () : ConnectionCell =
    { State = ref Disconnected; Gate = new SemaphoreSlim(1, 1) }

/// Alles, was den Server verlässt, als Funktionswert. Der Bank-Port aus ADR 0071 Punkt 5 ist ein
/// eigener Task.
type ConnectionPorts = {
    Cell: ConnectionCell
    Login: ComdirectSettings -> ApiKeys -> Async<ComdirectResult<AuthSession>>
    CompleteAuth: AuthSession -> ApiKeys -> Async<ComdirectResult<Tokens>>
    Refresh: ApiKeys -> DateTimeOffset -> Tokens -> Async<Result<Tokens, RefreshFailure>>
    FetchAccounts: RequestInfo -> Tokens -> Async<ComdirectResult<ComdirectAccount list>>
    FetchTransactions: RequestInfo -> Tokens -> string -> int -> DateTime -> Async<ComdirectResult<BankTransaction list>>
    /// Der Grund trägt Status und `error`-Code, nie Tokens.
    ReportLoss: string -> unit
}

let connectionState (ports: ConnectionPorts) : BankConnectionState =
    ports.Cell.State.Value

let private locked (ports: ConnectionPorts) (work: Async<'T>) : Async<'T> =
    async {
        do! ports.Cell.Gate.WaitAsync() |> Async.AwaitTask
        try
            return! work
        finally
            ports.Cell.Gate.Release() |> ignore
    }

/// Nur bei gehaltenem `Gate` aufrufen.
let private applyRefreshFailure (ports: ConnectionPorts) (now: DateTimeOffset) (failure: RefreshFailure) : unit =
    let next, lost = refreshFailed now failure ports.Cell.State.Value
    ports.Cell.State.Value <- next
    lost |> Option.iter ports.ReportLoss

/// Nur bei gehaltenem `Gate` aufrufen.
let private refreshConnection (ports: ConnectionPorts) (now: DateTimeOffset) (connection: ActiveConnection) : Async<ActiveConnection option> =
    async {
        match! ports.Refresh connection.Keys now connection.Session.Tokens with
        | Ok tokens ->
            let refreshed = refreshSucceeded tokens connection
            ports.Cell.State.Value <- Connected refreshed
            return Some refreshed
        | Error failure ->
            applyRefreshFailure ports now failure
            return None
    }

/// Ein Schritt des Hintergrunddiensts. Erneuert fällige Tokens und tut sonst nichts: weder Challenge
/// noch Passwort-Login, auch nicht bei `Disconnected`.
let tick (ports: ConnectionPorts) (now: DateTimeOffset) : Async<unit> =
    locked ports (async {
        match ports.Cell.State.Value with
        | Connected connection when refreshDue now connection ->
            let! _ = refreshConnection ports now connection
            return ()
        | _ -> return ()
    })

/// 401 und 403 auf einem Datenabruf (`ComdirectClient.httpError`).
let private isAuthRejection (error: ComdirectError) : bool =
    match error with
    | ComdirectError.AuthenticationFailed _
    | ComdirectError.SessionExpired -> true
    | _ -> false

/// Bankzugriff nur aus `Connected`. Ein fälliger Refresh läuft vorher, damit gleichzeitige Anlässe
/// sein Ergebnis teilen. Antwortet die Bank 401 oder 403, folgen ein Refresh und ein Wiederholversuch.
let private withConnection (ports: ConnectionPorts) (now: DateTimeOffset) (call: ActiveConnection -> Async<ComdirectResult<'T>>) : Async<ComdirectResult<'T>> =
    locked ports (async {
        match ports.Cell.State.Value with
        | Connected connection when refreshDue now connection ->
            let! _ = refreshConnection ports now connection
            ()
        | _ -> ()

        match ports.Cell.State.Value with
        | Connected connection ->
            match! call connection with
            | Error rejection when isAuthRejection rejection ->
                match! refreshConnection ports now connection with
                | None -> return Error rejection
                | Some refreshed ->
                    match! call refreshed with
                    | Error again when isAuthRejection again ->
                        applyRefreshFailure ports now (ReloginRequired "Datenabruf nach Refresh erneut abgelehnt")
                        return Error again
                    | result -> return result
            | result -> return result
        | _ -> return Error ComdirectError.SessionExpired
    })

/// Startet eine Challenge. Eine Handlung von Roman, nie aus dem Hintergrund aufrufen. Scheitert die
/// Anmeldung, bleibt der Zustand unverändert.
let startChallengeWith (ports: ConnectionPorts) (credentials: ComdirectSettings) : Async<ComdirectResult<Challenge>> =
    locked ports (async {
        let keys = { ClientId = credentials.ClientId; ClientSecret = credentials.ClientSecret }
        match! ports.Login credentials keys with
        | Error error -> return Error error
        | Ok session ->
            match session.Challenge with
            | None -> return Error (ComdirectError.AuthenticationFailed "No challenge received")
            | Some challenge ->
                ports.Cell.State.Value <- challengeStarted keys session
                return Ok challenge
    })

/// Bestätigt die offene Challenge. Bei `Connected` geht nichts mehr zur Bank: eine weitere
/// Validierung würde auf die Sperre nach drei Fehlversuchen einzahlen.
let confirmChallengeWith (ports: ConnectionPorts) : Async<ComdirectResult<unit>> =
    locked ports (async {
        match ports.Cell.State.Value with
        | Disconnected -> return Error (ComdirectError.AuthenticationFailed "No active session")
        | Connected _ -> return Ok ()
        | ChallengePending pending ->
            match! ports.CompleteAuth pending.Session pending.Keys with
            | Ok tokens ->
                ports.Cell.State.Value <- tanConfirmed tokens ports.Cell.State.Value
                return Ok ()
            | Error error ->
                ports.Cell.State.Value <- tanFailed ports.Cell.State.Value
                return Error error
    })

/// Abbruch eines Syncs: eine offene Challenge verfällt, eine aktive Verbindung bleibt.
let discardPendingChallengeWith (ports: ConnectionPorts) : Async<unit> =
    locked ports (async {
        ports.Cell.State.Value <- challengeCancelled ports.Cell.State.Value
    })

/// Trennt die Verbindung auf Wunsch. Kein Verlust, deshalb keine Meldung.
let disconnectWith (ports: ConnectionPorts) : Async<unit> =
    locked ports (async {
        ports.Cell.State.Value <- Disconnected
    })

let fetchTransactionsWith (ports: ConnectionPorts) (now: DateTimeOffset) (accountId: string) (days: int) (today: DateTime) : Async<ComdirectResult<BankTransaction list>> =
    withConnection ports now (fun connection ->
        ports.FetchTransactions connection.Session.RequestInfo connection.Session.Tokens accountId days today)

let fetchAccountsWith (ports: ConnectionPorts) (now: DateTimeOffset) : Async<ComdirectResult<ComdirectAccount list>> =
    withConnection ports now (fun connection ->
        ports.FetchAccounts connection.Session.RequestInfo connection.Session.Tokens)

// ============================================
// Die Verbindung des laufenden Servers
// ============================================

let livePorts: ConnectionPorts = {
    Cell = newConnectionCell ()
    Login = startAuthFlow
    CompleteAuth = completeAuthFlow
    Refresh = refreshTokensLive
    FetchAccounts = getAccounts
    FetchTransactions = getTransactions
    ReportLoss = fun reason -> eprintfn "Bankverbindung verloren: %s" reason
}

let startAuth (credentials: ComdirectSettings) : Async<ComdirectResult<Challenge>> =
    startChallengeWith livePorts credentials

let confirmTan () : Async<ComdirectResult<unit>> =
    confirmChallengeWith livePorts

let discardPendingChallenge () : Async<unit> =
    discardPendingChallengeWith livePorts

let disconnect () : Async<unit> =
    disconnectWith livePorts

let fetchTransactions (accountId: string) (days: int) (today: DateTime) : Async<ComdirectResult<BankTransaction list>> =
    fetchTransactionsWith livePorts DateTimeOffset.UtcNow accountId days today

let runTick (now: DateTimeOffset) : Async<unit> =
    tick livePorts now
