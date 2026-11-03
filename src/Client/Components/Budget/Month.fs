/// Der Monat der Budget-Seite: Laden, Blättern, Rahmen, Texte und die Zeilen der Liste.
module Components.Budget.Month

open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Client.DesignSystem

let categoriesOf (month: BudgetMonth) : BudgetCategory list =
    month.Groups |> List.collect (fun g -> g.Categories)

/// Eine neue Abfrage des Monats beginnt: ältere Antworten zählen ab jetzt nicht mehr.
let requestMonth (model: Model) : Model =
    { model with MonthGeneration = model.MonthGeneration + 1 }

/// Der gezeigte Monat: der gewählte, sonst der laufende aus dem Rahmen.
let shownMonth (model: Model) : string option =
    match model.ChosenMonth, model.Frame with
    | Some month, _ -> Some month
    | None, Success frame -> Some frame.CurrentMonth
    | None, _ -> None

/// Die Monatsart des gezeigten Monats; `None` ohne Rahmen.
let shownMonthKind (model: Model) : MonthKind option =
    match model.Frame, shownMonth model with
    | Success frame, Some month -> Some (monthKind frame.CurrentMonth month)
    | _ -> None

/// Läuft ein Monatswechsel? `Month` zeigt noch den alten Monat, `shownMonth` schon den neuen. Das
/// folgt aus beiden Feldern, ein eigener Merker könnte auseinanderlaufen.
let monthChanging (model: Model) : bool =
    match model.Month, shownMonth model with
    | Success month, Some shown -> month.Month <> shown
    | _ -> false

/// Der Monat ist bis zur Antwort einer neuen Abfrage veraltet (`MonthFreshness`).
let awaitFreshMonth (model: Model) : Model =
    requestMonth { model with Freshness = MonthStale }

/// Der alte Stand ist keine Rechengrundlage mehr. Bis zur Antwort steht `Loading`: Es gibt kein Feld
/// und keinen Knopf. Scheitert sie, steht die Fehlerkarte mit Wiederholen. Ein Monatswechsel sperrt
/// schon selbst, die alte Liste bleibt stehen.
let awaitMonth (model: Model) : Model =
    awaitFreshMonth (if monthChanging model then model else { model with Month = Loading })

/// Die Monatsart des Monats, den die Seite gerade darstellt. Während eines Wechsels ist das noch der
/// alte: der Knopf "Targets anwenden" springt nicht vor der Antwort.
let displayedMonthKind (model: Model) : MonthKind option =
    match model.Frame, model.Month with
    | Success frame, Success month -> Some (monthKind frame.CurrentMonth month.Month)
    | _ -> None

/// Der Monat davor im Fenster. Per Vergleich: trägt auch für einen Monat außerhalb des Fensters.
let previousMonth (months: string list) (month: string) : string option =
    months |> List.filter (fun m -> compare m month < 0) |> List.tryLast

/// Der Monat danach im Fenster, per Vergleich wie `previousMonth`.
let nextMonth (months: string list) (month: string) : string option =
    months |> List.tryFind (fun m -> compare m month > 0)

/// Die Monatsgruppen in Romans Reihenfolge. Liste, Picker und Pfeile sehen dieselben `Groups`.
let private ordered (model: Model) (month: BudgetMonth) : BudgetMonth =
    match model.Frame with
    | Success frame -> { month with Groups = orderGroups frame.CategoryOrder month.Groups }
    | _ -> month

/// Die Buchungen gelten nicht mehr: `entriesDue` holt sie, sobald sie zu sehen sind. Die neue
/// Generation verwirft eine noch laufende Antwort zum alten Monat.
let resetEntries (model: Model) : Model =
    { model with
        Selected = model.Selected |> Option.map (fun s -> { s with Entries = NotAsked })
        EntriesGeneration = model.EntriesGeneration + 1 }

/// Der Monat wechselt: neue Generation, eine laufende Antwort zählt nicht mehr. Fertige Buchungen
/// bleiben bis zur Antwort des Monats zu sehen, laufende werden wieder ungefragt.
let private invalidateEntries (model: Model) : Model =
    { model with
        Selected =
            model.Selected
            |> Option.map (fun s ->
                match s.Entries with
                | Loading -> { s with Entries = NotAsked }
                | _ -> s)
        EntriesGeneration = model.EntriesGeneration + 1 }

/// Ist `month` ein anderer Monat als der bisher gezeigte? Dann gelten die Buchungen nicht mehr.
let private entriesOutdated (model: Model) (month: BudgetMonth) : bool =
    match model.Month with
    | Success previous -> previous.Month <> month.Month
    | _ -> true

/// Trägt ein neues Zugewiesen in den Monat ein und schreibt Verfügbar und Zu verteilen fort. Der Wert
/// ist absolut: Steht er schon da, ändert sich nichts.
let withAssigned (categoryId: System.Guid) (assigned: Money) (month: BudgetMonth) : BudgetMonth =
    match categoriesOf month |> List.tryFind (fun c -> c.Id = categoryId) with
    | Some found ->
        let delta = assigned.Amount - found.Assigned.Amount
        let written (c: BudgetCategory) =
            if c.Id = categoryId then
                { c with
                    Assigned = { c.Assigned with Amount = assigned.Amount }
                    Available = { c.Available with Amount = c.Available.Amount + delta } }
            else
                c
        { month with
            ToBeBudgeted = { month.ToBeBudgeted with Amount = month.ToBeBudgeted.Amount - delta }
            Groups = month.Groups |> List.map (fun g -> { g with Categories = g.Categories |> List.map written }) }
    | None -> month

/// Die Antwort kann vor dem PATCH gelesen sein: Was in der Schlange steht, gilt, bis es bestätigt ist.
/// Sonst rechnete das Feld vom Stand davor und nähme die Zuweisung zurück.
let private withQueue (queue: AssignWrite list) (month: BudgetMonth) : BudgetMonth =
    queue
    |> List.filter (fun w -> w.Month = month.Month)
    |> List.fold (fun current w -> withAssigned w.CategoryId w.Assigned current) month

/// Antwort einnehmen. Eine Antwort einer älteren Abfrage wird verworfen, damit ein langsamer
/// Aufruf den gerade gezeigten Monat nicht überschreibt. Fehlt die gewählte Kategorie, endet die Auswahl.
/// Kommt ein anderer Monat, sind die Buchungen wieder fällig.
let applyLoaded (generation: int) (result: Result<BudgetMonth, string>) (model: Model) : Model =
    if generation <> model.MonthGeneration then
        model
    else
        match result with
        | Ok month ->
            let month = ordered model month |> withQueue model.AssignQueue
            let selected =
                model.Selected
                |> Option.filter (fun s -> categoriesOf month |> List.exists (fun c -> c.Id = s.CategoryId))
            let settled = { model with Month = Success month; Selected = selected; Freshness = MonthRead }
            if entriesOutdated model month then resetEntries settled else settled
        | Error message -> { model with Month = Failure message }

/// Das stille Neu-Lesen nach einer Verschiebung oder Zuweisung. Scheitert es, bleibt die Liste stehen und ein
/// Hinweis nennt den Grund: die Seite verliert nicht ihren Stand, weil ein Nachladen ausfiel. Das
/// gilt nicht während eines Monatswechsels: dann fehlt der neue Monat, und die Fehlerkarte mit
/// Wiederholen steht da.
let applyReloaded
    (generation: int)
    (result: Result<BudgetMonth, string>)
    (model: Model)
    : Model * ExternalMsg =
    match model.Month, result with
    | Success _, Error message when generation = model.MonthGeneration && not (monthChanging model) ->
        model, ShowToast ($"Beträge nicht neu gelesen, sie können veraltet sein: {message}", ToastWarning)
    | _ -> applyLoaded generation result model, NoOp

/// Monat wählen. Derselbe Monat noch einmal ändert nichts (`false`); ein anderer lädt (`true` heißt:
/// der Aufrufer muss laden). Ein geladener Monat bleibt bis zur Antwort stehen, sonst steht `Loading`.
/// Id und Tab bleiben. Der laufende Monat wird zu `ChosenMonth = None`.
let select (month: string) (model: Model) : Model * bool =
    match model.Frame with
    | Success frame when shownMonth model <> Some month || model.Month = NotAsked ->
        let chosen = if month = frame.CurrentMonth then None else Some month
        let month' =
            match model.Month with
            | Success _ -> model.Month
            | _ -> Loading
        requestMonth (invalidateEntries { model with ChosenMonth = chosen; Month = month' }), true
    | _ -> model, false

/// Rahmen und Monat holen beginnt. Ein vorhandener Rahmen bleibt stehen, der Monat kommt nach der
/// Antwort auf den Rahmen. Eine noch laufende Monatsantwort zählt ab jetzt nicht mehr.
let beginLoad (model: Model) : Model =
    requestMonth
        { model with
            Month = Loading
            Frame =
                match model.Frame with
                | Success _ -> model.Frame
                | _ -> Loading }

/// Antwort auf den Rahmen. Der Monat folgt (`true`), außer der erste Rahmen scheiterte: dann gibt
/// es keinen Monat zu zeigen, die Seite zeigt den Fehler mit Wiederholen. Gibt es schon einen
/// Rahmen, bleibt er stehen und ein Toast nennt den Grund.
let applyFrameLoaded (result: Result<BudgetFrame, string>) (model: Model) : Model * ExternalMsg * bool =
    match result, model.Frame with
    | Ok frame, _ -> requestMonth { model with Frame = Success frame; Month = Loading }, NoOp, true
    | Error message, Success _ ->
        requestMonth { model with Month = Loading },
        ShowToast ($"Monate nicht neu gelesen, sie können veraltet sein: {message}", ToastWarning),
        true
    | Error message, _ -> { model with Frame = Failure message; Month = Failure message }, NoOp, false

/// Rückt dieser Rahmen den gezeigten Monat weiter?
let frameMovesMonth (result: Result<BudgetFrame, string>) (model: Model) : bool =
    match result with
    | Ok frame -> shownMonth { model with Frame = Success frame } <> shownMonth model
    | Error _ -> false

/// Das stille Nachlesen. Scheitert es, bleibt das alte Fenster ohne Meldung, der nächste
/// Seitenaufruf heilt es. Ändert es den gezeigten Monat (Monatswechsel während die Seite offen ist),
/// liest der Aufrufer ihn neu (`true`), wie nach `applyFrameLoaded`. Die Liste bleibt bis dahin stehen.
let applyFrameRefreshed (result: Result<BudgetFrame, string>) (model: Model) : Model * bool =
    match result with
    | Ok frame ->
        let refreshed = { model with Frame = Success frame }
        if shownMonth refreshed <> shownMonth model then
            requestMonth (invalidateEntries refreshed), true
        else
            refreshed, false
    | Error _ -> model, false

/// Kann der Bericht das Fenster geöffnet haben?
let frameDueAfterReport (report: ApplyReport) (model: Model) : bool =
    match model.Frame with
    | Success frame -> windowMayHaveGrown frame.Months report
    | _ -> false

/// Der Ausgang ist unbekannt (Aufruf abgebrochen): es reicht, dass der gezeigte Monat der letzte ist.
let frameDueAfterLostCall (model: Model) : bool =
    match model.Frame, shownMonth model with
    | Success frame, Some month -> isLastMonth frame.Months month
    | _ -> false

/// Nach "Erneut senden": mit Bericht wie nach `frameDueAfterReport`. Scheiterte der Aufruf, kann der
/// Lauf YNAB trotzdem erreicht haben, der Ausgang ist unbekannt.
let frameDueAfterRetry (result: Result<ApplyReport, string>) (model: Model) : bool =
    match result with
    | Ok report -> frameDueAfterReport report model
    | Error _ -> frameDueAfterLostCall model

let private monthNames =
    [| "Januar"; "Februar"; "März"; "April"; "Mai"; "Juni"
       "Juli"; "August"; "September"; "Oktober"; "November"; "Dezember" |]

/// "2026-10-01" als "Oktober 2026". Ein unlesbarer Wert bleibt, wie er ist.
let monthLabel (month: string) : string =
    match month.Split '-' with
    | [| year; mm; _ |] ->
        match System.Int32.TryParse mm with
        | true, m when m >= 1 && m <= 12 -> $"{monthNames.[m - 1]} {year}"
        | _ -> month
    | _ -> month

/// Ein Betrag als Text: "1.600,00 €".
let moneyText (m: Money) = Money.formatAmountText false true m.Amount m.Currency

/// Der Rhythmus hinter dem Betrag: "/Monat", " alle 3 Monate", ohne Rhythmus nichts (budgeting-kjsht).
let rhythmText (rhythm: TargetRhythm) : string =
    match rhythm with
    | Weeks 1 -> "/Woche"
    | Months 1 -> "/Monat"
    | Years 1 -> "/Jahr"
    | Weeks n -> $" alle {n} Wochen"
    | Months n -> $" alle {n} Monate"
    | Years n -> $" alle {n} Jahre"
    | NoRhythm -> ""

/// Betrag und Rhythmus eines Targets: "1.600,00 €/Monat", "120,00 € alle 3 Monate".
let targetText (target: Target) : string =
    moneyText target.Goal + rhythmText target.Rhythm

/// Der Zusatz zum Target: pausiert oder erfüllt. Pausiert geht vor, weil der Fehlbetrag dann nicht
/// zählt. Ein offener Fehlbetrag erscheint als `missingText`. Liefert YNAB keinen Fehlbetrag, gibt
/// es keinen Zusatz.
let targetStatusText (category: BudgetCategory) : string option =
    match category.Snoozed, category.Underfunded with
    | true, _ -> Some "pausiert"
    | false, Some under when under.Amount = 0m -> Some "erfüllt"
    | false, _ -> None

/// Der Zusatz in der Liste: nur pausiert. "erfüllt" sagt dort nichts und steht im Tab Target.
let listStatusText (category: BudgetCategory) : string option =
    if category.Snoozed then Some "pausiert" else None

/// "fehlt 12,30 €", in Gelb (ADR 0075). `None`, wenn nichts fehlt.
let missingText (category: BudgetCategory) : string option =
    missingAmount category |> Option.map (fun amount -> $"fehlt {moneyText amount}")

/// Die Zeile unter dem neuen Betrag in der Vorschau.
let previewDetailText (line: TargetLine) : string =
    let amounts = $"Zugewiesen {moneyText line.OldAssigned}, Fehlbetrag {moneyText line.Underfunded}"
    match line.Target with
    | Some target -> $"Target {targetText target}, {amounts}"
    | None -> amounts

/// Die Kategorien eines Monats als flache Zählung, für die Leer-Anzeige.
let categoryCount (month: BudgetMonth) : int =
    month.Groups |> List.sumBy (fun g -> g.Categories.Length)

/// Kategorie-Suche (budgeting-rxxb4). Ein Treffer im Gruppennamen zeigt die ganze Gruppe,
/// sonst bleiben die passenden Kategorien; Gruppen ohne Treffer fallen weg.
let filterGroups (search: string) (groups: BudgetGroup list) : BudgetGroup list =
    groups
    |> List.choose (fun group ->
        if CategoryPicker.matchesText search group.Name then
            Some group
        else
            match group.Categories |> List.filter (fun c -> CategoryPicker.matchesText search c.Name) with
            | [] -> None
            | hits -> Some { group with Categories = hits })

let private matchesFilter (filter: CategoryFilter) (c: BudgetCategory) : bool =
    match filter with
    | AllCategories -> true
    | MissingOnly -> (missingAmount c).IsSome
    | OverdrawnOnly -> c.Available.Amount < 0m

/// Die Zeilen der Liste: Filter und Suche gelten zusammen, Gruppen ohne Treffer fallen weg.
/// Einmal je Render berechnen.
let shownGroups (filter: CategoryFilter) (search: string) (groups: BudgetGroup list) : BudgetGroup list =
    groups
    |> List.choose (fun group ->
        match group.Categories |> List.filter (matchesFilter filter) with
        | [] -> None
        | hits -> Some { group with Categories = hits })
    |> filterGroups search

/// Zahlen hinter den Filtern: immer der ganze Monat, unabhängig von der Suche (von Roman bestätigt,
/// nicht ändern). Überzogen ist eine Teilmenge von Fehlt: `missingAmount` zählt jedes Minus mit.
let filterCount (filter: CategoryFilter) (groups: BudgetGroup list) : int =
    groups |> List.sumBy (fun g -> g.Categories |> List.filter (matchesFilter filter) |> List.length)

/// Die Zeilen der Vorschau in Romans Reihenfolge, nur für die Anzeige. `TargetPreview.Lines` bleibt
/// unangetastet: `previewsMatch` vergleicht reihenfolgetreu und meldete sonst `PreviewChanged`.
let orderedPreviewLines (order: System.Guid list) (lines: TargetLine list) : TargetLine list =
    let ranks = order |> List.mapi (fun index id -> id, index) |> Map.ofList
    lines
    |> List.sortBy (fun l ->
        match ranks |> Map.tryFind l.CategoryId with
        | Some rank -> 0, rank, ""
        | None -> 1, 0, l.CategoryName)

/// Die Reihenfolge des Rahmens, für die Vorschau; ohne Rahmen leer.
let categoryOrder (model: Model) : System.Guid list =
    match model.Frame with
    | Success frame -> frame.CategoryOrder
    | _ -> []
