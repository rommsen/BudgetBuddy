module BotApiTests

// Routen der Bot-Schnittstelle (ledger-b0b7q, ADR 0066) über den Giraffe-Handler, gegen die
// In-Memory-Datenbank (ADR 0030), nie gegen die Produktionsdatenbank. YNAB, Uhr und
// Quick-Add-Konto sind injiziert. Beträge: Euro und YNAB-Milliunits werden benannt.

open System
open System.IO
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Server.BotApi

let private token = "test-token-123"
let private accountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000b0")
let private ynabAccountId = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000b1")

let private cat name group hidden : YnabCategory =
    { Id = YnabCategoryId (Guid.NewGuid()); Name = name; GroupName = group; Available = None; Hidden = hidden }

let private haushaltshilfe = cat "Haushaltshilfe" "Haushalt" false
let private categories =
    [ haushaltshilfe
      cat "Wäsche" "Haushalt" false
      cat "Wäsche" "Kleidung" false
      cat "Altlast" "Haushalt" true
      cat "Inflow: Ready to Assign" internalMasterGroupName false ]

/// Eine Testumgebung mit zählbarem Push und verstellbarer Uhr.
type private Env(?pushResult: Async<YnabResult<string>>, ?target: Result<BotTarget, string>, ?token': string option, ?delayMs: int) =
    let mutable now = DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc)
    let mutable pushes = 0
    member _.Now with get () = now and set v = now <- v
    member _.Pushes = pushes
    member val Deps : BotDeps =
        { Token = fun () -> defaultArg token' (Some token)
          MaxAmountEur = fun () -> 500m
          Now = fun () -> now
          LoadCategories = fun () -> async.Return (Ok categories)
          ResolveTarget =
            fun () ->
                async.Return (
                    defaultArg target (Ok { LedgerAccountId = accountId; YnabAccountId = ynabAccountId; AccountName = "Bar" })
                )
          Push =
            fun _ ->
                async {
                    Interlocked.Increment(&pushes) |> ignore
                    match delayMs with
                    | Some ms -> do! Async.Sleep ms
                    | None -> ()
                    match pushResult with
                    | Some r -> return! r
                    | None -> return Ok $"ynab-{Guid.NewGuid()}"
                }
          TriggerMirror = ignore
          Store = BotPreviewStore() }

/// Ruft den Handler auf. Liefert Status und Rumpf (null, wenn der Handler den Pfad nicht
/// beansprucht und weitergereicht hat).
let private call (deps: BotDeps) (meth: string) (path: string) (auth: string option) (body: string) : int * JsonElement option =
    let ctx = DefaultHttpContext()
    ctx.Request.Method <- meth
    ctx.Request.Path <- PathString path
    auth |> Option.iter (fun a -> ctx.Request.Headers["Authorization"] <- a)
    ctx.Request.Body <- new MemoryStream(Text.Encoding.UTF8.GetBytes body)
    let responseBody = new MemoryStream()
    ctx.Response.Body <- responseBody
    let mutable passedOn = false
    let next : Giraffe.Core.HttpFunc = fun c -> passedOn <- true; Task.FromResult(Some c)
    (handler deps next ctx).GetAwaiter().GetResult() |> ignore
    if passedOn then
        -1, None
    else
        responseBody.Position <- 0L
        let text = (new StreamReader(responseBody)).ReadToEnd()
        ctx.Response.StatusCode, (if text = "" then None else Some (JsonDocument.Parse(text).RootElement))

let private bearer = Some $"Bearer {token}"

let private bodyOf (payee: string) (amount: string) (direction: string) =
    $"""{{"amount": {amount}, "direction": "{direction}", "payee": "{payee}", "category": "Haushaltshilfe", "date": "2026-10-28", "memo": "Test"}}"""

let private preview (deps: BotDeps) (payee: string) (amount: string) (direction: string) : Guid =
    match call deps "POST" "/bot/transactions/preview" bearer (bodyOf payee amount direction) with
    | 200, Some json -> Guid.Parse(json.GetProperty("preview_id").GetString())
    | status, json -> failtest $"Vorschau sollte 200 liefern, war {status} {json}"

let private commit (deps: BotDeps) (id: Guid) = call deps "POST" $"/bot/transactions/{id}/commit" bearer ""

let private entriesFor (payee: string) : Ledger.Domain.Entry list =
    loadEntriesForAccount accountId
    |> Async.RunSynchronously
    |> List.filter (fun e -> e.Payee = Some payee)

let private uniquePayee () = $"Anastasia-{Guid.NewGuid()}"

let private str (json: JsonElement option) (name: string) = json.Value.GetProperty(name).GetString()

[<Tests>]
let botApiTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Bot-Schnittstelle (Routen)" [

        testCase "ohne Bearer-Token antworten alle /bot/-Routen mit 401" <| fun () ->
            let env = Env()
            for meth, path in [ "POST", "/bot/transactions/preview"; "POST", $"/bot/transactions/{Guid.NewGuid()}/commit"; "GET", "/bot/categories"; "GET", "/bot/irgendwas" ] do
                let status, json = call env.Deps meth path None ""
                Expect.equal status 401 $"{meth} {path} ohne Header"
                Expect.equal (str json "error") "unauthorized" "JSON-Fehler"
                Expect.equal (fst (call env.Deps meth path (Some "Bearer falsch") "")) 401 $"{meth} {path} falsches Token"
                Expect.equal (fst (call env.Deps meth path (Some token) "")) 401 "ohne Bearer-Präfix"

        testCase "ist kein Token konfiguriert, sind die Routen abgeschaltet (401), auch mit beliebigem Header" <| fun () ->
            let env = Env(token' = None)
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 401 "abgeschaltet"
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" (Some "Bearer ") "")) 401 "leeres Token"

        testCase "unbekannte Pfade unter /bot/ antworten 404 als JSON, Pfade außerhalb gehen weiter" <| fun () ->
            let env = Env()
            let status, json = call env.Deps "GET" "/bot/gibt-es-nicht" bearer ""
            Expect.equal status 404 "404"
            Expect.equal (str json "error") "not_found" "JSON statt index.html"
            Expect.equal (fst (call env.Deps "GET" "/bot/transactions" bearer "")) 404 "keine Lese-Route für Buchungen"
            Expect.equal (fst (call env.Deps "GET" "/bot/balances" bearer "")) 404 "keine Lese-Route für Salden"
            Expect.equal (fst (call env.Deps "GET" "/bot/transactions/preview" bearer "")) 404 "falsche Methode"
            Expect.equal (fst (call env.Deps "GET" "/api/irgendwas" None "")) -1 "kein /bot/-Pfad: weitergereicht"
            Expect.equal (fst (call env.Deps "GET" "/botanik" None "")) -1 "/botanik ist kein /bot/-Pfad"

        testCase "GET /bot/categories liefert Name, Gruppe, Id ohne interne und versteckte Kategorien" <| fun () ->
            let env = Env()
            let status, json = call env.Deps "GET" "/bot/categories" bearer ""
            Expect.equal status 200 "200"
            let items = json.Value.GetProperty("categories").EnumerateArray() |> Seq.toList
            Expect.equal items.Length 3 "drei wählbare"
            Expect.isTrue (items |> List.exists (fun i -> i.GetProperty("name").GetString() = "Haushaltshilfe" && i.GetProperty("group").GetString() = "Haushalt" && i.GetProperty("id").GetString() <> "")) "Felder"

        testCase "Vorschau löst auf, schreibt weder ins Kassenbuch noch nach YNAB" <| fun () ->
            let env = Env()
            let payee = uniquePayee ()
            let status, json = call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf payee "50" "outflow")
            Expect.equal status 200 "200"
            Expect.equal (str json "payee") payee "Payee"
            Expect.equal (str json "account") "Bar" "Konto"
            Expect.equal (json.Value.GetProperty("amount").GetDecimal()) 50m "50 Euro"
            Expect.equal (str json "direction") "outflow" "Richtung"
            Expect.equal (json.Value.GetProperty("category").GetProperty("name").GetString()) "Haushaltshilfe" "Kategorie"
            Expect.equal (json.Value.GetProperty("category").GetProperty("group").GetString()) "Haushalt" "Gruppe"
            Expect.equal (str json "date") "2026-10-28" "Datum"
            Expect.equal (str json "memo") "Test" "Memo"
            Expect.equal (str json "expires_at") "2026-10-28T10:15:00Z" "15 Minuten"
            Expect.isEmpty (entriesFor payee) "nichts im Kassenbuch"
            Expect.equal env.Pushes 0 "nichts an YNAB"

        testCase "Betrag über der Obergrenze, 0 und negativ werden mit 422 abgelehnt, 500 Euro geht" <| fun () ->
            let env = Env()
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf "x" "500.01" "outflow"))) 422 "500,01 Euro"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf "x" "0" "outflow"))) 422 "0 Euro"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf "x" "-20" "outflow"))) 422 "-20 Euro (-20000 Milliunits)"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf "x" "500" "outflow"))) 200 "500 Euro"

        testCase "ungültige Eingaben ergeben 400 mit Feldnamen" <| fun () ->
            let env = Env()
            let status, json = call env.Deps "POST" "/bot/transactions/preview" bearer """{"amount": 5, "direction": "quer", "payee": "x", "category": "c", "date": "kein-datum"}"""
            Expect.equal status 400 "400"
            let fields = json.Value.GetProperty("fields").EnumerateArray() |> Seq.map (fun f -> f.GetProperty("field").GetString()) |> Seq.sort |> Seq.toList
            Expect.equal fields [ "date"; "direction" ] "Feldnamen"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer """{"amount":5,"direction":"outflow","payee":"x","category":"c","date":"2026-10-28","account":"Giro"}""")) 400 "Konto-Feld abgelehnt"

        testCase "Kategorie: mehrere und keine Treffer ergeben 422 mit Kandidaten, interne Kategorie nicht wählbar" <| fun () ->
            let env = Env()
            let withCategory c = $"""{{"amount": 5, "direction": "outflow", "payee": "x", "category": "{c}", "date": "2026-10-28"}}"""
            let status, json = call env.Deps "POST" "/bot/transactions/preview" bearer (withCategory "wäsche")
            Expect.equal status 422 "mehrdeutig"
            Expect.equal (str json "error") "category_ambiguous" "Code"
            Expect.equal (json.Value.GetProperty("candidates").GetArrayLength()) 2 "zwei Kandidaten"
            let status2, json2 = call env.Deps "POST" "/bot/transactions/preview" bearer (withCategory "Unbekannt")
            Expect.equal status2 422 "kein Treffer"
            Expect.equal (str json2 "error") "category_not_found" "Code"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (withCategory "Inflow: Ready to Assign"))) 422 "intern"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (withCategory "Altlast"))) 422 "versteckt"

        testCase "ohne eingerichtetes Quick-Add-Konto antwortet die Vorschau mit 422 und Grund" <| fun () ->
            let env = Env(target = Error "Kein Quick-Add-Konto eingerichtet")
            let status, json = call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf "x" "5" "outflow")
            Expect.equal status 422 "422"
            Expect.equal (str json "message") "Kein Quick-Add-Konto eingerichtet" "Grund"

        testCase "Commit einer unbekannten Vorschau ergibt 404, einer abgelaufenen 410" <| fun () ->
            let env = Env()
            Expect.equal (fst (commit env.Deps (Guid.NewGuid()))) 404 "unbekannt"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/keine-guid/commit" bearer "")) 404 "keine Guid"
            let payee = uniquePayee ()
            let id = preview env.Deps payee "5" "outflow"
            env.Now <- env.Now.AddMinutes 15.0
            Expect.equal (fst (commit env.Deps id)) 410 "genau 15 Minuten: abgelaufen"
            Expect.isEmpty (entriesFor payee) "nichts gebucht"
            Expect.equal env.Pushes 0 "nichts gepusht"

        testCase "Commit bucht die Vorschau mit Actor Bob, 50 Euro Ausgabe sind -50000 Milliunits" <| fun () ->
            let env = Env()
            let payee = uniquePayee ()
            let id = preview env.Deps payee "50" "outflow"
            let status, json = commit env.Deps id
            Expect.equal status 200 "200"
            Expect.equal (str json "status") "booked" "gebucht"
            Expect.isTrue (json.Value.GetProperty("pushed_to_ynab").GetBoolean()) "gepusht"
            let entry = entriesFor payee |> List.exactlyOne
            Expect.equal entry.Amount { Amount = -50m; Currency = "EUR" } "-50,00 EUR"
            Expect.equal (entry.Category |> Option.map (fun c -> c.CategoryName)) (Some "Haushaltshilfe") "Kategorie"
            Expect.equal entry.Memo (Some "Test") "Memo"
            Expect.isTrue entry.LinkedToYnab "verknüpft"
            let recorded = loadEventsForEntry entry.Id |> Async.RunSynchronously
            Expect.equal (recorded |> List.map (fun r -> r.EventType)) [ "CashExpenseAdded"; "EntryLinkedToYnab" ] "Ereignisse"
            Expect.equal (recorded |> List.map (fun r -> r.Event.Actor)) [ Bob; Bob ] "Herkunft Bob nach dem Laden aus der Datenbank"
            Expect.equal (Ledger.QueryHandlers.ReadModel.originOf recorded.Head.Event.Actor) AddedByBob "Herkunft sichtbar"
            Expect.equal env.Pushes 1 "ein Push"

        testCase "eine Einnahme (inflow) wird positiv gebucht: 25 Euro sind +25000 Milliunits" <| fun () ->
            let env = Env()
            let payee = uniquePayee ()
            commit env.Deps (preview env.Deps payee "25" "inflow") |> ignore
            Expect.equal (entriesFor payee |> List.exactlyOne).Amount { Amount = 25m; Currency = "EUR" } "+25,00 EUR"

        testCase "Commit ist idempotent: zweimal dieselbe preview_id bucht einmal und liefert dasselbe Ergebnis" <| fun () ->
            let env = Env()
            let payee = uniquePayee ()
            let id = preview env.Deps payee "12.34" "outflow"
            let _, first = commit env.Deps id
            let status, second = commit env.Deps id
            Expect.equal status 200 "zweiter Aufruf 200"
            Expect.equal (str second "entry_id") (str first "entry_id") "dieselbe Buchung"
            Expect.equal (entriesFor payee).Length 1 "genau eine Buchung"
            Expect.equal env.Pushes 1 "genau ein Push"
            // Auch nach Ablauf der Gültigkeit liefert ein Wiederholungsaufruf das erste Ergebnis.
            env.Now <- env.Now.AddHours 2.0
            let status3, third = commit env.Deps id
            Expect.equal status3 200 "nach Ablauf weiter 200"
            Expect.equal (str third "entry_id") (str first "entry_id") "dasselbe Ergebnis"

        testCase "zehn gleichzeitige Commits derselben Vorschau buchen nur einmal" <| fun () ->
            let env = Env(delayMs = 150)
            let payee = uniquePayee ()
            let id = preview env.Deps payee "7" "outflow"
            let results =
                [ for _ in 1..10 -> Task.Run(fun () -> commit env.Deps id) ]
                |> List.map (fun t -> t.GetAwaiter().GetResult())
            Expect.isTrue (results |> List.forall (fun (s, _) -> s = 200)) "alle 200"
            Expect.equal (results |> List.map (fun (_, j) -> str j "entry_id") |> List.distinct |> List.length) 1 "dieselbe entry_id"
            Expect.equal (entriesFor payee).Length 1 "genau eine Buchung"
            Expect.equal env.Pushes 1 "genau ein Push"

        testCase "Push-Fehlschlag: Buchung steht, EntryPushToYnabFailed mit Actor Bob, die Antwort meldet ihn" <| fun () ->
            let env = Env(pushResult = async.Return (Error (YnabError.NetworkError "Timeout")))
            let payee = uniquePayee ()
            let id = preview env.Deps payee "9" "outflow"
            let status, json = commit env.Deps id
            Expect.equal status 200 "Buchung steht"
            Expect.isFalse (json.Value.GetProperty("pushed_to_ynab").GetBoolean()) "nicht in YNAB"
            Expect.stringContains (str json "push_error") "Netzwerk" "Grund"
            let entry = entriesFor payee |> List.exactlyOne
            Expect.isSome entry.PushToYnabFailure "Fehlschlag im Zustand"
            let recorded = loadEventsForEntry entry.Id |> Async.RunSynchronously
            Expect.equal (recorded |> List.map (fun r -> r.EventType)) [ "CashExpenseAdded"; "EntryPushToYnabFailed" ] "Ereignisse"
            Expect.equal (recorded |> List.map (fun r -> r.Event.Actor)) [ Bob; Bob ] "Actor Bob"
            let _, again = commit env.Deps id
            Expect.equal (str again "push_error") (str json "push_error") "Wiederholung liefert das erste Ergebnis"
            Expect.equal (entriesFor payee).Length 1 "keine zweite Buchung"
            Expect.equal env.Pushes 1 "kein zweiter Push"
    ]
