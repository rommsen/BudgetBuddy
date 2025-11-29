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

        testCase "documents manual testing procedure" <| fun () ->
            // This test just documents how to do manual integration testing

            printfn ""
            printfn "==================================================="
            printfn "MANUAL COMDIRECT INTEGRATION TESTING"
            printfn "==================================================="
            printfn ""
            printfn "To test the complete Comdirect OAuth flow:"
            printfn ""
            printfn "1. Fill in Comdirect credentials in .env:"
            printfn "   - COMDIRECT_CLIENT_ID"
            printfn "   - COMDIRECT_CLIENT_SECRET"
            printfn "   - COMDIRECT_USERNAME"
            printfn "   - COMDIRECT_PASSWORD"
            printfn "   - COMDIRECT_ACCOUNT_ID"
            printfn ""
            printfn "2. Run the interactive test script:"
            printfn "   $ dotnet fsi scripts/test-comdirect.fsx"
            printfn ""
            printfn "3. When prompted, confirm the Push-TAN on your phone"
            printfn ""
            printfn "4. The script will complete the OAuth flow and fetch"
            printfn "   your recent transactions"
            printfn ""
            printfn "This cannot be automated because Push-TAN requires"
            printfn "human interaction on a mobile device."
            printfn ""

            // Always pass
            ()
    ]

// ============================================
// Unit Tests for Comdirect Types
// ============================================

[<Tests>]
let typeTests =
    testList "Comdirect Type Tests" [
        testCase "RequestInfo encodes correctly" <| fun () ->
            let requestInfo = {
                RequestId = "123456789"
                SessionId = "550e8400-e29b-41d4-a716-446655440000"
            }

            let encoded = requestInfo.Encode()
            let expectedPattern = "\"clientRequestId\""

            Expect.stringContains encoded expectedPattern "Encoded JSON should contain clientRequestId"
            Expect.stringContains encoded "123456789" "Encoded JSON should contain request ID"
            Expect.stringContains encoded "550e8400-e29b-41d4-a716-446655440000" "Encoded JSON should contain session ID"

        testCase "ApiKeys stores credentials" <| fun () ->
            let apiKeys = {
                ClientId = "test-client-id"
                ClientSecret = "test-secret"
            }

            Expect.equal apiKeys.ClientId "test-client-id" "ClientId should match"
            Expect.equal apiKeys.ClientSecret "test-secret" "ClientSecret should match"

        testCase "Challenge validates P_TAN_PUSH type" <| fun () ->
            let challenge = {
                Id = "challenge-123"
                Type = "P_TAN_PUSH"
            }

            Expect.equal challenge.Type "P_TAN_PUSH" "Challenge type should be P_TAN_PUSH"
            Expect.equal challenge.Id "challenge-123" "Challenge ID should match"

        testCase "AuthSession contains all required fields" <| fun () ->
            let requestInfo = {
                RequestId = "123456789"
                SessionId = "session-guid"
            }

            let tokens = {
                Access = "access-token"
                Refresh = "refresh-token"
            }

            let challenge = {
                Id = "challenge-123"
                Type = "P_TAN_PUSH"
            }

            let authSession = {
                RequestInfo = requestInfo
                SessionId = "session-id"
                Tokens = tokens
                SessionIdentifier = "session-identifier-123"
                Challenge = Some challenge
            }

            Expect.equal authSession.RequestInfo.RequestId "123456789" "Request ID should match"
            Expect.equal authSession.SessionId "session-id" "Session ID should match"
            Expect.equal authSession.Tokens.Access "access-token" "Access token should match"
            Expect.isSome authSession.Challenge "Challenge should be present"
    ]
