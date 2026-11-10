/// Der Tab Geld: Zuweisen, Holen und Abgeben, dazu die Offene Verschiebung (budgeting-c5w2n).
module Components.Budget.MovesView

open Feliz
open Fable.Core.JsInterop
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Targets
open Components.Budget.Moves
open Components.Budget.Inspector
open Components.Budget.Pause
open Components.Budget.PauseView
open Client.DesignSystem
open Client.DesignSystem.Tokens
open Components.Budget.MonthView

/// Markiert den Inhalt beim Fokus: das nächste Zeichen ersetzt ihn.
let private selectAll (e: Browser.Types.FocusEvent) : unit =
    e.target?select ()

/// Führt die Tastenhandlung aus (`assignKeyAction`, `amountKeyAction`). Ein Enter oder Esc nimmt dem
/// Feld den Fokus, die Pfeiltasten gehen danach wieder (`pageKeyAction`).
let private fieldKeys (action: string -> FieldKeyAction) (dispatch: Msg -> unit) (e: Browser.Types.KeyboardEvent) : unit =
    let leaveField () = e.currentTarget?blur ()
    match action e.key with
    | KeepField -> ()
    | LeaveField msg ->
        msg |> Option.iter dispatch
        leaveField ()
    | CancelField msg ->
        e.stopPropagation ()
        dispatch msg
        leaveField ()

/// Zählt nur, wenn der Fokus wirklich ging: beim Wechsel in ein anderes Programm bleibt das Feld das
/// aktive Element, ein Blick in einen Rechner speichert nicht.
let private focusLeft (e: Browser.Types.FocusEvent) : bool =
    not (obj.ReferenceEquals (Browser.Dom.document.activeElement, e.target))

let moneySwitch (tab: MoneyTabState) (dispatch: Msg -> unit) =
    let segment label mode : Segmented.Segment =
        { Label = label
          IsActive = (tab.Mode = mode)
          Disabled = false
          KeepsFocus = false
          OnClick = fun () -> dispatch (SwitchMoneyMode mode) }
    Segmented.view "money-mode" "Modus" [ segment "Zuweisen" AssignMode; segment "Holen" TakeMode; segment "Abgeben" GiveMode ]

/// Ein Betragsfeld. `.input-field` setzt Farbe und Schrift ohne Layer, deshalb eigene Regeln (`styles.css`).
let private assignField (model: Model) (category: BudgetCategory) (tab: MoneyTabState) (dispatch: Msg -> unit) =
    Input.groupSimple "Zugewiesen" (
        Html.div [
            prop.custom ("data-testid", "assign-field")
            prop.children [
                Input.text
                    { Input.textInputDefaults with
                        Value = assignFieldText category tab
                        OnChange = AssignTextChanged >> dispatch
                        InputMode = Some "decimal"
                        ReadOnly = assignFieldReadOnly model
                        OnFocus = Some selectAll
                        OnBlur = Some (fun e -> if focusLeft e then dispatch AssignFieldLeft)
                        OnKeyDown = Some (fieldKeys assignKeyAction dispatch)
                        AriaLabel = Some "Zugewiesen"
                        DescribedBy = (if assignFieldInvalid tab then Some "assign-amount-hint" else None)
                        AriaInvalid = assignFieldInvalid tab
                        ClassName = Some (Input.amountFieldClass false (assignFieldInvalid tab)) }
            ]
        ]
    )

/// Fester Platz: das Sheet springt nicht, wenn die Vorschau erscheint.
let private previewSlot (month: BudgetMonth) (category: BudgetCategory) (tab: MoneyTabState) =
    Html.div [
        prop.custom ("data-testid", "assign-preview")
        prop.className "flex flex-col gap-0.5 min-h-10"
        prop.children [
            match assignPreview month category tab with
            | NoPreview -> ()
            | InvalidCalculation -> invalidHint "assign-amount-hint" AmountMath.InvalidCalculationText
            | AssignAfter (available, assigned, toBeBudgeted) ->
                Html.div [
                    prop.className ListRoles.mobileLine1
                    prop.children [
                        Html.span [ prop.className ListRoles.secondary; prop.text "Verfügbar danach" ]
                        Html.span [
                            prop.custom ("data-testid", "assign-available-after")
                            prop.children [ Money.afterAmount available.Amount available.Currency ]
                        ]
                    ]
                ]
                Html.div [
                    prop.className "flex flex-wrap items-baseline gap-x-1"
                    prop.children [
                        Html.span [
                            prop.className ListRoles.muted
                            prop.text $"Ergebnis {moneyText assigned} · Zu verteilen danach"
                        ]
                        availableAmount toBeBudgeted
                    ]
                ]
        ]
    ]

let private assignSteps (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "assign-steps")
        prop.className "grid grid-cols-6 gap-1"
        prop.children [
            for label, delta in [ "-50", -50m; "-20", -20m; "-10", -10m; "+10", 10m; "+20", 20m; "+50", 50m ] do
                let spoken = if delta < 0m then $"{int -delta} abziehen" else $"{int delta} dazu"
                Button.amountStep "assign-step" spoken label (fun () -> dispatch (AssignStepped delta))
        ]
    ]

let private missingButton (model: Model) (month: BudgetMonth) (category: BudgetCategory) (tab: MoneyTabState) (dispatch: Msg -> unit) =
    let action = missingAction month category
    let button variant enabled =
        Html.div [
            prop.custom ("data-testid", "assign-missing")
            prop.children [
                Button.view
                    { Button.defaultProps with
                        Text = missingActionText action
                        Variant = variant
                        IsDisabled = not enabled
                        IsLoading = (model.MoveInFlight <> NoMoveCall)
                        ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                        KeepsFocus = true
                        OnClick = fun () -> dispatch CoverMissing }
            ]
        ]
    match action with
    | NothingMissing -> Html.none
    | AssignMissing _ -> button Button.Primary (canCover model)
    | OnlyLeft _ -> button Button.Secondary false

/// Die Markierung einer offenen Rückschreibung mit dem Weg zu "erneut senden" (budgeting-dgrp7). Gelb wie
/// jedes "offen, kein Fehler" der Budget-Seite (ADR 0075).
let private notInYnabNotice (model: Model) (category: BudgetCategory) (dispatch: Msg -> unit) =
    match category.OpenWriteBack with
    | None -> Html.none
    | Some groupId ->
        let applicationId = string groupId
        Html.div [
            prop.custom ("data-testid", "assign-not-in-ynab")
            // Die Leiste reicht wie im Buchungs-Detail bis an den Rand des Panels, der Tab hat `p-4`.
            prop.className "-mx-4"
            prop.children [
                Detail.notice
                    { Tone = Detail.NoticeYellow
                      Badge = NotInYnabText
                      Reason = NotInYnabReason
                      Action =
                        Some
                            { Label = retryMoveLabel
                              Busy = writeBackRetryRunning applicationId model
                              Disabled = not (canStartMove model)
                              OnClick = fun () -> dispatch (RetryMove applicationId) } }
            ]
        ]

let private assignMode (model: Model) (month: BudgetMonth) (category: BudgetCategory) (tab: MoneyTabState) (dispatch: Msg -> unit) =
    React.fragment [
        assignField model category tab dispatch
        notInYnabNotice model category dispatch
        previewSlot month category tab
        // Am PC tippt man die Rechnung, die Schritte ersetzen die fehlenden Tasten am Handy.
        if not (Viewport.isInspectorPanelViewport ()) then
            assignSteps dispatch
        missingButton model month category tab dispatch
    ]

let private sideContent (label: string) (side: TransferEnd option) (placeholder: string) =
    [ Html.span [
          prop.className "flex flex-col min-w-0"
          prop.children [
              Html.span [ prop.className ListRoles.muted; prop.text label ]
              Html.span [
                  prop.className $"{ListRoles.primary} break-words min-w-0"
                  prop.text (side |> Option.map (fun s -> s.Label) |> Option.defaultValue placeholder)
              ]
          ]
      ]
      match side with
      | Some { After = after } -> availableAmount after
      | None -> () ]

let private fixedSide (testId: string) (label: string) (side: TransferEnd option) =
    Button.sideRowFixed testId (sideContent label side "")

let private choosePrompt = "Kategorie wählen"

/// Die wählbare Seite. Am Handy geht der Picker über dem Inspektor auf, im Panel als Popover.
let private counterpartSide
    (model: Model)
    (month: BudgetMonth)
    (tab: MoneyTabState)
    (testId: string)
    (label: string)
    (side: TransferEnd option)
    (dispatch: Msg -> unit)
    =
    let pickerProps : CategoryPicker.CategoryPickerProps =
        { Categories = pickerCategories month tab.CategoryId
          Current = None
          Suggested = []
          Recent = recentCounterparts model
          Accounts = []
          CurrentAccount = None
          AccountsLoad = CategoryPicker.PickerReady
          OfferWithout = false
          ToBeBudgeted = Some month.ToBeBudgeted
          StandLabel = None
          LoadState = CategoryPicker.PickerReady
          OnRetry = ignore
          OnSelect = fun choice -> counterpartOf choice |> Option.iter (PickCounterpart >> dispatch)
          OnClose = fun () -> dispatch ClosePicker }
    let sideName = side |> Option.map (fun s -> s.Label) |> Option.defaultValue choosePrompt
    Html.div [
        prop.className "relative"
        prop.children [
            Html.div [
                // Das Popover schließt beim Mausdruck außerhalb, der Klick öffnete es sonst
                // gleich wieder. So bleibt der Auslöser ein Umschalter.
                prop.onMouseDown (fun e -> e.stopPropagation ())
                prop.children [
                    Button.sideRow testId $"{label}: {sideName}" tab.Transfer.PickerOpen
                        (fun () -> dispatch (if tab.Transfer.PickerOpen then ClosePicker else OpenPicker))
                        (sideContent label side choosePrompt)
                ]
            ]
            // ADR 0043: zur Renderzeit genau eine Hülle.
            if Viewport.isInspectorPanelViewport () then
                CategoryPicker.popoverInPanelRevealed tab.Transfer.PickerOpen pickerProps
            else
                CategoryPicker.sheetLayered tab.Transfer.PickerOpen None pickerProps
        ]
    ]

let private transferMode (model: Model) (month: BudgetMonth) (tab: MoneyTabState) (dispatch: Msg -> unit) =
    let source, target = transferEnds month tab
    React.fragment [
        Input.groupSimple "Betrag" (
            Html.div [
                prop.custom ("data-testid", "transfer-amount")
                prop.children [
                    Input.text
                        { Input.textInputDefaults with
                            Value = transferAmountText month tab
                            OnChange = TransferAmountChanged >> dispatch
                            Placeholder = "0,00"
                            InputMode = Some "decimal"
                            Size = Input.Large
                            OnFocus = Some selectAll
                            OnKeyDown = Some (fieldKeys amountKeyAction dispatch)
                            ReadOnly = assignFieldReadOnly model
                            AriaLabel = Some "Betrag"
                            DescribedBy = (if transferAmountInvalid month tab then Some "transfer-amount-hint" else None)
                            AriaInvalid = transferAmountInvalid month tab
                            ClassName = Some (Input.amountFieldClass false (transferAmountInvalid month tab)) }
                ]
            ]
        )
        if transferAmountInvalid month tab then invalidHint "transfer-amount-hint" AmountMath.InvalidCalculationText
        if tab.Mode = GiveMode then
            fixedSide "transfer-from" "von" source
            counterpartSide model month tab "transfer-to" "nach" target dispatch
        else
            counterpartSide model month tab "transfer-from" "von" source dispatch
            fixedSide "transfer-to" "nach" target
        match transferWarning month tab with
        | Some warning ->
            Html.div [
                prop.custom ("data-testid", "transfer-warning")
                prop.children [ ErrorDisplay.notice warning ]
            ]
        | None -> ()
        Html.div [
            prop.custom ("data-testid", "transfer-submit")
            prop.children [
                Button.view
                    { Button.defaultProps with
                        Text = transferButtonText month tab
                        IsLoading = (model.MoveInFlight <> NoMoveCall)
                        IsDisabled = not (canTransfer model month tab)
                        ClassName = Some $"w-full whitespace-normal break-words {TouchTargets.sheetMinHeight}"
                        OnClick = fun () -> dispatch ConfirmTransfer }
            ]
        ]
    ]

/// Der Modus unter dem Umschalter. Im Modus Zuweisen steht unten der Pause-Knopf.
let moneyMode (model: Model) (month: BudgetMonth) (category: BudgetCategory) (tab: MoneyTabState) (dispatch: Msg -> unit) =
    React.fragment [
        match tab.Mode with
        | AssignMode ->
            assignMode model month category tab dispatch
            PauseView.pauseControlView PauseInMoneyTab model category dispatch
        | TakeMode
        | GiveMode -> transferMode model month tab dispatch
        match tab.Transfer.Submission with
        | Rejected message
        | RejectedAway message ->
            Html.div [
                prop.custom ("data-testid", "transfer-error")
                prop.children [ ErrorDisplay.cardCompact message None ]
            ]
        | Editing -> ()
    ]


/// Verschiebungen mit fehlgeschlagener Zeile. Der Toast läuft ab, dieser Hinweis nicht.
let moveRetries (model: Model) (retries: OpenMoveRetry list) (dispatch: Msg -> unit) =
    React.fragment (
        retries
        |> List.map (fun retry ->
            Html.div [
                prop.key retry.ApplicationId
                prop.custom ("data-testid", "move-retry")
                prop.className "flex items-center justify-between gap-3 rounded-xl border border-border-subtle bg-surface-card p-4"
                prop.children [
                    Html.div [
                        prop.className "flex flex-col gap-1 min-w-0"
                        prop.children [
                            Html.span [ prop.className ListRoles.primary; prop.text $"{retry.Headline} ({retry.MonthLabel}): {retry.Summary}" ]
                            Html.span [ prop.className ListRoles.muted; prop.text retry.Reason ]
                        ]
                    ]
                    Html.span [
                        prop.className "shrink-0"
                        prop.children [
                            Button.view
                                { Button.defaultProps with
                                    Text = retryMoveLabel
                                    Variant = Button.Secondary
                                    IsLoading = (retry.State = RetryRunning)
                                    IsDisabled = not (canStartMove model)
                                    OnClick = fun () -> dispatch (RetryMove retry.ApplicationId) }
                        ]
                    ]
                ]
            ])
    )
