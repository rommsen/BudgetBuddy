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
                Expect.equal result.Value "Field is required" "Error message should be correct"
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
                Expect.stringContains result.Value "between 5 and 10" "Error message should contain range"
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
                Expect.stringContains result.Value "between 1 and 90" "Error message should contain range"
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
            test "returns Ok for valid settings" {
                let settings = { DaysToFetch = 30 }
                let result = validateSyncSettings settings
                Expect.isOk result "Should return Ok for valid settings"
            }

            test "returns Ok for minimum days" {
                let settings = { DaysToFetch = 1 }
                let result = validateSyncSettings settings
                Expect.isOk result "Should return Ok for minimum days"
            }

            test "returns Ok for maximum days" {
                let settings = { DaysToFetch = 90 }
                let result = validateSyncSettings settings
                Expect.isOk result "Should return Ok for maximum days"
            }

            test "returns Error for zero days" {
                let settings = { DaysToFetch = 0 }
                let result = validateSyncSettings settings
                Expect.isError result "Should return Error for zero days"
            }

            test "returns Error for too many days" {
                let settings = { DaysToFetch = 100 }
                let result = validateSyncSettings settings
                Expect.isError result "Should return Error for too many days"
            }

            test "returns Error for negative days" {
                let settings = { DaysToFetch = -1 }
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

        testList "validateRuleCreateRequest" [
            test "returns Ok for valid request" {
                let request = {
                    Name = "Test Rule"
                    Pattern = ".*test.*"
                    PatternType = PatternType.Regex
                    TargetField = Combined
                    CategoryId = YnabCategoryId (Guid.NewGuid())
                    PayeeOverride = None
                    Priority = 100
                }
                let result = validateRuleCreateRequest request
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
                    Priority = 100
                }
                let result = validateRuleCreateRequest request
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
                    Priority = 100
                }
                let result = validateRuleCreateRequest request
                Expect.isError result "Should return Error for empty pattern"
            }

            test "returns Error for priority out of range" {
                let request = {
                    Name = "Test Rule"
                    Pattern = ".*test.*"
                    PatternType = PatternType.Regex
                    TargetField = Combined
                    CategoryId = YnabCategoryId (Guid.NewGuid())
                    PayeeOverride = None
                    Priority = -1
                }
                let result = validateRuleCreateRequest request
                Expect.isError result "Should return Error for priority out of range"
            }

            test "returns multiple errors when multiple fields invalid" {
                let request = {
                    Name = ""
                    Pattern = ""
                    PatternType = PatternType.Regex
                    TargetField = Combined
                    CategoryId = YnabCategoryId (Guid.NewGuid())
                    PayeeOverride = None
                    Priority = 100
                }
                let result = validateRuleCreateRequest request
                match result with
                | Error errors -> Expect.isGreaterThan errors.Length 1 "Should have multiple errors"
                | _ -> failtest "Expected Error"
            }
        ]

        testList "validateRuleUpdateRequest" [
            test "returns Ok for valid update request" {
                let request = {
                    Id = RuleId (Guid.NewGuid())
                    Name = Some "Updated Rule"
                    Pattern = Some ".*updated.*"
                    PatternType = Some PatternType.Regex
                    TargetField = Some Combined
                    CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                    PayeeOverride = None
                    Priority = Some 50
                    Enabled = Some true
                }
                let result = validateRuleUpdateRequest request
                Expect.isOk result "Should return Ok for valid update request"
            }

            test "returns Ok when all optional fields are None" {
                let request = {
                    Id = RuleId (Guid.NewGuid())
                    Name = None
                    Pattern = None
                    PatternType = None
                    TargetField = None
                    CategoryId = None
                    PayeeOverride = None
                    Priority = None
                    Enabled = None
                }
                let result = validateRuleUpdateRequest request
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
                    PayeeOverride = None
                    Priority = None
                    Enabled = None
                }
                let result = validateRuleUpdateRequest request
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
                    PayeeOverride = None
                    Priority = None
                    Enabled = None
                }
                let result = validateRuleUpdateRequest request
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
                    PayeeOverride = None
                    Priority = Some 50000
                    Enabled = None
                }
                let result = validateRuleUpdateRequest request
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

// ============================================
// All Tests
// ============================================

[<Tests>]
let tests =
    testList "Validation Tests" [
        reusableValidatorTests
        settingsValidationTests
        rulesValidationTests
        transactionValidationTests
    ]
