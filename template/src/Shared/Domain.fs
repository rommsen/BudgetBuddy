module Shared.Domain

open System

// Define your domain types here
// Use records for data, discriminated unions for states

type EntityStatus =
    | Active
    | Completed
    | Archived

type Entity = {
    Id: int
    Name: string
    Description: string option
    Status: EntityStatus
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

// Request DTOs (what clients send)
type CreateEntityRequest = {
    Name: string
    Description: string option
}

type UpdateEntityRequest = {
    Id: int
    Name: string
    Description: string option
    Status: EntityStatus
}
