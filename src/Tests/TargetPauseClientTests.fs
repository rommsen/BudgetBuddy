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
            Expect.isFalse (match lost.Month with Success _ -> true | _ -> false) "Vorbedingung: der Monat ist nicht geladen"
            let answered, cmd = update (TargetPauseAnswered (paused, current, Ok (Some PausedInBb))) lost
            Expect.isGreaterThan answered.MonthGeneration lost.MonthGeneration "neu gelesen"
            Expect.isNonEmpty cmd "der Monat wird gelesen"
        }
        test "Während eines Wechsels in einen anderen Monat liest die Pause-Antwort nicht neu" {
            let running = { onTrip with Freshness = MonthStale } |> step (SetTargetPause PauseTarget)
            let changing = running |> step (SelectMonth next)
            let answered = changing |> step (TargetPauseAnswered (paused, current, Ok (Some PausedInBb)))
            Expect.equal answered.MonthGeneration changing.MonthGeneration "kein zweites Lesen"
        }
        // budgeting-e6p4r, Regressionstest zu budgeting-c6vn4 (Q1), Bug: eine Pause-Antwort während eines
        // Wechsels zurück in den Monat der Pause las nicht neu. Das Lesen des Wechsels konnte vor dem Speichern
        // begonnen haben, seine Antwort kannte die Pause nicht, sie verschwand bis zum nächsten Lesen. Bisher
        // nur im Walk bei Tiefe 6 sichtbar.
        test "GIVEN Pause läuft, Wechsel in den nächsten Monat und zurück WHEN die Pause-Antwort kommt THEN wird der Monat noch einmal gelesen" {
            let running = onTrip |> step (SetTargetPause PauseTarget)
            let away = running |> step (SelectMonth next)
            let away = away |> step (Loaded (away.MonthGeneration, Ok (monthWith next None)))
            let back = away |> step (SelectMonth current)
            Expect.isTrue (monthChanging back) "Vorbedingung: der Wechsel zurück läuft"
            let answered, cmd = update (TargetPauseAnswered (paused, current, Ok (Some PausedInBb))) back
            Expect.isGreaterThan answered.MonthGeneration back.MonthGeneration "neu gelesen"
            Expect.isNonEmpty cmd "der Monat wird gelesen"
        }
        // budgeting-e6p4r, Regressionstest zu budgeting-c6vn4 (Q1), derselbe Bug beim Abbruch: ob die Pause
        // gespeichert ist, ist offen, das Lesen des Wechsels zurück kann vor dem Speichern begonnen haben.
        test "GIVEN Pause läuft, Wechsel in den nächsten Monat und zurück WHEN der Aufruf abbricht THEN wird der Monat noch einmal gelesen und der Toast sagt es" {
            let running = onTrip |> step (SetTargetPause PauseTarget)
            let away = running |> step (SelectMonth next)
            let away = away |> step (Loaded (away.MonthGeneration, Ok (monthWith next None)))
            let back = away |> step (SelectMonth current)
            Expect.isTrue (monthChanging back) "Vorbedingung: der Wechsel zurück läuft"
            let lost, cmd, external = Components.Budget.State.update (TargetPauseCallLost (paused, current, "Netz weg")) back
            Expect.isGreaterThan lost.MonthGeneration back.MonthGeneration "neu gelesen"
            Expect.isNonEmpty cmd "der Monat wird gelesen"
            Expect.equal external (ShowToast (pauseCallLostText true "Netz weg", ToastError)) "Toast verspricht das Neu-Lesen"
        }
        // budgeting-r3wq8: ein abgebrochener Aufruf ist keine Ablehnung. Ob die Pause steht, ist offen:
        // Meldung im Tab, der Monat wird still neu gelesen, die Pause bleibt, wie sie war.
        test "GIVEN Pause läuft WHEN der Aufruf abbricht THEN Meldung im Tab, der Monat wird still neu gelesen" {
            let running = onTrip |> step (SetTargetPause PauseTarget)
            let lost, cmd = update (TargetPauseCallLost (paused, current, "Netz weg")) running
            Expect.equal (pauseError lost paused) (Some (pauseCallLostText true "Netz weg")) "Meldung"
            Expect.equal (categoryOf lost paused).Pause None "unverändert"
            Expect.isGreaterThan lost.MonthGeneration running.MonthGeneration "neu gelesen"
            Expect.equal lost.Freshness MonthStale "still, die Liste bleibt"
            Expect.isTrue (match lost.Month with Success _ -> true | _ -> false) "kein Loading"
            Expect.isNonEmpty cmd "der Monat wird gelesen"
        }
        test "GIVEN eine andere Kategorie ist gezeigt WHEN der Aufruf abbricht THEN ein Toast nennt es" {
            let away = onTrip |> step (SetTargetPause PauseTarget) |> step (SelectCategory other)
            let _, _, external = Components.Budget.State.update (TargetPauseCallLost (paused, current, "Netz weg")) away
            Expect.equal
                external
                (ShowToast ("Ob die Pause gespeichert ist, ist unklar: Netz weg. Der Stand wird gleich neu gelesen.", ToastError))
                "Toast"
        }
        // budgeting-c6vn4 (P2): der Toast versprach ein Neu-Lesen auch, wenn ein anderer Monat gezeigt wird,
        // und eine Meldung mit Punkt ergab zwei Punkte.
        test "GIVEN ein anderer Monat ist gezeigt WHEN der Aufruf abbricht THEN verspricht der Toast kein Neu-Lesen, ohne doppelten Punkt" {
            let running = onTrip |> step (SetTargetPause PauseTarget)
            let elsewhere = running |> step (SelectMonth next)
            let elsewhere = elsewhere |> step (Loaded (elsewhere.MonthGeneration, Ok (monthWith next None)))
            let lost, _, external = Components.Budget.State.update (TargetPauseCallLost (paused, current, "Netz weg.")) elsewhere
            Expect.equal external (ShowToast ("Ob die Pause gespeichert ist, ist unklar: Netz weg.", ToastError)) "Toast"
            Expect.equal lost.MonthGeneration elsewhere.MonthGeneration "kein Neu-Lesen"
        }
        // budgeting-c6vn4 (P2): eine Ablehnung zu einer nicht gezeigten Kategorie blieb ohne Rückmeldung.
        test "GIVEN eine andere Kategorie ist gezeigt WHEN der Server ablehnt THEN ein Warn-Toast nennt den Grund" {
            let away = onTrip |> step (SetTargetPause PauseTarget) |> step (SelectCategory other)
            let _, _, external = Components.Budget.State.update (TargetPauseAnswered (paused, current, Error "Ausflug hat kein Target.")) away
            Expect.equal external (ShowToast ("Pause nicht gespeichert: Ausflug hat kein Target.", ToastWarning)) "Toast"
        }
        test "Die Meldung steht im Tab Target und im Tab Geld beim Zuweisen, in den anderen Tabs und Modi als Toast" {
            let answerIn (prepare: Model -> Model) =
                let running = onTrip |> prepare |> step (SetTargetPause PauseTarget)
                let _, _, rejected = Components.Budget.State.update (TargetPauseAnswered (paused, current, Error "nein")) running
                let _, _, lost = Components.Budget.State.update (TargetPauseCallLost (paused, current, "weg")) running
                rejected, lost
            for name, prepare in [ "Tab Target", step (SelectInspectorTab TargetTab); "Tab Geld, Zuweisen", id ] do
                Expect.equal (answerIn prepare) (NoOp, NoOp) $"{name}: im Tab"
            // Der Tipp geht im Tab Geld nur beim Zuweisen hinaus; danach Holen gewählt, steht der Knopf nicht mehr da.
            let running = onTrip |> step (SetTargetPause PauseTarget)
            for name, moved in [ "Tab Historie", running |> step (SelectInspectorTab HistoryTab)
                                 "Tab Geld, Holen", running |> step (SwitchMoneyMode TakeMode) ] do
                let _, _, rejected = Components.Budget.State.update (TargetPauseAnswered (paused, current, Error "nein")) moved
                Expect.equal rejected (ShowToast ("Pause nicht gespeichert: nein", ToastWarning)) $"{name}: Ablehnung als Toast"
                let _, _, lost = Components.Budget.State.update (TargetPauseCallLost (paused, current, "weg")) moved
                Expect.equal lost (ShowToast ("Ob die Pause gespeichert ist, ist unklar: weg. Der Stand wird gleich neu gelesen.", ToastError)) $"{name}: Abbruch als Toast"
        }
    ]

// ============================================
// Reihenfolgen (Doktrin Punkt 20)
// ============================================

/// Was die Aufzählung über den Zustand weiß, unabhängig vom getesteten Code. "Monat" und das Eintreffen
/// des Lesens sind getrennt (budgeting-r3wq8): dazwischen läuft ein Monatswechsel, die Seite zeigt noch
/// den alten Monat. Jedes Lesen des Monats trägt den Serverstand von seinem Start (budgeting-c6vn4, Q1):
/// eine Antwort, die vor dem Speichern der Pause begann, kennt sie nicht.
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
    /// Laufende Lesevorgänge: Generation, Monat und der Serverstand beim Start.
    Reads: (int * string * Set<string>) list
    /// Die Generation des jüngsten gestarteten Lesens. Nur seine Antwort wird gezeigt.
    LastRead: int
    /// Steht eine Liste da? Nach "Neu-Lesen startet" ohne Wechsel steht `Loading` bis zum jüngsten Lesen:
    /// dann lässt sich weder wählen noch tippen.
    Listed: bool
}

/// Alle Ereignisse, die im Zustand kommen können: Name, Nachricht, Zustand danach.
let private events (walk: Walk) : (string * Msg * Walk) list =
    let otherCategory = if walk.Shown = paused then other else paused
    let arrive (generation, month, snapshot: Set<string>) =
        let pause = if snapshot.Contains month then Some PausedInBb else None
        $"Lesen {generation} kommt an",
        Loaded (generation, Ok (monthWith month pause)),
        { walk with
            Reads = walk.Reads |> List.filter (fun (g, _, _) -> g <> generation)
            Displayed = if generation = walk.LastRead then month else walk.Displayed
            Listed = walk.Listed || generation = walk.LastRead }
    [ "Tipp", SetTargetPause PauseTarget, walk
      if walk.Listed then
          "Kategorie", SelectCategory otherCategory, { walk with Shown = otherCategory }
      if walk.Displayed = walk.Chosen && walk.Listed then
          let otherMonth = if walk.Chosen = current then next else current
          "Monat", SelectMonth otherMonth, { walk with Chosen = otherMonth }
      // Ohne Wechsel steht danach `Loading`, im Wechsel bleibt die alte Liste stehen.
      "Neu-Lesen startet", MoveCallLost "weg", { walk with Listed = walk.Listed && walk.Displayed <> walk.Chosen }
      // Das älteste und das jüngste laufende Lesen: dazwischen liegt nichts Neues.
      match walk.Reads with
      | [] -> ()
      | [ only ] -> arrive only
      | oldest :: rest ->
          arrive oldest
          arrive (List.last rest)
      match walk.Pending with
      | Some month ->
          "Antwort", TargetPauseAnswered (paused, month, Ok (Some PausedInBb)),
          { walk with Pending = None; PausedOnServer = walk.PausedOnServer.Add month }
          "Abbruch, gespeichert", TargetPauseCallLost (paused, month, "weg"),
          { walk with Pending = None; PausedOnServer = walk.PausedOnServer.Add month }
          "Abbruch, nicht gespeichert", TargetPauseCallLost (paused, month, "weg"), { walk with Pending = None }
      | None -> () ]

let private walkTests =
    let start =
        { Displayed = current; Chosen = current; Shown = paused; Pending = None; PausedOnServer = Set.empty
          Reads = []; LastRead = onTrip.MonthGeneration; Listed = true }
    let rec walkFrom (depth: int) (path: string list) (walk: Walk) (model: Model) : unit =
        if depth > 0 then
            for name, msg, after in events walk do
                let route = List.rev (name :: path) |> String.concat ", "
                let model', cmd = update msg model
                let after =
                    if name = "Tipp" then
                        let sent = not (List.isEmpty cmd)
                        // Nie, während etwas läuft, ein Wechsel läuft oder eine andere Kategorie gezeigt wird.
                        // Ist alles gelesen, genau dann, wenn Ausflug dort nicht schon pausiert ist.
                        let allowed = walk.Pending.IsNone && walk.Displayed = walk.Chosen && walk.Shown = paused && walk.Listed
                        if not allowed then Expect.isFalse sent $"{route}: nicht gesendet"
                        if List.isEmpty walk.Reads then
                            Expect.equal sent (allowed && not (walk.PausedOnServer.Contains walk.Displayed)) $"{route}: gesendet"
                        if sent then { after with Pending = Some walk.Displayed } else after
                    else after
                // Ein neues Lesen beginnt, wenn die Generation steigt: es liest den gewählten Monat.
                let after =
                    if model'.MonthGeneration > model.MonthGeneration then
                        { after with
                            Reads = after.Reads @ [ model'.MonthGeneration, after.Chosen, after.PausedOnServer ]
                            LastRead = model'.MonthGeneration }
                    else after
                // Orakel: sind alle Lesevorgänge angekommen, zeigt der Monat den Serverstand.
                if List.isEmpty after.Reads then
                    let expectedPause = if after.PausedOnServer.Contains after.Displayed then Some PausedInBb else None
                    Expect.equal (categoryOf model' paused).Pause expectedPause $"{route}: Pause im Monat {after.Displayed}"
                    Expect.equal (categoryOf model' other).Pause None $"{route}: Urlaub unberührt"
                walkFrom (depth - 1) (name :: path) after model'
    testList "Pause: Reihenfolgen" [
        test "In jeder Reihenfolge zeigt der Monat nach allen Lesevorgängen den Stand des Servers, ein zweiter Tipp und ein Tipp im Wechsel senden nicht" {
            walkFrom 6 [] start onTrip
        }
    ]

[<Tests>]
let tests =
    testList "Target pausieren im Client (budgeting-t6p8z)" [ controlTests; flowTests; walkTests ]
