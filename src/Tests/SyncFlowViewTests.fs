module BudgetBuddy.Tests.SyncFlowViewTests

open Expecto
open System
open Shared.Domain
open Components.SyncFlow.Types
open Components.SyncFlow.Views.TransactionRow
open Components.SyncFlow.Views.TransactionList

// ============================================
// Test Helpers
// ============================================

let private defaultDuplicateDetails = {
    TransactionReference = ""
    ReferenceFoundInYnab = false
    ImportIdFoundInYnab = false
    FuzzyMatchDate = None
    FuzzyMatchAmount = None
    FuzzyMatchPayee = None
}

let private mkTransaction
    (id: string)
    (bookingDate: DateTime)
    (amount: decimal)
    (payee: string option)
    (memo: string)
    (status: TransactionStatus)
    (categoryId: YnabCategoryId option)
    (duplicateStatus: DuplicateStatus)
    : SyncTransaction =
    {
        Transaction = {
            Id = TransactionId id
            BookingDate = bookingDate
            Amount = { Amount = amount; Currency = "EUR" }
            Payee = payee
            Memo = memo
            Reference = ""
            RawData = ""
        }
        Status = status
        CategoryId = categoryId
        CategoryName = None
        MatchedRuleId = None
        PayeeOverride = None
        ExternalLinks = []
        UserNotes = None
        DuplicateStatus = duplicateStatus
        YnabImportStatus = NotAttempted
        Splits = None
        SuggestedByOrderId = None
    }

/// Shorthand: create a simple pending transaction with NotDuplicate status
let private mkSimpleTx id date amount payee status categoryId =
    mkTransaction id date amount payee "" status categoryId (NotDuplicate defaultDuplicateDetails)

// ============================================
// Regression Tests for Existing Functions
// ============================================

[<Tests>]
let existingFunctionTests =
    testList "SyncFlow View - Regression" [

        testList "formatDateCompact" [
            testCase "formats date as dd.MM" <| fun () ->
                let date = DateTime(2025, 3, 15)
                let result = formatDateCompact date
                Expect.equal result "15.03" "Should format as day.month"

            testCase "pads single-digit day and month with leading zero" <| fun () ->
                let date = DateTime(2025, 1, 5)
                let result = formatDateCompact date
                Expect.equal result "05.01" "Should pad single digits"

            testCase "formats December 31st correctly" <| fun () ->
                let date = DateTime(2025, 12, 31)
                let result = formatDateCompact date
                Expect.equal result "31.12" "Should handle end of year"

            testCase "formats January 1st correctly" <| fun () ->
                let date = DateTime(2026, 1, 1)
                let result = formatDateCompact date
                Expect.equal result "01.01" "Should handle start of year"
        ]

        testList "categoryText" [
            testCase "returns category name when id matches" <| fun () ->
                let catId = Guid.Parse("11111111-1111-1111-1111-111111111111")
                let options = [
                    ("11111111-1111-1111-1111-111111111111", "Groceries")
                    ("22222222-2222-2222-2222-222222222222", "Transport")
                ]
                let result = categoryText (Some (YnabCategoryId catId)) options
                Expect.equal result "Groceries" "Should find matching category"

            testCase "returns Unknown when id does not match any option" <| fun () ->
                let catId = Guid.Parse("99999999-9999-9999-9999-999999999999")
                let options = [
                    ("11111111-1111-1111-1111-111111111111", "Groceries")
                ]
                let result = categoryText (Some (YnabCategoryId catId)) options
                Expect.equal result "Unknown" "Should return Unknown for unmatched id"

            testCase "returns em dash when categoryId is None" <| fun () ->
                let options = [
                    ("11111111-1111-1111-1111-111111111111", "Groceries")
                ]
                let result = categoryText None options
                Expect.equal result "\u2014" "Should return em dash for None"

            testCase "returns Unknown when category list is empty" <| fun () ->
                let catId = Guid.Parse("11111111-1111-1111-1111-111111111111")
                let result = categoryText (Some (YnabCategoryId catId)) []
                Expect.equal result "Unknown" "Should return Unknown for empty options"

            testCase "returns em dash when None and options are empty" <| fun () ->
                let result = categoryText None []
                Expect.equal result "\u2014" "Should return em dash even with empty options"
        ]

        testList "getRowStateClasses" [
            testCase "Skipped transaction returns tx-row status-skipped regardless of duplicate status" <| fun () ->
                let tx = mkTransaction "tx1" DateTime.Now -10.0m None "" Skipped None
                            (ConfirmedDuplicate ("ref", defaultDuplicateDetails))
                let result = getRowStateClasses tx
                Expect.equal result "tx-row status-skipped" "Skipped should use status-skipped class"

            testCase "ConfirmedDuplicate non-skipped returns duplicate styling" <| fun () ->
                let tx = mkTransaction "tx2" DateTime.Now -10.0m None "" Pending None
                            (ConfirmedDuplicate ("ref", defaultDuplicateDetails))
                let result = getRowStateClasses tx
                Expect.equal result "tx-row status-duplicate"
                    "Confirmed duplicate should have duplicate styling"

            testCase "PossibleDuplicate non-skipped returns duplicate styling" <| fun () ->
                let tx = mkTransaction "tx3" DateTime.Now -10.0m None "" Pending None
                            (PossibleDuplicate ("fuzzy match", defaultDuplicateDetails))
                let result = getRowStateClasses tx
                Expect.equal result "tx-row status-duplicate"
                    "Possible duplicate should have duplicate styling"

            testCase "NotDuplicate Pending uncategorized returns attention styling" <| fun () ->
                let tx = mkTransaction "tx4" DateTime.Now -10.0m None "" Pending None
                            (NotDuplicate defaultDuplicateDetails)
                let result = getRowStateClasses tx
                Expect.equal result "tx-row status-attention" "Uncategorized Pending should have attention styling"

            testCase "Skipped with PossibleDuplicate still returns tx-row status-skipped" <| fun () ->
                let tx = mkTransaction "tx5" DateTime.Now -10.0m None "" Skipped None
                            (PossibleDuplicate ("fuzzy", defaultDuplicateDetails))
                let result = getRowStateClasses tx
                Expect.equal result "tx-row status-skipped" "Skipped overrides duplicate styling"

            testCase "AutoCategorized NotDuplicate returns ready styling" <| fun () ->
                let catId = Some (YnabCategoryId (Guid.NewGuid()))
                let tx = mkTransaction "tx6" DateTime.Now -25.0m (Some "REWE") "" AutoCategorized catId
                            (NotDuplicate defaultDuplicateDetails)
                let result = getRowStateClasses tx
                Expect.equal result "tx-row status-ready" "AutoCategorized with NotDuplicate should have ready styling"

            testCase "NotDuplicate AutoCategorized with None categoryId returns ready" <| fun () ->
                let tx = mkTransaction "tx7" DateTime.Now -15.0m (Some "Shop") "" AutoCategorized None
                            (NotDuplicate defaultDuplicateDetails)
                let result = getRowStateClasses tx
                Expect.equal result "tx-row status-ready" "AutoCategorized with no categoryId should still be ready"

            testCase "NotDuplicate Imported returns ready" <| fun () ->
                let tx = mkTransaction "tx8" DateTime.Now -15.0m (Some "Shop") "" Imported None
                            (NotDuplicate defaultDuplicateDetails)
                let result = getRowStateClasses tx
                Expect.equal result "tx-row status-ready" "Imported with no categoryId should be ready"
        ]

        testList "getCategoryBadgeClass" [
            testCase "Skipped transaction returns badge-ready" <| fun () ->
                let tx = mkTransaction "tx1" DateTime.Now -10.0m (Some "Shop") "" Skipped None
                            (NotDuplicate defaultDuplicateDetails)
                let result = getCategoryBadgeClass tx
                Expect.equal result "tx-category badge-ready" "Skipped should return badge-ready"

            testCase "ConfirmedDuplicate non-skipped returns badge-duplicate" <| fun () ->
                let tx = mkTransaction "tx2" DateTime.Now -10.0m (Some "Shop") "" Pending None
                            (ConfirmedDuplicate ("ref", defaultDuplicateDetails))
                let result = getCategoryBadgeClass tx
                Expect.equal result "tx-category badge-duplicate" "ConfirmedDuplicate should return badge-duplicate"

            testCase "PossibleDuplicate non-skipped returns badge-duplicate" <| fun () ->
                let tx = mkTransaction "tx3" DateTime.Now -10.0m (Some "Shop") "" Pending None
                            (PossibleDuplicate ("fuzzy", defaultDuplicateDetails))
                let result = getCategoryBadgeClass tx
                Expect.equal result "tx-category badge-duplicate" "PossibleDuplicate should return badge-duplicate"

            testCase "NotDuplicate with CategoryId returns badge-ready" <| fun () ->
                let catId = Some (YnabCategoryId (Guid.NewGuid()))
                let tx = mkTransaction "tx4" DateTime.Now -10.0m (Some "Shop") "" Pending catId
                            (NotDuplicate defaultDuplicateDetails)
                let result = getCategoryBadgeClass tx
                Expect.equal result "tx-category badge-ready" "NotDuplicate with category should return badge-ready"

            testCase "NotDuplicate without CategoryId returns badge-attention" <| fun () ->
                let tx = mkTransaction "tx5" DateTime.Now -10.0m (Some "Shop") "" Pending None
                            (NotDuplicate defaultDuplicateDetails)
                let result = getCategoryBadgeClass tx
                Expect.equal result "tx-category badge-attention" "NotDuplicate without category should return badge-attention"
        ]

        testList "filterTransactions" [
            // Set up a representative list of transactions for filter tests
            let catId = Some (YnabCategoryId (Guid.NewGuid()))
            let now = DateTime(2025, 3, 15)

            let pendingUncategorized =
                mkSimpleTx "t1" now -10.0m (Some "Shop A") Pending None
            let autoCategorized =
                mkSimpleTx "t2" now -20.0m (Some "Shop B") AutoCategorized catId
            let manualCategorized =
                mkSimpleTx "t3" now -30.0m (Some "Shop C") ManualCategorized catId
            let skipped =
                mkSimpleTx "t4" now -40.0m (Some "Shop D") Skipped None
            let imported =
                mkSimpleTx "t5" now -50.0m (Some "Shop E") Imported catId
            let needsAttention =
                mkSimpleTx "t6" now -60.0m (Some "Amazon") NeedsAttention None
            let confirmedDup =
                mkTransaction "t7" now -70.0m (Some "Dup Shop") "" Pending None
                    (ConfirmedDuplicate ("ref123", defaultDuplicateDetails))

            let allTxs = [
                pendingUncategorized
                autoCategorized
                manualCategorized
                skipped
                imported
                needsAttention
                confirmedDup
            ]

            testCase "AllTransactions returns all" <| fun () ->
                let result = filterTransactions AllTransactions allTxs
                Expect.equal (List.length result) 7 "AllTransactions should return all transactions"

            testCase "ToBeImported excludes Skipped and Imported" <| fun () ->
                let result = filterTransactions ToBeImported allTxs
                let ids = result |> List.map (fun tx -> let (TransactionId id) = tx.Transaction.Id in id)
                Expect.equal (List.length result) 5 "ToBeImported should exclude Skipped and Imported"
                Expect.isFalse (ids |> List.contains "t4") "Should not contain Skipped"
                Expect.isFalse (ids |> List.contains "t5") "Should not contain Imported"

            testCase "CategorizedTransactions returns only categorized, non-skipped, non-imported" <| fun () ->
                let result = filterTransactions CategorizedTransactions allTxs
                let ids = result |> List.map (fun tx -> let (TransactionId id) = tx.Transaction.Id in id)
                Expect.equal (List.length result) 2 "Should return autoCategorized and manualCategorized"
                Expect.isTrue (ids |> List.contains "t2") "Should contain autoCategorized"
                Expect.isTrue (ids |> List.contains "t3") "Should contain manualCategorized"

            testCase "UncategorizedTransactions returns only uncategorized, non-skipped, non-imported" <| fun () ->
                let result = filterTransactions UncategorizedTransactions allTxs
                let ids = result |> List.map (fun tx -> let (TransactionId id) = tx.Transaction.Id in id)
                // pendingUncategorized (t1), needsAttention (t6), confirmedDup (t7) - all have None categoryId
                Expect.equal (List.length result) 3 "Should return uncategorized non-skipped non-imported"
                Expect.isTrue (ids |> List.contains "t1") "Should contain pending uncategorized"
                Expect.isTrue (ids |> List.contains "t6") "Should contain needsAttention"
                Expect.isTrue (ids |> List.contains "t7") "Should contain confirmed duplicate (uncategorized)"

            testCase "SkippedTransactions returns only skipped" <| fun () ->
                let result = filterTransactions SkippedTransactions allTxs
                Expect.equal (List.length result) 1 "Should return only skipped"
                let (TransactionId id) = (List.head result).Transaction.Id
                Expect.equal id "t4" "Should be the skipped transaction"

            testCase "ConfirmedDuplicates returns only confirmed duplicates" <| fun () ->
                let result = filterTransactions ConfirmedDuplicates allTxs
                Expect.equal (List.length result) 1 "Should return only confirmed duplicates"
                let (TransactionId id) = (List.head result).Transaction.Id
                Expect.equal id "t7" "Should be the confirmed duplicate transaction"

            testCase "empty list returns empty for all filters" <| fun () ->
                Expect.equal (filterTransactions AllTransactions []) [] "AllTransactions on empty"
                Expect.equal (filterTransactions ToBeImported []) [] "ToBeImported on empty"
                Expect.equal (filterTransactions CategorizedTransactions []) [] "Categorized on empty"
                Expect.equal (filterTransactions UncategorizedTransactions []) [] "Uncategorized on empty"
                Expect.equal (filterTransactions SkippedTransactions []) [] "Skipped on empty"
                Expect.equal (filterTransactions ConfirmedDuplicates []) [] "ConfirmedDuplicates on empty"
        ]
    ]

// ============================================================
open Components.SyncFlow.Views.ViewHelpers

[<Tests>]
let newFunctionTests =
    testList "SyncFlow View - New Helpers" [

        testList "groupTransactionsByDate" [
            testCase "3 transactions on 2 dates produces 2 groups, most recent first" <| fun () ->
                let day1 = DateTime(2025, 3, 14)
                let day2 = DateTime(2025, 3, 15)
                let tx1 = mkSimpleTx "a" (day1.AddHours(10.0)) -10.0m (Some "Shop A") Pending None
                let tx2 = mkSimpleTx "b" (day2.AddHours(9.0)) -20.0m (Some "Shop B") Pending None
                let tx3 = mkSimpleTx "c" (day1.AddHours(14.0)) -30.0m (Some "Shop C") AutoCategorized None
                let result = groupTransactionsByDate [ tx1; tx2; tx3 ]
                Expect.equal (List.length result) 2 "Should produce 2 groups"
                let (firstDate, firstGroup) = result.[0]
                let (secondDate, secondGroup) = result.[1]
                Expect.equal firstDate day2.Date "Most recent date should be first"
                Expect.equal (List.length firstGroup) 1 "Day2 should have 1 transaction"
                Expect.equal secondDate day1.Date "Older date should be second"
                Expect.equal (List.length secondGroup) 2 "Day1 should have 2 transactions"

            testCase "empty list returns empty result" <| fun () ->
                let result = groupTransactionsByDate []
                Expect.equal result [] "Empty input should produce empty output"

            testCase "all same date produces single group" <| fun () ->
                let day = DateTime(2025, 3, 15)
                let tx1 = mkSimpleTx "a" (day.AddHours(8.0)) -10.0m (Some "A") Pending None
                let tx2 = mkSimpleTx "b" (day.AddHours(12.0)) -20.0m (Some "B") Pending None
                let tx3 = mkSimpleTx "c" (day.AddHours(18.0)) -30.0m (Some "C") Pending None
                let result = groupTransactionsByDate [ tx1; tx2; tx3 ]
                Expect.equal (List.length result) 1 "Should produce single group"
                let (groupDate, groupTxs) = result.[0]
                Expect.equal groupDate day.Date "Group date should match"
                Expect.equal (List.length groupTxs) 3 "Group should contain all 3 transactions"
        ]

        testList "titleCasePayee" [
            testCase "ALL CAPS multi-word becomes title case" <| fun () ->
                let result = titleCasePayee "STUDENTENWERK HANNOVER"
                Expect.equal result "Studentenwerk Hannover" "Should title-case all-caps words"

            testCase "ALL CAPS with common words" <| fun () ->
                let result = titleCasePayee "REWE SAGT DANKE"
                Expect.equal result "Rewe Sagt Danke" "Should title-case each word"

            testCase "short word 2 chars or less stays uppercase" <| fun () ->
                let result = titleCasePayee "DM"
                Expect.equal result "DM" "2-char words should stay uppercase"

            testCase "already mixed case stays as-is" <| fun () ->
                let result = titleCasePayee "Amazon.de"
                Expect.equal result "Amazon.de" "Mixed case should not be modified"

            testCase "empty string returns empty string" <| fun () ->
                let result = titleCasePayee ""
                Expect.equal result "" "Empty string should return empty string"

            testCase "whitespace-only returns unchanged" <| fun () ->
                let result = titleCasePayee "  "
                Expect.equal result "  " "Whitespace-only should return unchanged"

            testCase "single ALL CAPS word longer than 2 chars becomes title case" <| fun () ->
                let result = titleCasePayee "REWE"
                Expect.equal result "Rewe" "Single all-caps word should become title case"

            testCase "3-char ALL CAPS word becomes title case" <| fun () ->
                let result = titleCasePayee "DHL"
                Expect.equal result "Dhl" "3-char all-caps word should become title case"
        ]

        testList "calculateImportCounts" [
            testCase "mix of statuses returns correct counts" <| fun () ->
                let catId = Some (YnabCategoryId (Guid.NewGuid()))
                let now = DateTime(2025, 3, 15)
                let txs = [
                    // ToImport + categorized (ready)
                    mkSimpleTx "r1" now -10.0m (Some "A") AutoCategorized catId
                    mkSimpleTx "r2" now -20.0m (Some "B") ManualCategorized catId
                    // ToImport + uncategorized (needs category)
                    mkSimpleTx "n1" now -30.0m (Some "C") Pending None
                    mkSimpleTx "n2" now -40.0m (Some "D") NeedsAttention None
                    // Skipped
                    mkSimpleTx "s1" now -50.0m (Some "E") Skipped None
                    // Confirmed duplicate
                    mkTransaction "d1" now -60.0m (Some "F") "" Pending None
                        (ConfirmedDuplicate ("ref", defaultDuplicateDetails))
                ]
                let counts = calculateImportCounts txs
                Expect.equal counts.Total 6 "Total should be 6"
                Expect.equal counts.ToImport 5 "ToImport excludes skipped (not imported since none are Imported)"
                Expect.equal counts.NeedCategory 2 "NeedCategory: 2 uncategorized non-skipped"
                Expect.equal counts.Duplicates 1 "Duplicates: 1 confirmed"
                Expect.equal counts.Skipped 1 "Skipped: 1"

            testCase "empty list returns all zeros" <| fun () ->
                let counts = calculateImportCounts []
                Expect.equal counts.Total 0 "Total should be 0"
                Expect.equal counts.ToImport 0 "ToImport should be 0"
                Expect.equal counts.NeedCategory 0 "NeedCategory should be 0"
                Expect.equal counts.Duplicates 0 "Duplicates should be 0"
                Expect.equal counts.Skipped 0 "Skipped should be 0"

            testCase "ConfirmedDuplicate that is also Skipped counts in both dups and skipped" <| fun () ->
                let now = DateTime(2025, 3, 15)
                let txs = [
                    mkTransaction "ds1" now -10.0m (Some "Dup+Skip") "" Skipped None
                        (ConfirmedDuplicate ("ref", defaultDuplicateDetails))
                ]
                let counts = calculateImportCounts txs
                Expect.equal counts.Total 1 "Total should be 1"
                Expect.equal counts.Duplicates 1 "Should count as duplicate"
                Expect.equal counts.Skipped 1 "Should count as skipped"
                Expect.equal counts.ToImport 0 "Should not count as toImport (skipped)"
                Expect.equal counts.NeedCategory 0 "Should not need category (skipped)"
        ]

        testList "calculateProgressSegments" [
            testCase "mixed counts produce correct percentages" <| fun () ->
                // 8 ready, 2 attention, 3 skipped out of 13 total
                let counts = { Total = 13; ToImport = 10; NeedCategory = 2; Duplicates = 0; Skipped = 3 }
                let segments = calculateProgressSegments counts
                // ReadyPct = (ToImport - NeedCategory) / Total = 8/13
                let expectedReady = (float 8) / (float 13) * 100.0
                let expectedAttention = (float 2) / (float 13) * 100.0
                let expectedSkipped = (float 3) / (float 13) * 100.0
                Expect.floatClose Accuracy.medium segments.ReadyPct expectedReady "ReadyPct"
                Expect.floatClose Accuracy.medium segments.AttentionPct expectedAttention "AttentionPct"
                Expect.floatClose Accuracy.medium segments.SkippedPct expectedSkipped "SkippedPct"

            testCase "all ready produces 100% ready" <| fun () ->
                let counts = { Total = 5; ToImport = 5; NeedCategory = 0; Duplicates = 0; Skipped = 0 }
                let segments = calculateProgressSegments counts
                Expect.floatClose Accuracy.medium segments.ReadyPct 100.0 "ReadyPct should be 100"
                Expect.floatClose Accuracy.medium segments.AttentionPct 0.0 "AttentionPct should be 0"
                Expect.floatClose Accuracy.medium segments.SkippedPct 0.0 "SkippedPct should be 0"

            testCase "zero total returns all zero percentages" <| fun () ->
                let counts = { Total = 0; ToImport = 0; NeedCategory = 0; Duplicates = 0; Skipped = 0 }
                let segments = calculateProgressSegments counts
                Expect.floatClose Accuracy.medium segments.ReadyPct 0.0 "ReadyPct should be 0"
                Expect.floatClose Accuracy.medium segments.AttentionPct 0.0 "AttentionPct should be 0"
                Expect.floatClose Accuracy.medium segments.SkippedPct 0.0 "SkippedPct should be 0"
        ]

        testList "formatDailyTotal" [
            testCase "sums amounts as int64 milliunits" <| fun () ->
                let day = DateTime(2025, 3, 15)
                let txs = [
                    mkSimpleTx "a" day -20.000m (Some "A") Pending None
                    mkSimpleTx "b" day -40.800m (Some "B") Pending None
                    mkSimpleTx "c" day -13.990m (Some "C") Pending None
                ]
                let result = formatDailyTotal txs
                // -20000 + -40800 + -13990 = -74790 milliunits
                Expect.equal result -74790L "Should sum to -74790 milliunits"

            testCase "empty list returns 0" <| fun () ->
                let result = formatDailyTotal []
                Expect.equal result 0L "Empty list should return 0"

            testCase "mixed positive and negative amounts sum correctly" <| fun () ->
                let day = DateTime(2025, 3, 15)
                let txs = [
                    mkSimpleTx "a" day 100.000m (Some "Income") Pending None
                    mkSimpleTx "b" day -30.500m (Some "Expense") Pending None
                    mkSimpleTx "c" day -20.250m (Some "Expense2") Pending None
                ]
                let result = formatDailyTotal txs
                // 100000 + -30500 + -20250 = 49250 milliunits
                Expect.equal result 49250L "Should correctly sum mixed positive and negative amounts"
        ]
    ]
