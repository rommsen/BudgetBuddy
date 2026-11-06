module Tests.BudgetingTests

// budgeting-k7wmd: Decoder für YNABs Monats-Endpunkt, Gruppieren der Kategorien,
// Monatsumrechnung und die Prüfung des Monats-Schlüssels an der Grenze. Alles ohne Netz.

open System
open Expecto
open Shared.Domain
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

let private decodeCategory (goalFields: string) : YnabMonthCategory =
    let separator = if goalFields = "" then "" else ","
    let json =
        $"""{{ "id": "55555555-5555-5555-5555-555555555555", "name": "x", "budgeted": 0, "activity": 0, "balance": 0 {separator} {goalFields} }}"""
    match Thoth.Json.Net.Decode.fromString Server.YnabClient.Decoders.monthCategoryDecoder json with
    | Ok c -> c
    | Error e -> failwith e

let private category groupName name hidden deleted internal' : YnabMonthCategory =
    { Id = Guid.NewGuid()
      Name = name
      GroupName = groupName
      Assigned = eur 1000L
      Activity = eur -500L
      Available = eur 500L
      Target = None
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
        // budgeting-kjsht
        test "goal_target mit Kadenz und Häufigkeit ergibt das Target" {
            let c = decodeCategory """ "goal_type": "NEED", "goal_target": 120000, "goal_cadence": 1, "goal_cadence_frequency": 3 """
            let expected : Target = { Goal = { Amount = 120m; Currency = "EUR" }; Rhythm = Months 3; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }
            Expect.equal c.Target (Some expected) "Betrag und Rhythmus"
        }
        test "goal_target null oder goal_type null ergibt kein Target" {
            let noAmount = decodeCategory """ "goal_type": "NEED", "goal_target": null, "goal_cadence": 1 """
            Expect.equal noAmount.Target None "goal_target null"
            let noType = decodeCategory """ "goal_type": null, "goal_target": 120000, "goal_cadence": 1 """
            Expect.equal noType.Target None "goal_type null"
            let nothing = decodeCategory ""
            Expect.equal nothing.Target None "keine goal-Felder"
        }
        test "Fehlende Kadenz-Felder lassen die Kategorie nicht scheitern" {
            let c = decodeCategory """ "goal_type": "NEED", "goal_target": 120000 """
            Expect.equal (c.Target |> Option.map (fun t -> t.Rhythm)) (Some NoRhythm) "nur der Betrag"
            let nulls = decodeCategory """ "goal_type": "MF", "goal_target": 120000, "goal_cadence": null, "goal_cadence_frequency": null """
            Expect.equal (nulls.Target |> Option.map (fun t -> t.Rhythm)) (Some (Months 1)) "MF ohne Kadenz"
        }
        test "hidden wird gelesen, fehlende Flags gelten als false" {
            Expect.isTrue decoded.Categories.[3].Hidden "hidden"
            let minimal =
                """{ "id": "55555555-5555-5555-5555-555555555555", "name": "x", "budgeted": 0, "activity": 0, "balance": 0 }"""
            match Thoth.Json.Net.Decode.fromString Server.YnabClient.Decoders.monthCategoryDecoder minimal with
            | Ok c ->
                Expect.isFalse c.Hidden "hidden fehlt"
                Expect.isFalse c.Deleted "deleted fehlt"
            | Error e -> failwith e
        }
        test "Der bestehende Ready-to-Assign-Decoder liest dieselbe Antwort unverändert" {
            match Thoth.Json.Net.Decode.fromString Server.YnabClient.Decoders.monthToBeBudgetedDecoder monthJson with
            | Ok m -> Expect.equal m (eur -12340L) "to_be_budgeted"
            | Error e -> failwith e
        }
    ]

let private rhythmTests =
    let rhythmOf = Server.YnabClient.Decoders.targetRhythm
    testList "Rhythmus aus YNABs Kadenz (kjsht)" [
        for name, goalType, cadence, frequency, expected in
            [ "wöchentlich", "NEED", 2, 1, Weeks 1
              "alle 2 Wochen", "NEED", 2, 2, Weeks 2
              "monatlich", "NEED", 1, 1, Months 1
              "Häufigkeit 0 zählt wie 1", "NEED", 1, 0, Months 1
              "monatlich mal Häufigkeit", "NEED", 1, 3, Months 3
              "jährlich", "NEED", 13, 1, Years 1
              "jährlich mal Häufigkeit", "NEED", 13, 2, Years 2
              "Kadenz 3 ist alle 2 Monate", "NEED", 3, 1, Months 2
              "Kadenz 4 ist alle 3 Monate", "NEED", 4, 1, Months 3
              "Kadenz 12 ist alle 11 Monate", "NEED", 12, 1, Months 11
              "Kadenz 14 ist alle 2 Jahre", "NEED", 14, 1, Years 2
              "Kadenz 0 bei MF ist monatlich", "MF", 0, 0, Months 1
              "Kadenz 0 ohne MF hat keinen Rhythmus", "TB", 0, 1, NoRhythm
              "unbekannte Kadenz hat keinen Rhythmus", "NEED", 15, 1, NoRhythm ] do
            test name { Expect.equal (rhythmOf goalType cadence frequency) expected name }
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
            let result = BudgetEventsFixture.toBudgetMonthOfYnab Set.empty month
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
            let names = (BudgetEventsFixture.toBudgetMonthOfYnab Set.empty month).Groups |> List.collect (fun g -> g.Categories |> List.map (fun c -> c.Name))
            Expect.equal names [ "sichtbar" ] "nur die sichtbare"
        }
        test "Eine Gruppe ohne sichtbare Kategorie erscheint nicht" {
            let month =
                monthOf [
                    category "Leer" "versteckt" true false false
                    category "Voll" "da" false false false
                ]
            Expect.equal ((BudgetEventsFixture.toBudgetMonthOfYnab Set.empty month).Groups |> List.map (fun g -> g.Name)) [ "Voll" ] "leere Gruppe fehlt"
        }
        // budgeting-kjsht
        // budgeting-eh8xe: die Seite zeigt BBs Regel, YNABs Goal zählt nicht.
        test "BBs Target gelangt in die angezeigte Kategorie, YNABs Goal nicht" {
            let target : Target = { Goal = eur 50000L; Rhythm = Weeks 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = Some 1; CreatedIn = None }
            let goal = { target with Goal = eur 70000L }
            let withBb = category "Alltag" "Essen" false false false
            let ynabOnly = { category "Alltag" "Miete" false false false with Target = Some goal }
            let month = monthOf [ { withBb with Target = Some goal }; ynabOnly ]
            let shown = (Domain.toBudgetMonth (Map.ofList [ withBb.Id, target ]) Set.empty month).Groups.Head.Categories
            Expect.equal (shown |> List.map (fun c -> c.Target)) [ Some target; None ] "BBs Regel, ohne Regel kein Target"
        }
        test "Ein Monat ohne Kategorien hat keine Gruppen und behält Zu verteilen" {
            let result = BudgetEventsFixture.toBudgetMonthOfYnab Set.empty { monthOf [] with ToBeBudgeted = eur -5000L }
            Expect.isEmpty result.Groups "keine Gruppen"
            Expect.equal result.ToBeBudgeted.Amount -5m "Zu verteilen bleibt"
        }
        test "Der echte Beispielmonat ergibt zwei Gruppen mit je ihren Kategorien" {
            let result = BudgetEventsFixture.toBudgetMonthOfYnab Set.empty decoded
            Expect.equal (result.Groups |> List.map (fun g -> g.Name, g.Categories.Length)) [ "Alltag", 2; "Rücklagen", 1 ] "Gruppen"
            // budgeting-eh8xe: `goal_snoozed_at` ist gesetzt, eine Pause in YNAB wirkt in BB nicht.
            Expect.equal result.Groups.[0].Categories.[1].Pause None "eine Pause in YNAB ändert in BB nichts"
        }
        test "Gruppennamen werden gekürzt (YNAB liefert Leerzeichen am Ende)" {
            let month = monthOf [ category "Medien + Software " "m" false false false ]
            Expect.equal (BudgetEventsFixture.toBudgetMonthOfYnab Set.empty month).Groups.[0].Name "Medien + Software" "gekürzt"
        }
    ]

let private berlin = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"

let private monthKindTests =
    testList "Monatsart (ADR 0074)" [
        test "Vergangen, aktuell und künftig folgen aus dem Vergleich, auch über den Jahreswechsel" {
            let current = "2026-11-01"
            Expect.equal (monthKind current "2026-10-01") PastMonth "Vormonat"
            Expect.equal (monthKind current "2026-11-01") CurrentMonth "laufend"
            Expect.equal (monthKind current "2026-12-01") FutureMonth "Folgemonat"
            Expect.equal (monthKind current "2025-12-01") PastMonth "Vorjahr"
        }
        test "Targets anwenden geht nur in vergangenen Monaten nicht" {
            Expect.isFalse (targetsAllowed PastMonth) "vergangen"
            Expect.isTrue (targetsAllowed CurrentMonth) "aktuell"
            Expect.isTrue (targetsAllowed FutureMonth) "künftig"
        }
        test "Der laufende Monat ist der Erste des Kalendertags" {
            Expect.equal (Domain.currentMonthKey (DateOnly(2026, 1, 31))) "2026-01-01" "Monatsletzter"
        }
        // Bug B1: der Monat hing an `DateTime.Today`. Im UTC-Container war am Monatsersten bis
        // 02:00 Uhr Berliner Zeit noch der Vormonat "dieser Monat", Targets anwenden schrieb dorthin.
        test "Kurz nach Mitternacht in Berlin gilt schon der neue Monat, auch wenn UTC noch im alten liegt" {
            let utc = DateTime(2026, 11, 30, 23, 30, 0, DateTimeKind.Utc)
            let today = Server.Budgeting.Domain.berlinToday berlin utc
            Expect.equal today (DateOnly(2026, 12, 1)) "Berliner Tag"
            Expect.equal (Domain.currentMonthKey today) "2026-12-01" "laufender Monat"
        }
        test "Kurz vor Mitternacht in Berlin bleibt es beim alten Monat" {
            let today = Server.Budgeting.Domain.berlinToday berlin (DateTime(2026, 11, 30, 22, 30, 0, DateTimeKind.Utc))
            Expect.equal today (DateOnly(2026, 11, 30)) "Berliner Tag"
        }
    ]

let private validationTests =
    testList "Monat an der Grenze (ADR 0074)" [
        test "Alles außer einem Monatsersten wird abgelehnt, die Meldung nennt den Wert" {
            for bad in [ "last"; "current"; ""; "2026-13-01"; "2026-11-15"; " 2026-11-01" ] do
                match Server.Validation.validateMonthKey bad with
                | Error msg ->
                    Expect.stringContains msg "Ungültiger Monat" $"'{bad}' abgelehnt"
                    Expect.stringContains msg $"'{bad}'" "nennt den Wert"
                | Ok _ -> failtest $"'{bad}' hätte abgelehnt werden müssen"
        }
        test "Ein alter Monatserster ist gültig" {
            Expect.equal (Server.Validation.validateMonthKey "2019-03-01") (Ok "2019-03-01") "2019"
        }
        test "Targets anwenden nimmt den laufenden und künftige Monate an" {
            let validate = Server.Validation.validateTargetsMonth "2026-11-01"
            Expect.equal (validate "2026-11-01") (Ok "2026-11-01") "laufend"
            Expect.equal (validate "2031-02-01") (Ok "2031-02-01") "künftig, das Fenster entscheidet YNAB"
        }
        test "Targets anwenden lehnt vergangene Monate mit Grund ab" {
            match Server.Validation.validateTargetsMonth "2026-11-01" "2026-10-01" with
            | Error msg -> Expect.stringContains msg "vergangenen Monaten" "Grund"
            | Ok _ -> failtest "Vormonat hätte abgelehnt werden müssen"
        }
        test "Targets anwenden lehnt ungültige Monate mit demselben Text wie das Lesen ab" {
            let key = "kein-monat"
            Expect.equal
                (Server.Validation.validateTargetsMonth "2026-11-01" key)
                (Server.Validation.validateMonthKey key)
                "ein Meldungstext"
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
        test "404 heißt Monat nicht offen" {
            match runMonth 404 "{}" with
            | Error ((Shared.Domain.YnabError.MonthNotOpen "2026-10-01") as e) ->
                Expect.equal (Shared.Domain.ynabErrorText e) "Monat nicht offen" "Text"
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
        let portFor token budgetId : CommandHandlers.YnabPort =
            { FetchMonth = fetch token budgetId
              SetAssigned = fun _ _ _ -> async { return Ok () }
              FetchCategoryGoals = fun () -> async { return Ok [] } }
        Server.Api.budgetMonthFor token budget key portFor (DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)) |> Async.RunSynchronously
    testList "Weg der Budget-Seite (k7wmd)" [
        test "429 kommt als lesbare Meldung an" {
            match call (Some "t") (Some "b") "2026-10-01" (fetchWith (Error (Shared.Domain.YnabError.RateLimitExceeded 60))) with
            | Error msg -> Expect.equal msg "Rate-Limit (HTTP 429), erneut in 60 Sekunden" "deutsch"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Fehlender Token wird gemeldet, ohne YNAB zu fragen" {
            let fetch = fun _ _ _ -> async { return failwith "darf nicht aufgerufen werden" }
            match call None (Some "b") "2026-10-01" fetch with
            | Error msg -> Expect.stringContains msg "YNAB-Token" "Token fehlt"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Fehlendes Standard-Budget wird gemeldet" {
            match call (Some "t") None "2026-10-01" (fetchWith (Ok decoded)) with
            | Error msg -> Expect.stringContains msg "Standard-Budget" "Budget fehlt"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Ungültiger Monat wird abgelehnt, bevor YNAB gefragt wird" {
            let fetch = fun _ _ _ -> async { return failwith "darf nicht aufgerufen werden" }
            match call (Some "t") (Some "b") "current" fetch with
            | Error msg -> Expect.stringContains msg "Ungültiger Monat" "Grenze"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Ein freier Monat wird genau so gelesen" {
            let mutable seen = ""
            let fetch = fun _ _ key -> async { seen <- key; return Ok decoded }
            match call (Some "t") (Some "b") "2019-03-01" fetch with
            | Ok m -> Expect.equal m.Groups.Length 2 "gruppiert"
            | Error e -> failtest e
            Expect.equal seen "2019-03-01" "genau dieser Monat"
        }
    ]

let private runMonths status body =
    Server.YnabClient.getMonthsWith (fun () -> async { return status, body }) "budget"
    |> Async.RunSynchronously

let private monthsJson =
    """{"data":{"months":[
        {"month":"2026-12-01","deleted":false},
        {"month":"2026-09-01","deleted":true},
        {"month":"2026-11-01","deleted":false}]}}"""

let private monthsResponseTests =
    testList "Monatsfenster aus GET /months" [
        test "Ein gelöschter Monat fehlt" {
            Expect.equal (runMonths 200 monthsJson) (Ok [ "2026-12-01"; "2026-11-01" ]) "ohne September"
        }
        test "401, 404, 429 und anderer Status werden wie bei getMonth abgebildet" {
            match runMonths 401 "{}" with
            | Error (Shared.Domain.YnabError.Unauthorized _) -> ()
            | other -> failtestf "401: %A" other
            match runMonths 404 "{}" with
            | Error (Shared.Domain.YnabError.BudgetNotFound _) -> ()
            | other -> failtestf "404 meint das Budget: %A" other
            match runMonths 429 "{}" with
            | Error (Shared.Domain.YnabError.RateLimitExceeded _) -> ()
            | other -> failtestf "429: %A" other
            match runMonths 503 "weg" with
            | Error (Shared.Domain.YnabError.NetworkError msg) -> Expect.stringContains msg "503" "Status"
            | other -> failtestf "503: %A" other
        }
        test "Kaputtes JSON ist eine ungültige Antwort" {
            match runMonths 200 "{}" with
            | Error (Shared.Domain.YnabError.InvalidResponse _) -> ()
            | other -> failtestf "InvalidResponse erwartet: %A" other
        }
        test "Eine Ausnahme beim Abruf wird zum Netzfehler" {
            let result =
                Server.YnabClient.getMonthsWith
                    (fun () -> async { return raise (System.Net.Http.HttpRequestException "kaputt") })
                    "budget"
                |> Async.RunSynchronously
            match result with
            | Error (Shared.Domain.YnabError.NetworkError msg) -> Expect.equal msg "YNAB nicht erreichbar" "fester Satz"
            | other -> failtestf "Netzfehler erwartet: %A" other
        }
    ]

// budgeting-p4n6r: Der Antworttext stand im Fehler und kam so bis in die Oberfläche der Budget-Seite.
let private responseTextTests =
    let body = """{"error":{"detail":"interner Rumpf 4711"}}"""
    let months status text = runMonths status text
    let categories status text =
        Server.YnabClient.getCategoriesWith (fun () -> async { return status, text }) "budget" |> Async.RunSynchronously
    let readable (result: Result<'a, Shared.Domain.YnabError>) : string =
        match result with
        | Error e -> Shared.Domain.ynabErrorText e
        | Ok _ -> failtest "Fehler erwartet"
    testList "Kein Antworttext im Fehler des Rahmens" [
        test "GET /months antwortet 500 mit Rumpf: deutscher Satz mit Status, ohne Rumpf" {
            Expect.stringContains (readable (months 500 body)) "YNAB antwortet mit HTTP 500" "deutsch mit Status"
            Expect.isFalse ((readable (months 500 body)).Contains "4711") "kein Rumpf"
        }
        test "GET /categories antwortet 500 mit Rumpf: deutscher Satz mit Status, ohne Rumpf" {
            Expect.stringContains (readable (categories 500 body)) "YNAB antwortet mit HTTP 500" "deutsch mit Status"
            Expect.isFalse ((readable (categories 500 body)).Contains "4711") "kein Rumpf"
        }
        test "Eine unlesbare 200-Antwort nennt ihren Inhalt nicht" {
            Expect.isFalse ((readable (months 200 body)).Contains "4711") "Monate"
            Expect.isFalse ((readable (categories 200 body)).Contains "4711") "Kategorien"
        }
        test "Eine Ausnahme beim Abruf der Kategorien nennt ihren Text nicht" {
            let failing =
                Server.YnabClient.getCategoriesWith
                    (fun () -> async { return raise (System.Net.Http.HttpRequestException "kaputt") })
                    "budget"
                |> Async.RunSynchronously
            Expect.isFalse ((readable failing).Contains "kaputt") "kein Ausnahmetext"
        }
        // Review-Fund eh8xe (QA D7): die Goals für die Target-Übernahme lesen dieselbe Antwort wie die Kategorien.
        test "GET /categories für die Goals: 500 mit Rumpf, unlesbare 200 und Ausnahme nennen weder Rumpf noch Ausnahmetext" {
            let goals status text =
                Server.YnabClient.getCategoryGoalsWith (fun () -> async { return status, text }) "budget" |> Async.RunSynchronously
            Expect.stringContains (readable (goals 500 body)) "YNAB antwortet mit HTTP 500" "deutsch mit Status"
            Expect.isFalse ((readable (goals 500 body)).Contains "4711") "kein Rumpf bei 500"
            Expect.isFalse ((readable (goals 200 body)).Contains "4711") "kein Rumpf bei unlesbarer 200"
            let failing =
                Server.YnabClient.getCategoryGoalsWith
                    (fun () -> async { return raise (System.Net.Http.HttpRequestException "kaputt") })
                    "budget"
                |> Async.RunSynchronously
            Expect.isFalse ((readable failing).Contains "kaputt") "kein Ausnahmetext"
        }
        test "Die Kategorien kommen flach mit Gruppennamen" {
            let json =
                """{"data":{"category_groups":[{"name":"Alltag","categories":[
                   {"id":"00000000-0000-0000-0000-000000000001","name":"Essen","category_group_name":"X","hidden":false,"deleted":false}]}]}}"""
            match categories 200 json with
            | Ok [ category ] -> Expect.equal category.GroupName "Alltag" "Gruppe"
            | other -> failtestf "eine Kategorie erwartet: %A" other
        }
    ]

let private frameTests =
    let today = DateOnly(2026, 11, 2)
    let order = [ Guid.NewGuid() ]
    let port months categories : QueryHandlers.BudgetFramePort =
        { FetchMonths = fun () -> async { return months }
          FetchCategoryOrder = fun () -> async { return categories }
          LoadMonthStartHidden = fun _ -> async { return Ok [] } }
    let frameOf p = QueryHandlers.frame p today |> Async.RunSynchronously
    let limited : Shared.Domain.YnabResult<_> = Error (Shared.Domain.YnabError.RateLimitExceeded 60)
    testList "Rahmen der Budget-Seite" [
        test "Monate stehen aufsteigend, mit dem laufenden Monat" {
            let frame = Domain.toBudgetFrame today [ "2026-12-01"; "2019-01-01"; "2026-11-01" ] order
            Expect.equal frame.Months [ "2019-01-01"; "2026-11-01"; "2026-12-01" ] "aufsteigend"
            Expect.equal frame.CurrentMonth "2026-11-01" "laufend"
            Expect.equal frame.CategoryOrder order "Reihenfolge"
        }
        test "Beide Quellen ergeben den Rahmen" {
            match frameOf (port (Ok [ "2026-11-01" ]) (Ok order)) with
            | Ok frame -> Expect.equal frame.CategoryOrder order "Reihenfolge"
            | Error e -> failtestf "Ok erwartet: %A" e
        }
        test "Scheitert das Monatsfenster mit 429, kommt der Fehler zurück" {
            Expect.equal (frameOf (port (limited |> Result.map (fun _ -> [])) (Ok order))) (Error (Shared.Domain.YnabError.RateLimitExceeded 60)) "Monate"
        }
        test "Scheitert die Reihenfolge mit 429, kommt der Fehler zurück" {
            Expect.equal (frameOf (port (Ok [ "2026-11-01" ]) (limited |> Result.map (fun _ -> [])))) (Error (Shared.Domain.YnabError.RateLimitExceeded 60)) "Reihenfolge"
        }
    ]


let private euro amount : Shared.Domain.Money = { Amount = amount; Currency = "EUR" }

let private budgetCategory (id: Guid) (name: string) : BudgetCategory =
    { Id = id; Name = name; Assigned = euro 0m; Activity = euro 0m; Available = euro 0m
      Underfunded = None; Target = None; Pause = None; OpenWriteBack = None }

let private orderTests =
    let ids = List.init 5 (fun _ -> Guid.NewGuid())
    let a, b, c, d, e = ids[0], ids[1], ids[2], ids[3], ids[4]
    let names (groups: BudgetGroup list) =
        groups |> List.map (fun g -> g.Name, g.Categories |> List.map (fun x -> x.Name))
    testList "Reihenfolge der Kategorien (ADR 0074)" [
        test "Gruppen und Kategorien stehen nach dem Rang, nicht in der Monatsfolge" {
            let groups =
                [ { Name = "Zwei"; Categories = [ budgetCategory d "d"; budgetCategory c "c" ] }
                  { Name = "Eins"; Categories = [ budgetCategory b "b"; budgetCategory a "a" ] } ]
            Expect.equal (names (orderGroups [ a; b; c; d ] groups)) [ "Eins", [ "a"; "b" ]; "Zwei", [ "c"; "d" ] ] "Rang"
        }
        test "Der Rang einer Gruppe ist der kleinste ihrer Kategorien" {
            let groups =
                [ { Name = "Spät"; Categories = [ budgetCategory c "c"; budgetCategory a "a" ] }
                  { Name = "Mitte"; Categories = [ budgetCategory b "b" ] } ]
            Expect.equal (names (orderGroups [ a; b; c ] groups) |> List.map fst) [ "Spät"; "Mitte" ] "a zieht die Gruppe nach vorn"
        }
        test "Ohne Rang stehen Kategorie und Gruppe am Ende nach Name, nichts fällt weg" {
            let groups =
                [ { Name = "Y neu"; Categories = [ budgetCategory (Guid.NewGuid()) "x" ] }
                  { Name = "X neu"; Categories = [ budgetCategory (Guid.NewGuid()) "y" ] }
                  { Name = "Alt"; Categories = [ budgetCategory (Guid.NewGuid()) "z neu"; budgetCategory (Guid.NewGuid()) "m neu"; budgetCategory a "a" ] } ]
            Expect.equal
                (names (orderGroups [ a ] groups))
                [ "Alt", [ "a"; "m neu"; "z neu" ]; "X neu", [ "y" ]; "Y neu", [ "x" ] ]
                "am Ende nach Name"
        }
        test "Eine Id ohne Kategorie im Monat stört nicht" {
            let groups = [ { Name = "G"; Categories = [ budgetCategory e "e"; budgetCategory a "a" ] } ]
            Expect.equal (names (orderGroups [ Guid.NewGuid(); a; Guid.NewGuid(); e ] groups)) [ "G", [ "a"; "e" ] ] "unbekannte Ids fallen nicht auf"
        }
    ]

let private missingTests =
    let target = { Goal = euro 900m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }
    let category available under snoozed =
        { budgetCategory (Guid.NewGuid()) "k" with
            Available = euro available
            Underfunded = under |> Option.map euro
            Target = under |> Option.map (fun _ -> target)
            Pause = (if snoozed then Some PausedInBb else None); OpenWriteBack = None }
    let missing c = missingAmount c |> Option.map (fun m -> m.Amount)
    testList "Fehlt" [
        test "Negatives Verfügbar ohne Target fehlt" {
            Expect.equal (missing (category -12.30m None false)) (Some 12.30m) "ohne Target"
        }
        test "Das Größere zählt" {
            Expect.equal (missing (category -12.30m (Some 900m) false)) (Some 900m) "Fehlbetrag größer"
            Expect.equal (missing (category -20m (Some 10m) false)) (Some 20m) "Verfügbar größer"
        }
        test "Pausiert zählt der Fehlbetrag nicht, negatives Verfügbar bleibt" {
            Expect.equal (missing (category 5m (Some 900m) true)) None "pausiert"
            Expect.equal (missing (category -20m (Some 900m) true)) (Some 20m) "pausiert und rot"
        }
        test "Beides 0 fehlt nichts" {
            Expect.equal (missing (category 0m (Some 0m) false)) None "nichts"
        }
        test "Frei ist Verfügbar minus Fehlbetrag" {
            Expect.equal (freeAmount (category 100m (Some 30m) false)).Amount 70m "offenes Target"
        }
        test "Frei darf negativ sein" {
            Expect.equal (freeAmount (category 10m (Some 30m) false)).Amount -20m "mehr offen als da"
        }
        test "Ohne Target, pausiert und ohne Fehlbetrag ist alles Verfügbare frei" {
            Expect.equal (freeAmount (category 100m None false)).Amount 100m "ohne Target"
            Expect.equal (freeAmount (category 100m (Some 30m) true)).Amount 100m "pausiert"
            Expect.equal (freeAmount (category 100m (Some 0m) false)).Amount 100m "erfüllt"
        }
    ]

let private windowTests =
    let line outcome amount : ApplyLineResult =
        { CategoryId = Guid.NewGuid(); CategoryName = "k"; OldAssigned = euro 0m; NewAssigned = euro amount; Outcome = outcome }
    let report month lines : ApplyReport =
        { ApplicationId = "a"; Month = month; Lines = lines; ActualToBeBudgeted = None }
    let months = [ "2026-11-01"; "2026-12-01" ]
    testList "Fenster wächst (ADR 0074)" [
        test "Geschrieben, ungleich 0, im letzten Monat: das Fenster kann gewachsen sein" {
            Expect.isTrue (windowMayHaveGrown months (report "2026-12-01" [ line Written 10m ])) "gewachsen"
        }
        test "Mit neuem Zugewiesen 0 wächst es nicht" {
            Expect.isFalse (windowMayHaveGrown months (report "2026-12-01" [ line Written 0m ])) "0"
        }
        test "Nur fehlgeschlagene Zeilen öffnen nichts" {
            Expect.isFalse (windowMayHaveGrown months (report "2026-12-01" [ line (Failed "x") 10m; line ChangedInYnab 5m ])) "nichts geschrieben"
        }
        test "In einem anderen Monat wächst es nicht" {
            Expect.isFalse (windowMayHaveGrown months (report "2026-11-01" [ line Written 10m ])) "nicht der letzte"
        }
    ]

let private roundingTests =
    // Review zu e86f8d2 (budgeting-q4v8n): `Math.Round` mit `MidpointRounding.AwayFromZero` rundet im
    // Fable-Kompilat zur geraden Zahl. `roundHalfUp` rechnet von Hand, gleich auf beiden Seiten; der Beleg im
    // Kompilat ist der `[fable]`-Test in scripts/target-check.test.mjs.
    let month (m: string) (spent: decimal) : MonthSpending = { Month = m; Activity = euro -spent }
    testList "Kaufmännisch runden" [
        test "Ein genauer Mittelwert geht weg von null, auch eine gerade Zahl darunter" {
            Expect.equal (roundHalfUp 12.5m) 13m "12,5"
            Expect.equal (roundHalfUp 84.5m) 85m "84,5"
            Expect.equal (roundHalfUp 0.5m) 1m "0,5"
            Expect.equal (roundHalfUp -12.5m) -13m "-12,5"
            Expect.equal (roundHalfUp -0.5m) -1m "-0,5"
        }
        test "Knapp unter und über dem Mittelwert rundet zur nächsten Zahl" {
            Expect.equal (roundHalfUp 12.4999m) 12m "unter"
            Expect.equal (roundHalfUp 12.5001m) 13m "über"
            Expect.equal (roundHalfUp -12.4999m) -12m "negativ unter"
            Expect.equal (roundHalfUp -12.5001m) -13m "negativ über"
            Expect.equal (roundHalfUp 0m) 0m "null"
            Expect.equal (roundHalfUp 7m) 7m "ganz"
            Expect.equal (roundHalfUp -0.4m) 0m "negativ zu null"
        }
        test "Ein halber Cent im Betragstext wird aufgerundet, negativ mit Minus" {
            Expect.equal (euroText (euro 1.225m)) "1,23 €" "1,225"
            Expect.equal (euroText (euro -1.225m)) "-1,23 €" "-1,225"
            Expect.equal (euroText (euro 1.224m)) "1,22 €" "darunter"
            Expect.equal (euroText (euro -0.004m)) "0,00 €" "gerundet null ohne Minus"
        }
        test "Ein Balken bei genau 12,5 Prozent des größten ist 13 hoch" {
            let bars = spendingBars "2026-11-01" [ month "2026-09-01" 200m; month "2026-10-01" 25m ]
            Expect.equal (bars |> List.map (fun b -> b.Percent)) [ 100; 13 ] "12,5 aufgerundet"
        }
    ]

[<Tests>]
let tests =
    testList "Budgeting (k7wmd)" [ decoderTests; rhythmTests; groupingTests; monthKindTests; validationTests; errorMappingTests; apiPathTests; monthsResponseTests; responseTextTests; frameTests; orderTests; missingTests; windowTests; roundingTests ]
