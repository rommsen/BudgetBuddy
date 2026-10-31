module CategoryMonthEntriesTests

// Die Lese-Naht ledger an budgeting (budgeting-m1bce): Buchungen einer Kategorie in einem Monat,
// gegen In-Memory-SQLite und über das Record-Feld der API, dazu die Grenze.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private at = DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc)

let private catRef (id: Guid) (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId id; CategoryName = name }

let private splitLine (c: CategoryRef) (amount: decimal) : TransactionSplit =
    { Target = ToCategory (c.CategoryId, c.CategoryName); Amount = eur amount; Memo = None }

let private observed (account: LedgerAccountId) (date: DateTime) (amount: decimal) : EntryObservedInYnabPayload = {
    AccountId = account
    YnabTransactionId = $"ynab-m1bce-{Guid.NewGuid()}"
    ImportId = None
    Date = date
    Amount = eur amount
    Payee = Some "Empfänger M1bce"
    Memo = None
    Category = None
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = None
    FlagName = None
}

let private seed (payload: EntryObservedInYnabPayload) : Guid =
    let entryId = newEntryId ()
    appendAndApply
        [ { EntryId = entryId; OccurredAt = at; Actor = YnabMirror; Body = EntryObservedInYnab payload } ]
    |> Async.RunSynchronously
    let (EntryId id) = entryId
    id

let private utc (y: int) (m: int) (d: int) = DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc)

let private query (category: CategoryRef) (month: string) : CategoryMonthQuery =
    let (YnabCategoryId id) = category.CategoryId
    { CategoryId = id; Month = month }

let private read (category: CategoryRef) (month: string) : CategoryMonthEntry list =
    match Server.Api.ledgerApi.getCategoryMonthEntries (query category month) |> Async.RunSynchronously with
    | Ok entries -> entries
    | Error message -> failtest message

let private freshCategory () = catRef (Guid.NewGuid()) "Kategorie M1bce"
let private freshAccount () = LedgerAccountId (Guid.NewGuid())

[<Tests>]
let shareTests =
    let category = freshCategory ()
    let other = freshCategory ()
    let entry (payload: EntryObservedInYnabPayload) : Entry =
        Ledger.Domain.apply None
            { EntryId = newEntryId (); OccurredAt = at; Actor = YnabMirror; Body = EntryObservedInYnab payload }
    let base' = observed (freshAccount ()) (utc 2026 10 1) -30m

    testList "Anteil einer Kategorie an einer Buchung" [
        testCase "Eine Buchung dieser Kategorie zählt ganz" <| fun () ->
            Expect.equal (categoryShare category.CategoryId (entry { base' with Category = Some category })) (Some (WholeEntry (eur -30m))) "ganz"

        testCase "Eine Buchung einer anderen Kategorie zählt nicht" <| fun () ->
            Expect.isNone (categoryShare category.CategoryId (entry { base' with Category = Some other })) "nichts"

        testCase "Bei einem Split zählt nur der Anteil dieser Kategorie" <| fun () ->
            let splits = [ splitLine category -10m; splitLine other -20m ]
            Expect.equal (categoryShare category.CategoryId (entry { base' with Splits = splits })) (Some (SplitShare (eur -10m))) "Anteil"

        testCase "Zwei Split-Zeilen derselben Kategorie ergeben einen Anteil" <| fun () ->
            let splits = [ splitLine category -10m; splitLine category -5m; splitLine other -15m ]
            Expect.equal (categoryShare category.CategoryId (entry { base' with Splits = splits })) (Some (SplitShare (eur -15m))) "Summe"

        testCase "Ein Split ohne diese Kategorie zählt nicht" <| fun () ->
            let splits = [ splitLine other -30m ]
            Expect.isNone (categoryShare category.CategoryId (entry { base' with Splits = splits })) "nichts"
    ]

[<Tests>]
let queryTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Buchungen einer Kategorie im Monat (SQLite)" [
        testCase "Monatsgrenzen: der letzte Tag zählt, der Erste des Folgemonats nicht" <| fun () ->
            let category = freshCategory ()
            let account = freshAccount ()
            let inMonth date = seed { observed account date -1m with Category = Some category }
            inMonth (utc 2026 9 30) |> ignore
            let first = inMonth (utc 2026 10 1)
            let last = inMonth (utc 2026 10 31)
            inMonth (utc 2026 11 1) |> ignore
            let ids = read category "2026-10-01" |> List.map (fun e -> e.EntryId)
            Expect.equal ids [ last; first ] "neueste zuerst, nur der Oktober"

        // B4: `AddMonths(1)` warf für den letzten darstellbaren Monat, die Abfrage brach ab.
        testCase "Der letzte darstellbare Monat geht ohne Folgemonat" <| fun () ->
            let category = freshCategory ()
            let last = seed { observed (freshAccount ()) (utc 9999 12 31) -1m with Category = Some category }
            Expect.equal (read category "9999-12-01" |> List.map (fun e -> e.EntryId)) [ last ] "der 31. zählt"

        testCase "Über alle Konten, mit Kontoname" <| fun () ->
            let category = freshCategory ()
            let named = freshAccount ()
            saveAccount
                { Id = named
                  Name = "Giro M1bce"
                  Kind = BankAccount
                  Link = BbOwned
                  Closed = false }
            |> Async.RunSynchronously
            let date = utc 2026 8 10
            seed { observed named date -1m with Category = Some category } |> ignore
            seed { observed (freshAccount ()) date -2m with Category = Some category } |> ignore
            let entries = read category "2026-08-01"
            Expect.equal entries.Length 2 "beide Konten"
            // B6: ein unbekanntes Konto kam als "" und ließ im Client einen Trenner ohne Namen stehen.
            let names = entries |> List.map (fun e -> e.AccountName) |> Set.ofList
            Expect.equal names (Set.ofList [ Some "Giro M1bce"; None ]) "Name des Kontos, unbekanntes Konto ohne Namen"

        testCase "Vom Split kommt nur der Anteil dieser Kategorie" <| fun () ->
            let category = freshCategory ()
            let other = freshCategory ()
            seed
                { observed (freshAccount ()) (utc 2026 7 10) -30m with
                    Splits = [ splitLine category -10m; splitLine other -20m ] }
            |> ignore
            match read category "2026-07-01" with
            | [ entry ] ->
                Expect.equal entry.Amount (eur -10m) "Anteil"
                Expect.isTrue entry.IsSplitShare "als Split gekennzeichnet"
            | other -> failtestf "eine Zeile erwartet: %A" other

        testCase "In YNAB gelöschte Buchungen fehlen" <| fun () ->
            let category = freshCategory ()
            let entryId = newEntryId ()
            appendAndApply
                [ { EntryId = entryId
                    OccurredAt = at
                    Actor = YnabMirror
                    Body = EntryObservedInYnab { observed (freshAccount ()) (utc 2026 6 10) -1m with Category = Some category } }
                  { EntryId = entryId; OccurredAt = at; Actor = YnabMirror; Body = EntryDeletionObservedInYnab } ]
            |> Async.RunSynchronously
            Expect.isEmpty (read category "2026-06-01") "gelöscht"

        testCase "Eine andere Kategorie bleibt draußen" <| fun () ->
            let category = freshCategory ()
            seed { observed (freshAccount ()) (utc 2026 5 10) -1m with Category = Some (freshCategory ()) } |> ignore
            Expect.isEmpty (read category "2026-05-01") "fremd"
    ]

[<Tests>]
let validationTests =
    let valid = query (freshCategory ()) "2026-10-01"
    let errorsOf q =
        match Server.Validation.validateCategoryMonthQuery q with
        | Ok _ -> []
        | Error errors -> errors

    testList "Grenze der Kategorie-Abfrage" [
        testCase "Eine gültige Anfrage ergibt den Monatsersten" <| fun () ->
            Expect.equal (Server.Validation.validateCategoryMonthQuery valid) (Ok (DateTime(2026, 10, 1))) "Erster"

        testCase "Ein Tag mitten im Monat wird abgelehnt" <| fun () ->
            Expect.hasLength (errorsOf { valid with Month = "2026-10-15" }) 1 "kein Monatserster"

        testCase "Ein unlesbarer Monat wird abgelehnt" <| fun () ->
            Expect.hasLength (errorsOf { valid with Month = "current" }) 1 "unlesbar"

        testCase "Eine fehlende Kategorie wird abgelehnt" <| fun () ->
            Expect.hasLength (errorsOf { valid with CategoryId = Guid.Empty }) 1 "leer"

        testCase "Alle Fehler kommen zugleich" <| fun () ->
            Expect.hasLength (errorsOf { CategoryId = Guid.Empty; Month = "x" }) 2 "beide"
    ]
