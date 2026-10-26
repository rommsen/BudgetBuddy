module Components.SyncFlow.Views.TransactionRow

open Feliz
open Components.SyncFlow.Types
open Types
open Shared.Domain
open Client.DesignSystem
open Client.DesignSystem.Tokens
open Components.SyncFlow.Views.InlineRuleForm
open Components.SyncFlow.Views.ViewHelpers

// ============================================
// Helper Functions
// ============================================

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

/// Kategorie-Chip (ADR 0049): leise und neutral, nur "ohne Kategorie" ist ein offener
/// Fall und bleibt Orange. Die Zeile meldet ihren Zustand über die Statuskante.
let getCategoryBadgeClass (tx: SyncTransaction) =
    if tx.Status = Skipped then
        "tx-category badge-neutral"
    else
        match tx.DuplicateStatus with
        | ConfirmedDuplicate (_, _) | PossibleDuplicate (_, _) -> "tx-category badge-duplicate"
        | NotDuplicate _ ->
            // A split transaction is a completed/ready state (it has no single
            // category), so it gets the ready badge — not the orange "attention"
            // badge that an uncategorized transaction shows (ynab-004).
            match tx.Splits with
            | Some splits when not (List.isEmpty splits) -> "tx-category badge-neutral"
            | _ ->
                match tx.CategoryId with
                | Some _ -> "tx-category badge-neutral"
                | None -> "tx-category badge-attention"

/// The label shown in a transaction's category chip. A split transaction (Splits
/// set) reads as "Aufgeteilt" so a completed split is visually distinct from an
/// untouched, uncategorized transaction — which would otherwise both show the
/// "Kategorie…" placeholder because a split has no single CategoryId (ynab-004).
let categoryChipLabel (categoryOptions: (string * string) list) (tx: SyncTransaction) : string =
    match tx.DuplicateStatus with
    | ConfirmedDuplicate _ -> "Duplikat"
    | PossibleDuplicate _ -> "Duplikat?"
    | NotDuplicate _ ->
        match tx.Splits with
        | Some splits when not (List.isEmpty splits) -> "Aufgeteilt"
        | _ ->
            match tx.CategoryId with
            | Some _ -> categoryText tx.CategoryId categoryOptions
            | None -> "Kategorie…"

// ============================================
// Expanded Content
// ============================================

/// Sichtbarkeit der Duplikat-Prüfung in der aufgeklappten Zeile. Der Status allein
/// reicht: jeder Referenz- oder Import-ID-Treffer ist ein ConfirmedDuplicate, jeder
/// Fuzzy-Treffer ein PossibleDuplicate, NotDuplicate trägt nie einen Treffer.
type DuplicateDetailsMode =
    | Shown
    | BehindLink

let duplicateDetailsMode (status: DuplicateStatus) : DuplicateDetailsMode =
    match status with
    | ConfirmedDuplicate _ | PossibleDuplicate _ -> Shown
    | NotDuplicate _ -> BehindLink

/// Text des Fuzzy-Treffers: "Gefunden: Rewe am 07.10.2026 über 12,34 €".
/// Ohne Datum oder Betrag gibt es keinen Fuzzy-Treffer (None).
let fuzzyMatchText (details: DuplicateDetectionDetails) : string option =
    match details.FuzzyMatchDate, details.FuzzyMatchAmount with
    | Some date, Some amount ->
        let payee = details.FuzzyMatchPayee |> Option.defaultValue "unbekannter Empfänger"
        let amountText = Money.formatAmountText false true (abs amount) "EUR"
        let dateText = date.ToString "dd.MM.yyyy"
        Some $"Gefunden: {payee} am {dateText} über {amountText}"
    | _ -> None

/// Das Ergebnis des YNAB-Imports wird angezeigt, sobald ein Import versucht wurde,
/// unabhängig vom Duplikat-Verdacht.
let showYnabResult (status: YnabImportStatus) : bool =
    match status with
    | NotAttempted -> false
    | YnabImported | RejectedByYnab _ -> true

let private ynabResultView (tx: SyncTransaction) =
    if not (showYnabResult tx.YnabImportStatus) then Html.none else
    match tx.YnabImportStatus with
    | NotAttempted -> Html.none
    | YnabImported ->
        Html.div [
            prop.className $"mt-2 flex items-center gap-2 {ListRoles.secondary}"
            prop.children [
                Icons.checkCircle Icons.XS Icons.Default
                Html.span [ prop.text "YNAB: erfolgreich importiert" ]
            ]
        ]
    | RejectedByYnab reason ->
        let rejectionText =
            match reason with
            | DuplicateImportId id -> $"YNAB hat abgelehnt: doppelte Import-ID ({id})"
            | UnknownRejection msg ->
                let grund = msg |> Option.defaultValue "unbekannter Grund"
                $"YNAB hat abgelehnt: {grund}"
        Html.div [
            prop.className $"mt-2 flex items-center gap-2 flex-wrap {ListRoles.secondary}"
            prop.children [
                Icons.xCircle Icons.XS Icons.Error
                Html.span [ prop.text rejectionText ]
                match tx.DuplicateStatus with
                | NotDuplicate _ -> Html.span [ prop.text "(BudgetBuddy hat das nicht erkannt)" ]
                | _ -> Html.none
            ]
        ]

let private foundBadge (text: string) =
    Html.span [ prop.className ListRoles.secondary; prop.text text ]

let private duplicateCheckView (details: DuplicateDetectionDetails) =
    Html.div [
        prop.className $"mt-3 space-y-1 {ListRoles.muted}"
        prop.children [
            Html.div [ prop.className ListRoles.secondary; prop.text "Duplikat-Prüfung" ]
            Html.div [
                prop.className "flex items-center gap-2 flex-wrap"
                prop.children [
                    Html.span [ prop.text "Referenz" ]
                    Html.code [ prop.className "font-mono"; prop.text details.TransactionReference ]
                    foundBadge (if details.ReferenceFoundInYnab then "In YNAB gefunden" else "Nicht in YNAB")
                ]
            ]
            Html.div [
                prop.className "flex items-center gap-2"
                prop.children [
                    Html.span [ prop.text "Import-ID" ]
                    foundBadge (if details.ImportIdFoundInYnab then "In YNAB gefunden" else "Neu")
                ]
            ]
            match fuzzyMatchText details with
            | Some text -> Html.div [ prop.className ListRoles.secondary; prop.text text ]
            | None -> Html.none
        ]
    ]

/// Bei Verdacht sofort sichtbar, sonst hinter dem gedämpften Link "Details".
[<ReactComponent>]
let private DuplicateDetailsBlock (status: DuplicateStatus) =
    let isOpen, setIsOpen = React.useState false
    let details = getDuplicateDetails status
    match duplicateDetailsMode status with
    | Shown -> duplicateCheckView details
    | BehindLink ->
        Html.div [
            prop.className "mt-2"
            prop.children [
                Html.button [
                    prop.className $"{ListRoles.muted} underline"
                    prop.onClick (fun e ->
                        e.stopPropagation()
                        setIsOpen (not isOpen))
                    prop.text "Details"
                ]
                if isOpen then duplicateCheckView details
            ]
        ]

// ============================================
// Main Transaction Row Component
// ============================================

type TransactionRowProps = {
    Transaction: SyncTransaction
    CategoryOptions: (string * string) list
    PayeeOptions: Input.ComboBoxOption list
    ExpandedIds: Set<TransactionId>
    InlineRuleFormState: InlineRuleFormState option
    ManuallyCategorizedIds: Set<TransactionId>
    IsPendingCategorySave: bool
    Dispatch: Msg -> unit
    OnOpenCategoryPicker: TransactionId -> string -> unit
    /// Die Lena-Flag-Farbe aus den Settings; ohne sie ist "Lena fragen" gesperrt.
    LenaFlagColor: string option
}

let transactionRow (props: TransactionRowProps) =
    let tx = props.Transaction
    let categoryOptions = props.CategoryOptions
    let payeeOptions = props.PayeeOptions
    let expandedIds = props.ExpandedIds
    let inlineRuleFormState = props.InlineRuleFormState
    let manuallyCategorizedIds = props.ManuallyCategorizedIds
    let isPendingCategorySave = props.IsPendingCategorySave
    let dispatch = props.Dispatch
    let onOpenCategoryPicker = props.OnOpenCategoryPicker
    let askLenaAction = askLenaActionFor props.LenaFlagColor tx

    let rowClasses = getRowStateClasses tx
    let originalPayee = tx.Transaction.Payee |> Option.defaultValue ""
    let displayPayee =
        match tx.PayeeOverride with
        | Some p -> p
        | None -> originalPayee
    let displayPayeeTitleCase = titleCasePayee displayPayee
    let isExpanded = expandedIds.Contains tx.Transaction.Id
    let hasExpandableContent = not (System.String.IsNullOrWhiteSpace tx.Transaction.Memo)

    let showRuleForm =
        inlineRuleFormState
        |> Option.exists (fun f -> f.TransactionId = tx.Transaction.Id)

    let categoryDisplayText = categoryChipLabel categoryOptions tx

    let categoryBadgeClass = getCategoryBadgeClass tx

    let hasOrderIdSuggestion = tx.SuggestedByOrderId.IsSome

    let pendingCategorySaveIndicator =
        if isPendingCategorySave then
            Html.span [
                prop.className "ml-1 text-xs text-neon-orange animate-pulse"
                prop.title "Kategorie wird gespeichert..."
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

    let rowElement = Html.div [
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
                            Html.span [
                                prop.className "tx-amount"
                                prop.children [
                                    Money.listAmount tx.Transaction.Amount.Amount tx.Transaction.Amount.Currency
                                ]
                            ]
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

                            // Kennzeichen der Rückfrage Lena, im Wortlaut des Kassenbuchs
                            // (ynab-sync-d9e51). Nur bei gesetzter Rückfrage, sonst bleibt die
                            // ruhige Liste (ADR 0049) unverändert.
                            if tx.AskLena then
                                Badge.view {
                                    Badge.defaultProps with
                                        Text = "Rückfrage Lena"
                                        Variant = Badge.Purple
                                        Size = Badge.Small
                                }

                            if shouldShowCreateRule then
                                Html.button [
                                    prop.className "tx-create-rule-btn"
                                    prop.onClick (fun e ->
                                        e.stopPropagation()
                                        dispatch (OpenInlineRuleForm tx.Transaction.Id))
                                    prop.title "Regel erstellen"
                                    prop.text "+ Regel"
                                ]

                            // External link (Amazon/PayPal) - visible in compact row
                            match tx.ExternalLinks |> List.tryHead with
                            | Some link ->
                                Html.a [
                                    prop.className "tx-external-link"
                                    prop.href link.Url
                                    prop.target "_blank"
                                    prop.rel "noopener noreferrer"
                                    prop.onClick (fun e -> e.stopPropagation())
                                    prop.title link.Label
                                    prop.children [ Icons.externalLink Icons.XS Icons.Default ]
                                ]
                            | None -> ()

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
                                            // Deliberately raw (not Icons.check): CSS-baked 10x10-viewBox micro-glyph
                                            // positioned/sized by the .toggle-check CSS; lifting to the DS 24-viewBox
                                            // Icons.check would be CSS rework, not a 1:1 visual swap (design-system-007).
                                            Svg.svg [
                                                svg.className "toggle-check"
                                                svg.viewBox (0, 0, 10, 10)
                                                svg.children [
                                                    Svg.path [
                                                        svg.d "M2 5l2.5 2.5L8 3"
                                                        svg.fill "none"
                                                        svg.stroke "currentColor"
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
                                            | PossibleDuplicate (_, details) ->
                                                Html.div [
                                                    prop.className "tx-memo"
                                                    prop.children [
                                                        Html.span [ prop.className "memo-label"; prop.text "Hinweis" ]
                                                        Html.span [
                                                            prop.className "memo-text"
                                                            prop.text (fuzzyMatchText details |> Option.defaultValue "Ähnliche Buchung in YNAB")
                                                        ]
                                                    ]
                                                ]
                                            | NotDuplicate _ -> ()

                                            // Duplikat-Prüfung (bei Verdacht sichtbar, sonst hinter "Details")
                                            // und YNAB-Ergebnis (immer, wenn ein Import versucht wurde)
                                            if isExpanded then
                                                DuplicateDetailsBlock tx.DuplicateStatus
                                                ynabResultView tx

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

                                                    // Split actions (ynab-002): cashback shortcut first
                                                    // (the ~80% case), generic editor next.
                                                    if tx.Status <> Skipped then
                                                        Html.button [
                                                            prop.className "action-chip"
                                                            prop.onClick (fun e ->
                                                                e.stopPropagation()
                                                                dispatch (StartCashbackSplit tx.Transaction.Id))
                                                            prop.text "Barabhebung"
                                                        ]
                                                        Html.button [
                                                            prop.className "action-chip"
                                                            prop.onClick (fun e ->
                                                                e.stopPropagation()
                                                                dispatch (StartSplitEdit tx.Transaction.Id))
                                                            prop.text "Aufteilen"
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

                                                    // Force re-import for duplicates (e.g. deleted in YNAB)
                                                    match tx.DuplicateStatus with
                                                    | ConfirmedDuplicate _ | PossibleDuplicate _ ->
                                                        Html.button [
                                                            prop.className "action-chip"
                                                            prop.onClick (fun e ->
                                                                e.stopPropagation()
                                                                dispatch ForceImportDuplicates)
                                                            prop.children [
                                                                Icons.sync Icons.XS Icons.NeonTeal
                                                                Html.span [ prop.text "Erneut importieren" ]
                                                            ]
                                                        ]
                                                    | _ -> ()

                                                    // Lena fragen (ynab-sync-d9e51): gesperrt ohne Flag-Farbe,
                                                    // mit sichtbarem Grund.
                                                    match askLenaAction with
                                                    | Some action ->
                                                        Html.button [
                                                            prop.className "action-chip"
                                                            prop.disabled action.DisabledReason.IsSome
                                                            prop.title (action.DisabledReason |> Option.defaultValue action.Label)
                                                            prop.onClick (fun e ->
                                                                e.stopPropagation()
                                                                if action.DisabledReason.IsNone then
                                                                    dispatch (SetAskLena (tx.Transaction.Id, action.Target)))
                                                            prop.text action.Label
                                                        ]
                                                        match action.DisabledReason with
                                                        | Some blockedBecause ->
                                                            Html.span [
                                                                prop.className $"{ListRoles.muted} self-center"
                                                                prop.text blockedBecause
                                                            ]
                                                        | None -> ()
                                                    | None -> ()

                                                    if tx.Status <> Skipped then
                                                        Html.button [
                                                            prop.className "action-chip"
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

    // Swipe left to skip (or to re-include when already skipped). The toggle
    // and the action chips remain the accessible, non-gesture path.
    Swipe.SwipeableRow {
        ActionLabel = (if isIncluded then "Überspringen" else "Einschließen")
        ActionClass = (if isIncluded then "skip" else "include")
        OnCommit =
            (fun () ->
                if isIncluded then dispatch (SkipTransaction tx.Transaction.Id)
                else dispatch (UnskipTransaction tx.Transaction.Id))
        Children = rowElement
    }
