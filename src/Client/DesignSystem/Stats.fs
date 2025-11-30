module Client.DesignSystem.Stats

open Feliz
open Client.DesignSystem.Tokens
open Client.DesignSystem.Icons

// ============================================
// Stat Types
// ============================================

/// Trend direction for stat changes
type Trend =
    | Up of percentChange: decimal
    | Down of percentChange: decimal
    | Neutral

/// Accent color for the gradient line at top of stat card
type StatAccent =
    | Teal
    | Green
    | Orange
    | Purple
    | Pink
    | Gradient  // Default: teal -> green -> orange

/// Size variants for stat cards
type StatSize =
    | Compact   // Tighter padding, smaller text
    | Normal    // Standard stat card
    | Large     // Hero-style large stat

// ============================================
// Stat Props
// ============================================

type StatProps = {
    Label: string
    Value: string
    Icon: ReactElement option
    Trend: Trend option
    Accent: StatAccent
    Size: StatSize
    Description: string option
    ClassName: string option
}

let defaultProps = {
    Label = ""
    Value = ""
    Icon = None
    Trend = None
    Accent = Gradient
    Size = Normal
    Description = None
    ClassName = None
}

// ============================================
// Stat Implementation
// ============================================

let private accentToGradient = function
    | Teal -> "from-neon-teal to-neon-teal"
    | Green -> "from-neon-green to-neon-green"
    | Orange -> "from-neon-orange to-neon-orange"
    | Purple -> "from-neon-purple to-neon-purple"
    | Pink -> "from-neon-pink to-neon-pink"
    | Gradient -> "from-neon-teal via-neon-green to-neon-orange"

let private sizeToClasses = function
    | Compact -> "p-3 md:p-4", "text-lg md:text-xl", "text-[10px] md:text-xs"
    | Normal -> "p-4 md:p-6", "text-xl md:text-2xl", "text-xs md:text-sm"
    | Large -> "p-5 md:p-8", "text-2xl md:text-4xl", "text-sm md:text-base"

let private trendView (trend: Trend) =
    match trend with
    | Neutral -> Html.none
    | Up pct ->
        Html.div [
            prop.className "flex items-center gap-1 text-neon-green text-xs md:text-sm font-medium"
            prop.children [
                Svg.svg [
                    svg.className "w-3 h-3 md:w-4 md:h-4"
                    svg.fill "none"
                    svg.viewBox (0, 0, 24, 24)
                    svg.stroke "currentColor"
                    svg.strokeWidth 2
                    svg.children [
                        Svg.path [
                            svg.strokeLineCap "round"
                            svg.strokeLineJoin "round"
                            svg.d "M7 17l9.2-9.2M17 17V7H7"
                        ]
                    ]
                ]
                Html.span [ prop.text (sprintf "+%.1f%%" pct) ]
            ]
        ]
    | Down pct ->
        Html.div [
            prop.className "flex items-center gap-1 text-neon-red text-xs md:text-sm font-medium"
            prop.children [
                Svg.svg [
                    svg.className "w-3 h-3 md:w-4 md:h-4"
                    svg.fill "none"
                    svg.viewBox (0, 0, 24, 24)
                    svg.stroke "currentColor"
                    svg.strokeWidth 2
                    svg.children [
                        Svg.path [
                            svg.strokeLineCap "round"
                            svg.strokeLineJoin "round"
                            svg.d "M17 7l-9.2 9.2M7 7v10h10"
                        ]
                    ]
                ]
                Html.span [ prop.text (sprintf "-%.1f%%" (abs pct)) ]
            ]
        ]

/// Create a stat card with the specified props
let view (props: StatProps) =
    let padding, valueSize, labelSize = sizeToClasses props.Size
    let gradientClass = accentToGradient props.Accent
    let extraClass = props.ClassName |> Option.defaultValue ""

    Html.div [
        prop.className $"bg-base-100 border border-white/5 rounded-xl {padding} relative overflow-hidden transition-all hover:border-white/10 hover:-translate-y-0.5 {extraClass}"
        prop.children [
            // Decorative gradient accent line at top
            Html.div [
                prop.className $"absolute top-0 left-0 right-0 h-[3px] bg-gradient-to-r {gradientClass}"
            ]

            // Main content
            Html.div [
                prop.className "flex flex-col gap-2"
                prop.children [
                    // Top row: Label + Icon
                    Html.div [
                        prop.className "flex items-center justify-between"
                        prop.children [
                            Html.span [
                                prop.className $"{labelSize} font-medium text-base-content/50 uppercase tracking-wider"
                                prop.text props.Label
                            ]
                            match props.Icon with
                            | Some icon ->
                                Html.div [
                                    prop.className "text-base-content/30"
                                    prop.children [ icon ]
                                ]
                            | None -> Html.none
                        ]
                    ]

                    // Value row
                    Html.div [
                        prop.className "flex items-baseline gap-2"
                        prop.children [
                            Html.span [
                                prop.className $"font-mono font-bold {valueSize} text-base-content tabular-nums"
                                prop.text props.Value
                            ]
                            match props.Trend with
                            | Some trend -> trendView trend
                            | None -> Html.none
                        ]
                    ]

                    // Optional description
                    match props.Description with
                    | Some desc ->
                        Html.p [
                            prop.className "text-xs text-base-content/40 mt-1"
                            prop.text desc
                        ]
                    | None -> Html.none
                ]
            ]
        ]
    ]

// ============================================
// Convenience Functions
// ============================================

/// Simple stat card with label and value
let simple label value =
    view { defaultProps with Label = label; Value = value }

/// Stat card with icon
let withIcon label value icon =
    view { defaultProps with Label = label; Value = value; Icon = Some icon }

/// Stat card with trend indicator
let withTrend label value trend =
    view { defaultProps with Label = label; Value = value; Trend = Some trend }

/// Stat card with icon and trend
let withIconAndTrend label value icon trend =
    view {
        defaultProps with
            Label = label
            Value = value
            Icon = Some icon
            Trend = Some trend
    }

/// Compact stat for tighter spaces
let compact label value =
    view { defaultProps with Label = label; Value = value; Size = Compact }

/// Large hero-style stat
let hero label value =
    view { defaultProps with Label = label; Value = value; Size = Large }

/// Stat with specific accent color
let withAccent label value accent =
    view { defaultProps with Label = label; Value = value; Accent = accent }

// ============================================
// Stat Grid Layout
// ============================================

/// Responsive grid for stat cards (1 col mobile, 2 tablet, 3 desktop)
let grid (stats: ReactElement list) =
    Html.div [
        prop.className "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 md:gap-4"
        prop.children stats
    ]

/// Grid with 2 columns max (for dashboards with fewer stats)
let gridTwoCol (stats: ReactElement list) =
    Html.div [
        prop.className "grid grid-cols-1 sm:grid-cols-2 gap-3 md:gap-4"
        prop.children stats
    ]

/// Grid with 4 columns on large screens
let gridFourCol (stats: ReactElement list) =
    Html.div [
        prop.className "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 md:gap-4"
        prop.children stats
    ]

// ============================================
// Specialized Stat Cards
// ============================================

/// Transaction count stat
let transactionCount count =
    withIcon "Transactions" (string count) (Icons.creditCard MD Default)

/// Sync count stat
let syncCount count =
    withIcon "Syncs" (string count) (Icons.sync MD Default)

/// Money stat (positive = green accent, negative = red-ish)
let moneyStat label (amount: decimal) currency =
    let formatted = sprintf "%+.2f %s" amount currency
    let accent = if amount >= 0m then Green else Pink
    withAccent label formatted accent

/// Category stat (purple accent)
let categoryStat label count =
    view {
        defaultProps with
            Label = label
            Value = string count
            Accent = Purple
            Icon = Some (Icons.rules MD Default)
    }

/// Rules count stat
let rulesCount count =
    withIcon "Active Rules" (string count) (Icons.rules MD Default)
