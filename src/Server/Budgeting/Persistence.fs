/// Die Budget-Bewegungen (Context `budgeting`, budgeting-h2zfn, ADR 0065): was "Targets
/// anwenden" je Kategorie zugewiesen hat, und was beim Rückschreiben nach YNAB daraus wurde.
///
/// Zwei Tabellen, beide nach dem Schreiben unveränderlich (SQLite-Trigger weisen UPDATE und
/// DELETE ab). `budget_movements` hält je Kategorie einer Anwendung eine Zeile (Zeitpunkt,
/// Herkunft, Monat, Kategorie, altes und neues Zugewiesen in Milliunits).
/// `budget_movement_results` ist angehängt: jedes Ergebnis ist eine neue Zeile, der Stand
/// einer Bewegung ist ihr jüngstes Ergebnis. Getrennt vom Kassenbuch-Ereignis-Log (ADR 0013).
module Budgeting.Persistence

open System
open System.Globalization
open Microsoft.Data.Sqlite
open Dapper
open Shared.Budgeting
open Server.Budgeting.Domain

module Core = global.Persistence

/// Eine festgehaltene Budget-Bewegung. Beträge in Milliunits.
type Movement = {
    Id: Guid
    ApplicationId: Guid
    OccurredAt: DateTime
    Origin: MovementOrigin
    /// "YYYY-MM-01".
    Month: string
    CategoryId: Guid
    CategoryName: string
    OldAssigned: int64
    NewAssigned: int64
}

/// Eine Bewegung mit ihrem jüngsten Ergebnis (`None`, solange keines angehängt ist).
type MovementState = {
    Movement: Movement
    Latest: LineOutcome option
}

[<CLIMutable>]
type private MovementRow = {
    id: string
    application_id: string
    occurred_at: string
    origin: string
    month: string
    category_id: string
    category_name: string
    old_assigned: int64
    new_assigned: int64
    status: string
    reason: string
}

let private dateToText (d: DateTime) = d.ToString("o", CultureInfo.InvariantCulture)

let private dateFromText (s: string) =
    DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)

let private statusToText (outcome: LineOutcome) : string * string =
    match outcome with
    | Written -> "written", null
    | Failed reason -> "failed", reason
    | ChangedInYnab -> "changed-in-ynab", null

let private statusFromText (status: string) (reason: string) : LineOutcome option =
    if isNull status then None
    else
        match status with
        | "written" -> Some Written
        | "failed" -> Some (Failed (if isNull reason then "" else reason))
        | "changed-in-ynab" -> Some ChangedInYnab
        | other -> failwith $"Unbekannter Ergebnis-Status: {other}"

/// Ergänzt die Spalten aus budgeting-xp46a an einer Datenbank aus budgeting-k8wq3: den Rhythmus beim
/// Anlegen und beim Wechsel der Art (`target_changes.new_frequency`) und den Schritt je Ergebnis
/// (`target_change_results.step`, bei alten Zeilen leer, gemeint ist Schritt 1). Eine neue Datenbank hat
/// sie schon aus `CREATE TABLE`. SQLite kennt kein "ADD COLUMN IF NOT EXISTS": ob die Spalte fehlt, sagt
/// `pragma_table_info` (budgeting-g2ty9, X11). Nimmt die Verbindung als Parameter, damit ein Test sie an
/// einer eigenen Datenbank prüft.
let migrateTargetChangeColumns (conn: SqliteConnection) =
    for table, column, alter in
        [ "target_changes", "new_frequency", "ALTER TABLE target_changes ADD COLUMN new_frequency TEXT"
          "target_change_results", "step", "ALTER TABLE target_change_results ADD COLUMN step INTEGER" ] do
        let present =
            conn.ExecuteScalar<int64>(
                "SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name = @column",
                {| table = table; column = column |}
            )
        if present = 0L then
            conn.Execute(alter) |> ignore

/// Legt die Tabellen an (`CREATE ... IF NOT EXISTS`, wiederholbar). Die Trigger machen alle
/// Tabellen append-only, auch `target_pause_events` (budgeting-t6p8z, ADR 0079) und die Target-Änderungen
/// (budgeting-k8wq3, ADR 0080). Läuft beim Start hinter `Ledger.Persistence.initializeLedgerSchema`.
let initializeBudgetingSchema () =
    use conn = Core.getConnection ()
    conn.Execute(
        """
        CREATE TABLE IF NOT EXISTS budget_movements (
            id TEXT PRIMARY KEY,
            application_id TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            origin TEXT NOT NULL,
            month TEXT NOT NULL,
            category_id TEXT NOT NULL,
            category_name TEXT NOT NULL,
            old_assigned INTEGER NOT NULL,
            new_assigned INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_budget_movements_application ON budget_movements (application_id);
        CREATE TABLE IF NOT EXISTS budget_movement_results (
            sequence INTEGER PRIMARY KEY AUTOINCREMENT,
            movement_id TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            status TEXT NOT NULL,
            reason TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_budget_movement_results_movement ON budget_movement_results (movement_id);
        CREATE TRIGGER IF NOT EXISTS budget_movements_no_update BEFORE UPDATE ON budget_movements
        BEGIN SELECT RAISE(ABORT, 'budget_movements ist unveränderlich'); END;
        CREATE TRIGGER IF NOT EXISTS budget_movements_no_delete BEFORE DELETE ON budget_movements
        BEGIN SELECT RAISE(ABORT, 'budget_movements ist unveränderlich'); END;
        CREATE TRIGGER IF NOT EXISTS budget_movement_results_no_update BEFORE UPDATE ON budget_movement_results
        BEGIN SELECT RAISE(ABORT, 'budget_movement_results ist append-only'); END;
        CREATE TRIGGER IF NOT EXISTS budget_movement_results_no_delete BEFORE DELETE ON budget_movement_results
        BEGIN SELECT RAISE(ABORT, 'budget_movement_results ist append-only'); END;
        CREATE TABLE IF NOT EXISTS target_pause_events (
            id TEXT PRIMARY KEY,
            occurred_at TEXT NOT NULL,
            month TEXT NOT NULL,
            category_id TEXT NOT NULL,
            category_name TEXT NOT NULL,
            kind TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_target_pause_events_month ON target_pause_events (month, category_id);
        CREATE TRIGGER IF NOT EXISTS target_pause_events_no_update BEFORE UPDATE ON target_pause_events
        BEGIN SELECT RAISE(ABORT, 'target_pause_events ist append-only'); END;
        CREATE TRIGGER IF NOT EXISTS target_pause_events_no_delete BEFORE DELETE ON target_pause_events
        BEGIN SELECT RAISE(ABORT, 'target_pause_events ist append-only'); END;
        CREATE TABLE IF NOT EXISTS target_changes (
            id TEXT PRIMARY KEY,
            occurred_at TEXT NOT NULL,
            month TEXT NOT NULL,
            category_id TEXT NOT NULL,
            category_name TEXT NOT NULL,
            change_kind TEXT NOT NULL,
            old_goal_target INTEGER,
            new_goal_target INTEGER,
            old_date TEXT,
            new_date TEXT,
            new_frequency TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_target_changes_category ON target_changes (category_id, month);
        CREATE TABLE IF NOT EXISTS target_change_results (
            sequence INTEGER PRIMARY KEY AUTOINCREMENT,
            change_id TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            status TEXT NOT NULL,
            reason TEXT,
            step INTEGER
        );
        CREATE INDEX IF NOT EXISTS idx_target_change_results_change ON target_change_results (change_id);
        CREATE TRIGGER IF NOT EXISTS target_changes_no_update BEFORE UPDATE ON target_changes
        BEGIN SELECT RAISE(ABORT, 'target_changes ist unveränderlich'); END;
        CREATE TRIGGER IF NOT EXISTS target_changes_no_delete BEFORE DELETE ON target_changes
        BEGIN SELECT RAISE(ABORT, 'target_changes ist unveränderlich'); END;
        CREATE TRIGGER IF NOT EXISTS target_change_results_no_update BEFORE UPDATE ON target_change_results
        BEGIN SELECT RAISE(ABORT, 'target_change_results ist append-only'); END;
        CREATE TRIGGER IF NOT EXISTS target_change_results_no_delete BEFORE DELETE ON target_change_results
        BEGIN SELECT RAISE(ABORT, 'target_change_results ist append-only'); END;
        """
    )
    |> ignore
    migrateTargetChangeColumns conn

/// Hält alle Bewegungen einer Anwendung in einer Transaktion fest. Entweder sind alle
/// da oder keine; erst danach darf der erste Push nach YNAB gehen (ADR 0050, Punkt 1).
let appendMovements (movements: Movement list) : Async<unit> =
    async {
        use conn = Core.getConnection ()
        conn.Open()
        use tx = conn.BeginTransaction()
        for m in movements do
            conn.Execute(
                "INSERT INTO budget_movements (id, application_id, occurred_at, origin, month, category_id, category_name, old_assigned, new_assigned) \
                 VALUES (@id, @application_id, @occurred_at, @origin, @month, @category_id, @category_name, @old_assigned, @new_assigned)",
                {| id = string m.Id
                   application_id = string m.ApplicationId
                   occurred_at = dateToText m.OccurredAt
                   origin = originText m.Origin
                   month = m.Month
                   category_id = string m.CategoryId
                   category_name = m.CategoryName
                   old_assigned = m.OldAssigned
                   new_assigned = m.NewAssigned |},
                tx
            )
            |> ignore
        tx.Commit()
    }

/// Hängt ein Ergebnis an eine Bewegung an. Frühere Ergebnisse bleiben stehen.
let appendResult (movementId: Guid) (at: DateTime) (outcome: LineOutcome) : Async<unit> =
    async {
        let status, reason = statusToText outcome
        use conn = Core.getConnection ()
        conn.Execute(
            "INSERT INTO budget_movement_results (movement_id, occurred_at, status, reason) VALUES (@movement_id, @occurred_at, @status, @reason)",
            {| movement_id = string movementId; occurred_at = dateToText at; status = status; reason = reason |}
        )
        |> ignore
    }

/// Die Kategorien der jüngsten Bewegungen einer Herkunft, neueste zuerst, ohne Dubletten.
let queryRecentCategories (conn: SqliteConnection) (origin: MovementOrigin) (limit: int) : Guid list =
    conn.Query<string>(
        "SELECT category_id FROM budget_movements WHERE origin = @origin \
         GROUP BY category_id ORDER BY MAX(rowid) DESC LIMIT @limit",
        {| origin = originText origin; limit = limit |}
    )
    |> Seq.map Guid.Parse
    |> Seq.toList

/// "Zuletzt verwendet" im Picker: nur Verschiebungen zwischen zwei Kategorien.
let loadRecentMoveCategories (limit: int) : Async<Guid list> =
    async {
        use conn = Core.getConnection ()
        return queryRecentCategories conn MoneyMoved limit
    }

/// Alle Zeilen oder der erste Fehler, in der Reihenfolge der Eingabe.
let private collectRows (rows: Result<'T, string> list) : Result<'T list, string> =
    List.foldBack
        (fun row acc ->
            match row, acc with
            | Ok value, Ok values -> Ok (value :: values)
            | Error message, _ -> Error message
            | _, Error message -> Error message)
        rows
        (Ok [])

/// Alle Bewegungen einer Anwendung (in der Reihenfolge des Festhaltens) mit ihrem jüngsten
/// Ergebnis. `Error` nennt eine Zeile mit unbekannter Herkunft.
let loadApplication (applicationId: Guid) : Async<Result<MovementState list, string>> =
    async {
        use conn = Core.getConnection ()
        let rows =
            conn.Query<MovementRow>(
                "SELECT m.id, m.application_id, m.occurred_at, m.origin, m.month, m.category_id, m.category_name, \
                        m.old_assigned, m.new_assigned, r.status, r.reason \
                 FROM budget_movements m \
                 LEFT JOIN budget_movement_results r \
                   ON r.sequence = (SELECT MAX(sequence) FROM budget_movement_results WHERE movement_id = m.id) \
                 WHERE m.application_id = @application_id \
                 ORDER BY m.rowid",
                {| application_id = string applicationId |}
            )
            |> Seq.toList
        let toState (r: MovementRow) : Result<MovementState, string> =
            originOfText r.origin
            |> Result.map (fun origin ->
                { Movement =
                    { Id = Guid.Parse r.id
                      ApplicationId = Guid.Parse r.application_id
                      OccurredAt = dateFromText r.occurred_at
                      Origin = origin
                      Month = r.month
                      CategoryId = Guid.Parse r.category_id
                      CategoryName = r.category_name
                      OldAssigned = r.old_assigned
                      NewAssigned = r.new_assigned }
                  Latest = statusFromText r.status r.reason })
        return rows |> List.map toState |> collectRows
    }

[<CLIMutable>]
type private HistoryRow = {
    id: string
    application_id: string
    occurred_at: string
    origin: string
    month: string
    category_id: string
    category_name: string
    old_assigned: int64
    new_assigned: int64
    status: string
    reason: string
    counterpart_name: string
}

/// Die Bewegungen zum Filter, neueste zuerst, je mit dem jüngsten Ergebnis und bei einer
/// Verschiebung mit der Kategorie der Gegenseite (dieselbe Gruppen-Id, andere Bewegung).
/// `Error` nennt eine Zeile mit unbekannter Herkunft.
let queryMovements (conn: SqliteConnection) (filter: MovementFilter) : Result<HistoryMovement list, string> =
    let orNull (value: 'a option) (text: 'a -> string) : string =
        match value with
        | Some v -> text v
        | None -> null
    let rows =
        conn.Query<HistoryRow>(
            "SELECT m.id, m.application_id, m.occurred_at, m.origin, m.month, m.category_id, m.category_name, \
                    m.old_assigned, m.new_assigned, r.status, r.reason, c.category_name AS counterpart_name \
             FROM budget_movements m \
             LEFT JOIN budget_movement_results r \
               ON r.sequence = (SELECT MAX(sequence) FROM budget_movement_results WHERE movement_id = m.id) \
             LEFT JOIN budget_movements c \
               ON m.origin = @moved AND c.application_id = m.application_id AND c.id <> m.id \
             WHERE (@category_id IS NULL OR m.category_id = @category_id) \
               AND (@month IS NULL OR m.month = @month) \
               AND (@origin IS NULL OR m.origin = @origin) \
             ORDER BY m.occurred_at DESC, m.rowid DESC",
            {| moved = originText MoneyMoved
               category_id = orNull filter.CategoryId string
               month = orNull filter.Month id
               origin = orNull filter.Origin originText |}
        )
        |> Seq.toList
    let toMovement (r: HistoryRow) : Result<HistoryMovement, string> =
        originOfText r.origin
        |> Result.map (fun origin ->
            { MovementId = Guid.Parse r.id
              ApplicationId = Guid.Parse r.application_id
              OccurredAt = dateFromText r.occurred_at
              Origin = origin
              Month = r.month
              CategoryId = Guid.Parse r.category_id
              CategoryName = r.category_name
              OldAssigned = euroOfMilliunits r.old_assigned
              NewAssigned = euroOfMilliunits r.new_assigned
              Counterpart = Option.ofObj r.counterpart_name
              Latest = statusFromText r.status r.reason })
    rows |> List.map toMovement |> collectRows

/// Die Historie liest nur die Datenbank, kein YNAB.
let loadMovements (filter: MovementFilter) : Async<Result<HistoryMovement list, string>> =
    async {
        use conn = Core.getConnection ()
        return queryMovements conn filter
    }

// ============================================
// Target pausieren (budgeting-t6p8z, ADR 0079)
// ============================================

/// Ein festgehaltenes Pause-Ereignis. Der Stand je (Monat, Kategorie) ist das jüngste.
type PauseEvent = {
    Id: Guid
    OccurredAt: DateTime
    /// "YYYY-MM-01".
    Month: string
    CategoryId: Guid
    CategoryName: string
    Kind: PauseKind
}

/// Hängt ein Pause-Ereignis an. Frühere bleiben stehen.
let appendPauseEvent (event: PauseEvent) : Async<unit> =
    async {
        use conn = Core.getConnection ()
        conn.Execute(
            "INSERT INTO target_pause_events (id, occurred_at, month, category_id, category_name, kind) \
             VALUES (@id, @occurred_at, @month, @category_id, @category_name, @kind)",
            {| id = string event.Id
               occurred_at = dateToText event.OccurredAt
               month = event.Month
               category_id = string event.CategoryId
               category_name = event.CategoryName
               kind = pauseKindText event.Kind |}
        )
        |> ignore
    }

[<CLIMutable>]
type private PauseEventRow = {
    id: string
    occurred_at: string
    month: string
    category_id: string
    category_name: string
    kind: string
}

/// Die Kategorien, deren Target in BB für den Monat pausiert ist: je Kategorie zählt das jüngste
/// Ereignis nach `rowid`, wie in der Historie (budgeting-r3wq8). Eine Abfrage für alle Kategorien des
/// Monats. Ein unbekannter Wert in `kind` wird gemeldet, nicht auf einen Standard gelegt.
let queryPausedCategories (conn: SqliteConnection) (month: string) : Result<Set<Guid>, string> =
    conn.Query<PauseEventRow>(
        "SELECT e.id, e.occurred_at, e.month, e.category_id, e.category_name, e.kind \
         FROM target_pause_events e \
         WHERE e.month = @month \
           AND e.rowid = (SELECT MAX(rowid) FROM target_pause_events l \
                          WHERE l.month = e.month AND l.category_id = e.category_id)",
        {| month = month |}
    )
    |> Seq.toList
    |> List.map (fun r -> pauseKindOfText r.kind |> Result.map (fun kind -> kind, Guid.Parse r.category_id))
    |> collectRows
    |> Result.map (List.choose (fun (kind, id) -> if kind = PauseKind.Paused then Some id else None) >> Set.ofList)

/// Die Meldung, wenn die Pausen nicht lesbar sind. Die Ursache bleibt im Server-Log.
[<Literal>]
let PausesUnreadableText = "Pausen nicht lesbar"

/// Die Meldung, wenn die Pause-Ereignisse für die Historie nicht lesbar sind.
[<Literal>]
let PauseEventsUnreadableText = "Pause-Ereignisse nicht lesbar"

/// Die Ursache eines Lesefehlers bleibt im Server-Log, der Aufrufer bekommt nur den Text der Meldung.
let logReadFailure (what: string) (cause: string) : unit =
    eprintfn "%s gescheitert: %s" what cause

/// Die BB-Pausen des Monats, für `toBudgetMonth`. Ein Lesefehler oder eine unbekannte Art wird zu
/// `Error PausesUnreadableText` (budgeting-r3wq8).
let loadPausedCategories (month: string) : Async<Result<Set<Guid>, string>> =
    async {
        try
            use conn = Core.getConnection ()
            match queryPausedCategories conn month with
            | Ok paused -> return Ok paused
            | Error message ->
                logReadFailure "Pausen lesen" message
                return Error PausesUnreadableText
        with ex ->
            logReadFailure "Pausen lesen" ex.Message
            return Error PausesUnreadableText
    }

/// Die Pause-Ereignisse einer Kategorie im Monat für die Historie, neueste zuerst nach `rowid`, wie der
/// Stand (budgeting-r3wq8). Eine unbekannte Art wird gemeldet.
let queryPauseEvents (conn: SqliteConnection) (categoryId: Guid) (month: string) : Result<HistoryEntry list, string> =
    conn.Query<PauseEventRow>(
        "SELECT id, occurred_at, month, category_id, category_name, kind FROM target_pause_events \
         WHERE category_id = @category_id AND month = @month \
         ORDER BY rowid DESC",
        {| category_id = string categoryId; month = month |}
    )
    |> Seq.toList
    |> List.map (fun r ->
        let pause =
            { EventId = Guid.Parse r.id
              OccurredAt = dateFromText r.occurred_at
              Month = r.month
              CategoryId = Guid.Parse r.category_id
              CategoryName = r.category_name }
        pauseKindOfText r.kind
        |> Result.map (fun kind ->
            match kind with
            | PauseKind.Paused -> TargetPaused pause
            | PauseKind.PauseLifted -> HistoryEntry.PauseLifted pause))
    |> collectRows

/// Die Pause-Ereignisse für die Historie. Liest nur die Datenbank, kein YNAB. Ein Lesefehler kommt als
/// `Error` mit Text.
let loadPauseEvents (categoryId: Guid) (month: string) : Async<Result<HistoryEntry list, string>> =
    async {
        try
            use conn = Core.getConnection ()
            match queryPauseEvents conn categoryId month with
            | Ok events -> return Ok events
            | Error message ->
                logReadFailure "Pause-Ereignisse lesen" message
                return Error PauseEventsUnreadableText
        with ex ->
            logReadFailure "Pause-Ereignisse lesen" ex.Message
            return Error PauseEventsUnreadableText
    }

// ============================================
// Target ändern und entfernen (budgeting-k8wq3, ADR 0080)
// ============================================

/// Eine festgehaltene Target-Änderung. Beträge in Milliunits, Datum "YYYY-MM-DD". `None`, wo kein
/// Target oder kein Datum steht.
type TargetChange = {
    Id: Guid
    OccurredAt: DateTime
    /// Der laufende Monat, "YYYY-MM-01".
    Month: string
    CategoryId: Guid
    CategoryName: string
    Kind: TargetChangeKind
    OldGoalTarget: int64 option
    NewGoalTarget: int64 option
    OldDate: string option
    NewDate: string option
}

let private orDbNull (value: 'a option) : obj =
    match value with
    | Some v -> box v
    | None -> null

/// Hält Target-Änderungen in einer Transaktion fest (ADR 0080 Punkt 4). Erst danach darf der PATCH
/// hinausgehen. Eine Änderung mit zwei Schritten ist eine Zeile, die Ergebnisse tragen den Schritt.
let appendTargetChanges (changes: TargetChange list) : Async<unit> =
    async {
        use conn = Core.getConnection ()
        conn.Open()
        use tx = conn.BeginTransaction()
        for c in changes do
            conn.Execute(
                "INSERT INTO target_changes (id, occurred_at, month, category_id, category_name, change_kind, \
                    old_goal_target, new_goal_target, old_date, new_date, new_frequency) \
                 VALUES (@id, @occurred_at, @month, @category_id, @category_name, @change_kind, \
                    @old_goal_target, @new_goal_target, @old_date, @new_date, @new_frequency)",
                {| id = string c.Id
                   occurred_at = dateToText c.OccurredAt
                   month = c.Month
                   category_id = string c.CategoryId
                   category_name = c.CategoryName
                   change_kind = fst (targetChangeKindText c.Kind)
                   new_frequency = orDbNull (snd (targetChangeKindText c.Kind))
                   old_goal_target = orDbNull c.OldGoalTarget
                   new_goal_target = orDbNull c.NewGoalTarget
                   old_date = orDbNull c.OldDate
                   new_date = orDbNull c.NewDate |},
                tx
            )
            |> ignore
        tx.Commit()
    }

/// Hängt das Ergebnis eines Schritts (1 oder 2) an eine Target-Änderung an. Frühere Ergebnisse bleiben
/// stehen, der Stand der Änderung ist das jüngste.
let appendTargetChangeStepResult (changeId: Guid) (step: int) (at: DateTime) (outcome: LineOutcome) : Async<unit> =
    async {
        let status, reason = statusToText outcome
        use conn = Core.getConnection ()
        conn.Execute(
            "INSERT INTO target_change_results (change_id, occurred_at, status, reason, step) \
             VALUES (@change_id, @occurred_at, @status, @reason, @step)",
            {| change_id = string changeId; occurred_at = dateToText at; status = status; reason = reason; step = step |}
        )
        |> ignore
    }

[<CLIMutable>]
type private TargetChangeRow = {
    id: string
    occurred_at: string
    month: string
    category_id: string
    category_name: string
    change_kind: string
    old_goal_target: int64 option
    new_goal_target: int64 option
    old_date: string option
    new_date: string option
    new_frequency: string option
    status: string
    reason: string
    date_status: string
    date_reason: string
}

/// Die Target-Änderungen einer Kategorie im Monat für die Historie, neueste zuerst nach `rowid` wie die
/// Pausen, je mit dem jüngsten Ergebnis von Schritt 1 und von Schritt 2 (budgeting-g2ty9, X7). Ein Ergebnis
/// ohne Schritt stammt aus budgeting-k8wq3 und zählt als Schritt 1. Eine unbekannte Art wird gemeldet,
/// nicht auf einen Standard gelegt.
let queryTargetChanges (conn: SqliteConnection) (categoryId: Guid) (month: string) : Result<HistoryTargetChange list, string> =
    conn.Query<TargetChangeRow>(
        "SELECT c.id, c.occurred_at, c.month, c.category_id, c.category_name, c.change_kind, \
                c.old_goal_target, c.new_goal_target, c.old_date, c.new_date, c.new_frequency, \
                r1.status, r1.reason, r2.status AS date_status, r2.reason AS date_reason \
         FROM target_changes c \
         LEFT JOIN target_change_results r1 \
           ON r1.sequence = (SELECT MAX(sequence) FROM target_change_results \
                             WHERE change_id = c.id AND COALESCE(step, 1) = 1) \
         LEFT JOIN target_change_results r2 \
           ON r2.sequence = (SELECT MAX(sequence) FROM target_change_results \
                             WHERE change_id = c.id AND step = 2) \
         WHERE c.category_id = @category_id AND c.month = @month \
         ORDER BY c.rowid DESC",
        {| category_id = string categoryId; month = month |}
    )
    |> Seq.toList
    |> List.map (fun r ->
        targetChangeKindOfText r.change_kind r.new_frequency
        |> Result.map (fun kind ->
            { ChangeId = Guid.Parse r.id
              OccurredAt = dateFromText r.occurred_at
              Month = r.month
              CategoryId = Guid.Parse r.category_id
              CategoryName = r.category_name
              ChangeKind = kind
              GoalBefore = r.old_goal_target |> Option.map euroOfMilliunits
              GoalAfter = r.new_goal_target |> Option.map euroOfMilliunits
              DateBefore = r.old_date
              DateAfter = r.new_date
              Latest = statusFromText r.status r.reason
              DateStep = statusFromText r.date_status r.date_reason }))
    |> collectRows

/// Die Meldung, wenn die Target-Änderungen für die Historie nicht lesbar sind.
[<Literal>]
let TargetChangesUnreadableText = "Target-Änderungen nicht lesbar"

/// Die Target-Änderungen für die Historie. Liest nur die Datenbank, kein YNAB. Ein Lesefehler oder eine
/// unbekannte Art kommt als `Error` mit Text, die Ursache bleibt im Server-Log (wie `loadPauseEvents`).
let loadTargetChanges (categoryId: Guid) (month: string) : Async<Result<HistoryTargetChange list, string>> =
    async {
        try
            use conn = Core.getConnection ()
            match queryTargetChanges conn categoryId month with
            | Ok changes -> return Ok changes
            | Error message ->
                logReadFailure "Target-Änderungen lesen" message
                return Error TargetChangesUnreadableText
        with ex ->
            logReadFailure "Target-Änderungen lesen" ex.Message
            return Error TargetChangesUnreadableText
    }
