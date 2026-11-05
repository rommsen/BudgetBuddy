module Tests.BudgetHistoryTests

// budgeting-dyn8g: Tab Historie je Kategorie. Reine Regeln (Zeile aus Sicht der Kategorie, Aufbau
// von Verfügbar, Balken, Monatsfenster) und die Abfrage gegen In-Memory-SQLite über das Record-Feld
// der API: Bewegungen aus `events`, Ausgaben aus dem Kassenbuch.

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

let private movement origin counterpart oldAssigned newAssigned latest : HistoryMovement =
    { MovementId = Guid.NewGuid()
      ApplicationId = Guid.NewGuid()
      OccurredAt = DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc)
      Origin = origin
      Month = "2026-10-01"
      CategoryId = Guid.NewGuid()
      CategoryName = "Lebensmittel"
      OldAssigned = eur oldAssigned
      NewAssigned = eur newAssigned
      Counterpart = counterpart
      Latest = latest }

[<Tests>]
let lineTests =
    testList "Historie: Bewegung als Zeile aus Sicht der Kategorie" [
        testCase "Verschieben weg: Betrag negativ, nach der Gegenseite" <| fun () ->
            let line = historyLine (movement MoneyMoved (Some "Urlaub") 100m 80m (Some Written))
            Expect.equal line.Change (Some (eur -20m)) "neu minus alt"
            Expect.equal line.Text "nach Urlaub" "Gegenseite"

        testCase "Verschieben her: Betrag positiv, von der Gegenseite" <| fun () ->
            let line = historyLine (movement MoneyMoved (Some "Lebensmittel") 10m 30m (Some Written))
            Expect.equal line.Change (Some (eur 20m)) "neu minus alt"
            Expect.equal line.Text "von Lebensmittel" "Gegenseite"

        testCase "Zuweisen aus Zu verteilen: zugewiesen" <| fun () ->
            let line = historyLine (movement MoneyAssigned None 0m 20m (Some Written))
            Expect.equal line.Change (Some (eur 20m)) "Betrag"
            Expect.equal line.Text "zugewiesen" "Richtung"

        testCase "Zuweisen zurück: zurück nach Zu verteilen" <| fun () ->
            let line = historyLine (movement MoneyAssigned None 50m 30m (Some Written))
            Expect.equal line.Change (Some (eur -20m)) "Betrag"
            Expect.equal line.Text "zurück nach Zu verteilen" "Richtung"

        testCase "Targets anwenden: Herkunft als Text" <| fun () ->
            let line = historyLine (movement TargetsApplied None 25m 75m (Some Written))
            Expect.equal line.Change (Some (eur 50m)) "Betrag"
            Expect.equal line.Text "Targets anwenden" "Herkunft"

        testCase "Der Stand folgt dem jüngsten Ergebnis, ohne Ergebnis noch offen" <| fun () ->
            let statusOf latest = (historyLine (movement TargetsApplied None 0m 10m latest)).Status
            Expect.equal (statusOf (Some Written)) (Some HistoryWritten) "geschrieben"
            Expect.equal (statusOf (Some (Failed "Rate-Limit"))) (Some (HistoryFailed "Rate-Limit")) "fehlgeschlagen mit Grund"
            Expect.equal (statusOf (Some ChangedInYnab)) (Some HistoryChangedInYnab) "in YNAB geändert"
            Expect.equal (statusOf None) (Some HistoryOpen) "noch offen"
    ]

let private pauseAt (time: DateTime) : HistoryEntry =
    TargetPaused
        { EventId = Guid.NewGuid()
          OccurredAt = time
          Month = "2026-10-01"
          CategoryId = Guid.NewGuid()
          CategoryName = "Urlaub" }

let private targetRemovedAt (time: DateTime) : HistoryTargetChange =
    { ChangeId = Guid.NewGuid()
      OccurredAt = time
      Month = "2026-10-01"
      CategoryId = Guid.NewGuid()
      CategoryName = "Urlaub"
      ChangeKind = TargetRemoved
      GoalBefore = Some (eur 50m)
      GoalAfter = None
      DateBefore = None
      DateAfter = None
      Latest = Some Written
      DateStep = None }

// budgeting-h7q4v (H4), hierher gezogen in budgeting-c6vn4: das Mischen gehört zur Historie je Kategorie.
[<Tests>]
let mergeTests =
    testList "Historie je Kategorie: Bewegungen, Pausen und Target-Änderungen gemischt" [
        testCase "Neueste zuerst, bei gleichem Zeitpunkt die Bewegung vor der Pause und die Pause vor der Target-Änderung" <| fun () ->
            let at (hour: int) = DateTime(2026, 10, 3, hour, 0, 0, DateTimeKind.Utc)
            let m = { movement MoneyAssigned None 0m 5m None with OccurredAt = at 9 }
            let p = pauseAt (at 9)
            let c = targetRemovedAt (at 9)
            let laterChange = targetRemovedAt (at 12)
            let earlierPause = pauseAt (at 7)
            let merged = Server.Budgeting.Domain.mergeHistory [ m ] [ p; earlierPause ] [ laterChange; c ]
            Expect.equal merged [ TargetChanged laterChange; Movement m; p; TargetChanged c; earlierPause ] "Reihenfolge"
    ]

[<Tests>]
let berlinTimeTests =
    let text y mo d h mi = historyTimeText (DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc))
    testList "Historie: Zeitpunkt in Berliner Zeit" [
        testCase "Winterzeit ist UTC+1, Sommerzeit UTC+2" <| fun () ->
            Expect.equal (text 2026 1 15 10 5) "15.01. 11:05" "Winter"
            Expect.equal (text 2026 7 1 10 5) "01.07. 12:05" "Sommer"

        testCase "Die Sommerzeit beginnt am letzten Sonntag im März um 01:00 UTC" <| fun () ->
            Expect.equal (text 2026 3 29 0 59) "29.03. 01:59" "davor"
            Expect.equal (text 2026 3 29 1 0) "29.03. 03:00" "danach"

        testCase "Die Sommerzeit endet am letzten Sonntag im Oktober um 01:00 UTC" <| fun () ->
            Expect.equal (text 2026 10 25 0 59) "25.10. 02:59" "davor"
            Expect.equal (text 2026 10 25 1 0) "25.10. 02:00" "danach"

        testCase "Über Mitternacht wechselt der Tag" <| fun () ->
            Expect.equal (text 2026 12 31 23 30) "01.01. 00:30" "Neujahr"

        testCase "Die Regel stimmt ein Jahr lang mit der Zeitzone Europe/Berlin überein" <| fun () ->
            let zone = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"
            for hour in 0 .. 3 .. 24 * 366 do
                let instant = DateTime(2027, 1, 1, 0, 30, 0, DateTimeKind.Utc).AddHours(float hour)
                Expect.equal (berlinTime instant) (TimeZoneInfo.ConvertTimeFromUtc(instant, zone)) $"{instant:o}"

        // budgeting-x4k7p Q4: Ein als lokal markierter Zeitpunkt wird vorher nach UTC gerechnet.
        // `test.runsettings` setzt TZ=Europe/Berlin, die lokale Uhr weicht also von UTC ab.
        testCase "Ein als lokal markierter Zeitpunkt ergibt dieselbe Berliner Uhrzeit wie sein UTC-Wert" <| fun () ->
            let instant = DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc)
            let local = instant.ToLocalTime()
            Expect.equal local.Kind DateTimeKind.Local "lokal markiert"
            Expect.notEqual local.Hour instant.Hour "die lokale Uhr weicht von UTC ab (TZ=Europe/Berlin)"
            Expect.equal (berlinTime local) (berlinTime instant) "dieselbe Uhrzeit"
            Expect.equal (historyTimeText local) "03.10. 11:30" "Text"
    ]

let private category assigned activity available : BudgetCategory =
    { Id = Guid.NewGuid()
      Name = "Drogerie"
      Assigned = eur assigned
      Activity = eur activity
      Available = eur available
      Underfunded = None
      Target = None
      Pause = None }

[<Tests>]
let buildupTests =
    testList "Historie: Aufbau von Verfügbar" [
        testCase "Vortrag ist Verfügbar minus Zugewiesen minus Aktivität" <| fun () ->
            let buildup = availableBuildup (category 92.3m -92.3m 15.1m)
            Expect.equal buildup.CarryOver (eur 15.1m) "Vortrag"
            Expect.equal (buildup.Assigned, buildup.Activity, buildup.Available) (eur 92.3m, eur -92.3m, eur 15.1m) "YNABs Werte"

        testCase "Auch bei negativem Verfügbar und negativem Zugewiesen" <| fun () ->
            let buildup = availableBuildup (category -20m -30m -12.3m)
            Expect.equal buildup.CarryOver (eur 37.7m) "-12,30 - (-20) - (-30)"
    ]

let private spending month activity : MonthSpending = { Month = month; Activity = eur activity }

[<Tests>]
let barTests =
    let months = [ "2026-05-01"; "2026-06-01"; "2026-07-01"; "2026-08-01"; "2026-09-01"; "2026-10-01" ]
    let sums = List.zip months [ -100m; 0m; -50m; 20m; -25m; -10m ] |> List.map (fun (m, a) -> spending m a)

    testList "Historie: Balken der Ausgaben" [
        testCase "Der laufende Monat trägt seine Monatsart, vergangene sind vergangen" <| fun () ->
            let bars = spendingBars "2026-10-01" sums
            Expect.equal
                (bars |> List.map (fun b -> b.Kind))
                [ PastMonth; PastMonth; PastMonth; PastMonth; PastMonth; CurrentMonth ]
                "nur Oktober läuft"

        testCase "Ist ein künftiger Monat gezeigt, läuft der laufende darin, die späteren kommen erst" <| fun () ->
            let bars = spendingBars "2026-08-01" sums
            Expect.equal
                (bars |> List.map (fun b -> b.Kind))
                [ PastMonth; PastMonth; PastMonth; CurrentMonth; FutureMonth; FutureMonth ]
                "nur August läuft"

        testCase "Ganz in der Vergangenheit läuft kein Balken" <| fun () ->
            Expect.isTrue (spendingBars "2027-01-01" sums |> List.forall (fun b -> b.Kind = PastMonth)) "alle vergangen"

        testCase "Ausgaben sind die Aktivität mit umgekehrtem Vorzeichen, die Höhe bezieht sich auf den größten" <| fun () ->
            let bars = spendingBars "2026-10-01" sums
            Expect.equal (bars |> List.map (fun b -> b.Spending.Amount)) [ 100m; 0m; 50m; -20m; 25m; 10m ] "Ausgaben"
            Expect.equal (bars |> List.map (fun b -> b.Percent)) [ 100; 0; 50; 0; 25; 10 ] "Höhe in Prozent"

        testCase "Ohne Ausgaben haben alle Balken die Höhe 0" <| fun () ->
            let bars = spendingBars "2026-10-01" (months |> List.map (fun m -> spending m 0m))
            Expect.isTrue (bars |> List.forall (fun b -> b.Percent = 0)) "keine Division durch 0"

        testCase "Das Fenster sind die 6 Monate bis einschließlich des gezeigten, über den Jahreswechsel" <| fun () ->
            Expect.equal
                (Server.Budgeting.Domain.spendingMonths (DateTime(2026, 3, 1)))
                [ DateTime(2025, 10, 1); DateTime(2025, 11, 1); DateTime(2025, 12, 1)
                  DateTime(2026, 1, 1); DateTime(2026, 2, 1); DateTime(2026, 3, 1) ]
                "aufsteigend"

        testCase "Vor dem Jahr 1 gibt es keinen Monat" <| fun () ->
            Expect.equal (Server.Budgeting.Domain.spendingMonths (DateTime(1, 2, 1))) [ DateTime(1, 1, 1); DateTime(1, 2, 1) ] "nur zwei"
    ]

// ---------------- Abfrage gegen In-Memory-SQLite ----------------

let private budget = Server.Api.budgetApi (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")

let private history (categoryId: Guid) (month: string) : CategoryHistory =
    match budget.getCategoryHistory { CategoryId = categoryId; Month = month } |> run with
    | Ok h -> h
    | Error message -> failtest message

let private stored
    (applicationId: Guid)
    (origin: MovementOrigin)
    (month: string)
    (categoryId: Guid)
    (name: string)
    (at: DateTime)
    (oldAssigned: int64)
    (newAssigned: int64)
    : Budgeting.Persistence.Movement =
    { Id = Guid.NewGuid()
      ApplicationId = applicationId
      OccurredAt = at
      Origin = origin
      Month = month
      CategoryId = categoryId
      CategoryName = name
      OldAssigned = oldAssigned
      NewAssigned = newAssigned }

let private catRef (id: Guid) (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId id; CategoryName = name }

let private observed (date: DateTime) (amount: decimal) : EntryObservedInYnabPayload = {
    AccountId = LedgerAccountId (Guid.NewGuid())
    YnabTransactionId = $"ynab-dyn8g-{Guid.NewGuid()}"
    ImportId = None
    Date = date
    Amount = eur amount
    Payee = Some "Empfänger Dyn8g"
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

let private seed (events: (EntryId -> LedgerEvent list)) =
    appendAndApply (events (newEntryId ())) |> run

let private observedEvent (payload: EntryObservedInYnabPayload) (entryId: EntryId) : LedgerEvent =
    { EntryId = entryId
      OccurredAt = DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc)
      Actor = YnabMirror
      Body = EntryObservedInYnab payload }

[<Tests>]
let queryTests =
    let testCase name body =
        testCase name (fun () ->
            Persistence.initializeDatabase ()
            Ledger.Persistence.initializeLedgerSchema ()
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    testSequenced
    <| testList "Historie: Abfrage (SQLite)" [
        testCase "Nur Bewegungen der Kategorie im Monat, neueste zuerst, mit jüngstem Ergebnis und Gegenseite" <| fun () ->
            let a, b = Guid.NewGuid(), Guid.NewGuid()
            let at hour = DateTime(2026, 10, 3, hour, 0, 0, DateTimeKind.Utc)
            let targets = stored (Guid.NewGuid()) TargetsApplied "2026-10-01" a "A" (at 8) 0L 50000L
            let otherCategory = stored (Guid.NewGuid()) TargetsApplied "2026-10-01" b "B" (at 8) 0L 10000L
            let moveId = Guid.NewGuid()
            let moveOut = stored moveId MoneyMoved "2026-10-01" a "A" (at 9) 50000L 30000L
            let moveIn = stored moveId MoneyMoved "2026-10-01" b "B" (at 9) 10000L 30000L
            let assigned = stored (Guid.NewGuid()) MoneyAssigned "2026-10-01" a "A" (at 10) 30000L 40000L
            let otherMonth = stored (Guid.NewGuid()) MoneyAssigned "2026-09-01" a "A" (at 11) 0L 5000L
            let targets, moveOut, assigned =
                match BudgetEventsFixture.recordGroups [ targets; otherCategory; moveOut; moveIn; assigned; otherMonth ] with
                | [ t; _; o; _; a; _ ] -> t, o, a
                | other -> failtest $"sechs Zeilen erwartet, kamen {List.length other}"
            BudgetEventsFixture.recordResult targets (at 8) (Failed "Rate-Limit")
            BudgetEventsFixture.recordResult targets (at 12) Written
            BudgetEventsFixture.recordResult moveOut (at 9) ChangedInYnab

            let found = (history a "2026-10-01").Entries |> List.choose (function Movement m -> Some m | _ -> None)
            Expect.equal (found |> List.map (fun m -> m.MovementId)) [ assigned.Id; moveOut.Id; targets.Id ] "nur A im Oktober, neueste zuerst"
            Expect.equal (found |> List.map (fun m -> m.Latest)) [ None; Some ChangedInYnab; Some Written ] "jüngstes Ergebnis"
            Expect.equal (found |> List.map (fun m -> m.Counterpart)) [ None; Some "B"; None ] "Gegenseite nur bei der Verschiebung"
            Expect.equal (found |> List.map (fun m -> m.Origin)) [ MoneyAssigned; MoneyMoved; TargetsApplied ] "Herkunft"
            Expect.equal ((List.item 1 found).NewAssigned) (eur 30m) "Beträge in Euro"

        testCase "Mit Filter nach Herkunft und ohne Kategorie und Monat: alle Bewegungen dieser Herkunft" <| fun () ->
            let at = DateTime(2026, 10, 4, 8, 0, 0, DateTimeKind.Utc)
            let first = stored (Guid.NewGuid()) MoneyAssigned "2026-10-01" (Guid.NewGuid()) "C" at 0L 1000L
            let second = stored (Guid.NewGuid()) MoneyAssigned "2026-08-01" (Guid.NewGuid()) "D" at 0L 2000L
            let other = stored (Guid.NewGuid()) TargetsApplied "2026-10-01" (Guid.NewGuid()) "E" at 0L 3000L
            let first, second, other =
                match BudgetEventsFixture.recordGroups [ first; second; other ] with
                | [ a; b; c ] -> a, b, c
                | found -> failtest $"drei Zeilen erwartet, kamen {List.length found}"
            let filter : Server.Budgeting.Domain.MovementFilter = { CategoryId = None; Month = None; Origin = Some MoneyAssigned }
            match Server.Budgeting.QueryHandlers.movements Server.Budgeting.QueryHandlers.liveHistoryPort filter |> run with
            | Error message -> failtest message
            | Ok found ->
                let ids = found |> List.map (fun m -> m.MovementId) |> Set.ofList
                Expect.isTrue (ids.Contains first.Id && ids.Contains second.Id) "beide Monate und Kategorien"
                Expect.isFalse (ids.Contains other.Id) "keine andere Herkunft"
                Expect.isTrue (found |> List.forall (fun m -> m.Origin = MoneyAssigned)) "nur Zuweisen"

        testCase "Ausgaben: 6 Monatssummen bis einschließlich des gezeigten, netto, ohne gelöschte, mit Split-Anteil" <| fun () ->
            let id = Guid.NewGuid()
            let cat = catRef id "Kategorie Dyn8g"
            let other = catRef (Guid.NewGuid()) "Andere Dyn8g"
            let booked date amount = seed (fun e -> [ observedEvent { observed date amount with Category = Some cat } e ])
            booked (utc 2025 9 30) -999m // der 7. Monat zählt nicht
            booked (utc 2025 10 1) -10m
            // November ohne Buchung
            booked (utc 2025 12 5) -50m
            booked (utc 2025 12 20) 20m // Erstattung
            booked (utc 2026 1 10) -5m
            seed (fun e ->
                [ observedEvent { observed (utc 2026 1 11) -99m with Category = Some cat } e
                  { observedEvent (observed (utc 2026 1 11) -99m) e with Body = EntryDeletionObservedInYnab } ])
            booked (utc 2026 2 1) -7m
            seed (fun e ->
                [ observedEvent
                      { observed (utc 2026 2 28) -30m with
                          Splits =
                              [ { Target = ToCategory (cat.CategoryId, cat.CategoryName); Amount = eur -10m; Memo = None }
                                { Target = ToCategory (other.CategoryId, other.CategoryName); Amount = eur -20m; Memo = None } ] }
                      e ])
            booked (utc 2026 3 31) -3m
            booked (utc 2026 4 1) -1000m // nach dem gezeigten Monat

            Expect.equal
                ((history id "2026-03-01").Spending |> List.map (fun s -> s.Month, s.Activity.Amount))
                [ "2025-10-01", -10m
                  "2025-11-01", 0m
                  "2025-12-01", -30m
                  "2026-01-01", -5m
                  "2026-02-01", -17m
                  "2026-03-01", -3m ]
                "netto je Monat, Monate ohne Buchung 0"

        testCase "Ungültiger Monat und fehlende Kategorie werden zugleich gemeldet" <| fun () ->
            match budget.getCategoryHistory { CategoryId = Guid.Empty; Month = "kein-monat" } |> run with
            | Ok _ -> failtest "Fehler erwartet"
            | Error message ->
                Expect.stringContains message "Kategorie fehlt" "Kategorie"
                Expect.stringContains message "kein-monat" "Monat"
    ]
