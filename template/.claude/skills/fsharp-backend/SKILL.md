---
name: fsharp-backend
description: |
  Implement F# backend using Giraffe + Fable.Remoting with proper separation: Validation → Domain (pure logic) → Persistence (I/O) → API.
  Use when implementing server-side logic, API endpoints, or business rules.
  Creates code in src/Server/ files: Validation.fs, Domain.fs, Persistence.fs, Api.fs.
---

# F# Backend Implementation

## When to Use This Skill

Activate when:
- User requests "implement backend for X"
- Need to add API endpoints
- Implementing business logic

## Architecture Layers

```
API (Fable.Remoting)
    ↓
Validation (Input checking)
    ↓
Domain (Pure business logic - NO I/O)
    ↓
Persistence (Database/File I/O)
```

## Layer 1: Validation (`Validation.fs`)

```fsharp
let validateEntity (entity: Entity) : Result<Entity, string list> =
    let errors = [
        if String.IsNullOrWhiteSpace(entity.Name) then "Name required"
        if entity.Name.Length > 100 then "Name too long"
    ]
    if errors.IsEmpty then Ok entity else Error errors
```

## Layer 2: Domain (`Domain.fs`) - PURE, NO I/O

```fsharp
// GOOD - Pure transformation
let processEntity (entity: Entity) : Entity =
    { entity with Name = entity.Name.Trim(); UpdatedAt = DateTime.UtcNow }

// BAD - I/O in domain
let processEntity id =
    let entity = Persistence.getById id  // NO!
    { entity with Status = Processed }
```

## Layer 3: Persistence (`Persistence.fs`)

```fsharp
let getAllEntities () : Async<Entity list> =
    async {
        let conn = getConnection()
        let! entities = conn.QueryAsync<Entity>("SELECT * FROM Entities") |> Async.AwaitTask
        return entities |> Seq.toList
    }

let insertEntity (entity: Entity) : Async<Entity> =
    async {
        let conn = getConnection()
        let! id = conn.ExecuteScalarAsync<int64>(
            "INSERT INTO Entities (...) VALUES (...) RETURNING Id", entity
        ) |> Async.AwaitTask
        return { entity with Id = int id }
    }
```

## Layer 4: API (`Api.fs`)

```fsharp
let entityApi : IEntityApi = {
    create = fun request -> async {
        match Validation.validateEntity request with
        | Error errs -> return Error (String.concat "; " errs)
        | Ok valid ->
            let processed = Domain.processEntity valid
            let! saved = Persistence.insertEntity processed
            return Ok saved
    }
}
```

## Verification Checklist

- [ ] Validation in `src/Server/Validation.fs`
- [ ] Domain logic in `src/Server/Domain.fs` (PURE - no I/O)
- [ ] Persistence in `src/Server/Persistence.fs`
- [ ] API implementation in `src/Server/Api.fs`
- [ ] No I/O in domain layer
- [ ] Parameterized queries (SQL injection prevention)
