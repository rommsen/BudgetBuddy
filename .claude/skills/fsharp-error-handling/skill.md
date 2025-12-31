---
name: fsharp-error-handling
description: |
  Implement error handling patterns using Result types and proper error propagation.
  Use when handling failures, validation errors, or exceptional cases.
  Ensures explicit error handling without exceptions, using F# Result type.
allowed-tools: Read, Edit, Write, Grep, Glob
standards:
  - standards/backend/error-handling.md
---

# Error Handling with Result Types

## When to Use This Skill

Activate when:
- Implementing fallible operations
- Need to handle validation errors
- Working with API responses
- User asks "how to handle errors"
- Avoiding exception-based control flow

## Error Handling Philosophy

**Use Result types, not exceptions:**

```fsharp
// ✅ Explicit error handling
Result<'T, 'Error>

// ❌ Implicit error handling
try/catch exceptions
```

## Quick Start

### 1. Define Error Types

```fsharp
// Simple: string errors
type SaveResult = Result<Item, string>

// Rich: discriminated union errors
type ApiError =
    | ValidationError of string list
    | NotFound of id:int
    | Unauthorized
    | ServerError of exn

type ApiResult<'T> = Result<'T, ApiError>
```

### 2. Return Results from Functions

```fsharp
// Domain logic
let processItem (item: Item) : Result<Item, string> =
    if String.IsNullOrWhiteSpace(item.Name) then
        Error "Name is required"
    else
        Ok { item with Name = item.Name.Trim() }

// API calls
let saveItem (item: Item) : Async<Result<Item, ApiError>> = async {
    try
        match Validation.validate item with
        | Error errs -> return Error (ValidationError errs)
        | Ok valid ->
            let processed = Domain.process valid
            do! Persistence.save processed
            return Ok processed
    with
    | ex -> return Error (ServerError ex)
}
```

### 3. Compose Results with Railway-Oriented Programming

```fsharp
// Result.bind chains operations
let processAndSave item =
    item
    |> Validation.validate      // Result<Item, string list>
    |> Result.map Domain.process  // Result<Item, string list>
    |> Result.bind (fun i ->
        Persistence.save i |> Async.RunSynchronously)
```

### 4. Handle Results in API/UI

```fsharp
// In API
let itemApi = {
    save = fun item -> async {
        let! result = saveItem item
        return match result with
               | Ok item -> Ok item
               | Error (ValidationError errs) -> Error (String.concat ", " errs)
               | Error (NotFound id) -> Error $"Item {id} not found"
               | Error Unauthorized -> Error "Unauthorized"
               | Error (ServerError ex) -> Error $"Server error: {ex.Message}"
    }
}

// In Update (frontend)
| ItemSaved (Ok item) ->
    { model with Items = Success item }, Cmd.none

| ItemSaved (Error err) ->
    { model with Items = Failure err }, Cmd.none
```

## Result Helper Functions

```fsharp
// Transform success value
Result.map (fun x -> x + 1) result

// Chain operations
Result.bind (fun x -> if x > 0 then Ok x else Error "negative") result

// Provide default on error
Result.defaultValue 0 result

// Convert to option
Result.toOption result  // Some x or None

// Combine results
let result =
    result1
    |> Result.bind (fun x ->
        result2
        |> Result.map (fun y -> x + y))
```

## Error Conversion

```fsharp
// Option to Result
let toResult error = function
    | Some x -> Ok x
    | None -> Error error

// Async Result composition
let asyncBind f asyncResult = async {
    let! result = asyncResult
    match result with
    | Ok x -> return! f x
    | Error e -> return Error e
}
```

## Checklist

- [ ] **Read** `standards/backend/error-handling.md`
- [ ] Use Result types for fallible operations
- [ ] Define error types (string or DU)
- [ ] NO try/catch for control flow
- [ ] Handle all Result cases explicitly
- [ ] Use Result.map/bind for composition
- [ ] Clear error messages for users

## Common Mistakes

❌ **Using exceptions for control flow:**
```fsharp
try
    let item = getItem()
    process item
with
| :? NotFoundException -> handleNotFound()
```

✅ **Use Result type:**
```fsharp
match getItem() with
| Ok item -> process item
| Error NotFound -> handleNotFound()
```

❌ **Ignoring errors:**
```fsharp
let result = saveItem item
// What if it failed?
```

✅ **Handle all cases:**
```fsharp
match saveItem item with
| Ok saved -> // success
| Error err -> // handle error
```

## Related Skills

- **fsharp-backend** - Uses Result in API
- **fsharp-validation** - Returns Result from validation
- **fsharp-frontend** - Handles Result in Update

## Detailed Documentation

For complete error handling patterns:
- `standards/backend/error-handling.md` - Complete guide
- `standards/shared/validation.md` - Validation errors
- `standards/frontend/remotedata.md` - Frontend error states
