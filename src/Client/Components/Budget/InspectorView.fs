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

let private detailFigure (testId: string) (label: string) (value: ReactElement) =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className "flex flex-col gap-1 min-w-0"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text label ]
            value
        ]
    ]

/// Aktivität und Verfügbar. Zugewiesen ist das Feld darunter.
let private detailFigures (category: BudgetCategory) =
    Html.div [
        prop.className "grid grid-cols-2 gap-3"
        prop.children [
            detailFigure "detail-activity" "Aktivität" (detailAmount category.Activity)
            detailFigure "detail-available" "Verfügbar" (availableAmount category.Available)
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
                Html.span [ prop.className $"{ListRoles.primary} truncate min-w-0"; prop.text payee ]
                Money.listAmount entry.Amount.Amount entry.Amount.Currency
            ]
        ]
        Html.span [
            prop.className ListRoles.mobileLine2
            prop.children [
                Html.span [ prop.className ListRoles.date; prop.text date ]
                match entry.AccountName with
                | Some account ->
                    Html.span [ prop.className $"{ListRoles.secondary} truncate"; prop.text $"· {account}" ]
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

/// Tab Geld: Umschalter oben, darunter die Kennzahlen, dann der Modus.
let private moneyTab (model: Model) (month: BudgetMonth) (category: BudgetCategory) (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "inspector-money")
        prop.className "flex flex-col gap-2 p-4"
        prop.children [
            match model.TabMoney with
            | Some tab ->
                moneySwitch tab dispatch
                detailFigures category
                moneyMode model month category tab dispatch
            | None -> detailFigures category
        ]
    ]

/// Tab Target, nur lesend: Pflege kommt in budgeting-t6p8z.
let private targetTab (category: BudgetCategory) =
    Html.div [
        prop.custom ("data-testid", "inspector-target")
        prop.className "flex flex-col gap-4 p-4"
        prop.children [
            match category.Target with
            | None -> Html.p [ prop.className ListRoles.muted; prop.text "Kein Target" ]
            | Some target ->
                detailFigure "target-goal" "Target" (Html.span [ prop.className ListRoles.primary; prop.text (targetText target) ])
                match targetStatusText category with
                | Some status -> detailFigure "target-status" "Status" (Html.span [ prop.className ListRoles.secondary; prop.text status ])
                | None -> ()
                match targetUnderfunded category with
                | Some under -> detailFigure "target-underfunded" "Fehlbetrag" (detailAmount under)
                | None -> ()
        ]
    ]

/// Der Inhalt des gewählten Tabs, in Panel und Sheet derselbe.
let private inspectorContent (model: Model) (month: BudgetMonth) (selected: SelectedCategory) (category: BudgetCategory) (dispatch: Msg -> unit) =
    let activeIndex = inspectorTabs |> List.findIndex (fun (_, tab) -> tab = model.Tab)
    Tabs.panel "inspector-tabs" activeIndex [
        match model.Tab with
        | MoneyTab -> moneyTab model month category dispatch
        | EntriesTab ->
            Html.div [
                prop.custom ("data-testid", "category-detail")
                prop.children [ detailEntries category selected dispatch ]
            ]
        | HistoryTab ->
            Html.p [
                prop.custom ("data-testid", "inspector-history")
                prop.className $"{ListRoles.muted} p-4"
                prop.text "Die Historie dieser Kategorie folgt."
            ]
        | TargetTab -> targetTab category
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
let inspectorSheet (model: Model) (shown: BudgetGroup list) (dispatch: Msg -> unit) =
    let props title subtitle isOpen : BottomSheet.BottomSheetProps =
        { IsOpen = isOpen
          OnClose = fun () -> dispatch CloseInspector
          Title = title
          Subtitle = subtitle
          Footer = None }
    match sheetCategory model with
    | Some (selected, month, (category, groupName)) ->
        BottomSheet.inspector
            (props category.Name (Some $"{groupName} · {monthLabel month.Month}") true)
            (sheetStepper model shown dispatch)
            [ sheetRetries model dispatch
              inspectorTabBar model dispatch ]
            // `-mx-5` hebt den Rand von `.sheet-body` auf, die Zeilen tragen ihn selbst.
            [ Html.div [
                  prop.custom ("data-testid", "budget-inspector")
                  prop.className "-mx-5"
                  prop.children [ inspectorContent model month selected category dispatch ]
              ] ]
    | None -> BottomSheet.inspector (props "" None false) [] [] []

/// Das Panel ab 1024 px. Der Hinweis steht ohne Auswahl da, die Höhe bleibt gleich. Beim Blättern
/// bleibt der Inhalt stehen. Lädt der Monat bei bestehender Auswahl, steht ein Skeleton da. Scheitert
/// das Laden, bleibt das Panel neben der Fehlerkarte leer: es kommt kein Inhalt nach.
let inspectorPanel (model: Model) (dispatch: Msg -> unit) =
    Html.aside [
        prop.custom ("data-testid", "budget-inspector")
        prop.className $"budget-inspector-panel flex flex-col rounded-xl border border-border-subtle bg-surface-card overflow-hidden {monthChangeClass model}"
        prop.children [
            match sheetCategory model with
            | Some (selected, month, (category, groupName)) ->
                Html.div [
                    prop.className "flex flex-col gap-1 p-4 shrink-0"
                    prop.children [
                        Html.h2 [ prop.className $"{ListRoles.primary} truncate"; prop.text category.Name ]
                        Html.span [ prop.className $"{ListRoles.secondary} truncate"; prop.text $"{groupName} · {monthLabel month.Month}" ]
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
