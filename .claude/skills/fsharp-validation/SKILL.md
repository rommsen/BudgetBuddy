---
name: fsharp-validation
description: |
  Implement input validation patterns for F# API boundaries.
  Use when validating user input, API requests, or form data.
  Ensures early validation with clear error messages using Result types.
  Creates code in src/Server/Validation.fs.
allowed-tools: Read, Edit, Write, Grep, Glob
standards:
  - standards/shared/validation.md
---

# Input Validation Patterns

## When to Use This Skill

Activate when:
- User requests "add validation for X"
- Implementing API endpoints (ALWAYS validate at boundary)
- Need to check user input
- Defining validation rules
- Project has src/Server/Validation.fs

## Validation Principles

1. **Validate at API boundary** - Before any processing
2. **Accumulate all errors** - Return ALL validation errors, not just first
3. **Clear error messages** - User-friendly, specific messages
4. **Use Result type** - `Result<'T, string list>` for multiple errors

## Implementation Workflow

### Step 1: Define Validators

**Read:** `standards/shared/validation.md`
**Edit:** `src/Server/Validation.fs`

```fsharp
module Validation

open System

// Helper to build error list
let private errors conditions =
    conditions
    |> List.choose id
    |> function
        | [] -> None
        | errs -> Some errs

// Validate single entity
let validateItem (item: Item) : Result<Item, string list> =
    match errors [
        if String.IsNullOrWhiteSpace(item.Name) then
            Some "Name is required"

        if item.Name.Length > 100 then
            Some "Name must be 100 characters or less"

        if item.Amount < 0m then
            Some "Amount must be positive"
    ] with
    | None -> Ok item
    | Some errs -> Error errs

// Validate with dependencies (e.g., check uniqueness)
let validateItemUnique (item: Item) (existingNames: string list) : Result<Item, string list> =
    match errors [
        if String.IsNullOrWhiteSpace(item.Name) then
            Some "Name is required"

        if existingNames |> List.contains item.Name then
            Some "Name already exists"
    ] with
    | None -> Ok item
    | Some errs -> Error errs
```

**Key Pattern:** Accumulate errors, return all at once

---

### Step 2: Use in API

**Pattern:** Validate before processing

```fsharp
// In Api.fs
let itemApi : IItemApi = {
    save = fun item -> async {
        // Validate first
        match Validation.validateItem item with
        | Error errs ->
            return Error (String.concat ", " errs)

        | Ok validItem ->
            // Now process
            let processed = Domain.processItem validItem
            do! Persistence.save processed
            return Ok processed
    }
}
```

---

## Quick Reference

### Common Validation Patterns

```fsharp
// Required field
if String.IsNullOrWhiteSpace(value) then
    Some "Field is required"

// Length constraints
if value.Length > 100 then
    Some "Must be 100 characters or less"
if value.Length < 3 then
    Some "Must be at least 3 characters"

// Numeric constraints
if amount < 0 then
    Some "Must be positive"
if amount > 1000 then
    Some "Must be 1000 or less"

// Format validation
if not (value.Contains("@")) then
    Some "Must be a valid email"

// List constraints
if items.IsEmpty then
    Some "Must have at least one item"
if items.Length > 10 then
    Some "Cannot have more than 10 items"
```

### Validation Builder Pattern

```fsharp
let validate entity =
    match errors [
        // Add all validation rules here
        if condition then Some "error"
    ] with
    | None -> Ok entity
    | Some errs -> Error errs
```

### Using in API

```fsharp
// Single entity
match Validation.validate entity with
| Error errs -> return Error (String.concat ", " errs)
| Ok valid -> // process valid entity

// With dependencies
let! existing = Persistence.getAll()
match Validation.validateUnique entity existing with
| Error errs -> return Error (String.concat ", " errs)
| Ok valid -> // process
```

## Verification Checklist

- [ ] **Read standards** (shared/validation.md)
- [ ] Validation in `src/Server/Validation.fs`
- [ ] Returns `Result<'T, string list>`
- [ ] Accumulates ALL errors (not just first)
- [ ] Clear, user-friendly error messages
- [ ] Validated at API boundary (before domain logic)
- [ ] Tests written for all validation rules
- [ ] `dotnet build` succeeds

## Common Pitfalls

**Most Critical:**
- ❌ Stopping at first error
- ❌ Vague error messages ("Invalid input")
- ❌ Validating inside domain logic
- ❌ Using exceptions for validation errors
- ✅ Accumulate all errors
- ✅ Specific messages ("Name is required")
- ✅ Validate at API boundary
- ✅ Use Result type

## Validation Test Example

```fsharp
[<Tests>]
let tests = testList "Validation" [
    testCase "rejects empty name" <| fun () ->
        let item = { Id = 0; Name = "" }
        match Validation.validateItem item with
        | Error errs -> Expect.contains errs "Name is required" "Should reject empty"
        | Ok _ -> failtest "Should have failed"

    testCase "accepts valid item" <| fun () ->
        let item = { Id = 0; Name = "Valid"; Amount = 10.0m }
        match Validation.validateItem item with
        | Ok _ -> ()
        | Error errs -> failtestf "Should be valid: %A" errs

    testCase "accumulates multiple errors" <| fun () ->
        let item = { Id = 0; Name = ""; Amount = -5.0m }
        match Validation.validateItem item with
        | Error errs ->
            Expect.isGreaterThan errs.Length 1 "Should have multiple errors"
        | Ok _ -> failtest "Should have failed"
]
```

## Related Skills

- **fsharp-backend** - Uses validation in API
- **fsharp-feature** - Validation is step 1 in backend workflow
- **fsharp-tests** - Testing validation rules

## Detailed Documentation

For complete patterns and examples:
- `standards/shared/validation.md` - All validation patterns
- `standards/backend/error-handling.md` - Error handling strategies
