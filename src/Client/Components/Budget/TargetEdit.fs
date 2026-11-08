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
let private targetBusy (model: Model) : bool =
    match model.TargetCall with
    | TargetSaving _ -> true
    | TargetIdle -> false

/// Wartet der Tab Target für die Kategorie? Solange irgendein Target-Aufruf läuft (es gibt nur einen) und
/// solange eine Pause derselben Kategorie läuft (`Pause.pauseCallFor`): je Kategorie läuft höchstens ein
/// Aufruf, und keine Rückfrage öffnet sich daneben (budgeting-c2t9w). Eine Pause einer anderen Kategorie
/// sperrt nicht (budgeting-f8w2n).
let private targetWaits (model: Model) (categoryId: System.Guid) : bool =
    targetBusy model || Pause.pauseCallFor model categoryId

/// Was der Aufruf für diese Kategorie in diesem Monat tut, wenn er läuft und von `origin` kam. Dann dreht der
/// Knopf, der ihn ausgelöst hat (budgeting-g2ty9, F1), und nur dieser (budgeting-q4v8n).
let targetActionFrom (origin: TargetOrigin) (model: Model) (categoryId: System.Guid) (month: string) : TargetAction option =
    match model.TargetCall with
    | TargetSaving (c, m, action, o) when c = categoryId && m = month && o = origin -> Some action
    | _ -> None

/// Was der Aufruf aus dem Tab Target für diese Kategorie im dargestellten Monat tut, wenn er läuft.
let targetActionFor (model: Model) (categoryId: System.Guid) : TargetAction option =
    displayedMonth model |> Option.bind (targetActionFrom FromTargetTab model categoryId)

/// Der Name einer Kategorie für die Sätze zur Sperre: aus dem gezeigten Monat, sonst aus der Seite Targets
/// prüfen (budgeting-q4v8n).
let private targetCategoryName (model: Model) (categoryId: System.Guid) : string option =
    match Pause.categoryNameOf model categoryId, model.TargetCheck.Data with
    | Some name, _ -> Some name
    | None, Success data -> data.Categories |> List.tryFind (fun c -> c.CategoryId = categoryId) |> Option.map (fun c -> c.Name)
    | None, _ -> None

/// Der Satz zur Sperre eines Formulars. Läuft ein Target-Aufruf einer anderen Kategorie oder derselben aus
/// einem anderen Formular, nennt der Satz sie und hat Vorrang (budgeting-n7h3x, budgeting-q4v8n). Läuft der
/// eigene, dreht dort der Knopf, ein Satz steht nicht. Sonst steht der Satz zur Pause, wenn eine Pause
/// derselben Kategorie läuft (budgeting-f8w2n).
let private targetWaitHint (origin: TargetOrigin) (model: Model) (categoryId: System.Guid) : string option =
    match model.TargetCall with
    | TargetSaving (c, _, _, o) when c = categoryId && o = origin -> None
    | TargetSaving (c, _, _, _) -> Some (targetWaitsForTargetText (targetCategoryName model c))
    | TargetIdle -> if Pause.pauseCallFor model categoryId then Some TargetWaitsForPauseText else None

/// Die Sperrlage eines Formulars der Kategorie im Monat des Formulars, für den Kern (budgeting-r7k2m). Der
/// Knopf dreht nur bei einem Aufruf aus demselben Formular (`origin`, budgeting-q4v8n): das Panel der Seite
/// Targets prüfen ruft sie mit `TargetCheckData.Month`, auch wenn die Budget-Seite einen anderen Monat zeigt.
let targetFormLocksFor (model: Model) (categoryId: System.Guid) (month: string) (origin: TargetOrigin) : TargetFormLocks =
    { Waits = targetWaits model categoryId
      Running = targetActionFrom origin model categoryId month
      WaitHint = targetWaitHint origin model categoryId }

/// Die Sperrlage des Formulars der Kategorie im Tab Target: `targetFormLocksFor` mit dem dargestellten Monat.
let targetFormLocks (model: Model) (categoryId: System.Guid) : TargetFormLocks =
    match displayedMonth model with
    | Some month -> targetFormLocksFor model categoryId month FromTargetTab
    | None ->
        { Waits = targetWaits model categoryId
          Running = None
          WaitHint = targetWaitHint FromTargetTab model categoryId }

/// Das gezeigte Target und das Formular, wenn der Tab bearbeitbar ist (ändern oder anlegen).
let private editableForm (mode: TargetTabMode) : (Target option * TargetForm) option =
    match mode with
    | TargetEditable (target, form) -> Some (Some target, form)
    | TargetCreatable form -> Some (None, form)
    | NoTargetShown
    | TargetReadOnly _ -> None

let private selectedEditable (model: Model) : (BudgetCategory * Target option * TargetForm) option =
    match model.Selected, model.Month with
    | Some selected, Success month ->
        categoriesOf month
        |> List.tryFind (fun c -> c.Id = selected.CategoryId)
        |> Option.bind (fun c -> editableForm (targetTabMode model c) |> Option.map (fun (shown, form) -> c, shown, form))
    | _ -> None

let private updateForm (change: TargetForm -> TargetForm) (model: Model) : Model =
    { model with TargetForm = model.TargetForm |> Option.map change }

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
                TargetCall = TargetSaving (category.Id, form.Month, send.Action, FromTargetTab)
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
    changeForm (fun locks f -> formChooseRhythm frequency (shownTarget model f.CategoryId) locks f) model

/// Wochentag gewählt (`formChooseWeekday`, budgeting-eh8xe).
let chooseTargetWeekday (day: int) (model: Model) : Model =
    changeForm (fun locks f -> formChooseWeekday day locks f) model

/// "Target speichern" oder "Target anlegen" (`formPlanSave`).
let startTargetSave (model: Model) : Model * (string * TargetChangeRequest) option =
    start formPlanSave model

/// Die Warnung bestätigt (`formConfirmChange`).
let confirmTargetChange (model: Model) : Model * (string * TargetChangeRequest) option =
    start formConfirmChange model

/// Die Warnung abgebrochen: geschlossen, die Eingabe bleibt.
let cancelTargetChange (model: Model) : Model =
    updateForm formCancelChange model

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

/// Die Herkunft des laufenden Aufrufs dieser Kategorie in diesem Monat, `None` ohne (budgeting-q4v8n).
let targetCallOrigin (categoryId: System.Guid) (month: string) (model: Model) : TargetOrigin option =
    match model.TargetCall with
    | TargetSaving (c, m, _, origin) when c = categoryId && m = month -> Some origin
    | _ -> None

/// Gibt den Target-Aufruf frei, wenn er der Aufruf dieser Kategorie in diesem Monat ist.
let private releaseTargetCall (categoryId: System.Guid) (month: string) (model: Model) : Model =
    match targetCallOrigin categoryId month model with
    | Some _ -> { model with TargetCall = TargetIdle }
    | None -> model

/// Ein Übergang auf dem Formular des Tabs dieser Kategorie in diesem Monat, wenn der Aufruf aus dem Tab kam
/// (budgeting-q4v8n): eine Antwort auf "übernehmen" oder das Panel der Seite Targets prüfen lässt die Eingabe
/// im Tab stehen. Andere Formulare bleiben.
let private onOwnForm
    (origin: TargetOrigin option)
    (categoryId: System.Guid)
    (month: string)
    (change: TargetForm -> TargetForm)
    (model: Model)
    : Model =
    if origin <> Some FromTargetTab then model
    else updateForm (fun f -> if f.CategoryId = categoryId && f.Month = month then change f else f) model

/// Nimmt die Antwort ein. Sie wirkt nur auf ihre Kategorie und ihren Monat. Ergebnis: das Modell und ob der
/// Monat neu zu lesen ist. Festgehalten: das neue Target steht, der Monat wird neu gelesen. Inzwischen geändert
/// (budgeting-eh8xe): BBs Stand aus der Antwort steht, die Eingabe bleibt mit dem Hinweis, ein Neulesen braucht
/// es nicht. Ein Fehler lässt Target und Eingabe stehen und zeigt die Meldung im Formular (`formTakeAnswer`).
let applyTargetAnswered
    (categoryId: System.Guid)
    (month: string)
    (result: Result<TargetChangeResult, string>)
    (model: Model)
    : Model * bool =
    let origin = targetCallOrigin categoryId month model
    let answered = releaseTargetCall categoryId month model
    let takeAnswer = onOwnForm origin categoryId month (formTakeAnswer result)
    match result with
    | Ok (TargetWritten target) -> withTarget categoryId month target answered |> takeAnswer, true
    | Ok (TargetChangedMeanwhile target) -> withTarget categoryId month target answered |> takeAnswer, false
    | Error _ -> takeAnswer answered, false

/// Liest der Abbruch den Monat neu? Wenn der Monat des Aufrufs gewählt ist (wie `pauseCallLostRereads`).
let targetCallLostRereads (month: string) (model: Model) : bool =
    shownMonth model = Some month

/// Der Aufruf brach ab (budgeting-eh8xe, Vorbild `applyPauseCallLost`): der Aufruf ist frei, die Eingabe bleibt
/// mit dem Satz aus `targetCallLostText`. Ergebnis: das Modell und ob der Monat still neu zu lesen ist.
let applyTargetCallLost (categoryId: System.Guid) (month: string) (message: string) (model: Model) : Model * bool =
    let rereads = targetCallLostRereads month model
    let origin = targetCallOrigin categoryId month model
    releaseTargetCall categoryId month model
    |> onOwnForm origin categoryId month (formTakeCallLost (targetCallLostText rereads message)),
    rereads

/// Der Toast, wenn eine Ablehnung kam, während ihre Kategorie nicht mehr gezeigt wird.
let targetRejectedAwayText (message: string) : string =
    $"Target nicht gespeichert: {message}"

// ============================================
// Targets nach YNAB (budgeting-e2z7v, Nachtrag ADR 0084 vom 2026-11-08)
// ============================================

/// Was am Target über YNAB steht: gelb "Nicht in YNAB" mit Grund und "Erneut senden", die Lücken als Sätze und
/// "In YNAB nicht gesnoozt" an einem pausierten Target.
type TargetYnabHints = {
    /// Der Grund, wenn die letzte Target-Entscheidung nicht in YNAB ist.
    NotInYnab: string option
    Gaps: string list
    NotSnoozed: bool
}

/// Die Hinweise aus dem Zustand der Kategorie (`BudgetCategory.TargetInYnab`, `Pause`). Rein.
let targetYnabHints (category: BudgetCategory) : TargetYnabHints =
    let state = category.TargetInYnab
    { NotInYnab = state |> Option.bind (fun s -> s.NotInYnab)
      Gaps = state |> Option.map (fun s -> s.Gaps |> List.map ynabGoalGapText) |> Option.defaultValue []
      NotSnoozed = category.Target.IsSome && isPaused category }

/// Läuft "Erneut senden" am Target dieser Kategorie? Dann ist der Knopf gesperrt.
let targetRetryRunning (categoryId: System.Guid) (model: Model) : bool =
    model.TargetYnabRetry = Some categoryId

/// "Erneut senden" am Target. `true` heißt: der Aufrufer muss senden. Läuft schon eins (für irgendeine
/// Kategorie) oder ist die Kategorie im geladenen Monat nicht "Nicht in YNAB", geschieht nichts.
let startTargetRetry (categoryId: System.Guid) (model: Model) : Model * bool =
    let notInYnab =
        match model.Month with
        | Success month ->
            categoriesOf month |> List.exists (fun c -> c.Id = categoryId && (targetYnabHints c).NotInYnab.IsSome)
        | _ -> false
    if model.TargetYnabRetry.IsNone && notInYnab then { model with TargetYnabRetry = Some categoryId }, true
    else model, false

/// Antwort auf "Erneut senden" am Target: gibt den Knopf frei; ein Fehler nennt den Grund als Toast. Der Aufrufer
/// liest den Monat danach still neu, der Hinweis kommt aus dem Server.
let applyTargetRetried (categoryId: System.Guid) (result: Result<unit, string>) (model: Model) : Model * ExternalMsg =
    let released = if model.TargetYnabRetry = Some categoryId then { model with TargetYnabRetry = None } else model
    match result with
    | Ok () -> released, ShowToast ("Target in YNAB geschrieben", ToastSuccess)
    | Error message -> released, ShowToast ($"Target weiter nicht in YNAB: {message}", ToastError)

/// "Targets nach YNAB schreiben" im Menü: schließt das Menü und öffnet die Bestätigung, außer ein Lauf läuft.
let askWriteAllTargets (model: Model) : Model =
    match model.TargetsToYnab with
    | TargetsToYnabRunning -> { model with MenuOpen = false }
    | _ -> { model with MenuOpen = false; TargetsToYnab = TargetsToYnabAsking }

/// Bestätigt: `true` heißt senden. Nur aus der offenen Bestätigung, ein zweiter Tipp sendet nicht noch einmal.
let startWriteAllTargets (model: Model) : Model * bool =
    match model.TargetsToYnab with
    | TargetsToYnabAsking -> { model with TargetsToYnab = TargetsToYnabRunning }, true
    | _ -> model, false

/// Die Antwort des Laufs: der Bericht oder der Fehler. Der Aufrufer liest den Monat still neu.
let applyAllTargetsWritten (result: Result<TargetsToYnabReport, string>) (model: Model) : Model =
    match model.TargetsToYnab, result with
    | TargetsToYnabRunning, Ok report -> { model with TargetsToYnab = TargetsToYnabDone report }
    | TargetsToYnabRunning, Error message -> { model with TargetsToYnab = TargetsToYnabFailed message }
    | _ -> model

/// Bestätigung abbrechen oder den Bericht schließen. Ein laufender Lauf bleibt sichtbar, bis er antwortet.
let closeWriteAllTargets (model: Model) : Model =
    match model.TargetsToYnab with
    | TargetsToYnabRunning -> model
    | _ -> { model with TargetsToYnab = TargetsToYnabClosed }

/// Der Bericht nach Gruppen, wie die Liste ihn zeigt: "Geschrieben", "Mit Abweichung" (mit den Sätzen),
/// "Fehlgeschlagen" (mit Grund), "Nicht versucht" nach einem 429 und die Zahl der schon gleichen.
type TargetsToYnabGroups = {
    Written: string list
    WithGaps: (string * string list) list
    Failed: (string * string) list
    NotAttempted: string list
    Unchanged: int
}

let targetsToYnabGroups (report: TargetsToYnabReport) : TargetsToYnabGroups =
    let pick f = report.Lines |> List.choose f
    { Written = pick (fun l -> match l.Outcome with GoalWritten [] -> Some l.CategoryName | _ -> None)
      WithGaps =
        pick (fun l ->
            match l.Outcome with
            | GoalWritten (_ :: _ as gaps) -> Some (l.CategoryName, gaps |> List.map ynabGoalGapText)
            | _ -> None)
      Failed = pick (fun l -> match l.Outcome with GoalWriteFailed reason -> Some (l.CategoryName, reason) | _ -> None)
      NotAttempted = pick (fun l -> match l.Outcome with GoalNotAttempted -> Some l.CategoryName | _ -> None)
      Unchanged = report.Lines |> List.filter (fun l -> l.Outcome = GoalAlreadyInYnab) |> List.length }
