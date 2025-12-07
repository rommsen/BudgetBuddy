---
name: fsharp-persistence
description: |
  Implement data persistence using SQLite with Dapper, JSON file storage, or event sourcing patterns.
  Use when adding database tables, CRUD operations, file storage, or event logs.
  Creates code in src/Server/Persistence.fs with patterns for queries, transactions, relationships, and async I/O.
  Includes SQLite schema creation, parameterized queries, and proper connection management.
allowed-tools: Read, Edit, Write, Grep, Bash
---

# F# Persistence Patterns

## When to Use This Skill

Activate when:
- User requests "add database table", "save X to database"
- Implementing CRUD operations
- Need file-based storage
- Implementing event sourcing or audit logs
- Creating persistence layer for domain entities

## SQLite with Dapper

### Setup with Test-Isolation Support

**Location:** `src/Server/Persistence.fs`

**CRITICAL**: Always use lazy loading for database configuration to support test-isolation via environment variables.

```fsharp
module Persistence

open System
open System.IO
open Microsoft.Data.Sqlite
open Dapper
open Shared.Domain

let private dataDir = "./data"

// Use lazy loading to allow tests to set USE_MEMORY_DB before first access
type private DbConfig = {
    IsTestMode: bool
    ConnectionString: string
    SharedConnection: SqliteConnection option
}

let private dbConfig = lazy (
    let isTestMode =
        match Environment.GetEnvironmentVariable("USE_MEMORY_DB") with
        | "true" | "1" -> true
        | _ -> false

    let connectionString =
        if isTestMode then
            // In-memory SQLite with shared cache (stays alive across connections)
            "Data Source=:memory:;Mode=Memory;Cache=Shared"
        else
            let dbPath = Path.Combine(dataDir, "app.db")
            $"Data Source={dbPath}"

    // For In-Memory: Keep a shared connection alive
    let sharedConnection =
        if isTestMode then
            let conn = new SqliteConnection(connectionString)
            conn.Open()
            Some conn
        else
            None

    { IsTestMode = isTestMode; ConnectionString = connectionString; SharedConnection = sharedConnection }
)

let private getConnection () =
    let config = dbConfig.Force()
    match config.SharedConnection with
    | Some conn -> conn  // Shared connection for tests - don't dispose!
    | None -> new SqliteConnection(config.ConnectionString)

let ensureDataDir () =
    if not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore
```

### Why Lazy Loading?

F# modules are initialized when the assembly loads, **before** `Main()` runs. Without lazy loading:
1. Tests cannot set `USE_MEMORY_DB` before the connection string is evaluated
2. Tests will write to the production database
3. `open TestSetup` before `open Persistence` does NOT help - F# initializes by dependency graph, not `open` order

### Connection Management for Tests

**IMPORTANT**: For In-Memory SQLite, don't use `use conn = getConnection()`:

```fsharp
// ❌ WRONG - Connection disposed, In-Memory DB lost
let getAllItems () = async {
    use conn = getConnection()  // Disposed at end = DB gone!
    // ...
}

// ✅ CORRECT - Don't dispose shared connection
let getAllItems () = async {
    let conn = getConnection()  // No dispose for shared connection
    // ...
}
```

In-Memory SQLite databases only exist while the connection is open. For test isolation, we keep one shared connection alive for the entire test run.

### Initialize Database

```fsharp
let initializeDatabase () =
    ensureDataDir()
    use conn = getConnection()

    // Create tables
    conn.Execute("""
        CREATE TABLE IF NOT EXISTS TodoItems (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL,
            Description TEXT,
            Priority INTEGER NOT NULL,
            Status INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        )
    """) |> ignore

    // Create indexes
    conn.Execute("""
        CREATE INDEX IF NOT EXISTS idx_todos_status
        ON TodoItems(Status)
    """) |> ignore
```

### CRUD Operations

#### Read All
```fsharp
let getAllTodos () : Async<TodoItem list> =
    async {
        use conn = getConnection()
        let! todos = conn.QueryAsync<TodoItem>(
            "SELECT * FROM TodoItems ORDER BY CreatedAt DESC"
        ) |> Async.AwaitTask
        return todos |> Seq.toList
    }
```

#### Read with Filter
```fsharp
let getActiveTodos () : Async<TodoItem list> =
    async {
        use conn = getConnection()
        let! todos = conn.QueryAsync<TodoItem>(
            "SELECT * FROM TodoItems WHERE Status = @Status",
            {| Status = int Active |}
        ) |> Async.AwaitTask
        return todos |> Seq.toList
    }
```

#### Read Single
```fsharp
let getTodoById (id: int) : Async<TodoItem option> =
    async {
        use conn = getConnection()
        let! todo = conn.QuerySingleOrDefaultAsync<TodoItem>(
            "SELECT * FROM TodoItems WHERE Id = @Id",
            {| Id = id |}
        ) |> Async.AwaitTask
        return if isNull (box todo) then None else Some todo
    }
```

#### Insert
```fsharp
let insertTodo (todo: TodoItem) : Async<TodoItem> =
    async {
        use conn = getConnection()
        let! id = conn.ExecuteScalarAsync<int64>(
            """INSERT INTO TodoItems (Title, Description, Priority, Status, CreatedAt, UpdatedAt)
               VALUES (@Title, @Description, @Priority, @Status, @CreatedAt, @UpdatedAt)
               RETURNING Id""",
            {|
                Title = todo.Title
                Description = todo.Description
                Priority = int todo.Priority
                Status = int todo.Status
                CreatedAt = todo.CreatedAt.ToString("o")
                UpdatedAt = todo.UpdatedAt.ToString("o")
            |}
        ) |> Async.AwaitTask
        return { todo with Id = int id }
    }
```

#### Update
```fsharp
let updateTodo (todo: TodoItem) : Async<unit> =
    async {
        use conn = getConnection()
        let! _ = conn.ExecuteAsync(
            """UPDATE TodoItems
               SET Title = @Title, Description = @Description,
                   Priority = @Priority, Status = @Status, UpdatedAt = @UpdatedAt
               WHERE Id = @Id""",
            todo
        ) |> Async.AwaitTask
        return ()
    }
```

#### Delete
```fsharp
let deleteTodo (id: int) : Async<unit> =
    async {
        use conn = getConnection()
        let! _ = conn.ExecuteAsync(
            "DELETE FROM TodoItems WHERE Id = @Id",
            {| Id = id |}
        ) |> Async.AwaitTask
        return ()
    }
```

### Transactions

```fsharp
let transferTodo (todoId: int) (fromListId: int) (toListId: int) : Async<Result<unit, string>> =
    async {
        use conn = getConnection()
        use transaction = conn.BeginTransaction()

        try
            let! _ = conn.ExecuteAsync(
                "DELETE FROM TodoListItems WHERE TodoId = @TodoId AND ListId = @ListId",
                {| TodoId = todoId; ListId = fromListId |},
                transaction
            ) |> Async.AwaitTask

            let! _ = conn.ExecuteAsync(
                "INSERT INTO TodoListItems (TodoId, ListId) VALUES (@TodoId, @ListId)",
                {| TodoId = todoId; ListId = toListId |},
                transaction
            ) |> Async.AwaitTask

            transaction.Commit()
            return Ok ()
        with
        | ex ->
            transaction.Rollback()
            return Error $"Transfer failed: {ex.Message}"
    }
```

## JSON File Persistence

### Single File

```fsharp
open System.IO
open System.Text.Json

let dataDir = "./data"
let todosFile = Path.Combine(dataDir, "todos.json")

let ensureDataDir () =
    if not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore

let loadTodos () : Async<TodoItem list> =
    async {
        ensureDataDir()
        if File.Exists todosFile then
            let! json = File.ReadAllTextAsync todosFile |> Async.AwaitTask
            let options = JsonSerializerOptions()
            options.PropertyNameCaseInsensitive <- true
            return JsonSerializer.Deserialize<TodoItem list>(json, options)
        else
            return []
    }

let saveTodos (todos: TodoItem list) : Async<unit> =
    async {
        ensureDataDir()
        let options = JsonSerializerOptions(WriteIndented = true)
        let json = JsonSerializer.Serialize(todos, options)
        do! File.WriteAllTextAsync(todosFile, json) |> Async.AwaitTask
    }

// CRUD operations
let addTodo (todo: TodoItem) : Async<TodoItem> =
    async {
        let! todos = loadTodos()
        let newId = if todos.IsEmpty then 1 else (todos |> List.map (fun t -> t.Id) |> List.max) + 1
        let newTodo = { todo with Id = newId }
        do! saveTodos (newTodo :: todos)
        return newTodo
    }

let updateTodo (todo: TodoItem) : Async<unit> =
    async {
        let! todos = loadTodos()
        let updated = todos |> List.map (fun t -> if t.Id = todo.Id then todo else t)
        do! saveTodos updated
    }
```

## Event Sourcing

### Event Types
```fsharp
type TodoEvent =
    | TodoCreated of TodoItem
    | TodoUpdated of TodoItem
    | TodoCompleted of todoId: int
    | TodoDeleted of todoId: int

type EventEnvelope = {
    Id: System.Guid
    Timestamp: System.DateTime
    Event: TodoEvent
}
```

### Append Event
```fsharp
let eventLogFile = Path.Combine(dataDir, "events.jsonl")

let appendEvent (event: TodoEvent) : Async<unit> =
    async {
        ensureDataDir()
        let envelope = {
            Id = System.Guid.NewGuid()
            Timestamp = System.DateTime.UtcNow
            Event = event
        }
        let json = JsonSerializer.Serialize(envelope)
        do! File.AppendAllTextAsync(eventLogFile, json + "\n") |> Async.AwaitTask
    }
```

### Rebuild State
```fsharp
let readAllEvents () : Async<EventEnvelope list> =
    async {
        if File.Exists eventLogFile then
            let! lines = File.ReadAllLinesAsync eventLogFile |> Async.AwaitTask
            return
                lines
                |> Array.filter (not << String.IsNullOrWhiteSpace)
                |> Array.map (fun line -> JsonSerializer.Deserialize<EventEnvelope>(line))
                |> Array.toList
        else
            return []
    }

let rebuildState () : Async<Map<int, TodoItem>> =
    async {
        let! events = readAllEvents()

        let applyEvent state envelope =
            match envelope.Event with
            | TodoCreated todo -> Map.add todo.Id todo state
            | TodoUpdated todo -> Map.add todo.Id todo state
            | TodoCompleted todoId ->
                state
                |> Map.tryFind todoId
                |> Option.map (fun todo -> Map.add todoId { todo with Status = Completed } state)
                |> Option.defaultValue state
            | TodoDeleted todoId -> Map.remove todoId state

        return events |> List.fold applyEvent Map.empty
    }
```

## Best Practices

### ✅ Do
- Use parameterized queries (SQL injection prevention)
- Create indexes for frequently queried columns
- Use transactions for multi-step operations
- Always use `async` for I/O
- Use `lazy` for database configuration (test isolation!)
- Store DateTimes as ISO 8601 strings
- Return options for "not found" cases
- Support `USE_MEMORY_DB` environment variable for tests

### ❌ Don't
- Use string concatenation for queries
- Use static connection strings evaluated at module load
- Use JSON files for large datasets (use SQLite)
- Use JSON files for high-frequency writes
- Modify past events in event sourcing
- Rebuild from events every time (use snapshots)
- Write tests that persist to production database

## Verification Checklist

- [ ] Data directory created
- [ ] Database initialized (if using SQLite)
- [ ] Parameterized queries used
- [ ] Proper error handling
- [ ] Async operations used
- [ ] Lazy loading for DB configuration (test isolation)
- [ ] `USE_MEMORY_DB` environment variable supported
- [ ] Indexes created for queries
- [ ] Transactions used for multi-step operations
- [ ] DateTime serialization handled
- [ ] Tests use in-memory SQLite (don't pollute production DB)

## Related Skills

- **fsharp-backend** - Integration with API
- **fsharp-shared** - Type definitions
- **fsharp-tests** - Testing persistence

## Related Documentation

- `/docs/05-PERSISTENCE.md` - Detailed persistence guide
