module Tests.SettingsViewTests

// Reine Bausteine der Settings-Seite (Components.Settings.View).

open System
open Expecto
open Shared.Domain

[<Tests>]
let settingsViewTests =
    testList "Settings: Konto-Auswahl" [

        // Regression ledger-vq2g8 (Bestandsreview 2026-11-06): die Auswahl des Quick-Add-Kontos
        // zeigte den Stand per `sprintf "%.2f"` englisch als "Bar (1234.50 EUR)".
        test "Ein Konto steht mit deutschem Betrag in der Auswahl" {
            let account : YnabAccount =
                { Id = YnabAccountId (Guid.Parse "00000000-0000-0000-0002-000000000001")
                  Name = "Bar"
                  Balance = { Amount = 1234.5m; Currency = "EUR" }
                  OnBudget = true
                  Closed = false }
            Expect.equal (Components.Settings.View.accountOptionLabel account) "Bar (1.234,50 €)" "deutsches Zahlenformat"
            Expect.equal
                (Components.Settings.View.accountOptionLabel { account with Balance = { Amount = -7.05m; Currency = "EUR" } })
                "Bar (-7,05 €)"
                "negativer Stand"
        }
    ]
