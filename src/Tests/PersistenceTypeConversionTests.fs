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
        SuggestedByOrderId = None
        ReviewSteps = []
        AskLena = false
    }

[<Tests>]
let typeConversionTests =
    // initializeDatabase() läuft erst, wenn ein Testfall tatsächlich ausgeführt wird,
    // nicht schon beim Modul-Laden (ADR 0030, infra-sdad8). Idempotent (CREATE TABLE IF
    // NOT EXISTS), der wiederholte Aufruf pro Testfall kostet nur eine überflüssige Prüfung.
    let testCase name body =
        testCase name (fun () ->
            initializeDatabase ()
            body ())

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

        // categorization-gdrx8: the review trail (rule proposal, then Roman's
        // correction) must survive a server restart, i.e. a save/reload roundtrip
        // against SQLite. This is the "Kassenbuch-Buchung" that ledger-qvt84 will
        // later read at push time (ADR 0021), so the persisted shape must be exact.
        testList "ReviewSteps Conversions" [
            testCase "rule step followed by a user correction roundtrips in order" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = { createTestSession AwaitingBankAuth with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let ruleId = RuleId (Guid.NewGuid())
                let categoryA = YnabCategoryId (Guid.NewGuid())
                let categoryB = YnabCategoryId (Guid.NewGuid())
                let ruleAt = DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc)
                let userAt = DateTime(2026, 9, 11, 8, 5, 0, DateTimeKind.Utc)
                let ruleStep = {
                    At = ruleAt
                    By = ByRule (ruleId, "Regel X")
                    Category = Some categoryA
                    CategoryName = Some "Lebensmittel"
                    Splits = None
                    PayeeOverride = None
                }
                let userStep = {
                    At = userAt
                    By = ByUser
                    Category = Some categoryB
                    CategoryName = Some "Drogerie"
                    Splits = None
                    PayeeOverride = Some "dm"
                }
                let tx =
                    { createTestSyncTransaction sessionId ManualCategorized with
                        ReviewSteps = [ ruleStep; userStep ] }
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                Expect.isNonEmpty retrieved "Should have at least one transaction"
                let first = List.head retrieved
                Expect.equal first.ReviewSteps [ ruleStep; userStep ] "Review steps should survive save/reload, oldest first"

            testCase "a review step carrying splits roundtrips the split target" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = { createTestSession AwaitingBankAuth with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let splitStep = {
                    At = DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc)
                    By = ByUser
                    Category = None
                    CategoryName = None
                    Splits = Some [
                        { Target = ToCategory (YnabCategoryId (Guid.NewGuid()), "Lebensmittel")
                          Amount = { Amount = 80M; Currency = "EUR" }
                          Memo = None }
                        { Target = ToTransfer (YnabAccountId (Guid.NewGuid()), "Bargeld")
                          Amount = { Amount = 20M; Currency = "EUR" }
                          Memo = Some "Abhebung" }
                    ]
                    PayeeOverride = None
                }
                let tx =
                    { createTestSyncTransaction sessionId ManualCategorized with
                        ReviewSteps = [ splitStep ] }
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                let first = List.head retrieved
                Expect.equal first.ReviewSteps [ splitStep ] "Split target inside a review step should roundtrip"

            testCase "a transaction with no review steps yet roundtrips as an empty list" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = { createTestSession AwaitingBankAuth with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let tx = createTestSyncTransaction sessionId Pending
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                let first = List.head retrieved
                Expect.isEmpty first.ReviewSteps "A transaction never reviewed has no review steps"

            // Iteration 2 (Entscheidung Roman, 2026-09-11): der dritte ReviewActor-Fall
            // für den Order-ID-Abgleich muss denselben Codec-Weg wie ByRule/ByUser
            // durchlaufen.
            testCase "a ByOrderIdMatch step roundtrips its order id" <| fun () ->
                let sessionId = SyncSessionId (Guid.NewGuid())
                let session = { createTestSession AwaitingBankAuth with Id = sessionId }
                SyncSessions.createSession session |> Async.RunSynchronously

                let orderMatchStep = {
                    At = DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc)
                    By = ByOrderIdMatch "ABC-1234567-1234567"
                    Category = Some (YnabCategoryId (Guid.NewGuid()))
                    CategoryName = Some "Elektronik"
                    Splits = None
                    PayeeOverride = None
                }
                let tx =
                    { createTestSyncTransaction sessionId AutoCategorized with
                        ReviewSteps = [ orderMatchStep ] }
                SyncTransactions.saveTransaction sessionId tx |> Async.RunSynchronously

                let retrieved = SyncTransactions.getTransactionsBySession sessionId |> Async.RunSynchronously
                let first = List.head retrieved
                Expect.equal first.ReviewSteps [ orderMatchStep ] "ByOrderIdMatch (incl. order id) should roundtrip"
        ]
    ]

[<Tests>]
let connectionDisposalRegressionTests =
    // Siehe typeConversionTests oben: Initialisierung erst innerhalb der Testausführung,
    // nicht beim Modul-Laden (ADR 0030, infra-sdad8).
    let testCase name body =
        testCase name (fun () ->
            initializeDatabase ()
            body ())

    testList "Persistence Connection Disposal" [
        // Concurrency guard for infra-001: the flaky
        // `SqliteConnection.RemoveCommand index-out-of-range during connection disposal`.
        // Root cause was that test-mode getConnection() handed the SAME shared
        // SqliteConnection object to every Dapper call, so concurrent operations mutated
        // one connection's internal command list at the same time during command Dispose.
        // The fix hands out a fresh connection per operation (keep-alive anchor preserves
        // the shared in-memory DB).
        //
        // NOTE: the original race needed the FULL suite's parallel test cases all hitting
        // the one shared connection at once; it is intermittent and not faithfully
        // reproducible from a single isolated case (this test passes against both the
        // broken and the fixed code). The real regression proof is determinism over many
        // full runs (15/15 fresh runs green after the fix vs. reproduces within ~2-8 runs
        // before — see diary/development.md). This case stays as a defense-in-depth guard
        // that the persistence layer tolerates concurrent operations.
        testCase "concurrent persistence operations do not crash on command disposal" <| fun () ->
            let runOnce (i: int) =
                async {
                    let rule = createTestRule Contains Payee
                    let rule = { rule with Name = sprintf "concurrent-%d" i }
                    do! Rules.insertRule rule
                    let! retrieved = Rules.getRuleById rule.Id
                    match retrieved with
                    | Some r -> Expect.equal r.PatternType Contains "PatternType should round-trip under concurrency"
                    | None -> failtest "Rule not found after concurrent insert"
                }

            // 50 operations in parallel maximizes the chance of two commands disposing
            // on the same connection object simultaneously (the original race).
            [ for i in 1..50 -> runOnce i ]
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
    ]
