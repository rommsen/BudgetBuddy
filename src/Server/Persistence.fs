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
            let conn = getConnection()
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
            let conn = getConnection()
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

// ============================================
// Sync Transactions Persistence
// ============================================

module SyncTransactions =

    /// JSON-Codec für die Review-Spur (categorization-gdrx8). Bewusst eigenständig
    /// statt eines geteilten Codecs mit `Server.Ledger.Events.Codec`: `categorization`
    /// ist laut BC-README upstream von `ledger`, ein gemeinsamer Codec würde diese
    /// Richtung umkehren. Handgeschriebene Encode/Decode-Funktionen statt Auto-Codec,
    /// damit die gespeicherte Form stabil bleibt (gleiches Muster wie
    /// `Server.Ledger.Events.Codec`).
    module private ReviewStepJson =
        open System.Globalization

        /// ISO-8601-Roundtrip-Text statt `Encode.datetime`/`Decode.datetimeUtc`: das
        /// Paar verschiebt einen zeitzonenlosen Zeitpunkt um den lokalen Offset. "o"
        /// plus `RoundtripKind` ist tickgenau (siehe `Server.Ledger.Events.Codec`).
        let private encodeDate (d: DateTime) =
            Encode.string (d.ToString("o", CultureInfo.InvariantCulture))

        let private decodeDate : Decoder<DateTime> =
            Decode.string
            |> Decode.andThen (fun s ->
                match DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) with
                | true, d -> Decode.succeed d
                | false, _ -> Decode.fail $"Kein ISO-8601-Zeitpunkt: {s}")

        let private encodeMoney (m: Money) =
            Encode.object [
                "amount", Encode.decimal m.Amount
                "currency", Encode.string m.Currency
            ]

        let private decodeMoney : Decoder<Money> =
            Decode.object (fun get -> {
                Amount = get.Required.Field "amount" Decode.decimal
                Currency = get.Required.Field "currency" Decode.string
            })

        let private encodeSplitTarget (t: SplitTarget) =
            match t with
            | ToCategory (YnabCategoryId id, name) ->
                Encode.object [
                    "kind", Encode.string "category"
                    "categoryId", Encode.guid id
                    "categoryName", Encode.string name
                ]
            | ToTransfer (YnabAccountId id, name) ->
                Encode.object [
                    "kind", Encode.string "transfer"
                    "accountId", Encode.guid id
                    "accountName", Encode.string name
                ]
            | Uncategorized ->
                Encode.object [ "kind", Encode.string "uncategorized" ]

        let private decodeSplitTarget : Decoder<SplitTarget> =
            Decode.field "kind" Decode.string
            |> Decode.andThen (function
                | "category" ->
                    Decode.object (fun get ->
                        ToCategory (
                            get.Required.Field "categoryId" Decode.guid |> YnabCategoryId,
                            get.Required.Field "categoryName" Decode.string))
                | "transfer" ->
                    Decode.object (fun get ->
                        ToTransfer (
                            get.Required.Field "accountId" Decode.guid |> YnabAccountId,
                            get.Required.Field "accountName" Decode.string))
                | "uncategorized" -> Decode.succeed Uncategorized
                | other -> Decode.fail $"Unbekanntes Split-Ziel: {other}")

        let private encodeSplit (s: TransactionSplit) =
            Encode.object [
                "target", encodeSplitTarget s.Target
                "amount", encodeMoney s.Amount
                "memo", (match s.Memo with Some m -> Encode.string m | None -> Encode.nil)
            ]

        let private decodeSplit : Decoder<TransactionSplit> =
            Decode.object (fun get -> {
                Target = get.Required.Field "target" decodeSplitTarget
                Amount = get.Required.Field "amount" decodeMoney
                Memo = get.Optional.Field "memo" Decode.string
            })

        let private encodeActor (by: ReviewActor) =
            match by with
            | ByRule (RuleId ruleId, ruleName) ->
                Encode.object [
                    "kind", Encode.string "rule"
                    "ruleId", Encode.guid ruleId
                    "ruleName", Encode.string ruleName
                ]
            | ByUser ->
                Encode.object [ "kind", Encode.string "user" ]
            | ByOrderIdMatch orderId ->
                Encode.object [
                    "kind", Encode.string "orderIdMatch"
                    "orderId", Encode.string orderId
                ]

        let private decodeActor : Decoder<ReviewActor> =
            Decode.field "kind" Decode.string
            |> Decode.andThen (function
                | "rule" ->
                    Decode.object (fun get ->
                        ByRule (
                            get.Required.Field "ruleId" Decode.guid |> RuleId,
                            get.Required.Field "ruleName" Decode.string))
                | "user" -> Decode.succeed ByUser
                | "orderIdMatch" ->
                    Decode.object (fun get ->
                        ByOrderIdMatch (get.Required.Field "orderId" Decode.string))
                | other -> Decode.fail $"Unbekannter Review-Actor: {other}")

        let private encodeStep (step: ReviewStep) =
            Encode.object [
                "at", encodeDate step.At
                "by", encodeActor step.By
                "category",
                    (match step.Category with
                     | Some (YnabCategoryId id) -> Encode.guid id
                     | None -> Encode.nil)
                "categoryName", (match step.CategoryName with Some n -> Encode.string n | None -> Encode.nil)
                "splits",
                    (match step.Splits with
                     | Some splits -> splits |> List.map encodeSplit |> Encode.list
                     | None -> Encode.nil)
                "payeeOverride", (match step.PayeeOverride with Some p -> Encode.string p | None -> Encode.nil)
            ]

        let private decodeStep : Decoder<ReviewStep> =
            Decode.object (fun get -> {
                At = get.Required.Field "at" decodeDate
                By = get.Required.Field "by" decodeActor
                Category = get.Optional.Field "category" Decode.guid |> Option.map YnabCategoryId
                CategoryName = get.Optional.Field "categoryName" Decode.string
                Splits = get.Optional.Field "splits" (Decode.list decodeSplit)
                PayeeOverride = get.Optional.Field "payeeOverride" Decode.string
            })

        /// `[]` serialisiert als `None` (keine Spalte statt eines leeren JSON-Arrays):
        /// hält bestehende Zeilen ohne Review-Spur von neuen ununterscheidbar.
        let encode (steps: ReviewStep list) : string option =
            match steps with
            | [] -> None
            | steps -> steps |> List.map encodeStep |> Encode.list |> Encode.toString 0 |> Some

        /// Fehlende Spalte, `NULL` oder nicht lesbares JSON werden als leere Spur
        /// behandelt statt den ganzen Zeilen-Read scheitern zu lassen: eine vor
        /// categorization-gdrx8 gespeicherte Sitzung soll weiterhin laden (ADR 0021
        /// verlangt das nicht explizit, aber ein Restart darf bestehende Sessions
        /// nicht unlesbar machen).
        let decode (raw: string option) : ReviewStep list =
            match raw with
            | None -> []
            | Some json ->
                match Decode.fromString (Decode.list decodeStep) json with
                | Ok steps -> steps
                | Error _ -> []

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
        review_steps: string option
    }

    let saveTransaction (sessionId: SyncSessionId) (tx: SyncTransaction) : Async<unit> =
        async {
            let conn = getConnection()
            let (SyncSessionId sid) = sessionId
            let (TransactionId txId) = tx.Transaction.Id

            do! (conn.ExecuteAsync(
                """INSERT OR REPLACE INTO sync_transactions
                   (id, session_id, transaction_id, booking_date, amount, currency, payee, memo,
                    reference, status, category_id, category_name, matched_rule_id, payee_override, created_at,
                    review_steps)
                   VALUES (@id, @session_id, @transaction_id, @booking_date, @amount, @currency, @payee,
                           @memo, @reference, @status, @category_id, @category_name, @matched_rule_id,
                           @payee_override, @created_at, @review_steps)""",
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
                    review_steps = ReviewStepJson.encode tx.ReviewSteps
                |}
            ) |> Async.AwaitTask |> Async.Ignore)
        }

    let getTransactionsBySession (SyncSessionId sessionId: SyncSessionId) : Async<SyncTransaction list> =
        async {
            let conn = getConnection()
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
                    DuplicateStatus = NotDuplicate (emptyDetectionDetails row.reference)  // Duplicate status is not persisted, always reset to NotDuplicate
                    YnabImportStatus = NotAttempted  // YnabImportStatus is not persisted
                    Splits = None  // Splits are not persisted yet
                    SuggestedByOrderId = None  // Suggestions are session-only, not persisted
                    ReviewSteps = ReviewStepJson.decode row.review_steps
                    AskLena = false  // Die Rückfrage Lena ist sitzungsgebunden und nicht persistiert (ynab-sync-d9e51)
                }
            )
        }
