# Quick Reference

## Domain Types (`src/Shared/Domain.fs`)

```fsharp
module Shared.Domain

open System

// Records for data
type Entity = {
    Id: int
    Name: string
    Description: string option
    Status: EntityStatus
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

// Discriminated unions for states
and EntityStatus = Active | Completed | Archived

// Request DTOs
type CreateEntityRequest = {
    Name: string
    Description: string option
}
```

## API Contract (`src/Shared/Api.fs`)

```fsharp
module Shared.Api

open Domain

type IEntityApi = {
    getAll: unit -> Async<Entity list>
    getById: int -> Async<Result<Entity, string>>
    create: CreateEntityRequest -> Async<Result<Entity, string>>
    update: Entity -> Async<Result<Entity, string>>
    delete: int -> Async<Result<unit, string>>
}
```

## Validation (`src/Server/Validation.fs`)

```fsharp
module Validation

let validateRequired field value =
    if String.IsNullOrWhiteSpace(value) then Some $"{field} required" else None

let validateEntity (entity: Entity) : Result<Entity, string list> =
    let errors = [
        validateRequired "Name" entity.Name
        if entity.Name.Length > 100 then Some "Name too long" else None
    ] |> List.choose id
    if errors.IsEmpty then Ok entity else Error errors
```

## Domain Logic (`src/Server/Domain.fs`) - PURE!

```fsharp
module Domain

open System
open Shared.Domain

// NO I/O IN THIS FILE!

let createEntity (req: CreateEntityRequest) : Entity =
    {
        Id = 0
        Name = req.Name.Trim()
        Description = req.Description |> Option.map String.trim
        Status = Active
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

let completeEntity (entity: Entity) : Entity =
    { entity with Status = Completed; UpdatedAt = DateTime.UtcNow }
```

## Persistence (`src/Server/Persistence.fs`)

```fsharp
module Persistence

open Dapper
open Microsoft.Data.Sqlite

let private connectionString = "Data Source=./data/app.db"
let private getConnection () = new SqliteConnection(connectionString)

let getAllEntities () : Async<Entity list> =
    async {
        use conn = getConnection()
        let! entities = conn.QueryAsync<Entity>("SELECT * FROM Entities") |> Async.AwaitTask
        return entities |> Seq.toList
    }

let insertEntity (entity: Entity) : Async<Entity> =
    async {
        use conn = getConnection()
        let! id = conn.ExecuteScalarAsync<int64>(
            """INSERT INTO Entities (Name, Description, Status, CreatedAt, UpdatedAt)
               VALUES (@Name, @Description, @Status, @CreatedAt, @UpdatedAt)
               RETURNING Id""", entity
        ) |> Async.AwaitTask
        return { entity with Id = int id }
    }
```

## API Implementation (`src/Server/Api.fs`)

```fsharp
module Api

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api

let entityApi : IEntityApi = {
    getAll = Persistence.getAllEntities

    getById = fun id -> async {
        match! Persistence.getEntityById id with
        | Some e -> return Ok e
        | None -> return Error "Not found"
    }

    create = fun request -> async {
        match Validation.validateRequest request with
        | Error errs -> return Error (String.concat "; " errs)
        | Ok valid ->
            let entity = Domain.createEntity valid
            let! saved = Persistence.insertEntity entity
            return Ok saved
    }
}

let webApp =
    Remoting.createApi()
    |> Remoting.fromValue entityApi
    |> Remoting.buildHttpHandler
```

## Frontend State (`src/Client/State.fs`)

```fsharp
module State

open Elmish
open Shared.Domain
open Types

type Model = {
    Entities: RemoteData<Entity list>
    NewEntityName: string
}

type Msg =
    | LoadEntities
    | EntitiesLoaded of Result<Entity list, string>
    | UpdateName of string
    | CreateEntity
    | EntityCreated of Result<Entity, string>

let init () =
    { Entities = NotAsked; NewEntityName = "" }, Cmd.ofMsg LoadEntities

let update msg model =
    match msg with
    | LoadEntities ->
        { model with Entities = Loading },
        Cmd.OfAsync.either Api.api.getAll () (Ok >> EntitiesLoaded) (fun ex -> Error ex.Message |> EntitiesLoaded)

    | EntitiesLoaded (Ok entities) ->
        { model with Entities = Success entities }, Cmd.none

    | EntitiesLoaded (Error err) ->
        { model with Entities = Failure err }, Cmd.none

    | UpdateName name ->
        { model with NewEntityName = name }, Cmd.none

    | CreateEntity ->
        model, Cmd.OfAsync.either Api.api.create { Name = model.NewEntityName; Description = None } EntityCreated (fun ex -> Error ex.Message |> EntityCreated)

    | EntityCreated (Ok _) ->
        { model with NewEntityName = "" }, Cmd.ofMsg LoadEntities

    | EntityCreated (Error _) ->
        model, Cmd.none
```

## Frontend View (`src/Client/View.fs`)

```fsharp
module View

open Feliz
open State
open Types

let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "container mx-auto p-4"
        prop.children [
            Html.h1 [ prop.className "text-4xl font-bold mb-8"; prop.text "My App" ]

            // Form
            Html.input [
                prop.className "input input-bordered w-full mb-2"
                prop.value model.NewEntityName
                prop.onChange (UpdateName >> dispatch)
            ]
            Html.button [
                prop.className "btn btn-primary"
                prop.text "Create"
                prop.onClick (fun _ -> dispatch CreateEntity)
            ]

            // List
            match model.Entities with
            | NotAsked -> Html.div "Click to load"
            | Loading -> Html.span [ prop.className "loading loading-spinner" ]
            | Success entities ->
                Html.div [
                    for e in entities ->
                        Html.div [ prop.key (string e.Id); prop.text e.Name ]
                ]
            | Failure err -> Html.div [ prop.className "alert alert-error"; prop.text err ]
        ]
    ]
```

## Tests (`src/Tests/`)

```fsharp
module DomainTests

open Expecto
open Shared.Domain

[<Tests>]
let tests =
    testList "Domain" [
        testCase "createEntity trims name" <| fun () ->
            let req = { Name = "  Test  "; Description = None }
            let result = Domain.createEntity req
            Expect.equal result.Name "Test" "Should trim"

        testCase "validation rejects empty name" <| fun () ->
            let req = { Name = ""; Description = None }
            let result = Validation.validateRequest req
            Expect.isError result "Should fail"
    ]
```

## Common Patterns

### RemoteData
```fsharp
type RemoteData<'T> = NotAsked | Loading | Success of 'T | Failure of string
```

### Result Handling
```fsharp
match Validation.validate entity with
| Error errs -> return Error (String.concat "; " errs)
| Ok valid -> // proceed
```

### Async Operations
```fsharp
let! result = Persistence.getById id
match result with
| Some entity -> return Ok entity
| None -> return Error "Not found"
```
