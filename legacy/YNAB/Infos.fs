module YNAB.Infos 

let private accountsForBudget (ynabApi : YNAB.SDK.API) budgetId =
  async {
    let! accountResponse = 
      ynabApi.Accounts.GetAccountsAsync (budgetId.ToString())
      |> Async.AwaitTask

    return
      accountResponse.Data.Accounts
      |> Seq.map (fun account -> { Id = account.Id; Name = account.Name })
      |> Seq.toList
  }


let budgetsWithAccounts (ynabApi : YNAB.SDK.API) =
  async {
    let! budgetSummary = ynabApi.Budgets.GetBudgetsAsync() |> Async.AwaitTask

    let budgets =
      budgetSummary.Data.Budgets
      |> Seq.map (fun budget -> 
          async {
            let! accounts = accountsForBudget ynabApi budget.Id
            return 
              { Id = budget.Id
                Name = budget.Name  
                Accounts = accounts
              }
          })
      |> Async.Parallel

    return! budgets
  }