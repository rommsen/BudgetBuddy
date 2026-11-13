module Tests.ManualTransactionTests

// Die Bausteine einer manuell erfassten Buchung (Neue Buchung, Bobs Barbuchung), vorher Teil von
// QuickAddTests.fs; die Seite Quick Add ist mit ledger-ea70s entfallen, diese Bausteine bleiben:
// - Shared.Domain.manualTransactionMilliunits (Vorzeichen und Milliunits)
// - Server.Validation.validateManualTransaction (Prüfung an der API-Grenze, auch für Neue Buchung)
// - Server.YnabClient.buildManualTransactionBody (JSON-Vertrag mit YNAB)
// - Types.parseAmountInput / formatAmountForInput (Betragsfelder im Client)

open System
open Expecto
open Shared.Domain
open Server.Validation
open Server.YnabClient
open Types
open Thoth.Json.Net

let private validRequest = {
    Amount = 4.50m
    IsOutflow = true
    PayeeName = "Bäcker"
    CategoryId = None
    Date = DateTime.Today
    Memo = None
}

let private decodeAt (path: string list) (decoder: Decoder<'T>) (json: string) : 'T =
    match Decode.fromString (Decode.at path decoder) json with
    | Ok value -> value
    | Error err -> failwith $"JSON decode failed at %A{path}: {err}"

let private transactionKeys (json: string) : string list =
    decodeAt [ "transaction" ] Decode.keys json

let private berlinDayAtHalfPastMidnight =
    Ledger.Bot.Domain.berlinDayOf
        (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
        (DateTime(2026, 11, 1, 23, 30, 0, DateTimeKind.Utc))

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

let validationTests =
    let today = validRequest.Date
    testList "validateManualTransaction" [
        test "accepts a valid request" {
            let result = validateManualTransaction today validRequest
            Expect.isOk result "Valid request should pass validation"
        }

        test "rejects zero amount" {
            let result = validateManualTransaction today { validRequest with Amount = 0m }
            Expect.isError result "Zero amount must be rejected"
        }

        test "rejects negative amount" {
            // The sign comes from IsOutflow; raw negative input indicates a client bug
            let result = validateManualTransaction today { validRequest with Amount = -5m }
            Expect.isError result "Negative amount must be rejected"
        }

        test "rejects unreasonably large amount" {
            let result = validateManualTransaction today { validRequest with Amount = 2_000_000m }
            Expect.isError result "Amounts above 1 million must be rejected"
        }

        test "accepts empty payee" {
            // Payee is optional — YNAB allows payee-less transactions
            let result = validateManualTransaction today { validRequest with PayeeName = "" }
            Expect.isOk result "Empty payee must be accepted"
        }

        test "accepts whitespace payee" {
            let result = validateManualTransaction today { validRequest with PayeeName = "   " }
            Expect.isOk result "Whitespace payee must be accepted (treated as no payee)"
        }

        test "rejects overlong payee" {
            let result = validateManualTransaction today { validRequest with PayeeName = String('x', 201) }
            Expect.isError result "Payee > 200 chars must be rejected"
        }

        test "rejects future date" {
            // YNAB rejects future-dated transactions; we catch this at the API boundary
            let result = validateManualTransaction today { validRequest with Date = today.AddDays(1.0) }
            Expect.isError result "Future date must be rejected"
        }

        test "accepts today" {
            let result = validateManualTransaction today { validRequest with Date = today }
            Expect.isOk result "Today must be accepted"
        }

        test "rejects date older than five years" {
            let result = validateManualTransaction today { validRequest with Date = today.AddYears(-6) }
            Expect.isError result "Very old dates must be rejected"
        }

        test "rejects memo longer than 500 characters" {
            let result = validateManualTransaction today { validRequest with Memo = Some (String('x', 501)) }
            Expect.isError result "Memo > 500 chars must be rejected"
        }

        test "accepts memo of exactly 500 characters" {
            let result = validateManualTransaction today { validRequest with Memo = Some (String('x', 500)) }
            Expect.isOk result "Memo of 500 chars must be accepted"
        }

        test "collects multiple errors" {
            let result = validateManualTransaction today { validRequest with Amount = 0m; Memo = Some (String('x', 501)) }
            match result with
            | Error errors -> Expect.equal errors.Length 2 "Both amount and memo errors should be reported"
            | Ok _ -> failtest "Expected validation errors"
        }

        // Bug infrastructure-p747v: die Prüfung las `DateTime.Today`. Im UTC-Container war um
        // 00:30 Uhr Berliner Zeit noch der Vortag, eine Buchung von heute galt als Zukunft.
        test "kurz nach Mitternacht in Berlin gilt der Berliner Tag, auch wenn UTC noch im Vortag liegt" {
            let result =
                validateManualTransaction berlinDayAtHalfPastMidnight { validRequest with Date = DateTime(2026, 11, 2) }
            Expect.isOk result "Eine Buchung von heute ist keine Zukunft"
        }

        test "kurz nach Mitternacht in Berlin bleibt der Folgetag Zukunft" {
            let result =
                validateManualTransaction berlinDayAtHalfPastMidnight { validRequest with Date = DateTime(2026, 11, 3) }
            Expect.equal result (Error [ "Date cannot be in the future" ]) "Morgen bleibt abgelehnt"
        }

        test "die Grenze von fünf Jahren zählt ab dem Berliner Tag" {
            let boundary = DateTime(2021, 11, 2)
            Expect.isOk (validateManualTransaction berlinDayAtHalfPastMidnight { validRequest with Date = boundary }) "genau fünf Jahre"
            let result = validateManualTransaction berlinDayAtHalfPastMidnight { validRequest with Date = boundary.AddDays -1.0 }
            Expect.equal result (Error [ "Date is too far in the past" ]) "ein Tag mehr"
        }
    ]

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

        test "eine übergebene import_id steht im Rumpf" {
            // ledger-ay81j: Comdirect-Importe gehen mit import_id; ohne fehlt der Schlüssel (Test darunter).
            let account = YnabAccountId (Guid.NewGuid())
            let withId = buildManualTransactionBodyWith (Some "BB:ref-1") account validRequest
            Expect.equal (decodeAt [ "transaction"; "import_id" ] Decode.string withId) "BB:ref-1" "mit import_id"
        }

        test "eine Buchung ohne import_id geht ohne den Schlüssel hinaus" {
            // Buchungen, die vor ledger-p3vq8 ohne import_id gepusht wurden, senden weiter ohne sie.
            let json = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) validRequest
            Expect.isFalse (transactionKeys json |> List.contains "import_id") "import_id abwesend"
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

        test "ein Übertrag geht mit payee_id der Übertrags-Payee, ohne category_id und payee_name" {
            // ledger-at63w: eine erfasste Buchung auf ein anderes Konto. Auch wenn der Request eine
            // Kategorie und einen Payee trägt, geht nur die Übertrags-Payee hinaus.
            let request = { validRequest with CategoryId = Some (YnabCategoryId (Guid.NewGuid())) }
            let json = buildNewTransactionBody (Some "BB:x") None (Some "payee-tagesgeld") (YnabAccountId (Guid.NewGuid())) request
            Expect.equal (decodeAt [ "transaction"; "payee_id" ] Decode.string json) "payee-tagesgeld" "payee_id"
            let keys = transactionKeys json
            Expect.isFalse (keys |> List.contains "category_id") "keine category_id"
            Expect.isFalse (keys |> List.contains "payee_name") "kein payee_name"
        }

        test "manual entries are uncleared" {
            // Project convention: imported/created transactions must never be cleared
            let json = buildManualTransactionBody (YnabAccountId (Guid.NewGuid())) validRequest
            let cleared = decodeAt [ "transaction"; "cleared" ] Decode.string json
            Expect.equal cleared "uncleared" "Cleared should be 'uncleared'"
        }
    ]

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
        test "rundet einen genauen halben Cent kaufmännisch auf" {
            // Bug-Regression (Bestandsreviews budgeting Gruppe 3 und ledger Gruppe 3): `Math.Round`
            // ohne Modus rundete zur geraden Zahl, eine bestätigte Vorbelegung schickte 12,34 weiter.
            Expect.equal (formatAmountForInput 12.345m) "12,35" "12.345 → 12,35"
            Expect.equal (formatAmountForInput 0.005m) "0,01" "0.005 → 0,01"
        }
    ]

[<Tests>]
let tests =
    testList "Manuelle Buchung" [
        milliunitTests
        formatAmountTests
        validationTests
        jsonBodyTests
        parseAmountTests
    ]
