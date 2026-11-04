/// Target pausieren und Pause aufheben (budgeting-t6p8z, ADR 0073, ADR 0079): was der Knopf zeigt, wann
/// der Aufruf startet und wie die Antwort in den geladenen Monat kommt. Alles rein, die View ruft es nur auf.
module Components.Budget.Pause

open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month

/// Der Knopf: Beschriftung und der Stand, den ein Tipp verlangt.
type PauseButton = {
    Label: string
    /// `true` pausiert in BB, `false` hebt die BB-Pause auf.
    Paused: bool
}

/// Was an der Stelle des Pause-Knopfs steht.
type PauseControl =
    /// Ohne Target: kein Knopf, kein Platz.
    | NoPauseControl
    /// Vergangener Monat: kein Knopf, sein Platz bleibt frei (wie Targets anwenden).
    | PauseSlotFree
    /// Der Knopf, wenn BB etwas tun kann, und der Satz zur YNAB-Pause, wenn sie in YNAB gesetzt ist.
    | PauseOffered of button: PauseButton option * ynabNote: string option

/// Der Satz, wenn die Pause in YNAB gesetzt ist (ADR 0073 Punkt 2).
let ynabPauseNote = "Die Pause ist in YNAB gesetzt und lässt sich nur dort aufheben."

/// Sichtbarkeit und Beschriftung des Knopfs. Pausieren geht im laufenden und in künftigen Monaten
/// (`targetsAllowed`), nur mit Target, auch wenn es erfüllt ist. Aufheben gilt nur der BB-Pause.
let pauseControl (kind: MonthKind) (category: BudgetCategory) : PauseControl =
    match category.Target with
    | None -> NoPauseControl
    | Some _ when not (targetsAllowed kind) -> PauseSlotFree
    | Some _ ->
        let note = if pausedInYnab category.Pause then Some ynabPauseNote else None
        let button =
            if pausedInBb category.Pause then Some { Label = "Pause aufheben"; Paused = false }
            elif pausedInYnab category.Pause then None
            else Some { Label = "Target pausieren"; Paused = true }
        PauseOffered (button, note)

/// `pauseControl` für den Monat, den die Seite darstellt (während eines Wechsels noch der alte).
let pauseControlFor (model: Model) (category: BudgetCategory) : PauseControl =
    match displayedMonthKind model with
    | Some kind -> pauseControl kind category
    | None -> NoPauseControl

let private displayedMonth (model: Model) : string option =
    match model.Month with
    | Success month -> Some month.Month
    | _ -> None

/// Läuft der Aufruf für diese Kategorie im dargestellten Monat? Dann dreht der Knopf.
let pauseRunningFor (model: Model) (categoryId: System.Guid) : bool =
    match model.PauseCall, displayedMonth model with
    | PauseRunning (c, m), Some shown -> c = categoryId && m = shown
    | _ -> false

/// Läuft irgendein Aufruf? Dann sendet ein zweiter Tipp nichts.
let pauseBusy (model: Model) : bool =
    match model.PauseCall with
    | PauseRunning _ -> true
    | PauseIdle
    | PauseRejected _ -> false

/// Die Meldung zur Kategorie im dargestellten Monat, wenn der letzte Aufruf scheiterte.
let pauseError (model: Model) (categoryId: System.Guid) : string option =
    match model.PauseCall, displayedMonth model with
    | PauseRejected (c, m, message), Some shown when c = categoryId && m = shown -> Some message
    | _ -> None

/// Der Tipp auf den Knopf. Gesendet wird nur, was `pauseControlFor` gerade anbietet, nie während ein
/// Aufruf läuft und nie in einen wechselnden Monat. Ergebnis: Monat und Auftrag für den Aufruf.
let startPause (paused: bool) (model: Model) : Model * (string * TargetPauseRequest) option =
    match model.Selected, model.Month with
    | Some selected, Success month when not (pauseBusy model) && not (monthChanging model) ->
        match categoriesOf month |> List.tryFind (fun c -> c.Id = selected.CategoryId) with
        | Some category ->
            match pauseControlFor model category with
            | PauseOffered (Some button, _) when button.Paused = paused ->
                { model with PauseCall = PauseRunning (category.Id, month.Month) },
                Some (month.Month, { CategoryId = category.Id; Paused = paused })
            | _ -> model, None
        | None -> model, None
    | _ -> model, None

/// Setzt die Pause der Kategorie im geladenen Monat, wenn er der Monat des Aufrufs ist. YNAB wird nicht
/// neu gelesen: die Pause ändert dort nichts. Fehlt, Frei und die Filterzahlen folgen daraus.
let withPause (categoryId: System.Guid) (month: string) (pause: PauseSource option) (model: Model) : Model =
    match model.Month with
    | Success shown when shown.Month = month ->
        let set (c: BudgetCategory) = if c.Id = categoryId then { c with Pause = pause } else c
        let groups = shown.Groups |> List.map (fun g -> { g with Categories = g.Categories |> List.map set })
        { model with Month = Success { shown with Groups = groups } }
    | _ -> model

/// Nimmt die Antwort ein. Sie wirkt nur auf ihre Kategorie und ihren Monat. Ein Fehler lässt die Pause,
/// wie sie war, und steht im Tab, solange Kategorie und Monat gezeigt werden.
let applyPauseAnswered
    (categoryId: System.Guid)
    (month: string)
    (result: Result<PauseSource option, string>)
    (model: Model)
    : Model =
    let call =
        match model.PauseCall with
        | PauseRunning (c, m) when c = categoryId && m = month ->
            match result with
            | Ok _ -> PauseIdle
            | Error message -> PauseRejected (c, m, message)
        | call -> call
    let answered = { model with PauseCall = call }
    match result with
    | Ok pause -> withPause categoryId month pause answered
    | Error _ -> answered

/// Gilt die Antwort der gezeigten Kategorie im dargestellten Monat? Dann ist ihre Historie wieder fällig.
let pauseAnswerShown (categoryId: System.Guid) (month: string) (model: Model) : bool =
    model.Selected |> Option.exists (fun s -> s.CategoryId = categoryId) && displayedMonth model = Some month

/// Läuft gerade ein Neu-Lesen des Monats, das vor der Pause begonnen haben kann? Dann kennte seine
/// Antwort die Pause nicht und nähme sie zurück: der Monat wird noch einmal gelesen.
let pauseNeedsReread (month: string) (model: Model) : bool =
    model.Freshness = MonthStale && displayedMonth model = Some month

/// Eine Meldung gilt nur, solange ihre Kategorie und ihr Monat gezeigt werden.
let syncPause (model: Model) : Model =
    match model.PauseCall with
    | PauseRejected (c, m, _) when not (pauseAnswerShown c m model) -> { model with PauseCall = PauseIdle }
    | _ -> model
