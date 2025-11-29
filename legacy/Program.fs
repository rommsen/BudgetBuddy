open System 
open System.IO
open FsToolkit.ErrorHandling
open Comdirect2YNAB
open Helper
open Config
open UI.Menu
open Thoth.Json.Net
open RulesManager

let getYnabInfo config =
  let ynabApi = new YNAB.SDK.API(config.YNAB_Api.Secret)

  asyncResult {
    let! budgetInfo = YNAB.Infos.budgetsWithAccounts ynabApi
    Console.WriteLine(System.Environment.NewLine)
    Console.WriteLine("Budgets:")
    budgetInfo
    |> Array.iteri (
        fun i budgetInfo ->
          Console.WriteLine(sprintf ("%i. %s (Id: %s)") (i+1) budgetInfo.Name (budgetInfo.Id.ToString()))
          Console.WriteLine("  Accounts:")
          budgetInfo.Accounts
          |> List.iteri(
              fun j accountInfo -> 
                Console.WriteLine(sprintf ("  %i. %s (Id: %s)") (j+1) accountInfo.Name (accountInfo.Id.ToString())) 
        )
    )

    return ()
  }
  |> runAsync
  |> function | Ok _ -> printfn "Copy and paste id to appsettings.json. Press key to continue" | Error error -> printfn "Error %s" error


let transfer (config: Config.Config) (rulesPath: string) = 
  let ynabApi = new YNAB.SDK.API(config.YNAB_Api.Secret)
  Console.Write("Username: ")
  let username = Console.ReadLine()
  Console.Write("Password: ")
  let password = Console.ReadLine()
  let credentials = Comdirect.API.Credentials.Create username password

  asyncResult {
    let! requestInfo,tokens = Comdirect.Login.login credentials config.Comdirect_Api

    let! transactions =
      Comdirect.Transactions.getLastXDays 
        config.Transfer.Days
        requestInfo 
        tokens 
        config.Transfer.Comdirect_Account

    let! compiledRules = RulesManager.test config rulesPath

    return!
      YNAB.Transactions.addNonexistent 
        config.Transfer.Days 
        config.Transfer.YNAB_Budget
        (Guid.Parse(config.Transfer.YNAB_Account))
        transactions 
        ynabApi
        compiledRules
  }
  |> runAsync
  |> function | Ok result -> printfn "Finished %A" result | Error error -> printfn "Error %s" error

let testYnab (config: Config.Config)  = 
  let ynabApi = new YNAB.SDK.API(config.YNAB_Api.Secret)

  asyncResult {
    let transactions : Comdirect.Transactions.Transaction list =
      [
        {
          Reference = System.Guid.NewGuid().ToString()
          Booking_Date = DateTime.Now
          Amount = 20M
          Name = Some "test"
          Info = "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. Duis autem vel eum iriure dolor in hendrerit in vulputate velit esse molestie consequat, vel illum dolore eu f"
        }
      ]

    return!
      YNAB.Transactions.addNonexistent 
        config.Transfer.Days 
        config.Transfer.YNAB_Budget
        (Guid.Parse(config.Transfer.YNAB_Account))
        transactions 
        ynabApi
        []
  }
  |> runAsync
  |> function | Ok result -> printfn "Finished %A" result | Error error -> printfn "Error %s" error
  

[<EntryPoint>]
let main args =
  let config = Config.fetch()

  // Only initialize rules path, don't load rules yet
  let rulesPath = RulesManager.initializeRulesPath args

  // Function to display rules information
  let showRulesInfo (config: Config.Config) =
    RulesManager.showRulesInfo config rulesPath
    |> runAsync

  // Define menu actions
  let menuActions =
    [
      ("Transfer new transactions to YNAB", fun cfg -> transfer cfg rulesPath ; waitForAnyKey())
      ("Show rules info", fun cfg -> showRulesInfo cfg ; waitForAnyKey())
      ("Get YNAB infos", fun cfg -> getYnabInfo cfg ; waitForAnyKey())
      ("Transfer a test transaction to a YNAB", fun cfg -> testYnab cfg ; waitForAnyKey())
    ]  

  // Initialize menu
  // Assumes UI.Menu.initialize expects: action list -> config -> title string -> unit
  UI.Menu.initialize  config "Comdirect to Ynab" (menuActions,ignore)
  |> ignore // Original code had ignore here

  0 // Return success code