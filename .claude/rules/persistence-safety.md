---
paths: src/Server/Persistence.fs
---

# Persistence Safety — Dapper/SQLite Pflicht-Patterns

## Verboten

- Dapper Row Types OHNE `[<CLIMutable>]` — Laufzeit-Crash
- Direkte `SqliteConnection` ohne Connection-Helper
- Queries ohne Parameterisierung (SQL Injection)
- Tests die auf die Produktions-Datenbank schreiben — IMMER `Data Source=:memory:` oder temp DB

## Richtig

```fsharp
// Jeder Dapper Row Type
[<CLIMutable>]
type EntityRow = { Id: string; Name: string; Amount: int64; ... }

// Connection Management
let withConnectionAsync (f: SqliteConnection -> Async<'T>) : Async<'T> = ...

// Parameterisierte Queries
let! rows = conn.QueryAsync<Row>("SELECT * FROM rules WHERE pattern = @Pattern", {| Pattern = pattern |})
```

## SQLite-spezifisch

- `INTEGER` statt `BIGINT` für int64
- Kein `RETURNING` Clause — verwende `last_insert_rowid()`
- WAL-Mode für Concurrent Reads: `PRAGMA journal_mode=WAL`
- F# Option Types brauchen `OptionHandler<'T>` für Dapper

## Grep-Checks

```bash
# Finde Row Types ohne CLIMutable
grep -B1 "type.*Row" src/Server/Persistence.fs | grep -v "CLIMutable"

# Finde String-Interpolation in SQL
grep -n '\$".*SELECT\|\$".*INSERT\|\$".*UPDATE' src/Server/Persistence.fs

# Finde Tests mit Produktions-DB
grep -rn "DATA_DIR\|budgetbuddy.db" src/Tests/ | grep -v ":memory:"
```
