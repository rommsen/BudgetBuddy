module Client.DesignSystem.BottomSheet

open Feliz
open Fable.Core
open Fable.Core.JsInterop

[<Import("createPortal", from="react-dom")>]
let private createPortal (element: ReactElement) (container: Browser.Types.Element) : ReactElement = jsNative

let private renderToBody (element: ReactElement) =
    createPortal element Browser.Dom.document.body

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type BottomSheetProps = {
    IsOpen: bool
    OnClose: unit -> unit
    Title: string
    Subtitle: string option
    Footer: ReactElement list option
}

// Internal props for the React functional component
type private BottomSheetInternalProps = {
    Props: BottomSheetProps
    Children: ReactElement list
}

type private CategoryPickerInternalProps = {
    IsOpen: bool
    PayeeName: string
    Categories: (string * string) list
    SuggestedCategories: string list
    RecentCategories: string list
    OnSelect: string -> unit
    OnClose: unit -> unit
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/// Renders a section title label (e.g. "Vorgeschlagen", "Zuletzt verwendet")
let sectionTitle (title: string) : ReactElement =
    Html.div [
        prop.className "sheet-section-title"
        prop.text title
    ]

/// Renders a chip button used in suggestion / recent sections
let chipButton (text: string) (isSuggested: bool) (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.className (
            if isSuggested then "chip suggested"
            else "chip"
        )
        prop.onPointerDown (fun e ->
            e.preventDefault()
            e.stopPropagation()
            onClick()
        )
        prop.text text
    ]

// ---------------------------------------------------------------------------
// Generic BottomSheet view
// ---------------------------------------------------------------------------

[<ReactComponent>]
let private BottomSheetInternal (input: BottomSheetInternalProps) =
    let props = input.Props
    let children = input.Children

    let activeClass = if props.IsOpen then " active" else ""

    renderToBody (
        Html.div [
            prop.children [
                // Overlay
                Html.div [
                    prop.className ("overlay" + activeClass)
                    prop.onClick (fun e ->
                        e.stopPropagation()
                        props.OnClose()
                    )
                ]

                // Bottom sheet container
                Html.div [
                    prop.className ("bottom-sheet" + activeClass)
                    prop.onClick (fun e -> e.stopPropagation())
                    prop.children [
                        // Drag handle
                        Html.div [
                            prop.className "sheet-handle"
                        ]

                        // Header
                        Html.div [
                            prop.className "sheet-header"
                            prop.children [
                                Html.div [
                                    prop.children [
                                        Html.h3 [
                                            prop.text props.Title
                                        ]
                                        match props.Subtitle with
                                        | Some sub ->
                                            Html.div [
                                                prop.className "sheet-header-sub"
                                                prop.text sub
                                            ]
                                        | None -> ()
                                    ]
                                ]
                                Html.button [
                                    prop.className "sheet-close"
                                    prop.onClick (fun _ -> props.OnClose())
                                    prop.children [
                                        Svg.svg [
                                            svg.width 14
                                            svg.height 14
                                            svg.viewBox (0, 0, 24, 24)
                                            svg.custom ("fill", "none")
                                            svg.custom ("stroke", "currentColor")
                                            svg.custom ("strokeWidth", "2.5")
                                            svg.custom ("strokeLinecap", "round")
                                            svg.children [
                                                Svg.path [ svg.d "M18 6L6 18M6 6l12 12" ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        ]

                        // Scrollable body
                        Html.div [
                            prop.className "sheet-body"
                            prop.children children
                        ]

                        // Optional footer (outside sheet-body, stays fixed at bottom)
                        match props.Footer with
                        | Some footerChildren ->
                            Html.div [
                                prop.className "rule-sheet-footer"
                                prop.children footerChildren
                            ]
                        | None -> ()
                    ]
                ]
            ]
        ]
    )

/// Renders a bottom sheet with overlay, drag handle, header and scrollable body.
/// The sheet and overlay are always in the DOM; visibility is toggled via the `.active` CSS class.
let view (props: BottomSheetProps) (children: ReactElement list) : ReactElement =
    BottomSheetInternal { Props = props; Children = children }

// ---------------------------------------------------------------------------
// Category Picker (specialised bottom sheet)
// ---------------------------------------------------------------------------

[<ReactComponent>]
let private CategoryPickerInternal (input: CategoryPickerInternalProps) =
    let searchText, setSearchText = React.useState ""

    let activeClass = if input.IsOpen then " active" else ""

    // Wraps onSelect to also clear search text
    let selectAndClose catId =
        setSearchText ""
        input.OnSelect catId

    let closeAndClear () =
        setSearchText ""
        input.OnClose()

    // Group categories by YNAB group prefix "GroupName: CategoryName"
    let groupCategories (cats: (string * string) list) =
        cats
        |> List.groupBy (fun (_, name) ->
            match name.IndexOf(": ") with
            | -1 -> ""
            | idx -> name.Substring(0, idx).Trim()
        )

    // Filter categories by search text (case-insensitive)
    let filteredCategories =
        if System.String.IsNullOrWhiteSpace searchText then
            input.Categories
        else
            let searchLower = searchText.ToLowerInvariant()
            input.Categories
            |> List.filter (fun (_, name) ->
                name.ToLowerInvariant().Contains(searchLower))

    let groupedCategories = groupCategories filteredCategories

    // Find category name by id for chip display
    let categoryNameById (catId: string) =
        input.Categories
        |> List.tryFind (fun (id, _) -> id = catId)
        |> Option.map snd
        |> Option.defaultValue catId

    // Extract display name (part after "GroupName: " or full name)
    let displayName (name: string) =
        match name.IndexOf(": ") with
        | -1 -> name
        | idx -> name.Substring(idx + 2).Trim()

    renderToBody (
        Html.div [
            prop.children [
                // Overlay
                Html.div [
                    prop.className ("overlay" + activeClass)
                    prop.onClick (fun e ->
                        e.stopPropagation()
                        closeAndClear()
                    )
                ]

                // Bottom sheet
                Html.div [
                    prop.className ("bottom-sheet" + activeClass)
                    prop.onClick (fun e -> e.stopPropagation())
                    prop.children [
                        // Drag handle
                        Html.div [ prop.className "sheet-handle" ]

                        // Header
                        Html.div [
                            prop.className "sheet-header"
                            prop.children [
                                Html.h3 [ prop.text "Kategorie wählen" ]
                                if input.PayeeName <> "" then
                                    Html.p [ prop.text input.PayeeName ]
                            ]
                        ]

                        // Body - only mount content when open so autoFocus works on each open
                        Html.div [
                            prop.className "sheet-body"
                            prop.children [
                                if input.IsOpen then
                                    // Search input (first, with autoFocus)
                                    Html.div [
                                        prop.className "sheet-search"
                                        prop.children [
                                            Html.input [
                                                prop.type' "text"
                                                prop.placeholder "Kategorie suchen\u2026"
                                                prop.value searchText
                                                prop.onChange setSearchText
                                                prop.autoFocus true
                                                prop.className "text-base"
                                            ]
                                        ]
                                    ]

                                    // Suggested section
                                    if not input.SuggestedCategories.IsEmpty && System.String.IsNullOrWhiteSpace searchText then
                                        Html.div [
                                            prop.className "sheet-section"
                                            prop.children [
                                                sectionTitle "Vorgeschlagen"
                                                Html.div [
                                                    prop.className "suggestion-chips"
                                                    prop.children [
                                                        for i, catId in input.SuggestedCategories |> List.indexed do
                                                            chipButton
                                                                (categoryNameById catId)
                                                                (i = 0)
                                                                (fun () -> selectAndClose catId)
                                                    ]
                                                ]
                                            ]
                                        ]

                                    // Recent section
                                    if not input.RecentCategories.IsEmpty && System.String.IsNullOrWhiteSpace searchText then
                                        Html.div [
                                            prop.className "sheet-section"
                                            prop.children [
                                                sectionTitle "Zuletzt verwendet"
                                                Html.div [
                                                    prop.className "suggestion-chips"
                                                    prop.children [
                                                        for catId in input.RecentCategories do
                                                            chipButton
                                                                (categoryNameById catId)
                                                                false
                                                                (fun () -> selectAndClose catId)
                                                    ]
                                                ]
                                            ]
                                        ]

                                    // All categories (grouped)
                                    Html.div [
                                        prop.className "sheet-section"
                                        prop.children [
                                            sectionTitle "Alle Kategorien"

                                            for (groupName, items) in groupedCategories do
                                                Html.div [
                                                    prop.className "category-group"
                                                    prop.children [
                                                        if groupName <> "" then
                                                            Html.div [
                                                                prop.className "category-parent"
                                                                prop.text groupName
                                                            ]
                                                        for (catId, catName) in items do
                                                            Html.div [
                                                                prop.className "category-item"
                                                                prop.onPointerDown (fun e ->
                                                                    e.preventDefault()
                                                                    selectAndClose catId)
                                                                prop.text (displayName catName)
                                                            ]
                                                    ]
                                                ]
                                        ]
                                    ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    )

/// Specialised category picker bottom sheet.
/// Categories are `(categoryId, categoryName)` pairs. Names containing "/" are grouped
/// by the prefix before "/" (e.g. "Fixkosten / Miete" groups under "Fixkosten").
let categoryPicker
    (isOpen: bool)
    (payeeName: string)
    (categories: (string * string) list)
    (suggestedCategories: string list)
    (recentCategories: string list)
    (onSelect: string -> unit)
    (onClose: unit -> unit)
    : ReactElement =
    CategoryPickerInternal {
        IsOpen = isOpen
        PayeeName = payeeName
        Categories = categories
        SuggestedCategories = suggestedCategories
        RecentCategories = recentCategories
        OnSelect = onSelect
        OnClose = onClose
    }
