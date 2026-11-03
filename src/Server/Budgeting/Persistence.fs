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
        | other -> failwith $"Unbekannter Ergebnis-Status der Budget-Bewegung: {other}"

/// Legt die Tabellen an (`CREATE ... IF NOT EXISTS`, wiederholbar). Die Trigger machen beide
/// Tabellen append-only. Läuft beim Start hinter `Ledger.Persistence.initializeLedgerSchema`.
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
        """
    )
    |> ignore

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
        return
            List.foldBack
                (fun row states ->
                    match toState row, states with
                    | Ok state, Ok rest -> Ok (state :: rest)
                    | Error e, _
                    | _, Error e -> Error e)
                rows
                (Ok [])
    }

/// Welche Bewegungen die Historie liest (budgeting-dyn8g). `None` heißt: ohne diese Einschränkung.
/// budgeting-h7q4v liest so alle Kategorien und filtert nach Herkunft.
type MovementFilter = {
    CategoryId: Guid option
    /// "YYYY-MM-01".
    Month: string option
    Origin: MovementOrigin option
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

let private euro (milliunits: int64) : Shared.Domain.Money =
    { Amount = decimal milliunits / 1000m; Currency = "EUR" }

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
              OldAssigned = euro r.old_assigned
              NewAssigned = euro r.new_assigned
              Counterpart = Option.ofObj r.counterpart_name
              Latest = statusFromText r.status r.reason })
    List.foldBack
        (fun row movements ->
            match toMovement row, movements with
            | Ok movement, Ok rest -> Ok (movement :: rest)
            | Error e, _
            | _, Error e -> Error e)
        rows
        (Ok [])

/// Die Historie liest nur die Datenbank, kein YNAB.
let loadMovements (filter: MovementFilter) : Async<Result<HistoryMovement list, string>> =
    async {
        use conn = Core.getConnection ()
        return queryMovements conn filter
    }
