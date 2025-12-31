# Shared Types

> Domain type design patterns for client/server sharing.

## Overview

The `src/Shared/` project contains types used by both client (Fable) and server (Giraffe). This ensures type safety across the network boundary and eliminates manual JSON serialization.

## When to Use This

- Defining core business entities
- Creating value objects with validation
- Representing domain states with discriminated unions
- Ensuring client/server type agreement

## Patterns

### Core Entities

```fsharp
// src/Shared/Domain.fs
module Shared.Domain

open System

type Item = {
    Id: int
    Name: string
    Description: string
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

type ItemUpdate = {
    Name: string option
    Description: string option
}
```

### Value Objects with Validation

```fsharp
type EmailAddress = private EmailAddress of string

module EmailAddress =
    let create (email: string) : Result<EmailAddress, string> =
        if email.Contains("@") && email.Contains(".") then
            Ok (EmailAddress email)
        else
            Error "Invalid email format"

    let value (EmailAddress email) = email
```

### Discriminated Unions for States

```fsharp
type OrderStatus =
    | Pending
    | Confirmed of confirmedAt: DateTime
    | Shipped of trackingNumber: string
    | Delivered of deliveredAt: DateTime
    | Cancelled of reason: string
```

### Single Case Unions for Type Safety

```fsharp
type UserId = UserId of int
type ItemId = ItemId of int
type ProductId = ProductId of int

// Prevents mixing up IDs
let getUser (userId: UserId) : User = // ...
let getItem (itemId: ItemId) : Item = // ...

// Compiler prevents: getUser (ItemId 5)  // Error!
```

### Paged Results

```fsharp
type PagedResult<'T> = {
    Items: 'T list
    TotalCount: int
    Page: int
    PageSize: int
    HasNextPage: bool
    HasPreviousPage: bool
}

module PagedResult =
    let create items totalCount page pageSize =
        {
            Items = items
            TotalCount = totalCount
            Page = page
            PageSize = pageSize
            HasNextPage = (page * pageSize) < totalCount
            HasPreviousPage = page > 1
        }
```

## What NOT to Share

### Server-Only Types

Keep in `src/Server/`:
- Database entities with SQLite attributes
- Internal caching structures
- Server configuration
- File system paths
- Connection strings

```fsharp
// Server-only (src/Server/Types.fs)
type DbItem = {
    [<PrimaryKey; AutoIncrement>]
    Id: int
    Name: string
    // Internal metadata
    DbVersion: int
    LastSyncedAt: DateTime
}

// Map to shared type
let toSharedItem (dbItem: DbItem) : Item =
    { Id = dbItem.Id; Name = dbItem.Name; ... }
```

### Client-Only Types

Keep in `src/Client/Types.fs`:
- React component state
- UI-specific types (modals, toasts)
- Browser-specific types
- View models

```fsharp
// Client-only
type RemoteData<'T> =
    | NotAsked | Loading | Success of 'T | Failure of string

type Page =
    | HomePage | DetailPage of itemId: int | NotFound
```

## JSON Serialization

Fable.Remoting handles serialization automatically:

### DateTime

```fsharp
// Serializes as ISO 8601 string
type Item = { CreatedAt: DateTime }
```

### Option Types

```fsharp
// Option<'T> → null or value
type Update = {
    Name: string option  // null or "value"
    Age: int option      // null or 42
}
```

### Discriminated Unions

```fsharp
// DU → {"Case": "CaseName", "Fields": [...]}
type Status = Active | Inactive | Suspended of reason: string
```

## Anti-Patterns

### ❌ Circular Dependencies

```fsharp
// Domain depends on Api, Api depends on Domain → cycle!
// Solution: Keep in single file or separate properly
```

### ❌ Expose Implementation Details

```fsharp
// BAD
type Item = {
    DbId: int64  // SQLite-specific
    MongoId: string  // MongoDB-specific
}

// GOOD
type Item = { Id: string }
```

### ❌ Mutable Types

```fsharp
// BAD
type Item() =
    member val Name = "" with get, set

// GOOD
type Item = { Name: string }
```

## Checklist

- [ ] Types are immutable records or DUs
- [ ] No I/O or side effects
- [ ] No database-specific attributes
- [ ] Clear, meaningful names
- [ ] Option types for optional data
- [ ] Result types for operations that can fail

## See Also

- `api-contracts.md` - Fable.Remoting API patterns
- `validation.md` - Input validation
- `../backend/domain-logic.md` - Business logic
