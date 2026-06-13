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
        Target = ToCategory (YnabCategoryId (Guid.NewGuid()), categoryName)
        Amount = { Amount = amount; Currency = "EUR" }
        Memo = None
    }

let private createTransferSplit (accountName: string) (amount: decimal) : TransactionSplit =
    {
        Target = ToTransfer (YnabAccountId (Guid.NewGuid()), accountName)
        Amount = { Amount = amount; Currency = "EUR" }
        Memo = None
    }

let private categoryNameOf (split: TransactionSplit) : string =
    match split.Target with
    | ToCategory (_, name) -> name
    | ToTransfer (_, name) -> name

// ============================================
// Split Transaction Type Tests
// ============================================

[<Tests>]
let splitTypeTests =
    testList "Split Transaction Types" [
        testCase "TransactionSplit can be created with required fields" <| fun () ->
            let split = createTestSplit "Groceries" -50.00m
            Expect.equal (categoryNameOf split) "Groceries" "Category name should match"
            Expect.equal split.Amount.Amount -50.00m "Amount should match"
            Expect.equal split.Amount.Currency "EUR" "Currency should match"
            Expect.isNone split.Memo "Memo should be None by default"

        testCase "A split line is either a category or a transfer (XOR by construction)" <| fun () ->
            // The SplitTarget DU makes "category and transfer" and "neither" unrepresentable.
            let categoryLine = createTestSplit "Groceries" -17.00m
            let transferLine = createTransferSplit "Cash" -200.00m
            match categoryLine.Target with
            | ToCategory _ -> ()
            | ToTransfer _ -> failtest "Expected a category target"
            match transferLine.Target with
            | ToTransfer _ -> ()
            | ToCategory _ -> failtest "Expected a transfer target"

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
                SuggestedByOrderId = None
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
                SuggestedByOrderId = None
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
                SuggestedByOrderId = None
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
                SuggestedByOrderId = None
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
                SuggestedByOrderId = None
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
                SuggestedByOrderId = None
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
                SuggestedByOrderId = None
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

// ============================================
// mkSplits smart-constructor (ADR 0006 structural invariants)
// ============================================

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

[<Tests>]
let mkSplitsTests =
    testList "mkSplits" [
        testCase "rejects fewer than two lines as TooFewLines" <| fun () ->
            let total = eur -100.00m
            let lines = [ createTestSplit "Groceries" -100.00m ]
            match mkSplits total lines with
            | Error (TooFewLines n) -> Expect.equal n 1 "Should report the actual line count"
            | other -> failtest $"Expected TooFewLines, got %A{other}"

        testCase "rejects when the sign-correct sum does not equal the total (SumMismatch)" <| fun () ->
            let total = eur -100.00m
            let lines = [ createTestSplit "A" -70.00m; createTestSplit "B" -20.00m ]  // sums to -90
            match mkSplits total lines with
            | Error (SumMismatch (expected, actual)) ->
                Expect.equal expected -100.00m "Expected should be the total"
                Expect.equal actual -90.00m "Actual should be the line sum"
            | other -> failtest $"Expected SumMismatch, got %A{other}"

        testCase "rejects a currency mix as CurrencyMismatch" <| fun () ->
            let total = eur -100.00m
            let lines =
                [ createTestSplit "A" -60.00m
                  { createTestSplit "B" -40.00m with Amount = { Amount = -40.00m; Currency = "USD" } } ]
            match mkSplits total lines with
            | Error CurrencyMismatch -> ()
            | other -> failtest $"Expected CurrencyMismatch, got %A{other}"

        testCase "accepts a valid two-line split (Ok)" <| fun () ->
            let total = eur -100.00m
            let lines = [ createTestSplit "A" -60.00m; createTestSplit "B" -40.00m ]
            match mkSplits total lines with
            | Ok validated -> Expect.equal validated.Length 2 "Should return the validated lines"
            | other -> failtest $"Expected Ok, got %A{other}"

        testCase "counts a transfer line toward the sum (cashback shape)" <| fun () ->
            // €217 total: €17 groceries + €200 transfer to cash must sum to the total.
            let total = eur -217.00m
            let lines = [ createTestSplit "Groceries" -17.00m; createTransferSplit "Cash" -200.00m ]
            match mkSplits total lines with
            | Ok _ -> ()
            | other -> failtest $"Expected Ok (transfer counts toward sum), got %A{other}"
    ]

// ============================================
// splitRemainder
// ============================================

[<Tests>]
let splitRemainderTests =
    testList "splitRemainder" [
        testCase "is total minus the sum of entered lines" <| fun () ->
            let total = eur -217.00m
            let entered = [ createTestSplit "Groceries" -17.00m ]
            Expect.equal (splitRemainder total entered) -200.00m "Remainder should be -200 (the cash withdrawal)"

        testCase "is the full total when no lines are entered" <| fun () ->
            let total = eur -217.00m
            Expect.equal (splitRemainder total []) -217.00m "Remainder should equal the total"

        testCase "is negative when over-allocated" <| fun () ->
            // Entered lines exceed the total → negative remainder signals over-allocation.
            let total = eur -100.00m
            let entered = [ createTestSplit "A" -60.00m; createTestSplit "B" -60.00m ]  // -120 entered
            Expect.equal (splitRemainder total entered) 20.00m "Over-allocation yields a remainder of opposite sign"
    ]

// ============================================
// buildCashbackSplit
// ============================================

[<Tests>]
let buildCashbackSplitTests =
    testList "buildCashbackSplit" [
        testCase "produces a valid two-line split (category + transfer remainder) that passes mkSplits" <| fun () ->
            let total = eur -217.00m
            let categoryId = YnabCategoryId (Guid.NewGuid())
            let cashAccountId = YnabAccountId (Guid.NewGuid())
            match buildCashbackSplit total categoryId "Groceries" (eur -17.00m) cashAccountId "Cash" None with
            | Ok lines ->
                Expect.equal lines.Length 2 "Should be two lines"
                // First line is the category, second is the transfer soaking up the rest.
                match lines.[0].Target, lines.[1].Target with
                | ToCategory (_, "Groceries"), ToTransfer (acc, "Cash") ->
                    Expect.equal acc cashAccountId "Transfer should target the cash account"
                | other -> failtest $"Unexpected targets: %A{other}"
                Expect.equal lines.[0].Amount.Amount -17.00m "Category line keeps its amount"
                Expect.equal lines.[1].Amount.Amount -200.00m "Transfer line soaks up the remainder"
                // And the result is itself a valid split.
                Expect.isOk (mkSplits total lines) "buildCashbackSplit output must pass mkSplits"
            | other -> failtest $"Expected Ok, got %A{other}"

        testCase "carries the transfer memo onto the transfer line" <| fun () ->
            let total = eur -217.00m
            match buildCashbackSplit total (YnabCategoryId (Guid.NewGuid())) "Groceries" (eur -17.00m) (YnabAccountId (Guid.NewGuid())) "Cash" (Some "Barabhebung") with
            | Ok lines -> Expect.equal lines.[1].Memo (Some "Barabhebung") "Transfer memo should be carried"
            | other -> failtest $"Expected Ok, got %A{other}"
    ]
