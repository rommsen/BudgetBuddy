module View

open Feliz
open State
open Types
open Shared.Domain

/// Floating orb component for animated background
let private floatingOrbs =
    React.fragment [
        Html.div [ prop.className "floating-orb orb-1" ]
        Html.div [ prop.className "floating-orb orb-2" ]
        Html.div [ prop.className "floating-orb orb-3" ]
    ]

/// 3D pushable increment button with high affordance
let private incrementButton (dispatch: Msg -> unit) =
    Html.button [
        prop.className "pushable-button"
        prop.onClick (fun _ -> dispatch IncrementCounter)
        prop.children [
            Html.span [ prop.className "pushable-shadow" ]
            Html.span [ prop.className "pushable-edge" ]
            Html.span [
                prop.className "pushable-front"
                prop.children [
                    Html.span [
                        prop.className "flex items-center gap-3"
                        prop.children [
                            Html.span [
                                prop.className "text-2xl"
                                prop.text "+"
                            ]
                            Html.span [ prop.text "Increment" ]
                        ]
                    ]
                ]
            ]
        ]
    ]

/// Animated counter number display
let private counterDisplay (value: int) (isAnimating: bool) =
    Html.div [
        prop.className "relative"
        prop.children [
            Html.div [
                prop.key (string value)
                prop.className (
                    "counter-number" +
                    (if isAnimating then " animating" else "")
                )
                prop.text (string value)
            ]
        ]
    ]

/// Loading spinner
let private loadingSpinner =
    Html.div [
        prop.className "flex flex-col items-center gap-4"
        prop.children [
            Html.div [ prop.className "fancy-spinner" ]
            Html.span [
                prop.className "text-white/80 text-lg"
                prop.text "Loading..."
            ]
        ]
    ]

/// Counter display component
let private counterView (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "flex flex-col items-center gap-12"
        prop.children [
            // Glass card with counter
            Html.div [
                prop.key "counter-card"
                prop.className "glass-card rounded-3xl p-12 min-w-[400px]"
                prop.children [
                    Html.div [
                        prop.className "flex flex-col items-center gap-8"
                        prop.children [
                            // Title
                            Html.h2 [
                                prop.className "text-3xl font-bold text-white/90 tracking-wide"
                                prop.text "Counter"
                            ]

                            // Counter value or loading state
                            match model.Counter with
                            | NotAsked ->
                                Html.div [
                                    prop.className "text-white/60 text-xl"
                                    prop.text "Initializing..."
                                ]

                            | Loading ->
                                // Show previous value during loading if available
                                match model.PreviousValue with
                                | Some prev ->
                                    Html.div [
                                        prop.className "flex flex-col items-center gap-4"
                                        prop.children [
                                            Html.div [
                                                prop.className "counter-number opacity-50"
                                                prop.text (string prev)
                                            ]
                                        ]
                                    ]
                                | None ->
                                    loadingSpinner

                            | Success counter ->
                                counterDisplay counter.Value model.IsAnimating

                            | Failure err ->
                                Html.div [
                                    prop.className "bg-red-500/20 border border-red-400/30 rounded-xl p-4"
                                    prop.children [
                                        Html.span [
                                            prop.className "text-red-200"
                                            prop.text $"Error: {err}"
                                        ]
                                    ]
                                ]

                            // Increment button (always visible except on error)
                            match model.Counter with
                            | Success _ | Loading ->
                                incrementButton dispatch
                            | _ -> Html.none
                        ]
                    ]
                ]
            ]

            // Subtitle info
            Html.div [
                prop.key "info-card"
                prop.className "text-center"
                prop.children [
                    Html.p [
                        prop.key "info-1"
                        prop.className "text-white/70 text-lg"
                        prop.text "Click the button to increment"
                    ]
                    Html.p [
                        prop.key "info-2"
                        prop.className "text-white/50 text-sm mt-1"
                        prop.text "Persisted on the backend"
                    ]
                    // Display data path
                    match model.DataPath with
                    | Success path ->
                        Html.p [
                            prop.key "data-path"
                            prop.className "text-white/40 text-xs mt-3 font-mono break-all"
                            prop.text $"Data file: {path}"
                        ]
                    | Loading ->
                        Html.p [
                            prop.key "data-path-loading"
                            prop.className "text-white/40 text-xs mt-3"
                            prop.text "Loading path..."
                        ]
                    | Failure err ->
                        Html.p [
                            prop.key "data-path-error"
                            prop.className "text-red-400/60 text-xs mt-3"
                            prop.text $"Path error: {err}"
                        ]
                    | NotAsked -> Html.none
                ]
            ]
        ]
    ]

/// Main view
let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "min-h-screen animated-background flex flex-col"
        prop.children [
            // Floating orbs for visual interest
            floatingOrbs

            // Glass header
            Html.div [
                prop.key "header"
                prop.className "glass-header relative z-10"
                prop.children [
                    Html.div [
                        prop.className "container mx-auto px-6 py-4"
                        prop.children [
                            Html.h1 [
                                prop.className "text-2xl font-bold text-white tracking-wide"
                                prop.children [
                                    Html.span [
                                        prop.className "opacity-80"
                                        prop.text "F#"
                                    ]
                                    Html.span [
                                        prop.className "mx-2 opacity-40"
                                        prop.text "|"
                                    ]
                                    Html.span [
                                        prop.text "Counter Demo"
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]

            // Main content
            Html.div [
                prop.key "main-content"
                prop.className "flex-1 container mx-auto p-8 flex items-center justify-center relative z-10"
                prop.children [
                    counterView model dispatch
                ]
            ]

            // Support banner
            Html.div [
                prop.key "support-banner"
                prop.className "bg-gradient-to-r from-pink-500/20 to-orange-500/20 border-t border-white/10 relative z-10"
                prop.children [
                    Html.div [
                        prop.className "container mx-auto px-6 py-3 flex items-center justify-center gap-3"
                        prop.children [
                            Html.span [
                                prop.className "text-white/80 text-sm"
                                prop.text "Support the development of this free, open-source project!"
                            ]
                            Html.a [
                                prop.href "https://ko-fi.com/heimeshoff"
                                prop.target "_blank"
                                prop.rel "noopener noreferrer"
                                prop.className "inline-flex items-center gap-2 bg-[#FF5E5B] hover:bg-[#ff4742] text-white font-semibold px-4 py-1.5 rounded-full text-sm transition-colors"
                                prop.children [
                                    Html.span [ prop.text "☕" ]
                                    Html.span [ prop.text "Buy me a coffee on Ko-fi" ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]
