module Shared.Domain

open System

// ============================================
// Value Types
// ============================================

/// Represents a monetary amount with currency.
/// Purpose: Ensures currency is always associated with amounts to prevent conversion errors.
type Money = {
    /// The numeric amount (can be negative for expenses)
    Amount: decimal

    /// ISO 4217 currency code (e.g., "EUR", "USD")
    Currency: string
}

/// Unique identifier for bank transactions from Comdirect.
/// Purpose: Prevents mixing transaction IDs with other ID types at compile time.
type TransactionId = TransactionId of string

// ============================================
// Import ID Utilities (for YNAB duplicate detection)
// ============================================

/// Import ID prefix used for YNAB transactions to prevent duplicates.
/// MUST be used in both YnabClient (generation) and DuplicateDetection (matching).
[<Literal>]
let ImportIdPrefix = "BB"

/// Generates an import ID from a transaction ID.
/// Format: "BB:{transactionId}" (max 36 chars for YNAB)
let generateImportId (TransactionId txId) : string =
    let txIdNoDashes = txId.Replace("-", "")
    $"{ImportIdPrefix}:{txIdNoDashes}"

/// Checks if an import ID matches a transaction ID.
/// Used in duplicate detection to identify transactions we previously imported.
let matchesImportId (TransactionId txId) (importId: string) : bool =
    let txIdNoDashes = txId.Replace("-", "")
    importId.StartsWith($"{ImportIdPrefix}:{txIdNoDashes}")

/// Unique identifier for categorization rules.
/// Purpose: Type-safe reference to rules in database and UI operations.
type RuleId = RuleId of Guid

/// Unique identifier for sync sessions.
/// Purpose: Tracks state across async TAN flow and multi-step sync process.
type SyncSessionId = SyncSessionId of Guid

/// YNAB budget identifier (YNAB uses string IDs for budgets).
/// Purpose: Type-safe reference to YNAB budgets, prevents ID type confusion.
type YnabBudgetId = YnabBudgetId of string

/// YNAB account identifier.
/// Purpose: Identifies target account for transaction imports.
type YnabAccountId = YnabAccountId of Guid

/// YNAB category identifier.
/// Purpose: Maps transactions to budget categories during categorization.
type YnabCategoryId = YnabCategoryId of Guid

/// YNAB payee identifier.
/// Purpose: Identifies payees for transaction editing and autocomplete.
type YnabPayeeId = YnabPayeeId of Guid

// ============================================
// Bank Transaction (from Comdirect)
// ============================================

type BankTransaction = {
    Id: TransactionId
    BookingDate: DateTime
    Amount: Money
    Payee: string option
    Memo: string
    Reference: string  // Comdirect reference for dedup
    RawData: string    // Original JSON for debugging
}

// ============================================
// Transaction Status in Sync Flow
// ============================================

type TransactionStatus =
    | Pending           // Newly fetched, no categorization
    | AutoCategorized   // Rule applied automatically
    | ManualCategorized // User assigned category
    | NeedsAttention    // Special case (Amazon, PayPal)
    | Skipped           // User chose to skip
    | Imported          // Successfully sent to YNAB

type ExternalLink = {
    Label: string
    Url: string
}

/// The target of a single split line: a budget category, a transfer to another YNAB
/// account (e.g. cash withdrawal at the till), or no target at all.
/// Purpose: the DU keeps the illegal "category AND transfer" state unrepresentable
/// (ADR 0006). "Neither" used to be unrepresentable too, but the mirror reads YNAB,
/// where an uncategorized split line is ordinary reality — so that case is now named
/// rather than forbidden. The transfer line stores the destination ACCOUNT; the
/// YNAB transfer payee_id is resolved only at push time (ADR 0006), never here.
type SplitTarget =
    /// Allocate this split line to a YNAB budget category.
    | ToCategory of categoryId: YnabCategoryId * categoryName: string
    /// Move this split line to another YNAB account (a transfer, no budget impact).
    /// Stores the destination account; the transfer payee_id is resolved at push time.
    | ToTransfer of accountId: YnabAccountId * accountName: string
    /// No category and no transfer: an uncategorized split line, as YNAB allows it.
    /// Only the mirror produces this (BB's own split editor never does); it counts
    /// fully towards the split sum and is never dropped.
    | Uncategorized

/// Represents a single split within a transaction for multi-target allocation.
/// Purpose: Allows splitting a single bank transaction into multiple YNAB
/// categories and/or transfers (e.g. cashback: one category line + one transfer
/// line to the cash account).
type TransactionSplit = {
    /// The target of this split: a category or a transfer to another account.
    Target: SplitTarget
    /// The amount for this split (must sum to transaction total)
    Amount: Money
    /// Optional memo for this specific split
    Memo: string option
}

/// Why a proposed set of splits is structurally invalid.
/// Purpose: The shared smart-constructor `mkSplits` rejects invalid splits up front
/// so neither the client nor the server can build an illegal split (ADR 0006).
type SplitError =
    /// Fewer than the required two split lines were supplied.
    | TooFewLines of count: int
    /// The sign-correct sum of the split lines does not match the transaction total.
    | SumMismatch of expected: decimal * actual: decimal
    /// The split lines (or the total) mix more than one currency.
    | CurrencyMismatch

/// Tolerance for the split sum comparison (one cent), matching the existing
/// server-side split validation tolerance. Not a [<Literal>]: decimal cannot be a
/// literal in F# and an attempted decimal literal breaks the module's static init.
let private splitSumTolerance = 0.01m

/// The amount still unallocated when `enteredLines` are subtracted from `total`.
/// A negative result means over-allocation (the entered lines exceed the total).
/// Used by the UI/cashback builder to compute the transfer line that "soaks up"
/// the rest of a transaction.
let splitRemainder (total: Money) (enteredLines: TransactionSplit list) : decimal =
    total.Amount - (enteredLines |> List.sumBy (fun s -> s.Amount.Amount))

/// Smart-constructor for a set of splits. Enforces the structural invariants from
/// ADR 0006 so that an invalid split is unrepresentable at the boundary:
///   * at least two lines (a "split" of one line is not a split),
///   * one currency across the total and every line,
///   * the sign-correct sum of all lines (transfer lines included) equals the total.
/// The "category and transfer at once" case is already unrepresentable in the
/// `SplitTarget` DU; an `Uncategorized` line counts towards the sum like any other.
/// Returns the validated splits unchanged on success.
let mkSplits (total: Money) (lines: TransactionSplit list) : Result<TransactionSplit list, SplitError> =
    if lines.Length < 2 then
        Error (TooFewLines lines.Length)
    elif lines |> List.exists (fun s -> s.Amount.Currency <> total.Currency) then
        Error CurrencyMismatch
    else
        let sum = lines |> List.sumBy (fun s -> s.Amount.Amount)
        if abs (sum - total.Amount) > splitSumTolerance then
            Error (SumMismatch (total.Amount, sum))
        else
            Ok lines

/// Builds the canonical "cashback" split: one category line for the actual purchase
/// plus one transfer line to the cash account that soaks up the remainder of the
/// transaction (the amount that was withdrawn as cash). The result is validated
/// through `mkSplits`, so a degenerate input (e.g. the category line already equal
/// to the total, leaving a zero transfer) is rejected as `TooFewLines`-safe only by
/// the caller's amounts — callers should ensure both lines carry a non-trivial
/// amount. Returns `Ok` only for a structurally valid two-line split.
let buildCashbackSplit
    (total: Money)
    (categoryId: YnabCategoryId)
    (categoryName: string)
    (categoryAmount: Money)
    (cashAccountId: YnabAccountId)
    (cashAccountName: string)
    (transferMemo: string option)
    : Result<TransactionSplit list, SplitError> =
    let categoryLine =
        { Target = ToCategory (categoryId, categoryName)
          Amount = categoryAmount
          Memo = None }
    let remainder = splitRemainder total [ categoryLine ]
    let transferLine =
        { Target = ToTransfer (cashAccountId, cashAccountName)
          Amount = { Amount = remainder; Currency = total.Currency }
          Memo = transferMemo }
    mkSplits total [ categoryLine; transferLine ]

// ============================================
// Duplicate Detection (BudgetBuddy's pre-import analysis)
// ============================================

/// Details about why BudgetBuddy detected (or didn't detect) this as a duplicate.
/// Purpose: Provides transparency into the duplicate detection algorithm for debugging.
type DuplicateDetectionDetails = {
    /// The bank transaction's Reference field from Comdirect
    TransactionReference: string
    /// Did we find this Reference in any YNAB transaction memo ("Ref: X")?
    ReferenceFoundInYnab: bool
    /// Did we find an ImportId matching our format (BB:{txId}) in YNAB?
    ImportIdFoundInYnab: bool
    /// If fuzzy matched: matched YNAB transaction date
    FuzzyMatchDate: DateTime option
    /// If fuzzy matched: matched YNAB transaction amount
    FuzzyMatchAmount: decimal option
    /// If fuzzy matched: matched YNAB transaction payee
    FuzzyMatchPayee: string option
}

/// Default empty detection details (used when no YNAB data available for comparison)
let emptyDetectionDetails reference = {
    TransactionReference = reference
    ReferenceFoundInYnab = false
    ImportIdFoundInYnab = false
    FuzzyMatchDate = None
    FuzzyMatchAmount = None
    FuzzyMatchPayee = None
}

/// Indicates whether a transaction is a potential duplicate (BudgetBuddy's analysis BEFORE import)
type DuplicateStatus =
    | NotDuplicate of details: DuplicateDetectionDetails
    | PossibleDuplicate of reason: string * details: DuplicateDetectionDetails
    | ConfirmedDuplicate of reference: string * details: DuplicateDetectionDetails

/// Helper to extract details from any DuplicateStatus
let getDuplicateDetails (status: DuplicateStatus) : DuplicateDetectionDetails =
    match status with
    | NotDuplicate details -> details
    | PossibleDuplicate (_, details) -> details
    | ConfirmedDuplicate (_, details) -> details

// ============================================
// YNAB Import Status (what happened DURING import)
// ============================================

/// Why YNAB rejected a transaction during import
type YnabRejectionReason =
    | DuplicateImportId of importId: string  // YNAB already has this import_id
    | UnknownRejection of rawResponse: string option

/// Status of YNAB's import attempt for a transaction
type YnabImportStatus =
    | NotAttempted            // Import not yet tried
    | YnabImported            // Successfully imported to YNAB
    | RejectedByYnab of YnabRejectionReason  // YNAB rejected it

// ============================================
// Review Trail (categorization-gdrx8)
// ============================================
// Roman hat entschieden (2026-09-11), dass Regel-Vorschlag und Romans Korrektur beim
// Push als Ereignisfolge im Kassenbuch sichtbar werden ("Regel X: Lebensmittel", dann
// "Roman in BB: Drogerie", ADR 0021). Weil die Kassenbuch-Buchung laut ADR 0021 erst
// mit dem Push entsteht, führt die SyncSession bis dahin selbst Buch, wer wann welchen
// Kategorisierungs-Stand gesetzt hat. `ledger-qvt84` liest diese Spur beim Push und
// übersetzt sie in `EntryCategorizedByRule` / `EntryRecategorized`-Ereignisse mit den
// hier festgehaltenen Original-Zeitpunkten.

/// Wer einen Review-Schritt gesetzt hat: die Regel-Engine (mit Regel-Identität und
/// -Name, damit eine spätere Anzeige "Regel X: ..." nicht erst nachschlagen muss),
/// Roman selbst (manuelle Kategorisierung/Split/PayeeOverride; die fachliche
/// Entscheidung bleibt seine, auch wenn BB sie nur auf Geschwister-Buchungen
/// derselben Amazon-Order-ID überträgt, `OrderIdMatcher.propagateInSession`),
/// oder der Order-ID-Abgleich (`OrderIdMatcher.applySuggestions`): eine Kategorie, die
/// beim Abruf aus der YNAB-Historie derselben Bestellung übernommen wurde, ohne dass
/// Roman oder eine Regel sie für DIESE Buchung getroffen hätte. Anzeigename
/// "Order-ID-Abgleich" (Entscheidung Roman, 2026-09-11).
type ReviewActor =
    | ByRule of ruleId: RuleId * ruleName: string
    | ByUser
    | ByOrderIdMatch of orderId: string

/// Ein einzelner Schritt in der Review-Spur einer SyncTransaction: ein vollständiger
/// Schnappschuss von Kategorie, Splits und PayeeOverride zum Zeitpunkt `At`, nicht nur
/// das geänderte Feld. So kann `ledger-qvt84` aus der Spur direkt eine Ereignisfolge
/// mit dem tatsächlichen historischen Zustand bauen.
type ReviewStep = {
    At: DateTime
    By: ReviewActor
    Category: YnabCategoryId option
    CategoryName: string option
    Splits: TransactionSplit list option
    PayeeOverride: string option
}

type SyncTransaction = {
    Transaction: BankTransaction
    Status: TransactionStatus
    CategoryId: YnabCategoryId option
    CategoryName: string option
    MatchedRuleId: RuleId option
    PayeeOverride: string option
    ExternalLinks: ExternalLink list
    UserNotes: string option
    /// BudgetBuddy's pre-import duplicate detection (analyzed BEFORE sending to YNAB)
    DuplicateStatus: DuplicateStatus
    /// What happened when we tried to import to YNAB (set AFTER import attempt)
    YnabImportStatus: YnabImportStatus
    /// Optional list of splits for multi-category transactions.
    /// None = single category transaction (uses CategoryId)
    /// Some [] = invalid state (splits must have at least 2 items)
    /// Some [split1; split2; ...] = split transaction (splits must sum to transaction amount)
    Splits: TransactionSplit list option
    /// If set, this transaction's category was suggested based on another transaction with the same Amazon Order ID
    SuggestedByOrderId: string option
    /// Die Review-Spur: älteste Entscheidung zuerst, damit die Reihenfolge direkt in
    /// Ereignis-Reihenfolge übersetzbar ist (categorization-gdrx8, ADR 0021).
    ReviewSteps: ReviewStep list
}

/// Hängt einen Review-Schritt an, der den aktuellen Kategorisierungs-Stand von `tx`
/// (CategoryId, CategoryName, Splits, PayeeOverride) mit Zeitpunkt und Herkunft
/// festhält. Eine Wiederholung mit unverändertem Kategorie-/Split-/PayeeOverride-Stand
/// erzeugt bewusst keinen neuen Eintrag (sonst würde jeder erneute Aufruf mit
/// identischem Ergebnis, z. B. ein wiederholtes `categorizeTransaction` auf denselben
/// Wert, die Spur unnötig aufblähen). `CategoryName` fließt nicht in den Vergleich ein:
/// sie ist ein Anzeige-Cache der Kategorie-Id, keine eigene Fachentscheidung.
let appendReviewStep (at: DateTime) (by: ReviewActor) (tx: SyncTransaction) : SyncTransaction =
    let candidate = {
        At = at
        By = by
        Category = tx.CategoryId
        CategoryName = tx.CategoryName
        Splits = tx.Splits
        PayeeOverride = tx.PayeeOverride
    }
    match List.tryLast tx.ReviewSteps with
    | Some last
        when last.Category = candidate.Category
          && last.Splits = candidate.Splits
          && last.PayeeOverride = candidate.PayeeOverride ->
        tx
    | _ ->
        { tx with ReviewSteps = tx.ReviewSteps @ [ candidate ] }

// ============================================
// Categorization Rules
// ============================================

type PatternType =
    | Regex
    | Contains
    | Exact

type TargetField =
    | Payee
    | Memo
    | Combined  // Searches both payee and memo

type Rule = {
    Id: RuleId
    Name: string
    Pattern: string
    PatternType: PatternType
    TargetField: TargetField
    CategoryId: YnabCategoryId
    CategoryName: string  // Cached for display
    PayeeOverride: string option
    Priority: int
    Enabled: bool
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

type RuleCreateRequest = {
    Name: string
    Pattern: string
    PatternType: PatternType
    TargetField: TargetField
    CategoryId: YnabCategoryId
    PayeeOverride: string option
    Priority: int
}

type RuleUpdateRequest = {
    Id: RuleId
    Name: string option
    Pattern: string option
    PatternType: PatternType option
    TargetField: TargetField option
    CategoryId: YnabCategoryId option
    PayeeOverride: string option
    Priority: int option
    Enabled: bool option
}

// ============================================
// YNAB Types
// ============================================

type YnabBudget = {
    Id: YnabBudgetId
    Name: string
}

type YnabAccount = {
    Id: YnabAccountId
    Name: string
    Balance: Money
    /// YNAB's `on_budget` flag: true for budget accounts (cash, checking),
    /// false for tracking/off-budget accounts (savings, assets). Used by the
    /// split transfer-target picker, which offers only on-budget accounts (ADR 0006).
    OnBudget: bool
    /// YNAB's `closed` flag: a closed account is hidden from selection.
    Closed: bool
}

/// The accounts eligible as a split transfer target: open (not closed) and
/// on-budget. Every YNAB account auto-carries a transfer payee, so this filter
/// is pure scope (Roman, 2026-06-13) — it excludes nothing technically required;
/// the push-time transfer-payee resolution (ADR 0006) remains the backstop.
let openOnBudgetAccounts (accounts: YnabAccount list) : YnabAccount list =
    accounts |> List.filter (fun a -> a.OnBudget && not a.Closed)

type YnabCategory = {
    Id: YnabCategoryId
    Name: string
    GroupName: string
    /// Current YNAB "Available" of this category. `Some` carries the value (YNAB
    /// `balance`, current month) shown right-aligned in the category picker; `None`
    /// means there is no meaningful value to show. The internal "Inflow: Ready to
    /// Assign" category stays `None` until overridden with the month's real
    /// Ready-to-Assign (`to_be_budgeted`) via `applyReadyToAssign` — its own
    /// `balance` is an internal accumulator, not a real per-category Available.
    Available: Money option
    /// YNAB's own `hidden` flag (ledger-j8xq4): a category Roman hid in YNAB. The
    /// Kassenbuch-Filterleiste keeps hidden categories collapsed under "Versteckt (n)"
    /// so they are never accidentally picked. Missing in the JSON (older cached data,
    /// or a decoder that never asked) decodes to `false`, the conformist-safe default,
    /// matching `OnBudget`/`Closed` on `YnabAccount` above.
    Hidden: bool
}

/// YNAB's internal category group; its categories' `balance` values are not normal
/// per-category Availables.
[<Literal>]
let internalMasterGroupName = "Internal Master Category"

/// The internal "Ready to Assign" inflow category. Its own `balance` from the
/// /categories endpoint is an internal accumulator (can read as a huge number),
/// NOT the real Ready-to-Assign — that is the month's `to_be_budgeted`.
[<Literal>]
let readyToAssignCategoryName = "Inflow: Ready to Assign"

/// Overrides the "Inflow: Ready to Assign" category's Available with the month's
/// real Ready-to-Assign (`to_be_budgeted`). `toBeBudgeted = None` (e.g. the month
/// fetch failed) leaves that row without a number rather than showing the garbage
/// category balance. Every other category passes through unchanged.
let applyReadyToAssign (toBeBudgeted: Money option) (categories: YnabCategory list) : YnabCategory list =
    categories
    |> List.map (fun c ->
        if c.GroupName = internalMasterGroupName && c.Name = readyToAssignCategoryName then
            { c with Available = toBeBudgeted }
        else c)

/// Blendet die YNAB-Interna aus der Kategorieauswahl aus: eine Kategorie der Gruppe
/// "Internal Master Category" bleibt nur, wenn sie "Inflow: Ready to Assign" ist.
/// Erkennung über die Gruppe, nicht über den Namen allein. Reihenfolge unverändert.
let withoutInternalCategories (categories: YnabCategory list) : YnabCategory list =
    categories
    |> List.filter (fun c ->
        c.GroupName <> internalMasterGroupName || c.Name = readyToAssignCategoryName)

/// Nachbearbeitung der Kategorien für die Auswahlen (Endpunkt `getCategories`):
/// Ready to Assign bekommt den echten Monatswert, danach fallen die YNAB-Interna weg.
let categoriesForPicker (toBeBudgeted: Money option) (categories: YnabCategory list) : YnabCategory list =
    categories |> applyReadyToAssign toBeBudgeted |> withoutInternalCategories

/// YNAB payee for transaction assignment.
/// Purpose: Used in payee dropdown for transaction editing.
type YnabPayee = {
    Id: YnabPayeeId
    Name: string
    /// If set, this payee is a transfer payee to the specified account.
    TransferAccountId: YnabAccountId option
}

type YnabBudgetWithAccounts = {
    Budget: YnabBudget
    Accounts: YnabAccount list
    Categories: YnabCategory list
}

/// Represents an existing transaction in YNAB (for duplicate detection)
type YnabTransaction = {
    Id: string
    Date: DateTime
    Amount: Money
    Payee: string option
    Memo: string option
    ImportId: string option  // Used for dedup
    CategoryId: string option
    CategoryName: string option
}

// ============================================
// Sync Session
// ============================================

type SyncSessionStatus =
    | AwaitingBankAuth      // Need to start Comdirect OAuth
    | AwaitingTan           // Waiting for user to confirm TAN
    | FetchingTransactions  // Fetching from bank
    | ReviewingTransactions // User reviewing/categorizing
    | ImportingToYnab       // Sending to YNAB
    | Completed             // Done
    | Failed of string      // Error occurred

type SyncSession = {
    Id: SyncSessionId
    StartedAt: DateTime
    CompletedAt: DateTime option
    Status: SyncSessionStatus
    TransactionCount: int
    ImportedCount: int
    SkippedCount: int
}

type SyncSessionSummary = {
    Session: SyncSession
    Transactions: SyncTransaction list
}

// ============================================
// Settings/Configuration
// ============================================

type ComdirectSettings = {
    ClientId: string
    ClientSecret: string
    Username: string
    Password: string
    AccountId: string option  // Optional: only needed for fetching transactions
}

/// Comdirect bank account information retrieved from API
type ComdirectAccount = {
    AccountId: string
    Iban: string
    AccountDisplayId: string  // Masked account number for display
    AccountType: string       // e.g., "GIRO", "DEPOT", "TAGESGELD"
    Currency: string
    Balance: Money option
}

/// Type-safe wrapper for Comdirect Account ID
type ComdirectAccountId = ComdirectAccountId of string

type YnabSettings = {
    PersonalAccessToken: string
    DefaultBudgetId: YnabBudgetId option
    DefaultAccountId: YnabAccountId option
    /// Account for Quick Add entries (e.g. the cash account) — distinct from
    /// the bank-import account so manual cash expenses land where they belong
    QuickAddAccountId: YnabAccountId option
}

type SyncSettings = {
    DaysToFetch: int  // How many days back to fetch
}

type AppSettings = {
    Comdirect: ComdirectSettings option
    Ynab: YnabSettings option
    Sync: SyncSettings
}

// ============================================
// Comdirect Auth State (for TAN flow)
// ============================================

type ComdirectAuthState =
    | NotAuthenticated
    | WaitingForPushTan of challengeId: string
    | Authenticated of accessToken: string * refreshToken: string

// ============================================
// Error Types
// ============================================

/// Errors that can occur during settings operations.
/// Purpose: Provides granular error information for settings validation and persistence.
type SettingsError =
    | YnabTokenInvalid of message: string
    | YnabConnectionFailed of httpStatus: int * message: string
    | ComdirectCredentialsInvalid of field: string * reason: string
    | EncryptionFailed of message: string
    | DatabaseError of operation: string * message: string

/// Errors from YNAB API operations.
/// Purpose: Differentiates between network issues, auth failures, and data problems.
type YnabError =
    | Unauthorized of message: string
    | BudgetNotFound of budgetId: string
    | AccountNotFound of accountId: string
    | CategoryNotFound of categoryId: string
    | RateLimitExceeded of retryAfterSeconds: int
    | NetworkError of message: string
    | InvalidResponse of message: string

/// Errors during rules operations.
/// Purpose: Provides clear feedback for rule validation and compilation failures.
type RulesError =
    | RuleNotFound of ruleId: Guid
    | InvalidPattern of pattern: string * reason: string
    | CategoryNotFound of categoryId: Guid
    | DuplicateRule of pattern: string
    | DatabaseError of operation: string * message: string

/// Errors during sync operations.
/// Purpose: Tracks failures across the multi-step sync process for debugging.
type SyncError =
    | SessionNotFound of sessionId: Guid
    | ComdirectAuthFailed of reason: string
    | TanTimeout
    | TransactionFetchFailed of message: string
    | YnabImportFailed of failedCount: int * message: string
    | InvalidSessionState of expected: string * actual: string
    | DatabaseError of operation: string * message: string

/// Errors from Comdirect API operations.
/// Purpose: Distinguishes auth failures from network issues for retry logic.
type ComdirectError =
    | AuthenticationFailed of message: string
    | TanChallengeExpired
    | TanRejected
    | SessionExpired
    | InvalidCredentials
    | NetworkError of httpStatus: int * message: string
    | InvalidResponse of message: string

// ============================================
// Result Type Aliases
// ============================================

type SettingsResult<'T> = Result<'T, SettingsError>
type YnabResult<'T> = Result<'T, YnabError>
type RulesResult<'T> = Result<'T, RulesError>
type SyncResult<'T> = Result<'T, SyncError>

// ============================================
// Manual Transaction Entry (Quick Add)
// ============================================

/// A manually entered transaction (e.g. a cash expense), pushed directly to
/// YNAB without a bank sync session. Phase 0 of the YNAB-replacement vision
/// (docs/idea-ynab-replacement.md).
type ManualTransactionRequest = {
    /// Positive amount as entered by the user; direction comes from IsOutflow
    Amount: decimal
    /// true = expense (negative amount in YNAB), false = income
    IsOutflow: bool
    PayeeName: string
    CategoryId: YnabCategoryId option
    Date: DateTime
    Memo: string option
}

/// Signed YNAB milliunits for a manual entry (outflow = negative).
let manualTransactionMilliunits (request: ManualTransactionRequest) : int =
    let signedAmount = if request.IsOutflow then -request.Amount else request.Amount
    int (Math.Round(signedAmount * 1000m))

/// Inverse of `manualTransactionMilliunits`: signed YNAB milliunits → direction
/// and positive amount. Negative milliunits mean an outflow (expense); the
/// returned amount is the absolute value in major units (|milliunits| / 1000).
/// Together with `manualTransactionMilliunits` this round-trips a manual entry.
let amountFromMilliunits (milliunits: int) : bool * decimal =
    let isOutflow = milliunits < 0
    let amount = decimal (abs milliunits) / 1000m
    isOutflow, amount

// ============================================
// Quick Add Templates (recurring cash bookings)
// ============================================

/// A reusable Quick-Add template distilled from a past transaction of the
/// Quick-Add account (ynab-t4n8p). Carries exactly the fields the Quick Add form
/// prefills. The source booking's date is intentionally absent — a prefilled
/// entry is always dated *today*, never the original transaction's date.
type QuickAddTemplate = {
    /// Positive major-unit amount; the direction lives in IsOutflow.
    Amount: decimal
    IsOutflow: bool
    PayeeName: string option
    CategoryId: YnabCategoryId
    CategoryName: string option
    Memo: string option
}

/// Projects a YNAB transaction into a Quick-Add template, or None when it is not
/// a usable template shape. Only a plain *categorized* booking qualifies: a
/// missing or unparseable category id drops the transaction, which naturally
/// excludes transfers and split parents (both carry no category on the account
/// side) — exactly the shapes we do not want to offer as a one-tap template.
let private toQuickAddTemplate (tx: YnabTransaction) : QuickAddTemplate option =
    match tx.CategoryId with
    | None -> None
    | Some categoryIdStr ->
        match Guid.TryParse categoryIdStr with
        | false, _ -> None
        | true, categoryGuid ->
            let milliunits = int (Math.Round(tx.Amount.Amount * 1000m))
            let isOutflow, amount = amountFromMilliunits milliunits
            Some {
                Amount = amount
                IsOutflow = isOutflow
                PayeeName = tx.Payee
                CategoryId = YnabCategoryId categoryGuid
                CategoryName = tx.CategoryName
                Memo = tx.Memo
            }

/// Builds up to `maxTemplates` deduplicated Quick-Add templates from recent
/// transactions of the Quick-Add account, most-recent first. The dedup key is
/// Payee + signed amount + category, so a recurring booking (same payee, amount
/// and category) collapses to a single template; up to `maxTemplates` *distinct*
/// recurring bookings are returned. Pure — testable without any YNAB I/O.
let recentQuickAddTemplates (maxTemplates: int) (transactions: YnabTransaction list) : QuickAddTemplate list =
    transactions
    |> List.sortByDescending (fun tx -> tx.Date)
    |> List.choose toQuickAddTemplate
    |> List.distinctBy (fun t -> (t.PayeeName, t.IsOutflow, t.Amount, t.CategoryId))
    |> List.truncate maxTemplates

type ComdirectResult<'T> = Result<'T, ComdirectError>

// ============================================
// Spiegel-Lauf (Kassenbuch, ADR 0014)
// ============================================

/// Was ein Spiegel-Lauf bewegt hat: wie viele Buchungen neu ins Kassenbuch kamen, bei
/// wie vielen eine Aenderung beobachtet wurde und wie viele in YNAB geloescht sind.
/// `ServerKnowledge` ist der Stand, ab dem der naechste Lauf sein Delta holt.
type MirrorRunSummary = {
    NewEntries: int
    ChangedEntries: int
    DeletedEntries: int
    ServerKnowledge: int64
}

/// Wie ein Spiegel-Lauf ausging. Ein Fehler wird sichtbar gemacht, nicht still
/// geschluckt (ADR 0014).
type MirrorRunOutcome =
    | MirrorRunSucceeded of MirrorRunSummary
    | MirrorRunFailed of message: string

/// Der zuletzt gelaufene Spiegel-Lauf: Zeitpunkt und Ergebnis.
type MirrorRunStatus = {
    RanAt: DateTime
    Outcome: MirrorRunOutcome
}

// ============================================
// Kassenbuch-Sichten (ledger-q5w9c)
// ============================================
// Die Lesemodelle der Kassenbuch-Seite. Bewusst eigene, flache Formen statt der
// Server-Typen aus `Ledger.Events` / `Ledger.Domain`: die Seite braucht Namen und
// Saetze, nicht Ereignis-Typen (ADR 0013). Der Zustand wird gelesen, nie neu
// gefaltet — Replay ist Gegenprobe, kein Lesepfad.

/// Die Herkunft einer Buchung oder einer Aenderung, in der Sprache des Kassenbuchs.
/// Spiegelt den `Actor` des Ereignis-Logs (ADR 0013), ohne dessen Nutzlast: die
/// Anzeige zeigt "Regel", nicht welche Regel.
type LedgerOrigin =
    /// Der Spiegel hat es in YNAB gesehen.
    | MirroredFromYnab
    /// Aus dem Comdirect-Import.
    | ImportedFromComdirect
    /// Eine Kategorisierungs-Regel hat entschieden.
    | DecidedByRule
    /// Ein Mensch hat in BB entschieden.
    | DecidedInBb
    /// Ueber Quick Add erfasst.
    | AddedViaQuickAdd
    /// Kategorie aus dem Order-ID-Abgleich beim Abruf übernommen (Vorschlag aus der
    /// YNAB-Historie derselben Amazon-Bestellung, weder Regel noch Roman für DIESE
    /// Buchung, Entscheidung Roman 2026-09-11).
    | MatchedByOrderId

/// Wie eine Herkunft im Kassenbuch heisst. Wortlaut aus dem Ubiquitous Language
/// des Contexts; die Anzeige erfindet keine eigenen Worte.
let originLabel (origin: LedgerOrigin) : string =
    match origin with
    | MirroredFromYnab -> "YNAB-Spiegel"
    | ImportedFromComdirect -> "Comdirect-Import"
    | DecidedByRule -> "Regel"
    | DecidedInBb -> "Roman in BB"
    | AddedViaQuickAdd -> "Quick Add"
    | MatchedByOrderId -> "Order-ID-Abgleich"

/// Die Art eines Kassenbuch-Kontos (ADR 0016) in der Form, die die Anzeige braucht.
/// v1 erzeugt nur `LedgerBankAccount`; die anderen Arten sind da, damit die
/// Konto-Auswahl sie nicht ausschliesst.
type LedgerAccountKindView =
    | LedgerBankAccount
    | LedgerCashAccount
    | LedgerSecuritiesAccount

/// Das Flag einer Buchung, gedeutet (ADR 0032): roh im Log/Zustand nur Farbe und
/// Name, hier die drei Fälle, die das Kassenbuch unterscheidet. `AskLena` erkennt
/// den Flag-Namen "Lena" (nach Trim, ohne Beachtung der Groß-/Kleinschreibung),
/// `Marked` jedes andere gesetzte Flag (auch nur eine Farbe ohne Namen).
type LedgerFlag =
    | NoFlag
    | AskLena
    | Marked

/// Wie ein Flag im Kassenbuch heisst. `None` bei `NoFlag`: kein Flag zeigt kein
/// Kennzeichen, statt eines leeren.
let flagLabel (flag: LedgerFlag) : string option =
    match flag with
    | NoFlag -> None
    | AskLena -> Some "Rückfrage Lena"
    | Marked -> Some "Markiert"

/// Ein Konto in der Kontoauswahl. Kein Kontostand als Feld: der Stand ist die Summe
/// der Buchungen (ADR 0016/0017) und wird aus der geladenen Liste gerechnet.
type LedgerAccountView = {
    Id: Guid
    Name: string
    Kind: LedgerAccountKindView
    /// Die YNAB-Konto-Id, falls das Konto gespiegelt wird (`YnabMirrored`, ADR 0016).
    /// `None` bei BB-eigenen Konten.
    YnabAccountId: YnabAccountId option
    Closed: bool
}

/// Die Konten des Kassenbuchs samt der Vorauswahl der Seite.
type LedgerAccountsView = {
    Accounts: LedgerAccountView list
    /// Das Konto, das die Seite ohne Zutun zeigt: das gespiegelte YNAB-Standardkonto,
    /// sonst das erste. `None` nur, wenn es gar kein Konto gibt.
    DefaultAccountId: Guid option
}

/// Ein gescheiterter Push nach YNAB: lesbarer Grund und Zeitpunkt (UTC) des Ereignisses.
type LedgerPushFailure = {
    Reason: string
    FailedAt: DateTime
}

/// Eine Buchung, wie die Kassenbuch-Seite sie zeigt.
type LedgerEntryView = {
    Id: Guid
    AccountId: Guid
    Date: DateTime
    Amount: Money
    Payee: string option
    Memo: string option
    /// Der Kategorie-Name aus der letzten Beobachtung. `None`, wenn die Buchung
    /// keine Kategorie traegt (auch bei Splits).
    CategoryName: string option
    /// Die Id der Kategorie der Buchung (ledger-rfq5p), damit die Zeile nach genau dieser
    /// Kategorie filtern kann. `None`, wenn die Buchung keine Kategorie trägt (auch bei
    /// Splits). Reines Read-Model-Feld, kein Ereignis, kein Schema.
    CategoryId: Guid option
    /// Die Kategoriegruppe, im Lesepfad nachgeschlagen (ADR 0039, ledger-h6ptn): die
    /// Gruppe ist Stammdatum der Kategorie, nicht Zustand der Buchung, und steht
    /// deshalb NICHT im Ereignis-Log. `None`, wenn die Buchung keine Kategorie
    /// trägt, oder wenn die Kategorie noch nie gespiegelt wurde
    /// (`ledger_categories`, `ledger-p3wnq`). In beiden Fällen zeigt die Anzeige
    /// dann nur `CategoryName`, nie ein erfundenes Präfix.
    CategoryGroupName: string option
    Splits: TransactionSplit list
    /// Woher die Buchung stammt: der Actor ihres ersten Ereignisses.
    Origin: LedgerOrigin
    /// In YNAB geloescht. Die Buchung bleibt sichtbar und traegt ein Kennzeichen;
    /// sie zaehlt nicht mehr zum Kontostand (ADR 0013).
    DeletedInYnab: bool
    /// Zu prüfen: `approved = false` oder `cleared = Uncleared` (ADR 0032). Der Chip
    /// "Zu prüfen (n)" und der Punkt am linken Zeilenrand hängen daran.
    NeedsReview: bool
    /// Das gedeutete Flag der Buchung (ADR 0032).
    Flag: LedgerFlag
    /// Die Buchung ist ein Übertrag (rohes `transfer_account_id` oder aufgelöste
    /// Gegenbuchung, ADR 0048). Ein Übertrag trägt ein Ziel und ist darum nicht
    /// "ohne Kategorie".
    IsTransfer: bool
    /// Grund des offenen Fehlschlags beim Zurückschreiben des Prüfvermerks nach YNAB
    /// (ADR 0050, Punkt 11): die Zeile zeigt dann "Prüfvermerk nicht in YNAB" mit
    /// diesem Grund und "Erneut senden". `None`, wenn nichts offen ist. Ändert die
    /// Zählung von "Zu prüfen" nicht.
    ReviewMarkWriteBackFailure: string option
    /// Die Buchung trägt eine YNAB-Verknüpfung: vom Spiegel gesehene Buchungen immer,
    /// BB-geführte (Comdirect-Import, Quick Add) nach `EntryLinkedToYnab` (ADR 0021).
    LinkedToYnab: bool
    /// Der offene Fehlschlag des Push nach YNAB mit Grund und Zeitpunkt (ADR 0021,
    /// Punkt 2, Nachtrag ledger-rynk5). `None`, wenn nichts offen ist; die Verknüpfung
    /// mit YNAB löst ihn auf.
    PushToYnabFailure: LedgerPushFailure option
}

// ============================================
// Prüfvermerk setzen und zurückschreiben (ADR 0050, ledger-r777c)
// ============================================

/// Das Ziel einer Geste auf dem Prüfvermerk: `Reviewed` setzt `approved = true`
/// (und `cleared` von offen auf bestätigt), `Unreviewed` das Gegenteil. `Reconciled`
/// bleibt in beiden Fällen unberührt (ADR 0050, Punkt 2).
type ReviewMarkTarget =
    | Reviewed
    | Unreviewed

/// Der Cleared-Status im Prüfvermerk. Eigene Fallnamen, damit sie in Server-Dateien,
/// die `Ledger.Events.ClearedState` öffnen, nicht mit dessen Fällen kollidieren.
type ReviewClearedState =
    | ReviewUncleared
    | ReviewCleared
    | ReviewReconciled

/// Der Prüfvermerk einer Buchung: `approved` und `cleared` (ADR 0032). Der Client
/// bekommt den Vorzustand in der Antwort und schickt ihn für "Rückgängig" zurück
/// (ADR 0050, Punkt 10).
type ReviewMarkState = {
    IsApproved: bool
    ClearedState: ReviewClearedState
}

/// Hat der Befehl den Prüfvermerk geändert? `ReviewMarkUnchanged`: Ziel gleich Ist,
/// weder Ereignis noch YNAB-Aufruf (ADR 0050, Punkt 4).
type ReviewMarkChange =
    | ReviewMarkChanged
    | ReviewMarkUnchanged

/// Was mit dem Zurückschreiben nach YNAB passiert ist.
type ReviewMarkWriteBack =
    /// Nichts zu senden (Ziel gleich Ist, oder kein offener Fehlschlag beim erneuten Senden).
    | ReviewMarkNotSent
    /// YNAB hat den Prüfvermerk angenommen.
    | ReviewMarkSent
    /// Der Weg nach YNAB ist gescheitert oder nicht möglich; der Prüfvermerk bleibt in BB.
    | ReviewMarkSendFailed of reason: string

/// Antwort von `setReviewMark` und `restoreReviewMark` (ADR 0050, Punkt 10).
type ReviewMarkOutcome = {
    /// Der Prüfvermerk vor der Geste, Grundlage für "Rückgängig".
    Previous: ReviewMarkState
    Change: ReviewMarkChange
    WriteBack: ReviewMarkWriteBack
}

/// Eine Zeile der Historie einer Buchung: wann, von wem, und was passiert ist —
/// in der Sprache des Kassenbuchs, nicht als Ereignis-Typname (ADR 0013).
type LedgerHistoryLine = {
    /// Die laufende Nummer des Ereignisses je Buchung (ab 1).
    Sequence: int
    OccurredAt: DateTime
    Origin: LedgerOrigin
    /// Der sprechende Satz, z. B. "Kategorie in YNAB geändert: Lebensmittel → Restaurant".
    Description: string
}

/// Kontostand: die Summe der **nicht** in YNAB geloeschten Buchungen (ADR 0016).
///
/// Bewusst hier in F# summiert und nie per SQL-`SUM` oder `CAST(amount AS REAL)`:
/// die Betraege liegen als exakter Dezimal-Text im Log und im Zustand, damit ein
/// auditierbares Kassenbuch keine Gleitkomma-Drift zeigt (ADR 0017). `decimal`
/// summiert exakt, `REAL` nicht.
///
/// Die Waehrung ist die der ersten mitzaehlenden Buchung; ohne Buchungen bleibt der
/// Stand null in `fallbackCurrency`.
let ledgerBalance (fallbackCurrency: string) (entries: LedgerEntryView list) : Money =
    let counted = entries |> List.filter (fun e -> not e.DeletedInYnab)
    let currency =
        counted
        |> List.tryHead
        |> Option.map (fun e -> e.Amount.Currency)
        |> Option.defaultValue fallbackCurrency
    { Amount = counted |> List.sumBy (fun e -> e.Amount.Amount); Currency = currency }

// ============================================
// Seitenweise Liste und Kontostaende (ledger-b1xwe)
// ============================================
// Fundament der zweiten Scheibe: eine seitenweise Sicht ueber ein Konto oder alle
// Konten, plus die Kontostaende aller Konten in einem Aufruf. Beide Summen werden in
// F# ueber `decimal` gebildet, nie per SQL-`SUM` (ADR 0017) — dieselbe Regel wie bei
// `ledgerBalance` oben, hier auf zwei weitere Faelle angewandt (Treffer-Summe,
// Kontostand je Konto).

/// Summe einer Liste von Betraegen, exakt in `decimal` gebildet. Anders als
/// `ledgerBalance` schliesst diese Funktion nichts aus — welche Betraege zaehlen,
/// entscheidet der Aufrufer vorher (z. B. "alle Treffer einer Seiten-Abfrage" oder
/// "die Kontostaende aller Konten" fuer die Summe "Alle Konten").
let sumMoney (fallbackCurrency: string) (amounts: Money list) : Money =
    let currency =
        amounts |> List.tryHead |> Option.map (fun m -> m.Currency) |> Option.defaultValue fallbackCurrency
    { Amount = amounts |> List.sumBy (fun m -> m.Amount); Currency = currency }

/// Das Feld, nach dem eine Seite des Kassenbuchs sortiert wird (ADR 0051). Geschlossene
/// Union: Fable.Remoting lehnt einen unbekannten Fall beim Deserialisieren ab.
type LedgerSortField =
    | SortByDate
    | SortByAmount
    | SortByPayee
    | SortByCategory
    | SortByAccount

/// Die Richtung der Sortierung. Die Namen sind mit Absicht von `Table.SortDirection`
/// (Design-System) verschieden, damit ein `open` beider Module nichts verdeckt.
type LedgerSortDirection =
    | SortAsc
    | SortDesc

/// Sortierfeld und Richtung einer Seitenanfrage (ADR 0051). Kein Teil des Filters:
/// sie wirkt nur auf die Reihenfolge der Seite, nie auf Trefferzahl, Summe oder Zähler.
type LedgerSort = {
    Field: LedgerSortField
    Direction: LedgerSortDirection
}

module LedgerSort =
    /// Datum, absteigend: die bisherige feste Reihenfolge des Kassenbuchs.
    let defaultSort : LedgerSort = { Field = SortByDate; Direction = SortDesc }

    /// Ein Client, der älter als ADR 0051 ist (etwa aus dem PWA-Cache), schickt kein `Sort`;
    /// Fable.Remoting setzt das fehlende Record-Feld dann auf null. Das gilt als Standard.
    let orDefault (sort: LedgerSort) : LedgerSort =
        if isNull (box sort) then defaultSort else sort

/// Parameter einer Seiten-Abfrage der Buchungsliste: ein Konto (optional — `None`
/// heisst alle Konten, die Gesamtsicht), ein 0-basierter Seitenzeiger und die
/// Seitengröße. `IncludeDeleted` (ledger-p3n8k): in YNAB gelöschte Buchungen
/// gehören standardmäßig NICHT in den Treffer (`false`), damit die sichtbare
/// Liste sich zum angezeigten Kontostand summiert; der Schalter der Seite setzt
/// `true`, um sie gedämpft wieder einzublenden.
/// `OnlyNeedsReview` (ADR 0032): Standard `false`, der Chip "Zu prüfen (n)" setzt
/// `true` und lässt nur ungeprüfte Buchungen (`approved = false` oder
/// `cleared = Uncleared`) im Treffer.
///
/// Die fünf Such-Filter (ledger-vgfw3, ADR 0036): `CategoryIds`, `DateFrom`/`DateTo`,
/// `Payee`, `Memo`. Leer/`None` heisst "kein Filter" — ein Aufruf ohne gesetzte
/// Filter liefert dasselbe Ergebnis wie vor dieser Erweiterung. Alle gesetzten Filter
/// wirken als Und-Verknüpfung; `CategoryIds` selbst ist eine Mehrfachauswahl
/// (Oder-Verknüpfung innerhalb der Liste) — der Punkt, an dem BB mehr kann als
/// YNAB-Web (nur eine Kategorie je Filter). `Payee` ist eine exakte, `Memo` eine
/// Teiltext-Übereinstimmung (beide ohne Berücksichtigung der Groß-/Kleinschreibung),
/// `DateFrom`/`DateTo` schließen die Randtage ein. `AmountMin`/`AmountMax` grenzen
/// den Betrag ein (beide Enden unabhängig voneinander, offen wenn `None`); weil der
/// Betrag als Text liegt (ADR 0017), wertet `Ledger.Persistence` sie in F# aus, nie
/// per SQL-Vergleich (ADR 0036).
type LedgerEntryPageQuery = {
    AccountId: Guid option
    Page: int
    PageSize: int
    /// Zahl der Zeilen, die der Client aus den schon geladenen Seiten lokal entfernt hat
    /// (z. B. geprüft bei aktivem Chip "Zu prüfen"). Der Server zählt den Versatz auf der
    /// gefilterten Menge neu, deshalb beginnt die Seite bei `Page * PageSize - LocallyRemoved`
    /// (nie unter 0). 0 heißt: Versatz wie bisher `Page * PageSize` (ledger-be1yh).
    LocallyRemoved: int
    IncludeDeleted: bool
    OnlyNeedsReview: bool
    /// Chip "Ohne Kategorie" (ADR 0048): nur Buchungen ohne Ziel (weder Kategorie noch
    /// Übertrag). Mit nicht leeren `CategoryIds` gilt die Vereinigung beider Mengen.
    WithoutCategory: bool
    CategoryIds: Guid list
    DateFrom: DateTime option
    DateTo: DateTime option
    Payee: string option
    Memo: string option
    AmountMin: decimal option
    AmountMax: decimal option
    /// Reihenfolge der Seite (ADR 0051). Wirkt nur auf `loadEntriesPage`, nicht auf Zählwege.
    Sort: LedgerSort
}

/// Eine Seite der Buchungsliste: die Buchungen dieser Seite, die Gesamtzahl der
/// Treffer und die Summe ueber den **gesamten** Treffer — nicht ueber die Seite
/// (Akzeptanzkriterium ledger-b1xwe). So kann die Seite waehrend des Nachladens immer
/// dieselbe, richtige Summe zeigen, auch bevor alle Seiten geladen sind.
/// `DeletedCount` (ledger-p3n8k): die Zahl der in YNAB gelöschten Buchungen im
/// Filter (Konto oder alle), unabhängig von `IncludeDeleted` (so kann die Seite
/// "3 gelöschte Buchungen ausgeblendet" auch zeigen, während sie ausgeblendet sind).
/// `NeedsReviewCount` (ADR 0032): die Zahl der zu prüfenden Buchungen im Filter
/// (Konto oder alle), unabhängig von `OnlyNeedsReview`: dasselbe Muster wie
/// `DeletedCount`/`IncludeDeleted`.
type LedgerEntryPage = {
    Entries: LedgerEntryView list
    TotalCount: int
    TotalAmount: Money
    DeletedCount: int
    NeedsReviewCount: int
}

/// Ein Payee-Vorschlag mit seiner Anzahl **innerhalb des übergebenen Filters**
/// (ledger-vgfw3, ADR 0036) — nicht über alle Buchungen und nicht über die geladene
/// Seite, sondern über den gesamten Treffer der anderen aktiven Filter. Grundlage
/// der Payee-Auswahl (Handoff Ablauf 2, Screens 2.7/2.13: "Vorschläge aus den 148
/// Treffern").
type LedgerPayeeSuggestion = {
    Payee: string
    Count: int
}

/// Die Filter für die Payee-Vorschlagsabfrage: dieselben Such-Filter wie
/// `LedgerEntryPageQuery`, ohne Seiten-Parameter (eine Vorschlagsliste hat keine
/// Seite) und ohne `Payee` selbst — der ist ja gerade das, was gleich ausgewählt
/// wird.
type LedgerPayeeSuggestionQuery = {
    AccountId: Guid option
    IncludeDeleted: bool
    OnlyNeedsReview: bool
    /// Chip "Ohne Kategorie" (ADR 0048): nur Buchungen ohne Ziel (weder Kategorie noch
    /// Übertrag). Mit nicht leeren `CategoryIds` gilt die Vereinigung beider Mengen.
    WithoutCategory: bool
    CategoryIds: Guid list
    DateFrom: DateTime option
    DateTo: DateTime option
    Memo: string option
    AmountMin: decimal option
    AmountMax: decimal option
}

// ============================================
// Prüfvermerk für eine ganze Auswahl (ledger-nhwrt)
// ============================================

/// Die Auswahl, auf die der Stapel-Befehl wirkt. `BatchPicked`: die Ids der gewählten
/// Zeilen. `BatchAllExcept`: der ganze Treffer einer Seitenanfrage (dieselbe Filter-
/// Beschreibung wie `getLedgerEntriesPage`, Seite und Versatz zählen nicht) ohne die
/// abgewählten Ids. Der Server löst die Menge selbst auf, weil der Client die Ids nicht
/// geladener Buchungen nicht kennt.
type ReviewMarkBatchSelection =
    | BatchPicked of Guid list
    | BatchAllExcept of query: LedgerEntryPageQuery * excluded: Guid list

/// Eine Buchung, deren Prüfvermerk der Stapel geändert hat: ihr Vorzustand (für das
/// gemeinsame "Rückgängig") und das Ergebnis des Sammel-PATCH für sie.
type ReviewMarkBatchChange = {
    EntryId: Guid
    Previous: ReviewMarkState
    WriteBack: ReviewMarkWriteBack
}

/// Antwort der Stapel-Befehle. `Changed`: je geänderter Buchung ein Eintrag.
/// `UnchangedCount`: Ziel gleich Ist (`NoChange`, kein Ereignis, nicht im PATCH).
/// `FailedCount`: in YNAB gelöscht, nicht mit YNAB verknüpft oder unbekannt (kein
/// Ereignis, nicht im PATCH).
type ReviewMarkBatchOutcome = {
    Changed: ReviewMarkBatchChange list
    UnchangedCount: int
    FailedCount: int
}

/// Eine Buchung mit dem Prüfvermerk, auf den "Rückgängig" sie zurückstellen soll.
type ReviewMarkRestoreItem = {
    EntryId: Guid
    Previous: ReviewMarkState
}

/// Der Rohwert einer Buchung, wie ihn die Kontostaende-Abfrage braucht: Konto, Betrag
/// und ob sie in YNAB geloescht ist. Bewusst kein `LedgerEntryView`: die
/// Kontostaende-Leiste laedt nicht die volle Buchungsliste, sie braucht nur so viel,
/// wie der Kontostand verlangt (ADR 0016: Summe der nicht geloeschten Buchungen).
type LedgerBalanceInput = {
    AccountId: Guid
    Amount: Money
    DeletedInYnab: bool
}

/// Ein Konto mit seinem Kontostand, wie die Kontostaende-Leiste ihn braucht.
type LedgerAccountBalanceView = {
    AccountId: Guid
    Balance: Money
}

/// Die Kontostaende aller Konten in einem Durchlauf: je Konto die Summe seiner nicht
/// geloeschten Betraege, in F# gebildet (ADR 0017). Ein Konto ohne (zaehlende)
/// Buchungen fehlt in der Rueckgabe-Map; der Aufrufer ergaenzt es mit einem
/// Null-Stand — diese Funktion kennt die Konten-Liste selbst nicht.
///
/// Der Stand ist unabhaengig davon, wie viele Seiten der Buchungsliste geladen sind:
/// er wird immer aus **allen** gespeicherten Buchungen gebildet, nie aus dem gerade
/// sichtbaren Ausschnitt (Akzeptanzkriterium ledger-b1xwe).
let ledgerBalancesByAccount (fallbackCurrency: string) (inputs: LedgerBalanceInput list) : Map<Guid, Money> =
    inputs
    |> List.filter (fun i -> not i.DeletedInYnab)
    |> List.groupBy (fun i -> i.AccountId)
    |> List.map (fun (accountId, group) -> accountId, sumMoney fallbackCurrency (group |> List.map (fun i -> i.Amount)))
    |> Map.ofList
