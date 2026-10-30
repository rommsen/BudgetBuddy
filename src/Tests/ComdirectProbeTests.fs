module ComdirectProbeTests

// Sonde gegen Fake-Ports (banking-import-cx4b7): kein Netz, die Uhr läuft über `Sleep`.

open System
open System.IO
open Expecto
open Shared.Domain
open Server.ComdirectClient
open ComdirectProbe

let private tokenText = "GEHEIMES-TOKEN-KUENSTLICH-4711"
let private startTime = DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero)
let private requestInfo = { RequestId = "123456789"; SessionId = "sitzung-kuenstlich" }

let private tokensValidFor (now: DateTimeOffset) (seconds: float) =
    { Access = tokenText; Refresh = tokenText; ExpiresAt = Some (now.AddSeconds seconds) }

let private session =
    { RequestInfo = requestInfo
      SessionId = requestInfo.SessionId
      Tokens = { Access = tokenText; Refresh = tokenText; ExpiresAt = None }
      SessionIdentifier = "kennung-kuenstlich"
      Challenge = Some { Id = "challenge-kuenstlich"; Type = "P_TAN_PUSH" } }

let private account: ComdirectAccount =
    { AccountId = "konto-kuenstlich"; Iban = ""; AccountDisplayId = ""; AccountType = "GIRO"
      Currency = "EUR"; Balance = None }

/// Zeichnet alle Aufrufe auf. `Refresh` gibt 10 Minuten gültige Tokens, außer `refreshResult`
/// sagt etwas anderes.
type private Fake() =
    member val Now = startTime with get, set
    member val Logins = 0 with get, set
    member val Confirmations = 0 with get, set
    member val Refreshes = ResizeArray<DateTimeOffset>()
    member val DataRounds = ResizeArray<DateTimeOffset * RequestInfo>()
    member val Log = ResizeArray<ProbeLogEntry>()
    member val Saved = ResizeArray<ProbeState>()
    member val Stored : ProbeState option = None with get, set
    member val Signal = true with get, set
    member val SignalCleared = false with get, set
    /// Sentinel erscheint erst nach dieser Zeit.
    member val SignalAfter : TimeSpan option = None with get, set
    member val Login : ComdirectResult<AuthSession> = Ok session with get, set
    member val Confirm : ComdirectResult<Tokens> = Ok (tokensValidFor startTime 600.) with get, set
    member val RefreshResult : Fake -> Result<Tokens, RefreshFailure> =
        (fun fake -> Ok (tokensValidFor fake.Now 600.)) with get, set
    member val Encryption = true with get, set
    member val ConfiguredAccount : string option = None with get, set
    member val FetchedAccounts = ResizeArray<string>() with get

    member this.Ports : ProbePorts =
        { Now = fun () -> this.Now
          Sleep = fun span -> async { this.Now <- this.Now + span }
          Login = fun () -> async { this.Logins <- this.Logins + 1; return this.Login }
          ConfirmTan = fun _ -> async { this.Confirmations <- this.Confirmations + 1; return this.Confirm }
          Refresh = fun _ -> async { this.Refreshes.Add this.Now; return this.RefreshResult this }
          FetchAccounts = fun state -> async {
              this.DataRounds.Add((this.Now, state.RequestInfo))
              return Ok [ account ] }
          FetchTransactions = fun _ accountId -> async {
              this.FetchedAccounts.Add accountId
              return Ok [] }
          ConfiguredAccountId = this.ConfiguredAccount
          SignalPresent = fun () ->
              match this.SignalAfter with
              | Some after -> this.Now >= startTime + after
              | None -> this.Signal
          ClearSignal = fun () -> this.SignalCleared <- true
          EncryptionAvailable = this.Encryption
          LoadState = fun () -> this.Stored
          SaveState = fun state -> this.Saved.Add state; this.Stored <- Some state
          ClearState = fun () -> this.Stored <- None
          Log = this.Log.Add }

let private runPhase phase (fake: Fake) = run phase fake.Ports |> Async.RunSynchronously

let private storedState () =
    { RequestInfo = requestInfo; SessionIdentifier = "kennung-kuenstlich"; Tokens = tokensValidFor startTime 600. }

[<Tests>]
let switchTests =
    testList "Comdirect-Sonde (Fake-Ports): Schalter" [
        // Gegenprobe zur Sonde, Teil 1: die Entscheidung. Teil 2 ist der TAN-Wächter in
        // NetworkCallGuardTests.fs, der jeden TAN-Aufruf in src/Tests an diesen Schalter bindet.
        testCase "RUN_INTEGRATION_TESTS ohne COMDIRECT_PROBE wählt keine Phase" <| fun () ->
            Expect.equal (selectPhase true None) None "aus"

        testCase "COMDIRECT_PROBE ohne RUN_INTEGRATION_TESTS wählt keine Phase" <| fun () ->
            Expect.equal (selectPhase false (Some "1")) None "aus"

        testCase "nur 1, 2 und 3 schalten die Sonde ein" <| fun () ->
            Expect.equal (selectPhase true (Some "1")) (Some Observe) "Phase 1"
            Expect.equal (selectPhase true (Some "2")) (Some Resume) "Phase 2"
            Expect.equal (selectPhase true (Some "3")) (Some RunToRejection) "Phase 3"
            for value in [ ""; "0"; "true"; "ja" ] do
                Expect.equal (selectPhase true (Some value)) None $"'{value}' ist aus"
    ]

[<Tests>]
let tanTests =
    testList "Comdirect-Sonde (Fake-Ports): TAN" [
        testCase "die TAN wird genau einmal bestätigt" <| fun () ->
            let fake = Fake()
            Expect.equal (runPhase Observe fake) Finished "Lauf endet regulär"
            Expect.equal fake.Logins 1 "eine Challenge"
            Expect.equal fake.Confirmations 1 "eine Bestätigung"

        testCase "die Bestätigung wartet auf Romans Signal" <| fun () ->
            let fake = Fake(SignalAfter = Some (TimeSpan.FromMinutes 2.))
            fake.Confirm <- Ok (tokensValidFor (startTime.AddMinutes 2.) 600.)
            runPhase Observe fake |> ignore
            let firstDataRound = fst fake.DataRounds[0]
            Expect.isGreaterThanOrEqual firstDataRound (startTime.AddMinutes 2.) "erst nach dem Signal"
            Expect.equal fake.Confirmations 1 "eine Bestätigung"

        testCase "eine alte Sentinel-Datei wird vor der Anmeldung entfernt" <| fun () ->
            let fake = Fake()
            runPhase Observe fake |> ignore
            Expect.isTrue fake.SignalCleared "alte Datei weg"

        testCase "ohne Signal wird nichts bestätigt" <| fun () ->
            let fake = Fake(Signal = false)
            match runPhase Observe fake with
            | Aborted _ -> ()
            | other -> failtestf "Aborted erwartet: %A" other
            Expect.equal fake.Confirmations 0 "kein Fehlversuch"

        testCase "eine gescheiterte Bestätigung wird nicht wiederholt" <| fun () ->
            let fake = Fake(Confirm = Error ComdirectError.TanRejected)
            match runPhase Observe fake with
            | Aborted _ -> ()
            | other -> failtestf "Aborted erwartet: %A" other
            Expect.equal fake.Confirmations 1 "kein zweiter Versuch"
            Expect.equal fake.Logins 1 "keine zweite Challenge"

        testCase "eine liegende Zustandsdatei verhindert eine neue TAN" <| fun () ->
            let fake = Fake(Stored = Some (storedState ()))
            match runPhase Observe fake with
            | Aborted _ -> ()
            | other -> failtestf "Aborted erwartet: %A" other
            Expect.equal fake.Logins 0 "keine Challenge"

        testCase "ohne Schlüssel startet keine TAN" <| fun () ->
            let fake = Fake(Encryption = false)
            match runPhase Observe fake with
            | Aborted _ -> ()
            | other -> failtestf "Aborted erwartet: %A" other
            Expect.equal fake.Logins 0 "keine Challenge"

        testCase "Phase 2 und 3 starten ohne Zustandsdatei keine TAN" <| fun () ->
            for phase in [ Resume; RunToRejection ] do
                let fake = Fake()
                match runPhase phase fake with
                | Aborted _ -> ()
                | other -> failtestf "Aborted erwartet: %A" other
                Expect.equal fake.Logins 0 "keine Challenge"

        testCase "auch ein abgelehnter Refresh führt zu keiner neuen TAN" <| fun () ->
            for phase in [ Resume; RunToRejection ] do
                let fake = Fake(Stored = Some (storedState ()))
                fake.RefreshResult <- fun _ -> Error (ReloginRequired "HTTP 400: invalid_grant")
                Expect.equal (runPhase phase fake) (SessionLost "HTTP 400: invalid_grant") "Sitzung verloren"
                Expect.equal fake.Logins 0 "keine Challenge"
                Expect.isNone fake.Stored "verlorener Zustand wird gelöscht"
    ]

[<Tests>]
let scheduleTests =
    testList "Comdirect-Sonde (Fake-Ports): Zeitplan" [
        testCase "Refresh etwa 60 s vor Ablauf" <| fun () ->
            let fake = Fake()
            runPhase Observe fake |> ignore
            let first = fake.Refreshes[0]
            Expect.equal first (startTime.AddSeconds 540.) "60 s vor den 600 s"

        testCase "der Lauf deckt 2 Stunden ab und ruft alle 30 Minuten Daten ab" <| fun () ->
            let fake = Fake()
            runPhase Observe fake |> ignore
            Expect.equal fake.DataRounds.Count 4 "bei 0, 30, 60 und 90 Minuten"
            Expect.isGreaterThanOrEqual fake.Now (startTime.AddHours 2.) "2 Stunden"

        testCase "die RequestInfo bleibt über alle Abrufe unverändert" <| fun () ->
            let fake = Fake()
            runPhase Observe fake |> ignore
            for _, info in fake.DataRounds do
                Expect.equal info requestInfo "unverändert"

        testCase "jeder Refresh wird vor der Benutzung gespeichert" <| fun () ->
            let fake = Fake()
            runPhase Observe fake |> ignore
            Expect.equal fake.Saved.Count (fake.Refreshes.Count + 1) "nach der TAN und nach jedem Refresh"

        testCase "später nochmal wartet 60 s und behält die Sitzung" <| fun () ->
            let fake = Fake()
            let mutable failures = 1
            fake.RefreshResult <- fun current ->
                if failures > 0 then
                    failures <- failures - 1
                    Error (RetryLater "HTTP 503")
                else
                    Ok (tokensValidFor current.Now 600.)
            Expect.equal (runPhase Observe fake) Finished "Lauf geht weiter"
            Expect.equal (fake.Refreshes[1] - fake.Refreshes[0]) (TimeSpan.FromSeconds 60.) "nächster Versuch"

        testCase "neu anmelden nötig beendet die Beobachtung" <| fun () ->
            let fake = Fake()
            fake.RefreshResult <- fun _ -> Error (ReloginRequired "HTTP 401")
            Expect.equal (runPhase Observe fake) (SessionLost "HTTP 401") "Ende"
            Expect.equal fake.Refreshes.Count 1 "kein zweiter Refresh mit demselben Token"

        testCase "sehr kurze Laufzeiten lösen keine Refresh-Schleife aus" <| fun () ->
            let tokens = tokensValidFor startTime 5.
            Expect.equal (refreshDueAt startTime tokens) (startTime.AddSeconds 10.) "Mindestabstand"

        testCase "ein Datenfehler beendet die Beobachtung nicht" <| fun () ->
            let fake = Fake()
            let failing =
                { fake.Ports with FetchAccounts = fun _ -> async { return Error ComdirectError.SessionExpired } }
            Expect.equal (run Observe failing |> Async.RunSynchronously) Finished "weiter beobachten"
            Expect.exists fake.Log (fun e -> e.Event = "konten" && e.Status = "403") "Status im Protokoll"

        // Regression: Comdirect antwortete live auf alle Kontenpfade mit einem Fehler, die Sonde
        // rief dadurch nie Umsätze ab, und das Go-Kriterium blieb unmessbar.
        testCase "bei gescheitertem Kontenabruf holt die Sonde Umsätze über das konfigurierte Konto" <| fun () ->
            let fake = Fake(ConfiguredAccount = Some "konfiguriert-kuenstlich")
            let failing =
                { fake.Ports with FetchAccounts = fun _ -> async { return Error (ComdirectError.NetworkError (404, "No valid accounts endpoint found")) } }
            Expect.equal (run Observe failing |> Async.RunSynchronously) Finished "Lauf endet regulär"
            Expect.exists fake.Log (fun e -> e.Event = "konten" && e.Status = "404") "Fehler bleibt im Protokoll"
            Expect.exists fake.Log (fun e -> e.Event = "umsaetze" && e.Status = "ok") "Umsätze geholt"
            Expect.all fake.FetchedAccounts (fun id -> id = "konfiguriert-kuenstlich") "konfiguriertes Konto"

        testCase "ein konfiguriertes Konto geht dem ersten Konto aus dem Abruf vor" <| fun () ->
            let fake = Fake(ConfiguredAccount = Some "konfiguriert-kuenstlich")
            runPhase Observe fake |> ignore
            Expect.isNonEmpty fake.FetchedAccounts "Umsätze geholt"
            Expect.all fake.FetchedAccounts (fun id -> id = "konfiguriert-kuenstlich") "konfiguriertes Konto"

        testCase "ohne konfiguriertes Konto nimmt die Sonde das erste Konto aus dem Abruf" <| fun () ->
            let fake = Fake()
            runPhase Observe fake |> ignore
            Expect.isNonEmpty fake.FetchedAccounts "Umsätze geholt"
            Expect.all fake.FetchedAccounts (fun id -> id = account.AccountId) "erstes Konto"

        testCase "Phase 2 setzt mit RequestInfo aus der Zustandsdatei fort" <| fun () ->
            let fake = Fake(Stored = Some (storedState ()))
            Expect.equal (runPhase Resume fake) Finished "Lauf endet regulär"
            Expect.equal fake.Logins 0 "keine TAN"
            Expect.equal (snd fake.DataRounds[0]) requestInfo "gleiche RequestInfo"

        testCase "Phase 3 verlängert die Pause, bis der Refresh abgelehnt wird" <| fun () ->
            let fake = Fake(Stored = Some (storedState ()))
            fake.RefreshResult <- fun current ->
                if current.Refreshes.Count < 4 then Ok (tokensValidFor current.Now 600.)
                else Error (ReloginRequired "HTTP 400: invalid_grant")
            Expect.equal (runPhase RunToRejection fake) (SessionLost "HTTP 400: invalid_grant") "abgelehnt"
            let gaps = [ for i in 1 .. 3 -> (fake.Refreshes[i] - fake.Refreshes[i - 1]).TotalMinutes ]
            Expect.equal gaps [ 10.; 15.; 20. ] "wachsende Pausen"
            Expect.exists fake.Log (fun e -> e.Event = "refresh-abgelehnt" && e.Status.Contains "invalid_grant") "Fehlerform im Protokoll"
    ]

[<Tests>]
let secrecyTests =
    testList "Comdirect-Sonde (Fake-Ports): Geheimnisse" [
        testCase "das Protokoll enthält nie ein Token" <| fun () ->
            let fake = Fake()
            runPhase Observe fake |> ignore
            Expect.isNonEmpty fake.Log "Protokoll geschrieben"
            for entry in fake.Log do
                Expect.isFalse ((formatLogEntry entry).Contains tokenText) "kein Token"

        testCase "die Protokollzeile trägt Zeit, Status und expires_in" <| fun () ->
            let line = formatLogEntry { At = startTime; Event = "refresh"; Status = "ok"; ExpiresIn = Some 599 }
            Expect.equal line "2026-10-30T08:00:00.0000000+00:00 refresh status=ok expires_in=599" "Zeile"

        testCase "die Zustandsdatei ist verschlüsselt und trägt alle Felder durch einen neuen Prozess" <| fun () ->
            let path = Path.Combine(Path.GetTempPath(), $"probe-{Guid.NewGuid()}.enc")
            try
                saveEncryptedState path (storedState ())
                Expect.isFalse ((File.ReadAllText path).Contains tokenText) "kein Klartext"
                Expect.equal (loadEncryptedState path) (Some (storedState ())) "Rundlauf"
            finally
                File.Delete path

        testCase "eine fehlende Zustandsdatei ist None, eine kaputte stoppt die Sonde" <| fun () ->
            let path = Path.Combine(Path.GetTempPath(), $"probe-{Guid.NewGuid()}.enc")
            Expect.isNone (loadEncryptedState path) "fehlt"
            try
                File.WriteAllText(path, "kein-gueltiger-inhalt")
                Expect.throws (fun () -> loadEncryptedState path |> ignore) "kaputt zählt nicht als fehlend"
            finally
                File.Delete path
    ]
