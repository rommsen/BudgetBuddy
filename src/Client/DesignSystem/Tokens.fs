module Client.DesignSystem.Tokens

/// Design tokens for the Neon Glow Dark Mode Theme.
/// These match the CSS custom properties defined in styles.css and tailwind.config.js.

// ============================================
// Color Tokens
// ============================================

/// Neon accent colors for the theme
module Colors =
    // Primary: Neon Green - Success, Growth, Positive
    let neonGreen = "text-neon-green"
    let neonGreenDim = "text-[#00cc6a]"

    // Secondary: Electric Orange - Energy, Action, Alerts
    let neonOrange = "text-neon-orange"
    let neonOrangeDim = "text-[#e55a1f]"

    // Accent: Cyber Teal - Info, Navigation, Interactive
    let neonTeal = "text-neon-teal"
    let neonTealDim = "text-[#00b894]"

    // Supporting: Electric Purple - Special, Premium
    let neonPurple = "text-neon-purple"
    let neonPurpleDim = "text-[#9333ea]"

    // Warning: Hot Pink - Attention needed
    let neonPink = "text-neon-pink"
    let neonPinkDim = "text-[#e6177f]"

    // Error: Neon Red
    let neonRed = "text-neon-red"
    let neonRedDim = "text-[#e6324f]"

    // Text colors (WCAG AA compliant on dark backgrounds)
    let textPrimary = "text-base-content"
    let textSecondary = "text-base-content/70"
    let textMuted = "text-base-content/60"  // Increased from 50 for accessibility
    let textDisabled = "text-base-content/50"  // Increased from 30 for accessibility

/// Background color tokens
module Backgrounds =
    // Dark backgrounds
    let void' = "bg-[#0a0a0f]"
    let dark = "bg-base-100"
    let surface = "bg-base-200"
    let elevated = "bg-base-300"
    let subtle = "bg-[#252836]"

    // Neon subtle backgrounds
    let greenSubtle = "bg-neon-green/10"
    let orangeSubtle = "bg-neon-orange/10"
    let tealSubtle = "bg-neon-teal/10"
    let purpleSubtle = "bg-neon-purple/10"
    let pinkSubtle = "bg-neon-pink/10"
    let redSubtle = "bg-neon-red/10"

/// Border color tokens
module Borders =
    let subtle = "border-white/5"
    let default' = "border-white/10"
    let strong = "border-white/15"

    // Neon borders
    let green = "border-neon-green"
    let orange = "border-neon-orange"
    let teal = "border-neon-teal"
    let purple = "border-neon-purple"
    let pink = "border-neon-pink"
    let red = "border-neon-red"

// ============================================
// Glow Effect Tokens
// ============================================

/// Box shadow glow effects
module Glows =
    let green = "shadow-glow-green"
    let orange = "shadow-glow-orange"
    let teal = "shadow-glow-teal"
    let purple = "shadow-glow-purple"

    // Hover states with reduced opacity
    let greenHover = "hover:shadow-glow-green"
    let orangeHover = "hover:shadow-glow-orange"
    let tealHover = "hover:shadow-glow-teal"
    let purpleHover = "hover:shadow-glow-purple"

/// Text glow effects (text-shadow)
module TextGlows =
    let green = "text-glow-green"
    let orange = "text-glow-orange"
    let teal = "text-glow-teal"

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
    let minSize = "min-h-[48px] min-w-[48px]"

// ============================================
// Common Utility Combinations
// ============================================

/// Pre-built class combinations for common patterns
module Presets =
    /// Card with neon theme styling
    let card = "bg-base-100 border border-white/5 rounded-xl p-4 md:p-6 transition-all hover:border-white/10"

    /// Glass card effect
    let glassCard = "bg-base-100/80 backdrop-blur-xl border border-white/5 rounded-xl p-4 md:p-6"

    /// Glowing card (featured)
    let glowCard = "bg-base-100 border border-neon-teal/50 rounded-xl p-4 md:p-6 shadow-glow-teal/30"

    /// Page header styling
    let pageHeader = "text-xl md:text-3xl font-bold font-display mb-4 md:mb-6"

    /// Section header styling
    let sectionHeader = "text-lg md:text-2xl font-semibold font-display mb-3 md:mb-4"

    /// Monospace number display
    let monoNumber = "font-mono font-semibold tabular-nums"

    /// Positive money amount
    let moneyPositive = "font-mono font-semibold text-neon-green text-glow-green"

    /// Negative money amount
    let moneyNegative = "font-mono font-semibold text-neon-red"

    /// Gradient text effect
    let gradientText = "bg-gradient-to-r from-neon-teal via-neon-green to-neon-orange bg-clip-text text-transparent"
