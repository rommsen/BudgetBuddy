module Api

open Fable.Remoting.Server
open Fable.Remoting.Giraffe

// BudgetBuddy API implementations will be defined here
// See docs/MILESTONE-PLAN.md for API implementation details

let webApp() =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.withErrorHandler (fun ex routeInfo ->
        printfn "Fable.Remoting ERROR in %s: %s" routeInfo.methodName ex.Message
        printfn "Stack trace: %s" ex.StackTrace
        Propagate ex)
    // API will be added here with |> Remoting.fromValue
    |> Remoting.buildHttpHandler
