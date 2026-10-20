module LedgerReviewMarkBatchTests

// "Geprüft" für eine ganze Auswahl, ein Sammel-PATCH (ledger-nhwrt, ADR 0050).
//
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall), mit einem
//    injizierten Sammel-Push, der nie YNAB erreicht und seine Aufrufe zählt;
//  * der PATCH an YNAB gegen einen Fake-`HttpMessageHandler` (kein echter Aufruf).

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
let private at = DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc)

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

/// Eine mit YNAB verknüpfte Buchung auf einem frischen Konto.
let private seedLinkedOn (account: LedgerAccountId) (approved: bool) (cleared: ClearedState) : EntryId * string =
    let entryId = newEntryId ()
    let ynabId = $"ynab-batch-{Guid.NewGuid()}"
    let payload : EntryObservedInYnabPayload = {
        AccountId = account
        YnabTransactionId = ynabId
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
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab payload) ] |> Async.RunSynchronously
    entryId, ynabId

let private freshAccount () = LedgerAccountId (Guid.NewGuid())

let private seedUnlinked (account: LedgerAccountId) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        eventOn entryId QuickAdd (
            CashExpenseAdded {
                AccountId = account
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

let private entryOf (entryId: EntryId) : Entry = loadEntry entryId |> Async.RunSynchronously |> Option.get

let private eventTypesOf (entryId: EntryId) : string list =
    loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)

let private guidOf (EntryId id) = id

/// Ein Sammel-Push, der jeden Aufruf aufzeichnet und ein festes Ergebnis liefert.
type private BatchProbe(result: YnabResult<string list>) =
    member val Calls : ReviewMarkBatchItem list list = [] with get, set
    member this.Push : ReviewMarkBatchPush =
        fun items ->
            async {
                this.Calls <- this.Calls @ [ items ]
                return result
            }

let private okProbe () = BatchProbe(Ok [])

let private unwrap (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"Befehl sollte gelingen, war: {e}"

let private pageQueryOn (account: LedgerAccountId) : LedgerEntryPageQuery =
    let (LedgerAccountId id) = account
    {
        AccountId = Some id
        Page = 3
        PageSize = 10
        LocallyRemoved = 4
        IncludeDeleted = false
        OnlyNeedsReview = true
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

[<Tests>]
let batchCommandTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Ledger Prüfvermerk für die Auswahl (Befehle)" [

        testCase "Picked mit drei Buchungen: drei Changed-Ereignisse und genau ein PATCH mit drei Buchungen" <| fun () ->
            let account = freshAccount ()
            let seeded = [ for _ in 1..3 -> seedLinkedOn account false Uncleared ]
            let probe = okProbe ()
            let ids = seeded |> List.map (fst >> guidOf)
            let outcome = setReviewMarkBatch at probe.Push (BatchPicked ids) |> Async.RunSynchronously |> unwrap

            Expect.equal (List.length probe.Calls) 1 "genau ein Sammel-PATCH"
            Expect.equal (List.length probe.Calls.Head) 3 "mit drei Buchungen"
            Expect.equal
                (probe.Calls.Head |> List.map (fun i -> i.YnabTransactionId) |> List.sort)
                (seeded |> List.map snd |> List.sort)
                "die YNAB-Ids der drei Buchungen"
            for entryId, _ in seeded do
                Expect.equal
                    (eventTypesOf entryId)
                    [ "EntryObservedInYnab"; "EntryReviewMarkChangedInBb" ]
                    "ein eigenes Changed-Ereignis je Buchung, kein Sammel-Ereignis"
                let e = entryOf entryId
                Expect.isTrue e.Approved "approved gesetzt"
                Expect.equal e.Cleared Cleared "cleared bestätigt"
            Expect.equal (List.length outcome.Changed) 3 "drei geändert"
            Expect.equal outcome.UnchangedCount 0 "keine ohne Änderung"
            Expect.equal outcome.FailedCount 0 "keine gescheitert"
            for change in outcome.Changed do
                Expect.equal change.WriteBack ReviewMarkSent "gesendet"
                Expect.equal change.Previous { IsApproved = false; ClearedState = ReviewUncleared } "Vorzustand"

        testCase "AllExcept löst Filter und Ausnahmen auf dem Server auf, auch nie geladene Buchungen" <| fun () ->
            let account = freshAccount ()
            // 25 Buchungen bei Seitengröße 10: der Client hätte höchstens eine Seite geladen.
            let seeded = [ for _ in 1..25 -> seedLinkedOn account false Uncleared ]
            let kept = seeded |> List.take 2 |> List.map fst
            let probe = okProbe ()
            let selection = BatchAllExcept (pageQueryOn account, kept |> List.map guidOf)
            let outcome = setReviewMarkBatch at probe.Push selection |> Async.RunSynchronously |> unwrap

            Expect.equal (List.length outcome.Changed) 23 "alle 25 minus die zwei Ausnahmen"
            Expect.equal (List.length probe.Calls.Head) 23 "ein PATCH mit 23 Buchungen"
            for entryId in kept do
                Expect.isFalse (entryOf entryId).Approved "Ausnahme bleibt ungeprüft"
                Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "Ausnahme ohne Ereignis"
            for entryId, _ in seeded |> List.skip 2 do
                Expect.isTrue (entryOf entryId).Approved "alle übrigen sind geprüft"

        testCase "AllExcept nimmt nur den Treffer des Filters, nicht andere Konten" <| fun () ->
            let account = freshAccount ()
            let other = freshAccount ()
            let inHit = seedLinkedOn account false Uncleared |> fst
            let outside = seedLinkedOn other false Uncleared |> fst
            let probe = okProbe ()
            setReviewMarkBatch at probe.Push (BatchAllExcept (pageQueryOn account, []))
            |> Async.RunSynchronously
            |> unwrap
            |> ignore
            Expect.isTrue (entryOf inHit).Approved "Treffer geprüft"
            Expect.isFalse (entryOf outside).Approved "anderes Konto unberührt"

        testCase "eine bereits geprüfte Buchung ist NoChange: kein Ereignis, nicht im PATCH" <| fun () ->
            let account = freshAccount ()
            let done_, _ = seedLinkedOn account true Cleared
            let todo, todoYnab = seedLinkedOn account false Uncleared
            let probe = okProbe ()
            let outcome =
                setReviewMarkBatch at probe.Push (BatchPicked [ guidOf done_; guidOf todo ])
                |> Async.RunSynchronously
                |> unwrap
            Expect.equal outcome.UnchangedCount 1 "eine ohne Änderung"
            Expect.equal (List.length outcome.Changed) 1 "eine geändert"
            Expect.equal (eventTypesOf done_) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.equal (probe.Calls.Head |> List.map (fun i -> i.YnabTransactionId)) [ todoYnab ] "nur die geänderte im PATCH"

        testCase "eine in YNAB gelöschte Buchung scheitert ohne Ereignis, die übrigen werden verarbeitet" <| fun () ->
            let account = freshAccount ()
            let deleted, deletedYnab = seedLinkedOn account false Uncleared
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let good, _ = seedLinkedOn account false Uncleared
            let probe = okProbe ()
            let outcome =
                setReviewMarkBatch at probe.Push (BatchPicked [ guidOf deleted; guidOf good ])
                |> Async.RunSynchronously
                |> unwrap
            Expect.equal outcome.FailedCount 1 "eine gescheitert"
            Expect.equal (List.length outcome.Changed) 1 "die andere geändert"
            Expect.equal (eventTypesOf deleted) [ "EntryObservedInYnab"; "EntryDeletionObservedInYnab" ] "kein Ereignis"
            Expect.isFalse
                (probe.Calls.Head |> List.exists (fun i -> i.YnabTransactionId = deletedYnab))
                "nicht im PATCH"

        testCase "eine nicht mit YNAB verknüpfte Buchung scheitert ohne Ereignis" <| fun () ->
            let account = freshAccount ()
            let unlinked = seedUnlinked account
            let good, _ = seedLinkedOn account false Uncleared
            let probe = okProbe ()
            let outcome =
                setReviewMarkBatch at probe.Push (BatchPicked [ guidOf unlinked; guidOf good ])
                |> Async.RunSynchronously
                |> unwrap
            Expect.equal outcome.FailedCount 1 "eine gescheitert"
            Expect.equal (eventTypesOf unlinked) [ "CashExpenseAdded" ] "kein Ereignis"
            Expect.equal (List.length probe.Calls.Head) 1 "nur die verknüpfte im PATCH"

        testCase "Reconciled steht im PATCH nur mit approved, ohne cleared" <| fun () ->
            let account = freshAccount ()
            let reconciled, _ = seedLinkedOn account false Reconciled
            let uncleared, _ = seedLinkedOn account false Uncleared
            let probe = okProbe ()
            setReviewMarkBatch at probe.Push (BatchPicked [ guidOf reconciled; guidOf uncleared ])
            |> Async.RunSynchronously
            |> unwrap
            |> ignore
            let items = probe.Calls.Head
            let reconciledItem = items |> List.find (fun i -> i.Cleared.IsNone)
            Expect.isTrue reconciledItem.Approved "approved gesetzt"
            Expect.equal (items |> List.filter (fun i -> i.Cleared = Some SendCleared) |> List.length) 1 "die offene bekommt cleared"
            Expect.equal (entryOf reconciled).Cleared Reconciled "Reconciled bleibt"

        testCase "ein Netzfehler des Sammel-PATCH setzt bei jeder Buchung WriteBackFailed mit dem Grund" <| fun () ->
            let account = freshAccount ()
            let seeded = [ for _ in 1..3 -> seedLinkedOn account false Uncleared ]
            let probe = BatchProbe(Error (YnabError.NetworkError "Timeout"))
            let outcome =
                setReviewMarkBatch at probe.Push (BatchPicked (seeded |> List.map (fst >> guidOf)))
                |> Async.RunSynchronously
                |> unwrap
            for entryId, _ in seeded do
                Expect.equal
                    (eventTypesOf entryId)
                    [ "EntryObservedInYnab"; "EntryReviewMarkChangedInBb"; "EntryReviewMarkWriteBackFailed" ]
                    "Changed bleibt, dazu Failed"
                let reason = (entryOf entryId).ReviewMarkWriteBackFailure
                Expect.isSome reason "Grund am Zustand"
                Expect.stringContains reason.Value "Timeout" "Grund nennt den Fehler"
                Expect.isTrue (entryOf entryId).Approved "Prüfvermerk bleibt in BB gesetzt"
            for change in outcome.Changed do
                match change.WriteBack with
                | ReviewMarkSendFailed _ -> ()
                | other -> failtest $"erwartet SendFailed, war {other}"

        testCase "HTTP 429 trifft alle Buchungen des PATCH mit demselben Grund" <| fun () ->
            let account = freshAccount ()
            let seeded = [ for _ in 1..2 -> seedLinkedOn account false Uncleared ]
            let probe = BatchProbe(Error (YnabError.RateLimitExceeded 60))
            setReviewMarkBatch at probe.Push (BatchPicked (seeded |> List.map (fst >> guidOf)))
            |> Async.RunSynchronously
            |> unwrap
            |> ignore
            let reasons = seeded |> List.map (fun (e, _) -> (entryOf e).ReviewMarkWriteBackFailure)
            Expect.allEqual reasons reasons.Head "derselbe Grund"
            Expect.stringContains reasons.Head.Value "Limit" "YNAB bremst"

        testCase "nennt die Antwort einzelne nicht gespeicherte Buchungen, scheitern nur diese" <| fun () ->
            let account = freshAccount ()
            let ok1, _ = seedLinkedOn account false Uncleared
            let bad, badYnab = seedLinkedOn account false Uncleared
            let probe = BatchProbe(Ok [ badYnab ])
            let outcome =
                setReviewMarkBatch at probe.Push (BatchPicked [ guidOf ok1; guidOf bad ])
                |> Async.RunSynchronously
                |> unwrap
            Expect.isNone (entryOf ok1).ReviewMarkWriteBackFailure "gespeicherte Buchung ohne Fehlschlag"
            Expect.isSome (entryOf bad).ReviewMarkWriteBackFailure "nicht gespeicherte Buchung mit Fehlschlag"
            Expect.equal
                (outcome.Changed |> List.map (fun c -> c.WriteBack = ReviewMarkSent) |> List.sort)
                [ false; true ]
                "eine gesendet, eine gescheitert"

        testCase "ein Stapel ohne geänderte Buchung schickt keinen PATCH" <| fun () ->
            let account = freshAccount ()
            let done_, _ = seedLinkedOn account true Cleared
            let deleted, _ = seedLinkedOn account false Uncleared
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let probe = okProbe ()
            let outcome =
                setReviewMarkBatch at probe.Push (BatchPicked [ guidOf done_; guidOf deleted ])
                |> Async.RunSynchronously
                |> unwrap
            Expect.isEmpty probe.Calls "kein PATCH"
            Expect.isEmpty outcome.Changed "nichts geändert"
            Expect.equal (outcome.UnchangedCount, outcome.FailedCount) (1, 1) "eine ohne Änderung, eine gescheitert"

        testCase "Sammel-Rückgängig stellt den Vorzustand her, fasst NoChange nicht an, ein PATCH" <| fun () ->
            let account = freshAccount ()
            // Vorzustände: a (true, Uncleared), b (false, Cleared), c (false, Reconciled)
            let a, aYnab = seedLinkedOn account true Uncleared
            let b, bYnab = seedLinkedOn account false Cleared
            let c, cYnab = seedLinkedOn account false Reconciled
            let batch =
                setReviewMarkBatch at (okProbe ()).Push (BatchPicked [ guidOf a; guidOf b; guidOf c ])
                |> Async.RunSynchronously
                |> unwrap
            let restoreItems =
                batch.Changed |> List.map (fun ch -> { EntryId = ch.EntryId; Previous = ch.Previous })
            // d steht schon im Vorzustand: bleibt unangetastet
            let d, _ = seedLinkedOn account true Cleared
            let dItem = { EntryId = guidOf d; Previous = { IsApproved = true; ClearedState = ReviewCleared } }

            let undoProbe = okProbe ()
            let undo =
                restoreReviewMarkBatch at undoProbe.Push (restoreItems @ [ dItem ])
                |> Async.RunSynchronously
                |> unwrap

            Expect.equal (List.length undoProbe.Calls) 1 "genau ein PATCH"
            Expect.equal (List.length undo.Changed) 3 "drei zurückgestellt"
            Expect.equal undo.UnchangedCount 1 "d ohne Änderung"
            Expect.equal ((entryOf a).Approved, (entryOf a).Cleared) (true, Uncleared) "approved true, Uncleared exakt wieder"
            Expect.equal ((entryOf b).Approved, (entryOf b).Cleared) (false, Cleared) "false, Cleared"
            Expect.equal ((entryOf c).Approved, (entryOf c).Cleared) (false, Reconciled) "Reconciled bleibt"
            Expect.equal (eventTypesOf d) [ "EntryObservedInYnab" ] "NoChange ohne Ereignis"
            let sent ynabId = undoProbe.Calls.Head |> List.find (fun i -> i.YnabTransactionId = ynabId)
            Expect.equal ((sent aYnab).Approved, (sent aYnab).Cleared) (true, Some SendUncleared) "a: approved und Uncleared"
            Expect.equal ((sent bYnab).Approved, (sent bYnab).Cleared) (false, None) "b: nur approved, cleared unverändert"
            Expect.equal ((sent cYnab).Approved, (sent cYnab).Cleared) (false, None) "c: Reconciled wird nie gesendet"
    ]

// ============================================
// PATCH an YNAB (Fake-Handler, kein echter Aufruf)
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

let private items : ReviewMarkBatchItem list = [
    { YnabTransactionId = "tx-1"; Approved = true; Cleared = Some SendCleared }
    { YnabTransactionId = "tx-2"; Approved = true; Cleared = None }
]

let private sendWith (status: HttpStatusCode) (responseBody: string) =
    let handler = new FakeHandler(status, responseBody)
    use client = new HttpClient(handler)
    let result = sendReviewMarkBatch client (YnabBudgetId "budget-1") items |> Async.RunSynchronously
    handler, result

[<Tests>]
let batchYnabClientTests =
    testList "YnabClient Prüfvermerk-Sammel-PATCH (Fake-Handler)" [

        testCase "Methode, Pfad und Body: PATCH auf transactions mit id je Eintrag" <| fun () ->
            let handler, result = sendWith HttpStatusCode.OK "{\"data\":{\"transaction_ids\":[\"tx-1\",\"tx-2\"],\"server_knowledge\":1}}"
            Expect.equal result (Ok []) "200 mit allen Ids ist Erfolg ohne Abgelehnte"
            Expect.equal handler.CallCount 1 "genau ein Aufruf für den ganzen Stapel"
            Expect.equal handler.Method HttpMethod.Patch "Sammel-Route ist PATCH (YNAB-OpenAPI updateTransactions)"
            Expect.stringEnds handler.Url "/budgets/budget-1/transactions" "Pfad ohne Transaction-Id"
            let parsed =
                Thoth.Json.Net.Decode.fromString
                    (Thoth.Json.Net.Decode.field "transactions" (Thoth.Json.Net.Decode.list (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value)))
                    handler.Body
            match parsed with
            | Ok [ first; second ] ->
                Expect.equal (first |> List.map fst |> List.sort) [ "approved"; "cleared"; "id" ] "Felder mit cleared"
                Expect.equal (second |> List.map fst |> List.sort) [ "approved"; "id" ] "ohne cleared nur id und approved"
            | other -> failtest $"zwei Einträge erwartet, war {other}"
            Expect.stringContains handler.Body "\"cleared\": \"cleared\"" "cleared geht als cleared hinaus"
            Expect.isFalse (handler.Body.Contains "reconciled") "nie reconciled"

        testCase "eine in transaction_ids fehlende Buchung gilt als nicht gespeichert" <| fun () ->
            let _, result = sendWith HttpStatusCode.OK "{\"data\":{\"transaction_ids\":[\"tx-1\"],\"server_knowledge\":1}}"
            Expect.equal result (Ok [ "tx-2" ]) "nur tx-2 fehlt"

        testCase "nennt die Antwort keine Ids, gilt der Stapel als gespeichert" <| fun () ->
            let _, result = sendWith HttpStatusCode.OK "{}"
            Expect.equal result (Ok []) "nichts als abgelehnt benannt"

        testCase "Fehlerzuordnung 400, 401, 429 und ein Netzfehler" <| fun () ->
            let result status = sendWith status "{}" |> snd
            match result HttpStatusCode.BadRequest with
            | Error (YnabError.InvalidResponse _) -> ()
            | other -> failtest $"400: {other}"
            match result HttpStatusCode.Unauthorized with
            | Error (YnabError.Unauthorized _) -> ()
            | other -> failtest $"401: {other}"
            match result (enum<HttpStatusCode> 429) with
            | Error (YnabError.RateLimitExceeded _) -> ()
            | other -> failtest $"429: {other}"
            let handler =
                { new HttpMessageHandler() with
                    member _.SendAsync(_, _) = raise (HttpRequestException "kaputt") }
            use client = new HttpClient(handler)
            match sendReviewMarkBatch client (YnabBudgetId "b") items |> Async.RunSynchronously with
            | Error (YnabError.NetworkError _) -> ()
            | other -> failtest $"erwartet Netzfehler, war {other}"
    ]
