module Tests.DuplicateDetectionTests

open System
open Expecto
open Shared.Domain
open Server.DuplicateDetection

// ============================================
// Test Helpers
// ============================================

let createBankTransaction reference payee memo amount bookingDate =
    {
        Id = TransactionId (Guid.NewGuid().ToString())
        BookingDate = bookingDate
        Amount = { Amount = amount; Currency = "EUR" }
        Payee = payee
        Memo = memo
        Reference = reference
        RawData = ""
    }

let createYnabTransaction id date amount payee memo importId =
    {
        Id = id
        Date = date
        Amount = { Amount = amount; Currency = "EUR" }
        Payee = payee
        Memo = memo
        ImportId = importId
    }

// ============================================
// Reference Extraction Tests
// ============================================

[<Tests>]
let referenceExtractionTests =
    testList "Reference Extraction" [
        test "extractReference returns reference from standard format" {
            let memo = Some "Some payee, Some description, Ref: ABC123"
            let result = extractReference memo
            Expect.equal result (Some "ABC123") "Should extract reference"
        }

        test "extractReference handles reference with spaces" {
            let memo = Some "Some payee, Some description, Ref: ABC 123 XYZ"
            let result = extractReference memo
            Expect.equal result (Some "ABC 123 XYZ") "Should extract reference with spaces"
        }

        test "extractReference handles Ref with extra space" {
            let memo = Some "Some payee, Some description, Ref:    SPACES"
            let result = extractReference memo
            Expect.equal result (Some "SPACES") "Should trim extra spaces"
        }

        test "extractReference returns None for None memo" {
            let result = extractReference None
            Expect.equal result None "Should return None"
        }

        test "extractReference returns None for empty string" {
            let result = extractReference (Some "")
            Expect.equal result None "Should return None for empty string"
        }

        test "extractReference returns None for whitespace" {
            let result = extractReference (Some "   ")
            Expect.equal result None "Should return None for whitespace"
        }

        test "extractReference returns None for memo without Ref" {
            let result = extractReference (Some "Some payee, Some description without reference")
            Expect.equal result None "Should return None without Ref:"
        }
    ]

// ============================================
// Match By Reference Tests
// ============================================

[<Tests>]
let matchByReferenceTests =
    testList "Match By Reference" [
        test "matchesByReference returns true when references match" {
            let bankTx = createBankTransaction "REF123" (Some "Payee") "Memo" -50m DateTime.Today
            let ynabTx = createYnabTransaction "id1" DateTime.Today -50m (Some "Payee") (Some "Description, Ref: REF123") None

            let result = matchesByReference bankTx ynabTx
            Expect.isTrue result "Should match by reference"
        }

        test "matchesByReference returns false when references differ" {
            let bankTx = createBankTransaction "REF123" (Some "Payee") "Memo" -50m DateTime.Today
            let ynabTx = createYnabTransaction "id1" DateTime.Today -50m (Some "Payee") (Some "Description, Ref: REF456") None

            let result = matchesByReference bankTx ynabTx
            Expect.isFalse result "Should not match different references"
        }

        test "matchesByReference returns false when YNAB memo has no reference" {
            let bankTx = createBankTransaction "REF123" (Some "Payee") "Memo" -50m DateTime.Today
            let ynabTx = createYnabTransaction "id1" DateTime.Today -50m (Some "Payee") (Some "Description without reference") None

            let result = matchesByReference bankTx ynabTx
            Expect.isFalse result "Should not match without reference"
        }

        test "matchesByReference returns false when YNAB memo is None" {
            let bankTx = createBankTransaction "REF123" (Some "Payee") "Memo" -50m DateTime.Today
            let ynabTx = createYnabTransaction "id1" DateTime.Today -50m (Some "Payee") None None

            let result = matchesByReference bankTx ynabTx
            Expect.isFalse result "Should not match when memo is None"
        }
    ]

// ============================================
// Match By Import ID Tests
// ============================================

[<Tests>]
let matchByImportIdTests =
    testList "Match By Import ID" [
        test "matchesByImportId returns true when import IDs match" {
            let txId = "TX123"
            let bankTx = createBankTransaction "ref" (Some "Payee") "Memo" -50m DateTime.Today
            let bankTx' = { bankTx with Id = TransactionId txId }
            let ynabTx = createYnabTransaction "id1" DateTime.Today -50m (Some "Payee") None (Some $"BUDGETBUDDY:{txId}:12345")

            let result = matchesByImportId bankTx' ynabTx
            Expect.isTrue result "Should match by import ID"
        }

        test "matchesByImportId returns false when import ID format differs" {
            let bankTx = createBankTransaction "ref" (Some "Payee") "Memo" -50m DateTime.Today
            let ynabTx = createYnabTransaction "id1" DateTime.Today -50m (Some "Payee") None (Some "OTHER:TX123:12345")

            let result = matchesByImportId bankTx ynabTx
            Expect.isFalse result "Should not match different format"
        }

        test "matchesByImportId returns false when import ID is None" {
            let bankTx = createBankTransaction "ref" (Some "Payee") "Memo" -50m DateTime.Today
            let ynabTx = createYnabTransaction "id1" DateTime.Today -50m (Some "Payee") None None

            let result = matchesByImportId bankTx ynabTx
            Expect.isFalse result "Should not match when import ID is None"
        }
    ]

// ============================================
// Match By Date/Amount/Payee Tests
// ============================================

[<Tests>]
let matchByDateAmountPayeeTests =
    testList "Match By Date/Amount/Payee" [
        test "matchesByDateAmountPayee returns true for exact match" {
            let today = DateTime.Today
            let bankTx = createBankTransaction "ref" (Some "AMAZON EU") "Memo" -50.00m today
            let ynabTx = createYnabTransaction "id1" today -50.00m (Some "AMAZON EU") None None

            let result = matchesByDateAmountPayee defaultConfig bankTx ynabTx
            Expect.isTrue result "Should match exactly"
        }

        test "matchesByDateAmountPayee returns true when payee contains other" {
            let today = DateTime.Today
            let bankTx = createBankTransaction "ref" (Some "AMAZON EU SARL") "Memo" -50.00m today
            let ynabTx = createYnabTransaction "id1" today -50.00m (Some "AMAZON EU") None None

            let result = matchesByDateAmountPayee defaultConfig bankTx ynabTx
            Expect.isTrue result "Should match when payee contains"
        }

        test "matchesByDateAmountPayee returns true for date within tolerance" {
            let today = DateTime.Today
            let yesterday = today.AddDays(-1.0)
            let bankTx = createBankTransaction "ref" (Some "PAYEE") "Memo" -50.00m today
            let ynabTx = createYnabTransaction "id1" yesterday -50.00m (Some "PAYEE") None None

            let result = matchesByDateAmountPayee defaultConfig bankTx ynabTx
            Expect.isTrue result "Should match within date tolerance"
        }

        test "matchesByDateAmountPayee returns false for date outside tolerance" {
            let today = DateTime.Today
            let threeDaysAgo = today.AddDays(-3.0)
            let bankTx = createBankTransaction "ref" (Some "PAYEE") "Memo" -50.00m today
            let ynabTx = createYnabTransaction "id1" threeDaysAgo -50.00m (Some "PAYEE") None None

            let result = matchesByDateAmountPayee defaultConfig bankTx ynabTx
            Expect.isFalse result "Should not match outside date tolerance"
        }

        test "matchesByDateAmountPayee returns false for different amounts" {
            let today = DateTime.Today
            let bankTx = createBankTransaction "ref" (Some "PAYEE") "Memo" -50.00m today
            let ynabTx = createYnabTransaction "id1" today -51.00m (Some "PAYEE") None None

            let result = matchesByDateAmountPayee defaultConfig bankTx ynabTx
            Expect.isFalse result "Should not match different amounts"
        }

        test "matchesByDateAmountPayee returns false for different payees" {
            let today = DateTime.Today
            let bankTx = createBankTransaction "ref" (Some "PAYEE A") "Memo" -50.00m today
            let ynabTx = createYnabTransaction "id1" today -50.00m (Some "PAYEE B") None None

            let result = matchesByDateAmountPayee defaultConfig bankTx ynabTx
            Expect.isFalse result "Should not match different payees"
        }

        test "matchesByDateAmountPayee is case insensitive" {
            let today = DateTime.Today
            let bankTx = createBankTransaction "ref" (Some "payee") "Memo" -50.00m today
            let ynabTx = createYnabTransaction "id1" today -50.00m (Some "PAYEE") None None

            let result = matchesByDateAmountPayee defaultConfig bankTx ynabTx
            Expect.isTrue result "Should match case-insensitively"
        }

        test "matchesByDateAmountPayee returns false when payee is missing" {
            let today = DateTime.Today
            let bankTx = createBankTransaction "ref" None "Memo" -50.00m today
            let ynabTx = createYnabTransaction "id1" today -50.00m (Some "PAYEE") None None

            let result = matchesByDateAmountPayee defaultConfig bankTx ynabTx
            Expect.isFalse result "Should not match when payee is missing"
        }
    ]

// ============================================
// Detect Duplicate Tests
// ============================================

[<Tests>]
let detectDuplicateTests =
    testList "Detect Duplicate" [
        test "detectDuplicate returns ConfirmedDuplicate for reference match" {
            let bankTx = createBankTransaction "REF123" (Some "Payee") "Memo" -50m DateTime.Today
            let ynabTransactions = [
                createYnabTransaction "id1" DateTime.Today -50m (Some "Payee") (Some "Desc, Ref: REF123") None
            ]

            let result = detectDuplicate defaultConfig ynabTransactions bankTx
            match result with
            | ConfirmedDuplicate ref -> Expect.equal ref "REF123" "Should be confirmed duplicate"
            | _ -> failwith "Expected ConfirmedDuplicate"
        }

        test "detectDuplicate returns ConfirmedDuplicate for import ID match" {
            let txId = "TX789"
            let bankTx = { createBankTransaction "REF456" (Some "Payee") "Memo" -50m DateTime.Today with Id = TransactionId txId }
            let ynabTransactions = [
                createYnabTransaction "id1" DateTime.Today -50m (Some "Payee") None (Some $"BUDGETBUDDY:{txId}:12345")
            ]

            let result = detectDuplicate defaultConfig ynabTransactions bankTx
            match result with
            | ConfirmedDuplicate _ -> ()
            | _ -> failwith "Expected ConfirmedDuplicate"
        }

        test "detectDuplicate returns PossibleDuplicate for fuzzy match" {
            let today = DateTime.Today
            let bankTx = createBankTransaction "NEW_REF" (Some "AMAZON EU") "Memo" -50m today
            let ynabTransactions = [
                createYnabTransaction "id1" today -50m (Some "AMAZON EU") (Some "No reference") None
            ]

            let result = detectDuplicate defaultConfig ynabTransactions bankTx
            match result with
            | PossibleDuplicate reason ->
                Expect.stringContains reason "AMAZON EU" "Reason should mention payee"
            | _ -> failwith "Expected PossibleDuplicate"
        }

        test "detectDuplicate returns NotDuplicate when no match" {
            let today = DateTime.Today
            let tenDaysAgo = today.AddDays(-10.0)
            let bankTx = createBankTransaction "REF123" (Some "Unique Payee") "Memo" -123.45m today
            let ynabTransactions = [
                createYnabTransaction "id1" tenDaysAgo -50m (Some "Other Payee") None None
            ]

            let result = detectDuplicate defaultConfig ynabTransactions bankTx
            match result with
            | NotDuplicate -> ()
            | _ -> failwith "Expected NotDuplicate"
        }

        test "detectDuplicate returns NotDuplicate for empty YNAB transactions" {
            let bankTx = createBankTransaction "REF123" (Some "Payee") "Memo" -50m DateTime.Today
            let result = detectDuplicate defaultConfig [] bankTx
            match result with
            | NotDuplicate -> ()
            | _ -> failwith "Expected NotDuplicate"
        }
    ]

// ============================================
// Mark Duplicates Tests
// ============================================

[<Tests>]
let markDuplicatesTests =
    testList "Mark Duplicates" [
        test "markDuplicates marks all transactions correctly" {
            let today = DateTime.Today

            let bankTx1 = createBankTransaction "REF1" (Some "Payee1") "Memo1" -50m today
            let bankTx2 = createBankTransaction "REF2" (Some "Payee2") "Memo2" -100m today
            let bankTx3 = createBankTransaction "REF3" (Some "Payee3") "Memo3" -150m today

            let syncTransactions = [
                { Transaction = bankTx1
                  Status = Pending
                  CategoryId = None
                  CategoryName = None
                  MatchedRuleId = None
                  PayeeOverride = None
                  ExternalLinks = []
                  UserNotes = None
                  DuplicateStatus = NotDuplicate }
                { Transaction = bankTx2
                  Status = Pending
                  CategoryId = None
                  CategoryName = None
                  MatchedRuleId = None
                  PayeeOverride = None
                  ExternalLinks = []
                  UserNotes = None
                  DuplicateStatus = NotDuplicate }
                { Transaction = bankTx3
                  Status = Pending
                  CategoryId = None
                  CategoryName = None
                  MatchedRuleId = None
                  PayeeOverride = None
                  ExternalLinks = []
                  UserNotes = None
                  DuplicateStatus = NotDuplicate }
            ]

            let ynabTransactions = [
                createYnabTransaction "id1" today -50m (Some "Payee1") (Some "Desc, Ref: REF1") None  // matches bankTx1
            ]

            let result = markDuplicates ynabTransactions syncTransactions

            // First transaction should be confirmed duplicate
            match result.[0].DuplicateStatus with
            | ConfirmedDuplicate ref -> Expect.equal ref "REF1" "First should be confirmed duplicate"
            | _ -> failwith "Expected ConfirmedDuplicate for first"

            // Second and third should be not duplicate
            match result.[1].DuplicateStatus with
            | NotDuplicate -> ()
            | _ -> failwith "Expected NotDuplicate for second"

            match result.[2].DuplicateStatus with
            | NotDuplicate -> ()
            | _ -> failwith "Expected NotDuplicate for third"
        }
    ]

// ============================================
// Additional Edge Case Tests
// ============================================

[<Tests>]
let additionalEdgeCaseTests =
    testList "Additional Edge Cases" [
        test "markDuplicates returns empty list for empty input" {
            let result = markDuplicates [] []
            Expect.isEmpty result "Should return empty list"
        }

        test "detectDuplicate prioritizes reference match over fuzzy match" {
            let today = DateTime.Today
            let bankTx = createBankTransaction "REF123" (Some "AMAZON EU") "Memo" -50m today

            // This YNAB transaction would match by both reference AND fuzzy (same date/amount/payee)
            let ynabTransactions = [
                createYnabTransaction "id1" today -50m (Some "AMAZON EU") (Some "Desc, Ref: REF123") None
            ]

            let result = detectDuplicate defaultConfig ynabTransactions bankTx

            // Should be ConfirmedDuplicate (by reference), not PossibleDuplicate (by fuzzy)
            match result with
            | ConfirmedDuplicate _ -> ()
            | _ -> failwith "Expected ConfirmedDuplicate when reference matches"
        }

        test "markDuplicates preserves other SyncTransaction fields" {
            let today = DateTime.Today
            let bankTx = createBankTransaction "REF999" (Some "Payee") "Memo" -50m today
            let ruleId = RuleId (Guid.NewGuid())
            let categoryId = YnabCategoryId (Guid.NewGuid())

            let originalSyncTx = {
                Transaction = bankTx
                Status = AutoCategorized
                CategoryId = Some categoryId
                CategoryName = Some "Test Category"
                MatchedRuleId = Some ruleId
                PayeeOverride = Some "Override Payee"
                ExternalLinks = [{ Label = "Link"; Url = "https://example.com" }]
                UserNotes = Some "User note"
                DuplicateStatus = NotDuplicate
            }

            // YNAB transaction that matches
            let ynabTransactions = [
                createYnabTransaction "id1" today -50m (Some "Payee") (Some "Desc, Ref: REF999") None
            ]

            let result = markDuplicates ynabTransactions [originalSyncTx]

            // Verify all fields are preserved except DuplicateStatus
            let resultTx = result.[0]
            Expect.equal resultTx.Status AutoCategorized "Status should be preserved"
            Expect.equal resultTx.CategoryId (Some categoryId) "CategoryId should be preserved"
            Expect.equal resultTx.CategoryName (Some "Test Category") "CategoryName should be preserved"
            Expect.equal resultTx.MatchedRuleId (Some ruleId) "MatchedRuleId should be preserved"
            Expect.equal resultTx.PayeeOverride (Some "Override Payee") "PayeeOverride should be preserved"
            Expect.hasLength resultTx.ExternalLinks 1 "ExternalLinks should be preserved"
            Expect.equal resultTx.UserNotes (Some "User note") "UserNotes should be preserved"
            // DuplicateStatus should be updated
            match resultTx.DuplicateStatus with
            | ConfirmedDuplicate _ -> ()
            | _ -> failwith "DuplicateStatus should be ConfirmedDuplicate"
        }

        test "matchesByDateAmountPayee respects custom date tolerance" {
            let today = DateTime.Today
            let fourDaysAgo = today.AddDays(-4.0)
            let bankTx = createBankTransaction "ref" (Some "PAYEE") "Memo" -50.00m today
            let ynabTx = createYnabTransaction "id1" fourDaysAgo -50.00m (Some "PAYEE") None None

            // Default config (1 day tolerance) should NOT match
            let defaultResult = matchesByDateAmountPayee defaultConfig bankTx ynabTx
            Expect.isFalse defaultResult "Should not match with default 1-day tolerance"

            // Custom config (5 day tolerance) SHOULD match
            let customConfig = { DateToleranceDays = 5; AmountTolerancePercent = 0.01m }
            let customResult = matchesByDateAmountPayee customConfig bankTx ynabTx
            Expect.isTrue customResult "Should match with 5-day tolerance"
        }
    ]

// ============================================
// Count Duplicates Tests
// ============================================

[<Tests>]
let countDuplicatesTests =
    testList "Count Duplicates" [
        test "countDuplicates counts correctly" {
            let bankTx = createBankTransaction "ref" (Some "Payee") "Memo" -50m DateTime.Today

            let syncTransactions = [
                { Transaction = bankTx
                  Status = Pending
                  CategoryId = None
                  CategoryName = None
                  MatchedRuleId = None
                  PayeeOverride = None
                  ExternalLinks = []
                  UserNotes = None
                  DuplicateStatus = ConfirmedDuplicate "ref1" }
                { Transaction = bankTx
                  Status = Pending
                  CategoryId = None
                  CategoryName = None
                  MatchedRuleId = None
                  PayeeOverride = None
                  ExternalLinks = []
                  UserNotes = None
                  DuplicateStatus = PossibleDuplicate "reason" }
                { Transaction = bankTx
                  Status = Pending
                  CategoryId = None
                  CategoryName = None
                  MatchedRuleId = None
                  PayeeOverride = None
                  ExternalLinks = []
                  UserNotes = None
                  DuplicateStatus = NotDuplicate }
                { Transaction = bankTx
                  Status = Pending
                  CategoryId = None
                  CategoryName = None
                  MatchedRuleId = None
                  PayeeOverride = None
                  ExternalLinks = []
                  UserNotes = None
                  DuplicateStatus = NotDuplicate }
            ]

            let result = countDuplicates syncTransactions
            Expect.equal result.Confirmed 1 "Should have 1 confirmed"
            Expect.equal result.Possible 1 "Should have 1 possible"
            Expect.equal result.None 2 "Should have 2 not duplicate"
        }
    ]
