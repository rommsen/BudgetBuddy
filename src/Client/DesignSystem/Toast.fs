module Client.DesignSystem.Toast

open Feliz
open System
open Client.DesignSystem.Tokens
open Client.DesignSystem.Icons

// ============================================
// Toast Types
// ============================================

/// Toast variant determines the color and icon
type ToastVariant =
    | Success
    | Error
    | Warning
    | Info

/// Toast position on screen
type ToastPosition =
    | TopRight
    | TopLeft
    | TopCenter
    | BottomRight
    | BottomLeft
    | BottomCenter

/// Toast properties
type ToastProps = {
    Id: Guid
    Message: string
    Variant: ToastVariant
    Title: string option
    AutoDismiss: bool
    DismissAfterMs: int
    OnDismiss: Guid -> unit
}

// ============================================
// Helper Functions
// ============================================

let private variantToClasses variant =
    match variant with
    | Success -> "border-l-neon-green bg-neon-green/5"
    | Error -> "border-l-neon-red bg-neon-red/5"
    | Warning -> "border-l-neon-orange bg-neon-orange/5"
    | Info -> "border-l-neon-teal bg-neon-teal/5"

let private variantToIcon variant =
    match variant with
    | Success -> checkCircle SM IconColor.NeonGreen
    | Error -> xCircle SM IconColor.NeonRed
    | Warning -> warning SM IconColor.NeonOrange
    | Info -> info SM IconColor.NeonTeal

let private variantToTitle variant =
    match variant with
    | Success -> "Success"
    | Error -> "Error"
    | Warning -> "Warning"
    | Info -> "Info"

let private positionToClasses position =
    match position with
    | TopRight -> "top-4 right-4"
    | TopLeft -> "top-4 left-4"
    | TopCenter -> "top-4 left-1/2 -translate-x-1/2"
    | BottomRight -> "bottom-4 right-4 md:bottom-4"
    | BottomLeft -> "bottom-4 left-4 md:bottom-4"
    | BottomCenter -> "bottom-4 left-1/2 -translate-x-1/2 md:bottom-4"

// ============================================
// Toast Component
// ============================================

/// Single toast notification
let toast (props: ToastProps) =
    Html.div [
        prop.key (props.Id.ToString())
        prop.className (
            "flex items-start gap-3 p-4 rounded-lg border-l-4 border border-white/10 " +
            "bg-base-300 shadow-xl backdrop-blur-sm " +
            "animate-slide-up " +
            variantToClasses props.Variant
        )
        prop.children [
            // Icon
            Html.div [
                prop.className "flex-shrink-0 mt-0.5"
                prop.children [ variantToIcon props.Variant ]
            ]

            // Content
            Html.div [
                prop.className "flex-1 min-w-0"
                prop.children [
                    // Title (optional)
                    match props.Title with
                    | Some title ->
                        Html.div [
                            prop.className "font-semibold text-base-content text-sm mb-0.5"
                            prop.text title
                        ]
                    | None -> ()

                    // Message
                    Html.div [
                        prop.className "text-base-content/80 text-sm break-words"
                        prop.text props.Message
                    ]
                ]
            ]

            // Close button
            Html.button [
                prop.className (
                    "flex-shrink-0 p-1 rounded-md transition-colors " +
                    "text-base-content/50 hover:text-base-content hover:bg-white/5"
                )
                prop.onClick (fun _ -> props.OnDismiss props.Id)
                prop.ariaLabel "Dismiss"
                prop.children [ x XS IconColor.Default ]
            ]
        ]
    ]

/// Toast with minimal configuration
let toastSimple (id: Guid) (message: string) (variant: ToastVariant) (onDismiss: Guid -> unit) =
    toast {
        Id = id
        Message = message
        Variant = variant
        Title = None
        AutoDismiss = true
        DismissAfterMs = 5000
        OnDismiss = onDismiss
    }

/// Toast with title
let toastWithTitle (id: Guid) (title: string) (message: string) (variant: ToastVariant) (onDismiss: Guid -> unit) =
    toast {
        Id = id
        Message = message
        Variant = variant
        Title = Some title
        AutoDismiss = true
        DismissAfterMs = 5000
        OnDismiss = onDismiss
    }

// ============================================
// Toast Container Component
// ============================================

/// Container for toast notifications (fixed position)
let container (position: ToastPosition) (children: ReactElement list) =
    Html.div [
        prop.className (
            "fixed z-50 flex flex-col gap-2 w-full max-w-sm px-4 md:px-0 " +
            // On mobile, position at top to avoid bottom nav
            "top-16 md:top-auto " +
            positionToClasses position
        )
        prop.children children
    ]

/// Default container at bottom-right (top on mobile)
let containerDefault (children: ReactElement list) =
    container BottomRight children

// ============================================
// Convenience Functions
// ============================================

/// Success toast
let success (id: Guid) (message: string) (onDismiss: Guid -> unit) =
    toastSimple id message Success onDismiss

/// Error toast
let error (id: Guid) (message: string) (onDismiss: Guid -> unit) =
    toastSimple id message Error onDismiss

/// Warning toast
let warning' (id: Guid) (message: string) (onDismiss: Guid -> unit) =
    toastSimple id message Warning onDismiss

/// Info toast
let info' (id: Guid) (message: string) (onDismiss: Guid -> unit) =
    toastSimple id message Info onDismiss

// ============================================
// Toast List Component (for use in View.fs)
// ============================================

/// Render a list of toasts from records with Id, Message, Variant
let renderList
    (toasts: (Guid * string * ToastVariant) list)
    (onDismiss: Guid -> unit) =

    containerDefault [
        for (id, message, variant) in toasts do
            toastSimple id message variant onDismiss
    ]
