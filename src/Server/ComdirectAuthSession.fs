module Server.ComdirectAuthSession

open System
open System.Threading
open Shared.Domain
open Server.ComdirectClient
open Server.BankConnectionStore
open Server.BankConnection

// Hält die Bankverbindung des Servers (Single-User) und serialisiert jeden Bankzugriff
// (ADR 0071 Punkt 3): ein zweiter Refresh mit demselben Token würde die Verbindung fälschlich
// beenden. Nur `startChallengeWith` und `startChallengeWhileWith` starten eine Challenge und melden
// sich mit Passwort an, und nur Handlungen von Roman rufen diese Funktionen auf. `tick` kennt ausschließlich den Refresh.

/// Zustand samt Semaphor. Geschrieben wird nur bei gehaltenem `Gate`.
type ConnectionCell = {
    State: BankConnectionState ref
    Gate: SemaphoreSlim
}

let newConnectionCell () : ConnectionCell =
    { State = ref Disconnected; Gate = new SemaphoreSlim(1, 1) }

/// Alles, was den Server verlässt, als Funktionswert. Der Bank-Port aus ADR 0071 Punkt 5 liegt
/// darüber in `BankPort.fs`.
type ConnectionPorts = {
    Cell: ConnectionCell
    Login: ComdirectSettings -> ApiKeys -> Async<ComdirectResult<AuthSession>>
    CompleteAuth: AuthSession -> ApiKeys -> Async<ComdirectResult<Tokens>>
    Refresh: ApiKeys -> DateTimeOffset -> Tokens -> Async<Result<Tokens, RefreshFailure>>
    FetchAccounts: RequestInfo -> Tokens -> Async<ComdirectResult<ComdirectAccount list>>
    FetchTransactions: RequestInfo -> Tokens -> string -> int -> DateTime -> Async<ComdirectResult<BankTransaction list>>
    /// Der Grund trägt Status und `error`-Code, nie Tokens.
    ReportLoss: string -> unit
    /// Der gespeicherte Wert aus ADR 0071 Punkt 4.
    Store: ConnectionStore
    /// Die Zugangsdaten liegen in den Einstellungen, nicht im gespeicherten Wert.
    LoadKeys: unit -> Async<ApiKeys option>
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

/// Scheitert das Speichern, geht es im Speicher weiter: das alte Token ist bei der Bank schon
/// verbraucht, ein Abbruch hier beendete die Verbindung. Das kostet beim nächsten Neustart eine TAN.
/// Der Typname reicht fürs Log, die Meldung könnte den Wert zitieren.
let private saveStored (ports: ConnectionPorts) (connection: ActiveConnection) : Async<unit> =
    async {
        try
            do! ports.Store.Save (storedConnectionOf connection.Session)
        with ex ->
            eprintfn "Bankverbindung nicht gespeichert: %s" (ex.GetType().Name)
    }

/// Für den Verlust: die Meldung darf nicht am Löschen scheitern. Bleibt der Wert liegen, scheitert
/// sein Refresh beim nächsten Start mit demselben Ergebnis.
let private discardStored (ports: ConnectionPorts) : Async<unit> =
    async {
        try
            do! ports.Store.Clear ()
        with ex ->
            eprintfn "Gespeicherte Bankverbindung nicht gelöscht: %s" (ex.GetType().Name)
    }

/// Nur bei gehaltenem `Gate` aufrufen.
let private applyRefreshFailure (ports: ConnectionPorts) (now: DateTimeOffset) (failure: RefreshFailure) : Async<unit> =
    async {
        let next, lost = refreshFailed now failure ports.Cell.State.Value
        ports.Cell.State.Value <- next
        match lost with
        | Some reason ->
            do! discardStored ports
            ports.ReportLoss reason
        | None -> ()
    }

/// Nur bei gehaltenem `Gate` aufrufen. Der Wert ist gespeichert, bevor das neue Token in Verwendung
/// geht (ADR 0071 Punkt 4).
let private refreshConnection (ports: ConnectionPorts) (now: DateTimeOffset) (connection: ActiveConnection) : Async<ActiveConnection option> =
    async {
        match! ports.Refresh connection.Keys now connection.Session.Tokens with
        | Ok tokens ->
            let refreshed = refreshSucceeded tokens connection
            do! saveStored ports refreshed
            ports.Cell.State.Value <- Connected refreshed
            return Some refreshed
        | Error failure ->
            do! applyRefreshFailure ports now failure
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
                        do! applyRefreshFailure ports now (ReloginRequired "Datenabruf nach Refresh erneut abgelehnt")
                        return Error again
                    | result -> return result
            | result -> return result
        | _ -> return Error ComdirectError.SessionExpired
    })

/// Der Wert ist unbrauchbar. Zählt als Verlust: Roman erfährt so, dass der nächste Sync eine TAN braucht.
let private dropUnusableStored (ports: ConnectionPorts) : Async<unit> =
    async {
        do! discardStored ports
        ports.ReportLoss "Gespeicherte Bankverbindung nicht lesbar"
    }

/// Ein Versuch beim Start (ADR 0071 Punkt 4), immer mit Refresh: das Refresh-Token lebt nur Minuten.
/// Weder Challenge noch Passwort-Login. Hat Roman schon eine Challenge gestartet, bleibt sie.
let resumeConnectionWith (ports: ConnectionPorts) (now: DateTimeOffset) : Async<unit> =
    locked ports (async {
        match ports.Cell.State.Value with
        | Disconnected ->
            match! ports.Store.Load () with
            | NothingStored -> ()
            | StoredUnreadable -> do! dropUnusableStored ports
            | StoredReadable stored ->
                match! ports.LoadKeys () with
                | None -> do! dropUnusableStored ports
                | Some keys ->
                    let connection = resumedConnection keys stored
                    ports.Cell.State.Value <- Connected connection
                    let! _ = refreshConnection ports now connection
                    ()
        | _ -> ()
    })

/// Nimmt einen liegenden Wert auf, bevor ein Klick über die TAN entscheidet. Sonst fiele `Disconnected`
/// direkt nach dem Start als "keine Verbindung" aus: unnötige Push-TAN, und die Challenge löscht den
/// Wert. Auch hier nur Refresh, nie Challenge oder Passwort-Login. Der Versuch ist wiederholbar, mit
/// dem des Hintergrunddiensts teilt er sich das `Gate`, der zweite findet `Connected` vor.
/// Grenze: Es gibt keine eigene Frist. Hängt die Bank, wartet der Klick bis zum Timeout des
/// `HttpClient` (100 s), danach ist das Urteil `RetryLater`. Eine Frist hier brächte nichts: Der Abruf
/// oder die Challenge dahinter warten am selben `Gate`. `cancelSync` wartet auf nichts davon.
/// Scheitert das Lesen, geht es ohne Wert weiter, ein Klick soll daran nicht hängen bleiben.
let awaitResumeWith (ports: ConnectionPorts) (now: DateTimeOffset) : Async<unit> =
    async {
        try
            do! resumeConnectionWith ports now
        with ex ->
            eprintfn "Wiederaufnahme der Bankverbindung gescheitert: %s" (ex.GetType().Name)
    }

/// Nur bei gehaltenem `Gate` aufrufen.
let private startChallenge (ports: ConnectionPorts) (credentials: ComdirectSettings) : Async<ComdirectResult<Challenge>> =
    async {
        let keys = { ClientId = credentials.ClientId; ClientSecret = credentials.ClientSecret }
        match! ports.Login credentials keys with
        | Error error -> return Error error
        | Ok session ->
            match session.Challenge with
            | None -> return Error (ComdirectError.AuthenticationFailed "No challenge received")
            | Some challenge ->
                // Die neue Anmeldung löst eine bestehende Verbindung ab, ihr Wert gilt nicht mehr. Die
                // Push-TAN ist schon unterwegs, deshalb stoppt ein gescheitertes Löschen sie nicht.
                ports.Cell.State.Value <- challengeStarted keys session
                do! discardStored ports
                return Ok challenge
    }

/// Startet eine Challenge. Eine Handlung von Roman, nie aus dem Hintergrund aufrufen. Scheitert die
/// Anmeldung, bleibt der Zustand unverändert.
let startChallengeWith (ports: ConnectionPorts) (credentials: ComdirectSettings) : Async<ComdirectResult<Challenge>> =
    locked ports (startChallenge ports credentials)

/// Wie `startChallengeWith` für den Sync, der abgebrochen werden kann. `stillWanted` läuft bei
/// gehaltenem `Gate`: Antwortet es `false`, geht nichts zur Bank (`Ok None`). Kommt der Abbruch erst
/// danach, läuft sein `discardPendingChallengeWith` hinter dieser Anmeldung und verwirft die Challenge.
let startChallengeWhileWith (ports: ConnectionPorts) (stillWanted: unit -> bool) (credentials: ComdirectSettings) : Async<ComdirectResult<Challenge option>> =
    locked ports (async {
        if stillWanted () then
            let! started = startChallenge ports credentials
            return started |> Result.map Some
        else
            return Ok None
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
                // Schon vor dem ersten Refresh: auch ein Deploy kurz nach der TAN soll keine neue kosten.
                let confirmed = tanConfirmed tokens ports.Cell.State.Value
                match confirmed with
                | Connected connection -> do! saveStored ports connection
                | _ -> ()
                ports.Cell.State.Value <- confirmed
                return Ok ()
            | Error error ->
                ports.Cell.State.Value <- tanFailed ports.Cell.State.Value
                return Error error
    })

/// Abbruch eines Syncs: eine offene Challenge verfällt, eine aktive Verbindung bleibt. Wartet nie auf
/// das `Gate`, das hält ein laufender Abruf bis zu seinem Ende. Ist es belegt, läuft das Verwerfen
/// dahinter im Hintergrund. Das `Gate` bedient Wartende der Reihe nach, ein späterer Start kommt danach.
/// Der gespeicherte Wert bleibt hier unberührt: Eine offene Challenge hat ihn schon gelöscht, und zu
/// einer aktiven Verbindung gehört er weiter.
let discardPendingChallengeWith (ports: ConnectionPorts) : Async<unit> =
    async {
        let discard () = ports.Cell.State.Value <- challengeCancelled ports.Cell.State.Value
        if ports.Cell.Gate.Wait 0 then
            try
                discard ()
            finally
                ports.Cell.Gate.Release() |> ignore
        else
            Async.Start (locked ports (async { discard () }))
    }

/// Trennt die Verbindung auf Wunsch. Kein Verlust, deshalb keine Meldung. Scheitert das Löschen,
/// kommt der Fehler an: sonst läge ein Token, das der nächste Start wieder aufnimmt.
let disconnectWith (ports: ConnectionPorts) : Async<unit> =
    locked ports (async {
        ports.Cell.State.Value <- Disconnected
        do! ports.Store.Clear ()
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
    Store = liveConnectionStore
    LoadKeys = loadLiveApiKeys
}

let resumeConnection (now: DateTimeOffset) : Async<unit> =
    resumeConnectionWith livePorts now

let confirmTan () : Async<ComdirectResult<unit>> =
    confirmChallengeWith livePorts

let discardPendingChallenge () : Async<unit> =
    discardPendingChallengeWith livePorts

let disconnect () : Async<unit> =
    disconnectWith livePorts

let runTick (now: DateTimeOffset) : Async<unit> =
    tick livePorts now
