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

/// Die Tagesgrenze zählt alle Bob-Buchungen eines Berliner Kalendertags in der geteilten
/// In-Memory-Datenbank. Damit kein Test vom Kalendertag eines anderen abhängt, bekommt jede
/// Testumgebung einen eigenen Tag D aus diesem Zähler. Benutzt werden darf D-1 bis D+2,
/// deshalb liegen vier Tage zwischen zwei Umgebungen (infrastructure-hdf7j).
let mutable private lastTestDay = 0
let private testDayStride = 4
let private firstTestDay = DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)

let private nextTestDay () =
    firstTestDay.AddDays(float (Interlocked.Increment(&lastTestDay) * testDayStride))

/// Eine Testumgebung mit zählbarem Push und verstellbarer Uhr. Die Uhr startet um 10:00 UTC
/// am eigenen Tag D (in Berlin derselbe Kalendertag).
type private Env(?pushResult: Async<YnabResult<string>>, ?target: Result<BotTarget, string>, ?token': string option, ?delayMs: int, ?rateLimit: int, ?loadCategories: Async<Result<YnabCategory list, string>>) =
    let day = nextTestDay ()
    let mutable now = day.AddHours 10.0
    let mutable pushes = 0
    let mutable categoryLoads = 0
    member _.CategoryLoads = categoryLoads
    member _.Now with get () = now and set v = now <- v
    /// Der eigene Tag D dieser Umgebung (UTC-Mitternacht).
    member _.Day = day
    /// Stellt die Uhr auf D plus `offsetDays`, zur angegebenen UTC-Uhrzeit.
    member _.SetUtc(offsetDays: int, hour: int, minute: int) =
        now <- day.AddDays(float offsetDays).AddHours(float hour).AddMinutes(float minute)
    /// D plus `offsetDays` als Buchungsdatum im Format der Bot-Schnittstelle.
    member _.DateText(offsetDays: int) = day.AddDays(float offsetDays).ToString("yyyy-MM-dd")
    member _.Pushes = pushes
    member val Deps : BotDeps =
        { Token = fun () -> defaultArg token' (Some token)
          MaxAmountEur = fun () -> 500m
          Now = fun () -> now
          BerlinZone = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"
          LoadCategories =
            fun () ->
                Interlocked.Increment(&categoryLoads) |> ignore
                defaultArg loadCategories (async.Return (Ok categories))
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
          Store = BotPreviewStore()
          Limiter = BotRateLimiter(defaultArg rateLimit 100000, rateLimitWindow)
          Categories = BotCategoryCache() }

/// Ruft den Handler auf. Liefert Status und Rumpf, oder -1, wenn der Handler den Pfad nicht
/// beansprucht (None zurückgibt, damit `choose` den nächsten Handler probiert). Ruft er
/// dafür `next` auf, wäre die Anfrage im `choose` von Api.webApp erledigt (Fehler 2026-10-29).
let private call (deps: BotDeps) (meth: string) (path: string) (auth: string option) (body: string) : int * JsonElement option =
    let ctx = DefaultHttpContext()
    ctx.Request.Method <- meth
    ctx.Request.Path <- PathString path
    auth |> Option.iter (fun a -> ctx.Request.Headers["Authorization"] <- a)
    ctx.Request.Body <- new MemoryStream(Text.Encoding.UTF8.GetBytes body)
    let responseBody = new MemoryStream()
    ctx.Response.Body <- responseBody
    let next : Giraffe.Core.HttpFunc = fun c -> Task.FromResult(Some c)
    let result = (handler deps next ctx).GetAwaiter().GetResult()
    if result.IsNone then
        -1, None
    else
        responseBody.Position <- 0L
        let text = (new StreamReader(responseBody)).ReadToEnd()
        ctx.Response.StatusCode, (if text = "" then None else Some (JsonDocument.Parse(text).RootElement))

let private bearer = Some $"Bearer {token}"

let private uniquePayeeText () = $"Limit-{Guid.NewGuid()}"

/// Heute als Berliner Kalendertag der injizierten Uhr.
let private todayText (deps: BotDeps) =
    (Ledger.Bot.Domain.berlinDayOf deps.BerlinZone (deps.Now())).ToString("yyyy-MM-dd")

let private bodyOf (deps: BotDeps) (payee: string) (amount: string) (direction: string) =
    $"""{{"amount": {amount}, "direction": "{direction}", "payee": "{payee}", "category": "Haushaltshilfe", "date": "{todayText deps}", "memo": "Test"}}"""

let private preview (deps: BotDeps) (payee: string) (amount: string) (direction: string) : Guid =
    match call deps "POST" "/bot/transactions/preview" bearer (bodyOf deps payee amount direction) with
    | 200, Some json -> Guid.Parse(json.GetProperty("preview_id").GetString())
    | status, json -> failtest $"Vorschau sollte 200 liefern, war {status} {json}"

/// Vorschau mit frei gewähltem Buchungsdatum; liefert Status und Rumpf.
let private previewOn (deps: BotDeps) (date: string) (amount: string) (direction: string) =
    let body =
        $"""{{"amount": {amount}, "direction": "{direction}", "payee": "{uniquePayeeText ()}", "category": "Haushaltshilfe", "date": "{date}"}}"""
    call deps "POST" "/bot/transactions/preview" bearer body

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

        testCase "zwei Vorschauen und eine Kategorienliste innerhalb von 5 Minuten laden die Kategorien genau einmal, danach erneut" <| fun () ->
            let env = Env()
            preview env.Deps (uniquePayee ()) "1.00" "outflow" |> ignore
            preview env.Deps (uniquePayee ()) "1.00" "outflow" |> ignore
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 200 "Kategorien"
            Expect.equal env.CategoryLoads 1 "ein YNAB-Abruf"
            env.Now <- env.Now.AddMinutes 4.9
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 200 "noch im Cache"
            Expect.equal env.CategoryLoads 1 "weiter ein Abruf"
            env.Now <- env.Now.AddMinutes 0.2
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 200 "nach Ablauf"
            Expect.equal env.CategoryLoads 2 "zweiter Abruf nach 5 Minuten"

        testCase "ein YNAB-Fehler ohne Cache bleibt 502 und wird nicht gecacht" <| fun () ->
            let env = Env(loadCategories = async.Return (Error "YNAB down"))
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 502 "erster Fehler"
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 502 "zweiter Fehler"
            Expect.equal env.CategoryLoads 2 "Fehler wird erneut abgefragt"

        testCase "die 11. Anfrage innerhalb einer Minute bekommt 429 rate_limited mit Retry-After, danach geht es wieder" <| fun () ->
            let env = Env(rateLimit = 10)
            for i in 1..10 do
                Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 200 $"Anfrage {i}"
            let status, json = call env.Deps "GET" "/bot/categories" bearer ""
            Expect.equal status 429 "11. Anfrage"
            Expect.equal (str json "error") "rate_limited" "Code"
            let ctx = DefaultHttpContext()
            ctx.Request.Path <- PathString "/bot/categories"
            ctx.Response.Body <- new MemoryStream()
            let next : Giraffe.Core.HttpFunc = fun c -> Task.FromResult(Some c)
            (handler env.Deps next ctx).GetAwaiter().GetResult() |> ignore
            Expect.equal (int (string ctx.Response.Headers["Retry-After"])) 60 "Retry-After in Sekunden"
            env.Now <- env.Now.AddSeconds 61.0
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 200 "nach der Minute"

        testCase "Retry-After nennt die Sekunden bis zur nächsten freien Anfrage" <| fun () ->
            let limiter = BotRateLimiter(1, TimeSpan.FromMinutes 1.0)
            let t0 = DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc)
            Expect.isNone (limiter.TryAcquire t0) "erste erlaubt"
            Expect.equal (limiter.TryAcquire(t0.AddSeconds 20.0)) (Some 40) "40 Sekunden"
            Expect.isNone (limiter.TryAcquire(t0.AddSeconds 60.0)) "nach 60 Sekunden frei"

        testCase "falsche Tokens zählen mit: nach 10 Fehlversuchen gibt es 429 auch mit richtigem Token" <| fun () ->
            let env = Env(rateLimit = 10)
            for _ in 1..10 do
                Expect.equal (fst (call env.Deps "GET" "/bot/categories" (Some "Bearer falsch") "")) 401 "Fehlversuch"
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 429 "gedrosselt trotz richtigem Token"

        testCase "parallele Anfragen überschreiten die Grenze nicht" <| fun () ->
            let limiter = BotRateLimiter(10, TimeSpan.FromMinutes 1.0)
            let t0 = DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc)
            let allowed =
                [| for _ in 1..200 -> Task.Run(fun () -> (limiter.TryAcquire t0).IsNone) |]
                |> Array.map (fun t -> t.Result)
                |> Array.filter id
                |> Array.length
            Expect.equal allowed 10 "genau 10 durch"

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

        testCase "im choose vor Fable.Remoting erreichen /api/-Anfragen den nächsten Handler" <| fun () ->
            // Verhindert die Wiederkehr des Ausfalls vom 2026-10-29: der Bot-Handler rief für
            // fremde Pfade `next ctx` auf. Im `choose` von Api.webApp beendete das die Anfrage,
            // jeder API-Aufruf bekam 200 mit leerem Rumpf, der Client scheiterte an JSON.parse.
            let env = Env()
            let app = Giraffe.Core.choose [ handler env.Deps; Giraffe.Core.setStatusCode 299 ]
            for meth, path in [ "GET", "/api/SettingsApi/getSettings"; "POST", "/api/LedgerApi/getLedgerAccounts"; "GET", "/botanik" ] do
                let ctx = DefaultHttpContext()
                ctx.Request.Method <- meth
                ctx.Request.Path <- PathString path
                let finish : Giraffe.Core.HttpFunc = fun c -> Task.FromResult(Some c)
                let result = (app finish ctx).GetAwaiter().GetResult()
                Expect.isSome result $"{meth} {path} beantwortet"
                Expect.equal ctx.Response.StatusCode 299 $"{meth} {path} landet beim Handler nach dem Bot-Handler"

        testCase "Bot-Pfade ohne Groß-/Kleinschreibung und mit kodiertem Schrägstrich landen im Handler (401 ohne Token)" <| fun () ->
            let env = Env()
            for path in [ "/bot"; "/BOT"; "/Bot/categories"; "/bot%2fcategories"; "/BOT%2Fcategories" ] do
                let status, json = call env.Deps "GET" path None ""
                Expect.equal status 401 $"{path} ohne Token"
                Expect.equal (str json "error") "unauthorized" $"{path} als JSON"

        testCase "Mit Token: andere Schreibweisen und kodierte Pfade antworten 404 JSON, nur /bot/categories liefert Kategorien" <| fun () ->
            let env = Env()
            for path in [ "/bot"; "/BOT"; "/Bot/categories"; "/bot%2fcategories"; "/BOT%2Fcategories" ] do
                let status, json = call env.Deps "GET" path bearer ""
                Expect.equal status 404 $"{path} mit Token"
                Expect.equal (str json "error") "not_found" $"{path} als JSON"
            Expect.equal (fst (call env.Deps "GET" "/bot/categories" bearer "")) 200 "kleingeschriebene Route bleibt"
            Expect.equal (fst (call env.Deps "GET" "/botanik" None "")) -1 "/botanik weiter"
            Expect.equal (fst (call env.Deps "GET" "/api/bot" None "")) -1 "/api/... weiter"

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
            let status, json = call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf env.Deps payee "50" "outflow")
            Expect.equal status 200 "200"
            Expect.equal (str json "payee") payee "Payee"
            Expect.equal (str json "account") "Bar" "Konto"
            Expect.equal (json.Value.GetProperty("amount").GetDecimal()) 50m "50 Euro"
            Expect.equal (str json "direction") "outflow" "Richtung"
            Expect.equal (json.Value.GetProperty("category").GetProperty("name").GetString()) "Haushaltshilfe" "Kategorie"
            Expect.equal (json.Value.GetProperty("category").GetProperty("group").GetString()) "Haushalt" "Gruppe"
            Expect.equal (str json "date") (env.DateText 0) "Datum"
            Expect.equal (str json "memo") "Test" "Memo"
            Expect.equal (str json "expires_at") $"{env.DateText 0}T10:15:00Z" "15 Minuten"
            Expect.isEmpty (entriesFor payee) "nichts im Kassenbuch"
            Expect.equal env.Pushes 0 "nichts an YNAB"

        testCase "Betrag über der Obergrenze, 0 und negativ werden mit 422 abgelehnt, 500 Euro geht" <| fun () ->
            let env = Env()
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf env.Deps "x" "500.01" "outflow"))) 422 "500,01 Euro"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf env.Deps "x" "0" "outflow"))) 422 "0 Euro"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf env.Deps "x" "-20" "outflow"))) 422 "-20 Euro (-20000 Milliunits)"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf env.Deps "x" "500" "outflow"))) 200 "500 Euro"

        testCase "ungültige Eingaben ergeben 400 mit Feldnamen" <| fun () ->
            let env = Env()
            let status, json = call env.Deps "POST" "/bot/transactions/preview" bearer """{"amount": 5, "direction": "quer", "payee": "x", "category": "c", "date": "kein-datum"}"""
            Expect.equal status 400 "400"
            let fields = json.Value.GetProperty("fields").EnumerateArray() |> Seq.map (fun f -> f.GetProperty("field").GetString()) |> Seq.sort |> Seq.toList
            Expect.equal fields [ "date"; "direction" ] "Feldnamen"
            Expect.equal (fst (call env.Deps "POST" "/bot/transactions/preview" bearer $"""{{"amount":5,"direction":"outflow","payee":"x","category":"c","date":"{env.DateText 0}","account":"Giro"}}""")) 400 "Konto-Feld abgelehnt"

        testCase "Kategorie: mehrere und keine Treffer ergeben 422 mit Kandidaten, interne Kategorie nicht wählbar" <| fun () ->
            let env = Env()
            let withCategory c = $"""{{"amount": 5, "direction": "outflow", "payee": "x", "category": "{c}", "date": "{env.DateText 0}"}}"""
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
            let status, json = call env.Deps "POST" "/bot/transactions/preview" bearer (bodyOf env.Deps "x" "5" "outflow")
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
        // ---- Tagesgrenze und Datumsfenster (ledger-ssmp3, ADR 0068) ----
        // Jeder Test rechnet relativ zum eigenen Tag D seiner Umgebung (`Env`, `nextTestDay`),
        // kein Test nennt ein festes Datum.

        testCase "Tagesgrenze nach Summe: bei 450 Euro (450000 Milliunits) heute geht 50 Euro, 60 Euro ergibt 422" <| fun () ->
            let env = Env()
            commit env.Deps (preview env.Deps (uniquePayee ()) "450" "outflow") |> ignore
            let status, json = previewOn env.Deps (env.DateText 0) "60" "outflow"
            Expect.equal status 422 "über der Tagessumme"
            Expect.equal (str json "error") "daily_limit_reached" "Code"
            Expect.stringContains (str json "message") "450.00 EUR in 1 Buchungen" "nennt Summe und Anzahl des Tages"
            let status50, _ = previewOn env.Deps (env.DateText 0) "50" "outflow"
            Expect.equal status50 200 "50 Euro passen genau"

        testCase "Tagesgrenze nach Anzahl: nach 10 Commits heute lehnt die 11. Vorschau und der 11. Commit mit 422 ab" <| fun () ->
            let env = Env()
            let pending = [ for _ in 1..11 -> preview env.Deps (uniquePayee ()) "1" "outflow" ]
            for id in pending |> List.take 10 do
                Expect.equal (fst (commit env.Deps id)) 200 "gebucht"
            let status, json = commit env.Deps (List.last pending)
            Expect.equal status 422 "11. Commit abgelehnt"
            Expect.equal (str json "error") "daily_limit_reached" "Code"
            let previewStatus, previewJson = previewOn env.Deps (env.DateText 0) "1" "outflow"
            Expect.equal previewStatus 422 "11. Vorschau abgelehnt"
            Expect.equal (str previewJson "error") "daily_limit_reached" "Code der Vorschau"

        testCase "Einnahmen zählen mit ihrem Betrag ohne Vorzeichen zur Tagessumme" <| fun () ->
            let env = Env()
            commit env.Deps (preview env.Deps (uniquePayee ()) "300" "outflow") |> ignore
            commit env.Deps (preview env.Deps (uniquePayee ()) "150" "inflow") |> ignore
            Expect.equal (fst (previewOn env.Deps (env.DateText 0) "60" "inflow")) 422 "450 plus 60 über 500, auch als Einnahme"
            Expect.equal (fst (previewOn env.Deps (env.DateText 0) "50" "outflow")) 200 "450 plus 50 passt"

        testCase "gestern und andere Herkunft zählen nicht: Quick Add und Bob von gestern lassen 500 Euro heute zu" <| fun () ->
            let env = Env()
            env.SetUtc(-1, 10, 0)
            commit env.Deps (preview env.Deps (uniquePayee ()) "400" "outflow") |> ignore
            env.SetUtc(0, 10, 0)
            let quickAdd : LedgerEvent =
                { EntryId = EntryId (Guid.NewGuid())
                  OccurredAt = env.Now
                  Actor = QuickAdd
                  Body =
                    CashExpenseAdded
                        { AccountId = accountId
                          Date = env.Now
                          Amount = { Amount = -400m; Currency = "EUR" }
                          Payee = Some "Quick"
                          Memo = None
                          Category = None
                          Cleared = Uncleared
                          Approved = false } }
            appendAndApply [ quickAdd ] |> Async.RunSynchronously
            Expect.equal (fst (previewOn env.Deps (env.DateText 0) "500" "outflow")) 200 "weder gestern noch Quick Add zählen"

        testCase "die Grenze kommt aus dem Kassenbuch: ein neuer BotPreviewStore (Neustart) setzt nichts zurück" <| fun () ->
            let before = Env()
            commit before.Deps (preview before.Deps (uniquePayee ()) "450" "outflow") |> ignore
            let after = Env()
            after.Now <- before.Now
            Expect.equal (fst (previewOn after.Deps (before.DateText 0) "60" "outflow")) 422 "Grenze gilt nach Neustart weiter"

        testCase "zwei offene Vorschauen über der Grenze: der erste Commit bucht, der zweite ergibt 422" <| fun () ->
            let env = Env()
            let first = preview env.Deps (uniquePayee ()) "300" "outflow"
            let second = preview env.Deps (uniquePayee ()) "300" "outflow"
            Expect.equal (fst (commit env.Deps first)) 200 "erster bucht"
            let status, json = commit env.Deps second
            Expect.equal status 422 "zweiter abgelehnt"
            Expect.equal (str json "error") "daily_limit_reached" "Code"
            Expect.equal env.Pushes 1 "nur ein Push"

        testCase "gleichzeitige Commits verschiedener Vorschauen buchen höchstens bis zur Grenze" <| fun () ->
            let env = Env(delayMs = 50)
            let ids = [ for _ in 1..6 -> preview env.Deps (uniquePayee ()) "150" "outflow" ]
            let results =
                ids
                |> List.map (fun id -> Task.Run(fun () -> commit env.Deps id))
                |> List.map (fun t -> t.GetAwaiter().GetResult())
            Expect.equal (results |> List.filter (fun (s, _) -> s = 200) |> List.length) 3 "genau drei mal 150 Euro passen"
            Expect.equal (results |> List.filter (fun (s, _) -> s = 422) |> List.length) 3 "der Rest wird abgelehnt"
            Expect.equal env.Pushes 3 "drei Pushes"

        testCase "Wiederholung an der Grenze: derselbe Commit zählt nicht doppelt und liefert das erste Ergebnis" <| fun () ->
            let env = Env()
            let id = preview env.Deps (uniquePayee ()) "500" "outflow"
            let _, first = commit env.Deps id
            let status, second = commit env.Deps id
            Expect.equal status 200 "Wiederholung 200 trotz erreichter Grenze"
            Expect.equal (str second "entry_id") (str first "entry_id") "dasselbe Ergebnis"
            Expect.equal env.Pushes 1 "ein Push"

        testCase "Datumsfenster: heute und heute minus 31 Tage gehen, minus 32 Tage und morgen ergeben 400 mit Feld date" <| fun () ->
            let env = Env()
            Expect.equal (fst (previewOn env.Deps (env.DateText 0) "1" "outflow")) 200 "heute"
            Expect.equal (fst (previewOn env.Deps (env.DateText -31) "1" "outflow")) 200 "heute minus 31 Tage"
            for date in [ env.DateText -32; env.DateText 1 ] do
                let status, json = previewOn env.Deps date "1" "outflow"
                Expect.equal status 400 $"{date} abgelehnt"
                Expect.equal (json.Value.GetProperty("fields").[0].GetProperty("field").GetString()) "date" "Feld date"

        testCase "Berliner Tag: kurz nach Mitternacht in Berlin gilt schon der neue Tag, obwohl UTC noch beim Vortag liegt" <| fun () ->
            let env = Env()
            // D 23:30 UTC ist in Berlin schon D+1 (00:30 im Winter, 01:30 im Sommer).
            env.SetUtc(0, 23, 30)
            Expect.equal (fst (previewOn env.Deps (env.DateText 1) "1" "outflow")) 200 "Berliner Tag ist erlaubt, kein Zukunftsdatum"
            Expect.equal (fst (previewOn env.Deps (env.DateText 2) "1" "outflow")) 400 "übermorgen Berlin bleibt Zukunft"
            // Die Zählung folgt dem Berliner Tag: 450 Euro um D 23:30 UTC zählen für D+1, nicht für D.
            commit env.Deps (preview env.Deps (uniquePayee ()) "450" "outflow") |> ignore
            env.SetUtc(1, 9, 0)
            Expect.equal (fst (previewOn env.Deps (env.DateText 1) "60" "outflow")) 422 "gleicher Berliner Tag"
            env.SetUtc(0, 21, 0)
            Expect.equal (fst (previewOn env.Deps (env.DateText 0) "500" "outflow")) 200 "D 21:00 UTC ist in Berlin noch D, ein anderer Tag"
    ]
