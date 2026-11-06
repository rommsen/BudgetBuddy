/// Die Budget-Ereignisse in `events` lesen und schreiben (Context `budgeting`, ADR 0083, ADR 0084,
/// budgeting-hyn77). Entscheidungen (Zuweisen, Verschieben, Targets anwenden), Pausen und ihre
/// Rückschreib-Ergebnisse liegen im Monats-Stream, Übernahme und Änderungen der Targets (samt Altbestand mit
/// Schritt-Ergebnissen) im Target-Stream der Kategorie. Jeder Leser filtert auf `context = 'budgeting'` und sortiert fachlich
/// (`occurred_at`, `stream_version`), nie nach `position`. Ein unbekannter Ereignistyp im Kontext wird
/// gemeldet, nicht übersprungen.
module Budgeting.Persistence

open System
open System.Globalization
open Microsoft.Data.Sqlite
open Dapper
open Shared.Budgeting
open Server.Budgeting.Domain
open Budgeting.Events

module Core = global.Persistence

type private StoredEventRow = EventStore.StoredEventRow

/// Eine Zeile einer festgehaltenen Entscheidung. Beträge in Milliunits.
type Movement = {
    /// Die `lineId` der Zeile.
    Id: Guid
    /// Die `event_id` des Entscheidungsereignisses.
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

/// Eine Zeile mit ihrem jüngsten Ergebnis (`None`, solange keines angehängt ist).
type MovementState = {
    Movement: Movement
    Latest: LineOutcome option
}

/// Entfernt `category_month_activity` (bis budgeting-k5q2w YNABs Aktivität je Monat, abgeleiteter Zustand), nur
/// wenn sie noch da ist: ein DROP sperrt auch ohne Tabelle das Schema für alle Verbindungen. Wiederholbar.
let dropMonthActivityOn (conn: SqliteConnection) : unit =
    let leftover =
        conn.ExecuteScalar<int64>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'category_month_activity'")
    if leftover > 0L then
        conn.Execute("DROP TABLE IF EXISTS category_month_activity") |> ignore

/// Entfernt `underfunded_comparisons` (bis budgeting-hbhzf die Abweichungsliste der Parallelrechnung, vorläufiger
/// Zustand), nur wenn sie noch da ist, aus demselben Grund wie `dropMonthActivityOn`. Wiederholbar.
let dropUnderfundedComparisonsOn (conn: SqliteConnection) : unit =
    let leftover =
        conn.ExecuteScalar<int64>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'underfunded_comparisons'")
    if leftover > 0L then
        conn.Execute("DROP TABLE IF EXISTS underfunded_comparisons") |> ignore

// ============================================
// Leiste Monatserster ausgeblendet (budgeting-m3x7f)
// ============================================

/// Für welche Monate die Leiste Monatserster ausgeblendet ist. Zustand, kein Ereignis (ADR 0083): Ausblenden bewegt
/// kein Geld und entscheidet nichts über Geld. Eine eigene kleine Tabelle statt eines Settings-Schlüssels, weil der
/// Zustand je Monat gilt: ein Ausblenden im kommenden Monat nimmt das im laufenden nicht weg. Wiederholbar.
let createMonthStartHiddenSchemaOn (conn: SqliteConnection) : unit =
    conn.Execute(
        """CREATE TABLE IF NOT EXISTS budget_month_start_hidden (
               month     TEXT NOT NULL PRIMARY KEY,
               hidden_at TEXT NOT NULL
           )"""
    )
    |> ignore

/// Blendet die Leiste für den Monat ("YYYY-MM-01") aus. Ein zweites Ausblenden ändert nichts, der erste Zeitpunkt bleibt.
let hideMonthStartOn (conn: SqliteConnection) (month: string) (at: DateTime) : unit =
    conn.Execute(
        "INSERT OR IGNORE INTO budget_month_start_hidden (month, hidden_at) VALUES (@month, @at)",
        {| month = month; at = at.ToString("o", CultureInfo.InvariantCulture) |}
    )
    |> ignore

/// Die Monate ab `fromMonth` ("YYYY-MM-01", einschließlich), für die die Leiste ausgeblendet ist, aufsteigend. Ältere
/// Monate braucht die Seite nicht: dort steht die Leiste nie.
let loadMonthStartHiddenOn (conn: SqliteConnection) (fromMonth: string) : string list =
    conn.Query<string>(
        "SELECT month FROM budget_month_start_hidden WHERE month >= @fromMonth ORDER BY month",
        {| fromMonth = fromMonth |}
    )
    |> List.ofSeq

/// Legt `events` (ADR 0083) an und überführt die früheren Budget-Tabellen nach `events` (budgeting-hyn77). Entfernt
/// `category_month_activity` (budgeting-k5q2w: die Ausgaben der Periode kommen aus dem Kassenbuch) und
/// `underfunded_comparisons` (budgeting-hbhzf: BB rechnet den Fehlbetrag selbst, die Parallelrechnung entfällt),
/// beides abgeleiteter Zustand ohne Geldentscheidung. Legt `budget_month_start_hidden` an (budgeting-m3x7f).
/// Wiederholbar. Läuft beim Start hinter `Ledger.Persistence.initializeLedgerSchema`. Bricht die Migration ab, wirft der Aufruf und BB startet nicht.
let initializeBudgetingSchema () =
    use conn = Core.getConnection ()
    EventStore.createSchemaOn conn
    createMonthStartHiddenSchemaOn conn
    dropMonthActivityOn conn
    dropUnderfundedComparisonsOn conn
    Budgeting.EventMigration.migrateBudgetEventsOn conn |> ignore

// ============================================
// Laden
// ============================================

/// Ein gelesenes Budget-Ereignis.
type RecordedBudgetEvent = {
    EventId: Guid
    StreamId: string
    StreamVersion: int64
    OccurredAt: DateTime
    Event: BudgetEvent
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

let private toRecordedBudgetEvent (row: StoredEventRow) : Result<RecordedBudgetEvent, string> =
    let recorded = EventStore.toRecordedEvent row
    decodePayload recorded.EventType recorded.SchemaVersion recorded.Payload
    |> Result.mapError (fun message -> $"Ereignis {recorded.EventId} in {recorded.StreamId}: {message}")
    |> Result.map (fun event ->
        { EventId = recorded.EventId
          StreamId = recorded.StreamId
          StreamVersion = recorded.StreamVersion
          OccurredAt = recorded.OccurredAt
          Event = event })

/// Welche Budget-Ereignisse ein Leser lädt.
type internal EventScope =
    /// Ein Stream, alle Typen.
    | InStream of streamId: string
    /// Der ganze Kontext, nur diese Typen (`Events.EventTypes`).
    | OfTypes of eventTypes: string list
    /// Der ganze Kontext, alle Typen.
    | WholeContext

/// Die Budget-Ereignisse zum Bereich, je Stream nach `stream_version`. Ein Ereignis, das sich nicht
/// lesen lässt (unbekannter Typ, kaputte Payload), wird zum `Error` mit Ereignis-Id und Stream. Ein
/// Typfilter blendet Ereignisse anderer Typen aus, auch unbekannte.
let internal loadBudgetEventsOn (conn: SqliteConnection) (scope: EventScope) : Result<RecordedBudgetEvent list, string> =
    let rows =
        match scope with
        | InStream streamId ->
            conn.Query<StoredEventRow>(
                """SELECT position, event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                          actor_ref, event_type, schema_version, payload, command_id
                   FROM events
                   WHERE context = @context AND stream_id = @stream_id
                   ORDER BY stream_id, stream_version""",
                {| context = Context; stream_id = streamId |}
            )
        | OfTypes eventTypes ->
            conn.Query<StoredEventRow>(
                """SELECT position, event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                          actor_ref, event_type, schema_version, payload, command_id
                   FROM events
                   WHERE context = @context AND event_type IN @event_types
                   ORDER BY stream_id, stream_version""",
                {| context = Context; event_types = List.toArray eventTypes |}
            )
        | WholeContext ->
            conn.Query<StoredEventRow>(
                """SELECT position, event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                          actor_ref, event_type, schema_version, payload, command_id
                   FROM events
                   WHERE context = @context
                   ORDER BY stream_id, stream_version""",
                {| context = Context |}
            )
    rows
    |> Seq.toList
    |> List.map toRecordedBudgetEvent
    |> collectRows

/// Die Ursache eines Lesefehlers bleibt im Server-Log, der Aufrufer bekommt nur den Text der Meldung.
let logReadFailure (what: string) (cause: string) : unit =
    eprintfn "%s gescheitert: %s" what cause

/// Liest über `connect`; ein `Error` oder eine Ausnahme wird geloggt und zu `unreadable`.
let private readWith
    (connect: unit -> SqliteConnection)
    (what: string)
    (unreadable: string)
    (query: SqliteConnection -> Result<'T, string>)
    : Async<Result<'T, string>> =
    async {
        try
            use conn = connect ()
            match query conn with
            | Ok value -> return Ok value
            | Error message ->
                logReadFailure what message
                return Error unreadable
        with ex ->
            logReadFailure what ex.Message
            return Error unreadable
    }

/// Der Monats-Stream zu "YYYY-MM-01", `None` bei einem Monat in anderer Form.
let private monthStreamOf (monthKey: string) : string option =
    monthIdOf monthKey |> Result.toOption |> Option.map (MonthStream >> streamIdOf)

// ============================================
// Entscheidungen und ihre Zeilen
// ============================================

let private decisionOf (event: BudgetEvent) : (MovementOrigin * string * BudgetLine list) option =
    match event with
    | BudgetEvent.AssignmentChangedInBb p -> Some (MoneyAssigned, p.Month, [ p.Line ])
    | BudgetEvent.MoneyMoved p -> Some (MoneyMoved, p.Month, [ p.From; p.To ])
    | BudgetEvent.TargetsApplied p -> Some (TargetsApplied, p.Month, p.Lines)
    | _ -> None

/// Die Zeilen eines Entscheidungsereignisses in Sendereihenfolge (bei einer Verschiebung erst die
/// Quelle). Leer für jedes andere Ereignis.
let private movementsOf (recorded: RecordedBudgetEvent) : Movement list =
    match decisionOf recorded.Event with
    | None -> []
    | Some (origin, monthId, lines) ->
        lines
        |> List.map (fun l ->
            { Id = l.LineId
              ApplicationId = recorded.EventId
              OccurredAt = recorded.OccurredAt
              Origin = origin
              Month = monthKeyOfId monthId
              CategoryId = l.CategoryId
              CategoryName = l.CategoryName
              OldAssigned = l.PreviousAssigned
              NewAssigned = l.Assigned })

let private writeBackOf (event: BudgetEvent) : (WriteBackRef * LineOutcome) option =
    match event with
    | BudgetEvent.AssignmentWrittenBackToYnab r -> Some (r, lineOutcomeOf WrittenBack)
    | BudgetEvent.AssignmentWriteBackFailed (r, reason) -> Some (r, lineOutcomeOf (WriteBackFailed reason))
    | BudgetEvent.AssignmentWriteBackSupersededInYnab r -> Some (r, lineOutcomeOf SupersededInYnab)
    | BudgetEvent.AssignmentWriteBackWithdrawn r -> Some (r, lineOutcomeOf Withdrawn)
    | _ -> None

/// Je (Entscheidung, Kategorie) das jüngste Ergebnis. Die Ereignisse kommen je Stream nach
/// `stream_version`, ein Ergebnis liegt im Stream seiner Entscheidung.
let private latestWriteBacks (events: RecordedBudgetEvent list) : Map<Guid * Guid, LineOutcome> =
    events
    |> List.choose (fun e -> writeBackOf e.Event)
    |> List.fold (fun acc (r, outcome) -> Map.add (r.DecisionEventId, r.CategoryId) outcome acc) Map.empty

let private stateOf (latest: Map<Guid * Guid, LineOutcome>) (m: Movement) : MovementState =
    { Movement = m; Latest = Map.tryFind (m.ApplicationId, m.CategoryId) latest }

/// Alle Zeilen einer Anwendung (Gruppen-Id = `event_id` der Entscheidung) mit ihrem jüngsten Ergebnis.
/// Leer, wenn es keine Entscheidung mit dieser Id gibt.
let internal queryApplication (conn: SqliteConnection) (applicationId: Guid) : Result<MovementState list, string> =
    let stream =
        conn.Query<string>(
            "SELECT stream_id FROM events WHERE context = @context AND event_id = @event_id",
            {| context = Context; event_id = applicationId.ToString "D" |}
        )
        |> Seq.tryHead
    match stream with
    | None -> Ok []
    | Some streamId ->
        loadBudgetEventsOn conn (InStream streamId)
        |> Result.map (fun events ->
            let latest = latestWriteBacks events
            events
            |> List.filter (fun e -> e.EventId = applicationId)
            |> List.collect movementsOf
            |> List.map (stateOf latest))

/// `queryApplication` über `connect`.
let loadApplicationWith (connect: unit -> SqliteConnection) (applicationId: Guid) : Async<Result<MovementState list, string>> =
    async {
        use conn = connect ()
        return queryApplication conn applicationId
    }

/// `queryApplication` auf der Datenbank der Anwendung.
let loadApplication (applicationId: Guid) : Async<Result<MovementState list, string>> =
    loadApplicationWith Core.getConnection applicationId

/// Eine Zeilen-Id je (Ereignis, Kategorie) für die Kategorien einer Übernahme: stabil über jedes Lesen, ohne
/// sie zu speichern. Sie ist nur der Anzeigeschlüssel der Historienzeile (`MovementId`); kein Ergebnis-Ereignis
/// und keine andere Abfrage verweist auf sie, eine Übernahme wird nicht nach YNAB geschrieben.
let private takeoverLineId (eventId: Guid) (categoryId: Guid) : Guid =
    let a = eventId.ToByteArray()
    let b = categoryId.ToByteArray()
    Guid(Array.map2 (^^^) a b)

/// Eine Zeile der Historie: eine Zeile einer Entscheidung oder eine aus YNAB (budgeting-dgrp7), mit ihrem
/// jüngsten Ergebnis. Beträge in Milliunits.
type private HistoryItem = {
    Origin: HistoryOrigin
    /// Die `lineId`, bei einer Übernahme `takeoverLineId`, bei einer Beobachtung die `event_id`.
    RowId: Guid
    /// Die `event_id` des Ereignisses.
    EventId: Guid
    At: DateTime
    MonthKey: string
    Category: Guid
    Name: string
    Before: int64
    After: int64
    Outcome: LineOutcome option
}

let private historyItemOf (m: Movement) (outcome: LineOutcome option) : HistoryItem =
    { Origin = Decision m.Origin
      RowId = m.Id
      EventId = m.ApplicationId
      At = m.OccurredAt
      MonthKey = m.Month
      Category = m.CategoryId
      Name = m.CategoryName
      Before = m.OldAssigned
      After = m.NewAssigned
      Outcome = outcome }

/// Übernahme und Beobachtung als Zeilen der Historie (budgeting-dgrp7): je Kategorie einer Übernahme eine Zeile
/// mit dem übernommenen Wert vorher und nachher, je Beobachtung eine Zeile vom Soll davor zum YNAB-Wert. Sie
/// sind in YNAB, ihr Stand ist geschrieben.
let private fromYnabOf (recorded: RecordedBudgetEvent) : HistoryItem list =
    let row origin (monthId: string) (rowId: Guid) (categoryId: Guid) (name: string) (previous: int64) (assigned: int64) : HistoryItem =
        { Origin = origin
          RowId = rowId
          EventId = recorded.EventId
          At = recorded.OccurredAt
          MonthKey = monthKeyOfId monthId
          Category = categoryId
          Name = name
          Before = previous
          After = assigned
          Outcome = Some Written }
    match recorded.Event with
    | BudgetEvent.BudgetTakenOverFromYnab p ->
        p.Categories
        |> List.map (fun c ->
            row TakenOverFromYnab p.Month (takeoverLineId recorded.EventId c.CategoryId) c.CategoryId c.CategoryName c.Assigned c.Assigned)
    | BudgetEvent.AssignmentChangeObservedInYnab p ->
        [ row ObservedInYnab p.Month recorded.EventId p.CategoryId p.CategoryName p.PreviousAssigned p.Assigned ]
    | _ -> []

/// Die Zeilen der Historie eines Ereignisses: Entscheidungen mit ihrem jüngsten Ergebnis, Übernahme und
/// Beobachtung als geschrieben.
let private historyRowsOf (latest: Map<Guid * Guid, LineOutcome>) (recorded: RecordedBudgetEvent) : HistoryItem list =
    match movementsOf recorded with
    | [] -> fromYnabOf recorded
    | lines -> lines |> List.map (fun m -> historyItemOf m (Map.tryFind (m.ApplicationId, m.CategoryId) latest))

/// Zurückgezogene Zeilen, die eine spätere Entscheidung in BB ersetzt hat (budgeting-dgrp7), abgeleitet aus dem
/// Stream: Die jüngste Entscheidung derselben Kategorie vor dem Zurückziehen ist eine andere als die der Zeile.
/// Je (Entscheidung, Kategorie) die Gruppen-Id der ersetzenden Entscheidung. Die Ereignisse kommen je Stream nach
/// `stream_version`.
let private replacedLines (events: RecordedBudgetEvent list) : Map<Guid * Guid, Guid> =
    events
    |> List.fold
        (fun (lastDecision: Map<string * Guid, Guid>, replaced: Map<Guid * Guid, Guid>) e ->
            match e.Event with
            | BudgetEvent.AssignmentWriteBackWithdrawn r ->
                match lastDecision |> Map.tryFind (e.StreamId, r.CategoryId) with
                | Some decision when decision <> r.DecisionEventId ->
                    lastDecision, Map.add (r.DecisionEventId, r.CategoryId) decision replaced
                | _ -> lastDecision, replaced
            | _ ->
                let lastDecision =
                    movementsOf e |> List.fold (fun acc m -> Map.add (e.StreamId, m.CategoryId) e.EventId acc) lastDecision
                lastDecision, replaced)
        (Map.empty, Map.empty)
    |> snd

/// Neueste zuerst: Zeitpunkt, dann Stream und Version, in einer Entscheidung die spätere Zeile zuerst.
let private newestFirstWith (rowsOf: RecordedBudgetEvent -> 'row list) (events: RecordedBudgetEvent list) : (RecordedBudgetEvent * 'row) list =
    events
    |> List.sortWith (fun a b ->
        match compare b.OccurredAt a.OccurredAt with
        | 0 ->
            match compare b.StreamId a.StreamId with
            | 0 -> compare b.StreamVersion a.StreamVersion
            | c -> c
        | c -> c)
    |> List.collect (fun e -> rowsOf e |> List.rev |> List.map (fun m -> e, m))

let private newestFirst (events: RecordedBudgetEvent list) : (RecordedBudgetEvent * Movement) list =
    newestFirstWith movementsOf events

/// Die Zeilen zum Filter, neueste zuerst, je mit dem jüngsten Ergebnis und bei einer Verschiebung mit
/// der Kategorie der Gegenseite. `Error` nennt ein nicht lesbares Ereignis.
let queryMovements (conn: SqliteConnection) (filter: MovementFilter) : Result<HistoryMovement list, string> =
    // Ohne Monat reichen die Entscheidungen und ihre Rückschreib-Ergebnisse, Pausen und Target-Änderungen
    // liest die Historie nicht.
    let scope =
        match filter.Month with
        | None -> Some (OfTypes (EventTypes.decisions @ EventTypes.writeBacks @ EventTypes.fromYnab))
        | Some month -> monthStreamOf month |> Option.map InStream
    match scope with
    | None -> Ok []
    | Some scope ->
        loadBudgetEventsOn conn scope
        |> Result.map (fun events ->
            let latest = latestWriteBacks events
            let replaced = replacedLines events
            let counterpart (e: RecordedBudgetEvent) (r: HistoryItem) =
                match e.Event with
                | BudgetEvent.MoneyMoved p ->
                    [ p.From; p.To ] |> List.tryFind (fun l -> l.LineId <> r.RowId) |> Option.map (fun l -> l.CategoryName)
                | _ -> None
            newestFirstWith (historyRowsOf latest) events
            |> List.filter (fun (_, r) ->
                (filter.CategoryId |> Option.forall ((=) r.Category))
                && (filter.Month |> Option.forall ((=) r.MonthKey))
                && (filter.Origin |> Option.forall (fun o -> r.Origin = Decision o)))
            |> List.map (fun (e, r) ->
                { MovementId = r.RowId
                  ApplicationId = r.EventId
                  OccurredAt = r.At
                  Origin = r.Origin
                  Month = r.MonthKey
                  CategoryId = r.Category
                  CategoryName = r.Name
                  OldAssigned = euroOfMilliunits r.Before
                  NewAssigned = euroOfMilliunits r.After
                  Counterpart = counterpart e r
                  Latest = r.Outcome
                  ReplacedBy = Map.tryFind (r.EventId, r.Category) replaced }))

/// Die Historie über `connect`. Liest nur die Datenbank, kein YNAB.
let loadMovementsWith (connect: unit -> SqliteConnection) (filter: MovementFilter) : Async<Result<HistoryMovement list, string>> =
    async {
        use conn = connect ()
        return queryMovements conn filter
    }

/// Die Historie liest nur die Datenbank, kein YNAB.
let loadMovements (filter: MovementFilter) : Async<Result<HistoryMovement list, string>> =
    loadMovementsWith Core.getConnection filter

/// Die Kategorien der jüngsten Entscheidungen einer Herkunft, neueste zuerst, ohne Dubletten.
let queryRecentCategories (conn: SqliteConnection) (origin: MovementOrigin) (limit: int) : Result<Guid list, string> =
    loadBudgetEventsOn conn (OfTypes [ EventTypes.decisionOf origin ])
    |> Result.map (fun events ->
        newestFirst events
        |> List.filter (fun (_, m) -> m.Origin = origin)
        |> List.map (fun (_, m) -> m.CategoryId)
        |> List.distinct
        |> List.truncate limit)

/// Die Meldung, wenn "Zuletzt verwendet" nicht lesbar ist. Die Ursache bleibt im Server-Log.
[<Literal>]
let RecentCategoriesUnreadableText = "Zuletzt verwendete Kategorien nicht lesbar"

/// "Zuletzt verwendet" im Picker über `connect`: nur Verschiebungen zwischen zwei Kategorien. Ein
/// Lesefehler wird zu `Error RecentCategoriesUnreadableText`.
let loadRecentMoveCategoriesWith (connect: unit -> SqliteConnection) (limit: int) : Async<Result<Guid list, string>> =
    readWith connect "Zuletzt verwendet lesen" RecentCategoriesUnreadableText (fun conn -> queryRecentCategories conn MoneyMoved limit)

/// `loadRecentMoveCategoriesWith` auf der Datenbank der Anwendung.
let loadRecentMoveCategories (limit: int) : Async<Result<Guid list, string>> =
    loadRecentMoveCategoriesWith Core.getConnection limit

// ============================================
// Target pausieren (budgeting-t6p8z, ADR 0079)
// ============================================

let private pauseOf (event: BudgetEvent) : (PauseKind * PauseChange) option =
    match event with
    | BudgetEvent.TargetPaused p -> Some (PauseKind.Paused, p)
    | BudgetEvent.TargetPauseLifted p -> Some (PauseKind.PauseLifted, p)
    | _ -> None

/// Die Kategorien, deren Target in BB für den Monat pausiert ist: je Kategorie zählt das jüngste
/// Pause-Ereignis im Monats-Stream nach `stream_version`. Ein nicht lesbares Ereignis wird gemeldet.
let queryPausedCategories (conn: SqliteConnection) (month: string) : Result<Set<Guid>, string> =
    match monthStreamOf month with
    | None -> Ok Set.empty
    | Some stream ->
        loadBudgetEventsOn conn (InStream stream)
        |> Result.map (fun events ->
            events
            |> List.choose (fun e -> pauseOf e.Event)
            |> List.fold (fun acc (kind, p) -> Map.add p.CategoryId kind acc) Map.empty
            |> Map.filter (fun _ kind -> kind = PauseKind.Paused)
            |> Map.keys
            |> Set.ofSeq)

/// Die Meldung, wenn die Pausen nicht lesbar sind. Die Ursache bleibt im Server-Log.
[<Literal>]
let PausesUnreadableText = "Pausen nicht lesbar"

/// Die Meldung, wenn die Pause-Ereignisse für die Historie nicht lesbar sind.
[<Literal>]
let PauseEventsUnreadableText = "Pause-Ereignisse nicht lesbar"

/// Die BB-Pausen des Monats über `connect`. Ein Lesefehler wird zu `Error PausesUnreadableText`.
let loadPausedCategoriesWith (connect: unit -> SqliteConnection) (month: string) : Async<Result<Set<Guid>, string>> =
    readWith connect "Pausen lesen" PausesUnreadableText (fun conn -> queryPausedCategories conn month)

/// Die BB-Pausen des Monats, für `toBudgetMonth` (budgeting-r3wq8).
let loadPausedCategories (month: string) : Async<Result<Set<Guid>, string>> =
    loadPausedCategoriesWith Core.getConnection month

/// Die Pause-Ereignisse einer Kategorie im Monat für die Historie, neueste zuerst nach `stream_version`.
let queryPauseEvents (conn: SqliteConnection) (categoryId: Guid) (month: string) : Result<HistoryEntry list, string> =
    match monthStreamOf month with
    | None -> Ok []
    | Some stream ->
        loadBudgetEventsOn conn (InStream stream)
        |> Result.map (fun events ->
            events
            |> List.choose (fun e ->
                pauseOf e.Event
                |> Option.filter (fun (_, p) -> p.CategoryId = categoryId)
                |> Option.map (fun (kind, p) ->
                    let pause =
                        { EventId = e.EventId
                          OccurredAt = e.OccurredAt
                          Month = monthKeyOfId p.Month
                          CategoryId = p.CategoryId
                          CategoryName = p.CategoryName }
                    match kind with
                    | PauseKind.Paused -> TargetPaused pause
                    | PauseKind.PauseLifted -> HistoryEntry.PauseLifted pause))
            |> List.rev)

/// Die Pause-Ereignisse für die Historie über `connect`. Ein Lesefehler kommt als `Error` mit Text.
let loadPauseEventsWith (connect: unit -> SqliteConnection) (categoryId: Guid) (month: string) : Async<Result<HistoryEntry list, string>> =
    readWith connect "Pause-Ereignisse lesen" PauseEventsUnreadableText (fun conn -> queryPauseEvents conn categoryId month)

/// Die Pause-Ereignisse für die Historie. Liest nur die Datenbank, kein YNAB.
let loadPauseEvents (categoryId: Guid) (month: string) : Async<Result<HistoryEntry list, string>> =
    loadPauseEventsWith Core.getConnection categoryId month

// ============================================
// Targets (budgeting-k8wq3, ADR 0080, budgeting-eh8xe)
// ============================================

let private goalStepOf (event: BudgetEvent) : (GoalStepRef * LineOutcome) option =
    match event with
    | BudgetEvent.YnabGoalStepWrittenToYnab r -> Some (r, Written)
    | BudgetEvent.YnabGoalStepWriteFailed (r, reason) -> Some (r, Failed reason)
    | BudgetEvent.YnabGoalStepSupersededInYnab r -> Some (r, ChangedInYnab)
    | _ -> None

/// Je (Änderung, Schritt) das jüngste Ergebnis, die Ereignisse je Stream nach `stream_version`.
let private latestGoalSteps (events: RecordedBudgetEvent list) : Map<Guid * int, LineOutcome> =
    events
    |> List.choose (fun e -> goalStepOf e.Event)
    |> List.fold (fun acc (r, outcome) -> Map.add (r.DecisionEventId, r.Step) outcome acc) Map.empty

/// Der jüngste bekannte Name der Kategorie im Target-Stream: aus Übernahme oder Altbestand. Eine Änderung in BB
/// trägt keinen Namen, weil Target ändern YNAB nicht liest (budgeting-eh8xe).
let private categoryNameIn (events: RecordedBudgetEvent list) : string =
    events
    |> List.choose (fun e ->
        match e.Event with
        | BudgetEvent.TargetTakenOverFromYnab p -> Some p.CategoryName
        | BudgetEvent.YnabGoalChangedFromBb p -> Some p.CategoryName
        | _ -> None)
    |> List.tryLast
    |> Option.defaultValue ""

/// Die Target-Änderungen einer Kategorie im Monat für die Historie, neueste zuerst nach `stream_version`: der
/// Altbestand je mit dem jüngsten Ergebnis von Schritt 1 und von Schritt 2, die Änderungen in BB (`TargetSet`,
/// `TargetRemoved`) mit Art, vorher und nachher aus der vorigen Faltung (ADR 0084 Punkt 2). Die Übernahme ist
/// keine Änderung.
let queryTargetChanges (conn: SqliteConnection) (categoryId: Guid) (month: string) : Result<HistoryTargetChange list, string> =
    match monthIdOf month with
    | Error _ -> Ok []
    | Ok monthId ->
        loadBudgetEventsOn conn (InStream (streamIdOf (TargetStream categoryId)))
        |> Result.map (fun events ->
            let steps = latestGoalSteps events
            let name = categoryNameIn events
            let states = events |> List.map (fun e -> e.Event) |> Server.Budgeting.TargetRules.withStates
            List.zip events states
            |> List.choose (fun (e, (_, before, after)) ->
                let inBb (effectiveFrom: string) =
                    if effectiveFrom <> monthId then None
                    else
                        Some
                            { ChangeId = e.EventId
                              OccurredAt = e.OccurredAt
                              Month = monthKeyOfId effectiveFrom
                              CategoryId = categoryId
                              CategoryName = name
                              ChangeKind = Server.Budgeting.TargetRules.changeKindBetween before after
                              GoalBefore = before |> Option.map (fun t -> t.Goal)
                              GoalAfter = after |> Option.map (fun t -> t.Goal)
                              DateBefore = before |> Option.bind (fun t -> t.Date)
                              DateAfter = after |> Option.bind (fun t -> t.Date)
                              DayBefore = before |> Option.bind (fun t -> t.Day)
                              DayAfter = after |> Option.bind (fun t -> t.Day)
                              Latest = None
                              DateStep = None
                              SentToYnab = false }
                match e.Event with
                | BudgetEvent.TargetSet p -> inBb p.EffectiveFrom
                | BudgetEvent.TargetRemoved p -> inBb p.EffectiveFrom
                | BudgetEvent.YnabGoalChangedFromBb p when p.Month = monthId ->
                    Some
                        { ChangeId = e.EventId
                          OccurredAt = e.OccurredAt
                          Month = monthKeyOfId p.Month
                          CategoryId = categoryId
                          CategoryName = p.CategoryName
                          ChangeKind = p.ChangeKind
                          GoalBefore = p.OldGoalTarget |> Option.map euroOfMilliunits
                          GoalAfter = p.NewGoalTarget |> Option.map euroOfMilliunits
                          DateBefore = p.OldDate
                          DateAfter = p.NewDate
                          DayBefore = None
                          DayAfter = None
                          Latest = Map.tryFind (e.EventId, 1) steps
                          DateStep = Map.tryFind (e.EventId, 2) steps
                          SentToYnab = true }
                | _ -> None)
            |> List.rev)

/// Die Meldung, wenn die Target-Änderungen für die Historie nicht lesbar sind.
[<Literal>]
let TargetChangesUnreadableText = "Target-Änderungen nicht lesbar"

/// Die Target-Änderungen für die Historie über `connect`. Ein Lesefehler kommt als `Error` mit Text.
let loadTargetChangesWith (connect: unit -> SqliteConnection) (categoryId: Guid) (month: string) : Async<Result<HistoryTargetChange list, string>> =
    readWith connect "Target-Änderungen lesen" TargetChangesUnreadableText (fun conn -> queryTargetChanges conn categoryId month)

/// Die Target-Änderungen für die Historie. Liest nur die Datenbank, kein YNAB.
let loadTargetChanges (categoryId: Guid) (month: string) : Async<Result<HistoryTargetChange list, string>> =
    loadTargetChangesWith Core.getConnection categoryId month

/// Je Kategorie die jüngste Target-Änderung (Targets prüfen, budgeting-p2zr6, ADR 0082 Punkt 7, Nachtrag ADR
/// 0084): ein `TargetSet` oder `TargetRemoved`, oder aus dem Altbestand eine Änderung, deren jüngstes Ergebnis
/// von Schritt 1 geschrieben ist. `TargetTakenOverFromYnab` zählt nicht. Jüngste nach `occurred_at`, bei gleichem
/// Zeitpunkt nach `stream_version`. Der Tag ist der Berliner Kalendertag "YYYY-MM-DD" (`berlinTime`).
let queryLastTargetChanges (conn: SqliteConnection) : Result<Map<Guid, TargetChangeMark>, string> =
    loadBudgetEventsOn conn (OfTypes (EventTypes.goalChanges @ EventTypes.targetChanges))
    |> Result.map (fun events ->
        let steps = latestGoalSteps events
        events
        |> List.choose (fun e ->
            let removed =
                match e.Event with
                | BudgetEvent.TargetSet _ -> Some false
                | BudgetEvent.TargetRemoved _ -> Some true
                | BudgetEvent.YnabGoalChangedFromBb p when Map.tryFind (e.EventId, 1) steps = Some Written ->
                    Some (p.ChangeKind = TargetChangeKind.TargetRemoved)
                | _ -> None
            match categoryOfTargetStream e.StreamId, removed with
            | Some categoryId, Some removed -> Some (categoryId, e, removed)
            | _ -> None)
        |> List.groupBy (fun (categoryId, _, _) -> categoryId)
        |> List.map (fun (categoryId, rows) ->
            let _, e, removed = rows |> List.maxBy (fun (_, e, _) -> e.OccurredAt, e.StreamVersion)
            let day = (berlinTime e.OccurredAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            categoryId, (if removed then TargetRemovedOn day else TargetSetOn day))
        |> Map.ofList)

/// Die letzten Target-Änderungen aller Kategorien für Targets prüfen über die Verbindung aus `connect`.
/// Ein Lesefehler kommt als `Error` mit `TargetChangesUnreadableText`, die Ursache bleibt im Server-Log.
let loadLastTargetChangesWith (connect: unit -> SqliteConnection) : Async<Result<Map<Guid, TargetChangeMark>, string>> =
    readWith connect "Letzte Target-Änderungen lesen" TargetChangesUnreadableText queryLastTargetChanges

/// `loadLastTargetChangesWith` auf der Datenbank der Anwendung.
let loadLastTargetChanges () : Async<Result<Map<Guid, TargetChangeMark>, string>> =
    loadLastTargetChangesWith Core.getConnection

/// Die Kategorien, deren Target BB führt (`TargetRules.leadsTarget`): jeder Target-Stream mit Übernahme oder einer
/// Änderung in BB, in einer Abfrage über `events`, ohne Payload.
let internal queryLedTargets (conn: SqliteConnection) : Result<Set<Guid>, string> =
    conn.Query<string>(
        """SELECT DISTINCT stream_id FROM events
           WHERE context = @context AND event_type IN @event_types""",
        {| context = Context; event_types = List.toArray EventTypes.targetRules |}
    )
    |> Seq.choose categoryOfTargetStream
    |> Set.ofSeq
    |> Ok

/// BBs Regeln aller Targets im Monat "YYYY-MM-01" (`TargetRules.ruleAt`), je Kategorie mit Target. Gelesen werden
/// nur Übernahme und Änderungen in BB, in einer Abfrage.
let internal queryTargetRules (conn: SqliteConnection) (monthKey: string) : Result<Map<Guid, Target>, string> =
    match monthIdOf monthKey with
    | Error message -> Error message
    | Ok monthId ->
        loadBudgetEventsOn conn (OfTypes EventTypes.targetRules)
        |> Result.map (fun events ->
            events
            |> List.groupBy (fun e -> e.StreamId)
            |> List.choose (fun (streamId, inStream) ->
                match categoryOfTargetStream streamId with
                | None -> None
                | Some categoryId ->
                    inStream
                    |> List.map (fun e -> e.Event)
                    |> Server.Budgeting.TargetRules.ruleAt monthId
                    |> Option.map (fun rule -> categoryId, rule))
            |> Map.ofList)

/// Die Meldung, wenn sich BBs Targets nicht lesen lassen.
[<Literal>]
let TargetRulesUnreadableText = "Targets nicht lesbar"

/// `queryTargetRules` über `connect`. Ein Lesefehler kommt als `Error` mit `TargetRulesUnreadableText`.
let loadTargetRulesWith (connect: unit -> SqliteConnection) (monthKey: string) : Async<Result<Map<Guid, Target>, string>> =
    readWith connect "Targets lesen" TargetRulesUnreadableText (fun conn -> queryTargetRules conn monthKey)

/// Die Ereignisse des Target-Streams einer Kategorie in Stream-Reihenfolge, für Target ändern.
let internal loadTargetStreamOn (conn: SqliteConnection) (categoryId: Guid) : Result<BudgetEvent list, string> =
    loadBudgetEventsOn conn (InStream (streamIdOf (TargetStream categoryId)))
    |> Result.map (List.map (fun e -> e.Event))

// ============================================
// Schreiben
// ============================================

/// Ausgang des Festhaltens einer Entscheidung mit Befehls-Id (ADR 0084 Punkt 8).
type Recorded<'T> =
    | Recorded of 'T
    /// Die Befehls-Id stand schon in `processed_commands`; es ist nichts entstanden. Trägt die Id, damit der
    /// Aufrufer die festgehaltene Antwort ohne Umweg über ein `Guid option` sucht.
    | CommandAlreadyProcessed of commandId: Guid * firstPosition: int64

/// Hängt Ereignisse an einen Stream an, eine Transaktion (ADR 0083 Punkt 3). Die Zeitpunkte müssen UTC
/// sein. Mit Befehls-Id prüft der Event Store in derselben Transaktion `processed_commands`.
let private appendCommandOn
    (conn: SqliteConnection)
    (commandId: Guid option)
    (stream: BudgetStream)
    (events: (DateTime * BudgetEvent) list)
    : Recorded<EventStore.RecordedEvent list> =
    if conn.State <> Data.ConnectionState.Open then
        conn.Open()
    let aggregate, id = streamParts stream
    let request : EventStore.AppendRequest =
        { Context = Context
          CommandId = commandId
          Streams =
            [ { Aggregate = aggregate
                Id = id
                Events = events |> List.map (fun (at, event) -> toNewEvent at event) } ] }
    use tx = conn.BeginTransaction()
    match EventStore.appendOn conn tx request EventStore.noReadModels with
    | EventStore.Appended recorded ->
        tx.Commit()
        Recorded recorded
    | EventStore.CommandAlreadyProcessed firstPosition ->
        match commandId with
        | Some id -> CommandAlreadyProcessed (id, firstPosition)
        | None -> failwith "Der Event Store meldet eine Befehls-Id als festgehalten, das Anhängen trug keine"

/// Hängt Ereignisse ohne Befehls-Id an, etwa die Ergebnis-Ereignisse einer Rückschreibung (ADR 0084
/// Punkt 8). Liefert die gespeicherten Ereignisse samt `event_id`.
let internal appendOn (conn: SqliteConnection) (stream: BudgetStream) (events: (DateTime * BudgetEvent) list) : EventStore.RecordedEvent list =
    match appendCommandOn conn None stream events with
    | Recorded recorded -> recorded
    | CommandAlreadyProcessed _ -> failwith "Ein Anhängen ohne Befehls-Id kann nicht schon festgehalten sein"

let private monthStreamOrFail (monthKey: string) : string * BudgetStream =
    match monthIdOf monthKey with
    | Ok monthId -> monthId, MonthStream monthId
    | Error message -> invalidArg "monthKey" message

/// Eine geplante Zeile einer Entscheidung, Beträge in Milliunits.
type PlannedLine = {
    CategoryId: Guid
    CategoryName: string
    OldAssigned: int64
    NewAssigned: int64
}

/// Das Entscheidungsereignis zu Herkunft und Zeilen; jede Zeile bekommt eine frische `lineId`.
/// Verschieben braucht genau zwei Zeilen (Quelle, Ziel), Zuweisen genau eine. Jede Kategorie darf nur
/// einmal vorkommen: die Rückschreib-Ergebnisse verweisen über Entscheidung und Kategorie auf ihre Zeile
/// (`latestWriteBacks`).
let internal decisionEvent
    (monthId: string)
    (origin: MovementOrigin)
    (lines: PlannedLine list)
    (skippedPaused: SkippedPausedLine list)
    : BudgetEvent =
    let categories = lines |> List.map (fun l -> l.CategoryId)
    if (List.distinct categories).Length <> categories.Length then
        invalidArg "lines" $"{originText origin}: eine Kategorie steht mehrfach in der Entscheidung"
    let line (l: PlannedLine) : BudgetLine =
        { LineId = Guid.NewGuid()
          CategoryId = l.CategoryId
          CategoryName = l.CategoryName
          PreviousAssigned = l.OldAssigned
          Assigned = l.NewAssigned }
    match origin, lines with
    | TargetsApplied, _ ->
        BudgetEvent.TargetsApplied { Month = monthId; Lines = lines |> List.map line; SkippedPaused = skippedPaused }
    | MoneyMoved, [ from; target ] ->
        BudgetEvent.MoneyMoved
            { Month = monthId
              From = line from
              To = line target
              Amount = from.OldAssigned - from.NewAssigned }
    | MoneyAssigned, [ single ] -> BudgetEvent.AssignmentChangedInBb { Month = monthId; Line = line single }
    | _ -> invalidArg "lines" $"{originText origin}: {lines.Length} Zeilen passen nicht zur Herkunft"

/// Hält eine Entscheidung als ein Ereignis im Monats-Stream fest (ADR 0084 Punkt 4). Erst danach darf der
/// erste PATCH nach YNAB gehen (ADR 0050). Liefert die Zeilen, ihre Gruppen-Id ist die `event_id`. Das
/// Ereignis trägt die Befehls-Id; ist sie schon festgehalten, entsteht nichts. `withdrawn` sind die offenen
/// Rückschreibungen, die die Entscheidung ersetzt (budgeting-dgrp7): je eine `AssignmentWriteBackWithdrawn`
/// im selben Append hinter der Entscheidung, damit `first_position` der Befehls-Id auf die Entscheidung zeigt.
let internal recordDecisionOn
    (conn: SqliteConnection)
    (commandId: Guid option)
    (at: DateTime)
    (monthKey: string)
    (origin: MovementOrigin)
    (lines: PlannedLine list)
    (skippedPaused: SkippedPausedLine list)
    (withdrawn: WriteBackRef list)
    : Recorded<Movement list> =
    let monthId, stream = monthStreamOrFail monthKey
    let event = decisionEvent monthId origin lines skippedPaused
    let withdrawals = withdrawn |> List.map (fun r -> at, BudgetEvent.AssignmentWriteBackWithdrawn r)
    match appendCommandOn conn commandId stream ((at, event) :: withdrawals) with
    | Recorded (recorded :: _) ->
        Recorded (
            movementsOf
                { EventId = recorded.EventId
                  StreamId = recorded.StreamId
                  StreamVersion = recorded.StreamVersion
                  OccurredAt = at
                  Event = event }
        )
    | Recorded [] -> failwith "Ein Entscheidungsereignis erwartet, keins gespeichert"
    | CommandAlreadyProcessed (id, firstPosition) -> CommandAlreadyProcessed (id, firstPosition)

/// `recordDecisionOn` über `connect`.
let recordDecisionWith
    (connect: unit -> SqliteConnection)
    (commandId: Guid option)
    (at: DateTime)
    (monthKey: string)
    (origin: MovementOrigin)
    (lines: PlannedLine list)
    (skippedPaused: SkippedPausedLine list)
    (withdrawn: WriteBackRef list)
    : Async<Recorded<Movement list>> =
    async {
        use conn = connect ()
        return recordDecisionOn conn commandId at monthKey origin lines skippedPaused withdrawn
    }

/// `recordDecisionOn` auf der Datenbank der Anwendung.
let recordDecision
    (commandId: Guid option)
    (at: DateTime)
    (monthKey: string)
    (origin: MovementOrigin)
    (lines: PlannedLine list)
    (skippedPaused: SkippedPausedLine list)
    (withdrawn: WriteBackRef list)
    : Async<Recorded<Movement list>> =
    recordDecisionWith Core.getConnection commandId at monthKey origin lines skippedPaused withdrawn

/// Hängt den Ausgang einer Zeile als Ergebnis-Ereignis an den Monats-Stream ihrer Entscheidung.
let internal recordWriteBackOn (conn: SqliteConnection) (m: Movement) (at: DateTime) (outcome: WriteBackOutcome) : unit =
    let _, stream = monthStreamOrFail m.Month
    let reference = { DecisionEventId = m.ApplicationId; CategoryId = m.CategoryId }
    appendOn conn stream [ at, writeBackEvent reference outcome ] |> ignore

/// Die Ereignisse des Monats-Streams zu "YYYY-MM-01" in Stream-Reihenfolge, je mit `event_id`, für die
/// Faltung (budgeting-dgrp7). Ein nicht lesbares Ereignis wird gemeldet.
let internal loadMonthEventsOn (conn: SqliteConnection) (monthKey: string) : Result<(Guid * BudgetEvent) list, string> =
    match monthIdOf monthKey with
    | Error message -> Error message
    | Ok monthId ->
        loadBudgetEventsOn conn (InStream (streamIdOf (MonthStream monthId)))
        |> Result.map (List.map (fun e -> e.EventId, e.Event))

/// Die übernommenen Monate als "YYYY-MM-01": jeder Monats-Stream mit einem `BudgetTakenOverFromYnab`, in einer
/// Abfrage über `events` (budgeting-dkjc5). Gelesen wird nur `stream_id`, keine Payload.
let internal queryTakenOverMonths (conn: SqliteConnection) : Result<Set<string>, string> =
    conn.Query<string>(
        """SELECT DISTINCT stream_id FROM events
           WHERE context = @context AND event_type = @event_type""",
        {| context = Context; event_type = EventTypes.takenOver |}
    )
    |> Seq.choose monthOfMonthStream
    |> Seq.map monthKeyOfId
    |> Set.ofSeq
    |> Ok

/// Liegt im Monats-Stream zu "YYYY-MM-01" schon ein `BudgetTakenOverFromYnab`? Eine gezielte Abfrage über
/// `events`, ohne Payload: ein nicht dekodierbares Ereignis im selben Stream stört die Antwort nicht.
let internal queryMonthTakenOver (conn: SqliteConnection) (monthKey: string) : Result<bool, string> =
    match monthIdOf monthKey with
    | Error message -> Error message
    | Ok monthId ->
        conn.ExecuteScalar<int64>(
            """SELECT EXISTS (
                   SELECT 1 FROM events
                   WHERE context = @context AND stream_id = @stream_id AND event_type = @event_type)""",
            {| context = Context
               stream_id = streamIdOf (MonthStream monthId)
               event_type = EventTypes.takenOver |}
        )
        |> (=) 1L
        |> Ok

/// Die Meldung, wenn sich die übernommenen Monate nicht lesen lassen.
[<Literal>]
let TakenOverMonthsUnreadableText = "Übernommene Monate nicht lesbar"

/// `queryTakenOverMonths` über `connect`.
let loadTakenOverMonthsWith (connect: unit -> SqliteConnection) : Async<Result<Set<string>, string>> =
    readWith connect "Übernommene Monate lesen" TakenOverMonthsUnreadableText queryTakenOverMonths

/// `queryTakenOverMonths` auf der Datenbank der Anwendung.
let loadTakenOverMonths () : Async<Result<Set<string>, string>> =
    loadTakenOverMonthsWith Core.getConnection

/// Hängt Übernahme und Beobachtungen ohne Befehls-Id an den Monats-Stream, in einer Transaktion. Eine leere
/// Liste hängt nichts an.
let internal appendMonthEventsOn (conn: SqliteConnection) (at: DateTime) (monthKey: string) (events: BudgetEvent list) : unit =
    if not (List.isEmpty events) then
        let _, stream = monthStreamOrFail monthKey
        appendOn conn stream (events |> List.map (fun e -> at, e)) |> ignore

/// `recordWriteBackOn` über `connect`.
let recordWriteBackWith (connect: unit -> SqliteConnection) (m: Movement) (at: DateTime) (outcome: WriteBackOutcome) : Async<unit> =
    async {
        use conn = connect ()
        recordWriteBackOn conn m at outcome
    }

/// `recordWriteBackOn` auf der Datenbank der Anwendung.
let recordWriteBack (m: Movement) (at: DateTime) (outcome: WriteBackOutcome) : Async<unit> =
    recordWriteBackWith Core.getConnection m at outcome

/// Hält ein Pause-Ereignis im Monats-Stream fest. Liefert seine `event_id`. Das Ereignis trägt die
/// Befehls-Id; ist sie schon festgehalten, entsteht nichts.
let internal recordPauseOn
    (conn: SqliteConnection)
    (commandId: Guid option)
    (at: DateTime)
    (monthKey: string)
    (categoryId: Guid)
    (categoryName: string)
    (kind: PauseKind)
    : Recorded<Guid> =
    let monthId, stream = monthStreamOrFail monthKey
    let pause = { Month = monthId; CategoryId = categoryId; CategoryName = categoryName }
    let event =
        match kind with
        | PauseKind.Paused -> BudgetEvent.TargetPaused pause
        | PauseKind.PauseLifted -> BudgetEvent.TargetPauseLifted pause
    match appendCommandOn conn commandId stream [ at, event ] with
    | Recorded recorded -> Recorded (List.exactlyOne recorded).EventId
    | CommandAlreadyProcessed (id, firstPosition) -> CommandAlreadyProcessed (id, firstPosition)

/// `recordPauseOn` über `connect`.
let recordPauseWith
    (connect: unit -> SqliteConnection)
    (commandId: Guid option)
    (at: DateTime)
    (monthKey: string)
    (categoryId: Guid)
    (categoryName: string)
    (kind: PauseKind)
    : Async<Recorded<Guid>> =
    async {
        use conn = connect ()
        return recordPauseOn conn commandId at monthKey categoryId categoryName kind
    }

/// `recordPauseOn` auf der Datenbank der Anwendung.
let recordPause
    (commandId: Guid option)
    (at: DateTime)
    (monthKey: string)
    (categoryId: Guid)
    (categoryName: string)
    (kind: PauseKind)
    : Async<Recorded<Guid>> =
    recordPauseWith Core.getConnection commandId at monthKey categoryId categoryName kind

/// Hält eine Target-Änderung in BB (`TargetSet` oder `TargetRemoved`) im Target-Stream der Kategorie fest
/// (budgeting-eh8xe). Das Ereignis trägt die Befehls-Id; ist sie schon festgehalten, entsteht nichts.
let internal recordTargetChangeOn
    (conn: SqliteConnection)
    (commandId: Guid option)
    (at: DateTime)
    (categoryId: Guid)
    (event: BudgetEvent)
    : Recorded<Guid> =
    match appendCommandOn conn commandId (TargetStream categoryId) [ at, event ] with
    | Recorded recorded -> Recorded (List.exactlyOne recorded).EventId
    | CommandAlreadyProcessed (id, firstPosition) -> CommandAlreadyProcessed (id, firstPosition)

/// Hält die Übernahme der Targets fest (ADR 0084 Punkt 5): je Kategorie ein Ereignis in ihrem Target-Stream, alle
/// in einer Transaktion. Eine leere Liste hängt nichts an.
let internal appendTargetTakeoverOn (conn: SqliteConnection) (at: DateTime) (events: (Guid * BudgetEvent) list) : unit =
    if not (List.isEmpty events) then
        if conn.State <> Data.ConnectionState.Open then
            conn.Open()
        let request : EventStore.AppendRequest =
            { Context = Context
              CommandId = None
              Streams =
                events
                |> List.map (fun (categoryId, event) ->
                    let aggregate, id = streamParts (TargetStream categoryId)
                    { Aggregate = aggregate
                      Id = id
                      Events = [ toNewEvent at event ] }) }
        use tx = conn.BeginTransaction()
        match EventStore.appendOn conn tx request EventStore.noReadModels with
        | EventStore.Appended _ -> tx.Commit()
        | EventStore.CommandAlreadyProcessed _ -> failwith "Eine Übernahme ohne Befehls-Id kann nicht schon festgehalten sein"
