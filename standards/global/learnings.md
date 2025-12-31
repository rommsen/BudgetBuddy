# Learnings

> Consolidated best practices and insights from F# full-stack development.

## Why This Stack Works

| Layer | Choice | Rationale |
|-------|--------|-----------|
| Frontend | Elmish + Feliz | MVU guarantees predictable state, F# catches bugs at compile time |
| Backend | Giraffe + Fable.Remoting | Type-safe RPC eliminates API contract drift |
| Database | SQLite + Dapper | Simple, embedded, no infrastructure |
| Networking | Tailscale | Zero-config private networking |

## Patterns That Work

### Domain Layer

**Pure Functions Only**
```fsharp
let completeTodo (item: TodoItem) : TodoItem =
    { item with Status = Completed; UpdatedAt = DateTime.UtcNow }
```

### Backend Layer

**API Orchestration Pattern**
```fsharp
let api = {
    save = fun entity -> async {
        match Validation.validate entity with
        | Error errs -> return Error (String.concat ", " errs)
        | Ok valid ->
            let processed = Domain.process valid
            do! Persistence.save processed
            return Ok processed
    }
}
```

### Frontend Layer

**Optimistic UI Updates**
```fsharp
| CategorizeTransaction (txId, categoryId) ->
    let updatedModel = updateLocally model txId categoryId
    updatedModel, Cmd.OfAsync.either Api.api.categorize (txId, categoryId) ...
```

**Version-Based Debouncing**
```fsharp
type Model = { PendingVersions: Map<TransactionId, int> }

| CommitChange (id, value, version) when model.PendingVersions.[id] = version ->
    // Only commit if this is the latest version
```

## Performance Learnings

### Frontend

**Pre-compute Expensive Operations**
```fsharp
// BAD: Computed 30,880 times
for tx in transactions do
    let options = categories |> List.map formatOption

// GOOD: Computed once
let categoryOptions = categories |> List.map formatOption
for tx in transactions do
    renderWithOptions tx categoryOptions
```

**Reduce DOM for Inactive Elements**
```fsharp
if tx.Status = Skipped then
    Html.span [ prop.text categoryName ]  // Simple
else
    Input.searchableSelect props  // Full component
```

### Backend

- SQLite in WAL mode for concurrent reads
- Async for all I/O operations
- Parameterized queries (prepared statements are cached)
- Batch operations where possible

## Testing Insights

**Test Actual Behavior**
```fsharp
// BAD - Tautology
Expect.equal entity.Id 1 "Should be 1"

// GOOD - Tests actual behavior
let result = Validation.validate { Email = "  TEST@EXAMPLE.COM  " }
Expect.equal (Result.map (fun v -> v.Email) result) (Ok "test@example.com")
```

## Key Insights Summary

1. **Type safety prevents bugs** - Invest in types upfront
2. **Pure domain logic is testable** - Keep I/O at boundaries
3. **Explicit error handling** - Use Result types, never ignore errors
4. **Test actual behavior** - Avoid tautologies
5. **Document everything** - Future you will thank present you
6. **Regression tests are mandatory** - Bugs without tests come back
7. **Mobile-first UI** - Desktop users can use mobile UI
8. **Performance matters** - Pre-compute, debounce, reduce DOM
9. **Simple infrastructure** - SQLite + Tailscale eliminates complexity
10. **Milestone-based development** - Structured progress with QA gates

## Dapper with F# Options Template

```fsharp
type OptionHandler<'T>() =
    inherit SqlMapper.TypeHandler<'T option>()

    override _.SetValue(param, value) =
        param.Value <- match value with Some v -> box v | None -> box DBNull.Value

    override _.Parse(value) =
        if isNull value || value = box DBNull.Value then None
        else Some (unbox<'T> value)

// Register at startup
SqlMapper.AddTypeHandler(OptionHandler<string>())
SqlMapper.AddTypeHandler(OptionHandler<int>())

// Row types need [<CLIMutable>]
[<CLIMutable>]
type EntityRow = { Id: int; Name: string; Description: string }
```

## See Also

- `anti-patterns.md` - What to avoid
- `quick-reference.md` - Code templates
