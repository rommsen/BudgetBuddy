/// Der Tab Target: Formular im laufenden Monat, zum Ändern oder ohne Target zum Anlegen, sonst nur Text
/// (budgeting-k8wq3, budgeting-xp46a, ADR 0080). Was erscheint,
/// sagt `targetTabMode`, die View ruft es nur auf.
module Components.Budget.TargetEditView

open Feliz
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.TargetEdit
open Components.Budget.MonthView
open Client.DesignSystem
open Client.DesignSystem.Tokens

let private figure (testId: string) (label: string) (text: string) =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className "flex flex-col gap-1 min-w-0"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text label ]
            Html.span [ prop.className ListRoles.primary; prop.text text ]
        ]
    ]

/// "Auffüllen bis" als Text, in beiden Ansichten gleich.
let private refillFact (target: Target) =
    match refillText target with
    | Some text -> figure "target-refill" "Art" text
    | None -> Html.none

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
        figure "target-goal" "Target" (targetText target)
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

/// Wohin der Fokus nach einer Rückfrage im Sheet geht (budgeting-g2ty9, F3): auf den Knopf, der sie
/// geöffnet hat. Er steht nach dem Schließen wieder an ihrer Stelle, nach dem Bestätigen dreht er.
let private removeButtonSelector = "[data-testid='target-remove'] button"
let private saveButtonSelector = "[data-testid='target-save'] button"

/// Die Bestätigung für "Target entfernen": am PC `Modal.confirmDanger`, am Handy im Sheet
/// `BottomSheet.confirmDanger` mit denselben Texten.
let private removeConfirm (isPanel: bool) (form: TargetForm) (dispatch: Msg -> unit) =
    let title = "Target entfernen?"
    let message = "Das Target wird in YNAB entfernt. Der Fehlbetrag entfällt."
    if isPanel then
        Modal.confirmDanger
            form.ConfirmingRemove
            title
            message
            "Entfernen"
            (fun () -> dispatch ConfirmRemoveTarget)
            (fun () -> dispatch CancelRemoveTarget)
    elif form.ConfirmingRemove then
        Html.div [
            prop.custom ("data-testid", "target-remove-confirm")
            prop.children [
                BottomSheet.confirmDanger
                    { Title = title
                      Message = message
                      ConfirmText = "Entfernen"
                      OnConfirm = fun () -> dispatch ConfirmRemoveTarget
                      OnCancel = fun () -> dispatch CancelRemoveTarget
                      ReturnFocus = Some removeButtonSelector }
            ]
        ]
    else
        Html.none

/// Die Warnung vor einem Wechsel der Art mit Verlusten: am PC `Modal.confirm`, am Handy `BottomSheet.confirm`
/// im Sheet, mit denselben Texten (budgeting-xp46a).
let private changeConfirm (isPanel: bool) (shown: Target option) (form: TargetForm) (dispatch: Msg -> unit) =
    match shown with
    | None -> Html.none
    | Some target ->
        let title = targetChangeTitle target form
        let message = targetChangeWarningText target form
        if isPanel then
            Modal.confirm
                form.ConfirmingChange
                title
                message
                "Wechseln"
                (fun () -> dispatch ConfirmTargetChange)
                (fun () -> dispatch CancelTargetChange)
        elif form.ConfirmingChange then
            Html.div [
                prop.custom ("data-testid", "target-change-confirm")
                prop.children [
                    BottomSheet.confirm
                        { Title = title
                          Message = message
                          ConfirmText = "Wechseln"
                          OnConfirm = fun () -> dispatch ConfirmTargetChange
                          OnCancel = fun () -> dispatch CancelTargetChange
                          ReturnFocus = Some saveButtonSelector }
                ]
            ]
        else
            Html.none

/// Der Rhythmus als `Segmented`: Monat, Woche, Jahr (budgeting-xp46a). Hat das Target einen Rhythmus, den BB
/// nicht herstellen kann, ist kein Segment aktiv und der Rhythmus steht darüber als Text. Solange das
/// Speichern läuft, ist er gesperrt wie Betrag und Datum (budgeting-g2ty9, X3).
let private rhythmChoice (model: Model) (shown: Target option) (form: TargetForm) (dispatch: Msg -> unit) =
    let active = targetRhythmValue shown form
    let locked = targetFieldsLocked model
    let segment (label: string) (frequency: TargetFrequency) : Segmented.Segment =
        { Label = label
          IsActive = (active = Some frequency)
          Disabled = locked
          OnClick = fun () -> dispatch (TargetRhythmChanged frequency) }
    Input.groupSimple "Rhythmus" (
        Html.div [
            prop.custom ("data-testid", "target-rhythm-choice")
            prop.className "flex flex-col gap-1"
            prop.children [
                match shown, active with
                | Some target, None ->
                    match target.Rhythm with
                    | NoRhythm -> ()
                    | rhythm -> figure "target-rhythm" "Bisher" (rhythmWordText rhythm)
                | _ -> ()
                Segmented.view
                    "target-rhythm-segment"
                    "Rhythmus des Targets"
                    [ segment "Monat" Monthly; segment "Woche" Weekly; segment "Jahr" Yearly ]
            ]
        ]
    )

let private amountField (model: Model) (target: Target option) (form: TargetForm) (dispatch: Msg -> unit) =
    let hint = targetAmountHint target form
    Input.groupSimple "Target" (
        Html.div [
            prop.custom ("data-testid", "target-amount")
            prop.className "flex flex-col gap-1"
            prop.children [
                Input.text
                    { Input.textInputDefaults with
                        Value = targetAmountText target form
                        OnChange = TargetAmountChanged >> dispatch
                        InputMode = Some "decimal"
                        AriaLabel = Some "Target"
                        ReadOnly = targetFieldsLocked model
                        DescribedBy = (if hint.IsSome then Some "target-amount-hint-text" else None)
                        AriaInvalid = hint.IsSome
                        ClassName = Some (amountFieldClass hint.IsSome) }
                match hint with
                | Some text ->
                    Html.div [
                        prop.custom ("data-testid", "target-amount-hint")
                        prop.children [ invalidHint "target-amount-hint-text" text ]
                    ]
                | None -> ()
            ]
        ]
    )

/// Laufender Monat (budgeting-k8wq3, Reihenfolge budgeting-r3wq8): der gelbe Hinweis zu YNAB oben, dann
/// der Rhythmus als `Segmented` (budgeting-xp46a), Betrag und Datum, die Art, Status und Fehlbetrag
/// (`status`), "Target speichern" mit der Fehlermeldung darunter, der Pause-Knopf (`pause`) und "Target
/// entfernen" am Ende. Ohne Target (`shown = None`, budgeting-xp46a) dieselben Felder mit "Target anlegen",
/// ohne Entfernen.
let editableTarget
    (model: Model)
    (category: BudgetCategory)
    (shown: Target option)
    (form: TargetForm)
    (status: ReactElement)
    (pause: ReactElement)
    (dispatch: Msg -> unit)
    =
    let isPanel = Viewport.isInspectorPanelViewport ()
    let save = saveButtonState model category
    React.fragment [
        match form.Notice with
        | Some TargetChangedNotice ->
            Html.div [
                prop.custom ("data-testid", "target-notice")
                prop.children [ ErrorDisplay.notice (targetNoticeText TargetChangedNotice) ]
            ]
        | Some (TargetRejectedNotice _)
        | Some (TargetPartlyNotice _)
        | None -> ()
        rhythmChoice model shown form dispatch
        Html.div [
            // Im 340-px-Panel am PC steht das Datum in eigener Zeile, sonst schnitte das Feld den Platzhalter
            // ab (budgeting-g2ty9, X6).
            prop.className (if isPanel then "grid grid-cols-1 gap-3" else "grid grid-cols-2 gap-3")
            prop.children [
                amountField model shown form dispatch
                // Bei wöchentlich kein Datum, bei monatlich und jährlich "Fällig am" (budgeting-d7r4k).
                if targetDateShown shown form then
                    let label = targetDateLabel (targetRhythmValue shown form)
                    Input.groupSimple label (
                        Html.div [
                            prop.custom ("data-testid", "target-date")
                            prop.className "flex flex-col gap-1"
                            prop.children [
                                DateField.view
                                    { Value = targetDateValue shown form
                                      OnChange = TargetDateChanged >> dispatch
                                      Invalid = targetDateInvalid form
                                      Disabled = false
                                      ReadOnly = targetFieldsLocked model
                                      Label = targetDateAriaLabel (targetRhythmValue shown form) }
                                match targetDateHint shown form with
                                | Some text ->
                                    Html.p [
                                        prop.custom ("data-testid", "target-date-hint")
                                        prop.className ListRoles.secondary
                                        prop.text text
                                    ]
                                | None -> ()
                            ]
                        ]
                    )
            ]
        ]
        match shown with
        | Some target -> refillFact target
        | None -> ()
        status
        // Am Handy steht die Warnung an der Stelle des Knopfs, damit sie nach dem Tipp im Bild liegt.
        if form.ConfirmingChange && not isPanel then
            changeConfirm isPanel shown form dispatch
        else
            Html.div [
                prop.custom ("data-testid", "target-save")
                prop.className "flex flex-col gap-2"
                prop.children [
                    Button.view
                        { Button.defaultProps with
                            Text = (if shown.IsSome then "Target speichern" else "Target anlegen")
                            Variant = Button.Primary
                            IsBusy = save.Busy
                            IsDisabled = save.Disabled
                            ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                            OnClick = fun () -> dispatch SaveTarget }
                    match form.Notice with
                    | Some (TargetRejectedNotice _ as notice) ->
                        Html.div [
                            prop.custom ("data-testid", "target-error")
                            prop.children [ ErrorDisplay.cardCompact (targetNoticeText notice) None ]
                        ]
                    | Some (TargetPartlyNotice _ as notice) ->
                        // Gelb, kein Fehler: das Target steht, nur das Datum fehlt (budgeting-g2ty9, X4).
                        Html.div [
                            prop.custom ("data-testid", "target-partly")
                            prop.children [ ErrorDisplay.notice (targetNoticeText notice) ]
                        ]
                    | Some TargetChangedNotice
                    | None -> ()
                ]
            ]
        if isPanel then changeConfirm isPanel shown form dispatch
        pause
        if shown.IsSome && (not form.ConfirmingRemove || isPanel) then
            let remove = removeButtonState model form
            Html.div [
                prop.custom ("data-testid", "target-remove")
                prop.children [
                    Button.view
                        { Button.defaultProps with
                            Text = "Target entfernen"
                            Variant = Button.Danger
                            IsBusy = remove.Busy
                            IsDisabled = remove.Disabled
                            ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                            OnClick = fun () -> dispatch AskRemoveTarget }
                ]
            ]
        removeConfirm isPanel form dispatch
    ]
