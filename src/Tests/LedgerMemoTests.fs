module LedgerMemoTests

// Memo bearbeiten (ledger-fa1fp, ADR 0062):
//
//  * Regel `normalizeMemo` (getrimmt, leer heißt kein Memo, Grenze von YNAB);
//  * Ereignisse: Codec, Namen, Schema-Version;
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall) mit einem
//    injizierten Push, der nie YNAB erreicht und seine Aufrufe zählt;
//  * `observe` mit Baseline (der Spiegel);
//  * Historie: Memo-Sätze und die Zeile nach dem Aufteilen mit Zielen und Beträgen;
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
let private at = DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc)

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

let private freshAccount () = LedgerAccountId (Guid.NewGuid())

let private observedPayload (account: LedgerAccountId) (memo: string option) : EntryObservedInYnabPayload = {
    AccountId = account
    YnabTransactionId = $"ynab-fa1fp-{Guid.NewGuid()}"
    ImportId = None
    Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -12.00m
    Payee = Some "Rewe fa1fp"
    Memo = memo
    Category = None
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = None
    FlagName = None
}

/// Eine mit YNAB verknüpfte Buchung mit gegebenem Memo.
let private seedLinked (account: LedgerAccountId) (memo: string option) : EntryId * string =
    let entryId = newEntryId ()
    let payload = observedPayload account memo
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab payload) ] |> Async.RunSynchronously
    entryId, payload.YnabTransactionId

let private seedUnlinked (account: LedgerAccountId) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        eventOn entryId QuickAdd (
            CashExpenseAdded {
                AccountId = account
                Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -3.00m
                Payee = Some "Kiosk fa1fp"
                Memo = None
                Category = None
                Cleared = Uncleared
                Approved = false
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
type private MemoProbe(result: YnabResult<unit>) =
    member val Calls : (string * string option) list = [] with get, set
    member val OnCall : (unit -> unit) = ignore with get, set
    member this.Push : MemoPush =
        fun ynabId memo ->
            async {
                this.OnCall ()
                this.Calls <- this.Calls @ [ ynabId, memo ]
                return result
            }

let private okProbe () = MemoProbe(Ok ())

let private unwrap (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"Befehl sollte gelingen, war: {e}"

let private setMemo (push: MemoPush) (entryId: EntryId) (text: string) =
    setEntryMemo at push entryId text |> Async.RunSynchronously

// ============================================
// Regel: normalizeMemo
// ============================================

[<Tests>]
let normalizeTests =
    testList "Memo: Eingabe prüfen (ADR 0062)" [

        testCase "Text wird getrimmt, leer und nur Leerraum heißen kein Memo" <| fun () ->
            Expect.equal (normalizeMemo "  Werkzeug + Lampe  ") (Ok (Some "Werkzeug + Lampe")) "getrimmt"
            Expect.equal (normalizeMemo "") (Ok None) "leer"
            Expect.equal (normalizeMemo "  \t ") (Ok None) "nur Leerraum"
            Expect.equal (normalizeMemo null) (Ok None) "null"

        testCase "die Grenze von YNAB: genau 500 Zeichen gehen, 501 werden abgelehnt" <| fun () ->
            Expect.equal ynabMemoMaxLength 500 "Grenze"
            let atLimit = String('a', ynabMemoMaxLength)
            Expect.equal (normalizeMemo atLimit) (Ok (Some atLimit)) "genau an der Grenze"
            Expect.equal (normalizeMemo (atLimit + "a")) (Error memoTooLongReason) "eins darüber"

        testCase "die Länge zählt nach dem Trimmen" <| fun () ->
            let padded = "  " + String('a', ynabMemoMaxLength) + "  "
            Expect.isOk (normalizeMemo padded) "Leerraum am Rand zählt nicht"

        testCase "memoKey: Leerraum und kein Memo sind dasselbe" <| fun () ->
            Expect.equal (memoKey (Some "   ")) None "Leerraum"
            Expect.equal (memoKey (Some " a ")) (Some "a") "getrimmt"
            Expect.equal (memoKey None) None "None"
    ]

// ============================================
// Ereignisse
// ============================================

let private changedBody : LedgerEventBody = EntryMemoChangedInBb { OldMemo = Some "alt"; NewMemo = Some "neu" }

let private failedBody : LedgerEventBody = EntryMemoWriteBackFailed { Reason = "Verbindung weg"; BaselineMemo = Some "alt" }

[<Tests>]
let eventTests =
    testList "Memo: Ereignisse" [

        testCase "Rundreise des Codecs, Namen und Schema-Version 1 für alle drei Typen" <| fun () ->
            for body, name in
                [ changedBody, "EntryMemoChangedInBb"
                  failedBody, "EntryMemoWriteBackFailed"
                  EntryMemoWrittenBackToYnab, "EntryMemoWrittenBackToYnab" ] do
                Expect.equal (eventTypeName body) name "Typname"
                Expect.equal (schemaVersion body) 1 $"Version von {name}"
                Expect.equal (decodePayload name 1 (encodePayload body)) (Ok body) $"Rundreise {name}"

        testCase "leere Werte laufen heil durch den Codec (kein Memo als Alt und Neu, keine Baseline)" <| fun () ->
            let empty = EntryMemoChangedInBb { OldMemo = None; NewMemo = None }
            Expect.equal (decodePayload "EntryMemoChangedInBb" 1 (encodePayload empty)) (Ok empty) "Rundreise leer"
            let noBaseline = EntryMemoWriteBackFailed { Reason = "x"; BaselineMemo = None }
            Expect.equal (decodePayload "EntryMemoWriteBackFailed" 1 (encodePayload noBaseline)) (Ok noBaseline) "ohne Baseline"

        testCase "eine unbekannte Version bleibt ein Fehler" <| fun () ->
            match decodePayload "EntryMemoChangedInBb" 2 "{}" with
            | Error _ -> ()
            | Ok _ -> failtest "Version 2 gibt es nicht"
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

    testSequenced
    <| testList "Memo: Befehle (ADR 0062)" [

        testCase "Setzen ohne Memo: ein Ereignis mit Alt = keins, Neu = Text, Push mit nur dem Memo" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedLinked account None
            let probe = okProbe ()
            let outcome = setMemo probe.Push entryId "  Werkzeug + Lampe " |> unwrap

            Expect.equal outcome.MemoChange MemoChanged "geändert"
            Expect.equal outcome.MemoWriteBack MemoSent "gesendet"
            Expect.equal outcome.PreviousMemo None "Vorzustand: kein Memo"
            Expect.equal outcome.CurrentMemo (Some "Werkzeug + Lampe") "getrimmt gespeichert"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab"; "EntryMemoChangedInBb" ] "ein Ereignis"
            match (eventsOf entryId |> List.last).Event with
            | { Actor = UserInBb; Body = EntryMemoChangedInBb p } ->
                Expect.equal (p.OldMemo, p.NewMemo) (None, Some "Werkzeug + Lampe") "Alt und Neu"
            | other -> failtest $"erwartet EntryMemoChangedInBb von UserInBb, war {other}"
            Expect.equal (entryOf entryId).Memo (Some "Werkzeug + Lampe") "Zustand"
            Expect.equal probe.Calls [ ynabId, Some "Werkzeug + Lampe" ] "genau ein Push"

        testCase "Ändern: Alt ist das bisherige Memo" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account (Some "alt")
            let probe = okProbe ()
            let outcome = setMemo probe.Push entryId "neu" |> unwrap
            Expect.equal outcome.PreviousMemo (Some "alt") "Vorzustand für Rückgängig"
            match (eventsOf entryId |> List.last).Event.Body with
            | EntryMemoChangedInBb p -> Expect.equal (p.OldMemo, p.NewMemo) (Some "alt", Some "neu") "Alt und Neu"
            | other -> failtest $"{other}"

        testCase "Leeren: leerer Text heißt kein Memo, der Push sendet null" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedLinked account (Some "weg damit")
            let probe = okProbe ()
            let outcome = setMemo probe.Push entryId "   " |> unwrap
            Expect.equal outcome.MemoChange MemoChanged "geändert"
            Expect.equal outcome.CurrentMemo None "kein Memo"
            Expect.equal (entryOf entryId).Memo None "Zustand ohne Memo"
            Expect.equal probe.Calls [ ynabId, None ] "Push mit null"

        testCase "Ziel gleich Ist (auch Leerraum um dasselbe Memo): kein Ereignis, kein Push" <| fun () ->
            let account = freshAccount ()
            let withMemo, _ = seedLinked account (Some "gleich")
            let without, _ = seedLinked account None
            let probe = okProbe ()
            let same = setMemo probe.Push withMemo " gleich " |> unwrap
            Expect.equal (same.MemoChange, same.MemoWriteBack) (MemoUnchanged, MemoNotSent) "gleiches Memo"
            let empty = setMemo probe.Push without "" |> unwrap
            Expect.equal (empty.MemoChange, empty.MemoWriteBack) (MemoUnchanged, MemoNotSent) "gleich leer"
            Expect.equal (eventTypesOf withMemo) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.equal (eventTypesOf without) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "ein zu langes Memo wird abgelehnt, es entsteht weder Ereignis noch Push" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None
            let probe = okProbe ()
            Expect.equal (setMemo probe.Push entryId (String('x', ynabMemoMaxLength + 1))) (Error memoTooLongReason) "zu lang"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein Push"
            let atLimit = String('x', ynabMemoMaxLength)
            Expect.isOk (setMemo probe.Push entryId atLimit) "an der Grenze geht es"

        testCase "eine in YNAB gelöschte oder unbekannte Buchung wird abgelehnt" <| fun () ->
            let account = freshAccount ()
            let deleted, _ = seedLinked account None
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let probe = okProbe ()
            match setMemo probe.Push deleted "Notiz" with
            | Error message -> Expect.stringContains message "gelöscht" "Grund"
            | Ok _ -> failtest "gelöschte Buchung muss abgelehnt werden"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal (eventTypesOf deleted) [ "EntryObservedInYnab"; "EntryDeletionObservedInYnab" ] "kein Memo-Ereignis"
            Expect.equal (setMemo probe.Push (newEntryId ()) "x") (Error "Buchung nicht gefunden") "unbekannt"

        testCase "Reihenfolge: das Ereignis steht im Log, bevor der Push läuft; Erfolg erzeugt kein weiteres" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None
            let probe = okProbe ()
            let mutable seenAtPush : string list = []
            probe.OnCall <- fun () -> seenAtPush <- eventTypesOf entryId
            setMemo probe.Push entryId "Notiz" |> unwrap |> ignore
            Expect.equal seenAtPush [ "EntryObservedInYnab"; "EntryMemoChangedInBb" ] "beim Push steht das Ereignis schon da"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab"; "EntryMemoChangedInBb" ] "danach kein weiteres"

        testCase "Rückgängig ist derselbe Befehl mit dem alten Memo und stellt es wieder her" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedLinked account (Some "vorher")
            let probe = okProbe ()
            let outcome = setMemo probe.Push entryId "nachher" |> unwrap
            let undone = setMemo probe.Push entryId (outcome.PreviousMemo |> Option.defaultValue "") |> unwrap
            Expect.equal undone.MemoChange MemoChanged "zurückgesetzt"
            Expect.equal (entryOf entryId).Memo (Some "vorher") "altes Memo"
            Expect.equal probe.Calls [ ynabId, Some "nachher"; ynabId, Some "vorher" ] "zwei Pushes"

        testCase "Push scheitert (Netz, 4xx, 429): Fehlschlag mit Grund und Baseline, das Memo bleibt in BB" <| fun () ->
            for error in
                [ YnabError.NetworkError "Verbindung weg"
                  YnabError.InvalidResponse "Bad request"
                  YnabError.RateLimitExceeded 60 ] do
                let account = freshAccount ()
                let entryId, _ = seedLinked account (Some "alt")
                let probe = MemoProbe(Error error)
                let outcome = setMemo probe.Push entryId "neu" |> unwrap
                let reason =
                    match outcome.MemoWriteBack with
                    | MemoSendFailed reason -> reason
                    | other -> failtest $"erwartet MemoSendFailed, war {other}"
                Expect.equal
                    (eventTypesOf entryId)
                    [ "EntryObservedInYnab"; "EntryMemoChangedInBb"; "EntryMemoWriteBackFailed" ]
                    "Änderung zuerst, dann der Fehlschlag"
                let entry = entryOf entryId
                Expect.equal entry.Memo (Some "neu") "BB behält das Memo"
                let failure = entry.MemoWriteBackFailure |> Option.get
                Expect.equal failure.Reason reason "Grund im Zustand"
                Expect.equal failure.BaselineMemo (Some "alt") "Baseline: der Stand davor"

        testCase "ohne YNAB-Verknüpfung: Ereignis und sofort der Fehlschlag mit dem Grund, kein Push" <| fun () ->
            let account = freshAccount ()
            let entryId = seedUnlinked account
            let probe = okProbe ()
            let outcome = setMemo probe.Push entryId "Notiz" |> unwrap
            Expect.equal outcome.MemoWriteBack (MemoSendFailed memoNotLinkedReason) "noch nicht in YNAB verknüpft"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal
                (eventTypesOf entryId |> List.rev |> List.take 2 |> List.rev)
                [ "EntryMemoChangedInBb"; "EntryMemoWriteBackFailed" ]
                "Ereignis, dann Fehlschlag"

        testCase "eine weitere Geste bei offenem Fehlschlag behält die erste Baseline" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account (Some "start")
            let failing = MemoProbe(Error (YnabError.NetworkError "eins"))
            setMemo failing.Push entryId "zwei" |> unwrap |> ignore
            let failingAgain = MemoProbe(Error (YnabError.NetworkError "zwei"))
            setMemo failingAgain.Push entryId "drei" |> unwrap |> ignore
            let failure = (entryOf entryId).MemoWriteBackFailure |> Option.get
            Expect.equal failure.BaselineMemo (Some "start") "erste Baseline bleibt"

        testCase "eine neue Geste mit Erfolg beendet den offenen Fehlschlag" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None
            let failing = MemoProbe(Error (YnabError.NetworkError "weg"))
            setMemo failing.Push entryId "eins" |> unwrap |> ignore
            Expect.isSome (entryOf entryId).MemoWriteBackFailure "offen"
            let ok = okProbe ()
            setMemo ok.Push entryId "zwei" |> unwrap |> ignore
            Expect.isNone (entryOf entryId).MemoWriteBackFailure "mit der neuen Geste zurückgesetzt"
            Expect.contains (eventTypesOf entryId) "EntryMemoWrittenBackToYnab" "Erfolg nach Fehlschlag wird festgehalten"

        testCase "Erneut senden: Erfolg schreibt EntryMemoWrittenBackToYnab, der Push sendet das aktuelle Memo" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedLinked account None
            let failing = MemoProbe(Error (YnabError.NetworkError "weg"))
            setMemo failing.Push entryId "Notiz" |> unwrap |> ignore
            let ok = okProbe ()
            let result = retryMemoWriteBack at ok.Push entryId |> Async.RunSynchronously |> unwrap
            Expect.equal result MemoSent "gesendet"
            Expect.equal ok.Calls [ ynabId, Some "Notiz" ] "das Memo des BB-Stands"
            Expect.isNone (entryOf entryId).MemoWriteBackFailure "Fehlschlag beendet"
            match (eventsOf entryId |> List.last).Event with
            | { Actor = UserInBb; Body = EntryMemoWrittenBackToYnab } -> ()
            | other -> failtest $"erwartet EntryMemoWrittenBackToYnab von UserInBb, war {other}"

        testCase "Erneut senden scheitert erneut: neuer Grund, die Baseline bleibt" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account (Some "start")
            let first = MemoProbe(Error (YnabError.NetworkError "eins"))
            setMemo first.Push entryId "neu" |> unwrap |> ignore
            let second = MemoProbe(Error (YnabError.InvalidResponse "abgelehnt"))
            match retryMemoWriteBack at second.Push entryId |> Async.RunSynchronously |> unwrap with
            | MemoSendFailed reason -> Expect.stringContains reason "abgelehnt" "neuer Grund"
            | other -> failtest $"{other}"
            let failure = (entryOf entryId).MemoWriteBackFailure |> Option.get
            Expect.stringContains failure.Reason "abgelehnt" "Grund ersetzt"
            Expect.equal failure.BaselineMemo (Some "start") "Baseline bleibt"

        testCase "Erneut senden ohne offenen Fehlschlag und bei gelöschter Buchung" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account (Some "x")
            let probe = okProbe ()
            Expect.equal (retryMemoWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap) MemoNotSent "nichts offen"
            Expect.isEmpty probe.Calls "kein Push"
            appendAndApply [ eventOn entryId YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            match retryMemoWriteBack at probe.Push entryId |> Async.RunSynchronously with
            | Error message -> Expect.stringContains message "gelöscht" "abgelehnt"
            | Ok _ -> failtest "gelöscht muss abgelehnt werden"

        testCase "der offene Fehlschlag überlebt das Speichern und Laden der Zeile" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account (Some "alt")
            let failing = MemoProbe(Error (YnabError.NetworkError "weg"))
            setMemo failing.Push entryId "neu" |> unwrap |> ignore
            let loaded = entryOf entryId
            Expect.equal loaded.MemoWriteBackFailure.Value.BaselineMemo (Some "alt") "Baseline aus der Datenbank"
            Expect.equal (loaded.MemoWriteBackFailure |> Option.map (fun f -> f.Reason)) (Some "Netzwerkfehler: weg") "Grund"
            let view = ReadModel.toEntryView Map.empty Map.empty DecidedInBb loaded
            Expect.equal view.MemoWriteBackFailure (Some "Netzwerkfehler: weg") "die Sicht trägt den Grund"
    ]

// ============================================
// Spiegel: observe mit Baseline (ADR 0062)
// ============================================

let private observationOf (entry: Entry) (memo: string option) : YnabObservation = {
    YnabTransactionId = "ynab-x"
    AccountId = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000d1")
    ImportId = None
    Date = entry.Date
    Amount = entry.Amount
    Payee = entry.Payee
    Memo = memo
    Category = entry.Category
    Splits = entry.Splits
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

/// BB hat das Memo auf "neu" gesetzt, der Push ist gescheitert (Baseline: "alt").
let private entryWithOpenFailure () : Entry =
    let entryId = newEntryId ()
    [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (freshAccount ()) (Some "alt")))
      eventOn entryId UserInBb (EntryMemoChangedInBb { OldMemo = Some "alt"; NewMemo = Some "neu" })
      eventOn entryId UserInBb (EntryMemoWriteBackFailed { Reason = "weg"; BaselineMemo = Some "alt" }) ]
    |> replay
    |> Option.get

let private replayWith (entry: Entry) (events: LedgerEvent list) : Entry =
    events |> List.fold (fun state ev -> Some (apply state ev)) (Some entry) |> Option.get

[<Tests>]
let observeTests =
    testList "Memo: Spiegel mit Baseline" [

        testCase "YNAB zeigt den BB-Stand: der Fehlschlag endet, Actor YnabMirror, keine Beobachtung" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "neu"))
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryMemoWrittenBackToYnab" ] "nur das Ende"
            Expect.equal (events |> List.map (fun e -> e.Actor)) [ YnabMirror ] "vom Spiegel"
            Expect.isNone (replayWith entry events).MemoWriteBackFailure "beendet"

        testCase "YNAB zeigt noch die Baseline: kein Ereignis, der Fehlschlag bleibt, das BB-Memo bleibt" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "alt"))
            Expect.isEmpty events "YNAB hat den Push nie gesehen"

        testCase "YNAB zeigt ein drittes Memo: echte Änderung, der Fehlschlag endet, YNAB gewinnt" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "ganz anders"))
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryDetailsChangeObservedInYnab" ] "Beobachtung"
            let after = replayWith entry events
            Expect.equal after.Memo (Some "ganz anders") "YNABs Stand"
            Expect.isNone after.MemoWriteBackFailure "Fehlschlag beendet"

        testCase "andere Kopfdaten ändern sich bei Baseline: das BB-Memo wird nicht überschrieben" <| fun () ->
            let entry = entryWithOpenFailure ()
            let obs = { observationOf entry (Some "alt") with Payee = Some "Anderer Payee" }
            let events = observe (contextOf entry) (Some entry) obs
            let after = replayWith entry events
            Expect.equal after.Payee (Some "Anderer Payee") "Payee gespiegelt"
            Expect.equal after.Memo (Some "neu") "Memo bleibt der BB-Stand"
            Expect.isSome after.MemoWriteBackFailure "Fehlschlag bleibt"

        testCase "ohne offenen Fehlschlag gewinnt YNAB wie bisher" <| fun () ->
            let entryId = newEntryId ()
            let entry =
                [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (freshAccount ()) (Some "alt"))) ]
                |> replay
                |> Option.get
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "neu in YNAB"))
            Expect.equal (replayWith entry events).Memo (Some "neu in YNAB") "Beobachtung"
    ]

// ============================================
// Historie
// ============================================

let private moebel : TransactionSplit =
    { Target = ToCategory (YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000a1"), "Möbel")
      Amount = eur -23.40m
      Memo = None }

let private haushalt : TransactionSplit =
    { Target = ToCategory (YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000a2"), "Haushalt")
      Amount = eur -58.50m
      Memo = None }

let private recategorized (oldSplits: TransactionSplit list) (newSplits: TransactionSplit list) =
    EntryRecategorized {
        OldCategory = None
        NewCategory = None
        OldSplits = oldSplits
        NewSplits = newSplits
        OldPayee = None
        NewPayee = None
    }

[<Tests>]
let historyTests =
    testList "Memo und Aufteilen: Historie" [

        testCase "Memo gesetzt, geändert, entfernt" <| fun () ->
            let describe changed = ReadModel.describe (EntryMemoChangedInBb changed)
            Expect.equal (describe { OldMemo = None; NewMemo = Some "Lampe" }) "Memo gesetzt: Lampe" "gesetzt"
            Expect.equal (describe { OldMemo = Some "Lampe"; NewMemo = Some "Werkzeug" }) "Memo geändert: Lampe → Werkzeug" "geändert"
            Expect.equal (describe { OldMemo = Some "Lampe"; NewMemo = None }) "Memo entfernt: Lampe" "entfernt"

        testCase "Fehlschlag und Ankunft des Memos haben eigene Sätze" <| fun () ->
            Expect.equal (ReadModel.describe failedBody) "Memo nicht in YNAB: Verbindung weg" "Fehlschlag"
            Expect.equal (ReadModel.describe EntryMemoWrittenBackToYnab) "Memo in YNAB angekommen" "angekommen"

        testCase "Aufteilen nennt jede Zeile mit Kategorie und Betrag (Tafel 4.7)" <| fun () ->
            let line = ReadModel.describe (recategorized [] [ moebel; haushalt ])
            Expect.equal line "In BB umkategorisiert: Aufgeteilt in Möbel -23,40 EUR, Haushalt -58,50 EUR" "beide Zeilen"
            Expect.isFalse (line.Contains "2 Split-Zeilen") "nicht mehr nur die Anzahl"

        testCase "eine Übertrag-Zeile nennt das Konto, eine offene Zeile ihren Betrag" <| fun () ->
            let transfer =
                { Target = ToTransfer (YnabAccountId (Guid.NewGuid()), "Sparkonto"); Amount = eur -10.00m; Memo = None }
            let open' = { Target = Uncategorized; Amount = eur -2.00m; Memo = None }
            let line = ReadModel.describe (recategorized [] [ transfer; open' ])
            Expect.stringContains line "Übertrag Sparkonto -10,00 EUR" "Übertrag"
            Expect.stringContains line "ohne Ziel -2,00 EUR" "offen"

        testCase "geänderte Aufteilung zeigt alte und neue Zeilen, Aufheben bleibt kurz" <| fun () ->
            let changed = ReadModel.describe (recategorized [ moebel; haushalt ] [ haushalt; moebel ])
            Expect.stringContains changed "Aufteilung geändert: Möbel -23,40 EUR, Haushalt -58,50 EUR → Haushalt -58,50 EUR, Möbel -23,40 EUR" "alt → neu"
            Expect.stringContains (ReadModel.describe (recategorized [ moebel; haushalt ] [])) "Split aufgehoben" "aufgehoben"

        testCase "die Historie einer Buchung trägt die Memo-Zeilen in der Reihenfolge des Logs" <| fun () ->
            initializeLedgerSchema ()
            let entryId, _ = seedLinked (freshAccount ()) None
            let failing = MemoProbe(Error (YnabError.NetworkError "weg"))
            setMemo failing.Push entryId "Notiz" |> unwrap |> ignore
            let (EntryId guid) = entryId
            let lines = getEntryHistory guid |> Async.RunSynchronously |> List.map (fun l -> l.Description)
            Expect.equal (lines |> List.skip 1) [ "Memo gesetzt: Notiz"; "Memo nicht in YNAB: Netzwerkfehler: weg" ] "Memo-Zeilen"
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

let private sendWith (status: HttpStatusCode) (memo: string option) =
    let handler = new FakeHandler(status, "{}")
    use client = new HttpClient(handler)
    let result = sendMemo client (YnabBudgetId "budget-1") "tx-1" memo |> Async.RunSynchronously
    handler, result

[<Tests>]
let ynabClientTests =
    let keysOf (body: string) =
        match Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.field "transaction" (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value)) body with
        | Ok pairs -> pairs |> List.map fst
        | Error e -> failtest e

    testList "YnabClient Memo (Fake-Handler)" [

        testCase "Body trägt nur memo, mit dem Text unverändert" <| fun () ->
            let body = buildMemoBody (Some "Werkzeug + Lampe")
            Expect.equal (keysOf body) [ "memo" ] "nur memo"
            let value = Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.at [ "transaction"; "memo" ] Thoth.Json.Net.Decode.string) body
            Expect.equal value (Ok "Werkzeug + Lampe") "Text"
            for forbidden in [ "flag_color"; "approved"; "cleared"; "category_id"; "payee" ] do
                Expect.isFalse (body.Contains forbidden) $"kein {forbidden} im Body"

        testCase "ohne Memo geht memo als null hinaus" <| fun () ->
            let body = buildMemoBody None
            Expect.stringContains body "\"memo\": null" "wörtlich null"
            Expect.equal (keysOf body) [ "memo" ] "nur memo"

        testCase "Methode, Pfad und Body stimmen" <| fun () ->
            let handler, result = sendWith HttpStatusCode.OK (Some "Notiz")
            Expect.equal result (Ok ()) "200 ist Erfolg"
            Expect.equal handler.Method HttpMethod.Put "einzelne Buchung ändern ist PUT"
            Expect.stringEnds handler.Url "/budgets/budget-1/transactions/tx-1" "Pfad"
            Expect.stringContains handler.Body "Notiz" "Body"

        testCase "Fehlerzuordnung 400, 401, 404, 429 wie sendFlag" <| fun () ->
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
            match sendMemo client (YnabBudgetId "b") "t" None |> Async.RunSynchronously with
            | Error (YnabError.NetworkError _) -> ()
            | other -> failtest $"erwartet Netzfehler, war {other}"
    ]
