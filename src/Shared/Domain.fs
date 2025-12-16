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

/// Represents a single split within a transaction for multi-category allocation.
/// Purpose: Allows splitting a single bank transaction into multiple YNAB categories.
type TransactionSplit = {
    /// The category to assign to this split
    CategoryId: YnabCategoryId
    /// Cached category name for display
    CategoryName: string
    /// The amount for this split (must sum to transaction total)
    Amount: Money
    /// Optional memo for this specific split
    Memo: string option
}

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
}

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
}

type YnabCategory = {
    Id: YnabCategoryId
    Name: string
    GroupName: string
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
type ComdirectResult<'T> = Result<'T, ComdirectError>
