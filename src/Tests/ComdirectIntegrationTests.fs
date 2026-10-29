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

// ============================================
// Integration Tests (require Comdirect credentials in .env)
// ============================================

[<Tests>]
let integrationTests =
    testList "Comdirect Integration Tests (requires .env)" [
        testCase "can initiate OAuth flow (up to TAN challenge)" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getApiKeys(), getCredentials() with
            | None, _ | _, None ->
                Tests.skiptest "COMDIRECT credentials not set in .env - skipping integration test"
            | Some apiKeys, Some (username, password) ->
                async {
                    printfn "⚠️  This test will initiate a real Push-TAN - make sure you have your phone ready!"

                    let credentials: ComdirectSettings = {
                        ClientId = apiKeys.ClientId
                        ClientSecret = apiKeys.ClientSecret
                        Username = username
                        Password = password
                        AccountId = getEnvVar "COMDIRECT_ACCOUNT_ID"  // Optional
                    }

                    let! result = startAuthFlow credentials apiKeys

                    match result with
                    | Ok authSession ->
                        // Verify we got a session
                        Expect.isNotEmpty authSession.SessionId "Session ID should not be empty"
                        Expect.isNotEmpty authSession.RequestInfo.RequestId "Request ID should not be empty"
                        Expect.isNotEmpty authSession.Tokens.Access "Access token should not be empty"
                        Expect.isNotEmpty authSession.Tokens.Refresh "Refresh token should not be empty"

                        // Verify we got a TAN challenge
                        match authSession.Challenge with
                        | None ->
                            failtestf "Expected to receive a Push-TAN challenge"
                        | Some challenge ->
                            Expect.equal challenge.Type "P_TAN_PUSH" "Challenge type should be P_TAN_PUSH"
                            Expect.isNotEmpty challenge.Id "Challenge ID should not be empty"

                            printfn $"✅ OAuth flow initiated successfully"
                            printfn $"   Session ID: {authSession.SessionId}"
                            printfn $"   Challenge ID: {challenge.Id}"
                            printfn ""
                            printfn "⚠️  NOTE: A Push-TAN was sent to your phone."
                            printfn "   You can ignore it - this was just a test."
                            printfn "   The TAN will expire automatically."

                    | Error err ->
                        failtestf "Failed to initiate OAuth flow: %A" err
                } |> Async.RunSynchronously

        testCase "handles invalid credentials gracefully" <| fun () ->
            // Ruft die echte Comdirect-API auf, lief bis infrastructure-am805 ohne Schalter,
            // sobald die API-Schlüssel in .env standen (Fund des Netz-Wächters,
            // NetworkCallGuardTests.fs).
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

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

                    let! result = startAuthFlow invalidCredentials apiKeys

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
