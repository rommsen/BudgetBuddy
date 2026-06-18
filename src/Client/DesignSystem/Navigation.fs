module Client.DesignSystem.Navigation

open Feliz
open Client.DesignSystem.Tokens
open Client.DesignSystem.Icons

// ============================================
// Navigation Types
// ============================================

/// Page identifiers for navigation
/// Note: This mirrors Types.Page but is defined here to avoid circular dependencies
type NavPage =
    | SyncFlow
    | Rules
    | Settings

/// Navigation item definition
type NavItem = {
    Page: NavPage
    Label: string
    Icon: IconSize -> IconColor -> ReactElement
}

/// Define the app's navigation items
let navItems: NavItem list = [
    { Page = SyncFlow; Label = "Sync"; Icon = sync }
    { Page = Rules; Label = "Rules"; Icon = rules }
    { Page = Settings; Label = "Settings"; Icon = settings }
]

// ============================================
// Desktop Navigation (hidden on mobile)
// ============================================

/// Single desktop nav item
let private desktopNavItem (item: NavItem) (currentPage: NavPage) (onClick: NavPage -> unit) =
    let isActive = item.Page = currentPage
    Html.a [
        prop.className (
            "flex items-center gap-2.5 px-4 py-2.5 rounded-lg transition-all duration-200 cursor-pointer " +
            if isActive then
                "bg-neon-teal/15 text-neon-teal shadow-sm"
            else
                "text-text-muted hover:text-text-primary hover:bg-surface-hover"
        )
        prop.onClick (fun _ -> onClick item.Page)
        prop.children [
            item.Icon SM (if isActive then NeonTeal else Default)
            Html.span [
                prop.className "font-medium text-sm"
                prop.text item.Label
            ]
        ]
    ]

/// Brand/logo component for navbar
let private brand (onClick: unit -> unit) =
    Html.a [
        prop.className "flex items-center gap-3 cursor-pointer group"
        prop.onClick (fun _ -> onClick())
        prop.children [
            // Logo icon
            Html.div [
                prop.className (
                    "w-10 h-10 rounded-xl bg-gradient-to-br from-neon-orange to-neon-teal " +
                    "flex items-center justify-center shadow-lg group-hover:shadow-glow-orange/30 transition-shadow"
                )
                prop.children [
                    Html.span [
                        prop.className "text-xl font-bold text-white font-display"
                        prop.text "B"
                    ]
                ]
            ]
            // Brand text
            Html.span [
                prop.className "text-lg font-bold font-display gradient-text"
                prop.text "BudgetBuddy"
            ]
        ]
    ]

/// Desktop top navbar (hidden on mobile)
let desktopNav (currentPage: NavPage) (onNavigate: NavPage -> unit) =
    Html.nav [
        prop.className (
            "hidden md:flex fixed top-0 left-0 right-0 z-50 " +
            "h-16 px-6 items-center justify-between " +
            "bg-surface-app/85 backdrop-blur-xl border-b border-border-subtle"
        )
        prop.children [
            // Left: Brand
            Html.div [
                prop.className "flex-shrink-0"
                prop.children [ brand (fun () -> onNavigate SyncFlow) ]
            ]

            // Right: Nav items
            Html.div [
                prop.className "flex items-center gap-1"
                prop.children [
                    for item in navItems do
                        desktopNavItem item currentPage onNavigate
                ]
            ]
        ]
    ]

// ============================================
// Mobile Navigation (fixed bottom bar)
// ============================================

/// Single mobile nav item
let private mobileNavItem (item: NavItem) (currentPage: NavPage) (onClick: NavPage -> unit) =
    let isActive = item.Page = currentPage
    Html.a [
        prop.className (
            "flex flex-col items-center justify-center gap-0.5 py-2.5 px-4 " +
            "rounded-lg transition-all duration-200 cursor-pointer min-w-[64px] min-h-[52px] " +
            if isActive then
                "text-neon-teal"
            else
                "text-text-muted/70 active:text-text-secondary"
        )
        prop.onClick (fun _ -> onClick item.Page)
        prop.children [
            item.Icon MD (if isActive then NeonTeal else Default)
            Html.span [
                prop.className (
                    $"{FontSizes.micro} font-medium " +
                    if isActive then "text-neon-teal" else "text-text-muted/70"
                )
                prop.text item.Label
            ]
        ]
    ]

/// Mobile bottom navigation (visible only on mobile)
let mobileNav (currentPage: NavPage) (onNavigate: NavPage -> unit) =
    Html.nav [
        prop.className (
            "md:hidden fixed bottom-0 left-0 right-0 z-50 " +
            "bg-surface-app/95 backdrop-blur-xl border-t border-border-subtle " +
            "safe-area-pb"
        )
        prop.children [
            Html.div [
                prop.className "flex items-center justify-around px-2 py-1"
                prop.children [
                    for item in navItems do
                        mobileNavItem item currentPage onNavigate
                ]
            ]
        ]
    ]

/// Mobile header bar (shows brand on mobile)
let mobileHeader (onNavigate: NavPage -> unit) =
    Html.header [
        prop.className (
            "md:hidden fixed top-0 left-0 right-0 z-50 " +
            "h-14 px-4 flex items-center " +
            "bg-surface-app/95 backdrop-blur-xl border-b border-border-subtle " +
            "safe-area-pt"
        )
        prop.children [
            Html.a [
                prop.className "flex items-center gap-2 cursor-pointer"
                prop.onClick (fun _ -> onNavigate SyncFlow)
                prop.children [
                    // Small logo icon
                    Html.div [
                        prop.className (
                            "w-8 h-8 rounded-lg bg-gradient-to-br from-neon-orange to-neon-teal " +
                            "flex items-center justify-center"
                        )
                        prop.children [
                            Html.span [
                                prop.className "text-sm font-bold text-white font-display"
                                prop.text "B"
                            ]
                        ]
                    ]
                    Html.span [
                        prop.className "text-lg font-bold font-display"
                        prop.text "BudgetBuddy"
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Combined Navigation Component
// ============================================

/// Complete navigation component (shows appropriate nav based on screen size)
let navigation (currentPage: NavPage) (onNavigate: NavPage -> unit) (hideBottomNav: bool) =
    React.fragment [
        // Desktop: top navbar
        desktopNav currentPage onNavigate
        // Mobile: header + bottom nav
        mobileHeader onNavigate
        if not hideBottomNav then
            mobileNav currentPage onNavigate
    ]

// ============================================
// Page Layout Helpers
// ============================================

/// Main content wrapper with correct padding for navigation
let pageContent (children: ReactElement list) =
    Html.main [
        prop.className (
            "container mx-auto px-4 " +
            // Mobile: top header (56px) + bottom nav (safe area)
            "pt-16 pb-20 " +
            // Desktop: top navbar (64px) only
            "md:pt-20 md:pb-8 " +
            // Animation
            "animate-fade-in"
        )
        prop.children children
    ]

/// Main app wrapper with dark gradient background
let appWrapper (children: ReactElement list) =
    Html.div [
        prop.className "min-h-screen bg-surface-app"
        prop.children children
    ]
