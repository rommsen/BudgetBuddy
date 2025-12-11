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
    | CategorizedTransactions    // Has CategoryId, not Skipped/Imported
    | UncategorizedTransactions  // No CategoryId, not Skipped/Imported
    | SkippedTransactions        // Status = Skipped
    | ConfirmedDuplicates        // DuplicateStatus = ConfirmedDuplicate

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
}

/// SyncFlow-specific model state
type Model = {
    CurrentSession: RemoteData<SyncSession option>
    SyncTransactions: RemoteData<SyncTransaction list>
    Categories: YnabCategory list
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
    | TransactionCategorized of Result<SyncTransaction, SyncError>
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

/// External message to notify parent of events
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | NavigateToDashboard
