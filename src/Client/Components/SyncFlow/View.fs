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

/// Filter transactions based on the active filter
let private filterTransactions (filter: TransactionFilter) (transactions: SyncTransaction list) =
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

let private tanWaitingView (isConfirming: bool) (dispatch: Msg -> unit) =
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
                                if isConfirming then
                                    Button.primaryLoading "Importing..." true (fun () -> ())
                                else
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

/// Duplicate status badge component (kept for backwards compatibility)
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

// ============================================
// NEW: Compact Transaction Row Components
// ============================================

/// Status dot - small colored indicator based on transaction state
/// Priority: Skipped > DuplicateStatus > TransactionStatus
let private statusDot (tx: SyncTransaction) =
    let (dotColor, shouldPulse) =
        // Skipped always shows gray, regardless of duplicate status
        if tx.Status = Skipped then
            ("bg-base-content/30", false)
        else
            match tx.DuplicateStatus with
            | ConfirmedDuplicate _ -> ("bg-neon-red", false)
            | PossibleDuplicate _ -> ("bg-neon-orange", true)
            | NotDuplicate ->
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

/// Row state classes for background/border styling
/// Priority: Skipped > DuplicateStatus > TransactionStatus
let private getRowStateClasses (tx: SyncTransaction) =
    // Skipped always shows faded, regardless of duplicate status
    if tx.Status = Skipped then
        "opacity-50"
    else
        match tx.DuplicateStatus with
        | ConfirmedDuplicate _ -> "bg-neon-red/5 border-l-2 border-l-neon-red"
        | PossibleDuplicate _ -> "bg-neon-orange/5 border-l-2 border-l-neon-orange"
        | NotDuplicate -> ""  // No special styling for NeedsAttention anymore

/// Duplicate indicator with tooltip (icon only)
let private duplicateIndicator (status: DuplicateStatus) =
    match status with
    | NotDuplicate -> Html.none
    | PossibleDuplicate reason ->
        Html.span [
            prop.className "cursor-help text-neon-orange"
            prop.title reason
            prop.children [ Icons.warning Icons.XS Icons.NeonOrange ]
        ]
    | ConfirmedDuplicate reference ->
        Html.span [
            prop.className "cursor-help text-neon-red"
            prop.title $"Already imported: {reference}"
            prop.children [ Icons.xCircle Icons.XS Icons.Error ]
        ]

/// Skip toggle as icon button
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

/// "Create Rule" button - shown for manually categorized transactions
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

/// Inline rule creation form (expands below transaction row)
let private inlineRuleForm
    (formState: InlineRuleFormState)
    (dispatch: Msg -> unit) =

    let patternTypeToString pt =
        match pt with
        | Contains -> "Contains"
        | Exact -> "Exact"
        | PatternType.Regex -> "Regex"

    let stringToPatternType s =
        match s with
        | "Exact" -> Exact
        | "Regex" -> PatternType.Regex
        | _ -> Contains

    let targetFieldToString tf =
        match tf with
        | Combined -> "Combined"
        | Payee -> "Payee"
        | Memo -> "Memo"

    let stringToTargetField s =
        match s with
        | "Payee" -> Payee
        | "Memo" -> Memo
        | _ -> Combined

    Html.div [
        prop.className "border-t border-white/5 bg-base-200/50 p-4 animate-fade-in"
        prop.children [
            // Header
            Html.div [
                prop.className "flex items-center justify-between mb-4"
                prop.children [
                    Html.div [
                        prop.className "flex items-center gap-2"
                        prop.children [
                            Icons.rules Icons.SM Icons.NeonTeal
                            Html.span [
                                prop.className "text-sm font-medium text-base-content"
                                prop.text "Create Categorization Rule"
                            ]
                        ]
                    ]
                    Html.button [
                        prop.className "p-1 rounded hover:bg-white/10 text-base-content/50 hover:text-base-content"
                        prop.onClick (fun _ -> dispatch CloseInlineRuleForm)
                        prop.children [ Icons.x Icons.SM Icons.Default ]
                    ]
                ]
            ]

            // Form content - responsive grid
            Html.div [
                prop.className "space-y-3"
                prop.children [
                    // Row 1: Rule name + Category display
                    Html.div [
                        prop.className "grid grid-cols-1 md:grid-cols-2 gap-3"
                        prop.children [
                            // Rule name input
                            Html.div [
                                prop.className "space-y-1.5"
                                prop.children [
                                    Html.label [
                                        prop.className "block text-sm font-medium text-base-content/80"
                                        prop.text "Rule Name"
                                    ]
                                    Input.textSimple
                                        formState.RuleName
                                        (UpdateInlineRuleName >> dispatch)
                                        "e.g., Amazon Purchases"
                                ]
                            ]
                            // Category display (read-only)
                            Html.div [
                                prop.className "space-y-1.5"
                                prop.children [
                                    Html.label [
                                        prop.className "block text-sm font-medium text-base-content/80"
                                        prop.text "Category"
                                    ]
                                    Html.div [
                                        prop.className "flex items-center gap-2 px-3 py-2 bg-neon-teal/10 border border-neon-teal/30 rounded-lg text-neon-teal"
                                        prop.children [
                                            Icons.check Icons.SM Icons.NeonTeal
                                            Html.span [ prop.text formState.CategoryName ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]

                    // Row 2: Pattern + Type + Field
                    Html.div [
                        prop.className "grid grid-cols-1 md:grid-cols-12 gap-3"
                        prop.children [
                            // Pattern (spans 6 cols on desktop)
                            Html.div [
                                prop.className "md:col-span-6"
                                prop.children [
                                    Html.div [
                                        prop.className "space-y-1.5"
                                        prop.children [
                                            Html.label [
                                                prop.className "block text-sm font-medium text-base-content/80"
                                                prop.children [
                                                    Html.text "Pattern "
                                                    Html.span [ prop.className "text-neon-orange"; prop.text "*" ]
                                                ]
                                            ]
                                            Html.input [
                                                prop.type' "text"
                                                prop.className "w-full px-3 py-2 bg-base-200 border border-white/10 rounded-lg text-base-content font-mono text-sm focus:border-neon-teal focus:ring-1 focus:ring-neon-teal/50 outline-none transition-all placeholder:text-base-content/30"
                                                prop.value formState.Pattern
                                                prop.onChange (UpdateInlineRulePattern >> dispatch)
                                                prop.placeholder "Text to match..."
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                            // Pattern Type (spans 3 cols)
                            Html.div [
                                prop.className "md:col-span-3"
                                prop.children [
                                    Html.div [
                                        prop.className "space-y-1.5"
                                        prop.children [
                                            Html.label [
                                                prop.className "block text-sm font-medium text-base-content/80"
                                                prop.text "Type"
                                            ]
                                            Input.selectSimple
                                                (patternTypeToString formState.PatternType)
                                                (fun value -> dispatch (UpdateInlineRulePatternType (stringToPatternType value)))
                                                [
                                                    ("Contains", "Contains")
                                                    ("Exact", "Exact Match")
                                                    ("Regex", "Regex")
                                                ]
                                        ]
                                    ]
                                ]
                            ]
                            // Target Field (spans 3 cols)
                            Html.div [
                                prop.className "md:col-span-3"
                                prop.children [
                                    Html.div [
                                        prop.className "space-y-1.5"
                                        prop.children [
                                            Html.label [
                                                prop.className "block text-sm font-medium text-base-content/80"
                                                prop.text "Match In"
                                            ]
                                            Input.selectSimple
                                                (targetFieldToString formState.TargetField)
                                                (fun value -> dispatch (UpdateInlineRuleTargetField (stringToTargetField value)))
                                                [
                                                    ("Combined", "Payee & Memo")
                                                    ("Payee", "Payee only")
                                                    ("Memo", "Memo only")
                                                ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]

                    // Row 3: Actions
                    Html.div [
                        prop.className "flex flex-col sm:flex-row justify-end gap-2 pt-2"
                        prop.children [
                            Button.ghost "Cancel" (fun () -> dispatch CloseInlineRuleForm)
                            Button.view {
                                Button.defaultProps with
                                    Text = "Create Rule"
                                    Variant = Button.Primary
                                    IsLoading = formState.IsSaving
                                    IsDisabled =
                                        formState.IsSaving ||
                                        System.String.IsNullOrWhiteSpace formState.Pattern ||
                                        System.String.IsNullOrWhiteSpace formState.RuleName
                                    OnClick = fun () -> dispatch SaveInlineRule
                                    Icon = Some (Icons.check Icons.SM Icons.Primary)
                            }
                        ]
                    ]
                ]
            ]
        ]
    ]

/// Compact date format for row display
let private formatDateCompact (date: System.DateTime) =
    date.ToString("dd.MM")

/// Category display for skipped transactions (text only, no selectbox)
/// Returns the category name if found, otherwise a placeholder
let private categoryText (categoryId: YnabCategoryId option) (categoryOptions: (string * string) list) =
    match categoryId with
    | Some (YnabCategoryId id) ->
        let idStr = id.ToString()
        categoryOptions
        |> List.tryFind (fun (optId, _) -> optId = idStr)
        |> Option.map snd
        |> Option.defaultValue "Unknown"
    | None -> "—"

/// Expand chevron button (only shown when memo exists)
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

/// Memo detail row (shown when expanded) - glassmorphism style
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

/// NEW: Compact Transaction Row (Mobile-First)
/// categoryOptions is pre-computed once, not per row
let private transactionRow
    (tx: SyncTransaction)
    (categoryOptions: (string * string) list)
    (expandedIds: Set<TransactionId>)
    (inlineRuleFormState: InlineRuleFormState option)
    (manuallyCategorizedIds: Set<TransactionId>)
    (dispatch: Msg -> unit) =

    let rowClasses = getRowStateClasses tx
    let payee = tx.Transaction.Payee |> Option.defaultValue "Unknown"
    let dateStr = formatDateCompact tx.Transaction.BookingDate
    let isExpanded = expandedIds.Contains tx.Transaction.Id
    let hasExpandableContent = not (System.String.IsNullOrWhiteSpace tx.Transaction.Memo)

    // Check if the inline rule form is open for THIS transaction
    let showRuleForm =
        inlineRuleFormState
        |> Option.exists (fun f -> f.TransactionId = tx.Transaction.Id)

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
                                prop.className "flex-1 min-w-0"
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
                    // Line 2: Payee + Date + Actions
                    Html.div [
                        prop.className "flex items-center gap-2 pl-4 text-sm"
                        prop.children [
                            // Payee (as link if external link exists)
                            match tx.ExternalLinks |> List.tryHead with
                            | Some link ->
                                Html.a [
                                    prop.className "flex-1 flex items-center gap-1 min-w-0 text-neon-teal hover:text-neon-teal/80 transition-colors"
                                    prop.href link.Url
                                    prop.target "_blank"
                                    prop.title $"{payee} - {link.Label}"
                                    prop.children [
                                        Html.span [ prop.className "truncate"; prop.text payee ]
                                        Icons.externalLink Icons.XS Icons.NeonTeal
                                    ]
                                ]
                            | None ->
                                Html.span [
                                    prop.className "flex-1 truncate text-base-content/70"
                                    prop.title payee
                                    prop.text payee
                                ]
                            Html.span [
                                prop.className "text-xs text-base-content/40 tabular-nums"
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
                        prop.className "w-96 flex-shrink-0"
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
                        ]
                    ]
                    // Payee (as link if external link exists)
                    match tx.ExternalLinks |> List.tryHead with
                    | Some link ->
                        Html.a [
                            prop.className "flex-1 flex items-center gap-1.5 min-w-0 text-sm text-neon-teal hover:text-neon-teal/80 transition-colors"
                            prop.href link.Url
                            prop.target "_blank"
                            prop.title $"{payee} - {link.Label}"
                            prop.children [
                                Html.span [ prop.className "truncate"; prop.text payee ]
                                Icons.externalLink Icons.XS Icons.NeonTeal
                            ]
                        ]
                    | None ->
                        Html.span [
                            prop.className "flex-1 truncate text-sm text-base-content"
                            prop.title payee
                            prop.text payee
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

            // Memo row (when expanded and memo exists)
            if isExpanded && hasExpandableContent then
                memoRow tx

            // Inline rule form (when active for this transaction)
            match inlineRuleFormState with
            | Some form when form.TransactionId = tx.Transaction.Id ->
                inlineRuleForm form dispatch
            | _ -> ()
        ]
    ]

// ============================================
// OLD: Transaction Card Component (DEPRECATED)
// ============================================

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
                    Input.searchableSelect
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
                let (categorized, uncategorized, skipped, confirmedDuplicates) =
                    transactions |> List.fold (fun (cat, uncat, skip, dup) tx ->
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
                        // ConfirmedDuplicates only (not PossibleDuplicate)
                        let dup' =
                            match tx.DuplicateStatus with
                            | ConfirmedDuplicate _ -> dup + 1
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
                        // Confirmed duplicates info banner (clickable filter)
                        if confirmedDuplicates > 0 then
                            let activeClass =
                                if model.ActiveFilter = ConfirmedDuplicates then
                                    "ring-2 ring-neon-orange ring-offset-2 ring-offset-base-100"
                                else ""
                            Html.div [
                                prop.className $"flex items-center gap-3 p-3 rounded-xl bg-neon-orange/10 border border-neon-orange/30 cursor-pointer hover:bg-neon-orange/20 transition-colors {activeClass}"
                                prop.onClick (fun _ -> dispatch (SetFilter ConfirmedDuplicates))
                                prop.children [
                                    Icons.warning Icons.MD Icons.NeonOrange
                                    Html.div [
                                        prop.className "flex-1"
                                        prop.children [
                                            Html.p [
                                                prop.className "text-sm font-medium text-neon-orange"
                                                prop.text $"{confirmedDuplicates} bekannte Duplikate (automatisch übersprungen)"
                                            ]
                                            Html.p [
                                                prop.className "text-xs text-base-content/60"
                                                prop.text "Diese Transaktionen wurden durch Reference oder Import-ID als bereits in YNAB vorhanden erkannt."
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
                            // Enable import if any non-skipped, non-imported transaction exists
                            let canImport =
                                match model.SyncTransactions with
                                | Success transactions ->
                                    transactions
                                    |> List.exists (fun tx ->
                                        tx.Status <> Skipped &&
                                        tx.Status <> Imported)
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
                                        transactionRow tx categoryOptions model.ExpandedTransactionIds model.InlineRuleForm model.ManuallyCategorizedIds dispatch
                                ]
                            ]
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
                    tanWaitingView model.IsTanConfirming dispatch

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
