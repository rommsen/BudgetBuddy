module LedgerSplitReadTests

// Lesepfad der Split-Sperren (ledger-pafkp, ADR 0058 Punkt 2, 6 und 7): `LedgerEntryView.SplitLocks`
// trägt, wann Aufteilen und Ändern/Aufheben gesperrt sind und ob eine Neuanlage in YNAB offen ist.
// Gruppen: die reine Regel (`splitLocksOf`), die Sichten gegen In-Memory-SQLite
// (`testSequenced`, Schema-Init je Testfall, ADR 0030) und der Tracking-Anteil
// (`withTrackingLock`, `trackingLedgerAccounts`).

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Server.YnabObservations

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }

let private ledgerGuidPafkp = Guid.Parse "0195c0f0-0000-7000-8000-0000000000a1"
let private accountId = LedgerAccountId ledgerGuidPafkp
let private ynabAccountPafkp = YnabAccountId (Guid.Parse "0195c0f0-0000-7000-8000-0000000000b1")
let private cashAccountPafkp = Guid.Parse "0195c0f0-0000-7000-8000-0000000000b2"
let private at = DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc)

let private catRef (id: Guid) (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId id; CategoryName = name }

let private catA = catRef (Guid.Parse "0195c0f0-0000-7000-8000-00000000ca01") "Lebensmittel Pafkp"
let private catB = catRef (Guid.Parse "0195c0f0-0000-7000-8000-00000000ca02") "Drogerie Pafkp"

let private line (c: CategoryRef) (amount: decimal) : TransactionSplit =
    { Target = ToCategory (c.CategoryId, c.CategoryName); Amount = eur amount; Memo = None }

let private twoLines = [ line catA -5.00m; line catB -7.00m ]

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

let private observedPayload (ynabTxId: string) (importId: string option) : EntryObservedInYnabPayload = {
    AccountId = accountId
    YnabTransactionId = ynabTxId
    ImportId = importId
    Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -12.00m
    Payee = Some "Rewe Pafkp"
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

let private freshYnabId () = $"ynab-pafkp-{Guid.NewGuid()}"
let private freshBbImportId () = "BB:" + Guid.NewGuid().ToString("N").Substring(0, 20)

let private seed (payload: EntryObservedInYnabPayload) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab payload) ] |> Async.RunSynchronously
    entryId

let private entryOf (entryId: EntryId) : Entry = loadEntry entryId |> Async.RunSynchronously |> Option.get

let private viewOf (entryId: EntryId) : LedgerEntryView =
    let (EntryId id) = entryId
    Ledger.QueryHandlers.getLedgerEntriesForAccount ledgerGuidPafkp
    |> Async.RunSynchronously
    |> List.find (fun v -> v.Id = id)

let private locksOf (entryId: EntryId) : SplitLocks option = (viewOf entryId).SplitLocks

let private blockedSplit (l: SplitLocks option) = l |> Option.bind (fun x -> x.CannotSplitReason)
let private blockedChange (l: SplitLocks option) = l |> Option.bind (fun x -> x.CannotChangeReason)

[<Tests>]
let splitLocksRuleTests =
    // Reine Regel, ohne Datenbank: der Entry kommt aus `apply` eines Beobachtungs-Ereignisses.
    let entryFrom (payload: EntryObservedInYnabPayload) : Entry =
        let entryId = newEntryId ()
        let event = eventOn entryId YnabMirror (EntryObservedInYnab payload)
        Ledger.Domain.apply None event

    testList "Ledger Split-Sperren (Regel)" [

        testCase "eine gewöhnliche Buchung mit BB-import_id ist nicht gesperrt (None)" <| fun () ->
            let entry = entryFrom (observedPayload (freshYnabId ()) (Some (freshBbImportId ())))
            Expect.isNone (splitLocksOf false (Some "BB:abc") entry) "nichts gesperrt, nichts offen"

        testCase "abgeglichen sperrt Aufteilen und Ändern" <| fun () ->
            let entry = entryFrom { observedPayload (freshYnabId ()) None with Cleared = Reconciled }
            let locks = splitLocksOf false None entry
            Expect.equal (blockedSplit locks) (Some "Abgeglichene Buchungen lassen sich nicht aufteilen") "Aufteilen"
            Expect.equal (blockedChange locks) (blockedSplit locks) "Ändern gleich"

        testCase "Tracking-Konto sperrt Aufteilen und Ändern" <| fun () ->
            let entry = entryFrom (observedPayload (freshYnabId ()) None)
            let locks = splitLocksOf true None entry
            Expect.equal (blockedSplit locks) (Some trackingSplitReason) "Aufteilen"
            Expect.equal (blockedChange locks) (Some trackingSplitReason) "Ändern"

        testCase "Übertrag zwischen Budget-Konten sperrt Aufteilen und Ändern" <| fun () ->
            let entry =
                entryFrom { observedPayload (freshYnabId ()) None with TransferAccountId = Some (YnabAccountId cashAccountPafkp) }
            let locks = splitLocksOf false None entry
            Expect.isSome (blockedSplit locks) "Aufteilen gesperrt"
            Expect.isSome (blockedChange locks) "Ändern gesperrt"

        testCase "in YNAB gelöscht sperrt Aufteilen und Ändern" <| fun () ->
            let entry = entryFrom (observedPayload (freshYnabId ()) None)
            let deleted = { entry with DeletedInYnab = true }
            let locks = splitLocksOf false None deleted
            Expect.equal (blockedSplit locks) (Some "Die Buchung ist in YNAB gelöscht") "Aufteilen"
            Expect.equal (blockedChange locks) (Some "Die Buchung ist in YNAB gelöscht") "Ändern"

        testCase "fremde import_id sperrt nur Ändern und Aufheben einer Split-Buchung" <| fun () ->
            let split = entryFrom { observedPayload (freshYnabId ()) (Some "YNAB:-12000:2026-10-01:1") with Splits = twoLines }
            let locks = splitLocksOf false (Some "YNAB:-12000:2026-10-01:1") split
            Expect.isNone (blockedSplit locks) "Aufteilen bleibt erlaubt"
            Expect.equal (blockedChange locks) (Some foreignImportIdChangeReason) "Ändern gesperrt"
            let plain = entryFrom (observedPayload (freshYnabId ()) (Some "YNAB:-12000:2026-10-01:2"))
            let plainLocks = splitLocksOf false (Some "YNAB:-12000:2026-10-01:2") plain
            Expect.isNone (blockedSplit plainLocks) "unaufgeteilt: Aufteilen erlaubt"
            Expect.equal (blockedChange plainLocks) (Some foreignImportIdChangeReason) "aber eine Aufteilung ließe sich nicht zurücknehmen"

        testCase "eine Split-Buchung ohne import_id oder mit BB-import_id ist änderbar" <| fun () ->
            let split = entryFrom { observedPayload (freshYnabId ()) None with Splits = twoLines }
            Expect.isNone (splitLocksOf false None split) "ohne import_id"
            Expect.isNone (splitLocksOf false (Some "BB:abc:r1") split) "mit BB-import_id und Ersatz-Suffix"
    ]

[<Tests>]
let splitLocksViewTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            saveAccount {
                Id = accountId
                Name = "Girokonto Pafkp"
                Kind = BankAccount
                Link = YnabMirrored ynabAccountPafkp
                Closed = false
            }
            |> Async.RunSynchronously
            body ())

    testSequenced
    <| testList "Ledger Split-Sperren (Lesepfad)" [

        testCase "gewöhnliche Buchung: SplitLocks ist None" <| fun () ->
            let entryId = seed (observedPayload (freshYnabId ()) (Some (freshBbImportId ())))
            Expect.isNone (locksOf entryId) "nichts gesperrt"

        testCase "abgeglichen" <| fun () ->
            let entryId = seed { observedPayload (freshYnabId ()) None with Cleared = Reconciled }
            let locks = locksOf entryId
            Expect.isSome (blockedSplit locks) "Aufteilen gesperrt"
            Expect.isSome (blockedChange locks) "Ändern gesperrt"

        testCase "Übertrag zwischen Budget-Konten" <| fun () ->
            let entryId =
                seed { observedPayload (freshYnabId ()) None with TransferAccountId = Some (YnabAccountId cashAccountPafkp) }
            Expect.isSome (blockedSplit (locksOf entryId)) "Aufteilen gesperrt"

        testCase "in YNAB gelöscht" <| fun () ->
            let entryId = seed (observedPayload (freshYnabId ()) None)
            appendAndApply [ eventOn entryId YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            Expect.isSome (blockedSplit (locksOf entryId)) "Aufteilen gesperrt"
            Expect.isSome (blockedChange (locksOf entryId)) "Ändern gesperrt"

        testCase "Split-Buchung mit import_id YNAB: sperrt nur Ändern, aus der Verknüpfung gelesen" <| fun () ->
            let entryId = seed { observedPayload (freshYnabId ()) (Some "YNAB:-12000:2026-10-01:7") with Splits = twoLines }
            let locks = locksOf entryId
            Expect.isNone (blockedSplit locks) "Aufteilen nicht gesperrt"
            Expect.equal (blockedChange locks) (Some foreignImportIdChangeReason) "Ändern gesperrt"

        testCase "Split-Buchung mit BB-import_id: nicht gesperrt" <| fun () ->
            let entryId = seed { observedPayload (freshYnabId ()) (Some (freshBbImportId ())) with Splits = twoLines }
            Expect.isNone (locksOf entryId) "nichts gesperrt"

        testCase "Neuanlage offen: das DELETE gelang, das POST scheiterte" <| fun () ->
            let entryId = seed { observedPayload (freshYnabId ()) (Some (freshBbImportId ())) with Splits = twoLines }
            upsertCategories [
                for c in [ catA; catB ] ->
                    { Id = c.CategoryId; Name = c.CategoryName; GroupName = "Alltag"; Available = None; Hidden = false }
            ]
            |> Async.RunSynchronously
            let tools : SplitWriteTools = {
                IsTrackingAccount = fun _ -> async { return Ok false }
                PutCategory = fun _ _ -> async { return Ok () }
                PutSplits = fun _ _ -> async { return Ok () }
                Delete = fun _ -> async { return Ok () }
                Create = fun _ -> async { return Error (YnabError.NetworkError "weg") }
            }
            let outcome =
                setEntrySplits at tools entryId [ line catA -4.00m; line catB -8.00m ] |> Async.RunSynchronously
            match outcome with
            | Ok o -> Expect.equal (o.WriteBack |> function CategorySendFailed _ -> true | _ -> false) true "POST gescheitert"
            | Error e -> failtest e
            let locks = locksOf entryId
            Expect.equal (locks |> Option.map (fun l -> l.ReplacementPending)) (Some true) "Neuanlage offen"
            Expect.isNone (blockedChange locks) "Erneut senden bleibt möglich, Ändern nicht gesperrt"
            Expect.isSome (viewOf entryId).CategorizationWriteBackFailure "Grund steht weiter an der Buchung"

        testCase "getLedgerEntry liefert die einzelne Buchung mit ihren Sperren, eine unbekannte Id None" <| fun () ->
            let entryId = seed { observedPayload (freshYnabId ()) None with Cleared = Reconciled }
            let (EntryId id) = entryId
            let single = Ledger.QueryHandlers.getLedgerEntry id |> Async.RunSynchronously
            Expect.equal (single |> Option.map (fun v -> v.Id)) (Some id) "die Buchung"
            Expect.isSome (blockedSplit (single |> Option.bind (fun v -> v.SplitLocks))) "Aufteilen gesperrt"
            Expect.isNone (Ledger.QueryHandlers.getLedgerEntry (Guid.NewGuid()) |> Async.RunSynchronously) "unbekannt"

        testCase "Seitenabfrage trägt dieselben Sperren" <| fun () ->
            let entryId = seed { observedPayload (freshYnabId ()) None with Cleared = Reconciled }
            let (EntryId id) = entryId
            let query : LedgerEntryPageQuery =
                { AccountId = Some ledgerGuidPafkp
                  Page = 0
                  PageSize = 50
                  LocallyRemoved = 0
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
                  Sort = LedgerSort.defaultSort }
            let page = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously
            let view = page.Entries |> List.find (fun v -> v.Id = id)
            Expect.isSome (blockedSplit view.SplitLocks) "Aufteilen gesperrt"
    ]

[<Tests>]
let trackingLockTests =
    let ynabAccount (id: YnabAccountId) (onBudget: bool) : YnabAccount =
        { Id = id; Name = "Konto"; Balance = eur 0m; OnBudget = onBudget; Closed = false }

    let ledgerAccount (guid: Guid) (ynab: YnabAccountId) : LedgerAccount =
        { Id = LedgerAccountId guid; Name = "Konto"; Kind = BankAccount; Link = YnabMirrored ynab; Closed = false }

    let viewWith (accountGuid: Guid) : LedgerEntryView =
        { Id = Guid.NewGuid()
          AccountId = accountGuid
          Date = DateTime(2026, 10, 1)
          Amount = eur -12m
          Payee = None
          Memo = None
          CategoryName = None
          CategoryId = None
          CategoryGroupName = None
          SplitCategoryGroupNames = Map.empty
          Splits = []
          Origin = MirroredFromYnab
          DeletedInYnab = false
          NeedsReview = false
          Flag = NoFlag
          IsTransfer = false
          ReviewMarkWriteBackFailure = None
          CategorizationWriteBackFailure = None
          FlagWriteBackFailure = None
          LinkedToYnab = true
          PushToYnabFailure = None
          SplitLocks = None }

    testList "Ledger Split-Sperren (Tracking-Konten)" [

        testCase "trackingLedgerAccounts nennt nur gespiegelte Konten mit on_budget falsch" <| fun () ->
            let budgetYnab = YnabAccountId (Guid.NewGuid())
            let trackingYnab = YnabAccountId (Guid.NewGuid())
            let budgetLedger = Guid.NewGuid()
            let trackingLedger = Guid.NewGuid()
            let own = { ledgerAccount (Guid.NewGuid()) budgetYnab with Link = BbOwned }
            let result =
                Ledger.QueryHandlers.ReadModel.trackingLedgerAccounts
                    [ ynabAccount budgetYnab true; ynabAccount trackingYnab false ]
                    [ ledgerAccount budgetLedger budgetYnab; ledgerAccount trackingLedger trackingYnab; own ]
            Expect.equal result (Set.ofList [ trackingLedger ]) "nur das Tracking-Konto"

        testCase "withTrackingLock sperrt Aufteilen und Ändern auf einem Tracking-Konto und lässt andere in Ruhe" <| fun () ->
            let trackingLedger = Guid.NewGuid()
            let other = viewWith (Guid.NewGuid())
            let tracked = viewWith trackingLedger
            let tracking = Set.ofList [ trackingLedger ]
            let lockedView = Ledger.QueryHandlers.ReadModel.withTrackingLock tracking tracked
            Expect.equal (lockedView.SplitLocks |> Option.bind (fun l -> l.CannotSplitReason)) (Some trackingSplitReason) "Aufteilen"
            Expect.equal (lockedView.SplitLocks |> Option.bind (fun l -> l.CannotChangeReason)) (Some trackingSplitReason) "Ändern"
            Expect.equal (Ledger.QueryHandlers.ReadModel.withTrackingLock tracking other) other "anderes Konto unverändert"

        testCase "withTrackingLock behält den ersten Grund und die offene Neuanlage" <| fun () ->
            let trackingLedger = Guid.NewGuid()
            let existing =
                { CannotSplitReason = Some "Abgeglichene Buchungen lassen sich nicht aufteilen"
                  CannotChangeReason = Some "Abgeglichene Buchungen lassen sich nicht aufteilen"
                  ReplacementPending = true }
            let view = { viewWith trackingLedger with SplitLocks = Some existing }
            let result = Ledger.QueryHandlers.ReadModel.withTrackingLock (Set.ofList [ trackingLedger ]) view
            Expect.equal result.SplitLocks (Some existing) "bleibt, wie es war"
    ]
