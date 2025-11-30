module Client.DesignSystem.Primitives

open Feliz
open Client.DesignSystem.Tokens

// ============================================
// Layout Primitives
// ============================================

/// Responsive container with max-width constraints
module Container =
    /// Full-width container with responsive padding
    let view (children: ReactElement list) =
        Html.div [
            prop.className "w-full px-4 md:px-6 mx-auto max-w-7xl"
            prop.children children
        ]

    /// Container with narrow max-width (for forms, settings)
    let narrow (children: ReactElement list) =
        Html.div [
            prop.className "w-full px-4 md:px-6 mx-auto max-w-2xl"
            prop.children children
        ]

    /// Container with medium max-width
    let medium (children: ReactElement list) =
        Html.div [
            prop.className "w-full px-4 md:px-6 mx-auto max-w-4xl"
            prop.children children
        ]

// ============================================
// Stack - Vertical spacing
// ============================================

type StackSize =
    | XS   // gap-1 (4px)
    | SM   // gap-2 (8px)
    | MD   // gap-4 (16px)
    | LG   // gap-6 (24px)
    | XL   // gap-8 (32px)

module Stack =
    let private sizeToClass = function
        | XS -> "gap-1"
        | SM -> "gap-2"
        | MD -> "gap-4"
        | LG -> "gap-6"
        | XL -> "gap-8"

    /// Vertical stack with specified gap
    let view (size: StackSize) (children: ReactElement list) =
        Html.div [
            prop.className $"flex flex-col {sizeToClass size}"
            prop.children children
        ]

    /// Default stack with medium gap
    let md (children: ReactElement list) = view MD children

    /// Small gap stack
    let sm (children: ReactElement list) = view SM children

    /// Large gap stack
    let lg (children: ReactElement list) = view LG children

// ============================================
// HStack - Horizontal spacing
// ============================================

module HStack =
    let private sizeToClass = function
        | XS -> "gap-1"
        | SM -> "gap-2"
        | MD -> "gap-4"
        | LG -> "gap-6"
        | XL -> "gap-8"

    /// Horizontal stack with specified gap
    let view (size: StackSize) (children: ReactElement list) =
        Html.div [
            prop.className $"flex flex-row items-center {sizeToClass size}"
            prop.children children
        ]

    /// Default horizontal stack with medium gap
    let md (children: ReactElement list) = view MD children

    /// Small gap horizontal stack
    let sm (children: ReactElement list) = view SM children

    /// Horizontal stack with space-between
    let spaceBetween (children: ReactElement list) =
        Html.div [
            prop.className "flex flex-row items-center justify-between gap-4"
            prop.children children
        ]

    /// Horizontal stack that wraps on mobile
    let wrap (size: StackSize) (children: ReactElement list) =
        Html.div [
            prop.className $"flex flex-wrap items-center {sizeToClass size}"
            prop.children children
        ]

// ============================================
// Grid - Responsive grid layouts
// ============================================

type GridCols =
    | One
    | Two
    | Three
    | Four
    | Auto

module Grid =
    let private colsToClass = function
        | One -> "grid-cols-1"
        | Two -> "grid-cols-1 sm:grid-cols-2"
        | Three -> "grid-cols-1 sm:grid-cols-2 lg:grid-cols-3"
        | Four -> "grid-cols-1 sm:grid-cols-2 lg:grid-cols-4"
        | Auto -> "grid-cols-[repeat(auto-fit,minmax(280px,1fr))]"

    /// Responsive grid with specified columns
    let view (cols: GridCols) (children: ReactElement list) =
        Html.div [
            prop.className $"grid gap-3 md:gap-4 {colsToClass cols}"
            prop.children children
        ]

    /// 2-column responsive grid
    let two (children: ReactElement list) = view Two children

    /// 3-column responsive grid (for stats cards)
    let three (children: ReactElement list) = view Three children

    /// Auto-fit grid (cards)
    let auto (children: ReactElement list) = view Auto children

// ============================================
// Spacer - Empty space
// ============================================

type SpacerSize =
    | SpacerXS  // h-1 (4px)
    | SpacerSM  // h-2 (8px)
    | SpacerMD  // h-4 (16px)
    | SpacerLG  // h-6 (24px)
    | SpacerXL  // h-8 (32px)

module Spacer =
    let private sizeToClass = function
        | SpacerXS -> "h-1"
        | SpacerSM -> "h-2"
        | SpacerMD -> "h-4"
        | SpacerLG -> "h-6"
        | SpacerXL -> "h-8"

    /// Vertical spacer with specified height
    let view (size: SpacerSize) =
        Html.div [
            prop.className (sizeToClass size)
            prop.ariaHidden true
        ]

    let xs = view SpacerXS
    let sm = view SpacerSM
    let md = view SpacerMD
    let lg = view SpacerLG
    let xl = view SpacerXL

    /// Flexible spacer that grows to fill space
    let flex =
        Html.div [
            prop.className "flex-1"
            prop.ariaHidden true
        ]

// ============================================
// Divider - Horizontal line separator
// ============================================

module Divider =
    /// Subtle horizontal divider
    let horizontal =
        Html.hr [
            prop.className "border-t border-white/5 my-4"
        ]

    /// Divider with more spacing
    let large =
        Html.hr [
            prop.className "border-t border-white/5 my-6 md:my-8"
        ]

    /// Divider with gradient accent
    let gradient =
        Html.div [
            prop.className "h-px bg-gradient-to-r from-transparent via-neon-teal/30 to-transparent my-4"
        ]

    /// Vertical divider (for horizontal layouts)
    let vertical =
        Html.div [
            prop.className "w-px h-6 bg-white/10"
            prop.ariaHidden true
        ]

// ============================================
// Center - Center content
// ============================================

module Center =
    /// Center content both horizontally and vertically
    let view (children: ReactElement list) =
        Html.div [
            prop.className "flex items-center justify-center"
            prop.children children
        ]

    /// Center content horizontally only
    let horizontal (children: ReactElement list) =
        Html.div [
            prop.className "flex justify-center"
            prop.children children
        ]

    /// Center text
    let text (children: ReactElement list) =
        Html.div [
            prop.className "text-center"
            prop.children children
        ]

// ============================================
// Page Layout
// ============================================

module Page =
    /// Page content wrapper with proper padding for nav bars
    /// Bottom padding for mobile nav, top padding for desktop nav
    let content (children: ReactElement list) =
        Html.main [
            prop.className "pt-4 pb-24 md:pt-20 md:pb-8 min-h-screen"
            prop.children children
        ]

    /// Page header section
    let header (title: string) (subtitle: string option) =
        Html.div [
            prop.className "space-y-1 mb-4 md:mb-6"
            prop.children [
                Html.h1 [
                    prop.className Presets.pageHeader
                    prop.text title
                ]
                match subtitle with
                | Some sub ->
                    Html.p [
                        prop.className "text-sm md:text-base text-base-content/60"
                        prop.text sub
                    ]
                | None -> ()
            ]
        ]

    /// Page section with header
    let section (title: string) (children: ReactElement list) =
        Html.section [
            prop.className "mb-6 md:mb-8"
            prop.children [
                Html.h2 [
                    prop.className Presets.sectionHeader
                    prop.text title
                ]
                yield! children
            ]
        ]

// ============================================
// Responsive Visibility
// ============================================

module Responsive =
    /// Show only on mobile (hidden on md and up)
    let mobileOnly (children: ReactElement list) =
        Html.div [
            prop.className "md:hidden"
            prop.children children
        ]

    /// Show only on desktop (hidden below md)
    let desktopOnly (children: ReactElement list) =
        Html.div [
            prop.className "hidden md:block"
            prop.children children
        ]

    /// Show on mobile as flex, hide on desktop
    let mobileOnlyFlex (children: ReactElement list) =
        Html.div [
            prop.className "flex md:hidden"
            prop.children children
        ]

    /// Hide on mobile, show as flex on desktop
    let desktopOnlyFlex (children: ReactElement list) =
        Html.div [
            prop.className "hidden md:flex"
            prop.children children
        ]

// ============================================
// Scroll Container
// ============================================

module ScrollContainer =
    /// Horizontal scroll container (for tables on mobile)
    let horizontal (children: ReactElement list) =
        Html.div [
            prop.className "overflow-x-auto -mx-4 px-4 md:mx-0 md:px-0"
            prop.children children
        ]

    /// Vertical scroll container with max height
    let vertical (maxHeight: string) (children: ReactElement list) =
        Html.div [
            prop.className $"overflow-y-auto {maxHeight}"
            prop.children children
        ]
