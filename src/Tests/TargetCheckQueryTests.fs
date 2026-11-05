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
      Underfunded = None
      Target = None
      Snoozed = false
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
                { Server.Budgeting.QueryHandlers.liveTargetCheckPort (fun month -> async { return Ok (monthWith [ category ] month) }) with
                    LoadLastTargetChanges = fun () -> async { return Ok Map.empty } }
            match Server.Budgeting.QueryHandlers.targetCheck port today |> run with
            | Error message -> failtest message
            | Ok data ->
                let spending = (data.Categories |> List.exactlyOne).Spending
                Expect.equal (spending |> List.map (fun s -> s.Month)) ([ for m in months -> m.ToString("yyyy-MM-01") ]) "November 2025 bis Oktober 2026"
                Expect.equal (List.head months) (DateTime(2025, 11, 1)) "erster Monat"
                Expect.equal (spending |> List.map (fun s -> s.Activity.Amount)) (List.replicate 11 0m @ [ -12m ]) "0 ohne Buchung"
    ]

// ============================================
// Letzte Target-Änderung je Kategorie (eigene Verbindung)
// ============================================

/// Die beiden Tabellen in ihrer heutigen Form auf einer eigenen Datenbank: eine unbekannte Art bliebe in der
/// geteilten sonst für andere Tests stehen.
let private openChangesDb () =
    let conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
    conn.Open()
    Dapper.SqlMapper.Execute(
        conn,
        "CREATE TABLE target_changes (id TEXT PRIMARY KEY, occurred_at TEXT NOT NULL, month TEXT NOT NULL, \
            category_id TEXT NOT NULL, category_name TEXT NOT NULL, change_kind TEXT NOT NULL, \
            old_goal_target INTEGER, new_goal_target INTEGER, old_date TEXT, new_date TEXT, new_frequency TEXT); \
         CREATE TABLE target_change_results (sequence INTEGER PRIMARY KEY AUTOINCREMENT, change_id TEXT NOT NULL, \
            occurred_at TEXT NOT NULL, status TEXT NOT NULL, reason TEXT, step INTEGER)")
    |> ignore
    conn

let private change (conn: Microsoft.Data.Sqlite.SqliteConnection) (categoryId: Guid) (at: string) (kind: string) : Guid =
    let id = Guid.NewGuid()
    Dapper.SqlMapper.Execute(
        conn,
        "INSERT INTO target_changes (id, occurred_at, month, category_id, category_name, change_kind) \
         VALUES (@id, @at, '2026-07-01', @category, 'X', @kind)",
        {| id = string id; at = at; category = string categoryId; kind = kind |})
    |> ignore
    id

/// Ein Ergebnis zu einem Schritt, `None` wie aus budgeting-k8wq3 ohne Schritt.
let private result (conn: Microsoft.Data.Sqlite.SqliteConnection) (changeId: Guid) (step: int option) (status: string) =
    Dapper.SqlMapper.Execute(
        conn,
        "INSERT INTO target_change_results (change_id, occurred_at, status, reason, step) \
         VALUES (@id, '2026-07-01T10:00:00.0000000Z', @status, NULL, @step)",
        {| id = string changeId; status = status; step = (match step with Some s -> box s | None -> null) |})
    |> ignore

let private edited = fst (Server.Budgeting.Domain.targetChangeKindText TargetEdited)
let private removedKind = fst (Server.Budgeting.Domain.targetChangeKindText TargetRemoved)

[<Tests>]
let lastChangeTests =
    testList "Targets prüfen: letzte Target-Änderung je Kategorie" [
        testCase "Von zwei geschriebenen gilt die jüngere, eine jüngere mit fehlgeschlagenem Schritt 1 zählt nicht" <| fun () ->
            use conn = openChangesDb ()
            let k = Guid.NewGuid()
            result conn (change conn k "2026-06-10T08:00:00.0000000Z" edited) (Some 1) "written"
            result conn (change conn k "2026-07-10T08:00:00.0000000Z" edited) None "written"
            let failed = change conn k "2026-08-10T08:00:00.0000000Z" edited
            result conn failed (Some 1) "written"
            result conn failed (Some 1) "failed"
            Expect.equal
                (Budgeting.Persistence.queryLastTargetChanges conn |> Result.map (Map.tryFind k))
                (Ok (Some (TargetSetOn "2026-07-10")))
                "die jüngere geschriebene, ein Ergebnis ohne Schritt zählt als Schritt 1"

        testCase "Eine geschriebene mit fehlgeschlagenem Schritt 2 zählt, Entfernt kommt als Entfernen" <| fun () ->
            use conn = openChangesDb ()
            let partly, removed = Guid.NewGuid(), Guid.NewGuid()
            let createdText, frequency = Server.Budgeting.Domain.targetChangeKindText (TargetCreated Monthly)
            let c = change conn partly "2026-07-10T08:00:00.0000000Z" createdText
            Dapper.SqlMapper.Execute(conn, "UPDATE target_changes SET new_frequency = @f WHERE id = @id", {| id = string c; f = frequency.Value |}) |> ignore
            result conn c (Some 1) "written"
            result conn c (Some 2) "failed"
            result conn (change conn removed "2026-07-11T08:00:00.0000000Z" removedKind) (Some 1) "written"
            match Budgeting.Persistence.queryLastTargetChanges conn with
            | Error message -> failtest message
            | Ok marks ->
                Expect.equal (marks |> Map.tryFind partly) (Some (TargetSetOn "2026-07-10")) "Schritt 2 zählt nicht"
                Expect.equal (marks |> Map.tryFind removed) (Some (TargetRemovedOn "2026-07-11")) "Entfernen"

        testCase "occurred_at 2026-07-31T22:30Z ist in Berlin der 01.08.2026" <| fun () ->
            use conn = openChangesDb ()
            let k = Guid.NewGuid()
            result conn (change conn k "2026-07-31T22:30:00.0000000Z" edited) (Some 1) "written"
            Expect.equal
                (Budgeting.Persistence.queryLastTargetChanges conn |> Result.map (Map.tryFind k))
                (Ok (Some (TargetSetOn "2026-08-01")))
                "Berliner Kalendertag"

        testCase "Eine unbekannte Art wird gemeldet, nicht auf einen Standard gelegt" <| fun () ->
            use conn = openChangesDb ()
            result conn (change conn (Guid.NewGuid()) "2026-07-10T08:00:00.0000000Z" "Quatsch") (Some 1) "written"
            Expect.isError (Budgeting.Persistence.queryLastTargetChanges conn) "unbekannte Art"

        testCase "Bei gleichem occurred_at gilt die später eingefügte Zeile (rowid)" <| fun () ->
            use conn = openChangesDb ()
            let removedLast, editedLast = Guid.NewGuid(), Guid.NewGuid()
            let at = "2026-07-10T08:00:00.0000000Z"
            result conn (change conn removedLast at edited) (Some 1) "written"
            result conn (change conn removedLast at removedKind) (Some 1) "written"
            result conn (change conn editedLast at removedKind) (Some 1) "written"
            result conn (change conn editedLast at edited) (Some 1) "written"
            match Budgeting.Persistence.queryLastTargetChanges conn with
            | Error message -> failtest message
            | Ok marks ->
                Expect.equal (marks |> Map.tryFind removedLast) (Some (TargetRemovedOn "2026-07-10")) "Entfernen zuletzt eingefügt"
                Expect.equal (marks |> Map.tryFind editedLast) (Some (TargetSetOn "2026-07-10")) "Ändern zuletzt eingefügt"

        testCase "loadLastTargetChangesWith meldet eine unbekannte Art mit TargetChangesUnreadableText" <| fun () ->
            let connect () =
                let conn = openChangesDb ()
                result conn (change conn (Guid.NewGuid()) "2026-07-10T08:00:00.0000000Z" "Quatsch") (Some 1) "written"
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
}

let private probe (categories: YnabMonthCategory list) : Probe =
    let calls = ResizeArray()
    { Port =
        { FetchMonth = fun month -> async { return Ok (monthWith categories month) }
          LoadMonthlyActivity =
            fun ids firstDays ->
                async {
                    calls.Add((ids, firstDays))
                    return Map.empty
                }
          LoadLastTargetChanges = fun () -> async { return Ok Map.empty } }
      ActivityCalls = calls }

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
            let visible = { ynabCategory "Lebensmittel" with Target = Some { Goal = eur 300m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None } }
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

        testCase "Die BB-Pausen liest der Handler nicht: eine unlesbare Pause des Monats ändert nichts" <| fun () ->
            // Ein eigener Monat weit in der Zukunft: die unbekannte Art bleibt in der geteilten DB stehen.
            let monthKey = "2093-04-01"
            use conn = global.Persistence.getConnection ()
            Dapper.SqlMapper.Execute(
                conn,
                "INSERT INTO target_pause_events (id, occurred_at, month, category_id, category_name, kind) \
                 VALUES (@id, '2093-04-02T09:00:00.0000000Z', @month, @k, 'X', 'Quatsch')",
                {| id = string (Guid.NewGuid()); month = monthKey; k = string (Guid.NewGuid()) |})
            |> ignore
            let p = probe [ ynabCategory "Lebensmittel" ]
            Expect.equal
                (Server.Budgeting.QueryHandlers.budgetMonthOf p.Port.FetchMonth monthKey |> run |> Result.isError)
                true
                "Gegenprobe: mit Pausen scheitert der Monat"
            match Server.Budgeting.QueryHandlers.targetCheck p.Port (DateOnly(2093, 4, 15)) |> run with
            | Error message -> failtest message
            | Ok data -> Expect.equal (data.Categories |> List.map (fun c -> c.Name)) [ "Lebensmittel" ] "geprüft wie sonst"

        testCase "Lesefehler im Kassenbuch, in target_changes und bei YNAB kommen als Error mit Text" <| fun () ->
            let p = probe [ ynabCategory "Lebensmittel" ]
            let check port = Server.Budgeting.QueryHandlers.targetCheck port today |> run
            Expect.equal
                (check { p.Port with LoadMonthlyActivity = fun _ _ -> async { return failwith "Datenbank weg" } })
                (Error Server.Budgeting.QueryHandlers.SpendingUnreadableText)
                "Kassenbuch"
            Expect.equal
                (check { p.Port with LoadLastTargetChanges = fun () -> async { return failwith "Datenbank weg" } })
                (Error Budgeting.Persistence.TargetChangesUnreadableText)
                "target_changes wirft"
            Expect.equal
                (check { p.Port with LoadLastTargetChanges = fun () -> async { return Error Budgeting.Persistence.TargetChangesUnreadableText } })
                (Error Budgeting.Persistence.TargetChangesUnreadableText)
                "target_changes meldet"
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
            // Eine in BB geschriebene Änderung über den echten Persistenzweg (Schritt 1 geschrieben).
            let written : Budgeting.Persistence.TargetChange =
                { Id = Guid.NewGuid()
                  OccurredAt = DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc)
                  Month = "2026-08-01"
                  CategoryId = category.Id
                  CategoryName = category.Name
                  Kind = TargetEdited
                  OldGoalTarget = Some 50000L
                  NewGoalTarget = Some 60000L
                  OldDate = None
                  NewDate = None }
            Budgeting.Persistence.appendTargetChanges [ written ] |> run
            Budgeting.Persistence.appendTargetChangeStepResult written.Id 1 written.OccurredAt Written |> run
            let fetched = ResizeArray<string>()
            let ynab : Server.Budgeting.CommandHandlers.YnabPort =
                { FetchMonth = fun month -> async { fetched.Add month; return Ok (monthWith [ category ] month) }
                  SetAssigned = fun _ _ _ -> async { return failwith "Targets prüfen schreibt nicht" }
                  SetTarget = fun _ _ -> async { return failwith "Targets prüfen schreibt nicht" } }
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
                Expect.equal only.LastChange (Some (TargetSetOn "2026-08-10")) "letzte Änderung aus target_changes"
    ]
