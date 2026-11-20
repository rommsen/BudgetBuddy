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

/// Die optionale Aktion eines Toasts, z. B. "Rückgängig" (ledger-th36h). Der Toast
/// zeigt nur die Beschriftung; was der Klick tut, entscheidet `OnAction` des Aufrufers.
type ToastActionView = {
    Label: string
}

/// Der eine Toast am PC (`single`): Text, Variante, Abgang und optionale Aktion.
type ToastItem = {
    Id: Guid
    Message: string
    Variant: ToastVariant
    Exiting: bool
    Action: ToastActionView option
}

/// Toast properties
type ToastProps = {
    Id: Guid
    Message: string
    Variant: ToastVariant
    Title: string option
    /// When true the toast plays its exit animation (soft fade-out) instead of the
    /// entrance. The two-phase removal in the app state sets this before the toast
    /// is actually removed from the list (design-system-004).
    Exiting: bool
    OnDismiss: Guid -> unit
    /// Optionale Schaltfläche neben dem Schließen-Knopf. `None`: der Toast ist wie zuvor.
    Action: ToastActionView option
    /// Klick auf die Aktion. Der Aufrufer schließt den Toast und führt die Aktion aus.
    OnAction: Guid -> unit
}

// ============================================
// Helper Functions
// ============================================

/// Rand und Fläche einer Variante (öffentlich für Tests der Farbrollen, ADR 0087).
let variantClass variant =
    match variant with
    | Success -> "border-l-success bg-success/5"
    | Error -> "border-l-error bg-error/5"
    | Warning -> "border-l-attention bg-attention/5"
    | Info -> "border-l-info bg-info/5"

let private variantToIcon variant =
    match variant with
    | Success -> checkCircle SM IconColor.Success
    | Error -> xCircle SM IconColor.Error
    | Warning -> warning SM IconColor.Warning
    | Info -> info SM IconColor.Info

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
            variantClass props.Variant
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

            // Aktion (z. B. "Rückgängig"), Akzent wie in Handoff 1.3
            match props.Action with
            | Some action ->
                Html.button [
                    prop.className (
                        // Am Handy 48 px Trefferfläche, der negative Rand hält den Toast so hoch wie bisher.
                        $"flex-shrink-0 inline-flex items-center justify-center {TouchTargets.mobileOverlap} " +
                        "px-2 py-1 rounded-md text-sm font-semibold transition-colors " +
                        "text-accent hover:bg-surface-hover"
                    )
                    prop.custom ("data-toast-action", "true")
                    prop.onClick (fun _ -> props.OnAction props.Id)
                    prop.text action.Label
                ]
            | None -> ()

            // Close button
            Html.button [
                prop.className (
                    $"flex-shrink-0 inline-flex items-center justify-center {TouchTargets.mobileOverlap} " +
                    "p-1 rounded-md transition-colors " +
                    "text-text-muted/70 hover:text-text-primary hover:bg-surface-hover"
                )
                prop.onClick (fun _ -> props.OnDismiss props.Id)
                prop.ariaLabel "Schließen"
                prop.children [ x XS IconColor.Default ]
            ]
        ]
    ]

// ============================================
// Der eine Toast am PC
// ============================================

/// Höchstens ein Toast, nur ab 1024 px, unten rechts (Regel R3, ADR 0098, Styleguide §7.13, design-system-gkded).
/// Unter 1024 px zeigt dieselbe Meldung die Leiste (`Feedback.bar`), deshalb ist der Behälter darunter `hidden`.
/// `toast-above-bar` (styles.css) hebt ihn über eine feste Leiste unten (Auswahl-Leiste, Tastenleiste des
/// Kassenbuchs), Ebene `toast-layer` (90) über Panel und Dialog. `onDismiss` startet den Abgang (✕), `onAction`
/// ist der Klick auf die Aktion.
let single (item: ToastItem option) (onDismiss: Guid -> unit) (onAction: Guid -> unit) =
    Html.div [
        prop.className "hidden lg:flex fixed toast-layer flex-col pointer-events-none right-4 w-full max-w-sm toast-above-bar"
        prop.children [
            match item with
            | Some item ->
                toast {
                    Id = item.Id
                    Message = item.Message
                    Variant = item.Variant
                    Title = None
                    Exiting = item.Exiting
                    OnDismiss = onDismiss
                    Action = item.Action
                    OnAction = onAction
                }
            | None -> Html.none
        ]
    ]
