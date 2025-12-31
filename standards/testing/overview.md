# Testing Overview

> Expecto testing strategies for F# applications.

## Overview

Expecto is a smooth testing framework for F# with powerful features including property-based testing with FsCheck.

## When to Use This

- Writing unit tests
- Testing domain logic
- API integration tests
- Property-based tests

## Patterns

### Basic Test Structure

```fsharp
module Tests.DomainTests

open Expecto
open Shared.Domain

[<Tests>]
let tests =
    testList "Domain" [
        testCase "process trims name" <| fun () ->
            let item = { Id = 1; Name = "  Test  "; CreatedAt = DateTime.UtcNow }
            let result = Domain.process item
            Expect.equal result.Name "Test" "Should trim"

        testAsync "load returns data" {
            let! items = Api.itemApi.getItems()
            Expect.isNotEmpty items "Should return items"
        }
    ]
```

### Test Organization

```fsharp
// Group by feature
[<Tests>]
let allTests =
    testList "All Tests" [
        testList "Domain" domainTests
        testList "Validation" validationTests
        testList "Persistence" persistenceTests
    ]
```

### Expect Module

```fsharp
Expect.equal actual expected "message"
Expect.isTrue condition "message"
Expect.isFalse condition "message"
Expect.isOk result "message"
Expect.isError result "message"
Expect.isNone option "message"
Expect.isSome option "message"
Expect.isNotEmpty list "message"
Expect.throws (fun () -> failwith "error") "message"
```

## Checklist

- [ ] Test project configured
- [ ] Tests organized by feature
- [ ] All domain functions tested
- [ ] API endpoints tested
- [ ] Validation tested
- [ ] Async operations tested
- [ ] Edge cases covered

## See Also

- `domain-tests.md` - Testing pure functions
- `api-tests.md` - Testing APIs
- `../global/development-workflow.md` - Bug fix protocol
