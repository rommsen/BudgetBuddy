module Tests.SplitTransactionTests

open System
open Expecto
open Shared.Domain

// ============================================
// Test Helpers
// ============================================

let private createTestTransaction (id: string) (amount: decimal) : BankTransaction =
    {
        Id = TransactionId id
        BookingDate = DateTime.Today
        Amount = { Amount = amount; Currency = "EUR" }
        Payee = Some "Test Payee"
        Memo = "Test Memo"
        Reference = $"REF-{id}"
        RawData = "{}"
    }

let private createTestSplit (categoryName: string) (amount: decimal) : TransactionSplit =
    {
        CategoryId = YnabCategoryId (Guid.NewGuid())
        CategoryName = categoryName
        Amount = { Amount = amount; Currency = "EUR" }
        Memo = None
    }

// ============================================
// Split Transaction Type Tests
// ============================================

[<Tests>]
let splitTypeTests =
    testList "Split Transaction Types" [
        testCase "TransactionSplit can be created with required fields" <| fun () ->
            let split = createTestSplit "Groceries" -50.00m
            Expect.equal split.CategoryName "Groceries" "Category name should match"
            Expect.equal split.Amount.Amount -50.00m "Amount should match"
            Expect.equal split.Amount.Currency "EUR" "Currency should match"
            Expect.isNone split.Memo "Memo should be None by default"

        testCase "TransactionSplit can have optional memo" <| fun () ->
            let split = { createTestSplit "Dining" -25.00m with Memo = Some "Lunch" }
            Expect.equal split.Memo (Some "Lunch") "Memo should be set"

        testCase "SyncTransaction can have no splits (None)" <| fun () ->
            let bankTx = createTestTransaction "tx-1" -100.00m
            let syncTx = {
                Transaction = bankTx
                Status = Pending
                CategoryId = None
                CategoryName = None
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF-tx-1")
                YnabImportStatus = NotAttempted
                Splits = None
            }
            Expect.isNone syncTx.Splits "Splits should be None"

        testCase "SyncTransaction can have empty splits list (Some [])" <| fun () ->
            let bankTx = createTestTransaction "tx-2" -100.00m
            let syncTx = {
                Transaction = bankTx
                Status = ManualCategorized
                CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                CategoryName = Some "Category"
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF-tx-2")
                YnabImportStatus = NotAttempted
                Splits = Some []
            }
            Expect.isSome syncTx.Splits "Splits should be Some"
            Expect.isEmpty syncTx.Splits.Value "Splits list should be empty"

        testCase "SyncTransaction can have multiple splits" <| fun () ->
            let bankTx = createTestTransaction "tx-3" -150.00m
            let splits = [
                createTestSplit "Groceries" -100.00m
                createTestSplit "Household" -50.00m
            ]
            let syncTx = {
                Transaction = bankTx
                Status = ManualCategorized
                CategoryId = None  // No single category when split
                CategoryName = None
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF-tx-3")
                YnabImportStatus = NotAttempted
                Splits = Some splits
            }
            Expect.isSome syncTx.Splits "Splits should be Some"
            Expect.equal syncTx.Splits.Value.Length 2 "Should have 2 splits"
    ]

// ============================================
// Split Amount Validation Tests
// ============================================

[<Tests>]
let splitAmountValidationTests =
    testList "Split Amount Validation" [
        testCase "Split amounts should sum to transaction amount" <| fun () ->
            let bankTx = createTestTransaction "tx-sum" -100.00m
            let splits = [
                createTestSplit "Category1" -60.00m
                createTestSplit "Category2" -40.00m
            ]
            let totalSplitAmount = splits |> List.sumBy (fun s -> s.Amount.Amount)
            Expect.equal totalSplitAmount bankTx.Amount.Amount "Split amounts should equal transaction amount"

        testCase "Detects when splits don't sum to transaction amount" <| fun () ->
            let bankTx = createTestTransaction "tx-mismatch" -100.00m
            let splits = [
                createTestSplit "Category1" -70.00m
                createTestSplit "Category2" -20.00m
            ]
            let totalSplitAmount = splits |> List.sumBy (fun s -> s.Amount.Amount)
            let diff = abs (totalSplitAmount - bankTx.Amount.Amount)
            Expect.isGreaterThan diff 0.01m "Should detect amount mismatch"

        testCase "Splits with three categories" <| fun () ->
            let bankTx = createTestTransaction "tx-three" -120.00m
            let splits = [
                createTestSplit "Groceries" -50.00m
                createTestSplit "Dining" -30.00m
                createTestSplit "Household" -40.00m
            ]
            let totalSplitAmount = splits |> List.sumBy (fun s -> s.Amount.Amount)
            Expect.equal totalSplitAmount bankTx.Amount.Amount "Three splits should sum correctly"

        testCase "Handles positive amounts (refunds)" <| fun () ->
            let bankTx = createTestTransaction "tx-refund" 75.00m
            let splits = [
                createTestSplit "Refund1" 50.00m
                createTestSplit "Refund2" 25.00m
            ]
            let totalSplitAmount = splits |> List.sumBy (fun s -> s.Amount.Amount)
            Expect.equal totalSplitAmount bankTx.Amount.Amount "Positive splits should sum correctly"
    ]

// ============================================
// Split Transaction Ready for Import Tests
// ============================================

[<Tests>]
let splitImportReadyTests =
    testList "Split Transaction Import Ready Check" [
        testCase "Transaction with valid splits is ready for import" <| fun () ->
            let bankTx = createTestTransaction "tx-ready" -100.00m
            let splits = [
                createTestSplit "Cat1" -60.00m
                createTestSplit "Cat2" -40.00m
            ]
            let syncTx = {
                Transaction = bankTx
                Status = ManualCategorized
                CategoryId = None
                CategoryName = None
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF-tx-ready")
                YnabImportStatus = NotAttempted
                Splits = Some splits
            }

            // Check import readiness logic
            let isReadyForImport =
                syncTx.Status <> Skipped &&
                (syncTx.CategoryId.IsSome ||
                 (syncTx.Splits |> Option.map (fun s -> s.Length >= 2) |> Option.defaultValue false))

            Expect.isTrue isReadyForImport "Split transaction should be ready for import"

        testCase "Transaction with single split is NOT ready for import" <| fun () ->
            let bankTx = createTestTransaction "tx-single" -100.00m
            let splits = [ createTestSplit "Cat1" -100.00m ]
            let syncTx = {
                Transaction = bankTx
                Status = ManualCategorized
                CategoryId = None
                CategoryName = None
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF-tx-single")
                YnabImportStatus = NotAttempted
                Splits = Some splits
            }

            let isReadyForImport =
                syncTx.Status <> Skipped &&
                (syncTx.CategoryId.IsSome ||
                 (syncTx.Splits |> Option.map (fun s -> s.Length >= 2) |> Option.defaultValue false))

            Expect.isFalse isReadyForImport "Single split should NOT be ready for import"

        testCase "Skipped split transaction is NOT ready for import" <| fun () ->
            let bankTx = createTestTransaction "tx-skipped" -100.00m
            let splits = [
                createTestSplit "Cat1" -60.00m
                createTestSplit "Cat2" -40.00m
            ]
            let syncTx = {
                Transaction = bankTx
                Status = Skipped
                CategoryId = None
                CategoryName = None
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF-tx-skipped")
                YnabImportStatus = NotAttempted
                Splits = Some splits
            }

            let isReadyForImport =
                syncTx.Status <> Skipped &&
                (syncTx.CategoryId.IsSome ||
                 (syncTx.Splits |> Option.map (fun s -> s.Length >= 2) |> Option.defaultValue false))

            Expect.isFalse isReadyForImport "Skipped transaction should NOT be ready"

        testCase "Transaction with category (no splits) is ready for import" <| fun () ->
            let bankTx = createTestTransaction "tx-cat" -100.00m
            let syncTx = {
                Transaction = bankTx
                Status = ManualCategorized
                CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                CategoryName = Some "Category"
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF-tx-cat")
                YnabImportStatus = NotAttempted
                Splits = None
            }

            let isReadyForImport =
                syncTx.Status <> Skipped &&
                (syncTx.CategoryId.IsSome ||
                 (syncTx.Splits |> Option.map (fun s -> s.Length >= 2) |> Option.defaultValue false))

            Expect.isTrue isReadyForImport "Transaction with category should be ready"
    ]

// ============================================
// Currency Consistency Tests
// ============================================

[<Tests>]
let splitCurrencyTests =
    testList "Split Currency Consistency" [
        testCase "All splits should have same currency as transaction" <| fun () ->
            let bankTx = createTestTransaction "tx-eur" -100.00m
            let splits = [
                { createTestSplit "Cat1" -60.00m with Amount = { Amount = -60.00m; Currency = "EUR" } }
                { createTestSplit "Cat2" -40.00m with Amount = { Amount = -40.00m; Currency = "EUR" } }
            ]

            let allSameCurrency =
                splits |> List.forall (fun s -> s.Amount.Currency = bankTx.Amount.Currency)

            Expect.isTrue allSameCurrency "All splits should have same currency"

        testCase "Detects currency mismatch in splits" <| fun () ->
            let bankTx = createTestTransaction "tx-mixed" -100.00m  // EUR
            let splits = [
                { createTestSplit "Cat1" -60.00m with Amount = { Amount = -60.00m; Currency = "EUR" } }
                { createTestSplit "Cat2" -40.00m with Amount = { Amount = -40.00m; Currency = "USD" } }  // Different currency
            ]

            let allSameCurrency =
                splits |> List.forall (fun s -> s.Amount.Currency = bankTx.Amount.Currency)

            Expect.isFalse allSameCurrency "Should detect currency mismatch"
    ]
