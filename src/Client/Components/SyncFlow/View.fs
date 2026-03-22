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
    // Check if we're in the reviewing state to use a custom header
    let isReviewing =
        match model.CurrentSession with
        | Success (Some session) ->
            match session.Status with
            | ReviewingTransactions -> true
            | _ -> false
        | _ -> false

    if isReviewing then
        // Custom review header + content wrapped in sync-flow-active
        let transactionCount =
            match model.SyncTransactions with
            | Success txs -> txs.Length
            | _ -> 0

        Html.div [
            prop.className "sync-flow-active"
            prop.children [
                // Custom review header matching prototype
                Html.header [
                    prop.className "sf-header"
                    prop.children [
                        Html.button [
                            prop.className "back-btn"
                            prop.ariaLabel "Zurück"
                            prop.onClick (fun _ -> dispatch CancelSync)
                            prop.children [
                                Svg.svg [
                                    svg.custom ("width", "18")
                                    svg.custom ("height", "18")
                                    svg.viewBox (0, 0, 24, 24)
                                    svg.fill "none"
                                    svg.stroke "currentColor"
                                    svg.custom ("strokeWidth", "2.5")
                                    svg.custom ("strokeLinecap", "round")
                                    svg.custom ("strokeLinejoin", "round")
                                    svg.children [
                                        Svg.path [ svg.d "M15 18l-6-6 6-6" ]
                                    ]
                                ]
                            ]
                        ]
                        Html.div [
                            prop.className "header-text"
                            prop.children [
                                Html.h1 [ prop.text "Import prüfen" ]
                                Html.div [
                                    prop.className "header-subtitle"
                                    prop.children [
                                        Html.span [ prop.text (sprintf "%d Transaktionen" transactionCount) ]
                                        Html.span [ prop.className "dot" ]
                                        Html.span [ prop.text "Comdirect" ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]

                // Transaction list content
                transactionListView model dispatch
            ]
        ]
    else
        // Standard layout for all non-reviewing states
        Html.div [
            prop.className "space-y-6"
            prop.children [
                // Header
                PageHeader.withActions
                    "Sync Transactions"
                    (Some "Fetch and categorize your bank transactions.")
                    [
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
                        // This branch should not be reached due to isReviewing check above
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
