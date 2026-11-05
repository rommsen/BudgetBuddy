/// Baustein für Budget-Tests (budgeting-hyn77): hält vorbereitete Zeilen über den echten Schreibweg nach
/// `events` fest. Die Gruppen-Id und die Zeilen-Id vergibt das Schreiben (`event_id`, `lineId`); die Tests
/// arbeiten mit den zurückgegebenen Zeilen weiter.
module BudgetEventsFixture

open Shared.Budgeting
open Budgeting.Events
open Budgeting.Persistence

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
            let recorded = recordDecision first.OccurredAt first.Month first.Origin lines [] |> Async.RunSynchronously
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

/// Hängt ein Ereignis eines unbekannten Typs an einen Budget-Stream.
let appendUnknown (conn: Microsoft.Data.Sqlite.SqliteConnection) (stream: BudgetStream) : unit =
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
                  [ { EventType = "TargetQuatschGemacht"
                      SchemaVersion = 1
                      Payload = "{}"
                      OccurredAt = System.DateTime(2026, 11, 4, 9, 0, 0, System.DateTimeKind.Utc)
                      ActorKind = "UserInBb"
                      ActorRef = None } ] } ] }
        EventStore.noReadModels
    |> ignore
    tx.Commit()
