module Components.SyncFlow.Types

open Shared.Domain
open Shared.Api
open Types

/// A single editable split line in the split sheet. The amount is held as text so
/// the user can type partial input as a POSITIVE magnitude; the editor applies the
/// transaction's sign when building the domain split (ynab-003). The target is a
/// category or a transfer account (XOR, mirroring `SplitTarget`). Empty `Target` =
/// a line whose target is not yet chosen (the picker has not committed yet).
type SplitDraftLine = {
    /// The chosen target, if any. `None` until the user picks a category/account.
    Target: SplitTarget option
    /// Raw amount input as a positive magnitude (German comma or dot), parsed
    /// leniently via `parseSplitAmount`. The sign is applied from the total.
    AmountText: string
    /// Optional per-line memo.
    Memo: string
}

/// Which picker (if any) is layered over the split form sheet, and for which line.
/// Only one is open at a time, one level deep (ADR 0005 §4).
type SplitPicker =
    | NoPicker
    /// Category picker open for the draft line at this index.
    | CategoryPickerFor of lineIndex: int
    /// Transfer-account picker open for the draft line at this index.
    | AccountPickerFor of lineIndex: int

/// Split editing state for a single transaction. The editor is a FORM (explicit
/// Save/Cancel), not a click-commit picker — the layered category/account picker
/// commits on click, but the form persists only on Save (ADR 0005, ynab-002).
type SplitEditState = {
    TransactionId: TransactionId
    /// The full transaction amount being split (sign-carrying).
    Total: Money
    /// The editable draft lines (≥2 required to save).
    Lines: SplitDraftLine list
    Currency: string
    /// The layered picker currently open over the form, if any.
    ActivePicker: SplitPicker
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

// Quick Add form state (`QuickAddFormState`) and its pure helpers
// (`parseAmountInput`, `buildQuickAddRequest`) were lifted to the top-level
// `Types` module (ynab-q7k3m) — Quick Add is now a standalone page, no longer a
// sub-state of this component. `parseAmountInput` (still used by `parseSplitAmount`
// below) lives in `Types`, which this module already opens.

// ============================================
// Split editor — pure logic (ynab-002)
// ============================================
// All split arithmetic and validation flows through the shared `ynab-001`
// domain helpers (`splitRemainder`, `mkSplits`, `buildCashbackSplit`). The
// invariant is NEVER reimplemented here — these helpers only adapt the draft
// form state to those domain functions so the UI can show a live remainder and
// gate the Save button (ADR 0006).

/// Formats a decimal for prefilling an amount text field as a POSITIVE magnitude
/// (invariant "0.00" shape, dot decimal). The sign is applied from the total when
/// the split is built (`draftLineToSplit`), so the field only ever shows a
/// magnitude — matching what the user types (ynab-003).
let formatAmountForEdit (amount: decimal) : string =
    if amount = 0m then ""
    else (abs amount).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)

/// Parses a split-line amount, accepting German comma decimals ("17,50") and
/// "17.50", with an optional leading minus for outflow lines. Empty/blank input
/// parses as 0 (an as-yet-unfilled line contributes nothing to the remainder).
/// Returns `None` only for genuinely malformed input.
let parseSplitAmount (text: string) : decimal option =
    if System.String.IsNullOrWhiteSpace text then Some 0m
    else
        let trimmed = text.Trim()
        if trimmed.StartsWith("-") then
            parseAmountInput (trimmed.Substring(1)) |> Option.map (fun a -> -a)
        else
            parseAmountInput trimmed

/// The positive magnitude a draft line contributes, regardless of target. Blank
/// or malformed input contributes 0. Used for the live remainder preview and the
/// per-line "Rest" button (presentation), not for the saveable invariant.
let lineMagnitude (line: SplitDraftLine) : decimal =
    parseSplitAmount line.AmountText |> Option.map abs |> Option.defaultValue 0m

/// The sign every split line moves money in — matched to the transaction total
/// (negative for an outflow). The user types positive magnitudes; this applies
/// the direction so the signed sum can equal the (often negative) total. Sign is
/// uniform across lines; mixed-sign splits are out of scope (ynab-003).
let private totalSign (total: Money) : decimal =
    if total.Amount < 0m then -1m else 1m

/// Converts a draft line to a domain `TransactionSplit`, applying the total's
/// sign to the user's positive magnitude. Returns `None` for an incomplete line
/// (no target / unparseable amount).
let draftLineToSplit (total: Money) (line: SplitDraftLine) : TransactionSplit option =
    match line.Target, parseSplitAmount line.AmountText with
    | Some target, Some a ->
        Some {
            Target = target
            Amount = { Amount = (totalSign total) * abs a; Currency = total.Currency }
            Memo = if System.String.IsNullOrWhiteSpace line.Memo then None else Some (line.Memo.Trim())
        }
    | _ -> None

/// The committed splits derivable from the current draft lines (those with a
/// chosen target), each carrying the total's sign applied to its magnitude.
let committedSplits (state: SplitEditState) : TransactionSplit list =
    state.Lines |> List.choose (draftLineToSplit state.Total)

/// The amount still unallocated, as a POSITIVE magnitude: |Total| − Σ entered
/// magnitudes across ALL lines (targeted or not). Positive = still to allocate,
/// 0 = balanced, negative = over-allocated. This is a presentation helper for the
/// remainder banner; the saveable invariant itself stays in `mkSplits`
/// (`validateSplitEdit`) and is never reimplemented (ADR 0006).
let splitEditRemainder (state: SplitEditState) : decimal =
    (abs state.Total.Amount) - (state.Lines |> List.sumBy lineMagnitude)

/// The positive magnitude to place on line `index` so the split balances:
/// |Total| − Σ(other lines' magnitudes), clamped at 0 (never suggests a negative
/// amount). Backs the per-line "Rest" button (ynab-003).
let restMagnitudeForLine (index: int) (state: SplitEditState) : decimal =
    let others =
        state.Lines
        |> List.indexed
        |> List.sumBy (fun (i, l) -> if i = index then 0m else lineMagnitude l)
    max 0m ((abs state.Total.Amount) - others)

/// Validates the current draft via the shared `mkSplits` smart-constructor.
/// Returns the validated splits on success, or the `SplitError` explaining why
/// the split is not yet saveable (too few lines / sum mismatch / currency mix).
let validateSplitEdit (state: SplitEditState) : Result<TransactionSplit list, SplitError> =
    mkSplits state.Total (committedSplits state)

/// Whether the Save button should be enabled: only when `mkSplits` accepts the
/// current draft AND every draft line has a chosen target (no half-filled rows).
let canSaveSplits (state: SplitEditState) : bool =
    let allLinesTargeted = state.Lines |> List.forall (fun l -> l.Target.IsSome)
    allLinesTargeted &&
    (match validateSplitEdit state with Ok _ -> true | Error _ -> false)

/// SyncFlow-specific model state
type Model = {
    CurrentSession: RemoteData<SyncSession option>
    SyncTransactions: RemoteData<SyncTransaction list>
    Categories: YnabCategory list
    /// YNAB accounts (for the split transfer-target picker, filtered to open
    /// on-budget via `openOnBudgetAccounts`).
    Accounts: YnabAccount list
    /// The configured Quick-Add account id (`ynab_quickadd_account_id`, ADR 0004),
    /// reused as the default transfer target for the cashback shortcut (ynab-002).
    QuickAddAccountId: YnabAccountId option
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
    /// Start the split editor pre-filled with a cashback shortcut: the original
    /// category line + a transfer line to the (configured Quick-Add) cash account
    /// taking the remainder (ADR 0004, ynab-002).
    | StartCashbackSplit of TransactionId
    | CancelSplitEdit
    /// Append an empty draft line to the editor.
    | AddSplitLine
    | RemoveSplit of int  // index
    /// Fill the draft line at index with the remaining amount so the split
    /// balances (the per-line "Rest" button, ynab-003).
    | FillSplitRemainder of int
    /// Update the raw amount text of the draft line at index.
    | UpdateSplitAmountText of int * string
    | UpdateSplitMemo of int * string option  // index, memo
    /// Open/close the layered category or account picker over the split form.
    | OpenSplitCategoryPicker of int
    | OpenSplitAccountPicker of int
    | CloseSplitPicker
    /// Commit a category target onto the draft line at index (from the picker).
    | SelectSplitCategory of lineIndex: int * YnabCategoryId
    /// Commit a transfer-account target onto the draft line at index (from the picker).
    | SelectSplitAccount of lineIndex: int * YnabAccountId
    | SaveSplits
    | SplitsSaved of Result<SyncTransaction, SyncError>
    | ClearSplit of TransactionId
    | SplitCleared of Result<SyncTransaction, SyncError>
    // Account loading (for the transfer-target picker + cashback default)
    | LoadAccounts
    | AccountsLoaded of YnabAccount list * YnabAccountId option
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

/// External message to notify parent of events
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
