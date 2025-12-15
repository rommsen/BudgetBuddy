module Components.SyncFlow.Views.TransactionList

open Feliz
open Components.SyncFlow.Types
open Types
open Shared.Domain
open Client.DesignSystem
open Components.SyncFlow.Views.TransactionRow

// ============================================
// Transaction Filter Helper
// ============================================

let filterTransactions (filter: TransactionFilter) (transactions: SyncTransaction list) =
    match filter with
    | AllTransactions -> transactions
    | CategorizedTransactions ->
        transactions
        |> List.filter (fun tx ->
            tx.CategoryId.IsSome &&
            tx.Status <> Skipped &&
            tx.Status <> Imported)
    | UncategorizedTransactions ->
        transactions
        |> List.filter (fun tx ->
            tx.CategoryId.IsNone &&
            tx.Status <> Skipped &&
            tx.Status <> Imported)
    | SkippedTransactions ->
        transactions
        |> List.filter (fun tx -> tx.Status = Skipped)
    | ConfirmedDuplicates ->
        transactions
        |> List.filter (fun tx ->
            match tx.DuplicateStatus with
            | ConfirmedDuplicate _ -> true
            | _ -> false)

// ============================================
// Transaction List View
// ============================================

let transactionListView (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-4"
        prop.children [
            // Stats summary card using design system
            match model.SyncTransactions with
            | Success transactions ->
                // Calculate all counts in a single pass for better performance
                let (categorized, uncategorized, skipped, confirmedDuplicates, ynabRejected) =
                    transactions |> List.fold (fun (cat, uncat, skip, dup, rej) tx ->
                        // Categorized: has CategoryId, not Skipped/Imported
                        let cat' =
                            if tx.CategoryId.IsSome && tx.Status <> Skipped && tx.Status <> Imported then
                                cat + 1
                            else cat
                        // Uncategorized: no CategoryId, not Skipped/Imported
                        let uncat' =
                            if tx.CategoryId.IsNone && tx.Status <> Skipped && tx.Status <> Imported then
                                uncat + 1
                            else uncat
                        // Skipped: Status = Skipped
                        let skip' = if tx.Status = Skipped then skip + 1 else skip
                        // ConfirmedDuplicates: BudgetBuddy detected BEFORE import
                        let dup' =
                            match tx.DuplicateStatus with
                            | ConfirmedDuplicate (_, _) -> dup + 1
                            | _ -> dup
                        // YnabRejected: YNAB rejected DURING import
                        let rej' =
                            match tx.YnabImportStatus with
                            | RejectedByYnab _ -> rej + 1
                            | _ -> rej
                        (cat', uncat', skip', dup', rej')
                    ) (0, 0, 0, 0, 0)
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
                                    OnClick = Some (fun () -> dispatch (SetFilter AllTransactions))
                                    IsActive = model.ActiveFilter = AllTransactions
                            }
                            Stats.view {
                                Stats.defaultProps with
                                    Label = "Categorized"
                                    Value = string categorized
                                    Accent = Stats.Green
                                    Size = Stats.Compact
                                    OnClick = Some (fun () -> dispatch (SetFilter CategorizedTransactions))
                                    IsActive = model.ActiveFilter = CategorizedTransactions
                            }
                            Stats.view {
                                Stats.defaultProps with
                                    Label = "Uncategorized"
                                    Value = string uncategorized
                                    Accent = if uncategorized > 0 then Stats.Orange else Stats.Gradient
                                    Size = Stats.Compact
                                    OnClick = Some (fun () -> dispatch (SetFilter UncategorizedTransactions))
                                    IsActive = model.ActiveFilter = UncategorizedTransactions
                            }
                            Stats.view {
                                Stats.defaultProps with
                                    Label = "Skipped"
                                    Value = string skipped
                                    Size = Stats.Compact
                                    OnClick = Some (fun () -> dispatch (SetFilter SkippedTransactions))
                                    IsActive = model.ActiveFilter = SkippedTransactions
                            }
                        ]
                        // Compact summary: Skipped transactions (duplicates + YNAB rejected)
                        if confirmedDuplicates > 0 || ynabRejected > 0 then
                            let totalSkippedIssues = confirmedDuplicates + ynabRejected
                            Html.div [
                                prop.className "rounded-xl bg-base-200/50 border-l-4 border-neon-teal overflow-hidden"
                                prop.children [
                                    // Header
                                    Html.div [
                                        prop.className "px-4 py-3 flex items-center gap-3"
                                        prop.children [
                                            Icons.info Icons.MD Icons.NeonTeal
                                            Html.span [
                                                prop.className "text-sm font-medium text-base-content"
                                                prop.text (sprintf "%d transaction%s won't be imported" totalSkippedIssues (if totalSkippedIssues = 1 then "" else "s"))
                                            ]
                                        ]
                                    ]
                                    // Details list
                                    Html.div [
                                        prop.className "px-4 pb-3 space-y-2"
                                        prop.children [
                                            // Pre-detected duplicates
                                            if confirmedDuplicates > 0 then
                                                let activeClass =
                                                    if model.ActiveFilter = ConfirmedDuplicates then
                                                        "ring-1 ring-neon-teal"
                                                    else ""
                                                Html.div [
                                                    prop.className $"flex items-center gap-2 px-3 py-2 rounded-lg bg-base-100/50 cursor-pointer hover:bg-base-100 transition-colors {activeClass}"
                                                    prop.onClick (fun _ -> dispatch (SetFilter ConfirmedDuplicates))
                                                    prop.children [
                                                        Html.span [
                                                            prop.className "text-base-content/40"
                                                            prop.text "├"
                                                        ]
                                                        Html.span [
                                                            prop.className "text-sm text-base-content/80"
                                                            prop.text (sprintf "%d duplicate%s (auto-detected)" confirmedDuplicates (if confirmedDuplicates = 1 then "" else "s"))
                                                        ]
                                                    ]
                                                ]
                                            // YNAB rejected
                                            if ynabRejected > 0 then
                                                Html.div [
                                                    prop.className "flex items-center justify-between gap-2 px-3 py-2 rounded-lg bg-neon-orange/5 border border-neon-orange/20"
                                                    prop.children [
                                                        Html.div [
                                                            prop.className "flex items-center gap-2"
                                                            prop.children [
                                                                Html.span [
                                                                    prop.className "text-base-content/40"
                                                                    prop.text "└"
                                                                ]
                                                                Html.span [
                                                                    prop.className "text-sm text-neon-orange"
                                                                    prop.text (sprintf "%d rejected by YNAB" ynabRejected)
                                                                ]
                                                            ]
                                                        ]
                                                        // Inline force re-import button
                                                        Html.button [
                                                            prop.className "text-xs text-neon-teal hover:text-neon-teal/80 flex items-center gap-1"
                                                            prop.onClick (fun _ -> dispatch ForceImportDuplicates)
                                                            prop.children [
                                                                Icons.sync Icons.XS Icons.NeonTeal
                                                                Html.span [ prop.text "Force import" ]
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
            | _ -> Html.none

            // Actions bar (sticky with glassmorphism)
            Html.div [
                prop.className "sticky top-16 z-40 bg-base-100/80 backdrop-blur-xl rounded-xl shadow-lg p-3 md:p-4 border border-white/10"
                prop.children [
                    Html.div [
                        prop.className "flex flex-wrap justify-between gap-2"
                        prop.children [
                            // Skip/Unskip All buttons (left side)
                            Html.div [
                                prop.className "flex gap-2"
                                prop.children [
                                    // Calculate counts for button states
                                    let (skippableCount, unskippableCount) =
                                        match model.SyncTransactions with
                                        | Success transactions ->
                                            let filtered = filterTransactions model.ActiveFilter transactions
                                            let skippable = filtered |> List.filter (fun tx -> tx.Status <> Skipped && tx.Status <> Imported) |> List.length
                                            let unskippable = filtered |> List.filter (fun tx -> tx.Status = Skipped) |> List.length
                                            (skippable, unskippable)
                                        | _ -> (0, 0)

                                    // Skip All button - shown when there are skippable transactions
                                    if skippableCount > 0 then
                                        Button.view {
                                            Button.defaultProps with
                                                Text = $"Skip All ({skippableCount})"
                                                Variant = Button.Ghost
                                                Size = Button.Small
                                                Icon = Some (Icons.forward Icons.SM Icons.Default)
                                                OnClick = fun () -> dispatch SkipAllVisible
                                        }

                                    // Unskip All button - shown when there are unskippable transactions
                                    if unskippableCount > 0 then
                                        Button.view {
                                            Button.defaultProps with
                                                Text = $"Unskip All ({unskippableCount})"
                                                Variant = Button.Ghost
                                                Size = Button.Small
                                                Icon = Some (Icons.undo Icons.SM Icons.NeonGreen)
                                                OnClick = fun () -> dispatch UnskipAllVisible
                                        }
                                ]
                            ]

                            // Right side buttons
                            Html.div [
                                prop.className "flex gap-2"
                                prop.children [
                                    Button.secondary "Cancel" (fun () -> dispatch CancelSync)
                                    // Enable import if any non-skipped, non-imported transaction exists
                                    let importableCount =
                                        match model.SyncTransactions with
                                        | Success transactions ->
                                            transactions
                                            |> List.filter (fun tx ->
                                                tx.Status <> Skipped &&
                                                tx.Status <> Imported)
                                            |> List.length
                                        | _ -> 0
                                    Button.view {
                                        Button.defaultProps with
                                            Text = if importableCount > 0 then sprintf "Import %d to YNAB" importableCount else "Import to YNAB"
                                            Variant = Button.Primary
                                            Icon = Some (Icons.upload Icons.SM Icons.Primary)
                                            OnClick = fun () -> dispatch ImportToYnab
                                            IsDisabled = importableCount = 0
                                    }
                                ]
                            ]
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
                // Pre-compute category options ONCE instead of per row
                // This reduces 193 x 160 = 30,880 string operations to just 160
                let categoryOptions =
                    model.Categories
                    |> List.map (fun cat ->
                        let (YnabCategoryId id) = cat.Id
                        (id.ToString(), $"{cat.GroupName}: {cat.Name}"))

                // Apply active filter to transactions
                let filteredTransactions = filterTransactions model.ActiveFilter transactions

                // Show filter info when not showing all
                Html.div [
                    prop.className "space-y-3"
                    prop.children [
                        // Filter indicator
                        if model.ActiveFilter <> AllTransactions then
                            Html.div [
                                prop.className "flex items-center justify-between px-4 py-2 rounded-lg bg-neon-teal/10 border border-neon-teal/30"
                                prop.children [
                                    Html.span [
                                        prop.className "text-sm text-neon-teal"
                                        prop.children [
                                            Html.text $"Showing {filteredTransactions.Length} of {transactions.Length} transactions"
                                        ]
                                    ]
                                    Html.button [
                                        prop.className "text-xs text-neon-teal hover:text-neon-teal/80 underline"
                                        prop.onClick (fun _ -> dispatch (SetFilter AllTransactions))
                                        prop.text "Show all"
                                    ]
                                ]
                            ]

                        // Compact list container with card styling
                        if filteredTransactions.IsEmpty then
                            Card.emptyState
                                (Icons.search Icons.XL Icons.Default)
                                "No matching transactions"
                                "Try selecting a different filter above."
                                (Some (Button.ghost "Show all" (fun () -> dispatch (SetFilter AllTransactions))))
                        else
                            Html.div [
                                prop.className "bg-base-100 rounded-xl border border-white/5 overflow-hidden"
                                prop.children [
                                    for tx in filteredTransactions do
                                        let (TransactionId id) = tx.Transaction.Id
                                        // Check if this transaction has a pending category save
                                        let isPendingSave =
                                            model.PendingCategoryVersions |> Map.containsKey tx.Transaction.Id
                                        Html.div [
                                            prop.key id
                                            prop.children [ transactionRow tx categoryOptions model.ExpandedTransactionIds model.InlineRuleForm model.ManuallyCategorizedIds isPendingSave dispatch ]
                                        ]
                                ]
                            ]
                    ]
                ]
            | Failure error ->
                ErrorDisplay.cardCompact error None
        ]
    ]
