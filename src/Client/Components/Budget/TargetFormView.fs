/// Die View des Target-Formulars (budgeting-r7k2m): Rhythmus, Betrag und Datum, Speichern, Entfernen und die
/// Rückfragen, am PC als `Modal`, im Sheet als `BottomSheet`. Sie nimmt nur Props mit Rückrufen und kennt das
/// Modell der Budget-Seite nicht. Nutzer sind der Tab Target (`TargetEditView`) und die Seite Targets prüfen
/// (ADR 0082 Punkt 11). Was erscheint und was gesperrt ist, sagt der Kern `TargetFormCore`.
module Components.Budget.TargetFormView

open Feliz
open Shared.Domain
open Shared.Budgeting
open Components.Budget.TargetFormCore
open Components.Budget.MonthView
open Client.DesignSystem
open Client.DesignSystem.Tokens

/// Was die View des Formulars braucht: gezeigtes Target, Formular, Sperrlage, Panel oder Sheet und die
/// Rückrufe für jede Bedienung.
type TargetFormProps = {
    /// Das gezeigte Target, `None` zum Anlegen (budgeting-xp46a).
    Shown: Target option
    Form: TargetForm
    Locks: TargetFormLocks
    /// Am PC im Panel: Rückfragen als `Modal`, das Datum in eigener Zeile. Sonst im Sheet.
    IsPanel: bool
    /// Status und Fehlbetrag unter den Feldern, `Html.none` ohne.
    Status: ReactElement
    /// Der Pause-Knopf vor "Target entfernen", `Html.none` ohne.
    Pause: ReactElement
    /// Ein Satz unter dem Betragsfeld, etwa der Vorschlag auf der Seite Targets prüfen (budgeting-c4ve4).
    AmountNote: string option
    OnAmountChanged: string -> unit
    OnDateChanged: DateField.DateEntry -> unit
    OnRhythmChanged: TargetFrequency -> unit
    /// Wochentag gewählt, 0 Sonntag bis 6 Samstag (budgeting-eh8xe).
    OnWeekdayChanged: int -> unit
    OnSave: unit -> unit
    OnConfirmChange: unit -> unit
    OnCancelChange: unit -> unit
    OnAskRemove: unit -> unit
    OnConfirmRemove: unit -> unit
    OnCancelRemove: unit -> unit
}

let figure (testId: string) (label: string) (text: string) =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className "flex flex-col gap-1 min-w-0"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text label ]
            Html.span [ prop.className ListRoles.primary; prop.text text ]
        ]
    ]

/// "Auffüllen bis" als Text, in beiden Ansichten gleich.
let refillFact (target: Target) =
    match refillText target with
    | Some text -> figure "target-refill" "Art" text
    | None -> Html.none

/// Wohin der Fokus nach einer Rückfrage im Sheet geht (budgeting-g2ty9, F3): auf den Knopf, der sie
/// geöffnet hat. Er steht nach dem Schließen wieder an ihrer Stelle, nach dem Bestätigen dreht er.
let private removeButtonSelector = "[data-testid='target-remove'] button"
let private saveButtonSelector = "[data-testid='target-save'] button"

/// Die Bestätigung für "Target entfernen": am PC `Modal.confirmDanger`, am Handy im Sheet
/// `BottomSheet.confirmDanger` mit denselben Texten.
let private removeConfirm (props: TargetFormProps) =
    let form = props.Form
    let isPanel = props.IsPanel
    let title = "Target entfernen?"
    let message = "Das Target wird entfernt. Der Fehlbetrag entfällt."
    if isPanel then
        Modal.confirmDanger
            form.ConfirmingRemove
            title
            message
            "Entfernen"
            props.OnConfirmRemove
            props.OnCancelRemove
    elif form.ConfirmingRemove then
        Html.div [
            prop.custom ("data-testid", "target-remove-confirm")
            prop.children [
                BottomSheet.confirmDanger
                    { Title = title
                      Message = message
                      ConfirmText = "Entfernen"
                      OnConfirm = props.OnConfirmRemove
                      OnCancel = props.OnCancelRemove
                      ReturnFocus = Some removeButtonSelector }
            ]
        ]
    else
        Html.none

/// Die Warnung vor einem Wechsel der Art mit Verlusten: am PC `Modal.confirm`, am Handy `BottomSheet.confirm`
/// im Sheet, mit denselben Texten (budgeting-xp46a).
let private changeConfirm (props: TargetFormProps) =
    let form = props.Form
    let isPanel = props.IsPanel
    match props.Shown with
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
                props.OnConfirmChange
                props.OnCancelChange
        elif form.ConfirmingChange then
            Html.div [
                prop.custom ("data-testid", "target-change-confirm")
                prop.children [
                    BottomSheet.confirm
                        { Title = title
                          Message = message
                          ConfirmText = "Wechseln"
                          OnConfirm = props.OnConfirmChange
                          OnCancel = props.OnCancelChange
                          ReturnFocus = Some saveButtonSelector }
                ]
            ]
        else
            Html.none

/// Der Rhythmus als `Segmented`: Monat, Woche, Jahr (budgeting-xp46a). Hat das Target einen Rhythmus, den BB
/// nicht herstellen kann, ist kein Segment aktiv und der Rhythmus steht darüber als Text. Solange das
/// Speichern läuft, ist er gesperrt wie Betrag und Datum (budgeting-g2ty9, X3).
let private rhythmChoice (props: TargetFormProps) =
    let shown = props.Shown
    let active = targetRhythmValue shown props.Form
    let locked = formFieldsLocked props.Locks
    let segment (label: string) (frequency: TargetFrequency) : Segmented.Segment =
        { Label = label
          IsActive = (active = Some frequency)
          Disabled = locked
          OnClick = fun () -> props.OnRhythmChanged frequency }
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

/// Der Wochentag eines wöchentlichen Targets als `Segmented`, Montag zuerst (budgeting-eh8xe). Steht nur, wenn
/// das Target nach dem Speichern wöchentlich ist (`targetWeeklyAfter`), gesperrt wie der Rhythmus. Ein Pflichtfeld:
/// ohne Wahl steht der Grund darunter (`targetWeekdayHint`) und Speichern ist gesperrt.
let private weekdayChoice (props: TargetFormProps) =
    let active = targetWeekdayValue props.Shown props.Form
    let locked = formFieldsLocked props.Locks
    let segment (label: string, day: int) : Segmented.Segment =
        { Label = label
          IsActive = (active = Some day)
          Disabled = locked
          OnClick = fun () -> props.OnWeekdayChanged day }
    Input.group
        { Label = "Wochentag"
          Required = true
          Error = targetWeekdayHint props.Shown props.Form
          HelpText = None
          Children =
            Html.div [
                prop.custom ("data-testid", "target-weekday-choice")
                prop.children [ Segmented.view "target-weekday-segment" "Wochentag des Targets" (weekdays |> List.map segment) ]
            ] }

let private amountField (props: TargetFormProps) =
    let target = props.Shown
    let form = props.Form
    let hint = targetAmountHint target form
    Input.groupSimple "Target" (
        Html.div [
            prop.custom ("data-testid", "target-amount")
            prop.className "flex flex-col gap-1"
            prop.children [
                Input.text
                    { Input.textInputDefaults with
                        Value = targetAmountText target form
                        OnChange = props.OnAmountChanged
                        InputMode = Some "decimal"
                        AriaLabel = Some "Target"
                        ReadOnly = formFieldsLocked props.Locks
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
                match props.AmountNote with
                | Some text ->
                    Html.p [ prop.custom ("data-testid", "target-amount-note"); prop.className ListRoles.secondary; prop.text text ]
                | None -> ()
            ]
        ]
    )

/// Der Satz unter einem Knopf, den eine Pause derselben Kategorie sperrt (budgeting-f8w2n).
let private waitHint (testId: string) (text: string option) : ReactElement =
    match text with
    | Some text ->
        Html.p [
            prop.custom ("data-testid", testId)
            prop.className ListRoles.secondary
            prop.text text
        ]
    | None -> Html.none

/// Laufender Monat (budgeting-k8wq3, Reihenfolge budgeting-r3wq8): der gelbe Hinweis zu YNAB oben, dann
/// der Rhythmus als `Segmented` (budgeting-xp46a), Betrag und Datum, die Art, Status und Fehlbetrag
/// (`status`), "Target speichern" mit der Fehlermeldung darunter, der Pause-Knopf (`pause`) und "Target
/// entfernen" am Ende. Ohne Target (`shown = None`, budgeting-xp46a) dieselben Felder mit "Target anlegen",
/// ohne Entfernen. Status und Pause gibt der Nutzer als fertige Elemente hinein, `Html.none` ohne.
let view (props: TargetFormProps) =
    let shown = props.Shown
    let form = props.Form
    let isPanel = props.IsPanel
    let save = formSaveButton shown props.Locks form
    React.fragment [
        rhythmChoice props
        if targetWeeklyAfter shown form then weekdayChoice props
        Html.div [
            // Im 340-px-Panel am PC steht das Datum in eigener Zeile, sonst schnitte das Feld den Platzhalter
            // ab (budgeting-g2ty9, X6).
            prop.className (if isPanel then "grid grid-cols-1 gap-3" else "grid grid-cols-2 gap-3")
            prop.children [
                amountField props
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
                                      OnChange = props.OnDateChanged
                                      Invalid = targetDateInvalid form
                                      Disabled = false
                                      ReadOnly = formFieldsLocked props.Locks
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
        props.Status
        // Am Handy steht die Warnung an der Stelle des Knopfs, damit sie nach dem Tipp im Bild liegt.
        if form.ConfirmingChange && not isPanel then
            changeConfirm props
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
                            OnClick = props.OnSave }
                    waitHint "target-save-wait" (formSaveWaitHint shown props.Locks form)
                    match form.Notice with
                    | Some (TargetRejectedNotice _ as notice) ->
                        Html.div [
                            prop.custom ("data-testid", "target-error")
                            prop.children [ ErrorDisplay.cardCompact (targetNoticeText notice) None ]
                        ]
                    // Inzwischen geändert ist kein Fehler: der Tab zeigt BBs Stand, gelb wie andere offene Hinweise
                    // der Budget-Seite (ADR 0075).
                    | Some TargetChangedMeanwhileNotice ->
                        Html.div [
                            prop.custom ("data-testid", "target-changed-meanwhile")
                            prop.children [ ErrorDisplay.notice (targetNoticeText TargetChangedMeanwhileNotice) ]
                        ]
                    | None -> ()
                ]
            ]
        if isPanel then changeConfirm props
        props.Pause
        if shown.IsSome && (not form.ConfirmingRemove || isPanel) then
            let remove = formRemoveButton props.Locks form
            Html.div [
                prop.custom ("data-testid", "target-remove")
                prop.className "flex flex-col gap-2"
                prop.children [
                    Button.view
                        { Button.defaultProps with
                            Text = "Target entfernen"
                            Variant = Button.Danger
                            IsBusy = remove.Busy
                            IsDisabled = remove.Disabled
                            ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                            OnClick = props.OnAskRemove }
                    waitHint "target-remove-wait" (formRemoveWaitHint props.Locks)
                ]
            ]
        removeConfirm props
    ]
