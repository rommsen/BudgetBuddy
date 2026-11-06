module Tests.TargetCheckTests

// budgeting-p2zr6, ADR 0082: die reine Prüfregel von Targets prüfen in `Shared.Budgeting`. Monatswert,
// gezählte Monate, Gruppen in ihrer Reihenfolge, Schwellen, Vorschlag und das Neurechnen nach einem Schreiben.

open System
open Expecto
open FsCheck
open Shared.Domain
open Shared.Budgeting

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

/// Der laufende Tag aller Fälle: November 2026.
[<Literal>]
let private Today = "2026-11-05"

/// Die 12 vollen Monate vor November 2026, aufsteigend.
let private fullMonths : string list =
    [ for i in 0 .. 11 -> DateTime(2025, 11, 1).AddMonths(i).ToString("yyyy-MM-01") ]

let private target (kind: TargetKind) (rhythm: TargetRhythm) (goal: decimal) : Target =
    { Goal = eur goal; Rhythm = rhythm; Kind = kind; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }

let private monthly (goal: decimal) = target Need (Months 1) goal

/// Ausgaben je Monat, positiv wie auf der Seite: die Aktivität trägt das umgekehrte Vorzeichen. Fehlende
/// Monate am Anfang stehen mit 0, die Liste füllt von hinten (Oktober zuletzt).
let private spendingOf (amounts: decimal list) : MonthSpending list =
    let padded = List.replicate (12 - List.length amounts) 0m @ amounts
    List.zip fullMonths padded |> List.map (fun (month, spent) -> { Month = month; Activity = eur -spent })

let private source (name: string) (t: Target option) (amounts: decimal list) (change: TargetChangeMark option) : TargetCheckSource =
    { CategoryId = Guid.NewGuid()
      Name = name
      GroupName = "Alltag"
      Target = t
      Spending = spendingOf amounts
      LastChange = change }

let private changedOn (day: string) = Some (TargetSetOn day)

let private rowOf t amounts change = targetCheckRow Today (source "Kategorie" t amounts change)

let private groupOf t amounts change = (rowOf t amounts change).Group

let private every (n: int) (amount: decimal) = List.replicate n amount

[<Tests>]
let monthlyValueTests =
    testList "Targets prüfen: Monatswert" [
        test "Monatlich und MF bleiben, alle 3 Monate ÷ 3, jährlich ÷ 12, alle 2 Jahre ÷ 24" {
            Expect.equal (monthlyValue (monthly 50m)) (Some (eur 50m)) "monatlich"
            Expect.equal (monthlyValue (target MonthlyFunding (Months 1) 50m)) (Some (eur 50m)) "MF"
            Expect.equal (monthlyValue (target Need (Months 3) 300m)) (Some (eur 100m)) "alle 3 Monate"
            Expect.equal (monthlyValue (target Need (Years 1) 1200m)) (Some (eur 100m)) "jährlich"
            Expect.equal (monthlyValue (target Need (Years 2) 2400m)) (Some (eur 100m)) "alle 2 Jahre"
        }

        test "Wöchentlich × 52 ÷ 12 ungerundet, alle 2 Wochen × 52 ÷ 24" {
            match monthlyValue (target Need (Weeks 1) 50m) with
            | Some m ->
                Expect.equal m.Amount (50m * 52m / 12m) "216,666… €"
                Expect.isTrue (m.Amount > 216.666m && m.Amount < 216.667m) "nicht auf Cent gerundet"
            | None -> failtest "wöchentlich hat einen Monatswert"
            Expect.equal (monthlyValue (target Need (Weeks 2) 60m)) (Some (eur 130m)) "alle 2 Wochen"
        }

        test "Sparziele, DEBT, unbekannte Art und ein NEED ohne Rhythmus haben keinen Monatswert" {
            for t in
                [ target TargetBalance (Months 1) 100m
                  target TargetBalanceByDate NoRhythm 100m
                  target Debt (Months 1) 100m
                  target (OtherKind "XYZ") (Months 1) 100m
                  target Need NoRhythm 100m ] do
                Expect.isNone (monthlyValue t) $"{t.Kind} {t.Rhythm}"
        }
    ]

[<Tests>]
let notCheckedTests =
    testList "Targets prüfen: nicht geprüft" [
        test "TB und TBD stehen bei Passt mit Sparziel, auch wenn ihre Ausgaben mehr wären" {
            for kind in [ TargetBalance; TargetBalanceByDate ] do
                let row = rowOf (Some (target kind (Months 1) 100m)) (every 12 500m) None
                Expect.equal (row.Group, row.Note) (Fits, Some SavingsGoalNotChecked) $"{kind}"
            Expect.equal (fitsNoteText SavingsGoalNotChecked) "Sparziel, wird nicht geprüft" "Text"
        }

        test "DEBT, eine unbekannte Art und NEED ohne Rhythmus stehen bei Passt mit wird nicht geprüft" {
            for t in [ target Debt (Months 1) 100m; target (OtherKind "XYZ") (Months 1) 100m; target Need NoRhythm 100m ] do
                let more = rowOf (Some t) (every 12 500m) None
                let never = rowOf (Some t) [] None
                Expect.equal (more.Group, more.Note) (Fits, Some NotChecked) $"{t.Kind} {t.Rhythm} mit hohen Ausgaben"
                Expect.equal (never.Group, never.Note) (Fits, Some NotChecked) $"{t.Kind} {t.Rhythm} ohne Ausgaben"
            Expect.equal (fitsNoteText NotChecked) "wird nicht geprüft" "Text"
        }

        test "Alle 2 Jahre steht immer bei Passt mit wird nicht geprüft" {
            let t = Some (target Need (Years 2) 2400m)
            for amounts in [ []; every 12 500m; [ 2400m ] ] do
                let row = rowOf t amounts None
                Expect.equal (row.Group, row.Note) (Fits, Some NotChecked) $"{amounts}"
        }
    ]

[<Tests>]
let countedMonthTests =
    testList "Targets prüfen: gezählte Monate" [
        test "Ohne Änderung die 12 vollen Monate vor dem laufenden, der laufende nie" {
            let s = source "Kategorie" (Some (monthly 100m)) (every 12 100m) None
            let withCurrent = { s with Spending = s.Spending @ [ { Month = "2026-11-01"; Activity = eur -9999m } ] }
            let row = targetCheckRow Today withCurrent
            Expect.equal (row.CountedMonths |> List.map (fun m -> m.Month)) fullMonths "12 Monate, ohne November"
            Expect.equal row.Average (Some (eur 100m)) "der laufende zählt nicht in den Ø"
        }

        test "Änderung am 15.07. bei laufendem November: August bis Oktober, wird eingeordnet" {
            let row = rowOf (Some (monthly 100m)) (every 12 100m) (changedOn "2026-07-15")
            Expect.equal (row.CountedMonths |> List.map (fun m -> m.Month)) [ "2026-08-01"; "2026-09-01"; "2026-10-01" ] "3 Monate"
            Expect.isNone row.Note "eingeordnet"
        }

        test "Änderung am 01.08.: September und Oktober, Passt mit neu gesetzt am 01.08.2026" {
            let row = rowOf (Some (monthly 100m)) (every 12 500m) (changedOn "2026-08-01")
            Expect.equal (List.length row.CountedMonths) 2 "2 Monate"
            Expect.equal (row.Group, row.Note) (Fits, Some (RecentlySet "2026-08-01")) "zu wenige Monate"
            Expect.equal (fitsNoteText (RecentlySet "2026-08-01")) "neu gesetzt am 01.08.2026" "Text"
        }

        test "Änderung vor mehr als 12 Monaten: 12 Monate" {
            let row = rowOf (Some (monthly 100m)) (every 12 100m) (changedOn "2025-09-20")
            Expect.equal (List.length row.CountedMonths) 12 "12 Monate"
            Expect.isNone row.Note "eingeordnet"
        }

        test "Entfernen vor weniger als 3 vollen Monaten: Passt mit entfernt am …" {
            let row = rowOf None (every 12 500m) (Some (TargetRemovedOn "2026-09-10"))
            Expect.equal (row.Group, row.Note) (Fits, Some (RecentlyRemoved "2026-09-10")) "zu wenige Monate"
            Expect.equal (fitsNoteText (RecentlyRemoved "2026-09-10")) "entfernt am 10.09.2026" "Text"
        }

        test "Entfernen am 15.07. bei laufendem November: 3 gezählte Monate, Kein Target ohne Zusatz" {
            let row = rowOf None (every 12 40m) (Some (TargetRemovedOn "2026-07-15"))
            Expect.equal (row.CountedMonths |> List.map (fun m -> m.Month)) [ "2026-08-01"; "2026-09-01"; "2026-10-01" ] "3 Monate"
            Expect.equal (row.Group, row.Note) (WithoutTarget, None) "genug Monate seit dem Entfernen"
        }

        test "Periode P: jährlich mit 7 gezählten Monaten steht bei Passt, alle 3 Monate mit 3 wird eingeordnet" {
            let yearly = rowOf (Some (target Need (Years 1) 1200m)) [] (changedOn "2026-03-10")
            Expect.equal (List.length yearly.CountedMonths) 7 "April bis Oktober"
            Expect.equal (yearly.Group, yearly.Note) (Fits, Some (RecentlySet "2026-03-10")) "weniger als P = 12"
            let quarterly = rowOf (Some (target Need (Months 3) 300m)) [] (changedOn "2026-07-15")
            Expect.equal (List.length quarterly.CountedMonths) 3 "3 Monate"
            Expect.equal (quarterly.Group, quarterly.Note) (NeverSpends, None) "eingeordnet"
        }
    ]

[<Tests>]
let neverTests =
    testList "Targets prüfen: Gibst du nie aus?" [
        test "Bei P = 1: 0 und 1 Monat mit Ausgaben sind nie, 2 Monate nicht" {
            let t = Some (monthly 100m)
            Expect.equal (groupOf t [] None) NeverSpends "kein Monat"
            Expect.equal (groupOf t [ 50m; 0m ] None) NeverSpends "1 Monat"
            Expect.notEqual (groupOf t [ 50m; 50m ] None) NeverSpends "2 Monate"
        }

        test "Ein Monat mit überwiegender Erstattung zählt nicht als Monat mit Ausgaben" {
            let row = rowOf (Some (monthly 100m)) [ 50m; -30m ] None
            Expect.equal row.MonthsWithSpending 1 "die Erstattung zählt nicht"
            Expect.equal row.Group NeverSpends "nie"
        }

        test "1 Monat Ausgaben, der zugleich weniger erfüllt, steht bei nie, ohne Vorschlag" {
            let row = rowOf (Some (monthly 100m)) [ 50m ] None
            Expect.isTrue (row.Average.Value.Amount < 70m) "erfüllt weniger"
            Expect.equal row.Group NeverSpends "Reihenfolge: nie vor weniger"
            Expect.isNone row.Suggestion "nie hat keinen Vorschlag"
        }

        test "Bei P > 1: jährlich mit einer Zahlung in Höhe des Targets ist Passt, ohne Zahlung nie" {
            let yearly = Some (target Need (Years 1) 240m)
            Expect.equal (groupOf yearly [ 240m; 0m; 0m ] None) Fits "eine Zahlung (Regressionsfall der ersten Fassung)"
            Expect.equal (groupOf yearly [] None) NeverSpends "keine Zahlung"
        }

        test "Alle 3 Monate mit 4 Zahlungen in Höhe des Targets ist Passt" {
            let amounts = [ 300m; 0m; 0m; 300m; 0m; 0m; 300m; 0m; 0m; 300m; 0m; 0m ]
            Expect.equal (groupOf (Some (target Need (Months 3) 300m)) amounts None) Fits "Ø = Monatswert"
        }
    ]

[<Tests>]
let moreTests =
    testList "Targets prüfen: Gibst du mehr aus?" [
        test "Ø genau 1,1 × Monatswert ist nicht mehr, 1 Cent darüber ist mehr" {
            let t = Some (monthly 200m)
            Expect.equal (groupOf t (every 12 220m) None) Fits "Ø 220,00 €"
            Expect.equal (groupOf t (every 11 220m @ [ 220.12m ]) None) SpendsMore "Ø 220,01 €"
        }

        test "Ø − Monatswert 9,99 € ist nicht mehr, 10,00 € ist mehr" {
            let t = Some (monthly 50m)
            Expect.equal (groupOf t (every 12 59.99m) None) Fits "9,99 €"
            Expect.equal (groupOf t (every 12 60m) None) SpendsMore "10,00 €"
        }

        test "Bei P = 1 und 12 Monaten: 6 Monate darüber ist mehr, 5 nicht" {
            let t = Some (monthly 100m)
            Expect.equal (groupOf t (every 6 300m @ every 6 0m) None) SpendsMore "6 Monate"
            Expect.equal (groupOf t (every 5 300m @ every 7 0m) None) Fits "5 Monate, Ø 125 €"
        }

        test "Bei 5 gezählten Monaten: 3 darüber ist mehr, 2 nicht" {
            let t = Some (monthly 100m)
            let since = changedOn "2026-05-15"
            Expect.equal (List.length (rowOf t [] since).CountedMonths) 5 "Juni bis Oktober"
            Expect.equal (groupOf t [ 300m; 300m; 300m; 0m; 0m ] since) SpendsMore "3 Monate"
            Expect.equal (groupOf t [ 300m; 300m; 0m; 0m; 0m ] since) Fits "2 Monate, Ø 120 €"
        }

        test "Ein Monat genau auf dem Monatswert ist nicht darüber" {
            let row = rowOf (Some (monthly 100m)) (every 5 300m @ [ 100m ] @ every 6 0m) None
            Expect.equal row.MonthsOver 5 "100 € ist nicht über 100 €"
            Expect.equal row.Group Fits "5 von 12 reicht nicht"
        }

        test "Bei P > 1 gilt die Zahl der Monate nicht: jährlich 240 € mit zwei Zahlungen von 240 € ist mehr" {
            let row = rowOf (Some (target Need (Years 1) 240m)) [ 240m; 0m; 0m; 0m; 0m; 240m ] None
            Expect.equal row.MonthsOver 2 "2 von 12 Monaten darüber"
            Expect.equal row.Group SpendsMore "Ø 40 € gegen 20 €"
        }
    ]

[<Tests>]
let lessTests =
    testList "Targets prüfen: Gibst du weniger aus?" [
        test "Ø genau 0,7 × Monatswert ist nicht weniger, 1 Cent darunter ist weniger" {
            let t = Some (monthly 100m)
            Expect.equal (groupOf t (every 12 70m) None) Fits "Ø 70,00 €"
            Expect.equal (groupOf t (every 12 69.99m) None) SpendsLess "Ø 69,99 €"
        }

        test "Monatswert − Ø 9,99 € ist nicht weniger, 10,00 € ist weniger" {
            let t = Some (monthly 20m)
            Expect.equal (groupOf t (every 12 10.01m) None) Fits "9,99 €"
            Expect.equal (groupOf t (every 12 10m) None) SpendsLess "10,00 €"
        }
    ]

[<Tests>]
let withoutTargetTests =
    testList "Targets prüfen: Kein Target" [
        test "3 Monate mit Ausgaben und Ø 10,00 € ist Kein Target, Ø 9,99 € Passt" {
            Expect.equal (groupOf None [ 40m; 40m; 40m ] None) WithoutTarget "Ø 10,00 €"
            Expect.equal (groupOf None [ 40m; 40m; 39.88m ] None) Fits "Ø 9,99 €"
        }

        test "2 Monate mit hohem Ø und ohne Ausgaben: Passt" {
            Expect.equal (groupOf None [ 1000m; 1000m ] None) Fits "2 Monate"
            Expect.equal (groupOf None [] None) Fits "ohne Ausgaben"
        }
    ]

[<Tests>]
let suggestionTests =
    let fromAverage (t: Target option) (average: decimal) = suggestTarget t (eur average)
    testList "Targets prüfen: Vorschlag" [
        test "Ø auf volle 5 € aufgerundet, Ø ≤ 0 ohne Vorschlag" {
            let monthlyOf average = fromAverage (Some (monthly 100m)) average |> Option.map (fun s -> s.Monthly)
            Expect.equal (monthlyOf 212.01m) (Some (eur 215m)) "212,01 €"
            Expect.equal (monthlyOf 215m) (Some (eur 215m)) "215,00 €"
            Expect.equal (monthlyOf 0.01m) (Some (eur 5m)) "0,01 €"
            Expect.isNone (monthlyOf 0m) "0 €"
            Expect.isNone (monthlyOf -20m) "Erstattungen überwiegen"
        }

        test "Zurückgerechnet in den Rhythmus" {
            let inRhythm rhythm average = (fromAverage (Some (target Need rhythm 100m)) average).Value.InRhythm
            Expect.equal (inRhythm (Months 1) 215m) (eur 215m) "monatlich"
            Expect.equal (inRhythm (Months 3) 215m) (eur 645m) "alle 3 Monate"
            Expect.equal (inRhythm (Years 1) 215m) (eur 2580m) "jährlich"
            Expect.equal (inRhythm (Weeks 1) 215m) (eur 50m) "wöchentlich: 215 × 12 ÷ 52 = 49,62"
            Expect.equal (inRhythm (Weeks 1) 3m) (eur 1m) "5 € wöchentlich: mindestens 1 €"
        }

        // budgeting-eh8xe: das Übernehmen ändert nur den Betrag, Art, Rhythmus, Datum und Tag bleiben.
        test "Mit Target: gelesenes Datum, KeepRhythm, und das Target danach ändert nur den Betrag" {
            let weekly = { target Need (Weeks 1) 30m with Date = Some "2027-03-01"; Day = Some 2 }
            for shown in [ weekly; { target MonthlyFunding (Months 1) 100m with Day = Some 6 }; target Need (Years 1) 1200m ] do
                let s = (fromAverage (Some shown) 212.01m).Value
                Expect.equal s.Draft { NewGoal = s.InRhythm; NewDate = shown.Date; NewRhythm = KeepRhythm; NewDay = keptWeekday shown } $"{shown.Rhythm}"
                Expect.equal (targetAfter (Some shown) s.Draft) (Ok { shown with Goal = s.InRhythm }) $"{shown.Kind} {shown.Rhythm}: nur der Betrag"
        }

        test "Ohne Target: SetRhythm Monthly ohne Datum mit dem Vorschlag als Betrag" {
            let row = rowOf None [ 40m; 40m; 40m ] None
            Expect.equal row.Group WithoutTarget "Kein Target"
            Expect.equal
                (row.Suggestion |> Option.map (fun s -> s.Draft))
                (Some { NewGoal = eur 10m; NewDate = None; NewRhythm = SetRhythm Monthly; NewDay = None })
                "monatliches NEED"
        }

        test "Mehr und weniger tragen den Vorschlag aus dem Ø der gezählten Monate" {
            let more = rowOf (Some (monthly 50m)) (every 12 60m) None
            Expect.equal (more.Suggestion |> Option.map (fun s -> s.InRhythm)) (Some (eur 60m)) "mehr"
            let less = rowOf (Some (monthly 100m)) (every 12 41m) None
            Expect.equal (less.Suggestion |> Option.map (fun s -> s.InRhythm)) (Some (eur 45m)) "weniger"
        }
    ]

/// Die Rhythmen der Eigenschaft: monatlich bis alle 12 Monate, jährlich, wöchentlich bis alle 4 Wochen, MF.
/// Alle 2 und 3 Jahre fehlen: P > 12 steht immer bei "nicht geprüft", die Eigenschaft wäre dort leer.
let private rhythms : (TargetKind * TargetRhythm) list =
    [ for n in 1 .. 12 -> Need, Months n
      yield Need, Years 1
      for n in 1 .. 4 -> Need, Weeks n
      yield MonthlyFunding, Months 1 ]

/// Ein Target in einem der Rhythmen mit 1 € bis 20.000 € und 12 Monate Ausgaben in Cent. Die Obergrenze je
/// Monat ist 20 €, 200 €, 2.000 € oder 10.000 €, die Untergrenze ein Zwanzigstel davon negativ (Erstattung).
/// So kommen kleine und große Ø vor, und der weite Bereich des Targets macht "mehr" und "weniger" erreichbar.
let private targetAndSpending : Gen<Target * decimal list> =
    gen {
        let! kind, rhythm = Gen.elements rhythms
        let! goalCents = Gen.choose (100, 2_000_000)
        let! top = Gen.elements [ 2_000; 20_000; 200_000; 1_000_000 ]
        let! cents = Gen.listOfLength 12 (Gen.choose (-top / 20, top))
        return target kind rhythm (decimal goalCents / 100m), cents |> List.map (fun c -> decimal c / 100m)
    }

[<Tests>]
let invariantTests =
    testList "Targets prüfen: Invariant ADR 0082 Punkt 9" [
        // 1000 Fälle: ein wöchentlicher Rhythmus mit kleinem Ø, an dem eine grobe Rundung auffällt, ist selten.
        testPropertyWithConfig
            { FsCheckConfig.defaultConfig with maxTest = 1000 }
            "Mit dem zurückgerechneten Vorschlag als Target liegt eine Kategorie weder in mehr noch in weniger" <|
            Prop.forAll (Arb.fromGen targetAndSpending) (fun (shown, amounts) ->
                let before = rowOf (Some shown) amounts None
                let holds =
                    match before.Average |> Option.bind (suggestTarget (Some shown)) with
                    | None -> true
                    | Some suggestion ->
                        let after = rowOf (Some { shown with Goal = suggestion.InRhythm }) amounts None
                        after.Group <> SpendsMore && after.Group <> SpendsLess
                holds
                |> Prop.classify (before.Group = SpendsMore) "vorher mehr"
                |> Prop.classify (before.Group = SpendsLess) "vorher weniger")
    ]

[<Tests>]
let afterWriteTests =
    testList "Targets prüfen: nach dem Schreiben" [
        test "Ein neues Target ergibt Passt mit neu gesetzt am Today, None entfernt am Today, andere bleiben" {
            let written = source "Lebensmittel" (Some (monthly 100m)) (every 12 300m) None
            let other = source "Urlaub" (Some (monthly 100m)) (every 12 300m) None
            let data = { Month = "2026-11-01"; Today = Today; Categories = [ written; other ] }
            let rowFor (d: TargetCheckData) id = d.Categories |> List.find (fun c -> c.CategoryId = id) |> targetCheckRow Today
            Expect.equal (rowFor data written.CategoryId).Group SpendsMore "vorher mehr"

            let set = withWrittenTarget written.CategoryId (Some (monthly 300m)) data
            let row = rowFor set written.CategoryId
            Expect.equal (row.Group, row.Note) (Fits, Some (RecentlySet Today)) "neu gesetzt am Today"
            Expect.equal row.Target (Some (monthly 300m)) "das Target aus der Antwort"
            Expect.equal (set.Categories |> List.find (fun c -> c.CategoryId = other.CategoryId)) other "andere unverändert"

            let removed = withWrittenTarget written.CategoryId None data
            let row = rowFor removed written.CategoryId
            Expect.equal (row.Group, row.Note) (Fits, Some (RecentlyRemoved Today)) "entfernt am Today"
            Expect.equal (fitsNoteText (RecentlyRemoved Today)) "entfernt am 05.11.2026" "Text"
        }
    ]

[<Tests>]
let groupTests =
    testList "Targets prüfen: Gruppen" [
        test "Reihenfolge nach ADR 0082 Punkt 3, leere Gruppen fehlen, Sortierung in der Gruppe" {
            // Die Namen laufen gegen den Abstand: "Z groß" steht alphabetisch hinter "A klein", aber davor.
            // "M gleich" hat denselben Abstand wie "Z groß" und steht nach Name davor.
            let categories =
                [ source "Passt B" (Some (monthly 100m)) (every 12 100m) None
                  source "A Kein Target klein" None (every 12 20m) None
                  source "A Mehr klein" (Some (monthly 100m)) (every 12 150m) None
                  source "Passt A" None [] None
                  source "Z Weniger groß" (Some (monthly 500m)) (every 12 100m) None
                  source "Z Mehr groß" (Some (monthly 100m)) (every 12 400m) None
                  source "M Mehr gleich" (Some (monthly 200m)) (every 12 500m) None
                  source "Z Kein Target groß" None (every 12 200m) None
                  source "M Kein Target gleich" None (every 12 200m) None
                  source "A Weniger klein" (Some (monthly 100m)) (every 12 50m) None
                  source "M Weniger gleich" (Some (monthly 450m)) (every 12 50m) None ]
            let groups = targetCheckGroups Today { Month = "2026-11-01"; Today = Today; Categories = categories }
            Expect.equal
                (groups |> List.map (fun (g, rows) -> g, rows |> List.map (fun r -> r.Name)))
                [ SpendsMore, [ "M Mehr gleich"; "Z Mehr groß"; "A Mehr klein" ]
                  SpendsLess, [ "M Weniger gleich"; "Z Weniger groß"; "A Weniger klein" ]
                  WithoutTarget, [ "M Kein Target gleich"; "Z Kein Target groß"; "A Kein Target klein" ]
                  Fits, [ "Passt A"; "Passt B" ] ]
                "ohne nie, je Gruppe nach Abstand, bei gleichem Abstand nach Name"
            Expect.equal
                (checkGroupOrder |> List.map checkGroupText)
                [ "Gibst du mehr aus?"; "Gibst du weniger aus?"; "Gibst du nie aus?"; "Kein Target"; "Passt" ]
                "Sätze"
        }

        test "Nie steht nach Name sortiert zwischen weniger und Kein Target" {
            let categories =
                [ source "Nie B" (Some (monthly 100m)) [] None
                  source "Kein Target" None (every 12 20m) None
                  source "Nie A" (Some (monthly 100m)) [ 10m ] None ]
            let groups = targetCheckGroups Today { Month = "2026-11-01"; Today = Today; Categories = categories }
            Expect.equal
                (groups |> List.map (fun (g, rows) -> g, rows |> List.map (fun r -> r.Name)))
                [ NeverSpends, [ "Nie A"; "Nie B" ]; WithoutTarget, [ "Kein Target" ] ]
                "nie nach Name"
        }
    ]
