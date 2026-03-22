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

let formatDateCompact (date: System.DateTime) =
    date.ToString("dd.MM")

let categoryText (categoryId: YnabCategoryId option) (categoryOptions: (string * string) list) =
    match categoryId with
    | Some (YnabCategoryId id) ->
        let idStr = id.ToString()
        categoryOptions
        |> List.tryFind (fun (optId, _) -> optId = idStr)
        |> Option.map snd
        |> Option.defaultValue "Unknown"
    | None -> "—"

let getRowStateClasses (tx: SyncTransaction) =
    if tx.Status = Skipped then
        "tx-row status-skipped"
    else
        match tx.DuplicateStatus with
        | ConfirmedDuplicate (_, _) -> "tx-row status-duplicate"
        | PossibleDuplicate (_, _) -> "tx-row status-duplicate"
        | NotDuplicate _ ->
            match tx.CategoryId, tx.Status with
            | Some _, _ -> "tx-row status-ready"
            | None, AutoCategorized -> "tx-row status-ready"
            | None, ManualCategorized -> "tx-row status-ready"
            | None, Imported -> "tx-row status-ready"
            | _ -> "tx-row status-attention"

let getCategoryBadgeClass (tx: SyncTransaction) =
    if tx.Status = Skipped then
        "tx-category badge-ready"
    else
        match tx.DuplicateStatus with
        | ConfirmedDuplicate (_, _) | PossibleDuplicate (_, _) -> "tx-category badge-duplicate"
        | NotDuplicate _ ->
            match tx.CategoryId with
            | Some _ -> "tx-category badge-ready"
            | None -> "tx-category badge-attention"

let titleCasePayee (name: string) =
    if System.String.IsNullOrWhiteSpace name then name
    elif name.Length <= 2 then name
    elif name = name.ToUpperInvariant() then
        name.Split(' ')
        |> Array.map (fun word ->
            if word.Length <= 2 then word
            else word.[0..0].ToUpper() + word.[1..].ToLower())
        |> String.concat " "
    else name

let private formatAmountForRow (amount: decimal) (currency: string) =
    let absAmount = abs amount
    let formattedAmount = System.Math.Round(float absAmount, 2).ToString("0.00")
    let signPrefix = if amount < 0m then "\u2212" else "+"
    let currSymbol =
        match currency with
        | "EUR" -> "\u20AC"
        | "USD" -> "$"
        | "GBP" -> "\u00A3"
        | c -> c
    let amountClass =
        if amount >= 0m then "tx-amount positive"
        else "tx-amount"
    Html.span [
        prop.className amountClass
        prop.text $"{signPrefix}{formattedAmount} {currSymbol}"
    ]

// ============================================
// Expanded Content
// ============================================

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
    (payeeOptions: Input.ComboBoxOption list)
    (expandedIds: Set<TransactionId>)
    (inlineRuleFormState: InlineRuleFormState option)
    (manuallyCategorizedIds: Set<TransactionId>)
    (isPendingCategorySave: bool)
    (isPendingPayeeSave: bool)
    (dispatch: Msg -> unit)
    (onOpenCategoryPicker: TransactionId -> string -> unit) =

    let rowClasses = getRowStateClasses tx
    let originalPayee = tx.Transaction.Payee |> Option.defaultValue ""
    let displayPayee =
        match tx.PayeeOverride with
        | Some p -> p
        | None -> originalPayee
    let displayPayeeTitleCase = titleCasePayee displayPayee
    let dateStr = formatDateCompact tx.Transaction.BookingDate
    let isExpanded = expandedIds.Contains tx.Transaction.Id
    let hasExpandableContent = not (System.String.IsNullOrWhiteSpace tx.Transaction.Memo)

    let showRuleForm =
        inlineRuleFormState
        |> Option.exists (fun f -> f.TransactionId = tx.Transaction.Id)

    let categoryDisplayText =
        match tx.DuplicateStatus with
        | ConfirmedDuplicate _ -> "Duplikat"
        | PossibleDuplicate _ -> "Duplikat?"
        | NotDuplicate _ ->
            match tx.CategoryId with
            | Some _ -> categoryText tx.CategoryId categoryOptions
            | None -> "Kategorie\u2026"

    let categoryBadgeClass = getCategoryBadgeClass tx

    let hasOrderIdSuggestion = tx.SuggestedByOrderId.IsSome

    let pendingCategorySaveIndicator =
        if isPendingCategorySave then
            Html.span [
                prop.className "ml-1 text-xs text-neon-orange animate-pulse"
                prop.title "Saving category..."
                prop.text "\u25CF"
            ]
        else
            Html.none

    let isIncluded = tx.Status <> Skipped
    let expandedClass = if isExpanded then " expanded" else ""

    let shouldShowCreateRule =
        manuallyCategorizedIds.Contains tx.Transaction.Id &&
        tx.CategoryId.IsSome &&
        not showRuleForm

    Html.div [
        prop.className $"{rowClasses}{expandedClass}"
        prop.onClick (fun _ ->
            dispatch (ToggleTransactionExpand tx.Transaction.Id))
        prop.children [
            Html.div [ prop.className "tx-status-bar" ]

            Html.div [
                prop.className "tx-content"
                prop.children [
                    // Line 1: Payee + Amount
                    Html.div [
                        prop.className "tx-line1"
                        prop.children [
                            Html.span [
                                prop.className "tx-payee"
                                prop.text (if displayPayeeTitleCase = "" then "\u2014" else displayPayeeTitleCase)
                            ]
                            formatAmountForRow tx.Transaction.Amount.Amount tx.Transaction.Amount.Currency
                        ]
                    ]

                    // Line 2: Category badge + Create Rule + Date + Chevron + Toggle
                    Html.div [
                        prop.className "tx-line2"
                        prop.children [
                            Html.button [
                                prop.className categoryBadgeClass
                                prop.onClick (fun e ->
                                    e.stopPropagation()
                                    onOpenCategoryPicker tx.Transaction.Id displayPayeeTitleCase)
                                prop.children [
                                    Html.text categoryDisplayText
                                    if hasOrderIdSuggestion then
                                        Html.span [
                                            prop.className "ml-1 text-neon-purple"
                                            prop.text "\u2606"
                                        ]
                                    pendingCategorySaveIndicator
                                ]
                            ]

                            if shouldShowCreateRule then
                                Html.button [
                                    prop.className "tx-create-rule-btn"
                                    prop.onClick (fun e ->
                                        e.stopPropagation()
                                        dispatch (OpenInlineRuleForm tx.Transaction.Id))
                                    prop.title "Regel erstellen"
                                    prop.text "+ Regel"
                                ]

                            Html.span [
                                prop.className "tx-date"
                                prop.text dateStr
                            ]

                            Html.span [
                                prop.className "tx-chevron"
                                prop.text "\u203A"
                            ]

                            Html.label [
                                prop.className "tx-toggle"
                                prop.onClick (fun e -> e.stopPropagation())
                                prop.children [
                                    Html.input [
                                        prop.type' "checkbox"
                                        prop.isChecked isIncluded
                                        prop.onChange (fun (_: bool) ->
                                            if isIncluded then
                                                dispatch (SkipTransaction tx.Transaction.Id)
                                            else
                                                dispatch (UnskipTransaction tx.Transaction.Id))
                                    ]
                                    Html.span [
                                        prop.className "toggle-track"
                                        prop.children [
                                            Svg.svg [
                                                svg.className "toggle-check"
                                                svg.viewBox (0, 0, 10, 10)
                                                svg.children [
                                                    Svg.path [
                                                        svg.d "M2 5l2.5 2.5L8 3"
                                                        svg.fill "none"
                                                        svg.stroke "#08081a"
                                                        svg.strokeWidth 2
                                                        svg.custom ("strokeLinecap", "round")
                                                        svg.custom ("strokeLinejoin", "round")
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]

                    // Expanded content
                    Html.div [
                        prop.className "tx-expanded"
                        prop.children [
                            Html.div [
                                prop.className "tx-expanded-inner"
                                prop.children [
                                    Html.div [
                                        prop.className "tx-expanded-content"
                                        prop.children [
                                            // Memo
                                            if hasExpandableContent then
                                                Html.div [
                                                    prop.className "tx-memo"
                                                    prop.children [
                                                        Html.span [ prop.className "memo-label"; prop.text "Memo" ]
                                                        Html.span [ prop.className "memo-text"; prop.text tx.Transaction.Memo ]
                                                    ]
                                                ]

                                            // Duplicate info
                                            match tx.DuplicateStatus with
                                            | ConfirmedDuplicate (reference, _) ->
                                                Html.div [
                                                    prop.className "tx-memo"
                                                    prop.children [
                                                        Html.span [ prop.className "memo-label"; prop.text "Grund" ]
                                                        Html.span [ prop.className "memo-text"; prop.text $"Bereits in YNAB vorhanden ({reference})" ]
                                                    ]
                                                ]
                                            | PossibleDuplicate (reason, _) ->
                                                Html.div [
                                                    prop.className "tx-memo"
                                                    prop.children [
                                                        Html.span [ prop.className "memo-label"; prop.text "Hinweis" ]
                                                        Html.span [ prop.className "memo-text"; prop.text reason ]
                                                    ]
                                                ]
                                            | NotDuplicate _ -> ()

                                            // Debug info (collapsible, less prominent)
                                            if isExpanded then
                                                duplicateDebugInfo tx

                                            // Action chips row
                                            Html.div [
                                                prop.className "tx-actions"
                                                prop.children [
                                                    Html.button [
                                                        prop.className "action-chip"
                                                        prop.onClick (fun e ->
                                                            e.stopPropagation()
                                                            onOpenCategoryPicker tx.Transaction.Id displayPayeeTitleCase)
                                                        prop.text (
                                                            match tx.CategoryId with
                                                            | Some _ -> "Kategorie \u00E4ndern"
                                                            | None -> "Kategorie w\u00E4hlen")
                                                    ]

                                                    if shouldShowCreateRule then
                                                        Html.button [
                                                            prop.className "action-chip"
                                                            prop.onClick (fun e ->
                                                                e.stopPropagation()
                                                                dispatch (OpenInlineRuleForm tx.Transaction.Id))
                                                            prop.text "Regel erstellen"
                                                        ]

                                                    match tx.ExternalLinks |> List.tryHead with
                                                    | Some link ->
                                                        Html.a [
                                                            prop.className "action-chip"
                                                            prop.href link.Url
                                                            prop.target "_blank"
                                                            prop.rel "noopener noreferrer"
                                                            prop.onClick (fun e -> e.stopPropagation())
                                                            prop.text link.Label
                                                        ]
                                                    | None -> ()

                                                    if tx.Status <> Skipped then
                                                        Html.button [
                                                            prop.className "action-chip danger"
                                                            prop.onClick (fun e ->
                                                                e.stopPropagation()
                                                                dispatch (SkipTransaction tx.Transaction.Id))
                                                            prop.text "\u00DCberspringen"
                                                        ]
                                                ]
                                            ]

                                            // Payee edit (styled to match new design)
                                            if tx.Status <> Skipped then
                                                Html.div [
                                                    prop.className "tx-payee-edit"
                                                    prop.onClick (fun e -> e.stopPropagation())
                                                    prop.children [
                                                        Input.comboBoxGrouped
                                                            displayPayee
                                                            (fun value ->
                                                                dispatch (SetPayeeOverride (tx.Transaction.Id, Some value)))
                                                            "Payee bearbeiten..."
                                                            payeeOptions
                                                    ]
                                                ]

                                            // Inline rule form (BottomSheet via portal)
                                            match inlineRuleFormState with
                                            | Some form when form.TransactionId = tx.Transaction.Id ->
                                                inlineRuleForm form dispatch
                                            | _ -> ()
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
