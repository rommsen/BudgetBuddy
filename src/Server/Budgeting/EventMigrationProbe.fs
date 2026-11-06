/// Probe der Budget-Migration (budgeting-hyn77, ADR 0084 Punkt 4) für eine **Kopie** der
/// Produktions-Datenbank: liest jeden Budget-Leser vorher aus den früheren Tabellen, führt die Migration
/// wirklich aus und liest danach aus `events`. Das Ergebnis enthält nur Zahlen und Wahrheitswerte, keine
/// Beträge, Namen oder Ids. Die Leser für den Stand vorher sind die Abfragen, mit denen BB die früheren
/// Tabellen bis budgeting-hyn77 las.
module Budgeting.EventMigrationProbe

open System
open System.Globalization
open Microsoft.Data.Sqlite
open Dapper
open Shared.Budgeting
open Server.Budgeting.Domain
open Budgeting.EventMigration
open Budgeting.Persistence

type private StoredEventRow = EventStore.StoredEventRow

// ============================================
// Die früheren Leser
// ============================================

[<CLIMutable>]
type private LegacyHistoryRow = {
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

[<CLIMutable>]
type private LegacyPauseEventRow = {
    id: string
    occurred_at: string
    month: string
    category_id: string
    category_name: string
    kind: string
}

[<CLIMutable>]
type private LegacyChangeHistoryRow = {
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

[<CLIMutable>]
type private LegacyLastChangeRow = {
    row_order: int64
    occurred_at: string
    category_id: string
    change_kind: string
    new_frequency: string option
}

let private dateFromText (s: string) =
    DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)

let private collect (rows: Result<'T, string> list) : Result<'T list, string> =
    List.foldBack
        (fun row acc ->
            match row, acc with
            | Ok value, Ok values -> Ok (value :: values)
            | Error message, _ -> Error message
            | _, Error message -> Error message)
        rows
        (Ok [])

let private statusFromText (status: string) (reason: string) : LineOutcome option =
    match status with
    | null -> None
    | "written" -> Some Written
    | "failed" -> Some (Failed (if isNull reason then "" else reason))
    | "changed-in-ynab" -> Some ChangedInYnab
    | other -> failwith $"Unbekannter Ergebnis-Status: {other}"

let private legacyMovements (conn: SqliteConnection) (filter: MovementFilter) : Result<HistoryMovement list, string> =
    let orNull (value: 'a option) (text: 'a -> string) : string =
        match value with
        | Some v -> text v
        | None -> null
    conn.Query<LegacyHistoryRow>(
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
    |> List.map (fun r ->
        originOfText r.origin
        |> Result.map (fun origin ->
            { MovementId = Guid.Parse r.id
              ApplicationId = Guid.Parse r.application_id
              OccurredAt = dateFromText r.occurred_at
              Origin = Decision origin
              Month = r.month
              CategoryId = Guid.Parse r.category_id
              CategoryName = r.category_name
              OldAssigned = euroOfMilliunits r.old_assigned
              NewAssigned = euroOfMilliunits r.new_assigned
              Counterpart = Option.ofObj r.counterpart_name
              Latest = statusFromText r.status r.reason
              ReplacedBy = None }))
    |> collect

/// Die Zeilen einer Anwendung wie früher: rowid-Reihenfolge, jüngstes Ergebnis nach `sequence`.
let private legacyApplication (conn: SqliteConnection) (applicationId: Guid) : Result<MovementState list, string> =
    legacyMovements conn { CategoryId = None; Month = None; Origin = None }
    |> Result.map (fun all ->
        all
        |> List.filter (fun m -> m.ApplicationId = applicationId)
        |> List.rev
        |> List.map (fun m ->
            { Movement =
                { Id = m.MovementId
                  ApplicationId = m.ApplicationId
                  OccurredAt = m.OccurredAt
                  // Der Altbestand kennt nur Entscheidungen (`originOfText`).
                  Origin =
                    match m.Origin with
                    | Decision origin -> origin
                    | other -> failwith $"Altbestand mit Herkunft {other}"
                  Month = m.Month
                  CategoryId = m.CategoryId
                  CategoryName = m.CategoryName
                  OldAssigned = toMilliunits m.OldAssigned
                  NewAssigned = toMilliunits m.NewAssigned }
              Latest = m.Latest }))

let private legacyRecent (conn: SqliteConnection) (limit: int) : Guid list =
    conn.Query<string>(
        "SELECT category_id FROM budget_movements WHERE origin = @origin \
         GROUP BY category_id ORDER BY MAX(rowid) DESC LIMIT @limit",
        {| origin = originText MoneyMoved; limit = limit |}
    )
    |> Seq.map Guid.Parse
    |> Seq.toList

let private legacyPaused (conn: SqliteConnection) (month: string) : Result<Set<Guid>, string> =
    conn.Query<LegacyPauseEventRow>(
        "SELECT e.id, e.occurred_at, e.month, e.category_id, e.category_name, e.kind \
         FROM target_pause_events e \
         WHERE e.month = @month \
           AND e.rowid = (SELECT MAX(rowid) FROM target_pause_events l \
                          WHERE l.month = e.month AND l.category_id = e.category_id)",
        {| month = month |}
    )
    |> Seq.toList
    |> List.map (fun r -> pauseKindOfText r.kind |> Result.map (fun kind -> kind, Guid.Parse r.category_id))
    |> collect
    |> Result.map (List.choose (fun (kind, id) -> if kind = PauseKind.Paused then Some id else None) >> Set.ofList)

let private legacyPauseEvents (conn: SqliteConnection) (categoryId: Guid) (month: string) : Result<HistoryEntry list, string> =
    conn.Query<LegacyPauseEventRow>(
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
    |> collect

let private legacyTargetChanges (conn: SqliteConnection) (categoryId: Guid) (month: string) : Result<HistoryTargetChange list, string> =
    conn.Query<LegacyChangeHistoryRow>(
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
              DayBefore = None
              DayAfter = None
              Latest = statusFromText r.status r.reason
              DateStep = statusFromText r.date_status r.date_reason
              SentToYnab = true }))
    |> collect

let private legacyLastTargetChanges (conn: SqliteConnection) : Result<Map<Guid, TargetChangeMark>, string> =
    conn.Query<LegacyLastChangeRow>(
        "SELECT c.rowid AS row_order, c.occurred_at, c.category_id, c.change_kind, c.new_frequency \
         FROM target_changes c \
         JOIN target_change_results r1 \
           ON r1.sequence = (SELECT MAX(sequence) FROM target_change_results \
                             WHERE change_id = c.id AND COALESCE(step, 1) = 1) \
         WHERE r1.status = 'written'"
    )
    |> Seq.toList
    |> List.map (fun r ->
        targetChangeKindOfText r.change_kind r.new_frequency
        |> Result.map (fun kind -> Guid.Parse r.category_id, dateFromText r.occurred_at, r.row_order, kind))
    |> collect
    |> Result.map (fun changes ->
        changes
        |> List.groupBy (fun (categoryId, _, _, _) -> categoryId)
        |> List.map (fun (categoryId, rows) ->
            let _, at, _, kind = rows |> List.maxBy (fun (_, at, order, _) -> at, order)
            let day = (berlinTime at).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            let mark = if kind = TargetRemoved then TargetRemovedOn day else TargetSetOn day
            categoryId, mark)
        |> Map.ofList)

// ============================================
// Probe
// ============================================

/// Ergebnis der Probe: nur Zahlen und Wahrheitswerte.
type BudgetEventMigrationProbe = {
    /// Zeilen je früherer Tabelle vor der Migration.
    RowsBefore: (string * int) list
    /// Budget-Ereignisse je Typ in `events` nach der Migration.
    EventsAfter: (string * int) list
    /// `stream_version` je Budget-Stream lückenlos ab 1.
    StreamVersionsGapless: bool
    /// Die Zeitpunkte als Text: je Entscheidung, Ergebnis, Pause und Änderung derselbe Text wie vorher.
    OccurredAtUnchanged: bool
    /// Die allgemeine Historie (alle Zeilen samt Gegenseite und Stand).
    HistoryUnchanged: bool
    /// Je Kategorie und Monat: Zeilen, Pause-Ereignisse und Target-Änderungen.
    CategoryHistoriesUnchanged: bool
    /// "Zuletzt verwendet".
    RecentCategoriesUnchanged: bool
    /// Die BB-Pausen je Monat.
    PausesUnchanged: bool
    /// Die letzte Target-Änderung je Kategorie (Targets prüfen).
    LastTargetChangesUnchanged: bool
    /// Jede Anwendung über ihre Gruppen-Id ("erneut senden" und Bericht).
    ApplicationsUnchanged: bool
    /// Die fünf früheren Tabellen sind entfernt.
    LegacyTablesGone: bool
}

/// Die Leser über alle Kategorien, Monate und Anwendungen des Altbestands.
type private Readings = {
    History: Result<HistoryMovement list, string>
    CategoryHistories: (Result<HistoryMovement list, string> * Result<HistoryEntry list, string> * Result<HistoryTargetChange list, string>) list
    Recent: Result<Guid list, string>
    Paused: Result<Set<Guid>, string> list
    LastChanges: Result<Map<Guid, TargetChangeMark>, string>
    Applications: Result<MovementState list, string> list
}

/// Führt die Startmigration auf der übergebenen Connection aus und prüft sie. Gedacht für eine Kopie der
/// Produktions-Datenbank. Legt `events` an, falls sie fehlt, wie der Start.
let internal probeBudgetEventMigrationOn (conn: SqliteConnection) : BudgetEventMigrationProbe =
    if conn.State <> Data.ConnectionState.Open then
        conn.Open()
    EventStore.createSchemaOn conn

    let legacy = loadLegacyOn conn null
    let months =
        (legacy.Movements |> List.map (fun r -> r.month)) @ (legacy.Pauses |> List.map (fun r -> r.month))
        |> List.distinct
    let categoryMonths =
        (legacy.Movements |> List.map (fun r -> r.category_id, r.month))
        @ (legacy.Pauses |> List.map (fun r -> r.category_id, r.month))
        @ (legacy.TargetChanges |> List.map (fun r -> r.category_id, r.month))
        |> List.distinct
        |> List.map (fun (c, m) -> Guid.Parse c, m)
    let applications = legacy.Movements |> List.map (fun r -> Guid.Parse r.application_id) |> List.distinct
    let present = existingLegacyTables conn null
    let has table = present.Contains table

    let before =
        let movements filter = if has "budget_movements" then legacyMovements conn filter else Ok []
        { History = movements { CategoryId = None; Month = None; Origin = None }
          CategoryHistories =
            categoryMonths
            |> List.map (fun (c, m) ->
                movements { CategoryId = Some c; Month = Some m; Origin = None },
                (if has "target_pause_events" then legacyPauseEvents conn c m else Ok []),
                (if has "target_changes" then legacyTargetChanges conn c m else Ok []))
          Recent = Ok (if has "budget_movements" then legacyRecent conn 10 else [])
          Paused = months |> List.map (fun m -> if has "target_pause_events" then legacyPaused conn m else Ok Set.empty)
          LastChanges = if has "target_changes" then legacyLastTargetChanges conn else Ok Map.empty
          Applications = applications |> List.map (fun a -> if has "budget_movements" then legacyApplication conn a else Ok []) }

    let occurredBefore =
        (legacy.Movements |> List.groupBy (fun r -> r.application_id) |> List.map (fun (_, g) -> (List.head g).occurred_at))
        @ (legacy.MovementResults |> List.map (fun r -> r.occurred_at))
        @ (legacy.Pauses |> List.map (fun r -> r.occurred_at))
        @ (legacy.TargetChanges |> List.map (fun r -> r.occurred_at))
        @ (legacy.TargetChangeResults |> List.map (fun r -> r.occurred_at))
        |> List.sort

    migrateBudgetEventsOn conn |> ignore

    let after =
        { History = queryMovements conn { CategoryId = None; Month = None; Origin = None }
          CategoryHistories =
            categoryMonths
            |> List.map (fun (c, m) ->
                queryMovements conn { CategoryId = Some c; Month = Some m; Origin = None },
                queryPauseEvents conn c m,
                queryTargetChanges conn c m)
          Recent = queryRecentCategories conn MoneyMoved 10
          Paused = months |> List.map (queryPausedCategories conn)
          LastChanges = queryLastTargetChanges conn
          Applications = applications |> List.map (queryApplication conn) }

    let stored =
        conn.Query<StoredEventRow>(
            """SELECT position, event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                      actor_ref, event_type, schema_version, payload, command_id
               FROM events WHERE context = @context""",
            {| context = Budgeting.Events.Context |}
        )
        |> Seq.toList

    let gapless =
        stored
        |> List.groupBy (fun r -> r.stream_id)
        |> List.forall (fun (_, rows) ->
            (rows |> List.map (fun r -> r.stream_version) |> List.sort) = [ 1L .. int64 rows.Length ])

    { RowsBefore =
        [ "budget_movements", legacy.Movements.Length
          "budget_movement_results", legacy.MovementResults.Length
          "target_pause_events", legacy.Pauses.Length
          "target_changes", legacy.TargetChanges.Length
          "target_change_results", legacy.TargetChangeResults.Length ]
      EventsAfter = stored |> List.countBy (fun r -> r.event_type) |> List.sort
      StreamVersionsGapless = gapless
      OccurredAtUnchanged = (stored |> List.map (fun r -> r.occurred_at) |> List.sort) = occurredBefore
      HistoryUnchanged = before.History = after.History
      CategoryHistoriesUnchanged = before.CategoryHistories = after.CategoryHistories
      RecentCategoriesUnchanged = before.Recent = after.Recent
      PausesUnchanged = before.Paused = after.Paused
      LastTargetChangesUnchanged = before.LastChanges = after.LastChanges
      ApplicationsUnchanged = before.Applications = after.Applications
      LegacyTablesGone = (existingLegacyTables conn null).IsEmpty }
