module Components.SyncFlow.Views.TransactionRow

open Feliz
open Components.SyncFlow.Types
open Types
open Shared.Domain
open Client.DesignSystem
open Components.SyncFlow.Views.InlineRuleForm

// ============================================
// Helper Functions
// ============================================

let private formatDateCompact (date: System.DateTime) =
    date.ToString("dd.MM")

let private categoryText (categoryId: YnabCategoryId option) (categoryOptions: (string * string) list) =
    match categoryId with
    | Some (YnabCategoryId id) ->
        let idStr = id.ToString()
        categoryOptions
        |> List.tryFind (fun (optId, _) -> optId = idStr)
        |> Option.map snd
        |> Option.defaultValue "Unknown"
    | None -> "—"

let private getRowStateClasses (tx: SyncTransaction) =
    // Skipped always shows faded, regardless of duplicate status
    if tx.Status = Skipped then
        "opacity-50"
    else
        match tx.DuplicateStatus with
        | ConfirmedDuplicate (_, _) -> "bg-neon-red/5 border-l-2 border-l-neon-red"
        | PossibleDuplicate (_, _) -> "bg-neon-orange/5 border-l-2 border-l-neon-orange"
        | NotDuplicate _ -> ""  // No special styling for NeedsAttention anymore

// ============================================
// Status Indicators
// ============================================

let private statusDot (tx: SyncTransaction) =
    let (dotColor, shouldPulse) =
        // Skipped always shows gray, regardless of duplicate status
        if tx.Status = Skipped then
            ("bg-base-content/30", false)
        else
            match tx.DuplicateStatus with
            | ConfirmedDuplicate (_, _) -> ("bg-neon-red", false)
            | PossibleDuplicate (_, _) -> ("bg-neon-orange", true)
            | NotDuplicate _ ->
                match tx.Status with
                | Pending | NeedsAttention -> ("bg-neon-orange", false)  // Same color for all uncategorized
                | AutoCategorized -> ("bg-neon-teal", false)
                | ManualCategorized -> ("bg-neon-green", false)
                | Skipped -> ("bg-base-content/30", false)  // Fallback
                | Imported -> ("bg-neon-green", false)

    let pulseClass = if shouldPulse then "animate-pulse" else ""
    Html.div [
        prop.className $"w-2 h-2 rounded-full flex-shrink-0 {dotColor} {pulseClass}"
    ]

let private duplicateIndicator (status: DuplicateStatus) =
    match status with
    | NotDuplicate _ -> Html.none
    | PossibleDuplicate (reason, _) ->
        Html.span [
            prop.className "cursor-help text-neon-orange"
            prop.title reason
            prop.children [ Icons.warning Icons.XS Icons.NeonOrange ]
        ]
    | ConfirmedDuplicate (reference, _) ->
        Html.span [
            prop.className "cursor-help text-neon-red"
            prop.title $"Already imported: {reference}"
            prop.children [ Icons.xCircle Icons.XS Icons.Error ]
        ]

// ============================================
// Action Buttons
// ============================================

let private expandChevron (tx: SyncTransaction) (isExpanded: bool) (dispatch: Msg -> unit) =
    let hasExpandableContent = not (System.String.IsNullOrWhiteSpace tx.Transaction.Memo)
    if hasExpandableContent then
        Html.button [
            prop.className "p-1 -ml-1 text-base-content/40 hover:text-base-content/70 transition-colors flex-shrink-0"
            prop.onClick (fun e ->
                e.stopPropagation()
                dispatch (ToggleTransactionExpand tx.Transaction.Id))
            prop.children [
                if isExpanded then Icons.chevronDown Icons.XS Icons.Default
                else Icons.chevronRight Icons.XS Icons.Default
            ]
        ]
    else
        Html.div [ prop.className "w-4 flex-shrink-0" ]  // Placeholder for alignment

let private skipToggleIcon (tx: SyncTransaction) (dispatch: Msg -> unit) =
    if tx.Status = Skipped then
        Html.button [
            prop.className "p-2 rounded-lg hover:bg-neon-green/10 text-neon-green/70 hover:text-neon-green transition-colors min-w-[44px] min-h-[44px] flex items-center justify-center"
            prop.title "Unskip transaction"
            prop.onClick (fun _ -> dispatch (UnskipTransaction tx.Transaction.Id))
            prop.children [ Icons.undo Icons.SM Icons.NeonGreen ]
        ]
    else
        Html.button [
            prop.className "p-2 rounded-lg hover:bg-base-content/10 text-base-content/40 hover:text-base-content/70 transition-colors min-w-[44px] min-h-[44px] flex items-center justify-center"
            prop.title "Skip transaction"
            prop.onClick (fun _ -> dispatch (SkipTransaction tx.Transaction.Id))
            prop.children [ Icons.forward Icons.SM Icons.Default ]
        ]

let private createRuleButton (tx: SyncTransaction) (showForm: bool) (manuallyCategorizedIds: Set<TransactionId>) (dispatch: Msg -> unit) =
    // Only show for manually categorized transactions with a category, and when form is not open
    let shouldShow =
        manuallyCategorizedIds.Contains tx.Transaction.Id &&
        tx.CategoryId.IsSome &&
        not showForm
    if shouldShow then
        Html.button [
            prop.className "p-2 rounded-lg hover:bg-neon-teal/10 text-neon-teal/70 hover:text-neon-teal transition-colors w-[32px] h-[32px] flex items-center justify-center"
            prop.title "Create categorization rule from this transaction"
            prop.onClick (fun e ->
                e.stopPropagation()
                dispatch (OpenInlineRuleForm tx.Transaction.Id))
            prop.children [ Icons.rules Icons.SM Icons.NeonTeal ]
        ]
    else
        // Placeholder to maintain consistent layout
        Html.div [ prop.className "w-[32px] h-[32px]" ]

// ============================================
// Expanded Content
// ============================================

let private memoRow (tx: SyncTransaction) =
    Html.div [
        prop.className "mx-3 my-2 px-4 py-3 rounded-lg bg-base-200/30 backdrop-blur-sm border border-white/5"
        prop.children [
            Html.div [
                prop.className "flex items-start gap-3"
                prop.children [
                    Html.div [
                        prop.className "flex-shrink-0 w-6 h-6 rounded-md bg-neon-teal/10 flex items-center justify-center"
                        prop.children [ Icons.info Icons.XS Icons.NeonTeal ]
                    ]
                    Html.div [
                        prop.className "flex-1 min-w-0"
                        prop.children [
                            Html.p [
                                prop.className "text-xs font-medium text-base-content/50 uppercase tracking-wider mb-1"
                                prop.text "Memo"
                            ]
                            Html.p [
                                prop.className "text-sm text-base-content/80 leading-relaxed"
                                prop.text tx.Transaction.Memo
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let private duplicateDebugInfo (tx: SyncTransaction) =
    let details = getDuplicateDetails tx.DuplicateStatus

    Html.div [
        prop.className "mt-3 px-3 py-2.5 rounded-lg bg-base-200/50 text-xs font-mono space-y-2 border border-white/5"
        prop.children [
            // Section header: BudgetBuddy Detection
            Html.div [
                prop.className "flex items-center gap-2 text-neon-teal/80 font-medium pb-1 border-b border-white/5"
                prop.children [
                    Icons.search Icons.XS Icons.NeonTeal
                    Html.span [ prop.text "BudgetBuddy Duplicate Detection" ]
                ]
            ]

            // Reference info
            Html.div [
                prop.className "flex items-center gap-2 flex-wrap"
                prop.children [
                    Html.span [ prop.className "text-base-content/50"; prop.text "Reference:" ]
                    Html.code [ prop.className "text-base-content bg-base-300/50 px-1 rounded"; prop.text details.TransactionReference ]
                    if details.ReferenceFoundInYnab then
                        Html.span [
                            prop.className "px-1.5 py-0.5 rounded text-[10px] bg-neon-green/20 text-neon-green border border-neon-green/30"
                            prop.text "Found in YNAB"
                        ]
                    else
                        Html.span [
                            prop.className "px-1.5 py-0.5 rounded text-[10px] bg-base-content/10 text-base-content/50 border border-white/10"
                            prop.text "Not in YNAB"
                        ]
                ]
            ]

            // Import ID info
            Html.div [
                prop.className "flex items-center gap-2"
                prop.children [
                    Html.span [ prop.className "text-base-content/50"; prop.text "Import ID:" ]
                    if details.ImportIdFoundInYnab then
                        Html.span [
                            prop.className "px-1.5 py-0.5 rounded text-[10px] bg-neon-green/20 text-neon-green border border-neon-green/30"
                            prop.text "Exists in YNAB"
                        ]
                    else
                        Html.span [
                            prop.className "px-1.5 py-0.5 rounded text-[10px] bg-base-content/10 text-base-content/50 border border-white/10"
                            prop.text "New"
                        ]
                ]
            ]

            // Fuzzy match info (if applicable)
            match details.FuzzyMatchDate, details.FuzzyMatchAmount, details.FuzzyMatchPayee with
            | Some date, Some amount, payee ->
                let dateStr = date.ToString("yyyy-MM-dd")
                let payeeStr = payee |> Option.defaultValue "?"
                let amountStr = sprintf "%.2f" amount
                Html.div [
                    prop.className "flex items-center gap-2 text-neon-orange/90 pt-1 border-t border-white/5"
                    prop.children [
                        Icons.warning Icons.XS Icons.NeonOrange
                        Html.span [
                            prop.text $"Fuzzy match: {payeeStr} on {dateStr} for {amountStr}"
                        ]
                    ]
                ]
            | _ -> Html.none

            // YNAB Import Status (if attempted)
            match tx.YnabImportStatus with
            | NotAttempted -> Html.none
            | YnabImported ->
                Html.div [
                    prop.className "flex items-center gap-2 text-neon-green pt-2 mt-1 border-t border-white/10"
                    prop.children [
                        Icons.checkCircle Icons.XS Icons.NeonGreen
                        Html.span [ prop.text "YNAB: Successfully imported" ]
                    ]
                ]
            | RejectedByYnab reason ->
                let reasonText =
                    match reason with
                    | DuplicateImportId id -> sprintf "YNAB rejected: duplicate import_id (%s)" id
                    | UnknownRejection msg -> sprintf "YNAB rejected: %s" (msg |> Option.defaultValue "unknown reason")
                Html.div [
                    prop.className "flex items-center gap-2 text-neon-red pt-2 mt-1 border-t border-white/10 flex-wrap"
                    prop.children [
                        Icons.xCircle Icons.XS Icons.Error
                        Html.span [ prop.text reasonText ]
                        // Explain discrepancy if BudgetBuddy didn't detect it
                        match tx.DuplicateStatus with
                        | NotDuplicate _ ->
                            Html.span [
                                prop.className "text-neon-orange font-medium"
                                prop.text "(BudgetBuddy missed this!)"
                            ]
                        | _ -> Html.none
                    ]
                ]
        ]
    ]

// ============================================
// Main Transaction Row Component
// ============================================

let transactionRow
    (tx: SyncTransaction)
    (categoryOptions: (string * string) list)
    (payeeOptions: (string * string) list)
    (expandedIds: Set<TransactionId>)
    (inlineRuleFormState: InlineRuleFormState option)
    (manuallyCategorizedIds: Set<TransactionId>)
    (isPendingCategorySave: bool)
    (isPendingPayeeSave: bool)
    (dispatch: Msg -> unit) =

    let rowClasses = getRowStateClasses tx
    let originalPayee = tx.Transaction.Payee |> Option.defaultValue ""
    // Use override if set (including empty string), otherwise fall back to original
    // PayeeOverride = None means "not edited", Some "" means "user cleared it"
    let displayPayee =
        match tx.PayeeOverride with
        | Some p -> p  // User has edited (even if empty)
        | None -> originalPayee  // Not edited, use original
    let dateStr = formatDateCompact tx.Transaction.BookingDate
    let isExpanded = expandedIds.Contains tx.Transaction.Id
    let hasExpandableContent = not (System.String.IsNullOrWhiteSpace tx.Transaction.Memo)

    // Check if the inline rule form is open for THIS transaction
    let showRuleForm =
        inlineRuleFormState
        |> Option.exists (fun f -> f.TransactionId = tx.Transaction.Id)

    // Pending save indicator component for category
    let pendingCategorySaveIndicator =
        if isPendingCategorySave then
            Html.span [
                prop.className "ml-2 text-xs text-neon-orange animate-pulse"
                prop.title "Saving category..."
                prop.text "●"
            ]
        else
            Html.none

    // Pending save indicator component for payee
    let pendingPayeeSaveIndicator =
        if isPendingPayeeSave then
            Html.span [
                prop.className "ml-1 text-xs text-neon-orange animate-pulse"
                prop.title "Saving payee..."
                prop.text "●"
            ]
        else
            Html.none

    Html.div [
        prop.className $"group border-b border-white/5 last:border-b-0 transition-all duration-200 {rowClasses}"
        prop.children [
            // Mobile Layout (Default - shown below md breakpoint)
            Html.div [
                prop.className "md:hidden flex flex-col gap-2 p-3"
                prop.children [
                    // Line 1: Expand + Status + Category + Amount
                    Html.div [
                        prop.className "flex items-center gap-2"
                        prop.children [
                            expandChevron tx isExpanded dispatch
                            statusDot tx
                            duplicateIndicator tx.DuplicateStatus
                            // Category: Selectbox for active, text for skipped
                            Html.div [
                                prop.className "flex-1 min-w-0 flex items-center"
                                prop.children [
                                    if tx.Status = Skipped then
                                        // Skipped: render as plain text (fast)
                                        Html.span [
                                            prop.className "text-sm text-base-content/50 truncate block py-2"
                                            prop.text (categoryText tx.CategoryId categoryOptions)
                                        ]
                                    else
                                        // Active: render selectbox (interactive)
                                        Input.searchableSelect
                                            (tx.CategoryId
                                             |> Option.map (fun (YnabCategoryId id) -> id.ToString())
                                             |> Option.defaultValue "")
                                            (fun (value: string) ->
                                                if value = "" then
                                                    dispatch (CategorizeTransaction (tx.Transaction.Id, None))
                                                else
                                                    dispatch (CategorizeTransaction (tx.Transaction.Id, Some (YnabCategoryId (System.Guid.Parse value)))))
                                            "Category..."
                                            categoryOptions
                                    pendingCategorySaveIndicator
                                ]
                            ]
                            // Amount (fixed width for alignment)
                            Html.div [
                                prop.className "flex-shrink-0 w-24 text-right"
                                prop.children [
                                    Money.view {
                                        Money.defaultProps with
                                            Amount = tx.Transaction.Amount.Amount
                                            Currency = tx.Transaction.Amount.Currency
                                            Size = Money.Small
                                            Glow = Money.NoGlow
                                    }
                                ]
                            ]
                        ]
                    ]
                    // Line 2: Payee (editable) + Date + Actions
                    Html.div [
                        prop.className "flex items-center gap-2 pl-4 text-sm"
                        prop.children [
                            // Payee ComboBox (editable with suggestions)
                            Html.div [
                                prop.className "flex-1 min-w-0 flex items-center"
                                prop.children [
                                    if tx.Status = Skipped then
                                        // Skipped: render as plain text
                                        Html.span [
                                            prop.className "text-sm text-base-content/50 truncate"
                                            prop.title displayPayee
                                            prop.text (if displayPayee = "" then "—" else displayPayee)
                                        ]
                                    else
                                        // Active: render ComboBox (interactive)
                                        Input.comboBox
                                            displayPayee
                                            (fun value ->
                                                // Always store as Some - even empty string means "user edited"
                                                dispatch (SetPayeeOverride (tx.Transaction.Id, Some value)))
                                            "Payee..."
                                            payeeOptions
                                    pendingPayeeSaveIndicator
                                ]
                            ]
                            Html.span [
                                prop.className "text-xs text-base-content/40 tabular-nums flex-shrink-0"
                                prop.text dateStr
                            ]
                            // Actions (always visible on mobile for touch, fixed width)
                            Html.div [
                                prop.className "flex items-center gap-0.5 w-16 flex-shrink-0"
                                prop.children [
                                    createRuleButton tx showRuleForm manuallyCategorizedIds dispatch
                                    skipToggleIcon tx dispatch
                                ]
                            ]
                        ]
                    ]
                ]
            ]

            // Desktop Layout (shown at md breakpoint and above)
            Html.div [
                prop.className "hidden md:flex items-center gap-3 px-4 py-2.5 hover:bg-white/5 transition-colors"
                prop.children [
                    // Expand chevron
                    expandChevron tx isExpanded dispatch
                    // Status dot
                    statusDot tx
                    // Duplicate indicator
                    duplicateIndicator tx.DuplicateStatus
                    // Actions (always visible, fixed width for layout stability)
                    Html.div [
                        prop.className "flex items-center gap-0.5 w-16 flex-shrink-0"
                        prop.children [
                            createRuleButton tx showRuleForm manuallyCategorizedIds dispatch
                            skipToggleIcon tx dispatch
                        ]
                    ]
                    // Category: Selectbox for active, text for skipped - fixed width
                    Html.div [
                        prop.className "w-96 flex-shrink-0 flex items-center"
                        prop.children [
                            if tx.Status = Skipped then
                                // Skipped: render as plain text (fast)
                                Html.span [
                                    prop.className "text-sm text-base-content/50 truncate block py-2"
                                    prop.text (categoryText tx.CategoryId categoryOptions)
                                ]
                            else
                                // Active: render selectbox (interactive)
                                Input.searchableSelect
                                    (tx.CategoryId
                                     |> Option.map (fun (YnabCategoryId id) -> id.ToString())
                                     |> Option.defaultValue "")
                                    (fun (value: string) ->
                                        if value = "" then
                                            dispatch (CategorizeTransaction (tx.Transaction.Id, None))
                                        else
                                            dispatch (CategorizeTransaction (tx.Transaction.Id, Some (YnabCategoryId (System.Guid.Parse value)))))
                                    "Category..."
                                    categoryOptions
                            pendingCategorySaveIndicator
                        ]
                    ]
                    // Payee ComboBox (editable with suggestions)
                    Html.div [
                        prop.className "w-48 flex-shrink-0 flex items-center"
                        prop.children [
                            if tx.Status = Skipped then
                                // Skipped: render as plain text
                                Html.span [
                                    prop.className "text-sm text-base-content/50 truncate block py-2"
                                    prop.title displayPayee
                                    prop.text (if displayPayee = "" then "—" else displayPayee)
                                ]
                            else
                                // Active: render ComboBox (interactive)
                                Input.comboBox
                                    displayPayee
                                    (fun value ->
                                        // Always store as Some - even empty string means "user edited"
                                        dispatch (SetPayeeOverride (tx.Transaction.Id, Some value)))
                                    "Payee..."
                                    payeeOptions
                            pendingPayeeSaveIndicator
                        ]
                    ]
                    // Date
                    Html.span [
                        prop.className "w-16 text-xs text-base-content/50 text-right tabular-nums"
                        prop.text dateStr
                    ]
                    // Amount
                    Html.div [
                        prop.className "w-24 text-right"
                        prop.children [
                            Money.view {
                                Money.defaultProps with
                                    Amount = tx.Transaction.Amount.Amount
                                    Currency = tx.Transaction.Amount.Currency
                                    Size = Money.Small
                                    Glow = Money.NoGlow
                            }
                        ]
                    ]
                ]
            ]

            // Expanded content (memo + duplicate debug info)
            if isExpanded then
                Html.div [
                    prop.className "px-4 pb-3"
                    prop.children [
                        // Memo (if exists)
                        if hasExpandableContent then
                            memoRow tx
                        // Always show duplicate debug info when expanded
                        duplicateDebugInfo tx
                    ]
                ]

            // Inline rule form (when active for this transaction)
            match inlineRuleFormState with
            | Some form when form.TransactionId = tx.Transaction.Id ->
                inlineRuleForm form dispatch
            | _ -> ()
        ]
    ]
