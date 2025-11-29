#!/usr/bin/env dotnet fsi

// ============================================
// Comdirect OAuth Integration Test Script
// ============================================
//
// Usage:
//   1. Copy .env.example to .env and fill in Comdirect credentials
//   2. Run: dotnet fsi scripts/test-comdirect.fsx
//   3. When prompted, confirm the Push-TAN on your phone
//
// This script will:
//   - Load credentials from .env
//   - Test initOAuth
//   - Test session identifier
//   - Test TAN challenge request
//   - Wait for user to confirm TAN
//   - Test session activation
//   - Test extended token retrieval
//   - Test transaction fetching

#r "nuget: Thoth.Json.Net, 12.0.0"
#r "nuget: FsToolkit.ErrorHandling, 4.16.0"

#load "EnvLoader.fsx"
#load "../src/Shared/Domain.fs"
#load "../src/Server/ComdirectClient.fs"

open System
open EnvLoader
open Shared.Domain
open Server.ComdirectClient

// ============================================
// Load Environment
// ============================================

printfn "==================================================="
printfn "Comdirect OAuth Integration Test"
printfn "==================================================="
printfn ""
printfn "⚠️  WARNING: This test will initiate a real Push-TAN"
printfn "   Make sure you have your phone ready!"
printfn ""

let env = EnvLoader.loadProjectEnv()
EnvLoader.printEnvInfo env

let clientId = EnvLoader.getRequired env "COMDIRECT_CLIENT_ID"
let clientSecret = EnvLoader.getRequired env "COMDIRECT_CLIENT_SECRET"
let username = EnvLoader.getRequired env "COMDIRECT_USERNAME"
let password = EnvLoader.getRequired env "COMDIRECT_PASSWORD"
let accountId = EnvLoader.getOptional env "COMDIRECT_ACCOUNT_ID"

let apiKeys: ApiKeys = {
    ClientId = clientId
    ClientSecret = clientSecret
}

let credentials: ComdirectSettings = {
    ClientId = clientId
    ClientSecret = clientSecret
    Username = username
    Password = password
    AccountId = accountId  // Optional - only for fetching transactions
}

printfn "Press ENTER to start the OAuth flow, or Ctrl+C to cancel..."
Console.ReadLine() |> ignore
printfn ""

// ============================================
// Test 1: Start OAuth Flow
// ============================================

printfn "Test 1: Starting OAuth flow..."

let authFlowResult =
    startAuthFlow credentials apiKeys
    |> Async.RunSynchronously

match authFlowResult with
| Error err ->
    printfn $"❌ FAILED: Could not start OAuth flow"
    printfn $"   Error: {err}"
    printfn ""
    printfn "Please check:"
    printfn "  1. Your Comdirect credentials in .env are correct"
    printfn "  2. Your Comdirect API access is enabled"
    printfn "  3. Your internet connection is working"
    Environment.Exit(1)

| Ok authSession ->
    printfn "✅ SUCCESS: OAuth flow started"
    printfn $"   Session ID: {authSession.SessionId}"
    printfn $"   Request ID: {authSession.RequestInfo.RequestId}"
    printfn ""

    match authSession.Challenge with
    | None ->
        printfn "❌ FAILED: No TAN challenge received"
        printfn "   Expected Push-TAN challenge but got none"
        Environment.Exit(1)

    | Some challenge ->
        printfn $"✅ Push-TAN Challenge received"
        printfn $"   Challenge ID: {challenge.Id}"
        printfn $"   Challenge Type: {challenge.Type}"
        printfn ""

        // ============================================
        // Test 2: Wait for TAN Confirmation
        // ============================================

        printfn "==================================================="
        printfn "⏳ WAITING FOR TAN CONFIRMATION"
        printfn "==================================================="
        printfn ""
        printfn "Please check your phone and confirm the Push-TAN now."
        printfn ""
        printfn "After you confirmed on your phone, press ENTER here..."
        Console.ReadLine() |> ignore
        printfn ""

        printfn "Test 2: Completing OAuth flow after TAN confirmation..."

        let completeResult =
            completeAuthFlow authSession apiKeys
            |> Async.RunSynchronously

        match completeResult with
        | Error err ->
            printfn $"❌ FAILED: Could not complete auth flow"
            printfn $"   Error: {err}"
            printfn ""
            printfn "Common issues:"
            printfn "  1. TAN was not confirmed on phone"
            printfn "  2. TAN confirmation timed out"
            printfn "  3. Network issue during activation"
            Environment.Exit(1)

        | Ok tokens ->
            printfn "✅ SUCCESS: OAuth flow completed"
            printfn $"   Access Token: {tokens.Access.Substring(0, 20)}..."
            printfn $"   Refresh Token: {tokens.Refresh.Substring(0, 20)}..."
            printfn ""

            // ============================================
            // Test 3: Fetch Transactions
            // ============================================

            // Only fetch transactions if accountId is provided
            match accountId with
            | None ->
                printfn "⚠️  COMDIRECT_ACCOUNT_ID not set - skipping transaction fetch"
                printfn "   (OAuth flow completed successfully!)"
                printfn ""
                printfn "==================================================="
                printfn "PARTIAL SUCCESS ✅"
                printfn "==================================================="
                printfn ""
                printfn "Summary:"
                printfn $"  - OAuth flow: SUCCESS"
                printfn $"  - TAN confirmation: SUCCESS"
                printfn $"  - Token retrieval: SUCCESS"
                printfn $"  - Transaction fetch: SKIPPED (no account ID)"
                printfn ""
                printfn "To fetch transactions, add COMDIRECT_ACCOUNT_ID to .env"
                printfn ""

            | Some accountId ->
                printfn "Test 3: Fetching transactions..."
                printfn $"   Account ID: {accountId}"
                printfn $"   Days back: 30"
                printfn ""

                let requestInfo = authSession.RequestInfo
                let txResult =
                    getTransactions requestInfo tokens accountId 30
                    |> Async.RunSynchronously

                match txResult with
                | Error err ->
                    printfn $"❌ FAILED: Could not fetch transactions"
                    printfn $"   Error: {err}"

                | Ok transactions ->
                    printfn $"✅ SUCCESS: Fetched {transactions.Length} transactions"
                    printfn ""

                    if transactions.Length > 0 then
                        printfn "Sample transactions (first 5):"
                        for tx in transactions |> List.take (min 5 transactions.Length) do
                            let (TransactionId id) = tx.Id
                            let payee = tx.Payee |> Option.defaultValue "(no payee)"
                            let dateStr = tx.BookingDate.ToString("yyyy-MM-dd")
                            printfn $"   - {dateStr} | {tx.Amount.Amount:F2} {tx.Amount.Currency} | {payee}"
                            printfn $"     Memo: {tx.Memo.Substring(0, min 60 tx.Memo.Length)}..."
                        printfn ""
                    else
                        printfn "   No transactions found in the last 30 days"
                        printfn ""

                    // ============================================
                    // Summary
                    // ============================================

                    printfn "==================================================="
                    printfn "ALL TESTS PASSED ✅"
                    printfn "==================================================="
                    printfn ""
                    printfn "Summary:"
                    printfn $"  - OAuth flow: SUCCESS"
                    printfn $"  - TAN confirmation: SUCCESS"
                    printfn $"  - Token retrieval: SUCCESS"
                    printfn $"  - Transactions fetched: {transactions.Length}"
                    printfn ""
                    printfn "Your Comdirect integration is working correctly!"
                    printfn ""
                    printfn "Note: The access token will expire after a while."
                    printfn "      In production, use the refresh token to get a new access token."
                    printfn ""
