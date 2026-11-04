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

let private target : Target = { Goal = eur 50m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None }

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
            let pausing = Some { Label = "Target pausieren"; Intent = PauseTarget }
            let lifting = Some { Label = "Pause aufheben"; Intent = LiftPause }
            // budgeting-r3wq8: bei Pause auf beiden Seiten hebt der Knopf nur die BB-Pause auf und sagt es.
            let liftingBb = Some { Label = "Pause in BB aufheben"; Intent = LiftPause }
            let cases =
                [ None, PauseOffered (pausing, None)
                  Some PausedInBb, PauseOffered (lifting, None)
                  Some PausedInYnab, PauseOffered (None, Some ynabPauseNote)
                  Some PausedInBoth,
                  PauseOffered (liftingBb, Some "In YNAB ist das Target ebenfalls pausiert, diese Pause lässt sich nur dort aufheben.") ]
            for kind in [ CurrentMonth; FutureMonth ] do
                for pause, expected in cases do
                    Expect.equal (pauseControl kind (withPause pause)) expected $"{kind} {pause}"
        }
        test "Ein erfülltes Target lässt sich auch pausieren" {
            match pauseControl CurrentMonth { trip with Underfunded = Some (eur 0m) } with
            | PauseOffered (Some button, None) -> Expect.equal button.Intent PauseTarget "pausieren"
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
            let running, cmd = update (SetTargetPause PauseTarget) onTrip
            Expect.isNonEmpty cmd "der Aufruf geht hinaus"
            Expect.equal running.PauseCall (PauseRunning (paused, current, onTrip.MonthGeneration)) "läuft"
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
            let answered = inMoney |> step (SetTargetPause PauseTarget) |> step (TargetPauseAnswered (paused, current, Ok (Some PausedInBb)))
            Expect.equal (answered.Selected |> Option.map (fun s -> s.History)) (Some NotAsked) "wieder fällig"
        }
        test "GIVEN Fehler vom Server THEN Meldung im Tab, Pause unverändert" {
            let answered =
                onTrip |> step (SetTargetPause PauseTarget) |> step (TargetPauseAnswered (paused, current, Error "Ausflug hat kein Target."))
            Expect.equal (categoryOf answered paused).Pause None "unverändert"
            Expect.equal (pauseError answered paused) (Some "Ausflug hat kein Target.") "Meldung"
            Expect.equal (answered |> step (SelectCategory other) |> step (SelectCategory paused) |> fun m -> pauseError m paused) None "gilt nur, solange gezeigt"
        }
        test "Ein Neu-Lesen, das vor der Pause begann, wird nach der Antwort wiederholt" {
            let stale = { onTrip with Freshness = MonthStale }
            let running = stale |> step (SetTargetPause PauseTarget)
            let answered, cmd = update (TargetPauseAnswered (paused, current, Ok (Some PausedInBb))) running
            Expect.isGreaterThan answered.MonthGeneration running.MonthGeneration "die alte Antwort zählt nicht mehr"
            Expect.isNonEmpty cmd "der Monat wird neu gelesen"
        }
        test "Aufheben einer BB-Pause schickt die Absicht LiftPause" {
            let pausedTrip = { onTrip with Month = Success (monthWith current (Some PausedInBoth)) }
            match startPause LiftPause pausedTrip with
            | _, Some (month, request) -> Expect.equal (month, request) (current, { CategoryId = paused; Intent = LiftPause }) "Auftrag"
            | _, None -> failtest "Auftrag erwartet"
            Expect.isNone (snd (startPause PauseTarget pausedTrip)) "Pausieren wird nicht angeboten"
        }
        // budgeting-r3wq8, Bug: die Pause-Antwort sah ein laufendes Neu-Lesen nur, wenn der Monat geladen
        // war. Nach `MoveCallLost` steht `Loading`, die Antwort des Neu-Lesens kennte die Pause nicht.
        test "GIVEN Pause läuft, MoveCallLost WHEN die Pause-Antwort kommt THEN wird der Monat noch einmal gelesen" {
            let running = onTrip |> step (SetTargetPause PauseTarget)
            let lost = running |> step (MoveCallLost "weg")
            let answered, cmd = update (TargetPauseAnswered (paused, current, Ok (Some PausedInBb))) lost
            Expect.isGreaterThan answered.MonthGeneration lost.MonthGeneration "neu gelesen"
            Expect.isNonEmpty cmd "der Monat wird gelesen"
        }
        test "GIVEN ein Neu-Lesen begann und endete während der Pause WHEN die Antwort kommt THEN wird neu gelesen" {
            let running = onTrip |> step (SetTargetPause PauseTarget)
            let rereading = { running with MonthGeneration = running.MonthGeneration + 1 }
            let answered = rereading |> step (TargetPauseAnswered (paused, current, Ok (Some PausedInBb)))
            Expect.isGreaterThan answered.MonthGeneration rereading.MonthGeneration "neu gelesen"
        }
        test "Während eines Monatswechsels liest die Pause-Antwort nicht neu" {
            let running = { onTrip with Freshness = MonthStale } |> step (SetTargetPause PauseTarget)
            let changing = running |> step (SelectMonth next)
            let answered = changing |> step (TargetPauseAnswered (paused, current, Ok (Some PausedInBb)))
            Expect.equal answered.MonthGeneration changing.MonthGeneration "kein zweites Lesen"
        }
        // budgeting-r3wq8: ein abgebrochener Aufruf ist keine Ablehnung. Ob die Pause steht, ist offen:
        // Meldung im Tab, der Monat wird still neu gelesen, die Pause bleibt, wie sie war.
        test "GIVEN Pause läuft WHEN der Aufruf abbricht THEN Meldung im Tab, der Monat wird still neu gelesen" {
            let running = onTrip |> step (SetTargetPause PauseTarget)
            let lost, cmd = update (TargetPauseCallLost (paused, current, "Netz weg")) running
            Expect.equal (pauseError lost paused) (Some (pauseCallLostText "Netz weg")) "Meldung"
            Expect.equal (categoryOf lost paused).Pause None "unverändert"
            Expect.isGreaterThan lost.MonthGeneration running.MonthGeneration "neu gelesen"
            Expect.equal lost.Freshness MonthStale "still, die Liste bleibt"
            Expect.isTrue (match lost.Month with Success _ -> true | _ -> false) "kein Loading"
            Expect.isNonEmpty cmd "der Monat wird gelesen"
        }
        test "GIVEN eine andere Kategorie ist gezeigt WHEN der Aufruf abbricht THEN ein Toast nennt es" {
            let away = onTrip |> step (SetTargetPause PauseTarget) |> step (SelectCategory other)
            let _, _, external = Components.Budget.State.update (TargetPauseCallLost (paused, current, "Netz weg")) away
            Expect.equal external (ShowToast (pauseCallLostText "Netz weg", ToastError)) "Toast"
        }
    ]

// ============================================
// Reihenfolgen (Doktrin Punkt 20)
// ============================================

/// Was die Aufzählung über den Zustand weiß, unabhängig vom getesteten Code. "Monat" und "Geladen" sind
/// getrennt (budgeting-r3wq8): dazwischen läuft ein Monatswechsel, die Seite zeigt noch den alten Monat.
type private Walk = {
    /// Der Monat, den die Seite zeigt (geladen).
    Displayed: string
    /// Der gewählte Monat. Weicht er ab, läuft ein Wechsel.
    Chosen: string
    /// Die gezeigte Kategorie.
    Shown: Guid
    /// Der laufende Aufruf: sein Monat.
    Pending: string option
    /// Die Monate, in denen der Server Ausflug pausiert hat.
    PausedOnServer: Set<string>
}

/// Alle Ereignisse, die im Zustand kommen können: Name, Nachricht, Zustand danach. Die Antwort des
/// Monats liest den Stand des Servers beim Eintreffen.
let private events (walk: Walk) : (string * (Model -> Msg) * Walk) list =
    let otherCategory = if walk.Shown = paused then other else paused
    [ "Tipp", (fun _ -> SetTargetPause PauseTarget), walk
      "Kategorie", (fun _ -> SelectCategory otherCategory), { walk with Shown = otherCategory }
      if walk.Displayed = walk.Chosen then
          let otherMonth = if walk.Chosen = current then next else current
          "Monat", (fun _ -> SelectMonth otherMonth), { walk with Chosen = otherMonth }
      else
          let pause = if walk.PausedOnServer.Contains walk.Chosen then Some PausedInBb else None
          "Geladen", (fun (m: Model) -> Loaded (m.MonthGeneration, Ok (monthWith walk.Chosen pause))), { walk with Displayed = walk.Chosen }
      match walk.Pending with
      | Some month ->
          "Antwort", (fun _ -> TargetPauseAnswered (paused, month, Ok (Some PausedInBb))),
          { walk with Pending = None; PausedOnServer = walk.PausedOnServer.Add month }
      | None -> () ]

let private walkTests =
    let start = { Displayed = current; Chosen = current; Shown = paused; Pending = None; PausedOnServer = Set.empty }
    let rec walkFrom (depth: int) (path: string list) (walk: Walk) (model: Model) : unit =
        if depth > 0 then
            for name, msgOf, after in events walk do
                let route = List.rev (name :: path) |> String.concat ", "
                let model', cmd = update (msgOf model) model
                let after =
                    if name = "Tipp" then
                        // Gesendet wird nur, wenn nichts läuft, kein Monatswechsel läuft, Ausflug gezeigt wird und
                        // dort nicht schon pausiert ist.
                        let expected =
                            walk.Pending.IsNone
                            && walk.Displayed = walk.Chosen
                            && walk.Shown = paused
                            && not (walk.PausedOnServer.Contains walk.Displayed)
                        Expect.equal (not (List.isEmpty cmd)) expected $"{route}: gesendet"
                        if expected then { after with Pending = Some walk.Displayed } else after
                    else after
                let expectedPause = if after.PausedOnServer.Contains after.Displayed then Some PausedInBb else None
                Expect.equal (categoryOf model' paused).Pause expectedPause $"{route}: Pause im Monat {after.Displayed}"
                Expect.equal (categoryOf model' other).Pause None $"{route}: Urlaub unberührt"
                walkFrom (depth - 1) (name :: path) after model'
    testList "Pause: Reihenfolgen" [
        test "In jeder Reihenfolge wirkt die Antwort nur auf ihre Kategorie und ihren Monat, ein zweiter Tipp und ein Tipp im Wechsel senden nicht" {
            walkFrom 6 [] start onTrip
        }
    ]

[<Tests>]
let tests =
    testList "Target pausieren im Client (budgeting-t6p8z)" [ controlTests; flowTests; walkTests ]
