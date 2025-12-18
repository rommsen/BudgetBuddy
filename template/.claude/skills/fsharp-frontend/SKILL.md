---
name: fsharp-frontend
description: |
  Implement F# frontend using Elmish MVU architecture with Feliz for React components.
  Use when creating UI, managing client state, or building interactive features.
  Creates state management in src/Client/State.fs and UI in src/Client/View.fs.
---

# F# Frontend Implementation (Elmish + Feliz)

## When to Use This Skill

Activate when:
- User requests "add UI for X", "create component for Y"
- Implementing client-side functionality
- Managing application state

## MVU Architecture

```
View (user sees UI)
    ↓ (user action)
Msg (message describing action)
    ↓
Update (pure state transition)
    ↓ (optional)
Cmd (side effects like API calls)
    ↓
View (re-renders with new model)
```

## RemoteData Pattern

```fsharp
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string
```

## State Management (`State.fs`)

```fsharp
type Model = {
    Entities: RemoteData<Entity list>
    NewEntityName: string
}

type Msg =
    | LoadEntities
    | EntitiesLoaded of Result<Entity list, string>
    | UpdateNewEntityName of string
    | CreateEntity
    | EntityCreated of Result<Entity, string>

let init () : Model * Cmd<Msg> =
    { Entities = NotAsked; NewEntityName = "" }, Cmd.ofMsg LoadEntities

let update msg model =
    match msg with
    | LoadEntities ->
        { model with Entities = Loading },
        Cmd.OfAsync.either Api.api.getAll () (Ok >> EntitiesLoaded) (fun ex -> Error ex.Message |> EntitiesLoaded)

    | EntitiesLoaded (Ok entities) ->
        { model with Entities = Success entities }, Cmd.none

    | EntitiesLoaded (Error err) ->
        { model with Entities = Failure err }, Cmd.none

    | UpdateNewEntityName name ->
        { model with NewEntityName = name }, Cmd.none
```

## View Components (`View.fs`)

```fsharp
let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "container mx-auto p-4"
        prop.children [
            match model.Entities with
            | NotAsked -> Html.button [ prop.text "Load"; prop.onClick (fun _ -> dispatch LoadEntities) ]
            | Loading -> Html.span [ prop.className "loading loading-spinner" ]
            | Success entities ->
                Html.div [
                    for entity in entities ->
                        Html.div [ prop.key (string entity.Id); prop.text entity.Name ]
                ]
            | Failure err -> Html.div [ prop.className "alert alert-error"; prop.text err ]
        ]
    ]
```

## Common Patterns

### Form Input
```fsharp
Html.input [
    prop.value model.InputValue
    prop.onChange (UpdateInput >> dispatch)
]
```

### Button Click
```fsharp
Html.button [
    prop.onClick (fun _ -> dispatch SaveData)
    prop.text "Save"
]
```

## Verification Checklist

- [ ] RemoteData type defined
- [ ] Model defined in `src/Client/State.fs`
- [ ] Messages defined
- [ ] Update function handles all messages
- [ ] View components render all RemoteData states
