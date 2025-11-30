module Client.DesignSystem.Table

open Feliz
open Client.DesignSystem.Tokens
open Client.DesignSystem.Icons

// ============================================
// Table Types
// ============================================

/// Sort direction for sortable columns
type SortDirection =
    | Ascending
    | Descending
    | Unsorted

/// Table size variants
type TableSize =
    | Compact   // Tighter rows
    | Normal    // Standard spacing
    | Spacious  // More breathing room

/// Table variant styles
type TableVariant =
    | Default       // Standard table
    | Zebra         // Alternating row colors
    | Hover         // Highlight on hover
    | ZebraHover    // Both zebra and hover

// ============================================
// Table Props
// ============================================

type TableProps = {
    Variant: TableVariant
    Size: TableSize
    Responsive: bool        // Wrap in responsive container
    Sticky: bool            // Sticky header
    ClassName: string option
}

let defaultTableProps = {
    Variant = ZebraHover
    Size = Normal
    Responsive = true
    Sticky = false
    ClassName = None
}

// ============================================
// Table Wrapper Component
// ============================================

let private sizeToClass = function
    | Compact -> "table-sm"
    | Normal -> ""
    | Spacious -> "table-lg"

let private variantToClass = function
    | Default -> ""
    | Zebra -> "table-zebra"
    | Hover -> "[&_tr:hover]:bg-white/5"
    | ZebraHover -> "table-zebra [&_tr:hover]:bg-white/5"

/// Responsive table wrapper
let wrapper (children: ReactElement list) =
    Html.div [
        prop.className "overflow-x-auto rounded-xl border border-white/5 bg-base-100"
        prop.children children
    ]

/// Create a styled table
let view (props: TableProps) (children: ReactElement list) =
    let sizeClass = sizeToClass props.Size
    let variantClass = variantToClass props.Variant
    let stickyClass = if props.Sticky then "[&_thead]:sticky [&_thead]:top-0 [&_thead]:z-10" else ""
    let extraClass = props.ClassName |> Option.defaultValue ""

    let table =
        Html.table [
            prop.className $"table w-full {sizeClass} {variantClass} {stickyClass} {extraClass}"
            prop.children children
        ]

    if props.Responsive then
        wrapper [ table ]
    else
        table

/// Simple table with default props
let simple (children: ReactElement list) =
    view defaultTableProps children

// ============================================
// Table Head Component
// ============================================

type HeaderCellProps = {
    Text: string
    Sortable: bool
    SortDirection: SortDirection
    OnSort: (unit -> unit) option
    Width: string option
    Align: string option
    ClassName: string option
}

let defaultHeaderCell = {
    Text = ""
    Sortable = false
    SortDirection = Unsorted
    OnSort = None
    Width = None
    Align = None
    ClassName = None
}

let private sortIcon direction =
    match direction with
    | Unsorted ->
        Html.span [
            prop.className "opacity-30"
            prop.children [
                Icons.chevronDown XS IconColor.Default
            ]
        ]
    | Ascending ->
        Html.span [
            prop.className "rotate-180 transition-transform"
            prop.children [
                Icons.chevronDown XS IconColor.NeonTeal
            ]
        ]
    | Descending ->
        Icons.chevronDown XS IconColor.NeonTeal

/// Create a header cell (th)
let headerCell (props: HeaderCellProps) =
    let widthStyle = props.Width |> Option.map (fun w -> prop.style [ style.width (length.px (int w)) ])
    let alignClass =
        props.Align
        |> Option.map (fun a -> $"text-{a}")
        |> Option.defaultValue "text-left"
    let extraClass = props.ClassName |> Option.defaultValue ""

    Html.th [
        match widthStyle with Some s -> s | None -> ()
        prop.className $"text-xs font-semibold uppercase tracking-wider text-base-content/50 bg-base-200 {alignClass} {extraClass}"
        if props.Sortable then
            prop.className $"cursor-pointer select-none hover:text-base-content/80 transition-colors"
            match props.OnSort with
            | Some handler -> prop.onClick (fun _ -> handler())
            | None -> ()
        prop.children [
            Html.div [
                prop.className "flex items-center gap-1"
                prop.children [
                    Html.span [ prop.text props.Text ]
                    if props.Sortable then sortIcon props.SortDirection
                ]
            ]
        ]
    ]

/// Simple header cell (non-sortable)
let th text =
    headerCell { defaultHeaderCell with Text = text }

/// Sortable header cell
let thSortable text direction onSort =
    headerCell {
        defaultHeaderCell with
            Text = text
            Sortable = true
            SortDirection = direction
            OnSort = Some onSort
    }

/// Header cell with specific width
let thWidth text width =
    headerCell { defaultHeaderCell with Text = text; Width = Some width }

/// Right-aligned header cell
let thRight text =
    headerCell { defaultHeaderCell with Text = text; Align = Some "right" }

/// Center-aligned header cell
let thCenter text =
    headerCell { defaultHeaderCell with Text = text; Align = Some "center" }

/// Table head wrapper
let thead (cells: ReactElement list) =
    Html.thead [
        Html.tr [ prop.children cells ]
    ]

// ============================================
// Table Body Component
// ============================================

/// Table body wrapper
let tbody (rows: ReactElement list) =
    Html.tbody [ prop.children rows ]

// ============================================
// Table Row Component
// ============================================

type RowProps = {
    IsSelected: bool
    IsHighlighted: bool
    OnClick: (unit -> unit) option
    ClassName: string option
}

let defaultRowProps = {
    IsSelected = false
    IsHighlighted = false
    OnClick = None
    ClassName = None
}

/// Create a table row
let row (props: RowProps) (cells: ReactElement list) =
    let selectedClass = if props.IsSelected then "bg-neon-teal/10 border-l-2 border-l-neon-teal" else ""
    let highlightClass = if props.IsHighlighted then "bg-neon-orange/5" else ""
    let clickableClass = if props.OnClick.IsSome then "cursor-pointer" else ""
    let extraClass = props.ClassName |> Option.defaultValue ""

    Html.tr [
        prop.className $"transition-colors {selectedClass} {highlightClass} {clickableClass} {extraClass}"
        match props.OnClick with
        | Some handler -> prop.onClick (fun _ -> handler())
        | None -> ()
        prop.children cells
    ]

/// Simple table row
let tr (cells: ReactElement list) =
    row defaultRowProps cells

/// Clickable table row
let trClickable onClick (cells: ReactElement list) =
    row { defaultRowProps with OnClick = Some onClick } cells

/// Selected table row
let trSelected (cells: ReactElement list) =
    row { defaultRowProps with IsSelected = true } cells

/// Highlighted table row
let trHighlighted (cells: ReactElement list) =
    row { defaultRowProps with IsHighlighted = true } cells

// ============================================
// Table Cell Component
// ============================================

type CellProps = {
    Align: string option
    ClassName: string option
    IsMono: bool
    IsTruncate: bool
    MaxWidth: string option
}

let defaultCellProps = {
    Align = None
    ClassName = None
    IsMono = false
    IsTruncate = false
    MaxWidth = None
}

/// Create a table cell (td)
let cell (props: CellProps) (children: ReactElement list) =
    let alignClass =
        props.Align
        |> Option.map (fun a -> $"text-{a}")
        |> Option.defaultValue ""
    let monoClass = if props.IsMono then "font-mono tabular-nums" else ""
    let truncateClass = if props.IsTruncate then "truncate" else ""
    let maxWidthStyle =
        props.MaxWidth
        |> Option.map (fun mw -> prop.style [ style.maxWidth (length.px (int mw)) ])
    let extraClass = props.ClassName |> Option.defaultValue ""

    Html.td [
        match maxWidthStyle with Some s -> s | None -> ()
        prop.className $"text-base-content/90 {alignClass} {monoClass} {truncateClass} {extraClass}"
        prop.children children
    ]

/// Simple table cell with text
let td (text: string) =
    cell defaultCellProps [ Html.text text ]

/// Table cell with children
let tdContent (children: ReactElement list) =
    cell defaultCellProps children

/// Right-aligned cell (for numbers)
let tdRight (text: string) =
    cell { defaultCellProps with Align = Some "right" } [ Html.text text ]

/// Center-aligned cell
let tdCenter (text: string) =
    cell { defaultCellProps with Align = Some "center" } [ Html.text text ]

/// Monospace cell (for IDs, codes)
let tdMono (text: string) =
    cell { defaultCellProps with IsMono = true } [ Html.text text ]

/// Truncated cell with max width
let tdTruncate maxWidth (text: string) =
    cell { defaultCellProps with IsTruncate = true; MaxWidth = Some maxWidth } [ Html.text text ]

/// Empty cell
let tdEmpty =
    Html.td [
        prop.className "text-base-content/30"
        prop.children [ Html.text "—" ]
    ]

// ============================================
// Empty State
// ============================================

/// Empty state for tables
let empty (message: string) =
    Html.tr [
        Html.td [
            prop.colSpan 100  // Span all columns
            prop.className "py-12 text-center"
            prop.children [
                Html.div [
                    prop.className "flex flex-col items-center gap-3 text-base-content/50"
                    prop.children [
                        Icons.search LG IconColor.Default
                        Html.p [
                            prop.className "text-sm"
                            prop.text message
                        ]
                    ]
                ]
            ]
        ]
    ]

/// Empty state with action button
let emptyWithAction (message: string) (buttonText: string) onClick =
    Html.tr [
        Html.td [
            prop.colSpan 100
            prop.className "py-12 text-center"
            prop.children [
                Html.div [
                    prop.className "flex flex-col items-center gap-4 text-base-content/50"
                    prop.children [
                        Icons.search LG IconColor.Default
                        Html.p [
                            prop.className "text-sm"
                            prop.text message
                        ]
                        Html.button [
                            prop.className "btn btn-sm btn-ghost border border-neon-teal text-neon-teal hover:bg-neon-teal/10"
                            prop.onClick (fun _ -> onClick())
                            prop.text buttonText
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Mobile Card View Alternative
// ============================================

/// Mobile card for table data when screen is too small
let mobileCard (title: string) (rows: (string * ReactElement) list) (actions: ReactElement option) =
    Html.div [
        prop.className "bg-base-100 border border-white/5 rounded-xl p-4 space-y-3"
        prop.children [
            // Title
            Html.h3 [
                prop.className "font-semibold text-base-content truncate"
                prop.text title
            ]

            // Data rows
            Html.div [
                prop.className "space-y-2"
                prop.children [
                    for (label, value) in rows do
                        Html.div [
                            prop.className "flex items-center justify-between text-sm"
                            prop.children [
                                Html.span [
                                    prop.className "text-base-content/50"
                                    prop.text label
                                ]
                                value
                            ]
                        ]
                ]
            ]

            // Actions
            match actions with
            | Some actionElement ->
                Html.div [
                    prop.className "pt-2 border-t border-white/5 flex justify-end gap-2"
                    prop.children [ actionElement ]
                ]
            | None -> Html.none
        ]
    ]

/// Mobile card list container
let mobileCardList (cards: ReactElement list) =
    Html.div [
        prop.className "space-y-3 md:hidden"
        prop.children cards
    ]

/// Responsive table that shows cards on mobile, table on desktop
let responsive (mobileCards: ReactElement list) (desktopTable: ReactElement) =
    React.fragment [
        // Mobile view
        Html.div [
            prop.className "md:hidden"
            prop.children mobileCards
        ]
        // Desktop view
        Html.div [
            prop.className "hidden md:block"
            prop.children [ desktopTable ]
        ]
    ]

// ============================================
// Loading State
// ============================================

/// Loading row with skeleton placeholders
let loadingRow colCount =
    Html.tr [
        prop.children [
            for _ in 1..colCount do
                Html.td [
                    Html.div [
                        prop.className "h-4 bg-base-200 rounded animate-pulse"
                    ]
                ]
        ]
    ]

/// Loading table body with multiple skeleton rows
let loadingBody colCount rowCount =
    tbody [
        for _ in 1..rowCount do
            loadingRow colCount
    ]
