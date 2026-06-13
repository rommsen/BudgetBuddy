module Tests.SplitPushTests

// Tests for the YNAB push of split transactions with transfer lines (ADR 0006):
// - Server.YnabClient.encodeSubtransaction        (JSON contract: key presence/absence, amount)
// - Server.YnabClient.transferPayeeByAccount      (join on TransferAccountId, never name)
// - Server.YnabClient.resolveSubtransaction       (pure resolution against a payee Map)
// - Server.YnabClient.resolveSplits / buildTransactionRequest (per-transaction reject)
// - Server.YnabClient.batchHasTransferLine        (fetch-at-most-once gate)
//
// The Fable.Remoting proxy is not .NET-testable, so we assert directly on the
// encoder/builder output (mirrors QuickAddTests' encoder idiom).

open System
open Expecto
open Shared.Domain
open Server.YnabClient
open Thoth.Json.Net

// ============================================
// Helpers
// ============================================

let private decodeAt (path: string list) (decoder: Decoder<'T>) (json: string) : 'T =
    match Decode.fromString (Decode.at path decoder) json with
    | Ok value -> value
    | Error err -> failwith $"JSON decode failed at %A{path}: {err}"

let private subKeys (json: string) : string list =
    match Decode.fromString Decode.keys json with
    | Ok keys -> keys
    | Error err -> failwith $"JSON keys decode failed: {err}"

let private encodeToString (sub: YnabSubtransactionRequest) : string =
    encodeSubtransaction sub |> Encode.toString 0

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private bankTx (id: string) (amount: decimal) : BankTransaction =
    { Id = TransactionId id
      BookingDate = DateTime.Today
      Amount = eur amount
      Payee = Some "Edeka"
      Memo = "Einkauf"
      Reference = $"REF-{id}"
      RawData = "{}" }

let private splitTx (id: string) (amount: decimal) (splits: TransactionSplit list) : SyncTransaction =
    { Transaction = bankTx id amount
      Status = ManualCategorized
      CategoryId = None
      CategoryName = None
      MatchedRuleId = None
      PayeeOverride = None
      ExternalLinks = []
      UserNotes = None
      DuplicateStatus = NotDuplicate (emptyDetectionDetails $"REF-{id}")
      YnabImportStatus = NotAttempted
      Splits = Some splits
      SuggestedByOrderId = None }

let private categorySplit (amount: decimal) : TransactionSplit =
    { Target = ToCategory (YnabCategoryId (Guid.NewGuid()), "Groceries")
      Amount = eur amount
      Memo = None }

let private transferSplit (accountId: YnabAccountId) (amount: decimal) : TransactionSplit =
    { Target = ToTransfer (accountId, "Cash")
      Amount = eur amount
      Memo = None }

// ============================================
// encodeSubtransaction — JSON contract
// ============================================

[<Tests>]
let encoderTests =
    testList "encodeSubtransaction (YNAB JSON contract)" [
        test "transfer line serializes with payee_id and WITHOUT category_id" {
            // ADR 0006: a transfer subtransaction is encoded via the transfer payee_id;
            // category_id must be omitted entirely (not sent as null).
            let payeeGuid = Guid.NewGuid()
            let json = encodeToString (TransferSub (-200000, payeeGuid.ToString(), None))
            let keys = subKeys json
            Expect.isTrue (keys |> List.contains "payee_id") "payee_id must be present"
            Expect.isFalse (keys |> List.contains "category_id") "category_id must be ABSENT for transfer lines"
            let payeeId = decodeAt [ "payee_id" ] Decode.string json
            Expect.equal payeeId (payeeGuid.ToString()) "payee_id should be the resolved transfer payee"
        }

        test "transfer line amount is a signed JSON number in milliunits" {
            // Guards the class of bug where amounts were serialized as strings.
            let json = encodeToString (TransferSub (-200000, Guid.NewGuid().ToString(), None))
            let amount = decodeAt [ "amount" ] Decode.int json
            Expect.equal amount -200000 "Amount should be a signed milliunit JSON number"
        }

        test "category line serializes with category_id and WITHOUT payee_id (no regression)" {
            let catGuid = Guid.NewGuid()
            let json = encodeToString (CategorySub (-17000, catGuid.ToString(), None))
            let keys = subKeys json
            Expect.isTrue (keys |> List.contains "category_id") "category_id must be present"
            Expect.isFalse (keys |> List.contains "payee_id") "payee_id must be absent for category lines"
            let catId = decodeAt [ "category_id" ] Decode.string json
            Expect.equal catId (catGuid.ToString()) "category_id should match"
        }

        test "category line amount is a signed JSON number in milliunits" {
            let json = encodeToString (CategorySub (-17000, Guid.NewGuid().ToString(), None))
            let amount = decodeAt [ "amount" ] Decode.int json
            Expect.equal amount -17000 "Amount should be a signed milliunit JSON number"
        }

        test "memo is included when present and omitted when None (both target kinds)" {
            let catWithMemo = encodeToString (CategorySub (-17000, Guid.NewGuid().ToString(), Some "Brot"))
            let transferNoMemo = encodeToString (TransferSub (-200000, Guid.NewGuid().ToString(), None))
            Expect.equal (decodeAt [ "memo" ] Decode.string catWithMemo) "Brot" "Memo should be present"
            Expect.isFalse (subKeys transferNoMemo |> List.contains "memo") "Memo must be absent when None"
        }
    ]

// ============================================
// transferPayeeByAccount — join on TransferAccountId
// ============================================

[<Tests>]
let payeeMapTests =
    testList "transferPayeeByAccount" [
        test "maps account id to payee id for transfer payees, joining on TransferAccountId" {
            let cashAccount = YnabAccountId (Guid.NewGuid())
            let transferPayeeId = YnabPayeeId (Guid.NewGuid())
            let payees =
                [ { Id = transferPayeeId; Name = "Transfer : Cash"; TransferAccountId = Some cashAccount }
                  { Id = YnabPayeeId (Guid.NewGuid()); Name = "Edeka"; TransferAccountId = None } ]
            let map = transferPayeeByAccount payees
            Expect.equal (Map.tryFind cashAccount map) (Some transferPayeeId) "Should map the cash account to its transfer payee"
            Expect.equal map.Count 1 "Non-transfer payees must not appear in the map"
        }
    ]

// ============================================
// resolveSubtransaction / resolveSplits — pure resolution
// ============================================

[<Tests>]
let resolutionTests =
    testList "resolveSubtransaction / resolveSplits (pure)" [
        test "resolves a transfer line against the payee map" {
            let cashAccount = YnabAccountId (Guid.NewGuid())
            let payeeGuid = Guid.NewGuid()
            let map = Map.ofList [ cashAccount, YnabPayeeId payeeGuid ]
            match resolveSubtransaction map (transferSplit cashAccount -200.00m) with
            | Ok (TransferSub (amount, payeeId, _)) ->
                Expect.equal amount -200000 "Milliunits with sign"
                Expect.equal payeeId (payeeGuid.ToString()) "Resolved transfer payee id"
            | other -> failtest $"Expected Ok TransferSub, got %A{other}"
        }

        test "rejects a transfer line with no transfer payee for the destination account" {
            // ADR 0006: missing transfer payee → reject (Error accountId), never guess.
            let cashAccount = YnabAccountId (Guid.NewGuid())
            match resolveSubtransaction Map.empty (transferSplit cashAccount -200.00m) with
            | Error acc -> Expect.equal acc cashAccount "Should report the unresolvable account"
            | Ok _ -> failtest "Expected rejection for a missing transfer payee"
        }

        test "a category line never consults the payee map" {
            match resolveSubtransaction Map.empty (categorySplit -17.00m) with
            | Ok (CategorySub _) -> ()
            | other -> failtest $"Expected Ok CategorySub without payee lookup, got %A{other}"
        }

        test "resolveSplits fails the whole set if any transfer line is unresolvable" {
            let cashAccount = YnabAccountId (Guid.NewGuid())
            let splits = [ categorySplit -17.00m; transferSplit cashAccount -200.00m ]
            match resolveSplits Map.empty splits with
            | Error acc -> Expect.equal acc cashAccount "Should report the unresolvable account"
            | Ok _ -> failtest "Expected rejection"
        }
    ]

// ============================================
// buildTransactionRequest — per-transaction reject + batch gate
// ============================================

[<Tests>]
let buildRequestTests =
    testList "buildTransactionRequest / batchHasTransferLine" [
        test "a split tx with an unresolvable transfer is rejected (Error txAccount)" {
            // The rejected transaction is excluded from the body and reported back so
            // the caller marks it RejectedByYnab (UnknownRejection ...).
            let cashAccount = YnabAccountId (Guid.NewGuid())
            let importAccount = YnabAccountId (Guid.NewGuid())
            let tx = splitTx "tx-cb" -217.00m [ categorySplit -17.00m; transferSplit cashAccount -200.00m ]
            match buildTransactionRequest importAccount Map.empty "BB:tx-cb" tx with
            | Error acc -> Expect.equal acc cashAccount "Should report the unresolvable transfer account"
            | Ok _ -> failtest "Expected the transaction to be rejected"
        }

        test "a split tx with a resolvable transfer builds subtransactions, no parent category_id" {
            let cashAccount = YnabAccountId (Guid.NewGuid())
            let importAccount = YnabAccountId (Guid.NewGuid())
            let map = Map.ofList [ cashAccount, YnabPayeeId (Guid.NewGuid()) ]
            let tx = splitTx "tx-ok" -217.00m [ categorySplit -17.00m; transferSplit cashAccount -200.00m ]
            match buildTransactionRequest importAccount map "BB:tx-ok" tx with
            | Ok request ->
                Expect.isNone request.CategoryId "Parent of a split has no category_id"
                match request.Subtransactions with
                | Some subs -> Expect.equal subs.Length 2 "Should carry two subtransactions"
                | None -> failtest "Expected subtransactions"
            | Error _ -> failtest "Expected the transaction to build"
        }

        test "batchHasTransferLine is false for category-only batches (skips payee fetch)" {
            let catOnly = splitTx "tx-cat" -100.00m [ categorySplit -60.00m; categorySplit -40.00m ]
            Expect.isFalse (batchHasTransferLine [ catOnly ]) "Category-only batch needs no payee fetch"
        }

        test "batchHasTransferLine is true when any transaction has a transfer line" {
            let cashAccount = YnabAccountId (Guid.NewGuid())
            let catOnly = splitTx "tx-cat" -100.00m [ categorySplit -60.00m; categorySplit -40.00m ]
            let withTransfer = splitTx "tx-cb" -217.00m [ categorySplit -17.00m; transferSplit cashAccount -200.00m ]
            Expect.isTrue (batchHasTransferLine [ catOnly; withTransfer ]) "A batch with any transfer line needs the payee fetch"
        }
    ]
