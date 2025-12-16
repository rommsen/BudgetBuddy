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
        })

    /// Decoder for categories when category_group_name is present (from /categories endpoint)
    let categoryDecoder : Decoder<YnabCategory> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" Decode.guid |> YnabCategoryId
            Name = get.Required.Field "name" Decode.string
            GroupName = get.Required.Field "category_group_name" Decode.string
        })

    /// Decoder for categories nested within category_groups (from /budgets/{id} endpoint)
    let categoryInGroupDecoder (groupName: string) : Decoder<YnabCategory> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" Decode.guid |> YnabCategoryId
            Name = get.Required.Field "name" Decode.string
            GroupName = groupName
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
}

/// Subtransaction for split transactions
type YnabSubtransactionRequest = {
    Amount: int  // Milliunits (int32 is sufficient for amounts up to ~2.1 million EUR)
    CategoryId: string
    Memo: string option
}

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

/// Encoder for subtransactions
let private encodeSubtransaction (sub: YnabSubtransactionRequest) =
    Encode.object [
        "amount", Encode.int sub.Amount  // Use int for proper JSON number serialization
        "category_id", Encode.string sub.CategoryId
        match sub.Memo with
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

/// Helper to create a subtransaction for a split
let private createSubtransaction (split: TransactionSplit) : YnabSubtransactionRequest =
    let (YnabCategoryId categoryIdGuid) = split.CategoryId
    {
        Amount = int (split.Amount.Amount * 1000m)  // Convert to milliunits
        CategoryId = categoryIdGuid.ToString()
        Memo = split.Memo |> Option.map truncateSplitMemo
    }

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

            // Convert SyncTransactions to YNAB transaction request format
            let ynabTransactions : YnabTransactionRequest list =
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

                    // Check if this is a split transaction
                    match tx.Splits with
                    | Some splits when splits.Length >= 2 ->
                        // Split transaction: use subtransactions array, no category_id on parent
                        let subtransactions = splits |> List.map createSubtransaction
                        { baseFields with Subtransactions = Some subtransactions }
                    | _ ->
                        // Regular transaction: use category_id if present
                        match tx.CategoryId with
                        | Some (YnabCategoryId categoryIdGuid) ->
                            { baseFields with CategoryId = Some (categoryIdGuid.ToString()) }
                        | None ->
                            // No category - will appear as uncategorized in YNAB
                            baseFields
                )

            if ynabTransactions.IsEmpty then
                return Ok { CreatedCount = 0; DuplicateImportIds = [] }
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
                        return Ok { CreatedCount = createdCount; DuplicateImportIds = duplicateIds }
                    | Error parseErr ->
                        // If we can't parse, fall back to assuming all were created (old behavior)
                        printfn "[YNAB] WARNING: Could not parse response, assuming all %d transactions created. Parse error: %s" ynabTransactions.Length parseErr
                        return Ok { CreatedCount = validTransactions.Length; DuplicateImportIds = [] }
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
