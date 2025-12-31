---
name: fsharp-backend
description: |
  Implement F# backend using Giraffe + Fable.Remoting with proper separation: Validation → Domain (pure logic) → Persistence (I/O) → API.
  Use when implementing server-side logic, API endpoints, or business rules.
  Ensures validation at boundaries, pure domain functions, and proper error handling with Result types.
  Creates code in src/Server/ files: Validation.fs, Domain.fs, Persistence.fs, Api.fs.
allowed-tools: Read, Edit, Write, Grep, Glob, Bash
standards:
  required-reading:
    - standards/backend/overview.md
  workflow:
    - step: 1
      file: standards/shared/validation.md
      purpose: Input validation
      output: src/Server/Validation.fs
    - step: 2
      file: standards/backend/domain-logic.md
      purpose: Pure business logic
      output: src/Server/Domain.fs
    - step: 3
      file: standards/backend/persistence-sqlite.md
      purpose: Database operations
      output: src/Server/Persistence.fs
    - step: 4
      file: standards/backend/api-implementation.md
      purpose: Implement API endpoints
      output: src/Server/Api.fs
---

# F# Backend Implementation

## When to Use This Skill

Activate when:
- User requests "implement backend for X"
- Need to add API endpoints
- Implementing business logic
- Creating server-side functionality
- Project has src/Server/ directory with Giraffe

## Architecture Overview

```
API (Fable.Remoting)          ← src/Server/Api.fs
    ↓ orchestrates
Validation (Input checking)   ← src/Server/Validation.fs
    ↓ validates
Domain (Pure logic, NO I/O)   ← src/Server/Domain.fs
    ↓ uses results from
Persistence (Database/File)   ← src/Server/Persistence.fs
```

## Implementation Workflow

### Step 1: Input Validation

**Read:** `standards/shared/validation.md`
**Create:** `src/Server/Validation.fs`

```fsharp
module Validation

let validateItem (item: Item) : Result<Item, string list> =
    let errors = [
        if String.IsNullOrWhiteSpace(item.Name) then "Name required"
        if item.Name.Length > 100 then "Name too long"
    ] |> List.choose id
    if errors.IsEmpty then Ok item else Error errors
```

**Key:** Return `Result<'T, string list>` for multiple errors

---

### Step 2: Domain Logic (Pure Functions)

**Read:** `standards/backend/domain-logic.md`
**Create:** `src/Server/Domain.fs`

```fsharp
module Domain

// ✅ PURE - no I/O, no side effects
let processItem (item: Item) : Item =
    { item with Name = item.Name.Trim() }

let calculateScore (items: Item list) : int =
    items |> List.sumBy (fun i -> i.Name.Length)
```

**CRITICAL:** NO I/O operations in Domain.fs!

---

### Step 3: Persistence Layer

**Read:** `standards/backend/persistence-sqlite.md`
**Create:** `src/Server/Persistence.fs`

```fsharp
module Persistence

open Dapper
open Microsoft.Data.Sqlite

let getConnection () = new SqliteConnection("Data Source=./data/app.db")

let getItems () : Async<Item list> = async {
    use conn = getConnection()
    let! items = conn.QueryAsync<Item>("SELECT * FROM Items") |> Async.AwaitTask
    return items |> Seq.toList
}
```

**Key:** Always `async`, use parameterized queries

---

### Step 4: API Implementation

**Read:** `standards/backend/api-implementation.md`
**Create:** `src/Server/Api.fs`

```fsharp
module Api

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api

let itemApi : IItemApi = {
    save = fun item -> async {
        // 1. Validate
        match Validation.validateItem item with
        | Error errs -> return Error (String.concat ", " errs)
        | Ok valid ->
            // 2. Domain logic (pure)
            let processed = Domain.processItem valid
            // 3. Persist
            do! Persistence.save processed
            return Ok processed
    }
}

let webApp =
    Remoting.createApi()
    |> Remoting.fromValue itemApi
    |> Remoting.buildHttpHandler
```

**Pattern:** Validate → Domain → Persist

---

## Quick Reference

### Standard API Endpoint Pattern

```fsharp
save = fun entity -> async {
    match Validation.validate entity with
    | Error errs -> return Error (String.concat ", " errs)
    | Ok valid ->
        let processed = Domain.process valid
        do! Persistence.save processed
        return Ok processed
}
```

### Not Found Pattern

```fsharp
getById = fun id -> async {
    match! Persistence.getById id with
    | Some item -> return Ok item
    | None -> return Error "Not found"
}
```

## Verification Checklist

- [ ] **Read workflow standards** (step 1-4 above)
- [ ] Validation in `src/Server/Validation.fs`
- [ ] Domain logic in `src/Server/Domain.fs` (PURE - no I/O!)
- [ ] Persistence in `src/Server/Persistence.fs`
- [ ] API implementation in `src/Server/Api.fs`
- [ ] All async operations for I/O
- [ ] Parameterized SQL queries
- [ ] Error handling with Result types
- [ ] `dotnet build` succeeds
- [ ] Tests written

## Common Pitfalls

See `standards/global/anti-patterns.md` for full list.

**Most Critical:**
- ❌ I/O in Domain.fs
- ❌ Skipping validation
- ❌ String concatenation in SQL
- ✅ Keep layers separated

## Related Skills

- **fsharp-validation** - Complex validation patterns
- **fsharp-persistence** - Advanced persistence patterns
- **fsharp-tests** - Testing backend logic
- **fsharp-shared** - Type definitions

## Detailed Documentation

For in-depth patterns and examples, refer to:
- `standards/backend/overview.md` - Architecture
- `standards/backend/api-implementation.md` - API patterns
- `standards/backend/domain-logic.md` - Pure functions
- `standards/backend/persistence-sqlite.md` - Database
- `standards/backend/error-handling.md` - Error patterns
