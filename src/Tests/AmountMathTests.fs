module Tests.AmountMathTests

// budgeting-c5w2n: die Betragsrechnung des Felds Zugewiesen und des Betragsfelds.

open Expecto
open Components.Budget.AmountMath

let private cases (name: string) (baseValue: decimal) (table: (string * decimal) list) =
    test name {
        for text, expected in table do
            Expect.equal (evaluateAmount baseValue text) (Some expected) text
    }

[<Tests>]
let tests =
    testList "Betragsrechnung" [
        cases "Ein Operator vorn rechnet relativ zum Stand, eine Zahl vorn ersetzt ihn" 900m
            [ "+20", 920m; "-20", 880m; "*2", 1800m; "x2", 1800m; "X2", 1800m; "/2", 450m; ":2", 450m
              "900+40", 940m; "940", 940m ]
        cases "Punkt vor Strich, auch relativ zum Stand" 900m [ "2+3*4", 14m; "+3*4", 912m; "*2+1", 1801m ]
        cases "Deutsche Beträge" 0m
            [ "1.240,50", 1240.50m; "1.240", 1240m; "12,5", 12.50m; "1.5", 1.50m
              " 1 240 , 50 ", 1240.50m; "1.234,56", 1234.56m ]
        test "Ungültig ist, was keine Rechnung ist" {
            for text in [ ""; "   "; "abc"; "+"; "5+"; "2**3"; "5*-2"; "1,2,3"; "/0"; "5/0"; "20 €"; "1.2.3"; "12,"; "1.000,00,00" ] do
                Expect.isNone (evaluateAmount 900m text) $"'{text}'"
        }
        test "Über 1.000.000,00 im Betrag ist kein Ergebnis, die Grenze selbst schon" {
            Expect.isNone (evaluateAmount 0m "1000000,01") "darüber"
            Expect.isNone (evaluateAmount 0m "0-1000000,01") "negativ darüber"
            Expect.equal (evaluateAmount 0m "1000000") (Some 1000000m) "die Grenze"
        }
        test "Eine Eingabe, die decimal überliefe, wirft nicht" {
            let nines = String.replicate 40 "9"
            for text in [ nines; $"{nines}*{nines}"; "9999999999999999*9999999999999999*9999999999999999"; $"1/{nines}" ] do
                Expect.isNone (evaluateAmount 900m text |> Option.filter (fun v -> abs v > 1000000m)) text
            Expect.isNone (evaluateAmount 900m nines) "zu groß"
        }
        cases "Das Ergebnis liegt auf dem Cent, kaufmännisch gerundet" 0m
            [ "100/3", 33.33m; "0,05/2", 0.03m; "0,1+0,2", 0.30m; "0-0,05/2", -0.03m ]
        cases "Ein negatives Ergebnis ist gültig" 900m [ "0-20", -20m; "-1000", -100m ]
        test "Der Formatierer behält das Vorzeichen und liest sich zurück" {
            Expect.equal (formatSignedAmount -50m) "-50,00" "negativ"
            Expect.equal (formatSignedAmount 900m) "900,00" "positiv"
            Expect.equal (formatSignedAmount -0.05m) "-0,05" "unter einem Euro"
            Expect.equal (evaluateAmount 0m (formatSignedAmount 1240.5m)) (Some 1240.5m) "Rundlauf"
        }
    ]
