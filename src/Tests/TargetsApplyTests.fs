module Tests.TargetsApplyTests

// budgeting-h2zfn: "Targets anwenden". Reine Planung, YNAB-Zuweisung (Pfad, Body,
// Fehlerzuordnung), der Weg im Api, und die Abläufe Vorschau, Übernehmen und erneut Senden
// gegen In-Memory-SQLite mit einem Fake-YNAB, der nie das Netz erreicht.

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

let private category name (assigned: int64) (under: int64 option) (snoozed: bool) : BudgetCategory =
    { Id = Guid.NewGuid()
      Name = name
      Assigned = eur assigned
      Activity = eur 0L
      Available = eur assigned
      Underfunded = under |> Option.map eur
      Snoozed = snoozed }

let private monthOf (tbb: int64) (cats: BudgetCategory list) : BudgetMonth =
    { Month = "2026-11-01"
      ToBeBudgeted = eur tbb
      Groups = [ { Name = "Alltag"; Categories = cats } ] }

let private planOk month =
    match Domain.planTargetApplication month with
    | Ok p -> p
    | Error e -> failtest $"Plan erwartet: {e}"

// ============================================
// Planung (rein)
// ============================================

let private planTests =
    testList "planTargetApplication (h2zfn)" [
        test "Fehlbetrag größer 0 wird aufgenommen, 0 und unbekannt nicht" {
            let a = category "Essen" 0L (Some 50000L) false
            let b = category "Miete" 100000L (Some 0L) false
            let c = category "Sparen" 0L None false
            let p = planOk (monthOf 900000L [ a; b; c ])
            Expect.equal (p.Lines |> List.map (fun l -> l.CategoryName)) [ "Essen" ] "nur Essen"
        }
        test "Neu ist altes Zugewiesen plus Fehlbetrag, auch bei Alt größer 0" {
            let a = category "Essen" 120000L (Some 30500L) false
            let l = (planOk (monthOf 0L [ a ])).Lines.Head
            Expect.equal l.OldAssigned (eur 120000L) "alt"
            Expect.equal l.Underfunded (eur 30500L) "Fehlbetrag"
            Expect.equal l.NewAssigned (eur 150500L) "neu = alt + Fehlbetrag"
        }
        test "Pausiertes Target wird übersprungen, aber ausgewiesen" {
            let a = category "Essen" 0L (Some 10000L) false
            let s = category "Ausflug" 0L (Some 25500L) true
            let p = planOk (monthOf 100000L [ a; s ])
            Expect.equal (p.Lines |> List.map (fun l -> l.CategoryName)) [ "Essen" ] "nicht in den Zeilen"
            Expect.equal (p.Skipped |> List.map (fun l -> l.CategoryName)) [ "Ausflug" ] "separat ausgewiesen"
            Expect.equal p.Total (eur 10000L) "pausierte zählen nicht in die Summe"
        }
        test "Summe und Zu verteilen danach stimmen" {
            let p = planOk (monthOf 100000L [ category "A" 0L (Some 30000L) false; category "B" 5000L (Some 20000L) false ])
            Expect.equal p.Total (eur 50000L) "Summe"
            Expect.equal p.ToBeBudgeted (eur 100000L) "davor"
            Expect.equal p.ToBeBudgetedAfter (eur 50000L) "danach = davor minus Summe"
        }
        test "Zu verteilen danach darf negativ sein" {
            let p = planOk (monthOf 10000L [ category "A" 0L (Some 30000L) false ])
            Expect.equal p.ToBeBudgetedAfter (eur -20000L) "negativ, kein Fehler"
        }
        test "Ein Betrag zwischen den 10 Milliunits wird abgelehnt" {
            let a = category "Essen k" 0L (Some 12345L) false
            match Domain.planTargetApplication (monthOf 0L [ a ]) with
            | Error msg -> Expect.stringContains msg "Essen k" "nennt die Kategorie"
            | Ok _ -> failtest "12345 Milliunits muss abgelehnt werden (YNAB rundet auf 12350)"
        }
        test "Ein Monat ohne Fehlbetrag liefert eine leere Vorschau" {
            let p = planOk (monthOf 5000L [ category "A" 0L (Some 0L) false ])
            Expect.isEmpty p.Lines "keine Zeilen"
            Expect.equal p.ToBeBudgetedAfter (eur 5000L) "unverändert"
        }
        test "previewsMatch vergleicht Zeilen und Zu verteilen" {
            let a = category "A" 0L (Some 10000L) false
            let p = planOk (monthOf 50000L [ a ])
            Expect.isTrue (Domain.previewsMatch p p) "gleich"
            let moved = planOk (monthOf 50000L [ { a with Assigned = eur 2000L } ])
            Expect.isFalse (Domain.previewsMatch p moved) "anderes Zugewiesen"
            let other = planOk (monthOf 49000L [ a ])
            Expect.isFalse (Domain.previewsMatch p other) "anderes Zu verteilen"
        }
        test "decideRetry: Ziel, Baseline, sonst gewinnt YNAB" {
            let baseline, target = eur 0L, eur 10000L
            Expect.equal (Domain.decideRetry baseline target (eur 10000L)) Domain.AlreadyAtTarget "schon am Ziel"
            Expect.equal (Domain.decideRetry baseline target (eur 0L)) Domain.SendTarget "noch an der Baseline"
            Expect.equal (Domain.decideRetry baseline target (eur 7000L)) Domain.YnabWins "dazwischen"
        }
    ]

// ============================================
// YNAB-Zuweisung: Pfad, Body, Fehlerzuordnung
// ============================================

let private ynabTests =
    let id1 = Guid.Parse "11111111-1111-1111-1111-111111111111"
    testList "YNAB-Zuweisung (h2zfn)" [
        test "Der PATCH-Body trägt budgeted als JSON-Zahl" {
            // Verhindert, dass der Betrag als String ("12350") gesendet wird: YNAB würde den
            // Body ablehnen oder die Zuweisung stillschweigend nicht setzen.
            let body = Server.YnabClient.buildAssignBody 12350L
            Expect.equal body """{"category":{"budgeted":12350}}""" "exakter Body"
            let parsed = Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.at [ "category"; "budgeted" ] Thoth.Json.Net.Decode.int64) body
            Expect.equal parsed (Ok 12350L) "als Zahl lesbar"
        }
        test "Der Pfad nutzt den Ersten des Monats und die Kategorie-Id" {
            let url = Server.YnabClient.assignUrl "bud" "2026-11-01" id1
            Expect.stringEnds url "/budgets/bud/months/2026-11-01/categories/11111111-1111-1111-1111-111111111111" "Pfad"
        }
        test "Statuscodes werden zugeordnet" {
            let map status = Server.YnabClient.interpretAssignResponse "2026-11-01" status "x"
            Expect.equal (map 200) (Ok ()) "200"
            Expect.equal (map 429) (Error (YnabError.RateLimitExceeded 60)) "429"
            match map 401 with Error (YnabError.Unauthorized _) -> () | other -> failtest $"401: {other}"
            match map 404 with Error (YnabError.InvalidResponse m) -> Expect.stringContains m "2026-11-01" "404 nennt den Monat" | other -> failtest $"404: {other}"
            match map 400 with Error (YnabError.InvalidResponse _) -> () | other -> failtest $"400: {other}"
            match map 503 with Error (YnabError.NetworkError m) -> Expect.stringContains m "503" "503" | other -> failtest $"503: {other}"
        }
        test "setAssignedWith reicht Pfad und Body durch und macht aus einer Ausnahme einen Netzfehler" {
            let mutable seen = "", ""
            let send url body = async { seen <- url, body; return 200, "{}" }
            let r = Server.YnabClient.setAssignedWith send "bud" "2026-11-01" id1 5000L |> Async.RunSynchronously
            Expect.equal r (Ok ()) "ok"
            Expect.equal (snd seen) """{"category":{"budgeted":5000}}""" "Body"
            Expect.stringContains (fst seen) "/months/2026-11-01/categories/" "Pfad"
            let boom _ _ = async { return failwith "kaputt" }
            match Server.YnabClient.setAssignedWith boom "bud" "2026-11-01" id1 5000L |> Async.RunSynchronously with
            | Error (YnabError.NetworkError m) -> Expect.stringContains m "kaputt" "Meldung"
            | other -> failtest $"Netzfehler erwartet: {other}"
        }
    ]

// ============================================
// Weg im Api
// ============================================

let private fakePortWith (result: YnabResult<unit>) : YnabPort =
    { FetchMonth = fun _ -> async { return Error (YnabError.RateLimitExceeded 60) }
      SetAssigned = fun _ _ _ -> async { return result } }

let private apiTests =
    let run port = async { return! Server.Budgeting.CommandHandlers.preview port "2026-11-01" }
    let call token budget =
        Server.Api.targetsWith token budget (fun _ _ -> fakePortWith (Ok ())) run |> Async.RunSynchronously
    testList "Weg von Targets anwenden im Api (h2zfn)" [
        test "Fehlender Token wird gemeldet, ohne YNAB zu fragen" {
            match call None (Some "b") with
            | Error msg -> Expect.stringContains msg "token" "Token fehlt"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Fehlendes Standard-Budget wird gemeldet" {
            match call (Some "t") None with
            | Error msg -> Expect.stringContains msg "Standard-Budget" "Budget fehlt"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "429 beim Lesen kommt als lesbare Meldung an" {
            match call (Some "t") (Some "b") with
            | Error msg -> Expect.stringContains msg "rate limit" "lesbar"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Gründe fehlgeschlagener Zeilen sind lesbar, 429 heißt Rate-Limit" {
            Expect.equal (failureReason (YnabError.RateLimitExceeded 60)) "Rate-Limit" "429"
            Expect.stringContains (failureReason (YnabError.Unauthorized "x")) "Token" "401"
            Expect.stringContains (failureReason (YnabError.NetworkError "weg")) "weg" "Netz"
            Expect.stringContains (failureReason (YnabError.InvalidResponse "404")) "404" "404"
        }
    ]

// ============================================
// Abläufe gegen Fake-YNAB und In-Memory-SQLite
// ============================================

/// Ein Fake-YNAB, das Zugewiesen und Zu verteilen wie YNAB führt: Fehlbetrag = Ziel minus
/// Zugewiesen (nicht negativ), Zu verteilen sinkt mit jeder erfolgreichen Zuweisung.
/// Kategorien als (Id, Name, Zugewiesen, Ziel, pausiert) in Milliunits.
type private FakeYnab(initial: (Guid * string * int64 * int64 * bool) list, tbb0: int64) as this =
    let assigned = Dictionary<Guid, int64>()
    do for (id, _, a, _, _) in initial do assigned[id] <- a
    let initialSum = initial |> List.sumBy (fun (_, _, a, _, _) -> a)
    let concurrent = ref 0
    let maxConcurrent = ref 0
    member val Attempts = ResizeArray<Guid * int64>()
    member val Failures = Dictionary<Guid, YnabError>()
    member val OnWrite : Guid -> unit = ignore with get, set
    member val ReadsFailAfterWrite = false with get, set
    member val WriteDelayMs = 0 with get, set
    /// Zu verteilen weicht nach jedem Schreiben um diesen Betrag ab (Milliunits).
    member val Drift = 0L with get, set
    member _.Assigned = assigned
    member _.MaxConcurrent = maxConcurrent.Value
    member _.Port : YnabPort =
        { FetchMonth =
            fun monthKey ->
                async {
                    if this.ReadsFailAfterWrite && this.Attempts.Count > 0 then
                        return Error (YnabError.NetworkError "Lesen scheitert")
                    else
                        let sum = assigned.Values |> Seq.sum
                        let written = this.Attempts.Count |> int64
                        return
                            Ok
                                { Month = monthKey
                                  ToBeBudgeted = eur (tbb0 - (sum - initialSum) + (if written > 0L then this.Drift else 0L))
                                  Categories =
                                    initial
                                    |> List.map (fun (id, name, _, target, snoozed) ->
                                        { Id = id
                                          Name = name
                                          GroupName = "Alltag"
                                          Assigned = eur assigned[id]
                                          Activity = eur 0L
                                          Available = eur assigned[id]
                                          Underfunded = Some (eur (max 0L (target - assigned[id])))
                                          Snoozed = snoozed
                                          Hidden = false
                                          Deleted = false
                                          Internal = false }) }
                }
          SetAssigned =
            fun _ id milliunits ->
                async {
                    concurrent.Value <- concurrent.Value + 1
                    maxConcurrent.Value <- max maxConcurrent.Value concurrent.Value
                    this.Attempts.Add((id, milliunits))
                    this.OnWrite id
                    if this.WriteDelayMs > 0 then do! Async.Sleep this.WriteDelayMs
                    concurrent.Value <- concurrent.Value - 1
                    match this.Failures.TryGetValue id with
                    | true, error -> return Error error
                    | _ ->
                        assigned[id] <- milliunits
                        return Ok ()
                } }

/// Frische Ids je Test, damit gemeinsame Tabellen sich nicht in die Quere kommen.
/// Reihenfolge der Zeilen: A (0 auf 50), B (20 auf 70), E (0 auf 10); C ist pausiert, D erfüllt.
let private fake (tbb0: int64) =
    let a, b, c, d, e = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    let f =
        FakeYnab(
            [ a, "Essen", 0L, 50000L, false
              b, "Kleidung", 20000L, 70000L, false
              c, "Ausflug", 0L, 30000L, true
              d, "Miete", 5000L, 5000L, false
              e, "Geschenke", 0L, 10000L, false ],
            tbb0
        )
    f, (a, b, c, d, e)

let private monthKey = "2026-11-01"
let private now = DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc)

let private run x = Async.RunSynchronously x

let private previewOf (f: FakeYnab) =
    match run (preview f.Port monthKey) with
    | Ok p -> p
    | Error e -> failtest $"Vorschau erwartet: {e}"

let private applyOk (f: FakeYnab) (shown: TargetPreview) : ApplyReport =
    match run (apply f.Port now monthKey shown) with
    | Ok (Applied r) -> r
    | other -> failtest $"Applied erwartet: {other}"

let private movementRows (ids: Guid list) : int =
    use conn = global.Persistence.getConnection ()
    let sql = "SELECT COUNT(*) FROM budget_movements WHERE category_id IN (" + (ids |> List.map (fun i -> $"'{i}'") |> String.concat ",") + ")"
    Dapper.SqlMapper.ExecuteScalar<int>(conn, sql)

let private outcomeOf (report: ApplyReport) (id: Guid) =
    (report.Lines |> List.find (fun l -> l.CategoryId = id)).Outcome

let private attemptsFor (f: FakeYnab) (id: Guid) =
    f.Attempts |> Seq.filter (fun (i, _) -> i = id) |> Seq.length

let private flowTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    testSequenced
    <| testList "Targets anwenden: Abläufe (h2zfn)" [

        testCase "Die Vorschau schreibt weder nach YNAB noch in die Datenbank" <| fun () ->
            let f, (a, b, c, d, e) = fake 200000L
            let p = previewOf f
            Expect.equal (p.Lines |> List.map (fun l -> l.CategoryId)) [ a; b; e ] "A, B, E; D erfüllt, C pausiert"
            Expect.equal (p.Skipped |> List.map (fun l -> l.CategoryId)) [ c ] "C übersprungen"
            Expect.isEmpty f.Attempts "kein schreibender YNAB-Aufruf"
            Expect.equal (movementRows [ a; b; c; d; e ]) 0 "keine Bewegung in der Datenbank"

        testCase "Übernehmen mit abweichendem Live-Stand schreibt nichts und liefert die neue Vorschau" <| fun () ->
            let f, (a, b, c, d, e) = fake 200000L
            let shown = previewOf f
            f.Assigned[a] <- 10000L // Roman hat in YNAB inzwischen selbst zugewiesen
            match run (apply f.Port now monthKey shown) with
            | Ok (PreviewChanged fresh) ->
                Expect.equal (fresh.Lines.Head.OldAssigned) (eur 10000L) "die frische Vorschau kennt den neuen Stand"
            | other -> failtest $"PreviewChanged erwartet: {other}"
            Expect.isEmpty f.Attempts "nichts geschrieben"
            Expect.equal (movementRows [ a; b; c; d; e ]) 0 "keine Bewegung"

        testCase "Übernehmen hält zuerst alle Bewegungen fest, dann folgen die PATCHes" <| fun () ->
            let f, (a, b, c, d, e) = fake 200000L
            let seenAtWrite = ResizeArray<int>()
            f.OnWrite <- fun _ -> seenAtWrite.Add(movementRows [ a; b; c; d; e ])
            let report = applyOk f (previewOf f)
            Expect.equal (List.ofSeq seenAtWrite) [ 3; 3; 3 ] "vor jedem PATCH sind alle drei Bewegungen da"
            Expect.equal (report.Lines |> List.map (fun l -> l.Outcome)) [ Written; Written; Written ] "alle geschrieben"
            Expect.equal (List.ofSeq f.Attempts) [ a, 50000L; b, 70000L; e, 10000L ] "absolute Zielwerte in Milliunits"

        testCase "Ein Fehlschlag trifft nur seine Zeile, die übrigen werden trotzdem gesendet" <| fun () ->
            let f, (a, b, c, d, e) = fake 200000L
            f.Failures[b] <- YnabError.NetworkError "boom"
            let report = applyOk f (previewOf f)
            Expect.equal (outcomeOf report a) Written "A geschrieben"
            match outcomeOf report b with
            | Failed reason -> Expect.stringContains reason "boom" "Grund steht an der Zeile"
            | other -> failtest $"Failed erwartet: {other}"
            Expect.equal (outcomeOf report e) Written "E nach dem Fehlschlag trotzdem gesendet"
            Expect.equal (movementRows [ a; b; c; d; e ]) 3 "Bewegungen für alle Zeilen"
            Expect.equal (writtenCount report) 2 "2 von 3"
            Expect.equal (failedCount report) 1 "eine fehlgeschlagen"

        testCase "Bei 429 bricht der Lauf ab, der Rest ist fehlgeschlagen mit Rate-Limit" <| fun () ->
            let f, (a, b, _, _, e) = fake 200000L
            f.Failures[b] <- YnabError.RateLimitExceeded 60
            let report = applyOk f (previewOf f)
            Expect.equal (outcomeOf report a) Written "bereits Geschriebenes bleibt"
            Expect.equal f.Assigned[a] 50000L "A in YNAB unberührt"
            Expect.equal (outcomeOf report b) (Failed "Rate-Limit") "B"
            Expect.equal (outcomeOf report e) (Failed "Rate-Limit") "E"
            Expect.equal (attemptsFor f e) 0 "E wurde gar nicht erst gesendet"

        testCase "Zu verteilen danach: stimmt mit der Vorschau überein, sonst ist der Unterschied sichtbar" <| fun () ->
            let f, _ = fake 200000L
            let shown = previewOf f
            let report = applyOk f shown
            Expect.equal report.ActualToBeBudgeted (Some shown.ToBeBudgetedAfter) "gleich"
            let g, _ = fake 200000L
            g.Drift <- 5000L
            let shownG = previewOf g
            let reportG = applyOk g shownG
            Expect.notEqual reportG.ActualToBeBudgeted (Some shownG.ToBeBudgetedAfter) "weicht ab"
            let h, _ = fake 200000L
            h.ReadsFailAfterWrite <- true
            Expect.isNone (applyOk h (previewOf h)).ActualToBeBudgeted "erneutes Lesen scheitert: unbekannt"

        testCase "Ein negatives Zu verteilen danach sperrt Übernehmen nicht" <| fun () ->
            let f, _ = fake 10000L
            let shown = previewOf f
            Expect.isTrue (shown.ToBeBudgetedAfter.Amount < 0m) "Vorschau ist negativ"
            let report = applyOk f shown
            Expect.equal (writtenCount report) 3 "trotzdem angewendet"
            Expect.isTrue ((Option.get report.ActualToBeBudgeted).Amount < 0m) "YNAB meldet negativ"

        testCase "Erneut senden sendet nur fehlgeschlagene Zeilen und ist wiederholbar" <| fun () ->
            let f, (a, b, _, _, e) = fake 200000L
            f.Failures[b] <- YnabError.NetworkError "boom"
            let report = applyOk f (previewOf f)
            f.Failures.Remove b |> ignore
            let appId = Guid.Parse report.ApplicationId
            let retried = match run (retryFailed f.Port now appId) with Ok r -> r | Error x -> failtest $"{x}"
            Expect.equal (retried.Lines |> List.map (fun l -> l.Outcome)) [ Written; Written; Written ] "alle geschrieben"
            Expect.equal (attemptsFor f a) 1 "A nicht erneut gesendet"
            Expect.equal (attemptsFor f e) 1 "E nicht erneut gesendet"
            Expect.equal (attemptsFor f b) 2 "B einmal erneut"
            let before = f.Attempts.Count
            run (retryFailed f.Port now appId) |> ignore
            Expect.equal f.Attempts.Count before "erneutes Senden eines geschriebenen Ziels ändert nichts"

        testCase "Erneut senden: YNAB weder auf Baseline noch auf Ziel ist in YNAB geändert, nichts überschrieben" <| fun () ->
            let f, (_, b, _, _, _) = fake 200000L
            f.Failures[b] <- YnabError.NetworkError "boom"
            let report = applyOk f (previewOf f)
            f.Failures.Remove b |> ignore
            f.Assigned[b] <- 33330L
            let before = f.Attempts.Count
            let retried = match run (retryFailed f.Port now (Guid.Parse report.ApplicationId)) with Ok r -> r | Error x -> failtest $"{x}"
            Expect.equal (outcomeOf retried b) ChangedInYnab "geschlossen als in YNAB geändert"
            Expect.equal f.Attempts.Count before "nichts gesendet"
            Expect.equal f.Assigned[b] 33330L "YNABs Wert bleibt"

        testCase "Erneut senden: steht YNAB schon auf dem Ziel, wird nichts gesendet und die Zeile ist geschrieben" <| fun () ->
            let f, (_, b, _, _, _) = fake 200000L
            f.Failures[b] <- YnabError.NetworkError "Antwort verloren"
            let report = applyOk f (previewOf f)
            f.Failures.Remove b |> ignore
            f.Assigned[b] <- 70000L // der PATCH war doch angekommen
            let before = f.Attempts.Count
            let retried = match run (retryFailed f.Port now (Guid.Parse report.ApplicationId)) with Ok r -> r | Error x -> failtest $"{x}"
            Expect.equal (outcomeOf retried b) Written "geschrieben"
            Expect.equal f.Attempts.Count before "kein Senden"

        testCase "Erneut senden bricht bei 429 ab" <| fun () ->
            let f, (a, b, _, _, _) = fake 200000L
            f.Failures[a] <- YnabError.NetworkError "x"
            f.Failures[b] <- YnabError.NetworkError "y"
            let report = applyOk f (previewOf f)
            f.Failures[a] <- YnabError.RateLimitExceeded 60
            let before = attemptsFor f b
            let retried = match run (retryFailed f.Port now (Guid.Parse report.ApplicationId)) with Ok r -> r | Error x -> failtest $"{x}"
            Expect.equal (outcomeOf retried a) (Failed "Rate-Limit") "A"
            Expect.equal (outcomeOf retried b) (Failed "Rate-Limit") "B"
            Expect.equal (attemptsFor f b) before "B nicht erneut gesendet"

        testCase "Zwei gleichzeitige Anwendungen für denselben Monat laufen nicht ineinander" <| fun () ->
            let f, _ = fake 200000L
            f.WriteDelayMs <- 30
            let shown = previewOf f
            let results =
                [ apply f.Port now monthKey shown; apply f.Port now monthKey shown ]
                |> Async.Parallel
                |> run
            let applied = results |> Array.filter (function Ok (Applied _) -> true | _ -> false)
            let changed = results |> Array.filter (function Ok (PreviewChanged _) -> true | _ -> false)
            Expect.equal applied.Length 1 "eine Anwendung läuft"
            Expect.equal changed.Length 1 "die andere sieht ihre Vorschau als überholt"
            Expect.equal f.MaxConcurrent 1 "nie zwei PATCHes zugleich"
            Expect.equal f.Attempts.Count 3 "jede Kategorie genau einmal"
    ]

let private persistenceTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    let movement () : Budgeting.Persistence.Movement =
        { Id = Guid.NewGuid()
          ApplicationId = Guid.NewGuid()
          OccurredAt = now
          Origin = Budgeting.Persistence.OriginTargets
          Month = monthKey
          CategoryId = Guid.NewGuid()
          CategoryName = "Essen"
          OldAssigned = 0L
          NewAssigned = 50000L }

    let tryExec (sql: string) =
        use conn = global.Persistence.getConnection ()
        try
            Dapper.SqlMapper.Execute(conn, sql) |> ignore
            None
        with ex -> Some ex.Message

    testSequenced
    <| testList "Budget-Bewegungen: Persistenz (h2zfn)" [
        testCase "Eine Bewegung trägt Zeitpunkt, Herkunft, Monat, Kategorie, altes und neues Zugewiesen" <| fun () ->
            let m = movement ()
            Budgeting.Persistence.appendMovements [ m ] |> run
            let loaded = Budgeting.Persistence.loadApplication m.ApplicationId |> run
            Expect.equal (loaded |> List.map (fun s -> s.Movement)) [ m ] "unverändert gelesen"
            Expect.equal loaded.Head.Latest None "noch kein Ergebnis"

        testCase "Bewegungen sind nach dem Schreiben nicht veränderbar" <| fun () ->
            let m = movement ()
            Budgeting.Persistence.appendMovements [ m ] |> run
            Expect.isSome (tryExec $"UPDATE budget_movements SET new_assigned = 1 WHERE id = '{m.Id}'") "UPDATE abgewiesen"
            Expect.isSome (tryExec $"DELETE FROM budget_movements WHERE id = '{m.Id}'") "DELETE abgewiesen"
            Expect.equal (Budgeting.Persistence.loadApplication m.ApplicationId |> run |> List.map (fun s -> s.Movement)) [ m ] "unverändert"

        testCase "Ergebnisse werden nur angehängt, das jüngste ist der Stand" <| fun () ->
            let m = movement ()
            Budgeting.Persistence.appendMovements [ m ] |> run
            Budgeting.Persistence.appendResult m.Id now (Failed "boom") |> run
            Budgeting.Persistence.appendResult m.Id now Written |> run
            let state = Budgeting.Persistence.loadApplication m.ApplicationId |> run |> List.head
            Expect.equal state.Latest (Some Written) "jüngstes Ergebnis"
            use conn = global.Persistence.getConnection ()
            let rows = Dapper.SqlMapper.ExecuteScalar<int>(conn, $"SELECT COUNT(*) FROM budget_movement_results WHERE movement_id = '{m.Id}'")
            Expect.equal rows 2 "beide Zeilen bleiben stehen"
            Expect.isSome (tryExec "UPDATE budget_movement_results SET status = 'written'") "UPDATE abgewiesen"
            Expect.isSome (tryExec "DELETE FROM budget_movement_results") "DELETE abgewiesen"

        testCase "Der Grund eines Fehlschlags bleibt erhalten, ChangedInYnab hat keinen" <| fun () ->
            let m1, m2 = movement (), { movement () with ApplicationId = Guid.NewGuid() }
            Budgeting.Persistence.appendMovements [ m1; m2 ] |> run
            Budgeting.Persistence.appendResult m1.Id now (Failed "Rate-Limit") |> run
            Budgeting.Persistence.appendResult m2.Id now ChangedInYnab |> run
            let l1 = Budgeting.Persistence.loadApplication m1.ApplicationId |> run |> List.head
            let l2 = Budgeting.Persistence.loadApplication m2.ApplicationId |> run |> List.head
            Expect.equal l1.Latest (Some (Failed "Rate-Limit")) "Grund"
            Expect.equal l2.Latest (Some ChangedInYnab) "in YNAB geändert"
    ]

[<Tests>]
let tests =
    testList "Targets anwenden (budgeting-h2zfn)" [ planTests; ynabTests; apiTests; flowTests; persistenceTests ]
