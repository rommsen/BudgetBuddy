module Validation

open System
open Shared.Domain

// Reusable validators - return Option<string> (None = valid, Some = error)

let validateRequired (fieldName: string) (value: string) : string option =
    if String.IsNullOrWhiteSpace(value) then
        Some $"{fieldName} is required"
    else
        None

let validateLength (fieldName: string) (minLen: int) (maxLen: int) (value: string) : string option =
    let len = value.Length
    if len < minLen then
        Some $"{fieldName} must be at least {minLen} characters"
    elif len > maxLen then
        Some $"{fieldName} must be at most {maxLen} characters"
    else
        None

// Entity validation - accumulate all errors

let validateCreateRequest (req: CreateEntityRequest) : Result<CreateEntityRequest, string list> =
    let errors = [
        validateRequired "Name" req.Name
        validateLength "Name" 1 100 req.Name
        match req.Description with
        | Some desc -> validateLength "Description" 0 500 desc
        | None -> None
    ] |> List.choose id

    if errors.IsEmpty then Ok req else Error errors

let validateUpdateRequest (req: UpdateEntityRequest) : Result<UpdateEntityRequest, string list> =
    let errors = [
        validateRequired "Name" req.Name
        validateLength "Name" 1 100 req.Name
        match req.Description with
        | Some desc -> validateLength "Description" 0 500 desc
        | None -> None
    ] |> List.choose id

    if errors.IsEmpty then Ok req else Error errors

// Helper to convert to single error string
let errorsToString (errors: string list) : string =
    String.concat "; " errors
