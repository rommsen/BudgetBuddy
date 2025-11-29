module Persistence

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Data
open Microsoft.Data.Sqlite
open Dapper
open Shared.Domain

// ============================================
// Dapper TypeHandler for F# Option
// ============================================

type OptionHandler<'T>() =
    inherit SqlMapper.TypeHandler<'T option>()

    override _.SetValue(param, value) =
        let valueOrNull =
            match value with
            | Some x -> box x
            | None -> null

        param.Value <- valueOrNull

    override _.Parse value =
        if isNull value || value = box DBNull.Value then
            None
        else
            Some (value :?> 'T)

// Register the option handler for common types
do
    SqlMapper.AddTypeHandler(OptionHandler<string>())
    SqlMapper.AddTypeHandler(OptionHandler<DateTime>())

// ============================================
// Configuration
// ============================================

// Data directory is configurable via DATA_DIR environment variable
let private dataDir =
    match Environment.GetEnvironmentVariable("DATA_DIR") with
    | null | "" -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "heimeshoff", "budgetbuddy")
    | path -> path

/// Ensure the data directory exists
let ensureDataDir () =
    if not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore

let private dbPath = Path.Combine(dataDir, "budgetbuddy.db")
let private connectionString = $"Data Source={dbPath}"

let private getConnection () = new SqliteConnection(connectionString)

// ============================================
// Encryption Helper
// ============================================

module Encryption =
    // Use a machine-specific key derived from environment or machine ID
    // In production, this should be stored securely (e.g., Azure Key Vault, environment variable)
    let private getEncryptionKey () =
        let keyEnv = Environment.GetEnvironmentVariable("BUDGETBUDDY_ENCRYPTION_KEY")
        if String.IsNullOrWhiteSpace(keyEnv) then
            // Fallback: derive from machine name (not ideal for production)
            let machineKey = Environment.MachineName + "BudgetBuddy2025"
            use sha = SHA256.Create()
            sha.ComputeHash(Encoding.UTF8.GetBytes(machineKey))
        else
            Convert.FromBase64String(keyEnv)

    /// Encrypts a plaintext string using AES-256
    let encrypt (plaintext: string) : Result<string, string> =
        try
            if String.IsNullOrEmpty(plaintext) then
                Ok ""
            else
                use aes = Aes.Create()
                aes.Key <- getEncryptionKey()
                aes.GenerateIV()

                use encryptor = aes.CreateEncryptor(aes.Key, aes.IV)
                use msEncrypt = new MemoryStream()
                use csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write)
                use swEncrypt = new StreamWriter(csEncrypt)
                swEncrypt.Write(plaintext)
                swEncrypt.Close()

                let encrypted = msEncrypt.ToArray()
                let combined = Array.append aes.IV encrypted
                Ok (Convert.ToBase64String(combined))
        with ex ->
            Error $"Encryption failed: {ex.Message}"

    /// Decrypts an encrypted string using AES-256
    let decrypt (ciphertext: string) : Result<string, string> =
        try
            if String.IsNullOrEmpty(ciphertext) then
                Ok ""
            else
                let combined = Convert.FromBase64String(ciphertext)

                use aes = Aes.Create()
                aes.Key <- getEncryptionKey()

                let iv = combined.[0..15]
                let encrypted = combined.[16..]

                use decryptor = aes.CreateDecryptor(aes.Key, iv)
                use msDecrypt = new MemoryStream(encrypted)
                use csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read)
                use srDecrypt = new StreamReader(csDecrypt)

                Ok (srDecrypt.ReadToEnd())
        with ex ->
            Error $"Decryption failed: {ex.Message}"

// ============================================
// Database Initialization
// ============================================

let initializeDatabase () =
    ensureDataDir()
    use conn = getConnection()
    conn.Open()

    // Rules table
    use cmd1 = new SqliteCommand("""
        CREATE TABLE IF NOT EXISTS rules (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            pattern TEXT NOT NULL,
            pattern_type TEXT NOT NULL,
            target_field TEXT NOT NULL,
            category_id TEXT NOT NULL,
            category_name TEXT NOT NULL,
            payee_override TEXT,
            priority INTEGER NOT NULL DEFAULT 0,
            enabled INTEGER NOT NULL DEFAULT 1,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_rules_priority ON rules(priority DESC);
    """, conn)
    cmd1.ExecuteNonQuery() |> ignore

    // Settings table
    use cmd2 = new SqliteCommand("""
        CREATE TABLE IF NOT EXISTS settings (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL,
            encrypted INTEGER NOT NULL DEFAULT 0,
            updated_at TEXT NOT NULL
        );
    """, conn)
    cmd2.ExecuteNonQuery() |> ignore

    // Sync sessions table
    use cmd3 = new SqliteCommand("""
        CREATE TABLE IF NOT EXISTS sync_sessions (
            id TEXT PRIMARY KEY,
            started_at TEXT NOT NULL,
            completed_at TEXT,
            status TEXT NOT NULL,
            transaction_count INTEGER NOT NULL DEFAULT 0,
            imported_count INTEGER NOT NULL DEFAULT 0,
            skipped_count INTEGER NOT NULL DEFAULT 0
        );

        CREATE INDEX IF NOT EXISTS idx_sync_sessions_started ON sync_sessions(started_at DESC);
    """, conn)
    cmd3.ExecuteNonQuery() |> ignore

    // Sync transactions table
    use cmd4 = new SqliteCommand("""
        CREATE TABLE IF NOT EXISTS sync_transactions (
            id TEXT PRIMARY KEY,
            session_id TEXT NOT NULL,
            transaction_id TEXT NOT NULL,
            booking_date TEXT NOT NULL,
            amount REAL NOT NULL,
            currency TEXT NOT NULL,
            payee TEXT,
            memo TEXT NOT NULL,
            reference TEXT NOT NULL,
            status TEXT NOT NULL,
            category_id TEXT,
            category_name TEXT,
            matched_rule_id TEXT,
            payee_override TEXT,
            created_at TEXT NOT NULL,
            FOREIGN KEY (session_id) REFERENCES sync_sessions(id)
        );

        CREATE INDEX IF NOT EXISTS idx_sync_transactions_session ON sync_transactions(session_id);
    """, conn)
    cmd4.ExecuteNonQuery() |> ignore

// ============================================
// Rules Persistence
// ============================================

module Rules =
    [<CLIMutable>]
    type RuleRow = {
        id: string
        name: string
        pattern: string
        pattern_type: string
        target_field: string
        category_id: string
        category_name: string
        payee_override: string option
        priority: int
        enabled: int
        created_at: string
        updated_at: string
    }

    let private patternTypeToString = function
        | Regex -> "Regex"
        | Contains -> "Contains"
        | Exact -> "Exact"

    let private patternTypeFromString = function
        | "Regex" -> Regex
        | "Contains" -> Contains
        | "Exact" -> Exact
        | _ -> Regex

    let private targetFieldToString = function
        | Payee -> "Payee"
        | Memo -> "Memo"
        | Combined -> "Combined"

    let private targetFieldFromString = function
        | "Payee" -> Payee
        | "Memo" -> Memo
        | "Combined" -> Combined
        | _ -> Combined

    let private rowToRule (row: RuleRow) : Rule =
        let (RuleId ruleId) = RuleId (Guid.Parse row.id)
        let (YnabCategoryId categoryId) = YnabCategoryId (Guid.Parse row.category_id)
        {
            Id = RuleId ruleId
            Name = row.name
            Pattern = row.pattern
            PatternType = patternTypeFromString row.pattern_type
            TargetField = targetFieldFromString row.target_field
            CategoryId = YnabCategoryId categoryId
            CategoryName = row.category_name
            PayeeOverride = row.payee_override
            Priority = row.priority
            Enabled = row.enabled = 1
            CreatedAt = DateTime.Parse row.created_at
            UpdatedAt = DateTime.Parse row.updated_at
        }

    let getAllRules () : Async<Rule list> =
        async {
            use conn = getConnection()
            let! rows = conn.QueryAsync<RuleRow>("SELECT * FROM rules ORDER BY priority DESC") |> Async.AwaitTask
            return rows |> Seq.map rowToRule |> Seq.toList
        }

    let getRuleById (RuleId ruleId: RuleId) : Async<Rule option> =
        async {
            use conn = getConnection()
            let! row =
                conn.QueryFirstOrDefaultAsync<RuleRow>(
                    "SELECT * FROM rules WHERE id = @Id",
                    {| Id = ruleId.ToString() |}
                ) |> Async.AwaitTask
            return if isNull (box row) then None else Some (rowToRule row)
        }

    let insertRule (rule: Rule) : Async<unit> =
        async {
            use conn = getConnection()
            let (RuleId ruleId) = rule.Id
            let (YnabCategoryId categoryId) = rule.CategoryId
            do! (conn.ExecuteAsync(
                """INSERT INTO rules
                   (id, name, pattern, pattern_type, target_field, category_id, category_name,
                    payee_override, priority, enabled, created_at, updated_at)
                   VALUES (@id, @name, @pattern, @pattern_type, @target_field, @category_id,
                           @category_name, @payee_override, @priority, @enabled, @created_at, @updated_at)""",
                {|
                    id = ruleId.ToString()
                    name = rule.Name
                    pattern = rule.Pattern
                    pattern_type = patternTypeToString rule.PatternType
                    target_field = targetFieldToString rule.TargetField
                    category_id = categoryId.ToString()
                    category_name = rule.CategoryName
                    payee_override = rule.PayeeOverride
                    priority = rule.Priority
                    enabled = if rule.Enabled then 1 else 0
                    created_at = rule.CreatedAt.ToString("O")
                    updated_at = rule.UpdatedAt.ToString("O")
                |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let updateRule (rule: Rule) : Async<unit> =
        async {
            use conn = getConnection()
            let (RuleId ruleId) = rule.Id
            let (YnabCategoryId categoryId) = rule.CategoryId
            do! (conn.ExecuteAsync(
                """UPDATE rules
                   SET name = @name, pattern = @pattern, pattern_type = @pattern_type,
                       target_field = @target_field, category_id = @category_id,
                       category_name = @category_name, payee_override = @payee_override,
                       priority = @priority, enabled = @enabled, updated_at = @updated_at
                   WHERE id = @id""",
                {|
                    id = ruleId.ToString()
                    name = rule.Name
                    pattern = rule.Pattern
                    pattern_type = patternTypeToString rule.PatternType
                    target_field = targetFieldToString rule.TargetField
                    category_id = categoryId.ToString()
                    category_name = rule.CategoryName
                    payee_override = rule.PayeeOverride
                    priority = rule.Priority
                    enabled = if rule.Enabled then 1 else 0
                    updated_at = rule.UpdatedAt.ToString("O")
                |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let deleteRule (RuleId ruleId: RuleId) : Async<unit> =
        async {
            use conn = getConnection()
            do! (conn.ExecuteAsync(
                "DELETE FROM rules WHERE id = @Id",
                {| Id = ruleId.ToString() |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let updatePriorities (ruleIds: RuleId list) : Async<unit> =
        async {
            use conn = getConnection()
            conn.Open()
            use transaction = conn.BeginTransaction()

            try
                for i, (RuleId ruleId) in List.indexed ruleIds do
                    let priority = List.length ruleIds - i
                    do! (conn.ExecuteAsync(
                        "UPDATE rules SET priority = @priority WHERE id = @id",
                        {| id = ruleId.ToString(); priority = priority |},
                        transaction
                    ) |> Async.AwaitTask |> Async.Ignore)

                transaction.Commit()
            with ex ->
                transaction.Rollback()
                raise ex
        }

// ============================================
// Settings Persistence
// ============================================

module Settings =
    let getSetting (key: string) : Async<string option> =
        async {
            use conn = getConnection()
            let! row =
                conn.QueryFirstOrDefaultAsync<{| value: string; encrypted: int |}>(
                    "SELECT value, encrypted FROM settings WHERE key = @Key",
                    {| Key = key |}
                ) |> Async.AwaitTask

            if isNull (box row) then
                return None
            else
                if row.encrypted = 1 then
                    match Encryption.decrypt row.value with
                    | Ok decrypted -> return Some decrypted
                    | Error _ -> return None
                else
                    return Some row.value
        }

    let setSetting (key: string) (value: string) (encrypted: bool) : Async<unit> =
        async {
            use conn = getConnection()

            let valueToStore =
                if encrypted then
                    match Encryption.encrypt value with
                    | Ok encrypted -> encrypted
                    | Error err -> failwith err
                else
                    value

            do! (conn.ExecuteAsync(
                """INSERT INTO settings (key, value, encrypted, updated_at)
                   VALUES (@key, @value, @encrypted, @updated_at)
                   ON CONFLICT(key) DO UPDATE SET value = @value, encrypted = @encrypted, updated_at = @updated_at""",
                {|
                    key = key
                    value = valueToStore
                    encrypted = if encrypted then 1 else 0
                    updated_at = DateTime.UtcNow.ToString("O")
                |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let deleteSetting (key: string) : Async<unit> =
        async {
            use conn = getConnection()
            do! (conn.ExecuteAsync(
                "DELETE FROM settings WHERE key = @Key",
                {| Key = key |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

// ============================================
// Sync Sessions Persistence
// ============================================

module SyncSessions =
    [<CLIMutable>]
    type SyncSessionRow = {
        id: string
        started_at: string
        completed_at: string option
        status: string
        transaction_count: int
        imported_count: int
        skipped_count: int
    }

    let private statusToString = function
        | AwaitingBankAuth -> "AwaitingBankAuth"
        | AwaitingTan -> "AwaitingTan"
        | FetchingTransactions -> "FetchingTransactions"
        | ReviewingTransactions -> "ReviewingTransactions"
        | ImportingToYnab -> "ImportingToYnab"
        | Completed -> "Completed"
        | Failed msg -> $"Failed:{msg}"

    let private statusFromString (s: string) : SyncSessionStatus =
        if s.StartsWith("Failed:") then
            Failed (s.Substring(7))
        else
            match s with
            | "AwaitingBankAuth" -> AwaitingBankAuth
            | "AwaitingTan" -> AwaitingTan
            | "FetchingTransactions" -> FetchingTransactions
            | "ReviewingTransactions" -> ReviewingTransactions
            | "ImportingToYnab" -> ImportingToYnab
            | "Completed" -> Completed
            | _ -> Failed "Unknown status"

    let private rowToSession (row: SyncSessionRow) : SyncSession =
        {
            Id = SyncSessionId (Guid.Parse row.id)
            StartedAt = DateTime.Parse row.started_at
            CompletedAt = row.completed_at |> Option.map DateTime.Parse
            Status = statusFromString row.status
            TransactionCount = row.transaction_count
            ImportedCount = row.imported_count
            SkippedCount = row.skipped_count
        }

    let createSession (session: SyncSession) : Async<unit> =
        async {
            use conn = getConnection()
            let (SyncSessionId sessionId) = session.Id
            do! (conn.ExecuteAsync(
                """INSERT INTO sync_sessions
                   (id, started_at, completed_at, status, transaction_count, imported_count, skipped_count)
                   VALUES (@id, @started_at, @completed_at, @status, @transaction_count, @imported_count, @skipped_count)""",
                {|
                    id = sessionId.ToString()
                    started_at = session.StartedAt.ToString("O")
                    completed_at = session.CompletedAt |> Option.map (fun d -> d.ToString("O"))
                    status = statusToString session.Status
                    transaction_count = session.TransactionCount
                    imported_count = session.ImportedCount
                    skipped_count = session.SkippedCount
                |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let updateSession (session: SyncSession) : Async<unit> =
        async {
            use conn = getConnection()
            let (SyncSessionId sessionId) = session.Id
            do! (conn.ExecuteAsync(
                """UPDATE sync_sessions
                   SET completed_at = @completed_at, status = @status,
                       transaction_count = @transaction_count, imported_count = @imported_count,
                       skipped_count = @skipped_count
                   WHERE id = @id""",
                {|
                    id = sessionId.ToString()
                    completed_at = session.CompletedAt |> Option.map (fun d -> d.ToString("O"))
                    status = statusToString session.Status
                    transaction_count = session.TransactionCount
                    imported_count = session.ImportedCount
                    skipped_count = session.SkippedCount
                |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let getRecentSessions (count: int) : Async<SyncSession list> =
        async {
            use conn = getConnection()
            let! rows =
                conn.QueryAsync<SyncSessionRow>(
                    "SELECT * FROM sync_sessions ORDER BY started_at DESC LIMIT @Count",
                    {| Count = count |}
                ) |> Async.AwaitTask
            return rows |> Seq.map rowToSession |> Seq.toList
        }

    let getSessionById (SyncSessionId sessionId: SyncSessionId) : Async<SyncSession option> =
        async {
            use conn = getConnection()
            let! row =
                conn.QueryFirstOrDefaultAsync<SyncSessionRow>(
                    "SELECT * FROM sync_sessions WHERE id = @Id",
                    {| Id = sessionId.ToString() |}
                ) |> Async.AwaitTask
            return if isNull (box row) then None else Some (rowToSession row)
        }

// ============================================
// Sync Transactions Persistence
// ============================================

module SyncTransactions =
    [<CLIMutable>]
    type SyncTransactionRow = {
        id: string
        session_id: string
        transaction_id: string
        booking_date: string
        amount: float
        currency: string
        payee: string option
        memo: string
        reference: string
        status: string
        category_id: string option
        category_name: string option
        matched_rule_id: string option
        payee_override: string option
        created_at: string
    }

    let saveTransaction (sessionId: SyncSessionId) (tx: SyncTransaction) : Async<unit> =
        async {
            use conn = getConnection()
            let (SyncSessionId sid) = sessionId
            let (TransactionId txId) = tx.Transaction.Id

            do! (conn.ExecuteAsync(
                """INSERT OR REPLACE INTO sync_transactions
                   (id, session_id, transaction_id, booking_date, amount, currency, payee, memo,
                    reference, status, category_id, category_name, matched_rule_id, payee_override, created_at)
                   VALUES (@id, @session_id, @transaction_id, @booking_date, @amount, @currency, @payee,
                           @memo, @reference, @status, @category_id, @category_name, @matched_rule_id,
                           @payee_override, @created_at)""",
                {|
                    id = Guid.NewGuid().ToString()
                    session_id = sid.ToString()
                    transaction_id = txId
                    booking_date = tx.Transaction.BookingDate.ToString("O")
                    amount = float tx.Transaction.Amount.Amount
                    currency = tx.Transaction.Amount.Currency
                    payee = tx.Transaction.Payee
                    memo = tx.Transaction.Memo
                    reference = tx.Transaction.Reference
                    status = tx.Status.ToString()
                    category_id = tx.CategoryId |> Option.map (fun (YnabCategoryId id) -> id.ToString())
                    category_name = tx.CategoryName
                    matched_rule_id = tx.MatchedRuleId |> Option.map (fun (RuleId id) -> id.ToString())
                    payee_override = tx.PayeeOverride
                    created_at = DateTime.UtcNow.ToString("O")
                |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let getTransactionsBySession (SyncSessionId sessionId: SyncSessionId) : Async<SyncTransaction list> =
        async {
            use conn = getConnection()
            let! rows =
                conn.QueryAsync<SyncTransactionRow>(
                    "SELECT * FROM sync_transactions WHERE session_id = @SessionId ORDER BY booking_date DESC",
                    {| SessionId = sessionId.ToString() |}
                ) |> Async.AwaitTask

            // Note: This is a simplified conversion. External links and full status parsing would be added here.
            return rows |> Seq.toList |> List.map (fun row ->
                {
                    Transaction = {
                        Id = TransactionId row.transaction_id
                        BookingDate = DateTime.Parse row.booking_date
                        Amount = { Amount = decimal row.amount; Currency = row.currency }
                        Payee = row.payee
                        Memo = row.memo
                        Reference = row.reference
                        RawData = ""
                    }
                    Status =
                        match row.status with
                        | "Pending" -> Pending
                        | "AutoCategorized" -> AutoCategorized
                        | "ManualCategorized" -> ManualCategorized
                        | "NeedsAttention" -> NeedsAttention
                        | "Skipped" -> Skipped
                        | "Imported" -> Imported
                        | _ -> Pending
                    CategoryId = row.category_id |> Option.map (fun id -> YnabCategoryId (Guid.Parse id))
                    CategoryName = row.category_name
                    MatchedRuleId = row.matched_rule_id |> Option.map (fun id -> RuleId (Guid.Parse id))
                    PayeeOverride = row.payee_override
                    ExternalLinks = []
                    UserNotes = None
                }
            )
        }
