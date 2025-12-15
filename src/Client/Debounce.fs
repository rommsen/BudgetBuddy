namespace Client

open Elmish

/// Debounce utilities for Elmish applications.
/// Uses a version-based approach that works with the Elmish architecture.
[<RequireQualifiedAccess>]
module Debounce =
    /// Default debounce delay in milliseconds
    let [<Literal>] DefaultDelayMs = 400

    /// Creates a delayed command that fires a message after the specified delay.
    /// Use with version tracking to implement debouncing:
    /// 1. Increment version on each change
    /// 2. Use this command with current version
    /// 3. In the delayed message handler, check if version is still current
    let delayed<'Msg> (delayMs: int) (msg: 'Msg) : Cmd<'Msg> =
        Cmd.OfAsync.perform
            (fun () -> async {
                do! Async.Sleep delayMs
                return ()
            })
            ()
            (fun () -> msg)

    /// Creates a delayed command with default delay (400ms)
    let delayedDefault<'Msg> (msg: 'Msg) : Cmd<'Msg> =
        delayed DefaultDelayMs msg
