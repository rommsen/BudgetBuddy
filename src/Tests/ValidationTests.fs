module Tests.ValidationTests

open System
open Expecto
open Shared.Domain
open Server.Validation

// ============================================
// Reusable Validators Tests
// ============================================

let reusableValidatorTests =
    testList "Reusable Validators" [
        testList "validateRequired" [
            test "returns None for valid string" {
                let result = validateRequired "Field" "valid value"
                Expect.isNone result "Should return None for valid string"
            }

            test "returns Some error for empty string" {
                let result = validateRequired "Field" ""
                Expect.isSome result "Should return error for empty string"
                Expect.equal result.Value "Field ist erforderlich" "Error message should be correct"
            }

            test "returns Some error for whitespace string" {
                let result = validateRequired "Field" "   "
                Expect.isSome result "Should return error for whitespace string"
            }

            test "returns Some error for null string" {
                let result = validateRequired "Field" null
                Expect.isSome result "Should return error for null string"
            }
        ]

        testList "validateLength" [
            test "returns None when length is within range" {
                let result = validateLength "Field" 5 10 "hello"
                Expect.isNone result "Should return None for valid length"
            }

            test "returns None when length equals minimum" {
                let result = validateLength "Field" 5 10 "12345"
                Expect.isNone result "Should return None when length equals minimum"
            }

            test "returns None when length equals maximum" {
                let result = validateLength "Field" 5 10 "1234567890"
                Expect.isNone result "Should return None when length equals maximum"
            }

            test "returns Some error when length below minimum" {
                let result = validateLength "Field" 5 10 "abc"
                Expect.isSome result "Should return error when length below minimum"
                Expect.stringContains result.Value "zwischen 5 und 10" "Error message should contain range"
            }

            test "returns Some error when length above maximum" {
                let result = validateLength "Field" 5 10 "12345678901"
                Expect.isSome result "Should return error when length above maximum"
            }
        ]

        testList "validateRange" [
            test "returns None when value is within range" {
                let result = validateRange "Days" 1 90 30
                Expect.isNone result "Should return None for valid value"
            }

            test "returns None when value equals minimum" {
                let result = validateRange "Days" 1 90 1
                Expect.isNone result "Should return None when value equals minimum"
            }

            test "returns None when value equals maximum" {
                let result = validateRange "Days" 1 90 90
                Expect.isNone result "Should return None when value equals maximum"
            }

            test "returns Some error when value below minimum" {
                let result = validateRange "Days" 1 90 0
                Expect.isSome result "Should return error when value below minimum"
                Expect.stringContains result.Value "zwischen 1 und 90" "Error message should contain range"
            }

            test "returns Some error when value above maximum" {
                let result = validateRange "Days" 1 90 100
                Expect.isSome result "Should return error when value above maximum"
            }
        ]
    ]

// ============================================
// Settings Validation Tests
// ============================================

let settingsValidationTests =
    testList "Settings Validation" [
        testList "validateYnabToken" [
            test "returns Ok for valid token" {
                let token = "valid-ynab-token-12345"
                let result = validateYnabToken token
                Expect.isOk result "Should return Ok for valid token"
            }

            test "returns Error for empty token" {
                let result = validateYnabToken ""
                Expect.isError result "Should return Error for empty token"
                match result with
                | Error errors -> Expect.isNonEmpty errors "Should have error messages"
                | _ -> failtest "Expected Error"
            }

            test "returns Error for token too short" {
                let result = validateYnabToken "short"
                Expect.isError result "Should return Error for token too short"
            }

            test "returns Ok for token at minimum length" {
                let token = String.replicate 10 "a"
                let result = validateYnabToken token
                Expect.isOk result "Should return Ok for token at minimum length"
            }
        ]

        // banking-import-vwhfm: die Texte stehen auf der Seite Einstellungen unter dem Feld, mit deren Beschriftungen.
        test "die Feldnamen der Ablehnung heißen wie auf der Seite Einstellungen" {
            Expect.equal (validateYnabToken "") (Error [ "YNAB-Token ist erforderlich"; "YNAB-Token muss zwischen 10 und 500 Zeichen lang sein" ]) "Token"
            let empty = { ClientId = ""; ClientSecret = ""; Username = ""; Password = ""; AccountId = None }
            Expect.equal
                (validateComdirectSettings empty)
                (Error [ "Client-ID ist erforderlich"; "Client-Secret ist erforderlich"; "Benutzername ist erforderlich"; "PIN ist erforderlich" ])
                "Comdirect"
        }

        testList "validateComdirectSettings" [
            test "returns Ok for valid settings" {
                let settings = {
                    ClientId = "client-id"
                    ClientSecret = "client-secret"
                    Username = "username"
                    Password = "password"
                    AccountId = Some "account-id"
                }
                let result = validateComdirectSettings settings
                Expect.isOk result "Should return Ok for valid settings"
            }

            test "returns Error when ClientId is empty" {
                let settings = {
                    ClientId = ""
                    ClientSecret = "client-secret"
                    Username = "username"
                    Password = "password"
                    AccountId = Some "account-id"
                }
                let result = validateComdirectSettings settings
                Expect.isError result "Should return Error when ClientId is empty"
            }

            test "returns Error when ClientSecret is empty" {
                let settings = {
                    ClientId = "client-id"
                    ClientSecret = ""
                    Username = "username"
                    Password = "password"
                    AccountId = Some "account-id"
                }
                let result = validateComdirectSettings settings
                Expect.isError result "Should return Error when ClientSecret is empty"
            }

            test "returns Error when Username is empty" {
                let settings = {
                    ClientId = "client-id"
                    ClientSecret = "client-secret"
                    Username = ""
                    Password = "password"
                    AccountId = Some "account-id"
                }
                let result = validateComdirectSettings settings
                Expect.isError result "Should return Error when Username is empty"
            }

            test "returns Error when Password is empty" {
                let settings = {
                    ClientId = "client-id"
                    ClientSecret = "client-secret"
                    Username = "username"
                    Password = ""
                    AccountId = Some "account-id"
                }
                let result = validateComdirectSettings settings
                Expect.isError result "Should return Error when Password is empty"
            }

            test "returns Ok when AccountId is None (optional)" {
                let settings = {
                    ClientId = "client-id"
                    ClientSecret = "client-secret"
                    Username = "username"
                    Password = "password"
                    AccountId = None
                }
                let result = validateComdirectSettings settings
                Expect.isOk result "Should return Ok when AccountId is None (optional field)"
            }

            test "returns multiple errors when multiple fields are empty" {
                let settings = {
                    ClientId = ""
                    ClientSecret = ""
                    Username = "username"
                    Password = "password"
                    AccountId = None
                }
                let result = validateComdirectSettings settings
                match result with
                | Error errors -> Expect.equal errors.Length 2 "Should have 2 error messages"
                | _ -> failtest "Expected Error"
            }
        ]

        testList "validateSyncSettings" [
            let validSync : SyncSettings = { DaysToFetch = 30; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 360 }
            let intervalMessage = "Intervall muss zwischen 60 und 1440 Minuten liegen"

            // banking-import-t4raw (ADR 0093 Punkt 9): das Intervall des automatischen Abrufs.
            test "ein Intervall von 59 oder 1441 Minuten wird mit konkreter Meldung abgelehnt" {
                for minutes in [ 59; 1441 ] do
                    Expect.equal
                        (validateSyncSettings { validSync with AutoFetchIntervalMinutes = minutes })
                        (Error [ intervalMessage ])
                        $"{minutes} Minuten"
            }

            test "60 und 1440 Minuten gehen durch, auch bei abgeschaltetem Abruf" {
                for minutes in [ 60; 1440 ] do
                    Expect.isOk (validateSyncSettings { validSync with AutoFetchIntervalMinutes = minutes; AutoFetchEnabled = false }) $"{minutes} Minuten"
            }

            test "Tage und Intervall falsch: beide Fehler gesammelt" {
                match validateSyncSettings { validSync with DaysToFetch = 0; AutoFetchIntervalMinutes = 0 } with
                | Error errors ->
                    Expect.equal errors.Length 2 "zwei Meldungen"
                    Expect.contains errors intervalMessage "das Intervall"
                    Expect.contains errors "Abzurufende Tage müssen zwischen 1 und 90 liegen" "die Tage, auf Deutsch"
                | Ok _ -> failtest "Ablehnung erwartet"
            }

            test "returns Ok for valid settings" {
                let settings = { validSync with DaysToFetch = 30 }
                let result = validateSyncSettings settings
                Expect.isOk result "Should return Ok for valid settings"
            }

            test "returns Ok for minimum days" {
                let settings = { validSync with DaysToFetch = 1 }
                let result = validateSyncSettings settings
                Expect.isOk result "Should return Ok for minimum days"
            }

            test "returns Ok for maximum days" {
                let settings = { validSync with DaysToFetch = 90 }
                let result = validateSyncSettings settings
                Expect.isOk result "Should return Ok for maximum days"
            }

            test "returns Error for zero days" {
                let settings = { validSync with DaysToFetch = 0 }
                let result = validateSyncSettings settings
                Expect.isError result "Should return Error for zero days"
            }

            test "returns Error for too many days" {
                let settings = { validSync with DaysToFetch = 100 }
                let result = validateSyncSettings settings
                Expect.isError result "Should return Error for too many days"
            }

            test "returns Error for negative days" {
                let settings = { validSync with DaysToFetch = -1 }
                let result = validateSyncSettings settings
                Expect.isError result "Should return Error for negative days"
            }
        ]
    ]

// ============================================
// Rules Validation Tests
// ============================================

let rulesValidationTests =
    testList "Rules Validation" [
        testList "validateRuleName" [
            test "returns None for valid name" {
                let result = validateRuleName "My Rule"
                Expect.isNone result "Should return None for valid name"
            }

            test "returns Some error for empty name" {
                let result = validateRuleName ""
                Expect.isSome result "Should return error for empty name"
            }

            test "returns Some error for name too long" {
                let longName = String.replicate 101 "a"
                let result = validateRuleName longName
                Expect.isSome result "Should return error for name too long"
            }
        ]

        testList "validatePattern" [
            test "returns None for valid pattern" {
                let result = validatePattern ".*amazon.*"
                Expect.isNone result "Should return None for valid pattern"
            }

            test "returns Some error for empty pattern" {
                let result = validatePattern ""
                Expect.isSome result "Should return error for empty pattern"
            }

            test "returns Some error for pattern too long" {
                let longPattern = String.replicate 501 "a"
                let result = validatePattern longPattern
                Expect.isSome result "Should return error for pattern too long"
            }
        ]

        testList "validateNewRule" [
            test "returns Ok for valid request" {
                let request = {
                    Name = "Test Rule"
                    Pattern = ".*test.*"
                    PatternType = PatternType.Regex
                    TargetField = Combined
                    CategoryId = YnabCategoryId (Guid.NewGuid())
                    PayeeOverride = None
                    Enabled = true
                }
                let result = validateNewRule request
                Expect.isOk result "Should return Ok for valid request"
            }

            test "returns Error for empty name" {
                let request = {
                    Name = ""
                    Pattern = ".*test.*"
                    PatternType = PatternType.Regex
                    TargetField = Combined
                    CategoryId = YnabCategoryId (Guid.NewGuid())
                    PayeeOverride = None
                    Enabled = true
                }
                let result = validateNewRule request
                Expect.isError result "Should return Error for empty name"
            }

            test "returns Error for empty pattern" {
                let request = {
                    Name = "Test Rule"
                    Pattern = ""
                    PatternType = PatternType.Regex
                    TargetField = Combined
                    CategoryId = YnabCategoryId (Guid.NewGuid())
                    PayeeOverride = None
                    Enabled = true
                }
                let result = validateNewRule request
                Expect.isError result "Should return Error for empty pattern"
            }

            test "returns multiple errors when multiple fields invalid" {
                let request = {
                    Name = ""
                    Pattern = ""
                    PatternType = PatternType.Regex
                    TargetField = Combined
                    CategoryId = YnabCategoryId (Guid.NewGuid())
                    PayeeOverride = None
                    Enabled = true
                }
                let result = validateNewRule request
                match result with
                | Error errors -> Expect.isGreaterThan errors.Length 1 "Should have multiple errors"
                | _ -> failtest "Expected Error"
            }

            test "sammelt alle Fehler auf Deutsch, auch den leeren Payee-Override" {
                let request = {
                    Name = ""
                    Pattern = ""
                    PatternType = PatternType.Regex
                    TargetField = Combined
                    CategoryId = YnabCategoryId (Guid.NewGuid())
                    PayeeOverride = Some "   "
                    Enabled = true
                }
                Expect.equal
                    (validateNewRule request)
                    (Error [ "Regelname ist erforderlich"; "Muster ist erforderlich"; "Payee-Override darf nicht leer sein" ])
                    "alle drei Fehler"
            }
        ]

        // Doktrin Punkt 4: dieselben Ablehnungsfälle wie die Vorlage `validateNewRule`, dazu Stelle und Kategorie.
        testList "validateRuleRestore" [
            let validRule : Rule = {
                Id = RuleId (Guid.NewGuid())
                Name = "Test Rule"
                Pattern = ".*test.*"
                PatternType = PatternType.Regex
                TargetField = Combined
                CategoryId = YnabCategoryId (Guid.NewGuid())
                CategoryName = "Lebensmittel"
                PayeeOverride = None
                Priority = 3
                Enabled = true
                CreatedAt = DateTime(2026, 1, 1)
                UpdatedAt = DateTime(2026, 1, 1)
            }
            let restore rule position = validateRuleRestore { Rule = rule; Position = position }

            test "eine gültige Regel an Stelle 0 ist Ok" {
                Expect.isOk (restore validRule 0) "gültig"
            }

            test "ein leerer Name wird abgelehnt" {
                Expect.equal (restore { validRule with Name = "" } 0) (Error [ "Regelname ist erforderlich" ]) "Name"
            }

            test "ein leeres Muster wird abgelehnt" {
                Expect.equal (restore { validRule with Pattern = "" } 0) (Error [ "Muster ist erforderlich" ]) "Muster"
            }

            test "ein leerer Payee-Override wird abgelehnt" {
                Expect.equal
                    (restore { validRule with PayeeOverride = Some " " } 0)
                    (Error [ "Payee-Override darf nicht leer sein" ])
                    "Payee-Override"
            }

            test "eine negative Stelle wird abgelehnt" {
                Expect.equal (restore validRule -1) (Error [ "Die Stelle darf nicht negativ sein" ]) "Stelle"
            }

            test "eine leere Kategorie wird abgelehnt" {
                Expect.equal
                    (restore { validRule with CategoryId = YnabCategoryId Guid.Empty } 0)
                    (Error [ "Kategorie ist erforderlich" ])
                    "Kategorie"
            }

            test "ein leerer Kategoriename wird abgelehnt" {
                Expect.equal
                    (restore { validRule with CategoryName = "  " } 0)
                    (Error [ "Kategoriename ist erforderlich" ])
                    "Kategoriename"
            }

            test "alle Fehler kommen gesammelt" {
                let broken =
                    { validRule with
                        Name = ""
                        Pattern = ""
                        PayeeOverride = Some " "
                        CategoryId = YnabCategoryId Guid.Empty
                        CategoryName = "" }
                Expect.equal
                    (restore broken -1)
                    (Error [
                        "Regelname ist erforderlich"
                        "Muster ist erforderlich"
                        "Payee-Override darf nicht leer sein"
                        "Die Stelle darf nicht negativ sein"
                        "Kategorie ist erforderlich"
                        "Kategoriename ist erforderlich" ])
                    "alle sechs Fehler"
            }
        ]

        testList "validateRuleChange Payee-Override" [
            let change payeeOverride = {
                Id = RuleId (Guid.NewGuid())
                Name = Some ""
                Pattern = None
                PatternType = None
                TargetField = None
                CategoryId = None
                PayeeOverride = payeeOverride
                Priority = Some 50000
                Enabled = None
            }

            test "ein leerer neuer Override wird abgelehnt, alle Fehler kommen gesammelt" {
                Expect.equal
                    (validateRuleChange (change (PayeeOverrideChange.Set " ")))
                    (Error [
                        "Regelname ist erforderlich"
                        "Priorität darf höchstens 10000 sein"
                        "Payee-Override darf nicht leer sein" ])
                    "alle drei Fehler"
            }

            test "Entfernen und Behalten brauchen keinen Wert" {
                let onlyName payeeOverride = { change payeeOverride with Name = Some "x"; Priority = None }
                Expect.isOk (validateRuleChange (onlyName PayeeOverrideChange.Remove)) "Entfernen"
                Expect.isOk (validateRuleChange (onlyName PayeeOverrideChange.Keep)) "Behalten"
            }
        ]

        testList "validateRuleChange" [
            test "returns Ok for valid update request" {
                let request = {
                    Id = RuleId (Guid.NewGuid())
                    Name = Some "Updated Rule"
                    Pattern = Some ".*updated.*"
                    PatternType = Some PatternType.Regex
                    TargetField = Some Combined
                    CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                    PayeeOverride = PayeeOverrideChange.Keep
                    Priority = Some 50
                    Enabled = Some true
                }
                let result = validateRuleChange request
                Expect.isOk result "Should return Ok for valid update request"
            }

            // Bug (Review nach categorization-m47g8): Der Server vergibt neuen Regeln die niedrigste Priorität
            // minus 1, also auch 0 oder negativ. Die Prüfung ließ nur 0 bis 10000 zu und hätte eine Änderung,
            // die die vorhandene Priorität mitschickt, abgelehnt.
            test "eine Priorität 0 oder negativ, wie sie der Server neuen Regeln gibt, ist gültig" {
                for priority in [ 0; -3 ] do
                    let request = {
                        Id = RuleId (Guid.NewGuid())
                        Name = None
                        Pattern = None
                        PatternType = None
                        TargetField = None
                        CategoryId = None
                        PayeeOverride = PayeeOverrideChange.Keep
                        Priority = Some priority
                        Enabled = None
                    }
                    Expect.isOk (validateRuleChange request) $"Priorität {priority} muss gültig sein"
            }

            test "returns Ok when all optional fields are None" {
                let request = {
                    Id = RuleId (Guid.NewGuid())
                    Name = None
                    Pattern = None
                    PatternType = None
                    TargetField = None
                    CategoryId = None
                    PayeeOverride = PayeeOverrideChange.Keep
                    Priority = None
                    Enabled = None
                }
                let result = validateRuleChange request
                Expect.isOk result "Should return Ok when all optional fields are None"
            }

            test "returns Error when Name is Some empty" {
                let request = {
                    Id = RuleId (Guid.NewGuid())
                    Name = Some ""
                    Pattern = None
                    PatternType = None
                    TargetField = None
                    CategoryId = None
                    PayeeOverride = PayeeOverrideChange.Keep
                    Priority = None
                    Enabled = None
                }
                let result = validateRuleChange request
                Expect.isError result "Should return Error when Name is Some empty"
            }

            test "returns Error when Pattern is Some empty" {
                let request = {
                    Id = RuleId (Guid.NewGuid())
                    Name = None
                    Pattern = Some ""
                    PatternType = None
                    TargetField = None
                    CategoryId = None
                    PayeeOverride = PayeeOverrideChange.Keep
                    Priority = None
                    Enabled = None
                }
                let result = validateRuleChange request
                Expect.isError result "Should return Error when Pattern is Some empty"
            }

            test "returns Error when Priority is Some and out of range" {
                let request = {
                    Id = RuleId (Guid.NewGuid())
                    Name = None
                    Pattern = None
                    PatternType = None
                    TargetField = None
                    CategoryId = None
                    PayeeOverride = PayeeOverrideChange.Keep
                    Priority = Some 50000
                    Enabled = None
                }
                let result = validateRuleChange request
                Expect.isError result "Should return Error when Priority is out of range"
            }
        ]
    ]

// ============================================
// Transaction Validation Tests
// ============================================

let transactionValidationTests =
    testList "Transaction Validation" [
        testList "validatePayeeOverride" [
            test "returns None when payee is None" {
                let result = validatePayeeOverride None
                Expect.isNone result "Should return None when payee is None"
            }

            test "returns None for valid payee" {
                let result = validatePayeeOverride (Some "Valid Payee")
                Expect.isNone result "Should return None for valid payee"
            }

            test "returns Some error for empty payee" {
                let result = validatePayeeOverride (Some "")
                Expect.isSome result "Should return error for empty payee"
            }

            test "returns Some error for whitespace payee" {
                let result = validatePayeeOverride (Some "   ")
                Expect.isSome result "Should return error for whitespace payee"
            }

            test "returns Some error for payee too long" {
                let longPayee = String.replicate 201 "a"
                let result = validatePayeeOverride (Some longPayee)
                Expect.isSome result "Should return error for payee too long"
            }
        ]
    ]

let splitErrorTextTests =
    testList "splitErrorText" [
        testCase "zu wenige Positionen nennt die Mindestzahl" <| fun () ->
            Expect.equal (splitErrorText (TooFewLines 1)) "Eine Aufteilung braucht mindestens 2 Positionen" "Text"
        testCase "falsche Währung nennt die Buchung" <| fun () ->
            Expect.equal (splitErrorText CurrencyMismatch) "Alle Positionen müssen die Währung der Buchung haben" "Text"
        testCase "falsche Summe nennt Ist und Soll in dieser Reihenfolge" <| fun () ->
            Expect.equal (splitErrorText (SumMismatch (50m, 40m))) "Die Positionen ergeben 40, die Buchung beträgt aber 50" "Text"
    ]

// ============================================
// All Tests
// ============================================

// budgeting-qm5x9, ADR 0084 Punkt 8: Eine leere Befehls-Id wird je Auftrag abgelehnt, gesammelt mit den übrigen
// Fehlern desselben Auftrags.
let commandIdTests =
    let empty = Some Guid.Empty
    let errorsOf (result: Result<'a, string list>) =
        match result with
        | Error errors -> errors
        | Ok _ -> failtest "Fehler erwartet"
    let expectBoth (errors: string list) (other: string) =
        Expect.contains errors EmptyCommandIdText "leere Befehls-Id"
        Expect.exists errors (fun e -> e.Contains other) $"und der übrige Fehler: {errors}"
    testList "Befehls-Id an der Grenze" [
        test "Verschieben mit leerer Id und ungültigem Monat meldet beides" {
            let request : Shared.Budgeting.MoveRequest =
                { Source = Shared.Budgeting.ToBeBudgetedSide
                  Target = Shared.Budgeting.CategorySide (Guid.NewGuid())
                  Amount = { Amount = 10m; Currency = "EUR" }
                  CommandId = empty }
            expectBoth (errorsOf (validateMoveRequest "kein-monat" request)) "Monat"
        }
        test "Zuweisen mit leerer Id und leerer Kategorie meldet beides" {
            let request : Shared.Budgeting.AssignRequest =
                { CategoryId = Guid.Empty; Assigned = { Amount = 10m; Currency = "EUR" }; CommandId = empty }
            expectBoth (errorsOf (validateAssignRequest "2026-11-01" request)) "Kategorie"
        }
        test "Pausieren mit leerer Id und leerer Kategorie meldet beides" {
            let request : Shared.Budgeting.TargetPauseRequest =
                { CategoryId = Guid.Empty; Intent = Shared.Budgeting.PauseTarget; CommandId = empty }
            expectBoth (errorsOf (validateTargetPauseRequest "2026-11-01" "2026-11-01" request)) "Kategorie"
        }
        test "Übernehmen mit leerer Id im vergangenen Monat meldet beides" {
            let preview : Shared.Budgeting.TargetPreview =
                { Month = "2026-10-01"
                  Lines = []
                  Skipped = []
                  Total = { Amount = 0m; Currency = "EUR" }
                  ToBeBudgeted = { Amount = 0m; Currency = "EUR" }
                  ToBeBudgetedAfter = { Amount = 0m; Currency = "EUR" }
                  CommandId = empty }
            expectBoth (errorsOf (validateTargetsApply "2026-11-01" "2026-10-01" preview)) "vergangenen"
        }
        test "Eine echte Id geht durch" {
            let request : Shared.Budgeting.AssignRequest =
                { CategoryId = Guid.NewGuid(); Assigned = { Amount = 10m; Currency = "EUR" }; CommandId = Some (Guid.NewGuid()) }
            Expect.equal (validateAssignRequest "2026-11-01" request) (Ok ()) "gültig"
        }
    ]

[<Tests>]
let tests =
    testList "Validation Tests" [
        commandIdTests
        reusableValidatorTests
        settingsValidationTests
        rulesValidationTests
        transactionValidationTests
        splitErrorTextTests
    ]
