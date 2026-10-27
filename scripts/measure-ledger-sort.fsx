// Messung der Kassenbuch-Sortierung bei 14.000 Buchungen (ADR 0051 Punkt 8, ledger-r72re).
//
// Läuft gegen eine Datei-Datenbank (wie im Container), nie gegen die Entwicklungsdatenbank:
//   dotnet build src/Server
//   DYLD_FALLBACK_LIBRARY_PATH=$PWD/src/Server/bin/Debug/net10.0/runtimes/osx-arm64/native \
//     dotnet fsi scripts/measure-ledger-sort.fsx <leeres Verzeichnis für die Messdatenbank>
// (Die DYLD-Variable braucht fsi, damit die native SQLite-Bibliothek gefunden wird.)
//
// Je Feld und Richtung: Seite 0 und Seite 50 von `getLedgerEntriesPage`, ein Aufwärmlauf,
// dann der Median aus fünf Läufen. Schwelle: höchstens 1000 ms. Ein Wert darüber heißt:
// keinen Index auf Verdacht anlegen, die Entscheidung neu treffen (ADR 0051 Punkt 8).

#r "../src/Server/bin/Debug/net10.0/FSharp.Core.dll"
#r "../src/Server/bin/Debug/net10.0/Dapper.dll"
#r "../src/Server/bin/Debug/net10.0/Microsoft.Data.Sqlite.dll"
#r "../src/Server/bin/Debug/net10.0/SQLitePCLRaw.core.dll"
#r "../src/Server/bin/Debug/net10.0/SQLitePCLRaw.batteries_v2.dll"
#r "../src/Server/bin/Debug/net10.0/SQLitePCLRaw.provider.e_sqlite3.dll"
#r "../src/Server/bin/Debug/net10.0/Thoth.Json.Net.dll"
#r "../src/Server/bin/Debug/net10.0/Shared.dll"
#r "../src/Server/bin/Debug/net10.0/Server.dll"

open System
open System.Diagnostics
open System.IO
open Shared.Domain
open Ledger.Events
open Ledger.Domain

let dataDir =
    match fsi.CommandLineArgs |> Array.tryItem 1 with
    | Some d -> d
    | None -> failwith "Aufruf: dotnet fsi scripts/measure-ledger-sort.fsx <Verzeichnis>"

Directory.CreateDirectory dataDir |> ignore
if File.Exists(Path.Combine(dataDir, "budgetbuddy.db")) then
    failwith "Im Verzeichnis liegt schon eine budgetbuddy.db, bitte ein leeres Verzeichnis angeben"
// Muss vor dem ersten Datenbankzugriff stehen (Persistence liest beides einmal, lazy).
Environment.SetEnvironmentVariable("USE_MEMORY_DB", "false")
Environment.SetEnvironmentVariable("DATA_DIR", dataDir)

let guid (kind: int) (n: int) = Guid.Parse $"7272{kind:D4}-0000-7000-8000-{n:D12}"
let eur (a: decimal) : Money = { Amount = a; Currency = "EUR" }

Ledger.Persistence.initializeLedgerSchema ()
let accounts = [ for i in 1..5 -> LedgerAccountId (guid 4 i) ]
accounts
|> List.iteri (fun i a ->
    Ledger.Persistence.saveAccount { Id = a; Name = $"Konto {i}"; Kind = BankAccount; Link = BbOwned; Closed = false }
    |> Async.RunSynchronously)

let categories : YnabCategory list =
    [ for i in 1..60 ->
          { Id = YnabCategoryId (guid 5 i); Name = $"Kategorie {i:D2}"; GroupName = $"Gruppe {i % 8}"; Available = None; Hidden = false } ]
Ledger.Persistence.upsertCategories categories |> Async.RunSynchronously

let rnd = Random 72
let events =
    [ for n in 1..14000 ->
          let date = DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(float (rnd.Next 900))
          let isSplit = n % 40 = 0
          let isTransfer = not isSplit && n % 53 = 0
          let category =
              if isSplit || isTransfer || n % 17 = 0 then None
              else
                  let c = categories.[rnd.Next 60]
                  Some { CategoryId = c.Id; CategoryName = c.Name }
          let amount = decimal (rnd.Next(-200000, 20000)) / 100m
          {
              EntryId = EntryId (guid 6 n)
              OccurredAt = date
              Actor = YnabMirror
              Body =
                  EntryObservedInYnab {
                      AccountId = accounts.[rnd.Next 5]
                      YnabTransactionId = $"measure-{n}"
                      ImportId = None
                      Date = date
                      Amount = eur amount
                      Payee = (if n % 23 = 0 then None else Some $"Payee {rnd.Next 40}")
                      Memo = None
                      Category = category
                      Splits =
                          (if isSplit then [ { Target = Uncategorized; Amount = eur amount; Memo = None } ] else [])
                      Cleared = Cleared
                      TransferAccountId = (if isTransfer then Some (YnabAccountId (guid 3 1)) else None)
                      TransferYnabTransactionId = None
                      Approved = true
                      FlagColor = None
                      FlagName = None
                  }
          } ]
for chunk in events |> List.chunkBySize 1000 do
    Ledger.Persistence.appendAndApply chunk |> Async.RunSynchronously

let query field direction pageIndex : LedgerEntryPageQuery = {
    AccountId = None
    Page = pageIndex
    PageSize = 100
    LocallyRemoved = 0
    IncludeDeleted = false
    OnlyNeedsReview = false
    WithoutCategory = false
    CategoryIds = []
    DateFrom = None
    DateTo = None
    Payee = None
    Memo = None
    AmountMin = None
    AmountMax = None
    Sort = { Field = field; Direction = direction }
}

let timeOnce q =
    let sw = Stopwatch.StartNew()
    Ledger.QueryHandlers.getLedgerEntriesPage q |> Async.RunSynchronously |> ignore
    sw.Elapsed.TotalMilliseconds

let median (xs: float list) = xs |> List.sort |> List.item (List.length xs / 2)

printfn "| Feld | Richtung | Seite | Median ms |"
printfn "|---|---|---|---|"
let mutable worst = 0.0
for field in [ SortByDate; SortByAmount; SortByPayee; SortByCategory; SortByAccount ] do
    for direction in [ SortAsc; SortDesc ] do
        for pageIndex in [ 0; 50 ] do
            let q = query field direction pageIndex
            timeOnce q |> ignore // Aufwärmlauf
            let m = median [ for _ in 1..5 -> timeOnce q ]
            worst <- max worst m
            printfn "| %A | %A | %d | %.0f |" field direction pageIndex m
printfn "Größter Median: %.0f ms (Schwelle 1000 ms)" worst
