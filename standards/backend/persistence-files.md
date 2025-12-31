# File-Based Persistence

> JSON files and event sourcing patterns.

## Overview

Use file-based persistence for simple configuration, caching, and event logs. JSON files are appropriate for small datasets that don't require complex queries.

## When to Use This

- Simple configuration
- Small datasets (< 1000 records)
- Event logs (append-only)
- Caching
- Backups

## Patterns

### JSON File Persistence

```fsharp
// src/Server/Persistence.fs
module Persistence

open System.IO
open System.Text.Json

let private dataDir = "./data"
let private filePath fileName = Path.Combine(dataDir, fileName)

// Save to JSON
let saveToJsonFile<'T> (fileName: string) (data: 'T) : Async<unit> =
    async {
        let json = JsonSerializer.Serialize(data, JsonSerializerOptions(WriteIndented = true))
        do! File.WriteAllTextAsync(filePath fileName, json) |> Async.AwaitTask
    }

// Load from JSON
let loadFromJsonFile<'T> (fileName: string) : Async<'T option> =
    async {
        let path = filePath fileName
        if File.Exists(path) then
            let! json = File.ReadAllTextAsync(path) |> Async.AwaitTask
            let data = JsonSerializer.Deserialize<'T>(json)
            return Some data
        else
            return None
    }

// Example usage
let saveSettings (settings: AppSettings) : Async<unit> =
    saveToJsonFile "settings.json" settings

let loadSettings () : Async<AppSettings option> =
    loadFromJsonFile<AppSettings> "settings.json"
```

### Event Sourcing (Append-Only Files)

```fsharp
open System

type Event<'T> = {
    Id: Guid
    Timestamp: DateTime
    Data: 'T
}

// Append event to log
let appendEvent<'T> (fileName: string) (event: 'T) : Async<unit> =
    async {
        let eventRecord = {
            Id = Guid.NewGuid()
            Timestamp = DateTime.UtcNow
            Data = event
        }

        let json = JsonSerializer.Serialize(eventRecord)
        let line = json + "\n"

        do! File.AppendAllTextAsync(filePath fileName, line) |> Async.AwaitTask
    }

// Read all events
let readEvents<'T> (fileName: string) : Async<Event<'T> list> =
    async {
        let path = filePath fileName
        if File.Exists(path) then
            let! lines = File.ReadAllLinesAsync(path) |> Async.AwaitTask
            return
                lines
                |> Array.filter (fun line -> not (String.IsNullOrWhiteSpace line))
                |> Array.map (fun line -> JsonSerializer.Deserialize<Event<'T>>(line))
                |> Array.toList
        else
            return []
    }

// Replay events to rebuild state
let replayEvents<'TEvent, 'TState>
    (fileName: string)
    (initialState: 'TState)
    (apply: 'TState -> 'TEvent -> 'TState) : Async<'TState> =
    async {
        let! events = readEvents<'TEvent> fileName
        return
            events
            |> List.map (fun e -> e.Data)
            |> List.fold apply initialState
    }
```

### Text/CSV Files

```fsharp
// Export to CSV
let exportToCsv (items: Item list) (fileName: string) : Async<unit> =
    async {
        let header = "Id,Name,Description,CreatedAt"
        let rows =
            items
            |> List.map (fun item ->
                $"{item.Id},{item.Name},{item.Description},{item.CreatedAt:O}"
            )

        let content = header :: rows |> String.concat "\n"
        do! File.WriteAllTextAsync(filePath fileName, content) |> Async.AwaitTask
    }
```

### Backup and Restore

```fsharp
// Backup data directory
let createBackup () : Async<string> =
    async {
        let timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")
        let backupDir = $"./backups/backup-{timestamp}"

        Directory.CreateDirectory(backupDir) |> ignore

        let files = Directory.GetFiles(dataDir)
        for file in files do
            let fileName = Path.GetFileName(file)
            let destPath = Path.Combine(backupDir, fileName)
            File.Copy(file, destPath)

        return backupDir
    }

// Restore from backup
let restoreBackup (backupDir: string) : Async<unit> =
    async {
        if not (Directory.Exists backupDir) then
            failwith $"Backup directory not found: {backupDir}"

        let files = Directory.GetFiles(backupDir)
        for file in files do
            let fileName = Path.GetFileName(file)
            let destPath = Path.Combine(dataDir, fileName)
            File.Copy(file, destPath, overwrite = true)
    }
```

## Anti-Patterns

### ❌ Large Datasets in JSON

```fsharp
// BAD - 100,000 records in JSON file
let saveAllTransactions txs = saveToJsonFile "transactions.json" txs

// GOOD - Use SQLite for large datasets
let saveTransactions = Persistence.insertTransactions
```

### ❌ No Error Handling

```fsharp
// BAD
let loadSettings () =
    let json = File.ReadAllText("settings.json")
    JsonSerializer.Deserialize<Settings>(json)

// GOOD
let loadSettings () = async {
    try
        if File.Exists("settings.json") then
            let! json = File.ReadAllTextAsync("settings.json") |> Async.AwaitTask
            return Some (JsonSerializer.Deserialize<Settings>(json))
        else
            return None
    with ex ->
        printfn $"Failed to load settings: {ex.Message}"
        return None
}
```

## Checklist

- [ ] JSON for small, simple data
- [ ] Event sourcing for audit trails
- [ ] Backup strategy implemented
- [ ] Error handling for file operations
- [ ] Directory existence checked
- [ ] Not using files for large datasets

## See Also

- `persistence-sqlite.md` - Structured data
- `domain-logic.md` - Event replay patterns
- `../global/learnings.md` - Best practices
