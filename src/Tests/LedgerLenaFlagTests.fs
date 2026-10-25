module LedgerLenaFlagTests

// Lena fragen (ledger-xfjcp, ADR 0056 mit Nachtrag zur Baseline):
//
//  * Ereignisse: Codec, Namen, Schema-Version;
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall) mit einem
//    injizierten Push, der nie YNAB erreicht und seine Aufrufe zählt;
//  * `observe` mit Baseline (der Spiegel);
//  * Abfragen: Filter `OnlyAskLena`, Zähler `AskLenaCount`, Historie;
//  * die Einstellung `lena_flag_color`;
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

/// Eine mit YNAB verknüpfte Buchung mit gegebenem Flag, Datum und Prüfstand.
let private seedFlagged
    (account: LedgerAccountId)
    (date: DateTime)
    (approved: bool)
    (flagColor: string option)
    (flagName: string option)
    : EntryId * string =
    let entryId = newEntryId ()
    let ynabId = $"ynab-xfjcp-{Guid.NewGuid()}"
    let payload : EntryObservedInYnabPayload = {
        AccountId = account
        YnabTransactionId = ynabId
        ImportId = None
        Date = date
        Amount = eur -12.00m
        Payee = Some "Rewe xfjcp"
        Memo = None
        Category = None
        Splits = []
        Cleared = Cleared
        TransferAccountId = None
        TransferYnabTransactionId = None
        Approved = approved
        FlagColor = flagColor
        FlagName = flagName
    }
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab payload) ] |> Async.RunSynchronously
    entryId, ynabId

let private seedLinked (account: LedgerAccountId) (flagColor: string option) (flagName: string option) =
    seedFlagged account (DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)) true flagColor flagName

let private seedUnlinked (account: LedgerAccountId) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        eventOn entryId QuickAdd (
            CashExpenseAdded {
                AccountId = account
                Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -3.00m
                Payee = Some "Kiosk xfjcp"
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
type private FlagProbe(result: YnabResult<unit>) =
    member val Calls : (string * string option) list = [] with get, set
    member val OnCall : (unit -> unit) = ignore with get, set
    member this.Push : FlagPush =
        fun ynabId color ->
            async {
                this.OnCall ()
                this.Calls <- this.Calls @ [ ynabId, color ]
                return result
            }

let private okProbe () = FlagProbe(Ok ())

let private unwrap (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"Befehl sollte gelingen, war: {e}"

let private purple = Some "purple"

let private assign (push: FlagPush) (color: string option) (entryId: EntryId) =
    setLenaFlag at push color entryId AssignLena |> Async.RunSynchronously

let private withdraw (push: FlagPush) (entryId: EntryId) =
    setLenaFlag at push purple entryId WithdrawLena |> Async.RunSynchronously

let private none : FlagState = { Color = None; Name = None }

// ============================================
// Ereignisse
// ============================================

let private changedBody : LedgerEventBody =
    EntryFlagChangedInBb {
        OldFlagColor = Some "orange"
        OldFlagName = Some "Markiert"
        NewFlagColor = purple
        NewFlagName = Some "Lena"
    }

let private failedBody : LedgerEventBody =
    EntryFlagWriteBackFailed { Reason = "Verbindung weg"; BaselineFlagColor = Some "orange"; BaselineFlagName = None }

[<Tests>]
let flagEventTests =
    testList "Lena fragen: Ereignisse" [

        testCase "Rundreise des Codecs, Namen und Schema-Version 1 für alle drei Typen" <| fun () ->
            for body, name in
                [ changedBody, "EntryFlagChangedInBb"
                  failedBody, "EntryFlagWriteBackFailed"
                  EntryFlagWrittenBackToYnab, "EntryFlagWrittenBackToYnab" ] do
                Expect.equal (eventTypeName body) name "Typname"
                Expect.equal (schemaVersion body) 1 $"Version von {name}"
                Expect.equal (decodePayload name 1 (encodePayload body)) (Ok body) $"Rundreise {name}"

        testCase "leere Werte laufen heil durch den Codec (kein Flag als Alt und Neu, keine Baseline)" <| fun () ->
            let empty =
                EntryFlagChangedInBb { OldFlagColor = None; OldFlagName = None; NewFlagColor = None; NewFlagName = None }
            Expect.equal (decodePayload "EntryFlagChangedInBb" 1 (encodePayload empty)) (Ok empty) "Rundreise leer"
            let noBaseline =
                EntryFlagWriteBackFailed { Reason = "x"; BaselineFlagColor = None; BaselineFlagName = None }
            Expect.equal (decodePayload "EntryFlagWriteBackFailed" 1 (encodePayload noBaseline)) (Ok noBaseline) "Rundreise ohne Baseline"

        testCase "eine unbekannte Version bleibt ein Fehler, nie ein Rateversuch" <| fun () ->
            match decodePayload "EntryFlagChangedInBb" 2 "{}" with
            | Error _ -> ()
            | Ok _ -> failtest "Version 2 gibt es nicht"
    ]

// ============================================
// Befehle
// ============================================

[<Tests>]
let flagCommandTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Lena fragen: Befehle (ADR 0056)" [

        testCase "Zuweisen ohne Flag: ein Ereignis mit Alt = keins, Neu = Farbe und Name Lena, Push mit nur der Farbe" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedLinked account None None
            let probe = okProbe ()
            let outcome = assign probe.Push purple entryId |> unwrap

            Expect.equal outcome.Change FlagChanged "geändert"
            Expect.equal outcome.WriteBack FlagSent "gesendet"
            Expect.equal outcome.Previous none "Vorzustand: kein Flag"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab"; "EntryFlagChangedInBb" ] "ein Ereignis"
            match (eventsOf entryId |> List.last).Event with
            | { Actor = UserInBb; Body = EntryFlagChangedInBb p } ->
                Expect.equal (p.OldFlagColor, p.OldFlagName) (None, None) "Alt"
                Expect.equal (p.NewFlagColor, p.NewFlagName) (purple, Some "Lena") "Neu"
            | other -> failtest $"erwartet EntryFlagChangedInBb von UserInBb, war {other}"
            let entry = entryOf entryId
            Expect.equal (entry.FlagColor, entry.FlagName) (purple, Some "Lena") "Zustand"
            Expect.equal (ReadModel.flagOf entry.FlagColor entry.FlagName) AskLena "gedeutet als Rückfrage Lena"
            Expect.equal probe.Calls [ ynabId, purple ] "genau ein Push, nur die Farbe"

        testCase "Zuweisen auf einer Rückfrage Lena und Zurücknehmen ohne Flag: NoChange, kein Ereignis, kein Push" <| fun () ->
            let account = freshAccount ()
            let lena, _ = seedLinked account purple (Some "Lena")
            let plain, _ = seedLinked account None None
            let probe = okProbe ()

            let again = assign probe.Push purple lena |> unwrap
            Expect.equal (again.Change, again.WriteBack) (FlagUnchanged, FlagNotSent) "Zuweisen ist idempotent"
            let nothing = withdraw probe.Push plain |> unwrap
            Expect.equal (nothing.Change, nothing.WriteBack) (FlagUnchanged, FlagNotSent) "Zurücknehmen ist idempotent"

            Expect.equal (eventTypesOf lena) [ "EntryObservedInYnab" ] "kein Ereignis auf der Lena-Buchung"
            Expect.equal (eventTypesOf plain) [ "EntryObservedInYnab" ] "kein Ereignis auf der flaglosen Buchung"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "Zuweisen erkennt Lena am Namen: getrimmt und ohne Groß-/Kleinschreibung" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account (Some "blue") (Some "  lENA ")
            let probe = okProbe ()
            let outcome = assign probe.Push purple entryId |> unwrap
            Expect.equal outcome.Change FlagUnchanged "schon Rückfrage Lena"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "Zurücknehmen schreibt kein Flag; ein anderes Flag bleibt dabei stehen" <| fun () ->
            let account = freshAccount ()
            let lena, ynabLena = seedLinked account purple (Some "Lena")
            let marked, _ = seedLinked account (Some "orange") (Some "Markiert")
            let probe = okProbe ()

            let outcome = withdraw probe.Push lena |> unwrap
            Expect.equal outcome.Change FlagChanged "zurückgenommen"
            Expect.equal outcome.Previous { Color = purple; Name = Some "Lena" } "Vorzustand für Rückgängig"
            Expect.equal ((entryOf lena).FlagColor, (entryOf lena).FlagName) (None, None) "Zustand ohne Flag"
            Expect.equal probe.Calls [ ynabLena, None ] "Push mit null als Farbe"

            let untouched = withdraw probe.Push marked |> unwrap
            Expect.equal untouched.Change FlagUnchanged "Markiert ist keine Rückfrage Lena"
            Expect.equal (entryOf marked).FlagColor (Some "orange") "das andere Flag bleibt"

        testCase "Zuweisen überschreibt ein anderes Flag, Rückgängig stellt Farbe und Name wieder her" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedLinked account (Some "orange") (Some "Markiert")
            let probe = okProbe ()

            let outcome = assign probe.Push purple entryId |> unwrap
            Expect.equal outcome.Previous { Color = Some "orange"; Name = Some "Markiert" } "Vorzustand aus der Antwort"
            Expect.equal (entryOf entryId).FlagName (Some "Lena") "überschrieben"

            let restored = restoreFlag at probe.Push entryId outcome.Previous |> Async.RunSynchronously |> unwrap
            Expect.equal restored.Change FlagChanged "zurückgesetzt"
            let entry = entryOf entryId
            Expect.equal (entry.FlagColor, entry.FlagName) (Some "orange", Some "Markiert") "Farbe und Name wie vorher"
            Expect.equal probe.Calls [ ynabId, purple; ynabId, Some "orange" ] "der Push sendet nur die Farbe"

        testCase "ohne gesetzte Farbe lehnt der Befehl ab, es entsteht kein Ereignis" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None None
            let probe = okProbe ()
            Expect.equal (assign probe.Push None entryId) (Error lenaColorMissingReason) "ohne Farbe"
            Expect.equal (assign probe.Push (Some "pink") entryId) (Error lenaColorMissingReason) "keine YNAB-Farbe"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "eine in YNAB gelöschte oder unbekannte Buchung wird abgelehnt" <| fun () ->
            let account = freshAccount ()
            let deleted, _ = seedLinked account None None
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let probe = okProbe ()
            match assign probe.Push purple deleted with
            | Error message -> Expect.stringContains message "gelöscht" "Grund"
            | Ok _ -> failtest "gelöschte Buchung muss abgelehnt werden"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal (eventTypesOf deleted) [ "EntryObservedInYnab"; "EntryDeletionObservedInYnab" ] "kein Flag-Ereignis"
            Expect.equal (assign probe.Push purple (newEntryId ())) (Error "Buchung nicht gefunden") "unbekannt"

        testCase "Reihenfolge: das Ereignis steht im Log, bevor der Push läuft; Erfolg erzeugt kein weiteres" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None None
            let probe = okProbe ()
            let mutable seenAtPush : string list = []
            probe.OnCall <- fun () -> seenAtPush <- eventTypesOf entryId
            assign probe.Push purple entryId |> unwrap |> ignore
            Expect.equal seenAtPush [ "EntryObservedInYnab"; "EntryFlagChangedInBb" ] "beim Push steht das Ereignis schon da"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab"; "EntryFlagChangedInBb" ] "danach kein weiteres"

        testCase "Push scheitert (Netz, 4xx, 429): Fehlschlag mit Grund und Baseline, das Flag bleibt in BB" <| fun () ->
            for error in
                [ YnabError.NetworkError "Verbindung weg"
                  YnabError.InvalidResponse "Bad request"
                  YnabError.RateLimitExceeded 60 ] do
                let account = freshAccount ()
                let entryId, _ = seedLinked account (Some "orange") (Some "Markiert")
                let probe = FlagProbe(Error error)
                let outcome = assign probe.Push purple entryId |> unwrap
                let reason =
                    match outcome.WriteBack with
                    | FlagSendFailed reason -> reason
                    | other -> failtest $"erwartet FlagSendFailed, war {other}"
                Expect.equal
                    (eventTypesOf entryId)
                    [ "EntryObservedInYnab"; "EntryFlagChangedInBb"; "EntryFlagWriteBackFailed" ]
                    "Änderung zuerst, dann der Fehlschlag"
                let entry = entryOf entryId
                Expect.equal (entry.FlagColor, entry.FlagName) (purple, Some "Lena") "BB behält das Flag"
                let failure = entry.FlagWriteBackFailure |> Option.get
                Expect.equal failure.Reason reason "Grund im Zustand"
                Expect.equal (failure.BaselineFlagColor, failure.BaselineFlagName) (Some "orange", Some "Markiert") "Baseline: der Stand davor"

        testCase "ohne YNAB-Verknüpfung: Ereignis und sofort der Fehlschlag mit dem Grund, kein Push" <| fun () ->
            let account = freshAccount ()
            let entryId = seedUnlinked account
            let probe = okProbe ()
            let outcome = assign probe.Push purple entryId |> unwrap
            Expect.equal outcome.WriteBack (FlagSendFailed flagNotLinkedReason) "noch nicht in YNAB verknüpft"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal
                (eventTypesOf entryId |> List.rev |> List.take 2 |> List.rev)
                [ "EntryFlagChangedInBb"; "EntryFlagWriteBackFailed" ]
                "Ereignis, dann Fehlschlag"

        testCase "eine weitere Geste bei offenem Fehlschlag behält die erste Baseline" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None None
            let failing = FlagProbe(Error (YnabError.NetworkError "eins"))
            assign failing.Push purple entryId |> unwrap |> ignore
            let failingAgain = FlagProbe(Error (YnabError.NetworkError "zwei"))
            withdraw failingAgain.Push entryId |> unwrap |> ignore
            let failure = (entryOf entryId).FlagWriteBackFailure |> Option.get
            Expect.equal (failure.BaselineFlagColor, failure.BaselineFlagName) (None, None) "erste Baseline bleibt"
            Expect.stringContains failure.Reason "zwei" "neuer Grund"

        testCase "retryFlagWriteBack mit Erfolg schreibt EntryFlagWrittenBackToYnab und sendet die Farbe des BB-Stands" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedLinked account None None
            let failing = FlagProbe(Error (YnabError.NetworkError "weg"))
            assign failing.Push purple entryId |> unwrap |> ignore
            let probe = okProbe ()
            let writeBack = retryFlagWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap
            Expect.equal writeBack FlagSent "angekommen"
            Expect.equal probe.Calls [ ynabId, purple ] "BB-Stand geht hinaus"
            Expect.isNone (entryOf entryId).FlagWriteBackFailure "Fehlschlag beendet"
            match (eventsOf entryId |> List.last).Event with
            | { Actor = UserInBb; Body = EntryFlagWrittenBackToYnab } -> ()
            | other -> failtest $"erwartet EntryFlagWrittenBackToYnab von UserInBb, war {other}"

        testCase "retryFlagWriteBack scheitert erneut: der Grund wird ersetzt, die Baseline bleibt" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account (Some "orange") (Some "Markiert")
            let first = FlagProbe(Error (YnabError.NetworkError "eins"))
            assign first.Push purple entryId |> unwrap |> ignore
            let second = FlagProbe(Error (YnabError.NetworkError "zwei"))
            let writeBack = retryFlagWriteBack at second.Push entryId |> Async.RunSynchronously |> unwrap
            match writeBack with
            | FlagSendFailed _ -> ()
            | other -> failtest $"erwartet FlagSendFailed, war {other}"
            let failure = (entryOf entryId).FlagWriteBackFailure |> Option.get
            Expect.stringContains failure.Reason "zwei" "neuer Grund"
            Expect.equal failure.BaselineFlagColor (Some "orange") "Baseline bleibt"

        testCase "retryFlagWriteBack ohne offenen Fehlschlag tut nichts" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None None
            let probe = okProbe ()
            let writeBack = retryFlagWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap
            Expect.equal writeBack FlagNotSent "nichts zu senden"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"

        testCase "Erfolg nach einem früheren Fehlschlag (neue Geste) beendet ihn" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None None
            let failing = FlagProbe(Error (YnabError.NetworkError "weg"))
            assign failing.Push purple entryId |> unwrap |> ignore
            let probe = okProbe ()
            withdraw probe.Push entryId |> unwrap |> ignore
            Expect.isNone (entryOf entryId).FlagWriteBackFailure "kein offener Fehlschlag mehr"

        testCase "die Zeile der Sicht trägt den Grund des Fehlschlags" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None None
            let failing = FlagProbe(Error (YnabError.NetworkError "weg"))
            assign failing.Push purple entryId |> unwrap |> ignore
            let entry = entryOf entryId
            let view = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab entry
            Expect.equal view.FlagWriteBackFailure (entry.FlagWriteBackFailure |> Option.map (fun f -> f.Reason)) "Grund an der Sicht"
            Expect.equal view.Flag AskLena "Kennzeichen Rückfrage Lena"
    ]

// ============================================
// Spiegel: observe mit Baseline (ADR 0056, Nachtrag)
// ============================================

let private observationOf (entry: Entry) (flagColor: string option) (flagName: string option) : YnabObservation = {
    YnabTransactionId = "ynab-x"
    AccountId = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000d1")
    ImportId = None
    Date = entry.Date
    Amount = entry.Amount
    Payee = entry.Payee
    Memo = entry.Memo
    Category = entry.Category
    Splits = entry.Splits
    SplitTransfers = []
    Cleared = entry.Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Deleted = false
    Approved = entry.Approved
    FlagColor = flagColor
    FlagName = flagName
}

let private contextOf (entry: Entry) : ObservationContext =
    { EntryId = entry.Id; AccountId = entry.AccountId; ObservedAt = at }

/// BB hat Lena zugewiesen, der Push ist gescheitert (Baseline: kein Flag).
let private entryWithOpenFailure () : Entry =
    let entryId = newEntryId ()
    let account = freshAccount ()
    let observed : EntryObservedInYnabPayload = {
        AccountId = account
        YnabTransactionId = "ynab-xfjcp-obs"
        ImportId = None
        Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        Amount = eur -12.00m
        Payee = Some "Rewe xfjcp"
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
    [ eventOn entryId YnabMirror (EntryObservedInYnab observed)
      eventOn entryId UserInBb (
          EntryFlagChangedInBb { OldFlagColor = None; OldFlagName = None; NewFlagColor = purple; NewFlagName = Some "Lena" })
      eventOn entryId UserInBb (
          EntryFlagWriteBackFailed { Reason = "weg"; BaselineFlagColor = None; BaselineFlagName = None }) ]
    |> replay
    |> Option.get

let private replayWith (entry: Entry) (events: LedgerEvent list) : Entry =
    events |> List.fold (fun state ev -> Some (apply state ev)) (Some entry) |> Option.get

[<Tests>]
let flagObserveTests =
    testList "Lena fragen: Spiegel mit Baseline" [

        testCase "YNAB zeigt den BB-Stand: der Fehlschlag endet, Actor YnabMirror, keine Beobachtung" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry purple (Some "Lena"))
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryFlagWrittenBackToYnab" ] "nur das Ende"
            Expect.isTrue (events |> List.forall (fun e -> e.Actor = YnabMirror)) "Actor YnabMirror"
            let after = replayWith entry events
            Expect.isNone after.FlagWriteBackFailure "beendet"
            Expect.equal (after.FlagColor, after.FlagName) (purple, Some "Lena") "Flag bleibt"

        testCase "YNAB zeigt noch die Baseline (anderes Feld geändert): kein Ereignis, Flag und Fehlschlag bleiben" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry None None)
            Expect.isEmpty events "die Baseline ist keine Änderung in YNAB"
            Expect.isSome entry.FlagWriteBackFailure "Fehlschlag bleibt offen"
            Expect.equal (entry.FlagColor, entry.FlagName) (purple, Some "Lena") "Flag in BB bleibt"

        testCase "YNAB zeigt ein drittes Flag: YNAB gewinnt, der Fehlschlag endet, die Beobachtung steht im Log" <| fun () ->
            let entry = entryWithOpenFailure ()
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "blue") (Some "Anders"))
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryFlagChangeObservedInYnab" ] "Beobachtung"
            let after = replayWith entry events
            Expect.isNone after.FlagWriteBackFailure "beendet durch die Beobachtung"
            Expect.equal (after.FlagColor, after.FlagName) (Some "blue", Some "Anders") "YNABs Stand gilt"

        testCase "ohne offenen Fehlschlag gewinnt YNAB wie bisher" <| fun () ->
            let entry = entryWithOpenFailure () |> fun e -> { e with FlagWriteBackFailure = None }
            let events = observe (contextOf entry) (Some entry) (observationOf entry None None)
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryFlagChangeObservedInYnab" ] "Beobachtung"

        testCase "ohne offenen Fehlschlag und unverändertem Flag entsteht nichts" <| fun () ->
            let entry = entryWithOpenFailure () |> fun e -> { e with FlagWriteBackFailure = None }
            Expect.isEmpty (observe (contextOf entry) (Some entry) (observationOf entry purple (Some "Lena"))) "Idempotenz"

        testCase "eine falsche Farbe in den Settings fällt beim nächsten Spiegel auf: YNAB meldet einen anderen Namen, das Kennzeichen verschwindet" <| fun () ->
            // BB schrieb Farbe "red" mit Name "Lena" (Push gelang, kein offener Fehlschlag). In YNAB
            // heißt "red" aber anders; die Beobachtung gewinnt und das Kennzeichen fällt weg.
            let entry = entryWithOpenFailure () |> fun e -> { e with FlagWriteBackFailure = None; FlagColor = Some "red" }
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "red") (Some "Anderer Name"))
            let after = replayWith entry events
            Expect.equal (ReadModel.flagOf after.FlagColor after.FlagName) Marked "nicht mehr Rückfrage Lena"
    ]

// ============================================
// Abfragen: Filter, Zähler, Historie
// ============================================

let private pageQueryOn (account: LedgerAccountId) : LedgerEntryPageQuery =
    let (LedgerAccountId id) = account
    {
        AccountId = Some id
        Page = 0
        PageSize = 100
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
        Sort = LedgerSort.defaultSort
    }

let private idsOf (page: LedgerEntryPage) : Set<Guid> = page.Entries |> List.map (fun e -> e.Id) |> Set.ofList

let private guidOf (EntryId id) = id

[<Tests>]
let flagQueryTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Lena fragen: Filter, Zähler und Historie" [

        testCase "OnlyAskLena liefert nur Buchungen mit Flag-Name Lena (getrimmt, ohne Groß-/Kleinschreibung)" <| fun () ->
            let account = freshAccount ()
            let lena, _ = seedLinked account purple (Some "Lena")
            let spaced, _ = seedLinked account (Some "blue") (Some "  lena ")
            let marked, _ = seedLinked account (Some "orange") (Some "Markiert")
            let colorOnly, _ = seedLinked account purple None
            let plain, _ = seedLinked account None None

            let all = getLedgerEntriesPage (pageQueryOn account) |> Async.RunSynchronously
            Expect.equal (Set.count (idsOf all)) 5 "ohne Chip alle fünf"

            let page = getLedgerEntriesPage { pageQueryOn account with OnlyAskLena = true } |> Async.RunSynchronously
            Expect.equal (idsOf page) (set [ guidOf lena; guidOf spaced ]) "nur Name Lena"
            Expect.equal page.TotalCount 2 "Trefferzahl"
            Expect.isFalse (idsOf page |> Set.contains (guidOf marked)) "anderes Flag zählt nicht"
            Expect.isFalse (idsOf page |> Set.contains (guidOf colorOnly)) "nur Farbe zählt nicht"
            Expect.isFalse (idsOf page |> Set.contains (guidOf plain)) "ohne Flag zählt nicht"

        testCase "AskLenaCount zählt unabhängig vom Chip, nur im Konto und ohne gelöschte" <| fun () ->
            let account = freshAccount ()
            let other = freshAccount ()
            seedLinked account purple (Some "Lena") |> ignore
            seedLinked account purple (Some "Lena") |> ignore
            seedLinked other purple (Some "Lena") |> ignore
            let deleted, _ = seedLinked account purple (Some "Lena")
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously

            let without = getLedgerEntriesPage (pageQueryOn account) |> Async.RunSynchronously
            let withChip = getLedgerEntriesPage { pageQueryOn account with OnlyAskLena = true } |> Async.RunSynchronously
            Expect.equal without.AskLenaCount 2 "zwei im Konto, gelöschte nicht gezählt"
            Expect.equal withChip.AskLenaCount 2 "unabhängig vom Chip"

        testCase "Und-Verknüpfung: OnlyAskLena mit Zu prüfen und mit dem Konto" <| fun () ->
            let account = freshAccount ()
            let other = freshAccount ()
            let date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
            let reviewLena, _ = seedFlagged account date false purple (Some "Lena")
            seedFlagged account date true purple (Some "Lena") |> ignore
            seedFlagged account date false (Some "orange") (Some "Markiert") |> ignore
            let otherAccountLena, _ = seedFlagged other date false purple (Some "Lena")

            let page = getLedgerEntriesPage { pageQueryOn account with OnlyAskLena = true; OnlyNeedsReview = true } |> Async.RunSynchronously
            Expect.equal (idsOf page) (set [ guidOf reviewLena ]) "Lena und zu prüfen"
            Expect.isFalse (idsOf page |> Set.contains (guidOf otherAccountLena)) "anderes Konto bleibt draußen"

        testCase "die Payee-Vorschläge gehen mit dem Chip durch denselben Filter" <| fun () ->
            let account = freshAccount ()
            let lena, _ = seedLinked account purple (Some "Lena")
            seedLinked account None None |> ignore
            let (LedgerAccountId raw) = account
            let query : LedgerPayeeSuggestionQuery = {
                AccountId = Some raw
                IncludeDeleted = false
                OnlyNeedsReview = false
                OnlyAskLena = true
                WithoutCategory = false
                CategoryIds = []
                DateFrom = None
                DateTo = None
                Memo = None
                AmountMin = None
                AmountMax = None
            }
            let suggestions = getLedgerPayeeSuggestions query |> Async.RunSynchronously
            Expect.equal (suggestions |> List.map (fun s -> s.Count)) [ 1 ] "nur die Lena-Buchung zählt"
            ignore lena

        testCase "Historie: Lena gefragt, Rückfrage Lena zurückgenommen, Flag in BB geändert, Herkunft In BB geändert" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None None
            let probe = okProbe ()
            assign probe.Push purple entryId |> unwrap |> ignore
            withdraw probe.Push entryId |> unwrap |> ignore
            // Rückgängig auf ein anderes Flag als Vorzustand.
            restoreFlag at probe.Push entryId { Color = Some "orange"; Name = Some "Markiert" } |> Async.RunSynchronously |> unwrap |> ignore
            let failing = FlagProbe(Error (YnabError.NetworkError "weg"))
            assign failing.Push purple entryId |> unwrap |> ignore
            retryFlagWriteBack at probe.Push entryId |> Async.RunSynchronously |> unwrap |> ignore

            let lines = getEntryHistory (guidOf entryId) |> Async.RunSynchronously |> List.map (fun l -> l.Description)
            Expect.equal
                (lines |> List.skip 1 |> List.map (fun d -> if d.StartsWith "Flag nicht in YNAB" then "Flag nicht in YNAB" else d))
                [ "Lena gefragt"
                  "Rückfrage Lena zurückgenommen"
                  "Flag in BB geändert"
                  "Lena gefragt"
                  "Flag nicht in YNAB"
                  "Flag in YNAB angekommen" ]
                "Sätze der Historie"
            let changed = getEntryHistory (guidOf entryId) |> Async.RunSynchronously |> List.item 1
            Expect.equal changed.Origin DecidedInBb "Herkunft"

        testCase "eine Beobachtung aus YNAB nach der Änderung erscheint als eigene Zeile" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedLinked account None None
            let probe = okProbe ()
            assign probe.Push purple entryId |> unwrap |> ignore
            let entry = entryOf entryId
            let events = observe (contextOf entry) (Some entry) (observationOf entry (Some "blue") (Some "Anders"))
            appendAndApply events |> Async.RunSynchronously
            let lines = getEntryHistory (guidOf entryId) |> Async.RunSynchronously |> List.map (fun l -> l.Description)
            Expect.equal (lines |> List.skip 1) [ "Lena gefragt"; "Flag in YNAB gesetzt" ] "BB-Änderung und danach die Beobachtung"
    ]

// ============================================
// Einstellung lena_flag_color
// ============================================

[<Tests>]
let lenaSettingTests =
    let testCase name body =
        testCase name (fun () ->
            global.Persistence.initializeDatabase ()
            initializeLedgerSchema ()
            // Die Einstellung und alle Flag-Buchungen früherer Fälle weg: der Vorschlag liest den Spiegel.
            saveLenaFlagColor None |> Async.RunSynchronously |> ignore
            use conn = global.Persistence.getConnection ()
            if conn.State <> System.Data.ConnectionState.Open then conn.Open()
            // Log, Zustand und Verknüpfungen zusammen leeren, sonst stimmt der Replay anderer Tests nicht mehr.
            for table in [ "ledger_entry_events"; "ledger_entries"; "ledger_entry_links" ] do
                use cmd = new Microsoft.Data.Sqlite.SqliteCommand($"DELETE FROM {table}", conn)
                cmd.ExecuteNonQuery() |> ignore
            body ())

    testSequenced
    <| testList "Lena fragen: Einstellung (ADR 0056, Punkt 1)" [

        testCase "die Farbe wird gespeichert und gelesen, None löscht sie" <| fun () ->
            Expect.equal (getLenaFlagSettings () |> Async.RunSynchronously).SavedColor None "anfangs keine"
            Expect.equal (saveLenaFlagColor purple |> Async.RunSynchronously) (Ok ()) "speichern"
            Expect.equal (getLenaFlagSettings () |> Async.RunSynchronously).SavedColor purple "gelesen"
            Expect.equal (saveLenaFlagColor None |> Async.RunSynchronously) (Ok ()) "löschen"
            Expect.equal (getLenaFlagSettings () |> Async.RunSynchronously).SavedColor None "wieder keine"

        testCase "nur die sechs YNAB-Farben werden gespeichert" <| fun () ->
            match saveLenaFlagColor (Some "pink") |> Async.RunSynchronously with
            | Error message -> Expect.stringContains message "pink" "nennt die Farbe"
            | Ok () -> failtest "pink ist keine YNAB-Farbe"
            Expect.equal (getLenaFlagSettings () |> Async.RunSynchronously).SavedColor None "nichts gespeichert"
            for color in ynabFlagColors do
                Expect.equal (saveLenaFlagColor (Some color) |> Async.RunSynchronously) (Ok ()) $"{color} ist erlaubt"

        testCase "die Vorbelegung nimmt die Farbe der jüngsten Buchung mit Flag-Name Lena" <| fun () ->
            let account = freshAccount ()
            let old = DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            let recent = DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc)
            let newest = DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
            seedFlagged account old true (Some "red") (Some "Lena") |> ignore
            seedFlagged account recent true (Some "blue") (Some "lena") |> ignore
            // Jünger, aber kein Lena-Flag: zählt nicht.
            seedFlagged account newest true (Some "green") (Some "Markiert") |> ignore
            let settings = getLenaFlagSettings () |> Async.RunSynchronously
            Expect.equal settings.SuggestedColor (Some "blue") "jüngste Lena-Buchung"
            Expect.equal settings.SavedColor None "Vorschlag ist nicht gespeichert"

        testCase "ohne Lena-Buchung gibt es keinen Vorschlag" <| fun () ->
            let account = freshAccount ()
            seedLinked account (Some "green") (Some "Markiert") |> ignore
            Expect.equal (getLenaFlagSettings () |> Async.RunSynchronously).SuggestedColor None "kein Vorschlag"

        testCase "gespeicherte Farbe und Vorschlag stehen nebeneinander" <| fun () ->
            let account = freshAccount ()
            seedLinked account (Some "blue") (Some "Lena") |> ignore
            saveLenaFlagColor purple |> Async.RunSynchronously |> ignore
            let settings = getLenaFlagSettings () |> Async.RunSynchronously
            Expect.equal (settings.SavedColor, settings.SuggestedColor) (purple, Some "blue") "beide"
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

let private sendWith (status: HttpStatusCode) (color: string option) =
    let handler = new FakeHandler(status, "{}")
    use client = new HttpClient(handler)
    let result = sendFlag client (YnabBudgetId "budget-1") "tx-1" color |> Async.RunSynchronously
    handler, result

[<Tests>]
let flagYnabClientTests =
    let keysOf (body: string) =
        match Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.field "transaction" (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value)) body with
        | Ok pairs -> pairs |> List.map fst
        | Error e -> failtest e

    testList "YnabClient Flag (Fake-Handler)" [

        testCase "Body trägt nur flag_color, mit der Farbe als Text" <| fun () ->
            let body = buildFlagBody purple
            Expect.equal (keysOf body) [ "flag_color" ] "nur flag_color"
            let value = Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.at [ "transaction"; "flag_color" ] Thoth.Json.Net.Decode.string) body
            Expect.equal value (Ok "purple") "purple"
            for forbidden in [ "flag_name"; "approved"; "cleared" ] do
                Expect.isFalse (body.Contains forbidden) $"kein {forbidden} im Body"

        testCase "ohne Flag geht flag_color als null hinaus" <| fun () ->
            let body = buildFlagBody None
            Expect.stringContains body "\"flag_color\": null" "wörtlich null"
            Expect.equal (keysOf body) [ "flag_color" ] "nur flag_color"

        testCase "Methode, Pfad und Body stimmen" <| fun () ->
            let handler, result = sendWith HttpStatusCode.OK purple
            Expect.equal result (Ok ()) "200 ist Erfolg"
            Expect.equal handler.Method HttpMethod.Put "einzelne Buchung ändern ist PUT"
            Expect.stringEnds handler.Url "/budgets/budget-1/transactions/tx-1" "Pfad"
            Expect.stringContains handler.Body "purple" "Body"

        testCase "Fehlerzuordnung 400, 401, 404, 429 wie sendReviewMark" <| fun () ->
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
            match sendFlag client (YnabBudgetId "b") "t" None |> Async.RunSynchronously with
            | Error (YnabError.NetworkError _) -> ()
            | other -> failtest $"erwartet Netzfehler, war {other}"
    ]
