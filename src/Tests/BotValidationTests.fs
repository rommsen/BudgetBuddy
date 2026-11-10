module BotValidationTests

// Reine Teile der Bot-Schnittstelle (ledger-b0b7q, ADR 0066): Eingabeprüfung der Vorschau,
// Betragsgrenze, Konfiguration, Herkunft Bob. Ohne I/O. Die Fachregeln (Datumsfenster,
// Tagesgrenze, Berliner Tag, Kategorie-Auflösung) prüft BotDomainTests (ledger-ct981).

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Bot.Domain
open Server.Validation

let private today = DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc)

let private validJson =
    """{"amount": 50, "direction": "outflow", "payee": "Anastasia", "category": "Haushaltshilfe", "date": "2026-10-28", "memo": "Oktober"}"""

/// Die Vorschau-Prüfung wie in der App: mit dem Datumsfenster der Domain zum Tag `today`.
let private parseAt (json: string) = parseBotPreviewRequest (checkBotBookingDate today) json

let private fieldsOf (result: Result<BotPreviewInput, BotFieldError list>) : string list =
    match result with
    | Error errors -> errors |> List.map (fun e -> e.BotField) |> List.sort
    | Ok _ -> []

[<Tests>]
let botParseTests =
    testList "Bot Vorschau: Eingabeprüfung" [

        testCase "gültige Eingabe wird übernommen, Betrag in Euro, Datum als UTC-Tag" <| fun () ->
            match parseAt validJson with
            | Ok input ->
                Expect.equal input.AmountEur 50m "50 Euro (entspricht -50000 Milliunits bei Ausgabe)"
                Expect.equal input.Direction BotOutflow "Richtung"
                Expect.equal input.Payee "Anastasia" "Payee"
                Expect.equal input.Category "Haushaltshilfe" "Kategorie als Rohtext"
                Expect.equal input.Date (DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc)) "Datum"
                Expect.equal input.Memo (Some "Oktober") "Memo"
            | Error e -> failtest $"sollte gelten, war {e}"

        testCase "Memo ist optional, leeres Memo wird zu None" <| fun () ->
            let json = """{"amount": 1.5, "direction": "inflow", "payee": "x", "category": "c", "date": "2026-10-01", "memo": "  "}"""
            match parseAt json with
            | Ok input ->
                Expect.equal input.Memo None "kein Memo"
                Expect.equal input.Direction BotInflow "inflow"
            | Error e -> failtest $"{e}"

        testCase "fehlende Pflichtfelder nennen ihre Feldnamen" <| fun () ->
            Expect.equal
                (fieldsOf (parseAt "{}"))
                [ "amount"; "category"; "date"; "direction"; "payee" ]
                "alle fünf Pflichtfelder"

        testCase "falsches Datum, unbekannte direction, Betrag als Text ergeben je einen Feldfehler" <| fun () ->
            let json = """{"amount": "50", "direction": "sideways", "payee": "x", "category": "c", "date": "28.10.2026"}"""
            Expect.equal (fieldsOf (parseAt json)) [ "amount"; "date"; "direction" ] "drei Felder"

        testCase "Datum in der Zukunft und über fünf Jahre zurück werden abgelehnt" <| fun () ->
            let withDate d = $"""{{"amount": 1, "direction": "outflow", "payee": "x", "category": "c", "date": "{d}"}}"""
            Expect.equal (fieldsOf (parseAt (withDate "2026-10-29"))) [ "date" ] "Zukunft"
            Expect.equal (fieldsOf (parseAt (withDate "2021-10-27"))) [ "date" ] "zu alt"
            Expect.isOk (parseAt (withDate "2026-09-27")) "heute minus 31 Tage ist erlaubt"
            Expect.equal (fieldsOf (parseAt (withDate "2026-09-26"))) [ "date" ] "heute minus 32 Tage"
            Expect.isOk (parseAt (withDate "2026-10-28")) "heute ist erlaubt"

        testCase "die Datumsregel kommt von außen, ihr Fehler wird zum Feldfehler date" <| fun () ->
            // Validation.fs prüft nur die Form; das Fenster liefert die Domain (ledger-ct981).
            match parseBotPreviewRequest (fun _ -> Error "Regel sagt nein") validJson with
            | Error [ e ] ->
                Expect.equal e.BotField "date" "Feld"
                Expect.equal e.BotProblem "Regel sagt nein" "Meldung der Regel"
            | other -> failtest $"erwartet genau einen Feldfehler date, war {other}"
            let seen = ref None
            parseBotPreviewRequest (fun d -> seen.Value <- Some d; Ok ()) validJson |> ignore
            Expect.equal seen.Value (Some (DateTime(2026, 10, 28))) "die Regel bekommt das gelesene Datum"

        testCase "ein Datum außerhalb des Fensters wird mit den übrigen Feldfehlern gesammelt" <| fun () ->
            let json = """{"amount": "50", "direction": "outflow", "payee": "x", "category": "c", "date": "2026-10-29"}"""
            Expect.equal (fieldsOf (parseAt json)) [ "amount"; "date" ] "Betrag und Zukunft in einer Antwort"

        testCase "mehr als zwei Nachkommastellen im Betrag werden abgelehnt" <| fun () ->
            let json = """{"amount": 10.005, "direction": "outflow", "payee": "x", "category": "c", "date": "2026-10-01"}"""
            Expect.equal (fieldsOf (parseAt json)) [ "amount" ] "amount"

        testCase "ein mitgeschicktes Konto wird abgelehnt, das Konto ist fest" <| fun () ->
            let json = """{"amount": 1, "direction": "outflow", "payee": "x", "category": "c", "date": "2026-10-01", "account": "Girokonto"}"""
            Expect.equal (fieldsOf (parseAt json)) [ "account" ] "account"

        testCase "kein JSON und kein Objekt ergeben den Feldnamen body" <| fun () ->
            Expect.equal (fieldsOf (parseAt "nicht json")) [ "body" ] "kein JSON"
            Expect.equal (fieldsOf (parseAt "[1,2]")) [ "body" ] "kein Objekt"

        testCase "zu langer Payee und zu langes Memo werden abgelehnt" <| fun () ->
            let long n = String('a', n)
            let json = $"""{{"amount": 1, "direction": "outflow", "payee": "{long 201}", "category": "c", "date": "2026-10-01", "memo": "{long 501}"}}"""
            Expect.equal (fieldsOf (parseAt json)) [ "memo"; "payee" ] "beide"
    ]

[<Tests>]
let botAmountTests =
    testList "Bot Vorschau: Betragsgrenze" [

        testCase "die Grenze selbst ist erlaubt (500 Euro entspricht 500000 Milliunits)" <| fun () ->
            Expect.isOk (checkBotAmount 500m 500m) "genau 500 Euro"

        testCase "ein Cent über der Grenze wird abgelehnt (500,01 Euro)" <| fun () ->
            Expect.isError (checkBotAmount 500m 500.01m) "über 500 Euro"

        testCase "Betrag 0 und negativer Betrag werden abgelehnt" <| fun () ->
            Expect.isError (checkBotAmount 500m 0m) "0 Euro"
            Expect.isError (checkBotAmount 500m -5m) "-5 Euro (-5000 Milliunits)"

        testCase "die Grenze ist eine Funktion des Arguments, nicht fest 500" <| fun () ->
            Expect.isOk (checkBotAmount 1000m 750m) "750 Euro bei Grenze 1000"
            Expect.isError (checkBotAmount 100m 150m) "150 Euro bei Grenze 100"
    ]

[<Tests>]
let botConfigTests =
    testList "Bot Konfiguration und Herkunft" [

        testCase "ohne Token (null, leer, Leerraum) bleibt die Schnittstelle abgeschaltet" <| fun () ->
            Expect.equal (Server.ServerConfig.resolveBotToken null) None "null"
            Expect.equal (Server.ServerConfig.resolveBotToken "") None "leer"
            Expect.equal (Server.ServerConfig.resolveBotToken "   ") None "Leerraum"
            Expect.equal (Server.ServerConfig.resolveBotToken " abc ") (Some "abc") "getrimmt"

        testCase "Actor Bob überlebt die Persistenzform (Codec-Rundlauf)" <| fun () ->
            let kind, rule = actorToColumns Bob
            Expect.equal (kind, rule) ("Bob", None) "Spalten"
            Expect.equal (actorFromColumns kind rule) (Ok Bob) "zurück"

        testCase "Bob ist eine eigene Herkunft mit Anzeigename Bob, nicht Quick Add" <| fun () ->
            Expect.equal (Ledger.QueryHandlers.ReadModel.originOf Bob) AddedByBob "Herkunft"
            Expect.equal (originLabel AddedByBob) "Bob" "Anzeigename"
            Expect.notEqual (Ledger.QueryHandlers.ReadModel.originOf Bob) (Ledger.QueryHandlers.ReadModel.originOf QuickAdd) "getrennt"
    ]
