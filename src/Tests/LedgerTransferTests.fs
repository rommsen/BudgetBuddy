module LedgerTransferTests

// Buchung zum Übertrag machen und zurück (ledger-qvjvs).
//
// Gruppen:
//  * reine Domäne: `apply` der beiden Ereignisse, Codec, `observe` mit offenem Fehlschlag;
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall) mit injizierten
//    Werkzeugen (Konten und PUT), die nie YNAB erreichen und ihre Aufrufe aufzeichnen;
//  * der Spiegel nach dem Push: Gegenbuchung wird verknüpft, keine fremde zweite Buchung;
//  * der PUT an YNAB gegen einen Fake-`HttpMessageHandler`;
//  * die Validierung an der API-Grenze.

open System
open System.Net
open System.Net.Http

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Server.YnabObservations
open Server.YnabClient

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private at = DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc)

let private catA : CategoryRef =
    { CategoryId = YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-00000000aa01"); CategoryName = "Lebensmittel" }

let private catB : CategoryRef =
    { CategoryId = YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-00000000aa02"); CategoryName = "Drogerie" }

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

/// Ein Satz Konten je Testfall: das eigene (Girokonto), ein Ziel (Tagesgeld), ein
/// Tracking-Konto, ein geschlossenes und ein Budget-Konto ohne Übertrags-Payee.
type private Accounts = {
    Own: YnabAccountId
    Ledger: LedgerAccountId
    Target: YnabAccountId
    Second: YnabAccountId
    Tracking: YnabAccountId
    Closed: YnabAccountId
    NoPayee: YnabAccountId
    List: TransferAccount list
}

let private freshAccounts () : Accounts =
    let id () = YnabAccountId (Guid.NewGuid())
    let own, target, second, tracking, closed, noPayee = id (), id (), id (), id (), id (), id ()
    let ledger = LedgerAccountId (Guid.NewGuid())
    saveAccount { Id = ledger; Name = "Girokonto"; Kind = BankAccount; Link = YnabMirrored own; Closed = false }
    |> Async.RunSynchronously
    let account (acc: YnabAccountId) name onBudget closed payee : TransferAccount =
        { Id = acc; Name = name; OnBudget = onBudget; Closed = closed; TransferPayeeId = payee }
    {
        Own = own
        Ledger = ledger
        Target = target
        Second = second
        Tracking = tracking
        Closed = closed
        NoPayee = noPayee
        List = [
            account own "Girokonto" true false (Some "payee-own")
            account target "Tagesgeld ING" true false (Some "payee-tagesgeld")
            account second "Bar" true false (Some "payee-bar")
            account tracking "Depot" false false (Some "payee-depot")
            account closed "Altes Konto" true true (Some "payee-alt")
            account noPayee "Ohne Payee" true false None
        ]
    }

let private observedPayload (accounts: Accounts) (ynabTxId: string) : EntryObservedInYnabPayload = {
    AccountId = accounts.Ledger
    YnabTransactionId = ynabTxId
    ImportId = None
    Date = DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -500.00m
    Payee = Some "Rewe"
    Memo = None
    Category = Some catA
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = None
    FlagName = None
}

let private seedCategories () =
    upsertCategories [
        for c in [ catA; catB ] ->
            { Id = c.CategoryId; Name = c.CategoryName; GroupName = "Alltag"; Available = None; Hidden = false }
    ]
    |> Async.RunSynchronously

/// Eine mit YNAB verknüpfte Buchung mit Kategorie A auf dem eigenen Konto.
let private seedLinked (accounts: Accounts) : EntryId * string =
    let entryId = newEntryId ()
    let ynabId = $"ynab-qvjvs-{Guid.NewGuid()}"
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload accounts ynabId)) ]
    |> Async.RunSynchronously
    entryId, ynabId

let private entryOf (entryId: EntryId) : Entry = loadEntry entryId |> Async.RunSynchronously |> Option.get

let private eventTypesOf (entryId: EntryId) : string list =
    loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)

/// Werkzeuge, die jeden PUT aufzeichnen und ein festes Ergebnis liefern.
type private TransferProbe(accounts: TransferAccount list, result: YnabResult<unit>) =
    member val Calls : (string * TransferWrite) list = [] with get, set
    member val AccountCalls = 0 with get, set
    member this.Tools : TransferWriteTools =
        { Accounts =
            fun () ->
                async {
                    this.AccountCalls <- this.AccountCalls + 1
                    return Ok accounts
                }
          Push =
            fun ynabId write ->
                async {
                    this.Calls <- this.Calls @ [ ynabId, write ]
                    return result
                } }

/// Split-Werkzeuge, die der Übertrags-Weg nie braucht.
let private noSplit : SplitWriteTools =
    let error = YnabError.NetworkError "kein Split im Test"
    { IsTrackingAccount = fun _ -> async { return Error error }
      PutCategory = fun _ _ -> async { return Error error }
      PutSplits = fun _ _ -> async { return Error error }
      Delete = fun _ -> async { return Error error }
      Create = fun _ -> async { return Error error } }

let private okCategoryPush : CategoryPush = fun _ _ -> async { return Ok () }

let private unwrap (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"Befehl sollte gelingen, war: {e}"

let private setTransfer (probe: TransferProbe) (entryId: EntryId) (target: YnabAccountId) =
    setEntryTransfer at probe.Tools entryId target |> Async.RunSynchronously

let private setCategory (probe: TransferProbe) (entryId: EntryId) (target: CategoryRef option) =
    setEntryCategory at okCategoryPush probe.Tools entryId (target |> Option.map (fun c -> c.CategoryId))
    |> Async.RunSynchronously

// ============================================
// Domäne
// ============================================

/// Konten ohne Datenbank, nur für die reinen Domänen-Tests.
let private freshAccountsPure () : Accounts =
    let id () = YnabAccountId (Guid.NewGuid())
    { Own = id (); Ledger = LedgerAccountId (Guid.NewGuid()); Target = id (); Second = id (); Tracking = id ()
      Closed = id (); NoPayee = id (); List = [] }

let private baseEntry () : Entry =
    let entryId = newEntryId ()
    apply None (eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (freshAccountsPure ()) "ynab-pure")))

let private setBody (oldTarget: YnabAccountId option) (target: YnabAccountId) (name: string) (oldPayee: string option) =
    EntryTransferTargetSetInBb {
        OldCategory = None
        OldPayee = oldPayee
        OldTransferAccountId = oldTarget
        NewTransferAccountId = target
        NewTransferAccountName = name
        TransferPayeeId = "payee"
        NewPayee = Some (transferPayeeName name)
    }

[<Tests>]
let transferDomainTests =
    testList "Übertrag in BB: Domäne (ledger-qvjvs)" [

        testCase "zum Übertrag: ohne Kategorie, mit Ziel, Payee wie YNAB, der Payee davor bleibt gemerkt" <| fun () ->
            let entry = baseEntry ()
            let target = YnabAccountId (Guid.NewGuid())
            let after = apply (Some entry) (eventOn entry.Id UserInBb (setBody None target "Tagesgeld ING" entry.Payee))
            Expect.isNone after.Category "keine Kategorie"
            Expect.equal after.TransferAccountId (Some target) "Ziel"
            Expect.equal after.Payee (Some "Transfer : Tagesgeld ING") "Payee wie YNAB"
            Expect.equal after.PayeeBeforeTransfer (Some "Rewe") "Payee davor"

        testCase "ein Zielwechsel behält den Payee vor dem ersten Übertrag" <| fun () ->
            let entry = baseEntry ()
            let a, b = YnabAccountId (Guid.NewGuid()), YnabAccountId (Guid.NewGuid())
            let first = apply (Some entry) (eventOn entry.Id UserInBb (setBody None a "A" entry.Payee))
            let second = apply (Some first) (eventOn entry.Id UserInBb (setBody (Some a) b "B" first.Payee))
            Expect.equal second.TransferAccountId (Some b) "neues Ziel"
            Expect.equal second.PayeeBeforeTransfer (Some "Rewe") "nicht \"Transfer : A\""

        testCase "zurück: Kategorie und Payee aus BB, kein Ziel, keine Gegenbuchung" <| fun () ->
            let entry = baseEntry ()
            let a = YnabAccountId (Guid.NewGuid())
            let transfer = apply (Some entry) (eventOn entry.Id UserInBb (setBody None a "A" entry.Payee))
            let linked = { transfer with TransferEntryId = Some (newEntryId ()) }
            let back =
                apply (Some linked) (
                    eventOn entry.Id UserInBb (
                        EntryTransferTargetClearedInBb {
                            OldTransferAccountId = a
                            NewCategory = Some catB
                            OldPayee = linked.Payee
                            NewPayee = Some "Rewe"
                        }))
            Expect.equal back.Category (Some catB) "Kategorie"
            Expect.equal back.Payee (Some "Rewe") "Payee aus BB"
            Expect.isNone back.TransferAccountId "kein Ziel"
            Expect.isNone back.TransferEntryId "keine Gegenbuchung"
            Expect.isNone back.PayeeBeforeTransfer "nichts mehr gemerkt"

        testCase "beide Ereignisse überstehen Codec und Typnamen" <| fun () ->
            let a = YnabAccountId (Guid.NewGuid())
            let bodies = [
                setBody (Some a) a "Tagesgeld ING" (Some "Rewe")
                EntryTransferTargetClearedInBb { OldTransferAccountId = a; NewCategory = None; OldPayee = Some "Transfer : X"; NewPayee = Some "Rewe" }
            ]
            for body in bodies do
                let decoded = decodePayload (eventTypeName body) (schemaVersion body) (encodePayload body)
                Expect.equal decoded (Ok body) (eventTypeName body)

        testCase "die Baseline des Fehlschlags trägt das Ziel, ältere Ereignisse ohne Feld lesen sich als None" <| fun () ->
            let a = YnabAccountId (Guid.NewGuid())
            let body =
                EntryCategorizationWriteBackFailed
                    { Reason = "weg"; BaselineCategory = None; BaselineSplits = []; YnabDeletedTransactionId = None; BaselineTransferAccountId = Some a }
            Expect.equal (decodePayload (eventTypeName body) 1 (encodePayload body)) (Ok body) "mit Ziel"
            match decodePayload "EntryCategorizationWriteBackFailed" 1 """{"reason":"alt","baselineCategory":null,"baselineSplits":[]}""" with
            | Ok (EntryCategorizationWriteBackFailed p) -> Expect.isNone p.BaselineTransferAccountId "alt: None"
            | other -> failtest $"{other}"

        testCase "observe bei offenem Fehlschlag eines Zielwechsels: YNAB zeigt noch das alte Ziel, kein Ereignis" <| fun () ->
            let entry = baseEntry ()
            let a, b = YnabAccountId (Guid.NewGuid()), YnabAccountId (Guid.NewGuid())
            let onA = apply (Some entry) (eventOn entry.Id UserInBb (setBody None a "A" entry.Payee))
            let onB = apply (Some onA) (eventOn entry.Id UserInBb (setBody (Some a) b "B" onA.Payee))
            let failed =
                apply (Some onB) (
                    eventOn entry.Id UserInBb (
                        EntryCategorizationWriteBackFailed
                            { Reason = "weg"; BaselineCategory = None; BaselineSplits = []; YnabDeletedTransactionId = None; BaselineTransferAccountId = Some a }))
            let ctx = { EntryId = entry.Id; AccountId = entry.AccountId; ObservedAt = at }
            let obs (target: YnabAccountId) (payee: string) : YnabObservation = {
                YnabTransactionId = "ynab-pure"; AccountId = YnabAccountId (Guid.NewGuid()); ImportId = None
                Date = entry.Date; Amount = entry.Amount; Payee = Some payee; Memo = entry.Memo; Category = None
                Splits = []; SplitTransfers = []; Cleared = entry.Cleared; TransferAccountId = Some target
                TransferYnabTransactionId = None; Deleted = false; Approved = true; FlagColor = None; FlagName = None }
            let stillA = observe ctx (Some failed) (obs a "Transfer : B")
            Expect.isEmpty stillA "YNAB hat den Push nie gesehen, Fehlschlag bleibt"
            let arrived = observe ctx (Some failed) (obs b "Transfer : B")
            Expect.equal (arrived |> List.map (fun e -> eventTypeName e.Body)) [ "EntryCategorizationWrittenBackToYnab" ] "angekommen"
    ]

// ============================================
// Befehle
// ============================================

[<Tests>]
let transferCommandTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            seedCategories ()
            body ())

    testSequenced
    <| testList "Übertrag in BB: Befehle (ledger-qvjvs)" [

        testCase "zum Übertrag: erst das Ereignis, dann der PUT mit der transfer_payee_id des Ziels und ohne Kategorie" <| fun () ->
            let accounts = freshAccounts ()
            let entryId, ynabId = seedLinked accounts
            let mutable typesAtPush = []
            let probe = TransferProbe(accounts.List, Ok ())
            let tools =
                { probe.Tools with
                    Push = fun id write -> typesAtPush <- eventTypesOf entryId; probe.Tools.Push id write }
            let outcome = setEntryTransfer at tools entryId accounts.Target |> Async.RunSynchronously |> unwrap
            Expect.equal outcome.Change CategoryChanged "geändert"
            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.equal outcome.Previous (Some { CategoryId = catA.CategoryId; CategoryName = catA.CategoryName }) "vorher Kategorie A"
            Expect.isNone outcome.PreviousTransferAccountId "vorher kein Übertrag"
            Expect.equal typesAtPush [ "EntryObservedInYnab"; "EntryTransferTargetSetInBb" ] "Ereignis vor dem Push"
            Expect.equal probe.Calls [ ynabId, MakeTransfer "payee-tagesgeld" ] "PUT mit der Übertrags-Payee, ohne Kategorie"
            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously |> List.last
            Expect.equal recorded.Event.Actor UserInBb "Actor"
            match recorded.Event.Body with
            | EntryTransferTargetSetInBb p ->
                Expect.equal p.NewTransferAccountId accounts.Target "Ziel"
                Expect.equal p.TransferPayeeId "payee-tagesgeld" "Payee-Id"
                Expect.equal p.OldCategory (Some catA) "alte Kategorie"
            | other -> failtest $"{other}"
            let entry = entryOf entryId
            Expect.equal entry.TransferAccountId (Some accounts.Target) "Zustand: Ziel"
            Expect.isNone entry.Category "Zustand: ohne Kategorie"
            let view = Ledger.QueryHandlers.getLedgerEntry (let (EntryId g) = entryId in g) |> Async.RunSynchronously |> Option.get
            Expect.isTrue view.IsTransfer "die Liste zeigt einen Übertrag"
            Expect.equal view.TransferAccountId (Some (let (YnabAccountId g) = accounts.Target in g)) "Detail kennt das Ziel"

        testCase "Ablehnungen ohne Ereignis und ohne PUT, jede mit eigenem Satz" <| fun () ->
            let accounts = freshAccounts ()
            let expectRejected (entryId: EntryId) (target: YnabAccountId) (expected: string) =
                let before = eventTypesOf entryId
                let probe = TransferProbe(accounts.List, Ok ())
                match setTransfer probe entryId target with
                | Error reason -> Expect.equal reason expected expected
                | Ok o -> failtest $"sollte abgelehnt werden ({expected}), war {o}"
                Expect.isEmpty probe.Calls $"kein PUT ({expected})"
                Expect.equal (eventTypesOf entryId) before $"kein Ereignis ({expected})"

            // unbekannte Buchung
            let probe = TransferProbe(accounts.List, Ok ())
            Expect.equal (setTransfer probe (newEntryId ()) accounts.Target) (Error "Buchung nicht gefunden") "unbekannt"
            Expect.isEmpty probe.Calls "unbekannt: kein PUT"

            // in YNAB gelöscht
            let deleted, _ = seedLinked accounts
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            expectRejected deleted accounts.Target "Die Buchung ist in YNAB gelöscht"

            // nicht verknüpft
            let unlinked = newEntryId ()
            appendAndApply [
                eventOn unlinked QuickAdd (
                    CashExpenseAdded {
                        AccountId = accounts.Ledger; Date = at; Amount = eur -3m; Payee = Some "Kiosk"; Memo = None
                        Category = None; Cleared = Uncleared; Approved = false; ImportId = None })
            ]
            |> Async.RunSynchronously
            expectRejected unlinked accounts.Target transferNotLinkedReason

            // das eigene Konto, Tracking, geschlossen, unbekannt, ohne Übertrags-Payee
            let linked, _ = seedLinked accounts
            expectRejected linked accounts.Own transferOwnAccountReason
            expectRejected linked accounts.Tracking transferTargetNotBudgetReason
            expectRejected linked accounts.Closed transferTargetNotBudgetReason
            expectRejected linked (YnabAccountId (Guid.NewGuid())) transferTargetNotBudgetReason
            expectRejected linked accounts.NoPayee transferNoPayeeReason

            // Split: die Kategorie ist nicht änderbar
            let split, _ = seedLinked accounts
            appendAndApply [
                eventOn split UserInBb (
                    EntryRecategorized {
                        OldCategory = Some catA; NewCategory = None; OldSplits = []
                        NewSplits = [
                            { Target = ToCategory (catA.CategoryId, catA.CategoryName); Amount = eur -300m; Memo = None }
                            { Target = ToCategory (catB.CategoryId, catB.CategoryName); Amount = eur -200m; Memo = None }
                        ]
                        OldPayee = Some "Rewe"; NewPayee = Some "Rewe" })
            ]
            |> Async.RunSynchronously
            expectRejected split accounts.Target transferSplitReason

        testCase "dasselbe Ziel noch einmal: kein Ereignis, kein PUT" <| fun () ->
            let accounts = freshAccounts ()
            let entryId, _ = seedLinked accounts
            setTransfer (TransferProbe(accounts.List, Ok ())) entryId accounts.Target |> unwrap |> ignore
            let before = eventTypesOf entryId
            let probe = TransferProbe(accounts.List, Ok ())
            let outcome = setTransfer probe entryId accounts.Target |> unwrap
            Expect.equal outcome.Change CategoryUnchanged "unverändert"
            Expect.isEmpty probe.Calls "kein PUT"
            Expect.equal (eventTypesOf entryId) before "kein Ereignis"

        testCase "scheitert der PUT: Fehlschlag mit Grund im Log und im Detail, Erneut senden löst ihn auf" <| fun () ->
            let accounts = freshAccounts ()
            let entryId, ynabId = seedLinked accounts
            let failing = TransferProbe(accounts.List, Error (YnabError.InvalidResponse "kaputt"))
            let outcome = setTransfer failing entryId accounts.Target |> unwrap
            let reason =
                match outcome.WriteBack with
                | CategorySendFailed r -> r
                | other -> failtest $"erwartet CategorySendFailed, war {other}"
            Expect.stringContains reason "kaputt" "Grund"
            Expect.equal
                (eventTypesOf entryId)
                [ "EntryObservedInYnab"; "EntryTransferTargetSetInBb"; "EntryCategorizationWriteBackFailed" ]
                "Änderung, dann Fehlschlag"
            let failure = (entryOf entryId).CategorizationWriteBackFailure |> Option.get
            Expect.equal failure.BaselineCategory (Some catA) "Baseline: Kategorie A"
            Expect.isNone failure.BaselineTransferAccountId "Baseline: kein Übertrag"
            let view = Ledger.QueryHandlers.getLedgerEntry (let (EntryId g) = entryId in g) |> Async.RunSynchronously |> Option.get
            Expect.equal view.CategorizationWriteBackFailure (Some reason) "das Detail zeigt den Grund"
            let ok = TransferProbe(accounts.List, Ok ())
            let retried =
                retryCategorizationOrSplitWriteBack at okCategoryPush noSplit ok.Tools entryId
                |> Async.RunSynchronously
                |> unwrap
            Expect.equal retried CategorySent "gesendet"
            Expect.equal ok.Calls [ ynabId, MakeTransfer "payee-tagesgeld" ] "Erneut senden schickt den Übertrag"
            Expect.isNone (entryOf entryId).CategorizationWriteBackFailure "aufgelöst"
            Expect.equal (eventTypesOf entryId |> List.last) "EntryCategorizationWrittenBackToYnab" "Erfolg im Log"

        testCase "zurück auf eine Kategorie: category_id und der Payee aus BB, kein Übertrags-Payee" <| fun () ->
            let accounts = freshAccounts ()
            let entryId, ynabId = seedLinked accounts
            setTransfer (TransferProbe(accounts.List, Ok ())) entryId accounts.Target |> unwrap |> ignore
            let probe = TransferProbe(accounts.List, Ok ())
            let outcome = setCategory probe entryId (Some catB) |> unwrap
            Expect.equal outcome.Change CategoryChanged "geändert"
            Expect.equal outcome.PreviousTransferAccountId (Some accounts.Target) "vorher Übertrag, für Rückgängig"
            Expect.equal probe.Calls [ ynabId, BackToCategory (Some catB.CategoryId, Some "Rewe") ] "Kategorie und Payee"
            Expect.equal (eventTypesOf entryId |> List.last) "EntryTransferTargetClearedInBb" "Ereignis"
            let entry = entryOf entryId
            Expect.equal entry.Category (Some catB) "Kategorie"
            Expect.equal entry.Payee (Some "Rewe") "Payee aus BB"
            Expect.isNone entry.TransferAccountId "kein Übertrag mehr"

        testCase "ein Übertrag aus YNAB: zurück auf eine Kategorie wird abgelehnt, ein neues Ziel geht" <| fun () ->
            let accounts = freshAccounts ()
            let entryId = newEntryId ()
            let ynabId = $"ynab-qvjvs-{Guid.NewGuid()}"
            let payload =
                { observedPayload accounts ynabId with
                    Category = None
                    Payee = Some "Transfer : Bar"
                    TransferAccountId = Some accounts.Second }
            appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab payload) ] |> Async.RunSynchronously
            let probe = TransferProbe(accounts.List, Ok ())
            Expect.equal (setCategory probe entryId (Some catB)) (Error transferUnknownPayeeReason) "abgelehnt"
            Expect.isEmpty probe.Calls "kein PUT"
            let outcome = setTransfer probe entryId accounts.Target |> unwrap
            Expect.equal outcome.PreviousTransferAccountId (Some accounts.Second) "vorher Bar"
            Expect.equal probe.Calls [ ynabId, MakeTransfer "payee-tagesgeld" ] "neues Ziel"
            Expect.isNone (entryOf entryId).PayeeBeforeTransfer "kein Payee davor"
    ]

// ============================================
// Spiegel nach dem Push
// ============================================

[<Tests>]
let transferMirrorTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            seedCategories ()
            body ())

    testSequenced
    <| testList "Übertrag in BB: Spiegel (ledger-qvjvs)" [

        testCase "nach dem Push verknüpft der Spiegel die Gegenbuchung und legt keine fremde zweite Buchung an" <| fun () ->
            let own, target = YnabAccountId (Guid.NewGuid()), YnabAccountId (Guid.NewGuid())
            let budgetId = YnabBudgetId $"budget-qvjvs-{Guid.NewGuid()}"
            let ynabId = $"ynab-qvjvs-{Guid.NewGuid()}"
            let counterpartId = $"ynab-qvjvs-gegen-{Guid.NewGuid()}"
            let observation : YnabObservation = {
                YnabTransactionId = ynabId; AccountId = own; ImportId = None
                Date = DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc); Amount = eur -500m; Payee = Some "Rewe"
                Memo = None; Category = Some catA; Splits = []; SplitTransfers = []; Cleared = Cleared
                TransferAccountId = None; TransferYnabTransactionId = None; Deleted = false; Approved = true
                FlagColor = None; FlagName = None }
            let fetchers (knowledge: int64) (observations: YnabObservation list) : MirrorFetchers = {
                emptyMirrorFetchers with
                    FetchAccounts =
                        fun _ ->
                            async.Return (
                                Ok {
                                    Accounts = [
                                        { AccountId = own; Name = "Girokonto"; Closed = false; Deleted = false }
                                        { AccountId = target; Name = "Tagesgeld ING"; Closed = false; Deleted = false }
                                    ]
                                    ServerKnowledge = knowledge })
                    FetchTransactions = fun _ -> async.Return (Ok { Observations = observations; ServerKnowledge = knowledge })
            }
            runMirror at (fetchers 1L [ observation ]) budgetId |> Async.RunSynchronously |> unwrap |> ignore
            let entryId = findEntryByLink YnabSystem ynabId |> Async.RunSynchronously |> Option.get

            let transferAccounts : TransferAccount list = [
                { Id = own; Name = "Girokonto"; OnBudget = true; Closed = false; TransferPayeeId = Some "payee-own" }
                { Id = target; Name = "Tagesgeld ING"; OnBudget = true; Closed = false; TransferPayeeId = Some "payee-tagesgeld" }
            ]
            setTransfer (TransferProbe(transferAccounts, Ok ())) entryId target |> unwrap |> ignore
            let eventsAfterCommand = eventTypesOf entryId

            // So liefert YNAB beide Seiten nach dem PUT.
            let afterPush =
                { observation with
                    Payee = Some "Transfer : Tagesgeld ING"
                    Category = None
                    TransferAccountId = Some target
                    TransferYnabTransactionId = Some counterpartId }
            let counterpart =
                { observation with
                    YnabTransactionId = counterpartId
                    AccountId = target
                    Amount = eur 500m
                    Payee = Some "Transfer : Girokonto"
                    Category = None
                    TransferAccountId = Some own
                    TransferYnabTransactionId = Some ynabId }
            let summary = runMirror at (fetchers 2L [ afterPush; counterpart ]) budgetId |> Async.RunSynchronously |> unwrap
            Expect.equal summary.NewEntries 1 "nur die Gegenbuchung ist neu"
            Expect.equal summary.ChangedEntries 0 "die eigene Buchung zeigt keinen geänderten Stand"

            let counterpartEntryId = findEntryByLink YnabSystem counterpartId |> Async.RunSynchronously |> Option.get
            Expect.notEqual counterpartEntryId entryId "eigene Buchung für die Gegenseite"
            Expect.equal (entryOf entryId).TransferEntryId (Some counterpartEntryId) "unsere Buchung kennt die Gegenbuchung"
            Expect.equal (entryOf counterpartEntryId).TransferEntryId (Some entryId) "die Gegenbuchung kennt unsere"
            let newOnOurs = eventTypesOf entryId |> List.skip eventsAfterCommand.Length
            Expect.equal newOnOurs [ "EntryTransferCounterpartObservedInYnab" ] "nur die Verknüpfung, keine Beobachtung einer Änderung"

            // Ein zweiter Lauf mit denselben Daten legt nichts doppelt an.
            let again = runMirror at (fetchers 3L [ afterPush; counterpart ]) budgetId |> Async.RunSynchronously |> unwrap
            Expect.equal again.NewEntries 0 "keine zweite Buchung"
    ]

// ============================================
// PUT an YNAB (Fake-Handler) und Validierung
// ============================================

type private FakeHandler(status: HttpStatusCode, responseBody: string) =
    inherit HttpMessageHandler()
    member val Body : string = "" with get, set
    member val Url : string = "" with get, set
    override this.SendAsync(request: HttpRequestMessage, _ct: Threading.CancellationToken) =
        task {
            this.Url <- request.RequestUri.ToString()
            let! body = request.Content.ReadAsStringAsync()
            this.Body <- body
            return new HttpResponseMessage(status, Content = new StringContent(responseBody))
        }

let private transactionKeys (body: string) : string list =
    match
        Thoth.Json.Net.Decode.fromString
            (Thoth.Json.Net.Decode.field "transaction" (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value))
            body
    with
    | Ok pairs -> pairs |> List.map fst
    | Error e -> failtest e

[<Tests>]
let transferYnabClientTests =
    testList "YnabClient Übertrag (ledger-qvjvs, Fake-Handler)" [

        testCase "zum Übertrag: nur payee_id, keine category_id" <| fun () ->
            let body = buildTransferBody (MakeTransfer "payee-tagesgeld")
            Expect.equal (transactionKeys body) [ "payee_id" ] "nur payee_id"

        testCase "zurück: category_id, payee_id als null und payee_name" <| fun () ->
            let body = buildTransferBody (BackToCategory (Some catB.CategoryId, Some "Rewe"))
            Expect.equal (transactionKeys body) [ "category_id"; "payee_id"; "payee_name" ] "drei Felder"
            let payeeId =
                Thoth.Json.Net.Decode.fromString
                    (Thoth.Json.Net.Decode.at [ "transaction"; "payee_id" ] (Thoth.Json.Net.Decode.option Thoth.Json.Net.Decode.string))
                    body
            Expect.equal payeeId (Ok None) "kein Übertrags-Payee"

        testCase "200 mit transfer_account_id gilt, 200 ohne gilt als Fehlschlag" <| fun () ->
            let send (response: string) =
                let handler = new FakeHandler(HttpStatusCode.OK, response)
                use client = new HttpClient(handler)
                handler, sendTransfer client (YnabBudgetId "budget-1") "tx-1" (MakeTransfer "p") |> Async.RunSynchronously
            let handler, ok = send """{"data":{"transaction":{"id":"tx-1","transfer_account_id":"a"}}}"""
            Expect.equal ok (Ok ()) "angelegt"
            Expect.stringContains handler.Url "/budgets/budget-1/transactions/tx-1" "PUT auf die Buchung"
            let _, ignored = send """{"data":{"transaction":{"id":"tx-1","transfer_account_id":null}}}"""
            Expect.isError ignored "YNAB hat den Übertrag nicht angelegt"

        testCase "die Konten tragen ihre transfer_payee_id" <| fun () ->
            let json =
                """{"data":{"accounts":[{"id":"0195c000-0000-7000-8000-000000000001","name":"Tagesgeld","on_budget":true,"closed":false,"transfer_payee_id":"p-1"}]}}"""
            match parseTransferAccountsResponse json with
            | Ok [ account ] -> Expect.equal account.TransferPayeeId (Some "p-1") "Payee-Id"
            | other -> failtest $"{other}"
    ]

[<Tests>]
let transferValidationTests =
    testList "Validierung setEntryTransfer (ledger-qvjvs)" [
        testCase "leere Buchung und leeres Ziel werden beide gemeldet" <| fun () ->
            Expect.equal
                (Server.Validation.validateTransferRequest Guid.Empty (YnabAccountId Guid.Empty))
                (Error [ "Die Buchung fehlt."; "Das Zielkonto fehlt." ])
                "beide Fehler"
            Expect.equal (Server.Validation.validateTransferRequest (Guid.NewGuid()) (YnabAccountId (Guid.NewGuid()))) (Ok ()) "gültig"
    ]
