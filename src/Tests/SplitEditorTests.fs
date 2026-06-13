module Tests.SplitEditorTests

// Pure-logic tests for the split-review editor (ynab-002).
// These exercise the client's split form helpers WITHOUT touching the
// Fable.Remoting proxy, so they run under .NET via Expecto. All arithmetic and
// validation delegates to the shared ynab-001 domain helpers (splitRemainder /
// mkSplits) — these tests pin the form-state adaptation, not a reimplemented
// invariant (ADR 0006).

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

/// A draft line with a category target and the given amount text.
let private categoryLine (name: string) (amountText: string) : SplitDraftLine =
    { Target = Some (ToCategory (catId (), name)); AmountText = amountText; Memo = ""; AutoRemainder = false }

/// A draft line with a transfer-account target and the given amount text.
let private transferLine (name: string) (amountText: string) : SplitDraftLine =
    { Target = Some (ToTransfer (accId (), name)); AmountText = amountText; Memo = ""; AutoRemainder = false }

/// An auto-remainder ("Rest") category line — its amount is the live remainder
/// of the other lines (the cashback category line).
let private autoCategoryLine (name: string) : SplitDraftLine =
    { Target = Some (ToCategory (catId (), name)); AmountText = ""; Memo = ""; AutoRemainder = true }

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

        testCase "accepts a leading minus for outflow lines" <| fun () ->
            Expect.equal (parseSplitAmount "-200,00") (Some -200.00m) "Negative comma decimal"

        testCase "rejects malformed input" <| fun () ->
            Expect.equal (parseSplitAmount "abc") None "Letters are malformed"
    ]

// ============================================
// Cashback auto-remainder line (AC 2): user types only the transfer amount,
// the category line absorbs the rest live → split balances automatically.
// ============================================

[<Tests>]
let cashbackAutoRemainderTests =
    testList "cashback auto-remainder line" [
        testCase "category auto-line absorbs the rest when only the transfer is entered" <| fun () ->
            // €217 total; user enters the €200 cash withdrawal on the transfer
            // line; the category 'Rest' line should commit at €17 (the purchase).
            let state =
                mkState -217.00m
                    [ autoCategoryLine "Groceries"
                      transferLine "Bargeld" "-200,00" ]
            let splits = committedSplits state
            Expect.equal splits.Length 2 "Both lines commit"
            let categorySplit = splits |> List.find (fun s -> match s.Target with ToCategory _ -> true | _ -> false)
            Expect.equal categorySplit.Amount.Amount -17.00m "Category auto-line absorbs the remainder (-17)"

        testCase "auto-remainder split balances → remainder is zero, Save unlocked" <| fun () ->
            let state =
                mkState -217.00m
                    [ autoCategoryLine "Groceries"
                      transferLine "Bargeld" "-200,00" ]
            Expect.equal (splitEditRemainder state) 0.00m "Auto line drives the remainder to zero"
            Expect.isTrue (canSaveSplits state) "A balanced cashback split must unlock Save"

        testCase "changing only the transfer amount re-balances the category live" <| fun () ->
            let state =
                mkState -217.00m
                    [ autoCategoryLine "Groceries"
                      transferLine "Bargeld" "-150,00" ]
            let categorySplit =
                committedSplits state
                |> List.find (fun s -> match s.Target with ToCategory _ -> true | _ -> false)
            Expect.equal categorySplit.Amount.Amount -67.00m "Category re-absorbs the new remainder (-67)"
            Expect.isTrue (canSaveSplits state) "Still balanced after the transfer change"
    ]

// ============================================
// Live remainder preview (AC 3) — delegates to splitRemainder
// ============================================

[<Tests>]
let splitEditRemainderTests =
    testList "splitEditRemainder (live preview)" [
        testCase "cashback shape: €17 category entered against €217 total leaves €200" <| fun () ->
            // Only the category line is filled; the transfer line is still blank,
            // so the live remainder shows the cash withdrawal yet to be allocated.
            let state = mkState -217.00m [ categoryLine "Groceries" "-17,00"; transferLine "Bargeld" "" ]
            Expect.equal (splitEditRemainder state) -200.00m
                "Remainder should be the unentered transfer amount (-200)"

        testCase "fully balanced split leaves a zero remainder" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" "-17,00"; transferLine "Bargeld" "-200,00" ]
            Expect.equal (splitEditRemainder state) 0.00m "Balanced split leaves 0"

        testCase "remainder equals the whole total when nothing is entered" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" ""; transferLine "Bargeld" "" ]
            Expect.equal (splitEditRemainder state) -217.00m "Empty lines leave the full total"

        testCase "over-allocation yields a remainder of opposite sign" <| fun () ->
            let state = mkState -100.00m [ categoryLine "A" "-60,00"; categoryLine "B" "-60,00" ]
            Expect.equal (splitEditRemainder state) 20.00m "Over-allocation flips the remainder sign"
    ]

// ============================================
// Save-locked-on-mismatch (AC 3) — delegates to mkSplits
// ============================================

[<Tests>]
let canSaveSplitsTests =
    testList "canSaveSplits (Save gating)" [
        testCase "Save is LOCKED while the sum does not match the total" <| fun () ->
            // €17 + €0 against €217 → mismatch → Save disabled.
            let state = mkState -217.00m [ categoryLine "Groceries" "-17,00"; transferLine "Bargeld" "" ]
            Expect.isFalse (canSaveSplits state) "Mismatched sum must lock Save"

        testCase "Save is UNLOCKED once the sum matches the total" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" "-17,00"; transferLine "Bargeld" "-200,00" ]
            Expect.isTrue (canSaveSplits state) "Balanced split must unlock Save"

        testCase "Save is LOCKED with fewer than two lines" <| fun () ->
            let state = mkState -200.00m [ transferLine "Bargeld" "-200,00" ]
            Expect.isFalse (canSaveSplits state) "A single line is not a split"

        testCase "Save is LOCKED while a line has no chosen target" <| fun () ->
            // Amounts balance, but one line still lacks a category/account → not saveable.
            let untargeted = { Target = None; AmountText = "-200,00"; Memo = ""; AutoRemainder = false }
            let state = mkState -217.00m [ categoryLine "Groceries" "-17,00"; untargeted ]
            Expect.isFalse (canSaveSplits state) "An untargeted line must lock Save"

        testCase "Save is UNLOCKED for a valid three-line split" <| fun () ->
            let state =
                mkState -100.00m
                    [ categoryLine "A" "-30,00"
                      categoryLine "B" "-20,00"
                      transferLine "Bargeld" "-50,00" ]
            Expect.isTrue (canSaveSplits state) "Balanced N-line split unlocks Save"
    ]

// ============================================
// validateSplitEdit surfaces the exact SplitError (drives the Save toast)
// ============================================

[<Tests>]
let validateSplitEditTests =
    testList "validateSplitEdit" [
        testCase "reports SumMismatch when lines do not add up" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" "-17,00"; transferLine "Bargeld" "-100,00" ]
            match validateSplitEdit state with
            | Error (SumMismatch (expected, actual)) ->
                Expect.equal expected -217.00m "Expected is the total"
                Expect.equal actual -117.00m "Actual is the line sum"
            | other -> failtest $"Expected SumMismatch, got %A{other}"

        testCase "returns the validated splits on a balanced split" <| fun () ->
            let state = mkState -217.00m [ categoryLine "Groceries" "-17,00"; transferLine "Bargeld" "-200,00" ]
            match validateSplitEdit state with
            | Ok splits -> Expect.equal splits.Length 2 "Two validated lines"
            | other -> failtest $"Expected Ok, got %A{other}"
    ]

// ============================================
// formatAmountForEdit round-trips through parseSplitAmount
// ============================================

[<Tests>]
let formatAmountForEditTests =
    testList "formatAmountForEdit" [
        testCase "negative amount round-trips through parseSplitAmount" <| fun () ->
            let text = formatAmountForEdit -200.00m
            Expect.equal (parseSplitAmount text) (Some -200.00m) "Round-trip negative"

        testCase "zero formats as empty string" <| fun () ->
            Expect.equal (formatAmountForEdit 0m) "" "Zero is empty"
    ]
