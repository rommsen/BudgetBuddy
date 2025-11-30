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
    let (color, text, icon) =
        match status with
        | Pending -> ("bg-red-100 text-red-700 border-red-200", "Uncategorized", "!")
        | AutoCategorized -> ("bg-emerald-100 text-emerald-700 border-emerald-200", "Auto", "A")
        | ManualCategorized -> ("bg-blue-100 text-blue-700 border-blue-200", "Manual", "M")
        | NeedsAttention -> ("bg-amber-100 text-amber-700 border-amber-200", "Review", "?")
        | Skipped -> ("bg-gray-100 text-gray-500 border-gray-200", "Skip", "-")
        | Imported -> ("bg-emerald-100 text-emerald-700 border-emerald-200", "Done", "D")
    Html.span [
        prop.className $"inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium border {color}"
        prop.children [
            Html.span [
                prop.className "w-4 h-4 rounded-full bg-current/20 flex items-center justify-center text-[10px] font-bold"
                prop.text icon
            ]
            Html.span [ prop.text text ]
        ]
    ]

// ============================================
// TAN Waiting View
// ============================================

let private tanWaitingView (dispatch: Msg -> unit) =
    Html.div [
        prop.className "card bg-base-100 shadow-xl max-w-lg mx-auto animate-scale-in"
        prop.children [
            Html.div [
                prop.className "card-body p-6 md:p-8 items-center text-center"
                prop.children [
                    // Animated phone icon
                    Html.div [
                        prop.className "relative"
                        prop.children [
                            Html.div [
                                prop.className "w-24 h-24 rounded-2xl bg-gradient-to-br from-primary to-secondary flex items-center justify-center text-white shadow-lg"
                                prop.children [
                                    Html.span [
                                        prop.className "text-5xl"
                                        prop.text "📱"
                                    ]
                                ]
                            ]
                            // Pulsing notification dot
                            Html.div [
                                prop.className "absolute -top-1 -right-1 w-6 h-6 bg-warning rounded-full flex items-center justify-center animate-bounce"
                                prop.children [
                                    Html.span [
                                        prop.className "text-warning-content text-xs font-bold"
                                        prop.text "1"
                                    ]
                                ]
                            ]
                        ]
                    ]

                    Html.h2 [
                        prop.className "text-xl md:text-2xl font-bold mt-6"
                        prop.text "TAN Confirmation Required"
                    ]
                    Html.p [
                        prop.className "text-base-content/60 mt-2 max-w-sm"
                        prop.text "Please open your banking app and confirm the push TAN notification to authorize the connection."
                    ]

                    // Steps indicator
                    Html.div [
                        prop.className "flex items-center gap-3 mt-6 text-sm"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-2 text-success"
                                prop.children [
                                    Html.div [ prop.className "w-6 h-6 rounded-full bg-success text-success-content flex items-center justify-center text-xs font-bold"; prop.text "1" ]
                                    Html.span [ prop.text "Connected" ]
                                ]
                            ]
                            Html.div [ prop.className "w-8 h-0.5 bg-base-300" ]
                            Html.div [
                                prop.className "flex items-center gap-2 text-primary"
                                prop.children [
                                    Html.div [ prop.className "w-6 h-6 rounded-full bg-primary text-primary-content flex items-center justify-center loading loading-spinner loading-xs" ]
                                    Html.span [ prop.className "font-medium"; prop.text "TAN" ]
                                ]
                            ]
                            Html.div [ prop.className "w-8 h-0.5 bg-base-300" ]
                            Html.div [
                                prop.className "flex items-center gap-2 text-base-content/40"
                                prop.children [
                                    Html.div [ prop.className "w-6 h-6 rounded-full bg-base-200 flex items-center justify-center text-xs font-bold"; prop.text "3" ]
                                    Html.span [ prop.text "Fetch" ]
                                ]
                            ]
                        ]
                    ]

                    Html.div [
                        prop.className "flex flex-col sm:flex-row gap-3 mt-8 w-full sm:w-auto"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-primary btn-lg gap-2 flex-1 sm:flex-none"
                                prop.onClick (fun _ -> dispatch ConfirmTan)
                                prop.children [
                                    Html.span [
                                        prop.className "text-xl"
                                        prop.text "✓"
                                    ]
                                    Html.span [ prop.text "I've Confirmed" ]
                                ]
                            ]
                            Html.button [
                                prop.className "btn btn-ghost btn-lg"
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
// Transaction Card Component (Mobile-first)
// ============================================

let private transactionCard
    (tx: SyncTransaction)
    (categories: YnabCategory list)
    (isSelected: bool)
    (dispatch: Msg -> unit) =
    let bgClass =
        match tx.Status with
        | NeedsAttention -> "border-l-4 border-l-warning bg-warning/5"
        | Pending -> "border-l-4 border-l-error bg-error/5"
        | Skipped -> "opacity-50"
        | _ -> "border-l-4 border-l-transparent"

    Html.div [
        prop.className $"card bg-base-100 shadow-sm hover:shadow-md transition-all {bgClass}"
        prop.children [
            Html.div [
                prop.className "card-body p-4"
                prop.children [
                    // Top row: Checkbox, Date, Amount
                    Html.div [
                        prop.className "flex items-start justify-between gap-3"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-3"
                                prop.children [
                                    Html.input [
                                        prop.type'.checkbox
                                        prop.className "checkbox checkbox-primary"
                                        prop.isChecked isSelected
                                        prop.onChange (fun (_: bool) -> dispatch (ToggleTransactionSelection tx.Transaction.Id))
                                    ]
                                    Html.div [
                                        prop.children [
                                            Html.p [
                                                prop.className "font-medium text-base-content"
                                                prop.text (tx.Transaction.Payee |> Option.defaultValue "Unknown")
                                            ]
                                            Html.p [
                                                prop.className "text-sm text-base-content/60"
                                                prop.text (formatDate tx.Transaction.BookingDate)
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                            Html.div [
                                prop.className "text-right"
                                prop.children [
                                    Html.p [
                                        prop.className (
                                            "font-mono font-semibold text-lg " +
                                            if tx.Transaction.Amount.Amount < 0m then "text-red-500" else "text-emerald-500"
                                        )
                                        prop.text (formatAmount tx.Transaction.Amount)
                                    ]
                                    statusBadge tx.Status
                                ]
                            ]
                        ]
                    ]

                    // Memo
                    if not (System.String.IsNullOrWhiteSpace tx.Transaction.Memo) then
                        Html.p [
                            prop.className "text-sm text-base-content/60 mt-2 line-clamp-2"
                            prop.title tx.Transaction.Memo
                            prop.text tx.Transaction.Memo
                        ]

                    // Category & Actions
                    Html.div [
                        prop.className "flex flex-col sm:flex-row sm:items-center gap-2 mt-3"
                        prop.children [
                            Html.select [
                                prop.className "select select-bordered select-sm flex-1"
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
                                        prop.text "Select category..."
                                    ]
                                    for cat in categories do
                                        Html.option [
                                            prop.value (let (YnabCategoryId id) = cat.Id in id.ToString())
                                            prop.text $"{cat.GroupName}: {cat.Name}"
                                        ]
                                ]
                            ]
                            Html.div [
                                prop.className "flex gap-2"
                                prop.children [
                                    for link in tx.ExternalLinks do
                                        Html.a [
                                            prop.className "btn btn-ghost btn-sm"
                                            prop.href link.Url
                                            prop.target "_blank"
                                            prop.children [
                                                Html.span [
                                                    prop.className "text-xl"
                                                    prop.text "↗️"
                                                ]
                                            ]
                                        ]
                                    if tx.Status <> Skipped then
                                        Html.button [
                                            prop.className "btn btn-ghost btn-sm text-base-content/60"
                                            prop.onClick (fun _ -> dispatch (SkipTransaction tx.Transaction.Id))
                                            prop.children [
                                                Html.span [
                                                    prop.className "text-xl"
                                                    prop.text "⊘"
                                                ]
                                                Html.span [ prop.className "hidden sm:inline"; prop.text "Skip" ]
                                            ]
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
// Transaction List View
// ============================================

let private transactionListView (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-4"
        prop.children [
            // Stats summary card
            match model.SyncTransactions with
            | Success transactions ->
                let categorized = transactions |> List.filter (fun tx -> match tx.Status with | AutoCategorized | ManualCategorized | NeedsAttention -> tx.CategoryId.IsSome | _ -> false) |> List.length
                let uncategorized = transactions |> List.filter (fun tx -> tx.Status = Pending) |> List.length
                let skipped = transactions |> List.filter (fun tx -> tx.Status = Skipped) |> List.length
                let needsAttention = transactions |> List.filter (fun tx -> tx.Status = NeedsAttention) |> List.length
                let total = transactions.Length

                Html.div [
                    prop.className "card bg-base-100 shadow-lg"
                    prop.children [
                        Html.div [
                            prop.className "card-body p-4"
                            prop.children [
                                Html.div [
                                    prop.className "grid grid-cols-2 sm:grid-cols-4 gap-4"
                                    prop.children [
                                        Html.div [
                                            prop.className "text-center"
                                            prop.children [
                                                Html.p [ prop.className "text-2xl font-bold font-mono"; prop.text (string total) ]
                                                Html.p [ prop.className "text-xs text-base-content/60 uppercase tracking-wide"; prop.text "Total" ]
                                            ]
                                        ]
                                        Html.div [
                                            prop.className "text-center"
                                            prop.children [
                                                Html.p [ prop.className "text-2xl font-bold font-mono text-emerald-500"; prop.text (string categorized) ]
                                                Html.p [ prop.className "text-xs text-base-content/60 uppercase tracking-wide"; prop.text "Ready" ]
                                            ]
                                        ]
                                        Html.div [
                                            prop.className "text-center"
                                            prop.children [
                                                let pendingColor = if uncategorized > 0 then "text-red-500" else "text-base-content/40"
                                                Html.p [ prop.className $"text-2xl font-bold font-mono {pendingColor}"; prop.text (string uncategorized) ]
                                                Html.p [ prop.className "text-xs text-base-content/60 uppercase tracking-wide"; prop.text "Pending" ]
                                            ]
                                        ]
                                        Html.div [
                                            prop.className "text-center"
                                            prop.children [
                                                Html.p [ prop.className "text-2xl font-bold font-mono text-base-content/40"; prop.text (string skipped) ]
                                                Html.p [ prop.className "text-xs text-base-content/60 uppercase tracking-wide"; prop.text "Skipped" ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            | _ -> Html.none

            // Bulk actions bar (sticky on mobile)
            Html.div [
                prop.className "sticky top-16 z-40 bg-base-100/90 backdrop-blur-xl rounded-xl shadow-lg p-3 md:p-4 border border-base-200"
                prop.children [
                    Html.div [
                        prop.className "flex flex-col sm:flex-row sm:justify-between sm:items-center gap-3"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-2 flex-wrap"
                                prop.children [
                                    Html.button [
                                        prop.className "btn btn-sm btn-ghost"
                                        prop.onClick (fun _ -> dispatch SelectAllTransactions)
                                        prop.children [
                                            Html.span [
                                                prop.className "text-xl"
                                                prop.text "✓"
                                            ]
                                            Html.span [ prop.text "All" ]
                                        ]
                                    ]
                                    Html.button [
                                        prop.className "btn btn-sm btn-ghost"
                                        prop.onClick (fun _ -> dispatch DeselectAllTransactions)
                                        prop.children [
                                            Html.span [
                                                prop.className "text-xl"
                                                prop.text "❌"
                                            ]
                                            Html.span [ prop.text "None" ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "badge badge-primary badge-lg"
                                        prop.text $"{model.SelectedTransactions.Count} selected"
                                    ]
                                ]
                            ]
                            Html.div [
                                prop.className "flex gap-2"
                                prop.children [
                                    Html.button [
                                        prop.className "btn btn-ghost btn-sm text-error"
                                        prop.onClick (fun _ -> dispatch CancelSync)
                                        prop.text "Cancel"
                                    ]
                                    Html.button [
                                        prop.className "btn btn-primary gap-2"
                                        prop.onClick (fun _ -> dispatch ImportToYnab)
                                        prop.children [
                                            Html.span [
                                                prop.className "text-xl"
                                                prop.text "⬆️"
                                            ]
                                            Html.span [ prop.text "Import to YNAB" ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]

            // Transaction cards
            match model.SyncTransactions with
            | NotAsked ->
                Html.div [
                    prop.className "flex flex-col items-center justify-center py-16 text-base-content/50"
                    prop.children [
                        Html.div [ prop.className "loading loading-spinner loading-lg" ]
                        Html.p [ prop.className "mt-4"; prop.text "No transactions loaded" ]
                    ]
                ]
            | Loading ->
                Html.div [
                    prop.className "flex flex-col items-center justify-center py-16"
                    prop.children [
                        Html.div [ prop.className "loading loading-spinner loading-lg text-primary" ]
                        Html.p [ prop.className "mt-4 text-base-content/60"; prop.text "Loading transactions..." ]
                    ]
                ]
            | Success transactions when transactions.IsEmpty ->
                Html.div [
                    prop.className "flex flex-col items-center justify-center py-16 text-center"
                    prop.children [
                        Html.div [
                            prop.className "w-20 h-20 rounded-full bg-base-200 flex items-center justify-center mb-4"
                            prop.children [
                                Html.span [
                                    prop.className "text-5xl"
                                    prop.text "ℹ️"
                                ]
                            ]
                        ]
                        Html.p [ prop.className "font-medium text-lg"; prop.text "No transactions found" ]
                        Html.p [ prop.className "text-sm text-base-content/50 mt-1"; prop.text "Try adjusting the date range in settings." ]
                    ]
                ]
            | Success transactions ->
                Html.div [
                    prop.className "space-y-3"
                    prop.children [
                        for tx in transactions do
                            let isSelected = model.SelectedTransactions.Contains(tx.Transaction.Id)
                            transactionCard tx model.Categories isSelected dispatch
                    ]
                ]
            | Failure error ->
                Html.div [
                    prop.className "alert alert-error"
                    prop.children [
                        Html.span [
                            prop.className "text-xl"
                            prop.text "⚠️"
                        ]
                        Html.span [ prop.text error ]
                    ]
                ]
        ]
    ]

// ============================================
// Completed View
// ============================================

let private completedView (session: SyncSession) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "max-w-lg mx-auto animate-scale-in"
        prop.children [
            Html.div [
                prop.className "card bg-base-100 shadow-xl overflow-hidden"
                prop.children [
                    // Success header with gradient
                    Html.div [
                        prop.className "bg-gradient-to-br from-emerald-500 to-teal-600 p-8 text-white text-center"
                        prop.children [
                            Html.div [
                                prop.className "w-20 h-20 rounded-full bg-white/20 flex items-center justify-center mx-auto mb-4"
                                prop.children [
                                    Html.span [
                                        prop.className "text-5xl"
                                        prop.text "✓"
                                    ]
                                ]
                            ]
                            Html.h2 [
                                prop.className "text-2xl md:text-3xl font-bold"
                                prop.text "Sync Complete!"
                            ]
                            Html.p [
                                prop.className "text-white/80 mt-2"
                                prop.text "Your transactions have been imported to YNAB."
                            ]
                        ]
                    ]

                    Html.div [
                        prop.className "card-body p-6"
                        prop.children [
                            // Stats grid
                            Html.div [
                                prop.className "grid grid-cols-3 gap-4 -mt-12 mb-6"
                                prop.children [
                                    Html.div [
                                        prop.className "card bg-base-100 shadow-lg"
                                        prop.children [
                                            Html.div [
                                                prop.className "card-body p-4 text-center"
                                                prop.children [
                                                    Html.p [ prop.className "text-2xl font-bold font-mono"; prop.text (string session.TransactionCount) ]
                                                    Html.p [ prop.className "text-xs text-base-content/60 uppercase"; prop.text "Total" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "card bg-base-100 shadow-lg"
                                        prop.children [
                                            Html.div [
                                                prop.className "card-body p-4 text-center"
                                                prop.children [
                                                    Html.p [ prop.className "text-2xl font-bold font-mono text-emerald-500"; prop.text (string session.ImportedCount) ]
                                                    Html.p [ prop.className "text-xs text-base-content/60 uppercase"; prop.text "Imported" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "card bg-base-100 shadow-lg"
                                        prop.children [
                                            Html.div [
                                                prop.className "card-body p-4 text-center"
                                                prop.children [
                                                    Html.p [ prop.className "text-2xl font-bold font-mono text-base-content/40"; prop.text (string session.SkippedCount) ]
                                                    Html.p [ prop.className "text-xs text-base-content/60 uppercase"; prop.text "Skipped" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]

                            // Actions
                            Html.div [
                                prop.className "flex flex-col sm:flex-row gap-3"
                                prop.children [
                                    Html.button [
                                        prop.className "btn btn-primary flex-1 gap-2"
                                        prop.onClick (fun _ -> dispatch StartSync)
                                        prop.children [
                                            Html.span [
                                                prop.className "text-xl"
                                                prop.text "🔄"
                                            ]
                                            Html.span [ prop.text "Sync Again" ]
                                        ]
                                    ]
                                    Html.button [
                                        prop.className "btn btn-ghost flex-1"
                                        prop.onClick (fun _ -> dispatch (NavigateTo Dashboard))
                                        prop.text "Back to Dashboard"
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
// Start Sync View
// ============================================

let private startSyncView (dispatch: Msg -> unit) =
    Html.div [
        prop.className "max-w-lg mx-auto animate-scale-in"
        prop.children [
            Html.div [
                prop.className "card bg-base-100 shadow-xl overflow-hidden"
                prop.children [
                    // Header with animated gradient
                    Html.div [
                        prop.className "gradient-bg p-8 text-white text-center"
                        prop.children [
                            Html.div [
                                prop.className "w-20 h-20 rounded-full bg-white/20 flex items-center justify-center mx-auto mb-4"
                                prop.children [
                                    Html.span [
                                        prop.className "text-5xl"
                                        prop.text "🔄"
                                    ]
                                ]
                            ]
                            Html.h2 [
                                prop.className "text-2xl md:text-3xl font-bold"
                                prop.text "Ready to Sync"
                            ]
                            Html.p [
                                prop.className "text-white/80 mt-2"
                                prop.text "Connect to your bank and import transactions."
                            ]
                        ]
                    ]

                    Html.div [
                        prop.className "card-body p-6"
                        prop.children [
                            // Features list
                            Html.div [
                                prop.className "space-y-4 mb-6"
                                prop.children [
                                    Html.div [
                                        prop.className "flex items-center gap-3"
                                        prop.children [
                                            Html.div [
                                                prop.className "w-10 h-10 rounded-full bg-primary/10 text-primary flex items-center justify-center flex-shrink-0"
                                                prop.children [
                                                    Html.span [
                                                        prop.className "text-xl"
                                                        prop.text "🛡️"
                                                    ]
                                                ]
                                            ]
                                            Html.div [
                                                prop.children [
                                                    Html.p [ prop.className "font-medium"; prop.text "Secure Connection" ]
                                                    Html.p [ prop.className "text-sm text-base-content/60"; prop.text "Bank-level encryption for your data" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "flex items-center gap-3"
                                        prop.children [
                                            Html.div [
                                                prop.className "w-10 h-10 rounded-full bg-secondary/10 text-secondary flex items-center justify-center flex-shrink-0"
                                                prop.children [
                                                    Html.span [
                                                        prop.className "text-xl"
                                                        prop.text "⚡"
                                                    ]
                                                ]
                                            ]
                                            Html.div [
                                                prop.children [
                                                    Html.p [ prop.className "font-medium"; prop.text "Auto-Categorization" ]
                                                    Html.p [ prop.className "text-sm text-base-content/60"; prop.text "Rules automatically categorize transactions" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "flex items-center gap-3"
                                        prop.children [
                                            Html.div [
                                                prop.className "w-10 h-10 rounded-full bg-accent/10 text-accent flex items-center justify-center flex-shrink-0"
                                                prop.children [
                                                    Html.span [
                                                        prop.className "text-xl"
                                                        prop.text "⬆️"
                                                    ]
                                                ]
                                            ]
                                            Html.div [
                                                prop.children [
                                                    Html.p [ prop.className "font-medium"; prop.text "YNAB Import" ]
                                                    Html.p [ prop.className "text-sm text-base-content/60"; prop.text "Direct import to your YNAB budget" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]

                            Html.button [
                                prop.className "btn btn-primary btn-lg w-full gap-2"
                                prop.onClick (fun _ -> dispatch StartSync)
                                prop.children [
                                    Html.span [
                                        prop.className "text-xl"
                                        prop.text "🔄"
                                    ]
                                    Html.span [ prop.text "Start Sync" ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Loading State View
// ============================================

let private loadingView (message: string) =
    Html.div [
        prop.className "max-w-md mx-auto animate-fade-in"
        prop.children [
            Html.div [
                prop.className "card bg-base-100 shadow-xl"
                prop.children [
                    Html.div [
                        prop.className "card-body items-center text-center py-12"
                        prop.children [
                            Html.div [
                                prop.className "relative"
                                prop.children [
                                    Html.div [
                                        prop.className "w-16 h-16 rounded-full border-4 border-base-200 border-t-primary animate-spin"
                                    ]
                                ]
                            ]
                            Html.p [
                                prop.className "mt-6 font-medium text-base-content/80"
                                prop.text message
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Error View
// ============================================

let private errorView (error: string) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "max-w-md mx-auto animate-scale-in"
        prop.children [
            Html.div [
                prop.className "card bg-base-100 shadow-xl overflow-hidden"
                prop.children [
                    Html.div [
                        prop.className "bg-gradient-to-br from-red-500 to-rose-600 p-6 text-white text-center"
                        prop.children [
                            Html.div [
                                prop.className "w-16 h-16 rounded-full bg-white/20 flex items-center justify-center mx-auto mb-3"
                                prop.children [
                                    Html.span [
                                        prop.className "text-4xl"
                                        prop.text "⚠️"
                                    ]
                                ]
                            ]
                            Html.h2 [
                                prop.className "text-xl font-bold"
                                prop.text "Sync Failed"
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "card-body p-6 text-center"
                        prop.children [
                            Html.p [
                                prop.className "text-base-content/70 mb-4"
                                prop.text error
                            ]
                            Html.button [
                                prop.className "btn btn-primary gap-2"
                                prop.onClick (fun _ -> dispatch StartSync)
                                prop.children [
                                    Html.span [
                                        prop.className "text-xl"
                                        prop.text "🔄"
                                    ]
                                    Html.span [ prop.text "Try Again" ]
                                ]
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
            Html.div [
                prop.className "animate-fade-in"
                prop.children [
                    Html.h1 [
                        prop.className "text-2xl md:text-4xl font-bold"
                        prop.text "Sync Transactions"
                    ]
                    Html.p [
                        prop.className "text-base-content/60 mt-1"
                        prop.text "Fetch and categorize your bank transactions."
                    ]
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
                    tanWaitingView dispatch

                | FetchingTransactions ->
                    loadingView "Fetching transactions..."

                | ReviewingTransactions ->
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
        ]
    ]
