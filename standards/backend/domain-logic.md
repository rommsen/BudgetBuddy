# Domain Logic

> Pure business logic patterns with no I/O.

## Overview

Keep all business logic pure in `Domain.fs`. No I/O operations, no side effects - just pure transformations. This makes domain logic trivially testable.

## When to Use This

- Implementing business rules
- Processing data transformations
- Calculating derived values
- Event sourcing patterns

## Patterns

### Pure Business Logic

```fsharp
// src/Server/Domain.fs
module Domain

open System
open Shared.Domain

let processItem (item: Item) : Item =
    { item with Name = item.Name.Trim() }

let calculateItemScore (item: Item) : int =
    item.Name.Length * 10

let isItemValid (item: Item) : bool =
    not (String.IsNullOrWhiteSpace item.Name) &&
    item.Name.Length >= 3
```

### Business Operations

```fsharp
let processItemList (items: Item list) : Item list =
    items
    |> List.filter isItemValid
    |> List.map processItem
    |> List.sortBy (fun item -> item.Name)

let aggregateStats (items: Item list) : ItemStats =
    {
        TotalCount = items.Length
        AverageScore =
            if items.Length > 0 then
                items |> List.averageBy (calculateItemScore >> float)
            else
                0.0
    }
```

### Domain Events (Event Sourcing)

```fsharp
type ItemEvent =
    | ItemCreated of Item
    | ItemUpdated of Item
    | ItemDeleted of itemId: int
    | ItemsImported of Item list

let applyEvent (state: Item list) (event: ItemEvent) : Item list =
    match event with
    | ItemCreated item ->
        item :: state

    | ItemUpdated item ->
        state |> List.map (fun i ->
            if i.Id = item.Id then item else i
        )

    | ItemDeleted itemId ->
        state |> List.filter (fun i -> i.Id <> itemId)

    | ItemsImported items ->
        items @ state

let replayEvents (events: ItemEvent list) : Item list =
    events |> List.fold applyEvent []
```

## Anti-Patterns

### ❌ I/O in Domain

```fsharp
// BAD - I/O operation in domain
let completeTodo itemId =
    let item = Persistence.getItem itemId  // NO!
    { item with Status = Completed }

// GOOD - Pure function
let completeTodo (item: TodoItem) : TodoItem =
    { item with Status = Completed; UpdatedAt = DateTime.UtcNow }
```

### ❌ Side Effects

```fsharp
// BAD - Logging/printing in domain
let processItem item =
    printfn "Processing: %s" item.Name  // NO!
    { item with Name = item.Name.Trim() }

// GOOD - Pure transformation
let processItem item =
    { item with Name = item.Name.Trim() }
```

## Separation of Concerns

```fsharp
// ✅ Good separation
module Domain =
    let calculatePrice (item: Item) (discount: Discount) : decimal =
        item.Price * (1.0m - discount.Percentage)

module Api =
    let checkout itemId discountCode = async {
        let! item = Persistence.getItem itemId
        let! discount = Persistence.getDiscount discountCode

        let finalPrice = Domain.calculatePrice item discount

        do! Persistence.recordTransaction itemId finalPrice
        return Ok finalPrice
    }
```

## Checklist

- [ ] No I/O operations (database, files, network)
- [ ] No side effects (logging, printing)
- [ ] Pure functions only
- [ ] All business rules in Domain.fs
- [ ] Easily testable
- [ ] No dependencies on Persistence or API modules

## See Also

- `api-implementation.md` - API orchestration
- `../shared/types.md` - Domain types
- `../testing/domain-tests.md` - Testing pure functions
