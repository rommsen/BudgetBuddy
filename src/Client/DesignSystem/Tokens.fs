module Client.DesignSystem.Tokens

/// Design tokens for the Neon Glow Dark Mode Theme.
/// These match the CSS custom properties defined in styles.css and tailwind.config.js.

// ============================================
// Color Tokens
// ============================================

/// Farbrollen (ADR 0087, Palette 2): eine Farbe, eine Bedeutung, auf jeder Seite.
/// Die Werte stehen in styles.css (`@theme`), der Kontrast-Wächter
/// scripts/contrast-guard.test.mjs prüft sie gegen die Flächen.
module Colors =
    // Akzent: Hauptaktion (gefüllter Knopf) und Auswahl/Navigation (Kante, Strich, Fläche).
    let accent = "text-accent"

    // Braucht dich, kein Fehler: zu prüfen, fehlt, ohne Kategorie. Immer mit Wort daneben.
    let attention = "text-attention"

    // Fehler und Minus.
    let error = "text-error"

    // Gelungen: Rückmeldung, alles erledigt.
    let success = "text-success"

    // Hinweis.
    let info = "text-info"

    // Text colors (using CSS variable tokens)
    let textPrimary = "text-text-primary"
    let textSecondary = "text-text-secondary"
    let textMuted = "text-text-muted"
    let textDisabled = "text-text-muted/70"  // Increased from 30 for accessibility

    // Dunkler Text auf dem Akzent und auf gefüllten Rollenfarben (Badge-Zahlen, Schrittkreise,
    // Hauptknopf). Der Token `on-accent` ist `bg-app` (styles.css).
    let onAccent = "text-on-accent"
    let onAccentMuted = "text-on-accent/70"

/// Background color tokens
module Backgrounds =
    // Dark backgrounds
    let void' = "bg-surface-app"
    let dark = "bg-surface-card"
    let surface = "bg-surface-elevated"
    let elevated = "bg-surface-input"
    let subtle = "bg-surface-input"

    /// Gefüllte Fläche der Hauptaktion (Text darauf `Colors.onAccent`)
    let accent = "bg-accent"

    // Abgeschwächte Rollenflächen
    let accentSubtle = "bg-accent/10"
    let attentionSubtle = "bg-attention/10"
    let successSubtle = "bg-success/10"
    let errorSubtle = "bg-error/10"

/// Border color tokens
module Borders =
    let subtle = "border-border-subtle"
    let default' = "border-border-default"
    /// Rand, der ein Bedienelement erkennbar macht (Eingabe, Auswahl, Knopf ohne Füllung), 3:1 (ADR 0087 Punkt 5).
    let strong = "border-border-strong"

    // Rollenränder
    let accent = "border-accent"
    let attention = "border-attention"
    let success = "border-success"
    let error = "border-error"

/// Glow nur an der Hauptaktion und an Rückmeldungen (ADR 0087 Punkt 6).
module Glows =
    let accent = "shadow-glow-accent"
    let accentHover = "hover:shadow-glow-accent"
    let success = "shadow-glow-success"
    let error = "shadow-glow-error"

    // Große Variante für den Hero-Knopf
    let accentLg = "shadow-[0_0_30px_color-mix(in_srgb,var(--color-accent)_40%,transparent)]"
    let accentHoverLg = "hover:shadow-[0_0_50px_color-mix(in_srgb,var(--color-accent)_60%,transparent)]"

/// Text glow effects (text-shadow)
module TextGlows =
    // design-system-thtpp: "text-glow-green" war keine echte Tailwind-Utility
    // (nicht in styles.css definiert), ebenso orange/teal (design-system-tg2wn): Arbitrary-Property-Syntax.
    // Die Farbe kommt aus dem Theme-Token, kein eigener rgba-Wert (ADR 0087).
    let success = "[text-shadow:0_0_10px_var(--color-success),0_0_20px_color-mix(in_srgb,var(--color-success)_50%,transparent)]"
    let accent = "[text-shadow:0_0_10px_var(--color-accent),0_0_20px_color-mix(in_srgb,var(--color-accent)_50%,transparent)]"

// ============================================
// Typography Tokens
// ============================================

/// Font family tokens
module Fonts =
    let sans = "font-sans"       // Outfit - UI text
    let display = "font-display" // Orbitron - Headers
    let mono = "font-mono"       // JetBrains Mono - Numbers/code

/// Font size tokens (mobile-first)
module FontSizes =
    // Headers
    let hero = "text-2xl md:text-5xl"      // Hero headers
    let pageTitle = "text-xl md:text-4xl"  // Page titles
    let sectionTitle = "text-lg md:text-3xl" // Section headers
    let cardTitle = "text-base md:text-2xl"  // Card titles
    let subheading = "text-base md:text-xl"  // Subheadings

    // Body text
    let lg = "text-base md:text-lg"     // Large body
    let body = "text-[15px] md:text-base" // Body text (mobile optimized)
    let sm = "text-sm"                   // Small text
    let xs = "text-xs"                   // Labels, captions
    let micro = "text-[10px]"            // Micro labels: stat captions, status chips, mono glyphs (below the xs scale by design)
    let microPlus = "text-[11px]"        // Slightly larger micro labels (e.g. desktop-only meta strings)
    let compact = "text-[13px]"          // Zwischen xs und sm: Kopf und Werte der Leiste Monatserster (Skizze, budgeting-m3x7f)

/// Typografie-Rollen für ruhige Listen (ADR 0049, Styleguide §2 "Ruhige Listen").
/// Arbiträre Größen (`text-[15px]`, `text-[10px]`) stehen nur hier, nicht in Views.
module ListRoles =
    /// Payee und Betrag: 15px am Handy, 14px ab md
    let primarySize = "text-[15px] md:text-sm"
    let primary = "text-[15px] md:text-sm font-medium text-text-primary"
    /// Kategorie: 12px, gedämpft
    let secondary = "text-xs text-text-secondary"
    /// Memo: 12px, noch stärker gedämpft
    let muted = "text-xs text-text-muted"
    /// Klein in Gelb: braucht dich, kein Fehler (`fehlt`, ADR 0087 Punkt 3).
    let missing = "text-xs text-attention"
    /// Datum: Mono 12px
    let date = "font-mono text-xs font-normal text-text-muted"
    /// Datumsgruppen-Kopf: Mono 600 10px, gesperrt
    let groupHeader = "font-mono text-[10px] font-semibold tracking-[0.16em] uppercase text-text-muted"
    /// Spaltenkopf: Mono 600 10px, gesperrt, Höhe 42px
    let columnHeader = "font-mono text-[10px] font-semibold tracking-[0.12em] uppercase text-text-muted h-[42px]"
    /// Zeilenhöhe der ruhigen Tabelle (44px)
    let rowHeight = "h-11"
    /// Die dichte Zeile der Budget-Liste: 40px, die Linie unter der Zeile zählt mit
    let denseRowHeight = "h-[39px] [&>td]:py-1"
    /// Zweizeilige Handy-Zeile: Innenabstand 11px/16px
    let mobileRowPadding = "px-4 py-[11px]"
    /// Zeile 1 der zweizeiligen Handy-Zeile: Payee links, Betrag rechts, feste Höhe
    let mobileLine1 = "flex items-center justify-between gap-3 h-5"
    /// Zeile 2: Kategorie, Memo, Kennzeichen; gleiche feste Höhe und gleicher Abstand
    /// zu Zeile 1 in jeder Zeile, auch mit Kennzeichen
    let mobileLine2 = "flex items-center gap-2 h-5 mt-0.5 min-w-0"
    /// Statuskante "zu prüfen": 3px in Gelb links (braucht dich, ADR 0087 Punkt 3), als inset-Schatten,
    /// damit die Zellinhalte nicht verrutschen. Gilt für Tabellenzeile und Handy-Zeile.
    let statusEdge = "shadow-[inset_3px_0_0_0_var(--color-attention)]"
    /// Waagerechter Zellabstand der Tabelle (liest die Variable von `Table.view`), für
    /// Zeilen, die keine `Table.cell` sind, etwa der Datumsgruppen-Kopf
    let cellPaddingX = "px-[var(--table-cell-px,0.75rem)]"

/// Font weight tokens
module FontWeights =
    let regular = "font-normal"   // 400
    let medium = "font-medium"    // 500
    let semibold = "font-semibold" // 600
    let bold = "font-bold"        // 700

// ============================================
// Spacing Tokens
// ============================================

/// Gap/spacing tokens (mobile-first)
module Spacing =
    let xs = "gap-1"   // 4px
    let sm = "gap-2"   // 8px
    let md = "gap-4"   // 16px
    let lg = "gap-6"   // 24px
    let xl = "gap-8"   // 32px
    let xxl = "gap-10 md:gap-12" // 40px/48px

/// Padding tokens
module Padding =
    let xs = "p-1"
    let sm = "p-2"
    let md = "p-4"
    let lg = "p-6"

    // Responsive padding
    let cardMobile = "p-4 md:p-6"
    let containerMobile = "px-4 md:px-6"

/// Margin tokens
module Margin =
    let xs = "m-1"
    let sm = "m-2"
    let md = "m-4"
    let lg = "m-6"

    // Section margins (mobile-first)
    let sectionMobile = "mb-4 md:mb-6"

// ============================================
// Border Radius Tokens
// ============================================

module Radius =
    let sm = "rounded-md"   // 6px - Small elements
    let md = "rounded-lg"   // 8px - Buttons, inputs
    let lg = "rounded-xl"   // 12px - Cards
    let xl = "rounded-2xl"  // 16px - Modals
    let full = "rounded-full" // Pills, circles

// ============================================
// Animation Tokens
// ============================================

module Animations =
    let fadeIn = "animate-fade-in"
    let slideUp = "animate-slide-up"
    let scaleIn = "animate-scale-in"
    let neonPulse = "animate-neon-pulse"
    let pageEnter = "animate-page-enter"
    let shake = "animate-shake"
    let successPop = "animate-success-pop"
    let checkmark = "animate-checkmark"
    let slideInRight = "animate-slide-in-right"
    let slideOutRight = "animate-slide-out-right"
    let bounceSubtle = "animate-bounce-subtle"
    let glowPulse = "animate-glow-pulse"

    // Easing curves
    let easeOutQuad = "ease-out-quad"
    let easeOutCubic = "ease-out-cubic"
    let easeOutQuart = "ease-out-quart"
    let easeOutQuint = "ease-out-quint"
    let easeInOutCubic = "ease-in-out-cubic"

/// Stagger delays for sequential animations
module StaggerDelays =
    let stagger1 = "stagger-1"
    let stagger2 = "stagger-2"
    let stagger3 = "stagger-3"
    let stagger4 = "stagger-4"
    let stagger5 = "stagger-5"
    let stagger6 = "stagger-6"
    let stagger7 = "stagger-7"
    let stagger8 = "stagger-8"
    let stagger9 = "stagger-9"
    let stagger10 = "stagger-10"

    /// Get stagger class for index (0-based)
    let forIndex (i: int) =
        match i % 10 with
        | 0 -> stagger1
        | 1 -> stagger2
        | 2 -> stagger3
        | 3 -> stagger4
        | 4 -> stagger5
        | 5 -> stagger6
        | 6 -> stagger7
        | 7 -> stagger8
        | 8 -> stagger9
        | _ -> stagger10

/// Transition tokens
module Transitions =
    let fast = "transition-all duration-150 ease-out"
    let normal = "transition-all duration-200 ease-out"
    let slow = "transition-all duration-300 ease-out"
    let spring = "transition-all duration-500 ease-[cubic-bezier(0.34,1.56,0.64,1)]"

// ============================================
// Responsive Breakpoints (for reference)
// ============================================

/// Breakpoint reference - use Tailwind prefixes (sm:, md:, lg:, xl:)
module Breakpoints =
    // sm: 640px  - Small tablets
    // md: 768px  - Tablets/small laptops
    // lg: 1024px - Laptops/desktops
    // xl: 1280px - Large desktops

    // Mobile-first means no prefix = mobile
    // Then add md: or lg: for larger screens
    let mobileOnly = "md:hidden"
    let desktopOnly = "hidden md:block"
    let tabletUp = "sm:block"

// ============================================
// Z-Index Tokens
// ============================================

module ZIndex =
    let dropdown = "z-10"
    let sticky = "z-20"
    let fixed' = "z-30"
    let modal = "z-40"
    let toast = "z-50"

// ============================================
// Touch Target Tokens
// ============================================

/// Minimum touch target size for mobile (48px)
module TouchTargets =
    let minHeight = "min-h-[48px] md:min-h-0"
    /// Im Sheet, das bis 1023 px gilt: 48 px bis dorthin, darüber im Panel frei.
    let sheetMinHeight = "min-h-[48px] lg:min-h-0"
    /// 48 px auf jeder Breite.
    let alwaysMinHeight = "min-h-[48px]"
    let minSize = "min-h-[48px] min-w-[48px]"
    /// 48 px Mindesthöhe nur als Handy-Wert: der Aufrufer setzt den PC-Wert mit `md:min-h-…` dahinter.
    let mobileMinHeight = "min-h-[48px]"
    /// 48 × 48 px als Handy-Wert: der Aufrufer setzt den PC-Wert mit `md:h-… md:w-…` dahinter.
    let mobileSquare = "h-[48px] w-[48px]"
    /// 48 × 48 px am Handy, ohne das umgebende Element höher zu machen (der negative Rand gleicht die Höhe aus); ab md
    /// ohne Mindestmaß und ohne Rand. Für kleine Knöpfe in einer festen Zeile wie im Toast.
    let mobileOverlap = "min-h-[48px] min-w-[48px] -my-3 md:min-h-0 md:min-w-0 md:my-0"

// ============================================
// Common Utility Combinations
// ============================================

/// Pre-built class combinations for common patterns
module Presets =
    /// Card with neon theme styling
    let card = "bg-surface-card border border-border-default rounded-xl p-4 md:p-6 transition-all hover:border-border-default"

    /// Glass card effect
    let glassCard = "bg-surface-card/80 backdrop-blur-xl border border-border-default rounded-xl p-4 md:p-6"

    /// Glowing card (featured)
    let glowCard = "bg-surface-card border border-accent/50 rounded-xl p-4 md:p-6"

    /// Page header styling
    let pageHeader = "text-xl md:text-3xl font-bold font-display mb-4 md:mb-6"

    /// Section header styling
    let sectionHeader = "text-lg md:text-2xl font-semibold font-display mb-3 md:mb-4"

    /// Monospace number display
    let monoNumber = "font-mono font-semibold tabular-nums"

    /// Positive money amount
    let moneyPositive = $"font-mono font-semibold text-success {TextGlows.success}"

    /// Negative money amount
    let moneyNegative = "font-mono font-semibold text-error"

    /// Gradient text effect
    let gradientText = "bg-gradient-to-r from-accent to-success bg-clip-text text-transparent"
