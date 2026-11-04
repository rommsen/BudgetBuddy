/// Target ändern und entfernen im Tab Target (budgeting-k8wq3, ADR 0072, ADR 0080): wann das Formular
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

/// Das Formular gilt für die gewählte Kategorie im dargestellten Monat, wenn dieser der laufende ist
/// (`targetEditAllowed`) und die Kategorie ein Target hat.
let private formKey (model: Model) : (System.Guid * string) option =
    match model.Selected, model.Month, displayedMonthKind model with
    | Some selected, Success month, Some kind when targetEditAllowed kind && not (monthChanging model) ->
        categoriesOf month
        |> List.tryFind (fun c -> c.Id = selected.CategoryId && c.Target.IsSome)
        |> Option.map (fun c -> c.Id, month.Month)
    | _ -> None

let private emptyForm (categoryId: System.Guid) (month: string) : TargetForm =
    { CategoryId = categoryId
      Month = month
      AmountInput = None
      DateInput = None
      ConfirmingRemove = false
      Notice = None }

/// Hält das Formular am Schlüssel aus Kategorie und Monat. Wechselt einer, ist die Eingabe verworfen; im
/// vergangenen oder künftigen Monat und ohne Target gibt es kein Formular.
let syncTargetForm (model: Model) : Model =
    match model.TargetForm, formKey model with
    | Some form, Some (c, m) when form.CategoryId = c && form.Month = m -> model
    | _, Some (c, m) -> { model with TargetForm = Some (emptyForm c m) }
    | _, None -> { model with TargetForm = None }

/// Was der Tab Target für die Kategorie zeigt. Die Bearbeitbarkeit kommt aus `targetEditAllowed`.
let targetTabMode (model: Model) (category: BudgetCategory) : TargetTabMode =
    match category.Target, model.TargetForm with
    | None, _ -> NoTargetShown
    | Some target, Some form when form.CategoryId = category.Id && displayedMonth model = Some form.Month ->
        TargetEditable (target, form)
    | Some target, _ -> TargetReadOnly target

/// Der Text im Betragsfeld: die Eingabe oder der Betrag des Targets.
let targetAmountText (target: Target) (form: TargetForm) : string =
    form.AmountInput |> Option.defaultValue (AmountMath.formatSignedAmount target.Goal.Amount)

/// Der Betrag der Eingabe: wie Zugewiesen eine Rechnung, der Stand ist das Target. `None`: ungültig oder
/// negativ.
let targetAmountOf (target: Target) (form: TargetForm) : decimal option =
    match form.AmountInput with
    | None -> Some target.Goal.Amount
    | Some text -> AmountMath.evaluateAmount target.Goal.Amount text |> Option.filter (fun v -> v >= 0m)

/// Das Datum des Felds als `DateTime`, für den Eingangswert von `DateField`.
let targetDateValue (target: Target) (form: TargetForm) : System.DateTime option =
    match form.DateInput with
    | Some (DateField.ValidDate d) -> Some d
    | Some DateField.EmptyDate
    | Some (DateField.InvalidDate _) -> None
    | None -> target.Date |> Option.bind (fun iso -> DateField.parseGermanDate (dateText iso))

/// Der Hinweis unter dem Betragsfeld, wenn die Eingabe nicht gilt (budgeting-r3wq8): negativ oder keine
/// gültige Rechnung. `None`, wenn sie gilt.
let targetAmountHint (target: Target) (form: TargetForm) : string option =
    match form.AmountInput with
    | None -> None
    | Some text ->
        match AmountMath.evaluateAmount target.Goal.Amount text with
        | None -> Some AmountMath.InvalidCalculationText
        | Some value when value < 0m -> Some "Target darf nicht negativ sein"
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

let targetFormState (target: Target) (form: TargetForm) : TargetFormState =
    let date =
        match form.DateInput with
        | None -> Some target.Date
        | Some DateField.EmptyDate -> Some None
        | Some (DateField.ValidDate d) -> Some (DateField.germanToIso (DateField.formatGermanDate d))
        | Some (DateField.InvalidDate _) -> None
    match targetAmountOf target form, date with
    | Some amount, Some date ->
        if amount = target.Goal.Amount && date = target.Date then FormUnchanged
        else FormChanged { NewGoal = { target.Goal with Amount = amount }; NewDate = date; NewRhythm = KeepRhythm }
    | _ -> FormInvalid

/// Läuft irgendein Aufruf? Dann sendet ein zweiter Tipp nichts.
let targetBusy (model: Model) : bool =
    match model.TargetCall with
    | TargetSaving _ -> true
    | TargetIdle -> false

/// Läuft der Aufruf für diese Kategorie im dargestellten Monat? Dann dreht der Knopf.
let targetSavingFor (model: Model) (categoryId: System.Guid) : bool =
    match model.TargetCall, displayedMonth model with
    | TargetSaving (c, m), Some shown -> c = categoryId && m = shown
    | _ -> false

/// Geht "Target speichern"? Nur bei Abweichung vom geladenen Stand und wenn kein Aufruf läuft.
let canSaveTarget (model: Model) (category: BudgetCategory) : bool =
    match targetTabMode model category with
    | TargetEditable (target, form) when not (targetBusy model) ->
        match targetFormState target form with
        | FormChanged _ -> true
        | FormInvalid
        | FormUnchanged -> false
    | _ -> false

let private selectedEditable (model: Model) : (BudgetCategory * Target * TargetForm) option =
    match model.Selected, model.Month with
    | Some selected, Success month ->
        categoriesOf month
        |> List.tryFind (fun c -> c.Id = selected.CategoryId)
        |> Option.bind (fun c ->
            match targetTabMode model c with
            | TargetEditable (target, form) -> Some (c, target, form)
            | NoTargetShown
            | TargetReadOnly _ -> None)
    | _ -> None

let private updateForm (change: TargetForm -> TargetForm) (model: Model) : Model =
    { model with TargetForm = model.TargetForm |> Option.map change }

/// Sind Betrags- und Datumsfeld gesperrt? Solange das eigene Speichern läuft: die Antwort setzt das
/// Formular zurück, eine nachgetippte Eingabe ginge sonst still verloren (budgeting-r3wq8).
let targetFieldsLocked (model: Model) : bool =
    model.TargetForm |> Option.exists (fun f -> targetSavingFor model f.CategoryId)

let private typeInForm (change: TargetForm -> TargetForm) (model: Model) : Model =
    if targetFieldsLocked model then model else updateForm change model

/// Eingabe im Betragsfeld. Eine Meldung von vorher geht weg. Während des eigenen Speicherns nichts.
let typeTargetAmount (text: string) (model: Model) : Model =
    typeInForm (fun f -> { f with AmountInput = Some text; Notice = None }) model

/// Eingabe im Datumsfeld, wie `typeTargetAmount`.
let typeTargetDate (entry: DateField.DateEntry) (model: Model) : Model =
    typeInForm (fun f -> { f with DateInput = Some entry; Notice = None }) model

let private start (wanted: Target -> TargetForm -> TargetDraft option option) (model: Model) : Model * (string * TargetChangeRequest) option =
    match selectedEditable model with
    | Some (category, target, form) when not (targetBusy model) ->
        match wanted target form with
        | Some draft ->
            let request : TargetChangeRequest = { CategoryId = category.Id; Shown = Some target; Wanted = draft }
            { model with
                TargetCall = TargetSaving (category.Id, form.Month)
                TargetForm = Some { form with ConfirmingRemove = false; Notice = None } },
            Some (form.Month, request)
        | None -> model, None
    | _ -> model, None

/// "Target speichern": ein Aufruf mit dem geladenen Target als `Shown`, nur bei Abweichung.
let startTargetSave (model: Model) : Model * (string * TargetChangeRequest) option =
    start
        (fun target form ->
            match targetFormState target form with
            | FormChanged draft -> Some (Some draft)
            | FormInvalid
            | FormUnchanged -> None)
        model

/// Ist "Target entfernen" gesperrt? Solange ein Aufruf läuft und solange die Bestätigung offen ist
/// (budgeting-r3wq8): am PC liegt sie als Modal darüber.
let removeTargetLocked (model: Model) (form: TargetForm) : bool =
    targetBusy model || form.ConfirmingRemove

/// "Target entfernen": die Bestätigung öffnen.
let askRemoveTarget (model: Model) : Model =
    match selectedEditable model with
    | Some _ -> updateForm (fun f -> { f with ConfirmingRemove = true }) model
    | None -> model

let cancelRemoveTarget (model: Model) : Model =
    updateForm (fun f -> { f with ConfirmingRemove = false }) model

/// Entfernen bestätigt: ein Aufruf ohne Wunsch.
let startTargetRemove (model: Model) : Model * (string * TargetChangeRequest) option =
    start (fun _ form -> if form.ConfirmingRemove then Some None else None) model

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
        | TargetSaving (c, m) when c = categoryId && m = month -> TargetIdle
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
    | Error message ->
        updateForm (fun f -> if ownForm f then { f with Notice = Some (TargetRejectedNotice message) } else f) answered,
        false

/// Der Toast, wenn eine Ablehnung kam, während ihre Kategorie nicht mehr gezeigt wird.
let targetRejectedAwayText (message: string) : string =
    $"Target nicht gespeichert: {message}"

/// Der Hinweis über dem Formular.
let targetNoticeText (notice: TargetNotice) : string =
    match notice with
    | TargetChangedNotice ->
        "Target wurde inzwischen in YNAB geändert. Hier steht jetzt der Stand aus YNAB, deine Eingabe ist verworfen."
    | TargetRejectedNotice message -> message

/// "Auffüllen bis" (F6), wenn YNAB `goal_needs_whole_amount = false` liefert.
let refillText (target: Target) : string option =
    match target.NeedsWholeAmount with
    | Some false -> Some "Auffüllen bis"
    | Some true
    | None -> None
