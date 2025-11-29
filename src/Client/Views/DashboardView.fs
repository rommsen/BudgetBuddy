module Views.DashboardView

open Feliz
open State
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
        | AwaitingBankAuth -> ("badge-warning", "Awaiting Bank Auth")
        | AwaitingTan -> ("badge-warning", "Awaiting TAN")
        | FetchingTransactions -> ("badge-info", "Fetching")
        | ReviewingTransactions -> ("badge-primary", "Reviewing")
        | ImportingToYnab -> ("badge-info", "Importing")
        | Completed -> ("badge-success", "Completed")
        | Failed _ -> ("badge-error", "Failed")
    Html.span [
        prop.className $"badge {color}"
        prop.text text
    ]

// ============================================
// Stats Card Component
// ============================================

let private statsCard (title: string) (value: string) (description: string) =
    Html.div [
        prop.className "stat"
        prop.children [
            Html.div [
                prop.className "stat-title"
                prop.text title
            ]
            Html.div [
                prop.className "stat-value"
                prop.text value
            ]
            Html.div [
                prop.className "stat-desc"
                prop.text description
            ]
        ]
    ]

// ============================================
// History Table Component
// ============================================

let private historyRow (session: SyncSession) =
    Html.tr [
        prop.children [
            Html.td [ prop.text (formatDate session.StartedAt) ]
            Html.td [ sessionStatusBadge session.Status ]
            Html.td [ prop.text (string session.TransactionCount) ]
            Html.td [ prop.text (string session.ImportedCount) ]
            Html.td [ prop.text (string session.SkippedCount) ]
        ]
    ]

let private historyTable (sessions: SyncSession list) =
    Html.div [
        prop.className "overflow-x-auto"
        prop.children [
            Html.table [
                prop.className "table table-zebra"
                prop.children [
                    Html.thead [
                        Html.tr [
                            Html.th [ prop.text "Date" ]
                            Html.th [ prop.text "Status" ]
                            Html.th [ prop.text "Transactions" ]
                            Html.th [ prop.text "Imported" ]
                            Html.th [ prop.text "Skipped" ]
                        ]
                    ]
                    Html.tbody [
                        for session in sessions do
                            historyRow session
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-6"
        prop.children [
            // Header
            Html.h1 [
                prop.className "text-3xl font-bold"
                prop.text "Dashboard"
            ]

            // Stats
            Html.div [
                prop.className "stats shadow w-full"
                prop.children [
                    match model.RecentSessions with
                    | Success sessions when not sessions.IsEmpty ->
                        let lastSync = sessions |> List.head
                        let totalImported = sessions |> List.sumBy (fun s -> s.ImportedCount)
                        let totalTransactions = sessions |> List.sumBy (fun s -> s.TransactionCount)

                        statsCard "Last Sync" (formatDate lastSync.StartedAt) (sessionStatusText lastSync.Status)
                        statsCard "Total Imported" (string totalImported) "transactions to YNAB"
                        statsCard "Recent Sessions" (string sessions.Length) "sync sessions"
                    | Success _ ->
                        statsCard "Last Sync" "Never" "Start your first sync"
                        statsCard "Total Imported" "0" "transactions to YNAB"
                        statsCard "Recent Sessions" "0" "sync sessions"
                    | Loading ->
                        statsCard "Last Sync" "..." "Loading"
                        statsCard "Total Imported" "..." "Loading"
                        statsCard "Recent Sessions" "..." "Loading"
                    | NotAsked ->
                        statsCard "Last Sync" "-" "Not loaded"
                        statsCard "Total Imported" "-" "Not loaded"
                        statsCard "Recent Sessions" "-" "Not loaded"
                    | Failure _ ->
                        statsCard "Last Sync" "Error" "Failed to load"
                        statsCard "Total Imported" "Error" "Failed to load"
                        statsCard "Recent Sessions" "Error" "Failed to load"
                ]
            ]

            // Configuration warnings
            match model.Settings with
            | Success settings ->
                if settings.Ynab.IsNone then
                    Html.div [
                        prop.className "alert alert-warning"
                        prop.children [
                            Html.span [ prop.text "YNAB is not configured. " ]
                            Html.a [
                                prop.className "link link-primary"
                                prop.text "Go to Settings"
                                prop.onClick (fun _ -> dispatch (NavigateTo Settings))
                            ]
                        ]
                    ]
                elif settings.Comdirect.IsNone then
                    Html.div [
                        prop.className "alert alert-warning"
                        prop.children [
                            Html.span [ prop.text "Comdirect is not configured. " ]
                            Html.a [
                                prop.className "link link-primary"
                                prop.text "Go to Settings"
                                prop.onClick (fun _ -> dispatch (NavigateTo Settings))
                            ]
                        ]
                    ]
                else
                    Html.none
            | _ -> Html.none

            // Start sync card
            Html.div [
                prop.className "card bg-primary text-primary-content"
                prop.children [
                    Html.div [
                        prop.className "card-body items-center text-center"
                        prop.children [
                            Html.h2 [
                                prop.className "card-title text-2xl"
                                prop.text "Ready to Sync?"
                            ]
                            Html.p [
                                prop.text "Fetch new transactions from Comdirect and import them to YNAB"
                            ]
                            Html.div [
                                prop.className "card-actions"
                                prop.children [
                                    Html.button [
                                        prop.className "btn btn-lg"
                                        prop.text "Start New Sync"
                                        prop.onClick (fun _ -> dispatch (NavigateTo SyncFlow))
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]

            // Recent sync history
            Html.div [
                prop.className "card bg-base-100 shadow-xl"
                prop.children [
                    Html.div [
                        prop.className "card-body"
                        prop.children [
                            Html.h2 [
                                prop.className "card-title"
                                prop.text "Recent Syncs"
                            ]

                            match model.RecentSessions with
                            | NotAsked ->
                                Html.div [
                                    prop.className "text-center p-4"
                                    prop.text "Click to load history"
                                ]
                            | Loading ->
                                Html.div [
                                    prop.className "flex justify-center p-4"
                                    prop.children [
                                        Html.span [ prop.className "loading loading-spinner loading-lg" ]
                                    ]
                                ]
                            | Success sessions when sessions.IsEmpty ->
                                Html.div [
                                    prop.className "text-center p-4 text-gray-500"
                                    prop.text "No sync history yet. Start your first sync above!"
                                ]
                            | Success sessions ->
                                historyTable sessions
                            | Failure error ->
                                Html.div [
                                    prop.className "alert alert-error"
                                    prop.text $"Failed to load history: {error}"
                                ]
                        ]
                    ]
                ]
            ]
        ]
    ]
