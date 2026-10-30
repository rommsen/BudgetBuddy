module ComdirectIntegrationTests

open System
open System.IO
open Expecto
open Shared.Domain
open Server.ComdirectClient

// ============================================
// .env File Loader
// ============================================

let private loadEnv () =
    let projectRoot =
        let testDir = __SOURCE_DIRECTORY__
        Directory.GetParent(Directory.GetParent(testDir).FullName).FullName

    let envPath = Path.Combine(projectRoot, ".env")

    if not (File.Exists envPath) then
        Map.empty
    else
        File.ReadAllLines(envPath)
        |> Array.filter (fun line ->
            not (String.IsNullOrWhiteSpace(line)) &&
            not (line.TrimStart().StartsWith("#"))
        )
        |> Array.choose (fun line ->
            match line.Split('=', 2) with
            | [| key; value |] -> Some (key.Trim(), value.Trim())
            | _ -> None
        )
        |> Map.ofArray

let private getEnvVar key =
    let env = loadEnv()
    env.TryFind key

let private getApiKeys () =
    match getEnvVar "COMDIRECT_CLIENT_ID", getEnvVar "COMDIRECT_CLIENT_SECRET" with
    | Some clientId, Some clientSecret ->
        Some { ClientId = clientId; ClientSecret = clientSecret }
    | _ -> None

let private getCredentials () =
    match getEnvVar "COMDIRECT_USERNAME", getEnvVar "COMDIRECT_PASSWORD" with
    | Some username, Some password -> Some (username, password)
    | _ -> None

let private shouldRunIntegrationTests () =
    match getEnvVar "RUN_INTEGRATION_TESTS" with
    | Some value when value.ToLower() = "true" -> true
    | _ -> false

/// `COMDIRECT_PROBE` zählt nur aus der Prozess-Umgebung, nie aus `.env` (siehe ComdirectProbe.fs).
let private selectedProbePhase () =
    ComdirectProbe.selectPhase
        (shouldRunIntegrationTests ())
        (Environment.GetEnvironmentVariable "COMDIRECT_PROBE" |> Option.ofObj)

/// Dasselbe Datenverzeichnis wie `Persistence`, darunter ein eigener Ordner.
let private probeDirectory () =
    let dataDir =
        match Environment.GetEnvironmentVariable "DATA_DIR" with
        | null | "" ->
            Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, "my_apps", "budgetbuddy-dev")
        | path -> path
    Path.Combine(dataDir, "comdirect-probe")

// ============================================
// Integration Tests (require Comdirect credentials in .env)
// ============================================

[<Tests>]
let integrationTests =
    testList "Comdirect Integration Tests (requires .env)" [
        // Die einzige Stelle, die in einem Testlauf eine Push-TAN auslösen kann. Der frühere
        // Testfall "can initiate OAuth flow" löste schon mit RUN_INTEGRATION_TESTS allein eine aus
        // und ist deshalb entfallen (banking-import-cx4b7). Ablauf und Start: ComdirectProbe.fs.
        testCase "Comdirect-Sonde: Session trägt nur per Refresh" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match selectedProbePhase(), getApiKeys(), getCredentials() with
            | None, _, _ ->
                Tests.skiptest "COMDIRECT_PROBE nicht gesetzt: keine Sonde, keine TAN"
            | _, None, _ | _, _, None ->
                Tests.skiptest "COMDIRECT credentials not set in .env - skipping integration test"
            | Some phase, Some apiKeys, Some (username, password) ->
                let credentials: ComdirectSettings = {
                    ClientId = apiKeys.ClientId
                    ClientSecret = apiKeys.ClientSecret
                    Username = username
                    Password = password
                    AccountId = None
                }
                let directory = probeDirectory ()
                Directory.CreateDirectory directory |> ignore
                let statePath = Path.Combine(directory, "session.enc")
                let signalPath = Path.Combine(directory, "tan-bestaetigt")
                let logPath = Path.Combine(directory, "probe.log")
                use client = new System.Net.Http.HttpClient()
                let ports: ComdirectProbe.ProbePorts = {
                    Now = fun () -> DateTimeOffset.UtcNow
                    Sleep = fun span -> Async.Sleep span
                    Login = fun () -> startAuthFlow credentials apiKeys
                    ConfirmTan = fun session -> completeAuthFlow session apiKeys
                    Refresh = fun tokens -> refreshTokens client apiKeys DateTimeOffset.UtcNow tokens
                    FetchAccounts = fun state -> getAccounts state.RequestInfo state.Tokens
                    FetchTransactions = fun state accountId -> getTransactions state.RequestInfo state.Tokens accountId 7
                    SignalPresent = fun () -> File.Exists signalPath
                    ClearSignal = fun () -> File.Delete signalPath
                    EncryptionAvailable =
                        not (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable "BUDGETBUDDY_ENCRYPTION_KEY"))
                    LoadState = fun () -> ComdirectProbe.loadEncryptedState statePath
                    SaveState = ComdirectProbe.saveEncryptedState statePath
                    ClearState = fun () -> File.Delete statePath
                    Log = fun entry -> File.AppendAllLines(logPath, [ ComdirectProbe.formatLogEntry entry ])
                }
                match ComdirectProbe.run phase ports |> Async.RunSynchronously with
                | ComdirectProbe.Finished -> ()
                | ComdirectProbe.SessionLost reason when phase = ComdirectProbe.RunToRejection ->
                    printfn "Refresh abgelehnt, wie von Phase 3 erwartet: %s" reason
                | outcome -> failtestf "Sonde beendet: %A" outcome

        testCase "handles invalid credentials gracefully" <| fun () ->
            // Ruft die echte Comdirect-API auf, lief bis infrastructure-am805 ohne Schalter,
            // sobald die API-Schlüssel in .env standen (Fund des Netz-Wächters,
            // NetworkCallGuardTests.fs).
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            // Ein paralleler Login könnte die Sitzung der Sonde beenden.
            if (selectedProbePhase()).IsSome then
                Tests.skiptest "COMDIRECT_PROBE gesetzt: kein paralleler Login neben der Sonde"

            match getApiKeys() with
            | None ->
                Tests.skiptest "COMDIRECT_CLIENT_ID and COMDIRECT_CLIENT_SECRET not set in .env - skipping test"
            | Some apiKeys ->
                async {
                    let invalidCredentials: ComdirectSettings = {
                        ClientId = apiKeys.ClientId
                        ClientSecret = apiKeys.ClientSecret
                        Username = "invalid-username"
                        Password = "invalid-password"
                        AccountId = None
                    }

                    // Nur Schritt 1: dieser Aufruf kann keine TAN-Challenge starten.
                    let! result = initOAuth invalidCredentials apiKeys

                    match result with
                    | Ok _ ->
                        failtestf "Should have failed with invalid credentials"
                    | Error (ComdirectError.InvalidCredentials) ->
                        () // Expected error
                    | Error (ComdirectError.AuthenticationFailed _) ->
                        () // Also acceptable
                    | Error (ComdirectError.NetworkError (400, msg)) when msg.Contains("invalid_grant") ->
                        () // Also acceptable (400 with invalid_grant)
                    | Error (ComdirectError.NetworkError (401, _)) ->
                        () // Also acceptable (401 Unauthorized)
                    | Error err ->
                        failtestf "Unexpected error type: %A" err
                } |> Async.RunSynchronously

        // Note: Interactive tests with Console.ReadLine() don't work in dotnet test.
        // Use scripts/test-comdirect.fsx for manual end-to-end testing with TAN confirmation.
    ]
