module Client.DesignSystem.Button

open Feliz
open Client.DesignSystem.Tokens
open Client.DesignSystem.Icons

// ============================================
// Button Types
// ============================================

/// Button visual variants
type ButtonVariant =
    | Primary    // Neon orange gradient with glow
    | Secondary  // Neutral mit grauem Rand (ADR 0075)
    | Ghost      // Transparent background
    | Danger     // Neon red for destructive actions

/// Button size variants
type ButtonSize =
    | Small   // btn-sm
    | Medium  // default
    | Large   // btn-lg

// ============================================
// Button Props
// ============================================

type ButtonProps = {
    Text: string
    Variant: ButtonVariant
    Size: ButtonSize
    IsLoading: bool
    IsDisabled: bool
    OnClick: unit -> unit
    FullWidth: bool
    Icon: ReactElement option
    IconPosition: IconPosition
    ClassName: string option
    Title: string option  // Tooltip/accessibility title
    /// Name für Screenreader; Pflicht bei einem Knopf nur mit Icon
    AriaLabel: string option
}

and IconPosition = Left | Right

/// Default button props
let defaultProps = {
    Text = ""
    Variant = Primary
    Size = Medium
    IsLoading = false
    IsDisabled = false
    OnClick = ignore
    FullWidth = false
    Icon = None
    IconPosition = Left
    ClassName = None
    Title = None
    AriaLabel = None
}

// ============================================
// Button Implementation
// ============================================

/// Größenklassen je Button-Größe. Small ist mobil 48 px hoch (Touch-Fläche, Styleguide §4), ab `md` ohne Mindesthöhe.
let private sizeToClass = function
    | Small -> "text-xs px-3 py-1.5 min-h-[48px] md:min-h-0"
    | Medium -> "text-sm px-[18px] py-2.5 min-h-[48px] md:min-h-0"
    | Large -> "text-base px-6 py-3 min-h-[52px] md:min-h-0"

/// Icon-only-Knöpfe (Small ohne Text) sind mobil auch 48 px breit, ab `md` ohne Mindestbreite.
let iconOnlyClass (size: ButtonSize) (text: string) =
    if size = Small && System.String.IsNullOrEmpty text then "min-w-[48px] md:min-w-0" else ""

let private variantToClass = function
    | Primary ->
        "bg-gradient-to-br from-neon-orange to-[#e55a1f] border-none text-white shadow-glow-orange hover:shadow-[0_0_25px_rgba(255,107,44,0.5),0_0_50px_rgba(255,107,44,0.3)] hover:-translate-y-0.5 active:scale-[0.98]"
    | Secondary ->
        "bg-surface-elevated border border-border-default text-text-primary hover:bg-surface-hover active:scale-[0.98]"
    | Ghost ->
        "text-text-secondary hover:text-text-primary hover:bg-surface-hover"
    | Danger ->
        "bg-neon-red/10 border border-neon-red/20 text-neon-red hover:bg-neon-red/20 active:scale-[0.98]"

/// Setzt der Aufrufer in `ClassName` eine eigene Anzeige-Klasse ohne Breakpoint (z. B. `hidden` in `hidden md:inline-flex`)?
/// Dann entfällt das feste `inline-flex`: Tailwind 4 ordnet Utilities im erzeugten CSS,
/// nicht nach der Reihenfolge im class-Attribut, und `inline-flex` würde `hidden`
/// sonst überstimmen.
let callerSetsDisplay (extraClass: string) =
    extraClass.Split([| ' ' |], System.StringSplitOptions.RemoveEmptyEntries)
    |> Array.exists (fun c ->
        match c with
        | "hidden" | "block" | "inline-block" | "inline" | "flex" | "inline-flex" | "grid" | "inline-grid" -> true
        | _ -> false)

/// Create a button with the specified props
let view (props: ButtonProps) =
    let sizeClass = sizeToClass props.Size
    let iconOnly = iconOnlyClass props.Size props.Text
    let variantClass = variantToClass props.Variant
    let widthClass = if props.FullWidth then "w-full md:w-auto" else ""
    let extraClass = props.ClassName |> Option.defaultValue ""
    let displayClass = if callerSetsDisplay extraClass then "" else "inline-flex"
    let disabledClass = "disabled:opacity-50 disabled:cursor-not-allowed disabled:shadow-none disabled:hover:shadow-none disabled:hover:translate-y-0"

    Html.button [
        prop.className $"{displayClass} items-center justify-center gap-1.5 rounded-lg font-semibold transition-all duration-200 {variantClass} {sizeClass} {iconOnly} {widthClass} {disabledClass} {extraClass}"
        prop.disabled (props.IsLoading || props.IsDisabled)
        prop.onClick (fun _ -> props.OnClick())
        if props.Title.IsSome then prop.title props.Title.Value
        if props.AriaLabel.IsSome then prop.ariaLabel props.AriaLabel.Value
        prop.children [
            // Loading spinner
            if props.IsLoading then
                Html.span [
                    prop.className "proto-spinner sm"
                ]

            // Icon (left position)
            match props.Icon, props.IconPosition with
            | Some icon, Left when not props.IsLoading -> icon
            | _ -> ()

            // Text
            if not (System.String.IsNullOrEmpty props.Text) then
                Html.span [ prop.text props.Text ]

            // Icon (right position)
            match props.Icon, props.IconPosition with
            | Some icon, Right when not props.IsLoading -> icon
            | _ -> ()
        ]
    ]

// ============================================
// Convenience Functions
// ============================================

/// Primary button (orange gradient with glow)
let primary text onClick =
    view { defaultProps with Text = text; OnClick = onClick; Variant = Primary }

/// Primary button with loading state
let primaryLoading text isLoading onClick =
    view { defaultProps with Text = text; OnClick = onClick; Variant = Primary; IsLoading = isLoading }

/// Primary button with icon
let primaryWithIcon text icon onClick =
    view { defaultProps with Text = text; OnClick = onClick; Variant = Primary; Icon = Some icon }

/// Secondary button: der neutrale Standard-Knopf (ADR 0075)
let secondary text onClick =
    view { defaultProps with Text = text; OnClick = onClick; Variant = Secondary }

/// Secondary button with loading state
let secondaryLoading text isLoading onClick =
    view { defaultProps with Text = text; OnClick = onClick; Variant = Secondary; IsLoading = isLoading }

/// Ghost button (transparent)
let ghost text onClick =
    view { defaultProps with Text = text; OnClick = onClick; Variant = Ghost }

/// Ghost button with icon only (no text)
let ghostIcon icon onClick =
    view { defaultProps with OnClick = onClick; Variant = Ghost; Icon = Some icon }

/// Danger button (red outline)
let danger text onClick =
    view { defaultProps with Text = text; OnClick = onClick; Variant = Danger }

/// Full-width primary button (for mobile forms)
let primaryFullWidth text onClick =
    view { defaultProps with Text = text; OnClick = onClick; Variant = Primary; FullWidth = true }

/// Full-width secondary button
let secondaryFullWidth text onClick =
    view { defaultProps with Text = text; OnClick = onClick; Variant = Secondary; FullWidth = true }

// ============================================
// Icon Button Variants
// ============================================

/// Small icon-only button
let iconButton icon variant onClick =
    view {
        defaultProps with
            OnClick = onClick
            Variant = variant
            Icon = Some icon
            Size = Small
    }

/// Edit button (ghost with edit icon)
let editButton onClick =
    iconButton (Icons.edit SM Default) Ghost onClick

/// Delete button (ghost with trash icon)
let deleteButton onClick =
    iconButton (Icons.trash SM Error) Ghost onClick

/// Add button (primary with plus icon)
let addButton text onClick =
    view {
        defaultProps with
            Text = text
            OnClick = onClick
            Variant = Primary
            Icon = Some (Icons.plus SM IconColor.Primary)
    }

// ============================================
// Button Group
// ============================================

/// Group buttons horizontally with proper spacing
let group (buttons: ReactElement list) =
    Html.div [
        prop.className "flex flex-col sm:flex-row gap-2 sm:gap-3"
        prop.children buttons
    ]

/// Group buttons horizontally, full width on mobile
let groupMobile (buttons: ReactElement list) =
    Html.div [
        prop.className "flex flex-col gap-2 sm:flex-row sm:gap-3"
        prop.children buttons
    ]


// ============================================
// Knöpfe in Listen
// ============================================

/// Der Name in einer Listenzeile als Knopf, der etwas öffnet: sieht aus wie der Name, unterstreicht
/// beim Überfahren. `touchTarget` gibt am Handy 48 px Trefferfläche, ohne die Zeile höher zu machen;
/// `relative` hebt den Knopf über die zweite Zeile, deren Text sonst die unteren 8 px überdeckt.
let listName (testId: string) (ariaLabel: string) (touchTarget: bool) (name: string) (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.ariaLabel ariaLabel
        prop.title name
        prop.className (
            if touchTarget then
                $"{ListRoles.primary} {TouchTargets.minSize} relative -my-3 truncate min-w-0 text-left cursor-pointer touch-manipulation hover:underline"
            else
                $"{ListRoles.primary} truncate min-w-0 text-left cursor-pointer touch-manipulation hover:underline"
        )
        prop.onClick (fun _ -> onClick ())
        prop.text name
    ]

let private listRowClass =
    $"block w-full text-left cursor-pointer touch-manipulation {ListRoles.mobileRowPadding} border-b border-border-subtle"

/// Eine ganze zweizeilige Listenzeile als Knopf. In einem `button` sind nur Inline-Elemente erlaubt:
/// die Kinder sind `Html.span`.
let listRow (key: string) (testId: string) (ariaLabel: string) (onClick: unit -> unit) (children: ReactElement list) =
    Html.button [
        prop.key key
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.ariaLabel ariaLabel
        prop.className $"{listRowClass} hover:bg-surface-hover"
        prop.onClick (fun _ -> onClick ())
        prop.children children
    ]

/// Wie `listRow`, die Zeile wählt etwas aus: die gewählte trägt `aria-current` und Teal (ADR 0075).
/// `aria-selected` gilt nur in Rollen wie `option` oder `tab`, nicht an einem `button`.
let selectableListRow (key: string) (testId: string) (ariaLabel: string) (isSelected: bool) (onClick: unit -> unit) (children: ReactElement list) =
    Html.button [
        prop.key key
        prop.type' "button"
        prop.custom ("data-testid", testId)
        if isSelected then prop.custom ("aria-current", "true")
        prop.ariaLabel ariaLabel
        prop.className $"""{listRowClass} {if isSelected then Backgrounds.tealSubtle else "hover:bg-surface-hover"}"""
        prop.onClick (fun _ -> onClick ())
        prop.children children
    ]

/// Ein Pfeil zum Blättern (`‹ ›`): schlichter Knopf mit 48 px Trefferfläche am Handy.
let stepArrow (testId: string) (ariaLabel: string) (arrow: string) (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.ariaLabel ariaLabel
        prop.className $"inline-flex items-center justify-center {TouchTargets.minSize} md:min-h-[36px] md:min-w-[36px] rounded-md text-lg {Colors.textSecondary} cursor-pointer touch-manipulation hover:bg-surface-hover hover:text-text-primary"
        prop.onClick (fun _ -> onClick ())
        prop.text arrow
    ]

/// Der Platz eines fehlenden Pfeils: bleibt frei, damit daneben nichts springt.
let stepArrowGap () : ReactElement =
    Html.span [
        prop.custom ("aria-hidden", true)
        prop.className $"inline-block {TouchTargets.minSize} md:min-h-[36px] md:min-w-[36px]"
    ]

/// Ein Pfeil im Kopf eines Sheets (`˄ ˅`): 48 px auf jeder Breite, ein Sheet gibt es nur auf
/// Touch-Geräten. Der negative Rand hält den Kopf so hoch wie `.sheet-close`.
let sheetArrow (testId: string) (ariaLabel: string) (icon: ReactElement) (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.ariaLabel ariaLabel
        prop.className $"inline-flex items-center justify-center {TouchTargets.minSize} -my-2 rounded-md {Colors.textSecondary} cursor-pointer touch-manipulation hover:text-text-primary"
        prop.onClick (fun _ -> onClick ())
        prop.children [ icon ]
    ]

/// Der Platz eines fehlenden `sheetArrow`.
let sheetArrowGap () : ReactElement =
    Html.span [
        prop.custom ("aria-hidden", true)
        prop.className $"inline-block {TouchTargets.minSize} -my-2"
    ]

/// Ein Schritt am Handy ("+20"). Hält den Fokus im Feld daneben (ADR 0005 Punkt 2), gewirkt wird
/// auf dem `click`. Am Handy 48 px, sechs passen in 360 px Breite.
let amountStep (testId: string) (ariaLabel: string) (text: string) (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.ariaLabel ariaLabel
        prop.className $"inline-flex items-center justify-center {TouchTargets.sheetMinHeight} min-w-0 rounded-lg border {Borders.default'} bg-surface-elevated font-mono text-sm tabular-nums {Colors.textPrimary} cursor-pointer touch-manipulation hover:bg-surface-hover"
        prop.onMouseDown (fun e -> e.preventDefault ())
        prop.onClick (fun _ -> onClick ())
        prop.text text
    ]

/// Die Klassen einer Zeile `von` oder `nach`, wählbar oder fest: Text und Betrag brechen um.
let private sideRowClass =
    $"flex w-full items-center justify-between gap-3 {TouchTargets.sheetMinHeight} rounded-lg border {Borders.default'} bg-surface-card px-3 py-2 text-left"

/// Eine wählbare Zeile `von` oder `nach`: öffnet die Auswahl der Gegenseite.
let sideRow (testId: string) (ariaLabel: string) (expanded: bool) (onClick: unit -> unit) (children: ReactElement list) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.ariaLabel ariaLabel
        prop.ariaExpanded expanded
        prop.className $"{sideRowClass} cursor-pointer touch-manipulation hover:bg-surface-hover"
        prop.onClick (fun _ -> onClick ())
        prop.children children
    ]

/// Dieselbe Zeile fest, ohne Auswahl.
let sideRowFixed (testId: string) (children: ReactElement list) =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className sideRowClass
        prop.children children
    ]

/// Der Name einer Stelle als Knopf zurück nach Hause, etwa der Monat. `away` heißt: gerade woanders,
/// dann ist er teal (ADR 0075: Navigation). Die feste Breite hält die Pfeile daneben an ihrem Platz.
let homeLabel (testId: string) (ariaLabel: string) (away: bool) (text: string) (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.ariaLabel ariaLabel
        prop.className $"""inline-flex items-center justify-center w-36 {TouchTargets.minHeight} md:min-h-[36px] text-sm font-semibold whitespace-nowrap cursor-pointer touch-manipulation rounded-md hover:bg-surface-hover {if away then Colors.neonTeal else Colors.textPrimary}"""
        prop.onClick (fun _ -> onClick ())
        prop.text text
    ]

// ============================================
// Hero Button
// ============================================

/// Large hero button with prominent glow effect for main CTAs
/// Use sparingly - typically one per page for the primary action
let hero (text: string) (onClick: unit -> unit) =
    Html.button [
        prop.className (
            [
                "group relative px-12 py-5 rounded-xl"
                "bg-gradient-to-r from-neon-orange to-neon-orange/80"
                "text-surface-app font-bold text-lg md:text-xl font-display"
                Glows.orangeLg
                Glows.orangeHoverLg
                "hover:scale-105 transition-all duration-300"
            ] |> String.concat " "
        )
        prop.onClick (fun _ -> onClick())
        prop.children [
            Html.span [ prop.text text ]
        ]
    ]

/// Hero button with icon (icon appears before text)
let heroWithIcon (text: string) (icon: ReactElement) (onClick: unit -> unit) =
    Html.button [
        prop.className (
            [
                "group relative px-12 py-5 rounded-xl"
                "bg-gradient-to-r from-neon-orange to-neon-orange/80"
                "text-surface-app font-bold text-lg md:text-xl font-display"
                Glows.orangeLg
                Glows.orangeHoverLg
                "hover:scale-105 transition-all duration-300"
            ] |> String.concat " "
        )
        prop.onClick (fun _ -> onClick())
        prop.children [
            Html.div [
                prop.className "flex items-center gap-3"
                prop.children [
                    icon
                    Html.span [ prop.text text ]
                ]
            ]
        ]
    ]

/// Hero button with loading state
let heroLoading (text: string) (isLoading: bool) (onClick: unit -> unit) =
    Html.button [
        prop.className (
            [
                "group relative px-12 py-5 rounded-xl"
                "bg-gradient-to-r from-neon-orange to-neon-orange/80"
                "text-surface-app font-bold text-lg md:text-xl font-display"
                Glows.orangeLg
                Glows.orangeHoverLg
                "hover:scale-105 transition-all duration-300"
                "disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
            ] |> String.concat " "
        )
        prop.disabled isLoading
        prop.onClick (fun _ -> onClick())
        prop.children [
            if isLoading then
                Html.span [ prop.className "proto-spinner" ]
            else
                Html.span [ prop.text text ]
        ]
    ]

/// Teal variant hero button for secondary prominent actions
let heroTeal (text: string) (onClick: unit -> unit) =
    Html.button [
        prop.className (
            [
                "group relative px-12 py-5 rounded-xl"
                "bg-gradient-to-r from-neon-teal to-neon-teal/80"
                "text-surface-app font-bold text-lg md:text-xl font-display"
                Glows.tealLg
                Glows.tealHoverLg
                "hover:scale-105 transition-all duration-300"
            ] |> String.concat " "
        )
        prop.onClick (fun _ -> onClick())
        prop.children [
            Html.span [ prop.text text ]
        ]
    ]

/// Teal variant hero button with icon (icon appears before text)
let heroTealWithIcon (text: string) (icon: ReactElement) (onClick: unit -> unit) =
    Html.button [
        prop.className (
            [
                "group relative px-12 py-5 rounded-xl"
                "bg-gradient-to-r from-neon-teal to-neon-teal/80"
                "text-surface-app font-bold text-lg md:text-xl font-display"
                Glows.tealLg
                Glows.tealHoverLg
                "hover:scale-105 transition-all duration-300"
            ] |> String.concat " "
        )
        prop.onClick (fun _ -> onClick())
        prop.children [
            Html.div [
                prop.className "flex items-center gap-3"
                prop.children [
                    icon
                    Html.span [ prop.text text ]
                ]
            ]
        ]
    ]
