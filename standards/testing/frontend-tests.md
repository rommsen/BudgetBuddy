# Frontend Tests

> Testing Elmish state management.

## Overview

Test the update function and state transitions. Views are typically not unit tested, but state logic should be thoroughly tested.

## When to Use This

- Testing state transitions
- Testing Msg handling
- Testing Cmd generation

## Patterns

### Testing Update

```fsharp
[<Tests>]
let tests =
    testList "State" [
        testCase "LoadItems sets Loading state" <| fun () ->
            let model, _ = State.init()
            let newModel, _ = State.update LoadItems model
            
            match newModel.Items with
            | Loading -> ()
            | _ -> failtest "Should be Loading"

        testCase "ItemsLoaded sets Success state" <| fun () ->
            let model = { Items = Loading }
            let items = [ { Id = 1; Name = "Test" } ]
            let newModel, _ = State.update (ItemsLoaded (Ok items)) model
            
            match newModel.Items with
            | Success loaded -> Expect.equal loaded items "Items should match"
            | _ -> failtest "Should be Success"
    ]
```

## Checklist

- [ ] Update function tested
- [ ] All Msg cases tested
- [ ] State transitions verified
- [ ] RemoteData states tested

## See Also

- `../frontend/state-management.md` - State patterns
