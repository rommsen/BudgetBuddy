# SQLite Persistence

> SQLite + Dapper patterns for structured data.

## Overview

Use SQLite for structured data requiring queries, indexes, and relationships. Dapper provides a lightweight query layer over ADO.NET.

## When to Use This

- Structured data with relationships
- Data requiring queries/filters
- Indexed lookups
- Transactional operations

## Patterns

### Database Configuration with Test Isolation

```fsharp
// src/Server/Persistence.fs
module Persistence

open Microsoft.Data.Sqlite
open System

type private DbConfig = {
    IsTestMode: bool
    ConnectionString: string
    SharedConnection: SqliteConnection option
}

// CRITICAL: Use lazy loading!
let private dbConfig = lazy (
    let isTestMode =
        match Environment.GetEnvironmentVariable("USE_MEMORY_DB") with
        | "true" | "1" -> true
        | _ -> false

    let connectionString =
        if isTestMode then
            "Data Source=:memory:;Mode=Memory;Cache=Shared"
        else
            "Data Source=./data/app.db"

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
    | Some conn -> conn  // Shared for tests - don't dispose
    | None -> new SqliteConnection(config.ConnectionString)
```

### Database Initialization

```fsharp
let initializeDatabase () =
    use conn = getConnection()
    conn.Open()

    // Enable WAL mode
    use walCmd = new SqliteCommand("PRAGMA journal_mode=WAL;", conn)
    walCmd.ExecuteNonQuery() |> ignore

    // Create tables
    use cmd = new SqliteCommand("""
        CREATE TABLE IF NOT EXISTS items (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            description TEXT NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_items_name ON items(name);
    """, conn)

    cmd.ExecuteNonQuery() |> ignore
```

### Dapper CRUD Operations

```fsharp
open Dapper

// Query All
let getAllItems () : Async<Item list> =
    async {
        use conn = getConnection()
        let! items =
            conn.QueryAsync<Item>("SELECT * FROM items ORDER BY created_at DESC")
            |> Async.AwaitTask
        return items |> Seq.toList
    }

// Query by ID
let getItemById (id: int) : Async<Item option> =
    async {
        use conn = getConnection()
        let! item =
            conn.QueryFirstOrDefaultAsync<Item>(
                "SELECT * FROM items WHERE id = @Id",
                {| Id = id |}
            ) |> Async.AwaitTask

        return if isNull (box item) then None else Some item
    }

// Insert
let insertItem (item: Item) : Async<Item> =
    async {
        use conn = getConnection()
        let now = DateTime.UtcNow

        let! id =
            conn.ExecuteScalarAsync<int64>(
                """
                INSERT INTO items (name, description, created_at, updated_at)
                VALUES (@Name, @Description, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();
                """,
                {|
                    Name = item.Name
                    Description = item.Description
                    CreatedAt = now.ToString("O")
                    UpdatedAt = now.ToString("O")
                |}
            ) |> Async.AwaitTask

        return { item with Id = int id; CreatedAt = now; UpdatedAt = now }
    }

// Update
let updateItem (item: Item) : Async<unit> =
    async {
        use conn = getConnection()
        do! conn.ExecuteAsync(
            """
            UPDATE items
            SET name = @Name, description = @Description, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            {|
                Id = item.Id
                Name = item.Name
                Description = item.Description
                UpdatedAt = DateTime.UtcNow.ToString("O")
            |}
        ) |> Async.AwaitTask |> Async.Ignore
    }

// Delete
let deleteItem (id: int) : Async<unit> =
    async {
        use conn = getConnection()
        do! conn.ExecuteAsync("DELETE FROM items WHERE id = @Id", {| Id = id |})
            |> Async.AwaitTask |> Async.Ignore
    }
```

### F# Options with Dapper

```fsharp
open Dapper

type OptionHandler<'T>() =
    inherit SqlMapper.TypeHandler<'T option>()

    override _.SetValue(param, value) =
        param.Value <-
            match value with
            | Some v -> box v
            | None -> box DBNull.Value

    override _.Parse(value) =
        if isNull value || value = box DBNull.Value then None
        else Some (unbox<'T> value)

// Register at startup
SqlMapper.AddTypeHandler(OptionHandler<string>())
SqlMapper.AddTypeHandler(OptionHandler<int>())

// Row types need [<CLIMutable>]
[<CLIMutable>]
type ItemRow = {
    Id: int
    Name: string
    Description: string option  // nullable in DB
}
```

### Parameterized Queries

```fsharp
// Search with parameters
let searchItems (term: string) : Async<Item list> =
    async {
        use conn = getConnection()
        let! items =
            conn.QueryAsync<Item>(
                """
                SELECT * FROM items
                WHERE name LIKE @Term OR description LIKE @Term
                """,
                {| Term = $"%%{term}%%" |}
            ) |> Async.AwaitTask
        return items |> Seq.toList
    }
```

## Anti-Patterns

### ❌ String Concatenation in SQL

```fsharp
// BAD - SQL injection risk!
let search term =
    conn.QueryAsync<Item>($"SELECT * FROM items WHERE name = '{term}'")

// GOOD - Parameterized
let search term =
    conn.QueryAsync<Item>("SELECT * FROM items WHERE name = @Term", {| Term = term |})
```

### ❌ Disposing In-Memory Connection

```fsharp
// BAD for In-Memory SQLite
let getItems () =
    use conn = getConnection()  // Disposed! DB gone!
    conn.QueryAsync<Item>(...)

// GOOD
let getItems () =
    let conn = getConnection()  // No 'use' for shared connection
    conn.QueryAsync<Item>(...)
```

## Checklist

- [ ] Lazy loading for test isolation
- [ ] WAL mode enabled
- [ ] Parameterized queries (no string concatenation)
- [ ] OptionHandler registered for nullable fields
- [ ] [<CLIMutable>] on row types
- [ ] Indexes on frequently queried columns
- [ ] Connection properly managed

## See Also

- `persistence-files.md` - File-based storage
- `../testing/persistence-tests.md` - Testing with in-memory DB
- `domain-logic.md` - Pure business logic
