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
    /// Renders overlay/sheet on the elevated z-layer so the picker can open
    /// on top of another bottom sheet (e.g. the Quick Add form)
    Elevated: bool
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

/// Renders a chip button used in suggestion / recent sections.
/// Selection commits on the real `click` — committing on pointerdown lets the
/// browser's synthetic click fall through to whatever sits behind the sheet
/// once it closes (the mobile "ghost click"). preventDefault on mousedown
/// keeps the search input focused so the keyboard doesn't shift the layout
/// between tap and click.
let chipButton (text: string) (isSuggested: bool) (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.className (
            if isSuggested then "chip suggested"
            else "chip"
        )
        prop.onMouseDown (fun e -> e.preventDefault())
        prop.onClick (fun e ->
            e.stopPropagation()
            onClick()
        )
        prop.text text
    ]

let private closeIcon : ReactElement =
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

/// Locks page scrolling while the sheet is open (restores on close/unmount).
let private useBodyScrollLock (isOpen: bool) =
    React.useEffect (
        (fun () ->
            if isOpen then Viewport.lockBodyScroll ()
            React.createDisposable (fun () -> if isOpen then Viewport.unlockBodyScroll ())),
        [| box isOpen |]
    )

// ---------------------------------------------------------------------------
// Generic BottomSheet view
// ---------------------------------------------------------------------------

[<ReactComponent>]
let private BottomSheetInternal (input: BottomSheetInternalProps) =
    let props = input.Props
    let children = input.Children

    let activeClass = if props.IsOpen then " active" else ""

    useBodyScrollLock props.IsOpen

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
                                    prop.ariaLabel "Schließen"
                                    prop.onClick (fun _ -> props.OnClose())
                                    prop.children [ closeIcon ]
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

    let layerClass = if input.Elevated then " layer-2" else ""
    let activeClass = if input.IsOpen then " active" else ""

    useBodyScrollLock input.IsOpen

    // Wraps onSelect: clear search, arm the ghost-click guard (the sheet is
    // about to close — any late synthetic click must not reach the list
    // behind it), give light haptic feedback where supported.
    let selectAndClose catId =
        Viewport.swallowNextClick 350
        Viewport.vibrate 8
        setSearchText ""
        input.OnSelect catId

    let closeAndClear () =
        Viewport.swallowNextClick 350
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
                    prop.className ("overlay" + layerClass + activeClass)
                    prop.onClick (fun e ->
                        e.stopPropagation()
                        closeAndClear()
                    )
                ]

                // Bottom sheet
                Html.div [
                    prop.className ("bottom-sheet" + layerClass + activeClass)
                    prop.onClick (fun e -> e.stopPropagation())
                    prop.children [
                        // Drag handle
                        Html.div [ prop.className "sheet-handle" ]

                        // Header (with explicit close button — the grabber
                        // alone is not a discoverable/accessible dismiss)
                        Html.div [
                            prop.className "sheet-header"
                            prop.children [
                                Html.div [
                                    prop.children [
                                        Html.h3 [ prop.text "Kategorie wählen" ]
                                        if input.PayeeName <> "" then
                                            Html.p [ prop.text input.PayeeName ]
                                    ]
                                ]
                                Html.button [
                                    prop.className "sheet-close"
                                    prop.ariaLabel "Schließen"
                                    prop.onClick (fun _ -> closeAndClear())
                                    prop.children [ closeIcon ]
                                ]
                            ]
                        ]

                        // Search — pinned outside the scrollable body so it
                        // stays visible above the keyboard while the list
                        // scrolls underneath. Only mounted while open so
                        // autoFocus re-triggers on each open.
                        if input.IsOpen then
                            Html.div [
                                prop.className "sheet-search"
                                prop.children [
                                    Html.input [
                                        prop.type' "text"
                                        prop.placeholder "Kategorie suchen…"
                                        prop.value searchText
                                        prop.onChange setSearchText
                                        // Autofocus only on mouse/trackpad devices:
                                        // on touch it would summon the keyboard
                                        // before the user even sees the suggestions.
                                        prop.autoFocus (Viewport.isFinePointer())
                                        prop.className "text-base"
                                    ]
                                ]
                            ]

                        // Body - only mount content when open
                        Html.div [
                            prop.className "sheet-body"
                            prop.children [
                                if input.IsOpen then
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
                                                                // Keep focus (and keyboard) on the search
                                                                // input — prevents a blur-induced layout
                                                                // shift between tap and click.
                                                                prop.onMouseDown (fun e -> e.preventDefault())
                                                                prop.onClick (fun e ->
                                                                    e.stopPropagation()
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
        Elevated = false
    }

/// Category picker on the elevated z-layer — for opening on top of another
/// bottom sheet (e.g. from the Quick Add form).
let categoryPickerLayered
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
        Elevated = true
    }
