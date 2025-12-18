---
name: fsharp-tests
description: |
  Write comprehensive tests using Expecto for F# applications including domain logic, validation, and async operations.
  Use when implementing tests, ensuring code quality, or verifying functionality.
  Creates test files in src/Tests/ with patterns for unit tests, property tests, and async tests.
---

# F# Testing with Expecto

## When to Use This Skill

Activate when:
- User requests "add tests for X"
- Implementing any new feature (always write tests)
- Need to verify domain logic

## Test Project Setup

### Program.fs

**CRITICAL**: Set `USE_MEMORY_DB` to prevent tests from writing to production!

```fsharp
module Program

open System
open Expecto

[<EntryPoint>]
let main args =
    Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")
    runTestsInAssembly defaultConfig args
```

## Testing Domain Logic

```fsharp
[<Tests>]
let domainTests =
    testList "Domain Logic" [
        testCase "processEntity trims name" <| fun () ->
            let input = { Name = "  Test  "; Description = None }
            let result = Domain.processEntity input
            Expect.equal result.Name "Test" "Should trim whitespace"

        testCase "processEntity sets timestamps" <| fun () ->
            let before = DateTime.UtcNow
            let result = Domain.processEntity input
            Expect.isGreaterThanOrEqual result.CreatedAt before "Should set timestamp"
    ]
```

## Testing Validation

```fsharp
[<Tests>]
let validationTests =
    testList "Validation" [
        testCase "valid entity passes" <| fun () ->
            let entity = { Name = "Valid"; Description = None }
            let result = Validation.validateEntity entity
            Expect.isOk result "Should pass"

        testCase "empty name fails" <| fun () ->
            let entity = { Name = ""; Description = None }
            let result = Validation.validateEntity entity
            Expect.isError result "Should fail"

        testCase "accumulates multiple errors" <| fun () ->
            let entity = { Name = ""; Id = -1 }
            match Validation.validateEntity entity with
            | Error errors -> Expect.isGreaterThan errors.Length 1 "Multiple errors"
            | Ok _ -> failtest "Should fail"
    ]
```

## Testing Async Operations

```fsharp
[<Tests>]
let asyncTests =
    testList "Async Operations" [
        testCaseAsync "getAllEntities returns list" <| async {
            let! result = Persistence.getAllEntities()
            Expect.isNotNull result "Should return list"
        }
    ]
```

## Property-Based Testing

```fsharp
open FsCheck

[<Tests>]
let propertyTests =
    testList "Properties" [
        testProperty "trimming is idempotent" <| fun (s: string) ->
            let trimmed = s.Trim()
            trimmed.Trim() = trimmed
    ]
```

## Running Tests

```bash
dotnet test                              # Run all tests
dotnet test --filter "Name~Validation"   # Run specific tests
dotnet test --watch                      # Watch mode
```

## Best Practices

### Do
- Test domain logic thoroughly (it's pure)
- Test validation rules
- Use descriptive test names
- Test edge cases
- Set `USE_MEMORY_DB=true` for persistence tests

### Don't
- Test implementation details
- Make tests dependent on order
- Write tests that persist to production DB

## Verification Checklist

- [ ] Domain logic tests written
- [ ] Validation tests written
- [ ] Edge cases tested
- [ ] All tests pass
- [ ] `USE_MEMORY_DB` set in Program.fs
