# Anti-Patterns

> Common mistakes to avoid in F# full-stack development.

## Critical Bugs Found in Production

| Bug | Root Cause | Lesson |
|-----|------------|--------|
| JSON encoding as string | `Encode.int64` outputs strings in JS | Use `Encode.int` for safe range |
| Stale reference in mutable state | Captured old value, then mutated | Re-read state after modifications |
| Format mismatch in ID matching | `BB:` vs `BUDGETBUDDY:` prefixes | Centralize format constants |
| Encryption key changes with Docker | Key derived from hostname | Use env var for stable key |
| DB not initialized on deploy | `initializeDatabase()` never called | Initialize in `Program.fs` at startup |

## Design Anti-Patterns

### 1. I/O in Domain.fs

```fsharp
// BAD
let completeTodo itemId =
    let item = Persistence.getItem itemId  // I/O in domain!
    { item with Status = Completed }

// GOOD
let completeTodo (item: TodoItem) : TodoItem =
    { item with Status = Completed; UpdatedAt = DateTime.UtcNow }
```

### 2. Ignoring Result Types

```fsharp
// BAD
let item = result.Value  // Throws if Error!

// GOOD
match result with
| Ok item -> // use item
| Error err -> // handle error
```

### 3. Classes for Domain Types

```fsharp
// BAD
type Item() =
    member val Name = "" with get, set

// GOOD
type Item = { Name: string }
```

### 4. Skipping Validation

```fsharp
// BAD
let saveItem item = Persistence.save item  // No validation!

// GOOD
let saveItem item =
    match Validation.validate item with
    | Ok valid -> Persistence.save valid
    | Error errs -> Error (String.concat ", " errs)
```

### 5. Tests Writing to Production Database

```fsharp
// BAD
let connectionString = "Data Source=./data/production.db"

// GOOD
let connectionString =
    if isTestMode then "Data Source=:memory:"
    else "Data Source=./data/app.db"
```

### 6. Bug Fixes Without Regression Tests

Every bug fix MUST include a test that would have caught it.

### 7. Hover-Only Actions

Mobile users can't hover. Provide visible interaction points.

### 8. Missing React Keys

```fsharp
// BAD
for item in items do
    Html.div [ prop.text item.Name ]

// GOOD
for item in items do
    Html.div [
        prop.key (string item.Id)
        prop.text item.Name
    ]
```

## Fable/JavaScript Gotchas

### Format Strings

```fsharp
// BAD - Fable transpiles incorrectly
let bad = sprintf "%.2f" amount

// GOOD
let good = System.Math.Round(float amount, 2).ToString("0.00")
```

### int64 Serialization

```fsharp
// BAD - Produces string "12345"
Encode.int64 value

// GOOD - Produces number 12345
Encode.int value
```

## Common Gotchas

| Issue | Symptom | Solution |
|-------|---------|----------|
| F# Options with Dapper | `null` causes exceptions | Register `OptionHandler<'T>` |
| `[<CLIMutable>]` missing | Dapper can't populate records | Add attribute to row types |
| React keys missing | List re-renders slowly | Add `prop.key` to list items |
| Encryption key in Docker | Settings lost after rebuild | Use env var |
| DB not initialized | "no such table" error | Call `initializeDatabase()` at startup |
| Double-click bugs | Race conditions | Add loading state, disable button |

## See Also

- `development-workflow.md` - Correct patterns
- `quick-reference.md` - Working examples
