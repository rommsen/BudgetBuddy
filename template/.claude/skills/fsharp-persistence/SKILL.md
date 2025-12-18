---
name: fsharp-persistence
description: |
  Implement data persistence using SQLite with Dapper, JSON file storage, or event sourcing patterns.
  Use when adding database tables, CRUD operations, or file storage.
  Creates code in src/Server/Persistence.fs with patterns for queries, transactions, and async I/O.
---

# F# Persistence Patterns

## When to Use This Skill

Activate when:
- User requests "add database table", "save X to database"
- Implementing CRUD operations
- Need file-based storage

## SQLite with Dapper

### Setup with Test Isolation

**CRITICAL**: Use lazy loading for test isolation via `USE_MEMORY_DB` environment variable.

```fsharp
module Persistence

let private dbConfig = lazy (
    let isTestMode =
        match Environment.GetEnvironmentVariable("USE_MEMORY_DB") with
        | "true" | "1" -> true
        | _ -> false

    if isTestMode then "Data Source=:memory:;Mode=Memory;Cache=Shared"
    else $"Data Source=./data/app.db"
)

let private getConnection () = new SqliteConnection(dbConfig.Force())
```

### Initialize Database

```fsharp
let initializeDatabase () =
    let conn = getConnection()
    conn.Execute("""
        CREATE TABLE IF NOT EXISTS Entities (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        )
    """) |> ignore
```

### CRUD Operations

```fsharp
let getAllEntities () : Async<Entity list> =
    async {
        let conn = getConnection()
        let! entities = conn.QueryAsync<Entity>("SELECT * FROM Entities") |> Async.AwaitTask
        return entities |> Seq.toList
    }

let getEntityById (id: int) : Async<Entity option> =
    async {
        let conn = getConnection()
        let! entity = conn.QuerySingleOrDefaultAsync<Entity>(
            "SELECT * FROM Entities WHERE Id = @Id", {| Id = id |}
        ) |> Async.AwaitTask
        return if isNull (box entity) then None else Some entity
    }

let insertEntity (entity: Entity) : Async<Entity> =
    async {
        let conn = getConnection()
        let! id = conn.ExecuteScalarAsync<int64>(
            """INSERT INTO Entities (Name, Description, CreatedAt, UpdatedAt)
               VALUES (@Name, @Description, @CreatedAt, @UpdatedAt)
               RETURNING Id""", entity
        ) |> Async.AwaitTask
        return { entity with Id = int id }
    }
```

## Best Practices

### Do
- Use parameterized queries
- Create indexes for frequently queried columns
- Use `async` for all I/O
- Use lazy loading for DB config (test isolation)
- Support `USE_MEMORY_DB` for tests

### Don't
- Use string concatenation for SQL
- Write tests that persist to production DB

## Verification Checklist

- [ ] Database initialized
- [ ] Parameterized queries used
- [ ] Async operations used
- [ ] Lazy loading for DB configuration
- [ ] `USE_MEMORY_DB` supported for tests
