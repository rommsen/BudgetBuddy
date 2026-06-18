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
    /// When true the toast plays its exit animation (soft fade-out) instead of the
    /// entrance. The two-phase removal in the app state sets this before the toast
    /// is actually removed from the list (design-system-004).
    Exiting: bool
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

/// Desktop (md and up) anchoring for the chosen position. Mobile placement is
/// NOT derived from this — `container` builds a deliberate mobile top-strip and
/// only switches over to these anchors at the `md:` breakpoint. Keeping these
/// `md:`-prefixed prevents the mobile/desktop inset conflict that made the toast
/// stretch full-height on phones (design-system-005). All classes are full
/// string literals so Tailwind's purge keeps them.
let private positionToClasses position =
    match position with
    | TopRight -> "md:top-4 md:right-4"
    | TopLeft -> "md:top-4 md:left-4"
    | TopCenter -> "md:top-4 md:left-1/2 md:-translate-x-1/2"
    | BottomRight -> "md:bottom-4 md:right-4"
    | BottomLeft -> "md:bottom-4 md:left-4"
    | BottomCenter -> "md:bottom-4 md:left-1/2 md:-translate-x-1/2"

// ============================================
// Toast Component
// ============================================

/// Single toast notification
let toast (props: ToastProps) =
    // Entrance vs. exit motion (design-system-004). The animation classes sit on
    // this inner element, never on the fixed container (css-animation-safety).
    let motionClass = if props.Exiting then "animate-toast-out" else "animate-toast-in"
    Html.div [
        prop.key (props.Id.ToString())
        prop.className (
            "pointer-events-auto flex items-start gap-3 p-3 rounded-lg border border-border-subtle " +
            // Fully opaque surface (design-system-005): over the bright sync hero
            // gradient a translucent /95 fill let the gradient bleed through and
            // wrecked contrast. Keep backdrop-blur for parity with elevated
            // surfaces; the opaque fill guarantees the toast stays legible.
            "bg-surface-card backdrop-blur-md shadow-lg " +
            motionClass + " " +
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
                            prop.className "font-semibold text-text-primary text-sm mb-0.5"
                            prop.text title
                        ]
                    | None -> ()

                    // Message
                    Html.div [
                        prop.className "text-text-secondary text-sm break-words"
                        prop.text props.Message
                    ]
                ]
            ]

            // Close button
            Html.button [
                prop.className (
                    "flex-shrink-0 p-1 rounded-md transition-colors " +
                    "text-text-muted/70 hover:text-text-primary hover:bg-surface-hover"
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
        Exiting = false
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
        Exiting = false
        OnDismiss = onDismiss
    }

// ============================================
// Toast Container Component
// ============================================

/// Container for toast notifications (fixed position).
///
/// Mobile and desktop placements are built as two deliberate cases, not one
/// half-overridden (design-system-005). On mobile the container is a compact,
/// symmetrically inset strip pinned below the header:
///   `top-16 inset-x-4` — equal left/right inset so the toast's neon left-border
///   and glow are never clipped at the viewport edge, and `top` alone (no
///   `bottom`) so the fixed box does NOT stretch the full viewport height.
/// At the `md:` breakpoint we reset the mobile insets and switch to the chosen
/// desktop anchor (default bottom-right) with the original `max-w-sm` width.
let container (position: ToastPosition) (children: ReactElement list) =
    Html.div [
        prop.className (
            "fixed z-50 flex flex-col gap-2 pointer-events-none " +
            // Mobile: deliberate top strip, symmetric inset (no inherited bottom/right).
            "top-16 inset-x-4 " +
            // Desktop: drop the mobile insets, take a bounded width, then anchor.
            "md:top-auto md:inset-x-auto md:w-full md:max-w-sm " +
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

/// Render a list of toasts from tuples of (Id, Message, Variant, Exiting).
/// The `Exiting` flag selects the exit animation for toasts in their fade-out
/// phase of the two-phase removal (design-system-004). `onDismiss` is called when
/// the user clicks the close button — the caller is responsible for starting the
/// exit phase (it should NOT remove the toast immediately).
let renderList
    (toasts: (Guid * string * ToastVariant * bool) list)
    (onDismiss: Guid -> unit) =

    containerDefault [
        for (id, message, variant, exiting) in toasts do
            toast {
                Id = id
                Message = message
                Variant = variant
                Title = None
                AutoDismiss = true
                DismissAfterMs = 5000
                Exiting = exiting
                OnDismiss = onDismiss
            }
    ]
