module LedgerReviewMarkTests

// Prüfvermerk setzen und nach YNAB zurückschreiben (ledger-r777c, ADR 0050).
//
// Drei Gruppen:
//  * reine Domäne: Übergänge, `apply`, Replay, Codec, `observe`, Lesemodell;
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall, ADR 0030),
//    mit einem injizierten Push, der nie YNAB erreicht;
//  * der PUT an YNAB gegen einen Fake-`HttpMessageHandler` (kein echter Aufruf).

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

let private accountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000a1")
let private at = DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc)

let private observedPayload (ynabTxId: string) (approved: bool) (cleared: ClearedState) : EntryObservedInYnabPayload = {
    AccountId = accountId
    YnabTransactionId = ynabTxId
    ImportId = None
    Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -12.00m
    Payee = Some "Rewe"
    Memo = None
    Category = None
    Splits = []
    Cleared = cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = approved
    FlagColor = None
    FlagName = None
}

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent = {
    EntryId = entryId
    OccurredAt = at
    Actor = actor
    Body = body
}

/// Eine frische, mit YNAB verknüpfte Buchung (EntryObservedInYnab trägt die Verknüpfung).
let private seedLinked (approved: bool) (cleared: ClearedState) : EntryId * string =
    let entryId = newEntryId ()
    let ynabId = $"ynab-pv-{Guid.NewGuid()}"
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload ynabId approved cleared)) ]
    |> Async.RunSynchronously
    entryId, ynabId

/// Eine Buchung ohne YNAB-Verknüpfung (Quick Add vor dem Push).
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
    member val Calls : (string * bool * ReviewMarkCleared option) list = [] with get, set
    member this.Push : ReviewMarkPush =
        fun ynabId approved cleared ->
            async {
                this.Calls <- this.Calls @ [ ynabId, approved, cleared ]
                return result
            }

let private okProbe () = PushProbe(Ok ())

let private unwrap (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"Befehl sollte gelingen, war: {e}"

// ============================================
// Reine Domäne
// ============================================

let private entryWith (approved: bool) (cleared: ClearedState) : Entry =
    apply None (eventOn (newEntryId ()) YnabMirror (EntryObservedInYnab (observedPayload "ynab-x" approved cleared)))

[<Tests>]
let reviewMarkDomainTests =
    testList "Ledger Prüfvermerk (Domäne)" [

        testCase "Reviewed: approved wird true, offen wird bestätigt" <| fun () ->
            Expect.equal (reviewMarkTarget Reviewed (entryWith false Uncleared)) (true, Cleared) "approved und cleared"

        testCase "Reviewed auf Cleared lässt cleared stehen" <| fun () ->
            Expect.equal (reviewMarkTarget Reviewed (entryWith false Cleared)) (true, Cleared) "nur approved"

        testCase "Reviewed auf Reconciled setzt nur approved" <| fun () ->
            Expect.equal (reviewMarkTarget Reviewed (entryWith false Reconciled)) (true, Reconciled) "Reconciled bleibt"

        testCase "Unreviewed auf Cleared setzt Uncleared, auf Reconciled nur approved" <| fun () ->
            Expect.equal (reviewMarkTarget Unreviewed (entryWith true Cleared)) (false, Uncleared) "Cleared wird offen"
            Expect.equal (reviewMarkTarget Unreviewed (entryWith true Reconciled)) (false, Reconciled) "Reconciled bleibt"

        testCase "Restore stellt den Vorzustand her, außer Reconciled" <| fun () ->
            Expect.equal
                (reviewMarkRestoreTarget (true, Uncleared) (entryWith true Cleared))
                (true, Uncleared)
                "approved true, Uncleared exakt wieder"
            Expect.equal
                (reviewMarkRestoreTarget (false, Cleared) (entryWith true Reconciled))
                (false, Reconciled)
                "Ist Reconciled: cleared bleibt"
            Expect.equal
                (reviewMarkRestoreTarget (false, Reconciled) (entryWith true Cleared))
                (false, Cleared)
                "Vorzustand Reconciled: nie nach Reconciled schreiben"

        testCase "apply: EntryReviewMarkChangedInBb setzt beide Felder und löscht einen offenen Fehlschlag" <| fun () ->
            let e0 = entryWith false Uncleared
            let failed = apply (Some e0) (eventOn e0.Id UserInBb (EntryReviewMarkWriteBackFailed { Reason = "Timeout" }))
            Expect.equal failed.ReviewMarkWriteBackFailure (Some "Timeout") "Fehlschlag offen"
            let changed =
                apply
                    (Some failed)
                    (eventOn
                        e0.Id
                        UserInBb
                        (EntryReviewMarkChangedInBb {
                            OldApproved = false
                            NewApproved = true
                            OldCleared = Uncleared
                            NewCleared = Cleared
                        }))
            Expect.equal (changed.Approved, changed.Cleared) (true, Cleared) "neuer Stand"
            Expect.isNone changed.ReviewMarkWriteBackFailure "neue Geste setzt den Zustand zurück"

        testCase "apply: WrittenBackToYnab beendet den Fehlschlag" <| fun () ->
            let e0 = entryWith true Cleared
            let failed = apply (Some e0) (eventOn e0.Id UserInBb (EntryReviewMarkWriteBackFailed { Reason = "x" }))
            let done' = apply (Some failed) (eventOn e0.Id UserInBb EntryReviewMarkWrittenBackToYnab)
            Expect.isNone done'.ReviewMarkWriteBackFailure "beendet"

        testCase "Replay ergibt denselben Zustand wie apply nacheinander" <| fun () ->
            let entryId = newEntryId ()
            let events = [
                eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload "ynab-r" false Uncleared))
                eventOn entryId UserInBb (EntryReviewMarkChangedInBb {
                    OldApproved = false; NewApproved = true; OldCleared = Uncleared; NewCleared = Cleared })
                eventOn entryId UserInBb (EntryReviewMarkWriteBackFailed { Reason = "Timeout" })
                eventOn entryId UserInBb EntryReviewMarkWrittenBackToYnab
                eventOn entryId UserInBb (EntryReviewMarkWriteBackFailed { Reason = "429" })
            ]
            let folded = events |> List.fold (fun s ev -> Some (apply s ev)) None
            Expect.equal (replay events) folded "Replay gleich Faltung"
            Expect.equal (replay events |> Option.get).ReviewMarkWriteBackFailure (Some "429") "letzter Fehlschlag gilt"

        testCase "Codec-Rundreise je Ereignis, Schema-Version 1" <| fun () ->
            let bodies = [
                EntryReviewMarkChangedInBb {
                    OldApproved = false; NewApproved = true; OldCleared = Uncleared; NewCleared = Reconciled }
                EntryReviewMarkWriteBackFailed { Reason = "Grund mit Umlaut: größer" }
                EntryReviewMarkWrittenBackToYnab
            ]
            for body in bodies do
                Expect.equal (schemaVersion body) 1 $"Version von {eventTypeName body}"
                let decoded = decodePayload (eventTypeName body) 1 (encodePayload body)
                Expect.equal decoded (Ok body) $"Rundreise {eventTypeName body}"

        testCase "Ereignis-Namen sind die in ADR 0050 vereinbarten" <| fun () ->
            Expect.equal
                (eventTypeName (EntryReviewMarkChangedInBb {
                    OldApproved = false; NewApproved = true; OldCleared = Uncleared; NewCleared = Cleared }))
                "EntryReviewMarkChangedInBb"
                "Changed"
            Expect.equal (eventTypeName (EntryReviewMarkWriteBackFailed { Reason = "" })) "EntryReviewMarkWriteBackFailed" "Failed"
            Expect.equal (eventTypeName EntryReviewMarkWrittenBackToYnab) "EntryReviewMarkWrittenBackToYnab" "WrittenBack"
    ]

// --- observe (Spiegel-Lauf) ---

let private observationOf (entry: Entry) (approved: bool) (cleared: ClearedState) : YnabObservation = {
    YnabTransactionId = "ynab-x"
    AccountId = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000b1")
    ImportId = None
    Date = entry.Date
    Amount = entry.Amount
    Payee = entry.Payee
    Memo = entry.Memo
    Category = entry.Category
    Splits = entry.Splits
    SplitTransfers = []
    Cleared = cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Deleted = false
    Approved = approved
    FlagColor = None
    FlagName = None
}

let private contextOf (entry: Entry) : ObservationContext = {
    EntryId = entry.Id
    AccountId = entry.AccountId
    ObservedAt = at
}

let private withOpenFailure (entry: Entry) : Entry =
    apply (Some entry) (eventOn entry.Id UserInBb (EntryReviewMarkWriteBackFailed { Reason = "Timeout" }))

[<Tests>]
let reviewMarkObserveTests =
    testList "Ledger Prüfvermerk (observe)" [

        testCase "offener Fehlschlag, YNAB zeigt den BB-Stand: WrittenBackToYnab mit Actor YnabMirror" <| fun () ->
            let entry = entryWith true Cleared |> withOpenFailure
            let events = observe (contextOf entry) (Some entry) (observationOf entry true Cleared)
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryReviewMarkWrittenBackToYnab" ] "ein Ereignis"
            Expect.equal (events |> List.map (fun e -> e.Actor)) [ YnabMirror ] "Actor YnabMirror"

        testCase "offener Fehlschlag, YNAB zeigt anderen Stand: Beobachtungs-Ereignis, Fehlschlag aufgelöst" <| fun () ->
            let entry = entryWith true Cleared |> withOpenFailure
            let events = observe (contextOf entry) (Some entry) (observationOf entry false Cleared)
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryApprovalChangeObservedInYnab" ] "nur Beobachtung"
            let after = events |> List.fold (fun s ev -> Some (apply s ev)) (Some entry) |> Option.get
            Expect.isNone after.ReviewMarkWriteBackFailure "Fehlschlag aufgelöst"
            Expect.isFalse after.Approved "YNAB-Stand gilt"

        testCase "anderer Cleared-Stand löst den Fehlschlag ebenfalls auf" <| fun () ->
            let entry = entryWith true Cleared |> withOpenFailure
            let events = observe (contextOf entry) (Some entry) (observationOf entry true Uncleared)
            let after = events |> List.fold (fun s ev -> Some (apply s ev)) (Some entry) |> Option.get
            Expect.isNone after.ReviewMarkWriteBackFailure "Fehlschlag aufgelöst"

        testCase "ohne offenen Fehlschlag und ohne Unterschied entsteht kein Ereignis (ADR 0015)" <| fun () ->
            let entry = entryWith true Cleared
            Expect.isEmpty (observe (contextOf entry) (Some entry) (observationOf entry true Cleared)) "kein Ereignis"
    ]

// --- Lesemodell ---

[<Tests>]
let reviewMarkReadModelTests =
    testList "Ledger Prüfvermerk (Lesemodell)" [

        testCase "LedgerEntryView trägt den Grund bei offenem Fehlschlag, sonst None" <| fun () ->
            let entry = entryWith true Cleared
            let viewOf e = Ledger.QueryHandlers.ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab e
            Expect.isNone (viewOf entry).ReviewMarkWriteBackFailure "nichts offen"
            Expect.equal (viewOf (withOpenFailure entry)).ReviewMarkWriteBackFailure (Some "Timeout") "Grund durchgereicht"

        testCase "der Fehlschlag ändert NeedsReview nicht (ADR 0050, Punkt 11)" <| fun () ->
            let entry = entryWith true Cleared
            let viewOf e = Ledger.QueryHandlers.ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab e
            Expect.equal (viewOf (withOpenFailure entry)).NeedsReview (viewOf entry).NeedsReview "gleiche Zählung"

        testCase "Historien-Sätze für die drei Ereignisse" <| fun () ->
            let describe = Ledger.QueryHandlers.ReadModel.describe
            Expect.equal
                (describe (EntryReviewMarkChangedInBb {
                    OldApproved = false; NewApproved = true; OldCleared = Uncleared; NewCleared = Cleared }))
                "In BB als geprüft markiert"
                "Setzen"
            Expect.equal
                (describe (EntryReviewMarkChangedInBb {
                    OldApproved = true; NewApproved = false; OldCleared = Cleared; NewCleared = Uncleared }))
                "In BB: Prüfvermerk entfernt"
                "Entfernen"
            Expect.equal (describe (EntryReviewMarkWriteBackFailed { Reason = "Timeout" })) "Prüfvermerk nicht in YNAB: Timeout" "Fehlschlag"
            Expect.equal (describe EntryReviewMarkWrittenBackToYnab) "Prüfvermerk in YNAB angekommen" "Erfolg"
    ]

// ============================================
// Befehle (In-Memory-SQLite)
// ============================================

[<Tests>]
let reviewMarkCommandTests =
    // Schema erst beim Ausführen eines Testfalls anlegen, nie beim Modul-Laden (ADR 0030).
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Ledger Prüfvermerk (Befehle)" [

        testCase "Reviewed schreibt genau ein Changed-Ereignis (UserInBb) und setzt approved und Cleared" <| fun () ->
            let entryId, ynabId = seedLinked false Uncleared
            let probe = okProbe ()
            let outcome = setReviewMark at probe.Push entryId Reviewed |> Async.RunSynchronously |> unwrap

            Expect.equal outcome.Change ReviewMarkChanged "geändert"
            Expect.equal outcome.WriteBack ReviewMarkSent "gesendet"
            Expect.equal
                outcome.Previous
                { IsApproved = false; ClearedState = ReviewUncleared }
                "Vorzustand für Rückgängig"
            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
            Expect.equal
                (recorded |> List.map (fun r -> r.EventType))
                [ "EntryObservedInYnab"; "EntryReviewMarkChangedInBb" ]
                "genau ein neues Ereignis, kein zweites bei Erfolg"
            let changed = recorded |> List.last
            Expect.equal changed.Event.Actor UserInBb "Actor"
            Expect.equal
                changed.Event.Body
                (EntryReviewMarkChangedInBb {
                    OldApproved = false; NewApproved = true; OldCleared = Uncleared; NewCleared = Cleared })
                "Alt und Neu"
            let e = entryOf entryId
            Expect.equal (e.Approved, e.Cleared) (true, Cleared) "Zustand"
            Expect.isFalse (needsReview e) "nicht mehr zu prüfen"
            Expect.equal probe.Calls [ ynabId, true, Some SendCleared ] "Push mit approved und cleared"

        testCase "Reviewed auf Reconciled: nur approved, kein cleared im Push" <| fun () ->
            let entryId, ynabId = seedLinked false Reconciled
            let probe = okProbe ()
            setReviewMark at probe.Push entryId Reviewed |> Async.RunSynchronously |> unwrap |> ignore
            let e = entryOf entryId
            Expect.equal (e.Approved, e.Cleared) (true, Reconciled) "Reconciled bleibt"
            Expect.equal probe.Calls [ ynabId, true, None ] "kein cleared im Push"
            let changed =
                loadEventsForEntry entryId |> Async.RunSynchronously |> List.last
            match changed.Event.Body with
            | EntryReviewMarkChangedInBb p ->
                Expect.equal (p.OldCleared, p.NewCleared) (Reconciled, Reconciled) "Cleared im Ereignis unverändert"
            | other -> failtest $"unerwartetes Ereignis {other}"

        testCase "Unreviewed auf Reconciled setzt nur approved, auf Cleared setzt Uncleared" <| fun () ->
            let reconciledId, _ = seedLinked true Reconciled
            let probe1 = okProbe ()
            setReviewMark at probe1.Push reconciledId Unreviewed |> Async.RunSynchronously |> unwrap |> ignore
            let r = entryOf reconciledId
            Expect.equal (r.Approved, r.Cleared) (false, Reconciled) "Reconciled bleibt"
            Expect.equal (probe1.Calls |> List.map (fun (_, a, c) -> a, c)) [ false, None ] "kein cleared"

            let clearedId, _ = seedLinked true Cleared
            let probe2 = okProbe ()
            setReviewMark at probe2.Push clearedId Unreviewed |> Async.RunSynchronously |> unwrap |> ignore
            let c = entryOf clearedId
            Expect.equal (c.Approved, c.Cleared) (false, Uncleared) "Cleared wird Uncleared"
            Expect.equal (probe2.Calls |> List.map (fun (_, a, c) -> a, c)) [ false, Some SendUncleared ] "cleared uncleared"

        testCase "restoreReviewMark stellt den Vorzustand exakt her (Rundreise)" <| fun () ->
            let entryId, _ = seedLinked true Uncleared
            let probe = okProbe ()
            let outcome = setReviewMark at probe.Push entryId Reviewed |> Async.RunSynchronously |> unwrap
            Expect.equal
                outcome.Previous
                { IsApproved = true; ClearedState = ReviewUncleared }
                "Vorzustand approved true, Uncleared"
            Expect.equal (entryOf entryId).Cleared Cleared "nach Reviewed bestätigt"

            let restored = restoreReviewMark at probe.Push entryId outcome.Previous |> Async.RunSynchronously |> unwrap
            Expect.equal restored.Change ReviewMarkChanged "Rückgängig ändert"
            let e = entryOf entryId
            Expect.equal (e.Approved, e.Cleared) (true, Uncleared) "Ausgangszustand wieder da"

        testCase "restoreReviewMark schreibt nie von oder nach Reconciled" <| fun () ->
            let entryId, ynabId = seedLinked true Reconciled
            let probe = okProbe ()
            let previous = { IsApproved = false; ClearedState = ReviewCleared }
            restoreReviewMark at probe.Push entryId previous |> Async.RunSynchronously |> unwrap |> ignore
            let e = entryOf entryId
            Expect.equal (e.Approved, e.Cleared) (false, Reconciled) "Reconciled bleibt, approved zurück"
            Expect.equal probe.Calls [ ynabId, false, None ] "kein cleared im Push"

            let toReconciledId, _ = seedLinked true Cleared
            let probe2 = okProbe ()
            restoreReviewMark at probe2.Push toReconciledId { IsApproved = true; ClearedState = ReviewReconciled }
            |> Async.RunSynchronously
            |> unwrap
            |> fun o -> Expect.equal o.Change ReviewMarkUnchanged "Vorzustand Reconciled: nichts zu tun"
            Expect.isEmpty probe2.Calls "kein Push"
            Expect.equal (entryOf toReconciledId).Cleared Cleared "nie nach Reconciled"

        testCase "Ziel gleich Ist: kein Ereignis, kein Push, NoChange" <| fun () ->
            let entryId, _ = seedLinked true Cleared
            let probe = okProbe ()
            let outcome = setReviewMark at probe.Push entryId Reviewed |> Async.RunSynchronously |> unwrap
            Expect.equal outcome.Change ReviewMarkUnchanged "keine Änderung"
            Expect.equal outcome.WriteBack ReviewMarkNotSent "nichts gesendet"
            Expect.isEmpty probe.Calls "Push wird nicht aufgerufen"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"

        testCase "das Ereignis liegt schon im Log, wenn der Push läuft" <| fun () ->
            let entryId, _ = seedLinked false Uncleared
            let mutable typesAtPush : string list = []
            let push : ReviewMarkPush =
                fun _ _ _ ->
                    async {
                        typesAtPush <- eventTypesOf entryId
                        return Ok ()
                    }
            setReviewMark at push entryId Reviewed |> Async.RunSynchronously |> unwrap |> ignore
            Expect.equal typesAtPush [ "EntryObservedInYnab"; "EntryReviewMarkChangedInBb" ] "Ereignis vor Push"

        testCase "ein gescheiterter Push (401, 429, Netz) hinterlässt den Fehlschlag und lässt den Vermerk in BB" <| fun () ->
            let cases = [
                YnabError.Unauthorized "Invalid YNAB token", "Zugang"
                YnabError.RateLimitExceeded 60, "Limit"
                YnabError.NetworkError "Timeout", "Verbindung"
            ]
            for error, marker in cases do
                let entryId, _ = seedLinked false Uncleared
                let probe = PushProbe(Error error)
                let outcome = setReviewMark at probe.Push entryId Reviewed |> Async.RunSynchronously |> unwrap
                match outcome.WriteBack with
                | ReviewMarkSendFailed reason ->
                    Expect.stringContains reason marker $"Grund nennt {marker}"
                    let e = entryOf entryId
                    Expect.equal e.ReviewMarkWriteBackFailure (Some reason) "Entry trägt den Grund"
                    Expect.isTrue e.Approved "Vermerk bleibt in BB gesetzt"
                    Expect.isFalse (needsReview e) "NeedsReview ist false"
                | other -> failtest $"erwartet Fehlschlag, war {other}"
                Expect.equal
                    (eventTypesOf entryId)
                    [ "EntryObservedInYnab"; "EntryReviewMarkChangedInBb"; "EntryReviewMarkWriteBackFailed" ]
                    "Failed liegt im Log"

        testCase "ohne YNAB-Verknüpfung: Ereignis und Fehlschlag, kein Push" <| fun () ->
            let entryId = seedUnlinked ()
            let probe = okProbe ()
            let outcome = setReviewMark at probe.Push entryId Reviewed |> Async.RunSynchronously |> unwrap
            Expect.equal outcome.WriteBack (ReviewMarkSendFailed "noch nicht in YNAB verknüpft") "Grund"
            Expect.isEmpty probe.Calls "kein Push versucht"
            Expect.equal
                (eventTypesOf entryId)
                [ "CashExpenseAdded"; "EntryReviewMarkChangedInBb"; "EntryReviewMarkWriteBackFailed" ]
                "Ereignis und Fehlschlag"
            Expect.equal (entryOf entryId).ReviewMarkWriteBackFailure (Some "noch nicht in YNAB verknüpft") "Zustand"

        testCase "eine in YNAB gelöschte Buchung wird abgelehnt, ohne Ereignis" <| fun () ->
            let entryId, _ = seedLinked false Uncleared
            appendAndApply [ eventOn entryId YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let probe = okProbe ()
            match setReviewMark at probe.Push entryId Reviewed |> Async.RunSynchronously with
            | Error _ -> ()
            | Ok _ -> failtest "gelöschte Buchung muss abgelehnt werden"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab"; "EntryDeletionObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "retry mit Erfolg schreibt WrittenBackToYnab (UserInBb) und leert den Fehlschlag" <| fun () ->
            let entryId, ynabId = seedLinked false Uncleared
            let failing = PushProbe(Error (YnabError.NetworkError "Timeout"))
            setReviewMark at failing.Push entryId Reviewed |> Async.RunSynchronously |> unwrap |> ignore
            Expect.isSome (entryOf entryId).ReviewMarkWriteBackFailure "Fehlschlag offen"

            let probe = okProbe ()
            let result = retryReviewMarkWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap
            Expect.equal result ReviewMarkSent "gesendet"
            Expect.equal probe.Calls [ ynabId, true, Some SendCleared ] "aktueller Stand geht hinaus"
            Expect.isNone (entryOf entryId).ReviewMarkWriteBackFailure "Fehlschlag geleert"
            let last = loadEventsForEntry entryId |> Async.RunSynchronously |> List.last
            Expect.equal last.EventType "EntryReviewMarkWrittenBackToYnab" "Ereignis"
            Expect.equal last.Event.Actor UserInBb "Actor"

        testCase "retry mit erneutem Fehler ersetzt den Grund" <| fun () ->
            let entryId, _ = seedLinked false Uncleared
            let first = PushProbe(Error (YnabError.NetworkError "erster"))
            setReviewMark at first.Push entryId Reviewed |> Async.RunSynchronously |> unwrap |> ignore
            let second = PushProbe(Error (YnabError.NetworkError "zweiter"))
            match retryReviewMarkWriteBack at second.Push entryId |> Async.RunSynchronously |> unwrap with
            | ReviewMarkSendFailed reason -> Expect.stringContains reason "zweiter" "neuer Grund"
            | other -> failtest $"erwartet Fehlschlag, war {other}"
            let failure = (entryOf entryId).ReviewMarkWriteBackFailure |> Option.get
            Expect.stringContains failure "zweiter" "Entry trägt den neuen Grund"
            Expect.isFalse (failure.Contains "erster") "alter Grund ist ersetzt"

        testCase "retry ohne offenen Fehlschlag tut nichts" <| fun () ->
            let entryId, _ = seedLinked true Cleared
            let probe = okProbe ()
            let result = retryReviewMarkWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap
            Expect.equal result ReviewMarkNotSent "nichts gesendet"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"

        testCase "retry auf einer Reconciled-Buchung sendet kein cleared" <| fun () ->
            let entryId, ynabId = seedLinked false Reconciled
            let failing = PushProbe(Error (YnabError.NetworkError "x"))
            setReviewMark at failing.Push entryId Reviewed |> Async.RunSynchronously |> unwrap |> ignore
            let probe = okProbe ()
            retryReviewMarkWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap |> ignore
            Expect.equal probe.Calls [ ynabId, true, None ] "nur approved"

        testCase "Spiegel-Lauf mit offenem Fehlschlag und gleichem YNAB-Stand schreibt WrittenBackToYnab (YnabMirror)" <| fun () ->
            let entryId, _ = seedLinked false Uncleared
            let failing = PushProbe(Error (YnabError.NetworkError "x"))
            setReviewMark at failing.Push entryId Reviewed |> Async.RunSynchronously |> unwrap |> ignore
            let entry = entryOf entryId
            let events = observe (contextOf entry) (Some entry) (observationOf entry true Cleared)
            appendAndApply events |> Async.RunSynchronously
            Expect.isNone (entryOf entryId).ReviewMarkWriteBackFailure "Fehlschlag beendet"
            let last = loadEventsForEntry entryId |> Async.RunSynchronously |> List.last
            Expect.equal (last.EventType, last.Event.Actor) ("EntryReviewMarkWrittenBackToYnab", YnabMirror) "Ereignis des Spiegels"
    ]

// ============================================
// PUT an YNAB (Fake-Handler, kein echter Aufruf)
// ============================================

type private FakeHandler(status: HttpStatusCode, responseBody: string) =
    inherit HttpMessageHandler()
    member val Method : HttpMethod = null with get, set
    member val Url : string = "" with get, set
    member val Body : string = "" with get, set
    member val CallCount = 0 with get, set
    override this.SendAsync(request: HttpRequestMessage, _ct: Threading.CancellationToken) =
        task {
            this.CallCount <- this.CallCount + 1
            this.Method <- request.Method
            this.Url <- request.RequestUri.ToString()
            let! body = request.Content.ReadAsStringAsync()
            this.Body <- body
            return new HttpResponseMessage(status, Content = new StringContent(responseBody))
        }

let private sendWith (status: HttpStatusCode) (cleared: ReviewMarkCleared option) =
    let handler = new FakeHandler(status, "{}")
    use client = new HttpClient(handler)
    let result =
        sendReviewMark client (YnabBudgetId "budget-1") "tx-1" true cleared |> Async.RunSynchronously
    handler, result

[<Tests>]
let reviewMarkYnabClientTests =
    testList "YnabClient Prüfvermerk (Fake-Handler)" [

        testCase "Body trägt nur approved und gegebenenfalls cleared" <| fun () ->
            let withCleared = Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.field "transaction" (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value)) (buildReviewMarkBody true (Some SendCleared))
            match withCleared with
            | Ok pairs -> Expect.equal (pairs |> List.map fst |> List.sort) [ "approved"; "cleared" ] "Felder mit cleared"
            | Error e -> failtest e
            let withoutCleared = Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.field "transaction" (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value)) (buildReviewMarkBody false None)
            match withoutCleared with
            | Ok pairs -> Expect.equal (pairs |> List.map fst) [ "approved" ] "ohne cleared nur approved"
            | Error e -> failtest e

        testCase "cleared geht als cleared oder uncleared hinaus, nie als reconciled" <| fun () ->
            let body = buildReviewMarkBody true (Some SendCleared)
            Expect.stringContains body "\"cleared\": \"cleared\"" "cleared"
            Expect.stringContains (buildReviewMarkBody false (Some SendUncleared)) "\"cleared\": \"uncleared\"" "uncleared"
            Expect.isFalse (body.Contains "reconciled") "kein reconciled"

        testCase "Methode, Pfad und Body stimmen" <| fun () ->
            let handler, result = sendWith HttpStatusCode.OK (Some SendCleared)
            Expect.equal result (Ok ()) "200 ist Erfolg"
            Expect.equal handler.Method HttpMethod.Put "einzelne Buchung ändern ist PUT (YNAB-OpenAPI updateTransaction)"
            Expect.stringEnds handler.Url "/budgets/budget-1/transactions/tx-1" "Pfad"
            Expect.stringContains handler.Body "\"approved\": true" "approved"

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
            match sendReviewMark client (YnabBudgetId "b") "t" true None |> Async.RunSynchronously with
            | Error (YnabError.NetworkError _) -> ()
            | other -> failtest $"erwartet Netzfehler, war {other}"
    ]
