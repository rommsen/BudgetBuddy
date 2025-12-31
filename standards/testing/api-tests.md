# API Tests

> Testing API layer with mocked persistence.

## Overview

Test API endpoints by mocking the persistence layer. Focus on validation, error handling, and orchestration logic.

## When to Use This

- Testing API validation
- Testing error handling
- Testing API orchestration
- Integration testing

## Patterns

### API Tests

```fsharp
[<Tests>]
let tests =
    testList "API" [
        testCase "getItem returns not found" <| fun () ->
            let result =
                Api.itemApi.getItem 99999
                |> Async.RunSynchronously

            match result with
            | Error _ -> ()
            | Ok _ -> failtest "Should return error"

        testCase "saveItem validates input" <| fun () ->
            let item = { Id = 0; Name = "" }  // Invalid
            let result =
                Api.itemApi.saveItem item
                |> Async.RunSynchronously

            Expect.isError result "Should fail validation"
    ]
```

## Checklist

- [ ] All endpoints tested
- [ ] Validation tested
- [ ] Error cases tested
- [ ] Result types verified

## See Also

- `../backend/api-implementation.md` - API patterns
- `../shared/validation.md` - Validation
