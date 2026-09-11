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
                    match Map.tryFind orderId orderIdMap with
                    | Some suggestion ->
                        { tx with
                            CategoryId = Some (YnabCategoryId (Guid.Parse suggestion.CategoryId))
                            CategoryName = Some suggestion.CategoryName
                            SuggestedByOrderId = Some suggestion.OrderId }
                        |> appendReviewStep now (ByOrderIdMatch suggestion.OrderId)
                    | None -> tx
                | None -> tx
            | _ -> tx)

/// Given a just-categorized transaction, finds other transactions in the session
/// with the same Amazon Order ID that don't have a category yet, and propagates the category to them.
/// Returns only the newly updated transactions (not the source transaction).
/// Appends a `ByUser` review step (categorization-gdrx8, Entscheidung Roman
/// 2026-09-11): Roman made the categorization decision on the source transaction, BB
/// only carries it over to the siblings: the decision stays his, not a new actor.
/// `at` should be the same timestamp used for the source transaction's own review step
/// (the propagation happens as part of that one categorization action).
let propagateInSession (at: DateTime) (categorized: SyncTransaction) (allTransactions: SyncTransaction list) : SyncTransaction list =
    match categorized.CategoryId, RulesEngine.extractAmazonOrderId categorized.Transaction with
    | Some categoryId, Some orderId ->
        allTransactions
        |> List.choose (fun tx ->
            // Skip the source transaction itself
            if tx.Transaction.Id = categorized.Transaction.Id then
                None
            // Only propagate to uncategorized transactions
            elif tx.CategoryId.IsSome then
                None
            else
                match tx.Status with
                | Pending | NeedsAttention ->
                    match RulesEngine.extractAmazonOrderId tx.Transaction with
                    | Some txOrderId when txOrderId = orderId ->
                        { tx with
                            CategoryId = Some categoryId
                            CategoryName = categorized.CategoryName
                            SuggestedByOrderId = Some orderId }
                        |> appendReviewStep at ByUser
                        |> Some
                    | _ -> None
                | _ -> None)
    | _ -> []
