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

let private hintTests =
    testList "Hinweis „YNAB-Stand von <Uhrzeit>“" [
        testCase "Der Text nennt die Uhrzeit des Stands in Berliner Zeit" <| fun () ->
            Expect.equal (ynabCopyText copiedAt) "YNAB-Stand von 14:05" "Winterzeit"
            Expect.equal (ynabCopyText (DateTime(2026, 7, 1, 6, 30, 0, DateTimeKind.Utc))) "YNAB-Stand von 08:30" "Sommerzeit"

        testCase "Nach dem Lesen startet das Nachziehen, nach einem gescheiterten Lesen nicht" <| fun () ->
            let framed = run [ FrameLoaded (Ok frame) ] (init ())
            let _, cmd = update (Loaded (framed.MonthGeneration, Ok (readMonth november))) framed
            Expect.equal (List.length cmd) 1 "ein Aufruf syncBudgetMonth"
            let _, none = update (Loaded (framed.MonthGeneration, Error "weg")) framed
            Expect.isEmpty none "kein Aufruf"
            let _, stale = update (Loaded (framed.MonthGeneration - 1, Ok (readMonth november))) framed
            Expect.isEmpty stale "eine überholte Antwort startet nichts"

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
    ]

/// Was die Aufzählung selbst weiß: welcher Monat zuletzt gelesen ankam und ob das Nachziehen dazu noch gilt. Der
/// Merker ist eigen, er glaubt nicht dem Code, den der Test prüft.
type private WalkState = {
    /// Das Nachziehen, das nach dem letzten gelesenen Monat startete: Generation und Monat beim Start.
    Sync: (int * string) option
    /// Gilt es noch? Ein neues Lesen (Monatswechsel, Rückkehr auf die Seite) überholt es.
    Valid: bool
}

/// Der Monat, den die Seite darstellt, wie die Aufzählung ihn sieht.
let private displayed (model: Model) : string option =
    match model.Month with
    | Success month -> Some month.Month
    | _ -> None

let private events (model: Model) (state: WalkState) : (string * Msg * WalkState) list =
    let target = Components.Budget.Month.shownMonth model
    let overtaken = { state with Valid = false }
    [ "November", SelectMonth november, (if target = Some november then state else overtaken)
      "Dezember", SelectMonth december, (if target = Some december then state else overtaken)
      "Seite weg", PageLeft, state
      "Seite da", PageEntered, overtaken
      "Tippen", AssignTextChanged "+5", state
      match target with
      | Some month ->
          "Gelesen", Loaded (model.MonthGeneration, Ok (readMonth month)), { Sync = Some (model.MonthGeneration, month); Valid = true }
      | None -> ()
      match state.Sync with
      | Some (generation, month) ->
          "Sync geändert", MonthSynced (generation, month, Ok (MonthChanged (syncedMonth month))), state
          "Sync unverändert", MonthSynced (generation, month, Ok MonthUnchanged), state
          "Sync gescheitert", MonthSynced (generation, month, Error "weg"), state
          let other = if month = november then december else november
          "Sync anderer Monat", MonthSynced (generation, other, Ok (MonthChanged (syncedMonth other))), state
      | None -> () ]

let private check (path: string list) (name: string) (msg: Msg) (before: Model) (after: Model) (state: WalkState) : unit =
    let shownPath = String.concat ", " (List.rev path)
    match msg with
    | MonthSynced (generation, month, result) ->
        let isLoading (m: Model) = m.Month = Loading
        Expect.equal (isLoading after) (isLoading before) $"Month wird nie Loading: {shownPath}"
        Expect.equal (entryOf after) (entryOf before) $"die Eingabe im Feld Zugewiesen bleibt: {shownPath}"
        if displayed before <> Some month || Components.Budget.Month.shownMonth before <> Some month then
            Expect.equal after before $"eine Antwort für einen anderen als den gezeigten Monat wird verworfen: {shownPath}"
        elif not state.Valid || generation <> before.MonthGeneration then
            // Überholt: ein neues Lesen läuft oder kam (Monatswechsel, Rückkehr, Neu-Lesen nach einer Zuweisung).
            Expect.equal after before $"eine überholte Antwort wird verworfen: {shownPath}"
        else
            match result with
            | Ok (MonthChanged synced) ->
                // Eine gespeicherte, noch nicht bestätigte Zuweisung gilt, bis sie bestätigt ist (ADR 0078 Punkt 5).
                let expected =
                    before.AssignQueue
                    |> List.filter (fun w -> w.Month = month && w.CategoryId = food.Id)
                    |> List.tryLast
                    |> Option.map (fun w -> w.Assigned.Amount)
                    |> Option.defaultValue 950m
                Expect.equal (assignedOf after) (Some expected) $"geändert tauscht die Beträge: {shownPath}"
                Expect.equal (after.Month |> function Success m -> Some m.ToBeBudgeted | _ -> None) (Some { synced.ToBeBudgeted with Amount = synced.ToBeBudgeted.Amount - (expected - 950m) }) $"Zu verteilen getauscht: {shownPath}"
                Expect.isNone (ynabSyncHint after) $"kein Hinweis nach Erfolg: {shownPath}"
            | Ok MonthUnchanged ->
                Expect.equal after.Month before.Month $"unverändert lässt den Monat: {shownPath}"
                Expect.isNone (ynabSyncHint after) $"kein Hinweis nach Erfolg: {shownPath}"
            | Error _ ->
                Expect.equal after.Month before.Month $"ein Fehlschlag lässt den Monat: {shownPath}"
                Expect.isSome (ynabSyncHint after) $"Hinweis nach Fehlschlag: {shownPath}"
    | _ -> ()
    ignore name

/// Geht alle Folgen bis `depth` ab. Ein Zustand, der schon mit gleicher oder größerer Resttiefe besucht wurde, bringt
/// nichts Neues. Gibt zurück, wie viele gültige Antworten „geändert“ einen Monat getauscht haben.
let private walk (depth: int) : int =
    let visited = Collections.Generic.Dictionary<Model * WalkState, int> HashIdentity.Structural
    let mutable swaps = 0
    let rec go (left: int) (path: string list) (model: Model) (state: WalkState) =
        for name, msg, state' in events model state do
            let after, _ = update msg model
            let path' = name :: path
            check path' name msg model after state
            if name = "Sync geändert" && after.Month <> model.Month then swaps <- swaps + 1
            let seen, deepest = visited.TryGetValue((after, state'))
            if left > 1 && not (seen && deepest >= left - 1) then
                visited[(after, state')] <- left - 1
                go (left - 1) path' after state'
    go depth [] shown { Sync = Some (shown.MonthGeneration, november); Valid = true }
    swaps

let private orderTests =
    testList "Reihenfolgen von Lesen, Nachziehen, Monatswechsel und Verlassen" [
        testCase "Alle Folgen bis Tiefe 6: verworfen wird nur, was nicht zum gezeigten Monat gehört, getauscht wird still" <| fun () ->
            let swaps = walk 6
            // Ohne genug getauschte Monate prüfte die Aufzählung nur das Verwerfen.
            Expect.isGreaterThan swaps 50 "gültige Antworten tauschen den Monat"
    ]

[<Tests>]
let tests = testList "Nachziehen der Budget-Seite (Client)" [ hintTests; orderTests ]
