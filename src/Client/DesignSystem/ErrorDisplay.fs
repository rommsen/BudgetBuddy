namespace Client.DesignSystem

open Feliz
open Client.DesignSystem.Icons
open Client.DesignSystem.Button

/// Standardized error display components for consistent error handling across the application.
/// Provides card, inline, and full-page variants with optional retry functionality.
module ErrorDisplay =

    // ============================================================================
    // INLINE ERROR - For form fields and small contexts
    // ============================================================================

    /// Inline error message for form validation feedback
    /// Compact, red text with icon
    let inline' (message: string) =
        Html.div [
            prop.className "flex items-center gap-2 text-error text-sm mt-1"
            prop.role "alert"
            prop.children [
                Icons.xCircle SM Error
                Html.span [ prop.text message ]
            ]
        ]

    /// Inline error with custom icon
    let inlineWithIcon (icon: ReactElement) (message: string) =
        Html.div [
            prop.className "flex items-center gap-2 text-error text-sm mt-1"
            prop.role "alert"
            prop.children [
                icon
                Html.span [ prop.text message ]
            ]
        ]

    // ============================================================================
    // CARD ERROR - Standard error card with optional retry
    // ============================================================================

    /// Standard error card with icon, message, and optional retry button
    let card (message: string) (onRetry: (unit -> unit) option) =
        Html.div [
            prop.className "rounded-xl bg-base-100 border border-error/20 overflow-hidden"
            prop.role "alert"
            prop.children [
                // Error header with gradient
                Html.div [
                    prop.className "bg-gradient-to-r from-error/10 to-error/5 px-6 py-4 flex items-center gap-3"
                    prop.children [
                        Html.div [
                            prop.className "w-10 h-10 rounded-full bg-error/20 flex items-center justify-center flex-shrink-0"
                            prop.children [ Icons.xCircle MD Error ]
                        ]
                        Html.span [
                            prop.className "font-semibold text-error"
                            prop.text "Error"
                        ]
                    ]
                ]
                // Message body
                Html.div [
                    prop.className "px-6 py-4"
                    prop.children [
                        Html.p [
                            prop.className "text-base-content/70"
                            prop.text message
                        ]
                        match onRetry with
                        | Some retry ->
                            Html.div [
                                prop.className "mt-4"
                                prop.children [
                                    Button.secondary "Try Again" retry
                                ]
                            ]
                        | None -> ()
                    ]
                ]
            ]
        ]

    /// Error card with custom title
    let cardWithTitle (title: string) (message: string) (onRetry: (unit -> unit) option) =
        Html.div [
            prop.className "rounded-xl bg-base-100 border border-error/20 overflow-hidden"
            prop.role "alert"
            prop.children [
                // Error header with gradient
                Html.div [
                    prop.className "bg-gradient-to-r from-error/10 to-error/5 px-6 py-4 flex items-center gap-3"
                    prop.children [
                        Html.div [
                            prop.className "w-10 h-10 rounded-full bg-error/20 flex items-center justify-center flex-shrink-0"
                            prop.children [ Icons.xCircle MD Error ]
                        ]
                        Html.span [
                            prop.className "font-semibold text-error"
                            prop.text title
                        ]
                    ]
                ]
                // Message body
                Html.div [
                    prop.className "px-6 py-4"
                    prop.children [
                        Html.p [
                            prop.className "text-base-content/70"
                            prop.text message
                        ]
                        match onRetry with
                        | Some retry ->
                            Html.div [
                                prop.className "mt-4"
                                prop.children [
                                    Button.secondary "Try Again" retry
                                ]
                            ]
                        | None -> ()
                    ]
                ]
            ]
        ]

    /// Compact error card without header, just icon + message
    let cardCompact (message: string) (onRetry: (unit -> unit) option) =
        Html.div [
            prop.className "rounded-lg bg-error/5 border border-error/20 px-4 py-3 flex items-start gap-3"
            prop.role "alert"
            prop.children [
                Icons.xCircle MD Error
                Html.div [
                    prop.className "flex-1"
                    prop.children [
                        Html.p [
                            prop.className "text-base-content/80 text-sm"
                            prop.text message
                        ]
                        match onRetry with
                        | Some retry ->
                            Html.button [
                                prop.className "text-error hover:text-error/80 text-sm font-medium mt-2 underline"
                                prop.onClick (fun _ -> retry())
                                prop.text "Try again"
                            ]
                        | None -> ()
                    ]
                ]
            ]
        ]

    // ============================================================================
    // HERO ERROR - For SyncFlow and major operations
    // ============================================================================

    /// Hero-style error display with large icon and gradient header
    /// Suitable for major operation failures like sync errors
    let hero (title: string) (message: string) (retryText: string) (retryIcon: ReactElement) (onRetry: unit -> unit) =
        Html.div [
            prop.className "max-w-md mx-auto animate-fade-in"
            prop.children [
                Html.div [
                    prop.className "rounded-xl bg-base-100 border border-white/5 overflow-hidden"
                    prop.role "alert"
                    prop.children [
                        // Error header with neon red gradient
                        Html.div [
                            prop.className "bg-gradient-to-br from-neon-red to-neon-pink p-6 text-center"
                            prop.children [
                                Html.div [
                                    prop.className "w-16 h-16 rounded-full bg-white/20 flex items-center justify-center mx-auto mb-3"
                                    prop.children [ Icons.xCircle XL IconColor.Primary ]
                                ]
                                Html.h2 [
                                    prop.className "text-xl font-bold font-display text-white"
                                    prop.text title
                                ]
                            ]
                        ]
                        Html.div [
                            prop.className "p-6 text-center"
                            prop.children [
                                Html.p [
                                    prop.className "text-base-content/70 mb-4"
                                    prop.text message
                                ]
                                Button.primaryWithIcon retryText retryIcon onRetry
                            ]
                        ]
                    ]
                ]
            ]
        ]

    /// Hero error with simple retry button (no icon)
    let heroSimple (title: string) (message: string) (onRetry: unit -> unit) =
        Html.div [
            prop.className "max-w-md mx-auto animate-fade-in"
            prop.children [
                Html.div [
                    prop.className "rounded-xl bg-base-100 border border-white/5 overflow-hidden"
                    prop.role "alert"
                    prop.children [
                        // Error header with neon red gradient
                        Html.div [
                            prop.className "bg-gradient-to-br from-neon-red to-neon-pink p-6 text-center"
                            prop.children [
                                Html.div [
                                    prop.className "w-16 h-16 rounded-full bg-white/20 flex items-center justify-center mx-auto mb-3"
                                    prop.children [ Icons.xCircle XL IconColor.Primary ]
                                ]
                                Html.h2 [
                                    prop.className "text-xl font-bold font-display text-white"
                                    prop.text title
                                ]
                            ]
                        ]
                        Html.div [
                            prop.className "p-6 text-center"
                            prop.children [
                                Html.p [
                                    prop.className "text-base-content/70 mb-4"
                                    prop.text message
                                ]
                                Button.primary "Try Again" onRetry
                            ]
                        ]
                    ]
                ]
            ]
        ]

    // ============================================================================
    // FULL PAGE ERROR - For critical failures
    // ============================================================================

    /// Full-page error state for critical failures
    /// Centers content and takes up available space
    let fullPage (title: string) (message: string) (onRetry: (unit -> unit) option) =
        Html.div [
            prop.className "flex items-center justify-center min-h-[50vh] p-4"
            prop.children [
                Html.div [
                    prop.className "text-center max-w-md"
                    prop.role "alert"
                    prop.children [
                        // Large error icon
                        Html.div [
                            prop.className "w-20 h-20 rounded-full bg-error/10 flex items-center justify-center mx-auto mb-6"
                            prop.children [ Icons.xCircle XL Error ]
                        ]
                        Html.h1 [
                            prop.className "text-2xl font-bold font-display text-base-content mb-3"
                            prop.text title
                        ]
                        Html.p [
                            prop.className "text-base-content/60 mb-6"
                            prop.text message
                        ]
                        match onRetry with
                        | Some retry ->
                            Button.primary "Try Again" retry
                        | None -> ()
                    ]
                ]
            ]
        ]

    /// Full-page error with custom action button
    let fullPageWithAction (title: string) (message: string) (actionButton: ReactElement) =
        Html.div [
            prop.className "flex items-center justify-center min-h-[50vh] p-4"
            prop.children [
                Html.div [
                    prop.className "text-center max-w-md"
                    prop.role "alert"
                    prop.children [
                        // Large error icon
                        Html.div [
                            prop.className "w-20 h-20 rounded-full bg-error/10 flex items-center justify-center mx-auto mb-6"
                            prop.children [ Icons.xCircle XL Error ]
                        ]
                        Html.h1 [
                            prop.className "text-2xl font-bold font-display text-base-content mb-3"
                            prop.text title
                        ]
                        Html.p [
                            prop.className "text-base-content/60 mb-6"
                            prop.text message
                        ]
                        actionButton
                    ]
                ]
            ]
        ]

    // ============================================================================
    // CONVENIENCE FUNCTIONS
    // ============================================================================

    /// Error display for RemoteData.Failure scenarios
    /// Uses card layout with retry button
    let forRemoteData (error: string) (onRetry: unit -> unit) =
        card error (Some onRetry)

    /// Simple error message without retry (for read-only contexts)
    let simple (message: string) =
        card message None

    /// Warning-style display (uses warning colors instead of error)
    let warning (message: string) (onAction: (unit -> unit) option) =
        Html.div [
            prop.className "rounded-lg bg-warning/5 border border-warning/20 px-4 py-3 flex items-start gap-3"
            prop.role "alert"
            prop.children [
                Icons.warning MD Warning
                Html.div [
                    prop.className "flex-1"
                    prop.children [
                        Html.p [
                            prop.className "text-base-content/80 text-sm"
                            prop.text message
                        ]
                        match onAction with
                        | Some action ->
                            Html.button [
                                prop.className "text-warning hover:text-warning/80 text-sm font-medium mt-2 underline"
                                prop.onClick (fun _ -> action())
                                prop.text "Dismiss"
                            ]
                        | None -> ()
                    ]
                ]
            ]
        ]
