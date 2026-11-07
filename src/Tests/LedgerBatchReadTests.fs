module LedgerBatchReadTests

// ledger-r8h19: Lesefunktionen für eine ganze Auswahl (Sammelbefehle, Spiegel-Lauf) statt einer
// Abfrage je Buchung. Je Funktion: leere Liste, mehr als 400 Ids (Stückgrenze) und eine
// unbekannte Id, die im Ergebnis fehlt.

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence

// Buchungsdatum weit in der Vergangenheit: die In-Memory-DB ist geteilt, und neueste-zuerst-Seiten
// der Gesamtsicht (LedgerPageTests) sollen diese 1.200 Buchungen nie auf Seite 0 sehen.
let private bookingDate = DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)

let private account = LedgerAccountId (Guid.Parse "0195d0b1-0000-7000-8000-000000000001")

let private observed (entryId: EntryId) (ynabTxId: string) (importId: string option) : LedgerEvent = {
    EntryId = entryId
    OccurredAt = DateTime(2026, 11, 7, 6, 0, 0, DateTimeKind.Utc)
    Actor = YnabMirror
    Body =
        EntryObservedInYnab {
            AccountId = account
            YnabTransactionId = ynabTxId
            ImportId = importId
            Date = bookingDate
            Amount = { Amount = -1m; Currency = "EUR" }
            Payee = Some "Stapel r8h19"
            Memo = None
            Category = None
            Splits = []
            Cleared = Uncleared
            TransferAccountId = None
            TransferYnabTransactionId = None
            Approved = true
            FlagColor = None
            FlagName = None
        }
}

/// Legt `count` Buchungen mit YNAB-Id und ImportId an, in einem Anhängen.
let private seed (count: int) : (EntryId * string * string) list =
    let tag = Guid.NewGuid().ToString("N").Substring(0, 8)
    let rows = [ for n in 1..count -> newEntryId (), $"r8h19-{tag}-{n}", $"BB:r8h19-{tag}-{n}" ]
    appendAndApply [ for entryId, ynabId, importId in rows -> observed entryId ynabId (Some importId) ]
    |> Async.RunSynchronously
    rows

[<Tests>]
let tests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testList "Ledger Persistence: Lesen für eine Auswahl (ledger-r8h19)" [

        testCase "loadEntries: leere Liste ergibt eine leere Map" <| fun () ->
            Expect.isEmpty (loadEntries [] |> Async.RunSynchronously) "leer"

        testCase "loadEntries: 401 Ids über die Stückgrenze, eine unbekannte fehlt" <| fun () ->
            let rows = seed 401
            let unknown = newEntryId ()
            let found = loadEntries (unknown :: [ for entryId, _, _ in rows -> entryId ]) |> Async.RunSynchronously
            Expect.equal found.Count 401 "alle 401 bekannten Buchungen"
            Expect.isFalse (found.ContainsKey unknown) "die unbekannte Id fehlt"
            let entryId, _, _ = List.last rows
            Expect.equal (Map.tryFind entryId found) (loadEntry entryId |> Async.RunSynchronously) "derselbe Zustand wie loadEntry"

        testCase "linksForEntries: leere Liste ergibt eine leere Map" <| fun () ->
            Expect.isEmpty (linksForEntries [] |> Async.RunSynchronously) "leer"

        testCase "linksForEntries: 401 Ids über die Stückgrenze, je Buchung wie linksForEntry, eine unbekannte fehlt" <| fun () ->
            let rows = seed 401
            let unknown = newEntryId ()
            let found = linksForEntries (unknown :: [ for entryId, _, _ in rows -> entryId ]) |> Async.RunSynchronously
            Expect.equal found.Count 401 "Verknüpfungen aller 401 Buchungen"
            Expect.isFalse (found.ContainsKey unknown) "die unbekannte Id fehlt"
            for entryId, _, _ in [ List.head rows; List.last rows ] do
                Expect.equal (Map.find entryId found) (linksForEntry entryId |> Async.RunSynchronously) "gleiche Liste und Reihenfolge wie linksForEntry"

        testCase "findEntriesByLinks: leere Liste ergibt eine leere Map" <| fun () ->
            Expect.isEmpty (findEntriesByLinks YnabSystem [] |> Async.RunSynchronously) "leer"

        testCase "findEntriesByLinks: 401 Werte über die Stückgrenze, je System, ein unbekannter fehlt" <| fun () ->
            let rows = seed 401
            let byYnab = findEntriesByLinks YnabSystem ("r8h19-unbekannt" :: [ for _, ynabId, _ in rows -> ynabId ]) |> Async.RunSynchronously
            Expect.equal byYnab.Count 401 "alle 401 YNAB-Ids"
            Expect.isFalse (byYnab.ContainsKey "r8h19-unbekannt") "der unbekannte Wert fehlt"
            for entryId, ynabId, _ in rows do
                Expect.equal (Map.tryFind ynabId byYnab) (Some entryId) "Wert zeigt auf seine Buchung"
            let entryId, ynabId, importId = List.head rows
            let byImport = findEntriesByLinks ImportIdSystem [ importId; ynabId ] |> Async.RunSynchronously
            Expect.equal byImport (Map.ofList [ importId, entryId ]) "nur Werte des gefragten Systems"
    ]
