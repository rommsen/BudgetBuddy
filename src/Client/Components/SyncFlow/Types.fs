module Components.SyncFlow.Types

open Shared.Domain
open Shared.Api
open Types

/// A single editable split line in the split sheet. The amount is held as text so
/// the user can type partial input; the target is a category or a transfer account
/// (XOR, mirroring `SplitTarget`). Empty `Target` = a line whose target is not yet
/// chosen (the picker has not committed yet).
type SplitDraftLine = {
    /// The chosen target, if any. `None` until the user picks a category/account.
    Target: SplitTarget option
    /// Raw amount input (German comma or dot), parsed leniently via `parseSplitAmount`.
    AmountText: string
    /// Optional per-line memo.
    Memo: string
    /// When true, this line's amount is NOT user-entered but always equals the
    /// live remainder of the other lines (the "Rest" line). This is what makes
    /// the cashback shortcut work: the user types only the transfer amount, and
    /// the category line auto-absorbs the rest (AC 2, ynab-002).
    AutoRemainder: bool
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

// ============================================
// Split editor — pure logic (ynab-002)
// ============================================
// All split arithmetic and validation flows through the shared `ynab-001`
// domain helpers (`splitRemainder`, `mkSplits`, `buildCashbackSplit`). The
// invariant is NEVER reimplemented here — these helpers only adapt the draft
// form state to those domain functions so the UI can show a live remainder and
// gate the Save button (ADR 0006).

/// Formats a signed decimal for prefilling an amount text field. Uses an
/// invariant "0.00" shape (dot decimal); the lenient `parseSplitAmount` accepts
/// it back. Negative amounts keep their sign so an outflow line round-trips.
let formatAmountForEdit (amount: decimal) : string =
    if amount = 0m then ""
    else (abs amount).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
         |> fun s -> if amount < 0m then "-" + s else s

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

/// Converts a draft line to a domain `TransactionSplit`. For an auto-remainder
/// line, `amountOverride` carries the computed remainder. Returns `None` for an
/// incomplete line (no target / unparseable user amount).
let draftLineToSplitWith (currency: string) (amountOverride: decimal option) (line: SplitDraftLine) : TransactionSplit option =
    let amount =
        if line.AutoRemainder then amountOverride
        else parseSplitAmount line.AmountText
    match line.Target, amount with
    | Some target, Some a ->
        Some {
            Target = target
            Amount = { Amount = a; Currency = currency }
            Memo = if System.String.IsNullOrWhiteSpace line.Memo then None else Some (line.Memo.Trim())
        }
    | _ -> None

/// The remainder that an auto ("Rest") line absorbs: total minus the sum of all
/// the user-entered (non-auto, targeted) lines. Delegates to the shared
/// `splitRemainder` (ADR 0006). When there is no auto line this is also the live
/// "still unallocated" amount surfaced to the user.
let autoRemainderAmount (state: SplitEditState) : decimal =
    let userLines =
        state.Lines
        |> List.filter (fun l -> not l.AutoRemainder)
        |> List.choose (draftLineToSplitWith state.Currency None)
    splitRemainder state.Total userLines

/// The committed splits derivable from the current draft lines (those with a
/// chosen target). The auto-remainder line, if present, takes the remainder of
/// the user-entered lines so the user only types the transfer amount (AC 2).
let committedSplits (state: SplitEditState) : TransactionSplit list =
    let auto = autoRemainderAmount state
    state.Lines |> List.choose (draftLineToSplitWith state.Currency (Some auto))

/// The amount still unallocated: total − Σ committed lines. Delegates to the
/// shared `splitRemainder` so the client never reimplements the sum (ADR 0006).
/// With an auto-remainder line present this is 0 once that line has a target
/// (the line absorbs the rest); otherwise it is the live unallocated amount.
let splitEditRemainder (state: SplitEditState) : decimal =
    splitRemainder state.Total (committedSplits state)

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
    /// Start the split editor pre-filled with a cashback shortcut: the original
    /// category line + a transfer line to the (configured Quick-Add) cash account
    /// taking the remainder (ADR 0004, ynab-002).
    | StartCashbackSplit of TransactionId
    | CancelSplitEdit
    /// Append an empty draft line to the editor.
    | AddSplitLine
    | RemoveSplit of int  // index
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
