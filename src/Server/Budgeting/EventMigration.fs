/// Startmigration der früheren Budget-Tabellen nach `events` (ADR 0084 Punkt 4, budgeting-hyn77).
///
/// Die fünf Tabellen `budget_movements`, `budget_movement_results`, `target_pause_events`,
/// `target_changes` und `target_change_results` gehen in einer Transaktion nach `events` und werden
/// danach entfernt. Vorbild ist die Kassenbuch-Migration (ledger-k9tm2): Kopie per SQL-Parameter,
/// `occurred_at` als unveränderter Text, kein `EventStore.validate`. Nur dieses Modul, die Probe und
/// ihre Tests nennen die alten Tabellen.
module Budgeting.EventMigration

open System
open System.Collections.Generic
open Microsoft.Data.Sqlite
open Dapper
open Shared.Budgeting
open Server.Budgeting.Domain
open Budgeting.Events

/// Die fünf früheren Tabellen.
let legacyTables =
    [ "budget_movements"
      "budget_movement_results"
      "target_pause_events"
      "target_changes"
      "target_change_results" ]

/// Welche der früheren Tabellen es auf der Verbindung gibt.
let internal existingLegacyTables (conn: SqliteConnection) (tx: SqliteTransaction) : Set<string> =
    conn.Query<string>(
        "SELECT name FROM sqlite_master WHERE type = 'table' AND name IN @names",
        {| names = legacyTables |> List.toArray |},
        tx
    )
    |> Set.ofSeq

// ============================================
// Spaltentexte des Altbestands
// ============================================

/// Der frühere Text der Pause-Art in `target_pause_events.kind`.
let pauseKindText (kind: PauseKind) : string =
    match kind with
    | PauseKind.Paused -> "Pausiert"
    | PauseKind.PauseLifted -> "Aufgehoben"

/// Der noch ältere Text für `PauseLifted`. Lokale Dev-Datenbanken tragen ihn noch.
[<Literal>]
let LegacyPauseLiftedText = "Fortgesetzt"

/// Liest die Spalte. Ein unbekannter Text wird gemeldet, nicht auf einen Standard gelegt.
let pauseKindOfText (text: string) : Result<PauseKind, string> =
    match [ PauseKind.Paused; PauseKind.PauseLifted ] |> List.tryFind (fun k -> pauseKindText k = text) with
    | Some kind -> Ok kind
    | None when text = LegacyPauseLiftedText -> Ok PauseKind.PauseLifted
    | None -> Error $"Unbekannte Art eines Pause-Ereignisses: {text}"

/// Die früheren Texte von Art und Rhythmus in `target_changes.change_kind` und `new_frequency`. Den
/// Rhythmus schrieb die Spalte mit denselben Texten wie die Payload (`Codec.frequencyText`).
let targetChangeKindText (kind: TargetChangeKind) : string * string option =
    match kind with
    | TargetEdited -> "Geändert", None
    | TargetRemoved -> "Entfernt", None
    | TargetCreated f -> "Angelegt", Some (Codec.frequencyText f)
    | TargetKindChanged f -> "Art gewechselt", Some (Codec.frequencyText f)

/// Liest beide Spalten. Ein unbekannter Text oder ein fehlender Rhythmus wird gemeldet, nicht auf einen
/// Standard gelegt.
let targetChangeKindOfText (text: string) (frequency: string option) : Result<TargetChangeKind, string> =
    let frequencyOf () =
        match frequency |> Option.bind Codec.frequencyOfText with
        | Some f -> Ok f
        | None ->
            let seen = frequency |> Option.defaultValue "keiner"
            Error $"Unbekannter Rhythmus einer Target-Änderung: {seen}"
    match text with
    | "Geändert" -> Ok TargetEdited
    | "Entfernt" -> Ok TargetRemoved
    | "Angelegt" -> frequencyOf () |> Result.map TargetCreated
    | "Art gewechselt" -> frequencyOf () |> Result.map TargetKindChanged
    | _ -> Error $"Unbekannte Art einer Target-Änderung: {text}"

// ============================================
// Zeilen des Altbestands
// ============================================

[<CLIMutable>]
type LegacyMovementRow = {
    row_id: int64
    id: string
    application_id: string
    occurred_at: string
    origin: string
    month: string
    category_id: string
    category_name: string
    old_assigned: int64
    new_assigned: int64
}

[<CLIMutable>]
type LegacyMovementResultRow = {
    row_id: int64
    movement_id: string
    occurred_at: string
    status: string
    reason: string
}

[<CLIMutable>]
type LegacyPauseRow = {
    row_id: int64
    id: string
    occurred_at: string
    month: string
    category_id: string
    category_name: string
    kind: string
}

[<CLIMutable>]
type LegacyTargetChangeRow = {
    row_id: int64
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
}

[<CLIMutable>]
type LegacyTargetChangeResultRow = {
    row_id: int64
    change_id: string
    occurred_at: string
    status: string
    reason: string
    step: int64 option
}

/// Der ganze Altbestand in rowid-Reihenfolge. Eine fehlende Tabelle ist leer.
type LegacyBudget = {
    Movements: LegacyMovementRow list
    MovementResults: LegacyMovementResultRow list
    Pauses: LegacyPauseRow list
    TargetChanges: LegacyTargetChangeRow list
    TargetChangeResults: LegacyTargetChangeResultRow list
}

let internal loadLegacyOn (conn: SqliteConnection) (tx: SqliteTransaction) : LegacyBudget =
    let present = existingLegacyTables conn tx
    let load (table: string) (query: unit -> 'T seq) : 'T list =
        if present.Contains table then query () |> Seq.toList else []
    { Movements =
        load
            "budget_movements"
            (fun () -> conn.Query<LegacyMovementRow>(
                "SELECT rowid AS row_id, id, application_id, occurred_at, origin, month, category_id, category_name, \
                    old_assigned, new_assigned FROM budget_movements ORDER BY rowid",
                transaction = tx))
      MovementResults =
        load
            "budget_movement_results"
            (fun () -> conn.Query<LegacyMovementResultRow>(
                "SELECT rowid AS row_id, movement_id, occurred_at, status, reason FROM budget_movement_results ORDER BY rowid",
                transaction = tx))
      Pauses =
        load
            "target_pause_events"
            (fun () -> conn.Query<LegacyPauseRow>(
                "SELECT rowid AS row_id, id, occurred_at, month, category_id, category_name, kind \
             FROM target_pause_events ORDER BY rowid",
                transaction = tx))
      TargetChanges =
        load
            "target_changes"
            (fun () -> conn.Query<LegacyTargetChangeRow>(
                "SELECT rowid AS row_id, id, occurred_at, month, category_id, category_name, change_kind, \
                    old_goal_target, new_goal_target, old_date, new_date, new_frequency \
             FROM target_changes ORDER BY rowid",
                transaction = tx))
      TargetChangeResults =
        load
            "target_change_results"
            (fun () -> conn.Query<LegacyTargetChangeResultRow>(
                "SELECT rowid AS row_id, change_id, occurred_at, status, reason, step \
             FROM target_change_results ORDER BY rowid",
                transaction = tx)) }

// ============================================
// Abbildung
// ============================================

/// Der Abbruch: die Transaktion rollt zurück, BB startet nicht. Die Meldung nennt Tabelle, Zeile und Wert.
let private abort (table: string) (rowId: int64) (detail: string) : 'a =
    failwith $"Budget-Migration abgebrochen: Tabelle {table}, Zeile {rowId}: {detail}"

let private guidOf (table: string) (rowId: int64) (column: string) (text: string) : Guid =
    match Guid.TryParseExact((if isNull text then "" else text), "D") with
    | true, g when g.ToString "D" = text -> g
    | _ -> abort table rowId $"{column} '{text}' ist keine Guid im Format D"

let private monthOf (table: string) (rowId: int64) (text: string) : string =
    match monthIdOf text with
    | Ok monthId -> monthId
    | Error message -> abort table rowId message

/// Ein Ereignis vor dem Einfügen, mit dem Sortierschlüssel aus ADR 0084 Punkt 4.
type private Pending = {
    OccurredAt: string
    /// 0 Entscheidung, 1 Ergebnis: bei gleichem Zeitpunkt die Entscheidung zuerst.
    Rank: int
    /// Feste Reihenfolge der Tabellen, nur damit gleiche rowids verschiedener Tabellen eindeutig ordnen.
    TableRank: int
    RowId: int64
    EventId: string
    StreamId: string
    Event: BudgetEvent
}

let private writeBackOfStatus (table: string) (rowId: int64) (status: string) (reason: string) : WriteBackOutcome =
    match status with
    | "written" -> WrittenBack
    | "failed" -> WriteBackFailed (if isNull reason then "" else reason)
    // Ein ungesendet geschlossenes Ziel ist im Altbestand nicht von "in YNAB geändert" zu unterscheiden.
    | "changed-in-ynab" -> SupersededInYnab
    | other -> abort table rowId $"unbekannter Status '{other}'"

let private decisions (rows: LegacyMovementRow list) : Pending list * Map<string, Guid * Guid * string> =
    let table = "budget_movements"
    for r in rows do
        guidOf table r.row_id "id" r.id |> ignore
        guidOf table r.row_id "application_id" r.application_id |> ignore
        guidOf table r.row_id "category_id" r.category_id |> ignore
        monthOf table r.row_id r.month |> ignore
        match originOfText r.origin with
        | Ok _ -> ()
        | Error _ -> abort table r.row_id $"unbekannte Herkunft '{r.origin}'"
    let line (r: LegacyMovementRow) : BudgetLine =
        { LineId = Guid.Parse r.id
          CategoryId = Guid.Parse r.category_id
          CategoryName = r.category_name
          PreviousAssigned = r.old_assigned
          Assigned = r.new_assigned }
    let pending =
        rows
        |> List.groupBy (fun r -> r.application_id)
        |> List.map (fun (applicationId, group) ->
            let first = List.head group
            let differing (value: LegacyMovementRow -> string) (what: string) =
                match group |> List.tryFind (fun r -> value r <> value first) with
                | Some r -> abort table r.row_id $"Anwendung {applicationId} über mehrere {what}: '{value first}' und '{value r}'"
                | None -> ()
            differing (fun r -> r.month) "Monate"
            differing (fun r -> r.origin) "Herkünfte"
            differing (fun r -> r.occurred_at) "Zeitpunkte"
            match group |> List.countBy (fun r -> r.category_id) |> List.tryFind (fun (_, n) -> n > 1) with
            | Some (categoryId, _) -> abort table first.row_id $"Anwendung {applicationId} trägt die Kategorie {categoryId} mehrfach"
            | None -> ()
            let monthId = monthOf table first.row_id first.month
            let event =
                match originOfText first.origin with
                | Ok TargetsApplied ->
                    BudgetEvent.TargetsApplied { Month = monthId; Lines = group |> List.map line; SkippedPaused = [] }
                | Ok MoneyMoved ->
                    if group.Length <> 2 then
                        abort table first.row_id $"Verschieben {applicationId} hat {group.Length} Zeilen statt 2"
                    match group |> List.partition (fun r -> r.new_assigned < r.old_assigned) with
                    | [ from ], [ target ] ->
                        BudgetEvent.MoneyMoved
                            { Month = monthId
                              From = line from
                              To = line target
                              Amount = from.old_assigned - from.new_assigned }
                    | _ -> abort table first.row_id $"Verschieben {applicationId} hat keine eindeutige Quelle"
                | Ok MoneyAssigned ->
                    match group with
                    | [ single ] -> BudgetEvent.AssignmentChangedInBb { Month = monthId; Line = line single }
                    | _ -> abort table first.row_id $"Zuweisen {applicationId} hat {group.Length} Zeilen statt 1"
                | Ok (ObservedInYnab | TakenOverFromYnab as origin) ->
                    // `originOfText` kennt nur die drei früheren Herkünfte; Beobachtung und Übernahme gab es dort nie.
                    abort table first.row_id $"Herkunft {originText origin} gibt es im Altbestand nicht"
                | Error message -> abort table first.row_id message
            { OccurredAt = first.occurred_at
              Rank = 0
              TableRank = 0
              RowId = first.row_id
              EventId = applicationId
              StreamId = streamIdOf (MonthStream monthId)
              Event = event })
    let lookup =
        rows
        |> List.map (fun r -> r.id, (Guid.Parse r.application_id, Guid.Parse r.category_id, monthOf table r.row_id r.month))
        |> Map.ofList
    pending, lookup

let private movementResults (lookup: Map<string, Guid * Guid * string>) (rows: LegacyMovementResultRow list) : Pending list =
    let table = "budget_movement_results"
    rows
    |> List.map (fun r ->
        match Map.tryFind r.movement_id lookup with
        | None -> abort table r.row_id $"Ergebnis ohne Bewegung '{r.movement_id}'"
        | Some (applicationId, categoryId, monthId) ->
            let outcome = writeBackOfStatus table r.row_id r.status r.reason
            { OccurredAt = r.occurred_at
              Rank = 1
              TableRank = 1
              RowId = r.row_id
              EventId = Guid.NewGuid().ToString "D"
              StreamId = streamIdOf (MonthStream monthId)
              Event = writeBackEvent { DecisionEventId = applicationId; CategoryId = categoryId } outcome })

let private pauses (rows: LegacyPauseRow list) : Pending list =
    let table = "target_pause_events"
    rows
    |> List.map (fun r ->
        let id = guidOf table r.row_id "id" r.id
        let categoryId = guidOf table r.row_id "category_id" r.category_id
        let monthId = monthOf table r.row_id r.month
        let pause = { Month = monthId; CategoryId = categoryId; CategoryName = r.category_name }
        let event =
            match pauseKindOfText r.kind with
            | Ok PauseKind.Paused -> BudgetEvent.TargetPaused pause
            | Ok PauseKind.PauseLifted -> BudgetEvent.TargetPauseLifted pause
            | Error _ -> abort table r.row_id $"unbekannte Art '{r.kind}'"
        { OccurredAt = r.occurred_at
          Rank = 0
          TableRank = 2
          RowId = r.row_id
          EventId = id.ToString "D"
          StreamId = streamIdOf (MonthStream monthId)
          Event = event })

let private targetChanges (rows: LegacyTargetChangeRow list) : Pending list * Map<string, Guid * Guid> =
    let table = "target_changes"
    let pending =
        rows
        |> List.map (fun r ->
            let id = guidOf table r.row_id "id" r.id
            let categoryId = guidOf table r.row_id "category_id" r.category_id
            let monthId = monthOf table r.row_id r.month
            let kind =
                match targetChangeKindOfText r.change_kind r.new_frequency with
                | Ok kind -> kind
                | Error message -> abort table r.row_id $"Change-Art '{r.change_kind}': {message}"
            { OccurredAt = r.occurred_at
              Rank = 0
              TableRank = 3
              RowId = r.row_id
              EventId = id.ToString "D"
              StreamId = streamIdOf (TargetStream categoryId)
              Event =
                BudgetEvent.YnabGoalChangedFromBb
                    { Month = monthId
                      CategoryName = r.category_name
                      ChangeKind = kind
                      OldGoalTarget = r.old_goal_target
                      NewGoalTarget = r.new_goal_target
                      OldDate = r.old_date
                      NewDate = r.new_date } })
    let lookup = rows |> List.map (fun r -> r.id, (Guid.Parse r.id, Guid.Parse r.category_id)) |> Map.ofList
    pending, lookup

let private targetChangeResults (lookup: Map<string, Guid * Guid>) (rows: LegacyTargetChangeResultRow list) : Pending list =
    let table = "target_change_results"
    rows
    |> List.map (fun r ->
        match Map.tryFind r.change_id lookup with
        | None -> abort table r.row_id $"Ergebnis ohne Änderung '{r.change_id}'"
        | Some (changeId, categoryId) ->
            // Ohne Schritt stammt das Ergebnis aus budgeting-k8wq3 und gilt als Schritt 1.
            let reference = { DecisionEventId = changeId; Step = r.step |> Option.map int |> Option.defaultValue 1 }
            let outcome = writeBackOfStatus table r.row_id r.status r.reason |> lineOutcomeOf
            { OccurredAt = r.occurred_at
              Rank = 1
              TableRank = 4
              RowId = r.row_id
              EventId = Guid.NewGuid().ToString "D"
              StreamId = streamIdOf (TargetStream categoryId)
              Event = goalStepEvent reference outcome })

/// Die Ereignisse aus dem Altbestand in der Reihenfolge aus ADR 0084 Punkt 4: `occurred_at` (als Text),
/// Entscheidung vor Ergebnis, dann rowid. Bricht bei einer Abbruchregel ab.
let private pendingOf (legacy: LegacyBudget) : Pending list =
    let decided, movementLookup = decisions legacy.Movements
    let changed, changeLookup = targetChanges legacy.TargetChanges
    decided
    @ movementResults movementLookup legacy.MovementResults
    @ pauses legacy.Pauses
    @ changed
    @ targetChangeResults changeLookup legacy.TargetChangeResults
    |> List.sortWith (fun a b ->
        match String.CompareOrdinal(a.OccurredAt, b.OccurredAt) with
        | 0 -> compare (a.Rank, a.TableRank, a.RowId) (b.Rank, b.TableRank, b.RowId)
        | c -> c)

/// Startmigration (ADR 0084 Punkt 4, budgeting-hyn77): überführt die fünf früheren Tabellen nach
/// `events` und entfernt sie, alles in einer Transaktion. Läuft nur, wenn mindestens eine existiert;
/// sonst ändert sie nichts. `events` muss schon angelegt sein. Liefert die Zahl der Ereignisse.
///
/// `stream_version` wird je Stream lückenlos ab 1 vergeben, an eine schon belegte Version stößt das
/// Einfügen über `UNIQUE (stream_id, stream_version)` und wirft. Eingefügt wird in der Sortierfolge aus
/// `pendingOf`, so folgt `position` ihr und liegt hinter dem Kassenbuch. Schlägt eine Abbruchregel oder
/// das Einfügen an, wirft die Funktion, und das Dispose der nicht bestätigten Transaktion lässt alle
/// Tabellen unverändert.
let migrateBudgetEventsOn (conn: SqliteConnection) : int =
    if conn.State <> Data.ConnectionState.Open then
        conn.Open()

    // Erst ohne Transaktion nachsehen: nach der Migration ist das der Normalfall.
    if (existingLegacyTables conn null).IsEmpty then
        0
    else
        use tx = conn.BeginTransaction()
        let legacy = loadLegacyOn conn tx
        let pending = pendingOf legacy
        let versions = Dictionary<string, int64>()
        let copies =
            pending
            |> List.map (fun p ->
                let version =
                    match versions.TryGetValue p.StreamId with
                    | true, v -> v + 1L
                    | false, _ -> 1L
                versions[p.StreamId] <- version
                {| event_id = p.EventId
                   context = Context
                   stream_id = p.StreamId
                   stream_version = version
                   occurred_at = p.OccurredAt
                   actor_kind = actorKindOf p.Event
                   event_type = eventTypeName p.Event
                   schema_version = SchemaVersion
                   payload = encodePayload p.Event |})
            |> List.toArray

        if copies.Length > 0 then
            conn.Execute(
                """INSERT INTO events
                     (event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                      actor_ref, event_type, schema_version, payload, command_id)
                   VALUES
                     (@event_id, @context, @stream_id, @stream_version, @occurred_at, @actor_kind,
                      NULL, @event_type, @schema_version, @payload, NULL)""",
                copies,
                tx
            )
            |> ignore

        // DROP TABLE entfernt die Indizes und Trigger der Tabelle mit.
        conn.Execute("DROP TABLE IF EXISTS budget_movement_results", transaction = tx) |> ignore
        conn.Execute("DROP TABLE IF EXISTS budget_movements", transaction = tx) |> ignore
        conn.Execute("DROP TABLE IF EXISTS target_pause_events", transaction = tx) |> ignore
        conn.Execute("DROP TABLE IF EXISTS target_change_results", transaction = tx) |> ignore
        conn.Execute("DROP TABLE IF EXISTS target_changes", transaction = tx) |> ignore
        tx.Commit()
        copies.Length
