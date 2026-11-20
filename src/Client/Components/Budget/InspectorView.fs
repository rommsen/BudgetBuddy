/// Der Inspektor: Panel und Sheet (budgeting-g8s3w).
module Components.Budget.InspectorView

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
open Client.DesignSystem
open Client.DesignSystem.Tokens
open Components.Budget.MonthView
open Components.Budget.MovesView
open Components.Budget.HistoryRowsView

let private detailFigure (testId: string) (label: string) (value: ReactElement) =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className "flex flex-col gap-1 min-w-0"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text label ]
            value
        ]
    ]

/// Aktivität und Verfügbar. Zugewiesen ist das Feld darunter. Nach einem Bericht mit geschriebener Zeile leuchtet
/// „Verfügbar“ kurz (`Feedback.flash`, budgeting-gtrgk); der Schlüssel baut den Wert je Leuchten neu, so beginnt die
/// Animation auch beim zweiten Mal von vorn.
let private detailFigures (flash: AvailableFlash) (category: BudgetCategory) =
    Html.div [
        prop.className "grid grid-cols-2 gap-3"
        prop.children [
            detailFigure "detail-activity" "Aktivität" (detailAmount category.Activity)
            Html.div [
                prop.key $"available-{flash.Count}"
                prop.custom ("data-flash", flash.On)
                prop.className ("-mx-1 rounded-md px-1 " + (if flash.On then Feedback.flash else ""))
                prop.children [ detailFigure "detail-available" "Verfügbar" (availableAmount category.Available) ]
            ]
        ]
    ]

let private detailEntryLine (entry: CategoryMonthEntry) (dispatch: Msg -> unit) =
    let payee = entry.Payee |> Option.defaultValue "Ohne Empfänger"
    let date = entry.Date.ToString "dd.MM.yyyy"
    let label = $"{payee}, {moneyText entry.Amount}, {date}, im Kassenbuch öffnen"
    Button.listRow (string entry.EntryId) "detail-entry" label (fun () -> dispatch (OpenEntryInLedger entry.EntryId)) [
        Html.span [
            prop.className ListRoles.mobileLine1
            prop.children [
                Truncate.text ListRoles.primary payee
                Money.listAmount entry.Amount.Amount entry.Amount.Currency
            ]
        ]
        Html.span [
            prop.className ListRoles.mobileLine2
            prop.children [
                Html.span [ prop.className ListRoles.date; prop.text date ]
                match entry.AccountName with
                | Some account ->
                    Html.span (Truncate.props ListRoles.secondary account @ [ prop.text $"· {account}" ])
                | None -> ()
                if entry.IsSplitShare then
                    Badge.neutral "Split"
            ]
        ]
    ]

let private detailTotal (activity: Money) (entries: CategoryMonthEntry list) =
    let total = entriesTotal activity entries
    Html.div [
        prop.className "flex flex-col gap-2 px-4 py-3"
        prop.children [
            Html.div [
                prop.className ListRoles.mobileLine1
                prop.children [
                    Html.span [ prop.className ListRoles.groupHeader; prop.text "Summe" ]
                    Html.span [ prop.custom ("data-testid", "detail-total"); prop.children [ Money.listAmount total.Amount total.Currency ] ]
                ]
            ]
            match deviationHint total activity with
            | Some hint ->
                Html.p [
                    prop.custom ("data-testid", "detail-deviation")
                    prop.className ListRoles.secondary
                    prop.text hint
                ]
            | None -> ()
        ]
    ]

let private detailEntries (category: BudgetCategory) (selected: SelectedCategory) (dispatch: Msg -> unit) =
    match selected.Entries with
    | NotAsked
    | Loading -> Loading.txListSkeleton 3
    | Failure message ->
        Html.div [
            prop.className "px-4"
            prop.children [ ErrorDisplay.card message (Some (fun () -> dispatch LoadCategoryEntries)) ]
        ]
    | Success entries ->
        React.fragment [
            if List.isEmpty entries then
                Html.p [
                    prop.custom ("data-testid", "detail-empty")
                    prop.className $"{ListRoles.muted} px-4 py-8 text-center"
                    prop.text "Keine Buchungen in diesem Monat"
                ]
            else
                Html.div [ prop.children (entries |> List.map (fun e -> detailEntryLine e dispatch)) ]
            detailTotal category.Activity entries
        ]

/// Eine Zeile im Aufbau von Verfügbar: Name links, Betrag rechts.
let private buildupLine (testId: string) (label: string) (amount: ReactElement) (isTotal: bool) =
    let lineClass =
        if isTotal then $"{ListRoles.mobileLine1} border-t border-border-subtle pt-2 mt-1"
        else ListRoles.mobileLine1
    let labelClass = if isTotal then ListRoles.primary else ListRoles.secondary
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className lineClass
        prop.children [
            Html.span [ prop.className labelClass; prop.text label ]
            amount
        ]
    ]

/// Aufbau von Verfügbar: Vortrag, Zugewiesen, Aktivität, Verfügbar. Rot nur ein Verfügbar im Minus.
let private availableBuildupView (category: BudgetCategory) =
    let buildup = availableBuildup category
    Html.div [
        prop.custom ("data-testid", "history-buildup")
        prop.className "flex flex-col gap-1"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text "Aufbau von Verfügbar" ]
            buildupLine "history-carry-over" "Vortrag" (detailAmount buildup.CarryOver) false
            buildupLine "history-assigned" "Zugewiesen" (detailAmount buildup.Assigned) false
            buildupLine "history-activity" "Aktivität" (detailAmount buildup.Activity) false
            buildupLine "history-available" "Verfügbar" (availableAmount buildup.Available) true
        ]
    ]

/// Ein Eintrag nach §7.11 (`HistoryList`, budgeting-yarty): Zeit, Kürzel, Text, Betrag; der Stand mit dem Grund eines
/// Fehlschlags steht als Zusatz darunter. Eine Pause und eine Änderung nur in BB haben keinen Stand.
let private historyEntryView (entry: HistoryEntry) =
    HistoryList.rowWithNote
        (entryItem entry)
        [ match entryStatus entry with
          | Some status ->
              Html.span [ prop.className "shrink-0"; prop.children [ historyStatusView status ] ]
              historyFailureWrapped status
          | None -> () ]

/// Der Verlauf der Kategorie im Monat: Bewegungen, Pause-Ereignisse und Target-Änderungen, neueste
/// zuerst. Der Leertext steht nur bei leerer Liste.
let private historyMovements (history: CategoryHistory) =
    Html.div [
        prop.className "flex flex-col px-4"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text "Verlauf" ]
            if List.isEmpty history.Entries then
                HistoryList.empty "Nichts festgehalten in diesem Monat"
            else
                HistoryList.listOf "history-list" (history.Entries |> List.map historyEntryView)
        ]
    ]

[<Literal>]
let private SpendingTitle = "Ausgaben der letzten 6 Monate"

/// Der Abschnitt der Ausgaben: Überschrift und darunter die Balken oder ihr Platzhalter.
let private spendingSection (bars: ReactElement) =
    Html.div [
        prop.custom ("data-testid", "history-spending")
        prop.className "flex flex-col gap-2 px-4"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text SpendingTitle ]
            bars
        ]
    ]

/// Ausgaben der letzten 6 Monate aus dem Kassenbuch, ohne waagrechtes Scrollen.
let private spendingView (currentMonth: string) (history: CategoryHistory) =
    spendingSection (Stats.monthBars SpendingTitle (spendingBars currentMonth history.Spending |> List.map spendingMonthBar))

/// Tab Historie: Aufbau von Verfügbar sofort aus dem Monat, Bewegungen und Ausgaben aus der Abfrage.
/// Beim Laden hält ein Platzhalter die Höhe der Balken.
let private historyTab (model: Model) (category: BudgetCategory) (selected: SelectedCategory) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "inspector-history")
        prop.className "flex flex-col gap-4 py-4"
        prop.children [
            // Die Historie kam nach einer Bewegung nicht neu (budgeting-gtrgk): ein Zustand des Tabs, kein Toast.
            match staleHistoryHint model with
            | Some hint ->
                Html.div [
                    prop.className "px-4"
                    prop.children [
                        Feedback.stateHint
                            "history-stale"
                            hint.Text
                            hint.Reasons
                            (Some { Label = "Neu laden"; OnClick = fun () -> dispatch RereadStaleHistory })
                            hint.Running
                    ]
                ]
            | None -> ()
            Html.div [ prop.className "px-4"; prop.children [ availableBuildupView category ] ]
            match selected.History with
            | NotAsked
            | Loading ->
                Loading.txListSkeleton 3
                spendingSection (Stats.monthBarsPlaceholder SpendingMonthCount)
            | Failure message ->
                Html.div [
                    prop.className "px-4"
                    prop.children [ ErrorDisplay.card message (Some (fun () -> dispatch LoadCategoryHistory)) ]
                ]
            | Success history ->
                historyMovements history
                match model.Frame with
                | Success frame -> spendingView frame.CurrentMonth history
                | _ -> ()
        ]
    ]

/// Tab Geld: Umschalter oben, darunter die Kennzahlen, dann der Modus.
let private moneyTab (model: Model) (month: BudgetMonth) (category: BudgetCategory) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "inspector-money")
        prop.className "flex flex-col gap-2 p-4"
        prop.children [
            match model.TabMoney with
            | Some tab ->
                moneySwitch tab dispatch
                detailFigures model.AvailableFlash category
                moneyMode model month category tab dispatch
            | None -> detailFigures model.AvailableFlash category
        ]
    ]

/// Status mit Quelle der Pause und Fehlbetrag unter dem Target.
let private targetStatus (category: BudgetCategory) =
    React.fragment [
        match targetStatusText category with
        | Some status -> detailFigure "target-status" "Status" (Html.span [ prop.className ListRoles.secondary; prop.text status ])
        | None -> ()
        match targetUnderfunded category with
        | Some under -> detailFigure "target-underfunded" "Fehlbetrag" (detailAmount under)
        | None -> ()
    ]

/// Das Target gegenüber YNAB (budgeting-e2z7v): gelb "Nicht in YNAB" mit Grund und "Erneut senden" (ADR 0087
/// Nachtrag), darunter gedämpft, was YNAB nicht zeigt, und an einem pausierten Target "In YNAB nicht gesnoozt".
/// Was erscheint, sagt `targetYnabHints`.
let private targetYnabView (model: Model) (category: BudgetCategory) (dispatch: Msg -> unit) =
    let hints = Components.Budget.TargetEdit.targetYnabHints category
    Html.div [
        prop.custom ("data-testid", "target-ynab")
        prop.className "flex flex-col gap-1"
        prop.children [
            match hints.NotInYnab with
            | Some reason ->
                Html.div [
                    prop.role "status"
                    prop.className "flex items-center gap-2 min-w-0"
                    prop.children [
                        Html.span [
                            prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.attention} font-medium shrink-0"
                            prop.text TargetNotInYnabText
                        ]
                        Truncate.text ListRoles.muted reason
                        Button.view
                            { Button.defaultProps with
                                Text = "Erneut senden"
                                Variant = Button.Ghost
                                Size = Button.Small
                                // Beschäftigt statt gesperrt, damit der Fokus am Knopf bleibt (Styleguide §5);
                                // gesperrt nur, solange eine andere Kategorie sendet.
                                IsBusy = Components.Budget.TargetEdit.targetRetryRunning category.Id model
                                IsDisabled =
                                    model.TargetYnabRetry.IsSome
                                    && not (Components.Budget.TargetEdit.targetRetryRunning category.Id model)
                                ClassName = Some "shrink-0"
                                OnClick = fun () -> dispatch (RetryTargetToYnab category.Id) }
                    ]
                ]
                // Scheiterte „Erneut senden“, steht der Grund rot unter der Markierung (Regel R3, budgeting-sx9v5).
                match Components.Budget.TargetEdit.targetRetryFailure category.Id model with
                | Some failure ->
                    Html.div [
                        prop.custom ("data-testid", "target-ynab-retry-error")
                        prop.children [ ErrorDisplay.inline' $"Target weiter nicht in YNAB: {failure}" ]
                    ]
                | None -> ()
            | None -> ()
            for gap in hints.Gaps do
                Html.p [ prop.key gap; prop.className ListRoles.muted; prop.text gap ]
            if hints.NotSnoozed then
                Html.p [ prop.className ListRoles.muted; prop.text TargetNotSnoozedText ]
        ]
    ]

/// Tab Target: im laufenden Monat das Formular (budgeting-k8wq3), sonst das Target als Text, dazu Status mit
/// Quelle der Pause, Fehlbetrag und derselbe Pause-Knopf wie im Tab Geld.
let private targetTab (model: Model) (category: BudgetCategory) (dispatch: Msg -> unit) =
    let pause = Components.Budget.PauseView.pauseControlView Components.Budget.PauseView.PauseInTargetTab model category dispatch
    Html.div [
        prop.custom ("data-testid", "inspector-target")
        prop.className "flex flex-col gap-4 p-4"
        prop.children [
            match Components.Budget.TargetEdit.targetTabMode model category with
            | Components.Budget.TargetEdit.NoTargetShown -> Components.Budget.TargetEditView.noTarget model category
            | Components.Budget.TargetEdit.TargetEditable (target, form) ->
                Components.Budget.TargetEditView.editableTarget model category (Some target) form (targetStatus category) pause dispatch
            | Components.Budget.TargetEdit.TargetCreatable form ->
                Components.Budget.TargetEditView.editableTarget model category None form Html.none Html.none dispatch
            | Components.Budget.TargetEdit.TargetReadOnly target ->
                Components.Budget.TargetEditView.readOnlyTarget model category target
                targetStatus category
                pause
            targetYnabView model category dispatch
        ]
    ]

/// Der Inhalt des gewählten Tabs, in Panel und Sheet derselbe.
let private inspectorContent (model: Model) (month: BudgetMonth) (selected: SelectedCategory) (category: BudgetCategory) (dispatch: Msg -> unit) =
    let activeIndex = inspectorTabs |> List.findIndex (fun (_, tab) -> tab = model.Tab)
    // Im Tab Geld steht die Zeile über dem Hauptknopf (`MovesView`); kommt sie, während ein anderer Tab offen ist (etwa
    // eine abgelehnte Pause), steht sie oben im Tab.
    let lineOnTop =
        if model.Tab <> MoneyTab && model.SheetLine.IsSome then
            Html.div [ prop.className "px-4 pt-3"; prop.children [ sheetLineView model dispatch ] ]
        else
            Html.none
    Tabs.panel "inspector-tabs" activeIndex [
        lineOnTop
        match model.Tab with
        | MoneyTab -> moneyTab model month category dispatch
        | EntriesTab ->
            Html.div [
                prop.custom ("data-testid", "category-detail")
                prop.children [ detailEntries category selected dispatch ]
            ]
        | HistoryTab -> historyTab model category selected dispatch
        | TargetTab -> targetTab model category dispatch
    ]

let private inspectorTabBar (model: Model) (dispatch: Msg -> unit) =
    Tabs.bar
        "inspector-tabs"
        "Bereiche der Kategorie"
        (inspectorTabs
         |> List.map (fun (label, value) ->
             { Label = label
               IsActive = (model.Tab = value)
               OnClick = fun () -> dispatch (SelectInspectorTab value) }))

/// `˄ ˅` im Kopf des Sheets, neben Schließen: wechselt die Kategorie ohne Schließen. Am Rand fehlt
/// der Pfeil, sein Platz bleibt frei.
let private sheetStepper (model: Model) (shown: BudgetGroup list) (dispatch: Msg -> unit) =
    let arrow testId label icon direction =
        match neighbourCategory direction shown model with
        | Some _ -> Button.sheetArrow testId label icon (fun () -> dispatch (StepCategory direction))
        | None -> Button.sheetArrowGap ()
    [ arrow "inspector-previous" "Vorherige Kategorie" (Icons.chevronUp Icons.SM Icons.Default) Previous
      arrow "inspector-next" "Nächste Kategorie" (Icons.chevronDown Icons.SM Icons.Default) Next ]

/// Der Pin-Knopf (budgeting-w9yt4) zwischen den Schrittpfeilen und Schließen, im Panel rechts im Kopf. Ohne geladene
/// Pins fehlt er: ein Umschalten ohne Stand überschriebe die gespeicherte Liste.
let private pinButton (pin: InspectorPin option) (category: BudgetCategory) =
    match pin with
    | Some pin ->
        Button.pinToggle "inspector-pin" (pin.IsPinned category.Id) (fun () -> pin.Toggle category.Id)
    | None -> Html.none

/// Offene Verschiebungen im festen Bereich des Sheets. Es deckt die Seite ab, der Hinweis dort wäre
/// nach Ablauf des Toasts nicht mehr bedienbar.
let private sheetRetries (model: Model) (dispatch: Msg -> unit) =
    if List.isEmpty model.OpenRetries then
        Html.none
    else
        Html.div [
            prop.className "flex flex-col gap-2 pb-2"
            prop.children [ moveRetries model model.OpenRetries dispatch ]
        ]

/// Die gewählte Kategorie im geladenen Monat, für Sheet und Panel.
let sheetCategory (model: Model) =
    match model.Selected, model.Month with
    | Some selected, Success month ->
        selectedCategory month selected |> Option.map (fun found -> selected, month, found)
    | _ -> None

/// Offene Verschiebungen auf der Seite. Steht das Sheet offen, trägt es den Hinweis: genau eine Stelle.
let pageRetries (isPanel: bool) (model: Model) : OpenMoveRetry list =
    if isPanel || (sheetCategory model).IsNone then model.OpenRetries else []

/// Was das Panel ohne gezeigte Kategorie trägt.
type EmptyPanel =
    | PanelLoading
    /// Das Laden ist gescheitert: es kommt kein Inhalt nach.
    | PanelBlank
    | PanelHint

let emptyPanel (model: Model) : EmptyPanel =
    match model.Selected, model.Month with
    | Some _, Loading -> PanelLoading
    | Some _, _ -> PanelBlank
    | None, _ -> PanelHint

/// Das Sheet unter 1024 px. Immer gerendert, damit es ein- und ausfährt. Beim Ausfahren hält
/// `BottomSheet.inspector` den letzten Inhalt.
let inspectorSheet (pin: InspectorPin option) (model: Model) (shown: BudgetGroup list) (dispatch: Msg -> unit) =
    let props title subtitle isOpen : BottomSheet.BottomSheetProps =
        { IsOpen = isOpen
          OnClose = fun () -> dispatch CloseInspector
          Title = title
          Subtitle = subtitle
          Footer = None }
    let weekdayChoice =
        sheetCategory model
        |> Option.bind (fun (_, _, (category, _)) -> Components.Budget.TargetEditView.weekdayChoiceInSheet model category dispatch)
    match sheetCategory model, model.TabMoney, weekdayChoice with
    // Die Gegenseite beim Verschieben: die Auswahl ersetzt den Inhalt des Sheets (§7.15, ledger-ea70s), Wahl oder
    // Schließen führen zurück. Betrag und Modus stehen im Modell und bleiben.
    | Some (_, month, (category, _)), Some tab, _ when model.Tab = MoneyTab && tab.Transfer.PickerOpen ->
        let picker = counterpartPickerProps model month tab dispatch
        BottomSheet.inspector
            { IsOpen = true
              OnClose = picker.OnClose
              Title = CategoryPicker.sheetTitle []
              // Wie an den anderen Stellen nennt die Unterzeile, wofür gewählt wird: die Kategorie im Monat.
              Subtitle = Some $"{category.Name} · {monthLabel month.Month}"
              Footer = None }
            []
            []
            [ CategoryPicker.sheetContent picker ]
    | Some (_, month, (category, _)), _, Some choice ->
        // Die Auswahl des Wochentags ersetzt den Inhalt des Sheets wie die Gegenseite (§7.15, budgeting-h2brj); Wahl,
        // „Zurück“ oder Schließen führen zum Tab Target zurück, die Eingaben stehen im Modell und bleiben.
        BottomSheet.inspector
            { IsOpen = true
              OnClose = fun () -> dispatch CloseTargetWeekdayChoice
              Title = "Wochentag"
              Subtitle = Some $"{category.Name} · {monthLabel month.Month}"
              Footer = None }
            []
            []
            [ choice ]
    | Some (selected, month, (category, groupName)), _, _ ->
        BottomSheet.inspector
            (props category.Name (Some $"{groupName} · {monthLabel month.Month}") true)
            (sheetStepper model shown dispatch @ [ pinButton pin category ])
            [ sheetRetries model dispatch
              inspectorTabBar model dispatch ]
            // `-mx-5` hebt den Rand von `.sheet-body` auf, die Zeilen tragen ihn selbst.
            [ Html.div [
                  prop.custom ("data-testid", "budget-inspector")
                  prop.className "-mx-5"
                  prop.children [ inspectorContent model month selected category dispatch ]
              ] ]
    | None, _, _ -> BottomSheet.inspector (props "" None false) [] [] []

/// Das Panel ab 1024 px. Der Hinweis steht ohne Auswahl da, die Höhe bleibt gleich. Beim Blättern
/// bleibt der Inhalt stehen. Lädt der Monat bei bestehender Auswahl, steht ein Skeleton da. Scheitert
/// das Laden, bleibt das Panel neben der Fehlerkarte leer: es kommt kein Inhalt nach.
let inspectorPanel (pin: InspectorPin option) (model: Model) (dispatch: Msg -> unit) =
    // Die Hülle ist dieselbe wie im Kassenbuch (design-system-gcpws, §7.6): unter dem Seitenkopf, ohne Karte, 30 % breit.
    Detail.panelWith "budget-inspector" "Kategorie" [
      Html.div [
        prop.className $"flex min-h-0 flex-1 flex-col {monthChangeClass model}"
        prop.children [
            match sheetCategory model with
            | Some (selected, month, (category, groupName)) ->
                Html.div [
                    prop.className "flex items-start gap-2 p-4 shrink-0"
                    prop.children [
                        Html.div [
                            prop.className "flex flex-col gap-1 flex-1 min-w-0"
                            prop.children [
                                Html.h2 (Truncate.textProps ListRoles.primary category.Name)
                                Truncate.text ListRoles.secondary $"{groupName} · {monthLabel month.Month}"
                            ]
                        ]
                        pinButton pin category
                    ]
                ]
                Html.div [ prop.className "shrink-0"; prop.children [ inspectorTabBar model dispatch ] ]
                Html.div [
                    prop.className "flex-1 min-h-0 overflow-y-auto"
                    prop.children [ inspectorContent model month selected category dispatch ]
                ]
            | None ->
                match emptyPanel model with
                | PanelLoading -> Loading.txListSkeleton 3
                | PanelBlank -> ()
                | PanelHint ->
                    Html.p [
                        prop.className $"{ListRoles.muted} p-4 m-auto text-center"
                        prop.text "Kategorie wählen"
                    ]
        ]
      ]
    ]
