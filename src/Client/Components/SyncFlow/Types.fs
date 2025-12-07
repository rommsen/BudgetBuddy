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

/// SyncFlow-specific model state
type Model = {
    CurrentSession: RemoteData<SyncSession option>
    SyncTransactions: RemoteData<SyncTransaction list>
    Categories: YnabCategory list
    /// Active split editing state (None when not editing splits)
    SplitEdit: SplitEditState option
    /// Transaction IDs that were rejected as duplicates by YNAB
    DuplicateTransactionIds: TransactionId list
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

/// External message to notify parent of events
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | NavigateToDashboard
