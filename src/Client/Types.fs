module Types

open System

/// Represents the state of a remote data fetch
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string

/// Utility functions for working with RemoteData values
[<RequireQualifiedAccess>]
module RemoteData =

    /// Map a function over the success value
    let map (f: 'a -> 'b) (rd: RemoteData<'a>) : RemoteData<'b> =
        match rd with
        | Success x -> Success (f x)
        | Loading -> Loading
        | NotAsked -> NotAsked
        | Failure err -> Failure err

    /// Bind a function that returns RemoteData over the success value
    let bind (f: 'a -> RemoteData<'b>) (rd: RemoteData<'a>) : RemoteData<'b> =
        match rd with
        | Success x -> f x
        | Loading -> Loading
        | NotAsked -> NotAsked
        | Failure err -> Failure err

    /// Check if the value is Loading
    let isLoading (rd: RemoteData<'a>) : bool =
        match rd with
        | Loading -> true
        | _ -> false

    /// Check if the value is Success
    let isSuccess (rd: RemoteData<'a>) : bool =
        match rd with
        | Success _ -> true
        | _ -> false

    /// Check if the value is Failure
    let isFailure (rd: RemoteData<'a>) : bool =
        match rd with
        | Failure _ -> true
        | _ -> false

    /// Check if the value is NotAsked
    let isNotAsked (rd: RemoteData<'a>) : bool =
        match rd with
        | NotAsked -> true
        | _ -> false

    /// Convert to Option, returning None for non-Success states
    let toOption (rd: RemoteData<'a>) : 'a option =
        match rd with
        | Success x -> Some x
        | _ -> None

    /// Get the success value or a default
    let withDefault (defaultValue: 'a) (rd: RemoteData<'a>) : 'a =
        match rd with
        | Success x -> x
        | _ -> defaultValue

    /// Map a function over the error message
    let mapError (f: string -> string) (rd: RemoteData<'a>) : RemoteData<'a> =
        match rd with
        | Failure err -> Failure (f err)
        | other -> other

    /// Recover from an error with a default value
    let recover (defaultValue: 'a) (rd: RemoteData<'a>) : RemoteData<'a> =
        match rd with
        | Failure _ -> Success defaultValue
        | other -> other

    /// Recover from an error with a function that produces a value from the error
    let recoverWith (f: string -> 'a) (rd: RemoteData<'a>) : RemoteData<'a> =
        match rd with
        | Failure err -> Success (f err)
        | other -> other

    /// Combine two RemoteData values, succeeding only if both succeed
    let map2 (f: 'a -> 'b -> 'c) (rd1: RemoteData<'a>) (rd2: RemoteData<'b>) : RemoteData<'c> =
        match rd1, rd2 with
        | Success a, Success b -> Success (f a b)
        | Failure e, _ -> Failure e
        | _, Failure e -> Failure e
        | Loading, _ -> Loading
        | _, Loading -> Loading
        | NotAsked, _ -> NotAsked
        | _, NotAsked -> NotAsked

    /// Get the error message if in Failure state
    let toError (rd: RemoteData<'a>) : string option =
        match rd with
        | Failure err -> Some err
        | _ -> None

    /// Fold over the RemoteData value
    let fold (onNotAsked: 'b) (onLoading: 'b) (onSuccess: 'a -> 'b) (onFailure: string -> 'b) (rd: RemoteData<'a>) : 'b =
        match rd with
        | NotAsked -> onNotAsked
        | Loading -> onLoading
        | Success x -> onSuccess x
        | Failure err -> onFailure err

    /// Convert a Result to RemoteData
    let fromResult (result: Result<'a, string>) : RemoteData<'a> =
        match result with
        | Ok x -> Success x
        | Error err -> Failure err

    /// Convert an Option to RemoteData (None becomes NotAsked)
    let fromOption (opt: 'a option) : RemoteData<'a> =
        match opt with
        | Some x -> Success x
        | None -> NotAsked

    /// Convert an Option to RemoteData (None becomes Failure with given message)
    let fromOptionWithError (errorMsg: string) (opt: 'a option) : RemoteData<'a> =
        match opt with
        | Some x -> Success x
        | None -> Failure errorMsg

/// Application pages/routes
type Page =
    | Dashboard
    | SyncFlow
    | Rules
    | Settings


/// URL routing helpers for hash-based navigation
module Routing =
    open Feliz.Router

    /// Parse URL segments to Page
    let parseUrl (segments: string list) : Page =
        match segments with
        | [] -> Dashboard
        | ["sync"] -> SyncFlow
        | ["rules"] -> Rules
        | ["settings"] -> Settings
        | _ -> Dashboard  // Fallback to dashboard for unknown routes

    /// Convert Page to URL segments (for navigation)
    let toUrlSegments (page: Page) : string list =
        match page with
        | Dashboard -> []
        | SyncFlow -> ["sync"]
        | Rules -> ["rules"]
        | Settings -> ["settings"]

    /// Get current page from URL (for initialization)
    let currentPage () : Page =
        Router.currentUrl () |> parseUrl

/// Toast notification types
type ToastType =
    | ToastSuccess
    | ToastError
    | ToastInfo
    | ToastWarning

/// Toast notification
type Toast = {
    Id: Guid
    Message: string
    Type: ToastType
}
