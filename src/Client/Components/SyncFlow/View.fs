module Components.SyncFlow.View

open Feliz
open Components.SyncFlow.Types
open Types
open Shared.Domain
open Client.DesignSystem

// ============================================
// Helper Functions
// ============================================

let private formatDate (date: System.DateTime) =
    date.ToString("dd.MM.yyyy")

// ============================================
// Status Badge Component (using Design System)
// ============================================

let private statusBadge (status: TransactionStatus) =
    match status with
    | Pending -> Badge.uncategorized
    | AutoCategorized -> Badge.autoCategorized
    | ManualCategorized -> Badge.manual
    | NeedsAttention -> Badge.pendingReview
    | Skipped -> Badge.skipped
    | Imported -> Badge.imported

// ============================================
// TAN Waiting View
// ============================================

let private tanWaitingView (dispatch: Msg -> unit) =
    Html.div [
        prop.className "max-w-lg mx-auto animate-fade-in"
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

                        // Steps indicator with neon styling
                        Html.div [
                            prop.className "flex items-center gap-3 mt-6 text-sm"
                            prop.children [
                                Html.div [
                                    prop.className "flex items-center gap-2 text-neon-green"
                                    prop.children [
                                        Html.div [
                                            prop.className "w-6 h-6 rounded-full bg-neon-green text-[#0a0a0f] flex items-center justify-center text-xs font-bold"
                                            prop.children [ Icons.check Icons.XS Icons.Primary ]
                                        ]
                                        Html.span [ prop.text "Connected" ]
                                    ]
                                ]
                                Html.div [ prop.className "w-8 h-0.5 bg-neon-teal/30" ]
                                Html.div [
                                    prop.className "flex items-center gap-2 text-neon-teal"
                                    prop.children [
                                        Html.div [
                                            prop.className "w-6 h-6 rounded-full bg-neon-teal/20 border border-neon-teal flex items-center justify-center"
                                            prop.children [ Loading.spinner Loading.XS Loading.Teal ]
                                        ]
                                        Html.span [ prop.className "font-medium"; prop.text "TAN" ]
                                    ]
                                ]
                                Html.div [ prop.className "w-8 h-0.5 bg-base-content/10" ]
                                Html.div [
                                    prop.className "flex items-center gap-2 text-base-content/40"
                                    prop.children [
                                        Html.div [ prop.className "w-6 h-6 rounded-full bg-base-200 flex items-center justify-center text-xs font-bold"; prop.text "3" ]
                                        Html.span [ prop.text "Fetch" ]
                                    ]
                                ]
                            ]
                        ]

                        // Action buttons
                        Html.div [
                            prop.className "flex flex-col sm:flex-row gap-3 mt-8 w-full sm:w-auto"
                            prop.children [
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
// Transaction Card Component (Mobile-first)
// ============================================

/// Duplicate status badge component
let private duplicateStatusBadge (status: DuplicateStatus) =
    match status with
    | NotDuplicate -> Html.none
    | PossibleDuplicate reason ->
        Html.div [
            prop.className "inline-flex items-center gap-1.5 px-2 py-1 rounded-md bg-neon-orange/20 text-neon-orange text-xs border border-neon-orange/30"
            prop.title reason
            prop.children [
                Icons.warning Icons.XS Icons.NeonOrange
                Html.span [ prop.text "Possible Duplicate" ]
            ]
        ]
    | ConfirmedDuplicate _ ->
        Html.div [
            prop.className "inline-flex items-center gap-1.5 px-2 py-1 rounded-md bg-neon-red/20 text-neon-red text-xs border border-neon-red/30"
            prop.children [
                Icons.xCircle Icons.XS Icons.Error
                Html.span [ prop.text "Duplicate" ]
            ]
        ]

let private transactionCard
    (tx: SyncTransaction)
    (categories: YnabCategory list)
    (dispatch: Msg -> unit) =
    // Check for duplicate status first
    let isDuplicate =
        match tx.DuplicateStatus with
        | ConfirmedDuplicate _ -> true
        | _ -> false

    let isPossibleDuplicate =
        match tx.DuplicateStatus with
        | PossibleDuplicate _ -> true
        | _ -> false

    let borderClass =
        if isDuplicate then
            "border-l-4 border-l-neon-red bg-neon-red/5"
        elif isPossibleDuplicate then
            "border-l-4 border-l-neon-orange bg-neon-orange/5"
        else
            match tx.Status with
            | NeedsAttention -> "border-l-4 border-l-neon-pink bg-neon-pink/5"
            | Pending -> "border-l-4 border-l-neon-orange bg-neon-orange/5"
            | Skipped -> "opacity-50 border-l-4 border-l-transparent"
            | AutoCategorized | ManualCategorized -> "border-l-4 border-l-neon-green/50"
            | Imported -> "border-l-4 border-l-neon-teal/50"

    Html.div [
        prop.className $"bg-base-100 border border-white/5 rounded-xl p-4 hover:border-white/10 transition-all {borderClass}"
        prop.children [
            // Duplicate warning banner (if applicable)
            match tx.DuplicateStatus with
            | NotDuplicate -> ()
            | PossibleDuplicate reason ->
                Html.div [
                    prop.className "flex items-center gap-2 mb-3 p-2 rounded-lg bg-neon-orange/10 border border-neon-orange/30"
                    prop.children [
                        Icons.warning Icons.SM Icons.NeonOrange
                        Html.span [
                            prop.className "text-sm text-neon-orange flex-1"
                            prop.text reason
                        ]
                        Html.span [
                            prop.className "text-xs text-neon-orange/70"
                            prop.text "You can still import this transaction if it's not a duplicate."
                        ]
                    ]
                ]
            | ConfirmedDuplicate reference ->
                Html.div [
                    prop.className "flex items-center gap-2 mb-3 p-2 rounded-lg bg-neon-red/10 border border-neon-red/30"
                    prop.children [
                        Icons.xCircle Icons.SM Icons.Error
                        Html.span [
                            prop.className "text-sm text-neon-red flex-1"
                            prop.text $"This transaction was already imported (Ref: {reference})"
                        ]
                        Html.span [
                            prop.className "text-xs text-neon-red/70"
                            prop.text "Consider skipping this transaction."
                        ]
                    ]
                ]

            // Top row: Payee/Date, Amount/Status
            Html.div [
                prop.className "flex items-start justify-between gap-3"
                prop.children [
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
                    Html.div [
                        prop.className "text-right flex flex-col items-end gap-1"
                        prop.children [
                            Money.view {
                                Money.defaultProps with
                                    Amount = tx.Transaction.Amount.Amount
                                    Currency = tx.Transaction.Amount.Currency
                                    Size = Money.Medium
                                    Glow = Money.NoGlow
                            }
                            statusBadge tx.Status
                            duplicateStatusBadge tx.DuplicateStatus
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
                    Input.selectWithPlaceholder
                        (tx.CategoryId
                         |> Option.map (fun (YnabCategoryId id) -> id.ToString())
                         |> Option.defaultValue "")
                        (fun (value: string) ->
                            if value = "" then
                                dispatch (CategorizeTransaction (tx.Transaction.Id, None))
                            else
                                dispatch (CategorizeTransaction (tx.Transaction.Id, Some (YnabCategoryId (System.Guid.Parse value)))))
                        "Select category..."
                        [ for cat in categories ->
                            let (YnabCategoryId id) = cat.Id
                            (id.ToString(), $"{cat.GroupName}: {cat.Name}") ]
                    Html.div [
                        prop.className "flex gap-2"
                        prop.children [
                            for link in tx.ExternalLinks do
                                Html.a [
                                    prop.className "btn btn-ghost btn-sm text-neon-teal hover:text-neon-teal hover:bg-neon-teal/10"
                                    prop.href link.Url
                                    prop.target "_blank"
                                    prop.children [ Icons.externalLink Icons.SM Icons.NeonTeal ]
                                ]
                            if tx.Status = Skipped then
                                Html.button [
                                    prop.className "btn btn-ghost btn-sm text-neon-green hover:text-neon-green hover:bg-neon-green/10"
                                    prop.onClick (fun _ -> dispatch (UnskipTransaction tx.Transaction.Id))
                                    prop.children [
                                        Icons.undo Icons.SM Icons.NeonGreen
                                        Html.span [ prop.className "hidden sm:inline ml-1"; prop.text "Unskip" ]
                                    ]
                                ]
                            else
                                Html.button [
                                    prop.className "btn btn-ghost btn-sm text-base-content/60 hover:text-neon-pink"
                                    prop.onClick (fun _ -> dispatch (SkipTransaction tx.Transaction.Id))
                                    prop.children [
                                        Icons.forward Icons.SM Icons.Default
                                        Html.span [ prop.className "hidden sm:inline ml-1"; prop.text "Skip" ]
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
            // Stats summary card using design system
            match model.SyncTransactions with
            | Success transactions ->
                // Calculate all counts in a single pass for better performance
                let (categorized, uncategorized, skipped, duplicates) =
                    transactions |> List.fold (fun (cat, uncat, skip, dup) tx ->
                        let cat' =
                            match tx.Status with
                            | AutoCategorized | ManualCategorized | NeedsAttention when tx.CategoryId.IsSome -> cat + 1
                            | _ -> cat
                        let uncat' = if tx.Status = Pending then uncat + 1 else uncat
                        let skip' = if tx.Status = Skipped then skip + 1 else skip
                        let dup' =
                            match tx.DuplicateStatus with
                            | ConfirmedDuplicate _ | PossibleDuplicate _ -> dup + 1
                            | _ -> dup
                        (cat', uncat', skip', dup')
                    ) (0, 0, 0, 0)
                let total = transactions.Length

                Html.div [
                    prop.className "space-y-3"
                    prop.children [
                        Stats.gridFourCol [
                            Stats.view {
                                Stats.defaultProps with
                                    Label = "Total"
                                    Value = string total
                                    Accent = Stats.Gradient
                                    Size = Stats.Compact
                            }
                            Stats.view {
                                Stats.defaultProps with
                                    Label = "Ready"
                                    Value = string categorized
                                    Accent = Stats.Green
                                    Size = Stats.Compact
                            }
                            Stats.view {
                                Stats.defaultProps with
                                    Label = "Pending"
                                    Value = string uncategorized
                                    Accent = if uncategorized > 0 then Stats.Orange else Stats.Gradient
                                    Size = Stats.Compact
                            }
                            Stats.view {
                                Stats.defaultProps with
                                    Label = "Skipped"
                                    Value = string skipped
                                    Size = Stats.Compact
                            }
                        ]
                        // Duplicate warning banner
                        if duplicates > 0 then
                            Html.div [
                                prop.className "flex items-center gap-3 p-3 rounded-xl bg-neon-orange/10 border border-neon-orange/30"
                                prop.children [
                                    Icons.warning Icons.MD Icons.NeonOrange
                                    Html.div [
                                        prop.className "flex-1"
                                        prop.children [
                                            Html.p [
                                                prop.className "text-sm font-medium text-neon-orange"
                                                prop.text $"{duplicates} potential duplicate(s) detected"
                                            ]
                                            Html.p [
                                                prop.className "text-xs text-base-content/60"
                                                prop.text "Review transactions marked in orange or red before importing."
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                    ]
                ]
            | _ -> Html.none

            // Actions bar (sticky with glassmorphism)
            Html.div [
                prop.className "sticky top-16 z-40 bg-base-100/80 backdrop-blur-xl rounded-xl shadow-lg p-3 md:p-4 border border-white/10"
                prop.children [
                    Html.div [
                        prop.className "flex justify-end gap-2"
                        prop.children [
                            Button.danger "Cancel" (fun () -> dispatch CancelSync)
                            // Enable import if any non-skipped transaction has a category
                            let canImport =
                                match model.SyncTransactions with
                                | Success transactions ->
                                    transactions
                                    |> List.exists (fun tx ->
                                        tx.Status <> Skipped &&
                                        tx.CategoryId.IsSome)
                                | _ -> false
                            Button.view {
                                Button.defaultProps with
                                    Text = "Import to YNAB"
                                    Variant = Button.Primary
                                    Icon = Some (Icons.upload Icons.SM Icons.Primary)
                                    OnClick = fun () -> dispatch ImportToYnab
                                    IsDisabled = not canImport
                            }
                            // Show force import button if there are duplicates
                            // Check both: explicit duplicate list OR non-imported categorized transactions
                            let duplicateCount =
                                if not model.DuplicateTransactionIds.IsEmpty then
                                    model.DuplicateTransactionIds.Length
                                else
                                    match model.SyncTransactions with
                                    | Success transactions ->
                                        transactions
                                        |> List.filter (fun tx ->
                                            tx.Status <> Imported &&
                                            tx.Status <> Skipped &&
                                            (tx.CategoryId.IsSome || tx.Splits.IsSome))
                                        |> List.length
                                    | _ -> 0
                            if duplicateCount > 0 then
                                Button.view {
                                    Button.defaultProps with
                                        Text = $"Re-import {duplicateCount} Duplicate(s)"
                                        Variant = Button.Secondary
                                        Icon = Some (Icons.sync Icons.SM Icons.NeonTeal)
                                        OnClick = fun () -> dispatch ForceImportDuplicates
                                }
                        ]
                    ]
                ]
            ]

            // Transaction cards
            match model.SyncTransactions with
            | NotAsked ->
                Card.emptyState
                    (Icons.creditCard Icons.XL Icons.Default)
                    "No transactions loaded"
                    "Start a sync to fetch transactions from your bank."
                    None
            | Loading ->
                Loading.centered "Loading transactions..."
            | Success transactions when transactions.IsEmpty ->
                Card.emptyState
                    (Icons.info Icons.XL Icons.Default)
                    "No transactions found"
                    "Try adjusting the date range in settings."
                    None
            | Success transactions ->
                Html.div [
                    prop.className "space-y-3"
                    prop.children [
                        for tx in transactions do
                            transactionCard tx model.Categories dispatch
                    ]
                ]
            | Failure error ->
                Html.div [
                    prop.className "rounded-xl bg-neon-red/10 border border-neon-red/30 p-4 flex items-center gap-3"
                    prop.children [
                        Icons.warning Icons.MD Icons.Error
                        Html.span [ prop.className "text-neon-red"; prop.text error ]
                    ]
                ]
        ]
    ]

// ============================================
// Completed View
// ============================================

let private completedView (session: SyncSession) (dispatch: Msg -> unit) (onNavigateToDashboard: unit -> unit) =
    Html.div [
        prop.className "max-w-lg mx-auto animate-fade-in"
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

let private startSyncView (dispatch: Msg -> unit) =
    Html.div [
        prop.className "max-w-lg mx-auto animate-fade-in"
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

// ============================================
// Loading State View
// ============================================

let private loadingView (message: string) =
    Html.div [
        prop.className "max-w-md mx-auto animate-fade-in"
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

let private errorView (error: string) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "max-w-md mx-auto animate-fade-in"
        prop.children [
            Html.div [
                prop.className "rounded-xl bg-base-100 border border-white/5 overflow-hidden"
                prop.children [
                    // Error header with neon red
                    Html.div [
                        prop.className "bg-gradient-to-br from-neon-red to-neon-pink p-6 text-center"
                        prop.children [
                            Html.div [
                                prop.className "w-16 h-16 rounded-full bg-white/20 flex items-center justify-center mx-auto mb-3"
                                prop.children [ Icons.xCircle Icons.XL Icons.Primary ]
                            ]
                            Html.h2 [
                                prop.className "text-xl font-bold font-display text-white"
                                prop.text "Sync Failed"
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "p-6 text-center"
                        prop.children [
                            Html.p [
                                prop.className "text-base-content/70 mb-4"
                                prop.text error
                            ]
                            Button.primaryWithIcon "Try Again" (Icons.sync Icons.SM Icons.Primary) (fun () -> dispatch StartSync)
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Main View
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
                    tanWaitingView dispatch

                | FetchingTransactions ->
                    loadingView "Fetching transactions..."

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
