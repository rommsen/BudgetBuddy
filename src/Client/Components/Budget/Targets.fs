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
    | ApplyFailed _ -> awaitMonth { model with Targets = TargetsClosed }, true
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

/// Die Zeilen des Berichts. Geschriebene stehen nach den Problemfällen, damit Fehlgeschlagene oben sichtbar sind.
let reportLines (report: ApplyReport) : ApplyLineResult list =
    report.Lines |> List.sortBy (fun l -> match l.Outcome with Written -> 1 | _ -> 0)
