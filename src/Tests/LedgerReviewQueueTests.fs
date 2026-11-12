module LedgerReviewQueueTests

// Die Warteschlange des Durchgehens (ledger-v6s7c), Server-Seite:
//
//  * reine Reihenfolge `reviewQueueOrder`: wartende zuerst, dann Datum aufsteigend, Gleichstand EntryId;
//  * `getReviewQueue` gegen In-Memory-SQLite: alle ungeprüften, nicht gelöschten Buchungen jeder Herkunft,
//    dieselbe Menge wie `countNeedsReviewEntries`;
//  * „aus Bestellnummer“ im Lesepfad: nur bei einer Kategorie aus `OrderIdMatch`, weg nach Romans Änderung.

open System

do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Persistence

let private run (work: Async<'T>) = Async.RunSynchronously work
let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private at = DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc)
let private food = { CategoryId = YnabCategoryId (Guid.NewGuid()); CategoryName = "Lebensmittel" }
let private toys = { CategoryId = YnabCategoryId (Guid.NewGuid()); CategoryName = "Kinderkram" }

let private ev (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

let private guidOf (EntryId g) = g

// ============================================
// Reine Reihenfolge
// ============================================

let private view (id: string) (date: DateTime) (held: bool) : LedgerEntryView =
    { Id = Guid.Parse id
      AccountId = Guid.Empty
      Date = date
      Amount = eur -1m
      Payee = None
      Memo = None
      CategoryName = None
      CategoryId = None
      CategoryGroupName = None
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = ImportedFromComdirect
      DeletedInYnab = false
      Reconciled = false
      NeedsReview = true
      Flag = NoFlag
      IsTransfer = false
      TransferAccountId = None
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      MemoWriteBackFailure = None
      AmountWriteBackFailure = None
      AmountLockReason = None
      PayeeWriteBackFailure = None
      LinkedToYnab = true
      PushToYnabFailure = None
      SplitLocks = None
      ExternalLinks = []
      PossibleDuplicate =
        if held then
            Some { Reason = "gleich"; MatchDate = date; MatchAmount = eur -1m; MatchPayee = None; MatchCategoryName = None; MatchEntryId = None }
        else None
      CategoryFromOrderId = false }

let private idsOf (views: LedgerEntryView list) = views |> List.map (fun v -> v.Id.ToString().Substring(0, 1))

[<Tests>]
let orderTests =
    testList "Warteschlange: Reihenfolge (rein)" [

        testCase "ohne Buchung bleibt die Schlange leer" <| fun () ->
            Expect.isEmpty (reviewQueueOrder []) "leer"

        testCase "älteste zuerst, bei gleichem Datum die kleinere EntryId" <| fun () ->
            let d1 = DateTime(2026, 11, 1)
            let d2 = DateTime(2026, 11, 2)
            let entries =
                [ view "c0000000-0000-0000-0000-000000000000" d2 false
                  view "b0000000-0000-0000-0000-000000000000" d1 false
                  view "a0000000-0000-0000-0000-000000000000" d2 false ]
            Expect.equal (idsOf (reviewQueueOrder entries)) [ "b"; "a"; "c" ] "1.11. vor 2.11., dort a vor c"

        testCase "wartende Buchungen stehen vorn, auch wenn sie jünger sind" <| fun () ->
            let entries =
                [ view "a0000000-0000-0000-0000-000000000000" (DateTime(2026, 10, 1)) false
                  view "d0000000-0000-0000-0000-000000000000" (DateTime(2026, 11, 9)) true
                  view "b0000000-0000-0000-0000-000000000000" (DateTime(2026, 10, 2)) false
                  view "c0000000-0000-0000-0000-000000000000" (DateTime(2026, 11, 8)) true ]
            Expect.equal (idsOf (reviewQueueOrder entries)) [ "c"; "d"; "a"; "b" ] "erst die wartenden nach Datum, dann der Rest"
    ]

// ============================================
// Lesepfad gegen In-Memory-SQLite
// ============================================

let private comdirect (account: LedgerAccountId) (date: DateTime) : LedgerEventBody =
    let importId = $"BB:{Guid.NewGuid():N}"
    EntryImportedFromComdirect {
        AccountId = account
        ImportId = importId
        ComdirectReference = importId
        Date = date
        Amount = eur -20m
        Payee = Some "Markt v6s7c"
        Memo = None
        Cleared = Uncleared
        Approved = false
        FlagColor = None
        FlagName = None
    }

let private mirrored (account: LedgerAccountId) (date: DateTime) (approved: bool) : LedgerEventBody =
    EntryObservedInYnab {
        AccountId = account
        YnabTransactionId = $"ynab-{Guid.NewGuid():N}"
        ImportId = None
        Date = date
        Amount = eur -30m
        Payee = Some "Spiegel v6s7c"
        Memo = None
        Category = None
        Splits = []
        Cleared = (if approved then Cleared else Uncleared)
        TransferAccountId = None
        TransferYnabTransactionId = None
        Approved = approved
        FlagColor = None
        FlagName = None
    }

let private cash (account: LedgerAccountId) (date: DateTime) : LedgerEventBody =
    CashExpenseAdded {
        AccountId = account
        Date = date
        Amount = eur -5m
        Payee = Some "Kiosk v6s7c"
        Memo = None
        Category = None
        Cleared = Uncleared
        Approved = false
        ImportId = None
    }

let private held : EntryHeldAsPossibleDuplicatePayload =
    { Reason = "Gleicher Betrag, gleicher Tag, gleicher Payee, aber eine andere Bank-Referenz."
      MatchYnabTransactionId = "ynab-treffer-v6s7c"
      MatchEntryId = None
      Match = { Date = DateTime(2026, 11, 3); Amount = eur -20m; Payee = Some "Markt v6s7c"; CategoryName = None } }

let private seed (events: LedgerEvent list) = appendAndApply events |> run

let private recategorized (actor: Actor) (oldC: CategoryRef option) (newC: CategoryRef option) (entryId: EntryId) =
    ev entryId actor (EntryRecategorized { OldCategory = oldC; NewCategory = newC; OldSplits = []; NewSplits = []; OldPayee = None; NewPayee = None })

/// Ein Beispielwert je Typ für die Payloads (Review-Fund zu ledger-v6s7c): Optionen immer `Some`, damit
/// ein Ereignis, das die Kategorie setzt, eine neue Kategorie mitbringt; jede Guid neu, damit sie von der
/// Ausgangsbuchung abweicht; Listen leer (keine Splits).
let rec private sampleOf (t: Type) : obj =
    if t = typeof<string> then box "x"
    elif t = typeof<Guid> then box (Guid.NewGuid())
    elif t = typeof<DateTime> then box at
    elif t = typeof<decimal> then box 0m
    elif t = typeof<int> then box 0
    elif t = typeof<bool> then box false
    elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then
        let some = Reflection.FSharpType.GetUnionCases t |> Array.find (fun c -> c.Name = "Some")
        Reflection.FSharpValue.MakeUnion(some, [| sampleOf (t.GetGenericArguments()[0]) |])
    elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<list<_>> then
        let empty = Reflection.FSharpType.GetUnionCases t |> Array.find (fun c -> c.Name = "Empty")
        Reflection.FSharpValue.MakeUnion(empty, [||])
    elif Reflection.FSharpType.IsRecord t then
        Reflection.FSharpValue.MakeRecord(t, Reflection.FSharpType.GetRecordFields t |> Array.map (fun f -> sampleOf f.PropertyType))
    elif Reflection.FSharpType.IsUnion t then
        let first = (Reflection.FSharpType.GetUnionCases t).[0]
        Reflection.FSharpValue.MakeUnion(first, first.GetFields() |> Array.map (fun f -> sampleOf f.PropertyType))
    else failwith $"Kein Beispielwert für {t.FullName}"

[<Tests>]
let categorySettingTests =
    testList "aus Bestellnummer: welche Ereignisse die Kategorie setzen (rein)" [

        // Review-Fund zu ledger-v6s7c: `loadOrderIdCategorizedEntries` filtert mit `categorySettingEventTypes`.
        // Fehlt dort ein Ereignis, das die Kategorie ändert, bliebe „aus Bestellnummer“ nach der Änderung stehen.
        testCase "genau die Ereignisse der Liste ändern Kategorie oder Splits, jedes Ereignis wird geprüft" <| fun () ->
            let entryId = newEntryId ()
            let eventOf (body: LedgerEventBody) = ev entryId UserInBb body
            let bodies =
                Reflection.FSharpType.GetUnionCases typeof<LedgerEventBody>
                |> Array.map (fun c ->
                    Reflection.FSharpValue.MakeUnion(c, c.GetFields() |> Array.map (fun f -> sampleOf f.PropertyType)) :?> LedgerEventBody)
                |> List.ofArray
            let created = Ledger.Domain.apply None (eventOf (comdirect (LedgerAccountId (Guid.NewGuid())) at))
            let categorized = { created with Category = Some food; LinkedToYnab = true }
            let waiting = { created with PossibleDuplicate = Some held }
            let discarded = { waiting with DeletedInYnab = true }
            let categorizationOf (e: Ledger.Domain.Entry) = e.Category, e.Splits
            let isCreation (body: LedgerEventBody) =
                try Ledger.Domain.apply None (eventOf body) |> ignore; true
                with :? InvalidOperationException -> false
            // Ein Entstehungs-Ereignis legt die erste Kategorisierung fest und zählt damit; jedes andere wird auf
            // die Ausgangsbuchungen angewandt, auf denen es gilt.
            let changesCategory (body: LedgerEventBody) =
                if isCreation body then true
                else
                    let outcomes =
                        [ categorized; waiting; discarded ]
                        |> List.choose (fun before ->
                            try Some (categorizationOf (Ledger.Domain.apply (Some before) (eventOf body)) <> categorizationOf before)
                            with :? InvalidOperationException -> None)
                    Expect.isNonEmpty outcomes $"{eventTypeName body} gilt auf keiner Ausgangsbuchung"
                    List.contains true outcomes
            let changing = bodies |> List.filter changesCategory |> List.map eventTypeName |> Set.ofList
            Expect.equal changing (Set.ofList categorySettingEventTypes) "die Liste der Ereignisse, die die Kategorie setzen"
    ]

[<Tests>]
let queueTests =
    testSequenced <| testList "Warteschlange: getReviewQueue (In-Memory-SQLite)" [

        testCase "liefert jede ungeprüfte Herkunft, ohne geprüfte und gelöschte, in der Folge der Schlange" <| fun () ->
            initializeLedgerSchema ()
            let account = LedgerAccountId (Guid.NewGuid())
            let day n = DateTime(2026, 11, n)
            let fromImport = newEntryId ()
            let fromMirror = newEntryId ()
            let fromQuickAdd = newEntryId ()
            let fromBob = newEntryId ()
            let fromRule = newEntryId ()
            let waiting = newEntryId ()
            let reviewed = newEntryId ()
            let deleted = newEntryId ()
            seed [ ev fromImport ComdirectImport (comdirect account (day 4)) ]
            seed [ ev fromMirror YnabMirror (mirrored account (day 2) false) ]
            seed [ ev fromQuickAdd QuickAdd (cash account (day 6)) ]
            seed [ ev fromBob Bob (cash account (day 5)) ]
            seed [ ev fromRule ComdirectImport (comdirect account (day 1))
                   ev fromRule (Rule (RuleId (Guid.NewGuid()))) (EntryCategorizedByRule { RuleName = "Markt"; Category = Some food; Splits = []; PayeeOverride = None }) ]
            seed [ ev waiting ComdirectImport (comdirect account (day 9))
                   ev waiting ComdirectImport (EntryHeldAsPossibleDuplicate held) ]
            seed [ ev reviewed YnabMirror (mirrored account (day 3) true) ]
            seed [ ev deleted YnabMirror (mirrored account (day 3) false)
                   ev deleted YnabMirror EntryDeletionObservedInYnab ]

            let (LedgerAccountId accountGuid) = account
            let queue =
                Ledger.QueryHandlers.getReviewQueue () |> run |> List.filter (fun v -> v.AccountId = accountGuid)
            // Orakel von Hand: die wartende vorn, dann nach Datum 1., 2., 4., 5., 6.
            Expect.equal
                (queue |> List.map (fun v -> v.Id))
                ([ waiting; fromRule; fromMirror; fromImport; fromBob; fromQuickAdd ] |> List.map guidOf)
                "Reihenfolge und Menge"
            Expect.equal
                (queue |> List.map (fun v -> v.Origin))
                [ ImportedFromComdirect; ImportedFromComdirect; MirroredFromYnab; ImportedFromComdirect; AddedByBob; AddedViaQuickAdd ]
                "jede Herkunft"
            Expect.equal (List.length queue) (countNeedsReviewEntries (Some account) |> run) "dieselbe Zahl wie Zu prüfen"
            Expect.isSome (queue.Head.PossibleDuplicate) "die wartende trägt ihren Treffer"

        // Review-Fund zu ledger-v6s7c: „Ist doppelt, verwerfen“ nimmt die Buchung aus der Schlange, „Rückgängig“
        // stellt sie als wartende wieder vorn ein (ADR 0093 Punkt 4).
        testCase "eine als doppelt verworfene fehlt, nach Rückgängig steht sie wieder vorn" <| fun () ->
            initializeLedgerSchema ()
            let account = LedgerAccountId (Guid.NewGuid())
            let earlier = newEntryId ()
            let discarded = newEntryId ()
            seed [ ev earlier ComdirectImport (comdirect account (DateTime(2026, 11, 1))) ]
            seed [ ev discarded ComdirectImport (comdirect account (DateTime(2026, 11, 9)))
                   ev discarded ComdirectImport (EntryHeldAsPossibleDuplicate held)
                   ev discarded UserInBb (EntryDiscardedAsDuplicateInBb { MatchedYnabTransactionId = Some held.MatchYnabTransactionId; MatchedEntryId = None }) ]
            let (LedgerAccountId accountGuid) = account
            let queueIds () =
                Ledger.QueryHandlers.getReviewQueue () |> run |> List.filter (fun v -> v.AccountId = accountGuid) |> List.map (fun v -> v.Id)
            Expect.equal (queueIds ()) [ guidOf earlier ] "verworfen: nicht in der Schlange"
            seed [ ev discarded UserInBb EntryDiscardRevokedInBb ]
            Expect.equal (queueIds ()) [ guidOf discarded; guidOf earlier ] "nach Rückgängig: wartend vorn"

        testCase "aus Bestellnummer nur bei Kategorie aus OrderIdMatch, weg nach einer Änderung durch Roman" <| fun () ->
            initializeLedgerSchema ()
            let account = LedgerAccountId (Guid.NewGuid())
            let byOrder = newEntryId ()
            let byRule = newEntryId ()
            seed [ ev byOrder ComdirectImport (comdirect account (DateTime(2026, 11, 3)))
                   recategorized (OrderIdMatch "302-1234567-1234567") None (Some toys) byOrder ]
            seed [ ev byRule ComdirectImport (comdirect account (DateTime(2026, 11, 3)))
                   ev byRule (Rule (RuleId (Guid.NewGuid()))) (EntryCategorizedByRule { RuleName = "Markt"; Category = Some food; Splits = []; PayeeOverride = None }) ]
            let read (id: EntryId) = Ledger.QueryHandlers.getLedgerEntry (guidOf id) |> run |> Option.get
            let inQueue (id: EntryId) =
                Ledger.QueryHandlers.getReviewQueue () |> run |> List.find (fun v -> v.Id = guidOf id)
            Expect.isTrue (read byOrder).CategoryFromOrderId "Order-ID-Abgleich: Einzelabfrage"
            Expect.isTrue (inQueue byOrder).CategoryFromOrderId "Order-ID-Abgleich: Schlange"
            Expect.isFalse (read byRule).CategoryFromOrderId "Regel"
            seed [ recategorized UserInBb (Some toys) (Some food) byOrder ]
            Expect.isFalse (read byOrder).CategoryFromOrderId "nach Romans Änderung"
            Expect.isFalse (inQueue byOrder).CategoryFromOrderId "nach Romans Änderung, Schlange"
    ]
