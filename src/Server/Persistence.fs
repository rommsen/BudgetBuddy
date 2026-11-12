module Persistence

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Data
open Microsoft.Data.Sqlite
open Dapper
open Thoth.Json.Net
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
    // Nullbare Milliunits der früheren Target-Änderungen (budgeting-k8wq3), gelesen von der Budget-Migration.
    SqlMapper.AddTypeHandler(OptionHandler<int64>())

// ============================================
// Configuration (Lazy-loaded for test isolation)
// ============================================

// Use Lazy<T> to defer configuration loading until first database access
// This allows tests to set USE_MEMORY_DB before any persistence operations
type private DbConfig = {
    IsTestMode: bool
    ConnectionString: string
    SharedConnection: SqliteConnection option
}

let private dbConfig = lazy (
    let isTestMode =
        match Environment.GetEnvironmentVariable("USE_MEMORY_DB") with
        | "true" | "1" -> true
        | _ -> false

    let dataDir =
        match Environment.GetEnvironmentVariable("DATA_DIR") with
        | null | "" -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "my_apps", "budgetbuddy-dev")
        | path -> path

    // Ensure the data directory exists (only for file-based mode)
    if not isTestMode && not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore

    let connectionString =
        if isTestMode then
            "Data Source=:memory:;Mode=Memory;Cache=Shared"
        else
            let dbPath = Path.Combine(dataDir, "budgetbuddy.db")
            $"Data Source={dbPath}"

    // For in-memory mode we keep ONE connection open for the whole process lifetime.
    // It is a keep-alive *anchor* only: SQLite drops a `Mode=Memory;Cache=Shared`
    // database as soon as the last connection to it closes, so this anchor keeps the
    // shared in-memory DB alive between operations. It is NOT handed to callers — see
    // getConnection — because sharing a single SqliteConnection object across the
    // parallel Expecto test cases caused an intermittent index-out-of-range in
    // SqliteConnection.RemoveCommand during command disposal (infra-001).
    let keepAliveConnection =
        if isTestMode then
            let conn = new SqliteConnection(connectionString)
            conn.Open()
            Some conn
        else
            None

    { IsTestMode = isTestMode; ConnectionString = connectionString; SharedConnection = keepAliveConnection }
)

/// Ensure the data directory exists (call this for production mode)
let ensureDataDir () =
    let _ = dbConfig.Force() // This triggers directory creation if needed
    ()

/// Gibt den tatsächlich aufgelösten Connection-String zurück, ohne selbst eine
/// Connection zu öffnen (die Konfiguration ist bereits geladen, sobald irgendein
/// Testfall zuvor die Datenbank angefasst hat; ist sie es noch nicht, löst
/// `dbConfig.Force()` sie genau einmal auf, wie jeder andere Zugriff auch).
/// Wächter gegen ADR 0030: `dotnet test --settings` oder ein direkter
/// `dotnet vstest`-Aufruf umgehen `test.runsettings` stillschweigend und lassen
/// `USE_MEMORY_DB` ungesetzt, siehe TestDatabaseGuardTests.fs.
let activeConnectionString () =
    let config = dbConfig.Force()
    config.ConnectionString

// `internal` statt `private`: `Ledger.Persistence` (gleiche Assembly) baut seinen
// appendAndApply-Schreibpfad auf derselben frischen Connection auf (ADR 0008/0013).
// ADR 0037: SQLites eingebautes `COLLATE NOCASE` faltet nur ASCII A-Z, `lower()`/
// `upper()` sind ebenfalls ASCII-only, und es gibt kein ICU im gebundenen SQLite.
// `ToLowerInvariant` bildet dagegen unicodefähig ab, sowohl unter Globalization-
// Invariant (Container) als auch lokal unter de-DE identisch. Eine Zerlegung in
// Unicode-Normalform darf hier NICHT vorkommen: sie zerlegt im Invariant-Modus
// nicht mehr, ein darauf gebauter Test wäre lokal grün und in Produktion
// wirkungslos (ADR 0037 Context 4).
let internal searchFold (s: string) : string =
    if isNull s then s else s.ToLowerInvariant()

// Registriert `bb_lower` als anwendungsdefinierte SQL-Skalarfunktion auf einer
// noch nicht geöffneten Connection (ADR 0037 Decision 1). `isDeterministic = true`
// ist das dritte Argument von `CreateFunction`. `null` läuft unverändert durch
// `searchFold` und bleibt damit SQL-`NULL`, eine Buchung ohne Payee trifft die
// Gleichheit also weiterhin nicht.
let private withSearchFunctions (conn: SqliteConnection) : SqliteConnection =
    conn.CreateFunction<string, string>("bb_lower", (fun s -> searchFold s), true)
    conn

let internal getConnection () =
    let config = dbConfig.Force()
    // Always hand out a FRESH connection (Dapper auto-opens/closes closed connections).
    // In test mode the keep-alive anchor (config.SharedConnection) keeps the shared
    // in-memory DB alive, but each operation gets its own connection object against the
    // same `Cache=Shared` string — so concurrent test cases never mutate one connection's
    // internal command list at the same time (infra-001: SqliteConnection.RemoveCommand
    // index-out-of-range during disposal). This mirrors how production already behaves.
    let _keepAlive = config.SharedConnection
    // `withSearchFunctions` läuft VOR `Open()` durch `getConnection`: Microsoft.Data.Sqlite
    // legt registrierte Funktionen bei jedem `Open()` neu auf den Handle, über
    // Pool-Zyklen hinweg (ADR 0037). Jede über diesen Flaschenhals ausgegebene
    // Connection trägt `bb_lower` deshalb garantiert.
    new SqliteConnection(config.ConnectionString) |> withSearchFunctions

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

    /// Wahr nur mit gesetztem `BUDGETBUDDY_ENCRYPTION_KEY`. Für Werte, die der Maschinen-Fallback
    /// nicht schützen darf (ADR 0071 Punkt 4). Leer zählt als nicht gesetzt, dann griffe der
    /// Fallback. Die Umgebung ist eingespeist, weil Tests sie nicht umstellen dürfen.
    let hasConfiguredKey (readEnvironment: string -> string) : bool =
        not (String.IsNullOrWhiteSpace (readEnvironment "BUDGETBUDDY_ENCRYPTION_KEY"))

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
    let conn = getConnection()
    if conn.State <> Data.ConnectionState.Open then conn.Open()

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
            review_steps TEXT,
            FOREIGN KEY (session_id) REFERENCES sync_sessions(id)
        );

        CREATE INDEX IF NOT EXISTS idx_sync_transactions_session ON sync_transactions(session_id);
    """, conn)
    cmd4.ExecuteNonQuery() |> ignore

    // Migration: `review_steps` (categorization-gdrx8, Review-Spur) for databases
    // created before this column existed. SQLite has no "ADD COLUMN IF NOT EXISTS";
    // the CREATE TABLE above already covers fresh databases, so this only ever fires
    // once against a pre-existing file and is a no-op afterwards.
    try
        use cmdMigrateReviewSteps =
            new SqliteCommand("ALTER TABLE sync_transactions ADD COLUMN review_steps TEXT", conn)
        cmdMigrateReviewSteps.ExecuteNonQuery() |> ignore
    with :? SqliteException as ex when ex.Message.Contains("duplicate column name") ->
        ()

    // banking-import-d0fey (ADR 0093 Punkt 10): Auslöser und Zahlen des Abrufs mit Buchen.
    // Bestandszeilen bekommen 0 und keinen Auslöser (gelesen als `Manual`).
    for statement in [
        "ALTER TABLE sync_sessions ADD COLUMN triggered_by TEXT"
        "ALTER TABLE sync_sessions ADD COLUMN new_count INTEGER NOT NULL DEFAULT 0"
        "ALTER TABLE sync_sessions ADD COLUMN by_rule_count INTEGER NOT NULL DEFAULT 0"
        "ALTER TABLE sync_sessions ADD COLUMN by_order_id_count INTEGER NOT NULL DEFAULT 0"
        "ALTER TABLE sync_sessions ADD COLUMN without_category_count INTEGER NOT NULL DEFAULT 0"
        "ALTER TABLE sync_sessions ADD COLUMN safe_duplicate_count INTEGER NOT NULL DEFAULT 0"
        "ALTER TABLE sync_sessions ADD COLUMN held_count INTEGER NOT NULL DEFAULT 0"
        "ALTER TABLE sync_sessions ADD COLUMN not_in_ynab_count INTEGER NOT NULL DEFAULT 0"
        // Review-Fund zu banking-import-d0fey: Umsätze mit Datum nach dem Berliner Tag.
        "ALTER TABLE sync_sessions ADD COLUMN future_dated_count INTEGER NOT NULL DEFAULT 0"
    ] do
        try
            use cmdMigrate = new SqliteCommand(statement, conn)
            cmdMigrate.ExecuteNonQuery() |> ignore
        with :? SqliteException as ex when ex.Message.Contains("duplicate column name") ->
            ()

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
        | unknown -> failwith $"Unbekannter PatternType: {unknown}"

    let private targetFieldToString = function
        | Payee -> "Payee"
        | Memo -> "Memo"
        | Combined -> "Combined"

    let private targetFieldFromString = function
        | "Payee" -> Payee
        | "Memo" -> Memo
        | "Combined" -> Combined
        | unknown -> failwith $"Unbekanntes TargetField: {unknown}"

    let internal rowToRule (row: RuleRow) : Rule =
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
            let conn = getConnection()
            let! rows = conn.QueryAsync<RuleRow>("SELECT * FROM rules ORDER BY priority DESC, rowid ASC") |> Async.AwaitTask
            return rows |> Seq.map rowToRule |> Seq.toList
        }

    let getRuleById (RuleId ruleId: RuleId) : Async<Rule option> =
        async {
            let conn = getConnection()
            let! row =
                conn.QueryFirstOrDefaultAsync<RuleRow>(
                    "SELECT * FROM rules WHERE id = @Id",
                    {| Id = ruleId.ToString() |}
                ) |> Async.AwaitTask
            return if isNull (box row) then None else Some (rowToRule row)
        }

    let private insertRuleSql =
        """INSERT INTO rules
           (id, name, pattern, pattern_type, target_field, category_id, category_name,
            payee_override, priority, enabled, created_at, updated_at)
           VALUES (@id, @name, @pattern, @pattern_type, @target_field, @category_id,
                   @category_name, @payee_override, @priority, @enabled, @created_at, @updated_at)"""

    let private insertRuleParameters (rule: Rule) =
        let (RuleId ruleId) = rule.Id
        let (YnabCategoryId categoryId) = rule.CategoryId
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

    let insertRule (rule: Rule) : Async<unit> =
        async {
            let conn = getConnection()
            do! (conn.ExecuteAsync(insertRuleSql, insertRuleParameters rule) |> Async.AwaitTask |> Async.Ignore)
        }

    /// Legt alle Regeln in einer Transaktion an: scheitert eine, bleibt keine (Import).
    /// Die Reihenfolge der Liste ist die rowid-Reihenfolge, also der Gleichstand bei gleicher Priorität.
    let insertRules (rules: Rule list) : Async<unit> =
        async {
            use conn = getConnection()
            conn.Open()
            use transaction = conn.BeginTransaction()

            for rule in rules do
                do! (conn.ExecuteAsync(insertRuleSql, insertRuleParameters rule, transaction)
                     |> Async.AwaitTask |> Async.Ignore)

            transaction.Commit()
        }

    /// Die Priorität einer neuen Regel: höchste vorhandene plus 1, sie steht damit ganz vorn.
    let nextPriority () : Async<int> =
        async {
            let conn = getConnection()
            return! conn.ExecuteScalarAsync<int>("SELECT COALESCE(MAX(priority), 0) + 1 FROM rules") |> Async.AwaitTask
        }

    let updateRule (rule: Rule) : Async<unit> =
        async {
            let conn = getConnection()
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
            let conn = getConnection()
            do! (conn.ExecuteAsync(
                "DELETE FROM rules WHERE id = @Id",
                {| Id = ruleId.ToString() |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let updatePriorities (ruleIds: RuleId list) : Async<unit> =
        async {
            let conn = getConnection()
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
    [<CLIMutable>]
    type SettingRow = {
        value: string
        encrypted: int
    }

    let getSetting (key: string) : Async<string option> =
        async {
            let conn = getConnection()
            let! row =
                conn.QueryFirstOrDefaultAsync<SettingRow>(
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

    /// Lesen eines verschlüsselten Werts. `getSetting` macht aus einem kaputten Wert `None`, wer ihn
    /// aufräumen muss, braucht den Unterschied zu "nichts gespeichert".
    type EncryptedSettingRead =
        | SettingAbsent
        | SettingDecrypted of string
        | SettingUndecryptable

    let readEncryptedSetting (key: string) : Async<EncryptedSettingRead> =
        async {
            let conn = getConnection()
            let! row =
                conn.QueryFirstOrDefaultAsync<SettingRow>(
                    "SELECT value, encrypted FROM settings WHERE key = @Key",
                    {| Key = key |}
                ) |> Async.AwaitTask

            if isNull (box row) then
                return SettingAbsent
            elif row.encrypted <> 1 then
                return SettingUndecryptable
            else
                match Encryption.decrypt row.value with
                | Ok decrypted -> return SettingDecrypted decrypted
                | Error _ -> return SettingUndecryptable
        }

    let setSetting (key: string) (value: string) (encrypted: bool) : Async<unit> =
        async {
            let conn = getConnection()

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
            let conn = getConnection()
            do! (conn.ExecuteAsync(
                "DELETE FROM settings WHERE key = @Key",
                {| Key = key |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

// ============================================
// Sync Sessions Persistence
// ============================================

module SyncSessions =
    /// Eine Zeile in `sync_sessions`. Die Spalten `transaction_count`, `imported_count` und
    /// `skipped_count` stammen aus der Prüfsitzung und werden seit ADR 0093 nicht mehr geschrieben.
    [<CLIMutable>]
    type SyncSessionRow = {
        id: string
        started_at: string
        completed_at: string option
        status: string
        triggered_by: string option
        new_count: int64
        by_rule_count: int64
        by_order_id_count: int64
        without_category_count: int64
        safe_duplicate_count: int64
        held_count: int64
        not_in_ynab_count: int64
        future_dated_count: int64
    }

    let private statusToString = function
        | AwaitingBankAuth -> "AwaitingBankAuth"
        | AwaitingTan -> "AwaitingTan"
        | FetchingTransactions -> "FetchingTransactions"
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
            // Eine Prüfsitzung von vor ADR 0093 lebte nur im Speicher, ihr Stand ist vergangen.
            | "ReviewingTransactions" -> Failed "Prüfsitzung von vor dem Umbau"
            | "ImportingToYnab" -> ImportingToYnab
            | "Completed" -> Completed
            | other -> failwith $"Unbekannter Sitzungsstatus: {other}"

    let private triggerToString = function
        | Manual -> "Manual"
        | Scheduled -> "Scheduled"

    let private triggerFromString (s: string option) : SyncTrigger =
        match s with
        | None | Some "Manual" -> Manual
        | Some "Scheduled" -> Scheduled
        | Some other -> failwith $"Unbekannter Auslöser: {other}"

    let private rowToSession (row: SyncSessionRow) : SyncSession =
        {
            Id = SyncSessionId (Guid.Parse row.id)
            StartedAt = DateTime.Parse row.started_at
            CompletedAt = row.completed_at |> Option.map DateTime.Parse
            Status = statusFromString row.status
            Trigger = triggerFromString row.triggered_by
            Counts = {
                NewEntries = int row.new_count
                ByRule = int row.by_rule_count
                ByOrderId = int row.by_order_id_count
                WithoutCategory = int row.without_category_count
                SafeDuplicatesSkipped = int row.safe_duplicate_count
                Held = int row.held_count
                NotInYnab = int row.not_in_ynab_count
                FutureDated = int row.future_dated_count
            }
        }

    let private parameters (session: SyncSession) =
        let (SyncSessionId sessionId) = session.Id
        {|
            id = sessionId.ToString()
            started_at = session.StartedAt.ToString("O")
            completed_at = session.CompletedAt |> Option.map (fun d -> d.ToString("O"))
            status = statusToString session.Status
            triggered_by = triggerToString session.Trigger
            new_count = session.Counts.NewEntries
            by_rule_count = session.Counts.ByRule
            by_order_id_count = session.Counts.ByOrderId
            without_category_count = session.Counts.WithoutCategory
            safe_duplicate_count = session.Counts.SafeDuplicatesSkipped
            held_count = session.Counts.Held
            not_in_ynab_count = session.Counts.NotInYnab
            future_dated_count = session.Counts.FutureDated
        |}

    let createSession (session: SyncSession) : Async<unit> =
        async {
            let conn = getConnection()
            do! (conn.ExecuteAsync(
                """INSERT INTO sync_sessions
                   (id, started_at, completed_at, status, triggered_by, new_count, by_rule_count, by_order_id_count,
                    without_category_count, safe_duplicate_count, held_count, not_in_ynab_count, future_dated_count)
                   VALUES (@id, @started_at, @completed_at, @status, @triggered_by, @new_count, @by_rule_count, @by_order_id_count,
                    @without_category_count, @safe_duplicate_count, @held_count, @not_in_ynab_count, @future_dated_count)""",
                parameters session
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let updateSession (session: SyncSession) : Async<unit> =
        async {
            let conn = getConnection()
            do! (conn.ExecuteAsync(
                """UPDATE sync_sessions
                   SET completed_at = @completed_at, status = @status, triggered_by = @triggered_by,
                       new_count = @new_count, by_rule_count = @by_rule_count, by_order_id_count = @by_order_id_count,
                       without_category_count = @without_category_count, safe_duplicate_count = @safe_duplicate_count,
                       held_count = @held_count, not_in_ynab_count = @not_in_ynab_count,
                       future_dated_count = @future_dated_count
                   WHERE id = @id""",
                parameters session
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let getRecentSessions (count: int) : Async<SyncSession list> =
        async {
            let conn = getConnection()
            let! rows =
                conn.QueryAsync<SyncSessionRow>(
                    "SELECT * FROM sync_sessions ORDER BY started_at DESC LIMIT @Count",
                    {| Count = count |}
                ) |> Async.AwaitTask
            return rows |> Seq.map rowToSession |> Seq.toList
        }

    let getSessionById (SyncSessionId sessionId: SyncSessionId) : Async<SyncSession option> =
        async {
            let conn = getConnection()
            let! row =
                conn.QueryFirstOrDefaultAsync<SyncSessionRow>(
                    "SELECT * FROM sync_sessions WHERE id = @Id",
                    {| Id = sessionId.ToString() |}
                ) |> Async.AwaitTask
            return if isNull (box row) then None else Some (rowToSession row)
        }

    /// Beginn des jüngsten Abrufs, der zu Ende kam (beendet oder gescheitert), von Hand oder automatisch,
    /// in UTC. Daraus rechnet der Zeitplan die Fälligkeit (banking-import-t4raw): ein Abruf von Hand setzt
    /// ihn zurück, ein Neustart nicht. Eine Sitzung, die bei der TAN stehen blieb, zählt nicht.
    let latestFinishedStartedAt () : Async<DateTime option> =
        async {
            let conn = getConnection()
            let! value =
                conn.QueryFirstOrDefaultAsync<string>(
                    """SELECT started_at FROM sync_sessions
                       WHERE status = 'Completed' OR status LIKE 'Failed:%'
                       ORDER BY started_at DESC LIMIT 1"""
                ) |> Async.AwaitTask
            return
                if isNull value then None
                else
                    Some (
                        DateTime.Parse(
                            value,
                            Globalization.CultureInfo.InvariantCulture,
                            Globalization.DateTimeStyles.AssumeUniversal ||| Globalization.DateTimeStyles.AdjustToUniversal))
        }
