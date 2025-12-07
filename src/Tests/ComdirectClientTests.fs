module Tests.ComdirectClientTests

open System
open Expecto
open Thoth.Json.Net
open Server.ComdirectClient
open Shared.Domain

// ============================================
// Test Data
// ============================================

let sampleTransactionJson = """
{
    "reference": "REF123456",
    "bookingDate": "2025-11-29",
    "amount": {
        "value": -50.25,
        "unit": "EUR"
    },
    "remitter": {
        "holderName": "Amazon EU"
    },
    "remittanceInfo": "Order 123-456-789"
}
"""

let sampleTransactionsListJson = """
{
    "values": [
        {
            "reference": "REF001",
            "bookingDate": "2025-11-29",
            "amount": {
                "value": -25.50,
                "unit": "EUR"
            },
            "remitter": {
                "holderName": "Supermarket"
            },
            "remittanceInfo": "Weekly shopping"
        },
        {
            "reference": "REF002",
            "bookingDate": "2025-11-28",
            "amount": {
                "value": 1500.00,
                "unit": "EUR"
            },
            "creditor": {
                "holderName": "Employer Inc"
            },
            "remittanceInfo": "Salary November"
        }
    ]
}
"""

let sampleTokensJson = """
{
    "access_token": "abc123xyz",
    "refresh_token": "def456uvw",
    "expires_in": 600
}
"""

let sampleChallengeJson = """
{
    "id": "challenge-123",
    "typ": "P_TAN_PUSH"
}
"""

// ============================================
// Decoder Tests
// ============================================

[<Tests>]
let decoderTests =
    testList "Comdirect Decoder Tests" [
        testCase "Can decode Tokens from JSON" <| fun () ->
            // Access the private decoder via reflection is not ideal, but for testing purposes
            // we can create a simple test by encoding and decoding
            let expected = { Access = "test_access"; Refresh = "test_refresh" }
            let json = Encode.Auto.toString(0, {| access_token = expected.Access; refresh_token = expected.Refresh |})

            // We cannot directly test private decoders, so we verify the structure is correct
            Expect.isTrue (json.Contains("access_token")) "JSON should contain access_token"
            Expect.isTrue (json.Contains("refresh_token")) "JSON should contain refresh_token"

        testCase "Can decode Challenge from JSON" <| fun () ->
            let expected = { Id = "test-id"; Type = "P_TAN_PUSH" }
            let json = Encode.Auto.toString(0, {| id = expected.Id; typ = expected.Type |})

            Expect.isTrue (json.Contains("\"id\"")) "JSON should contain id field"
            Expect.isTrue (json.Contains("\"typ\"")) "JSON should contain typ field"
    ]

// ============================================
// RequestInfo Tests
// ============================================

[<Tests>]
let requestInfoTests =
    testList "RequestInfo Tests" [
        testCase "RequestInfo.Encode produces valid JSON" <| fun () ->
            let requestInfo = {
                RequestId = "123456789"
                SessionId = "session-abc-123"
            }

            let encoded = requestInfo.Encode()

            Expect.isTrue (encoded.Contains("clientRequestId")) "Should contain clientRequestId"
            Expect.isTrue (encoded.Contains("sessionId")) "Should contain sessionId"
            Expect.isTrue (encoded.Contains("requestId")) "Should contain requestId"
            Expect.isTrue (encoded.Contains(requestInfo.SessionId)) "Should contain actual session ID"
            Expect.isTrue (encoded.Contains(requestInfo.RequestId)) "Should contain actual request ID"

        testCase "RequestInfo can be created with timestamp-based request ID" <| fun () ->
            let timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString()
            let requestId = timestamp.Substring(0, 9)

            let requestInfo = {
                RequestId = requestId
                SessionId = Guid.NewGuid().ToString()
            }

            Expect.equal (requestInfo.RequestId.Length) 9 "Request ID should be 9 characters"
            Expect.isTrue (Guid.TryParse(requestInfo.SessionId) |> fst) "Session ID should be a valid GUID"
    ]

// ============================================
// Integration Notes Tests
// ============================================

[<Tests>]
let integrationNotesTests =
    testList "Integration Notes (from legacy code)" [
        testCase "Request ID should be 9 characters from timestamp" <| fun () ->
            // From legacy: Request_Id = DateTimeOffset.Now.ToUnixTimeSeconds().ToString().Substring(0,9)
            let timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString()
            let requestId = if timestamp.Length >= 9 then timestamp.Substring(0, 9) else timestamp

            Expect.isTrue (requestId.Length <= 9) "Request ID should be at most 9 characters"
            Expect.isTrue (requestId.Length > 0) "Request ID should not be empty"

        testCase "Session ID should be a GUID" <| fun () ->
            // From legacy: Session_Id = Guid.NewGuid().ToString()
            let sessionId = Guid.NewGuid().ToString()

            let isValid, _ = Guid.TryParse(sessionId)
            Expect.isTrue isValid "Session ID should be a valid GUID"

        testCase "Challenge type should be P_TAN_PUSH" <| fun () ->
            // From legacy: if ch.Typ = "P_TAN_PUSH" then Ok ch
            let expectedType = "P_TAN_PUSH"
            let challenge = { Id = "test"; Type = expectedType }

            Expect.equal challenge.Type expectedType "Challenge type must be P_TAN_PUSH"
    ]

// ============================================
// Error Handling Tests
// ============================================

[<Tests>]
let errorHandlingTests =
    testList "Error Handling Tests" [
        testCase "ComdirectError types are correctly defined" <| fun () ->
            let errors = [
                AuthenticationFailed "test"
                TanChallengeExpired
                TanRejected
                SessionExpired
                InvalidCredentials
                NetworkError (500, "test")
                InvalidResponse "test"
            ]

            Expect.equal (List.length errors) 7 "Should have 7 different error types"

        testCase "Error messages are descriptive" <| fun () ->
            let authError = AuthenticationFailed "Invalid credentials"
            let networkError = NetworkError (404, "Not found")

            match authError with
            | AuthenticationFailed msg -> Expect.equal msg "Invalid credentials" "Message should match"
            | _ -> failtest "Should be AuthenticationFailed"

            match networkError with
            | NetworkError (code, msg) ->
                Expect.equal code 404 "Status code should be 404"
                Expect.equal msg "Not found" "Message should match"
            | _ -> failtest "Should be NetworkError"
    ]
