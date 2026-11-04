/// Target pausieren und Pause aufheben (budgeting-t6p8z, ADR 0073, ADR 0079): was der Knopf zeigt, wann
/// der Aufruf startet und wie die Antwort in den geladenen Monat kommt. Alles rein, die View ruft es nur auf.
module Components.Budget.Pause

open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month

/// Der Knopf: Beschriftung und die Absicht, die ein Tipp sendet.
type PauseButton = {
    Label: string
    Intent: PauseIntent
}

/// Was an der Stelle des Pause-Knopfs steht.
type PauseControl =
    /// Ohne Target: kein Knopf, kein Platz.
    | NoPauseControl
    /// Vergangener Monat: kein Knopf, sein Platz bleibt frei (wie Targets anwenden).
    | PauseSlotFree
    /// Der Knopf, wenn BB etwas tun kann, und der Satz zur YNAB-Pause, wenn sie in YNAB gesetzt ist.
    | PauseOffered of button: PauseButton option * ynabNote: string option

/// Der Satz, wenn die Pause nur in YNAB gesetzt ist (ADR 0073 Punkt 2).
let ynabPauseNote = "Die Pause ist in YNAB gesetzt und lässt sich nur dort aufheben."

/// Der Satz, wenn die Pause in BB und in YNAB steht: der Knopf hebt nur die BB-Pause auf (budgeting-r3wq8).
let bothPauseNote = "In YNAB ist das Target ebenfalls pausiert, diese Pause lässt sich nur dort aufheben."

/// Sichtbarkeit und Beschriftung des Knopfs. Pausieren geht im laufenden und in künftigen Monaten
/// (`targetsAllowed`), nur mit Target, auch wenn es erfüllt ist. Aufheben gilt nur der BB-Pause.
let pauseControl (kind: MonthKind) (category: BudgetCategory) : PauseControl =
    match category.Target with
    | None -> NoPauseControl
    | Some _ when not (targetsAllowed kind) -> PauseSlotFree
    | Some _ ->
        match category.Pause with
        | None -> PauseOffered (Some { Label = "Target pausieren"; Intent = PauseTarget }, None)
        | Some PausedInBb -> PauseOffered (Some { Label = "Pause aufheben"; Intent = LiftPause }, None)
        | Some PausedInBoth -> PauseOffered (Some { Label = "Pause in BB aufheben"; Intent = LiftPause }, Some bothPauseNote)
        | Some PausedInYnab -> PauseOffered (None, Some ynabPauseNote)

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
    | PauseRunning (c, m, _), Some shown -> c = categoryId && m = shown
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
let startPause (intent: PauseIntent) (model: Model) : Model * (string * TargetPauseRequest) option =
    match model.Selected, model.Month with
    | Some selected, Success month when not (pauseBusy model) && not (monthChanging model) ->
        match categoriesOf month |> List.tryFind (fun c -> c.Id = selected.CategoryId) with
        | Some category ->
            match pauseControlFor model category with
            | PauseOffered (Some button, _) when button.Intent = intent ->
                { model with PauseCall = PauseRunning (category.Id, month.Month, model.MonthGeneration) },
                Some (month.Month, { CategoryId = category.Id; Intent = intent })
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
        | PauseRunning (c, m, _) when c = categoryId && m = month ->
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

/// Steht der Pause-Knopf der Kategorie im offenen Tab? Nur dann ist eine Meldung dort zu sehen: im Tab
/// Target und im Tab Geld im Modus Zuweisen (budgeting-c6vn4, P2; Vorbild `targetAnswerInTab`).
let pauseAnswerInTab (categoryId: System.Guid) (month: string) (model: Model) : bool =
    pauseAnswerShown categoryId month model
    && (match model.Tab with
        | TargetTab -> true
        | MoneyTab -> model.TabMoney |> Option.forall (fun tab -> tab.Mode = AssignMode)
        | EntriesTab
        | HistoryTab -> false)

/// Der Toast, wenn der Server die Pause ablehnte und die Meldung nicht im offenen Tab steht.
let pauseRejectedAwayText (message: string) : string =
    $"Pause nicht gespeichert: {message}"

/// Die Monatsgeneration beim Start des Aufrufs für Kategorie und Monat, solange er läuft.
let pauseStartGeneration (categoryId: System.Guid) (month: string) (model: Model) : int option =
    match model.PauseCall with
    | PauseRunning (c, m, generation) when c = categoryId && m = month -> Some generation
    | _ -> None

/// Kann ein Lesen des Monats die Pause verpasst haben? Dann kennte seine Antwort sie nicht und nähme sie
/// zurück: der Monat wird noch einmal gelesen. Das gilt, wenn der Monat gewählt ist, und wenn ein
/// Neu-Lesen läuft (`MonthStale`), seit dem Start eines begann (Generation), der Monat gerade nicht
/// geladen ist (budgeting-r3wq8) oder ein Wechsel zu ihm läuft: dessen Lesen kann vor dem Speichern
/// begonnen haben (budgeting-c6vn4, Q1). `startGeneration` ist die Generation beim Start des Aufrufs,
/// vor dem Einnehmen der Antwort gelesen (`pauseStartGeneration`).
let pauseNeedsReread (month: string) (startGeneration: int option) (model: Model) : bool =
    let generationMoved = startGeneration |> Option.exists (fun g -> g <> model.MonthGeneration)
    let notLoaded =
        match model.Month with
        | Success _ -> false
        | _ -> true
    shownMonth model = Some month
    && (model.Freshness = MonthStale || generationMoved || notLoaded || monthChanging model)

/// Der Aufruf brach ab: ob die Pause gespeichert ist, ist unbekannt. Die Meldung steht im Tab wie eine
/// Ablehnung, der Aufrufer liest den Monat still neu, wenn er ihn zeigt (Vorbild `applyMoveCallLost`).
/// Nur dann verspricht der Text das Neu-Lesen (budgeting-c6vn4, P2). Ein Punkt am Ende der Meldung
/// fällt weg, sonst stünden zwei.
let pauseCallLostText (rereads: bool) (message: string) : string =
    let reason = message.TrimEnd('.')
    if rereads then $"Ob die Pause gespeichert ist, ist unklar: {reason}. Der Stand wird gleich neu gelesen."
    else $"Ob die Pause gespeichert ist, ist unklar: {reason}."

/// Liest der Abbruch den Monat neu? Wenn der Monat des Aufrufs gewählt ist, auch während eines Wechsels zu
/// ihm: dessen Lesen kann vor dem Speichern begonnen haben (budgeting-c6vn4, Q1).
let pauseCallLostRereads (month: string) (model: Model) : bool =
    shownMonth model = Some month

let applyPauseCallLost (categoryId: System.Guid) (month: string) (message: string) (model: Model) : Model =
    match model.PauseCall with
    | PauseRunning (c, m, _) when c = categoryId && m = month ->
        { model with PauseCall = PauseRejected (c, m, pauseCallLostText (pauseCallLostRereads month model) message) }
    | _ -> model

/// Eine Meldung gilt nur, solange ihre Kategorie und ihr Monat gezeigt werden.
let syncPause (model: Model) : Model =
    match model.PauseCall with
    | PauseRejected (c, m, _) when not (pauseAnswerShown c m model) -> { model with PauseCall = PauseIdle }
    | _ -> model
