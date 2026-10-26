module Server.YnabClient

open System
open FsHttp
open Thoth.Json.Net
open Shared.Domain
open Server.MemoFormatting
open Server.PayeeFormatting

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

    /// Reads YNAB's `hidden` flag (ledger-j8xq4 AC3): missing decodes to `false`,
    /// the conformist-safe default (same pattern as `OnBudget`/`Closed` above).
    let private categoryHidden (get: Decode.IGetters) : bool =
        get.Optional.Field "hidden" Decode.bool |> Option.defaultValue false

    /// Decodes the month's real "Ready to Assign" (YNAB `to_be_budgeted`, milliunits)
    /// from GET /budgets/{id}/months/current as Money. This is the authoritative
    /// Ready-to-Assign, unlike the internal Inflow category's own `balance`.
    let monthToBeBudgetedDecoder : Decoder<Money> =
        Decode.field "data" (Decode.field "month" (Decode.field "to_be_budgeted" Decode.int64))
        |> Decode.map (fun milliunits -> { Amount = decimal milliunits / 1000m; Currency = "EUR" })

    /// Milliunits aus YNAB als Money (EUR).
    let private milliunitsMoney (milliunits: int64) : Money =
        { Amount = decimal milliunits / 1000m; Currency = "EUR" }

    /// Eine Kategorie aus `GET /budgets/{id}/months/{month}` (budgeting-k7wmd).
    /// `goal_under_funded` ist `null` ohne Target (dann `None`), sonst der Fehlbetrag;
    /// `goal_snoozed_at` gesetzt heißt pausiert. Fehlende Flags gelten als `false`.
    let monthCategoryDecoder : Decoder<Shared.Budgeting.YnabMonthCategory> =
        Decode.object (fun get ->
            let flag name = get.Optional.Field name Decode.bool |> Option.defaultValue false
            ({ Id = get.Required.Field "id" Decode.guid
               Name = get.Required.Field "name" Decode.string
               GroupName =
                   get.Optional.Field "category_group_name" Decode.string
                   |> Option.defaultValue ""
               Assigned = get.Required.Field "budgeted" Decode.int64 |> milliunitsMoney
               Activity = get.Required.Field "activity" Decode.int64 |> milliunitsMoney
               Available = get.Required.Field "balance" Decode.int64 |> milliunitsMoney
               Underfunded =
                   get.Optional.Field "goal_under_funded" Decode.int64 |> Option.map milliunitsMoney
               Snoozed =
                   get.Optional.Field "goal_snoozed_at" Decode.string |> Option.isSome
               Hidden = flag "hidden"
               Deleted = flag "deleted"
               Internal = flag "internal" } : Shared.Budgeting.YnabMonthCategory))

    /// Der ganze Monat aus `GET /budgets/{id}/months/{month}`: Monat, Zu verteilen und
    /// alle Kategorien in YNABs Reihenfolge (budgeting-k7wmd).
    let monthDecoder : Decoder<Shared.Budgeting.YnabMonth> =
        Decode.field "data" (Decode.field "month" (Decode.object (fun get ->
            ({ Month = get.Required.Field "month" Decode.string
               ToBeBudgeted = get.Required.Field "to_be_budgeted" Decode.int64 |> milliunitsMoney
               Categories = get.Required.Field "categories" (Decode.list monthCategoryDecoder) }
             : Shared.Budgeting.YnabMonth))))

    /// Decoder for categories when category_group_name is present (from /categories endpoint)
    let categoryDecoder : Decoder<YnabCategory> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" Decode.guid |> YnabCategoryId
            Name = get.Required.Field "name" Decode.string
            GroupName = get.Required.Field "category_group_name" Decode.string
            Available = categoryAvailable get
            Hidden = categoryHidden get
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
            Hidden = categoryHidden get
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
            // Bewusst nicht Decode.datetimeUtc (liest den Kalendertag als Ortszeit und
            // verschiebt ihn um den lokalen Offset, ynab-r9k4t): derselbe Kalendertag-
            // Decoder wie der Spiegel, keine zweite Kopie.
            Date = get.Required.Field "date" Server.YnabObservations.Decoders.ynabDateDecoder
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

/// Liest einen Monat live aus YNAB (`GET /budgets/{id}/months/{YYYY-MM-01}`), mit Zu verteilen
/// und je Kategorie Zugewiesen, Aktivität, Verfügbar, Fehlbetrag und Pausiert (budgeting-k7wmd).
/// Schreibt nichts und merkt sich nichts. 429 kommt als `RateLimitExceeded`, ein Monat, den
/// YNAB nicht kennt (404), als `InvalidResponse`.
let interpretMonthResponse (monthKey: string) (status: int) (bodyText: string) : YnabResult<Shared.Budgeting.YnabMonth> =
    match status with
    | 200 ->
        match Decode.fromString Decoders.monthDecoder bodyText with
        | Ok month -> Ok month
        | Error err -> Error (YnabError.InvalidResponse $"Failed to parse month: {err}")
    | 401 -> Error (YnabError.Unauthorized "Invalid YNAB Personal Access Token")
    | 404 -> Error (YnabError.InvalidResponse $"YNAB kennt den Monat {monthKey} nicht")
    | 429 -> Error (YnabError.RateLimitExceeded 60)
    | other -> Error (YnabError.NetworkError $"HTTP {other}: {bodyText}")

/// Naht für Tests: `fetch` liefert Statuscode und Antworttext. Eine Ausnahme (Netzfehler)
/// wird zu `NetworkError`.
let getMonthWith
    (fetch: unit -> Async<int * string>)
    (monthKey: string)
    : Async<YnabResult<Shared.Budgeting.YnabMonth>> =
    async {
        try
            let! status, bodyText = fetch ()
            return interpretMonthResponse monthKey status bodyText
        with ex ->
            return Error (YnabError.NetworkError $"Failed to fetch month: {ex.Message}")
    }

let getMonth
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (monthKey: string)
    : Async<YnabResult<Shared.Budgeting.YnabMonth>> =
    let fetch () =
        async {
            let! response =
                http {
                    GET $"{baseUrl}/budgets/{budgetId}/months/{monthKey}"
                    Authorization $"Bearer {token}"
                }
                |> Request.sendAsync
            let! bodyText = response |> Response.toTextAsync
            return (response.statusCode |> int), bodyText
        }
    getMonthWith fetch monthKey

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

/// `memoLimit`, `compressWhitespace` und `truncateSplitMemo` sind nach
/// `Server.MemoFormatting` gezogen (ledger-jkb0f): `Ledger.Domain.recordComdirectPush`
/// muss dieselbe Kürzung anwenden wie der tatsächlich gesendete Request, eine zweite,
/// unabhängige Kopie hier würde genau das wieder auseinanderlaufen lassen.

/// Builds memo with reference suffix for YNAB, ensuring reference is never truncated.
/// Format: "<memo>, Ref: <reference>"
/// If memo exceeds limit, truncates from the BEGINNING to preserve the reference.
/// Non-private so tests can build the exact memo YNAB receives (e.g. to derive a
/// mirror-run observation from the sent request data instead of a hand-built guess,
/// ledger-qvt84 Iteration 3).
let buildMemoWithReference (memo: string) (reference: string) : string =
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

/// Eine von YNAB tatsächlich angelegte Buchung. Über die ImportId zugeordnet (die BB
/// selbst gesetzt hat), nicht über die Reihenfolge im Batch, YNAB kann umsortieren.
type CreatedTransactionResult = {
    TransactionId: TransactionId
    ImportId: string
    YnabTransactionId: string
}

/// Was das Auswerten der 201-Antwort von `POST /transactions` ergeben hat, oder dass
/// die Antwort nicht lesbar war. Pure Zwischenform, damit der JSON-Vertrag direkt gegen
/// eine repräsentative Antwort testbar ist (Fable.Remoting-Proxy ist nicht
/// .NET-testbar; Muster wie `manualTransactionIdDecoder`).
type ParsedCreateResponse =
    /// `data.transactions[].{import_id, id}` und `data.duplicate_import_ids`.
    | ParsedCreateResponse of created: (string option * string) list * duplicateImportIds: string list
    | UnparseableCreateResponse of parseError: string

/// Result of creating transactions in YNAB. Getrennt in "Created" (mit YNAB-Id, über
/// die ImportId zugeordnet), Duplikate, Transfer-Rejects und Zukunftsdatierte, plus ein
/// Kennzeichen für eine nicht auswertbare Antwort (Teil 1, Punkte 2 bis 5: drei Bugs
/// behoben, die vorher an dieser Stelle steckten).
type TransactionCreateResult = {
    /// Tatsächlich angelegte Buchungen, YNAB-Id über die ImportId zugeordnet.
    Created: CreatedTransactionResult list
    DuplicateImportIds: string list
    /// Transactions excluded from the request body because a transfer split line
    /// had no resolvable transfer payee in YNAB (ADR 0006). The caller marks these
    /// RejectedByYnab (UnknownRejection ...).
    RejectedTransferTransactionIds: TransactionId list
    /// Buchungen mit Datum in der Zukunft: nie gesendet, deshalb auch nie in `Created`
    /// (Bug: vorher wurden sie danach trotzdem als importiert markiert).
    FutureDatedTransactionIds: TransactionId list
    /// War die 201-Antwort nicht auswertbar? Dann ist `Created` leer, statt pauschal
    /// alle gesendeten Buchungen als angelegt zu behandeln (Bug: vorher galt genau das
    /// als Fallback).
    ResponseUnparseable: bool
}

/// Das Ergebnis, wenn nichts zu senden war (leerer Batch nach dem Vorbereiten).
/// Eigene Bindung statt Inline-Literal an jeder Stelle, u. a. für
/// `Ledger.CommandHandlers`, das nie `Server.YnabClient`-Felder unqualifiziert öffnet.
let emptyCreateResult : TransactionCreateResult = {
    Created = []
    DuplicateImportIds = []
    RejectedTransferTransactionIds = []
    FutureDatedTransactionIds = []
    ResponseUnparseable = false
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
    /// An uncategorized subtransaction: amount and memo only, no category_id and no
    /// payee_id — YNAB's own shape for a split line without a target. BB's split
    /// editor never builds one; this arm exists because the mirror can read one back
    /// (ADR 0006, Leserichtung).
    | UncategorizedSub of amount: int * memo: string option

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
    /// Flag-Farbe für "Lena fragen" beim Import (ynab-sync-d9e51, ADR 0056). `None`
    /// (Normalfall): der Schlüssel `flag_color` fehlt im Body, er ist dann byte-gleich zu
    /// früher. Der Flag-Name geht nie hinaus, YNAB leitet ihn aus der Farbe ab.
    FlagColor: string option
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
    | UncategorizedSub (amount, memo) ->
        Encode.object [
            "amount", Encode.int amount
            // Neither category_id nor payee_id: that IS an uncategorized line in YNAB.
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

/// Encoder for transaction requests. Non-private so the Create-Body contract (e.g. the
/// optional `flag_color`, ynab-sync-d9e51) can be asserted directly in tests.
let encodeTransaction (tx: YnabTransactionRequest) =
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
        match tx.FlagColor with
        | Some color -> "flag_color", Encode.string color
        | None -> ()
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
    | Uncategorized ->
        Ok (UncategorizedSub (amount, memo))

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
        PayeeName = resolvePayeeName tx.PayeeOverride tx.Transaction.Payee
        Memo = buildMemoWithReference tx.Transaction.Memo tx.Transaction.Reference
        Cleared = "uncleared"
        ImportId = importId  // Prevents duplicates (max 36 chars)
        CategoryId = None
        Subtransactions = None
        FlagColor = None
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

/// Eine SyncTransaction, vorbereitet für das Senden: die ImportId, die sie trägt
/// (normal deterministisch aus der TransactionId, bei Force-Import neu erzeugt und ab
/// jetzt für den Aufrufer auslesbar, Bug-Fix), und das fertige YNAB-Request-Objekt.
/// Pure Daten, kein I/O.
type PreparedTransaction = {
    TransactionId: TransactionId
    ImportId: string
    Request: YnabTransactionRequest
}

/// Ergebnis des Vorbereitens (Teil 1, Punkt 1): was gesendet werden soll, und was schon
/// vorher ausgeschlossen wurde (nie gesendet, nie als importiert geführt).
type PrepareResult = {
    ToSend: PreparedTransaction list
    FutureDatedTransactionIds: TransactionId list
    RejectedTransferTransactionIds: TransactionId list
}

/// Vorbereiten (Teil 1, Punkt 1): filtert Skipped und zukunftsdatierte Buchungen heraus
/// (YNAB lehnt Daten in der Zukunft ab), löst Splits samt Transfer-Zeilen gegen die
/// übergebene Payee-Map auf und vergibt die ImportId (Force-Import: neue Guid). Pure,
/// die Payee-Map kommt vom Aufrufer (`resolveTransferPayees`, ein HTTP-Call, deshalb
/// nicht Teil dieser Funktion). `today` ist injiziert, damit "zukunftsdatiert" ohne
/// Systemzeit testbar ist.
let prepareTransactions
    (today: DateTime)
    (accountId: YnabAccountId)
    (transferPayees: Map<YnabAccountId, YnabPayeeId>)
    (forceNewImportId: bool)
    (transactions: SyncTransaction list)
    : PrepareResult =
    let notSkipped = transactions |> List.filter (fun tx -> tx.Status <> Skipped)

    let futureDated, dueTransactions =
        notSkipped |> List.partition (fun tx -> tx.Transaction.BookingDate.Date > today.Date)

    let toSend, rejected =
        (([], []), dueTransactions)
        ||> List.fold (fun (sends, rejects) tx ->
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

            match buildTransactionRequest accountId transferPayees importId tx with
            | Ok request ->
                sends @ [ { TransactionId = tx.Transaction.Id; ImportId = importId; Request = request } ], rejects
            | Error _unresolvableAccount ->
                sends, rejects @ [ tx.Transaction.Id ])

    {
        ToSend = toSend
        FutureDatedTransactionIds = futureDated |> List.map (fun tx -> tx.Transaction.Id)
        RejectedTransferTransactionIds = rejected
    }

/// "Lena fragen" beim Import (ynab-sync-d9e51): trägt für jede vorbereitete Buchung, deren
/// Sitzungs-Transaktion eine Rückfrage trägt (`AskLena`), die eingestellte Lena-Farbe als
/// `flag_color` in den Create-Body ein. Buchungen ohne Rückfrage bleiben unverändert (kein
/// `flag_color`-Schlüssel). Ist keine Farbe gesetzt (zwischen Setzen der Rückfrage und
/// Push entfernt), geht keine Buchung mit Flag; die Anzahl der Rückfragen, die deshalb
/// ohne Flag blieben, liefert die zweite Komponente. Pure, kein I/O.
let applyLenaFlag
    (lenaColor: string option)
    (transactions: SyncTransaction list)
    (prepared: PrepareResult)
    : PrepareResult * int =
    let asking =
        transactions |> List.filter (fun tx -> tx.AskLena) |> List.map (fun tx -> tx.Transaction.Id) |> Set.ofList
    match lenaColor with
    | None ->
        let notSent = prepared.ToSend |> List.filter (fun p -> asking.Contains p.TransactionId) |> List.length
        prepared, notSent
    | Some color ->
        let toSend =
            prepared.ToSend
            |> List.map (fun p ->
                if asking.Contains p.TransactionId then
                    { p with Request = { p.Request with FlagColor = Some color } }
                else
                    p)
        { prepared with ToSend = toSend }, 0

/// Liest `data.transactions[].{import_id, id}` und `data.duplicate_import_ids` aus der
/// 201-Antwort von `POST /transactions`. Non-private und pur, damit der JSON-Vertrag
/// direkt gegen eine repräsentative YNAB-Antwort testbar ist (Muster wie
/// `manualTransactionIdDecoder`). Eine nicht lesbare Antwort liefert
/// `UnparseableCreateResponse`, nie einen geratenen Erfolg (Bug-Fix).
let parseCreateTransactionsResponse (bodyText: string) : ParsedCreateResponse =
    let decoder =
        Decode.field "data" (
            Decode.object (fun get ->
                let created =
                    get.Optional.Field "transactions" (
                        Decode.list (
                            Decode.object (fun g ->
                                g.Optional.Field "import_id" Decode.string, g.Required.Field "id" Decode.string)
                        )
                    )
                    |> Option.defaultValue []
                let duplicates =
                    get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string)
                    |> Option.defaultValue []
                created, duplicates)
        )
    match Decode.fromString decoder bodyText with
    | Ok (created, duplicates) -> ParsedCreateResponse (created, duplicates)
    | Error err -> UnparseableCreateResponse err

/// Löst die Transfer-Payee-Map einmal pro Batch auf, nur wenn der Batch überhaupt
/// eine Transfer-Split-Zeile enthält (ADR 0006, Rate-Limit-Schonung).
let resolveTransferPayees
    (token: string)
    (budgetId: YnabBudgetId)
    (transactions: SyncTransaction list)
    : Async<YnabResult<Map<YnabAccountId, YnabPayeeId>>> =
    async {
        let candidates = transactions |> List.filter (fun tx -> tx.Status <> Skipped)
        if batchHasTransferLine candidates then
            match! getPayees token budgetId with
            | Ok payees -> return Ok (transferPayeeByAccount payees)
            | Error err -> return Error err
        else
            return Ok Map.empty
    }

/// Baut aus der geparsten YNAB-Antwort (`parseCreateTransactionsResponse`) das
/// `TransactionCreateResult` für genau die gesendete Liste. Pure Zuordnung, extrahiert
/// aus `sendPrepared` (Verifier-Fund, ledger-qvt84 Iteration 2), damit sie ohne HTTP
/// testbar ist: ordnet YNAB-Ids über die ImportId zu (nicht über die Reihenfolge im
/// Batch, YNAB kann umsortieren), reicht Duplikate durch. Eine nicht auswertbare Antwort
/// liefert `Created = []` und `ResponseUnparseable = true` (Regressionstest für Bug 3:
/// vorher galten hier pauschal alle gesendeten Buchungen als angelegt, wenn die Antwort
/// nicht lesbar war).
let buildCreateResult (prepared: PreparedTransaction list) (parsed: ParsedCreateResponse) : TransactionCreateResult =
    match parsed with
    | ParsedCreateResponse (created, duplicateIds) ->
        let importIdToTxId = prepared |> List.map (fun p -> p.ImportId, p.TransactionId) |> Map.ofList
        let createdResults =
            created
            |> List.choose (fun (importIdOpt, ynabId) ->
                importIdOpt
                |> Option.bind (fun importId ->
                    Map.tryFind importId importIdToTxId
                    |> Option.map (fun txId ->
                        { TransactionId = txId; ImportId = importId; YnabTransactionId = ynabId })))
        {
            Created = createdResults
            DuplicateImportIds = duplicateIds
            RejectedTransferTransactionIds = []
            FutureDatedTransactionIds = []
            ResponseUnparseable = false
        }
    | UnparseableCreateResponse _ ->
        // Bug-Fix (Teil 1, Bug 3): vorher galten bei einer nicht lesbaren Antwort
        // pauschal alle gesendeten Buchungen als angelegt (Fallback-Zeile im alten
        // Code). Jetzt bleibt `Created` leer, `ResponseUnparseable` macht den Zustand
        // "unbekannt" erkennbar, kein geratener Erfolg.
        { emptyCreateResult with ResponseUnparseable = true }

/// Senden (Teil 1, Punkt 1): postet genau die übergebene, vorbereitete Liste und
/// wertet die Antwort über `buildCreateResult` aus. `RejectedTransferTransactionIds`/
/// `FutureDatedTransactionIds` sind hier immer leer, die trägt der Aufrufer aus dem
/// `PrepareResult` nach (`createTransactions` tut das; `Ledger.CommandHandlers` hängt
/// dazwischen den Kassenbuch-Schreibpfad, ADR 0021).
let sendPrepared
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (prepared: PreparedTransaction list)
    : Async<YnabResult<TransactionCreateResult>> =
    async {
        try
            if prepared.IsEmpty then
                return Ok emptyCreateResult
            else
                let requestBody =
                    Encode.object [
                        "transactions", Encode.list (prepared |> List.map (fun p -> encodeTransaction p.Request))
                    ]
                    |> Encode.toString 2

                use httpClient = new System.Net.Http.HttpClient()
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")

                use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
                let! httpResponse = httpClient.PostAsync($"{baseUrl}/budgets/{budgetId}/transactions", content) |> Async.AwaitTask
                let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
                let statusCode = int httpResponse.StatusCode

                printfn "[YNAB] POST /budgets/%s/transactions - Sending %d transactions" budgetId prepared.Length
                printfn "[YNAB] Response Status: %d" statusCode
                printfn "[YNAB] Response Body: %s" bodyText

                match statusCode with
                | 201 ->
                    let parsed = parseCreateTransactionsResponse bodyText
                    let result = buildCreateResult prepared parsed
                    match parsed with
                    | ParsedCreateResponse (_, duplicateIds) ->
                        if duplicateIds.Length > 0 then
                            printfn "[YNAB] WARNING: %d transactions were rejected as duplicates: %A" duplicateIds.Length duplicateIds
                        printfn
                            "[YNAB] Successfully created %d transactions (sent: %d, duplicates: %d)"
                            result.Created.Length
                            prepared.Length
                            duplicateIds.Length
                    | UnparseableCreateResponse parseErr ->
                        printfn "[YNAB] WARNING: Could not parse response, treating as unknown. Parse error: %s" parseErr
                    return Ok result
                | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
                | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
                | 404 -> return Error (YnabError.InvalidResponse "Budget or account not found")
                | 429 -> return Error (YnabError.RateLimitExceeded 60)
                | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to create transactions: {ex.Message}")
    }

/// Creates transactions in YNAB: vorbereiten (Filter, ImportId, Payee-Auflösung),
/// dann senden (Teil 1, Punkt 1). Behält Name und Signatur für `forceImportDuplicates`
/// bei; `Server.Api.importToYnab` nutzt `prepareTransactions`/`sendPrepared` einzeln,
/// weil dazwischen der Kassenbuch-Schreibpfad liegt (ADR 0021).
let createTransactions
    (token: string)
    (budgetId: YnabBudgetId)
    (accountId: YnabAccountId)
    (transactions: SyncTransaction list)
    (forceNewImportId: bool)
    : Async<YnabResult<TransactionCreateResult>> =
    async {
        match! resolveTransferPayees token budgetId transactions with
        | Error err -> return Error err
        | Ok transferPayees ->
            let today = DateTime.Today
            let prepared = prepareTransactions today accountId transferPayees forceNewImportId transactions

            for txId in prepared.FutureDatedTransactionIds do
                let (TransactionId id) = txId
                printfn "[YNAB] Skipping future-dated transaction: %s" id
            for txId in prepared.RejectedTransferTransactionIds do
                let (TransactionId id) = txId
                printfn "[YNAB] Excluding transaction %s: a transfer split line has no transfer payee in YNAB" id

            match! sendPrepared token budgetId prepared.ToSend with
            | Error err -> return Error err
            | Ok sent ->
                return Ok {
                    sent with
                        RejectedTransferTransactionIds = prepared.RejectedTransferTransactionIds
                        FutureDatedTransactionIds = prepared.FutureDatedTransactionIds
                }
    }

/// Markiert nur die Buchungen, die YNAB laut `result.Created` tatsächlich angelegt hat,
/// als importiert. Zukunftsdatierte, transfer-abgelehnte oder (bei nicht auswertbarer
/// Antwort) unbestätigte Buchungen bleiben unverändert. Pure, extrahiert für
/// `forceImportDuplicates` (Verifier-Fund, ledger-qvt84 Iteration 2): vorher markierte
/// der Aufrufer jede gesendete Buchung pauschal als `Imported`, unabhängig von
/// `result.Created`, `result.ResponseUnparseable` und `result.FutureDatedTransactionIds`.
let markCreatedAsImported (result: TransactionCreateResult) (transactions: SyncTransaction list) : SyncTransaction list =
    let createdTxIds = result.Created |> List.map (fun c -> c.TransactionId) |> Set.ofList
    transactions
    |> List.map (fun tx ->
        if createdTxIds.Contains tx.Transaction.Id then
            { tx with Status = Imported; YnabImportStatus = YnabImported }
        else
            tx)

/// Builds the JSON body for a single transaction created from the Kassenbuch (Quick Add,
/// and "Erneut senden" for a booking that is not yet in YNAB, ledger-cks06).
/// A manual entry sets no import_id (`importId = None`): it is user-entered (like
/// YNAB's own mobile quick add) and must never collide with the bank-import
/// deduplication that keys on import_id. Only a re-send of a booking that already carries
/// an import_id passes it (`Some`), so YNAB deduplicates the re-send.
let buildManualTransactionBodyWithFlag
    (importId: string option)
    (flagColor: string option)
    (YnabAccountId accountId: YnabAccountId)
    (request: ManualTransactionRequest)
    : string =
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
            // Nur beim erneuten Senden einer Buchung, die schon eine import_id trägt
            // (ledger-cks06): YNAB dedupliziert dann über sie.
            match importId with
            | Some id -> "import_id", Encode.string id
            | None -> ()
            // Erneutes Senden einer Buchung mit Rückfrage Lena (ynab-sync-d9e51): nur die
            // Farbe, nie der Name (ADR 0056). Ohne Flag fehlt der Schlüssel.
            match flagColor with
            | Some color -> "flag_color", Encode.string color
            | None -> ()
        ]
    ]
    |> Encode.toString 2

let buildManualTransactionBodyWith
    (importId: string option)
    (accountId: YnabAccountId)
    (request: ManualTransactionRequest)
    : string =
    buildManualTransactionBodyWithFlag importId None accountId request

let buildManualTransactionBody (accountId: YnabAccountId) (request: ManualTransactionRequest) : string =
    buildManualTransactionBodyWith None accountId request

/// Liest die YNAB-Transaction-Id aus der Antwort eines Einzel-Posts
/// (`POST /transactions` mit einem einzelnen `"transaction"`-Objekt, nicht der
/// Batch-Antwort mit `"transactions"`). Non-private, damit der JSON-Vertrag direkt
/// testbar ist (Fable.Remoting-Proxy ist nicht .NET-testbar; Muster wie
/// `encodeSubtransaction`). Fuer Quick Add ist diese Antwort die einzige Stelle, an der
/// BBs Buchung und YNABs Buchung verbunden werden koennen, da Quick Add keine ImportId
/// traegt (ADR 0004, ADR 0015).
let manualTransactionIdDecoder : Decoder<string> =
    Decode.field "data" (Decode.field "transaction" (Decode.field "id" Decode.string))

/// Creates a single manually entered transaction (Quick Add) in YNAB.
/// Returns the YNAB transaction id from the response so the caller can link the
/// Kassenbuch-Buchung to it (`EntryLinkedToYnab`, ADR 0021).
let createTransactionWithFlag
    (importId: string option)
    (flagColor: string option)
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (accountId: YnabAccountId)
    (request: ManualTransactionRequest)
    : Async<YnabResult<string>> =

    async {
        try
            let requestBody = buildManualTransactionBodyWithFlag importId flagColor accountId request

            use httpClient = new System.Net.Http.HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")

            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            let! httpResponse = httpClient.PostAsync($"{baseUrl}/budgets/{budgetId}/transactions", content) |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] POST /budgets/%s/transactions - Quick Add" budgetId
            printfn "[YNAB] Response Status: %d" statusCode

            match statusCode with
            | 201 ->
                match Decode.fromString manualTransactionIdDecoder bodyText with
                | Ok ynabTransactionId -> return Ok ynabTransactionId
                | Error parseErr ->
                    return Error (YnabError.InvalidResponse $"Konnte YNAB-Transaction-Id nicht aus der Antwort lesen: {parseErr}")
            | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 404 -> return Error (YnabError.InvalidResponse "Budget or account not found")
            | 429 -> return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to create transaction: {ex.Message}")
    }

let createTransactionWith
    (importId: string option)
    (token: string)
    (budgetId: YnabBudgetId)
    (accountId: YnabAccountId)
    (request: ManualTransactionRequest)
    : Async<YnabResult<string>> =
    createTransactionWithFlag importId None token budgetId accountId request

let createManualTransaction
    (token: string)
    (budget: YnabBudgetId)
    (accountId: YnabAccountId)
    (request: ManualTransactionRequest)
    : Async<YnabResult<string>> =
    createTransactionWith None token budget accountId request

// ============================================
// Prüfvermerk zurückschreiben (ADR 0050)
// ============================================

/// Der `cleared`-Wert, den das Zurückschreiben des Prüfvermerks senden darf. Bewusst
/// ohne `reconciled`: BB fasst Reconciled nie an (ADR 0050, Punkt 2 und 6).
type ReviewMarkCleared =
    | SendCleared
    | SendUncleared

/// Baut den Request-Body für das Zurückschreiben des Prüfvermerks: nur `approved`,
/// `cleared` nur wenn geändert (`None` lässt das Feld weg). Kein anderes Feld
/// (ADR 0028, Punkt 1; ADR 0050, Punkt 6). Non-private, damit der JSON-Vertrag direkt
/// testbar ist.
let buildReviewMarkBody (approved: bool) (cleared: ReviewMarkCleared option) : string =
    Encode.object [
        "transaction", Encode.object [
            "approved", Encode.bool approved
            match cleared with
            | Some SendCleared -> "cleared", Encode.string "cleared"
            | Some SendUncleared -> "cleared", Encode.string "uncleared"
            | None -> ()
        ]
    ]
    |> Encode.toString 2

/// Sendet den Prüfvermerk mit dem übergebenen `HttpClient` (injizierbar, damit ein
/// Test gegen einen Fake-Handler läuft, nie gegen YNAB). Einzelne Buchung ändern ist
/// laut YNAB-OpenAPI `PUT .../transactions/{transaction_id}` (`updateTransaction`,
/// alle Felder des Bodys optional); der Pfad bleibt bei `/budgets/`, wie in den
/// übrigen Aufrufen dieser Datei. Fehlerzuordnung wie `createManualTransaction`.
let sendReviewMark
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (approved: bool)
    (cleared: ReviewMarkCleared option)
    : Async<YnabResult<unit>> =
    async {
        try
            let requestBody = buildReviewMarkBody approved cleared
            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            let! httpResponse =
                httpClient.PutAsync($"{baseUrl}/budgets/{budgetId}/transactions/{ynabTransactionId}", content)
                |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] PUT /budgets/%s/transactions/%s - Prüfvermerk" budgetId ynabTransactionId
            printfn "[YNAB] Response Status: %d" statusCode

            match statusCode with
            | 200 -> return Ok ()
            | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 404 -> return Error (YnabError.InvalidResponse "Transaction not found")
            | 429 -> return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to update review mark: {ex.Message}")
    }

/// Schreibt den Prüfvermerk einer Buchung nach YNAB zurück (ADR 0050). Eigener Name und
/// eigene Signatur, die nur `approved` und `cleared` kennt, keine allgemeine
/// Update-Funktion (ADR 0028, Punkt 1).
let updateReviewMark
    (token: string)
    (budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (approved: bool)
    (cleared: ReviewMarkCleared option)
    : Async<YnabResult<unit>> =
    async {
        use httpClient = new System.Net.Http.HttpClient()
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
        return! sendReviewMark httpClient budgetId ynabTransactionId approved cleared
    }

/// Eine Buchung im Sammel-PATCH des Prüfvermerks: dieselben Felder wie der Einzelweg
/// (`approved`, `cleared` nur wenn geändert, nie `reconciled`), dazu die YNAB-Id.
type ReviewMarkBatchItem = {
    YnabTransactionId: string
    Approved: bool
    Cleared: ReviewMarkCleared option
}

/// Baut den Body des Sammel-PATCH: `{"transactions":[{"id", "approved", "cleared"?}]}`
/// (YNAB-OpenAPI `updateTransactions`, `PatchTransactionsWrapper`). Non-private, damit
/// der JSON-Vertrag direkt testbar ist.
let buildReviewMarkBatchBody (items: ReviewMarkBatchItem list) : string =
    Encode.object [
        "transactions", Encode.list [
            for item in items ->
                Encode.object [
                    "id", Encode.string item.YnabTransactionId
                    "approved", Encode.bool item.Approved
                    match item.Cleared with
                    | Some SendCleared -> "cleared", Encode.string "cleared"
                    | Some SendUncleared -> "cleared", Encode.string "uncleared"
                    | None -> ()
                ]
        ]
    ]
    |> Encode.toString 2

/// Liest aus der Antwort des Sammel-PATCH die Ids, die YNAB als gespeichert nennt
/// (`data.transaction_ids`). `None`, wenn die Antwort sie nicht nennt.
let savedTransactionIdsDecoder : Decoder<string list> =
    Decode.at [ "data"; "transaction_ids" ] (Decode.list Decode.string)

/// Sendet den Prüfvermerk mehrerer Buchungen in EINER Anfrage:
/// `PATCH .../transactions` (YNAB-OpenAPI `updateTransactions`). Die OpenAPI nennt keine
/// Obergrenze je Anfrage, deshalb geht der ganze Stapel in einem Aufruf. Die Antwort
/// nennt nur die gespeicherten Buchungen (`transaction_ids`), keine abgelehnten: Ok
/// liefert die Ids aus `items`, die YNAB nicht als gespeichert nennt (leer, wenn alle
/// genannt sind oder die Antwort die Liste nicht trägt). Fehlerzuordnung wie
/// `sendReviewMark`; ein Fehler trifft den ganzen Stapel.
let sendReviewMarkBatch
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (items: ReviewMarkBatchItem list)
    : Async<YnabResult<string list>> =
    async {
        try
            let requestBody = buildReviewMarkBatchBody items
            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            use request =
                new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Patch, $"{baseUrl}/budgets/{budgetId}/transactions")
            request.Content <- content
            let! httpResponse = httpClient.SendAsync(request) |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] PATCH /budgets/%s/transactions - Prüfvermerk für %d Buchungen" budgetId (List.length items)
            printfn "[YNAB] Response Status: %d" statusCode

            match statusCode with
            | 200 ->
                match Decode.fromString savedTransactionIdsDecoder bodyText with
                | Ok saved ->
                    let savedSet = Set.ofList saved
                    return Ok (items |> List.map (fun i -> i.YnabTransactionId) |> List.filter (savedSet.Contains >> not))
                | Error _ -> return Ok []
            | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 404 -> return Error (YnabError.InvalidResponse "Transaction not found")
            | 429 -> return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to update review marks: {ex.Message}")
    }

/// Schreibt den Prüfvermerk mehrerer Buchungen in einem Sammel-PATCH nach YNAB zurück
/// (ledger-nhwrt). Eigener Name neben `updateReviewMark`, auf `approved` und `cleared`
/// zugeschnitten (ADR 0028, Punkt 1).
let updateReviewMarkBatch
    (token: string)
    (budgetId: YnabBudgetId)
    (items: ReviewMarkBatchItem list)
    : Async<YnabResult<string list>> =
    async {
        use httpClient = new System.Net.Http.HttpClient()
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
        return! sendReviewMarkBatch httpClient budgetId items
    }

// ============================================
// Kategorie zurückschreiben (ADR 0055)
// ============================================

/// Baut den Request-Body für das Zurückschreiben der Kategorie: nur `category_id`, `null`
/// für "ohne Kategorie". Kein anderes Feld (ADR 0028, Punkt 1). Non-private, damit der
/// JSON-Vertrag direkt testbar ist.
let buildCategoryBody (categoryId: YnabCategoryId option) : string =
    Encode.object [
        "transaction", Encode.object [
            match categoryId with
            | Some (YnabCategoryId id) -> "category_id", Encode.guid id
            | None -> "category_id", Encode.nil
        ]
    ]
    |> Encode.toString 2

/// Sendet die Kategorie mit dem übergebenen `HttpClient` (injizierbar, damit ein Test
/// gegen einen Fake-Handler läuft, nie gegen YNAB): `PUT .../transactions/{transaction_id}`,
/// Fehlerzuordnung wie `sendReviewMark`.
let sendCategory
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (categoryId: YnabCategoryId option)
    : Async<YnabResult<unit>> =
    async {
        try
            let requestBody = buildCategoryBody categoryId
            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            let! httpResponse =
                httpClient.PutAsync($"{baseUrl}/budgets/{budgetId}/transactions/{ynabTransactionId}", content)
                |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] PUT /budgets/%s/transactions/%s - Kategorie" budgetId ynabTransactionId
            printfn "[YNAB] Response Status: %d" statusCode

            match statusCode with
            | 200 -> return Ok ()
            | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 404 -> return Error (YnabError.InvalidResponse "Transaction not found")
            | 429 -> return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to update category: {ex.Message}")
    }

/// Schreibt die Kategorie einer Buchung nach YNAB zurück (ADR 0055). Eigener Name und
/// eigene Signatur, die nur `category_id` kennt, keine allgemeine Update-Funktion
/// (ADR 0028, Punkt 1).
let updateCategory
    (token: string)
    (budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (categoryId: YnabCategoryId option)
    : Async<YnabResult<unit>> =
    async {
        use httpClient = new System.Net.Http.HttpClient()
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
        return! sendCategory httpClient budgetId ynabTransactionId categoryId
    }

/// Eine Buchung im Sammel-PATCH der Kategorie: YNAB-Id und Ziel-Kategorie (`None`:
/// ohne Kategorie).
type CategoryBatchItem = {
    YnabTransactionId: string
    CategoryId: YnabCategoryId option
}

/// Baut den Body des Sammel-PATCH der Kategorie:
/// `{"transactions":[{"id", "category_id"}]}`, `null` für "ohne Kategorie" (YNAB-OpenAPI
/// `updateTransactions`). Kein anderes Feld (ADR 0028, Punkt 1). Non-private, damit der
/// JSON-Vertrag direkt testbar ist.
let buildCategoryBatchBody (items: CategoryBatchItem list) : string =
    Encode.object [
        "transactions", Encode.list [
            for item in items ->
                Encode.object [
                    "id", Encode.string item.YnabTransactionId
                    match item.CategoryId with
                    | Some (YnabCategoryId id) -> "category_id", Encode.guid id
                    | None -> "category_id", Encode.nil
                ]
        ]
    ]
    |> Encode.toString 2

/// Sendet die Kategorie mehrerer Buchungen in EINER Anfrage: `PATCH .../transactions`
/// (Sammelweg; der Einzelweg `sendCategory` ist ein PUT). Ok liefert die Ids aus `items`,
/// die YNAB in der Antwort nicht als gespeichert nennt (`transaction_ids`); Fehlerzuordnung
/// wie `sendReviewMarkBatch`, ein Fehler trifft den ganzen Stapel.
let sendCategoryBatch
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (items: CategoryBatchItem list)
    : Async<YnabResult<string list>> =
    async {
        try
            let requestBody = buildCategoryBatchBody items
            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            use request =
                new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Patch, $"{baseUrl}/budgets/{budgetId}/transactions")
            request.Content <- content
            let! httpResponse = httpClient.SendAsync(request) |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] PATCH /budgets/%s/transactions - Kategorie für %d Buchungen" budgetId (List.length items)
            printfn "[YNAB] Response Status: %d" statusCode

            match statusCode with
            | 200 ->
                match Decode.fromString savedTransactionIdsDecoder bodyText with
                | Ok saved ->
                    let savedSet = Set.ofList saved
                    return Ok (items |> List.map (fun i -> i.YnabTransactionId) |> List.filter (savedSet.Contains >> not))
                | Error _ -> return Ok []
            | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 404 -> return Error (YnabError.InvalidResponse "Transaction not found")
            | 429 -> return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to update categories: {ex.Message}")
    }

/// Schreibt die Kategorie mehrerer Buchungen in einem Sammel-PATCH nach YNAB zurück
/// (ledger-w29y6). Eigener Name, nur `category_id` (ADR 0028, Punkt 1).
let updateCategoryBatch
    (token: string)
    (budgetId: YnabBudgetId)
    (items: CategoryBatchItem list)
    : Async<YnabResult<string list>> =
    async {
        use httpClient = new System.Net.Http.HttpClient()
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
        return! sendCategoryBatch httpClient budgetId items
    }

// ============================================
// Flag zurückschreiben (ADR 0056, ledger-xfjcp)
// ============================================

/// Baut den Request-Body für das Zurückschreiben des Flags: nur `flag_color`, `null` für
/// "kein Flag". Weder `flag_name` (der Name ist ein Feld der Antwort, YNAB leitet ihn aus
/// den Flag-Namen des Budgets ab) noch `approved`/`cleared` (ADR 0028, Punkt 1, ADR 0056,
/// Punkt 4). Non-private, damit der JSON-Vertrag direkt testbar ist.
let buildFlagBody (flagColor: string option) : string =
    Encode.object [
        "transaction", Encode.object [
            match flagColor with
            | Some color -> "flag_color", Encode.string color
            | None -> "flag_color", Encode.nil
        ]
    ]
    |> Encode.toString 2

/// Sendet das Flag mit dem übergebenen `HttpClient` (injizierbar, damit ein Test gegen
/// einen Fake-Handler läuft, nie gegen YNAB): `PUT .../transactions/{transaction_id}`,
/// Fehlerzuordnung wie `sendReviewMark` und `sendCategory`.
let sendFlag
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (flagColor: string option)
    : Async<YnabResult<unit>> =
    async {
        try
            let requestBody = buildFlagBody flagColor
            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            let! httpResponse =
                httpClient.PutAsync($"{baseUrl}/budgets/{budgetId}/transactions/{ynabTransactionId}", content)
                |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] PUT /budgets/%s/transactions/%s - Flag" budgetId ynabTransactionId
            printfn "[YNAB] Response Status: %d" statusCode

            match statusCode with
            | 200 -> return Ok ()
            | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 404 -> return Error (YnabError.InvalidResponse "Transaction not found")
            | 429 -> return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to update flag: {ex.Message}")
    }

/// Schreibt das Flag einer Buchung nach YNAB zurück (ADR 0056). Eigener Name und eigene
/// Signatur, die nur `flag_color` kennt, keine allgemeine Update-Funktion (ADR 0028, Punkt 1).
let updateFlag
    (token: string)
    (budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (flagColor: string option)
    : Async<YnabResult<unit>> =
    async {
        use httpClient = new System.Net.Http.HttpClient()
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
        return! sendFlag httpClient budgetId ynabTransactionId flagColor
    }

/// Eine Buchung im Sammel-PATCH des Flags: YNAB-Id und Ziel-Farbe (`None`: kein Flag).
type FlagBatchItem = {
    YnabTransactionId: string
    FlagColor: string option
}

/// Baut den Body des Sammel-PATCH des Flags: `{"transactions":[{"id", "flag_color"}]}`,
/// `null` für "kein Flag" (YNAB-OpenAPI `updateTransactions`). Weder `flag_name` noch ein
/// anderes Feld (ADR 0028, Punkt 1, ADR 0056, Punkt 4). Non-private, damit der JSON-Vertrag
/// direkt testbar ist.
let buildFlagBatchBody (items: FlagBatchItem list) : string =
    Encode.object [
        "transactions", Encode.list [
            for item in items ->
                Encode.object [
                    "id", Encode.string item.YnabTransactionId
                    match item.FlagColor with
                    | Some color -> "flag_color", Encode.string color
                    | None -> "flag_color", Encode.nil
                ]
        ]
    ]
    |> Encode.toString 2

/// Sendet das Flag mehrerer Buchungen in EINER Anfrage: `PATCH .../transactions` (Sammelweg;
/// der Einzelweg `sendFlag` ist ein PUT). Ok liefert die Ids aus `items`, die YNAB in der
/// Antwort nicht als gespeichert nennt (`transaction_ids`); Fehlerzuordnung wie
/// `sendReviewMarkBatch`, ein Fehler trifft den ganzen Stapel.
let sendFlagBatch
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (items: FlagBatchItem list)
    : Async<YnabResult<string list>> =
    async {
        try
            let requestBody = buildFlagBatchBody items
            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            use request =
                new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Patch, $"{baseUrl}/budgets/{budgetId}/transactions")
            request.Content <- content
            let! httpResponse = httpClient.SendAsync(request) |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] PATCH /budgets/%s/transactions - Flag für %d Buchungen" budgetId (List.length items)
            printfn "[YNAB] Response Status: %d" statusCode

            match statusCode with
            | 200 ->
                match Decode.fromString savedTransactionIdsDecoder bodyText with
                | Ok saved ->
                    let savedSet = Set.ofList saved
                    return Ok (items |> List.map (fun i -> i.YnabTransactionId) |> List.filter (savedSet.Contains >> not))
                | Error _ -> return Ok []
            | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 404 -> return Error (YnabError.InvalidResponse "Transaction not found")
            | 429 -> return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to update flags: {ex.Message}")
    }

/// Schreibt das Flag mehrerer Buchungen in einem Sammel-PATCH nach YNAB zurück
/// (ledger-y54gr). Eigener Name, nur `flag_color` (ADR 0028, Punkt 1).
let updateFlagBatch
    (token: string)
    (budgetId: YnabBudgetId)
    (items: FlagBatchItem list)
    : Async<YnabResult<string list>> =
    async {
        use httpClient = new System.Net.Http.HttpClient()
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
        return! sendFlagBatch httpClient budgetId items
    }

// ============================================
// Memo zurückschreiben (ADR 0062, ledger-fa1fp)
// ============================================

/// Baut den Request-Body für das Zurückschreiben des Memos: nur `memo`, `null` für "kein
/// Memo". Weder `approved`/`cleared` noch Kategorie oder Flag (ADR 0028, Punkt 1). Der Text
/// geht unverändert hinaus (BB hat ihn vorher getrimmt und auf die Grenze von YNAB geprüft,
/// `Shared.Domain.normalizeMemo`). Non-private, damit der JSON-Vertrag direkt testbar ist.
let buildMemoBody (memo: string option) : string =
    Encode.object [
        "transaction", Encode.object [
            match memo with
            | Some text -> "memo", Encode.string text
            | None -> "memo", Encode.nil
        ]
    ]
    |> Encode.toString 2

/// Sendet das Memo mit dem übergebenen `HttpClient` (injizierbar, damit ein Test gegen einen
/// Fake-Handler läuft, nie gegen YNAB): `PUT .../transactions/{transaction_id}`,
/// Fehlerzuordnung wie `sendFlag`.
let sendMemo
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (memo: string option)
    : Async<YnabResult<unit>> =
    async {
        try
            let requestBody = buildMemoBody memo
            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            let! httpResponse =
                httpClient.PutAsync($"{baseUrl}/budgets/{budgetId}/transactions/{ynabTransactionId}", content)
                |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] PUT /budgets/%s/transactions/%s - Memo" budgetId ynabTransactionId
            printfn "[YNAB] Response Status: %d" statusCode

            match statusCode with
            | 200 -> return Ok ()
            | 400 -> return Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 404 -> return Error (YnabError.InvalidResponse "Transaction not found")
            | 429 -> return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to update memo: {ex.Message}")
    }

/// Schreibt das Memo einer Buchung nach YNAB zurück (ADR 0062). Eigener Name und eigene
/// Signatur, die nur `memo` kennt, keine allgemeine Update-Funktion (ADR 0028, Punkt 1).
let updateMemo
    (token: string)
    (budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (memo: string option)
    : Async<YnabResult<unit>> =
    async {
        use httpClient = new System.Net.Http.HttpClient()
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
        return! sendMemo httpClient budgetId ynabTransactionId memo
    }

// ============================================
// Split-Schreibweg (ledger-kxme4, ADR 0058)
// ============================================
// Aufteilen einer unaufgeteilten Buchung per PUT, Ändern und Aufheben einer Split-Buchung
// per DELETE und anschließendem POST (YNAB lässt `subtransactions` einer bestehenden
// Split-Buchung nicht ändern). Eigene Funktionen, keine allgemeine Update-Funktion
// (ADR 0028, Punkt 1).

/// Die Buchung, die nach dem Löschen einer Split-Buchung neu angelegt wird (ADR 0058,
/// Punkt 2): Kopfdaten des Eintrags plus neue Kategorie oder neue Split-Zeilen.
type SplitReplacement = {
    AccountId: YnabAccountId
    Date: DateTime
    Amount: Money
    Payee: string option
    Memo: string option
    /// YNABs `flag_color`, roh.
    FlagColor: string option
    /// "uncleared" oder "cleared" (`reconciled` wird im Split-Schreibweg nie geschrieben).
    Cleared: string
    Approved: bool
    /// Die abgeleitete `import_id` (alte plus `:r<n>`); `None`, wenn die Buchung keine hatte.
    ImportId: string option
    /// Die Kategorie beim Aufheben (eine Zeile); bei Splits `None`.
    CategoryId: YnabCategoryId option
    /// Die neuen Split-Zeilen; leer beim Aufheben.
    Splits: TransactionSplit list
}

let private milliunitsOf (money: Money) : int = int (money.Amount * 1000m)

/// Der Body für das Aufteilen einer unaufgeteilten Buchung: `category_id` null und die
/// Zeilen als `subtransactions` (ADR 0058, Probelauf 1). Übertrags-Zeilen gehen mit der
/// `transfer_payee_id` des Zielkontos als `payee_id` und ohne `category_id` hinaus
/// (ADR 0006). `Error` nennt das Konto ohne Übertrags-Payee. Non-private, damit der
/// JSON-Vertrag direkt testbar ist.
let buildSplitBody
    (transferPayees: Map<YnabAccountId, YnabPayeeId>)
    (splits: TransactionSplit list)
    : Result<string, YnabAccountId> =
    resolveSplits transferPayees splits
    |> Result.map (fun subs ->
        Encode.object [
            "transaction", Encode.object [
                "category_id", Encode.nil
                "subtransactions", Encode.list (subs |> List.map encodeSubtransaction)
            ]
        ]
        |> Encode.toString 2)

/// Der Body für das Neuanlegen nach dem Löschen (ADR 0058, Punkt 2): Konto, Datum, Betrag,
/// Payee, Memo, Flag, `cleared`, `approved` des Eintrags, die abgeleitete `import_id` und
/// entweder die `category_id` (Aufheben) oder die `subtransactions`. Beträge in Milliunits
/// als JSON-Zahl. Non-private, damit der JSON-Vertrag direkt testbar ist.
let buildReplacementBody
    (transferPayees: Map<YnabAccountId, YnabPayeeId>)
    (replacement: SplitReplacement)
    : Result<string, YnabAccountId> =
    let (YnabAccountId accountGuid) = replacement.AccountId
    resolveSplits transferPayees replacement.Splits
    |> Result.map (fun subs ->
        Encode.object [
            "transaction", Encode.object [
                "account_id", Encode.guid accountGuid
                "date", Encode.string (replacement.Date.ToString("yyyy-MM-dd"))
                "amount", Encode.int (milliunitsOf replacement.Amount)
                match replacement.Payee with
                | Some payee -> "payee_name", Encode.string payee
                | None -> ()
                match replacement.Memo with
                | Some memo -> "memo", Encode.string memo
                | None -> ()
                "cleared", Encode.string replacement.Cleared
                "approved", Encode.bool replacement.Approved
                match replacement.FlagColor with
                | Some flag -> "flag_color", Encode.string flag
                | None -> ()
                match replacement.ImportId with
                | Some importId -> "import_id", Encode.string importId
                | None -> ()
                match replacement.CategoryId with
                | Some (YnabCategoryId categoryGuid) -> "category_id", Encode.guid categoryGuid
                | None -> ()
                if not (List.isEmpty subs) then
                    "subtransactions", Encode.list (subs |> List.map encodeSubtransaction)
            ]
        ]
        |> Encode.toString 2)

/// Trägt die Antwort des PUT `data.transaction.subtransactions` mit mindestens einer
/// Zeile? Fehlt sie, hat YNAB das Aufteilen still ignoriert (ADR 0058, Decision Punkt 1).
let splitResponseHasSubtransactions (bodyText: string) : bool =
    let decoder =
        Decode.field "data" (
            Decode.field "transaction" (
                Decode.field "subtransactions" (Decode.list Decode.value)
            )
        )
    match Decode.fromString decoder bodyText with
    | Ok subs -> not (List.isEmpty subs)
    | Error _ -> false

/// Liest die YNAB-Id der neu angelegten Buchung aus der Antwort des POST einer einzelnen
/// Buchung: `data.transaction.id`, sonst `data.transaction_ids[0]`.
let parseCreatedTransactionId (bodyText: string) : string option =
    let fromTransaction = Decode.field "data" (Decode.field "transaction" (Decode.field "id" Decode.string))
    let fromIds = Decode.field "data" (Decode.field "transaction_ids" (Decode.list Decode.string))
    match Decode.fromString fromTransaction bodyText with
    | Ok id -> Some id
    | Error _ ->
        match Decode.fromString fromIds bodyText with
        | Ok (id :: _) -> Some id
        | _ -> None

let private mapSplitStatus (statusCode: int) (bodyText: string) (okCodes: int list) : YnabResult<unit> =
    if List.contains statusCode okCodes then Ok ()
    else
        match statusCode with
        | 400 -> Error (YnabError.InvalidResponse $"Bad request: {bodyText}")
        | 401 -> Error (YnabError.Unauthorized "Invalid YNAB token")
        | 404 -> Error (YnabError.InvalidResponse "Transaction not found")
        | 409 -> Error (YnabError.InvalidResponse $"Conflict: {bodyText}")
        | 429 -> Error (YnabError.RateLimitExceeded 60)
        | _ -> Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")

/// Aufteilen per PUT mit dem übergebenen `HttpClient` (injizierbar, damit ein Test gegen
/// einen Fake-Handler läuft). Ein 200 ohne `subtransactions` in der Antwort gilt als
/// Fehlschlag: YNAB hat das Aufteilen dann still ignoriert.
let sendSplits
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (requestBody: string)
    : Async<YnabResult<unit>> =
    async {
        try
            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            let! httpResponse =
                httpClient.PutAsync($"{baseUrl}/budgets/{budgetId}/transactions/{ynabTransactionId}", content)
                |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] PUT /budgets/%s/transactions/%s - Split" budgetId ynabTransactionId
            printfn "[YNAB] Response Status: %d" statusCode

            match mapSplitStatus statusCode bodyText [ 200 ] with
            | Error e -> return Error e
            | Ok () ->
                if splitResponseHasSubtransactions bodyText then return Ok ()
                else
                    return
                        Error (
                            YnabError.InvalidResponse
                                "Die Antwort enthält keine subtransactions, YNAB hat das Aufteilen nicht übernommen"
                        )
        with ex ->
            return Error (YnabError.NetworkError $"Failed to update splits: {ex.Message}")
    }

/// Löscht eine Buchung (`DELETE .../transactions/{id}`), erster Schritt beim Ersetzen
/// einer Split-Buchung.
let sendDeleteTransaction
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    : Async<YnabResult<unit>> =
    async {
        try
            let! httpResponse =
                httpClient.DeleteAsync($"{baseUrl}/budgets/{budgetId}/transactions/{ynabTransactionId}")
                |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] DELETE /budgets/%s/transactions/%s" budgetId ynabTransactionId
            printfn "[YNAB] Response Status: %d" statusCode

            return mapSplitStatus statusCode bodyText [ 200 ]
        with ex ->
            return Error (YnabError.NetworkError $"Failed to delete transaction: {ex.Message}")
    }

/// Legt die ersetzte Buchung neu an (`POST .../transactions`, eine Buchung) und liefert
/// ihre neue YNAB-Id.
let sendCreateTransaction
    (httpClient: System.Net.Http.HttpClient)
    (YnabBudgetId budgetId: YnabBudgetId)
    (requestBody: string)
    : Async<YnabResult<string>> =
    async {
        try
            use content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
            let! httpResponse =
                httpClient.PostAsync($"{baseUrl}/budgets/{budgetId}/transactions", content) |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            printfn "[YNAB] POST /budgets/%s/transactions - Ersatz einer Split-Buchung" budgetId
            printfn "[YNAB] Response Status: %d" statusCode

            match mapSplitStatus statusCode bodyText [ 200; 201 ] with
            | Error e -> return Error e
            | Ok () ->
                match parseCreatedTransactionId bodyText with
                | Some id -> return Ok id
                | None -> return Error (YnabError.InvalidResponse "Die Antwort nennt keine Id der neuen Buchung")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to create transaction: {ex.Message}")
    }

let private transferPayeesFor
    (token: string)
    (budgetId: YnabBudgetId)
    (splits: TransactionSplit list)
    : Async<YnabResult<Map<YnabAccountId, YnabPayeeId>>> =
    async {
        let hasTransfer = splits |> List.exists (fun s -> match s.Target with ToTransfer _ -> true | _ -> false)
        if hasTransfer then
            match! getPayees token budgetId with
            | Ok payees -> return Ok (transferPayeeByAccount payees)
            | Error e -> return Error e
        else
            return Ok Map.empty
    }

let private noTransferPayee (YnabAccountId accountGuid) =
    YnabError.InvalidResponse $"Für das Zielkonto {accountGuid} gibt es in YNAB keinen Übertrags-Payee"

/// Teilt eine unaufgeteilte Buchung in YNAB auf (ADR 0058, Punkt 1). Holt die Payees nur,
/// wenn eine Übertrags-Zeile dabei ist.
let updateSplits
    (token: string)
    (budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    (splits: TransactionSplit list)
    : Async<YnabResult<unit>> =
    async {
        match! transferPayeesFor token budgetId splits with
        | Error e -> return Error e
        | Ok payees ->
            match buildSplitBody payees splits with
            | Error account -> return Error (noTransferPayee account)
            | Ok body ->
                use httpClient = new System.Net.Http.HttpClient()
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
                return! sendSplits httpClient budgetId ynabTransactionId body
    }

/// Löscht eine Buchung in YNAB (ADR 0058, Punkt 2, erster Schritt).
let deleteTransaction
    (token: string)
    (budgetId: YnabBudgetId)
    (ynabTransactionId: string)
    : Async<YnabResult<unit>> =
    async {
        use httpClient = new System.Net.Http.HttpClient()
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
        return! sendDeleteTransaction httpClient budgetId ynabTransactionId
    }

/// Legt die ersetzte Split-Buchung in YNAB neu an (ADR 0058, Punkt 2, zweiter Schritt) und
/// liefert die neue YNAB-Id. `replaceSplitTransaction` ist DELETE (`deleteTransaction`) und
/// dieses POST; sie sind getrennt, damit ein gescheitertes POST den Zustand "in YNAB
/// gelöscht, Neuanlage offen" tragen kann.
let createReplacement
    (token: string)
    (budgetId: YnabBudgetId)
    (replacement: SplitReplacement)
    : Async<YnabResult<string>> =
    async {
        match! transferPayeesFor token budgetId replacement.Splits with
        | Error e -> return Error e
        | Ok payees ->
            match buildReplacementBody payees replacement with
            | Error account -> return Error (noTransferPayee account)
            | Ok body ->
                use httpClient = new System.Net.Http.HttpClient()
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
                return! sendCreateTransaction httpClient budgetId body
    }

/// Ersetzt eine Split-Buchung in YNAB: DELETE, danach POST (ADR 0058, Punkt 2). Liefert die
/// neue YNAB-Id. Der Befehl in `Ledger.CommandHandlers` ruft die beiden Schritte einzeln auf,
/// damit er zwischen ihnen den Zustand festhalten kann.
let replaceSplitTransaction
    (token: string)
    (budgetId: YnabBudgetId)
    (oldYnabTransactionId: string)
    (replacement: SplitReplacement)
    : Async<YnabResult<string>> =
    async {
        match! deleteTransaction token budgetId oldYnabTransactionId with
        | Error e -> return Error e
        | Ok () -> return! createReplacement token budgetId replacement
    }

/// Liest `data.accounts` einer Konten-Antwort als `YnabAccount`-Liste (mit `on_budget`).
let parseAccountsResponse (bodyText: string) : Result<YnabAccount list, string> =
    Decode.fromString (Decode.field "data" (Decode.field "accounts" (Decode.list Decoders.accountDecoder))) bodyText

/// Alle Konten des Budgets mit `on_budget` und `closed` (ledger-pafkp): speist im Kassenbuch die
/// Sperre für Tracking-Konten und die Ziel-Liste der Übertrags-Zeilen im Split-Editor.
let fetchBudgetAccounts (token: string) (YnabBudgetId budgetId: YnabBudgetId) : Async<YnabResult<YnabAccount list>> =
    async {
        try
            use httpClient = new System.Net.Http.HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
            let! httpResponse = httpClient.GetAsync($"{baseUrl}/budgets/{budgetId}/accounts") |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            match mapSplitStatus statusCode bodyText [ 200 ] with
            | Error e -> return Error e
            | Ok () ->
                match parseAccountsResponse bodyText with
                | Error err -> return Error (YnabError.InvalidResponse $"Failed to parse accounts: {err}")
                | Ok accounts -> return Ok accounts
        with ex ->
            return Error (YnabError.NetworkError $"Failed to fetch accounts: {ex.Message}")
    }

/// Ist das Konto ein Tracking-Konto (`on_budget` falsch)? Spec: auf Tracking-Konten gibt es
/// keine Splits (ADR 0058, Punkt 6). Leichter Abruf `GET .../accounts`, nicht das ganze Budget.
let isTrackingAccount
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (accountId: YnabAccountId)
    : Async<YnabResult<bool>> =
    async {
        try
            use httpClient = new System.Net.Http.HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
            let! httpResponse = httpClient.GetAsync($"{baseUrl}/budgets/{budgetId}/accounts") |> Async.AwaitTask
            let! bodyText = httpResponse.Content.ReadAsStringAsync() |> Async.AwaitTask
            let statusCode = int httpResponse.StatusCode

            match mapSplitStatus statusCode bodyText [ 200 ] with
            | Error e -> return Error e
            | Ok () ->
                match parseAccountsResponse bodyText with
                | Error err -> return Error (YnabError.InvalidResponse $"Failed to parse accounts: {err}")
                | Ok accounts ->
                    match accounts |> List.tryFind (fun a -> a.Id = accountId) with
                    | Some account -> return Ok (not account.OnBudget)
                    | None ->
                        let (YnabAccountId g) = accountId
                        return Error (YnabError.AccountNotFound (g.ToString()))
        with ex ->
            return Error (YnabError.NetworkError $"Failed to fetch accounts: {ex.Message}")
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

// ============================================
// Spiegel-Abrufe (Delta-Sync, ADR 0014)
// ============================================
// Eigene Funktionen neben den bestehenden `since_date`-Abrufen. Die alten Pfade
// (Duplikat-Erkennung, Quick-Add-Vorlagen) bleiben unangetastet.

let private knowledgeQuery (lastKnowledge: int64 option) =
    match lastKnowledge with
    | Some k -> $"?last_knowledge_of_server={k}"
    | None -> ""

/// Der frueheste Tag, ab dem die Erstbefuellung Buchungen holt.
///
/// **Warum das noetig ist:** `GET /budgets/{id}/transactions` **ohne** `since_date`
/// liefert nicht alle Buchungen, sondern nur ein rollendes Fenster der letzten rund
/// zwoelf Monate. Gemessen am 2026-09-06 auf Romans Budget: ohne Parameter 2508
/// Buchungen ab 2025-09-08, mit `since_date=2000-01-01` 13863 Buchungen ab 2018-12-29.
/// Erst mit dem vollen Abruf stimmen die Kontostaende mit YNAB ueberein — ohne ihn
/// fehlen die alten Buchungen samt Anfangssalden, und drei von sieben Konten standen
/// im Kassenbuch falsch (Abnahme-Fund `ledger-kt0wq`, 2026-09-06).
///
/// ADR 0014 fordert fuer die Erstbefuellung ausdruecklich einen **Vollabruf**; dieser
/// Parameter ist die Umsetzung davon. Ein Datum statt `since_date=0001-01-01`, weil
/// YNAB ein gueltiges ISO-Datum erwartet; 1900 liegt vor jedem denkbaren Budget.
[<Literal>]
let private mirrorEarliestDate = "1900-01-01"

/// Die Abfrage-Parameter des Transaktions-Abrufs im Spiegel: entweder das Delta seit
/// der gemerkten Marke, oder — beim allerersten Lauf — der Vollabruf ab
/// `mirrorEarliestDate`. Oeffentlich, damit der Regressionstest den fehlenden
/// `since_date` der Erstbefuellung festnageln kann, ohne YNAB anzurufen.
let transactionsQuery (lastKnowledge: int64 option) =
    match lastKnowledge with
    | Some k -> $"?last_knowledge_of_server={k}"
    | None -> $"?since_date={mirrorEarliestDate}"

let private sendDelta (token: string) (url: string) (decoder: Decoder<'T>) (what: string) : Async<YnabResult<'T>> =
    async {
        try
            let! response =
                http {
                    GET url
                    Authorization $"Bearer {token}"
                }
                |> Request.sendAsync

            let! bodyText = response |> Response.toTextAsync
            let statusCode = response.statusCode |> int

            match statusCode with
            | 200 ->
                match Decode.fromString decoder bodyText with
                | Ok value -> return Ok value
                | Error err -> return Error (YnabError.InvalidResponse $"Failed to parse {what}: {err}")
            | 401 -> return Error (YnabError.Unauthorized "Invalid YNAB token")
            | 429 ->
                // Rate-Limit: der Lauf bricht ab, der naechste Ausloeser versucht es
                // erneut (ADR 0014). Kein Retry an dieser Stelle.
                return Error (YnabError.RateLimitExceeded 60)
            | _ -> return Error (YnabError.NetworkError $"HTTP {statusCode}: {bodyText}")
        with ex ->
            return Error (YnabError.NetworkError $"Failed to fetch {what}: {ex.Message}")
    }

/// Budgetweiter Transaktions-Abruf fuer den Spiegel: **ein** Request fuer alle Konten
/// (ADR 0014). Ohne `lastKnowledge` ist es die Erstbefuellung — die `since_date` mit
/// `mirrorEarliestDate` setzen **muss**, weil YNAB sonst nur die letzten zwoelf Monate
/// liefert (siehe dort) — mit `lastKnowledge` das Delta,
/// das auch geloeschte Buchungen als `deleted: true` liefert. Die Antwort traegt das
/// `server_knowledge` des Laufs.
let getTransactionsDelta
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (lastKnowledge: int64 option)
    : Async<YnabResult<Server.YnabObservations.YnabObservationBatch>> =
    sendDelta
        token
        $"{baseUrl}/budgets/{budgetId}/transactions{transactionsQuery lastKnowledge}"
        Server.YnabObservations.Decoders.observationBatchDecoder
        "transactions delta"

/// Konten-Abruf fuer den Spiegel, mit optionalem Delta.
let getAccountsDelta
    (token: string)
    (YnabBudgetId budgetId: YnabBudgetId)
    (lastKnowledge: int64 option)
    : Async<YnabResult<Server.YnabObservations.YnabAccountObservationBatch>> =
    sendDelta
        token
        $"{baseUrl}/budgets/{budgetId}/accounts{knowledgeQuery lastKnowledge}"
        Server.YnabObservations.Decoders.accountObservationBatchDecoder
        "accounts delta"
