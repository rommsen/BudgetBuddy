module ComdirectProbe

// Beobachtungssonde für die Comdirect-Session (banking-import-cx4b7, ADR 0071 Punkt 1). Der
// Ablauf hängt an Ports und ist ohne Bank testbar. Die echte Verdrahtung steht als Testfall in
// `ComdirectIntegrationTests.fs`, gestartet wird sie nur von Roman (banking-import-p5mcp).
//
// Start: `RUN_INTEGRATION_TESTS=true` in `.env`, dazu als Prozess-Variable `COMDIRECT_PROBE=1`
// (2 oder 3 für die Phasen) und ein gesetzter `BUDGETBUDDY_ENCRYPTION_KEY`:
//
//   COMDIRECT_PROBE=1 dotnet test src/Tests/Tests.fsproj --filter "FullyQualifiedName~Comdirect-Sonde"
//
// `COMDIRECT_PROBE` zählt bewusst nur als Prozess-Variable: ein Eintrag in `.env` bliebe stehen
// und würde später eine TAN auslösen.
//
// Phase 1: Push-TAN anfordern, auf dem Handy freigeben, danach `touch <DATA_DIR>/comdirect-probe/
// tan-bestaetigt`. Die Sonde bestätigt genau einmal und beobachtet 2 Stunden. Phase 2 setzt aus
// der verschlüsselten Datei in einem neuen Prozess fort, Phase 3 setzt den Refresh aus, bis er
// abgelehnt wird. Phase 2 und 3 starten nie eine TAN. Phase 1 verweigert den Start, solange die
// Datei `session.enc` liegt.
//
// Ergebnis steht in `<DATA_DIR>/comdirect-probe/probe.log`: nur Zeit, Status und `expires_in`.

open System
open System.IO
open Thoth.Json.Net
open Shared.Domain
open Server.ComdirectClient

type ProbePhase =
    /// Neue Anmeldung mit TAN, danach Beobachtung.
    | Observe
    /// Fortsetzen aus der Zustandsdatei in einem neuen Prozess.
    | Resume
    /// Refresh aussetzen, bis Comdirect ihn ablehnt.
    | RunToRejection

/// Beide Schalter müssen gesetzt sein. Jeder andere Wert von `COMDIRECT_PROBE` bedeutet aus.
let selectPhase (runIntegrationTests: bool) (probeSwitch: string option) : ProbePhase option =
    match runIntegrationTests, probeSwitch with
    | true, Some "1" -> Some Observe
    | true, Some "2" -> Some Resume
    | true, Some "3" -> Some RunToRejection
    | _ -> None

/// Was über Prozessgrenzen trägt. Liegt nur verschlüsselt auf der Platte.
type ProbeState = {
    RequestInfo: RequestInfo
    SessionIdentifier: string
    Tokens: Tokens
}

/// Eine Protokollzeile. Der Typ lässt keinen Platz für Tokens: `Event` ist fester Text,
/// `Status` stammt aus Status und `error`-Code.
type ProbeLogEntry = {
    At: DateTimeOffset
    Event: string
    Status: string
    ExpiresIn: int option
}

let formatLogEntry (entry: ProbeLogEntry) : string =
    let expiresIn = entry.ExpiresIn |> Option.map string |> Option.defaultValue "-"
    $"""{entry.At.ToString("o")} {entry.Event} status={entry.Status} expires_in={expiresIn}"""

type ProbePorts = {
    Now: unit -> DateTimeOffset
    Sleep: TimeSpan -> Async<unit>
    /// Löst die Push-TAN aus.
    Login: unit -> Async<ComdirectResult<AuthSession>>
    /// Zählt bei Comdirect als Validierung, läuft deshalb höchstens einmal.
    ConfirmTan: AuthSession -> Async<ComdirectResult<Tokens>>
    Refresh: Tokens -> Async<Result<Tokens, RefreshFailure>>
    FetchAccounts: ProbeState -> Async<ComdirectResult<ComdirectAccount list>>
    FetchTransactions: ProbeState -> string -> Async<ComdirectResult<BankTransaction list>>
    /// Lokale Sentinel-Datei, die Roman nach der Freigabe anlegt.
    SignalPresent: unit -> bool
    ClearSignal: unit -> unit
    EncryptionAvailable: bool
    LoadState: unit -> ProbeState option
    SaveState: ProbeState -> unit
    ClearState: unit -> unit
    Log: ProbeLogEntry -> unit
}

type ProbeOutcome =
    | Finished
    /// Comdirect hat den Refresh abgelehnt. In Phase 3 das erwartete Ergebnis.
    | SessionLost of reason: string
    /// Vor der Beobachtung abgebrochen, ohne weiteren Versuch.
    | Aborted of reason: string

// Zeitplan

let private refreshLead = TimeSpan.FromSeconds 60.
let private refreshRetry = TimeSpan.FromSeconds 60.
let private minimumRefreshGap = TimeSpan.FromSeconds 10.
let private dataInterval = TimeSpan.FromMinutes 30.
let private signalCheck = TimeSpan.FromSeconds 5.
let private signalDeadline = TimeSpan.FromMinutes 5.
let private observeDuration = TimeSpan.FromHours 2.
let private resumeDuration = TimeSpan.FromMinutes 35.
/// Wachsende Pausen: die erste abgelehnte Pause begrenzt die Laufzeit des Refresh-Tokens.
let private rejectionPauses = [ for minutes in 5.0 .. 5.0 .. 60.0 -> TimeSpan.FromMinutes minutes ]

type ProbeStep =
    | Finish
    | RefreshNow
    | FetchNow
    | WaitUntil of DateTimeOffset

/// Der Refresh geht vor dem Datenabruf, damit der Abruf das frische Token nutzt.
let nextStep (now: DateTimeOffset) (endAt: DateTimeOffset) (refreshAt: DateTimeOffset) (dataAt: DateTimeOffset) : ProbeStep =
    if now >= endAt then Finish
    elif now >= refreshAt then RefreshNow
    elif now >= dataAt then FetchNow
    else WaitUntil (List.min [ endAt; refreshAt; dataAt ])

/// Etwa 60 s vor Ablauf, mit Mindestabstand: eine sehr kurze Laufzeit darf keine Schleife auslösen.
let refreshDueAt (now: DateTimeOffset) (tokens: Tokens) : DateTimeOffset =
    let wanted = tokens.ExpiresAt |> Option.map (fun at -> at - refreshLead) |> Option.defaultValue now
    max wanted (now + minimumRefreshGap)

// Ablauf

let private errorStatus (error: ComdirectError) : string =
    match error with
    | ComdirectError.NetworkError (status, code) when isSafeErrorCode code -> $"{status}:{code}"
    | ComdirectError.NetworkError (status, _) -> string status
    | ComdirectError.AuthenticationFailed _ -> "401"
    | ComdirectError.SessionExpired -> "403"
    | ComdirectError.TanRejected -> "tan-abgelehnt"
    | ComdirectError.TanChallengeExpired -> "tan-abgelaufen"
    | ComdirectError.InvalidCredentials -> "zugangsdaten"
    | ComdirectError.InvalidResponse _ -> "nicht-lesbar"

let private logEvent (ports: ProbePorts) (event: string) (status: string) (expiresIn: int option) =
    ports.Log { At = ports.Now(); Event = event; Status = status; ExpiresIn = expiresIn }

let private expiresInSeconds (now: DateTimeOffset) (tokens: Tokens) : int option =
    tokens.ExpiresAt |> Option.map (fun at -> int (at - now).TotalSeconds)

let private fetchRound (ports: ProbePorts) (state: ProbeState) : Async<unit> =
    async {
        match! ports.FetchAccounts state with
        | Error error -> logEvent ports "konten" (errorStatus error) None
        | Ok accounts ->
            logEvent ports "konten" "ok" None
            match accounts with
            | [] -> ()
            | account :: _ ->
                match! ports.FetchTransactions state account.AccountId with
                | Ok _ -> logEvent ports "umsaetze" "ok" None
                | Error error -> logEvent ports "umsaetze" (errorStatus error) None
    }

/// Speichert vor der ersten Benutzung des neuen Tokens (ADR 0071 Punkt 4).
let private applyRefresh (ports: ProbePorts) (event: string) (state: ProbeState) (tokens: Tokens) : ProbeState =
    let refreshed = { state with Tokens = tokens }
    ports.SaveState refreshed
    logEvent ports event "ok" (expiresInSeconds (ports.Now()) tokens)
    refreshed

let private loseSession (ports: ProbePorts) (event: string) (reason: string) : ProbeOutcome =
    logEvent ports event reason None
    ports.ClearState()
    SessionLost reason

/// Refresh und Datenabruf nach Zeitplan. `RequestInfo` bleibt über den ganzen Lauf gleich.
let private observe (ports: ProbePorts) (duration: TimeSpan) (start: ProbeState) : Async<ProbeOutcome> =
    let startedAt = ports.Now()
    let endAt = startedAt + duration

    let rec loop (state: ProbeState) (refreshAt: DateTimeOffset) (dataAt: DateTimeOffset) =
        async {
            let now = ports.Now()
            match nextStep now endAt refreshAt dataAt with
            | Finish -> return Finished
            | WaitUntil until ->
                do! ports.Sleep (until - now)
                return! loop state refreshAt dataAt
            | FetchNow ->
                do! fetchRound ports state
                return! loop state refreshAt (now + dataInterval)
            | RefreshNow ->
                match! ports.Refresh state.Tokens with
                | Ok tokens ->
                    let refreshed = applyRefresh ports "refresh" state tokens
                    return! loop refreshed (refreshDueAt (ports.Now()) tokens) dataAt
                | Error (RetryLater reason) ->
                    logEvent ports "refresh-spaeter" reason None
                    return! loop state (now + refreshRetry) dataAt
                | Error (ReloginRequired reason) ->
                    return loseSession ports "refresh-abgelehnt" reason
        }

    loop start (refreshDueAt startedAt start.Tokens) startedAt

/// Kein Polling bei der Bank: geprüft wird nur die lokale Datei.
let private waitForSignal (ports: ProbePorts) : Async<bool> =
    let deadline = ports.Now() + signalDeadline

    let rec loop () =
        async {
            if ports.SignalPresent() then return true
            elif ports.Now() >= deadline then return false
            else
                do! ports.Sleep signalCheck
                return! loop ()
        }

    loop ()

let private startWithTan (ports: ProbePorts) : Async<ProbeOutcome> =
    async {
        match ports.LoadState() with
        | Some _ -> return Aborted "Zustandsdatei liegt noch: Phase 2 oder 3 nutzen oder die Datei löschen"
        | None when not ports.EncryptionAvailable ->
            return Aborted "BUDGETBUDDY_ENCRYPTION_KEY fehlt: ohne Schlüssel wäre Phase 2 nicht möglich"
        | None ->
            // Eine alte Sentinel-Datei würde zu früh bestätigen und könnte als Fehlversuch zählen.
            ports.ClearSignal()
            match! ports.Login() with
            | Error error ->
                logEvent ports "login" (errorStatus error) None
                return Aborted "Anmeldung gescheitert"
            | Ok session ->
                logEvent ports "tan-angefordert" "ok" None
                match! waitForSignal ports with
                | false ->
                    logEvent ports "tan-signal" "ausgeblieben" None
                    return Aborted "Kein Signal: TAN wurde nicht bestätigt"
                | true ->
                    match! ports.ConfirmTan session with
                    | Error error ->
                        logEvent ports "tan-bestaetigung" (errorStatus error) None
                        return Aborted "TAN-Bestätigung gescheitert, kein zweiter Versuch"
                    | Ok tokens ->
                        let state =
                            { RequestInfo = session.RequestInfo
                              SessionIdentifier = session.SessionIdentifier
                              Tokens = tokens }
                        ports.SaveState state
                        logEvent ports "tan-bestaetigung" "ok" (expiresInSeconds (ports.Now()) tokens)
                        return! observe ports observeDuration state
    }

let private runToRejection (ports: ProbePorts) (start: ProbeState) : Async<ProbeOutcome> =
    let rec refreshUntilAnswered (state: ProbeState) =
        async {
            match! ports.Refresh state.Tokens with
            | Ok tokens -> return Choice1Of2 (applyRefresh ports "refresh-nach-pause" state tokens)
            | Error (ReloginRequired reason) -> return Choice2Of2 (loseSession ports "refresh-abgelehnt" reason)
            | Error (RetryLater reason) ->
                logEvent ports "refresh-spaeter" reason None
                do! ports.Sleep refreshRetry
                return! refreshUntilAnswered state
        }

    let rec loop (state: ProbeState) (pauses: TimeSpan list) =
        async {
            match pauses with
            | [] -> return Finished
            | pause :: rest ->
                do! ports.Sleep pause
                match! refreshUntilAnswered state with
                | Choice1Of2 refreshed -> return! loop refreshed rest
                | Choice2Of2 outcome -> return outcome
        }

    loop start rejectionPauses

let run (phase: ProbePhase) (ports: ProbePorts) : Async<ProbeOutcome> =
    let fromState (continueWith: ProbeState -> Async<ProbeOutcome>) =
        match ports.LoadState() with
        | Some state -> continueWith state
        | None -> async.Return (Aborted "Keine Zustandsdatei: erst Phase 1")

    match phase with
    | Observe -> startWithTan ports
    | Resume -> fromState (observe ports resumeDuration)
    | RunToRejection -> fromState (runToRejection ports)

// Verschlüsselte Zustandsdatei

let saveEncryptedState (path: string) (state: ProbeState) : unit =
    match Persistence.Encryption.encrypt (Encode.Auto.toString(0, state)) with
    | Ok encrypted -> File.WriteAllText(path, encrypted)
    | Error _ -> failwith "Zustand ließ sich nicht verschlüsseln"

/// Eine unlesbare Datei stoppt die Sonde als Ausnahme, sie gilt nicht als fehlend: sonst
/// könnte Phase 1 dadurch eine neue TAN anfordern.
let loadEncryptedState (path: string) : ProbeState option =
    if not (File.Exists path) then
        None
    else
        match Persistence.Encryption.decrypt (File.ReadAllText path) with
        | Error _ -> failwith "Zustandsdatei ließ sich nicht entschlüsseln"
        | Ok json ->
            match Decode.Auto.fromString<ProbeState> json with
            | Ok state -> Some state
            | Error _ -> failwith "Zustandsdatei ist nicht lesbar"
