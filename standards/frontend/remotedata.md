# RemoteData Pattern

> Explicit async state handling for frontend.

## Overview

The RemoteData pattern makes impossible states unrepresentable and forces explicit handling of loading and error states. Use this for ALL async operations in frontend state.

## When to Use This

- API calls
- Async data loading
- Form submissions
- Any operation with loading/error states

## Pattern

### Type Definition

```fsharp
// src/Client/Types.fs
type RemoteData<'T> =
    | NotAsked      // Initial state
    | Loading       // Request in flight
    | Success of 'T // Data received
    | Failure of string // Error message
```

### Helper Functions

```fsharp
module RemoteData =

    // Transformations
    let map (f: 'T -> 'U) (rd: RemoteData<'T>) : RemoteData<'U> =
        match rd with
        | NotAsked -> NotAsked
        | Loading -> Loading
        | Success value -> Success (f value)
        | Failure err -> Failure err

    let bind (f: 'T -> RemoteData<'U>) (rd: RemoteData<'T>) : RemoteData<'U> =
        match rd with
        | Success value -> f value
        | NotAsked -> NotAsked
        | Loading -> Loading
        | Failure err -> Failure err

    // State checks
    let isNotAsked rd = match rd with NotAsked -> true | _ -> false
    let isLoading rd = match rd with Loading -> true | _ -> false
    let isSuccess rd = match rd with Success _ -> true | _ -> false
    let isFailure rd = match rd with Failure _ -> true | _ -> false

    // Extraction
    let withDefault defaultValue rd =
        match rd with
        | Success value -> value
        | _ -> defaultValue

    let toOption rd =
        match rd with
        | Success value -> Some value
        | _ -> None

    // Error handling
    let recover defaultValue rd =
        match rd with
        | Failure _ -> Success defaultValue
        | other -> other

    let mapError (f: string -> string) rd =
        match rd with
        | Failure err -> Failure (f err)
        | other -> other

    // Combining
    let map2 (f: 'T1 -> 'T2 -> 'U) (rd1: RemoteData<'T1>) (rd2: RemoteData<'T2>) =
        match rd1, rd2 with
        | Success v1, Success v2 -> Success (f v1 v2)
        | Failure err, _ | _, Failure err -> Failure err
        | Loading, _ | _, Loading -> Loading
        | _ -> NotAsked

    // Conversion
    let fromResult result =
        match result with
        | Ok value -> Success value
        | Error err -> Failure err

    let fromOption option =
        match option with
        | Some value -> Success value
        | None -> NotAsked
```

## Usage in State

### Model

```fsharp
type Model = {
    Items: RemoteData<Item list>
    SelectedItem: RemoteData<Item>
}

let init () =
    { Items = NotAsked; SelectedItem = NotAsked }, Cmd.none
```

### Update

```fsharp
type Msg =
    | LoadItems
    | ItemsLoaded of Result<Item list, string>

let update msg model =
    match msg with
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
        { model with Items = Failure err }, Cmd.none
```

### View

```fsharp
let view model dispatch =
    match model.Items with
    | NotAsked ->
        Html.button [
            prop.onClick (fun _ -> dispatch LoadItems)
            prop.text "Load Items"
        ]

    | Loading ->
        Html.div [ prop.className "loading loading-spinner" ]

    | Success items ->
        Html.div [
            for item in items ->
                Html.div [ prop.key (string item.Id); prop.text item.Name ]
        ]

    | Failure error ->
        Html.div [
            prop.className "alert alert-error"
            prop.children [
                Html.span error
                Html.button [
                    prop.onClick (fun _ -> dispatch LoadItems)
                    prop.text "Retry"
                ]
            ]
        ]
```

## Helper Usage Examples

```fsharp
// Transform data
let itemCount = RemoteData.map List.length model.Items  // RemoteData<int>

// Extract with default
let items = RemoteData.withDefault [] model.Items  // Item list

// Check state
if RemoteData.isLoading model.Items then
    showSpinner()

// Convert from Result
let rd = RemoteData.fromResult (Ok 42)  // Success 42

// Combine two RemoteData
let combined =
    RemoteData.map2 (fun items user -> items, user)
        model.Items
        model.CurrentUser
```

## Anti-Patterns

### ❌ Not Using RemoteData

```fsharp
// BAD
type Model = {
    Items: Item list option
    IsLoading: bool
    Error: string option
}

// GOOD
type Model = {
    Items: RemoteData<Item list>
}
```

### ❌ Ignoring States

```fsharp
// BAD
match model.Items with
| Success items -> renderItems items
| _ -> Html.none

// GOOD
match model.Items with
| NotAsked -> Html.button "Load"
| Loading -> Html.div "Loading..."
| Success items -> renderItems items
| Failure err -> Html.div err
```

## Checklist

- [ ] All async operations use RemoteData
- [ ] Model has RemoteData types
- [ ] Update sets Loading before async call
- [ ] View handles all 4 states
- [ ] NotAsked shows action button
- [ ] Loading shows spinner
- [ ] Success shows data
- [ ] Failure shows error + retry

## See Also

- `state-management.md` - Update patterns
- `view-patterns.md` - Rendering
- `../global/learnings.md` - Best practices
