/// Target anlegen, ändern, die Art wechseln und entfernen im Tab Target (budgeting-k8wq3, budgeting-xp46a,
/// ADR 0072, ADR 0080): wann das Formular steht, welche Sperren gelten und wie die Antwort in den geladenen
/// Monat kommt. Die Übergänge des Formulars liegen im Kern `TargetFormCore` (budgeting-r7k2m), hier steht nur
/// die Anbindung an das Modell der Budget-Seite. Alles rein, die View ruft es nur auf.
module Components.Budget.TargetEdit

open Shared.Domain
open Shared.Budgeting
open Types
open Client.DesignSystem
open Components.Budget.TargetFormCore
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

/// Läuft irgendein Aufruf? Dann sendet ein zweiter Tipp nichts.
let targetBusy (model: Model) : bool =
    match model.TargetCall with
    | TargetSaving _ -> true
    | TargetIdle -> false

/// Wartet der Tab Target für die Kategorie? Solange irgendein Target-Aufruf läuft (es gibt nur einen) und
/// solange eine Pause derselben Kategorie läuft (`Pause.pauseCallFor`): je Kategorie läuft höchstens ein
/// Aufruf, und keine Rückfrage öffnet sich daneben (budgeting-c2t9w). Eine Pause einer anderen Kategorie
/// sperrt nicht (budgeting-f8w2n).
let private targetWaits (model: Model) (categoryId: System.Guid) : bool =
    targetBusy model || Pause.pauseCallFor model categoryId

/// Was der Aufruf für diese Kategorie im dargestellten Monat tut, wenn er läuft. Dann dreht der Knopf, der
/// ihn ausgelöst hat (budgeting-g2ty9, F1).
let targetActionFor (model: Model) (categoryId: System.Guid) : TargetAction option =
    match model.TargetCall, displayedMonth model with
    | TargetSaving (c, m, action), Some shown when c = categoryId && m = shown -> Some action
    | _ -> None

/// Der Satz zur Sperre im Tab Target. Läuft ein Target-Aufruf einer anderen Kategorie, nennt der Satz sie
/// und hat Vorrang (budgeting-n7h3x). Läuft einer derselben Kategorie, dreht dort der Knopf, ein Satz
/// steht nicht. Sonst steht der Satz zur Pause, wenn eine Pause derselben Kategorie läuft (budgeting-f8w2n).
let private targetWaitHint (model: Model) (categoryId: System.Guid) : string option =
    match model.TargetCall with
    | TargetSaving (c, _, _) when c <> categoryId -> Some (targetWaitsForTargetText (Pause.categoryNameOf model c))
    | TargetSaving _ -> None
    | TargetIdle -> if Pause.pauseCallFor model categoryId then Some TargetWaitsForPauseText else None

/// Die Sperrlage des Formulars der Kategorie im Tab Target, für den Kern (budgeting-r7k2m).
let targetFormLocks (model: Model) (categoryId: System.Guid) : TargetFormLocks =
    { Waits = targetWaits model categoryId
      Running = targetActionFor model categoryId
      WaitHint = targetWaitHint model categoryId }

/// Das gezeigte Target und das Formular, wenn der Tab bearbeitbar ist (ändern oder anlegen).
let private editableForm (mode: TargetTabMode) : (Target option * TargetForm) option =
    match mode with
    | TargetEditable (target, form) -> Some (Some target, form)
    | TargetCreatable form -> Some (None, form)
    | NoTargetShown
    | TargetReadOnly _ -> None

/// Geht "Target speichern" oder "Target anlegen"? Nur bei Abweichung vom geladenen Stand, wenn kein Aufruf
/// läuft, auch keine Pause derselben Kategorie (`targetWaits`), und keine Rückfrage offen ist.
let canSaveTarget (model: Model) (category: BudgetCategory) : bool =
    match editableForm (targetTabMode model category) with
    | Some (shown, form) -> formCanSave shown (targetFormLocks model category.Id) form
    | None -> false

let private selectedEditable (model: Model) : (BudgetCategory * Target option * TargetForm) option =
    match model.Selected, model.Month with
    | Some selected, Success month ->
        categoriesOf month
        |> List.tryFind (fun c -> c.Id = selected.CategoryId)
        |> Option.bind (fun c -> editableForm (targetTabMode model c) |> Option.map (fun (shown, form) -> c, shown, form))
    | _ -> None

let private updateForm (change: TargetForm -> TargetForm) (model: Model) : Model =
    { model with TargetForm = model.TargetForm |> Option.map change }

/// Sind Betrags- und Datumsfeld gesperrt? Solange das eigene Speichern läuft (`formFieldsLocked`).
let targetFieldsLocked (model: Model) : bool =
    model.TargetForm |> Option.exists (fun f -> formFieldsLocked (targetFormLocks model f.CategoryId))

/// Das gezeigte Target der Kategorie im dargestellten Monat.
let private shownTarget (model: Model) (categoryId: System.Guid) : Target option =
    match model.Month with
    | Success month -> categoriesOf month |> List.tryFind (fun c -> c.Id = categoryId) |> Option.bind (fun c -> c.Target)
    | _ -> None

/// Ein Übergang des Kerns auf dem Formular im Modell, mit der Sperrlage seiner Kategorie.
let private changeForm (change: TargetFormLocks -> TargetForm -> TargetForm) (model: Model) : Model =
    updateForm (fun f -> change (targetFormLocks model f.CategoryId) f) model

/// Eingabe im Betragsfeld (`formTypeAmount`).
let typeTargetAmount (text: string) (model: Model) : Model =
    changeForm (formTypeAmount text) model

/// Eingabe im Datumsfeld (`formTypeDate`).
let typeTargetDate (entry: DateField.DateEntry) (model: Model) : Model =
    changeForm (formTypeDate entry) model

/// Ein Übergang des Kerns, der einen Aufruf auslösen kann: nur im bearbeitbaren Tab. Startet er einen, läuft
/// der Aufruf für Kategorie und Monat des Formulars.
let private start
    (step: Target option -> TargetFormLocks -> TargetForm -> TargetForm * TargetFormSend option)
    (model: Model)
    : Model * (string * TargetChangeRequest) option =
    match selectedEditable model with
    | Some (category, shown, form) ->
        match step shown (targetFormLocks model category.Id) form with
        | form', Some send ->
            { model with
                TargetCall = TargetSaving (category.Id, form.Month, send.Action)
                TargetForm = Some form' },
            Some (form.Month, send.Request)
        | form', None -> { model with TargetForm = Some form' }, None
    | None -> model, None

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
    | _, Some (c, m) -> { model with TargetForm = Some (emptyTargetForm c m) }
    | _, None -> { model with TargetForm = None }

/// Rhythmus im `Segmented` gewählt (`formChooseRhythm`).
let chooseTargetRhythm (frequency: TargetFrequency) (model: Model) : Model =
    updateForm (fun f -> formChooseRhythm frequency (shownTarget model f.CategoryId) (targetFormLocks model f.CategoryId) f) model

/// "Target speichern" oder "Target anlegen" (`formPlanSave`).
let startTargetSave (model: Model) : Model * (string * TargetChangeRequest) option =
    start formPlanSave model

/// Die Warnung bestätigt (`formConfirmChange`).
let confirmTargetChange (model: Model) : Model * (string * TargetChangeRequest) option =
    start formConfirmChange model

/// Die Warnung abgebrochen: geschlossen, die Eingabe bleibt.
let cancelTargetChange (model: Model) : Model =
    updateForm formCancelChange model

/// Ist "Target entfernen" gesperrt? Solange ein Aufruf läuft, auch eine Pause derselben Kategorie
/// (budgeting-c2t9w, budgeting-f8w2n), und solange eine Rückfrage offen ist (`formRemoveLocked`).
let removeTargetLocked (model: Model) (form: TargetForm) : bool =
    formRemoveLocked (targetFormLocks model form.CategoryId) form

/// "Target entfernen": die Bestätigung öffnen (`formAskRemove`).
let askRemoveTarget (model: Model) : Model =
    match selectedEditable model with
    | Some (category, shown, form) -> { model with TargetForm = Some (formAskRemove shown (targetFormLocks model category.Id) form) }
    | None -> model

let cancelRemoveTarget (model: Model) : Model =
    updateForm formCancelRemove model

/// Entfernen bestätigt: ein Aufruf ohne Wunsch (`formConfirmRemove`).
let startTargetRemove (model: Model) : Model * (string * TargetChangeRequest) option =
    start formConfirmRemove model

/// "Target speichern" oder "Target anlegen" im Tab (`formSaveButton`).
let saveButtonState (model: Model) (category: BudgetCategory) : TargetButtonState =
    let locks = targetFormLocks model category.Id
    match editableForm (targetTabMode model category) with
    | Some (shown, form) -> formSaveButton shown locks form
    | None ->
        let busy = locks.Running = Some SavingTarget
        { Busy = busy; Disabled = not busy }

/// "Target entfernen" im Tab (`formRemoveButton`).
let removeButtonState (model: Model) (form: TargetForm) : TargetButtonState =
    formRemoveButton (targetFormLocks model form.CategoryId) form

/// Steht unter "Target speichern" oder "Target anlegen", warum es wartet (`formSaveWaitHint`)?
let saveWaitHint (model: Model) (category: BudgetCategory) : string option =
    match editableForm (targetTabMode model category) with
    | Some (shown, form) -> formSaveWaitHint shown (targetFormLocks model category.Id) form
    | None -> None

/// Steht unter "Target entfernen", warum es wartet (`formRemoveWaitHint`)?
let removeWaitHint (model: Model) (form: TargetForm) : string option =
    formRemoveWaitHint (targetFormLocks model form.CategoryId)

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
/// Monat neu zu lesen ist. Ein Fehler lässt Target und Eingabe stehen und zeigt die Meldung im Formular
/// (`formTakeAnswer`).
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
    let takeAnswer (m: Model) = updateForm (fun f -> if ownForm f then formTakeAnswer result f else f) m
    match result with
    | Ok (TargetWritten target)
    | Ok (TargetChangedInYnab target)
    | Ok (TargetPartlyWritten (target, _)) -> withTarget categoryId month target answered |> takeAnswer, true
    | Error _ -> takeAnswer answered, false

/// Der Toast, wenn eine Ablehnung kam, während ihre Kategorie nicht mehr gezeigt wird.
let targetRejectedAwayText (message: string) : string =
    $"Target nicht gespeichert: {message}"
