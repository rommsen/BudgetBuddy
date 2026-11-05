module EventStoreTests

// Gemeinsame Ereignistabelle `events` (infrastructure-e4vq7, ADR 0083): Schema, Trigger,
// generisches Anhängen und Laden. Die Fälle laufen gegen die geteilte In-Memory-DB und
// isolieren sich über einen eigenen Kontextnamen je Testfall. Nur der Fall zum
// Transaktionsbeginn öffnet eine Datei-Datenbank im Temp-Verzeichnis.

open System
open System.IO
open Expecto
open Dapper
open Microsoft.Data.Sqlite
open EventStore

/// Ein eigener Kontext je Testfall: die Tabelle ist für alle Testlisten geteilt.
let private freshContext () = "t" + Guid.NewGuid().ToString("N")

let private at (minute: int) = DateTime(2026, 11, 5, 10, minute, 0, DateTimeKind.Utc)

let private event (eventType: string) (minute: int) : NewEvent = {
    EventType = eventType
    SchemaVersion = 1
    Payload = $"{{\"minute\":{minute}}}"
    OccurredAt = at minute
    ActorKind = "UserInBb"
    ActorRef = None
}

let private stream (aggregate: string) (id: AggregateId) (events: NewEvent list) : StreamAppend = {
    Aggregate = aggregate
    Id = id
    Events = events
}

let private request (context: string) (commandId: Guid option) (streams: StreamAppend list) : AppendRequest = {
    Context = context
    CommandId = commandId
    Streams = streams
}

let private setup () = initializeSchema ()

let private run (a: Async<'T>) = Async.RunSynchronously a

let private appended (result: AppendResult) =
    match result with
    | Appended events -> events
    | CommandAlreadyProcessed p -> failtestf "Ereignisse erwartet, bekam CommandAlreadyProcessed %d" p

let private processedCommandRows (commandId: Guid) =
    use conn = global.Persistence.getConnection ()
    conn.ExecuteScalar<int64>(
        "SELECT COUNT(*) FROM processed_commands WHERE command_id = @id",
        {| id = commandId.ToString("D") |}
    )

let private expectRejected (req: AppendRequest) =
    Expect.throwsT<InvalidAppend>
        (fun () -> append req noReadModels |> run |> ignore)
        "Anhängen mit ungültiger Form wird abgewiesen"

[<Tests>]
let tests =
    testList "Gemeinsame Ereignistabelle events" [

        testCase "zwei aufeinanderfolgende Anhängen in denselben Stream ergeben die Versionen 1 und 2" <| fun () ->
            setup ()
            let context = freshContext ()
            let id = GuidId (Guid.NewGuid())
            let first = append (request context None [ stream "entry" id [ event "EntryObserved" 1 ] ]) noReadModels |> run |> appended
            let second = append (request context None [ stream "entry" id [ event "EntryRecategorized" 2 ] ]) noReadModels |> run |> appended
            Expect.equal (first @ second |> List.map _.StreamVersion) [ 1L; 2L ] "Versionen 1 und 2"
            Expect.isGreaterThan second.Head.Position first.Head.Position "position steigt in Einfügereihenfolge"

        testCase "stream_version beginnt je Stream bei 1 und ist lückenlos, position steigt in Einfügereihenfolge" <| fun () ->
            setup ()
            let context = freshContext ()
            let a = GuidId (Guid.NewGuid())
            let b = TextId "2026-11"
            let recorded =
                append
                    (request context None [
                        stream "entry" a [ event "A" 1; event "B" 2; event "C" 3 ]
                        stream "month" b [ event "D" 4; event "E" 5 ]
                    ])
                    noReadModels
                |> run
                |> appended
            Expect.equal
                (recorded |> List.map (fun e -> e.StreamId, e.StreamVersion))
                [ streamIdOf context "entry" a, 1L
                  streamIdOf context "entry" a, 2L
                  streamIdOf context "entry" a, 3L
                  context + ":month:2026-11", 1L
                  context + ":month:2026-11", 2L ]
                "je Stream ab 1 lückenlos"
            let positions = recorded |> List.map _.Position
            Expect.equal positions (List.sort positions) "position in Einfügereihenfolge"
            Expect.equal (List.distinct positions |> List.length) 5 "position eindeutig"

        testCase "der Stream-Name hat die Form context:aggregat:id mit der Guid im Format D" <| fun () ->
            setup ()
            let context = freshContext ()
            let g = Guid.NewGuid()
            let recorded = append (request context None [ stream "entry" (GuidId g) [ event "EntryObserved" 1 ] ]) noReadModels |> run |> appended
            Expect.equal recorded.Head.StreamId $"{context}:entry:{g:D}" "Stream-Name"

        testCase "ein zweites Einfügen derselben stream_id und stream_version per SQL scheitert am UNIQUE" <| fun () ->
            setup ()
            let context = freshContext ()
            let id = GuidId (Guid.NewGuid())
            append (request context None [ stream "entry" id [ event "EntryObserved" 1 ] ]) noReadModels |> run |> ignore
            use conn = global.Persistence.getConnection ()
            let insertVersionOne () =
                conn.Execute(
                    """INSERT INTO events (event_id, context, stream_id, stream_version, occurred_at, actor_kind,
                                           event_type, schema_version, payload)
                       VALUES (@event_id, @context, @stream_id, 1, '2026-11-05T10:00:00.0000000Z', 'UserInBb',
                               'EntryObserved', 1, '{}')""",
                    {| event_id = Guid.NewGuid().ToString("D"); context = context; stream_id = streamIdOf context "entry" id |}
                )
                |> ignore
            let ex = Expect.throwsC insertVersionOne (fun (e: exn) -> e)
            match ex with
            | :? SqliteException as e -> Expect.equal e.SqliteErrorCode 19 "SQLITE_CONSTRAINT"
            | other -> failtestf "SqliteException erwartet, bekam %O" other
            let log = loadStream context "entry" id |> run
            Expect.equal (log |> List.map _.EventType) [ "EntryObserved" ] "nur das erste Ereignis im Log"

        testCase "UPDATE und DELETE auf events weist der Trigger ab" <| fun () ->
            setup ()
            let context = freshContext ()
            let id = GuidId (Guid.NewGuid())
            let recorded = append (request context None [ stream "entry" id [ event "EntryObserved" 1 ] ]) noReadModels |> run |> appended
            use conn = global.Persistence.getConnection ()
            let position = {| position = recorded.Head.Position |}
            for sql in [
                "UPDATE events SET payload = '{}' WHERE position = @position"
                "DELETE FROM events WHERE position = @position"
            ] do
                let ex = Expect.throwsC (fun () -> conn.Execute(sql, position) |> ignore) (fun (e: exn) -> e)
                Expect.stringContains ex.Message "events ist append-only" sql
            Expect.equal (loadStream context "entry" id |> run) recorded "Ereignis unverändert"

        testCase "ein Anhängen mit zwei Streams ist eine Transaktion: scheitert der zweite, steht auch vom ersten nichts im Log" <| fun () ->
            setup ()
            let context = freshContext ()
            let ok = stream "entry" (GuidId (Guid.NewGuid())) [ event "EntryObserved" 1 ]
            let both = stream "entry" (GuidId (Guid.NewGuid())) [ event "EntryObserved" 2 ]
            let recorded = append (request context None [ ok; both ]) noReadModels |> run |> appended
            Expect.equal (recorded |> List.map _.StreamId |> List.distinct |> List.length) 2 "beide Streams gespeichert"

            let context2 = freshContext ()
            let commandId = Guid.NewGuid()
            expectRejected (request context2 (Some commandId) [ ok; stream "entry" (GuidId (Guid.NewGuid())) [ event "entry_observed" 1 ] ])
            Expect.isEmpty (loadContext context2 |> run) "vom ersten Stream steht nichts im Log"
            Expect.equal (processedCommandRows commandId) 0L "keine Zeile in processed_commands"

        testCase "ein ungültiger event_type wird abgewiesen, ohne ein Ereignis oder eine Befehlszeile zu schreiben" <| fun () ->
            setup ()
            for bad in [ "entryRecategorized"; "Entry_Recategorized"; ""; "Entry Recategorized"; "1Entry" ] do
                let context = freshContext ()
                let commandId = Guid.NewGuid()
                expectRejected (request context (Some commandId) [ stream "entry" (GuidId (Guid.NewGuid())) [ event bad 1 ] ])
                Expect.isEmpty (loadContext context |> run) $"kein Ereignis für '{bad}'"
                Expect.equal (processedCommandRows commandId) 0L $"keine Befehlszeile für '{bad}'"

        testCase "ein ungültiger Kontext oder ein ungültiges Aggregat wird abgewiesen, ohne zu schreiben" <| fun () ->
            setup ()
            let marker = freshContext ()
            for context, aggregate in [
                marker.ToUpperInvariant(), "entry"
                marker + ":x", "entry"
                "", "entry"
                marker, "Entry"
                marker, "en:try"
                marker, ""
            ] do
                expectRejected (request context None [ stream aggregate (GuidId (Guid.NewGuid())) [ event "EntryObserved" 1 ] ])
            Expect.isEmpty (loadContext marker |> run) "nichts geschrieben"
            use conn = global.Persistence.getConnection ()
            let anyForMarker =
                conn.ExecuteScalar<int64>(
                    "SELECT COUNT(*) FROM events WHERE stream_id LIKE @prefix",
                    {| prefix = "%" + marker.Substring(1) + "%" |}
                )
            Expect.equal anyForMarker 0L "auch unter abgewandelten Namen nichts geschrieben"

        testCase "loadContext liefert nur den genannten Kontext nach position, loadStream nur den genannten Stream nach stream_version" <| fun () ->
            setup ()
            let mine = freshContext ()
            let other = freshContext ()
            let a = GuidId (Guid.NewGuid())
            let b = GuidId (Guid.NewGuid())
            append (request mine None [ stream "entry" a [ event "A" 1 ] ]) noReadModels |> run |> ignore
            append (request other None [ stream "entry" a [ event "X" 2 ] ]) noReadModels |> run |> ignore
            append (request mine None [ stream "entry" b [ event "B" 3 ] ]) noReadModels |> run |> ignore
            append (request mine None [ stream "entry" a [ event "C" 4 ] ]) noReadModels |> run |> ignore

            let context = loadContext mine |> run
            Expect.equal (context |> List.map _.EventType) [ "A"; "B"; "C" ] "nur der eigene Kontext, nach position"
            Expect.isTrue (context |> List.forall (fun e -> e.Context = mine)) "Kontextspalte"
            let positions = context |> List.map _.Position
            Expect.equal positions (List.sort positions) "aufsteigend nach position"

            let streamA = loadStream mine "entry" a |> run
            Expect.equal (streamA |> List.map (fun e -> e.EventType, e.StreamVersion)) [ "A", 1L; "C", 2L ] "nur Stream a, nach stream_version"

        testCase "RecordedEvent übersteht Schreiben und Laden unverändert, auch ohne ActorRef und Befehls-Id" <| fun () ->
            setup ()
            let context = freshContext ()
            let id = GuidId (Guid.NewGuid())
            let commandId = Guid.NewGuid()
            let withRef = { event "RuleApplied" 7 with ActorKind = "Rule"; ActorRef = Some "rule-42"; SchemaVersion = 3 }
            let plain = event "EntryObserved" 8
            let first = append (request context None [ stream "entry" id [ plain ] ]) noReadModels |> run |> appended
            let second = append (request context (Some commandId) [ stream "entry" id [ withRef ] ]) noReadModels |> run |> appended

            let loaded = loadStream context "entry" id |> run
            Expect.equal loaded (first @ second) "geladen wie gespeichert"

            let p = loaded.[0]
            Expect.equal p.ActorRef None "ActorRef None"
            Expect.equal p.CommandId None "CommandId None"
            Expect.equal p.OccurredAt (at 8) "OccurredAt"
            Expect.equal p.OccurredAt.Kind DateTimeKind.Utc "UTC bleibt UTC"
            Expect.equal p.Payload plain.Payload "Payload"
            Expect.equal p.ActorKind "UserInBb" "ActorKind"
            let r = loaded.[1]
            Expect.equal r.ActorRef (Some "rule-42") "ActorRef Some"
            Expect.equal r.CommandId (Some commandId) "CommandId Some"
            Expect.equal r.SchemaVersion 3 "SchemaVersion"
            Expect.equal r.EventType "RuleApplied" "EventType"
            Expect.notEqual p.EventId r.EventId "eigene event_id je Ereignis"

        testCase "scheitert die Read-Model-Funktion, steht weder ein Ereignis noch eine Befehlszeile in der Datenbank" <| fun () ->
            setup ()
            let context = freshContext ()
            let commandId = Guid.NewGuid()
            let mutable seen = []
            let failing : ApplyReadModels =
                fun _ _ events ->
                    seen <- events |> List.map _.EventType
                    failwith "Read Model kaputt"
            Expect.throws
                (fun () -> append (request context (Some commandId) [ stream "entry" (GuidId (Guid.NewGuid())) [ event "A" 1; event "B" 2 ] ]) failing |> run |> ignore)
                "Fehler der Read-Model-Funktion kommt beim Aufrufer an"
            Expect.equal seen [ "A"; "B" ] "die Read-Model-Funktion sah die Ereignisse dieses Anhängens"
            Expect.isEmpty (loadContext context |> run) "kein Ereignis nach Rollback"
            Expect.equal (processedCommandRows commandId) 0L "keine Befehlszeile nach Rollback"

        testCase "die Read-Model-Funktion sieht die gespeicherten Ereignisse in derselben Transaktion" <| fun () ->
            setup ()
            let context = freshContext ()
            let mutable seenPositions = []
            let readModel : ApplyReadModels =
                fun conn tx events ->
                    // Liest in der offenen Transaktion: die Ereignisse sind schon sichtbar.
                    seenPositions <-
                        conn.Query<int64>(
                            "SELECT position FROM events WHERE context = @context ORDER BY position",
                            {| context = context |},
                            tx
                        )
                        |> List.ofSeq
                    Expect.equal (events |> List.map _.Position) seenPositions "übergeben, was gespeichert ist"
            let recorded = append (request context None [ stream "entry" (GuidId (Guid.NewGuid())) [ event "A" 1 ] ]) readModel |> run |> appended
            Expect.equal seenPositions (recorded |> List.map _.Position) "Read Model sah das Ereignis"

        testCase "ein Befehl mit zwei Ereignissen in zwei Streams wird gespeichert, seine Wiederholung erzeugt nichts" <| fun () ->
            setup ()
            let context = freshContext ()
            let commandId = Guid.NewGuid()
            let req =
                request context (Some commandId) [
                    stream "category" (GuidId (Guid.NewGuid())) [ event "MoneyMovedOut" 1 ]
                    stream "category" (GuidId (Guid.NewGuid())) [ event "MoneyMovedIn" 1 ]
                ]
            let first = append req noReadModels |> run |> appended
            Expect.equal first.Length 2 "zwei Ereignisse"
            Expect.isTrue (first |> List.forall (fun e -> e.CommandId = Some commandId)) "beide tragen die Befehls-Id"
            Expect.equal (processedCommandRows commandId) 1L "eine Befehlszeile"

            let mutable readModelCalled = false
            let again = append req (fun _ _ _ -> readModelCalled <- true) |> run
            Expect.equal again (CommandAlreadyProcessed first.Head.Position) "Wiederholung meldet die erste position"
            Expect.isFalse readModelCalled "keine Read-Model-Fortschreibung bei Wiederholung"
            Expect.equal (loadContext context |> run) first "kein weiteres Ereignis"

        testCase "Ereignisse ohne Befehls-Id hinterlassen keine Zeile in processed_commands" <| fun () ->
            setup ()
            let context = freshContext ()
            append (request context None [ stream "entry" (GuidId (Guid.NewGuid())) [ event "A" 1 ] ]) noReadModels |> run |> ignore
            use conn = global.Persistence.getConnection ()
            let rows =
                conn.ExecuteScalar<int64>(
                    "SELECT COUNT(*) FROM processed_commands WHERE context = @context",
                    {| context = context |}
                )
            Expect.equal rows 0L "keine Befehlszeile"

        testCase "ein Befehl ohne Ereignis hinterlässt keine Befehlszeile" <| fun () ->
            setup ()
            let context = freshContext ()
            let commandId = Guid.NewGuid()
            let result = append (request context (Some commandId) [ stream "entry" (GuidId (Guid.NewGuid())) [] ]) noReadModels |> run
            Expect.equal result (Appended []) "nichts angehängt"
            Expect.equal (processedCommandRows commandId) 0L "keine Befehlszeile"

        testCase "BeginTransaction() öffnet mit BEGIN IMMEDIATE: eine zweite Connection liest, schreibt aber erst nach dem Ende" <| fun () ->
            // Belegt ADR 0083 Abschnitt 5 gegen eine Datei-Datenbank. In der Cache=Shared-
            // In-Memory-DB der übrigen Tests sperrt SQLite je Tabelle, dort wäre der Befund ein
            // anderer. Pooling=False, damit die Datei danach gelöscht werden kann.
            let path = Path.Combine(Path.GetTempPath(), $"bb-eventstore-{Guid.NewGuid():N}.db")
            let connectionString = $"Data Source={path};Pooling=False;Default Timeout=1"
            try
                use setupConn = new SqliteConnection(connectionString)
                setupConn.Open()
                setupConn.Execute("CREATE TABLE probe (n INTEGER NOT NULL)") |> ignore
                setupConn.Execute("INSERT INTO probe (n) VALUES (0)") |> ignore

                use second = new SqliteConnection(connectionString)
                second.Open()
                let secondWrites () = second.Execute("INSERT INTO probe (n) VALUES (2)") |> ignore
                let secondReads () = second.ExecuteScalar<int64>("SELECT COUNT(*) FROM probe")

                // Parameterlos: ohne einen einzigen Schreibbefehl hält die Transaktion schon
                // die Schreibsperre der Datei.
                use first = new SqliteConnection(connectionString)
                first.Open()
                using (first.BeginTransaction()) (fun tx ->
                    Expect.equal (secondReads ()) 1L "eine zweite Connection darf lesen"
                    let ex = Expect.throwsC secondWrites (fun (e: exn) -> e)
                    match ex with
                    | :? SqliteException as e -> Expect.equal e.SqliteErrorCode 5 "SQLITE_BUSY nach dem Timeout"
                    | other -> failtestf "SqliteException erwartet, bekam %O" other
                    tx.Rollback())
                secondWrites ()

                // Gegenprobe: aufgeschoben hält die Transaktion vor dem ersten Schreiben keine
                // Sperre, die zweite Connection schreibt durch.
                using (first.BeginTransaction(true)) (fun tx ->
                    secondWrites ()
                    tx.Rollback())
                Expect.equal (secondReads ()) 3L "beide Schreibvorgänge der zweiten Connection stehen"
            finally
                SqliteConnection.ClearAllPools()
                if File.Exists path then File.Delete path
    ]
