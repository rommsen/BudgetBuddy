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
      Target = None
      Pause = (if snoozed then Some PausedInBb else None); OpenWriteBack = None }

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
        // budgeting-kjsht
        test "Das Target der Kategorie steht in der Zeile der Vorschau" {
            let target : Target = { Goal = eur 1600000L; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }
            let p = planOk (monthOf 50000L [ { category "A" 0L (Some 10000L) false with Target = Some target } ])
            Expect.equal p.Lines.Head.Target (Some target) "Target übernommen"
        }
        test "Ein geändertes Target allein löst kein Stand hat sich geändert aus" {
            let a = category "A" 0L (Some 10000L) false
            let before = planOk (monthOf 50000L [ { a with Target = Some { Goal = eur 10000L; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None } } ])
            let after = planOk (monthOf 50000L [ { a with Target = Some { Goal = eur 99000L; Rhythm = Weeks 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None } } ])
            Expect.notEqual before.Lines.Head.Target after.Lines.Head.Target "Target weicht ab"
            Expect.isTrue (Domain.previewsMatch before after) "Vorschau gilt weiter"
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
            match map 404 with
            | Error ((YnabError.MonthNotOpen _) as e) -> Expect.equal (ynabErrorText e) "Monat nicht offen" "ADR 0074 Punkt 3"
            | other -> failtest $"404: {other}"
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
      SetAssigned = fun _ _ _ -> async { return result }
      FetchCategoryGoals = fun () -> async { return Ok [] } }

let private apiTests =
    let run port = async { return! Server.Budgeting.CommandHandlers.preview port DateTime.UtcNow "2026-11-01" }
    let call token budget =
        Server.Api.withYnabPort token budget (fun _ _ -> fakePortWith (Ok ())) run |> Async.RunSynchronously
    testList "Weg von Targets anwenden im Api (h2zfn)" [
        test "Fehlender Token wird gemeldet, ohne YNAB zu fragen" {
            match call None (Some "b") with
            | Error msg -> Expect.stringContains msg "YNAB-Token" "Token fehlt"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Fehlendes Standard-Budget wird gemeldet" {
            match call (Some "t") None with
            | Error msg -> Expect.stringContains msg "Standard-Budget" "Budget fehlt"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "429 beim Lesen kommt als lesbare Meldung an" {
            match call (Some "t") (Some "b") with
            | Error msg -> Expect.equal msg "Rate-Limit (HTTP 429), erneut in 60 Sekunden" "deutsch"
            | Ok _ -> failtest "Fehler erwartet"
        }
        test "Gründe fehlgeschlagener Zeilen sind lesbar, 429 heißt Rate-Limit" {
            Expect.equal (ynabErrorText (YnabError.RateLimitExceeded 60)) "Rate-Limit (HTTP 429), erneut in 60 Sekunden" "429"
            Expect.stringContains (ynabErrorText (YnabError.Unauthorized "x")) "Token" "401"
            Expect.stringContains (ynabErrorText (YnabError.NetworkError "weg")) "weg" "Netz"
            Expect.stringContains (ynabErrorText (YnabError.InvalidResponse "404")) "404" "404"
        }
    ]

// ============================================
// Abläufe gegen Fake-YNAB und In-Memory-SQLite
// ============================================

/// Ein Fake-YNAB, das Zugewiesen und Zu verteilen wie YNAB führt: Fehlbetrag = Ziel minus
/// Zugewiesen (nicht negativ), Zu verteilen sinkt mit jeder erfolgreichen Zuweisung.
/// Kategorien als (Id, Name, Zugewiesen, Ziel, pausiert) in Milliunits. Jede hat ein monatliches Goal über das
/// Ziel, BB übernimmt es beim ersten Lesen (budgeting-eh8xe); pausiert heißt in BB pausiert (`fake`).
let private goalOf (target: int64) : Target =
    { Goal = eur target; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }

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
    member val Reads = 0 with get, set
    /// Läuft bei jedem Lesen des Monats, vor der Antwort.
    member val OnRead : unit -> unit = ignore with get, set
    member _.Assigned = assigned
    member _.MaxConcurrent = maxConcurrent.Value
    member _.Port : YnabPort =
        { FetchMonth =
            fun monthKey ->
                async {
                    this.Reads <- this.Reads + 1
                    this.OnRead ()
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
                                          Target = Some (goalOf target)
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
                }
          FetchCategoryGoals =
            fun () ->
                async {
                    return
                        Ok [ for id, name, _, target, _ in initial -> { Id = id; Name = name; Target = Some (goalOf target) } ]
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
    // C ist in BB pausiert (eine Pause in YNAB wirkt seit budgeting-eh8xe nicht mehr).
    Budgeting.Persistence.recordPause None (DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc)) "2026-11-01" c "Ausflug" Domain.PauseKind.Paused
    |> Async.RunSynchronously
    |> ignore
    f, (a, b, c, d, e)

let private monthKey = "2026-11-01"
let private now = DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc)

let private run x = Async.RunSynchronously x

let private previewOf (f: FakeYnab) =
    match run (preview f.Port now monthKey) with
    | Ok p -> p
    | Error e -> failtest $"Vorschau erwartet: {e}"

let private applyOk (f: FakeYnab) (shown: TargetPreview) : ApplyReport =
    match run (apply f.Port now monthKey shown) with
    | Ok (Applied r) -> r
    | other -> failtest $"Applied erwartet: {other}"

/// Die festgehaltenen Zeilen der Entscheidungen in BB über alle Monate, gelesen wie die Historie. Übernahme und
/// Beobachtung zählen nicht: sie hält das Beobachten vor jedem Befehl fest (budgeting-dgrp7).
let private movementRows (ids: Guid list) : int =
    ids
    |> List.sumBy (fun id ->
        match Budgeting.Persistence.loadMovements { CategoryId = Some id; Month = None; Origin = None } |> Async.RunSynchronously with
        | Ok found -> found |> List.filter (fun m -> not (isFromYnab m.Origin)) |> List.length
        | Error message -> failtest message)

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

        // budgeting-b5n8q: BB lehnt die Planung selbst ab, die Meldung schob die Ablehnung YNAB zu.
        testCase "Lehnt BB die Planung ab, meldet die Vorschau eine Ablehnung durch BB ohne YNAB im Text" <| fun () ->
            let f = FakeYnab([ Guid.NewGuid(), "Essen k", 0L, 12345L, false ], 100000L)
            match run (preview f.Port now monthKey) with
            | Error ((YnabError.RefusedByBb message) as e) ->
                Expect.stringContains message "Essen k" "nennt die Kategorie"
                Expect.isFalse ((ynabErrorText e).Contains "YNAB") $"Nutzertext ohne YNAB: {ynabErrorText e}"
            | other -> failtest $"RefusedByBb erwartet: {other}"

        // budgeting-b5n8q: eine unbekannte Anwendung ist BBs eigene Ablehnung, kein "YNAB hat abgelehnt".
        testCase "Erneut senden mit unbekannter Anwendung meldet, dass BB die Targets-Anwendung nicht findet" <| fun () ->
            let f, _ = fake 200000L
            match run (retryFailed f.Port now (Guid.NewGuid())) with
            | Error ((YnabError.RefusedByBb _) as e) ->
                Expect.equal (ynabErrorText e) "Targets-Anwendung nicht gefunden" "Nutzertext"
            | other -> failtest $"RefusedByBb erwartet: {other}"
            Expect.isEmpty f.Attempts "kein schreibender YNAB-Aufruf"

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
            Expect.equal (outcomeOf report b) (Failed "Rate-Limit (HTTP 429), erneut in 60 Sekunden") "B"
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
            Expect.equal (outcomeOf retried a) (Failed "Rate-Limit (HTTP 429), erneut in 60 Sekunden") "A"
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

let private loadedOk (applicationId: Guid) =
    match Budgeting.Persistence.loadApplication applicationId |> run with
    | Ok states -> states
    | Error e -> failtest $"Zeilen erwartet: {e}"

let private persistenceTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    let movement () : Budgeting.Persistence.Movement =
        { Id = Guid.NewGuid()
          ApplicationId = Guid.NewGuid()
          OccurredAt = now
          Origin = TargetsApplied
          Month = monthKey
          CategoryId = Guid.NewGuid()
          CategoryName = "Essen"
          OldAssigned = 0L
          NewAssigned = 50000L }

    let record (m: Budgeting.Persistence.Movement) =
        match BudgetEventsFixture.recordGroups [ m ] with
        | [ stored ] -> stored
        | other -> failtest $"eine Zeile erwartet, kamen {List.length other}"

    testSequenced
    <| testList "Budget-Bewegungen: Persistenz (h2zfn)" [
        testCase "Eine Bewegung trägt Zeitpunkt, Herkunft, Monat, Kategorie, altes und neues Zugewiesen" <| fun () ->
            let m = movement ()
            let stored = record m
            let loaded = loadedOk stored.ApplicationId
            Expect.equal
                (loaded |> List.map (fun s -> s.Movement))
                [ { m with Id = stored.Id; ApplicationId = stored.ApplicationId } ]
                "unverändert gelesen, Ids vom Schreiben"
            Expect.equal loaded.Head.Latest None "noch kein Ergebnis"

        // budgeting-hyn77: jedes Ergebnis ist ein eigenes Ereignis im Monats-Stream.
        testCase "Ergebnisse werden nur angehängt, das jüngste ist der Stand" <| fun () ->
            let m = record (movement ())
            BudgetEventsFixture.recordResult m now (Failed "boom")
            BudgetEventsFixture.recordResult m now Written
            let state = loadedOk m.ApplicationId |> List.head
            Expect.equal state.Latest (Some Written) "jüngstes Ergebnis"
            use conn = global.Persistence.getConnection ()
            let rows =
                Dapper.SqlMapper.ExecuteScalar<int>(
                    conn,
                    "SELECT COUNT(*) FROM events WHERE context = 'budgeting' AND stream_id = 'budgeting:month:2026-11' \
                     AND event_type IN ('AssignmentWriteBackFailed', 'AssignmentWrittenBackToYnab') AND payload LIKE @id",
                    {| id = $"%%{m.ApplicationId}%%" |})
            Expect.equal rows 2 "beide Ergebnisse bleiben stehen"

        testCase "Der Grund eines Fehlschlags bleibt erhalten, ersetzt und zurückgezogen lesen sich als in YNAB geändert" <| fun () ->
            let m1, m2, m3 = record (movement ()), record (movement ()), record (movement ())
            Budgeting.Persistence.recordWriteBack m1 now (Budgeting.Events.WriteBackFailed "Rate-Limit") |> run
            Budgeting.Persistence.recordWriteBack m2 now Budgeting.Events.SupersededInYnab |> run
            Budgeting.Persistence.recordWriteBack m3 now Budgeting.Events.Withdrawn |> run
            Expect.equal (loadedOk m1.ApplicationId |> List.head).Latest (Some (Failed "Rate-Limit")) "Grund"
            Expect.equal (loadedOk m2.ApplicationId |> List.head).Latest (Some ChangedInYnab) "in YNAB geändert"
            Expect.equal (loadedOk m3.ApplicationId |> List.head).Latest (Some ChangedInYnab) "zurückgezogen"
    ]

// budgeting-et7z8: im letzten Monat kein Targets anwenden. Der Port zählt jedes Lesen
// (`FetchMonth`), die Ablehnung muss also davor fallen; das Schreiben prüft `f.Attempts`.
let private lastMonthTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())
    let today = DateOnly(2026, 11, 1)
    let countingPort (f: FakeYnab) =
        let reads = ResizeArray<string>()
        let port =
            { f.Port with
                FetchMonth =
                    fun key ->
                        reads.Add key
                        f.Port.FetchMonth key }
        port, reads
    let previewVia port key =
        Server.Api.previewTargetsWith (Some "t") (Some "b") (fun _ _ -> port) today now key |> run
    let applyVia port key shown =
        Server.Api.applyTargetsWith (Some "t") (Some "b") (fun _ _ -> port) today now key shown |> run
    testList "Targets anwenden in vergangenen Monaten (ADR 0074)" [
        testCase "Die Vorschau lehnt einen vergangenen Monat ab, ohne YNAB zu fragen" <| fun () ->
            let f, _ = fake 500000L
            let port, reads = countingPort f
            match previewVia port "2026-10-01" with
            | Error msg -> Expect.stringContains msg "vergangenen Monaten" "Grund"
            | Ok _ -> failtest "Fehler erwartet"
            Expect.isEmpty reads "kein Lesen"

        testCase "Übernehmen lehnt einen vergangenen Monat ab, liest und schreibt nichts" <| fun () ->
            let f, _ = fake 500000L
            let shown = previewOf f
            let port, reads = countingPort f
            match applyVia port "2026-10-01" shown with
            | Error msg -> Expect.stringContains msg "vergangenen Monaten" "Grund"
            | Ok _ -> failtest "Fehler erwartet"
            Expect.isEmpty reads "kein Lesen"
            Expect.isEmpty f.Attempts "kein Schreiben"

        testCase "Die Vorschau für einen künftigen Monat liest genau diesen Monat" <| fun () ->
            let f, _ = fake 500000L
            let port, reads = countingPort f
            Expect.isOk (previewVia port "2026-12-01") "Vorschau"
            Expect.equal (List.ofSeq reads) [ "2026-12-01" ] "Monat"

        testCase "Übernehmen für diesen Monat schreibt in den laufenden Monat" <| fun () ->
            let f, _ = fake 500000L
            let shown =
                match previewVia f.Port "2026-11-01" with
                | Ok p -> p
                | Error e -> failtest e
            match applyVia f.Port "2026-11-01" shown with
            | Ok (Applied report) ->
                Expect.equal report.Month "2026-11-01" "Monat"
                let states = loadedOk (Guid.Parse report.ApplicationId)
                Expect.isNonEmpty states "Bewegungen festgehalten"
                for state in states do
                    Expect.equal state.Movement.OccurredAt now "Zeitstempel ist die Uhr, nicht der Kalendertag"
            | other -> failtest $"Applied erwartet: {other}"
    ]

// Die Verdrahtung der BudgetApi: Settings, Port und Uhr sind die einzigen Stellen, an denen
// `budgetApi` etwas anderes tut als `budgetApiWith`.
let private wiringTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())
    let berlin = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"
    // In Berlin schon der 1. Dezember, in UTC noch der 30. November.
    let lateUtc = DateTime(2026, 11, 30, 23, 30, 0, DateTimeKind.Utc)
    let apiFor (f: FakeYnab) =
        let seen = ResizeArray<string * string>()
        let reads = ResizeArray<string>()
        let settings key =
            async {
                return
                    match key with
                    | "ynab_token" -> Some "das-token"
                    | "ynab_default_budget_id" -> Some "das-budget"
                    | _ -> None
            }
        let portFor token (YnabBudgetId budget) =
            seen.Add((token, budget))
            { f.Port with
                FetchMonth =
                    fun key ->
                        reads.Add key
                        f.Port.FetchMonth key }
        let framePortFor token (YnabBudgetId budget) : Server.Budgeting.QueryHandlers.BudgetFramePort =
            seen.Add((token, budget))
            { FetchMonths = fun () -> async { return Ok [ "2026-12-01"; "2026-11-01" ] }
              FetchCategoryOrder = fun () -> async { return Ok [] } }
        Server.Api.budgetApiWith settings portFor framePortFor (fun () -> lateUtc) berlin, seen, reads
    testList "Verdrahtung der BudgetApi" [
        // Bug B1 am Endpunkt: siehe "Kurz nach Mitternacht" in BudgetingTests.
        testCase "Der Rahmen nennt den Berliner Monat, auch wenn UTC noch im alten liegt" <| fun () ->
            let f, _ = fake 500000L
            let api, _, _ = apiFor f
            match api.getBudgetFrame () |> run with
            | Ok frame ->
                Expect.equal frame.CurrentMonth "2026-12-01" "Dezember"
                Expect.equal frame.Months [ "2026-11-01"; "2026-12-01" ] "aufsteigend"
            | Error e -> failtest e

        testCase "Der November ist in Berlin schon vergangen, Targets anwenden lehnt ihn ab" <| fun () ->
            let f, _ = fake 500000L
            let api, _, reads = apiFor f
            match api.previewTargets "2026-11-01" |> run with
            | Error msg -> Expect.stringContains msg "vergangenen Monaten" "Grund"
            | Ok _ -> failtest "Fehler erwartet"
            Expect.isEmpty reads "kein Lesen"

        testCase "Lesen liest genau den übergebenen Monat" <| fun () ->
            let f, _ = fake 500000L
            let api, _, reads = apiFor f
            Expect.isOk (api.getBudgetMonth "2019-03-01" |> run) "Monat"
            Expect.equal (List.ofSeq reads) [ "2019-03-01" ] "2019"

        // Review-Fund dgrp7 (QA 7): der Live-Anschluss der Uhr beim Lesen. `budgetMonthFor` bekommt `utcNow ()`
        // aus `budgetApiWith`; mit einer anderen Uhr trüge die Übernahme einen anderen Zeitpunkt.
        testCase "Lesen stempelt die Übernahme mit der Uhr" <| fun () ->
            let f, _ = fake 500000L
            let api, _, _ = apiFor f
            Expect.isOk (api.getBudgetMonth "2017-04-01" |> run) "Monat"
            let stream =
                match Budgeting.Events.monthIdOf "2017-04-01" with
                | Ok monthId -> Budgeting.Events.streamIdOf (Budgeting.Events.MonthStream monthId)
                | Error message -> failtest message
            use conn = global.Persistence.getConnection ()
            match Budgeting.Persistence.loadBudgetEventsOn conn (Budgeting.Persistence.InStream stream) with
            | Ok events ->
                Expect.equal
                    (events |> List.map (fun e -> Budgeting.Events.eventTypeName e.Event, e.OccurredAt))
                    [ "BudgetTakenOverFromYnab", lateUtc ]
                    "Übernahme mit der Uhr"
            | Error message -> failtest message

        testCase "Übernehmen schreibt in den übergebenen Monat und stempelt mit der Uhr" <| fun () ->
            let f, _ = fake 500000L
            let api, _, _ = apiFor f
            let shown =
                match api.previewTargets "2026-12-01" |> run with
                | Ok p -> p
                | Error e -> failtest e
            match api.applyTargets "2026-12-01" shown |> run with
            | Ok (Applied report) ->
                Expect.equal report.Month "2026-12-01" "Monat"
                for state in loadedOk (Guid.Parse report.ApplicationId) do
                    Expect.equal state.Movement.OccurredAt lateUtc "Uhr"
            | other -> failtest $"Applied erwartet: {other}"

        testCase "Jeder Endpunkt reicht Token und Budget in dieser Reihenfolge an den Port" <| fun () ->
            let f, _ = fake 500000L
            let shown = previewOf f
            let move : MoveRequest = { Source = ToBeBudgetedSide; Target = CategorySide (Guid.NewGuid()); Amount = eur 1000L; CommandId = None }
            let calls : (string * (Shared.Api.BudgetApi -> unit)) list =
                [ "getBudgetFrame", fun api -> api.getBudgetFrame () |> run |> ignore
                  "getBudgetMonth", fun api -> api.getBudgetMonth "2026-12-01" |> run |> ignore
                  "previewTargets", fun api -> api.previewTargets "2026-12-01" |> run |> ignore
                  "applyTargets", fun api -> api.applyTargets "2026-12-01" shown |> run |> ignore
                  "retryFailedTargets", fun api -> api.retryFailedTargets (string (Guid.NewGuid())) |> run |> ignore
                  "moveMoney", fun api -> api.moveMoney "2026-12-01" move |> run |> ignore
                  "assignMoney", fun api ->
                    api.assignMoney "2026-12-01" { CategoryId = Guid.NewGuid(); Assigned = eur 1000L; CommandId = None } |> run |> ignore
                  "setTargetPause", fun api ->
                    api.setTargetPause "2026-12-01" { CategoryId = Guid.NewGuid(); Intent = Shared.Budgeting.PauseTarget; CommandId = None } |> run |> ignore ]
            for name, call in calls do
                let api, seen, _ = apiFor f
                call api
                Expect.equal (List.ofSeq seen |> List.distinct) [ "das-token", "das-budget" ] name
        // Der Live-Anschluss von `assignMoney`. Ohne Budget endet der Weg vor dem Netz, die Meldung
        // zeigt: Das Token kam aus den übergebenen Settings.
        testCase "Der Live-Anschluss liest Zuweisen-Token und Budget aus den Settings" <| fun () ->
            let setting key = async { return (if key = "ynab_token" then Some "das-token" else None) }
            let berlin = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"
            let api = Server.Api.liveBudgetApi setting berlin
            let result = api.assignMoney "2026-12-01" { CategoryId = Guid.NewGuid(); Assigned = eur 1000L; CommandId = None } |> run
            Expect.equal result (Error "Kein Standard-Budget konfiguriert (Settings)") "bis zum Budget gekommen"
    ]


// budgeting-qm5x9, ADR 0084 Punkt 8: Übernehmen mit Befehls-Id in der gezeigten Vorschau.
let private commandTests =
    let testCase name body =
        testCase name (fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            body ())

    testSequenced
    <| testList "Targets anwenden: Befehls-Id" [
        testCase "GIVEN ein Übernehmen mit Id ist festgehalten WHEN es mit derselben Id wiederkommt THEN liest der Server YNAB nicht, schreibt nichts und antwortet mit denselben Zeilen ohne Zu verteilen" <| fun () ->
            let f, (a, b, c, d, e) = fake 200000L
            let shown = { previewOf f with CommandId = Some (Guid.NewGuid()) }
            let first = applyOk f shown
            let ids = [ a; b; c; d; e ]
            let events = BudgetEventsFixture.eventsMentioning ids
            f.Reads <- 0
            f.Attempts.Clear()
            let again = applyOk f shown
            Expect.equal (BudgetEventsFixture.eventsMentioning ids) events "kein Ereignis"
            Expect.equal f.Reads 0 "kein FetchMonth"
            Expect.isEmpty f.Attempts "kein SetAssigned"
            Expect.equal again.ApplicationId first.ApplicationId "dieselbe Anwendung"
            Expect.equal again.Lines first.Lines "dieselben Zeilen"
            Expect.isNone again.ActualToBeBudgeted "Zu verteilen nicht neu gelesen"

        testCase "Die Id eines Zuweisens als Übernehmen gesendet wird abgelehnt, ohne Lesen, ohne Senden und ohne Ereignis" <| fun () ->
            let f, (a, b, c, d, e) = fake 200000L
            let x = Guid.NewGuid()
            let line : Budgeting.Persistence.PlannedLine =
                { CategoryId = a; CategoryName = "Essen"; OldAssigned = 0L; NewAssigned = 10000L }
            // Die Vorschau vor dem Zuweisen: dessen offene Rückschreibung ließe Targets anwenden sonst ablehnen.
            let shown = { previewOf f with CommandId = Some x }
            Budgeting.Persistence.recordDecision (Some x) now monthKey MoneyAssigned [ line ] [] [] |> run |> ignore
            let ids = [ a; b; c; d; e ]
            let events = BudgetEventsFixture.eventsMentioning ids
            f.Reads <- 0
            Expect.equal (run (apply f.Port now monthKey shown)) (Error (YnabError.RefusedByBb ForeignCommandText)) "außen abgelehnt"
            Expect.equal f.Reads 0 "kein FetchMonth"
            Expect.isEmpty f.Attempts "kein SetAssigned"
            Expect.equal (BudgetEventsFixture.eventsMentioning ids) events "kein Ereignis"

        testCase "Rennt die Id an der Vorabprüfung vorbei, antwortet Übernehmen aus dem Stream und sendet nichts" <| fun () ->
            let f, (a, _, _, _, _) = fake 200000L
            let x = Guid.NewGuid()
            let shown = { previewOf f with CommandId = Some x }
            // Eine Kategorie außerhalb der Vorschau: eine offene Rückschreibung auf einer ihrer Kategorien ließe
            // Targets anwenden vor dem Anhängen ablehnen (budgeting-dgrp7).
            let elsewhere = Guid.NewGuid()
            let line : Budgeting.Persistence.PlannedLine =
                { CategoryId = elsewhere; CategoryName = "Anderswo"; OldAssigned = 0L; NewAssigned = 50000L }
            // Während des Lesens hält ein zweiter Weg dieselbe Id fest, wie es das Netz der Transaktion abfängt.
            f.OnRead <- fun () ->
                f.OnRead <- ignore
                Budgeting.Persistence.recordDecision (Some x) now monthKey TargetsApplied [ line ] [] [] |> run |> ignore
            let report = applyOk f shown
            Expect.isEmpty f.Attempts "kein SetAssigned"
            Expect.equal (report.Lines |> List.map (fun l -> l.CategoryId, l.NewAssigned)) [ elsewhere, eur 50000L ] "die festgehaltene Entscheidung"
            Expect.isNone report.ActualToBeBudgeted "Zu verteilen nicht neu gelesen"

        // Die Anschlussstelle: `Api.applyTargetsWith` prüft die Befehls-Id über `validateTargetsApply`.
        testCase "Übernehmen über die API mit leerer Befehls-Id wird abgelehnt, ohne YNAB zu lesen" <| fun () ->
            let f, _ = fake 200000L
            let shown = { previewOf f with CommandId = Some Guid.Empty }
            f.Reads <- 0
            let result =
                Server.Api.applyTargetsWith (Some "t") (Some "b") (fun _ _ -> f.Port) (DateOnly(2026, 11, 1)) now monthKey shown
                |> run
            Expect.equal result (Error Server.Validation.EmptyCommandIdText) "Grund"
            Expect.equal f.Reads 0 "kein FetchMonth"
            Expect.isEmpty f.Attempts "kein SetAssigned"
    ]

[<Tests>]
let tests =
    testList "Targets anwenden (budgeting-h2zfn)" [ planTests; ynabTests; apiTests; flowTests; persistenceTests; lastMonthTests; wiringTests; commandTests ]
