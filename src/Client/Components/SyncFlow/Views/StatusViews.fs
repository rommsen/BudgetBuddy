module Components.SyncFlow.Views.StatusViews

open Feliz
open Components.SyncFlow.Types
open Shared.Domain
open Client.DesignSystem

// ============================================
// TAN Waiting View
// ============================================

let tanWaitingView (isConfirming: bool) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "max-w-lg mx-auto px-4 animate-fade-in"
        prop.children [
            Card.view { Card.defaultProps with Variant = Card.Glow; Size = Card.Spacious } [
                Html.div [
                    prop.className "flex flex-col items-center text-center"
                    prop.children [
                        // Animated phone icon with neon glow
                        Html.div [
                            prop.className "relative"
                            prop.children [
                                Html.div [
                                    prop.className "w-24 h-24 rounded-2xl bg-gradient-to-br from-neon-teal to-neon-green flex items-center justify-center shadow-glow-teal animate-neon-pulse"
                                    prop.children [
                                        Html.span [
                                            prop.className "text-5xl"
                                            prop.text "📱"
                                        ]
                                    ]
                                ]
                                // Pulsing notification dot
                                Html.div [
                                    prop.className "absolute -top-1 -right-1"
                                    prop.children [ Badge.pulsingDot Badge.Orange ]
                                ]
                                Html.div [
                                    prop.className "absolute -top-2 -right-2 w-6 h-6 bg-neon-orange rounded-full flex items-center justify-center animate-bounce shadow-glow-orange"
                                    prop.children [
                                        Html.span [
                                            prop.className "text-[#0a0a0f] text-xs font-bold"
                                            prop.text "1"
                                        ]
                                    ]
                                ]
                            ]
                        ]

                        Html.h2 [
                            prop.className "text-xl md:text-2xl font-bold font-display mt-6 text-base-content"
                            prop.text "TAN Confirmation Required"
                        ]
                        Html.p [
                            prop.className "text-base-content/60 mt-2 max-w-sm"
                            prop.text "Please open your banking app and confirm the push TAN notification to authorize the connection."
                        ]

                        // Steps indicator with neon styling (responsive)
                        Html.div [
                            prop.className "flex items-center gap-2 sm:gap-3 mt-6 text-xs sm:text-sm"
                            prop.children [
                                Html.div [
                                    prop.className "flex items-center gap-1 sm:gap-2 text-neon-green"
                                    prop.children [
                                        Html.div [
                                            prop.className "w-5 h-5 sm:w-6 sm:h-6 rounded-full bg-neon-green text-[#0a0a0f] flex items-center justify-center text-xs font-bold"
                                            prop.children [ Icons.check Icons.XS Icons.Primary ]
                                        ]
                                        Html.span [ prop.className "hidden sm:inline"; prop.text "Connected" ]
                                    ]
                                ]
                                Html.div [ prop.className "w-4 sm:w-8 h-0.5 bg-neon-teal/30" ]
                                Html.div [
                                    prop.className "flex items-center gap-1 sm:gap-2 text-neon-teal"
                                    prop.children [
                                        Html.div [
                                            prop.className "w-5 h-5 sm:w-6 sm:h-6 rounded-full bg-neon-teal/20 border border-neon-teal flex items-center justify-center"
                                            prop.children [ Loading.spinner Loading.XS Loading.Teal ]
                                        ]
                                        Html.span [ prop.className "font-medium"; prop.text "TAN" ]
                                    ]
                                ]
                                Html.div [ prop.className "w-4 sm:w-8 h-0.5 bg-base-content/10" ]
                                Html.div [
                                    prop.className "flex items-center gap-1 sm:gap-2 text-base-content/40"
                                    prop.children [
                                        Html.div [ prop.className "w-5 h-5 sm:w-6 sm:h-6 rounded-full bg-base-200 flex items-center justify-center text-xs font-bold"; prop.text "3" ]
                                        Html.span [ prop.className "hidden sm:inline"; prop.text "Fetch" ]
                                    ]
                                ]
                            ]
                        ]

                        // Action buttons
                        Html.div [
                            prop.className "flex flex-col sm:flex-row gap-3 mt-8 w-full sm:w-auto"
                            prop.children [
                                if isConfirming then
                                    Button.primaryLoading "Importing..." true (fun () -> ())
                                else
                                    Button.primaryWithIcon "I've Confirmed" (Icons.check Icons.SM Icons.Primary) (fun () -> dispatch ConfirmTan)
                                Button.ghost "Cancel" (fun () -> dispatch CancelSync)
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Fetching Transactions View
// ============================================

let fetchingView () =
    Html.div [
        prop.className "max-w-lg mx-auto px-4 animate-fade-in"
        prop.children [
            Card.view { Card.defaultProps with Variant = Card.Glow; Size = Card.Spacious } [
                Html.div [
                    prop.className "flex flex-col items-center text-center"
                    prop.children [
                        // Animated sync icon with neon glow
                        Html.div [
                            prop.className "relative"
                            prop.children [
                                Html.div [
                                    prop.className "w-24 h-24 rounded-2xl bg-gradient-to-br from-neon-teal to-neon-green flex items-center justify-center shadow-glow-teal animate-neon-pulse"
                                    prop.children [
                                        Html.div [
                                            prop.className "animate-spin"
                                            prop.children [ Icons.sync Icons.XL Icons.Primary ]
                                        ]
                                    ]
                                ]
                            ]
                        ]

                        Html.h2 [
                            prop.className "text-xl md:text-2xl font-bold font-display mt-6 text-base-content"
                            prop.text "Fetching Transactions"
                        ]
                        Html.p [
                            prop.className "text-base-content/60 mt-2 max-w-sm"
                            prop.text "Retrieving your transactions from Comdirect. This may take a moment..."
                        ]

                        // Steps indicator with neon styling - Fetch is now active (responsive)
                        Html.div [
                            prop.className "flex items-center gap-2 sm:gap-3 mt-6 text-xs sm:text-sm"
                            prop.children [
                                Html.div [
                                    prop.className "flex items-center gap-1 sm:gap-2 text-neon-green"
                                    prop.children [
                                        Html.div [
                                            prop.className "w-5 h-5 sm:w-6 sm:h-6 rounded-full bg-neon-green text-[#0a0a0f] flex items-center justify-center text-xs font-bold"
                                            prop.children [ Icons.check Icons.XS Icons.Primary ]
                                        ]
                                        Html.span [ prop.className "hidden sm:inline"; prop.text "Connected" ]
                                    ]
                                ]
                                Html.div [ prop.className "w-4 sm:w-8 h-0.5 bg-neon-green" ]
                                Html.div [
                                    prop.className "flex items-center gap-1 sm:gap-2 text-neon-green"
                                    prop.children [
                                        Html.div [
                                            prop.className "w-5 h-5 sm:w-6 sm:h-6 rounded-full bg-neon-green text-[#0a0a0f] flex items-center justify-center text-xs font-bold"
                                            prop.children [ Icons.check Icons.XS Icons.Primary ]
                                        ]
                                        Html.span [ prop.className "hidden sm:inline"; prop.text "TAN" ]
                                    ]
                                ]
                                Html.div [ prop.className "w-4 sm:w-8 h-0.5 bg-neon-teal/30" ]
                                Html.div [
                                    prop.className "flex items-center gap-1 sm:gap-2 text-neon-teal"
                                    prop.children [
                                        Html.div [
                                            prop.className "w-5 h-5 sm:w-6 sm:h-6 rounded-full bg-neon-teal/20 border border-neon-teal flex items-center justify-center"
                                            prop.children [ Loading.spinner Loading.XS Loading.Teal ]
                                        ]
                                        Html.span [ prop.className "font-medium"; prop.text "Fetch" ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Loading View (Generic)
// ============================================

let loadingView (message: string) =
    Html.div [
        prop.className "max-w-md mx-auto px-4 animate-fade-in"
        prop.children [
            Card.view { Card.defaultProps with Variant = Card.Glass; Size = Card.Spacious } [
                Html.div [
                    prop.className "flex flex-col items-center text-center py-8"
                    prop.children [
                        Loading.neonPulse Loading.Teal
                        Html.p [
                            prop.className "mt-6 font-medium text-base-content/80"
                            prop.text message
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Error View
// ============================================

let errorView (error: string) (dispatch: Msg -> unit) =
    ErrorDisplay.hero "Sync Failed" error "Try Again" (Icons.sync Icons.SM Icons.Primary) (fun () -> dispatch StartSync)

// ============================================
// Completed View
// ============================================

let completedView (session: SyncSession) (dispatch: Msg -> unit) (onNavigateToDashboard: unit -> unit) =
    Html.div [
        prop.className "max-w-lg mx-auto px-4 animate-fade-in"
        prop.children [
            Html.div [
                prop.className "rounded-xl bg-base-100 border border-white/5 overflow-hidden"
                prop.children [
                    // Success header with neon gradient
                    Html.div [
                        prop.className "bg-gradient-to-br from-neon-teal to-neon-green p-8 text-center"
                        prop.children [
                            Html.div [
                                prop.className "w-20 h-20 rounded-full bg-white/20 flex items-center justify-center mx-auto mb-4 shadow-glow-green"
                                prop.children [ Icons.checkCircle Icons.XL Icons.Primary ]
                            ]
                            Html.h2 [
                                prop.className "text-2xl md:text-3xl font-bold font-display text-[#0a0a0f]"
                                prop.text "Sync Complete!"
                            ]
                            Html.p [
                                prop.className "text-[#0a0a0f]/70 mt-2"
                                prop.text "Your transactions have been imported to YNAB."
                            ]
                        ]
                    ]

                    Html.div [
                        prop.className "p-6"
                        prop.children [
                            // Stats grid with neon styling
                            Html.div [
                                prop.className "grid grid-cols-3 gap-3 -mt-12 mb-6"
                                prop.children [
                                    Html.div [
                                        prop.className "bg-base-100 rounded-xl border border-white/5 shadow-lg p-4 text-center"
                                        prop.children [
                                            Html.p [ prop.className "text-2xl font-bold font-mono text-base-content"; prop.text (string session.TransactionCount) ]
                                            Html.p [ prop.className "text-xs text-base-content/60 uppercase tracking-wider"; prop.text "Total" ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "bg-base-100 rounded-xl border border-neon-green/30 shadow-lg p-4 text-center"
                                        prop.children [
                                            Html.p [ prop.className "text-2xl font-bold font-mono text-neon-green"; prop.text (string session.ImportedCount) ]
                                            Html.p [ prop.className "text-xs text-base-content/60 uppercase tracking-wider"; prop.text "Imported" ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "bg-base-100 rounded-xl border border-white/5 shadow-lg p-4 text-center"
                                        prop.children [
                                            Html.p [ prop.className "text-2xl font-bold font-mono text-base-content/40"; prop.text (string session.SkippedCount) ]
                                            Html.p [ prop.className "text-xs text-base-content/60 uppercase tracking-wider"; prop.text "Skipped" ]
                                        ]
                                    ]
                                ]
                            ]

                            // Actions
                            Button.group [
                                Button.primaryWithIcon "Sync Again" (Icons.sync Icons.SM Icons.Primary) (fun () -> dispatch StartSync)
                                Button.secondary "Back to Dashboard" (fun () -> onNavigateToDashboard())
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Start Sync View
// ============================================

let startSyncView (dispatch: Msg -> unit) =
    Html.div [
        prop.className "max-w-lg mx-auto px-4 animate-fade-in"
        prop.children [
            Html.div [
                prop.className "rounded-xl bg-base-100 border border-white/5 overflow-hidden"
                prop.children [
                    // Header with neon gradient
                    Html.div [
                        prop.className "bg-gradient-to-br from-neon-orange to-neon-pink p-8 text-center"
                        prop.children [
                            Html.div [
                                prop.className "w-20 h-20 rounded-full bg-white/20 flex items-center justify-center mx-auto mb-4 shadow-glow-orange"
                                prop.children [ Icons.sync Icons.XL Icons.Primary ]
                            ]
                            Html.h2 [
                                prop.className "text-2xl md:text-3xl font-bold font-display text-white"
                                prop.text "Ready to Sync"
                            ]
                            Html.p [
                                prop.className "text-white/80 mt-2"
                                prop.text "Connect to your bank and import transactions."
                            ]
                        ]
                    ]

                    Html.div [
                        prop.className "p-6"
                        prop.children [
                            // Features list with neon accents
                            Html.div [
                                prop.className "space-y-4 mb-6"
                                prop.children [
                                    Html.div [
                                        prop.className "flex items-center gap-3"
                                        prop.children [
                                            Html.div [
                                                prop.className "w-10 h-10 rounded-full bg-neon-teal/10 flex items-center justify-center flex-shrink-0"
                                                prop.children [ Icons.creditCard Icons.MD Icons.NeonTeal ]
                                            ]
                                            Html.div [
                                                prop.children [
                                                    Html.p [ prop.className "font-medium text-base-content"; prop.text "Secure Connection" ]
                                                    Html.p [ prop.className "text-sm text-base-content/60"; prop.text "Bank-level encryption for your data" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "flex items-center gap-3"
                                        prop.children [
                                            Html.div [
                                                prop.className "w-10 h-10 rounded-full bg-neon-green/10 flex items-center justify-center flex-shrink-0"
                                                prop.children [ Icons.rules Icons.MD Icons.NeonGreen ]
                                            ]
                                            Html.div [
                                                prop.children [
                                                    Html.p [ prop.className "font-medium text-base-content"; prop.text "Auto-Categorization" ]
                                                    Html.p [ prop.className "text-sm text-base-content/60"; prop.text "Rules automatically categorize transactions" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "flex items-center gap-3"
                                        prop.children [
                                            Html.div [
                                                prop.className "w-10 h-10 rounded-full bg-neon-purple/10 flex items-center justify-center flex-shrink-0"
                                                prop.children [ Icons.upload Icons.MD Icons.NeonPurple ]
                                            ]
                                            Html.div [
                                                prop.children [
                                                    Html.p [ prop.className "font-medium text-base-content"; prop.text "YNAB Import" ]
                                                    Html.p [ prop.className "text-sm text-base-content/60"; prop.text "Direct import to your YNAB budget" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]

                            Button.view {
                                Button.defaultProps with
                                    Text = "Start Sync"
                                    Variant = Button.Primary
                                    Size = Button.Large
                                    FullWidth = true
                                    Icon = Some (Icons.sync Icons.SM Icons.Primary)
                                    OnClick = fun () -> dispatch StartSync
                            }
                        ]
                    ]
                ]
            ]
        ]
    ]
