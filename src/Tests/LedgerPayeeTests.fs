module LedgerPayeeTests

// Payee ändern in BB (ledger-fxt09, Muster Memo ADR 0062):
//
//  * Regeln `normalizePayee`, `payeeKey`, `payeeLockReason` und die Prüfung an der API-Grenze;
//  * Ereignisse: Codec, Namen, Schema-Version, Replay;
//  * Befehle gegen In-Memory-SQLite (Gruppe `TestGroups.InMemoryDb`, Schema-Init je Testfall) mit einem
//    injizierten Push, der nie YNAB erreicht und seine Aufrufe zählt;
//  * `observe` mit Baseline (der Spiegel);
//  * Historie;
//  * PUT an YNAB gegen einen Fake-`HttpMessageHandler`.

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
open Ledger.QueryHandlers
open Server.YnabObservations
open Server.YnabClient

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private at = DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc)

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

let private freshAccount () = LedgerAccountId (Guid.NewGuid())

let private bankPayee = "REWE SAGT DANKE 4711"

let private observedPayload (account: LedgerAccountId) (payee: string option) : EntryObservedInYnabPayload = {
    AccountId = account
    YnabTransactionId = $"ynab-fxt09-{Guid.NewGuid()}"
    ImportId = None
    Date = DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -23.40m
    Payee = payee
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

let private seedWith (payload: EntryObservedInYnabPayload) : EntryId * string =
    let entryId = newEntryId ()
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab payload) ] |> Async.RunSynchronously
    entryId, payload.YnabTransactionId

/// Eine mit YNAB verknüpfte Buchung mit gegebenem Payee.
let private seedLinked (account: LedgerAccountId) (payee: string option) : EntryId * string =
    seedWith (observedPayload account payee)

/// Eine Barbuchung aus BB ohne YNAB-Verknüpfung (Noch nicht in YNAB).
let private seedUnlinked (account: LedgerAccountId) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        eventOn entryId QuickAdd (
            CashExpenseAdded {
                AccountId = account
                Date = DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -3.00m
                Payee = Some "Kiosk fxt09"
                Memo = None
                Category = None
                Cleared = Uncleared
                Approved = false
                ImportId = None
            }
        )
    ]
    |> Async.RunSynchronously
    entryId

let private entryOf (entryId: EntryId) : Entry = loadEntry entryId |> Async.RunSynchronously |> Option.get

let private eventsOf (entryId: EntryId) = loadEventsForEntry entryId |> Async.RunSynchronously

let private eventTypesOf (entryId: EntryId) : string list = eventsOf entryId |> List.map (fun r -> r.EventType)

/// Ein Push, der jeden Aufruf aufzeichnet und ein festes Ergebnis liefert. `OnCall` läuft
/// beim Aufruf und sieht den Zustand des Logs in diesem Moment.
type private PayeeProbe(result: YnabResult<unit>) =
    member val Calls : (string * string option) list = [] with get, set
    member val OnCall : (unit -> unit) = ignore with get, set
    member this.Push : PayeePush =
        fun ynabId payee ->
            async {
                this.OnCall ()
                this.Calls <- this.Calls @ [ ynabId, payee ]
                return result
            }

let private okProbe () = PayeeProbe(Ok ())

let private unwrap (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"Befehl sollte gelingen, war: {e}"

let private setPayee (push: PayeePush) (entryId: EntryId) (text: string) =
    setEntryPayee at push entryId (Some text) |> Async.RunSynchronously

/// "Payee entfernen" (ledger-pkrwd): derselbe Befehl mit `None`.
let private removePayee (push: PayeePush) (entryId: EntryId) =
    setEntryPayee at push entryId None |> Async.RunSynchronously

let private retry (push: PayeePush) (entryId: EntryId) =
    retryPayeeWriteBack at push entryId |> Async.RunSynchronously

// ============================================
// Regeln
// ============================================

[<Tests>]
let ruleTests =
    testList "Payee: Eingabe prüfen und Sperren" [

        testCase "der Payee wird getrimmt, leer und nur Leerraum werden abgelehnt" <| fun () ->
            Expect.equal (normalizePayee "  Rewe  ") (Ok "Rewe") "getrimmt"
            Expect.equal (normalizePayee "") (Error payeeEmptyReason) "leer"
            Expect.equal (normalizePayee " \t ") (Error payeeEmptyReason) "nur Leerraum"
            Expect.equal (normalizePayee null) (Error payeeEmptyReason) "null"

        testCase "die Grenze von YNAB: genau 200 Zeichen gehen, 201 werden abgelehnt, gezählt nach dem Trimmen" <| fun () ->
            Expect.equal ynabPayeeMaxLength 200 "Grenze aus der OpenAPI"
            let atLimit = String('a', ynabPayeeMaxLength)
            Expect.equal (normalizePayee atLimit) (Ok atLimit) "genau an der Grenze"
            Expect.equal (normalizePayee (atLimit + "a")) (Error payeeTooLongReason) "eins darüber"
            Expect.equal (normalizePayee ("  " + atLimit + "  ")) (Ok atLimit) "Leerraum am Rand zählt nicht"

        testCase "payeeKey: getrimmt und ohne Groß- und Kleinschreibung, Leerraum heißt kein Payee" <| fun () ->
            Expect.equal (payeeKey (Some " Rewe ")) (payeeKey (Some "REWE")) "gleich"
            Expect.notEqual (payeeKey (Some "Rewe")) (payeeKey (Some "Rewe Markt")) "anders"
            Expect.equal (payeeKey (Some "  ")) None "Leerraum"
            Expect.equal (payeeKey None) None "kein Payee"

        testCase "gesperrt sind gelöscht, nicht verknüpft und Übertrag, je mit eigenem Satz" <| fun () ->
            Expect.equal (payeeLockReason true true false) (Some payeeDeletedReason) "gelöscht"
            Expect.equal (payeeLockReason false false false) (Some payeeNotInYnabReason) "nicht verknüpft"
            Expect.equal (payeeLockReason false true true) (Some payeeTransferReason) "Übertrag"
            Expect.equal (payeeLockReason false true false) None "änderbar"

        testCase "API-Grenze: leer, zu lang und fehlende Buchung, alle Fehler gesammelt" <| fun () ->
            let id = Guid.NewGuid()
            Expect.equal (Server.Validation.validatePayeeChange id (Some "  Rewe ")) (Ok (Some "Rewe")) "gültig, getrimmt"
            Expect.equal (Server.Validation.validatePayeeChange id (Some " ")) (Error [ payeeEmptyReason ]) "leer"
            Expect.equal
                (Server.Validation.validatePayeeChange id (Some (String('x', ynabPayeeMaxLength + 1))))
                (Error [ payeeTooLongReason ])
                "zu lang"
            Expect.equal
                (Server.Validation.validatePayeeChange Guid.Empty (Some ""))
                (Error [ Server.Validation.payeeEntryMissingText; payeeEmptyReason ])
                "beide Fehler"
            Expect.equal
                (Server.Validation.validatePayeeChange Guid.Empty (Some (String('x', ynabPayeeMaxLength + 1))))
                (Error [ Server.Validation.payeeEntryMissingText; payeeTooLongReason ])
                "Buchung fehlt und zu lang"

        // ledger-pkrwd: `None` heißt "Payee entfernen" und ist gültig; nur die Buchung kann fehlen.
        testCase "API-Grenze: ohne Payee ist ein gültiges Ziel, eine fehlende Buchung bleibt ein Fehler" <| fun () ->
            Expect.equal (Server.Validation.validatePayeeChange (Guid.NewGuid()) None) (Ok None) "entfernen gültig"
            Expect.equal
                (Server.Validation.validatePayeeChange Guid.Empty None)
                (Error [ Server.Validation.payeeEntryMissingText ])
                "Buchung fehlt"
            Expect.equal (normalizePayeeTarget (Some "  ")) (Error payeeEmptyReason) "ein leerer Text bleibt ein Fehler"
    ]

// ============================================
// Ereignisse
// ============================================

let private changedBody : LedgerEventBody = EntryPayeeChangedInBb { OldPayee = Some bankPayee; NewPayee = Some "Rewe" }

let private failedBody : LedgerEventBody =
    EntryPayeeWriteBackFailed { Reason = "Verbindung weg"; BaselinePayee = Some bankPayee }

[<Tests>]
let eventTests =
    testList "Payee: Ereignisse" [

        testCase "Rundreise des Codecs, Namen und Schema-Version für alle drei Typen" <| fun () ->
            for body, name, version in
                [ changedBody, "EntryPayeeChangedInBb", 2
                  failedBody, "EntryPayeeWriteBackFailed", 1
                  EntryPayeeWrittenBackToYnab, "EntryPayeeWrittenBackToYnab", 1 ] do
                Expect.equal (eventTypeName body) name "Typname"
                Expect.equal (schemaVersion body) version $"Version von {name}"
                Expect.equal (decodePayload name version (encodePayload body)) (Ok body) $"Rundreise {name}"

        testCase "ohne alten Payee und ohne Baseline laufen die Ereignisse heil durch den Codec" <| fun () ->
            let noOld = EntryPayeeChangedInBb { OldPayee = None; NewPayee = Some "Neu" }
            Expect.equal (decodePayload "EntryPayeeChangedInBb" 2 (encodePayload noOld)) (Ok noOld) "ohne Alt"
            let noBaseline = EntryPayeeWriteBackFailed { Reason = "x"; BaselinePayee = None }
            Expect.equal (decodePayload "EntryPayeeWriteBackFailed" 1 (encodePayload noBaseline)) (Ok noBaseline) "ohne Baseline"

        testCase "eine unbekannte Version und ein fehlender neuer Payee bleiben Fehler" <| fun () ->
            Expect.isError (decodePayload "EntryPayeeChangedInBb" 3 "{}") "Version 3 gibt es nicht"
            Expect.isError (decodePayload "EntryPayeeChangedInBb" 1 "{\"oldPayee\":\"a\"}") "newPayee ist Pflicht"
            // Review ledger-pkrwd: Version 2 las ein fehlendes Feld still als „Payee entfernt“, ein Replay hätte
            // den Payee gelöscht. Der Encoder schreibt `null` immer ausdrücklich, das Feld bleibt Pflicht.
            Expect.isError (decodePayload "EntryPayeeChangedInBb" 2 "{\"oldPayee\":\"a\"}") "newPayee ist auch in Version 2 Pflicht"

        // ledger-pkrwd: Version 2 erlaubt `newPayee: null`. Gespeicherte Ereignisse der Version 1
        // (immer mit Text) lesen sich unverändert.
        testCase "Codec: ein Ereignis im alten Format (Version 1, Text) und eins mit newPayee null (Version 2) lesen sich" <| fun () ->
            Expect.equal
                (decodePayload "EntryPayeeChangedInBb" 1 "{\"oldPayee\":\"REWE SAGT DANKE 4711\",\"newPayee\":\"Rewe\"}")
                (Ok (EntryPayeeChangedInBb { OldPayee = Some bankPayee; NewPayee = Some "Rewe" }))
                "Version 1 mit Text"
            Expect.equal
                (decodePayload "EntryPayeeChangedInBb" 2 "{\"oldPayee\":\"Rewe\",\"newPayee\":null}")
                (Ok (EntryPayeeChangedInBb { OldPayee = Some "Rewe"; NewPayee = None }))
                "Version 2 ohne Payee"
            Expect.isError
                (decodePayload "EntryPayeeChangedInBb" 1 "{\"oldPayee\":\"Rewe\",\"newPayee\":null}")
                "Version 1 kannte kein Entfernen"
            let removed = EntryPayeeChangedInBb { OldPayee = Some "Rewe"; NewPayee = None }
            Expect.equal (encodePayload removed) "{\"oldPayee\":\"Rewe\",\"newPayee\":null}" "newPayee wörtlich null"
            Expect.equal (decodePayload "EntryPayeeChangedInBb" (schemaVersion removed) (encodePayload removed)) (Ok removed) "Rundreise"

        testCase "Replay: ein altes Ereignis (Version 1) und ein Entfernen (Version 2) ergeben eine Buchung ohne Payee" <| fun () ->
            let entryId = newEntryId ()
            let decoded (version: int) (json: string) =
                match decodePayload "EntryPayeeChangedInBb" version json with
                | Ok body -> eventOn entryId UserInBb body
                | Error e -> failtest e
            let events = [
                eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (freshAccount ()) (Some bankPayee)))
                decoded 1 "{\"oldPayee\":\"REWE SAGT DANKE 4711\",\"newPayee\":\"Rewe\"}"
            ]
            Expect.equal (replay events |> Option.get).Payee (Some "Rewe") "nach Version 1"
            let removed = events @ [ decoded 2 "{\"oldPayee\":\"Rewe\",\"newPayee\":null}" ]
            Expect.equal (replay removed |> Option.get).Payee None "nach dem Entfernen"

        testCase "Replay: das Log mit allen drei Ereignissen ergibt denselben Zustand wie apply nacheinander" <| fun () ->
            let entryId = newEntryId ()
            let events = [
                eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (freshAccount ()) (Some bankPayee)))
                eventOn entryId UserInBb changedBody
                eventOn entryId UserInBb failedBody
                eventOn entryId UserInBb (EntryPayeeChangedInBb { OldPayee = Some "Rewe"; NewPayee = Some "Rewe Markt" })
                eventOn entryId UserInBb (EntryPayeeWriteBackFailed { Reason = "zwei"; BaselinePayee = Some "Rewe" })
                eventOn entryId YnabMirror EntryPayeeWrittenBackToYnab
                eventOn entryId UserInBb (EntryPayeeChangedInBb { OldPayee = Some "Rewe Markt"; NewPayee = Some "Rewe City" })
                eventOn entryId UserInBb (EntryPayeeWriteBackFailed { Reason = "drei"; BaselinePayee = Some "Rewe Markt" })
            ]
            let folded = events |> List.fold (fun s ev -> Some (apply s ev)) None
            Expect.equal (replay events) folded "Replay gleich Faltung"
            let entry = replay events |> Option.get
            Expect.equal entry.Payee (Some "Rewe City") "BB-Stand"
            Expect.equal entry.PayeeWriteBackFailure (Some { Reason = "drei"; BaselinePayee = Some "Rewe Markt" }) "Fehlschlag nach dem Ende neu"

        testCase "apply: ein zweiter Fehlschlag behält die erste Baseline" <| fun () ->
            let entryId = newEntryId ()
            let entry =
                [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (freshAccount ()) (Some bankPayee)))
                  eventOn entryId UserInBb changedBody
                  eventOn entryId UserInBb failedBody
                  eventOn entryId UserInBb (EntryPayeeWriteBackFailed { Reason = "zwei"; BaselinePayee = Some "Rewe" }) ]
                |> replay
                |> Option.get
            Expect.equal entry.PayeeWriteBackFailure (Some { Reason = "zwei"; BaselinePayee = Some bankPayee }) "erste Baseline"
    ]

// ============================================
// Befehle
// ============================================

[<Tests>]
let commandTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    // Geteilt: die In-Memory-DB. Die DB-Listen laufen nacheinander in dieser Gruppe, parallel zum Rest
    // (`TestGroups.InMemoryDb`).
    testSequencedGroup TestGroups.InMemoryDb
    <| testList "Payee: Befehle" [

        testCase "verknüpfte Buchung: zuerst EntryPayeeChangedInBb mit Alt und Neu, dann genau ein Push, der Zustand trägt den neuen Payee" <| fun () ->
            let entryId, ynabId = seedLinked (freshAccount ()) (Some bankPayee)
            let probe = okProbe ()
            let outcome = setPayee probe.Push entryId "Rewe" |> unwrap

            Expect.equal outcome.PayeeChange PayeeChanged "geändert"
            Expect.equal outcome.PayeeWriteBack PayeeSent "gesendet"
            Expect.equal outcome.PreviousPayee (Some bankPayee) "Vorzustand für Rückgängig"
            Expect.equal outcome.CurrentPayee (Some "Rewe") "neuer Payee"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab"; "EntryPayeeChangedInBb" ] "ein Ereignis"
            match (eventsOf entryId |> List.last).Event with
            | { Actor = UserInBb; Body = EntryPayeeChangedInBb p } ->
                Expect.equal (p.OldPayee, p.NewPayee) (Some bankPayee, Some "Rewe") "Alt und Neu"
            | other -> failtest $"erwartet EntryPayeeChangedInBb von UserInBb, war {other}"
            Expect.equal (entryOf entryId).Payee (Some "Rewe") "Zustand"
            Expect.equal probe.Calls [ ynabId, Some "Rewe" ] "genau ein Push"

        testCase "die Eingabe wird getrimmt gespeichert und gesendet" <| fun () ->
            let entryId, ynabId = seedLinked (freshAccount ()) (Some bankPayee)
            let probe = okProbe ()
            setPayee probe.Push entryId "  Rewe Markt  " |> unwrap |> ignore
            Expect.equal (entryOf entryId).Payee (Some "Rewe Markt") "getrimmt"
            Expect.equal probe.Calls [ ynabId, Some "Rewe Markt" ] "getrimmt gesendet"

        testCase "Reihenfolge: das Ereignis steht im Log, bevor der Push läuft; Erfolg erzeugt kein weiteres" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            let probe = okProbe ()
            let mutable seenAtPush : string list = []
            probe.OnCall <- fun () -> seenAtPush <- eventTypesOf entryId
            setPayee probe.Push entryId "Rewe" |> unwrap |> ignore
            Expect.equal seenAtPush [ "EntryObservedInYnab"; "EntryPayeeChangedInBb" ] "beim Push steht das Ereignis schon da"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab"; "EntryPayeeChangedInBb" ] "danach kein weiteres"

        testCase "derselbe Payee mit Leerzeichen am Rand oder anderer Schreibweise: PayeeUnchanged, kein Ereignis, kein Push" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some "Rewe")
            let probe = okProbe ()
            for text in [ "  Rewe "; "rewe"; " REWE" ] do
                let outcome = setPayee probe.Push entryId text |> unwrap
                Expect.equal (outcome.PayeeChange, outcome.PayeeWriteBack) (PayeeUnchanged, PayeeNotSent) $"unverändert: '{text}'"
                Expect.equal outcome.CurrentPayee (Some "Rewe") "der Stand bleibt"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "leerer und zu langer Payee werden abgelehnt, ohne Ereignis und ohne Push; an der Grenze geht es" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            let probe = okProbe ()
            Expect.equal (setPayee probe.Push entryId "   ") (Error payeeEmptyReason) "leer"
            Expect.equal (setPayee probe.Push entryId (String('x', ynabPayeeMaxLength + 1))) (Error payeeTooLongReason) "zu lang"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.isOk (setPayee probe.Push entryId (String('x', ynabPayeeMaxLength))) "an der Grenze geht es"

        testCase "unbekannte, in YNAB gelöschte, nicht verknüpfte Buchung und ein Übertrag: Fehler mit Satz, kein Ereignis, kein Push" <| fun () ->
            let account = freshAccount ()
            let deleted, _ = seedLinked account (Some bankPayee)
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let unlinked = seedUnlinked account
            let transfer, _ =
                seedWith {
                    observedPayload account (Some "Transfer : Sparkonto") with
                        TransferAccountId = Some (YnabAccountId (Guid.NewGuid()))
                }
            let probe = okProbe ()
            Expect.equal (setPayee probe.Push (newEntryId ()) "Rewe") (Error "Buchung nicht gefunden") "unbekannt"
            Expect.equal (setPayee probe.Push deleted "Rewe") (Error payeeDeletedReason) "gelöscht"
            Expect.equal (setPayee probe.Push unlinked "Rewe") (Error payeeNotInYnabReason) "nicht verknüpft"
            Expect.equal (setPayee probe.Push transfer "Rewe") (Error payeeTransferReason) "Übertrag"
            Expect.equal (eventTypesOf deleted) [ "EntryObservedInYnab"; "EntryDeletionObservedInYnab" ] "kein Ereignis (gelöscht)"
            Expect.equal (eventTypesOf unlinked) [ "CashExpenseAdded" ] "kein Ereignis (nicht verknüpft)"
            Expect.equal (eventTypesOf transfer) [ "EntryObservedInYnab" ] "kein Ereignis (Übertrag)"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "eine abgeglichene Buchung bleibt änderbar" <| fun () ->
            let entryId, ynabId = seedWith { observedPayload (freshAccount ()) (Some bankPayee) with Cleared = Reconciled }
            let probe = okProbe ()
            let outcome = setPayee probe.Push entryId "Rewe" |> unwrap
            Expect.equal outcome.PayeeWriteBack PayeeSent "gesendet"
            Expect.equal probe.Calls [ ynabId, Some "Rewe" ] "Push"

        testCase "eine Buchung ohne Payee bekommt einen, Alt ist dann kein Payee" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) None
            let outcome = setPayee (okProbe ()).Push entryId "Rewe" |> unwrap
            Expect.equal outcome.PreviousPayee None "kein Payee vorher"
            match (eventsOf entryId |> List.last).Event.Body with
            | EntryPayeeChangedInBb p -> Expect.equal p.OldPayee None "Alt"
            | other -> failtest $"{other}"

        testCase "Rückgängig ist derselbe Befehl mit dem alten Payee und stellt ihn wieder her" <| fun () ->
            let entryId, ynabId = seedLinked (freshAccount ()) (Some bankPayee)
            let probe = okProbe ()
            let outcome = setPayee probe.Push entryId "Rewe" |> unwrap
            let undone = setPayee probe.Push entryId (outcome.PreviousPayee |> Option.get) |> unwrap
            Expect.equal undone.PayeeChange PayeeChanged "zurückgesetzt"
            Expect.equal (entryOf entryId).Payee (Some bankPayee) "alter Payee"
            Expect.equal probe.Calls [ ynabId, Some "Rewe"; ynabId, Some bankPayee ] "zwei Pushes"

        // ledger-pkrwd: Payee entfernen.
        testCase "Payee entfernen: EntryPayeeChangedInBb mit Alt „Rewe“ und ohne Neu, die Buchung hat keinen Payee, der Push sendet ohne Payee" <| fun () ->
            let entryId, ynabId = seedLinked (freshAccount ()) (Some "Rewe")
            let probe = okProbe ()
            let mutable seenAtPush : string list = []
            probe.OnCall <- fun () -> seenAtPush <- eventTypesOf entryId
            let outcome = removePayee probe.Push entryId |> unwrap
            Expect.equal (outcome.PayeeChange, outcome.PayeeWriteBack) (PayeeChanged, PayeeSent) "entfernt und gesendet"
            Expect.equal (outcome.PreviousPayee, outcome.CurrentPayee) (Some "Rewe", None) "Alt und Neu in der Antwort"
            match (eventsOf entryId |> List.last).Event with
            | { Actor = UserInBb; Body = EntryPayeeChangedInBb p } ->
                Expect.equal (p.OldPayee, p.NewPayee) (Some "Rewe", None) "Alt und Neu"
            | other -> failtest $"erwartet EntryPayeeChangedInBb von UserInBb, war {other}"
            Expect.equal seenAtPush [ "EntryObservedInYnab"; "EntryPayeeChangedInBb" ] "das Ereignis steht vor dem Push"
            Expect.equal (entryOf entryId).Payee None "ohne Payee"
            Expect.equal probe.Calls [ ynabId, None ] "genau ein Push ohne Payee"
            let (EntryId guid) = entryId
            let lines = getEntryHistory guid |> Async.RunSynchronously |> List.map (fun l -> l.Description)
            Expect.equal (List.last lines) "Payee entfernt, vorher Rewe" "Historie"

        testCase "Payee entfernen, der Push scheitert: die Buchung bleibt ohne Payee, Baseline „Rewe“, Erneut senden schickt wieder ohne Payee" <| fun () ->
            let entryId, ynabId = seedLinked (freshAccount ()) (Some "Rewe")
            let outcome = removePayee (PayeeProbe(Error (YnabError.NetworkError "weg"))).Push entryId |> unwrap
            match outcome.PayeeWriteBack with
            | PayeeSendFailed _ -> ()
            | other -> failtest $"erwartet PayeeSendFailed, war {other}"
            Expect.equal
                (eventTypesOf entryId)
                [ "EntryObservedInYnab"; "EntryPayeeChangedInBb"; "EntryPayeeWriteBackFailed" ]
                "Änderung zuerst, dann der Fehlschlag"
            let entry = entryOf entryId
            Expect.equal entry.Payee None "BB bleibt ohne Payee"
            Expect.equal (entry.PayeeWriteBackFailure |> Option.map (fun f -> f.BaselinePayee)) (Some (Some "Rewe")) "Baseline"
            let view = ReadModel.toEntryView Map.empty Map.empty DecidedInBb entry
            Expect.equal view.PayeeWriteBackFailure (Some "Netzwerkfehler: weg") "die Hinweisleiste hat ihren Grund"
            let ok = okProbe ()
            Expect.equal (retry ok.Push entryId |> unwrap) PayeeSent "erneut gesendet"
            Expect.equal ok.Calls [ ynabId, None ] "wieder ohne Payee"
            Expect.isNone (entryOf entryId).PayeeWriteBackFailure "Fehlschlag beendet"

        testCase "Payee entfernen bei einer Buchung ohne Payee: PayeeUnchanged, kein Ereignis, kein Push" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) None
            let probe = okProbe ()
            let outcome = removePayee probe.Push entryId |> unwrap
            Expect.equal (outcome.PayeeChange, outcome.PayeeWriteBack) (PayeeUnchanged, PayeeNotSent) "unverändert"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "Payee entfernen bei unbekannter, gelöschter, nicht verknüpfter Buchung und Übertrag: Fehler, kein Ereignis, kein Push" <| fun () ->
            let account = freshAccount ()
            let deleted, _ = seedLinked account (Some "Rewe")
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let unlinked = seedUnlinked account
            let transfer, _ =
                seedWith {
                    observedPayload account (Some "Transfer : Sparkonto") with
                        TransferAccountId = Some (YnabAccountId (Guid.NewGuid()))
                }
            let probe = okProbe ()
            Expect.equal (removePayee probe.Push (newEntryId ())) (Error "Buchung nicht gefunden") "unbekannt"
            Expect.equal (removePayee probe.Push deleted) (Error payeeDeletedReason) "gelöscht"
            Expect.equal (removePayee probe.Push unlinked) (Error payeeNotInYnabReason) "nicht verknüpft"
            Expect.equal (removePayee probe.Push transfer) (Error payeeTransferReason) "Übertrag"
            Expect.equal (eventTypesOf deleted) [ "EntryObservedInYnab"; "EntryDeletionObservedInYnab" ] "kein Ereignis (gelöscht)"
            Expect.equal (eventTypesOf unlinked) [ "CashExpenseAdded" ] "kein Ereignis (nicht verknüpft)"
            Expect.equal (eventTypesOf transfer) [ "EntryObservedInYnab" ] "kein Ereignis (Übertrag)"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "Payee entfernen bei einer abgeglichenen Buchung geht" <| fun () ->
            let entryId, ynabId = seedWith { observedPayload (freshAccount ()) (Some "Rewe") with Cleared = Reconciled }
            let probe = okProbe ()
            Expect.equal (removePayee probe.Push entryId |> unwrap).PayeeWriteBack PayeeSent "gesendet"
            Expect.equal probe.Calls [ ynabId, None ] "Push"

        testCase "Rückgängig nach dem Entfernen setzt „Rewe“ mit demselben Befehl" <| fun () ->
            let entryId, ynabId = seedLinked (freshAccount ()) (Some "Rewe")
            let probe = okProbe ()
            let removed = removePayee probe.Push entryId |> unwrap
            setPayee probe.Push entryId (removed.PreviousPayee |> Option.get) |> unwrap |> ignore
            Expect.equal (entryOf entryId).Payee (Some "Rewe") "wieder Rewe"
            Expect.equal probe.Calls [ ynabId, None; ynabId, Some "Rewe" ] "zwei Pushes"

        testCase "Push scheitert (Netz, 4xx, 429): Fehlschlag mit Grund und Baseline, der Payee bleibt in BB" <| fun () ->
            for error in
                [ YnabError.NetworkError "Verbindung weg"
                  YnabError.InvalidResponse "Bad request"
                  YnabError.RateLimitExceeded 60 ] do
                let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
                let probe = PayeeProbe(Error error)
                let outcome = setPayee probe.Push entryId "Rewe" |> unwrap
                let reason =
                    match outcome.PayeeWriteBack with
                    | PayeeSendFailed reason -> reason
                    | other -> failtest $"erwartet PayeeSendFailed, war {other}"
                Expect.equal
                    (eventTypesOf entryId)
                    [ "EntryObservedInYnab"; "EntryPayeeChangedInBb"; "EntryPayeeWriteBackFailed" ]
                    "Änderung zuerst, dann der Fehlschlag"
                let entry = entryOf entryId
                Expect.equal entry.Payee (Some "Rewe") "BB behält den Payee"
                let failure = entry.PayeeWriteBackFailure |> Option.get
                Expect.equal failure.Reason reason "Grund im Zustand"
                Expect.equal failure.BaselinePayee (Some bankPayee) "Baseline: der Stand davor"

        testCase "die Gründe nennen YNABs Ablehnung" <| fun () ->
            Expect.equal (describePayeePushError (YnabError.InvalidResponse "Bad request")) "YNAB hat den Payee abgelehnt: Bad request" "4xx"
            Expect.stringContains (describePayeePushError (YnabError.NetworkError "weg")) "Netzwerkfehler" "Netz"

        testCase "eine weitere Änderung bei offenem Fehlschlag behält die erste Baseline" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            setPayee (PayeeProbe(Error (YnabError.NetworkError "eins"))).Push entryId "Rewe" |> unwrap |> ignore
            setPayee (PayeeProbe(Error (YnabError.NetworkError "zwei"))).Push entryId "Rewe Markt" |> unwrap |> ignore
            let failure = (entryOf entryId).PayeeWriteBackFailure |> Option.get
            Expect.equal failure.BaselinePayee (Some bankPayee) "erste Baseline bleibt"

        testCase "eine neue Änderung mit Erfolg beendet den offenen Fehlschlag" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            setPayee (PayeeProbe(Error (YnabError.NetworkError "weg"))).Push entryId "Rewe" |> unwrap |> ignore
            Expect.isSome (entryOf entryId).PayeeWriteBackFailure "offen"
            setPayee (okProbe ()).Push entryId "Rewe Markt" |> unwrap |> ignore
            Expect.isNone (entryOf entryId).PayeeWriteBackFailure "mit der neuen Änderung zurückgesetzt"
            Expect.equal (eventTypesOf entryId |> List.last) "EntryPayeeWrittenBackToYnab" "Erfolg nach Fehlschlag wird festgehalten"

        testCase "Erneut senden: der Push schickt den BB-Stand, Erfolg schreibt EntryPayeeWrittenBackToYnab" <| fun () ->
            let entryId, ynabId = seedLinked (freshAccount ()) (Some bankPayee)
            setPayee (PayeeProbe(Error (YnabError.NetworkError "weg"))).Push entryId "Rewe" |> unwrap |> ignore
            let ok = okProbe ()
            Expect.equal (retry ok.Push entryId |> unwrap) PayeeSent "gesendet"
            Expect.equal ok.Calls [ ynabId, Some "Rewe" ] "der Payee des BB-Stands"
            Expect.isNone (entryOf entryId).PayeeWriteBackFailure "Fehlschlag beendet"
            match (eventsOf entryId |> List.last).Event with
            | { Actor = UserInBb; Body = EntryPayeeWrittenBackToYnab } -> ()
            | other -> failtest $"erwartet EntryPayeeWrittenBackToYnab von UserInBb, war {other}"

        testCase "Erneut senden scheitert erneut: neuer Grund, die Baseline bleibt" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            setPayee (PayeeProbe(Error (YnabError.NetworkError "eins"))).Push entryId "Rewe" |> unwrap |> ignore
            match retry (PayeeProbe(Error (YnabError.InvalidResponse "abgelehnt"))).Push entryId |> unwrap with
            | PayeeSendFailed reason -> Expect.stringContains reason "abgelehnt" "neuer Grund"
            | other -> failtest $"{other}"
            let failure = (entryOf entryId).PayeeWriteBackFailure |> Option.get
            Expect.stringContains failure.Reason "abgelehnt" "Grund ersetzt"
            Expect.equal failure.BaselinePayee (Some bankPayee) "Baseline bleibt"

        testCase "Erneut senden ohne offenen Fehlschlag tut nichts, bei unbekannter und gelöschter Buchung ein Fehler" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            let probe = okProbe ()
            Expect.equal (retry probe.Push entryId |> unwrap) PayeeNotSent "nichts offen"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal (retry probe.Push (newEntryId ())) (Error "Buchung nicht gefunden") "unbekannt"
            appendAndApply [ eventOn entryId YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            Expect.equal (retry probe.Push entryId) (Error payeeDeletedReason) "gelöscht"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "der offene Fehlschlag überlebt das Speichern, die Sicht trägt den Grund, der Zustand gleicht dem Replay" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            setPayee (PayeeProbe(Error (YnabError.NetworkError "weg"))).Push entryId "Rewe" |> unwrap |> ignore
            let loaded = entryOf entryId
            Expect.equal loaded.PayeeWriteBackFailure.Value.BaselinePayee (Some bankPayee) "Baseline aus der Datenbank"
            let view = ReadModel.toEntryView Map.empty Map.empty DecidedInBb loaded
            Expect.equal view.PayeeWriteBackFailure (Some "Netzwerkfehler: weg") "die Sicht trägt den Grund"
            Expect.equal view.Payee (Some "Rewe") "die Sicht trägt den neuen Payee"
            let replayed = eventsOf entryId |> List.map (fun r -> r.Event) |> replay |> Option.get
            Expect.equal loaded replayed "gelesener Zustand gleich Replay des Logs"
    ]

// ============================================
// Comdirect-Import: Importschlüssel bleiben
// ============================================

let private importCandidate (txId: string) (importId: string) (reference: string) : ComdirectImportCandidate =
    let request : YnabTransactionRequest = {
        AccountId = "11111111-1111-1111-1111-111111111111"
        Date = "2026-11-01"
        Amount = -23400
        PayeeName = bankPayee
        Memo = ""
        Cleared = "uncleared"
        ImportId = importId
        CategoryId = None
        Subtransactions = None
        FlagColor = None
    }
    { Prepared = { TransactionId = TransactionId txId; ImportId = importId; Request = request }
      ComdirectReference = reference
      Date = DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
      Amount = eur -23.40m
      Payee = Some bankPayee
      RawPayee = Some bankPayee
      Memo = None
      ReviewSteps = []
      Hold = None }

[<Tests>]
let importTests =
    // Geteilt: die In-Memory-DB. Die DB-Listen laufen nacheinander in dieser Gruppe, parallel zum Rest
    // (`TestGroups.InMemoryDb`).
    testSequencedGroup TestGroups.InMemoryDb
    <| testList "Payee: Comdirect-Import" [

        testCase "ein geänderter Payee lässt import_id und Bank-Referenz stehen; derselbe Abruf legt keine zweite Buchung an" <| fun () ->
            initializeLedgerSchema ()
            let account = freshAccount ()
            let suffix = Guid.NewGuid().ToString("N").Substring(0, 8)
            let txId = $"tx-fxt09-{suffix}"
            let importId = $"BB:fxt09-{suffix}"
            let reference = $"ref-fxt09-{suffix}"
            let ynabId = $"ynab-fxt09-{suffix}"
            let candidate = importCandidate txId importId reference
            let created : YnabResult<TransactionCreateResult> =
                Ok { emptyCreateResult with Created = [ { TransactionId = TransactionId txId; ImportId = importId; YnabTransactionId = ynabId } ] }
            let first = pushComdirectImports at (fun _ -> async.Return created) ignore account [ candidate ] |> Async.RunSynchronously
            let entryId =
                match Map.tryFind (TransactionId txId) first.Outcomes with
                | Some (Linked (entryId, _)) -> entryId
                | other -> failtest $"erwartet Linked, war {other}"
            let linksBefore = linksForEntry entryId |> Async.RunSynchronously

            let probe = okProbe ()
            setPayee probe.Push entryId "Rewe" |> unwrap |> ignore
            Expect.equal probe.Calls [ ynabId, Some "Rewe" ] "Update an die verknüpfte YNAB-Buchung"
            let links = linksForEntry entryId |> Async.RunSynchronously
            Expect.equal (Set.ofList links) (Set.ofList linksBefore) "Verknüpfungen unverändert"
            Expect.isTrue (links |> List.exists (fun l -> l.System = ImportIdSystem && l.Value = importId)) "import_id bleibt"
            Expect.isTrue (links |> List.exists (fun l -> l.System = ComdirectSystem && l.Value = reference)) "Bank-Referenz bleibt"

            let mutable sent = 0
            let second =
                pushComdirectImports at (fun txs -> sent <- sent + List.length txs; async.Return created) ignore account [ candidate ]
                |> Async.RunSynchronously
            match Map.tryFind (TransactionId txId) second.Outcomes with
            | Some (AlreadyImported again) -> Expect.equal again entryId "dieselbe Buchung"
            | other -> failtest $"erwartet AlreadyImported, war {other}"
            Expect.equal sent 0 "nichts erneut gesendet"
            Expect.equal (findEntryByLink ComdirectSystem reference |> Async.RunSynchronously) (Some entryId) "eine Buchung je Reference"
            Expect.equal (entryOf entryId).Payee (Some "Rewe") "der Payee in BB bleibt der geänderte"
    ]

// ============================================
// Schreib-Sperre gegen den Spiegel-Lauf (ADR 0055 Punkt 6)
// ============================================
//
// Ohne Sperre sähe ein Lauf zwischen Ereignis und PUT den alten Payee in YNAB als Änderung und
// drehte den BB-Payee zurück.

[<Tests>]
let payeeWriteGuardTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    let waitsForMirror (entryId: EntryId) (probe: PayeeProbe) (command: Async<'a>) : 'a =
        let before = eventTypesOf entryId
        LedgerWriteGuardProbe.commandWaitsForMirror
            (fun () ->
                Expect.equal (eventTypesOf entryId) before "noch kein Ereignis während des Laufs"
                Expect.isEmpty probe.Calls "noch kein Push")
            command

    // Geteilt: die In-Memory-DB (`TestGroups.InMemoryDb`), dazu die prozessweite Schreib-Sperre: jeder Fall hält
    // einen Spiegel-Lauf fest, und Listen, die über alle Buchungen zählen, sähen seine Buchungen.
    testSequencedGroup TestGroups.InMemoryDb
    <| testList "Payee: Schreib-Sperre" [

        testCase "Payee ändern während eines Spiegel-Laufs schreibt und pusht erst nach dem Lauf" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            let probe = okProbe ()
            waitsForMirror entryId probe (setEntryPayee at probe.Push entryId (Some "Rewe")) |> unwrap |> ignore
            Expect.equal (eventTypesOf entryId |> List.length) 2 "ein neues Ereignis erst nach dem Lauf"
            Expect.equal probe.Calls.Length 1 "ein Push"

        testCase "Erneut senden des Payees während eines Spiegel-Laufs pusht erst nach dem Lauf" <| fun () ->
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            setPayee (PayeeProbe(Error (YnabError.NetworkError "weg"))).Push entryId "Rewe" |> unwrap |> ignore
            let probe = okProbe ()
            let result = waitsForMirror entryId probe (retryPayeeWriteBack at probe.Push entryId) |> unwrap
            Expect.equal result PayeeSent "gesendet"
            Expect.equal (eventTypesOf entryId |> List.last) "EntryPayeeWrittenBackToYnab" "Ereignis erst nach dem Lauf"
            Expect.equal probe.Calls.Length 1 "ein Push"
    ]

// ============================================
// Spiegel: observe mit Baseline
// ============================================

let private observationOf (entry: Entry) (payee: string option) : YnabObservation = {
    YnabTransactionId = "ynab-x"
    AccountId = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000f9")
    ImportId = None
    Date = entry.Date
    Amount = entry.Amount
    Payee = payee
    Memo = entry.Memo
    Category = entry.Category
    Splits = entry.Splits
    MatchedYnabTransactionId = None
    SplitTransfers = []
    Cleared = entry.Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Deleted = false
    Approved = entry.Approved
    FlagColor = entry.FlagColor
    FlagName = entry.FlagName
}

let private contextOf (entry: Entry) : ObservationContext =
    { EntryId = entry.Id; AccountId = entry.AccountId; ObservedAt = at }

/// BB hat den Payee auf "Rewe" gesetzt, der Push ist gescheitert (Baseline: der Bank-Payee).
let private entryWithOpenFailure () : Entry =
    let entryId = newEntryId ()
    [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (freshAccount ()) (Some bankPayee)))
      eventOn entryId UserInBb changedBody
      eventOn entryId UserInBb failedBody ]
    |> replay
    |> Option.get

/// BB hat den Payee "Rewe" entfernt, der Push ist gescheitert (Baseline: "Rewe", ledger-pkrwd).
let private entryWithRemovedFailure () : Entry =
    let entryId = newEntryId ()
    [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (freshAccount ()) (Some "Rewe")))
      eventOn entryId UserInBb (EntryPayeeChangedInBb { OldPayee = Some "Rewe"; NewPayee = None })
      eventOn entryId UserInBb (EntryPayeeWriteBackFailed { Reason = "weg"; BaselinePayee = Some "Rewe" }) ]
    |> replay
    |> Option.get

let private replayWith (entry: Entry) (events: LedgerEvent list) : Entry =
    events |> List.fold (fun state ev -> Some (apply state ev)) (Some entry) |> Option.get

let private namesOf (events: LedgerEvent list) = events |> List.map (fun e -> eventTypeName e.Body)

[<Tests>]
let observeTests =
    testList "Payee: Spiegel mit Baseline" [

        testCase "YNAB zeigt den BB-Stand: der Fehlschlag endet mit Actor YnabMirror, keine Beobachtung" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "Rewe"))
            Expect.equal (namesOf events) [ "EntryPayeeWrittenBackToYnab" ] "nur das Ende"
            Expect.equal (events |> List.map (fun e -> e.Actor)) [ YnabMirror ] "vom Spiegel"
            Expect.isNone (replayWith entry events).PayeeWriteBackFailure "beendet"

        testCase "YNAB zeigt noch die Baseline: kein Ereignis, Payee und Fehlschlag bleiben" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some bankPayee))
            Expect.isEmpty events "YNAB hat den Push nie gesehen"

        testCase "YNAB zeigt einen dritten Payee: echte Änderung, der Fehlschlag endet, YNAB gilt" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "Penny"))
            Expect.equal (namesOf events) [ "EntryDetailsChangeObservedInYnab" ] "Beobachtung"
            let after = replayWith entry events
            Expect.equal after.Payee (Some "Penny") "YNABs Stand"
            Expect.isNone after.PayeeWriteBackFailure "Fehlschlag beendet"

        testCase "YNAB bildet „Rewe“ auf den vorhandenen Payee „REWE“ ab: das ist eine echte Änderung und gilt" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "REWE"))
            Expect.equal (namesOf events) [ "EntryDetailsChangeObservedInYnab" ] "Beobachtung"
            Expect.equal (replayWith entry events).Payee (Some "REWE") "YNABs Schreibweise"

        testCase "andere Kopfdaten ändern sich bei Baseline: der BB-Payee wird nicht überschrieben" <| fun () ->
            let entry = entryWithOpenFailure ()
            let obs = { observationOf entry (Some bankPayee) with Memo = Some "neu in YNAB" }
            let events = observe (contextOf entry) (Some entry) obs
            let after = replayWith entry events
            Expect.equal after.Memo (Some "neu in YNAB") "Memo gespiegelt"
            Expect.equal after.Payee (Some "Rewe") "Payee bleibt der BB-Stand"
            Expect.isSome after.PayeeWriteBackFailure "Fehlschlag bleibt"

        // ledger-pkrwd: dieselbe Baseline-Regel, wenn BB den Payee entfernt hat.
        testCase "Payee entfernt, YNAB zeigt keinen Payee: der Fehlschlag endet mit Actor YnabMirror" <| fun () ->
            let entry = entryWithRemovedFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry None)
            Expect.equal (namesOf events) [ "EntryPayeeWrittenBackToYnab" ] "nur das Ende"
            Expect.equal (events |> List.map (fun e -> e.Actor)) [ YnabMirror ] "vom Spiegel"
            Expect.isNone (replayWith entry events).PayeeWriteBackFailure "beendet"

        testCase "Payee entfernt, YNAB zeigt noch die Baseline „Rewe“: BB bleibt ohne Payee, der Fehlschlag bleibt" <| fun () ->
            let entry = entryWithRemovedFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "Rewe"))
            Expect.isEmpty events "YNAB hat den Push nie gesehen"
            let after = replayWith entry events
            Expect.equal after.Payee None "ohne Payee"
            Expect.isSome after.PayeeWriteBackFailure "Fehlschlag bleibt"

        testCase "Payee entfernt, YNAB zeigt einen dritten Payee: der gilt, der Fehlschlag endet" <| fun () ->
            let entry = entryWithRemovedFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "Penny"))
            Expect.equal (namesOf events) [ "EntryDetailsChangeObservedInYnab" ] "Beobachtung"
            let after = replayWith entry events
            Expect.equal after.Payee (Some "Penny") "YNABs Stand"
            Expect.isNone after.PayeeWriteBackFailure "Fehlschlag beendet"

        testCase "ohne offenen Fehlschlag gewinnt YNAB wie bisher" <| fun () ->
            let entryId = newEntryId ()
            let entry =
                [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (freshAccount ()) (Some "Rewe"))) ]
                |> replay
                |> Option.get
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "Rewe Markt"))
            Expect.equal (replayWith entry events).Payee (Some "Rewe Markt") "Beobachtung"
    ]

// ============================================
// Historie
// ============================================

[<Tests>]
let historyTests =
    testList "Payee: Historie" [

        testCase "Änderung, Fehlschlag und Ankunft haben eigene Sätze" <| fun () ->
            Expect.equal (ReadModel.describe changedBody) $"Payee geändert auf Rewe, vorher {bankPayee}" "geändert"
            Expect.equal
                (ReadModel.describe (EntryPayeeChangedInBb { OldPayee = None; NewPayee = Some "Rewe" }))
                "Payee geändert auf Rewe, vorher ohne Payee"
                "ohne alten Payee"
            Expect.equal (ReadModel.describe failedBody) "Payee nicht in YNAB: Verbindung weg" "Fehlschlag"
            Expect.equal (ReadModel.describe EntryPayeeWrittenBackToYnab) "Payee in YNAB angekommen" "angekommen"
            Expect.equal
                (ReadModel.describe (EntryPayeeChangedInBb { OldPayee = Some "Rewe"; NewPayee = None }))
                "Payee entfernt, vorher Rewe"
                "entfernt (ledger-pkrwd)"

        testCase "die Historie einer Buchung trägt die Payee-Zeilen in der Reihenfolge des Logs" <| fun () ->
            initializeLedgerSchema ()
            let entryId, _ = seedLinked (freshAccount ()) (Some bankPayee)
            setPayee (PayeeProbe(Error (YnabError.NetworkError "weg"))).Push entryId "Rewe" |> unwrap |> ignore
            let (EntryId guid) = entryId
            let lines = getEntryHistory guid |> Async.RunSynchronously |> List.map (fun l -> l.Description)
            Expect.equal
                (lines |> List.skip 1)
                [ $"Payee geändert auf Rewe, vorher {bankPayee}"; "Payee nicht in YNAB: Netzwerkfehler: weg" ]
                "Payee-Zeilen"
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

let private sendWith (status: HttpStatusCode) (payee: string option) =
    let handler = new FakeHandler(status, "{}")
    use client = new HttpClient(handler)
    let result = sendPayee client (YnabBudgetId "budget-1") "tx-1" payee |> Async.RunSynchronously
    handler, result

module JD = Thoth.Json.Net.Decode

[<Tests>]
let ynabClientTests =
    let keysOf (body: string) =
        match JD.fromString (JD.field "transaction" (JD.keyValuePairs JD.value)) body with
        | Ok pairs -> pairs |> List.map fst
        | Error e -> failtest e

    testList "YnabClient Payee (Fake-Handler)" [

        testCase "der Rumpf trägt payee_id null und payee_name, sonst nichts" <| fun () ->
            let body = buildPayeeBody (Some "Rewe")
            Expect.equal (keysOf body) [ "payee_id"; "payee_name" ] "nur die beiden Felder"
            Expect.stringContains body "\"payee_id\": null" "payee_id wörtlich null"
            let name = JD.fromString (JD.at [ "transaction"; "payee_name" ] JD.string) body
            Expect.equal name (Ok "Rewe") "der Name"
            for forbidden in [ "memo"; "category_id"; "approved"; "cleared"; "import_id"; "flag_color"; "amount" ] do
                Expect.isFalse (body.Contains $"\"{forbidden}\"") $"kein {forbidden} im Rumpf"

        // ledger-pkrwd: "Payee entfernen" schickt beide Felder als null (Probelauf gegen das Budget Test).
        testCase "ohne Payee trägt der Rumpf payee_id null und payee_name null, sonst nichts" <| fun () ->
            let body = buildPayeeBody None
            Expect.equal (keysOf body) [ "payee_id"; "payee_name" ] "nur die beiden Felder"
            Expect.stringContains body "\"payee_id\": null" "payee_id wörtlich null"
            Expect.stringContains body "\"payee_name\": null" "payee_name wörtlich null"
            let handler, result = sendWith HttpStatusCode.OK None
            Expect.equal result (Ok ()) "200 ist Erfolg"
            Expect.equal handler.Body body "gesendet wie gebaut"

        testCase "Methode, Pfad und Rumpf stimmen" <| fun () ->
            let handler, result = sendWith HttpStatusCode.OK (Some "Rewe")
            Expect.equal result (Ok ()) "200 ist Erfolg"
            Expect.equal handler.Method HttpMethod.Put "einzelne Buchung ändern ist PUT"
            Expect.stringEnds handler.Url "/budgets/budget-1/transactions/tx-1" "Pfad"
            Expect.equal handler.Body (buildPayeeBody (Some "Rewe")) "der Rumpf aus buildPayeeBody"

        testCase "Fehlerzuordnung 400, 401, 404, 429 wie sendMemo" <| fun () ->
            let result status = sendWith status (Some "Rewe") |> snd
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
            match sendPayee client (YnabBudgetId "b") "t" (Some "Rewe") |> Async.RunSynchronously with
            | Error (YnabError.NetworkError _) -> ()
            | other -> failtest $"erwartet Netzfehler, war {other}"
    ]

// ============================================
// Token und Budget aus den Settings (Review-Fund zu ledger-fxt09)
// ============================================

[<Tests>]
let ynabPushSettingsTests =
    // `memoPush` und `payeePush` holen Token und Budget über `Server.Api.ynabPushWith`; die Verdrahtung
    // mit `Persistence.Settings.getSetting` und `YnabClient.updatePayee` prüft `apiWirings`.
    let pushWith (settings: (string * string) list) =
        let lookup key = async.Return (settings |> List.tryFind (fun (k, _) -> k = key) |> Option.map snd)
        Server.Api.ynabPushWith
            lookup
            (fun token budgetId -> fun (_: string) (_: string option) -> async.Return (Ok (token, budgetId)))
            (fun error -> fun _ _ -> async.Return (Error error))
        |> Async.RunSynchronously
    let send push = push "tx-1" (Some "Rewe") |> Async.RunSynchronously
    let both = [ "ynab_token", "t-1"; "ynab_default_budget_id", "b-1" ]
    testList "Payee: Token und Budget aus den Settings" [
        testCase "mit Token und Budget geht der Push mit genau diesen an YNAB" <| fun () ->
            Expect.equal (send (pushWith both)) (Ok ("t-1", YnabBudgetId "b-1")) "Token und Budget"

        testCase "ohne Token antwortet der Push mit Unauthorized, der Grund im Hinweis ist deutsch" <| fun () ->
            match send (pushWith [ "ynab_default_budget_id", "b-1" ]) with
            | Error (YnabError.Unauthorized _ as error) ->
                Expect.equal (describePayeePushError error) "YNAB-Token ungültig (HTTP 401)" "Grund"
            | other -> failtest $"erwartet Unauthorized, war {other}"

        testCase "ohne Budget antwortet der Push mit BudgetNotFound" <| fun () ->
            match send (pushWith [ "ynab_token", "t-1" ]) with
            | Error (YnabError.BudgetNotFound _ as error) ->
                Expect.equal (describePayeePushError error) "Budget in YNAB nicht gefunden" "Grund"
            | other -> failtest $"erwartet BudgetNotFound, war {other}"
    ]
