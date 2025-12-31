# Validation

> Input validation patterns for boundary protection.

## Overview

Validate all input at API boundaries before any processing. Return clear error messages and accumulate multiple errors when possible.

## When to Use This

- Validating API requests
- Checking required fields
- Enforcing length/range constraints
- Email/format validation

## Patterns

### Field Validators

```fsharp
// src/Server/Validation.fs
module Validation

type ValidationError = string
type ValidationResult<'T> = Result<'T, ValidationError list>

let validateRequired (fieldName: string) (value: string) : ValidationError option =
    if String.IsNullOrWhiteSpace value then
        Some $"{fieldName} is required"
    else
        None

let validateLength (fieldName: string) (min: int) (max: int) (value: string) : ValidationError option =
    let len = value.Length
    if len < min || len > max then
        Some $"{fieldName} must be between {min} and {max} characters"
    else
        None

let validateRange (fieldName: string) (min: int) (max: int) (value: int) : ValidationError option =
    if value < min || value > max then
        Some $"{fieldName} must be between {min} and {max}"
    else
        None

let validateEmail (email: string) : ValidationError option =
    if email.Contains("@") && email.Contains(".") then None
    else Some "Invalid email format"
```

### Entity Validators

```fsharp
let validateItem (item: Item) : ValidationResult<Item> =
    let errors = [
        validateRequired "Name" item.Name
        validateLength "Name" 3 100 item.Name
    ] |> List.choose id

    if errors.IsEmpty then Ok item
    else Error errors
```

### Error Accumulation

```fsharp
let validateItemUpdate (existing: Item) (updates: ItemUpdate) : ValidationResult<Item> =
    let errors = []

    let errors =
        match updates.Name with
        | Some name ->
            [
                validateRequired "Name" name
                validateLength "Name" 3 100 name
            ]
            |> List.choose id
            |> List.append errors
        | None -> errors

    if errors.IsEmpty then
        let updated =
            { existing with
                Name = updates.Name |> Option.defaultValue existing.Name }
        Ok updated
    else
        Error errors
```

### Validation Combinators

```fsharp
let (>>=) result f =
    match result with
    | Ok value -> f value
    | Error errors -> Error errors

let validateAll (validators: ('T -> ValidationError option) list) (value: 'T) : ValidationResult<'T> =
    let errors =
        validators
        |> List.choose (fun validator -> validator value)

    if errors.IsEmpty then Ok value
    else Error errors
```

## Usage in API

```fsharp
// src/Server/Api.fs
let itemApi = {
    saveItem = fun item -> async {
        match Validation.validateItem item with
        | Error errors -> return Error (String.concat ", " errors)
        | Ok valid ->
            let processed = Domain.processItem valid
            do! Persistence.saveItem processed
            return Ok processed
    }
}
```

## Anti-Patterns

### ❌ Skipping Validation

```fsharp
// BAD
let saveItem item = Persistence.save item

// GOOD
let saveItem item =
    match Validation.validate item with
    | Ok valid -> Persistence.save valid
    | Error errs -> Error errs
```

### ❌ Throwing Exceptions for Validation

```fsharp
// BAD
if String.IsNullOrEmpty item.Name then
    raise (ArgumentException "Name required")

// GOOD
if String.IsNullOrEmpty item.Name then
    Error ["Name required"]
```

## Checklist

- [ ] All API inputs are validated
- [ ] Validation at boundary (before domain logic)
- [ ] Clear error messages
- [ ] Multiple errors accumulated
- [ ] Return Result<'T, string list>
- [ ] No exceptions for validation failures

## See Also

- `types.md` - Domain type design
- `api-contracts.md` - API patterns
- `../backend/api-implementation.md` - API implementation
