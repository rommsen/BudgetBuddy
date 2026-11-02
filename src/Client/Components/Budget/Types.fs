/// Modell und Nachrichten der Budget-Seite (budgeting-k7wmd, Liste und Inspektor budgeting-g8s3w).
///
/// Die Seite liest den Monat live aus YNAB und merkt sich nichts außer dem, was gerade
/// angezeigt wird. Der Rahmen (`BudgetFrame`) liegt je Seitenaufruf im Modell: laufender Monat,
/// Monatsfenster und Kategorie-Reihenfolge. Eine Zeile wählt die Kategorie, alles zu ihr steht im
/// Inspektor. Ob Panel oder Sheet, entscheidet allein das Rendern.
module Components.Budget.Types

open Shared.Domain
open Shared.Budgeting
open Types
open Client.DesignSystem

/// Stand des Dialogs "Targets anwenden" (budgeting-h2zfn).
type TargetsState =
    | TargetsClosed
    /// Die Vorschau wird geholt.
    | PreviewLoading
    | PreviewFailed of message: string
    /// Die Vorschau steht. `changed` heißt: sie wurde neu geladen, weil sich YNABs Stand beim
    /// Übernehmen geändert hatte (es wurde nichts geschrieben).
    | PreviewReady of preview: TargetPreview * changed: bool
    /// Übernehmen läuft.
    | Applying of preview: TargetPreview
    /// Der Aufruf selbst scheiterte: ob etwas geschrieben wurde, ist unbekannt.
    | ApplyFailed of preview: TargetPreview * message: string
    /// Ausgang der Anwendung; `retrying` während "Fehlgeschlagene erneut senden" läuft,
    /// `retryError` wenn dieser Aufruf scheiterte.
    | ApplyDone of preview: TargetPreview * report: ApplyReport * retrying: bool * retryError: string option

/// Wo der Auftrag im Sheet steht.
type Submission =
    | Editing
    /// Der Aufruf läuft: der Knopf ist gesperrt und zeigt den Ladezustand.
    | Sending
    /// Nichts geschrieben (Monat nicht lesbar, Auftrag abgelehnt), mit Grund.
    | Rejected of string

/// Das Sheet "Verschieben" zu einer Kategorie X (budgeting-afd8h). Es hält nur X' Id: Beträge
/// kommen aus `Model.Month`, damit eine neu gelesene Liste auch ein offenes Sheet erreicht.
type MoveForm = {
    /// X, die Kategorie, zu der das Sheet geöffnet wurde.
    CategoryId: System.Guid
    /// Ist X die Quelle? Sonst das Ziel.
    IsSource: bool
    /// Die Gegenseite; `None`, solange keine gewählt ist.
    Counterpart: MoveSide option
    /// Eingabe im Betragsfeld; `None`, solange nichts getippt wurde: dann gilt die Vorbelegung
    /// aus dem aktuellen Monat.
    AmountInput: string option
    PickerOpen: bool
    Submission: Submission
}

/// Läuft "Erneut senden" für eine Verschiebung gerade?
type MoveRetryState =
    | RetryReady
    | RetryRunning

/// Eine Verschiebung mit fehlgeschlagener Zeile. Bleibt auf der Seite sichtbar, wenn der Toast weg
/// ist, bis nichts mehr offen ist.
type OpenMoveRetry = {
    ApplicationId: string
    /// Der Monat der Verschiebung, etwa "Oktober 2026": der Hinweis steht auch in anderen Monaten da.
    MonthLabel: string
    Summary: string
    Reason: string
    State: MoveRetryState
}

/// Filter über der Liste neben der Suche.
type CategoryFilter =
    | AllCategories
    | MissingOnly
    | RedOnly

/// Die Tabs des Inspektors.
type InspectorTab =
    | MoneyTab
    | EntriesTab
    | HistoryTab
    | TargetTab

/// Richtung beim Wechsel der Kategorie ohne Schließen.
type Direction =
    | Previous
    | Next

/// Die gewählte Kategorie, der einzige Auswahl-Zustand. Hält nur die Id: Zugewiesen, Aktivität und
/// Verfügbar kommen aus `Model.Month`, also live aus YNAB. Aus dem Kassenbuch kommt allein die Liste.
type SelectedCategory = {
    CategoryId: System.Guid
    Entries: RemoteData<CategoryMonthEntry list>
    /// Tipp auf eine Buchung: die Seite ist verlassen, die Auswahl wartet auf die Rückkehr.
    ReturnPending: bool
}

type Model = {
    /// Laufender Monat, Monatsfenster und Reihenfolge. Je Seitenaufruf gelesen.
    Frame: RemoteData<BudgetFrame>
    /// Der gewählte Monat. `None` heißt laufender Monat: ein offener Tab springt am Monatsersten mit.
    ChosenMonth: string option
    /// Der geladene Monat, Gruppen und Kategorien in Romans Reihenfolge.
    Month: RemoteData<BudgetMonth>
    /// Der Dialog "Targets anwenden".
    Targets: TargetsState
    /// Suchtext über der Kategorie-Liste (budgeting-rxxb4). Bleibt beim Monatswechsel stehen,
    /// wird nicht gespeichert.
    CategorySearch: string
    /// Filter über der Liste. Bleibt beim Monatswechsel stehen.
    Filter: CategoryFilter
    /// Das offene Sheet "Verschieben".
    Move: MoveForm option
    /// Verschiebungen, die noch erneut gesendet werden können.
    OpenRetries: OpenMoveRetry list
    /// Die gewählte Kategorie. Filter und Suche wirken nur auf die Liste, sie bleibt gewählt.
    Selected: SelectedCategory option
    /// Der Tab des Inspektors. Bleibt bei Kategorie- und Monatswechsel stehen.
    Tab: InspectorTab
    /// Zählt die Abfragen der Buchungen: eine späte Antwort ersetzt keine neuere Liste.
    EntriesGeneration: int
    /// Zählt die Abfragen des Monats: eine späte Antwort ersetzt keinen neueren Stand, auch nicht
    /// bei gleicher Monatswahl (zwei Reloads nach schnellen Verschiebungen, Wechsel A, B, A).
    MonthGeneration: int
}

type Msg =
    /// Die Seite wird betreten: Rahmen und Monat holen, nach der Rückkehr aus dem Kassenbuch nur den Monat.
    | PageEntered
    /// Rahmen und Monat (neu) holen: "Wiederholen".
    | Load
    /// Antwort auf den Rahmen. Danach kommt der Monat.
    | FrameLoaded of Result<BudgetFrame, string>
    /// Antwort des stillen Nachlesens, wenn das Fenster gewachsen sein kann.
    | FrameRefreshed of Result<BudgetFrame, string>
    /// `‹ ›` oder der Monatsname.
    | SelectMonth of month: string
    /// Antwort des Servers, mit der Abfrage, zu der sie gehört.
    | Loaded of generation: int * Result<BudgetMonth, string>
    /// Antwort des stillen Neu-Lesens nach einer Verschiebung.
    | Reloaded of generation: int * Result<BudgetMonth, string>
    /// Knopf "Targets anwenden": Vorschau öffnen.
    | OpenTargets
    /// Antwort der Vorschau, mit dem Monat, zu dem sie gehört.
    | PreviewLoaded of month: string * Result<TargetPreview, string>
    /// "Übernehmen".
    | ConfirmTargets
    | TargetsApplied of Result<ApplyResult, string>
    /// "Fehlgeschlagene erneut senden".
    | RetryFailed
    | TargetsRetried of Result<ApplyReport, string>
    /// "Abbrechen" oder "Schließen".
    | CloseTargets
    /// Eingabe im Suchfeld über der Kategorie-Liste.
    | CategorySearchChanged of string
    /// Das ✕ im Suchfeld.
    | ClearCategorySearch
    /// Alle, Fehlt oder Rot.
    | SelectFilter of CategoryFilter
    /// Knopf "Verschieben" im Tab Geld.
    | OpenMove of System.Guid
    | CloseMove
    /// Der Umschalter: Quelle und Ziel tauschen, der Betrag bleibt.
    | ToggleMoveDirection
    | MoveAmountChanged of string
    | OpenMovePicker
    | CloseMovePicker
    | PickMoveCounterpart of MoveSide
    /// "Verschieben".
    | ConfirmMove
    /// Antwort des Servers. `Error` heißt: nichts geschrieben.
    | MoveAnswered of Result<ApplyReport, string>
    /// Der Aufruf selbst brach ab: ob etwas geschrieben wurde, ist unbekannt.
    | MoveCallLost of string
    /// "Erneut senden" am Toast oder am Hinweis auf der Seite.
    | RetryMove of applicationId: string
    | MoveRetried of applicationId: string * Result<ApplyReport, string>
    /// Tipp auf eine Zeile.
    | SelectCategory of System.Guid
    /// Pfeiltaste am PC, `‹ ›` im Sheet.
    | StepCategory of Direction
    /// Das Sheet schließen.
    | CloseInspector
    | SelectInspectorTab of InspectorTab
    /// Die Route der Buchung wurde nicht per Zurück verlassen: die Auswahl wartet nicht mehr.
    | ReturnAbandoned
    /// Die Buchungen (neu) holen: "Wiederholen".
    | LoadCategoryEntries
    | CategoryEntriesLoaded of generation: int * Result<CategoryMonthEntry list, string>
    /// Tipp auf eine Buchung im Tab Buchungen: ins Kassenbuch springen.
    | OpenEntryInLedger of System.Guid

/// Was die Seite an die App meldet (wie im Kassenbuch).
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | ShowToastWithAction of string * ToastType * ToastAction
    /// Sprung ins Kassenbuch auf diese Buchung.
    | JumpToEntry of System.Guid

let init () : Model =
    { Frame = NotAsked
      ChosenMonth = None
      Month = NotAsked
      Targets = TargetsClosed
      CategorySearch = ""
      Filter = AllCategories
      Move = None
      OpenRetries = []
      Selected = None
      Tab = MoneyTab
      EntriesGeneration = 0
      MonthGeneration = 0 }

let private categoriesOf (month: BudgetMonth) : BudgetCategory list =
    month.Groups |> List.collect (fun g -> g.Categories)

/// Kategorie X des Sheets im Monat.
let moveCategory (month: BudgetMonth) (form: MoveForm) : BudgetCategory option =
    categoriesOf month |> List.tryFind (fun c -> c.Id = form.CategoryId)

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
let private resetEntries (model: Model) : Model =
    { model with
        Selected = model.Selected |> Option.map (fun s -> { s with Entries = NotAsked })
        EntriesGeneration = model.EntriesGeneration + 1 }

/// Antwort einnehmen. Eine Antwort einer älteren Abfrage wird verworfen, damit ein langsamer
/// Aufruf den gerade gezeigten Monat nicht überschreibt. Fehlt X im neuen Stand, schließt ein
/// offenes Sheet, außer während des Aufrufs. Fehlt die gewählte Kategorie, endet die Auswahl.
let applyLoaded (generation: int) (result: Result<BudgetMonth, string>) (model: Model) : Model =
    if generation <> model.MonthGeneration then
        model
    else
        match result with
        | Ok month ->
            let month = ordered model month
            let move =
                model.Move
                |> Option.filter (fun form -> form.Submission = Sending || (moveCategory month form).IsSome)
            let selected =
                model.Selected
                |> Option.filter (fun s -> categoriesOf month |> List.exists (fun c -> c.Id = s.CategoryId))
            { model with Month = Success month; Move = move; Selected = selected }
        | Error message -> { model with Month = Failure message }

/// Das stille Neu-Lesen nach einer Verschiebung. Scheitert es, bleibt die Liste stehen und ein
/// Hinweis nennt den Grund: die Seite verliert nicht ihren Stand, weil ein Nachladen ausfiel.
let applyReloaded
    (generation: int)
    (result: Result<BudgetMonth, string>)
    (model: Model)
    : Model * ExternalMsg =
    match model.Month, result with
    | Success _, Error message when generation = model.MonthGeneration ->
        model, ShowToast ($"Beträge nicht neu gelesen, sie können veraltet sein: {message}", ToastWarning)
    | _ -> applyLoaded generation result model, NoOp

/// Monat wählen. Derselbe Monat noch einmal ändert nichts (`false`); ein anderer setzt den
/// Monat auf `Loading` (`true` heißt: der Aufrufer muss laden). Id und Tab bleiben, die Buchungen
/// kommen neu. Der laufende Monat wird zu `ChosenMonth = None`.
let select (month: string) (model: Model) : Model * bool =
    match model.Frame with
    | Success frame when shownMonth model <> Some month || model.Month = NotAsked ->
        let chosen = if month = frame.CurrentMonth then None else Some month
        requestMonth (resetEntries { model with ChosenMonth = chosen; Month = Loading }), true
    | _ -> model, false

/// Rahmen und Monat holen beginnt. Ein vorhandener Rahmen bleibt stehen, der Monat kommt nach der
/// Antwort auf den Rahmen.
let beginLoad (model: Model) : Model =
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

/// Das stille Nachlesen. Scheitert es, bleibt das alte Fenster ohne Meldung, der nächste
/// Seitenaufruf heilt es.
let applyFrameRefreshed (result: Result<BudgetFrame, string>) (model: Model) : Model =
    match result with
    | Ok frame -> { model with Frame = Success frame }
    | Error _ -> model

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
    | RedOnly -> c.Available.Amount < 0m

/// Die Zeilen der Liste: Filter und Suche gelten zusammen, Gruppen ohne Treffer fallen weg.
/// Einmal je Render berechnen.
let shownGroups (filter: CategoryFilter) (search: string) (groups: BudgetGroup list) : BudgetGroup list =
    groups
    |> List.choose (fun group ->
        match group.Categories |> List.filter (matchesFilter filter) with
        | [] -> None
        | hits -> Some { group with Categories = hits })
    |> filterGroups search

/// Zahlen hinter den Filtern: der ganze Monat, unabhängig von der Suche.
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

// ============================================
// Targets anwenden (budgeting-h2zfn)
// ============================================

/// Dialog öffnen oder nach gescheiterter Vorschau "Wiederholen". Nur bei geladenem Monat und nur,
/// wenn der Dialog zu ist oder an der Vorschau scheiterte; `true` heißt: der Aufrufer muss die
/// Vorschau holen.
let openTargets (model: Model) : Model * bool =
    match model.Month, model.Targets with
    | Success _, (TargetsClosed | PreviewFailed _) when shownMonthKind model |> Option.exists targetsAllowed ->
        { model with Targets = PreviewLoading }, true
    | _ -> model, false

/// Antwort der Vorschau einnehmen. Eine Antwort zu einem anderen Monat oder zu einem
/// inzwischen geschlossenen Dialog wird verworfen.
let applyPreviewLoaded (month: string) (result: Result<TargetPreview, string>) (model: Model) : Model =
    if shownMonth model <> Some month || model.Targets <> PreviewLoading then
        model
    else
        match result with
        | Ok preview -> { model with Targets = PreviewReady (preview, false) }
        | Error message -> { model with Targets = PreviewFailed message }

/// "Übernehmen". Gibt die Vorschau zurück, die an den Server geht. Ohne Zeilen gibt es nichts
/// zu übernehmen; ein zweiter Klick während des Laufs tut nichts. Ein negatives Zu verteilen
/// danach sperrt nichts.
let confirmTargets (model: Model) : Model * TargetPreview option =
    match model.Targets with
    | PreviewReady (preview, _) when not (List.isEmpty preview.Lines) ->
        { model with Targets = Applying preview }, Some preview
    | _ -> model, None

/// Antwort auf "Übernehmen". Hat sich YNABs Stand geändert, steht die neue Vorschau mit dem
/// Hinweis da (nichts wurde geschrieben).
let applyApplied (result: Result<ApplyResult, string>) (model: Model) : Model =
    match model.Targets with
    | Applying preview ->
        match result with
        | Ok (Applied report) -> { model with Targets = ApplyDone (preview, report, false, None) }
        | Ok (PreviewChanged fresh) -> { model with Targets = PreviewReady (fresh, true) }
        | Error message -> { model with Targets = ApplyFailed (preview, message) }
    | _ -> model

/// "Fehlgeschlagene erneut senden". Gibt die Anwendung zurück, die der Server erneut senden
/// soll; ohne fehlgeschlagene Zeile oder während eines Laufs `None`.
let retryFailed (model: Model) : Model * string option =
    match model.Targets with
    | ApplyDone (preview, report, false, _) when failedCount report > 0 ->
        { model with Targets = ApplyDone (preview, report, true, None) }, Some report.ApplicationId
    | _ -> model, None

let applyRetried (result: Result<ApplyReport, string>) (model: Model) : Model =
    match model.Targets with
    | ApplyDone (preview, report, true, _) ->
        match result with
        | Ok report' -> { model with Targets = ApplyDone (preview, report', false, None) }
        | Error message -> { model with Targets = ApplyDone (preview, report, false, Some message) }
    | _ -> model

/// Dialog schließen. Während Übernehmen oder erneutem Senden läuft, bleibt er offen. Nach
/// einer Anwendung (auch nach einem gescheiterten Aufruf) muss die Seite den Monat neu
/// laden (`true`), weil YNABs Stand sich geändert haben kann.
let closeTargets (model: Model) : Model * bool =
    match model.Targets with
    | Applying _
    | ApplyDone (_, _, true, _) -> model, false
    | ApplyDone _
    | ApplyFailed _ -> requestMonth { model with Targets = TargetsClosed }, true
    | _ -> { model with Targets = TargetsClosed }, false

/// Wie sich Zu verteilen nach dem Lauf zur Vorschau verhält.
type ActualCheck =
    | SameAsPreview
    /// YNAB meldet einen anderen Betrag als die Vorschau.
    | DiffersFromPreview of actual: Shared.Domain.Money
    /// Das erneute Lesen nach dem Lauf scheiterte.
    | ActualUnknown

let actualCheck (preview: TargetPreview) (report: ApplyReport) : ActualCheck =
    match report.ActualToBeBudgeted with
    | None -> ActualUnknown
    | Some actual when actual = preview.ToBeBudgetedAfter -> SameAsPreview
    | Some actual -> DiffersFromPreview actual

// ============================================
// Verschieben (budgeting-afd8h, ADR 0069)
// ============================================

/// Der Zustand beim Öffnen. Verfügbar negativ: X wird gedeckt, ist also das Ziel. Sonst ist X die
/// Quelle.
let openMoveForm (category: BudgetCategory) : MoveForm =
    { CategoryId = category.Id
      IsSource = category.Available.Amount >= 0m
      Counterpart = None
      AmountInput = None
      PickerOpen = false
      Submission = Editing }

/// Der Umschalter dreht Quelle und Ziel, der Betrag bleibt.
let toggleDirection (form: MoveForm) : MoveForm =
    { form with IsSource = not form.IsSource }

/// Der Text im Betragsfeld: die Eingabe, sonst die Vorbelegung aus dem aktuellen Monat (Verfügbar
/// beziehungsweise bei negativem Verfügbar der Fehlbetrag).
let moveAmountText (month: BudgetMonth) (form: MoveForm) : string =
    match form.AmountInput, moveCategory month form with
    | Some text, _ -> text
    | None, Some category -> formatAmountForInput category.Available.Amount
    | None, None -> ""

/// Der Betrag der Eingabe; leer, unlesbar und 0 sind keiner.
let moveAmount (month: BudgetMonth) (form: MoveForm) : decimal option =
    parseAmountInput (moveAmountText month form) |> Option.filter (fun amount -> amount > 0m)

/// Darf "Verschieben" gedrückt werden?
let canMove (month: BudgetMonth) (form: MoveForm) : bool =
    form.Submission <> Sending
    && form.Counterpart.IsSome
    && (moveCategory month form).IsSome
    && (moveAmount month form).IsSome

/// Der Auftrag an den Server; `None`, solange etwas fehlt.
let moveRequest (month: BudgetMonth) (form: MoveForm) : MoveRequest option =
    match form.Counterpart, moveAmount month form, moveCategory month form with
    | Some counterpart, Some amount, Some category ->
        let x = CategorySide form.CategoryId
        let source, target = if form.IsSource then x, counterpart else counterpart, x
        Some { Source = source; Target = target; Amount = { Amount = amount; Currency = category.Available.Currency } }
    | _ -> None

/// Name der Gegenseite.
let counterpartName (month: BudgetMonth) (side: MoveSide) : string =
    match side with
    | ToBeBudgetedSide -> CategoryPicker.toBeBudgetedLabel
    | CategorySide id ->
        categoriesOf month
        |> List.tryFind (fun c -> c.Id = id)
        |> Option.map (fun c -> c.Name)
        |> Option.defaultValue ""

/// Eine Zeile "Verfügbar danach".
type MoveAfterLine = { Label: string; After: Money }

/// "Verfügbar danach" für X und, sobald gewählt, die Gegenseite, aus dem aktuellen Monat. Ohne
/// gültigen Betrag bleiben die heutigen Werte stehen. Ein negatives Ergebnis ist gültig.
let moveAfter (month: BudgetMonth) (form: MoveForm) : MoveAfterLine list =
    let amount = moveAmount month form |> Option.defaultValue 0m
    let change = if form.IsSource then -amount else amount
    let shifted (m: Money) (delta: decimal) = { m with Amount = m.Amount + delta }
    let counterpart =
        match form.Counterpart with
        | None -> None
        | Some ToBeBudgetedSide -> Some month.ToBeBudgeted
        | Some (CategorySide id) ->
            categoriesOf month |> List.tryFind (fun c -> c.Id = id) |> Option.map (fun c -> c.Available)
    [ match moveCategory month form with
      | Some category -> { Label = category.Name; After = shifted category.Available change }
      | None -> ()
      match form.Counterpart, counterpart with
      | Some side, Some available -> { Label = counterpartName month side; After = shifted available -change }
      | _ -> () ]

/// Die Kategorien des Pickers: alle sichtbaren des Monats ohne X, mit `Available` aus dem Monat.
/// Die Kategorie-Suche der Seite gilt hier nicht.
let pickerCategories (month: BudgetMonth) (x: System.Guid) : YnabCategory list =
    month.Groups
    |> List.collect (fun g ->
        g.Categories
        |> List.filter (fun c -> c.Id <> x)
        |> List.map (fun c ->
            { Id = YnabCategoryId c.Id
              Name = c.Name
              GroupName = g.Name
              Available = Some c.Available
              Hidden = false }))

/// Aus der Wahl im Picker wird die Gegenseite. Ohne Kategorie und Konto gibt es dort nicht.
let counterpartOf (choice: CategoryPicker.PickerChoice) : MoveSide option =
    match choice with
    | CategoryPicker.ChoseToBeBudgeted -> Some ToBeBudgetedSide
    | CategoryPicker.ChoseCategory (Some (YnabCategoryId id)) -> Some (CategorySide id)
    | _ -> None

/// Die Überschrift im Sheet.
let moveHeadline (month: BudgetMonth) (form: MoveForm) : string =
    let name = moveCategory month form |> Option.map (fun c -> c.Name) |> Option.defaultValue ""
    let other = form.Counterpart |> Option.map (counterpartName month) |> Option.defaultValue "…"
    if form.IsSource then $"Von {name} nach {other}"
    else $"{name} decken aus {other}"

/// Sheet öffnen, nur bei geladenem Monat, bekannter Kategorie und ohne offenen Dialog.
let openMove (categoryId: System.Guid) (model: Model) : Model =
    match model.Month, model.Targets, model.Move with
    | Success month, TargetsClosed, None ->
        match categoriesOf month |> List.tryFind (fun c -> c.Id = categoryId) with
        | Some category -> { model with Move = Some (openMoveForm category) }
        | None -> model
    | _ -> model

/// Sheet schließen. Während des Aufrufs bleibt es offen.
let closeMove (model: Model) : Model =
    match model.Move with
    | Some form when form.Submission = Sending -> model
    | _ -> { model with Move = None }

/// Ändert das offene Formular, außer während des Aufrufs.
let updateForm (change: MoveForm -> MoveForm) (model: Model) : Model =
    match model.Move with
    | Some form when form.Submission <> Sending -> { model with Move = Some (change form) }
    | _ -> model

/// "Verschieben". Gibt den Auftrag zurück; ist der Knopf gesperrt oder läuft schon ein Aufruf,
/// geschieht nichts. Sonst ginge beim Doppeltipp das Geld zweimal, denn der Server rechnet relativ.
/// Mit dem Auftrag kommt der Monat, den die Seite zeigt: dorthin schreibt der Server.
let confirmMove (model: Model) : Model * (string * MoveRequest) option =
    match model.Move, model.Month with
    | Some form, Success month when canMove month form ->
        { model with Move = Some { form with Submission = Sending } },
        moveRequest month form |> Option.map (fun request -> month.Month, request)
    | _ -> model, None

/// "20,00 € von Lebensmittel nach Urlaub", aus dem Bericht des Servers. Die Zeilen stehen in
/// Sendereihenfolge, eine einzelne Zeile hat Zu verteilen auf der anderen Seite.
let moveSummaryText (report: ApplyReport) : string =
    let amountOf (l: ApplyLineResult) =
        let amount = abs (l.NewAssigned.Amount - l.OldAssigned.Amount)
        Money.formatAmountText false true amount l.NewAssigned.Currency
    match report.Lines with
    | [ source; target ] -> $"{amountOf source} von {source.CategoryName} nach {target.CategoryName}"
    | [ line ] when line.NewAssigned.Amount < line.OldAssigned.Amount ->
        $"{amountOf line} von {line.CategoryName} nach {CategoryPicker.toBeBudgetedLabel}"
    | [ line ] -> $"{amountOf line} von {CategoryPicker.toBeBudgetedLabel} nach {line.CategoryName}"
    | _ -> "Verschoben"

/// Beschriftung der Toast-Aktion.
let retryMoveLabel = "Erneut senden"

/// Der Toast zu einem Bericht: eine fehlgeschlagene Zeile bietet "Erneut senden" an.
let moveToast (report: ApplyReport) : ExternalMsg =
    let failure =
        report.Lines
        |> List.tryPick (fun l ->
            match l.Outcome with
            | Failed reason -> Some reason
            | _ -> None)
    match failure with
    | Some reason ->
        ShowToastWithAction (
            $"Nicht vollständig verschoben: {reason}",
            ToastError,
            { Label = retryMoveLabel; Kind = RetryBudgetMove report.ApplicationId }
        )
    | None when report.Lines |> List.exists (fun l -> l.Outcome = ChangedInYnab) ->
        ShowToast ("In YNAB geändert, nichts überschrieben", ToastWarning)
    | None -> ShowToast (moveSummaryText report, ToastSuccess)

/// Merkt sich eine Verschiebung mit fehlgeschlagener Zeile; ist nichts mehr offen, verschwindet
/// sie. Der Hinweis auf der Seite hängt daran, der Toast läuft nach fünf Sekunden ab.
let noteMoveReport (month: string) (report: ApplyReport) (model: Model) : Model =
    let others = model.OpenRetries |> List.filter (fun r -> r.ApplicationId <> report.ApplicationId)
    let reason =
        report.Lines
        |> List.tryPick (fun l ->
            match l.Outcome with
            | Failed reason -> Some reason
            | _ -> None)
    match reason with
    | Some reason ->
        let entry =
            { ApplicationId = report.ApplicationId
              MonthLabel = monthLabel month
              Summary = moveSummaryText report
              Reason = reason
              State = RetryReady }
        { model with OpenRetries = others @ [ entry ] }
    | None -> { model with OpenRetries = others }

/// Antwort auf "Verschieben". `Error` heißt: nichts geschrieben, das Sheet bleibt offen und nennt
/// den Grund. Ist es inzwischen zu (Seitenwechsel), nennt ein Toast den Grund. Mit Bericht schließt
/// es, der Monat wird neu gelesen (`true`).
let applyMoveAnswered (result: Result<ApplyReport, string>) (model: Model) : Model * ExternalMsg * bool =
    match result, model.Move with
    | Ok report, _ -> requestMonth (noteMoveReport report.Month report { model with Move = None }), moveToast report, true
    | Error message, Some form -> { model with Move = Some { form with Submission = Rejected message } }, NoOp, false
    | Error message, None -> model, ShowToast ($"Nicht verschoben: {message}", ToastError), false

/// Der Aufruf brach ab. Weil der Server relativ rechnet, wäre ein zweiter Versuch im offenen Sheet
/// gefährlich: es schließt, und der Monat wird neu gelesen.
let applyMoveCallLost (message: string) (model: Model) : Model * ExternalMsg =
    requestMonth { model with Move = None }, ShowToast ($"Ob verschoben wurde, ist unklar: {message}", ToastError)

/// "Erneut senden". `true` heißt: der Aufrufer muss senden. Läuft es schon oder ist die Verschiebung
/// nicht mehr offen, geschieht nichts.
let startMoveRetry (applicationId: string) (model: Model) : Model * bool =
    match model.OpenRetries |> List.tryFind (fun r -> r.ApplicationId = applicationId) with
    | Some { State = RetryReady } ->
        { model with
            OpenRetries =
                model.OpenRetries
                |> List.map (fun r -> if r.ApplicationId = applicationId then { r with State = RetryRunning } else r) },
        true
    | _ -> model, false

/// Antwort auf "Erneut senden". Scheitert der Aufruf, bleibt die Verschiebung offen und sendbar,
/// ein Toast nennt den Grund. In beiden Fällen liest der Aufrufer den Monat neu, denn der Lauf kann
/// YNAB erreicht haben.
let applyMoveRetried (applicationId: string) (result: Result<ApplyReport, string>) (model: Model) : Model * ExternalMsg =
    match result with
    | Ok report -> requestMonth (noteMoveReport report.Month report model), moveToast report
    | Error message ->
        let reopened =
            model.OpenRetries
            |> List.map (fun r -> if r.ApplicationId = applicationId then { r with State = RetryReady } else r)
        requestMonth { model with OpenRetries = reopened }, ShowToast ($"Erneut senden gescheitert: {message}", ToastError)

// ============================================
// Auswahl und Inspektor (budgeting-g8s3w)
// ============================================

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

/// Kategorie wählen. Dieselbe Id ändert nichts, eine andere setzt die Buchungen zurück. Laufen
/// Vorschau oder Sheet "Verschieben", geschieht nichts.
let selectCategory (categoryId: System.Guid) (model: Model) : Model =
    match model.Month, model.Targets, model.Move with
    | Success month, TargetsClosed, None
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

/// Sind die Buchungen fällig? Nur im Tab Buchungen, bei geladenem Monat und `NotAsked`. Das liest
/// nur das Kassenbuch und kostet kein YNAB-Limit.
let entriesDue (model: Model) : (System.Guid * string) option =
    match model.Tab, model.Selected, model.Month with
    | EntriesTab, Some { CategoryId = id; Entries = NotAsked }, Success month -> Some (id, month.Month)
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

/// "Wiederholen" in der Liste der Buchungen.
let reloadCategoryEntries (model: Model) : Model =
    resetEntries model

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
    /// Rückkehr aus dem Kassenbuch: nur Monat und Buchungen, kein YNAB-Aufruf für den Rahmen.
    | ReturnedFromLedger

/// Die Seite wird betreten. Ein Sheet überlebt den Seitenwechsel nicht. Wartet die Auswahl auf die
/// Rückkehr aus dem Kassenbuch, stehen Auswahl, Tab, Filter und Suche wieder da und die Liste kommt
/// neu, weil die Buchung dort geändert worden sein kann. Sonst beginnt die Seite von vorn.
let enterPage (model: Model) : Model * PageEntry =
    let entered = { model with Move = None }
    if returnPending model then
        let returned =
            resetEntries
                { entered with
                    Selected = entered.Selected |> Option.map (fun s -> { s with ReturnPending = false }) }
        returned, ReturnedFromLedger
    else
        { entered with
            CategorySearch = ""
            Filter = AllCategories
            Selected = None
            Tab = MoneyTab },
        FreshEntry
