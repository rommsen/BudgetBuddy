# Quick Reference

> Copy-paste ready code templates for common patterns.

## Project Structure

```
src/
├── Shared/          # Domain types & API contracts
├── Client/          # Fable + Elmish frontend
├── Server/          # Giraffe backend
└── Tests/           # Expecto tests
```

## Shared Domain Type

```fsharp
// src/Shared/Domain.fs
module Shared.Domain

type Item = {
    Id: int
    Name: string
    CreatedAt: DateTime
}

type ItemUpdate = {
    Name: string option
}
```

## Shared API Contract

```fsharp
// src/Shared/Api.fs
module Shared.Api

open Domain

type IItemApi = {
    getItems: unit -> Async<Item list>
    getItem: int -> Async<Result<Item, string>>
    saveItem: Item -> Async<Result<Item, string>>
    deleteItem: int -> Async<Result<unit, string>>
}
```

## Backend API Implementation

```fsharp
// src/Server/Api.fs
let itemApi : IItemApi = {
    getItems = fun () -> Persistence.getAllItems()

    getItem = fun id -> async {
        match! Persistence.getById id with
        | Some item -> return Ok item
        | None -> return Error "Not found"
    }

    saveItem = fun item -> async {
        match Validation.validate item with
        | Error errs -> return Error (String.concat ", " errs)
        | Ok valid ->
            let processed = Domain.process valid
            do! Persistence.save processed
            return Ok processed
    }

    deleteItem = fun id -> async {
        do! Persistence.delete id
        return Ok ()
    }
}

let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun t m -> $"/api/{t}/{m}")
    |> Remoting.fromValue itemApi
    |> Remoting.buildHttpHandler
```

## Frontend State (Elmish)

```fsharp
// src/Client/State.fs
type Model = {
    Items: RemoteData<Item list>
    SelectedItem: RemoteData<Item>
}

type Msg =
    | LoadItems
    | ItemsLoaded of Result<Item list, string>

let init () =
    { Items = NotAsked; SelectedItem = NotAsked },
    Cmd.ofMsg LoadItems

let update msg model =
    match msg with
    | LoadItems ->
        { model with Items = Loading },
        Cmd.OfAsync.either Api.api.getItems () (Ok >> ItemsLoaded) (fun ex -> Error ex.Message |> ItemsLoaded)

    | ItemsLoaded (Ok items) ->
        { model with Items = Success items }, Cmd.none

    | ItemsLoaded (Error err) ->
        { model with Items = Failure err }, Cmd.none
```

## Frontend View (Feliz)

```fsharp
// src/Client/View.fs
let view model dispatch =
    Html.div [
        prop.className "container mx-auto p-4"
        prop.children [
            match model.Items with
            | NotAsked -> Html.button [ prop.onClick (fun _ -> dispatch LoadItems); prop.text "Load" ]
            | Loading -> Html.div [ prop.className "loading loading-spinner" ]
            | Success items ->
                Html.div [
                    for item in items do
                        Html.div [
                            prop.key (string item.Id)
                            prop.text item.Name
                        ]
                ]
            | Failure err -> Html.div [ prop.className "alert alert-error"; prop.text err ]
        ]
    ]
```

## SQLite Persistence

```fsharp
// src/Server/Persistence.fs
let private connectionString = "Data Source=./data/app.db"
let private getConnection () = new SqliteConnection(connectionString)

let getAllItems () : Async<Item list> =
    async {
        use conn = getConnection()
        let! items = conn.QueryAsync<Item>("SELECT * FROM items") |> Async.AwaitTask
        return items |> Seq.toList
    }

let saveItem (item: Item) : Async<unit> =
    async {
        use conn = getConnection()
        if item.Id = 0 then
            do! conn.ExecuteAsync("INSERT INTO items (name, created_at) VALUES (@Name, @CreatedAt)", item)
                |> Async.AwaitTask |> Async.Ignore
        else
            do! conn.ExecuteAsync("UPDATE items SET name = @Name WHERE id = @Id", item)
                |> Async.AwaitTask |> Async.Ignore
    }
```

## Validation

```fsharp
// src/Server/Validation.fs
let validateRequired field value =
    if String.IsNullOrWhiteSpace value then Some $"{field} is required" else None

let validateLength field min max (value: string) =
    if value.Length < min || value.Length > max then
        Some $"{field} must be {min}-{max} characters"
    else None

let validateItem item : Result<Item, string list> =
    let errors = [
        validateRequired "Name" item.Name
        validateLength "Name" 3 100 item.Name
    ] |> List.choose id

    if errors.IsEmpty then Ok item else Error errors
```

## Expecto Test

```fsharp
// src/Tests/DomainTests.fs
[<Tests>]
let tests =
    testList "Domain" [
        testCase "process trims name" <| fun () ->
            let item = { Id = 1; Name = "  Test  "; CreatedAt = DateTime.UtcNow }
            let result = Domain.process item
            Expect.equal result.Name "Test" "Should trim"
    ]
```

## File Locations

| What | Where |
|------|-------|
| Domain types | `src/Shared/Domain.fs` |
| API contracts | `src/Shared/Api.fs` |
| Client state | `src/Client/State.fs` |
| Client views | `src/Client/View.fs` |
| Server API | `src/Server/Api.fs` |
| Persistence | `src/Server/Persistence.fs` |
| Domain logic | `src/Server/Domain.fs` |
| Validation | `src/Server/Validation.fs` |
| Tests | `src/Tests/` |

## See Also

- `development-workflow.md` - Implementation order
- `anti-patterns.md` - What to avoid
