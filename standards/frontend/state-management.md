# State Management

> Model, Msg, and update patterns for Elmish.

## Overview

State management in Elmish uses the MVU pattern. All state lives in the Model, changes happen through Messages, and the update function is the single point of state transition.

## When to Use This

- Defining application state
- Handling user actions
- Managing async operations
- Coordinating side effects

## Patterns

### Model Definition

```fsharp
// src/Client/State.fs
module State

open Elmish
open Types

type Model = {
    // Page state
    CurrentPage: Page

    // Data from API
    Items: RemoteData<Item list>
    SelectedItem: RemoteData<Item>

    // Form state
    FormInput: string
    FormErrors: Map<string, string>

    // UI state
    IsModalOpen: bool
    Toast: Toast option
}
```

**Key Principles**:
- Keep model flat and simple
- No derived data (compute in view)
- Use RemoteData for async operations
- Separate concerns: page, data, forms, UI

### Message Types

```fsharp
type Msg =
    // Navigation
    | NavigateTo of Page

    // API Calls - Request/Response pattern
    | LoadItems
    | ItemsLoaded of Result<Item list, string>

    | SaveItem of Item
    | ItemSaved of Result<Item, string>

    // User Input
    | FormInputChanged of string
    | FormSubmitted

    // UI Events
    | OpenModal
    | CloseModal
```

**Naming Conventions**:
- Present tense for actions: `LoadItems`, `SaveItem`
- Past tense for results: `ItemsLoaded`, `ItemSaved`
- Include type in Result: `Result<Data, string>`

### Init Function

```fsharp
let init () : Model * Cmd<Msg> =
    let initialModel = {
        CurrentPage = HomePage
        Items = NotAsked
        SelectedItem = NotAsked
        FormInput = ""
        FormErrors = Map.empty
        IsModalOpen = false
        Toast = None
    }

    let initialCmd = Cmd.batch [
        Cmd.ofMsg LoadItems  // Load data on startup
    ]

    initialModel, initialCmd
```

### Update Function

```fsharp
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    // Loading items
    | LoadItems ->
        { model with Items = Loading },
        Cmd.OfAsync.either
            Api.api.getItems
            ()
            (Ok >> ItemsLoaded)
            (fun ex -> Error ex.Message |> ItemsLoaded)

    | ItemsLoaded (Ok items) ->
        { model with Items = Success items }, Cmd.none

    | ItemsLoaded (Error err) ->
        { model with Items = Failure err },
        Cmd.ofMsg (ShowToast { Message = err; Type = ToastError })

    // Saving item
    | SaveItem item ->
        model,
        Cmd.OfAsync.either
            Api.api.saveItem
            item
            (Ok >> ItemSaved)
            (fun ex -> Error ex.Message |> ItemSaved)

    | ItemSaved (Ok savedItem) ->
        let updatedItems =
            match model.Items with
            | Success items ->
                Success (items |> List.map (fun i ->
                    if i.Id = savedItem.Id then savedItem else i
                ))
            | other -> other

        { model with Items = updatedItems },
        Cmd.batch [
            Cmd.ofMsg CloseModal
            Cmd.ofMsg (ShowToast { Message = "Saved!"; Type = ToastSuccess })
        ]

    // Form handling
    | FormInputChanged value ->
        { model with FormInput = value }, Cmd.none

    | FormSubmitted ->
        if String.IsNullOrWhiteSpace model.FormInput then
            { model with FormErrors = Map.add "input" "Cannot be empty" Map.empty },
            Cmd.none
        else
            { model with FormErrors = Map.empty },
            Cmd.ofMsg (SaveItem { Id = 0; Name = model.FormInput })
```

## Update Patterns

1. **Immediate state changes** - Return new model + `Cmd.none`
2. **Async operations** - Set Loading + return Cmd
3. **Success handling** - Update model + optional commands
4. **Error handling** - Set Failure + show toast
5. **Batch commands** - Use `Cmd.batch`

## Optimistic UI Updates

```fsharp
| SaveItem item ->
    // Update UI immediately
    let updatedItems =
        model.Items
        |> RemoteData.map (fun items ->
            items |> List.map (fun i -> if i.Id = item.Id then item else i))

    { model with Items = updatedItems },
    Cmd.OfAsync.either Api.api.saveItem item (Ok >> ItemSaved) (Error >> ItemSaved)

| ItemSaved (Error err) ->
    // Revert on error
    model,
    Cmd.batch [
        Cmd.ofMsg LoadItems  // Reload from server
        Cmd.ofMsg (ShowToast { Message = err; Type = ToastError })
    ]
```

## Version-Based Debouncing

```fsharp
type Model = {
    PendingVersions: Map<ItemId, int>
}

| InputChanged (id, value) ->
    let version = model.PendingVersions.TryFind id |> Option.defaultValue 0 |> (+) 1
    { model with PendingVersions = model.PendingVersions.Add(id, version) },
    Cmd.OfFunc.delayed 400 (fun () -> CommitChange (id, value, version))

| CommitChange (id, value, version) when model.PendingVersions.[id] = version ->
    // Only commit if this is the latest version
    model, Cmd.ofMsg (SaveChange (id, value))
```

## Checklist

- [ ] Model contains all state
- [ ] Messages for all actions
- [ ] Init returns model + cmd
- [ ] Update handles all messages
- [ ] RemoteData for async
- [ ] Cmd for side effects
- [ ] Form errors in Map
- [ ] Loading states explicit

## See Also

- `overview.md` - MVU architecture
- `view-patterns.md` - Rendering UI
- `remotedata.md` - Async data handling
