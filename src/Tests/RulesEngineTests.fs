module Tests.RulesEngineTests

open System
open Expecto
open Shared.Domain
open Server.RulesEngine

// ============================================
// Test Data Helpers
// ============================================

let createRule
    (id: Guid)
    (name: string)
    (pattern: string)
    (patternType: PatternType)
    (targetField: TargetField)
    (categoryId: Guid)
    (priority: int)
    (enabled: bool)
    : Rule =
    {
        Id = RuleId id
        Name = name
        Pattern = pattern
        PatternType = patternType
        TargetField = targetField
        CategoryId = YnabCategoryId categoryId
        CategoryName = "Test Category"
        PayeeOverride = None
        Priority = priority
        Enabled = enabled
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

let createTransaction
    (id: string)
    (payee: string option)
    (memo: string)
    (amount: decimal)
    : BankTransaction =
    {
        Id = TransactionId id
        BookingDate = DateTime.UtcNow
        Amount = { Amount = amount; Currency = "EUR" }
        Payee = payee
        Memo = memo
        Reference = "REF123"
        RawData = "{}"
    }

// ============================================
// Pattern Compilation Tests
// ============================================

[<Tests>]
let patternCompilationTests =
    testList "Pattern Compilation Tests" [
        testCase "Exact pattern compiles with anchors and escaping" <| fun () ->
            let rule = createRule (Guid.NewGuid()) "Exact Rule" "Test.Pattern" Exact Payee (Guid.NewGuid()) 1 true
            let result = compileRule rule

            match result with
            | Ok compiled ->
                // Test that exact pattern matches only exact string
                Expect.isTrue (compiled.Regex.IsMatch("Test.Pattern")) "Should match exact string"
                Expect.isFalse (compiled.Regex.IsMatch("Test Pattern")) "Should not match different string"
                Expect.isFalse (compiled.Regex.IsMatch("Test.Pattern Extra")) "Should not match with extra text"
            | Error e ->
                failtest $"Should compile successfully: {e}"

        testCase "Contains pattern compiles with escaping" <| fun () ->
            let rule = createRule (Guid.NewGuid()) "Contains Rule" "REWE" Contains Memo (Guid.NewGuid()) 1 true
            let result = compileRule rule

            match result with
            | Ok compiled ->
                Expect.isTrue (compiled.Regex.IsMatch("REWE Supermarkt")) "Should match when pattern is contained"
                Expect.isTrue (compiled.Regex.IsMatch("Shopping at REWE")) "Should match in middle"
                Expect.isFalse (compiled.Regex.IsMatch("EDEKA")) "Should not match different text"
            | Error e ->
                failtest $"Should compile successfully: {e}"

        testCase "Regex pattern compiles as-is" <| fun () ->
            let rule = createRule (Guid.NewGuid()) "Regex Rule" @"REWE\s+\d+" Regex Memo (Guid.NewGuid()) 1 true
            let result = compileRule rule

            match result with
            | Ok compiled ->
                Expect.isTrue (compiled.Regex.IsMatch("REWE 123")) "Should match valid regex pattern"
                Expect.isTrue (compiled.Regex.IsMatch("REWE   456")) "Should match multiple spaces"
                Expect.isFalse (compiled.Regex.IsMatch("REWE")) "Should not match without digits"
            | Error e ->
                failtest $"Should compile successfully: {e}"

        testCase "Invalid regex pattern returns error" <| fun () ->
            let rule = createRule (Guid.NewGuid()) "Invalid Rule" @"[invalid(regex" Regex Memo (Guid.NewGuid()) 1 true
            let result = compileRule rule

            match result with
            | Ok _ -> failtest "Should fail to compile invalid regex"
            | Error e ->
                Expect.stringContains e "Failed to compile pattern" "Error should mention compilation failure"

        testCase "Pattern matching is case-insensitive" <| fun () ->
            let rule = createRule (Guid.NewGuid()) "Case Rule" "amazon" Contains Memo (Guid.NewGuid()) 1 true
            let result = compileRule rule

            match result with
            | Ok compiled ->
                Expect.isTrue (compiled.Regex.IsMatch("AMAZON")) "Should match uppercase"
                Expect.isTrue (compiled.Regex.IsMatch("Amazon")) "Should match mixed case"
                Expect.isTrue (compiled.Regex.IsMatch("amazon")) "Should match lowercase"
            | Error e ->
                failtest $"Should compile successfully: {e}"

        testCase "compileRules collects all errors" <| fun () ->
            let rules = [
                createRule (Guid.NewGuid()) "Valid" "REWE" Contains Memo (Guid.NewGuid()) 1 true
                createRule (Guid.NewGuid()) "Invalid1" "[invalid" Regex Memo (Guid.NewGuid()) 2 true
                createRule (Guid.NewGuid()) "Invalid2" "(unclosed" Regex Memo (Guid.NewGuid()) 3 true
            ]

            let result = compileRules rules

            match result with
            | Ok _ -> failtest "Should fail when any rule fails to compile"
            | Error errors ->
                Expect.equal errors.Length 2 "Should have 2 errors"
                Expect.all errors (fun e -> e.Contains("Failed to compile pattern")) "All errors should be compilation errors"

        testCase "compileRules succeeds with all valid rules" <| fun () ->
            let rules = [
                createRule (Guid.NewGuid()) "Rule1" "REWE" Contains Memo (Guid.NewGuid()) 1 true
                createRule (Guid.NewGuid()) "Rule2" "EDEKA" Contains Memo (Guid.NewGuid()) 2 true
                createRule (Guid.NewGuid()) "Rule3" "Amazon" Exact Payee (Guid.NewGuid()) 3 true
            ]

            let result = compileRules rules

            match result with
            | Ok compiled ->
                Expect.equal compiled.Length 3 "Should compile all rules"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Should compile all valid rules: {errors}"
    ]

// ============================================
// Transaction Classification Tests
// ============================================

[<Tests>]
let classificationTests =
    testList "Transaction Classification Tests" [
        testCase "Classify matches Payee field" <| fun () ->
            let categoryId = Guid.NewGuid()
            let rule = createRule (Guid.NewGuid()) "Payee Rule" "REWE" Contains Payee categoryId 1 true
            let transaction = createTransaction "TX1" (Some "REWE Supermarkt") "Shopping" -50.0m

            let result = compileRules [rule]
            match result with
            | Ok compiled ->
                let classification = classify compiled transaction
                match classification with
                | Some (matchedRule, matchedCategoryId) ->
                    Expect.equal matchedRule.Id rule.Id "Should match the rule"
                    Expect.equal matchedCategoryId (YnabCategoryId categoryId) "Should return correct category"
                | None ->
                    failtest "Should classify the transaction"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Rules should compile: {errors}"

        testCase "Classify matches Memo field" <| fun () ->
            let categoryId = Guid.NewGuid()
            let rule = createRule (Guid.NewGuid()) "Memo Rule" "Shopping" Contains Memo categoryId 1 true
            let transaction = createTransaction "TX1" (Some "Store") "Shopping at REWE" -50.0m

            let result = compileRules [rule]
            match result with
            | Ok compiled ->
                let classification = classify compiled transaction
                match classification with
                | Some (matchedRule, _) ->
                    Expect.equal matchedRule.Id rule.Id "Should match the rule"
                | None ->
                    failtest "Should classify the transaction"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Rules should compile: {errors}"

        testCase "Classify matches Combined field (payee + memo)" <| fun () ->
            let categoryId = Guid.NewGuid()
            let rule = createRule (Guid.NewGuid()) "Combined Rule" "REWE" Contains Combined categoryId 1 true
            let transaction1 = createTransaction "TX1" (Some "REWE Supermarkt") "Shopping" -50.0m
            let transaction2 = createTransaction "TX2" (Some "Store") "Shopping at REWE" -50.0m

            let result = compileRules [rule]
            match result with
            | Ok compiled ->
                let classification1 = classify compiled transaction1
                let classification2 = classify compiled transaction2

                match classification1, classification2 with
                | Some _, Some _ ->
                    () // Both should match
                | _ ->
                    failtest "Both transactions should match with Combined field"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Rules should compile: {errors}"

        testCase "Priority ordering - first matching rule wins" <| fun () ->
            let categoryId1 = Guid.NewGuid()
            let categoryId2 = Guid.NewGuid()
            let rule1 = createRule (Guid.NewGuid()) "High Priority" "REWE" Contains Memo categoryId1 1 true
            let rule2 = createRule (Guid.NewGuid()) "Low Priority" "REWE" Contains Memo categoryId2 2 true

            let transaction = createTransaction "TX1" (Some "Store") "REWE Supermarkt" -50.0m

            let result = compileRules [rule1; rule2]
            match result with
            | Ok compiled ->
                let classification = classify compiled transaction
                match classification with
                | Some (matchedRule, matchedCategoryId) ->
                    Expect.equal matchedRule.Id rule1.Id "Should match first rule (highest priority)"
                    Expect.equal matchedCategoryId (YnabCategoryId categoryId1) "Should use first rule's category"
                | None ->
                    failtest "Should classify the transaction"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Rules should compile: {errors}"

        testCase "Disabled rules are skipped" <| fun () ->
            let categoryId = Guid.NewGuid()
            let disabledRule = createRule (Guid.NewGuid()) "Disabled" "REWE" Contains Memo categoryId 1 false
            let transaction = createTransaction "TX1" (Some "Store") "REWE Supermarkt" -50.0m

            let result = compileRules [disabledRule]
            match result with
            | Ok compiled ->
                let classification = classify compiled transaction
                match classification with
                | Some _ ->
                    failtest "Should not match disabled rule"
                | None ->
                    () // Expected - no match
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Rules should compile: {errors}"

        testCase "No rules return None" <| fun () ->
            let transaction = createTransaction "TX1" (Some "Store") "REWE Supermarkt" -50.0m
            let result = compileRules []

            match result with
            | Ok compiled ->
                let classification = classify compiled transaction
                Expect.isNone classification "Should return None with no rules"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Empty rules should compile: {errors}"

        testCase "No matching rules return None" <| fun () ->
            let categoryId = Guid.NewGuid()
            let rule = createRule (Guid.NewGuid()) "Rule" "EDEKA" Contains Memo categoryId 1 true
            let transaction = createTransaction "TX1" (Some "Store") "REWE Supermarkt" -50.0m

            let result = compileRules [rule]
            match result with
            | Ok compiled ->
                let classification = classify compiled transaction
                Expect.isNone classification "Should return None when no rules match"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Rules should compile: {errors}"
    ]

// ============================================
// Special Pattern Detection Tests
// ============================================

[<Tests>]
let specialPatternTests =
    testList "Special Pattern Detection Tests" [
        testCase "Detects Amazon in payee" <| fun () ->
            let transaction = createTransaction "TX1" (Some "AMAZON PAYMENTS EU") "Order" -29.99m
            let links = detectSpecialTransaction transaction

            Expect.isNonEmpty links "Should detect Amazon transaction"
            Expect.exists links (fun l -> l.Url.Contains("amazon.de")) "Should link to Amazon"

        testCase "Detects Amazon in memo" <| fun () ->
            let transaction = createTransaction "TX1" (Some "Credit Card") "AMAZON.DE Order 123" -29.99m
            let links = detectSpecialTransaction transaction

            Expect.isNonEmpty links "Should detect Amazon transaction"
            Expect.exists links (fun l -> l.Url.Contains("amazon.de")) "Should link to Amazon"

        testCase "Amazon order ID in memo generates deep link" <| fun () ->
            // Order ID format: ABC-1234567-1234567
            let transaction = createTransaction "TX1" (Some "AMAZON EU") "Order ABC-1234567-7654321" -29.99m
            let links = detectSpecialTransaction transaction

            Expect.isNonEmpty links "Should detect Amazon transaction"
            let amazonLink = links |> List.find (fun l -> l.Url.Contains("amazon.de"))
            Expect.stringContains amazonLink.Url "order-details" "Should link to order details"
            Expect.stringContains amazonLink.Url "orderID=ABC-1234567-7654321" "Should contain order ID in URL"
            Expect.stringContains amazonLink.Label "ABC-1234567-7654321" "Label should contain order ID"

        testCase "Amazon order ID in payee generates deep link" <| fun () ->
            let transaction = createTransaction "TX1" (Some "AMAZON 123-4567890-1234567") "Some memo" -29.99m
            let links = detectSpecialTransaction transaction

            Expect.isNonEmpty links "Should detect Amazon transaction"
            let amazonLink = links |> List.find (fun l -> l.Url.Contains("amazon.de"))
            Expect.stringContains amazonLink.Url "orderID=123-4567890-1234567" "Should contain order ID in URL"

        testCase "Amazon order ID with Comdirect line prefix generates deep link" <| fun () ->
            // Comdirect adds 2-digit line prefixes like 01, 02, etc.
            // This test verifies we extract order ID correctly from "01305-0831755-7604349"
            let transaction = createTransaction "TX1" (Some "Amazon.de") "01305-0831755-7604349 AMZN Mktp DE" -29.99m
            let links = detectSpecialTransaction transaction

            Expect.isNonEmpty links "Should detect Amazon transaction"
            let amazonLink = links |> List.find (fun l -> l.Url.Contains("amazon.de"))
            Expect.stringContains amazonLink.Url "order-details" "Should link to order details"
            Expect.stringContains amazonLink.Url "orderID=305-0831755-7604349" "Should extract order ID without prefix"
            Expect.stringContains amazonLink.Label "305-0831755-7604349" "Label should contain order ID without prefix"

        testCase "Amazon without order ID generates history link" <| fun () ->
            let transaction = createTransaction "TX1" (Some "AMAZON PAYMENTS") "Generic purchase" -29.99m
            let links = detectSpecialTransaction transaction

            Expect.isNonEmpty links "Should detect Amazon transaction"
            let amazonLink = links |> List.find (fun l -> l.Url.Contains("amazon.de"))
            Expect.stringContains amazonLink.Url "order-history" "Should link to order history"
            Expect.equal amazonLink.Label "Amazon Orders" "Label should be generic"

        testCase "Detects PayPal in payee" <| fun () ->
            let transaction = createTransaction "TX1" (Some "PAYPAL *EBAY") "Payment" -15.50m
            let links = detectSpecialTransaction transaction

            Expect.isNonEmpty links "Should detect PayPal transaction"
            Expect.exists links (fun l -> l.Label = "PayPal Activity") "Should have PayPal link"
            Expect.exists links (fun l -> l.Url.Contains("paypal.com")) "Should link to PayPal"

        testCase "Detects PayPal in memo" <| fun () ->
            let transaction = createTransaction "TX1" (Some "Credit Card") "PP.123456.789" -25.00m
            let links = detectSpecialTransaction transaction

            Expect.isNonEmpty links "Should detect PayPal transaction"
            Expect.exists links (fun l -> l.Label = "PayPal Activity") "Should have PayPal link"

        testCase "Returns empty list for regular transactions" <| fun () ->
            let transaction = createTransaction "TX1" (Some "REWE Supermarkt") "Groceries" -50.0m
            let links = detectSpecialTransaction transaction

            Expect.isEmpty links "Should not detect special patterns in regular transaction"

        testCase "Case-insensitive pattern detection" <| fun () ->
            let transaction1 = createTransaction "TX1" (Some "amazon payments") "Order" -29.99m
            let transaction2 = createTransaction "TX2" (Some "paypal *store") "Payment" -15.50m

            let links1 = detectSpecialTransaction transaction1
            let links2 = detectSpecialTransaction transaction2

            Expect.isNonEmpty links1 "Should detect lowercase Amazon"
            Expect.isNonEmpty links2 "Should detect lowercase PayPal"
    ]

// ============================================
// classifyTransactions Integration Tests
// ============================================

[<Tests>]
let classifyTransactionsTests =
    testList "classifyTransactions Integration Tests" [
        testCase "Auto-categorizes matching transactions" <| fun () ->
            let categoryId = Guid.NewGuid()
            let rule = createRule (Guid.NewGuid()) "Groceries" "REWE" Contains Payee categoryId 1 true
            let transactions = [
                createTransaction "TX1" (Some "REWE Supermarkt") "Shopping" -50.0m
                createTransaction "TX2" (Some "EDEKA") "Shopping" -30.0m
            ]

            let result = classifyTransactions DateTime.UtcNow [rule] transactions

            match result with
            | Ok syncTransactions ->
                Expect.equal syncTransactions.Length 2 "Should process all transactions"

                let tx1 = syncTransactions |> List.find (fun t -> let (TransactionId id) = t.Transaction.Id in id = "TX1")
                let tx2 = syncTransactions |> List.find (fun t -> let (TransactionId id) = t.Transaction.Id in id = "TX2")

                Expect.equal tx1.Status AutoCategorized "TX1 should be auto-categorized"
                Expect.equal tx1.CategoryId (Some (YnabCategoryId categoryId)) "TX1 should have category"

                Expect.equal tx2.Status Pending "TX2 should be pending (no match)"
                Expect.isNone tx2.CategoryId "TX2 should have no category"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Should classify transactions: {errors}"

        testCase "NeedsAttention for Amazon transactions even if categorized" <| fun () ->
            let categoryId = Guid.NewGuid()
            let rule = createRule (Guid.NewGuid()) "Amazon" "AMAZON" Contains Payee categoryId 1 true
            let transactions = [
                createTransaction "TX1" (Some "AMAZON PAYMENTS EU") "Order" -29.99m
            ]

            let result = classifyTransactions DateTime.UtcNow [rule] transactions

            match result with
            | Ok syncTransactions ->
                Expect.equal syncTransactions.Length 1 "Should process transaction"

                let tx1 = syncTransactions.[0]
                Expect.equal tx1.Status NeedsAttention "Amazon transaction should need attention"
                Expect.isSome tx1.CategoryId "Should still have category from rule"
                Expect.isNonEmpty tx1.ExternalLinks "Should have external link"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Should classify transactions: {errors}"

        testCase "NeedsAttention for PayPal transactions without category" <| fun () ->
            let transactions = [
                createTransaction "TX1" (Some "PAYPAL *STORE") "Payment" -15.50m
            ]

            let result = classifyTransactions DateTime.UtcNow [] transactions

            match result with
            | Ok syncTransactions ->
                Expect.equal syncTransactions.Length 1 "Should process transaction"

                let tx1 = syncTransactions.[0]
                Expect.equal tx1.Status NeedsAttention "PayPal transaction should need attention"
                Expect.isNone tx1.CategoryId "Should have no category (no rules)"
                Expect.isNonEmpty tx1.ExternalLinks "Should have external link"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Should classify transactions: {errors}"

        testCase "Propagates rule compilation errors" <| fun () ->
            let rule = createRule (Guid.NewGuid()) "Invalid" "[invalid" Regex Memo (Guid.NewGuid()) 1 true
            let transactions = [
                createTransaction "TX1" (Some "Store") "Shopping" -50.0m
            ]

            let result = classifyTransactions DateTime.UtcNow [rule] transactions

            match result with
            | Ok _ ->
                failtest "Should propagate compilation errors"
            | Error errors ->
                Expect.isNonEmpty errors "Should have compilation errors"

        testCase "Uses PayeeOverride from matched rule" <| fun () ->
            let categoryId = Guid.NewGuid()
            let rule = {
                createRule (Guid.NewGuid()) "REWE" "REWE" Contains Payee categoryId 1 true
                with PayeeOverride = Some "REWE Grocery Store"
            }
            let transactions = [
                createTransaction "TX1" (Some "REWE Filiale 123") "Shopping" -50.0m
            ]

            let result = classifyTransactions DateTime.UtcNow [rule] transactions

            match result with
            | Ok syncTransactions ->
                let tx1 = syncTransactions.[0]
                Expect.equal tx1.PayeeOverride (Some "REWE Grocery Store") "Should use PayeeOverride from rule"
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Should classify transactions: {errors}"
    ]

// ============================================
// Review Trail Tests (categorization-gdrx8, ADR 0021)
// ============================================

/// A minimal SyncTransaction for `appendReviewStep` tests: no category/splits/
/// PayeeOverride set yet, no ReviewSteps recorded.
let createSyncTransaction () : SyncTransaction =
    {
        Transaction = createTransaction "TX1" (Some "Test Payee") "Test Memo" -10.0m
        Status = Pending
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
let classifyTransactionsReviewStepsTests =
    testList "classifyTransactions records ReviewSteps" [
        testCase "a matched rule records one ByRule step at the given timestamp" <| fun () ->
            let ruleId = Guid.NewGuid()
            let categoryId = Guid.NewGuid()
            let rule = createRule ruleId "Groceries" "REWE" Contains Payee categoryId 1 true
            let transactions = [ createTransaction "TX1" (Some "REWE Supermarkt") "Shopping" -50.0m ]
            let now = DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc)

            match classifyTransactions now [rule] transactions with
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Should classify: {errors}"
            | Ok syncTransactions ->
                let tx1 = syncTransactions.[0]
                Expect.equal tx1.ReviewSteps.Length 1 "Should record exactly one review step"
                let step = tx1.ReviewSteps.[0]
                Expect.equal step.At now "Step timestamp should be the passed-in `now`"
                Expect.equal step.By (ByRule (RuleId ruleId, "Groceries")) "Step actor should name the matched rule"
                Expect.equal step.Category (Some (YnabCategoryId categoryId)) "Step should carry the matched category"

        testCase "no matching rule records no review step" <| fun () ->
            let transactions = [ createTransaction "TX1" (Some "Unbekannt") "Shopping" -50.0m ]

            match classifyTransactions DateTime.UtcNow [] transactions with
            | Error e ->
                let errors = String.concat ", " e
                failtest $"Should classify: {errors}"
            | Ok syncTransactions ->
                Expect.isEmpty syncTransactions.[0].ReviewSteps "No rule matched: nothing to record yet"
    ]

[<Tests>]
let appendReviewStepTests =
    testList "appendReviewStep" [
        testCase "rule application followed by manual correction yields two entries with different At and By" <| fun () ->
            let ruleId = RuleId (Guid.NewGuid())
            let categoryA = YnabCategoryId (Guid.NewGuid())
            let categoryB = YnabCategoryId (Guid.NewGuid())
            let ruleAt = DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc)
            let userAt = DateTime(2026, 9, 11, 8, 5, 0, DateTimeKind.Utc)

            let afterRule =
                { createSyncTransaction () with CategoryId = Some categoryA; CategoryName = Some "Lebensmittel" }
                |> appendReviewStep ruleAt (ByRule (ruleId, "Regel X"))

            let afterCorrection =
                { afterRule with CategoryId = Some categoryB; CategoryName = Some "Drogerie" }
                |> appendReviewStep userAt ByUser

            Expect.equal afterCorrection.ReviewSteps.Length 2 "Rule step + user correction should be two entries"
            let ruleStep = afterCorrection.ReviewSteps.[0]
            let userStep = afterCorrection.ReviewSteps.[1]
            Expect.equal ruleStep.At ruleAt "First step keeps the rule's timestamp"
            Expect.equal ruleStep.By (ByRule (ruleId, "Regel X")) "First step is attributed to the rule"
            Expect.equal userStep.At userAt "Second step carries the correction's timestamp"
            Expect.equal userStep.By ByUser "Second step is attributed to Roman"
            Expect.notEqual ruleStep.At userStep.At "The two steps must have different timestamps"

        testCase "re-applying the same category does not add a new entry" <| fun () ->
            let category = YnabCategoryId (Guid.NewGuid())
            let at1 = DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc)
            let at2 = DateTime(2026, 9, 11, 8, 1, 0, DateTimeKind.Utc)

            let first =
                { createSyncTransaction () with CategoryId = Some category; CategoryName = Some "Lebensmittel" }
                |> appendReviewStep at1 ByUser

            let repeated =
                { first with CategoryId = Some category; CategoryName = Some "Lebensmittel" }
                |> appendReviewStep at2 ByUser

            Expect.equal repeated.ReviewSteps.Length 1 "A repeat with the same category must not add a step"
            Expect.equal repeated.ReviewSteps.[0].At at1 "The single step keeps the original timestamp"

        testCase "a changed PayeeOverride alone still records a new step" <| fun () ->
            let at1 = DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc)
            let at2 = DateTime(2026, 9, 11, 8, 1, 0, DateTimeKind.Utc)

            let first = createSyncTransaction () |> appendReviewStep at1 ByUser
            let withOverride =
                { first with PayeeOverride = Some "REWE" }
                |> appendReviewStep at2 ByUser

            Expect.equal withOverride.ReviewSteps.Length 2 "A PayeeOverride-only change is still a new step"
    ]

// ledger-rjhwp: textbasierte Erkennung (Payee und Memo), geteilt von Sync und Kassenbuch.
[<Tests>]
let textBasedSpecialLinkTests =
    let labels links = links |> List.map (fun (l: ExternalLink) -> l.Label)

    testList "detectSpecialLinksFromText (ledger-rjhwp)" [
        testCase "Amazon mit Bestellnummer ergibt genau einen Deep-Link, auch mit Comdirect-Zeilenpräfix" <| fun () ->
            let links = detectSpecialLinksFromText (Some "AMAZON PAYMENTS EUROPE") "01305-304-1234567-7654321 Kopfhörer"
            Expect.equal (labels links) [ "Bestellung 304-1234567-7654321" ] "ein Link, Label wie im Sync"
            Expect.equal
                links.Head.Url
                "https://www.amazon.de/gp/your-account/order-details?ie=UTF8&orderID=304-1234567-7654321"
                "Deep-Link mit der Id ohne Präfix"

        testCase "Amazon ohne Bestellnummer ergibt den Link zur Bestellübersicht" <| fun () ->
            let links = detectSpecialLinksFromText (Some "AMZN Mktp DE") "Kauf"
            Expect.equal (labels links) [ "Amazon Orders" ] "Bestellübersicht"
            Expect.equal links.Head.Url "https://www.amazon.de/gp/your-account/order-history" "Adresse"

        testCase "PayPal ergibt den Aktivitäten-Link" <| fun () ->
            let links = detectSpecialLinksFromText (Some "PayPal (Europe) S.a.r.l.") "PP.1234.PP Händler"
            Expect.equal (labels links) [ "PayPal Activity" ] "PayPal"
            Expect.equal links.Head.Url "https://www.paypal.com/activities" "Adresse"

        testCase "Text ohne Amazon- oder PayPal-Muster ergibt eine leere Liste" <| fun () ->
            Expect.isEmpty (detectSpecialLinksFromText (Some "REWE Markt") "Wocheneinkauf") "kein Treffer"
            Expect.isEmpty (detectSpecialLinksFromText None "") "leer"

        testCase "Payee = None mit Treffer im Memo wird erkannt" <| fun () ->
            let links = detectSpecialLinksFromText None "AMAZON.DE 028-1111111-2222222"
            Expect.equal (labels links) [ "Bestellung 028-1111111-2222222" ] "Memo allein reicht"

        testCase "detectSpecialTransaction (BankTransaction) und die Textvariante liefern dasselbe" <| fun () ->
            for payee, memo in
                [ Some "AMAZON PAYMENTS", "304-1234567-7654321"
                  None, "PAYPAL *SHOP"
                  Some "AMAZON EU", "PAYPAL Sammelzahlung"
                  Some "REWE", "Einkauf"
                  None, "" ] do
                let transaction = createTransaction "TX" payee memo -10.0m
                Expect.equal
                    (detectSpecialTransaction transaction)
                    (detectSpecialLinksFromText payee memo)
                    $"gleiches Ergebnis für {payee} / {memo}"
    ]
