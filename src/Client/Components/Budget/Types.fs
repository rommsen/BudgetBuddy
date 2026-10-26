/// Modell und Nachrichten der Budget-Seite (budgeting-k7wmd).
///
/// Die Seite liest den Monat live aus YNAB und merkt sich nichts außer dem, was gerade
/// angezeigt wird. "Targets anwenden" (budgeting-h2zfn) setzt auf diesem Modell auf: es
/// braucht den gewählten Monat (`Choice`) und die geladenen Kategorien mit Fehlbetrag und
/// Pausiert (`Month`).
module Components.Budget.Types

open Shared.Budgeting
open Types

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

type Model = {
    /// Welcher Monat gewählt ist. Vorgabe: der laufende.
    Choice: BudgetMonthChoice
    /// Der geladene Monat zur Wahl `Choice`.
    Month: RemoteData<BudgetMonth>
    /// Der Dialog "Targets anwenden".
    Targets: TargetsState
}

type Msg =
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

let init () : Model =
    { Choice = CurrentMonth; Month = NotAsked; Targets = TargetsClosed }

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
