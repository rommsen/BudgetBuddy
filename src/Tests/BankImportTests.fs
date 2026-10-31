module Tests.BankImportTests

// Der Server entscheidet über die TAN (banking-import-cyg94, ADR 0071 Punkt 5 und 6). Der Bank-Port
// läuft über den echten Zustandsautomaten mit Fake-Ports, kein Testfall erreicht Comdirect oder YNAB.

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Shared.Domain
open Server.ComdirectClient
open Server.BankConnectionStore
open Server.BankConnection
open Server.ComdirectAuthSession
open Server.BankPort
open Server.BankImport

let private run (work: Async<'T>) = Async.RunSynchronously work

let private now = DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)
let private today = DateTime(2026, 10, 31)
let private keys = { ClientId = "kuenstliche-id"; ClientSecret = "kuenstliches-secret" }
let private requestInfo = { RequestId = "123456789"; SessionId = "sitzung-kuenstlich" }
let private challenge = { Id = "challenge-kuenstlich"; Type = "P_TAN_PUSH" }
let private validTokens = { Access = "access"; Refresh = "refresh"; ExpiresAt = Some (now.AddSeconds 600.0) }

let private credentials : ComdirectSettings =
    { ClientId = keys.ClientId
      ClientSecret = keys.ClientSecret
      Username = "kuenstlich"
      Password = "kuenstlich"
      AccountId = Some "konfiguriertes-konto" }

let private authSession (tokens: Tokens) (pending: Challenge option) : AuthSession =
    { RequestInfo = requestInfo
      SessionId = requestInfo.SessionId
      Tokens = tokens
      SessionIdentifier = "kennung-kuenstlich"
      Challenge = pending }

let private stored : StoredConnection =
    { RequestInfo = requestInfo
      SessionId = requestInfo.SessionId
      SessionIdentifier = "kennung-kuenstlich"
      Tokens = { validTokens with Access = "gespeichert" } }

let private connected = Connected { Session = authSession validTokens None; Keys = keys; NextRefreshAttempt = None }
let private pendingChallenge = ChallengePending { Session = authSession validTokens (Some challenge); Keys = keys }

let private bankTransaction (reference: string) (payee: string) (memo: string) : BankTransaction =
    { Id = TransactionId reference
      BookingDate = DateTime(2026, 10, 30)
      Amount = { Amount = -12.34m; Currency = "EUR" }
      Payee = Some payee
      Memo = memo
      Reference = reference
      RawData = "" }

let private ynabTransaction (memo: string) (amount: decimal) (category: (Guid * string) option) : YnabTransaction =
    { Id = Guid.NewGuid().ToString()
      Date = DateTime(2026, 9, 1)
      Amount = { Amount = amount; Currency = "EUR" }
      Payee = None
      Memo = Some memo
      ImportId = None
      CategoryId = category |> Option.map (fun (id, _) -> id.ToString())
      CategoryName = category |> Option.map snd }

let private ruleFor (pattern: string) (categoryId: Guid) : Rule =
    { Id = RuleId (Guid.NewGuid())
      Name = "Regel"
      Pattern = pattern
      PatternType = Contains
      TargetField = Payee
      CategoryId = YnabCategoryId categoryId
      CategoryName = "Alltag: Lebensmittel"
      PayeeOverride = None
      Priority = 0
      Enabled = true
      CreatedAt = DateTime(2026, 1, 1)
      UpdatedAt = DateTime(2026, 1, 1) }

/// Sitzung im Speicher, wie `SyncSessionManager`. Der echte Manager ist global, Expecto läuft parallel.
type private SessionsProbe() =
    let mutable current : SyncSession option = None
    let transactions = ResizeArray<SyncTransaction>()
    member val Started = ResizeArray<SyncSession>()
    member val Changed = ResizeArray<SyncSession>()
    member val OnRecordStarted : unit -> unit = ignore with get, set
    member _.Status = current |> Option.map (fun session -> session.Status)
    member _.Transactions = List.ofSeq transactions
    member _.Clear() = current <- None
    member _.Open (status: SyncSessionStatus) (existing: SyncTransaction list) =
        let session =
            { Id = SyncSessionId (Guid.NewGuid())
              StartedAt = DateTime(2026, 10, 31)
              CompletedAt = None
              Status = status
              TransactionCount = existing.Length
              ImportedCount = 0
              SkippedCount = 0 }
        current <- Some session
        transactions.AddRange existing
        session
    member this.Build() : SyncSessions =
        let setStatus status =
            match current with
            | Some session -> current <- Some { session with Status = status }
            | None -> failwith "Keine Sitzung"
        { Current = fun () -> current
          StartNew = fun () -> this.Open AwaitingBankAuth []
          SetStatus = setStatus
          Fail = fun message -> if current.IsSome then setStatus (Failed message)
          AddTransactions = fun added ->
              transactions.AddRange added
              current <- current |> Option.map (fun s -> { s with TransactionCount = transactions.Count })
          ValidateAwaitingTan = fun sessionId ->
              match current with
              | Some session when session.Id = sessionId && session.Status = AwaitingTan -> Ok ()
              | _ -> Error (SyncError.InvalidSessionState ("AwaitingTan", "anders"))
          RecordStarted = fun session -> async {
              this.OnRecordStarted ()
              this.Started.Add session }
          RecordChanged = fun session -> async { this.Changed.Add session } }

/// Bank und Sitzung mit Zählern. Die Bankverbindung ist der echte Automat, gefälscht sind nur die
/// Aufrufe nach außen.
type private Probe(initial: BankConnectionState) =
    let mutable fetches = 0
    let mutable logins = 0
    let mutable accounts = 0
    let mutable refreshes = 0
    let mutable clears = 0
    member val Sessions = SessionsProbe()
    member val Losses = ResizeArray<string>()
    member val FetchAnswer : ComdirectResult<BankTransaction list> = Ok [ bankTransaction "ref-1" "Markt" "Karte" ] with get, set
    member val LoginAnswer : ComdirectResult<AuthSession> = Ok (authSession validTokens (Some challenge)) with get, set
    member val RefreshAnswer : Result<Tokens, RefreshFailure> = Ok validTokens with get, set
    member val ConfirmAnswer : ComdirectResult<Tokens> = Ok validTokens with get, set
    member val StoredRead : Async<StoredConnectionRead> = async.Return NothingStored with get, set
    member val DuringRefresh : unit -> Async<unit> = (fun () -> async.Return ()) with get, set
    member val DuringFetch : unit -> Async<unit> = (fun () -> async.Return ()) with get, set
    member val DuringLogin : unit -> Async<unit> = (fun () -> async.Return ()) with get, set
    member val DuringConfirm : unit -> Async<unit> = (fun () -> async.Return ()) with get, set
    member val Settings = { Comdirect = Some credentials; DaysToFetch = 14 } with get, set
    member _.Fetches = Volatile.Read(&fetches)
    member _.Logins = Volatile.Read(&logins)
    member _.AccountCalls = Volatile.Read(&accounts)
    member _.Refreshes = Volatile.Read(&refreshes)
    member _.Clears = Volatile.Read(&clears)
    member val FetchedAccountIds = ResizeArray<string>()
    member val Connection : ConnectionPorts = Unchecked.defaultof<ConnectionPorts> with get, set
    member this.Build() : ImportPorts =
        let cell = newConnectionCell ()
        cell.State.Value <- initial
        let connection : ConnectionPorts =
            { Cell = cell
              Login = fun _ _ -> async {
                  Interlocked.Increment(&logins) |> ignore
                  do! this.DuringLogin ()
                  return this.LoginAnswer }
              CompleteAuth = fun _ _ -> async {
                  do! this.DuringConfirm ()
                  return this.ConfirmAnswer }
              Refresh = fun _ _ _ -> async {
                  Interlocked.Increment(&refreshes) |> ignore
                  do! this.DuringRefresh ()
                  return this.RefreshAnswer }
              FetchAccounts = fun _ _ -> async {
                  Interlocked.Increment(&accounts) |> ignore
                  return Error (ComdirectError.NetworkError (404, "")) }
              FetchTransactions = fun _ _ accountId _ _ -> async {
                  Interlocked.Increment(&fetches) |> ignore
                  lock this.FetchedAccountIds (fun () -> this.FetchedAccountIds.Add accountId)
                  do! this.DuringFetch ()
                  return this.FetchAnswer }
              ReportLoss = this.Losses.Add
              Store =
                { Load = fun () -> this.StoredRead
                  Save = fun _ -> async.Return ()
                  Clear = fun () -> async { Interlocked.Increment(&clears) |> ignore } }
              LoadKeys = fun () -> async.Return (Some keys) }
        this.Connection <- connection
        { Gate = new SemaphoreSlim(1, 1)
          Sources =
            { Bank = comdirectBankPort connection (fun () -> now)
              LoadRules = fun () -> async.Return []
              LoadYnabTransactions = fun _ -> async.Return None }
          Sessions = this.Sessions.Build()
          LoadSettings = fun () -> async.Return this.Settings
          DescribeBankError = fun error -> $"Bankfehler: %A{error}"
          Clock = fun () -> now.UtcDateTime
          Today = fun () -> today }

let private rejected = ComdirectError.AuthenticationFailed "HTTP 401"

let private expectReviewReady (result: SyncResult<SyncStart>) =
    match result with
    | Ok (ReviewReady session) -> session
    | other -> failtestf "ReviewReady erwartet, bekam %A" other

let private expectTanRequired (result: SyncResult<SyncStart>) =
    match result with
    | Ok (TanRequired session) -> session
    | other -> failtestf "TanRequired erwartet, bekam %A" other

let private isChallengePending (ports: ConnectionPorts) =
    match connectionState ports with
    | ChallengePending _ -> true
    | _ -> false

let private isConnected (ports: ConnectionPorts) =
    match connectionState ports with
    | Connected _ -> true
    | _ -> false

let private hangLimit = TimeSpan.FromSeconds 30.0

let private isDisconnected (ports: ConnectionPorts) =
    match connectionState ports with
    | Disconnected -> true
    | _ -> false

let private expectSessionGone (result: SyncResult<'T>) =
    match result with
    | Error (SyncError.SessionNotFound _) -> ()
    | other -> failtestf "SessionNotFound erwartet: %A" other

/// `cancelSync`, wie er live läuft: echtes `Cell.Gate`, Sitzung im Speicher.
let private cancelSync (probe: Probe) (sessionId: SyncSessionId) : Task<SyncResult<unit>> =
    Server.Api.cancelSyncWith Ok (fun () -> discardPendingChallengeWith probe.Connection) probe.Sessions.Clear sessionId
    |> Async.StartAsTask

/// Eine Schranke, an der ein Fake-Bankaufruf bei gehaltenem `Cell.Gate` stehen bleibt.
type private Barrier() =
    let reached = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    let released = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    member _.Hold () : Async<unit> = async {
        reached.TrySetResult () |> ignore
        do! released.Task |> Async.AwaitTask }
    member _.WaitReached () = reached.Task.Wait hangLimit
    member _.Release () = released.SetResult ()

let private startedSessionId (probe: Probe) = (Seq.exactlyOne probe.Sessions.Started).Id

[<Tests>]
let startSyncTests =
    testList "startSync: der Server entscheidet über die TAN" [

        testCase "aktive Verbindung: ReviewReady ohne Challenge, die Buchungen liegen in der Sitzung" <| fun () ->
            let probe = Probe(connected)
            let ports = probe.Build()
            let session = startSyncWith ports |> run |> expectReviewReady
            Expect.equal session.Status ReviewingTransactions "Prüfung offen"
            Expect.equal session.TransactionCount 1 "Anzahl in der gelieferten Sitzung"
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.equal (probe.Sessions.Transactions |> List.map (fun t -> t.Transaction.Reference)) [ "ref-1" ] "Buchung installiert"
            Expect.equal probe.Sessions.Started.Count 1 "Sitzung gespeichert"
            Expect.equal (probe.Sessions.Changed |> Seq.map (fun s -> s.Status) |> List.ofSeq) [ ReviewingTransactions ] "Endstand gespeichert"

        // Live-Befund banking-import-p5mcp: Konten 404, Umsätze über die konfigurierte Konto-Id.
        testCase "der Abruf nutzt die konfigurierte Konto-Id und ruft keine Konten ab" <| fun () ->
            let probe = Probe(connected)
            startSyncWith (probe.Build()) |> run |> expectReviewReady |> ignore
            Expect.equal (List.ofSeq probe.FetchedAccountIds) [ "konfiguriertes-konto" ] "Konto aus den Einstellungen"
            Expect.equal probe.AccountCalls 0 "kein Kontenabruf"

        testCase "keine Verbindung: TanRequired, genau eine Challenge, kein Abruf" <| fun () ->
            let probe = Probe(Disconnected)
            let ports = probe.Build()
            let session = startSyncWith ports |> run |> expectTanRequired
            Expect.equal session.Status AwaitingTan "wartet auf die TAN"
            Expect.equal probe.Logins 1 "genau eine Challenge"
            Expect.equal probe.Fetches 0 "kein Abruf"
            Expect.isTrue (isChallengePending probe.Connection) "Challenge offen"

        testCase "offene Challenge gibt keine Daten frei: TanRequired statt Abruf" <| fun () ->
            let probe = Probe(pendingChallenge)
            startSyncWith (probe.Build()) |> run |> expectTanRequired |> ignore
            Expect.equal probe.Fetches 0 "kein Abruf"
            Expect.equal probe.Logins 1 "neue Challenge"

        testCase "die Verbindung stirbt im Abruf: TanRequired, genau eine Challenge, Verlust einmal gemeldet" <| fun () ->
            let probe = Probe(connected)
            probe.FetchAnswer <- Error rejected
            probe.RefreshAnswer <- Error (ReloginRequired "HTTP 401")
            let session = startSyncWith (probe.Build()) |> run |> expectTanRequired
            Expect.equal session.Status AwaitingTan "wartet auf die TAN"
            Expect.equal session.TransactionCount 0 "keine Buchungen"
            Expect.equal probe.Logins 1 "genau eine Challenge"
            Expect.equal probe.Losses.Count 1 "Verlust gemeldet"

        // `InvalidResponse` ist auch das Ende einer Blätterschleife ohne Fortschritt: kein falscher Verlust.
        for error in [ ComdirectError.NetworkError (503, ""); ComdirectError.InvalidResponse "Umsatzseite" ] do
            testCase $"ein Bankfehler bei lebender Verbindung startet keine Challenge (%A{error})" <| fun () ->
                let probe = Probe(connected)
                probe.FetchAnswer <- Error error
                match startSyncWith (probe.Build()) |> run with
                | Error (SyncError.TransactionFetchFailed _) -> ()
                | other -> failtestf "TransactionFetchFailed erwartet: %A" other
                Expect.equal probe.Logins 0 "keine Challenge"
                Expect.equal probe.Losses.Count 0 "kein Verlust gemeldet"
                Expect.isTrue (isConnected probe.Connection) "Verbindung bleibt"
                match probe.Sessions.Status with
                | Some (Failed _) -> ()
                | other -> failtestf "Sitzung gescheitert erwartet: %A" other

        testCase "abgelehnter Abruf und Refresh RetryLater: keine Challenge, die Verbindung bleibt" <| fun () ->
            let probe = Probe(connected)
            probe.FetchAnswer <- Error rejected
            probe.RefreshAnswer <- Error (RetryLater "HTTP 503")
            match startSyncWith (probe.Build()) |> run with
            | Error (SyncError.TransactionFetchFailed _) -> ()
            | other -> failtestf "TransactionFetchFailed erwartet: %A" other
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.isTrue (isConnected probe.Connection) "Verbindung bleibt"

        testCase "gescheiterte Challenge: Fehler, die Sitzung ist gescheitert" <| fun () ->
            let probe = Probe(Disconnected)
            probe.LoginAnswer <- Error ComdirectError.InvalidCredentials
            match startSyncWith (probe.Build()) |> run with
            | Error (SyncError.ComdirectAuthFailed _) -> ()
            | other -> failtestf "ComdirectAuthFailed erwartet: %A" other
            match probe.Sessions.Status with
            | Some (Failed _) -> ()
            | other -> failtestf "Sitzung gescheitert erwartet: %A" other

        testCase "offene Prüfung: dieselbe Sitzung, Buchungen unverändert, kein Bankzugriff" <| fun () ->
            let probe = Probe(connected)
            let reviewed =
                { Transaction = bankTransaction "bleibt" "Markt" "Karte"
                  Status = ManualCategorized
                  CategoryId = None
                  CategoryName = None
                  MatchedRuleId = None
                  PayeeOverride = None
                  ExternalLinks = []
                  UserNotes = None
                  DuplicateStatus = NotDuplicate { TransactionReference = "bleibt"; ReferenceFoundInYnab = false; ImportIdFoundInYnab = false; FuzzyMatchDate = None; FuzzyMatchAmount = None; FuzzyMatchPayee = None }
                  YnabImportStatus = NotAttempted
                  Splits = None
                  SuggestedByOrderId = None
                  ReviewSteps = []
                  AskLena = false }
            let opened = probe.Sessions.Open ReviewingTransactions [ reviewed ]
            let session = startSyncWith (probe.Build()) |> run |> expectReviewReady
            Expect.equal session opened "dieselbe Sitzung"
            Expect.equal probe.Sessions.Transactions [ reviewed ] "Buchungen unverändert"
            Expect.equal probe.Fetches 0 "kein Abruf"
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.equal probe.Sessions.Started.Count 0 "keine neue Sitzung"

        testCase "offene Prüfung ohne Zugangsdaten: die Prüfung kommt trotzdem zurück" <| fun () ->
            let probe = Probe(Disconnected)
            probe.Settings <- { probe.Settings with Comdirect = None }
            let opened = probe.Sessions.Open ReviewingTransactions []
            let session = startSyncWith (probe.Build()) |> run |> expectReviewReady
            Expect.equal session opened "dieselbe Sitzung"

        testCase "eine abgeschlossene Sitzung blockiert keinen neuen Sync" <| fun () ->
            let probe = Probe(connected)
            let finished = probe.Sessions.Open Completed []
            let session = startSyncWith (probe.Build()) |> run |> expectReviewReady
            Expect.notEqual session.Id finished.Id "neue Sitzung"

        testCase "ohne Zugangsdaten: Fehler, keine Sitzung, keine Challenge" <| fun () ->
            let probe = Probe(Disconnected)
            probe.Settings <- { probe.Settings with Comdirect = None }
            match startSyncWith (probe.Build()) |> run with
            | Error (SyncError.ComdirectAuthFailed _) -> ()
            | other -> failtestf "ComdirectAuthFailed erwartet: %A" other
            Expect.equal probe.Sessions.Started.Count 0 "keine Sitzung"
            Expect.equal probe.Logins 0 "keine Challenge"

        testCase "eine Ausnahme im Ablauf lässt keine Sitzung im Abruf stehen" <| fun () ->
            let probe = Probe(connected)
            probe.DuringFetch <- fun () -> async { failwith "kaputt" }
            match startSyncWith (probe.Build()) |> run with
            | Error (SyncError.DatabaseError ("start_sync", _)) -> ()
            | other -> failtestf "DatabaseError erwartet: %A" other
            Expect.equal probe.Sessions.Status (Some (Failed "kaputt")) "Sitzung gescheitert"

        testCase "zweiter startSync während des Abrufs: ein Abruf, beide bekommen dieselbe Prüfung" <| fun () ->
            let probe = Probe(connected)
            let inFetch = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            probe.DuringFetch <- fun () -> async {
                inFetch.TrySetResult () |> ignore
                do! release.Task |> Async.AwaitTask }
            let ports = probe.Build()
            let first = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (inFetch.Task.Wait hangLimit) "erster Start hängt im Abruf"
            let second = Async.StartImmediateAsTask (startSyncWith ports)
            Expect.isFalse second.IsCompleted "der zweite Start wartet"
            release.SetResult ()
            Expect.isTrue (Task.WaitAll([| first :> Task; second :> Task |], hangLimit)) "beide fertig"
            let a = expectReviewReady first.Result
            let b = expectReviewReady second.Result
            Expect.equal a.Id b.Id "dieselbe Sitzung"
            Expect.equal probe.Fetches 1 "ein Abruf"
            Expect.equal probe.Sessions.Transactions.Length 1 "Buchung einmal"

        testCase "ein Fehler vor der neuen Sitzung lässt die alte Sitzung unberührt" <| fun () ->
            let probe = Probe(connected)
            let older = probe.Sessions.Open AwaitingTan []
            let ports = { probe.Build() with LoadSettings = fun () -> async { return failwith "Einstellungen kaputt" } }
            match startSyncWith ports |> run with
            | Error (SyncError.DatabaseError ("start_sync", _)) -> ()
            | other -> failtestf "DatabaseError erwartet: %A" other
            Expect.equal probe.Sessions.Status (Some older.Status) "nicht als gescheitert markiert"

        // Bug: `cancelSync` wartete am `Cell.Gate`, das der Abruf hält, bis zum Ende des Abrufs.
        testCase "Abbruch während des Abrufs wartet nicht auf die Bank, das Ergebnis verfällt" <| fun () ->
            let probe = Probe(connected)
            let bank = Barrier()
            probe.DuringFetch <- bank.Hold
            let start = startSyncWith (probe.Build()) |> Async.StartAsTask
            Expect.isTrue (bank.WaitReached ()) "der Abruf läuft"
            let cancelled = cancelSync probe (startedSessionId probe)
            Expect.isTrue (cancelled.Wait hangLimit) "der Abbruch kommt durch, obwohl der Abruf das Gate hält"
            Expect.equal cancelled.Result (Ok ()) "abgebrochen"
            bank.Release ()
            Expect.isTrue (start.Wait hangLimit) "der Abruf endet"
            expectSessionGone start.Result
            Expect.isEmpty probe.Sessions.Transactions "nichts installiert"
            Expect.isTrue (isConnected probe.Connection) "die Verbindung bleibt"

        // Bug: nach dem Abbruch startete der Verbindungstod eine Challenge, `ChallengePending` blieb liegen.
        testCase "die Verbindung stirbt nach dem Abbruch: keine Challenge, nichts bleibt liegen" <| fun () ->
            let probe = Probe(connected)
            probe.FetchAnswer <- Error rejected
            probe.RefreshAnswer <- Error (ReloginRequired "HTTP 401")
            let bank = Barrier()
            probe.DuringFetch <- bank.Hold
            let start = startSyncWith (probe.Build()) |> Async.StartAsTask
            Expect.isTrue (bank.WaitReached ()) "der Abruf läuft"
            let cancelled = cancelSync probe (startedSessionId probe)
            Expect.isTrue (cancelled.Wait hangLimit) "abgebrochen"
            bank.Release ()
            Expect.isTrue (start.Wait hangLimit) "der Abruf endet"
            expectSessionGone start.Result
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.isTrue (isDisconnected probe.Connection) "kein liegengebliebenes ChallengePending"

        testCase "Abbruch, während die Challenge am Gate wartet: nichts geht zur Bank" <| fun () ->
            let probe = Probe(Disconnected)
            let built = probe.Build()
            // Die Wiederaufnahme ist hier vorbei, sonst warteten wir schon an ihrem Gate.
            let bank = { built.Sources.Bank with Resume = fun () -> async.Return () }
            let ports = { built with Sources = { built.Sources with Bank = bank } }
            probe.Connection.Cell.Gate.Wait ()
            let start = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (SpinWait.SpinUntil ((fun () -> probe.Sessions.Status = Some AwaitingTan), hangLimit)) "wartet am Gate"
            let cancelled = cancelSync probe (startedSessionId probe)
            Expect.isTrue (cancelled.Wait hangLimit) "abgebrochen"
            probe.Connection.Cell.Gate.Release () |> ignore
            Expect.isTrue (start.Wait hangLimit) "der Start endet"
            expectSessionGone start.Result
            Expect.equal probe.Logins 0 "keine Challenge"

        testCase "Abbruch, während die Wiederaufnahme am Gate wartet: nichts geht zur Bank" <| fun () ->
            let probe = Probe(Disconnected)
            let ports = probe.Build()
            probe.Connection.Cell.Gate.Wait ()
            let start = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (SpinWait.SpinUntil ((fun () -> probe.Sessions.Started.Count = 1), hangLimit)) "Sitzung läuft"
            let cancelled = cancelSync probe (startedSessionId probe)
            Expect.isTrue (cancelled.Wait hangLimit) "abgebrochen"
            probe.Connection.Cell.Gate.Release () |> ignore
            Expect.isTrue (start.Wait hangLimit) "der Start endet"
            expectSessionGone start.Result
            Expect.equal probe.Logins 0 "keine Challenge"

        testCase "Abbruch während der Anmeldung: die Challenge wird danach verworfen" <| fun () ->
            let probe = Probe(Disconnected)
            let bank = Barrier()
            probe.DuringLogin <- bank.Hold
            let start = startSyncWith (probe.Build()) |> Async.StartAsTask
            Expect.isTrue (bank.WaitReached ()) "die Anmeldung läuft"
            let cancelled = cancelSync probe (startedSessionId probe)
            Expect.isTrue (cancelled.Wait hangLimit) "der Abbruch wartet nicht auf die Anmeldung"
            bank.Release ()
            Expect.isTrue (start.Wait hangLimit) "der Start endet"
            expectSessionGone start.Result
            Expect.isTrue (SpinWait.SpinUntil ((fun () -> isDisconnected probe.Connection), hangLimit)) "kein liegengebliebenes ChallengePending"

        testCase "der Abbruch löscht erst die Sitzung, dann die Challenge" <| fun () ->
            let steps = ResizeArray<string>()
            let cancel =
                Server.Api.cancelSyncWith
                    Ok
                    (fun () -> async { steps.Add "Challenge" })
                    (fun () -> steps.Add "Sitzung")
                    (SyncSessionId (Guid.NewGuid()))
            Expect.equal (run cancel) (Ok ()) "abgebrochen"
            Expect.equal (List.ofSeq steps) [ "Sitzung"; "Challenge" ] "ein laufender Ablauf sieht den Abbruch vor dem Verwerfen"
    ]

[<Tests>]
let confirmTanTests =
    testList "confirmTan: nach der TAN derselbe Abruf" [

        testCase "bestätigte TAN: die Prüfung ist offen" <| fun () ->
            let probe = Probe(pendingChallenge)
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan []
            Expect.equal (confirmTanWith ports session.Id |> run) (Ok ()) "bestätigt"
            Expect.equal probe.Sessions.Status (Some ReviewingTransactions) "Prüfung offen"
            Expect.equal probe.Sessions.Transactions.Length 1 "Buchung installiert"
            Expect.isTrue (isConnected probe.Connection) "verbunden"

        testCase "gescheiterte TAN: Fehler, kein Abruf" <| fun () ->
            let probe = Probe(pendingChallenge)
            probe.ConfirmAnswer <- Error ComdirectError.TanRejected
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan []
            match confirmTanWith ports session.Id |> run with
            | Error (SyncError.ComdirectAuthFailed _) -> ()
            | other -> failtestf "ComdirectAuthFailed erwartet: %A" other
            Expect.equal probe.Fetches 0 "kein Abruf"

        testCase "falscher Sitzungszustand: nichts geht zur Bank" <| fun () ->
            let probe = Probe(pendingChallenge)
            let ports = probe.Build()
            let session = probe.Sessions.Open ReviewingTransactions []
            match confirmTanWith ports session.Id |> run with
            | Error (SyncError.InvalidSessionState _) -> ()
            | other -> failtestf "InvalidSessionState erwartet: %A" other
            Expect.isTrue (isChallengePending probe.Connection) "Challenge unberührt"

        testCase "Abruf scheitert nach der TAN: Fehler, die Sitzung ist gescheitert" <| fun () ->
            let probe = Probe(pendingChallenge)
            probe.FetchAnswer <- Error (ComdirectError.NetworkError (503, ""))
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan []
            match confirmTanWith ports session.Id |> run with
            | Error (SyncError.TransactionFetchFailed _) -> ()
            | other -> failtestf "TransactionFetchFailed erwartet: %A" other
            match probe.Sessions.Status with
            | Some (Failed _) -> ()
            | other -> failtestf "Sitzung gescheitert erwartet: %A" other

        // Bug: eine Ausnahme ließ die Sitzung dauerhaft in FetchingTransactions, der Client las ewig nach.
        testCase "eine Ausnahme im Abruf nach der TAN lässt keine Sitzung im Abruf stehen" <| fun () ->
            let probe = Probe(pendingChallenge)
            probe.DuringFetch <- fun () -> async { failwith "kaputt" }
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan []
            match confirmTanWith ports session.Id |> run with
            | Error (SyncError.DatabaseError ("confirm_tan", _)) -> ()
            | other -> failtestf "DatabaseError erwartet: %A" other
            Expect.equal probe.Sessions.Status (Some (Failed "kaputt")) "Sitzung gescheitert"

        testCase "Abbruch während der TAN-Bestätigung: kein Abruf, die Verbindung bleibt" <| fun () ->
            let probe = Probe(pendingChallenge)
            probe.DuringConfirm <- fun () -> async { probe.Sessions.Clear () }
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan []
            confirmTanWith ports session.Id |> run |> expectSessionGone
            Expect.equal probe.Fetches 0 "kein Abruf"
            Expect.isTrue (isConnected probe.Connection) "verbunden"
    ]

let private fakeBank (transactions: BankTransaction list) : BankPort =
    { Resume = fun () -> async.Return ()
      Status = fun () -> BankReady
      StartAuth = fun _ _ -> failtest "keine Challenge im Abruf"
      CompleteAuth = fun () -> failtest "keine Bestätigung im Abruf"
      FetchAccounts = fun () -> failtest "der Abruf hängt nicht am Kontenabruf"
      FetchTransactions = fun _ _ _ -> async.Return (Ok transactions) }

let private sources (bank: BankPort) (rules: Rule list) (ynab: YnabTransaction list option) : ReviewSources =
    { Bank = bank
      LoadRules = fun () -> async.Return rules
      LoadYnabTransactions = fun _ -> async.Return ynab }

let private request = { AccountId = "konto"; DaysToFetch = 14; Now = now.UtcDateTime; Today = today }

let private fetched (source: ReviewSources) =
    match fetchForReview source request |> run with
    | Ok transactions -> transactions
    | Error failure -> failtestf "Buchungen erwartet: %A" failure

let private orderId = "302-1234567-7654321"

[<Tests>]
let fetchForReviewTests =
    testList "fetchForReview: Regeln, Duplikate, Order-ID und Auto-Skip" [

        testCase "die Regel kategorisiert und trägt den Prüfschritt mit der eingespeisten Zeit" <| fun () ->
            let categoryId = Guid.NewGuid()
            let source = sources (fakeBank [ bankTransaction "r1" "Marktkauf" "Karte" ]) [ ruleFor "Marktkauf" categoryId ] None
            let tx = fetched source |> List.exactlyOne
            Expect.equal tx.Status AutoCategorized "Regel gegriffen"
            Expect.equal tx.CategoryId (Some (YnabCategoryId categoryId)) "Kategorie der Regel"
            Expect.equal (tx.ReviewSteps |> List.map (fun s -> s.At)) [ now.UtcDateTime ] "Zeit kommt als Parameter"

        testCase "eine ungültige Regel beendet den Abruf mit der Meldung" <| fun () ->
            let invalid = { ruleFor "(" (Guid.NewGuid()) with PatternType = PatternType.Regex }
            match fetchForReview (sources (fakeBank []) [ invalid ] None) request |> run with
            | Error (RulesInvalid message) -> Expect.isNotEmpty message "Meldung"
            | other -> failtestf "RulesInvalid erwartet: %A" other

        testCase "bestätigtes Duplikat wird übersprungen, die übrigen bleiben offen" <| fun () ->
            let bank = fakeBank [ bankTransaction "schon-da" "Markt" "Karte"; bankTransaction "neu" "Markt" "Karte" ]
            let known = [ ynabTransaction "Karte, Ref: schon-da" -99m None ]
            let byReference = fetched (sources bank [] (Some known)) |> List.map (fun t -> t.Transaction.Reference, t.Status) |> Map.ofList
            Expect.equal byReference["schon-da"] Skipped "Auto-Skip"
            Expect.equal byReference["neu"] Pending "bleibt offen"

        testCase "mögliches Duplikat bleibt offen und trägt das Urteil" <| fun () ->
            let tx = { bankTransaction "aehnlich" "Markt" "Karte" with BookingDate = DateTime(2026, 9, 1) }
            let known = { ynabTransaction "ohne Referenz" -12.34m None with Payee = Some "Markt" }
            let result = fetched (sources (fakeBank [ tx ]) [] (Some [ known ])) |> List.exactlyOne
            Expect.equal result.Status Pending "nicht übersprungen"
            match result.DuplicateStatus with
            | PossibleDuplicate _ -> ()
            | other -> failtestf "PossibleDuplicate erwartet: %A" other

        testCase "Order-ID aus der YNAB-Historie schlägt die Kategorie vor" <| fun () ->
            let categoryId = Guid.NewGuid()
            let known = [ ynabTransaction $"Bestellung {orderId}" -5m (Some (categoryId, "Haushalt")) ]
            let bank = fakeBank [ bankTransaction "amazon" "AMAZON" $"Bestellung {orderId}" ]
            let tx = fetched (sources bank [] (Some known)) |> List.exactlyOne
            Expect.equal tx.SuggestedByOrderId (Some orderId) "Vorschlag"
            Expect.equal tx.CategoryId (Some (YnabCategoryId categoryId)) "Kategorie aus der Historie"

        testCase "ohne YNAB-Abgleich bleiben die Buchungen unbeurteilt, der Abruf läuft" <| fun () ->
            let tx = fetched (sources (fakeBank [ bankTransaction "r1" "Markt" "Karte" ]) [] None) |> List.exactlyOne
            Expect.equal tx.Status Pending "offen"
            match tx.DuplicateStatus with
            | NotDuplicate _ -> ()
            | other -> failtestf "NotDuplicate erwartet: %A" other

        testCase "der YNAB-Abgleich reicht sieben Tage weiter als der Bankabruf" <| fun () ->
            let requested = ResizeArray<int>()
            let source =
                { sources (fakeBank []) [] None with
                    LoadYnabTransactions = fun days -> async {
                        requested.Add days
                        return None } }
            fetched source |> ignore
            Expect.equal (List.ofSeq requested) [ 21 ] "14 + 7 Tage"

        testCase "Konto, Tage und Kalendertag gehen unverändert an die Bank" <| fun () ->
            let seen = ResizeArray<string * int * DateTime>()
            let bank =
                { fakeBank [] with
                    FetchTransactions = fun account days day -> async {
                        seen.Add ((account, days, day))
                        return Ok [] } }
            fetched (sources bank [] None) |> ignore
            Expect.equal (List.ofSeq seen) [ "konto", 14, today ] "Parameter"

        testCase "Bankfehler: Regeln und YNAB werden nicht gelesen" <| fun () ->
            let bank = { fakeBank [] with FetchTransactions = fun _ _ _ -> async.Return (Error ComdirectError.SessionExpired) }
            let source =
                { sources bank [] None with
                    LoadRules = fun () -> failtest "keine Regeln"
                    LoadYnabTransactions = fun _ -> failtest "kein YNAB" }
            match fetchForReview source request |> run with
            | Error (BankFetchFailed ComdirectError.SessionExpired) -> ()
            | other -> failtestf "BankFetchFailed erwartet: %A" other
    ]

let private storedProbe () =
    Probe(Disconnected, StoredRead = async.Return (StoredReadable stored))

/// Ein Fenster aus banking-import-z4h6p: `BankPort.Status` liest die Zelle ohne `Gate`. Direkt nach
/// dem Start steht dort `Disconnected`, obwohl ein Wert liegt. Der Klick kostete sonst eine TAN und
/// den Wert.
[<Tests>]
let startSyncAfterRestartTests =
    testList "startSync nach dem Neustart: ein gespeicherter Wert kostet keine TAN" [

        testCase "Klick vor der Wiederaufnahme: ReviewReady, keine Challenge, der Wert bleibt" <| fun () ->
            let probe = storedProbe ()
            startSyncWith (probe.Build()) |> run |> expectReviewReady |> ignore
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.equal probe.Refreshes 1 "per Refresh aufgenommen"
            Expect.equal probe.Clears 0 "Wert bleibt"

        testCase "Klick während die Wiederaufnahme das Gate hält: er wartet und braucht keine Challenge" <| fun () ->
            let probe = storedProbe ()
            let ports = probe.Build()
            let bank = Barrier()
            probe.DuringRefresh <- bank.Hold
            let resume = resumeConnectionWith probe.Connection now |> Async.StartAsTask
            Expect.isTrue (bank.WaitReached ()) "die Wiederaufnahme hält das Gate"
            let start = startSyncWith ports |> Async.StartAsTask
            Expect.isFalse (start.Wait 200) "der Klick wartet hinter dem Refresh"
            bank.Release ()
            Expect.isTrue (resume.Wait hangLimit) "Wiederaufnahme fertig"
            Expect.isTrue (start.Wait hangLimit) "Klick fertig"
            start.Result |> expectReviewReady |> ignore
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.equal probe.Refreshes 1 "ein Refresh"
            Expect.equal probe.Clears 0 "Wert bleibt"

        testCase "Klick im Moment vor dem Setzen der Zelle: das Gate ist belegt, die Zelle noch Disconnected" <| fun () ->
            let probe = storedProbe ()
            let ports = probe.Build()
            let loading = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let load = TaskCompletionSource<StoredConnectionRead>(TaskCreationOptions.RunContinuationsAsynchronously)
            probe.StoredRead <- async {
                loading.TrySetResult () |> ignore
                return! load.Task |> Async.AwaitTask }
            let resume = resumeConnectionWith probe.Connection now |> Async.StartAsTask
            Expect.isTrue (loading.Task.Wait hangLimit) "die Wiederaufnahme liest den Wert"
            Expect.isTrue (isDisconnected probe.Connection) "die Zelle steht noch auf Disconnected"
            let start = startSyncWith ports |> Async.StartAsTask
            Expect.isFalse (start.Wait 200) "der Klick wartet am Gate"
            load.SetResult (StoredReadable stored)
            Expect.isTrue (resume.Wait hangLimit) "Wiederaufnahme fertig"
            Expect.isTrue (start.Wait hangLimit) "Klick fertig"
            start.Result |> expectReviewReady |> ignore
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.equal probe.Clears 0 "Wert bleibt"

        testCase "Die Bank hängt: cancelSync wartet nicht, der Sync endet ohne Challenge" <| fun () ->
            let probe = storedProbe ()
            let ports = probe.Build()
            let bank = Barrier()
            probe.DuringRefresh <- bank.Hold
            let start = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (bank.WaitReached ()) "der Klick steht im Refresh"
            let cancelled = cancelSync probe (startedSessionId probe)
            Expect.isTrue (cancelled.Wait hangLimit) "der Abbruch wartet nicht auf die Bank"
            bank.Release ()
            Expect.isTrue (start.Wait hangLimit) "Sync fertig"
            expectSessionGone start.Result
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.equal probe.Fetches 0 "kein Abruf"
            Expect.equal probe.Clears 0 "Wert bleibt"

        testCase "Abbruch, und das Urteil heißt ReloginRequired: keine Challenge" <| fun () ->
            let probe = storedProbe ()
            probe.RefreshAnswer <- Error (ReloginRequired "HTTP 401")
            let ports = probe.Build()
            let bank = Barrier()
            probe.DuringRefresh <- bank.Hold
            let start = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (bank.WaitReached ()) "der Klick steht im Refresh"
            let cancelled = cancelSync probe (startedSessionId probe)
            Expect.isTrue (cancelled.Wait hangLimit) "Abbruch fertig"
            bank.Release ()
            Expect.isTrue (start.Wait hangLimit) "Sync fertig"
            expectSessionGone start.Result
            Expect.equal probe.Logins 0 "keine Challenge"

        testCase "ReloginRequired beim Klick: Wert gelöscht, Romans Klick darf die TAN auslösen" <| fun () ->
            let probe = storedProbe ()
            probe.RefreshAnswer <- Error (ReloginRequired "HTTP 401")
            startSyncWith (probe.Build()) |> run |> expectTanRequired |> ignore
            Expect.equal probe.Logins 1 "genau eine Challenge"
            Expect.equal probe.Fetches 0 "kein Abruf"
            Expect.equal (List.ofSeq probe.Losses) [ "HTTP 401" ] "der Verlust ist gemeldet"
            Expect.isGreaterThan probe.Clears 0 "Wert gelöscht"

        testCase "Der Wert ist nicht lesbar, weil das Lesen wirft: der Klick geht in die TAN" <| fun () ->
            let probe = Probe(Disconnected, StoredRead = async { return failwith "Datenbank gestört" })
            startSyncWith (probe.Build()) |> run |> expectTanRequired |> ignore
            Expect.equal probe.Logins 1 "eine Challenge"

        testCase "Nichts gespeichert: kein Refresh vor der Challenge" <| fun () ->
            let probe = Probe(Disconnected)
            startSyncWith (probe.Build()) |> run |> expectTanRequired |> ignore
            Expect.equal probe.Refreshes 0 "kein Refresh"

        testCase "Aktive Verbindung: der Wert löst keinen Refresh aus" <| fun () ->
            let probe = Probe(connected, StoredRead = async.Return (StoredReadable stored))
            startSyncWith (probe.Build()) |> run |> expectReviewReady |> ignore
            Expect.equal probe.Refreshes 0 "kein Refresh aus dem Wert, das Access-Token gilt noch"
    ]

/// Nach `RetryLater` gilt weiter `Connected`: `fetchOrRequireTan` darf einen Fehler dann nicht als
/// gestorbene Verbindung lesen.
[<Tests>]
let retryLaterSyncTests =
    testList "startSync nach RetryLater: die Verbindung lebt, es gibt keine Challenge" [

        testCase "RetryLater beim Klick: der Abruf läuft mit dem alten Token, der Wert bleibt" <| fun () ->
            let probe = storedProbe ()
            probe.RefreshAnswer <- Error (RetryLater "HTTP 503")
            startSyncWith (probe.Build()) |> run |> expectReviewReady |> ignore
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.equal probe.Fetches 1 "Abruf gelaufen"
            Expect.equal probe.Clears 0 "Wert bleibt"
            Expect.isEmpty probe.Losses "kein Verlust"

        testCase "RetryLater und die Bank lehnt den Abruf ab: Fehler sichtbar, keine Challenge, der Wert bleibt" <| fun () ->
            let probe = storedProbe ()
            probe.RefreshAnswer <- Error (RetryLater "HTTP 503")
            probe.FetchAnswer <- Error rejected
            let ports = probe.Build()
            match startSyncWith ports |> run with
            | Error (SyncError.TransactionFetchFailed _) -> ()
            | other -> failtestf "TransactionFetchFailed erwartet: %A" other
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.isTrue (isConnected probe.Connection) "die Verbindung steht"
            Expect.equal probe.Clears 0 "Wert bleibt"
            Expect.isEmpty probe.Losses "kein Verlust"
            match probe.Sessions.Status with
            | Some (Failed _) -> ()
            | other -> failtestf "gescheiterte Sitzung erwartet: %A" other

        testCase "RetryLater und ein Netzfehler im Abruf: keine Challenge" <| fun () ->
            let probe = storedProbe ()
            probe.RefreshAnswer <- Error (RetryLater "HTTP 503")
            probe.FetchAnswer <- Error (ComdirectError.NetworkError (503, ""))
            let ports = probe.Build()
            match startSyncWith ports |> run with
            | Error (SyncError.TransactionFetchFailed _) -> ()
            | other -> failtestf "TransactionFetchFailed erwartet: %A" other
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.isTrue (isConnected probe.Connection) "die Verbindung steht"

        testCase "Die Verbindung stirbt im Abruf: TanRequired, der Wert ist weg, einmal gemeldet" <| fun () ->
            let probe = Probe(connected)
            probe.FetchAnswer <- Error rejected
            probe.RefreshAnswer <- Error (ReloginRequired "HTTP 401")
            startSyncWith (probe.Build()) |> run |> expectTanRequired |> ignore
            Expect.equal probe.Logins 1 "genau eine Challenge"
            Expect.isGreaterThan probe.Clears 0 "Wert gelöscht"
            Expect.equal probe.Losses.Count 1 "einmal gemeldet"
    ]

/// Die Sync-Wege aus banking-import-cyg94 behandeln den Wert wie `startChallengeWith`.
[<Tests>]
let storedValueOnSyncPathsTests =
    testList "Gespeicherter Wert auf den Sync-Wegen: Challenge und Abbruch" [

        testCase "neue Challenge über den Bank-Port: der Wert der abgelösten Verbindung ist weg" <| fun () ->
            let probe = Probe(connected)
            probe.Build() |> ignore
            startChallengeWhileWith probe.Connection (fun () -> true) credentials |> run |> ignore
            Expect.isTrue (isChallengePending probe.Connection) "Challenge offen"
            Expect.equal probe.Clears 1 "gelöscht"

        testCase "die Bedingung gilt nicht mehr: nichts zur Bank, der Wert und die Verbindung bleiben" <| fun () ->
            let probe = Probe(connected)
            probe.Build() |> ignore
            startChallengeWhileWith probe.Connection (fun () -> false) credentials |> run |> ignore
            Expect.equal probe.Logins 0 "keine Anmeldung"
            Expect.isTrue (isConnected probe.Connection) "Verbindung steht"
            Expect.equal probe.Clears 0 "bleibt"

        testCase "gescheiterte Anmeldung über den Bank-Port: der Wert bleibt" <| fun () ->
            let probe = Probe(connected)
            probe.LoginAnswer <- Error ComdirectError.InvalidCredentials
            probe.Build() |> ignore
            startChallengeWhileWith probe.Connection (fun () -> true) credentials |> run |> ignore
            Expect.isTrue (isConnected probe.Connection) "Verbindung steht"
            Expect.equal probe.Clears 0 "bleibt"

        testCase "Abbruch bei aktiver Verbindung und freiem Gate: Verbindung und Wert bleiben" <| fun () ->
            let probe = Probe(connected)
            probe.Build() |> ignore
            discardPendingChallengeWith probe.Connection |> run
            Expect.isTrue (isConnected probe.Connection) "Verbindung steht"
            Expect.equal probe.Clears 0 "bleibt"

        testCase "Abbruch bei aktiver Verbindung und belegtem Gate: Verbindung und Wert bleiben" <| fun () ->
            let probe = Probe(connected)
            let ports = probe.Build()
            let bank = Barrier()
            probe.DuringFetch <- bank.Hold
            let start = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (bank.WaitReached ()) "der Abruf hält das Gate"
            let cancelled = cancelSync probe (startedSessionId probe)
            Expect.isTrue (cancelled.Wait hangLimit) "Abbruch fertig"
            bank.Release ()
            Expect.isTrue (start.Wait hangLimit) "Sync fertig"
            discardPendingChallengeWith probe.Connection |> run
            Expect.isTrue (isConnected probe.Connection) "Verbindung steht"
            Expect.equal probe.Clears 0 "bleibt"

        testCase "verworfene Challenge: das Verwerfen löscht nichts mehr, der Wert ist schon weg" <| fun () ->
            let probe = Probe(pendingChallenge)
            probe.Build() |> ignore
            discardPendingChallengeWith probe.Connection |> run
            Expect.isTrue (isDisconnected probe.Connection) "Challenge verworfen"
            Expect.equal probe.Clears 0 "kein zweites Löschen"
    ]
