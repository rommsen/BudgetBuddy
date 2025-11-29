module Views.SyncFlowView

open Feliz
open State
open Types
open Shared.Domain

// ============================================
// Helper Functions
// ============================================

let private formatDate (date: System.DateTime) =
    date.ToString("dd.MM.yyyy")

let private formatAmount (amount: Money) =
    let sign = if amount.Amount < 0m then "" else "+"
    $"{sign}{amount.Amount:N2} {amount.Currency}"

// ============================================
// Status Badge Component
// ============================================

let private statusBadge (status: TransactionStatus) =
    let (color, text) =
        match status with
        | Pending -> ("badge-error", "Uncategorized")
        | AutoCategorized -> ("badge-success", "Auto")
        | ManualCategorized -> ("badge-info", "Manual")
        | NeedsAttention -> ("badge-warning", "Review")
        | Skipped -> ("badge-ghost", "Skipped")
        | Imported -> ("badge-success", "Imported")
    Html.span [
        prop.className $"badge {color}"
        prop.text text
    ]

// ============================================
// TAN Waiting View
// ============================================

let private tanWaitingView (dispatch: Msg -> unit) =
    Html.div [
        prop.className "card bg-base-100 shadow-xl"
        prop.children [
            Html.div [
                prop.className "card-body items-center text-center"
                prop.children [
                    Html.div [
                        prop.className "text-6xl mb-4"
                        prop.text "📱"
                    ]
                    Html.h2 [
                        prop.className "card-title text-2xl"
                        prop.text "Waiting for TAN Confirmation"
                    ]
                    Html.p [
                        prop.className "text-gray-600 mb-4"
                        prop.text "Please confirm the push TAN notification on your phone to continue."
                    ]
                    Html.div [
                        prop.className "flex flex-col gap-2 items-center"
                        prop.children [
                            Html.span [ prop.className "loading loading-spinner loading-lg text-primary" ]
                            Html.p [
                                prop.className "text-sm text-gray-500"
                                prop.text "Once you've confirmed, click the button below"
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "card-actions mt-4"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-primary"
                                prop.text "I've Confirmed the TAN"
                                prop.onClick (fun _ -> dispatch ConfirmTan)
                            ]
                            Html.button [
                                prop.className "btn btn-ghost"
                                prop.text "Cancel"
                                prop.onClick (fun _ -> dispatch CancelSync)
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Transaction Row Component
// ============================================

let private transactionRow
    (tx: SyncTransaction)
    (categories: YnabCategory list)
    (isSelected: bool)
    (dispatch: Msg -> unit) =
    Html.tr [
        prop.className (
            match tx.Status with
            | NeedsAttention -> "bg-warning/10"
            | Pending -> "bg-error/10"
            | Skipped -> "opacity-50"
            | _ -> ""
        )
        prop.children [
            // Checkbox
            Html.td [
                Html.input [
                    prop.type'.checkbox
                    prop.className "checkbox"
                    prop.isChecked isSelected
                    prop.onChange (fun (_: bool) -> dispatch (ToggleTransactionSelection tx.Transaction.Id))
                ]
            ]
            // Date
            Html.td [
                prop.text (formatDate tx.Transaction.BookingDate)
            ]
            // Amount
            Html.td [
                prop.className (if tx.Transaction.Amount.Amount < 0m then "text-error font-medium" else "text-success font-medium")
                prop.text (formatAmount tx.Transaction.Amount)
            ]
            // Payee/Memo
            Html.td [
                Html.div [
                    Html.div [
                        prop.className "font-medium"
                        prop.text (tx.Transaction.Payee |> Option.defaultValue "-")
                    ]
                    Html.div [
                        prop.className "text-sm text-base-content/70 truncate max-w-xs"
                        prop.title tx.Transaction.Memo
                        prop.text tx.Transaction.Memo
                    ]
                ]
            ]
            // Status
            Html.td [ statusBadge tx.Status ]
            // Category dropdown
            Html.td [
                Html.select [
                    prop.className "select select-bordered select-sm w-full max-w-xs"
                    prop.value (
                        tx.CategoryId
                        |> Option.map (fun (YnabCategoryId id) -> id.ToString())
                        |> Option.defaultValue ""
                    )
                    prop.onChange (fun (value: string) ->
                        if value = "" then
                            dispatch (CategorizeTransaction (tx.Transaction.Id, None))
                        else
                            dispatch (CategorizeTransaction (tx.Transaction.Id, Some (YnabCategoryId (System.Guid.Parse value))))
                    )
                    prop.children [
                        Html.option [
                            prop.value ""
                            prop.text "-- Select Category --"
                        ]
                        for cat in categories do
                            Html.option [
                                prop.value (let (YnabCategoryId id) = cat.Id in id.ToString())
                                prop.text $"{cat.GroupName}: {cat.Name}"
                            ]
                    ]
                ]
            ]
            // Actions
            Html.td [
                Html.div [
                    prop.className "flex gap-1"
                    prop.children [
                        // External links
                        for link in tx.ExternalLinks do
                            Html.a [
                                prop.className "btn btn-ghost btn-xs"
                                prop.href link.Url
                                prop.target "_blank"
                                prop.text link.Label
                            ]
                        // Skip button
                        if tx.Status <> Skipped then
                            Html.button [
                                prop.className "btn btn-ghost btn-xs"
                                prop.text "Skip"
                                prop.onClick (fun _ -> dispatch (SkipTransaction tx.Transaction.Id))
                            ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Transaction List View
// ============================================

let private transactionListView (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-4"
        prop.children [
            // Bulk actions bar
            Html.div [
                prop.className "flex justify-between items-center bg-base-200 p-4 rounded-lg"
                prop.children [
                    Html.div [
                        prop.className "flex gap-2"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-sm"
                                prop.text "Select All"
                                prop.onClick (fun _ -> dispatch SelectAllTransactions)
                            ]
                            Html.button [
                                prop.className "btn btn-sm"
                                prop.text "Deselect All"
                                prop.onClick (fun _ -> dispatch DeselectAllTransactions)
                            ]
                            Html.span [
                                prop.className "text-sm self-center ml-2"
                                prop.text $"{model.SelectedTransactions.Count} selected"
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "flex gap-2"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-error btn-sm"
                                prop.text "Cancel Sync"
                                prop.onClick (fun _ -> dispatch CancelSync)
                            ]
                            Html.button [
                                prop.className "btn btn-primary"
                                prop.text "Import to YNAB"
                                prop.onClick (fun _ -> dispatch ImportToYnab)
                            ]
                        ]
                    ]
                ]
            ]

            // Summary
            match model.SyncTransactions with
            | Success transactions ->
                let categorized =
                    transactions
                    |> List.filter (fun tx ->
                        match tx.Status with
                        | AutoCategorized | ManualCategorized | NeedsAttention -> tx.CategoryId.IsSome
                        | _ -> false)
                    |> List.length
                let uncategorized = transactions |> List.filter (fun tx -> tx.Status = Pending) |> List.length
                let skipped = transactions |> List.filter (fun tx -> tx.Status = Skipped) |> List.length
                let needsAttention = transactions |> List.filter (fun tx -> tx.Status = NeedsAttention) |> List.length

                Html.div [
                    prop.className "flex gap-4 text-sm"
                    prop.children [
                        Html.span [ prop.className "badge badge-success"; prop.text $"Categorized: {categorized}" ]
                        if uncategorized > 0 then
                            Html.span [ prop.className "badge badge-error"; prop.text $"Uncategorized: {uncategorized}" ]
                        else Html.none
                        if needsAttention > 0 then
                            Html.span [ prop.className "badge badge-warning"; prop.text $"Needs Review: {needsAttention}" ]
                        else Html.none
                        if skipped > 0 then
                            Html.span [ prop.className "badge badge-ghost"; prop.text $"Skipped: {skipped}" ]
                        else Html.none
                    ]
                ]
            | _ -> Html.none

            // Transaction table
            match model.SyncTransactions with
            | NotAsked ->
                Html.div [
                    prop.className "text-center p-8 text-gray-500"
                    prop.text "No transactions loaded"
                ]
            | Loading ->
                Html.div [
                    prop.className "flex justify-center p-8"
                    prop.children [
                        Html.span [ prop.className "loading loading-spinner loading-lg" ]
                    ]
                ]
            | Success transactions when transactions.IsEmpty ->
                Html.div [
                    prop.className "text-center p-8 text-gray-500"
                    prop.text "No transactions found for the selected period"
                ]
            | Success transactions ->
                Html.div [
                    prop.className "overflow-x-auto"
                    prop.children [
                        Html.table [
                            prop.className "table table-zebra"
                            prop.children [
                                Html.thead [
                                    Html.tr [
                                        Html.th [ ]
                                        Html.th [ prop.text "Date" ]
                                        Html.th [ prop.text "Amount" ]
                                        Html.th [ prop.text "Payee / Memo" ]
                                        Html.th [ prop.text "Status" ]
                                        Html.th [ prop.text "Category" ]
                                        Html.th [ prop.text "Actions" ]
                                    ]
                                ]
                                Html.tbody [
                                    for tx in transactions do
                                        let isSelected = model.SelectedTransactions.Contains(tx.Transaction.Id)
                                        transactionRow tx model.Categories isSelected dispatch
                                ]
                            ]
                        ]
                    ]
                ]
            | Failure error ->
                Html.div [
                    prop.className "alert alert-error"
                    prop.text error
                ]
        ]
    ]

// ============================================
// Completed View
// ============================================

let private completedView (session: SyncSession) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "card bg-base-100 shadow-xl"
        prop.children [
            Html.div [
                prop.className "card-body items-center text-center"
                prop.children [
                    Html.div [
                        prop.className "text-6xl mb-4"
                        prop.text "✅"
                    ]
                    Html.h2 [
                        prop.className "card-title text-2xl"
                        prop.text "Sync Complete!"
                    ]
                    Html.div [
                        prop.className "stats stats-vertical lg:stats-horizontal shadow mt-4"
                        prop.children [
                            Html.div [
                                prop.className "stat"
                                prop.children [
                                    Html.div [ prop.className "stat-title"; prop.text "Total Transactions" ]
                                    Html.div [ prop.className "stat-value"; prop.text (string session.TransactionCount) ]
                                ]
                            ]
                            Html.div [
                                prop.className "stat"
                                prop.children [
                                    Html.div [ prop.className "stat-title"; prop.text "Imported to YNAB" ]
                                    Html.div [ prop.className "stat-value text-success"; prop.text (string session.ImportedCount) ]
                                ]
                            ]
                            Html.div [
                                prop.className "stat"
                                prop.children [
                                    Html.div [ prop.className "stat-title"; prop.text "Skipped" ]
                                    Html.div [ prop.className "stat-value text-gray-500"; prop.text (string session.SkippedCount) ]
                                ]
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "card-actions mt-6"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-primary"
                                prop.text "Start Another Sync"
                                prop.onClick (fun _ -> dispatch StartSync)
                            ]
                            Html.button [
                                prop.className "btn btn-ghost"
                                prop.text "Back to Dashboard"
                                prop.onClick (fun _ -> dispatch (NavigateTo Dashboard))
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

let private startSyncView (dispatch: Msg -> unit) =
    Html.div [
        prop.className "card bg-base-100 shadow-xl"
        prop.children [
            Html.div [
                prop.className "card-body items-center text-center"
                prop.children [
                    Html.div [
                        prop.className "text-6xl mb-4"
                        prop.text "🔄"
                    ]
                    Html.h2 [
                        prop.className "card-title text-2xl"
                        prop.text "Ready to Sync"
                    ]
                    Html.p [
                        prop.className "text-gray-600"
                        prop.text "Start a new sync to fetch transactions from Comdirect and categorize them for import to YNAB."
                    ]
                    Html.div [
                        prop.className "card-actions mt-4"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-primary btn-lg"
                                prop.text "Start Sync"
                                prop.onClick (fun _ -> dispatch StartSync)
                            ]
                        ]
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
                prop.text "Sync Transactions"
            ]

            // Show appropriate content based on session status
            match model.CurrentSession with
            | NotAsked ->
                startSyncView dispatch

            | Loading ->
                Html.div [
                    prop.className "flex flex-col items-center gap-4 p-8"
                    prop.children [
                        Html.span [ prop.className "loading loading-spinner loading-lg" ]
                        Html.span [ prop.text "Starting sync..." ]
                    ]
                ]

            | Success (Some session) ->
                match session.Status with
                | AwaitingBankAuth ->
                    Html.div [
                        prop.className "flex flex-col items-center gap-4 p-8"
                        prop.children [
                            Html.span [ prop.className "loading loading-spinner loading-lg" ]
                            Html.span [ prop.text "Initiating bank connection..." ]
                        ]
                    ]

                | AwaitingTan ->
                    tanWaitingView dispatch

                | FetchingTransactions ->
                    Html.div [
                        prop.className "flex flex-col items-center gap-4 p-8"
                        prop.children [
                            Html.span [ prop.className "loading loading-spinner loading-lg" ]
                            Html.span [ prop.text "Fetching transactions from Comdirect..." ]
                        ]
                    ]

                | ReviewingTransactions ->
                    transactionListView model dispatch

                | ImportingToYnab ->
                    Html.div [
                        prop.className "flex flex-col items-center gap-4 p-8"
                        prop.children [
                            Html.span [ prop.className "loading loading-spinner loading-lg" ]
                            Html.span [ prop.text "Importing to YNAB..." ]
                        ]
                    ]

                | Completed ->
                    completedView session dispatch

                | Failed error ->
                    Html.div [
                        prop.className "alert alert-error"
                        prop.children [
                            Html.span [ prop.text $"Sync failed: {error}" ]
                            Html.button [
                                prop.className "btn btn-sm"
                                prop.text "Try Again"
                                prop.onClick (fun _ -> dispatch StartSync)
                            ]
                        ]
                    ]

            | Success None ->
                startSyncView dispatch

            | Failure error ->
                Html.div [
                    prop.className "alert alert-error"
                    prop.children [
                        Html.span [ prop.text error ]
                        Html.button [
                            prop.className "btn btn-sm"
                            prop.text "Try Again"
                            prop.onClick (fun _ -> dispatch StartSync)
                        ]
                    ]
                ]
        ]
    ]
