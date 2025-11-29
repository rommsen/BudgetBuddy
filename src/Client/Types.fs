module Types

open System

/// Represents the state of a remote data fetch
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string

/// Application pages/routes
type Page =
    | Dashboard
    | SyncFlow
    | Rules
    | Settings

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
