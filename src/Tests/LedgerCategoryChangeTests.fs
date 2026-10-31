module LedgerCategoryChangeTests

// Kategorie ändern und nach YNAB zurückschreiben (ledger-k2rbn, ADR 0055).
//
// Gruppen:
//  * reine Domäne: `apply`, Codec, `observe` mit offenem Fehlschlag, Lesemodell;
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall, ADR 0030),
//    mit einem injizierten Push, der nie YNAB erreicht;
//  * die Schreib-Sperre gegen den Spiegel-Lauf;
//  * der PUT an YNAB gegen einen Fake-`HttpMessageHandler` (kein echter Aufruf).

open System
open System.Net
open System.Net.Http
open System.Threading.Tasks

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

let private accountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000c1")
let private at = DateTime(2026, 10, 24, 10, 0, 0, DateTimeKind.Utc)

let private catRef (id: Guid) (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId id; CategoryName = name }

let private catA = catRef (Guid.Parse "0195c000-0000-7000-8000-00000000ca01") "Lebensmittel"
let private catB = catRef (Guid.Parse "0195c000-0000-7000-8000-00000000ca02") "Drogerie"
let private catC = catRef (Guid.Parse "0195c000-0000-7000-8000-00000000ca03") "Freizeit"

let private idOf (c: CategoryRef) = c.CategoryId

let private observedPayload (ynabTxId: string) (category: CategoryRef option) : EntryObservedInYnabPayload = {
    AccountId = accountId
    YnabTransactionId = ynabTxId
    ImportId = None
    Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -12.00m
    Payee = Some "Rewe"
    Memo = Some "Wocheneinkauf"
    Category = category
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = None
    FlagName = None
}

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent = {
    EntryId = entryId
    OccurredAt = at
    Actor = actor
    Body = body
}

let private recategorized (oldC: CategoryRef option) (newC: CategoryRef option) (payee: string option) =
    EntryRecategorized {
        OldCategory = oldC
        NewCategory = newC
        OldSplits = []
        NewSplits = []
        OldPayee = payee
        NewPayee = payee
    }

let private failedBody (reason: string) (baseline: CategoryRef option) =
    EntryCategorizationWriteBackFailed { Reason = reason; BaselineCategory = baseline; BaselineSplits = []; YnabDeletedTransactionId = None }

let private entryWith (category: CategoryRef option) : Entry =
    apply None (eventOn (newEntryId ()) YnabMirror (EntryObservedInYnab (observedPayload "ynab-x" category)))

let private applyAll (entry: Entry) (events: LedgerEvent list) : Entry =
    events |> List.fold (fun s ev -> Some (apply s ev)) (Some entry) |> Option.get

// ============================================
// Reine Domäne
// ============================================

[<Tests>]
let categoryDomainTests =
    testList "Ledger Kategorie ändern (Domäne)" [

        testCase "Recategorized mit leeren Splits setzt die Kategorie und lässt die übrigen Felder" <| fun () ->
            let e0 = entryWith (Some catA)
            let e1 = apply (Some e0) (eventOn e0.Id UserInBb (recategorized (Some catA) (Some catB) e0.Payee))
            Expect.equal e1.Category (Some catB) "neue Kategorie"
            Expect.equal { e1 with Category = e0.Category } e0 "alle anderen Felder unverändert"

        testCase "Recategorized auf keine Kategorie leert die Kategorie" <| fun () ->
            let e0 = entryWith (Some catA)
            let e1 = apply (Some e0) (eventOn e0.Id UserInBb (recategorized (Some catA) None e0.Payee))
            Expect.isNone e1.Category "ohne Kategorie"

        testCase "WriteBackFailed setzt den Zustand samt Baseline, WrittenBack löscht ihn" <| fun () ->
            let e0 = entryWith (Some catA)
            let failed = apply (Some e0) (eventOn e0.Id UserInBb (failedBody "Timeout" (Some catA)))
            Expect.equal
                failed.CategorizationWriteBackFailure
                (Some { Reason = "Timeout"; BaselineCategory = Some catA; BaselineSplits = []; YnabDeletedTransactionId = None })
                "Grund und Baseline"
            let done' = apply (Some failed) (eventOn e0.Id UserInBb EntryCategorizationWrittenBackToYnab)
            Expect.isNone done'.CategorizationWriteBackFailure "beendet"

        testCase "eine zweite Änderung bei offenem Fehlschlag behält die erste Baseline" <| fun () ->
            let e0 = entryWith (Some catA)
            let events = [
                eventOn e0.Id UserInBb (recategorized (Some catA) (Some catB) e0.Payee)
                eventOn e0.Id UserInBb (failedBody "erster" (Some catA))
                eventOn e0.Id UserInBb (recategorized (Some catB) (Some catC) e0.Payee)
                // Der zweite Push scheitert ebenfalls; der Befehl nennt hier (fälschlich) B als Baseline.
                eventOn e0.Id UserInBb (failedBody "zweiter" (Some catB))
            ]
            let after = applyAll e0 events
            let failure = after.CategorizationWriteBackFailure |> Option.get
            Expect.equal failure.BaselineCategory (Some catA) "erste Baseline bleibt"
            Expect.equal failure.Reason "zweiter" "Grund wird ersetzt"
            Expect.equal after.Category (Some catC) "BB-Stand ist die letzte Änderung"

        testCase "eine Recategorized-Änderung allein beendet den offenen Fehlschlag nicht" <| fun () ->
            let e0 = entryWith (Some catA)
            let failed = applyAll e0 [ eventOn e0.Id UserInBb (failedBody "x" (Some catA)) ]
            let after = apply (Some failed) (eventOn e0.Id UserInBb (recategorized (Some catA) (Some catB) e0.Payee))
            Expect.isSome after.CategorizationWriteBackFailure "bleibt offen"

        testCase "Replay ergibt denselben Zustand wie apply nacheinander" <| fun () ->
            let entryId = newEntryId ()
            let events = [
                eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload "ynab-r" (Some catA)))
                eventOn entryId UserInBb (recategorized (Some catA) (Some catB) (Some "Rewe"))
                eventOn entryId UserInBb (failedBody "Timeout" (Some catA))
                eventOn entryId UserInBb EntryCategorizationWrittenBackToYnab
                eventOn entryId UserInBb (failedBody "429" (Some catB))
            ]
            let folded = events |> List.fold (fun s ev -> Some (apply s ev)) None
            Expect.equal (replay events) folded "Replay gleich Faltung"
            Expect.equal
                (replay events |> Option.get).CategorizationWriteBackFailure
                (Some { Reason = "429"; BaselineCategory = Some catB; BaselineSplits = []; YnabDeletedTransactionId = None })
                "letzter Fehlschlag gilt"

        testCase "Codec-Rundreise beider neuen Ereignisse, Name und Schema-Version 1" <| fun () ->
            let bodies = [
                EntryCategorizationWriteBackFailed {
                    Reason = "Grund mit Umlaut: größer"
                    BaselineCategory = Some catA
                    BaselineSplits = []
                    YnabDeletedTransactionId = None
                }
                EntryCategorizationWriteBackFailed { Reason = "ohne Baseline"; BaselineCategory = None; BaselineSplits = []; YnabDeletedTransactionId = None }
                EntryCategorizationWrittenBackToYnab
            ]
            for body in bodies do
                Expect.equal (schemaVersion body) 1 $"Version von {eventTypeName body}"
                let decoded = decodePayload (eventTypeName body) 1 (encodePayload body)
                Expect.equal decoded (Ok body) $"Rundreise {eventTypeName body}"
            Expect.equal (eventTypeName (failedBody "" None)) "EntryCategorizationWriteBackFailed" "Name Failed"
            Expect.equal (eventTypeName EntryCategorizationWrittenBackToYnab) "EntryCategorizationWrittenBackToYnab" "Name WrittenBack"
    ]

// --- observe ---

let private observationOf (entry: Entry) (category: CategoryRef option) : YnabObservation = {
    YnabTransactionId = "ynab-x"
    AccountId = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000d1")
    ImportId = None
    Date = entry.Date
    Amount = entry.Amount
    Payee = entry.Payee
    Memo = entry.Memo
    Category = category
    Splits = []
    SplitTransfers = []
    Cleared = entry.Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Deleted = false
    Approved = entry.Approved
    FlagColor = entry.FlagColor
    FlagName = entry.FlagName
}

let private contextOf (entry: Entry) : ObservationContext = {
    EntryId = entry.Id
    AccountId = entry.AccountId
    ObservedAt = at
}

/// BB hat von A auf B geändert, der Push ist gescheitert (Baseline A).
let private entryWithOpenFailure () : Entry =
    let e0 = entryWith (Some catA)
    applyAll e0 [
        eventOn e0.Id UserInBb (recategorized (Some catA) (Some catB) e0.Payee)
        eventOn e0.Id UserInBb (failedBody "Timeout" (Some catA))
    ]

[<Tests>]
let categoryObserveTests =
    testList "Ledger Kategorie ändern (observe)" [

        testCase "offener Fehlschlag, YNAB zeigt die Baseline: kein Ereignis, Zustand bleibt" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some catA))
            Expect.isEmpty events "YNAB hat unseren Push nie gesehen"
            Expect.isSome entry.CategorizationWriteBackFailure "Zustand bleibt"

        testCase "offener Fehlschlag, YNAB zeigt den BB-Stand: WrittenBackToYnab mit Actor YnabMirror" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some catB))
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryCategorizationWrittenBackToYnab" ] "ein Ereignis"
            Expect.equal (events |> List.map (fun e -> e.Actor)) [ YnabMirror ] "Actor YnabMirror"
            Expect.isNone (applyAll entry events).CategorizationWriteBackFailure "Fehlschlag beendet"

        testCase "offener Fehlschlag, YNAB zeigt eine dritte Kategorie: Beobachtung, Fehlschlag beendet" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some catC))
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryCategorizationChangeObservedInYnab" ] "nur Beobachtung"
            let after = applyAll entry events
            Expect.isNone after.CategorizationWriteBackFailure "Fehlschlag beendet"
            Expect.equal after.Category (Some catC) "YNAB-Stand gilt"

        testCase "ohne offenen Fehlschlag übernimmt observe einen abweichenden YNAB-Stand (YNAB gewinnt)" <| fun () ->
            let e0 = entryWith (Some catA)
            let entry = apply (Some e0) (eventOn e0.Id UserInBb (recategorized (Some catA) (Some catB) e0.Payee))
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some catA))
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryCategorizationChangeObservedInYnab" ] "Beobachtung"
            Expect.equal (applyAll entry events).Category (Some catA) "YNAB gewinnt"

        testCase "ohne offenen Fehlschlag und ohne Unterschied entsteht kein Ereignis" <| fun () ->
            let entry = entryWith (Some catA)
            Expect.isEmpty (observe (contextOf entry) (Some entry) (observationOf entry (Some catA))) "kein Ereignis"

        testCase "eine Umbenennung in YNAB zählt bei offenem Fehlschlag als BB-Stand" <| fun () ->
            let entry = entryWithOpenFailure ()
            let renamed = Some { catB with CategoryName = "Drogerie neu" }
            let events = observe (contextOf entry) (Some entry) (observationOf entry renamed)
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryCategorizationWrittenBackToYnab" ] "Identität über die Id"
    ]

// --- Lesemodell ---

[<Tests>]
let categoryReadModelTests =
    testList "Ledger Kategorie ändern (Lesemodell)" [

        testCase "LedgerEntryView trägt den Grund bei offenem Fehlschlag, sonst None" <| fun () ->
            let viewOf e = Ledger.QueryHandlers.ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab e
            Expect.isNone (viewOf (entryWith (Some catA))).CategorizationWriteBackFailure "nichts offen"
            Expect.equal
                (viewOf (entryWithOpenFailure ())).CategorizationWriteBackFailure
                (Some "Timeout")
                "Grund durchgereicht"

        testCase "Historien-Sätze für die zwei neuen Ereignisse" <| fun () ->
            let describe = Ledger.QueryHandlers.ReadModel.describe
            Expect.equal (describe (failedBody "Timeout" None)) "Kategorie nicht in YNAB: Timeout" "Fehlschlag"
            Expect.equal (describe EntryCategorizationWrittenBackToYnab) "Kategorie in YNAB angekommen" "Erfolg"
    ]

// ============================================
// Befehle (In-Memory-SQLite)
// ============================================

let private seedCategories () =
    upsertCategories [
        for c in [ catA; catB; catC ] ->
            { Id = c.CategoryId; Name = c.CategoryName; GroupName = "Alltag"; Available = None; Hidden = false }
    ]
    |> Async.RunSynchronously

/// Eine frische, mit YNAB verknüpfte Buchung.
let private seedLinked (category: CategoryRef option) : EntryId * string =
    let entryId = newEntryId ()
    let ynabId = $"ynab-cat-{Guid.NewGuid()}"
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload ynabId category)) ]
    |> Async.RunSynchronously
    entryId, ynabId

let private seedUnlinked () : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        eventOn entryId QuickAdd (
            CashExpenseAdded {
                AccountId = accountId
                Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -3.00m
                Payee = Some "Kiosk"
                Memo = None
                Category = None
                Cleared = Uncleared
                Approved = false
            }
        )
    ]
    |> Async.RunSynchronously
    entryId

let private entryOf (entryId: EntryId) : Entry =
    loadEntry entryId |> Async.RunSynchronously |> Option.get

let private eventTypesOf (entryId: EntryId) : string list =
    loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)

/// Ein Push, der aufzeichnet, was er bekommt, und ein festes Ergebnis liefert.
type private PushProbe(result: YnabResult<unit>) =
    member val Calls : (string * YnabCategoryId option) list = [] with get, set
    member this.Push : CategoryPush =
        fun ynabId category ->
            async {
                this.Calls <- this.Calls @ [ ynabId, category ]
                return result
            }

let private okProbe () = PushProbe(Ok ())

let private unwrap (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"Befehl sollte gelingen, war: {e}"

let private choice (c: CategoryRef) : CategoryChoice = { CategoryId = c.CategoryId; CategoryName = c.CategoryName }

let private setCategory (push: CategoryPush) (entryId: EntryId) (target: CategoryRef option) =
    setEntryCategory at push entryId (target |> Option.map idOf) |> Async.RunSynchronously

[<Tests>]
let categoryCommandTests =
    // Schema erst beim Ausführen eines Testfalls anlegen, nie beim Modul-Laden (ADR 0030).
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            seedCategories ()
            body ())

    testSequenced
    <| testList "Ledger Kategorie ändern (Befehle)" [

        testCase "eine Änderung schreibt genau ein Recategorized (UserInBb) vor dem Push und einen Push mit category_id" <| fun () ->
            let entryId, ynabId = seedLinked (Some catA)
            let mutable typesAtPush : string list = []
            let probe = okProbe ()
            let push : CategoryPush =
                fun id category ->
                    typesAtPush <- eventTypesOf entryId
                    probe.Push id category
            let outcome = setCategory push entryId (Some catB) |> unwrap

            Expect.equal outcome.Change CategoryChanged "geändert"
            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.equal outcome.Previous (Some (choice catA)) "Vorherige Kategorie mit Id und Namen"
            Expect.equal typesAtPush [ "EntryObservedInYnab"; "EntryRecategorized" ] "Ereignis liegt vor dem Push im Log"
            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
            Expect.equal
                (recorded |> List.map (fun r -> r.EventType))
                [ "EntryObservedInYnab"; "EntryRecategorized" ]
                "bei Erfolg kein weiteres Ereignis"
            let changed = recorded |> List.last
            Expect.equal changed.Event.Actor UserInBb "Actor"
            Expect.equal
                changed.Event.Body
                (recategorized (Some catA) (Some catB) (Some "Rewe"))
                "Alt und Neu, Payee unverändert, Splits leer"
            Expect.equal (entryOf entryId).Category (Some catB) "Zustand"
            Expect.equal probe.Calls [ ynabId, Some (idOf catB) ] "genau ein Push mit der Kategorie"

        testCase "Rücknahme mit dem alten Wert schreibt ein zweites Recategorized und einen zweiten Push" <| fun () ->
            let entryId, ynabId = seedLinked (Some catA)
            let probe = okProbe ()
            let first = setCategory probe.Push entryId (Some catB) |> unwrap
            let previous = first.Previous |> Option.map (fun p -> catRef (let (YnabCategoryId g) = p.CategoryId in g) p.CategoryName)
            let undone = setCategory probe.Push entryId previous |> unwrap

            Expect.equal undone.Change CategoryChanged "Rücknahme ändert"
            Expect.equal undone.Previous (Some (choice catB)) "Previous der Rücknahme ist die neue Kategorie"
            Expect.equal
                (eventTypesOf entryId)
                [ "EntryObservedInYnab"; "EntryRecategorized"; "EntryRecategorized" ]
                "zwei Ereignisse"
            Expect.equal (entryOf entryId).Category (Some catA) "Ausgangszustand wieder da"
            Expect.equal probe.Calls [ ynabId, Some (idOf catB); ynabId, Some (idOf catA) ] "zwei Pushes"

        testCase "Rücknahme von und nach 'ohne Kategorie'" <| fun () ->
            let entryId, ynabId = seedLinked None
            let probe = okProbe ()
            let first = setCategory probe.Push entryId (Some catA) |> unwrap
            Expect.isNone first.Previous "vorher ohne Kategorie"
            let undone = setCategory probe.Push entryId None |> unwrap
            Expect.equal undone.Change CategoryChanged "zurück auf ohne Kategorie"
            Expect.isNone (entryOf entryId).Category "ohne Kategorie"
            Expect.equal probe.Calls [ ynabId, Some (idOf catA); ynabId, None ] "zweiter Push mit null"
            Expect.equal (eventTypesOf entryId |> List.length) 3 "Observed plus zwei Recategorized"

        testCase "Ziel gleich Ist erzeugt weder Ereignis noch Push" <| fun () ->
            let entryId, _ = seedLinked (Some catA)
            let probe = okProbe ()
            let outcome = setCategory probe.Push entryId (Some catA) |> unwrap
            Expect.equal outcome.Change CategoryUnchanged "keine Änderung"
            Expect.equal outcome.WriteBack CategoryNotSent "nichts gesendet"
            Expect.isEmpty probe.Calls "Push wird nicht aufgerufen"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"

        testCase "Ziel 'ohne Kategorie' bei einer Buchung ohne Kategorie ist ebenfalls unverändert" <| fun () ->
            let entryId, _ = seedLinked None
            let probe = okProbe ()
            let outcome = setCategory probe.Push entryId None |> unwrap
            Expect.equal outcome.Change CategoryUnchanged "keine Änderung"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "Wechsel auf 'ohne Kategorie' sendet category_id = null" <| fun () ->
            let entryId, ynabId = seedLinked (Some catA)
            let probe = okProbe ()
            let outcome = setCategory probe.Push entryId None |> unwrap
            Expect.equal outcome.Change CategoryChanged "geändert"
            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.equal probe.Calls [ ynabId, None ] "Push mit None"
            Expect.isNone (entryOf entryId).Category "ohne Kategorie in BB"

        testCase "ein gescheiterter Push (Netz, 400, 429) hinterlässt den Fehlschlag und lässt die Kategorie in BB" <| fun () ->
            let cases = [
                YnabError.NetworkError "Timeout", "Netzwerkfehler"
                YnabError.InvalidResponse "Bad request: x", "abgelehnt"
                YnabError.RateLimitExceeded 60, "Limit"
            ]
            for error, marker in cases do
                let entryId, _ = seedLinked (Some catA)
                let probe = PushProbe(Error error)
                let outcome = setCategory probe.Push entryId (Some catB) |> unwrap
                match outcome.WriteBack with
                | CategorySendFailed reason ->
                    Expect.stringContains reason marker $"Grund nennt {marker}"
                    let e = entryOf entryId
                    Expect.equal
                        e.CategorizationWriteBackFailure
                        (Some { Reason = reason; BaselineCategory = Some catA; BaselineSplits = []; YnabDeletedTransactionId = None })
                        "Entry trägt Grund und Baseline"
                    Expect.equal e.Category (Some catB) "Kategorie bleibt in BB gesetzt"
                | other -> failtest $"erwartet Fehlschlag, war {other}"
                Expect.equal
                    (eventTypesOf entryId)
                    [ "EntryObservedInYnab"; "EntryRecategorized"; "EntryCategorizationWriteBackFailed" ]
                    "Failed liegt im Log"

        testCase "ohne YNAB-Verknüpfung: Ereignis und Fehlschlag, kein Push" <| fun () ->
            let entryId = seedUnlinked ()
            let probe = okProbe ()
            let outcome = setCategory probe.Push entryId (Some catA) |> unwrap
            Expect.equal outcome.WriteBack (CategorySendFailed "noch nicht in YNAB verknüpft") "Grund"
            Expect.isEmpty probe.Calls "kein Push versucht"
            Expect.equal
                (eventTypesOf entryId)
                [ "CashExpenseAdded"; "EntryRecategorized"; "EntryCategorizationWriteBackFailed" ]
                "Ereignis und Fehlschlag"
            Expect.equal (entryOf entryId).Category (Some catA) "Kategorie in BB"

        testCase "zweite Änderung bei offenem Fehlschlag behält die erste Baseline, ein Erfolg beendet den Fehlschlag" <| fun () ->
            let entryId, _ = seedLinked (Some catA)
            let failing = PushProbe(Error (YnabError.NetworkError "x"))
            setCategory failing.Push entryId (Some catB) |> unwrap |> ignore
            let failingAgain = PushProbe(Error (YnabError.NetworkError "y"))
            setCategory failingAgain.Push entryId (Some catC) |> unwrap |> ignore
            let failure = (entryOf entryId).CategorizationWriteBackFailure |> Option.get
            Expect.equal failure.BaselineCategory (Some catA) "erste Baseline"
            Expect.stringContains failure.Reason "y" "neuer Grund"

            let ok = okProbe ()
            let outcome = setCategory ok.Push entryId (Some catB) |> unwrap
            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.isNone (entryOf entryId).CategorizationWriteBackFailure "Erfolg beendet den Fehlschlag"
            Expect.equal (eventTypesOf entryId |> List.last) "EntryCategorizationWrittenBackToYnab" "Ereignis"

        testCase "gelöschte Buchung, Übertrag ohne Kategorie, Split-Buchung und unbekannte Kategorie liefern Error ohne Ereignis" <| fun () ->
            let probe = okProbe ()
            let expectError (entryId: EntryId) (target: CategoryRef option) (what: string) =
                match setCategory probe.Push entryId target with
                | Error _ -> ()
                | Ok _ -> failtest $"{what} muss abgelehnt werden"

            let deletedId, _ = seedLinked (Some catA)
            appendAndApply [ eventOn deletedId YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            expectError deletedId (Some catB) "gelöschte Buchung"
            Expect.equal (eventTypesOf deletedId) [ "EntryObservedInYnab"; "EntryDeletionObservedInYnab" ] "kein Ereignis (gelöscht)"

            let transferId = newEntryId ()
            appendAndApply [
                eventOn transferId YnabMirror (
                    EntryObservedInYnab {
                        observedPayload "ynab-transfer" None with
                            TransferAccountId = Some (YnabAccountId (Guid.NewGuid()))
                    }
                )
            ]
            |> Async.RunSynchronously
            expectError transferId (Some catB) "Übertrag ohne Kategorie"
            Expect.equal (eventTypesOf transferId) [ "EntryObservedInYnab" ] "kein Ereignis (Übertrag)"

            let splitId = newEntryId ()
            let splits = [
                { Target = ToCategory (idOf catA, catA.CategoryName); Amount = eur -5.00m; Memo = None }
                { Target = ToCategory (idOf catB, catB.CategoryName); Amount = eur -7.00m; Memo = None }
            ]
            appendAndApply [
                eventOn splitId YnabMirror (
                    EntryObservedInYnab { observedPayload "ynab-split" None with Splits = splits }
                )
            ]
            |> Async.RunSynchronously
            expectError splitId (Some catC) "Split-Buchung"
            Expect.equal (eventTypesOf splitId) [ "EntryObservedInYnab" ] "kein Ereignis (Split)"

            let plainId, _ = seedLinked (Some catA)
            expectError plainId (Some (catRef (Guid.NewGuid()) "Unbekannt")) "unbekannte Kategorie"
            Expect.equal (eventTypesOf plainId) [ "EntryObservedInYnab" ] "kein Ereignis (unbekannte Kategorie)"

            expectError (newEntryId ()) (Some catA) "unbekannte Buchung"
            Expect.isEmpty probe.Calls "nie ein Push"

        testCase "ein Übertrag MIT Kategorie (Tracking-Konto) bleibt änderbar" <| fun () ->
            let entryId = newEntryId ()
            appendAndApply [
                eventOn entryId YnabMirror (
                    EntryObservedInYnab {
                        observedPayload "ynab-transfer-cat" (Some catA) with
                            TransferAccountId = Some (YnabAccountId (Guid.NewGuid()))
                    }
                )
            ]
            |> Async.RunSynchronously
            let probe = okProbe ()
            let outcome = setCategory probe.Push entryId (Some catB) |> unwrap
            Expect.equal outcome.Change CategoryChanged "geändert"

        testCase "retry mit Erfolg schreibt WrittenBackToYnab (UserInBb), leert den Fehlschlag und sendet den BB-Stand" <| fun () ->
            let entryId, ynabId = seedLinked (Some catA)
            let failing = PushProbe(Error (YnabError.NetworkError "Timeout"))
            setCategory failing.Push entryId (Some catB) |> unwrap |> ignore
            Expect.isSome (entryOf entryId).CategorizationWriteBackFailure "Fehlschlag offen"

            let probe = okProbe ()
            let result = retryCategorizationWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap
            Expect.equal result CategorySent "gesendet"
            Expect.equal probe.Calls [ ynabId, Some (idOf catB) ] "aktueller BB-Stand geht hinaus"
            Expect.isNone (entryOf entryId).CategorizationWriteBackFailure "Fehlschlag geleert"
            let last = loadEventsForEntry entryId |> Async.RunSynchronously |> List.last
            Expect.equal last.EventType "EntryCategorizationWrittenBackToYnab" "Ereignis"
            Expect.equal last.Event.Actor UserInBb "Actor"

        testCase "retry mit erneutem Fehler ersetzt den Grund, die Baseline bleibt" <| fun () ->
            let entryId, _ = seedLinked (Some catA)
            let first = PushProbe(Error (YnabError.NetworkError "erster"))
            setCategory first.Push entryId (Some catB) |> unwrap |> ignore
            let second = PushProbe(Error (YnabError.NetworkError "zweiter"))
            match retryCategorizationWriteBack at second.Push entryId |> Async.RunSynchronously |> unwrap with
            | CategorySendFailed reason -> Expect.stringContains reason "zweiter" "neuer Grund"
            | other -> failtest $"erwartet Fehlschlag, war {other}"
            let failure = (entryOf entryId).CategorizationWriteBackFailure |> Option.get
            Expect.stringContains failure.Reason "zweiter" "Entry trägt den neuen Grund"
            Expect.isFalse (failure.Reason.Contains "erster") "alter Grund ist ersetzt"
            Expect.equal failure.BaselineCategory (Some catA) "Baseline bleibt"

        testCase "retry ohne offenen Fehlschlag tut nichts" <| fun () ->
            let entryId, _ = seedLinked (Some catA)
            let probe = okProbe ()
            let result = retryCategorizationWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap
            Expect.equal result CategoryNotSent "nichts gesendet"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"

        testCase "retry einer Buchung ohne Kategorie sendet null" <| fun () ->
            let entryId, ynabId = seedLinked (Some catA)
            let failing = PushProbe(Error (YnabError.NetworkError "x"))
            setCategory failing.Push entryId None |> unwrap |> ignore
            let probe = okProbe ()
            retryCategorizationWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap |> ignore
            Expect.equal probe.Calls [ ynabId, None ] "None geht hinaus"

        testCase "Spiegel-Lauf-Beobachtung mit offenem Fehlschlag und gleichem YNAB-Stand beendet ihn (YnabMirror)" <| fun () ->
            let entryId, _ = seedLinked (Some catA)
            let failing = PushProbe(Error (YnabError.NetworkError "x"))
            setCategory failing.Push entryId (Some catB) |> unwrap |> ignore
            let entry = entryOf entryId
            appendAndApply (observe (contextOf entry) (Some entry) (observationOf entry (Some catB)))
            |> Async.RunSynchronously
            Expect.isNone (entryOf entryId).CategorizationWriteBackFailure "Fehlschlag beendet"
            let last = loadEventsForEntry entryId |> Async.RunSynchronously |> List.last
            Expect.equal (last.EventType, last.Event.Actor) ("EntryCategorizationWrittenBackToYnab", YnabMirror) "Ereignis des Spiegels"

        testCase "die Projektion trägt den offenen Fehlschlag samt Baseline durch die Datenbank" <| fun () ->
            let entryId, _ = seedLinked (Some catA)
            let failing = PushProbe(Error (YnabError.NetworkError "Verbindung weg"))
            setCategory failing.Push entryId (Some catB) |> unwrap |> ignore
            let fromDb = entryOf entryId
            let replayed =
                loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.Event) |> replay |> Option.get
            Expect.equal fromDb replayed "gelesener Zustand gleich Replay des Logs"
    ]

// ============================================
// Schreib-Sperre gegen den Spiegel-Lauf (ADR 0055, Punkt 6)
// ============================================

[<Tests>]
let categoryWriteGuardTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            seedCategories ()
            body ())

    testSequenced
    <| testList "Ledger Kategorie ändern (Schreib-Sperre)" [

        testCase "ein Befehl, der während eines Spiegel-Laufs startet, schreibt sein Ereignis erst nach dem Lauf" <| fun () ->
            let entryId, _ = seedLinked (Some catA)
            let fetchStarted = TaskCompletionSource<unit>()
            let release = TaskCompletionSource<unit>()
            let fetchers : MirrorFetchers = {
                emptyMirrorFetchers with
                    FetchTransactions =
                        fun _ ->
                            async {
                                fetchStarted.TrySetResult () |> ignore
                                do! Async.AwaitTask release.Task
                                return Ok { Observations = []; ServerKnowledge = 1L }
                            }
            }
            let mirrorTask =
                Async.StartAsTask(runMirrorGuarded at fetchers (YnabBudgetId "budget-write-guard"))
            Expect.isTrue (fetchStarted.Task.Wait 5000) "der Lauf hängt im Abruf"

            let probe = okProbe ()
            let commandTask =
                Async.StartAsTask(setEntryCategory at probe.Push entryId (Some (idOf catB)))
            Threading.Thread.Sleep 300
            Expect.isFalse commandTask.IsCompleted "der Befehl wartet auf den Lauf"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "noch kein Ereignis während des Laufs"
            Expect.isEmpty probe.Calls "noch kein Push"

            release.SetResult ()
            Expect.isTrue (mirrorTask.Wait 5000) "Lauf beendet"
            Expect.isTrue (commandTask.Wait 5000) "Befehl beendet"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab"; "EntryRecategorized" ] "Ereignis erst nach dem Lauf"
            Expect.equal (entryOf entryId).Category (Some catB) "Kategorie gesetzt"
            Expect.equal probe.Calls.Length 1 "ein Push"
    ]

// ============================================
// PUT an YNAB (Fake-Handler, kein echter Aufruf)
// ============================================

type private FakeHandler(status: HttpStatusCode, responseBody: string) =
    inherit HttpMessageHandler()
    member val Method : HttpMethod = null with get, set
    member val Url : string = "" with get, set
    member val Body : string = "" with get, set
    override this.SendAsync(request: HttpRequestMessage, _ct: Threading.CancellationToken) =
        task {
            this.Method <- request.Method
            this.Url <- request.RequestUri.ToString()
            let! body = request.Content.ReadAsStringAsync()
            this.Body <- body
            return new HttpResponseMessage(status, Content = new StringContent(responseBody))
        }

let private sendWith (status: HttpStatusCode) (category: YnabCategoryId option) =
    let handler = new FakeHandler(status, "{}")
    use client = new HttpClient(handler)
    let result =
        sendCategory client (YnabBudgetId "budget-1") "tx-1" category |> Async.RunSynchronously
    handler, result

[<Tests>]
let categoryYnabClientTests =
    testList "YnabClient Kategorie (Fake-Handler)" [

        testCase "Body trägt nur category_id" <| fun () ->
            let body = buildCategoryBody (Some (idOf catA))
            let keys = Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.field "transaction" (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value)) body
            match keys with
            | Ok pairs -> Expect.equal (pairs |> List.map fst) [ "category_id" ] "nur category_id"
            | Error e -> failtest e
            let id = Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.at [ "transaction"; "category_id" ] Thoth.Json.Net.Decode.string) body
            Expect.equal id (Ok ((let (YnabCategoryId g) = idOf catA in g).ToString())) "die Id als Text"

        testCase "ohne Kategorie geht category_id als null hinaus" <| fun () ->
            let body = buildCategoryBody None
            let value =
                Thoth.Json.Net.Decode.fromString
                    (Thoth.Json.Net.Decode.at [ "transaction"; "category_id" ] (Thoth.Json.Net.Decode.option Thoth.Json.Net.Decode.string))
                    body
            Expect.equal value (Ok None) "null"
            Expect.stringContains body "\"category_id\": null" "wörtlich null"

        testCase "Methode, Pfad und Body stimmen" <| fun () ->
            let handler, result = sendWith HttpStatusCode.OK (Some (idOf catB))
            Expect.equal result (Ok ()) "200 ist Erfolg"
            Expect.equal handler.Method HttpMethod.Put "einzelne Buchung ändern ist PUT"
            Expect.stringEnds handler.Url "/budgets/budget-1/transactions/tx-1" "Pfad"
            Expect.stringContains handler.Body "category_id" "Body"

        testCase "Fehlerzuordnung 400, 401, 404, 429" <| fun () ->
            let result status = sendWith status None |> snd
            match result HttpStatusCode.BadRequest with
            | Error (YnabError.InvalidResponse _) -> ()
            | other -> failtest $"400: {other}"
            match result HttpStatusCode.Unauthorized with
            | Error (YnabError.Unauthorized _) -> ()
            | other -> failtest $"401: {other}"
            match result HttpStatusCode.NotFound with
            | Error (YnabError.InvalidResponse msg) -> Expect.stringContains msg "not found" "404"
            | other -> failtest $"404: {other}"
            match result (enum<HttpStatusCode> 429) with
            | Error (YnabError.RateLimitExceeded _) -> ()
            | other -> failtest $"429: {other}"

        testCase "eine Ausnahme im Handler wird zum Netzfehler" <| fun () ->
            let handler =
                { new HttpMessageHandler() with
                    member _.SendAsync(_, _) = raise (HttpRequestException "kaputt") }
            use client = new HttpClient(handler)
            match sendCategory client (YnabBudgetId "b") "t" None |> Async.RunSynchronously with
            | Error (YnabError.NetworkError _) -> ()
            | other -> failtest $"erwartet Netzfehler, war {other}"
    ]
