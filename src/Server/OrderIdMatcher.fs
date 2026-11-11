module Server.OrderIdMatcher

open System
open Shared.Domain

/// Category suggestion derived from another transaction with the same Amazon Order ID
type OrderIdSuggestion = {
    OrderId: string
    CategoryId: string
    CategoryName: string
    SourceDate: DateTime
}

/// Builds a map from Amazon Order ID to category suggestion based on existing YNAB transactions.
/// Only includes transactions that have both an Order ID and a category assigned.
/// When multiple transactions share the same Order ID, the most recent one wins.
let buildYnabOrderIdMap (ynabTransactions: YnabTransaction list) : Map<string, OrderIdSuggestion> =
    ynabTransactions
    |> List.choose (fun tx ->
        // Need both category and an order ID in memo or payee
        match tx.CategoryId, tx.CategoryName with
        | Some catId, Some catName ->
            let text =
                [ tx.Payee |> Option.defaultValue ""
                  tx.Memo |> Option.defaultValue "" ]
                |> String.concat " "
            match RulesEngine.extractAmazonOrderIdFromText text with
            | Some orderId ->
                Some (orderId, { OrderId = orderId; CategoryId = catId; CategoryName = catName; SourceDate = tx.Date })
            | None -> None
        | _ -> None)
    // Group by order ID and pick the most recent
    |> List.groupBy fst
    |> List.map (fun (orderId, entries) ->
        let newest = entries |> List.maxBy (fun (_, s) -> s.SourceDate) |> snd
        (orderId, newest))
    |> Map.ofList

/// Die Kategorie-Id eines Vorschlags. `None`, wenn YNAB etwas anderes als eine Guid lieferte.
let private parseCategoryId (suggestion: OrderIdSuggestion) : YnabCategoryId option =
    match Guid.TryParse suggestion.CategoryId with
    | true, id -> Some (YnabCategoryId id)
    | false, _ -> None

/// Applies Order ID-based category suggestions to uncategorized Amazon transactions.
/// Only affects transactions with status Pending or NeedsAttention that don't already have a category.
/// Sets SuggestedByOrderId to indicate the source of the suggestion.
/// Appends a `ByOrderIdMatch` review step (categorization-gdrx8, Entscheidung Roman
/// 2026-09-11): the category comes from the YNAB history of the same order, not from a
/// rule or Roman deciding on THIS transaction, so it gets its own `ReviewActor`.
/// `now` is threaded through rather than read via `DateTime.UtcNow` here, so this stays
/// pure and testable.
let applySuggestions (now: DateTime) (orderIdMap: Map<string, OrderIdSuggestion>) (transactions: SyncTransaction list) : SyncTransaction list =
    if Map.isEmpty orderIdMap then
        transactions
    else
        transactions
        |> List.map (fun tx ->
            // Only suggest for uncategorized transactions
            match tx.CategoryId, tx.Status with
            | None, (Pending | NeedsAttention) ->
                match RulesEngine.extractAmazonOrderId tx.Transaction with
                | Some orderId ->
                    // Eine nicht lesbare Kategorie-Id aus der YNAB-Historie überspringt den Eintrag
                    // (categorization-hz6vj): `Guid.Parse` warf hier und brach den ganzen Abruf ab.
                    match Map.tryFind orderId orderIdMap |> Option.bind (fun s -> parseCategoryId s |> Option.map (fun id -> s, id)) with
                    | Some (suggestion, categoryId) ->
                        { tx with
                            CategoryId = Some categoryId
                            CategoryName = Some suggestion.CategoryName
                            SuggestedByOrderId = Some suggestion.OrderId }
                        |> appendReviewStep now (ByOrderIdMatch suggestion.OrderId)
                    | None -> tx
                | None -> tx
            | _ -> tx)
