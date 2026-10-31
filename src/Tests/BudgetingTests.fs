module Tests.BudgetingTests

// budgeting-k7wmd: Decoder für YNABs Monats-Endpunkt, Gruppieren der Kategorien,
// Monatsumrechnung und die Prüfung des Monats-Schlüssels an der Grenze. Alles ohne Netz.

open System
open Expecto
open Shared.Budgeting
open Server.Budgeting

let private eur (milliunits: int64) : Shared.Domain.Money =
    { Amount = decimal milliunits / 1000m; Currency = "EUR" }

/// Fester Monats-Antworttext. Kategorien: mit Fehlbetrag 0, positivem Fehlbetrag und null,
/// pausiert und nicht pausiert, ausgeblendet, gelöscht und intern.
let private monthJson =
    """{ "data": { "month": {
      "month": "2026-10-01",
      "to_be_budgeted": -12340,
      "categories": [
        { "id": "11111111-1111-1111-1111-111111111111", "category_group_name": "Alltag", "name": "Essen k7wmd",
          "hidden": false, "deleted": false, "internal": false,
          "budgeted": 1600000, "activity": -921100, "balance": 1120090,
          "goal_under_funded": 0, "goal_snoozed_at": null },
        { "id": "22222222-2222-2222-2222-222222222222", "category_group_name": "Alltag", "name": "Ausflüge k7wmd",
          "hidden": false, "deleted": false, "internal": false,
          "budgeted": 100000, "activity": -10000, "balance": 185490,
          "goal_under_funded": 25500, "goal_snoozed_at": "2026-10-12T08:00:00+00:00" },
        { "id": "33333333-3333-3333-3333-333333333333", "category_group_name": "Rücklagen", "name": "Macbook k7wmd",
          "hidden": false, "deleted": false, "internal": false,
          "budgeted": 0, "activity": 0, "balance": 3200000,
          "goal_under_funded": null, "goal_snoozed_at": null },
        { "id": "44444444-4444-4444-4444-444444444444", "category_group_name": "Rücklagen", "name": "Versteckt k7wmd",
          "hidden": true, "deleted": false, "internal": false,
          "budgeted": 0, "activity": 0, "balance": 0, "goal_under_funded": null, "goal_snoozed_at": null }
      ] } } }"""

let private decoded : YnabMonth =
    match Thoth.Json.Net.Decode.fromString Server.YnabClient.Decoders.monthDecoder monthJson with
    | Ok m -> m
    | Error e -> failwith e

let private category groupName name hidden deleted internal' : YnabMonthCategory =
    { Id = Guid.NewGuid()
      Name = name
      GroupName = groupName
      Assigned = eur 1000L
      Activity = eur -500L
      Available = eur 500L
      Underfunded = None
      Snoozed = false
      Hidden = hidden
      Deleted = deleted
      Internal = internal' }

let private monthOf categories : YnabMonth =
    { Month = "2026-10-01"; ToBeBudgeted = eur 0L; Categories = categories }

let private decoderTests =
    testList "Monats-Decoder (k7wmd)" [
        test "Zu verteilen kommt als negatives Money in Euro an" {
            Expect.equal decoded.ToBeBudgeted (eur -12340L) "to_be_budgeted -12340 Milliunits"
            Expect.equal decoded.ToBeBudgeted.Amount -12.34m "ohne Rundungsrest"
            Expect.equal decoded.Month "2026-10-01" "Monat"
        }
        test "Zugewiesen, Aktivität und Verfügbar sind Milliunits durch 1000" {
            let c = decoded.Categories.[0]
            Expect.equal c.Assigned.Amount 1600m "budgeted"
            Expect.equal c.Activity.Amount -921.1m "activity"
            Expect.equal c.Available.Amount 1120.09m "balance"
            Expect.equal c.GroupName "Alltag" "Gruppe"
        }
        test "goal_under_funded 0 ist Some 0, positiv ist Some, null ist None" {
            Expect.equal decoded.Categories.[0].Underfunded (Some (eur 0L)) "0 bleibt Some 0"
            Expect.equal decoded.Categories.[1].Underfunded (Some (eur 25500L)) "positiv"
            Expect.equal decoded.Categories.[2].Underfunded None "null"
        }
        test "goal_snoozed_at gesetzt ist pausiert, null nicht" {
            Expect.isTrue decoded.Categories.[1].Snoozed "gesetzt"
            Expect.isFalse decoded.Categories.[0].Snoozed "null"
        }
        test "hidden wird gelesen, fehlende Flags gelten als false" {
            Expect.isTrue decoded.Categories.[3].Hidden "hidden"
            let minimal =
                """{ "id": "55555555-5555-5555-5555-555555555555", "name": "x", "budgeted": 0, "activity": 0, "balance": 0 }"""
            match Thoth.Json.Net.Decode.fromString Server.YnabClient.Decoders.monthCategoryDecoder minimal with
            | Ok c ->
                Expect.isFalse c.Hidden "hidden fehlt"
                Expect.isFalse c.Deleted "deleted fehlt"
                Expect.equal c.Underfunded None "kein Target"
            | Error e -> failwith e
        }
        test "Der bestehende Ready-to-Assign-Decoder liest dieselbe Antwort unverändert" {
            match Thoth.Json.Net.Decode.fromString Server.YnabClient.Decoders.monthToBeBudgetedDecoder monthJson with
            | Ok m -> Expect.equal m (eur -12340L) "to_be_budgeted"
            | Error e -> failwith e
        }
    ]

let private groupingTests =
    testList "Gruppieren (k7wmd)" [
        test "Gruppen und Kategorien behalten YNABs Reihenfolge" {
            let month =
                monthOf [
                    category "B" "b1" false false false
                    category "A" "a1" false false false
                    category "B" "b2" false false false
                ]
            let result = Domain.toBudgetMonth month
            Expect.equal (result.Groups |> List.map (fun g -> g.Name)) [ "B"; "A" ] "Gruppen in Reihenfolge des ersten Auftretens"
            Expect.equal (result.Groups.[0].Categories |> List.map (fun c -> c.Name)) [ "b1"; "b2" ] "Kategorien in Reihenfolge"
        }
        test "Ausgeblendete, gelöschte und interne Kategorien fehlen" {
            let month =
                monthOf [
                    category "A" "sichtbar" false false false
                    category "A" "versteckt" true false false
                    category "A" "gelöscht" false true false
                    category "A" "Inflow" false false true
                ]
            let names = (Domain.toBudgetMonth month).Groups |> List.collect (fun g -> g.Categories |> List.map (fun c -> c.Name))
            Expect.equal names [ "sichtbar" ] "nur die sichtbare"
        }
        test "Eine Gruppe ohne sichtbare Kategorie erscheint nicht" {
            let month =
                monthOf [
                    category "Leer" "versteckt" true false false
                    category "Voll" "da" false false false
                ]
            Expect.equal ((Domain.toBudgetMonth month).Groups |> List.map (fun g -> g.Name)) [ "Voll" ] "leere Gruppe fehlt"
        }
        test "Ein Monat ohne Kategorien hat keine Gruppen und behält Zu verteilen" {
            let result = Domain.toBudgetMonth { monthOf [] with ToBeBudgeted = eur -5000L }
            Expect.isEmpty result.Groups "keine Gruppen"
            Expect.equal result.ToBeBudgeted.Amount -5m "Zu verteilen bleibt"
        }
        test "Der echte Beispielmonat ergibt zwei Gruppen mit je ihren Kategorien" {
            let result = Domain.toBudgetMonth decoded
            Expect.equal (result.Groups |> List.map (fun g -> g.Name, g.Categories.Length)) [ "Alltag", 2; "Rücklagen", 1 ] "Gruppen"
            Expect.equal result.Groups.[0].Categories.[1].Underfunded (Some (eur 25500L)) "Fehlbetrag bleibt erhalten"
            Expect.isTrue result.Groups.[0].Categories.[1].Snoozed "Pausiert bleibt erhalten"
        }
        test "Gruppennamen werden gekürzt (YNAB liefert Leerzeichen am Ende)" {
            let month = monthOf [ category "Medien + Software " "m" false false false ]
            Expect.equal (Domain.toBudgetMonth month).Groups.[0].Name "Medien + Software" "gekürzt"
        }
    ]

let private monthKeyTests =
    testList "Monatsumrechnung (k7wmd)" [
        test "Dieser Monat ist der Erste des laufenden Monats" {
            Expect.equal (Domain.monthKey (DateTime(2026, 10, 26)) CurrentMonth) "2026-10-01" "dieser"
        }
        test "Nächster Monat ist der Erste des Folgemonats" {
            Expect.equal (Domain.monthKey (DateTime(2026, 10, 30)) NextMonth) "2026-11-01" "nächster"
        }
        test "Von Dezember geht es in den Januar des Folgejahres" {
            Expect.equal (Domain.monthKey (DateTime(2026, 12, 15)) NextMonth) "2027-01-01" "Jahreswechsel"
        }
        test "Am Monatsletzten bleibt der Schlüssel im selben Monat" {
            Expect.equal (Domain.monthKey (DateTime(2026, 1, 31)) NextMonth) "2026-02-01" "31. Januar"
        }
    ]

let private validationTests =
    testList "Monats-Schlüssel an der Grenze (k7wmd)" [
        test "current und next sind gültig" {
            Expect.equal (Server.Validation.validateBudgetMonth "current") (Ok CurrentMonth) "current"
            Expect.equal (Server.Validation.validateBudgetMonth "next") (Ok NextMonth) "next"
        }
        test "Alles andere wird abgelehnt" {
            for bad in [ ""; "2026-10-01"; "previous"; "Current"; " next" ] do
                match Server.Validation.validateBudgetMonth bad with
                | Error msg -> Expect.stringContains msg "Ungültiger Monat" $"'{bad}' abgelehnt"
                | Ok _ -> failtest $"'{bad}' hätte abgelehnt werden müssen"
        }
        test "Der Schlüssel einer Wahl liest sich wieder zur Wahl" {
            for choice in [ CurrentMonth; NextMonth ] do
                Expect.equal (parseMonthChoice (monthChoiceKey choice)) (Ok choice) "Rundlauf"
        }
    ]

let private okMonthJson = monthJson

let private runMonth status body =
    Server.YnabClient.getMonthWith (fun () -> async { return status, body }) "2026-10-01"
    |> Async.RunSynchronously

let private errorMappingTests =
    testList "YNAB-Fehler beim Monat (k7wmd)" [
        test "200 mit gültigem JSON liefert den Monat" {
            match runMonth 200 okMonthJson with
            | Ok m -> Expect.equal m.Month "2026-10-01" "Monat"
            | Error e -> failtestf "Ok erwartet: %A" e
        }
        test "200 mit kaputtem JSON ist eine ungültige Antwort" {
            match runMonth 200 "{}" with
            | Error (Shared.Domain.YnabError.InvalidResponse _) -> ()
            | other -> failtestf "InvalidResponse erwartet: %A" other
        }
        test "401 ist Unauthorized" {
            match runMonth 401 "{}" with
            | Error (Shared.Domain.YnabError.Unauthorized _) -> ()
            | other -> failtestf "401: %A" other
        }
        test "404 nennt den Monat" {
            match runMonth 404 "{}" with
            | Error (Shared.Domain.YnabError.InvalidResponse msg) -> Expect.stringContains msg "2026-10-01" "Monat in der Meldung"
            | other -> failtestf "404: %A" other
        }
        test "429 ist RateLimitExceeded" {
            match runMonth 429 "{}" with
            | Error (Shared.Domain.YnabError.RateLimitExceeded _) -> ()
            | other -> failtestf "429: %A" other
        }
        test "Anderer Status ist ein Netzfehler mit Status" {
            match runMonth 503 "weg" with
            | Error (Shared.Domain.YnabError.NetworkError msg) -> Expect.stringContains msg "503" "Status"
            | other -> failtestf "503: %A" other
        }
        test "Eine Ausnahme beim Abruf wird zum Netzfehler" {
            let result =
                Server.YnabClient.getMonthWith (fun () -> async { return raise (System.Net.Http.HttpRequestException "kaputt") }) "2026-10-01"
                |> Async.RunSynchronously
            match result with
            | Error (Shared.Domain.YnabError.NetworkError msg) -> Expect.stringContains msg "kaputt" "Ursache"
            | other -> failtestf "Netzfehler erwartet: %A" other
        }
    ]

let private apiPathTests =
    let fetchWith result = fun (_: string) (_: Shared.Domain.YnabBudgetId) (_: string) -> async { return result }
    let call token budget key fetch =
        Server.Api.budgetMonthFor token budget (DateTime(2026, 10, 26)) key fetch |> Async.RunSynchronously
    testList "Weg der Budget-Seite (k7wmd)" [
        test "429 kommt als lesbare Meldung an" {
            match call (Some "t") (Some "b") "current" (fetchWith (Error (Shared.Domain.YnabError.RateLimitExceeded 60))) with
            | Error msg -> Expect.equal msg "Rate-Limit (HTTP 429), erneut in 60 Sekunden" "deutsch"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Fehlender Token wird gemeldet, ohne YNAB zu fragen" {
            let fetch = fun _ _ _ -> async { return failwith "darf nicht aufgerufen werden" }
            match call None (Some "b") "current" fetch with
            | Error msg -> Expect.stringContains msg "YNAB-Token" "Token fehlt"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Fehlendes Standard-Budget wird gemeldet" {
            match call (Some "t") None "current" (fetchWith (Ok decoded)) with
            | Error msg -> Expect.stringContains msg "Standard-Budget" "Budget fehlt"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Ungültiger Monat wird abgelehnt, bevor YNAB gefragt wird" {
            let fetch = fun _ _ _ -> async { return failwith "darf nicht aufgerufen werden" }
            match call (Some "t") (Some "b") "2026-10-01" fetch with
            | Error msg -> Expect.stringContains msg "Ungültiger Monat" "Grenze"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Der nächste Monat fragt YNAB mit dem Ersten des Folgemonats" {
            let mutable seen = ""
            let fetch = fun _ _ key -> async { seen <- key; return Ok decoded }
            match call (Some "t") (Some "b") "next" fetch with
            | Ok m -> Expect.equal m.Groups.Length 2 "gruppiert"
            | Error e -> failtest e
            Expect.equal seen "2026-11-01" "Schlüssel"
        }
    ]

[<Tests>]
let tests =
    testList "Budgeting (k7wmd)" [ decoderTests; groupingTests; monthKeyTests; validationTests; errorMappingTests; apiPathTests ]
