module Components.SyncFlow.View

open Feliz
open Components.SyncFlow.Types
open Types
open Shared.Domain
open Client.DesignSystem

// Import from sub-modules
open Components.SyncFlow.Views.StatusViews
open Components.SyncFlow.Views.TransactionList

// ============================================
// Main View (Composition)
// ============================================

let view (model: Model) (dispatch: Msg -> unit) (onNavigateToDashboard: unit -> unit) =
    Html.div [
        prop.className "space-y-6"
        prop.children [
            // Header with neon styling
            Html.div [
                prop.className "flex items-center justify-between animate-fade-in"
                prop.children [
                    Html.div [
                        prop.children [
                            Html.h1 [
                                prop.className "text-2xl md:text-4xl font-bold font-display text-base-content"
                                prop.text "Sync Transactions"
                            ]
                            Html.p [
                                prop.className "text-base-content/60 mt-1"
                                prop.text "Fetch and categorize your bank transactions."
                            ]
                        ]
                    ]
                    Button.view {
                        Button.defaultProps with
                            Text = ""
                            OnClick = fun () ->
                                dispatch LoadTransactions
                                dispatch LoadCurrentSession
                            Variant = Button.Ghost
                            Icon = Some (Icons.sync Icons.SM Icons.Default)
                            Title = Some "Refresh transactions"
                    }
                ]
            ]

            // Show appropriate content based on session status
            match model.CurrentSession with
            | NotAsked ->
                startSyncView dispatch

            | Loading ->
                loadingView "Starting sync..."

            | Success (Some session) ->
                match session.Status with
                | AwaitingBankAuth ->
                    loadingView "Connecting to Comdirect..."

                | AwaitingTan ->
                    tanWaitingView model.IsTanConfirming dispatch

                | FetchingTransactions ->
                    fetchingView ()

                | ReviewingTransactions ->
                    transactionListView model dispatch

                | ImportingToYnab ->
                    loadingView "Importing to YNAB..."

                | Completed ->
                    completedView session dispatch onNavigateToDashboard

                | Failed error ->
                    errorView error dispatch

            | Success None ->
                startSyncView dispatch

            | Failure error ->
                errorView error dispatch
        ]
    ]
