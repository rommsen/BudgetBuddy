# UI Refactoring Milestone Plan

This document outlines the step-by-step plan to refactor BudgetBuddy's UI to implement the **Neon Glow Dark Mode Theme** design system defined in `docs/DESIGN-SYSTEM.md`.

## Overview

**Current State:**
- 4 component directories (Dashboard, Settings, SyncFlow, Rules) with Types/State/View pattern
- CSS-only theming with DaisyUI defaults
- Inline Feliz code with repeated UI patterns
- No shared component library

**Target State:**
- Neon Glow Dark theme with mobile-first design
- Reusable F# UI component library
- Design tokens in F# for type-safe styling
- Consistent animations and micro-interactions
- Mobile bottom nav / desktop top nav

---

## Phase 1: Foundation (Milestones R0-R1)

### Milestone R0: Theme Configuration & CSS Foundation

**Goal:** Update Tailwind/DaisyUI configuration and implement CSS custom properties for the neon theme.

**Read First:** `docs/DESIGN-SYSTEM.md` sections 2-6, 10-11

#### Tasks

1. **Update `tailwind.config.js`**
   - Add neon color palette (green, orange, teal, purple, pink, red)
   - Add custom font families (Outfit, Orbitron, JetBrains Mono)
   - Add custom box shadows for glow effects
   - Configure DaisyUI neon theme

2. **Replace `src/Client/styles.css`**
   - Import Google Fonts
   - Add CSS custom properties for all neon colors
   - Add glow utility classes
   - Add animation keyframes (fadeIn, slideUp, scaleIn, neonPulse, gradientFlow)
   - Add component overrides (cards, buttons, inputs)
   - Add mobile-first responsive styles

3. **Update `index.html`**
   - Set `data-theme="neon"`
   - Verify font loading

#### Files to Create/Modify

| File | Action | Description |
|------|--------|-------------|
| `tailwind.config.js` | Modify | Add neon theme configuration |
| `src/Client/styles.css` | Replace | Full neon theme CSS implementation |
| `index.html` | Modify | Set data-theme="neon" |

#### Verification
- [ ] `npm run dev` shows dark theme
- [ ] Neon colors visible in browser dev tools
- [ ] Fonts load correctly (Outfit, Orbitron, JetBrains Mono)
- [ ] Glow utility classes work (.glow-green, .glow-orange, etc.)
- [ ] Animations work (.animate-fade-in, .animate-slide-up)
- [ ] `dotnet build` succeeds

---

### Milestone R1: Design Tokens & UI Primitives Module

**Goal:** Create F# modules for design tokens and reusable UI primitives.

**Read First:** `docs/DESIGN-SYSTEM.md` sections 7, 14

#### Tasks

1. **Create `src/Client/DesignSystem/Tokens.fs`**
   - Define color constants matching CSS variables
   - Define spacing scale
   - Define typography classes
   - Define animation classes

2. **Create `src/Client/DesignSystem/Primitives.fs`**
   - Container component (responsive max-width)
   - Stack component (vertical spacing)
   - Grid component (responsive columns)
   - Spacer component
   - Divider component

3. **Create `src/Client/DesignSystem/Icons.fs`**
   - Icon component with size variants
   - Define common icons (dashboard, sync, rules, settings, etc.)
   - Support for neon color variants

4. **Update `src/Client/Client.fsproj`**
   - Add DesignSystem folder
   - Set compilation order

#### Files to Create

| File | Description |
|------|-------------|
| `src/Client/DesignSystem/Tokens.fs` | Color, spacing, typography constants |
| `src/Client/DesignSystem/Primitives.fs` | Layout primitives |
| `src/Client/DesignSystem/Icons.fs` | Icon component and definitions |

#### Design Token Structure

```fsharp
module Client.DesignSystem.Tokens

// Colors
module Colors =
    let neonGreen = "text-neon-green"
    let neonOrange = "text-neon-orange"
    let neonTeal = "text-neon-teal"
    // ... etc

// Backgrounds
module Backgrounds =
    let void = "bg-[#0a0a0f]"
    let dark = "bg-base-100"
    let surface = "bg-base-200"
    // ... etc

// Glows
module Glows =
    let green = "shadow-glow-green"
    let orange = "shadow-glow-orange"
    // ... etc

// Spacing
module Spacing =
    let xs = "gap-1"   // 4px
    let sm = "gap-2"   // 8px
    let md = "gap-4"   // 16px
    let lg = "gap-6"   // 24px
    // ... etc

// Animations
module Animations =
    let fadeIn = "animate-fade-in"
    let slideUp = "animate-slide-up"
    let neonPulse = "animate-neon-pulse"
```

#### Verification
- [ ] Tokens module compiles
- [ ] Primitives render correctly
- [ ] Icons display with correct colors
- [ ] `dotnet build` succeeds

---

## Phase 2: Component Library (Milestones R2-R4)

### Milestone R2: Core UI Components

**Goal:** Create reusable Feliz components for buttons, cards, badges, and inputs.

**Read First:** `docs/DESIGN-SYSTEM.md` section 7.1-7.4

#### Tasks

1. **Create `src/Client/DesignSystem/Button.fs`**
   - Primary button (neon orange gradient with glow)
   - Secondary button (neon teal outline)
   - Ghost button (transparent)
   - Size variants (sm, md, lg)
   - Loading state with spinner
   - Mobile-first (full-width on mobile, auto on desktop)

2. **Create `src/Client/DesignSystem/Card.fs`**
   - Standard card (dark background, subtle border)
   - Glass card (glassmorphism effect)
   - Glow card (neon border with glow)
   - Card header, body, footer slots
   - Hover effects

3. **Create `src/Client/DesignSystem/Badge.fs`**
   - Success badge (neon green)
   - Warning badge (neon pink)
   - Error badge (neon red)
   - Info badge (neon teal)
   - Outline and filled variants

4. **Create `src/Client/DesignSystem/Input.fs`**
   - Text input with neon focus
   - Password input
   - Select dropdown
   - Textarea
   - Input groups (label + input + error)
   - Mobile-optimized (16px font, 48px min-height)

#### Files to Create

| File | Description |
|------|-------------|
| `src/Client/DesignSystem/Button.fs` | Button component variants |
| `src/Client/DesignSystem/Card.fs` | Card component variants |
| `src/Client/DesignSystem/Badge.fs` | Badge component variants |
| `src/Client/DesignSystem/Input.fs` | Form input components |

#### Button Component Structure

```fsharp
module Client.DesignSystem.Button

open Feliz

type ButtonVariant = Primary | Secondary | Ghost
type ButtonSize = Small | Medium | Large

type ButtonProps = {
    Text: string
    Variant: ButtonVariant
    Size: ButtonSize
    IsLoading: bool
    IsDisabled: bool
    OnClick: unit -> unit
    FullWidth: bool
    Icon: ReactElement option
}

let button (props: ButtonProps) =
    let sizeClass =
        match props.Size with
        | Small -> "btn-sm"
        | Medium -> ""
        | Large -> "btn-lg"

    let variantClass =
        match props.Variant with
        | Primary -> "btn-primary shadow-glow-orange hover:shadow-glow-orange/80"
        | Secondary -> "btn-ghost border border-neon-teal text-neon-teal hover:bg-neon-teal/10 hover:shadow-glow-teal"
        | Ghost -> "btn-ghost"

    let widthClass = if props.FullWidth then "w-full md:w-auto" else ""
    let mobileClass = "min-h-[48px] md:min-h-0"

    Html.button [
        prop.className $"btn {variantClass} {sizeClass} {widthClass} {mobileClass}"
        prop.disabled (props.IsLoading || props.IsDisabled)
        prop.onClick (fun _ -> props.OnClick())
        prop.children [
            if props.IsLoading then
                Html.span [ prop.className "loading loading-spinner loading-sm" ]
            match props.Icon with
            | Some icon -> icon
            | None -> ()
            Html.span [ prop.text props.Text ]
        ]
    ]
```

#### Verification
- [ ] All button variants render correctly
- [ ] Cards have proper hover effects
- [ ] Badges display with correct colors
- [ ] Inputs have neon focus glow
- [ ] Mobile touch targets are 48px minimum
- [ ] `dotnet build` succeeds

---

### Milestone R3: Data Display Components

**Goal:** Create components for displaying data (stats, tables, money, loading states).

**Read First:** `docs/DESIGN-SYSTEM.md` sections 7.5, 7.7

#### Tasks

1. **Create `src/Client/DesignSystem/Stats.fs`**
   - Stat card with icon, label, value
   - Gradient accent line at top
   - Trend indicator (up/down arrow)
   - Responsive grid layout

2. **Create `src/Client/DesignSystem/Money.fs`**
   - Money display with +/- coloring
   - Neon green for positive (with glow)
   - Neon red for negative
   - Monospace font (JetBrains Mono)

3. **Create `src/Client/DesignSystem/Table.fs`**
   - Responsive table wrapper
   - Table with zebra striping
   - Sortable header
   - Row hover effects
   - Mobile card view alternative

4. **Create `src/Client/DesignSystem/Loading.fs`**
   - Spinner with neon color
   - Skeleton/shimmer loader
   - Neon pulse animation
   - Page loading overlay

#### Files to Create

| File | Description |
|------|-------------|
| `src/Client/DesignSystem/Stats.fs` | Statistics card component |
| `src/Client/DesignSystem/Money.fs` | Money display component |
| `src/Client/DesignSystem/Table.fs` | Table component |
| `src/Client/DesignSystem/Loading.fs` | Loading state components |

#### Money Component Example

```fsharp
module Client.DesignSystem.Money

open Feliz
open Shared.Domain

let money (amount: Money) (showGlow: bool) =
    let isPositive = amount.Amount >= 0m
    let colorClass = if isPositive then "text-neon-green" else "text-neon-red"
    let glowClass = if showGlow && isPositive then "text-glow-green" else ""

    Html.span [
        prop.className $"font-mono font-semibold text-lg md:text-xl {colorClass} {glowClass}"
        prop.text (sprintf "%+.2f %s" amount.Amount amount.Currency)
    ]
```

#### Verification
- [ ] Stats cards display with gradient accent
- [ ] Money displays with correct colors and glow
- [ ] Tables are responsive
- [ ] Loading states have neon styling
- [ ] `dotnet build` succeeds

---

### Milestone R4: Feedback & Navigation Components

**Goal:** Create toast, modal, and navigation components.

**Read First:** `docs/DESIGN-SYSTEM.md` sections 7.6, 7.8

#### Tasks

1. **Create `src/Client/DesignSystem/Toast.fs`**
   - Toast container (fixed position)
   - Toast variants (success, error, warning, info)
   - Neon left border accent
   - Auto-dismiss with animation
   - Close button

2. **Create `src/Client/DesignSystem/Modal.fs`**
   - Modal backdrop with blur
   - Modal content with glass effect
   - Header, body, footer slots
   - Full-screen on mobile
   - Close on backdrop click
   - Animation (scale in)

3. **Create `src/Client/DesignSystem/Navigation.fs`**
   - Desktop top navbar (hidden on mobile)
   - Mobile bottom navbar (fixed)
   - Nav item with active state (neon teal)
   - Glass effect background
   - Safe area padding for mobile

4. **Update `src/Client/View.fs`**
   - Replace inline navbar with Navigation component
   - Replace inline toasts with Toast component

#### Files to Create/Modify

| File | Action | Description |
|------|--------|-------------|
| `src/Client/DesignSystem/Toast.fs` | Create | Toast notification component |
| `src/Client/DesignSystem/Modal.fs` | Create | Modal dialog component |
| `src/Client/DesignSystem/Navigation.fs` | Create | Navigation components |
| `src/Client/View.fs` | Modify | Use new navigation and toast |

#### Navigation Structure

```fsharp
module Client.DesignSystem.Navigation

open Feliz
open Client.Types

type NavItem = {
    Page: Page
    Label: string
    Icon: string  // emoji for now, SVG later
}

let navItems = [
    { Page = Dashboard; Label = "Dashboard"; Icon = "📊" }
    { Page = SyncFlow; Label = "Sync"; Icon = "🔄" }
    { Page = Rules; Label = "Rules"; Icon = "📋" }
    { Page = Settings; Label = "Settings"; Icon = "⚙️" }
]

// Desktop: horizontal top bar
let desktopNav (currentPage: Page) (dispatch: Msg -> unit) = ...

// Mobile: fixed bottom bar
let mobileNav (currentPage: Page) (dispatch: Msg -> unit) = ...

// Combined: shows appropriate nav based on screen size
let navigation (currentPage: Page) (dispatch: Msg -> unit) =
    Html.fragment [
        // Desktop nav (hidden md:flex)
        desktopNav currentPage dispatch
        // Mobile nav (flex md:hidden)
        mobileNav currentPage dispatch
    ]
```

#### Verification
- [ ] Toast notifications appear with correct styling
- [ ] Modal opens with animation
- [ ] Modal is full-screen on mobile
- [ ] Desktop shows top navbar
- [ ] Mobile shows bottom navbar
- [ ] Active nav item has neon teal color
- [ ] Safe area padding works on iPhone
- [ ] `dotnet build` succeeds

---

## Phase 3: View Migrations (Milestones R5-R8)

### Milestone R5: Main Layout & Navigation Migration

**Goal:** Refactor the main app layout and navigation to use the new design system.

#### Tasks

1. **Update `src/Client/View.fs`**
   - Replace inline navbar with Navigation component
   - Update page layout with proper container
   - Add mobile-first padding (bottom for mobile nav)
   - Update toast container placement
   - Add gradient background

2. **Update `src/Client/Types.fs`**
   - Add any new types needed for navigation state

3. **Verify responsive behavior**
   - Test mobile bottom nav
   - Test desktop top nav
   - Test nav transitions

#### Files to Modify

| File | Changes |
|------|---------|
| `src/Client/View.fs` | Use Navigation, Toast, Container components |
| `src/Client/Types.fs` | Add navigation-related types if needed |

#### Verification
- [ ] Mobile shows bottom nav with 4 items
- [ ] Desktop shows top nav bar
- [ ] Navigation transitions work
- [ ] Toasts appear in correct position
- [ ] Background is dark gradient
- [ ] `npm run dev` shows correct layout
- [ ] `dotnet build` succeeds

---

### Milestone R6: Dashboard View Migration

**Goal:** Refactor Dashboard to use new components and neon theme.

**Read First:** `docs/DESIGN-SYSTEM.md` sections 7.7, 14

#### Tasks

1. **Update `src/Client/Components/Dashboard/View.fs`**
   - Use Stats component for stat cards
   - Use Card component for action card
   - Use Button component for start sync
   - Use Table component for history
   - Add page header with display font
   - Add responsive grid for stats
   - Add animations (slide up on load)

2. **Update styling**
   - Stats grid: 1 col mobile, 2 col tablet, 3 col desktop
   - Action card with glow effect
   - History table with hover states

#### Current Dashboard Elements to Migrate

| Current | New Component |
|---------|---------------|
| Inline stat cards | Stats component |
| Start sync button | Button (Primary, Large) |
| History table | Table component |
| Loading spinner | Loading component |

#### Verification
- [ ] Stats cards have gradient accent line
- [ ] Action button has orange glow
- [ ] Grid is responsive (1/2/3 columns)
- [ ] Page header uses Orbitron font
- [ ] Animations play on page load
- [ ] `dotnet build` succeeds

---

### Milestone R7: Settings View Migration

**Goal:** Refactor Settings page to use new components.

#### Tasks

1. **Update `src/Client/Components/Settings/View.fs`**
   - Use Card component for each settings section
   - Use Input component for all form fields
   - Use Button component for actions
   - Use Badge component for status indicators
   - Add section headers with display font
   - Mobile-first form layout

2. **Settings sections to update**
   - YNAB Settings card
   - Comdirect Settings card
   - Sync Settings card
   - Budget/Account selection

#### Current Settings Elements to Migrate

| Current | New Component |
|---------|---------------|
| Inline cards | Card component |
| Text inputs | Input component |
| Password inputs | Input (password) component |
| Select dropdowns | Input (select) component |
| Save buttons | Button (Primary) component |
| Test button | Button (Secondary) component |
| Status badges | Badge component |

#### Verification
- [ ] All cards have consistent styling
- [ ] Inputs have neon focus glow
- [ ] Buttons have correct variants
- [ ] Form is usable on mobile
- [ ] Error states display correctly
- [ ] `dotnet build` succeeds

---

### Milestone R8: SyncFlow View Migration

**Goal:** Refactor SyncFlow page with transaction list and review UI.

#### Tasks

1. **Update `src/Client/Components/SyncFlow/View.fs`**
   - Use Card component for TAN waiting state
   - Use Table component for transactions
   - Use Badge component for status
   - Use Money component for amounts
   - Use Button component for actions
   - Add row color coding (green/yellow/red backgrounds)
   - Mobile card layout for transactions

2. **Transaction row updates**
   - Status badge with correct neon colors
   - Amount with Money component
   - Category dropdown styled
   - External links as ghost buttons
   - Checkbox for selection

3. **Action bar**
   - Fixed bottom on mobile (above nav)
   - Bulk action buttons
   - Import button with glow

#### Current SyncFlow Elements to Migrate

| Current | New Component |
|---------|---------------|
| TAN waiting card | Card (Glow) component |
| Transaction table | Table component |
| Status badges | Badge component |
| Amount display | Money component |
| Action buttons | Button component |
| Loading state | Loading component |

#### Verification
- [ ] TAN waiting has pulsing glow animation
- [ ] Transactions show with correct status colors
- [ ] Money displays with +/- coloring
- [ ] Bulk actions accessible on mobile
- [ ] Import button has orange glow
- [ ] External links work
- [ ] `dotnet build` succeeds

---

### Milestone R9: Rules View Migration

**Goal:** Refactor Rules management page.

#### Tasks

1. **Update `src/Client/Components/Rules/View.fs`**
   - Use Card component for page structure
   - Use Table component for rules list
   - Use Badge component for pattern type
   - Use Button component for actions
   - Use Modal component for create/edit form
   - Mobile-friendly rule cards

2. **Rule edit modal**
   - Use Input components for all fields
   - Pattern type selector
   - Target field selector
   - Category dropdown
   - Test area with result indicator
   - Full-screen on mobile

3. **Rule row**
   - Name, pattern, category display
   - Enabled toggle
   - Edit/Delete actions
   - Pattern type badge

#### Current Rules Elements to Migrate

| Current | New Component |
|---------|---------------|
| Rules table | Table component |
| Add Rule button | Button (Primary) |
| Edit/Delete buttons | Button (Ghost) |
| Toggle | Keep DaisyUI toggle |
| Edit modal | Modal component |
| Form inputs | Input components |
| Test result | Badge component |

#### Verification
- [ ] Rules table displays correctly
- [ ] Create/Edit modal is full-screen on mobile
- [ ] Pattern test shows visual result
- [ ] Import/Export buttons work
- [ ] `dotnet build` succeeds

---

## Phase 4: Polish & Animations (Milestones R10-R11)

### Milestone R10: Micro-interactions & Animations

**Goal:** Add polish with animations and hover effects.

#### Tasks

1. **Page transitions**
   - Add fade-in animation when switching pages
   - Staggered animation for list items

2. **Button interactions**
   - Scale on press (0.98)
   - Glow intensify on hover
   - Ripple effect (optional)

3. **Card interactions**
   - Subtle lift on hover
   - Border color change on hover
   - Glow effect on featured cards

4. **Loading states**
   - Neon pulse animation
   - Skeleton shimmer with gradient

5. **Form feedback**
   - Input focus animation
   - Error shake animation
   - Success checkmark animation

#### Verification
- [ ] Page transitions are smooth
- [ ] Buttons respond to interaction
- [ ] Cards have hover effects
- [ ] Loading states look polished
- [ ] Animations don't cause jank
- [ ] `dotnet build` succeeds

---

### Milestone R11: Mobile Optimization & Testing

**Goal:** Final mobile optimization and cross-device testing.

#### Tasks

1. **Touch targets**
   - Verify all buttons are 48px minimum
   - Verify inputs are 48px minimum
   - Add proper spacing between interactive elements

2. **Safe areas**
   - Test iPhone notch handling
   - Test bottom nav safe area
   - Test landscape orientation

3. **Performance**
   - Verify animations are smooth (60fps)
   - Test on low-end devices
   - Optimize large lists

4. **Accessibility**
   - Verify focus states are visible
   - Test keyboard navigation
   - Check color contrast

5. **Cross-browser testing**
   - Chrome
   - Safari (iOS)
   - Firefox

#### Verification
- [ ] Touch targets meet 48px requirement
- [ ] Safe areas work on iPhone
- [ ] Animations are smooth
- [ ] Keyboard navigation works
- [ ] Looks good on iPhone/Android
- [ ] Looks good on tablet
- [ ] Looks good on desktop
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes

---

## Phase 5: Documentation & Cleanup (Milestone R12)

### Milestone R12: Documentation & Component Showcase

**Goal:** Document the design system and clean up legacy code.

#### Tasks

1. **Create component showcase page** (optional)
   - Add /showcase route
   - Display all components with variants
   - Include code examples

2. **Update documentation**
   - Document component API in code comments
   - Update CLAUDE.md with component usage
   - Add design system quick reference

3. **Code cleanup**
   - Remove unused inline styles
   - Remove duplicate helper functions
   - Ensure consistent naming

4. **Update diary**
   - Document all changes made
   - List files added/modified/deleted

#### Verification
- [ ] All components documented
- [ ] No unused code remains
- [ ] CLAUDE.md updated
- [ ] Diary entry complete
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes

---

## Quick Reference: Files to Create

| Milestone | Files |
|-----------|-------|
| R0 | `tailwind.config.js` (modify), `styles.css` (replace) |
| R1 | `DesignSystem/Tokens.fs`, `DesignSystem/Primitives.fs`, `DesignSystem/Icons.fs` |
| R2 | `DesignSystem/Button.fs`, `DesignSystem/Card.fs`, `DesignSystem/Badge.fs`, `DesignSystem/Input.fs` |
| R3 | `DesignSystem/Stats.fs`, `DesignSystem/Money.fs`, `DesignSystem/Table.fs`, `DesignSystem/Loading.fs` |
| R4 | `DesignSystem/Toast.fs`, `DesignSystem/Modal.fs`, `DesignSystem/Navigation.fs` |
| R5-R9 | Modify existing View.fs files in Components |
| R10-R11 | Modify CSS and component files |
| R12 | Documentation updates |

---

## Estimated Effort

| Phase | Milestones | Complexity |
|-------|------------|------------|
| Foundation | R0-R1 | Medium |
| Component Library | R2-R4 | High |
| View Migrations | R5-R9 | High |
| Polish | R10-R11 | Medium |
| Documentation | R12 | Low |

---

## Notes for Implementation

1. **Test frequently** - Run `npm run dev` after each change to verify visual appearance
2. **Mobile-first** - Always design for mobile first, then add desktop enhancements
3. **Incremental migration** - Migrate one component/view at a time
4. **Preserve functionality** - Don't break existing functionality during refactoring
5. **Use existing DaisyUI** - Leverage DaisyUI components where possible, customize with Tailwind
6. **Keep it simple** - Don't over-engineer the component library
