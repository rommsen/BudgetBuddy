module MoneyFormatTests

// `formatAmountText` (ledger-x0yga): die eine Stelle im Code, die einen Geldbetrag
// zu deutsch formatiertem Text macht (Tausenderpunkt, Komma-Dezimaltrennzeichen,
// Euro-Zeichen). Regressionstest fuer den Bug, dass `CultureInfo("de-DE")` in der
// nach JavaScript uebersetzten Fassung nicht greift und stattdessen englisch
// formatiert wird (`38,690.62` statt `38.690,62`).

open Expecto
open Client.DesignSystem.Money

let private formatTests =
    testList "formatAmountText" [

        test "Negativer Betrag ohne Tausendertrennung: Minus, Komma, Euro-Zeichen" {
            // Bug-Regression: dies war zuvor `-48.12 EUR` (englische Formatierung
            // aus `CultureInfo("de-DE")`, das unter Fable nicht greift).
            Expect.equal (formatAmountText true true -48.12m "EUR") "-48,12 €" "Wie im Mockup"
        }

        test "Grosser positiver Betrag bekommt Tausenderpunkte" {
            Expect.equal (formatAmountText true true 21388.85m "EUR") "+21.388,85 €" "Tausenderpunkt vor der Zifferngruppe"
        }

        test "ShowSign = false zeigt kein '+' bei positiven Betraegen (Kontostaende)" {
            Expect.equal (formatAmountText false true 21388.85m "EUR") "21.388,85 €" "Kein Vorzeichen fuer Kontostaende"
        }

        test "Mehrfache Tausendergruppen (Millionen) werden an jeder Stelle getrennt" {
            Expect.equal (formatAmountText false true 1234567.89m "EUR") "1.234.567,89 €" "Zwei Trennpunkte bei sieben Ziffern"
        }

        test "Null wird als 0,00 formatiert, kein Crash auf leerer Gruppierung" {
            Expect.equal (formatAmountText false true 0m "EUR") "0,00 €" "Null bleibt Null"
        }

        test "Vierstelliger Betrag bekommt genau einen Trennpunkt" {
            Expect.equal (formatAmountText false true 1234.5m "EUR") "1.234,50 €" "Eine Ziffer in der ersten Gruppe"
        }

        test "ShowCurrency = false laesst das Euro-Zeichen weg" {
            Expect.equal (formatAmountText false false 48.12m "EUR") "48,12" "Kein Symbol angehaengt"
        }

        test "Unbekannter Waehrungscode faellt auf den Code selbst zurueck" {
            Expect.equal (formatAmountText false true 10m "USD") "10,00 USD" "Kein erfundenes Symbol"
        }
    ]

[<Tests>]
let moneyFormatTests = testList "Money: Formatierung (ledger-x0yga)" [ formatTests ]
