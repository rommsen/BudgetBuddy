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

/// Vergangener oder künftiger Monat: Target, Rhythmus und Datum nur als Text, kein Formular. Darunter der
/// Satz, warum (budgeting-r3wq8).
let readOnlyTarget (target: Target) =
    React.fragment [
        figure "target-goal" "Target" (targetText target)
        match target.Date with
        | Some date -> figure "target-date-text" "Datum" (dateText date)
        | None -> ()
        refillFact target
        Html.p [
            prop.custom ("data-testid", "target-edit-month")
            prop.className ListRoles.muted
            prop.text TargetEditMonthText
        ]
    ]

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
                      OnCancel = fun () -> dispatch CancelRemoveTarget }
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
        let title = "Rhythmus wechseln?"
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
                          OnCancel = fun () -> dispatch CancelTargetChange }
                ]
            ]
        else
            Html.none

/// Der Rhythmus als `Segmented`: Monat, Woche, Jahr (budgeting-xp46a). Hat das Target einen Rhythmus, den BB
/// nicht herstellen kann, ist kein Segment aktiv und der Rhythmus steht darüber als Text.
let private rhythmChoice (shown: Target option) (form: TargetForm) (dispatch: Msg -> unit) =
    let active = targetRhythmValue shown form
    let segment (label: string) (frequency: TargetFrequency) : Segmented.Segment =
        { Label = label
          IsActive = (active = Some frequency)
          OnClick = fun () -> dispatch (TargetRhythmChanged frequency) }
    Input.groupSimple "Rhythmus" (
        Html.div [
            prop.custom ("data-testid", "target-rhythm-choice")
            prop.className "flex flex-col gap-1"
            prop.children [
                match shown, active with
                | Some target, None ->
                    match rhythmText target.Rhythm with
                    | "" -> ()
                    | rhythm -> figure "target-rhythm" "Bisher" (rhythm.TrimStart('/', ' '))
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
                        ClassName = Some (amountFieldClass hint.IsSome) }
                match hint with
                | Some text ->
                    Html.div [ prop.custom ("data-testid", "target-amount-hint"); prop.children [ invalidHint text ] ]
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
    let saving = targetSavingFor model category.Id
    React.fragment [
        match form.Notice with
        | Some TargetChangedNotice ->
            Html.div [
                prop.custom ("data-testid", "target-notice")
                prop.children [ ErrorDisplay.notice (targetNoticeText TargetChangedNotice) ]
            ]
        | Some (TargetRejectedNotice _)
        | None -> ()
        rhythmChoice shown form dispatch
        Html.div [
            prop.className "grid grid-cols-2 gap-3"
            prop.children [
                amountField model shown form dispatch
                Input.groupSimple "Datum" (
                    Html.div [
                        prop.custom ("data-testid", "target-date")
                        prop.children [
                            DateField.view
                                { Value = targetDateValue shown form
                                  OnChange = TargetDateChanged >> dispatch
                                  Invalid = targetDateInvalid form
                                  Disabled = targetFieldsLocked model
                                  Label = "Datum des Targets" }
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
                            IsLoading = saving
                            IsDisabled = not (canSaveTarget model category)
                            ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                            OnClick = fun () -> dispatch SaveTarget }
                    match form.Notice with
                    | Some (TargetRejectedNotice _ as notice) ->
                        Html.div [
                            prop.custom ("data-testid", "target-error")
                            prop.children [ ErrorDisplay.cardCompact (targetNoticeText notice) None ]
                        ]
                    | Some TargetChangedNotice
                    | None -> ()
                ]
            ]
        if isPanel then changeConfirm isPanel shown form dispatch
        pause
        if shown.IsSome && (not form.ConfirmingRemove || isPanel) then
            Html.div [
                prop.custom ("data-testid", "target-remove")
                prop.children [
                    Button.view
                        { Button.defaultProps with
                            Text = "Target entfernen"
                            Variant = Button.Danger
                            IsDisabled = removeTargetLocked model form
                            ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                            OnClick = fun () -> dispatch AskRemoveTarget }
                ]
            ]
        removeConfirm isPanel form dispatch
    ]
