module Tests.MoveMoneyTests

// budgeting-afd8h, ADR 0069: "Verschieben". Reine Planung und die Abläufe gegen In-Memory-SQLite
// mit einem Fake-YNAB, der nie das Netz erreicht.

open System
open System.Collections.Generic

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.CommandHandlers

let private eur (milliunits: int64) : Money =
    { Amount = decimal milliunits / 1000m; Currency = "EUR" }

let private budgetCategory name (assigned: int64) : BudgetCategory =
    { Id = Guid.NewGuid()
      Name = name
      Assigned = eur assigned
      Activity = eur 0L
      Available = eur assigned
      Underfunded = None
      Snoozed = false }

let private monthWith (cats: BudgetCategory list) : BudgetMonth =
    { Month = "2026-11-01"
      ToBeBudgeted = eur 100000L
      Groups = [ { Name = "Alltag"; Categories = cats } ] }

let private request source target (amount: int64) : MoveRequest =
    { Source = source; Target = target; Amount = eur amount }

let private planOk month req =
    match Domain.planMove month req with
    | Ok plan -> plan
    | Error e -> failtest $"Plan erwartet: {e}"

let private planErrorOf month req =
    match Domain.planMove month req with
    | Ok plan -> failtest $"Ablehnung erwartet: {plan}"
    | Error e -> e

let private planTests =
    let a = budgetCategory "Lebensmittel" 100000L
    let b = budgetCategory "Urlaub" 20000L
    let month = monthWith [ a; b ]
    testList "planMove" [
        test "Zwischen zwei Kategorien entstehen zwei Zeilen, Quelle zuerst, neu = alt ∓ Betrag" {
            let plan = planOk month (request (CategorySide a.Id) (CategorySide b.Id) 30000L)
            Expect.equal plan.Origin "Verschieben" "Herkunft"
            Expect.equal (plan.Lines |> List.map (fun l -> l.CategoryId, l.OldAssigned, l.NewAssigned))
                [ a.Id, eur 100000L, eur 70000L; b.Id, eur 20000L, eur 50000L ] "Beträge und Reihenfolge"
        }
        test "Aus Zu verteilen entsteht eine Zeile mit Herkunft Zuweisen" {
            let plan = planOk month (request ToBeBudgetedSide (CategorySide b.Id) 30000L)
            Expect.equal plan.Origin "Zuweisen" "Herkunft"
            Expect.equal (plan.Lines |> List.map (fun l -> l.CategoryId, l.NewAssigned)) [ b.Id, eur 50000L ] "Ziel steigt"
        }
        test "Zurück nach Zu verteilen senkt die Kategorie" {
            let plan = planOk month (request (CategorySide a.Id) ToBeBudgetedSide 30000L)
            Expect.equal plan.Origin "Zuweisen" "Herkunft"
            Expect.equal (plan.Lines |> List.map (fun l -> l.CategoryId, l.NewAssigned)) [ a.Id, eur 70000L ] "Quelle sinkt"
        }
        test "Ein negatives neues Zugewiesen ist gültig: die Kategorie lebt vom Vortrag" {
            let carried = { budgetCategory "Vortrag" 0L with Available = eur 100000L }
            let plan = planOk (monthWith [ carried; b ]) (request (CategorySide carried.Id) (CategorySide b.Id) 50000L)
            Expect.equal plan.Lines.Head.NewAssigned (eur -50000L) "negativ, kein Fehler"
        }
        test "Betrag 0 und negativer Betrag werden abgelehnt" {
            for amount in [ 0L; -10000L ] do
                let e = planErrorOf month (request (CategorySide a.Id) (CategorySide b.Id) amount)
                Expect.stringContains e "größer als 0" "Grund"
        }
        test "Ein Betrag neben dem Cent-Raster wird abgelehnt" {
            let e = planErrorOf month (request (CategorySide a.Id) (CategorySide b.Id) 12345L)
            Expect.stringContains e "Cent" "Grund"
        }
        test "Quelle gleich Ziel wird abgelehnt, auch zweimal Zu verteilen" {
            Expect.stringContains (planErrorOf month (request (CategorySide a.Id) (CategorySide a.Id) 10000L)) "verschieden" "Kategorie"
            Expect.stringContains (planErrorOf month (request ToBeBudgetedSide ToBeBudgetedSide 10000L)) "verschieden" "Zu verteilen"
        }
        test "Eine im Monat fehlende Kategorie wird abgelehnt, auf jeder Seite" {
            let missing = Guid.NewGuid()
            Expect.stringContains (planErrorOf month (request (CategorySide missing) (CategorySide b.Id) 10000L)) (string missing) "Quelle"
            Expect.stringContains (planErrorOf month (request (CategorySide a.Id) (CategorySide missing) 10000L)) (string missing) "Ziel"
        }
        test "Ein altes Zugewiesen neben dem Raster wird abgelehnt, YNAB würde runden" {
            let odd = budgetCategory "Krumm" 12345L
            let e = planErrorOf (monthWith [ odd; b ]) (request (CategorySide odd.Id) (CategorySide b.Id) 10000L)
            Expect.stringContains e "10 Milliunits" "Grund"
        }
        test "Alle Gründe werden zusammen gemeldet" {
            let e = planErrorOf month (request (CategorySide (Guid.NewGuid())) (CategorySide (Guid.NewGuid())) 0L)
            Expect.stringContains e "größer als 0" "Betrag"
            Expect.stringContains e "fehlt" "Kategorie"
        }
    ]

/// Fake-YNAB. `Assigned` ist der Live-Stand, `Failures` lässt eine Kategorie beim PATCH scheitern.
type private FakeYnab(categories: (Guid * string * int64) list) =
    let assigned = Dictionary<Guid, int64>()
    do
        for id, _, value in categories do assigned[id] <- value
    member val Attempts = ResizeArray<Guid * int64>()
    member val Failures = Dictionary<Guid, YnabError>()
    /// Der PATCH landet in YNAB, aber die Antwort geht verloren: BB sieht einen Fehler.
    member val LostAnswers = Dictionary<Guid, YnabError>()
    member val Reads = 0 with get, set
    member val FailRead = false with get, set
    member val OnWrite : Guid -> unit = ignore with get, set
    member val WriteDelayMs = 0 with get, set
    member _.Assigned = assigned
    member this.Port : YnabPort =
        { FetchMonth =
            fun monthKey ->
                async {
                    this.Reads <- this.Reads + 1
                    if this.FailRead then
                        return Error (YnabError.NetworkError "Lesen scheitert")
                    else
                        return
                            Ok
                                { Month = monthKey
                                  ToBeBudgeted = eur 100000L
                                  Categories =
                                    categories
                                    |> List.map (fun (id, name, _) ->
                                        { Id = id
                                          Name = name
                                          GroupName = "Alltag"
                                          Assigned = eur assigned[id]
                                          Activity = eur 0L
                                          Available = eur assigned[id]
                                          Underfunded = None
                                          Snoozed = false
                                          Hidden = false
                                          Deleted = false
                                          Internal = false }) }
                }
          SetAssigned =
            fun _ id milliunits ->
                async {
                    this.Attempts.Add((id, milliunits))
                    this.OnWrite id
                    if this.WriteDelayMs > 0 then do! Async.Sleep this.WriteDelayMs
                    match this.Failures.TryGetValue id, this.LostAnswers.TryGetValue id with
                    | (true, error), _ -> return Error error
                    | _, (true, error) ->
                        assigned[id] <- milliunits
                        return Error error
                    | _ ->
                        assigned[id] <- milliunits
                        return Ok ()
                } }

let private monthKey = "2026-11-01"
let private now = DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc)
let private run x = Async.RunSynchronously x

let private fake () =
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    FakeYnab([ a, "Lebensmittel", 100000L; b, "Urlaub", 20000L ]), a, b

let private moveOk (f: FakeYnab) req : ApplyReport =
    match run (moveMoney f.Port now monthKey req) with
    | Ok (Ok report) -> report
    | other -> failtest $"Bericht erwartet: {other}"

let private retryOk (f: FakeYnab) (report: ApplyReport) : ApplyReport =
    match run (retryFailed f.Port now (Guid.Parse report.ApplicationId)) with
    | Ok r -> r
    | Error e -> failtest $"Bericht erwartet: {e}"

let private outcomes (report: ApplyReport) = report.Lines |> List.map (fun l -> l.Outcome)

let private isFailed outcome =
    match outcome with
    | Failed _ -> true
    | _ -> false

let private movementsOf (report: ApplyReport) =
    Budgeting.Persistence.loadApplication (Guid.Parse report.ApplicationId) |> run

let private movementRows (ids: Guid list) : int =
    use conn = global.Persistence.getConnection ()
    let sql = "SELECT COUNT(*) FROM budget_movements WHERE category_id IN (" + (ids |> List.map (fun i -> $"'{i}'") |> String.concat ",") + ")"
    Dapper.SqlMapper.ExecuteScalar<int>(conn, sql)

let private networkDown = YnabError.NetworkError "Netz weg"

let private flowTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    testSequenced
    <| testList "Verschieben: Abläufe" [
        testCase "Der Server rechnet relativ zum frischen Stand, nicht zum angezeigten" <| fun () ->
            let f, a, b = fake ()
            f.Assigned[a] <- 80000L // nach dem Anzeigen in YNAB geändert
            let _ = moveOk f (request (CategorySide a) (CategorySide b) 30000L)
            Expect.equal (List.ofSeq f.Attempts) [ a, 50000L; b, 50000L ] "erst Quelle, dann Ziel, relativ zu 80 €"

        testCase "Bewegungen sind vor dem ersten PATCH festgehalten, mit gemeinsamer Gruppen-Id" <| fun () ->
            let f, a, b = fake ()
            let seen = ResizeArray<int>()
            f.OnWrite <- fun _ -> seen.Add(movementRows [ a; b ])
            let report = moveOk f (request (CategorySide a) (CategorySide b) 30000L)
            Expect.equal (List.ofSeq seen) [ 2; 2 ] "vor jedem PATCH sind beide Bewegungen da"
            let states = movementsOf report
            Expect.equal (states |> List.map (fun s -> s.Movement.Origin)) [ "Verschieben"; "Verschieben" ] "Herkunft"
            Expect.equal (states |> List.map (fun s -> s.Movement.CategoryId)) [ a; b ] "Quelle vor Ziel"
            Expect.equal (states |> List.map (fun s -> string s.Movement.ApplicationId) |> List.distinct) [ report.ApplicationId ] "eine Gruppe"
            Expect.equal (outcomes report) [ Written; Written ] "geschrieben"

        testCase "Zuweisen hält eine Bewegung mit Herkunft Zuweisen fest" <| fun () ->
            let f, a, b = fake ()
            let report = moveOk f (request ToBeBudgetedSide (CategorySide b) 30000L)
            Expect.equal (movementsOf report |> List.map (fun s -> s.Movement.Origin)) [ "Zuweisen" ] "Herkunft"
            Expect.equal (List.ofSeq f.Attempts) [ b, 50000L ] "ein PATCH"
            Expect.equal f.Assigned[a] 100000L "die andere Kategorie bleibt"

        testCase "Scheitert das Lesen, ist nichts geschrieben und nichts festgehalten" <| fun () ->
            let f, a, b = fake ()
            f.FailRead <- true
            match run (moveMoney f.Port now monthKey (request (CategorySide a) (CategorySide b) 30000L)) with
            | Error (YnabError.NetworkError _) -> ()
            | other -> failtest $"Lesefehler erwartet: {other}"
            Expect.isEmpty f.Attempts "kein PATCH"
            Expect.equal (movementRows [ a; b ]) 0 "keine Bewegung"

        testCase "Ein abgelehnter Auftrag schreibt nichts und hält nichts fest" <| fun () ->
            let f, a, b = fake ()
            match run (moveMoney f.Port now monthKey (request (CategorySide a) (CategorySide (Guid.NewGuid())) 30000L)) with
            | Ok (Error message) -> Expect.stringContains message "fehlt" "Grund"
            | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.isEmpty f.Attempts "kein PATCH"
            Expect.equal (movementRows [ a; b ]) 0 "keine Bewegung"

        testCase "Fehlgeschlagener Ziel-PATCH: Quelle bleibt geschrieben, Ziel ist mit Grund erneut sendbar" <| fun () ->
            let f, a, b = fake ()
            f.Failures[b] <- networkDown
            let report = moveOk f (request (CategorySide a) (CategorySide b) 30000L)
            Expect.equal report.Lines.Head.Outcome Written "Quelle geschrieben"
            Expect.equal report.Lines[1].Outcome (Failed "Netzwerkfehler: Netz weg") "Ziel mit Grund"
            Expect.equal f.Assigned[a] 70000L "kein Rollback"
            f.Failures.Clear()
            let again = retryOk f report
            Expect.equal (outcomes again) [ Written; Written ] "erneut gesendet"
            Expect.equal f.Assigned[b] 50000L "Ziel steht"
            Expect.equal (f.Attempts |> Seq.filter (fun (i, _) -> i = a) |> Seq.length) 1 "Quelle nur einmal"

        testCase "GIVEN Quelle-PATCH scheitert WHEN verschoben wird THEN ist das Ziel nicht gesendet, beide fehlgeschlagen" <| fun () ->
            let f, a, b = fake ()
            f.Failures[a] <- networkDown
            let report = moveOk f (request (CategorySide a) (CategorySide b) 30000L)
            Expect.equal (List.ofSeq f.Attempts) [ a, 70000L ] "nur die Quelle wurde versucht"
            Expect.isTrue (outcomes report |> List.forall isFailed) "beide fehlgeschlagen"
            Expect.equal f.Assigned[b] 20000L "das Ziel blieb"

        testCase "GIVEN Quelle scheiterte WHEN erneut gesendet THEN geht erst die Quelle, dann das Ziel" <| fun () ->
            let f, a, b = fake ()
            f.Failures[a] <- networkDown
            let report = moveOk f (request (CategorySide a) (CategorySide b) 30000L)
            f.Failures.Clear()
            f.Attempts.Clear()
            let again = retryOk f report
            Expect.equal (List.ofSeq f.Attempts) [ a, 70000L; b, 50000L ] "Reihenfolge"
            Expect.equal (outcomes again) [ Written; Written ] "beide geschrieben"

        testCase "GIVEN Quelle scheitert wieder WHEN erneut gesendet THEN bleibt das Ziel ungesendet" <| fun () ->
            let f, a, b = fake ()
            f.Failures[a] <- networkDown
            let report = moveOk f (request (CategorySide a) (CategorySide b) 30000L)
            f.Attempts.Clear()
            let again = retryOk f report
            Expect.equal (List.ofSeq f.Attempts) [ a, 70000L ] "nur die Quelle"
            Expect.isTrue (outcomes again |> List.forall isFailed) "beide bleiben fehlgeschlagen"

        testCase "Die Quelle stand schon auf dem Ziel: nichts senden, das Ziel geht hinaus" <| fun () ->
            let f, a, b = fake ()
            f.Failures[a] <- networkDown
            let report = moveOk f (request (CategorySide a) (CategorySide b) 30000L)
            f.Failures.Clear()
            f.Attempts.Clear()
            f.Assigned[a] <- 70000L // der PATCH kam an, nur die Antwort ging verloren
            let again = retryOk f report
            Expect.equal (List.ofSeq f.Attempts) [ b, 50000L ] "nur das Ziel"
            Expect.equal (outcomes again) [ Written; Written ] "beide geschrieben"

        testCase "Hat YNAB bei der Quelle gewonnen, bleibt das Ziel ungesendet und die Verschiebung ist erledigt" <| fun () ->
            let f, a, b = fake ()
            f.Failures[a] <- networkDown
            let report = moveOk f (request (CategorySide a) (CategorySide b) 30000L)
            f.Failures.Clear()
            f.Attempts.Clear()
            f.Assigned[a] <- 90000L
            let again = retryOk f report
            Expect.isEmpty f.Attempts "kein PATCH: das Geld käme sonst aus Zu verteilen"
            Expect.equal (outcomes again) [ ChangedInYnab; ChangedInYnab ] "beide geschlossen"

        testCase "GIVEN die Quelle landete, die Antwort ging verloren und sie änderte sich danach WHEN erneut gesendet THEN bleibt das Ziel ungesendet und das Geld in Zu verteilen" <| fun () ->
            let f, a, b = fake ()
            f.LostAnswers[a] <- networkDown
            let report = moveOk f (request (CategorySide a) (CategorySide b) 30000L)
            Expect.equal f.Assigned[a] 70000L "der PATCH kam an"
            Expect.isTrue (outcomes report |> List.forall isFailed) "BB meldet beide als fehlgeschlagen"
            f.LostAnswers.Clear()
            f.Attempts.Clear()
            f.Assigned[a] <- 65000L
            let again = retryOk f report
            Expect.isEmpty f.Attempts "kein PATCH: die Quelle gehört YNAB, das Ziel bleibt liegen"
            Expect.equal (f.Assigned[a], f.Assigned[b]) (65000L, 20000L) "die 30 € sind weder am Ziel noch zurück an der Quelle"
            Expect.equal (outcomes again) [ ChangedInYnab; ChangedInYnab ] "beide geschlossen, auch das nie gesendete Ziel"
            let second = retryOk f report
            Expect.isEmpty f.Attempts "ein weiteres Senden holt das Ziel nicht nach"
            Expect.equal (outcomes second) [ ChangedInYnab; ChangedInYnab ] "der Stand bleibt"

        testCase "Verschieben und Targets anwenden für denselben Monat laufen nicht ineinander" <| fun () ->
            let f, a, b = fake ()
            let key = $"sperre-{Guid.NewGuid()}"
            let holder = new System.Threading.SemaphoreSlim(0, 1)
            let release = new System.Threading.SemaphoreSlim(0, 1)
            // Die Sperre halten wie ein laufendes "Targets anwenden".
            let held =
                MonthGuard.exclusive key (async {
                    holder.Release() |> ignore
                    do! release.WaitAsync() |> Async.AwaitTask
                })
                |> Async.StartAsTask
            holder.Wait()
            let moving = moveMoney f.Port now key (request (CategorySide a) (CategorySide b) 30000L) |> Async.StartAsTask
            Threading.Thread.Sleep 50
            Expect.equal f.Reads 0 "gelesen wird erst unter der Sperre, sonst wäre der Stand veraltet"
            Expect.isEmpty f.Attempts "Verschieben wartet auf die Sperre"
            release.Release() |> ignore
            held.Wait()
            moving.Wait()
            Expect.equal f.Reads 1 "danach liest es einmal"
            Expect.equal f.Attempts.Count 2 "danach läuft es"
    ]

[<Tests>]
let tests =
    testList "Verschieben (budgeting-afd8h)" [ planTests; flowTests ]
