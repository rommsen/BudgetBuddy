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
    "input-field"

/// Schriftgröße kompakter Suchfelder: 16px unterhalb `md` (iOS-Zoom), 14px ab `md`
/// (Styleguide §7.1).
let searchFieldTextClass = "text-base md:text-sm"

/// Klassen je Größe. Öffentlich, damit die Tests sie prüfen können.
let sizeToClass = function
    | Small -> $"{searchFieldTextClass} px-3 py-2 min-h-[36px]"
    | Medium -> "text-base px-4 py-3 min-h-[48px] md:min-h-[44px]"
    | Large -> "text-lg px-4 py-3.5 min-h-[52px]"

let private stateToClass = function
    | Normal -> ""
    | Error _ -> "border-error focus:border-error focus:shadow-[0_0_0_2px_color-mix(in_srgb,var(--color-error)_30%,transparent)] animate-shake"
    | Success -> "border-success focus:border-success focus:shadow-[0_0_0_2px_color-mix(in_srgb,var(--color-success)_30%,transparent)]"

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
    /// Bildschirmtastatur am Handy, zum Beispiel "decimal" für Beträge. `None` lässt sie offen.
    InputMode: string option
    /// Name für den Screenreader, wenn kein sichtbares Label dasteht.
    AriaLabel: string option
    ClassName: string option
    /// Nimmt keine Eingabe an, behält aber den Fokus (anders als `Disabled`).
    ReadOnly: bool
    OnFocus: (Browser.Types.FocusEvent -> unit) option
    OnBlur: (Browser.Types.FocusEvent -> unit) option
    OnKeyDown: (Browser.Types.KeyboardEvent -> unit) option
    /// Die Id eines Hinweises unter dem Feld (`aria-describedby`), etwa "Ungültige Rechnung"
    /// (budgeting-g2ty9, F5).
    DescribedBy: string option
    /// Die Eingabe gilt nicht (`aria-invalid`). Die rote Umrandung setzt weiter `State` oder die Klasse.
    AriaInvalid: bool
}

let textInputDefaults = {
    Value = ""
    OnChange = ignore
    Placeholder = ""
    Size = Medium
    State = Normal
    Disabled = false
    Type = "text"
    InputMode = None
    AriaLabel = None
    ClassName = None
    ReadOnly = false
    OnFocus = None
    OnBlur = None
    OnKeyDown = None
    DescribedBy = None
    AriaInvalid = false
}

/// Setzt `text` das Inline-`fontSize 16` (iOS-Zoom-Schutz)? Nur Medium. Small trägt
/// `text-base md:text-sm` über Klassen, Large `text-lg`, beides ab 16 px; ein Inline-Style würde
/// die Tailwind-Größe überstimmen.
let usesInlineFontSize (size: InputSize) : bool =
    match size with
    | Small | Large -> false
    | Medium -> true

/// Text input field
let text (props: TextInputProps) =
    let sizeClass = sizeToClass props.Size
    let stateClass = stateToClass props.State
    let extraClass = props.ClassName |> Option.defaultValue ""
    let disabledClass = if props.Disabled then "opacity-50 cursor-not-allowed" else ""
    // Schreibgeschützt behält den Fokus, sieht aber nicht bedienbar aus.
    let readOnlyClass = if props.ReadOnly then "opacity-60 cursor-default" else ""

    Html.input [
        prop.type' props.Type
        prop.className $"{baseInputClass} {sizeClass} {stateClass} {disabledClass} {readOnlyClass} {extraClass}"
        prop.value props.Value
        prop.onChange props.OnChange
        prop.placeholder props.Placeholder
        prop.disabled props.Disabled
        if props.ReadOnly then prop.readOnly true
        match props.OnFocus with
        | Some handler -> prop.onFocus handler
        | None -> ()
        match props.OnBlur with
        | Some handler -> prop.onBlur handler
        | None -> ()
        match props.OnKeyDown with
        | Some handler -> prop.onKeyDown handler
        | None -> ()
        match props.InputMode with
        | Some mode -> prop.custom ("inputMode", mode)
        | None -> ()
        match props.AriaLabel with
        | Some label -> prop.ariaLabel label
        | None -> ()
        match props.DescribedBy with
        | Some id -> prop.ariaDescribedBy id
        | None -> ()
        if props.AriaInvalid then prop.ariaInvalid true
        // Prevent iOS zoom on focus (Small und Large regeln das über ihre Klassen)
        if usesInlineFontSize props.Size then
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

/// Die Klasse eines Betragsfelds: Mono, Ziffern gleich breit, bei ungültiger Eingabe färbt `input-invalid`
/// die Ziffern rot (`styles.css`). `hero` macht den Betrag groß (`input-amount-hero`, Formular "Neue Buchung"
/// und Sheet "Betrag"). Zugewiesen, Holen und Abgeben, das Target und das Kassenbuch teilen sie.
let amountFieldClass (hero: bool) (invalid: bool) : string =
    let heroClass = if hero then " input-amount-hero" else ""
    let invalidClass = if invalid then " input-invalid" else ""
    $"w-full input-amount{heroClass}{invalidClass} tabular-nums"

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

    Html.select [
        prop.className $"select-field {sizeClass} {stateClass} {disabledClass} {extraClass}"
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

/// Klassen des Beschriftungstexts einer Listenzeilen-Checkbox: Größe und Gewicht von
/// `ListRoles.primary`, einzeilig gekürzt. Nur eine Farbklasse (die aus `primary`).
let checkboxListLabelClass : string =
    $"{ListRoles.primary} truncate min-w-0"

let private checkboxWith (listRow: bool) (isChecked: bool) (onChange: bool -> unit) (label: string) =
    Html.label [
        prop.className (if listRow then "flex items-center gap-3 cursor-pointer min-w-0" else "flex items-center gap-3 cursor-pointer group")
        prop.children [
            Html.label [
                prop.className "proto-toggle shrink-0"
                prop.onClick (fun e -> e.stopPropagation())
                prop.children [
                    Html.input [
                        prop.type' "checkbox"
                        prop.isChecked isChecked
                        prop.onChange (fun (e: Browser.Types.Event) ->
                            let target = e.target :?> Browser.Types.HTMLInputElement
                            onChange target.``checked``
                        )
                    ]
                    Html.span [
                        prop.className "toggle-track"
                        prop.children [
                            Svg.svg [
                                svg.className "toggle-check"
                                svg.viewBox(0, 0, 10, 10)
                                svg.children [
                                    Svg.path [
                                        svg.d "M2 5l2.5 2.5L8 3"
                                        svg.fill "none"
                                        svg.stroke "currentColor"
                                        svg.strokeWidth 2
                                        svg.strokeLineCap "round"
                                        svg.strokeLineJoin "round"
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
            Html.span [
                prop.className (if listRow then checkboxListLabelClass else "text-text-primary group-hover:text-text-secondary transition-colors")
                prop.text label
            ]
        ]
    ]

/// Checkbox input
let checkbox (isChecked: bool) (onChange: bool -> unit) (label: string) =
    checkboxWith false isChecked onChange label

/// Checkbox für Listenzeilen (Auswahlen): Beschriftung in `ListRoles.primary`, einzeilig gekürzt.
let checkboxListRow (isChecked: bool) (onChange: bool -> unit) (label: string) =
    checkboxWith true isChecked onChange label

/// Die native Checkbox des Browsers ohne eigene Gestalt, wie in der Skizze der Vorschau Targets anwenden
/// (budgeting-m3x7f). Ohne Beschriftung: sie steht im umgebenden `label` der Zeile, ein Klick auf die Zeile schaltet.
let checkboxNative (isChecked: bool) (onChange: bool -> unit) =
    Html.input [
        prop.type' "checkbox"
        prop.isChecked isChecked
        prop.onChange (fun (value: bool) -> onChange value)
    ]

/// Checkbox without label
let checkboxSimple (isChecked: bool) (onChange: bool -> unit) =
    Html.label [
        prop.className "proto-toggle"
        prop.children [
            Html.input [
                prop.type' "checkbox"
                prop.isChecked isChecked
                prop.onChange (fun (e: Browser.Types.Event) ->
                    let target = e.target :?> Browser.Types.HTMLInputElement
                    onChange target.``checked``
                )
            ]
            Html.span [
                prop.className "toggle-track"
                prop.children [
                    Svg.svg [
                        svg.className "toggle-check"
                        svg.viewBox(0, 0, 10, 10)
                        svg.children [
                            Svg.path [
                                svg.d "M2 5l2.5 2.5L8 3"
                                svg.fill "none"
                                svg.stroke "currentColor"
                                svg.strokeWidth 2
                                svg.strokeLineCap "round"
                                svg.strokeLineJoin "round"
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Toggle/Switch
// ============================================

/// Toggle switch
let toggle (isChecked: bool) (onChange: bool -> unit) (label: string option) =
    Html.label [
        prop.className "flex items-center gap-3 cursor-pointer"
        prop.children [
            Html.label [
                prop.className "proto-toggle"
                prop.onClick (fun e -> e.stopPropagation())
                prop.children [
                    Html.input [
                        prop.type' "checkbox"
                        prop.isChecked isChecked
                        prop.onChange (fun (e: Browser.Types.Event) ->
                            let target = e.target :?> Browser.Types.HTMLInputElement
                            onChange target.``checked``
                        )
                    ]
                    Html.span [
                        prop.className "toggle-track"
                        prop.children [
                            Svg.svg [
                                svg.className "toggle-check"
                                svg.viewBox(0, 0, 10, 10)
                                svg.children [
                                    Svg.path [
                                        svg.d "M2 5l2.5 2.5L8 3"
                                        svg.fill "none"
                                        svg.stroke "currentColor"
                                        svg.strokeWidth 2
                                        svg.strokeLineCap "round"
                                        svg.strokeLineJoin "round"
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
            match label with
            | Some l ->
                Html.span [
                    prop.className "text-text-primary"
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
                prop.className "block text-sm font-medium text-text-secondary"
                prop.children [
                    Html.text props.Label
                    if props.Required then
                        Html.span [
                            prop.className "text-error ml-0.5"
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
                    prop.className "text-xs text-error"
                    prop.text err
                ]
            | Some help, None ->
                Html.p [
                    prop.className "text-xs text-text-muted/70"
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
// Auswahlfeld (Styleguide §7.5)
// ============================================

/// Ein Auswahlfeld als Knopf (ledger-at63w, Formular "Neue Buchung"): die Bezeichnung klein oben,
/// darunter der gewählte Wert (oder gedämpft der Platzhalter), rechts optional ein Zusatz (etwa der
/// Saldo eines Kontos) und der Pfeil. Ein Tipp öffnet die Auswahl (`CategoryPicker`, Sheet am
/// Handy). Der Knopf nimmt den Fokus beim Tippen nicht weg (`preventDefault` auf `mousedown`),
/// damit eine offene Tastatur bei einem Wechsel zwischen Feldern nicht zuklappt.
let pickerField
    (testId: string)
    (label: string)
    (value: string option)
    (placeholder: string)
    (extra: ReactElement option)
    (isOpen: bool)
    (onClick: unit -> unit)
    : ReactElement =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.custom ("aria-haspopup", "dialog")
        prop.custom ("aria-expanded", (if isOpen then "true" else "false"))
        prop.className (
            "input-field w-full flex items-center gap-3 px-4 py-2 min-h-[52px] text-left touch-manipulation"
        )
        prop.onMouseDown (fun e -> e.preventDefault ())
        prop.onClick (fun _ -> onClick ())
        prop.children [
            Html.span [
                prop.className "flex min-w-0 flex-1 flex-col"
                prop.children [
                    Html.span [ prop.className $"{FontSizes.xs} {Colors.textMuted}"; prop.text label ]
                    match value with
                    | Some v -> Truncate.text $"text-base md:text-sm {Colors.textPrimary}" v
                    | None -> Html.span [ prop.className $"text-base md:text-sm {Colors.textMuted}"; prop.text placeholder ]
                ]
            ]
            match extra with
            | Some e -> e
            | None -> ()
            Icons.chevronRight Icons.SM Icons.Default
        ]
    ]

// ============================================
// Form Sections
// ============================================

/// Horizontal form row (label and input side by side on desktop)
let formRow (label: string) (children: ReactElement) =
    Html.div [
        prop.className "flex flex-col md:flex-row md:items-center gap-2 md:gap-4"
        prop.children [
            Html.label [
                prop.className "text-sm font-medium text-text-secondary md:w-1/3 md:text-right"
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
                prop.className "text-lg font-semibold font-display text-text-primary border-b border-border-subtle pb-2"
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
/// Platzhalter der Suche im searchableSelect
let searchPlaceholderText = "Suchen…"
/// Text bei leerer Trefferliste im searchableSelect
let noMatchesText = "Keine Treffer"

/// Filtert Optionen `(value, "Gruppe: Name")` ohne Beachtung der Schreibweise. Die Suche
/// trifft das ganze Label, findet also Gruppe und Name.
let filterOptionsBySearch (search: string) (options: (string * string) list) : (string * string) list =
    if System.String.IsNullOrWhiteSpace search then
        options
    else
        let searchLower = search.Trim().ToLowerInvariant()
        options |> List.filter (fun (_, label) -> label.ToLowerInvariant().Contains searchLower)

/// Gliedert Optionen `(value, "Gruppe: Name")` in Gruppen. Der Gruppenname steht ohne
/// Präfix im Ergebnis, die Optionen tragen nur den Namen. Labels ohne ": " landen in
/// einer Gruppe mit leerem Namen (ohne Kopf). Die Gruppen folgen der Reihenfolge ihres
/// ersten Auftretens.
let groupOptions (options: (string * string) list) : (string * (string * string) list) list =
    let split (label: string) =
        match label.IndexOf ": " with
        | -1 -> "", label
        | idx -> label.Substring(0, idx).Trim(), label.Substring(idx + 2).Trim()
    options
    |> List.map (fun (value, label) ->
        let group, name = split label
        group, (value, name))
    |> List.groupBy fst
    |> List.map (fun (group, items) -> group, items |> List.map snd)

/// Zeilenklassen einer Option im searchableSelect: 15px am Handy, 14px am PC (wie die
/// Picker-Zeile), nur eine Farbklasse je Zustand.
let searchableOptionClass (isHighlighted: bool) (isSelected: bool) : string =
    let color =
        if isHighlighted then "bg-accent/20 text-accent"
        elif isSelected then "bg-accent/10 text-accent"
        else "text-text-primary hover:bg-accent/10 hover:text-accent"
    $"w-full text-left px-4 py-2.5 {ListRoles.primarySize} font-medium transition-colors {color}"

[<ReactComponent>]
let SearchableSelect (props: SearchableSelectProps) =
    let isOpen, setIsOpen = React.useState false
    let searchText, setSearchText = React.useState ""
    let highlightedIndex, setHighlightedIndex = React.useState -1
    let isKeyboardNav, setIsKeyboardNav = React.useState false  // Track if navigation is via keyboard
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None
    let inputRef = React.useRef<Browser.Types.HTMLInputElement option> None
    let listRef = React.useRef<Browser.Types.HTMLElement option> None
    let dropdownRef = React.useRef<Browser.Types.HTMLElement option> None

    // Find current label for display
    let currentLabel =
        props.Options
        |> List.tryFind (fun (v, _) -> v = props.Value)
        |> Option.map snd
        |> Option.defaultValue ""

    // Filter (Gruppe oder Name), dann nach Gruppen gliedern. Die Tastaturnavigation
    // läuft über die gegliederte Reihenfolge, damit Index und Anzeige übereinstimmen.
    let groupedOptions = groupOptions (filterOptionsBySearch searchText props.Options)
    let filteredOptions = groupedOptions |> List.collect snd

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

    // Option mit laufender Nummer (0 ist die Leer-Option), in Anzeigereihenfolge
    let indexedGroups =
        groupedOptions
        |> List.mapFold
            (fun n (g, items) -> (g, items |> List.mapi (fun i it -> n + i + 1, it)), n + items.Length)
            0
        |> fst

    // Beim Öffnen die Liste ins Sichtfeld holen: in einem Dialog mit scrollendem Körper
    // reicht der Platz unter dem Feld sonst nicht bis zur letzten Option.
    React.useEffect (fun () ->
        if isOpen then
            match dropdownRef.current with
            | Some el -> el.scrollIntoView(false)
            | None -> ()
    , [| isOpen :> obj |])

    let sizeClass = sizeToClass props.Size
    let disabledClass = if props.Disabled then "opacity-50 cursor-not-allowed" else ""

    Html.div [
        prop.className "relative w-full"
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
                        prop.className (if props.Value = "" then "text-text-muted/70" else "text-text-primary truncate")
                        prop.text (if props.Value = "" then props.Placeholder else currentLabel)
                    ]
                    // Chevron icon
                    let rotateClass = if isOpen then "rotate-180" else ""
                    Svg.svg [
                        svg.className $"w-4 h-4 text-text-muted/70 flex-shrink-0 transition-transform {rotateClass}"
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
                    prop.className "absolute z-50 w-full mt-1 bg-surface-input border border-border-default rounded-lg shadow-xl overflow-hidden"
                    prop.ref dropdownRef
                    prop.children [
                        // Search input
                        Html.div [
                            prop.className "p-2 border-b border-border-default"
                            prop.children [
                                Html.input [
                                    prop.ref inputRef
                                    prop.type' "text"
                                    prop.className $"w-full bg-surface-card text-text-primary border border-border-strong rounded-md px-3 py-2 min-h-[36px] focus:ring-2 focus:ring-accent focus:outline-none focus-visible:outline-none placeholder:text-text-muted/70 {searchFieldTextClass}"
                                    prop.placeholder searchPlaceholderText
                                    prop.value searchText
                                    prop.autoFocus true
                                    prop.onChange setSearchText
                                    prop.onKeyDown handleKeyDown
                                ]
                            ]
                        ]

                        // Options list
                        Html.div [
                            prop.className "max-h-64 overflow-y-auto"
                            prop.ref listRef
                            prop.children [
                                // Empty option (clear selection) - index 0
                                let clearHighlighted = highlightedIndex = 0
                                let clearClass =
                                    if clearHighlighted then
                                        $"w-full text-left px-4 py-2.5 {ListRoles.primarySize} italic bg-accent/20 text-accent"
                                    else
                                        $"w-full text-left px-4 py-2.5 {ListRoles.primarySize} text-text-muted/70 hover:bg-accent/10 hover:text-accent transition-colors italic"
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
                                        prop.className "px-3 py-4 text-sm text-text-muted/70 text-center"
                                        prop.text noMatchesText
                                    ]
                                else
                                    for (groupName, items) in indexedGroups do
                                        if groupName <> "" then
                                            Html.div [
                                                prop.key $"group-{groupName}"
                                                prop.className $"px-4 pt-3 pb-1 {ListRoles.groupHeader}"
                                                prop.text groupName
                                            ]
                                        for (idx, (value, name)) in items do
                                            let isSelected = value = props.Value
                                            let isHighlighted = highlightedIndex = idx
                                            Html.button [
                                                prop.key $"opt-{value}"
                                                prop.type' "button"
                                                prop.className (searchableOptionClass isHighlighted isSelected)
                                                prop.custom ("data-option-index", string idx)
                                                prop.onClick (fun _ -> selectOption idx)
                                                prop.onMouseEnter (fun _ -> setHighlightFromMouse idx)
                                                prop.children [
                                                    Html.span [
                                                        prop.className "truncate block"
                                                        prop.text name
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

// ============================================================================
// COMBOBOX - Text input with dropdown suggestions (allows custom values)
// ============================================================================

/// Option for ComboBox with support for disabled items (section headers)
type ComboBoxOption = {
    Id: string
    Label: string
    IsDisabled: bool  // True for section headers (non-selectable)
}

type ComboBoxProps = {
    Value: string
    OnChange: string -> unit
    Options: ComboBoxOption list  // Supports grouped options with disabled headers
    Placeholder: string
    Size: InputSize
    Disabled: bool
}

/// Create a section header (non-selectable) for grouping options
let sectionHeader label : ComboBoxOption =
    { Id = ""; Label = label; IsDisabled = true }

/// Convert simple (id, label) tuples to ComboBoxOption list
let toComboBoxOptions (options: (string * string) list) : ComboBoxOption list =
    options |> List.map (fun (id, label) ->
        { Id = id; Label = label; IsDisabled = false })

let comboBoxDefaults = {
    Value = ""
    OnChange = ignore
    Options = []
    Placeholder = ""
    Size = Medium
    Disabled = false
}

/// ComboBox: A text input with dropdown suggestions.
/// Unlike SearchableSelect, this allows custom text input (not just selection).
/// The Value is the actual text, not an option id.
[<ReactComponent>]
let ComboBox (props: ComboBoxProps) =
    let isOpen, setIsOpen = React.useState false
    let highlightedIndex, setHighlightedIndex = React.useState -1
    let isKeyboardNav, setIsKeyboardNav = React.useState false
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None
    let inputRef = React.useRef<Browser.Types.HTMLInputElement option> None
    let listRef = React.useRef<Browser.Types.HTMLElement option> None

    // Filter options based on current value (case-insensitive contains)
    // Section headers are always included if any of their following items match
    let filteredOptions =
        if System.String.IsNullOrWhiteSpace props.Value then
            props.Options
        else
            let searchLower = props.Value.ToLowerInvariant()
            // Keep section headers if any following non-header matches
            let rec filterWithHeaders (opts: ComboBoxOption list) (acc: ComboBoxOption list) =
                match opts with
                | [] -> List.rev acc
                | header :: rest when header.IsDisabled ->
                    // Find all items until next header
                    let itemsUntilNextHeader =
                        rest |> List.takeWhile (fun o -> not o.IsDisabled)
                    let hasMatchingItems =
                        itemsUntilNextHeader
                        |> List.exists (fun o -> o.Label.ToLowerInvariant().Contains searchLower)
                    if hasMatchingItems then
                        // Include header and matching items
                        let matchingItems =
                            itemsUntilNextHeader
                            |> List.filter (fun o -> o.Label.ToLowerInvariant().Contains searchLower)
                        filterWithHeaders (rest |> List.skip itemsUntilNextHeader.Length) (List.rev matchingItems @ header :: acc)
                    else
                        // Skip header and its items
                        filterWithHeaders (rest |> List.skip itemsUntilNextHeader.Length) acc
                | opt :: rest ->
                    if opt.Label.ToLowerInvariant().Contains searchLower then
                        filterWithHeaders rest (opt :: acc)
                    else
                        filterWithHeaders rest acc
            filterWithHeaders props.Options []

    // Get only selectable (non-disabled) options for keyboard navigation
    let selectableIndices =
        filteredOptions
        |> List.indexed
        |> List.filter (fun (_, opt) -> not opt.IsDisabled)
        |> List.map fst

    let totalItems = filteredOptions.Length

    // Reset highlighted index when value changes
    React.useEffect (fun () ->
        setHighlightedIndex -1
    , [| props.Value :> obj |])

    // Close dropdown when clicking outside
    React.useEffect (fun () ->
        let handleClickOutside (e: Browser.Types.Event) =
            match containerRef.current with
            | Some container ->
                let target = e.target :?> Browser.Types.HTMLElement
                if not (container.contains target) then
                    setIsOpen false
                    setHighlightedIndex -1
            | None -> ()

        Browser.Dom.document.addEventListener("mousedown", handleClickOutside)
        { new System.IDisposable with
            member _.Dispose() =
                Browser.Dom.document.removeEventListener("mousedown", handleClickOutside)
        }
    , [| isOpen :> obj |])

    // Scroll highlighted item into view - ONLY for keyboard navigation
    React.useEffect (fun () ->
        if highlightedIndex >= 0 && isKeyboardNav then
            match listRef.current with
            | Some list ->
                let items = list.querySelectorAll("[data-option-index]")
                if highlightedIndex < int items.length then
                    let item = items.[highlightedIndex] :?> Browser.Types.HTMLElement
                    let itemTop = item.offsetTop
                    let itemHeight = item.offsetHeight
                    let listScrollTop = list.scrollTop
                    let listHeight = list.clientHeight

                    if itemTop < listScrollTop then
                        list.scrollTop <- itemTop
                    elif itemTop + itemHeight > listScrollTop + listHeight then
                        list.scrollTop <- itemTop + itemHeight - listHeight
            | None -> ()
    , [| highlightedIndex :> obj; isKeyboardNav :> obj |])

    let selectOption index =
        if index >= 0 && index < filteredOptions.Length then
            let opt = filteredOptions.[index]
            if not opt.IsDisabled then
                props.OnChange opt.Label
        setIsOpen false
        setHighlightedIndex -1
        setIsKeyboardNav false

    // Find next/previous selectable index (skipping disabled items)
    let findNextSelectable currentIndex direction =
        if selectableIndices.IsEmpty then -1
        else
            match direction with
            | 1 -> // Down
                selectableIndices
                |> List.tryFind (fun i -> i > currentIndex)
                |> Option.defaultValue (List.head selectableIndices)
            | _ -> // Up
                selectableIndices
                |> List.rev
                |> List.tryFind (fun i -> i < currentIndex)
                |> Option.defaultValue (List.last selectableIndices)

    let handleKeyDown (e: Browser.Types.KeyboardEvent) =
        match e.key with
        | "Escape" ->
            e.preventDefault()
            setIsOpen false
            setHighlightedIndex -1
            setIsKeyboardNav false
        | "ArrowDown" ->
            e.preventDefault()
            if not isOpen && filteredOptions.Length > 0 then
                setIsOpen true
            setIsKeyboardNav true
            let nextIndex = findNextSelectable highlightedIndex 1
            setHighlightedIndex nextIndex
        | "ArrowUp" ->
            e.preventDefault()
            if not isOpen && filteredOptions.Length > 0 then
                setIsOpen true
            setIsKeyboardNav true
            let nextIndex = findNextSelectable highlightedIndex -1
            setHighlightedIndex nextIndex
        | "Enter" ->
            if highlightedIndex >= 0 then
                e.preventDefault()
                selectOption highlightedIndex
            // If no selection, allow form submission (don't prevent default)
        | "Tab" ->
            setIsOpen false
            setHighlightedIndex -1
            setIsKeyboardNav false
        | _ -> ()

    let setHighlightFromMouse index =
        // Only highlight if not disabled
        if index >= 0 && index < filteredOptions.Length && not filteredOptions.[index].IsDisabled then
            setIsKeyboardNav false
            setHighlightedIndex index

    let sizeClass = sizeToClass props.Size
    let disabledClass = if props.Disabled then "opacity-50 cursor-not-allowed" else ""

    Html.div [
        prop.className "relative w-full"
        prop.ref containerRef
        prop.children [
            // Text input (always visible and editable)
            Html.input [
                prop.ref inputRef
                prop.type' "text"
                prop.className $"{baseInputClass} {sizeClass} {disabledClass}"
                prop.placeholder props.Placeholder
                prop.value props.Value
                prop.disabled props.Disabled
                prop.onChange (fun v ->
                    props.OnChange v
                    if not isOpen && filteredOptions.Length > 0 then
                        setIsOpen true
                )
                prop.onFocus (fun _ ->
                    if filteredOptions.Length > 0 then
                        setIsOpen true
                )
                prop.onKeyDown handleKeyDown
                prop.style [ style.fontSize 16 ]
            ]

            // Dropdown with suggestions
            if isOpen && filteredOptions.Length > 0 then
                Html.div [
                    prop.className "absolute z-50 w-full mt-1 bg-surface-input border border-border-default rounded-lg shadow-xl overflow-hidden"
                    prop.children [
                        Html.div [
                            prop.className "max-h-60 overflow-y-auto"
                            prop.ref listRef
                            prop.children [
                                for i, opt in filteredOptions |> List.indexed do
                                    if opt.IsDisabled then
                                        // Section header (non-selectable)
                                        Html.div [
                                            prop.key $"header-{i}"
                                            prop.className "px-4 py-2 text-xs font-semibold uppercase tracking-wider text-text-muted bg-surface-card/50 select-none"
                                            prop.text opt.Label
                                        ]
                                    else
                                        // Regular option (selectable)
                                        let isHighlighted = highlightedIndex = i
                                        let optionClass =
                                            if isHighlighted then
                                                "w-full text-left px-4 py-2 text-sm transition-colors bg-accent/20 text-accent"
                                            else
                                                "w-full text-left px-4 py-2 text-sm transition-colors text-text-primary hover:bg-accent/10 hover:text-accent"
                                        Html.button [
                                            prop.key $"opt-{i}"
                                            prop.type' "button"
                                            prop.className optionClass
                                            prop.custom ("data-option-index", string i)
                                            prop.onClick (fun _ -> selectOption i)
                                            prop.onMouseEnter (fun _ -> setHighlightFromMouse i)
                                            prop.children [
                                                Html.span [
                                                    prop.className "truncate block"
                                                    prop.text opt.Label
                                                ]
                                            ]
                                        ]
                            ]
                        ]
                    ]
                ]
        ]
    ]

/// Simple helper for ComboBox with common defaults.
/// Converts (id, label) tuples to ComboBoxOption list automatically.
let comboBox value onChange placeholder (options: (string * string) list) =
    ComboBox {
        comboBoxDefaults with
            Value = value
            OnChange = onChange
            Placeholder = placeholder
            Options = toComboBoxOptions options
    }

/// ComboBox with support for grouped options (including section headers).
/// Use sectionHeader to create non-selectable group headers.
let comboBoxGrouped value onChange placeholder (options: ComboBoxOption list) =
    ComboBox {
        comboBoxDefaults with
            Value = value
            OnChange = onChange
            Placeholder = placeholder
            Options = options
    }

// ============================================
// Schalter als Zeile (budgeting-sn2sn)
// ============================================

/// Ein Schalter als eigene Zeile mit Titel und Erklärung (Seite Kategorien, Schalter "Aktiv", Prototyp Version 17):
/// links Titel und darunter der Satz in `muted`, rechts die Schiene 46 × 28 px, an im Akzent mit dunklem Knopf,
/// aus in `border-strong` (3:1 gegen Karte und Fläche, §2) mit hellem Knopf. Die ganze Zeile ist der Knopf
/// (`role switch`, `aria-checked`), am Handy mindestens 48 px hoch; der Titel ist der Name, der Satz die
/// Beschreibung (`aria-describedby`). Die Linie darunter wie eine Zeile im Prototyp. `Disabled` sperrt ihn, etwa
/// während des Speicherns.
let switchRow (testId: string) (title: string) (description: string) (isOn: bool) (disabled: bool) (onToggle: bool -> unit) =
    let track = if isOn then "bg-accent" else "bg-border-strong"
    let descriptionId = $"{testId}-description"
    let knob = if isOn then "translate-x-[18px] bg-on-accent" else "translate-x-0 bg-text-primary"
    Html.button [
        prop.type'.button
        prop.custom ("data-testid", testId)
        prop.role "switch"
        prop.ariaLabel title
        prop.ariaDescribedBy descriptionId
        prop.custom ("aria-checked", (if isOn then "true" else "false"))
        prop.disabled disabled
        prop.className
            "flex w-full min-h-[52px] items-center justify-between gap-3 py-2 text-left border-b border-border-subtle cursor-pointer touch-manipulation disabled:opacity-50 disabled:cursor-not-allowed"
        prop.onClick (fun _ -> onToggle (not isOn))
        prop.children [
            Html.span [
                prop.className "flex flex-col gap-0.5 min-w-0"
                prop.children [
                    Html.span [ prop.className $"text-base md:text-sm {FontWeights.medium} {Colors.textPrimary}"; prop.text title ]
                    Html.span [ prop.id descriptionId; prop.className ListRoles.muted; prop.text description ]
                ]
            ]
            Html.span [
                prop.ariaHidden true
                prop.className $"relative inline-flex h-7 w-[46px] shrink-0 items-center rounded-full px-[3px] transition-colors {track}"
                prop.children [
                    Html.span [ prop.className $"inline-block h-[22px] w-[22px] rounded-full transition-transform motion-reduce:transition-none {knob}" ]
                ]
            ]
        ]
    ]
