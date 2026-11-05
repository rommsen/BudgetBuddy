module Tests.UnderfundedComparisonTests

// budgeting-v7kfw, ADR 0084 Punkt 7: BB rechnet den Fehlbetrag selbst, parallel zu YNABs
// `goal_under_funded`, und hält die Abweichungsliste als vorläufigen Zustand fest. Reine Funktion je
// Zeile der Zuordnungstabelle, Vergleich, Einbindung in `budgetMonth` und Ablage gegen In-Memory-SQLite.
// Alle Beträge und Namen sind erfunden.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.Domain

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private run x = Async.RunSynchronously x

let private need (goal: decimal) (rhythm: TargetRhythm) : Target =
    { Goal = eur goal; Rhythm = rhythm; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None }

/// Fehlbetrag mit Zugewiesen, Übertrag und Aktivität im gelesenen Monat `month`.
let private compute (target: Target) (month: string) (assigned: decimal) (carryover: decimal) (activity: decimal) =
    underfunded target month (eur assigned) (eur carryover) (eur activity)

let private computed (amount: decimal) = Computed (eur amount)

let private isNotComputed (result: UnderfundedResult) =
    match result with
    | NotComputed _ -> true
    | Computed _ -> false

// 2026-02 hat jeden Wochentag viermal (1. Februar ist ein Sonntag, 28 Tage).
// 2026-11 beginnt am Sonntag und hat 30 Tage: Sonntag und Montag fünfmal, Dienstag viermal.
let private february = "2026-02-01"
let private november = "2026-11-01"

let private ruleTests =
    testList "Reine Funktion je Zeile der Zuordnungstabelle" [
        testList "Mit Fälligkeit" [
            testCase "NEED mit Datum: (Betrag − Übertrag) / verbleibende Monate − Zugewiesen" <| fun () ->
                let target = { need 120m (Years 1) with Date = Some "2027-01-15" }
                // November, Dezember, Januar: 3 Monate. (120 − 30) / 3 = 30, minus 10 zugewiesen.
                Expect.equal (compute target november 10m 30m 0m) (computed 20m) "drei Monate"

            testCase "TBD rechnet wie NEED mit Datum" <| fun () ->
                let target = { need 60m NoRhythm with Kind = TargetBalanceByDate; Date = Some "2026-12-31" }
                Expect.equal (compute target november 0m 0m 0m) (computed 30m) "zwei Monate"

            testCase "Ein Quotient wird auf den Cent aufgerundet: 100,00 € auf 3 Monate ergibt 33,34 €" <| fun () ->
                let target = { need 100m (Months 1) with Date = Some "2027-01-01" }
                Expect.equal (compute target november 0m 0m 0m) (computed 33.34m) "aufgerundet"

            testCase "Fälligkeit im gelesenen Monat zählt als ein Monat" <| fun () ->
                let target = { need 80m (Months 1) with Date = Some "2026-11-30" }
                Expect.equal (compute target november 5m 20m 0m) (computed 55m) "ein Monat"

            testCase "Fälligkeit vor dem gelesenen Monat zählt als ein Monat" <| fun () ->
                let target = { need 80m (Months 1) with Date = Some "2026-08-15" }
                Expect.equal (compute target november 5m 20m 0m) (computed 55m) "ein Monat"

            testCase "Zugewiesen über dem Betrag ergibt 0" <| fun () ->
                let target = { need 50m (Months 1) with Date = Some "2026-11-30" }
                Expect.equal (compute target november 70m 0m 0m) (computed 0m) "nicht negativ"

            testCase "TBD ohne Datum wird nicht gerechnet" <| fun () ->
                let target = { need 60m NoRhythm with Kind = TargetBalanceByDate }
                Expect.isTrue (isNotComputed (compute target november 0m 0m 0m)) "ohne Datum"
        ]

        testList "Auffüllen bis" [
            testCase "Betrag − (Übertrag + Zugewiesen)" <| fun () ->
                let target = { need 100m (Months 1) with NeedsWholeAmount = Some false }
                Expect.equal (compute target november 15m 40m -25m) (computed 45m) "aufgefüllt"

            testCase "Übertrag über dem Betrag ergibt 0" <| fun () ->
                let target = { need 100m (Months 1) with NeedsWholeAmount = Some false }
                Expect.equal (compute target november 0m 130m 0m) (computed 0m) "nicht negativ"
        ]

        testList "Monatlich" [
            testCase "NEED monatlich ohne Datum: Betrag − Zugewiesen, der Übertrag zählt nicht" <| fun () ->
                Expect.equal (compute (need 100m (Months 1)) november 30m 500m 0m) (computed 70m) "monatlich"

            testCase "NEED monatlich mit `NeedsWholeAmount = Some true` rechnet ebenso" <| fun () ->
                let target = { need 100m (Months 1) with NeedsWholeAmount = Some true }
                Expect.equal (compute target november 30m 500m 0m) (computed 70m) "Set aside another"

            testCase "MF: Betrag − Zugewiesen" <| fun () ->
                let target = { need 40m (Months 1) with Kind = MonthlyFunding }
                Expect.equal (compute target november 15m 0m 0m) (computed 25m) "MF"

            testCase "Zugewiesen über dem Betrag ergibt 0" <| fun () ->
                Expect.equal (compute (need 100m (Months 1)) november 120m 0m 0m) (computed 0m) "nicht negativ"
        ]

        testList "Wöchentlich" [
            testCase "Monat mit vier Vorkommen des Wochentags" <| fun () ->
                let target = { need 10m (Weeks 1) with Day = Some 1 }
                Expect.equal (compute target february 5m 0m 0m) (computed 35m) "4 × 10 − 5"

            testCase "Monat mit fünf Vorkommen des Wochentags" <| fun () ->
                let target = { need 10m (Weeks 1) with Day = Some 1 }
                Expect.equal (compute target november 5m 0m 0m) (computed 45m) "5 × 10 − 5"

            testCase "0 ist Sonntag" <| fun () ->
                let sunday = { need 10m (Weeks 1) with Day = Some 0 }
                let tuesday = { need 10m (Weeks 1) with Day = Some 2 }
                Expect.equal (compute sunday november 0m 0m 0m) (computed 50m) "fünf Sonntage"
                Expect.equal (compute tuesday november 0m 0m 0m) (computed 40m) "vier Dienstage"
        ]

        testList "Zielstand" [
            testCase "TB: Betrag − Verfügbar, Verfügbar = Übertrag + Zugewiesen + Aktivität" <| fun () ->
                let target = { need 500m NoRhythm with Kind = TargetBalance }
                Expect.equal (compute target november 50m 200m -30m) (computed 280m) "500 − 220"

            testCase "TB mit Verfügbar über dem Betrag ergibt 0" <| fun () ->
                let target = { need 500m NoRhythm with Kind = TargetBalance }
                Expect.equal (compute target november 0m 600m 0m) (computed 0m) "nicht negativ"
        ]

        testList "Nicht gerechnet" [
            for name, target in
                [ "DEBT", { need 100m (Months 1) with Kind = Debt }
                  "OtherKind", { need 100m (Months 1) with Kind = OtherKind "XYZ" }
                  "NEED wöchentlich ohne Wochentag", need 10m (Weeks 1)
                  "NEED alle 2 Wochen", { need 10m (Weeks 2) with Day = Some 1 }
                  "NEED alle 3 Monate ohne Datum", need 90m (Months 3)
                  "NEED jährlich ohne Datum", need 120m (Years 1)
                  "NEED ohne Rhythmus", need 50m NoRhythm ] do
                testCase $"{name} liefert keinen Betrag, aber einen Grund" <| fun () ->
                    match compute target november 0m 0m 0m with
                    | NotComputed reason -> Expect.isNotEmpty reason "Grund"
                    | Computed amount -> failtest $"kein Betrag erwartet, kam {amount}"
        ]

        testCase "Eine Pause spielt für die Funktion keine Rolle" <| fun () ->
            // Die Funktion kennt keine Pause: dieselben Eingaben, dasselbe Ergebnis.
            Expect.equal (compute (need 100m (Months 1)) november 0m 0m 0m) (computed 100m) "ohne Pause gerechnet"
    ]

// ============================================
// Vergleich
// ============================================

let private category (name: string) (target: Target option) (assigned: decimal) (ynab: decimal option) : BudgetCategory =
    { Id = Guid.NewGuid()
      Name = name
      Assigned = eur assigned
      Activity = eur 0m
      Available = eur assigned
      Underfunded = ynab |> Option.map eur
      Target = target
      Pause = None }

let private monthWith (categories: BudgetCategory list) : BudgetMonth =
    { Month = november
      ToBeBudgeted = eur 0m
      Groups = [ { Name = "Alltag"; Categories = categories } ] }

let private rowOf (rows: UnderfundedComparison list) (id: Guid) =
    rows |> List.find (fun r -> r.CategoryId = id)

let private comparisonTests =
    testList "Vergleich" [
        testCase "Gleich, Abweichung mit Differenz BB − YNAB und DEBT als nicht gerechnet; ohne Target fehlt" <| fun () ->
            let equal = category "Gleich" (Some (need 100m (Months 1))) 40m (Some 60m)
            let deviating = category "Abweichend" (Some (need 100m (Months 1))) 40m (Some 50m)
            let debt = category "Schulden" (Some { need 100m (Months 1) with Kind = Debt }) 0m (Some 100m)
            let withoutTarget = category "Ohne" None 0m None
            let rows = compareUnderfunded (monthWith [ equal; deviating; debt; withoutTarget ])

            Expect.equal (rows |> List.map (fun r -> r.CategoryId)) [ equal.Id; deviating.Id; debt.Id ] "ohne Target fehlt"
            let e = rowOf rows equal.Id
            Expect.equal e.Outcome Equal "gleich"
            Expect.equal e.Difference (Some (eur 0m)) "Differenz 0"
            let d = rowOf rows deviating.Id
            Expect.equal d.Outcome Deviation "abweichend"
            Expect.equal (d.Bb, d.Ynab, d.Difference) (Some (eur 60m), Some (eur 50m), Some (eur 10m)) "beide Werte und BB − YNAB"
            Expect.equal d.Rule (Some MonthlyRule) "Formel"
            let n = rowOf rows debt.Id
            Expect.equal n.Outcome NotCompared "DEBT nicht gerechnet"
            Expect.equal (n.Bb, n.Difference) (None, None) "keine Abweichung 0"
            Expect.isSome n.Reason "Grund"
            Expect.equal n.Kind Debt "Art"

        testCase "Jeder Fall der Zeile \"Nicht gerechnet\" erscheint als nicht gerechnet mit Grund" <| fun () ->
            let targets =
                [ { need 100m (Months 1) with Kind = OtherKind "XYZ" }
                  need 10m (Weeks 1)
                  { need 10m (Weeks 2) with Day = Some 1 }
                  need 90m (Months 3)
                  need 120m (Years 1)
                  need 50m NoRhythm ]
            let rows = compareUnderfunded (monthWith (targets |> List.map (fun t -> category "X" (Some t) 0m (Some 0m))))
            for row in rows do
                Expect.equal row.Outcome NotCompared "nicht gerechnet"
                Expect.isNone row.Difference "keine Differenz"
                Expect.isSome row.Reason "Grund"
            Expect.equal rows.Length targets.Length "alle da"

        testCase "Ausgeblendete Kategorien fehlen" <| fun () ->
            let visible = Guid.NewGuid()
            let hidden = Guid.NewGuid()
            let raw (id: Guid) (isHidden: bool) : YnabMonthCategory =
                { Id = id
                  Name = "Kat"
                  GroupName = "Alltag"
                  Assigned = eur 0m
                  Activity = eur 0m
                  Available = eur 0m
                  Underfunded = Some (eur 100m)
                  Target = Some (need 100m (Months 1))
                  Snoozed = false
                  Hidden = isHidden
                  Deleted = false
                  Internal = false }
            let month = toBudgetMonth Set.empty { Month = november; ToBeBudgeted = eur 0m; Categories = [ raw visible false; raw hidden true ] }
            Expect.equal (compareUnderfunded month |> List.map (fun r -> r.CategoryId)) [ visible ] "nur sichtbar"

        testCase "Eine pausierte Kategorie wird verglichen und trägt das Kennzeichen pausiert" <| fun () ->
            let paused = { category "Pausiert" (Some (need 100m (Months 1))) 0m (Some 100m) with Pause = Some PausedInBb }
            let active = category "Aktiv" (Some (need 100m (Months 1))) 0m (Some 100m)
            let rows = compareUnderfunded (monthWith [ paused; active ])
            let p = rowOf rows paused.Id
            Expect.equal p.Outcome Equal "verglichen"
            Expect.isTrue p.Paused "pausiert"
            Expect.isFalse (rowOf rows active.Id).Paused "nicht pausiert"

        testCase "Target, aber YNAB ohne Fehlbetrag: nicht gerechnet mit Grund \"YNAB ohne Wert\"" <| fun () ->
            let c = category "Ohne Wert" (Some (need 100m (Months 1))) 0m None
            let row = rowOf (compareUnderfunded (monthWith [ c ])) c.Id
            Expect.equal row.Outcome NotCompared "nicht gerechnet"
            Expect.equal row.Reason (Some YnabWithoutValueText) "Grund"
            Expect.equal row.Bb (Some (eur 100m)) "BBs Wert bleibt"

        testCase "Die Log-Zeile nennt Monat und die drei Anzahlen, keine Beträge" <| fun () ->
            let rows =
                compareUnderfunded (
                    monthWith
                        [ category "A" (Some (need 100m (Months 1))) 0m (Some 100m)
                          category "B" (Some (need 100m (Months 1))) 0m (Some 90m)
                          category "C" (Some { need 100m (Months 1) with Kind = Debt }) 0m (Some 0m) ])
            Expect.equal
                (comparisonSummary november rows)
                "Parallelrechnung Fehlbetrag 2026-11-01: 1 gleich, 1 abweichend, 1 nicht gerechnet"
                "Log-Zeile"
    ]

// ============================================
// Einbindung in budgetMonth und Ablage
// ============================================

let private ynabMonth (monthKey: string) : YnabMonth =
    let raw (name: string) (under: decimal) : YnabMonthCategory =
        { Id = Guid.NewGuid()
          Name = name
          GroupName = "Alltag"
          Assigned = eur 20m
          Activity = eur 0m
          Available = eur 20m
          Underfunded = Some (eur under)
          Target = Some (need 100m (Months 1))
          Snoozed = false
          Hidden = false
          Deleted = false
          Internal = false }
    { Month = monthKey; ToBeBudgeted = eur 500m; Categories = [ raw "Eins" 80m; raw "Zwei" 70m ] }

let private portFor (month: YnabMonth) : CommandHandlers.YnabPort =
    { FetchMonth = fun _ -> async { return Ok month }
      SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben erwartet" }
      SetTarget = fun _ _ -> async { return failwith "kein Schreiben erwartet" } }

let private handlerTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    testList "Einbindung in budgetMonth" [
        testCase "Die Speicherfunktion bekommt die Vergleichszeilen des Monats, die Antwort bleibt gleich" <| fun () ->
            let monthKey = "2031-03-01"
            let raw = ynabMonth monthKey
            let port = portFor raw
            let saved = ResizeArray<string * UnderfundedComparison list>()
            let save month rows = async { saved.Add((month, rows)) }

            let withComparison = QueryHandlers.budgetMonthWith save port monthKey |> run
            let without = QueryHandlers.budgetMonthOf port.FetchMonth monthKey |> run

            Expect.equal withComparison without "Antwort an den Client wie ohne Vergleich"
            match withComparison with
            | Ok month ->
                Expect.equal (List.ofSeq saved) [ monthKey, compareUnderfunded month ] "Zeilen dieses Monats"
                Expect.equal
                    (saved[0] |> snd |> List.map (fun r -> r.Outcome))
                    [ Equal; Deviation ]
                    "ein Treffer, eine Abweichung"
            | Error e -> failtest $"Monat erwartet: {e}"

        testCase "Eine Speicherfunktion, die wirft, lässt das Lesen nicht scheitern" <| fun () ->
            let monthKey = "2031-04-01"
            let port = portFor (ynabMonth monthKey)
            let save _ _ : Async<unit> = async { return failwith "Datenbank weg" }

            let result = QueryHandlers.budgetMonthWith save port monthKey |> run
            let without = QueryHandlers.budgetMonthOf port.FetchMonth monthKey |> run

            Expect.isOk result "Ok trotz Fehler"
            Expect.equal result without "derselbe Monat"
    ]

let private storedRows (month: string) : (string * string * string * Nullable<int64> * Nullable<int64> * int64) list =
    use conn = global.Persistence.getConnection ()
    Dapper.SqlMapper.Query<string * string * string * Nullable<int64> * Nullable<int64> * int64>(
        conn,
        "SELECT category_name AS item1, kind AS item2, outcome AS item3, bb_milliunits AS item4, \
         ynab_milliunits AS item5, paused AS item6 \
         FROM underfunded_comparisons WHERE month = @month ORDER BY category_name",
        {| month = month |})
    |> Seq.toList

let private comparisonRow (month: string) (name: string) (outcome: ComparisonOutcome) : UnderfundedComparison =
    { Month = month
      CategoryId = Guid.NewGuid()
      CategoryName = name
      Kind = Need
      Rule = Some MonthlyRule
      Outcome = outcome
      Bb = Some (eur 12.34m)
      Ynab = (if outcome = NotCompared then None else Some (eur 10m))
      Difference = None
      Reason = (if outcome = NotCompared then Some YnabWithoutValueText else None)
      Paused = false }

let private persistenceTests =
    testSequenced <| testList "Ablage in underfunded_comparisons" [
        testCase "Ein zweites Speichern ersetzt die Zeilen des Monats, andere Monate bleiben" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            let save = Budgeting.Persistence.saveUnderfundedComparisonsWith global.Persistence.getConnection
            let at = DateTime(2031, 5, 2, 8, 0, 0, DateTimeKind.Utc)
            let month = "2031-05-01"
            let other = "2031-06-01"
            let a = comparisonRow month "A" Deviation
            let b = comparisonRow month "B" NotCompared
            let o = comparisonRow other "O" Equal

            save at other [ o ] |> run
            save at month [ a; b ] |> run
            Expect.equal
                (storedRows month)
                [ "A", "NEED", "Abweichung", Nullable 12340L, Nullable 10000L, 0L
                  "B", "NEED", "Nicht gerechnet", Nullable 12340L, Nullable(), 0L ]
                "erstes Speichern"

            save at month [ { a with Outcome = Equal; Paused = true } ] |> run
            Expect.equal (storedRows month) [ "A", "NEED", "Gleich", Nullable 12340L, Nullable 10000L, 1L ] "B ist weg, A ersetzt"
            Expect.equal (storedRows other) [ "O", "NEED", "Gleich", Nullable 12340L, Nullable 10000L, 0L ] "anderer Monat unberührt"

        testCase "budgetMonth hält die Abweichungsliste des gelesenen Monats in der Datenbank fest" <| fun () ->
            // Belegt den Live-Anschluss: `budgetMonth` gibt `saveUnderfundedComparisons` an `budgetMonthWith`.
            Budgeting.Persistence.initializeBudgetingSchema ()
            let monthKey = "2031-07-01"
            let result = QueryHandlers.budgetMonth (portFor (ynabMonth monthKey)) monthKey |> run
            Expect.isOk result "Monat gelesen"
            Expect.equal
                (storedRows monthKey)
                [ "Eins", "NEED", "Gleich", Nullable 80000L, Nullable 80000L, 0L
                  "Zwei", "NEED", "Abweichung", Nullable 80000L, Nullable 70000L, 0L ]
                "zwei Zeilen"
    ]

[<Tests>]
let tests =
    testList
        "Fehlbetrag in BB, Parallelrechnung"
        [ ruleTests; comparisonTests; handlerTests; persistenceTests ]
