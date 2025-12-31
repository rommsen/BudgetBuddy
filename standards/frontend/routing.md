# Routing

> Feliz.Router patterns for client-side navigation.

## Overview

Feliz.Router provides type-safe URL routing for single-page applications. Define pages as discriminated unions and parse URLs to messages.

## When to Use This

- Multi-page applications
- URL-based navigation
- Deep linking
- Browser history

## Patterns

### Page Definition

```fsharp
// src/Client/Types.fs
type Page =
    | HomePage
    | DetailPage of itemId: int
    | SettingsPage
    | NotFound
```

### URL Parsing

```fsharp
// src/Client/State.fs
open Feliz.Router

let parseUrl (segments: string list) : Page =
    match segments with
    | [] -> HomePage
    | [ "item"; Route.Int itemId ] -> DetailPage itemId
    | [ "settings" ] -> SettingsPage
    | _ -> NotFound
```

### State Integration

```fsharp
// Add to Model
type Model = {
    CurrentPage: Page
    // ... other fields
}

// Add to Msg
type Msg =
    | UrlChanged of string list
    | NavigateTo of Page
    // ... other messages

// Update URL parsing
let update msg model =
    match msg with
    | UrlChanged segments ->
        let page = parseUrl segments
        { model with CurrentPage = page }, Cmd.none

    | NavigateTo page ->
        let segments =
            match page with
            | HomePage -> []
            | DetailPage itemId -> [ "item"; string itemId ]
            | SettingsPage -> [ "settings" ]
            | NotFound -> [ "not-found" ]

        model, Cmd.navigatePath segments

// Update init to handle initial URL
let init () : Model * Cmd<Msg> =
    let initialModel = { CurrentPage = HomePage; (* ... *) }
    let initialCmd = Cmd.batch [
        Cmd.ofMsg (UrlChanged (Router.currentPath()))
        // ... other commands
    ]
    initialModel, initialCmd
```

### App.fs Router Setup

```fsharp
// src/Client/App.fs
module App

open Elmish
open Elmish.React
open Elmish.HMR
open Feliz.Router

Program.mkProgram State.init State.update View.view
|> Program.toNavigable (Router.currentPath >> State.UrlChanged) State.update
|> Program.withReactSynchronous "root"
|> Program.withDebugger
|> Program.run
```

### View Routing

```fsharp
// src/Client/View.fs
let view model dispatch =
    let pageView =
        match model.CurrentPage with
        | HomePage -> Pages.homePage model dispatch
        | DetailPage itemId -> Pages.detailPage itemId model dispatch
        | SettingsPage -> Pages.settingsPage model dispatch
        | NotFound -> Pages.notFoundPage

    Html.div [
        // Navigation bar
        Html.div [
            prop.className "navbar"
            prop.children [
                Html.a [
                    prop.onClick (fun _ -> dispatch (NavigateTo HomePage))
                    prop.text "Home"
                ]
                Html.a [
                    prop.onClick (fun _ -> dispatch (NavigateTo SettingsPage))
                    prop.text "Settings"
                ]
            ]
        ]

        // Main content
        Html.div [
            prop.className "container mx-auto"
            prop.children [ pageView ]
        ]
    ]
```

### Navigation Patterns

```fsharp
// Programmatic navigation
dispatch (NavigateTo (DetailPage 123))

// Link with onClick
Html.a [
    prop.onClick (fun e ->
        e.preventDefault()
        dispatch (NavigateTo HomePage)
    )
    prop.text "Go Home"
]

// Back navigation
Html.button [
    prop.onClick (fun _ -> dispatch (NavigateTo HomePage))
    prop.text "← Back"
]
```

### Complex URL Parsing

```fsharp
let parseUrl segments =
    match segments with
    | [] -> HomePage

    | [ "items" ] -> ItemsPage { Filter = None; Page = 1 }
    | [ "items"; "page"; Route.Int page ] -> ItemsPage { Filter = None; Page = page }
    | [ "items"; "filter"; filter ] -> ItemsPage { Filter = Some filter; Page = 1 }

    | [ "item"; Route.Int id ] -> DetailPage id
    | [ "item"; Route.Int id; "edit" ] -> EditPage id

    | [ "user"; Route.Guid userId ] -> UserPage userId

    | _ -> NotFound
```

### Query Parameters

```fsharp
open Feliz.Router

// Parse query string
let parseQueryParams () =
    Router.currentPath()
    |> Router.parseQueryString
    |> Map.ofList

// Get specific param
let getQueryParam key =
    parseQueryParams()
    |> Map.tryFind key

// Usage
let searchTerm = getQueryParam "q" |> Option.defaultValue ""
let page = getQueryParam "page" |> Option.bind Int32.TryParse |> Option.defaultValue 1
```

## Checklist

- [ ] Page type defined
- [ ] URL parser implemented
- [ ] Router integrated in App.fs
- [ ] Navigation commands work
- [ ] Initial URL handled
- [ ] 404 page for unknown routes
- [ ] Browser back/forward works
- [ ] Deep linking works

## See Also

- `state-management.md` - Msg and update
- `view-patterns.md` - Page views
- `overview.md` - MVU architecture
