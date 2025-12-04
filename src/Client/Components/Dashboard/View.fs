module Components.Dashboard.View

open Feliz
open Components.Dashboard.Types
open Types
open Shared.Domain
open Client.DesignSystem

// ============================================
// Helper Functions
// ============================================

let private formatDate (date: System.DateTime) =
    date.ToString("dd.MM.yyyy HH:mm")

let private sessionStatusText (status: SyncSessionStatus) =
    match status with
    | AwaitingBankAuth -> "Awaiting Bank Auth"
    | AwaitingTan -> "Awaiting TAN"
    | FetchingTransactions -> "Fetching Transactions"
    | ReviewingTransactions -> "Reviewing"
    | ImportingToYnab -> "Importing to YNAB"
    | Completed -> "Completed"
    | Failed msg -> $"Failed: {msg}"

let private sessionStatusBadge (status: SyncSessionStatus) =
    match status with
    | AwaitingBankAuth -> Badge.warning "Pending"
    | AwaitingTan -> Badge.warning "TAN"
    | FetchingTransactions -> Badge.info "Fetching"
    | ReviewingTransactions -> Badge.info "Review"
    | ImportingToYnab -> Badge.info "Import"
    | Completed -> Badge.success "Done"
    | Failed _ -> Badge.error "Failed"

// ============================================
// Stats Section using Stats Component
// ============================================

let private statsSection (model: Model) =
    match model.RecentSessions with
    | Success sessions when not sessions.IsEmpty ->
        let lastSync = sessions |> List.head
        let totalImported = sessions |> List.sumBy (fun s -> s.ImportedCount)

        Stats.grid [
            Stats.view {
                Stats.defaultProps with
                    Label = "Last Sync"
                    Value = formatDate lastSync.StartedAt
                    Icon = Some (Icons.sync Icons.MD Icons.Default)
                    Description = Some (sessionStatusText lastSync.Status)
                    Accent = Stats.Teal
            }
            Stats.view {
                Stats.defaultProps with
                    Label = "Total Imported"
                    Value = string totalImported
                    Icon = Some (Icons.check Icons.MD Icons.NeonGreen)
                    Description = Some "transactions to YNAB"
                    Accent = Stats.Green
            }
            Stats.view {
                Stats.defaultProps with
                    Label = "Sync Sessions"
                    Value = string sessions.Length
                    Icon = Some (Icons.dashboard Icons.MD Icons.Default)
                    Description = Some "in your history"
                    Accent = Stats.Purple
            }
        ]

    | Success _ ->
        Stats.grid [
            Stats.view {
                Stats.defaultProps with
                    Label = "Last Sync"
                    Value = "Never"
                    Icon = Some (Icons.sync Icons.MD Icons.Default)
                    Description = Some "Start your first sync"
                    Accent = Stats.Teal
            }
            Stats.view {
                Stats.defaultProps with
                    Label = "Total Imported"
                    Value = "0"
                    Icon = Some (Icons.check Icons.MD Icons.Default)
                    Description = Some "transactions to YNAB"
                    Accent = Stats.Green
            }
            Stats.view {
                Stats.defaultProps with
                    Label = "Sync Sessions"
                    Value = "0"
                    Icon = Some (Icons.dashboard Icons.MD Icons.Default)
                    Description = Some "in your history"
                    Accent = Stats.Purple
            }
        ]

    | Loading -> Loading.statsGridSkeleton 3

    | NotAsked -> Loading.statsGridSkeleton 3

    | Failure _ ->
        Stats.grid [
            Stats.view {
                Stats.defaultProps with
                    Label = "Last Sync"
                    Value = "Error"
                    Description = Some "Failed to load"
                    Accent = Stats.Pink
            }
            Stats.view {
                Stats.defaultProps with
                    Label = "Total Imported"
                    Value = "Error"
                    Description = Some "Failed to load"
                    Accent = Stats.Pink
            }
            Stats.view {
                Stats.defaultProps with
                    Label = "Sync Sessions"
                    Value = "Error"
                    Description = Some "Failed to load"
                    Accent = Stats.Pink
            }
        ]

// ============================================
// Quick Action Card using Card Component
// ============================================

let private quickActionCard (onNavigateToSync: unit -> unit) =
    Html.div [
        prop.className "rounded-xl bg-gradient-to-br from-neon-orange/20 via-neon-orange/10 to-neon-teal/10 border border-neon-orange/30 p-5 md:p-8 shadow-[0_0_30px_rgba(255,107,44,0.15)] hover:shadow-[0_0_40px_rgba(255,107,44,0.25)] transition-all duration-300 relative overflow-hidden"
        prop.children [
            // Decorative circles
            Html.div [
                prop.className "absolute top-0 right-0 w-64 h-64 bg-neon-orange/5 rounded-full -translate-y-1/2 translate-x-1/2"
            ]
            Html.div [
                prop.className "absolute bottom-0 left-0 w-32 h-32 bg-neon-teal/5 rounded-full translate-y-1/2 -translate-x-1/2"
            ]

            Html.div [
                prop.className "relative z-10 flex flex-col md:flex-row md:items-center md:justify-between gap-4"
                prop.children [
                    Html.div [
                        prop.children [
                            Html.h2 [
                                prop.className "text-xl md:text-2xl font-bold font-display text-base-content"
                                prop.text "Ready to Sync?"
                            ]
                            Html.p [
                                prop.className "text-base-content/60 mt-2 max-w-md text-sm md:text-base"
                                prop.text "Fetch new transactions from Comdirect and automatically categorize them for YNAB import."
                            ]
                        ]
                    ]
                    Button.view {
                        Button.defaultProps with
                            Text = "Start Sync"
                            OnClick = onNavigateToSync
                            Variant = Button.Primary
                            Size = Button.Large
                            Icon = Some (Icons.sync Icons.SM Icons.Primary)
                    }
                ]
            ]
        ]
    ]

// ============================================
// History Card using Card Component
// ============================================

let private historyItem (session: SyncSession) =
    Html.div [
        prop.className "flex items-center justify-between p-3 md:p-4 bg-base-200/30 rounded-lg hover:bg-base-200/50 transition-colors"
        prop.children [
            Html.div [
                prop.className "flex items-center gap-3 md:gap-4"
                prop.children [
                    // Date badge
                    Html.div [
                        prop.className "flex flex-col items-center justify-center w-10 h-10 md:w-12 md:h-12 bg-base-100 rounded-lg border border-white/5"
                        prop.children [
                            Html.span [
                                prop.className "text-[10px] md:text-xs font-medium text-base-content/50 uppercase"
                                prop.text (session.StartedAt.ToString("MMM"))
                            ]
                            Html.span [
                                prop.className "text-sm md:text-lg font-bold text-base-content font-mono"
                                prop.text (session.StartedAt.Day.ToString())
                            ]
                        ]
                    ]
                    // Details
                    Html.div [
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-2"
                                prop.children [
                                    Html.span [
                                        prop.className "font-medium text-base-content text-sm md:text-base"
                                        prop.text (session.StartedAt.ToString("HH:mm"))
                                    ]
                                    sessionStatusBadge session.Status
                                ]
                            ]
                            Html.p [
                                prop.className "text-xs md:text-sm text-base-content/50 mt-0.5"
                                prop.text $"{session.TransactionCount} transactions"
                            ]
                        ]
                    ]
                ]
            ]
            // Imported count
            Html.div [
                prop.className "text-right"
                prop.children [
                    Html.p [
                        prop.className "font-mono font-semibold text-neon-green text-sm md:text-base"
                        prop.text $"+{session.ImportedCount}"
                    ]
                    if session.SkippedCount > 0 then
                        Html.p [
                            prop.className "text-xs text-base-content/40"
                            prop.text $"{session.SkippedCount} skipped"
                        ]
                ]
            ]
        ]
    ]

let private historySection (model: Model) =
    Card.standard [
        Card.header "Recent Activity" None None

        match model.RecentSessions with
        | NotAsked | Loading ->
            Loading.centered "Loading history..."

        | Success sessions when sessions.IsEmpty ->
            Card.emptyState
                (Icons.dashboard Icons.XL Icons.Default)
                "No sync history yet"
                "Start your first sync to see your activity here."
                (Some (Button.secondary "Start Sync" ignore))

        | Success sessions ->
            Html.div [
                prop.className "space-y-2"
                prop.children [
                    for session in sessions |> List.take (min 5 sessions.Length) do
                        historyItem session
                ]
            ]

        | Failure error ->
            Html.div [
                prop.className "flex items-center gap-3 p-4 rounded-lg bg-neon-red/10 border border-neon-red/30"
                prop.children [
                    Icons.xCircle Icons.MD Icons.Error
                    Html.span [
                        prop.className "text-neon-red text-sm"
                        prop.text $"Failed to load history: {error}"
                    ]
                ]
            ]
    ]

// ============================================
// Warning Alert Component
// ============================================

let private warningAlert (message: string) (linkText: string) (onNavigateToSettings: unit -> unit) =
    Html.div [
        prop.className "flex items-center gap-3 p-4 rounded-xl bg-neon-orange/10 border border-neon-orange/30 animate-fade-in"
        prop.children [
            Icons.warning Icons.MD Icons.NeonOrange
            Html.div [
                prop.className "flex-1"
                prop.children [
                    Html.span [
                        prop.className "text-base-content text-sm md:text-base"
                        prop.text message
                    ]
                ]
            ]
            Button.view {
                Button.defaultProps with
                    Text = linkText
                    OnClick = onNavigateToSettings
                    Variant = Button.Secondary
                    Size = Button.Small
            }
        ]
    ]

// ============================================
// Page Header
// ============================================

let private pageHeader =
    Html.div [
        prop.className "animate-fade-in"
        prop.children [
            Html.h1 [
                prop.className "text-2xl md:text-3xl font-bold font-display"
                prop.text "Dashboard"
            ]
            Html.p [
                prop.className "text-base-content/50 mt-1 text-sm md:text-base"
                prop.text "Welcome back! Here's your sync overview."
            ]
        ]
    ]

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) (onNavigateToSync: unit -> unit) (onNavigateToSettings: unit -> unit) =
    Html.div [
        prop.className "space-y-5 md:space-y-6"
        prop.children [
            // Header
            pageHeader

            // Configuration warnings
            match model.Settings with
            | Success settings ->
                if settings.Ynab.IsNone then
                    warningAlert "YNAB is not configured." "Configure YNAB" onNavigateToSettings
                elif settings.Comdirect.IsNone then
                    warningAlert "Comdirect is not configured." "Configure Comdirect" onNavigateToSettings
                else
                    Html.none
            | _ -> Html.none

            // Stats Grid
            statsSection model

            // Quick Action Card
            quickActionCard onNavigateToSync

            // Recent sync history
            historySection model
        ]
    ]
