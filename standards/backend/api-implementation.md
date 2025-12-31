# API Implementation

> Fable.Remoting API implementation patterns.

## Overview

Implement API contracts defined in `src/Shared/Api.fs` using Fable.Remoting. This provides type-safe RPC with automatic JSON serialization.

## When to Use This

- Implementing API endpoints
- Handling async operations
- Error handling in APIs
- Combining multiple APIs

## Patterns

### Basic API Implementation

```fsharp
// src/Server/Api.fs
module Api

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api
open Shared.Domain

let itemApi : IItemApi = {
    getItems = fun () -> async {
        return! Persistence.getAllItems()
    }

    getItem = fun itemId -> async {
        match! Persistence.getItemById itemId with
        | Some item -> return Ok item
        | None -> return Error $"Item {itemId} not found"
    }

    saveItem = fun item -> async {
        try
            match Validation.validateItem item with
            | Error errors -> return Error (String.concat ", " errors)
            | Ok valid ->
                let processed = Domain.processItem valid
                do! Persistence.saveItem processed
                return Ok processed
        with ex ->
            return Error ex.Message
    }

    deleteItem = fun itemId -> async {
        try
            do! Persistence.deleteItem itemId
            return Ok ()
        with ex ->
            return Error ex.Message
    }
}
```

### Remoting Configuration

```fsharp
let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName ->
        $"/api/{typeName}/{methodName}"
    )
    |> Remoting.fromValue itemApi
    |> Remoting.withErrorHandler (fun ex routeInfo ->
        printfn $"Error in {routeInfo.methodName}: {ex.Message}"
        Propagate ex.Message
    )
    |> Remoting.buildHttpHandler
```

### Multiple APIs

```fsharp
let userApi : IUserApi = {
    getCurrentUser = fun () -> async {
        return { Id = 1; Name = "User" }
    }
}

let combinedWebApp =
    choose [
        Remoting.createApi()
        |> Remoting.fromValue itemApi
        |> Remoting.buildHttpHandler

        Remoting.createApi()
        |> Remoting.fromValue userApi
        |> Remoting.buildHttpHandler
    ]
```

### API Orchestration Pattern

```fsharp
let api = {
    save = fun entity -> async {
        // 1. Validate
        match Validation.validate entity with
        | Error errs -> return Error (String.concat ", " errs)
        | Ok valid ->
            // 2. Domain logic (pure)
            let processed = Domain.process valid
            // 3. Persist
            do! Persistence.save processed
            return Ok processed
    }
}
```

## Error Handling

### Result Type for Expected Failures

```fsharp
let getItem itemId : Async<Result<Item, string>> =
    async {
        match! Persistence.getItemById itemId with
        | Some item -> return Ok item
        | None -> return Error $"Item {itemId} not found"
    }
```

### Exception Handling at Boundaries

```fsharp
let saveItem item : Async<Result<Item, string>> =
    async {
        try
            do! Persistence.saveItem item
            return Ok item
        with
        | :? System.IO.IOException as ex ->
            return Error $"File operation failed: {ex.Message}"
        | ex ->
            return Error $"Unexpected error: {ex.Message}"
    }
```

## Anti-Patterns

### ❌ Business Logic in API

```fsharp
// BAD
let saveItem item = async {
    let processed = { item with Name = item.Name.Trim() }  // Logic here!
    do! Persistence.save processed
    return Ok processed
}

// GOOD
let saveItem item = async {
    let processed = Domain.processItem item  // Logic in Domain.fs
    do! Persistence.save processed
    return Ok processed
}
```

### ❌ Ignoring Validation

```fsharp
// BAD
let saveItem item = Persistence.save item

// GOOD
let saveItem item =
    match Validation.validate item with
    | Ok valid -> Persistence.save valid
    | Error errs -> Error errs
```

## Checklist

- [ ] API contract implemented from Shared
- [ ] Validation before processing
- [ ] Domain logic separated
- [ ] Error handling with Result types
- [ ] Async for all I/O operations
- [ ] Route builder configured

## See Also

- `domain-logic.md` - Business logic
- `error-handling.md` - Error patterns
- `../shared/api-contracts.md` - API contracts
- `../shared/validation.md` - Validation
