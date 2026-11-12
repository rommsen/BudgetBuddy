module Tests.BankImportTests

// Der Server entscheidet über die TAN (banking-import-cyg94, ADR 0071 Punkt 5 und 6). Der Bank-Port
// läuft über den echten Zustandsautomaten mit Fake-Ports, kein Testfall erreicht Comdirect oder YNAB.
// Seit banking-import-d0fey (ADR 0093) bucht der Abruf: das Kassenbuch ist die In-Memory-SQLite, der Push
// nach YNAB ein Fake-Port. Jede Reference ist je Testfall eindeutig, Expecto läuft parallel.

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

do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

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

/// Eine Reference, die kein anderer Testfall nutzt: der Importschlüssel `BB:` plus Reference ist global.
let private uniqueRef (label: string) = $"{label}-{Guid.NewGuid():N}"

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
    member val Started = ResizeArray<SyncSession>()
    member val Changed = ResizeArray<SyncSession>()
    member val OnRecordStarted : unit -> unit = ignore with get, set
    member _.Status = current |> Option.map (fun session -> session.Status)
    member _.Current = current
    member _.Clear() = current <- None
    member _.Open (status: SyncSessionStatus) =
        let session =
            { Id = SyncSessionId (Guid.NewGuid())
              StartedAt = DateTime(2026, 10, 31)
              CompletedAt = None
              Status = status
              Trigger = Manual
              Counts = emptyFetchCounts }
        current <- Some session
        session
    member this.Build() : SyncSessions =
        let setStatus status =
            match current with
            | Some session -> current <- Some { session with Status = status }
            | None -> failwith "Keine Sitzung"
        { Current = fun () -> current
          StartNew = fun () -> this.Open AwaitingBankAuth
          SetStatus = setStatus
          Fail = fun message -> if current.IsSome then setStatus (Failed message)
          Complete = fun counts ->
              current <- current |> Option.map (fun s -> { s with Status = Completed; Counts = counts })
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
    let mutable refreshes = 0
    let mutable clears = 0
    member val Sessions = SessionsProbe()
    member val Losses = ResizeArray<string>()
    member val FetchAnswer : ComdirectResult<BankTransaction list> = Ok [ bankTransaction (uniqueRef "ref") "Markt" "Karte" ] with get, set
    /// Die vorbereiteten Buchungen je Push nach YNAB.
    member val Pushes = ResizeArray<Server.YnabClient.PreparedTransaction list>()
    member val Rules : Rule list = [] with get, set
    member val YnabAnswer : YnabTransaction list option = Some [] with get, set
    /// Die Antwort von YNAB auf einen Push. Ohne Angabe legt YNAB jede Buchung unter einer eigenen Id an.
    member val PushAnswer : (Server.YnabClient.PreparedTransaction list -> YnabResult<Server.YnabClient.TransactionCreateResult>) option = None with get, set
    /// Das Kassenbuch-Konto des Import-Kontos, je Probe eigenes.
    member val LedgerAccount = Ledger.Events.newLedgerAccountId () with get
    member val Book = Ledger.CommandHandlers.pushComdirectImports with get, set
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
    member _.Refreshes = Volatile.Read(&refreshes)
    member _.Clears = Volatile.Read(&clears)
    member val FetchedAccountIds = ResizeArray<string>()
    /// Der Kalendertag, den die Bank für den Abruf bekommt.
    member val FetchedDays = ResizeArray<DateTime>()
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
              FetchTransactions = fun _ _ accountId _ day -> async {
                  Interlocked.Increment(&fetches) |> ignore
                  lock this.FetchedAccountIds (fun () -> this.FetchedAccountIds.Add accountId)
                  lock this.FetchedDays (fun () -> this.FetchedDays.Add day)
                  do! this.DuringFetch ()
                  return this.FetchAnswer }
              FetchFirstTransactionsPage = fun _ _ _ -> failtest "der Abruf holt keine einzelne Seite"
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
              LoadRules = fun () -> async.Return this.Rules
              LoadYnabTransactions = fun _ -> async.Return this.YnabAnswer }
          Sessions = this.Sessions.Build()
          LoadSettings = fun () -> async.Return this.Settings
          LoadTarget = fun () -> async {
              Ledger.Persistence.initializeLedgerSchema ()
              return
                  Ok {
                      LedgerAccountId = this.LedgerAccount
                      YnabAccountId = YnabAccountId (Guid.NewGuid())
                      Push = fun prepared -> async {
                          lock this.Pushes (fun () -> this.Pushes.Add prepared)
                          match this.PushAnswer with
                          | Some answer -> return answer prepared
                          | None ->
                              return
                                  Ok {
                                      Server.YnabClient.emptyCreateResult with
                                          Created =
                                              prepared
                                              |> List.map (fun p ->
                                                  ({ TransactionId = p.TransactionId
                                                     ImportId = p.ImportId
                                                     YnabTransactionId = $"ynab-{p.ImportId}" } : Server.YnabClient.CreatedTransactionResult)) } } } }
          TriggerMirror = ignore
          BookImports = fun now push trigger account candidates -> this.Book now push trigger account candidates
          DescribeBankError = fun error -> $"Bankfehler: %A{error}"
          LastStart = ref None
          Clock = fun () -> now.UtcDateTime
          Today = fun () -> today }

let private rejected = ComdirectError.AuthenticationFailed "HTTP 401"

let private expectFetched (result: SyncResult<SyncStart>) =
    match result with
    | Ok (Fetched session) -> session
    | other -> failtestf "Fetched erwartet, bekam %A" other

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

        testCase "aktive Verbindung: der Abruf bucht ohne Challenge, die Sitzung ist mit ihren Zahlen beendet" <| fun () ->
            let probe = Probe(connected)
            let ports = probe.Build()
            let session = startSyncWith ports |> run |> expectFetched
            Expect.equal session.Status Completed "beendet"
            Expect.equal session.Counts.NewEntries 1 "Anzahl in der gelieferten Sitzung"
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.equal (probe.Pushes |> Seq.sumBy List.length) 1 "eine Buchung nach YNAB"
            Expect.equal probe.Sessions.Started.Count 1 "Sitzung gespeichert"
            Expect.equal (probe.Sessions.Changed |> Seq.map (fun s -> s.Status) |> List.ofSeq) [ Completed ] "Endstand gespeichert"

        testCase "der Abruf nutzt die konfigurierte Konto-Id" <| fun () ->
            let probe = Probe(connected)
            startSyncWith (probe.Build()) |> run |> expectFetched |> ignore
            Expect.equal (List.ofSeq probe.FetchedAccountIds) [ "konfiguriertes-konto" ] "Konto aus den Einstellungen"

        // infrastructure-p747v: abends um 23:30 UTC ist in Berlin schon der nächste Tag.
        testCase "der Abruf nutzt den Kalendertag aus `Today`, auch wenn die Uhr noch den Vortag zeigt" <| fun () ->
            let probe = Probe(connected)
            let ports =
                { probe.Build() with
                    Clock = fun () -> DateTime(2026, 11, 1, 23, 30, 0, DateTimeKind.Utc)
                    Today = fun () -> DateTime(2026, 11, 2) }
            startSyncWith ports |> run |> expectFetched |> ignore
            Expect.equal (List.ofSeq probe.FetchedDays) [ DateTime(2026, 11, 2) ] "der 2. November kommt an"

        // Bug: Ohne Konto-Id ging ein leerer Wert an die Bank, die Antwort sagte nichts über die Ursache.
        for label, accountId in [ "fehlt", None; "ist leer", Some "  " ] do
            for connection, state in [ "aktive Verbindung", connected; "keine Verbindung", Disconnected ] do
                testCase $"Konto-Id {label}, {connection}: konkrete Meldung, nichts geht zur Bank, keine Sitzung" <| fun () ->
                    let probe = Probe(state)
                    probe.Settings <- { probe.Settings with Comdirect = Some { credentials with AccountId = accountId } }
                    match startSyncWith (probe.Build()) |> run with
                    | Error (SyncError.TransactionFetchFailed reason) -> Expect.stringContains reason "Konto-Id" "die Meldung nennt, was fehlt"
                    | other -> failtestf "TransactionFetchFailed erwartet: %A" other
                    Expect.equal probe.Fetches 0 "kein Abruf"
                    Expect.equal probe.Logins 0 "keine Challenge"
                    Expect.equal probe.Sessions.Started.Count 0 "keine Sitzung"

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
            Expect.equal session.Counts.NewEntries 0 "keine Buchungen"
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

        testCase "eine abgeschlossene Sitzung blockiert keinen neuen Sync" <| fun () ->
            let probe = Probe(connected)
            let finished = probe.Sessions.Open Completed
            let session = startSyncWith (probe.Build()) |> run |> expectFetched
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

        testCase "zweiter startSync während des Abrufs: ein Abruf, beide bekommen dasselbe Ergebnis" <| fun () ->
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
            let a = expectFetched first.Result
            let b = expectFetched second.Result
            Expect.equal a.Id b.Id "dieselbe Sitzung"
            Expect.equal probe.Fetches 1 "ein Abruf"
            Expect.equal (probe.Pushes |> Seq.sumBy List.length) 1 "Buchung einmal gebucht und gesendet"

        testCase "ein Fehler vor der neuen Sitzung lässt die alte Sitzung unberührt" <| fun () ->
            let probe = Probe(connected)
            let older = probe.Sessions.Open AwaitingTan
            let ports = { probe.Build() with LoadSettings = fun () -> async { return failwith "Einstellungen kaputt" } }
            match startSyncWith ports |> run with
            | Error (SyncError.DatabaseError ("start_sync", _)) -> ()
            | other -> failtestf "DatabaseError erwartet: %A" other
            Expect.equal probe.Sessions.Status (Some older.Status) "nicht als gescheitert markiert"

        // Review-Fund zu banking-import-d0fey: ein Start, der am Gate gewartet hatte, übernahm jede fertige Sitzung,
        // auch eine von vor Stunden, wenn der Abruf vor ihm keine neue angelegt hatte.
        testCase "zweiter Start am Gate: eine alte, schon beendete Sitzung ist nicht sein Ergebnis" <| fun () ->
            let probe = Probe(connected)
            let old = probe.Sessions.Open Completed
            let reached = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let mutable loads = 0
            let ports =
                { probe.Build() with
                    LoadSettings = fun () -> async {
                        if Interlocked.Increment(&loads) = 1 then
                            reached.TrySetResult () |> ignore
                            do! release.Task |> Async.AwaitTask
                            return failwith "Einstellungen kaputt"
                        else
                            return probe.Settings } }
            let first = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (reached.Task.Wait hangLimit) "der erste Start hält das Gate"
            let second = Async.StartImmediateAsTask (startSyncWith ports)
            Expect.isFalse second.IsCompleted "der zweite Start wartet"
            release.SetResult ()
            Expect.isTrue (Task.WaitAll([| first :> Task; second :> Task |], hangLimit)) "beide fertig"
            let session = expectFetched second.Result
            Expect.notEqual session.Id old.Id "eine neue Sitzung"
            Expect.equal probe.Fetches 1 "der zweite Start ruft selbst ab"

        // Review-Fund zu banking-import-d0fey: aus jeder gescheiterten Sitzung wurde TransactionFetchFailed.
        testCase "zweiter Start am Gate: der Fehler des Abrufs kommt mit seinem Typ an" <| fun () ->
            let probe = Probe(connected)
            probe.YnabAnswer <- None
            let inFetch = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            probe.DuringFetch <- fun () -> async {
                inFetch.TrySetResult () |> ignore
                do! release.Task |> Async.AwaitTask }
            let ports = probe.Build()
            let first = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (inFetch.Task.Wait hangLimit) "erster Start hängt im Abruf"
            let second = Async.StartImmediateAsTask (startSyncWith ports)
            release.SetResult ()
            Expect.isTrue (Task.WaitAll([| first :> Task; second :> Task |], hangLimit)) "beide fertig"
            Expect.equal second.Result first.Result "dasselbe Ergebnis"
            match second.Result with
            | Error (SyncError.YnabImportFailed (_, message)) -> Expect.equal message ynabUnavailableMessage "die Meldung"
            | other -> failtestf "YnabImportFailed erwartet: %A" other
            Expect.equal probe.Fetches 1 "ein Abruf"

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
            Expect.isEmpty probe.Pushes "nichts gebucht"
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

        testCase "bestätigte TAN: der Abruf bucht, die beendete Sitzung kommt zurück" <| fun () ->
            let probe = Probe(pendingChallenge)
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan
            match confirmTanWith ports session.Id |> run with
            | Ok finished ->
                Expect.equal finished.Status Completed "beendet"
                Expect.equal finished.Counts.NewEntries 1 "eine neue Buchung"
            | other -> failtestf "Ok erwartet: %A" other
            Expect.equal probe.Sessions.Status (Some Completed) "Sitzung beendet"
            Expect.equal (probe.Pushes |> Seq.sumBy List.length) 1 "Buchung gebucht und gesendet"
            Expect.isTrue (isConnected probe.Connection) "verbunden"

        testCase "gescheiterte TAN: Fehler, kein Abruf" <| fun () ->
            let probe = Probe(pendingChallenge)
            probe.ConfirmAnswer <- Error ComdirectError.TanRejected
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan
            match confirmTanWith ports session.Id |> run with
            | Error (SyncError.ComdirectAuthFailed _) -> ()
            | other -> failtestf "ComdirectAuthFailed erwartet: %A" other
            Expect.equal probe.Fetches 0 "kein Abruf"

        testCase "falscher Sitzungszustand: nichts geht zur Bank" <| fun () ->
            let probe = Probe(pendingChallenge)
            let ports = probe.Build()
            let session = probe.Sessions.Open FetchingTransactions
            match confirmTanWith ports session.Id |> run with
            | Error (SyncError.InvalidSessionState _) -> ()
            | other -> failtestf "InvalidSessionState erwartet: %A" other
            Expect.isTrue (isChallengePending probe.Connection) "Challenge unberührt"

        testCase "Abruf scheitert nach der TAN: Fehler, die Sitzung ist gescheitert" <| fun () ->
            let probe = Probe(pendingChallenge)
            probe.FetchAnswer <- Error (ComdirectError.NetworkError (503, ""))
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan
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
            let session = probe.Sessions.Open AwaitingTan
            match confirmTanWith ports session.Id |> run with
            | Error (SyncError.DatabaseError ("confirm_tan", _)) -> ()
            | other -> failtestf "DatabaseError erwartet: %A" other
            Expect.equal probe.Sessions.Status (Some (Failed "kaputt")) "Sitzung gescheitert"

        testCase "die Konto-Id fehlt nach der TAN: konkrete Meldung, kein leerer Abruf" <| fun () ->
            let probe = Probe(pendingChallenge)
            probe.Settings <- { probe.Settings with Comdirect = Some { credentials with AccountId = None } }
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan
            match confirmTanWith ports session.Id |> run with
            | Error (SyncError.TransactionFetchFailed reason) -> Expect.stringContains reason "Konto-Id" "die Meldung nennt, was fehlt"
            | other -> failtestf "TransactionFetchFailed erwartet: %A" other
            Expect.equal probe.Fetches 0 "kein Abruf"
            match probe.Sessions.Status with
            | Some (Failed _) -> ()
            | other -> failtestf "Sitzung gescheitert erwartet: %A" other

        testCase "Abbruch während der TAN-Bestätigung: kein Abruf, die Verbindung bleibt" <| fun () ->
            let probe = Probe(pendingChallenge)
            probe.DuringConfirm <- fun () -> async { probe.Sessions.Clear () }
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingTan
            confirmTanWith ports session.Id |> run |> expectSessionGone
            Expect.equal probe.Fetches 0 "kein Abruf"
            Expect.isTrue (isConnected probe.Connection) "verbunden"
    ]

let private storedProbe () =
    Probe(Disconnected, StoredRead = async.Return (StoredReadable stored))

/// Ein Fenster aus banking-import-z4h6p: `BankPort.Status` liest die Zelle ohne `Gate`. Direkt nach
/// dem Start steht dort `Disconnected`, obwohl ein Wert liegt. Der Klick kostete sonst eine TAN und
/// den Wert.
[<Tests>]
let startSyncAfterRestartTests =
    testList "startSync nach dem Neustart: ein gespeicherter Wert kostet keine TAN" [

        testCase "Klick vor der Wiederaufnahme: Abruf ohne Challenge, der Wert bleibt" <| fun () ->
            let probe = storedProbe ()
            startSyncWith (probe.Build()) |> run |> expectFetched |> ignore
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
            start.Result |> expectFetched |> ignore
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
            start.Result |> expectFetched |> ignore
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
            startSyncWith (probe.Build()) |> run |> expectFetched |> ignore
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
            startSyncWith (probe.Build()) |> run |> expectFetched |> ignore
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

// "Bank trennen": die Bankverbindung endet, die Sitzung bleibt nur, wenn sie ohne Bank auskommt.
let private disconnectBank (ports: ImportPorts) (probe: Probe) : SyncResult<unit> =
    disconnectBankWith ports (fun () -> disconnectWith probe.Connection) |> run

[<Tests>]
let disconnectBankTests =
    testList "Bank trennen: die Sync-Sitzung" [

        testCase "eine Sitzung beim TAN-Schritt endet sichtbar, confirmTan scheitert ohne Bankzugriff" <| fun () ->
            let probe = Probe(Disconnected)
            let ports = probe.Build()
            let session = startSyncWith ports |> run |> expectTanRequired
            Expect.equal (disconnectBank ports probe) (Ok ()) "getrennt"
            Expect.equal probe.Sessions.Status (Some (Failed "Bank getrennt. Sync neu starten.")) "die Sitzung sagt, was los ist"
            match confirmTanWith ports session.Id |> run with
            | Error _ -> ()
            | Ok _ -> failtest "confirmTan muss scheitern"
            Expect.equal probe.Fetches 0 "kein Abruf"

        testCase "eine Sitzung in AwaitingBankAuth bleibt dort nicht stehen" <| fun () ->
            let probe = Probe(connected)
            let ports = probe.Build()
            let session = probe.Sessions.Open AwaitingBankAuth
            disconnectBank ports probe |> ignore
            Expect.equal probe.Sessions.Status (Some (Failed "Bank getrennt. Sync neu starten.")) "beendet"
            Expect.isError (confirmTanWith ports session.Id |> run) "confirmTan scheitert"

        testCase "eine beendete Sitzung bleibt beendet, ihre Buchungen stehen im Kassenbuch" <| fun () ->
            let probe = Probe(connected)
            let ports = probe.Build()
            startSyncWith ports |> run |> expectFetched |> ignore
            Expect.equal (disconnectBank ports probe) (Ok ()) "getrennt"
            Expect.equal probe.Sessions.Status (Some Completed) "beendet"
            Expect.equal (connectionState probe.Connection) Disconnected "Verbindung weg"

        testCase "ein laufender Abruf endet zuerst und bucht" <| fun () ->
            let probe = Probe(connected)
            let bank = Barrier()
            probe.DuringFetch <- bank.Hold
            let ports = probe.Build()
            let start = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (bank.WaitReached ()) "der Abruf läuft"
            let disconnected = disconnectBankWith ports (fun () -> disconnectWith probe.Connection) |> Async.StartAsTask
            bank.Release ()
            Expect.isTrue (disconnected.Wait hangLimit) "das Trennen endet"
            Expect.isTrue (start.Wait hangLimit) "der Start endet"
            Expect.equal probe.Sessions.Status (Some Completed) "beendet"
            Expect.equal (connectionState probe.Connection) Disconnected "danach getrennt"

        // banking-import-bftq0: Ohne `ImportPorts.Gate` startet der Start nach dem Trennen eine Challenge.
        // Dann gibt es zwei Ausgänge: Er überschreibt das `Failed` mit `AwaitingTan`, oder das Trennen
        // kommt später und die Sitzung steht auf `Failed`. Die Challenge bleibt in beiden offen.
        testCase "ein Start, der noch auf die Wiederaufnahme wartet, kann das Trennen nicht überholen" <| fun () ->
            let probe = Probe(Disconnected)
            let bank = Barrier()
            probe.StoredRead <- async {
                do! bank.Hold ()
                return NothingStored }
            let ports = probe.Build()
            let start = startSyncWith ports |> Async.StartAsTask
            Expect.isTrue (bank.WaitReached ()) "der Start hängt in der Wiederaufnahme"
            Expect.equal probe.Sessions.Status (Some AwaitingBankAuth) "die Sitzung wartet auf die Bank"
            use entered = new ManualResetEventSlim(false)
            let endConnection () = async {
                entered.Set ()
                do! disconnectWith probe.Connection }
            let disconnected = disconnectBankWith ports endConnection |> Async.StartAsTask
            // Das Trennen hält auch `Cell.Gate` auf, Maßstab ist deshalb sein Eintritt, nicht sein Ende.
            Expect.isFalse (entered.Wait 100) "das Trennen wartet, bis der Start vorbei ist"
            bank.Release ()
            Expect.isTrue (disconnected.Wait hangLimit) "das Trennen endet"
            Expect.isTrue (start.Wait hangLimit) "der Start endet"
            Expect.equal probe.Sessions.Status (Some (Failed "Bank getrennt. Sync neu starten.")) "die Sitzung endet sichtbar"
            Expect.equal (connectionState probe.Connection) Disconnected "keine Challenge nach dem Trennen"

        // Bug: Die Sitzung endete, obwohl die Verbindung weiterlebte. Die offene TAN war damit verloren.
        testCase "scheitert das Löschen, kommt der Fehler an, nennt den Wert nicht und lässt die Sitzung stehen" <| fun () ->
            let probe = Probe(Disconnected)
            let ports = probe.Build()
            startSyncWith ports |> run |> expectTanRequired |> ignore
            match disconnectBankWith ports (fun () -> async { failwith "wert-aus-der-ausnahme" }) |> run with
            | Error (SyncError.DatabaseError ("disconnect_bank", message)) ->
                Expect.isFalse (message.Contains "wert-aus-der-ausnahme") "nur der Typ"
                Expect.stringContains message "Exception" "der Typname reicht zur Einordnung"
            | other -> failtestf "DatabaseError erwartet: %A" other
            Expect.equal probe.Sessions.Status (Some AwaitingTan) "die Sitzung wartet weiter auf die TAN"
    ]

// ============================================
// Abruf bucht (banking-import-d0fey, ADR 0093 Punkt 1 bis 8)
// ============================================
// Bank und YNAB sind Fake-Ports, das Kassenbuch ist die In-Memory-SQLite. Jeder Testfall nutzt eigene
// References und ein eigenes Kassenbuch-Konto.

module private Booking =
    open Ledger.Events

    let importIdOf (reference: string) = generateImportId (TransactionId reference)

    let entryOf (reference: string) : EntryId option =
        Ledger.Persistence.findEntryByLink Ledger.Domain.ImportIdSystem (importIdOf reference) |> run

    let eventsOf (entryId: EntryId) =
        Ledger.Persistence.loadEventsForEntry entryId |> run |> List.map (fun r -> r.Event)

    let typesOf (entryId: EntryId) = eventsOf entryId |> List.map (fun e -> eventTypeName e.Body)

    let pushedImportIds (probe: Probe) =
        probe.Pushes |> Seq.collect id |> Seq.map (fun p -> p.ImportId) |> List.ofSeq

    let counts (result: SyncResult<SyncStart>) : FetchCounts =
        (expectFetched result).Counts

    /// Eine Comdirect-Buchung, die schon im Kassenbuch steht (eigener Testaufbau, wie ein früherer Abruf).
    let seedImported (account: LedgerAccountId) (reference: string) (more: EntryId -> LedgerEvent list) : EntryId =
        let entryId = newEntryId ()
        let created =
            { EntryId = entryId
              OccurredAt = DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc)
              Actor = ComdirectImport
              Body =
                EntryImportedFromComdirect {
                    AccountId = account
                    ImportId = importIdOf reference
                    ComdirectReference = reference
                    Date = DateTime(2026, 10, 30)
                    Amount = { Amount = -12.34m; Currency = "EUR" }
                    Payee = Some "Markt"
                    Memo = Some "Karte"
                    Cleared = Uncleared
                    Approved = false
                    FlagColor = None
                    FlagName = None
                } }
        Ledger.Persistence.appendAndApply (created :: more entryId) |> run
        entryId

    let at = DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc)
    let ev (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
        { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

[<Tests>]
let bookingTests =
    let testCase name body =
        testCase name (fun () ->
            Ledger.Persistence.initializeLedgerSchema ()
            body ())
    testList "Abruf bucht ins Kassenbuch und nach YNAB" [

        testCase "drei neue Umsätze: je Buchung Entstehung, Kategorisierung, ein Push und die Verknüpfung" <| fun () ->
            let probe = Probe(connected)
            let ruleRef, orderRef, plainRef = uniqueRef "regel", uniqueRef "bestellung", uniqueRef "nichts"
            let groceries = Guid.NewGuid()
            let electronics = Guid.NewGuid()
            let orderId = "302-1234567-7654321"
            probe.Rules <- [ ruleFor "Rewe" groceries ]
            probe.YnabAnswer <- Some [ ynabTransaction $"Amazon {orderId}" -99m (Some (electronics, "Elektronik")) ]
            probe.FetchAnswer <-
                Ok [ bankTransaction ruleRef "Rewe Markt" "Einkauf"
                     bankTransaction orderRef "AMAZON" $"Bestellung {orderId}"
                     bankTransaction plainRef "Kiosk" "Zeitung" ]
            let result = startSyncWith (probe.Build()) |> run
            Expect.equal
                (Booking.counts result)
                { emptyFetchCounts with NewEntries = 3; ByRule = 1; ByOrderId = 1; WithoutCategory = 1 }
                "3 neu, 1 per Regel, 1 aus Bestellnummer, 1 ohne Kategorie"
            Expect.equal probe.Pushes.Count 1 "ein Push"
            Expect.equal
                (Booking.pushedImportIds probe |> List.sort)
                ([ ruleRef; orderRef; plainRef ] |> List.map Booking.importIdOf |> List.sort)
                "mit den drei Buchungen"
            let ruleEntry = Booking.entryOf ruleRef |> Option.get
            let orderEntry = Booking.entryOf orderRef |> Option.get
            let plainEntry = Booking.entryOf plainRef |> Option.get
            Expect.equal (Booking.typesOf ruleEntry) [ "EntryImportedFromComdirect"; "EntryCategorizedByRule"; "EntryLinkedToYnab" ] "Regel"
            Expect.equal (Booking.typesOf orderEntry) [ "EntryImportedFromComdirect"; "EntryRecategorized"; "EntryLinkedToYnab" ] "Bestellnummer"
            Expect.equal ((Booking.eventsOf orderEntry).[1].Actor) (Ledger.Events.OrderIdMatch orderId) "Actor des Order-ID-Abgleichs"
            Expect.equal (Booking.typesOf plainEntry) [ "EntryImportedFromComdirect"; "EntryLinkedToYnab" ] "ohne Kategorie"
            match (Booking.eventsOf plainEntry).Head.Body with
            | Ledger.Events.EntryImportedFromComdirect p ->
                Expect.isFalse p.Approved "zu prüfen"
                Expect.equal p.Cleared Ledger.Events.Uncleared "offen"
            | other -> failtestf "Entstehung erwartet: %A" other

        // categorization-hz6vj: eine nicht lesbare Kategorie-Id in der YNAB-Historie brach den ganzen Abruf ab
        // (Regressionstest auf der reinen Ebene in OrderIdMatcherTests).
        testCase "eine Kategorie-Id ohne Guid in der YNAB-Historie: der Abruf bucht, die Buchung bekommt keinen Vorschlag" <| fun () ->
            let probe = Probe(connected)
            let reference = uniqueRef "kaputte-historie"
            let orderId = "302-7654321-1234567"
            probe.YnabAnswer <-
                Some [ { ynabTransaction $"Amazon {orderId}" -99m None with CategoryId = Some "keine-guid"; CategoryName = Some "Elektronik" } ]
            probe.FetchAnswer <- Ok [ bankTransaction reference "AMAZON" $"Bestellung {orderId}" ]
            let counts = startSyncWith (probe.Build()) |> run |> Booking.counts
            Expect.equal counts { emptyFetchCounts with NewEntries = 1; WithoutCategory = 1 } "gebucht, ohne Vorschlag"
            let entryId = Booking.entryOf reference |> Option.get
            Expect.equal (Booking.typesOf entryId) [ "EntryImportedFromComdirect"; "EntryLinkedToYnab" ] "keine Kategorisierung"

        testCase "eine in YNAB gefundene Reference ist ein sicheres Duplikat: keine Buchung, kein Push" <| fun () ->
            let probe = Probe(connected)
            let reference = uniqueRef "doppelt"
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte" ]
            probe.YnabAnswer <- Some [ ynabTransaction $"Karte, Ref: {reference}" -12.34m None ]
            let counts = startSyncWith (probe.Build()) |> run |> Booking.counts
            Expect.equal counts { emptyFetchCounts with SafeDuplicatesSkipped = 1 } "1 sicheres Duplikat"
            Expect.isNone (Booking.entryOf reference) "keine Buchung"
            Expect.isEmpty probe.Pushes "kein Push"

        testCase "ein unsicheres Duplikat wartet im Kassenbuch mit Grund, Treffer und Schnappschuss und geht nicht nach YNAB" <| fun () ->
            let probe = Probe(connected)
            let reference, other = uniqueRef "fuzzy", uniqueRef "normal"
            let matchYnabId = $"ynab-{Guid.NewGuid()}"
            // Der Treffer steht schon im Kassenbuch, über die `ynab`-Verknüpfung auffindbar.
            let matchEntry =
                Booking.seedImported probe.LedgerAccount (uniqueRef "treffer") (fun id ->
                    [ Booking.ev id Ledger.Events.ComdirectImport (Ledger.Events.EntryLinkedToYnab { YnabTransactionId = matchYnabId }) ])
            let matched =
                { ynabTransaction "Wocheneinkauf" -12.34m (Some (Guid.NewGuid(), "Lebensmittel")) with
                    Id = matchYnabId
                    Date = DateTime(2026, 10, 29)
                    Payee = Some "Markt" }
            probe.YnabAnswer <- Some [ matched ]
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte"; { bankTransaction other "Kiosk" "Zeitung" with Amount = { Amount = -3m; Currency = "EUR" } } ]
            let counts = startSyncWith (probe.Build()) |> run |> Booking.counts
            Expect.equal counts { emptyFetchCounts with NewEntries = 2; WithoutCategory = 2; Held = 1 } "1 wartend"
            let held = Booking.entryOf reference |> Option.get
            Expect.equal (Booking.typesOf held) [ "EntryImportedFromComdirect"; "EntryHeldAsPossibleDuplicate" ] "wartet statt Push"
            let heldEvent = Booking.eventsOf held |> List.last
            Expect.equal heldEvent.Actor Ledger.Events.ComdirectImport "Actor des Imports"
            match heldEvent.Body with
            | Ledger.Events.EntryHeldAsPossibleDuplicate p ->
                Expect.stringContains p.Reason "29.10.2026" "der Grund nennt den Treffer"
                Expect.equal p.MatchYnabTransactionId matchYnabId "YNAB-Id des Treffers"
                Expect.equal p.MatchEntryId (Some matchEntry) "Buchung des Treffers im Kassenbuch"
                Expect.equal
                    p.Match
                    { Date = DateTime(2026, 10, 29); Amount = { Amount = -12.34m; Currency = "EUR" }; Payee = Some "Markt"; CategoryName = Some "Lebensmittel" }
                    "Schnappschuss"
            | body -> failtestf "EntryHeldAsPossibleDuplicate erwartet: %A" body
            Expect.equal (Booking.pushedImportIds probe) [ Booking.importIdOf other ] "nur die andere Buchung geht nach YNAB"

        testCase "scheitert der Push, bleiben die Buchungen mit je einem Fehlschlag und stehen unter Buchung nicht in YNAB" <| fun () ->
            let probe = Probe(connected)
            let reference = uniqueRef "pushfehler"
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte" ]
            probe.PushAnswer <- Some (fun _ -> Error (YnabError.NetworkError "Timeout"))
            let counts = startSyncWith (probe.Build()) |> run |> Booking.counts
            Expect.equal counts { emptyFetchCounts with NewEntries = 1; WithoutCategory = 1; NotInYnab = 1 } "1 nicht in YNAB"
            let entryId = Booking.entryOf reference |> Option.get
            Expect.equal (Booking.typesOf entryId) [ "EntryImportedFromComdirect"; "EntryPushToYnabFailed" ] "Fehlschlag festgehalten"
            let notInYnab = Ledger.QueryHandlers.getEntriesNotInYnab () |> run
            Expect.isTrue (notInYnab |> List.exists (fun e -> Ledger.Events.EntryId e.EntryId = entryId)) "Zu-tun-Eintrag 5 zeigt sie"

        testCase "ohne YNAB-Abgleich wird nichts gebucht, die Sitzung scheitert mit der Meldung" <| fun () ->
            let probe = Probe(connected)
            let reference = uniqueRef "ohne-ynab"
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte" ]
            probe.YnabAnswer <- None
            match startSyncWith (probe.Build()) |> run with
            | Error (SyncError.YnabImportFailed (_, message)) ->
                Expect.equal message "YNAB ist nicht erreichbar, es wurde nichts gebucht." "Meldung aus ADR 0093 Punkt 8"
            | other -> failtestf "YnabImportFailed erwartet: %A" other
            Expect.isNone (Booking.entryOf reference) "kein Ereignis"
            Expect.isEmpty probe.Pushes "kein Push"
            Expect.equal probe.Sessions.Status (Some (Failed ynabUnavailableMessage)) "Sitzung gescheitert"

        testCase "scheitert eine Kassenbuch-Invariante beim Schreiben, endet die Sitzung gescheitert statt im Buchen" <| fun () ->
            let probe = Probe(connected)
            probe.Book <- fun now push trigger account candidates -> async {
                let! real = Ledger.CommandHandlers.pushComdirectImports now push trigger account candidates
                return
                    { real with
                        Outcomes = real.Outcomes |> Map.map (fun _ _ -> Ledger.CommandHandlers.LedgerWriteFailed "Kategorie und Splits schließen sich aus") } }
            match startSyncWith (probe.Build()) |> run with
            | Error (SyncError.YnabImportFailed (1, message)) ->
                Expect.stringContains message "Kategorie und Splits" "die Meldung des Kassenbuchs"
                Expect.stringContains message "für 1 Buchung gescheitert" "die Zahl gebeugt"
            | other -> failtestf "YnabImportFailed erwartet: %A" other
            match probe.Sessions.Status with
            | Some (Failed message) -> Expect.stringContains message "Kassenbuch" "sichtbar gescheitert"
            | other -> failtestf "Failed erwartet, nie ImportingToYnab: %A" other
            Expect.equal (probe.Sessions.Changed |> Seq.last |> fun s -> s.Status |> function Failed _ -> true | _ -> false) true "gespeichert"

        // infrastructure-p747v: `DateTime.Today` lieferte im UTC-Container den Vortag, die Buchung von heute blieb als
        // Zukunft liegen. Vorher in YnabClientTests (`prepareForImport`), seit banking-import-d0fey trägt der Abruf
        // den Berliner Tag aus `Today` bis ins Vorbereiten.
        testCase "eine Buchung vom Berliner Tag wird gebucht, auch wenn die Uhr in UTC noch den Vortag zeigt" <| fun () ->
            let probe = Probe(connected)
            let reference = uniqueRef "heute"
            probe.FetchAnswer <- Ok [ { bankTransaction reference "Markt" "Karte" with BookingDate = DateTime(2026, 11, 2) } ]
            let ports =
                { probe.Build() with
                    Clock = fun () -> DateTime(2026, 11, 1, 23, 30, 0, DateTimeKind.Utc)
                    Today = fun () -> DateTime(2026, 11, 2) }
            let counts = startSyncWith ports |> run |> Booking.counts
            Expect.equal counts.NewEntries 1 "gebucht"
            Expect.equal (Booking.pushedImportIds probe) [ Booking.importIdOf reference ] "gesendet"

        // Review-Fund zu banking-import-d0fey: ein Umsatz nach dem Berliner Tag fiel still aus Abruf und Zählung, das
        // Ergebnis hieß „Nichts Neues“.
        testCase "ein Umsatz nach dem Berliner Tag wird nicht gebucht und als später gezählt" <| fun () ->
            let probe = Probe(connected)
            let future, due = uniqueRef "morgen", uniqueRef "heute"
            probe.FetchAnswer <-
                Ok [ { bankTransaction future "Markt" "Karte" with BookingDate = today.AddDays 1.0 }
                     { bankTransaction due "Kiosk" "Zeitung" with BookingDate = today } ]
            let counts = startSyncWith (probe.Build()) |> run |> Booking.counts
            Expect.equal counts { emptyFetchCounts with NewEntries = 1; WithoutCategory = 1; FutureDated = 1 } "1 neu, 1 später"
            Expect.isNone (Booking.entryOf future) "nicht im Kassenbuch"
            Expect.equal (Booking.pushedImportIds probe) [ Booking.importIdOf due ] "nur der fällige geht nach YNAB"

        // Ein unsicheres Duplikat mit Datum in der Zukunft wartet nicht: YNAB nähme es noch nicht an, und am Tag des
        // Umsatzes prüft der Abruf gegen den dann aktuellen YNAB-Stand.
        testCase "ein unsicheres Duplikat nach dem Berliner Tag wartet nicht, es zählt als später" <| fun () ->
            let probe = Probe(connected)
            let reference = uniqueRef "fuzzy-morgen"
            let tomorrow = today.AddDays 1.0
            probe.YnabAnswer <- Some [ { ynabTransaction "Wocheneinkauf" -12.34m None with Date = tomorrow; Payee = Some "Markt" } ]
            probe.FetchAnswer <- Ok [ { bankTransaction reference "Markt" "Karte" with BookingDate = tomorrow } ]
            let counts = startSyncWith (probe.Build()) |> run |> Booking.counts
            Expect.equal counts { emptyFetchCounts with FutureDated = 1 } "nur später"
            Expect.isNone (Booking.entryOf reference) "nicht im Kassenbuch"
            Expect.isEmpty probe.Pushes "kein Push"
    ]

[<Tests>]
let holdTests =
    testList "Unsicheres Duplikat: das Ereignis zum Warten" [

        // Review-Fund zu banking-import-d0fey: fand `possibleMatchOf` den Treffer der Erkennung nicht, lieferte `holdOf`
        // None und die Buchung ging still nach YNAB.
        testCase "ohne wiedergefundenen Treffer wartet die Buchung mit dem Schnappschuss der Erkennung" <| fun () ->
            let tx = bankTransaction (uniqueRef "ohne-treffer") "Markt" "Karte"
            let details =
                { emptyDetectionDetails tx.Reference with
                    FuzzyMatchDate = Some (DateTime(2026, 10, 29))
                    FuzzyMatchAmount = Some -12.34m
                    FuzzyMatchPayee = Some "Markt" }
            let syncTx =
                { (Server.RulesEngine.classifyTransactions now.UtcDateTime [] [ tx ] |> Result.defaultValue [] |> List.exactlyOne) with
                    DuplicateStatus = PossibleDuplicate ("ähnlich", details) }
            match holdOf [] Map.empty syncTx with
            | Some payload ->
                Expect.equal
                    payload.Match
                    { Date = DateTime(2026, 10, 29); Amount = { Amount = -12.34m; Currency = "EUR" }; Payee = Some "Markt"; CategoryName = None }
                    "Schnappschuss aus der Erkennung"
                Expect.stringContains payload.Reason "29.10.2026" "der Grund nennt den Tag"
                Expect.equal payload.MatchYnabTransactionId "" "keine YNAB-Id bekannt"
            | None -> failtest "die Buchung muss warten"
    ]

[<Tests>]
let repeatedFetchTests =
    let testCase name body =
        testCase name (fun () ->
            Ledger.Persistence.initializeLedgerSchema ()
            body ())
    // ADR 0093 Punkt 7: dieselbe Reference kommt bei jedem Abruf im Zeitraum wieder.
    testList "Wiederholter Abruf: Abgleich mit dem Kassenbuch über die import_id" [

        testCase "wartend und verworfen: kein Ereignis, kein Push" <| fun () ->
            let probe = Probe(connected)
            let heldRef, discardedRef = uniqueRef "wartend", uniqueRef "verworfen"
            let heldPayload : Ledger.Events.EntryHeldAsPossibleDuplicatePayload =
                { Reason = "ähnlich"; MatchYnabTransactionId = "ynab-t"; MatchEntryId = None
                  Match = { Date = DateTime(2026, 10, 29); Amount = { Amount = -12.34m; Currency = "EUR" }; Payee = Some "Markt"; CategoryName = None } }
            let held =
                Booking.seedImported probe.LedgerAccount heldRef (fun id ->
                    [ Booking.ev id Ledger.Events.ComdirectImport (Ledger.Events.EntryHeldAsPossibleDuplicate heldPayload) ])
            // Verworfen über den echten Befehl „Ist doppelt, verwerfen“ (ledger-r7h2w,
            // `EntryDiscardedAsDuplicateInBb`).
            let discarded =
                Booking.seedImported probe.LedgerAccount discardedRef (fun id ->
                    [ Booking.ev id Ledger.Events.ComdirectImport (Ledger.Events.EntryHeldAsPossibleDuplicate heldPayload) ])
            Expect.equal
                (Ledger.CommandHandlers.discardDuplicateEntry (DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc)) discarded |> run)
                (Ok ())
                "verworfen"
            Expect.equal (List.last (Booking.typesOf discarded)) "EntryDiscardedAsDuplicateInBb" "das echte Ereignis"
            let before = Booking.typesOf held, Booking.typesOf discarded
            probe.FetchAnswer <- Ok [ bankTransaction heldRef "Markt" "Karte"; bankTransaction discardedRef "Markt" "Karte" ]
            let counts = startSyncWith (probe.Build()) |> run |> Booking.counts
            Expect.equal counts emptyFetchCounts "nichts Neues"
            Expect.equal (Booking.typesOf held, Booking.typesOf discarded) before "kein Ereignis"
            Expect.isEmpty probe.Pushes "kein Push"

        testCase "verknüpft: schon importiert, nicht erneut gesendet" <| fun () ->
            let probe = Probe(connected)
            let reference = uniqueRef "verknuepft"
            let linked =
                Booking.seedImported probe.LedgerAccount reference (fun id ->
                    [ Booking.ev id Ledger.Events.ComdirectImport (Ledger.Events.EntryLinkedToYnab { YnabTransactionId = $"ynab-{reference}" }) ])
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte" ]
            let counts = startSyncWith (probe.Build()) |> run |> Booking.counts
            Expect.equal counts emptyFetchCounts "nichts Neues"
            Expect.equal (Booking.typesOf linked) [ "EntryImportedFromComdirect"; "EntryLinkedToYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Pushes "kein Push"

        testCase "unverknüpft nach gescheitertem Push: erneut gesendet, dieselbe Buchung wird verknüpft" <| fun () ->
            let probe = Probe(connected)
            let reference = uniqueRef "erneut"
            let failed =
                Booking.seedImported probe.LedgerAccount reference (fun id ->
                    [ Booking.ev id Ledger.Events.ComdirectImport (Ledger.Events.EntryPushToYnabFailed { Reason = "Timeout" }) ])
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte" ]
            let counts = startSyncWith (probe.Build()) |> run |> Booking.counts
            Expect.equal counts emptyFetchCounts "keine neue Buchung"
            Expect.equal (Booking.pushedImportIds probe) [ Booking.importIdOf reference ] "erneut gesendet"
            Expect.equal (Booking.entryOf reference) (Some failed) "keine Reference entsteht zweimal"
            Expect.equal (Booking.typesOf failed) [ "EntryImportedFromComdirect"; "EntryPushToYnabFailed"; "EntryLinkedToYnab" ] "verknüpft"

        // Review-Fund zu banking-import-d0fey: der erneute Versand nahm die frisch klassifizierten Bankdaten, eine in BB
        // geänderte Kategorie ging verloren.
        testCase "Kategorie in BB geändert, Push gescheitert: der nächste Abruf sendet BBs Kategorie" <| fun () ->
            let probe = Probe(connected)
            let reference = uniqueRef "bb-kategorie"
            let ruleCategory, bbCategory = Guid.NewGuid(), Guid.NewGuid()
            probe.Rules <- [ ruleFor "Markt" ruleCategory ]
            let bbRef : Ledger.Events.CategoryRef = { CategoryId = YnabCategoryId bbCategory; CategoryName = "Haushalt" }
            let failed =
                Booking.seedImported probe.LedgerAccount reference (fun id ->
                    [ Booking.ev id Ledger.Events.UserInBb (Ledger.Events.EntryRecategorized {
                          OldCategory = None; NewCategory = Some bbRef; OldSplits = []; NewSplits = []; OldPayee = None; NewPayee = None })
                      Booking.ev id Ledger.Events.ComdirectImport (Ledger.Events.EntryPushToYnabFailed { Reason = "Timeout" }) ])
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte" ]
            startSyncWith (probe.Build()) |> run |> Booking.counts |> ignore
            let sent = probe.Pushes |> Seq.collect id |> Seq.exactlyOne
            Expect.equal sent.ImportId (Booking.importIdOf reference) "dieselbe import_id"
            Expect.equal sent.Request.CategoryId (Some (bbCategory.ToString())) "BBs Kategorie, nicht die der Regel"
            Expect.equal (Booking.typesOf failed |> List.last) "EntryLinkedToYnab" "verknüpft"
    ]

// ============================================
// Automatisch abrufen (banking-import-t4raw, ADR 0093 Punkt 9)
// ============================================
// Derselbe Abruf wie „Abrufen“ mit denselben Fake-Ports; die Historie ist der `SessionsProbe`, der Beginn des
// jüngsten Abrufs ein Funktionswert. Die Uhr steht auf `now`.

let private minutesAgo (minutes: float) = Some (now.UtcDateTime.AddMinutes -minutes)

let private schedule360 : AutoFetchSchedule = { Enabled = true; IntervalMinutes = 360 }

/// Ports eines Takts über einer Probe. `Log` sammelt die Zeilen.
type private AutoFetchProbe(ports: ImportPorts) =
    member val Schedule = schedule360 with get, set
    member val Latest : DateTime option = None with get, set
    member val Logs = ResizeArray<string>()
    member this.Ports : AutoFetchPorts =
        { Import = ports
          LoadSchedule = fun () -> async.Return this.Schedule
          LatestFetch = fun () -> async.Return this.Latest
          Log = fun line -> lock this.Logs (fun () -> this.Logs.Add line) }
    member this.Tick () = runAutoFetchWith this.Ports |> run

let private autoFetchProbe (initial: BankConnectionState) =
    let probe = Probe(initial)
    let ports = probe.Build()
    probe, ports, AutoFetchProbe(ports)

/// Die Einträge des Laufs in der Historie: begonnen und Endstand.
let private historyOf (probe: Probe) =
    List.ofSeq probe.Sessions.Started, probe.Sessions.Changed |> Seq.map (fun s -> s.Status) |> List.ofSeq

let private expectFailedEntry (probe: Probe) =
    let started, changed = historyOf probe
    Expect.equal (started |> List.map (fun s -> s.Trigger)) [ Scheduled ] "ein automatischer Eintrag"
    match changed with
    | [ Failed message ] -> message
    | other -> failtestf "ein Eintrag gescheitert erwartet: %A" other

[<Tests>]
let autoFetchDueTests =
    testList "Automatisch abrufen: Fälligkeit" [
        test "nach 360 und 361 Minuten fällig, nach 359 nicht" {
            let at = now.UtcDateTime
            Expect.isTrue (isAutoFetchDue at schedule360 (minutesAgo 361.0)) "361"
            Expect.isTrue (isAutoFetchDue at schedule360 (minutesAgo 360.0)) "genau das Intervall"
            Expect.isFalse (isAutoFetchDue at schedule360 (minutesAgo 359.0)) "359"
        }
        test "ohne Abruf in der Historie sofort fällig, abgeschaltet nie" {
            let at = now.UtcDateTime
            Expect.isTrue (isAutoFetchDue at schedule360 None) "keine Historie"
            Expect.isFalse (isAutoFetchDue at { schedule360 with Enabled = false } None) "aus"
            Expect.isFalse (isAutoFetchDue at { schedule360 with Enabled = false } (minutesAgo 5000.0)) "aus, lange her"
        }
        test "das Intervall aus den Einstellungen zählt" {
            let at = now.UtcDateTime
            Expect.isTrue (isAutoFetchDue at { schedule360 with IntervalMinutes = 60 } (minutesAgo 60.0)) "eine Stunde"
            Expect.isFalse (isAutoFetchDue at { schedule360 with IntervalMinutes = 1440 } (minutesAgo 1439.0)) "ein Tag"
        }
    ]

[<Tests>]
let autoFetchRunTests =
    let testCase name body =
        testCase name (fun () ->
            Ledger.Persistence.initializeLedgerSchema ()
            body ())
    testList "Automatisch abrufen: der Lauf" [

        testCase "Schalter aus: kein Bankzugriff, kein Eintrag in der Historie" <| fun () ->
            let probe, _, auto = autoFetchProbe connected
            auto.Schedule <- { schedule360 with Enabled = false }
            Expect.equal (auto.Tick ()) AutoFetchOff "aus"
            Expect.equal probe.Fetches 0 "kein Bankzugriff"
            Expect.isEmpty probe.Sessions.Started "kein Eintrag"

        testCase "fällig und verbunden: der Abruf bucht ins Kassenbuch und nach YNAB, ein Eintrag Scheduled" <| fun () ->
            let probe, _, auto = autoFetchProbe connected
            let reference = uniqueRef "auto"
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte" ]
            auto.Latest <- minutesAgo 361.0
            let expected = { emptyFetchCounts with NewEntries = 1; WithoutCategory = 1 }
            Expect.equal (auto.Tick ()) (AutoFetchRan (Ok expected)) "gebucht"
            Expect.equal probe.Logins 0 "keine Challenge"
            let entryId = Booking.entryOf reference |> Option.get
            Expect.equal (Booking.typesOf entryId) [ "EntryImportedFromComdirect"; "EntryLinkedToYnab" ] "Kassenbuch zuerst, dann verknüpft"
            Expect.isTrue (Booking.eventsOf entryId |> List.forall (fun e -> e.Actor = Ledger.Events.ComdirectImport)) "Actor ComdirectImport"
            Expect.equal (Booking.pushedImportIds probe) [ Booking.importIdOf reference ] "nach YNAB"
            let started, changed = historyOf probe
            Expect.equal (started |> List.map (fun s -> s.Trigger, s.StartedAt)) [ Scheduled, now.UtcDateTime ] "Eintrag automatisch, Beginn aus der Uhr"
            Expect.equal changed [ Completed ] "beendet"
            Expect.equal (Seq.last probe.Sessions.Changed).Counts expected "mit den Zahlen"
            Expect.isNone probe.Sessions.Current "die Sitzung von „Abrufen“ bleibt unberührt"

        testCase "jüngster Abruf vor 359 Minuten: kein Bankzugriff" <| fun () ->
            let probe, _, auto = autoFetchProbe connected
            auto.Latest <- minutesAgo 359.0
            Expect.equal (auto.Tick ()) AutoFetchNotDue "nicht fällig"
            Expect.equal probe.Fetches 0 "kein Bankzugriff"
            Expect.isEmpty probe.Sessions.Started "kein Eintrag"

        testCase "ein Abruf von Hand vor 10 Minuten: kein automatischer Lauf" <| fun () ->
            let probe, _, auto = autoFetchProbe connected
            auto.Latest <- minutesAgo 10.0
            Expect.equal (auto.Tick ()) AutoFetchNotDue "nicht fällig"
            Expect.equal probe.Fetches 0 "kein Bankzugriff"

        testCase "keine Historie: ein Abruf läuft" <| fun () ->
            let probe, _, auto = autoFetchProbe connected
            match auto.Tick () with
            | AutoFetchRan (Ok _) -> ()
            | other -> failtestf "Lauf erwartet: %A" other
            Expect.equal probe.Fetches 1 "ein Abruf"

        for label, state in [ "Disconnected", Disconnected; "ChallengePending", pendingChallenge ] do
            testCase $"{label}: keine Challenge, kein Passwort-Login, kein Abruf, kein Eintrag, der nächste Takt prüft wieder" <| fun () ->
                let probe, _, auto = autoFetchProbe state
                Expect.equal (auto.Tick ()) (AutoFetchSkipped BankNotConnected) "übersprungen"
                Expect.equal probe.Logins 0 "keine Challenge, kein Passwort-Login"
                Expect.equal probe.Fetches 0 "kein Abruf"
                Expect.isEmpty probe.Sessions.Started "kein Eintrag"
                probe.Connection.Cell.State.Value <- connected
                match auto.Tick () with
                | AutoFetchRan (Ok _) -> ()
                | other -> failtestf "der nächste Takt ruft ab: %A" other

        testCase "die Verbindung stirbt im Abruf: keine Challenge, ein Eintrag gescheitert, nichts gebucht" <| fun () ->
            let probe, _, auto = autoFetchProbe connected
            let reference = uniqueRef "stirbt"
            probe.FetchAnswer <- Error rejected
            probe.RefreshAnswer <- Error (ReloginRequired "HTTP 401")
            match auto.Tick () with
            | AutoFetchRan (Error _) -> ()
            | other -> failtestf "gescheitert erwartet: %A" other
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.isTrue (isDisconnected probe.Connection) "die Verbindung ist weg"
            expectFailedEntry probe |> ignore
            Expect.isEmpty probe.Pushes "nichts nach YNAB"
            Expect.isNone (Booking.entryOf reference) "nichts gebucht"

        testCase "ein Bankfehler bei lebender Verbindung: nichts gebucht, gescheitert, Log mit Status und Code" <| fun () ->
            let probe, _, auto = autoFetchProbe connected
            probe.FetchAnswer <- Error (ComdirectError.NetworkError (503, "service_unavailable"))
            match auto.Tick () with
            | AutoFetchRan (Error _) -> ()
            | other -> failtestf "gescheitert erwartet: %A" other
            Expect.equal probe.Logins 0 "keine Challenge"
            Expect.isTrue (isConnected probe.Connection) "die Verbindung lebt"
            let message = expectFailedEntry probe
            Expect.stringContains message "Bankfehler" "die Meldung des Bankfehlers"
            Expect.isEmpty probe.Pushes "nichts nach YNAB"
            // Review-Fund zu banking-import-t4raw: eine Zeile mit Status und Code, die Meldung nicht noch einmal.
            let line = Expect.wantSome (Seq.tryExactlyOne auto.Logs) "genau eine Zeile im Log"
            Expect.stringContains line "HTTP 503, Code service_unavailable" "Status und Code im Log"
            Expect.isFalse (line.Contains message) "die Meldung steht nicht zusätzlich im Log"
            // Der Eintrag zählt als jüngster Abruf: der nächste Versuch folgt nach dem Intervall.
            let started = (Seq.exactlyOne probe.Sessions.Started).StartedAt
            Expect.isFalse (isAutoFetchDue (started.AddMinutes 359.0) schedule360 (Some started)) "nicht vor dem Intervall"

        testCase "ohne YNAB-Abgleich bucht der Lauf nichts und scheitert mit Grund" <| fun () ->
            let probe, _, auto = autoFetchProbe connected
            let reference = uniqueRef "ohne-ynab"
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte" ]
            probe.YnabAnswer <- None
            Expect.equal (auto.Tick ()) (AutoFetchRan (Error ynabUnavailableMessage)) "gescheitert mit Grund"
            Expect.equal (expectFailedEntry probe) ynabUnavailableMessage "Grund in der Historie"
            Expect.isNone (Booking.entryOf reference) "nichts gebucht"
            Expect.isEmpty probe.Pushes "nichts nach YNAB"

        testCase "ein Abruf von Hand hält das Gate: der Lauf wartet nicht, überspringt und bleibt fällig" <| fun () ->
            let probe, ports, auto = autoFetchProbe connected
            ports.Gate.Wait()
            try
                Expect.equal (auto.Tick ()) (AutoFetchSkipped FetchRunning) "übersprungen"
                Expect.equal probe.Fetches 0 "kein Abruf"
                Expect.isEmpty probe.Sessions.Started "kein Eintrag"
            finally
                ports.Gate.Release() |> ignore
            match auto.Tick () with
            | AutoFetchRan (Ok _) -> ()
            | other -> failtestf "danach fällig: %A" other

        testCase "„Abrufen“ wartet auf einen laufenden automatischen Lauf und findet dessen Buchung im Kassenbuch" <| fun () ->
            let probe, ports, auto = autoFetchProbe connected
            let reference = uniqueRef "beide"
            probe.FetchAnswer <- Ok [ bankTransaction reference "Markt" "Karte" ]
            let barrier = Barrier()
            let held = ref 0
            probe.DuringFetch <- fun () ->
                if Interlocked.Increment(held) = 1 then barrier.Hold () else async.Return ()
            let scheduled = runAutoFetchWith auto.Ports |> Async.StartAsTask
            Expect.isTrue (barrier.WaitReached ()) "der automatische Lauf ruft ab"
            let manual = startSyncWith ports |> Async.StartAsTask
            Expect.isFalse (manual.Wait 200) "„Abrufen“ wartet am Gate"
            barrier.Release ()
            Expect.isTrue (scheduled.Wait hangLimit) "der automatische Lauf endet"
            Expect.isTrue (manual.Wait hangLimit) "„Abrufen“ endet"
            Expect.equal scheduled.Result (AutoFetchRan (Ok { emptyFetchCounts with NewEntries = 1; WithoutCategory = 1 })) "automatisch gebucht"
            let session = expectFetched manual.Result
            Expect.equal session.Trigger Manual "eigene Sitzung von Hand"
            Expect.equal session.Counts.NewEntries 0 "schon im Kassenbuch"
            Expect.equal probe.Fetches 2 "beide fragten die Bank"
            Expect.equal (Booking.pushedImportIds probe) [ Booking.importIdOf reference ] "ein Push, keine Reference zweimal"
            Expect.isSome (Booking.entryOf reference) "eine Buchung"
    ]

[<Tests>]
let autoFetchWorkerTests =
    testList "Automatisch abrufen: Hintergrunddienst" [
        testCase "ein Fehler in einem Takt beendet den Dienst nicht" <| fun () ->
            let calls = ref 0
            let secondCall = TaskCompletionSource<unit>()
            let failingTick () = async {
                if Interlocked.Increment(calls) >= 2 then
                    secondCall.TrySetResult () |> ignore
                failwith "Takt scheitert"
            }
            use worker = new Server.AutoFetchWorker.AutoFetchWorker(failingTick, TimeSpan.FromMilliseconds 1.0)
            worker.StartAsync(CancellationToken.None).Wait()
            Expect.isTrue (secondCall.Task.Wait(TimeSpan.FromSeconds 10.0)) "der zweite Takt läuft"
            worker.StopAsync(CancellationToken.None).Wait()

        testCase "ein hängender Takt blockiert das Beenden nicht" <| fun () ->
            let reached = TaskCompletionSource<unit>()
            let never = TaskCompletionSource<unit>()
            let hangingTick () = async {
                reached.TrySetResult () |> ignore
                do! never.Task |> Async.AwaitTask
            }
            use worker = new Server.AutoFetchWorker.AutoFetchWorker(hangingTick, TimeSpan.FromMilliseconds 1.0)
            worker.StartAsync(CancellationToken.None).Wait()
            Expect.isTrue (reached.Task.Wait(TimeSpan.FromSeconds 10.0)) "der Takt läuft"
            Expect.isTrue (worker.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds 10.0)) "das Beenden kehrt zurück"

        // Review-Fund zu banking-import-t4raw: ohne Abbruch-Token lief ein hängender Takt nach dem Beenden weiter.
        testCase "das Beenden bricht einen laufenden Takt ab" <| fun () ->
            let reached = TaskCompletionSource<unit>()
            let cancelled = TaskCompletionSource<unit>()
            let never = TaskCompletionSource<unit>()
            let hangingTick () = async {
                use! _ = Async.OnCancel(fun () -> cancelled.TrySetResult () |> ignore)
                reached.TrySetResult () |> ignore
                do! never.Task |> Async.AwaitTask
            }
            use worker = new Server.AutoFetchWorker.AutoFetchWorker(hangingTick, TimeSpan.FromMilliseconds 1.0)
            worker.StartAsync(CancellationToken.None).Wait()
            Expect.isTrue (reached.Task.Wait(TimeSpan.FromSeconds 10.0)) "der Takt läuft"
            worker.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds 10.0) |> ignore
            Expect.isTrue (cancelled.Task.Wait(TimeSpan.FromSeconds 10.0)) "der Takt bekommt den Abbruch"
    ]

[<Tests>]
let autoFetchHistoryTests =
    testList "Automatisch abrufen: der jüngste Abruf aus der Historie" [
        // Einziger Testfall, der `sync_sessions` der geteilten In-Memory-DB beschreibt; die Zeiten liegen weit
        // nach jeder echten Sitzung.
        testCase "beendet oder gescheitert zählt, bei der TAN stehen geblieben nicht, die Zeit kommt als UTC" <| fun () ->
            global.Persistence.initializeDatabase ()
            let at (day: int) (hour: int) = DateTime(2099, 1, day, hour, 0, 0, DateTimeKind.Utc)
            let session (status: SyncSessionStatus) (started: DateTime) (trigger: SyncTrigger) =
                { Id = SyncSessionId (Guid.NewGuid())
                  StartedAt = started
                  CompletedAt = Some (started.AddMinutes 1.0)
                  Status = status
                  Trigger = trigger
                  Counts = emptyFetchCounts }
            let record s = global.Persistence.SyncSessions.createSession s |> run
            let latest () = global.Persistence.SyncSessions.latestFinishedStartedAt () |> run
            let failed = session (Failed "Bankfehler") (at 1 9) Scheduled
            record failed
            record (session AwaitingTan (at 2 9) Manual)
            Expect.equal (latest ()) (Some (at 1 9)) "gescheitert zählt, die TAN nicht"
            Expect.equal (latest () |> Option.map (fun d -> d.Kind)) (Some DateTimeKind.Utc) "als UTC"
            record (session Completed (at 1 10) Manual)
            Expect.equal (latest ()) (Some (at 1 10)) "der jüngere beendete"
            let stored = global.Persistence.SyncSessions.getSessionById failed.Id |> run
            Expect.equal (stored |> Option.map (fun s -> s.Trigger)) (Some Scheduled) "der Auslöser bleibt"
    ]
