/// Modell und Nachrichten der Budget-Seite (budgeting-k7wmd).
///
/// Die Seite liest den Monat live aus YNAB und merkt sich nichts außer dem, was gerade
/// angezeigt wird. "Targets anwenden" (budgeting-h2zfn) setzt auf diesem Modell auf: es
/// braucht den gewählten Monat (`Choice`) und die geladenen Kategorien mit Fehlbetrag und
/// Pausiert (`Month`). "Verschieben" (budgeting-afd8h) öffnet sich am Verfügbar-Betrag.
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

/// Das Sheet "Verschieben" zu einer Kategorie X (budgeting-afd8h).
type MoveForm = {
    /// X, die Kategorie, an deren Verfügbar-Betrag getippt wurde.
    Category: BudgetCategory
    /// Ist X die Quelle? Sonst das Ziel.
    IsSource: bool
    /// Die Gegenseite; `None`, solange keine gewählt ist.
    Counterpart: MoveSide option
    /// Eingabe im Betragsfeld.
    AmountText: string
    PickerOpen: bool
    /// Der Aufruf läuft: der Knopf ist gesperrt und zeigt den Ladezustand.
    Sending: bool
    /// Warum nichts geschrieben wurde (Monat nicht lesbar, Auftrag abgelehnt).
    Error: string option
}

type Model = {
    /// Welcher Monat gewählt ist. Vorgabe: der laufende.
    Choice: BudgetMonthChoice
    /// Der geladene Monat zur Wahl `Choice`.
    Month: RemoteData<BudgetMonth>
    /// Der Dialog "Targets anwenden".
    Targets: TargetsState
    /// Suchtext über der Kategorie-Liste (budgeting-rxxb4). Bleibt beim Monatswechsel stehen,
    /// wird nicht gespeichert.
    CategorySearch: string
    /// Das offene Sheet "Verschieben".
    Move: MoveForm option
}

type Msg =
    /// Die Seite wird betreten: Suche leeren, Monat holen.
    | PageEntered
    /// Den gewählten Monat (neu) holen: Seitenaufruf und "Wiederholen".
    | Load
    /// Umschalter dieser/nächster Monat.
    | SelectMonth of BudgetMonthChoice
    /// Antwort des Servers, mit der Wahl, zu der sie gehört.
    | Loaded of BudgetMonthChoice * Result<BudgetMonth, string>
    /// Knopf "Targets anwenden": Vorschau öffnen.
    | OpenTargets
    /// Antwort der Vorschau, mit der Monatswahl, zu der sie gehört.
    | PreviewLoaded of BudgetMonthChoice * Result<TargetPreview, string>
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
    /// Tipp auf den Verfügbar-Betrag einer Kategorie.
    | OpenMove of BudgetCategory
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
    /// "Erneut senden" am Toast.
    | RetryMove of applicationId: string
    | MoveRetried of Result<ApplyReport, string>

/// Was die Seite an die App meldet (wie im Kassenbuch).
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | ShowToastWithAction of string * ToastType * ToastAction

let init () : Model =
    { Choice = CurrentMonth; Month = NotAsked; Targets = TargetsClosed; CategorySearch = ""; Move = None }

/// Antwort einnehmen. Eine Antwort zu einer inzwischen abgewählten Monatswahl wird
/// verworfen, damit ein langsamer Aufruf den gerade gezeigten Monat nicht überschreibt.
let applyLoaded (choice: BudgetMonthChoice) (result: Result<BudgetMonth, string>) (model: Model) : Model =
    if choice <> model.Choice then
        model
    else
        match result with
        | Ok month -> { model with Month = Success month }
        | Error message -> { model with Month = Failure message }

/// Monat wählen. Dieselbe Wahl noch einmal ändert nichts (`false`); eine andere setzt den
/// Monat auf `Loading` (`true` heißt: der Aufrufer muss laden).
let select (choice: BudgetMonthChoice) (model: Model) : Model * bool =
    if choice = model.Choice && model.Month <> NotAsked then
        model, false
    else
        { model with Choice = choice; Month = Loading }, true

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

// ============================================
// Targets anwenden (budgeting-h2zfn)
// ============================================

/// Dialog öffnen. Nur bei geladenem Monat und nur, wenn der Dialog zu ist; `true` heißt: der
/// Aufrufer muss die Vorschau holen.
let openTargets (model: Model) : Model * bool =
    match model.Month, model.Targets with
    | Success _, TargetsClosed -> { model with Targets = PreviewLoading }, true
    | _ -> model, false

/// Antwort der Vorschau einnehmen. Eine Antwort zu einer abgewählten Monatswahl oder zu
/// einem inzwischen geschlossenen Dialog wird verworfen.
let applyPreviewLoaded (choice: BudgetMonthChoice) (result: Result<TargetPreview, string>) (model: Model) : Model =
    if choice <> model.Choice || model.Targets <> PreviewLoading then
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
    | ApplyFailed _ -> { model with Targets = TargetsClosed }, true
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

/// Der Zustand beim Öffnen. Verfügbar negativ: X wird gedeckt, ist also das Ziel, Betrag ist der
/// Fehlbetrag. Sonst ist X die Quelle, Betrag ist Verfügbar.
let openMoveForm (category: BudgetCategory) : MoveForm =
    { Category = category
      IsSource = category.Available.Amount >= 0m
      Counterpart = None
      AmountText = formatAmountForInput category.Available.Amount
      PickerOpen = false
      Sending = false
      Error = None }

/// Der Umschalter dreht Quelle und Ziel, der Betrag bleibt.
let toggleDirection (form: MoveForm) : MoveForm =
    { form with IsSource = not form.IsSource }

/// Der Betrag der Eingabe; leer, unlesbar und 0 sind keiner.
let moveAmount (form: MoveForm) : decimal option =
    parseAmountInput form.AmountText |> Option.filter (fun amount -> amount > 0m)

/// Darf "Verschieben" gedrückt werden?
let canMove (form: MoveForm) : bool =
    not form.Sending && form.Counterpart.IsSome && (moveAmount form).IsSome

/// Der Auftrag an den Server; `None`, solange etwas fehlt.
let moveRequest (form: MoveForm) : MoveRequest option =
    match form.Counterpart, moveAmount form with
    | Some counterpart, Some amount ->
        let x = CategorySide form.Category.Id
        let source, target = if form.IsSource then x, counterpart else counterpart, x
        Some { Source = source; Target = target; Amount = { Amount = amount; Currency = form.Category.Available.Currency } }
    | _ -> None

let private categoriesOf (month: BudgetMonth) : BudgetCategory list =
    month.Groups |> List.collect (fun g -> g.Categories)

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

/// "Verfügbar danach" für X und, sobald gewählt, die Gegenseite. Ohne gültigen Betrag bleiben die
/// heutigen Werte stehen. Ein negatives Ergebnis ist gültig.
let moveAfter (month: BudgetMonth) (form: MoveForm) : MoveAfterLine list =
    let amount = moveAmount form |> Option.defaultValue 0m
    let change = if form.IsSource then -amount else amount
    let shifted (m: Money) (delta: decimal) = { m with Amount = m.Amount + delta }
    let counterpart =
        match form.Counterpart with
        | None -> None
        | Some ToBeBudgetedSide -> Some month.ToBeBudgeted
        | Some (CategorySide id) ->
            categoriesOf month |> List.tryFind (fun c -> c.Id = id) |> Option.map (fun c -> c.Available)
    [ { Label = form.Category.Name; After = shifted form.Category.Available change }
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
    let other = form.Counterpart |> Option.map (counterpartName month) |> Option.defaultValue "…"
    if form.IsSource then $"Von {form.Category.Name} nach {other}"
    else $"{form.Category.Name} decken aus {other}"

/// Sheet öffnen, nur bei geladenem Monat und ohne offenen Dialog.
let openMove (category: BudgetCategory) (model: Model) : Model =
    match model.Month, model.Targets, model.Move with
    | Success _, TargetsClosed, None -> { model with Move = Some (openMoveForm category) }
    | _ -> model

/// Sheet schließen. Während des Aufrufs bleibt es offen.
let closeMove (model: Model) : Model =
    match model.Move with
    | Some form when form.Sending -> model
    | _ -> { model with Move = None }

/// Ändert das offene Formular, außer während des Aufrufs.
let updateForm (change: MoveForm -> MoveForm) (model: Model) : Model =
    match model.Move with
    | Some form when not form.Sending -> { model with Move = Some (change form) }
    | _ -> model

/// "Verschieben". Gibt den Auftrag zurück; ist der Knopf gesperrt oder läuft schon ein Aufruf,
/// geschieht nichts. Sonst ginge beim Doppeltipp das Geld zweimal, denn der Server rechnet relativ.
let confirmMove (model: Model) : Model * MoveRequest option =
    match model.Move with
    | Some form when canMove form ->
        { model with Move = Some { form with Sending = true; Error = None } }, moveRequest form
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

/// Antwort auf "Verschieben". `Error` heißt: nichts geschrieben, das Sheet bleibt offen und nennt
/// den Grund. Mit Bericht schließt es, der Monat wird neu gelesen (`true`).
let applyMoveAnswered (result: Result<ApplyReport, string>) (model: Model) : Model * ExternalMsg * bool =
    match result with
    | Ok report -> { model with Move = None }, moveToast report, true
    | Error message ->
        let form = model.Move |> Option.map (fun f -> { f with Sending = false; Error = Some message })
        { model with Move = form }, NoOp, false

/// Der Aufruf brach ab. Weil der Server relativ rechnet, wäre ein zweiter Versuch im offenen Sheet
/// gefährlich: es schließt, und der Monat wird neu gelesen.
let applyMoveCallLost (message: string) (model: Model) : Model * ExternalMsg =
    { model with Move = None }, ShowToast ($"Ob verschoben wurde, ist unklar: {message}", ToastError)
