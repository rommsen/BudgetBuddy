module Server.BankImport

open System
open System.Threading
open Shared.Domain
open Server.BankPort
open Server.ComdirectClient
open Server.RulesEngine

// Der Weg von der Bank in die Prüfung (ADR 0071 Punkt 5 und 6). Der Server entscheidet über die
// TAN: bei aktiver Bankverbindung läuft der Abruf direkt in `startSyncWith`.

// ============================================
// fetchForReview: eine Stelle für den Abruf
// ============================================

type ReviewSources = {
    Bank: BankPort
    LoadRules: unit -> Async<Rule list>
    /// Buchungen der letzten n Tage. `None`: kein YNAB-Abgleich möglich, der Abruf läuft ohne weiter.
    LoadYnabTransactions: int -> Async<YnabTransaction list option>
}

type ReviewRequest = {
    AccountId: string
    DaysToFetch: int
    Now: DateTime
    Today: DateTime
}

type ReviewFetchFailure =
    | BankFetchFailed of ComdirectError
    | RulesInvalid of string

let private skipConfirmedDuplicate (tx: SyncTransaction) : SyncTransaction =
    match tx.DuplicateStatus with
    | ConfirmedDuplicate _ -> { tx with Status = Skipped }
    | _ -> tx

/// Holt die Umsätze und bereitet sie zur Prüfung auf: Regeln, Duplikate, Order-ID-Vorschläge,
/// Auto-Skip. Hängt nur an der konfigurierten Konto-Id, nicht am Kontenabruf (Live-Befund
/// `banking-import-p5mcp`: Konten lieferten 404, Umsätze kamen). Fasst die Sitzung nicht an.
let fetchForReview (sources: ReviewSources) (request: ReviewRequest) : Async<Result<SyncTransaction list, ReviewFetchFailure>> =
    async {
        match! sources.Bank.FetchTransactions request.AccountId request.DaysToFetch request.Today with
        | Error error -> return Error (BankFetchFailed error)
        | Ok bankTransactions ->
            let! rules = sources.LoadRules ()
            match classifyTransactions request.Now rules bankTransactions with
            | Error errors -> return Error (RulesInvalid (String.concat "; " errors))
            | Ok classified ->
                let! ynabTransactions = sources.LoadYnabTransactions (request.DaysToFetch + 7)
                let matched =
                    match ynabTransactions with
                    | Some known ->
                        let withDuplicates = DuplicateDetection.markDuplicates known classified
                        let orderIdMap = OrderIdMatcher.buildYnabOrderIdMap known
                        OrderIdMatcher.applySuggestions request.Now orderIdMap withDuplicates
                    | None -> classified
                return Ok (matched |> List.map skipConfirmedDuplicate)
    }

// ============================================
// startSync und confirmTan
// ============================================

/// Die Sync-Sitzung, wie der Ablauf sie braucht (live: `SyncSessionManager` und `Persistence`).
type SyncSessions = {
    Current: unit -> SyncSession option
    StartNew: unit -> SyncSession
    SetStatus: SyncSessionStatus -> unit
    Fail: string -> unit
    AddTransactions: SyncTransaction list -> unit
    ValidateAwaitingTan: SyncSessionId -> Result<unit, SyncError>
    RecordStarted: SyncSession -> Async<unit>
    RecordChanged: SyncSession -> Async<unit>
}

type ImportSettings = {
    Comdirect: ComdirectSettings option
    DaysToFetch: int
}

type ImportPorts = {
    /// Ein Start oder eine TAN-Bestätigung zur Zeit: ein zweiter `startSync` während des Abrufs
    /// wartet und bekommt dann die offene Prüfung, statt die Sitzung zu ersetzen.
    Gate: SemaphoreSlim
    Sources: ReviewSources
    Sessions: SyncSessions
    LoadSettings: unit -> Async<ImportSettings>
    DescribeBankError: ComdirectError -> string
    Clock: unit -> DateTime
    Today: unit -> DateTime
}

let private serialized (ports: ImportPorts) (work: Async<'T>) : Async<'T> =
    async {
        do! ports.Gate.WaitAsync() |> Async.AwaitTask
        try
            return! work
        finally
            ports.Gate.Release() |> ignore
    }

let private sessionNotFound (SyncSessionId id) : SyncError = SyncError.SessionNotFound id

/// `cancelSync` löscht die Sitzung, ohne auf den Abruf zu warten. Danach darf der Ablauf sie nicht
/// mehr anfassen und keine Challenge mehr starten.
let private isCurrent (ports: ImportPorts) (sessionId: SyncSessionId) : bool =
    ports.Sessions.Current () |> Option.exists (fun session -> session.Id = sessionId)

/// Eine Ausnahme beendet die Sitzung, sonst bliebe sie ohne Ausweg im Abruf stehen.
let private failingSessionOnException (ports: ImportPorts) (operation: string) (work: Async<SyncResult<'T>>) : Async<SyncResult<'T>> =
    async {
        try
            return! work
        with ex ->
            ports.Sessions.Fail ex.Message
            return Error (SyncError.DatabaseError (operation, ex.Message))
    }

type private FetchOutcome =
    | ReviewInstalled of SyncSession
    | FetchFailed of ReviewFetchFailure
    | SessionGone

let private fetchIntoSession (ports: ImportPorts) (settings: ImportSettings) (sessionId: SyncSessionId) : Async<FetchOutcome> =
    async {
        let request = {
            AccountId = settings.Comdirect |> Option.bind (fun c -> c.AccountId) |> Option.defaultValue ""
            DaysToFetch = settings.DaysToFetch
            Now = ports.Clock ()
            Today = ports.Today ()
        }
        match! fetchForReview ports.Sources request with
        | Error failure -> return FetchFailed failure
        | Ok _ when not (isCurrent ports sessionId) -> return SessionGone
        | Ok transactions ->
            ports.Sessions.AddTransactions transactions
            ports.Sessions.SetStatus ReviewingTransactions
            match ports.Sessions.Current () with
            | Some session ->
                do! ports.Sessions.RecordChanged session
                return ReviewInstalled session
            | None -> return SessionGone
    }

let private failFetch (ports: ImportPorts) (failure: ReviewFetchFailure) : SyncError =
    let message =
        match failure with
        | BankFetchFailed error -> ports.DescribeBankError error
        | RulesInvalid reason -> reason
    ports.Sessions.Fail message
    SyncError.TransactionFetchFailed message

let private requireTan (ports: ImportPorts) (credentials: ComdirectSettings) (sessionId: SyncSessionId) : Async<SyncResult<SyncStart>> =
    async {
        ports.Sessions.SetStatus AwaitingTan
        match! ports.Sources.Bank.StartAuth (fun () -> isCurrent ports sessionId) credentials with
        | Error error ->
            let message = ports.DescribeBankError error
            ports.Sessions.Fail message
            return Error (SyncError.ComdirectAuthFailed message)
        | Ok None -> return Error (sessionNotFound sessionId)
        | Ok (Some _) ->
            match ports.Sessions.Current () with
            | Some session -> return Ok (TanRequired session)
            | None -> return Error (sessionNotFound sessionId)
    }

let private fetchOrRequireTan (ports: ImportPorts) (settings: ImportSettings) (credentials: ComdirectSettings) (sessionId: SyncSessionId) : Async<SyncResult<SyncStart>> =
    async {
        ports.Sessions.SetStatus FetchingTransactions
        match! fetchIntoSession ports settings sessionId with
        | ReviewInstalled session -> return Ok (ReviewReady session)
        | SessionGone -> return Error (sessionNotFound sessionId)
        | FetchFailed failure ->
            match failure, ports.Sources.Bank.Status () with
            // Die Verbindung starb im Abruf. Roman hat den Sync ausgelöst, die Challenge ist erlaubt.
            // Ein abgebrochener Sync startet keine mehr.
            | BankFetchFailed _, BankAuthRequired when isCurrent ports sessionId ->
                return! requireTan ports credentials sessionId
            | BankFetchFailed _, BankAuthRequired ->
                return Error (sessionNotFound sessionId)
            | _ -> return Error (failFetch ports failure)
    }

/// Startet einen Sync. Eine offene Prüfung kommt unverändert zurück, sonst entsteht eine neue Sitzung.
/// Wirft etwas vor `StartNew`, bleibt eine ältere Sitzung unberührt.
let startSyncWith (ports: ImportPorts) : Async<SyncResult<SyncStart>> =
    serialized ports (async {
        try
            match ports.Sessions.Current () with
            | Some open' when open'.Status = ReviewingTransactions -> return Ok (ReviewReady open')
            | _ ->
                let! settings = ports.LoadSettings ()
                match settings.Comdirect with
                | None ->
                    return Error (SyncError.ComdirectAuthFailed "Comdirect credentials not configured. Please configure them in Settings first.")
                | Some credentials ->
                    let session = ports.Sessions.StartNew ()
                    return! failingSessionOnException ports "start_sync" (async {
                        do! ports.Sessions.RecordStarted session
                        do! ports.Sources.Bank.Resume ()
                        if isCurrent ports session.Id then
                            match ports.Sources.Bank.Status () with
                            | BankAuthRequired -> return! requireTan ports credentials session.Id
                            | BankReady -> return! fetchOrRequireTan ports settings credentials session.Id
                        else
                            return Error (sessionNotFound session.Id)
                    })
        with ex ->
            return Error (SyncError.DatabaseError ("start_sync", ex.Message))
    })

/// Schließt die Push-TAN ab und holt die Umsätze.
let confirmTanWith (ports: ImportPorts) (sessionId: SyncSessionId) : Async<SyncResult<unit>> =
    serialized ports (failingSessionOnException ports "confirm_tan" (async {
        match ports.Sessions.ValidateAwaitingTan sessionId with
        | Error err -> return Error err
        | Ok () ->
            match! ports.Sources.Bank.CompleteAuth () with
            | Error error ->
                let message = ports.DescribeBankError error
                ports.Sessions.Fail message
                return Error (SyncError.ComdirectAuthFailed message)
            | Ok () when not (isCurrent ports sessionId) -> return Error (sessionNotFound sessionId)
            | Ok () ->
                ports.Sessions.SetStatus FetchingTransactions
                let! settings = ports.LoadSettings ()
                match! fetchIntoSession ports settings sessionId with
                | ReviewInstalled _ -> return Ok ()
                | SessionGone -> return Error (sessionNotFound sessionId)
                | FetchFailed failure -> return Error (failFetch ports failure)
    }))
