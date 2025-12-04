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
    | Teal      // Neon teal
    | Orange    // Neon orange
    | Green     // Neon green
    | Purple    // Neon purple

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
    | XS -> "loading-xs"
    | SM -> "loading-sm"
    | MD -> "loading-md"
    | LG -> "loading-lg"
    | XL -> "loading-lg scale-150"

let private spinnerColorToClass = function
    | Default -> "text-base-content/70"
    | Teal -> "text-neon-teal"
    | Orange -> "text-neon-orange"
    | Green -> "text-neon-green"
    | Purple -> "text-neon-purple"

/// Create a loading spinner
let spinner (size: SpinnerSize) (color: SpinnerColor) =
    Html.span [
        prop.className $"loading loading-spinner {spinnerSizeToClass size} {spinnerColorToClass color}"
    ]

/// Default medium teal spinner
let spinnerDefault = spinner MD Teal

/// Small inline spinner
let spinnerInline = spinner SM Default

/// Large page spinner
let spinnerLarge = spinner XL Teal

// ============================================
// Ring Spinner (alternative style)
// ============================================

/// Ring-style loading spinner
let ring (size: SpinnerSize) (color: SpinnerColor) =
    Html.span [
        prop.className $"loading loading-ring {spinnerSizeToClass size} {spinnerColorToClass color}"
    ]

// ============================================
// Dots Spinner (alternative style)
// ============================================

/// Dots-style loading spinner
let dots (size: SpinnerSize) (color: SpinnerColor) =
    Html.span [
        prop.className $"loading loading-dots {spinnerSizeToClass size} {spinnerColorToClass color}"
    ]

// ============================================
// Neon Pulse Spinner
// ============================================

/// Custom neon pulse spinner with glow effect
let neonPulse (color: SpinnerColor) =
    let colorClass = spinnerColorToClass color
    let glowClass =
        match color with
        | Teal -> "shadow-glow-teal"
        | Orange -> "shadow-glow-orange"
        | Green -> "shadow-glow-green"
        | Purple -> "shadow-glow-purple"
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
        prop.className $"bg-base-200 animate-pulse {shapeClass} {extraClass}"
        if not styleProps.IsEmpty then prop.style styleProps
    ]

/// Line skeleton (text placeholder)
let line width =
    skeleton { defaultSkeleton with Width = Some width }

/// Full-width line skeleton
let lineFull =
    skeleton { defaultSkeleton with Width = Some "100" }

/// Circle skeleton (avatar placeholder)
let circle size =
    skeleton { defaultSkeleton with Shape = Circle; Width = Some size; Height = Some size }

/// Square skeleton (image placeholder)
let square size =
    skeleton { defaultSkeleton with Shape = Square; Width = Some size; Height = Some size }

/// Card skeleton
let card =
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
let avatarWithText =
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
let cardSkeleton =
    Html.div [
        prop.className "bg-base-100 border border-white/5 rounded-xl p-4 space-y-3"
        prop.children [
            Html.div [
                prop.className "flex justify-between items-center"
                prop.children [
                    line "30"
                    circle "24"
                ]
            ]
            lineFull
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
                    prop.className "bg-base-100 border border-white/5 rounded-xl p-4 space-y-3"
                    prop.children [
                        line "40"
                        Html.div [ prop.className "h-8 bg-base-200 rounded animate-pulse w-24" ]
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
                        prop.className "h-4 bg-base-200 rounded animate-pulse"
                    ]
                ]
        ]
    ]

/// Table skeleton
let tableSkeleton colCount rowCount =
    Html.div [
        prop.className "overflow-x-auto rounded-xl border border-white/5 bg-base-100"
        prop.children [
            Html.table [
                prop.className "table w-full"
                prop.children [
                    Html.thead [
                        Html.tr [
                            for _ in 1..colCount do
                                Html.th [
                                    Html.div [
                                        prop.className "h-3 bg-base-200 rounded animate-pulse w-16"
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

// ============================================
// Loading States with Context
// ============================================

/// Inline loading indicator with text
let inlineWithText (text: string) =
    Html.div [
        prop.className "flex items-center gap-2 text-base-content/70"
        prop.children [
            spinnerInline
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
            spinnerLarge
            Html.p [
                prop.className "text-base-content/60 text-sm"
                prop.text message
            ]
        ]
    ]

/// Full page loading overlay
let pageOverlay (message: string option) =
    Html.div [
        prop.className "fixed inset-0 bg-[#0a0a0f]/80 backdrop-blur-sm flex items-center justify-center z-50"
        prop.children [
            Html.div [
                prop.className "flex flex-col items-center gap-4"
                prop.children [
                    neonPulse Teal
                    match message with
                    | Some msg ->
                        Html.p [
                            prop.className "text-base-content/70 text-sm animate-pulse"
                            prop.text msg
                        ]
                    | None -> Html.none
                ]
            ]
        ]
    ]

/// Card with loading state
let cardLoading =
    Html.div [
        prop.className "bg-base-100 border border-white/5 rounded-xl p-6 flex items-center justify-center min-h-[200px]"
        prop.children [
            Html.div [
                prop.className "flex flex-col items-center gap-3"
                prop.children [
                    spinnerDefault
                    Html.span [
                        prop.className "text-sm text-base-content/50"
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
let buttonSpinner =
    Html.span [
        prop.className "loading loading-spinner loading-sm"
    ]

// ============================================
// Progress Indicators
// ============================================

/// Progress bar with neon styling
let progressBar (percent: int) (color: SpinnerColor) =
    let colorClass =
        match color with
        | Teal -> "bg-neon-teal"
        | Orange -> "bg-neon-orange"
        | Green -> "bg-neon-green"
        | Purple -> "bg-neon-purple"
        | Default -> "bg-base-content"

    let glowClass =
        match color with
        | Teal -> "shadow-[0_0_10px_theme(colors.neon-teal)]"
        | Orange -> "shadow-[0_0_10px_theme(colors.neon-orange)]"
        | Green -> "shadow-[0_0_10px_theme(colors.neon-green)]"
        | Purple -> "shadow-[0_0_10px_theme(colors.neon-purple)]"
        | Default -> ""

    Html.div [
        prop.className "w-full h-2 bg-base-200 rounded-full overflow-hidden"
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
                        prop.className "text-base-content/70"
                        prop.text label
                    ]
                    Html.span [
                        prop.className "text-base-content/50 font-mono"
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
        | Teal -> "bg-neon-teal"
        | Orange -> "bg-neon-orange"
        | Green -> "bg-neon-green"
        | Purple -> "bg-neon-purple"
        | Default -> "bg-base-content"

    Html.div [
        prop.className "w-full h-2 bg-base-200 rounded-full overflow-hidden"
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
let shimmer =
    Html.div [
        prop.className "absolute inset-0 -translate-x-full animate-[shimmer_1.5s_infinite] bg-gradient-to-r from-transparent via-white/5 to-transparent"
    ]

/// Neon shimmer effect overlay (enhanced with neon gradient)
let shimmerNeon =
    Html.div [
        prop.className "absolute inset-0 shimmer-neon"
    ]

/// Apply shimmer effect to a container
let withShimmer (children: ReactElement list) =
    Html.div [
        prop.className "relative overflow-hidden"
        prop.children [
            yield! children
            shimmer
        ]
    ]

/// Apply neon shimmer effect to a container
let withShimmerNeon (children: ReactElement list) =
    Html.div [
        prop.className "relative overflow-hidden"
        prop.children [
            yield! children
            shimmerNeon
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
                svg.className $"{sizeClass} text-neon-green"
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
        prop.className $"animate-success-pop {sizeClass} rounded-full bg-neon-green/20 flex items-center justify-center shadow-glow-green"
        prop.children [
            successCheckmark size
        ]
    ]

/// Success message with animated checkmark
let successMessage (message: string) =
    Html.div [
        prop.className "flex items-center gap-3 p-4 rounded-xl bg-neon-green/10 border border-neon-green/30 animate-scale-in"
        prop.children [
            successBadge SM
            Html.span [
                prop.className "text-neon-green font-medium"
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
        prop.className "flex items-center gap-3 p-4 rounded-xl bg-neon-red/10 border border-neon-red/30 animate-shake"
        prop.children [
            Icons.xCircle Icons.MD Icons.Error
            Html.span [
                prop.className "text-neon-red font-medium"
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
