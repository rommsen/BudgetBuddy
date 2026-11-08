module Client.DesignSystem.Badge

open Feliz
open Client.DesignSystem.Tokens

// ============================================
// Badge Types
// ============================================

/// Badge-Varianten nach Farbrolle (ADR 0087): eine Farbe, eine Bedeutung, auf jeder Seite.
type BadgeVariant =
    | Success   // Grün: gelungen, importiert
    | Warning   // Gelb: braucht dich, kein Fehler (zu prüfen, ohne Kategorie, in YNAB geändert)
    | Error     // Rot: Fehler
    | Info      // Blau: Hinweis
    | Neutral   // Grau: ein Stand ohne Wertung

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
    | Success -> "bg-success/10 text-success border-success/30"
    | Warning -> "bg-attention/10 text-attention border-attention/30"
    | Error -> "bg-error/10 text-error border-error/30"
    | Info -> "bg-info/10 text-info border-info/30"
    | Neutral -> "bg-text-muted/10 text-text-secondary border-text-muted/20"

let private variantToFilledClass = function
    | Success -> $"bg-success {Colors.onAccent} border-transparent"
    | Warning -> $"bg-attention {Colors.onAccent} border-transparent"
    | Error -> $"bg-error {Colors.onAccent} border-transparent"
    | Info -> $"bg-info {Colors.onAccent} border-transparent"
    | Neutral -> "bg-text-muted/20 text-text-primary border-transparent"

let private variantToOutlineClass = function
    | Success -> "bg-transparent text-success border-success"
    | Warning -> "bg-transparent text-attention border-attention"
    | Error -> "bg-transparent text-error border-error"
    | Info -> "bg-transparent text-info border-info"
    | Neutral -> "bg-transparent text-text-secondary border-text-muted/30"

/// Die Farb- und Rahmenklassen einer Variante je Stil (öffentlich für Tests der Farbrollen, ADR 0087).
let variantClass variant = function
    | Soft -> variantToSoftClass variant
    | Filled -> variantToFilledClass variant
    | Outline -> variantToOutlineClass variant

let private sizeToClass = function
    | Small -> $"{FontSizes.micro} px-1.5 py-0.5"
    | Medium -> "text-xs px-2 py-0.5"
    | Large -> "text-sm px-3 py-1"

/// Create a badge with the specified props
let view (props: BadgeProps) =
    let styleClass = variantClass props.Variant props.Style
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

/// Gelb: braucht dich, kein Fehler (ADR 0087 Punkt 3). Immer mit Wort.
let warning text =
    view { defaultProps with Text = text; Variant = Warning }

/// Error badge (red)
let error text =
    view { defaultProps with Text = text; Variant = Error }

/// Blau: Hinweis
let info text =
    view { defaultProps with Text = text; Variant = Info }

/// Neutral badge (gray)
let neutral text =
    view { defaultProps with Text = text; Variant = Neutral }

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

/// Klein und neutral: ein Stand ohne Wertung.
let neutralSmall text =
    view { defaultProps with Text = text; Variant = Neutral; Size = Small }

// ============================================
// Lena (ADR 0087 Punkt 8)
// ============================================

/// Das Kennzeichen *Rückfrage Lena*: Flag-Symbol und Name, ohne farbiges Badge. Die Farbe aus
/// `lena_flag_color` steht nur im Symbol (`Icons.LenaFlag`, `data-lena-flag` in styles.css), der
/// Text bleibt neutral. `text` enthält immer den Namen "Lena".
let lenaMark (text: string) =
    Html.span [
        prop.className $"inline-flex items-center gap-0.5 {FontSizes.micro} {Colors.textSecondary} whitespace-nowrap"
        prop.custom ("data-testid", "lena-mark")
        prop.children [
            Html.span [
                prop.role "img"
                prop.ariaLabel "Flag"
                prop.className "inline-flex"
                prop.children [ Icons.flag Icons.XS Icons.LenaFlag ]
            ]
            Html.span [ prop.text text ]
        ]
    ]

// ============================================
// With Icon
// ============================================

let withIcon icon variant text =
    view { defaultProps with Text = text; Variant = variant; Icon = Some icon }

// ============================================
// Status-specific Badges (for transactions)
// ============================================

/// Status badge for imported transactions
let imported () = success "Imported"

/// Status badge for pending review transactions
let pendingReview () = warning "Review"

/// Status badge for auto-categorized transactions
let autoCategorized () = info "Auto"

/// Status badge for manual categorized transactions
let manual () = neutral "Manual"

/// Status badge for uncategorized transactions
let uncategorized () = warning "Uncategorized"

/// Status badge for skipped transactions
let skipped () = neutral "Skipped"

/// Status badge for error state
let failed () = error "Failed"

// ============================================
// Count Badge (for notifications)
// ============================================

/// Small count badge (for nav items)
let count (value: int) =
    if value > 0 then
        Html.span [
            prop.className $"inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 {FontSizes.micro} font-bold rounded-full bg-attention {Colors.onAccent}"
            prop.text (if value > 99 then "99+" else string value)
        ]
    else
        Html.none

/// Dot badge (simple indicator)
let dot variant =
    let colorClass =
        match variant with
        | Success -> "bg-success"
        | Warning -> "bg-attention"
            | Error -> "bg-error"
        | Info -> "bg-info"
        | Neutral -> "bg-text-muted/30"
        
    Html.span [
        prop.className $"inline-block w-2 h-2 rounded-full {colorClass}"
    ]

/// Pulsing dot badge (for live/active status)
let pulsingDot variant =
    let colorClass =
        match variant with
        | Success -> "bg-success"
        | Warning -> "bg-attention"
            | Error -> "bg-error"
        | Info -> "bg-info"
        | Neutral -> "bg-text-muted/30"
        
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
