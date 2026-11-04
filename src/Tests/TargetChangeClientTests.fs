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
                (FormChanged { NewGoal = eur 50m; NewDate = None; NewRhythm = KeepRhythm })
                "leer löscht"
            Expect.equal (targetFormState (Some dated) (form (DateField.ValidDate (DateTime(2027, 6, 1))))) FormUnchanged "gleiches Datum"
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
            Expect.equal running.TargetCall (TargetSaving (tripId, current, SavingTarget)) "läuft"
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
            Expect.equal
                (targetNoticeText TargetChangedNotice)
                "Target wurde inzwischen in YNAB geändert. Hier steht jetzt der Stand aus YNAB, deine Eingabe ist verworfen."
                "Text"
        }
        test "Ein Fehler zeigt die Meldung, die Eingabe bleibt, ein zweites Speichern geht" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            let answered, _, external = update (TargetChangeAnswered (tripId, current, Error "YNAB hat abgelehnt: kaputt")) running
            Expect.equal ((formOf answered).AmountInput, (formOf answered).Notice) (Some "60", Some (TargetRejectedNotice "YNAB hat abgelehnt: kaputt")) "Meldung"
            Expect.equal (categoryOf answered tripId).Target (Some (targetOf 50m)) "Target unverändert"
            Expect.equal external NoOp "kein Toast, die Meldung steht im Tab"
            Expect.isTrue (sends SaveTarget answered) "erneut speichern"
        }
        // budgeting-r3wq8, Bug: während des Speicherns ließen sich Betrag und Datum weiter bearbeiten. Die
        // Antwort setzte das Formular zurück, die nachgetippte Eingabe war still weg.
        test "GIVEN Speichern läuft WHEN getippt wird THEN nehmen die Felder nichts an, danach wieder" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            Expect.isTrue (targetFieldsLocked running) "gesperrt"
            let typed = running |> step (TargetAmountChanged "70") |> step (TargetDateChanged (DateField.ValidDate (DateTime(2027, 6, 1))))
            Expect.equal ((formOf typed).AmountInput, (formOf typed).DateInput) (Some "60", None) "keine Eingabe angenommen"
            let answered = typed |> step (TargetChangeAnswered (tripId, current, Ok (TargetWritten (Some (targetOf 60m)))))
            Expect.isFalse (targetFieldsLocked answered) "wieder frei"
            Expect.equal (formOf (answered |> step (TargetAmountChanged "70"))).AmountInput (Some "70") "Eingabe geht wieder"
        }
        test "Läuft das Speichern einer anderen Kategorie, ist das Formular der gezeigten frei" {
            let away = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget |> step (SelectCategory holidayId)
            Expect.isFalse (targetFieldsLocked away) "frei"
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
            Expect.isTrue (removeTargetLocked asked (formOf asked)) "offen"
            let cancelled = asked |> step CancelRemoveTarget
            Expect.isFalse (removeTargetLocked cancelled (formOf cancelled)) "abgebrochen"
            let saving = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            Expect.isTrue (removeTargetLocked saving (formOf saving)) "Aufruf läuft"
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

let private requestOf (model: Model) : TargetChangeRequest =
    match startTargetSave model with
    | _, Some (_, request) -> request
    | _, None -> failtest "Aufruf erwartet"

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
            Expect.isFalse (canSaveTarget model (categoryOf model holidayId)) "ohne Betrag"
            let typed = model |> step (TargetAmountChanged "50")
            Expect.isTrue (canSaveTarget typed (categoryOf typed holidayId)) "mit Betrag"
            Expect.equal
                (requestOf typed)
                { CategoryId = holidayId; Shown = None; Wanted = Some { NewGoal = eur 50m; NewDate = None; NewRhythm = SetRhythm Monthly } }
                "monatlich"
            let yearly = typed |> step (TargetRhythmChanged Yearly) |> step (TargetDateChanged (DateField.ValidDate (DateTime(2027, 6, 1))))
            Expect.equal
                (requestOf yearly).Wanted
                (Some { NewGoal = eur 50m; NewDate = Some "2027-06-01"; NewRhythm = SetRhythm Yearly })
                "jährlich mit Datum"
            Expect.isTrue (sends SaveTarget yearly) "ohne Warnung gesendet"
        }
    ]

let private changeTests =
    testList "Tab Target: Wechsel der Art" [
        test "Den gelesenen Rhythmus wählen ist keine Änderung" {
            Expect.isFalse (canSaveTarget (onTrip |> step (TargetRhythmChanged Monthly)) (trip 50m)) "monatlich bleibt"
        }
        test "GIVEN Wechsel mit Verlusten WHEN Speichern THEN erst die Warnung, gesendet wird nur nach Bestätigung" {
            let chosen = onTrip |> step (TargetRhythmChanged Weekly)
            let warned, cmd, _ = update SaveTarget chosen
            Expect.isEmpty cmd "Speichern sendet nicht"
            Expect.isTrue (formOf warned).ConfirmingChange "Warnung offen"
            Expect.isFalse (canSaveTarget warned (trip 50m)) "Speichern gesperrt, solange sie offen ist"
            let confirmed, cmd, _ = update ConfirmTargetChange warned
            Expect.isNonEmpty cmd "Bestätigen sendet"
            Expect.equal confirmed.TargetCall (TargetSaving (tripId, current, SavingTarget)) "Aufruf läuft"
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
            Expect.isTrue (sends SaveTarget (model |> step (TargetRhythmChanged Weekly))) "gesendet"
        }
        test "GIVEN Warnung offen WHEN Monatswechsel THEN Warnung zu, nichts gesendet" {
            let warned = onTrip |> step (TargetRhythmChanged Weekly) |> step SaveTarget
            let moved = step (SelectMonth next) warned
            Expect.isNone moved.TargetForm "Formular weg"
            Expect.isFalse (sends ConfirmTargetChange moved) "nichts gesendet"
            let back = moved |> moveTo current 50m
            Expect.isFalse (formOf back).ConfirmingChange "zurück im laufenden Monat ohne Warnung"
            Expect.isFalse (sends ConfirmTargetChange back) "auch dort nichts"
        }
        test "Die Warnung nennt die Verluste und beim Wechsel auf wöchentlich den Wochentag" {
            let dated = { targetOf 50m with Date = Some "2027-06-01"; Rhythm = Months 3 }
            let form rhythm = { formOf onTrip with RhythmInput = Some rhythm; DateInput = Some DateField.EmptyDate }
            Expect.equal
                (targetChangeWarningText dated (form Weekly))
                ("Der Tag des Targets geht verloren. Das Datum 01.06.2027 geht verloren. "
                 + "Der Rhythmus „alle 3 Monate“ geht verloren. BB kann ihn nicht wiederherstellen, der Wechsel ist endgültig. "
                 + "Ein Wochentag lässt sich danach in BB nicht setzen.")
                "wöchentlich"
            Expect.equal
                (targetChangeWarningText { targetOf 50m with Kind = MonthlyFunding; Day = None } (form Yearly))
                "Die bisherige Art des Targets geht verloren, BB legt ein Target „Set aside another“ an."
                "MF auf jährlich"
        }
        test "GIVEN Teilergebnis Datum nicht gesetzt THEN Meldung, Formular mit Datum und neuem Stand, Speichern sendet nur das Datum" {
            let model = onHolidayWithout current
            let typed =
                model
                |> step (TargetAmountChanged "600")
                |> step (TargetRhythmChanged Yearly)
                |> step (TargetDateChanged (DateField.ValidDate (DateTime(2027, 6, 1))))
                |> step SaveTarget
            let after = { Goal = eur 600m; Rhythm = Years 1; Kind = Need; Date = None; NeedsWholeAmount = Some true; Day = None }
            let message = "Target angelegt, Datum 01.06.2027 nicht gesetzt: kaputt"
            let answered, _, external = update (TargetChangeAnswered (holidayId, current, Ok (TargetPartlyWritten (Some after, message)))) typed
            Expect.equal external NoOp "im Tab, kein Toast"
            Expect.equal (categoryOf answered holidayId).Target (Some after) "neuer gezeigter Stand"
            let form = formOf answered
            // budgeting-g2ty9 (X4): ein gelber Hinweis mit Handlungssatz, keine Fehlerkarte.
            Expect.equal form.Notice (Some (TargetPartlyNotice message)) "Hinweis"
            Expect.equal
                (targetNoticeText (TargetPartlyNotice message))
                "Target angelegt, Datum 01.06.2027 nicht gesetzt: kaputt Speichern setzt das Datum erneut."
                "mit Handlungssatz"
            Expect.equal (targetDateValue (Some after) form) (Some (DateTime(2027, 6, 1))) "Datum bleibt"
            Expect.isTrue (canSaveTarget answered (categoryOf answered holidayId)) "Speichern aktiv"
            Expect.equal
                (requestOf answered)
                { CategoryId = holidayId; Shown = Some after; Wanted = Some { NewGoal = eur 600m; NewDate = Some "2027-06-01"; NewRhythm = KeepRhythm } }
                "gegen den neuen Stand, ohne Wechsel"
            Expect.isTrue (sends SaveTarget answered) "ohne Warnung gesendet"
        }
        test "Ein Teilergebnis, während eine andere Kategorie gezeigt wird, kommt als Toast" {
            let running = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget |> step (SelectCategory holidayId)
            let _, _, external = update (TargetChangeAnswered (tripId, current, Ok (TargetPartlyWritten (Some (targetOf 60m), "Datum fehlt")))) running
            Expect.equal external (ShowToast ("Datum fehlt", ToastWarning)) "Toast"
        }
    ]

/// Was die Aufzählung der Warnung weiß, unabhängig vom getesteten Code.
type private WarnWalk = {
    Month: string
    /// Wöchentlich ist im Formular gewählt.
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
        | "Rhythmus" -> run (TargetRhythmChanged Weekly)
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
            Expect.equal (saveButtonState saving (categoryOf saving tripId)) { Busy = true; Disabled = false } "Speichern dreht"
            Expect.equal (removeButtonState saving (formOf saving)) { Busy = false; Disabled = true } "Entfernen gesperrt"
            let removing = onTrip |> step AskRemoveTarget |> step ConfirmRemoveTarget
            Expect.equal removing.TargetCall (TargetSaving (tripId, current, RemovingTarget)) "Entfernen läuft"
            Expect.equal (removeButtonState removing (formOf removing)) { Busy = true; Disabled = false } "Entfernen dreht"
            Expect.equal (saveButtonState removing (categoryOf removing tripId)) { Busy = false; Disabled = true } "Speichern gesperrt"
            let switching = onTrip |> step (TargetRhythmChanged Weekly) |> step SaveTarget |> step ConfirmTargetChange
            Expect.equal (saveButtonState switching (categoryOf switching tripId)) { Busy = true; Disabled = false } "Wechseln dreht am Speichern-Knopf"
            Expect.equal (saveButtonState onTrip (trip 50m)) { Busy = false; Disabled = true } "ohne Änderung gesperrt"
            Expect.equal (removeButtonState onTrip (formOf onTrip)) { Busy = false; Disabled = false } "Entfernen frei"
        }
        // X3: der Rhythmus wirkte beim Speichern bedienbar.
        test "Solange das Speichern läuft, ist der Rhythmus gesperrt wie Betrag und Datum, eine Wahl ändert nichts" {
            let saving = onTrip |> step (TargetAmountChanged "60") |> step SaveTarget
            Expect.isTrue (targetFieldsLocked saving) "gesperrt"
            Expect.equal (formOf (saving |> step (TargetRhythmChanged Yearly))).RhythmInput None "keine Wahl angenommen"
        }
        // X2: ein stilles Neulesen bei offener Warnung ließ "Wechseln" wirkungslos und Speichern gesperrt.
        test "GIVEN Warnung offen WHEN ein stilles Neulesen zeigt ein Target ohne Verluste THEN schließt die Warnung, Speichern geht" {
            let warned = onTrip |> step (TargetRhythmChanged Weekly) |> step SaveTarget
            Expect.isTrue (formOf warned).ConfirmingChange "Warnung offen"
            let withoutDay =
                { monthWith current 50m with
                    Groups = [ { Name = "Alltag"; Categories = [ { trip 50m with Target = Some { targetOf 50m with Day = None } }; holiday ] } ] }
            let reread = warned |> step (Reloaded (warned.MonthGeneration, Ok withoutDay))
            Expect.isFalse (formOf reread).ConfirmingChange "Warnung zu"
            Expect.isTrue (sends SaveTarget reread) "Speichern sendet ohne Warnung"
            let weekly =
                { monthWith current 50m with
                    Groups = [ { Name = "Alltag"; Categories = [ { trip 50m with Target = Some { targetOf 50m with Rhythm = Weeks 1 } }; holiday ] } ] }
            let unchanged = warned |> step (Reloaded (warned.MonthGeneration, Ok weekly))
            Expect.isFalse (formOf unchanged).ConfirmingChange "kein Wechsel mehr: Warnung zu"
            let still = warned |> step (Reloaded (warned.MonthGeneration, Ok (monthWith current 50m)))
            Expect.isTrue (formOf still).ConfirmingChange "gleicher Stand: Warnung bleibt"
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
            Expect.isFalse (canSaveTarget zero (categoryOf zero holidayId)) "gesperrt"
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

/// Was die Aufzählung der Rückfragen weiß, unabhängig vom getesteten Code (budgeting-g2ty9, X1).
type private AskWalk = {
    /// Wöchentlich ist gewählt, ein Speichern verlöre den Tag.
    Chosen: bool
    WarnOpen: bool
    RemoveOpen: bool
    TargetPending: bool
    PausePending: bool
    Paused: bool
}

// X1, Bug: am Handy konnten zwei Rückfragen zugleich offen sein. Stand die Warnung vor dem Wechsel an der
// Stelle des Speichern-Knopfs, blieben "Target entfernen" und "Target pausieren" bedienbar, ein Tipp
// öffnete zusätzlich die Bestätigung des Entfernens.
let private askWalkTests =
    let events (w: AskWalk) =
        [ "Rhythmus"; "Speichern"; "Wechseln"; "Abbrechen Wechsel"; "Entfernen"; "Bestätigen Entfernen"; "Abbrechen Entfernen"; "Pausieren"
          if w.TargetPending then "Antwort"
          if w.PausePending then "Pause-Antwort" ]
    let oracle (name: string) (w: AskWalk) : AskWalk * bool =
        let noAsk = not w.WarnOpen && not w.RemoveOpen
        match name with
        | "Rhythmus" when not w.TargetPending -> { w with Chosen = true; WarnOpen = false }, false
        | "Speichern" when not w.TargetPending && noAsk && w.Chosen -> { w with WarnOpen = true }, false
        | "Wechseln" when not w.TargetPending && w.WarnOpen -> { w with WarnOpen = false; TargetPending = true }, true
        | "Abbrechen Wechsel" -> { w with WarnOpen = false }, false
        | "Entfernen" when not w.TargetPending && noAsk -> { w with RemoveOpen = true }, false
        | "Bestätigen Entfernen" when not w.TargetPending && w.RemoveOpen -> { w with RemoveOpen = false; TargetPending = true }, true
        | "Abbrechen Entfernen" -> { w with RemoveOpen = false }, false
        | "Pausieren" when not w.PausePending && noAsk -> { w with PausePending = true }, true
        | "Antwort" -> { w with TargetPending = false }, false
        | "Pause-Antwort" -> { w with PausePending = false; Paused = not w.Paused }, false
        | _ -> w, false
    let apply (name: string) (w: AskWalk) (model: Model) : Model * Cmd<Msg> =
        let run msg = let m, cmd, _ = update msg model in m, cmd
        match name with
        | "Rhythmus" -> run (TargetRhythmChanged Weekly)
        | "Speichern" -> run SaveTarget
        | "Wechseln" -> run ConfirmTargetChange
        | "Abbrechen Wechsel" -> run CancelTargetChange
        | "Entfernen" -> run AskRemoveTarget
        | "Bestätigen Entfernen" -> run ConfirmRemoveTarget
        | "Abbrechen Entfernen" -> run CancelRemoveTarget
        | "Pausieren" -> run (SetTargetPause (if w.Paused then LiftPause else PauseTarget))
        | "Antwort" -> run (TargetChangeAnswered (tripId, current, Error "kaputt"))
        | _ -> run (TargetPauseAnswered (tripId, current, Ok (if w.Paused then None else Some PausedInBb)))
    let rec walkFrom depth path (w: AskWalk) (model: Model) =
        if depth > 0 then
            for name in events w do
                let route = List.rev (name :: path) |> String.concat ", "
                let expected, sent = oracle name w
                let model', cmd = apply name w model
                Expect.equal (not (List.isEmpty cmd)) sent $"{route}: gesendet"
                let form = formOf model'
                Expect.equal (form.ConfirmingChange, form.ConfirmingRemove) (expected.WarnOpen, expected.RemoveOpen) $"{route}: Rückfragen offen"
                walkFrom (depth - 1) (name :: path) expected model'
    testList "Tab Target: Reihenfolgen der Rückfragen" [
        test "In jeder Reihenfolge steht höchstens eine Rückfrage offen, Speichern, Entfernen und Pausieren warten auf sie" {
            walkFrom 5 [] { Chosen = false; WarnOpen = false; RemoveOpen = false; TargetPending = false; PausePending = false; Paused = false } onTrip
        }
    ]

// budgeting-k8wq3
[<Tests>]
let tests =
    testList "Target ändern im Client" [ visibilityTests; flowTests; walkTests ]

// budgeting-xp46a, Korrekturen aus budgeting-g2ty9
[<Tests>]
let createAndChangeTests =
    testList "Target anlegen und Art wechseln im Client" [ createTests; changeTests; warnWalkTests; reviewTests; askWalkTests ]
