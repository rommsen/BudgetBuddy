/// Der Kern des Target-Formulars (budgeting-r7k2m): Formular, Antwort-Hinweis und Sperrlage als Typen, dazu
/// alle Übergänge auf `TargetForm` mit dem gezeigten Target und der Sperrlage als Eingabe. Der Kern kennt das
/// Modell der Budget-Seite nicht. Der Tab Target bindet ihn in `TargetEdit` an `Model` und `Msg`, die Seite
/// Targets prüfen (ADR 0082 Punkt 11) an ihr eigenes Modell. Alles rein (ADR 0072, ADR 0080).
module Components.Budget.TargetFormCore

open Shared.Domain
open Shared.Budgeting
open Client.DesignSystem

/// Was über dem Formular steht, nachdem eine Antwort kam (budgeting-k8wq3).
type TargetNotice =
    /// YNAB stand anders als gezeigt: das Formular zeigt den frischen Stand, die Eingabe ist verworfen.
    | TargetChangedNotice
    /// Der Server oder YNAB lehnte ab, mit der konkreten Meldung. Die Eingabe bleibt stehen.
    | TargetRejectedNotice of message: string
    /// Schritt 1 geschrieben, das Datum nicht (budgeting-g2ty9, X4): ein gelber Hinweis, kein Fehler. Die
    /// Eingabe mit dem Datum bleibt, Speichern setzt es erneut.
    | TargetPartlyNotice of message: string

/// Das Formular (budgeting-k8wq3, ADR 0080). Hält Kategorie und Monat: es gilt nur, solange beide gezeigt
/// werden und der Monat der laufende ist (im Tab Target `syncTargetForm`). Ein Wechsel verwirft die Eingabe.
type TargetForm = {
    CategoryId: System.Guid
    Month: string
    /// Eingabe im Betragsfeld. `None`, solange nichts getippt wurde: dann gilt das geladene Target.
    AmountInput: string option
    /// Eingabe im Datumsfeld. `None`, solange nichts geändert wurde: dann gilt das Datum des Targets.
    DateInput: DateField.DateEntry option
    /// "Target entfernen" wurde getippt, die Bestätigung steht offen.
    ConfirmingRemove: bool
    Notice: TargetNotice option
    /// Der gewählte Rhythmus im `Segmented` (budgeting-xp46a). `None`, solange nichts gewählt wurde: dann gilt
    /// der gelesene Rhythmus, beim Anlegen monatlich.
    RhythmInput: TargetFrequency option
    /// Ein Wechsel der Art mit Verlusten wurde gespeichert, die Warnung steht offen (budgeting-xp46a).
    ConfirmingChange: bool
}

/// Was der laufende Aufruf des Formulars tut (budgeting-g2ty9, F1): der Spinner steht nur am Knopf, der
/// ihn ausgelöst hat.
type TargetAction =
    /// "Target speichern", "Target anlegen" oder "Wechseln" in der Warnung.
    | SavingTarget
    /// "Entfernen" in der Bestätigung von "Target entfernen".
    | RemovingTarget

/// Die Sperrlage des Formulars, die der Nutzer des Kerns aus seinem Modell bestimmt (budgeting-r7k2m).
type TargetFormLocks = {
    /// Wartet das Formular? Solange ein Aufruf läuft, der es sperrt: im Tab Target irgendein Target-Aufruf
    /// und eine Pause derselben Kategorie (budgeting-c2t9w, budgeting-f8w2n).
    Waits: bool
    /// Der laufende Aufruf dieses Formulars, wenn einer läuft: dann dreht sein Knopf (budgeting-g2ty9, F1)
    /// und Betrag, Datum und Rhythmus sind gesperrt (budgeting-r3wq8).
    Running: TargetAction option
    /// Der Satz, warum das Formular wartet (budgeting-f8w2n, budgeting-n7h3x), `None` ohne Satz.
    WaitHint: string option
}

/// Ein leeres Formular für Kategorie und Monat.
let emptyTargetForm (categoryId: System.Guid) (month: string) : TargetForm =
    { CategoryId = categoryId
      Month = month
      AmountInput = None
      DateInput = None
      ConfirmingRemove = false
      Notice = None
      RhythmInput = None
      ConfirmingChange = false }

/// Der Stand, auf dem eine Rechnung im Betragsfeld aufsetzt: der Betrag des Targets, ohne Target 0.
let private baseAmount (shown: Target option) : decimal =
    shown |> Option.map (fun t -> t.Goal.Amount) |> Option.defaultValue 0m

/// Der Text im Betragsfeld: die Eingabe oder der Betrag des Targets, ohne Target leer.
let targetAmountText (shown: Target option) (form: TargetForm) : string =
    match form.AmountInput, shown with
    | Some text, _ -> text
    | None, Some target -> AmountMath.formatSignedAmount target.Goal.Amount
    | None, None -> ""

/// Der Betrag der Eingabe: wie Zugewiesen eine Rechnung, der Stand ist das Target. `None`: ungültig,
/// negativ, oder ohne Target noch nichts getippt.
let targetAmountOf (shown: Target option) (form: TargetForm) : decimal option =
    match form.AmountInput with
    | None -> shown |> Option.map (fun t -> t.Goal.Amount)
    | Some text -> AmountMath.evaluateAmount (baseAmount shown) text |> Option.filter (fun v -> v >= 0m)

/// Das Datum des Felds als `DateTime`, für den Eingangswert von `DateField`.
let targetDateValue (shown: Target option) (form: TargetForm) : System.DateTime option =
    match form.DateInput with
    | Some (DateField.ValidDate d) -> Some d
    | Some DateField.EmptyDate
    | Some (DateField.InvalidDate _) -> None
    | None -> shown |> Option.bind (fun t -> t.Date) |> Option.bind (fun iso -> DateField.parseGermanDate (dateText iso))

/// Der Rhythmus, den BB über `goal_frequency` herstellen kann, im gelesenen Target: nur NEED mit Abstand 1.
let targetReadFrequency (target: Target) : TargetFrequency option =
    if target.Kind <> Need then None
    else [ Monthly; Weekly; Yearly ] |> List.tryFind (fun f -> rhythmOfFrequency f = target.Rhythm)

/// Der aktive Rhythmus im `Segmented`: die Wahl, sonst der gelesene, beim Anlegen monatlich. `None`: das
/// Target hat einen Rhythmus, den BB nicht herstellen kann, kein Segment ist aktiv.
let targetRhythmValue (shown: Target option) (form: TargetForm) : TargetFrequency option =
    match form.RhythmInput, shown with
    | Some f, _ -> Some f
    | None, Some target -> targetReadFrequency target
    | None, None -> Some Monthly

/// Steht das Datumsfeld im Formular (budgeting-d7r4k, ADR 0072 Nachtrag 2026-11-04)? Bei wöchentlich nicht:
/// dort zählt der Wochentag, ein Datum hat keine belegte Wirkung. Nur ein gelesenes Datum eines
/// wöchentlichen Targets bleibt sichtbar, damit es sich entfernen lässt.
let targetDateShown (shown: Target option) (form: TargetForm) : bool =
    match targetRhythmValue shown form with
    | Some Weekly -> shown |> Option.exists (fun t -> t.Date.IsSome && targetReadFrequency t = Some Weekly)
    | Some Monthly
    | Some Yearly
    | None -> true

/// Die Beschriftung des Datums: "Fällig am" bei monatlich und jährlich, dort ist das Datum eine Fälligkeit,
/// sonst "Datum" (budgeting-d7r4k).
let targetDateLabel (frequency: TargetFrequency option) : string =
    match frequency with
    | Some Monthly
    | Some Yearly -> "Fällig am"
    | Some Weekly
    | None -> "Datum"

/// Der Name des Datumsfelds für Screenreader (`aria-label`): "Fällig am" wie die Beschriftung, sonst wie
/// bisher "Datum des Targets".
let targetDateAriaLabel (frequency: TargetFrequency option) : string =
    match targetDateLabel frequency with
    | "Datum" -> "Datum des Targets"
    | label -> label

/// Die Beschriftung des Datums in der lesenden Anzeige, nach dem gelesenen Rhythmus.
let targetReadDateLabel (target: Target) : string = targetDateLabel (targetReadFrequency target)

/// Der Hinweis unter dem Betragsfeld, wenn die Eingabe nicht gilt (budgeting-r3wq8): negativ oder keine
/// gültige Rechnung, beim Anlegen auch 0 € wie an der API-Grenze (budgeting-g2ty9, X8). `None`, wenn sie
/// gilt.
let targetAmountHint (shown: Target option) (form: TargetForm) : string option =
    match form.AmountInput with
    | None -> None
    | Some text ->
        match AmountMath.evaluateAmount (baseAmount shown) text with
        | None -> Some AmountMath.InvalidCalculationText
        | Some value when value < 0m -> Some "Target darf nicht negativ sein"
        | Some 0m when shown.IsNone -> Some TargetCreateZeroText
        | Some _ -> None

/// Der Satz unter dem Datumsfeld bei monatlich mit Datum: YNAB verteilt den Betrag bis zur Fälligkeit, danach
/// läuft das Target weiter (budgeting-d7r4k, ADR 0072 Nachtrag 2026-11-04).
let targetDateHint (shown: Target option) (form: TargetForm) : string option =
    match targetRhythmValue shown form, targetDateValue shown form with
    | Some Monthly, Some _ -> Some "Danach läuft das Target jeden Monat weiter."
    | _ -> None

/// Ist die Eingabe im Datumsfeld ungültig?
let targetDateInvalid (form: TargetForm) : bool =
    match form.DateInput with
    | Some (DateField.InvalidDate _) -> true
    | _ -> false

/// Der Stand des Formulars gegen das geladene Target.
type TargetFormState =
    | FormInvalid
    | FormUnchanged
    | FormChanged of TargetDraft

/// Ein Entwurf, der wöchentlich anlegt oder auf wöchentlich wechselt, trägt kein Datum: sonst ginge
/// `goal_target_date` für ein wöchentliches Target hinaus (budgeting-d7r4k).
let private withoutWeeklyDate (draft: TargetDraft) : TargetDraft =
    match draft.NewRhythm with
    | SetRhythm Weekly -> { draft with NewDate = None }
    | SetRhythm _
    | KeepRhythm -> draft

/// Ohne Target ist das Formular erst geändert, wenn ein Betrag getippt ist; der Rhythmus ist dann immer
/// gesetzt (`SetRhythm`, Vorgabe monatlich). Mit Target ist ein gewählter Rhythmus gleich dem gelesenen
/// keine Änderung (`targetKindChange`).
let targetFormState (shown: Target option) (form: TargetForm) : TargetFormState =
    let date =
        match form.DateInput with
        | None -> Some (shown |> Option.bind (fun t -> t.Date))
        | Some DateField.EmptyDate -> Some None
        | Some (DateField.ValidDate d) -> Some (DateField.germanToIso (DateField.formatGermanDate d))
        | Some (DateField.InvalidDate _) -> None
    match shown with
    | None ->
        match form.AmountInput, targetAmountOf None form, date with
        | None, _, Some _ -> FormUnchanged
        | Some _, Some amount, Some date when amount > 0m ->
            let rhythm = targetRhythmValue None form |> Option.defaultValue Monthly
            FormChanged (withoutWeeklyDate { NewGoal = { Amount = amount; Currency = TargetCurrency }; NewDate = date; NewRhythm = SetRhythm rhythm })
        | _ -> FormInvalid
    | Some target ->
        match targetAmountOf shown form, date with
        | Some amount, Some date ->
            let draft =
                { NewGoal = { target.Goal with Amount = amount }
                  NewDate = date
                  NewRhythm = match form.RhythmInput with Some f -> SetRhythm f | None -> KeepRhythm }
            let draft = if (targetKindChange target draft).IsNone then { draft with NewRhythm = KeepRhythm } else draft
            let draft = withoutWeeklyDate draft
            if amount = target.Goal.Amount && date = target.Date && draft.NewRhythm = KeepRhythm then FormUnchanged
            else FormChanged draft
        | _ -> FormInvalid

/// Steht im Formular eine Rückfrage offen, die Warnung vor dem Wechsel oder die Bestätigung von "Target
/// entfernen"? Dann gibt es keine zweite, und Speichern, Entfernen und Pausieren warten (budgeting-g2ty9, X1).
let confirmOpen (form: TargetForm) : bool =
    form.ConfirmingChange || form.ConfirmingRemove

/// Was ein Speichern jetzt am gelesenen Target verlöre (`targetChangeLosses`). Leer ohne Target und ohne
/// Wechsel der Art.
let targetFormLosses (shown: Target option) (form: TargetForm) : TargetLoss list =
    match shown, targetFormState shown form with
    | Some target, FormChanged draft -> targetChangeLosses target draft
    | _ -> []

/// Ginge Speichern, wenn kein Aufruf liefe? Abweichung vom geladenen Stand und keine offene Rückfrage.
let formSaveableApartFromCalls (shown: Target option) (form: TargetForm) : bool =
    if confirmOpen form then false
    else
        match targetFormState shown form with
        | FormChanged _ -> true
        | FormInvalid
        | FormUnchanged -> false

/// Geht "Target speichern" oder "Target anlegen"? Nur bei Abweichung vom geladenen Stand, wenn das Formular
/// nicht wartet und keine Rückfrage offen ist.
let formCanSave (shown: Target option) (locks: TargetFormLocks) (form: TargetForm) : bool =
    formSaveableApartFromCalls shown form && not locks.Waits

/// Sind Betrag, Datum und Rhythmus gesperrt? Solange der eigene Aufruf läuft: die Antwort setzt das Formular
/// zurück, eine nachgetippte Eingabe ginge sonst still verloren (budgeting-r3wq8).
let formFieldsLocked (locks: TargetFormLocks) : bool = locks.Running.IsSome

/// Eine Eingabe im Formular, außer während des eigenen Aufrufs.
let private typeIn (change: TargetForm -> TargetForm) (locks: TargetFormLocks) (form: TargetForm) : TargetForm =
    if formFieldsLocked locks then form else change form

/// Eingabe im Betragsfeld. Eine Meldung von vorher geht weg, eine offene Warnung schließt (sie galt der
/// alten Eingabe). Während des eigenen Aufrufs nichts.
let formTypeAmount (text: string) (locks: TargetFormLocks) (form: TargetForm) : TargetForm =
    typeIn (fun f -> { f with AmountInput = Some text; Notice = None; ConfirmingChange = false }) locks form

/// Eingabe im Datumsfeld, wie `formTypeAmount`.
let formTypeDate (entry: DateField.DateEntry) (locks: TargetFormLocks) (form: TargetForm) : TargetForm =
    typeIn (fun f -> { f with DateInput = Some entry; Notice = None; ConfirmingChange = false }) locks form

/// Rhythmus im `Segmented` gewählt, wie `formTypeAmount`. Verschwindet dabei das Datumsfeld
/// (`targetDateShown`, wöchentlich), fällt ein getipptes Datum weg (budgeting-d7r4k).
let formChooseRhythm
    (frequency: TargetFrequency)
    (shown: Target option)
    (locks: TargetFormLocks)
    (form: TargetForm)
    : TargetForm =
    typeIn
        (fun f ->
            let chosen = { f with RhythmInput = Some frequency; Notice = None; ConfirmingChange = false }
            if targetDateShown shown chosen then chosen else { chosen with DateInput = None })
        locks
        form

/// Ein Aufruf, den ein Übergang auslöst: was er tut (dafür dreht der Knopf) und die Anfrage. Der Monat ist
/// der des Formulars.
type TargetFormSend = { Action: TargetAction; Request: TargetChangeRequest }

/// Startet einen Aufruf, wenn das Formular nicht wartet und `wanted` einen Wunsch liefert. Rückfragen und
/// Meldung schließen dabei.
let private start
    (action: TargetAction)
    (wanted: Target option -> TargetForm -> TargetDraft option option)
    (shown: Target option)
    (locks: TargetFormLocks)
    (form: TargetForm)
    : TargetForm * TargetFormSend option =
    if locks.Waits then form, None
    else
        match wanted shown form with
        | Some draft ->
            let request : TargetChangeRequest = { CategoryId = form.CategoryId; Shown = shown; Wanted = draft }
            { form with ConfirmingRemove = false; ConfirmingChange = false; Notice = None },
            Some { Action = action; Request = request }
        | None -> form, None

/// "Target speichern" oder "Target anlegen": ein Aufruf mit dem geladenen Target als `Shown`, nur bei
/// Abweichung. Verlöre der Wechsel der Art etwas, öffnet erst die Warnung, gesendet wird nichts.
let formPlanSave (shown: Target option) (locks: TargetFormLocks) (form: TargetForm) : TargetForm * TargetFormSend option =
    if not locks.Waits && not (confirmOpen form) && not (List.isEmpty (targetFormLosses shown form)) then
        { form with ConfirmingChange = true; Notice = None }, None
    else
        start
            SavingTarget
            (fun shown form ->
                if confirmOpen form then None
                else
                    match targetFormState shown form with
                    | FormChanged draft -> Some (Some draft)
                    | FormInvalid
                    | FormUnchanged -> None)
            shown
            locks
            form

/// Die Warnung bestätigt: der Aufruf mit dem Entwurf, nur solange die Warnung offen ist.
let formConfirmChange (shown: Target option) (locks: TargetFormLocks) (form: TargetForm) : TargetForm * TargetFormSend option =
    start
        SavingTarget
        (fun shown form ->
            match form.ConfirmingChange, targetFormState shown form with
            | true, FormChanged draft -> Some (Some draft)
            | _ -> None)
        shown
        locks
        form

/// Die Warnung abgebrochen: geschlossen, die Eingabe bleibt.
let formCancelChange (form: TargetForm) : TargetForm = { form with ConfirmingChange = false }

/// Ist "Target entfernen" gesperrt? Solange das Formular wartet und solange eine Rückfrage offen ist
/// (budgeting-r3wq8): am PC liegt die Bestätigung als Modal darüber, am Handy steht die Warnung vor dem
/// Wechsel an der Stelle des Speichern-Knopfs (budgeting-g2ty9, X1).
let formRemoveLocked (locks: TargetFormLocks) (form: TargetForm) : bool =
    locks.Waits || confirmOpen form

/// "Target entfernen": die Bestätigung öffnen. Nur mit Target, nie während das Formular wartet und nie neben
/// einer offenen Warnung (budgeting-g2ty9, X1).
let formAskRemove (shown: Target option) (locks: TargetFormLocks) (form: TargetForm) : TargetForm =
    if shown.IsSome && not (formRemoveLocked locks form) then { form with ConfirmingRemove = true } else form

let formCancelRemove (form: TargetForm) : TargetForm = { form with ConfirmingRemove = false }

/// Entfernen bestätigt: ein Aufruf ohne Wunsch.
let formConfirmRemove (shown: Target option) (locks: TargetFormLocks) (form: TargetForm) : TargetForm * TargetFormSend option =
    start RemovingTarget (fun shown form -> if form.ConfirmingRemove && shown.IsSome then Some None else None) shown locks form

/// Nimmt die Antwort auf den Aufruf dieses Formulars ein. Geschrieben: das Formular ist leer, bei einem
/// anderen Stand in YNAB mit Hinweis. Zum Teil geschrieben: die Eingabe mit dem Datum bleibt, ein erneutes
/// Speichern plant dann nur das Datum (ADR 0080 Punkt 6). Abgelehnt: die Eingabe bleibt, mit der Meldung.
let formTakeAnswer (result: Result<TargetChangeResult, string>) (form: TargetForm) : TargetForm =
    match result with
    | Ok (TargetWritten _) -> emptyTargetForm form.CategoryId form.Month
    | Ok (TargetChangedInYnab _) -> { emptyTargetForm form.CategoryId form.Month with Notice = Some TargetChangedNotice }
    | Ok (TargetPartlyWritten (_, message)) ->
        // Den ganzen Satz baut der Server (`targetPartlyText`), der Client zeigt ihn unverändert (budgeting-c2t9w).
        { form with Notice = Some (TargetPartlyNotice message); ConfirmingChange = false }
    | Error message -> { form with Notice = Some (TargetRejectedNotice message) }

/// Wie ein Knopf im Formular aussieht: `Busy` dreht und behält den Fokus (`Button.IsBusy`), `Disabled`
/// sperrt (budgeting-g2ty9, F1 und F2).
type TargetButtonState = { Busy: bool; Disabled: bool }

/// "Target speichern" oder "Target anlegen": dreht nur, wenn sein eigener Aufruf läuft (auch nach
/// "Wechseln"), sonst gesperrt, wenn Speichern nicht geht.
let formSaveButton (shown: Target option) (locks: TargetFormLocks) (form: TargetForm) : TargetButtonState =
    let busy = locks.Running = Some SavingTarget
    { Busy = busy; Disabled = not busy && not (formCanSave shown locks form) }

/// "Target entfernen": dreht nur, wenn das Entfernen läuft, sonst gesperrt nach `formRemoveLocked`.
let formRemoveButton (locks: TargetFormLocks) (form: TargetForm) : TargetButtonState =
    let busy = locks.Running = Some RemovingTarget
    { Busy = busy; Disabled = not busy && formRemoveLocked locks form }

/// Steht unter "Target speichern" oder "Target anlegen", warum es wartet? Nur, wenn ohne die laufenden
/// Aufrufe gespeichert werden könnte (`formSaveableApartFromCalls`).
let formSaveWaitHint (shown: Target option) (locks: TargetFormLocks) (form: TargetForm) : string option =
    if formSaveableApartFromCalls shown form then locks.WaitHint else None

/// Steht unter "Target entfernen", warum es wartet? Eine offene Rückfrage gibt es neben einem laufenden
/// Aufruf nicht (`formAskRemove`, im Tab Target auch `startPause`).
let formRemoveWaitHint (locks: TargetFormLocks) : string option = locks.WaitHint

/// Der Satz unter "Target speichern" und "Target entfernen", solange eine Pause derselben Kategorie sie
/// sperrt (budgeting-f8w2n).
[<Literal>]
let TargetWaitsForPauseText = "Wartet, bis die Pause gespeichert ist."

/// Der Satz unter "Target speichern" und "Target entfernen", solange ein Target-Aufruf einer anderen
/// Kategorie läuft (budgeting-n7h3x). Ohne Namen, wenn die Kategorie nicht im gezeigten Monat steht.
let targetWaitsForTargetText (categoryName: string option) : string =
    match categoryName with
    | Some name -> $"Wartet, bis das Target bei {name} gespeichert ist."
    | None -> "Wartet, bis das laufende Target gespeichert ist."

/// Der Hinweis über dem Formular oder unter dem Speichern-Knopf.
let targetNoticeText (notice: TargetNotice) : string =
    match notice with
    | TargetChangedNotice ->
        "Target wurde inzwischen in YNAB geändert. Hier steht jetzt der Stand aus YNAB, deine Eingabe ist verworfen."
    | TargetRejectedNotice message -> message
    | TargetPartlyNotice message -> message

/// Ein Satz je Verlust für die Warnung vor dem Wechsel der Art.
let targetLossText (shown: Target) (loss: TargetLoss) : string =
    match loss with
    | LosesDay -> "Der Tag des Targets geht verloren."
    | LosesDate ->
        let date = shown.Date |> Option.map dateText |> Option.defaultValue ""
        $"Das Datum {date} geht verloren."
    | LosesKind -> "Die bisherige Art des Targets geht verloren, BB legt ein Target „Set aside another“ an."
    | LosesRhythm ->
        match shown.Rhythm with
        | NoRhythm -> "Ein Target ohne Rhythmus kann BB nicht wiederherstellen, der Wechsel ist endgültig."
        | rhythm ->
            $"Der Rhythmus „{rhythmWordText rhythm}“ geht verloren. BB kann ihn nicht wiederherstellen, der Wechsel ist endgültig."

/// Der Text der Warnung: die Sätze zu den Verlusten, beim Wechsel auf wöchentlich der Hinweis zum Wochentag.
let targetChangeWarningText (shown: Target) (form: TargetForm) : string =
    let losses = targetFormLosses (Some shown) form |> List.map (targetLossText shown)
    let weekday =
        match targetRhythmValue (Some shown) form with
        | Some Weekly -> [ "Ein Wochentag lässt sich danach in BB nicht setzen." ]
        | _ -> []
    losses @ weekday |> String.concat " "

/// Der Titel der Warnung: "Art wechseln?", wenn die bisherige Art verloren geht, sonst "Rhythmus wechseln?"
/// (budgeting-g2ty9, X5).
let targetChangeTitle (shown: Target) (form: TargetForm) : string =
    if List.contains LosesKind (targetFormLosses (Some shown) form) then "Art wechseln?" else "Rhythmus wechseln?"

/// "Auffüllen bis" (F6), wenn YNAB `goal_needs_whole_amount = false` liefert.
let refillText (target: Target) : string option =
    match target.NeedsWholeAmount with
    | Some false -> Some "Auffüllen bis"
    | Some true
    | None -> None
