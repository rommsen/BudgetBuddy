# API Contracts

> Fable.Remoting API patterns for type-safe client/server communication.

## Overview

Define your API surface in `src/Shared/Api.fs` using F# record types. Fable.Remoting ensures compile-time type safety between client and server.

## When to Use This

- Defining API endpoints
- Creating CRUD operations
- Implementing command/query separation
- Versioning APIs

## Patterns

### Single API Interface

```fsharp
// src/Shared/Api.fs
module Shared.Api

open Domain

type IItemApi = {
    // Queries
    getItems: unit -> Async<Item list>
    getItem: int -> Async<Result<Item, string>>
    searchItems: SearchRequest -> Async<PagedResult<Item>>

    // Commands
    createItem: CreateItemRequest -> Async<Result<Item, string>>
    updateItem: UpdateItemRequest -> Async<Result<Item, string>>
    deleteItem: int -> Async<Result<unit, string>>

    // Bulk operations
    importItems: Item list -> Async<Result<int, string>>
}
```

### Multiple API Interfaces

```fsharp
type IUserApi = {
    getCurrentUser: unit -> Async<User>
    updateProfile: User -> Async<Result<User, string>>
}

type IStatsApi = {
    getDashboard: unit -> Async<DashboardSummary>
    getItemStats: int -> Async<ItemStats>
}
```

### Command/Query Separation

```fsharp
// Commands (write) - return Result
type ICommandApi = {
    createItem: CreateItemRequest -> Async<Result<Item, string>>
    updateItem: UpdateItemRequest -> Async<Result<Item, string>>
    deleteItem: int -> Async<Result<unit, string>>
}

// Queries (read) - return data directly or Option
type IQueryApi = {
    getItems: unit -> Async<Item list>
    getItem: int -> Async<Item option>
    searchItems: string -> Async<Item list>
}
```

## Versioning Strategy

### Option 1: Add Optional Fields

```fsharp
// V1
type Item = { Id: int; Name: string }

// V2 - Backward compatible
type Item = {
    Id: int
    Name: string
    Description: string option  // New field
}
```

### Option 2: Separate API Versions

```fsharp
module V1 =
    type Item = { Id: int; Name: string }
    type IItemApi = { getItems: unit -> Async<Item list> }

module V2 =
    type Item = { Id: int; Name: string; Description: string }
    type IItemApi = {
        getItems: unit -> Async<Item list>
        getItemsWithDetails: unit -> Async<Item list>
    }

// In server
let apiV1 = Remoting.createApi() |> Remoting.withRouteBuilder (fun t m -> $"/api/v1/{t}/{m}") ...
let apiV2 = Remoting.createApi() |> Remoting.withRouteBuilder (fun t m -> $"/api/v2/{t}/{m}") ...
```

### Option 3: Feature Flags

```fsharp
type ApiFeatures = {
    SupportsAdvancedSearch: bool
    SupportsExport: bool
}

type IAppApi = {
    getFeatures: unit -> Async<ApiFeatures>
    getItems: unit -> Async<Item list>
    advancedSearch: SearchRequest -> Async<PagedResult<Item>>  // Only if SupportsAdvancedSearch
}
```

## Checklist

- [ ] All API methods return `Async<'T>`
- [ ] Commands return `Result<'T, string>` for errors
- [ ] Queries return data directly or `Option<'T>`
- [ ] Parameter types are defined in Domain.fs
- [ ] Method names are clear and descriptive
- [ ] No I/O or business logic in contract

## See Also

- `types.md` - Domain type patterns
- `../backend/api-implementation.md` - Server implementation
- `../frontend/state-management.md` - Client usage
