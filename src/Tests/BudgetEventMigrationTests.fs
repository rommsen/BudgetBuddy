module BudgetEventMigrationTests

// Budget-Ereignisse in `events` (budgeting-hyn77, ADR 0084 Punkte 1, 2 und 4): Codec, Startmigration der
// fünf früheren Tabellen, Probe, Leser vorher und nachher, Replay und Abbruchregeln. Jeder Migrationsfall
// öffnet eine eigene In-Memory-Datenbank mit `events` und den früheren Tabellen in ihrer letzten Form
// (`legacyDdl`). Die Probe gegen eine Kopie der Produktions-Datenbank läuft nur mit
// `BUDGET_MIGRATION_PROBE_DB` (Liste "(requires .env)").

open System
open System.IO

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Dapper
open Microsoft.Data.Sqlite
open Shared.Domain
open Shared.Budgeting
open Budgeting.Events
open Budgeting.EventMigration
open Budgeting.EventMigrationProbe
open Budgeting.Persistence

let private run (a: Async<'T>) = Async.RunSynchronously a

// ============================================
// Codec
// ============================================

let private line (name: string) (previous: int64) (assigned: int64) : BudgetLine =
    { LineId = Guid.NewGuid()
      CategoryId = Guid.NewGuid()
      CategoryName = name
      PreviousAssigned = previous
      Assigned = assigned }

let private reference () : WriteBackRef = { DecisionEventId = Guid.NewGuid(); CategoryId = Guid.NewGuid() }
let private step (n: int) : GoalStepRef = { DecisionEventId = Guid.NewGuid(); Step = n }

/// Je Ereignistyp ein Beispiel; Beträge auch negativ und über `Int32.MaxValue`.
let private samples : BudgetEvent list =
    [ BudgetEvent.AssignmentChangedInBb { Month = "2026-11"; Line = line "Urlaub" 10000L -20000L }
      BudgetEvent.MoneyMoved
          { Month = "2026-11"
            From = line "Lebensmittel" 100000L 70000L
            To = line "Urlaub" 20000L 50000L
            Amount = 30000L }
      BudgetEvent.TargetsApplied
          { Month = "2026-12"
            Lines = [ line "Miete" 0L 80000000000L; line "Strom" 0L 60000L ]
            SkippedPaused = [ { CategoryId = Guid.NewGuid(); CategoryName = "Ausflug"; Underfunded = 15000L } ] }
      BudgetEvent.TargetPaused { Month = "2026-11"; CategoryId = Guid.NewGuid(); CategoryName = "Ausflug" }
      BudgetEvent.TargetPauseLifted { Month = "2026-11"; CategoryId = Guid.NewGuid(); CategoryName = "Ausflug" }
      BudgetEvent.AssignmentWrittenBackToYnab (reference ())
      BudgetEvent.AssignmentWriteBackFailed (reference (), "Rate-Limit")
      BudgetEvent.AssignmentWriteBackSupersededInYnab (reference ())
      BudgetEvent.AssignmentWriteBackWithdrawn (reference ())
      BudgetEvent.YnabGoalChangedFromBb
          { Month = "2026-11"
            CategoryName = "Urlaub"
            ChangeKind = TargetKindChanged Weekly
            OldGoalTarget = Some 50000L
            NewGoalTarget = Some 12000L
            OldDate = Some "2027-01-01"
            NewDate = None }
      BudgetEvent.YnabGoalChangedFromBb
          { Month = "2026-11"
            CategoryName = "Urlaub"
            ChangeKind = TargetRemoved
            OldGoalTarget = Some 50000L
            NewGoalTarget = None
            OldDate = None
            NewDate = None }
      BudgetEvent.YnabGoalStepWrittenToYnab (step 1)
      BudgetEvent.YnabGoalStepWriteFailed (step 2, "goal_target_date is invalid")
      BudgetEvent.YnabGoalStepSupersededInYnab (step 1) ]

[<Tests>]
let codecTests =
    testList "Budget-Ereignisse: Codec" [
        for i, event in List.indexed samples do
            test $"Round-Trip {i + 1}: {eventTypeName event} kommt unverändert zurück" {
                let payload = encodePayload event
                Expect.equal (decodePayload (eventTypeName event) SchemaVersion payload) (Ok event) "unverändert zurück"
            }

        test "Milliunits stehen als JSON-Zahl in camelCase-Feldern, der Monat als YYYY-MM" {
            let payload = encodePayload (BudgetEvent.AssignmentChangedInBb { Month = "2026-11"; Line = { line "U" 1L 80000000000L with CategoryName = "U" } })
            Expect.stringContains payload "\"assigned\":80000000000" "Zahl, kein String"
            Expect.stringContains payload "\"previousAssigned\":1" "camelCase"
            Expect.stringContains payload "\"month\":\"2026-11\"" "Monat"
        }

        test "Ein unbekannter Typ, eine unbekannte Version und ein Monat in anderer Form sind Fehler" {
            Expect.isError (decodePayload "TargetSet" 1 "{}") "unbekannter Typ"
            let payload = encodePayload samples.Head
            Expect.isError (decodePayload "AssignmentChangedInBb" 2 payload) "unbekannte Version"
            Expect.isError (decodePayload "AssignmentChangedInBb" 1 (payload.Replace("2026-11", "2026-11-01"))) "Monat als Monatserster"
        }

        test "Actor: Entscheidungen, Pausen und Zurückziehen von Roman, Ergebnisse vom Spiegel" {
            Expect.equal
                (samples |> List.map (fun e -> eventTypeName e, actorKindOf e) |> List.filter (fun (_, a) -> a = "YnabMirror") |> List.map fst)
                [ "AssignmentWrittenBackToYnab"
                  "AssignmentWriteBackFailed"
                  "AssignmentWriteBackSupersededInYnab"
                  "YnabGoalStepWrittenToYnab"
                  "YnabGoalStepWriteFailed"
                  "YnabGoalStepSupersededInYnab" ]
                "nur die Ergebnisse"
        }

        test "Der Monat des Ereignismodells kommt aus dem Monatsersten, anderes wird gemeldet" {
            Expect.equal (monthIdOf "2026-11-01") (Ok "2026-11") "Monatserster"
            Expect.isError (monthIdOf "2026-11-02") "kein Erster"
            Expect.isError (monthIdOf "2026-13-01") "kein Monat"
            Expect.isError (monthIdOf "sperre") "Text"
            Expect.equal (streamIdOf (MonthStream "2026-11")) "budgeting:month:2026-11" "Monats-Stream"
            let k = Guid.NewGuid()
            Expect.equal (streamIdOf (TargetStream k)) $"budgeting:target:{k}" "Target-Stream im Format D"
            Expect.equal (categoryOfTargetStream (streamIdOf (TargetStream k))) (Some k) "zurück"
            Expect.equal (categoryOfTargetStream "budgeting:month:2026-11") None "kein Target-Stream"
        }
    ]

// ============================================
// Altbestand
// ============================================

/// Die fünf Tabellen, wie `initializeBudgetingSchema` sie bis budgeting-hyn77 anlegte.
let private legacyDdl =
    """
    CREATE TABLE budget_movements (
        id TEXT PRIMARY KEY, application_id TEXT NOT NULL, occurred_at TEXT NOT NULL, origin TEXT NOT NULL,
        month TEXT NOT NULL, category_id TEXT NOT NULL, category_name TEXT NOT NULL,
        old_assigned INTEGER NOT NULL, new_assigned INTEGER NOT NULL);
    CREATE INDEX idx_budget_movements_application ON budget_movements (application_id);
    CREATE TABLE budget_movement_results (
        sequence INTEGER PRIMARY KEY AUTOINCREMENT, movement_id TEXT NOT NULL, occurred_at TEXT NOT NULL,
        status TEXT NOT NULL, reason TEXT);
    CREATE INDEX idx_budget_movement_results_movement ON budget_movement_results (movement_id);
    CREATE TRIGGER budget_movements_no_update BEFORE UPDATE ON budget_movements
    BEGIN SELECT RAISE(ABORT, 'budget_movements ist unveränderlich'); END;
    CREATE TRIGGER budget_movements_no_delete BEFORE DELETE ON budget_movements
    BEGIN SELECT RAISE(ABORT, 'budget_movements ist unveränderlich'); END;
    CREATE TRIGGER budget_movement_results_no_update BEFORE UPDATE ON budget_movement_results
    BEGIN SELECT RAISE(ABORT, 'budget_movement_results ist append-only'); END;
    CREATE TRIGGER budget_movement_results_no_delete BEFORE DELETE ON budget_movement_results
    BEGIN SELECT RAISE(ABORT, 'budget_movement_results ist append-only'); END;
    CREATE TABLE target_pause_events (
        id TEXT PRIMARY KEY, occurred_at TEXT NOT NULL, month TEXT NOT NULL, category_id TEXT NOT NULL,
        category_name TEXT NOT NULL, kind TEXT NOT NULL);
    CREATE INDEX idx_target_pause_events_month ON target_pause_events (month, category_id);
    CREATE TRIGGER target_pause_events_no_update BEFORE UPDATE ON target_pause_events
    BEGIN SELECT RAISE(ABORT, 'target_pause_events ist append-only'); END;
    CREATE TRIGGER target_pause_events_no_delete BEFORE DELETE ON target_pause_events
    BEGIN SELECT RAISE(ABORT, 'target_pause_events ist append-only'); END;
    CREATE TABLE target_changes (
        id TEXT PRIMARY KEY, occurred_at TEXT NOT NULL, month TEXT NOT NULL, category_id TEXT NOT NULL,
        category_name TEXT NOT NULL, change_kind TEXT NOT NULL, old_goal_target INTEGER, new_goal_target INTEGER,
        old_date TEXT, new_date TEXT, new_frequency TEXT);
    CREATE INDEX idx_target_changes_category ON target_changes (category_id, month);
    CREATE TABLE target_change_results (
        sequence INTEGER PRIMARY KEY AUTOINCREMENT, change_id TEXT NOT NULL, occurred_at TEXT NOT NULL,
        status TEXT NOT NULL, reason TEXT, step INTEGER);
    CREATE INDEX idx_target_change_results_change ON target_change_results (change_id);
    CREATE TRIGGER target_changes_no_update BEFORE UPDATE ON target_changes
    BEGIN SELECT RAISE(ABORT, 'target_changes ist unveränderlich'); END;
    CREATE TRIGGER target_changes_no_delete BEFORE DELETE ON target_changes
    BEGIN SELECT RAISE(ABORT, 'target_changes ist unveränderlich'); END;
    CREATE TRIGGER target_change_results_no_update BEFORE UPDATE ON target_change_results
    BEGIN SELECT RAISE(ABORT, 'target_change_results ist append-only'); END;
    CREATE TRIGGER target_change_results_no_delete BEFORE DELETE ON target_change_results
    BEGIN SELECT RAISE(ABORT, 'target_change_results ist append-only'); END;
    """

let private freshDb () =
    let conn = new SqliteConnection("Data Source=:memory:")
    conn.Open()
    EventStore.createSchemaOn conn
    conn.Execute(legacyDdl) |> ignore
    conn

let private time (hour: int) (minute: int) = $"2026-10-03T{hour:D2}:{minute:D2}:00.0000000Z"

let private movementRow (conn: SqliteConnection) (id: Guid) (app: Guid) (at: string) (origin: string) (month: string) (category: Guid) (name: string) (oldA: int64) (newA: int64) =
    conn.Execute(
        "INSERT INTO budget_movements VALUES (@id, @app, @at, @origin, @month, @category, @name, @oldA, @newA)",
        {| id = string id; app = string app; at = at; origin = origin; month = month; category = string category; name = name; oldA = oldA; newA = newA |})
    |> ignore

let private movementResult (conn: SqliteConnection) (movement: string) (at: string) (status: string) (reason: string) =
    conn.Execute(
        "INSERT INTO budget_movement_results (movement_id, occurred_at, status, reason) VALUES (@movement, @at, @status, @reason)",
        {| movement = movement; at = at; status = status; reason = reason |})
    |> ignore

let private pauseRow (conn: SqliteConnection) (id: string) (at: string) (month: string) (category: Guid) (name: string) (kind: string) =
    conn.Execute(
        "INSERT INTO target_pause_events VALUES (@id, @at, @month, @category, @name, @kind)",
        {| id = id; at = at; month = month; category = string category; name = name; kind = kind |})
    |> ignore

let private changeRow
    (conn: SqliteConnection) (id: string) (at: string) (category: Guid) (name: string) (kind: string)
    (oldGoal: obj) (newGoal: obj) (newDate: obj) (frequency: obj) =
    conn.Execute(
        "INSERT INTO target_changes (id, occurred_at, month, category_id, category_name, change_kind, old_goal_target, \
            new_goal_target, old_date, new_date, new_frequency) \
         VALUES (@id, @at, '2026-10-01', @category, @name, @kind, @oldGoal, @newGoal, NULL, @newDate, @frequency)",
        {| id = id; at = at; category = string category; name = name; kind = kind; oldGoal = oldGoal; newGoal = newGoal; newDate = newDate; frequency = frequency |})
    |> ignore

let private changeResult (conn: SqliteConnection) (change: string) (at: string) (status: string) (reason: string) (step: obj) =
    conn.Execute(
        "INSERT INTO target_change_results (change_id, occurred_at, status, reason, step) VALUES (@change, @at, @status, @reason, @step)",
        {| change = change; at = at; status = status; reason = reason; step = step |})
    |> ignore

/// Die Ids des Altbestands, damit Tests sie nach der Migration wiederfinden.
type private Fixture = {
    X: Guid
    Y: Guid
    Z: Guid
    Applied: Guid
    AppliedLines: Guid * Guid
    Moved: Guid
    MovedLines: Guid * Guid
    Assigned: Guid
    AssignedLine: Guid
    OtherMonth: Guid
    Pauses: Guid list
    Changes: Guid * Guid
    LedgerPosition: int64
}

/// Ein Kassenbuch-Ereignis in `events`, vor dem Altbestand festgehalten.
let private ledgerEvent (conn: SqliteConnection) : int64 =
    conn.Execute(
        "INSERT INTO events (event_id, context, stream_id, stream_version, occurred_at, actor_kind, event_type, schema_version, payload) \
         VALUES (@id, 'ledger', @stream, 1, '2026-09-01T08:00:00.0000000Z', 'YnabMirror', 'EntryDeletionObservedInYnab', 1, '{}')",
        {| id = string (Guid.NewGuid()); stream = $"ledger:entry:{Guid.NewGuid()}" |})
    |> ignore
    conn.ExecuteScalar<int64>("SELECT MAX(position) FROM events")

/// Der Altbestand: jede Herkunft, jeder Status, jede Pause-Art samt Altwert "Fortgesetzt", eine Änderung mit
/// Ergebnis ohne Schritt und eine mit zwei Schritten, Entscheidungen und Ergebnisse mit gleichem Zeitpunkt.
let private seedLegacy (conn: SqliteConnection) : Fixture =
    let ledger = ledgerEvent conn
    let x, y, z = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    let applied, a1, a2 = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    movementRow conn a1 applied (time 8 0) "Targets anwenden" "2026-10-01" x "Miete" 0L 800000L
    movementRow conn a2 applied (time 8 0) "Targets anwenden" "2026-10-01" y "Strom" 0L 60000L
    let moved, m1, m2 = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    movementRow conn m1 moved (time 10 0) "Verschieben" "2026-10-01" x "Miete" 800000L 700000L
    movementRow conn m2 moved (time 10 0) "Verschieben" "2026-10-01" z "Urlaub" 0L 100000L
    let assigned, z1 = Guid.NewGuid(), Guid.NewGuid()
    movementRow conn z1 assigned (time 11 0) "Zuweisen" "2026-10-01" y "Strom" 60000L 70000L
    let otherMonth = Guid.NewGuid()
    movementRow conn (Guid.NewGuid()) otherMonth (time 12 0) "Zuweisen" "2026-11-01" x "Miete" 0L 5000L
    movementResult conn (string a1) (time 8 0) "written" null
    movementResult conn (string a2) (time 8 0) "failed" "Rate-Limit"
    movementResult conn (string a2) (time 9 30) "written" null
    movementResult conn (string m1) (time 10 0) "written" null
    movementResult conn (string m2) (time 10 5) "changed-in-ynab" null
    let pauses = List.init 4 (fun _ -> Guid.NewGuid())
    pauseRow conn (string pauses[0]) (time 8 30) "2026-10-01" x "Miete" "Pausiert"
    pauseRow conn (string pauses[1]) (time 9 0) "2026-10-01" x "Miete" "Fortgesetzt"
    pauseRow conn (string pauses[2]) (time 10 30) "2026-10-01" z "Urlaub" "Pausiert"
    pauseRow conn (string pauses[3]) (time 11 30) "2026-10-01" z "Urlaub" "Aufgehoben"
    let c1, c2 = Guid.NewGuid(), Guid.NewGuid()
    changeRow conn (string c1) (time 7 0) x "Miete" "Geändert" (box 50000L) (box 60000L) null null
    changeRow conn (string c2) (time 7 30) y "Strom" "Angelegt" null (box 600000L) (box "2027-06-01") (box "yearly")
    changeResult conn (string c1) (time 7 0) "written" null null
    changeResult conn (string c2) (time 7 30) "written" null (box 1L)
    changeResult conn (string c2) (time 7 30) "failed" "kaputt" (box 2L)
    { X = x
      Y = y
      Z = z
      Applied = applied
      AppliedLines = a1, a2
      Moved = moved
      MovedLines = m1, m2
      Assigned = assigned
      AssignedLine = z1
      OtherMonth = otherMonth
      Pauses = pauses
      Changes = c1, c2
      LedgerPosition = ledger }

/// Alle Zeilen einer Abfrage als Text.
let private dump (conn: SqliteConnection) (sql: string) : string list =
    use cmd = new SqliteCommand(sql, conn)
    use reader = cmd.ExecuteReader()
    [ while reader.Read() do
          yield
              [ for i in 0 .. reader.FieldCount - 1 -> if reader.IsDBNull i then "NULL" else string (reader.GetValue i) ]
              |> String.concat "|" ]

let private allEvents (conn: SqliteConnection) = dump conn "SELECT * FROM events ORDER BY position"

let private legacyContent (conn: SqliteConnection) =
    [ for table in legacyTables -> dump conn ("SELECT * FROM " + table + " ORDER BY rowid") ]

let private budgetStream (conn: SqliteConnection) (stream: string) : (int64 * string * string * int64) list =
    dump
        conn
        ("SELECT stream_version, event_type, occurred_at, position FROM events WHERE context = 'budgeting' AND stream_id = '"
         + stream
         + "' ORDER BY stream_version")
    |> List.map (fun row ->
        match row.Split '|' with
        | [| v; t; at; p |] -> int64 v, t, at, int64 p
        | _ -> failtest row)

let private ok (result: Result<'T, string>) : 'T =
    match result with
    | Ok value -> value
    | Error message -> failtest message

// ============================================
// Migration
// ============================================

[<Tests>]
let migrationTests =
    testList "Budget-Migration nach events" [

        testCase "Die Ereignisse stehen je Stream in fachlicher Reihenfolge, lückenlos, hinter dem Kassenbuch, mit unverändertem Zeitpunkt" <| fun () ->
            use conn = freshDb ()
            let f = seedLegacy conn
            Expect.equal (migrateBudgetEventsOn conn) 18 "18 Ereignisse"
            let streamOf (s: BudgetStream) = budgetStream conn (streamIdOf s)
            let shape rows = rows |> List.map (fun (v, t, at, _) -> v, t, at)
            Expect.equal
                (shape (streamOf (MonthStream "2026-10")))
                [ 1L, "TargetsApplied", time 8 0
                  2L, "AssignmentWrittenBackToYnab", time 8 0
                  3L, "AssignmentWriteBackFailed", time 8 0
                  4L, "TargetPaused", time 8 30
                  5L, "TargetPauseLifted", time 9 0
                  6L, "AssignmentWrittenBackToYnab", time 9 30
                  7L, "MoneyMoved", time 10 0
                  8L, "AssignmentWrittenBackToYnab", time 10 0
                  9L, "AssignmentWriteBackSupersededInYnab", time 10 5
                  10L, "TargetPaused", time 10 30
                  11L, "AssignmentChangedInBb", time 11 0
                  12L, "TargetPauseLifted", time 11 30 ]
                "Oktober: Entscheidung vor Ergebnis bei gleichem Zeitpunkt, Fortgesetzt als Aufheben"
            Expect.equal (shape (streamOf (MonthStream "2026-11"))) [ 1L, "AssignmentChangedInBb", time 12 0 ] "November"
            Expect.equal
                (shape (streamOf (TargetStream f.X)))
                [ 1L, "YnabGoalChangedFromBb", time 7 0; 2L, "YnabGoalStepWrittenToYnab", time 7 0 ]
                "Ergebnis ohne Schritt als Schritt 1"
            Expect.equal
                (shape (streamOf (TargetStream f.Y)))
                [ 1L, "YnabGoalChangedFromBb", time 7 30
                  2L, "YnabGoalStepWrittenToYnab", time 7 30
                  3L, "YnabGoalStepWriteFailed", time 7 30 ]
                "zwei Schritte"
            for s in [ MonthStream "2026-10"; MonthStream "2026-11"; TargetStream f.X; TargetStream f.Y ] do
                let positions = streamOf s |> List.map (fun (_, _, _, p) -> p)
                Expect.equal positions (List.sort positions) "position folgt der Version"
                Expect.isTrue (positions |> List.forall (fun p -> p > f.LedgerPosition)) "hinter dem Kassenbuch"
            Expect.isEmpty (existingLegacyTables conn null) "die fünf Tabellen sind entfernt"
            Expect.equal
                (dump conn "SELECT name FROM sqlite_master WHERE type IN ('index', 'trigger') AND (name LIKE '%budget_movement%' OR name LIKE '%target_pause%' OR name LIKE '%target_change%')")
                []
                "Indizes und Trigger mit entfernt"

        testCase "Die Ids bleiben: Gruppen-Id als event_id, Zeilen-Id als lineId, Pause- und Änderungs-Id als event_id" <| fun () ->
            use conn = freshDb ()
            let f = seedLegacy conn
            migrateBudgetEventsOn conn |> ignore
            let history = ok (queryMovements conn { CategoryId = None; Month = None; Origin = None })
            let ofApp app = history |> List.filter (fun m -> m.ApplicationId = app)
            let m1, m2 = f.MovedLines
            Expect.equal
                (ofApp f.Moved |> List.map (fun m -> m.MovementId, m.CategoryName, m.Counterpart, m.Latest))
                [ m2, "Urlaub", Some "Miete", Some ChangedInYnab; m1, "Miete", Some "Urlaub", Some Written ]
                "Verschieben mit Gegenseite"
            let a1, a2 = f.AppliedLines
            Expect.equal
                (ofApp f.Applied |> List.map (fun m -> m.MovementId, m.Latest))
                [ a2, Some Written; a1, Some Written ]
                "Targets anwenden, jüngstes Ergebnis"
            Expect.equal (ofApp f.Assigned |> List.map (fun m -> m.MovementId, m.Origin, m.Month)) [ f.AssignedLine, MoneyAssigned, "2026-10-01" ] "Zuweisen"
            let pauseIds =
                [ f.X; f.Z ]
                |> List.collect (fun c -> ok (queryPauseEvents conn c "2026-10-01"))
                |> List.map (function
                    | TargetPaused p
                    | PauseLifted p -> p.EventId
                    | _ -> Guid.Empty)
            Expect.equal (Set.ofList pauseIds) (Set.ofList f.Pauses) "Pause-Ids"
            let c1, c2 = f.Changes
            Expect.equal
                (ok (queryTargetChanges conn f.Y "2026-10-01") |> List.map (fun c -> c.ChangeId, c.ChangeKind, c.Latest, c.DateStep))
                [ c2, TargetCreated Yearly, Some Written, Some (Failed "kaputt") ]
                "Änderung mit Schritten"
            Expect.equal
                (ok (queryTargetChanges conn f.X "2026-10-01") |> List.map (fun c -> c.ChangeId, c.GoalBefore, c.GoalAfter))
                [ c1, Some { Amount = 50m; Currency = "EUR" }, Some { Amount = 60m; Currency = "EUR" } ]
                "Teilwerte"

        testCase "Jeder Leser liefert vor und nach der Migration dasselbe" <| fun () ->
            use conn = freshDb ()
            seedLegacy conn |> ignore
            let probe = probeBudgetEventMigrationOn conn
            Expect.equal
                probe.RowsBefore
                [ "budget_movements", 6
                  "budget_movement_results", 5
                  "target_pause_events", 4
                  "target_changes", 2
                  "target_change_results", 3 ]
                "Zeilen vorher"
            Expect.equal
                probe.EventsAfter
                ([ "AssignmentChangedInBb", 2
                   "AssignmentWriteBackFailed", 1
                   "AssignmentWriteBackSupersededInYnab", 1
                   "AssignmentWrittenBackToYnab", 3
                   "MoneyMoved", 1
                   "TargetPauseLifted", 2
                   "TargetPaused", 2
                   "TargetsApplied", 1
                   "YnabGoalChangedFromBb", 2
                   "YnabGoalStepWriteFailed", 1
                   "YnabGoalStepWrittenToYnab", 2 ]
                 |> List.sort)
                "Ereignisse je Typ"
            Expect.isTrue probe.StreamVersionsGapless "lückenlos"
            Expect.isTrue probe.OccurredAtUnchanged "Zeitpunkte als Text"
            Expect.isTrue probe.HistoryUnchanged "allgemeine Historie"
            Expect.isTrue probe.CategoryHistoriesUnchanged "Historie je Kategorie"
            Expect.isTrue probe.RecentCategoriesUnchanged "Zuletzt verwendet"
            Expect.isTrue probe.PausesUnchanged "Pausen je Monat"
            Expect.isTrue probe.LastTargetChangesUnchanged "letzte Target-Änderung"
            Expect.isTrue probe.ApplicationsUnchanged "Anwendungen"
            Expect.isTrue probe.LegacyTablesGone "Tabellen entfernt"

        // Gegenbeweis: die Probe vergleicht wirklich. Lagen Pausen in der rowid-Reihenfolge anders als in der
        // Zeit, ordnet `events` nach Zeitpunkt, und der Stand der Pause ändert sich.
        testCase "Die Probe meldet einen Leser, der nach der Migration anderes liefert" <| fun () ->
            use conn = freshDb ()
            let k = Guid.NewGuid()
            pauseRow conn (string (Guid.NewGuid())) (time 10 0) "2026-10-01" k "A" "Pausiert"
            pauseRow conn (string (Guid.NewGuid())) (time 9 0) "2026-10-01" k "A" "Aufgehoben"
            let probe = probeBudgetEventMigrationOn conn
            Expect.isFalse probe.PausesUnchanged "Pausenstand gekippt"
            Expect.isFalse probe.CategoryHistoriesUnchanged "Reihenfolge in der Historie gekippt"

        testCase "Replay: die Leser liefern aus migrierten Ereignissen dasselbe wie aus neu geschriebenen" <| fun () ->
            use migrated = freshDb ()
            let f = seedLegacy migrated
            migrateBudgetEventsOn migrated |> ignore
            use written = new SqliteConnection("Data Source=:memory:")
            written.Open()
            EventStore.createSchemaOn written
            let at (text: string) = DateTime.Parse(text, Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.RoundtripKind)
            let planned category name oldA newA : PlannedLine =
                { CategoryId = category; CategoryName = name; OldAssigned = oldA; NewAssigned = newA }
            let october = "2026-10-01"
            // In der Reihenfolge der Streams aus dem ersten Test.
            let c1 =
                recordTargetChangeOn
                    written
                    { OccurredAt = at (time 7 0); Month = october; CategoryId = f.X; CategoryName = "Miete"; Kind = TargetEdited
                      OldGoalTarget = Some 50000L; NewGoalTarget = Some 60000L; OldDate = None; NewDate = None }
            recordTargetChangeStepResultOn written f.X c1 1 (at (time 7 0)) Written
            let c2 =
                recordTargetChangeOn
                    written
                    { OccurredAt = at (time 7 30); Month = october; CategoryId = f.Y; CategoryName = "Strom"; Kind = TargetCreated Yearly
                      OldGoalTarget = None; NewGoalTarget = Some 600000L; OldDate = None; NewDate = Some "2027-06-01" }
            recordTargetChangeStepResultOn written f.Y c2 1 (at (time 7 30)) Written
            recordTargetChangeStepResultOn written f.Y c2 2 (at (time 7 30)) (Failed "kaputt")
            let applied =
                recordDecisionOn written (at (time 8 0)) october TargetsApplied [ planned f.X "Miete" 0L 800000L; planned f.Y "Strom" 0L 60000L ] []
            recordWriteBackOn written applied[0] (at (time 8 0)) WrittenBack
            recordWriteBackOn written applied[1] (at (time 8 0)) (WriteBackFailed "Rate-Limit")
            recordPauseOn written (at (time 8 30)) october f.X "Miete" Server.Budgeting.Domain.PauseKind.Paused |> ignore
            recordPauseOn written (at (time 9 0)) october f.X "Miete" Server.Budgeting.Domain.PauseKind.PauseLifted |> ignore
            recordWriteBackOn written applied[1] (at (time 9 30)) WrittenBack
            let moved =
                recordDecisionOn written (at (time 10 0)) october MoneyMoved [ planned f.X "Miete" 800000L 700000L; planned f.Z "Urlaub" 0L 100000L ] []
            recordWriteBackOn written moved[0] (at (time 10 0)) WrittenBack
            recordWriteBackOn written moved[1] (at (time 10 5)) SupersededInYnab
            recordPauseOn written (at (time 10 30)) october f.Z "Urlaub" Server.Budgeting.Domain.PauseKind.Paused |> ignore
            let assigned = recordDecisionOn written (at (time 11 0)) october MoneyAssigned [ planned f.Y "Strom" 60000L 70000L ] []
            recordPauseOn written (at (time 11 30)) october f.Z "Urlaub" Server.Budgeting.Domain.PauseKind.PauseLifted |> ignore
            let other = recordDecisionOn written (at (time 12 0)) "2026-11-01" MoneyAssigned [ planned f.X "Miete" 0L 5000L ] []

            let anyId (m: HistoryMovement) = { m with MovementId = Guid.Empty; ApplicationId = Guid.Empty }
            let anyEntry (e: HistoryEntry) =
                match e with
                | Movement m -> Movement (anyId m)
                | TargetPaused p -> TargetPaused { p with EventId = Guid.Empty }
                | PauseLifted p -> PauseLifted { p with EventId = Guid.Empty }
                | TargetChanged c -> TargetChanged { c with ChangeId = Guid.Empty }
            let anyState (s: MovementState) = { s with Movement = { s.Movement with Id = Guid.Empty; ApplicationId = Guid.Empty } }
            let readers (conn: SqliteConnection) (applications: Guid list) =
                ok (queryMovements conn { CategoryId = None; Month = None; Origin = None }) |> List.map anyId,
                [ for c in [ f.X; f.Y; f.Z ] do
                      for month in [ october; "2026-11-01" ] do
                          yield
                              ok (queryMovements conn { CategoryId = Some c; Month = Some month; Origin = None }) |> List.map anyId,
                              ok (queryPauseEvents conn c month) |> List.map anyEntry,
                              ok (queryTargetChanges conn c month) |> List.map (TargetChanged >> anyEntry) ],
                ok (queryRecentCategories conn MoneyMoved 10),
                [ october; "2026-11-01" ] |> List.map (queryPausedCategories conn >> ok),
                ok (queryLastTargetChanges conn),
                applications |> List.map (queryApplication conn >> ok >> List.map anyState)
            let fromMigration = readers migrated [ f.Applied; f.Moved; f.Assigned; f.OtherMonth ]
            let fromWrites =
                readers written ([ applied; moved; assigned; other ] |> List.map (fun ms -> ms.Head.ApplicationId))
            Expect.equal fromWrites fromMigration "gleiche Leser bis auf die Ids"

        testCase "Ein zweiter Start nach der Migration tut nichts und legt keine Tabelle neu an" <| fun () ->
            use conn = freshDb ()
            seedLegacy conn |> ignore
            migrateBudgetEventsOn conn |> ignore
            let before = allEvents conn
            Expect.equal (migrateBudgetEventsOn conn) 0 "nichts zu tun"
            Expect.equal (allEvents conn) before "events unverändert"
            Expect.isEmpty (existingLegacyTables conn null) "keine Tabelle neu angelegt"

    ]

// ============================================
// Abbruch
// ============================================

/// Eine Abbruchregel: der Altbestand bekommt eine verbotene Zeile, die Migration wirft, die Meldung nennt
/// Tabelle und Wert, und `events` und alle fünf Tabellen sind unverändert.
let private aborts (name: string) (table: string) (value: string) (spoil: SqliteConnection -> Fixture -> unit) =
    testCase name <| fun () ->
        use conn = freshDb ()
        let f = seedLegacy conn
        spoil conn f
        let eventsBefore = allEvents conn
        let legacyBefore = legacyContent conn
        let error = Expect.throwsC (fun () -> migrateBudgetEventsOn conn |> ignore) id
        Expect.stringContains error.Message table "nennt die Tabelle"
        Expect.stringContains error.Message value "nennt den Wert"
        Expect.equal (allEvents conn) eventsBefore "events unverändert"
        Expect.equal (legacyContent conn) legacyBefore "alle fünf Tabellen unverändert"

[<Tests>]
let abortTests =
    let month = "2026-10-01"
    testList "Budget-Migration: Abbruch" [
        aborts "Eine unbekannte Herkunft bricht ab" "budget_movements" "Quatsch" (fun conn f ->
            movementRow conn (Guid.NewGuid()) (Guid.NewGuid()) (time 13 0) "Quatsch" month f.X "Miete" 0L 1L)
        aborts "Eine unbekannte Pause-Art bricht ab" "target_pause_events" "Schlummern" (fun conn f ->
            pauseRow conn (string (Guid.NewGuid())) (time 13 0) month f.X "Miete" "Schlummern")
        aborts "Eine unbekannte Change-Art bricht ab" "target_changes" "Verdreht" (fun conn f ->
            changeRow conn (string (Guid.NewGuid())) (time 13 0) f.X "Miete" "Verdreht" null null null null)
        aborts "Ein unbekannter Status eines Bewegungs-Ergebnisses bricht ab" "budget_movement_results" "vielleicht" (fun conn f ->
            movementResult conn (string f.AssignedLine) (time 13 0) "vielleicht" null)
        aborts "Ein unbekannter Status eines Schritt-Ergebnisses bricht ab" "target_change_results" "vielleicht" (fun conn f ->
            changeResult conn (string (fst f.Changes)) (time 13 0) "vielleicht" null (box 1L))
        aborts "Eine Id außerhalb des Guid-Formats D bricht ab" "budget_movements" "ABC" (fun conn f ->
            let upper = Guid.NewGuid().ToString("D").ToUpperInvariant()
            movementRow conn (Guid.NewGuid()) (Guid.NewGuid()) (time 13 0) "Zuweisen" month f.X "Miete" 0L 1L
            conn.Execute(
                "INSERT INTO budget_movements VALUES ('ABC', @app, @at, 'Zuweisen', @month, @category, 'Miete', 0, 1)",
                {| app = upper; at = time 13 0; month = month; category = string f.Y |})
            |> ignore)
        aborts "Eine Pause-Id außerhalb des Guid-Formats D bricht ab" "target_pause_events" "pause-1" (fun conn f ->
            pauseRow conn "pause-1" (time 13 0) month f.X "Miete" "Pausiert")
        aborts "Ein Ergebnis ohne seine Bewegung bricht ab" "budget_movement_results" "verwaist" (fun conn _ ->
            movementResult conn "verwaist" (time 13 0) "written" null)
        aborts "Ein Ergebnis ohne seine Änderung bricht ab" "target_change_results" "verwaist" (fun conn _ ->
            changeResult conn "verwaist" (time 13 0) "written" null (box 1L))
        aborts "Eine Anwendung über mehrere Monate bricht ab" "budget_movements" "2026-11-01" (fun conn f ->
            movementRow conn (Guid.NewGuid()) f.Applied (time 8 0) "Targets anwenden" "2026-11-01" f.Z "Urlaub" 0L 1L)
        aborts "Eine Anwendung über mehrere Herkünfte bricht ab" "budget_movements" "Zuweisen" (fun conn f ->
            movementRow conn (Guid.NewGuid()) f.Applied (time 8 0) "Zuweisen" month f.Z "Urlaub" 0L 1L)
        aborts "Eine Verschiebung mit drei Zeilen bricht ab" "budget_movements" "3 Zeilen" (fun conn f ->
            movementRow conn (Guid.NewGuid()) f.Moved (time 10 0) "Verschieben" month f.Y "Strom" 0L 1L)
        aborts "Eine Verschiebung mit einer Zeile bricht ab" "budget_movements" "1 Zeilen" (fun conn f ->
            movementRow conn (Guid.NewGuid()) (Guid.NewGuid()) (time 13 0) "Verschieben" month f.Y "Strom" 1L 0L)
        aborts "Eine Verschiebung ohne sinkende Quelle bricht ab" "budget_movements" "keine eindeutige Quelle" (fun conn f ->
            let app = Guid.NewGuid()
            movementRow conn (Guid.NewGuid()) app (time 13 0) "Verschieben" month f.X "Miete" 0L 1L
            movementRow conn (Guid.NewGuid()) app (time 13 0) "Verschieben" month f.Y "Strom" 0L 1L)
        aborts "Ein Zuweisen mit zwei Zeilen bricht ab" "budget_movements" "2 Zeilen statt 1" (fun conn f ->
            movementRow conn (Guid.NewGuid()) f.Assigned (time 11 0) "Zuweisen" month f.Z "Urlaub" 0L 1L)
        aborts "Eine Anwendung mit zwei Zeitpunkten bricht ab" "budget_movements" (time 8 1) (fun conn f ->
            movementRow conn (Guid.NewGuid()) f.Applied (time 8 1) "Targets anwenden" month f.Z "Urlaub" 0L 1L)
        aborts "Eine Kategorie zweimal in einer Anwendung bricht ab" "budget_movements" "mehrfach" (fun conn f ->
            movementRow conn (Guid.NewGuid()) f.Applied (time 8 0) "Targets anwenden" month f.X "Miete" 0L 1L)
        aborts "Ein Monat, der kein Monatserster ist, bricht ab" "target_pause_events" "2026-10-15" (fun conn f ->
            pauseRow conn (string (Guid.NewGuid())) (time 13 0) "2026-10-15" f.X "Miete" "Pausiert")

        // Belegt die Transaktion: der Fehler kommt erst beim Einfügen einer späteren Zeile (UNIQUE auf
        // stream_id, stream_version), nach den Abbruchprüfungen und nach einer schon geschriebenen ersten Zeile.
        testCase "Scheitert das Schreiben bei einer späteren Zeile, bleiben events und alle fünf Tabellen unverändert" <| fun () ->
            use conn = freshDb ()
            seedLegacy conn |> ignore
            conn.Execute(
                "INSERT INTO events (event_id, context, stream_id, stream_version, occurred_at, actor_kind, event_type, schema_version, payload) \
                 VALUES (@id, 'budgeting', 'budgeting:month:2026-10', 2, '2026-10-01T00:00:00.0000000Z', 'UserInBb', 'TargetPaused', 1, '{}')",
                {| id = string (Guid.NewGuid()) |})
            |> ignore
            let eventsBefore = allEvents conn
            let legacyBefore = legacyContent conn
            let error = Expect.throwsC (fun () -> migrateBudgetEventsOn conn |> ignore) id
            Expect.stringContains error.Message "UNIQUE" "UNIQUE greift beim Schreiben"
            Expect.equal (allEvents conn) eventsBefore "events unverändert"
            Expect.equal (legacyContent conn) legacyBefore "alle fünf Tabellen unverändert"
    ]

// ============================================
// Kontextgrenze und erneut senden
// ============================================

[<Tests>]
let boundaryTests =
    testList "Budget-Ereignisse: Kontextgrenze" [
        testCase "Kein Budget-Leser liest Kassenbuch-Ereignisse oder Ereignisse eines fremden Kontexts" <| fun () ->
            use conn = new SqliteConnection("Data Source=:memory:")
            conn.Open()
            EventStore.createSchemaOn conn
            let k = Guid.NewGuid()
            ledgerEvent conn |> ignore
            let foreign (stream: string) (eventType: string) (payload: string) =
                conn.Execute(
                    "INSERT INTO events (event_id, context, stream_id, stream_version, occurred_at, actor_kind, event_type, schema_version, payload) \
                     VALUES (@id, 'fremd', @stream, 1, '2026-10-03T08:00:00.0000000Z', 'UserInBb', @type, 1, @payload)",
                    {| id = string (Guid.NewGuid()); stream = stream; ``type`` = eventType; payload = payload |})
                |> ignore
            let pause = BudgetEvent.TargetPaused { Month = "2026-10"; CategoryId = k; CategoryName = "A" }
            let moved =
                BudgetEvent.MoneyMoved
                    { Month = "2026-10"; From = { line "A" 2L 1L with CategoryId = k }; To = line "B" 0L 1L; Amount = 1L }
            let goal =
                BudgetEvent.YnabGoalChangedFromBb
                    { Month = "2026-10"; CategoryName = "A"; ChangeKind = TargetEdited; OldGoalTarget = None; NewGoalTarget = None; OldDate = None; NewDate = None }
            foreign (streamIdOf (MonthStream "2026-10")) (eventTypeName pause) (encodePayload pause)
            foreign "budgeting:month:2026-11" (eventTypeName moved) (encodePayload moved)
            foreign (streamIdOf (TargetStream k)) (eventTypeName goal) (encodePayload goal)
            foreign "fremd:sonstwas:1" "GanzAndersPassiert" "{}"
            Expect.equal (queryMovements conn { CategoryId = None; Month = None; Origin = None }) (Ok []) "allgemeine Historie"
            Expect.equal (queryMovements conn { CategoryId = Some k; Month = Some "2026-10-01"; Origin = None }) (Ok []) "Historie je Kategorie"
            Expect.equal (queryRecentCategories conn MoneyMoved 10) (Ok []) "Zuletzt verwendet"
            Expect.equal (queryPausedCategories conn "2026-10-01") (Ok Set.empty) "Pausen"
            Expect.equal (queryPauseEvents conn k "2026-10-01") (Ok []) "Pause-Ereignisse"
            Expect.equal (queryTargetChanges conn k "2026-10-01") (Ok []) "Target-Änderungen"
            Expect.equal (queryLastTargetChanges conn) (Ok Map.empty) "letzte Target-Änderung"
            Expect.equal (loadBudgetEventsOn conn WholeContext) (Ok []) "keine Budget-Ereignisse"

        testCase "Ein unbekannter Ereignistyp im Kontext budgeting wird beim Laden gemeldet, nicht übersprungen" <| fun () ->
            use conn = new SqliteConnection("Data Source=:memory:")
            conn.Open()
            EventStore.createSchemaOn conn
            recordPauseOn conn (DateTime(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc)) "2026-10-01" (Guid.NewGuid()) "A" Server.Budgeting.Domain.PauseKind.Paused |> ignore
            BudgetEventsFixture.appendUnknown conn (MonthStream "2026-10")
            match loadBudgetEventsOn conn WholeContext with
            | Error message -> Expect.stringContains message "TargetQuatschGemacht" "nennt den Typ"
            | Ok events -> failtest $"Fehler erwartet, kamen {events.Length} Ereignisse"
            Expect.isError (queryMovements conn { CategoryId = None; Month = Some "2026-10-01"; Origin = None }) "Historie des Monats meldet"

        // Die Leser über den ganzen Kontext laden nur die Typen, die sie auswerten (Index
        // `idx_events_context_type`). Ein fremder Typ im Kontext erreicht sie nicht, der Leser des Monats-Streams
        // lädt alle Typen und meldet ihn weiterhin.
        testCase "Ein unbekannter Ereignistyp stört Zuletzt verwendet, die allgemeine Historie und die letzte Target-Änderung nicht, der Monat meldet ihn" <| fun () ->
            use conn = new SqliteConnection("Data Source=:memory:")
            conn.Open()
            EventStore.createSchemaOn conn
            let k = Guid.NewGuid()
            BudgetEventsFixture.appendUnknown conn (MonthStream "2026-10")
            BudgetEventsFixture.appendUnknown conn (TargetStream k)
            Expect.equal (queryRecentCategories conn MoneyMoved 10) (Ok []) "Zuletzt verwendet"
            Expect.equal (queryMovements conn { CategoryId = None; Month = None; Origin = None }) (Ok []) "allgemeine Historie"
            Expect.equal (queryLastTargetChanges conn) (Ok Map.empty) "letzte Target-Änderung"
            Expect.isError (queryMovements conn { CategoryId = None; Month = Some "2026-10-01"; Origin = None }) "Historie des Monats meldet"
            Expect.isError (queryTargetChanges conn k "2026-10-01") "Target-Änderungen der Kategorie melden"
    ]

/// Ein YNAB-Port für "erneut senden": der Monat mit den Kategorien und ihrem Zugewiesen, jedes Senden
/// geht durch und wird gezählt.
let private retryPort (month: string) (categories: (Guid * decimal) list) (sent: ResizeArray<Guid * int64>) : Server.Budgeting.CommandHandlers.YnabPort =
    let category (id: Guid, assigned: decimal) : YnabMonthCategory =
        { Id = id
          Name = "K"
          GroupName = "G"
          Assigned = { Amount = assigned; Currency = "EUR" }
          Activity = { Amount = 0m; Currency = "EUR" }
          Available = { Amount = assigned; Currency = "EUR" }
          Underfunded = None
          Target = None
          Snoozed = false
          Hidden = false
          Deleted = false
          Internal = false }
    { FetchMonth =
        fun key ->
            async {
                return Ok { Month = key; ToBeBudgeted = { Amount = 0m; Currency = "EUR" }; Categories = categories |> List.map category }
            }
      SetAssigned =
        fun key id value ->
            async {
                if key = month then sent.Add((id, value))
                return Ok ()
            }
      SetTarget = fun _ _ -> async { return failwith "erneut senden schreibt kein Target" } }

[<Tests>]
let retryTests =
    // Auf der gemeinsamen Testdatenbank, in einem eigenen Monat: "erneut senden" liest über die Anwendung.
    testSequenced
    <| testList "Budget-Migration: erneut senden" [
        testCase "Erneut senden findet eine migrierte Anwendung über ihre alte application_id und eine neue über ihre event_id" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            let month = "2088-08-01"
            let x, y = Guid.NewGuid(), Guid.NewGuid()
            let oldApplication, oldLine = Guid.NewGuid(), Guid.NewGuid()
            do
                use conn = global.Persistence.getConnection ()
                conn.Open()
                use tx = conn.BeginTransaction()
                conn.Execute(legacyDdl.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ").Replace("CREATE TRIGGER ", "CREATE TRIGGER IF NOT EXISTS "), transaction = tx) |> ignore
                conn.Execute(
                    "INSERT INTO budget_movements VALUES (@id, @app, '2088-08-02T08:00:00.0000000Z', 'Targets anwenden', @month, @category, 'Miete', 0, 50000)",
                    {| id = string oldLine; app = string oldApplication; month = month; category = string x |},
                    tx)
                |> ignore
                conn.Execute(
                    "INSERT INTO budget_movement_results (movement_id, occurred_at, status, reason) VALUES (@id, '2088-08-02T08:00:00.0000000Z', 'failed', 'Rate-Limit')",
                    {| id = string oldLine |},
                    tx)
                |> ignore
                tx.Commit()
            Budgeting.Persistence.initializeBudgetingSchema ()
            let sent = ResizeArray<Guid * int64>()
            let port = retryPort month [ x, 0m; y, 10m ] sent
            let now = DateTime(2088, 8, 3, 9, 0, 0, DateTimeKind.Utc)
            match Server.Budgeting.CommandHandlers.retryFailed port now oldApplication |> run with
            | Ok report ->
                Expect.equal report.ApplicationId (string oldApplication) "dieselbe Gruppen-Id"
                Expect.equal (report.Lines |> List.map (fun l -> l.CategoryId, l.Outcome)) [ x, Written ] "erneut gesendet"
            | Error e -> failtest $"Bericht erwartet: {e}"
            Expect.equal (List.ofSeq sent) [ x, 50000L ] "das Ziel der migrierten Zeile"
            let failing = { port with SetAssigned = fun _ _ _ -> async { return Error (YnabError.NetworkError "weg") } }
            let request : AssignRequest = { CategoryId = y; Assigned = { Amount = 20m; Currency = "EUR" } }
            let fresh =
                match Server.Budgeting.CommandHandlers.assignMoney failing now month request |> run with
                | Ok (Ok report) -> report
                | other -> failtest $"Bericht erwartet: {other}"
            let decision =
                use conn = global.Persistence.getConnection ()
                conn.ExecuteScalar<string>(
                    "SELECT event_id FROM events WHERE stream_id = 'budgeting:month:2088-08' AND event_type = 'AssignmentChangedInBb'")
            Expect.equal fresh.ApplicationId decision "Gruppen-Id ist die event_id der Entscheidung"
            match Server.Budgeting.CommandHandlers.retryFailed port now (Guid.Parse fresh.ApplicationId) |> run with
            | Ok report -> Expect.equal (report.Lines |> List.map (fun l -> l.CategoryId, l.Outcome)) [ y, Written ] "neu gefunden"
            | Error e -> failtest $"Bericht erwartet: {e}"

        // In derselben Folge wie der Fall davor, der kurz frühere Tabellen in der gemeinsamen Datenbank anlegt.
        testCase "Der Start legt die früheren Tabellen nicht mehr an" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            use conn = global.Persistence.getConnection ()
            conn.Open()
            Expect.isEmpty (existingLegacyTables conn null) "keine der fünf Tabellen"
            Expect.equal (conn.ExecuteScalar<int64>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'events'")) 1L "events steht"
    ]

// ============================================
// Wächter: die Tabellennamen
// ============================================

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

/// Nur die Migration, die Probe und ihre Tests nennen die früheren Tabellen.
let private allowedFiles =
    set [ "src/Server/Budgeting/EventMigration.fs"
          "src/Server/Budgeting/EventMigrationProbe.fs"
          "src/Tests/BudgetEventMigrationTests.fs" ]

let private namePattern =
    Text.RegularExpressions.Regex(@"\b(budget_movements|budget_movement_results|target_pause_events|target_changes|target_change_results)\b")

/// Die Dateien unter `src/` (F#, ohne bin/obj), die einen der Tabellennamen tragen.
let private filesNamingTables (files: (string * string) list) : string list =
    files
    |> List.filter (fun (path, text) -> not (allowedFiles.Contains path) && namePattern.IsMatch text)
    |> List.map fst

[<Tests>]
let guardTests =
    testList "Budget-Migration: Tabellennamen" [
        test "Außer Migration, Probe und ihren Tests nennt keine Datei in src/ die fünf früheren Tabellen" {
            let files =
                Directory.EnumerateFiles(Path.Combine(repoRoot, "src"), "*.fs", SearchOption.AllDirectories)
                |> Seq.map (fun p -> Path.GetRelativePath(repoRoot, p).Replace('\\', '/'), p)
                |> Seq.filter (fun (rel, _) -> not (rel.Contains "/bin/" || rel.Contains "/obj/"))
                |> Seq.map (fun (rel, p) -> rel, File.ReadAllText p)
                |> Seq.toList
            Expect.isGreaterThan files.Length 100 "der Wächter sieht die Quellen"
            Expect.isEmpty (filesNamingTables files) "Dateien mit den früheren Tabellennamen"
        }
        test "Gegenbeweis: eine andere Datei mit einem Tabellennamen wird gemeldet, eine erlaubte nicht" {
            let files =
                [ "src/Server/Budgeting/Persistence.fs", "SELECT * FROM target_change_results"
                  "src/Server/Budgeting/EventMigration.fs", "DROP TABLE budget_movements"
                  "src/Server/Sonst.fs", "underfunded_comparisons" ]
            Expect.equal (filesNamingTables files) [ "src/Server/Budgeting/Persistence.fs" ] "nur die nicht erlaubte"
        }
    ]

// ============================================
// Probe gegen eine Kopie der Produktions-Datenbank (opt-in)
// ============================================

/// Opt-in: nur mit `BUDGET_MIGRATION_PROBE_DB` (Pfad einer Datenbank-Datei). Die Datei selbst wird nie
/// geöffnet; die Probe läuft auf einer Kopie im Temp-Verzeichnis, die danach gelöscht wird. Ins Protokoll
/// gehen nur Zahlen und Wahrheitswerte.
[<Tests>]
let probeTests =
    testList "Budget-Migration: Probe gegen eine Kopie (requires .env)" [
        testCase "Die Migration einer Kopie behält jeden Leser und entfernt die Tabellen" <| fun () ->
            let source = Environment.GetEnvironmentVariable "BUDGET_MIGRATION_PROBE_DB"
            if String.IsNullOrWhiteSpace source then
                Tests.skiptest "BUDGET_MIGRATION_PROBE_DB nicht gesetzt - Probe übersprungen"
            if not (File.Exists source) then
                failtest "BUDGET_MIGRATION_PROBE_DB zeigt auf keine Datei"
            let copy = Path.Combine(Path.GetTempPath(), $"bb-budget-probe-{Guid.NewGuid():N}.db")
            let sidecars = [ "-wal"; "-shm" ]
            try
                File.Copy(source, copy)
                for suffix in sidecars do
                    if File.Exists(source + suffix) then File.Copy(source + suffix, copy + suffix)
                let probe =
                    use conn = new SqliteConnection($"Data Source={copy};Pooling=False")
                    conn.Open()
                    probeBudgetEventMigrationOn conn
                let flags =
                    [ "StreamVersionsGapless", probe.StreamVersionsGapless
                      "OccurredAtUnchanged", probe.OccurredAtUnchanged
                      "HistoryUnchanged", probe.HistoryUnchanged
                      "CategoryHistoriesUnchanged", probe.CategoryHistoriesUnchanged
                      "RecentCategoriesUnchanged", probe.RecentCategoriesUnchanged
                      "PausesUnchanged", probe.PausesUnchanged
                      "LastTargetChangesUnchanged", probe.LastTargetChangesUnchanged
                      "ApplicationsUnchanged", probe.ApplicationsUnchanged
                      "LegacyTablesGone", probe.LegacyTablesGone ]
                printfn "Budget-Migrationsprobe:"
                for table, n in probe.RowsBefore do
                    printfn "  vorher %s: %d" table n
                for eventType, n in probe.EventsAfter do
                    printfn "  nachher %s: %d" eventType n
                for name, value in flags do
                    printfn "  %s: %b" name value
                for name, value in flags do
                    Expect.isTrue value name
            finally
                for path in copy :: (sidecars |> List.map (fun s -> copy + s)) do
                    if File.Exists path then File.Delete path
    ]
