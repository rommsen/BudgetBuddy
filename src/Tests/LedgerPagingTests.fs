module LedgerPagingTests

// ledger-be1yh: Seitenversatz beim Nachladen, wenn Zeilen lokal aus der geladenen Liste
// verschwunden sind. Client: der reine Anfragebau und der Zähler im Reducer. Server: der
// Versatz der Seite. Eigene Fixture-Ids (Präfix 9e9e9e9e, Namen mit Suffix Pg).

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.QueryHandlers
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

// ============================================
// Client
// ============================================

let private entryView (n: int) (needsReview: bool) : LedgerEntryView = {
    Id = Guid.Parse $"0195f900-0000-7000-8000-0000000000{n:D2}"
    AccountId = Guid.Parse "0195e900-0000-7000-8000-000000000001"
    Date = DateTime(2026, 10, 1)
    Amount = eur -10m
    Payee = Some $"Payee {n}"
    Memo = None
    CategoryName = None
    CategoryId = None
    CategoryGroupName = None
    SplitCategoryGroupNames = Map.empty
    Splits = []
    Origin = MirroredFromYnab
    DeletedInYnab = false
    NeedsReview = needsReview
    Flag = NoFlag
    IsTransfer = false
    ReviewMarkWriteBackFailure = None
    CategorizationWriteBackFailure = None
    FlagWriteBackFailure = None
    MemoWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
    SplitLocks = None
    ExternalLinks = []
}

/// Zwei geladene Seiten sind für den Test ein Modell, das schon bei Seite 2 steht und
/// mehr Treffer meldet, als geladen sind.
let private loadedModel (onlyNeedsReview: bool) (entries: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success entries
        OnlyNeedsReview = onlyNeedsReview
        NextPage = 2
        TotalCount = 250
        HasMoreEntries = true }

let private outcome : ReviewMarkOutcome = {
    Previous = { IsApproved = false; ClearedState = ReviewUncleared }
    Change = ReviewMarkChanged
    WriteBack = ReviewMarkSent
}

let private markReviewed (id: Guid) (model: Model) : Model =
    let model, _, _ = Components.Ledger.State.update (ReviewMarkSet (id, Reviewed, Ok outcome)) model
    model

let private nextQuery (model: Model) : LedgerEntryPageQuery =
    Components.Ledger.State.buildPageQuery model None model.NextPage

[<Tests>]
let clientTests =
    testList "Kassenbuch Nachladen (ledger-be1yh, Client)" [

        testCase "Chip aktiv, Zeile geprüft: die Nachladeanfrage trägt die entfernte Zeile" <| fun () ->
            // Verhindert: Der Server zählt den Versatz auf der gefilterten Menge neu. Ohne
            // diesen Ausgleich springt die erste Zeile der nächsten Seite um eins nach vorn
            // und die Buchung an der Seitengrenze fehlt in der Liste.
            let a, b = entryView 1 true, entryView 2 true
            let model = loadedModel true [ a; b ] |> markReviewed a.Id
            let query = nextQuery model
            Expect.equal query.Page 2 "weiter bei Seite 2"
            Expect.equal query.LocallyRemoved 1 "eine Zeile lokal entfernt"

        testCase "Zwei entfernte Zeilen werden beide gezählt" <| fun () ->
            let a, b, c = entryView 1 true, entryView 2 true, entryView 3 true
            let model = loadedModel true [ a; b; c ] |> markReviewed a.Id |> markReviewed b.Id
            Expect.equal (nextQuery model).LocallyRemoved 2 "zwei entfernt"

        testCase "Ohne aktiven Chip bleibt die Zeile stehen und die Anfrage unverändert" <| fun () ->
            let a, b = entryView 1 true, entryView 2 true
            let model = loadedModel false [ a; b ] |> markReviewed a.Id
            let query = nextQuery model
            Expect.equal query.LocallyRemoved 0 "nichts entfernt"
            Expect.equal query.Page 2 "Seite wie bisher"
            Expect.equal query.PageSize pageSize "Seitengröße wie bisher"

        testCase "Ohne jede Aktion ist die Anfrage die bisherige" <| fun () ->
            let model = loadedModel true [ entryView 1 true ]
            Expect.equal (nextQuery model).LocallyRemoved 0 "kein Ausgleich nötig"

        testCase "Ein Neuanstoß der Liste setzt den Zähler zurück" <| fun () ->
            let a = entryView 1 true
            let model = loadedModel true [ a ] |> markReviewed a.Id
            Expect.equal model.LocallyRemoved 1 "gezählt"
            let reloaded, _, _ = Components.Ledger.State.update LoadEntries model
            Expect.equal reloaded.LocallyRemoved 0 "zurückgesetzt"
    ]

// ============================================
// Server
// ============================================

let private accountGuid = Guid.Parse "9e9e9e9e-0000-7000-8000-000000000101"
let private account = LedgerAccountId accountGuid
let private entryGuid (n: int) = Guid.Parse $"9e9e9e9e-0000-7000-8000-0000000002{n:D2}"

let private observed (n: int) : LedgerEvent =
    let date = DateTime(2026, 9, n, 0, 0, 0, DateTimeKind.Utc)
    {
        EntryId = EntryId (entryGuid n)
        OccurredAt = date
        Actor = YnabMirror
        Body =
            EntryObservedInYnab {
                AccountId = account
                YnabTransactionId = $"pg-{n}"
                ImportId = None
                Date = date
                Amount = eur -10m
                Payee = Some "PayeePg"
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
    }

let private query (page: int) (removed: int) : LedgerEntryPageQuery = {
    AccountId = Some accountGuid
    Page = page
    PageSize = 2
    LocallyRemoved = removed
    IncludeDeleted = false
    OnlyNeedsReview = false
    OnlyAskLena = false
    WithoutCategory = false
    CategoryIds = []
    DateFrom = None
    DateTo = None
    Payee = None
    Memo = None
    AmountMin = None
    AmountMax = None
    Sort = LedgerSort.defaultSort
}

let private idsOf (q: LedgerEntryPageQuery) =
    (getLedgerEntriesPage q |> Async.RunSynchronously).Entries |> List.map (fun e -> e.Id)

[<Tests>]
let serverTests =
    testSequenced
    <| testList "Kassenbuch Nachladen (ledger-be1yh, Server)" [

        testCase "LocallyRemoved verschiebt den Versatz der Seite nach vorn" <| fun () ->
            initializeLedgerSchema ()
            saveAccount { Id = account; Name = "Konto Pg"; Kind = BankAccount; Link = BbOwned; Closed = false }
            |> Async.RunSynchronously
            // Neueste zuerst: 5, 4, 3, 2, 1.
            for n in 1..5 do
                appendAndApply [ observed n ] |> Async.RunSynchronously
            Expect.equal (idsOf (query 1 0)) [ entryGuid 3; entryGuid 2 ] "ohne Ausgleich wie bisher: Versatz 2"
            Expect.equal (idsOf (query 1 1)) [ entryGuid 4; entryGuid 3 ] "eine Zeile lokal entfernt: Versatz 1"
            Expect.equal (idsOf (query 1 9)) [ entryGuid 5; entryGuid 4 ] "der Versatz fällt nie unter 0"
            Expect.equal (idsOf (query 0 0)) [ entryGuid 5; entryGuid 4 ] "Seite 0 bleibt Seite 0"
    ]
