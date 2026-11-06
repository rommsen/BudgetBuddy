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
    { Goal = eur goal; Rhythm = rhythm; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }

/// Fehlbetrag mit Zugewiesen, Übertrag und Aktivität im gelesenen Monat `month`, ohne Ausgaben in der laufenden
/// Periode: jeder frühere Monat, den die Rechnung braucht, hat Aktivität 0.
let private compute (target: Target) (month: string) (assigned: decimal) (carryover: decimal) (activity: decimal) =
    let quiet = periodMonthsOf target month |> List.map (fun m -> m, eur 0m) |> Map.ofList
    underfunded target month (eur assigned) (eur carryover) (eur activity) quiet

/// Fehlbetrag mit der Aktivität früherer Monate `earlier` ("YYYY-MM-01", Betrag).
let private computeWith (target: Target) (month: string) (assigned: decimal) (carryover: decimal) (activity: decimal) (earlier: (string * decimal) list) =
    underfunded target month (eur assigned) (eur carryover) (eur activity) (earlier |> List.map (fun (m, a) -> m, eur a) |> Map.ofList)

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

            testCase "NEED mit Datum und needs_whole_amount=false rechnet nach Fälligkeit, nicht nach Auffüllen" <| fun () ->
                // Die Fälligkeit hat Vorrang vor "Auffüllen bis"; Auffüllen ergäbe hier 120 − (30 + 10) = 80.
                let target = { need 120m (Years 1) with Date = Some "2027-01-15"; NeedsWholeAmount = Some false }
                Expect.equal (compute target november 10m 30m 0m) (computed 20m) "(120 − 30) / 3 − 10"

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

            testCase "Fälligkeit vor dem gelesenen Monat ohne Rhythmus zählt als ein Monat" <| fun () ->
                let target = { need 80m NoRhythm with Kind = TargetBalanceByDate; Date = Some "2026-08-15" }
                Expect.equal (compute target november 5m 20m 0m) (computed 55m) "ein Monat"

            // Regressionstest budgeting-r4kqm: Die Parallelrechnung in Prod (Monate 2026-05 bis 2026-12,
            // gelesen am 2026-11-06) zeigte nur Abweichungen bei "Mit Fälligkeit". Bei wiederkehrenden
            // Zielen liegt das gespeicherte Datum in der Vergangenheit, BB rechnete mit 1 verbleibenden
            // Monat und verlangte den ganzen Rest auf einmal. YNAB rechnet bis zur nächsten Fälligkeit.
            testCase "Datum im Vormonat und Rhythmus jährlich: gerechnet bis zur nächsten Fälligkeit in 11 Monaten" <| fun () ->
                let target = { need 120m (Years 1) with Date = Some "2026-10-15" }
                // Nächste Fälligkeit 2027-10: November bis Oktober sind 12 Monate. 120 / 12 = 10.
                Expect.equal (compute target november 0m 0m 0m) (computed 10m) "zwölf Monate bis 2027-10"

            testCase "Datum im Vormonat und Rhythmus alle 12 Monate: gerechnet bis zur nächsten Fälligkeit" <| fun () ->
                let target = { need 120m (Months 12) with Date = Some "2026-10-15" }
                Expect.equal (compute target november 0m 0m 0m) (computed 10m) "zwölf Monate bis 2027-10"

            // Review-Fund eh8xe (QA D9): die Fälligkeit liegt mehrere Rhythmus-Schritte zurück.
            testCase "Datum zwei Jahre zurück und Rhythmus jährlich: gerechnet bis zur nächsten Fälligkeit in 12 Monaten" <| fun () ->
                let target = { need 120m (Years 1) with Date = Some "2024-10-15" }
                // 2024-10 um zwei Jahre ist 2026-10, vor November; um drei Jahre 2027-10: 12 Monate. 120 / 12 = 10.
                Expect.equal (compute target november 0m 0m 0m) (computed 10m) "zwölf Monate bis 2027-10"

            testCase "Wie im Befund: monatlicher Anteil zugewiesen ergibt 0, im Folgemonat ohne Zuweisung den Anteil" <| fun () ->
                // Erfundene Zahlen nach dem Muster des Befunds: 89,88 je Jahr, fällig im Februar, Datum 2026-02.
                let target = { need 89.88m (Years 1) with Date = Some "2026-02-28" }
                // November bis Februar: 4 Monate. (89,88 − 59,92) / 4 = 7,49, zugewiesen 7,49.
                Expect.equal (compute target november 7.49m 59.92m 0m) (computed 0m) "gedeckt wie YNAB"
                // Dezember bis Februar: 3 Monate. (89,88 − 67,41) / 3 = 7,49, nichts zugewiesen.
                Expect.equal (compute target "2026-12-01" 0m 67.41m 0m) (computed 7.49m) "monatlicher Anteil"

            testCase "Fälligkeit mit Rhythmus im gelesenen Monat bleibt im gelesenen Monat" <| fun () ->
                let target = { need 80m (Years 1) with Date = Some "2025-11-30" }
                // 2025-11 um ein Jahr verschoben ist 2026-11, der gelesene Monat: ein Monat.
                Expect.equal (compute target november 5m 20m 0m) (computed 55m) "ein Monat"

            testCase "Rhythmus in Wochen verschiebt das Datum bis in den gelesenen Monat" <| fun () ->
                let target = { need 80m (Weeks 2) with Date = Some "2026-10-20" }
                // 2026-10-20 plus zwei Wochen ist 2026-11-03, im gelesenen Monat: ein Monat.
                Expect.equal (compute target november 5m 20m 0m) (computed 55m) "ein Monat"

            testCase "Rhythmus in Wochen über den gelesenen Monat hinaus ergibt die Fälligkeit im Folgemonat" <| fun () ->
                let target = { need 80m (Weeks 6) with Date = Some "2026-10-30" }
                // 2026-10-30 plus sechs Wochen ist 2026-12-11: November und Dezember. (80 − 20) / 2 − 5 = 25.
                Expect.equal (compute target november 5m 20m 0m) (computed 25m) "zwei Monate"

            testCase "Datum in der Zukunft mit Rhythmus bleibt unverändert" <| fun () ->
                let target = { need 120m (Months 1) with Date = Some "2027-01-15" }
                Expect.equal (compute target november 10m 30m 0m) (computed 20m) "drei Monate"

            testCase "Zugewiesen über dem Betrag ergibt 0" <| fun () ->
                let target = { need 50m (Months 1) with Date = Some "2026-11-30" }
                Expect.equal (compute target november 70m 0m 0m) (computed 0m) "nicht negativ"

            testCase "TBD ohne Datum wird nicht gerechnet" <| fun () ->
                let target = { need 60m NoRhythm with Kind = TargetBalanceByDate }
                Expect.isTrue (isNotComputed (compute target november 0m 0m 0m)) "ohne Datum"
        ]

        // Regressionstests budgeting-b9869: Der zweite Prod-Vergleich (Monate 2025-12 bis 2026-11, gelesen
        // 2026-11-06) zeigte 8 Abweichungen, alle "Mit Fälligkeit" mit jährlichem Rhythmus: BB verlangte Geld,
        // YNAB 0. YNAB zählt Geld, das in der laufenden Periode schon ausgegeben wurde, als gedeckt
        // (`goal_overall_funded`), BB zählte nur den Übertrag. Beträge erfunden, Verhältnisse wie im Befund.
        testList "Ausgaben der laufenden Periode" [
            testCase "Auffüllen bis mit Fälligkeit: Übertrag plus Ausgaben der Periode decken den Monatsanteil" <| fun () ->
                // Wie A2 im Befund.
                // Fällig 2027-03, jährlich: Periode ab 2026-03 (vorige Fälligkeit, budgeting-pf8n7), November bis März 5 Monate.
                let target = { need 60m (Years 1) with Date = Some "2026-03-15"; NeedsWholeAmount = Some false }
                let earlier = [ for m in 3 .. 10 -> $"2026-%02d{m}-01", (if m = 6 then -10m else 0m) ]
                // (60 − (25 + 10)) / 5 = 5, zugewiesen 5. Ohne die Ausgaben: (60 − 25) / 5 − 5 = 2.
                Expect.equal (computeWith target november 5m 25m 0m earlier) (computed 0m) "gedeckt wie YNAB"

            testCase "Ausgaben über dem Ziel decken alles, ohne Übertrag und ohne Zuweisung" <| fun () ->
                // Wie A5 im Befund.
                // Fällig 2027-01, jährlich: Periode ab 2026-01 (vorige Fälligkeit), November bis Januar 3 Monate.
                let target = { need 240m (Years 1) with Date = Some "2026-01-20" }
                let earlier = [ for m in 1 .. 10 -> $"2026-%02d{m}-01", (if m = 3 then -130m elif m = 8 then -120m else 0m) ]
                // Ohne die Ausgaben: 240 / 3 = 80.
                Expect.equal (computeWith target november 0m 0m 0m earlier) (computed 0m) "gedeckt wie YNAB"

            testCase "Fälligkeit im gelesenen Monat, fast alles schon ausgegeben" <| fun () ->
                // Wie A7 im Befund.
                // Fällig 2026-11, jährlich: Periode ab 2025-11 (vorige Fälligkeit), 1 Monat.
                let target = { need 300m (Years 1) with Date = Some "2025-11-20" }
                let earlier =
                    [ "2025-11-01", 0m; "2025-12-01", 0m ] @ [ for m in 1 .. 10 -> $"2026-%02d{m}-01", (if m = 9 then -297.5m else 0m) ]
                // (300 − (0 + 297,50)) / 1 − 2,50 = 0. Ohne die Ausgaben: 300 − 2,50 = 297,50.
                Expect.equal (computeWith target november 2.5m 0m 0m earlier) (computed 0m) "gedeckt wie YNAB"

            // budgeting-pf8n7: seit dem dritten Prod-Vergleich beginnt die Periode im Monat der vorigen Fälligkeit, nicht danach.
            testCase "Die Periode beginnt im Monat der vorigen Fälligkeit und endet vor dem gelesenen Monat" <| fun () ->
                Expect.equal
                    (periodMonthsBefore (Years 1) None november "2026-03-15")
                    [ for m in 3 .. 10 -> $"2026-%02d{m}-01" ]
                    "März bis Oktober"
                Expect.equal (periodMonthsBefore (Years 1) None november "2025-11-20") ([ "2025-11-01"; "2025-12-01" ] @ [ for m in 1 .. 10 -> $"2026-%02d{m}-01" ]) "Fälligkeit im gelesenen Monat"
                Expect.equal (periodMonthsBefore (Months 3) None november "2026-09-30") [ "2026-09-01"; "2026-10-01" ] "vierteljährlich, fällig im Dezember"
                Expect.equal (periodMonthsBefore (Years 1) None november "2026-10-15") [ "2026-10-01" ] "vorige Fälligkeit im Vormonat"
                // Wöchentlich ab 2026-11-10, erste Fälligkeit: die rechnerisch vorige am 2026-11-03 liegt im gelesenen Monat.
                Expect.equal (periodMonthsBefore (Weeks 1) None november "2026-11-10") [] "vorige Fälligkeit im gelesenen Monat"
                Expect.equal (periodMonthsBefore (Weeks 6) None november "2026-10-30") [ "2026-10-01" ] "Wochen, vorige Fälligkeit im Oktober"
                Expect.equal (periodMonthsBefore NoRhythm None november "2026-03-15") [] "ohne Rhythmus"
                // Alle 8 Wochen ab 2026-07-16: 2026-09-10, dann 2026-11-05 im gelesenen Monat. Die vorige
                // Fälligkeit liegt im September, die Periode reicht von September bis Oktober.
                Expect.equal (periodMonthsBefore (Weeks 8) None november "2026-07-16") [ "2026-09-01"; "2026-10-01" ] "Wochen, vorige Fälligkeit im September"
                Expect.equal (periodMonthsBefore (Years 1) None november "kein Datum") [] "unlesbares Datum"

            testCase "Ausgaben im gelesenen Monat zählen nicht als gedeckt, gerechnet wird mit dem Übertrag" <| fun () ->
                // Bewusste Regel aus budgeting-b9869: die Aktivität des gelesenen Monats steckt nicht im Übertrag,
                // als Ausgabe gezählt würde sie den Monatsanteil senken. Verfügbar wäre hier 25 − 10 = 15.
                let target = { need 60m (Years 1) with Date = Some "2026-03-15" }
                let earlier = [ for m in 3 .. 10 -> $"2026-%02d{m}-01", (if m = 6 then -10m else 0m) ]
                let withActivity = computeWith target november 0m 25m -10m earlier
                // (60 − (25 + 10)) / 5 = 5. Mit der Aktivität als Ausgabe: (60 − 45) / 5 = 3; mit Verfügbar: 7.
                Expect.equal withActivity (computed 5m) "nur Übertrag und Ausgaben der Periode"
                Expect.equal withActivity (computeWith target november 0m 25m 0m earlier) "wie ohne Aktivität im gelesenen Monat"

            testCase "Ausgaben im Monat vor der vorigen Fälligkeit zählen nicht" <| fun () ->
                let target = { need 60m (Years 1) with Date = Some "2026-03-15" }
                // März 2026 ist die vorige Fälligkeit und beginnt die Periode (budgeting-pf8n7), der Februar gehört
                // zur vorigen Periode.
                let earlier = ("2026-02-01", -40m) :: [ for m in 3 .. 10 -> $"2026-%02d{m}-01", 0m ]
                Expect.equal (computeWith target november 0m 25m 0m earlier) (computed 7m) "(60 − 25) / 5"

            testCase "Zuflüsse senken die Ausgaben nicht unter 0, verrechnen sich aber mit Ausgaben" <| fun () ->
                let target = { need 60m (Years 1) with Date = Some "2026-03-15" }
                let monthsWith (values: Map<int, decimal>) =
                    [ for m in 3 .. 10 -> $"2026-%02d{m}-01", values |> Map.tryFind m |> Option.defaultValue 0m ]
                // Netto ein Zufluss von 20: Ausgaben 0, nicht −20.
                Expect.equal
                    (computeWith target november 0m 25m 0m (monthsWith (Map.ofList [ 5, 30m; 7, -10m ])))
                    (computed 7m)
                    "(60 − 25) / 5"
                // Netto Ausgaben 20: (60 − 45) / 5 = 3.
                Expect.equal
                    (computeWith target november 0m 25m 0m (monthsWith (Map.ofList [ 5, -30m; 7, 10m ])))
                    (computed 3m)
                    "(60 − (25 + 20)) / 5"

            testCase "Ohne Rhythmus zählen Ausgaben früherer Monate nicht" <| fun () ->
                let target = { need 60m NoRhythm with Date = Some "2027-03-15" }
                let earlier = [ for m in 1 .. 10 -> $"2026-%02d{m}-01", -50m ]
                Expect.equal (computeWith target november 0m 25m 0m earlier) (computed 7m) "wie bisher"
                Expect.equal (computeWith target november 0m 25m 0m []) (computed 7m) "braucht keine Aktivität"

            testCase "Fehlt die Aktivität eines Monats der Periode, ist der Fehlbetrag nicht gerechnet" <| fun () ->
                let target = { need 60m (Years 1) with Date = Some "2026-03-15" }
                let earlier = [ for m in 3 .. 10 do if m <> 8 then yield $"2026-%02d{m}-01", -10m ]
                Expect.equal (computeWith target november 0m 25m 0m earlier) (NotComputed PeriodActivityMissingText) "kein Betrag"
        ]

        // Regressionstests budgeting-pf8n7: Der dritte Prod-Vergleich (2026-11-06) prüfte alle 32 NEED-Targets mit
        // Fälligkeit gegen YNABs `goal_under_funded`. Mit dem Periodenbeginn im Monat nach der vorigen Fälligkeit
        // waren 26 gleich, mit dem Beginn im Monat der vorigen Fälligkeit 30. Bei einem Ziel, dessen erste
        // Fälligkeit noch aussteht (F3), zählt YNAB ab dem Anlegemonat. Beträge erfunden, Verhältnisse wie im Befund.
        testList "Periode ab voriger Fälligkeit und Anlegemonat" [
            testCase "GIVEN eine Ausgabe im Monat der vorigen Fälligkeit WHEN gerechnet wird THEN zählt sie als gedeckt" <| fun () ->
                // Wie F2 im Befund. Fällig 2027-04, jährlich: vorige Fälligkeit 2026-04, November bis April 6 Monate.
                let target = { need 120m (Years 1) with Date = Some "2026-04-10" }
                let earlier = [ for m in 4 .. 10 -> $"2026-%02d{m}-01", (if m = 4 then -60m else 0m) ]
                // (120 − (30 + 60)) / 6 − 5 = 0. Ohne den April: 120 − 30 = 90, 90 / 6 − 5 = 10.
                Expect.equal (computeWith target november 5m 30m 0m earlier) (computed 0m) "gedeckt wie YNAB"

            testCase "GIVEN ein Ziel vor seiner ersten Fälligkeit WHEN gerechnet wird THEN beginnt die Periode im Anlegemonat" <| fun () ->
                // Wie F3 im Befund. Angelegt im Mai 2026, erste Fälligkeit August 2027 (jährlich). November bis August
                // 10 Monate, die Periode reicht von Mai bis Oktober 2026.
                let target = { need 150m (Years 1) with Date = Some "2027-08-15"; CreatedIn = Some "2026-05-01" }
                let earlier = [ for m in 5 .. 10 -> $"2026-%02d{m}-01", (if m = 5 then -100m else 0m) ]
                // (150 − 100) / 10 − 5 = 0. Ab der rechnerisch vorigen Fälligkeit (August): 150 / 10 − 5 = 10.
                Expect.equal (computeWith target november 5m 0m 0m earlier) (computed 0m) "gedeckt wie YNAB"

            testCase "GIVEN ein Ziel vor seiner ersten Fälligkeit ohne Anlegemonat WHEN gerechnet wird THEN gilt ab voriger Fälligkeit" <| fun () ->
                let target = { need 150m (Years 1) with Date = Some "2027-08-15" }
                let earlier = [ for m in 5 .. 10 -> $"2026-%02d{m}-01", (if m = 5 then -100m elif m = 8 then -20m else 0m) ]
                // Nur August zählt: (150 − 20) / 10 − 5 = 8.
                Expect.equal (computeWith target november 5m 0m 0m earlier) (computed 8m) "Mai zählt nicht"
                Expect.equal (periodMonthsBefore (Years 1) None november "2027-08-15") [ "2026-08-01"; "2026-09-01"; "2026-10-01" ] "August bis Oktober"

            testCase "GIVEN ein verschobenes Datum WHEN gerechnet wird THEN gilt ab voriger Fälligkeit, der Anlegemonat zählt nicht" <| fun () ->
                // Gespeichert 2026-03-15, im November um ein Jahr verschoben: die erste Periode ist vorbei.
                let target = { need 60m (Years 1) with Date = Some "2026-03-15"; CreatedIn = Some "2025-01-01" }
                let earlier = [ for m in 3 .. 10 -> $"2026-%02d{m}-01", (if m = 3 then -10m else 0m) ]
                // (60 − (25 + 10)) / 5 = 5. Die Monate vor März 2026 braucht die Rechnung nicht.
                Expect.equal (computeWith target november 0m 25m 0m earlier) (computed 5m) "ab März"
                Expect.equal
                    (periodMonthsBefore (Years 1) (Some "2025-01-01") november "2026-03-15")
                    [ for m in 3 .. 10 -> $"2026-%02d{m}-01" ]
                    "März bis Oktober"

            testCase "Die erste Periode beginnt im Anlegemonat, auch an den Grenzen" <| fun () ->
                Expect.equal (periodMonthsBefore (Years 1) (Some "2026-10-01") november "2027-08-15") [ "2026-10-01" ] "angelegt im Vormonat"
                Expect.equal (periodMonthsBefore (Years 1) (Some "2026-11-01") november "2027-08-15") [] "angelegt im gelesenen Monat"
                Expect.equal (periodMonthsBefore (Years 1) (Some "2026-12-01") november "2027-08-15") [] "angelegt nach dem gelesenen Monat"
                Expect.equal (periodMonthsBefore (Years 1) (Some "kein Monat") november "2027-08-15") [ "2026-08-01"; "2026-09-01"; "2026-10-01" ] "unlesbarer Anlegemonat wie ohne"
                // Fälligkeit im gelesenen Monat, nicht verschoben: Anlegemonat statt vorige Fälligkeit.
                Expect.equal (periodMonthsBefore (Years 1) (Some "2026-07-01") november "2026-11-20") [ for m in 7 .. 10 -> $"2026-%02d{m}-01" ] "fällig im gelesenen Monat"

            testCase "Ohne Rhythmus zählen keine Ausgaben, auch mit Anlegemonat" <| fun () ->
                let target = { need 60m NoRhythm with Date = Some "2027-03-15"; CreatedIn = Some "2026-01-01" }
                Expect.equal (periodMonthsBefore NoRhythm (Some "2026-01-01") november "2027-03-15") [] "keine Monate"
                Expect.equal (computeWith target november 0m 25m 0m []) (computed 7m) "(60 − 25) / 5"
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
      Pause = None; OpenWriteBack = None }

/// Eine Kategorie mit Aktivität und Verfügbar; der Übertrag ist Verfügbar − Zugewiesen − Aktivität.
let private categoryWithFlow (target: Target) (assigned: decimal) (activity: decimal) (available: decimal) (ynab: decimal) =
    { category "Fluss" (Some target) assigned (Some ynab) with
        Activity = eur activity
        Available = eur available }

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
            let rows = compareUnderfunded (monthWith [ equal; deviating; debt; withoutTarget ]) Map.empty

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
            let rows = compareUnderfunded (monthWith (targets |> List.map (fun t -> category "X" (Some t) 0m (Some 0m)))) Map.empty
            for row in rows do
                Expect.equal row.Outcome NotCompared "nicht gerechnet"
                Expect.isNone row.Difference "keine Differenz"
                Expect.isSome row.Reason "Grund"
            Expect.equal rows.Length targets.Length "alle da"

        testCase "Der Vergleich rechnet mit dem Übertrag aus Verfügbar, Zugewiesen und Aktivität und mit dem gelesenen Monat" <| fun () ->
            // Übertrag = 50 − 20 − (−30) = 60; Auffüllen bis 100: 100 − (60 + 20) = 20.
            let refill = categoryWithFlow { need 100m (Months 1) with NeedsWholeAmount = Some false } 20m -30m 50m 20m
            // Fälligkeit Dezember, gelesen im November: zwei Monate, 60 / 2 = 30.
            let due = categoryWithFlow { need 60m (Months 1) with Date = Some "2026-12-31" } 0m 0m 0m 30m
            let rows = compareUnderfunded (monthWith [ refill; due ]) Map.empty
            Expect.equal (rowOf rows refill.Id).Bb (Some (eur 20m)) "Übertrag gerechnet"
            Expect.equal (rowOf rows refill.Id).Outcome Equal "gleich mit YNAB"
            Expect.equal (rowOf rows due.Id).Bb (Some (eur 30m)) "Monat aus dem BudgetMonth"
            Expect.equal (rowOf rows due.Id).Outcome Equal "gleich mit YNAB"

        testCase "Der Vergleich rechnet mit den Ausgaben der Periode je Kategorie, fehlt ein Monat mit Grund" <| fun () ->
            // budgeting-b9869. Fällig 2027-03, jährlich: Periode März bis Oktober (budgeting-pf8n7), November bis März 5 Monate.
            let target = { need 60m (Years 1) with Date = Some "2026-03-15" }
            let spent = categoryWithFlow target 5m 0m 30m 0m
            let missing = categoryWithFlow target 5m 0m 30m 0m
            let earlier =
                Map.ofList
                    [ spent.Id, Map.ofList [ for m in 3 .. 10 -> $"2026-%02d{m}-01", (if m = 6 then eur -10m else eur 0m) ]
                      missing.Id, Map.ofList [ for m in 3 .. 9 -> $"2026-%02d{m}-01", eur 0m ] ]
            let rows = compareUnderfunded (monthWith [ spent; missing ]) earlier
            Expect.equal (rowOf rows spent.Id).Outcome Equal "(60 − (25 + 10)) / 5 − 5 = 0 wie YNAB"
            let m = rowOf rows missing.Id
            Expect.equal m.Outcome NotCompared "nicht gerechnet"
            Expect.equal m.Reason (Some PeriodActivityMissingText) "Grund"
            Expect.isNone m.Bb "kein Betrag"

        testCase "Gebraucht werden die Periodenmonate aller Targets mit Fälligkeit, jeder Monat einmal" <| fun () ->
            // budgeting-b9869, Grenze nach budgeting-pf8n7. Jährlich fällig im März: März bis Oktober. Vierteljährlich
            // fällig im Dezember: September und Oktober.
            let annual = category "Jährlich" (Some { need 60m (Years 1) with Date = Some "2026-03-15" }) 0m (Some 0m)
            let quarterly = category "Vierteljährlich" (Some { need 30m (Months 3) with Date = Some "2026-09-30" }) 0m (Some 0m)
            let monthly = category "Monatlich" (Some (need 100m (Months 1))) 0m (Some 100m)
            let withoutTarget = category "Ohne" None 0m None
            Expect.equal
                (periodMonthsNeeded (monthWith [ annual; quarterly; monthly; withoutTarget ]))
                [ for m in 3 .. 10 -> $"2026-%02d{m}-01" ]
                "März bis Oktober, September und Oktober nur einmal"
            Expect.equal (periodMonthsNeeded (monthWith [ monthly; withoutTarget ])) [] "ohne Fälligkeit keine Monate"

        testCase "Eine pausierte Kategorie wird verglichen und trägt das Kennzeichen pausiert" <| fun () ->
            let paused = { category "Pausiert" (Some (need 100m (Months 1))) 0m (Some 100m) with Pause = Some PausedInBb }
            let active = category "Aktiv" (Some (need 100m (Months 1))) 0m (Some 100m)
            let rows = compareUnderfunded (monthWith [ paused; active ]) Map.empty
            let p = rowOf rows paused.Id
            Expect.equal p.Outcome Equal "verglichen"
            Expect.isTrue p.Paused "pausiert"
            Expect.isFalse (rowOf rows active.Id).Paused "nicht pausiert"

        testCase "Target, aber YNAB ohne Fehlbetrag: nicht gerechnet mit Grund \"YNAB ohne Wert\"" <| fun () ->
            let c = category "Ohne Wert" (Some (need 100m (Months 1))) 0m None
            let row = rowOf (compareUnderfunded (monthWith [ c ]) Map.empty) c.Id
            Expect.equal row.Outcome NotCompared "nicht gerechnet"
            Expect.equal row.Reason (Some YnabWithoutValueText) "Grund"
            Expect.equal row.Bb (Some (eur 100m)) "BBs Wert bleibt"

        testCase "Die Log-Zeile nennt Monat und die drei Anzahlen, keine Beträge" <| fun () ->
            let rows =
                compareUnderfunded (
                    monthWith
                        [ category "A" (Some (need 100m (Months 1))) 0m (Some 100m)
                          category "B" (Some (need 100m (Months 1))) 0m (Some 90m)
                          category "C" (Some { need 100m (Months 1) with Kind = Debt }) 0m (Some 0m) ]
                ) Map.empty
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
          Hidden = false
          Deleted = false
          Internal = false }
    { Month = monthKey; ToBeBudgeted = eur 500m; Categories = [ raw "Eins" 80m; raw "Zwei" 70m ] }

let private portFor (month: YnabMonth) : CommandHandlers.YnabPort =
    { FetchMonth = fun _ -> async { return Ok month }
      SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben erwartet" }
      FetchCategoryGoals = fun () -> async { return Ok (BudgetEventsFixture.goalsOf month) } }

/// Der Zeitpunkt des Lesens (UTC): stempelt Übernahme und Beobachtungen (budgeting-dgrp7).
let private readAt = DateTime(2031, 3, 2, 8, 0, 0, DateTimeKind.Utc)

/// Ein YNAB-Monat mit einer Kategorie "Jährlich" (budgeting-b9869): Ziel 60, jährlich fällig im März (das
/// gespeicherte Datum liegt weit zurück, `dueFrom` schiebt es). Gelesen im November: Periode März bis Oktober,
/// November bis März 5 Monate.
let private annualMonth (categoryId: Guid) (monthKey: string) (assigned: decimal) (activity: decimal) (available: decimal) (ynab: decimal) : YnabMonth =
    { Month = monthKey
      ToBeBudgeted = eur 0m
      Categories =
        [ { Id = categoryId
            Name = "Jährlich"
            GroupName = "Alltag"
            Assigned = eur assigned
            Activity = eur activity
            Available = eur available
            Underfunded = Some (eur ynab)
            Target = Some { need 60m (Years 1) with Date = Some "2000-03-15" }
            Hidden = false
            Deleted = false
            Internal = false } ] }

/// Ein Port, der jeden YNAB-Aufruf mitschreibt (Grenze: 200 je Stunde): `FetchMonth` mit dem Monat,
/// `FetchCategoryGoals` als "categories" mit den Goals des zuletzt gelesenen Monats (budgeting-eh8xe).
let private countingPort (monthOf: string -> YnabMonth) : CommandHandlers.YnabPort * ResizeArray<string> =
    let calls = ResizeArray<string>()
    let last = ref None
    { FetchMonth =
        fun key ->
            async {
                let month = monthOf key
                lock calls (fun () -> calls.Add key; last.Value <- Some month)
                return Ok month
            }
      SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben erwartet" }
      FetchCategoryGoals =
        fun () ->
            async {
                let month = lock calls (fun () -> calls.Add "categories"; last.Value)
                return Ok (month |> Option.map BudgetEventsFixture.goalsOf |> Option.defaultValue [])
            } },
    calls

let private handlerTests =
    // Das Schema braucht `budgetMonthWith` für die BB-Pausen und die Übernahme des Monats; das
    // Speichern der Abweichungsliste kommt hier nur als Funktionswert herein.
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    testList "Einbindung in budgetMonth" [
        testCase "Die Speicherfunktion bekommt die Vergleichszeilen des übernommenen Monats, die Antwort hängt nicht vom Speichern ab" <| fun () ->
            let monthKey = "2031-03-01"
            let raw = ynabMonth monthKey
            let port = portFor raw
            let saved = ResizeArray<string * UnderfundedComparison list>()
            let save month rows = async { saved.Add((month, rows)) }

            let withComparison = QueryHandlers.budgetMonthWith save global.Persistence.getConnection port readAt monthKey |> run
            let without = QueryHandlers.budgetMonthWith (fun _ _ -> async { return () }) global.Persistence.getConnection port readAt monthKey |> run

            Expect.equal withComparison without "Antwort an den Client wie ohne Vergleich"
            // Erwartet, unabhängig von der Antwort: der Monat mit BBs Soll gleich YNABs Stand, wie ihn die Übernahme
            // beim ersten Lesen hinterlässt.
            let takenOver =
                raw.Categories
                |> List.map (fun c -> c.Id, { Assigned = toMilliunits c.Assigned; Confirmed = toMilliunits c.Assigned; Open = None })
                |> Map.ofList
            let expected = compareUnderfunded (toBudgetMonthWith (Some takenOver) (BudgetEventsFixture.ynabTargets raw) Set.empty raw) Map.empty
            Expect.equal (List.ofSeq saved) [ monthKey, expected ] "Zeilen dieses Monats"
            Expect.equal (expected |> List.map (fun r -> r.Outcome)) [ Equal; Deviation ] "ein Treffer, eine Abweichung"

        testCase "Eine Speicherfunktion, die wirft, lässt das Lesen nicht scheitern" <| fun () ->
            let monthKey = "2031-04-01"
            let port = portFor (ynabMonth monthKey)
            let save _ _ : Async<unit> = async { return failwith "Datenbank weg" }

            let result = QueryHandlers.budgetMonthWith save global.Persistence.getConnection port readAt monthKey |> run
            let without = QueryHandlers.budgetMonthWith (fun _ _ -> async { return () }) global.Persistence.getConnection port readAt monthKey |> run

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

/// Die übrigen Spalten einer gespeicherten Zeile: Kategorie-Id, Formel, Grund, Zeitpunkt.
let private storedDetails (month: string) (name: string) : string * string * string * string =
    use conn = global.Persistence.getConnection ()
    Dapper.SqlMapper.Query<string * string * string * string>(
        conn,
        "SELECT category_id AS item1, rule AS item2, reason AS item3, compared_at AS item4 \
         FROM underfunded_comparisons WHERE month = @month AND category_name = @name",
        {| month = month; name = name |})
    |> Seq.exactlyOne

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
            let stamp = at.ToString("o", Globalization.CultureInfo.InvariantCulture)
            Expect.equal (storedDetails month "A") (string a.CategoryId, "Monatlich", null, stamp) "A: Id, Formel, kein Grund, Zeitpunkt"
            Expect.equal (storedDetails month "B") (string b.CategoryId, "Monatlich", YnabWithoutValueText, stamp) "B: mit Grund"

            save at month [ { a with Outcome = Equal; Paused = true } ] |> run
            Expect.equal (storedRows month) [ "A", "NEED", "Gleich", Nullable 12340L, Nullable 10000L, 1L ] "B ist weg, A ersetzt"
            Expect.equal (storedRows other) [ "O", "NEED", "Gleich", Nullable 12340L, Nullable 10000L, 0L ] "anderer Monat unberührt"

        testCase "Das Schema entfernt category_month_activity, auch wiederholt" <| fun () ->
            // budgeting-k5q2w: die Tabelle war abgeleiteter Zustand, die Ausgaben der Periode kommen aus dem Kassenbuch.
            // Auf einer eigenen Datenbank: in der geteilten sähen parallele Tests die Tabelle und sperrten mit (Review).
            use conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
            conn.Open()
            Dapper.SqlMapper.Execute(conn, "CREATE TABLE category_month_activity (month TEXT NOT NULL)") |> ignore
            Budgeting.Persistence.dropMonthActivityOn conn
            Budgeting.Persistence.dropMonthActivityOn conn
            let count =
                Dapper.SqlMapper.ExecuteScalar<int64>(
                    conn,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'category_month_activity'")
            Expect.equal count 0L "Tabelle entfernt"

        testCase "budgetMonth hält die Abweichungsliste des gelesenen Monats in der Datenbank fest" <| fun () ->
            // Belegt den Live-Anschluss: `budgetMonth` gibt `saveUnderfundedComparisons` an `budgetMonthWith`.
            Budgeting.Persistence.initializeBudgetingSchema ()
            let monthKey = "2031-07-01"
            let result = QueryHandlers.budgetMonth (portFor (ynabMonth monthKey)) readAt monthKey |> run
            Expect.isOk result "Monat gelesen"
            Expect.equal
                (storedRows monthKey)
                [ "Eins", "NEED", "Gleich", Nullable 80000L, Nullable 80000L, 0L
                  "Zwei", "NEED", "Abweichung", Nullable 80000L, Nullable 70000L, 0L ]
                "zwei Zeilen"
    ]

// ============================================
// Ausgaben der Periode aus dem Kassenbuch (budgeting-k5q2w)
// ============================================

let private utc (y: int) (m: int) (d: int) = DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc)

/// Eine Buchung im Kassenbuch auf `categoryId`, wie sie der Spiegel aus YNAB übernimmt.
let private booked (categoryId: Guid) (date: DateTime) (amount: decimal) : unit =
    let entryId = Ledger.Events.newEntryId ()
    let payload: Ledger.Events.EntryObservedInYnabPayload =
        { AccountId = Ledger.Events.LedgerAccountId (Guid.NewGuid())
          YnabTransactionId = $"ynab-k5q2w-{Guid.NewGuid()}"
          ImportId = None
          Date = date
          Amount = eur amount
          Payee = Some "Empfänger K5q2w"
          Memo = None
          Category = Some { CategoryId = YnabCategoryId categoryId; CategoryName = "Jährlich" }
          Splits = []
          Cleared = Ledger.Events.Cleared
          TransferAccountId = None
          TransferYnabTransactionId = None
          Approved = true
          FlagColor = None
          FlagName = None }
    Ledger.Persistence.appendAndApply
        [ { EntryId = entryId
            OccurredAt = utc 2031 3 2
            Actor = Ledger.Events.YnabMirror
            Body = Ledger.Events.EntryObservedInYnab payload } ]
    |> run

let private initializeWithLedger () =
    global.Persistence.initializeDatabase ()
    Ledger.Persistence.initializeLedgerSchema ()
    Budgeting.Persistence.initializeBudgetingSchema ()

let private ledgerAdapterTests =
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    let months = [ "2026-03-01"; "2026-04-01"; "2026-05-01" ]
    testList "Ausgaben der Periode aus dem Kassenbuch, Adapter" [
        testCase "Ein Periodenmonat ohne Buchung der Kategorie zählt als 0" <| fun () ->
            let sums = Map.ofList [ a, [ DateTime(2026, 4, 1), eur -12.5m ] ]
            let activity = QueryHandlers.periodActivityFromLedger [ a; b ] months (Some (DateTime(2019, 7, 14))) sums
            Expect.equal
                (activity |> Map.tryFind a)
                (Some (Map.ofList [ "2026-03-01", eur 0m; "2026-04-01", eur -12.5m; "2026-05-01", eur 0m ]))
                "a: März und Mai mit 0"
            Expect.equal
                (activity |> Map.tryFind b)
                (Some (Map.ofList [ "2026-03-01", eur 0m; "2026-04-01", eur 0m; "2026-05-01", eur 0m ]))
                "b ohne Buchung: jeder Monat 0"

        testCase "Ein Periodenmonat vor dem Monat der ersten Buchung fehlt, der Monat der ersten Buchung zählt" <| fun () ->
            let activity = QueryHandlers.periodActivityFromLedger [ a ] months (Some (DateTime(2026, 4, 30))) Map.empty
            Expect.equal (activity |> Map.tryFind a) (Some (Map.ofList [ "2026-04-01", eur 0m; "2026-05-01", eur 0m ])) "März fehlt"

        testCase "Ohne Buchung im Kassenbuch fehlt jeder Monat" <| fun () ->
            let activity = QueryHandlers.periodActivityFromLedger [ a ] months None Map.empty
            Expect.equal (activity |> Map.tryFind a) (Some Map.empty) "kein Monat"

        testCase "Fehlt ein Periodenmonat, ist der Fehlbetrag nicht gerechnet, mit Grund" <| fun () ->
            // Jährlich fällig im März, gelesen im November: Periode März bis Oktober. Erste Buchung im Mai.
            let november = "2032-11-01"
            let id = Guid.NewGuid()
            let month = toBudgetMonthWith None (BudgetEventsFixture.ynabTargets (annualMonth id november 5m 0m 30m 0m)) Set.empty (annualMonth id november 5m 0m 30m 0m)
            let needed = periodMonthsNeeded month
            let activity = QueryHandlers.periodActivityFromLedger (periodCategoriesNeeded month) needed (Some (DateTime(2032, 5, 3))) Map.empty
            let row = compareUnderfunded month activity |> List.exactlyOne
            Expect.equal (row.Outcome, row.Reason) (NotCompared, Some PeriodActivityMissingText) "nicht gerechnet"
            let complete = QueryHandlers.periodActivityFromLedger (periodCategoriesNeeded month) needed (Some (DateTime(2032, 3, 31))) Map.empty
            let row = compareUnderfunded month complete |> List.exactlyOne
            Expect.equal row.Reason None "Gegenprobe: ab März gerechnet"
    ]

let private ledgerHandlerTests =
    // Kassenbuch und Budget-Streams teilen die In-Memory-DB. Isolation über neue Kategorie-Ids und eigene Monate (die
    // Jahre 2032, 2037 und 2038 gehören nur diesen Tests), je
    // Test; eine Buchung vor der Periode sichert, dass die erste Buchung des Kassenbuchs nicht später liegt (andere
    // Tests können sie nur früher legen).
    testList "Ausgaben der Periode aus dem Kassenbuch, Lesen eines Monats" [
        testCase "Ausgaben der Periode im Kassenbuch zählen als gedeckt, ohne je gelesene frühere Monate" <| fun () ->
            // Regressionstest budgeting-k5q2w: vorher fehlten nie gelesene Monate, der Fehlbetrag war "nicht gerechnet".
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2032-11-01"
            booked id (utc 2032 2 10) -1m
            booked id (utc 2032 6 14) -10m
            let port, calls = countingPort (fun key -> annualMonth id key 5m 0m 30m 0m)
            let saved = ResizeArray<UnderfundedComparison list>()
            let save _ rows = async { saved.Add rows }

            let result = QueryHandlers.budgetMonthWith save global.Persistence.getConnection port readAt november |> run

            Expect.isOk result "Monat gelesen"
            // budgeting-eh8xe: das erste Lesen übernimmt das Target und fragt dafür einmal `/categories`.
            Expect.equal (List.ofSeq calls) [ november; "categories" ] "ein YNAB-Aufruf für den Monat, einer für die Übernahme"
            let row = saved |> Seq.exactlyOne |> List.exactlyOne
            // (60 − (25 + 10)) / 5 − 5 = 0 wie YNAB; ohne die Ausgaben wären es 2.
            Expect.equal (row.Outcome, row.Bb) (Equal, Some (eur 0m)) "gedeckt wie YNAB"

        testCase "Eine Ausgabe, die nach dem Lesen eines früheren Monats ins Kassenbuch kam, zählt mit" <| fun () ->
            // Regressionstest budgeting-k5q2w (Review b9869, Backend W1): vorher galt der Stand des letzten Lesens.
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2038-11-01"
            booked id (utc 2038 2 10) -1m
            let port, _ = countingPort (fun key -> if key = november then annualMonth id key 5m 0m 30m 0m else annualMonth id key 0m 0m 0m 0m)
            let ignoreRows _ _ = async { return () }
            for m in 3 .. 10 do
                QueryHandlers.budgetMonthWith ignoreRows global.Persistence.getConnection port readAt $"2038-%02d{m}-01"
                |> run
                |> ignore
            booked id (utc 2038 10 30) -10m
            let saved = ResizeArray<UnderfundedComparison list>()
            let save _ rows = async { saved.Add rows }

            QueryHandlers.budgetMonthWith save global.Persistence.getConnection port readAt november |> run |> ignore

            let row = saved |> Seq.exactlyOne |> List.exactlyOne
            Expect.equal (row.Outcome, row.Bb) (Equal, Some (eur 0m)) "die späte Ausgabe im Oktober zählt"

        testCase "Scheitert das Lesen des Kassenbuchs, ist die Antwort dieselbe und nichts wird gespeichert" <| fun () ->
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2037-11-01"
            let port = portFor (annualMonth id november 5m 0m 30m 0m)
            let broken: QueryHandlers.LedgerActivityPort =
                { LoadMonthlyActivity = fun _ _ -> async { return failwith "Kassenbuch weg" }
                  LoadFirstEntryDate = fun () -> async { return failwith "Kassenbuch weg" } }
            let saved = ResizeArray<UnderfundedComparison list>()
            let save _ rows = async { saved.Add rows }

            let result = QueryHandlers.budgetMonthWithLedger broken save global.Persistence.getConnection port readAt november |> run
            let without = QueryHandlers.budgetMonthWith (fun _ _ -> async { return () }) global.Persistence.getConnection port readAt november |> run

            Expect.isOk result "Ok trotz Fehler"
            Expect.equal result without "derselbe Monat"
            Expect.isEmpty saved "keine Abweichungsliste"

        testCase "Die erste Buchung des Kassenbuchs zählt ohne gelöschte" <| fun () ->
            // 1901 liegt vor jeder anderen Buchung der Tests.
            initializeWithLedger ()
            booked (Guid.NewGuid()) (utc 1901 1 3) -1m
            let deleted = Ledger.Events.newEntryId ()
            let category = Guid.NewGuid()
            let payload: Ledger.Events.EntryObservedInYnabPayload =
                { AccountId = Ledger.Events.LedgerAccountId (Guid.NewGuid())
                  YnabTransactionId = $"ynab-k5q2w-{Guid.NewGuid()}"
                  ImportId = None
                  Date = utc 1901 1 2
                  Amount = eur -1m
                  Payee = None
                  Memo = None
                  Category = Some { CategoryId = YnabCategoryId category; CategoryName = "Jährlich" }
                  Splits = []
                  Cleared = Ledger.Events.Cleared
                  TransferAccountId = None
                  TransferYnabTransactionId = None
                  Approved = true
                  FlagColor = None
                  FlagName = None }
            let event body : Ledger.Events.LedgerEvent =
                { EntryId = deleted; OccurredAt = utc 2031 3 2; Actor = Ledger.Events.YnabMirror; Body = body }
            Ledger.Persistence.appendAndApply
                [ event (Ledger.Events.EntryObservedInYnab payload); event Ledger.Events.EntryDeletionObservedInYnab ]
            |> run

            // Über den Live-Port: belegt dessen Anschluss an `loadFirstEntryDate` (Doktrin Punkt 3, Review k5q2w).
            Expect.equal (QueryHandlers.liveLedgerActivityPort.LoadFirstEntryDate () |> run) (Some (DateTime(1901, 1, 3))) "der 2. ist gelöscht"

        testCase "Liegt die erste Buchung des Kassenbuchs in der Periode, rechnet BB den Fehlbetrag nicht" <| fun () ->
            // Review k5q2w: belegt, dass das Lesen eines Monats die erste Buchung an den Adapter gibt. Periode März bis
            // Oktober 2039, erste Buchung im Mai: März und April fehlen.
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2039-11-01"
            let port = portFor (annualMonth id november 5m 0m 30m 0m)
            let ledger: QueryHandlers.LedgerActivityPort =
                { LoadMonthlyActivity = fun _ _ -> async { return Map.empty }
                  LoadFirstEntryDate = fun () -> async { return Some (DateTime(2039, 5, 2)) } }
            let saved = ResizeArray<UnderfundedComparison list>()
            let save _ rows = async { saved.Add rows }

            QueryHandlers.budgetMonthWithLedger ledger save global.Persistence.getConnection port readAt november |> run |> ignore

            let row = saved |> Seq.exactlyOne |> List.exactlyOne
            Expect.equal (row.Outcome, row.Reason) (NotCompared, Some PeriodActivityMissingText) "März und April fehlen"
    ]

[<Tests>]
let tests =
    testList
        "Fehlbetrag in BB, Parallelrechnung"
        [ ruleTests; comparisonTests; handlerTests; persistenceTests; ledgerAdapterTests; ledgerHandlerTests ]
