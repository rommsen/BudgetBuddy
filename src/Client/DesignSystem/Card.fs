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

/// Empty state card
/// Kurzer leerer Zustand für schmale Spalten (Seitenleiste): Kopf und ein Satz, ohne Symbol und Aktion (§7.12).
let emptyStateCompact (title: string) (description: string) =
    Html.div [
        prop.className "rounded-lg border border-border-default border-dashed px-3 py-3"
        prop.children [
            Html.p [ prop.className "text-sm font-semibold text-text-secondary"; prop.text title ]
            Html.p [ prop.className "text-xs text-text-muted mt-0.5"; prop.text description ]
        ]
    ]

/// Nichts mehr zu tun (Prototyp Version 17 `todoList`, ledger-s1weh): eine Karte mit grünem Punkt, Kopf halbfett und
/// ein Satz, was hier erscheinen wird (§7.12). Grün heißt gelungen, alles erledigt (§2).
let allDone (testId: string) (title: string) (description: string) =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className $"flex items-center gap-3 rounded-[14px] border {Borders.subtle} {Backgrounds.dark} {Padding.md}"
        prop.children [
            Html.span [ prop.ariaHidden true; prop.className $"w-2.5 h-2.5 shrink-0 {Radius.full} bg-success" ]
            Html.div [
                prop.className "min-w-0"
                prop.children [
                    Html.p [ prop.className $"{FontSizes.emphasis} {FontWeights.semibold} {Colors.textPrimary}"; prop.text title ]
                    Html.p [ prop.className $"{FontSizes.compact} {Colors.textSecondary}"; prop.text description ]
                ]
            ]
        ]
    ]

let private sheetMessageWith (testId: string) (busy: bool) (success: bool) (title: string) (description: string) =
    Html.div [
        prop.custom ("data-testid", testId)
        // Der Wechsel von „Rufe ab…“ zum Ergebnis wird vorgelesen (Review-Fund zu banking-import-d0fey).
        prop.role "status"
        prop.custom ("aria-live", "polite")
        prop.className "flex flex-col items-center gap-2 px-5 py-9 text-center"
        prop.children [
            if busy then Loading.spinner Loading.MD Loading.Accent
            Html.p [
                prop.className $"""{FontSizes.emphasis} {FontWeights.semibold} {(if success then Colors.success else Colors.textPrimary)}"""
                prop.text title
            ]
            // 36ch: das längste Ergebnis bleibt am Handy bei höchstens sechs Zeilen.
            Html.p [ prop.className $"{FontSizes.compact} {Colors.textMuted} max-w-[36ch]"; prop.text description ]
        ]
    ]

/// Leerer Zustand oder Ergebnis in einem Sheet oder Dialog, ohne Rahmen und Symbol (Prototyp Version 17
/// `.empty`, Sheet „Abrufen“, banking-import-d0fey): Kopf halbfett, darunter ein Satz, mittig, als
/// `role status`. Ein gelungenes Ergebnis zeigt den Kopf in Grün (§2).
let sheetMessage (testId: string) (success: bool) (title: string) (description: string) =
    sheetMessageWith testId false success title description

/// Wie `sheetMessage`, während etwas läuft: ein Spinner über dem Kopf („Rufe ab…“).
let sheetMessageBusy (testId: string) (title: string) (description: string) =
    sheetMessageWith testId true false title description

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
