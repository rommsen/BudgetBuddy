module YNAB.Transactions

open System
open System.Text.RegularExpressions
open YNAB.SDK.Model
open Comdirect2YNAB // Added
open Comdirect.Transactions // Added to ensure Transaction type is resolved

let private referenceOf input =
  if String.IsNullOrWhiteSpace input 
  then None
  else
    let regex = new Regex("Ref: (.+)")
    let result = (regex.Match input).Groups.[1].Value
    if not (String.IsNullOrWhiteSpace result) 
    then Some result
    else None

let private getExisting days budgetId accountId (ynabApi : YNAB.SDK.API) =
  let date  = new Nullable<DateTime>(DateTime.Today.Subtract(TimeSpan.FromDays(float days)))

  async {
    let! response = ynabApi.Transactions.GetTransactionsByAccountAsync(budgetId, accountId.ToString(), date) |> Async.AwaitTask
    
    return response.Data.Transactions |> Seq.toList
  }

let private truncateString str maxlength =
  if String.IsNullOrEmpty(str) then str
  elif str.Length <= maxlength then str 
  else str.Substring(0, maxlength)

let private toSaveTransaction
    (accountId: Guid)
    (transaction: Comdirect.Transactions.Transaction)
    (compiledRules: RulesEngine.CompiledRule list) : SaveTransaction =

  let memo =
    sprintf "%s, %s, Ref: %s" 
      (truncateString (transaction.Name |> Option.defaultValue "") 40)
      (truncateString (Regex.Replace(transaction.Info, @"\s+", " ").Substring(2)) 120)
      transaction.Reference

  // Determine category using the rules engine
  let categoryId = RulesEngine.classify compiledRules (Some memo) // Pass the full memo

  let tx = 
    new SaveTransaction(
      AccountId = accountId,
      Amount = int64 (transaction.Amount * 1000M),
      Memo = memo, // Using the full memo for classification and for YNAB
      Date = transaction.Booking_Date,
      Approved = true,
      CategoryId = match categoryId with Some id -> Nullable id | None -> Nullable() // Set the CategoryId
    )
  tx


let addNonexistent
    (days: int)
    (budgetId: string)
    (accountId: Guid)
    (bankTransactions: Comdirect.Transactions.Transaction list)
    (ynabApi: YNAB.SDK.API) 
    (compiledRules: RulesEngine.CompiledRule list) : Async<Result<string, string>> =
  async {

    let! ynabTransactions =
      getExisting days budgetId accountId ynabApi

    let references =
      ynabTransactions
      |> List.map (fun tx -> tx.Memo)
      |> List.choose referenceOf
   
    let newTransactions =
      bankTransactions
      |> List.filter (fun tx -> not (references |> List.contains tx.Reference))
      |> List.map (fun bankTx -> toSaveTransaction accountId bankTx compiledRules)

    if not (newTransactions |> List.isEmpty) then
      let wrapped =
        new SaveTransactionsWrapper(transactions = (ResizeArray<SaveTransaction> newTransactions))

      try 
        let! response = ynabApi.Transactions.CreateTransactionAsync(budgetId, wrapped) |> Async.AwaitTask
        // Assuming CreateTransactionAsync returns an object with a Data property,
        // and we want to indicate success. The original code returned "done".
        // If response structure is different, this part might need adjustment.
        // For now, preserving the "done" string for compatibility with existing logic.
        return Ok $"Added {List.length newTransactions} new transactions"
      with 
      | e -> return Error e.Message
    else 
      return Ok "No new transactions"
  }