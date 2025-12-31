---
name: fsharp-persistence
description: |
  Implement persistence layer using SQLite (Dapper) or file storage in F#.
  Use when adding database operations, file I/O, or data storage.
  Ensures proper separation: all I/O isolated in Persistence.fs, away from pure domain logic.
  Creates code in src/Server/Persistence.fs.
allowed-tools: Read, Edit, Write, Grep, Glob, Bash
standards:
  required-reading:
    - standards/backend/persistence-sqlite.md
  optional:
    - standards/backend/persistence-files.md
  workflow:
    - step: 1
      file: standards/backend/persistence-sqlite.md
      purpose: Database operations with Dapper
      output: src/Server/Persistence.fs
    - step: 2
      file: standards/backend/persistence-files.md
      purpose: File storage (if needed)
      output: src/Server/Persistence.fs
---

# F# Persistence Layer

## When to Use This Skill

Activate when:
- User requests "add database operations"
- Need to store/retrieve data
- Implementing file storage
- Adding CRUD operations
- Project has src/Server/Persistence.fs

## Persistence Options

1. **SQLite + Dapper** - Recommended for structured data
2. **File Storage** - For files, logs, event sourcing

## Implementation Workflow

### Step 1: SQLite Persistence

**Read:** `standards/backend/persistence-sqlite.md`
**Edit:** `src/Server/Persistence.fs`

```fsharp
module Persistence

open Dapper
open Microsoft.Data.Sqlite
open System.IO

// Connection factory
let private getConnection () =
    let dataDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "./data"
    Directory.CreateDirectory(dataDir) |> ignore
    let connStr = $"Data Source={dataDir}/app.db"
    new SqliteConnection(connStr)

// Setup tables
let ensureTables () = async {
    use conn = getConnection()
    do! conn.ExecuteAsync("""
        CREATE TABLE IF NOT EXISTS Items (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Amount DECIMAL NOT NULL,
            CreatedAt TEXT NOT NULL
        )
    """) |> Async.AwaitTask |> Async.Ignore
}

// Read operations
let getItems () : Async<Item list> = async {
    use conn = getConnection()
    let! items = conn.QueryAsync<Item>("SELECT * FROM Items ORDER BY CreatedAt DESC")
                 |> Async.AwaitTask
    return items |> Seq.toList
}

let getById (id: int) : Async<Item option> = async {
    use conn = getConnection()
    let! item = conn.QueryFirstOrDefaultAsync<Item>(
                    "SELECT * FROM Items WHERE Id = @Id",
                    {| Id = id |})
                |> Async.AwaitTask
    return if isNull (box item) then None else Some item
}

// Write operations
let save (item: Item) : Async<unit> = async {
    use conn = getConnection()
    if item.Id = 0 then
        // Insert
        do! conn.ExecuteAsync(
                "INSERT INTO Items (Name, Amount, CreatedAt) VALUES (@Name, @Amount, @CreatedAt)",
                item)
            |> Async.AwaitTask |> Async.Ignore
    else
        // Update
        do! conn.ExecuteAsync(
                "UPDATE Items SET Name = @Name, Amount = @Amount WHERE Id = @Id",
                item)
            |> Async.AwaitTask |> Async.Ignore
}

let delete (id: int) : Async<unit> = async {
    use conn = getConnection()
    do! conn.ExecuteAsync("DELETE FROM Items WHERE Id = @Id", {| Id = id |})
        |> Async.AwaitTask |> Async.Ignore
}
```

**Key Points:**
- Always `async` for I/O
- Parameterized queries (never string concat!)
- `use` for connection disposal
- Check `DATA_DIR` environment variable

---

### Step 2: File Persistence (Optional)

**Read:** `standards/backend/persistence-files.md`
**When:** Event sourcing, logs, or file-based data

```fsharp
module FilePersistence

open System.IO
open System.Text.Json

let private dataDir =
    let dir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "./data"
    Directory.CreateDirectory(dir) |> ignore
    dir

let saveToFile<'T> (filename: string) (data: 'T) : Async<unit> = async {
    let path = Path.Combine(dataDir, filename)
    let json = JsonSerializer.Serialize(data)
    do! File.WriteAllTextAsync(path, json) |> Async.AwaitTask
}

let loadFromFile<'T> (filename: string) : Async<'T option> = async {
    let path = Path.Combine(dataDir, filename)
    if File.Exists(path) then
        let! json = File.ReadAllTextAsync(path) |> Async.AwaitTask
        return Some (JsonSerializer.Deserialize<'T>(json))
    else
        return None
}
```

---

## Quick Reference

### SQLite Patterns

```fsharp
// Query single
conn.QueryFirstOrDefaultAsync<'T>(sql, params)

// Query list
conn.QueryAsync<'T>(sql, params) |> Seq.toList

// Execute (INSERT, UPDATE, DELETE)
conn.ExecuteAsync(sql, params)

// Parameters (always use!)
{| Id = id; Name = name |}
```

### Common Queries

```fsharp
// Get all
"SELECT * FROM Table ORDER BY CreatedAt DESC"

// Get by ID
"SELECT * FROM Table WHERE Id = @Id"

// Insert
"INSERT INTO Table (Col1, Col2) VALUES (@Col1, @Col2)"

// Update
"UPDATE Table SET Col = @Col WHERE Id = @Id"

// Delete
"DELETE FROM Table WHERE Id = @Id"
```

## Verification Checklist

- [ ] **Read standards** (persistence-sqlite.md)
- [ ] Connection factory with DATA_DIR support
- [ ] `ensureTables()` for schema setup
- [ ] All functions return `Async<'T>`
- [ ] Parameterized queries (NO string concat!)
- [ ] `use` for connection disposal
- [ ] Error handling for missing records
- [ ] `dotnet build` succeeds
- [ ] Integration tests written

## Common Pitfalls

**Most Critical:**
- ❌ SQL injection (string concat)
- ❌ Forgetting `async` wrapper
- ❌ Not disposing connections
- ❌ Hardcoded connection strings
- ✅ Parameterized queries
- ✅ Environment variable for DATA_DIR
- ✅ `use` for disposables

## Related Skills

- **fsharp-backend** - Uses persistence in API
- **fsharp-tests** - Testing persistence layer
- **fsharp-feature** - Full-stack workflow

## Detailed Documentation

For complete patterns and examples:
- `standards/backend/persistence-sqlite.md` - SQLite + Dapper
- `standards/backend/persistence-files.md` - File storage
- `standards/backend/error-handling.md` - Error patterns
