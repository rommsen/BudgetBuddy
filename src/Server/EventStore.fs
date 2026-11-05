/// Die gemeinsame Ereignistabelle `events` für alle Kontexte mit Event Sourcing (ADR 0083).
///
/// Hier liegen Schema, das generische Anhängen und das generische Laden. Ein Kontext gibt
/// Kontextname, Aggregat, Id und die schon kodierten Ereignisse (Typ, Version, Payload als
/// JSON-Text) und bekommt die gespeicherten Ereignisse zurück. Den Stream-Namen
/// `<context>:<aggregat>:<id>`, die `stream_version`, die `event_id` und die Zeile in
/// `processed_commands` vergibt nur dieses Modul.
///
/// Geschrieben wird auf einer frischen Connection in einer Transaktion (ADR 0008, ADR 0013
/// Punkt 5). Die Read Models schreibt der Aufrufer über `applyReadModels` in derselben
/// Transaktion fort. Queries benutzen keine SQLite-JSON-Funktionen (ADR 0013 Punkt 8).
module EventStore

open System
open System.Globalization
open System.Text.RegularExpressions
open Microsoft.Data.Sqlite
open Dapper

// ============================================
// Typen
// ============================================

/// Ein Ereignis vor dem Speichern, vom Kontext kodiert.
type NewEvent = {
    /// Englisch, PascalCase, Vergangenheitsform (ADR 0083 Abschnitt 9). Erzwungen wird nur die
    /// Form `^[A-Z][A-Za-z0-9]*$`.
    EventType: string
    SchemaVersion: int
    /// JSON-Text, vom Kontext kodiert.
    Payload: string
    /// Fachlicher Zeitpunkt in UTC, gespeichert im Format `o`.
    OccurredAt: DateTime
    /// Name der Actor-Variante in PascalCase (`YnabMirror`, `Rule`, `UserInBb`, ...).
    ActorKind: string
    /// Freie Referenz (RuleId, Bestell-Id), sonst `None`.
    ActorRef: string option
}

/// Id eines Aggregats. Eine Guid steht im Stream-Namen im Format `D`.
type AggregateId =
    | GuidId of Guid
    | TextId of string

/// Ereignisse für einen Stream innerhalb eines Anhängens.
type StreamAppend = {
    /// Aggregat in Kleinschreibung (`entry`, `category`).
    Aggregate: string
    Id: AggregateId
    Events: NewEvent list
}

/// Ein Anhängen: ein Kontext, optional eine Befehls-Id, ein oder mehrere Streams. Alles
/// landet in einer Transaktion.
type AppendRequest = {
    /// Kontext in Kleinschreibung (`ledger`, `budgeting`).
    Context: string
    CommandId: Guid option
    Streams: StreamAppend list
}

/// Ein gespeichertes Ereignis mit allen Spalten von `events`.
type RecordedEvent = {
    Position: int64
    EventId: Guid
    Context: string
    StreamId: string
    StreamVersion: int64
    OccurredAt: DateTime
    ActorKind: string
    ActorRef: string option
    EventType: string
    SchemaVersion: int
    Payload: string
    CommandId: Guid option
}

type AppendResult =
    /// Die gespeicherten Ereignisse in Einfügereihenfolge.
    | Appended of RecordedEvent list
    /// Die Befehls-Id stand schon in `processed_commands`; es ist nichts entstanden.
    | CommandAlreadyProcessed of firstPosition: int64

/// Die Read-Model-Funktion des Aufrufers: läuft in derselben Transaktion wie das Anhängen.
/// Wirft sie, rollt alles zurück.
type ApplyReadModels = SqliteConnection -> SqliteTransaction -> RecordedEvent list -> unit

/// Für Aufrufer ohne Read Models.
let noReadModels : ApplyReadModels = fun _ _ _ -> ()

// ============================================
// Schema
// ============================================

let private schemaSql =
    """
    CREATE TABLE IF NOT EXISTS events (
        position       INTEGER PRIMARY KEY,
        event_id       TEXT    NOT NULL UNIQUE,
        context        TEXT    NOT NULL,
        stream_id      TEXT    NOT NULL,
        stream_version INTEGER NOT NULL,
        occurred_at    TEXT    NOT NULL,
        actor_kind     TEXT    NOT NULL,
        actor_ref      TEXT,
        event_type     TEXT    NOT NULL,
        schema_version INTEGER NOT NULL,
        payload        TEXT    NOT NULL,
        command_id     TEXT,
        UNIQUE (stream_id, stream_version)
    );

    CREATE INDEX IF NOT EXISTS idx_events_context_type ON events(context, event_type);
    CREATE INDEX IF NOT EXISTS idx_events_command ON events(command_id);

    CREATE TRIGGER IF NOT EXISTS events_no_update BEFORE UPDATE ON events
    BEGIN SELECT RAISE(ABORT, 'events ist append-only'); END;
    CREATE TRIGGER IF NOT EXISTS events_no_delete BEFORE DELETE ON events
    BEGIN SELECT RAISE(ABORT, 'events ist append-only'); END;

    CREATE TABLE IF NOT EXISTS processed_commands (
        command_id     TEXT    PRIMARY KEY,
        context        TEXT    NOT NULL,
        first_position INTEGER NOT NULL
    );
    """

/// Legt `events` und `processed_commands` auf der übergebenen Connection an. Wiederholbar.
let internal createSchemaOn (conn: SqliteConnection) = conn.Execute(schemaSql) |> ignore

/// Legt `events` und `processed_commands` an; beim Start vor den Schemata der Kontexte.
let initializeSchema () =
    use conn = Persistence.getConnection ()
    createSchemaOn conn

// ============================================
// Form-Prüfung (ADR 0083 Abschnitte 2 und 9)
// ============================================

let private namePattern = Regex(@"^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)
let private eventTypePattern = Regex(@"^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)

let private idText (id: AggregateId) =
    match id with
    | GuidId g -> g.ToString("D")
    | TextId t -> t

/// Der eine Ort, an dem der Stream-Name gebaut wird: `<context>:<aggregat>:<id>`.
let streamIdOf (context: string) (aggregate: string) (id: AggregateId) =
    String.Join(":", [| context; aggregate; idText id |])

/// Sammelt alle Formfehler eines Anhängens. Leer heißt: in Ordnung.
let validate (request: AppendRequest) : string list =
    [
        if not (namePattern.IsMatch request.Context) then
            yield $"Kontext '{request.Context}' passt nicht auf ^[a-z][a-z0-9-]*$"
        for stream in request.Streams do
            if not (namePattern.IsMatch stream.Aggregate) then
                yield $"Aggregat '{stream.Aggregate}' passt nicht auf ^[a-z][a-z0-9-]*$"
            match stream.Id with
            | TextId t when String.IsNullOrWhiteSpace t || t <> t.ToLowerInvariant() || t |> Seq.exists Char.IsWhiteSpace ->
                yield $"Id '{t}' ist leer, enthält Leerraum oder Großbuchstaben"
            | _ -> ()
            for event in stream.Events do
                if isNull event.EventType || not (eventTypePattern.IsMatch event.EventType) then
                    yield $"Ereignistyp '{event.EventType}' passt nicht auf ^[A-Z][A-Za-z0-9]*$"
                if String.IsNullOrWhiteSpace event.ActorKind then
                    yield "Actor-Art ist leer"
                if isNull event.Payload then
                    yield $"Payload von '{event.EventType}' fehlt"
                // Die Tabelle ist append-only: ein Zeitpunkt ohne UTC ließe sich nie mehr korrigieren
                // und sortierte als Text anders als die übrigen (ADR 0083 Abschnitt 1).
                if event.OccurredAt.Kind <> DateTimeKind.Utc then
                    yield $"Zeitpunkt von '{event.EventType}' ist nicht UTC"
                if event.SchemaVersion < 1 then
                    yield $"Schema-Version von '{event.EventType}' ist kleiner als 1"
    ]

/// Ein Anhängen mit Formfehlern; es wurde nichts geschrieben.
exception InvalidAppend of errors: string list
    with
        override this.Message = String.Join("; ", this.errors)

// ============================================
// Text-Konvertierungen und Zeilen
// ============================================

let private dateToText (d: DateTime) = d.ToString("o", CultureInfo.InvariantCulture)

let private dateFromText (s: string) =
    DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)

let private orNull (s: string option) : obj =
    match s with
    | Some v -> box v
    | None -> null

[<CLIMutable>]
type private StoredEventRow = {
    position: int64
    event_id: string
    context: string
    stream_id: string
    stream_version: int64
    occurred_at: string
    actor_kind: string
    actor_ref: string option
    event_type: string
    schema_version: int64
    payload: string
    command_id: string option
}

let private rowToRecorded (row: StoredEventRow) : RecordedEvent = {
    Position = row.position
    EventId = Guid.Parse row.event_id
    Context = row.context
    StreamId = row.stream_id
    StreamVersion = row.stream_version
    OccurredAt = dateFromText row.occurred_at
    ActorKind = row.actor_kind
    ActorRef = row.actor_ref
    EventType = row.event_type
    SchemaVersion = int row.schema_version
    Payload = row.payload
    CommandId = row.command_id |> Option.map Guid.Parse
}

// ============================================
// Anhängen
// ============================================

/// Anhängen auf einer schon offenen Connection und Transaktion, etwa für eine
/// Startmigration. Committet nicht und rollt nicht zurück; das bleibt beim Aufrufer.
/// Reihenfolge: Form prüfen, Befehls-Id prüfen, je Stream `MAX(stream_version)` lesen und
/// fortlaufend vergeben, einfügen, `processed_commands` schreiben (nur mit Befehls-Id und
/// mindestens einem Ereignis), `applyReadModels` aufrufen.
let appendOn
    (conn: SqliteConnection)
    (tx: SqliteTransaction)
    (request: AppendRequest)
    (applyReadModels: ApplyReadModels)
    : AppendResult =
    let errors = validate request

    if not (List.isEmpty errors) then
        raise (InvalidAppend errors)

    let alreadyProcessed =
        match request.CommandId with
        | None -> None
        | Some commandId ->
            conn.Query<int64>(
                "SELECT first_position FROM processed_commands WHERE command_id = @command_id",
                {| command_id = commandId.ToString("D") |},
                tx
            )
            |> Seq.tryHead

    match alreadyProcessed with
    | Some firstPosition -> CommandAlreadyProcessed firstPosition
    | None ->
        let commandIdText = request.CommandId |> Option.map (fun g -> g.ToString("D"))
        // Letzte vergebene Version je Stream in diesem Anhängen; nur der erste Zugriff liest die Tabelle.
        let versions = Collections.Generic.Dictionary<string, int64>()
        let eventIds = ResizeArray<string>()

        for stream in request.Streams do
            let streamId = streamIdOf request.Context stream.Aggregate stream.Id

            for event in stream.Events do
                let version =
                    match versions.TryGetValue streamId with
                    | true, v -> v + 1L
                    | false, _ ->
                        conn.ExecuteScalar<int64>(
                            "SELECT COALESCE(MAX(stream_version), 0) FROM events WHERE stream_id = @stream_id",
                            {| stream_id = streamId |},
                            tx
                        )
                        + 1L

                versions[streamId] <- version
                let eventId = Guid.NewGuid().ToString("D")

                conn.Execute(
                    """INSERT INTO events
                         (event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                          actor_ref, event_type, schema_version, payload, command_id)
                       VALUES
                         (@event_id, @context, @stream_id, @stream_version, @occurred_at, @actor_kind,
                          @actor_ref, @event_type, @schema_version, @payload, @command_id)""",
                    {|
                        event_id = eventId
                        context = request.Context
                        stream_id = streamId
                        stream_version = version
                        occurred_at = dateToText event.OccurredAt
                        actor_kind = event.ActorKind
                        actor_ref = orNull event.ActorRef
                        event_type = event.EventType
                        schema_version = event.SchemaVersion
                        payload = event.Payload
                        command_id = orNull commandIdText
                    |},
                    tx
                )
                |> ignore

                eventIds.Add eventId

        // Einmal zurückgelesen statt aus den Eingaben gebaut: so sieht der Aufrufer genau das,
        // was gespeichert ist, samt der von SQLite vergebenen position.
        let recorded =
            if eventIds.Count = 0 then
                []
            else
                conn.Query<StoredEventRow>(
                    """SELECT position, event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                              actor_ref, event_type, schema_version, payload, command_id
                       FROM events WHERE event_id IN @event_ids ORDER BY position""",
                    {| event_ids = eventIds.ToArray() |},
                    tx
                )
                |> Seq.map rowToRecorded
                |> List.ofSeq

        match commandIdText, recorded with
        | Some commandId, first :: _ ->
            conn.Execute(
                "INSERT INTO processed_commands (command_id, context, first_position) VALUES (@command_id, @context, @first_position)",
                {| command_id = commandId; context = request.Context; first_position = first.Position |},
                tx
            )
            |> ignore
        | _ -> ()

        if not (List.isEmpty recorded) then
            applyReadModels conn tx recorded

        Appended recorded

/// Hängt Ereignisse an `events` an und schreibt die Read Models fort: eine frische
/// Connection, eine Transaktion (ADR 0008, ADR 0013 Punkt 5). Jede Ausnahme rollt alles
/// zurück, auch die Zeile in `processed_commands`.
let append (request: AppendRequest) (applyReadModels: ApplyReadModels) : Async<AppendResult> =
    async {
        use conn = Persistence.getConnection ()
        conn.Open()
        use tx = conn.BeginTransaction()

        // Ohne Commit rollt das Dispose von `tx` zurück, auch bei einer Ausnahme.
        let result = appendOn conn tx request applyReadModels
        tx.Commit()
        return result
    }

// ============================================
// Laden
// ============================================

/// Ein Stream nach `stream_version`.
let loadStream (context: string) (aggregate: string) (id: AggregateId) : Async<RecordedEvent list> =
    async {
        use conn = Persistence.getConnection ()

        return
            conn.Query<StoredEventRow>(
                """SELECT position, event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                          actor_ref, event_type, schema_version, payload, command_id
                   FROM events WHERE stream_id = @stream_id ORDER BY stream_version""",
                {| stream_id = streamIdOf context aggregate id |}
            )
            |> Seq.map rowToRecorded
            |> List.ofSeq
    }

/// Alle Ereignisse eines Kontexts nach `position`, also in der Reihenfolge des Festhaltens.
/// Fachliche Reports sortieren nach Datum oder `occurred_at` (ADR 0083 Abschnitt 1).
let loadContext (context: string) : Async<RecordedEvent list> =
    async {
        use conn = Persistence.getConnection ()

        return
            conn.Query<StoredEventRow>(
                """SELECT position, event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                          actor_ref, event_type, schema_version, payload, command_id
                   FROM events WHERE context = @context ORDER BY position""",
                {| context = context |}
            )
            |> Seq.map rowToRecorded
            |> List.ofSeq
    }
