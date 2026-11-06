module Tests.TargetCheckQueryTests

// budgeting-p2zr6, ADR 0082: die Rohdaten von Targets prüfen. Die Kassenbuch-Abfrage für alle Kategorien
// gegen In-Memory-SQLite, die letzte Target-Änderung je Kategorie auf einer eigenen Verbindung und der
// Handler `targetCheck` über seinen Port mit fester Uhr.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Shared.Budgeting
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }
let private run (a: Async<'T>) = Async.RunSynchronously a
let private utc (y: int) (m: int) (d: int) = DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc)

/// Der laufende Tag: 5. November 2026 in Berlin.
let private today = DateOnly(2026, 11, 5)
let private months = Server.Budgeting.Domain.targetCheckMonths (today.ToDateTime TimeOnly.MinValue)

let private catRef (id: Guid) (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId id; CategoryName = name }

let private observed (date: DateTime) (amount: decimal) : EntryObservedInYnabPayload = {
    AccountId = LedgerAccountId (Guid.NewGuid())
    YnabTransactionId = $"ynab-p2zr6-{Guid.NewGuid()}"
    ImportId = None
    Date = date
    Amount = eur amount
    Payee = Some "Empfänger P2zr6"
    Memo = None
    Category = None
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = None
    FlagName = None
}

let private observedEvent (payload: EntryObservedInYnabPayload) (entryId: EntryId) : LedgerEvent =
    { EntryId = entryId
      OccurredAt = DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc)
      Actor = YnabMirror
      Body = EntryObservedInYnab payload }

let private seed (events: EntryId -> LedgerEvent list) =
    appendAndApply (events (newEntryId ())) |> run

let private booked (cat: CategoryRef) (date: DateTime) (amount: decimal) =
    seed (fun e -> [ observedEvent { observed date amount with Category = Some cat } e ])

let private ynabCategory (name: string) : YnabMonthCategory =
    { Id = Guid.NewGuid()
      Name = name
      GroupName = "Alltag"
      Assigned = eur 0m
      Activity = eur 0m
      Available = eur 0m
      Target = None
      Hidden = false
      Deleted = false
      Internal = false }

let private monthWith (categories: YnabMonthCategory list) (month: string) : YnabMonth =
    { Month = month; ToBeBudgeted = eur 0m; Categories = categories }

let private initialize () =
    Persistence.initializeDatabase ()
    Ledger.Persistence.initializeLedgerSchema ()
    Budgeting.Persistence.initializeBudgetingSchema ()

[<Tests>]
let ledgerTests =
    testList "Targets prüfen: Ausgaben aller Kategorien aus dem Kassenbuch (SQLite)" [
        testCase "Je Kategorie der Anteil, ohne Überträge, gelöschte, laufenden und 13. Monat, Erstattungen mindern" <| fun () ->
            initialize ()
            let a = catRef (Guid.NewGuid()) "Kategorie A P2zr6"
            let b = catRef (Guid.NewGuid()) "Kategorie B P2zr6"
            let account = YnabAccountId (Guid.NewGuid())
            // Split über beide Kategorien und ein Konto: je Kategorie ihr Anteil, die Konto-Zeile zählt nicht.
            seed (fun e ->
                [ observedEvent
                      { observed (utc 2026 3 10) -70m with
                          Splits =
                              [ { Target = ToCategory (a.CategoryId, a.CategoryName); Amount = eur -10m; Memo = None }
                                { Target = ToCategory (b.CategoryId, b.CategoryName); Amount = eur -20m; Memo = None }
                                { Target = ToTransfer (account, "Sparkonto"); Amount = eur -40m; Memo = None } ] }
                      e ])
            // Übertragsbuchung ohne Kategorie.
            seed (fun e ->
                [ observedEvent
                      { observed (utc 2026 3 12) -500m with
                          TransferAccountId = Some account
                          TransferYnabTransactionId = Some "gegenbuchung-p2zr6" }
                      e ])
            // In YNAB gelöscht.
            seed (fun e ->
                [ observedEvent { observed (utc 2026 4 11) -99m with Category = Some a } e
                  { observedEvent (observed (utc 2026 4 11) -99m) e with Body = EntryDeletionObservedInYnab } ])
            booked a (utc 2026 4 5) -50m
            booked a (utc 2026 4 20) 20m // Erstattung
            booked a (utc 2026 11 1) -1000m // laufender Monat
            booked a (utc 2025 10 31) -1000m // 13. Monat zurück
            booked b (utc 2025 11 1) -7m

            let (YnabCategoryId idA), (YnabCategoryId idB) = a.CategoryId, b.CategoryId
            let result = Ledger.QueryHandlers.getMonthlyActivityByCategory [ idA; idB ] months |> run
            Expect.equal
                (result |> Map.tryFind idA)
                (Some [ DateTime(2026, 3, 1), eur -10m; DateTime(2026, 4, 1), eur -30m ])
                "A: Split-Anteil und netto, ohne gelöschte, laufenden und 13. Monat"
            Expect.equal
                (result |> Map.tryFind idB)
                (Some [ DateTime(2025, 11, 1), eur -7m; DateTime(2026, 3, 1), eur -20m ])
                "B: Split-Anteil, erster gezählter Monat"

        testCase "Über den Handler steht ein Monat ohne Buchung mit 0, die Monate sind die 12 vor dem laufenden" <| fun () ->
            initialize ()
            let category = ynabCategory "Kategorie C P2zr6"
            booked (catRef category.Id category.Name) (utc 2026 10 3) -12m
            let port =
                let ynab : Server.Budgeting.CommandHandlers.YnabPort =
                    { FetchMonth = fun month -> async { return Ok (monthWith [ category ] month) }
                      SetAssigned = fun _ _ _ -> async { return failwith "Targets prüfen schreibt nicht" }
                      FetchCategoryGoals = fun () -> async { return Ok [] } }
                { Server.Budgeting.QueryHandlers.liveTargetCheckPort ynab (fun () -> DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc)) with
                    LoadLastTargetChanges = fun () -> async { return Ok Map.empty } }
            match Server.Budgeting.QueryHandlers.targetCheck port today |> run with
            | Error message -> failtest message
            | Ok data ->
                let spending = (data.Categories |> List.exactlyOne).Spending
                Expect.equal (spending |> List.map (fun s -> s.Month)) ([ for m in months -> m.ToString("yyyy-MM-01") ]) "November 2025 bis Oktober 2026"
                Expect.equal (List.head months) (DateTime(2025, 11, 1)) "erster Monat"
                Expect.equal (spending |> List.map (fun s -> s.Activity.Amount)) (List.replicate 11 0m @ [ -12m ]) "0 ohne Buchung"

        testCase "Erste Buchung je Kategorie: vor dem Fenster, im Fenster, nur im Split, ohne gelöschte, ohne Buchung (budgeting-k7r2w)" <| fun () ->
            initialize ()
            let old = catRef (Guid.NewGuid()) "Alt K7r2w"
            let fresh = catRef (Guid.NewGuid()) "Neu K7r2w"
            let splitOnly = catRef (Guid.NewGuid()) "Nur Split K7r2w"
            let afterDeleted = catRef (Guid.NewGuid()) "Nach gelöschter K7r2w"
            let none = catRef (Guid.NewGuid()) "Ohne Buchung K7r2w"
            booked old (utc 2026 3 10) -20m
            booked old (utc 2024 5 10) -30m
            booked fresh (utc 2026 8 1) -40m
            booked fresh (utc 2026 6 15) -50m
            seed (fun e ->
                [ observedEvent
                      { observed (utc 2026 2 3) -30m with
                          Splits =
                              [ { Target = ToCategory (splitOnly.CategoryId, splitOnly.CategoryName); Amount = eur -10m; Memo = None }
                                { Target = ToCategory (old.CategoryId, old.CategoryName); Amount = eur -20m; Memo = None } ] }
                      e ])
            // In YNAB gelöscht: zählt nicht als erste Buchung.
            seed (fun e ->
                [ observedEvent { observed (utc 2025 1 1) -99m with Category = Some afterDeleted } e
                  { observedEvent (observed (utc 2025 1 1) -99m) e with Body = EntryDeletionObservedInYnab } ])
            booked afterDeleted (utc 2026 9 9) -5m

            let idOf (c: CategoryRef) = let (YnabCategoryId id) = c.CategoryId in id
            let result =
                Ledger.QueryHandlers.getFirstBookingDayByCategory (List.map idOf [ old; fresh; splitOnly; afterDeleted; none ]) |> run
            Expect.equal
                result
                (Map.ofList
                    [ idOf old, DateTime(2024, 5, 10)
                      idOf fresh, DateTime(2026, 6, 15)
                      idOf splitOnly, DateTime(2026, 2, 3)
                      idOf afterDeleted, DateTime(2026, 9, 9) ])
                "je Kategorie der früheste Tag, auch als Split-Zeile, ohne gelöschte, ohne Buchung fehlt sie"
            Expect.equal (Ledger.QueryHandlers.getFirstBookingDayByCategory [] |> run) Map.empty "ohne Kategorie leer"
    ]

// ============================================
// Letzte Target-Änderung je Kategorie (eigene Verbindung)
// ============================================

/// Eine eigene Datenbank mit `events` (budgeting-hyn77): ein unbekanntes Ereignis bliebe in der geteilten
/// sonst für andere Tests stehen.
let private openChangesDb () =
    let conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
    conn.Open()
    EventStore.createSchemaOn conn
    conn

let private utcOf (at: string) =
    DateTime.Parse(at, Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.RoundtripKind)

/// Eine Target-Änderung aus dem Altbestand (`YnabGoalChangedFromBb`), `at` als UTC-Zeitpunkt im Format `o`.
let private change (conn: Microsoft.Data.Sqlite.SqliteConnection) (categoryId: Guid) (at: string) (kind: TargetChangeKind) : Guid * Guid =
    let event =
        Budgeting.Events.BudgetEvent.YnabGoalChangedFromBb
            { Month = "2026-07"
              CategoryName = "X"
              ChangeKind = kind
              OldGoalTarget = None
              NewGoalTarget = None
              OldDate = None
              NewDate = None }
    let recorded = Budgeting.Persistence.appendOn conn (Budgeting.Events.TargetStream categoryId) [ utcOf at, event ]
    categoryId, (List.exactlyOne recorded).EventId

/// Ein Ergebnis zu einem Schritt der Änderung aus dem Altbestand.
let private result (conn: Microsoft.Data.Sqlite.SqliteConnection) (categoryId: Guid, changeId: Guid) (step: int) (outcome: LineOutcome) =
    let event = Budgeting.Events.goalStepEvent { DecisionEventId = changeId; Step = step } outcome
    Budgeting.Persistence.appendOn conn (Budgeting.Events.TargetStream categoryId) [ DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), event ]
    |> ignore

/// Ein Ereignis im Target-Stream, `at` wie bei `change` (budgeting-eh8xe).
let private appendTarget (conn: Microsoft.Data.Sqlite.SqliteConnection) (categoryId: Guid) (at: string) (event: Budgeting.Events.BudgetEvent) =
    Budgeting.Persistence.appendOn conn (Budgeting.Events.TargetStream categoryId) [ utcOf at, event ] |> ignore

let private rule : Target = { Goal = eur 50m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }

let private edited = TargetEdited
let private removedKind = TargetRemoved

[<Tests>]
let lastChangeTests =
    testList "Targets prüfen: letzte Target-Änderung je Kategorie" [
        testCase "Von zwei geschriebenen gilt die jüngere, eine jüngere mit fehlgeschlagenem Schritt 1 zählt nicht" <| fun () ->
            use conn = openChangesDb ()
            let k = Guid.NewGuid()
            result conn (change conn k "2026-06-10T08:00:00.0000000Z" edited) 1 Written
            result conn (change conn k "2026-07-10T08:00:00.0000000Z" edited) 1 Written
            let failed = change conn k "2026-08-10T08:00:00.0000000Z" edited
            result conn failed 1 Written
            result conn failed 1 (Failed "Rate-Limit")
            Expect.equal
                (Budgeting.Persistence.queryLastTargetChanges conn |> Result.map (Map.tryFind k))
                (Ok (Some (TargetSetOn "2026-07-10")))
                "die jüngere geschriebene"

        testCase "Eine geschriebene mit fehlgeschlagenem Schritt 2 zählt, Entfernt kommt als Entfernen" <| fun () ->
            use conn = openChangesDb ()
            let partly, removed = Guid.NewGuid(), Guid.NewGuid()
            let c = change conn partly "2026-07-10T08:00:00.0000000Z" (TargetCreated Monthly)
            result conn c 1 Written
            result conn c 2 (Failed "kaputt")
            result conn (change conn removed "2026-07-11T08:00:00.0000000Z" removedKind) 1 Written
            match Budgeting.Persistence.queryLastTargetChanges conn with
            | Error message -> failtest message
            | Ok marks ->
                Expect.equal (marks |> Map.tryFind partly) (Some (TargetSetOn "2026-07-10")) "Schritt 2 zählt nicht"
                Expect.equal (marks |> Map.tryFind removed) (Some (TargetRemovedOn "2026-07-11")) "Entfernen"

        testCase "occurred_at 2026-07-31T22:30Z ist in Berlin der 01.08.2026" <| fun () ->
            use conn = openChangesDb ()
            let k = Guid.NewGuid()
            result conn (change conn k "2026-07-31T22:30:00.0000000Z" edited) 1 Written
            Expect.equal
                (Budgeting.Persistence.queryLastTargetChanges conn |> Result.map (Map.tryFind k))
                (Ok (Some (TargetSetOn "2026-08-01")))
                "Berliner Kalendertag"

        testCase "Ein nicht lesbares Schritt-Ergebnis wird gemeldet, nicht übersprungen" <| fun () ->
            use conn = openChangesDb ()
            let k = Guid.NewGuid()
            result conn (change conn k "2026-07-10T08:00:00.0000000Z" edited) 1 Written
            BudgetEventsFixture.appendUnreadable conn (Budgeting.Events.TargetStream k) "YnabGoalStepWrittenToYnab"
            Expect.isError (Budgeting.Persistence.queryLastTargetChanges conn) "kaputte Payload"

        testCase "Bei gleichem occurred_at gilt die später festgehaltene Änderung (stream_version)" <| fun () ->
            use conn = openChangesDb ()
            let removedLast, editedLast = Guid.NewGuid(), Guid.NewGuid()
            let at = "2026-07-10T08:00:00.0000000Z"
            result conn (change conn removedLast at edited) 1 Written
            result conn (change conn removedLast at removedKind) 1 Written
            result conn (change conn editedLast at removedKind) 1 Written
            result conn (change conn editedLast at edited) 1 Written
            match Budgeting.Persistence.queryLastTargetChanges conn with
            | Error message -> failtest message
            | Ok marks ->
                Expect.equal (marks |> Map.tryFind removedLast) (Some (TargetRemovedOn "2026-07-10")) "Entfernen zuletzt eingefügt"
                Expect.equal (marks |> Map.tryFind editedLast) (Some (TargetSetOn "2026-07-10")) "Ändern zuletzt eingefügt"

        // budgeting-eh8xe, ADR 0082 Punkt 7 mit Nachtrag: die letzte Änderung kommt aus TargetSet oder TargetRemoved,
        // die Übernahme zählt nicht.
        testCase "TargetSet und TargetRemoved zählen, jünger als der Altbestand, die Übernahme zählt nicht" <| fun () ->
            use conn = openChangesDb ()
            let set, removed, takenOver = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            result conn (change conn set "2026-06-10T08:00:00.0000000Z" edited) 1 Written
            appendTarget conn set "2026-09-02T08:00:00.0000000Z" (Budgeting.Events.BudgetEvent.TargetSet { Rule = rule; EffectiveFrom = "2026-09" })
            appendTarget conn removed "2026-09-03T08:00:00.0000000Z" (Budgeting.Events.BudgetEvent.TargetTakenOverFromYnab { CategoryName = "X"; Rule = rule; YnabGoalType = "NEED" })
            appendTarget conn removed "2026-09-04T08:00:00.0000000Z" (Budgeting.Events.BudgetEvent.TargetRemoved { EffectiveFrom = "2026-09" })
            appendTarget conn takenOver "2026-09-05T08:00:00.0000000Z" (Budgeting.Events.BudgetEvent.TargetTakenOverFromYnab { CategoryName = "X"; Rule = rule; YnabGoalType = "NEED" })
            match Budgeting.Persistence.queryLastTargetChanges conn with
            | Error message -> failtest message
            | Ok marks ->
                Expect.equal (marks |> Map.tryFind set) (Some (TargetSetOn "2026-09-02")) "gesetzt, jünger als der Altbestand"
                Expect.equal (marks |> Map.tryFind removed) (Some (TargetRemovedOn "2026-09-04")) "entfernt"
                Expect.equal (marks |> Map.tryFind takenOver) None "die Übernahme ist keine Änderung"

        testCase "loadLastTargetChangesWith meldet ein nicht lesbares Ereignis mit TargetChangesUnreadableText" <| fun () ->
            let connect () =
                let conn = openChangesDb ()
                BudgetEventsFixture.appendUnreadable conn (Budgeting.Events.TargetStream (Guid.NewGuid())) "YnabGoalChangedFromBb"
                conn
            Expect.equal
                (Budgeting.Persistence.loadLastTargetChangesWith connect |> run)
                (Error Budgeting.Persistence.TargetChangesUnreadableText)
                "Text statt Ursache"
    ]

// ============================================
// Handler über den Port
// ============================================

type private Probe = {
    Port: Server.Budgeting.QueryHandlers.TargetCheckPort
    ActivityCalls: ResizeArray<Guid list * DateTime list>
    FirstBookingCalls: ResizeArray<Guid list>
}

let private probe (categories: YnabMonthCategory list) : Probe =
    let calls = ResizeArray()
    let firstCalls = ResizeArray()
    { Port =
        { FetchMonth = fun month -> async { return Ok (monthWith categories month) }
          LoadMonthlyActivity =
            fun ids firstDays ->
                async {
                    calls.Add((ids, firstDays))
                    return Map.empty
                }
          LoadFirstBookings =
            fun ids ->
                async {
                    firstCalls.Add ids
                    return Map.empty
                }
          LoadTargets = fun raw -> async { return Ok (BudgetEventsFixture.ynabTargets raw) }
          LoadLastTargetChanges = fun () -> async { return Ok Map.empty } }
      ActivityCalls = calls
      FirstBookingCalls = firstCalls }

[<Tests>]
let handlerTests =
    // Ohne Kassenbuch-Schema: der Handler fragt das Kassenbuch nur über den Port. Das Budgeting-Schema
    // braucht der Test zu den Pausen.
    let testCase name body =
        testCase name (fun () ->
            Persistence.initializeDatabase ()
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())
    testList "Targets prüfen: Handler über den Port" [
        testCase "Ausgeblendete, gelöschte und interne Kategorien fehlen, das Kassenbuch wird einmal für alle gefragt" <| fun () ->
            let visible = { ynabCategory "Lebensmittel" with Target = Some { Goal = eur 300m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None } }
            let other = { ynabCategory "Urlaub" with GroupName = "Rücklagen" }
            let hidden = { ynabCategory "Versteckt" with Hidden = true }
            let deleted = { ynabCategory "Gelöscht" with Deleted = true }
            let internalOne = { ynabCategory "Inflow: Ready to Assign" with Internal = true }
            let p = probe [ visible; hidden; other; deleted; internalOne ]
            match Server.Budgeting.QueryHandlers.targetCheck p.Port today |> run with
            | Error message -> failtest message
            | Ok data ->
                Expect.equal (data.Month, data.Today) ("2026-11-01", "2026-11-05") "Monat und Berliner Tag"
                Expect.equal
                    (data.Categories |> List.map (fun c -> c.Name, c.GroupName, c.Target) |> List.sort)
                    ([ "Lebensmittel", "Alltag", visible.Target; "Urlaub", "Rücklagen", None ] |> List.sort)
                    "nur sichtbare, mit Gruppe und Target"
                Expect.equal p.ActivityCalls.Count 1 "genau ein Aufruf"
                let ids, firstDays = p.ActivityCalls.[0]
                Expect.equal (Set.ofList ids) (Set.ofList [ visible.Id; other.Id ]) "alle sichtbaren Kategorien"
                Expect.equal firstDays months "die 12 vollen Monate"

        testCase "Die letzte Änderung hängt an ihrer Kategorie" <| fun () ->
            let category = ynabCategory "Lebensmittel"
            let mark = TargetRemovedOn "2026-08-01"
            let p = probe [ category; ynabCategory "Urlaub" ]
            let port = { p.Port with LoadLastTargetChanges = fun () -> async { return Ok (Map.ofList [ category.Id, mark ]) } }
            match Server.Budgeting.QueryHandlers.targetCheck port today |> run with
            | Error message -> failtest message
            | Ok data ->
                Expect.equal
                    (data.Categories |> List.map (fun c -> c.Name, c.LastChange) |> List.sort)
                    [ "Lebensmittel", Some mark; "Urlaub", None ]
                    "je Kategorie"

        testCase "Die erste Buchung hängt als Berliner Tag an ihrer Kategorie, einmal für alle gefragt (budgeting-k7r2w)" <| fun () ->
            let strom = ynabCategory "Strom"
            let never = ynabCategory "Urlaub"
            let p = probe [ strom; never ]
            let port = { p.Port with LoadFirstBookings = fun ids -> async { p.FirstBookingCalls.Add ids; return Map.ofList [ strom.Id, DateTime(2026, 6, 3) ] } }
            match Server.Budgeting.QueryHandlers.targetCheck port today |> run with
            | Error message -> failtest message
            | Ok data ->
                Expect.equal
                    (data.Categories |> List.map (fun c -> c.Name, c.FirstBooking) |> List.sort)
                    [ "Strom", Some "2026-06-03"; "Urlaub", None ]
                    "je Kategorie, ohne Buchung None"
                Expect.equal p.FirstBookingCalls.Count 1 "genau ein Aufruf"
                Expect.equal (Set.ofList p.FirstBookingCalls.[0]) (Set.ofList [ strom.Id; never.Id ]) "alle sichtbaren Kategorien"

        testCase "Die BB-Pausen liest der Handler nicht: eine unlesbare Pause des Monats ändert nichts" <| fun () ->
            // Eine eigene Datenbank: das unlesbare Ereignis träfe in der geteilten jeden anderen Leser.
            let monthKey = "2093-04-01"
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            BudgetEventsFixture.appendUnknown anchor (Budgeting.Events.MonthStream "2093-04")
            let p = probe [ ynabCategory "Lebensmittel" ]
            let read =
                Server.Budgeting.QueryHandlers.budgetMonthWith
                    connect
                    { FetchMonth = p.Port.FetchMonth
                      SetAssigned = fun _ _ _ -> async { return failwith "kein Schreiben" }
                      FetchCategoryGoals = fun () -> async { return Ok [] } }
                    (DateTime(2093, 4, 15, 8, 0, 0, DateTimeKind.Utc))
                    monthKey
            Expect.equal
                (read |> run |> Result.isError)
                true
                "Gegenprobe: mit Pausen scheitert der Monat"
            match Server.Budgeting.QueryHandlers.targetCheck p.Port (DateOnly(2093, 4, 15)) |> run with
            | Error message -> failtest message
            | Ok data -> Expect.equal (data.Categories |> List.map (fun c -> c.Name)) [ "Lebensmittel" ] "geprüft wie sonst"

        testCase "Lesefehler im Kassenbuch, bei den Target-Änderungen und bei YNAB kommen als Error mit Text" <| fun () ->
            let p = probe [ ynabCategory "Lebensmittel" ]
            let check port = Server.Budgeting.QueryHandlers.targetCheck port today |> run
            Expect.equal
                (check { p.Port with LoadMonthlyActivity = fun _ _ -> async { return failwith "Datenbank weg" } })
                (Error Server.Budgeting.QueryHandlers.SpendingUnreadableText)
                "Kassenbuch"
            Expect.equal
                (check { p.Port with LoadFirstBookings = fun _ -> async { return failwith "Datenbank weg" } })
                (Error Server.Budgeting.QueryHandlers.SpendingUnreadableText)
                "erste Buchungen"
            Expect.equal
                (check { p.Port with LoadLastTargetChanges = fun () -> async { return failwith "Datenbank weg" } })
                (Error Budgeting.Persistence.TargetChangesUnreadableText)
                "Target-Änderungen werfen"
            Expect.equal
                (check { p.Port with LoadLastTargetChanges = fun () -> async { return Error Budgeting.Persistence.TargetChangesUnreadableText } })
                (Error Budgeting.Persistence.TargetChangesUnreadableText)
                "Target-Änderungen melden"
            Expect.equal
                (check { p.Port with FetchMonth = fun _ -> async { return Error (YnabError.RateLimitExceeded 30) } })
                (Error (ynabErrorText (YnabError.RateLimitExceeded 30)))
                "YNAB mit seinem Text"
    ]

[<Tests>]
let endpointTests =
    testList "Targets prüfen: Endpoint" [
        testCase "getTargetCheck liest den Monat über den YNAB-Port, die Ausgaben aus dem Kassenbuch und den Berliner Tag" <| fun () ->
            initialize ()
            let category = ynabCategory "Kategorie E P2zr6"
            booked (catRef category.Id category.Name) (utc 2026 10 3) -12m
            // Eine Änderung in BB über den echten Schreibweg (budgeting-eh8xe).
            Budgeting.Persistence.recordTargetChangeOn
                (global.Persistence.getConnection ())
                None
                (DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc))
                category.Id
                (Budgeting.Events.BudgetEvent.TargetSet { Rule = rule; EffectiveFrom = "2026-08" })
            |> ignore
            let fetched = ResizeArray<string>()
            let ynab : Server.Budgeting.CommandHandlers.YnabPort =
                { FetchMonth = fun month -> async { fetched.Add month; return Ok (monthWith [ category ] month) }
                  SetAssigned = fun _ _ _ -> async { return failwith "Targets prüfen schreibt nicht" }
                  FetchCategoryGoals = fun () -> async { return Ok [] } }
            let api =
                Server.Api.budgetApiWith
                    (fun _ -> async { return Some "eingerichtet" })
                    (fun _ _ -> ynab)
                    (fun _ _ -> failwith "kein Rahmen")
                    // 23:30 UTC am 31.10. ist in Berlin schon der 01.11.
                    (fun () -> DateTime(2026, 10, 31, 23, 30, 0, DateTimeKind.Utc))
                    (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
            match api.getTargetCheck () |> run with
            | Error message -> failtest message
            | Ok data ->
                Expect.equal (data.Month, data.Today) ("2026-11-01", "2026-11-01") "Berliner Tag"
                Expect.equal (List.ofSeq fetched) [ "2026-11-01" ] "ein YNAB-Lesen des laufenden Monats"
                let only = data.Categories |> List.exactlyOne
                Expect.equal (List.last only.Spending) { Month = "2026-10-01"; Activity = eur -12m } "Ausgaben aus dem Kassenbuch"
                Expect.equal only.LastChange (Some (TargetSetOn "2026-08-10")) "letzte Änderung aus events"
                Expect.equal only.FirstBooking (Some "2026-10-03") "erste Buchung aus dem Kassenbuch"
                // YNAB zeigt kein Goal: das Target kommt aus BBs Regeln (Anschluss `liveTargetCheckPort.LoadTargets`).
                Expect.equal only.Target (Some rule) "BBs Regel"
    ]
