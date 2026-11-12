module Server.BankImport

open System
open System.Threading
open Shared.Domain
open Server.BankPort
open Server.ComdirectClient
open Server.RulesEngine
open Ledger.Events
open Ledger.Domain

module Ynab = Server.YnabClient
module LedgerCommands = Ledger.CommandHandlers

// Der Weg von der Bank ins Kassenbuch (ADR 0071 Punkt 5 und 6, ADR 0093). Der Server entscheidet über
// die TAN: bei aktiver Bankverbindung laufen Abruf und Buchen direkt in `startSyncWith`. Eine
// Prüfsitzung gibt es nicht mehr, jede neue Buchung steht nach dem Abruf als „zu prüfen“ im
// Kassenbuch und in YNAB, ein unsicheres Duplikat wartet im Kassenbuch.

// ============================================
// Ports
// ============================================

type FetchSources = {
    Bank: BankPort
    LoadRules: unit -> Async<Rule list>
    /// Buchungen des Import-Kontos in YNAB der letzten n Tage. `None`: kein Abgleich möglich, dann
    /// bucht der Abruf nichts (ADR 0093 Punkt 8).
    LoadYnabTransactions: int -> Async<YnabTransaction list option>
}

/// Wohin gebucht wird: das Kassenbuch-Konto des Import-Kontos, das YNAB-Konto und der Push
/// (`Ynab.sendPrepared` mit Token und Budget).
type ImportTarget = {
    LedgerAccountId: LedgerAccountId
    YnabAccountId: YnabAccountId
    Push: Ynab.PreparedTransaction list -> Async<YnabResult<Ynab.TransactionCreateResult>>
}

/// Die Sync-Sitzung, wie der Ablauf sie braucht (live: `SyncSessionManager` und `Persistence`).
type SyncSessions = {
    Current: unit -> SyncSession option
    StartNew: unit -> SyncSession
    SetStatus: SyncSessionStatus -> unit
    Fail: string -> unit
    Complete: FetchCounts -> unit
    ValidateAwaitingTan: SyncSessionId -> Result<unit, SyncError>
    RecordStarted: SyncSession -> Async<unit>
    RecordChanged: SyncSession -> Async<unit>
}

type ImportSettings = {
    Comdirect: ComdirectSettings option
    DaysToFetch: int
}

type ImportPorts = {
    /// Ein Abruf zur Zeit: Start, TAN-Bestätigung und Trennen laufen nacheinander. Ein zweiter
    /// `startSync` während eines Abrufs wartet und bekommt dessen Ergebnis.
    Gate: SemaphoreSlim
    Sources: FetchSources
    Sessions: SyncSessions
    LoadSettings: unit -> Async<ImportSettings>
    /// Das Ziel des Buchens, oder ein Satz, warum es fehlt.
    LoadTarget: unit -> Async<Result<ImportTarget, string>>
    /// Stößt nach einem gelungenen Push den Spiegel an (ADR 0014 Punkt 2).
    TriggerMirror: unit -> unit
    /// Kassenbuch zuerst, dann Push, dann Verknüpfung (ADR 0021); live
    /// `Ledger.CommandHandlers.pushComdirectImports`.
    BookImports:
        DateTime
            -> (Ynab.PreparedTransaction list -> Async<YnabResult<Ynab.TransactionCreateResult>>)
            -> (unit -> unit)
            -> LedgerAccountId
            -> LedgerCommands.ComdirectImportCandidate list
            -> Async<LedgerCommands.ComdirectPushResult>
    DescribeBankError: ComdirectError -> string
    /// Das Ergebnis des letzten Starts mit seiner Sitzung. Ein zweiter Start, der am Gate gewartet
    /// hat, bekommt daraus den Fehler mit seinem Typ statt nur der Meldung der Sitzung.
    LastStart: (SyncSessionId * SyncResult<SyncStart>) option ref
    Clock: unit -> DateTime
    /// Der Berliner Kalendertag.
    Today: unit -> DateTime
}

// ============================================
// Meldungen
// ============================================

/// Dieselbe Meldung für Sync und Verbindungstest.
let missingAccountIdMessage =
    "Keine Comdirect-Konto-Id eingetragen. Ohne sie lassen sich die Umsätze nicht lesen."

/// ADR 0093 Punkt 8: ohne YNAB-Abgleich fehlt die Duplikatprüfung.
let ynabUnavailableMessage = "YNAB ist nicht erreichbar, es wurde nichts gebucht."

/// Der Grund im Ereignis einer wartenden Buchung (ADR 0093 Punkt 3).
let possibleDuplicateReason (matched: YnabTransaction) : string =
    let payee = matched.Payee |> Option.defaultValue "ohne Payee"
    $"""In YNAB steht eine Buchung mit gleichem Betrag am {matched.Date.ToString("dd.MM.yyyy")} bei {payee}."""

// ============================================
// Planen: Kassenbuch-Abgleich, Duplikate, Order-ID (rein)
// ============================================

/// Was der Abruf mit den abgerufenen Umsätzen tut, bevor gebucht wird.
type ImportPlan = {
    /// Neue Umsätze, kategorisiert und mit Order-ID-Vorschlag; ein unsicheres Duplikat trägt
    /// `PossibleDuplicate`.
    ToBook: SyncTransaction list
    /// Schon im Kassenbuch, unverknüpft, nicht wartend und nicht verworfen: erneut senden.
    ToSendAgain: SyncTransaction list
    /// In YNAB über Reference oder `import_id` gefunden: übersprungen, keine Buchung.
    SafeDuplicatesSkipped: int
}

/// Ordnet die klassifizierten Umsätze ein. Zuerst der Abgleich mit dem Kassenbuch über die
/// `import_id` (ADR 0093 Punkt 7), danach für die neuen die Duplikaterkennung gegen YNAB und der
/// Order-ID-Vorschlag aus der YNAB-Historie. Rein.
let planImport
    (now: DateTime)
    (known: Map<string, KnownImportDecision>)
    (ynabTransactions: YnabTransaction list)
    (classified: SyncTransaction list)
    : ImportPlan =
    let decisionOf (tx: SyncTransaction) = Map.tryFind (generateImportId tx.Transaction.Id) known
    let fresh = classified |> List.filter (fun tx -> (decisionOf tx).IsNone)
    let sendAgain = classified |> List.filter (fun tx -> decisionOf tx = Some SendImportAgain)
    let marked = DuplicateDetection.markDuplicates ynabTransactions fresh
    let isSafeDuplicate (tx: SyncTransaction) =
        match tx.DuplicateStatus with
        | ConfirmedDuplicate _ -> true
        | NotDuplicate _ | PossibleDuplicate _ -> false
    let toBook =
        marked
        |> List.filter (isSafeDuplicate >> not)
        |> OrderIdMatcher.applySuggestions now (OrderIdMatcher.buildYnabOrderIdMap ynabTransactions)
    {
        ToBook = toBook
        ToSendAgain = sendAgain
        SafeDuplicatesSkipped = marked |> List.filter isSafeDuplicate |> List.length
    }

/// Das Ereignis-Payload für ein unsicheres Duplikat. `matchEntries` löst die YNAB-Id des Treffers
/// zur Buchung im Kassenbuch auf, soweit bekannt. Ein unsicheres Duplikat wartet immer, auch wenn
/// `possibleMatchOf` den Treffer der Erkennung nicht wiederfindet (Review-Fund zu banking-import-d0fey:
/// sonst ginge es still nach YNAB). Dann kommt der Schnappschuss aus den Erkennungsdetails, ohne
/// YNAB-Id (leer), und eine Log-Zeile meldet die Abweichung.
let holdOf
    (ynabTransactions: YnabTransaction list)
    (matchEntries: Map<string, EntryId>)
    (tx: SyncTransaction)
    : EntryHeldAsPossibleDuplicatePayload option =
    match tx.DuplicateStatus with
    | PossibleDuplicate (_, details) ->
        match DuplicateDetection.possibleMatchOf ynabTransactions tx.Transaction with
        | Some matched ->
            Some
                { Reason = possibleDuplicateReason matched
                  MatchYnabTransactionId = matched.Id
                  MatchEntryId = Map.tryFind matched.Id matchEntries
                  Match = {
                      Date = matched.Date
                      Amount = matched.Amount
                      Payee = matched.Payee
                      CategoryName = matched.CategoryName
                  } }
        | None ->
            eprintfn "Abruf: unsicheres Duplikat ohne Treffer in der YNAB-Liste, die Buchung wartet trotzdem"
            let snapshot : YnabTransaction =
                { Id = ""
                  Date = details.FuzzyMatchDate |> Option.defaultValue tx.Transaction.BookingDate
                  Amount =
                    details.FuzzyMatchAmount
                    |> Option.map (fun amount -> { tx.Transaction.Amount with Amount = amount })
                    |> Option.defaultValue tx.Transaction.Amount
                  Payee = details.FuzzyMatchPayee
                  Memo = None
                  ImportId = None
                  CategoryId = None
                  CategoryName = None }
            Some
                { Reason = possibleDuplicateReason snapshot
                  MatchYnabTransactionId = ""
                  MatchEntryId = None
                  Match = {
                      Date = snapshot.Date
                      Amount = snapshot.Amount
                      Payee = snapshot.Payee
                      CategoryName = None
                  } }
    | NotDuplicate _ | ConfirmedDuplicate _ -> None

/// Baut aus dem vorbereiteten Push die Kassenbuch-Kandidaten. Payee und Memo kommen aus dem
/// tatsächlich vorbereiteten Request (`p.Request`), sonst sähe der nächste Spiegel-Lauf ein falsches
/// Kopfdaten-Ereignis (ledger-qvt84); die rohe Reference bleibt in ihrem eigenen Feld. Rein.
let candidatesOf
    (holds: Map<TransactionId, EntryHeldAsPossibleDuplicatePayload>)
    (transactions: SyncTransaction list)
    (prepared: Ynab.PrepareResult)
    : LedgerCommands.ComdirectImportCandidate list =
    let byTransactionId = transactions |> List.map (fun tx -> tx.Transaction.Id, tx) |> Map.ofList
    prepared.ToSend
    |> List.map (fun p ->
        let tx = Map.find p.TransactionId byTransactionId
        ({
            Prepared = p
            ComdirectReference = tx.Transaction.Reference
            Date = tx.Transaction.BookingDate
            Amount = tx.Transaction.Amount
            Payee = Some p.Request.PayeeName
            // Der rohe Bank-Payee, unabhängig von einem Override (ledger-jkb0f).
            RawPayee = tx.Transaction.Payee
            Memo = Some p.Request.Memo
            ReviewSteps = tx.ReviewSteps
            Hold = Map.tryFind p.TransactionId holds
        } : LedgerCommands.ComdirectImportCandidate))

/// Die Zahlen des Abrufs aus Plan, Vorbereitung und Push-Ergebnis (ADR 0093 Punkt 10). Rein.
let countsOf
    (plan: ImportPlan)
    (prepared: Ynab.PrepareResult)
    (outcomes: Map<TransactionId, LedgerCommands.ComdirectPushOutcome>)
    : FetchCounts =
    let booked =
        plan.ToBook
        |> List.filter (fun tx ->
            match Map.tryFind tx.Transaction.Id outcomes with
            | Some (LedgerCommands.Linked _ | LedgerCommands.WrittenNotLinked _ | LedgerCommands.HeldAsPossibleDuplicate _) -> true
            | _ -> false)
    let countOutcomes predicate = outcomes |> Map.filter (fun _ o -> predicate o) |> Map.count
    {
        NewEntries = booked.Length
        ByRule = booked |> List.filter (fun tx -> tx.MatchedRuleId.IsSome) |> List.length
        ByOrderId = booked |> List.filter (fun tx -> tx.SuggestedByOrderId.IsSome) |> List.length
        WithoutCategory = booked |> List.filter (fun tx -> tx.CategoryId.IsNone) |> List.length
        SafeDuplicatesSkipped = plan.SafeDuplicatesSkipped
        Held = countOutcomes (function LedgerCommands.HeldAsPossibleDuplicate _ -> true | _ -> false)
        NotInYnab = countOutcomes (function LedgerCommands.WrittenNotLinked _ -> true | _ -> false)
        FutureDated = prepared.FutureDatedTransactionIds.Length
    }

// ============================================
// Abrufen und Buchen
// ============================================

type FetchRequest = {
    AccountId: string
    DaysToFetch: int
    Now: DateTime
    Today: DateTime
}

type FetchFailure =
    | BankFetchFailed of ComdirectError
    | RulesInvalid of string
    | AccountIdMissing
    | YnabUnavailable
    | TargetUnavailable of string
    /// Das Kassenbuch-Schreiben scheiterte für so viele Buchungen (`summarizeLedgerWriteFailures`).
    | BookingFailed of count: int * message: string

/// Holt die Umsätze und bucht sie (ADR 0093 Punkt 1 bis 8): Regeln, YNAB-Abgleich, Kassenbuch-
/// Abgleich, Duplikate, Order-ID, dann Kassenbuch zuerst, Push, Verknüpfung (ADR 0021). Ein
/// unsicheres Duplikat bekommt `EntryHeldAsPossibleDuplicate` statt des Pushs. `beforeBooking`
/// läuft, wenn der Abruf gelungen ist und gebucht wird; liefert es `false` (Sitzung abgebrochen),
/// bucht der Abruf nichts.
let fetchAndBook
    (ports: ImportPorts)
    (request: FetchRequest)
    (beforeBooking: unit -> bool)
    : Async<Result<FetchCounts, FetchFailure> option> =
    async {
        match! ports.Sources.Bank.FetchTransactions request.AccountId request.DaysToFetch request.Today with
        | Error error -> return Some (Error (BankFetchFailed error))
        | Ok bankTransactions ->
            let! rules = ports.Sources.LoadRules ()
            match classifyTransactions request.Now rules bankTransactions with
            | Error errors -> return Some (Error (RulesInvalid (String.concat "; " errors)))
            | Ok classified ->
                match! ports.Sources.LoadYnabTransactions (request.DaysToFetch + 7) with
                | None -> return Some (Error YnabUnavailable)
                | Some _ when not (beforeBooking ()) -> return None
                | Some ynabTransactions ->
                    match! ports.LoadTarget () with
                    | Error reason -> return Some (Error (TargetUnavailable reason))
                    | Ok target ->
                        let importIds = classified |> List.map (fun tx -> generateImportId tx.Transaction.Id)
                        let! known = LedgerCommands.knownComdirectImports importIds
                        let plan = planImport request.Now known ynabTransactions classified
                        let! matchEntries =
                            LedgerCommands.entriesOfYnabTransactions (ynabTransactions |> List.map (fun y -> y.Id))
                        let holds =
                            plan.ToBook
                            |> List.choose (fun tx -> holdOf ynabTransactions matchEntries tx |> Option.map (fun h -> tx.Transaction.Id, h))
                            |> Map.ofList
                        let toPush = plan.ToBook @ plan.ToSendAgain
                        // Ein Umsatz nach dem Berliner Tag geht noch nicht nach YNAB und nicht ins Kassenbuch,
                        // auch kein unsicheres Duplikat: YNAB nähme ihn nicht an, und beim Abruf an seinem Tag
                        // läuft die Duplikatprüfung mit dem dann aktuellen YNAB-Stand. `FutureDated` zählt ihn.
                        // Abgelehnte Übertrags-Splits gibt es hier nicht: die Regeln setzen keine Splits.
                        let prepared = Ynab.prepareTransactions request.Today target.YnabAccountId Map.empty false toPush
                        let candidates = candidatesOf holds toPush prepared
                        let! result =
                            ports.BookImports
                                request.Now
                                target.Push
                                ports.TriggerMirror
                                target.LedgerAccountId
                                candidates
                        match LedgerCommands.summarizeLedgerWriteFailures result with
                        | Some (count, message) -> return Some (Error (BookingFailed (count, message)))
                        | None -> return Some (Ok (countsOf plan prepared result.Outcomes))
    }

// ============================================
// startSync und confirmTan
// ============================================

/// Die Konto-Id, an die der Abruf geht. Ein leerer Wert ginge sonst bis zur Bank.
let private configuredAccountId (settings: ImportSettings) : string option =
    settings.Comdirect
    |> Option.bind (fun c -> c.AccountId)
    |> Option.filter (String.IsNullOrWhiteSpace >> not)

let private sessionNotFound (SyncSessionId id) : SyncError = SyncError.SessionNotFound id

/// `cancelSync` löscht die Sitzung, ohne auf den Abruf zu warten. Danach darf der Ablauf sie nicht
/// mehr anfassen und keine Challenge mehr starten.
let private isCurrent (ports: ImportPorts) (sessionId: SyncSessionId) : bool =
    ports.Sessions.Current () |> Option.exists (fun session -> session.Id = sessionId)

/// Eine Ausnahme beendet die Sitzung, sonst bliebe sie ohne Ausweg im Abruf oder im Buchen stehen.
let private failingSessionOnException (ports: ImportPorts) (operation: string) (work: Async<SyncResult<'T>>) : Async<SyncResult<'T>> =
    async {
        try
            return! work
        with ex ->
            ports.Sessions.Fail ex.Message
            match ports.Sessions.Current () with
            | Some session -> do! ports.Sessions.RecordChanged session
            | None -> ()
            return Error (SyncError.DatabaseError (operation, ex.Message))
    }

type private FetchOutcome =
    | Booked of SyncSession
    | FetchFailed of FetchFailure
    | SessionGone

let private recordCurrent (ports: ImportPorts) : Async<SyncSession option> =
    async {
        match ports.Sessions.Current () with
        | Some session ->
            do! ports.Sessions.RecordChanged session
            return Some session
        | None -> return None
    }

let private fetchIntoSession (ports: ImportPorts) (settings: ImportSettings) (sessionId: SyncSessionId) : Async<FetchOutcome> =
    async {
        match configuredAccountId settings with
        | None -> return FetchFailed AccountIdMissing
        | Some accountId ->
            let request = {
                AccountId = accountId
                DaysToFetch = settings.DaysToFetch
                Now = ports.Clock ()
                Today = ports.Today ()
            }
            let beforeBooking () =
                isCurrent ports sessionId && (ports.Sessions.SetStatus ImportingToYnab; true)
            match! fetchAndBook ports request beforeBooking with
            | None -> return SessionGone
            | Some (Error failure) -> return FetchFailed failure
            | Some (Ok counts) ->
                ports.Sessions.Complete counts
                match! recordCurrent ports with
                | Some session -> return Booked session
                | None -> return SessionGone
    }

/// Meldung für Sitzung und Historie und der Fehler für den Aufrufer.
let private describeFailure (ports: ImportPorts) (failure: FetchFailure) : string * SyncError =
    match failure with
    | BankFetchFailed error ->
        let m = ports.DescribeBankError error
        m, SyncError.TransactionFetchFailed m
    | RulesInvalid reason -> reason, SyncError.TransactionFetchFailed reason
    | AccountIdMissing -> missingAccountIdMessage, SyncError.TransactionFetchFailed missingAccountIdMessage
    | YnabUnavailable -> ynabUnavailableMessage, SyncError.YnabImportFailed (0, ynabUnavailableMessage)
    | TargetUnavailable reason -> reason, SyncError.YnabImportFailed (0, reason)
    | BookingFailed (count, reason) ->
        let entries = if count = 1 then "1 Buchung" else $"{count} Buchungen"
        let m = $"Kassenbuch-Schreiben für {entries} gescheitert: {reason}"
        m, SyncError.YnabImportFailed (count, m)

let private failFetch (ports: ImportPorts) (failure: FetchFailure) : Async<SyncError> =
    async {
        let message, error = describeFailure ports failure
        ports.Sessions.Fail message
        let! _ = recordCurrent ports
        return error
    }

let private requireTan (ports: ImportPorts) (credentials: ComdirectSettings) (sessionId: SyncSessionId) : Async<SyncResult<SyncStart>> =
    async {
        ports.Sessions.SetStatus AwaitingTan
        match! ports.Sources.Bank.StartAuth (fun () -> isCurrent ports sessionId) credentials with
        | Error error ->
            let message = ports.DescribeBankError error
            ports.Sessions.Fail message
            let! _ = recordCurrent ports
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
        | Booked session -> return Ok (Fetched session)
        | SessionGone -> return Error (sessionNotFound sessionId)
        | FetchFailed failure ->
            match failure, ports.Sources.Bank.Status () with
            // Die Verbindung starb im Abruf. Roman hat den Abruf ausgelöst, die Challenge ist erlaubt.
            // Ein abgebrochener Abruf startet keine mehr.
            | BankFetchFailed _, BankAuthRequired when isCurrent ports sessionId ->
                return! requireTan ports credentials sessionId
            | BankFetchFailed _, BankAuthRequired ->
                return Error (sessionNotFound sessionId)
            | _ ->
                let! error = failFetch ports failure
                return Error error
    }

/// Ob eine Sitzung fertig ist (beendet, gescheitert oder bei der TAN), also kein Abruf mehr an ihr läuft.
let private isSettled (status: SyncSessionStatus) : bool =
    match status with
    | Completed | Failed _ | AwaitingTan -> true
    | AwaitingBankAuth | FetchingTransactions | ImportingToYnab -> false

/// Das Ergebnis des Abrufs, auf den ein zweiter Start am Gate gewartet hat. `before` ist die Sitzung
/// beim Ankommen am Gate. War sie schon fertig und ist sie noch die aktuelle, hat der Abruf am Gate
/// keine neue Sitzung gebracht (Review-Fund zu banking-import-d0fey: sonst bekam der Start das Ergebnis
/// von vor Stunden). `None`: es gibt kein Ergebnis (auch nach einem Abbruch), dann startet der zweite
/// Start selbst. Ein Fehler behält seinen Typ aus `LastStart`.
let private resultOfAwaited (ports: ImportPorts) (before: SyncSession option) : SyncResult<SyncStart> option =
    match ports.Sessions.Current () with
    | Some session when before |> Option.exists (fun b -> b.Id = session.Id && isSettled b.Status) -> None
    | Some ({ Status = Completed } as session) -> Some (Ok (Fetched session))
    | Some ({ Status = AwaitingTan } as session) -> Some (Ok (TanRequired session))
    | Some ({ Status = Failed message } as session) ->
        match ports.LastStart.Value with
        | Some (sessionId, (Error _ as error)) when sessionId = session.Id -> Some error
        | _ -> Some (Error (SyncError.TransactionFetchFailed message))
    | _ -> None

/// Startet einen Abruf. Läuft schon einer, wartet der Start am Gate und bekommt dessen Ergebnis;
/// sonst entsteht eine neue Sitzung. Wirft etwas vor `StartNew`, bleibt eine ältere Sitzung unberührt.
let startSyncWith (ports: ImportPorts) : Async<SyncResult<SyncStart>> =
    async {
        let first = ports.Gate.Wait 0
        let before = if first then None else ports.Sessions.Current ()
        if not first then
            do! ports.Gate.WaitAsync() |> Async.AwaitTask
        try
            try
                match (if first then None else resultOfAwaited ports before) with
                | Some awaited -> return awaited
                | None ->
                    let! settings = ports.LoadSettings ()
                    match settings.Comdirect with
                    | None ->
                        return Error (SyncError.ComdirectAuthFailed "Comdirect credentials not configured. Please configure them in Settings first.")
                    // Vor der Sitzung und vor einer Challenge: eine TAN brächte hier nichts.
                    | Some _ when (configuredAccountId settings).IsNone ->
                        return Error (SyncError.TransactionFetchFailed missingAccountIdMessage)
                    | Some credentials ->
                        let session = ports.Sessions.StartNew ()
                        let! result = failingSessionOnException ports "start_sync" (async {
                            do! ports.Sessions.RecordStarted session
                            do! ports.Sources.Bank.Resume ()
                            if isCurrent ports session.Id then
                                match ports.Sources.Bank.Status () with
                                | BankAuthRequired -> return! requireTan ports credentials session.Id
                                | BankReady -> return! fetchOrRequireTan ports settings credentials session.Id
                            else
                                return Error (sessionNotFound session.Id)
                        })
                        ports.LastStart.Value <- Some (session.Id, result)
                        return result
            with ex ->
                return Error (SyncError.DatabaseError ("start_sync", ex.Message))
        finally
            ports.Gate.Release() |> ignore
    }

let private serialized (ports: ImportPorts) (work: Async<'T>) : Async<'T> =
    async {
        do! ports.Gate.WaitAsync() |> Async.AwaitTask
        try
            return! work
        finally
            ports.Gate.Release() |> ignore
    }

/// "Bank trennen" (ADR 0071). Läuft nach einem Start oder einer TAN-Bestätigung, ihr Ende ist also
/// bekannt. Eine Sitzung, die noch auf die Bank wartet, verliert hier ihre Grundlage und endet
/// sichtbar. Scheitert das Löschen, bleibt die Verbindung bestehen und die Sitzung deshalb unberührt.
/// Die Meldung nennt nur den Typ, der Text der Ausnahme könnte den gespeicherten Wert zitieren.
let disconnectBankWith (ports: ImportPorts) (endConnection: unit -> Async<unit>) : Async<SyncResult<unit>> =
    serialized ports (async {
        let! outcome = async {
            try
                do! endConnection ()
                return Ok ()
            with ex ->
                return Error (SyncError.DatabaseError ("disconnect_bank", $"Gespeicherte Bankverbindung nicht gelöscht ({ex.GetType().Name}). Bank trennen wiederholen."))
        }
        match outcome, ports.Sessions.Current () with
        | Ok (), Some { Status = (AwaitingBankAuth | AwaitingTan) } -> ports.Sessions.Fail "Bank getrennt. Sync neu starten."
        | _ -> ()
        return outcome
    })

/// Schließt die Push-TAN ab, ruft ab und bucht. Liefert die beendete Sitzung mit ihren Zahlen.
let confirmTanWith (ports: ImportPorts) (sessionId: SyncSessionId) : Async<SyncResult<SyncSession>> =
    serialized ports (failingSessionOnException ports "confirm_tan" (async {
        match ports.Sessions.ValidateAwaitingTan sessionId with
        | Error err -> return Error err
        | Ok () ->
            match! ports.Sources.Bank.CompleteAuth () with
            | Error error ->
                let message = ports.DescribeBankError error
                ports.Sessions.Fail message
                let! _ = recordCurrent ports
                return Error (SyncError.ComdirectAuthFailed message)
            | Ok () when not (isCurrent ports sessionId) -> return Error (sessionNotFound sessionId)
            | Ok () ->
                ports.Sessions.SetStatus FetchingTransactions
                let! settings = ports.LoadSettings ()
                match! fetchIntoSession ports settings sessionId with
                | Booked session -> return Ok session
                | SessionGone -> return Error (sessionNotFound sessionId)
                | FetchFailed failure ->
                    let! error = failFetch ports failure
                    return Error error
    }))

// ============================================
// Automatisch abrufen (banking-import-t4raw, ADR 0093 Punkt 9)
// ============================================

/// Schalter und Intervall des automatischen Abrufs aus den Einstellungen.
type AutoFetchSchedule = {
    Enabled: bool
    IntervalMinutes: int
}

/// Fällig ist ein Lauf, wenn der Schalter an ist und seit dem Beginn des jüngsten Abrufs (beendet oder
/// gescheitert, von Hand oder automatisch) mindestens das Intervall vergangen ist. Ohne Abruf in der
/// Historie ist er sofort fällig. Rein, beide Zeiten in UTC.
let isAutoFetchDue (now: DateTime) (schedule: AutoFetchSchedule) (latestFetch: DateTime option) : bool =
    schedule.Enabled
    && (match latestFetch with
        | None -> true
        | Some latest -> now - latest >= TimeSpan.FromMinutes (float schedule.IntervalMinutes))

/// Was ein Takt braucht: den Abruf mit seinen Ports, den Zeitplan, den Beginn des jüngsten Abrufs aus der
/// Historie (`sync_sessions`) und das Log.
type AutoFetchPorts = {
    Import: ImportPorts
    LoadSchedule: unit -> Async<AutoFetchSchedule>
    LatestFetch: unit -> Async<DateTime option>
    Log: string -> unit
}

/// Warum ein fälliger Lauf nichts tat. Ein übersprungener Lauf schreibt nichts in die Historie und bleibt fällig.
type AutoFetchSkip =
    /// Ein Abruf von Hand (oder ein Trennen, eine TAN-Bestätigung) hält das Gate.
    | FetchRunning
    /// `Disconnected` oder `ChallengePending`: ohne `Connected` kein Abruf, nie eine Challenge.
    | BankNotConnected
    /// Keine Zugangsdaten oder keine Konto-Id.
    | NotConfigured

type AutoFetchOutcome =
    | AutoFetchOff
    | AutoFetchNotDue
    | AutoFetchSkipped of AutoFetchSkip
    /// Der Lauf erreichte die Bank und steht in der Historie, beendet oder gescheitert.
    | AutoFetchRan of Result<FetchCounts, string>

/// Status und Code eines Bankfehlers fürs Log (Invariante Fehlertexte, ADR 0071 Punkt 3): nie der
/// Antwortrumpf. Ein `NetworkError` trägt schon nur den gefilterten Code, `AuthenticationFailed` schon
/// „HTTP 401“ samt Code. Rein.
let bankErrorLogDetail (error: ComdirectError) : string =
    match error with
    | ComdirectError.NetworkError (status, code) when isSafeErrorCode code -> $"HTTP {status}, Code {code}"
    | ComdirectError.NetworkError (status, _) -> $"HTTP {status}"
    | ComdirectError.AuthenticationFailed detail -> detail
    | ComdirectError.SessionExpired -> "HTTP 403"
    | ComdirectError.TanChallengeExpired -> "TAN abgelaufen"
    | ComdirectError.TanRejected -> "TAN abgelehnt"
    | ComdirectError.InvalidCredentials -> "Zugangsdaten ungültig"
    | ComdirectError.InvalidResponse _ -> "Antwort nicht lesbar"

/// Ein Takt des automatischen Abrufs. Ist ein Lauf fällig, ruft er bei `Connected` denselben Abruf wie
/// „Abrufen“ (`fetchAndBook`) und bucht damit direkt. Er startet nie eine Challenge und meldet sich nie per
/// Passwort an (ADR 0071 Punkt 2): ohne `Connected` überspringt er, stirbt die Verbindung im Abruf, endet er
/// gescheitert. Die Sitzung des Laufs steht nur in der Historie, die Sitzung im Speicher (TAN, „Abrufen“)
/// bleibt unberührt. Hält ein Abruf von Hand das Gate, wartet der Lauf nicht; „Abrufen“ dagegen wartet am
/// Gate auf einen laufenden automatischen Lauf und findet dessen Buchungen danach im Kassenbuch.
let runAutoFetchWith (ports: AutoFetchPorts) : Async<AutoFetchOutcome> =
    async {
        let! schedule = ports.LoadSchedule ()
        if not schedule.Enabled then
            return AutoFetchOff
        else
            let! latest = ports.LatestFetch ()
            let importPorts = ports.Import
            if not (isAutoFetchDue (importPorts.Clock ()) schedule latest) then
                return AutoFetchNotDue
            elif not (importPorts.Gate.Wait 0) then
                return AutoFetchSkipped FetchRunning
            else
                try
                    match importPorts.Sources.Bank.Status () with
                    | BankAuthRequired -> return AutoFetchSkipped BankNotConnected
                    | BankReady ->
                        let! settings = importPorts.LoadSettings ()
                        match settings.Comdirect, configuredAccountId settings with
                        | None, _
                        | _, None -> return AutoFetchSkipped NotConfigured
                        | Some _, Some accountId ->
                            let started = importPorts.Clock ()
                            let session = {
                                Id = SyncSessionId (Guid.NewGuid())
                                StartedAt = started
                                CompletedAt = None
                                Status = FetchingTransactions
                                Trigger = Scheduled
                                Counts = emptyFetchCounts
                            }
                            do! importPorts.Sessions.RecordStarted session
                            let finish (status: SyncSessionStatus) (counts: FetchCounts) =
                                importPorts.Sessions.RecordChanged
                                    { session with Status = status; CompletedAt = Some (importPorts.Clock ()); Counts = counts }
                            let fail (message: string) (detail: string option) =
                                async {
                                    // Bei einem Bankfehler nur Status und Code: die Meldung sagt dasselbe noch einmal.
                                    ports.Log $"Automatischer Abruf gescheitert: {detail |> Option.defaultValue message}"
                                    do! finish (Failed message) emptyFetchCounts
                                    return AutoFetchRan (Error message)
                                }
                            let request = {
                                AccountId = accountId
                                DaysToFetch = settings.DaysToFetch
                                Now = started
                                Today = importPorts.Today ()
                            }
                            let! outcome = async {
                                try
                                    let! result = fetchAndBook importPorts request (fun () -> true)
                                    return Choice1Of2 result
                                with ex ->
                                    // Nur der Typ: der Text der Ausnahme könnte Bankdaten zitieren.
                                    return Choice2Of2 (ex.GetType().Name)
                            }
                            match outcome with
                            | Choice1Of2 (Some (Ok counts)) ->
                                do! finish Completed counts
                                return AutoFetchRan (Ok counts)
                            | Choice1Of2 (Some (Error failure)) ->
                                let detail =
                                    match failure with
                                    | BankFetchFailed error -> Some (bankErrorLogDetail error)
                                    | _ -> None
                                return! fail (fst (describeFailure importPorts failure)) detail
                            // `beforeBooking` sagt immer ja, ohne Ergebnis endet der Lauf trotzdem sichtbar.
                            | Choice1Of2 None -> return! fail "Abruf ohne Ergebnis beendet" None
                            | Choice2Of2 typeName -> return! fail $"Abruf abgebrochen ({typeName})" None
                finally
                    importPorts.Gate.Release() |> ignore
    }
