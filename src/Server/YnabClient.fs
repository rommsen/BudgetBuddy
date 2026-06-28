module Server.YnabClient

open System
open FsHttp
open Thoth.Json.Net
open Shared.Domain

// ============================================
// YNAB API Configuration
// ============================================

let private baseUrl = "https://api.youneedabudget.com/v1"

// ============================================
// JSON Decoders
// ============================================

module Decoders =
    let budgetDecoder : Decoder<YnabBudget> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" Decode.string |> YnabBudgetId
            Name = get.Required.Field "name" Decode.string
        })

    let accountDecoder : Decoder<YnabAccount> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" Decode.guid |> YnabAccountId
            Name = get.Required.Field "name" Decode.string
            Balance = {
                Amount = get.Required.Field "balance" Decode.int64 |> decimal |> fun milliunits -> milliunits / 1000m
                Currency = "EUR"  // YNAB uses milliunits, currency is implicit
            }
            // on_budget / closed drive the split transfer-target filter (ynab-002).
            // Optional with Conformist-safe defaults: if YNAB ever omits a flag we
            // assume a normal open on-budget account rather than failing the load.
            OnBudget = get.Optional.Field "on_budget" Decode.bool |> Option.defaultValue true
            Closed = get.Optional.Field "closed" Decode.bool |> Option.defaultValue false
        })

    /// Reads YNAB's `balance` (milliunits) as the category's Available Money.
    /// Optional: a category that ships without a `balance` (e.g. an internal/special
    /// category) decodes to `None` (no value shown) rather than a misleading 0 or a
    /// failed load. Conversion is the same milliunits/1000 path as the account balance.
    let private categoryAvailable (get: Decode.IGetters) : Money option =
        get.Optional.Field "balance" Decode.int64
        |> Option.map (fun milliunits -> { Amount = decimal milliunits / 1000m; Currency = "EUR" })

    /// Decodes the month's real "Ready to Assign" (YNAB `to_be_budgeted`, milliunits)
    /// from GET /budgets/{id}/months/current as Money. This is the authoritative
    /// Ready-to-Assign, unlike the internal Inflow category's own `balance`.
    let monthToBeBudgetedDecoder : Decoder<Money> =
        Decode.field "data" (Decode.field "month" (Decode.field "to_be_budgeted" Decode.int64))
        |> Decode.map (fun milliunits -> { Amount = decimal milliunits / 1000m; Currency = "EUR" })

    /// Decoder for categories when category_group_name is present (from /categories endpoint)
    let categoryDecoder : Decoder<YnabCategory> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" Decode.guid |> YnabCategoryId
            Name = get.Required.Field "name" Decode.string
            GroupName = get.Required.Field "category_group_name" Decode.string
            Available = categoryAvailable get
        })

    let payeeDecoder : Decoder<YnabPayee> =
        Decode.object (fun get ->
            let isDeleted = get.Optional.Field "deleted" Decode.bool |> Option.defaultValue false
            {
                Id = get.Required.Field "id" Decode.guid |> YnabPayeeId
                Name = get.Required.Field "name" Decode.string
                TransferAccountId = get.Optional.Field "transfer_account_id" Decode.guid |> Option.map YnabAccountId
            })

    /// Decoder for categories nested within category_groups (from /budgets/{id} endpoint)
    let categoryInGroupDecoder (groupName: string) : Decoder<YnabCategory> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" Decode.guid |> YnabCategoryId
            Name = get.Required.Field "name" Decode.string
            GroupName = groupName
            Available = categoryAvailable get
        })

    /// Decoder for category groups containing nested categories
    /// Note: Some category groups (like "Internal Master Category") may not have a categories field
    let categoryGroupsDecoder : Decoder<YnabCategory list> =
        Decode.list (
            Decode.object (fun get ->
                let groupName = get.Required.Field "name" Decode.string
                // Categories field may be missing for internal/special category groups
                let categories = get.Optional.Field "categories" (Decode.list (categoryInGroupDecoder groupName))
                categories |> Option.defaultValue []
            )
        )
        |> Decode.map List.concat

    let budgetDetailDecoder : Decoder<YnabBudgetWithAccounts> =
        Decode.object (fun get -> {
            Budget = {
                Id = get.Required.Field "id" Decode.string |> YnabBudgetId
                Name = get.Required.Field "name" Decode.string
            }
            Accounts = get.Required.Field "accounts" (Decode.list accountDecoder)
            // Categories come from category_groups, not a flat categories list
            Categories = get.Optional.Field "category_groups" categoryGroupsDecoder |> Option.defaultValue []
        })

    /// Decoder for YNAB transactions (for duplicate detection)
    let transactionDecoder : Decoder<YnabTransaction> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" Decode.string
            Date = get.Required.Field "date" Decode.datetimeUtc
            Amount = {
                Amount = get.Required.Field "amount" Decode.int64 |> decimal |> fun milliunits -> milliunits / 1000m
                Currency = "EUR"
            }
            Payee = get.Optional.Field "payee_name" Decode.string
            Memo = get.Optional.Field "memo" Decode.string
            ImportId = get.Optional.Field "import_id" Decode.string
            CategoryId = get.Optional.Field "category_id" Decode.string
            CategoryName = get.Optional.Field "category_name" Decode.string
        })

// ============================================
// YNAB API Functions
// ============================================

/// Fetches all budgets accessible with the given token
let getBudgets (token: string) : Async<YnabResult<YnabBudget list>> =
    async {
        try
            let! response =
                http {
                    GET $"{baseUrl}/budgets"
                    Authorization $"Bearer {token}"
                }
                |> Request.sendAsync

            let! bodyText = response |> Response.toTextAsync
            let statusCode = response.statusCode |> int

            match statusCode with
            | 200 ->
                let decoder = Decode.field "data" (Decode.field "budgets" (Decode.list Decoders.budgetDecoder))
                match Decode.fromString decoder bodyText with
                | Ok budgets -> return Ok budgets
                | Error err -> return Error (YnabError.InvalidResponse $"Failed to parse budgets: {err}")

            | 401 ->
                return Error (YnabError.Unauthorized "Invalid YNAB Personal Access Token")

            | 429 ->
                return Error (YnabError.RateLimitExceeded 60)

            | _ ->
                return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")

        with
        | ex ->
            return Error (YnabError.NetworkError $"Request failed: {ex.Message}")
    }

/// Fetches detailed information for a specific budget including accounts and categories
let getBudgetWithAccounts (token: string) (YnabBudgetId budgetId: YnabBudgetId) : Async<YnabResult<YnabBudgetWithAccounts>> =
    async {
        try
            let! response =
                http {
                    GET $"{baseUrl}/budgets/{budgetId}"
                    Authorization $"Bearer {token}"
                }
                |> Request.sendAsync

            let! bodyText = response |> Response.toTextAsync
            let statusCode = response.statusCode |> int

            match statusCode with
            | 200 ->
                let decoder = Decode.field "data" (Decode.field "budget" Decoders.budgetDetailDecoder)
                match Decode.fromString decoder bodyText with
                | Ok budget -> return Ok budget
                | Error err -> return Error (YnabError.InvalidResponse $"Failed to parse budget details: {err}")

            | 401 ->
                return Error (YnabError.Unauthorized "Invalid YNAB token")

            | 404 ->
                return Error (YnabError.BudgetNotFound budgetId)

            | 429 ->
                return Error (YnabError.RateLimitExceeded 60)

            | _ ->
                return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")

        with
        | ex ->
            return Error (YnabError.NetworkError $"Failed to fetch budget details: {ex.Message}")
    }

/// Fetches the budget's real "Ready to Assign" (the current month's `to_be_budgeted`).
/// Returns `None` on any failure so a failed month fetch degrades to "no value"
/// rather than surfacing the internal Inflow category's garbage balance.
let getReadyToAssign (token: string) (YnabBudgetId budgetId: YnabBudgetId) : Async<Money option> =
    async {
        try
            let! response =
                http {
                    GET $"{baseUrl}/budgets/{budgetId}/months/current"
                    Authorization $"Bearer {token}"
                }
                |> Request.sendAsync

            let! bodyText = response |> Response.toTextAsync
            if (response.statusCode |> int) = 200 then
                match Decode.fromString Decoders.monthToBeBudgetedDecoder bodyText with
                | Ok money -> return Some money
                | Error _ -> return None
            else
                return None
        with _ -> return None
    }

/// Fetches all categories for a specific budget
let getCategories (token: string) (YnabBudgetId budgetId: YnabBudgetId) : Async<YnabResult<YnabCategory list>> =
    async {
        try
            let! response =
                http {
                    GET $"{baseUrl}/budgets/{budgetId}/categories"
                    Authorization $"Bearer {token}"
                }
                |> Request.sendAsync

            let! bodyText = response |> Response.toTextAsync
            let statusCode = response.statusCode |> int

            match statusCode with
            | 200 ->
                let decoder =
                    Decode.field "data" (
                        Decode.field "category_groups" (
                            Decode.list (
                                Decode.object (fun get ->
                                    let groupName = get.Required.Field "name" Decode.string
                                    let categories = get.Required.Field "categories" (Decode.list Decoders.categoryDecoder)
                                    // Add group name to each category
                                    categories |> List.map (fun cat -> { cat with GroupName = groupName })
                                )
                            )
                        )
                    )

                match Decode.fromString decoder bodyText with
                | Ok categoryGroups ->
                    // Flatten the category groups into a single list
                    return Ok (List.concat categoryGroups)
                | Error err ->
                    return Error (YnabError.InvalidResponse $"Failed to parse categories: {err}")

            | 401 ->
                return Error (YnabError.Unauthorized "Invalid YNAB token")

            | 404 ->
                return Error (YnabError.BudgetNotFound budgetId)

            | 429 ->
                return Error (YnabError.RateLimitExceeded 60)

            | _ ->
                return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")

        with
        | ex ->
            return Error (YnabError.NetworkError $"Failed to fetch categories: {ex.Message}")
    }

/// Fetches all payees from YNAB for the given budget.
let getPayees (token: string) (YnabBudgetId budgetId: YnabBudgetId) : Async<YnabResult<YnabPayee list>> =
    async {
        try
            let! response =
                http {
                    GET $"{baseUrl}/budgets/{budgetId}/payees"
                    Authorization $"Bearer {token}"
                }
                |> Request.sendAsync

            let! bodyText = response |> Response.toTextAsync
            let statusCode = response.statusCode |> int

            match statusCode with
            | 200 ->
                // YNAB payees response structure includes deleted field
                let payeeDecoderWithDeleted : Decoder<YnabPayee option> =
                    Decode.object (fun get ->
                        let isDeleted = get.Optional.Field "deleted" Decode.bool |> Option.defaultValue false
                        if isDeleted then
                            None
                        else
                            Some {
                                Id = get.Required.Field "id" Decode.guid |> YnabPayeeId
                                Name = get.Required.Field "name" Decode.string
                                TransferAccountId = get.Optional.Field "transfer_account_id" Decode.guid |> Option.map YnabAccountId
                            })
                
                let decoder =
                    Decode.field "data" (
                        Decode.field "payees" (Decode.list payeeDecoderWithDeleted)
                    )

                match Decode.fromString decoder bodyText with
                | Ok payeesWithNone ->
                    // Filter out deleted payees (None values) and unwrap
                    let activePayees = payeesWithNone |> List.choose id
                    return Ok activePayees
                | Error err ->
                    return Error (YnabError.InvalidResponse $"Failed to parse payees: {err}")

            | 401 ->
                return Error (YnabError.Unauthorized "Invalid YNAB token")

            | 404 ->
                return Error (YnabError.BudgetNotFound budgetId)

            | 429 ->
                return Error (YnabError.RateLimitExceeded 60)

            | _ ->
                return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")

        with
        | ex ->
            return Error (YnabError.NetworkError $"Failed to fetch payees: {ex.Message}")
    }

/// YNAB memo character limit (testing with 300, may need adjustment)
let private memoLimit = 300

/// Compresses multiple consecutive whitespace characters into a single space
let private compressWhitespace (text: string) =
    System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim()

/// Simple truncation for split memos (no reference needed - parent has it)
let private truncateSplitMemo (memo: string) =
    let compressed = compressWhitespace memo
    if compressed.Length > memoLimit then
        compressed.Substring(0, memoLimit - 3) + "..."
    else
        compressed

/// Builds memo with reference suffix for YNAB, ensuring reference is never truncated.
/// Format: "<memo>, Ref: <reference>"
/// If memo exceeds limit, truncates from the BEGINNING to preserve the reference.
let private buildMemoWithReference (memo: string) (reference: string) : string =
    let compressedMemo = compressWhitespace memo
    let suffix = $", Ref: {reference}"
    let fullMemo = $"{compressedMemo}{suffix}"

    if fullMemo.Length <= memoLimit then
        fullMemo
    else
        // Truncate from the beginning, keeping the reference intact
        // Format: "...<truncated memo>, Ref: <reference>"
        let availableForMemo = memoLimit - suffix.Length - 3  // -3 for "..."
        if availableForMemo <= 0 then
            // Reference alone is too long (unlikely), just truncate the whole thing
            fullMemo.Substring(fullMemo.Length - memoLimit)
        else
            let truncatedMemo = compressedMemo.Substring(compressedMemo.Length - availableForMemo)
            $"...{truncatedMemo}{suffix}"

// ============================================
// YNAB Transaction Types for JSON Encoding
// ============================================

/// Result of creating transactions in YNAB
type TransactionCreateResult = {
    CreatedCount: int
    DuplicateImportIds: string list
    /// Transactions excluded from the request body because a transfer split line
    /// had no resolvable transfer payee in YNAB (ADR 0006). The caller marks these
    /// RejectedByYnab (UnknownRejection ...).
    RejectedTransferTransactionIds: TransactionId list
}

/// Subtransaction for split transactions. Mirrors the domain `SplitTarget` DU so a
/// subtransaction can be either a category line (serialized with `category_id`) or a
/// transfer line (serialized with the resolved transfer `payee_id`, and crucially
/// WITHOUT a `category_id` key — ADR 0006). YNAB's SaveSubTransaction has no
/// `transfer_account_id`; a transfer is encoded solely via the transfer payee_id.
type YnabSubtransactionRequest =
    /// A category subtransaction: amount (milliunits), category id, optional memo.
    | CategorySub of amount: int * categoryId: string * memo: string option
    /// A transfer subtransaction: amount (milliunits), resolved transfer payee id,
    /// optional memo. No category_id is sent for transfer lines.
    | TransferSub of amount: int * payeeId: string * memo: string option

/// Transaction request for YNAB API
type YnabTransactionRequest = {
    AccountId: string
    Date: string
    Amount: int  // Milliunits (int32 is sufficient for amounts up to ~2.1 million EUR)
    PayeeName: string
    Memo: string
    Cleared: string
    ImportId: string
    CategoryId: string option
    Subtransactions: YnabSubtransactionRequest list option
}

/// Encoder for subtransactions.
/// Non-private so the JSON contract can be asserted directly in tests (the
/// Fable.Remoting proxy is not .NET-testable; mirror the QuickAdd encoder idiom).
/// A category line emits `category_id`; a transfer line emits the resolved
/// `payee_id` and deliberately OMITS `category_id` (ADR 0006: omit, never null).
/// `Encode.int` keeps the amount a JSON number (the prior string-amount bug).
let encodeSubtransaction (sub: YnabSubtransactionRequest) =
    match sub with
    | CategorySub (amount, categoryId, memo) ->
        Encode.object [
            "amount", Encode.int amount  // Use int for proper JSON number serialization
            "category_id", Encode.string categoryId
            match memo with
            | Some m -> "memo", Encode.string m
            | None -> ()
        ]
    | TransferSub (amount, payeeId, memo) ->
        Encode.object [
            "amount", Encode.int amount  // Use int for proper JSON number serialization
            "payee_id", Encode.string payeeId
            // No category_id key for transfer lines — YNAB infers the transfer from
            // the transfer payee_id. Sending category_id (even null) is wrong.
            match memo with
            | Some m -> "memo", Encode.string m
            | None -> ()
        ]

/// Encoder for transaction requests
let private encodeTransaction (tx: YnabTransactionRequest) =
    Encode.object [
        "account_id", Encode.string tx.AccountId
        "date", Encode.string tx.Date
        "amount", Encode.int tx.Amount  // Use int for proper JSON number serialization
        "payee_name", Encode.string tx.PayeeName
        "memo", Encode.string tx.Memo
        "cleared", Encode.string tx.Cleared
        "import_id", Encode.string tx.ImportId
        match tx.CategoryId with
        | Some catId -> "category_id", Encode.string catId
        | None -> ()
        match tx.Subtransactions with
        | Some subs when subs.Length > 0 ->
            "subtransactions", Encode.list (subs |> List.map encodeSubtransaction)
        | _ -> ()
    ]

/// Builds a `Map<YnabAccountId, YnabPayeeId>` from a payee list by joining on
/// `TransferAccountId` (NEVER on the payee name — ADR 0006). Only payees that ARE
/// transfer payees (have a `TransferAccountId`) appear in the map.
let transferPayeeByAccount (payees: YnabPayee list) : Map<YnabAccountId, YnabPayeeId> =
    payees
    |> List.choose (fun p -> p.TransferAccountId |> Option.map (fun acc -> acc, p.Id))
    |> Map.ofList

/// Pure resolution of a single split line into a YNAB subtransaction request.
/// A category line maps straight through. A transfer line is resolved against the
/// supplied transfer-payee map; if the destination account has no transfer payee,
/// resolution fails (`Error`) so the whole transaction can be rejected rather than
/// guessing a payload (ADR 0006). Takes the map as a parameter — it does NOT call
/// `getPayees` — so it is directly unit-testable.
let resolveSubtransaction
    (transferPayees: Map<YnabAccountId, YnabPayeeId>)
    (split: TransactionSplit)
    : Result<YnabSubtransactionRequest, YnabAccountId> =
    let amount = int (split.Amount.Amount * 1000m)  // Convert to milliunits
    let memo = split.Memo |> Option.map truncateSplitMemo
    match split.Target with
    | ToCategory (YnabCategoryId categoryIdGuid, _) ->
        Ok (CategorySub (amount, categoryIdGuid.ToString(), memo))
    | ToTransfer (accountId, _) ->
        match Map.tryFind accountId transferPayees with
        | Some (YnabPayeeId payeeGuid) ->
            Ok (TransferSub (amount, payeeGuid.ToString(), memo))
        | None ->
            // No transfer payee for this account — reject the transaction upstream.
            Error accountId

/// Resolves every split line of a transaction. Succeeds only if ALL lines resolve;
/// the first unresolvable transfer account is returned so the caller can mark the
/// whole transaction `RejectedByYnab`. Pure (takes the payee map).
let resolveSplits
    (transferPayees: Map<YnabAccountId, YnabPayeeId>)
    (splits: TransactionSplit list)
    : Result<YnabSubtransactionRequest list, YnabAccountId> =
    (Ok [], splits)
    ||> List.fold (fun acc split ->
        match acc with
        | Error e -> Error e
        | Ok subs ->
            match resolveSubtransaction transferPayees split with
            | Ok sub -> Ok (subs @ [ sub ])
            | Error accountId -> Error accountId)

/// True if any split line of the transaction is a transfer. Used to decide whether
/// a push batch needs to fetch payees at all (category-only batches skip the fetch).
let private hasTransferLine (tx: SyncTransaction) : bool =
    match tx.Splits with
    | Some splits -> splits |> List.exists (fun s -> match s.Target with ToTransfer _ -> true | _ -> false)
    | None -> false

/// True if ANY transaction in the batch carries a transfer split line. Only such a
/// batch needs `GET /payees`; category-only batches skip the fetch entirely (ADR 0006).
let batchHasTransferLine (transactions: SyncTransaction list) : bool =
    transactions |> List.exists hasTransferLine

/// Pure mapping of one SyncTransaction into a YNAB transaction request, given its
/// pre-computed import id and the transfer-payee map. A split transaction whose
/// transfer line has no resolvable payee yields `Error accountId` so the caller can
/// mark it `RejectedByYnab` and exclude it from the request body (ADR 0006).
/// Category-only and plain transactions never consult the map. Pure + testable.
let buildTransactionRequest
    (YnabAccountId accountId: YnabAccountId)
    (transferPayees: Map<YnabAccountId, YnabPayeeId>)
    (importId: string)
    (tx: SyncTransaction)
    : Result<YnabTransactionRequest, YnabAccountId> =
    let baseFields = {
        AccountId = accountId.ToString()
        Date = tx.Transaction.BookingDate.ToString("yyyy-MM-dd")
        Amount = int (tx.Transaction.Amount.Amount * 1000m)  // Convert to milliunits
        PayeeName =
            tx.PayeeOverride
            |> Option.orElse tx.Transaction.Payee
            |> Option.defaultValue "Unknown"
        Memo = buildMemoWithReference tx.Transaction.Memo tx.Transaction.Reference
        Cleared = "uncleared"
        ImportId = importId  // Prevents duplicates (max 36 chars)
        CategoryId = None
        Subtransactions = None
    }

    match tx.Splits with
    | Some splits when splits.Length >= 2 ->
        // Split transaction: resolve every line (transfer lines need a payee), no
        // category_id on the parent. Reject the whole transaction if any line fails.
        match resolveSplits transferPayees splits with
        | Ok subtransactions -> Ok { baseFields with Subtransactions = Some subtransactions }
        | Error accountId -> Error accountId
    | _ ->
        // Regular transaction: use category_id if present.
        match tx.CategoryId with
        | Some (YnabCategoryId categoryIdGuid) ->
            Ok { baseFields with CategoryId = Some (categoryIdGuid.ToString()) }
        | None ->
            // No category - will appear as uncategorized in YNAB
            Ok baseFields

/// Creates transactions in YNAB
/// Returns the number of successfully created transactions and any duplicate import IDs
/// Handles both regular transactions and split transactions (with subtransactions)
/// If forceNewImportId is true, generates new UUIDs to bypass YNAB's duplicate detection
let createTransactions
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (YnabAccountId accountId: YnabAccountId)
    (transactions: SyncTransaction list)
    (forceNewImportId: bool)
    : Async<YnabResult<TransactionCreateResult>> =

    async {
        try
            // Filter valid transactions (not skipped)
            // Category is optional - uncategorized transactions will be imported without category_id
            let validTransactions =
                transactions
                |> List.filter (fun tx -> tx.Status <> Skipped)

            // Filter out future-dated transactions (YNAB rejects dates in the future)
            let today = DateTime.Today
            let futureSkipped, validTransactions =
                validTransactions
                |> List.partition (fun tx -> tx.Transaction.BookingDate.Date > today)
            for tx in futureSkipped do
                printfn "[YNAB] Skipping future-dated transaction: %s (date=%s)" (tx.Transaction.Id |> fun (TransactionId id) -> id) (tx.Transaction.BookingDate.ToString("yyyy-MM-dd"))

            // Resolve transfer payees ONCE per batch, and only if the batch actually
            // contains a transfer split line. Category-only batches skip the fetch
            // entirely (ADR 0006). A getPayees failure fails the whole batch.
            let! transferPayeesResult = async {
                if batchHasTransferLine validTransactions then
                    match! getPayees token (YnabBudgetId budgetId) with
                    | Ok payees -> return Ok (transferPayeeByAccount payees)
                    | Error err -> return Error err
                else
                    return Ok Map.empty
            }

            match transferPayeesResult with
            | Error err -> return Error err
            | Ok transferPayees ->

            // Convert SyncTransactions to YNAB transaction request format.
            // A split transaction whose transfer line has no resolvable payee is
            // EXCLUDED from the request body and reported back so the caller can mark
            // it RejectedByYnab (UnknownRejection ...).
            let requestsOrRejections =
                validTransactions
                |> List.map (fun tx ->
                    // Generate import_id:
                    // - Normal: Based on transaction ID (prevents accidental duplicates)
                    // - Force: New UUID (allows re-import after deletion in YNAB)
                    // Uses Domain.ImportIdPrefix to ensure consistency with DuplicateDetection
                    let importId =
                        if forceNewImportId then
                            let newGuid = Guid.NewGuid().ToString("N")
                            $"{Shared.Domain.ImportIdPrefix}:{newGuid}"
                        else
                            Shared.Domain.generateImportId tx.Transaction.Id

                    match buildTransactionRequest (YnabAccountId accountId) transferPayees importId tx with
                    | Ok request -> Choice1Of2 request
                    | Error _unresolvableAccount -> Choice2Of2 tx.Transaction.Id
                )

            let ynabTransactions =
                requestsOrRejections |> List.choose (function Choice1Of2 r -> Some r | _ -> None)
            let rejectedTransferTxIds =
                requestsOrRejections |> List.choose (function Choice2Of2 id -> Some id | _ -> None)

            for rejId in rejectedTransferTxIds do
                let (TransactionId id) = rejId
                printfn "[YNAB] Excluding transaction %s: a transfer split line has no transfer payee in YNAB" id

            if ynabTransactions.IsEmpty then
                return Ok { CreatedCount = 0; DuplicateImportIds = []; RejectedTransferTransactionIds = rejectedTransferTxIds }
            else
                // Encode to JSON using manual encoder
                let requestBody =
                    Encode.object [
                        "transactions", Encode.list (ynabTransactions |> List.map encodeTransaction)
                    ]
                    |> Encode.toString 2

                use httpClient = new System.Net.Http.HttpClient()
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")

                use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
                let! httpResponse = httpClient.PostAsync($"{baseUrl}/budgets/{budgetId}/transactions", content) |> Async.AwaitTask
                let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
                let statusCode = int httpResponse.StatusCode

                let response =
                    {| statusCode = statusCode; bodyText = bodyText |}

                // Log request and response for debugging
                printfn "[YNAB] POST /budgets/%s/transactions - Sending %d transactions" budgetId ynabTransactions.Length
                printfn "[YNAB] Response Status: %d" response.statusCode
                printfn "[YNAB] Response Body: %s" response.bodyText

                match response.statusCode with
                | 201 ->
                    // Parse response to get actual created transactions count
                    // YNAB returns: { data: { transactions: [...], duplicate_import_ids: [...] } }
                    let createdCountDecoder =
                        Decode.field "data" (
                            Decode.object (fun get ->
                                let transactions = get.Optional.Field "transactions" (Decode.list (Decode.succeed ())) |> Option.defaultValue []
                                let duplicates = get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string) |> Option.defaultValue []
                                (transactions.Length, duplicates)
                            )
                        )

                    match Decode.fromString createdCountDecoder response.bodyText with
                    | Ok (createdCount, duplicateIds) ->
                        if duplicateIds.Length > 0 then
                            printfn "[YNAB] WARNING: %d transactions were rejected as duplicates: %A" duplicateIds.Length duplicateIds
                        printfn "[YNAB] Successfully created %d transactions (sent: %d, duplicates: %d)" createdCount ynabTransactions.Length duplicateIds.Length
                        return Ok { CreatedCount = createdCount; DuplicateImportIds = duplicateIds; RejectedTransferTransactionIds = rejectedTransferTxIds }
                    | Error parseErr ->
                        // If we can't parse, fall back to assuming all were created (old behavior)
                        printfn "[YNAB] WARNING: Could not parse response, assuming all %d transactions created. Parse error: %s" ynabTransactions.Length parseErr
                        return Ok { CreatedCount = ynabTransactions.Length; DuplicateImportIds = []; RejectedTransferTransactionIds = rejectedTransferTxIds }
                | 400 ->
                    return Error (YnabError.InvalidResponse $"Bad request: {response.bodyText}")
                | 401 ->
                    return Error (YnabError.Unauthorized "Invalid YNAB token")
                | 404 ->
                    return Error (YnabError.InvalidResponse "Budget or account not found")
                | 429 ->
                    return Error (YnabError.RateLimitExceeded 60)
                | _ ->
                    return Error (YnabError.NetworkError $"HTTP {response.statusCode}: {response.bodyText}")
        with
        | ex ->
            return Error (YnabError.NetworkError $"Failed to create transactions: {ex.Message}")
    }

/// Builds the JSON body for a single manual transaction (Quick Add).
/// Deliberately sets no import_id: manual entries are user-entered (like
/// YNAB's own mobile quick add) and must never collide with the bank-import
/// deduplication that keys on import_id.
let buildManualTransactionBody (YnabAccountId accountId: YnabAccountId) (request: ManualTransactionRequest) : string =
    let amount = Shared.Domain.manualTransactionMilliunits request

    Encode.object [
        "transaction", Encode.object [
            "account_id", Encode.string (accountId.ToString())
            "date", Encode.string (request.Date.ToString("yyyy-MM-dd"))
            "amount", Encode.int amount
            // Payee is optional — omit instead of sending an empty string,
            // which YNAB would otherwise create as a payee named ""
            match request.PayeeName with
            | p when not (System.String.IsNullOrWhiteSpace p) ->
                "payee_name", Encode.string (p.Trim())
            | _ -> ()
            "cleared", Encode.string "uncleared"
            match request.Memo with
            | Some memo when not (System.String.IsNullOrWhiteSpace memo) ->
                "memo", Encode.string (memo.Trim())
            | _ -> ()
            match request.CategoryId with
            | Some (YnabCategoryId catId) -> "category_id", Encode.string (catId.ToString())
            | None -> ()
        ]
    ]
    |> Encode.toString 2

/// Creates a single manually entered transaction (Quick Add) in YNAB.
let createManualTransaction
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (accountId: YnabAccountId)
    (request: ManualTransactionRequest)
    : Async<YnabResult<unit>> =

    async {
        try
            let requestBody = buildManualTransactionBody accountId request

            use httpClient = new System.Net.Http.HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")

            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            let! httpResponse = httpClient.PostAsync($"{baseUrl}/budgets/{budgetId}/transactions", content) |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] POST /budgets/%s/transactions - Quick Add" budgetId
            printfn "[YNAB] Response Status: %d" statusCode

            match statusCode with
            | 201 -> return Ok ()
            | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 404 -> return Error (YnabError.InvalidResponse "Budget or account not found")
            | 429 -> return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to create transaction: {ex.Message}")
    }

/// Validates a YNAB token by attempting to fetch budgets
let validateToken (token: string) : Async<YnabResult<unit>> =
    async {
        let! result = getBudgets token
        return result |> Result.map (fun _ -> ())
    }

/// Fetches recent transactions from a specific account
/// Used for duplicate detection before importing
let getAccountTransactions
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (YnabAccountId accountId: YnabAccountId)
    (sinceDays: int)
    : Async<YnabResult<YnabTransaction list>> =
    async {
        try
            let sinceDate = DateTime.Today.AddDays(float -sinceDays).ToString("yyyy-MM-dd")
            let! response =
                http {
                    GET $"{baseUrl}/budgets/{budgetId}/accounts/{accountId}/transactions?since_date={sinceDate}"
                    Authorization $"Bearer {token}"
                }
                |> Request.sendAsync

            let! bodyText = response |> Response.toTextAsync
            let statusCode = response.statusCode |> int

            match statusCode with
            | 200 ->
                let decoder = Decode.field "data" (Decode.field "transactions" (Decode.list Decoders.transactionDecoder))
                match Decode.fromString decoder bodyText with
                | Ok transactions -> return Ok transactions
                | Error err -> return Error (YnabError.InvalidResponse $"Failed to parse transactions: {err}")

            | 401 ->
                return Error (YnabError.Unauthorized "Invalid YNAB token")

            | 404 ->
                return Error (YnabError.AccountNotFound (accountId.ToString()))

            | 429 ->
                return Error (YnabError.RateLimitExceeded 60)

            | _ ->
                return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")

        with
        | ex ->
            return Error (YnabError.NetworkError $"Failed to fetch transactions: {ex.Message}")
    }
