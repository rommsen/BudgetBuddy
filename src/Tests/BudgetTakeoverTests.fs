module Tests.BudgetTakeoverTests

// budgeting-dkjc5, ADR 0084 Punkt 5 (Nachtrag zur Verfeinerung von qm5x9 und dgrp7): Übernahme im Hintergrund.
// Reine Regeln (Warteschlange, Takt, Log), `ensureTakenOverWith` gegen In-Memory-SQLite und die Schleife mit einer
// simulierten Uhr: Die Wartefunktion zählt die Zeit hoch, der Fake-YNAB zählt die Monatsaufrufe.
//
// Stellen, die den Live-Port anschließen (Test-Doktrin Punkt 3), und ihr Beleg:
// - `Api.liveTakeoverPort`: Wächter `findMissingApiWirings` in StructureGuardTests.fs (Settings über
//   `ynabAccessWith`, `getMonths`, `getMonth`, `loadTakenOverMonths`, `ensureTakenOver`). `ynabAccessWith` und
//   `ynabSettingsWith` (Schlüssel, die die Budget-Seite mitbenutzt) prüft "Settings" hier direkt.
// - `Program.fs`: Wächter `isTakeoverWorkerWired` mit Gegenbeweis in StructureGuardTests.fs, Registrierung nur dort
//   (`findHostedServiceRegistrations`).
// - `ensureTakenOver` und `loadTakenOverMonths`: der Fake-Port der Schleife nutzt die Live-Fassungen (die Test-DB
//   ist dieselbe In-Memory-DB), eine leere Fassung macht die Schleifentests rot.

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain
open Server.Budgeting.CommandHandlers
open Server.Budgeting.TakeoverWorker
open Budgeting.Events

let private run x = Async.RunSynchronously x

let private berlin =
    match Server.ServerConfig.tryResolveBerlinZone () with
    | Ok zone -> zone
    | Error message -> failwith message

let private eur (milliunits: int64) : Money = { Amount = decimal milliunits / 1000m; Currency = "EUR" }

let private yearCounter = ref 0

/// Ein eigenes Jahrzehnt je Test: Die übernommenen Monate liest der Dienst aus dem ganzen Kontext, die Monate
/// anderer Tests liegen so nie im eigenen Fenster.
let private freshYear () : int = 6000 + 10 * Interlocked.Increment yearCounter

let private key (year: int) (month: int) : string = $"{year}-{month:D2}-01"

/// `count` Monate ab Januar `year`, aufsteigend.
let private monthsFrom (year: int) (count: int) : string list =
    [ for i in 0 .. count - 1 -> key (year + i / 12) (i % 12 + 1) ]

let private takeoverCount (monthKey: string) : int =
    use conn = global.Persistence.getConnection ()
    match Budgeting.Persistence.loadMonthEventsOn conn monthKey with
    | Ok events ->
        events
        |> List.filter (fun (_, e) ->
            match e with
            | BudgetEvent.BudgetTakenOverFromYnab _ -> true
            | _ -> false)
        |> List.length
    | Error message -> failtest message

/// Die Simulation endet hier, nicht im getesteten Code.
exception private SimulationOver

/// Simulierte Zeit ab `start` (UTC). `Delay` zählt hoch und beendet die Simulation, sobald `stop` zutrifft.
type private Sim(start: DateTime, stop: TimeSpan -> TimeSpan -> bool) =
    member val Elapsed = TimeSpan.Zero with get, set
    member val Delays = ResizeArray<TimeSpan>()
    member this.Clock () : DateTime = start + this.Elapsed
    member this.Delay (span: TimeSpan) : Async<unit> =
        async {
            this.Delays.Add span
            this.Elapsed <- this.Elapsed + span
            if stop this.Elapsed span then raise SimulationOver
        }

/// Fake-YNAB: `Months` ist das Fenster, jeder Monat hat eine Kategorie. `Calls` hält je Monatsaufruf die simulierte
/// Zeit und den Monat fest. `FailOnce` antwortet einmal mit dem Fehler, `FailAlways` immer.
type private FakeYnab(months: string list, sim: Sim) =
    let category = Guid.NewGuid()
    member val Calls = ResizeArray<TimeSpan * string>()
    member val MonthListCalls = ResizeArray<TimeSpan>()
    member val SettingsCalls = ResizeArray<TimeSpan>()
    member val FailOnce = Dictionary<string, YnabError>()
    member val FailAlways = Dictionary<string, YnabError>()
    member val Settings : TimeSpan -> YnabAccess option =
        (fun _ -> Some { Token = "token"; Budget = YnabBudgetId "budget" }) with get, set
    member this.FetchMonth (monthKey: string) : Async<YnabResult<YnabMonth>> =
        async {
            this.Calls.Add((sim.Elapsed, monthKey))
            match this.FailOnce.TryGetValue monthKey with
            | true, error ->
                this.FailOnce.Remove monthKey |> ignore
                return Error error
            | _ ->
                match this.FailAlways.TryGetValue monthKey with
                | true, error -> return Error error
                | _ ->
                    return
                        Ok
                            { Month = monthKey
                              ToBeBudgeted = eur 0L
                              Categories =
                                [ { Id = category
                                    Name = "Lebensmittel"
                                    GroupName = "Alltag"
                                    Assigned = eur 50000L
                                    Activity = eur 0L
                                    Available = eur 50000L
                                    Target = None
                                    Hidden = false
                                    Deleted = false
                                    Internal = false } ] }
        }
    member this.Port : TakeoverPort =
        { Settings =
            fun () ->
                async {
                    this.SettingsCalls.Add sim.Elapsed
                    return this.Settings sim.Elapsed
                }
          FetchMonths =
            fun _ _ ->
                async {
                    this.MonthListCalls.Add sim.Elapsed
                    return Ok months
                }
          FetchMonth = fun _ _ monthKey -> this.FetchMonth monthKey
          LoadTakenOver = Budgeting.Persistence.loadTakenOverMonths
          EnsureTakenOver = ensureTakenOver }
    member this.CallsOf (monthKey: string) = this.Calls |> Seq.filter (fun (_, m) -> m = monthKey) |> Seq.length

/// Lässt die Schleife mit `port` laufen, bis die Simulation endet, und liefert die Log-Zeilen.
let private simulatePort (port: TakeoverPort) (sim: Sim) : string list =
    let logged = ResizeArray<string>()
    try
        runWith port berlin sim.Clock sim.Delay logged.Add |> run
        failtest "die Schleife endet nicht von selbst"
    with SimulationOver -> ()
    List.ofSeq logged

/// Lässt die Schleife laufen, bis die Simulation endet.
let private simulate (fake: FakeYnab) (sim: Sim) : unit =
    simulatePort fake.Port sim |> ignore

/// Bis zur simulierten Zeit `limit`.
let private until (limit: TimeSpan) = fun (elapsed: TimeSpan) (_: TimeSpan) -> elapsed >= limit

/// Bis der Dienst nichts mehr offen meldet (die Wartezeit nach `NothingLeft`), höchstens zwei simulierte Tage: Ein
/// Fehler im Dienst soll den Test rot machen, nicht hängen lassen.
let private untilNothingLeft =
    fun (elapsed: TimeSpan) (span: TimeSpan) -> span = TimeSpan.FromHours 6.0 || elapsed > TimeSpan.FromDays 2.0

/// Mitte Juni im Jahr `year`, 10 Uhr UTC: der laufende Berliner Monat ist Juni.
let private juneOf (year: int) = DateTime(year, 6, 15, 10, 0, 0, DateTimeKind.Utc)

let private testCase name body =
    testCase name (fun () ->
        Budgeting.Persistence.initializeBudgetingSchema ()
        body ())

// ============================================
// Reine Regeln
// ============================================

let private domainTests =
    testList "Warteschlange und Takt" [
        testCase "die Warteschlange ordnet laufend, künftig aufsteigend, älter absteigend" <| fun () ->
            let months = [ "2026-03-01"; "2026-07-01"; "2026-05-01"; "2026-06-01"; "2026-08-01"; "2026-04-01" ]
            Expect.equal
                (takeoverQueue "2026-06-01" months Set.empty)
                [ "2026-06-01"; "2026-07-01"; "2026-08-01"; "2026-05-01"; "2026-04-01"; "2026-03-01" ]
                "laufend, künftig, älter"

        testCase "übernommene Monate fallen aus der Warteschlange, auch der laufende" <| fun () ->
            let months = [ "2026-04-01"; "2026-05-01"; "2026-06-01"; "2026-07-01" ]
            Expect.equal
                (takeoverQueue "2026-06-01" months (set [ "2026-06-01"; "2026-04-01" ]))
                [ "2026-07-01"; "2026-05-01" ]
                "ohne Übernommene"

        testCase "fehlt der laufende Monat im Fenster, steht er trotzdem vorn" <| fun () ->
            Expect.equal
                (takeoverQueue "2026-06-01" [ "2026-04-01"; "2026-05-01" ] Set.empty)
                [ "2026-06-01"; "2026-05-01"; "2026-04-01" ]
                "laufend vorn"

        testCase "nach jedem Ergebnis folgt die vereinbarte Wartezeit" <| fun () ->
            let minutes (m: float) = TimeSpan.FromMinutes m
            [ TakeoverOutcome.TookOver "2026-06-01", minutes 1.0
              TakeoverOutcome.MonthNotOpen "2026-06-01", minutes 1.0
              TakeoverOutcome.RateLimited, minutes 15.0
              TakeoverOutcome.Unauthorized, minutes 15.0
              TakeoverOutcome.Failed (TakeoverFailure.Ynab (YnabError.NetworkError "weg")), minutes 15.0
              TakeoverOutcome.Failed TakeoverFailure.TakenOverUnreadable, minutes 15.0
              TakeoverOutcome.Failed (TakeoverFailure.Crashed "Exception"), minutes 15.0
              TakeoverOutcome.NotConfigured, minutes 15.0
              TakeoverOutcome.NothingLeft, TimeSpan.FromHours 6.0 ]
            |> List.iter (fun (outcome, expected) -> Expect.equal (nextDelay outcome) expected $"{outcome}")

        testCase "fehlende Settings und 401 stehen einmal je Zustandswechsel im Log, andere Fehler jedes Mal" <| fun () ->
            let unauthorized = TakeoverOutcome.Unauthorized
            let network = TakeoverOutcome.Failed (TakeoverFailure.Ynab (YnabError.NetworkError "Antwort mit Daten"))
            Expect.isSome (logLine None TakeoverOutcome.NotConfigured) "Wechsel nach nicht eingerichtet"
            Expect.isNone (logLine (Some TakeoverOutcome.NotConfigured) TakeoverOutcome.NotConfigured) "unverändert"
            Expect.isSome (logLine (Some (TakeoverOutcome.TookOver "2026-06-01")) unauthorized) "Wechsel nach 401"
            Expect.isNone (logLine (Some unauthorized) unauthorized) "401 unverändert"
            Expect.isSome (logLine (Some network) network) "anderer Fehler jedes Mal"
            Expect.equal (logLine None network) (Some "Übernahme aus YNAB gescheitert: NetworkError") "mit Fallnamen, ohne Meldung"
            Expect.equal
                (logLine None (TakeoverOutcome.Failed TakeoverFailure.TakenOverUnreadable))
                (Some "Übernahme aus YNAB gescheitert: LocalReadFailed")
                "lokaler Lesefehler"
            Expect.isNone (logLine None (TakeoverOutcome.TookOver "2026-06-01")) "Übernahme ohne Zeile"
    ]

// ============================================
// Settings
// ============================================

let private settingsTests =
    testList "Settings" [
        testCase "Token und Budget kommen aus ihren Schlüsseln, fehlt eines, wartet die Übernahme" <| fun () ->
            let from (values: (string * string) list) (name: string) = async.Return (List.tryFind (fst >> (=) name) values |> Option.map snd)
            let both = from [ "ynab_token", "das-token"; "ynab_default_budget_id", "das-budget" ]
            Expect.equal (Server.Api.ynabSettingsWith both |> run) (Some "das-token", Some "das-budget") "Budget-Seite"
            Expect.equal
                (Server.Api.ynabAccessWith both |> run)
                (Some { Token = "das-token"; Budget = YnabBudgetId "das-budget" })
                "Übernahme"
            Expect.isNone (Server.Api.ynabAccessWith (from [ "ynab_token", "das-token" ]) |> run) "ohne Budget"
            Expect.isNone (Server.Api.ynabAccessWith (from [ "ynab_default_budget_id", "das-budget" ]) |> run) "ohne Token"
    ]

// ============================================
// ensureTakenOver
// ============================================

let private ensureTests =
    testList "Übernahme unter der Sperre" [
        testCase "ein vorher durch Lesen übernommener Monat kostet keinen Monatsaufruf und keine zweite Übernahme" <| fun () ->
            let year = freshYear ()
            let month = key year 6
            let sim = Sim(juneOf year, fun _ _ -> false)
            let fake = FakeYnab([ month ], sim)
            let reader : YnabPort =
                { FetchMonth = fake.FetchMonth
                  SetAssigned = fun _ _ _ -> failwith "schreibt nicht"
                  FetchCategoryGoals = fun () -> async { return Ok [] } }
            match observeMonthWith global.Persistence.getConnection reader (juneOf year) month |> run with
            | Ok _ -> ()
            | Error e -> failtest $"Lesen erwartet: {e}"
            let result = ensureTakenOverWith global.Persistence.getConnection fake.FetchMonth (juneOf year) month |> run
            Expect.equal result (Ok AlreadyTakenOver) "schon übernommen"
            Expect.equal (fake.CallsOf month) 1 "nur der Aufruf des Lesens"
            Expect.equal (takeoverCount month) 1 "ein Übernahme-Ereignis"

        testCase "ein noch nicht übernommener Monat wird mit einem Monatsaufruf übernommen" <| fun () ->
            let year = freshYear ()
            let month = key year 6
            let sim = Sim(juneOf year, fun _ _ -> false)
            let fake = FakeYnab([ month ], sim)
            let result = ensureTakenOverWith global.Persistence.getConnection fake.FetchMonth (juneOf year) month |> run
            Expect.equal result (Ok TakenOverNow) "übernommen"
            Expect.equal (fake.CallsOf month) 1 "ein Aufruf"
            Expect.equal (takeoverCount month) 1 "ein Übernahme-Ereignis"
            Expect.isTrue
                (Budgeting.Persistence.loadTakenOverMonthsWith global.Persistence.getConnection |> run |> Result.map (Set.contains month) = Ok true)
                "der Monat zählt als übernommen"

        testCase "ein 404 kommt als Monat nicht offen und hält nichts fest" <| fun () ->
            let year = freshYear ()
            let month = key year 6
            let sim = Sim(juneOf year, fun _ _ -> false)
            let fake = FakeYnab([ month ], sim)
            fake.FailAlways[month] <- YnabError.MonthNotOpen month
            let result = ensureTakenOverWith global.Persistence.getConnection fake.FetchMonth (juneOf year) month |> run
            Expect.equal result (Error (YnabError.MonthNotOpen month)) "nicht offen"
            Expect.equal (takeoverCount month) 0 "keine Übernahme"

        // Review-Entscheidung: die Prüfung vor dem YNAB-Aufruf fragt gezielt nach der Übernahme. Vorher dekodierte sie
        // den ganzen Stream, ein nicht lesbares Ereignis machte den Monat für immer zu LocalReadFailed.
        testCase "ein übernommener Monat mit einem nicht lesbaren Ereignis im Stream kostet keinen Monatsaufruf" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let year = freshYear ()
            let month = key year 6
            let sim = Sim(juneOf year, fun _ _ -> false)
            let fake = FakeYnab([ month ], sim)
            Expect.equal (ensureTakenOverWith connect fake.FetchMonth (juneOf year) month |> run) (Ok TakenOverNow) "übernommen"
            BudgetEventsFixture.appendUnknown anchor (MonthStream $"{year}-06")
            let result = ensureTakenOverWith connect fake.FetchMonth (juneOf year) month |> run
            Expect.equal result (Ok AlreadyTakenOver) "die Übernahme zählt trotz des nicht lesbaren Ereignisses"
            Expect.equal (fake.CallsOf month) 1 "kein zweiter Monatsaufruf"
    ]

// ============================================
// Die Schleife mit simulierter Uhr
// ============================================

let private loopTests =
    testList "Schleife" [
        testCase "über eine simulierte Stunde höchstens 60 Monatsaufrufe, laufender Monat zuerst" <| fun () ->
            let year = freshYear ()
            let months = monthsFrom year 100
            let sim = Sim(juneOf year, until (TimeSpan.FromHours 1.0))
            let fake = FakeYnab(months, sim)
            simulate fake sim
            let inFirstHour = fake.Calls |> Seq.filter (fun (t, _) -> t < TimeSpan.FromHours 1.0) |> Seq.length
            Expect.equal inFirstHour 60 "ein Monat je Minute, nicht mehr"
            Expect.equal
                (fake.Calls |> Seq.take 3 |> Seq.map snd |> List.ofSeq)
                [ key year 6; key year 7; key year 8 ]
                "laufender Monat, dann künftige aufsteigend"
            Expect.equal fake.MonthListCalls.Count 1 "das Fenster einmal je Durchlauf"

        testCase "nach 429 kommt der nächste Monatsaufruf erst nach 15 simulierten Minuten" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, until (TimeSpan.FromHours 1.0))
            let fake = FakeYnab(monthsFrom year 12, sim)
            fake.FailOnce[key year 8] <- YnabError.RateLimitExceeded 60
            simulate fake sim
            let calls = fake.Calls |> List.ofSeq
            let limited = calls |> List.findIndex (fun (_, m) -> m = key year 8)
            let t429, _ = calls[limited]
            let tNext, next = calls[limited + 1]
            Expect.equal (tNext - t429) (TimeSpan.FromMinutes 15.0) "15 Minuten, nicht die 60 Sekunden aus YNAB"
            Expect.equal next (key year 8) "derselbe Monat noch einmal"
            Expect.equal (takeoverCount (key year 8)) 1 "danach übernommen"

        testCase "ein vorher durch Lesen übernommener Monat kostet den Dienst keinen Aufruf" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, untilNothingLeft)
            let fake = FakeYnab(monthsFrom year 12, sim)
            let reader : YnabPort =
                { FetchMonth = fake.FetchMonth
                  SetAssigned = fun _ _ _ -> failwith "schreibt nicht"
                  FetchCategoryGoals = fun () -> async { return Ok [] } }
            match observeMonthWith global.Persistence.getConnection reader (juneOf year) (key year 7) |> run with
            | Ok _ -> ()
            | Error e -> failtest $"Lesen erwartet: {e}"
            simulate fake sim
            Expect.equal (fake.CallsOf (key year 7)) 1 "nur der Aufruf des Lesens"
            Expect.equal (takeoverCount (key year 7)) 1 "kein zweites Übernahme-Ereignis"
            Expect.equal fake.Calls.Count 12 "jeder Monat genau einmal"

        testCase "ohne Token oder Budget kein YNAB-Aufruf, nach 15 Minuten neue Prüfung, danach läuft die Übernahme an" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, until (TimeSpan.FromMinutes 40.0))
            let fake = FakeYnab(monthsFrom year 12, sim)
            fake.Settings <-
                fun elapsed ->
                    if elapsed < TimeSpan.FromMinutes 30.0 then None
                    else Some { Token = "token"; Budget = YnabBudgetId "budget" }
            simulate fake sim
            Expect.equal
                (fake.SettingsCalls |> Seq.take 3 |> List.ofSeq)
                [ TimeSpan.Zero; TimeSpan.FromMinutes 15.0; TimeSpan.FromMinutes 30.0 ]
                "alle 15 Minuten neu gelesen"
            Expect.equal (fake.MonthListCalls |> List.ofSeq) [ TimeSpan.FromMinutes 30.0 ] "YNAB erst mit Token und Budget"
            Expect.equal (fake.Calls |> Seq.head) (TimeSpan.FromMinutes 30.0, key year 6) "dann der laufende Monat"
            Expect.equal (takeoverCount (key year 6)) 1 "ohne Neustart übernommen"

        testCase "nach einem Neustart mitten im Durchlauf übernimmt der neue Durchlauf nur die fehlenden Monate" <| fun () ->
            let year = freshYear ()
            let months = monthsFrom year 12
            let first = Sim(juneOf year, until (TimeSpan.FromMinutes 5.0))
            let before = FakeYnab(months, first)
            simulate before first
            Expect.equal before.Calls.Count 5 "fünf Monate vor dem Neustart"
            let second = Sim(juneOf year + TimeSpan.FromMinutes 6.0, untilNothingLeft)
            let after = FakeYnab(months, second)
            simulate after second
            let fetchedBefore = before.Calls |> Seq.map snd |> Set.ofSeq
            let fetchedAfter = after.Calls |> Seq.map snd |> Set.ofSeq
            Expect.isEmpty (Set.intersect fetchedBefore fetchedAfter) "kein Monat zweimal gefragt"
            Expect.equal (Set.union fetchedBefore fetchedAfter) (Set.ofList months) "alle Monate"
            for month in months do
                Expect.equal (takeoverCount month) 1 $"{month} genau einmal übernommen"

        testCase "ein 404 für einen Monat überspringt nur ihn" <| fun () ->
            let year = freshYear ()
            let months = monthsFrom year 12
            let sim = Sim(juneOf year, untilNothingLeft)
            let fake = FakeYnab(months, sim)
            fake.FailAlways[key year 8] <- YnabError.MonthNotOpen (key year 8)
            simulate fake sim
            Expect.equal (fake.CallsOf (key year 8)) 1 "im Durchlauf einmal gefragt"
            Expect.equal (takeoverCount (key year 8)) 0 "nicht übernommen"
            for month in months |> List.filter ((<>) (key year 8)) do
                Expect.equal (takeoverCount month) 1 $"{month} übernommen"
            Expect.equal (sim.Delays |> Seq.last) (TimeSpan.FromHours 6.0) "danach nichts mehr offen"

        // Review-Entscheidung: ein Monat, dessen Abruf dauerhaft mit einem anderen Fehler als 404, 429 oder 401
        // scheitert, blieb vorn in der Warteschlange und kam alle 15 Minuten wieder dran; die älteren Monate nie.
        testCase "ein dauerhaft scheiternder Monat wird im Durchlauf übersprungen, die älteren werden übernommen, der nächste Durchlauf fragt ihn neu" <| fun () ->
            let year = freshYear ()
            let months = monthsFrom year 12
            let broken = key year 8
            let sim = Sim(juneOf year, until (TimeSpan.FromHours 7.0))
            let fake = FakeYnab(months, sim)
            fake.FailAlways[broken] <- YnabError.NetworkError "weg"
            simulate fake sim
            for month in months |> List.filter ((<>) broken) do
                Expect.equal (takeoverCount month) 1 $"{month} übernommen"
            let brokenCalls = fake.Calls |> Seq.filter (fun (_, m) -> m = broken) |> Seq.map fst |> List.ofSeq
            Expect.equal brokenCalls.Length 2 "je Durchlauf einmal gefragt"
            Expect.isGreaterThanOrEqual brokenCalls[1] (TimeSpan.FromHours 6.0) "der zweite Versuch erst im neuen Durchlauf"
            let calls = fake.Calls |> List.ofSeq
            let failedAt = calls |> List.findIndex (fun (_, m) -> m = broken)
            Expect.equal (fst calls[failedAt + 1] - fst calls[failedAt]) (TimeSpan.FromMinutes 15.0) "nach dem Fehler 15 Minuten Pause"
            Expect.equal (snd calls[failedAt + 1]) (key year 9) "danach der nächste Monat"

        testCase "eine Ausnahme im Schritt beendet die Schleife nicht" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, until (TimeSpan.FromMinutes 20.0))
            let fake = FakeYnab(monthsFrom year 12, sim)
            let calls = ref 0
            let port =
                { fake.Port with
                    Settings =
                        fun () ->
                            Interlocked.Increment calls |> ignore
                            if calls.Value = 1 then failwith "Settings weg" else fake.Port.Settings () }
            let logged = ResizeArray<string>()
            try
                runWith port berlin sim.Clock sim.Delay logged.Add |> run
            with SimulationOver -> ()
            Expect.equal (logged |> Seq.head) "Übernahme aus YNAB gescheitert: Exception" "Typname im Log"
            Expect.equal (fake.Calls |> Seq.head) (TimeSpan.FromMinutes 15.0, key year 6) "nach 15 Minuten weiter"

        testCase "nach sechs Stunden ohne offenen Monat beginnt ein neuer Durchlauf mit neuem Fenster" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, until (TimeSpan.FromHours 7.0))
            let fake = FakeYnab(monthsFrom year 12, sim)
            fake.FailAlways[key year 8] <- YnabError.MonthNotOpen (key year 8)
            simulate fake sim
            Expect.equal fake.MonthListCalls.Count 2 "das Fenster je Durchlauf einmal"
            Expect.isGreaterThanOrEqual fake.MonthListCalls[1] (TimeSpan.FromHours 6.0) "der zweite Durchlauf nach der Pause"
            Expect.equal (fake.CallsOf (key year 8)) 2 "der 404-Monat im neuen Durchlauf noch einmal gefragt"

        testCase "scheitert das Monatsfenster, folgt der nächste Versuch nach 15 Minuten" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, until (TimeSpan.FromMinutes 30.0))
            let fake = FakeYnab(monthsFrom year 12, sim)
            let port = { fake.Port with FetchMonths = fun _ _ -> async.Return (Error (YnabError.NetworkError "weg")) }
            let logged = simulatePort port sim
            Expect.equal (List.ofSeq sim.Delays) [ TimeSpan.FromMinutes 15.0; TimeSpan.FromMinutes 15.0 ] "15 Minuten"
            Expect.isEmpty fake.Calls "kein Monatsaufruf"
            Expect.equal logged [ for _ in 1 .. 2 -> "Übernahme aus YNAB gescheitert: NetworkError" ] "jeder Fehler im Log"

        testCase "sind die übernommenen Monate nicht lesbar, folgt der nächste Versuch nach 15 Minuten" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, until (TimeSpan.FromMinutes 30.0))
            let fake = FakeYnab(monthsFrom year 12, sim)
            let port = { fake.Port with LoadTakenOver = fun () -> async.Return (Error "nicht lesbar") }
            let logged = simulatePort port sim
            Expect.equal (List.ofSeq sim.Delays) [ TimeSpan.FromMinutes 15.0; TimeSpan.FromMinutes 15.0 ] "15 Minuten"
            Expect.isEmpty fake.Calls "kein Monatsaufruf"
            Expect.equal (List.head logged) "Übernahme aus YNAB gescheitert: LocalReadFailed" "lokaler Lesefehler im Log"

        testCase "ein ungültiges Token steht einmal im Log, YNAB wird alle 15 Minuten neu gefragt" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, until (TimeSpan.FromHours 1.0))
            let fake = FakeYnab(monthsFrom year 12, sim)
            fake.FailAlways[key year 6] <- YnabError.Unauthorized "Antwort mit Daten"
            let logged = simulatePort fake.Port sim
            Expect.equal (fake.Calls |> Seq.map fst |> List.ofSeq) [ for i in 0 .. 3 -> TimeSpan.FromMinutes (15.0 * float i) ] "alle 15 Minuten"
            Expect.equal logged [ "Übernahme aus YNAB gescheitert: Unauthorized" ] "einmal, ohne Meldung"
    ]

// ============================================
// Der Dienst
// ============================================

let private workerTests =
    testList "Dienst" [
        testCase "der Dienst übernimmt nach dem Start und endet mit dem Stopp" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, fun _ _ -> false)
            let fake = FakeYnab(monthsFrom year 12, sim)
            let firstWait = TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously)
            let delay (span: TimeSpan) =
                async {
                    firstWait.TrySetResult span |> ignore
                    do! Async.Sleep Timeout.Infinite
                }
            use worker = new TakeoverWorker(fake.Port, berlin, sim.Clock, delay)
            worker.StartAsync(CancellationToken.None).Wait()
            Expect.isTrue (firstWait.Task.Wait(TimeSpan.FromSeconds 10.0)) "der erste Schritt lief"
            Expect.equal firstWait.Task.Result (TimeSpan.FromMinutes 1.0) "nach der Übernahme eine Minute"
            Expect.equal (takeoverCount (key year 6)) 1 "laufender Monat übernommen"
            Expect.isTrue (worker.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds 10.0)) "Stopp beendet den Dienst"
            Expect.isTrue worker.ExecuteTask.IsCompleted "die Schleife ist beendet"

        // Review-Fund zu budgeting-dkjc5: Der Stopp wartete auf einen Schritt, der an YNAB oder an der Monatssperre
        // hängt (beide ohne Token). Der Dienst wartet nur bis `stopping`, wie `BankConnectionWorker`.
        testCase "der Stopp wartet nicht auf einen hängenden Schritt" <| fun () ->
            let year = freshYear ()
            let sim = Sim(juneOf year, fun _ _ -> false)
            let fake = FakeYnab(monthsFrom year 12, sim)
            let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            use release = new ManualResetEventSlim(false)
            let port =
                { fake.Port with
                    Settings =
                        fun () ->
                            async {
                                started.TrySetResult () |> ignore
                                release.Wait()
                                return None
                            } }
            use worker = new TakeoverWorker(port, berlin, sim.Clock, fun _ -> Async.Sleep Timeout.Infinite)
            try
                worker.StartAsync(CancellationToken.None).Wait()
                Expect.isTrue (started.Task.Wait(TimeSpan.FromSeconds 10.0)) "der Schritt hängt"
                Expect.isTrue (worker.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds 10.0)) "der Stopp endet trotzdem"
            finally
                release.Set()

        testCase "die Wartefunktion des Servers wartet wirklich" <| fun () ->
            let watch = Diagnostics.Stopwatch.StartNew()
            sleep (TimeSpan.FromMilliseconds 50.0) |> run
            Expect.isGreaterThanOrEqual watch.Elapsed.TotalMilliseconds 50.0 "mindestens 50 ms"
    ]

// budgeting-dkjc5
[<Tests>]
let tests =
    testList "Budget: Übernahme im Hintergrund" [ domainTests; settingsTests; ensureTests; loopTests; workerTests ]
