module Shared.Api

open Domain

// ============================================
// Settings API
// ============================================

/// API contract for application settings management.
/// Purpose: Persists encrypted credentials and validates external API connections.
type SettingsApi = {
    /// Retrieves current application settings from database.
    /// Returns: Complete settings object with encrypted secrets decrypted.
    getSettings: unit -> Async<AppSettings>

    /// Validates and persists YNAB Personal Access Token.
    /// Purpose: Tests token validity before storage to prevent invalid configurations.
    /// Returns: Unit on success or SettingsError (YnabTokenInvalid, YnabConnectionFailed).
    saveYnabToken: string -> Async<SettingsResult<unit>>

    /// Encrypts and stores Comdirect OAuth credentials.
    /// Purpose: Securely persists bank API credentials for automatic syncs.
    /// Returns: Unit on success or SettingsError (EncryptionFailed, DatabaseError).
    saveComdirectCredentials: ComdirectSettings -> Async<SettingsResult<unit>>

    /// Updates sync behavior settings (e.g., days to fetch).
    /// Purpose: Allows user to customize transaction fetch window.
    saveSyncSettings: SyncSettings -> Async<SettingsResult<unit>>

    /// Tests YNAB connection and returns available budgets.
    /// Purpose: Validates credentials and populates budget selection UI.
    /// Returns: List of budgets or SettingsError (YnabConnectionFailed).
    testYnabConnection: unit -> Async<SettingsResult<YnabBudgetWithAccounts list>>
}

// ============================================
// YNAB API
// ============================================

/// API contract for YNAB integration operations.
/// Purpose: Manages communication with YNAB API for budget/category/account retrieval.
type YnabApi = {
    /// Fetches all budgets accessible with the configured token.
    /// Returns: List of budgets or YnabError (Unauthorized, NetworkError).
    getBudgets: unit -> Async<YnabResult<YnabBudget list>>

    /// Retrieves detailed information for a specific budget including accounts and categories.
    /// Purpose: Populates UI dropdowns and validates configuration.
    /// Returns: Budget details or YnabError (BudgetNotFound, Unauthorized).
    getBudgetDetails: YnabBudgetId -> Async<YnabResult<YnabBudgetWithAccounts>>

    /// Fetches all categories for categorization rules.
    /// Purpose: Validates rules engine can map patterns to valid categories.
    /// Returns: Category list or YnabError (BudgetNotFound, NetworkError).
    getCategories: YnabBudgetId -> Async<YnabResult<YnabCategory list>>

    /// Persists the default budget selection for sync operations.
    /// Purpose: Simplifies UX by pre-selecting budget for imports.
    setDefaultBudget: YnabBudgetId -> Async<YnabResult<unit>>

    /// Persists the default account for transaction imports.
    /// Purpose: All Comdirect transactions import to this YNAB account.
    setDefaultAccount: YnabAccountId -> Async<YnabResult<unit>>
}

// ============================================
// Rules API
// ============================================

/// API contract for categorization rules management.
/// Purpose: CRUD operations for rules engine and pattern testing.
type RulesApi = {
    /// Retrieves all rules ordered by priority.
    /// Returns: Complete list (never fails - returns empty list if no rules).
    getAllRules: unit -> Async<Rule list>

    /// Fetches a single rule by ID.
    /// Returns: Rule or RulesError (RuleNotFound, DatabaseError).
    getRule: RuleId -> Async<RulesResult<Rule>>

    /// Creates a new categorization rule after validation.
    /// Purpose: Validates pattern compiles and category exists before persistence.
    /// Returns: Created rule or RulesError (InvalidPattern, CategoryNotFound, DuplicateRule).
    createRule: RuleCreateRequest -> Async<RulesResult<Rule>>

    /// Updates an existing rule.
    /// Purpose: Applies partial updates while maintaining rule priority order.
    /// Returns: Updated rule or RulesError (RuleNotFound, InvalidPattern).
    updateRule: RuleUpdateRequest -> Async<RulesResult<Rule>>

    /// Deletes a rule permanently.
    /// Returns: Unit on success or RulesError (RuleNotFound, DatabaseError).
    deleteRule: RuleId -> Async<RulesResult<unit>>

    /// Reorders rules by updating their priorities.
    /// Purpose: Affects which rule matches first during categorization.
    /// Returns: Unit on success or RulesError (RuleNotFound for any missing ID).
    reorderRules: RuleId list -> Async<RulesResult<unit>>

    /// Exports all rules as JSON for backup/sharing.
    /// Returns: JSON string (never fails - returns empty array if no rules).
    exportRules: unit -> Async<string>

    /// Imports rules from JSON, validating and creating them.
    /// Returns: Count of successfully imported rules or RulesError (parsing/validation errors).
    importRules: string -> Async<RulesResult<int>>

    /// Tests if a pattern matches input text without persisting.
    /// Purpose: Allows users to validate regex before creating rule.
    /// Parameters: (pattern, patternType, targetField, testInput)
    /// Returns: True if pattern matches test input, false otherwise.
    testRule: string * PatternType * TargetField * string -> Async<bool>
}

// ============================================
// Sync Flow API
// ============================================

/// API contract for the complete sync workflow.
/// Purpose: Orchestrates multi-step process from bank auth to YNAB import.
type SyncApi = {
    // ============================================
    // Session management
    // ============================================

    /// Initiates a new sync session.
    /// Purpose: Creates session record and begins Comdirect OAuth flow.
    /// Returns: New session or SyncError (ComdirectAuthFailed, DatabaseError).
    startSync: unit -> Async<SyncResult<SyncSession>>

    /// Retrieves the currently active sync session if one exists.
    /// Purpose: Allows UI to resume interrupted sync or show progress.
    getCurrentSession: unit -> Async<SyncSession option>

    /// Cancels an active sync session and cleans up auth state.
    /// Purpose: Allows user to abort sync and start fresh.
    /// Returns: Unit on success or SyncError (SessionNotFound).
    cancelSync: SyncSessionId -> Async<SyncResult<unit>>

    // ============================================
    // Comdirect auth flow
    // ============================================

    /// Starts Comdirect OAuth and requests Push-TAN challenge.
    /// Purpose: Initiates async TAN confirmation on user's phone.
    /// Returns: Challenge ID/info to display to user or SyncError (ComdirectAuthFailed).
    initiateComdirectAuth: SyncSessionId -> Async<SyncResult<string>>

    /// Completes TAN flow after user confirms on phone.
    /// Purpose: Fetches transactions and applies rules engine after auth succeeds.
    /// Returns: Unit on success or SyncError (TanTimeout, TransactionFetchFailed).
    confirmTan: SyncSessionId -> Async<SyncResult<unit>>

    // ============================================
    // Transaction operations
    // ============================================

    /// Retrieves all transactions for a sync session.
    /// Purpose: Populates transaction review UI with categorized data.
    /// Returns: Transaction list or SyncError (SessionNotFound, InvalidSessionState).
    getTransactions: SyncSessionId -> Async<SyncResult<SyncTransaction list>>

    /// Manually categorizes a single transaction.
    /// Purpose: Allows user to override auto-categorization or categorize uncategorized.
    /// Parameters: (sessionId, transactionId, categoryId, payeeOverride)
    /// Returns: Updated transaction or SyncError (SessionNotFound, InvalidSessionState).
    categorizeTransaction: SyncSessionId * TransactionId * YnabCategoryId option * string option -> Async<SyncResult<SyncTransaction>>

    /// Marks a transaction to be skipped during import.
    /// Purpose: Excludes duplicates or unwanted transactions.
    /// Returns: Updated transaction or SyncError (SessionNotFound).
    skipTransaction: SyncSessionId * TransactionId -> Async<SyncResult<SyncTransaction>>

    /// Restores a skipped transaction to active status.
    /// Purpose: Allows user to re-include a previously skipped transaction.
    /// Returns: Updated transaction or SyncError (SessionNotFound).
    unskipTransaction: SyncSessionId * TransactionId -> Async<SyncResult<SyncTransaction>>

    /// Categorizes multiple transactions at once.
    /// Purpose: Bulk operation for efficiency when user selects multiple.
    /// Returns: Updated transactions or SyncError (SessionNotFound, InvalidSessionState).
    bulkCategorize: SyncSessionId * TransactionId list * YnabCategoryId -> Async<SyncResult<SyncTransaction list>>

    /// Splits a transaction into multiple categories.
    /// Purpose: Allows allocating a single transaction across multiple YNAB categories.
    /// Parameters: (sessionId, transactionId, splits)
    /// Constraints: Splits must have at least 2 items and their amounts must sum to the transaction total.
    /// Returns: Updated transaction with splits or SyncError (SessionNotFound, InvalidSessionState).
    splitTransaction: SyncSessionId * TransactionId * TransactionSplit list -> Async<SyncResult<SyncTransaction>>

    /// Clears splits from a transaction, reverting to single-category mode.
    /// Purpose: Allows user to undo a split and use regular categorization.
    /// Returns: Updated transaction without splits or SyncError (SessionNotFound).
    clearSplit: SyncSessionId * TransactionId -> Async<SyncResult<SyncTransaction>>

    // ============================================
    // Import
    // ============================================

    /// Sends all categorized transactions to YNAB.
    /// Purpose: Final step that creates transactions in YNAB budget.
    /// Returns: Count of successfully imported transactions or SyncError (YnabImportFailed).
    importToYnab: SyncSessionId -> Async<SyncResult<int>>

    // ============================================
    // History
    // ============================================

    /// Retrieves recent sync sessions for history display.
    /// Returns: Most recent N sessions (never fails - returns empty list).
    getSyncHistory: int -> Async<SyncSession list>
}

// Note: Each API (SettingsApi, YnabApi, RulesApi, SyncApi) is registered
// as a separate Fable.Remoting endpoint. See Server/Api.fs for implementation.
