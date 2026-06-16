module Tests.SplitEditorTests

// Pure-logic tests for the split-review editor (ynab-002 / ynab-003).
// CONVENTION: the user types POSITIVE magnitudes; the editor applies the
// transaction's sign (Total is negative for an outflow) when building the
// domain `TransactionSplit`s. All validation delegates to the shared ynab-001
// helpers (mkSplits / splitRemainder) — the invariant is never reimplemented
// here (ADR 0006). These fixtures deliberately use POSITIVE input text against
// a NEGATIVE total, mirroring real usage (the ynab-002 fixtures wrongly typed
// negative text and so masked the sign bug — ynab-003).

open System
open Expecto
open Shared.Domain
open Components.SyncFlow.Types

// ============================================
// Helpers
// ============================================

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }
let private catId () = YnabCategoryId (Guid.NewGuid())
let private accId () = YnabAccountId (Guid.NewGuid())

/// A draft line with a category target and the given (positive) amount text.
let private categoryLine (name: string) (amountText: string) : SplitDraftLine =
    { Target = Some (ToCategory (catId (), name)); AmountText = amountText; Memo = "" }

/// A draft line with a transfer-account target and the given (positive) amount text.
let private transferLine (name: string) (amountText: string) : SplitDraftLine =
    { Target = Some (ToTransfer (accId (), name)); AmountText = amountText; Memo = "" }

let private mkState (total: decimal) (lines: SplitDraftLine list) : SplitEditState =
    { TransactionId = TransactionId "tx-1"
      Total = eur total
      Lines = lines
      Currency = "EUR"
      ActivePicker = NoPicker }

// ============================================
// parseSplitAmount
// ============================================

[<Tests>]
let parseSplitAmountTests =
    testList "parseSplitAmount" [
        testCase "blank input parses as 0 (unfilled line contributes nothing)" <| fun () ->
            Expect.equal (parseSplitAmount "") (Some 0m) "Empty should be 0"
            Expect.equal (parseSplitAmount "   ") (Some 0m) "Whitespace should be 0"

        testCase "accepts German comma decimals" <| fun () ->
            Expect.equal (parseSplitAmount "17,50") (Some 17.50m) "Comma decimal"

        testCase "accepts dot decimals" <| fun () ->
            Expect.equal (parseSplitAmount "17.50") (Some 17.50m) "Dot decimal"

        testCase "rejects malformed input" <| fun () ->
            Expect.equal (parseSplitAmount "abc") None "Letters are malformed"
    ]

// ============================================
// Sign application (ynab-003): the user types a positive magnitude; the
// committed split adopts the TOTAL's sign so the signed sum can match.
// ============================================

[<Tests>]
let signApplicationTests =
    testList "draftLineToSplit sign application" [
        testCase "positive input adopts the negative total's sign (outflow)" <| fun () ->
            // The reported bug: typing 200 against a -222,15 total. The committed
            // amount MUST be -200 — typing positive previously made magnitudes ADD.
            let state = mkState -222.15m [ transferLine "Bargeld" "200" ]
            let split = committedSplits state |> List.exactlyOne
            Expect.equal split.Amount.Amount -200.00m "Positive input becomes a signed outflow"

        testCase "positive input stays positive for an inflow total" <| fun () ->
            let state = mkState 222.15m [ categoryLine "Erstattung" "200" ]
            let split = committedSplits state |> List.exactlyOne
            Expect.equal split.Amount.Amount 200.00m "Positive total keeps lines positive"
    ]

// ============================================
// Regression for the reported cashback bug (Roman, 2026-06-16): 222,15 total,
// 200 cash. Used to read 422,15 (magnitudes added). Must verrechnen to 22,15.
// ============================================

[<Tests>]
let cashbackSignRegressionTests =
    testList "cashback split with positive input (ynab-003 regression)" [
        testCase "222,15 total: 200 cash + 22,15 category balances (not 422,15)" <| fun () ->
            let state =
                mkState -222.15m
                    [ categoryLine "Essen" "22,15"
                      transferLine "Bargeld" "200" ]
            Expect.equal (splitEditRemainder state) 0.00m "Magnitudes verrechnen to zero"
            Expect.isTrue (canSaveSplits state) "Balanced split unlocks Save"
            match validateSplitEdit state with
            | Ok splits ->
                let cat = splits |> List.find (fun s -> match s.Target with ToCategory _ -> true | _ -> false)
                let tr  = splits |> List.find (fun s -> match s.Target with ToTransfer _ -> true | _ -> false)
                Expect.equal cat.Amount.Amount -22.15m "Category committed as a signed outflow"
                Expect.equal tr.Amount.Amount -200.00m "Transfer committed as a signed outflow"
            | other -> failtest $"Expected Ok, got %A{other}"

        testCase "before allocating the category, 200 cash leaves 22,15 unallocated" <| fun () ->
            let state = mkState -222.15m [ categoryLine "Essen" ""; transferLine "Bargeld" "200" ]
            Expect.equal (splitEditRemainder state) 22.15m "Unallocated remainder is the leftover magnitude"
            Expect.isFalse (canSaveSplits state) "Unbalanced split keeps Save locked"
    ]

// ============================================
// Per-line "Rest" button (ynab-003): fills a line with the leftover magnitude.
// ============================================

[<Tests>]
let restButtonTests =
    testList "restMagnitudeForLine (per-line Rest button)" [
        testCase "fills the category with the leftover after the cash line" <| fun () ->
            let state = mkState -222.15m [ categoryLine "Essen" ""; transferLine "Bargeld" "200" ]
            Expect.equal (restMagnitudeForLine 0 state) 22.15m "Rest(category) = |total| - cash"

        testCase "fills the cash line with the leftover after the category" <| fun () ->
            let state = mkState -222.15m [ categoryLine "Essen" "22,15"; transferLine "Bargeld" "" ]
            Expect.equal (restMagnitudeForLine 1 state) 200.00m "Rest(transfer) = |total| - category"

        testCase "rest is clamped at zero when other lines already over-allocate" <| fun () ->
            let state = mkState -100.00m [ categoryLine "A" "120"; categoryLine "B" "" ]
            Expect.equal (restMagnitudeForLine 1 state) 0.00m "Never suggests a negative rest"

        testCase "applying the rest balances the split and unlocks Save" <| fun () ->
            let state = mkState -222.15m [ categoryLine "Essen" ""; transferLine "Bargeld" "200" ]
            let filled = formatAmountForEdit (restMagnitudeForLine 0 state)
            let balanced = mkState -222.15m [ categoryLine "Essen" filled; transferLine "Bargeld" "200" ]
            Expect.equal (splitEditRemainder balanced) 0.00m "Filling the rest zeroes the remainder"
            Expect.isTrue (canSaveSplits balanced) "And unlocks Save"
    ]

// ============================================
// Live remainder preview (AC) — positive magnitude, 0 when balanced.
// ============================================

[<Tests>]
let splitEditRemainderTests =
    testList "splitEditRemainder (live magnitude preview)" [
        testCase "empty lines leave the full total magnitude" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" ""; transferLine "Bargeld" "" ]
            Expect.equal (splitEditRemainder state) 217.00m "Nothing entered → full total"

        testCase "fully balanced split leaves a zero remainder" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" "17"; transferLine "Bargeld" "200" ]
            Expect.equal (splitEditRemainder state) 0.00m "17 + 200 = 217"

        testCase "over-allocation yields a negative remainder" <| fun () ->
            let state = mkState -100.00m [ categoryLine "A" "60"; categoryLine "B" "60" ]
            Expect.equal (splitEditRemainder state) -20.00m "120 against 100 → -20"
    ]

// ============================================
// Save-locked-on-mismatch — delegates to mkSplits (positive input model).
// ============================================

[<Tests>]
let canSaveSplitsTests =
    testList "canSaveSplits (Save gating)" [
        testCase "Save is LOCKED while the sum does not match the total" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" "17"; transferLine "Bargeld" "" ]
            Expect.isFalse (canSaveSplits state) "Mismatched sum must lock Save"

        testCase "Save is UNLOCKED once the sum matches the total" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" "17"; transferLine "Bargeld" "200" ]
            Expect.isTrue (canSaveSplits state) "Balanced split must unlock Save"

        testCase "Save is LOCKED with fewer than two lines" <| fun () ->
            let state = mkState -200.00m [ transferLine "Bargeld" "200" ]
            Expect.isFalse (canSaveSplits state) "A single line is not a split"

        testCase "Save is LOCKED while a line has no chosen target" <| fun () ->
            let untargeted = { Target = None; AmountText = "200"; Memo = "" }
            let state = mkState -217.00m [ categoryLine "Groceries" "17"; untargeted ]
            Expect.isFalse (canSaveSplits state) "An untargeted line must lock Save"

        testCase "Save is UNLOCKED for a valid three-line split" <| fun () ->
            let state =
                mkState -100.00m
                    [ categoryLine "A" "30"
                      categoryLine "B" "20"
                      transferLine "Bargeld" "50" ]
            Expect.isTrue (canSaveSplits state) "Balanced N-line split unlocks Save"
    ]

// ============================================
// validateSplitEdit surfaces the exact SplitError (drives the Save toast)
// ============================================

[<Tests>]
let validateSplitEditTests =
    testList "validateSplitEdit" [
        testCase "reports SumMismatch when lines do not add up" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" "17"; transferLine "Bargeld" "100" ]
            match validateSplitEdit state with
            | Error (SumMismatch (expected, actual)) ->
                Expect.equal expected -217.00m "Expected is the total"
                Expect.equal actual -117.00m "Actual is the signed line sum"
            | other -> failtest $"Expected SumMismatch, got %A{other}"

        testCase "returns the validated splits on a balanced split" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" "17"; transferLine "Bargeld" "200" ]
            match validateSplitEdit state with
            | Ok splits -> Expect.equal splits.Length 2 "Two validated lines"
            | other -> failtest $"Expected Ok, got %A{other}"
    ]

// ============================================
// formatAmountForEdit emits a positive magnitude (the user-facing convention)
// ============================================

[<Tests>]
let formatAmountForEditTests =
    testList "formatAmountForEdit" [
        testCase "emits a positive magnitude even for a signed amount" <| fun () ->
            Expect.equal (formatAmountForEdit -200.00m) "200.00" "Sign dropped for display"
            Expect.equal (parseSplitAmount (formatAmountForEdit -200.00m)) (Some 200.00m) "Round-trips as a magnitude"

        testCase "zero formats as empty string" <| fun () ->
            Expect.equal (formatAmountForEdit 0m) "" "Zero is empty"
    ]
