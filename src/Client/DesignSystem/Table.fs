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
    /// Wraps the table in `wrapper`'s `overflow-x-auto` container. **Set this to
    /// `false` whenever `Sticky = true`** (ledger-x0yga): CSS forces a `visible`
    /// overflow-y to compute as `auto` on any element whose overflow-x is
    /// non-visible (spec-mandated, not a bug — see MDN `overflow-x`), which turns
    /// this wrapper into its own scroll container. A sticky `<thead>`'s `top` offset
    /// is then measured against *that* container instead of the page, and since the
    /// wrapper's height always equals its own content it never actually scrolls —
    /// the header ends up floating at a fixed pixel offset from the wrapper's own
    /// top, overlapping whatever row happens to sit there, instead of sticking below
    /// the navbar. Caller must reapply `wrapper`'s decorative classes via
    /// `ClassName` if `Responsive = false` is combined with `Sticky = true`.
    Responsive: bool
    Sticky: bool            // Sticky header, positioned below the app's fixed navbar
    /// `table-layout: fixed` — column widths follow the `Width` set on header cells
    /// (`thWidth`) instead of shrinking/growing with cell content. Needed wherever a
    /// column must not be able to push others out of the visible area (ledger-x0yga).
    FixedLayout: bool
    ClassName: string option
}

let defaultTableProps = {
    Variant = ZebraHover
    Size = Normal
    Responsive = true
    Sticky = false
    FixedLayout = false
    ClassName = None
}

// ============================================
// Table Wrapper Component
// ============================================

let private variantToClass = function
    | Default -> ""
    | Zebra -> "[&_tbody_tr:nth-child(even):not(:hover)]:bg-white/[0.03]"
    | Hover -> "[&_tr:hover]:bg-white/5"
    | ZebraHover -> "[&_tbody_tr:nth-child(even):not(:hover)]:bg-white/[0.03] [&_tr:hover]:bg-white/5"

/// Row density (`Compact`/`Spacious`). `cell`/`headerCell` already own the padding
/// (`px-3 py-2`, ledger-x0yga) instead of a `.table-sm`/`.table-lg` class cascading
/// from a stylesheet DaisyUI doesn't provide (design-system-fq8dn: DaisyUI isn't
/// loaded in this project, ADR 0031) — so density overrides attach there too, via a
/// CSS custom property set here as a `style` (not a class) on `<table>`. `cell`/
/// `headerCell`'s own padding utilities read the variable with a fallback equal to
/// the previous fixed `px-3 py-2` (0.75rem/0.5rem), so `Normal` — every caller today
/// — renders byte-identical.
let private sizeStyle = function
    | Compact -> [ style.custom ("--table-cell-px", "0.5rem"); style.custom ("--table-cell-py", "0.25rem") ]
    | Normal -> []
    | Spacious -> [ style.custom ("--table-cell-px", "1rem"); style.custom ("--table-cell-py", "0.75rem") ]

/// Responsive table wrapper
let wrapper (children: ReactElement list) =
    Html.div [
        prop.className "overflow-x-auto rounded-xl border border-border-subtle bg-surface-card"
        prop.children children
    ]

/// Create a styled table
let view (props: TableProps) (children: ReactElement list) =
    let variantClass = variantToClass props.Variant
    // `.table-sticky-thead` (styles.css) sticks the header just below the app's
    // fixed navbar (`Navigation`, h-16/64px at md+) instead of at the viewport's
    // very top: a plain `top-0` sticky header hides behind the navbar and its first
    // pixels get cut off (ledger-x0yga) — same pattern as `.filters` in styles.css.
    let stickyClass = if props.Sticky then "table-sticky-thead" else ""
    let fixedLayoutClass = if props.FixedLayout then "table-fixed" else ""
    let extraClass = props.ClassName |> Option.defaultValue ""

    let table =
        Html.table [
            prop.className $"w-full {variantClass} {stickyClass} {fixedLayoutClass} {extraClass}"
            prop.style (sizeStyle props.Size)
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
        // Cell padding lives here, not in a `.table`/`.table-sm` DaisyUI class —
        // DaisyUI isn't loaded in this project (ledger-x0yga, ADR 0031). Without it
        // that class did nothing and adjacent columns' text touched with 0px gap.
        // The var() fallback is the previous fixed `px-3 py-2`; `Table.view`'s
        // `Size` overrides it via `style` on `<table>` (`sizeStyle`, not a class).
        prop.className $"text-xs font-semibold uppercase tracking-wider text-text-muted/70 bg-surface-elevated px-[var(--table-cell-px,0.75rem)] py-[var(--table-cell-py,0.5rem)] {alignClass} {extraClass}"
        if props.Sortable then
            prop.className $"cursor-pointer select-none hover:text-text-secondary transition-colors"
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
        // Same reasoning as `headerCell` above: real padding instead of the
        // absent `.table`/`.table-sm` DaisyUI classes.
        prop.className $"text-text-secondary px-[var(--table-cell-px,0.75rem)] py-[var(--table-cell-py,0.5rem)] {alignClass} {monoClass} {truncateClass} {extraClass}"
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
        prop.className "text-text-muted"
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
                    prop.className "flex flex-col items-center gap-3 text-text-muted/70"
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
                    prop.className "flex flex-col items-center gap-4 text-text-muted/70"
                    prop.children [
                        Icons.search LG IconColor.Default
                        Html.p [
                            prop.className "text-sm"
                            prop.text message
                        ]
                        Html.button [
                            prop.className "px-3 py-1.5 text-sm rounded-lg border border-neon-teal text-neon-teal hover:bg-neon-teal/10 transition-colors"
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
        prop.className "bg-surface-card border border-border-subtle rounded-xl p-4 space-y-3"
        prop.children [
            // Title
            Html.h3 [
                prop.className "font-semibold text-text-primary truncate"
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
                                    prop.className "text-text-muted/70"
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
                    prop.className "pt-2 border-t border-border-subtle flex justify-end gap-2"
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
                        prop.className "h-4 bg-surface-elevated rounded animate-pulse"
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
