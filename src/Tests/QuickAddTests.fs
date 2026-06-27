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
open Types
open Thoth.Json.Net

// Quick Add form state and its pure helpers (`parseAmountInput`,
// `buildQuickAddRequest`, `QuickAddFormState`) live in the top-level `Types`
// module; the reducer (form lifecycle + submit) lives in the top-level `State`
// module after the Quick-Add page lift (ynab-q7k3m).

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
    ShowCategoryPicker = false
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

        test "accepts empty payee" {
            // Payee is optional — YNAB allows payee-less transactions
            let result = validateManualTransaction { validRequest with PayeeName = "" }
            Expect.isOk result "Empty payee must be accepted"
        }

        test "accepts whitespace payee" {
            let result = validateManualTransaction { validRequest with PayeeName = "   " }
            Expect.isOk result "Whitespace payee must be accepted (treated as no payee)"
        }

        test "rejects overlong payee" {
            let result = validateManualTransaction { validRequest with PayeeName = String('x', 201) }
            Expect.isError result "Payee > 200 chars must be rejected"
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
            let result = validateManualTransaction { validRequest with Amount = 0m; Memo = Some (String('x', 501)) }
            match result with
            | Error errors -> Expect.equal errors.Length 2 "Both amount and memo errors should be reported"
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

        test "payee_name is omitted when blank" {
            // An empty payee_name would create a payee literally named "" in YNAB
            let jsonEmpty = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) { validRequest with PayeeName = "" }
            let jsonBlank = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) { validRequest with PayeeName = "   " }
            Expect.isFalse (transactionKeys jsonEmpty |> List.contains "payee_name") "payee_name must be absent for empty payee"
            Expect.isFalse (transactionKeys jsonBlank |> List.contains "payee_name") "payee_name must be absent for whitespace payee"
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

        test "accepts empty payee" {
            // Payee is optional in Quick Add — a quick cash entry needs only an amount
            match buildQuickAddRequest { validForm with Payee = "  " } with
            | Ok request -> Expect.equal request.PayeeName "" "Blank payee should map to empty string"
            | Error err -> failtest $"Expected Ok, got Error: {err}"
        }

        test "rejects invalid date" {
            let result = buildQuickAddRequest { validForm with DateText = "kein-datum" }
            Expect.isError result "Invalid date should be rejected"
        }
    ]

// ============================================
// QuickAdd reducer transitions (top-level State.update, ynab-q7k3m)
// ============================================

// Build a top-level model without going through `State.init`, which reads the
// browser URL (Feliz.Router) and is Fable-only. The child inits are pure record
// builders, so they are safe under .NET.
let private modelWithForm (form: QuickAddFormState) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let syncFlowModel, _ = Components.SyncFlow.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    {
        CurrentPage = Types.QuickAdd
        Toasts = []
        Settings = settingsModel
        SyncFlow = syncFlowModel
        Rules = rulesModel
        QuickAdd = Some form
        QuickAddTemplates = NotAsked
    }

let reducerTests =
    testList "QuickAdd update transitions" [
        test "SubmitQuickAdd with invalid form sets form error and issues no save" {
            let model = modelWithForm { validForm with AmountText = "abc" }
            let updated, cmd = State.update State.SubmitQuickAdd model
            match updated.QuickAdd with
            | Some form ->
                Expect.isSome form.Error "Validation error should be set"
                Expect.isFalse form.IsSaving "Must not enter saving state"
            | None -> failtest "Form should stay present"
            Expect.isTrue (List.isEmpty cmd) "No command should be issued on validation error"
            Expect.isTrue (List.isEmpty updated.Toasts) "Validation errors show inline, not as toast"
        }

        // Note: the valid-form SubmitQuickAdd path is not reducer-tested because it
        // builds the Fable.Remoting proxy (Client.Api), whose type initializer is
        // Fable-only and throws under .NET. The form→request mapping it depends on
        // is fully covered by the buildQuickAddRequest tests above.

        test "QuickAddSaved Ok resets the form and emits a success toast" {
            let model = modelWithForm { validForm with IsSaving = true }
            let updated, _ = State.update (State.QuickAddSaved (Ok ())) model
            // On a page, success keeps the user here with a fresh, blank form
            // (rather than dismissing a sheet) so another entry can follow.
            match updated.QuickAdd with
            | Some form ->
                Expect.equal form.AmountText "" "Form should be reset after a successful save"
                Expect.isFalse form.IsSaving "Saving state should be cleared"
            | None -> failtest "Form should remain present on the page"
            Expect.isTrue
                (updated.Toasts |> List.exists (fun t -> t.Type = ToastSuccess))
                "A success toast should be emitted"
        }

        test "QuickAddSaved Error keeps the form with the error message" {
            let model = modelWithForm { validForm with IsSaving = true }
            let updated, _ = State.update (State.QuickAddSaved (Error "kaputt")) model
            match updated.QuickAdd with
            | Some form ->
                Expect.isFalse form.IsSaving "Saving state should be cleared"
                Expect.equal form.Error (Some "kaputt") "Error message should be shown inline"
            | None -> failtest "Form should stay present so the user can retry"
            Expect.isTrue (List.isEmpty updated.Toasts) "Error shows inline, not as toast"
        }
    ]

// ============================================
// amountFromMilliunits (reverse milliunits)
// ============================================

let reverseMilliunitsTests =
    testList "amountFromMilliunits" [
        test "negative milliunits decode to an outflow with absolute amount" {
            let isOutflow, amount = amountFromMilliunits -4500
            Expect.isTrue isOutflow "negative milliunits must be an outflow"
            Expect.equal amount 4.50m "amount must be the absolute value in major units"
        }

        test "positive milliunits decode to an inflow" {
            let isOutflow, amount = amountFromMilliunits 4500
            Expect.isFalse isOutflow "positive milliunits must be an inflow"
            Expect.equal amount 4.50m "amount must be the absolute value in major units"
        }

        test "zero milliunits decode to a non-outflow of zero" {
            let isOutflow, amount = amountFromMilliunits 0
            Expect.isFalse isOutflow "zero is not an outflow"
            Expect.equal amount 0m "zero milliunits is zero amount"
        }

        test "smallest cash amount decodes exactly" {
            let isOutflow, amount = amountFromMilliunits -10
            Expect.isTrue isOutflow "negative is an outflow"
            Expect.equal amount 0.01m "-10 milliunits is 1 cent"
        }

        test "round-trips with manualTransactionMilliunits (outflow)" {
            let req = { validRequest with Amount = 12.34m; IsOutflow = true }
            let isOutflow, amount = amountFromMilliunits (manualTransactionMilliunits req)
            Expect.isTrue isOutflow "direction must survive the round-trip"
            Expect.equal amount 12.34m "amount must survive the round-trip"
        }

        test "round-trips with manualTransactionMilliunits (inflow)" {
            let req = { validRequest with Amount = 12.34m; IsOutflow = false }
            let isOutflow, amount = amountFromMilliunits (manualTransactionMilliunits req)
            Expect.isFalse isOutflow "direction must survive the round-trip"
            Expect.equal amount 12.34m "amount must survive the round-trip"
        }
    ]

// ============================================
// recentQuickAddTemplates (dedup + projection)
// ============================================

let private cat1 = "11111111-1111-1111-1111-111111111111"
let private cat2 = "22222222-2222-2222-2222-222222222222"

/// Builds a YNAB transaction. `amount` is signed major units (negative = outflow),
/// exactly as the YnabClient decoder produces it (milliunits / 1000).
let private mkTxn id daysAgo amount payee categoryId memo : YnabTransaction =
    {
        Id = id
        Date = DateTime.Today.AddDays(- (daysAgo: float))
        Amount = { Amount = amount; Currency = "EUR" }
        Payee = payee
        Memo = memo
        ImportId = None
        CategoryId = categoryId
        CategoryName = categoryId |> Option.map (fun _ -> "Lebensmittel")
    }

let templateDedupTests =
    testList "recentQuickAddTemplates" [
        test "collapses same payee+amount+category to a single template" {
            let txns = [
                mkTxn "a" 1.0 -4.50m (Some "Bäcker") (Some cat1) (Some "neuer Memo")
                mkTxn "b" 5.0 -4.50m (Some "Bäcker") (Some cat1) (Some "alter Memo")
            ]
            let result = recentQuickAddTemplates 5 txns
            Expect.hasLength result 1 "a recurring booking must collapse to one template"
            // distinctBy keeps the most-recent occurrence
            Expect.equal result.[0].Memo (Some "neuer Memo") "the most recent booking's memo wins"
        }

        test "keeps distinct payees as separate templates" {
            let txns = [
                mkTxn "a" 1.0 -4.50m (Some "Bäcker") (Some cat1) None
                mkTxn "b" 2.0 -4.50m (Some "Kiosk") (Some cat1) None
            ]
            Expect.hasLength (recentQuickAddTemplates 5 txns) 2 "different payees are different templates"
        }

        test "keeps distinct amounts as separate templates" {
            let txns = [
                mkTxn "a" 1.0 -4.50m (Some "Bäcker") (Some cat1) None
                mkTxn "b" 2.0 -9.00m (Some "Bäcker") (Some cat1) None
            ]
            Expect.hasLength (recentQuickAddTemplates 5 txns) 2 "different amounts are different templates"
        }

        test "keeps distinct categories as separate templates" {
            let txns = [
                mkTxn "a" 1.0 -4.50m (Some "Bäcker") (Some cat1) None
                mkTxn "b" 2.0 -4.50m (Some "Bäcker") (Some cat2) None
            ]
            Expect.hasLength (recentQuickAddTemplates 5 txns) 2 "different categories are different templates"
        }

        test "returns at most 5 distinct templates" {
            let txns =
                [ for i in 1 .. 8 -> mkTxn (string i) (float i) (decimal (-i)) (Some $"Payee{i}") (Some cat1) None ]
            Expect.hasLength (recentQuickAddTemplates 5 txns) 5 "at most 5 templates are returned"
        }

        test "returns fewer than 5 when fewer distinct bookings exist" {
            let txns = [
                mkTxn "a" 1.0 -4.50m (Some "Bäcker") (Some cat1) None
                mkTxn "b" 2.0 -9.00m (Some "Kiosk") (Some cat1) None
            ]
            Expect.hasLength (recentQuickAddTemplates 5 txns) 2 "shows what exists"
        }

        test "empty input yields no templates" {
            Expect.isEmpty (recentQuickAddTemplates 5 []) "no transactions, no templates"
        }

        test "skips transfers and splits (no category)" {
            let txns = [
                mkTxn "transfer" 1.0 -20.00m (Some "Transfer : Cash") None None
                mkTxn "categorized" 2.0 -4.50m (Some "Bäcker") (Some cat1) None
            ]
            let result = recentQuickAddTemplates 5 txns
            Expect.hasLength result 1 "only the categorized booking becomes a template"
            Expect.equal result.[0].PayeeName (Some "Bäcker") "the transfer is skipped"
        }

        test "skips transactions with an unparseable category id" {
            let txns = [ mkTxn "junk" 1.0 -4.50m (Some "Bäcker") (Some "not-a-guid") None ]
            Expect.isEmpty (recentQuickAddTemplates 5 txns) "a malformed category id cannot be a template"
        }

        test "maps an outflow's sign to IsOutflow with a positive amount" {
            let txns = [ mkTxn "a" 1.0 -4.50m (Some "Bäcker") (Some cat1) None ]
            let t = (recentQuickAddTemplates 5 txns).[0]
            Expect.isTrue t.IsOutflow "negative YNAB amount is an outflow"
            Expect.equal t.Amount 4.50m "the template amount is positive (abs)"
        }

        test "maps an inflow to a non-outflow" {
            let txns = [ mkTxn "a" 1.0 25.00m (Some "Erstattung") (Some cat1) None ]
            let t = (recentQuickAddTemplates 5 txns).[0]
            Expect.isFalse t.IsOutflow "positive YNAB amount is an inflow"
            Expect.equal t.Amount 25.00m "the template amount is positive"
        }

        test "orders templates most-recent first" {
            let txns = [
                mkTxn "old" 30.0 -1.00m (Some "Alt") (Some cat1) None
                mkTxn "new" 1.0 -2.00m (Some "Neu") (Some cat1) None
                mkTxn "mid" 10.0 -3.00m (Some "Mitte") (Some cat1) None
            ]
            let payees = recentQuickAddTemplates 5 txns |> List.map (fun t -> t.PayeeName)
            Expect.equal payees [ Some "Neu"; Some "Mitte"; Some "Alt" ] "most recent booking comes first"
        }
    ]

// ============================================
// applyTemplateToForm (prefill) + formatAmountForInput
// ============================================

let private sampleTemplate : QuickAddTemplate = {
    Amount = 4.50m
    IsOutflow = true
    PayeeName = Some "Bäcker"
    CategoryId = YnabCategoryId (Guid.Parse cat1)
    CategoryName = Some "Lebensmittel"
    Memo = Some "Brötchen"
}

let formatAmountTests =
    testList "formatAmountForInput" [
        test "formats two decimals with a comma" {
            Expect.equal (formatAmountForInput 4.50m) "4,50" "4.50 → 4,50"
        }
        test "pads whole amounts to two decimals" {
            Expect.equal (formatAmountForInput 12m) "12,00" "12 → 12,00"
        }
        test "keeps a leading zero for sub-euro amounts" {
            Expect.equal (formatAmountForInput 0.05m) "0,05" "0.05 → 0,05"
        }
    ]

let prefillTests =
    testList "applyTemplateToForm" [
        test "fills every form field from the template" {
            let form = applyTemplateToForm sampleTemplate (validForm |> fun f -> { f with AmountText = ""; Payee = ""; CategoryId = ""; Memo = "" })
            Expect.equal form.AmountText "4,50" "amount is prefilled (German format)"
            Expect.isTrue form.IsOutflow "direction is prefilled"
            Expect.equal form.Payee "Bäcker" "payee is prefilled"
            Expect.equal form.CategoryId (cat1) "category id is prefilled as a guid string"
            Expect.equal form.Memo "Brötchen" "memo is prefilled"
        }

        test "leaves the date untouched so a prefilled entry stays dated today" {
            // validForm.DateText acts as the form's existing (today) date; prefill
            // must NOT overwrite it with the source transaction's date.
            let before = validForm.DateText
            let form = applyTemplateToForm sampleTemplate validForm
            Expect.equal form.DateText before "the date must not be overwritten by the template"
        }

        test "clears any prior error and closes the picker" {
            let dirty = { validForm with Error = Some "boom"; ShowCategoryPicker = true }
            let form = applyTemplateToForm sampleTemplate dirty
            Expect.equal form.Error None "prefill clears a stale error"
            Expect.isFalse form.ShowCategoryPicker "prefill closes the category picker"
        }

        test "maps an absent payee and memo to empty strings" {
            let t = { sampleTemplate with PayeeName = None; Memo = None }
            let form = applyTemplateToForm t validForm
            Expect.equal form.Payee "" "absent payee becomes an empty string"
            Expect.equal form.Memo "" "absent memo becomes an empty string"
        }
    ]

// ============================================
// All Tests
// ============================================

[<Tests>]
let tests =
    testList "Quick Add Tests" [
        milliunitTests
        reverseMilliunitsTests
        templateDedupTests
        formatAmountTests
        prefillTests
        validationTests
        jsonBodyTests
        parseAmountTests
        buildRequestTests
        reducerTests
    ]
