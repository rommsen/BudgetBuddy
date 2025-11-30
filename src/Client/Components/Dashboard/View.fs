module Components.Dashboard.View

open Feliz
open Components.Dashboard.Types
open Types
open Shared.Domain

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
    let (color, text) =
        match status with
        | AwaitingBankAuth -> ("badge-warning", "Pending")
        | AwaitingTan -> ("badge-warning", "TAN")
        | FetchingTransactions -> ("badge-info", "Fetching")
        | ReviewingTransactions -> ("badge-primary", "Review")
        | ImportingToYnab -> ("badge-info", "Import")
        | Completed -> ("badge-success", "Done")
        | Failed _ -> ("badge-error", "Failed")
    Html.span [
        prop.className $"badge badge-sm {color}"
        prop.text text
    ]

// ============================================
// Stats Card Component
// ============================================

let private statsCard (icon: string) (title: string) (value: string) (description: string) (colorClass: string) =
    Html.div [
        prop.className "card bg-base-100 shadow-lg hover:shadow-xl transition-all duration-300 animate-slide-up"
        prop.children [
            Html.div [
                prop.className "card-body p-4 md:p-6"
                prop.children [
                    Html.div [
                        prop.className "flex items-start justify-between"
                        prop.children [
                            Html.div [
                                prop.className $"w-12 h-12 rounded-xl {colorClass} flex items-center justify-center text-white text-2xl"
                                prop.children [ Html.span [ prop.text icon ] ]
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "mt-4"
                        prop.children [
                            Html.p [
                                prop.className "text-sm font-medium text-base-content/60 uppercase tracking-wide"
                                prop.text title
                            ]
                            Html.p [
                                prop.className "text-2xl md:text-3xl font-bold mt-1 font-mono"
                                prop.text value
                            ]
                            Html.p [
                                prop.className "text-sm text-base-content/50 mt-1"
                                prop.text description
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Quick Action Card
// ============================================

let private quickActionCard (onNavigateToSync: unit -> unit) =
    Html.div [
        prop.className "card bg-gradient-to-br from-primary via-primary to-secondary text-primary-content shadow-xl overflow-hidden relative animate-slide-up"
        prop.children [
            Html.div [
                prop.className "absolute top-0 right-0 w-64 h-64 bg-white/5 rounded-full -translate-y-1/2 translate-x-1/2"
            ]
            Html.div [
                prop.className "absolute bottom-0 left-0 w-32 h-32 bg-white/5 rounded-full translate-y-1/2 -translate-x-1/2"
            ]

            Html.div [
                prop.className "card-body p-6 md:p-8 relative z-10"
                prop.children [
                    Html.div [
                        prop.className "flex flex-col md:flex-row md:items-center md:justify-between gap-4"
                        prop.children [
                            Html.div [
                                prop.children [
                                    Html.h2 [
                                        prop.className "text-2xl md:text-3xl font-bold"
                                        prop.text "Ready to Sync?"
                                    ]
                                    Html.p [
                                        prop.className "text-primary-content/80 mt-2 max-w-md"
                                        prop.text "Fetch new transactions from Comdirect and automatically categorize them for YNAB import."
                                    ]
                                ]
                            ]
                            Html.button [
                                prop.className "btn btn-lg bg-white/20 hover:bg-white/30 border-none text-white gap-2 group"
                                prop.onClick (fun _ -> onNavigateToSync())
                                prop.children [
                                    Html.span [ prop.className "text-xl"; prop.text "🔄" ]
                                    Html.span [ prop.text "Start Sync" ]
                                    Html.span [ prop.className "transition-transform group-hover:translate-x-1"; prop.text "→" ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// History Card Component
// ============================================

let private historyCard (session: SyncSession) =
    Html.div [
        prop.className "flex items-center justify-between p-4 bg-base-100 rounded-xl hover:bg-base-200/50 transition-colors"
        prop.children [
            Html.div [
                prop.className "flex items-center gap-4"
                prop.children [
                    Html.div [
                        prop.className "flex flex-col items-center justify-center w-12 h-12 bg-base-200 rounded-lg"
                        prop.children [
                            Html.span [
                                prop.className "text-xs font-medium text-base-content/60"
                                prop.text (session.StartedAt.ToString("MMM"))
                            ]
                            Html.span [
                                prop.className "text-lg font-bold"
                                prop.text (session.StartedAt.Day.ToString())
                            ]
                        ]
                    ]
                    Html.div [
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-2"
                                prop.children [
                                    Html.span [
                                        prop.className "font-medium"
                                        prop.text (session.StartedAt.ToString("HH:mm"))
                                    ]
                                    sessionStatusBadge session.Status
                                ]
                            ]
                            Html.p [
                                prop.className "text-sm text-base-content/60 mt-0.5"
                                prop.text $"{session.TransactionCount} transactions"
                            ]
                        ]
                    ]
                ]
            ]
            Html.div [
                prop.className "text-right"
                prop.children [
                    Html.p [
                        prop.className "font-mono font-medium text-success"
                        prop.text $"+{session.ImportedCount}"
                    ]
                    if session.SkippedCount > 0 then
                        Html.p [
                            prop.className "text-xs text-base-content/50"
                            prop.text $"{session.SkippedCount} skipped"
                        ]
                ]
            ]
        ]
    ]

// ============================================
// Warning Alert Component
// ============================================

let private warningAlert (message: string) (linkText: string) (onNavigateToSettings: unit -> unit) =
    Html.div [
        prop.className "alert bg-warning/10 border border-warning/20 animate-slide-up"
        prop.children [
            Html.span [ prop.className "text-xl"; prop.text "⚠️" ]
            Html.div [
                prop.className "flex-1"
                prop.children [
                    Html.span [ prop.className "text-base-content"; prop.text message ]
                ]
            ]
            Html.a [
                prop.className "btn btn-sm btn-warning"
                prop.text linkText
                prop.onClick (fun _ -> onNavigateToSettings())
            ]
        ]
    ]

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) (onNavigateToSync: unit -> unit) (onNavigateToSettings: unit -> unit) =
    Html.div [
        prop.className "space-y-6 md:space-y-8"
        prop.children [
            // Header
            Html.div [
                prop.className "animate-fade-in"
                prop.children [
                    Html.h1 [
                        prop.className "text-2xl md:text-4xl font-bold"
                        prop.text "Dashboard"
                    ]
                    Html.p [
                        prop.className "text-base-content/60 mt-1"
                        prop.text "Welcome back! Here's your sync overview."
                    ]
                ]
            ]

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
            Html.div [
                prop.className "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 md:gap-6"
                prop.children [
                    match model.RecentSessions with
                    | Success sessions when not sessions.IsEmpty ->
                        let lastSync = sessions |> List.head
                        let totalImported = sessions |> List.sumBy (fun s -> s.ImportedCount)

                        statsCard "🕐" "Last Sync" (formatDate lastSync.StartedAt) (sessionStatusText lastSync.Status) "bg-gradient-to-br from-blue-500 to-blue-600"
                        statsCard "✓" "Total Imported" (string totalImported) "transactions to YNAB" "bg-gradient-to-br from-emerald-500 to-emerald-600"
                        statsCard "📊" "Sync Sessions" (string sessions.Length) "in your history" "bg-gradient-to-br from-purple-500 to-purple-600"
                    | Success _ ->
                        statsCard "🕐" "Last Sync" "Never" "Start your first sync" "bg-gradient-to-br from-blue-500 to-blue-600"
                        statsCard "✓" "Total Imported" "0" "transactions to YNAB" "bg-gradient-to-br from-emerald-500 to-emerald-600"
                        statsCard "📊" "Sync Sessions" "0" "in your history" "bg-gradient-to-br from-purple-500 to-purple-600"
                    | Loading ->
                        for i in 1..3 do
                            Html.div [
                                prop.key i
                                prop.className "card bg-base-100 shadow-lg animate-pulse"
                                prop.children [
                                    Html.div [
                                        prop.className "card-body p-6"
                                        prop.children [
                                            Html.div [ prop.className "w-12 h-12 bg-base-200 rounded-xl" ]
                                            Html.div [ prop.className "h-4 bg-base-200 rounded w-20 mt-4" ]
                                            Html.div [ prop.className "h-8 bg-base-200 rounded w-16 mt-2" ]
                                            Html.div [ prop.className "h-3 bg-base-200 rounded w-24 mt-2" ]
                                        ]
                                    ]
                                ]
                            ]
                    | NotAsked ->
                        statsCard "🕐" "Last Sync" "-" "Loading..." "bg-gradient-to-br from-gray-400 to-gray-500"
                        statsCard "✓" "Total Imported" "-" "Loading..." "bg-gradient-to-br from-gray-400 to-gray-500"
                        statsCard "📊" "Sync Sessions" "-" "Loading..." "bg-gradient-to-br from-gray-400 to-gray-500"
                    | Failure _ ->
                        statsCard "🕐" "Last Sync" "Error" "Failed to load" "bg-gradient-to-br from-red-400 to-red-500"
                        statsCard "✓" "Total Imported" "Error" "Failed to load" "bg-gradient-to-br from-red-400 to-red-500"
                        statsCard "📊" "Sync Sessions" "Error" "Failed to load" "bg-gradient-to-br from-red-400 to-red-500"
                ]
            ]

            // Quick Action Card
            quickActionCard onNavigateToSync

            // Recent sync history
            Html.div [
                prop.className "card bg-base-100 shadow-lg animate-slide-up"
                prop.children [
                    Html.div [
                        prop.className "card-body p-4 md:p-6"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center justify-between mb-4"
                                prop.children [
                                    Html.h2 [
                                        prop.className "text-lg md:text-xl font-bold"
                                        prop.text "Recent Activity"
                                    ]
                                ]
                            ]

                            match model.RecentSessions with
                            | NotAsked ->
                                Html.div [
                                    prop.className "flex flex-col items-center justify-center py-12 text-base-content/50"
                                    prop.children [
                                        Html.div [ prop.className "loading loading-spinner loading-lg" ]
                                        Html.p [ prop.className "mt-4"; prop.text "Loading history..." ]
                                    ]
                                ]
                            | Loading ->
                                Html.div [
                                    prop.className "flex flex-col items-center justify-center py-12"
                                    prop.children [
                                        Html.div [ prop.className "loading loading-spinner loading-lg text-primary" ]
                                    ]
                                ]
                            | Success sessions when sessions.IsEmpty ->
                                Html.div [
                                    prop.className "flex flex-col items-center justify-center py-12 text-center"
                                    prop.children [
                                        Html.div [
                                            prop.className "w-16 h-16 rounded-full bg-base-200 flex items-center justify-center mb-4 text-3xl"
                                            prop.children [ Html.span [ prop.text "📋" ] ]
                                        ]
                                        Html.p [
                                            prop.className "font-medium text-base-content/70"
                                            prop.text "No sync history yet"
                                        ]
                                        Html.p [
                                            prop.className "text-sm text-base-content/50 mt-1"
                                            prop.text "Start your first sync to see your activity here."
                                        ]
                                    ]
                                ]
                            | Success sessions ->
                                Html.div [
                                    prop.className "space-y-2"
                                    prop.children [
                                        for session in sessions |> List.take (min 5 sessions.Length) do
                                            historyCard session
                                    ]
                                ]
                            | Failure error ->
                                Html.div [
                                    prop.className "alert alert-error"
                                    prop.children [
                                        Html.span [ prop.text $"Failed to load history: {error}" ]
                                    ]
                                ]
                        ]
                    ]
                ]
            ]
        ]
    ]
