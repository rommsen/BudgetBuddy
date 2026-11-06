/// Die Seite Targets prüfen `#/budget/target-check` (budgeting-q4v8n, ADR 0082): reine Übergänge. Die Regel
/// rechnet `targetCheckGroups` in `Shared.Budgeting`, hier liegen Laden, Aufklappen, Rückkehr, "übernehmen"
/// und das Panel "Anpassen …". Geschrieben wird über denselben Aufruf wie im Tab Target (`TargetCall`), mit
/// der Herkunft der Zeile oder des Panels. Alles rein, die View ruft es nur auf.
module Components.Budget.TargetCheck

open Shared.Domain
open Shared.Budgeting
open Types
open Client.DesignSystem
open Components.Budget.TargetFormCore
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.TargetEdit

let private withCheck (model: Model) (change: TargetCheckPage -> TargetCheckPage) : Model =
    { model with TargetCheck = change model.TargetCheck }

// ============================================
// Laden, Aufklappen, Rückkehr
// ============================================

/// Der Knopf "Targets prüfen": die Budget-Seite merkt sich die Rückkehr.
let openTargetCheck (model: Model) : Model =
    withCheck model (fun c -> { c with ReturnPending = true })

/// Die Seite wird betreten: die Rohdaten kommen neu, "Passt" ist zu, das Panel zu.
let enterTargetCheck (model: Model) : Model =
    withCheck model (fun c ->
        { c with Data = Loading; FitsOpen = false; Panel = None; Generation = c.Generation + 1; LoadPending = true })

/// "Wiederholen": nur neu laden, das Aufklappen bleibt.
let retryTargetCheck (model: Model) : Model =
    withCheck model (fun c -> { c with Data = Loading; Generation = c.Generation + 1; LoadPending = true })

/// Das stille Neuladen nach einem abgebrochenen Aufruf: die Liste bleibt stehen, kein `Loading`.
let reloadTargetCheck (model: Model) : Model =
    withCheck model (fun c -> { c with Generation = c.Generation + 1; LoadPending = true })

/// Wie die Prüfung neu liest: mit `Loading`, solange die Seite noch lädt (Antwort `TargetCheckLoaded`), sonst
/// still (Antwort `TargetCheckReloaded`).
type CheckRead =
    | CheckLoad
    | CheckQuietReload

/// Liest die Prüfung neu, passend zu ihrem Stand: lädt sie noch, bleibt `Loading` und die Antwort ist eine
/// Lade-Antwort, sonst still. Die neue Generation verwirft die Antwort, die noch unterwegs ist.
let private readCheckAgain (model: Model) : Model * CheckRead =
    match model.TargetCheck.Data with
    | NotAsked
    | Loading -> retryTargetCheck model, CheckLoad
    | _ -> reloadTargetCheck model, CheckQuietReload

/// Neue Rohdaten: ein Panel eines anderen Monats schließt, es stünde sonst unsichtbar offen (`panelRow`).
let private settleLoaded (data: TargetCheckData) (c: TargetCheckPage) : TargetCheckPage =
    { c with
        Data = Success data
        LoadPending = false
        Panel = c.Panel |> Option.filter (fun form -> form.Month = data.Month) }

/// Eine Antwort mit fremder Generation ist überholt: ein späteres Betreten oder Wiederholen lädt schon.
let applyTargetCheckLoaded (generation: int) (result: Result<TargetCheckData, string>) (model: Model) : Model =
    withCheck model (fun c ->
        if generation <> c.Generation then c
        else
            match result with
            | Ok data -> settleLoaded data c
            | Error message -> { c with Data = Failure message; LoadPending = false })

/// Die Antwort des stillen Neuladens. Ein Fehler lässt die Liste stehen und meldet per Warn-Toast, dass sie
/// veraltet sein kann (wie `Month.applyReloaded`); lud die Seite noch, wird er zur Fehlerkarte, sonst bliebe sie
/// beim Skelett stehen.
let applyTargetCheckReloaded
    (generation: int)
    (result: Result<TargetCheckData, string>)
    (model: Model)
    : Model * ExternalMsg =
    let c = model.TargetCheck
    if generation <> c.Generation then model, NoOp
    else
        match result, c.Data with
        | Ok data, _ -> withCheck model (settleLoaded data), NoOp
        | Error message, (NotAsked | Loading) ->
            withCheck model (fun c -> { c with Data = Failure message; LoadPending = false }), NoOp
        | Error message, Success _ ->
            withCheck model (fun c -> { c with LoadPending = false }),
            ShowToast ($"Prüfung nicht neu gelesen, sie kann veraltet sein: {message}", ToastWarning)
        | Error _, Failure _ -> withCheck model (fun c -> { c with LoadPending = false }), NoOp

/// Ein Tipp auf "Passt" klappt auf, ein zweiter zu.
let toggleFits (model: Model) : Model =
    withCheck model (fun c -> { c with FitsOpen = not c.FitsOpen })

/// Der Knopf Zurück: über den Knopf "Targets prüfen" geöffnet, liegt die Budget-Seite im Browser-Verlauf direkt
/// davor und Zurück geht einen Schritt zurück. Per Link geöffnet, führt es zur Budget-Seite (wie
/// `leaveGeneralHistory`).
let leaveTargetCheck (model: Model) : ExternalMsg =
    if model.TargetCheck.ReturnPending then StepBackToBudgetPage else ShowBudgetPage

/// Die Seite wurde woandershin verlassen: das nächste Betreten der Budget-Seite beginnt von vorn.
let abandonTargetCheck (model: Model) : Model =
    withCheck model (fun c -> { c with ReturnPending = false })

// ============================================
// Gruppen und Zeilen
// ============================================

/// Die Gruppen der Seite, in der Reihenfolge aus `targetCheckGroups`.
let checkGroups (data: TargetCheckData) : (CheckGroup * TargetCheckRow list) list =
    targetCheckGroups data.Today data

/// "Nichts zu prüfen.", wenn die vier ersten Gruppen leer sind.
let nothingToCheck (groups: (CheckGroup * TargetCheckRow list) list) : bool =
    groups |> List.forall (fun (group, _) -> group = Fits)

[<Literal>]
let NothingToCheckText = "Nichts zu prüfen."

/// Die eingeordnete Zeile einer Kategorie.
let private rowOf (data: TargetCheckData) (categoryId: System.Guid) : TargetCheckRow option =
    data.Categories
    |> List.tryFind (fun c -> c.CategoryId = categoryId)
    |> Option.map (targetCheckRow data.Today)

/// Ein Betrag einer Zeile, getrennt in Wort davor, Betrag und Wort danach: der Betrag steht in Ziffern
/// untereinander (Mono), die Wörter in der Sans ("Target ", "216,67 €", "/Monat").
type FactAmount = { Before: string; Amount: string; After: string }

/// Die Werte einer Zeile ohne Zusatz.
type CheckRowValues = {
    /// "Target 216,67 €/Monat", ohne Target `None`.
    Monthly: FactAmount option
    /// Der gelesene Betrag mit Rhythmus wie in der Budget-Liste, nur bei anderem Rhythmus als monatlich.
    Read: FactAmount option
    /// "Ø 260,00 €".
    Average: FactAmount option
    /// "8 von 12 Monaten darüber", ohne Target und bei "nie" "1 von 12 Monaten mit Ausgaben".
    Count: string
}

/// Was eine Zeile zeigt: bei "Passt" mit Zusatz nur den Zusatz, ohne Werte und Balken, sonst die Werte mit Balken.
type CheckRowFacts =
    | NoteOnly of string
    | Values of CheckRowValues

let private monthsWord (n: int) : string = if n = 1 then "Monat" else "Monaten"

/// Die Werte einer Zeile (budgeting-q4v8n).
let checkRowFacts (row: TargetCheckRow) : CheckRowFacts =
    match row.Note with
    | Some note -> NoteOnly (fitsNoteText note)
    | None ->
        let n = List.length row.CountedMonths
        let count =
            match row.Monthly, row.Group with
            | Some _, group when group <> NeverSpends -> $"{row.MonthsOver} von {n} {monthsWord n} darüber"
            | _ -> $"{row.MonthsWithSpending} von {n} {monthsWord n} mit Ausgaben"
        Values
            { Monthly = row.Monthly |> Option.map (fun m -> { Before = "Target "; Amount = moneyText m; After = "/Monat" })
              Read =
                row.Target
                |> Option.filter (fun t -> t.Rhythm <> Months 1)
                |> Option.map (fun t -> { Before = ""; Amount = moneyText t.Goal; After = rhythmText t.Rhythm })
              Average = row.Average |> Option.map (fun a -> { Before = "Ø "; Amount = moneyText a; After = "" })
              Count = count }

/// Die Balken der gezählten Monate, alle vorbei.
let checkRowBars (data: TargetCheckData) (row: TargetCheckRow) : SpendingBar list =
    spendingBars data.Month row.CountedMonths

// ============================================
// Übernehmen
// ============================================

/// Der Auftrag von "<Betrag> übernehmen" (ADR 0082 Punkt 10): nur bei "mehr", "weniger" und "Kein Target" mit
/// Vorschlag. `Shown` ist das gelesene Target, `Wanted` der Entwurf des Vorschlags, der Monat der der Prüfung.
/// Die Befehls-Id setzt der Aufruf (`Retry.command`).
let takeRequest (data: TargetCheckData) (row: TargetCheckRow) : (string * TargetChangeRequest) option =
    match row.Group, row.Suggestion with
    | (SpendsMore | SpendsLess | WithoutTarget), Some suggestion ->
        Some (
            data.Month,
            { CategoryId = row.CategoryId
              Shown = row.Target
              Wanted = Some suggestion.Draft
              CommandId = None }
        )
    | _ -> None

/// Die Beschriftung von "übernehmen": der Betrag im Rhythmus mit Rhythmus wie in der Liste ("50,00 €/Woche
/// übernehmen"), ohne Target monatlich.
let takeText (row: TargetCheckRow) : string option =
    row.Suggestion
    |> Option.map (fun s ->
        let rhythm = row.Target |> Option.map (fun t -> t.Rhythm) |> Option.defaultValue (Months 1)
        $"{moneyText s.InRhythm}{rhythmText rhythm} übernehmen")

/// Läuft ein Target-Aufruf, oder eine Pause derselben Kategorie? Dann sendet "übernehmen" nichts.
let private takeBlocked (model: Model) (categoryId: System.Guid) : bool =
    match model.TargetCall with
    | TargetSaving _ -> true
    | TargetIdle -> Pause.pauseCallFor model categoryId

/// Der Knopf "übernehmen" einer Zeile, `None` ohne Knopf. Er dreht nur beim eigenen Aufruf, sonst ist er
/// gesperrt, solange ein Target-Aufruf oder eine Pause derselben Kategorie läuft.
let takeButton (model: Model) (data: TargetCheckData) (row: TargetCheckRow) : TargetButtonState option =
    takeRequest data row
    |> Option.map (fun _ ->
        let busy = (targetActionFrom FromCheckRow model row.CategoryId data.Month).IsSome
        { Busy = busy; Disabled = not busy && takeBlocked model row.CategoryId })

/// "<Betrag> übernehmen": startet den Aufruf, wenn die Zeile einen Vorschlag hat und nichts sperrt.
let startTake (categoryId: System.Guid) (model: Model) : Model * (string * TargetChangeRequest) option =
    match model.TargetCheck.Data with
    | Success data when not (takeBlocked model categoryId) ->
        match rowOf data categoryId |> Option.bind (takeRequest data) with
        | Some (month, request) ->
            { model with TargetCall = TargetSaving (categoryId, month, SavingTarget, FromCheckRow) }, Some (month, request)
        | None -> model, None
    | _ -> model, None

// ============================================
// Anpassen … (Panel)
// ============================================

/// Das Formular des Panels für eine Zeile: mit Vorschlag vorbelegt (Betrag im Rhythmus, Rhythmus und Datum wie
/// gelesen, ohne Target monatlich), sonst mit dem gelesenen Target ohne Eingabe.
let panelForm (data: TargetCheckData) (row: TargetCheckRow) : TargetForm =
    match row.Suggestion with
    | Some s ->
        let frequency = if row.Target.IsNone then Some Monthly else None
        prefilledTargetForm row.CategoryId data.Month s.InRhythm.Amount frequency
    | None -> emptyTargetForm row.CategoryId data.Month

/// "Anpassen …": öffnet das Panel für die Zeile. Eine offene Eingabe einer anderen Zeile ist verworfen.
let openPanel (categoryId: System.Guid) (model: Model) : Model =
    match model.TargetCheck.Data with
    | Success data ->
        match rowOf data categoryId with
        | Some row -> withCheck model (fun c -> { c with Panel = Some (panelForm data row) })
        | None -> model
    | _ -> model

/// "Abbrechen": zu, ohne Senden.
let closePanel (model: Model) : Model =
    withCheck model (fun c -> { c with Panel = None })

/// Die Zeile und das Formular des offenen Panels. Das gezeigte Target ist das Target der Zeile.
let panelRow (model: Model) : (TargetCheckRow * TargetForm) option =
    match model.TargetCheck.Data, model.TargetCheck.Panel with
    | Success data, Some form when form.Month = data.Month ->
        rowOf data form.CategoryId |> Option.map (fun row -> row, form)
    | _ -> None

/// Die Sperrlage des Panels: `targetFormLocksFor` mit dem Monat des Formulars und der Herkunft Panel.
let panelLocks (model: Model) (form: TargetForm) : TargetFormLocks =
    targetFormLocksFor model form.CategoryId form.Month FromCheckPanel

let private changePanel (change: Target option -> TargetFormLocks -> TargetForm -> TargetForm) (model: Model) : Model =
    match panelRow model with
    | Some (row, form) -> withCheck model (fun c -> { c with Panel = Some (change row.Target (panelLocks model form) form) })
    | None -> model

let typeCheckAmount (text: string) (model: Model) : Model =
    changePanel (fun _ -> formTypeAmount text) model

let typeCheckDate (entry: DateField.DateEntry) (model: Model) : Model =
    changePanel (fun _ -> formTypeDate entry) model

let chooseCheckRhythm (frequency: TargetFrequency) (model: Model) : Model =
    changePanel (formChooseRhythm frequency) model

let chooseCheckWeekday (day: int) (model: Model) : Model =
    changePanel (fun _ -> formChooseWeekday day) model

let cancelCheckChange (model: Model) : Model =
    changePanel (fun _ _ -> formCancelChange) model

let askRemoveCheck (model: Model) : Model =
    changePanel formAskRemove model

let cancelRemoveCheck (model: Model) : Model =
    changePanel (fun _ _ -> formCancelRemove) model

/// Ein Übergang des Kerns im Panel, der einen Aufruf auslösen kann. Startet er einen, läuft er für Kategorie
/// und Monat des Formulars mit der Herkunft Panel.
let private startPanel
    (step: Target option -> TargetFormLocks -> TargetForm -> TargetForm * TargetFormSend option)
    (model: Model)
    : Model * (string * TargetChangeRequest) option =
    match panelRow model with
    | Some (row, form) ->
        match step row.Target (panelLocks model form) form with
        | form', Some send ->
            { model with
                TargetCall = TargetSaving (form.CategoryId, form.Month, send.Action, FromCheckPanel)
                TargetCheck = { model.TargetCheck with Panel = Some form' } },
            Some (form.Month, send.Request)
        | form', None -> withCheck model (fun c -> { c with Panel = Some form' }), None
    | None -> model, None

/// Speichern im Panel (`formPlanSave`).
let startCheckSave (model: Model) = startPanel formPlanSave model

/// Die Warnung vor dem Wechsel der Art bestätigt (`formConfirmChange`).
let confirmCheckChange (model: Model) = startPanel formConfirmChange model

/// Entfernen bestätigt (`formConfirmRemove`).
let startCheckRemove (model: Model) = startPanel formConfirmRemove model

// ============================================
// Antworten
// ============================================

/// Nimmt das Panel die Antwort ein? Nur, wenn der Aufruf aus dem Panel kam und es noch für Kategorie und Monat
/// offen ist.
let panelOwnsAnswer (origin: TargetOrigin option) (categoryId: System.Guid) (month: string) (model: Model) : bool =
    origin = Some FromCheckPanel
    && model.TargetCheck.Panel |> Option.exists (fun f -> f.CategoryId = categoryId && f.Month = month)

/// Steht das Formular des Aufrufs noch offen und zeigt die Antwort selbst? Das Panel (`panelOwnsAnswer`) oder der
/// Tab Target, wenn der Aufruf von dort kam (`targetAnswerInTab`). Dann gibt es keinen Toast.
let answerFormOpen (origin: TargetOrigin option) (categoryId: System.Guid) (month: string) (model: Model) : bool =
    panelOwnsAnswer origin categoryId month model
    || (origin = Some FromTargetTab && targetAnswerInTab categoryId month model)

/// Setzt das Target der Kategorie in den Rohdaten, wenn sie zum Monat des Aufrufs gehören. `written`: mit
/// heute als letzter Änderung (`withWrittenTarget`), sonst nur BBs Stand.
let private setCheckTarget (categoryId: System.Guid) (month: string) (target: Target option) (written: bool) (model: Model) : Model =
    match model.TargetCheck.Data with
    | Success data when data.Month = month ->
        let data' =
            if written then withWrittenTarget categoryId target data
            else
                { data with
                    Categories =
                        data.Categories
                        |> List.map (fun c -> if c.CategoryId = categoryId then { c with Target = target } else c) }
        withCheck model (fun c -> { c with Data = Success data' })
    | _ -> model

let private onPanel (change: TargetForm -> TargetForm option) (model: Model) : Model =
    withCheck model (fun c -> { c with Panel = c.Panel |> Option.bind change })

/// Nimmt die Antwort auf der Seite ein, egal woher der Aufruf kam: das Target in den Rohdaten. Ins Panel kommt
/// sie nur bei `panelOwnsAnswer`: festgehalten schließt es, inzwischen geändert und abgelehnt bleibt es offen
/// mit Hinweis und Eingabe (`formTakeAnswer`). Läuft noch ein Laden, kann seine Antwort älter sein als diese:
/// dann liest die Prüfung neu (`Some`), und die Antwort, die noch unterwegs ist, zählt nicht mehr.
let applyCheckAnswered
    (origin: TargetOrigin option)
    (categoryId: System.Guid)
    (month: string)
    (result: Result<TargetChangeResult, string>)
    (model: Model)
    : Model * CheckRead option =
    let owns = panelOwnsAnswer origin categoryId month model
    let panel change = if owns then onPanel change else id
    let answered =
        match result with
        | Ok (TargetWritten target) -> model |> setCheckTarget categoryId month target true |> panel (fun _ -> None)
        | Ok (TargetChangedMeanwhile target) ->
            model |> setCheckTarget categoryId month target false |> panel (formTakeAnswer result >> Some)
        | Error _ -> model |> panel (formTakeAnswer result >> Some)
    // Gesetzt hätte die Antwort, wenn sie ein Target trägt und die Rohdaten zum Monat gehören oder noch kommen.
    let setsData =
        match result, answered.TargetCheck.Data with
        | Error _, _ -> false
        | Ok _, (NotAsked | Loading) -> true
        | Ok _, Success data -> data.Month = month
        | Ok _, Failure _ -> false
    if setsData && answered.TargetCheck.LoadPending then
        let again, read = readCheckAgain answered
        again, Some read
    else
        answered, None

/// Der Aufruf brach ab: der Ausgang ist offen, die Prüfung liest neu, wenn sie zum Monat des Aufrufs steht oder
/// noch lädt. Im eigenen Panel steht der Satz aus `targetCallLostText`, er verspricht das Neuladen nur, wenn es
/// läuft; die Eingabe bleibt.
let applyCheckCallLost
    (origin: TargetOrigin option)
    (categoryId: System.Guid)
    (month: string)
    (message: string)
    (model: Model)
    : Model * CheckRead option =
    let open' =
        match model.TargetCheck.Data with
        | Success data -> data.Month = month
        | Loading -> true
        | NotAsked
        | Failure _ -> false
    let reading, read =
        if open' then
            let again, how = readCheckAgain model
            again, Some how
        else
            model, None
    let owns = panelOwnsAnswer origin categoryId month model
    let noted =
        if owns then onPanel (formTakeCallLost (targetCallLostText read.IsSome message) >> Some) reading else reading
    noted, read

/// Der Toast zu einer Antwort auf "übernehmen": abgelehnt als Fehler, inzwischen geändert als Warnung mit
/// demselben Satz wie im Tab, festgehalten keiner.
let takeAnswerToast (result: Result<TargetChangeResult, string>) : ExternalMsg =
    match result with
    | Ok (TargetWritten _) -> NoOp
    | Ok (TargetChangedMeanwhile _) -> ShowToast (targetRejectedAwayText TargetChangedMeanwhileText, ToastWarning)
    | Error message -> ShowToast (targetRejectedAwayText message, ToastError)

let private fromCheckPage (origin: TargetOrigin option) : bool =
    origin = Some FromCheckRow || origin = Some FromCheckPanel

/// Der Toast zu einer Target-Antwort. `formOpen`: das Formular des Aufrufs (Tab oder Panel) steht noch offen
/// und zeigt die Antwort selbst, dann keiner. Aufrufe von der Seite (Zeile und Panel) melden wie "übernehmen"
/// (`takeAnswerToast`), alle anderen eine Ablehnung als Warnung.
let targetAnswerToast (origin: TargetOrigin option) (formOpen: bool) (result: Result<TargetChangeResult, string>) : ExternalMsg =
    if formOpen then NoOp
    elif fromCheckPage origin then takeAnswerToast result
    else
        match result with
        | Error message -> ShowToast (targetRejectedAwayText message, ToastWarning)
        | Ok (TargetChangedMeanwhile _) -> ShowToast (targetRejectedAwayText TargetChangedMeanwhileText, ToastWarning)
        | Ok (TargetWritten _) -> NoOp

/// Der Toast zu einem abgebrochenen Target-Aufruf, keiner bei offenem Formular. Der Satz verspricht das Neulesen
/// nur, wenn es läuft: von der Seite das Neuladen der Prüfung (`checkReads`), sonst auch das Neulesen des
/// Monats (`monthRereads`).
let targetCallLostToast
    (origin: TargetOrigin option)
    (formOpen: bool)
    (checkReads: bool)
    (monthRereads: bool)
    (message: string)
    : ExternalMsg =
    if formOpen then NoOp
    else
        let rereads = if fromCheckPage origin then checkReads else checkReads || monthRereads
        ShowToast (targetCallLostText rereads message, ToastError)
