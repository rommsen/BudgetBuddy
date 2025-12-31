# Persistence Tests

> Testing with in-memory SQLite.

## Overview

Use in-memory SQLite for persistence tests. Set `USE_MEMORY_DB=true` environment variable to enable test mode.

## When to Use This

- Testing database operations
- Testing queries
- Testing migrations
- Integration testing

## Patterns

### Setup Test Mode

```fsharp
// In test setup
Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

// Initialize database for tests
Persistence.initializeDatabase()
```

### Persistence Tests

```fsharp
[<Tests>]
let tests =
    testList "Persistence" [
        testCase "save and retrieve item" <| fun () ->
            let item = { Id = 0; Name = "Test" }
            
            let saved =
                Persistence.saveItem item
                |> Async.RunSynchronously
            
            let retrieved =
                Persistence.getItemById saved.Id
                |> Async.RunSynchronously
            
            Expect.isSome retrieved "Should retrieve item"
            Expect.equal retrieved.Value.Name "Test" "Name should match"
    ]
```

## Checklist

- [ ] USE_MEMORY_DB environment variable set
- [ ] Database initialized in tests
- [ ] CRUD operations tested
- [ ] Queries tested
- [ ] No production database writes

## See Also

- `../backend/persistence-sqlite.md` - SQLite patterns
