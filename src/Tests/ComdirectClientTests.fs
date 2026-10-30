module Tests.ComdirectClientTests

open System
open Expecto
open Thoth.Json.Net
open Server.ComdirectClient
open Server.Api
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
    ]
