module RemotingPipelineTests

// Pipeline-Test über die echte Giraffe-Pipeline (infrastructure-am805, Souls E7).
//
// Der /api-Ausfall vom 2026-10-29 (Fix 2a5c229) rutschte durch, weil kein Test
// `Api.webApp()` als Ganzes fuhr: der Bot-Handler stand im `choose` vor Fable.Remoting
// und rief für fremde Pfade `next ctx` auf. Damit war jede /api-Anfrage mit 200 und
// leerem Rumpf erledigt. Dieser Test startet `Api.webApp()` in einem In-Process-Testhost
// (Microsoft.AspNetCore.TestHost, kein offener Port, kein Netz) und verlangt je
// Remoting-Interface einen Aufruf, der einen JSON-Rumpf liefert.
//
// Die Interfaces kommen per Reflection aus `Shared.Api` (`remotingInterfaces`). Ein neues
// Interface ohne Zeile in `pipelineCalls` macht den Abgleich rot.
//
// Datenbank: In-Memory (USE_MEMORY_DB, siehe Main.fs und test.runsettings). Die Aufrufe
// sind so gewählt, dass sie ohne Token und ohne Zugangsdaten antworten, also nie YNAB
// oder Comdirect erreichen.

open System
open System.Net.Http
open System.Text
open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.FSharp.Reflection
open Expecto
open Giraffe

do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

/// Alle Fable.Remoting-Interfaces aus `Shared.Api`: Record-Typen, deren Felder sämtlich
/// Funktionen sind. Liefert Interface-Name und die Namen seiner Endpoints.
let remotingInterfaces () : (string * string list) list =
    let apiModule = typeof<Shared.Api.SettingsApi>.DeclaringType
    apiModule.GetNestedTypes()
    |> Array.filter (fun t ->
        FSharpType.IsRecord(t, true)
        && FSharpType.GetRecordFields(t, true) |> Array.forall (fun f -> FSharpType.IsFunction f.PropertyType))
    |> Array.map (fun t -> t.Name, FSharpType.GetRecordFields(t, true) |> Array.map (fun f -> f.Name) |> List.ofArray)
    |> Array.sortBy fst
    |> List.ofArray

/// Je Interface ein Aufruf: Methode, Pfad, Rumpf. Alle antworten ohne Token und ohne
/// Zugangsdaten aus der Datenbank.
/// POST mit JSON-Argumentliste wie der Fable-Client; ein GET auf eine unit-Methode endete
/// im Testhost beim ersten Aufruf mit einer NullReferenceException in Fable.Remoting.
let private pipelineCalls : Map<string, HttpMethod * string * string option> =
    Map.ofList [
        "SettingsApi", (HttpMethod.Post, "/api/SettingsApi/getSettings", Some "[]")
        "YnabApi", (HttpMethod.Post, "/api/YnabApi/getBudgets", Some "[]")
        "RulesApi", (HttpMethod.Post, "/api/RulesApi/getAllRules", Some "[]")
        "SyncApi", (HttpMethod.Post, "/api/SyncApi/getSyncHistory", Some "[3]")
        "LedgerApi", (HttpMethod.Post, "/api/LedgerApi/getLedgerAccounts", Some "[]")
        "BudgetApi", (HttpMethod.Post, "/api/BudgetApi/getBudgetMonth", Some "[\"kein-monat\"]")
    ]

let private withTestHost (body: HttpClient -> unit) =
    Persistence.initializeDatabase ()
    Ledger.Persistence.initializeLedgerSchema ()
    Budgeting.Persistence.initializeBudgetingSchema ()
    let builder = WebApplication.CreateBuilder()
    builder.WebHost.UseTestServer() |> ignore
    builder.Services.AddGiraffe() |> ignore
    use app = builder.Build()
    app.UseGiraffe(Server.Api.webApp())
    app.StartAsync().GetAwaiter().GetResult()
    try
        use client = app.GetTestClient()
        body client
    finally
        app.StopAsync().GetAwaiter().GetResult()

let private send (client: HttpClient) (meth: HttpMethod, path: string, body: string option) : int * string =
    use request = new HttpRequestMessage(meth, path)
    body |> Option.iter (fun b -> request.Content <- new StringContent(b, Encoding.UTF8, "application/json"))
    use response = client.SendAsync(request).GetAwaiter().GetResult()
    int response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

[<Tests>]
let pipelineTests =
    testSequenced
    <| testList "Remoting-Pipeline über Api.webApp (infrastructure-am805)" [

        testCase "jedes Remoting-Interface aus Shared.Api hat einen Pipeline-Aufruf" <| fun () ->
            let reflected = remotingInterfaces () |> List.map fst |> Set.ofList
            let covered = pipelineCalls |> Map.keys |> Set.ofSeq
            Expect.isNonEmpty reflected "Reflection findet die Remoting-Interfaces"
            Expect.isEmpty (Set.difference reflected covered) "Interfaces ohne Pipeline-Aufruf"
            Expect.isEmpty (Set.difference covered reflected) "Pipeline-Aufrufe für nicht mehr vorhandene Interfaces"

        testCase "jedes Remoting-Interface antwortet über Api.webApp mit JSON-Rumpf" <| fun () ->
            // Verhindert die Wiederkehr des Ausfalls vom 2026-10-29: mit `next ctx` statt
            // `skipPipeline` im Bot-Handler kam jede dieser Anfragen mit 200 und leerem Rumpf
            // zurück.
            withTestHost (fun client ->
                for KeyValue (name, call) in pipelineCalls do
                    let status, body = send client call
                    Expect.equal status 200 $"{name}: Status"
                    Expect.isFalse (String.IsNullOrWhiteSpace body) $"{name}: Rumpf ist leer"
                    let parsed =
                        try
                            use _ = JsonDocument.Parse body
                            true
                        with _ -> false
                    Expect.isTrue parsed $"{name}: Rumpf ist kein JSON: {body}")

        testCase "der Bot-Handler in Api.webApp beantwortet /bot ohne Token mit 401" <| fun () ->
            // Gegenprobe: der Bot-Handler sitzt wirklich in derselben Pipeline.
            withTestHost (fun client ->
                let status, _ = send client (HttpMethod.Get, "/bot/categories", None)
                Expect.equal status 401 "Bot-Pfad ohne Token")
    ]
