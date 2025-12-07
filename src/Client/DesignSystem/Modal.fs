module Client.DesignSystem.Modal

open Feliz
open Fable.Core
open Fable.Core.JsInterop
open Client.DesignSystem.Tokens
open Client.DesignSystem.Icons

// ============================================
// Portal Helper (renders children to document.body)
// ============================================

[<Import("createPortal", from="react-dom")>]
let private createPortal (element: ReactElement) (container: Browser.Types.Element) : ReactElement = jsNative

let private renderToBody (element: ReactElement) =
    createPortal element Browser.Dom.document.body

// ============================================
// Modal Types
// ============================================

/// Modal size variants
type ModalSize =
    | Small       // max-w-sm
    | Medium      // max-w-md
    | Large       // max-w-lg
    | ExtraLarge  // max-w-xl
    | Full        // Full screen on mobile, large on desktop

/// Modal properties
type ModalProps = {
    IsOpen: bool
    OnClose: unit -> unit
    Size: ModalSize
    CloseOnBackdropClick: bool
    CloseOnEscape: bool
    ShowCloseButton: bool
    Title: string option
    Subtitle: string option
}

let defaultProps = {
    IsOpen = false
    OnClose = fun () -> ()
    Size = Medium
    CloseOnBackdropClick = true
    CloseOnEscape = true
    ShowCloseButton = true
    Title = None
    Subtitle = None
}

// ============================================
// Helper Functions
// ============================================

let private sizeToClass = function
    | Small -> "max-w-sm"
    | Medium -> "max-w-md"
    | Large -> "max-w-lg"
    | ExtraLarge -> "max-w-xl"
    | Full -> "max-w-4xl"

let private sizeToMobileClass = function
    | Full -> "min-h-screen md:min-h-0 md:rounded-xl"
    | _ -> "rounded-xl"

// ============================================
// Modal Components
// ============================================

/// Modal backdrop (dark overlay with blur)
/// Note: No animation on backdrop to prevent flicker - animation is on modal content only
let private backdrop (onClick: unit -> unit) (closeOnClick: bool) =
    Html.div [
        prop.className "fixed inset-0 bg-black/70 backdrop-blur-sm z-40"
        if closeOnClick then
            prop.onClick (fun e ->
                e.stopPropagation()
                onClick()
            )
    ]

/// Modal header with title, subtitle, and close button
let header (title: string) (subtitle: string option) (showClose: bool) (onClose: unit -> unit) =
    Html.div [
        prop.className "flex items-start justify-between gap-4 p-4 md:p-6 border-b border-white/10"
        prop.children [
            Html.div [
                prop.className "flex-1 min-w-0"
                prop.children [
                    Html.h2 [
                        prop.className "text-lg md:text-xl font-semibold font-display text-base-content"
                        prop.text title
                    ]
                    match subtitle with
                    | Some sub ->
                        Html.p [
                            prop.className "text-sm text-base-content/60 mt-1"
                            prop.text sub
                        ]
                    | None -> ()
                ]
            ]
            if showClose then
                Html.button [
                    prop.className (
                        "flex-shrink-0 p-2 -m-2 rounded-lg transition-colors " +
                        "text-base-content/50 hover:text-base-content hover:bg-white/5"
                    )
                    prop.onClick (fun _ -> onClose())
                    prop.ariaLabel "Close modal"
                    prop.children [ x MD IconColor.Default ]
                ]
        ]
    ]

/// Modal body (scrollable content area)
let body (children: ReactElement list) =
    Html.div [
        prop.className "p-4 md:p-6 overflow-y-auto flex-1"
        prop.children children
    ]

/// Modal body with no padding (for custom layouts)
let bodyNoPadding (children: ReactElement list) =
    Html.div [
        prop.className "overflow-y-auto flex-1"
        prop.children children
    ]

/// Modal footer (for action buttons)
let footer (children: ReactElement list) =
    Html.div [
        prop.className "flex flex-col-reverse sm:flex-row sm:justify-end gap-2 sm:gap-3 p-4 md:p-6 border-t border-white/10 bg-base-200/50"
        prop.children children
    ]

/// Modal footer with left-aligned content
let footerWithLeft (leftContent: ReactElement) (rightContent: ReactElement list) =
    Html.div [
        prop.className "flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 p-4 md:p-6 border-t border-white/10 bg-base-200/50"
        prop.children [
            Html.div [
                prop.className "order-2 sm:order-1"
                prop.children [ leftContent ]
            ]
            Html.div [
                prop.className "flex flex-col-reverse sm:flex-row gap-2 sm:gap-3 order-1 sm:order-2"
                prop.children rightContent
            ]
        ]
    ]

// ============================================
// Internal Modal Component (with animation tracking)
// ============================================

/// Internal props for the functional component
type private ModalInternalProps = {
    Props: ModalProps
    Children: ReactElement list
}

/// Internal modal component that tracks animation state
let private ModalInternal = React.functionComponent("ModalInternal", fun (input: ModalInternalProps) ->
    let props = input.Props
    let children = input.Children

    // Track if animation has already played (prevents re-animation on re-renders)
    let hasAnimated = React.useRef false

    // Only set hasAnimated when actually showing the modal
    let shouldAnimate =
        if not props.IsOpen then
            false  // Don't track animation state when closed
        elif hasAnimated.current then
            false
        else
            hasAnimated.current <- true
            true

    if not props.IsOpen then
        Html.none
    else
        renderToBody (
            Html.div [
                prop.className "fixed inset-0 z-50 overflow-hidden"
                prop.children [
                    // Backdrop (no animation - prevents flicker)
                    backdrop props.OnClose props.CloseOnBackdropClick

                    // Modal container (centered)
                    Html.div [
                        prop.className "fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 overflow-y-auto"
                        prop.children [
                            // Modal content
                            Html.div [
                                prop.className (
                                    "relative z-50 w-full bg-base-100 border border-white/10 shadow-2xl " +
                                    (if shouldAnimate then "animate-scale-in " else "") +
                                    "flex flex-col max-h-[90vh] " +
                                    sizeToClass props.Size + " " +
                                    sizeToMobileClass props.Size
                                )
                                prop.onClick (fun e -> e.stopPropagation())
                                prop.children [
                                    // Header (if title provided)
                                    match props.Title with
                                    | Some title ->
                                        header title props.Subtitle props.ShowCloseButton props.OnClose
                                    | None ->
                                        // Just show close button if no title
                                        if props.ShowCloseButton then
                                            Html.div [
                                                prop.className "absolute top-4 right-4 z-10"
                                                prop.children [
                                                    Html.button [
                                                        prop.className (
                                                            "p-2 rounded-lg transition-colors " +
                                                            "text-base-content/50 hover:text-base-content hover:bg-white/5"
                                                        )
                                                        prop.onClick (fun _ -> props.OnClose())
                                                        prop.ariaLabel "Close modal"
                                                        prop.children [ x MD IconColor.Default ]
                                                    ]
                                                ]
                                            ]

                                    // Content
                                    yield! children
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        )
)

// ============================================
// Main Modal Component
// ============================================

/// Complete modal with props and children (renders via portal to body)
/// Animation only plays on first render, not on re-renders
let view (props: ModalProps) (children: ReactElement list) =
    ModalInternal { Props = props; Children = children }

// ============================================
// Convenience Functions
// ============================================

/// Simple modal with title
let simple (isOpen: bool) (title: string) (onClose: unit -> unit) (children: ReactElement list) =
    view {
        defaultProps with
            IsOpen = isOpen
            Title = Some title
            OnClose = onClose
    } children

/// Modal without title (custom header)
let custom (isOpen: bool) (onClose: unit -> unit) (size: ModalSize) (children: ReactElement list) =
    view {
        defaultProps with
            IsOpen = isOpen
            OnClose = onClose
            Size = size
            Title = None
    } children

/// Full-screen modal (for mobile forms)
let fullScreen (isOpen: bool) (title: string) (onClose: unit -> unit) (children: ReactElement list) =
    view {
        defaultProps with
            IsOpen = isOpen
            Title = Some title
            OnClose = onClose
            Size = Full
    } children

/// Confirmation dialog
let confirm
    (isOpen: bool)
    (title: string)
    (message: string)
    (confirmText: string)
    (onConfirm: unit -> unit)
    (onCancel: unit -> unit) =

    view {
        defaultProps with
            IsOpen = isOpen
            Title = Some title
            OnClose = onCancel
            Size = Small
    } [
        body [
            Html.p [
                prop.className "text-base-content/80"
                prop.text message
            ]
        ]
        footer [
            Html.button [
                prop.className "btn btn-ghost"
                prop.text "Cancel"
                prop.onClick (fun _ -> onCancel())
            ]
            Html.button [
                prop.className "btn btn-primary"
                prop.text confirmText
                prop.onClick (fun _ -> onConfirm())
            ]
        ]
    ]

/// Danger confirmation dialog (for destructive actions)
let confirmDanger
    (isOpen: bool)
    (title: string)
    (message: string)
    (confirmText: string)
    (onConfirm: unit -> unit)
    (onCancel: unit -> unit) =

    view {
        defaultProps with
            IsOpen = isOpen
            Title = Some title
            OnClose = onCancel
            Size = Small
    } [
        body [
            Html.p [
                prop.className "text-base-content/80"
                prop.text message
            ]
        ]
        footer [
            Html.button [
                prop.className "btn btn-ghost"
                prop.text "Cancel"
                prop.onClick (fun _ -> onCancel())
            ]
            Html.button [
                prop.className "btn bg-neon-red text-white hover:bg-neon-red/80"
                prop.text confirmText
                prop.onClick (fun _ -> onConfirm())
            ]
        ]
    ]

/// Alert/info dialog (single button)
let alert
    (isOpen: bool)
    (title: string)
    (message: string)
    (buttonText: string)
    (onClose: unit -> unit) =

    view {
        defaultProps with
            IsOpen = isOpen
            Title = Some title
            OnClose = onClose
            Size = Small
    } [
        body [
            Html.p [
                prop.className "text-base-content/80"
                prop.text message
            ]
        ]
        footer [
            Html.button [
                prop.className "btn btn-primary w-full sm:w-auto"
                prop.text buttonText
                prop.onClick (fun _ -> onClose())
            ]
        ]
    ]

// ============================================
// Loading Modal
// ============================================

/// Loading overlay modal (no close button, renders via portal)
let loading (isOpen: bool) (message: string) =
    if not isOpen then
        Html.none
    else
        renderToBody (
            Html.div [
                prop.className "fixed inset-0 z-50 flex items-center justify-center"
                prop.children [
                    // Backdrop (no click to close)
                    Html.div [
                        prop.className "absolute inset-0 bg-black/70 backdrop-blur-sm"
                    ]
                    // Content
                    Html.div [
                        prop.className "relative z-10 flex flex-col items-center gap-4 p-8 bg-base-100 rounded-xl border border-white/10 shadow-2xl animate-scale-in"
                        prop.children [
                            // Spinner
                            Html.div [
                                prop.className "loading loading-spinner loading-lg text-neon-teal"
                            ]
                            Html.p [
                                prop.className "text-base-content/80 text-center"
                                prop.text message
                            ]
                        ]
                    ]
                ]
            ]
        )
