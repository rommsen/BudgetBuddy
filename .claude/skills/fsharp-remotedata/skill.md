---
name: fsharp-remotedata
description: |
  Implement RemoteData pattern for explicit async state handling in Elmish frontend.
  Use when adding API calls, form submissions, or any async operation.
  Ensures all loading/error states are handled explicitly, preventing impossible UI states.
allowed-tools: Read, Edit, Write, Grep, Glob
standards:
  - standards/frontend/remotedata.md
---

# RemoteData Pattern for Async State

## When to Use This Skill

Activate when:
- Adding API calls to frontend
- Implementing form submissions
- Need to show loading/error states
- User asks "how to handle async data"
- Project has Elmish MVU frontend

## What is RemoteData?

Makes impossible states unrepresentable:

```fsharp
type RemoteData<'T> =
    | NotAsked      // Initial state
    | Loading       // Request in flight
    | Success of 'T // Data received
    | Failure of string // Error message
```

**Instead of:**
```fsharp
// ❌ Can represent impossible states
type Model = {
    Data: Item list option
    IsLoading: bool
    Error: string option
}
// What if IsLoading=true AND Error is Some?
```

## Quick Start

### 1. Add to Model

```fsharp
type Model = {
    Items: RemoteData<Item list>
    SelectedItem: RemoteData<Item>
}
```

### 2. Add Messages

```fsharp
type Msg =
    | LoadItems
    | ItemsLoaded of Result<Item list, string>
```

### 3. Update Pattern

```fsharp
let update msg model =
    match msg with
    | LoadItems ->
        { model with Items = Loading },
        Cmd.OfAsync.either
            Api.api.getItems ()
            (Ok >> ItemsLoaded)
            (fun ex -> Error ex.Message |> ItemsLoaded)

    | ItemsLoaded (Ok items) ->
        { model with Items = Success items }, Cmd.none

    | ItemsLoaded (Error err) ->
        { model with Items = Failure err }, Cmd.none
```

### 4. View Pattern

```fsharp
match model.Items with
| NotAsked -> Html.button [ prop.onClick (fun _ -> dispatch LoadItems); prop.text "Load" ]
| Loading -> Loading.spinner MD Teal
| Success items -> renderItems items dispatch
| Failure err -> ErrorDisplay.cardCompact err (Some (fun () -> dispatch LoadItems))
```

## Helper Functions

For detailed helpers, see `standards/frontend/remotedata.md`

**Most Used:**
```fsharp
RemoteData.map (fun x -> x + 1) rd          // Transform Success value
RemoteData.withDefault [] model.Items       // Extract with fallback
RemoteData.isLoading model.Items            // Check state
RemoteData.fromResult (Ok 42)               // Convert Result
```

## Integration with Design System

Use Design System components for rendering:

```fsharp
match model.Data with
| NotAsked -> Button.primary "Load Data" (fun () -> dispatch Load)
| Loading -> Loading.centered (Loading.spinner LG Teal) "Loading..."
| Success data -> renderData data
| Failure err -> ErrorDisplay.card "Failed to load" (Some (fun () -> dispatch Load))
```

## Checklist

- [ ] **Read** `standards/frontend/remotedata.md`
- [ ] Model uses RemoteData types (not bool + option)
- [ ] Update sets Loading before async
- [ ] View handles all 4 states:
  - [ ] NotAsked → Action button
  - [ ] Loading → Spinner
  - [ ] Success → Data
  - [ ] Failure → Error + Retry
- [ ] Used Design System components

## Common Mistakes

❌ **Missing states in view:**
```fsharp
// BAD - only handles Success
match model.Items with
| Success items -> renderItems items
| _ -> Html.none
```

✅ **Handle all states:**
```fsharp
match model.Items with
| NotAsked -> Button.primary "Load" ...
| Loading -> Loading.spinner ...
| Success items -> renderItems items
| Failure err -> ErrorDisplay.card err ...
```

## Related Skills

- **fsharp-frontend** - Complete frontend patterns
- **fsharp-feature** - Full-stack feature workflow

## Detailed Documentation

For helper functions and advanced patterns:
- `standards/frontend/remotedata.md` - Complete reference
- `standards/frontend/state-management.md` - Update patterns
- `standards/frontend/view-patterns.md` - Rendering patterns
