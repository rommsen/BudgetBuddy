/// Der Schreib- und Lesepfad des Kassenbuchs: append-only Ereignis-Log plus das
/// synchron gefuehrte Zustandsmodell (ADR 0013).
///
/// `appendAndApply` ist die eine Operation, die beides schreibt: eine frische
/// Connection (ADR 0008), eine SQLite-Transaktion (ADR 0013, Punkt 5). Das Log ist die
/// Quelle der Wahrheit, der Zustand ist ableitbar; `replayAll` baut ihn aus dem Log neu.
/// Payload liegt als JSON-Text, Queries benutzen keine SQLite-JSON-Funktionen
/// (ADR 0013, Punkt 8).
module Ledger.Persistence

open System
open System.Globalization
open Microsoft.Data.Sqlite
open Dapper
open Shared.Domain
open Ledger.Events
open Ledger.Domain

/// Der Kern-Persistenz-Modul mit `getConnection`. Alias, weil der einfache Name
/// `Persistence` hier auf dieses Modul zeigen wuerde.
module Core = global.Persistence

// ============================================
// Text-Konvertierungen
// ============================================
// Betraege liegen als exakter Dezimal-Text, nicht als REAL: ein Kassenbuch, dessen
// Zahlen auditierbar sein sollen, darf keine Rundungsdrift durch Gleitkomma zeigen.

let private decimalToText (d: decimal) = d.ToString(CultureInfo.InvariantCulture)

let private decimalFromText (s: string) =
    Decimal.Parse(s, NumberStyles.Number, CultureInfo.InvariantCulture)

let private dateToText (d: DateTime) = d.ToString("o", CultureInfo.InvariantCulture)

let private dateFromText (s: string) =
    DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)

let private orNull (s: string option) : obj =
    match s with
    | Some v -> box v
    | None -> null

let private toOption (s: string) = if isNull s then None else Some s

let private clearedToText =
    function
    | Uncleared -> "Uncleared"
    | Cleared -> "Cleared"
    | Reconciled -> "Reconciled"

let private clearedFromText =
    function
    | "Uncleared" -> Uncleared
    | "Cleared" -> Cleared
    | "Reconciled" -> Reconciled
    | other -> failwith $"Unbekannter Cleared-Status in der Zustandstabelle: {other}"

let private accountKindToText =
    function
    | BankAccount -> "BankAccount"
    | CashAccount -> "CashAccount"
    | SecuritiesAccount -> "SecuritiesAccount"

let private accountKindFromText =
    function
    | "BankAccount" -> BankAccount
    | "CashAccount" -> CashAccount
    | "SecuritiesAccount" -> SecuritiesAccount
    | other -> failwith $"Unbekannte Kontoart: {other}"

// ============================================
// Schema
// ============================================

/// Migration: `approved`/`flag_color`/`flag_name` (ADR 0032) für Zustandstabellen,
/// die vor diesen Spalten angelegt wurden. Das `CREATE TABLE IF NOT EXISTS` in
/// `initializeLedgerSchema` deckt frische Datenbanken bereits ab; SQLite kennt kein
/// "ADD COLUMN IF NOT EXISTS", deshalb je Spalte ein eigener Versuch nach dem Muster
/// von `Persistence.initializeDatabase` (`review_steps`-Migration). Bestehende Zeilen
/// bekommen dabei den SQL-Standardwert `1` für `approved`, kein Flag: genau die
/// Vorgabe für den Bestand (ADR 0032, Punkt 3). Nimmt die Connection als Parameter,
/// damit Tests sie gegen eine eigene, isolierte Verbindung ausführen können, statt
/// eine Kopie der SQL-Anweisungen zu wiederholen.
let migrateLedgerEntryColumns (conn: SqliteConnection) =
    let migrateColumn (sql: string) =
        try
            use migrateCmd = new SqliteCommand(sql, conn)
            migrateCmd.ExecuteNonQuery() |> ignore
        with :? SqliteException as ex when ex.Message.Contains("duplicate column name") ->
            ()

    migrateColumn "ALTER TABLE ledger_entries ADD COLUMN approved INTEGER NOT NULL DEFAULT 1"
    migrateColumn "ALTER TABLE ledger_entries ADD COLUMN flag_color TEXT"
    migrateColumn "ALTER TABLE ledger_entries ADD COLUMN flag_name TEXT"

/// Legt die Kassenbuch-Tabellen an, auf der übergebenen Connection. Bestehende
/// Tabellen bleiben unangetastet (`CREATE TABLE IF NOT EXISTS`); der Aufruf ist
/// idempotent, auch auf einer bestehenden Datenbank, die einzelne dieser Tabellen
/// (z. B. `ledger_categories`, ADR 0039) noch nicht hat. Connection als Parameter wie
/// bei `migrateLedgerEntryColumns`, damit Tests sie gegen eine eigene, isolierte
/// Verbindung ausführen können.
let internal createLedgerSchemaOn (conn: SqliteConnection) =
    if conn.State <> System.Data.ConnectionState.Open then conn.Open()

    use cmd =
        new SqliteCommand(
            """
        CREATE TABLE IF NOT EXISTS ledger_accounts (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            kind TEXT NOT NULL,
            link_kind TEXT NOT NULL,
            ynab_account_id TEXT,
            closed INTEGER NOT NULL DEFAULT 0
        );

        CREATE UNIQUE INDEX IF NOT EXISTS idx_ledger_accounts_ynab
            ON ledger_accounts(ynab_account_id) WHERE ynab_account_id IS NOT NULL;

        CREATE TABLE IF NOT EXISTS ledger_entry_events (
            entry_id TEXT NOT NULL,
            sequence INTEGER NOT NULL,
            occurred_at TEXT NOT NULL,
            actor_kind TEXT NOT NULL,
            actor_rule_id TEXT,
            event_type TEXT NOT NULL,
            schema_version INTEGER NOT NULL,
            payload TEXT NOT NULL,
            PRIMARY KEY (entry_id, sequence)
        );

        CREATE INDEX IF NOT EXISTS idx_ledger_entry_events_entry
            ON ledger_entry_events(entry_id, sequence);

        CREATE TABLE IF NOT EXISTS ledger_entries (
            id TEXT PRIMARY KEY,
            account_id TEXT NOT NULL,
            date TEXT NOT NULL,
            amount TEXT NOT NULL,
            currency TEXT NOT NULL,
            payee TEXT,
            memo TEXT,
            category_id TEXT,
            category_name TEXT,
            splits TEXT NOT NULL,
            cleared TEXT NOT NULL,
            transfer_entry_id TEXT,
            deleted_in_ynab INTEGER NOT NULL DEFAULT 0,
            approved INTEGER NOT NULL DEFAULT 1,
            flag_color TEXT,
            flag_name TEXT
        );

        CREATE INDEX IF NOT EXISTS idx_ledger_entries_account ON ledger_entries(account_id);
        CREATE INDEX IF NOT EXISTS idx_ledger_entries_category ON ledger_entries(category_id);
        CREATE INDEX IF NOT EXISTS idx_ledger_entries_date ON ledger_entries(date);

        CREATE TABLE IF NOT EXISTS ledger_entry_links (
            entry_id TEXT NOT NULL,
            system TEXT NOT NULL,
            value TEXT NOT NULL,
            PRIMARY KEY (system, value)
        );

        CREATE INDEX IF NOT EXISTS idx_ledger_entry_links_entry ON ledger_entry_links(entry_id);

        CREATE TABLE IF NOT EXISTS ledger_categories (
            ynab_category_id TEXT PRIMARY KEY,
            name             TEXT NOT NULL,
            group_name       TEXT NOT NULL,
            updated_at       TEXT NOT NULL
        );
    """,
            conn
        )

    cmd.ExecuteNonQuery() |> ignore

    migrateLedgerEntryColumns conn

/// Legt die Kassenbuch-Tabellen auf der Standard-Verbindung an; der Aufruf folgt dem
/// Muster von `Persistence.initializeDatabase`.
let initializeLedgerSchema () =
    let conn = Core.getConnection ()
    createLedgerSchemaOn conn

// ============================================
// Zeilen-Typen
// ============================================

[<CLIMutable>]
type private EventRow = {
    entry_id: string
    sequence: int64
    occurred_at: string
    actor_kind: string
    actor_rule_id: string
    event_type: string
    schema_version: int64
    payload: string
}

[<CLIMutable>]
type private EntryRow = {
    id: string
    account_id: string
    date: string
    amount: string
    currency: string
    payee: string
    memo: string
    category_id: string
    category_name: string
    splits: string
    cleared: string
    transfer_entry_id: string
    deleted_in_ynab: int64
    approved: int64
    flag_color: string
    flag_name: string
}

[<CLIMutable>]
type private AccountRow = {
    id: string
    name: string
    kind: string
    link_kind: string
    ynab_account_id: string
    closed: int64
}

[<CLIMutable>]
type private LinkRow = { entry_id: string; system: string; value: string }

[<CLIMutable>]
type private CategoryRow = {
    ynab_category_id: string
    name: string
    group_name: string
    updated_at: string
}

let private rowToRecordedEvent (row: EventRow) : RecordedLedgerEvent =
    let actor =
        match actorFromColumns row.actor_kind (toOption row.actor_rule_id) with
        | Ok a -> a
        | Error e -> failwith e

    let body =
        match decodePayload row.event_type (int row.schema_version) row.payload with
        | Ok b -> b
        | Error e -> failwith $"Ereignis {row.entry_id}/{row.sequence} nicht lesbar: {e}"

    {
        Sequence = int row.sequence
        EventType = row.event_type
        SchemaVersion = int row.schema_version
        Event = {
            EntryId = EntryId (Guid.Parse row.entry_id)
            OccurredAt = dateFromText row.occurred_at
            Actor = actor
            Body = body
        }
    }

let private rowToEntry (row: EntryRow) : Entry =
    let category =
        match toOption row.category_id, toOption row.category_name with
        | Some id, Some name ->
            Some { CategoryId = YnabCategoryId (Guid.Parse id); CategoryName = name }
        | _ -> None

    let splits =
        match Thoth.Json.Net.Decode.fromString Codec.decodeSplits row.splits with
        | Ok s -> s
        | Error e -> failwith $"Splits der Buchung {row.id} nicht lesbar: {e}"

    {
        Id = EntryId (Guid.Parse row.id)
        AccountId = LedgerAccountId (Guid.Parse row.account_id)
        Date = dateFromText row.date
        Amount = { Amount = decimalFromText row.amount; Currency = row.currency }
        Payee = toOption row.payee
        Memo = toOption row.memo
        Category = category
        Splits = splits
        Cleared = clearedFromText row.cleared
        TransferEntryId = toOption row.transfer_entry_id |> Option.map (Guid.Parse >> EntryId)
        DeletedInYnab = row.deleted_in_ynab = 1L
        Approved = row.approved = 1L
        FlagColor = toOption row.flag_color
        FlagName = toOption row.flag_name
    }

let private rowToAccount (row: AccountRow) : LedgerAccount =
    {
        Id = LedgerAccountId (Guid.Parse row.id)
        Name = row.name
        Kind = accountKindFromText row.kind
        Link =
            match row.link_kind, toOption row.ynab_account_id with
            | "YnabMirrored", Some id -> YnabMirrored (YnabAccountId (Guid.Parse id))
            | "YnabMirrored", None -> failwith $"Konto {row.id} ist gespiegelt, hat aber keine YnabAccountId"
            | "BbOwned", _ -> BbOwned
            | other, _ -> failwith $"Unbekannte Kontoverknuepfung: {other}"
        Closed = row.closed = 1L
    }

// ============================================
// Schreibpfad: append-only Log + Zustand in einer Transaktion
// ============================================

let private entryParams (entry: Entry) =
    let (EntryId id) = entry.Id
    let (LedgerAccountId accountId) = entry.AccountId
    let categoryId, categoryName =
        match entry.Category with
        | Some c ->
            let (YnabCategoryId cid) = c.CategoryId
            box (cid.ToString()), box c.CategoryName
        | None -> null, null

    {|
        id = id.ToString()
        account_id = accountId.ToString()
        date = dateToText entry.Date
        amount = decimalToText entry.Amount.Amount
        currency = entry.Amount.Currency
        payee = orNull entry.Payee
        memo = orNull entry.Memo
        category_id = categoryId
        category_name = categoryName
        splits = Thoth.Json.Net.Encode.toString 0 (Codec.encodeSplits entry.Splits)
        cleared = clearedToText entry.Cleared
        transfer_entry_id =
            match entry.TransferEntryId with
            | Some (EntryId t) -> box (t.ToString())
            | None -> null
        deleted_in_ynab = (if entry.DeletedInYnab then 1 else 0)
        approved = (if entry.Approved then 1 else 0)
        flag_color = orNull entry.FlagColor
        flag_name = orNull entry.FlagName
    |}

let private upsertEntrySql =
    """INSERT INTO ledger_entries
         (id, account_id, date, amount, currency, payee, memo, category_id, category_name,
          splits, cleared, transfer_entry_id, deleted_in_ynab, approved, flag_color, flag_name)
       VALUES
         (@id, @account_id, @date, @amount, @currency, @payee, @memo, @category_id,
          @category_name, @splits, @cleared, @transfer_entry_id, @deleted_in_ynab, @approved,
          @flag_color, @flag_name)
       ON CONFLICT(id) DO UPDATE SET
          account_id = excluded.account_id,
          date = excluded.date,
          amount = excluded.amount,
          currency = excluded.currency,
          payee = excluded.payee,
          memo = excluded.memo,
          category_id = excluded.category_id,
          category_name = excluded.category_name,
          splits = excluded.splits,
          cleared = excluded.cleared,
          transfer_entry_id = excluded.transfer_entry_id,
          deleted_in_ynab = excluded.deleted_in_ynab,
          approved = excluded.approved,
          flag_color = excluded.flag_color,
          flag_name = excluded.flag_name"""

/// Haengt Ereignisse an das Log und schreibt den daraus abgeleiteten Zustand fort:
/// beides auf **einer** Connection in **einer** Transaktion (ADR 0013, Punkt 5).
/// Schlaegt irgendein Schritt fehl, steht danach weder ein Ereignis im Log noch eine
/// Aenderung im Zustand. Die laufende Nummer je Buchung vergibt diese Funktion.
let appendAndApply (events: LedgerEvent list) : Async<unit> =
    async {
        if List.isEmpty events then
            return ()
        else
            use conn = Core.getConnection ()
            conn.Open()
            use tx = conn.BeginTransaction()

            try
                // Zustand je betroffener Buchung nur einmal laden; mehrere Ereignisse in
                // einem Aufruf falten aufeinander auf.
                let mutable states : Map<Guid, Entry option> = Map.empty
                let mutable sequences : Map<Guid, int> = Map.empty

                for event in events do
                    let (EntryId entryGuid) = event.EntryId
                    let entryIdText = entryGuid.ToString()

                    let currentState =
                        match Map.tryFind entryGuid states with
                        | Some s -> s
                        | None ->
                            let row =
                                conn.QueryFirstOrDefault<EntryRow>(
                                    "SELECT * FROM ledger_entries WHERE id = @id",
                                    {| id = entryIdText |},
                                    tx
                                )
                            if isNull (box row) then None else Some (rowToEntry row)

                    let nextSequence =
                        match Map.tryFind entryGuid sequences with
                        | Some s -> s + 1
                        | None ->
                            let maxSeq =
                                conn.ExecuteScalar<int64>(
                                    "SELECT COALESCE(MAX(sequence), 0) FROM ledger_entry_events WHERE entry_id = @id",
                                    {| id = entryIdText |},
                                    tx
                                )
                            int maxSeq + 1

                    let actorKind, actorRuleId = actorToColumns event.Actor

                    conn.Execute(
                        """INSERT INTO ledger_entry_events
                             (entry_id, sequence, occurred_at, actor_kind, actor_rule_id,
                              event_type, schema_version, payload)
                           VALUES
                             (@entry_id, @sequence, @occurred_at, @actor_kind, @actor_rule_id,
                              @event_type, @schema_version, @payload)""",
                        {|
                            entry_id = entryIdText
                            sequence = nextSequence
                            occurred_at = dateToText event.OccurredAt
                            actor_kind = actorKind
                            actor_rule_id = orNull actorRuleId
                            event_type = eventTypeName event.Body
                            schema_version = schemaVersion event.Body
                            payload = encodePayload event.Body
                        |},
                        tx
                    )
                    |> ignore

                    for link in linksFrom event do
                        conn.Execute(
                            "INSERT INTO ledger_entry_links (entry_id, system, value) VALUES (@entry_id, @system, @value)",
                            {|
                                entry_id = entryIdText
                                system = linkSystemToString link.System
                                value = link.Value
                            |},
                            tx
                        )
                        |> ignore

                    // Der Zustand ist abgeleitet: dieselbe reine Funktion, die auch das
                    // Replay benutzt. Wirft sie, rollt die Transaktion das Ereignis zurueck.
                    let newState = apply currentState event
                    conn.Execute(upsertEntrySql, entryParams newState, tx) |> ignore

                    states <- Map.add entryGuid (Some newState) states
                    sequences <- Map.add entryGuid nextSequence sequences

                tx.Commit()
            with ex ->
                // Ein Fehler an irgendeiner Stelle nimmt alles zurueck: kein Ereignis im
                // Log ohne den passenden Zustand und umgekehrt (ADR 0013, Punkt 5).
                tx.Rollback()
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw()
    }

/// Verknuepft eine Fremd-Id mit einer Buchung (ADR 0015). Ein Wert ist je System
/// eindeutig; ein zweiter Versuch auf dieselbe Fremd-Id schlaegt fehl.
let linkEntry (entryId: EntryId) (system: LinkSystem) (value: string) : Async<unit> =
    async {
        let (EntryId id) = entryId
        use conn = Core.getConnection ()
        do!
            conn.ExecuteAsync(
                "INSERT INTO ledger_entry_links (entry_id, system, value) VALUES (@entry_id, @system, @value)",
                {| entry_id = id.ToString(); system = linkSystemToString system; value = value |}
            )
            |> Async.AwaitTask
            |> Async.Ignore
    }

// ============================================
// Lesepfad
// ============================================

/// Die vollstaendige Historie einer Buchung, in gespeicherter Reihenfolge.
let loadEventsForEntry (entryId: EntryId) : Async<RecordedLedgerEvent list> =
    async {
        let (EntryId id) = entryId
        use conn = Core.getConnection ()
        let! rows =
            conn.QueryAsync<EventRow>(
                "SELECT * FROM ledger_entry_events WHERE entry_id = @id ORDER BY sequence",
                {| id = id.ToString() |}
            )
            |> Async.AwaitTask
        return rows |> Seq.map rowToRecordedEvent |> Seq.toList
    }

/// Das gesamte Log, nach Buchung und laufender Nummer sortiert.
let loadAllEvents () : Async<RecordedLedgerEvent list> =
    async {
        use conn = Core.getConnection ()
        let! rows =
            conn.QueryAsync<EventRow>("SELECT * FROM ledger_entry_events ORDER BY entry_id, sequence")
            |> Async.AwaitTask
        return rows |> Seq.map rowToRecordedEvent |> Seq.toList
    }

/// Der gespeicherte Zustand einer Buchung.
let loadEntry (entryId: EntryId) : Async<Entry option> =
    async {
        let (EntryId id) = entryId
        use conn = Core.getConnection ()
        let! row =
            conn.QueryFirstOrDefaultAsync<EntryRow>(
                "SELECT * FROM ledger_entries WHERE id = @id",
                {| id = id.ToString() |}
            )
            |> Async.AwaitTask
        return if isNull (box row) then None else Some (rowToEntry row)
    }

/// Alle gespeicherten Buchungen, nach Id sortiert.
let loadAllEntries () : Async<Entry list> =
    async {
        use conn = Core.getConnection ()
        let! rows =
            conn.QueryAsync<EntryRow>("SELECT * FROM ledger_entries ORDER BY id") |> Async.AwaitTask
        return rows |> Seq.map rowToEntry |> Seq.toList
    }

/// Alle Buchungen eines Kontos, neueste zuerst. Liest ausschliesslich die
/// Zustandstabelle: der Lesepfad der Kassenbuch-Seite faltet kein Log (ADR 0013).
///
/// Bewusst **kein** `SUM(amount)` und kein `CAST(amount AS REAL)`: der Betrag bleibt
/// exakter Dezimal-Text, der Kontostand wird in F# ueber die geladenen Buchungen
/// summiert (ADR 0017).
let loadEntriesForAccount (accountId: LedgerAccountId) : Async<Entry list> =
    async {
        let (LedgerAccountId id) = accountId
        use conn = Core.getConnection ()
        let! rows =
            conn.QueryAsync<EntryRow>(
                "SELECT * FROM ledger_entries WHERE account_id = @id ORDER BY date DESC, id DESC",
                {| id = id.ToString() |}
            )
            |> Async.AwaitTask
        return rows |> Seq.map rowToEntry |> Seq.toList
    }

// ============================================
// Seitenweise Liste und Kontostaende (ledger-b1xwe)
// ============================================
// Zwei Zweige statt eines "IS NULL OR"-Tricks: ein Konto-Filter oder keiner. Explizit
// statt clever, damit kein Nullable-Parameter-Sonderfall stillschweigend die falschen
// Zeilen liefert. Kein SQL-`SUM`, kein `CAST(amount AS REAL)` irgendwo unten
// (ADR 0017) — die Summenbildung bleibt F#.

/// Die WHERE-Klausel für "zu prüfen" (ADR 0032): nicht genehmigt oder noch offen.
/// `Reconciled` zählt wie `Cleared` als geprüft, deshalb reicht der Vergleich auf
/// `Uncleared`.
let private needsReviewSqlClause = "(approved = 0 OR cleared = 'Uncleared')"

/// Die fünf Such-Filter (ledger-vgfw3, ADR 0036), plus die beiden bestehenden
/// Schalter — gebündelt, weil inzwischen fünf der acht Lese-Funktionen unten
/// dieselben Kriterien anwenden müssen (AC "alle vier Zählwege ziehen mit").
/// `AmountMin`/`AmountMax` werden bewusst NICHT Teil der SQL-Klausel unten: der
/// Betrag liegt als exakter Dezimal-Text (ADR 0017), ein SQL-Vergleich (auch über
/// `CAST(amount AS REAL)`) würde dieselbe Rundungsdrift riskieren, die ADR 0017
/// gerade ausschließt. Sie wirken stattdessen als Nachfilter in F#
/// (`amountMatches`) auf die SQL-vorgefilterte Treffermenge — ADR 0017 sah diesen
/// Fall bereits vor ("wenn [Vergleich nach Betrag] auftritt, wird in F# sortiert").
type EntryFilter = {
    IncludeDeleted: bool
    OnlyNeedsReview: bool
    CategoryIds: Guid list
    DateFrom: DateTime option
    DateTo: DateTime option
    Payee: string option
    Memo: string option
    AmountMin: decimal option
    AmountMax: decimal option
}

/// Kein Filter gesetzt — die Rückwärtskompatibilitäts-Basislinie (AC1): ein Aufruf
/// mit `noFilter` (plus `IncludeDeleted`/`OnlyNeedsReview` wie zuvor) liefert
/// dasselbe Ergebnis wie vor ledger-vgfw3.
let noFilter = {
    IncludeDeleted = false
    OnlyNeedsReview = false
    CategoryIds = []
    DateFrom = None
    DateTo = None
    Payee = None
    Memo = None
    AmountMin = None
    AmountMax = None
}

let private hasAmountFilter (filter: EntryFilter) = filter.AmountMin.IsSome || filter.AmountMax.IsSome

/// Prüft `AmountMin`/`AmountMax` gegen einen geladenen Betrag, in `decimal`, nie
/// über eine SQL-Bedingung (siehe `EntryFilter`). Beide Enden sind unabhängig
/// voneinander offen: nur `AmountMin` gesetzt heißt "mindestens", nur `AmountMax`
/// heißt "höchstens".
let private amountMatches (filter: EntryFilter) (amount: decimal) : bool =
    (filter.AmountMin |> Option.forall (fun min -> amount >= min))
    && (filter.AmountMax |> Option.forall (fun max -> amount <= max))

/// Baut die WHERE-Klausel (ohne die führende `WHERE`) und die dazugehörigen
/// Dapper-Parameter aus Konto und `EntryFilter` — ohne `AmountMin`/`AmountMax`
/// (siehe `EntryFilter`). Gemeinsame Grundlage für alle Lese-Funktionen unten,
/// damit sie nie auseinanderlaufen können (AC "denselben Filter anwenden").
let private buildFilterClause (accountId: LedgerAccountId option) (filter: EntryFilter) : string * DynamicParameters =
    let clause = System.Text.StringBuilder("1 = 1")
    let ps = DynamicParameters()

    match accountId with
    | Some (LedgerAccountId id) ->
        clause.Append(" AND account_id = @accountId") |> ignore
        ps.Add("accountId", id.ToString())
    | None -> ()

    if not filter.IncludeDeleted then
        clause.Append(" AND deleted_in_ynab = 0") |> ignore

    if filter.OnlyNeedsReview then
        clause.Append($" AND {needsReviewSqlClause}") |> ignore

    match filter.CategoryIds with
    | [] -> ()
    | ids ->
        // Nur die einfache Kategorie (`category_id`-Spalte), nicht die Split-Zeilen:
        // die liegen als JSON-Text, und Queries dürfen keine SQLite-JSON-Funktionen
        // benutzen (ADR 0013, Punkt 8). Eine Buchung, deren einzige Kategorie in
        // einem Split steckt, findet der Kategorie-Filter also nicht — bewusst in
        // Kauf genommen für den weit häufigeren Fall (ADR 0036).
        clause.Append(" AND category_id IN @categoryIds") |> ignore
        ps.Add("categoryIds", ids |> List.map (fun g -> g.ToString()))

    // Datum liegt als ISO-8601-Roundtrip-Text (ADR 0017, Punkt 4: "`\"o\"` plus
    // `RoundtripKind`"). Roundtrip heißt: die Länge hängt vom `DateTimeKind` ab —
    // `Utc` hängt ein `Z` an, `Local` eine Zonenverschiebung, `Unspecified` nichts.
    // Die `date`-Spalte mischt beides: der Spiegel schreibt `Utc`
    // (`YnabObservations.fs`, `SpecifyKind(..., Utc)`), Comdirect-/Quick-Add-Zeilen
    // kommen aus `DateTime.Parse` ohne Kind. Ein direkter Textvergleich des vollen
    // Roundtrip-Strings ist deshalb falsch: je nach Kind-Mix verliert ein Rand still
    // Buchungen genau am Grenztag (siehe Verifier-Befund, Iteration 1). Der Vergleich
    // greift stattdessen nur den Datumsanteil ab (die ersten 10 Zeichen, `yyyy-MM-dd`
    // — bei jedem Kind gleich, weil der Kind-Suffix erst danach beginnt) und
    // vergleicht ihn gegen denselben `yyyy-MM-dd`-Text auf der Bound-Seite. `substr`
    // ist gewöhnliches SQL, keine SQLite-Spezialität (ADR 0013, Punkt 8 verbietet nur
    // `json_extract` u. Ä.). `.Date` kappt eine evtl. Uhrzeit auf der Bound-Seite,
    // damit die Randtage (AC "einschließlich") nicht an einer Uhrzeit scheitern.
    match filter.DateFrom with
    | Some d ->
        clause.Append(" AND substr(date, 1, 10) >= @dateFrom") |> ignore
        ps.Add("dateFrom", d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
    | None -> ()

    match filter.DateTo with
    | Some d ->
        clause.Append(" AND substr(date, 1, 10) <= @dateTo") |> ignore
        ps.Add("dateTo", d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
    | None -> ()

    // Payee kommt aus der Vorschlagsliste (Klick, "Payee ist immer nur einer"), also
    // exakter Vergleich; Memo ist ein Suchfeld, also Teiltext. Beide ohne
    // Berücksichtigung der Groß-/Kleinschreibung, auch für Umlaute: `bb_lower`
    // (ADR 0037) statt der früheren SQLite-Kollation, die nur ASCII A-Z faltet und
    // `LIKE` ohnehin nicht erreicht. Beide Seiten des Vergleichs laufen durch dieselbe
    // Funktion, sonst faltet nur die Spalte und jeder Suchtext mit einem
    // Großbuchstaben findet still nichts (derselbe Fehler in neuer Verkleidung).
    match filter.Payee with
    | Some payee ->
        clause.Append(" AND bb_lower(payee) = @payee") |> ignore
        ps.Add("payee", Core.searchFold payee)
    | None -> ()

    match filter.Memo with
    | Some memo ->
        clause.Append(" AND bb_lower(memo) LIKE @memo") |> ignore
        ps.Add("memo", $"%%{Core.searchFold memo}%%")
    | None -> ()

    clause.ToString(), ps

/// Lädt ALLE Buchungen, die Konto und `EntryFilter` (ohne Betrag) treffen, neueste
/// zuerst, ohne `LIMIT`. Nur der Weg für `AmountMin`/`AmountMax`: weil der Betrag
/// nicht in SQL geprüft werden kann (`EntryFilter`), braucht der Nachfilter in F#
/// die volle Treffermenge der übrigen Kriterien. Ohne Betrags-Filter benutzen
/// `loadEntriesPage`/`countEntries`/`loadAmountsForQuery`/`countDeletedEntries`/
/// `loadPayeeSuggestions` weiterhin ihre eigene, gezielte SQL-Abfrage (LIMIT/OFFSET,
/// COUNT, Projektion) statt dieser Funktion — sonst würde jede Abfrage ohne Not
/// erst alle rund 14.000 Buchungen laden (Task-Vorgabe: "Die Filterung gehört in
/// die Abfrage, nicht hinter sie").
let private loadAllMatching (accountId: LedgerAccountId option) (filter: EntryFilter) : Async<Entry list> =
    async {
        use conn = Core.getConnection ()
        let whereSql, ps = buildFilterClause accountId filter
        let! rows =
            conn.QueryAsync<EntryRow>($"SELECT * FROM ledger_entries WHERE {whereSql} ORDER BY date DESC, id DESC", ps)
            |> Async.AwaitTask
        return rows |> Seq.map rowToEntry |> Seq.toList
    }

let private safePage (pageIndex: int) (pageSize: int) (xs: Entry list) : Entry list =
    let offset = pageIndex * pageSize
    if offset >= List.length xs then [] else xs |> List.skip offset |> List.truncate pageSize

/// Eine Seite der Buchungen eines Kontos oder — `accountId = None` — aller Konten,
/// neueste zuerst. Liest ausschliesslich die Zustandstabelle (ADR 0013).
/// `filter.IncludeDeleted = false` (Standard, ledger-p3n8k) lässt in YNAB gelöschte
/// Buchungen aus der Seite weg, sonst zählen sie mit. `filter.OnlyNeedsReview = true`
/// (ADR 0032) lässt nur ungeprüfte Buchungen stehen. Die fünf Such-Filter
/// (ledger-vgfw3) wirken zusätzlich, alle als Und-Verknüpfung.
let loadEntriesPage
    (accountId: LedgerAccountId option)
    (pageIndex: int)
    (pageSize: int)
    (filter: EntryFilter)
    : Async<Entry list> =
    async {
        if hasAmountFilter filter then
            let! all = loadAllMatching accountId filter
            return all |> List.filter (fun e -> amountMatches filter e.Amount.Amount) |> safePage pageIndex pageSize
        else
            use conn = Core.getConnection ()
            let offset = pageIndex * pageSize
            let whereSql, ps = buildFilterClause accountId filter
            ps.Add("limit", pageSize)
            ps.Add("offset", offset)
            let! rows =
                conn.QueryAsync<EntryRow>(
                    $"SELECT * FROM ledger_entries WHERE {whereSql} ORDER BY date DESC, id DESC LIMIT @limit OFFSET @offset",
                    ps
                )
                |> Async.AwaitTask
            return rows |> Seq.map rowToEntry |> Seq.toList
    }

/// Die Gesamtzahl der Buchungen eines Kontos oder aller Konten — die Trefferzahl
/// einer Seiten-Abfrage, nicht die Zahl der geladenen Seite. Derselbe `filter` wie
/// `loadEntriesPage`, damit Trefferzahl und Seite immer zum selben Treffer gehören.
let countEntries (accountId: LedgerAccountId option) (filter: EntryFilter) : Async<int> =
    async {
        if hasAmountFilter filter then
            let! all = loadAllMatching accountId filter
            return all |> List.filter (fun e -> amountMatches filter e.Amount.Amount) |> List.length
        else
            use conn = Core.getConnection ()
            let whereSql, ps = buildFilterClause accountId filter
            let! count = conn.ExecuteScalarAsync<int64>($"SELECT COUNT(*) FROM ledger_entries WHERE {whereSql}", ps) |> Async.AwaitTask
            return int count
    }

/// Die Zahl der in YNAB gelöschten Buchungen eines Kontos oder aller Konten
/// (unabhängig von `filter.IncludeDeleted`/`filter.OnlyNeedsReview`, ledger-p3n8k
/// AC2): die Seite soll "N gelöschte Buchungen ausgeblendet" auch zeigen können,
/// während sie ausgeblendet sind. Die fünf Such-Filter (ledger-vgfw3) gelten
/// trotzdem mit, sonst passt die Zahl nicht mehr zum aktiven Filter (AC).
let countDeletedEntries (accountId: LedgerAccountId option) (filter: EntryFilter) : Async<int> =
    async {
        let deletedFilter = { filter with IncludeDeleted = true; OnlyNeedsReview = false }
        if hasAmountFilter filter then
            let! all = loadAllMatching accountId deletedFilter
            return
                all
                |> List.filter (fun e -> e.DeletedInYnab && amountMatches filter e.Amount.Amount)
                |> List.length
        else
            use conn = Core.getConnection ()
            let whereSql, ps = buildFilterClause accountId deletedFilter
            let! count =
                conn.ExecuteScalarAsync<int64>($"SELECT COUNT(*) FROM ledger_entries WHERE {whereSql} AND deleted_in_ynab = 1", ps)
                |> Async.AwaitTask
            return int count
    }

/// Die Zahl der zu prüfenden Buchungen eines Kontos oder aller Konten (ADR 0032),
/// unabhängig von `onlyNeedsReview` (wie `countDeletedEntries`/`IncludeDeleted`):
/// die Seite soll den Chip "Zu prüfen (n)" auch zeigen können, während der Filter
/// aus ist. Gelöschte Buchungen zählen nie mit. Bewusst OHNE die fünf Such-Filter
/// (ledger-vgfw3): der Chip ist ein fester Zähler über das Konto, keine
/// Trefferzahl der laufenden Suche — anders als `countDeletedEntries`, das die
/// Summenzeile stützt.
let countNeedsReviewEntries (accountId: LedgerAccountId option) : Async<int> =
    async {
        use conn = Core.getConnection ()
        let! count =
            match accountId with
            | Some (LedgerAccountId id) ->
                conn.ExecuteScalarAsync<int64>(
                    $"SELECT COUNT(*) FROM ledger_entries WHERE account_id = @accountId AND deleted_in_ynab = 0 AND {needsReviewSqlClause}",
                    {| accountId = id.ToString() |}
                )
                |> Async.AwaitTask
            | None ->
                conn.ExecuteScalarAsync<int64>(
                    $"SELECT COUNT(*) FROM ledger_entries WHERE deleted_in_ynab = 0 AND {needsReviewSqlClause}")
                |> Async.AwaitTask
        return int count
    }

[<CLIMutable>]
type private AmountRow = { amount: string; currency: string }

/// Die Betraege aller Buchungen eines Treffers (ein Konto oder alle), fuer die in F#
/// gebildete Summe ueber den GESAMTEN Treffer (ADR 0017, ledger-b1xwe AC1). Bewusst
/// kein `SUM(amount)`: der Betrag liegt als exakter Dezimal-Text, nicht als `REAL`.
/// Derselbe `filter` wie `loadEntriesPage`/`countEntries`, damit die Summe zum
/// tatsächlichen Treffer passt.
let loadAmountsForQuery (accountId: LedgerAccountId option) (filter: EntryFilter) : Async<Money list> =
    async {
        if hasAmountFilter filter then
            let! all = loadAllMatching accountId filter
            return all |> List.filter (fun e -> amountMatches filter e.Amount.Amount) |> List.map (fun e -> e.Amount)
        else
            use conn = Core.getConnection ()
            let whereSql, ps = buildFilterClause accountId filter
            let! rows =
                conn.QueryAsync<AmountRow>($"SELECT amount, currency FROM ledger_entries WHERE {whereSql}", ps)
                |> Async.AwaitTask
            return rows |> Seq.map (fun r -> { Amount = decimalFromText r.amount; Currency = r.currency }) |> Seq.toList
    }

[<CLIMutable>]
type private PayeeCountRow = { payee: string; cnt: int64 }

/// Die Payees eines Treffers samt ihrer Anzahl **innerhalb dieses Treffers**
/// (ledger-vgfw3 AC, ADR 0036) — nicht über alle Buchungen, nicht über die geladene
/// Seite. `filter.Payee` ist bei diesem Aufruf erwartungsgemäß `None` (der Aufrufer,
/// `getLedgerPayeeSuggestions`, übergibt nie einen bereits gewählten Payee-Filter);
/// wäre er trotzdem gesetzt, würde er wie jeder andere Filter mitwirken, statt
/// stillschweigend ignoriert zu werden.
let loadPayeeSuggestions (accountId: LedgerAccountId option) (filter: EntryFilter) : Async<(string * int) list> =
    async {
        if hasAmountFilter filter then
            let! all = loadAllMatching accountId filter
            return
                all
                |> List.filter (fun e -> amountMatches filter e.Amount.Amount)
                |> List.choose (fun e -> e.Payee)
                |> List.countBy id
                |> List.sortByDescending snd
        else
            use conn = Core.getConnection ()
            let whereSql, ps = buildFilterClause accountId filter
            let! rows =
                conn.QueryAsync<PayeeCountRow>(
                    $"SELECT payee, COUNT(*) as cnt FROM ledger_entries WHERE {whereSql} AND payee IS NOT NULL GROUP BY payee ORDER BY cnt DESC",
                    ps
                )
                |> Async.AwaitTask
            return rows |> Seq.map (fun r -> r.payee, int r.cnt) |> Seq.toList
    }

[<CLIMutable>]
type private BalanceRow = { account_id: string; amount: string; currency: string; deleted_in_ynab: int64 }

/// Die Rohdaten fuer die Kontostaende aller Konten in einem Durchlauf (ledger-b1xwe
/// AC2): Konto, Betrag und Geloescht-Kennzeichen jeder Buchung, ungefiltert — die
/// Auswahl "nicht geloescht" trifft die reine Funktion `ledgerBalancesByAccount`
/// (Shared.Domain), nicht diese Query. Kein SQL-`SUM`, kein `GROUP BY` mit Aggregat.
let loadAmountsForBalances () : Async<(Guid * Money * bool) list> =
    async {
        use conn = Core.getConnection ()
        let! rows =
            conn.QueryAsync<BalanceRow>("SELECT account_id, amount, currency, deleted_in_ynab FROM ledger_entries")
            |> Async.AwaitTask
        return
            rows
            |> Seq.map (fun r ->
                Guid.Parse r.account_id, { Amount = decimalFromText r.amount; Currency = r.currency }, r.deleted_in_ynab = 1L)
            |> Seq.toList
    }

[<CLIMutable>]
type private OriginRow = { entry_id: string; actor_kind: string; actor_rule_id: string }

/// Die Herkunft jeder Buchung: der Actor ihres **ersten** Ereignisses. Eine Zeile je
/// Buchung, kein Replay — die Herkunft steht fest, sobald die Buchung entsteht.
let loadEntryOrigins () : Async<Map<Guid, Actor>> =
    async {
        use conn = Core.getConnection ()
        let! rows =
            conn.QueryAsync<OriginRow>(
                "SELECT entry_id, actor_kind, actor_rule_id FROM ledger_entry_events WHERE sequence = 1"
            )
            |> Async.AwaitTask
        return
            rows
            |> Seq.choose (fun row ->
                match actorFromColumns row.actor_kind (toOption row.actor_rule_id) with
                | Ok actor -> Some (Guid.Parse row.entry_id, actor)
                | Error _ -> None)
            |> Map.ofSeq
    }

/// Alle Fremd-Id-Verknuepfungen einer Buchung.
let linksForEntry (entryId: EntryId) : Async<EntryLink list> =
    async {
        let (EntryId id) = entryId
        use conn = Core.getConnection ()
        let! rows =
            conn.QueryAsync<LinkRow>(
                "SELECT * FROM ledger_entry_links WHERE entry_id = @id ORDER BY system, value",
                {| id = id.ToString() |}
            )
            |> Async.AwaitTask
        return
            rows
            |> Seq.map (fun r ->
                match linkSystemFromString r.system with
                | Ok s -> { EntryId = EntryId (Guid.Parse r.entry_id); System = s; Value = r.value }
                | Error e -> failwith e)
            |> Seq.toList
    }

/// Sucht die Buchung zu einer Fremd-Id. Der Spiegel matcht damit YNAB-Id vor ImportId
/// (ADR 0015).
let findEntryByLink (system: LinkSystem) (value: string) : Async<EntryId option> =
    async {
        use conn = Core.getConnection ()
        let! found =
            conn.QueryFirstOrDefaultAsync<string>(
                "SELECT entry_id FROM ledger_entry_links WHERE system = @system AND value = @value",
                {| system = linkSystemToString system; value = value |}
            )
            |> Async.AwaitTask
        return if isNull found then None else Some (EntryId (Guid.Parse found))
    }

/// Baut den Zustand aller Buchungen ausschliesslich aus dem Log neu, ohne zu schreiben.
/// Pflichtgegenprobe zum synchron gefuehrten Zustandsmodell (ADR 0013, Punkt 6).
let replayAll () : Async<Entry list> =
    async {
        let! recorded = loadAllEvents ()
        return
            recorded
            |> List.groupBy (fun r -> r.Event.EntryId)
            |> List.map (fun (_, forEntry) ->
                forEntry
                |> List.sortBy (fun r -> r.Sequence)
                |> List.map (fun r -> r.Event)
                |> List.fold (fun state ev -> Some (apply state ev)) None
                |> Option.get)
            |> List.sortBy (fun e ->
                let (EntryId id) = e.Id
                id.ToString())
    }

// ============================================
// Konten (reiner Zustand, keine Historie in v1)
// ============================================

let saveAccount (account: LedgerAccount) : Async<unit> =
    async {
        let (LedgerAccountId id) = account.Id
        let linkKind, ynabAccountId =
            match account.Link with
            | YnabMirrored (YnabAccountId ynabId) -> "YnabMirrored", box (ynabId.ToString())
            | BbOwned -> "BbOwned", null

        use conn = Core.getConnection ()
        do!
            conn.ExecuteAsync(
                """INSERT INTO ledger_accounts (id, name, kind, link_kind, ynab_account_id, closed)
                   VALUES (@id, @name, @kind, @link_kind, @ynab_account_id, @closed)
                   ON CONFLICT(id) DO UPDATE SET
                     name = excluded.name,
                     kind = excluded.kind,
                     link_kind = excluded.link_kind,
                     ynab_account_id = excluded.ynab_account_id,
                     closed = excluded.closed""",
                {|
                    id = id.ToString()
                    name = account.Name
                    kind = accountKindToText account.Kind
                    link_kind = linkKind
                    ynab_account_id = ynabAccountId
                    closed = (if account.Closed then 1 else 0)
                |}
            )
            |> Async.AwaitTask
            |> Async.Ignore
    }

let loadAccounts () : Async<LedgerAccount list> =
    async {
        use conn = Core.getConnection ()
        let! rows =
            conn.QueryAsync<AccountRow>("SELECT * FROM ledger_accounts ORDER BY name") |> Async.AwaitTask
        return rows |> Seq.map rowToAccount |> Seq.toList
    }

let findAccountByYnabAccountId (YnabAccountId ynabId: YnabAccountId) : Async<LedgerAccount option> =
    async {
        use conn = Core.getConnection ()
        let! row =
            conn.QueryFirstOrDefaultAsync<AccountRow>(
                "SELECT * FROM ledger_accounts WHERE ynab_account_id = @id",
                {| id = ynabId.ToString() |}
            )
            |> Async.AwaitTask
        return if isNull (box row) then None else Some (rowToAccount row)
    }

// ============================================
// Kategorien (Stammdaten, gespiegelt wie die Konten; ADR 0039)
// ============================================

let private upsertCategorySql =
    """INSERT INTO ledger_categories (ynab_category_id, name, group_name, updated_at)
       VALUES (@ynab_category_id, @name, @group_name, @updated_at)
       ON CONFLICT(ynab_category_id) DO UPDATE SET
          name = excluded.name,
          group_name = excluded.group_name,
          updated_at = excluded.updated_at"""

/// Schreibt die gelieferten Kategorien als Upsert: ein geänderter Gruppenname
/// überschreibt die bestehende Zeile, es entsteht keine zweite. Kategorien, die in der
/// übergebenen Liste fehlen (in YNAB gelöscht), bleiben unverändert stehen, diese
/// Funktion löscht nie (ADR 0039, Punkt 3).
let upsertCategories (categories: YnabCategory list) : Async<unit> =
    async {
        if List.isEmpty categories then
            ()
        else
            use conn = Core.getConnection ()
            let now = dateToText DateTime.UtcNow

            let parameters =
                categories
                |> List.map (fun c ->
                    let (YnabCategoryId id) = c.Id

                    {|
                        ynab_category_id = id.ToString()
                        name = c.Name
                        group_name = c.GroupName
                        updated_at = now
                    |})

            do! conn.ExecuteAsync(upsertCategorySql, parameters) |> Async.AwaitTask |> Async.Ignore
    }

/// Die Zuordnung Kategorie-Id auf Gruppenname für den Lesepfad (`ledger-h6ptn`). Eine
/// leere Tabelle liefert eine leere Map, nie einen Fehler: nach einem frischen Deploy
/// ohne gelaufenen Spiegel-Lauf ist die Tabelle leer, das ist der vorgesehene Zustand.
let loadCategoryGroups () : Async<Map<Guid, string>> =
    async {
        use conn = Core.getConnection ()

        let! rows =
            conn.QueryAsync<CategoryRow>("SELECT * FROM ledger_categories") |> Async.AwaitTask

        return
            rows
            |> Seq.map (fun r -> Guid.Parse r.ynab_category_id, r.group_name)
            |> Map.ofSeq
    }
