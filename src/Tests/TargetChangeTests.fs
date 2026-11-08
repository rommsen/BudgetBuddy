module Tests.TargetChangeTests

// budgeting-eh8xe, ADR 0084 Punkte 5 bis 7, ADR 0080 mit Nachtrag: Targets in BB. Die einmalige Übernahme aus
// YNAB, die Faltung des Target-Streams je Monat, Target ändern nur in BB mit Befehls-Id, der Wochentag, die
// Historie aus der vorigen Faltung und die Grenze. Reine Regeln ohne I/O, Abläufe gegen In-Memory-SQLite ohne
// YNAB-Port (Target ändern ruft YNAB nicht auf), die Übernahme mit den Goals als Funktionswert.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.TargetRules
open Budgeting.Events

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private run x = Async.RunSynchronously x

let private targetOf (kind: TargetKind) (goal: decimal) (date: string option) : Target =
    { Goal = eur goal
      Rhythm = Months 1
      Kind = kind
      Date = date
      NeedsWholeAmount = Some true
      Day = Some 15
      CreatedIn = None }

let private fifty = targetOf Need 50m None

let private draft (goal: decimal) (date: string option) : TargetDraft =
    { NewGoal = eur goal; NewDate = date; NewRhythm = KeepRhythm; NewDay = None }

let private created (frequency: TargetFrequency) (goal: decimal) (date: string option) : TargetDraft =
    { NewGoal = eur goal; NewDate = date; NewRhythm = SetRhythm frequency; NewDay = None }

let private request (categoryId: Guid) (shown: Target option) (wanted: TargetDraft option) : TargetChangeRequest =
    { CategoryId = categoryId; Shown = shown; Wanted = wanted; CommandId = None }

let private weekly (day: int option) : Target =
    { Goal = eur 25m; Rhythm = Weeks 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = day; CreatedIn = None }

// ============================================
// Target ändern: reine Planung
// ============================================

let private planTests =
    let k = Guid.NewGuid()
    let plan current shown wanted = planTargetChange "2026-11-01" current (request k shown wanted)
    testList "Target ändern: Planung gegen BBs Stand" [
        test "Anlegen ergibt NEED mit dem gewählten Rhythmus und Datum, ohne Rhythmus abgelehnt" {
            Expect.equal
                (plan None None (Some (created Yearly 600m (Some "2027-06-01"))))
                (Ok (SetRule { Goal = eur 600m; Rhythm = Years 1; Kind = Need; Date = Some "2027-06-01"; NeedsWholeAmount = None; Day = None; CreatedIn = Some "2026-11-01" }))
                "jährlich mit Datum in einem Ereignis"
            Expect.equal (plan None None (Some (draft 50m None))) (Error TargetRhythmMissingText) "ohne Rhythmus"
        }
        test "Ändern behält Art, Rhythmus, Tag, Auffüllen bis und Anlegemonat, Betrag und Datum kommen aus dem Wunsch" {
            // budgeting-pf8n7: der Anlegemonat bleibt, sonst begänne die erste Periode im Monat der Änderung.
            let old = { fifty with CreatedIn = Some "2025-03-01" }
            Expect.equal
                (plan (Some old) (Some old) (Some (draft 60m (Some "2027-06-01"))))
                (Ok (SetRule { old with Goal = eur 60m; Date = Some "2027-06-01" }))
                "geändert"
        }
        test "Wechsel der Art ergibt NEED mit neuem Rhythmus, ohne den alten Tag" {
            let mf = targetOf MonthlyFunding 50m None
            Expect.equal
                (plan (Some mf) (Some mf) (Some (created Monthly 70m None)))
                (Ok (SetRule { Goal = eur 70m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = Some "2026-11-01" }))
                "MF auf NEED monatlich"
        }
        test "Entfernen ergibt RemoveRule, ohne Target ist nichts zu tun" {
            Expect.equal (plan (Some fifty) (Some fifty) None) (Ok RemoveRule) "entfernen"
            Expect.equal (plan None None None) (Ok (TargetUnchanged None)) "nichts da"
        }
        test "Steht BB schon auf dem Wunsch, ist nichts festzuhalten" {
            Expect.equal (plan (Some fifty) (Some fifty) (Some (draft 50m None))) (Ok (TargetUnchanged (Some fifty))) "gleich"
        }
        test "Weicht das gezeigte Target von BBs Stand ab, ist nichts festzuhalten und der Plan trägt BBs Stand" {
            let sixty = { fifty with Goal = eur 60m }
            Expect.equal (plan (Some sixty) (Some fifty) (Some (draft 70m None))) (Ok (ShownOutdated (Some sixty))) "anderer Betrag"
            Expect.equal (plan None (Some fifty) None) (Ok (ShownOutdated None)) "inzwischen entfernt"
            Expect.equal (plan (Some fifty) None (Some (created Monthly 50m None))) (Ok (ShownOutdated (Some fifty))) "inzwischen angelegt"
        }
    ]

// ============================================
// Wochentag
// ============================================

let private weekdayTests =
    testList "Target ändern: Wochentag" [
        test "Der Wochentag eines wöchentlichen Targets lässt sich setzen und ändern" {
            let shown = weekly (Some 1)
            Expect.equal (targetAfter (Some shown) { draft 25m None with NewDay = Some 5 }) (Ok (weekly (Some 5))) "Freitag"
            Expect.equal
                (targetAfter None { created Weekly 25m None with NewDay = Some 0 })
                (Ok { weekly (Some 0) with Rhythm = Weeks 1 })
                "beim Anlegen Sonntag"
            Expect.equal
                (targetAfter (Some fifty) { created Weekly 25m None with NewDay = Some 3 })
                (Ok (weekly (Some 3)))
                "beim Wechsel auf wöchentlich"
        }
        test "Ein Wochentag an einem Target, das nicht wöchentlich ist, oder außerhalb von 0 bis 6 wird abgelehnt" {
            Expect.equal (targetAfter (Some fifty) { draft 50m None with NewDay = Some 2 }) (Error TargetWeekdayText) "monatlich"
            Expect.equal (targetAfter (Some (weekly None)) { draft 25m None with NewDay = Some 7 }) (Error TargetWeekdayText) "7"
            Expect.equal (targetAfter (Some (weekly None)) { draft 25m None with NewDay = Some -1 }) (Error TargetWeekdayText) "-1"
        }
        // Review-Fund eh8xe (Backend W3): ein Entwurf ohne `NewDay` (älterer Client) löschte den Wochentag.
        test "GIVEN ein wöchentliches Target am Montag WHEN ein Entwurf ohne Wochentag nur den Betrag ändert THEN bleibt Montag" {
            Expect.equal (targetAfter (Some (weekly (Some 1))) (draft 30m None)) (Ok { weekly (Some 1) with Goal = eur 30m }) "Montag bleibt"
        }
        test "Ein monatliches Target behält seinen Tag, wenn nur der Betrag sich ändert" {
            Expect.equal (targetAfter (Some fifty) (draft 60m None)) (Ok { fifty with Goal = eur 60m }) "Tag 15 bleibt"
        }
        test "underfunded rechnet mit dem in BB gesetzten Wochentag aus der Faltung" {
            // November 2026: vier Dienstage, fünf Sonntage.
            let setOn (day: int) =
                [ BudgetEvent.TargetTakenOverFromYnab { CategoryName = "Essen"; Rule = weekly None; YnabGoalType = "NEED" }
                  BudgetEvent.TargetSet { Rule = weekly (Some day); EffectiveFrom = "2026-11" } ]
            let underfundedWith (day: int) =
                match ruleAt "2026-11" (setOn day) with
                | Some rule -> Domain.underfunded rule "2026-11-01" (eur 0m) (eur 0m) (eur 0m) Map.empty
                | None -> failtest "Regel erwartet"
            Expect.equal (underfundedWith 2) (Domain.Computed (eur 100m)) "vier Dienstage zu 25 €"
            Expect.equal (underfundedWith 0) (Domain.Computed (eur 125m)) "fünf Sonntage zu 25 €"
            match ruleAt "2026-11" (setOn 1 |> List.take 1) with
            | Some rule -> Expect.equal (Domain.underfunded rule "2026-11-01" (eur 0m) (eur 0m) (eur 0m) Map.empty) (Domain.NotComputed "NEED wöchentlich ohne Wochentag") "ohne Tag"
            | None -> failtest "Regel erwartet"
        }
    ]

// ============================================
// Faltung je Monat
// ============================================

let private foldTests =
    let sixty = { fifty with Goal = eur 60m }
    let takeover = BudgetEvent.TargetTakenOverFromYnab { CategoryName = "Urlaub"; Rule = fifty; YnabGoalType = "NEED" }
    let legacy =
        BudgetEvent.YnabGoalChangedFromBb
            { Month = "2026-05"; CategoryName = "Urlaub"; ChangeKind = TargetEdited; OldGoalTarget = Some 1L; NewGoalTarget = Some 2L; OldDate = None; NewDate = None }
    testList "Target-Stream: Faltung je Monat" [
        test "GIVEN ein Target, im Monat M geändert, WHEN ein Monat vor M gezeigt wird, THEN zeigt er die Regel vor der Änderung" {
            let events = [ takeover; BudgetEvent.TargetSet { Rule = sixty; EffectiveFrom = "2026-11" } ]
            Expect.equal (ruleAt "2026-10" events) (Some fifty) "Oktober: vorher"
            Expect.equal (ruleAt "2026-11" events) (Some sixty) "November: ab der Änderung"
            Expect.equal (ruleAt "2027-02" events) (Some sixty) "danach"
        }
        test "Die Übernahme gilt für alle Monate, ein Entfernen ab seinem Monat, ein Anlegen erst ab seinem Monat" {
            Expect.equal (ruleAt "2019-01" [ takeover ]) (Some fifty) "auch lange vorher"
            let removed = [ takeover; BudgetEvent.TargetRemoved { EffectiveFrom = "2026-11" } ]
            Expect.equal (ruleAt "2026-10" removed, ruleAt "2026-11" removed) (Some fifty, None) "entfernt ab November"
            let created = [ BudgetEvent.TargetSet { Rule = fifty; EffectiveFrom = "2026-11" } ]
            Expect.equal (ruleAt "2026-10" created, ruleAt "2026-11" created) (None, Some fifty) "angelegt ab November"
        }
        test "Der Altbestand vor der Übernahme ändert die Regel nicht" {
            Expect.equal (ruleAt "2026-11" [ legacy; takeover; legacy ]) (Some fifty) "Zustand beginnt bei der Übernahme"
            Expect.equal (ruleAt "2026-11" [ legacy ]) None "ohne Übernahme keine Regel"
        }
        test "Die Historie leitet Angelegt, Geändert, Wechsel und Entfernt aus der vorigen Faltung ab" {
            Expect.equal (changeKindBetween None (Some fifty)) (TargetCreated Monthly) "angelegt"
            Expect.equal (changeKindBetween (Some fifty) (Some sixty)) TargetEdited "geändert"
            Expect.equal (changeKindBetween (Some fifty) (Some (weekly None))) (TargetKindChanged Weekly) "Wechsel"
            Expect.equal (changeKindBetween (Some fifty) None) TargetChangeKind.TargetRemoved "entfernt"
            Expect.equal (changeKindBetween None (Some { fifty with Rhythm = Months 3 })) TargetEdited "Rhythmus, den BB nicht anlegt"
        }
    ]

// ============================================
// Übernahme aus YNAB
// ============================================

/// Ein Goal aus `/categories`, wie YNAB es liefert, als JSON-Felder.
let private goalsJson (categories: (Guid * string * string) list) : string =
    let category (id: Guid, name: string, goalFields: string) =
        $"""{{ "id": "{id}", "name": "{name}", "hidden": false, "deleted": false, "category_group_name": "Alltag", "balance": 0 {goalFields} }}"""
    let list = categories |> List.map category |> String.concat ", "
    $"""{{ "data": {{ "category_groups": [ {{ "name": "Alltag", "categories": [ {list} ] }} ] }} }}"""

/// Die Goal-Felder je in Romans Budget genutzter Art, dazu DEBT und eine Kategorie ohne Goal.
let private goalFields : (string * string) list =
    [ "Monatlich mit Tag", """, "goal_type": "NEED", "goal_target": 120000, "goal_cadence": 1, "goal_cadence_frequency": 1, "goal_day": 6, "goal_needs_whole_amount": true, "goal_creation_month": "2025-03-01" """
      "Auffüllen bis", """, "goal_type": "NEED", "goal_target": 80000, "goal_cadence": 1, "goal_cadence_frequency": 1, "goal_needs_whole_amount": false"""
      "Wöchentlich", """, "goal_type": "NEED", "goal_target": 25000, "goal_cadence": 2, "goal_cadence_frequency": 1, "goal_day": 1"""
      "Alle 3 Monate", """, "goal_type": "NEED", "goal_target": 90000, "goal_cadence": 1, "goal_cadence_frequency": 3, "goal_target_date": "2027-02-01" """
      "Jährlich mit Datum", """, "goal_type": "NEED", "goal_target": 600000, "goal_cadence": 13, "goal_cadence_frequency": 1, "goal_target_date": "2027-06-01" """
      "Zielstand", """, "goal_type": "TB", "goal_target": 300000"""
      "Zielstand mit Datum", """, "goal_type": "TBD", "goal_target": 500000, "goal_target_date": "2028-01-01" """
      "Monatlich alt", """, "goal_type": "MF", "goal_target": 40000"""
      "Kredit", """, "goal_type": "DEBT", "goal_target": 20000"""
      "Ohne Target", "" ]

let private takeoverTests =
    let parsed () =
        let categories = goalFields |> List.map (fun (name, fields) -> Guid.NewGuid(), name, fields)
        match Server.YnabClient.interpretCategoryGoalsResponse "b" 200 (goalsJson categories) with
        | Ok goals -> goals
        | Error e -> failtest $"Goals erwartet: {e}"
    testList "Target-Übernahme: reine Regeln" [
        test "GIVEN jede genutzte Art und DEBT WHEN übernommen THEN je Kategorie mit übernehmbarem Target genau ein Ereignis, DEBT gemeldet" {
            let goals = parsed ()
            let plan = planTargetTakeover Set.empty goals
            let takeable = goals |> List.filter (fun g -> g.Name <> "Kredit" && g.Name <> "Ohne Target")
            Expect.equal (plan.Events |> List.map fst) (takeable |> List.map (fun g -> g.Id)) "je übernehmbare Kategorie eins"
            Expect.equal
                (plan.NotTakenOver |> List.map (fun n -> n.CategoryName, n.Reason))
                [ "Kredit", "DEBT übernimmt BB nicht" ]
                "DEBT gemeldet"
            // Review-Fund eh8xe (QA D10): feste Erwartung in der Reihenfolge von `goalFields`, kein Orakel aus dem
            // Produktionscode.
            let goalTypes =
                plan.Events
                |> List.map (fun (_, event) ->
                    match event with
                    | BudgetEvent.TargetTakenOverFromYnab p -> p.YnabGoalType
                    | other -> failtest $"Übernahme erwartet: {other}")
            Expect.equal goalTypes [ "NEED"; "NEED"; "NEED"; "NEED"; "NEED"; "TB"; "TBD"; "MF" ] "rohes goal_type"
        }
        test "Eine Kategorie, deren Target BB schon führt, bekommt nichts, auch nicht gemeldet" {
            let goals = parsed ()
            let led = goals |> List.map (fun g -> g.Id) |> Set.ofList
            Expect.equal (planTargetTakeover led goals) { Events = []; NotTakenOver = [] } "ein zweiter Lauf schreibt nichts"
        }
        // ADR 0084 Punkt 6: die Faltung ergibt Art, Betrag, Rhythmus, Datum und Tag wie die gelesenen goal_*-Felder.
        test "Für jede übernommene Kategorie ergibt die Faltung nach dem Speichern dieselbe Regel wie die goal_*-Felder" {
            let goals = parsed ()
            let plan = planTargetTakeover Set.empty goals
            for id, event in plan.Events do
                let stored = decodePayload (eventTypeName event) SchemaVersion (encodePayload event)
                let goal = goals |> List.find (fun g -> g.Id = id)
                match stored with
                | Ok decoded -> Expect.equal (ruleAt "2026-11" [ decoded ]) goal.Target goal.Name
                | Error e -> failtest e
        }
        // budgeting-pf8n7: der Anlegemonat beginnt die erste Periode; die Übernahme bringt ihn aus YNAB mit.
        test "Die Übernahme liest den Anlegemonat aus goal_creation_month" {
            let monthly = parsed () |> List.find (fun g -> g.Name = "Monatlich mit Tag")
            Expect.equal (monthly.Target |> Option.bind (fun t -> t.CreatedIn)) (Some "2025-03-01") "Anlegemonat gelesen"
        }
        test "Ein gespeichertes Target ohne Anlegemonat wird ohne gelesen" {
            let withoutField =
                """{ "categoryName": "x", "rule": { "goal": 1000, "rhythm": { "unit": "none" }, "kind": "TB" }, "ynabGoalType": "TB" }"""
            match decodePayload "TargetTakenOverFromYnab" SchemaVersion withoutField with
            | Ok decoded -> Expect.equal (ruleAt "2026-11" [ decoded ] |> Option.map (fun t -> t.CreatedIn)) (Some None) "ohne Anlegemonat"
            | Error e -> failtest e
        }
        test "Welche Kategorien des Monats warten auf die Übernahme? Nur mit übernehmbarem Goal, nicht übersprungen, nicht im vergangenen Monat" {
            let k = Guid.NewGuid()
            let monthWith (target: Target option) : YnabMonth =
                { Month = "2026-11-01"
                  ToBeBudgeted = eur 0m
                  Categories =
                    [ { Id = k; Name = "x"; GroupName = "g"; Assigned = eur 0m; Activity = eur 0m; Available = eur 0m
                        Target = target; Hidden = true; Deleted = false; Internal = false } ] }
            let waiting skip from target = awaitingTakeover skip from (monthWith target)
            Expect.equal (waiting Set.empty "2026-11-01" (Some fifty)) (Set.ofList [ k ]) "Goal ohne Führung, auch ausgeblendet"
            Expect.equal (waiting Set.empty "2026-10-01" (Some fifty)) (Set.ofList [ k ]) "ein künftiger Monat wartet auch"
            Expect.isEmpty (waiting (Set.ofList [ k ]) "2026-11-01" (Some fifty)) "geführt oder gemerkt"
            Expect.isEmpty (waiting Set.empty "2026-11-01" None) "ohne Goal"
            Expect.isEmpty (waiting Set.empty "2026-11-01" (Some (targetOf Debt 50m None))) "DEBT"
            // Review-Fund eh8xe (Backend W1): ein vergangener Monat mit Goal fragte bei jedem Lesen /categories.
            Expect.isEmpty (waiting Set.empty "2026-12-01" (Some fifty)) "vergangener Monat"
        }
        test "Die Goals aus /categories: 401, 404 und 429 kommen als YNAB-Fehler" {
            Expect.isError (Server.YnabClient.interpretCategoryGoalsResponse "b" 401 "") "401"
            Expect.equal (Server.YnabClient.interpretCategoryGoalsResponse "b" 404 "") (Error (YnabError.BudgetNotFound "b")) "404"
            Expect.equal (Server.YnabClient.interpretCategoryGoalsResponse "b" 429 "") (Error (YnabError.RateLimitExceeded 60)) "429"
        }
    ]

// ============================================
// Abläufe
// ============================================

let private current = "2026-11-01"
let private now = DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)

/// Ein YNAB, das jeden Aufruf ablehnt: diese Abläufe prüfen nur BBs Teil, das Schreiben nach YNAB prüft
/// `TargetToYnabTests` (budgeting-e2z7v).
let private noYnab : CommandHandlers.YnabPort =
    let refused () = async { return Error (YnabError.RefusedByBb "kein YNAB im Test") }
    { FetchMonth = fun _ -> refused ()
      SetAssigned = fun _ _ _ -> refused ()
      WriteGoal = fun _ _ -> refused ()
      FetchCategoryGoals = fun () -> refused () }

/// Die Ereignistypen im Target-Stream der Kategorie, ohne die Ergebnisse des Schreibens nach YNAB.
let private streamTypes (k: Guid) : string list =
    use conn = global.Persistence.getConnection ()
    match Budgeting.Persistence.loadBudgetEventsOn conn (Budgeting.Persistence.InStream (streamIdOf (TargetStream k))) with
    | Ok events ->
        events
        |> List.map (fun e -> eventTypeName e.Event)
        |> List.filter (fun t -> not (List.contains t EventTypes.targetWrites))
    | Error e -> failtest e

let private takeOver (k: Guid) (target: Target) =
    use conn = global.Persistence.getConnection ()
    Budgeting.Persistence.appendTargetTakeoverOn
        conn
        (DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc))
        [ k, BudgetEvent.TargetTakenOverFromYnab { CategoryName = "Urlaub"; Rule = target; YnabGoalType = goalTypeText target.Kind } ]

let private change (month: string) (r: TargetChangeRequest) =
    CommandHandlers.changeTargetWith global.Persistence.getConnection noYnab now current month r |> run

let private changeOk month r : Target option =
    match change month r with
    | Ok (Ok (TargetWritten after)) -> after
    | other -> failtest $"geschrieben erwartet: {other}"

let private flowTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())
    testSequenced
    <| testList "Target ändern: Abläufe nur in BB" [
        testCase "Anlegen, Ändern, Wechsel der Art und Entfernen hängen je genau ein Ereignis an" <| fun () ->
            let k = Guid.NewGuid()
            let monthly = changeOk current (request k None (Some (created Monthly 50m None)))
            Expect.equal (streamTypes k) [ "TargetSet" ] "angelegt"
            let changed = changeOk current (request k monthly (Some (draft 60m None)))
            Expect.equal (changed |> Option.map (fun t -> t.Goal)) (Some (eur 60m)) "60 €"
            let weeklyNow = changeOk current (request k changed (Some { created Weekly 25m None with NewDay = Some 1 }))
            // budgeting-pf8n7: der Wechsel der Art legt eine neue Regel an, ihr Anlegemonat ist der laufende Monat.
            Expect.equal weeklyNow (Some { weekly (Some 1) with CreatedIn = Some current }) "wöchentlich am Montag"
            Expect.equal (changeOk current (request k weeklyNow None)) None "entfernt"
            Expect.equal (streamTypes k) [ "TargetSet"; "TargetSet"; "TargetSet"; "TargetRemoved" ] "je eins"

        testCase "Steht BB schon auf dem Wunsch, hängt nichts an" <| fun () ->
            let k = Guid.NewGuid()
            takeOver k fifty
            Expect.equal (changeOk current (request k (Some fifty) (Some (draft 50m None)))) (Some fifty) "Stand"
            Expect.equal (streamTypes k) [ "TargetTakenOverFromYnab" ] "nichts angehängt"

        testCase "Weicht das gezeigte Target von der Faltung ab, hängt nichts an und die Antwort trägt BBs Stand" <| fun () ->
            let k = Guid.NewGuid()
            takeOver k fifty
            let stale = { fifty with Goal = eur 55m }
            Expect.equal (change current (request k (Some stale) (Some (draft 70m None)))) (Ok (Ok (TargetChangedMeanwhile (Some fifty)))) "BBs Stand"
            Expect.equal (streamTypes k) [ "TargetTakenOverFromYnab" ] "nichts angehängt"

        testCase "Außerhalb des laufenden Monats wird abgelehnt wie bisher" <| fun () ->
            let k = Guid.NewGuid()
            takeOver k fifty
            for month in [ "2026-10-01"; "2026-12-01" ] do
                Expect.equal (change month (request k (Some fifty) (Some (draft 60m None)))) (Ok (Error TargetEditMonthText)) month
            Expect.equal (change "2026-10-01" (request k None (Some (created Monthly 60m None)))) (Ok (Error TargetCreateMonthText)) "anlegen"
            Expect.equal (streamTypes k) [ "TargetTakenOverFromYnab" ] "nichts angehängt"

        testCase "GIVEN ein gespeichertes Target ändern mit Id X WHEN derselbe Befehl mit Id X wiederkommt THEN hängt er nichts an und antwortet wie beim ersten Mal" <| fun () ->
            let k = Guid.NewGuid()
            takeOver k fifty
            let r = { request k (Some fifty) (Some (draft 60m None)) with CommandId = Some (Guid.NewGuid()) }
            let first = change current r
            Expect.equal first (Ok (Ok (TargetWritten (Some { fifty with Goal = eur 60m })))) "erstes Mal"
            Expect.equal (change current r) first "dieselbe Antwort"
            Expect.equal (streamTypes k) [ "TargetTakenOverFromYnab"; "TargetSet" ] "nur ein TargetSet"
            let removal = { request k (Some { fifty with Goal = eur 60m }) None with CommandId = Some (Guid.NewGuid()) }
            Expect.equal (change current removal) (Ok (Ok (TargetWritten None))) "entfernt"
            Expect.equal (change current removal) (Ok (Ok (TargetWritten None))) "Wiederholung des Entfernens"
            Expect.equal (streamTypes k |> List.length) 3 "kein zweites TargetRemoved"

        testCase "Ein Befehl mit neuer Id und veraltetem Shown wird abgelehnt" <| fun () ->
            let k = Guid.NewGuid()
            takeOver k fifty
            changeOk current { request k (Some fifty) (Some (draft 60m None)) with CommandId = Some (Guid.NewGuid()) } |> ignore
            let late = { request k (Some fifty) (Some (draft 60m None)) with CommandId = Some (Guid.NewGuid()) }
            Expect.equal (change current late) (Ok (Ok (TargetChangedMeanwhile (Some { fifty with Goal = eur 60m })))) "veraltet"

        testCase "Eine Id, die zu einem anderen Auftrag gehört, wird abgelehnt" <| fun () ->
            let k, other = Guid.NewGuid(), Guid.NewGuid()
            takeOver k fifty
            takeOver other fifty
            let id = Guid.NewGuid()
            changeOk current { request other (Some fifty) (Some (draft 60m None)) with CommandId = Some id } |> ignore
            let foreign = { request k (Some fifty) (Some (draft 70m None)) with CommandId = Some id }
            Expect.equal (change current foreign) (Ok (Error CommandHandlers.ForeignCommandText)) "andere Kategorie"
            Expect.equal (streamTypes k) [ "TargetTakenOverFromYnab" ] "nichts angehängt"

        testCase "Die Historie zeigt Angelegt und 50 → 60 aus der vorigen Faltung, ohne Stand" <| fun () ->
            let k = Guid.NewGuid()
            let monthly = changeOk current (request k None (Some (created Monthly 50m None)))
            changeOk current (request k monthly (Some (draft 60m None))) |> ignore
            match Budgeting.Persistence.loadTargetChanges k current |> run with
            | Ok changes ->
                let lines = changes |> List.map (fun c -> historyEntryLine (TargetChanged c))
                Expect.equal
                    (lines |> List.map (fun l -> l.Text, l.Status))
                    [ "Target 50,00 € → 60,00 €", None; "Target angelegt, 50,00 € monatlich", None ]
                    "neueste zuerst"
            | Error e -> failtest e
            Expect.equal (Budgeting.Persistence.loadTargetChanges k "2026-10-01" |> run) (Ok []) "anderer Monat"

        // Review-Fund eh8xe (QA D2): der Test hieß nach der Sperre, lief aber nacheinander. Jetzt warten beide
        // Änderungen an der gehaltenen Sperre und laufen erst nach ihrer Freigabe.
        testCase "Zwei Änderungen derselben Kategorie laufen unter TargetGuard nacheinander: eine hält fest, die andere sieht BBs neuen Stand" <| fun () ->
            let k = Guid.NewGuid()
            takeOver k fifty
            let holder = new Threading.SemaphoreSlim(0, 1)
            let release = new Threading.SemaphoreSlim(0, 1)
            let held =
                CommandHandlers.TargetGuard.exclusive k (async {
                    holder.Release() |> ignore
                    do! release.WaitAsync() |> Async.AwaitTask
                })
                |> Async.StartAsTask
            holder.Wait()
            let sixty = CommandHandlers.changeTargetWith global.Persistence.getConnection noYnab now current current (request k (Some fifty) (Some (draft 60m None))) |> Async.StartAsTask
            let seventy = CommandHandlers.changeTargetWith global.Persistence.getConnection noYnab now current current (request k (Some fifty) (Some (draft 70m None))) |> Async.StartAsTask
            Expect.isFalse (sixty.Wait 200 || seventy.Wait 0) "beide warten an der Sperre"
            release.Release() |> ignore
            held.Wait()
            let results = [ sixty.Result; seventy.Result ]
            let written = results |> List.filter (function Ok (Ok (TargetWritten _)) -> true | _ -> false)
            let meanwhile = results |> List.filter (function Ok (Ok (TargetChangedMeanwhile (Some _))) -> true | _ -> false)
            Expect.equal (written.Length, meanwhile.Length) (1, 1) $"eine festgehalten, eine inzwischen geändert: {results}"
            Expect.equal (streamTypes k) [ "TargetTakenOverFromYnab"; "TargetSet" ] "genau ein TargetSet"

        testCase "Die Sperre einer anderen Kategorie hält Target ändern nicht auf" <| fun () ->
            let a, b = Guid.NewGuid(), Guid.NewGuid()
            takeOver a fifty
            takeOver b fifty
            let holder = new Threading.SemaphoreSlim(0, 1)
            let release = new Threading.SemaphoreSlim(0, 1)
            let held =
                CommandHandlers.TargetGuard.exclusive a (async {
                    holder.Release() |> ignore
                    do! release.WaitAsync() |> Async.AwaitTask
                })
                |> Async.StartAsTask
            holder.Wait()
            let blocked = CommandHandlers.changeTargetWith global.Persistence.getConnection noYnab now current current (request a (Some fifty) (Some (draft 60m None))) |> Async.StartAsTask
            // Mit Zeitgrenze: eine globale Sperre macht den Test rot, statt ihn hängen zu lassen.
            let free = CommandHandlers.changeTargetWith global.Persistence.getConnection noYnab now current current (request b (Some fifty) (Some (draft 60m None))) |> Async.StartAsTask
            let finished = free.Wait(TimeSpan.FromSeconds 10.0)
            if not finished then release.Release() |> ignore
            Expect.isTrue finished "b endet, obwohl a gesperrt ist"
            Expect.equal free.Result (Ok (Ok (TargetWritten (Some { fifty with Goal = eur 60m })))) "b festgehalten"
            Expect.isFalse blocked.IsCompleted "a wartet auf seine Sperre"
            release.Release() |> ignore
            held.Wait()
            Expect.equal blocked.Result (Ok (Ok (TargetWritten (Some { fifty with Goal = eur 60m })))) "danach a"

        // Review-Fund eh8xe (QA D5): die Historie leitet den Wochentag aus der vorigen Faltung ab.
        testCase "Die Historie zeigt den Wechsel des Wochentags von Montag auf Freitag" <| fun () ->
            let k = Guid.NewGuid()
            takeOver k (weekly (Some 1))
            changeOk current (request k (Some (weekly (Some 1))) (Some { draft 25m None with NewDay = Some 5 })) |> ignore
            match Budgeting.Persistence.loadTargetChanges k current |> run with
            | Ok [ c ] ->
                Expect.equal (c.DayBefore, c.DayAfter) (Some 1, Some 5) "Tag vorher und nachher"
                Expect.stringContains (historyEntryLine (TargetChanged c)).Text "Freitag" "Zeile nennt Freitag"
            | other -> failtest $"eine Änderung erwartet: {other}"

        // Review-Fund eh8xe (Backend V5): ein Fehler beim Anhängen kam als Ausnahme statt als Fehler.
        testCase "GIVEN das Anhängen scheitert WHEN Target ändern THEN kommt LocalWriteFailed, nichts ist angehängt" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let k = Guid.NewGuid()
            Dapper.SqlMapper.Execute(anchor, "CREATE TRIGGER no_events BEFORE INSERT ON events BEGIN SELECT RAISE(ABORT, 'Platte voll'); END") |> ignore
            let result =
                CommandHandlers.changeTargetWith connect noYnab now current current
                    { request k None (Some (created Monthly 50m None)) with CommandId = Some (Guid.NewGuid()) }
                |> run
            Expect.equal result (Error (YnabError.LocalWriteFailed CommandHandlers.TargetChangeNotRecordedText)) "sauberer Fehler"
            Expect.equal (Dapper.SqlMapper.ExecuteScalar<int64>(anchor, "SELECT COUNT(*) FROM events")) 0L "nichts angehängt"

        // Review-Fund eh8xe (QA D6a), Vorlage: der entfallene Test zum unbekannten Ereignistyp beim Lesen.
        testCase "Ein unlesbarer Target-Stream wird gemeldet, Target ändern hängt nichts an" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let k = Guid.NewGuid()
            BudgetEventsFixture.appendUnreadable anchor (TargetStream k) "TargetSet"
            let result = CommandHandlers.changeTargetWith connect noYnab now current current (request k None (Some (created Monthly 50m None))) |> run
            Expect.equal result (Error (YnabError.LocalReadFailed Budgeting.Persistence.TargetRulesUnreadableText)) "gemeldet"
            Expect.equal (Dapper.SqlMapper.ExecuteScalar<int64>(anchor, "SELECT COUNT(*) FROM events")) 1L "nichts angehängt"
    ]

// ============================================
// Übernahme und Lesen eines Monats
// ============================================

type private GoalsProbe(goals: YnabCategoryGoal list) =
    member val Calls = 0 with get, set
    member this.Fetch () =
        async {
            this.Calls <- this.Calls + 1
            return Ok goals
        }

let private takeoverFlowTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())
    testSequenced
    <| testList "Target-Übernahme: Abläufe" [
        testCase "GIVEN Goals jeder Art und DEBT WHEN die Übernahme läuft THEN je Kategorie ein Ereignis, DEBT im Bericht, ein zweiter Lauf schreibt nichts" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let goals =
                match Server.YnabClient.interpretCategoryGoalsResponse "b" 200 (goalsJson (goalFields |> List.map (fun (n, f) -> Guid.NewGuid(), n, f))) with
                | Ok g -> g
                | Error e -> failtest $"{e}"
            let probe = GoalsProbe goals
            match CommandHandlers.takeOverTargetsWith connect probe.Fetch now |> run with
            | Ok report ->
                Expect.equal report.TakenOver.Length 8 "acht übernehmbare"
                Expect.equal (report.NotTakenOver |> List.map (fun n -> n.CategoryName)) [ "Kredit" ] "DEBT gemeldet"
            | Error e -> failtest $"{e}"
            let count () =
                use conn = connect ()
                Dapper.SqlMapper.ExecuteScalar<int64>(conn, "SELECT COUNT(*) FROM events WHERE event_type = 'TargetTakenOverFromYnab'")
            Expect.equal (count ()) 8L "acht Ereignisse"
            match CommandHandlers.takeOverTargetsWith connect probe.Fetch now |> run with
            | Ok report -> Expect.isEmpty report.TakenOver "nichts übernommen"
            | Error e -> failtest $"{e}"
            Expect.equal (count ()) 8L "ein zweiter Lauf schreibt nichts"

        testCase "Das Lesen eines Monats übernimmt einmal, zeigt BBs Regeln und fragt /categories danach nicht mehr" <| fun () ->
            let k = Guid.NewGuid()
            let monthKey = "2031-07-01"
            let raw : YnabMonth =
                { Month = monthKey
                  ToBeBudgeted = eur 0m
                  Categories =
                    [ { Id = k; Name = "Urlaub"; GroupName = "Rücklagen"; Assigned = eur 0m; Activity = eur 0m; Available = eur 0m
                        Target = Some fifty; Hidden = false; Deleted = false; Internal = false } ] }
            let probe = GoalsProbe [ { Id = k; Name = "Urlaub"; Target = Some fifty } ]
            let port : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok raw }
                  SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben" }
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = probe.Fetch }
            let read () =
                match QueryHandlers.budgetMonthWith global.Persistence.getConnection port now monthKey |> run with
                | Ok m -> (m.Groups |> List.collect (fun g -> g.Categories) |> List.exactlyOne).Target
                | Error e -> failtest $"{e}"
            Expect.equal (read ()) (Some fifty) "übernommen"
            Expect.equal (read ()) (Some fifty) "noch einmal"
            Expect.equal probe.Calls 1 "/categories nur einmal"
            // Ein Goal, das YNAB danach zeigt, ändert BBs Regel nicht (Goals eingefroren, ADR 0084 Punkt 6).
            let port' = { port with FetchMonth = fun _ -> async { return Ok { raw with Categories = [ { raw.Categories.Head with Target = Some { fifty with Goal = eur 99m } } ] } } }
            match QueryHandlers.budgetMonthWith global.Persistence.getConnection port' now monthKey |> run with
            | Ok m -> Expect.equal (m.Groups.Head.Categories.Head.Target) (Some fifty) "BBs Regel bleibt"
            | Error e -> failtest $"{e}"

        testCase "Scheitert /categories, kommt der Monat trotzdem, ohne Target" <| fun () ->
            let k = Guid.NewGuid()
            let monthKey = "2031-08-01"
            let raw : YnabMonth =
                { Month = monthKey
                  ToBeBudgeted = eur 0m
                  Categories =
                    [ { Id = k; Name = "Urlaub"; GroupName = "Rücklagen"; Assigned = eur 0m; Activity = eur 0m; Available = eur 0m
                        Target = Some fifty; Hidden = false; Deleted = false; Internal = false } ] }
            let port : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok raw }
                  SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben" }
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = fun () -> async { return Error (YnabError.RateLimitExceeded 60) } }
            match QueryHandlers.budgetMonthWith global.Persistence.getConnection port now monthKey |> run with
            | Ok m -> Expect.equal m.Groups.Head.Categories.Head.Target None "ohne Übernahme kein Target"
            | Error e -> failtest $"{e}"

        // Review-Fund eh8xe (QA D1): zwei Übernahmen zugleich schreiben nicht doppelt (`TargetTakeoverGuard`).
        testCase "Zwei Übernahmen zugleich halten je Kategorie genau ein Ereignis fest" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let goals =
                match Server.YnabClient.interpretCategoryGoalsResponse "b" 200 (goalsJson (goalFields |> List.map (fun (n, f) -> Guid.NewGuid(), n, f))) with
                | Ok g -> g
                | Error e -> failtest $"{e}"
            let slowGoals () =
                async {
                    do! Async.Sleep 100
                    return Ok goals
                }
            let both =
                [ CommandHandlers.takeOverTargetsWith connect slowGoals now; CommandHandlers.takeOverTargetsWith connect slowGoals now ]
                |> Async.Parallel
                |> run
            Expect.equal (both |> Array.sumBy (function Ok r -> r.TakenOver.Length | Error _ -> 0)) 8 "zusammen acht übernommen"
            Expect.equal
                (Dapper.SqlMapper.ExecuteScalar<int64>(anchor, "SELECT COUNT(*) FROM events WHERE event_type = 'TargetTakenOverFromYnab'"))
                8L
                "acht Ereignisse, nicht sechzehn"

        // Review-Fund eh8xe (QA D1, zweiter Fall): Übernahme und Target ändern derselben Kategorie zugleich. Die
        // Übernahme darf nicht hinter dem TargetSet liegen, sonst überdeckte sie das gesetzte Target.
        testCase "Übernahme und Target ändern derselben Kategorie zugleich: keine Übernahme hinter dem gesetzten Target" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let k = Guid.NewGuid()
            let slowGoals () =
                async {
                    do! Async.Sleep 100
                    return Ok [ { Id = k; Name = "Urlaub"; Target = Some fifty } ]
                }
            let takeover = CommandHandlers.takeOverTargetsWith connect slowGoals now |> Async.StartAsTask
            let set =
                CommandHandlers.changeTargetWith connect noYnab now current current (request k None (Some (created Monthly 70m None)))
                |> Async.StartAsTask
            takeover.Wait()
            set.Wait()
            let events =
                match Budgeting.Persistence.loadBudgetEventsOn anchor (Budgeting.Persistence.InStream (streamIdOf (TargetStream k))) with
                | Ok events -> events |> List.map (fun e -> e.Event)
                | Error e -> failtest e
            let types = events |> List.map eventTypeName
            let lastSet = types |> List.tryFindIndexBack ((=) "TargetSet")
            let lastTakeover = types |> List.tryFindIndexBack ((=) "TargetTakenOverFromYnab")
            match lastSet, lastTakeover with
            | Some s, Some t -> Expect.isLessThan t s $"Übernahme vor dem TargetSet: {types}"
            | _ -> ()
            match set.Result with
            | Ok (Ok (TargetWritten _)) -> Expect.equal (ruleAt "2026-11" events |> Option.map (fun r -> r.Goal)) (Some (eur 70m)) "gesetztes Target gilt"
            | Ok (Ok (TargetChangedMeanwhile shown)) -> Expect.equal shown (Some fifty) "Übernahme zuerst, gezeigt war keins"
            | other -> failtest $"{other}"

        // Review-Fund eh8xe (Backend W1): zeigte der Monat ein Goal, /categories aber keins, fragte jedes Lesen
        // /categories erneut.
        testCase "GIVEN der Monat zeigt ein Goal, /categories keins WHEN zweimal gelesen THEN fragt BB /categories nur einmal" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let k = Guid.NewGuid()
            let raw : YnabMonth =
                { Month = current
                  ToBeBudgeted = eur 0m
                  Categories =
                    [ { Id = k; Name = "Urlaub"; GroupName = "Rücklagen"; Assigned = eur 0m; Activity = eur 0m; Available = eur 0m
                        Target = Some fifty; Hidden = false; Deleted = false; Internal = false } ] }
            let probe = GoalsProbe [ { Id = k; Name = "Urlaub"; Target = None } ]
            let port : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok raw }
                  SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben" }
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = probe.Fetch }
            for _ in 1..2 do
                match QueryHandlers.budgetMonthWith connect port now current |> run with
                | Ok m -> Expect.equal m.Groups.Head.Categories.Head.Target None "kein Target"
                | Error e -> failtest $"{e}"
            Expect.equal probe.Calls 1 "/categories nur beim ersten Lesen"

        testCase "Ein vergangener Monat mit Goal fragt /categories nicht" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let k = Guid.NewGuid()
            let raw : YnabMonth =
                { Month = "2026-10-01"
                  ToBeBudgeted = eur 0m
                  Categories =
                    [ { Id = k; Name = "Urlaub"; GroupName = "Rücklagen"; Assigned = eur 0m; Activity = eur 0m; Available = eur 0m
                        Target = Some fifty; Hidden = false; Deleted = false; Internal = false } ] }
            let probe = GoalsProbe [ { Id = k; Name = "Urlaub"; Target = Some fifty } ]
            let port : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok raw }
                  SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben" }
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = probe.Fetch }
            Expect.isOk (QueryHandlers.budgetMonthWith connect port now "2026-10-01" |> run) "gelesen"
            Expect.equal probe.Calls 0 "kein Abruf"

        // Review-Fund eh8xe (Backend W2): scheiterte die Übernahme, planten Befehle ohne die Targets weiter.
        testCase "GIVEN die Übernahme scheitert WHEN zugewiesen wird THEN entscheidet der Befehl nichts, Lesen und Vorschau gehen weiter" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let k = Guid.NewGuid()
            let monthKey = current
            let raw : YnabMonth =
                { Month = monthKey
                  ToBeBudgeted = eur 100m
                  Categories =
                    [ { Id = k; Name = "Urlaub"; GroupName = "Rücklagen"; Assigned = eur 0m; Activity = eur 0m; Available = eur 0m
                        Target = Some fifty; Hidden = false; Deleted = false; Internal = false } ] }
            let patches = ResizeArray<Guid>()
            let port : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok raw }
                  SetAssigned = fun _ id _ -> async { patches.Add id; return Ok () }
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = fun () -> async { return Error (YnabError.RateLimitExceeded 60) } }
            let assigned =
                CommandHandlers.assignMoneyWith connect port now monthKey { CategoryId = k; Assigned = eur 20m; CommandId = None } |> run
            Expect.equal assigned (Error (YnabError.RateLimitExceeded 60)) "Befehl scheitert"
            Expect.isEmpty patches "kein PATCH"
            Expect.equal
                (Dapper.SqlMapper.ExecuteScalar<int64>(anchor, "SELECT COUNT(*) FROM events WHERE event_type = 'AssignmentChangedInBb'"))
                0L
                "keine Entscheidung"
            Expect.isOk (QueryHandlers.budgetMonthWith connect port now monthKey |> run) "Lesen geht"
            Expect.isOk (CommandHandlers.previewWith connect port now monthKey |> run) "Vorschau geht"

        // Review-Fund eh8xe (QA D6c, Backend W4): ein gescheitertes Festhalten kam als Lesefehler.
        testCase "GIVEN das Anhängen scheitert WHEN die Übernahme läuft THEN kommt LocalWriteFailed" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            Dapper.SqlMapper.Execute(anchor, "CREATE TRIGGER no_events BEFORE INSERT ON events BEGIN SELECT RAISE(ABORT, 'Platte voll'); END") |> ignore
            let goals () = async { return Ok [ { Id = Guid.NewGuid(); Name = "Urlaub"; Target = Some fifty } ] }
            Expect.equal
                (CommandHandlers.takeOverTargetsWith connect goals now |> run)
                (Error (YnabError.LocalWriteFailed CommandHandlers.TargetTakeoverNotRecordedText))
                "nicht festgehalten"

        // Review-Fund eh8xe (QA D6b).
        testCase "Sind BBs Regeln nicht lesbar, scheitert das Lesen des Monats" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            BudgetEventsFixture.appendUnreadable anchor (TargetStream (Guid.NewGuid())) "TargetSet"
            let raw : YnabMonth = { Month = current; ToBeBudgeted = eur 0m; Categories = [] }
            let port : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok raw }
                  SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben" }
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = fun () -> async { return Ok [] } }
            Expect.equal
                (QueryHandlers.budgetMonthWith connect port now current |> run)
                (Error (YnabError.LocalReadFailed Budgeting.Persistence.TargetRulesUnreadableText))
                "nicht lesbar"

        // Review-Fund eh8xe (QA D3): ein vergangener Monat zeigt über den echten Leseweg die damals gültige Regel.
        testCase "Übernahme 50 und ab November 60: Oktober liest 50, November 60" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let k = Guid.NewGuid()
            Budgeting.Persistence.appendTargetTakeoverOn anchor now [ k, BudgetEvent.TargetTakenOverFromYnab { CategoryName = "Urlaub"; Rule = fifty; YnabGoalType = "NEED" } ]
            let sixty = { fifty with Goal = eur 60m }
            Expect.equal
                (CommandHandlers.changeTargetWith connect noYnab now current current (request k (Some fifty) (Some (draft 60m None))) |> run)
                (Ok (Ok (TargetWritten (Some sixty))))
                "ab November 60"
            let port : CommandHandlers.YnabPort =
                { FetchMonth =
                    fun monthKey ->
                        async {
                            return
                                Ok
                                    { Month = monthKey
                                      ToBeBudgeted = eur 0m
                                      Categories =
                                        [ { Id = k; Name = "Urlaub"; GroupName = "Rücklagen"; Assigned = eur 0m; Activity = eur 0m
                                            Available = eur 0m; Target = Some { fifty with Goal = eur 99m }
                                            Hidden = false; Deleted = false; Internal = false } ] }
                        }
                  SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben" }
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = fun () -> async { return failwith "geführt, kein Abruf" } }
            let targetIn monthKey =
                match QueryHandlers.budgetMonthWith connect port now monthKey |> run with
                | Ok m -> m.Groups.Head.Categories.Head.Target |> Option.map (fun t -> t.Goal)
                | Error e -> failtest $"{e}"
            Expect.equal (targetIn "2026-10-01", targetIn current) (Some (eur 50m), Some (eur 60m)) "Oktober 50, November 60"
    ]

// ============================================
// Historie: Texte
// ============================================

let private historyTextTests =
    let change kind goals dates : HistoryTargetChange =
        { ChangeId = Guid.NewGuid()
          OccurredAt = DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)
          Month = "2026-11-01"
          CategoryId = Guid.NewGuid()
          CategoryName = "Urlaub"
          ChangeKind = kind
          GoalBefore = fst goals |> Option.map eur
          GoalAfter = snd goals |> Option.map eur
          DateBefore = fst dates
          DateAfter = snd dates
          DayBefore = None
          DayAfter = None
          Latest = Some Written
          DateStep = None
          SentToYnab = true }
    testList "Target ändern: Texte der Historie" [
        test "Betrag, Datum gesetzt, Datum entfernt und Entfernen haben ihren Text, ohne Betrag in der Spalte" {
            let line c = historyEntryLine (TargetChanged c)
            let amount = line (change TargetEdited (Some 50m, Some 60m) (None, None))
            Expect.equal (amount.Text, amount.Change) ("Target 50,00 € → 60,00 €", None) "Betrag"
            Expect.equal (line (change TargetEdited (Some 50m, Some 50m) (None, Some "2027-06-01"))).Text "Datum 01.06.2027" "Datum"
            Expect.equal (line (change TargetEdited (Some 50m, Some 50m) (Some "2027-06-01", None))).Text "Datum entfernt" "Datum weg"
            Expect.equal
                (line (change TargetEdited (Some 1200m, Some 1500.5m) (None, Some "2027-06-01"))).Text
                "Target 1.200,00 € → 1.500,50 €, Datum 01.06.2027"
                "beides"
            Expect.equal (line (change TargetChangeKind.TargetRemoved (Some 50m, None) (None, None))).Text "Target entfernt" "entfernt"
        }
        test "Der Wochentag hat seinen Text beim Ändern, Anlegen und Wechsel" {
            let line c = (historyEntryLine (TargetChanged c)).Text
            let base' = change TargetEdited (Some 25m, Some 25m) (None, None)
            Expect.equal (line { base' with DayBefore = Some 1; DayAfter = Some 5 }) "Wochentag Freitag" "geändert"
            Expect.equal (line { base' with DayBefore = Some 1; DayAfter = None }) "Wochentag entfernt" "entfernt"
            Expect.equal
                (line { change (TargetCreated Weekly) (None, Some 25m) (None, None) with DayAfter = Some 1 })
                "Target angelegt, 25,00 € wöchentlich, Wochentag Montag"
                "angelegt"
            Expect.equal
                (line { change (TargetKindChanged Weekly) (Some 50m, Some 25m) (None, None) with DayBefore = Some 15; DayAfter = Some 0 })
                "Rhythmus wöchentlich, Wochentag Sonntag, Target 50,00 € → 25,00 €"
                "Wechsel"
        }
        test "Eine Änderung nur in BB hat keinen Stand, der Altbestand folgt dem jüngsten Ergebnis" {
            let status c = (historyEntryLine (TargetChanged c)).Status
            let removed = change TargetChangeKind.TargetRemoved (Some 50m, None) (None, None)
            Expect.equal (status { removed with SentToYnab = false; Latest = None }) None "nur in BB"
            Expect.equal (status removed) (Some HistoryWritten) "geschrieben"
            Expect.equal (status { removed with Latest = Some (Failed "kaputt") }) (Some (HistoryFailed "kaputt")) "fehlgeschlagen"
            Expect.equal (status { removed with Latest = None }) (Some HistoryOpen) "offen"
        }
        // budgeting-g2ty9 (X7): eine Änderung des Altbestands in zwei Schritten hat einen Stand aus beiden.
        test "Altbestand: Schritt 1 geschrieben und Schritt 2 fehlgeschlagen ist teilweise geschrieben" {
            let dated = change (TargetCreated Yearly) (None, Some 600m) (None, Some "2027-06-01")
            let line c = historyEntryLine (TargetChanged c)
            let partly = line { dated with DateStep = Some (Failed "kaputt") }
            Expect.equal
                (partly.Text, partly.Status)
                ("Target angelegt, 600,00 € jährlich, Datum 01.06.2027 nicht gesetzt", Some (HistoryPartlyWritten "kaputt"))
                "teilweise"
            Expect.equal (line { dated with DateStep = Some Written }).Status (Some HistoryWritten) "beide geschrieben"
            Expect.equal (line dated).Status (Some HistoryOpen) "Schritt 2 ohne Ergebnis"
        }
    ]

// ============================================
// Verluste beim Wechsel der Art
// ============================================

let private lossTests =
    let need = targetOf Need 50m None
    testList "Target ändern: Verluste beim Wechsel der Art" [
        test "Tag gesetzt und Wechsel ohne Wochentag ergibt LosesDay, mit Wochentag nicht" {
            Expect.equal (targetChangeLosses need (created Weekly 50m None)) [ LosesDay ] "Tag"
            Expect.isEmpty (targetChangeLosses need { created Weekly 50m None with NewDay = Some 2 }) "neuer Wochentag"
        }
        test "Datum gesetzt und Wechsel ohne Datum ergibt LosesDate" {
            let dated = { need with Day = None; Date = Some "2027-06-01" }
            Expect.equal (targetChangeLosses dated (created Yearly 50m None)) [ LosesDate ] "anderer Rhythmus"
            Expect.isEmpty (targetChangeLosses dated (created Yearly 50m (Some "2027-07-01"))) "neues Datum im Entwurf"
        }
        test "MF ergibt LosesKind, alle 3 Monate LosesRhythm, reine Betragsänderung nichts" {
            let plain = { need with Day = None }
            Expect.equal (targetChangeLosses { plain with Kind = MonthlyFunding } (created Weekly 50m None)) [ LosesKind ] "MF"
            Expect.equal (targetChangeLosses { plain with Rhythm = Months 3 } (created Monthly 50m None)) [ LosesRhythm ] "alle 3 Monate"
            Expect.isEmpty (targetChangeLosses need (draft 60m None)) "nur Betrag"
        }
    ]

// ============================================
// Grenze
// ============================================

let private boundaryTests =
    let wanted goal date = Some { NewGoal = goal; NewDate = date; NewRhythm = KeepRhythm; NewDay = None }
    let req goal date : TargetChangeRequest = request (Guid.NewGuid()) (Some fifty) (wanted goal date)
    let errorsOf month r =
        match Server.Validation.validateTargetChange current month r with
        | Error errors -> errors
        | Ok () -> []
    testList "Target ändern: Grenze" [
        test "Ungültiger, vergangener und künftiger Monat werden abgelehnt" {
            Expect.equal
                (errorsOf "kein-monat" (req (eur 1m) None))
                [ "Ungültiger Monat: 'kein-monat'. Erwartet wird der Monatserste als JJJJ-MM-01." ]
                "ungültig"
            Expect.equal (errorsOf "2026-10-01" (req (eur 1m) None)) [ TargetEditMonthText ] "vergangen"
            Expect.equal (errorsOf "2026-12-01" (req (eur 1m) None)) [ TargetEditMonthText ] "künftig"
            Expect.isOk (Server.Validation.validateTargetChange current current (req (eur 1m) None)) "laufend"
            let create = { req (eur 1m) None with Shown = None; Wanted = Some (created Monthly 1m None) }
            Expect.equal (errorsOf "2026-10-01" create) [ TargetCreateMonthText ] "anlegen im Vormonat"
        }
        test "Alle Fehler werden gesammelt, auch Wochentag und leere Befehls-Id" {
            let r =
                { req { Amount = -1m; Currency = "USD" } (Some "01.06.2027") with
                    CategoryId = Guid.Empty
                    CommandId = Some Guid.Empty }
            let r = { r with Wanted = r.Wanted |> Option.map (fun d -> { d with NewDay = Some 9 }) }
            let errors = errorsOf "2026-10-01" r
            Expect.equal errors.Length 7 $"Monat, Kategorie, Währung, Betrag, Datum, Wochentag, Befehls-Id: {errors}"
            Expect.contains errors TargetWeekdayText "Wochentag"
        }
        test "Betrag negativ, nicht auf den Cent, über der Obergrenze und Datum falsch werden je gemeldet" {
            let one goal date = errorsOf current (req goal date)
            Expect.stringContains (one (eur -0.01m) None |> String.concat "") "negativ" "negativ"
            Expect.stringContains (one (eur 1.005m) None |> String.concat "") "Cent" "Cent"
            Expect.stringContains (one (eur 1000000.01m) None |> String.concat "") "zu groß" "Obergrenze"
            Expect.stringContains (one (eur 1m) (Some "2027-02-30") |> String.concat "") "Datum" "kein Kalendertag"
            Expect.isEmpty (one (eur 0m) (Some "2027-02-28")) "0 und ein echtes Datum gehen"
        }
        test "Anlegen ohne Rhythmus und Anlegen mit 0 € werden an der Grenze mitgesammelt" {
            let create goal rhythm : TargetChangeRequest =
                request (Guid.NewGuid()) None (Some { NewGoal = goal; NewDate = None; NewRhythm = rhythm; NewDay = None })
            Expect.equal (errorsOf current (create (eur 0m) KeepRhythm)) [ TargetRhythmMissingText; TargetCreateZeroText ] "beide"
            Expect.isEmpty (errorsOf current (create (eur 50m) (SetRhythm Monthly))) "50 € monatlich geht"
        }
        // budgeting-eh8xe: verhindert, dass ein Client am Formular vorbei ein wöchentliches Target ohne
        // Wochentag speichert. Vor dem Fix ließ die Grenze den Wunsch durch.
        test "Ein Target, das nach dem Speichern wöchentlich ohne Wochentag wäre, lehnt die Grenze ab" {
            let one shown wanted = errorsOf current (request (Guid.NewGuid()) shown (Some wanted))
            Expect.equal (one None (created Weekly 25m None)) [ TargetWeekdayMissingText ] "neu wöchentlich ohne Tag"
            Expect.equal (one (Some fifty) (created Weekly 25m None)) [ TargetWeekdayMissingText ] "monatlich auf wöchentlich ohne Tag"
            Expect.isEmpty (one (Some (weekly (Some 3))) (draft 40m None)) "Betrag ändern behält den Tag"
            Expect.isEmpty (one None { created Weekly 25m None with NewDay = Some 3 }) "neu wöchentlich mit Tag"
            Expect.isEmpty (one (Some fifty) { created Weekly 25m None with NewDay = Some 0 }) "Wechsel auf wöchentlich mit Tag"
            Expect.isEmpty (errorsOf current (request (Guid.NewGuid()) (Some (weekly None)) None)) "entfernen geht immer"
        }
        test "Über die BudgetApi: Target ändern ohne Einrichtung, mit Uhr und Monatsart aus Berlin" {
            Budgeting.Persistence.initializeBudgetingSchema ()
            let k = Guid.NewGuid()
            takeOver k fifty
            let framePort _ _ : QueryHandlers.BudgetFramePort =
                { FetchMonths = (fun () -> async { return Ok [] }); FetchCategoryOrder = (fun () -> async { return Ok [] }); LoadMonthStartHidden = (fun _ -> async { return Ok [] }) }
            // In Berlin schon der 1. Dezember: der November ist vergangen.
            let lateUtc = DateTime(2026, 11, 30, 23, 30, 0, DateTimeKind.Utc)
            let api =
                Server.Api.budgetApiWith
                    (fun _ -> async { return None })
                    (fun _ _ -> failwith "Target ändern braucht keinen YNAB-Port")
                    framePort
                    (fun () -> lateUtc)
                    (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
            let r = { req (eur 60m) None with CategoryId = k }
            Expect.equal (api.changeCategoryTarget "2026-11-01" r |> run) (Error TargetEditMonthText) "November vergangen"
            Expect.equal (api.changeCategoryTarget "2026-12-01" r |> run) (Ok (TargetWritten (Some { fifty with Goal = eur 60m }))) "Dezember"
            use conn = global.Persistence.getConnection ()
            let stamped =
                Dapper.SqlMapper.ExecuteScalar<string>(
                    conn,
                    "SELECT occurred_at FROM events WHERE stream_id = @stream AND event_type = 'TargetSet'",
                    {| stream = streamIdOf (TargetStream k) |})
            Expect.equal stamped "2026-11-30T23:30:00.0000000Z" "mit der Uhr gestempelt"
            match Budgeting.Persistence.loadTargetChanges k "2026-12-01" |> run with
            | Ok [ read ] -> Expect.equal read.Month "2026-12-01" "gültig ab dem laufenden Monat"
            | other -> failtest $"eine Änderung im Dezember erwartet: {other}"
        }
    ]

[<Tests>]
let tests =
    testList "Targets in BB" [
        planTests
        weekdayTests
        foldTests
        takeoverTests
        flowTests
        takeoverFlowTests
        historyTextTests
        lossTests
        boundaryTests ]
