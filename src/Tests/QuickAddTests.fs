module Tests.QuickAddTests

// Tests for the Quick Add feature (manual transaction entry → YNAB):
// - Shared.Domain.manualTransactionMilliunits (sign + milliunit conversion)
// - Server.Validation.validateManualTransaction (API boundary validation)
// - Server.YnabClient.buildManualTransactionBody (JSON contract with YNAB)
// - Components.SyncFlow.Types.parseAmountInput / buildQuickAddRequest (client form logic)

open System
open Expecto
open Shared.Domain
open Server.Validation
open Server.YnabClient
open Components.SyncFlow.Types
open Components.SyncFlow.State
open Types
open Thoth.Json.Net

// ============================================
// Helpers
// ============================================

let private validRequest = {
    Amount = 4.50m
    IsOutflow = true
    PayeeName = "Bäcker"
    CategoryId = None
    Date = DateTime.Today
    Memo = None
}

let private validForm = {
    AmountText = "4,50"
    IsOutflow = true
    Payee = "Bäcker"
    CategoryId = ""
    DateText = "2026-06-10"
    Memo = ""
    IsSaving = false
    Error = None
}

let private decodeAt (path: string list) (decoder: Decoder<'T>) (json: string) : 'T =
    match Decode.fromString (Decode.at path decoder) json with
    | Ok value -> value
    | Error err -> failwith $"JSON decode failed at %A{path}: {err}"

let private transactionKeys (json: string) : string list =
    decodeAt [ "transaction" ] Decode.keys json

// ============================================
// manualTransactionMilliunits
// ============================================

let milliunitTests =
    testList "manualTransactionMilliunits" [
        test "outflow becomes negative milliunits" {
            let result = manualTransactionMilliunits { validRequest with Amount = 4.50m; IsOutflow = true }
            Expect.equal result -4500 "4,50 € outflow should be -4500 milliunits"
        }

        test "inflow becomes positive milliunits" {
            let result = manualTransactionMilliunits { validRequest with Amount = 4.50m; IsOutflow = false }
            Expect.equal result 4500 "4,50 € inflow should be +4500 milliunits"
        }

        test "smallest cash amount converts exactly" {
            let result = manualTransactionMilliunits { validRequest with Amount = 0.01m }
            Expect.equal result -10 "1 cent outflow should be -10 milliunits"
        }

        test "large amount converts exactly" {
            let result = manualTransactionMilliunits { validRequest with Amount = 1234.56m }
            Expect.equal result -1234560 "1234,56 € outflow should be -1234560 milliunits"
        }
    ]

// ============================================
// validateManualTransaction
// ============================================

let validationTests =
    testList "validateManualTransaction" [
        test "accepts a valid request" {
            let result = validateManualTransaction validRequest
            Expect.isOk result "Valid request should pass validation"
        }

        test "rejects zero amount" {
            let result = validateManualTransaction { validRequest with Amount = 0m }
            Expect.isError result "Zero amount must be rejected"
        }

        test "rejects negative amount" {
            // The sign comes from IsOutflow; raw negative input indicates a client bug
            let result = validateManualTransaction { validRequest with Amount = -5m }
            Expect.isError result "Negative amount must be rejected"
        }

        test "rejects unreasonably large amount" {
            let result = validateManualTransaction { validRequest with Amount = 2_000_000m }
            Expect.isError result "Amounts above 1 million must be rejected"
        }

        test "rejects empty payee" {
            let result = validateManualTransaction { validRequest with PayeeName = "" }
            Expect.isError result "Empty payee must be rejected"
        }

        test "rejects whitespace payee" {
            let result = validateManualTransaction { validRequest with PayeeName = "   " }
            Expect.isError result "Whitespace payee must be rejected"
        }

        test "rejects future date" {
            // YNAB rejects future-dated transactions; we catch this at the API boundary
            let result = validateManualTransaction { validRequest with Date = DateTime.Today.AddDays(1.0) }
            Expect.isError result "Future date must be rejected"
        }

        test "accepts today" {
            let result = validateManualTransaction { validRequest with Date = DateTime.Today }
            Expect.isOk result "Today must be accepted"
        }

        test "rejects date older than five years" {
            let result = validateManualTransaction { validRequest with Date = DateTime.Today.AddYears(-6) }
            Expect.isError result "Very old dates must be rejected"
        }

        test "rejects memo longer than 500 characters" {
            let result = validateManualTransaction { validRequest with Memo = Some (String('x', 501)) }
            Expect.isError result "Memo > 500 chars must be rejected"
        }

        test "accepts memo of exactly 500 characters" {
            let result = validateManualTransaction { validRequest with Memo = Some (String('x', 500)) }
            Expect.isOk result "Memo of 500 chars must be accepted"
        }

        test "collects multiple errors" {
            let result = validateManualTransaction { validRequest with Amount = 0m; PayeeName = "" }
            match result with
            | Error errors -> Expect.equal errors.Length 2 "Both amount and payee errors should be reported"
            | Ok _ -> failtest "Expected validation errors"
        }
    ]

// ============================================
// buildManualTransactionBody (YNAB JSON contract)
// ============================================

let jsonBodyTests =
    testList "buildManualTransactionBody" [
        test "amount is serialized as JSON number in signed milliunits" {
            // Prevents regression of the class of bug where amounts were serialized
            // as strings (e.g. "-4500"), which YNAB silently rejects.
            let accountId = YnabAccountId (Guid.NewGuid())
            let json = buildManualTransactionBody accountId validRequest
            let amount = decodeAt [ "transaction"; "amount" ] Decode.int json
            Expect.equal amount -4500 "Amount should be a signed milliunit number"
        }

        test "no import_id is set for manual entries" {
            // Manual entries must never collide with the bank-import deduplication,
            // which keys on import_id. YNAB treats transactions without import_id
            // as user-entered (like its own mobile quick add).
            let accountId = YnabAccountId (Guid.NewGuid())
            let json = buildManualTransactionBody accountId validRequest
            let keys = transactionKeys json
            Expect.isFalse (keys |> List.contains "import_id") "import_id must be absent"
        }

        test "category_id is included when a category is selected" {
            let categoryGuid = Guid.NewGuid()
            let request = { validRequest with CategoryId = Some (YnabCategoryId categoryGuid) }
            let json = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) request
            let categoryId = decodeAt [ "transaction"; "category_id" ] Decode.string json
            Expect.equal categoryId (categoryGuid.ToString()) "category_id should match the selected category"
        }

        test "category_id is omitted when no category is selected" {
            let json = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) validRequest
            let keys = transactionKeys json
            Expect.isFalse (keys |> List.contains "category_id") "category_id must be absent without category"
        }

        test "memo is included when present" {
            let request = { validRequest with Memo = Some "Brötchen" }
            let json = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) request
            let memo = decodeAt [ "transaction"; "memo" ] Decode.string json
            Expect.equal memo "Brötchen" "Memo should be included"
        }

        test "memo is trimmed when present" {
            // Aligns with payee_name trimming — server-direct callers may not pre-trim
            let request = { validRequest with Memo = Some "  Brötchen  " }
            let json = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) request
            let memo = decodeAt [ "transaction"; "memo" ] Decode.string json
            Expect.equal memo "Brötchen" "Memo should be trimmed"
        }

        test "memo is omitted when None or whitespace" {
            let jsonNone = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) validRequest
            let jsonBlank = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) { validRequest with Memo = Some "   " }
            Expect.isFalse (transactionKeys jsonNone |> List.contains "memo") "Memo must be absent for None"
            Expect.isFalse (transactionKeys jsonBlank |> List.contains "memo") "Memo must be absent for whitespace"
        }

        test "date is formatted as yyyy-MM-dd" {
            let request = { validRequest with Date = DateTime(2026, 6, 10) }
            let json = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) request
            let date = decodeAt [ "transaction"; "date" ] Decode.string json
            Expect.equal date "2026-06-10" "Date should be ISO formatted"
        }

        test "payee name is trimmed" {
            let request = { validRequest with PayeeName = "  Bäcker  " }
            let json = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) request
            let payee = decodeAt [ "transaction"; "payee_name" ] Decode.string json
            Expect.equal payee "Bäcker" "Payee should be trimmed"
        }

        test "manual entries are uncleared" {
            // Project convention: imported/created transactions must never be cleared
            let json = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) validRequest
            let cleared = decodeAt [ "transaction"; "cleared" ] Decode.string json
            Expect.equal cleared "uncleared" "Cleared should be 'uncleared'"
        }
    ]

// ============================================
// parseAmountInput (client form parsing)
// ============================================

let parseAmountTests =
    testList "parseAmountInput" [
        test "parses German comma decimal" {
            Expect.equal (parseAmountInput "4,50") (Some 4.50m) "4,50 should parse"
        }

        test "parses dot decimal" {
            Expect.equal (parseAmountInput "4.50") (Some 4.50m) "4.50 should parse"
        }

        test "parses integer" {
            Expect.equal (parseAmountInput "12") (Some 12m) "12 should parse"
        }

        test "parses single decimal digit" {
            Expect.equal (parseAmountInput "7,5") (Some 7.50m) "7,5 should parse as 7.50"
        }

        test "tolerates surrounding whitespace" {
            Expect.equal (parseAmountInput " 7,50 ") (Some 7.50m) "Whitespace should be tolerated"
        }

        test "parses zero (rejected later by request builder)" {
            Expect.equal (parseAmountInput "0") (Some 0m) "0 parses; the request builder rejects it"
        }

        test "rejects empty input" {
            Expect.isNone (parseAmountInput "") "Empty input should not parse"
        }

        test "rejects non-numeric input" {
            Expect.isNone (parseAmountInput "abc") "Non-numeric input should not parse"
        }

        test "rejects more than two decimal places" {
            Expect.isNone (parseAmountInput "1,234") "Three decimals should not parse"
        }

        test "rejects negative input" {
            // Direction comes from the Ausgabe/Einnahme toggle, not from a sign
            Expect.isNone (parseAmountInput "-5") "Negative input should not parse"
        }

        test "rejects multiple separators" {
            Expect.isNone (parseAmountInput "1,2,3") "Multiple separators should not parse"
        }
    ]

// ============================================
// buildQuickAddRequest (client form → API request)
// ============================================

let buildRequestTests =
    testList "buildQuickAddRequest" [
        test "builds a valid request from a complete form" {
            let categoryGuid = Guid.NewGuid()
            let form = { validForm with Payee = "  Bäcker  "; CategoryId = categoryGuid.ToString(); Memo = " Brötchen " }
            match buildQuickAddRequest form with
            | Ok request ->
                Expect.equal request.Amount 4.50m "Amount should be parsed"
                Expect.isTrue request.IsOutflow "Direction should carry over"
                Expect.equal request.PayeeName "Bäcker" "Payee should be trimmed"
                Expect.equal request.CategoryId (Some (YnabCategoryId categoryGuid)) "Category should be parsed"
                Expect.equal request.Date (DateTime(2026, 6, 10)) "Date should be parsed"
                Expect.equal request.Memo (Some "Brötchen") "Memo should be trimmed"
            | Error err -> failtest $"Expected Ok, got Error: {err}"
        }

        test "empty category becomes None" {
            match buildQuickAddRequest validForm with
            | Ok request -> Expect.isNone request.CategoryId "Empty category string should map to None"
            | Error err -> failtest $"Expected Ok, got Error: {err}"
        }

        test "blank memo becomes None" {
            match buildQuickAddRequest { validForm with Memo = "   " } with
            | Ok request -> Expect.isNone request.Memo "Blank memo should map to None"
            | Error err -> failtest $"Expected Ok, got Error: {err}"
        }

        test "rejects unparseable amount" {
            let result = buildQuickAddRequest { validForm with AmountText = "abc" }
            Expect.isError result "Unparseable amount should be rejected"
        }

        test "rejects zero amount" {
            let result = buildQuickAddRequest { validForm with AmountText = "0" }
            Expect.isError result "Zero amount should be rejected"
        }

        test "rejects empty payee" {
            let result = buildQuickAddRequest { validForm with Payee = "  " }
            Expect.isError result "Empty payee should be rejected"
        }

        test "rejects invalid date" {
            let result = buildQuickAddRequest { validForm with DateText = "kein-datum" }
            Expect.isError result "Invalid date should be rejected"
        }
    ]

// ============================================
// QuickAdd reducer transitions (State.update)
// ============================================

let private modelWithForm form =
    let model, _ = init ()
    { model with QuickAdd = Some form }

let reducerTests =
    testList "QuickAdd update transitions" [
        test "SubmitQuickAdd with invalid form sets form error and issues no save" {
            let model = modelWithForm { validForm with AmountText = "abc" }
            let updated, cmd, external = update SubmitQuickAdd model
            match updated.QuickAdd with
            | Some form ->
                Expect.isSome form.Error "Validation error should be set"
                Expect.isFalse form.IsSaving "Must not enter saving state"
            | None -> failtest "Sheet should stay open"
            Expect.isTrue cmd.IsEmpty "No command should be issued on validation error"
            Expect.equal external NoOp "Validation errors show inline, not as toast"
        }

        // Note: the valid-form SubmitQuickAdd path is not reducer-tested because it
        // builds the Fable.Remoting proxy (Client.Api), whose type initializer is
        // Fable-only and throws under .NET. The form→request mapping it depends on
        // is fully covered by the buildQuickAddRequest tests above.

        test "QuickAddSaved Ok closes the sheet and emits a success toast" {
            let model = modelWithForm { validForm with IsSaving = true }
            let updated, _, external = update (QuickAddSaved (Ok ())) model
            Expect.isNone updated.QuickAdd "Sheet should close after successful save"
            match external with
            | ShowToast (_, ToastSuccess) -> ()
            | other -> failtest $"Expected success toast, got %A{other}"
        }

        test "QuickAddSaved Error keeps the sheet open with the error message" {
            let model = modelWithForm { validForm with IsSaving = true }
            let updated, _, external = update (QuickAddSaved (Error "kaputt")) model
            match updated.QuickAdd with
            | Some form ->
                Expect.isFalse form.IsSaving "Saving state should be cleared"
                Expect.equal form.Error (Some "kaputt") "Error message should be shown inline"
            | None -> failtest "Sheet should stay open so the user can retry"
            Expect.equal external NoOp "Error shows inline, not as toast"
        }
    ]

// ============================================
// All Tests
// ============================================

[<Tests>]
let tests =
    testList "Quick Add Tests" [
        milliunitTests
        validationTests
        jsonBodyTests
        parseAmountTests
        buildRequestTests
        reducerTests
    ]
