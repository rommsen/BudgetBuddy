module Tests.BankConnectionTests

// Bankverbindung als Zustandsautomat (banking-import-tshq9, ADR 0071 Punkt 2, 3 und 8). Alle
// Bankaufrufe sind Fake-Ports, kein Testfall erreicht Comdirect.

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Shared.Domain
open Server.ComdirectClient
open Server.BankConnectionStore
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
    member val StoredRead = NothingStored with get, set
    member val SaveFails = false with get, set
    member val ClearFails = false with get, set
    member val Saves = ResizeArray<StoredConnection>()
    /// Das Access-Token in der Zelle im Moment des Speicherns.
    member val AccessInCellAtSave = ResizeArray<string>()
    member val Clears = ref 0
    /// War die Zelle schon getrennt, als gelöscht und gemeldet wurde?
    member val DisconnectedAtClear = ResizeArray<bool>()
    member val DisconnectedAtLoss = ResizeArray<bool>()
    member val Keys = Some keys with get, set
    /// Speichern und Bankabruf in ihrer Reihenfolge.
    member val Events = ResizeArray<string>()
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
              this.Events.Add $"abruf:{tokens.Access}"
              return this.FetchAnswer tokens }
          ReportLoss = fun reason ->
              this.DisconnectedAtLoss.Add (cell.State.Value = Disconnected)
              this.Losses.Add reason
          Store =
            { Load = fun () -> async.Return this.StoredRead
              Save = fun stored -> async {
                  if this.SaveFails then failwith "Speichern scheitert"
                  this.Saves.Add stored
                  this.Events.Add $"speichern:{stored.Tokens.Access}"
                  match cell.State.Value with
                  | Connected connection -> this.AccessInCellAtSave.Add connection.Session.Tokens.Access
                  | _ -> this.AccessInCellAtSave.Add "keine Verbindung" }
              Clear = fun () -> async {
                  this.DisconnectedAtClear.Add (cell.State.Value = Disconnected)
                  if this.ClearFails then failwith "Löschen scheitert"
                  Interlocked.Increment(this.Clears) |> ignore } }
          LoadKeys = fun () -> async.Return this.Keys }

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

/// Gespeicherter Wert mit einem Access-Token, das noch lange gilt.
let private stored : StoredConnection =
    { RequestInfo = requestInfo
      SessionId = requestInfo.SessionId
      SessionIdentifier = "kennung-kuenstlich"
      Tokens = tokensValidFor 500.0 "gespeichert" }

let private storedProbe () =
    Probe(Disconnected, StoredRead = StoredReadable stored)

let private expectNoBankLogin (probe: Probe) =
    Expect.equal probe.Logins.Value 0 "kein Passwort-Login, keine Challenge"
    Expect.equal probe.Confirmations.Value 0 "keine TAN-Bestätigung"

[<Tests>]
let persistingTests =
    testList "Bankverbindung: gespeicherter Wert" [
        testCase "Refresh erfolgreich: der Wert enthält das neue Token, bevor es in Verwendung ist" <| fun () ->
            let probe = Probe(connected (tokensValidFor 10.0 "alt"))
            let ports = probe.Build()
            tick ports now |> run
            Expect.equal (probe.Saves |> Seq.map (fun s -> s.Tokens) |> List.ofSeq) [ tokensValidFor 598.0 "neu" ] "gespeichert"
            Expect.equal (List.ofSeq probe.AccessInCellAtSave) [ "alt" ] "die Zelle trägt noch das alte Token"
            Expect.equal (activeConnection ports).Session.Tokens.Access "neu" "danach in Verwendung"

        testCase "der Wert trägt RequestInfo und Sitzungskennung der Verbindung" <| fun () ->
            let probe = Probe(connected (tokensValidFor 10.0 "alt"))
            tick (probe.Build()) now |> run
            let saved = probe.Saves |> Seq.exactlyOne
            Expect.equal saved.RequestInfo requestInfo "RequestInfo"
            Expect.equal saved.SessionId requestInfo.SessionId "Session-Id"
            Expect.equal saved.SessionIdentifier "kennung-kuenstlich" "Kennung"

        testCase "Refresh nach 401: erst speichern, dann der Wiederholversuch" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            probe.FetchAnswer <- fun tokens -> if tokens.Access = "alt" then Error rejected else Ok []
            fetch (probe.Build()) |> ignore
            Expect.equal (List.ofSeq probe.Events) [ "abruf:alt"; "speichern:neu"; "abruf:neu" ] "Reihenfolge"

        testCase "TAN bestätigt: der Wert liegt schon vor dem ersten Refresh" <| fun () ->
            let probe = Probe(pending)
            let ports = probe.Build()
            confirmChallengeWith ports |> run |> ignore
            Expect.equal (probe.Saves |> Seq.map (fun s -> s.Tokens.Access) |> List.ofSeq) [ "bestaetigt" ] "gespeichert"
            Expect.equal (activeConnection ports).Session.Tokens.Access "bestaetigt" "verbunden"

        testCase "gescheiterte TAN speichert nichts" <| fun () ->
            let probe = Probe(pending)
            probe.ConfirmAnswer <- Error ComdirectError.TanRejected
            confirmChallengeWith (probe.Build()) |> run |> ignore
            Expect.isEmpty probe.Saves "nichts gespeichert"

        testCase "Speichern scheitert: das neue Token ist trotzdem in Verwendung" <| fun () ->
            // Das alte Token ist bei der Bank verbraucht. Bliebe es in der Zelle, beendete der
            // nächste Refresh die Verbindung.
            let probe = Probe(connected (tokensValidFor 10.0 "alt"), SaveFails = true)
            let ports = probe.Build()
            tick ports now |> run
            Expect.equal (activeConnection ports).Session.Tokens.Access "neu" "neues Token"
            Expect.isEmpty probe.Losses "kein Verlust"

        testCase "RetryLater: der Wert bleibt unverändert" <| fun () ->
            let probe = Probe(connected (tokensValidFor 10.0 "alt"))
            probe.RefreshAnswer <- fun _ -> Error (RetryLater "HTTP 503")
            tick (probe.Build()) now |> run
            Expect.isEmpty probe.Saves "nichts gespeichert"
            Expect.equal probe.Clears.Value 0 "nichts gelöscht"

        testCase "ReloginRequired im Takt: Wert gelöscht" <| fun () ->
            let probe = Probe(connected (tokensValidFor 10.0 "alt"))
            probe.RefreshAnswer <- fun _ -> Error (ReloginRequired "HTTP 401")
            tick (probe.Build()) now |> run
            Expect.equal probe.Clears.Value 1 "gelöscht"

        testCase "Löschen scheitert beim Verlust: Disconnected, der Verlust ist gemeldet" <| fun () ->
            let probe = Probe(connected (tokensValidFor 10.0 "alt"), ClearFails = true)
            probe.RefreshAnswer <- fun _ -> Error (ReloginRequired "HTTP 401")
            let ports = probe.Build()
            tick ports now |> run
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.equal probe.Losses.Count 1 "gemeldet"

        testCase "401 auch nach dem Refresh: Wert gelöscht" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            probe.FetchAnswer <- fun _ -> Error rejected
            fetch (probe.Build()) |> ignore
            Expect.equal probe.Clears.Value 1 "gelöscht"

        testCase "Trennen: Wert gelöscht" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            disconnectWith (probe.Build()) |> run
            Expect.equal probe.Clears.Value 1 "gelöscht"

        testCase "Trennen löscht auch ohne aktive Verbindung" <| fun () ->
            let probe = Probe(Disconnected)
            disconnectWith (probe.Build()) |> run
            Expect.equal probe.Clears.Value 1 "gelöscht"

        testCase "Trennen und Löschen scheitert: getrennt, der Fehler kommt an" <| fun () ->
            // Ein stilles Scheitern ließe ein Token liegen, das der nächste Start wieder aufnimmt.
            let probe = Probe(connected (tokensValidFor 600.0 "alt"), ClearFails = true)
            let ports = probe.Build()
            Expect.throws (fun () -> disconnectWith ports |> run) "der Fehler kommt an"
            Expect.isTrue (isDisconnected ports) "getrennt"

        testCase "Zugangsdaten geändert: Wert gelöscht" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            saveCredentials (probe.Build()) { savedForm with Username = "anderer-name" } |> ignore
            Expect.equal probe.Clears.Value 1 "gelöscht"

        testCase "Zugangsdaten geändert und Löschen scheitert: nichts geschrieben, der zweite Versuch löscht" <| fun () ->
            // Bug: die Zugangsdaten lagen schon, bevor das Trennen scheiterte. Der zweite Versuch sah
            // keine Änderung mehr, und der nächste Start nahm den alten Wert mit den neuen Schlüsseln auf.
            let probe = Probe(connected (tokensValidFor 600.0 "alt"), ClearFails = true)
            let ports = probe.Build()
            let settings = System.Collections.Generic.Dictionary<string, string>(dict storedCredentials)
            let read key = async.Return (match settings.TryGetValue key with | true, value -> Some value | _ -> None)
            let write key value (_: bool) = async { settings[key] <- value }
            let save () =
                Server.Api.saveComdirectCredentialsWith read write (fun () -> disconnectWith ports) { savedForm with Username = "anderer-name" }
                |> run
            match save () with
            | Error (SettingsError.DatabaseError _) -> ()
            | other -> failtestf "DatabaseError erwartet, bekam %A" other
            Expect.equal settings["comdirect_username"] "name" "die neuen Daten liegen noch nicht"
            probe.ClearFails <- false
            Expect.isOk (save ()) "der zweite Versuch gelingt"
            Expect.equal probe.Clears.Value 1 "der zweite Versuch löscht den Wert"
            Expect.equal settings["comdirect_username"] "anderer-name" "die neuen Daten liegen"

        testCase "nur die Konto-Id geändert: der Wert bleibt" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            saveCredentials (probe.Build()) { savedForm with AccountId = Some "anderes-konto" } |> ignore
            Expect.equal probe.Clears.Value 0 "bleibt"

        testCase "neue Challenge: der Wert der abgelösten Verbindung ist weg" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            startChallengeWith (probe.Build()) credentials |> run |> ignore
            Expect.equal probe.Clears.Value 1 "gelöscht"

        testCase "gescheiterte Anmeldung: der Wert bleibt" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            let ports =
                { probe.Build() with Login = fun _ _ -> async.Return (Error ComdirectError.InvalidCredentials) }
            startChallengeWith ports credentials |> run |> ignore
            Expect.equal probe.Clears.Value 0 "bleibt"

        testCase "Verbindungstest vor der Wiederaufnahme: keine Challenge, der Wert bleibt" <| fun () ->
            let probe = Probe(Disconnected, StoredRead = StoredReadable stored)
            let ports = probe.Build()
            Expect.equal (testConnection ports) (Ok BankConnectionActive) "aktiv"
            Expect.equal probe.Logins.Value 0 "keine Challenge"
            Expect.equal probe.Clears.Value 0 "bleibt"

        testCase "401 und ReloginRequired im Abruf: erst die Zelle, dann Löschen und Meldung" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            probe.FetchAnswer <- fun _ -> Error rejected
            probe.RefreshAnswer <- fun _ -> Error (ReloginRequired "HTTP 401")
            let ports = probe.Build()
            Expect.equal (fetch ports) (Error rejected) "Ablehnung kommt an"
            Expect.equal (List.ofSeq probe.DisconnectedAtClear) [ true ] "beim Löschen schon getrennt"
            Expect.equal (List.ofSeq probe.DisconnectedAtLoss) [ true ] "bei der Meldung schon getrennt"

        testCase "zweite Ablehnung nach gelungenem Refresh: erst die Zelle, dann Löschen und Meldung" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            probe.FetchAnswer <- fun _ -> Error rejected
            let ports = probe.Build()
            fetch ports |> ignore
            Expect.equal (List.ofSeq probe.DisconnectedAtClear) [ true ] "beim Löschen schon getrennt"
            Expect.equal (List.ofSeq probe.DisconnectedAtLoss) [ true ] "bei der Meldung schon getrennt"

        testCase "cancelSync bei Connected: der Wert bleibt" <| fun () ->
            let probe = Probe(connected (tokensValidFor 600.0 "alt"))
            cancel (probe.Build()) |> ignore
            Expect.equal probe.Clears.Value 0 "bleibt"
    ]

[<Tests>]
let resumeTests =
    testList "Bankverbindung: Wiederaufnahme beim Start" [
        testCase "gespeicherter Wert und Refresh gelingt: Connected, keine Challenge" <| fun () ->
            let probe = storedProbe ()
            let ports = probe.Build()
            resumeConnectionWith ports now |> run
            let connection = activeConnection ports
            Expect.equal connection.Session.Tokens.Access "neu" "neues Token"
            Expect.equal connection.Session.RequestInfo requestInfo "RequestInfo aus dem Wert"
            Expect.equal connection.Session.SessionIdentifier "kennung-kuenstlich" "Kennung aus dem Wert"
            Expect.equal connection.Keys keys "Schlüssel aus den Einstellungen"
            Expect.equal (probe.Refreshes |> Seq.map (fun t -> t.Refresh) |> List.ofSeq) [ "gespeichert-refresh" ] "mit dem gespeicherten Refresh-Token"
            Expect.equal (probe.Saves |> Seq.map (fun s -> s.Tokens.Access) |> List.ofSeq) [ "neu" ] "neues Token gespeichert"
            expectNoBankLogin probe

        testCase "ReloginRequired: Wert gelöscht, Disconnected, keine Challenge" <| fun () ->
            let probe = storedProbe ()
            probe.RefreshAnswer <- fun _ -> Error (ReloginRequired "HTTP 401: invalid_token")
            let ports = probe.Build()
            resumeConnectionWith ports now |> run
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.equal probe.Clears.Value 1 "gelöscht"
            Expect.equal (List.ofSeq probe.Losses) [ "HTTP 401: invalid_token" ] "einmal gemeldet"
            expectNoBankLogin probe

        testCase "nicht entschlüsselbarer Wert: gelöscht, Disconnected, kein Bankzugriff" <| fun () ->
            let probe = Probe(Disconnected, StoredRead = StoredUnreadable)
            let ports = probe.Build()
            resumeConnectionWith ports now |> run
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.equal probe.Clears.Value 1 "gelöscht"
            Expect.equal probe.Refreshes.Count 0 "kein Refresh"
            Expect.equal (List.ofSeq probe.Losses) [ "Gespeicherte Bankverbindung nicht lesbar" ] "einmal gemeldet"
            expectNoBankLogin probe

        testCase "Zugangsdaten nicht mehr lesbar: Wert gelöscht, kein Bankzugriff" <| fun () ->
            let probe = Probe(Disconnected, StoredRead = StoredReadable stored, Keys = None)
            let ports = probe.Build()
            resumeConnectionWith ports now |> run
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.equal probe.Clears.Value 1 "gelöscht"
            Expect.equal probe.Refreshes.Count 0 "kein Refresh"
            Expect.equal (List.ofSeq probe.Losses) [ "Gespeicherte Bankverbindung nicht lesbar" ] "einmal gemeldet"

        testCase "RetryLater: der Wert bleibt, nächster Versuch nach 60 s trotz gültigem Access-Token" <| fun () ->
            let probe = storedProbe ()
            probe.RefreshAnswer <- fun _ -> Error (RetryLater "HTTP 503")
            let ports = probe.Build()
            resumeConnectionWith ports now |> run
            Expect.equal probe.Clears.Value 0 "nichts gelöscht"
            Expect.isEmpty probe.Losses "kein Verlust"
            Expect.equal (activeConnection ports).Session.Tokens.Access "gespeichert" "Verbindung steht"
            tick ports (now.AddSeconds 59.0) |> run
            Expect.equal probe.Refreshes.Count 1 "vor Ablauf der 60 s kein Versuch"
            probe.RefreshAnswer <- fun _ -> Ok (tokensValidFor 658.0 "neu")
            tick ports (now.AddSeconds 60.0) |> run
            Expect.equal probe.Refreshes.Count 2 "nach 60 s der nächste Versuch"
            Expect.equal (activeConnection ports).Session.Tokens.Access "neu" "wieder aufgenommen"
            expectNoBankLogin probe

        testCase "RetryLater, dann ReloginRequired im Takt: Wert gelöscht" <| fun () ->
            let probe = storedProbe ()
            probe.RefreshAnswer <- fun _ -> Error (RetryLater "HTTP 503")
            let ports = probe.Build()
            resumeConnectionWith ports now |> run
            probe.RefreshAnswer <- fun _ -> Error (ReloginRequired "HTTP 401")
            tick ports (now.AddSeconds 60.0) |> run
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.equal probe.Clears.Value 1 "gelöscht"
            expectNoBankLogin probe

        testCase "nichts gespeichert: Disconnected, kein Bankzugriff" <| fun () ->
            let probe = Probe(Disconnected)
            let ports = probe.Build()
            resumeConnectionWith ports now |> run
            Expect.isTrue (isDisconnected ports) "getrennt"
            Expect.equal probe.Refreshes.Count 0 "kein Refresh"
            Expect.equal probe.Clears.Value 0 "nichts zu löschen"
            Expect.isEmpty probe.Losses "nichts verloren"
            expectNoBankLogin probe

        testCase "Roman war schneller: eine offene Challenge bleibt unberührt" <| fun () ->
            let probe = Probe(pending, StoredRead = StoredReadable stored)
            let ports = probe.Build()
            resumeConnectionWith ports now |> run
            Expect.equal (connectionState ports) pending "Challenge bleibt offen"
            Expect.equal probe.Refreshes.Count 0 "kein Refresh"

        testCase "Wiederaufnahme und Tick gleichzeitig: ein Aufruf" <| fun () ->
            let probe = storedProbe ()
            let ports = probe.Build()
            withRefreshHeld probe (resumeConnectionWith ports now)
                (fun () -> tick ports now |> Async.StartImmediateAsTask)
                (fun second ->
                    Expect.isFalse second.IsCompleted "der Tick wartet auf die Wiederaufnahme"
                    Expect.equal probe.Refreshes.Count 1 "kein zweiter Refresh, solange der erste läuft")
            |> ignore
            Expect.equal probe.Refreshes.Count 1 "ein Refresh"
    ]

let private withDatabase (test: unit -> unit) = fun () ->
    Persistence.initializeDatabase ()
    test ()

/// Eigener Schlüssel je Testfall: die In-Memory-DB ist für alle Listen geteilt.
let private freshSettingKey () = $"test_bankverbindung_{Guid.NewGuid():N}"

let private withKey = fun () -> true
let private withoutKey = fun () -> false

let private storedAccess = "zugriff-unverwechselbar"
let private storedRefresh = "erneuerung-unverwechselbar"

let private valueToStore : StoredConnection =
    { stored with Tokens = { Access = storedAccess; Refresh = storedRefresh; ExpiresAt = Some now } }

let private rawRow (settingKey: string) =
    use connection = global.Persistence.getConnection ()
    connection.Open()
    use command = connection.CreateCommand()
    command.CommandText <- "SELECT value, encrypted FROM settings WHERE key = $key"
    command.Parameters.AddWithValue("$key", settingKey) |> ignore
    use reader = command.ExecuteReader()
    if reader.Read() then Some (reader.GetString 0, reader.GetInt32 1) else None

[<Tests>]
let storeTests =
    testList "Bankverbindung: Speicher in den Einstellungen" [
        testCase "Rundlauf: gespeichert wird verschlüsselt, gelesen der gleiche Wert" <| withDatabase (fun () ->
            let settingKey = freshSettingKey ()
            let store = settingsConnectionStore withKey settingKey
            store.Save valueToStore |> run
            let value, encrypted = rawRow settingKey |> Option.get
            Expect.equal encrypted 1 "als verschlüsselt markiert"
            Expect.isFalse (value.Contains storedAccess) "kein Access-Token im Klartext"
            Expect.isFalse (value.Contains storedRefresh) "kein Refresh-Token im Klartext"
            Expect.equal (store.Load () |> run) (StoredReadable valueToStore) "Rundlauf")

        testCase "ohne Schlüssel wird nichts gespeichert" <| withDatabase (fun () ->
            let settingKey = freshSettingKey ()
            let store = settingsConnectionStore withoutKey settingKey
            store.Save valueToStore |> run
            Expect.isNone (rawRow settingKey) "keine Zeile"
            Expect.equal (store.Load () |> run) NothingStored "nichts da")

        testCase "ohne Schlüssel überschreibt Speichern auch einen alten Wert nicht" <| withDatabase (fun () ->
            let settingKey = freshSettingKey ()
            (settingsConnectionStore withKey settingKey).Save valueToStore |> run
            let before = rawRow settingKey
            (settingsConnectionStore withoutKey settingKey).Save { valueToStore with SessionId = "anders" } |> run
            Expect.equal (rawRow settingKey) before "unverändert")

        testCase "ohne Schlüssel gilt ein vorhandener Wert als unlesbar" <| withDatabase (fun () ->
            // Der Maschinen-Fallback gilt für diesen Wert nicht, auch nicht beim Lesen.
            let settingKey = freshSettingKey ()
            (settingsConnectionStore withKey settingKey).Save valueToStore |> run
            Expect.equal ((settingsConnectionStore withoutKey settingKey).Load () |> run) StoredUnreadable "unlesbar")

        testCase "nicht entschlüsselbar: unlesbar, die Zeile bleibt bis zum Löschen" <| withDatabase (fun () ->
            let settingKey = freshSettingKey ()
            Persistence.Settings.setSetting settingKey "kein-base64!" false |> run
            use connection = global.Persistence.getConnection ()
            connection.Open()
            use command = connection.CreateCommand()
            command.CommandText <- "UPDATE settings SET encrypted = 1 WHERE key = $key"
            command.Parameters.AddWithValue("$key", settingKey) |> ignore
            command.ExecuteNonQuery() |> ignore
            let store = settingsConnectionStore withKey settingKey
            Expect.equal (store.Load () |> run) StoredUnreadable "unlesbar"
            Expect.isSome (rawRow settingKey) "Lesen löscht nicht")

        testCase "unverschlüsselt abgelegter Wert: unlesbar" <| withDatabase (fun () ->
            let settingKey = freshSettingKey ()
            // Gültiges JSON: nur die fehlende Verschlüsselung macht ihn unlesbar.
            Persistence.Settings.setSetting settingKey (Thoth.Json.Net.Encode.Auto.toString (0, valueToStore)) false |> run
            Expect.equal ((settingsConnectionStore withKey settingKey).Load () |> run) StoredUnreadable "unlesbar")

        for name, json in [ "kein JSON", "kein json"; "Felder fehlen", "{}" ] do
            testCase $"entschlüsselbar, aber {name}: unlesbar" <| withDatabase (fun () ->
                let settingKey = freshSettingKey ()
                Persistence.Settings.setSetting settingKey json true |> run
                Expect.equal ((settingsConnectionStore withKey settingKey).Load () |> run) StoredUnreadable "unlesbar")

        testCase "Löschen entfernt den Wert, auch ohne Schlüssel" <| withDatabase (fun () ->
            let settingKey = freshSettingKey ()
            (settingsConnectionStore withKey settingKey).Save valueToStore |> run
            (settingsConnectionStore withoutKey settingKey).Clear () |> run
            Expect.isNone (rawRow settingKey) "weg")

        testCase "der Speicher des Servers mit gesetztem Schlüssel: der Wert wird gespeichert und gelesen" <| withDatabase (fun () ->
            let settingKey = freshSettingKey ()
            let store = connectionStoreFor (fun _ -> "c2NobHVlc3NlbA==") settingKey
            store.Save valueToStore |> run
            Expect.equal (rawRow settingKey |> Option.map snd) (Some 1) "verschlüsselt gespeichert"
            Expect.equal (store.Load () |> run) (StoredReadable valueToStore) "gelesen")

        testCase "der Speicher des Servers ohne Schlüssel: nichts gespeichert" <| withDatabase (fun () ->
            let settingKey = freshSettingKey ()
            let store = connectionStoreFor (fun _ -> null) settingKey
            store.Save valueToStore |> run
            Expect.isNone (rawRow settingKey) "keine Zeile")

        testCase "Schlüssel für den Refresh kommen aus den Einstellungen" <| fun () ->
            let found = loadApiKeysFrom (settingsFrom storedCredentials) |> run
            Expect.equal found (Some { ClientId = "id"; ClientSecret = "secret" }) "beide gelesen"

        testCase "fehlt das Client-Secret, gibt es keine Schlüssel" <| fun () ->
            let found = loadApiKeysFrom (settingsFrom [ "comdirect_client_id", "id" ]) |> run
            Expect.isNone found "keine Schlüssel"
    ]

let private noResume (_: DateTimeOffset) = async.Return ()

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
            use worker = new BankConnectionWorker(noResume, failingTick, TimeSpan.FromMilliseconds 1.0)
            worker.StartAsync(CancellationToken.None).Wait()
            Expect.isTrue (secondCall.Task.Wait(TimeSpan.FromSeconds 10.0)) "der zweite Takt läuft"
            worker.StopAsync(CancellationToken.None).Wait()

        testCase "die Wiederaufnahme läuft einmal und vor dem ersten Takt" <| fun () ->
            let order = System.Collections.Concurrent.ConcurrentQueue<string>()
            let ticked = TaskCompletionSource<unit>()
            let resume (_: DateTimeOffset) = async { order.Enqueue "wiederaufnahme" }
            let countTick (_: DateTimeOffset) = async {
                order.Enqueue "takt"
                if order.Count >= 3 then ticked.TrySetResult () |> ignore
            }
            use worker = new BankConnectionWorker(resume, countTick, TimeSpan.FromMilliseconds 1.0)
            worker.StartAsync(CancellationToken.None).Wait()
            Expect.isTrue (ticked.Task.Wait(TimeSpan.FromSeconds 10.0)) "die Takte laufen"
            worker.StopAsync(CancellationToken.None).Wait()
            Expect.equal (order |> Seq.take 3 |> List.ofSeq) [ "wiederaufnahme"; "takt"; "takt" ] "Reihenfolge"

        testCase "eine scheiternde Wiederaufnahme beendet den Dienst nicht" <| fun () ->
            let ticked = TaskCompletionSource<unit>()
            let failingResume (_: DateTimeOffset) : Async<unit> = async { return failwith "Wiederaufnahme scheitert" }
            let noteTick (_: DateTimeOffset) = async { ticked.TrySetResult () |> ignore }
            use worker = new BankConnectionWorker(failingResume, noteTick, TimeSpan.FromMilliseconds 1.0)
            worker.StartAsync(CancellationToken.None).Wait()
            Expect.isTrue (ticked.Task.Wait(TimeSpan.FromSeconds 10.0)) "der Takt läuft"
            worker.StopAsync(CancellationToken.None).Wait()

        testCase "eine hängende Bank blockiert weder den Start noch das Beenden" <| fun () ->
            let reached = TaskCompletionSource<unit>()
            let never = TaskCompletionSource<unit>()
            let hangingResume (_: DateTimeOffset) = async {
                reached.TrySetResult () |> ignore
                do! never.Task |> Async.AwaitTask
            }
            use worker = new BankConnectionWorker(hangingResume, (fun _ -> async.Return ()), TimeSpan.FromMilliseconds 1.0)
            Expect.isTrue (worker.StartAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds 10.0)) "der Start kehrt zurück"
            Expect.isTrue (reached.Task.Wait(TimeSpan.FromSeconds 10.0)) "die Wiederaufnahme läuft"
            Expect.isTrue (worker.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds 10.0)) "das Beenden kehrt zurück"
            never.TrySetResult () |> ignore

        testCase "ein hängender Takt blockiert das Beenden nicht" <| fun () ->
            // Bug: nur die Wiederaufnahme war an `stopping` gebunden. Ein Takt, der an der Bank oder am
            // Semaphor hing, hielt das Beenden auf.
            let reached = TaskCompletionSource<unit>()
            let never = TaskCompletionSource<unit>()
            let hangingTick (_: DateTimeOffset) = async {
                reached.TrySetResult () |> ignore
                do! never.Task |> Async.AwaitTask
            }
            use worker = new BankConnectionWorker(noResume, hangingTick, TimeSpan.FromMilliseconds 1.0)
            worker.StartAsync(CancellationToken.None).Wait()
            Expect.isTrue (reached.Task.Wait(TimeSpan.FromSeconds 10.0)) "der Takt läuft"
            Expect.isTrue (worker.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds 10.0)) "das Beenden kehrt zurück"
            never.TrySetResult () |> ignore
    ]
