---
name: fsharp-tests
description: |
  Write F# tests using Expecto framework for domain, API, and persistence layers.
  Use when adding tests for new features, fixing bugs, or ensuring code quality.
  Ensures regression prevention and documentation of expected behavior.
  Creates code in src/Tests/.
allowed-tools: Read, Edit, Write, Grep, Glob, Bash
standards:
  required-reading:
    - standards/testing/overview.md
  workflow:
    - step: 1
      file: standards/testing/domain-tests.md
      purpose: Test pure business logic
      output: src/Tests/DomainTests.fs
    - step: 2
      file: standards/testing/api-tests.md
      purpose: Test API endpoints
      output: src/Tests/ApiTests.fs
    - step: 3
      file: standards/testing/persistence-tests.md
      purpose: Test database operations
      output: src/Tests/PersistenceTests.fs
---

# F# Testing with Expecto

## When to Use This Skill

Activate when:
- User requests "add tests for X"
- Implementing new features (ALWAYS add tests)
- Fixing bugs (MUST add regression test)
- Need to verify behavior
- Project has src/Tests/ directory with Expecto

## Testing Priority

**MANDATORY:**
1. **Domain tests** - Pure business logic (ALWAYS)
2. **Validation tests** - Input validation rules (ALWAYS)
3. **Bug regression tests** - Prevent bug recurrence (ALWAYS)

**Optional (as needed):**
4. API integration tests
5. Persistence tests (use in-memory SQLite)
6. Frontend tests

## Implementation Workflow

### Step 1: Domain Tests (Pure Logic)

**Read:** `standards/testing/domain-tests.md`
**Create:** `src/Tests/DomainTests.fs`

```fsharp
module DomainTests

open Expecto
open Domain

[<Tests>]
let tests = testList "Domain" [
    testCase "processItem trims name" <| fun () ->
        let input = { Id = 1; Name = "  test  " }
        let result = Domain.processItem input
        Expect.equal result.Name "test" "Should trim whitespace"

    testCase "calculateTotal sums amounts" <| fun () ->
        let items = [
            { Id = 1; Amount = 10.0m }
            { Id = 2; Amount = 20.0m }
        ]
        let total = Domain.calculateTotal items
        Expect.equal total 30.0m "Should sum all amounts"

    // Bug regression test
    testCase "handles empty list without error" <| fun () ->
        // Regression: This used to throw NullReferenceException
        let result = Domain.calculateTotal []
        Expect.equal result 0.0m "Should return 0 for empty list"
]
```

**Key:** Test pure functions, fast, no I/O

---

### Step 2: Validation Tests

**Read:** `standards/testing/domain-tests.md` (validation section)
**Create:** `src/Tests/ValidationTests.fs`

```fsharp
module ValidationTests

open Expecto
open Validation

[<Tests>]
let tests = testList "Validation" [
    testCase "rejects empty name" <| fun () ->
        let item = { Id = 0; Name = "" }
        match Validation.validateItem item with
        | Error errs -> Expect.contains errs "Name required" "Should reject empty name"
        | Ok _ -> failtest "Should have failed"

    testCase "accepts valid item" <| fun () ->
        let item = { Id = 0; Name = "Valid" }
        match Validation.validateItem item with
        | Ok _ -> ()
        | Error errs -> failtestf "Should be valid: %A" errs

    testCase "accumulates multiple errors" <| fun () ->
        let item = { Id = 0; Name = "" }
        match Validation.validateItem item with
        | Error errs ->
            Expect.isGreaterThan errs.Length 0 "Should have errors"
        | Ok _ -> failtest "Should have failed"
]
```

**Key:** Test all validation rules, including edge cases

---

### Step 3: Integration Tests (Optional)

**Read:** `standards/testing/persistence-tests.md`
**Create:** `src/Tests/PersistenceTests.fs`

```fsharp
module PersistenceTests

open Expecto
open Persistence

// Use in-memory SQLite for tests
let private getTestConnection () =
    new SqliteConnection("Data Source=:memory:")

[<Tests>]
let tests = testList "Persistence" [
    testAsync "saves and retrieves item" {
        use conn = getTestConnection()
        do! Persistence.ensureTables conn |> Async.RunSynchronously

        let item = { Id = 0; Name = "Test" }
        do! Persistence.save conn item

        let! retrieved = Persistence.getAll conn
        Expect.hasLength retrieved 1 "Should have 1 item"
        Expect.equal retrieved.[0].Name "Test" "Should match saved name"
    }
]
```

**Key:** Use in-memory database, NEVER write to production DB

---

## Quick Reference

### Expecto Test Patterns

```fsharp
// Single test
testCase "description" <| fun () ->
    let result = function input
    Expect.equal result expected "message"

// Async test
testAsync "description" {
    let! result = asyncFunction()
    Expect.equal result expected "message"
}

// Test list
testList "Group" [
    testCase "test1" <| fun () -> ...
    testCase "test2" <| fun () -> ...
]
```

### Expect Assertions

```fsharp
Expect.equal actual expected "message"
Expect.isTrue condition "message"
Expect.isFalse condition "message"
Expect.isNone option "message"
Expect.isSome option "message"
Expect.contains list item "message"
Expect.hasLength list count "message"
Expect.throws (fun () -> ...) "message"
```

### Bug Regression Template

```fsharp
testCase "description of what bug it prevents" <| fun () ->
    // Comment explaining the bug and what was broken
    // Regression: [Bug description]
    let result = functionThatWasBroken input
    Expect.equal result expected "Should work correctly"
```

## Verification Checklist

- [ ] **Read standards** (testing/overview.md)
- [ ] Domain logic tests written
- [ ] Validation rules tested
- [ ] Bug fixes have regression tests
- [ ] Tests use in-memory DB (if persistence)
- [ ] All tests pass (`dotnet test`)
- [ ] Test names describe behavior
- [ ] Failure messages are clear

## Common Pitfalls

**Most Critical:**
- ❌ Writing tests that touch production database
- ❌ Bug fixes without regression tests
- ❌ Testing implementation details
- ❌ Unclear test names
- ✅ Test behavior, not implementation
- ✅ Use in-memory SQLite for integration tests
- ✅ Every bug fix gets a test

## Related Skills

- **fsharp-backend** - Code being tested
- **fsharp-feature** - Tests are part of workflow
- **red-test-fixer** - Use when tests fail

## Detailed Documentation

For complete patterns and examples:
- `standards/testing/overview.md` - Testing philosophy
- `standards/testing/domain-tests.md` - Pure function tests
- `standards/testing/api-tests.md` - API integration tests
- `standards/testing/persistence-tests.md` - Database tests
- `standards/testing/frontend-tests.md` - UI tests
- `standards/testing/property-tests.md` - Property-based tests
