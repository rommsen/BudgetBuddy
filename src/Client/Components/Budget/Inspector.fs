/// Auswahl, Tasten und Inspektor als reine Übergänge (budgeting-g8s3w).
module Components.Budget.Inspector

open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Client.DesignSystem
open Components.Budget.Month
open Components.Budget.Moves

/// Die gewählte Kategorie und ihr Gruppenname im Monat.
let selectedCategory (month: BudgetMonth) (selected: SelectedCategory) : (BudgetCategory * string) option =
    month.Groups
    |> List.tryPick (fun g ->
        g.Categories
        |> List.tryFind (fun c -> c.Id = selected.CategoryId)
        |> Option.map (fun c -> c, g.Name))

/// Summe der Liste, in der Währung der Kategorie (die Liste darf leer sein).
let entriesTotal (activity: Money) (entries: CategoryMonthEntry list) : Money =
    { activity with Amount = entries |> List.sumBy (fun e -> e.Amount.Amount) }

/// Der Hinweis unter der Summe, wenn sie von YNABs Aktivität (`activity`) abweicht, ohne Bewertung.
let deviationHint (total: Money) (activity: Money) : string option =
    if total.Amount = activity.Amount then
        None
    else
        Some $"Kassenbuch: {moneyText total}, YNAB: {moneyText activity}"

/// Der Hinweis im Detail der Kategorie: Summe der Buchungen gegen YNABs Aktivität des Monats aus dem YNAB-Stand
/// (`ActivityInYnab`, budgeting-m5pqn). `Activity` ist auf der Seite die des Kassenbuchs und taugt dafür nicht.
/// Ohne YNABs Aktivität gibt es keinen Hinweis.
let categoryDeviationHint (category: BudgetCategory) (entries: CategoryMonthEntry list) : string option =
    category.ActivityInYnab
    |> Option.bind (fun inYnab -> deviationHint (entriesTotal category.Activity entries) inYnab)

/// Kategorie wählen. Dieselbe Id ändert nichts, eine andere setzt die Buchungen zurück. Läuft die
/// Vorschau, geschieht nichts.
let selectCategory (categoryId: System.Guid) (model: Model) : Model =
    match model.Month, model.Targets with
    | Success month, TargetsClosed
        when model.Selected |> Option.forall (fun s -> s.CategoryId <> categoryId)
             && categoriesOf month |> List.exists (fun c -> c.Id = categoryId) ->
        resetTabData
            { model with Selected = Some { CategoryId = categoryId; Entries = NotAsked; History = NotAsked; ReturnPending = false } }
    | _ -> model

/// Die nächste sichtbare Zeile ab der Position von `current` in der vollen Reihenfolge. Am Rand
/// `None`. So läuft auch eine Kategorie, die aus dem Filter gefallen ist, von ihrem Platz aus weiter.
let stepCategory
    (direction: Direction)
    (all: System.Guid list)
    (visible: System.Guid list)
    (current: System.Guid)
    : System.Guid option =
    let candidates =
        match direction with
        | Next -> all |> List.skipWhile (fun id -> id <> current) |> List.filter (fun id -> id <> current)
        | Previous -> all |> List.takeWhile (fun id -> id <> current) |> List.rev
    candidates |> List.tryFind (fun id -> List.contains id visible)

let private groupIds (groups: BudgetGroup list) : System.Guid list =
    groups |> List.collect (fun g -> g.Categories |> List.map (fun c -> c.Id))

/// Die Nachbar-Kategorie der Auswahl in dieser Richtung; `None` am Rand. Der Knopf fehlt dann.
let neighbourCategory (direction: Direction) (shown: BudgetGroup list) (model: Model) : System.Guid option =
    match model.Selected, model.Month with
    | Some selected, Success month ->
        stepCategory direction (groupIds month.Groups) (groupIds shown) selected.CategoryId
    | _ -> None

/// `StepCategory`: wechselt zur Nachbar-Kategorie.
let stepSelection (direction: Direction) (model: Model) : Model =
    match model.Month with
    | Success month ->
        let shown = shownGroups model.Filter model.CategorySearch month.Groups
        match neighbourCategory direction shown model with
        | Some id -> selectCategory id model
        | None -> model
    | _ -> model

/// Was die Budget-Liste kann (§7.8): keine Mehrfachauswahl, Tabs im Panel, eine Suche, in die getippt wird.
let budgetListTraits : ListKeys.Traits = { MultiSelect = false; Tabs = true; Search = true; TypeToSearch = true }

let private listFocus (focus: KeyFocus) : ListKeys.Focus =
    match focus with
    | TextField -> ListKeys.InTextField
    | SearchField -> ListKeys.InSearch
    | OtherFocus -> ListKeys.Elsewhere

/// Steht die Auswahl des Wochentags im Tab Target offen (budgeting-h2brj)? Dann ist sie die oberste Ebene (§7.8):
/// die Liste ruht, Esc schließt nur sie.
let targetWeekdayChoiceOpen (model: Model) : bool =
    model.Tab = TargetTab && model.TargetForm |> Option.exists (fun f -> f.WeekdayChoiceOpen)

/// Die gemeinsame Tastenregel (`ListKeys.actionFor`, design-system-gcpws) im Zustand der Seite: Targets anwenden,
/// Targets nach YNAB, das Menü ⋮ und der offene Picker der Gegenseite liegen als Ebene darüber und behalten die
/// Tasten für sich; die gewählte Kategorie ist die gewählte Zeile und das, was im Panel offen ist. Ein druckbares
/// Zeichen ohne Feldfokus geht in die Suche (`TypeIntoSearch`, §7.8 Punkt 5), die Leertaste nicht.
let pageListAction (model: Model) (key: string) (focus: KeyFocus) (shift: bool) (command: bool) (handled: bool) : ListKeys.Action =
    ListKeys.actionFor
        budgetListTraits
        { Key = key
          Focus = listFocus focus
          Command = command
          Shift = shift
          Handled = handled
          LayerOpen =
              model.Targets <> TargetsClosed
              || model.TargetsToYnab <> TargetsToYnabClosed
              || model.MenuOpen
              || pickerOpen model
              || targetWeekdayChoiceOpen model
          RowSelected = model.Selected.IsSome
          PanelOpen = model.Selected.IsSome
          SelectionActive = false }

/// Was die Regel auf der Budget-Seite auslöst. Die Seite hat keine eigenen Tasten davor (§7.8).
let pageKeyOf (action: ListKeys.Action) : PageKeyAction =
    match action with
    | ListKeys.FirstRow -> FirstRow
    | ListKeys.LastRow -> LastRow
    | ListKeys.StepRow ListKeys.Forward -> StepRow Next
    | ListKeys.StepRow ListKeys.Backward -> StepRow Previous
    | ListKeys.StepTab ListKeys.Forward -> StepTab Next
    | ListKeys.StepTab ListKeys.Backward -> StepTab Previous
    | ListKeys.ClosePanel -> ClosePanelKey
    | ListKeys.ExtendSelection _
    | ListKeys.ClearSelection
    | ListKeys.FocusSearch
    | ListKeys.TypeIntoSearch
    | ListKeys.Leave -> LeaveKey

/// Der Nachbar-Tab, an den Rändern bleibt er stehen.
let private neighbourTab (direction: Direction) (tab: InspectorTab) : InspectorTab =
    let order = inspectorTabs |> List.map snd
    let index = order |> List.findIndex ((=) tab)
    let target =
        match direction with
        | Next -> index + 1
        | Previous -> index - 1
    order |> List.tryItem target |> Option.defaultValue tab

let private selectRow (pick: System.Guid list -> System.Guid option) (model: Model) : Model =
    match model.Month with
    | Success month ->
        match pick (groupIds (shownGroups model.Filter model.CategorySearch month.Groups)) with
        | Some id -> selectCategory id model
        | None -> model
    | _ -> model

/// `PageKeyPressed`: führt die Regel aus. Gewechselt wird zwischen den sichtbaren Zeilen.
let applyPageKey (action: PageKeyAction) (model: Model) : Model =
    match action with
    | StepRow direction -> stepSelection direction model
    | FirstRow -> selectRow List.tryHead model
    | LastRow -> selectRow List.tryLast model
    | StepTab direction -> { model with Tab = neighbourTab direction model.Tab }
    // Esc schließt den Inspektor am PC (design-system-gcpws), wie `CloseInspector`.
    | ClosePanelKey -> { model with Selected = None }
    | LeaveKey -> model

/// Wie Tab-Daten geholt werden.
type TabFetch =
    /// Bis zur Antwort steht `Loading`, ein Fehler steht im Tab.
    | ShownFetch
    /// Die geladenen Daten bleiben stehen, bis die Antwort sie ersetzt; ein Fehler wird zum Hinweis im Tab
    /// (die Historie nach einer Bewegung, wie `QuietReread` beim Monat).
    | QuietFetch

/// Eine Abfrage von Tab-Daten: welcher Tab, für welche Kategorie und welchen Monat, zu welchem Stand.
type TabRequest = {
    Tab: InspectorTab
    CategoryId: System.Guid
    Month: string
    /// Die Generation des Tabs beim Start (`EntriesGeneration` oder `HistoryGeneration`).
    Generation: int
    Fetch: TabFetch
}

/// Sind Daten des sichtbaren Tabs fällig? Buchungen und Historie, je bei geladenem Monat und `NotAsked`,
/// nicht während eines Monatswechsels. Beide lesen nur die Datenbank und kosten kein YNAB-Limit.
let tabDataDue (model: Model) : (InspectorTab * System.Guid * string) option =
    match model.Selected, model.Month with
    | Some selected, Success month when not (monthChanging model) ->
        match model.Tab, selected.Entries, selected.History with
        | EntriesTab, NotAsked, _
        | HistoryTab, _, NotAsked -> Some (model.Tab, selected.CategoryId, month.Month)
        | _ -> None
    | _ -> None

/// Die Generation eines Tabs mit nachgeladenen Daten.
let private generationOf (tab: InspectorTab) (model: Model) : int =
    match tab with
    | HistoryTab -> model.HistoryGeneration
    | EntriesTab
    | MoneyTab
    | TargetTab -> model.EntriesGeneration

/// Setzt die fälligen Tab-Daten auf `Loading`. Gibt die Abfrage für den Aufrufer zurück. Der Stand
/// bleibt: Er wechselt erst, wenn Kategorie oder Monat wechseln oder Daten neu fällig werden.
let requestTabData (model: Model) : Model * TabRequest option =
    match tabDataDue model with
    | Some (tab, id, month) ->
        let loading (s: SelectedCategory) =
            match tab with
            | HistoryTab -> { s with History = Loading }
            | _ -> { s with Entries = Loading }
        { model with Selected = model.Selected |> Option.map loading },
        Some { Tab = tab; CategoryId = id; Month = month; Generation = generationOf tab model; Fetch = ShownFetch }
    | None -> model, None

/// Nach einer Bewegung ist die Historie neu fällig. Steht sie geladen im sichtbaren Tab, bleibt sie
/// stehen und kommt still neu (`QuietFetch`): der Tab springt nicht auf das Skelett. Sonst ist sie
/// wieder ungefragt und kommt, sobald ihr Tab zu sehen ist. Die Buchungen bleiben in jedem Fall.
let historyAfterMovement (model: Model) : Model * TabRequest option =
    match model.Selected, model.Month with
    | Some ({ History = Success _ } as selected), Success month when model.Tab = HistoryTab && not (monthChanging model) ->
        let next = { model with HistoryGeneration = model.HistoryGeneration + 1 }
        next,
        Some
            { Tab = HistoryTab
              CategoryId = selected.CategoryId
              Month = month.Month
              Generation = next.HistoryGeneration
              Fetch = QuietFetch }
    | _ -> resetTab HistoryTab model, None

/// Die Antwort einer Abfrage der Buchungen, gebunden an die Generation der Anfrage. Erfolg und
/// Scheitern gehen denselben Weg.
let entriesRequestAnswer (request: TabRequest) : Result<CategoryMonthEntry list, string> -> Msg =
    fun result -> CategoryEntriesLoaded (request.Generation, result)

/// Die Antwort einer Abfrage der Historie, wie `entriesRequestAnswer`. Die stille Abfrage antwortet über
/// `CategoryHistoryReloaded`.
let historyRequestAnswer (request: TabRequest) : Result<CategoryHistory, string> -> Msg =
    match request.Fetch with
    | ShownFetch -> fun result -> CategoryHistoryLoaded (request.Generation, result)
    | QuietFetch -> fun result -> CategoryHistoryReloaded (request.Generation, result)

/// Eine Antwort zählt nur zum aktuellen Stand ihres Tabs und bei bestehender Auswahl.
let private applyTabAnswer
    (current: int)
    (generation: int)
    (fill: SelectedCategory -> SelectedCategory)
    (model: Model)
    : Model =
    match model.Selected with
    | Some selected when generation = current -> { model with Selected = Some (fill selected) }
    | _ -> model

let private remote (result: Result<'T, string>) : RemoteData<'T> =
    match result with
    | Ok value -> Success value
    | Error message -> Failure message

/// Antwort der Liste. Eine Antwort einer älteren Abfrage oder ohne Auswahl fällt weg.
let applyCategoryEntries (generation: int) (result: Result<CategoryMonthEntry list, string>) (model: Model) : Model =
    applyTabAnswer model.EntriesGeneration generation (fun s -> { s with Entries = remote result }) model

/// Antwort der Historie, wie `applyCategoryEntries`. Eine gezählte Antwort beendet den Hinweis „Historie nicht neu
/// gelesen“: gelesen oder als Fehlerkarte mit Wiederholen im Tab.
let applyCategoryHistory (generation: int) (result: Result<CategoryHistory, string>) (model: Model) : Model =
    let answered = applyTabAnswer model.HistoryGeneration generation (fun s -> { s with History = remote result }) model
    if generation = model.HistoryGeneration && model.Selected.IsSome then { answered with StaleHistory = None }
    else answered

/// Antwort des stillen Nachladens der Historie. Scheitert es, bleibt der alte Stand und der Hinweis oben im Tab
/// Historie nennt den Grund (`StaleHistory`, budgeting-gtrgk), wie `applyReloaded` beim Monat. Stand keine Historie
/// mehr da, gilt `applyCategoryHistory`.
let applyCategoryHistoryReloaded (generation: int) (result: Result<CategoryHistory, string>) (model: Model) : Model =
    match model.Selected, result with
    | Some { History = Success _ }, Error message when generation = model.HistoryGeneration ->
        { model with StaleHistory = Some (ReadFailed message) }
    | _ -> applyCategoryHistory generation result model

/// „Neu laden“ am Hinweis im Tab Historie: die Historie still neu holen wie nach einer Bewegung. Läuft es schon oder
/// steht die Historie nicht im sichtbaren Tab, geschieht nichts.
let startStaleHistoryReread (model: Model) : Model * TabRequest option =
    match model.StaleHistory with
    | Some (ReadFailed reason) ->
        match historyAfterMovement model with
        | next, Some request -> { next with StaleHistory = Some (Rereading reason) }, Some request
        | _ -> model, None
    | _ -> model, None

/// Der Stand einer Bewegung im Tab Historie.
let historyStatusText (status: HistoryStatus) : string =
    match status with
    | HistoryWritten -> "geschrieben"
    | HistoryFailed _ -> "fehlgeschlagen"
    | HistoryChangedInYnab -> "in YNAB geändert"
    | HistoryReplacedInBb -> "in BB ersetzt"
    | HistoryWithdrawnInBb -> "in BB zurückgezogen"
    | HistoryOpen -> "noch offen"
    | HistoryPartlyWritten _ -> "teilweise geschrieben"

/// Der Grund einer fehlgeschlagenen Bewegung, neben dem Badge als eigener Text. Ohne Grund nichts.
let historyFailureReason (status: HistoryStatus) : string option =
    match status with
    | HistoryFailed reason
    | HistoryPartlyWritten reason when not (System.String.IsNullOrWhiteSpace reason) -> Some reason
    | HistoryPartlyWritten _
    | HistoryFailed _
    | HistoryWritten
    | HistoryChangedInYnab
    | HistoryReplacedInBb
    | HistoryWithdrawnInBb
    | HistoryOpen -> None

/// Die Vorzeichenregel der Ausgaben: eine negative Ausgabe ist ein Erstattungsüberschuss und steht als
/// Betrag mit Plus da, eine positive ohne Vorzeichen. `format` bekommt den Betrag ohne Vorzeichen.
let private spendingWithSign (amount: decimal) (format: decimal -> string) : string =
    if amount < 0m then $"+{format -amount}" else format amount

/// Der Betrag unter einem Balken in ganzen Euro ohne Währung ("1.235"): sechs Balken passen so am Handy
/// nebeneinander. Der volle Betrag steht im Label des Balkens. Überwiegen Erstattungen, ist die Ausgabe
/// negativ und steht mit Plus da ("+85"): es kam mehr zurück, als ausgegeben wurde (budgeting-v6m3z).
let barAmountText (m: Money) : string =
    let rounded = roundHalfUp m.Amount
    spendingWithSign rounded (fun amount ->
        let text = Money.formatAmountText false false amount m.Currency
        text.Substring(0, text.Length - 3))

/// Der genaue Betrag eines Monats in der Wertzeile, ein Erstattungsüberschuss mit Plus ("+85,00 €").
let private spendingText (m: Money) : string =
    spendingWithSign m.Amount (fun amount -> moneyText { m with Amount = amount })

/// Ein Balken der Ausgaben als DS-Baustein. Das Label steht in der Wertzeile über den Balken und wird
/// vorgelesen: Monat, genauer Betrag und beim laufenden Monat "läuft noch".
let spendingMonthBar (bar: SpendingBar) : Stats.MonthBar =
    let running = bar.Kind = CurrentMonth
    let state = if running then " · läuft noch" else ""
    { Key = bar.Month
      Label = $"{monthLabel bar.Month} · {spendingText bar.Spending}{state}"
      MonthText = shortMonthLabel bar.Month
      AmountText = barAmountText bar.Spending
      Percent = bar.Percent
      Running = running }

/// Text und Klassen des Betrags einer Bewegungszeile (budgeting-h7q4v, H2): mit Vorzeichen ("+20,00 €",
/// "-20,00 €"), eine Null ohne ("0,00 €"). Die Bewegungsliste ist eine ruhige Liste (ADR 0049): der
/// Betrag neutral, das Stand-Badge ist der einzige Farbakzent der Zeile. Die View rendert nur das
/// Ergebnis.
let historyChangeAmount (change: Money) : string * string =
    let text =
        if change.Amount = 0m then Money.formatAmountText false true change.Amount change.Currency
        else Money.signedListAmountText change.Amount change.Currency
    text, Money.signedListAmountClass

/// Das Kürzel der Herkunft einer Historie-Zeile (§7.11, budgeting-yarty): „BB“ für eine Entscheidung in BB,
/// „YNAB“ für Beobachtetes und Übernommenes.
[<Literal>]
let WhoBb = "BB"

[<Literal>]
let WhoYnab = "YNAB"

let originWho (origin: HistoryOrigin) : string =
    if isFromYnab origin then WhoYnab else WhoBb

/// Das Kürzel eines Eintrags im Tab Historie. Pausen und Target-Änderungen entstehen in BB.
let entryWho (entry: HistoryEntry) : string =
    match entry with
    | Movement m -> originWho m.Origin
    | TargetPaused _
    | PauseLifted _
    | TargetChanged _ -> WhoBb

/// Der Stand eines Eintrags als Zusatz unter dem Text: `None` bei allem, was nie nach YNAB geht.
let entryStatus (entry: HistoryEntry) : HistoryStatus option = (historyEntryLine entry).Status

/// Ein Eintrag im Tab Historie als Zeile nach §7.11: Zeit, Kürzel, Text, Betrag mit Vorzeichen.
let entryItem (entry: HistoryEntry) : HistoryList.Item =
    let line = historyEntryLine entry
    { Key = string line.EntryId
      When = historyTimeText line.OccurredAt
      Who = entryWho entry
      Text = line.Text
      Amount = line.Change |> Option.map (historyChangeAmount >> fst) }

/// Tipp auf eine Buchung: die Auswahl bleibt stehen und wartet auf die Rückkehr.
let leaveForEntry (model: Model) : Model =
    { model with Selected = model.Selected |> Option.map (fun s -> { s with ReturnPending = true }) }

let returnPending (model: Model) : bool =
    model.Selected |> Option.exists (fun s -> s.ReturnPending)

/// Wie die Seite betreten wurde.
type PageEntry =
    /// Neuer Seitenaufruf: Rahmen und Monat lesen.
    | FreshEntry
    /// Die Seite bleibt, wie sie war (Rückkehr aus dem Kassenbuch, ungelesene Absage): nur Monat und
    /// Buchungen, kein YNAB-Aufruf für den Rahmen.
    | KeptPage

/// Roman kehrt zurück: Ein Aufruf läuft weiter unter seinen Augen, eine ungelesene Absage gilt ab jetzt
/// als gelesen. `true`: es gab eine, sie hält den Tab fest.
let private backOnPage (model: Model) : Model * bool =
    let watched =
        match model.MoveInFlight with
        | MoveCallRunningAway -> { model with MoveInFlight = MoveCallRunning }
        | _ -> model
    match watched.TabMoney with
    | Some ({ Transfer = { Submission = RejectedAway message } } as tab) ->
        { watched with TabMoney = Some { tab with Transfer = { tab.Transfer with Submission = Rejected message } } }, true
    | _ -> watched, false

/// Die Seite wird betreten. Der Tab Geld überlebt den Seitenwechsel nicht, außer eine Absage kam,
/// während die Seite verlassen war: Dann, bei der Rückkehr aus dem Kassenbuch, aus der allgemeinen
/// Historie und aus Targets prüfen (budgeting-q4v8n) stehen Auswahl, Tab, Filter und Suche wieder da und die Liste kommt neu. Sonst beginnt die Seite von vorn, beim
/// laufenden Monat. Ein laufender Aufruf hält nichts fest, er liegt auf `Model`.
let enterPage (model: Model) : Model * PageEntry =
    let back, keepsTab = backOnPage model
    let entered = if keepsTab then back else { back with TabMoney = None }
    if returnPending model || keepsTab || model.GeneralHistory.ReturnPending || model.TargetCheck.ReturnPending then
        let returned =
            resetTabData
                { entered with
                    Selected = entered.Selected |> Option.map (fun s -> { s with ReturnPending = false })
                    GeneralHistory = { entered.GeneralHistory with ReturnPending = false }
                    TargetCheck = { entered.TargetCheck with ReturnPending = false }
                    Filter = entered.EntryFilter |> Option.defaultValue entered.Filter
                    EntryFilter = None }
        returned, KeptPage
    else
        { entered with
            ChosenMonth = None
            CategorySearch = ""
            Filter = entered.EntryFilter |> Option.defaultValue AllCategories
            EntryFilter = None
            Selected = None
            Tab = MoneyTab },
        FreshEntry
