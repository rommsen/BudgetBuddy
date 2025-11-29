module Persistence

open System
open System.IO

// Data directory is configurable via DATA_DIR environment variable
let private dataDir =
    match Environment.GetEnvironmentVariable("DATA_DIR") with
    | null | "" -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "heimeshoff", "budgetbuddy")
    | path -> path

/// Ensure the data directory exists
let ensureDataDir () =
    if not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore

// BudgetBuddy persistence functions will be defined here
// See docs/MILESTONE-PLAN.md for database schema and persistence implementation
