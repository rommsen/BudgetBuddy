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

/// Creates transactions in YNAB
/// Returns the number of successfully created transactions
let createTransactions
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (YnabAccountId accountId: YnabAccountId)
    (transactions: SyncTransaction list)
    : Async<YnabResult<int>> =

    async {
        try
            // Convert SyncTransactions to YNAB transaction format
            let ynabTransactions =
                transactions
                |> List.filter (fun tx ->
                    // Only import categorized transactions that aren't skipped
                    tx.Status <> Skipped && tx.CategoryId.IsSome
                )
                |> List.map (fun tx ->
                    let (TransactionId txId) = tx.Transaction.Id
                    let (YnabCategoryId categoryIdGuid) = tx.CategoryId.Value

                    {|
                        account_id = accountId.ToString()
                        date = tx.Transaction.BookingDate.ToString("yyyy-MM-dd")
                        amount = int64 (tx.Transaction.Amount.Amount * 1000m)  // Convert to milliunits
                        payee_name =
                            tx.PayeeOverride
                            |> Option.orElse tx.Transaction.Payee
                            |> Option.defaultValue "Unknown"
                        category_id = categoryIdGuid.ToString()
                        memo =
                            if tx.Transaction.Memo.Length > 200 then
                                tx.Transaction.Memo.Substring(0, 197) + "..."
                            else
                                tx.Transaction.Memo
                        cleared = "cleared"
                        import_id = $"BUDGETBUDDY:{txId}:{tx.Transaction.BookingDate.Ticks}"  // Prevents duplicates
                    |}
                )

            if ynabTransactions.IsEmpty then
                return Ok 0
            else
                // Encode to JSON
                let requestBody =
                    Encode.object [
                        "transactions", (Encode.Auto.generateEncoder() ynabTransactions)
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

                match response.statusCode with
                | 201 ->
                    // Successfully created
                    return Ok ynabTransactions.Length
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
