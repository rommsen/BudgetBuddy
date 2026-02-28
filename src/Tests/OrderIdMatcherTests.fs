module Tests.OrderIdMatcherTests

open System
open Expecto
open Shared.Domain
open Server.OrderIdMatcher

// ============================================
// Test Helpers
// ============================================

let createBankTransaction payee memo =
    {
        Id = TransactionId (Guid.NewGuid().ToString())
        BookingDate = DateTime.Today
        Amount = { Amount = -50m; Currency = "EUR" }
        Payee = payee
        Memo = memo
        Reference = "REF"
        RawData = ""
    }

let createSyncTransaction payee memo status categoryId categoryName =
    {
        Transaction = createBankTransaction payee memo
        Status = status
        CategoryId = categoryId
        CategoryName = categoryName
        MatchedRuleId = None
        PayeeOverride = None
        ExternalLinks = []
        UserNotes = None
        DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF")
        YnabImportStatus = NotAttempted
        Splits = None
        SuggestedByOrderId = None
    }

let createYnabTransaction memo payee categoryId categoryName date =
    {
        Id = Guid.NewGuid().ToString()
        Date = date
        Amount = { Amount = -50m; Currency = "EUR" }
        Payee = payee
        Memo = memo
        ImportId = None
        CategoryId = categoryId
        CategoryName = categoryName
    }

let testCategoryId = YnabCategoryId (Guid.Parse "11111111-1111-1111-1111-111111111111")
let testCategoryIdStr = "11111111-1111-1111-1111-111111111111"

// ============================================
// buildYnabOrderIdMap Tests
// ============================================

[<Tests>]
let buildYnabOrderIdMapTests =
    testList "OrderIdMatcher.buildYnabOrderIdMap" [

        testCase "empty list returns empty map" <| fun () ->
            let result = buildYnabOrderIdMap []
            Expect.isEmpty (Map.toList result) "Map should be empty for empty input"

        testCase "transaction without Order ID is ignored" <| fun () ->
            let tx = createYnabTransaction (Some "Regular purchase") (Some "Store") (Some testCategoryIdStr) (Some "Groceries") DateTime.Today
            let result = buildYnabOrderIdMap [ tx ]
            Expect.isEmpty (Map.toList result) "Should ignore transactions without Amazon Order ID"

        testCase "transaction without category is ignored" <| fun () ->
            let tx = createYnabTransaction (Some "Amazon ABC-1234567-1234567") (Some "AMAZON") None None DateTime.Today
            let result = buildYnabOrderIdMap [ tx ]
            Expect.isEmpty (Map.toList result) "Should ignore transactions without category"

        testCase "transaction with Order ID and category creates entry" <| fun () ->
            let tx = createYnabTransaction (Some "Amazon ABC-1234567-1234567") (Some "AMAZON") (Some testCategoryIdStr) (Some "Electronics") DateTime.Today
            let result = buildYnabOrderIdMap [ tx ]
            Expect.equal (Map.count result) 1 "Should have one entry"
            let suggestion = Map.find "ABC-1234567-1234567" result
            Expect.equal suggestion.CategoryId testCategoryIdStr "Category ID should match"
            Expect.equal suggestion.CategoryName "Electronics" "Category name should match"
            Expect.equal suggestion.OrderId "ABC-1234567-1234567" "Order ID should match"

        testCase "Order ID in payee is also found" <| fun () ->
            let tx = createYnabTransaction None (Some "AMAZON ABC-1234567-1234567") (Some testCategoryIdStr) (Some "Electronics") DateTime.Today
            let result = buildYnabOrderIdMap [ tx ]
            Expect.equal (Map.count result) 1 "Should find Order ID in payee"

        testCase "newest transaction wins when same Order ID" <| fun () ->
            let older = createYnabTransaction (Some "Amazon ABC-1234567-1234567") (Some "AMAZON") (Some testCategoryIdStr) (Some "Old Category") (DateTime.Today.AddDays(-5))
            let newer = createYnabTransaction (Some "Amazon ABC-1234567-1234567") (Some "AMAZON") (Some "22222222-2222-2222-2222-222222222222") (Some "New Category") DateTime.Today
            let result = buildYnabOrderIdMap [ older; newer ]
            Expect.equal (Map.count result) 1 "Should have one entry for same Order ID"
            let suggestion = Map.find "ABC-1234567-1234567" result
            Expect.equal suggestion.CategoryName "New Category" "Newest transaction's category should win"

        testCase "different Order IDs create separate entries" <| fun () ->
            let tx1 = createYnabTransaction (Some "Amazon ABC-1234567-1234567") (Some "AMAZON") (Some testCategoryIdStr) (Some "Electronics") DateTime.Today
            let tx2 = createYnabTransaction (Some "Amazon DEF-7654321-7654321") (Some "AMAZON") (Some "22222222-2222-2222-2222-222222222222") (Some "Books") DateTime.Today
            let result = buildYnabOrderIdMap [ tx1; tx2 ]
            Expect.equal (Map.count result) 2 "Should have two entries for different Order IDs"

        testCase "transaction with CategoryId but no CategoryName is ignored" <| fun () ->
            let tx = createYnabTransaction (Some "Amazon ABC-1234567-1234567") (Some "AMAZON") (Some testCategoryIdStr) None DateTime.Today
            let result = buildYnabOrderIdMap [ tx ]
            Expect.isEmpty (Map.toList result) "Should ignore transaction with CategoryId but no CategoryName"

        testCase "transaction with CategoryName but no CategoryId is ignored" <| fun () ->
            let tx = createYnabTransaction (Some "Amazon ABC-1234567-1234567") (Some "AMAZON") None (Some "Electronics") DateTime.Today
            let result = buildYnabOrderIdMap [ tx ]
            Expect.isEmpty (Map.toList result) "Should ignore transaction with CategoryName but no CategoryId"
    ]

// ============================================
// applySuggestions Tests
// ============================================

[<Tests>]
let applySuggestionsTests =
    testList "OrderIdMatcher.applySuggestions" [

        testCase "empty map returns transactions unchanged" <| fun () ->
            let tx = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            let result = applySuggestions Map.empty [ tx ]
            Expect.equal result.Length 1 "Should have one transaction"
            Expect.isNone result.[0].CategoryId "Category should remain None"
            Expect.isNone result.[0].SuggestedByOrderId "SuggestedByOrderId should remain None"

        testCase "uncategorized Amazon transaction gets suggestion" <| fun () ->
            let orderIdMap = Map.ofList [
                "ABC-1234567-1234567", { OrderId = "ABC-1234567-1234567"; CategoryId = testCategoryIdStr; CategoryName = "Electronics"; SourceDate = DateTime.Today }
            ]
            let tx = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            let result = applySuggestions orderIdMap [ tx ]
            Expect.equal result.[0].CategoryId (Some testCategoryId) "Should have suggested category"
            Expect.equal result.[0].CategoryName (Some "Electronics") "Should have suggested category name"
            Expect.equal result.[0].SuggestedByOrderId (Some "ABC-1234567-1234567") "Should have SuggestedByOrderId"

        testCase "already categorized transaction is not changed" <| fun () ->
            let orderIdMap = Map.ofList [
                "ABC-1234567-1234567", { OrderId = "ABC-1234567-1234567"; CategoryId = testCategoryIdStr; CategoryName = "Electronics"; SourceDate = DateTime.Today }
            ]
            let existingCatId = YnabCategoryId (Guid.Parse "33333333-3333-3333-3333-333333333333")
            let tx = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" AutoCategorized (Some existingCatId) (Some "Existing")
            let result = applySuggestions orderIdMap [ tx ]
            Expect.equal result.[0].CategoryId (Some existingCatId) "Should keep existing category"
            Expect.isNone result.[0].SuggestedByOrderId "SuggestedByOrderId should remain None"

        testCase "non-Amazon transaction is not changed" <| fun () ->
            let orderIdMap = Map.ofList [
                "ABC-1234567-1234567", { OrderId = "ABC-1234567-1234567"; CategoryId = testCategoryIdStr; CategoryName = "Electronics"; SourceDate = DateTime.Today }
            ]
            let tx = createSyncTransaction (Some "Regular Store") "Regular purchase" Pending None None
            let result = applySuggestions orderIdMap [ tx ]
            Expect.isNone result.[0].CategoryId "Non-Amazon transaction should not get suggestion"

        testCase "NeedsAttention transaction gets suggestion but keeps status" <| fun () ->
            let orderIdMap = Map.ofList [
                "ABC-1234567-1234567", { OrderId = "ABC-1234567-1234567"; CategoryId = testCategoryIdStr; CategoryName = "Electronics"; SourceDate = DateTime.Today }
            ]
            let tx = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" NeedsAttention None None
            let result = applySuggestions orderIdMap [ tx ]
            Expect.equal result.[0].CategoryId (Some testCategoryId) "Should have suggested category"
            Expect.equal result.[0].Status NeedsAttention "Status should remain NeedsAttention"

        testCase "no matching Order ID leaves transaction unchanged" <| fun () ->
            let orderIdMap = Map.ofList [
                "XYZ-9999999-9999999", { OrderId = "XYZ-9999999-9999999"; CategoryId = testCategoryIdStr; CategoryName = "Electronics"; SourceDate = DateTime.Today }
            ]
            let tx = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            let result = applySuggestions orderIdMap [ tx ]
            Expect.isNone result.[0].CategoryId "Non-matching Order ID should not get suggestion"

        testCase "Imported status transaction is not given a suggestion" <| fun () ->
            let orderIdMap = Map.ofList [
                "ABC-1234567-1234567", { OrderId = "ABC-1234567-1234567"; CategoryId = testCategoryIdStr; CategoryName = "Electronics"; SourceDate = DateTime.Today }
            ]
            let tx = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Imported None None
            let result = applySuggestions orderIdMap [ tx ]
            Expect.isNone result.[0].CategoryId "Imported transaction should not get suggestion"
            Expect.isNone result.[0].SuggestedByOrderId "SuggestedByOrderId should remain None"

        testCase "Skipped status transaction is not given a suggestion" <| fun () ->
            let orderIdMap = Map.ofList [
                "ABC-1234567-1234567", { OrderId = "ABC-1234567-1234567"; CategoryId = testCategoryIdStr; CategoryName = "Electronics"; SourceDate = DateTime.Today }
            ]
            let tx = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Skipped None None
            let result = applySuggestions orderIdMap [ tx ]
            Expect.isNone result.[0].CategoryId "Skipped transaction should not get suggestion"
            Expect.isNone result.[0].SuggestedByOrderId "SuggestedByOrderId should remain None"

        testCase "ManualCategorized status transaction is not given a suggestion" <| fun () ->
            let orderIdMap = Map.ofList [
                "ABC-1234567-1234567", { OrderId = "ABC-1234567-1234567"; CategoryId = testCategoryIdStr; CategoryName = "Electronics"; SourceDate = DateTime.Today }
            ]
            let tx = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized None None
            let result = applySuggestions orderIdMap [ tx ]
            Expect.isNone result.[0].CategoryId "ManualCategorized transaction should not get suggestion"
            Expect.isNone result.[0].SuggestedByOrderId "SuggestedByOrderId should remain None"

        testCase "mixed list: only eligible transactions get suggestions" <| fun () ->
            let orderIdMap = Map.ofList [
                "ABC-1234567-1234567", { OrderId = "ABC-1234567-1234567"; CategoryId = testCategoryIdStr; CategoryName = "Electronics"; SourceDate = DateTime.Today }
            ]
            // 1. Pending Amazon with matching Order ID -> should get suggestion
            let tx1 = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            // 2. AutoCategorized Amazon with matching Order ID but existing category -> should NOT change
            let existingCatId = YnabCategoryId (Guid.Parse "33333333-3333-3333-3333-333333333333")
            let tx2 = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" AutoCategorized (Some existingCatId) (Some "Existing")
            // 3. Pending non-Amazon -> should NOT get suggestion
            let tx3 = createSyncTransaction (Some "Regular Store") "Regular purchase" Pending None None
            // 4. Pending Amazon with different Order ID -> should NOT get suggestion
            let tx4 = createSyncTransaction (Some "AMAZON") "DEF-7654321-7654321" Pending None None
            let result = applySuggestions orderIdMap [ tx1; tx2; tx3; tx4 ]
            Expect.equal result.Length 4 "Should have all four transactions"
            // tx1: should get suggestion
            Expect.equal result.[0].CategoryId (Some testCategoryId) "First transaction should get suggested category"
            Expect.equal result.[0].SuggestedByOrderId (Some "ABC-1234567-1234567") "First transaction should have SuggestedByOrderId"
            // tx2: should keep existing category
            Expect.equal result.[1].CategoryId (Some existingCatId) "Second transaction should keep existing category"
            Expect.isNone result.[1].SuggestedByOrderId "Second transaction should not have SuggestedByOrderId"
            // tx3: should remain unchanged
            Expect.isNone result.[2].CategoryId "Third transaction should remain uncategorized"
            Expect.isNone result.[2].SuggestedByOrderId "Third transaction should not have SuggestedByOrderId"
            // tx4: should remain unchanged (different Order ID)
            Expect.isNone result.[3].CategoryId "Fourth transaction should remain uncategorized"
            Expect.isNone result.[3].SuggestedByOrderId "Fourth transaction should not have SuggestedByOrderId"
    ]

// ============================================
// propagateInSession Tests
// ============================================

[<Tests>]
let propagateInSessionTests =
    testList "OrderIdMatcher.propagateInSession" [

        testCase "category propagates to transaction with same Order ID" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized (Some testCategoryId) (Some "Electronics")
            let other = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            let result = propagateInSession categorized [ categorized; other ]
            Expect.equal result.Length 1 "Should propagate to one transaction"
            Expect.equal result.[0].CategoryId (Some testCategoryId) "Should have propagated category"
            Expect.equal result.[0].CategoryName (Some "Electronics") "Should have propagated category name"
            Expect.equal result.[0].SuggestedByOrderId (Some "ABC-1234567-1234567") "Should have SuggestedByOrderId"

        testCase "does not propagate to self" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized (Some testCategoryId) (Some "Electronics")
            let result = propagateInSession categorized [ categorized ]
            Expect.isEmpty result "Should not propagate to self"

        testCase "does not propagate when source has no category" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            let other = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            let result = propagateInSession categorized [ categorized; other ]
            Expect.isEmpty result "Should not propagate without category"

        testCase "does not propagate when source is not Amazon" <| fun () ->
            let categorized = createSyncTransaction (Some "Regular Store") "Regular purchase" ManualCategorized (Some testCategoryId) (Some "Electronics")
            let other = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            let result = propagateInSession categorized [ categorized; other ]
            Expect.isEmpty result "Should not propagate from non-Amazon transaction"

        testCase "does not propagate to already categorized transactions" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized (Some testCategoryId) (Some "Electronics")
            let existingCatId = YnabCategoryId (Guid.Parse "33333333-3333-3333-3333-333333333333")
            let alreadyCategorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" AutoCategorized (Some existingCatId) (Some "Existing")
            let result = propagateInSession categorized [ categorized; alreadyCategorized ]
            Expect.isEmpty result "Should not overwrite existing category"

        testCase "does not propagate to different Order ID" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized (Some testCategoryId) (Some "Electronics")
            let other = createSyncTransaction (Some "AMAZON") "DEF-7654321-7654321" Pending None None
            let result = propagateInSession categorized [ categorized; other ]
            Expect.isEmpty result "Should not propagate to different Order ID"

        testCase "does not propagate to skipped transactions" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized (Some testCategoryId) (Some "Electronics")
            let skipped = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Skipped None None
            let result = propagateInSession categorized [ categorized; skipped ]
            Expect.isEmpty result "Should not propagate to skipped transactions"

        testCase "propagates to multiple matching transactions" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized (Some testCategoryId) (Some "Electronics")
            let other1 = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            let other2 = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" NeedsAttention None None
            let result = propagateInSession categorized [ categorized; other1; other2 ]
            Expect.equal result.Length 2 "Should propagate to both matching transactions"

        testCase "propagates even when source CategoryName is None" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized (Some testCategoryId) None
            let other = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Pending None None
            let result = propagateInSession categorized [ categorized; other ]
            Expect.equal result.Length 1 "Should propagate to one transaction"
            Expect.equal result.[0].CategoryId (Some testCategoryId) "Should have propagated category"
            Expect.isNone result.[0].CategoryName "CategoryName should be None since source had None"
            Expect.equal result.[0].SuggestedByOrderId (Some "ABC-1234567-1234567") "Should have SuggestedByOrderId"

        testCase "does not propagate to Imported transactions" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized (Some testCategoryId) (Some "Electronics")
            let imported = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" Imported None None
            let result = propagateInSession categorized [ categorized; imported ]
            Expect.isEmpty result "Should not propagate to Imported transactions"

        testCase "empty transaction list returns empty list" <| fun () ->
            let categorized = createSyncTransaction (Some "AMAZON") "ABC-1234567-1234567" ManualCategorized (Some testCategoryId) (Some "Electronics")
            let result = propagateInSession categorized []
            Expect.isEmpty result "Should return empty list for empty input"
    ]
