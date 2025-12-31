---
name: fsharp-shared
description: |
  Define shared types and API contracts in src/Shared/ for F# full-stack apps.
  Use when defining domain models or API interfaces shared between client and server.
  Ensures type safety across the stack with Fable.Remoting contracts.
  Creates code in src/Shared/: Domain.fs, Api.fs.
allowed-tools: Read, Edit, Write, Grep, Glob
standards:
  required-reading:
    - standards/shared/types.md
    - standards/shared/api-contracts.md
  workflow:
    - step: 1
      file: standards/shared/types.md
      purpose: Define domain types
      output: src/Shared/Domain.fs
    - step: 2
      file: standards/shared/api-contracts.md
      purpose: Define API interfaces
      output: src/Shared/Api.fs
---

# Shared Types & API Contracts

## When to Use This Skill

Activate when:
- User requests "define types for X"
- Starting a new feature (ALWAYS define types first)
- Need to add API endpoints
- Defining domain models
- Project has src/Shared/ directory

## Architecture Overview

```
src/Shared/Domain.fs    - Domain types (shared by client & server)
src/Shared/Api.fs       - API contracts (Fable.Remoting interfaces)
```

**Key Principle:** Define types BEFORE implementing any logic

## Implementation Workflow

### Step 1: Define Domain Types

**Read:** `standards/shared/types.md`
**Edit:** `src/Shared/Domain.fs`

```fsharp
namespace Shared

open System

// Simple types
type Item = {
    Id: int
    Name: string
    Amount: decimal
    CreatedAt: DateTime
}

// Enums as Discriminated Unions
type Status =
    | Active
    | Inactive
    | Archived

// Complex types
type Transaction = {
    Id: int
    Amount: int64  // milliunits (YNAB convention)
    Date: DateTime
    Status: TransactionStatus
    CategoryId: string option  // optional
}

and TransactionStatus =
    | Imported
    | PendingReview
    | AutoCategorized
    | Uncategorized
```

**Key Points:**
- Use records for data structures
- Use discriminated unions for enums/variants
- Use `option` for nullable fields
- Keep DTOs simple (no methods)

---

### Step 2: Define API Contracts

**Read:** `standards/shared/api-contracts.md`
**Edit:** `src/Shared/Api.fs`

```fsharp
namespace Shared

open System

type IItemApi = {
    // Query operations
    getAll: unit -> Async<Item list>
    getById: int -> Async<Result<Item, string>>

    // Command operations
    save: Item -> Async<Result<Item, string>>
    delete: int -> Async<Result<unit, string>>
}

// Group related operations
type ITransactionApi = {
    getRecent: DateTime -> Async<Transaction list>
    categorize: int * string -> Async<Result<Transaction, string>>
}

// Root API
type IApi = {
    items: IItemApi
    transactions: ITransactionApi
}
```

**Pattern:**
- Return `Async<'T>` for all operations
- Use `Result<'T, string>` for fallible operations
- Group related operations in separate interfaces

---

## Quick Reference

### Domain Type Patterns

```fsharp
// Record (data structure)
type Entity = { Id: int; Name: string }

// Discriminated Union (variants)
type Status = Active | Pending | Completed

// Optional fields
type User = { Id: int; Email: string option }

// Nested types
type Order = { Items: OrderItem list }
and OrderItem = { ProductId: int; Quantity: int }
```

### API Contract Patterns

```fsharp
// Read operations (no Result needed)
getAll: unit -> Async<'T list>
getById: 'Id -> Async<'T option>

// Write operations (use Result for validation errors)
save: 'T -> Async<Result<'T, string>>
update: 'T -> Async<Result<'T, string>>
delete: 'Id -> Async<Result<unit, string>>

// Complex operations
process: Input -> Async<Result<Output, string>>
```

## Verification Checklist

- [ ] **Read standards** (types.md, api-contracts.md)
- [ ] Types in `src/Shared/Domain.fs`
- [ ] API contracts in `src/Shared/Api.fs`
- [ ] Records for data structures
- [ ] Discriminated unions for enums
- [ ] All API operations return `Async<'T>`
- [ ] Fallible operations use `Result<'T, string>`
- [ ] No business logic in types (pure data)
- [ ] `dotnet build` succeeds

## Common Pitfalls

**Most Critical:**
- ❌ Starting implementation before defining types
- ❌ Putting logic in Domain.fs (only types!)
- ❌ Using `null` instead of `option`
- ❌ Forgetting `Async<>` in API contracts
- ✅ Define types first, implement later
- ✅ Keep Domain.fs pure (no functions)
- ✅ Use option for nullable fields

## Related Skills

- **fsharp-feature** - Uses types in full-stack workflow
- **fsharp-backend** - Implements API contracts
- **fsharp-frontend** - Uses domain types in state
- **fsharp-validation** - Validates domain types

## Detailed Documentation

For complete patterns and examples:
- `standards/shared/types.md` - Domain type patterns
- `standards/shared/api-contracts.md` - Fable.Remoting contracts
- `standards/shared/validation.md` - Validation patterns
