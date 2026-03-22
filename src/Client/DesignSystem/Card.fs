module Client.DesignSystem.Card

open Feliz
open Client.DesignSystem.Tokens

// ============================================
// Card Types
// ============================================

/// Card visual variants
type CardVariant =
    | Standard   // Dark background, subtle border
    | Glass      // Glassmorphism effect with blur
    | Glow       // Neon border with glow effect
    | Elevated   // Higher elevation with stronger shadow

/// Card size variants (affects padding)
type CardSize =
    | Compact   // Less padding
    | Normal    // Default padding
    | Spacious  // More padding

// ============================================
// Card Props
// ============================================

type CardProps = {
    Variant: CardVariant
    Size: CardSize
    Hoverable: bool
    ClassName: string option
}

let defaultProps = {
    Variant = Standard
    Size = Normal
    Hoverable = true
    ClassName = None
}

// ============================================
// Card Implementation
// ============================================

let private variantToClass = function
    | Standard ->
        "bg-surface-card border border-border-default"
    | Glass ->
        "bg-surface-card/80 backdrop-blur-xl border border-border-default"
    | Glow ->
        "bg-surface-card border border-neon-teal/50 shadow-glow-teal/30"
    | Elevated ->
        "bg-surface-elevated border border-border-subtle shadow-lg"

let private sizeToClass = function
    | Compact -> "p-3 md:p-4"
    | Normal -> "p-4 md:p-6"
    | Spacious -> "p-5 md:p-8"

let private hoverClass hoverable variant =
    if hoverable then
        match variant with
        | Standard | Elevated ->
            "hover:border-border-default hover:-translate-y-0.5 hover:shadow-lg"
        | Glass ->
            "hover:border-border-default hover:-translate-y-0.5"
        | Glow ->
            "hover:shadow-[0_0_30px_rgba(0,212,170,0.4)] hover:-translate-y-0.5"
    else ""

/// Create a card with children
let view (props: CardProps) (children: ReactElement list) =
    let variantClass = variantToClass props.Variant
    let sizeClass = sizeToClass props.Size
    let hover = hoverClass props.Hoverable props.Variant
    let extraClass = props.ClassName |> Option.defaultValue ""

    Html.div [
        prop.className $"rounded-xl {variantClass} {sizeClass} {hover} transition-all duration-200 {extraClass}"
        prop.children children
    ]

/// Standard card (simple wrapper)
let standard (children: ReactElement list) =
    view defaultProps children

/// Glass card with blur effect
let glass (children: ReactElement list) =
    view { defaultProps with Variant = Glass } children

/// Glowing card (for featured content)
let glow (children: ReactElement list) =
    view { defaultProps with Variant = Glow } children

/// Elevated card
let elevated (children: ReactElement list) =
    view { defaultProps with Variant = Elevated } children

/// Compact card (less padding)
let compact (children: ReactElement list) =
    view { defaultProps with Size = Compact } children

/// Non-hoverable card
let static' variant (children: ReactElement list) =
    view { defaultProps with Variant = variant; Hoverable = false } children

// ============================================
// Card Parts
// ============================================

/// Card header section
let header (title: string) (subtitle: string option) (action: ReactElement option) =
    Html.div [
        prop.className "flex items-start justify-between gap-4 mb-4"
        prop.children [
            Html.div [
                prop.className "flex-1 min-w-0"
                prop.children [
                    Html.h3 [
                        prop.className "text-lg md:text-xl font-semibold font-display text-text-primary truncate"
                        prop.text title
                    ]
                    match subtitle with
                    | Some sub ->
                        Html.p [
                            prop.className "text-sm text-text-muted mt-0.5"
                            prop.text sub
                        ]
                    | None -> ()
                ]
            ]
            match action with
            | Some actionEl -> actionEl
            | None -> ()
        ]
    ]

/// Simple card header (title only)
let headerSimple (title: string) =
    header title None None

/// Card body section
let body (children: ReactElement list) =
    Html.div [
        prop.className "space-y-3"
        prop.children children
    ]

/// Card footer section (for actions)
let footer (children: ReactElement list) =
    Html.div [
        prop.className "flex flex-col sm:flex-row gap-2 sm:gap-3 mt-4 pt-4 border-t border-border-subtle"
        prop.children children
    ]

/// Card footer with right-aligned actions
let footerRight (children: ReactElement list) =
    Html.div [
        prop.className "flex flex-col sm:flex-row sm:justify-end gap-2 sm:gap-3 mt-4 pt-4 border-t border-border-subtle"
        prop.children children
    ]

// ============================================
// Specialized Cards
// ============================================

/// Card with gradient accent line at top
let withAccent (children: ReactElement list) =
    Html.div [
        prop.className "rounded-xl bg-surface-card border border-border-subtle overflow-hidden hover:border-border-default hover:-translate-y-0.5 hover:shadow-lg transition-all duration-200"
        prop.children [
            // Gradient accent line
            Html.div [
                prop.className "h-1 bg-gradient-to-r from-neon-teal via-neon-green to-neon-orange"
            ]
            // Content
            Html.div [
                prop.className "p-4 md:p-6"
                prop.children children
            ]
        ]
    ]

/// Action card (featured with glow, typically for main CTA)
let action (title: string) (description: string) (actionButton: ReactElement) =
    Html.div [
        prop.className "rounded-xl bg-surface-card border border-neon-orange/30 p-4 md:p-6 shadow-[0_0_15px_rgba(255,107,44,0.2)] hover:shadow-[0_0_25px_rgba(255,107,44,0.3)] transition-all duration-200"
        prop.children [
            Html.h3 [
                prop.className "text-lg md:text-xl font-semibold font-display text-text-primary mb-2"
                prop.text title
            ]
            Html.p [
                prop.className "text-sm md:text-base text-text-muted mb-4"
                prop.text description
            ]
            actionButton
        ]
    ]

/// Stat card with icon
let stat (icon: ReactElement) (label: string) (value: string) (trend: ReactElement option) =
    Html.div [
        prop.className "rounded-xl bg-surface-card border border-border-subtle p-4 md:p-5 overflow-hidden relative group hover:border-border-default transition-all duration-200"
        prop.children [
            // Gradient accent (visible on hover)
            Html.div [
                prop.className "absolute top-0 left-0 right-0 h-0.5 bg-gradient-to-r from-neon-teal via-neon-green to-neon-orange opacity-0 group-hover:opacity-100 transition-opacity duration-200"
            ]
            Html.div [
                prop.className "flex items-start gap-3"
                prop.children [
                    Html.div [
                        prop.className "flex-shrink-0 p-2 rounded-lg bg-neon-teal/10"
                        prop.children [ icon ]
                    ]
                    Html.div [
                        prop.className "flex-1 min-w-0"
                        prop.children [
                            Html.p [
                                prop.className "text-xs font-medium uppercase tracking-wide text-text-muted/70"
                                prop.text label
                            ]
                            Html.div [
                                prop.className "flex items-baseline gap-2 mt-1"
                                prop.children [
                                    Html.p [
                                        prop.className "text-xl md:text-2xl font-bold font-mono text-text-primary"
                                        prop.text value
                                    ]
                                    match trend with
                                    | Some t -> t
                                    | None -> ()
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

/// Empty state card
let emptyState (icon: ReactElement) (title: string) (description: string) (action: ReactElement option) =
    Html.div [
        prop.className "rounded-xl bg-surface-card border border-border-default border-dashed p-8 md:p-12 text-center"
        prop.children [
            Html.div [
                prop.className "flex justify-center mb-4 text-text-muted"
                prop.children [ icon ]
            ]
            Html.h3 [
                prop.className "text-lg font-semibold text-text-secondary mb-2"
                prop.text title
            ]
            Html.p [
                prop.className "text-sm text-text-muted/70 mb-4 max-w-sm mx-auto"
                prop.text description
            ]
            match action with
            | Some a -> a
            | None -> ()
        ]
    ]
