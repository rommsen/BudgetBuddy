/// Baustein für Budget-Tests (budgeting-hyn77): hält vorbereitete Zeilen über den echten Schreibweg nach
/// `events` fest. Die Gruppen-Id und die Zeilen-Id vergibt das Schreiben (`event_id`, `lineId`); die Tests
/// arbeiten mit den zurückgegebenen Zeilen weiter.
module BudgetEventsFixture

open Shared.Budgeting
open Budgeting.Events
open Budgeting.Persistence

/// Das Festgehaltene; eine schon festgehaltene Befehls-Id ist hier ein Fehler des Testaufbaus.
let recordedOrFail (result: Recorded<'T>) : 'T =
    match result with
    | Recorded value -> value
    | CommandAlreadyProcessed (_, position) -> failwith $"Befehls-Id schon festgehalten an Position {position}"

/// Hält die Zeilen je Gruppen-Id (Reihenfolge der Eingabe) als ein Entscheidungsereignis fest und liefert
/// die gespeicherten Zeilen in der Reihenfolge der Eingabe. Zeitpunkt, Monat und Herkunft kommen aus der
/// ersten Zeile einer Gruppe.
let recordGroups (movements: Movement list) : Movement list =
    let stored =
        movements
        |> List.groupBy (fun m -> m.ApplicationId)
        |> List.collect (fun (_, group) ->
            let first = List.head group
            let lines =
                group
                |> List.map (fun m ->
                    { CategoryId = m.CategoryId
                      CategoryName = m.CategoryName
                      OldAssigned = m.OldAssigned
                      NewAssigned = m.NewAssigned })
            let recorded = recordDecision None first.OccurredAt first.Month first.Origin lines [] [] |> Async.RunSynchronously |> recordedOrFail
            List.zip group recorded)
        |> Map.ofList
    movements |> List.map (fun m -> Map.find m stored)

/// Der Ausgang eines Rückschreibens zu einem Stand, wie ihn die Leser zeigen.
let writeBackOf (outcome: LineOutcome) : WriteBackOutcome =
    match outcome with
    | Written -> WrittenBack
    | Failed reason -> WriteBackFailed reason
    | ChangedInYnab -> SupersededInYnab

/// Hängt ein Ergebnis an eine gespeicherte Zeile.
let recordResult (m: Movement) (at: System.DateTime) (outcome: LineOutcome) : unit =
    recordWriteBack m at (writeBackOf outcome) |> Async.RunSynchronously

/// Eine eigene In-Memory-Datenbank mit `events`, getrennt von der gemeinsamen Testdatenbank: für Fälle, die
/// ein nicht lesbares Budget-Ereignis brauchen (es träfe sonst jeden Leser über den ganzen Kontext). Die
/// erste Verbindung hält die Datenbank am Leben, `connect` liefert je Aufruf eine neue.
let isolatedDb () : Microsoft.Data.Sqlite.SqliteConnection * (unit -> Microsoft.Data.Sqlite.SqliteConnection) =
    let connectionString = $"Data Source=budget-{System.Guid.NewGuid():N};Mode=Memory;Cache=Shared"
    let anchor = new Microsoft.Data.Sqlite.SqliteConnection(connectionString)
    anchor.Open()
    EventStore.createSchemaOn anchor
    anchor, (fun () -> new Microsoft.Data.Sqlite.SqliteConnection(connectionString))

/// Hängt ein Ereignis mit Typname `eventType` und leerer Payload `{}` an einen Budget-Stream. Für einen
/// bekannten Typ ist das ein Ereignis mit kaputter Payload.
let appendUnreadable (conn: Microsoft.Data.Sqlite.SqliteConnection) (stream: BudgetStream) (eventType: string) : unit =
    let aggregate, id = streamParts stream
    let tx = conn.BeginTransaction()
    EventStore.appendOn
        conn
        tx
        { Context = Context
          CommandId = None
          Streams =
            [ { Aggregate = aggregate
                Id = id
                Events =
                  [ { EventType = eventType
                      SchemaVersion = 1
                      Payload = "{}"
                      OccurredAt = System.DateTime(2026, 11, 4, 9, 0, 0, System.DateTimeKind.Utc)
                      ActorKind = "UserInBb"
                      ActorRef = None } ] } ] }
        EventStore.noReadModels
    |> ignore
    tx.Commit()

/// Hängt ein Ereignis eines unbekannten Typs an einen Budget-Stream.
let appendUnknown (conn: Microsoft.Data.Sqlite.SqliteConnection) (stream: BudgetStream) : unit =
    appendUnreadable conn stream "TargetQuatschGemacht"

[<CLIMutable>]
type MentionRow = { position: int64; event_type: string; command_id: string }

/// Typ und Befehls-Id aller Budget-Ereignisse, deren Payload eine der Kategorien nennt, in der Reihenfolge
/// des Festhaltens (budgeting-qm5x9). Die Kategorien sind je Test frisch, andere Tests stören die Zählung nicht.
let eventsMentioning (categoryIds: System.Guid list) : (string * string option) list =
    use conn = global.Persistence.getConnection ()
    categoryIds
    |> List.collect (fun id ->
        Dapper.SqlMapper.Query<MentionRow>(
            conn,
            "SELECT position, event_type, COALESCE(command_id, '') AS command_id FROM events WHERE context = 'budgeting' AND payload LIKE @pattern",
            {| pattern = $"%%{id}%%" |}
        )
        |> List.ofSeq)
    |> List.distinctBy (fun r -> r.position)
    |> List.sortBy (fun r -> r.position)
    |> List.map (fun r -> r.event_type, (if r.command_id = "" then None else Some r.command_id))

/// Die Zeilen in `processed_commands` zu einer Befehls-Id.
let processedCommandRows (commandId: System.Guid) : int64 =
    use conn = global.Persistence.getConnection ()
    Dapper.SqlMapper.ExecuteScalar<int64>(
        conn,
        "SELECT COUNT(*) FROM processed_commands WHERE command_id = @id",
        {| id = commandId.ToString "D" |}
    )

/// YNABs Goals eines Monats als BBs Targets, für reine Tests von `toBudgetMonth`, die das Target aus dem Monat
/// erwarten (budgeting-eh8xe: die Seite zeigt BBs Regeln).
let ynabTargets (month: YnabMonth) : Map<System.Guid, Target> =
    month.Categories |> List.choose (fun c -> c.Target |> Option.map (fun t -> c.Id, t)) |> Map.ofList

/// Die Goals eines Monats so, wie `/categories` sie liefert: Quelle der Target-Übernahme für Fake-Ports.
let goalsOf (month: YnabMonth) : YnabCategoryGoal list =
    month.Categories |> List.map (fun c -> { Id = c.Id; Name = c.Name; Target = c.Target })

/// `toBudgetMonth` mit YNABs Goals als BB-Targets (`ynabTargets`).
let toBudgetMonthOfYnab (pausedInBb: Set<System.Guid>) (month: YnabMonth) : BudgetMonth =
    Server.Budgeting.Domain.toBudgetMonth (ynabTargets month) pausedInBb month

/// Hält eine Target-Änderung des Altbestands (`YnabGoalChangedFromBb`) im Target-Stream fest, wie sie der
/// Zwei-Schritt-Weg bis budgeting-eh8xe schrieb. Liefert ihre `event_id`.
let recordLegacyGoalChangeOn
    (conn: Microsoft.Data.Sqlite.SqliteConnection)
    (categoryId: System.Guid)
    (at: System.DateTime)
    (change: YnabGoalChange)
    : System.Guid =
    (appendOn conn (TargetStream categoryId) [ at, BudgetEvent.YnabGoalChangedFromBb change ] |> List.exactlyOne).EventId

/// Hängt ein Schritt-Ergebnis des Altbestands an den Target-Stream.
let recordLegacyStepOn
    (conn: Microsoft.Data.Sqlite.SqliteConnection)
    (categoryId: System.Guid)
    (changeId: System.Guid)
    (step: int)
    (at: System.DateTime)
    (outcome: LineOutcome)
    : unit =
    appendOn conn (TargetStream categoryId) [ at, goalStepEvent { DecisionEventId = changeId; Step = step } outcome ] |> ignore
