module LedgerCategoryBatchTests

// Kategorie für eine ganze Auswahl, ein Sammel-PATCH (ledger-w29y6, ADR 0050, ADR 0055).
//
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall), mit einem
//    injizierten Sammel-Push, der nie YNAB erreicht und seine Aufrufe zählt;
//  * die Schreib-Sperre gegen den Spiegel-Lauf;
//  * JSON-Vertrag und PATCH an YNAB gegen einen Fake-`HttpMessageHandler`.

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
let private at = DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc)

let private catRef (id: Guid) (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId id; CategoryName = name }

let private catA = catRef (Guid.Parse "0195c000-0000-7000-8000-00000029a001") "Lebensmittel w29y6"
let private catB = catRef (Guid.Parse "0195c000-0000-7000-8000-00000029a002") "Drogerie w29y6"
let private catC = catRef (Guid.Parse "0195c000-0000-7000-8000-00000029a003") "Freizeit w29y6"

let private idOf (c: CategoryRef) = c.CategoryId
let private guidOf (EntryId id) = id

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

let private freshAccount () = LedgerAccountId (Guid.NewGuid())

let private line (c: CategoryRef) (amount: decimal) : TransactionSplit =
    { Target = ToCategory (c.CategoryId, c.CategoryName); Amount = eur amount; Memo = None }

let private seedCategories () =
    upsertCategories [
        for c in [ catA; catB; catC ] ->
            { Id = c.CategoryId; Name = c.CategoryName; GroupName = "Alltag"; Available = None; Hidden = false }
    ]
    |> Async.RunSynchronously

/// Eine mit YNAB verknüpfte Buchung auf dem Konto, mit Kategorie und optionalen Split-Zeilen.
let private seedLinked (account: LedgerAccountId) (category: CategoryRef option) (splits: TransactionSplit list) : EntryId * string =
    let entryId = newEntryId ()
    let ynabId = $"ynab-w29y6-{Guid.NewGuid()}"
    let payload : EntryObservedInYnabPayload = {
        AccountId = account
        YnabTransactionId = ynabId
        ImportId = None
        Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        Amount = eur -12.00m
        Payee = Some "Rewe"
        Memo = None
        Category = category
        Splits = splits
        Cleared = Cleared
        TransferAccountId = None
        TransferYnabTransactionId = None
        Approved = true
        FlagColor = None
        FlagName = None
    }
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab payload) ] |> Async.RunSynchronously
    entryId, ynabId

let private seedUnlinked (account: LedgerAccountId) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        eventOn entryId QuickAdd (
            CashExpenseAdded {
                AccountId = account
                Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -3.00m
                Payee = Some "Kiosk w29y6"
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

/// Ein Sammel-Push, der jeden Aufruf aufzeichnet und ein festes Ergebnis liefert.
type private BatchProbe(result: YnabResult<string list>) =
    member val Calls : CategoryBatchItem list list = [] with get, set
    member this.Push : CategoryBatchPush =
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

let private runBatch (push: CategoryBatchPush) (selection: ReviewMarkBatchSelection) (target: CategoryRef option) =
    setEntryCategoryBatch at push selection (target |> Option.map idOf) |> Async.RunSynchronously

let private pageQueryOn (account: LedgerAccountId) : LedgerEntryPageQuery =
    let (LedgerAccountId id) = account
    {
        AccountId = Some id
        Page = 3
        PageSize = 2
        LocallyRemoved = 4
        IncludeDeleted = false
        OnlyNeedsReview = false
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
let categoryBatchCommandTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            seedCategories ()
            body ())

    testSequenced
    <| testList "Ledger Kategorie für die Auswahl (Befehle)" [

        testCase "Picked mit vier Buchungen: ein Ereignis je geänderter, ein PATCH, Zähler mit Grund" <| fun () ->
            let account = freshAccount ()
            let changedA, ynabA = seedLinked account (Some catA) []
            let changedB, ynabB = seedLinked account None []
            let same, _ = seedLinked account (Some catB) []
            let split, _ = seedLinked account None [ line catA -5.00m; line catC -7.00m ]
            // Offener Split-Fehlschlag: YNAB hält noch den alten Split.
            let pendingSplit, _ = seedLinked account (Some catA) []
            appendAndApply [
                eventOn pendingSplit UserInBb (
                    EntryCategorizationWriteBackFailed {
                        Reason = "x"
                        BaselineCategory = None
                        BaselineSplits = [ line catA -5.00m; line catC -7.00m ]
                        YnabDeletedTransactionId = None
                    }
                )
            ]
            |> Async.RunSynchronously

            let probe = okProbe ()
            let ids = [ changedA; changedB; same; split; pendingSplit ] |> List.map guidOf
            let outcome = runBatch probe.Push (BatchPicked ids) (Some catB) |> unwrap

            Expect.equal (List.length probe.Calls) 1 "genau ein Sammel-PATCH"
            Expect.equal
                (probe.Calls.Head |> List.map (fun i -> i.YnabTransactionId) |> List.sort)
                ([ ynabA; ynabB ] |> List.sort)
                "nur die zwei geänderten"
            Expect.isTrue
                (probe.Calls.Head |> List.forall (fun i -> i.CategoryId = Some (idOf catB)))
                "mit der Ziel-Kategorie"
            Expect.equal (outcome.Changed |> List.map (fun c -> c.EntryId) |> List.sort) ([ changedA; changedB ] |> List.map guidOf |> List.sort) "Changed"
            Expect.equal outcome.UnchangedCount 1 "eine gleich Ziel"
            Expect.equal (outcome.Skipped |> List.sort) ([ SkipSplit; SkipSplitNotAdjusted ] |> List.sort) "Gründe der Übersprungenen"
            for id in [ changedA; changedB ] do
                Expect.equal (eventTypesOf id |> List.last) "EntryRecategorized" "ein Ereignis je geänderter"
                Expect.equal (entryOf id).Category (Some catB) "Kategorie gesetzt"
            for id in [ same; split ] do
                Expect.equal (eventTypesOf id) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.equal (eventTypesOf pendingSplit) [ "EntryObservedInYnab"; "EntryCategorizationWriteBackFailed" ] "kein neues Ereignis bei offenem Split-Fehlschlag"

        testCase "gelöschte Buchung zählt als übersprungen mit Grund" <| fun () ->
            let account = freshAccount ()
            let deleted, _ = seedLinked account (Some catA) []
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let probe = okProbe ()
            let outcome = runBatch probe.Push (BatchPicked [ guidOf deleted ]) (Some catB) |> unwrap
            Expect.equal outcome.Skipped [ SkipDeletedInYnab ] "gelöscht"
            Expect.isEmpty probe.Calls "kein PATCH ohne geänderte Buchung"

        testCase "AllExcept löst den ganzen Treffer auf, auch nie geladene Buchungen (Seitengröße kleiner)" <| fun () ->
            let account = freshAccount ()
            let seeded = [ for _ in 1..5 -> seedLinked account None [] ]
            let keptOut = fst seeded.Head
            let probe = okProbe ()
            let outcome = runBatch probe.Push (BatchAllExcept (pageQueryOn account, [ guidOf keptOut ])) (Some catA) |> unwrap
            Expect.equal (List.length outcome.Changed) 4 "vier von fünf, obwohl PageSize 2"
            Expect.equal (List.length probe.Calls.Head) 4 "ein PATCH mit vier"
            Expect.isNone (entryOf keptOut).Category "die Ausnahme bleibt"

        testCase "PATCH scheitert: je geänderter Buchung ein Fehlschlag mit Baseline, die Kategorien bleiben in BB" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account (Some catA) []
            let b, _ = seedLinked account None []
            let failing = BatchProbe(Error (YnabError.NetworkError "Verbindung weg"))
            let outcome = runBatch failing.Push (BatchPicked [ guidOf a; guidOf b ]) (Some catB) |> unwrap
            Expect.isTrue
                (outcome.Changed |> List.forall (fun c -> match c.WriteBack with CategorySendFailed _ -> true | _ -> false))
                "alle gescheitert"
            Expect.equal (entryOf a).Category (Some catB) "BB behält die Kategorie"
            let failureOf id = (entryOf id).CategorizationWriteBackFailure |> Option.get
            Expect.equal (failureOf a).BaselineCategory (Some catA) "Baseline a"
            Expect.isNone (failureOf b).BaselineCategory "Baseline b: ohne Kategorie"

        testCase "bei schon offenem Fehlschlag bleibt die erste Baseline" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account (Some catA) []
            let failing = BatchProbe(Error (YnabError.NetworkError "eins"))
            runBatch failing.Push (BatchPicked [ guidOf a ]) (Some catB) |> unwrap |> ignore
            let failingAgain = BatchProbe(Error (YnabError.NetworkError "zwei"))
            runBatch failingAgain.Push (BatchPicked [ guidOf a ]) (Some catC) |> unwrap |> ignore
            let failure = (entryOf a).CategorizationWriteBackFailure |> Option.get
            Expect.equal failure.BaselineCategory (Some catA) "erste Baseline"
            Expect.stringContains failure.Reason "zwei" "neuer Grund"

        testCase "von YNAB nicht genannte Ids werden einzeln Fehlschlag" <| fun () ->
            let account = freshAccount ()
            let a, ynabA = seedLinked account (Some catA) []
            let b, _ = seedLinked account (Some catA) []
            let probe = BatchProbe(Ok [ ynabA ])
            let outcome = runBatch probe.Push (BatchPicked [ guidOf a; guidOf b ]) (Some catB) |> unwrap
            let writeBackOf id = (outcome.Changed |> List.find (fun c -> c.EntryId = guidOf id)).WriteBack
            Expect.equal (writeBackOf a) (CategorySendFailed categoryNotSavedReason) "a: nicht gespeichert"
            Expect.equal (writeBackOf b) CategorySent "b: gespeichert"
            Expect.isSome (entryOf a).CategorizationWriteBackFailure "a: Fehlschlag im Log"
            Expect.isNone (entryOf b).CategorizationWriteBackFailure "b: kein Fehlschlag"

        testCase "nicht verknüpfte Buchung: Ereignis plus Fehlschlag, nicht im PATCH" <| fun () ->
            let account = freshAccount ()
            let unlinked = seedUnlinked account
            let linked, ynabLinked = seedLinked account None []
            let probe = okProbe ()
            let outcome = runBatch probe.Push (BatchPicked [ guidOf unlinked; guidOf linked ]) (Some catA) |> unwrap
            Expect.equal (probe.Calls.Head |> List.map (fun i -> i.YnabTransactionId)) [ ynabLinked ] "nur die verknüpfte im PATCH"
            Expect.equal
                (eventTypesOf unlinked)
                [ "CashExpenseAdded"; "EntryRecategorized"; "EntryCategorizationWriteBackFailed" ]
                "Ereignis plus Fehlschlag"
            let wb = (outcome.Changed |> List.find (fun c -> c.EntryId = guidOf unlinked)).WriteBack
            Expect.equal wb (CategorySendFailed categoryNotLinkedReason) "Grund wie im Einzelweg"

        testCase "Erfolg bei offenem Fehlschlag schreibt EntryCategorizationWrittenBackToYnab" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account (Some catA) []
            let failing = BatchProbe(Error (YnabError.NetworkError "x"))
            runBatch failing.Push (BatchPicked [ guidOf a ]) (Some catB) |> unwrap |> ignore
            let ok = okProbe ()
            runBatch ok.Push (BatchPicked [ guidOf a ]) (Some catC) |> unwrap |> ignore
            Expect.isNone (entryOf a).CategorizationWriteBackFailure "Fehlschlag beendet"
            Expect.equal (eventTypesOf a |> List.last) "EntryCategorizationWrittenBackToYnab" "Ereignis"

        testCase "unbekannte Ziel-Kategorie lehnt den ganzen Befehl ab, ohne Ereignis und ohne PATCH" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account (Some catA) []
            let probe = okProbe ()
            let result = setEntryCategoryBatch at probe.Push (BatchPicked [ guidOf a ]) (Some (YnabCategoryId (Guid.NewGuid()))) |> Async.RunSynchronously
            Expect.isTrue (Result.isError result) "abgelehnt"
            Expect.equal (eventTypesOf a) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein PATCH"

        testCase "die Antwort nennt die vorherige Kategorie, ein Sammel-Befehl je Gruppe stellt den Ausgangsstand her" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account (Some catA) []
            let b, _ = seedLinked account None []
            let probe = okProbe ()
            let outcome = runBatch probe.Push (BatchPicked [ guidOf a; guidOf b ]) (Some catC) |> unwrap
            let previousOf id = (outcome.Changed |> List.find (fun c -> c.EntryId = guidOf id)).Previous
            Expect.equal (previousOf a) (Some { CategoryId = idOf catA; CategoryName = catA.CategoryName }) "vorher A"
            Expect.isNone (previousOf b) "vorher ohne Kategorie"

            // Rücknahme je Gruppe gleicher vorheriger Kategorie.
            let undo = okProbe ()
            runBatch undo.Push (BatchPicked [ guidOf a ]) (Some catA) |> unwrap |> ignore
            runBatch undo.Push (BatchPicked [ guidOf b ]) None |> unwrap |> ignore
            Expect.equal (List.length undo.Calls) 2 "ein PATCH je Gruppe"
            Expect.equal (entryOf a).Category (Some catA) "a zurück"
            Expect.isNone (entryOf b).Category "b zurück zu ohne Kategorie"
            Expect.equal (eventTypesOf a |> List.filter ((=) "EntryRecategorized") |> List.length) 2 "zweites Ereignis je Buchung"
            Expect.equal undo.Calls.[1].Head.CategoryId None "null für ohne Kategorie"
    ]

// ============================================
// Schreib-Sperre gegen den Spiegel-Lauf
// ============================================

[<Tests>]
let categoryBatchWriteGuardTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            seedCategories ()
            body ())

    testSequenced
    <| testList "Ledger Kategorie für die Auswahl (Schreib-Sperre)" [

        testCase "ein Sammel-Befehl, der während eines Spiegel-Laufs startet, schreibt erst nach dem Lauf" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account (Some catA) []
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
            let mirrorTask = Async.StartAsTask(runMirrorGuarded at fetchers (YnabBudgetId "budget-w29y6-guard"))
            Expect.isTrue (fetchStarted.Task.Wait 5000) "der Lauf hängt im Abruf"

            let probe = okProbe ()
            let commandTask =
                Async.StartAsTask(setEntryCategoryBatch at probe.Push (BatchPicked [ guidOf a ]) (Some (idOf catB)))
            Threading.Thread.Sleep 300
            Expect.isFalse commandTask.IsCompleted "der Befehl wartet auf den Lauf"
            Expect.equal (eventTypesOf a) [ "EntryObservedInYnab" ] "noch kein Ereignis während des Laufs"
            Expect.isEmpty probe.Calls "noch kein Push"

            release.SetResult ()
            Expect.isTrue (mirrorTask.Wait 5000) "Lauf beendet"
            Expect.isTrue (commandTask.Wait 5000) "Befehl beendet"
            Expect.equal (eventTypesOf a) [ "EntryObservedInYnab"; "EntryRecategorized" ] "Ereignis erst nach dem Lauf"
            Expect.equal probe.Calls.Length 1 "ein Push"
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

let private items : CategoryBatchItem list = [
    { YnabTransactionId = "tx-1"; CategoryId = Some (idOf catA) }
    { YnabTransactionId = "tx-2"; CategoryId = None }
]

let private sendWith (status: HttpStatusCode) (responseBody: string) =
    let handler = new FakeHandler(status, responseBody)
    use client = new HttpClient(handler)
    let result = sendCategoryBatch client (YnabBudgetId "budget-1") items |> Async.RunSynchronously
    handler, result

[<Tests>]
let categoryBatchYnabClientTests =
    testList "YnabClient Kategorie-Sammel-PATCH (Fake-Handler)" [

        testCase "JSON-Vertrag: id und category_id je Eintrag, null für ohne Kategorie, sonst nichts" <| fun () ->
            let body = buildCategoryBatchBody items
            let parsed =
                Thoth.Json.Net.Decode.fromString
                    (Thoth.Json.Net.Decode.field "transactions" (Thoth.Json.Net.Decode.list (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value)))
                    body
            match parsed with
            | Ok [ first; second ] ->
                Expect.equal (first |> List.map fst |> List.sort) [ "category_id"; "id" ] "Felder"
                Expect.equal (second |> List.map fst |> List.sort) [ "category_id"; "id" ] "Felder ohne Kategorie"
            | other -> failtest $"zwei Einträge erwartet, war {other}"
            Expect.stringContains body (string (idOf catA |> fun (YnabCategoryId g) -> g)) "Kategorie-Id"
            Expect.stringContains body "\"category_id\": null" "null für ohne Kategorie"

        testCase "Methode, Pfad und Body: PATCH auf transactions, ein Aufruf" <| fun () ->
            let handler, result = sendWith HttpStatusCode.OK "{\"data\":{\"transaction_ids\":[\"tx-1\",\"tx-2\"],\"server_knowledge\":1}}"
            Expect.equal result (Ok []) "alle genannt"
            Expect.equal handler.CallCount 1 "ein Aufruf für den Stapel"
            Expect.equal handler.Method HttpMethod.Patch "Sammel-Route ist PATCH, der Einzelweg PUT"
            Expect.stringEnds handler.Url "/budgets/budget-1/transactions" "Pfad ohne Transaction-Id"

        testCase "eine in transaction_ids fehlende Buchung gilt als nicht gespeichert" <| fun () ->
            let _, result = sendWith HttpStatusCode.OK "{\"data\":{\"transaction_ids\":[\"tx-1\"],\"server_knowledge\":1}}"
            Expect.equal result (Ok [ "tx-2" ]) "nur tx-2 fehlt"

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
            match sendCategoryBatch client (YnabBudgetId "b") items |> Async.RunSynchronously with
            | Error (YnabError.NetworkError _) -> ()
            | other -> failtest $"erwartet Netzfehler, war {other}"
    ]
