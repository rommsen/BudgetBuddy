/// Der Tab Target: Formular im laufenden Monat, sonst nur Text (budgeting-k8wq3, ADR 0080). Was erscheint,
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

let private amountField (model: Model) (target: Target) (form: TargetForm) (dispatch: Msg -> unit) =
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
/// Felder, Rhythmus und Art, Status und Fehlbetrag (`status`), "Target speichern" mit der Fehlermeldung
/// darunter, der Pause-Knopf (`pause`) und "Target entfernen" am Ende.
let editableTarget
    (model: Model)
    (category: BudgetCategory)
    (target: Target)
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
        Html.div [
            prop.className "grid grid-cols-2 gap-3"
            prop.children [
                amountField model target form dispatch
                Input.groupSimple "Datum" (
                    Html.div [
                        prop.custom ("data-testid", "target-date")
                        prop.children [
                            DateField.view
                                { Value = targetDateValue target form
                                  OnChange = TargetDateChanged >> dispatch
                                  Invalid = targetDateInvalid form
                                  Disabled = targetFieldsLocked model
                                  Label = "Datum des Targets" }
                        ]
                    ]
                )
            ]
        ]
        match rhythmText target.Rhythm with
        | "" -> ()
        | rhythm -> figure "target-rhythm" "Rhythmus" (rhythm.TrimStart('/', ' '))
        refillFact target
        status
        Html.div [
            prop.custom ("data-testid", "target-save")
            prop.className "flex flex-col gap-2"
            prop.children [
                Button.view
                    { Button.defaultProps with
                        Text = "Target speichern"
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
        pause
        if not form.ConfirmingRemove || isPanel then
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
