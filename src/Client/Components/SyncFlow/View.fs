module Components.SyncFlow.View

open Feliz
open Components.SyncFlow.Types
open Types
open Shared.Domain
open Client.DesignSystem

// Import from sub-modules
open Components.SyncFlow.Views.StatusViews
open Components.SyncFlow.Views.TransactionList
open Components.SyncFlow.Views.QuickAdd
open Components.SyncFlow.Views.SplitSheet

// TODO: derive from session model when bank name is available
let private bankName = "Comdirect"

// ============================================
// Main View (Composition)
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    // Check if we're in the reviewing state to use a custom header
    let isReviewing =
        match model.CurrentSession with
        | Success (Some session) ->
            match session.Status with
            | ReviewingTransactions -> true
            | _ -> false
        | _ -> false

    if isReviewing then
        // Custom review header + content wrapped in sf-app
        let transactionCount =
            match model.SyncTransactions with
            | Success txs -> txs.Length
            | _ -> 0

        React.fragment [
            // Review header as normal flowing content
            Html.div [
                prop.className "sf-review-header"
                prop.children [
                    Html.button [
                        prop.className "back-btn"
                        prop.ariaLabel "Zurück"
                        prop.onClick (fun _ -> dispatch CancelSync)
                        prop.children [
                            Icons.chevronLeft Icons.SM Icons.Default
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
                                    Html.span [ prop.text bankName ]
                                ]
                            ]
                        ]
                    ]

                    // Quick Add entry point during review
                    quickAddHeaderButton dispatch
                ]
            ]

            // Transaction list content
            transactionListView model dispatch

            // Quick Add (manual transaction entry)
            quickAddSheet model dispatch

            // Split-Review sheet (ynab-002)
            splitSheet model dispatch
        ]
    else
        // Standard layout for all non-reviewing states
        Html.div [
            prop.className "space-y-6"
            prop.children [
                // Show appropriate content based on session status
                match model.CurrentSession with
                | NotAsked ->
                    startSyncView dispatch

                | Loading ->
                    loadingView "Starting sync..."

                | Success (Some session) ->
                    match session.Status with
                    | AwaitingBankAuth ->
                        loadingView (sprintf "Verbindung zu %s..." bankName)

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
                        completedView session dispatch

                    | Failed error ->
                        errorView error dispatch

                | Success None ->
                    startSyncView dispatch

                | Failure error ->
                    errorView error dispatch

                // Quick Add (manual transaction entry)
                quickAddSheet model dispatch

                // Split-Review sheet (ynab-002)
                splitSheet model dispatch
            ]
        ]
