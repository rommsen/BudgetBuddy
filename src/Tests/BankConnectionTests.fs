module Tests.BankConnectionTests

// Bankverbindung als Zustandsautomat (banking-import-tshq9, ADR 0071 Punkt 2, 3 und 8). Alle
// Bankaufrufe sind Fake-Ports, kein Testfall erreicht Comdirect.

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Shared.Domain
open Server.ComdirectClient
open Server.BankConnection
open Server.ComdirectAuthSession
open Server.BankConnectionWorker

let private run (work: Async<'T>) = Async.RunSynchronously work

let private now = DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)
let private today = DateTime(2026, 10, 31)
let private keys = { ClientId = "kuenstliche-id"; ClientSecret = "kuenstliches-secret" }
let private requestInfo = { RequestId = "123456789"; SessionId = "sitzung-kuenstlich" }

let private credentials : ComdirectSettings =
    { ClientId = keys.ClientId
      ClientSecret = keys.ClientSecret
      Username = "kuenstlich"
      Password = "kuenstlich"
      AccountId = None }

let private tokensValidFor (seconds: float) (access: string) =
    { Access = access; Refresh = access + "-refresh"; ExpiresAt = Some (now.AddSeconds seconds) }

let private sessionWith (tokens: Tokens) (challenge: Challenge option) : AuthSession =
    { RequestInfo = requestInfo
      SessionId = requestInfo.SessionId
      Tokens = tokens
      SessionIdentifier = "kennung-kuenstlich"
      Challenge = challenge }

let private challenge = { Id = "challenge-kuenstlich"; Type = "P_TAN_PUSH" }

let private connected (tokens: Tokens) =
    Connected { Session = sessionWith tokens None; Keys = keys; NextRefreshAttempt = None }

let private pending =
    ChallengePending { Session = sessionWith (tokensValidFor 600.0 "vorlaeufig") (Some challenge); Keys = keys }

/// Fake-Ports mit Zählern. Jeder Port antwortet ohne weitere Belegung mit Erfolg, ein unerwarteter
/// Bankzugriff fällt deshalb nur über die Zähler auf. Die Zähler sind threadsicher, damit ein
/// fehlendes Semaphor als falsche Zahl sichtbar wird und nicht als kaputte Liste.
type private Probe(initial: BankConnectionState) =
    member val Refreshes = ResizeArray<Tokens>()
    member val Logins = ref 0
    member val Confirmations = ref 0
    member val Fetches = ResizeArray<Tokens>()
    member val Losses = ResizeArray<string>()
    member val RefreshAnswer : Tokens -> Result<Tokens, RefreshFailure> =
        (fun _ -> Ok (tokensValidFor 598.0 "neu")) with get, set
    member val FetchAnswer : Tokens -> ComdirectResult<BankTransaction list> =
        (fun _ -> Ok []) with get, set
    member val ConfirmAnswer : ComdirectResult<Tokens> =
        Ok (tokensValidFor 598.0 "bestaetigt") with get, set
    member val BeforeRefresh : unit -> Async<unit> = (fun () -> async.Return ()) with get, set
    member val Ports : ConnectionPorts option = None with get, set
    member this.Build() =
        let cell = newConnectionCell ()
        cell.State.Value <- initial
        { Cell = cell
          Login = fun _ _ -> async {
              Interlocked.Increment(this.Logins) |> ignore
              return Ok (sessionWith (tokensValidFor 600.0 "vorlaeufig") (Some challenge)) }
          CompleteAuth = fun _ _ -> async {
              Interlocked.Increment(this.Confirmations) |> ignore
              return this.ConfirmAnswer }
          Refresh = fun _ _ tokens -> async {
              lock this.Refreshes (fun () -> this.Refreshes.Add tokens)
              do! this.BeforeRefresh ()
              return this.RefreshAnswer tokens }
          FetchAccounts = fun _ tokens -> async {
              lock this.Fetches (fun () -> this.Fetches.Add tokens)
              return this.FetchAnswer tokens |> Result.map (fun _ -> []) }
          FetchTransactions = fun _ tokens _ _ _ -> async {
              lock this.Fetches (fun () -> this.Fetches.Add tokens)
              return this.FetchAnswer tokens }
          ReportLoss = this.Losses.Add }

let private isDisconnected (ports: ConnectionPorts) =
    match connectionState ports with
    | Disconnected -> true
    | _ -> false

let private activeConnection (ports: ConnectionPorts) =
    match connectionState ports with
    | Connected connection -> connection
    | other -> failtestf "Connected erwartet, bekam %A" other

let private rejected = ComdirectError.AuthenticationFailed "HTTP 401"

let private fetch (ports: ConnectionPorts) =
    fetchTransactionsWith ports now "konto" 7 today |> run

/// Zeitlimit gegen Hänger. Es entscheidet keinen Testfall, die Abfolge kommt aus Signalen.
let private hangLimit = TimeSpan.FromSeconds 30.0

let private expectFinished (task: Task) (message: string) =
    Expect.isTrue (task.Wait hangLimit) message

/// Der erste Anlass hängt nachweislich im Refresh-Port, wenn `start` den zweiten anstößt.
/// `Async.StartImmediateAsTask` läuft auf diesem Thread bis zur ersten echten Wartestelle: der zweite
/// Anlass steht also am Semaphor, oder ohne Semaphor selbst im Refresh-Port, sobald `start`
/// zurückkehrt. `whileHeld` prüft genau diesen Stand, danach gibt der Test den Refresh frei.
let private withRefreshHeld
    (probe: Probe)
    (first: Async<unit>)
    (start: unit -> Task<'T>)
    (whileHeld: Task<'T> -> unit)
    : Task<'T> =
    let running = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    probe.BeforeRefresh <- fun () -> async {
        running.TrySetResult () |> ignore
        do! release.Task |> Async.AwaitTask
    }
    let firstTask = first |> Async.StartAsTask
    expectFinished running.Task "der erste Anlass erreicht den Refresh-Port"
    let second = start ()
    try
        whileHeld second
    finally
        release.TrySetResult () |> ignore
    expectFinished firstTask "der erste Anlass endet"
    expectFinished second "der zweite Anlass endet"
    second

[<Tests>]
let tickTests =
    testList "Bankverbindung: tick" [
        testCase "Token läuft in unter 60 s ab: genau ein Refresh, das neue Token ist in Verwendung" <| fun () ->
            let probe = Probe(connected (tokensValidFor 59.0 "alt"))
            let ports = probe.Build()
            tick ports now |> run
            Expect.equal probe.Refreshes.Count 1 "genau ein Refresh"
            Expect.equal probe.Refreshes[0].Access "alt" "mit dem alten Token"
            fetch ports |> ignore
            Expect.equal probe.Fetches[0].Access "neu" "der Abruf nutzt das neue Token"

        testCase "Token läuft in genau 60 s ab: noch kein Refresh" <| fun () ->
            let probe = Probe(connected (tokensValidFor 60.0 "alt"))
            tick (probe.Build()) now |> run
            Expect.equal probe.Refreshes.Count 0 "kein Refresh"

        testCase "Ablaufzeit unbekannt: der Refresh ist fällig" <| fun () ->
            let probe = Probe(connected { tokensValidFor 0.0 "alt" with ExpiresAt = None })
            tick (probe.Build()) now |> run
            Expect.equal probe.Refreshes.Count 1 "ein Refresh"

        testCase "ChallengePending: kein Refresh" <| fun () ->
            let probe = Probe(pending)
            let ports = probe.Build()
            tick ports (now.AddHours 1.0) |> run
            Expect.equal probe.Refreshes.Count 0 "kein Refresh"
            Expect.equal (connectionState ports) pending "Challenge bleibt offen"

        testCase "Disconnected: beliebig viele Ticks lösen weder Challenge noch Passwort-Login aus" <| fun () ->
            let probe = Probe(Disconnected)
            let ports = probe.Build()
            for minute in 1 .. 200 do
                tick ports (now.AddMinutes (float minute)) |> run
            Expect.equal probe.Logins.Value 0 "kein Login"
            Expect.equal probe.Confirmations.Value 0 "keine Bestätigung"
            Expect.equal probe.Refreshes.Count 0 "kein Refresh"
            Expect.isTrue (isDisconnected ports) "bleibt getrennt"

        testCase "ReloginRequired: Disconnected, Verlust einmal gemeldet, keine Challenge" <| fun () ->
            let probe = Probe(connected (tokensValidFor 10.0 "alt"))
            probe.RefreshAnswer <- fun _ -> Error (ReloginRequired "HTTP 401: invalid_token")
            let ports = probe.Build()
            tick ports now |> run
            tick ports (now.AddSeconds 15.0) |> run
            tick ports (now.AddSeconds 90.0) |> run
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.equal (List.ofSeq probe.Losses) [ "HTTP 401: invalid_token" ] "einmal gemeldet"
            Expect.equal probe.Refreshes.Count 1 "kein weiterer Refresh"
            Expect.equal probe.Logins.Value 0 "keine Challenge"

        testCase "RetryLater: Connected bleibt, nächster Versuch nach 60 s" <| fun () ->
            let probe = Probe(connected (tokensValidFor 10.0 "alt"))
            probe.RefreshAnswer <- fun _ -> Error (RetryLater "HTTP 503")
            let ports = probe.Build()
            tick ports now |> run
            Expect.equal (activeConnection ports).Session.Tokens.Access "alt" "Verbindung steht"
            tick ports (now.AddSeconds 59.0) |> run
            Expect.equal probe.Refreshes.Count 1 "vor Ablauf der 60 s kein Versuch"
            Expect.isEmpty probe.Losses "kein Verlust"
            probe.RefreshAnswer <- fun _ -> Ok (tokensValidFor 658.0 "neu")
            tick ports (now.AddSeconds 60.0) |> run
            Expect.equal probe.Refreshes.Count 2 "nach 60 s der nächste Versuch"
            Expect.equal (activeConnection ports).Session.Tokens.Access "neu" "neues Token"
            Expect.isNone (activeConnection ports).NextRefreshAttempt "die Pause ist vorbei"

        testCase "zwei gleichzeitige Ticks: ein Aufruf" <| fun () ->
            let probe = Probe(connected (tokensValidFor 10.0 "alt"))
            let ports = probe.Build()
            withRefreshHeld probe (tick ports now)
                (fun () -> tick ports now |> Async.StartImmediateAsTask)
                (fun second ->
                    Expect.isFalse second.IsCompleted "der zweite Tick wartet auf den ersten"
                    Expect.equal probe.Refreshes.Count 1 "kein zweiter Refresh, solange der erste läuft")
            |> ignore
            Expect.equal probe.Refreshes.Count 1 "ein Refresh"
            Expect.equal (activeConnection ports).Session.Tokens.Access "neu" "das neue Token ist in Verwendung"

        testCase "Tick und Datenabruf gleichzeitig: ein Aufruf, beide nutzen das Ergebnis" <| fun () ->
            let probe = Probe(connected (tokensValidFor 10.0 "alt"))
            let ports = probe.Build()
            let fetched =
                withRefreshHeld probe (tick ports now)
                    (fun () -> fetchTransactionsWith ports now "konto" 7 today |> Async.StartImmediateAsTask)
                    (fun fetching ->
                        Expect.isFalse fetching.IsCompleted "der Datenabruf wartet auf den Refresh"
                        Expect.equal probe.Refreshes.Count 1 "kein zweiter Refresh, solange der erste läuft"
                        Expect.equal probe.Fetches.Count 0 "kein Bankzugriff während des Refresh")
            Expect.equal probe.Refreshes.Count 1 "ein Refresh"
            Expect.equal fetched.Result (Ok []) "der Abruf liefert Daten"
            Expect.equal (probe.Fetches |> Seq.map (fun t -> t.Access) |> List.ofSeq) [ "neu" ] "der Abruf nutzt das Ergebnis"
    ]

[<Tests>]
let dataAccessTests =
    testList "Bankverbindung: Datenabruf" [
        for name, error in [ "401", rejected; "403", ComdirectError.SessionExpired ] do
            testCase $"{name}: ein Refresh und ein Wiederholversuch mit neuem Token" <| fun () ->
                let probe = Probe(connected (tokensValidFor 600.0 "alt"))
                probe.FetchAnswer <- fun tokens -> if tokens.Access = "alt" then Error error else Ok []
                let ports = probe.Build()
                Expect.equal (fetch ports) (Ok []) "der Wiederholversuch liefert die Daten"
                Expect.equal probe.Refreshes.Count 1 "ein Refresh"
                Expect.equal (probe.Fetches |> Seq.map (fun t -> t.Access) |> List.ofSeq) [ "alt"; "neu" ] "zwei Versuche"
                Expect.equal (activeConnection ports).Session.Tokens.Access "neu" "Verbindung steht"

            testCase $"{name} auch nach dem Refresh: Disconnected und ein Verlust" <| fun () ->
                let probe = Probe(connected (tokensValidFor 600.0 "alt"))
                probe.FetchAnswer <- fun _ -> Error error
                let ports = probe.Build()
                Expect.equal (fetch ports) (Error error) "der Fehler kommt an"
                Expect.equal probe.Fetches.Count 2 "kein dritter Versuch"
                Expect.equal probe.Refreshes.Count 1 "ein Refresh"
                Expect.equal probe.Losses.Count 1 "ein Verlust"
                Expect.isTrue (isDisconnected ports) "getrennt"
                Expect.equal probe.Logins.Value 0 "keine Challenge"

        testCase "Refresh nach 401 sagt RetryLater: Connected bleibt, kein Wiederholversuch" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            probe.FetchAnswer <- fun _ -> Error rejected
            probe.RefreshAnswer <- fun _ -> Error (RetryLater "HTTP 503")
            let ports = probe.Build()
            Expect.equal (fetch ports) (Error rejected) "der Fehler kommt an"
            Expect.equal probe.Fetches.Count 1 "ohne neues Token kein zweiter Versuch"
            Expect.equal (activeConnection ports).NextRefreshAttempt (Some (now.AddSeconds 60.0)) "nächster Versuch nach 60 s"
            Expect.isEmpty probe.Losses "kein Verlust"

        testCase "Refresh nach 401 sagt ReloginRequired: Disconnected" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            probe.FetchAnswer <- fun _ -> Error rejected
            probe.RefreshAnswer <- fun _ -> Error (ReloginRequired "HTTP 401")
            let ports = probe.Build()
            fetch ports |> ignore
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.equal probe.Losses.Count 1 "ein Verlust"

        testCase "anderer Fehler: kein Refresh, Verbindung bleibt" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            let unavailable = ComdirectError.NetworkError (503, "")
            probe.FetchAnswer <- fun _ -> Error unavailable
            let ports = probe.Build()
            Expect.equal (fetch ports) (Error unavailable) "der Fehler kommt an"
            Expect.equal probe.Refreshes.Count 0 "kein Refresh"
            Expect.equal (activeConnection ports).Session.Tokens.Access "alt" "Verbindung steht"

        testCase "Daten nur aus Connected: bei offener Challenge geht nichts zur Bank" <| fun () ->
            let probe = Probe(pending)
            Expect.equal (fetch (probe.Build())) (Error ComdirectError.SessionExpired) "kein Zugriff"
            Expect.equal probe.Fetches.Count 0 "kein Abruf"
    ]

[<Tests>]
let transitionTests =
    testList "Bankverbindung: Übergänge" [
        testCase "Challenge starten führt nach ChallengePending, bestätigen nach Connected" <| fun () ->
            let probe = Probe(Disconnected)
            let ports = probe.Build()
            let started = startChallengeWith ports credentials |> run
            Expect.equal started (Ok challenge) "Challenge liegt vor"
            Expect.equal (fetch ports) (Error ComdirectError.SessionExpired) "noch kein Datenzugriff"
            Expect.equal (confirmChallengeWith ports |> run) (Ok ()) "bestätigt"
            let connection = activeConnection ports
            Expect.equal connection.Session.Tokens.Access "bestaetigt" "erweiterte Tokens"
            Expect.isNone connection.Session.Challenge "die Challenge ist erledigt"

        testCase "gescheiterte TAN führt nach Disconnected, ohne Verlustmeldung" <| fun () ->
            let probe = Probe(pending)
            probe.ConfirmAnswer <- Error ComdirectError.TanRejected
            let ports = probe.Build()
            Expect.equal (confirmChallengeWith ports |> run) (Error ComdirectError.TanRejected) "der Fehler kommt an"
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.isEmpty probe.Losses "eine nie aktive Verbindung geht nicht verloren"

        testCase "bestätigen bei Connected validiert nicht erneut bei der Bank" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            let ports = probe.Build()
            Expect.equal (confirmChallengeWith ports |> run) (Ok ()) "nichts zu tun"
            Expect.equal probe.Confirmations.Value 0 "zählt nicht auf die Sperre ein"

        testCase "bestätigen ohne Challenge ist ein Fehler" <| fun () ->
            let probe = Probe(Disconnected)
            Expect.isError (confirmChallengeWith (probe.Build()) |> run) "keine Anmeldung offen"
            Expect.equal probe.Confirmations.Value 0 "nichts geht zur Bank"

        testCase "gescheiterte Anmeldung lässt den Zustand unverändert" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            let ports =
                { probe.Build() with Login = fun _ _ -> async.Return (Error ComdirectError.InvalidCredentials) }
            Expect.isError (startChallengeWith ports credentials |> run) "Fehler"
            Expect.equal (activeConnection ports).Session.Tokens.Access "alt" "Verbindung steht"

        testCase "trennen ist kein Verlust" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            let ports = probe.Build()
            disconnectWith ports |> run
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.isEmpty probe.Losses "keine Meldung"
    ]

/// Port-Paar für die Einstellungen, ohne Datenbank.
let private settingsFrom (values: (string * string) list) (key: string) =
    async.Return (values |> List.tryFind (fun (k, _) -> k = key) |> Option.map snd)

let private storedCredentials =
    [ "comdirect_client_id", "id"
      "comdirect_client_secret", "secret"
      "comdirect_username", "name"
      "comdirect_password", "pin" ]

let private savedForm : ComdirectSettings =
    { ClientId = "id"; ClientSecret = "secret"; Username = "name"; Password = "pin"; AccountId = Some "konto" }

let private cancel (ports: ConnectionPorts) =
    let sessionId = SyncSessionId (Guid.NewGuid())
    let cleared = ref false
    let result =
        Server.Api.cancelSyncWith Ok (fun () -> discardPendingChallengeWith ports) (fun () -> cleared.Value <- true) sessionId
        |> run
    result, cleared.Value

let private testConnection (ports: ConnectionPorts) =
    Server.Api.testComdirectConnectionWith (settingsFrom storedCredentials) ports now |> run

let private saveCredentials (ports: ConnectionPorts) (form: ComdirectSettings) =
    Server.Api.saveComdirectCredentialsWith
        (settingsFrom storedCredentials)
        (fun _ _ _ -> async.Return ())
        (fun () -> disconnectWith ports)
        form
    |> run

[<Tests>]
let adapterTests =
    testList "Bankverbindung: Abbruch, Verbindungstest, Zugangsdaten" [
        // Bug: cancelSync löschte auch eine aktive Verbindung.
        testCase "Kaputt: Connected, cancelSync: die Verbindung bleibt" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            let ports = probe.Build()
            let result, cleared = cancel ports
            Expect.equal result (Ok ()) "abgebrochen"
            Expect.isTrue cleared "die Sync-Sitzung ist weg"
            Expect.equal (activeConnection ports).Session.Tokens.Access "alt" "Verbindung steht"

        testCase "Intakt: ChallengePending, cancelSync: Disconnected" <| fun () ->
            let probe = Probe(pending)
            let ports = probe.Build()
            cancel ports |> ignore
            Expect.isTrue (isDisconnected ports) "Challenge verworfen"

        // Bug: ein Verbindungstest startete eine Challenge und ersetzte die aktive Verbindung.
        testCase "Kaputt: Connected, testComdirectConnection: keine Challenge, Verbindung unverändert" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            let ports = probe.Build()
            Expect.equal (testConnection ports) (Ok BankConnectionActive) "Verbindung lebt"
            Expect.equal probe.Logins.Value 0 "kein Login, keine Challenge"
            Expect.equal probe.Fetches.Count 1 "die Konten wurden gelesen"
            Expect.equal (activeConnection ports).Session.Tokens.Access "alt" "Verbindung unverändert"

        testCase "Connected, Konten nicht lesbar: Fehler ohne Challenge, Verbindung bleibt" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            probe.FetchAnswer <- fun _ -> Error (ComdirectError.NetworkError (404, ""))
            let ports = probe.Build()
            Expect.isError (testConnection ports) "der Fehler kommt an"
            Expect.equal probe.Logins.Value 0 "keine Challenge"
            Expect.equal (activeConnection ports).Session.Tokens.Access "alt" "Verbindung steht"

        testCase "Intakt: Disconnected, testComdirectConnection: Challenge wie bisher" <| fun () ->
            let probe = Probe(Disconnected)
            let ports = probe.Build()
            Expect.equal (testConnection ports) (Ok (TanChallengeStarted challenge.Id)) "Challenge gestartet"
            Expect.equal probe.Logins.Value 1 "ein Login"

        for field, changed in
            [ "Client-Id", { savedForm with ClientId = "andere-id" }
              "Client-Secret", { savedForm with ClientSecret = "anderes-secret" }
              "Benutzername", { savedForm with Username = "anderer-name" }
              "Passwort", { savedForm with Password = "andere-pin" } ] do
            testCase $"Zugangsdaten geändert ({field}): Disconnected" <| fun () ->
                let probe = Probe(connected (tokensValidFor 600.0 "alt"))
                let ports = probe.Build()
                Expect.equal (saveCredentials ports changed) (Ok ()) "gespeichert"
                Expect.isTrue (isDisconnected ports) "getrennt"
                Expect.isEmpty probe.Losses "selbst getrennt ist kein Verlust"

        testCase "Secrets im Formular leer: nichts geändert, die Verbindung bleibt" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            let ports = probe.Build()
            saveCredentials ports { savedForm with ClientSecret = ""; Password = "" } |> ignore
            Expect.equal (activeConnection ports).Session.Tokens.Access "alt" "Verbindung steht"

        testCase "nur die Konto-Id geändert: die Verbindung bleibt" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            let ports = probe.Build()
            saveCredentials ports { savedForm with AccountId = Some "anderes-konto" } |> ignore
            Expect.equal (activeConnection ports).Session.Tokens.Access "alt" "Verbindung steht"

        testCase "Einstellungen: aktive Verbindung zeigt Erfolg statt TAN-Wartezustand" <| fun () ->
            let model, _ = Components.Settings.State.init ()
            let updated, _, _ =
                Components.Settings.State.update (Components.Settings.Types.ComdirectAuthStarted (Ok BankConnectionActive)) model
            Expect.isFalse updated.ComdirectAuthPending "keine TAN offen"
            Expect.equal updated.ComdirectConnectionValid (Types.Success ()) "Erfolg"
    ]

[<Tests>]
let workerTests =
    testList "Bankverbindung: Hintergrunddienst" [
        testCase "ein Fehler in einem Takt beendet den Dienst nicht" <| fun () ->
            let calls = ref 0
            let secondCall = TaskCompletionSource<unit>()
            let failingTick (_: DateTimeOffset) = async {
                if Interlocked.Increment(calls) >= 2 then
                    secondCall.TrySetResult () |> ignore
                failwith "Takt scheitert"
            }
            use worker = new BankConnectionWorker(failingTick, TimeSpan.FromMilliseconds 1.0)
            worker.StartAsync(CancellationToken.None).Wait()
            Expect.isTrue (secondCall.Task.Wait(TimeSpan.FromSeconds 10.0)) "der zweite Takt läuft"
            worker.StopAsync(CancellationToken.None).Wait()
    ]
