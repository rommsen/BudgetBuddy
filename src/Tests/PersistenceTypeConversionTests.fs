module PersistenceTypeConversionTests

open System

// CRITICAL: Set test mode BEFORE importing Persistence module
// F# module initialization happens on first access, so we must set this first
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Persistence

/// Helper to create a test Rule with specified PatternType and TargetField
let createTestRule (patternType: PatternType) (targetField: TargetField) : Rule =
    {
        Id = RuleId (Guid.NewGuid())
        Name = "Test Rule"
        Pattern = "test"
        PatternType = patternType
        TargetField = targetField
        CategoryId = YnabCategoryId (Guid.NewGuid())
        CategoryName = "Test Category"
        PayeeOverride = Some "Test Payee Override"
        Priority = 1
        Enabled = true
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

/// Helper to create a test SyncSession with specified status
let createTestSession (status: SyncSessionStatus) : SyncSession =
    {
        Id = SyncSessionId (Guid.NewGuid())
        StartedAt = DateTime.UtcNow
        CompletedAt = Some DateTime.UtcNow
        Status = status
        TransactionCount = 5
        ImportedCount = 3
        SkippedCount = 2
    }

/// Helper to create a test SyncTransaction with specified status
let createTestSyncTransaction (sessionId: SyncSessionId) (status: TransactionStatus) : SyncTransaction =
    {
        Transaction = {
            Id = TransactionId "test-tx-1"
            BookingDate = DateTime.UtcNow
            Amount = { Amount = 100M; Currency = "EUR" }
            Payee = Some "Test Payee"
            Memo = "Test Memo"
            Reference = "REF123"
            RawData = ""
        }
        Status = status
        CategoryId = None
        CategoryName = None
        MatchedRuleId = None
        PayeeOverride = None
        ExternalLinks = []
        UserNotes = None
        DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF123")
        YnabImportStatus = NotAttempted
        Splits = None
    }

[<Tests>]
let typeConversionTests =
    // Initialize test database
    initializeDatabase()

    testList "Persistence Type Conversions" [
        testList "PatternType Conversions" [
            testCase "Regex roundtrip" <| fun () ->
                let rule = createTestRule Regex Payee
                Rules.insertRule rule |> Async.RunSynchronously
                let retrieved = Rules.getRuleById rule.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Rule not found after insert"
                | Some r -> Expect.equal r.PatternType Regex "PatternType should be Regex"

            testCase "Contains roundtrip" <| fun () ->
                let rule = createTestRule Contains Payee
                Rules.insertRule rule |> Async.RunSynchronously
                let retrieved = Rules.getRuleById rule.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Rule not found after insert"
                | Some r -> Expect.equal r.PatternType Contains "PatternType should be Contains"

            testCase "Exact roundtrip" <| fun () ->
                let rule = createTestRule Exact Payee
                Rules.insertRule rule |> Async.RunSynchronously
                let retrieved = Rules.getRuleById rule.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Rule not found after insert"
                | Some r -> Expect.equal r.PatternType Exact "PatternType should be Exact"
        ]

        testList "TargetField Conversions" [
            testCase "Payee roundtrip" <| fun () ->
                let rule = createTestRule Regex Payee
                Rules.insertRule rule |> Async.RunSynchronously
                let retrieved = Rules.getRuleById rule.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Rule not found after insert"
                | Some r -> Expect.equal r.TargetField Payee "TargetField should be Payee"

            testCase "Memo roundtrip" <| fun () ->
                let rule = createTestRule Regex Memo
                Rules.insertRule rule |> Async.RunSynchronously
                let retrieved = Rules.getRuleById rule.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Rule not found after insert"
                | Some r -> Expect.equal r.TargetField Memo "TargetField should be Memo"

            testCase "Combined roundtrip" <| fun () ->
                let rule = createTestRule Regex Combined
                Rules.insertRule rule |> Async.RunSynchronously
                let retrieved = Rules.getRuleById rule.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Rule not found after insert"
                | Some r -> Expect.equal r.TargetField Combined "TargetField should be Combined"
        ]

        testList "SyncSessionStatus Conversions" [
            testCase "AwaitingBankAuth roundtrip" <| fun () ->
                let session = createTestSession AwaitingBankAuth
                SyncSessions.createSession session |> Async.RunSynchronously
                let retrieved = SyncSessions.getSessionById session.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Session not found after insert"
                | Some s -> Expect.equal s.Status AwaitingBankAuth "Status should be AwaitingBankAuth"

            testCase "AwaitingTan roundtrip" <| fun () ->
                let session = createTestSession AwaitingTan
                SyncSessions.createSession session |> Async.RunSynchronously
                let retrieved = SyncSessions.getSessionById session.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Session not found after insert"
                | Some s -> Expect.equal s.Status AwaitingTan "Status should be AwaitingTan"

            testCase "FetchingTransactions roundtrip" <| fun () ->
                let session = createTestSession FetchingTransactions
                SyncSessions.createSession session |> Async.RunSynchronously
                let retrieved = SyncSessions.getSessionById session.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Session not found after insert"
                | Some s -> Expect.equal s.Status FetchingTransactions "Status should be FetchingTransactions"

            testCase "ReviewingTransactions roundtrip" <| fun () ->
                let session = createTestSession ReviewingTransactions
                SyncSessions.createSession session |> Async.RunSynchronously
                let retrieved = SyncSessions.getSessionById session.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Session not found after insert"
                | Some s -> Expect.equal s.Status ReviewingTransactions "Status should be ReviewingTransactions"

            testCase "ImportingToYnab roundtrip" <| fun () ->
                let session = createTestSession ImportingToYnab
                SyncSessions.createSession session |> Async.RunSynchronously
                let retrieved = SyncSessions.getSessionById session.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Session not found after insert"
                | Some s -> Expect.equal s.Status ImportingToYnab "Status should be ImportingToYnab"

            testCase "Completed roundtrip" <| fun () ->
                let session = createTestSession Completed
                SyncSessions.createSession session |> Async.RunSynchronously
                let retrieved = SyncSessions.getSessionById session.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Session not found after insert"
                | Some s -> Expect.equal s.Status Completed "Status should be Completed"

            testCase "Failed roundtrip with error message" <| fun () ->
                let errorMsg = "Network timeout"
                let session = createTestSession (Failed errorMsg)
                SyncSessions.createSession session |> Async.RunSynchronously
                let retrieved = SyncSessions.getSessionById session.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Session not found after insert"
                | Some s ->
                    match s.Status with
                    | Failed msg -> Expect.equal msg errorMsg "Failed message should be preserved"
                    | _ -> failtest "Status should be Failed"

            testCase "Failed roundtrip with complex error message" <| fun () ->
                let errorMsg = "Complex error: API returned 500, body: {\"error\":\"Internal Server Error\"}"
                let session = createTestSession (Failed errorMsg)
                SyncSessions.createSession session |> Async.RunSynchronously
                let retrieved = SyncSessions.getSessionById session.Id |> Async.RunSynchronously
                match retrieved with
                | None -> failtest "Session not found after insert"
                | Some s ->
                    match s.Status with
                    | Failed msg -> Expect.equal msg errorMsg "Complex failed message should be preserved"
                    | _ -> failtest "Status should be Failed"
        ]

        testList "TransactionStatus Conversions" [
            testCase "Pending roundtrip" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = createTestSession AwaitingBankAuth
                let session = { session with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let tx = createTestSyncTransaction sessionId Pending
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                Expect.isNonEmpty retrieved "Should have at least one transaction"
                let first = List.head retrieved
                Expect.equal first.Status Pending "Status should be Pending"

            testCase "AutoCategorized roundtrip" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = createTestSession AwaitingBankAuth
                let session = { session with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let tx = createTestSyncTransaction sessionId AutoCategorized
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                Expect.isNonEmpty retrieved "Should have at least one transaction"
                let first = List.head retrieved
                Expect.equal first.Status AutoCategorized "Status should be AutoCategorized"

            testCase "ManualCategorized roundtrip" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = createTestSession AwaitingBankAuth
                let session = { session with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let tx = createTestSyncTransaction sessionId ManualCategorized
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                Expect.isNonEmpty retrieved "Should have at least one transaction"
                let first = List.head retrieved
                Expect.equal first.Status ManualCategorized "Status should be ManualCategorized"

            testCase "NeedsAttention roundtrip" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = createTestSession AwaitingBankAuth
                let session = { session with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let tx = createTestSyncTransaction sessionId NeedsAttention
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                Expect.isNonEmpty retrieved "Should have at least one transaction"
                let first = List.head retrieved
                Expect.equal first.Status NeedsAttention "Status should be NeedsAttention"

            testCase "Skipped roundtrip" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = createTestSession AwaitingBankAuth
                let session = { session with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let tx = createTestSyncTransaction sessionId Skipped
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                Expect.isNonEmpty retrieved "Should have at least one transaction"
                let first = List.head retrieved
                Expect.equal first.Status Skipped "Status should be Skipped"

            testCase "Imported roundtrip" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = createTestSession AwaitingBankAuth
                let session = { session with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let tx = createTestSyncTransaction sessionId Imported
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                Expect.isNonEmpty retrieved "Should have at least one transaction"
                let first = List.head retrieved
                Expect.equal first.Status Imported "Status should be Imported"
        ]
    ]
