module Tests.TargetToYnabTests

// budgeting-e2z7v, Nachtrag ADR 0084 vom 2026-11-08, ADR 0072: Targets beim Speichern nach YNAB. Die reine Planung
// (BB-Regel gegen YNABs Goal ergibt die Aufrufe und was YNAB nicht zeigt), der Rumpf des PATCH, die offene
// Schreibung im Target-Stream, Target ändern mit Schreiben, Erneut senden und der einmalige Lauf gegen
// In-Memory-SQLite und ein Fake-YNAB. Kein Live-Aufruf.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.YnabClient
open Server.Budgeting
open Server.Budgeting.TargetRules
open Budgeting.Events

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private run x = Async.RunSynchronously x

let private rule (rhythm: TargetRhythm) (goal: decimal) (date: string option) : Target =
    { Goal = eur goal
      Rhythm = rhythm
      Kind = Need
      Date = date
      NeedsWholeAmount = Some true
      Day = None
      CreatedIn = Some "2026-11-01" }

let private targetCall (milliunits: int64) (frequency: TargetFrequency) : GoalPatch =
    { emptyGoalPatch with GoalTarget = SendValue milliunits; Frequency = Some frequency; NeedsWholeAmount = Some true }

let private dateCall (date: string) : GoalPatch = { emptyGoalPatch with TargetDate = SendValue date }

// ============================================
// Reine Planung
// ============================================

let private planTests =
    testList "Target nach YNAB: Planung je Art (ohne Goal in YNAB)" [
        testCase "monatlich: ein Aufruf mit goal_target, goal_frequency und goal_needs_whole_amount" <| fun () ->
            let plan = planGoalWrite None (Some (rule (Months 1) 50m None))
            Expect.equal plan.Calls [ targetCall 50000L Monthly ] "ein Aufruf"
            Expect.isEmpty plan.Gaps "nichts verloren"

        testCase "wöchentlich ohne Wochentag: weekly, nichts verloren" <| fun () ->
            let plan = planGoalWrite None (Some (rule (Weeks 1) 20m None))
            Expect.equal plan.Calls [ targetCall 20000L Weekly ] "weekly"
            Expect.isEmpty plan.Gaps "kein Wochentag, nichts verloren"

        testCase "wöchentlich mit Wochentag: weekly, der Wochentag fehlt in YNAB" <| fun () ->
            let plan = planGoalWrite None (Some { rule (Weeks 1) 20m None with Day = Some 3 })
            Expect.equal plan.Calls [ targetCall 20000L Weekly ] "weekly, goal_day ist nicht schreibbar"
            Expect.equal plan.Gaps [ WeekdayMissing ] "ohne Wochentag"
            Expect.equal (plan.Gaps |> List.map ynabGoalGapText) [ "In YNAB ohne Wochentag" ] "Wortlaut"

        testCase "jährlich: yearly" <| fun () ->
            let plan = planGoalWrite None (Some (rule (Years 1) 1200m None))
            Expect.equal plan.Calls [ targetCall 1200000L Yearly ] "yearly"
            Expect.isEmpty plan.Gaps "nichts verloren"

        testCase "jährlich mit Datum: zwei Aufrufe, erst Betrag mit yearly, dann das Datum (ADR 0072 Punkt 5)" <| fun () ->
            let plan = planGoalWrite None (Some (rule (Years 1) 1200m (Some "2027-06-01")))
            Expect.equal plan.Calls [ targetCall 1200000L Yearly; dateCall "2027-06-01" ] "Reihenfolge"
            Expect.isEmpty plan.Gaps "nichts verloren"

        testCase "einmalig mit Datum: monatlich mit Datum, in YNAB läuft es danach weiter" <| fun () ->
            let plan = planGoalWrite None (Some (rule NoRhythm 600m (Some "2027-03-01")))
            Expect.equal plan.Calls [ targetCall 600000L Monthly; dateCall "2027-03-01" ] "monatlich, dann Datum"
            Expect.equal plan.Gaps [ RhythmDiffers (NoRhythm, Months 1) ] "Rhythmus weicht ab"
            Expect.equal (plan.Gaps |> List.map ynabGoalGapText) [ "In YNAB monatlich statt einmalig" ] "Wortlaut"

        testCase "ohne Target: goal_target null, wenn YNAB ein Goal hat, sonst nichts" <| fun () ->
            Expect.equal
                (planGoalWrite (Some (rule (Months 1) 50m None)) None).Calls
                [ { emptyGoalPatch with GoalTarget = SendNull } ]
                "entfernen"
            Expect.isEmpty (planGoalWrite None None).Calls "kein Goal, nichts zu tun"
    ]

let private diffTests =
    testList "Target nach YNAB: Planung gegen YNABs Goal" [
        testCase "Gleiche Art und Rhythmus: nur goal_target, ohne goal_frequency, Tag und Wochentag bleiben" <| fun () ->
            let inYnab = { rule (Weeks 1) 20m None with Day = Some 6 }
            let plan = planGoalWrite (Some inYnab) (Some { inYnab with Goal = eur 25m })
            Expect.equal plan.Calls [ { emptyGoalPatch with GoalTarget = SendValue 25000L } ] "nur der Betrag"
            Expect.isEmpty plan.Gaps "der Wochentag bleibt in YNAB"

        testCase "Gleiche Form: Datum entfernen sendet goal_target_date null" <| fun () ->
            let inYnab = rule (Months 1) 50m (Some "2027-01-01")
            Expect.equal
                (planGoalWrite (Some inYnab) (Some { inYnab with Date = None })).Calls
                [ { emptyGoalPatch with TargetDate = SendNull } ]
                "nur das Datum"

        testCase "Gleiche Form und gleiche Werte: kein Aufruf" <| fun () ->
            let same = { rule (Months 1) 50m None with Day = Some 6 }
            Expect.isEmpty (planGoalWrite (Some same) (Some same)).Calls "YNAB steht schon auf BBs Target"

        testCase "Art, die BB nicht anlegt (MF), bleibt bei gleicher Form ohne goal_frequency" <| fun () ->
            let mf = { rule (Months 1) 100m None with Kind = MonthlyFunding }
            let plan = planGoalWrite (Some mf) (Some { mf with Goal = eur 120m })
            Expect.equal plan.Calls [ { emptyGoalPatch with GoalTarget = SendValue 120000L } ] "nur der Betrag"
            Expect.isEmpty plan.Gaps "MF bleibt"

        testCase "Wechsel des Rhythmus: goal_frequency ersetzt, der Tag geht in YNAB verloren" <| fun () ->
            let inYnab = { rule (Months 1) 50m None with Day = Some 6 }
            let plan = planGoalWrite (Some inYnab) (Some { rule (Weeks 1) 20m None with Day = Some 2 })
            Expect.equal plan.Calls [ targetCall 20000L Weekly ] "weekly"
            Expect.equal plan.Gaps [ WeekdayMissing ] "Wochentag fehlt"

        testCase "Zielstand mit Datum, YNAB weicht ab: NEED monatlich, Art und Rhythmus fehlen in YNAB" <| fun () ->
            let tbd = { rule NoRhythm 900m (Some "2027-01-01") with Kind = TargetBalanceByDate }
            let plan = planGoalWrite (Some (rule (Months 1) 1m None)) (Some tbd)
            Expect.equal plan.Calls [ targetCall 900000L Monthly; dateCall "2027-01-01" ] "zwei Aufrufe"
            Expect.equal plan.Gaps [ KindDiffers (TargetBalanceByDate, Need); RhythmDiffers (NoRhythm, Months 1) ] "Art und Rhythmus"
    ]

let private gapTests =
    testList "Target nach YNAB: Lücken und Stand für die Seite" [
        testCase "Lücken vergleichen nur, was BB nicht schreiben kann" <| fun () ->
            let bb = { rule (Weeks 1) 20m None with Day = Some 3 }
            Expect.isEmpty (goalGaps bb (Some { bb with Goal = eur 99m; Date = Some "2027-01-01" })) "Betrag und Datum zählen nicht"
            Expect.equal (goalGaps bb (Some { bb with Day = Some 5 })) [ WeekdayDiffers 5 ] "anderer Wochentag"
            Expect.equal (goalGaps { rule (Months 1) 50m None with Day = Some 6 } (Some (rule (Months 1) 50m None))) [ DayMissing ] "Tag fehlt"
            Expect.isEmpty (goalGaps bb None) "ohne Goal keine Lücke"

        testCase "Offen heißt Nicht in YNAB ohne Lücken; sonst die Lücken; sonst nichts" <| fun () ->
            let bb = { rule (Weeks 1) 20m None with Day = Some 3 }
            let openWrite = Some { DecisionEventId = Guid.NewGuid(); Rule = Some bb; Reason = "Rate-Limit" }
            Expect.equal (targetYnabState (Some bb) None openWrite) (Some { NotInYnab = Some "Rate-Limit"; Gaps = [] }) "offen"
            Expect.equal
                (targetYnabState (Some bb) (Some { bb with Day = None }) None)
                (Some { NotInYnab = None; Gaps = [ WeekdayMissing ] })
                "Lücke"
            Expect.equal (targetYnabState (Some bb) (Some bb) None) None "nichts zu melden"
    ]

let private foldTests =
    let set = BudgetEvent.TargetSet { Rule = rule (Months 1) 60m None; EffectiveFrom = "2026-11" }
    let takeover = BudgetEvent.TargetTakenOverFromYnab { CategoryName = "Urlaub"; Rule = rule (Months 1) 50m None; YnabGoalType = "NEED" }
    testList "Target nach YNAB: offene Schreibung im Target-Stream" [
        testCase "Die Übernahme ist in YNAB, ein TargetSet ohne Ergebnis ist offen" <| fun () ->
            let t, s = Guid.NewGuid(), Guid.NewGuid()
            Expect.isNone (openTargetWrite [ t, takeover ]) "Übernahme"
            Expect.equal
                (openTargetWrite [ t, takeover; s, set ])
                (Some { DecisionEventId = s; Rule = Some (rule (Months 1) 60m None); Reason = TargetNotSentText })
                "noch nicht gesendet"

        testCase "Fehlschlag trägt den Grund, ein späterer Erfolg löst ihn auf" <| fun () ->
            let s = Guid.NewGuid()
            let failed = BudgetEvent.TargetWriteToYnabFailed ({ DecisionEventId = s }, "HTTP 500")
            Expect.equal ((openTargetWrite [ s, set; Guid.NewGuid(), failed ]) |> Option.map (fun o -> o.Reason)) (Some "HTTP 500") "Grund"
            Expect.isNone
                (openTargetWrite [ s, set; Guid.NewGuid(), failed; Guid.NewGuid(), BudgetEvent.TargetWrittenToYnab { DecisionEventId = s } ])
                "aufgelöst"

        testCase "Die letzte Entscheidung gewinnt: ein Ergebnis zu einer älteren schließt die neue nicht" <| fun () ->
            let older, newer = Guid.NewGuid(), Guid.NewGuid()
            let events =
                [ older, set
                  newer, BudgetEvent.TargetRemoved { EffectiveFrom = "2026-11" }
                  Guid.NewGuid(), BudgetEvent.TargetWrittenToYnab { DecisionEventId = older } ]
            Expect.equal (openTargetWrite events |> Option.map (fun o -> o.DecisionEventId, o.Rule)) (Some (newer, None)) "Entfernen offen"

        testCase "Schreib-Ergebnisse führen kein Target und ändern BBs Regel nicht" <| fun () ->
            let written = BudgetEvent.TargetWrittenToYnab { DecisionEventId = Guid.NewGuid() }
            Expect.isFalse (leadsTarget written) "führt nicht"
            Expect.equal (ruleAt "2026-11" [ takeover; written ]) (ruleAt "2026-11" [ takeover ]) "Regel gleich"
            Expect.isFalse (List.exists (fun t -> List.contains t EventTypes.targetRules) EventTypes.targetWrites) "nicht in targetRules"

        testCase "Codec: beide Ergebnisse lesen sich zurück, der Actor ist YnabMirror" <| fun () ->
            for event in
                [ BudgetEvent.TargetWrittenToYnab { DecisionEventId = Guid.NewGuid() }
                  BudgetEvent.TargetWriteToYnabFailed ({ DecisionEventId = Guid.NewGuid() }, "Aufruf 2 von 2: HTTP 400") ] do
                Expect.equal (decodePayload (eventTypeName event) SchemaVersion (encodePayload event)) (Ok event) "zurück"
                Expect.equal (actorKindOf event) "YnabMirror" "Actor"
    ]

// ============================================
// Rumpf des PATCH
// ============================================

let private encoderTests =
    testList "Target nach YNAB: Rumpf des PATCH" [
        // Regression zur JSON-Zahl (CLAUDE.md): `Encode.int64` schriebe "60000" als String, YNAB verwürfe das.
        testCase "goal_target geht als JSON-Zahl in Milliunits, mit Rhythmus und Auffüllen" <| fun () ->
            let body = buildGoalBody (targetCall 60000L Monthly)
            Expect.equal body """{"category":{"goal_target":60000,"goal_frequency":"monthly","goal_needs_whole_amount":true}}""" "Rumpf"
            let parsed = Newtonsoft.Json.Linq.JObject.Parse body
            Expect.equal (parsed.SelectToken("category.goal_target").Type) Newtonsoft.Json.Linq.JTokenType.Integer "Zahl, kein String"

        testCase "Entfernen ist goal_target null, ein Datum allein nur goal_target_date" <| fun () ->
            Expect.equal (buildGoalBody { emptyGoalPatch with GoalTarget = SendNull }) """{"category":{"goal_target":null}}""" "null"
            Expect.equal (buildGoalBody (dateCall "2027-06-01")) """{"category":{"goal_target_date":"2027-06-01"}}""" "Datum"
            Expect.equal (buildGoalBody { emptyGoalPatch with TargetDate = SendNull }) """{"category":{"goal_target_date":null}}""" "Datum weg"

        testCase "Der PATCH geht an .../categories/{id}; 429 kommt als Rate-Limit" <| fun () ->
            let k = Guid.NewGuid()
            let seen = ResizeArray<string * string * string>()
            let send status = fun (m: string) (url: string) (body: string) -> async { seen.Add((m, url, body)); return status, """{"data":{"category":{"id":"%s"}}}""".Replace("%s", string k) }
            Expect.equal (patchCategoryGoalWith (send 200) "b" k (dateCall "2027-06-01") |> run) (Ok ()) "200"
            let m, url, _ = seen[0]
            Expect.equal (m, url) ("PATCH", categoriesUrl "b" (Some k)) "Methode und Pfad"
            Expect.stringEnds url $"/budgets/b/categories/{k}" "eine Kategorie"
            Expect.equal (patchCategoryGoalWith (send 429) "b" k (dateCall "2027-06-01") |> run) (Error (YnabError.RateLimitExceeded 60)) "429"
    ]

// ============================================
// Abläufe gegen Fake-YNAB und In-Memory-SQLite
// ============================================

/// Ein Fake-YNAB für Goals: hält je Kategorie das Goal, merkt jeden PATCH und wendet ihn an. `FailOn` lässt den
/// n-ten Schreibaufruf (ab 1) mit dem Fehler scheitern.
type private FakeGoals(initial: (Guid * string * Target option) list) =
    let goals = Collections.Generic.Dictionary<Guid, string * Target option>()
    do for id, name, goal in initial do goals[id] <- (name, goal)
    member val Calls = ResizeArray<Guid * GoalPatch>()
    member val FailOn : (int * YnabError) option = None with get, set
    member val Fetches = 0 with get, set
    member this.Goal (id: Guid) = snd goals[id]
    member this.Port : CommandHandlers.YnabPort =
        { FetchMonth = fun _ -> async { return failwith "kein Monat in diesem Test" }
          SetAssigned = fun _ _ _ -> async { return failwith "kein Zuweisen in diesem Test" }
          WriteGoal =
            fun id patch ->
                async {
                    this.Calls.Add((id, patch))
                    match this.FailOn with
                    | Some (n, e) when n = this.Calls.Count -> return Error e
                    | _ ->
                        let name, goal = goals[id]
                        let next =
                            match patch.GoalTarget, goal with
                            | SendNull, _ -> None
                            | SendValue m, _ when patch.Frequency.IsSome ->
                                Some { Goal = euroOfMilliunits m; Rhythm = rhythmOfFrequency patch.Frequency.Value; Kind = Need; Date = None; NeedsWholeAmount = patch.NeedsWholeAmount; Day = None; CreatedIn = None }
                            | SendValue m, Some g -> Some { g with Goal = euroOfMilliunits m }
                            | _, g -> g
                        let dated =
                            match patch.TargetDate, next with
                            | SendValue d, Some g -> Some { g with Date = Some d }
                            | SendNull, Some g -> Some { g with Date = None }
                            | _ -> next
                        goals[id] <- (name, dated)
                        return Ok ()
                }
          FetchCategoryGoals =
            fun () ->
                async {
                    this.Fetches <- this.Fetches + 1
                    return Ok [ for KeyValue (id, (name, goal)) in goals -> { Id = id; Name = name; Target = goal } ]
                } }

let private current = "2026-11-01"
let private now = DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)
let private fifty = rule (Months 1) 50m None

let private takeOver (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection) (k: Guid) (target: Target) =
    use conn = connect ()
    Budgeting.Persistence.appendTargetTakeoverOn
        conn
        (DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc))
        [ k, BudgetEvent.TargetTakenOverFromYnab { CategoryName = "Urlaub"; Rule = target; YnabGoalType = goalTypeText target.Kind } ]

/// Die Ereignisse des Target-Streams mit `event_id`.
let private stream (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection) (k: Guid) : (Guid * BudgetEvent) list =
    use conn = connect ()
    match Budgeting.Persistence.loadTargetStreamWithIdsOn conn k with
    | Ok events -> events
    | Error e -> failtest e

let private types connect (k: Guid) = stream connect k |> List.map (snd >> eventTypeName)

let private request (k: Guid) (shown: Target option) (wanted: TargetDraft option) : TargetChangeRequest =
    { CategoryId = k; Shown = shown; Wanted = wanted; CommandId = None }

let private draft (goal: decimal) : TargetDraft =
    { NewGoal = eur goal; NewDate = None; NewRhythm = KeepRhythm; NewDay = None }

let private change connect (f: FakeGoals) (r: TargetChangeRequest) =
    match CommandHandlers.changeTargetWith connect f.Port now current current r |> run with
    | Ok (Ok (TargetWritten after)) -> after
    | other -> failtest $"geschrieben erwartet: {other}"

let private rulesNow connect =
    use conn = connect ()
    match Budgeting.Persistence.queryTargetRules conn current with
    | Ok rules -> rules
    | Error e -> failtest e

let private flowTests =
    let testCase name body =
        testCase name (fun () ->
            // Eigene Datenbank je Test: der einmalige Lauf liest alle Target-Streams.
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            body connect)
    testSequenced
    <| testList "Target nach YNAB: Abläufe" [
        testCase "Target speichern hält erst TargetSet fest, dann geht der Betrag nach YNAB, das Ergebnis steht fest" <| fun connect ->
            let k = Guid.NewGuid()
            takeOver connect k fifty
            let f = FakeGoals [ k, "Urlaub", Some fifty ]
            // Beim Aufruf muss das TargetSet schon im Stream stehen.
            let seenAtCall = ResizeArray<string list>()
            let port = { f.Port with WriteGoal = fun id patch -> async { seenAtCall.Add(types connect id); return! f.Port.WriteGoal id patch } }
            match CommandHandlers.changeTargetWith connect port now current current (request k (Some fifty) (Some (draft 60m))) |> run with
            | Ok (Ok (TargetWritten (Some after))) -> Expect.equal after.Goal (eur 60m) "BBs Target gilt"
            | other -> failtest $"{other}"
            Expect.equal (List.ofSeq f.Calls) [ k, { emptyGoalPatch with GoalTarget = SendValue 60000L } ] "nur goal_target"
            Expect.equal (List.ofSeq seenAtCall) [ [ "TargetTakenOverFromYnab"; "TargetSet" ] ] "TargetSet vor dem PATCH"
            Expect.equal (types connect k) [ "TargetTakenOverFromYnab"; "TargetSet"; "TargetWrittenToYnab" ] "Ergebnis fest"
            Expect.isNone (openTargetWrite (stream connect k)) "nichts offen"

        testCase "Jährlich mit Datum anlegen: zwei Aufrufe in der Reihenfolge aus ADR 0072 Punkt 5" <| fun connect ->
            let k = Guid.NewGuid()
            let f = FakeGoals [ k, "Versicherung", None ]
            let wanted = { NewGoal = eur 1200m; NewDate = Some "2027-06-01"; NewRhythm = SetRhythm Yearly; NewDay = None }
            change connect f (request k None (Some wanted)) |> ignore
            Expect.equal
                (List.ofSeq f.Calls |> List.map snd)
                [ { emptyGoalPatch with GoalTarget = SendValue 1200000L; Frequency = Some Yearly }
                  dateCall "2027-06-01" ]
                "erst Betrag mit yearly, dann das Datum"
            Expect.equal (types connect k) [ "TargetSet"; "TargetWrittenToYnab" ] "geschrieben"

        testCase "Scheitert der erste Aufruf: BBs Target ist gespeichert, der Fehlschlag steht mit Grund fest" <| fun connect ->
            let k = Guid.NewGuid()
            takeOver connect k fifty
            let f = FakeGoals [ k, "Urlaub", Some fifty ]
            f.FailOn <- Some (1, YnabError.NetworkError "Zeitüberschreitung")
            change connect f (request k (Some fifty) (Some (draft 60m))) |> ignore
            Expect.equal ((rulesNow connect).[k].Goal) (eur 60m) "BB führt"
            Expect.equal (types connect k) [ "TargetTakenOverFromYnab"; "TargetSet"; "TargetWriteToYnabFailed" ] "Fehlschlag fest"
            Expect.equal
                (openTargetWrite (stream connect k) |> Option.map (fun o -> o.Reason))
                (Some (ynabErrorText (YnabError.NetworkError "Zeitüberschreitung")))
                "mit Grund"

        testCase "Scheitert der zweite von zwei Aufrufen: der Grund nennt ihn, Erneut senden schreibt beide und löst ihn auf" <| fun connect ->
            let k = Guid.NewGuid()
            let f = FakeGoals [ k, "Versicherung", None ]
            f.FailOn <- Some (2, YnabError.InvalidResponse "HTTP 500")
            let wanted = { NewGoal = eur 1200m; NewDate = Some "2027-06-01"; NewRhythm = SetRhythm Yearly; NewDay = None }
            change connect f (request k None (Some wanted)) |> ignore
            let reason = openTargetWrite (stream connect k) |> Option.map (fun o -> o.Reason)
            let failure = ynabErrorText (YnabError.InvalidResponse "HTTP 500")
            Expect.equal reason (Some $"Aufruf 2 von 2: {failure}") "zweiter Aufruf"
            f.FailOn <- None
            f.Calls.Clear()
            Expect.equal (CommandHandlers.retryTargetWriteWith connect f.Port now k |> run) (Ok ()) "erneut gesendet"
            // YNAB steht nach Aufruf 1 jährlich ohne Datum: gegen diesen Stand fehlt nur noch das Datum.
            Expect.equal (List.ofSeq f.Calls |> List.map snd) [ dateCall "2027-06-01" ] "nur der fehlende Teil"
            Expect.isNone (openTargetWrite (stream connect k)) "aufgelöst"
            Expect.equal (f.Goal k |> Option.map (fun g -> g.Rhythm, g.Date)) (Some (Years 1, Some "2027-06-01")) "YNAB vollständig"

        testCase "Erneut senden ohne Erfolg nennt den Grund und lässt die Schreibung offen" <| fun connect ->
            let k = Guid.NewGuid()
            takeOver connect k fifty
            let f = FakeGoals [ k, "Urlaub", Some fifty ]
            f.FailOn <- Some (1, YnabError.RateLimitExceeded 60)
            change connect f (request k (Some fifty) (Some (draft 60m))) |> ignore
            f.Calls.Clear()
            f.FailOn <- Some (1, YnabError.RateLimitExceeded 60)
            Expect.equal
                (CommandHandlers.retryTargetWriteWith connect f.Port now k |> run)
                (Error (ynabErrorText (YnabError.RateLimitExceeded 60)))
                "Grund"
            Expect.isSome (openTargetWrite (stream connect k)) "offen"

        testCase "Target entfernen sendet goal_target null" <| fun connect ->
            let k = Guid.NewGuid()
            takeOver connect k fifty
            let f = FakeGoals [ k, "Urlaub", Some fifty ]
            Expect.isNone (change connect f (request k (Some fifty) None)) "entfernt"
            Expect.equal (List.ofSeq f.Calls |> List.map snd) [ { emptyGoalPatch with GoalTarget = SendNull } ] "null"
            Expect.isNone (f.Goal k) "in YNAB weg"

        testCase "Unverändertes Target und eine Wiederholung derselben Befehls-Id senden nichts" <| fun connect ->
            let k = Guid.NewGuid()
            takeOver connect k fifty
            let f = FakeGoals [ k, "Urlaub", Some fifty ]
            change connect f (request k (Some fifty) (Some (draft 50m))) |> ignore
            Expect.isEmpty f.Calls "unverändert"
            let id = Guid.NewGuid()
            let r = { request k (Some fifty) (Some (draft 60m)) with CommandId = Some id }
            change connect f r |> ignore
            change connect f r |> ignore
            Expect.equal f.Calls.Count 1 "einmal gesendet"

        testCase "Einmaliger Lauf: schreibt, was abweicht, meldet Abweichungen, BBs Targets bleiben gleich" <| fun connect ->
            let same, stale, weekly = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            takeOver connect same fifty
            takeOver connect stale fifty
            takeOver connect weekly (rule (Weeks 1) 20m None)
            // Seit budgeting-eh8xe in BB geändert und nie gesendet: offen.
            use conn = connect ()
            for k, r in [ stale, { fifty with Goal = eur 80m }; weekly, { rule (Weeks 1) 25m None with Day = Some 3 } ] do
                Budgeting.Persistence.recordTargetChangeOn conn None now k (BudgetEvent.TargetSet { Rule = r; EffectiveFrom = "2026-11" }) |> ignore
            let f = FakeGoals [ same, "A", Some fifty; stale, "B", Some fifty; weekly, "C", Some { rule (Weeks 1) 20m None with Day = Some 6 } ]
            let before = rulesNow connect
            match CommandHandlers.writeAllTargetsWith connect f.Port now |> run with
            | Ok report ->
                Expect.isFalse report.RateLimited "ohne 429"
                Expect.equal
                    (report.Lines |> List.map (fun l -> l.CategoryName, l.Outcome))
                    [ "A", GoalAlreadyInYnab
                      "B", GoalWritten []
                      "C", GoalWritten [ WeekdayDiffers 6 ] ]
                    "je Kategorie"
            | Error e -> failtest e
            Expect.equal f.Fetches 1 "ein GET /categories"
            Expect.equal (List.ofSeq f.Calls |> List.map fst) [ stale; weekly ] "nur was abweicht"
            Expect.equal (rulesNow connect) before "Faltung gleich"
            Expect.isNone (openTargetWrite (stream connect stale)) "B in YNAB"
            Expect.equal (types connect same) [ "TargetTakenOverFromYnab" ] "A ohne Ereignis"

        testCase "Einmaliger Lauf bricht bei 429 ab und nennt die offenen Kategorien" <| fun connect ->
            let a, b, c = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            use conn = connect ()
            for k in [ a; b; c ] do
                Budgeting.Persistence.recordTargetChangeOn conn None now k (BudgetEvent.TargetSet { Rule = fifty; EffectiveFrom = "2026-11" }) |> ignore
            let f = FakeGoals [ a, "A", None; b, "B", None; c, "C", None ]
            f.FailOn <- Some (2, YnabError.RateLimitExceeded 60)
            let before = rulesNow connect
            match CommandHandlers.writeAllTargetsWith connect f.Port now |> run with
            | Ok report ->
                Expect.isTrue report.RateLimited "abgebrochen"
                Expect.equal
                    (report.Lines |> List.map (fun l -> l.CategoryName, l.Outcome))
                    [ "A", GoalWritten []
                      "B", GoalWriteFailed (ynabErrorText (YnabError.RateLimitExceeded 60))
                      "C", GoalNotAttempted ]
                    "C offen"
            | Error e -> failtest e
            Expect.equal f.Calls.Count 2 "nach dem 429 kein Aufruf mehr"
            Expect.equal (types connect c) [ "TargetSet" ] "C ohne Ereignis, bleibt gelb"
            Expect.isSome (openTargetWrite (stream connect c)) "C offen"
            Expect.equal (rulesNow connect) before "Faltung gleich"
    ]

[<Tests>]
let tests =
    testList "Targets nach YNAB" [ planTests; diffTests; gapTests; foldTests; encoderTests; flowTests ]
