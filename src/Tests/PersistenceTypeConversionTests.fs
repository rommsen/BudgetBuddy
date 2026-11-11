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
        Trigger = Manual
        Counts = { NewEntries = 5; ByRule = 2; ByOrderId = 1; WithoutCategory = 2; SafeDuplicatesSkipped = 3; Held = 1; NotInYnab = 1 }
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

        // banking-import-d0fey (ADR 0093 Punkt 10): Auslöser und Zahlen des Abrufs gehen in `sync_sessions`
        // und kommen unverändert zurück.
        testList "Zahlen des Abrufs" [
            testCase "Zahlen und Auslöser einer beendeten Sitzung kommen unverändert zurück" <| fun () ->
                let session = createTestSession Completed
                SyncSessions.createSession session |> Async.RunSynchronously
                let changed = { session with Counts = { session.Counts with NewEntries = 7; NotInYnab = 2 } }
                SyncSessions.updateSession changed |> Async.RunSynchronously
                let retrieved = SyncSessions.getSessionById session.Id |> Async.RunSynchronously
                Expect.equal (retrieved |> Option.map (fun s -> s.Counts)) (Some changed.Counts) "Zahlen nach dem Ändern"
                Expect.equal (retrieved |> Option.map (fun s -> s.Trigger)) (Some Manual) "Auslöser"
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
