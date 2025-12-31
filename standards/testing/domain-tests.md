# Domain Tests

> Testing pure domain logic.

## Overview

Domain functions are pure - no I/O, no side effects. This makes them trivially testable.

## When to Use This

- Testing business rules
- Testing transformations
- Testing calculations
- Testing event replay

## Patterns

### Testing Pure Functions

```fsharp
[<Tests>]
let tests =
    testList "Domain Logic" [
        testCase "completeTodo updates status" <| fun () ->
            let todo = { Id = 1; Name = "Test"; Status = Pending }
            let result = Domain.completeTodo todo
            Expect.equal result.Status Completed "Status should be completed"

        testCase "calculatePrice applies discount" <| fun () ->
            let item = { Price = 100m }
            let discount = { Percentage = 0.2m }
            let result = Domain.calculatePrice item discount
            Expect.equal result 80m "Should apply 20% discount"
    ]
```

### Testing Event Replay

```fsharp
testCase "replay events rebuilds state" <| fun () ->
    let events = [
        ItemCreated { Id = 1; Name = "Item 1" }
        ItemCreated { Id = 2; Name = "Item 2" }
        ItemDeleted 1
    ]

    let finalState = Domain.replayEvents events
    Expect.equal finalState.Length 1 "Should have 1 item after replay"
    Expect.equal finalState.[0].Id 2 "Should be item 2"
```

## Checklist

- [ ] All domain functions tested
- [ ] Business rules verified
- [ ] Edge cases covered
- [ ] No I/O in tests
- [ ] Fast execution

## See Also

- `../backend/domain-logic.md` - Domain patterns
- `property-tests.md` - Property-based testing
