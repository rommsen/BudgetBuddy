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

/// Der Hinweis unter der Summe, wenn sie von YNABs Aktivität abweicht, ohne Bewertung.
let deviationHint (total: Money) (activity: Money) : string option =
    if total.Amount = activity.Amount then
        None
    else
        Some $"Kassenbuch: {moneyText total}, YNAB: {moneyText activity}"

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

/// Die Regel der Pfeiltasten ab 1024 px. In `TextField` (Zugewiesen und Betrag) gehören sie dem
/// Cursor. Im Suchfeld gelten alle vier Richtungen, damit man aus der Suche heraus Kategorie und Tab
/// wechseln kann, ohne zur Maus zu greifen; ohne Auswahl bleiben Links und Rechts beim Cursor, denn
/// dann gibt es keinen Tab zu wechseln.
let pageKeyAction (hasSelection: bool) (key: string) (focus: KeyFocus) : PageKeyAction =
    match focus, key with
    | TextField, _ -> LeaveKey
    | _, "ArrowDown" -> if hasSelection then StepRow Next else FirstRow
    | _, "ArrowUp" -> if hasSelection then StepRow Previous else LastRow
    | _, "ArrowRight" when hasSelection -> StepTab Next
    | _, "ArrowLeft" when hasSelection -> StepTab Previous
    | _ -> LeaveKey

/// `pageKeyAction` im Zustand der Seite: Targets anwenden und der offene Picker der Gegenseite
/// behalten die Tasten für sich.
let pageKeyActionFor (model: Model) (key: string) (focus: KeyFocus) : PageKeyAction =
    match model.Targets with
    | TargetsClosed when not (pickerOpen model) -> pageKeyAction model.Selected.IsSome key focus
    | _ -> LeaveKey

/// Tippen ohne Klick in die Suche: ein druckbares Zeichen, während kein Eingabefeld den Fokus hat,
/// geht in die Suche. Die Leertaste nicht, sie gehört dem Knopf mit Fokus und dem Scrollen. Bei
/// offenem Dialog oder Picker gehören die Tasten dort hin.
let typesIntoSearch (model: Model) (key: string) (focus: KeyFocus) : bool =
    focus = OtherFocus
    && key.Length = 1
    && key <> " "
    && model.Targets = TargetsClosed
    && not (pickerOpen model)

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
    | LeaveKey -> model

/// Wie Tab-Daten geholt werden.
type TabFetch =
    /// Bis zur Antwort steht `Loading`, ein Fehler steht im Tab.
    | ShownFetch
    /// Die geladenen Daten bleiben stehen, bis die Antwort sie ersetzt; ein Fehler wird zum Warn-Toast
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

/// Antwort der Historie, wie `applyCategoryEntries`.
let applyCategoryHistory (generation: int) (result: Result<CategoryHistory, string>) (model: Model) : Model =
    applyTabAnswer model.HistoryGeneration generation (fun s -> { s with History = remote result }) model

/// Antwort des stillen Nachladens der Historie. Scheitert es, bleibt der alte Stand und ein Hinweis
/// nennt den Grund, wie `applyReloaded` beim Monat. Stand keine Historie mehr da, gilt `applyCategoryHistory`.
let applyCategoryHistoryReloaded
    (generation: int)
    (result: Result<CategoryHistory, string>)
    (model: Model)
    : Model * ExternalMsg =
    match model.Selected, result with
    | Some { History = Success _ }, Error message when generation = model.HistoryGeneration ->
        model, ShowToast ($"Historie nicht neu gelesen, sie kann veraltet sein: {message}", ToastWarning)
    | _ -> applyCategoryHistory generation result model, NoOp

/// Der Stand einer Bewegung im Tab Historie.
let historyStatusText (status: HistoryStatus) : string =
    match status with
    | HistoryWritten -> "geschrieben"
    | HistoryFailed _ -> "fehlgeschlagen"
    | HistoryChangedInYnab -> "in YNAB geändert"
    | HistoryReplacedInBb -> "in BB ersetzt"
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
    | HistoryOpen -> None

/// Die Vorzeichenregel der Ausgaben: eine negative Ausgabe ist ein Erstattungsüberschuss und steht als
/// Betrag mit Plus da, eine positive ohne Vorzeichen. `format` bekommt den Betrag ohne Vorzeichen.
let private spendingWithSign (amount: decimal) (format: decimal -> string) : string =
    if amount < 0m then $"+{format -amount}" else format amount

/// Der Betrag unter einem Balken in ganzen Euro ohne Währung ("1.235"): sechs Balken passen so am Handy
/// nebeneinander. Der volle Betrag steht im Label des Balkens. Überwiegen Erstattungen, ist die Ausgabe
/// negativ und steht mit Plus da ("+85"): es kam mehr zurück, als ausgegeben wurde (budgeting-v6m3z).
let barAmountText (m: Money) : string =
    let rounded = System.Math.Round(m.Amount, 0, System.MidpointRounding.AwayFromZero)
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
                    TargetCheck = { entered.TargetCheck with ReturnPending = false } }
        returned, KeptPage
    else
        { entered with
            ChosenMonth = None
            CategorySearch = ""
            Filter = AllCategories
            Selected = None
            Tab = MoneyTab },
        FreshEntry
