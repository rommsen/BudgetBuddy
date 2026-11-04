module Tests.TargetChangeClientTests

// budgeting-k8wq3: Target ändern und entfernen im Tab Target. Sichtbarkeit und Sperre als reine Funktionen,
// der Ablauf durch das echte `update`. Die Cmds werden unter .NET nur gebaut, nie ausgeführt: der Proxy
// liegt hinter einem Lambda.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Client.DesignSystem
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Moves
open Components.Budget.TargetEdit

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private past = "2026-10-01"
let private current = "2026-11-01"
let private next = "2026-12-01"

let private tripId = Guid.Parse "00000000-0000-0000-0000-0000000a8301"
let private holidayId = Guid.Parse "00000000-0000-0000-0000-0000000a8302"

let private targetOf (goal: decimal) : Target =
    { Goal = eur goal; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = Some true; Day = Some 15 }

/// Ausflug: Target 50 €, Fehlbetrag 50 €. Urlaub: Target 20 €.
let private trip (goal: decimal) : BudgetCategory =
    { Id = tripId
      Name = "Ausflug"
      Assigned = eur 0m
      Activity = eur 0m
      Available = eur 0m
      Underfunded = Some (eur 50m)
      Target = Some (targetOf goal)
      Pause = None }

let private holiday : BudgetCategory =
    { trip 20m with Id = holidayId; Name = "Urlaub"; Underfunded = Some (eur 0m) }

let private monthWith (month: string) (goal: decimal) : BudgetMonth =
    { Month = month
      ToBeBudgeted = eur 500m
      Groups = [ { Name = "Alltag"; Categories = [ trip goal; holiday ] } ] }

let private frame : BudgetFrame =
    { CurrentMonth = current; Months = [ past; current; next ]; CategoryOrder = [ tripId; holidayId ] }

let private update (msg: Msg) (model: Model) =
    let model', cmd, external = Components.Budget.State.update msg model
    model', cmd, external

let private step (msg: Msg) (model: Model) : Model =
    let m, _, _ = update msg model
    m

let private sends (msg: Msg) (model: Model) : bool =
    let _, cmd, _ = update msg model
    not (List.isEmpty cmd)

let private pageIn (month: string) : Model =
    { Components.Budget.Types.init () with
        Frame = Success frame
        ChosenMonth = Some month
        Month = Success (monthWith month 50m)
        Tab = TargetTab }
    |> step (SelectCategory tripId)

let private onTrip = pageIn current

let private categoryOf (model: Model) (id: Guid) : BudgetCategory =
    match model.Month with
    | Success month -> categoriesOf month |> List.find (fun c -> c.Id = id)
    | _ -> failtest "Monat erwartet"

/// Monat wählen und die Antwort des Servers mit dem Stand `goal` einnehmen.
let private moveTo (month: string) (goal: decimal) (model: Model) : Model =
    let moved = step (SelectMonth month) model
    step (Loaded (moved.MonthGeneration, Ok (monthWith month goal))) moved

let private formOf (model: Model) : TargetForm =
    match model.TargetForm with
    | Some form -> form
    | None -> failtest "Formular erwartet"

// ============================================
// Sichtbarkeit und Sperre
// ============================================

let private visibilityTests =
    testList "Tab Target: Sichtbarkeit" [
        test "Im vergangenen und im künftigen Monat nur lesend, keine Nachricht löst einen Aufruf aus" {
            for month in [ past; next ] do
                let model = pageIn month
                Expect.equal (targetTabMode model (categoryOf model tripId)) (TargetReadOnly (targetOf 50m)) $"{month}: nur Text"
                Expect.isNone model.TargetForm $"{month}: kein Formular"
                let typed = model |> step (TargetAmountChanged "60") |> step (TargetDateChanged DateField.EmptyDate)
                for msg in [ SaveTarget; AskRemoveTarget; ConfirmRemoveTarget ] do
                    Expect.isFalse (sends msg typed) $"{month}: {msg} sendet nicht"
                Expect.isFalse (sends ConfirmRemoveTarget (typed |> step AskRemoveTarget)) $"{month}: Entfernen geht nicht"
        }
        test "Im laufenden Monat steht das Formular, ohne Target nicht" {
            match targetTabMode onTrip (categoryOf onTrip tripId) with
            | TargetEditable (target, form) -> Expect.equal (target, form.AmountInput) (targetOf 50m, None) "Formular"
            | other -> failtest $"Formular erwartet: {other}"
            Expect.equal (targetTabMode onTrip { holiday with Target = None }) NoTargetShown "ohne Target"
        }
        test "Ohne Abweichung ist Speichern gesperrt, eine ungültige Eingabe auch" {
            let trip' model = categoryOf model tripId
            Expect.isFalse (canSaveTarget onTrip (trip' onTrip)) "nichts geändert"
            let same = onTrip |> step (TargetAmountChanged "50,00")
            Expect.isFalse (canSaveTarget same (trip' same)) "derselbe Betrag"
            for text in [ "abc"; "-60" ] do
                let bad = onTrip |> step (TargetAmountChanged text)
                Expect.isFalse (canSaveTarget bad (trip' bad)) text
            let badDate = onTrip |> step (TargetDateChanged (DateField.InvalidDate "31.02."))
            Expect.isFalse (canSaveTarget badDate (trip' badDate)) "Datum ungültig"
            let changed = onTrip |> step (TargetAmountChanged "+10")
            Expect.isTrue (canSaveTarget changed (trip' changed)) "Rechnung wie Zugewiesen: 60 €"
        }
    ]

// ============================================
// Ablauf durch das echte update
// ============================================

let private flowTests =
    testList "Tab Target: Ablauf" [
        test "Eingabe im laufenden Monat, Wechsel in den künftigen verwirft sie, zurück zeigt den geladenen Stand" {
            let typed = onTrip |> step (TargetAmountChanged "60")
            let away = typed |> moveTo next 50m
            Expect.isNone away.TargetForm "verworfen"
            Expect.isFalse (sends SaveTarget away) "nichts gesendet"
            let back = away |> moveTo current 50m
            Expect.equal ((formOf back).AmountInput, (formOf back).DateInput) (None, None) "geladener Stand"
        }
        test "Speichern sendet einmal mit dem geladenen Target als Shown, ein zweiter Tipp sendet nicht" {
            let typed = onTrip |> step (TargetAmountChanged "60") |> step (TargetDateChanged (DateField.ValidDate (DateTime(2027, 6, 1))))
            match startTargetSave typed with
            | _, Some (month, request) ->
                Expect.equal month current "Monat"
                Expect.equal
                    request
                    { CategoryId = tripId; Shown = Some (targetOf 50m); Wanted = Some { NewGoal = eur 60m; NewDate = Some "2027-06-01"; NewRhythm = KeepRhythm } }
                    "Auftrag"
            | _, None -> failtest "Auftrag erwartet"
            let running, cmd, _ = update SaveTarget typed
            Expect.isNonEmpty cmd "gesendet"
            Expect.equal running.TargetCall (TargetSaving (tripId, current)) "läuft"
            Expect.isFalse (sends SaveTarget running) "zweiter Tipp"
            Expect.isFalse (sends ConfirmRemoveTarget (running |> step AskRemoveTarget)) "auch kein Entfernen"
        }
        test "Entfernen geht erst nach der Bestätigung, Abbrechen sendet nichts" {
            Expect.isFalse (sends ConfirmRemoveTarget onTrip) "ohne Bestätigung"
            let asked = onTrip |> step AskRemoveTarget
            Expect.isTrue (formOf asked).ConfirmingRemove "Bestätigung offen"
            Expect.isFalse (sends ConfirmRemoveTarget (asked |> step CancelRemoveTarget)) "abgebrochen"
            match startTargetRemove asked with
            | _, Some (_, request) -> Expect.equal (request.Shown, request.Wanted) (Some (targetOf 50m), None) "entfernen"
            | _, None -> failtest "Auftrag erwartet"
        }
        test "Nach Erfolg steht das neue Target, der Monat wird still gelesen, der Fehlbetrag-Knopf ist gesperrt, die Historie fällig" {
            let historyShown =
                let asked = onTrip |> step (SelectInspectorTab HistoryTab)
                asked |> step (CategoryHistoryLoaded (asked.HistoryGeneration, Ok { Entries = []; Spending = [] }))
            let running = historyShown |> step (SelectInspectorTab TargetTab) |> step (TargetAmountChanged "60") |> step SaveTarget
            let answered, cmd, _ = update (TargetChangeAnswered (tripId, current, Ok (TargetWritten (Some (targetOf 60m))))) running
            Expect.equal (categoryOf answered tripId).Target (Some (targetOf 60m)) "neues Target"
            Expect.equal (categoryOf answered holidayId).Target (Some (targetOf 20m)) "Urlaub unberührt"
            Expect.isGreaterThan answered.MonthGeneration running.MonthGeneration "neu gelesen"
            Expect.isNonEmpty cmd "Aufruf"
            Expect.equal answered.Freshness MonthStale "veraltet"
            Expect.isFalse (canCover (answered |> step (SelectInspectorTab MoneyTab))) "Fehlbetrag-Knopf gesperrt"
            Expect.equal (answered.Selected |> Option.map (fun s -> s.History)) (Some NotAsked) "Historie fällig"
            Expect.equal ((formOf answered).AmountInput, answered.TargetCall) (None, TargetIdle) "Formular auf dem neuen Stand"
        }
        test "TargetChangedInYnab zeigt den frischen Stand mit Hinweis, die Eingabe ist verworfen" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            let answered = running |> step (TargetChangeAnswered (tripId, current, Ok (TargetChangedInYnab (Some (targetOf 55m)))))
            Expect.equal (categoryOf answered tripId).Target (Some (targetOf 55m)) "frischer Stand"
            Expect.equal ((formOf answered).AmountInput, (formOf answered).Notice) (None, Some TargetChangedNotice) "Hinweis"
            Expect.equal (targetNoticeText TargetChangedNotice) "Target wurde in YNAB geändert" "Text"
        }
        test "Ein Fehler zeigt die Meldung, die Eingabe bleibt, ein zweites Speichern geht" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            let answered, _, external = update (TargetChangeAnswered (tripId, current, Error "YNAB hat abgelehnt: kaputt")) running
            Expect.equal ((formOf answered).AmountInput, (formOf answered).Notice) (Some "60", Some (TargetRejectedNotice "YNAB hat abgelehnt: kaputt")) "Meldung"
            Expect.equal (categoryOf answered tripId).Target (Some (targetOf 50m)) "Target unverändert"
            Expect.equal external NoOp "kein Toast, die Meldung steht im Tab"
            Expect.isTrue (sends SaveTarget answered) "erneut speichern"
        }
        test "Ein Fehler, während eine andere Kategorie gezeigt wird, kommt als Toast" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget |> step (SelectCategory holidayId)
            let _, _, external = update (TargetChangeAnswered (tripId, current, Error "kaputt")) running
            Expect.equal external (ShowToast (targetRejectedAwayText "kaputt", ToastWarning)) "Toast"
        }
    ]

// ============================================
// Reihenfolgen (Doktrin Punkt 20)
// ============================================

/// Was die Aufzählung über den Zustand weiß, unabhängig vom getesteten Code.
type private Walk = {
    Month: string
    Shown: Guid
    /// Kategorie und Monat des laufenden Aufrufs.
    Pending: (Guid * string) option
    /// Das Target je Kategorie auf dem Server.
    ServerGoal: Map<Guid, decimal>
    /// Das Target je Kategorie im geladenen Monat.
    LoadedGoal: Map<Guid, decimal>
}

let private monthOfGoals (month: string) (goals: Map<Guid, decimal>) : BudgetMonth =
    { Month = month
      ToBeBudgeted = eur 500m
      Groups = [ { Name = "Alltag"; Categories = [ trip goals[tripId]; { holiday with Target = Some (targetOf goals[holidayId]) } ] } ] }

let private events (walk: Walk) : (string * Walk) list =
    let otherMonth = if walk.Month = current then next else current
    let otherCategory = if walk.Shown = tripId then holidayId else tripId
    [ "Tipp", walk
      "Kategorie", { walk with Shown = otherCategory }
      "Monat", { walk with Month = otherMonth; LoadedGoal = walk.ServerGoal }
      match walk.Pending with
      | Some (category, month) ->
          "Antwort",
          { walk with
              Pending = None
              ServerGoal = walk.ServerGoal.Add(category, 60m)
              LoadedGoal = if month = walk.Month then walk.LoadedGoal.Add(category, 60m) else walk.LoadedGoal }
      | None -> () ]

let private walkTests =
    let goals = Map.ofList [ tripId, 50m; holidayId, 20m ]
    let start = { Month = current; Shown = tripId; Pending = None; ServerGoal = goals; LoadedGoal = goals }
    let rec walkFrom (depth: int) (path: string list) (walk: Walk) (model: Model) : unit =
        if depth > 0 then
            for name, after in events walk do
                let route = List.rev (name :: path) |> String.concat ", "
                let model', after =
                    match name with
                    | "Tipp" ->
                        let typed = model |> step (TargetAmountChanged "60")
                        let stepped, cmd, _ = update SaveTarget typed
                        // Gesendet wird nur im laufenden Monat, wenn nichts läuft und 60 € vom geladenen Stand abweichen.
                        let expected = walk.Pending.IsNone && walk.Month = current && walk.LoadedGoal[walk.Shown] <> 60m
                        Expect.equal (not (List.isEmpty cmd)) expected $"{route}: gesendet"
                        stepped, (if expected then { after with Pending = Some (walk.Shown, walk.Month) } else after)
                    | "Kategorie" -> step (SelectCategory after.Shown) model, after
                    | "Monat" ->
                        let moved = step (SelectMonth after.Month) model
                        step (Loaded (moved.MonthGeneration, Ok (monthOfGoals after.Month walk.ServerGoal))) moved, after
                    | _ ->
                        let category, month = walk.Pending |> Option.get
                        step (TargetChangeAnswered (category, month, Ok (TargetWritten (Some (targetOf 60m))))) model, after
                for id in [ tripId; holidayId ] do
                    Expect.equal (categoryOf model' id).Target (Some (targetOf after.LoadedGoal[id])) $"{route}: Target von {id}"
                let formFor = model'.TargetForm |> Option.map (fun f -> f.CategoryId, f.Month)
                let expectedForm = if after.Month = current then Some (after.Shown, current) else None
                Expect.equal formFor expectedForm $"{route}: Formular nur für die gezeigte Kategorie im laufenden Monat"
                walkFrom (depth - 1) (name :: path) after model'
    testList "Tab Target: Reihenfolgen" [
        test "In jeder Reihenfolge wirkt die Antwort nur auf ihre Kategorie, ein zweiter Tipp sendet nicht" {
            walkFrom 5 [] start onTrip
        }
    ]

[<Tests>]
let tests =
    testList "Target ändern im Client (budgeting-k8wq3)" [ visibilityTests; flowTests; walkTests ]
