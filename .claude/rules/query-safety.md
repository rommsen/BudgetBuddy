---
paths:
  - "src/Server/Persistence.fs"
---

# Query Safety — N+1, Transactions, Parameter

## Verboten

- **N+1 Queries:** Loop mit Query pro Item. Immer Batch mit `IN (@Ids)` + client-side `groupBy`
- **Multi-Table Writes ohne Transaction:** `withTransactionAsync` wenn 2+ Tabellen geschrieben werden
- **String-Interpolation in SQL:** Immer `@Parameter` nutzen, nie `$"WHERE id = '{id}'"`
- **Silent Defaults in Converters:** `| _ -> DefaultValue` — immer `| s -> failwith $"Unknown: {s}"`

## Richtig

```fsharp
// Batch statt N+1
let ids = items |> List.map (fun r -> r.Id) |> List.toArray
let! rows = conn.QueryAsync<Row>(sql, {| Ids = ids |}) |> Async.AwaitTask
let grouped = rows |> Seq.groupBy (fun r -> r.ParentId) |> Map.ofSeq

// Multi-Table Write (SQLite Transaction)
let insertWithRelated (parent: Parent) (children: Child list) : Async<unit> =
    withTransactionAsync (fun conn -> async {
        let! _ = conn.ExecuteAsync(parentSql, parentParam) |> Async.AwaitTask
        let! _ = conn.ExecuteAsync(childSql, childParams) |> Async.AwaitTask
        return ()
    })

// Row Types intern halten
[<CLIMutable>]
type internal MyRow = { Id: string; Name: string }
```

## Grep-Checks

```bash
# N+1 Pattern: Loop mit Query
grep -n 'for.*in.*do' src/Server/Persistence.fs | head -5

# String-Interpolation in SQL
grep -n '\$".*SELECT\|\$".*INSERT\|\$".*UPDATE' src/Server/Persistence.fs
```
