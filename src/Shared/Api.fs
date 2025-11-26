module Shared.Api

open Domain

/// API contract for counter operations
type ICounterApi = {
    /// Get the current counter value
    getCounter: unit -> Async<Counter>

    /// Increment the counter and return the new value
    incrementCounter: unit -> Async<Counter>

    /// Get the absolute path to the counter data file
    getDataPath: unit -> Async<string>
}
