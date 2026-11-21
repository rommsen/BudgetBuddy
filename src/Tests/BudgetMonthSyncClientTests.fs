module Tests.BudgetMonthSyncClientTests

// budgeting-ffjk9, ADR 0101 Punkt 6: das stille Nachziehen der Budget-Seite. Nach dem Lesen fragt die Seite
// `syncBudgetMonth` und tauscht die Werte still. Die Aufrufe hinter `update` liegen in Lambdas und laufen unter .NET nie.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private food : BudgetCategory =
    { Id = Guid.Parse "00000000-0000-0000-0000-0000000ff001"
      Name = "Lebensmittel"
      Assigned = eur 900m
      Activity = eur 0m
      Available = eur 80m
      Underfunded = None
      Target = None
      Pause = None; OpenWriteBack = None; TargetInYnab = None; Deactivated = None; ActivityInYnab = None }

let private november = "2026-11-01"
let private december = "2026-12-01"

/// Der Zeitpunkt des Stands: 13:05 UTC ist 14:05 in Berlin (die Tests laufen mit `TZ=Europe/Berlin`).
let private copiedAt = DateTime(2026, 11, 21, 13, 5, 0, DateTimeKind.Utc)

/// Der Monat, wie das sofortige Lesen ihn bringt (Zugewiesen 900), und wie das Nachziehen ihn bringt (950).
let private readMonth (key: string) : BudgetMonth =
    { Month = key; ToBeBudgeted = eur 100m; CopiedAt = Some copiedAt; Groups = [ { Name = "Alltag"; Categories = [ food ] } ] }

let private syncedMonth (key: string) : BudgetMonth =
    { readMonth key with
        ToBeBudgeted = eur 50m
        CopiedAt = Some (copiedAt.AddMinutes 1.)
        Groups = [ { Name = "Alltag"; Categories = [ { food with Assigned = eur 950m; Available = eur 130m } ] } ] }

let private frame : BudgetFrame =
    { CurrentMonth = november
      Months = [ "2026-10-01"; november; december ]
      CategoryOrder = []
      MonthStartHidden = [] }

let private update msg model =
    let model', cmd, _ = Components.Budget.State.update msg model
    model', cmd

let private run (msgs: Msg list) (model: Model) : Model =
    msgs |> List.fold (fun current msg -> fst (update msg current)) model

/// Die Seite nach Rahmen und gelesenem November, Lebensmittel im Tab Geld gewählt.
let private shown : Model =
    let framed = run [ FrameLoaded (Ok frame) ] (init ())
    run [ Loaded (framed.MonthGeneration, Ok (readMonth november)); SelectCategory food.Id ] framed

let private assignedOf (model: Model) : decimal option =
    match model.Month with
    | Success month -> month.Groups |> List.collect (fun g -> g.Categories) |> List.tryFind (fun c -> c.Id = food.Id) |> Option.map (fun c -> c.Assigned.Amount)
    | _ -> None

let private entryOf (model: Model) = model.TabMoney |> Option.bind (fun tab -> tab.Entry)


/// Ein Target, wie es die Antwort „inzwischen geändert“ bringt.
let private changedTarget : Target =
    { Goal = eur 120m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }

let private startTests =
    testList "Start des Nachziehens" [
        testCase "Nach dem Lesen startet das Nachziehen, nach einem gescheiterten Lesen nicht" <| fun () ->
            let framed = run [ FrameLoaded (Ok frame) ] (init ())
            let _, cmd = update (Loaded (framed.MonthGeneration, Ok (readMonth november))) framed
            Expect.equal (List.length cmd) 1 "ein Aufruf syncBudgetMonth"
            let _, none = update (Loaded (framed.MonthGeneration, Error "weg")) framed
            Expect.isEmpty none "kein Aufruf"
            let _, stale = update (Loaded (framed.MonthGeneration - 1, Ok (readMonth november))) framed
            Expect.isEmpty stale "eine überholte Antwort startet nichts"

        // Das stille Neu-Lesen (Rückkehr, nach Zuweisen, Verschieben, Pause) antwortet über `Reloaded` und zieht
        // ebenso nach.
        testCase "Nach dem stillen Neu-Lesen startet das Nachziehen, nach einem gescheiterten nicht" <| fun () ->
            let framed = run [ FrameLoaded (Ok frame) ] (init ())
            let read = run [ Loaded (framed.MonthGeneration, Ok (readMonth november)) ] framed
            let _, cmd = update (Reloaded (read.MonthGeneration, Ok (readMonth november))) read
            Expect.equal (List.length cmd) 1 "ein Aufruf syncBudgetMonth"
            let _, none = update (Reloaded (read.MonthGeneration, Error "weg")) read
            Expect.isEmpty none "kein Aufruf"
            let _, stale = update (Reloaded (read.MonthGeneration - 1, Ok (readMonth november))) read
            Expect.isEmpty stale "eine überholte Antwort startet nichts"
    ]

let private hintTests =
    testList "Hinweis „YNAB-Stand von <Uhrzeit>“" [
        testCase "Der Text nennt die Uhrzeit des Stands in Berliner Zeit" <| fun () ->
            Expect.equal (ynabCopyText copiedAt) "YNAB-Stand von 14:05" "Winterzeit"
            Expect.equal (ynabCopyText (DateTime(2026, 7, 1, 6, 30, 0, DateTimeKind.Utc))) "YNAB-Stand von 08:30" "Sommerzeit"

        testCase "Ein Fehlschlag des Nachziehens zeigt den Hinweis mit Grund, ein Erfolg zeigt nichts" <| fun () ->
            let failed = run [ MonthSynced (shown.MonthGeneration, november, Error "YNAB antwortet nicht (503)") ] shown
            Expect.equal
                (ynabSyncHint failed)
                (Some { Text = "YNAB-Stand von 14:05"; Reasons = [ "YNAB antwortet nicht (503)" ]; Running = false })
                "Hinweis"
            Expect.equal failed.Month shown.Month "der Stand bleibt stehen"
            for result in [ Ok MonthUnchanged; Ok (MonthChanged (syncedMonth november)) ] do
                Expect.isNone (ynabSyncHint (run [ MonthSynced (shown.MonthGeneration, november, result) ] shown)) $"{result}"
                Expect.isNone (ynabSyncHint (run [ MonthSynced (shown.MonthGeneration, november, result) ] failed)) $"nach dem Fehlschlag: {result}"

        // Der Hinweis ersetzt die Meldung: ein Toast daneben wäre doppelt (ADR 0101 Punkt 6, F4).
        testCase "Ein Fehlschlag des Nachziehens meldet sich ohne Toast" <| fun () ->
            let _, _, external = Components.Budget.State.update (MonthSynced (shown.MonthGeneration, november, Error "weg")) shown
            match external with
            | NoOp -> ()
            | other -> failtest $"keine Meldung nach außen erwartet, kam {other}"

        testCase "Ohne Zeitpunkt des Stands zeigt auch ein Fehlschlag keinen Hinweis" <| fun () ->
            let framed = run [ FrameLoaded (Ok frame) ] (init ())
            let direct = run [ Loaded (framed.MonthGeneration, Ok { readMonth november with CopiedAt = None }) ] framed
            Expect.isNone (ynabSyncHint (run [ MonthSynced (direct.MonthGeneration, november, Error "weg") ] direct)) "kein Hinweis"

        testCase "Neu laden am Hinweis zieht noch einmal nach, ein zweiter Tipp nicht" <| fun () ->
            let failed = run [ MonthSynced (shown.MonthGeneration, november, Error "weg") ] shown
            let retrying, cmd = update RetryYnabSync failed
            Expect.equal (List.length cmd) 1 "ein Aufruf"
            Expect.isTrue (ynabSyncHint retrying).Value.Running "läuft"
            let _, again = update RetryYnabSync retrying
            Expect.isEmpty again "kein zweiter Aufruf"
            Expect.isNone (ynabSyncHint (run [ MonthSynced (retrying.MonthGeneration, november, Ok MonthUnchanged) ] retrying)) "gelungen"

        // Bug: eine Antwort „geändert“ mit dem richtigen Schlüssel, aber einem anderen Monat im Inhalt ließ „Neu laden“
        // als laufend stehen; der Hinweis drehte dauerhaft.
        testCase "Eine Antwort mit fremdem Monat im Inhalt tauscht nichts und beendet ein laufendes Neu laden" <| fun () ->
            let failed = run [ MonthSynced (shown.MonthGeneration, november, Error "weg") ] shown
            let retrying = run [ RetryYnabSync ] failed
            let answered = run [ MonthSynced (retrying.MonthGeneration, november, Ok (MonthChanged (syncedMonth december))) ] retrying
            Expect.equal answered.Month retrying.Month "der Monat bleibt"
            Expect.isNone (ynabSyncHint answered) "kein Hinweis, der läuft"
    ]

/// Was die Aufzählung selbst weiß. Der Merker ist eigen, er glaubt nicht dem Code, den der Test prüft.
type private WalkState = {
    /// Der angesteuerte Monat: aus „November“ und „Dezember“, nach „Seite da“ der laufende Monat des Rahmens.
    Target: string
    /// Das Nachziehen, das nach dem letzten gelesenen Monat startete: Generation und Monat beim Start.
    Sync: (int * string) option
    /// Gilt es noch? Ein neues Lesen (Monatswechsel, Rückkehr auf die Seite) überholt es.
    Valid: bool
    /// Hat die Seite seit seinem Start selbst eine Pause oder ein Target in den dargestellten Monat gesetzt? Dann kennt
    /// eine Antwort „geändert“ es vielleicht nicht.
    Local: bool
}

/// Der Monat, den die Seite darstellt, wie die Aufzählung ihn sieht.
let private displayed (model: Model) : string option =
    match model.Month with
    | Success month -> Some month.Month
    | _ -> None

/// Die laufende Zuweisung der Schlange, wenn eine läuft.
let private runningAssign (model: Model) : AssignWrite option =
    model.AssignQueue |> List.tryFind (fun w -> w.State = WriteRunning)

let private events (model: Model) (state: WalkState) : (string * Msg * WalkState) list =
    let toMonth month = { state with Target = month; Valid = state.Valid && state.Target = month }
    let read = { Target = state.Target; Sync = Some (model.MonthGeneration, state.Target); Valid = true; Local = false }
    [ "November", SelectMonth november, toMonth november
      "Dezember", SelectMonth december, toMonth december
      "Seite weg", PageLeft, state
      // Ohne Absage und Kassenbuch beginnt die Seite beim laufenden Monat.
      "Seite da", PageEntered, { state with Target = frame.CurrentMonth; Valid = false }
      "Tippen", AssignTextChanged "+5", state
      "Bestätigen", AssignFieldLeft, state
      match runningAssign model with
      | Some write ->
          let line =
              { CategoryId = write.CategoryId; CategoryName = food.Name; OldAssigned = eur 900m; NewAssigned = write.Assigned; Outcome = Written }
          "Zuweisung bestätigt", AssignAnswered (Ok { ApplicationId = "a"; Month = write.Month; Lines = [ line ]; ActualToBeBudgeted = None }), state
      | None -> ()
      "Gelesen", Loaded (model.MonthGeneration, Ok (readMonth state.Target)), read
      "Still gelesen", Reloaded (model.MonthGeneration, Ok (readMonth state.Target)), read
      match displayed model with
      | Some month ->
          "Pause gespeichert", TargetPauseAnswered (food.Id, month, Ok (Some PausedInBb)), { state with Local = true }
          "Target inzwischen geändert", TargetChangeAnswered (food.Id, month, Ok (TargetChangedMeanwhile (Some changedTarget))), { state with Local = true }
      | None -> ()
      match state.Sync with
      | Some (generation, month) ->
          let other = if month = november then december else november
          "Sync geändert", MonthSynced (generation, month, Ok (MonthChanged (syncedMonth month))), state
          "Sync unverändert", MonthSynced (generation, month, Ok MonthUnchanged), state
          "Sync gescheitert", MonthSynced (generation, month, Error "weg"), state
          "Sync anderer Monat", MonthSynced (generation, other, Ok (MonthChanged (syncedMonth other))), state
          "Sync fremder Inhalt", MonthSynced (generation, month, Ok (MonthChanged (syncedMonth other))), state
      | None -> () ]

/// Welcher Zweig des Orakels eine gültige Antwort „geändert“ geprüft hat.
type private Swap =
    | NoSwap
    | Swapped
    | SwappedOverQueue
    | RereadInstead

let private check (path: string list) (msg: Msg) (before: Model) (after: Model) (cmd: Elmish.Cmd<Msg>) (state: WalkState) : Swap =
    let shownPath = String.concat ", " (List.rev path)
    match msg with
    | MonthSynced (generation, month, result) ->
        let isLoading (m: Model) = m.Month = Loading
        Expect.equal (isLoading after) (isLoading before) $"Month wird nie Loading: {shownPath}"
        Expect.equal (entryOf after) (entryOf before) $"die Eingabe im Feld Zugewiesen bleibt: {shownPath}"
        if displayed before <> Some month || state.Target <> month then
            Expect.equal after before $"eine Antwort für einen anderen als den gezeigten Monat wird verworfen: {shownPath}"
            NoSwap
        elif not state.Valid || generation <> before.MonthGeneration then
            // Überholt: ein neues Lesen läuft oder kam (Monatswechsel, Rückkehr, Neu-Lesen nach einer Zuweisung).
            Expect.equal after before $"eine überholte Antwort wird verworfen: {shownPath}"
            NoSwap
        else
            match result with
            | Ok (MonthChanged synced) when synced.Month = month && state.Local ->
                // Bug: die Antwort konnte vor dem Speichern von Pause oder Target gebaut sein und nahm sie zurück.
                Expect.equal after.Month before.Month $"geändert nimmt eine Pause oder ein Target der Seite nicht zurück: {shownPath}"
                Expect.equal (List.length cmd) 1 $"statt dessen liest die Seite still neu: {shownPath}"
                Expect.isNone (ynabSyncHint after) $"kein Hinweis nach Erfolg: {shownPath}"
                RereadInstead
            | Ok (MonthChanged synced) when synced.Month = month ->
                // Eine gespeicherte, noch nicht bestätigte Zuweisung gilt, bis sie bestätigt ist (ADR 0078 Punkt 5).
                let queued =
                    before.AssignQueue
                    |> List.filter (fun w -> w.Month = month && w.CategoryId = food.Id)
                    |> List.tryLast
                    |> Option.map (fun w -> w.Assigned.Amount)
                let expected = queued |> Option.defaultValue 950m
                Expect.equal (assignedOf after) (Some expected) $"geändert tauscht die Beträge: {shownPath}"
                Expect.equal (after.Month |> function Success m -> Some m.ToBeBudgeted | _ -> None) (Some { synced.ToBeBudgeted with Amount = synced.ToBeBudgeted.Amount - (expected - 950m) }) $"Zu verteilen getauscht: {shownPath}"
                Expect.isNone (ynabSyncHint after) $"kein Hinweis nach Erfolg: {shownPath}"
                Expect.isEmpty cmd $"kein weiterer Aufruf: {shownPath}"
                if queued.IsSome then SwappedOverQueue else Swapped
            | Ok (MonthChanged _) ->
                Expect.equal after.Month before.Month $"ein fremder Monat im Inhalt tauscht nichts: {shownPath}"
                Expect.isNone (ynabSyncHint after) $"kein Hinweis nach Erfolg: {shownPath}"
                NoSwap
            | Ok MonthUnchanged ->
                Expect.equal after.Month before.Month $"unverändert lässt den Monat: {shownPath}"
                Expect.isNone (ynabSyncHint after) $"kein Hinweis nach Erfolg: {shownPath}"
                NoSwap
            | Error _ ->
                Expect.equal after.Month before.Month $"ein Fehlschlag lässt den Monat: {shownPath}"
                Expect.isSome (ynabSyncHint after) $"Hinweis nach Fehlschlag: {shownPath}"
                NoSwap
    | _ -> NoSwap

/// Geht alle Folgen bis `depth` ab. Ein Zustand, der schon mit gleicher oder größerer Resttiefe besucht wurde, bringt
/// nichts Neues. Gibt zurück, wie oft jeder Zweig des Orakels für eine gültige Antwort „geändert“ lief.
let private walk (depth: int) : Map<Swap, int> =
    let visited = Collections.Generic.Dictionary<Model * WalkState, int> HashIdentity.Structural
    let mutable counts = Map.empty
    let rec go (left: int) (path: string list) (model: Model) (state: WalkState) =
        for name, msg, state' in events model state do
            let after, cmd = update msg model
            let path' = name :: path
            let swap = check path' msg model after cmd state
            counts <- counts |> Map.change swap (fun n -> Some ((defaultArg n 0) + 1))
            let seen, deepest = visited.TryGetValue((after, state'))
            if left > 1 && not (seen && deepest >= left - 1) then
                visited[(after, state')] <- left - 1
                go (left - 1) path' after state'
    go depth [] shown { Target = november; Sync = Some (shown.MonthGeneration, november); Valid = true; Local = false }
    counts

let private orderTests =
    testList "Reihenfolgen von Lesen, Nachziehen, Zuweisen, Pause, Target, Monatswechsel und Verlassen" [
        testCase "Alle Folgen bis Tiefe 6: verworfen wird nur, was nicht zum gezeigten Monat gehört, getauscht wird still" <| fun () ->
            let counts = walk 6
            let count swap = counts |> Map.tryFind swap |> Option.defaultValue 0
            // Ohne genug Treffer je Zweig prüfte die Aufzählung nur das Verwerfen.
            Expect.isGreaterThan (count Swapped) 50 "gültige Antworten tauschen den Monat"
            Expect.isGreaterThan (count SwappedOverQueue) 0 "eine Zuweisung in der Schlange gilt über die Antwort"
            Expect.isGreaterThan (count RereadInstead) 0 "nach Pause oder Target liest die Seite neu"
    ]

[<Tests>]
let tests = testList "Nachziehen der Budget-Seite (Client)" [ startTests; hintTests; orderTests ]
