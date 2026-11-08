module Tests.SettingsViewTests

// Reine Bausteine der Settings-Seite (Components.Settings.View).

open System
open Expecto
open Shared.Domain

[<Tests>]
let settingsViewTests =
    testList "Settings: reine Bausteine" [

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

        // ADR 0087 Punkt 24: eine Hauptaktion je Ansicht. Die TAN-Box steht über Lesen und Bearbeiten;
        // ist ein Formular offen, trägt dessen Speichern den Akzent.
        test "TAN bestätigt ist die Hauptaktion nur ohne offenes Formular" {
            let variant = Components.Settings.View.tanConfirmVariant
            Expect.equal (variant None) Client.DesignSystem.Button.Primary "kein Formular offen"
            for section in [ Components.Settings.Types.ComdirectSection; Components.Settings.Types.SyncSection ] do
                Expect.equal (variant (Some section)) Client.DesignSystem.Button.Secondary $"{section} offen"
        }
    ]
