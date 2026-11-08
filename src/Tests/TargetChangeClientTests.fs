module Tests.TargetChangeClientTests

// budgeting-k8wq3: Target ändern und entfernen im Tab Target. Sichtbarkeit und Sperre als reine Funktionen,
// der Ablauf durch das echte `update`. Die Cmds werden unter .NET nur gebaut, nie ausgeführt: der Proxy
// liegt hinter einem Lambda.

open System
open Expecto
open Elmish
open Shared.Domain
open Shared.Budgeting
open Types
open Client.DesignSystem
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Moves
open Components.Budget.TargetFormCore
open Components.Budget.TargetEdit

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private past = "2026-10-01"
let private current = "2026-11-01"
let private next = "2026-12-01"

let private tripId = Guid.Parse "00000000-0000-0000-0000-0000000a8301"
let private holidayId = Guid.Parse "00000000-0000-0000-0000-0000000a8302"

let private targetOf (goal: decimal) : Target =
    { Goal = eur goal; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = Some true; Day = Some 15; CreatedIn = None }

/// Ausflug: Target 50 €, Fehlbetrag 50 €. Urlaub: Target 20 €.
let private trip (goal: decimal) : BudgetCategory =
    { Id = tripId
      Name = "Ausflug"
      Assigned = eur 0m
      Activity = eur 0m
      Available = eur 0m
      Underfunded = Some (eur 50m)
      Target = Some (targetOf goal)
      Pause = None; OpenWriteBack = None; Deactivated = None }

let private holiday : BudgetCategory =
    { trip 20m with Id = holidayId; Name = "Urlaub"; Underfunded = Some (eur 0m) }

let private monthWith (month: string) (goal: decimal) : BudgetMonth =
    { Month = month
      ToBeBudgeted = eur 500m
      Groups = [ { Name = "Alltag"; Categories = [ trip goal; holiday ] } ] }

let private frame : BudgetFrame =
    { CurrentMonth = current; Months = [ past; current; next ]; CategoryOrder = [ tripId; holidayId ]; MonthStartHidden = [] }

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

/// Gezeigtes Target, Sperrlage und Formular der Kategorie, wie die View sie an den Kern gibt (`targetTabMode`,
/// `targetFormLocks`). Speichern und seine Sätze prüfen die Tests am Kern (budgeting-p8nd0).
let private tabForm (model: Model) (category: BudgetCategory) : Target option * TargetFormLocks * TargetForm =
    let locks = targetFormLocks model category.Id
    match targetTabMode model category with
    | TargetEditable (target, form) -> Some target, locks, form
    | TargetCreatable form -> None, locks, form
    | other -> failtest $"Formular erwartet: {other}"

/// Die Sperrlage der Kategorie des Formulars (`targetFormLocks`), für Felder und Entfernen am Kern.
let private locksOf (model: Model) : TargetFormLocks =
    targetFormLocks model (formOf model).CategoryId

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
        test "Im laufenden Monat steht das Formular, für eine andere Kategorie nicht" {
            match targetTabMode onTrip (categoryOf onTrip tripId) with
            | TargetEditable (target, form) -> Expect.equal (target, form.AmountInput) (targetOf 50m, None) "Formular"
            | other -> failtest $"Formular erwartet: {other}"
            Expect.equal (targetTabMode onTrip { holiday with Target = None }) NoTargetShown "ohne Target"
        }
        test "Unter dem Betragsfeld steht ein Hinweis bei negativem Betrag und ungültiger Rechnung" {
            let hint text = targetAmountHint (Some (targetOf 50m)) (formOf (onTrip |> step (TargetAmountChanged text)))
            Expect.equal (hint "-60") (Some "Target darf nicht negativ sein") "negativ"
            Expect.equal (hint "50-60") (Some "Target darf nicht negativ sein") "Rechnung negativ"
            Expect.equal (hint "abc") (Some "Keine gültige Rechnung") "ungültig"
            Expect.equal (hint "60") None "gültig"
            Expect.equal (targetAmountHint (Some (targetOf 50m)) (formOf onTrip)) None "ohne Eingabe"
        }
        test "Mit Datum: ein leeres Datum löscht es, dasselbe Datum ist keine Änderung" {
            let dated = { targetOf 50m with Date = Some "2027-06-01" }
            let form entry = { formOf onTrip with DateInput = Some entry }
            Expect.equal
                (targetFormState (Some dated) (form DateField.EmptyDate))
                (FormChanged { NewGoal = eur 50m; NewDate = None; NewRhythm = KeepRhythm; NewDay = None })
                "leer löscht"
            Expect.equal (targetFormState (Some dated) (form (DateField.ValidDate (DateTime(2027, 6, 1))))) FormUnchanged "gleiches Datum"
        }
        test "Ohne Abweichung ist Speichern gesperrt, eine ungültige Eingabe auch" {
            let trip' model = categoryOf model tripId
            Expect.isFalse (tabForm onTrip (trip' onTrip) |||> formCanSave) "nichts geändert"
            let same = onTrip |> step (TargetAmountChanged "50,00")
            Expect.isFalse (tabForm same (trip' same) |||> formCanSave) "derselbe Betrag"
            for text in [ "abc"; "-60" ] do
                let bad = onTrip |> step (TargetAmountChanged text)
                Expect.isFalse (tabForm bad (trip' bad) |||> formCanSave) text
            let badDate = onTrip |> step (TargetDateChanged (DateField.InvalidDate "31.02."))
            Expect.isFalse (tabForm badDate (trip' badDate) |||> formCanSave) "Datum ungültig"
            let changed = onTrip |> step (TargetAmountChanged "+10")
            Expect.isTrue (tabForm changed (trip' changed) |||> formCanSave) "Rechnung wie Zugewiesen: 60 €"
        }
    ]

// ============================================
// Ablauf durch das echte update
// ============================================

let private requestOf (model: Model) : TargetChangeRequest =
    match startTargetSave model with
    | _, Some (_, request) -> request
    | _, None -> failtest "Aufruf erwartet"

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
                    { CategoryId = tripId; Shown = Some (targetOf 50m); Wanted = Some { NewGoal = eur 60m; NewDate = Some "2027-06-01"; NewRhythm = KeepRhythm; NewDay = None }; CommandId = None }
                    "Auftrag"
            | _, None -> failtest "Auftrag erwartet"
            let running, cmd, _ = update SaveTarget typed
            Expect.isNonEmpty cmd "gesendet"
            Expect.equal running.TargetCall (TargetSaving (tripId, current, SavingTarget, FromTargetTab)) "läuft"
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
        // Review-Fund eh8xe (Frontend W2): die Ablehnung wurde am Text erkannt und rot gezeigt. Jetzt trägt die
        // Antwort BBs Stand, der Tab zeigt ihn ohne Neulesen, der Hinweis ist gelb, die Eingabe bleibt.
        test "GIVEN BBs Stand ist inzwischen 55 WHEN die Antwort kommt THEN steht 55, die Eingabe bleibt mit Hinweis, kein Neulesen" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            let answered, cmd, external = update (TargetChangeAnswered (tripId, current, Ok (TargetChangedMeanwhile (Some (targetOf 55m))))) running
            Expect.equal (categoryOf answered tripId).Target (Some (targetOf 55m)) "BBs Stand"
            Expect.equal ((formOf answered).AmountInput, (formOf answered).Notice) (Some "60", Some TargetChangedMeanwhileNotice) "Eingabe und Hinweis"
            Expect.equal answered.TargetCall TargetIdle "Aufruf frei"
            Expect.equal answered.MonthGeneration running.MonthGeneration "kein Neulesen"
            // Nur die Leiste liest ihre Prüfung neu (budgeting-gvajj): BBs Stand war ein anderer als gezeigt.
            Expect.equal answered.MonthStartCheckGeneration (running.MonthStartCheckGeneration + 1) "die Leiste liest neu"
            Expect.equal cmd.Length 1 "kein weiterer Aufruf"
            Expect.equal external NoOp "kein Toast im offenen Tab"
            Expect.isTrue (sends SaveTarget answered) "erneut speichern gegen den neuen Stand"
            Expect.equal (requestOf answered).Shown (Some (targetOf 55m)) "gezeigt ist jetzt BBs Stand"
        }
        test "Ein Fehler zeigt die Meldung, die Eingabe bleibt, der Monat wird nicht neu gelesen, ein zweites Speichern geht" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            let answered, cmd, external = update (TargetChangeAnswered (tripId, current, Error "Targets nicht lesbar")) running
            Expect.equal ((formOf answered).AmountInput, (formOf answered).Notice) (Some "60", Some (TargetRejectedNotice "Targets nicht lesbar")) "Meldung"
            Expect.equal (categoryOf answered tripId).Target (Some (targetOf 50m)) "Target unverändert"
            Expect.equal answered.MonthGeneration running.MonthGeneration "kein Neulesen"
            Expect.isEmpty cmd "kein Aufruf"
            Expect.equal external NoOp "kein Toast, die Meldung steht im Tab"
            Expect.isTrue (sends SaveTarget answered) "erneut speichern"
        }
        // Review-Fund eh8xe (Frontend W1): brach der Aufruf zweimal mit Status 0 ab, galt das als Ablehnung,
        // und der Monat wurde nicht neu gelesen, obwohl das Target gespeichert sein konnte.
        test "GIVEN Speichern läuft WHEN der Aufruf abbricht THEN ist der Aufruf frei, die Eingabe bleibt, der Monat wird still gelesen" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            let lost, cmd, external = update (TargetChangeCallLost (tripId, current, "Http error (0)")) running
            Expect.equal lost.TargetCall TargetIdle "Aufruf frei"
            Expect.equal ((formOf lost).AmountInput, (formOf lost).Notice) (Some "60", Some (TargetRejectedNotice (targetCallLostText true "Http error (0)"))) "Eingabe und Satz"
            Expect.isGreaterThan lost.MonthGeneration running.MonthGeneration "neu gelesen"
            Expect.isNonEmpty cmd "Aufruf"
            Expect.equal external NoOp "Satz im Tab"
        }
        test "Bricht der Aufruf ab, während eine andere Kategorie gezeigt wird, kommt der Satz als Toast" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget |> step (SelectCategory holidayId)
            let lost, _, external = update (TargetChangeCallLost (tripId, current, "Netz weg.")) running
            Expect.equal external (ShowToast (targetCallLostText true "Netz weg.", ToastError)) "Toast"
            Expect.equal lost.TargetCall TargetIdle "Aufruf frei"
        }
        test "Bricht der Aufruf ab, während ein anderer Monat gewählt ist, wird nichts neu gelesen und der Satz verspricht es nicht" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget |> moveTo next 50m
            let lost, cmd, external = update (TargetChangeCallLost (tripId, current, "weg")) running
            Expect.equal lost.MonthGeneration running.MonthGeneration "kein Neulesen"
            // Der Ausgang ist offen: nur die Leiste liest ihre Prüfung neu (budgeting-gvajj), sie gilt dem laufenden Monat.
            Expect.equal lost.MonthStartCheckGeneration (running.MonthStartCheckGeneration + 1) "die Leiste liest neu"
            Expect.equal cmd.Length 1 "kein weiterer Aufruf"
            Expect.equal external (ShowToast (targetCallLostText false "weg", ToastError)) "Toast ohne Versprechen"
        }
        // budgeting-r3wq8, Bug: während des Speicherns ließen sich Betrag und Datum weiter bearbeiten. Die
        // Antwort setzte das Formular zurück, die nachgetippte Eingabe war still weg.
        test "GIVEN Speichern läuft WHEN getippt wird THEN nehmen die Felder nichts an, danach wieder" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            Expect.isTrue (formFieldsLocked (locksOf running)) "gesperrt"
            let typed = running |> step (TargetAmountChanged "70") |> step (TargetDateChanged (DateField.ValidDate (DateTime(2027, 6, 1))))
            Expect.equal ((formOf typed).AmountInput, (formOf typed).DateInput) (Some "60", None) "keine Eingabe angenommen"
            let answered = typed |> step (TargetChangeAnswered (tripId, current, Ok (TargetWritten (Some (targetOf 60m)))))
            Expect.isFalse (formFieldsLocked (locksOf answered)) "wieder frei"
            Expect.equal (formOf (answered |> step (TargetAmountChanged "70"))).AmountInput (Some "70") "Eingabe geht wieder"
        }
        test "Läuft das Speichern einer anderen Kategorie, ist das Formular der gezeigten frei" {
            let away = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget |> step (SelectCategory holidayId)
            Expect.isFalse (formFieldsLocked (locksOf away)) "frei"
            Expect.equal (formOf (away |> step (TargetAmountChanged "30"))).AmountInput (Some "30") "Eingabe"
        }
        test "Ein Fehler, während der Tab Historie offen ist, kommt als Toast, die Meldung bleibt im Formular" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget |> step (SelectInspectorTab HistoryTab)
            let answered, _, external = update (TargetChangeAnswered (tripId, current, Error "kaputt")) running
            Expect.equal external (ShowToast (targetRejectedAwayText "kaputt", ToastWarning)) "Toast"
            Expect.equal (formOf answered).Notice (Some (TargetRejectedNotice "kaputt")) "Meldung im Formular"
        }
        test "Target entfernen ist gesperrt, solange die Bestätigung offen ist" {
            let asked = onTrip |> step AskRemoveTarget
            Expect.isTrue (formRemoveLocked (locksOf asked) (formOf asked)) "offen"
            let cancelled = asked |> step CancelRemoveTarget
            Expect.isFalse (formRemoveLocked (locksOf cancelled) (formOf cancelled)) "abgebrochen"
            let saving = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            Expect.isTrue (formRemoveLocked (locksOf saving) (formOf saving)) "Aufruf läuft"
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

// ============================================
// Anlegen und Wechsel der Art (budgeting-xp46a)
// ============================================

/// Der Monat mit Urlaub ohne Target, Urlaub gewählt.
let private onHolidayWithout (month: string) : Model =
    let loaded = monthWith month 50m
    let groups = loaded.Groups |> List.map (fun g -> { g with Categories = g.Categories |> List.map (fun c -> if c.Id = holidayId then { c with Target = None } else c) })
    { Components.Budget.Types.init () with
        Frame = Success frame
        ChosenMonth = Some month
        Month = Success { loaded with Groups = groups }
        Tab = TargetTab }
    |> step (SelectCategory holidayId)

let private createTests =
    testList "Tab Target: Anlegen" [
        test "Ohne Target im künftigen und im vergangenen Monat steht Kein Target ohne Formular, im laufenden das Formular zum Anlegen" {
            for month in [ past; next ] do
                let model = onHolidayWithout month
                Expect.equal (targetTabMode model (categoryOf model holidayId)) NoTargetShown $"{month}"
                Expect.isFalse (sends SaveTarget (model |> step (TargetAmountChanged "50"))) $"{month}: sendet nicht"
            let model = onHolidayWithout current
            match targetTabMode model (categoryOf model holidayId) with
            | TargetCreatable form -> Expect.equal (targetRhythmValue None form) (Some Monthly) "Vorgabe Monat"
            | other -> failtest $"Formular zum Anlegen erwartet: {other}"
        }
        test "Anlegen geht erst mit Betrag, gesendet wird kein gezeigtes Target und der gewählte Rhythmus" {
            let model = onHolidayWithout current
            Expect.isFalse (tabForm model (categoryOf model holidayId) |||> formCanSave) "ohne Betrag"
            let typed = model |> step (TargetAmountChanged "50")
            Expect.isTrue (tabForm typed (categoryOf typed holidayId) |||> formCanSave) "mit Betrag"
            Expect.equal
                (requestOf typed)
                { CategoryId = holidayId; Shown = None; Wanted = Some { NewGoal = eur 50m; NewDate = None; NewRhythm = SetRhythm Monthly; NewDay = None }; CommandId = None }
                "monatlich"
            let yearly = typed |> step (TargetRhythmChanged Yearly) |> step (TargetDateChanged (DateField.ValidDate (DateTime(2027, 6, 1))))
            Expect.equal
                (requestOf yearly).Wanted
                (Some { NewGoal = eur 50m; NewDate = Some "2027-06-01"; NewRhythm = SetRhythm Yearly; NewDay = None })
                "jährlich mit Datum"
            Expect.isTrue (sends SaveTarget yearly) "ohne Warnung gesendet"
        }
    ]

let private changeTests =
    testList "Tab Target: Wechsel der Art" [
        test "Den gelesenen Rhythmus wählen ist keine Änderung" {
            Expect.isFalse ((tabForm (onTrip |> step (TargetRhythmChanged Monthly)) (trip 50m) |||> formCanSave)) "monatlich bleibt"
        }
        test "GIVEN Wechsel mit Verlusten WHEN Speichern THEN erst die Warnung, gesendet wird nur nach Bestätigung" {
            let chosen = onTrip |> step (TargetRhythmChanged Yearly)
            let warned, cmd, _ = update SaveTarget chosen
            Expect.isEmpty cmd "Speichern sendet nicht"
            Expect.isTrue (formOf warned).ConfirmingChange "Warnung offen"
            Expect.isFalse (tabForm warned (trip 50m) |||> formCanSave) "Speichern gesperrt, solange sie offen ist"
            let confirmed, cmd, _ = update ConfirmTargetChange warned
            Expect.isNonEmpty cmd "Bestätigen sendet"
            Expect.equal confirmed.TargetCall (TargetSaving (tripId, current, SavingTarget, FromTargetTab)) "Aufruf läuft"
            Expect.isFalse (formOf confirmed).ConfirmingChange "Warnung zu"
        }
        test "GIVEN Warnung offen WHEN Abbrechen THEN nichts gesendet, die Eingabe bleibt" {
            let warned = onTrip |> step (TargetAmountChanged "70") |> step (TargetRhythmChanged Yearly) |> step SaveTarget
            let cancelled, cmd, _ = update CancelTargetChange warned
            Expect.isEmpty cmd "nichts gesendet"
            let form = formOf cancelled
            Expect.equal (form.ConfirmingChange, form.AmountInput, form.RhythmInput) (false, Some "70", Some Yearly) "Eingabe steht"
            Expect.isFalse (sends ConfirmTargetChange cancelled) "Bestätigen nach Abbrechen sendet nicht"
        }
        test "GIVEN reine Betragsänderung THEN keine Warnung" {
            let typed = onTrip |> step (TargetAmountChanged "70")
            let saved, cmd, _ = update SaveTarget typed
            Expect.isNonEmpty cmd "direkt gesendet"
            Expect.isFalse (formOf saved).ConfirmingChange "keine Warnung"
        }
        test "GIVEN Wechsel ohne Verlust THEN keine Warnung" {
            let plain = { targetOf 50m with Day = None }
            let model = onTrip |> withTarget tripId current (Some plain)
            Expect.isTrue (sends SaveTarget (model |> step (TargetRhythmChanged Weekly) |> step (TargetWeekdayChanged 3))) "gesendet"
        }
        test "GIVEN Warnung offen WHEN Monatswechsel THEN Warnung zu, nichts gesendet" {
            let warned = onTrip |> step (TargetRhythmChanged Weekly) |> step (TargetWeekdayChanged 3) |> step SaveTarget
            let moved = step (SelectMonth next) warned
            Expect.isNone moved.TargetForm "Formular weg"
            Expect.isFalse (sends ConfirmTargetChange moved) "nichts gesendet"
            let back = moved |> moveTo current 50m
            Expect.isFalse (formOf back).ConfirmingChange "zurück im laufenden Monat ohne Warnung"
            Expect.isFalse (sends ConfirmTargetChange back) "auch dort nichts"
        }
        // budgeting-eh8xe: der Satz, ein Wochentag lasse sich in BB nicht setzen, entfällt.
        test "Die Warnung nennt die Verluste" {
            let dated = { targetOf 50m with Date = Some "2027-06-01"; Rhythm = Months 3 }
            let form rhythm = { formOf onTrip with RhythmInput = Some rhythm; DateInput = Some DateField.EmptyDate }
            Expect.equal
                (targetChangeWarningText dated (form Yearly))
                ("Der Tag des Targets geht verloren. Das Datum 01.06.2027 geht verloren. "
                 + "Der Rhythmus „alle 3 Monate“ geht verloren. BB kann ihn nicht wiederherstellen, der Wechsel ist endgültig.")
                "jährlich"
            Expect.equal
                (targetChangeWarningText { targetOf 50m with Kind = MonthlyFunding; Day = None } (form Yearly))
                "Die bisherige Art des Targets geht verloren, BB legt ein Target „Set aside another“ an."
                "MF auf jährlich"
        }
        // budgeting-eh8xe: der Wochentag eines wöchentlichen Targets lässt sich in BB setzen.
        test "Bei wöchentlich steht der Wochentag, eine Wahl geht mit dem Auftrag hinaus" {
            let weekly = { targetOf 25m with Rhythm = Weeks 1; Day = Some 1; NeedsWholeAmount = None }
            let model = onTrip |> withTarget tripId current (Some weekly)
            Expect.isTrue (targetWeeklyAfter (Some weekly) (formOf model)) "wöchentlich"
            Expect.equal (targetWeekdayValue (Some weekly) (formOf model)) (Some 1) "Montag gelesen"
            Expect.equal (targetFormState (Some weekly) (formOf model)) FormUnchanged "unverändert"
            let friday = model |> step (TargetWeekdayChanged 5)
            Expect.equal
                (requestOf friday).Wanted
                (Some { NewGoal = eur 25m; NewDate = None; NewRhythm = KeepRhythm; NewDay = Some 5 })
                "Freitag"
            Expect.isFalse (targetWeeklyAfter (Some (targetOf 50m)) (formOf onTrip)) "monatlich ohne Wochentag"
            let switched = onTrip |> step (TargetRhythmChanged Weekly) |> step (TargetWeekdayChanged 3)
            Expect.equal ((requestOf (switched |> step ConfirmTargetChange)).Wanted |> Option.bind (fun d -> d.NewDay)) (Some 3) "beim Wechsel"
        }
        // Review-Fund eh8xe (Design W1): ein wöchentliches Target ließ sich ohne Wochentag speichern, BB legte es
        // ohne Tag an und konnte den Fehlbetrag nicht rechnen.
        test "GIVEN Wechsel auf wöchentlich ohne Wochentag THEN Speichern gesperrt mit Grund, nichts gesendet, nach der Wahl geht es" {
            let chosen = onTrip |> step (TargetRhythmChanged Weekly)
            let shown, locks, form = tabForm chosen (categoryOf chosen tripId)
            Expect.isFalse (formCanSave shown locks form) "gesperrt"
            Expect.equal (targetWeekdayHint shown form) (Some TargetWeekdayMissingText) "Grund"
            Expect.isFalse (sends SaveTarget chosen) "nichts gesendet"
            Expect.isFalse (formOf (step SaveTarget chosen)).ConfirmingChange "keine Warnung"
            let picked = chosen |> step (TargetWeekdayChanged 2)
            let shown, locks, form = tabForm picked (categoryOf picked tripId)
            Expect.isTrue (formCanSave shown locks form) "mit Wochentag"
            Expect.isNone (targetWeekdayHint shown form) "kein Grund mehr"
        }
        test "GIVEN Anlegen wöchentlich ohne Wochentag THEN nichts gesendet, mit Wochentag schon" {
            let typed = onHolidayWithout current |> step (TargetAmountChanged "25") |> step (TargetRhythmChanged Weekly)
            Expect.isFalse (sends SaveTarget typed) "ohne Tag"
            Expect.equal ((requestOf (typed |> step (TargetWeekdayChanged 0))).Wanted |> Option.bind (fun d -> d.NewDay)) (Some 0) "Sonntag"
        }
        test "Ein wöchentliches Target mit Wochentag braucht keine neue Wahl, ein Betrag allein geht" {
            let weekly = { targetOf 25m with Rhythm = Weeks 1; Day = Some 4; NeedsWholeAmount = None }
            let typed = onTrip |> withTarget tripId current (Some weekly) |> step (TargetAmountChanged "30")
            Expect.isNone (targetWeekdayHint (Some weekly) (formOf typed)) "kein Grund"
            Expect.equal ((requestOf typed).Wanted |> Option.bind (fun d -> d.NewDay)) (Some 4) "Donnerstag geht mit"
        }
    ]

/// Was die Aufzählung der Warnung weiß, unabhängig vom getesteten Code.
type private WarnWalk = {
    Month: string
    /// Ein anderer Rhythmus (jährlich) ist im Formular gewählt.
    Chosen: bool
    /// Betrag oder Datum sind getippt: ohne Wechsel sendet Speichern direkt.
    Typed: bool
    WarnOpen: bool
    Pending: bool
}

let private warnWalkTests =
    let events (w: WarnWalk) =
        [ "Rhythmus"; "Betrag"; "Datum"; "Speichern"; "Bestätigen"; "Abbrechen"; "Monat"; if w.Pending then "Antwort" ]
    let oracle (name: string) (w: WarnWalk) : WarnWalk * bool =
        let editable = w.Month = current
        match name with
        | "Rhythmus" when editable && not w.Pending -> { w with Chosen = true; WarnOpen = false }, false
        // Tippen in Betrag und Datum schließt die Warnung, sie galt der alten Eingabe (budgeting-g2ty9, X10).
        | "Betrag"
        | "Datum" when editable && not w.Pending -> { w with Typed = true; WarnOpen = false }, false
        | "Speichern" when editable && not w.Pending && w.Chosen && not w.WarnOpen -> { w with WarnOpen = true }, false
        | "Speichern" when editable && not w.Pending && w.Typed && not w.WarnOpen -> { w with Pending = true }, true
        | "Bestätigen" when editable && not w.Pending && w.WarnOpen -> { w with WarnOpen = false; Pending = true }, true
        | "Abbrechen" -> { w with WarnOpen = false }, false
        | "Monat" -> { w with Month = (if w.Month = current then next else current); Chosen = false; Typed = false; WarnOpen = false }, false
        | "Antwort" -> { w with Pending = false }, false
        | _ -> w, false
    let apply (name: string) (w: WarnWalk) (model: Model) : Model * Cmd<Msg> =
        let run msg = let m, cmd, _ = update msg model in m, cmd
        match name with
        | "Rhythmus" -> run (TargetRhythmChanged Yearly)
        | "Betrag" -> run (TargetAmountChanged "70")
        | "Datum" -> run (TargetDateChanged (DateField.ValidDate (DateTime(2027, 6, 1))))
        | "Speichern" -> run SaveTarget
        | "Bestätigen" -> run ConfirmTargetChange
        | "Abbrechen" -> run CancelTargetChange
        | "Monat" ->
            let target = if w.Month = current then next else current
            let moved = step (SelectMonth target) model
            step (Loaded (moved.MonthGeneration, Ok (monthWith target 50m))) moved, Cmd.none
        | _ -> run (TargetChangeAnswered (tripId, current, Error "kaputt"))
    let rec walkFrom depth path (w: WarnWalk) (model: Model) =
        if depth > 0 then
            for name in events w do
                let route = List.rev (name :: path) |> String.concat ", "
                let expected, sent = oracle name w
                let model', cmd = apply name w model
                Expect.equal (not (List.isEmpty cmd)) sent $"{route}: gesendet"
                let open' = model'.TargetForm |> Option.exists (fun f -> f.ConfirmingChange)
                Expect.equal open' expected.WarnOpen $"{route}: Warnung offen"
                walkFrom (depth - 1) (name :: path) expected model'
    testList "Tab Target: Reihenfolgen der Warnung" [
        test "In jeder Reihenfolge sendet nur Bestätigen bei offener Warnung, Abbrechen, Tippen und Monatswechsel schließen sie" {
            walkFrom 5 [] { Month = current; Chosen = false; Typed = false; WarnOpen = false; Pending = false } onTrip
        }
    ]

// ============================================
// Korrekturen aus den Reviews (budgeting-g2ty9)
// ============================================

let private reviewTests =
    testList "Tab Target: Knöpfe, Hinweise und Rückfragen" [
        // F1, F2: beim Entfernen drehte der Spinner an "Target speichern", und "Target speichern" setzte beim
        // eigenen Aufruf `disabled`, der Fokus ging verloren.
        test "Beim Speichern dreht nur Target speichern, beim Entfernen nur Target entfernen, der drehende Knopf ist nicht gesperrt" {
            let saving = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            Expect.equal (tabForm saving (categoryOf saving tripId) |||> formSaveButton) { Busy = true; Disabled = false } "Speichern dreht"
            Expect.equal (formRemoveButton (locksOf saving) (formOf saving)) { Busy = false; Disabled = true } "Entfernen gesperrt"
            let removing = onTrip |> step AskRemoveTarget |> step ConfirmRemoveTarget
            Expect.equal removing.TargetCall (TargetSaving (tripId, current, RemovingTarget, FromTargetTab)) "Entfernen läuft"
            Expect.equal (formRemoveButton (locksOf removing) (formOf removing)) { Busy = true; Disabled = false } "Entfernen dreht"
            Expect.equal (tabForm removing (categoryOf removing tripId) |||> formSaveButton) { Busy = false; Disabled = true } "Speichern gesperrt"
            let switching = onTrip |> step (TargetRhythmChanged Weekly) |> step (TargetWeekdayChanged 3) |> step SaveTarget |> step ConfirmTargetChange
            Expect.equal (tabForm switching (categoryOf switching tripId) |||> formSaveButton) { Busy = true; Disabled = false } "Wechseln dreht am Speichern-Knopf"
            Expect.equal (tabForm onTrip (trip 50m) |||> formSaveButton) { Busy = false; Disabled = true } "ohne Änderung gesperrt"
            Expect.equal (formRemoveButton (locksOf onTrip) (formOf onTrip)) { Busy = false; Disabled = false } "Entfernen frei"
        }
        // X3: der Rhythmus wirkte beim Speichern bedienbar.
        test "Solange das Speichern läuft, ist der Rhythmus gesperrt wie Betrag und Datum, eine Wahl ändert nichts" {
            let saving = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            Expect.isTrue (formFieldsLocked (locksOf saving)) "gesperrt"
            Expect.equal (formOf (saving |> step (TargetRhythmChanged Yearly))).RhythmInput None "keine Wahl angenommen"
        }
        // X2: ein stilles Neulesen bei offener Warnung ließ "Wechseln" wirkungslos und Speichern gesperrt.
        test "GIVEN Warnung offen WHEN ein stilles Neulesen zeigt ein Target ohne Verluste THEN schließt die Warnung, Speichern geht" {
            let warned = onTrip |> step (TargetRhythmChanged Yearly) |> step SaveTarget
            Expect.isTrue (formOf warned).ConfirmingChange "Warnung offen"
            let withoutDay =
                { monthWith current 50m with
                    Groups = [ { Name = "Alltag"; Categories = [ { trip 50m with Target = Some { targetOf 50m with Day = None } }; holiday ] } ] }
            let reread = warned |> step (Reloaded (warned.MonthGeneration, Ok withoutDay))
            Expect.isFalse (formOf reread).ConfirmingChange "Warnung zu"
            Expect.isTrue (sends SaveTarget reread) "Speichern sendet ohne Warnung"
            let yearly =
                { monthWith current 50m with
                    Groups = [ { Name = "Alltag"; Categories = [ { trip 50m with Target = Some { targetOf 50m with Rhythm = Years 1 } }; holiday ] } ] }
            let unchanged = warned |> step (Reloaded (warned.MonthGeneration, Ok yearly))
            Expect.isFalse (formOf unchanged).ConfirmingChange "kein Wechsel mehr: Warnung zu"
            let still = warned |> step (Reloaded (warned.MonthGeneration, Ok (monthWith current 50m)))
            Expect.isTrue (formOf still).ConfirmingChange "gleicher Stand: Warnung bleibt"
        }
        // budgeting-c2t9w, Bug: zeigte ein stilles Neulesen die Kategorie ohne Target, blieb "Target entfernen?"
        // offen, "Entfernen" tat nichts, "Target anlegen" und Pause waren gesperrt bis "Abbrechen".
        test "GIVEN Bestätigung des Entfernens offen WHEN ein stilles Neulesen zeigt kein Target THEN schließt sie, Target anlegen geht" {
            let asked = onTrip |> step AskRemoveTarget
            Expect.isTrue (formOf asked).ConfirmingRemove "Bestätigung offen"
            let without =
                { monthWith current 50m with
                    Groups = [ { Name = "Alltag"; Categories = [ { trip 50m with Target = None }; holiday ] } ] }
            let reread = asked |> step (Reloaded (asked.MonthGeneration, Ok without))
            Expect.isFalse (formOf reread).ConfirmingRemove "Bestätigung zu"
            let typed = reread |> step (TargetAmountChanged "40")
            Expect.equal (tabForm typed (categoryOf typed tripId) |||> formSaveButton) { Busy = false; Disabled = false } "Target anlegen bedienbar"
            Expect.isTrue (sends SaveTarget typed) "Target anlegen sendet"
            let still = asked |> step (Reloaded (asked.MonthGeneration, Ok (monthWith current 50m)))
            Expect.isTrue (formOf still).ConfirmingRemove "mit Target: Bestätigung bleibt"
        }
        // X5, F5
        test "Außerhalb des laufenden Monats nennt der Tab den Grund, mit Target ändern, ohne anlegen, nicht während eines Wechsels" {
            for month in [ past; next ] do
                let model = pageIn month
                Expect.equal (targetMonthHint model (categoryOf model tripId)) (Some TargetEditMonthText) $"{month} mit Target"
                let without = onHolidayWithout month
                Expect.equal (targetMonthHint without (categoryOf without holidayId)) (Some TargetCreateMonthText) $"{month} ohne Target"
            Expect.equal (targetMonthHint onTrip (trip 50m)) None "laufender Monat"
            let changing = onTrip |> step (SelectMonth next)
            Expect.equal (targetMonthHint changing (categoryOf changing tripId)) None "Wechsel aus dem laufenden Monat"
            Expect.equal (targetTabMode changing (categoryOf changing tripId)) (TargetReadOnly (targetOf 50m)) "dabei nur Text"
        }
        test "Der Titel der Warnung heißt Art wechseln, wenn die Art verloren geht, sonst Rhythmus wechseln" {
            let form = { formOf onTrip with RhythmInput = Some Yearly }
            Expect.equal (targetChangeTitle { targetOf 50m with Kind = MonthlyFunding } form) "Art wechseln?" "MF"
            Expect.equal (targetChangeTitle (targetOf 50m) form) "Rhythmus wechseln?" "NEED"
        }
        // X8: wie die API-Grenze.
        test "Anlegen mit 0 € ist gesperrt und nennt den Grund unter dem Betrag" {
            let zero = onHolidayWithout current |> step (TargetAmountChanged "0")
            Expect.isFalse (tabForm zero (categoryOf zero holidayId) |||> formCanSave) "gesperrt"
            Expect.equal (targetAmountHint None (formOf zero)) (Some TargetCreateZeroText) "Hinweis"
            Expect.equal (targetAmountHint (Some (targetOf 50m)) (formOf (onTrip |> step (TargetAmountChanged "0")))) None "Ändern auf 0 € geht"
        }
        // X10
        test "Der aktive Rhythmus: herstellbar nur NEED mit Abstand 1, sonst keiner" {
            let form = formOf onTrip
            let need rhythm = Some { targetOf 50m with Rhythm = rhythm }
            Expect.equal (targetRhythmValue (need (Weeks 1)) form) (Some Weekly) "wöchentliches NEED"
            Expect.equal (targetRhythmValue (need (Years 1)) form) (Some Yearly) "jährliches NEED"
            for rhythm in [ Months 3; Years 2; NoRhythm ] do
                Expect.equal (targetRhythmValue (need rhythm) form) None $"{rhythm}"
            for kind in [ MonthlyFunding; TargetBalance ] do
                Expect.equal (targetRhythmValue (Some { targetOf 50m with Kind = kind }) form) None $"{kind}"
            Expect.equal (targetRhythmValue (need (Months 3)) { form with RhythmInput = Some Weekly }) (Some Weekly) "die Wahl gilt"
        }
    ]

/// Was die Aufzählung der Rückfragen weiß, unabhängig vom getesteten Code (budgeting-g2ty9, X1; budgeting-c2t9w).
type private AskWalk = {
    /// Ein anderer Rhythmus ist gewählt, ein Speichern verlöre den Tag.
    Chosen: bool
    /// Ein Betrag ist getippt, der vom geladenen abweicht: ohne Wechsel sendet Speichern direkt.
    Typed: bool
    WarnOpen: bool
    RemoveOpen: bool
    TargetPending: bool
    /// Der laufende Target-Aufruf ist das Entfernen: dann dreht "Target entfernen".
    RemovePending: bool
    PausePending: bool
    Paused: bool
    /// Der offene Tab: Target, Geld oder Historie (budgeting-f8w2n).
    Tab: InspectorTab
}

// X1, Bug: am Handy konnten zwei Rückfragen zugleich offen sein. Stand die Warnung vor dem Wechsel an der
// Stelle des Speichern-Knopfs, blieben "Target entfernen" und "Target pausieren" bedienbar, ein Tipp
// öffnete zusätzlich die Bestätigung des Entfernens.
// budgeting-c2t9w, Bug: Rückfrage im Tab Target öffnen, auf Tab Geld wechseln, "Target pausieren" lief.
// Zurück im Tab Target stand die Rückfrage noch offen, "Wechseln" oder "Entfernen" sendete neben der
// laufenden Pause mit altem `Shown`. Ebenso ging Pausieren, während ein Target-Aufruf lief.
// budgeting-f8w2n: auch die Pfeiltaste in den Tab Historie schließt die Rückfragen; die Knöpfe drehen genau
// beim eigenen Aufruf, und die Sätze zur Sperre stehen genau dann, wenn der Aufruf der Kategorie sperrt.
let private askWalkTests =
    let events (w: AskWalk) =
        [ match w.Tab with
          | TargetTab ->
              yield! [ "Rhythmus"; "Betrag"; "Speichern"; "Wechseln"; "Abbrechen Wechsel"; "Entfernen"; "Bestätigen Entfernen"; "Abbrechen Entfernen"; "Pausieren"; "Tab Geld"; "Tab Verlauf" ]
          | MoneyTab -> yield! [ "Pausieren"; "Tab Target" ]
          | EntriesTab
          | HistoryTab -> "Tab Target"
          if w.TargetPending then "Antwort"
          if w.PausePending then "Pause-Antwort" ]
    let oracle (name: string) (w: AskWalk) : AskWalk * bool =
        let noAsk = not w.WarnOpen && not w.RemoveOpen
        // Höchstens ein Aufruf läuft: solange einer läuft, sendet nichts und öffnet sich keine Rückfrage.
        let idle = not w.TargetPending && not w.PausePending
        match name with
        | "Rhythmus" when not w.TargetPending -> { w with Chosen = true; WarnOpen = false }, false
        // Tippen schließt die Warnung, die Bestätigung des Entfernens bleibt (budgeting-f4k9n, M2).
        | "Betrag" when not w.TargetPending -> { w with Typed = true; WarnOpen = false }, false
        | "Speichern" when idle && noAsk && w.Chosen -> { w with WarnOpen = true }, false
        | "Speichern" when idle && noAsk && w.Typed -> { w with TargetPending = true }, true
        | "Wechseln" when idle && w.WarnOpen -> { w with WarnOpen = false; TargetPending = true }, true
        | "Abbrechen Wechsel" -> { w with WarnOpen = false }, false
        | "Entfernen" when idle && noAsk -> { w with RemoveOpen = true }, false
        | "Bestätigen Entfernen" when idle && w.RemoveOpen -> { w with RemoveOpen = false; TargetPending = true; RemovePending = true }, true
        | "Abbrechen Entfernen" -> { w with RemoveOpen = false }, false
        | "Pausieren" when idle && noAsk -> { w with PausePending = true }, true
        // Weg vom Tab Target schließen die Rückfragen, die Eingabe bleibt.
        | "Tab Geld" -> { w with Tab = MoneyTab; WarnOpen = false; RemoveOpen = false }, false
        | "Tab Verlauf" -> { w with Tab = HistoryTab; WarnOpen = false; RemoveOpen = false }, false
        | "Tab Target" -> { w with Tab = TargetTab }, false
        | "Antwort" -> { w with TargetPending = false; RemovePending = false }, false
        | "Pause-Antwort" -> { w with PausePending = false; Paused = not w.Paused }, false
        | _ -> w, false
    let apply (name: string) (w: AskWalk) (model: Model) : Model * Cmd<Msg> =
        let run msg = let m, cmd, _ = update msg model in m, cmd
        match name with
        | "Rhythmus" -> run (TargetRhythmChanged Yearly)
        | "Betrag" -> run (TargetAmountChanged "60")
        | "Speichern" -> run SaveTarget
        | "Wechseln" -> run ConfirmTargetChange
        | "Abbrechen Wechsel" -> run CancelTargetChange
        | "Entfernen" -> run AskRemoveTarget
        | "Bestätigen Entfernen" -> run ConfirmRemoveTarget
        | "Abbrechen Entfernen" -> run CancelRemoveTarget
        | "Pausieren" -> run (SetTargetPause (if w.Paused then LiftPause else PauseTarget))
        | "Tab Geld" -> run (SelectInspectorTab MoneyTab)
        | "Tab Verlauf" -> run (PageKeyPressed (StepTab Previous))
        | "Tab Target" -> run (SelectInspectorTab TargetTab)
        | "Antwort" -> run (TargetChangeAnswered (tripId, current, Error "kaputt"))
        | _ -> run (TargetPauseAnswered (tripId, current, Ok (if w.Paused then None else Some PausedInBb)))
    let rec walkFrom depth path (w: AskWalk) (model: Model) =
        if depth > 0 then
            for name in events w do
                let route = List.rev (name :: path) |> String.concat ", "
                let expected, sent = oracle name w
                let model', cmd = apply name w model
                // Im Tab Historie liest der Client die Historie: dort zählt kein Befehl als gesendeter Aufruf.
                let reads = w.Tab = HistoryTab || expected.Tab = HistoryTab
                if not reads || sent then
                    Expect.equal (not (List.isEmpty cmd)) sent $"{route}: gesendet"
                Expect.isFalse ((match model'.TargetCall with TargetSaving _ -> true | TargetIdle -> false) && Components.Budget.Pause.pauseBusy model') $"{route}: Target und Pause zugleich"
                let form = formOf model'
                Expect.equal (form.ConfirmingChange, form.ConfirmingRemove) (expected.WarnOpen, expected.RemoveOpen) $"{route}: Rückfragen offen"
                let busy = expected.TargetPending || expected.PausePending
                let asking = expected.WarnOpen || expected.RemoveOpen
                // Speichern geht nur mit einer Eingabe, ohne offene Rückfrage und ohne laufenden Aufruf.
                let saveable = not busy && not asking && (expected.Typed || expected.Chosen)
                Expect.equal (tabForm model' (categoryOf model' tripId) |||> formCanSave) saveable $"{route}: Speichern geht"
                // Der drehende Knopf ist nie gesperrt, jeder andere bei laufendem Aufruf und offener Rückfrage.
                Expect.equal
                    (formRemoveButton (locksOf model') form).Disabled
                    (not expected.RemovePending && (busy || asking))
                    $"{route}: Target entfernen gesperrt"
                Expect.equal
                    (Components.Budget.Pause.pauseButtonState model' tripId).Disabled
                    (not expected.PausePending && (busy || asking))
                    $"{route}: Target pausieren gesperrt"
                Expect.equal (Components.Budget.Pause.pauseButtonState model' tripId).Busy expected.PausePending $"{route}: Target pausieren dreht"
                Expect.equal (formRemoveButton (locksOf model') form).Busy expected.RemovePending $"{route}: Target entfernen dreht"
                let category = categoryOf model' tripId
                Expect.equal
                    (tabForm model' category |||> formSaveButton).Busy
                    (expected.TargetPending && not expected.RemovePending)
                    $"{route}: Target speichern dreht"
                // Die Sätze zur Sperre: nur, wenn der Aufruf der Kategorie den Knopf sperrt.
                Expect.equal
                    (Components.Budget.Pause.pauseWaitHint model' category)
                    (if expected.TargetPending then Some Components.Budget.Pause.PauseWaitsForTargetText else None)
                    $"{route}: Satz unter Target pausieren"
                let byPause = expected.PausePending && not asking
                Expect.equal
                    (tabForm model' category |||> formSaveWaitHint)
                    (if byPause && (expected.Typed || expected.Chosen) then Some TargetWaitsForPauseText else None)
                    $"{route}: Satz unter Target speichern"
                Expect.equal
                    (formRemoveWaitHint (locksOf model'))
                    (if byPause then Some TargetWaitsForPauseText else None)
                    $"{route}: Satz unter Target entfernen"
                walkFrom (depth - 1) (name :: path) expected model'
    testList "Tab Target: Reihenfolgen der Rückfragen" [
        test "In jeder Reihenfolge steht höchstens eine Rückfrage offen und läuft höchstens ein Aufruf, ein Tabwechsel schließt die Rückfragen" {
            walkFrom
                5
                []
                { Chosen = false; Typed = false; WarnOpen = false; RemoveOpen = false; TargetPending = false
                  RemovePending = false; PausePending = false; Paused = false; Tab = TargetTab }
                onTrip
        }
    ]

// ============================================
// Sperre je Kategorie (budgeting-f8w2n, ADR 0080 Punkt 5)
// ============================================

// budgeting-f8w2n, Bug: ein Speichern bei Ausflug sperrte "Target pausieren" bei Urlaub, eine Pause bei
// Ausflug sperrte Speichern und Entfernen bei Urlaub, ohne sichtbaren Grund im Tab.
let private perCategoryTests =
    testList "Tab Target: Sperre je Kategorie" [
        test "GIVEN Speichern bei Ausflug läuft WHEN Urlaub gezeigt THEN Target pausieren bei Urlaub geht, bei Ausflug wartet es mit Satz" {
            let saving = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            Expect.equal saving.TargetCall (TargetSaving (tripId, current, SavingTarget, FromTargetTab)) "Speichern läuft"
            Expect.equal (Components.Budget.Pause.pauseButtonState saving tripId) { Busy = false; Disabled = true } "Ausflug gesperrt"
            Expect.equal
                (Components.Budget.Pause.pauseWaitHint saving (categoryOf saving tripId))
                (Some "Wartet, bis das Target gespeichert ist.")
                "Ausflug nennt den Grund"
            Expect.isFalse (sends (SetTargetPause PauseTarget) saving) "Ausflug sendet nicht"
            let onHoliday = saving |> step (SelectCategory holidayId)
            Expect.equal (Components.Budget.Pause.pauseButtonState onHoliday holidayId) { Busy = false; Disabled = false } "Urlaub frei"
            Expect.isNone (Components.Budget.Pause.pauseWaitHint onHoliday (categoryOf onHoliday holidayId)) "Urlaub ohne Satz"
            let paused, cmd, _ = update (SetTargetPause PauseTarget) onHoliday
            Expect.isNonEmpty cmd "Urlaub sendet"
            Expect.equal (paused.TargetCall, paused.PauseCall) (saving.TargetCall, PauseRunning (holidayId, current, onHoliday.MonthGeneration)) "beide laufen"
        }
        test "GIVEN Pause bei Ausflug läuft WHEN Urlaub gezeigt THEN Speichern und Entfernen bei Urlaub gehen, bei Ausflug warten sie mit Satz" {
            let pausing = onTrip |> step (TargetAmountChanged "60") |> step (SetTargetPause PauseTarget)
            Expect.equal pausing.PauseCall (PauseRunning (tripId, current, onTrip.MonthGeneration)) "Pause läuft"
            Expect.isFalse (tabForm pausing (categoryOf pausing tripId) |||> formCanSave) "Ausflug: Speichern gesperrt"
            Expect.equal (formRemoveButton (locksOf pausing) (formOf pausing)) { Busy = false; Disabled = true } "Ausflug: Entfernen gesperrt"
            Expect.equal (tabForm pausing (categoryOf pausing tripId) |||> formSaveWaitHint) (Some "Wartet, bis die Pause gespeichert ist.") "Satz unter Speichern"
            Expect.equal (formRemoveWaitHint (locksOf pausing)) (Some "Wartet, bis die Pause gespeichert ist.") "Satz unter Entfernen"
            Expect.isFalse (sends SaveTarget pausing) "Ausflug sendet nicht"
            let onHoliday = pausing |> step (SelectCategory holidayId) |> step (TargetAmountChanged "30")
            let holidayNow = categoryOf onHoliday holidayId
            Expect.equal (tabForm onHoliday holidayNow |||> formSaveButton) { Busy = false; Disabled = false } "Urlaub: Speichern frei"
            Expect.equal (formRemoveButton (locksOf onHoliday) (formOf onHoliday)) { Busy = false; Disabled = false } "Urlaub: Entfernen frei"
            Expect.isNone (tabForm onHoliday holidayNow |||> formSaveWaitHint) "Urlaub: kein Satz unter Speichern"
            Expect.isNone (formRemoveWaitHint (locksOf onHoliday)) "Urlaub: kein Satz unter Entfernen"
            Expect.isTrue (formOf (onHoliday |> step AskRemoveTarget)).ConfirmingRemove "Urlaub: Entfernen fragt nach"
            let saved, cmd, _ = update SaveTarget onHoliday
            Expect.isNonEmpty cmd "Urlaub sendet"
            Expect.equal (saved.TargetCall, saved.PauseCall) (TargetSaving (holidayId, current, SavingTarget, FromTargetTab), pausing.PauseCall) "beide laufen"
        }
        // budgeting-n7h3x, Bug: eine Pause bei Ausflug sperrte "Target pausieren" bei Urlaub ohne sichtbaren
        // Grund, der Spinner stand bei Ausflug.
        test "GIVEN Pause bei Ausflug läuft WHEN Urlaub gezeigt THEN dreht der Pause-Knopf von Urlaub nicht, darunter steht die Pause bei Ausflug" {
            let pausing = onTrip |> step (SetTargetPause PauseTarget)
            Expect.isNone (Components.Budget.Pause.pauseWaitHint pausing (categoryOf pausing tripId)) "Ausflug: Spinner, kein Satz"
            let onHoliday = pausing |> step (SelectCategory holidayId)
            Expect.equal (Components.Budget.Pause.pauseButtonState onHoliday holidayId) { Busy = false; Disabled = true } "Urlaub gesperrt, dreht nicht"
            Expect.equal
                (Components.Budget.Pause.pauseWaitHint onHoliday (categoryOf onHoliday holidayId))
                (Some "Wartet, bis die Pause bei Ausflug gespeichert ist.")
                "Urlaub nennt die Pause bei Ausflug"
        }
        // budgeting-n7h3x, Bug: ein Speichern bei Ausflug sperrte Speichern und Entfernen bei Urlaub ohne Grund.
        test "GIVEN Speichern bei Ausflug läuft WHEN Urlaub gezeigt THEN stehen unter Speichern und Entfernen von Urlaub das Target bei Ausflug" {
            let saving = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            Expect.isNone (tabForm saving (categoryOf saving tripId) |||> formSaveWaitHint) "Ausflug: Spinner, kein Satz unter Speichern"
            Expect.isNone (formRemoveWaitHint (locksOf saving)) "Ausflug: kein Satz unter Entfernen"
            let onHoliday = saving |> step (SelectCategory holidayId) |> step (TargetAmountChanged "30")
            let holidayNow = categoryOf onHoliday holidayId
            Expect.equal (tabForm onHoliday holidayNow |||> formSaveButton) { Busy = false; Disabled = true } "Urlaub: Speichern gesperrt"
            Expect.equal (formRemoveButton (locksOf onHoliday) (formOf onHoliday)) { Busy = false; Disabled = true } "Urlaub: Entfernen gesperrt"
            Expect.equal (tabForm onHoliday holidayNow |||> formSaveWaitHint) (Some "Wartet, bis das Target bei Ausflug gespeichert ist.") "Satz unter Speichern"
            Expect.equal (formRemoveWaitHint (locksOf onHoliday)) (Some "Wartet, bis das Target bei Ausflug gespeichert ist.") "Satz unter Entfernen"
            // Ohne Eingabe ist Speichern ohnehin gesperrt: kein Satz.
            let untouched = saving |> step (SelectCategory holidayId)
            Expect.isNone (tabForm untouched (categoryOf untouched holidayId) |||> formSaveWaitHint) "ohne Eingabe kein Satz unter Speichern"
        }
        test "Steht die Kategorie des laufenden Aufrufs nicht im gezeigten Monat, steht der Satz ohne Namen" {
            let elsewhere = Guid.NewGuid()
            let pausing = { onTrip with PauseCall = PauseRunning (elsewhere, current, onTrip.MonthGeneration) }
            Expect.equal
                (Components.Budget.Pause.pauseWaitHint pausing (categoryOf pausing tripId))
                (Some "Wartet, bis die laufende Pause gespeichert ist.")
                "Pause ohne Namen"
            let typed = onTrip |> step (TargetAmountChanged "60")
            let saving = { typed with TargetCall = TargetSaving (elsewhere, current, SavingTarget, FromTargetTab) }
            Expect.equal (tabForm saving (categoryOf saving tripId) |||> formSaveWaitHint) (Some "Wartet, bis das laufende Target gespeichert ist.") "Speichern ohne Namen"
            Expect.equal (formRemoveWaitHint (locksOf saving)) (Some "Wartet, bis das laufende Target gespeichert ist.") "Entfernen ohne Namen"
        }
        test "Unter Target speichern und Target entfernen steht der Satz zur Pause nur, wenn kein Target-Aufruf mitsperrt" {
            let pausing = onTrip |> step (SetTargetPause PauseTarget)
            Expect.isNone (tabForm pausing (categoryOf pausing tripId) |||> formSaveWaitHint) "ohne Änderung gesperrt, nicht wegen der Pause"
            // Pause bei Ausflug, Speichern bei Urlaub, zurück zu Ausflug: auch der Target-Aufruf sperrt.
            let both =
                pausing
                |> step (SelectCategory holidayId)
                |> step (TargetAmountChanged "30")
                |> step SaveTarget
                |> step (SelectCategory tripId)
                |> step (TargetAmountChanged "60")
            Expect.equal (both.TargetCall, both.PauseCall) (TargetSaving (holidayId, current, SavingTarget, FromTargetTab), pausing.PauseCall) "beide laufen"
            // Sperren Pause und Target-Aufruf zugleich, steht nur der Satz zum gleichartigen Aufruf (budgeting-n7h3x).
            Expect.equal (tabForm both (categoryOf both tripId) |||> formSaveWaitHint) (Some "Wartet, bis das Target bei Urlaub gespeichert ist.") "Speichern: der Target-Aufruf hat Vorrang"
            Expect.equal (formRemoveWaitHint (locksOf both)) (Some "Wartet, bis das Target bei Urlaub gespeichert ist.") "Entfernen: der Target-Aufruf hat Vorrang"
        }
        test "Sperren Target-Aufruf derselben und Pause einer anderen Kategorie den Pause-Knopf, steht nur der Satz zur Pause" {
            let both =
                onTrip
                |> step (TargetAmountChanged "60")
                |> step SaveTarget
                |> step (SelectCategory holidayId)
                |> step (SetTargetPause PauseTarget)
                |> step (SelectCategory tripId)
            Expect.equal both.TargetCall (TargetSaving (tripId, current, SavingTarget, FromTargetTab)) "Speichern läuft"
            Expect.isTrue (Components.Budget.Pause.pauseCallFor both holidayId) "Pause bei Urlaub läuft"
            Expect.equal
                (Components.Budget.Pause.pauseWaitHint both (categoryOf both tripId))
                (Some "Wartet, bis die Pause bei Urlaub gespeichert ist.")
                "die Pause hat Vorrang"
        }
    ]

// ============================================
// Datum je Rhythmus (budgeting-d7r4k, ADR 0072 Nachtrag 2026-11-04)
// ============================================

let private june = DateField.ValidDate (DateTime(2027, 6, 1))

let private dateTests =
    testList "Tab Target: Datum je Rhythmus" [
        test "Bei wöchentlich ist kein Datumsfeld vorgesehen, bei monatlich und jährlich schon" {
            let model = onHolidayWithout current
            let form rhythm = { formOf model with RhythmInput = Some rhythm }
            Expect.isTrue (targetDateShown None (formOf model)) "Anlegen monatlich (Vorgabe)"
            Expect.isTrue (targetDateShown None (form Yearly)) "Anlegen jährlich"
            Expect.isFalse (targetDateShown None (form Weekly)) "Anlegen wöchentlich"
            Expect.isFalse (targetDateShown (Some (targetOf 50m)) (form Weekly)) "Wechsel auf wöchentlich"
            let datedMonthly = { targetOf 50m with Date = Some "2027-06-01" }
            Expect.isFalse (targetDateShown (Some datedMonthly) (form Weekly)) "Wechsel von monatlich mit Datum auf wöchentlich"
        }
        test "Ein gelesenes Datum eines wöchentlichen Targets bleibt sichtbar und lässt sich entfernen" {
            let weekly = { targetOf 50m with Rhythm = Weeks 1; Date = Some "2027-06-01"; Day = Some 3 }
            let model = onTrip |> withTarget tripId current (Some weekly)
            Expect.isTrue (targetDateShown (Some weekly) (formOf model)) "sichtbar"
            Expect.isTrue (targetDateShown (Some weekly) { formOf model with RhythmInput = Some Weekly }) "auch mit gewähltem Rhythmus"
            let removed = model |> step (TargetDateChanged DateField.EmptyDate)
            Expect.isTrue (targetDateShown (Some weekly) (formOf removed)) "nach dem Leeren weiter sichtbar"
            Expect.equal
                (requestOf removed).Wanted
                (Some { NewGoal = eur 50m; NewDate = None; NewRhythm = KeepRhythm; NewDay = Some 3 })
                "entfernt"
        }
        test "GIVEN Datum bei monatlich getippt WHEN Wechsel auf wöchentlich THEN das ungespeicherte Datum fällt aus dem Entwurf" {
            let typed = onTrip |> step (TargetDateChanged june)
            let weekly = typed |> step (TargetRhythmChanged Weekly) |> step (TargetWeekdayChanged 3)
            Expect.isNone (formOf weekly).DateInput "Eingabe verworfen"
            Expect.equal (targetDateValue (Some (targetOf 50m)) (formOf (weekly |> step (TargetRhythmChanged Monthly)))) None "zurück auf monatlich ohne Datum"
            let datedMonthly = { targetOf 50m with Date = Some "2027-06-01"; Day = None }
            let readDated = onTrip |> withTarget tripId current (Some datedMonthly) |> step (TargetRhythmChanged Weekly) |> step (TargetWeekdayChanged 3)
            match targetFormState (Some datedMonthly) (formOf readDated) with
            | FormChanged draft -> Expect.equal (draft.NewDate, draft.NewRhythm) (None, SetRhythm Weekly) "gelesenes Datum geht nicht mit"
            | other -> failtest $"Entwurf erwartet: {other}"
            // Ein Datum, das trotzdem ankommt (etwa ein Tipp vor dem Neuzeichnen), geht nie hinaus.
            let late = typed |> step (TargetRhythmChanged Weekly) |> step (TargetWeekdayChanged 3) |> step (TargetDateChanged june)
            match targetFormState (Some (targetOf 50m)) (formOf late) with
            | FormChanged draft -> Expect.isNone draft.NewDate "kein Datum"
            | other -> failtest $"Entwurf erwartet: {other}"
        }
        test "GIVEN Anlegen mit Datum bei monatlich WHEN wöchentlich angelegt THEN der Auftrag trägt kein Datum" {
            let typed = onHolidayWithout current |> step (TargetAmountChanged "50") |> step (TargetDateChanged june)
            Expect.equal (requestOf typed).Wanted (Some { NewGoal = eur 50m; NewDate = Some "2027-06-01"; NewRhythm = SetRhythm Monthly; NewDay = None }) "monatlich mit Datum"
            let weekly = typed |> step (TargetRhythmChanged Weekly) |> step (TargetWeekdayChanged 3)
            let saved, cmd, _ = update SaveTarget weekly
            Expect.isNonEmpty cmd "gesendet"
            Expect.equal saved.TargetCall (TargetSaving (holidayId, current, SavingTarget, FromTargetTab)) "Aufruf läuft"
            Expect.equal (requestOf weekly).Wanted (Some { NewGoal = eur 50m; NewDate = None; NewRhythm = SetRhythm Weekly; NewDay = Some 3 }) "wöchentlich ohne Datum"
            let lateDate = weekly |> step (TargetDateChanged june)
            Expect.equal (requestOf lateDate).Wanted (Some { NewGoal = eur 50m; NewDate = None; NewRhythm = SetRhythm Weekly; NewDay = Some 3 }) "auch ein spätes Datum geht nicht mit"
        }
        test "Das Feld heißt Fällig am bei monatlich und jährlich, sonst Datum" {
            Expect.equal (targetDateLabel (Some Monthly)) "Fällig am" "monatlich"
            Expect.equal (targetDateLabel (Some Yearly)) "Fällig am" "jährlich"
            Expect.equal (targetDateLabel (Some Weekly)) "Datum" "wöchentlich"
            Expect.equal (targetDateLabel None) "Datum" "nicht herstellbarer Rhythmus"
            Expect.equal (targetDateAriaLabel (Some Monthly)) "Fällig am" "aria monatlich"
            Expect.equal (targetDateAriaLabel (Some Weekly)) "Datum des Targets" "aria sonst"
        }
        test "Die lesende Anzeige heißt Fällig am bei einem monatlichen und jährlichen Target, sonst Datum" {
            let dated = { targetOf 50m with Date = Some "2027-06-01" }
            Expect.equal (targetReadDateLabel dated) "Fällig am" "monatlich"
            Expect.equal (targetReadDateLabel { dated with Rhythm = Years 1 }) "Fällig am" "jährlich"
            Expect.equal (targetReadDateLabel { dated with Rhythm = Weeks 1 }) "Datum" "wöchentlich"
            Expect.equal (targetReadDateLabel { dated with Rhythm = Months 3 }) "Datum" "alle 3 Monate bleibt"
            Expect.equal (targetReadDateLabel { dated with Kind = MonthlyFunding }) "Datum" "andere Art bleibt"
        }
        test "Unter dem Feld steht bei monatlich mit Datum, dass das Target danach jeden Monat weiterläuft" {
            let model = onHolidayWithout current
            let form = formOf model
            Expect.isNone (targetDateHint None form) "monatlich ohne Datum"
            let dated = { form with DateInput = Some june }
            Expect.equal (targetDateHint None dated) (Some "Danach läuft das Target jeden Monat weiter.") "monatlich mit Datum"
            Expect.isNone (targetDateHint None { dated with RhythmInput = Some Yearly }) "jährlich"
            Expect.isNone (targetDateHint None { dated with RhythmInput = Some Weekly }) "wöchentlich"
            let readDated = { targetOf 50m with Date = Some "2027-06-01" }
            Expect.isSome (targetDateHint (Some readDated) form) "gelesenes Datum bei monatlich"
            Expect.isNone (targetDateHint (Some readDated) { form with DateInput = Some DateField.EmptyDate }) "Datum geleert"
        }
    ]

// budgeting-k8wq3
[<Tests>]
let tests =
    testList "Target ändern im Client" [ visibilityTests; flowTests; walkTests ]

// budgeting-xp46a, Korrekturen aus budgeting-g2ty9
[<Tests>]
let createAndChangeTests =
    testList "Target anlegen und Art wechseln im Client" [ createTests; changeTests; warnWalkTests; reviewTests; askWalkTests; perCategoryTests; dateTests ]
