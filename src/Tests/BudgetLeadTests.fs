module Tests.BudgetLeadTests

// budgeting-dgrp7, ADR 0084 (Nachtrag zur Verfeinerung von qm5x9 und dgrp7): BB führt das Zugewiesen. Die Abläufe
// gegen In-Memory-SQLite mit einem Fake-YNAB als Port und der Uhr als Parameter: Übernahme, Beobachten beim Lesen
// und vor jedem Befehl, erneut senden, und die Reihenfolgen von Lesen und Senden.

open System
open System.Collections.Generic
open System.Threading

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Dapper
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.CommandHandlers
open Budgeting.Events

let private eur (milliunits: int64) : Money = { Amount = decimal milliunits / 1000m; Currency = "EUR" }

let private run x = Async.RunSynchronously x

/// Die Uhr der Tests (UTC). Früher als die Uhr der übrigen Budget-Tests: "Zuletzt verwendet" liest alle Monate,
/// neueste zuerst, und soll dort die Verschiebungen dieser Tests nicht vorn sehen.
let private now = DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc)

let private monthCounter = ref 0

/// Ein eigener Monat je Test: die Übernahme gilt je Monats-Stream, ein geteilter Monat hinge von der Reihenfolge
/// der Tests ab.
let private freshMonth () : string =
    let i = Interlocked.Increment monthCounter
    $"{4000 + i / 12}-{i % 12 + 1:D2}-01"

/// Absturz zwischen Festhalten und Senden: der PATCH-Aufruf wirft, kein Ergebnis wird angehängt.
exception private Crash

/// Fake-YNAB. `Assigned` ist der Stand in YNAB; Änderungen von außen setzt der Test direkt darin.
type private FakeYnab(categories: (Guid * string * int64) list) =
    let assigned = Dictionary<Guid, int64>()
    do
        for id, _, value in categories do assigned[id] <- value
    member val Reads = 0 with get, set
    member val FailRead = false with get, set
    member val Attempts = ResizeArray<Guid * int64>()
    member val Failures = Dictionary<Guid, YnabError>()
    /// Der PATCH landet in YNAB, die Antwort geht verloren.
    member val LostAnswers = HashSet<Guid>()
    member val CrashOnWrite = false with get, set
    member val OnWrite : Guid -> int64 -> unit = (fun _ _ -> ()) with get, set
    member val OnRead : unit -> unit = ignore with get, set
    /// YNABs Goal je Kategorie: BB übernimmt es beim ersten Lesen als Target (budgeting-eh8xe).
    member val Goals = Dictionary<Guid, Target>()
    member val Hidden = HashSet<Guid>()
    member val Deleted = HashSet<Guid>()
    member val Internal = HashSet<Guid>()
    member _.Assigned = assigned
    member this.Port : YnabPort =
        { FetchMonth =
            fun monthKey ->
                async {
                    this.Reads <- this.Reads + 1
                    this.OnRead ()
                    if this.FailRead then
                        return Error (YnabError.NetworkError "YNAB weg")
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
                                          Target =
                                            (match this.Goals.TryGetValue id with
                                             | true, goal -> Some goal
                                             | _ -> None)
                                          Hidden = this.Hidden.Contains id
                                          Deleted = this.Deleted.Contains id
                                          Internal = this.Internal.Contains id }) }
                }
          SetAssigned =
            fun _ id milliunits ->
                async {
                    this.Attempts.Add((id, milliunits))
                    this.OnWrite id milliunits
                    if this.CrashOnWrite then raise Crash
                    match this.Failures.TryGetValue id with
                    | true, error -> return Error error
                    | _ ->
                        assigned[id] <- milliunits
                        if this.LostAnswers.Contains id then return Error (YnabError.NetworkError "Antwort verloren")
                        else return Ok ()
                }
          FetchCategoryGoals =
            fun () ->
                async {
                    return
                        Ok
                            [ for id, name, _ in categories do
                                  match this.Goals.TryGetValue id with
                                  | true, goal -> yield { Id = id; Name = name; Target = Some goal }
                                  | _ -> () ]
                } }

/// Ein monatliches Goal über `milliunits`: BBs Fehlbetrag ist Betrag − Soll (ADR 0084 Punkt 7).
let private monthlyGoal (milliunits: int64) : Target =
    { Goal = eur milliunits; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }

let private fake () =
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    FakeYnab([ a, "Lebensmittel", 100000L; b, "Urlaub", 20000L ]), a, b

// ============================================
// Stream lesen, unabhängig von der Faltung
// ============================================

let private streamOf (monthKey: string) =
    match monthIdOf monthKey with
    | Ok monthId -> streamIdOf (MonthStream monthId)
    | Error message -> failtest message

let private recordedEvents (monthKey: string) : Budgeting.Persistence.RecordedBudgetEvent list =
    use conn = global.Persistence.getConnection ()
    match Budgeting.Persistence.loadBudgetEventsOn conn (Budgeting.Persistence.InStream (streamOf monthKey)) with
    | Ok events -> events
    | Error message -> failtest message

let private eventsOf (monthKey: string) : BudgetEvent list = recordedEvents monthKey |> List.map (fun e -> e.Event)

let private typesOf (monthKey: string) = eventsOf monthKey |> List.map eventTypeName

/// Die Beobachtungen im Stream als (Kategorie, vorher, nachher).
let private observations (monthKey: string) : (Guid * int64 * int64) list =
    eventsOf monthKey
    |> List.choose (fun e ->
        match e with
        | BudgetEvent.AssignmentChangeObservedInYnab p -> Some (p.CategoryId, p.PreviousAssigned, p.Assigned)
        | _ -> None)

let private linesOf (event: BudgetEvent) : BudgetLine list =
    match event with
    | BudgetEvent.AssignmentChangedInBb p -> [ p.Line ]
    | BudgetEvent.MoneyMoved p -> [ p.From; p.To ]
    | BudgetEvent.TargetsApplied p -> p.Lines
    | _ -> []

/// Entscheidungszeilen nach der Übernahme ohne schließendes Ergebnis (geschrieben, ersetzt, zurückgezogen), je
/// Kategorie als (Entscheidung, Wert). Abgezählt aus den Ereignissen, ohne die Faltung des Servers.
let private unclosedLines (monthKey: string) : Map<Guid, (Guid * int64) list> =
    let events = recordedEvents monthKey
    let start =
        events
        |> List.tryFindIndexBack (fun e ->
            match e.Event with
            | BudgetEvent.BudgetTakenOverFromYnab _ -> true
            | _ -> false)
        |> Option.defaultValue events.Length
    let after = events |> List.skip start
    let closed =
        after
        |> List.choose (fun e ->
            match e.Event with
            | BudgetEvent.AssignmentWrittenBackToYnab r
            | BudgetEvent.AssignmentWriteBackSupersededInYnab r
            | BudgetEvent.AssignmentWriteBackWithdrawn r -> Some (r.DecisionEventId, r.CategoryId)
            | _ -> None)
        |> Set.ofList
    after
    |> List.collect (fun e -> linesOf e.Event |> List.map (fun l -> l.CategoryId, (e.EventId, l.Assigned)))
    |> List.filter (fun (category, (decision, _)) -> not (closed.Contains (decision, category)))
    |> List.groupBy fst
    |> List.map (fun (category, rows) -> category, rows |> List.map snd)
    |> Map.ofList

let private readWith (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection) (f: FakeYnab) (monthKey: string) =
    QueryHandlers.budgetMonthWith connect f.Port now monthKey |> run

let private read (f: FakeYnab) (monthKey: string) : BudgetMonth =
    match readWith global.Persistence.getConnection f monthKey with
    | Ok month -> month
    | Error e -> failtest $"Monat erwartet: {e}"

let private shown (month: BudgetMonth) (id: Guid) : BudgetCategory =
    month.Groups |> List.collect (fun g -> g.Categories) |> List.find (fun c -> c.Id = id)

/// O3: Vor jedem PATCH prüft der Fake den Stream. Der gesendete Wert ist der Wert der jüngsten Entscheidung der
/// Kategorie, und nach ihr liegt weder eine Beobachtung derselben Kategorie (sonst wäre das Soll der YNAB-Wert)
/// noch ein Zurückziehen dieser Entscheidung für die Kategorie (sonst wäre das Soll der bestätigte Stand).
let private watchPatches (f: FakeYnab) (monthKey: string) : ResizeArray<string> =
    let violations = ResizeArray<string>()
    f.OnWrite <-
        fun category value ->
            let events = recordedEvents monthKey
            let lastDecision =
                events
                |> List.indexed
                |> List.choose (fun (i, e) ->
                    linesOf e.Event
                    |> List.tryFind (fun l -> l.CategoryId = category)
                    |> Option.map (fun l -> i, e.EventId, l.Assigned))
                |> List.tryLast
            match lastDecision with
            | None -> violations.Add $"PATCH {value} für {category} ohne Entscheidung"
            | Some (index, decision, decided) ->
                if decided <> value then violations.Add $"PATCH {value} für {category}, entschieden {decided}"
                for e in events |> List.skip (index + 1) do
                    match e.Event with
                    | BudgetEvent.AssignmentChangeObservedInYnab p when p.CategoryId = category ->
                        violations.Add $"PATCH {value} für {category} nach einer Beobachtung"
                    | BudgetEvent.AssignmentWriteBackWithdrawn r when r.DecisionEventId = decision && r.CategoryId = category ->
                        violations.Add $"PATCH {value} für {category} nach dem Zurückziehen"
                    | _ -> ()
    violations

/// O1 bis O3 nach einem abschließenden Lesen, liefert den gelesenen Monat. `expected` sind die externen Änderungen
/// des Fakes, deren Endwert vom Soll davor abweicht: in den festen Wegen von Hand bestimmt, in der Aufzählung vom
/// Merker.
let private checkOraclesOn (f: FakeYnab) (monthKey: string) (violations: ResizeArray<string>) (expected: (Guid * int64 * int64) list) : BudgetMonth =
    let month = read f monthKey
    Expect.equal (List.ofSeq violations) [] "O3: kein PATCH neben dem Soll"
    Expect.equal (observations monthKey) expected "O1: genau die abweichenden externen Änderungen sind beobachtet"
    let unclosed = unclosedLines monthKey
    for KeyValue (category, value) in f.Assigned do
        let c = shown month category
        match unclosed |> Map.tryFind category |> Option.defaultValue [] with
        | [] ->
            Expect.isNone c.OpenWriteBack "O2: keine offene Rückschreibung, keine Markierung"
            Expect.equal c.Assigned (eur value) "O2: ohne offene Rückschreibung ist das Soll der YNAB-Wert"
        | [ decision, decided ] ->
            Expect.equal c.OpenWriteBack (Some decision) "O2: die eine offene Rückschreibung ist markiert"
            Expect.equal c.Assigned (eur decided) "O2: ihr Wert ist das Soll"
        | more -> failtest $"O2: {more.Length} offene Rückschreibungen für {category}"
    month

let private checkOracles f monthKey violations expected =
    checkOraclesOn f monthKey violations expected |> ignore

let private assign (f: FakeYnab) (monthKey: string) (category: Guid) (value: int64) (commandId: Guid option) =
    assignMoney f.Port now monthKey { CategoryId = category; Assigned = eur value; CommandId = commandId } |> run

let private assignOk f monthKey category value =
    match assign f monthKey category value None with
    | Ok (Ok report) -> report
    | other -> failtest $"Bericht erwartet: {other}"

let private moveOk (f: FakeYnab) (monthKey: string) (source: Guid) (target: Guid) (amount: int64) =
    match moveMoney f.Port now monthKey { Source = CategorySide source; Target = CategorySide target; Amount = eur amount; CommandId = None } |> run with
    | Ok (Ok report) -> report
    | other -> failtest $"Bericht erwartet: {other}"

let private crashes (work: unit -> unit) =
    Expect.throwsT<Crash> work "Absturz beim Senden"

let private retryOk (f: FakeYnab) (applicationId: Guid) =
    match retryFailed f.Port now applicationId |> run with
    | Ok report -> report
    | Error e -> failtest $"Bericht erwartet: {e}"

/// Die Gruppen-Id der jüngsten Entscheidung im Monat.
let private lastDecision (monthKey: string) : Guid =
    recordedEvents monthKey |> List.filter (fun e -> not (List.isEmpty (linesOf e.Event))) |> List.last |> fun e -> e.EventId

let private testCase name body =
    testCase name (fun () ->
        Budgeting.Persistence.initializeBudgetingSchema ()
        body ())

/// Wartet, bis ein Ablauf an der Sperre des Monats steht. Die Frist ist nur die Grenze für einen roten Test.
let private atGate (monthKey: string) : bool =
    SpinWait.SpinUntil((fun () -> MonthGuard.waitingFor monthKey = 1), TimeSpan.FromSeconds 10.)

// ============================================
// Aufzählung der Reihenfolgen (Review-Fund dgrp7, W2; Doktrin Punkt 20)
// ============================================

/// Was die Aufzählung selbst über eine Zeile einer Entscheidung weiß.
type private LineFate =
    | LineOpen
    | LineWritten
    /// In YNAB ersetzt: beobachtet, solange die Zeile offen war.
    | LineSuperseded
    | LineWithdrawn

type private ModelLine = { Id: int; Category: Guid; Value: int64; mutable Fate: LineFate }

/// Eine Entscheidung im Merker, die Zeilen in Sendereihenfolge (bei einer Verschiebung erst die Quelle).
type private ModelDecision = { Lines: ModelLine list; IsMove: bool }

/// Der eigene Merker der Aufzählung, unabhängig von Faltung und Echo-Regel des Servers: je Kategorie Soll,
/// bestätigter Stand und offene Zeile, dazu welche Kategorien sich seit dem letzten Abgleich in YNAB von außen
/// geändert haben. Daraus folgt O1: erwartet ist eine Beobachtung genau für eine externe Änderung, deren Endwert
/// beim nächsten Abgleich vom Soll davor abweicht.
type private LeadModel(f: FakeYnab, categories: Guid list) =
    let soll = Dictionary<Guid, int64>()
    let confirmed = Dictionary<Guid, int64>()
    let openLine = Dictionary<Guid, ModelLine>()
    let changed = HashSet<Guid>()
    let decisions = ResizeArray<ModelDecision>()
    let expected = ResizeArray<Guid * int64 * int64>()
    let mutable takenOver = false
    let mutable nextLine = 0
    let isOpen (l: ModelLine) =
        match openLine.TryGetValue l.Category with
        | true, o -> o.Id = l.Id
        | _ -> false
    let written (l: ModelLine) =
        l.Fate <- LineWritten
        confirmed[l.Category] <- l.Value
        openLine.Remove l.Category |> ignore
    let withdraw (l: ModelLine) =
        l.Fate <- LineWithdrawn
        soll[l.Category] <- confirmed[l.Category]
        openLine.Remove l.Category |> ignore
    member _.Soll(c: Guid) = soll[c]
    member _.Expected = List.ofSeq expected
    member _.LastDecision = if decisions.Count = 0 then None else Some decisions[decisions.Count - 1]
    /// Eine Änderung in YNAB von außen.
    member _.ChangeInYnab (c: Guid) (delta: int64) =
        f.Assigned[c] <- f.Assigned[c] + delta
        changed.Add c |> ignore
    /// Ein Abgleich unter der Sperre (Lesen, vor jedem Befehl, vor erneut senden).
    member _.Observe() =
        for c in categories do
            let y = f.Assigned[c]
            if not takenOver then
                soll[c] <- y
                confirmed[c] <- y
            elif y = soll[c] then
                match openLine.TryGetValue c with
                | true, l -> written l
                | _ -> ()
            elif changed.Contains c then
                expected.Add((c, soll[c], y))
                match openLine.TryGetValue c with
                | true, l -> l.Fate <- LineSuperseded
                | _ -> ()
                openLine.Remove c |> ignore
                soll[c] <- y
                confirmed[c] <- y
        changed.Clear()
        takenOver <- true
    /// Eine Entscheidung: Soll ist der neue Wert, eine offene Zeile derselben Kategorie ist zurückgezogen.
    member _.Decide (isMove: bool) (lines: (Guid * int64) list) : ModelDecision =
        let decided =
            lines
            |> List.map (fun (c, v) ->
                nextLine <- nextLine + 1
                { Id = nextLine; Category = c; Value = v; Fate = LineOpen })
        for l in decided do
            match openLine.TryGetValue l.Category with
            | true, old -> old.Fate <- LineWithdrawn
            | _ -> ()
            soll[l.Category] <- l.Value
            openLine[l.Category] <- l
        let decision = { Lines = decided; IsMove = isMove }
        decisions.Add decision
        decision
    member _.Written(l: ModelLine) = written l
    /// Erneut senden nach dem Abgleich: offene Zeilen gehen hinaus, bei einer Verschiebung mit der Paarregel aus
    /// ADR 0069 Punkt 3 (eine verlorene Seite zieht die andere zurück). Jeder PATCH kommt hier an.
    member _.Retry(d: ModelDecision) =
        match d.IsMove, d.Lines with
        | true, [ source; target ] ->
            let sourceOpen, targetOpen = isOpen source, isOpen target
            let sourceWithdrawn = sourceOpen && not targetOpen && target.Fate = LineSuperseded
            if sourceWithdrawn then withdraw source
            elif sourceOpen then written source
            if targetOpen then
                if sourceWithdrawn || (not sourceOpen && source.Fate <> LineWritten) then withdraw target
                else written target
        | _ -> for l in d.Lines do if isOpen l then written l

/// Wie der PATCH eines Zuweisens ausgeht.
type private AssignFate =
    | Lands
    /// Gescheitert oder Antwort verloren: BB hält ihn fehlgeschlagen fest, die Zeile bleibt offen.
    | StaysOpen
    /// Absturz zwischen Festhalten und Senden: kein Ergebnis.
    | Crashes

/// Ein Schritt der Aufzählung: ruft die echten Handler und führt den Merker nach.
type private LeadStep = {
    Name: string
    /// Läuft der Schritt in diesem Stand? Erneut senden braucht eine Entscheidung.
    Applies: LeadModel -> bool
    Run: FakeYnab -> string -> Guid -> Guid -> LeadModel -> unit
}

let private leadSteps : LeadStep list =
    let always (_: LeadModel) = true
    let noArm (_: FakeYnab) (_: Guid) (_: Guid) (_: LeadModel) = ()
    /// Zuweisen auf a, zehn Euro über dem Soll. Der Merker gleicht dort ab, wo der Server liest: am Anfang des
    /// Befehls, vor dem Planen.
    let assignWith (arm: FakeYnab -> Guid -> Guid -> LeadModel -> unit) (fate: AssignFate) (f: FakeYnab) month a b (m: LeadModel) =
        m.Observe()
        let value = m.Soll a + 10000L
        let decision = m.Decide false [ a, value ]
        arm f a b m
        match fate with
        | Crashes -> crashes (fun () -> assign f month a value None |> ignore)
        | Lands
        | StaysOpen ->
            match assign f month a value None with
            | Ok (Ok _) -> ()
            | other -> failtest $"Bericht erwartet: {other}"
        if fate = Lands then m.Written decision.Lines.Head
        f.Failures.Clear()
        f.LostAnswers.Clear()
        f.CrashOnWrite <- false
    /// Verschieben von a nach b, eine Seite scheitert.
    let moveWith (sourceFails: bool) (f: FakeYnab) month a b (m: LeadModel) =
        m.Observe()
        let decision = m.Decide true [ a, m.Soll a - 5000L; b, m.Soll b + 5000L ]
        f.Failures[if sourceFails then a else b] <- YnabError.NetworkError "weg"
        moveOk f month a b 5000L |> ignore
        f.Failures.Clear()
        if not sourceFails then m.Written decision.Lines.Head
    /// Eine Änderung in YNAB nach dem FetchMonth des Befehls, unmittelbar vor seinem PATCH.
    let between (onA: bool) (f: FakeYnab) (a: Guid) (b: Guid) (m: LeadModel) =
        let original = f.OnWrite
        f.OnWrite <-
            fun category value ->
                original category value
                f.OnWrite <- original
                m.ChangeInYnab (if onA then a else b) 7000L
    [ { Name = "Lesen"; Applies = always; Run = fun f month _ _ m -> m.Observe(); read f month |> ignore }
      { Name = "Zuweisen"; Applies = always; Run = assignWith noArm Lands }
      { Name = "Zuweisen scheitert"
        Applies = always
        Run = assignWith (fun f a _ _ -> f.Failures[a] <- YnabError.NetworkError "weg") StaysOpen }
      { Name = "Zuweisen, Absturz vor dem Senden"; Applies = always; Run = assignWith (fun f _ _ _ -> f.CrashOnWrite <- true) Crashes }
      { Name = "Zuweisen, PATCH angekommen, Antwort verloren"
        Applies = always
        Run = assignWith (fun f a _ _ -> f.LostAnswers.Add a |> ignore) StaysOpen }
      { Name = "Zuweisen, YNAB ändert b zwischen FetchMonth und PATCH"; Applies = always; Run = assignWith (between false) Lands }
      { Name = "Zuweisen, YNAB ändert a zwischen FetchMonth und PATCH"; Applies = always; Run = assignWith (between true) Lands }
      { Name = "Verschieben, Ziel scheitert"; Applies = always; Run = moveWith false }
      { Name = "Verschieben, Quelle scheitert"; Applies = always; Run = moveWith true }
      { Name = "YNAB ändert a"; Applies = always; Run = fun _ _ a _ m -> m.ChangeInYnab a 3000L }
      { Name = "YNAB ändert b"; Applies = always; Run = fun _ _ _ b m -> m.ChangeInYnab b 3000L }
      { Name = "Erneut senden"
        Applies = fun m -> m.LastDecision.IsSome
        Run =
          fun f month _ _ m ->
              m.Observe()
              m.Retry m.LastDecision.Value
              retryOk f (lastDecision month) |> ignore } ]

/// Alle Folgen der Länge `depth` aus den Schritten.
let rec private paths (depth: int) : LeadStep list list =
    if depth = 0 then [ [] ]
    else [ for step in leadSteps do for rest in paths (depth - 1) -> step :: rest ]

/// Geht eine Folge auf einem eigenen Monat ab und prüft danach O1 bis O3 und das Soll gegen den Merker.
/// `false`, wenn ein Schritt in seinem Stand nicht läuft.
let private walkPath (path: LeadStep list) : bool =
    let f, a, b = fake ()
    let month = freshMonth ()
    let shownPath = path |> List.map (fun s -> s.Name) |> String.concat ", "
    try
        let watched = watchPatches f month
        let model = LeadModel(f, [ a; b ])
        let rec go (steps: LeadStep list) =
            match steps with
            | [] -> true
            | step :: rest when step.Applies model ->
                step.Run f month a b model
                go rest
            | _ -> false
        if go path then
            model.Observe()
            let page = checkOraclesOn f month watched model.Expected
            for c in [ a; b ] do
                Expect.equal (shown page c).Assigned (eur (model.Soll c)) "das Soll wie im Merker"
            true
        else
            false
    with ex ->
        failtest $"{shownPath}: {ex.Message}"

// ============================================
// Reihenfolgen von Lesen und Senden
// ============================================

let private orderTests =
    testList "Reihenfolgen von Lesen und Senden" [
        // Die Aufzählung aller Folgen aus drei Schritten (Doktrin Punkt 20). Die festen Wege darunter bleiben
        // Pflicht: Sperre, Wiederholung mit Befehls-Id und Paarregel brauchen eigene Aufbauten.
        testCase "Jede Folge aus drei Schritten hält O1 bis O3 und das Soll des Merkers" <| fun () ->
            let walked = paths 3 |> List.filter walkPath |> List.length
            // 12 hoch 3 Folgen, ohne die 189, in denen Erneut senden vor jeder Entscheidung käme (144 + 3 · 12 + 3 · 3).
            Expect.equal walked 1539 "so viele Folgen laufen wirklich"

        testCase "Lesen vor dem Befehl, dazwischen eine Änderung in YNAB" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            read f month |> ignore
            f.Assigned[b] <- 25000L
            assignOk f month a 120000L |> ignore
            checkOracles f month watched [ b, 20000L, 25000L ]

        testCase "Befehl vor dem Lesen" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            assignOk f month a 120000L |> ignore
            checkOracles f month watched []

        testCase "observeMonth wartet an der gehaltenen Sperre eines laufenden Befehls" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            use sending = new SemaphoreSlim(0, 1)
            use release = new SemaphoreSlim(0, 1)
            let check = f.OnWrite
            f.OnWrite <-
                fun category value ->
                    check category value
                    sending.Release() |> ignore
                    release.Wait()
            let command = Async.StartAsTask(async { return assign f month a 120000L None })
            sending.Wait()
            let readsBefore = f.Reads
            let observing = Async.StartAsTask(observeMonth f.Port now month)
            // Belegt über die Sperre selbst, nicht über eine Wartezeit (Review-Fund dgrp7, W7).
            Expect.isTrue (atGate month) "observeMonth steht an der Sperre"
            Expect.equal f.Reads readsBefore "kein FetchMonth, solange der Befehl die Sperre hält"
            Expect.isFalse observing.IsCompleted "observeMonth wartet"
            release.Release() |> ignore
            Expect.isOk command.Result "Befehl"
            Expect.isOk observing.Result "Beobachten"
            Expect.equal f.Reads (readsBefore + 1) "danach genau ein FetchMonth"
            f.OnWrite <- check
            checkOracles f month watched []

        testCase "Absturz zwischen Festhalten und Senden, dann Lesen, dann erneut senden" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.CrashOnWrite <- true
            crashes (fun () -> assign f month a 120000L None |> ignore)
            f.CrashOnWrite <- false
            let page = read f month
            Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 120000L, Some (lastDecision month)) "offen, Soll mit Markierung"
            Expect.isEmpty (observations month) "keine Beobachtung"
            let report = retryOk f (lastDecision month)
            Expect.equal (report.Lines |> List.map (fun l -> l.Outcome)) [ Written ] "erneut gesendet"
            Expect.contains (typesOf month) "AssignmentWrittenBackToYnab" "geschrieben"
            checkOracles f month watched []

        testCase "Absturz, dann eine Änderung in YNAB auf einen dritten Wert" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.CrashOnWrite <- true
            crashes (fun () -> assign f month a 120000L None |> ignore)
            f.CrashOnWrite <- false
            f.Assigned[a] <- 90000L
            let page = read f month
            Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 90000L, None) "YNAB gewinnt"
            Expect.contains (typesOf month) "AssignmentWriteBackSupersededInYnab" "ersetzt"
            checkOracles f month watched [ a, 120000L, 90000L ]

        testCase "PATCH angekommen, Antwort verloren: das Lesen sieht ihn geschrieben" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.LostAnswers.Add a |> ignore
            let report = assignOk f month a 120000L
            Expect.isTrue (report.Lines |> List.forall (fun l -> l.Outcome <> Written)) "BB meldet fehlgeschlagen"
            f.LostAnswers.Clear()
            let page = read f month
            Expect.isNone (shown page a).OpenWriteBack "geschrieben"
            Expect.equal (List.last (typesOf month)) "AssignmentWrittenBackToYnab" "per Echo geschlossen"
            checkOracles f month watched []

        testCase "Verschiebung mit gescheiterter Quelle und einer Änderung am Ziel in YNAB" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.Failures[a] <- YnabError.NetworkError "weg"
            let report = moveOk f month a b 30000L
            f.Failures.Clear()
            f.Assigned[b] <- 25000L
            let page = read f month
            Expect.equal (shown page a).OpenWriteBack (Some (Guid.Parse report.ApplicationId)) "die Quelle bleibt offen"
            f.Attempts.Clear()
            retryOk f (Guid.Parse report.ApplicationId) |> ignore
            Expect.isEmpty f.Attempts "die Quelle geht nicht hinaus, das Geld käme sonst aus Zu verteilen"
            checkOracles f month watched [ b, 50000L, 25000L ]

        testCase "Verschiebung mit geschriebener Quelle und gescheitertem Ziel" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            f.Failures[b] <- YnabError.NetworkError "weg"
            let report = moveOk f month a b 30000L
            f.Failures.Clear()
            let page = read f month
            Expect.equal ((shown page b).Assigned, (shown page b).OpenWriteBack) (eur 50000L, Some (Guid.Parse report.ApplicationId)) "das Ziel ist offen"
            f.Attempts.Clear()
            retryOk f (Guid.Parse report.ApplicationId) |> ignore
            Expect.equal (List.ofSeq f.Attempts) [ b, 50000L ] "nur das Ziel"
            checkOracles f month watched []

        testCase "Wiederholung mit bekannter Befehls-Id nach Absturz vor dem Senden" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            let watched = watchPatches f month
            let x = Guid.NewGuid()
            f.CrashOnWrite <- true
            crashes (fun () -> assign f month a 120000L (Some x) |> ignore)
            f.CrashOnWrite <- false
            let before = typesOf month
            f.Reads <- 0
            f.Attempts.Clear()
            match assign f month a 120000L (Some x) with
            | Ok (Ok report) -> Expect.equal (report.Lines |> List.map (fun l -> l.CategoryId)) [ a ] "die festgehaltene Entscheidung"
            | other -> failtest $"Bericht erwartet: {other}"
            Expect.equal f.Reads 0 "kein YNAB-Aufruf"
            Expect.isEmpty f.Attempts "kein PATCH"
            Expect.equal (typesOf month) before "keine Beobachtung, kein Ereignis"
            checkOracles f month watched []
    ]

// ============================================
// Übernahme
// ============================================

let private takeoverTests =
    testList "Übernahme" [
        testCase "Lesen und Befehl übernehmen denselben Monat gleichzeitig: ein Übernahme-Ereignis" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            // Wer zuerst liest, hält die Sperre, bis der andere an ihr steht oder (ohne Sperre) selbst liest. So
            // überlappen beide sicher, ohne feste Wartezeit (Review-Fund dgrp7, W7).
            let reads = ref 0
            f.OnRead <-
                fun () ->
                    if Interlocked.Increment(&reads.contents) = 1 then
                        SpinWait.SpinUntil((fun () -> MonthGuard.waitingFor month = 1 || reads.Value > 1), TimeSpan.FromSeconds 10.)
                        |> ignore
            let reading = Async.StartAsTask(async { return readWith global.Persistence.getConnection f month })
            let command = Async.StartAsTask(async { return assign f month a 120000L None })
            Expect.isOk reading.Result "Lesen"
            Expect.isOk command.Result "Befehl"
            Expect.equal (typesOf month |> List.filter ((=) "BudgetTakenOverFromYnab")) [ "BudgetTakenOverFromYnab" ] "eine Übernahme"

        testCase "Ein Monat mit Altereignissen wird übernommen, offene migrierte Rückschreibungen werden zurückgezogen" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            // Vor der Übernahme festgehalten, wie die Startmigration es hinterlässt: ohne Ergebnis und fehlgeschlagen.
            let line id name oldValue newValue : Budgeting.Persistence.PlannedLine =
                { CategoryId = id; CategoryName = name; OldAssigned = oldValue; NewAssigned = newValue }
            let pending =
                Budgeting.Persistence.recordDecision None now month MoneyAssigned [ line a "Lebensmittel" 90000L 95000L ] [] [] |> run
                |> BudgetEventsFixture.recordedOrFail
            let failed =
                Budgeting.Persistence.recordDecision None now month MoneyAssigned [ line b "Urlaub" 10000L 15000L ] [] [] |> run
                |> BudgetEventsFixture.recordedOrFail
            BudgetEventsFixture.recordResult failed.Head now (Failed "Rate-Limit")
            let page = read f month
            Expect.equal
                (typesOf month |> List.skip 3)
                [ "BudgetTakenOverFromYnab"; "AssignmentWriteBackWithdrawn"; "AssignmentWriteBackWithdrawn" ]
                "Übernahme, dann beide zurückgezogen"
            Expect.equal
                (eventsOf month
                 |> List.choose (function
                     | BudgetEvent.AssignmentWriteBackWithdrawn r -> Some r.DecisionEventId
                     | _ -> None))
                [ pending.Head.ApplicationId; failed.Head.ApplicationId ]
                "die migrierten Entscheidungen"
            Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 100000L, None) "Soll ist YNABs Stand"
            Expect.isEmpty (observations month) "keine Beobachtung"

        testCase "Die Übernahme lässt internal aus, nimmt ausgeblendete und gelöschte und trägt die Uhr des Tests" <| fun () ->
            let visible, hidden, deleted, inflow = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let f = FakeYnab([ visible, "Miete", 80000L; hidden, "Alt", 1000L; deleted, "Weg", 2000L; inflow, "Inflow", 0L ])
            f.Hidden.Add hidden |> ignore
            f.Deleted.Add deleted |> ignore
            f.Internal.Add inflow |> ignore
            let month = freshMonth ()
            read f month |> ignore
            match recordedEvents month with
            | [ e ] ->
                Expect.equal e.OccurredAt now "occurred_at ist die Uhr des Tests"
                match e.Event with
                | BudgetEvent.BudgetTakenOverFromYnab p ->
                    Expect.equal
                        (p.Categories |> List.map (fun c -> c.CategoryId, c.Assigned))
                        [ visible, 80000L; hidden, 1000L; deleted, 2000L ]
                        "ohne internal"
                | other -> failtest $"Übernahme erwartet: {other}"
            | other -> failtest $"ein Ereignis erwartet, kamen {other.Length}"

        testCase "Übernahme im selben Befehl: ein FetchMonth, keine Beobachtung, vorher ist der übernommene Wert" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            assignOk f month a 120000L |> ignore
            Expect.equal f.Reads 1 "ein FetchMonth"
            Expect.equal (typesOf month) [ "BudgetTakenOverFromYnab"; "AssignmentChangedInBb"; "AssignmentWrittenBackToYnab" ] "Ereignisse"
            match eventsOf month |> List.item 1 with
            | BudgetEvent.AssignmentChangedInBb p -> Expect.equal p.Line.PreviousAssigned 100000L "vorher übernommen"
            | other -> failtest $"Zuweisen erwartet: {other}"

        testCase "YNAB nicht erreichbar an einem Monat ohne Übernahme: alle Befehle lehnen außen ab, events bleibt leer" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            f.FailRead <- true
            let isOuterError result =
                match result with
                | Error (YnabError.NetworkError _) -> true
                | _ -> false
            Expect.isTrue (assign f month a 120000L None |> isOuterError) "Zuweisen"
            Expect.isTrue
                (moveMoney f.Port now month { Source = CategorySide a; Target = CategorySide b; Amount = eur 10000L; CommandId = None }
                 |> run
                 |> isOuterError)
                "Verschieben"
            let shownPreview : TargetPreview =
                { Month = month; Lines = []; Skipped = []; Total = eur 0L; ToBeBudgeted = eur 100000L; ToBeBudgetedAfter = eur 100000L; CommandId = None }
            Expect.isTrue (apply f.Port now month shownPreview |> run |> isOuterError) "Anwenden"
            Expect.isTrue
                (setTargetPause f.Port now month month { CategoryId = a; Intent = PauseTarget; CommandId = None } |> run |> isOuterError)
                "Pause"
            Expect.isEmpty (recordedEvents month) "events unverändert"
            Expect.isEmpty f.Attempts "nichts gesendet"
    ]

// ============================================
// Targets anwenden und Lesen
// ============================================

/// Fängt, was der Server ins Fehler-Log schreibt, während `work` läuft.
let private stderrOf (work: unit -> 'a) : 'a * string =
    let original = Console.Error
    use writer = new IO.StringWriter()
    Console.SetError writer
    try
        let result = work ()
        result, writer.ToString()
    finally
        Console.SetError original

let private readTests =
    // Fängt das Server-Log über `Console.SetError`, wie die Tests in `UnderfundedTests` (gleiche Gruppe).
    testSequencedGroup "Server-Log"
    <| testList "Lesen und Targets anwenden" [
        // Regressionstest budgeting-hbhzf: vorher lehnte Targets anwenden bei offener Rückschreibung ab
        // (`TargetsOpenWriteBackText`), weil YNABs Fehlbetrag auf YNABs Zugewiesen gerechnet war. BBs Fehlbetrag
        // rechnet gegen das Soll.
        testCase "Targets anwenden mit offener Rückschreibung rechnet gegen das Soll und lehnt nicht ab" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            f.Goals[a] <- monthlyGoal 125000L
            f.CrashOnWrite <- true
            crashes (fun () -> assign f month a 120000L None |> ignore)
            f.CrashOnWrite <- false
            f.Attempts.Clear()
            let shownPreview =
                match preview f.Port now month |> run with
                | Ok p -> p
                | other -> failtest $"Vorschau erwartet: {other}"
            Expect.equal
                (shownPreview.Lines |> List.map (fun l -> l.CategoryId, l.OldAssigned, l.Underfunded, l.NewAssigned))
                [ a, eur 120000L, eur 5000L, eur 125000L ]
                "vom Soll aus: 125 − 120"
            match apply f.Port now month shownPreview |> run with
            | Ok (Applied _) -> ()
            | other -> failtest $"Applied erwartet: {other}"
            Expect.equal (List.ofSeq f.Attempts) [ a, 125000L ] "Soll plus BBs Fehlbetrag"

        testCase "Lesen, dessen Anhängen in der Datenbank scheitert, liefert den Monat und loggt den Fehler" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            anchor.Execute("CREATE TRIGGER no_events BEFORE INSERT ON events BEGIN SELECT RAISE(ABORT, 'Platte voll'); END") |> ignore
            let f, a, _ = fake ()
            let month = freshMonth ()
            let result, log = stderrOf (fun () -> readWith connect f month)
            match result with
            | Ok page -> Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 100000L, None) "aus dem YNAB-Stand"
            | Error e -> failtest $"Monat erwartet: {e}"
            Expect.stringContains log "Platte voll" "Fehler im Log"

        testCase "Lesen, dessen Verbindungs-Port beim Anhängen wirft, liefert den Monat und loggt den Fehler" <| fun () ->
            let calls = ref 0
            let connect () =
                calls.Value <- calls.Value + 1
                if calls.Value = 1 then failwith "Verbindung weg" else global.Persistence.getConnection ()
            let f, a, _ = fake ()
            let month = freshMonth ()
            let result, log = stderrOf (fun () -> readWith connect f month)
            match result with
            | Ok page -> Expect.equal (shown page a).Assigned (eur 100000L) "aus dem YNAB-Stand"
            | Error e -> failtest $"Monat erwartet: {e}"
            Expect.stringContains log "Verbindung weg" "Fehler im Log"
            Expect.isEmpty (recordedEvents month) "nichts angehängt"

        testCase "Lesen mit scheiterndem FetchMonth scheitert wie bisher" <| fun () ->
            let f, _, _ = fake ()
            f.FailRead <- true
            Expect.equal (readWith global.Persistence.getConnection f (freshMonth ())) (Error (YnabError.NetworkError "YNAB weg")) "Fehler"

        testCase "Lesen braucht genau einen FetchMonth" <| fun () ->
            let f, _, _ = fake ()
            let month = freshMonth ()
            read f month |> ignore
            Expect.equal f.Reads 1 "erstes Lesen mit Übernahme"
            read f month |> ignore
            Expect.equal f.Reads 2 "zweites Lesen"

        testCase "Lesen bei offener Rückschreibung zeigt das Soll mit Markierung, ohne sie fehlt die Markierung" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            f.Failures[a] <- YnabError.NetworkError "weg"
            let report = assignOk f month a 120000L
            let page = read f month
            Expect.equal ((shown page a).Assigned, (shown page a).OpenWriteBack) (eur 120000L, Some (Guid.Parse report.ApplicationId)) "Soll mit Markierung"
            Expect.equal ((shown page a).Available, page.ToBeBudgeted) (eur 100000L, eur 100000L) "Verfügbar und Zu verteilen bleiben YNABs"
            Expect.equal ((shown page b).Assigned, (shown page b).OpenWriteBack) (eur 20000L, None) "ohne offene Rückschreibung keine Markierung"

        testCase "Denselben Wert bei offener Rückschreibung noch einmal zuweisen erzeugt nichts" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            f.Failures[a] <- YnabError.NetworkError "weg"
            assignOk f month a 120000L |> ignore
            let before = typesOf month
            f.Attempts.Clear()
            let again = assignOk f month a 120000L
            Expect.isEmpty again.Lines "kein Bericht"
            Expect.isEmpty f.Attempts "kein PATCH"
            Expect.equal (typesOf month) before "kein Ereignis"

        testCase "Eine neue Entscheidung auf offener Rückschreibung zieht die alte im selben Append zurück, die Entscheidung zuerst" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            f.Failures[a] <- YnabError.NetworkError "weg"
            let first = assignOk f month a 120000L
            f.Failures.Clear()
            let x = Guid.NewGuid()
            match assign f month a 130000L (Some x) with
            | Ok (Ok _) -> ()
            | other -> failtest $"Bericht erwartet: {other}"
            let events = recordedEvents month
            let decision = events |> List.findIndex (fun e -> e.EventId = lastDecision month)
            match events |> List.item (decision + 1) |> fun e -> e.Event with
            | BudgetEvent.AssignmentWriteBackWithdrawn r -> Expect.equal r.DecisionEventId (Guid.Parse first.ApplicationId) "die alte"
            | other -> failtest $"Zurückziehen direkt hinter der Entscheidung erwartet: {other}"
            Expect.equal (events.[decision].OccurredAt, events.[decision + 1].OccurredAt) (now, now) "ein Append"
            use conn = global.Persistence.getConnection ()
            let pointed = conn.ExecuteScalar<string>("SELECT e.event_id FROM processed_commands p JOIN events e ON e.position = p.first_position WHERE p.command_id = @id", {| id = x.ToString "D" |})
            Expect.equal pointed ((lastDecision month).ToString "D") "first_position zeigt auf die Entscheidung"
    ]

// ============================================
// Befehle und Beobachtung (Review-Funde dgrp7, QA 2 bis 6)
// ============================================

let private commandTests =
    testList "Befehle und Beobachtung" [
        // QA 2: Scheitert das Festhalten von Übernahme und Beobachtung, entscheidet kein Befehl etwas.
        testCase "Lässt sich der Abgleich vor einem Befehl nicht festhalten, lehnen alle Befehle außen ab und nichts entsteht" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            // Die erste Verbindung je Befehl ist die des Abgleichs; sie wirft. Alles danach ginge an die echte Test-DB.
            let failingFirst () =
                let calls = ref 0
                fun () ->
                    calls.Value <- calls.Value + 1
                    if calls.Value = 1 then failwith "Verbindung weg" else global.Persistence.getConnection ()
            let notRecorded result =
                match result with
                | Error (YnabError.LocalReadFailed message) -> message = ObservationNotRecordedText
                | _ -> false
            Expect.isTrue
                (assignMoneyWith (failingFirst ()) f.Port now month { CategoryId = a; Assigned = eur 120000L; CommandId = None }
                 |> run
                 |> notRecorded)
                "Zuweisen"
            Expect.isTrue
                (moveMoneyWith (failingFirst ()) f.Port now month { Source = CategorySide a; Target = CategorySide b; Amount = eur 10000L; CommandId = None }
                 |> run
                 |> notRecorded)
                "Verschieben"
            let shownPreview : TargetPreview =
                { Month = month; Lines = []; Skipped = []; Total = eur 0L; ToBeBudgeted = eur 100000L; ToBeBudgetedAfter = eur 100000L; CommandId = None }
            Expect.isTrue (applyWith (failingFirst ()) f.Port now month shownPreview |> run |> notRecorded) "Anwenden"
            Expect.isTrue
                (setTargetPauseWith (failingFirst ()) Budgeting.Persistence.loadPausedCategories f.Port now month month { CategoryId = a; Intent = PauseTarget; CommandId = None }
                 |> run
                 |> notRecorded)
                "Pause"
            Expect.equal f.Reads 4 "jeder Befehl las YNAB"
            Expect.isEmpty (recordedEvents month) "keine Entscheidung, keine Übernahme"
            Expect.isEmpty f.Attempts "nichts gesendet"

        // QA 3: Die Beobachtungen des Abgleichs stehen vor dem Planen fest und bleiben, wenn der Befehl ablehnt.
        testCase "Eine Änderung in YNAB bleibt beobachtet, auch wenn der folgende Befehl abgelehnt wird" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            read f month |> ignore
            f.Assigned[b] <- 25000L
            match moveMoney f.Port now month { Source = CategorySide a; Target = CategorySide b; Amount = eur 12345L; CommandId = None } |> run with
            | Ok (Error _) -> ()
            | other -> failtest $"Ablehnung erwartet: {other}"
            Expect.equal (observations month) [ b, 20000L, 25000L ] "beobachtet"
            Expect.equal (typesOf month) [ "BudgetTakenOverFromYnab"; "AssignmentChangeObservedInYnab" ] "keine Entscheidung"

        // QA 4: Die Vorschau schreibt nichts fest, plant aber auf dem beobachteten Wert.
        testCase "Die Vorschau wendet die Echo-Regel nur im Speicher an und plant auf dem Wert aus YNAB" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            // BBs Fehlbetrag: 115 − Soll; das Soll folgt der Beobachtung in YNAB (110).
            f.Goals[a] <- monthlyGoal 115000L
            read f month |> ignore
            f.Assigned[a] <- 110000L
            let before = recordedEvents month
            match preview f.Port now month |> run with
            | Ok shown ->
                Expect.equal
                    (shown.Lines |> List.map (fun l -> l.CategoryId, l.OldAssigned, l.NewAssigned))
                    [ a, eur 110000L, eur 115000L ]
                    "vom beobachteten Wert aus"
            | Error e -> failtest $"Vorschau erwartet: {e}"
            Expect.equal (recordedEvents month) before "events unverändert"

        // QA 5: ersetzt den reinen Test, der nur `planRetry` mit einer von Hand gebauten Zeile prüfte.
        testCase "Erneut senden einer gescheiterten Zuweisung, die eine spätere Entscheidung ersetzt hat, sendet nichts und hängt nichts an" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            f.Failures[a] <- YnabError.NetworkError "weg"
            let first = assignOk f month a 120000L
            f.Failures.Clear()
            assignOk f month a 130000L |> ignore
            let before = typesOf month
            f.Attempts.Clear()
            retryOk f (Guid.Parse first.ApplicationId) |> ignore
            Expect.isEmpty f.Attempts "kein PATCH"
            Expect.equal (typesOf month) before "kein Ergebnis-Ereignis"

        // QA 6: Verschieben auf eine Kategorie mit offener Rückschreibung zieht genau diese zurück.
        testCase "Verschieben auf eine Kategorie mit offener Rückschreibung zieht sie im selben Append zurück, die Entscheidung zuerst" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            f.Failures[b] <- YnabError.NetworkError "weg"
            let open' = assignOk f month b 30000L
            f.Failures.Clear()
            let move = moveOk f month a b 5000L
            let events = recordedEvents month
            let decision = events |> List.findIndex (fun e -> e.EventId = Guid.Parse move.ApplicationId)
            Expect.equal
                (events |> List.skip decision |> List.map (fun e -> e.Event) |> List.filter (fun e -> eventTypeName e = "AssignmentWriteBackWithdrawn"))
                [ BudgetEvent.AssignmentWriteBackWithdrawn { DecisionEventId = Guid.Parse open'.ApplicationId; CategoryId = b } ]
                "genau die offene Seite"
            Expect.equal
                (eventTypeName events.[decision + 1].Event, events.[decision + 1].OccurredAt)
                ("AssignmentWriteBackWithdrawn", events.[decision].OccurredAt)
                "direkt hinter der Entscheidung"
    ]

// ============================================
// Historie
// ============================================

let private historyPort = { QueryHandlers.liveHistoryPort with LoadMonthlyActivity = fun _ _ -> async { return [] } }

let private categoryEntries (monthKey: string) (category: Guid) : HistoryLine list =
    let firstDay = DateTime.ParseExact(monthKey, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
    match QueryHandlers.categoryHistory historyPort category firstDay |> run with
    | Ok history -> history.Entries |> List.map historyEntryLine |> List.rev
    | Error message -> failtest message

let private generalRuns (category: Guid) : HistoryRun list =
    match QueryHandlers.budgetHistory historyPort |> run with
    | Ok movements -> movements |> List.filter (fun m -> m.CategoryId = category) |> historyRuns |> List.rev
    | Error message -> failtest message

let private historyTests =
    testList "Historie" [
        testCase "Übernahme und Beobachtung stehen in der Historie je Kategorie und der allgemeinen, eine ersetzte Zeile heißt anders als in YNAB geändert" <| fun () ->
            let f, a, _ = fake ()
            let month = freshMonth ()
            read f month |> ignore
            f.Assigned[a] <- 110000L
            read f month |> ignore
            f.Failures[a] <- YnabError.NetworkError "weg"
            assignOk f month a 120000L |> ignore
            f.Failures.Clear()
            assignOk f month a 130000L |> ignore
            let lines = categoryEntries month a
            Expect.equal
                (lines |> List.map (fun l -> l.Text, l.Change |> Option.map (fun c -> c.Amount), l.Status))
                [ "aus YNAB übernommen, 100,00 €", None, None
                  "in YNAB zugewiesen", Some 10m, None
                  "zugewiesen", Some 10m, Some HistoryReplacedInBb
                  "zugewiesen", Some 10m, Some HistoryWritten ]
                "je Kategorie, älteste zuerst"
            Expect.equal
                (generalRuns a |> List.map (fun r -> r.Text, r.Status))
                [ "Aus YNAB übernommen, 1 Kategorie", RunFromYnab
                  "In YNAB zugewiesen, Lebensmittel", RunFromYnab
                  "Zuweisen, Lebensmittel", RunReplacedInBb
                  "Zuweisen, Lebensmittel", RunWritten ]
                "allgemein, älteste zuerst"

        // budgeting-my8p8: die zurückgezogene Seite hieß "in YNAB geändert", obwohl YNAB sie nie berührt hatte.
        testCase "Eine von der Paarregel zurückgezogene Seite einer Verschiebung heißt in BB zurückgezogen, nicht in BB ersetzt" <| fun () ->
            let f, a, b = fake ()
            let month = freshMonth ()
            f.Failures[a] <- YnabError.NetworkError "weg"
            let report = moveOk f month a b 30000L
            f.Failures.Clear()
            f.Assigned[b] <- 25000L
            retryOk f (Guid.Parse report.ApplicationId) |> ignore
            let source = categoryEntries month a |> List.filter (fun l -> l.Status.IsSome)
            Expect.equal (source |> List.map (fun l -> l.Status)) [ Some HistoryWithdrawnInBb ] "zurückgezogen, nicht ersetzt in BB"
            let target = categoryEntries month b |> List.filter (fun l -> l.Status.IsSome)
            Expect.equal (target |> List.map (fun l -> l.Status)) [ Some HistoryChangedInYnab ] "das Ziel in YNAB geändert"
    ]

[<Tests>]
let tests =
    testList "Budget führen: Abläufe (dgrp7)" [ orderTests; takeoverTests; readTests; commandTests; historyTests ]
