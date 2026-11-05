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

/// Läuft eine Pause dieser Kategorie, in welchem Monat auch immer? Dann warten Speichern und Entfernen im
/// Tab Target derselben Kategorie (budgeting-f8w2n, ADR 0080 Punkt 5).
let pauseCallFor (model: Model) (categoryId: System.Guid) : bool =
    match model.PauseCall with
    | PauseRunning (c, _, _) -> c = categoryId
    | PauseIdle
    | PauseRejected _ -> false

/// Steht im Tab Target eine Rückfrage zur Kategorie offen (Warnung vor dem Wechsel der Art, Bestätigung von
/// "Target entfernen")? Dann ist der Pause-Knopf gesperrt: höchstens eine Sache im Tab wartet auf eine
/// Antwort (budgeting-g2ty9, X1).
let pauseHeldByConfirm (model: Model) (categoryId: System.Guid) : bool =
    model.Tab = TargetTab
    && model.TargetForm
       |> Option.exists (fun f -> f.CategoryId = categoryId && (f.ConfirmingChange || f.ConfirmingRemove))

/// Läuft ein Aufruf im Tab Target (Speichern, Wechseln, Entfernen) für diese Kategorie? Dann wartet ihre
/// Pause: je Kategorie läuft höchstens ein Aufruf, sonst gingen beide mit altem Stand hinaus
/// (budgeting-c2t9w). Ein Aufruf einer anderen Kategorie sperrt nicht (budgeting-f8w2n).
let private targetCallFor (model: Model) (categoryId: System.Guid) : bool =
    match model.TargetCall with
    | TargetSaving (c, _, _) -> c = categoryId
    | TargetIdle -> false

/// Ist der Pause-Knopf der Kategorie gesperrt? Solange irgendein Pause-Aufruf läuft (es gibt nur einen),
/// solange ein Target-Aufruf derselben Kategorie läuft und solange im Tab Target eine Rückfrage offen ist
/// (`pauseHeldByConfirm`). `startPause` sendet dann nichts.
let pauseLocked (model: Model) (categoryId: System.Guid) : bool =
    pauseBusy model || targetCallFor model categoryId || pauseHeldByConfirm model categoryId

/// Wie der Pause-Knopf aussieht: `Busy` dreht und behält den Fokus (`Button.IsBusy`), `Disabled` sperrt
/// (Vorbild `formSaveButton` im Kern `TargetFormCore`, budgeting-c2t9w).
type PauseButtonState = { Busy: bool; Disabled: bool }

/// Dreht, wenn der eigene Aufruf läuft, sonst gesperrt nach `pauseLocked`.
let pauseButtonState (model: Model) (categoryId: System.Guid) : PauseButtonState =
    let busy = pauseRunningFor model categoryId
    { Busy = busy; Disabled = not busy && pauseLocked model categoryId }

/// Der Satz unter dem Pause-Knopf, solange ein Target-Aufruf derselben Kategorie ihn sperrt (budgeting-f8w2n).
[<Literal>]
let PauseWaitsForTargetText = "Wartet, bis das Target gespeichert ist."

/// Der Name der Kategorie im geladenen Monat, wenn sie dort steht.
let categoryNameOf (model: Model) (categoryId: System.Guid) : string option =
    match model.Month with
    | Success month -> categoriesOf month |> List.tryFind (fun c -> c.Id = categoryId) |> Option.map (fun c -> c.Name)
    | _ -> None

/// Der Satz unter dem Pause-Knopf, solange die Pause einer anderen Kategorie läuft (budgeting-n7h3x). Ohne
/// Namen, wenn die Kategorie nicht im gezeigten Monat steht.
let pauseWaitsForPauseText (categoryName: string option) : string =
    match categoryName with
    | Some name -> $"Wartet, bis die Pause bei {name} gespeichert ist."
    | None -> "Wartet, bis die laufende Pause gespeichert ist."

/// Läuft die Pause einer anderen Kategorie? Dann ihre Kategorie: es läuft nur eine Pause zugleich.
let private pauseCallElsewhere (model: Model) (categoryId: System.Guid) : System.Guid option =
    match model.PauseCall with
    | PauseRunning (c, _, _) when c <> categoryId -> Some c
    | _ -> None

/// Steht unter dem Pause-Knopf, warum er wartet? Nur, wenn ein Knopf steht. Läuft die Pause einer anderen
/// Kategorie, nennt der Satz sie (budgeting-n7h3x) und hat Vorrang; sonst steht der Satz, wenn ein
/// Target-Aufruf derselben Kategorie sperrt: der Spinner steht dann im Tab Target, im Tab Geld sähe man
/// sonst keinen Grund. Bei der eigenen Pause dreht der Knopf, ein Satz steht nicht.
let pauseWaitHint (model: Model) (category: BudgetCategory) : string option =
    match pauseControlFor model category with
    | PauseOffered (Some _, _) ->
        match pauseCallElsewhere model category.Id with
        | Some other -> Some (pauseWaitsForPauseText (categoryNameOf model other))
        | None when targetCallFor model category.Id -> Some PauseWaitsForTargetText
        | None -> None
    | _ -> None

/// Die Meldung zur Kategorie im dargestellten Monat, wenn der letzte Aufruf scheiterte.
let pauseError (model: Model) (categoryId: System.Guid) : string option =
    match model.PauseCall, displayedMonth model with
    | PauseRejected (c, m, message), Some shown when c = categoryId && m = shown -> Some message
    | _ -> None

/// Der Tipp auf den Knopf. Gesendet wird nur, was `pauseControlFor` gerade anbietet, nie solange der Knopf
/// gesperrt ist (`pauseLocked`: ein Aufruf läuft oder im Tab Target ist eine Rückfrage offen) und nie in
/// einen wechselnden Monat. Ergebnis: Monat und Auftrag für den Aufruf.
let startPause (intent: PauseIntent) (model: Model) : Model * (string * TargetPauseRequest) option =
    match model.Selected, model.Month with
    | Some selected, Success month when not (monthChanging model) ->
        match categoriesOf month |> List.tryFind (fun c -> c.Id = selected.CategoryId) with
        | Some category ->
            match pauseControlFor model category with
            | PauseOffered (Some button, _) when button.Intent = intent && not (pauseLocked model category.Id) ->
                { model with PauseCall = PauseRunning (category.Id, month.Month, model.MonthGeneration) },
                Some (month.Month, { CategoryId = category.Id; Intent = intent; CommandId = None })
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
/// zurück: der Monat wird noch einmal gelesen. Das gilt, wenn der Monat gewählt ist und
/// - ein Neu-Lesen läuft (`MonthStale`), auch eines, das vor dem Start begann,
/// - seit dem Start ein Lesen begann (Generation, `requestMonth`), auch ein Wechsel hin und zurück
///   (budgeting-c6vn4, Q1, budgeting-m4v8t),
/// - der Monat gerade nicht geladen ist (budgeting-r3wq8): ein schon laufendes Lesen scheiterte, die Pause
///   fand keinen Monat.
/// Ein laufender Wechsel zu ihm (`monthChanging`) braucht keine eigene Bedingung (budgeting-m4v8t): beim
/// Start zeigt die Seite den Monat des Aufrufs, und jeder Weg zu einem Wechsel (`select`,
/// `applyFrameRefreshed`, `applyFrameLoaded`, `beginLoad`) hebt die Generation. Jede Bedingung hat einen Test
/// in `TargetPauseClientTests.fs`, der sie gestrichen rot macht. `startGeneration` ist die Generation beim
/// Start des Aufrufs, vor dem Einnehmen der Antwort gelesen (`pauseStartGeneration`).
let pauseNeedsReread (month: string) (startGeneration: int option) (model: Model) : bool =
    let generationMoved = startGeneration |> Option.exists (fun g -> g <> model.MonthGeneration)
    let notLoaded =
        match model.Month with
        | Success _ -> false
        | _ -> true
    shownMonth model = Some month
    && (model.Freshness = MonthStale || generationMoved || notLoaded)

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
