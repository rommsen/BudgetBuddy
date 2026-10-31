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
    /// X, die Kategorie, an deren Verfügbar-Betrag getippt wurde.
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
    Summary: string
    Reason: string
    State: MoveRetryState
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
    /// Verschiebungen, die noch erneut gesendet werden können.
    OpenRetries: OpenMoveRetry list
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
    /// Antwort des stillen Neu-Lesens nach einer Verschiebung.
    | Reloaded of BudgetMonthChoice * Result<BudgetMonth, string>
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

/// Was die Seite an die App meldet (wie im Kassenbuch).
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | ShowToastWithAction of string * ToastType * ToastAction

let init () : Model =
    { Choice = CurrentMonth
      Month = NotAsked
      Targets = TargetsClosed
      CategorySearch = ""
      Move = None
      OpenRetries = [] }

let private categoriesOf (month: BudgetMonth) : BudgetCategory list =
    month.Groups |> List.collect (fun g -> g.Categories)

/// Kategorie X des Sheets im Monat.
let moveCategory (month: BudgetMonth) (form: MoveForm) : BudgetCategory option =
    categoriesOf month |> List.tryFind (fun c -> c.Id = form.CategoryId)

/// Antwort einnehmen. Eine Antwort zu einer inzwischen abgewählten Monatswahl wird
/// verworfen, damit ein langsamer Aufruf den gerade gezeigten Monat nicht überschreibt. Fehlt X im
/// neuen Stand, schließt ein offenes Sheet, außer während des Aufrufs.
let applyLoaded (choice: BudgetMonthChoice) (result: Result<BudgetMonth, string>) (model: Model) : Model =
    if choice <> model.Choice then
        model
    else
        match result with
        | Ok month ->
            let move =
                model.Move
                |> Option.filter (fun form -> form.Submission = Sending || (moveCategory month form).IsSome)
            { model with Month = Success month; Move = move }
        | Error message -> { model with Month = Failure message }

/// Das stille Neu-Lesen nach einer Verschiebung. Scheitert es, bleibt die Liste stehen und ein
/// Hinweis nennt den Grund: die Seite verliert nicht ihren Stand, weil ein Nachladen ausfiel.
let applyReloaded
    (choice: BudgetMonthChoice)
    (result: Result<BudgetMonth, string>)
    (model: Model)
    : Model * ExternalMsg =
    match model.Month, result with
    | Success _, Error message when choice = model.Choice ->
        model, ShowToast ($"Beträge nicht neu gelesen, sie können veraltet sein: {message}", ToastWarning)
    | _ -> applyLoaded choice result model, NoOp

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
let noteMoveReport (report: ApplyReport) (model: Model) : Model =
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
    | Ok report, _ -> noteMoveReport report { model with Move = None }, moveToast report, true
    | Error message, Some form -> { model with Move = Some { form with Submission = Rejected message } }, NoOp, false
    | Error message, None -> model, ShowToast ($"Nicht verschoben: {message}", ToastError), false

/// Der Aufruf brach ab. Weil der Server relativ rechnet, wäre ein zweiter Versuch im offenen Sheet
/// gefährlich: es schließt, und der Monat wird neu gelesen.
let applyMoveCallLost (message: string) (model: Model) : Model * ExternalMsg =
    { model with Move = None }, ShowToast ($"Ob verschoben wurde, ist unklar: {message}", ToastError)

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
    | Ok report -> noteMoveReport report model, moveToast report
    | Error message ->
        let reopened =
            model.OpenRetries
            |> List.map (fun r -> if r.ApplicationId = applicationId then { r with State = RetryReady } else r)
        { model with OpenRetries = reopened }, ShowToast ($"Erneut senden gescheitert: {message}", ToastError)
