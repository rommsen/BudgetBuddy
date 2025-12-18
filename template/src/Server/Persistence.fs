module Persistence

open System
open System.IO
open Microsoft.Data.Sqlite
open Dapper
open Shared.Domain

// Configuration with lazy loading for test isolation
let private dataDir = "./data"

let private dbConfig = lazy (
    let isTestMode =
        match Environment.GetEnvironmentVariable("USE_MEMORY_DB") with
        | "true" | "1" -> true
        | _ -> false

    if isTestMode then
        "Data Source=:memory:;Mode=Memory;Cache=Shared"
    else
        let dbPath = Path.Combine(dataDir, "app.db")
        $"Data Source={dbPath}"
)

let private getConnection () =
    new SqliteConnection(dbConfig.Force())

let ensureDataDir () =
    if not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore

let initializeDatabase () =
    ensureDataDir()
    use conn = getConnection()
    conn.Open()

    conn.Execute("""
        CREATE TABLE IF NOT EXISTS Entities (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT,
            Status INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        )
    """) |> ignore

    conn.Execute("""
        CREATE INDEX IF NOT EXISTS idx_entities_status ON Entities(Status)
    """) |> ignore

// CRUD Operations

let getAllEntities () : Async<Entity list> =
    async {
        use conn = getConnection()
        conn.Open()
        let! entities = conn.QueryAsync<Entity>(
            "SELECT * FROM Entities ORDER BY CreatedAt DESC"
        ) |> Async.AwaitTask
        return entities |> Seq.toList
    }

let getEntityById (id: int) : Async<Entity option> =
    async {
        use conn = getConnection()
        conn.Open()
        let! entity = conn.QuerySingleOrDefaultAsync<Entity>(
            "SELECT * FROM Entities WHERE Id = @Id",
            {| Id = id |}
        ) |> Async.AwaitTask
        return if isNull (box entity) then None else Some entity
    }

let insertEntity (entity: Entity) : Async<Entity> =
    async {
        use conn = getConnection()
        conn.Open()
        let! id = conn.ExecuteScalarAsync<int64>(
            """INSERT INTO Entities (Name, Description, Status, CreatedAt, UpdatedAt)
               VALUES (@Name, @Description, @Status, @CreatedAt, @UpdatedAt)
               RETURNING Id""",
            {|
                Name = entity.Name
                Description = entity.Description |> Option.toObj
                Status = int entity.Status
                CreatedAt = entity.CreatedAt.ToString("o")
                UpdatedAt = entity.UpdatedAt.ToString("o")
            |}
        ) |> Async.AwaitTask
        return { entity with Id = int id }
    }

let updateEntity (entity: Entity) : Async<unit> =
    async {
        use conn = getConnection()
        conn.Open()
        let! _ = conn.ExecuteAsync(
            """UPDATE Entities
               SET Name = @Name, Description = @Description,
                   Status = @Status, UpdatedAt = @UpdatedAt
               WHERE Id = @Id""",
            {|
                Id = entity.Id
                Name = entity.Name
                Description = entity.Description |> Option.toObj
                Status = int entity.Status
                UpdatedAt = entity.UpdatedAt.ToString("o")
            |}
        ) |> Async.AwaitTask
        return ()
    }

let deleteEntity (id: int) : Async<unit> =
    async {
        use conn = getConnection()
        conn.Open()
        let! _ = conn.ExecuteAsync(
            "DELETE FROM Entities WHERE Id = @Id",
            {| Id = id |}
        ) |> Async.AwaitTask
        return ()
    }
