---
name: fsharp-routing
description: |
  Implement URL routing for Elmish applications using Elmish.UrlParser or Feliz.Router.
  Use when adding navigation, page switching, or URL-based state.
  Ensures proper URL handling and browser history integration.
allowed-tools: Read, Edit, Write, Grep, Glob
standards:
  - standards/frontend/routing.md
---

# URL Routing for Elmish

## When to Use This Skill

Activate when:
- Adding navigation to multi-page app
- Implementing URL-based routing
- Need browser back/forward support
- User asks "how to add pages/routes"
- Project needs URL parameter parsing

## Routing Approach

We use **Feliz.Router** for simple, functional routing:

```fsharp
// URL → Route (discriminated union)
type Route =
    | Home
    | Items
    | ItemDetail of id:int
    | NotFound
```

## Quick Start

### 1. Define Routes

```fsharp
// In State.fs
type Route =
    | Home
    | Items
    | ItemDetail of id:int
    | Settings
    | NotFound

// Parse URL segments to Route
let parseRoute = function
    | [] -> Home
    | [ "items" ] -> Items
    | [ "items"; Route.Int id ] -> ItemDetail id
    | [ "settings" ] -> Settings
    | _ -> NotFound
```

### 2. Add to Model

```fsharp
type Model = {
    CurrentRoute: Route
    Items: RemoteData<Item list>
    // ... other state
}
```

### 3. Handle in Update

```fsharp
type Msg =
    | UrlChanged of Route
    | NavigateTo of Route
    // ... other messages

let update msg model =
    match msg with
    | UrlChanged route ->
        // Load data for new route
        match route with
        | ItemDetail id ->
            { model with CurrentRoute = route },
            Cmd.OfAsync.either Api.api.getItem id (Ok >> ItemLoaded) (Error >> ItemLoaded)
        | _ ->
            { model with CurrentRoute = route }, Cmd.none

    | NavigateTo route ->
        model, Navigation.newUrl (routeToUrl route)
```

### 4. Render Route in View

```fsharp
let view (model: Model) dispatch =
    Primitives.container [
        Navigation.view model.CurrentRoute (NavigateTo >> dispatch)

        match model.CurrentRoute with
        | Home -> renderHomePage model dispatch
        | Items -> renderItemsPage model dispatch
        | ItemDetail id -> renderItemDetailPage id model dispatch
        | Settings -> renderSettingsPage model dispatch
        | NotFound -> renderNotFoundPage()
    ]
```

## Helper Functions

```fsharp
// Convert Route to URL string
let routeToUrl = function
    | Home -> "/"
    | Items -> "/items"
    | ItemDetail id -> $"/items/{id}"
    | Settings -> "/settings"
    | NotFound -> "/404"

// Navigate programmatically
let navigateTo route dispatch =
    dispatch (NavigateTo route)
```

## Integration with Elmish

```fsharp
// In Program.fs
open Elmish.Navigation
open Elmish.UrlParser

Program.mkProgram init update view
|> Program.toNavigable parseUrl (UrlChanged)
|> Program.withReactSynchronous "app"
|> Program.run
```

## Checklist

- [ ] **Read** `standards/frontend/routing.md`
- [ ] Route discriminated union defined
- [ ] parseRoute function implemented
- [ ] CurrentRoute in Model
- [ ] UrlChanged and NavigateTo messages
- [ ] View matches on CurrentRoute
- [ ] Program.toNavigable configured
- [ ] Navigation component in UI

## Common Mistakes

❌ **Forgetting to parse URL segments:**
```fsharp
// BAD - hardcoded route
let parseRoute segments = Home
```

✅ **Pattern match on segments:**
```fsharp
let parseRoute = function
    | [] -> Home
    | [ "items" ] -> Items
    | [ "items"; Route.Int id ] -> ItemDetail id
```

❌ **Not loading data on route change:**
```fsharp
| UrlChanged route ->
    { model with CurrentRoute = route }, Cmd.none  // Missing data load!
```

✅ **Load data for new route:**
```fsharp
| UrlChanged (ItemDetail id) ->
    { model with CurrentRoute = ItemDetail id },
    Cmd.OfAsync.either Api.api.getItem id (Ok >> ItemLoaded) (Error >> ItemLoaded)
```

## Related Skills

- **fsharp-frontend** - Complete frontend patterns
- **fsharp-feature** - Full-stack workflow

## Detailed Documentation

For complete routing patterns:
- `standards/frontend/routing.md` - Complete routing guide
- `standards/frontend/state-management.md` - State integration
- `standards/frontend/view-patterns.md` - Navigation components
