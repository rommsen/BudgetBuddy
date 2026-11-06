module Tests.BudgetMonthStartTests

// budgeting-m3x7f: "Ausblenden" an der Leiste Monatserster je Monat auf dem Server. Zustand, kein Ereignis
// (ADR 0083): eine kleine Tabelle, geschrieben über `hideMonthStart`, gelesen über den Rahmen der Budget-Seite.

open System
open Expecto
open Microsoft.Data.Sqlite
open Server.Budgeting

do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

let private november = "2026-11-01"
let private december = "2026-12-01"
let private now = DateTime(2026, 11, 2, 7, 30, 0, DateTimeKind.Utc)
let private run x = Async.RunSynchronously x

/// Eine eigene In-Memory-Datenbank mit der Tabelle; die erste Verbindung hält sie am Leben.
let private isolated () : SqliteConnection * (unit -> SqliteConnection) =
    let connectionString = $"Data Source=month-start-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
    let anchor = new SqliteConnection(connectionString)
    anchor.Open()
    Budgeting.Persistence.createMonthStartHiddenSchemaOn anchor
    anchor, (fun () -> new SqliteConnection(connectionString))

let private hidden (connect: unit -> SqliteConnection) (fromMonth: string) =
    use conn = connect ()
    Budgeting.Persistence.loadMonthStartHiddenOn conn fromMonth

/// Der Rahmen mit einem Fake-YNAB und dem Ausblenden aus der Datenbank.
let private frameWith (load: string -> Async<Result<string list, string>>) =
    let port : QueryHandlers.BudgetFramePort =
        { FetchMonths = fun () -> async { return Ok [ november; december ] }
          FetchCategoryOrder = fun () -> async { return Ok [] }
          LoadMonthStartHidden = load }
    match QueryHandlers.frame port (DateOnly(2026, 11, 2)) |> run with
    | Ok frame -> frame
    | Error e -> failtest $"Rahmen erwartet: {e}"

[<Tests>]
let tests =
    testList "Leiste Monatserster: Ausblenden je Monat" [
        testCase "Ausblenden hält den Monat fest, der Rahmen liest ihn, ein anderer Monat gilt nicht" <| fun () ->
            let anchor, connect = isolated ()
            use _ = anchor
            Expect.equal (Server.Api.hideMonthStartWith connect now november |> run) (Ok ()) "gespeichert"
            let load fromMonth = async { return Ok (hidden connect fromMonth) }
            Expect.equal (frameWith load).MonthStartHidden [ november ] "der Rahmen nennt den November"
            Expect.isEmpty (hidden connect december) "ab Dezember gelesen ist nichts ausgeblendet"

        testCase "Wiederholtes Ausblenden ändert nichts und bleibt bei einer Zeile" <| fun () ->
            let anchor, connect = isolated ()
            use _ = anchor
            Server.Api.hideMonthStartWith connect now november |> run |> ignore
            Expect.equal (Server.Api.hideMonthStartWith connect (now.AddHours 1.0) november |> run) (Ok ()) "zweites Mal"
            Expect.equal (hidden connect november) [ november ] "eine Zeile"
            Server.Api.hideMonthStartWith connect now december |> run |> ignore
            Expect.equal (hidden connect november) [ november; december ] "der Dezember nimmt den November nicht weg"

        testCase "Ein ungültiger Monat wird abgelehnt und nichts gespeichert" <| fun () ->
            let anchor, connect = isolated ()
            use _ = anchor
            match Server.Api.hideMonthStartWith connect now "2026-13-01" |> run with
            | Error message -> Expect.stringContains message "2026-13-01" "nennt den Monat"
            | Ok () -> failtest "Fehler erwartet"
            Expect.isEmpty (hidden connect "2000-01-01") "nichts gespeichert"

        testCase "Ein Monat vor dem laufenden steht nicht im Rahmen" <| fun () ->
            let anchor, connect = isolated ()
            use _ = anchor
            Server.Api.hideMonthStartWith connect now "2026-10-01" |> run |> ignore
            let load fromMonth = async { return Ok (hidden connect fromMonth) }
            Expect.isEmpty (frameWith load).MonthStartHidden "Oktober ist vergangen"

        testCase "Ist das Ausblenden nicht lesbar, liefert der Rahmen trotzdem, ohne ausgeblendeten Monat" <| fun () ->
            let frame = frameWith (fun _ -> async { return Error "kaputt" })
            Expect.isEmpty frame.MonthStartHidden "die Leiste steht dann einmal zu viel"
            Expect.equal frame.Months [ november; december ] "der Rahmen selbst kommt"

        testCase "Das Schema entsteht beim Start und lässt sich wiederholen" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            Budgeting.Persistence.initializeBudgetingSchema ()
            use conn = global.Persistence.getConnection ()
            let exists =
                Dapper.SqlMapper.ExecuteScalar<int64>(
                    conn,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'budget_month_start_hidden'")
            Expect.equal exists 1L "Tabelle vorhanden"
            Expect.isOk (QueryHandlers.loadMonthStartHidden november |> run) "der Live-Leser liest sie"
    ]
