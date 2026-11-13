module Client.DesignSystem.Loading

open Feliz
open Client.DesignSystem.Tokens
open Client.DesignSystem.Icons

// ============================================
// Loading Types
// ============================================

/// Spinner size variants
type SpinnerSize =
    | XS    // 16px - inline with small text
    | SM    // 20px - inline with body
    | MD    // 24px - standard
    | LG    // 32px - prominent
    | XL    // 48px - hero/page loading

/// Spinner color variants
type SpinnerColor =
    | Default   // Base content color
    | Accent    // Akzent (ADR 0087)

/// Skeleton shape variants
type SkeletonShape =
    | Line      // Single line of text
    | Circle    // Circular avatar
    | Square    // Square image
    | Card      // Card placeholder
    | Custom    // Custom dimensions

// ============================================
// Spinner Component
// ============================================

let private spinnerSizeToClass = function
    | XS -> "sm"
    | SM -> "sm"
    | MD -> ""
    | LG -> "lg"
    | XL -> "lg scale-150"

let private spinnerColorToClass = function
    | Default -> "text-text-secondary"
    | Accent -> "text-accent"

/// Create a loading spinner
let spinner (size: SpinnerSize) (color: SpinnerColor) =
    let sizeClass =
        match size with
        | XS -> "sm"
        | SM -> "sm"
        | MD -> ""
        | LG -> "lg"
        | XL -> "lg"
    let spinnerClass = if sizeClass = "" then "proto-spinner" else $"proto-spinner {sizeClass}"
    Html.span [
        prop.className $"{spinnerClass} {spinnerColorToClass color}"
    ]

/// Default medium accent spinner
let spinnerDefault () = spinner MD Accent

/// Small inline spinner
let spinnerInline () = spinner SM Default

/// Large page spinner
let spinnerLarge () = spinner XL Accent

// ============================================
// Ring Spinner (alternative style)
// ============================================

/// Ring-style loading spinner
let ring (size: SpinnerSize) (color: SpinnerColor) =
    let sizeClass =
        match size with
        | XS -> "sm"
        | SM -> "sm"
        | MD -> ""
        | LG -> "lg"
        | XL -> "lg"
    let spinnerClass = if sizeClass = "" then "proto-spinner" else $"proto-spinner {sizeClass}"
    Html.span [
        prop.className $"{spinnerClass} {spinnerColorToClass color}"
    ]

// ============================================
// Dots Spinner (alternative style)
// ============================================

/// Dots-style loading spinner
let dots (size: SpinnerSize) (color: SpinnerColor) =
    let sizeClass =
        match size with
        | XS -> "sm"
        | SM -> "sm"
        | MD -> ""
        | LG -> "lg"
        | XL -> "lg"
    let spinnerClass = if sizeClass = "" then "proto-spinner" else $"proto-spinner {sizeClass}"
    Html.span [
        prop.className $"{spinnerClass} {spinnerColorToClass color}"
    ]

// ============================================
// Neon Pulse Spinner
// ============================================

/// Custom neon pulse spinner with glow effect
let neonPulse (color: SpinnerColor) =
    let colorClass = spinnerColorToClass color
    let glowClass =
        match color with
        | Accent -> "shadow-glow-accent"
        | Default -> ""

    Html.div [
        prop.className $"w-8 h-8 rounded-full border-2 border-current {colorClass} {glowClass} animate-neon-pulse"
    ]

// ============================================
// Skeleton Components
// ============================================

type SkeletonProps = {
    Width: string option
    Height: string option
    Shape: SkeletonShape
    ClassName: string option
}

let defaultSkeleton = {
    Width = None
    Height = None
    Shape = Line
    ClassName = None
}

/// Create a skeleton placeholder
let skeleton (props: SkeletonProps) =
    let shapeClass =
        match props.Shape with
        | Line -> "h-4 rounded"
        | Circle -> "rounded-full aspect-square"
        | Square -> "rounded-lg aspect-square"
        | Card -> "rounded-xl h-32"
        | Custom -> "rounded"

    let extraClass = props.ClassName |> Option.defaultValue ""

    let styleProps =
        [
            props.Width |> Option.map (fun w -> style.width (length.percent (int w)))
            props.Height |> Option.map (fun h -> style.height (length.px (int h)))
        ]
        |> List.choose id

    Html.div [
        prop.className $"bg-surface-elevated animate-pulse {shapeClass} {extraClass}"
        if not styleProps.IsEmpty then prop.style styleProps
    ]

/// Platzhalter in der Breite eines Betrags, etwa für einen Kontostand, der noch lädt
/// (ledger-vq2g8). So hoch wie die Zeile des Betrags (`text-sm md:text-base`: 20 px, ab
/// 768 px 24 px), damit die Zeile nicht springt, wenn der Stand kommt.
let amountSkeleton () =
    skeleton { defaultSkeleton with Shape = Custom; ClassName = Some "w-20 h-5 md:h-6" }

/// Line skeleton (text placeholder)
let line width =
    skeleton { defaultSkeleton with Width = Some width }

/// Full-width line skeleton
let lineFull () =
    skeleton { defaultSkeleton with Width = Some "100" }

/// Circle skeleton (avatar placeholder)
let circle size =
    skeleton { defaultSkeleton with Shape = Circle; Width = Some size; Height = Some size }

/// Square skeleton (image placeholder)
let square size =
    skeleton { defaultSkeleton with Shape = Square; Width = Some size; Height = Some size }

/// Card skeleton
let card () =
    skeleton { defaultSkeleton with Shape = Card; Width = Some "100" }

// ============================================
// Skeleton Groups
// ============================================

/// Text block skeleton (multiple lines)
let textBlock lineCount =
    Html.div [
        prop.className "space-y-2"
        prop.children [
            for i in 1..lineCount do
                let width = if i = lineCount then "60" else "100"
                line width
        ]
    ]

/// Avatar with text skeleton
let avatarWithText () =
    Html.div [
        prop.className "flex items-center gap-3"
        prop.children [
            circle "40"
            Html.div [
                prop.className "flex-1 space-y-2"
                prop.children [
                    line "40"
                    line "70"
                ]
            ]
        ]
    ]

/// Card skeleton with content
let cardSkeleton () =
    Html.div [
        prop.className "bg-surface-card border border-border-subtle rounded-xl p-4 space-y-3"
        prop.children [
            Html.div [
                prop.className "flex justify-between items-center"
                prop.children [
                    line "30"
                    circle "24"
                ]
            ]
            lineFull ()
            line "80"
            Html.div [
                prop.className "flex gap-2 pt-2"
                prop.children [
                    line "20"
                    line "20"
                ]
            ]
        ]
    ]

/// Stats grid skeleton
let statsGridSkeleton count =
    Html.div [
        prop.className "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 md:gap-4"
        prop.children [
            for _ in 1..count do
                Html.div [
                    prop.className "bg-surface-card border border-border-subtle rounded-xl p-4 space-y-3"
                    prop.children [
                        line "40"
                        Html.div [ prop.className "h-8 bg-surface-elevated rounded animate-pulse w-24" ]
                    ]
                ]
        ]
    ]

/// Table row skeleton
let tableRowSkeleton colCount =
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

/// Table skeleton
let tableSkeleton colCount rowCount =
    Html.div [
        prop.className "overflow-x-auto rounded-xl border border-border-subtle bg-surface-card"
        prop.children [
            Html.table [
                prop.className "w-full"
                prop.children [
                    Html.thead [
                        Html.tr [
                            for _ in 1..colCount do
                                Html.th [
                                    Html.div [
                                        prop.className "h-3 bg-surface-elevated rounded animate-pulse w-16"
                                    ]
                                ]
                        ]
                    ]
                    Html.tbody [
                        for _ in 1..rowCount do
                            tableRowSkeleton colCount
                    ]
                ]
            ]
        ]
    ]

/// Skeleton mimicking the transaction list (date header + rows), so the layout
/// doesn't jump when the real rows arrive.
let txListSkeleton (rowCount: int) =
    Html.div [
        prop.className "tx-list"
        prop.ariaLabel "Lade Transaktionen"
        prop.children [
            Html.div [
                prop.className "date-group"
                prop.children [
                    Html.div [
                        prop.className "date-header"
                        prop.children [
                            Html.div [ prop.className "h-3 w-32 bg-surface-elevated rounded animate-pulse" ]
                        ]
                    ]
                    for _ in 1..rowCount do
                        Html.div [
                            prop.className "tx-row"
                            prop.children [
                                Html.div [ prop.className "tx-status-bar" ]
                                Html.div [
                                    prop.className "tx-content"
                                    prop.children [
                                        Html.div [
                                            prop.className "tx-line1"
                                            prop.children [
                                                Html.div [ prop.className "h-4 w-40 bg-surface-elevated rounded animate-pulse" ]
                                                Html.div [ prop.className "h-4 w-16 bg-surface-elevated rounded animate-pulse" ]
                                            ]
                                        ]
                                        Html.div [
                                            prop.className "tx-line2"
                                            prop.children [
                                                Html.div [ prop.className "h-3 w-24 bg-surface-elevated rounded-full animate-pulse" ]
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

/// Balken in einer Textzeile: er steht in einer Zeile mit der Schriftgröße des Ziels, damit die Zeile so hoch ist wie
/// die echte.
let private lineBar = $"inline-block align-middle h-[0.75em] max-w-full {Radius.sm} {Backgrounds.surface} animate-pulse"

/// Platzhalter einer Liste mit großem ersten Eintrag (Prototyp Version 17 `todoList`, „Zu tun“ auf der Übersicht,
/// ledger-s1weh): Karte, oben Marke, Titel 16 px, Satz und der Knopf über die volle Breite, darunter `rowCount` Zeilen
/// mit Marke, zwei Textzeilen und Pfeil. Gleich hoch wie die Liste mit `rowCount + 1` Einträgen, damit beim Laden
/// nichts darunter springt (Layout-Invariante 9).
let firstItemListSkeleton (rowCount: int) =
    Html.div [
        prop.ariaLabel "Lade Aufgaben"
        prop.ariaBusy true
        prop.className $"{Backgrounds.dark} border {Borders.subtle} rounded-[14px]"
        prop.children [
            Html.div [
                prop.className (
                    "flex flex-col gap-2.5 px-3.5 pt-3.5 pb-3"
                    + if rowCount > 0 then $" border-b {Borders.subtle}" else ""
                )
                prop.children [
                    Html.div [
                        prop.className "flex items-center gap-2.5"
                        prop.children [
                            Html.span [ prop.className $"w-4 h-4 shrink-0 {Radius.full} {Backgrounds.surface} animate-pulse" ]
                            Html.div [
                                prop.className "flex-1 min-w-0"
                                prop.children [
                                    Html.div [ prop.className FontSizes.emphasis; prop.children [ Html.span [ prop.className $"{lineBar} w-44" ] ] ]
                                    Html.div [ prop.className FontSizes.compact; prop.children [ Html.span [ prop.className $"{lineBar} w-56" ] ] ]
                                ]
                            ]
                        ]
                    ]
                    // Wie `Button.view` Primary, Medium, `FullWidth`: am Handy 48 px über die volle Breite, ab `md` 40 px.
                    Html.div [ prop.className $"h-10 min-h-[48px] md:min-h-0 w-full md:w-24 {Radius.md} {Backgrounds.surface} animate-pulse" ]
                ]
            ]
            for _ in 1..rowCount do
                Html.div [
                    prop.className $"flex items-center gap-2.5 px-3 py-2.5 min-h-12 border-b {Borders.subtle} last:border-b-0"
                    prop.children [
                        Html.span [ prop.className $"w-4 h-4 shrink-0 {Radius.full} {Backgrounds.surface} animate-pulse" ]
                        Html.div [
                            prop.className "flex-1 min-w-0"
                            prop.children [
                                Html.div [ prop.className ListRoles.primarySize; prop.children [ Html.span [ prop.className $"{lineBar} w-40" ] ] ]
                                Html.div [ prop.className FontSizes.compact; prop.children [ Html.span [ prop.className $"{lineBar} w-52" ] ] ]
                            ]
                        ]
                    ]
                ]
        ]
    ]

/// Platzhalter einer Karte mit Listenzeilen (Tab „Regeln“ am Handy, categorization-n3sxw): Karte mit Rand, je Zeile
/// links die Nummer, daneben `lineCount` Textzeilen (die erste in der Größe von `ListRoles.primary`, die übrigen 12 px),
/// rechts zwei Pfeilflächen übereinander. So hoch wie die echte Zeile, damit beim Laden nichts springt.
let cardListSkeleton (rowCount: int) (lineCount: int) =
    Html.div [
        prop.ariaBusy true
        prop.className $"overflow-hidden rounded-xl border {Borders.default'} {Backgrounds.dark}"
        prop.children [
            for _ in 1..rowCount do
                Html.div [
                    prop.className $"flex items-center gap-2.5 py-3 pl-4 pr-1 border-b {Borders.subtle} last:border-b-0"
                    prop.children [
                        Html.span [ prop.className $"w-[18px] shrink-0 {FontSizes.xs}"; prop.children [ Html.span [ prop.className $"{lineBar} w-3" ] ] ]
                        Html.div [
                            prop.className "flex min-w-0 flex-1 flex-col gap-0.5"
                            prop.children [
                                Html.div [ prop.className ListRoles.primarySize; prop.children [ Html.span [ prop.className $"{lineBar} w-32" ] ] ]
                                for i in 2..lineCount do
                                    let barWidth = if i % 2 = 0 then "w-48" else "w-28"
                                    Html.div [ prop.className FontSizes.xs; prop.children [ Html.span [ prop.className $"{lineBar} {barWidth}" ] ] ]
                            ]
                        ]
                        Html.div [
                            prop.className "flex shrink-0 flex-col gap-2"
                            prop.children [
                                for _ in 1..2 do
                                    Html.span [ prop.className $"{TouchTargets.minSize} md:min-h-[40px] md:min-w-[40px] {Radius.md}" ]
                            ]
                        ]
                    ]
                ]
        ]
    ]

// ============================================
// Loading States with Context
// ============================================

/// Inline loading indicator with text
let inlineWithText (text: string) =
    Html.div [
        prop.className "flex items-center gap-2 text-text-secondary"
        prop.children [
            spinnerInline ()
            Html.span [
                prop.className "text-sm"
                prop.text text
            ]
        ]
    ]

/// Centered loading with message
let centered (message: string) =
    Html.div [
        prop.className "flex flex-col items-center justify-center gap-4 py-12"
        prop.children [
            spinnerLarge ()
            Html.p [
                prop.className "text-text-muted text-sm"
                prop.text message
            ]
        ]
    ]

/// Full page loading overlay
let pageOverlay (message: string option) =
    Html.div [
        prop.className "fixed inset-0 bg-surface-app/80 backdrop-blur-sm flex items-center justify-center z-50"
        prop.children [
            Html.div [
                prop.className "flex flex-col items-center gap-4"
                prop.children [
                    neonPulse Accent
                    match message with
                    | Some msg ->
                        Html.p [
                            prop.className "text-text-secondary text-sm animate-pulse"
                            prop.text msg
                        ]
                    | None -> Html.none
                ]
            ]
        ]
    ]

/// Card with loading state
let cardLoading () =
    Html.div [
        prop.className "bg-surface-card border border-border-subtle rounded-xl p-6 flex items-center justify-center min-h-[200px]"
        prop.children [
            Html.div [
                prop.className "flex flex-col items-center gap-3"
                prop.children [
                    spinnerDefault ()
                    Html.span [
                        prop.className "text-sm text-text-muted/70"
                        prop.text "Loading..."
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Button Loading State
// ============================================

/// Button with loading spinner (to be used with Button module)
let buttonSpinner () =
    Html.span [
        prop.className "proto-spinner sm"
    ]

// ============================================
// Progress Indicators
// ============================================

/// Progress bar with neon styling
let progressBar (percent: int) (color: SpinnerColor) =
    let colorClass =
        match color with
        | Accent -> "bg-accent"
        | Default -> "bg-text-primary"

    let glowClass =
        match color with
        | Accent -> "shadow-[0_0_10px_var(--color-accent)]"
        | Default -> ""

    Html.div [
        prop.className "w-full h-2 bg-surface-elevated rounded-full overflow-hidden"
        prop.children [
            Html.div [
                prop.className $"{colorClass} {glowClass} h-full rounded-full transition-all duration-300"
                prop.style [ style.width (length.percent percent) ]
            ]
        ]
    ]

/// Progress bar with label
let progressBarWithLabel (label: string) (percent: int) (color: SpinnerColor) =
    Html.div [
        prop.className "space-y-2"
        prop.children [
            Html.div [
                prop.className "flex justify-between text-sm"
                prop.children [
                    Html.span [
                        prop.className "text-text-secondary"
                        prop.text label
                    ]
                    Html.span [
                        prop.className "text-text-muted/70 font-mono"
                        prop.text $"{percent}%%"
                    ]
                ]
            ]
            progressBar percent color
        ]
    ]

/// Indeterminate progress bar (animated)
let progressIndeterminate (color: SpinnerColor) =
    let colorClass =
        match color with
        | Accent -> "bg-accent"
        | Default -> "bg-text-primary"

    Html.div [
        prop.className "w-full h-2 bg-surface-elevated rounded-full overflow-hidden"
        prop.children [
            Html.div [
                prop.className $"{colorClass} h-full rounded-full animate-[shimmer_1.5s_infinite]"
                prop.style [
                    style.width (length.percent 30)
                    style.custom ("animation", "shimmer 1.5s infinite")
                ]
            ]
        ]
    ]

// ============================================
// Shimmer Effect
// ============================================

/// Shimmer loading effect overlay
let shimmer () =
    Html.div [
        prop.className "absolute inset-0 -translate-x-full animate-[shimmer_1.5s_infinite] bg-gradient-to-r from-transparent via-white/5 to-transparent"
    ]

/// Neon shimmer effect overlay (enhanced with neon gradient)
let shimmerNeon () =
    Html.div [
        prop.className "absolute inset-0 shimmer-neon"
    ]

/// Apply shimmer effect to a container
let withShimmer (children: ReactElement list) =
    Html.div [
        prop.className "relative overflow-hidden"
        prop.children [
            yield! children
            shimmer ()
        ]
    ]

/// Apply neon shimmer effect to a container
let withShimmerNeon (children: ReactElement list) =
    Html.div [
        prop.className "relative overflow-hidden"
        prop.children [
            yield! children
            shimmerNeon ()
        ]
    ]

// ============================================
// Success Feedback Components
// ============================================

/// Animated success checkmark (SVG with draw animation)
let successCheckmark (size: SpinnerSize) =
    let sizeClass =
        match size with
        | XS -> "w-4 h-4"
        | SM -> "w-5 h-5"
        | MD -> "w-6 h-6"
        | LG -> "w-8 h-8"
        | XL -> "w-12 h-12"

    Html.div [
        prop.className "animate-success-pop"
        prop.children [
            Svg.svg [
                svg.className $"{sizeClass} text-success"
                svg.viewBox (0, 0, 24, 24)
                svg.fill "none"
                svg.stroke "currentColor"
                svg.strokeWidth 3
                svg.custom ("strokeLinecap", "round")
                svg.custom ("strokeLinejoin", "round")
                svg.children [
                    Svg.path [
                        svg.d "M5 13l4 4L19 7"
                        svg.className "animate-checkmark"
                        svg.custom ("strokeDasharray", "24")
                        svg.custom ("strokeDashoffset", "24")
                    ]
                ]
            ]
        ]
    ]

/// Success badge with checkmark (circular background)
let successBadge (size: SpinnerSize) =
    let sizeClass =
        match size with
        | XS -> "w-6 h-6"
        | SM -> "w-8 h-8"
        | MD -> "w-10 h-10"
        | LG -> "w-12 h-12"
        | XL -> "w-16 h-16"

    Html.div [
        prop.className $"animate-success-pop {sizeClass} rounded-full bg-success/20 flex items-center justify-center shadow-glow-success"
        prop.children [
            successCheckmark size
        ]
    ]

/// Success message with animated checkmark
let successMessage (message: string) =
    Html.div [
        prop.className "flex items-center gap-3 p-4 rounded-xl bg-success/10 border border-success/30 animate-scale-in"
        prop.children [
            successBadge SM
            Html.span [
                prop.className "text-success font-medium"
                prop.text message
            ]
        ]
    ]

// ============================================
// Error Feedback Components
// ============================================

/// Error shake wrapper - wraps content and shakes on error
let withShake (isError: bool) (children: ReactElement list) =
    Html.div [
        prop.className (if isError then "animate-shake" else "")
        prop.children children
    ]

/// Error message with icon
let errorMessage (message: string) =
    Html.div [
        prop.className "flex items-center gap-3 p-4 rounded-xl bg-error/10 border border-error/30 animate-shake"
        prop.children [
            Icons.xCircle Icons.MD Icons.Error
            Html.span [
                prop.className "text-error font-medium"
                prop.text message
            ]
        ]
    ]

// ============================================
// Staggered List Animation
// ============================================

/// Wrap list items with staggered animation
let staggeredList (animation: string) (items: ReactElement list) =
    Html.div [
        prop.className "space-y-2"
        prop.children [
            for i, item in items |> List.indexed do
                Html.div [
                    prop.key (string i)
                    prop.className $"{animation} opacity-0 {Tokens.StaggerDelays.forIndex i}"
                    prop.children [ item ]
                ]
        ]
    ]

/// Staggered slide-up list
let staggeredSlideUp (items: ReactElement list) =
    staggeredList "animate-slide-up" items

/// Staggered fade-in list
let staggeredFadeIn (items: ReactElement list) =
    staggeredList "animate-fade-in" items
