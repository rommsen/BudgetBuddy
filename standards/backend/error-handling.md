# Error Handling

> Result types and async error patterns.

## Overview

Use Result types for expected failures and exceptions only for unexpected errors. Handle all errors explicitly and provide clear messages.

## When to Use This

- Operations that can fail (validation, not found, etc.)
- Async operations with potential errors
- Multiple error types
- Error recovery

## Patterns

### Result Type for Expected Failures

```fsharp
// Expected failures use Result
let processOrder (order: Order) : Result<ProcessedOrder, string> =
    if order.Items.IsEmpty then
        Error "Order must contain at least one item"
    elif order.Total < 0m then
        Error "Order total cannot be negative"
    else
        Ok { OrderId = order.Id; ProcessedAt = DateTime.UtcNow }

// Usage in API
let orderApi = {
    submitOrder = fun order -> async {
        match Domain.processOrder order with
        | Ok processed ->
            do! Persistence.saveOrder processed
            return Ok processed
        | Error msg ->
            return Error msg
    }
}
```

### Exception Handling at Boundaries

```fsharp
let safeExecute (operation: unit -> Async<'T>) : Async<Result<'T, string>> =
    async {
        try
            let! result = operation()
            return Ok result
        with
        | :? System.IO.IOException as ex ->
            return Error $"File operation failed: {ex.Message}"
        | ex ->
            return Error $"Unexpected error: {ex.Message}"
    }

// Usage
let getItem itemId = async {
    let! result = safeExecute (fun () -> Persistence.getItemById itemId)
    match result with
    | Ok (Some item) -> return Ok item
    | Ok None -> return Error $"Item {itemId} not found"
    | Error msg -> return Error msg
}
```

### Custom Error Types

```fsharp
type AppError =
    | NotFound of entityType: string * id: int
    | ValidationError of errors: string list
    | DatabaseError of message: string
    | UnauthorizedAccess of resource: string

let errorToString (error: AppError) : string =
    match error with
    | NotFound (entityType, id) -> $"{entityType} with id {id} not found"
    | ValidationError errors -> String.concat ", " errors
    | DatabaseError msg -> $"Database error: {msg}"
    | UnauthorizedAccess resource -> $"Unauthorized access to {resource}"

type ApiResult<'T> = Result<'T, AppError>

let getItemResult (itemId: int) : Async<ApiResult<Item>> =
    async {
        try
            match! Persistence.getItemById itemId with
            | Some item -> return Ok item
            | None -> return Error (NotFound ("Item", itemId))
        with ex ->
            return Error (DatabaseError ex.Message)
    }
```

## Async Patterns

### Sequential Operations

```fsharp
let processWorkflow (item: Item) : Async<Result<Item, string>> =
    async {
        match Validation.validateItem item with
        | Error errors -> return Error (String.concat ", " errors)
        | Ok validItem ->
            let processedItem = Domain.processItem validItem

            try
                do! Persistence.saveItem processedItem
                do! Persistence.logEvent (ItemCreated processedItem)
                return Ok processedItem
            with ex ->
                return Error ex.Message
    }
```

### Parallel Operations

```fsharp
let loadDashboard () : Async<DashboardData> =
    async {
        let! itemsTask = Persistence.getAllItems() |> Async.StartChild
        let! usersTask = Persistence.getAllUsers() |> Async.StartChild
        let! statsTask = Persistence.getStats() |> Async.StartChild

        let! items = itemsTask
        let! users = usersTask
        let! stats = statsTask

        return { Items = items; Users = users; Stats = stats }
    }
```

### Async with Timeout

```fsharp
let withTimeout (timeout: int) (operation: Async<'T>) : Async<Result<'T, string>> =
    async {
        let! result =
            Async.Choice [
                async {
                    let! value = operation
                    return Choice1Of2 value
                }
                async {
                    do! Async.Sleep timeout
                    return Choice2Of2 "Operation timed out"
                }
            ]

        match result with
        | Choice1Of2 value -> return Ok value
        | Choice2Of2 error -> return Error error
    }
```

## Anti-Patterns

### ❌ Ignoring Errors

```fsharp
// BAD
let item = result.Value  // Throws if Error!

// GOOD
match result with
| Ok item -> // use item
| Error err -> // handle error
```

### ❌ Using Exceptions for Control Flow

```fsharp
// BAD
try
    let item = findItem id
    processItem item
with
| :? NotFoundException -> handleNotFound()

// GOOD
match findItem id with
| Some item -> processItem item
| None -> handleNotFound()
```

### ❌ Swallowing Exceptions

```fsharp
// BAD
try
    doSomething()
with _ -> ()  // Error is lost!

// GOOD
try
    doSomething()
with ex ->
    printfn $"Error: {ex.Message}"
    return Error ex.Message
```

## Checklist

- [ ] Use Result for expected failures
- [ ] Use exceptions for unexpected errors
- [ ] Handle all error cases explicitly
- [ ] Provide clear error messages
- [ ] Catch exceptions at boundaries
- [ ] Log errors appropriately
- [ ] Never swallow exceptions silently

## See Also

- `api-implementation.md` - Error handling in APIs
- `domain-logic.md` - Pure error handling
- `../shared/validation.md` - Validation errors
