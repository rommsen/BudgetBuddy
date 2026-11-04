/// Der Tab Target: Formular im laufenden Monat, sonst nur Text (budgeting-k8wq3, ADR 0080). Was erscheint,
/// sagt `targetTabMode`, die View ruft es nur auf.
module Components.Budget.TargetEditView

open Feliz
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.TargetEdit
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

/// Datum und "Auffüllen bis" als Text, in beiden Ansichten gleich.
let private targetFacts (target: Target) =
    React.fragment [
        match target.Date with
        | Some date -> figure "target-date-text" "Datum" (dateText date)
        | None -> ()
        match refillText target with
        | Some text -> figure "target-refill" "Art" text
        | None -> ()
    ]

/// Vergangener oder künftiger Monat: Target, Rhythmus und Datum nur als Text, kein Formular.
let readOnlyTarget (target: Target) =
    React.fragment [
        figure "target-goal" "Target" (targetText target)
        targetFacts target
    ]

/// Die Bestätigung für "Target entfernen": am PC `Modal.confirmDanger`, am Handy im Sheet ein Block mit
/// denselben Knöpfen.
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
            prop.className "flex flex-col gap-2"
            prop.children [
                Html.p [ prop.className ListRoles.secondary; prop.text message ]
                Html.div [
                    prop.className "grid grid-cols-2 gap-2"
                    prop.children [
                        Button.view
                            { Button.defaultProps with
                                Text = "Abbrechen"
                                Variant = Button.Secondary
                                ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                                OnClick = fun () -> dispatch CancelRemoveTarget }
                        Button.view
                            { Button.defaultProps with
                                Text = "Entfernen"
                                Variant = Button.Danger
                                ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                                OnClick = fun () -> dispatch ConfirmRemoveTarget }
                    ]
                ]
            ]
        ]
    else
        Html.none

/// Laufender Monat: Betrag, Datum, Rhythmus als Text, Speichern und Entfernen.
let editableTarget (model: Model) (category: BudgetCategory) (target: Target) (form: TargetForm) (dispatch: Msg -> unit) =
    let isPanel = Viewport.isInspectorPanelViewport ()
    let saving = targetSavingFor model category.Id
    React.fragment [
        match form.Notice with
        | Some TargetChangedNotice ->
            Html.div [
                prop.custom ("data-testid", "target-notice")
                prop.children [ ErrorDisplay.notice (targetNoticeText TargetChangedNotice) ]
            ]
        | Some (TargetRejectedNotice _ as notice) ->
            Html.div [
                prop.custom ("data-testid", "target-error")
                prop.children [ ErrorDisplay.cardCompact (targetNoticeText notice) None ]
            ]
        | None -> ()
        Html.div [
            prop.className "grid grid-cols-2 gap-3"
            prop.children [
                Input.groupSimple "Target" (
                    Html.div [
                        prop.custom ("data-testid", "target-amount")
                        prop.children [
                            Input.text
                                { Input.textInputDefaults with
                                    Value = targetAmountText target form
                                    OnChange = TargetAmountChanged >> dispatch
                                    InputMode = Some "decimal"
                                    AriaLabel = Some "Target"
                                    ClassName =
                                        Some (
                                            if (targetAmountOf target form).IsNone then "w-full input-amount input-invalid tabular-nums"
                                            else "w-full input-amount tabular-nums"
                                        ) }
                        ]
                    ]
                )
                Input.groupSimple "Datum" (
                    Html.div [
                        prop.custom ("data-testid", "target-date")
                        prop.children [
                            DateField.view
                                { Value = targetDateValue target form
                                  OnChange = TargetDateChanged >> dispatch
                                  Invalid = targetDateInvalid form
                                  Label = "Datum des Targets" }
                        ]
                    ]
                )
            ]
        ]
        match rhythmText target.Rhythm with
        | "" -> ()
        | rhythm -> figure "target-rhythm" "Rhythmus" (rhythm.TrimStart('/', ' '))
        match refillText target with
        | Some text -> figure "target-refill" "Art" text
        | None -> ()
        Html.div [
            prop.custom ("data-testid", "target-save")
            prop.children [
                Button.view
                    { Button.defaultProps with
                        Text = "Target speichern"
                        Variant = Button.Primary
                        IsLoading = saving
                        IsDisabled = not (canSaveTarget model category)
                        ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                        OnClick = fun () -> dispatch SaveTarget }
            ]
        ]
        if not form.ConfirmingRemove || isPanel then
            Html.div [
                prop.custom ("data-testid", "target-remove")
                prop.children [
                    Button.view
                        { Button.defaultProps with
                            Text = "Target entfernen"
                            Variant = Button.Danger
                            IsDisabled = targetBusy model
                            ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                            OnClick = fun () -> dispatch AskRemoveTarget }
                ]
            ]
        removeConfirm isPanel form dispatch
    ]
