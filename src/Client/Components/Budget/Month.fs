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

/// Wie der Monat neu gelesen wird.
type MonthReread =
    /// Der geladene Monat bleibt stehen, die Antwort kommt über `Reloaded` (`applyReloaded`).
    | QuietReread
    /// Bis zur Antwort steht `Loading`, sie kommt über `Loaded`, ein Fehler zeigt die Fehlerkarte.
    | FullReread

/// Die Antwort einer Abfrage des Monats, gebunden an ihre Generation beim Start. `QuietReread`
/// antwortet über `Reloaded`, `FullReread` über `Loaded`; Erfolg und Scheitern gehen denselben Weg.
let rereadAnswer (reread: MonthReread) (model: Model) : Result<BudgetMonth, string> -> Msg =
    let generation = model.MonthGeneration
    match reread with
    | QuietReread -> fun result -> Reloaded (generation, result)
    | FullReread -> fun result -> Loaded (generation, result)

/// Den Monat neu lesen, ohne dass Liste und Inspektor verschwinden: Ein geladener Monat bleibt stehen
/// wie nach einer Verschiebung. War er nicht geladen oder läuft ein Wechsel, gilt `awaitMonth`.
let rereadMonth (model: Model) : Model * MonthReread =
    match model.Month with
    | Success _ when not (monthChanging model) -> awaitFreshMonth model, QuietReread
    | _ -> awaitMonth model, FullReread

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

/// Eine laufende Abfrage wird wieder ungefragt, Fertiges bleibt stehen.
let private unloaded (data: RemoteData<'T>) : RemoteData<'T> =
    match data with
    | Loading -> NotAsked
    | other -> other

/// Die Tab-Daten gelten nicht mehr (andere Kategorie, anderer Monat, Rückkehr aus dem Kassenbuch):
/// Antworten zu älteren Abfragen zählen nicht mehr, beide Tabs sind wieder fällig, sobald ihr Tab zu
/// sehen ist (`tabDataDue`).
let resetTabData (model: Model) : Model =
    { model with
        Selected = model.Selected |> Option.map (fun s -> { s with Entries = NotAsked; History = NotAsked })
        EntriesGeneration = model.EntriesGeneration + 1
        HistoryGeneration = model.HistoryGeneration + 1 }

/// Nur die Daten eines Tabs gelten nicht mehr: "Wiederholen" oder die Historie nach einer Bewegung.
/// Die Daten des anderen Tabs und seine laufende Abfrage bleiben. Tabs ohne nachgeladene Daten ändern
/// nichts.
let resetTab (tab: InspectorTab) (model: Model) : Model =
    let withSelected (reset: SelectedCategory -> SelectedCategory) (next: Model) =
        { next with Selected = next.Selected |> Option.map reset }
    match tab with
    | EntriesTab ->
        withSelected (fun s -> { s with Entries = NotAsked }) { model with EntriesGeneration = model.EntriesGeneration + 1 }
    | HistoryTab ->
        withSelected (fun s -> { s with History = NotAsked }) { model with HistoryGeneration = model.HistoryGeneration + 1 }
    | MoneyTab
    | TargetTab -> model

/// Der Monat wechselt. Eine laufende Abfrage wird wieder ungefragt, ihre Antwort zählt nicht mehr, und
/// sie kommt, sobald ihr Tab zu sehen ist (`tabDataDue`), sonst hinge sie in `Loading`. Fertige
/// Tab-Daten bleiben bis zur Antwort des Monats zu sehen, und mit ihnen ihre Generation: Das stille
/// Nachladen der Historie gilt dem Monat, der noch zu sehen ist, und zählt weiter. Bringt die Antwort
/// einen anderen Monat, setzt `applyLoaded` die Tab-Daten zurück; kommt derselbe wieder (A, B, A),
/// ersetzt die stille Antwort den alten Stand.
let private invalidateTabData (model: Model) : Model =
    let running (data: RemoteData<'T>) =
        match data with
        | Loading -> true
        | NotAsked
        | Success _
        | Failure _ -> false
    let next (isRunning: SelectedCategory -> bool) (generation: int) =
        if model.Selected |> Option.exists isRunning then generation + 1 else generation
    { model with
        Selected = model.Selected |> Option.map (fun s -> { s with Entries = unloaded s.Entries; History = unloaded s.History })
        EntriesGeneration = next (fun s -> running s.Entries) model.EntriesGeneration
        HistoryGeneration = next (fun s -> running s.History) model.HistoryGeneration }

/// Ist `month` ein anderer Monat als der bisher gezeigte? Dann gelten die Tab-Daten nicht mehr.
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

/// Trägt die geschriebenen Zeilen eines Berichts ein (Verschiebung, Zuweisung, Targets-Lauf). Bis das
/// Neu-Lesen antwortet, rechnet das Feld sonst vom alten Stand und seine absolute Zuweisung nähme den
/// Bericht zurück (ADR 0078 Punkt 5).
let withReport (report: ApplyReport) (model: Model) : Model =
    match model.Month with
    | Success month when month.Month = report.Month ->
        let written =
            report.Lines
            |> List.filter (fun l -> l.Outcome = Written)
            |> List.fold (fun current l -> withAssigned l.CategoryId l.NewAssigned current) month
        { model with Month = Success written }
    | _ -> model

/// Die Antwort kann vor dem PATCH gelesen sein: Was in der Schlange steht, gilt, bis es bestätigt ist.
/// Sonst rechnete das Feld vom Stand davor und nähme die Zuweisung zurück.
let private withQueue (queue: AssignWrite list) (month: BudgetMonth) : BudgetMonth =
    queue
    |> List.filter (fun w -> w.Month = month.Month)
    |> List.fold (fun current w -> withAssigned w.CategoryId w.Assigned current) month

/// Antwort einnehmen. Eine Antwort einer älteren Abfrage wird verworfen, damit ein langsamer
/// Aufruf den gerade gezeigten Monat nicht überschreibt. Fehlt die gewählte Kategorie, endet die Auswahl.
/// Kommt ein anderer Monat, sind Buchungen und Historie wieder fällig.
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
            if entriesOutdated model month then resetTabData settled else settled
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
        requestMonth (invalidateTabData { model with ChosenMonth = chosen; Month = month' }), true
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
            requestMonth (invalidateTabData refreshed), true
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

let private shortMonthNames =
    [| "Jan"; "Feb"; "Mär"; "Apr"; "Mai"; "Jun"; "Jul"; "Aug"; "Sep"; "Okt"; "Nov"; "Dez" |]

/// "2026-10-01" als "Okt", für die Balken der Ausgaben. Ein unlesbarer Wert bleibt, wie er ist.
let shortMonthLabel (month: string) : string =
    match month.Split '-' with
    | [| _; mm; _ |] ->
        match System.Int32.TryParse mm with
        | true, m when m >= 1 && m <= 12 -> shortMonthNames.[m - 1]
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

/// Der Zusatz zum Target im Tab Target: pausiert mit Quelle oder erfüllt. Pausiert geht vor, weil der
/// Fehlbetrag dann nicht zählt. Ein offener Fehlbetrag erscheint als `missingText`. Liefert YNAB keinen
/// Fehlbetrag, gibt es keinen Zusatz.
let targetStatusText (category: BudgetCategory) : string option =
    match category.Pause, category.Underfunded with
    | Some PausedInBb, _ -> Some "pausiert in BB"
    | Some PausedInYnab, _ -> Some "pausiert in YNAB"
    | Some PausedInBoth, _ -> Some "pausiert in BB und YNAB"
    | None, Some under when under.Amount = 0m -> Some "erfüllt"
    | None, _ -> None

/// Der Zusatz in der Liste: nur pausiert, ohne Quelle. "erfüllt" sagt dort nichts und steht im Tab Target.
let listStatusText (category: BudgetCategory) : string option =
    if isPaused category then Some "pausiert" else None

/// "fehlt 12,30 €", in Gelb (ADR 0075). `None`, wenn nichts fehlt.
let missingText (category: BudgetCategory) : string option =
    missingAmount category |> Option.map (fun amount -> $"fehlt {moneyText amount}")

/// Der Fehlbetrag im Tab Target. Bei einem pausierten Target zählt er nicht.
let targetUnderfunded (category: BudgetCategory) : Money option =
    if isPaused category then None else category.Underfunded

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

/// Der Hinweis, wenn Filter und Suche keine Zeile übrig lassen.
let emptyListHint (search: string) : string =
    match search.Trim() with
    | "" -> "Keine Kategorie in dieser Ansicht."
    | trimmed -> $"Keine Kategorie passt zu „{trimmed}“"

/// Die Beschriftung eines Filter-Chips. Die Zahl gilt für den ganzen Monat, unabhängig von der Suche.
let filterChipLabel (filter: CategoryFilter) (groups: BudgetGroup list) : string =
    match filter with
    | AllCategories -> "Alle"
    | MissingOnly -> $"Fehlt ({filterCount MissingOnly groups})"
    | OverdrawnOnly -> $"Überzogen ({filterCount OverdrawnOnly groups})"

/// Was `‹ Monat ›` zeigt: das Ziel jedes Pfeils (am Rand keins) und ob ein anderer als der laufende
/// Monat gezeigt wird (dann ist der Name teal und führt zurück).
type MonthStepper =
    { Previous: string option
      Next: string option
      Elsewhere: bool }

let monthStepperFor (frame: BudgetFrame) (month: string) : MonthStepper =
    { Previous = previousMonth frame.Months month
      Next = nextMonth frame.Months month
      Elsewhere = month <> frame.CurrentMonth }

/// Targets anwenden steht nur im laufenden oder einem kommenden Monat, im vergangenen bleibt sein Platz frei.
let targetsButtonVisible (model: Model) : bool =
    displayedMonthKind model |> Option.exists targetsAllowed

/// Die Betragsspalten der PC-Tabelle hinter Kategorie und Target: Kopf und Wert je Kategorie.
/// `IsAvailable` markiert die Spalte mit der Farblogik für Verfügbar.
type AmountColumn =
    { Header: string
      Value: BudgetCategory -> Money
      IsAvailable: bool }

let amountColumns : AmountColumn list =
    [ { Header = "Zugewiesen"; Value = (fun c -> c.Assigned); IsAvailable = false }
      { Header = "Aktivität"; Value = (fun c -> c.Activity); IsAvailable = false }
      { Header = "Verfügbar"; Value = (fun c -> c.Available); IsAvailable = true } ]

/// Die Markierung einer offenen Rückschreibung (budgeting-dgrp7): Zugewiesen zeigt BBs Soll, YNAB hat es noch
/// nicht.
[<Literal>]
let NotInYnabText = "noch nicht in YNAB"

/// Der Grund unter der Markierung im Tab Geld.
[<Literal>]
let NotInYnabReason = "Zugewiesen zeigt BBs Wert, YNAB hat ihn noch nicht."

/// Die zweite Zeile am Handy: Zugewiesen und Aktivität. Die Markierung steht als eigenes Element dahinter
/// (`writeBackMark`): wird es eng, kürzen die Zahlen, nie der Status.
let mobileFiguresText (c: BudgetCategory) : string =
    $"Zugewiesen {moneyText c.Assigned} · Aktivität {moneyText c.Activity}"

/// Die Markierung der Zeile, nur bei offener Rückschreibung. PC und Handy zeigen sie gleich als neutrales
/// Badge: Gelb bleibt in der Zeile `fehlt` vorbehalten.
let writeBackMark (c: BudgetCategory) : string option =
    c.OpenWriteBack |> Option.map (fun _ -> NotInYnabText)
