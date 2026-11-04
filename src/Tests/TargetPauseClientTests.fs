module Tests.TargetPauseClientTests

// budgeting-t6p8z: Target pausieren und Pause aufheben auf der Budget-Seite. Die Sichtbarkeit des Knopfs
// als reine Funktion, der Ablauf durch das echte `update`. Die Cmds werden unter .NET nur gebaut, nie
// ausgeführt: der Proxy liegt hinter einem Lambda.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Moves
open Components.Budget.Pause

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private current = "2026-11-01"
let private next = "2026-12-01"

let private paused = Guid.Parse "00000000-0000-0000-0000-00000006a801"
let private other = Guid.Parse "00000000-0000-0000-0000-00000006a802"

let private target : Target = { Goal = eur 50m; Rhythm = Months 1 }

/// Ausflug: Target 50 €, nichts zugewiesen, Fehlbetrag 50 €. Urlaub ohne Target.
let private trip : BudgetCategory =
    { Id = paused
      Name = "Ausflug"
      Assigned = eur 0m
      Activity = eur 0m
      Available = eur 0m
      Underfunded = Some (eur 50m)
      Target = Some target
      Pause = None }

let private holiday : BudgetCategory =
    { trip with Id = other; Name = "Urlaub"; Underfunded = None; Target = None; Available = eur 10m }

let private monthWith (month: string) (pause: PauseSource option) : BudgetMonth =
    { Month = month
      ToBeBudgeted = eur 500m
      Groups = [ { Name = "Alltag"; Categories = [ { trip with Pause = pause }; holiday ] } ] }

let private frame : BudgetFrame =
    { CurrentMonth = current; Months = [ "2026-10-01"; current; next ]; CategoryOrder = [ paused; other ] }

let private page : Model =
    { Components.Budget.Types.init () with Frame = Success frame; Month = Success (monthWith current None) }

let private update (msg: Msg) (model: Model) =
    let model', cmd, _ = Components.Budget.State.update msg model
    model', cmd

let private step (msg: Msg) (model: Model) : Model = fst (update msg model)

let private categoryOf (model: Model) (id: Guid) : BudgetCategory =
    match model.Month with
    | Success month -> categoriesOf month |> List.find (fun c -> c.Id = id)
    | _ -> failtest "Monat erwartet"

let private onTrip = page |> step (SelectCategory paused)

// ============================================
// Der Knopf als reine Funktion
// ============================================

let private controlTests =
    let withPause pause = { trip with Pause = pause }
    testList "Pause-Knopf: Sichtbarkeit und Text" [
        test "Ohne Target gibt es keinen Knopf, auch keinen Platz" {
            for kind in [ PastMonth; CurrentMonth; FutureMonth ] do
                Expect.equal (pauseControl kind holiday) NoPauseControl $"{kind}"
        }
        test "Im vergangenen Monat fehlt der Knopf, sein Platz bleibt frei" {
            for pause in [ None; Some PausedInBb; Some PausedInYnab; Some PausedInBoth ] do
                Expect.equal (pauseControl PastMonth (withPause pause)) PauseSlotFree $"{pause}"
        }
        test "Laufender und künftiger Monat zeigen je Quelle denselben Knopf und Satz" {
            let pausing = Some { Label = "Target pausieren"; Paused = true }
            let lifting = Some { Label = "Pause aufheben"; Paused = false }
            let cases =
                [ None, PauseOffered (pausing, None)
                  Some PausedInBb, PauseOffered (lifting, None)
                  Some PausedInYnab, PauseOffered (None, Some ynabPauseNote)
                  Some PausedInBoth, PauseOffered (lifting, Some ynabPauseNote) ]
            for kind in [ CurrentMonth; FutureMonth ] do
                for pause, expected in cases do
                    Expect.equal (pauseControl kind (withPause pause)) expected $"{kind} {pause}"
        }
        test "Ein erfülltes Target lässt sich auch pausieren" {
            match pauseControl CurrentMonth { trip with Underfunded = Some (eur 0m) } with
            | PauseOffered (Some button, None) -> Expect.isTrue button.Paused "pausieren"
            | other -> failtest $"Knopf erwartet: {other}"
        }
    ]

// ============================================
// Ablauf durch das echte update
// ============================================

let private flowTests =
    testList "Pause: Ablauf im Client" [
        test "GIVEN Fehlbetrag WHEN Pause bestätigt THEN Fehlt leer, Filterzahl sinkt, Fehlbetrag-Knopf weg, Monat nicht neu gelesen" {
            let groups (model: Model) = match model.Month with Success m -> m.Groups | _ -> []
            Expect.equal (filterCount MissingOnly (groups onTrip)) 1 "vorher fehlt Ausflug"
            let running, cmd = update (SetTargetPause true) onTrip
            Expect.isNonEmpty cmd "der Aufruf geht hinaus"
            Expect.equal running.PauseCall (PauseRunning (paused, current)) "läuft"
            let answered, _ = update (TargetPauseAnswered (paused, current, Ok (Some PausedInBb))) running
            let shown = categoryOf answered paused
            Expect.equal shown.Pause (Some PausedInBb) "Pause gesetzt"
            Expect.equal (missingAmount shown) None "Fehlt leer"
            Expect.equal (filterCount MissingOnly (groups answered)) 0 "Filterzahl sinkt"
            let month = match answered.Month with Success m -> m | _ -> failtest "Monat"
            Expect.equal (missingAction month shown) NothingMissing "Fehlbetrag-Knopf weg"
            Expect.equal answered.MonthGeneration running.MonthGeneration "kein Neu-Lesen"
            Expect.equal answered.PauseCall PauseIdle "fertig"
        }
        test "Nach der Pause ist die Historie wieder fällig" {
            let historyShown =
                let asked = onTrip |> step (SelectInspectorTab HistoryTab)
                asked |> step (CategoryHistoryLoaded (asked.HistoryGeneration, Ok { Entries = []; Spending = [] }))
            let inMoney = historyShown |> step (SelectInspectorTab MoneyTab)
            let answered = inMoney |> step (SetTargetPause true) |> step (TargetPauseAnswered (paused, current, Ok (Some PausedInBb)))
            Expect.equal (answered.Selected |> Option.map (fun s -> s.History)) (Some NotAsked) "wieder fällig"
        }
        test "GIVEN Fehler vom Server THEN Meldung im Tab, Pause unverändert" {
            let answered =
                onTrip |> step (SetTargetPause true) |> step (TargetPauseAnswered (paused, current, Error "Ausflug hat kein Target."))
            Expect.equal (categoryOf answered paused).Pause None "unverändert"
            Expect.equal (pauseError answered paused) (Some "Ausflug hat kein Target.") "Meldung"
            Expect.equal (answered |> step (SelectCategory other) |> step (SelectCategory paused) |> fun m -> pauseError m paused) None "gilt nur, solange gezeigt"
        }
        test "Ein Neu-Lesen, das vor der Pause begann, wird nach der Antwort wiederholt" {
            let stale = { onTrip with Freshness = MonthStale }
            let running = stale |> step (SetTargetPause true)
            let answered, cmd = update (TargetPauseAnswered (paused, current, Ok (Some PausedInBb))) running
            Expect.isGreaterThan answered.MonthGeneration running.MonthGeneration "die alte Antwort zählt nicht mehr"
            Expect.isNonEmpty cmd "der Monat wird neu gelesen"
        }
        test "Aufheben einer BB-Pause schickt Paused = false" {
            let pausedTrip = { onTrip with Month = Success (monthWith current (Some PausedInBoth)) }
            match startPause false pausedTrip with
            | _, Some (month, request) -> Expect.equal (month, request) (current, { CategoryId = paused; Paused = false }) "Auftrag"
            | _, None -> failtest "Auftrag erwartet"
            Expect.isNone (snd (startPause true pausedTrip)) "Pausieren wird nicht angeboten"
        }
    ]

// ============================================
// Reihenfolgen (Doktrin Punkt 20)
// ============================================

/// Was die Aufzählung über den Zustand weiß, unabhängig vom getesteten Code.
type private Walk = {
    /// Der Monat, der gezeigt und geladen ist.
    Month: string
    /// Die gezeigte Kategorie.
    Shown: Guid
    /// Der laufende Aufruf: sein Monat.
    Pending: string option
    /// Die Monate, in denen der Server Ausflug pausiert hat.
    PausedOnServer: Set<string>
}

/// Alle Ereignisse, die im Zustand kommen können: Name, Nachrichten, Zustand danach.
let private events (walk: Walk) : (string * Msg list * Walk) list =
    let otherMonth = if walk.Month = current then next else current
    let otherCategory = if walk.Shown = paused then other else paused
    [ "Tipp", [ SetTargetPause true ], walk
      "Kategorie", [ SelectCategory otherCategory ], { walk with Shown = otherCategory }
      "Monat", [ SelectMonth otherMonth ], { walk with Month = otherMonth }
      match walk.Pending with
      | Some month ->
          "Antwort", [ TargetPauseAnswered (paused, month, Ok (Some PausedInBb)) ],
          { walk with Pending = None; PausedOnServer = walk.PausedOnServer.Add month }
      | None -> () ]

let private walkTests =
    let start = { Month = current; Shown = paused; Pending = None; PausedOnServer = Set.empty }
    let rec walkFrom (depth: int) (path: string list) (walk: Walk) (model: Model) : unit =
        if depth > 0 then
            for name, msgs, after in events walk do
                let route = List.rev (name :: path) |> String.concat ", "
                // Der Server kennt die Pause erst nach der Antwort: ein Monatswechsel lädt den Monat mit
                // dem Stand vor dem Schritt, nur die Antwort selbst ändert ihn.
                let mutable sent = false
                let mutable model' = model
                for msg in msgs do
                    match msg with
                    | SelectMonth month ->
                        let moved = step msg model'
                        let pause = if walk.PausedOnServer.Contains month then Some PausedInBb else None
                        model' <- step (Loaded (moved.MonthGeneration, Ok (monthWith month pause))) moved
                    | _ ->
                        let stepped, cmd = update msg model'
                        if msg = SetTargetPause true && not (List.isEmpty cmd) then sent <- true
                        model' <- stepped
                let after =
                    if name = "Tipp" then
                        // Gesendet wird nur, wenn nichts läuft, Ausflug gezeigt wird und dort nicht schon pausiert ist.
                        let expected = walk.Pending.IsNone && walk.Shown = paused && not (walk.PausedOnServer.Contains walk.Month)
                        Expect.equal sent expected $"{route}: gesendet"
                        if expected then { after with Pending = Some walk.Month } else after
                    else after
                let expectedPause = if after.PausedOnServer.Contains after.Month then Some PausedInBb else None
                Expect.equal (categoryOf model' paused).Pause expectedPause $"{route}: Pause im Monat {after.Month}"
                Expect.equal (categoryOf model' other).Pause None $"{route}: Urlaub unberührt"
                walkFrom (depth - 1) (name :: path) after model'
    testList "Pause: Reihenfolgen" [
        test "In jeder Reihenfolge wirkt die Antwort nur auf ihre Kategorie und ihren Monat, ein zweiter Tipp sendet nicht" {
            walkFrom 5 [] start onTrip
        }
    ]

[<Tests>]
let tests =
    testList "Target pausieren im Client (budgeting-t6p8z)" [ controlTests; flowTests; walkTests ]
