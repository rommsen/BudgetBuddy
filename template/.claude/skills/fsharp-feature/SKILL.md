---
name: fsharp-feature
description: |
  Orchestrates end-to-end F# full-stack feature development following Elmish MVU + Giraffe + Fable.Remoting patterns.
  Use when user requests "add X feature", "implement Y", or needs guidance through complete stack implementation.
  Guides through: Shared types → Backend (validation/domain/persistence/API) → Frontend (state/view) → Tests.
---

# F# Full-Stack Feature Development

## When to Use This Skill

Activate when:
- User requests complete new feature ("add todo feature", "implement user management")
- Need structured guidance through entire stack
- Building feature from scratch with types, backend, frontend, and tests

## Prerequisites

Project structure:
```
src/
├── Shared/        # Domain types and API contracts
├── Server/        # Giraffe backend
├── Client/        # Elmish.React + Feliz frontend
└── Tests/         # Expecto tests
```

## Development Process

### 1. Read Documentation First

Before implementing any feature:
```bash
Read: /docs/09-QUICK-REFERENCE.md
Read: CLAUDE.md
```

### 2. Define Shared Contracts (`src/Shared/`)

**Domain.fs** - Define domain types:
```fsharp
module Shared.Domain

type Entity = {
    Id: int
    Name: string
    Description: string option
    Status: EntityStatus
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

and EntityStatus = Active | Completed | Archived
```

**Api.fs** - Define API contract:
```fsharp
module Shared.Api

type IEntityApi = {
    getAll: unit -> Async<Entity list>
    getById: int -> Async<Result<Entity, string>>
    create: CreateRequest -> Async<Result<Entity, string>>
    update: Entity -> Async<Result<Entity, string>>
    delete: int -> Async<Result<unit, string>>
}
```

### 3. Implement Backend (`src/Server/`)

**Validation.fs** - Input validation:
```fsharp
let validateCreateRequest (req: CreateRequest) =
    let errors = [
        if String.IsNullOrWhiteSpace(req.Name) then "Name required"
        if req.Name.Length > 100 then "Name too long"
    ]
    if errors.IsEmpty then Ok req else Error errors
```

**Domain.fs** - Pure business logic (NO I/O):
```fsharp
let processNewEntity (req: CreateRequest) : Entity =
    {
        Id = 0
        Name = req.Name.Trim()
        Description = req.Description |> Option.map String.trim
        Status = Active
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }
```

**Persistence.fs** - Database operations:
```fsharp
let getAllEntities () : Async<Entity list> =
    async {
        let conn = getConnection()
        let! entities = conn.QueryAsync<Entity>("SELECT * FROM Entities") |> Async.AwaitTask
        return entities |> Seq.toList
    }
```

**Api.fs** - Implement API contract:
```fsharp
let entityApi : IEntityApi = {
    create = fun request -> async {
        match Validation.validateCreateRequest request with
        | Error errs -> return Error (String.concat "; " errs)
        | Ok valid ->
            let entity = Domain.processNewEntity valid
            let! saved = Persistence.insertEntity entity
            return Ok saved
    }
}
```

### 4. Implement Frontend (`src/Client/`)

**State.fs** - Model, Msg, update:
```fsharp
type Model = {
    Entities: RemoteData<Entity list>
    NewEntityName: string
}

type Msg =
    | LoadEntities
    | EntitiesLoaded of Result<Entity list, string>
    | UpdateNewEntityName of string
    | CreateEntity
    | EntityCreated of Result<Entity, string>

let update msg model =
    match msg with
    | LoadEntities ->
        { model with Entities = Loading },
        Cmd.OfAsync.either Api.api.getAll () (Ok >> EntitiesLoaded) (fun ex -> Error ex.Message |> EntitiesLoaded)
    | EntitiesLoaded (Ok entities) ->
        { model with Entities = Success entities }, Cmd.none
    // ... handle other messages
```

**View.fs** - UI components:
```fsharp
let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "container mx-auto p-4"
        prop.children [
            match model.Entities with
            | NotAsked -> Html.button [ prop.text "Load"; prop.onClick (fun _ -> dispatch LoadEntities) ]
            | Loading -> Html.span [ prop.className "loading loading-spinner" ]
            | Success entities -> entityList entities dispatch
            | Failure err -> Html.div [ prop.className "alert alert-error"; prop.text err ]
        ]
    ]
```

### 5. Write Tests (`src/Tests/`)

```fsharp
[<Tests>]
let tests =
    testList "Entity Feature" [
        testCase "processNewEntity trims name" <| fun () ->
            let request = { Name = "  Test  "; Description = None }
            let result = Domain.processNewEntity request
            Expect.equal result.Name "Test" "Should trim"

        testCase "validation rejects empty name" <| fun () ->
            let request = { Name = ""; Description = None }
            let result = Validation.validateCreateRequest request
            Expect.isError result "Should fail"
    ]
```

## Verification Checklist

- [ ] Types defined in `src/Shared/Domain.fs`
- [ ] API contract in `src/Shared/Api.fs`
- [ ] Validation in `src/Server/Validation.fs`
- [ ] Domain logic pure (no I/O) in `src/Server/Domain.fs`
- [ ] Persistence in `src/Server/Persistence.fs`
- [ ] API implementation in `src/Server/Api.fs`
- [ ] Frontend state in `src/Client/State.fs`
- [ ] Frontend view in `src/Client/View.fs`
- [ ] Tests written (minimum: domain + validation)
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes

## Related Skills

- **fsharp-shared** - Detailed type patterns
- **fsharp-backend** - Backend layer details
- **fsharp-frontend** - Frontend patterns
- **fsharp-validation** - Complex validation
- **fsharp-persistence** - Database patterns
- **fsharp-tests** - Testing patterns
