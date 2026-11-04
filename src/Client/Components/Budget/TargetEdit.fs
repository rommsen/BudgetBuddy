/// Target anlegen, ändern, die Art wechseln und entfernen im Tab Target (budgeting-k8wq3, budgeting-xp46a,
/// ADR 0072, ADR 0080): wann das Formular
/// steht, was es zeigt, wann Speichern geht und wie die Antwort in den geladenen Monat kommt. Alles rein,
/// die View ruft es nur auf.
module Components.Budget.TargetEdit

open Shared.Domain
open Shared.Budgeting
open Types
open Client.DesignSystem
open Components.Budget.Types
open Components.Budget.Month

let private displayedMonth (model: Model) : string option =
    match model.Month with
    | Success month -> Some month.Month
    | _ -> None

/// Was der Tab Target zeigt.
type TargetTabMode =
    | NoTargetShown
    /// Vergangener oder künftiger Monat: Target, Rhythmus und Datum nur als Text.
    | TargetReadOnly of Target
    /// Laufender Monat: das Formular.
    | TargetEditable of Target * TargetForm
    /// Laufender Monat ohne Target: das Formular zum Anlegen (budgeting-xp46a).
    | TargetCreatable of TargetForm

/// Das Formular gilt für die gewählte Kategorie im dargestellten Monat, wenn dieser der laufende ist
/// (`targetEditAllowed`), mit Target zum Ändern, ohne zum Anlegen.
let private formKey (model: Model) : (System.Guid * string) option =
    match model.Selected, model.Month, displayedMonthKind model with
    | Some selected, Success month, Some kind when targetEditAllowed kind && not (monthChanging model) ->
        categoriesOf month
        |> List.tryFind (fun c -> c.Id = selected.CategoryId)
        |> Option.map (fun c -> c.Id, month.Month)
    | _ -> None

let private emptyForm (categoryId: System.Guid) (month: string) : TargetForm =
    { CategoryId = categoryId
      Month = month
      AmountInput = None
      DateInput = None
      ConfirmingRemove = false
      Notice = None
      RhythmInput = None
      ConfirmingChange = false }

/// Was der Tab Target für die Kategorie zeigt. Die Bearbeitbarkeit kommt aus `targetEditAllowed`.
let targetTabMode (model: Model) (category: BudgetCategory) : TargetTabMode =
    match category.Target, model.TargetForm with
    | shown, Some form when form.CategoryId = category.Id && displayedMonth model = Some form.Month ->
        match shown with
        | Some target -> TargetEditable (target, form)
        | None -> TargetCreatable form
    | None, _ -> NoTargetShown
    | Some target, _ -> TargetReadOnly target

/// Der Satz, warum der Tab nur Text zeigt: in einem vergangenen oder künftigen Monat, mit Target "Target
/// ändern geht nur im laufenden Monat.", ohne "Target anlegen geht nur im laufenden Monat." (budgeting-g2ty9,
/// X5). Während eines Monatswechsels aus dem laufenden Monat steht er nicht: dort ginge es (F5).
let targetMonthHint (model: Model) (category: BudgetCategory) : string option =
    match displayedMonthKind model with
    | Some kind when not (targetEditAllowed kind) ->
        Some (if category.Target.IsSome then TargetEditMonthText else TargetCreateMonthText)
    | _ -> None

/// Der Stand, auf dem eine Rechnung im Betragsfeld aufsetzt: der Betrag des Targets, ohne Target 0.
let private baseAmount (shown: Target option) : decimal =
    shown |> Option.map (fun t -> t.Goal.Amount) |> Option.defaultValue 0m

/// Der Text im Betragsfeld: die Eingabe oder der Betrag des Targets, ohne Target leer.
let targetAmountText (shown: Target option) (form: TargetForm) : string =
    match form.AmountInput, shown with
    | Some text, _ -> text
    | None, Some target -> AmountMath.formatSignedAmount target.Goal.Amount
    | None, None -> ""

/// Der Betrag der Eingabe: wie Zugewiesen eine Rechnung, der Stand ist das Target. `None`: ungültig,
/// negativ, oder ohne Target noch nichts getippt.
let targetAmountOf (shown: Target option) (form: TargetForm) : decimal option =
    match form.AmountInput with
    | None -> shown |> Option.map (fun t -> t.Goal.Amount)
    | Some text -> AmountMath.evaluateAmount (baseAmount shown) text |> Option.filter (fun v -> v >= 0m)

/// Das Datum des Felds als `DateTime`, für den Eingangswert von `DateField`.
let targetDateValue (shown: Target option) (form: TargetForm) : System.DateTime option =
    match form.DateInput with
    | Some (DateField.ValidDate d) -> Some d
    | Some DateField.EmptyDate
    | Some (DateField.InvalidDate _) -> None
    | None -> shown |> Option.bind (fun t -> t.Date) |> Option.bind (fun iso -> DateField.parseGermanDate (dateText iso))

/// Der Rhythmus, den BB über `goal_frequency` herstellen kann, im gelesenen Target: nur NEED mit Abstand 1.
let private readFrequency (target: Target) : TargetFrequency option =
    if target.Kind <> Need then None
    else [ Monthly; Weekly; Yearly ] |> List.tryFind (fun f -> rhythmOfFrequency f = target.Rhythm)

/// Der aktive Rhythmus im `Segmented`: die Wahl, sonst der gelesene, beim Anlegen monatlich. `None`: das
/// Target hat einen Rhythmus, den BB nicht herstellen kann, kein Segment ist aktiv.
let targetRhythmValue (shown: Target option) (form: TargetForm) : TargetFrequency option =
    match form.RhythmInput, shown with
    | Some f, _ -> Some f
    | None, Some target -> readFrequency target
    | None, None -> Some Monthly

/// Der Hinweis unter dem Betragsfeld, wenn die Eingabe nicht gilt (budgeting-r3wq8): negativ oder keine
/// gültige Rechnung, beim Anlegen auch 0 € wie an der API-Grenze (budgeting-g2ty9, X8). `None`, wenn sie
/// gilt.
let targetAmountHint (shown: Target option) (form: TargetForm) : string option =
    match form.AmountInput with
    | None -> None
    | Some text ->
        match AmountMath.evaluateAmount (baseAmount shown) text with
        | None -> Some AmountMath.InvalidCalculationText
        | Some value when value < 0m -> Some "Target darf nicht negativ sein"
        | Some 0m when shown.IsNone -> Some TargetCreateZeroText
        | Some _ -> None

/// Ist die Eingabe im Datumsfeld ungültig?
let targetDateInvalid (form: TargetForm) : bool =
    match form.DateInput with
    | Some (DateField.InvalidDate _) -> true
    | _ -> false

/// Der Stand des Formulars gegen das geladene Target.
type TargetFormState =
    | FormInvalid
    | FormUnchanged
    | FormChanged of TargetDraft

/// Ohne Target ist das Formular erst geändert, wenn ein Betrag getippt ist; der Rhythmus ist dann immer
/// gesetzt (`SetRhythm`, Vorgabe monatlich). Mit Target ist ein gewählter Rhythmus gleich dem gelesenen
/// keine Änderung (`targetKindChange`).
let targetFormState (shown: Target option) (form: TargetForm) : TargetFormState =
    let date =
        match form.DateInput with
        | None -> Some (shown |> Option.bind (fun t -> t.Date))
        | Some DateField.EmptyDate -> Some None
        | Some (DateField.ValidDate d) -> Some (DateField.germanToIso (DateField.formatGermanDate d))
        | Some (DateField.InvalidDate _) -> None
    match shown with
    | None ->
        match form.AmountInput, targetAmountOf None form, date with
        | None, _, Some _ -> FormUnchanged
        | Some _, Some amount, Some date when amount > 0m ->
            let rhythm = targetRhythmValue None form |> Option.defaultValue Monthly
            FormChanged { NewGoal = { Amount = amount; Currency = TargetCurrency }; NewDate = date; NewRhythm = SetRhythm rhythm }
        | _ -> FormInvalid
    | Some target ->
        match targetAmountOf shown form, date with
        | Some amount, Some date ->
            let draft =
                { NewGoal = { target.Goal with Amount = amount }
                  NewDate = date
                  NewRhythm = match form.RhythmInput with Some f -> SetRhythm f | None -> KeepRhythm }
            let draft = if (targetKindChange target draft).IsNone then { draft with NewRhythm = KeepRhythm } else draft
            if amount = target.Goal.Amount && date = target.Date && draft.NewRhythm = KeepRhythm then FormUnchanged
            else FormChanged draft
        | _ -> FormInvalid

/// Läuft irgendein Aufruf? Dann sendet ein zweiter Tipp nichts.
let targetBusy (model: Model) : bool =
    match model.TargetCall with
    | TargetSaving _ -> true
    | TargetIdle -> false

/// Wartet der Tab Target? Solange ein Target-Aufruf oder eine Pause läuft (`Pause.pauseBusy`): höchstens ein
/// Aufruf läuft, und keine Rückfrage öffnet sich daneben (budgeting-c2t9w).
let private targetWaits (model: Model) : bool =
    targetBusy model || Pause.pauseBusy model

/// Was der Aufruf für diese Kategorie im dargestellten Monat tut, wenn er läuft. Dann dreht der Knopf, der
/// ihn ausgelöst hat (budgeting-g2ty9, F1).
let targetActionFor (model: Model) (categoryId: System.Guid) : TargetAction option =
    match model.TargetCall, displayedMonth model with
    | TargetSaving (c, m, action), Some shown when c = categoryId && m = shown -> Some action
    | _ -> None

/// Das gezeigte Target und das Formular, wenn der Tab bearbeitbar ist (ändern oder anlegen).
let private editableForm (mode: TargetTabMode) : (Target option * TargetForm) option =
    match mode with
    | TargetEditable (target, form) -> Some (Some target, form)
    | TargetCreatable form -> Some (None, form)
    | NoTargetShown
    | TargetReadOnly _ -> None

/// Steht im Formular eine Rückfrage offen, die Warnung vor dem Wechsel oder die Bestätigung von "Target
/// entfernen"? Dann gibt es keine zweite, und Speichern, Entfernen und Pausieren warten (budgeting-g2ty9, X1).
let confirmOpen (form: TargetForm) : bool =
    form.ConfirmingChange || form.ConfirmingRemove

/// Geht "Target speichern" oder "Target anlegen"? Nur bei Abweichung vom geladenen Stand, wenn kein Aufruf
/// läuft, auch keine Pause (`targetWaits`), und keine Rückfrage offen ist.
let canSaveTarget (model: Model) (category: BudgetCategory) : bool =
    match editableForm (targetTabMode model category) with
    | Some (shown, form) when not (targetWaits model) && not (confirmOpen form) ->
        match targetFormState shown form with
        | FormChanged _ -> true
        | FormInvalid
        | FormUnchanged -> false
    | _ -> false

let private selectedEditable (model: Model) : (BudgetCategory * Target option * TargetForm) option =
    match model.Selected, model.Month with
    | Some selected, Success month ->
        categoriesOf month
        |> List.tryFind (fun c -> c.Id = selected.CategoryId)
        |> Option.bind (fun c -> editableForm (targetTabMode model c) |> Option.map (fun (shown, form) -> c, shown, form))
    | _ -> None

let private updateForm (change: TargetForm -> TargetForm) (model: Model) : Model =
    { model with TargetForm = model.TargetForm |> Option.map change }

/// Sind Betrags- und Datumsfeld gesperrt? Solange das eigene Speichern läuft: die Antwort setzt das
/// Formular zurück, eine nachgetippte Eingabe ginge sonst still verloren (budgeting-r3wq8).
let targetFieldsLocked (model: Model) : bool =
    model.TargetForm |> Option.exists (fun f -> (targetActionFor model f.CategoryId).IsSome)

let private typeInForm (change: TargetForm -> TargetForm) (model: Model) : Model =
    if targetFieldsLocked model then model else updateForm change model

/// Eingabe im Betragsfeld. Eine Meldung von vorher geht weg, eine offene Warnung schließt (sie galt der
/// alten Eingabe). Während des eigenen Speicherns nichts.
let typeTargetAmount (text: string) (model: Model) : Model =
    typeInForm (fun f -> { f with AmountInput = Some text; Notice = None; ConfirmingChange = false }) model

/// Eingabe im Datumsfeld, wie `typeTargetAmount`.
let typeTargetDate (entry: DateField.DateEntry) (model: Model) : Model =
    typeInForm (fun f -> { f with DateInput = Some entry; Notice = None; ConfirmingChange = false }) model

/// Rhythmus im `Segmented` gewählt, wie `typeTargetAmount`.
let chooseTargetRhythm (frequency: TargetFrequency) (model: Model) : Model =
    typeInForm (fun f -> { f with RhythmInput = Some frequency; Notice = None; ConfirmingChange = false }) model

let private start
    (action: TargetAction)
    (wanted: Target option -> TargetForm -> TargetDraft option option)
    (model: Model)
    : Model * (string * TargetChangeRequest) option =
    match selectedEditable model with
    | Some (category, shown, form) when not (targetWaits model) ->
        match wanted shown form with
        | Some draft ->
            let request : TargetChangeRequest = { CategoryId = category.Id; Shown = shown; Wanted = draft }
            { model with
                TargetCall = TargetSaving (category.Id, form.Month, action)
                TargetForm = Some { form with ConfirmingRemove = false; ConfirmingChange = false; Notice = None } },
            Some (form.Month, request)
        | None -> model, None
    | _ -> model, None

/// Was ein Speichern jetzt am gelesenen Target verlöre (`targetChangeLosses`). Leer ohne Target und ohne
/// Wechsel der Art.
let targetFormLosses (shown: Target option) (form: TargetForm) : TargetLoss list =
    match shown, targetFormState shown form with
    | Some target, FormChanged draft -> targetChangeLosses target draft
    | _ -> []

/// Das gezeigte Target der Kategorie im dargestellten Monat.
let private shownTarget (model: Model) (categoryId: System.Guid) : Target option =
    match model.Month with
    | Success month -> categoriesOf month |> List.tryFind (fun c -> c.Id = categoryId) |> Option.bind (fun c -> c.Target)
    | _ -> None

/// Hält das Formular am Schlüssel aus Kategorie und Monat. Wechselt einer, ist die Eingabe verworfen und eine
/// offene Warnung geschlossen; im vergangenen oder künftigen Monat gibt es kein Formular. Verlöre der Wechsel
/// nach einem stillen Neulesen nichts mehr (oder ist er keiner mehr), schließt eine offene Warnung: sonst
/// bliebe "Wechseln" wirkungslos und Speichern gesperrt (budgeting-g2ty9, X2). Zeigt ein Neulesen kein
/// Target mehr, schließt die Bestätigung des Entfernens: sonst bliebe "Entfernen" wirkungslos und "Target
/// anlegen" gesperrt (budgeting-c2t9w). Außerhalb des Tabs Target ist keine Rückfrage offen, die Eingabe
/// bleibt: sonst liefe im Tab Geld eine Pause, und zurück im Tab Target ginge "Wechseln" oder "Entfernen"
/// mit altem Stand hinaus (budgeting-c2t9w).
let syncTargetForm (model: Model) : Model =
    match model.TargetForm, formKey model with
    | Some form, Some (c, m) when form.CategoryId = c && form.Month = m ->
        let shown = shownTarget model c
        let away = model.Tab <> TargetTab
        let warnStale = form.ConfirmingChange && (away || List.isEmpty (targetFormLosses shown form))
        let removeStale = form.ConfirmingRemove && (away || shown.IsNone)
        if warnStale || removeStale then
            { model with
                TargetForm =
                    Some
                        { form with
                            ConfirmingChange = form.ConfirmingChange && not warnStale
                            ConfirmingRemove = form.ConfirmingRemove && not removeStale } }
        else
            model
    | _, Some (c, m) -> { model with TargetForm = Some (emptyForm c m) }
    | _, None -> { model with TargetForm = None }

/// "Target speichern" oder "Target anlegen": ein Aufruf mit dem geladenen Target als `Shown`, nur bei
/// Abweichung. Verlöre der Wechsel der Art etwas, öffnet erst die Warnung, gesendet wird nichts.
let startTargetSave (model: Model) : Model * (string * TargetChangeRequest) option =
    match selectedEditable model with
    | Some (_, shown, form) when not (targetWaits model) && not (confirmOpen form) && not (List.isEmpty (targetFormLosses shown form)) ->
        updateForm (fun f -> { f with ConfirmingChange = true; Notice = None }) model, None
    | _ ->
        start
            SavingTarget
            (fun shown form ->
                if confirmOpen form then None
                else
                    match targetFormState shown form with
                    | FormChanged draft -> Some (Some draft)
                    | FormInvalid
                    | FormUnchanged -> None)
            model

/// Die Warnung bestätigt: der Aufruf mit dem Entwurf, nur solange die Warnung offen ist.
let confirmTargetChange (model: Model) : Model * (string * TargetChangeRequest) option =
    start
        SavingTarget
        (fun shown form ->
            match form.ConfirmingChange, targetFormState shown form with
            | true, FormChanged draft -> Some (Some draft)
            | _ -> None)
        model

/// Die Warnung abgebrochen: geschlossen, die Eingabe bleibt.
let cancelTargetChange (model: Model) : Model =
    updateForm (fun f -> { f with ConfirmingChange = false }) model

/// Ist "Target entfernen" gesperrt? Solange ein Aufruf läuft, auch eine Pause (budgeting-c2t9w), und solange
/// eine Rückfrage offen ist (budgeting-r3wq8): am PC liegt die Bestätigung als Modal darüber, am Handy steht
/// die Warnung vor dem Wechsel an der Stelle des Speichern-Knopfs (budgeting-g2ty9, X1).
let removeTargetLocked (model: Model) (form: TargetForm) : bool =
    targetWaits model || confirmOpen form

/// "Target entfernen": die Bestätigung öffnen. Nur mit Target, nie während eines Aufrufs (auch einer Pause)
/// und nie neben einer offenen Warnung (budgeting-g2ty9, X1).
let askRemoveTarget (model: Model) : Model =
    match selectedEditable model with
    | Some (_, Some _, form) when not (removeTargetLocked model form) ->
        updateForm (fun f -> { f with ConfirmingRemove = true }) model
    | _ -> model

let cancelRemoveTarget (model: Model) : Model =
    updateForm (fun f -> { f with ConfirmingRemove = false }) model

/// Entfernen bestätigt: ein Aufruf ohne Wunsch.
let startTargetRemove (model: Model) : Model * (string * TargetChangeRequest) option =
    start RemovingTarget (fun shown form -> if form.ConfirmingRemove && shown.IsSome then Some None else None) model

/// Wie ein Knopf im Tab Target aussieht: `Busy` dreht und behält den Fokus (`Button.IsBusy`), `Disabled`
/// sperrt (budgeting-g2ty9, F1 und F2).
type TargetButtonState = { Busy: bool; Disabled: bool }

/// "Target speichern" oder "Target anlegen": dreht nur, wenn sein eigener Aufruf läuft (auch nach
/// "Wechseln"), sonst gesperrt, wenn Speichern nicht geht.
let saveButtonState (model: Model) (category: BudgetCategory) : TargetButtonState =
    let busy = targetActionFor model category.Id = Some SavingTarget
    { Busy = busy; Disabled = not busy && not (canSaveTarget model category) }

/// "Target entfernen": dreht nur, wenn das Entfernen läuft, sonst gesperrt nach `removeTargetLocked`.
let removeButtonState (model: Model) (form: TargetForm) : TargetButtonState =
    let busy = targetActionFor model form.CategoryId = Some RemovingTarget
    { Busy = busy; Disabled = not busy && removeTargetLocked model form }

/// Setzt das Target der Kategorie im geladenen Monat, wenn er der Monat des Aufrufs ist.
let withTarget (categoryId: System.Guid) (month: string) (target: Target option) (model: Model) : Model =
    match model.Month with
    | Success shown when shown.Month = month ->
        let set (c: BudgetCategory) = if c.Id = categoryId then { c with Target = target } else c
        let groups = shown.Groups |> List.map (fun g -> { g with Categories = g.Categories |> List.map set })
        { model with Month = Success { shown with Groups = groups } }
    | _ -> model

/// Gilt die Antwort der gezeigten Kategorie im dargestellten Monat?
let targetAnswerShown (categoryId: System.Guid) (month: string) (model: Model) : bool =
    model.Selected |> Option.exists (fun s -> s.CategoryId = categoryId) && displayedMonth model = Some month

/// Sieht man die Antwort im Formular? Nur, wenn sie gezeigt wird und der Tab Target offen ist. Sonst kommt
/// eine Ablehnung als Toast (budgeting-r3wq8).
let targetAnswerInTab (categoryId: System.Guid) (month: string) (model: Model) : bool =
    targetAnswerShown categoryId month model && model.Tab = TargetTab

/// Nimmt die Antwort ein. Sie wirkt nur auf ihre Kategorie und ihren Monat. Ergebnis: das Modell und ob der
/// Monat neu zu lesen ist. Ein Fehler lässt Target und Eingabe stehen und zeigt die Meldung im Formular.
let applyTargetAnswered
    (categoryId: System.Guid)
    (month: string)
    (result: Result<TargetChangeResult, string>)
    (model: Model)
    : Model * bool =
    let call =
        match model.TargetCall with
        | TargetSaving (c, m, _) when c = categoryId && m = month -> TargetIdle
        | other -> other
    let answered = { model with TargetCall = call }
    let ownForm (f: TargetForm) = f.CategoryId = categoryId && f.Month = month
    let resetForm (notice: TargetNotice option) (m: Model) =
        { m with
            TargetForm =
                m.TargetForm
                |> Option.map (fun f -> if ownForm f then { emptyForm f.CategoryId f.Month with Notice = notice } else f) }
    match result with
    | Ok (TargetWritten target) -> withTarget categoryId month target answered |> resetForm None, true
    | Ok (TargetChangedInYnab target) ->
        withTarget categoryId month target answered |> resetForm (Some TargetChangedNotice), true
    | Ok (TargetPartlyWritten (target, message)) ->
        // Schritt 1 steht in YNAB: der gezeigte Stand ist der neue, die Eingabe mit dem Datum bleibt. Ein
        // erneutes Speichern plant dann nur das Datum (ADR 0080 Punkt 6). Den ganzen Satz baut der Server
        // (`targetPartlyText`), der Client zeigt ihn unverändert (budgeting-c2t9w).
        withTarget categoryId month target answered
        |> updateForm (fun f -> if ownForm f then { f with Notice = Some (TargetPartlyNotice message); ConfirmingChange = false } else f),
        true
    | Error message ->
        updateForm (fun f -> if ownForm f then { f with Notice = Some (TargetRejectedNotice message) } else f) answered,
        false

/// Der Toast, wenn eine Ablehnung kam, während ihre Kategorie nicht mehr gezeigt wird.
let targetRejectedAwayText (message: string) : string =
    $"Target nicht gespeichert: {message}"

/// Der Hinweis über dem Formular oder unter dem Speichern-Knopf.
let targetNoticeText (notice: TargetNotice) : string =
    match notice with
    | TargetChangedNotice ->
        "Target wurde inzwischen in YNAB geändert. Hier steht jetzt der Stand aus YNAB, deine Eingabe ist verworfen."
    | TargetRejectedNotice message -> message
    | TargetPartlyNotice message -> message

/// Ein Satz je Verlust für die Warnung vor dem Wechsel der Art.
let targetLossText (shown: Target) (loss: TargetLoss) : string =
    match loss with
    | LosesDay -> "Der Tag des Targets geht verloren."
    | LosesDate ->
        let date = shown.Date |> Option.map dateText |> Option.defaultValue ""
        $"Das Datum {date} geht verloren."
    | LosesKind -> "Die bisherige Art des Targets geht verloren, BB legt ein Target „Set aside another“ an."
    | LosesRhythm ->
        match shown.Rhythm with
        | NoRhythm -> "Ein Target ohne Rhythmus kann BB nicht wiederherstellen, der Wechsel ist endgültig."
        | rhythm ->
            $"Der Rhythmus „{rhythmWordText rhythm}“ geht verloren. BB kann ihn nicht wiederherstellen, der Wechsel ist endgültig."

/// Der Text der Warnung: die Sätze zu den Verlusten, beim Wechsel auf wöchentlich der Hinweis zum Wochentag.
let targetChangeWarningText (shown: Target) (form: TargetForm) : string =
    let losses = targetFormLosses (Some shown) form |> List.map (targetLossText shown)
    let weekday =
        match targetRhythmValue (Some shown) form with
        | Some Weekly -> [ "Ein Wochentag lässt sich danach in BB nicht setzen." ]
        | _ -> []
    losses @ weekday |> String.concat " "

/// Der Titel der Warnung: "Art wechseln?", wenn die bisherige Art verloren geht, sonst "Rhythmus wechseln?"
/// (budgeting-g2ty9, X5).
let targetChangeTitle (shown: Target) (form: TargetForm) : string =
    if List.contains LosesKind (targetFormLosses (Some shown) form) then "Art wechseln?" else "Rhythmus wechseln?"

/// "Auffüllen bis" (F6), wenn YNAB `goal_needs_whole_amount = false` liefert.
let refillText (target: Target) : string option =
    match target.NeedsWholeAmount with
    | Some false -> Some "Auffüllen bis"
    | Some true
    | None -> None
