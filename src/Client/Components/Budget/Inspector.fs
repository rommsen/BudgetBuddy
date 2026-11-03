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
        { model with
            Selected = Some { CategoryId = categoryId; Entries = NotAsked; ReturnPending = false }
            EntriesGeneration = model.EntriesGeneration + 1 }
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

/// Sind die Buchungen fällig? Nur im Tab Buchungen, bei geladenem Monat und `NotAsked`, nicht während
/// eines Monatswechsels. Das liest nur das Kassenbuch und kostet kein YNAB-Limit.
let entriesDue (model: Model) : (System.Guid * string) option =
    match model.Tab, model.Selected, model.Month with
    | EntriesTab, Some { CategoryId = id; Entries = NotAsked }, Success month when not (monthChanging model) ->
        Some (id, month.Month)
    | _ -> None

/// Setzt die Buchungen auf `Loading`, wenn sie fällig sind. Gibt die Abfrage für den Aufrufer zurück.
let requestEntries (model: Model) : Model * (System.Guid * string * int) option =
    match entriesDue model with
    | Some (id, month) ->
        let generation = model.EntriesGeneration + 1
        { model with
            Selected = model.Selected |> Option.map (fun s -> { s with Entries = Loading })
            EntriesGeneration = generation },
        Some (id, month, generation)
    | None -> model, None

/// Antwort der Liste. Eine Antwort einer älteren Abfrage oder ohne Auswahl fällt weg.
let applyCategoryEntries (generation: int) (result: Result<CategoryMonthEntry list, string>) (model: Model) : Model =
    match model.Selected with
    | Some selected when generation = model.EntriesGeneration ->
        let entries =
            match result with
            | Ok entries -> Success entries
            | Error message -> Failure message
        { model with Selected = Some { selected with Entries = entries } }
    | _ -> model

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
/// während die Seite verlassen war: Dann und bei der Rückkehr aus dem Kassenbuch stehen Auswahl, Tab,
/// Filter und Suche wieder da und die Liste kommt neu. Sonst beginnt die Seite von vorn, beim
/// laufenden Monat. Ein laufender Aufruf hält nichts fest, er liegt auf `Model`.
let enterPage (model: Model) : Model * PageEntry =
    let back, keepsTab = backOnPage model
    let entered = if keepsTab then back else { back with TabMoney = None }
    if returnPending model || keepsTab then
        let returned =
            resetEntries
                { entered with
                    Selected = entered.Selected |> Option.map (fun s -> { s with ReturnPending = false }) }
        returned, KeptPage
    else
        { entered with
            ChosenMonth = None
            CategorySearch = ""
            Filter = AllCategories
            Selected = None
            Tab = MoneyTab },
        FreshEntry
