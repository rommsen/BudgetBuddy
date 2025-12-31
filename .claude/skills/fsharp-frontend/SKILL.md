---
name: fsharp-frontend
description: |
  Implement F# frontend using Elmish.React + Feliz with MVU architecture.
  Use when implementing UI components, state management, or user interactions.
  Ensures proper separation: Model (state) → Update (logic) → View (UI).
  Creates code in src/Client/ files: State.fs, View.fs.
allowed-tools: Read, Edit, Write, Grep, Glob, Bash
standards:
  required-reading:
    - standards/frontend/overview.md
  workflow:
    - step: 1
      file: standards/frontend/state-management.md
      purpose: Model, Msg, init, update
      output: src/Client/State.fs
    - step: 2
      file: standards/frontend/view-patterns.md
      purpose: Feliz components
      output: src/Client/View.fs
    - step: 3
      file: standards/frontend/remotedata.md
      purpose: Async state handling
---

# F# Frontend Implementation

## When to Use This Skill

Activate when:
- User requests "implement frontend for X"
- Need to add UI components
- Implementing user interactions
- Adding state management
- Project has src/Client/ directory with Elmish

## Architecture Overview

```
View (Feliz)              ← src/Client/View.fs
    ↓ dispatches
Msg (Actions)
    ↓ processed by
Update (State logic)      ← src/Client/State.fs
    ↓ updates
Model (State)
```

## Implementation Workflow

### Step 1: Define State

**Read:** `standards/frontend/state-management.md`
**Edit:** `src/Client/State.fs`

```fsharp
module State

// Model - Application state
type Model = {
    Items: RemoteData<Item list>
    SelectedId: int option
}

// Msg - All possible actions
type Msg =
    | LoadItems
    | ItemsLoaded of Result<Item list, string>
    | SelectItem of int

// Init - Initial state
let init () : Model * Cmd<Msg> =
    { Items = NotAsked; SelectedId = None },
    Cmd.none

// Update - State transitions
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | LoadItems ->
        { model with Items = Loading },
        Cmd.OfAsync.either
            Api.api.getItems ()
            (Ok >> ItemsLoaded)
            (Error >> ItemsLoaded)

    | ItemsLoaded (Ok items) ->
        { model with Items = Success items }, Cmd.none

    | ItemsLoaded (Error err) ->
        { model with Items = Failure err }, Cmd.none

    | SelectItem id ->
        { model with SelectedId = Some id }, Cmd.none
```

**Key:** Use `RemoteData<'T>` for all async operations

---

### Step 2: Implement View

**Read:** `standards/frontend/view-patterns.md`
**Edit:** `src/Client/View.fs`

```fsharp
module View

open Feliz
open Client.DesignSystem

let private renderItem (item: Item) dispatch =
    Card.standard [
        Card.headerSimple item.Name
        Card.body [
            Html.p [ prop.text $"ID: {item.Id}" ]
            Button.primary "Select" (fun () -> dispatch (SelectItem item.Id))
        ]
    ]

let private renderItems items dispatch =
    Html.div [
        prop.className "grid grid-cols-1 md:grid-cols-2 gap-4"
        prop.children (items |> List.map (fun i -> renderItem i dispatch))
    ]

let view (model: Model) dispatch =
    Primitives.container [
        PageHeader.simple "Items"

        match model.Items with
        | NotAsked ->
            Button.primary "Load Items" (fun () -> dispatch LoadItems)

        | Loading ->
            Loading.centered (Loading.spinner LG Teal) "Loading items..."

        | Success items ->
            renderItems items dispatch

        | Failure err ->
            ErrorDisplay.card "Failed to load items" (Some (fun () -> dispatch LoadItems))
    ]
```

**Key:** Always use Design System components

---

### Step 3: Handle Async State

**Read:** `standards/frontend/remotedata.md`
**Pattern:** Already applied in Step 1

```fsharp
// Model uses RemoteData
type Model = { Data: RemoteData<'T> }

// Update sets Loading before async
| Load ->
    { model with Data = Loading }, Cmd.OfAsync...

// View handles all 4 states
match model.Data with
| NotAsked -> "action button"
| Loading -> "spinner"
| Success data -> "render data"
| Failure err -> "error + retry"
```

---

## Quick Reference

### Standard MVU Pattern

```fsharp
type Model = { Data: RemoteData<'T> }
type Msg = Load | Loaded of Result<'T, string>

let update msg model =
    match msg with
    | Load -> { model with Data = Loading }, Cmd.OfAsync.either api () (Ok >> Loaded) (Error >> Loaded)
    | Loaded (Ok x) -> { model with Data = Success x }, Cmd.none
    | Loaded (Error e) -> { model with Data = Failure e }, Cmd.none
```

### Design System Components

```fsharp
// Always use Design System, not raw Feliz
Button.primary "Save" onClick         // ✅
PageHeader.simple "Title"            // ✅
Card.standard [...]                  // ✅
ErrorDisplay.card err retry          // ✅
Loading.spinner MD Teal              // ✅

Html.button [...]                    // ❌
Html.div [ className "..." ]         // ❌ (unless complex custom)
```

## Verification Checklist

- [ ] **Read workflow standards** (step 1-3 above)
- [ ] Model in `src/Client/State.fs`
- [ ] Msg discriminated union for all actions
- [ ] init function returns Model * Cmd<Msg>
- [ ] update function pure (returns new model)
- [ ] View in `src/Client/View.fs`
- [ ] Uses Design System components
- [ ] RemoteData for async operations
- [ ] All 4 RemoteData states handled in view
- [ ] `dotnet build` succeeds
- [ ] UI tested manually

## Common Pitfalls

See `standards/global/anti-patterns.md` for full list.

**Most Critical:**
- ❌ Mutating model in update
- ❌ Side effects in update (use Cmd instead)
- ❌ Not handling all RemoteData states
- ❌ Using raw Feliz instead of Design System
- ✅ Pure update function
- ✅ Design System for all UI

## Related Skills

- **fsharp-remotedata** - Async state patterns
- **fsharp-feature** - Full-stack workflow
- **fsharp-backend** - API implementation

## Detailed Documentation

For in-depth patterns and examples, refer to:
- `standards/frontend/overview.md` - Architecture
- `standards/frontend/state-management.md` - MVU patterns
- `standards/frontend/view-patterns.md` - Feliz components
- `standards/frontend/remotedata.md` - Async state
- `standards/frontend/routing.md` - Navigation
