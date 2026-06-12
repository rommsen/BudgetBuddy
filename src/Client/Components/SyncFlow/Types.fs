module Components.SyncFlow.Types

open Shared.Domain
open Shared.Api
open Types

/// Split editing state for a single transaction
type SplitEditState = {
    TransactionId: TransactionId
    Splits: TransactionSplit list
    RemainingAmount: decimal
    Currency: string
}

/// Filter options for transaction list
type TransactionFilter =
    | AllTransactions
    | ToBeImported               // Status <> Skipped && Status <> Imported
    | CategorizedTransactions    // Has CategoryId, not Skipped/Imported
    | UncategorizedTransactions  // No CategoryId, not Skipped/Imported
    | SkippedTransactions        // Status = Skipped
    | ConfirmedDuplicates        // DuplicateStatus = ConfirmedDuplicate        // DuplicateStatus = ConfirmedDuplicate

/// State for inline rule creation from a transaction
type InlineRuleFormState = {
    TransactionId: TransactionId
    /// Pre-filled from transaction payee
    Pattern: string
    /// Default: Contains
    PatternType: PatternType
    /// Default: Combined
    TargetField: TargetField
    /// Pre-filled from selected category
    CategoryId: YnabCategoryId
    /// The category name for display
    CategoryName: string
    /// Optional payee override
    PayeeOverride: string
    /// Auto-generated from pattern
    RuleName: string
    /// Is the rule being saved?
    IsSaving: bool
    /// Transaction text (payee + memo) for auto-testing pattern match
    TransactionText: string
}

/// Form state for the Quick Add sheet (manual transaction entry → YNAB)
type QuickAddFormState = {
    /// Raw user input; parsed leniently (both "4,50" and "4.50" are valid)
    AmountText: string
    /// true = expense (default), false = income
    IsOutflow: bool
    Payee: string
    /// Selected category id as string; "" = no category
    CategoryId: string
    /// ISO date (yyyy-MM-dd), as produced by <input type="date">
    DateText: string
    Memo: string
    /// Category picker sheet open on top of the Quick Add sheet
    ShowCategoryPicker: bool
    IsSaving: bool
    Error: string option
}

/// Parses a user-entered amount, accepting German comma decimals ("4,50")
/// as well as "4.50". Hand-rolled to behave identically under .NET and Fable
/// (TryParse overloads are culture-dependent on .NET but not in JS).
/// Accepts at most 2 decimal places and amounts below 1 billion.
let parseAmountInput (text: string) : decimal option =
    let normalized = (text: string).Trim().Replace(" ", "").Replace(",", ".")

    let isDigitsOnly (s: string) =
        s.Length > 0 && s |> Seq.forall System.Char.IsDigit

    match normalized.Split('.') with
    | [| whole |] when isDigitsOnly whole && whole.Length <= 9 ->
        Some (decimal (int whole))
    | [| whole; frac |] when isDigitsOnly whole && whole.Length <= 9 && isDigitsOnly frac && frac.Length <= 2 ->
        let fracPadded = frac.PadRight(2, '0')
        Some (decimal (int whole) + decimal (int fracPadded) / 100m)
    | _ -> None

/// Builds the API request from the Quick Add form. Pure — unit-testable.
let buildQuickAddRequest (form: QuickAddFormState) : Result<ManualTransactionRequest, string> =
    match parseAmountInput form.AmountText with
    | None -> Error "Bitte einen gültigen Betrag eingeben"
    | Some amount when amount <= 0m -> Error "Der Betrag muss größer als 0 sein"
    | Some amount ->
        // Payee is optional — YNAB allows payee-less transactions
        match System.DateTime.TryParse(form.DateText) with
        | false, _ -> Error "Bitte ein gültiges Datum wählen"
        | true, date ->
            let categoryId =
                match System.Guid.TryParse(form.CategoryId) with
                | true, guid -> Some (YnabCategoryId guid)
                | false, _ -> None

            Ok {
                Amount = amount
                IsOutflow = form.IsOutflow
                PayeeName = form.Payee.Trim()
                CategoryId = categoryId
                Date = date
                Memo =
                    if System.String.IsNullOrWhiteSpace form.Memo then None
                    else Some (form.Memo.Trim())
            }

/// SyncFlow-specific model state
type Model = {
    CurrentSession: RemoteData<SyncSession option>
    SyncTransactions: RemoteData<SyncTransaction list>
    Categories: YnabCategory list
    /// YNAB payees for payee dropdown in transaction editing
    Payees: YnabPayee list
    /// Active split editing state (None when not editing splits)
    SplitEdit: SplitEditState option
    /// Transaction IDs that were rejected as duplicates by YNAB
    DuplicateTransactionIds: TransactionId list
    /// Whether TAN confirmation is in progress (prevents double-clicks)
    IsTanConfirming: bool
    /// Transaction IDs with expanded details (showing memo)
    ExpandedTransactionIds: Set<TransactionId>
    /// Active inline rule creation form (None when not creating rule)
    InlineRuleForm: InlineRuleFormState option
    /// Set of transaction IDs that have been manually categorized (show "Create Rule" button)
    ManuallyCategorizedIds: Set<TransactionId>
    /// Active filter for the transaction list
    ActiveFilter: TransactionFilter
    /// Version counter per transaction for debouncing category changes.
    /// Used to ensure only the latest change triggers an API call.
    PendingCategoryVersions: Map<TransactionId, int>
    /// Version counter per transaction for debouncing payee changes.
    PendingPayeeVersions: Map<TransactionId, int>
    /// Recently used category IDs, most recent first (max 10)
    RecentlyUsedCategoryIds: YnabCategoryId list
    /// Quick Add form (None when the sheet is closed)
    QuickAdd: QuickAddFormState option
}

/// SyncFlow-specific messages
type Msg =
    | LoadCurrentSession
    | CurrentSessionLoaded of SyncSession option
    | StartSync
    | SyncStarted of Result<SyncSession, SyncError>
    | InitiateComdirectAuth
    | ComdirectAuthInitiated of Result<string, SyncError>
    | ConfirmTan
    | TanConfirmed of Result<unit, SyncError>
    | LoadTransactions
    | TransactionsLoaded of Result<SyncTransaction list, SyncError>
    | CategorizeTransaction of TransactionId * YnabCategoryId option
    | TransactionCategorized of Result<SyncTransaction list, SyncError>
    /// Debounced category change commit - only triggers API call if version matches
    | CommitCategoryChange of TransactionId * YnabCategoryId option * int
    | SkipTransaction of TransactionId
    | TransactionSkipped of Result<SyncTransaction, SyncError>
    | UnskipTransaction of TransactionId
    | TransactionUnskipped of Result<SyncTransaction, SyncError>
    // Bulk skip/unskip
    | SkipAllVisible
    | UnskipAllVisible
    | BulkSkipCompleted of Result<SyncTransaction list, SyncError>
    | BulkUnskipCompleted of Result<SyncTransaction list, SyncError>
    // Split transaction messages
    | StartSplitEdit of TransactionId
    | CancelSplitEdit
    | AddSplit of YnabCategoryId * string * decimal  // categoryId, categoryName, amount
    | RemoveSplit of int  // index
    | UpdateSplitAmount of int * decimal  // index, amount
    | UpdateSplitMemo of int * string option  // index, memo
    | SaveSplits
    | SplitsSaved of Result<SyncTransaction, SyncError>
    | ClearSplit of TransactionId
    | SplitCleared of Result<SyncTransaction, SyncError>
    // Import
    | ImportToYnab
    | ImportCompleted of Result<ImportResult, SyncError>
    // Force re-import duplicates
    | ForceImportDuplicates
    | ForceImportCompleted of Result<int, SyncError>
    | CancelSync
    | SyncCancelled of Result<unit, SyncError>
    | LoadCategories
    | CategoriesLoaded of Result<YnabCategory list, YnabError>
    // Payee loading and editing
    | LoadPayees
    | PayeesLoaded of Result<YnabPayee list, YnabError>
    /// Set payee override for a transaction (optimistic update + debounce)
    | SetPayeeOverride of TransactionId * string option
    /// Debounced payee change commit - only triggers API call if version matches
    | CommitPayeeChange of TransactionId * string option * int
    // UI interactions
    | ToggleTransactionExpand of TransactionId
    | SetFilter of TransactionFilter
    // Inline rule creation
    | OpenInlineRuleForm of TransactionId
    | CloseInlineRuleForm
    | UpdateInlineRulePattern of string
    | UpdateInlineRulePatternType of PatternType
    | UpdateInlineRuleTargetField of TargetField
    | UpdateInlineRulePayeeOverride of string
    | UpdateInlineRuleName of string
    | SaveInlineRule
    | InlineRuleSaved of Result<Rule, RulesError>
    | ApplyNewRuleToTransactions of Rule
    | TransactionsUpdatedByRule of Result<SyncTransaction list, SyncError>
    // Quick Add (manual transaction entry → YNAB)
    | OpenQuickAdd
    | CloseQuickAdd
    | UpdateQuickAdd of QuickAddFormState
    | SubmitQuickAdd
    | QuickAddSaved of Result<unit, string>

/// External message to notify parent of events
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
