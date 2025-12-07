module Program

open System
open Expecto

[<EntryPoint>]
let main args =
    // CRITICAL: Set test mode BEFORE any persistence module is loaded
    // This ensures tests use in-memory SQLite instead of production database
    Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

    runTestsInAssemblyWithCLIArgs [] args
