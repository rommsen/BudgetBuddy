# View Patterns

> Feliz component patterns for F# React frontends.

## Overview

Feliz provides type-safe React bindings for F#. Views are pure functions that render the Model using the Feliz DSL.

## When to Use This

- Building UI components
- Rendering lists
- Handling user input
- Conditional rendering

## Patterns

### Component Organization

```fsharp
// src/Client/View.fs
module View

open Feliz
open State
open Types

// Reusable Components
module Components =

    let spinner =
        Html.div [ prop.className "loading loading-spinner loading-lg" ]

    let button text onClick extraClasses =
        Html.button [
            prop.className $"btn {extraClasses}"
            prop.onClick (fun _ -> onClick())
            prop.text text
        ]

    let primaryButton text onClick =
        button text onClick "btn-primary"

    let card title content =
        Html.div [
            prop.className "card bg-base-100 shadow-xl"
            prop.children [
                Html.div [
                    prop.className "card-body"
                    prop.children [
                        Html.h2 [ prop.className "card-title"; prop.text title ]
                        yield! content
                    ]
                ]
            ]
        ]

// Page Views
module Pages =

    let homePage model dispatch =
        Html.div [
            prop.className "space-y-4"
            prop.children [
                Html.h1 [ prop.className "text-4xl font-bold"; prop.text "Home" ]
                Components.primaryButton "Load Items" (fun () -> dispatch LoadItems)
                // ... more content
            ]
        ]

// Main View
let view model dispatch =
    Html.div [
        prop.className "min-h-screen bg-base-100"
        prop.children [
            // Navigation
            Html.div [ prop.className "navbar bg-base-300"; (* ... *) ]
            // Main content
            Html.div [
                prop.className "container mx-auto p-4"
                prop.children [ (* page view *) ]
            ]
        ]
    ]
```

### List Rendering with Keys

```fsharp
// ALWAYS add prop.key to list items
Html.ul [
    for item in items do
        Html.li [
            prop.key (string item.Id)  // Critical for React!
            prop.text item.Name
        ]
]

// For options in select
for option in options do
    Html.option [
        prop.key option.Value
        prop.value option.Value
        prop.text option.Label
    ]
```

### Conditional Rendering

```fsharp
// Using match
match model.IsLoggedIn with
| true -> Html.div "Welcome!"
| false -> Html.div "Please log in"

// Using if/then
if model.Count > 10 then
    Html.div "High"
else
    Html.div "Low"

// Using Html.none for nothing
if model.ShowWarning then
    Html.div [ prop.className "alert"; prop.text "Warning!" ]
else
    Html.none

// Conditional classes
Html.div [
    prop.className "btn"
    prop.classes [
        "btn-primary", model.IsPrimary
        "btn-disabled", not model.IsEnabled
    ]
]
```

### Event Handling

```fsharp
// Button click
Html.button [
    prop.onClick (fun _ -> dispatch ButtonClicked)
    prop.text "Click me"
]

// Input change
Html.input [
    prop.value model.Input
    prop.onChange (fun (value: string) -> dispatch (InputChanged value))
]

// Form submit
Html.form [
    prop.onSubmit (fun e ->
        e.preventDefault()
        dispatch FormSubmitted
    )
    prop.children [ (* form fields *) ]
]

// Keyboard events
Html.input [
    prop.onKeyDown (fun e ->
        if e.key = "Enter" then
            dispatch EnterPressed
    )
]
```

### RemoteData Rendering

```fsharp
let remoteDataView remoteData successView =
    match remoteData with
    | NotAsked -> Html.div "Not loaded"
    | Loading -> Html.div [ prop.className "loading loading-spinner" ]
    | Success data -> successView data
    | Failure err ->
        Html.div [
            prop.className "alert alert-error"
            prop.text err
        ]

// Usage
remoteDataView model.Items (fun items ->
    Html.div [
        for item in items ->
            Html.div [ prop.key (string item.Id); prop.text item.Name ]
    ]
)
```

### Modal Pattern

```fsharp
let modal isOpen onClose content =
    Html.div [
        prop.className "modal"
        prop.classes [ "modal-open", isOpen ]
        prop.children [
            Html.div [
                prop.className "modal-box"
                prop.children content
            ]
            Html.div [
                prop.className "modal-backdrop"
                prop.onClick (fun _ -> onClose())
            ]
        ]
    ]

// Usage
modal model.IsModalOpen (fun () -> dispatch CloseModal) [
    Html.h3 "Edit Item"
    // ... form fields
]
```

### Toast Notification

```fsharp
let toastView toast dispatch =
    match toast with
    | None -> Html.none
    | Some toast ->
        let alertClass =
            match toast.Type with
            | ToastSuccess -> "alert-success"
            | ToastError -> "alert-error"
            | ToastInfo -> "alert-info"

        Html.div [
            prop.className "toast toast-top toast-end"
            prop.children [
                Html.div [
                    prop.className $"alert {alertClass}"
                    prop.children [
                        Html.span toast.Message
                        Html.button [
                            prop.className "btn btn-sm btn-ghost"
                            prop.onClick (fun _ -> dispatch DismissToast)
                            prop.text "✕"
                        ]
                    ]
                ]
            ]
        ]
```

## Performance Tips

### Keep Model Lean

```fsharp
// BAD - Derived data in model
type Model = {
    Items: Item list
    FilteredItems: Item list  // Derived!
}

// GOOD - Compute in view
type Model = {
    Items: Item list
    Filter: string
}

// In view:
let filteredItems =
    model.Items
    |> List.filter (fun item -> item.Name.Contains(model.Filter))
```

### Debounce User Input

```fsharp
| InputChanged value ->
    { model with SearchInput = value },
    Cmd.OfAsync.perform
        (fun () -> async {
            do! Async.Sleep 300
            return value
        })
        ()
        DebouncedSearch
```

## Checklist

- [ ] View is pure function
- [ ] prop.key on all list items
- [ ] Event handlers dispatch messages
- [ ] No I/O in view
- [ ] Conditional rendering with match/if
- [ ] RemoteData handled explicitly
- [ ] Mobile-friendly (no hover-only)

## See Also

- `state-management.md` - Model and messages
- `remotedata.md` - Async data handling
- `routing.md` - Navigation
