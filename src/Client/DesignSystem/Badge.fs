module Client.DesignSystem.Badge

open Feliz
open Client.DesignSystem.Tokens

// ============================================
// Badge Types
// ============================================

/// Badge color/semantic variants
type BadgeVariant =
    | Success   // Neon green - completed, imported, positive
    | Warning   // Neon pink - needs attention, uncategorized
    | Error     // Neon red - failed, errors, negative
    | Info      // Neon teal - information, auto-categorized
    | Neutral   // Gray - default, inactive
    | Orange    // Neon orange - action needed
    | Purple    // Neon purple - special, premium

/// Badge style variants
type BadgeStyle =
    | Filled    // Solid background
    | Outline   // Border only
    | Soft      // Subtle background with colored text

/// Badge size variants
type BadgeSize =
    | Small   // Smaller text and padding
    | Medium  // Default
    | Large   // Larger for emphasis

// ============================================
// Badge Props
// ============================================

type BadgeProps = {
    Text: string
    Variant: BadgeVariant
    Style: BadgeStyle
    Size: BadgeSize
    Icon: ReactElement option
    ClassName: string option
}

let defaultProps = {
    Text = ""
    Variant = Neutral
    Style = Soft
    Size = Medium
    Icon = None
    ClassName = None
}

// ============================================
// Badge Implementation
// ============================================

let private variantToSoftClass = function
    | Success -> "bg-neon-green/10 text-neon-green border-neon-green/30"
    | Warning -> "bg-neon-pink/10 text-neon-pink border-neon-pink/30"
    | Error -> "bg-neon-red/10 text-neon-red border-neon-red/30"
    | Info -> "bg-neon-teal/10 text-neon-teal border-neon-teal/30"
    | Neutral -> "bg-base-content/10 text-base-content/70 border-base-content/20"
    | Orange -> "bg-neon-orange/10 text-neon-orange border-neon-orange/30"
    | Purple -> "bg-neon-purple/10 text-neon-purple border-neon-purple/30"

let private variantToFilledClass = function
    | Success -> "bg-neon-green text-[#0a0a0f] border-transparent"
    | Warning -> "bg-neon-pink text-[#0a0a0f] border-transparent"
    | Error -> "bg-neon-red text-white border-transparent"
    | Info -> "bg-neon-teal text-[#0a0a0f] border-transparent"
    | Neutral -> "bg-base-content/20 text-base-content border-transparent"
    | Orange -> "bg-neon-orange text-[#0a0a0f] border-transparent"
    | Purple -> "bg-neon-purple text-[#0a0a0f] border-transparent"

let private variantToOutlineClass = function
    | Success -> "bg-transparent text-neon-green border-neon-green"
    | Warning -> "bg-transparent text-neon-pink border-neon-pink"
    | Error -> "bg-transparent text-neon-red border-neon-red"
    | Info -> "bg-transparent text-neon-teal border-neon-teal"
    | Neutral -> "bg-transparent text-base-content/70 border-base-content/30"
    | Orange -> "bg-transparent text-neon-orange border-neon-orange"
    | Purple -> "bg-transparent text-neon-purple border-neon-purple"

let private styleToClass variant = function
    | Soft -> variantToSoftClass variant
    | Filled -> variantToFilledClass variant
    | Outline -> variantToOutlineClass variant

let private sizeToClass = function
    | Small -> "text-[10px] px-1.5 py-0.5"
    | Medium -> "text-xs px-2 py-0.5"
    | Large -> "text-sm px-3 py-1"

/// Create a badge with the specified props
let view (props: BadgeProps) =
    let styleClass = styleToClass props.Variant props.Style
    let sizeClass = sizeToClass props.Size
    let extraClass = props.ClassName |> Option.defaultValue ""

    Html.span [
        prop.className $"inline-flex items-center gap-1 rounded-full border font-medium whitespace-nowrap {styleClass} {sizeClass} {extraClass}"
        prop.children [
            match props.Icon with
            | Some icon -> icon
            | None -> ()
            Html.span [ prop.text props.Text ]
        ]
    ]

// ============================================
// Convenience Functions
// ============================================

/// Success badge (green)
let success text =
    view { defaultProps with Text = text; Variant = Success }

/// Warning badge (pink)
let warning text =
    view { defaultProps with Text = text; Variant = Warning }

/// Error badge (red)
let error text =
    view { defaultProps with Text = text; Variant = Error }

/// Info badge (teal)
let info text =
    view { defaultProps with Text = text; Variant = Info }

/// Neutral badge (gray)
let neutral text =
    view { defaultProps with Text = text; Variant = Neutral }

/// Orange badge
let orange text =
    view { defaultProps with Text = text; Variant = Orange }

/// Purple badge
let purple text =
    view { defaultProps with Text = text; Variant = Purple }

// ============================================
// Outline Variants
// ============================================

let successOutline text =
    view { defaultProps with Text = text; Variant = Success; Style = Outline }

let warningOutline text =
    view { defaultProps with Text = text; Variant = Warning; Style = Outline }

let errorOutline text =
    view { defaultProps with Text = text; Variant = Error; Style = Outline }

let infoOutline text =
    view { defaultProps with Text = text; Variant = Info; Style = Outline }

// ============================================
// Filled Variants
// ============================================

let successFilled text =
    view { defaultProps with Text = text; Variant = Success; Style = Filled }

let warningFilled text =
    view { defaultProps with Text = text; Variant = Warning; Style = Filled }

let errorFilled text =
    view { defaultProps with Text = text; Variant = Error; Style = Filled }

let infoFilled text =
    view { defaultProps with Text = text; Variant = Info; Style = Filled }

// ============================================
// Small Variants
// ============================================

let successSmall text =
    view { defaultProps with Text = text; Variant = Success; Size = Small }

let warningSmall text =
    view { defaultProps with Text = text; Variant = Warning; Size = Small }

let errorSmall text =
    view { defaultProps with Text = text; Variant = Error; Size = Small }

let infoSmall text =
    view { defaultProps with Text = text; Variant = Info; Size = Small }

// ============================================
// With Icon
// ============================================

let withIcon icon variant text =
    view { defaultProps with Text = text; Variant = variant; Icon = Some icon }

// ============================================
// Status-specific Badges (for transactions)
// ============================================

/// Status badge for imported transactions
let imported = success "Imported"

/// Status badge for pending review transactions
let pendingReview = warning "Review"

/// Status badge for auto-categorized transactions
let autoCategorized = info "Auto"

/// Status badge for manual categorized transactions
let manual = neutral "Manual"

/// Status badge for uncategorized transactions
let uncategorized = orange "Uncategorized"

/// Status badge for skipped transactions
let skipped = neutral "Skipped"

/// Status badge for error state
let failed = error "Failed"

// ============================================
// Count Badge (for notifications)
// ============================================

/// Small count badge (for nav items)
let count (value: int) =
    if value > 0 then
        Html.span [
            prop.className "inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 text-[10px] font-bold rounded-full bg-neon-orange text-[#0a0a0f]"
            prop.text (if value > 99 then "99+" else string value)
        ]
    else
        Html.none

/// Dot badge (simple indicator)
let dot variant =
    let colorClass =
        match variant with
        | Success -> "bg-neon-green"
        | Warning -> "bg-neon-pink"
        | Error -> "bg-neon-red"
        | Info -> "bg-neon-teal"
        | Neutral -> "bg-base-content/30"
        | Orange -> "bg-neon-orange"
        | Purple -> "bg-neon-purple"

    Html.span [
        prop.className $"inline-block w-2 h-2 rounded-full {colorClass}"
    ]

/// Pulsing dot badge (for live/active status)
let pulsingDot variant =
    let colorClass =
        match variant with
        | Success -> "bg-neon-green"
        | Warning -> "bg-neon-pink"
        | Error -> "bg-neon-red"
        | Info -> "bg-neon-teal"
        | Neutral -> "bg-base-content/30"
        | Orange -> "bg-neon-orange"
        | Purple -> "bg-neon-purple"

    Html.span [
        prop.className "relative inline-flex h-2 w-2"
        prop.children [
            Html.span [
                prop.className $"animate-ping absolute inline-flex h-full w-full rounded-full {colorClass} opacity-75"
            ]
            Html.span [
                prop.className $"relative inline-flex rounded-full h-2 w-2 {colorClass}"
            ]
        ]
    ]
