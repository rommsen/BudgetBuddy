module Tests.SyncSessionManagerTests

open System
open Expecto
open Shared.Domain
open Server.SyncSessionManager

// ============================================
// Test Helpers
// ============================================

/// Creates a sample bank transaction for testing
let createBankTransaction () =
    {
        Id = TransactionId (Guid.NewGuid().ToString())
        BookingDate = DateTime.UtcNow
        Amount = { Amount = -5000m; Currency = "EUR" }
        Payee = Some "Test Payee"
        Memo = "Test Memo"
        Reference = "REF123"
        RawData = ""
    }

/// Creates a sample sync transaction for testing
let createSyncTransaction () =
    {
        Transaction = createBankTransaction ()
        Status = Pending
        CategoryId = None
        CategoryName = None
        MatchedRuleId = None
        PayeeOverride = None
        ExternalLinks = []
        UserNotes = None
        DuplicateStatus = NotDuplicate
        Splits = None
    }

/// Creates a sync transaction with a specific status
let createSyncTransactionWithStatus (status: TransactionStatus) =
    { createSyncTransaction () with Status = status }

/// Resets the session state before each test to ensure isolation
let resetSession () =
    clearSession ()

// ============================================
// Session Lifecycle Tests
// ============================================

// All tests must run sequentially because SyncSessionManager uses global mutable state
[<Tests>]
let sessionLifecycleTests =
    testSequenced <| testList "Session Lifecycle Tests" [
        test "startNewSession creates session with AwaitingBankAuth status" {
            resetSession ()
            let session = startNewSession ()

            Expect.equal session.Status AwaitingBankAuth "Session should start with AwaitingBankAuth status"
            Expect.equal session.TransactionCount 0 "Transaction count should be 0"
            Expect.equal session.ImportedCount 0 "Imported count should be 0"
            Expect.equal session.SkippedCount 0 "Skipped count should be 0"
            Expect.isNone session.CompletedAt "CompletedAt should be None"
        }

        test "startNewSession generates unique session IDs" {
            resetSession ()
            let session1 = startNewSession ()
            clearSession ()
            let session2 = startNewSession ()

            Expect.notEqual session1.Id session2.Id "Each session should have a unique ID"
        }

        test "getCurrentSession returns None when no session exists" {
            resetSession ()
            let result = getCurrentSession ()

            Expect.isNone result "Should return None when no session exists"
        }

        test "getCurrentSession returns session after startNewSession" {
            resetSession ()
            let created = startNewSession ()
            let retrieved = getCurrentSession ()

            Expect.isSome retrieved "Should return Some when session exists"
            Expect.equal retrieved.Value.Id created.Id "Retrieved session should match created session"
        }

        test "completeSession sets Completed status and timestamp" {
            resetSession ()
            let _ = startNewSession ()

            completeSession ()

            let session = getCurrentSession ()
            Expect.isSome session "Session should still exist"
            Expect.equal session.Value.Status Completed "Status should be Completed"
            Expect.isSome session.Value.CompletedAt "CompletedAt should be set"
        }

        test "failSession sets Failed status with error message" {
            resetSession ()
            let _ = startNewSession ()
            let errorMessage = "Test error message"

            failSession errorMessage

            let session = getCurrentSession ()
            Expect.isSome session "Session should still exist"
            match session.Value.Status with
            | Failed msg -> Expect.equal msg errorMessage "Error message should match"
            | other -> failtest $"Expected Failed status but got {other}"
            Expect.isSome session.Value.CompletedAt "CompletedAt should be set"
        }

        test "clearSession removes current session" {
            resetSession ()
            let _ = startNewSession ()

            clearSession ()

            let result = getCurrentSession ()
            Expect.isNone result "Session should be None after clearing"
        }

        test "updateSessionStatus changes status correctly" {
            resetSession ()
            let _ = startNewSession ()

            updateSessionStatus FetchingTransactions

            let session = getCurrentSession ()
            Expect.isSome session "Session should exist"
            Expect.equal session.Value.Status FetchingTransactions "Status should be updated"
        }

        test "updateSession modifies session correctly" {
            resetSession ()
            let created = startNewSession ()
            let updated = { created with Status = ReviewingTransactions; TransactionCount = 10 }

            updateSession updated

            let session = getCurrentSession ()
            Expect.isSome session "Session should exist"
            Expect.equal session.Value.Status ReviewingTransactions "Status should be updated"
            Expect.equal session.Value.TransactionCount 10 "TransactionCount should be updated"
        }

        test "updateSession throws when no session exists" {
            resetSession ()
            let fakeSession = {
                Id = SyncSessionId (Guid.NewGuid())
                StartedAt = DateTime.UtcNow
                CompletedAt = None
                Status = AwaitingBankAuth
                TransactionCount = 0
                ImportedCount = 0
                SkippedCount = 0
            }

            Expect.throws (fun () -> updateSession fakeSession) "Should throw when no session exists"
        }

        test "updateSessionStatus throws when no session exists" {
            resetSession ()

            Expect.throws (fun () -> updateSessionStatus Completed) "Should throw when no session exists"
        }
    ]

// ============================================
// Transaction Operations Tests
// ============================================

[<Tests>]
let transactionOperationsTests =
    testSequenced <| testList "Transaction Operations Tests" [
        test "addTransactions updates TransactionCount correctly" {
            resetSession ()
            let _ = startNewSession ()
            let transactions = [ createSyncTransaction (); createSyncTransaction (); createSyncTransaction () ]

            addTransactions transactions

            let session = getCurrentSession ()
            Expect.isSome session "Session should exist"
            Expect.equal session.Value.TransactionCount 3 "TransactionCount should be 3"
        }

        test "addTransactions can be called multiple times" {
            resetSession ()
            let _ = startNewSession ()

            addTransactions [ createSyncTransaction () ]
            addTransactions [ createSyncTransaction (); createSyncTransaction () ]

            let session = getCurrentSession ()
            Expect.isSome session "Session should exist"
            Expect.equal session.Value.TransactionCount 3 "TransactionCount should be 3 after multiple adds"
        }

        test "addTransactions throws when no session exists" {
            resetSession ()

            Expect.throws (fun () -> addTransactions [ createSyncTransaction () ]) "Should throw when no session exists"
        }

        test "getTransactions returns empty list when no session exists" {
            resetSession ()
            let result = getTransactions ()

            Expect.isEmpty result "Should return empty list when no session exists"
        }

        test "getTransactions returns empty list for new session" {
            resetSession ()
            let _ = startNewSession ()
            let result = getTransactions ()

            Expect.isEmpty result "Should return empty list for new session"
        }

        test "getTransactions returns added transactions" {
            resetSession ()
            let _ = startNewSession ()
            let tx1 = createSyncTransaction ()
            let tx2 = createSyncTransaction ()

            addTransactions [ tx1; tx2 ]

            let result = getTransactions ()
            Expect.equal (List.length result) 2 "Should return 2 transactions"

            let ids = result |> List.map (fun t -> t.Transaction.Id) |> Set.ofList
            Expect.isTrue (Set.contains tx1.Transaction.Id ids) "Should contain first transaction"
            Expect.isTrue (Set.contains tx2.Transaction.Id ids) "Should contain second transaction"
        }

        test "getTransaction returns None for unknown ID" {
            resetSession ()
            let _ = startNewSession ()
            let unknownId = TransactionId (Guid.NewGuid().ToString())

            let result = getTransaction unknownId

            Expect.isNone result "Should return None for unknown ID"
        }

        test "getTransaction returns None when no session exists" {
            resetSession ()
            let unknownId = TransactionId (Guid.NewGuid().ToString())

            let result = getTransaction unknownId

            Expect.isNone result "Should return None when no session exists"
        }

        test "getTransaction returns transaction by ID" {
            resetSession ()
            let _ = startNewSession ()
            let tx = createSyncTransaction ()

            addTransactions [ tx ]

            let result = getTransaction tx.Transaction.Id
            Expect.isSome result "Should return the transaction"
            Expect.equal result.Value.Transaction.Id tx.Transaction.Id "Transaction ID should match"
        }

        test "updateTransaction modifies transaction in session" {
            resetSession ()
            let _ = startNewSession ()
            let tx = createSyncTransaction ()
            addTransactions [ tx ]

            let updated = { tx with Status = Imported; CategoryName = Some "New Category" }
            updateTransaction updated

            let result = getTransaction tx.Transaction.Id
            Expect.isSome result "Should return the transaction"
            Expect.equal result.Value.Status Imported "Status should be updated"
            Expect.equal result.Value.CategoryName (Some "New Category") "CategoryName should be updated"
        }

        test "updateTransaction throws when no session exists" {
            resetSession ()
            let tx = createSyncTransaction ()

            Expect.throws (fun () -> updateTransaction tx) "Should throw when no session exists"
        }

        test "updateTransactions modifies multiple transactions" {
            resetSession ()
            let _ = startNewSession ()
            let tx1 = createSyncTransaction ()
            let tx2 = createSyncTransaction ()
            addTransactions [ tx1; tx2 ]

            let updated1 = { tx1 with Status = Imported }
            let updated2 = { tx2 with Status = Skipped }
            updateTransactions [ updated1; updated2 ]

            let result1 = getTransaction tx1.Transaction.Id
            let result2 = getTransaction tx2.Transaction.Id
            Expect.equal result1.Value.Status Imported "First transaction should be Imported"
            Expect.equal result2.Value.Status Skipped "Second transaction should be Skipped"
        }

        test "getStatusCounts returns correct counts per status" {
            resetSession ()
            let _ = startNewSession ()

            // Add transactions with various statuses
            let pending1 = createSyncTransactionWithStatus Pending
            let pending2 = createSyncTransactionWithStatus Pending
            let imported = createSyncTransactionWithStatus Imported
            let skipped1 = createSyncTransactionWithStatus Skipped
            let skipped2 = createSyncTransactionWithStatus Skipped
            let skipped3 = createSyncTransactionWithStatus Skipped

            addTransactions [ pending1; pending2; imported; skipped1; skipped2; skipped3 ]

            let counts = getStatusCounts ()

            Expect.equal (Map.tryFind Pending counts) (Some 2) "Should have 2 Pending"
            Expect.equal (Map.tryFind Imported counts) (Some 1) "Should have 1 Imported"
            Expect.equal (Map.tryFind Skipped counts) (Some 3) "Should have 3 Skipped"
            Expect.equal (Map.tryFind AutoCategorized counts) None "Should have no AutoCategorized"
        }

        test "getStatusCounts returns empty map when no session exists" {
            resetSession ()
            let counts = getStatusCounts ()

            Expect.isEmpty counts "Should return empty map when no session exists"
        }

        test "updateSessionCounts updates ImportedCount and SkippedCount correctly" {
            resetSession ()
            let _ = startNewSession ()

            let imported1 = createSyncTransactionWithStatus Imported
            let imported2 = createSyncTransactionWithStatus Imported
            let skipped = createSyncTransactionWithStatus Skipped
            addTransactions [ imported1; imported2; skipped ]

            updateSessionCounts ()

            let session = getCurrentSession ()
            Expect.isSome session "Session should exist"
            Expect.equal session.Value.ImportedCount 2 "ImportedCount should be 2"
            Expect.equal session.Value.SkippedCount 1 "SkippedCount should be 1"
        }

        test "completeSession updates counts before completing" {
            resetSession ()
            let _ = startNewSession ()

            let imported = createSyncTransactionWithStatus Imported
            let skipped = createSyncTransactionWithStatus Skipped
            addTransactions [ imported; skipped ]

            completeSession ()

            let session = getCurrentSession ()
            Expect.isSome session "Session should exist"
            Expect.equal session.Value.ImportedCount 1 "ImportedCount should be 1"
            Expect.equal session.Value.SkippedCount 1 "SkippedCount should be 1"
            Expect.equal session.Value.Status Completed "Status should be Completed"
        }
    ]

// ============================================
// Session Validation Tests
// ============================================

[<Tests>]
let sessionValidationTests =
    testSequenced <| testList "Session Validation Tests" [
        test "validateSession returns error when no session exists" {
            resetSession ()
            let fakeId = SyncSessionId (Guid.NewGuid())

            let result = validateSession fakeId

            match result with
            | Error (SessionNotFound _) -> ()
            | Ok _ -> failtest "Should return error when no session exists"
            | Error other -> failtest $"Expected SessionNotFound but got {other}"
        }

        test "validateSession returns error for mismatched session ID" {
            resetSession ()
            let _ = startNewSession ()
            let wrongId = SyncSessionId (Guid.NewGuid())

            let result = validateSession wrongId

            match result with
            | Error (SessionNotFound _) -> ()
            | Ok _ -> failtest "Should return error for mismatched ID"
            | Error other -> failtest $"Expected SessionNotFound but got {other}"
        }

        test "validateSession returns Ok for matching session ID" {
            resetSession ()
            let session = startNewSession ()

            let result = validateSession session.Id

            Expect.isOk result "Should return Ok for matching session ID"
        }

        test "validateSessionStatus returns error when no session exists" {
            resetSession ()
            let fakeId = SyncSessionId (Guid.NewGuid())

            let result = validateSessionStatus fakeId AwaitingBankAuth

            match result with
            | Error (SessionNotFound _) -> ()
            | Ok _ -> failtest "Should return error when no session exists"
            | Error other -> failtest $"Expected SessionNotFound but got {other}"
        }

        test "validateSessionStatus returns error for wrong status" {
            resetSession ()
            let session = startNewSession ()
            // Session starts with AwaitingBankAuth, so we expect a different status

            let result = validateSessionStatus session.Id ReviewingTransactions

            match result with
            | Error (InvalidSessionState (expected, actual)) ->
                Expect.stringContains expected "ReviewingTransactions" "Expected status should mention ReviewingTransactions"
                Expect.stringContains actual "AwaitingBankAuth" "Actual status should mention AwaitingBankAuth"
            | Ok _ -> failtest "Should return error for wrong status"
            | Error other -> failtest $"Expected InvalidSessionState but got {other}"
        }

        test "validateSessionStatus returns Ok for correct status" {
            resetSession ()
            let session = startNewSession ()

            let result = validateSessionStatus session.Id AwaitingBankAuth

            Expect.isOk result "Should return Ok for correct status"
        }

        test "validateSessionStatus works after status update" {
            resetSession ()
            let session = startNewSession ()
            updateSessionStatus FetchingTransactions

            let result = validateSessionStatus session.Id FetchingTransactions

            Expect.isOk result "Should return Ok for updated status"
        }

        test "validateSessionStatus returns error for previous status after update" {
            resetSession ()
            let session = startNewSession ()
            updateSessionStatus FetchingTransactions

            let result = validateSessionStatus session.Id AwaitingBankAuth

            match result with
            | Error (InvalidSessionState _) -> ()
            | Ok _ -> failtest "Should return error for previous status"
            | Error other -> failtest $"Expected InvalidSessionState but got {other}"
        }
    ]

// ============================================
// Edge Cases and Integration Tests
// ============================================

[<Tests>]
let edgeCaseTests =
    testSequenced <| testList "Edge Cases and Integration" [
        test "starting new session clears previous session" {
            resetSession ()
            let session1 = startNewSession ()
            addTransactions [ createSyncTransaction () ]

            let session2 = startNewSession ()

            // New session should have no transactions
            let transactions = getTransactions ()
            Expect.isEmpty transactions "New session should start with empty transactions"
            Expect.notEqual session1.Id session2.Id "Session IDs should be different"
        }

        test "completeSession has no effect when no session exists" {
            resetSession ()

            // Should not throw
            completeSession ()

            let session = getCurrentSession ()
            Expect.isNone session "Session should still be None"
        }

        test "failSession has no effect when no session exists" {
            resetSession ()

            // Should not throw
            failSession "Error"

            let session = getCurrentSession ()
            Expect.isNone session "Session should still be None"
        }

        test "updateSessionCounts has no effect when no session exists" {
            resetSession ()

            // Should not throw
            updateSessionCounts ()

            let session = getCurrentSession ()
            Expect.isNone session "Session should still be None"
        }

        test "full sync workflow simulation" {
            resetSession ()

            // Start session
            let session = startNewSession ()
            Expect.equal session.Status AwaitingBankAuth "Initial status"

            // Progress through states
            updateSessionStatus AwaitingTan
            updateSessionStatus FetchingTransactions

            // Add transactions
            let tx1 = createSyncTransactionWithStatus Pending
            let tx2 = createSyncTransactionWithStatus Pending
            let tx3 = createSyncTransactionWithStatus Pending
            addTransactions [ tx1; tx2; tx3 ]

            updateSessionStatus ReviewingTransactions

            // User categorizes transactions
            let updated1 = { tx1 with Status = Imported }
            let updated2 = { tx2 with Status = Imported }
            let updated3 = { tx3 with Status = Skipped }
            updateTransactions [ updated1; updated2; updated3 ]

            updateSessionStatus ImportingToYnab

            // Complete
            completeSession ()

            let finalSession = getCurrentSession ()
            Expect.isSome finalSession "Session should exist"
            Expect.equal finalSession.Value.Status Completed "Final status should be Completed"
            Expect.equal finalSession.Value.TransactionCount 3 "Should have 3 transactions"
            Expect.equal finalSession.Value.ImportedCount 2 "Should have 2 imported"
            Expect.equal finalSession.Value.SkippedCount 1 "Should have 1 skipped"
            Expect.isSome finalSession.Value.CompletedAt "CompletedAt should be set"
        }

        test "transaction with same ID overwrites previous" {
            resetSession ()
            let _ = startNewSession ()

            let bankTx = createBankTransaction ()
            let tx1 = { createSyncTransaction () with
                         Transaction = bankTx
                         Status = Pending }
            addTransactions [ tx1 ]

            let tx2 = { tx1 with Status = Imported; CategoryName = Some "Updated" }
            addTransactions [ tx2 ]

            let result = getTransaction bankTx.Id
            Expect.isSome result "Transaction should exist"
            Expect.equal result.Value.Status Imported "Status should be from second add"
            Expect.equal result.Value.CategoryName (Some "Updated") "CategoryName should be from second add"

            // TransactionCount should still be 1 because it's the same transaction
            let session = getCurrentSession ()
            Expect.equal session.Value.TransactionCount 1 "TransactionCount should be 1 (same ID)"
        }
    ]
