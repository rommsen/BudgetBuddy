module Domain

open System
open Shared.Domain

// CRITICAL: NO I/O IN THIS FILE!
// All functions must be pure - only transformations, no side effects

let createEntity (req: CreateEntityRequest) : Entity =
    {
        Id = 0  // Will be set by persistence
        Name = req.Name.Trim()
        Description = req.Description |> Option.map (fun d -> d.Trim())
        Status = Active
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

let updateEntity (existing: Entity) (req: UpdateEntityRequest) : Entity =
    {
        existing with
            Name = req.Name.Trim()
            Description = req.Description |> Option.map (fun d -> d.Trim())
            Status = req.Status
            UpdatedAt = DateTime.UtcNow
    }

let completeEntity (entity: Entity) : Entity =
    { entity with Status = Completed; UpdatedAt = DateTime.UtcNow }

let archiveEntity (entity: Entity) : Entity =
    { entity with Status = Archived; UpdatedAt = DateTime.UtcNow }
