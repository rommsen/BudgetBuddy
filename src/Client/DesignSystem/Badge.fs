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
    Size: BadgeSize
    Icon: ReactElement option
    ClassName: string option
}

let defaultProps = {
    Text = ""
    Variant = Neutral
    Size = Medium
    Icon = None
    ClassName = None
}

// ============================================
// Badge Implementation
// ============================================

/// Die Farb- und Rahmenklassen einer Variante: zarte Fläche, Text und Rand in der Rollenfarbe
/// (öffentlich für Tests der Farbrollen, ADR 0087).
let variantClass = function
    | Success -> "bg-success/10 text-success border-success/30"
    | Warning -> "bg-attention/10 text-attention border-attention/30"
    | Error -> "bg-error/10 text-error border-error/30"
    | Info -> "bg-info/10 text-info border-info/30"
    | Neutral -> "bg-text-muted/10 text-text-secondary border-text-muted/20"

let private sizeToClass = function
    | Small -> $"{FontSizes.micro} px-1.5 py-0.5"
    | Medium -> "text-xs px-2 py-0.5"
    | Large -> "text-sm px-3 py-1"

/// Create a badge with the specified props
let view (props: BadgeProps) =
    let styleClass = variantClass props.Variant
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
// Small Variants
// ============================================

let warningSmall text =
    view { defaultProps with Text = text; Variant = Warning; Size = Small }

let errorSmall text =
    view { defaultProps with Text = text; Variant = Error; Size = Small }

/// Klein und neutral: ein Stand ohne Wertung.
let neutralSmall text =
    view { defaultProps with Text = text; Variant = Neutral; Size = Small }

// ============================================
// Lena (ADR 0087 Punkt 8)
// ============================================

/// Die Flag-Farben, die styles.css über `data-lena-flag` kennt (YNABs Flag-Farben).
let private lenaFlagColors = set [ "red"; "orange"; "yellow"; "green"; "blue"; "purple" ]

/// Wert für `data-lena-flag` aus der gespeicherten `lena_flag_color`: klein geschrieben, wenn styles.css
/// die Farbe kennt, sonst `None` (dann bleibt das Flag-Symbol gedämpft).
let lenaFlagAttr (color: string option) : string option =
    color
    |> Option.map (fun c -> c.Trim().ToLowerInvariant())
    |> Option.filter lenaFlagColors.Contains

/// Setzt `data-lena-flag` auf einem Vorfahren von `lenaMark`, ohne bekannte Farbe nichts.
let lenaFlagScope (color: string option) : IReactProperty list =
    match lenaFlagAttr color with
    | Some value -> [ prop.custom ("data-lena-flag", value) ]
    | None -> []

/// Das Kennzeichen *Rückfrage Lena*: Flag-Symbol und Name, ohne farbiges Badge. Die Farbe aus
/// `lena_flag_color` steht nur im Symbol (`Icons.LenaFlag`, `data-lena-flag` in styles.css), der
/// Text bleibt neutral. `text` enthält immer den Namen "Lena".
/// Die Farbe kommt von einem Vorfahren mit `data-lena-flag` (`lenaFlagScope`). In einem Portal
/// (Modal, BottomSheet) fehlt dieser Vorfahr, dort bleibt das Symbol gedämpft.
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
// Status-specific Badges (for transactions)
// ============================================

/// Status badge for imported transactions
let imported () = success "Imported"

/// Status badge for pending review transactions
let pendingReview () = warning "Review"

/// Status badge for auto-categorized transactions
let autoCategorized () = info "Auto"

/// Status badge for uncategorized transactions
let uncategorized () = warning "Uncategorized"

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
