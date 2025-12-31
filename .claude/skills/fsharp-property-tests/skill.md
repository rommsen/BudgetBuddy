---
name: fsharp-property-tests
description: |
  Write property-based tests using FsCheck to verify properties hold for all inputs.
  Use when testing invariants, edge cases, or mathematical properties.
  Ensures comprehensive testing by generating random test cases automatically.
allowed-tools: Read, Edit, Write, Grep, Glob, Bash
standards:
  - standards/testing/property-tests.md
---

# Property-Based Testing with FsCheck

## When to Use This Skill

Activate when:
- Testing mathematical properties
- Verifying invariants hold for all inputs
- Finding edge cases automatically
- Testing serialization roundtrips
- User asks "how to test all cases"

## What Are Property Tests?

**Instead of testing specific examples:**
```fsharp
testCase "reverse twice is identity" <| fun () ->
    let input = [1; 2; 3]
    let result = input |> List.rev |> List.rev
    Expect.equal result input "Should be same"
```

**Test the property for ALL inputs:**
```fsharp
testProperty "reverse twice is identity" <| fun (xs: int list) ->
    let result = xs |> List.rev |> List.rev
    result = xs  // FsCheck generates 100+ test cases automatically
```

## Quick Start

### 1. Add FsCheck Package

```bash
dotnet add package FsCheck
dotnet add package FsCheck.Xunit  # or Expecto
```

### 2. Write Property Tests

```fsharp
module PropertyTests

open Expecto
open FsCheck

[<Tests>]
let tests = testList "Properties" [
    testProperty "adding 0 doesn't change value" <| fun (x: int) ->
        x + 0 = x

    testProperty "reverse twice is identity" <| fun (xs: int list) ->
        xs |> List.rev |> List.rev = xs

    testProperty "length after filter <= original length" <| fun (xs: int list) ->
        let filtered = xs |> List.filter (fun x -> x > 0)
        List.length filtered <= List.length xs
]
```

### 3. Test Roundtrip Properties

```fsharp
testProperty "JSON serialize/deserialize roundtrip" <| fun (item: Item) ->
    let json = JsonSerializer.Serialize(item)
    let deserialized = JsonSerializer.Deserialize<Item>(json)
    deserialized = item
```

### 4. Test Invariants

```fsharp
testProperty "sorted list is ordered" <| fun (xs: int list) ->
    let sorted = xs |> List.sort
    let rec isOrdered = function
        | [] | [_] -> true
        | x::y::rest -> x <= y && isOrdered (y::rest)
    isOrdered sorted
```

## Custom Generators

```fsharp
// Generate non-empty lists
let nonEmptyList<'T> =
    Arb.generate<'T list>
    |> Gen.suchThat (fun xs -> not (List.isEmpty xs))
    |> Arb.fromGen

// Use custom generator
testPropertyWithConfig
    { FsCheckConfig.defaultConfig with arbitrary = [typeof<NonEmptyListGen>] }
    "property on non-empty lists" <| fun (NonEmpty xs) ->
        List.head xs = List.item 0 xs
```

## Common Properties to Test

### Invariants
```fsharp
// Collection properties
testProperty "filter preserves order" <| fun (xs: int list) ->
    let filtered = List.filter (fun x -> x > 0) xs
    filtered = (xs |> List.filter (fun x -> x > 0))

// Bounds checking
testProperty "amount is always >= 0" <| fun (item: Item) ->
    Domain.processItem item
    |> fun result -> result.Amount >= 0m
```

### Roundtrips
```fsharp
// Encoding/Decoding
testProperty "encode -> decode is identity" <| fun (x: 'T) ->
    x |> encode |> decode = x

// Serialization
testProperty "save -> load roundtrip" <| fun (entity: Entity) ->
    entity |> Persistence.save |> Persistence.load = entity
```

### Commutativity
```fsharp
testProperty "addition is commutative" <| fun (x: int) (y: int) ->
    x + y = y + x
```

## Checklist

- [ ] **Read** `standards/testing/property-tests.md`
- [ ] FsCheck package added
- [ ] Properties test invariants (not examples)
- [ ] Properties hold for generated inputs
- [ ] Custom generators for constrained types
- [ ] Roundtrip properties for serialization
- [ ] `dotnet test` passes with 100+ generated cases

## Common Mistakes

❌ **Testing specific examples:**
```fsharp
testProperty "property" <| fun () ->
    let result = myFunction 5
    result = 10
```

✅ **Test for all inputs:**
```fsharp
testProperty "property" <| fun (x: int) ->
    let result = myFunction x
    result = x * 2
```

❌ **Not constraining generators:**
```fsharp
testProperty "division" <| fun (x: int) (y: int) ->
    x / y  // Fails when y = 0!
```

✅ **Use preconditions:**
```fsharp
testProperty "division" <| fun (x: int) (y: int) ->
    y <> 0 ==> lazy (x / y |> ignore; true)
```

## Related Skills

- **fsharp-tests** - Regular unit tests
- **fsharp-backend** - Testing domain logic

## Detailed Documentation

For complete property testing patterns:
- `standards/testing/property-tests.md` - Complete guide
- `standards/testing/domain-tests.md` - Testing strategies
