module LedgerSortTests

// ledger-r72re (ADR 0051): Sortierung der Kassenbuch-Seite im Lesepfad. Eigene Fixtures
// (Guid-Präfix 7272, Namen mit Suffix R72). Die Gesamtsicht-Tests isolieren ihre Buchungen
// über ein eindeutiges Memo-Kennzeichen, weil die In-Memory-Datenbank im ganzen Testlauf
// geteilt wird. Die Messung bei 14.000 Buchungen steht in `scripts/measure-ledger-sort.fsx`.

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.QueryHandlers
open Ledger.CommandHandlers

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private guid (kind: int) (n: int) = Guid.Parse $"7272{kind:D4}-0000-7000-8000-{n:D12}"
let private accountA = LedgerAccountId (guid 1 1) // "Alpha R72"
let private accountC = LedgerAccountId (guid 1 2) // "charlie R72"
let private catRef (n: int) (name: string) : CategoryRef = { CategoryId = YnabCategoryId (guid 2 n); CategoryName = name }

type private Kind =
    | Plain
    | Split
    | Transfer

type private Spec = {
    N: int
    Account: LedgerAccountId
    Day: int
    Amount: decimal
    Payee: string option
    Category: CategoryRef option
    Kind: Kind
    Approved: bool
}

let private spec n account day amount payee category kind : Spec = {
    N = n
    Account = account
    Day = day
    Amount = amount
    Payee = payee
    Category = category
    Kind = kind
    Approved = true
}

let private entryGuid (tag: int) (n: int) = guid (100 + tag) n

// Datum im Jahr 2001: die Buchungen stehen in der Gesamtsicht hinter allen anderen Testdaten und
// verdrängen deren Erwartungen nicht von der ersten Seite.
let private observed (tag: int) (memo: string) (s: Spec) : LedgerEvent =
    let date = DateTime(2001, 8, s.Day, 0, 0, 0, DateTimeKind.Utc)
    {
        EntryId = EntryId (entryGuid tag s.N)
        OccurredAt = date
        Actor = YnabMirror
        Body =
            EntryObservedInYnab {
                AccountId = s.Account
                YnabTransactionId = $"r72-{tag}-{s.N}"
                ImportId = None
                Date = date
                Amount = eur s.Amount
                Payee = s.Payee
                Memo = Some memo
                Category = (if s.Kind = Plain then s.Category else None)
                Splits =
                    (if s.Kind = Split then
                         [ { Target = Uncategorized; Amount = eur s.Amount; Memo = None } ]
                     else
                         [])
                Cleared = (if s.Approved then Cleared else Uncleared)
                TransferAccountId = (if s.Kind = Transfer then Some (YnabAccountId (guid 3 1)) else None)
                TransferYnabTransactionId = None
                Approved = s.Approved
                FlagColor = None
                FlagName = None
            }
    }

let private ensureSchema () =
    initializeLedgerSchema ()
    saveAccount { Id = accountA; Name = "Alpha R72"; Kind = BankAccount; Link = BbOwned; Closed = false }
    |> Async.RunSynchronously
    saveAccount { Id = accountC; Name = "charlie R72"; Kind = BankAccount; Link = BbOwned; Closed = false }
    |> Async.RunSynchronously

let private seed (tag: int) (specs: Spec list) =
    ensureSchema ()
    appendAndApply (specs |> List.map (observed tag $"memo-r72-{tag}")) |> Async.RunSynchronously

let private baseQuery (tag: int) (sort: LedgerSort) : LedgerEntryPageQuery = {
    AccountId = None
    Page = 0
    PageSize = 100
    LocallyRemoved = 0
    IncludeDeleted = false
    OnlyNeedsReview = false
    WithoutCategory = false
    CategoryIds = []
    DateFrom = None
    DateTo = None
    Payee = None
    Memo = Some $"memo-r72-{tag}"
    AmountMin = None
    AmountMax = None
    Sort = sort
}

let private sortOf field direction : LedgerSort = { Field = field; Direction = direction }
let private allSorts =
    [ for f in [ SortByDate; SortByAmount; SortByPayee; SortByCategory; SortByAccount ] do
          for d in [ SortAsc; SortDesc ] -> sortOf f d ]

let private page (q: LedgerEntryPageQuery) = getLedgerEntriesPage q |> Async.RunSynchronously
let private idsOnPage (q: LedgerEntryPageQuery) = (page q).Entries |> List.map (fun e -> e.Id)

/// Alle Seiten nacheinander (kleine Seitengröße), ohne Ausgleich.
let private allPages (q: LedgerEntryPageQuery) : Guid list =
    let total = (page q).TotalCount
    [ for p in 0 .. (total / q.PageSize) -> idsOnPage { q with Page = p } ] |> List.concat

/// Die unabhängig berechnete Erwartung: erst nach `date DESC, id DESC` (Tiebreaker), dann
/// stabil nach dem Hauptschlüssel. Leere Schlüssel (None) stehen in beiden Richtungen am Ende.
let private expectedOrder (tag: int) (field: LedgerSortField) (direction: LedgerSortDirection) (specs: Spec list) =
    let tiebreak =
        specs |> List.sortBy (fun s -> s.Day, (entryGuid tag s.N).ToString()) |> List.rev
    let keyed (key: Spec -> 'k option) (descending: bool) =
        let present = tiebreak |> List.filter (fun s -> (key s).IsSome)
        let empty = tiebreak |> List.filter (fun s -> (key s).IsNone)
        let sorted =
            if descending then List.sortByDescending (fun s -> (key s).Value) present
            else List.sortBy (fun s -> (key s).Value) present
        sorted @ empty
    let desc = (direction = SortDesc)
    let ordered =
        match field with
        | SortByDate ->
            let dated = specs |> List.sortBy (fun s -> s.Day, (entryGuid tag s.N).ToString())
            if desc then List.rev dated else dated
        | SortByAmount -> keyed (fun s -> Some s.Amount) desc
        | SortByPayee ->
            keyed
                (fun s ->
                    match s.Payee with
                    | Some p when p.Trim() <> "" -> Some (p.ToLowerInvariant())
                    | _ -> None)
                desc
        | SortByCategory ->
            // Rang 0 nach dem Namen, Ränge 1 bis 3 danach in fester Reihenfolge, je nach Tiebreaker.
            let rank (s: Spec) = match s.Kind with | Plain when s.Category.IsSome -> 0 | Split -> 1 | Transfer -> 2 | _ -> 3
            let r0 = keyed (fun s -> if rank s = 0 then Some (s.Category.Value.CategoryName.ToLowerInvariant()) else None) desc |> List.filter (fun s -> rank s = 0)
            let rest r = tiebreak |> List.filter (fun s -> rank s = r)
            r0 @ rest 1 @ rest 2 @ rest 3
        | SortByAccount ->
            keyed (fun s -> Some (if s.Account = accountA then "alpha r72" else "charlie r72")) desc
    ordered |> List.map (fun s -> entryGuid tag s.N)

// Bestand mit absichtlich gleichen Schlüsseln (Datum, Betrag, Payee, Kategorie, Konto).
let private mixedSpecs =
    let c1, c2 = catRef 1 "Apfel", catRef 2 "Birne"
    [
        spec 1 accountA 5 -10m (Some "rewe") (Some c1) Plain
        spec 2 accountC 5 -10m (Some "REWE") (Some c2) Plain
        spec 3 accountA 3 -99.5m (Some "Aldi") (Some c1) Plain
        spec 4 accountC 3 -1000m None None Plain
        spec 5 accountA 7 12.3m (Some "Rewe") None Split
        spec 6 accountC 7 0m (Some "  ") None Transfer
        spec 7 accountA 3 -5m (Some "Zalando") (Some c2) Plain
        spec 8 accountC 9 -5m (Some "Aldi") None Plain
        spec 9 accountA 5 -10m (Some "") (Some c1) Plain
        spec 10 accountC 1 -99.5m (Some "aldi") None Split
    ]

// Kategorie-Umbenennung und Gruppen (Fixture-Kategorien 11 bis 13)
let private upsertFixtureCategories () =
    let cat n name group : YnabCategory =
        { Id = YnabCategoryId (guid 2 n); Name = name; GroupName = group; Available = None; Hidden = false }
    upsertCategories [ cat 11 "Aaa neu" "Gruppe 2"; cat 13 "Bbb" "Zeta"; cat 14 "Ccc" "  " ]
    |> Async.RunSynchronously

[<Tests>]
let sortTests =
    testSequenced
    <| testList "Kassenbuch-Sortierung im Lesepfad (ledger-r72re, ADR 0051)" [

        testCase "Standardsortierung liefert Datum absteigend mit id absteigend wie bisher" <| fun () ->
            seed 1 mixedSpecs
            let expected = expectedOrder 1 SortByDate SortDesc mixedSpecs
            Expect.equal (idsOnPage (baseQuery 1 LedgerSort.defaultSort)) expected "wie bisher"
            Expect.equal LedgerSort.defaultSort (sortOf SortByDate SortDesc) "Standard ist Datum absteigend"

        testCase "Jedes Feld und jede Richtung ordnet über mehrere Seiten wie erwartet, ohne Doppelte" <| fun () ->
            seed 2 mixedSpecs
            seed 2 [] // Schema idempotent
            for sort in allSorts do
                let q = { baseQuery 2 sort with PageSize = 3 }
                let actual = allPages q
                Expect.hasLength actual (List.length mixedSpecs) $"{sort}: Anzahl"
                Expect.equal (List.distinct actual |> List.length) (List.length actual) $"{sort}: keine Doppelten"
                Expect.equal actual (expectedOrder 2 sort.Field sort.Direction mixedSpecs) $"{sort}: Reihenfolge"

        testCase "Betrag ordnet numerisch, nicht als Text" <| fun () ->
            let amounts = [ -5m; 12.3m; -1000m; 0m; -99.5m ]
            seed 3 (amounts |> List.mapi (fun i a -> spec (i + 1) accountA (i + 1) a (Some "x") None Plain))
            let amountsOf sort =
                (page (baseQuery 3 sort)).Entries |> List.map (fun e -> e.Amount.Amount)
            Expect.equal (amountsOf (sortOf SortByAmount SortAsc)) [ -1000m; -99.5m; -5m; 0m; 12.3m ] "aufsteigend"
            Expect.equal (amountsOf (sortOf SortByAmount SortDesc)) [ 12.3m; 0m; -5m; -99.5m; -1000m ] "absteigend"

        testCase "Payee faltet Groß und Klein, leere Payees stehen in beiden Richtungen hinten" <| fun () ->
            let payees = [ Some "rewe"; Some "REWE"; Some "Rewe"; None; Some ""; Some "  "; Some "Aldi"; Some "Zalando" ]
            let specs = payees |> List.mapi (fun i p -> spec (i + 1) accountA (i + 1) -1m p None Plain)
            seed 4 specs
            let order sort = (page (baseQuery 4 sort)).Entries |> List.map (fun e -> e.Payee)
            let named (xs: string option list) = xs |> List.map (Option.defaultValue "")
            for direction in [ SortAsc; SortDesc ] do
                let result = order (sortOf SortByPayee direction)
                let firstFive = result |> List.take 5
                Expect.all firstFive (fun p -> match p with | Some x -> x.Trim() <> "" | None -> false) "die fünf benannten zuerst"
                let tail = result |> List.skip 5
                Expect.all tail (fun p -> match p with | Some x -> x.Trim() = "" | None -> true) "NULL, leer, Leerzeichen hinten"
            let asc = order (sortOf SortByPayee SortAsc) |> List.take 5 |> named
            Expect.equal asc.Head "Aldi" "aufsteigend beginnt mit Aldi"
            Expect.equal (List.last asc) "Zalando" "aufsteigend endet mit Zalando"
            // rewe, REWE, Rewe sind ein Schlüssel: dazwischen entscheidet das Datum (neueste zuerst).
            let middle = asc |> List.skip 1 |> List.take 3
            Expect.equal middle [ "Rewe"; "REWE"; "rewe" ] "gleicher Schlüssel, neueste zuerst (Tag 3, 2, 1)"
            let desc = order (sortOf SortByPayee SortDesc) |> List.take 5 |> named
            Expect.equal desc.Head "Zalando" "absteigend beginnt mit Zalando"

        testCase "Kategorie: Rang 0 nach Gruppe und Name, danach Split, Übertrag, ohne Kategorie, in beiden Richtungen" <| fun () ->
            upsertFixtureCategories ()
            // 11: gespeicherter Name alt, heutiger Name "Aaa neu" in Gruppe 2 -> "gruppe 2: aaa neu".
            // 12: nicht in ledger_categories -> nur der gespeicherte Name "Mmm".
            // 13: "Zeta: Bbb". 14: Gruppe nur Leerzeichen -> nur "Ccc".
            let specs = [
                spec 1 accountA 1 -1m (Some "a") (Some (catRef 11 "Zzz alt")) Plain
                spec 2 accountA 2 -1m (Some "b") (Some (catRef 12 "Mmm")) Plain
                spec 3 accountA 3 -1m (Some "c") (Some (catRef 13 "Bbb")) Plain
                spec 4 accountA 4 -1m (Some "d") (Some (catRef 14 "Ccc")) Plain
                spec 5 accountA 5 -1m (Some "e") None Split
                spec 6 accountA 6 -1m (Some "f") None Transfer
                spec 7 accountA 7 -1m (Some "g") None Plain
            ]
            seed 5 specs
            let ids sort = idsOnPage (baseQuery 5 sort)
            let g n = entryGuid 5 n
            Expect.equal (ids (sortOf SortByCategory SortAsc)) [ g 4; g 1; g 2; g 3; g 5; g 6; g 7 ] "aufsteigend: ccc, gruppe 2: aaa neu, mmm, zeta: bbb"
            Expect.equal (ids (sortOf SortByCategory SortDesc)) [ g 3; g 2; g 1; g 4; g 5; g 6; g 7 ] "absteigend: nur Rang 0 kehrt sich um"

        testCase "Konto: Gesamtsicht nach gefaltetem Kontonamen, mit AccountId die Standardreihenfolge" <| fun () ->
            seed 6 mixedSpecs
            let (LedgerAccountId rawA) = accountA
            let scoped sort = { baseQuery 6 sort with AccountId = Some rawA }
            Expect.equal
                (idsOnPage (scoped (sortOf SortByAccount SortAsc)))
                (idsOnPage (scoped LedgerSort.defaultSort))
                "ein Konto: Standardreihenfolge"
            let names = (page (baseQuery 6 (sortOf SortByAccount SortAsc))).Entries |> List.map (fun e -> e.AccountId)
            let (LedgerAccountId rawC) = accountC
            Expect.equal (names |> List.head) rawA "Alpha zuerst"
            Expect.equal (names |> List.last) rawC "charlie zuletzt"

        testCase "Trefferzahl, Summe und Zähler sind für alle zehn Sortierungen gleich" <| fun () ->
            seed 7 mixedSpecs
            let reference = page (baseQuery 7 LedgerSort.defaultSort)
            for sort in allSorts do
                let p = page (baseQuery 7 sort)
                Expect.equal p.TotalCount reference.TotalCount $"{sort}: TotalCount"
                Expect.equal p.TotalAmount reference.TotalAmount $"{sort}: TotalAmount"
                Expect.equal p.DeletedCount reference.DeletedCount $"{sort}: DeletedCount"
                Expect.equal p.NeedsReviewCount reference.NeedsReviewCount $"{sort}: NeedsReviewCount"

        testCase "Betragsfilter mit Sortierung nach Betrag, Kategorie und Payee liefert die gefilterte, sortierte Seite" <| fun () ->
            seed 8 mixedSpecs
            let filtered sort = { baseQuery 8 sort with AmountMin = Some -100m; AmountMax = Some -5m }
            let inRange = mixedSpecs |> List.filter (fun s -> s.Amount >= -100m && s.Amount <= -5m)
            for field in [ SortByAmount; SortByCategory; SortByPayee ] do
                for direction in [ SortAsc; SortDesc ] do
                    let q = { filtered (sortOf field direction) with PageSize = 3 }
                    let p = page q
                    Expect.equal p.TotalCount (List.length inRange) $"{field} {direction}: Trefferzahl"
                    Expect.equal (allPages q) (expectedOrder 8 field direction inRange) $"{field} {direction}: Reihenfolge"

        testCase "LocallyRemoved bleibt bei Sortierung nach Payee und Betrag lückenlos" <| fun () ->
            // Regression analog ledger-be1yh: Chip "Zu prüfen", drei Zeilen der Seite 0 geprüft,
            // Seite 1 mit LocallyRemoved = 3: die Verkettung ergibt die Treffermenge ohne Lücke.
            for field in [ SortByPayee; SortByAmount ] do
                let tag = if field = SortByPayee then 9 else 10
                let specs =
                    [ for n in 1..9 ->
                          { spec n accountA n (decimal (-n * 7 % 11) - 1m) (Some $"p{(n * 5) % 9}") None Plain with
                              Approved = false } ]
                seed tag specs
                let q page' = { baseQuery tag (sortOf field SortAsc) with OnlyNeedsReview = true; PageSize = 4; Page = page' }
                let all = expectedOrder tag field SortAsc specs
                Expect.equal (idsOnPage (q 0)) (List.take 4 all) "Seite 0 ohne Änderung"
                let page0 = idsOnPage (q 0)
                let reviewed = [ page0.[0]; page0.[1]; page0.[3] ]
                // Approval-Änderung aus YNAB: die Zeilen sind nun geprüft (Cleared war schon Cleared nicht,
                // daher beides setzen) und verlassen den Chip "Zu prüfen".
                let approved =
                    reviewed
                    |> List.collect (fun id ->
                        let at = DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
                        [ { EntryId = EntryId id; OccurredAt = at; Actor = YnabMirror
                            Body = EntryApprovalChangeObservedInYnab { OldApproved = false; NewApproved = true } }
                          { EntryId = EntryId id; OccurredAt = at; Actor = YnabMirror
                            Body = EntryClearedStateChangeObservedInYnab { OldCleared = Uncleared; NewCleared = Cleared } } ])
                appendAndApply approved |> Async.RunSynchronously
                let kept0 = page0 |> List.filter (fun id -> not (List.contains id reviewed))
                let later =
                    [ for p in 1..2 -> idsOnPage { q p with LocallyRemoved = 3 } ] |> List.concat
                let expected = all |> List.filter (fun id -> not (List.contains id reviewed))
                Expect.equal (kept0 @ later) expected $"{field}: lückenlos und ohne Doppelung"

        testCase "resolveReviewMarkBatchSelection ignoriert das Sort der Anfrage" <| fun () ->
            seed 11 (mixedSpecs |> List.map (fun s -> { s with Approved = false }))
            let excluded = [ entryGuid 11 1; entryGuid 11 4 ]
            let resolve sort =
                resolveReviewMarkBatchSelection (BatchAllExcept (baseQuery 11 sort, excluded))
                |> Async.RunSynchronously
            let byDefault = resolve LedgerSort.defaultSort
            let byAmount = resolve (sortOf SortByAmount SortAsc)
            Expect.hasLength byDefault 8 "zehn minus zwei Ausnahmen"
            Expect.equal byAmount byDefault "dieselbe Menge in derselben Reihenfolge"
    ]
