# Property-Based Tests

> FsCheck property-based testing patterns.

## Overview

Property-based testing generates random inputs to verify properties of functions. Use FsCheck with Expecto for property tests.

## When to Use This

- Testing serialization roundtrips
- Testing invariants
- Testing mathematical properties
- Testing edge cases

## Patterns

### Property Tests

```fsharp
open Expecto
open FsCheck

[<Tests>]
let tests =
    testList "Properties" [
        testProperty "roundtrip serialization" <| fun (item: Item) ->
            let json = serialize item
            let deserialized = deserialize json
            item = deserialized

        testProperty "adding 0 is identity" <| fun (x: int) ->
            x + 0 = x

        testProperty "list reverse twice is identity" <| fun (list: int list) ->
            list |> List.rev |> List.rev = list
    ]
```

## Checklist

- [ ] Serialization roundtrips tested
- [ ] Invariants verified
- [ ] Edge cases explored

## See Also

- `overview.md` - Testing basics
- `domain-tests.md` - Unit tests
