module Api

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api
open Shared.Domain

let counterApi : ICounterApi = {
    getCounter = fun () -> async {
        try
            printfn "API: getCounter called"
            let! counter = Persistence.loadCounter()
            printfn "API: loaded counter with value %d" counter.Value
            return counter
        with ex ->
            printfn "API ERROR: %s" ex.Message
            printfn "Stack trace: %s" ex.StackTrace
            return { Value = 0 }
    }

    incrementCounter = fun () -> async {
        try
            printfn "API: incrementCounter called"
            let! counter = Persistence.loadCounter()
            printfn "API: loaded counter with value %d" counter.Value
            let newCounter = { Value = counter.Value + 1 }
            do! Persistence.saveCounter newCounter
            printfn "API: saved counter with value %d" newCounter.Value
            return newCounter
        with ex ->
            printfn "API ERROR: %s" ex.Message
            printfn "Stack trace: %s" ex.StackTrace
            return { Value = 0 }
    }

    getDataPath = fun () -> async {
        return Persistence.getCounterFilePath()
    }
}

let webApp() =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.withErrorHandler (fun ex routeInfo ->
        printfn "Fable.Remoting ERROR in %s: %s" routeInfo.methodName ex.Message
        printfn "Stack trace: %s" ex.StackTrace
        Propagate ex)
    |> Remoting.fromValue counterApi
    |> Remoting.buildHttpHandler
