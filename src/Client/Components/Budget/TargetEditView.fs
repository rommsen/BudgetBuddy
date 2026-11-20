/// Der Tab Target: Formular im laufenden Monat, zum Ändern oder ohne Target zum Anlegen, sonst nur Text
/// (budgeting-k8wq3, budgeting-xp46a, ADR 0080). Was erscheint,
/// sagt `targetTabMode`, die View ruft es nur auf. Das Formular selbst zeichnet `TargetFormView`
/// (budgeting-r7k2m).
module Components.Budget.TargetEditView

open Feliz
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.TargetFormCore
open Components.Budget.TargetEdit
open Components.Budget.TargetFormView
open Client.DesignSystem
open Client.DesignSystem.Tokens

/// Der Satz, warum der Tab nur Text zeigt (`targetMonthHint`).
let private monthHint (hint: string option) =
    match hint with
    | Some text ->
        Html.p [
            prop.custom ("data-testid", "target-edit-month")
            prop.className ListRoles.muted
            prop.text text
        ]
    | None -> Html.none

/// Vergangener oder künftiger Monat: Target, Rhythmus und Datum nur als Text, kein Formular. Darunter der
/// Satz, warum (budgeting-r3wq8), nicht während eines Monatswechsels aus dem laufenden (budgeting-g2ty9, F5).
let readOnlyTarget (model: Model) (category: BudgetCategory) (target: Target) =
    React.fragment [
        // Nach gelungenem „Erneut senden“ leuchtet der Wert kurz (budgeting-sx9v5).
        Html.div [
            prop.key $"target-goal-{model.TargetFlash.Count}"
            prop.custom ("data-flash", model.TargetFlash.On)
            prop.className ("-mx-1 rounded-md px-1 " + (if model.TargetFlash.On then Feedback.flash else ""))
            prop.children [ figure "target-goal" "Target" (targetText target) ]
        ]
        match target.Date with
        | Some date -> figure "target-date-text" (targetReadDateLabel target) (dateText date)
        | None -> ()
        refillFact target
        monthHint (targetMonthHint model category)
    ]

/// Ohne Target außerhalb des laufenden Monats: "Kein Target" und der Satz, warum hier nicht angelegt wird
/// (budgeting-g2ty9, X5).
let noTarget (model: Model) (category: BudgetCategory) =
    React.fragment [
        Html.p [ prop.className ListRoles.muted; prop.text "Kein Target" ]
        monthHint (targetMonthHint model category)
    ]

/// Die Props des Formulars, angebunden an Modell und Nachrichten der Budget-Seite. Die Sperrlage kommt aus
/// `targetFormLocks`, Panel oder Sheet aus dem Viewport.
let formProps
    (model: Model)
    (category: BudgetCategory)
    (shown: Target option)
    (form: TargetForm)
    (status: ReactElement)
    (pause: ReactElement)
    (dispatch: Msg -> unit)
    : TargetFormProps =
    { Shown = shown
      Form = form
      Locks = targetFormLocks model category.Id
      IsPanel = Viewport.isInspectorPanelViewport ()
      Status = status
      Pause = pause
      AmountNote = None
      AmountFlash = Some model.TargetFlash
      OnAmountChanged = TargetAmountChanged >> dispatch
      OnDateChanged = TargetDateChanged >> dispatch
      OnRhythmChanged = TargetRhythmChanged >> dispatch
      OnWeekdayChanged = TargetWeekdayChanged >> dispatch
      OnOpenWeekdayChoice = fun () -> dispatch OpenTargetWeekdayChoice
      OnCloseWeekdayChoice = fun () -> dispatch CloseTargetWeekdayChoice
      OnSave = fun () -> dispatch SaveTarget
      OnConfirmChange = fun () -> dispatch ConfirmTargetChange
      OnCancelChange = fun () -> dispatch CancelTargetChange
      OnAskRemove = fun () -> dispatch AskRemoveTarget
      OnConfirmRemove = fun () -> dispatch ConfirmRemoveTarget
      OnCancelRemove = fun () -> dispatch CancelRemoveTarget }

/// Laufender Monat: das Formular (`TargetFormView.view`).
let editableTarget
    (model: Model)
    (category: BudgetCategory)
    (shown: Target option)
    (form: TargetForm)
    (status: ReactElement)
    (pause: ReactElement)
    (dispatch: Msg -> unit)
    =
    TargetFormView.view (formProps model category shown form status pause dispatch)

/// Die Auswahl des Wochentags, wenn sie am Handy offen steht (budgeting-h2brj): dann ersetzt sie den Inhalt des
/// Sheets (§7.15). `None` sonst.
let weekdayChoiceInSheet (model: Model) (category: BudgetCategory) (dispatch: Msg -> unit) : ReactElement option =
    let props =
        match targetTabMode model category with
        | TargetEditable (target, form) -> Some (formProps model category (Some target) form Html.none Html.none dispatch)
        | TargetCreatable form -> Some (formProps model category None form Html.none Html.none dispatch)
        | NoTargetShown
        | TargetReadOnly _ -> None
    match props with
    | Some p when model.Tab = TargetTab -> TargetFormView.weekdayChoiceInSheet p
    | _ -> None
