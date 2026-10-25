module LedgerLenaFlagBatchTests

// Lena fragen für eine ganze Auswahl, ein Sammel-PATCH (ledger-y54gr, ADR 0050, ADR 0056).
//
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall), mit einem
//    injizierten Sammel-Push, der nie YNAB erreicht und seine Aufrufe zählt;
//  * JSON-Vertrag und PATCH an YNAB gegen einen Fake-`HttpMessageHandler`.

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
let private at = DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc)
let private guidOf (EntryId id) = id

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

let private freshAccount () = LedgerAccountId (Guid.NewGuid())

let private seedLinked (account: LedgerAccountId) (flagColor: string option) (flagName: string option) : EntryId * string =
    let entryId = newEntryId ()
    let ynabId = $"ynab-y54gr-{Guid.NewGuid()}"
    let payload : EntryObservedInYnabPayload = {
        AccountId = account
        YnabTransactionId = ynabId
        ImportId = None
        Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        Amount = eur -12.00m
        Payee = Some "Rewe y54gr"
        Memo = None
        Category = None
        Splits = []
        Cleared = Cleared
        TransferAccountId = None
        TransferYnabTransactionId = None
        Approved = true
        FlagColor = flagColor
        FlagName = flagName
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
                Payee = Some "Kiosk y54gr"
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
    member val Calls : FlagBatchItem list list = [] with get, set
    member this.Push : FlagBatchPush =
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

let private purple = Some "purple"

let private assignBatch (push: FlagBatchPush) (color: string option) (ids: EntryId list) =
    setLenaFlagBatch at push color (BatchPicked (ids |> List.map guidOf)) AssignLena |> Async.RunSynchronously

let private withdrawBatch (push: FlagBatchPush) (color: string option) (ids: EntryId list) =
    setLenaFlagBatch at push color (BatchPicked (ids |> List.map guidOf)) WithdrawLena |> Async.RunSynchronously

[<Tests>]
let lenaFlagBatchCommandTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Ledger Lena fragen für die Auswahl (Befehle)" [

        testCase "ein Ereignis je geänderter Buchung, eine schon bei Lena erzeugt keins und geht nicht in den PATCH" <| fun () ->
            let account = freshAccount ()
            let plain, ynabPlain = seedLinked account None None
            let marked, ynabMarked = seedLinked account (Some "red") (Some "Markiert")
            let already, _ = seedLinked account purple (Some lenaFlagName)

            let probe = okProbe ()
            let outcome = assignBatch probe.Push purple [ plain; marked; already ] |> unwrap

            Expect.equal (List.length probe.Calls) 1 "genau ein Sammel-PATCH"
            Expect.equal
                (probe.Calls.Head |> List.map (fun i -> i.YnabTransactionId) |> List.sort)
                ([ ynabPlain; ynabMarked ] |> List.sort)
                "nur die zwei geänderten"
            Expect.isTrue (probe.Calls.Head |> List.forall (fun i -> i.FlagColor = purple)) "mit der Lena-Farbe"
            Expect.equal
                (outcome.Changed |> List.map (fun c -> c.EntryId) |> List.sort)
                ([ plain; marked ] |> List.map guidOf |> List.sort)
                "Changed"
            Expect.equal outcome.UnchangedCount 1 "eine schon bei Lena"
            Expect.equal outcome.FailedCount 0 "keine gescheiterte"
            for id in [ plain; marked ] do
                Expect.equal (eventTypesOf id |> List.last) "EntryFlagChangedInBb" "ein Ereignis je geänderter"
                Expect.equal (entryOf id).FlagName (Some lenaFlagName) "Flag-Name Lena"
                Expect.equal (entryOf id).FlagColor purple "Farbe gesetzt"
            Expect.equal (eventTypesOf already) [ "EntryObservedInYnab" ] "kein Ereignis bei schon Lena"
            let markedChange = outcome.Changed |> List.find (fun c -> c.EntryId = guidOf marked)
            Expect.equal markedChange.Previous { Color = Some "red"; Name = Some "Markiert" } "Vorzustand fürs Rückgängig"
            Expect.isTrue (outcome.Changed |> List.forall (fun c -> c.WriteBack = FlagSent)) "alle gesendet"

        testCase "ohne gültige Farbe wird 'Lena fragen' für den ganzen Stapel abgelehnt, ohne Ereignis" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account None None
            let probe = okProbe ()
            for color in [ None; Some "magenta" ] do
                match assignBatch probe.Push color [ a ] with
                | Error reason -> Expect.equal reason lenaColorMissingReason "Grund"
                | Ok _ -> failtest "ohne Farbe muss abgelehnt werden"
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal (eventTypesOf a) [ "EntryObservedInYnab" ] "kein Ereignis"

        testCase "zurücknehmen löscht nur Lena-Rückfragen, ein anderes Flag bleibt, ohne Ereignis und ohne PATCH" <| fun () ->
            let account = freshAccount ()
            let lena, ynabLena = seedLinked account purple (Some lenaFlagName)
            let other, _ = seedLinked account (Some "red") (Some "Markiert")
            let none, _ = seedLinked account None None

            let probe = okProbe ()
            let outcome = withdrawBatch probe.Push purple [ lena; other; none ] |> unwrap

            Expect.equal (probe.Calls |> List.map (List.map (fun i -> i.YnabTransactionId, i.FlagColor))) [ [ ynabLena, None ] ] "ein PATCH, Farbe null"
            Expect.equal (outcome.Changed |> List.map (fun c -> c.EntryId)) [ guidOf lena ] "nur die Rückfrage"
            Expect.equal outcome.UnchangedCount 2 "die anderen zwei unverändert"
            Expect.equal (entryOf lena).FlagColor None "Flag weg"
            Expect.equal (entryOf other).FlagColor (Some "red") "anderes Flag bleibt"
            Expect.equal (eventTypesOf other) [ "EntryObservedInYnab" ] "kein Ereignis am anderen Flag"

        testCase "zurücknehmen braucht keine Farbe in den Settings" <| fun () ->
            let account = freshAccount ()
            let lena, _ = seedLinked account purple (Some lenaFlagName)
            let outcome = withdrawBatch (okProbe ()).Push None [ lena ] |> unwrap
            Expect.equal outcome.Changed.Length 1 "zurückgenommen"

        testCase "ohne geänderte Buchung geht kein PATCH hinaus" <| fun () ->
            let account = freshAccount ()
            let already, _ = seedLinked account purple (Some lenaFlagName)
            let probe = okProbe ()
            let outcome = assignBatch probe.Push purple [ already ] |> unwrap
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal outcome.UnchangedCount 1 "unverändert"

        testCase "gelöschte und unbekannte Buchungen zählen als gescheitert, ohne Ereignis" <| fun () ->
            let account = freshAccount ()
            let deleted, _ = seedLinked account None None
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let ok, _ = seedLinked account None None
            let unknown = newEntryId ()
            let probe = okProbe ()
            let outcome = assignBatch probe.Push purple [ deleted; unknown; ok ] |> unwrap
            Expect.equal outcome.FailedCount 2 "zwei gescheitert"
            Expect.equal outcome.Changed.Length 1 "eine geändert"
            Expect.equal (eventTypesOf deleted |> List.last) "EntryDeletionObservedInYnab" "kein Flag-Ereignis"

        testCase "nennt die Antwort Ids nicht als gespeichert, folgt für sie EntryFlagWriteBackFailed mit Baseline" <| fun () ->
            let account = freshAccount ()
            let saved, _ = seedLinked account None None
            let lost, ynabLost = seedLinked account (Some "red") (Some "Markiert")
            let probe = BatchProbe(Ok [ ynabLost ])
            let outcome = assignBatch probe.Push purple [ saved; lost ] |> unwrap

            let writeBackOf id = (outcome.Changed |> List.find (fun c -> c.EntryId = guidOf id)).WriteBack
            Expect.equal (writeBackOf saved) FlagSent "gespeichert"
            match writeBackOf lost with
            | FlagSendFailed reason -> Expect.equal reason flagNotSavedReason "Grund"
            | other -> failtest $"Fehlschlag erwartet, war {other}"
            Expect.equal (eventTypesOf lost) [ "EntryObservedInYnab"; "EntryFlagChangedInBb"; "EntryFlagWriteBackFailed" ] "Fehlschlag nur für die ungenannte"
            Expect.equal (eventTypesOf saved) [ "EntryObservedInYnab"; "EntryFlagChangedInBb" ] "kein Fehlschlag für die genannte"
            let failure = (entryOf lost).FlagWriteBackFailure |> Option.get
            Expect.equal (failure.BaselineFlagColor, failure.BaselineFlagName) (Some "red", Some "Markiert") "Baseline ist der Stand vor dem Stapel"

        testCase "ein Netzfehler trifft den ganzen Stapel" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account None None
            let b, _ = seedLinked account None None
            let probe = BatchProbe(Error (YnabError.NetworkError "kaputt"))
            let outcome = assignBatch probe.Push purple [ a; b ] |> unwrap
            Expect.isTrue
                (outcome.Changed |> List.forall (fun c -> match c.WriteBack with FlagSendFailed _ -> true | _ -> false))
                "alle gescheitert"
            for id in [ a; b ] do
                Expect.equal (eventTypesOf id |> List.last) "EntryFlagWriteBackFailed" "Fehlschlag je Buchung"
                Expect.equal (entryOf id).FlagName (Some lenaFlagName) "BB behält das Flag"

        testCase "eine nicht verknüpfte Buchung bekommt den Fehlschlag, ohne in den PATCH zu gehen" <| fun () ->
            let account = freshAccount ()
            let linked, ynabLinked = seedLinked account None None
            let unlinked = seedUnlinked account
            let probe = okProbe ()
            let outcome = assignBatch probe.Push purple [ linked; unlinked ] |> unwrap
            Expect.equal (probe.Calls |> List.map (List.map (fun i -> i.YnabTransactionId))) [ [ ynabLinked ] ] "nur die verknüpfte im PATCH"
            let change = outcome.Changed |> List.find (fun c -> c.EntryId = guidOf unlinked)
            Expect.equal change.WriteBack (FlagSendFailed flagNotLinkedReason) "Grund nicht verknüpft"

        testCase "ein früherer offener Fehlschlag endet mit dem Erfolg des Stapels" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account None None
            assignBatch (BatchProbe(Error (YnabError.NetworkError "kaputt"))).Push purple [ a ] |> unwrap |> ignore
            Expect.isSome (entryOf a).FlagWriteBackFailure "Fehlschlag offen"
            // Erneut: Ziel gleich Ist, also "Zurücknehmen" ändert wieder und der Erfolg schließt den Fehlschlag.
            withdrawBatch (okProbe ()).Push purple [ a ] |> unwrap |> ignore
            Expect.isNone (entryOf a).FlagWriteBackFailure "Fehlschlag beendet"
            Expect.equal (eventTypesOf a |> List.last) "EntryFlagWrittenBackToYnab" "Ereignis für das Ende"

        testCase "BatchAllExcept löst den Treffer des Filters ohne die Ausnahmen auf" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account None None
            let b, _ = seedLinked account None None
            let c, _ = seedLinked account None None
            let (LedgerAccountId accountId) = account
            let query : LedgerEntryPageQuery = {
                AccountId = Some accountId
                Page = 3
                PageSize = 2
                LocallyRemoved = 4
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
            let probe = okProbe ()
            let outcome =
                setLenaFlagBatch at probe.Push purple (BatchAllExcept (query, [ guidOf c ])) AssignLena
                |> Async.RunSynchronously
                |> unwrap
            Expect.equal (outcome.Changed |> List.map (fun x -> x.EntryId) |> List.sort) ([ a; b ] |> List.map guidOf |> List.sort) "alle Seiten ohne Ausnahme"
            Expect.equal (eventTypesOf c) [ "EntryObservedInYnab" ] "Ausnahme unberührt"

        testCase "Rückgängig stellt Farbe und Name je Buchung her, auch ein anderes Flag" <| fun () ->
            let account = freshAccount ()
            let plain, ynabPlain = seedLinked account None None
            let marked, ynabMarked = seedLinked account (Some "red") (Some "Markiert")
            let outcome = assignBatch (okProbe ()).Push purple [ plain; marked ] |> unwrap
            let items : FlagRestoreItem list =
                [ for c in outcome.Changed -> { EntryId = c.EntryId; Previous = c.Previous } ]

            let probe = okProbe ()
            let undone = restoreFlagBatch at probe.Push items |> Async.RunSynchronously |> unwrap

            Expect.equal undone.Changed.Length 2 "beide zurückgestellt"
            Expect.equal (entryOf plain).FlagColor None "kein Flag"
            Expect.equal (entryOf marked).FlagColor (Some "red") "Markiert zurück"
            Expect.equal (entryOf marked).FlagName (Some "Markiert") "Name zurück"
            let sent = probe.Calls.Head |> List.map (fun i -> i.YnabTransactionId, i.FlagColor) |> List.sort
            Expect.equal sent ([ ynabPlain, None; ynabMarked, Some "red" ] |> List.sort) "ein PATCH mit den Vorfarben"

        testCase "Rückgängig lässt Buchungen im Vorzustand unangetastet" <| fun () ->
            let account = freshAccount ()
            let a, _ = seedLinked account None None
            let probe = okProbe ()
            let undone =
                restoreFlagBatch at probe.Push [ { EntryId = guidOf a; Previous = { Color = None; Name = None } } ]
                |> Async.RunSynchronously
                |> unwrap
            Expect.isEmpty probe.Calls "kein Push"
            Expect.equal undone.UnchangedCount 1 "unverändert"
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

let private items : FlagBatchItem list = [
    { YnabTransactionId = "tx-1"; FlagColor = Some "purple" }
    { YnabTransactionId = "tx-2"; FlagColor = None }
]

let private sendWith (status: HttpStatusCode) (responseBody: string) =
    let handler = new FakeHandler(status, responseBody)
    use client = new HttpClient(handler)
    let result = sendFlagBatch client (YnabBudgetId "budget-1") items |> Async.RunSynchronously
    handler, result

[<Tests>]
let lenaFlagBatchYnabClientTests =
    testList "YnabClient Flag-Sammel-PATCH (Fake-Handler)" [

        testCase "JSON-Vertrag: je Buchung nur id und flag_color, null für kein Flag" <| fun () ->
            let body = buildFlagBatchBody items
            let parsed =
                Thoth.Json.Net.Decode.fromString
                    (Thoth.Json.Net.Decode.field "transactions" (Thoth.Json.Net.Decode.list (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value)))
                    body
            match parsed with
            | Ok [ first; second ] ->
                Expect.equal (first |> List.map fst |> List.sort) [ "flag_color"; "id" ] "Felder"
                Expect.equal (second |> List.map fst |> List.sort) [ "flag_color"; "id" ] "Felder ohne Flag"
            | other -> failtest $"zwei Einträge erwartet, war {other}"
            Expect.stringContains body "\"flag_color\": \"purple\"" "Farbe"
            Expect.stringContains body "\"flag_color\": null" "null für kein Flag"
            Expect.isFalse (body.Contains "flag_name") "kein Flag-Name"

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
            match sendFlagBatch client (YnabBudgetId "b") items |> Async.RunSynchronously with
            | Error (YnabError.NetworkError _) -> ()
            | other -> failtest $"erwartet Netzfehler, war {other}"
    ]
