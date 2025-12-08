module Client.DesignSystem.Input

open Feliz
open Client.DesignSystem.Tokens

// ============================================
// Input Types
// ============================================

/// Input size variants
type InputSize =
    | Small   // Compact
    | Medium  // Default (48px min-height on mobile)
    | Large   // Larger for emphasis

/// Input validation state
type InputState =
    | Normal
    | Error of string
    | Success

// ============================================
// Common Styling
// ============================================

let private baseInputClass =
    "w-full bg-[#252836] text-base-content border border-white/10 rounded-lg transition-all duration-200 focus:border-neon-teal focus:shadow-[0_0_0_2px_rgba(0,212,170,0.3)] focus:outline-none placeholder:text-base-content/50"

let private sizeToClass = function
    | Small -> "text-sm px-3 py-2 min-h-[36px]"
    | Medium -> "text-base px-4 py-3 min-h-[48px] md:min-h-[44px]"
    | Large -> "text-lg px-4 py-3.5 min-h-[52px]"

let private stateToClass = function
    | Normal -> ""
    | Error _ -> "border-neon-red focus:border-neon-red focus:shadow-[0_0_0_2px_rgba(255,59,92,0.3)] animate-shake"
    | Success -> "border-neon-green focus:border-neon-green focus:shadow-[0_0_0_2px_rgba(0,255,136,0.3)]"

// ============================================
// Text Input
// ============================================

type TextInputProps = {
    Value: string
    OnChange: string -> unit
    Placeholder: string
    Size: InputSize
    State: InputState
    Disabled: bool
    Type: string
    ClassName: string option
}

let textInputDefaults = {
    Value = ""
    OnChange = ignore
    Placeholder = ""
    Size = Medium
    State = Normal
    Disabled = false
    Type = "text"
    ClassName = None
}

/// Text input field
let text (props: TextInputProps) =
    let sizeClass = sizeToClass props.Size
    let stateClass = stateToClass props.State
    let extraClass = props.ClassName |> Option.defaultValue ""
    let disabledClass = if props.Disabled then "opacity-50 cursor-not-allowed" else ""

    Html.input [
        prop.type' props.Type
        prop.className $"{baseInputClass} {sizeClass} {stateClass} {disabledClass} {extraClass}"
        prop.value props.Value
        prop.onChange props.OnChange
        prop.placeholder props.Placeholder
        prop.disabled props.Disabled
        // Prevent iOS zoom on focus
        prop.style [ style.fontSize 16 ]
    ]

/// Simple text input
let textSimple value onChange placeholder =
    text { textInputDefaults with Value = value; OnChange = onChange; Placeholder = placeholder }

/// Password input
let password value onChange placeholder =
    text { textInputDefaults with Value = value; OnChange = onChange; Placeholder = placeholder; Type = "password" }

/// Email input
let email value onChange placeholder =
    text { textInputDefaults with Value = value; OnChange = onChange; Placeholder = placeholder; Type = "email" }

/// Number input
let number value onChange placeholder =
    text { textInputDefaults with Value = value; OnChange = onChange; Placeholder = placeholder; Type = "number" }

// ============================================
// Textarea
// ============================================

type TextareaProps = {
    Value: string
    OnChange: string -> unit
    Placeholder: string
    Rows: int
    State: InputState
    Disabled: bool
    ClassName: string option
}

let textareaDefaults = {
    Value = ""
    OnChange = ignore
    Placeholder = ""
    Rows = 3
    State = Normal
    Disabled = false
    ClassName = None
}

/// Textarea field
let textarea (props: TextareaProps) =
    let stateClass = stateToClass props.State
    let extraClass = props.ClassName |> Option.defaultValue ""
    let disabledClass = if props.Disabled then "opacity-50 cursor-not-allowed" else ""

    Html.textarea [
        prop.className $"{baseInputClass} text-base px-4 py-3 resize-y {stateClass} {disabledClass} {extraClass}"
        prop.value props.Value
        prop.onChange props.OnChange
        prop.placeholder props.Placeholder
        prop.rows props.Rows
        prop.disabled props.Disabled
        prop.style [ style.fontSize 16 ]
    ]

/// Simple textarea
let textareaSimple value onChange placeholder =
    textarea { textareaDefaults with Value = value; OnChange = onChange; Placeholder = placeholder }

// ============================================
// Select
// ============================================

type SelectOption = {
    Value: string
    Label: string
    Disabled: bool
}

type SelectProps = {
    Value: string
    OnChange: string -> unit
    Options: SelectOption list
    Placeholder: string option
    Size: InputSize
    State: InputState
    Disabled: bool
    ClassName: string option
}

let selectDefaults = {
    Value = ""
    OnChange = ignore
    Options = []
    Placeholder = None
    Size = Medium
    State = Normal
    Disabled = false
    ClassName = None
}

/// Select dropdown
let select (props: SelectProps) =
    let sizeClass = sizeToClass props.Size
    let stateClass = stateToClass props.State
    let extraClass = props.ClassName |> Option.defaultValue ""
    let disabledClass = if props.Disabled then "opacity-50 cursor-not-allowed" else ""

    let selectArrowClass = "appearance-none bg-no-repeat bg-[right_0.75rem_center] bg-[length:1.25rem] pr-10"

    Html.select [
        prop.className $"{baseInputClass} {sizeClass} {stateClass} {disabledClass} {extraClass} {selectArrowClass} select-arrow"
        prop.value props.Value
        prop.onChange props.OnChange
        prop.disabled props.Disabled
        prop.children [
            match props.Placeholder with
            | Some ph ->
                Html.option [
                    prop.value ""
                    prop.disabled true
                    prop.text ph
                ]
            | None -> ()

            for opt in props.Options do
                Html.option [
                    prop.value opt.Value
                    prop.disabled opt.Disabled
                    prop.text opt.Label
                ]
        ]
    ]

/// Simple select with string options
let selectSimple value onChange (options: (string * string) list) =
    select {
        selectDefaults with
            Value = value
            OnChange = onChange
            Options = options |> List.map (fun (v, l) -> { Value = v; Label = l; Disabled = false })
    }

/// Select with placeholder
let selectWithPlaceholder value onChange placeholder (options: (string * string) list) =
    select {
        selectDefaults with
            Value = value
            OnChange = onChange
            Placeholder = Some placeholder
            Options = options |> List.map (fun (v, l) -> { Value = v; Label = l; Disabled = false })
    }

// ============================================
// Checkbox
// ============================================

/// Checkbox input
let checkbox (isChecked: bool) (onChange: bool -> unit) (label: string) =
    Html.label [
        prop.className "flex items-center gap-3 cursor-pointer group"
        prop.children [
            Html.input [
                prop.type' "checkbox"
                prop.className "checkbox checkbox-sm border-white/20 [--chkbg:theme(colors.neon-teal)] [--chkfg:theme(colors.base-100)] checked:border-neon-teal focus:ring-neon-teal/30"
                prop.isChecked isChecked
                prop.onChange (fun (e: Browser.Types.Event) ->
                    let target = e.target :?> Browser.Types.HTMLInputElement
                    onChange target.``checked``
                )
            ]
            Html.span [
                prop.className "text-base-content group-hover:text-base-content/90 transition-colors"
                prop.text label
            ]
        ]
    ]

/// Checkbox without label
let checkboxSimple (isChecked: bool) (onChange: bool -> unit) =
    Html.input [
        prop.type' "checkbox"
        prop.className "checkbox checkbox-sm border-white/20 [--chkbg:theme(colors.neon-teal)] [--chkfg:theme(colors.base-100)] checked:border-neon-teal"
        prop.isChecked isChecked
        prop.onChange (fun (e: Browser.Types.Event) ->
            let target = e.target :?> Browser.Types.HTMLInputElement
            onChange target.``checked``
        )
    ]

// ============================================
// Toggle/Switch
// ============================================

/// Toggle switch
let toggle (isChecked: bool) (onChange: bool -> unit) (label: string option) =
    Html.label [
        prop.className "flex items-center gap-3 cursor-pointer"
        prop.children [
            Html.input [
                prop.type' "checkbox"
                prop.className "toggle toggle-sm [--tglbg:theme(colors.base-300)] bg-base-content/20 border-base-content/20 checked:bg-neon-teal checked:border-neon-teal checked:[--tglbg:theme(colors.base-100)]"
                prop.isChecked isChecked
                prop.onChange (fun (e: Browser.Types.Event) ->
                    let target = e.target :?> Browser.Types.HTMLInputElement
                    onChange target.``checked``
                )
            ]
            match label with
            | Some l ->
                Html.span [
                    prop.className "text-base-content"
                    prop.text l
                ]
            | None -> ()
        ]
    ]

/// Toggle without label
let toggleSimple (isChecked: bool) (onChange: bool -> unit) =
    toggle isChecked onChange None

// ============================================
// Input Group (Label + Input + Error)
// ============================================

type InputGroupProps = {
    Label: string
    Required: bool
    Error: string option
    HelpText: string option
    Children: ReactElement
}

/// Input group with label, input, and optional error
let group (props: InputGroupProps) =
    Html.div [
        prop.className "space-y-1.5"
        prop.children [
            // Label
            Html.label [
                prop.className "block text-sm font-medium text-base-content/80"
                prop.children [
                    Html.text props.Label
                    if props.Required then
                        Html.span [
                            prop.className "text-neon-red ml-0.5"
                            prop.text "*"
                        ]
                ]
            ]

            // Input
            props.Children

            // Help text
            match props.HelpText, props.Error with
            | _, Some err ->
                Html.p [
                    prop.className "text-xs text-neon-red"
                    prop.text err
                ]
            | Some help, None ->
                Html.p [
                    prop.className "text-xs text-base-content/50"
                    prop.text help
                ]
            | None, None -> ()
        ]
    ]

/// Simple input group
let groupSimple label children =
    group {
        Label = label
        Required = false
        Error = None
        HelpText = None
        Children = children
    }

/// Input group with required marker
let groupRequired label children =
    group {
        Label = label
        Required = true
        Error = None
        HelpText = None
        Children = children
    }

/// Input group with error
let groupWithError label error children =
    group {
        Label = label
        Required = false
        Error = error
        HelpText = None
        Children = children
    }

// ============================================
// Form Sections
// ============================================

/// Horizontal form row (label and input side by side on desktop)
let formRow (label: string) (children: ReactElement) =
    Html.div [
        prop.className "flex flex-col md:flex-row md:items-center gap-2 md:gap-4"
        prop.children [
            Html.label [
                prop.className "text-sm font-medium text-base-content/80 md:w-1/3 md:text-right"
                prop.text label
            ]
            Html.div [
                prop.className "md:w-2/3"
                prop.children [ children ]
            ]
        ]
    ]

/// Form section with title
let formSection (title: string) (children: ReactElement list) =
    Html.div [
        prop.className "space-y-4"
        prop.children [
            Html.h3 [
                prop.className "text-lg font-semibold font-display text-base-content border-b border-white/5 pb-2"
                prop.text title
            ]
            Html.div [
                prop.className "space-y-4"
                prop.children children
            ]
        ]
    ]

// ============================================
// Searchable Select (Combobox)
// ============================================

type SearchableSelectProps = {
    Value: string
    OnChange: string -> unit
    Options: (string * string) list  // (value, label) pairs
    Placeholder: string
    Size: InputSize
    Disabled: bool
}

let searchableSelectDefaults = {
    Value = ""
    OnChange = ignore
    Options = []
    Placeholder = "Select..."
    Size = Medium
    Disabled = false
}

/// Searchable select dropdown with filter functionality and keyboard navigation
[<ReactComponent>]
let SearchableSelect (props: SearchableSelectProps) =
    let isOpen, setIsOpen = React.useState false
    let searchText, setSearchText = React.useState ""
    let highlightedIndex, setHighlightedIndex = React.useState -1
    let isKeyboardNav, setIsKeyboardNav = React.useState false  // Track if navigation is via keyboard
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None
    let inputRef = React.useRef<Browser.Types.HTMLInputElement option> None
    let listRef = React.useRef<Browser.Types.HTMLElement option> None

    // Find current label for display
    let currentLabel =
        props.Options
        |> List.tryFind (fun (v, _) -> v = props.Value)
        |> Option.map snd
        |> Option.defaultValue ""

    // Filter options based on search (case-insensitive contains)
    let filteredOptions =
        if System.String.IsNullOrWhiteSpace searchText then
            props.Options
        else
            let searchLower = searchText.ToLowerInvariant()
            props.Options
            |> List.filter (fun (_, label) ->
                label.ToLowerInvariant().Contains searchLower)

    // Total items: 1 (placeholder/clear) + filtered options
    let totalItems = 1 + filteredOptions.Length

    // Reset highlighted index when search changes
    React.useEffect (fun () ->
        setHighlightedIndex -1
    , [| searchText :> obj |])

    // Close dropdown when clicking outside
    React.useEffect (fun () ->
        let handleClickOutside (e: Browser.Types.Event) =
            match containerRef.current with
            | Some container ->
                let target = e.target :?> Browser.Types.HTMLElement
                if not (container.contains target) then
                    setIsOpen false
                    setSearchText ""
                    setHighlightedIndex -1
            | None -> ()

        Browser.Dom.document.addEventListener("mousedown", handleClickOutside)
        { new System.IDisposable with
            member _.Dispose() =
                Browser.Dom.document.removeEventListener("mousedown", handleClickOutside)
        }
    , [| isOpen :> obj |])

    // Focus input when dropdown opens
    React.useEffect (fun () ->
        if isOpen then
            match inputRef.current with
            | Some input -> input.focus()
            | None -> ()
            setHighlightedIndex -1
    , [| isOpen :> obj |])

    // Scroll highlighted item into view - ONLY for keyboard navigation
    // This prevents the page/modal from scrolling when using mouse
    React.useEffect (fun () ->
        if highlightedIndex >= 0 && isKeyboardNav then
            match listRef.current with
            | Some list ->
                let items = list.querySelectorAll("[data-option-index]")
                if highlightedIndex < int items.length then
                    let item = items.[highlightedIndex] :?> Browser.Types.HTMLElement
                    // Manual scroll within list container only
                    let itemTop = item.offsetTop
                    let itemHeight = item.offsetHeight
                    let listScrollTop = list.scrollTop
                    let listHeight = list.clientHeight

                    // Scroll up if item is above visible area
                    if itemTop < listScrollTop then
                        list.scrollTop <- itemTop
                    // Scroll down if item is below visible area
                    elif itemTop + itemHeight > listScrollTop + listHeight then
                        list.scrollTop <- itemTop + itemHeight - listHeight
            | None -> ()
    , [| highlightedIndex :> obj; isKeyboardNav :> obj |])

    let selectOption index =
        if index = 0 then
            // Placeholder = clear selection
            props.OnChange ""
        elif index > 0 && index <= filteredOptions.Length then
            let value, _ = filteredOptions.[index - 1]
            props.OnChange value
        setIsOpen false
        setSearchText ""
        setHighlightedIndex -1
        setIsKeyboardNav false

    let handleKeyDown (e: Browser.Types.KeyboardEvent) =
        match e.key with
        | "Escape" ->
            e.preventDefault()
            setIsOpen false
            setSearchText ""
            setHighlightedIndex -1
            setIsKeyboardNav false
        | "ArrowDown" ->
            e.preventDefault()
            setIsKeyboardNav true  // Mark as keyboard navigation
            let nextIndex =
                if highlightedIndex < totalItems - 1 then highlightedIndex + 1
                else 0  // Wrap to top
            setHighlightedIndex nextIndex
        | "ArrowUp" ->
            e.preventDefault()
            setIsKeyboardNav true  // Mark as keyboard navigation
            let nextIndex =
                if highlightedIndex > 0 then highlightedIndex - 1
                else totalItems - 1  // Wrap to bottom
            setHighlightedIndex nextIndex
        | "Enter" ->
            e.preventDefault()
            if highlightedIndex >= 0 then
                selectOption highlightedIndex
            elif filteredOptions.Length = 1 then
                // Auto-select single match
                selectOption 1
        | "Tab" ->
            // Close on tab
            setIsOpen false
            setSearchText ""
            setHighlightedIndex -1
            setIsKeyboardNav false
        | _ -> ()

    // Helper to set highlight from mouse without triggering scroll
    let setHighlightFromMouse index =
        setIsKeyboardNav false
        setHighlightedIndex index

    let sizeClass = sizeToClass props.Size
    let disabledClass = if props.Disabled then "opacity-50 cursor-not-allowed" else ""

    Html.div [
        prop.className "relative"
        prop.ref containerRef
        prop.children [
            // Display button (shows current selection or placeholder)
            Html.button [
                prop.type' "button"
                prop.className $"{baseInputClass} {sizeClass} {disabledClass} text-left flex items-center justify-between gap-2 cursor-pointer"
                prop.disabled props.Disabled
                prop.onClick (fun e ->
                    e.preventDefault()
                    if not props.Disabled then
                        setIsOpen (not isOpen)
                        setSearchText ""
                )
                prop.onKeyDown (fun e ->
                    // Open dropdown on arrow keys when closed
                    if not isOpen && (e.key = "ArrowDown" || e.key = "ArrowUp" || e.key = "Enter") then
                        e.preventDefault()
                        setIsOpen true
                )
                prop.children [
                    Html.span [
                        prop.className (if props.Value = "" then "text-base-content/50" else "text-base-content truncate")
                        prop.text (if props.Value = "" then props.Placeholder else currentLabel)
                    ]
                    // Chevron icon
                    let rotateClass = if isOpen then "rotate-180" else ""
                    Svg.svg [
                        svg.className $"w-4 h-4 text-base-content/50 flex-shrink-0 transition-transform {rotateClass}"
                        svg.fill "none"
                        svg.viewBox (0, 0, 24, 24)
                        svg.stroke "currentColor"
                        svg.custom ("strokeWidth", "2")
                        svg.children [
                            Svg.path [
                                svg.custom ("strokeLinecap", "round")
                                svg.custom ("strokeLinejoin", "round")
                                svg.d "M19 9l-7 7-7-7"
                            ]
                        ]
                    ]
                ]
            ]

            // Dropdown
            if isOpen then
                Html.div [
                    prop.className "absolute z-50 w-full mt-1 bg-[#252836] border border-white/10 rounded-lg shadow-xl overflow-hidden"
                    prop.children [
                        // Search input
                        Html.div [
                            prop.className "p-2 border-b border-white/10"
                            prop.children [
                                Html.input [
                                    prop.ref inputRef
                                    prop.type' "text"
                                    prop.className "w-full bg-base-100 text-base-content border border-white/10 rounded-md px-3 py-2 text-sm focus:border-neon-teal focus:outline-none placeholder:text-base-content/50"
                                    prop.placeholder "Type to search..."
                                    prop.value searchText
                                    prop.autoFocus true
                                    prop.onChange setSearchText
                                    prop.onKeyDown handleKeyDown
                                    prop.style [ style.fontSize 16 ]
                                ]
                            ]
                        ]

                        // Options list
                        Html.div [
                            prop.className "max-h-60 overflow-y-auto"
                            prop.ref listRef
                            prop.children [
                                // Empty option (clear selection) - index 0
                                let clearHighlighted = highlightedIndex = 0
                                let clearClass =
                                    if clearHighlighted then
                                        "w-full text-left px-3 py-2 text-sm italic bg-neon-teal/20 text-neon-teal"
                                    else
                                        "w-full text-left px-3 py-2 text-sm text-base-content/50 hover:bg-neon-teal/10 hover:text-neon-teal transition-colors italic"
                                Html.button [
                                    prop.type' "button"
                                    prop.className clearClass
                                    prop.custom ("data-option-index", "0")
                                    prop.onClick (fun _ -> selectOption 0)
                                    prop.onMouseEnter (fun _ -> setHighlightFromMouse 0)
                                    prop.text props.Placeholder
                                ]

                                if filteredOptions.IsEmpty then
                                    Html.div [
                                        prop.className "px-3 py-4 text-sm text-base-content/50 text-center"
                                        prop.text "No matches found"
                                    ]
                                else
                                    for i, (value, label) in filteredOptions |> List.indexed do
                                        let optionIndex = i + 1  // +1 because 0 is the clear option
                                        let isSelected = value = props.Value
                                        let isHighlighted = highlightedIndex = optionIndex
                                        let optionClass =
                                            if isHighlighted then
                                                "w-full text-left px-3 py-2 text-sm transition-colors bg-neon-teal/20 text-neon-teal"
                                            elif isSelected then
                                                "w-full text-left px-3 py-2 text-sm transition-colors bg-neon-teal/10 text-neon-teal"
                                            else
                                                "w-full text-left px-3 py-2 text-sm transition-colors text-base-content hover:bg-neon-teal/10 hover:text-neon-teal"
                                        Html.button [
                                            prop.type' "button"
                                            prop.className optionClass
                                            prop.custom ("data-option-index", string optionIndex)
                                            prop.onClick (fun _ -> selectOption optionIndex)
                                            prop.onMouseEnter (fun _ -> setHighlightFromMouse optionIndex)
                                            prop.children [
                                                Html.span [
                                                    prop.className "truncate block"
                                                    prop.text label
                                                ]
                                            ]
                                        ]
                            ]
                        ]
                    ]
                ]
        ]
    ]

/// Simple searchable select with placeholder
let searchableSelect value onChange placeholder (options: (string * string) list) =
    SearchableSelect {
        searchableSelectDefaults with
            Value = value
            OnChange = onChange
            Placeholder = placeholder
            Options = options
    }
