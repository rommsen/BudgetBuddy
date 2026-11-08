module LedgerEventMigrationTests

// Übergang des Kassenbuch-Logs von `ledger_entry_events` nach `events` (ledger-k9tm2,
// ADR 0083 Abschnitt 6). Jeder Fall öffnet eine eigene In-Memory-Datenbank mit dem echten
// Kassenbuch-Schema. Den Stand vor der Migration baut `toLegacyLog`: die über
// `appendAndApplyOn` geschriebenen Ereignisse wandern in eine Tabelle in der früheren Form,
// `events` wird leer neu angelegt. Die Probe gegen eine Kopie der Produktions-Datenbank läuft
// nur mit `LEDGER_MIGRATION_PROBE_DB` (Liste "(requires .env)").

open System
open System.IO

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Dapper
open Microsoft.Data.Sqlite
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence

// ============================================
// Bausteine
// ============================================

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }

let private at (day: int) (hour: int) = DateTime(2026, 10, day, hour, 0, 0, DateTimeKind.Utc)

let private account = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000a001")

let private categoryRef (n: int) (name: string) : CategoryRef = {
    CategoryId = YnabCategoryId (Guid.Parse $"0195c000-0000-7000-8000-00000000c{n:D3}")
    CategoryName = name
}

let private eventAt (entryId: EntryId) (occurredAt: DateTime) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = occurredAt; Actor = actor; Body = body }

let private observed (ynabId: string) : LedgerEventBody =
    EntryObservedInYnab {
        AccountId = account
        YnabTransactionId = ynabId
        ImportId = Some $"YNAB:{ynabId}"
        Date = at 1 0
        Amount = eur -50.00m
        Payee = Some "Markt"
        Memo = None
        Category = None
        Splits = [
            { Target = ToCategory (YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-00000000c101"), "Haushalt")
              Amount = eur -30.00m
              Memo = None }
            { Target = ToTransfer (YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000c102"), "Bar")
              Amount = eur -20.00m
              Memo = None }
        ]
        Cleared = Uncleared
        TransferAccountId = None
        TransferYnabTransactionId = None
        Approved = true
        FlagColor = None
        FlagName = None
    }

/// Eine Barbuchung ohne Kategorie; eine Regel darf sie danach kategorisieren.
let private cashExpense (amount: decimal) : LedgerEventBody =
    CashExpenseAdded {
        AccountId = account
        Date = at 2 0
        Amount = eur amount
        Payee = Some "Kiosk"
        Memo = None
        Category = None
        Cleared = Uncleared
        Approved = false
        ImportId = None
    }

let private byRule : LedgerEventBody =
    EntryCategorizedByRule {
        RuleName = "Kiosk"
        Category = Some (categoryRef 2 "Freizeit")
        Splits = []
        PayeeOverride = None
    }

let private ruleId = RuleId (Guid.Parse "0195c000-0000-7000-8000-00000000e001")

let private freshDb () =
    let conn = new SqliteConnection("Data Source=:memory:")
    conn.Open()
    createLedgerSchemaOn conn
    conn

let private exec (conn: SqliteConnection) (sql: string) =
    use cmd = new SqliteCommand(sql, conn)
    cmd.ExecuteNonQuery() |> ignore

let private scalar (conn: SqliteConnection) (sql: string) : int64 = conn.ExecuteScalar<int64>(sql)

/// Alle Zeilen einer Abfrage als Text, in der Reihenfolge der Abfrage.
let private dump (conn: SqliteConnection) (sql: string) : string list =
    use cmd = new SqliteCommand(sql, conn)
    use reader = cmd.ExecuteReader()
    [
        while reader.Read() do
            yield
                [ for i in 0 .. reader.FieldCount - 1 -> if reader.IsDBNull i then "NULL" else string (reader.GetValue i) ]
                |> String.concat "|"
    ]

/// Die frühere Log-Tabelle, wie `createLedgerSchemaOn` sie bis ledger-k9tm2 anlegte.
let private legacyDdl =
    """CREATE TABLE ledger_entry_events (
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
       CREATE INDEX idx_ledger_entry_events_entry ON ledger_entry_events(entry_id, sequence);"""

/// Baut den Stand vor der Migration: die Kassenbuch-Ereignisse aus `events` gehen in
/// Einfügereihenfolge in die frühere Tabelle, `events` wird leer neu angelegt (DROP ist
/// erlaubt, nur UPDATE und DELETE sperren die Trigger).
let private toLegacyLog (conn: SqliteConnection) =
    exec conn legacyDdl
    exec conn
        """INSERT INTO ledger_entry_events
             (entry_id, sequence, occurred_at, actor_kind, actor_rule_id, event_type, schema_version, payload)
           SELECT substr(stream_id, 14), stream_version, occurred_at, actor_kind, actor_ref, event_type, schema_version, payload
           FROM events WHERE context = 'ledger' ORDER BY position"""
    exec conn "DROP TABLE events"
    EventStore.createSchemaOn conn

let private legacyTableExists (conn: SqliteConnection) =
    scalar conn "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ledger_entry_events'" > 0L

let private legacyEnvelopes conn =
    dump conn
        "SELECT entry_id, sequence, occurred_at, actor_kind, actor_rule_id, event_type, schema_version, payload FROM ledger_entry_events ORDER BY rowid"

let private migratedEnvelopes conn =
    dump conn
        "SELECT substr(stream_id, 14), stream_version, occurred_at, actor_kind, actor_ref, event_type, schema_version, payload FROM events WHERE context = 'ledger' ORDER BY position"

let private allEvents conn = dump conn "SELECT * FROM events ORDER BY position"

/// Drei Buchungen mit verschränkten Ereignissen: eine YNAB-Beobachtung mit Splits und
/// Fremd-Ids, zwei Bob-Barbuchungen, eine davon später per Regel kategorisiert.
let private seed (conn: SqliteConnection) =
    let observedEntry = EntryId (Guid.NewGuid())
    let bobEarly = EntryId (Guid.NewGuid())
    let bobLate = EntryId (Guid.NewGuid())

    appendAndApplyOn conn [ eventAt observedEntry (at 1 8) YnabMirror (observed "ynab-k9tm2-1") ]
    appendAndApplyOn conn [ eventAt bobEarly (at 2 9) Bob (cashExpense -12.00m) ]
    appendAndApplyOn conn [
        eventAt bobLate (at 2 20) Bob (cashExpense -7.50m)
        eventAt bobEarly (at 3 8) (Rule ruleId) byRule
        eventAt bobEarly (at 3 9) Bob (EntryLinkedToYnab { YnabTransactionId = "ynab-k9tm2-2" })
    ]
    appendAndApplyOn conn [
        eventAt observedEntry (at 4 8) YnabMirror (EntryClearedStateChangeObservedInYnab { OldCleared = Uncleared; NewCleared = Cleared })
    ]
    observedEntry, bobEarly, bobLate

/// Die Bob-Summen, wie die Abfrage vor ledger-k9tm2 sie aus der früheren Tabelle las.
let private legacyBobAmounts (conn: SqliteConnection) (fromUtc: DateTime) (toUtc: DateTime) : Money list =
    use cmd =
        new SqliteCommand(
            "SELECT event_type, schema_version, payload FROM ledger_entry_events WHERE actor_kind = 'Bob' AND event_type = 'CashExpenseAdded' AND occurred_at >= @fromAt AND occurred_at < @toAt",
            conn
        )
    cmd.Parameters.AddWithValue("@fromAt", fromUtc.ToString("o")) |> ignore
    cmd.Parameters.AddWithValue("@toAt", toUtc.ToString("o")) |> ignore
    use reader = cmd.ExecuteReader()
    [
        while reader.Read() do
            match decodePayload (reader.GetString 0) (int (reader.GetInt64 1)) (reader.GetString 2) with
            | Ok (CashExpenseAdded p) -> yield p.Amount
            | other -> failtest $"unerwartet: {other}"
    ]

/// Die Herkunft je Buchung, wie die Abfrage vor ledger-k9tm2 sie aus der früheren Tabelle las.
let private legacyOrigins (conn: SqliteConnection) : Map<Guid, Actor> =
    use cmd = new SqliteCommand("SELECT entry_id, actor_kind, actor_rule_id FROM ledger_entry_events WHERE sequence = 1", conn)
    use reader = cmd.ExecuteReader()
    [
        while reader.Read() do
            let ruleRef = if reader.IsDBNull 2 then None else Some (reader.GetString 2)
            match actorFromColumns (reader.GetString 1) ruleRef with
            | Ok actor -> yield Guid.Parse (reader.GetString 0), actor
            | Error e -> failtest e
    ]
    |> Map.ofList

/// Eine Zeile in der früheren Tabelle mit ausdrücklicher rowid.
let private insertLegacy
    (conn: SqliteConnection)
    (rowid: int64)
    (entryId: Guid)
    (sequence: int64)
    (occurredAt: string)
    (body: LedgerEventBody)
    =
    use cmd =
        new SqliteCommand(
            """INSERT INTO ledger_entry_events
                 (rowid, entry_id, sequence, occurred_at, actor_kind, actor_rule_id, event_type, schema_version, payload)
               VALUES (@rowid, @entry_id, @sequence, @occurred_at, 'QuickAdd', NULL, @event_type, @schema_version, @payload)""",
            conn
        )
    cmd.Parameters.AddWithValue("@rowid", rowid) |> ignore
    cmd.Parameters.AddWithValue("@entry_id", entryId.ToString()) |> ignore
    cmd.Parameters.AddWithValue("@sequence", sequence) |> ignore
    cmd.Parameters.AddWithValue("@occurred_at", occurredAt) |> ignore
    cmd.Parameters.AddWithValue("@event_type", eventTypeName body) |> ignore
    cmd.Parameters.AddWithValue("@schema_version", schemaVersion body) |> ignore
    cmd.Parameters.AddWithValue("@payload", encodePayload body) |> ignore
    cmd.ExecuteNonQuery() |> ignore

let private allTrue (probe: LedgerEventMigrationProbe) =
    [
        "EnvelopeUnchanged", probe.EnvelopeUnchanged
        "StreamVersionsGapless", probe.StreamVersionsGapless
        "ReplayUnchanged", probe.ReplayUnchanged
        "ReplayMatchesEntries", probe.ReplayMatchesEntries
        "ReplayMatchesSplitIndexes", probe.ReplayMatchesSplitIndexes
        "ReplayMatchesLinks", probe.ReplayMatchesLinks
        "LegacyTableGone", probe.LegacyTableGone
    ]

// ============================================
// Migration
// ============================================

[<Tests>]
let migrationTests =
    testList "Kassenbuch-Migration nach events" [

        testCase "Replay vor und nach der Migration gleich und gleich ledger_entries, Split-Tabellen und Verknüpfungen" <| fun () ->
            use conn = freshDb ()
            seed conn |> ignore
            let recordedBefore = loadAllEventsOn conn null
            toLegacyLog conn

            let probe = probeLedgerEventMigrationOn conn

            for name, ok in allTrue probe do
                Expect.isTrue ok name
            Expect.equal (probe.EventsBefore, probe.EventsAfter, probe.Entries, probe.EntryRows) (6, 6, 3, 3) "Zahlen"
            Expect.equal (loadAllEventsOn conn null) recordedBefore "dieselben Ereignisse je Buchung"
            Expect.isGreaterThan (scalar conn "SELECT COUNT(*) FROM ledger_entry_split_categories") 0L "Split-Kategorien vorhanden"
            Expect.isGreaterThan (scalar conn "SELECT COUNT(*) FROM ledger_entry_split_transfers") 0L "Split-Überträge vorhanden"
            Expect.isGreaterThan (scalar conn "SELECT COUNT(*) FROM ledger_entry_links") 2L "Verknüpfungen vorhanden"

        // Gegenprobe: die Probe meldet eine Abweichung und ist damit kein Selbstläufer.
        testCase "die Probe meldet eine Zustandszeile, die nicht zum Replay passt" <| fun () ->
            use conn = freshDb ()
            let observedEntry, _, _ = seed conn
            toLegacyLog conn
            let (EntryId id) = observedEntry
            exec conn $"UPDATE ledger_entries SET payee = 'verändert' WHERE id = '{id}'"
            exec conn $"DELETE FROM ledger_entry_links WHERE entry_id = '{id}' AND system = 'importid'"

            let probe = probeLedgerEventMigrationOn conn

            Expect.isFalse probe.ReplayMatchesEntries "Zustand weicht ab"
            Expect.isFalse probe.ReplayMatchesLinks "Verknüpfung fehlt"
            Expect.isTrue probe.ReplayUnchanged "das Log selbst ist unverändert"

        testCase "Typ, Version, Payload, occurred_at als Text und Actor bleiben je Ereignis gleich, position folgt der rowid" <| fun () ->
            use conn = freshDb ()
            seed conn |> ignore
            toLegacyLog conn
            let before = legacyEnvelopes conn

            let copied = migrateLedgerEventsOn conn

            Expect.equal copied before.Length "Zahl der kopierten Ereignisse"
            Expect.equal (migratedEnvelopes conn) before "Umschlag und Inhalt je Ereignis in rowid-Reihenfolge"
            Expect.isTrue (before |> List.exists (fun row -> row.Contains "|Rule|")) "ein Ereignis mit Actor-Referenz"
            Expect.equal
                (dump conn "SELECT DISTINCT context, COALESCE(command_id, 'NULL') FROM events")
                [ "ledger|NULL" ]
                "Kontext ledger, keine Befehls-Id"
            Expect.equal (scalar conn "SELECT COUNT(DISTINCT event_id) FROM events") (int64 before.Length) "event_id je Ereignis neu"

        testCase "stream_version ist je Buchung lückenlos ab 1" <| fun () ->
            use conn = freshDb ()
            let observedEntry, bobEarly, bobLate = seed conn
            toLegacyLog conn
            migrateLedgerEventsOn conn |> ignore

            let versions (EntryId id) =
                dump conn $"SELECT stream_version FROM events WHERE stream_id = 'ledger:entry:{id}' ORDER BY position"

            Expect.equal (versions observedEntry) [ "1"; "2" ] "YNAB-Buchung"
            Expect.equal (versions bobEarly) [ "1"; "2"; "3" ] "Bob früh"
            Expect.equal (versions bobLate) [ "1" ] "Bob spät"

        testCase "fällt sequence in rowid-Reihenfolge, bricht die Migration ab und lässt beide Tabellen unverändert" <| fun () ->
            use conn = freshDb ()
            exec conn legacyDdl
            let fine = Guid.NewGuid()
            let swapped = Guid.NewGuid()
            insertLegacy conn 1L fine 1L "2026-10-01T08:00:00.0000000Z" (cashExpense -1.00m)
            insertLegacy conn 2L swapped 2L "2026-10-01T09:00:00.0000000Z" (EntryLinkedToYnab { YnabTransactionId = "y" })
            insertLegacy conn 3L swapped 1L "2026-10-01T08:30:00.0000000Z" (cashExpense -2.00m)
            let legacyBefore = legacyEnvelopes conn
            let eventsBefore = allEvents conn

            let error = Expect.throwsC (fun () -> migrateLedgerEventsOn conn |> ignore) id

            Expect.stringContains error.Message (swapped.ToString()) "nennt die Buchung"
            Expect.stringContains error.Message "erwartet laufende Nummer 1, gefunden 2" "erwartete und gefundene Nummer"
            Expect.isTrue (legacyTableExists conn) "alte Tabelle steht noch"
            Expect.equal (legacyEnvelopes conn) legacyBefore "alte Tabelle unverändert"
            Expect.equal (allEvents conn) eventsBefore "events unverändert"

        // Eine Id, die nicht im Format D steht (hier großgeschrieben), ergäbe einen Stream, der
        // nicht zu `ledger_entries.id` passt; die Migration bricht ab statt still umzudeuten.
        testCase "eine Buchungs-Id außerhalb des Guid-Formats D bricht die Migration ab und lässt beide Tabellen unverändert" <| fun () ->
            use conn = freshDb ()
            exec conn legacyDdl
            let entry = Guid.NewGuid()
            insertLegacy conn 1L entry 1L "2026-10-01T08:00:00.0000000Z" (cashExpense -1.00m)
            exec conn "UPDATE ledger_entry_events SET entry_id = upper(entry_id)"
            let legacyBefore = legacyEnvelopes conn
            let eventsBefore = allEvents conn

            let error = Expect.throwsC (fun () -> migrateLedgerEventsOn conn |> ignore) id

            Expect.stringContains error.Message (entry.ToString().ToUpperInvariant()) "nennt die Buchung"
            Expect.isTrue (legacyTableExists conn) "alte Tabelle steht noch"
            Expect.equal (legacyEnvelopes conn) legacyBefore "alte Tabelle unverändert"
            Expect.equal (allEvents conn) eventsBefore "events unverändert"

        // Belegt die Transaktion: der Fehler kommt erst beim Schreiben der zweiten Zeile (UNIQUE
        // auf stream_id, stream_version), nach der Abbruchprüfung und nach einer schon
        // geschriebenen ersten Zeile, die zurückgenommen werden muss.
        testCase "scheitert das Schreiben nach events, bleiben beide Tabellen unverändert" <| fun () ->
            use conn = freshDb ()
            let clashing = EntryId (Guid.NewGuid())
            appendAndApplyOn conn [ eventAt clashing (at 1 8) Bob (cashExpense -4.00m) ]
            exec conn legacyDdl
            let (EntryId clashingGuid) = clashing
            insertLegacy conn 1L (Guid.NewGuid()) 1L "2026-10-01T07:00:00.0000000Z" (cashExpense -3.00m)
            insertLegacy conn 2L clashingGuid 1L "2026-10-01T08:00:00.0000000Z" (cashExpense -4.00m)
            let legacyBefore = legacyEnvelopes conn
            let eventsBefore = allEvents conn

            Expect.throws (fun () -> migrateLedgerEventsOn conn |> ignore) "UNIQUE greift beim Schreiben"

            Expect.isTrue (legacyTableExists conn) "alte Tabelle steht noch"
            Expect.equal (legacyEnvelopes conn) legacyBefore "alte Tabelle unverändert"
            Expect.equal (allEvents conn) eventsBefore "events unverändert"

        testCase "ein Zeitpunkt ohne Zonenangabe wird unverändert kopiert und blockiert nicht" <| fun () ->
            use conn = freshDb ()
            exec conn legacyDdl
            let entry = Guid.NewGuid()
            insertLegacy conn 1L entry 1L "2026-01-15T10:00:00" (cashExpense -3.00m)

            Expect.equal (migrateLedgerEventsOn conn) 1 "kopiert"

            Expect.equal (dump conn "SELECT occurred_at FROM events") [ "2026-01-15T10:00:00" ] "Text unverändert"
            let recorded = loadAllEventsOn conn null
            Expect.equal (recorded |> List.map (fun r -> r.Event.OccurredAt)) [ DateTime(2026, 1, 15, 10, 0, 0) ] "lesbar"

        testCase "ein zweiter Start nach der Migration ändert nichts" <| fun () ->
            use conn = freshDb ()
            seed conn |> ignore
            toLegacyLog conn
            createLedgerSchemaOn conn
            let afterFirst = allEvents conn
            Expect.isFalse (legacyTableExists conn) "nach dem ersten Start entfernt"

            createLedgerSchemaOn conn

            Expect.equal (migrateLedgerEventsOn conn) 0 "nichts zu tun"
            Expect.equal (allEvents conn) afterFirst "events unverändert"

        testCase "die Bob-Tagesgrenze liest vor und nach der Migration dieselben Summen, nur aus dem Kassenbuch" <| fun () ->
            use conn = freshDb ()
            seed conn |> ignore
            toLegacyLog conn
            let windows = [ at 2 0, at 3 0; at 2 0, at 2 20; at 2 20, at 2 21; at 1 0, at 5 0; at 4 0, at 5 0 ]
            let before = windows |> List.map (fun (f, t) -> legacyBobAmounts conn f t)
            Expect.equal before.Head [ eur -12.00m; eur -7.50m ] "vorher: beide Bob-Buchungen am Tag"

            migrateLedgerEventsOn conn |> ignore
            // Ein fremder Kontext mit derselben Actor-Art und demselben Typ zählt nicht mit.
            use tx = conn.BeginTransaction()
            EventStore.appendOn conn tx {
                Context = "budgeting"
                CommandId = None
                Streams = [
                    {
                        Aggregate = "category"
                        Id = EventStore.GuidId (Guid.NewGuid())
                        Events = [
                            {
                                EventType = "CashExpenseAdded"
                                SchemaVersion = schemaVersion (cashExpense -99.00m)
                                Payload = encodePayload (cashExpense -99.00m)
                                OccurredAt = at 2 10
                                ActorKind = "Bob"
                                ActorRef = None
                            }
                        ]
                    }
                ]
            } EventStore.noReadModels
            |> ignore
            tx.Commit()

            let after = windows |> List.map (fun (f, t) -> loadBobBookedAmountsBetweenOn conn f t |> List.sortBy (fun m -> m.Amount))
            Expect.equal after (before |> List.map (List.sortBy (fun m -> m.Amount))) "gleiche Summen je Zeitraum"

        testCase "loadEntryOrigins liefert vor und nach der Migration dieselbe Herkunft je Buchung" <| fun () ->
            use conn = freshDb ()
            let observedEntry, bobEarly, bobLate = seed conn
            toLegacyLog conn
            let before = legacyOrigins conn
            let id (EntryId g) = g
            Expect.equal before (Map.ofList [ id observedEntry, YnabMirror; id bobEarly, Bob; id bobLate, Bob ]) "vorher"

            migrateLedgerEventsOn conn |> ignore

            Expect.equal (loadEntryOriginsOn conn) before "nachher gleich"

        // Kein stiller Default: eine Buchung, deren erstes Ereignis keinen lesbaren Actor trägt,
        // fiel früher still aus der Herkunft heraus.
        testCase "eine unlesbare Herkunft wird gemeldet statt ausgelassen" <| fun () ->
            use conn = freshDb ()
            let entry = Guid.NewGuid()
            use tx = conn.BeginTransaction()
            EventStore.appendOn conn tx {
                Context = "ledger"
                CommandId = None
                Streams = [
                    {
                        Aggregate = "entry"
                        Id = EventStore.GuidId entry
                        Events = [
                            {
                                EventType = "CashExpenseAdded"
                                SchemaVersion = schemaVersion (cashExpense -5.00m)
                                Payload = encodePayload (cashExpense -5.00m)
                                OccurredAt = at 2 10
                                ActorKind = "Unbekannt"
                                ActorRef = None
                            }
                        ]
                    }
                ]
            } EventStore.noReadModels
            |> ignore
            tx.Commit()

            let error = Expect.throwsC (fun () -> loadEntryOriginsOn conn |> ignore) id
            Expect.stringContains error.Message (entry.ToString()) "nennt den Stream"

        testCase "neue Ereignisse landen nach der Migration in events hinter den migrierten" <| fun () ->
            use conn = freshDb ()
            let observedEntry, _, _ = seed conn
            toLegacyLog conn
            migrateLedgerEventsOn conn |> ignore

            appendAndApplyOn conn [
                eventAt observedEntry (at 5 8) YnabMirror (EntryApprovalChangeObservedInYnab { OldApproved = true; NewApproved = false })
            ]

            let (EntryId id) = observedEntry
            Expect.equal
                (dump conn $"SELECT context, stream_version, event_type FROM events WHERE stream_id = 'ledger:entry:{id}' ORDER BY stream_version")
                [ "ledger|1|EntryObservedInYnab"; "ledger|2|EntryClearedStateChangeObservedInYnab"; "ledger|3|EntryApprovalChangeObservedInYnab" ]
                "fortlaufend hinter den migrierten"
            Expect.isFalse (legacyTableExists conn) "keine alte Tabelle"
    ]

// ============================================
// Probe gegen eine Kopie der Produktions-Datenbank (opt-in)
// ============================================

/// Opt-in: nur mit `LEDGER_MIGRATION_PROBE_DB` (Pfad einer Datenbank-Datei). Die Datei
/// selbst wird nie geöffnet; die Probe läuft auf einer Kopie im Temp-Verzeichnis, die danach
/// gelöscht wird. Ins Protokoll gehen nur Zahlen und Wahrheitswerte.
[<Tests>]
let probeTests =
    testList "Kassenbuch-Migration: Probe gegen eine Kopie (requires .env)" [

        testCase "die Migration einer Kopie behält jedes Ereignis und ergibt denselben Zustand" <| fun () ->
            let source = Environment.GetEnvironmentVariable "LEDGER_MIGRATION_PROBE_DB"
            if String.IsNullOrWhiteSpace source then
                Tests.skiptest "LEDGER_MIGRATION_PROBE_DB nicht gesetzt - Probe übersprungen"
            if not (File.Exists source) then
                failtest "LEDGER_MIGRATION_PROBE_DB zeigt auf keine Datei"

            let copy = Path.Combine(Path.GetTempPath(), $"bb-ledger-probe-{Guid.NewGuid():N}.db")
            let sidecars = [ "-wal"; "-shm" ]

            try
                File.Copy(source, copy)
                for suffix in sidecars do
                    if File.Exists(source + suffix) then File.Copy(source + suffix, copy + suffix)

                let probe =
                    use conn = new SqliteConnection($"Data Source={copy};Pooling=False")
                    conn.Open()
                    probeLedgerEventMigrationOn conn

                printfn "Kassenbuch-Migrationsprobe:"
                printfn "  Ereignisse vorher: %d, nachher: %d" probe.EventsBefore probe.EventsAfter
                printfn "  Buchungen im Log: %d, Zeilen in ledger_entries: %d" probe.Entries probe.EntryRows
                for name, ok in allTrue probe do
                    printfn "  %s: %b" name ok

                Expect.equal probe.EventsAfter probe.EventsBefore "gleiche Zahl der Ereignisse"
                for name, ok in allTrue probe do
                    Expect.isTrue ok name
            finally
                for path in copy :: (sidecars |> List.map (fun s -> copy + s)) do
                    if File.Exists path then File.Delete path
    ]
