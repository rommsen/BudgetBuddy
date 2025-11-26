module Persistence

open System
open System.IO
open System.Threading
open Newtonsoft.Json
open Shared.Domain

// Data directory is configurable via DATA_DIR environment variable
// Default: ~/heimeshoff/fsharp_counter/
let private dataDir =
    match Environment.GetEnvironmentVariable("DATA_DIR") with
    | null | "" -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "app", "data")
    | path -> path
let private counterFile = Path.Combine(dataDir, "counter.json")

/// Get the absolute path to the counter file
let getCounterFilePath () = counterFile

/// Semaphore to ensure only one file operation at a time (prevents race conditions)
let private fileLock = new SemaphoreSlim(1, 1)

/// Ensure the data directory exists
let ensureDataDir () =
    if not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore

/// Load counter from file, or initialize to 0 if file doesn't exist
let loadCounter () : Async<Counter> =
    async {
        do! fileLock.WaitAsync() |> Async.AwaitTask
        try
            ensureDataDir()

            if File.Exists counterFile then
                let! json = File.ReadAllTextAsync(counterFile) |> Async.AwaitTask
                let counter = JsonConvert.DeserializeObject<Counter>(json)
                return counter
            else
                return { Value = 0 }
        finally
            fileLock.Release() |> ignore
    }

/// Save counter to file
let saveCounter (counter: Counter) : Async<unit> =
    async {
        do! fileLock.WaitAsync() |> Async.AwaitTask
        try
            ensureDataDir()
            let json = JsonConvert.SerializeObject(counter)
            do! File.WriteAllTextAsync(counterFile, json) |> Async.AwaitTask
        finally
            fileLock.Release() |> ignore
    }
