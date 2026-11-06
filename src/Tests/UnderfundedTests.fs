module Tests.UnderfundedTests

// budgeting-v7kfw, budgeting-hbhzf, ADR 0084 Punkt 7: BBs Fehlbetrag. Reine Funktion je Zeile der
// Zuordnungstabelle, die Zeile des Monatsmodells (gegen das Soll), die Ausgaben der Periode aus dem Kassenbuch
// (Adapter rein, Lesen eines Monats, Vorschau und Targets anwenden gegen In-Memory-SQLite) und der Rückbau der
// Parallelrechnung im Schema. Alle Beträge und Namen sind erfunden.

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
// Periodenmonate
// ============================================

let private periodTests =
    testList "Periodenmonate" [
        testCase "Gebraucht werden die Periodenmonate aller Targets mit Fälligkeit, jeder Monat einmal" <| fun () ->
            // budgeting-b9869, Grenze nach budgeting-pf8n7. Jährlich fällig im März: März bis Oktober. Vierteljährlich
            // fällig im Dezember: September und Oktober.
            let annual, quarterly, monthly = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let targets =
                Map.ofList
                    [ annual, { need 60m (Years 1) with Date = Some "2026-03-15" }
                      quarterly, { need 30m (Months 3) with Date = Some "2026-09-30" }
                      monthly, need 100m (Months 1) ]
            Expect.equal
                (periodMonthsNeeded targets november)
                [ for m in 3 .. 10 -> $"2026-%02d{m}-01" ]
                "März bis Oktober, September und Oktober nur einmal"
            Expect.equal (List.sort (periodCategoriesNeeded targets november)) (List.sort [ annual; quarterly ]) "nur mit Periode"
            Expect.equal (periodMonthsNeeded (Map.ofList [ monthly, need 100m (Months 1) ]) november) [] "ohne Fälligkeit keine Monate"
    ]

// ============================================
// BBs Fehlbetrag in der Zeile (budgeting-hbhzf)
// ============================================

let private rawMonth (id: Guid) (assigned: decimal) (activity: decimal) (available: decimal) : YnabMonth =
    { Month = november
      ToBeBudgeted = eur 0m
      Categories =
        [ { Id = id; Name = "Zeile"; GroupName = "Alltag"; Assigned = eur assigned; Activity = eur activity
            Available = eur available; Target = None; Hidden = false; Deleted = false; Internal = false } ] }

let private rowIn (month: BudgetMonth) : BudgetCategory =
    month.Groups |> List.collect (fun g -> g.Categories) |> List.exactlyOne

/// BBs Faltung mit offener Rückschreibung: Soll `soll`, bestätigt `confirmed` (Euro).
let private openAt (id: Guid) (soll: decimal) (confirmed: decimal) : Map<Guid, AssignmentState> option =
    Some (Map.ofList [ id, { Assigned = toMilliunits (eur soll); Confirmed = toMilliunits (eur confirmed); Open = Some (Guid.NewGuid()) } ])

let private noActivity : Map<Guid, Map<string, Money>> option = Some Map.empty

let private projectionTests =
    testList "BBs Fehlbetrag in der Zeile" [
        testCase "Die Zeile zeigt fehlt aus BBs Fehlbetrag gegen das Soll, YNABs Wert zählt nicht" <| fun () ->
            // Regressionstest budgeting-hbhzf: vorher stand hier YNABs `goal_under_funded` (30, auf YNABs Zugewiesen 20).
            let id = Guid.NewGuid()
            let month = toBudgetMonthWith (openAt id 30m 20m) noActivity (Map.ofList [ id, need 50m (Months 1) ]) Set.empty (rawMonth id 20m 0m 20m)
            let row = rowIn month
            Expect.equal row.Underfunded (Some (eur 20m)) "50 − Soll 30"
            Expect.equal (missingAmount row) (Some (eur 20m)) "fehlt"

        testCase "Der Übertrag kommt aus YNABs Verfügbar, Zugewiesen und Aktivität, unabhängig vom Soll" <| fun () ->
            // Übertrag = 50 − 20 − (−30) = 60. Auffüllen bis 100 mit Soll 30: 100 − (60 + 30) = 10. Mit dem Soll im
            // Übertrag wären es 20.
            let id = Guid.NewGuid()
            let refill = { need 100m (Months 1) with NeedsWholeAmount = Some false }
            let month = toBudgetMonthWith (openAt id 30m 20m) noActivity (Map.ofList [ id, refill ]) Set.empty (rawMonth id 20m -30m 50m)
            Expect.equal (rowIn month).Underfunded (Some (eur 10m)) "Übertrag ohne Soll"

        testCase "Mit Fälligkeit zählen die Ausgaben der Periode der Kategorie als gedeckt" <| fun () ->
            // Jährlich fällig im März, gelesen im November: Periode März bis Oktober, November bis März 5 Monate.
            // Übertrag 25, Ausgaben im Juni 10: (60 − 35) / 5 = 5; ohne die Ausgaben 7.
            let id = Guid.NewGuid()
            let target = { need 60m (Years 1) with Date = Some "2026-03-15" }
            let activity = Some (Map.ofList [ id, Map.ofList [ for m in 3 .. 10 -> $"2026-%02d{m}-01", (if m = 6 then eur -10m else eur 0m) ] ])
            let month = toBudgetMonthWith None activity (Map.ofList [ id, target ]) Set.empty (rawMonth id 0m 0m 25m)
            Expect.equal (rowIn month).Underfunded (Some (eur 5m)) "gedeckt"

        testCase "Rechnet BB den Fehlbetrag nicht, zeigt die Zeile kein fehlt und Targets anwenden lässt sie weg" <| fun () ->
            let id, due = Guid.NewGuid(), Guid.NewGuid()
            let debt = { need 100m (Months 1) with Kind = Debt }
            // Eine Fälligkeit, deren Periode im Kassenbuch fehlt (`PeriodActivityMissingText`).
            let annual = { need 60m (Years 1) with Date = Some "2026-03-15" }
            let raw = { rawMonth id 0m 0m 0m with Categories = (rawMonth id 0m 0m 0m).Categories @ (rawMonth due 0m 0m 0m).Categories }
            let month = toBudgetMonthWith None noActivity (Map.ofList [ id, debt; due, annual ]) Set.empty raw
            for row in month.Groups |> List.collect (fun g -> g.Categories) do
                Expect.equal row.Underfunded None "kein Fehlbetrag"
                Expect.equal (missingAmount row) None "kein fehlt"
            match planTargetApplication month with
            | Ok preview -> Expect.isEmpty preview.Lines "weggelassen"
            | Error message -> failtest message

        testCase "Ohne Target in BB trägt die Zeile keinen Fehlbetrag" <| fun () ->
            let id = Guid.NewGuid()
            let month = toBudgetMonthWith None noActivity Map.empty Set.empty (rawMonth id 0m 0m 0m)
            Expect.equal (rowIn month).Underfunded None "ohne Target"

        testCase "Ohne Ausgaben der Periode trägt keine Zeile einen Fehlbetrag, auch ein monatliches Target nicht" <| fun () ->
            let id = Guid.NewGuid()
            let month = toBudgetMonthWith None None (Map.ofList [ id, need 50m (Months 1) ]) Set.empty (rawMonth id 0m 0m 0m)
            Expect.equal (rowIn month).Underfunded None "kein Fehlbetrag"
            Expect.equal (rowIn month).Target (Some (need 50m (Months 1))) "das Target bleibt"
    ]

// ============================================
// Ausgaben der Periode aus dem Kassenbuch (budgeting-k5q2w, budgeting-hbhzf)
// ============================================

let private readAt = DateTime(2031, 3, 2, 8, 0, 0, DateTimeKind.Utc)

/// Ein YNAB-Monat mit einer Kategorie "Jährlich" (budgeting-b9869): Ziel 60, jährlich fällig im März (das
/// gespeicherte Datum liegt weit zurück, `dueFrom` schiebt es). Gelesen im November: Periode März bis Oktober,
/// November bis März 5 Monate.
let private annualMonth (categoryId: Guid) (monthKey: string) (assigned: decimal) (activity: decimal) (available: decimal) : YnabMonth =
    { Month = monthKey
      ToBeBudgeted = eur 0m
      Categories =
        [ { Id = categoryId
            Name = "Jährlich"
            GroupName = "Alltag"
            Assigned = eur assigned
            Activity = eur activity
            Available = eur available
            Target = Some { need 60m (Years 1) with Date = Some "2000-03-15" }
            Hidden = false
            Deleted = false
            Internal = false } ] }

/// Ein Port, der jeden YNAB-Aufruf mitschreibt (Grenze: 200 je Stunde): `FetchMonth` mit dem Monat,
/// `FetchCategoryGoals` als "categories" mit den Goals des zuletzt gelesenen Monats (budgeting-eh8xe), `SetAssigned`
/// als (Kategorie, Milliunits) in der zweiten Liste.
let private countingPort (monthOf: string -> YnabMonth) : CommandHandlers.YnabPort * ResizeArray<string> * ResizeArray<Guid * int64> =
    let calls = ResizeArray<string>()
    let writes = ResizeArray<Guid * int64>()
    let last = ref None
    { FetchMonth =
        fun key ->
            async {
                let month = monthOf key
                lock calls (fun () -> calls.Add key; last.Value <- Some month)
                return Ok month
            }
      SetAssigned = fun _ id milliunits -> async { lock writes (fun () -> writes.Add((id, milliunits))); return Ok () }
      FetchCategoryGoals =
        fun () ->
            async {
                let month = lock calls (fun () -> calls.Add "categories"; last.Value)
                return Ok (month |> Option.map BudgetEventsFixture.goalsOf |> Option.defaultValue [])
            } },
    calls,
    writes

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

/// Das Kassenbuch scheitert bei jedem Lesen.
let private brokenLedger: CommandHandlers.LedgerActivityPort =
    { LoadMonthlyActivity = fun _ _ -> async { return failwith "Kassenbuch weg" }
      LoadFirstEntryDate = fun () -> async { return failwith "Kassenbuch weg" } }

/// Die Gruppe der Tests, die das Server-Log fangen (auch in `BudgetLeadTests`).
[<Literal>]
let ServerLogGroup = "Server-Log"

/// Fängt, was der Server ins Fehler-Log schreibt, während `work` läuft.
let private stderrOf (work: unit -> 'a) : 'a * string =
    let original = Console.Error
    use writer = new IO.StringWriter()
    Console.SetError writer
    try
        let result = work ()
        result, writer.ToString()
    finally
        Console.SetError original

let private pageRow (result: YnabResult<BudgetMonth>) : BudgetCategory =
    match result with
    | Ok month -> rowIn month
    | Error e -> failtest $"Monat erwartet: {e}"

let private ledgerAdapterTests =
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    let months = [ "2026-03-01"; "2026-04-01"; "2026-05-01" ]
    testList "Ausgaben der Periode aus dem Kassenbuch, Adapter" [
        testCase "Ein Periodenmonat ohne Buchung der Kategorie zählt als 0" <| fun () ->
            let sums = Map.ofList [ a, [ DateTime(2026, 4, 1), eur -12.5m ] ]
            let activity = periodActivityFromLedger [ a; b ] months (Some (DateTime(2019, 7, 14))) sums
            Expect.equal
                (activity |> Map.tryFind a)
                (Some (Map.ofList [ "2026-03-01", eur 0m; "2026-04-01", eur -12.5m; "2026-05-01", eur 0m ]))
                "a: März und Mai mit 0"
            Expect.equal
                (activity |> Map.tryFind b)
                (Some (Map.ofList [ "2026-03-01", eur 0m; "2026-04-01", eur 0m; "2026-05-01", eur 0m ]))
                "b ohne Buchung: jeder Monat 0"

        testCase "Ein Periodenmonat vor dem Monat der ersten Buchung fehlt, der Monat der ersten Buchung zählt" <| fun () ->
            let activity = periodActivityFromLedger [ a ] months (Some (DateTime(2026, 4, 30))) Map.empty
            Expect.equal (activity |> Map.tryFind a) (Some (Map.ofList [ "2026-04-01", eur 0m; "2026-05-01", eur 0m ])) "März fehlt"

        testCase "Ohne Buchung im Kassenbuch fehlt jeder Monat" <| fun () ->
            let activity = periodActivityFromLedger [ a ] months None Map.empty
            Expect.equal (activity |> Map.tryFind a) (Some Map.empty) "kein Monat"

        testCase "Fehlt ein Periodenmonat, trägt die Zeile keinen Fehlbetrag" <| fun () ->
            // Jährlich fällig im März, gelesen im November: Periode März bis Oktober. Erste Buchung im Mai.
            let november = "2032-11-01"
            let id = Guid.NewGuid()
            let raw = annualMonth id november 5m 0m 30m
            let targets = BudgetEventsFixture.ynabTargets raw
            let needed = periodMonthsNeeded targets november
            let ids = periodCategoriesNeeded targets november
            let late = periodActivityFromLedger ids needed (Some (DateTime(2032, 5, 3))) Map.empty
            Expect.equal (rowIn (toBudgetMonthWith None (Some late) targets Set.empty raw)).Underfunded None "nicht gerechnet"
            let complete = periodActivityFromLedger ids needed (Some (DateTime(2032, 3, 31))) Map.empty
            Expect.equal
                (rowIn (toBudgetMonthWith None (Some complete) targets Set.empty raw)).Underfunded
                (Some (eur 2m))
                "Gegenprobe: ab März gerechnet, (60 − 25) / 5 − 5 = 2"
    ]

let private ledgerHandlerTests =
    // Kassenbuch und Budget-Streams teilen die In-Memory-DB. Isolation über neue Kategorie-Ids und eigene Monate (die
    // Jahre 2032 bis 2039 gehören nur diesen Tests), je Test; eine Buchung vor der Periode sichert, dass die erste
    // Buchung des Kassenbuchs nicht später liegt (andere Tests können sie nur früher legen).
    // Die Tests fangen das Server-Log über `Console.SetError`: nie gleichzeitig mit anderen, die es auch tun.
    testSequencedGroup ServerLogGroup
    <| testList "Ausgaben der Periode aus dem Kassenbuch, Lesen eines Monats" [
        testCase "Die Seite zeigt BBs Fehlbetrag, Ausgaben der Periode im Kassenbuch zählen als gedeckt" <| fun () ->
            // Regressionstest budgeting-hbhzf: vorher zeigte die Seite YNABs `goal_under_funded`. Regressionstest
            // budgeting-k5q2w: vorher fehlten nie gelesene frühere Monate.
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2034-11-01"
            booked id (utc 2034 2 10) -1m
            booked id (utc 2034 6 14) -10m
            let port, calls, _ = countingPort (fun key -> annualMonth id key 5m 0m 30m)

            let row = QueryHandlers.budgetMonthWith global.Persistence.getConnection port readAt november |> run |> pageRow

            // (60 − (25 + 10)) / 5 − 5 = 0; ohne die Ausgaben wären es 2.
            Expect.equal row.Underfunded (Some (eur 0m)) "gedeckt"
            // budgeting-eh8xe: das erste Lesen übernimmt das Target und fragt dafür einmal `/categories`.
            Expect.equal (List.ofSeq calls) [ november; "categories" ] "ein YNAB-Aufruf für den Monat, einer für die Übernahme"

        testCase "Eine Ausgabe, die nach dem Lesen eines früheren Monats ins Kassenbuch kam, zählt mit" <| fun () ->
            // Regressionstest budgeting-k5q2w (Review b9869, Backend W1): vorher galt der Stand des letzten Lesens.
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2038-11-01"
            booked id (utc 2038 2 10) -1m
            let port, _, _ = countingPort (fun key -> if key = november then annualMonth id key 5m 0m 30m else annualMonth id key 0m 0m 0m)
            for m in 3 .. 10 do
                QueryHandlers.budgetMonthWith global.Persistence.getConnection port readAt $"2038-%02d{m}-01"
                |> run
                |> ignore
            booked id (utc 2038 10 30) -10m

            let row = QueryHandlers.budgetMonthWith global.Persistence.getConnection port readAt november |> run |> pageRow

            Expect.equal row.Underfunded (Some (eur 0m)) "die späte Ausgabe im Oktober zählt"

        testCase "Ist das Kassenbuch nicht lesbar, kommt der Monat ohne BBs Fehlbetrag und der Grund steht im Log" <| fun () ->
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2037-11-01"
            // Dazu ein monatliches Target, das ohne Periodenmonate gerechnet werden könnte (50 − 0).
            let monthly = Guid.NewGuid()
            let withMonthly key =
                let raw = annualMonth id key 5m 0m 30m
                { raw with
                    Categories =
                        raw.Categories
                        @ [ { raw.Categories.Head with Id = monthly; Name = "Monatlich"; Assigned = eur 0m; Available = eur 0m; Target = Some (need 50m (Months 1)) } ] }
            let port, _, _ = countingPort withMonthly

            let result, log =
                stderrOf (fun () -> QueryHandlers.budgetMonthWithLedger brokenLedger global.Persistence.getConnection port readAt november |> run)

            match result with
            | Ok month ->
                let rows = month.Groups |> List.collect (fun g -> g.Categories)
                Expect.equal (rows |> List.map (fun r -> r.Underfunded)) [ None; None ] "kein Fehlbetrag, auch nicht monatlich"
                Expect.equal (rows |> List.filter (fun r -> r.Target.IsSome) |> List.length) 2 "der Monat kommt mit Targets"
            | Error e -> failtest $"Monat erwartet: {e}"
            Expect.stringContains log "Kassenbuch weg" "Grund im Log"

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
            Expect.equal (CommandHandlers.liveLedgerActivityPort.LoadFirstEntryDate () |> run) (Some (DateTime(1901, 1, 3))) "der 2. ist gelöscht"

        testCase "Liegt die erste Buchung des Kassenbuchs in der Periode, rechnet BB den Fehlbetrag nicht" <| fun () ->
            // Review k5q2w: belegt, dass das Lesen eines Monats die erste Buchung an den Adapter gibt. Periode März bis
            // Oktober 2039, erste Buchung im Mai: März und April fehlen.
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2039-11-01"
            let port, _, _ = countingPort (fun key -> annualMonth id key 5m 0m 30m)
            let ledger: CommandHandlers.LedgerActivityPort =
                { LoadMonthlyActivity = fun _ _ -> async { return Map.empty }
                  LoadFirstEntryDate = fun () -> async { return Some (DateTime(2039, 5, 2)) } }

            let row = QueryHandlers.budgetMonthWithLedger ledger global.Persistence.getConnection port readAt november |> run |> pageRow

            Expect.equal row.Underfunded None "März und April fehlen"
    ]

let private applyTests =
    // Die Tests fangen das Server-Log über `Console.SetError`: nie gleichzeitig mit anderen, die es auch tun.
    testSequencedGroup ServerLogGroup
    <| testList "Ausgaben der Periode aus dem Kassenbuch, Vorschau und Targets anwenden" [
        testCase "Targets anwenden zählt die Ausgaben der Periode im Kassenbuch als gedeckt" <| fun () ->
            // Regressionstest budgeting-hbhzf: vorher wies Targets anwenden YNABs `goal_under_funded` zu.
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2035-11-01"
            booked id (utc 2035 2 10) -1m
            booked id (utc 2035 6 14) -10m
            let port, _, writes = countingPort (fun key -> annualMonth id key 0m 0m 25m)
            let shown =
                match CommandHandlers.previewWith global.Persistence.getConnection port readAt november |> run with
                | Ok p -> p
                | Error e -> failtest $"Vorschau erwartet: {e}"
            // Übertrag 25, Ausgaben im Juni 10: (60 − 35) / 5 = 5. Ohne die Ausgaben wären es 7.
            Expect.equal (shown.Lines |> List.map (fun l -> l.CategoryId, l.Underfunded)) [ id, eur 5m ] "Vorschau"
            match CommandHandlers.applyWith global.Persistence.getConnection port readAt november shown |> run with
            | Ok (Applied _) -> ()
            | other -> failtest $"Applied erwartet: {other}"
            Expect.equal (List.ofSeq writes) [ id, 5000L ] "zugewiesen"

        testCase "Ist das Kassenbuch nicht lesbar, lehnen Vorschau und Targets anwenden mit Meldung ab und weisen nichts zu" <| fun () ->
            initializeWithLedger ()
            let id = Guid.NewGuid()
            let november = "2036-11-01"
            let port, _, writes = countingPort (fun key -> annualMonth id key 0m 0m 25m)
            let unreadable = Error (YnabError.LocalReadFailed CommandHandlers.PeriodActivityUnreadableText)
            let shown : TargetPreview =
                { Month = november
                  Lines = []
                  Skipped = []
                  Total = eur 0m
                  ToBeBudgeted = eur 0m
                  ToBeBudgetedAfter = eur 0m
                  CommandId = Some (Guid.NewGuid()) }

            let preview, previewLog =
                stderrOf (fun () -> CommandHandlers.previewWithLedger brokenLedger global.Persistence.getConnection port readAt november |> run)
            let applied, applyLog =
                stderrOf (fun () -> CommandHandlers.applyWithLedger brokenLedger global.Persistence.getConnection port readAt november shown |> run)

            Expect.equal preview unreadable "Vorschau abgelehnt"
            Expect.equal (applied |> Result.map ignore) unreadable "Targets anwenden abgelehnt"
            Expect.stringContains previewLog "Kassenbuch weg" "Grund der Vorschau im Log"
            Expect.stringContains applyLog "Kassenbuch weg" "Grund im Log"
            Expect.isEmpty writes "nichts gesendet"
            let decisions =
                match Budgeting.Persistence.loadMovements { CategoryId = Some id; Month = None; Origin = None } |> run with
                | Ok found -> found |> List.filter (fun m -> not (isFromYnab m.Origin))
                | Error message -> failtest message
            Expect.isEmpty decisions "keine Entscheidung festgehalten"
    ]

// ============================================
// Rückbau im Schema
// ============================================

let private schemaTests =
    testList "Rückbau im Schema" [
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

        testCase "Das Schema entfernt underfunded_comparisons, auch wiederholt" <| fun () ->
            // budgeting-hbhzf: die Abweichungsliste der Parallelrechnung entfällt. Eigene Datenbank wie oben.
            use conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
            conn.Open()
            Dapper.SqlMapper.Execute(conn, "CREATE TABLE underfunded_comparisons (month TEXT NOT NULL)") |> ignore
            Budgeting.Persistence.dropUnderfundedComparisonsOn conn
            Budgeting.Persistence.dropUnderfundedComparisonsOn conn
            let count =
                Dapper.SqlMapper.ExecuteScalar<int64>(
                    conn,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'underfunded_comparisons'")
            Expect.equal count 0L "Tabelle entfernt"
    ]

[<Tests>]
let tests =
    testList
        "Fehlbetrag in BB"
        [ ruleTests; periodTests; projectionTests; ledgerAdapterTests; ledgerHandlerTests; applyTests; schemaTests ]
