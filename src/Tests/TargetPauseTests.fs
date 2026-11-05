module Tests.TargetPauseTests

// budgeting-t6p8z, ADR 0073, ADR 0079: Target pausieren und Pause aufheben. Reine Regeln, die Abläufe
// gegen In-Memory-SQLite mit einem Fake-YNAB, der nie das Netz erreicht, die Grenze und die Historie.

open System
open System.Collections.Generic

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.CommandHandlers

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private target : Target = { Goal = eur 100m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None }

let private run x = Async.RunSynchronously x

// ============================================
// Reine Regeln
// ============================================

let private ynabCategory (id: Guid) (name: string) (snoozed: bool) : YnabMonthCategory =
    { Id = id
      Name = name
      GroupName = "Alltag"
      Assigned = eur 0m
      Activity = eur 0m
      Available = eur 0m
      Underfunded = Some (eur 50m)
      Target = Some target
      Snoozed = snoozed
      Hidden = false
      Deleted = false
      Internal = false }

let private pauseOf (month: BudgetMonth) (id: Guid) : PauseSource option =
    (month.Groups |> List.collect (fun g -> g.Categories) |> List.find (fun c -> c.Id = id)).Pause

let private budgetCategory (available: decimal) (under: decimal option) (pause: PauseSource option) : BudgetCategory =
    { Id = Guid.NewGuid()
      Name = "Ausflug"
      Assigned = eur 0m
      Activity = eur 0m
      Available = eur available
      Underfunded = under |> Option.map eur
      Target = Some target
      Pause = pause }

let private sources = [ PausedInYnab; PausedInBb; PausedInBoth ]

let private pauseEvent (id: Guid) : HistoryPause =
    { EventId = id
      OccurredAt = DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)
      Month = "2026-11-01"
      CategoryId = Guid.NewGuid()
      CategoryName = "Ausflug" }

let private domainTests =
    testList "Pause: reine Regeln" [
        test "toBudgetMonth führt BB- und YNAB-Pause zur Quelle zusammen" {
            let onlyBb, onlyYnab, both, none = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let raw : YnabMonth =
                { Month = "2026-11-01"
                  ToBeBudgeted = eur 0m
                  Categories =
                    [ ynabCategory onlyBb "nur BB" false
                      ynabCategory onlyYnab "nur YNAB" true
                      ynabCategory both "beide" true
                      ynabCategory none "keine" false ] }
            let month = Domain.toBudgetMonth (Set.ofList [ onlyBb; both ]) raw
            Expect.equal (pauseOf month onlyBb) (Some PausedInBb) "nur in BB"
            Expect.equal (pauseOf month onlyYnab) (Some PausedInYnab) "nur in YNAB"
            Expect.equal (pauseOf month both) (Some PausedInBoth) "beide"
            Expect.equal (pauseOf month none) None "keine"
        }
        test "Eine BB-Pause für eine Kategorie, die im Monat fehlt, ändert nichts" {
            let raw : YnabMonth =
                { Month = "2026-11-01"; ToBeBudgeted = eur 0m; Categories = [ ynabCategory (Guid.NewGuid()) "da" false ] }
            Expect.equal (Domain.toBudgetMonth (Set.ofList [ Guid.NewGuid() ]) raw) (Domain.toBudgetMonth Set.empty raw) "gleich"
        }
        test "Bei jeder Quelle zählt der Fehlbetrag in Fehlt und Frei nicht" {
            for source in sources do
                let c = budgetCategory 10m (Some 50m) (Some source)
                Expect.equal (missingAmount c) None $"Fehlt bei {source}"
                Expect.equal (freeAmount c) (eur 10m) $"Frei bei {source}"
            let open' = budgetCategory 10m (Some 50m) None
            Expect.equal (missingAmount open') (Some (eur 50m)) "ohne Pause zählt er"
            Expect.equal (freeAmount open') (eur -40m) "ohne Pause sperrt er"
        }
        test "Bei jeder Quelle bleibt negatives Verfügbar in Fehlt" {
            for source in sources do
                Expect.equal (missingAmount (budgetCategory -5m (Some 50m) (Some source))) (Some (eur 5m)) $"{source}"
        }
        test "Targets anwenden überspringt eine in BB pausierte Kategorie mit Fehlbetrag" {
            let paused = budgetCategory 0m (Some 50m) (Some PausedInBb)
            let open' = { budgetCategory 0m (Some 20m) None with Id = Guid.NewGuid(); Name = "Essen" }
            let month = { Month = "2026-11-01"; ToBeBudgeted = eur 500m; Groups = [ { Name = "Alltag"; Categories = [ paused; open' ] } ] }
            match Domain.planTargetApplication month with
            | Ok preview ->
                Expect.equal (preview.Lines |> List.map (fun l -> l.CategoryId)) [ open'.Id ] "nicht in den Zeilen"
                Expect.equal (preview.Skipped |> List.map (fun l -> l.CategoryId)) [ paused.Id ] "übersprungen"
            | Error e -> failtest e
        }
        // budgeting-h7q4v (H1): die Pause trug den Stand "geschrieben", obwohl sie nie nach YNAB geht.
        test "Eine Pause steht in der Historie mit Text, ohne Betrag und ohne Stand" {
            let id = Guid.NewGuid()
            let paused = historyEntryLine (TargetPaused (pauseEvent id))
            Expect.equal (paused.EntryId, paused.Text, paused.Change, paused.Status) (id, "Target pausiert", None, None) "pausiert"
            let lifted = historyEntryLine (PauseLifted (pauseEvent id))
            Expect.equal (lifted.Text, lifted.Change, lifted.Status) ("Pause aufgehoben", None, None) "aufgehoben"
        }
        test "Ein unbekannter Text der Art wird gemeldet" {
            Expect.isError (Budgeting.EventMigration.pauseKindOfText "Irgendwas") "kein Standard"
            for kind in [ Domain.PauseKind.Paused; Domain.PauseKind.PauseLifted ] do
                Expect.equal (Budgeting.EventMigration.pauseKindOfText (Budgeting.EventMigration.pauseKindText kind)) (Ok kind) $"{kind}"
        }
        // budgeting-r3wq8: ein Name je Ereignis. Geschrieben wird "Aufgehoben", der Altwert "Fortgesetzt" aus
        // lokalen Dev-Datenbanken wird weiter als Aufheben gelesen.
        test "Der Altwert Fortgesetzt wird weiter als Aufheben gelesen" {
            Expect.equal (Budgeting.EventMigration.pauseKindOfText "Fortgesetzt") (Ok Domain.PauseKind.PauseLifted) "Altwert"
        }
        // budgeting-r3wq8: eine BB-Pause ohne Target wirkt nicht und wird nicht gemeldet.
        test "toBudgetMonth meldet eine BB-Pause ohne Target nicht" {
            let k = Guid.NewGuid()
            let raw : YnabMonth =
                { Month = "2026-11-01"
                  ToBeBudgeted = eur 0m
                  Categories = [ { ynabCategory k "ohne Target" false with Target = None; Underfunded = None } ] }
            Expect.equal (pauseOf (Domain.toBudgetMonth (Set.ofList [ k ]) raw) k) None "keine Pause"
        }
    ]

// ============================================
// Abläufe gegen Fake-YNAB und In-Memory-SQLite
// ============================================

/// Ein Fake-YNAB: liefert den Monat aus der Liste, zählt jedes Lesen und jeden Schreibversuch.
/// Kategorien als (Id, Name, Fehlbetrag in Euro, mit Target, in YNAB pausiert, ausgeblendet).
type private FakeYnab(categories: (Guid * string * decimal * bool * bool * bool) list) =
    member val Reads = 0 with get, set
    member val Writes = ResizeArray<Guid * int64>()
    member this.Port : YnabPort =
        { FetchMonth =
            fun monthKey ->
                async {
                    this.Reads <- this.Reads + 1
                    return
                        Ok
                            { Month = monthKey
                              ToBeBudgeted = eur 1000m
                              Categories =
                                categories
                                |> List.map (fun (id, name, under, hasTarget, snoozed, hidden) ->
                                    { ynabCategory id name snoozed with
                                        Underfunded = (if hasTarget then Some (eur under) else None)
                                        Target = (if hasTarget then Some target else None)
                                        Hidden = hidden }) }
                }
          SetAssigned =
            fun _ id milliunits ->
                async {
                    this.Writes.Add((id, milliunits))
                    return Ok ()
                }
          SetTarget = fun _ _ -> async { return failwith "schreibt kein Target" } }

let private current = "2026-11-01"
let private now = DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)

/// Eine Kategorie mit Target und Fehlbetrag 50 €, frische Id je Test.
let private fakeWith (snoozed: bool) =
    let k = Guid.NewGuid()
    FakeYnab([ k, "Ausflug", 50m, true, snoozed, false ]), k

let private pause (f: FakeYnab) (month: string) (id: Guid) (intent: PauseIntent) =
    setTargetPause f.Port now current month { CategoryId = id; Intent = intent; CommandId = None } |> run

let private pauseOk f month id intent : PauseSource option =
    match pause f month id intent with
    | Ok (Ok result) -> result
    | other -> failtest $"Ok erwartet: {other}"

/// Die Pause-Ereignisse der Kategorie aus `events` (budgeting-hyn77): Monat "YYYY-MM-01" und die Art als
/// deutscher Text, in der Reihenfolge des Festhaltens.
let private eventRows (id: Guid) : (string * string) list =
    use conn = global.Persistence.getConnection ()
    match Budgeting.Persistence.loadBudgetEventsOn conn Budgeting.Persistence.WholeContext with
    | Error message -> failtest message
    | Ok events ->
        events
        |> List.sortBy (fun e -> e.OccurredAt, e.StreamVersion)
        |> List.choose (fun e ->
            match e.Event with
            | Budgeting.Events.BudgetEvent.TargetPaused p when p.CategoryId = id ->
                Some (Budgeting.Events.monthKeyOfId p.Month, Budgeting.EventMigration.pauseKindText Domain.PauseKind.Paused)
            | Budgeting.Events.BudgetEvent.TargetPauseLifted p when p.CategoryId = id ->
                Some (Budgeting.Events.monthKeyOfId p.Month, Budgeting.EventMigration.pauseKindText Domain.PauseKind.PauseLifted)
            | _ -> None)

let private monthOk (f: FakeYnab) (month: string) : BudgetMonth =
    match QueryHandlers.budgetMonth f.Port month |> run with
    | Ok m -> m
    | Error e -> failtest $"Monat erwartet: {e}"

let private categoryIn (month: BudgetMonth) (id: Guid) : BudgetCategory =
    month.Groups |> List.collect (fun g -> g.Categories) |> List.find (fun c -> c.Id = id)

let private flowTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    testSequenced
    <| testList "Pause: Abläufe" [
        testCase "GIVEN Target im laufenden Monat WHEN pausieren THEN ein Ereignis, der Monat zeigt die BB-Pause, YNAB wird nicht beschrieben" <| fun () ->
            let f, k = fakeWith false
            Expect.equal (pauseOk f current k PauseTarget) (Some PausedInBb) "Ergebnis"
            Expect.equal (eventRows k) [ current, "Pausiert" ] "ein Ereignis"
            let shown = categoryIn (monthOk f current) k
            Expect.equal shown.Pause (Some PausedInBb) "getBudgetMonth"
            Expect.equal (missingAmount shown) None "Fehlt ohne Fehlbetrag"
            Expect.isEmpty f.Writes "kein Schreibaufruf an YNAB"

        testCase "GIVEN BB-Pause im Monat M WHEN der Folgemonat gelesen wird THEN gilt sie dort nicht" <| fun () ->
            let f, k = fakeWith false
            pauseOk f current k PauseTarget |> ignore
            Expect.equal (categoryIn (monthOk f "2026-12-01") k).Pause None "endet mit dem Monat"

        testCase "Doppeltes Pausieren und Aufheben ohne BB-Pause schreiben keine Zeile" <| fun () ->
            let f, k = fakeWith false
            Expect.equal (pauseOk f current k LiftPause) None "Aufheben ohne Pause"
            Expect.isEmpty (eventRows k) "keine Zeile beim Aufheben"
            pauseOk f current k PauseTarget |> ignore
            Expect.equal (pauseOk f current k PauseTarget) (Some PausedInBb) "noch einmal pausiert"
            Expect.equal (eventRows k |> List.length) 1 "keine zweite Zeile"

        testCase "GIVEN Pause in BB und YNAB WHEN aufheben THEN Aufgehoben, die YNAB-Pause bleibt" <| fun () ->
            let f, k = fakeWith true
            Expect.equal (pauseOk f current k PauseTarget) (Some PausedInBoth) "beide"
            Expect.equal (pauseOk f current k LiftPause) (Some PausedInYnab) "YNAB bleibt"
            Expect.equal (eventRows k |> List.map snd) [ "Pausiert"; "Aufgehoben" ] "angehängt"
            Expect.equal (categoryIn (monthOk f current) k).Pause (Some PausedInYnab) "gelesen"

        testCase "GIVEN nur YNAB-Pause WHEN aufheben THEN keine Zeile, sie bleibt" <| fun () ->
            let f, k = fakeWith true
            Expect.equal (pauseOk f current k LiftPause) (Some PausedInYnab) "unverändert"
            Expect.isEmpty (eventRows k) "keine Zeile"

        testCase "Im vergangenen Monat abgelehnt, ohne YNAB zu lesen" <| fun () ->
            let f, k = fakeWith false
            match pause f "2026-10-01" k PauseTarget with
            | Ok (Error message) -> Expect.equal message PausePastMonthText "Grund"
            | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.isEmpty (eventRows k) "keine Zeile"
            Expect.equal f.Reads 0 "kein YNAB-Aufruf"

        testCase "Ein künftiger Monat im Fenster wird wie der laufende pausiert" <| fun () ->
            let f, k = fakeWith false
            Expect.equal (pauseOk f "2027-01-01" k PauseTarget) (Some PausedInBb) "Ergebnis"
            Expect.equal (eventRows k) [ "2027-01-01", "Pausiert" ] "Ereignis im künftigen Monat"

        testCase "Ohne Target, ausgeblendet oder unbekannt: Fehler mit Grund, keine Zeile" <| fun () ->
            let noTarget, hidden, unknown = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let f = FakeYnab([ noTarget, "Ohne Ziel", 0m, false, false, false; hidden, "Versteckt", 50m, true, false, true ])
            let reason id =
                match pause f current id PauseTarget with
                | Ok (Error message) -> message
                | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.equal (reason noTarget) "Ohne Ziel hat kein Target. Pausieren geht nur mit Target." "ohne Target"
            Expect.equal (reason hidden) Domain.CategoryNotVisibleText "ausgeblendet"
            Expect.equal (reason unknown) Domain.CategoryNotVisibleText "unbekannt"
            for id in [ noTarget; hidden; unknown ] do
                Expect.isEmpty (eventRows id) "keine Zeile"

        testCase "Eine Pause zwischen Vorschau und Übernehmen ergibt PreviewChanged, die neue Vorschau überspringt K" <| fun () ->
            let f, k = fakeWith false
            let shown =
                match preview f.Port current |> run with
                | Ok p -> p
                | Error e -> failtest $"{e}"
            Expect.equal (shown.Lines |> List.map (fun l -> l.CategoryId)) [ k ] "K in den Zeilen"
            pauseOk f current k PauseTarget |> ignore
            match apply f.Port now current shown |> run with
            | Ok (PreviewChanged fresh) ->
                Expect.isEmpty fresh.Lines "nichts anzuwenden"
                Expect.equal (fresh.Skipped |> List.map (fun l -> l.CategoryId)) [ k ] "K übersprungen"
            | other -> failtest $"PreviewChanged erwartet: {other}"
            Expect.isEmpty f.Writes "nichts geschrieben"

        testCase "Pausieren wartet auf die Sperre je Monat, wie Targets anwenden" <| fun () ->
            let f, k = fakeWith false
            // Ein eigener Monat: kein anderer Test hält oder liest ihn.
            let month = "2094-05-01"
            let holder = new System.Threading.SemaphoreSlim(0, 1)
            let release = new System.Threading.SemaphoreSlim(0, 1)
            // Die Sperre halten wie eine laufende Anwendung.
            let held =
                MonthGuard.exclusive month (async {
                    holder.Release() |> ignore
                    do! release.WaitAsync() |> Async.AwaitTask
                })
                |> Async.StartAsTask
            holder.Wait()
            let pausing = setTargetPause f.Port now current month { CategoryId = k; Intent = PauseTarget; CommandId = None } |> Async.StartAsTask
            Threading.Thread.Sleep 50
            Expect.equal f.Reads 0 "gelesen wird erst unter der Sperre"
            Expect.isEmpty (eventRows k) "noch kein Ereignis"
            release.Release() |> ignore
            held.Wait()
            pausing.Wait()
            Expect.equal f.Reads 1 "danach liest es einmal"
            Expect.equal (eventRows k) [ month, "Pausiert" ] "danach festgehalten"

        testCase "Die Historie mischt Bewegungen und Pause-Ereignisse der Kategorie im Monat, neueste zuerst" <| fun () ->
            let k = Guid.NewGuid()
            let at hour = DateTime(2026, 11, 4, hour, 0, 0, DateTimeKind.Utc)
            let movement hour : Budgeting.Persistence.Movement =
                { Id = Guid.NewGuid()
                  ApplicationId = Guid.NewGuid()
                  OccurredAt = at hour
                  Origin = MoneyAssigned
                  Month = current
                  CategoryId = k
                  CategoryName = "Ausflug"
                  OldAssigned = 0L
                  NewAssigned = 10000L }
            let event hour kind (month: string) : Guid =
                Budgeting.Persistence.recordPause None (at hour) month k "Ausflug" kind |> run |> BudgetEventsFixture.recordedOrFail
            let m1, m3 =
                match BudgetEventsFixture.recordGroups [ movement 1; movement 3 ] with
                | [ a; b ] -> a, b
                | other -> failtest $"zwei Zeilen erwartet, kamen {List.length other}"
            let p2 = event 2 Domain.PauseKind.Paused current
            let p4 = event 4 Domain.PauseKind.PauseLifted current
            event 5 Domain.PauseKind.Paused "2026-12-01" |> ignore
            let port = { QueryHandlers.liveHistoryPort with LoadMonthlyActivity = fun _ _ -> async { return [] } }
            match QueryHandlers.categoryHistory port k (DateTime(2026, 11, 1)) |> run with
            | Ok history ->
                let ids =
                    history.Entries
                    |> List.map (function
                        | Movement m -> "Bewegung", m.MovementId
                        | TargetPaused p -> "pausiert", p.EventId
                        | PauseLifted p -> "aufgehoben", p.EventId
                        | TargetChanged c -> "Target", c.ChangeId)
                Expect.equal ids [ "aufgehoben", p4; "Bewegung", m3.Id; "pausiert", p2; "Bewegung", m1.Id ] "gemischt, neueste zuerst"
            | Error e -> failtest e

        // budgeting-r3wq8, Bug: Aufheben verlangte ein Target. Wurde das Target nach dem Pausieren entfernt,
        // ließ sich die gespeicherte BB-Pause nicht mehr aufheben und kam mit einem neuen Target zurück.
        // GIVEN pausiert, dann Target weg WHEN aufheben THEN ein Ereignis "Aufgehoben"; ein neues Target zeigt
        // keine Pause.
        testCase "GIVEN BB-Pause, Target danach entfernt WHEN aufheben THEN aufgehoben, ein neues Target ist nicht pausiert" <| fun () ->
            let k = Guid.NewGuid()
            let withTarget = FakeYnab([ k, "Ausflug", 50m, true, false, false ])
            let withoutTarget = FakeYnab([ k, "Ausflug", 0m, false, false, false ])
            Expect.equal (pauseOk withTarget current k PauseTarget) (Some PausedInBb) "pausiert"
            Expect.equal (pauseOk withoutTarget current k LiftPause) None "aufgehoben ohne Target"
            Expect.equal (eventRows k |> List.map snd) [ "Pausiert"; "Aufgehoben" ] "festgehalten"
            Expect.equal (categoryIn (monthOk withTarget current) k).Pause None "neues Target ohne Pause"

        // Intakt: Pausieren verlangt weiter ein Target.
        testCase "GIVEN kein Target WHEN pausieren THEN abgelehnt, keine Zeile" <| fun () ->
            let k = Guid.NewGuid()
            let f = FakeYnab([ k, "Ohne Ziel", 0m, false, false, false ])
            match pause f current k PauseTarget with
            | Ok (Error message) -> Expect.equal message "Ohne Ziel hat kein Target. Pausieren geht nur mit Target." "Grund"
            | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.isEmpty (eventRows k) "keine Zeile"

        testCase "Monat nicht lesbar: außen Error, keine Zeile" <| fun () ->
            let f, k = fakeWith false
            let port : YnabPort =
                { f.Port with FetchMonth = fun _ -> async { return Error (YnabError.NetworkError "weg") } }
            match setTargetPause port now current current { CategoryId = k; Intent = PauseTarget; CommandId = None } |> run with
            | Error (YnabError.NetworkError "weg") -> ()
            | other -> failtest $"Lesefehler erwartet: {other}"
            Expect.isEmpty (eventRows k) "keine Zeile"

        // budgeting-r3wq8: ein nicht lesbares Pause-Ereignis ist ein Lesefehler mit Text, in der Historie ein
        // Fehler. Eine eigene Datenbank, damit das kaputte Ereignis keinen anderen Test trifft (budgeting-hyn77).
        // budgeting-c6vn4 (P1): verhindert, dass ein BB-eigener Lesefehler als "YNAB hat abgelehnt: ..." beim
        // Nutzer ankommt. Beim Monat und beim Pausieren ist er ein `LocalReadFailed`.
        testCase "Pausen nicht lesbar: Monat, Pausieren und Historie melden den Fehler als BB-Lesefehler" <| fun () ->
            let month = "2093-07-01"
            let f, k = fakeWith false
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            BudgetEventsFixture.appendUnknown anchor (Budgeting.Events.MonthStream "2093-07")
            let loadPaused = Budgeting.Persistence.loadPausedCategoriesWith connect
            let unreadable = YnabError.LocalReadFailed "Pausen nicht lesbar"
            Expect.equal (QueryHandlers.budgetMonthOfWith loadPaused f.Port.FetchMonth month |> run) (Error unreadable) "Monat"
            Expect.equal (ynabErrorText unreadable) "BB-Daten nicht lesbar: Pausen nicht lesbar" "Text für den Nutzer"
            Expect.equal
                (setTargetPauseWith loadPaused f.Port now current month { CategoryId = k; Intent = PauseTarget; CommandId = None } |> run)
                (Error unreadable)
                "Pausieren"
            let port =
                { QueryHandlers.liveHistoryPort with
                    LoadMonthlyActivity = fun _ _ -> async { return [] }
                    LoadPauseEvents = Budgeting.Persistence.loadPauseEventsWith connect }
            Expect.equal (QueryHandlers.categoryHistory port k (DateTime(2093, 7, 1)) |> run) (Error "Pause-Ereignisse nicht lesbar") "Historie"
    ]

let private persistenceTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())
    testSequenced
    <| testList "Pause: Persistenz" [
        testCase "Das Schema läuft zweimal, eine festgehaltene Pause bleibt" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            let k = Guid.NewGuid()
            Budgeting.Persistence.recordPause None now current k "A" Domain.PauseKind.Paused |> run |> ignore
            Budgeting.Persistence.initializeBudgetingSchema ()
            Expect.equal (Budgeting.Persistence.loadPausedCategories current |> run |> Result.map (Set.contains k)) (Ok true) "pausiert"

        // budgeting-hyn77: ein unbekannter Ereignistyp im Monats-Stream wird gemeldet, nicht übersprungen.
        testCase "Ein unbekannter Ereignistyp beim Lesen wird gemeldet, nicht übersprungen" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            Budgeting.Persistence.recordPauseOn anchor None now current (Guid.NewGuid()) "A" Domain.PauseKind.Paused |> ignore
            BudgetEventsFixture.appendUnknown anchor (Budgeting.Events.MonthStream "2026-11")
            Expect.equal (Budgeting.Persistence.loadPausedCategoriesWith connect current |> run) (Error "Pausen nicht lesbar") "unbekannter Typ"

        // budgeting-r3wq8: Stand und Historie ordnen das jüngste Ereignis gleich, nach der Reihenfolge des
        // Festhaltens (`stream_version`). Ein späteres Ereignis mit früherer Uhrzeit (Uhr verstellt) ist
        // trotzdem das jüngste.
        testCase "Stand und Historie nehmen dasselbe jüngste Ereignis, auch bei verstellter Uhr" <| fun () ->
            let month = "2092-03-01"
            let k = Guid.NewGuid()
            let event hour kind =
                Budgeting.Persistence.recordPause None (DateTime(2026, 11, 4, hour, 0, 0, DateTimeKind.Utc)) month k "A" kind |> run |> ignore
            event 10 Domain.PauseKind.Paused
            event 9 Domain.PauseKind.PauseLifted
            Expect.equal (Budgeting.Persistence.loadPausedCategories month |> run |> Result.map (Set.contains k)) (Ok false) "Stand: aufgehoben"
            match Budgeting.Persistence.loadPauseEvents k month |> run with
            | Ok (PauseLifted _ :: _) -> ()
            | other -> failtest $"Aufheben zuerst erwartet: {other}"
    ]

// ============================================
// Grenze
// ============================================

let private boundaryTests =
    let request id = { CategoryId = id; Intent = PauseTarget; CommandId = None }
    testList "Pause: Grenze" [
        test "Ungültiger Monat und leere Kategorie werden zusammen gemeldet" {
            match Server.Validation.validateTargetPauseRequest current "kein-monat" (request Guid.Empty) with
            | Error errors -> Expect.equal errors.Length 2 $"beide: {errors}"
            | Ok () -> failtest "Fehler erwartet"
        }
        test "Ein vergangener Monat wird an der Grenze abgelehnt" {
            Expect.equal
                (Server.Validation.validateTargetPauseRequest current "2026-10-01" (request (Guid.NewGuid())))
                (Error [ PausePastMonthText ])
                "vergangen"
            Expect.isOk (Server.Validation.validateTargetPauseRequest current "2026-11-01" (request (Guid.NewGuid()))) "laufend"
        }
        test "Der Live-Anschluss liest Token und Budget für Pausieren aus den Settings" {
            // Ohne Budget endet der Weg vor dem Netz: die Meldung zeigt, dass das Token ankam.
            let setting key = async { return (if key = "ynab_token" then Some "das-token" else None) }
            let api = Server.Api.liveBudgetApi setting (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
            let result = api.setTargetPause "2099-01-01" (request (Guid.NewGuid())) |> run
            Expect.equal result (Error "Kein Standard-Budget konfiguriert (Settings)") "bis zum Budget gekommen"
        }
        test "Über die BudgetApi: pausieren mit Uhr und Monatsart aus Berlin" {
            Budgeting.Persistence.initializeBudgetingSchema ()
            let f, k = fakeWith false
            let settings key = async { return Some $"wert-{key}" }
            let framePort _ _ : QueryHandlers.BudgetFramePort =
                { FetchMonths = (fun () -> async { return Ok [] }); FetchCategoryOrder = (fun () -> async { return Ok [] }) }
            // In Berlin schon der 1. Dezember: der November ist vergangen.
            let lateUtc = DateTime(2026, 11, 30, 23, 30, 0, DateTimeKind.Utc)
            let api = Server.Api.budgetApiWith settings (fun _ _ -> f.Port) framePort (fun () -> lateUtc) (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
            Expect.equal (api.setTargetPause "2026-11-01" (request k) |> run) (Error PausePastMonthText) "November vergangen"
            Expect.equal (api.setTargetPause "2026-12-01" (request k) |> run) (Ok (Some PausedInBb)) "Dezember pausiert"
            use conn = global.Persistence.getConnection ()
            let stamped =
                Dapper.SqlMapper.ExecuteScalar<string>(
                    conn,
                    "SELECT occurred_at FROM events WHERE context = 'budgeting' AND event_type = 'TargetPaused' AND payload LIKE @id",
                    {| id = $"%%{k}%%" |})
            Expect.stringStarts stamped "2026-11-30T23:30" "mit der Uhr gestempelt"
        }
    ]


// budgeting-qm5x9, ADR 0084 Punkt 8: Pausieren mit Befehls-Id. Eine verspätete Wiederholung hebt kein späteres
// Aufheben auf.
let private commandTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())
    let pauseWithId (f: FakeYnab) (id: Guid) (k: Guid) (intent: PauseIntent) =
        setTargetPause f.Port now current current { CategoryId = k; Intent = intent; CommandId = Some id } |> run

    testSequenced
    <| testList "Pause: Befehls-Id" [
        testCase "GIVEN Pausieren mit X und danach Aufheben mit Y WHEN X verspätet wiederkommt THEN hängt nichts an, YNAB wird nicht gelesen und die Pause bleibt aufgehoben" <| fun () ->
            let f, k = fakeWith false
            let x = Guid.NewGuid()
            Expect.equal (pauseWithId f x k PauseTarget) (Ok (Ok (Some PausedInBb))) "pausiert"
            Expect.equal (pauseWithId f (Guid.NewGuid()) k LiftPause) (Ok (Ok None)) "aufgehoben"
            f.Reads <- 0
            Expect.equal (pauseWithId f x k PauseTarget) (Ok (Ok (Some PausedInBb))) "Antwort aus dem Ereignis"
            Expect.equal f.Reads 0 "YNAB nicht gelesen"
            Expect.equal (eventRows k) [ current, "Pausiert"; current, "Aufgehoben" ] "nichts angehängt"
            Expect.equal (categoryIn (monthOk f current) k).Pause None "die Pause bleibt aufgehoben"

        testCase "Die Id eines Pausierens beim Aufheben wird abgelehnt, ohne Ereignis" <| fun () ->
            let f, k = fakeWith false
            let x = Guid.NewGuid()
            pauseWithId f x k PauseTarget |> ignore
            f.Reads <- 0
            Expect.equal (pauseWithId f x k LiftPause) (Ok (Error ForeignCommandText)) "innen abgelehnt"
            Expect.equal f.Reads 0 "YNAB nicht gelesen"
            Expect.equal (eventRows k) [ current, "Pausiert" ] "nichts angehängt"
    ]

[<Tests>]
let tests =
    testList "Target pausieren (budgeting-t6p8z)" [ domainTests; flowTests; persistenceTests; boundaryTests; commandTests ]
