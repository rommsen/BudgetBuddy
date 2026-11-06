/// "Targets anwenden" als reine Übergänge (budgeting-h2zfn).
module Components.Budget.Targets

open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Client.DesignSystem

/// Dialog öffnen oder nach gescheiterter Vorschau "Wiederholen". Nur bei geladenem Monat und nur,
/// wenn der Dialog zu ist oder an der Vorschau scheiterte; `true` heißt: der Aufrufer muss die
/// Vorschau holen.
let openTargets (model: Model) : Model * bool =
    match model.Month, model.Targets with
    | Success _, (TargetsClosed | PreviewFailed _)
        when shownMonthKind model |> Option.exists targetsAllowed && not (monthChanging model) ->
        { model with Targets = PreviewLoading }, true
    | _ -> model, false

/// Antwort der Vorschau einnehmen. Eine Antwort zu einem anderen Monat oder zu einem
/// inzwischen geschlossenen Dialog wird verworfen.
let applyPreviewLoaded (month: string) (result: Result<TargetPreview, string>) (model: Model) : Model =
    if shownMonth model <> Some month || model.Targets <> PreviewLoading then
        model
    else
        match result with
        | Ok preview -> { model with Targets = PreviewReady { Shown = preview; Changed = false; Excluded = Set.empty } }
        | Error message -> { model with Targets = PreviewFailed message }

/// Die Vorschau mit der Auswahl: nur die gewählten Zeilen, Summe und Zu verteilen danach folgen ihnen
/// (budgeting-m3x7f). Das zeigt der Dialog, und das geht an den Server.
let selectedPreview (shown: PreviewShown) : TargetPreview =
    previewWithLines shown.Shown (shown.Shown.Lines |> List.filter (fun l -> not (shown.Excluded.Contains l.CategoryId)))

/// Ist die Zeile gewählt?
let lineChosen (shown: PreviewShown) (categoryId: System.Guid) : bool =
    not (shown.Excluded.Contains categoryId)

/// Die Checkbox einer Zeile: ab- oder wieder anwählen. Nur, solange die Vorschau steht.
let toggleTargetLine (categoryId: System.Guid) (model: Model) : Model =
    match model.Targets with
    | PreviewReady shown ->
        let excluded =
            if shown.Excluded.Contains categoryId then shown.Excluded.Remove categoryId else shown.Excluded.Add categoryId
        { model with Targets = PreviewReady { shown with Excluded = excluded } }
    | _ -> model

/// Kann "Anwenden" senden? Nur mit mindestens einer gewählten Zeile.
let canApplyTargets (shown: PreviewShown) : bool =
    not (List.isEmpty (selectedPreview shown).Lines)

/// "Anwenden". Gibt die Vorschau zurück, die an den Server geht: nur die gewählten Zeilen, abgewählte werden nicht
/// geschrieben. Ohne gewählte Zeile gibt es nichts anzuwenden; ein zweiter Klick während des Laufs tut nichts. Ein
/// negatives Zu verteilen danach sperrt nichts.
let confirmTargets (model: Model) : Model * TargetPreview option =
    match model.Targets with
    | PreviewReady shown when canApplyTargets shown ->
        let sent = selectedPreview shown
        { model with Targets = Applying sent }, Some sent
    | _ -> model, None

/// Antwort auf "Übernehmen". Hat sich YNABs Stand geändert, steht die neue Vorschau mit dem
/// Hinweis da (nichts wurde geschrieben).
let applyApplied (result: Result<ApplyResult, string>) (model: Model) : Model =
    match model.Targets with
    | Applying preview ->
        match result with
        | Ok (Applied report) ->
            { model with Targets = ApplyDone { Sent = preview; Report = report; Retrying = false; RetryError = None } }
        | Ok (PreviewChanged fresh) -> { model with Targets = PreviewReady { Shown = fresh; Changed = true; Excluded = Set.empty } }
        | Error message -> { model with Targets = ApplyFailed (preview, message) }
    | _ -> model

/// "Fehlgeschlagene erneut senden". Gibt die Anwendung zurück, die der Server erneut senden
/// soll; ohne fehlgeschlagene Zeile oder während eines Laufs `None`.
let retryFailed (model: Model) : Model * string option =
    match model.Targets with
    | ApplyDone applied when not applied.Retrying && failedCount applied.Report > 0 ->
        { model with Targets = ApplyDone { applied with Retrying = true; RetryError = None } }, Some applied.Report.ApplicationId
    | _ -> model, None

let applyRetried (result: Result<ApplyReport, string>) (model: Model) : Model =
    match model.Targets with
    | ApplyDone applied when applied.Retrying ->
        match result with
        | Ok report -> { model with Targets = ApplyDone { applied with Report = report; Retrying = false; RetryError = None } }
        | Error message -> { model with Targets = ApplyDone { applied with Retrying = false; RetryError = Some message } }
    | _ -> model

/// Dialog schließen. Während Übernehmen oder erneutem Senden läuft, bleibt er offen. Nach
/// einer Anwendung (auch nach einem gescheiterten Aufruf) muss die Seite den Monat neu
/// lesen (`Some`), weil YNABs Stand sich geändert haben kann. Nach einem Bericht bleibt die Liste
/// stehen, mit den geschriebenen Zeilen eingetragen, und der Monat wird still gelesen. Ist der Ausgang
/// unklar (gescheiterter Lauf oder gescheitertes erneutes Senden), ist der alte Stand keine
/// Rechengrundlage: `awaitMonth`.
let closeTargets (model: Model) : Model * MonthReread option =
    match model.Targets with
    | Applying _ -> model, None
    | ApplyDone applied when applied.Retrying -> model, None
    | ApplyDone { Report = report; RetryError = None } ->
        let closed, reread = rereadMonth (withReport report { model with Targets = TargetsClosed })
        closed, Some reread
    | ApplyDone _
    | ApplyFailed _ -> awaitMonth { model with Targets = TargetsClosed }, Some FullReread
    | _ -> { model with Targets = TargetsClosed }, None

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

/// Die Zeilen des Berichts. Geschriebene stehen nach den Problemfällen, damit Fehlgeschlagene oben sichtbar sind.
let reportLines (report: ApplyReport) : ApplyLineResult list =
    report.Lines |> List.sortBy (fun l -> match l.Outcome with Written -> 1 | _ -> 0)
