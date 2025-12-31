# Frontend Overview

> Elmish MVU architecture with Feliz for F# web frontends.

## Overview

The Model-View-Update (MVU) architecture ensures predictable state management through unidirectional data flow. Elmish provides the MVU implementation, and Feliz provides type-safe React bindings.

## When to Use This

- Understanding MVU architecture
- Starting a new frontend
- Managing application state
- Rendering UI components

## MVU Pattern

```
┌──────────────────────────────────────┐
│                                      │
│  User Interaction                    │
│         │                            │
│         ▼                            │
│    ┌────────┐      ┌──────────┐    │
│    │  Msg   │─────▶│  Update  │    │
│    └────────┘      └──────────┘    │
│         ▲               │           │
│         │               ▼           │
│    ┌────────┐      ┌──────────┐    │
│    │  View  │◀─────│  Model   │    │
│    └────────┘      └──────────┘    │
│                                      │
└──────────────────────────────────────┘
```

## Core Concepts

### Model
Application state - single source of truth

```fsharp
type Model = {
    Items: RemoteData<Item list>
    SelectedItem: RemoteData<Item>
    IsModalOpen: bool
}
```

### Messages
Events that change state

```fsharp
type Msg =
    | LoadItems
    | ItemsLoaded of Result<Item list, string>
    | OpenModal
```

### Update
Pure function: `(Msg, Model) → (Model, Cmd<Msg>)`

```fsharp
let update msg model =
    match msg with
    | LoadItems ->
        { model with Items = Loading },
        Cmd.OfAsync.either Api.getItems () (Ok >> ItemsLoaded) (Error >> ItemsLoaded)
    | ItemsLoaded (Ok items) ->
        { model with Items = Success items }, Cmd.none
```

### View
Pure function: `(Model, Dispatch) → ReactElement`

```fsharp
let view model dispatch =
    Html.div [
        match model.Items with
        | Loading -> Html.div "Loading..."
        | Success items -> renderItems items dispatch
        | Failure err -> Html.div err
    ]
```

## Key Principles

1. **Unidirectional Data Flow** - Model → View → Msg → Update → Model
2. **Pure Functions** - Update and View are pure
3. **Immutable State** - Model never mutates
4. **Explicit State** - All state in Model
5. **Side Effects via Cmd** - No I/O in update or view

## File Structure

```
src/Client/
├── App.fs           # Main Elmish app
├── Types.fs         # Client-only types
├── State.fs         # Model, Msg, init, update
├── View.fs          # UI components
└── Api.fs           # Fable.Remoting client
```

## Checklist

- [ ] Model defined with all state
- [ ] Messages for all user actions
- [ ] Init function returns model + cmd
- [ ] Update handles all messages
- [ ] View is pure function
- [ ] Cmd for side effects
- [ ] RemoteData for async operations

## See Also

- `state-management.md` - Model, Msg, update patterns
- `view-patterns.md` - Feliz component patterns
- `remotedata.md` - Async data handling
