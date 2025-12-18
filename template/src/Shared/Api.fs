module Shared.Api

open Domain

// Define your API contracts here
// Use Result<'T, string> for operations that can fail

type IEntityApi = {
    getAll: unit -> Async<Entity list>
    getById: int -> Async<Result<Entity, string>>
    create: CreateEntityRequest -> Async<Result<Entity, string>>
    update: UpdateEntityRequest -> Async<Result<Entity, string>>
    delete: int -> Async<Result<unit, string>>
}

// Add more API interfaces for different domain areas
// type IUserApi = { ... }
// type ISettingsApi = { ... }
