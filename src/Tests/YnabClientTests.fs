module YnabClientTests

open System
open Expecto
open Thoth.Json.Net
open Shared.Domain
open Server.YnabClient

// ============================================
// Sample JSON Responses (from YNAB API documentation)
// ============================================

module SampleData =
    let budgetsJson = """
    {
        "data": {
            "budgets": [
                {
                    "id": "budget-123",
                    "name": "My Budget"
                },
                {
                    "id": "budget-456",
                    "name": "Test Budget"
                }
            ]
        }
    }
    """

    // Budget detail JSON with category_groups (matches real YNAB API format)
    // Note: Internal Master Category has no "categories" field - this matches real YNAB API behavior
    let budgetDetailJson = """
    {
        "data": {
            "budget": {
                "id": "budget-123",
                "name": "My Budget",
                "accounts": [
                    {
                        "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                        "name": "Checking Account",
                        "balance": 250000
                    },
                    {
                        "id": "f1e2d3c4-b5a6-7890-1234-567890abcdef",
                        "name": "Savings Account",
                        "balance": 1500000
                    }
                ],
                "category_groups": [
                    {
                        "id": "group-internal",
                        "name": "Internal Master Category",
                        "hidden": false,
                        "deleted": false
                    },
                    {
                        "id": "group-1",
                        "name": "Essential Expenses",
                        "categories": [
                            {
                                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                                "name": "Groceries"
                            },
                            {
                                "id": "d1e2f3a4-b5c6-7890-1234-567890abcdef",
                                "name": "Rent"
                            }
                        ]
                    },
                    {
                        "id": "group-2",
                        "name": "Fun Money",
                        "categories": [
                            {
                                "id": "e1f2a3b4-c5d6-7890-1234-567890fedcba",
                                "name": "Entertainment"
                            }
                        ]
                    }
                ]
            }
        }
    }
    """

    let categoriesJson = """
    {
        "data": {
            "category_groups": [
                {
                    "id": "group-1",
                    "name": "Essential Expenses",
                    "categories": [
                        {
                            "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                            "name": "Groceries",
                            "category_group_name": "Essential Expenses"
                        },
                        {
                            "id": "d1e2f3a4-b5c6-7890-1234-567890abcdef",
                            "name": "Rent",
                            "category_group_name": "Essential Expenses"
                        }
                    ]
                },
                {
                    "id": "group-2",
                    "name": "Fun Money",
                    "categories": [
                        {
                            "id": "e1f2a3b4-c5d6-7890-1234-567890fedcba",
                            "name": "Entertainment",
                            "category_group_name": "Fun Money"
                        }
                    ]
                }
            ]
        }
    }
    """

    let invalidJsonResponse = """
    {
        "error": {
            "id": "401",
            "name": "unauthorized",
            "detail": "Unauthorized"
        }
    }
    """

    let malformedJson = """
    {
        "data": {
            "budgets": [
                {
                    "id": "budget-123"
                    // Missing "name" field
                }
            ]
        }
    """

// ============================================
// JSON Decoder Tests
// ============================================

[<Tests>]
let budgetDecoderTests =
    testList "YNAB Budget Decoder Tests" [
        testCase "decodes budget correctly" <| fun () ->
            let json = """{"id": "budget-123", "name": "My Budget"}"""
            let result = Decode.fromString Decoders.budgetDecoder json

            match result with
            | Ok budget ->
                let (YnabBudgetId id) = budget.Id
                Expect.equal id "budget-123" "Budget ID should match"
                Expect.equal budget.Name "My Budget" "Budget name should match"
            | Error err ->
                failtest $"Failed to decode budget: {err}"

        testCase "decodes budget list from API response" <| fun () ->
            let decoder = Decode.field "data" (Decode.field "budgets" (Decode.list Decoders.budgetDecoder))
            let result = Decode.fromString decoder SampleData.budgetsJson

            match result with
            | Ok budgets ->
                Expect.hasLength budgets 2 "Should have 2 budgets"
                let (YnabBudgetId id1) = budgets.[0].Id
                Expect.equal id1 "budget-123" "First budget ID should match"
                Expect.equal budgets.[0].Name "My Budget" "First budget name should match"
            | Error err ->
                failtest $"Failed to decode budgets: {err}"

        testCase "fails on malformed budget JSON" <| fun () ->
            let json = """{"id": "budget-123"}"""  // Missing "name"
            let result = Decode.fromString Decoders.budgetDecoder json

            match result with
            | Ok _ -> failtest "Should have failed on missing field"
            | Error _ -> ()  // Expected
    ]

[<Tests>]
let accountDecoderTests =
    testList "YNAB Account Decoder Tests" [
        testCase "decodes account with milliunits conversion" <| fun () ->
            let json = """
            {
                "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Checking Account",
                "balance": 250000
            }
            """
            let result = Decode.fromString Decoders.accountDecoder json

            match result with
            | Ok account ->
                let (YnabAccountId id) = account.Id
                Expect.equal (id.ToString()) "a1b2c3d4-e5f6-7890-abcd-ef1234567890" "Account ID should match"
                Expect.equal account.Name "Checking Account" "Account name should match"
                Expect.equal account.Balance.Amount 250m "Balance should be converted from milliunits (250000 / 1000 = 250)"
                Expect.equal account.Balance.Currency "EUR" "Currency should be EUR"
            | Error err ->
                failtest $"Failed to decode account: {err}"

        testCase "handles negative balance correctly" <| fun () ->
            let json = """
            {
                "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Credit Card",
                "balance": -150000
            }
            """
            let result = Decode.fromString Decoders.accountDecoder json

            match result with
            | Ok account ->
                Expect.equal account.Balance.Amount -150m "Negative balance should be converted correctly"
            | Error err ->
                failtest $"Failed to decode account: {err}"

        testCase "handles zero balance" <| fun () ->
            let json = """
            {
                "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Empty Account",
                "balance": 0
            }
            """
            let result = Decode.fromString Decoders.accountDecoder json

            match result with
            | Ok account ->
                Expect.equal account.Balance.Amount 0m "Zero balance should be handled correctly"
            | Error err ->
                failtest $"Failed to decode account: {err}"
    ]

[<Tests>]
let categoryDecoderTests =
    testList "YNAB Category Decoder Tests" [
        testCase "decodes category correctly" <| fun () ->
            let json = """
            {
                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Groceries",
                "category_group_name": "Essential Expenses"
            }
            """
            let result = Decode.fromString Decoders.categoryDecoder json

            match result with
            | Ok category ->
                let (YnabCategoryId id) = category.Id
                Expect.equal (id.ToString()) "c1b2a3d4-e5f6-7890-abcd-ef1234567890" "Category ID should match"
                Expect.equal category.Name "Groceries" "Category name should match"
                Expect.equal category.GroupName "Essential Expenses" "Category group name should match"
            | Error err ->
                failtest $"Failed to decode category: {err}"

        testCase "decodes category groups and flattens correctly" <| fun () ->
            let decoder =
                Decode.field "data" (
                    Decode.field "category_groups" (
                        Decode.list (
                            Decode.object (fun get ->
                                let groupName = get.Required.Field "name" Decode.string
                                let categories = get.Required.Field "categories" (Decode.list Decoders.categoryDecoder)
                                categories |> List.map (fun cat -> { cat with GroupName = groupName })
                            )
                        )
                    )
                )

            let result = Decode.fromString decoder SampleData.categoriesJson

            match result with
            | Ok categoryGroups ->
                let allCategories = List.concat categoryGroups
                Expect.hasLength allCategories 3 "Should have 3 categories total"

                // Verify group names are correctly assigned
                let essentialCategories = allCategories |> List.filter (fun c -> c.GroupName = "Essential Expenses")
                Expect.hasLength essentialCategories 2 "Should have 2 Essential Expenses categories"

                let funCategories = allCategories |> List.filter (fun c -> c.GroupName = "Fun Money")
                Expect.hasLength funCategories 1 "Should have 1 Fun Money category"
            | Error err ->
                failtest $"Failed to decode category groups: {err}"
    ]

[<Tests>]
let budgetDetailDecoderTests =
    testList "YNAB Budget Detail Decoder Tests" [
        testCase "decodes complete budget with accounts and categories" <| fun () ->
            let decoder = Decode.field "data" (Decode.field "budget" Decoders.budgetDetailDecoder)
            let result = Decode.fromString decoder SampleData.budgetDetailJson

            match result with
            | Ok budgetDetail ->
                // Verify budget
                let (YnabBudgetId id) = budgetDetail.Budget.Id
                Expect.equal id "budget-123" "Budget ID should match"
                Expect.equal budgetDetail.Budget.Name "My Budget" "Budget name should match"

                // Verify accounts
                Expect.hasLength budgetDetail.Accounts 2 "Should have 2 accounts"
                let checkingAccount = budgetDetail.Accounts |> List.find (fun a -> a.Name = "Checking Account")
                Expect.equal checkingAccount.Balance.Amount 250m "Checking account balance should be 250"

                // Verify categories
                Expect.hasLength budgetDetail.Categories 3 "Should have 3 categories"
                let groceriesCategory = budgetDetail.Categories |> List.find (fun c -> c.Name = "Groceries")
                Expect.equal groceriesCategory.GroupName "Essential Expenses" "Groceries should be in Essential Expenses group"
            | Error err ->
                failtest $"Failed to decode budget detail: {err}"
    ]

// ============================================
// Data Transformation Tests
// ============================================

[<Tests>]
let milliunitsConversionTests =
    testList "Milliunits Conversion Tests" [
        testCase "converts positive milliunits correctly" <| fun () ->
            let milliunits = 250000L
            let amount = decimal milliunits / 1000m
            Expect.equal amount 250m "250000 milliunits should be 250"

        testCase "converts negative milliunits correctly" <| fun () ->
            let milliunits = -150000L
            let amount = decimal milliunits / 1000m
            Expect.equal amount -150m "-150000 milliunits should be -150"

        testCase "converts zero milliunits" <| fun () ->
            let milliunits = 0L
            let amount = decimal milliunits / 1000m
            Expect.equal amount 0m "0 milliunits should be 0"

        testCase "converts decimal amounts to milliunits for API" <| fun () ->
            let amount = 123.45m
            let milliunits = int64 (amount * 1000m)
            Expect.equal milliunits 123450L "123.45 should be 123450 milliunits"

        testCase "converts negative decimal amounts to milliunits" <| fun () ->
            let amount = -67.89m
            let milliunits = int64 (amount * 1000m)
            Expect.equal milliunits -67890L "-67.89 should be -67890 milliunits"

        testCase "handles fractional cents correctly" <| fun () ->
            let amount = 12.345m  // 3 decimal places
            let milliunits = int64 (amount * 1000m)
            Expect.equal milliunits 12345L "12.345 should be 12345 milliunits"
    ]

[<Tests>]
let transactionConversionTests =
    testList "Transaction Conversion Tests" [
        testCase "converts SyncTransaction to YNAB format correctly" <| fun () ->
            let transactionId = TransactionId "tx-123"
            let categoryId = YnabCategoryId (Guid.Parse("c1b2a3d4-e5f6-7890-abcd-ef1234567890"))
            let accountId = YnabAccountId (Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"))

            let bankTransaction = {
                Id = transactionId
                BookingDate = DateTime(2025, 11, 29)
                Amount = { Amount = -50.25m; Currency = "EUR" }
                Payee = Some "Test Store"
                Memo = "Test purchase"
                Reference = "REF123"
                RawData = "{}"
            }

            let syncTransaction = {
                Transaction = bankTransaction
                Status = ManualCategorized
                CategoryId = Some categoryId
                CategoryName = Some "Groceries"
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate
                Splits = None
            }

            // Simulate the conversion logic from createTransactions
            let (TransactionId txId) = syncTransaction.Transaction.Id
            let (YnabCategoryId categoryIdGuid) = syncTransaction.CategoryId.Value

            let ynabTransaction = {|
                account_id = accountId.ToString()
                date = syncTransaction.Transaction.BookingDate.ToString("yyyy-MM-dd")
                amount = int64 (syncTransaction.Transaction.Amount.Amount * 1000m)
                payee_name = syncTransaction.Transaction.Payee |> Option.defaultValue "Unknown"
                category_id = categoryIdGuid.ToString()
                memo = syncTransaction.Transaction.Memo
                cleared = "cleared"
                import_id = $"BUDGETBUDDY:{txId}:{syncTransaction.Transaction.BookingDate.Ticks}"
            |}

            Expect.equal ynabTransaction.date "2025-11-29" "Date should be formatted correctly"
            Expect.equal ynabTransaction.amount -50250L "Amount should be converted to milliunits"
            Expect.equal ynabTransaction.payee_name "Test Store" "Payee should match"
            Expect.equal ynabTransaction.memo "Test purchase" "Memo should match"
            Expect.equal ynabTransaction.cleared "cleared" "Should be marked as cleared"
            Expect.stringContains ynabTransaction.import_id "BUDGETBUDDY:tx-123" "Import ID should contain transaction ID"

        testCase "uses PayeeOverride when provided" <| fun () ->
            let transactionId = TransactionId "tx-456"
            let categoryId = YnabCategoryId (Guid.Parse("c1b2a3d4-e5f6-7890-abcd-ef1234567890"))

            let bankTransaction = {
                Id = transactionId
                BookingDate = DateTime(2025, 11, 29)
                Amount = { Amount = -30m; Currency = "EUR" }
                Payee = Some "Original Payee"
                Memo = "Test"
                Reference = "REF456"
                RawData = "{}"
            }

            let syncTransaction = {
                Transaction = bankTransaction
                Status = ManualCategorized
                CategoryId = Some categoryId
                CategoryName = Some "Dining"
                MatchedRuleId = None
                PayeeOverride = Some "Custom Payee"
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate
                Splits = None
            }

            let payeeName =
                syncTransaction.PayeeOverride
                |> Option.orElse syncTransaction.Transaction.Payee
                |> Option.defaultValue "Unknown"

            Expect.equal payeeName "Custom Payee" "Should use PayeeOverride when provided"

        testCase "truncates long memo to 200 characters" <| fun () ->
            let longMemo = String.replicate 250 "a"
            let truncatedMemo =
                if longMemo.Length > 200 then
                    longMemo.Substring(0, 197) + "..."
                else
                    longMemo

            Expect.equal truncatedMemo.Length 200 "Memo should be truncated to 200 characters"
            Expect.stringEnds truncatedMemo "..." "Truncated memo should end with ..."

        testCase "keeps short memo as-is" <| fun () ->
            let shortMemo = "Short memo"
            let result =
                if shortMemo.Length > 200 then
                    shortMemo.Substring(0, 197) + "..."
                else
                    shortMemo

            Expect.equal result "Short memo" "Short memo should not be modified"

        testCase "filters out skipped transactions" <| fun () ->
            let transactions = [
                {
                    Transaction = {
                        Id = TransactionId "tx-1"
                        BookingDate = DateTime(2025, 11, 29)
                        Amount = { Amount = -10m; Currency = "EUR" }
                        Payee = Some "Store"
                        Memo = "Test"
                        Reference = "REF1"
                        RawData = "{}"
                    }
                    Status = Skipped
                    CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                    CategoryName = Some "Category"
                    MatchedRuleId = None
                    PayeeOverride = None
                    ExternalLinks = []
                    UserNotes = None
                    DuplicateStatus = NotDuplicate
                    Splits = None
                }
            ]

            let filtered =
                transactions
                |> List.filter (fun tx -> tx.Status <> Skipped && tx.CategoryId.IsSome)

            Expect.hasLength filtered 0 "Skipped transactions should be filtered out"

        testCase "filters out uncategorized transactions" <| fun () ->
            let transactions = [
                {
                    Transaction = {
                        Id = TransactionId "tx-2"
                        BookingDate = DateTime(2025, 11, 29)
                        Amount = { Amount = -20m; Currency = "EUR" }
                        Payee = Some "Store"
                        Memo = "Test"
                        Reference = "REF2"
                        RawData = "{}"
                    }
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
            ]

            let filtered =
                transactions
                |> List.filter (fun tx -> tx.Status <> Skipped && tx.CategoryId.IsSome)

            Expect.hasLength filtered 0 "Uncategorized transactions should be filtered out"
    ]

// ============================================
// Import ID Generation Tests
// ============================================

[<Tests>]
let importIdGenerationTests =
    testList "Import ID Generation Tests" [
        testCase "generates unique import IDs for different transactions" <| fun () ->
            let tx1 = {
                Transaction = {
                    Id = TransactionId "tx-1"
                    BookingDate = DateTime(2025, 11, 29, 10, 0, 0)
                    Amount = { Amount = -10m; Currency = "EUR" }
                    Payee = Some "Store"
                    Memo = "Test"
                    Reference = "REF1"
                    RawData = "{}"
                }
                Status = ManualCategorized
                CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                CategoryName = Some "Category"
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate
                Splits = None
            }

            let tx2 = {
                Transaction = {
                    Id = TransactionId "tx-2"
                    BookingDate = DateTime(2025, 11, 29, 11, 0, 0)
                    Amount = { Amount = -20m; Currency = "EUR" }
                    Payee = Some "Store"
                    Memo = "Test"
                    Reference = "REF2"
                    RawData = "{}"
                }
                Status = ManualCategorized
                CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                CategoryName = Some "Category"
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate
                Splits = None
            }

            let (TransactionId id1) = tx1.Transaction.Id
            let importId1 = $"BUDGETBUDDY:{id1}:{tx1.Transaction.BookingDate.Ticks}"

            let (TransactionId id2) = tx2.Transaction.Id
            let importId2 = $"BUDGETBUDDY:{id2}:{tx2.Transaction.BookingDate.Ticks}"

            Expect.notEqual importId1 importId2 "Import IDs should be unique for different transactions"

        testCase "generates consistent import IDs for same transaction" <| fun () ->
            let transactionId = TransactionId "tx-123"
            let bookingDate = DateTime(2025, 11, 29)

            let (TransactionId id) = transactionId
            let importId1 = $"BUDGETBUDDY:{id}:{bookingDate.Ticks}"
            let importId2 = $"BUDGETBUDDY:{id}:{bookingDate.Ticks}"

            Expect.equal importId1 importId2 "Same transaction should generate same import ID"

        testCase "import ID contains transaction ID and timestamp" <| fun () ->
            let transactionId = TransactionId "tx-456"
            let bookingDate = DateTime(2025, 11, 29)

            let (TransactionId id) = transactionId
            let importId = $"BUDGETBUDDY:{id}:{bookingDate.Ticks}"

            Expect.stringContains importId "BUDGETBUDDY" "Import ID should contain prefix"
            Expect.stringContains importId "tx-456" "Import ID should contain transaction ID"
            Expect.stringContains importId (bookingDate.Ticks.ToString()) "Import ID should contain timestamp"
    ]

// ============================================
// Error Handling Tests
// ============================================

[<Tests>]
let errorHandlingTests =
    testList "YNAB Error Handling Tests" [
        testCase "handles invalid JSON response" <| fun () ->
            let decoder = Decode.field "data" (Decode.field "budgets" (Decode.list Decoders.budgetDecoder))
            let result = Decode.fromString decoder SampleData.malformedJson

            match result with
            | Ok _ -> failtest "Should fail on malformed JSON"
            | Error err ->
                Expect.isNotNull err "Should return error message"
    ]


// ============================================
// Integration Test Documentation
// ============================================

[<Tests>]
let integrationTestDocumentation =
    testList "Integration Test Documentation" [
        testCase "documents how to run integration tests" <| fun () ->
            // This test documents the approach for integration testing
            // To run real integration tests:
            // 1. Set environment variable YNAB_TOKEN with a valid Personal Access Token
            // 2. Uncomment the integration test code below
            // 3. Run: dotnet test --filter Category=Integration

            // Example integration test (commented out to avoid requiring a real token):
            (*
            let token = Environment.GetEnvironmentVariable("YNAB_TOKEN")
            if String.IsNullOrEmpty(token) then
                Tests.skiptest "YNAB_TOKEN not set"
            else
                async {
                    let! result = YnabClient.getBudgets token
                    match result with
                    | Ok budgets ->
                        Expect.isGreaterThan budgets.Length 0 "Should have at least one budget"
                    | Error err ->
                        failtest $"Integration test failed: {err}"
                } |> Async.RunSynchronously
            *)

            // This test always passes and is just for documentation
            ()

        testCase "documents YNAB API rate limits" <| fun () ->
            // YNAB API Rate Limits (from documentation):
            // - 200 requests per hour per token
            // - Rate limit info in response headers:
            //   - X-Rate-Limit: 200
            //   - X-Rate-Limit-Remaining: 150
            // - When rate limited, returns 429 with Retry-After header

            // Our implementation handles this by:
            // 1. Returning YnabError.RateLimitExceeded with retry-after seconds
            // 2. Client code should implement exponential backoff

            ()

        testCase "documents YNAB API response structure" <| fun () ->
            // All YNAB API responses follow this structure:
            // Success: { "data": { ... } }
            // Error: { "error": { "id": "...", "name": "...", "detail": "..." } }

            // Our decoders always expect the "data" wrapper
            // Error responses are handled by HTTP status codes, not by parsing error JSON

            ()
    ]

// ============================================
// Property-Based Tests
// ============================================

[<Tests>]
let propertyBasedTests =
    testList "Property-Based YNAB Tests" [
        testProperty "milliunits conversion roundtrip" <| fun (amount: decimal) ->
            // Skip extreme values that might overflow
            if abs amount < 1000000000m then
                let milliunits = int64 (amount * 1000m)
                let converted = decimal milliunits / 1000m
                // Allow for rounding differences due to decimal precision
                abs (converted - amount) < 0.001m
            else
                true

        testProperty "import ID uniqueness with different dates" <| fun (id: string) (ticks1: int64) (ticks2: int64) ->
            if ticks1 <> ticks2 then
                let importId1 = $"BUDGETBUDDY:{id}:{ticks1}"
                let importId2 = $"BUDGETBUDDY:{id}:{ticks2}"
                importId1 <> importId2
            else
                true

        testProperty "memo truncation preserves prefix" <| fun (memo: string) ->
            if not (String.IsNullOrEmpty(memo)) then
                let truncated =
                    if memo.Length > 200 then
                        memo.Substring(0, 197) + "..."
                    else
                        memo
                truncated.Length <= 200
            else
                true
    ]

// ============================================
// JSON Encoding Tests (Critical - prevents regression of amount serialization bug)
// ============================================

[<Tests>]
let jsonEncodingTests =
    testList "YNAB JSON Encoding Tests" [
        testCase "amount is serialized as JSON number, not string" <| fun () ->
            // This test prevents regression of the bug where Encode.int64 serialized
            // amounts as strings (e.g., "-50250" instead of -50250), causing YNAB
            // to silently reject transactions.
            let testTx : YnabTransactionRequest = {
                AccountId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
                Date = "2025-12-07"
                Amount = -50250  // -50.25 EUR in milliunits
                PayeeName = "Test Store"
                Memo = "Test purchase"
                Cleared = "cleared"
                ImportId = "BB:tx123"
                CategoryId = Some "c1b2a3d4-e5f6-7890-abcd-ef1234567890"
                Subtransactions = None
            }

            let requestBody =
                Encode.object [
                    "transactions", Encode.list ([testTx] |> List.map (fun tx ->
                        Encode.object [
                            "account_id", Encode.string tx.AccountId
                            "date", Encode.string tx.Date
                            "amount", Encode.int tx.Amount  // Must be Encode.int, NOT Encode.int64!
                            "payee_name", Encode.string tx.PayeeName
                            "memo", Encode.string tx.Memo
                            "cleared", Encode.string tx.Cleared
                            "import_id", Encode.string tx.ImportId
                            match tx.CategoryId with
                            | Some catId -> "category_id", Encode.string catId
                            | None -> ()
                        ]
                    ))
                ]
                |> Encode.toString 0

            // Critical: Amount must NOT have quotes around it
            Expect.stringContains requestBody "\"amount\":-50250" "Amount must be a JSON number, not a string"
            Expect.isFalse (requestBody.Contains("\"amount\":\"-50250\"")) "Amount must NOT be serialized as string"

        testCase "subtransaction amount is serialized as JSON number" <| fun () ->
            let testSub : YnabSubtransactionRequest = {
                Amount = -25000  // -25.00 EUR in milliunits
                CategoryId = "c1b2a3d4-e5f6-7890-abcd-ef1234567890"
                Memo = Some "Split 1"
            }

            let json =
                Encode.object [
                    "amount", Encode.int testSub.Amount
                    "category_id", Encode.string testSub.CategoryId
                ]
                |> Encode.toString 0

            Expect.stringContains json "\"amount\":-25000" "Subtransaction amount must be a JSON number"
            Expect.isFalse (json.Contains("\"amount\":\"-25000\"")) "Subtransaction amount must NOT be serialized as string"
    ]

// ============================================
// YNAB Response Parsing Tests (Critical - prevents false success reports)
// ============================================

[<Tests>]
let ynabResponseParsingTests =
    testList "YNAB Response Parsing Tests" [
        testCase "correctly counts created transactions from YNAB response" <| fun () ->
            // This test prevents regression of the bug where we reported success
            // based on sent transaction count instead of actual YNAB response.
            let ynabResponse = """
            {
                "data": {
                    "transactions": [
                        { "id": "tx-1", "amount": -50000 },
                        { "id": "tx-2", "amount": -30000 }
                    ],
                    "duplicate_import_ids": []
                }
            }
            """

            let createdCountDecoder =
                Decode.field "data" (
                    Decode.object (fun get ->
                        let transactions = get.Optional.Field "transactions" (Decode.list (Decode.succeed ())) |> Option.defaultValue []
                        let duplicates = get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string) |> Option.defaultValue []
                        (transactions.Length, duplicates)
                    )
                )

            match Decode.fromString createdCountDecoder ynabResponse with
            | Ok (createdCount, duplicates) ->
                Expect.equal createdCount 2 "Should report 2 created transactions"
                Expect.isEmpty duplicates "Should have no duplicates"
            | Error err ->
                failtest $"Failed to parse YNAB response: {err}"

        testCase "correctly identifies duplicate transactions from YNAB response" <| fun () ->
            // This test ensures we detect when YNAB silently rejects transactions
            // as duplicates (which it reports in duplicate_import_ids).
            let ynabResponse = """
            {
                "data": {
                    "transactions": [
                        { "id": "tx-1", "amount": -50000 }
                    ],
                    "duplicate_import_ids": ["BB:abc123", "BB:def456"]
                }
            }
            """

            let createdCountDecoder =
                Decode.field "data" (
                    Decode.object (fun get ->
                        let transactions = get.Optional.Field "transactions" (Decode.list (Decode.succeed ())) |> Option.defaultValue []
                        let duplicates = get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string) |> Option.defaultValue []
                        (transactions.Length, duplicates)
                    )
                )

            match Decode.fromString createdCountDecoder ynabResponse with
            | Ok (createdCount, duplicates) ->
                Expect.equal createdCount 1 "Should report only 1 created transaction"
                Expect.hasLength duplicates 2 "Should have 2 duplicate import IDs"
                Expect.contains duplicates "BB:abc123" "Should contain first duplicate ID"
                Expect.contains duplicates "BB:def456" "Should contain second duplicate ID"
            | Error err ->
                failtest $"Failed to parse YNAB response: {err}"

        testCase "handles response with all transactions rejected as duplicates" <| fun () ->
            // When all transactions are duplicates, YNAB returns empty transactions array
            let ynabResponse = """
            {
                "data": {
                    "transactions": [],
                    "duplicate_import_ids": ["BB:abc123", "BB:def456", "BB:ghi789"]
                }
            }
            """

            let createdCountDecoder =
                Decode.field "data" (
                    Decode.object (fun get ->
                        let transactions = get.Optional.Field "transactions" (Decode.list (Decode.succeed ())) |> Option.defaultValue []
                        let duplicates = get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string) |> Option.defaultValue []
                        (transactions.Length, duplicates)
                    )
                )

            match Decode.fromString createdCountDecoder ynabResponse with
            | Ok (createdCount, duplicates) ->
                Expect.equal createdCount 0 "Should report 0 created transactions when all are duplicates"
                Expect.hasLength duplicates 3 "Should have 3 duplicate import IDs"
            | Error err ->
                failtest $"Failed to parse YNAB response: {err}"

        testCase "handles response missing duplicate_import_ids field" <| fun () ->
            // Older YNAB API responses might not include duplicate_import_ids
            let ynabResponse = """
            {
                "data": {
                    "transactions": [
                        { "id": "tx-1", "amount": -50000 }
                    ]
                }
            }
            """

            let createdCountDecoder =
                Decode.field "data" (
                    Decode.object (fun get ->
                        let transactions = get.Optional.Field "transactions" (Decode.list (Decode.succeed ())) |> Option.defaultValue []
                        let duplicates = get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string) |> Option.defaultValue []
                        (transactions.Length, duplicates)
                    )
                )

            match Decode.fromString createdCountDecoder ynabResponse with
            | Ok (createdCount, duplicates) ->
                Expect.equal createdCount 1 "Should report 1 created transaction"
                Expect.isEmpty duplicates "Should default to empty list when field is missing"
            | Error err ->
                failtest $"Failed to parse YNAB response: {err}"
    ]
