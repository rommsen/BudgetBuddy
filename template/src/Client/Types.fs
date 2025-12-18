module Types

// RemoteData pattern for async operations
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string

// Helper functions
module RemoteData =
    let map f rd =
        match rd with
        | NotAsked -> NotAsked
        | Loading -> Loading
        | Success x -> Success (f x)
        | Failure e -> Failure e

    let bind f rd =
        match rd with
        | NotAsked -> NotAsked
        | Loading -> Loading
        | Success x -> f x
        | Failure e -> Failure e

    let withDefault defaultValue rd =
        match rd with
        | Success x -> x
        | _ -> defaultValue

    let isLoading rd =
        match rd with
        | Loading -> true
        | _ -> false

    let isSuccess rd =
        match rd with
        | Success _ -> true
        | _ -> false

    let toOption rd =
        match rd with
        | Success x -> Some x
        | _ -> None

    let fromResult result =
        match result with
        | Ok x -> Success x
        | Error e -> Failure e
