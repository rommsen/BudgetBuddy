---
name: fsharp-validation
description: |
  Create comprehensive validation logic for F# backends with field validators, entity validation, and error accumulation.
  Use when implementing input validation, complex validation rules, or API request validation.
  Creates validators in src/Server/Validation.fs.
---

# F# Validation Patterns

## When to Use This Skill

Activate when:
- User requests "add validation for X"
- Implementing API endpoints (always validate at boundary)
- Need complex validation rules

## Core Principle

**Validate at the API boundary, before any processing.**

## Basic Validators

```fsharp
module Validation

let validateRequired (fieldName: string) (value: string) : string option =
    if String.IsNullOrWhiteSpace(value) then Some $"{fieldName} is required"
    else None

let validateLength (fieldName: string) (min: int) (max: int) (value: string) : string option =
    if value.Length < min then Some $"{fieldName} must be at least {min} characters"
    elif value.Length > max then Some $"{fieldName} must be at most {max} characters"
    else None

let validateEmail (email: string) : string option =
    if email.Contains("@") then None
    else Some "Invalid email format"
```

## Entity Validation (Accumulate All Errors)

```fsharp
let validateEntity (entity: Entity) : Result<Entity, string list> =
    let errors = [
        validateRequired "Name" entity.Name
        validateLength "Name" 1 100 entity.Name
        match entity.Description with
        | Some desc -> validateLength "Description" 0 500 desc
        | None -> None
    ] |> List.choose id

    if errors.IsEmpty then Ok entity else Error errors

// Convert to single error for API
let validateEntityString entity =
    match validateEntity entity with
    | Ok e -> Ok e
    | Error errors -> Error (String.concat "; " errors)
```

## Cross-Field Validation

```fsharp
let validateDateRange (start: DateTime) (endDate: DateTime) : string option =
    if endDate < start then Some "End date must be after start date"
    else None
```

## Integration with API

```fsharp
let api : IEntityApi = {
    create = fun request -> async {
        match Validation.validateEntity request with
        | Error errors -> return Error (String.concat "; " errors)
        | Ok valid ->
            let! saved = Persistence.insert valid
            return Ok saved
    }
}
```

## Verification Checklist

- [ ] Validation helpers defined
- [ ] Entity validators created
- [ ] Errors accumulated (not stop at first)
- [ ] Clear error messages
- [ ] Integrated with API layer
