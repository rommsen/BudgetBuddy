/// Die Budget-Ereignisse in `events` lesen und schreiben (Context `budgeting`, ADR 0083, ADR 0084,
/// budgeting-hyn77). Entscheidungen (Zuweisen, Verschieben, Targets anwenden), Pausen und ihre
/// Rückschreib-Ergebnisse liegen im Monats-Stream, Target-Änderungen samt Schritt-Ergebnissen im
/// Target-Stream der Kategorie. Jeder Leser filtert auf `context = 'budgeting'` und sortiert fachlich
/// (`occurred_at`, `stream_version`), nie nach `position`. Ein unbekannter Ereignistyp im Kontext wird
/// gemeldet, nicht übersprungen. `underfunded_comparisons` bleibt Zustand.
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

let private dateToText (d: DateTime) = d.ToString("o", CultureInfo.InvariantCulture)

let private orDbNull (value: 'a option) : obj =
    match value with
    | Some v -> box v
    | None -> null

/// Legt `events` (ADR 0083) und `underfunded_comparisons` an und überführt die früheren Budget-Tabellen
/// nach `events` (budgeting-hyn77). Wiederholbar. Läuft beim Start hinter
/// `Ledger.Persistence.initializeLedgerSchema`. Bricht die Migration ab, wirft der Aufruf und BB startet
/// nicht.
let initializeBudgetingSchema () =
    use conn = Core.getConnection ()
    EventStore.createSchemaOn conn
    conn.Execute(
        """
        CREATE TABLE IF NOT EXISTS underfunded_comparisons (
            month TEXT NOT NULL,
            category_id TEXT NOT NULL,
            category_name TEXT NOT NULL,
            kind TEXT NOT NULL,
            rule TEXT,
            outcome TEXT NOT NULL,
            bb_milliunits INTEGER,
            ynab_milliunits INTEGER,
            reason TEXT,
            paused INTEGER NOT NULL,
            compared_at TEXT NOT NULL,
            PRIMARY KEY (month, category_id)
        );
        """
    )
    |> ignore
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

/// Neueste zuerst: Zeitpunkt, dann Stream und Version, in einer Entscheidung die spätere Zeile zuerst.
let private newestFirst (events: RecordedBudgetEvent list) : (RecordedBudgetEvent * Movement) list =
    events
    |> List.sortWith (fun a b ->
        match compare b.OccurredAt a.OccurredAt with
        | 0 ->
            match compare b.StreamId a.StreamId with
            | 0 -> compare b.StreamVersion a.StreamVersion
            | c -> c
        | c -> c)
    |> List.collect (fun e -> movementsOf e |> List.rev |> List.map (fun m -> e, m))

/// Die Zeilen zum Filter, neueste zuerst, je mit dem jüngsten Ergebnis und bei einer Verschiebung mit
/// der Kategorie der Gegenseite. `Error` nennt ein nicht lesbares Ereignis.
let queryMovements (conn: SqliteConnection) (filter: MovementFilter) : Result<HistoryMovement list, string> =
    // Ohne Monat reichen die Entscheidungen und ihre Rückschreib-Ergebnisse, Pausen und Target-Änderungen
    // liest die Historie nicht.
    let scope =
        match filter.Month with
        | None -> Some (OfTypes (EventTypes.decisions @ EventTypes.writeBacks))
        | Some month -> monthStreamOf month |> Option.map InStream
    match scope with
    | None -> Ok []
    | Some scope ->
        loadBudgetEventsOn conn scope
        |> Result.map (fun events ->
            let latest = latestWriteBacks events
            let counterpart (e: RecordedBudgetEvent) (m: Movement) =
                match e.Event with
                | BudgetEvent.MoneyMoved p ->
                    [ p.From; p.To ] |> List.tryFind (fun l -> l.LineId <> m.Id) |> Option.map (fun l -> l.CategoryName)
                | _ -> None
            newestFirst events
            |> List.filter (fun (_, m) ->
                (filter.CategoryId |> Option.forall ((=) m.CategoryId))
                && (filter.Month |> Option.forall ((=) m.Month))
                && (filter.Origin |> Option.forall ((=) m.Origin)))
            |> List.map (fun (e, m) ->
                { MovementId = m.Id
                  ApplicationId = m.ApplicationId
                  OccurredAt = m.OccurredAt
                  Origin = m.Origin
                  Month = m.Month
                  CategoryId = m.CategoryId
                  CategoryName = m.CategoryName
                  OldAssigned = euroOfMilliunits m.OldAssigned
                  NewAssigned = euroOfMilliunits m.NewAssigned
                  Counterpart = counterpart e m
                  Latest = Map.tryFind (m.ApplicationId, m.CategoryId) latest }))

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
// Target ändern und entfernen (budgeting-k8wq3, ADR 0080)
// ============================================

/// Eine festzuhaltende Target-Änderung. Beträge in Milliunits, Datum "YYYY-MM-DD". `None`, wo kein
/// Target oder kein Datum steht.
type TargetChange = {
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

let private goalChangesOf (events: RecordedBudgetEvent list) : (RecordedBudgetEvent * YnabGoalChange) list =
    events
    |> List.choose (fun e ->
        match e.Event with
        | BudgetEvent.YnabGoalChangedFromBb p -> Some (e, p)
        | _ -> None)

/// Die Target-Änderungen einer Kategorie im Monat für die Historie, neueste zuerst nach
/// `stream_version`, je mit dem jüngsten Ergebnis von Schritt 1 und von Schritt 2.
let queryTargetChanges (conn: SqliteConnection) (categoryId: Guid) (month: string) : Result<HistoryTargetChange list, string> =
    match monthIdOf month with
    | Error _ -> Ok []
    | Ok monthId ->
        loadBudgetEventsOn conn (InStream (streamIdOf (TargetStream categoryId)))
        |> Result.map (fun events ->
            let steps = latestGoalSteps events
            goalChangesOf events
            |> List.filter (fun (_, p) -> p.Month = monthId)
            |> List.rev
            |> List.map (fun (e, p) ->
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
                  Latest = Map.tryFind (e.EventId, 1) steps
                  DateStep = Map.tryFind (e.EventId, 2) steps }))

/// Die Meldung, wenn die Target-Änderungen für die Historie nicht lesbar sind.
[<Literal>]
let TargetChangesUnreadableText = "Target-Änderungen nicht lesbar"

/// Die Target-Änderungen für die Historie über `connect`. Ein Lesefehler kommt als `Error` mit Text.
let loadTargetChangesWith (connect: unit -> SqliteConnection) (categoryId: Guid) (month: string) : Async<Result<HistoryTargetChange list, string>> =
    readWith connect "Target-Änderungen lesen" TargetChangesUnreadableText (fun conn -> queryTargetChanges conn categoryId month)

/// Die Target-Änderungen für die Historie. Liest nur die Datenbank, kein YNAB.
let loadTargetChanges (categoryId: Guid) (month: string) : Async<Result<HistoryTargetChange list, string>> =
    loadTargetChangesWith Core.getConnection categoryId month

/// Je Kategorie die jüngste Target-Änderung, deren jüngstes Ergebnis von Schritt 1 geschrieben ist (Targets
/// prüfen, budgeting-p2zr6, ADR 0082 Punkt 7). Jüngste nach `occurred_at`, bei gleichem Zeitpunkt nach
/// `stream_version`. Der Tag ist der Berliner Kalendertag "YYYY-MM-DD" (`berlinTime`).
let queryLastTargetChanges (conn: SqliteConnection) : Result<Map<Guid, TargetChangeMark>, string> =
    loadBudgetEventsOn conn (OfTypes EventTypes.goalChanges)
    |> Result.map (fun events ->
        let steps = latestGoalSteps events
        goalChangesOf events
        |> List.choose (fun (e, p) ->
            match categoryOfTargetStream e.StreamId, Map.tryFind (e.EventId, 1) steps with
            | Some categoryId, Some Written -> Some (categoryId, e, p)
            | _ -> None)
        |> List.groupBy (fun (categoryId, _, _) -> categoryId)
        |> List.map (fun (categoryId, rows) ->
            let _, e, p = rows |> List.maxBy (fun (_, e, _) -> e.OccurredAt, e.StreamVersion)
            let day = (berlinTime e.OccurredAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            let mark = if p.ChangeKind = TargetRemoved then TargetRemovedOn day else TargetSetOn day
            categoryId, mark)
        |> Map.ofList)

/// Die letzten Target-Änderungen aller Kategorien für Targets prüfen über die Verbindung aus `connect`.
/// Ein Lesefehler kommt als `Error` mit `TargetChangesUnreadableText`, die Ursache bleibt im Server-Log.
let loadLastTargetChangesWith (connect: unit -> SqliteConnection) : Async<Result<Map<Guid, TargetChangeMark>, string>> =
    readWith connect "Letzte Target-Änderungen lesen" TargetChangesUnreadableText queryLastTargetChanges

/// `loadLastTargetChangesWith` auf der Datenbank der Anwendung.
let loadLastTargetChanges () : Async<Result<Map<Guid, TargetChangeMark>, string>> =
    loadLastTargetChangesWith Core.getConnection

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
/// Ereignis trägt die Befehls-Id; ist sie schon festgehalten, entsteht nichts.
let internal recordDecisionOn
    (conn: SqliteConnection)
    (commandId: Guid option)
    (at: DateTime)
    (monthKey: string)
    (origin: MovementOrigin)
    (lines: PlannedLine list)
    (skippedPaused: SkippedPausedLine list)
    : Recorded<Movement list> =
    let monthId, stream = monthStreamOrFail monthKey
    let event = decisionEvent monthId origin lines skippedPaused
    match appendCommandOn conn commandId stream [ at, event ] with
    | Recorded [ recorded ] ->
        Recorded (
            movementsOf
                { EventId = recorded.EventId
                  StreamId = recorded.StreamId
                  StreamVersion = recorded.StreamVersion
                  OccurredAt = at
                  Event = event }
        )
    | Recorded other -> failwith $"Ein Entscheidungsereignis erwartet, {other.Length} gespeichert"
    | CommandAlreadyProcessed (id, firstPosition) -> CommandAlreadyProcessed (id, firstPosition)

/// `recordDecisionOn` auf der Datenbank der Anwendung.
let recordDecision
    (commandId: Guid option)
    (at: DateTime)
    (monthKey: string)
    (origin: MovementOrigin)
    (lines: PlannedLine list)
    (skippedPaused: SkippedPausedLine list)
    : Async<Recorded<Movement list>> =
    async {
        use conn = Core.getConnection ()
        return recordDecisionOn conn commandId at monthKey origin lines skippedPaused
    }

/// Hängt den Ausgang einer Zeile als Ergebnis-Ereignis an den Monats-Stream ihrer Entscheidung.
let internal recordWriteBackOn (conn: SqliteConnection) (m: Movement) (at: DateTime) (outcome: WriteBackOutcome) : unit =
    let _, stream = monthStreamOrFail m.Month
    let reference = { DecisionEventId = m.ApplicationId; CategoryId = m.CategoryId }
    appendOn conn stream [ at, writeBackEvent reference outcome ] |> ignore

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

/// `recordPauseOn` auf der Datenbank der Anwendung.
let recordPause
    (commandId: Guid option)
    (at: DateTime)
    (monthKey: string)
    (categoryId: Guid)
    (categoryName: string)
    (kind: PauseKind)
    : Async<Recorded<Guid>> =
    async {
        use conn = Core.getConnection ()
        return recordPauseOn conn commandId at monthKey categoryId categoryName kind
    }

/// Hält eine Target-Änderung im Target-Stream der Kategorie fest (ADR 0080 Punkt 4). Erst danach darf der
/// PATCH hinausgehen. Liefert die `event_id`, auf sie verweisen die Schritt-Ergebnisse.
let internal recordTargetChangeOn (conn: SqliteConnection) (change: TargetChange) : Guid =
    let monthId, _ = monthStreamOrFail change.Month
    let event =
        BudgetEvent.YnabGoalChangedFromBb
            { Month = monthId
              CategoryName = change.CategoryName
              ChangeKind = change.Kind
              OldGoalTarget = change.OldGoalTarget
              NewGoalTarget = change.NewGoalTarget
              OldDate = change.OldDate
              NewDate = change.NewDate }
    (appendOn conn (TargetStream change.CategoryId) [ change.OccurredAt, event ] |> List.exactlyOne).EventId

/// `recordTargetChangeOn` auf der Datenbank der Anwendung.
let recordTargetChange (change: TargetChange) : Async<Guid> =
    async {
        use conn = Core.getConnection ()
        return recordTargetChangeOn conn change
    }

/// Hängt das Ergebnis eines Schritts (1 oder 2) einer Target-Änderung an den Target-Stream an.
let internal recordTargetChangeStepResultOn
    (conn: SqliteConnection)
    (categoryId: Guid)
    (changeId: Guid)
    (step: int)
    (at: DateTime)
    (outcome: LineOutcome)
    : unit =
    let event = goalStepEvent { DecisionEventId = changeId; Step = step } outcome
    appendOn conn (TargetStream categoryId) [ at, event ] |> ignore

/// `recordTargetChangeStepResultOn` auf der Datenbank der Anwendung.
let recordTargetChangeStepResult (categoryId: Guid) (changeId: Guid) (step: int) (at: DateTime) (outcome: LineOutcome) : Async<unit> =
    async {
        use conn = Core.getConnection ()
        recordTargetChangeStepResultOn conn categoryId changeId step at outcome
    }

// ============================================
// Abweichungsliste (budgeting-v7kfw, ADR 0084 Punkt 7)
// ============================================

let private milliunitsOf (money: Shared.Domain.Money option) : obj =
    money |> Option.map toMilliunits |> orDbNull

/// Ersetzt die Abweichungsliste des Monats `month` in einer Transaktion: erst alle Zeilen des Monats
/// löschen, dann die neuen einfügen. So fällt eine Kategorie ohne Target heraus, andere Monate bleiben.
/// Vorläufiger Zustand nach ADR 0083 (kein Read Model), entfällt mit budgeting-eh8xe.
let saveUnderfundedComparisonsWith
    (connect: unit -> SqliteConnection)
    (comparedAt: DateTime)
    (month: string)
    (rows: UnderfundedComparison list)
    : Async<unit> =
    async {
        use conn = connect ()
        conn.Open()
        use tx = conn.BeginTransaction()
        conn.Execute("DELETE FROM underfunded_comparisons WHERE month = @month", {| month = month |}, tx) |> ignore
        let parameters =
            rows
            |> List.map (fun r ->
                {| month = month
                   category_id = string r.CategoryId
                   category_name = r.CategoryName
                   kind = goalTypeText r.Kind
                   rule = r.Rule |> Option.map underfundedRuleText |> orDbNull
                   outcome = comparisonOutcomeText r.Outcome
                   bb_milliunits = milliunitsOf r.Bb
                   ynab_milliunits = milliunitsOf r.Ynab
                   reason = orDbNull r.Reason
                   paused = (if r.Paused then 1L else 0L)
                   compared_at = dateToText comparedAt |})
            |> Array.ofList

        // Dapper führt das INSERT einmal je Element des Arrays aus, alles in derselben Transaktion.
        if parameters.Length > 0 then
            conn.Execute(
                "INSERT INTO underfunded_comparisons (month, category_id, category_name, kind, rule, outcome, \
                    bb_milliunits, ynab_milliunits, reason, paused, compared_at) \
                 VALUES (@month, @category_id, @category_name, @kind, @rule, @outcome, \
                    @bb_milliunits, @ynab_milliunits, @reason, @paused, @compared_at)",
                parameters,
                tx
            )
            |> ignore
        tx.Commit()
    }

/// `saveUnderfundedComparisonsWith` auf der Datenbank der Anwendung, Zeitpunkt ist jetzt (UTC).
let saveUnderfundedComparisons (month: string) (rows: UnderfundedComparison list) : Async<unit> =
    saveUnderfundedComparisonsWith Core.getConnection DateTime.UtcNow month rows
