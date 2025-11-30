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
    | Secondary  // Neon teal outline
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
}

// ============================================
// Button Implementation
// ============================================

let private sizeToClass = function
    | Small -> "btn-sm min-h-[36px] md:min-h-0"
    | Medium -> "min-h-[48px] md:min-h-0"
    | Large -> "btn-lg min-h-[52px] md:min-h-0"

let private variantToClass = function
    | Primary ->
        "btn-primary bg-gradient-to-br from-neon-orange to-[#e55a1f] border-none text-white shadow-glow-orange hover:shadow-[0_0_25px_rgba(255,107,44,0.5),0_0_50px_rgba(255,107,44,0.3)] hover:-translate-y-0.5 active:scale-[0.98]"
    | Secondary ->
        "btn-ghost border border-neon-teal text-neon-teal hover:bg-neon-teal/10 hover:shadow-glow-teal active:scale-[0.98]"
    | Ghost ->
        "btn-ghost text-base-content/70 hover:text-base-content hover:bg-white/5"
    | Danger ->
        "btn-ghost border border-neon-red text-neon-red hover:bg-neon-red/10 hover:shadow-[0_0_20px_rgba(255,59,92,0.5)] active:scale-[0.98]"

/// Create a button with the specified props
let view (props: ButtonProps) =
    let sizeClass = sizeToClass props.Size
    let variantClass = variantToClass props.Variant
    let widthClass = if props.FullWidth then "w-full md:w-auto" else ""
    let extraClass = props.ClassName |> Option.defaultValue ""

    Html.button [
        prop.className $"btn {variantClass} {sizeClass} {widthClass} {extraClass} transition-all duration-200"
        prop.disabled (props.IsLoading || props.IsDisabled)
        prop.onClick (fun _ -> props.OnClick())
        prop.children [
            // Loading spinner
            if props.IsLoading then
                Html.span [
                    prop.className "loading loading-spinner loading-sm"
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

/// Secondary button (teal outline)
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
