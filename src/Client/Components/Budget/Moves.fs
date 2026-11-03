/// Der Tab Geld als reine Übergänge: Zuweisen (budgeting-c5w2n, ADR 0078), der Fehlbetrag-Knopf,
/// Holen und Abgeben (budgeting-afd8h, ADR 0069) und die Offene Verschiebung.
module Components.Budget.Moves

open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.AmountMath
open Components.Budget.Types
open Components.Budget.Month
open Client.DesignSystem

let private findCategory (month: BudgetMonth) (id: System.Guid) : BudgetCategory option =
    categoriesOf month |> List.tryFind (fun c -> c.Id = id)

// ============================================
// Zustand des Tabs
// ============================================

let private emptyTransfer : TransferForm =
    { Counterpart = None; AmountInput = None; PickerOpen = false; Submission = Editing }

/// Eine rote Kategorie startet auf Holen, jede andere auf Zuweisen.
let startMode (category: BudgetCategory) : MoneyMode =
    if category.Available.Amount < 0m then TakeMode else AssignMode

/// Hält den Tab beim Schlüssel aus Auswahl und geladenem Monat. Läuft am Ende jeder Nachricht:
/// ein neuer Schlüssel beginnt frisch mit dem Startmodus. Während eines Monatswechsels bleibt er
/// stehen, gleicher Schlüssel ändert nichts, auch wenn ein Reload die Kategorie entfärbt.
let syncMoneyTab (model: Model) : Model =
    match model.Selected, model.Month with
    | None, _ -> { model with TabMoney = None }
    | Some _, _ when monthChanging model -> model
    | Some selected, Success month ->
        match model.TabMoney, findCategory month selected.CategoryId with
        | Some tab, _ when tab.CategoryId = selected.CategoryId && tab.Month = month.Month -> model
        | _, Some category ->
            { model with
                TabMoney =
                    Some
                        { CategoryId = category.Id
                          Month = month.Month
                          Mode = startMode category
                          Entry = None
                          Transfer = emptyTransfer } }
        | _, None -> { model with TabMoney = None }
    | Some _, _ -> model

/// Ging beim Abgleich eine Eingabe verloren? Beim Verlassen und beim Weiterrücken des Monats ist sie
/// schon gespeichert oder gemeldet, übrig bleibt der Fall, dass die Kategorie beim Neu-Lesen wegfiel.
let entryDropped (before: Model) (after: Model) : bool =
    let entry (model: Model) = model.TabMoney |> Option.bind (fun tab -> tab.Entry)
    (entry before).IsSome && (entry after).IsNone

/// Läuft gerade eine Bewegung, aus dem Tab, über "Erneut senden" oder über Targets anwenden?
let moveRunning (model: Model) : bool =
    model.MoveInFlight <> NoMoveCall
    || (model.OpenRetries |> List.exists (fun r -> r.State = RetryRunning))
    || (match model.Targets with
        | Applying _
        | ApplyDone (_, _, true, _) -> true
        | _ -> false)

/// Warum gerade keine Bewegung startet.
type MoveBlock =
    /// Eine Bewegung läuft.
    | CallRunning
    /// Der Bericht von Targets anwenden steht im Dialog: Der gezeigte Stand gilt erst beim Schließen wieder.
    | ReportOpen
    /// Zuweisungen sind unterwegs. Eine Zuweisung ist absolut, käme sie nach der Bewegung, nähme sie
    /// die Bewegung zurück (ADR 0078 Punkt 5).
    | AssignQueued
    /// `Month` zeigt noch den alten Monat, ein Auftrag ginge dorthin.
    | MonthChanging

/// Die eine Startregel für Bewegungen. Alle Knöpfe und `update` lesen sie.
type MoveGate =
    | Free
    /// Eine offene Eingabe wird zuerst gespeichert, die Bewegung startet erst beim nächsten Tipp.
    | SaveFirst
    | Blocked of MoveBlock

/// Der erste Grund zählt. Eine offene Eingabe wird auch unter einem offenen Bericht zuerst gespeichert,
/// nur vor dem Monatswechsel gibt sie nach: In einen wechselnden Monat geht nichts hinaus.
let moveGate (model: Model) : MoveGate =
    if moveRunning model then
        Blocked CallRunning
    elif not (List.isEmpty model.AssignQueue) then
        Blocked AssignQueued
    elif monthChanging model then
        Blocked MonthChanging
    elif model.TabMoney |> Option.exists (fun tab -> tab.Entry.IsSome) then
        SaveFirst
    else
        match model.Targets with
        | ApplyDone _
        | ApplyFailed _ -> Blocked ReportOpen
        | _ -> Free

/// Dürfen "Erneut senden" und der Dialog Targets starten? Sie schreiben nicht aus dem Tab: Weder der
/// Monatswechsel noch ihr eigener Bericht hält sie auf, eine offene Eingabe speichert der Tipp.
let canStartMove (model: Model) : bool =
    match moveGate model with
    | Blocked CallRunning
    | Blocked AssignQueued -> false
    | _ -> true

/// Darf der Tab einen Auftrag senden?
let canSend (model: Model) : bool =
    match moveGate model with
    | Free
    | SaveFirst -> true
    | Blocked _ -> false

/// Der Warn-Toast, wenn ein Tipp eine Bewegung nicht starten kann.
let moveBlockedText = "Eine Verschiebung läuft noch. Gleich noch einmal versuchen."

/// Bleibt der Inspektor stehen? Das Feld Zugewiesen und alle Wege hinaus warten dann.
let inspectorBusy (model: Model) : bool =
    match moveGate model with
    | Blocked CallRunning
    | Blocked ReportOpen -> true
    | _ -> false

let private withTab (change: MoneyTabState -> MoneyTabState) (model: Model) : Model =
    { model with TabMoney = model.TabMoney |> Option.map change }

/// Wechselt den Modus. Die Form von Holen und Abgeben bleibt dabei stehen.
let switchMode (mode: MoneyMode) (model: Model) : Model =
    withTab (fun tab -> { tab with Mode = mode; Transfer = { tab.Transfer with PickerOpen = false; Submission = Editing } }) model

let pickerOpen (model: Model) : bool =
    model.TabMoney |> Option.exists (fun tab -> tab.Transfer.PickerOpen)

/// Gibt es etwas, das ein Neuladen der App zerstörte? Die bloße Vorbelegung zählt nicht.
let inputActive (model: Model) : bool =
    let tabActive (tab: MoneyTabState) =
        tab.Entry.IsSome
        || tab.Transfer.AmountInput.IsSome
        || tab.Transfer.Counterpart.IsSome
    (model.TabMoney |> Option.exists tabActive)
    || inspectorBusy model
    || not (List.isEmpty model.AssignQueue)

// ============================================
// Feld Zugewiesen
// ============================================

/// Der Wert der Eingabe; `None`: keine gültige Rechnung.
let entryValue (entry: AssignEntry) : decimal option =
    match entry.Text with
    | Exact value -> Some value
    | Typed text -> evaluateAmount entry.BaseAssigned text

let private entryText (entry: AssignEntry) : string =
    match entry.Text with
    | Exact value -> formatSignedAmount value
    | Typed text -> text

/// Der Text im Feld: die Eingabe, sonst der Stand mit Vorzeichen.
let assignFieldText (category: BudgetCategory) (tab: MoneyTabState) : string =
    match tab.Entry with
    | Some entry -> entryText entry
    | None -> formatSignedAmount category.Assigned.Amount

/// Die Eingabe ist keine Rechnung: das Feld zeigt sie rot.
let assignFieldInvalid (tab: MoneyTabState) : bool =
    tab.Entry |> Option.exists (fun entry -> (entryValue entry).IsNone)

/// Was unter dem Feld steht.
type AssignPreview =
    | NoPreview
    | InvalidCalculation
    | AssignAfter of availableAfter: Money * assigned: Money * toBeBudgetedAfter: Money

let assignPreview (month: BudgetMonth) (category: BudgetCategory) (tab: MoneyTabState) : AssignPreview =
    match tab.Entry with
    | None -> NoPreview
    | Some entry ->
        match entryValue entry with
        | None -> InvalidCalculation
        | Some value when value = entry.BaseAssigned -> NoPreview
        | Some value ->
            let delta = value - entry.BaseAssigned
            AssignAfter (
                { category.Available with Amount = category.Available.Amount + delta },
                { category.Assigned with Amount = value },
                { month.ToBeBudgeted with Amount = month.ToBeBudgeted.Amount - delta }
            )

let private editEntry (text: BudgetCategory -> AssignEntry option -> AssignText option) (model: Model) : Model =
    match model.TabMoney, model.Month with
    | Some tab, Success month when not (monthChanging model) ->
        match findCategory month tab.CategoryId with
        | Some category ->
            match text category tab.Entry with
            | Some changed ->
                let entry =
                    tab.Entry
                    |> Option.defaultValue
                        { CategoryId = tab.CategoryId
                          Month = tab.Month
                          BaseAssigned = category.Assigned.Amount
                          Text = changed }
                { model with TabMoney = Some { tab with Entry = Some { entry with Text = changed } } }
            | None -> model
        | None -> model
    | _ -> model

/// Eingabe im Feld. Während eines Monatswechsels nimmt es keine an.
let typeAssign (text: string) (model: Model) : Model =
    editEntry (fun _ _ -> Some (Typed text)) model

/// Ein Schritt ändert nur die Eingabe. Auf einer ungültigen Rechnung tut er nichts.
let stepAssign (delta: decimal) (model: Model) : Model =
    editEntry
        (fun category entry ->
            match entry with
            | None -> Some (Exact (category.Assigned.Amount + delta))
            | Some current -> entryValue current |> Option.map (fun value -> Exact (value + delta)))
        model

/// Nimmt das Feld gerade keine Eingabe an? Während eines Monatswechsels und solange der Inspektor
/// stehen bleibt: Eine Zuweisung ist absolut und überschriebe die Bewegung (ADR 0078 Punkt 5). Eine
/// laufende Schlange sperrt das Feld nicht.
let assignFieldReadOnly (model: Model) : bool =
    monthChanging model || inspectorBusy model

/// Esc: das Feld zeigt wieder den Stand.
let discardEntry (model: Model) : Model =
    withTab (fun tab -> { tab with Entry = None }) model

/// Reiht eine Zuweisung ein. Eine wartende für dieselbe Kategorie und denselben Monat wird ersetzt,
/// die laufende nie.
let enqueueAssign (write: AssignWrite) (queue: AssignWrite list) : AssignWrite list =
    let sameTarget (w: AssignWrite) =
        w.State = WriteWaiting && w.CategoryId = write.CategoryId && w.Month = write.Month
    if queue |> List.exists sameTarget then
        queue |> List.map (fun w -> if sameTarget w then write else w)
    else
        queue @ [ write ]

/// `withAssigned` im Modell.
let private assignedInModel (categoryId: System.Guid) (assigned: Money) (model: Model) : Model =
    match model.Month with
    | Success month -> { model with Month = Success (withAssigned categoryId assigned month) }
    | _ -> model

/// Schreibt den gespeicherten Wert in den Monat: die Zahl steht sofort da. Die neue Generation
/// verwirft eine noch laufende, ältere Monatsantwort.
let private writeThrough (write: AssignWrite) (model: Model) : Model =
    match model.Month with
    | Success month when month.Month = write.Month && not (monthChanging model) ->
        if (findCategory month write.CategoryId).IsSome then
            requestMonth (assignedInModel write.CategoryId write.Assigned model)
        else
            model
    | _ -> model

/// Trägt die geschriebenen Zeilen eines Berichts ein. Bis das Neu-Lesen antwortet, rechnet das Feld
/// sonst vom alten Stand und seine absolute Zuweisung nähme die Verschiebung zurück (ADR 0078 Punkt 5).
let private withReport (report: ApplyReport) (model: Model) : Model =
    match model.Month with
    | Success month when month.Month = report.Month ->
        report.Lines
        |> List.filter (fun l -> l.Outcome = Written)
        |> List.fold (fun current l -> assignedInModel l.CategoryId l.NewAssigned current) model
    | _ -> model

/// Feld verlassen: gültig und geändert geht in die Schlange und steht im Monat, gleich fällt die
/// Eingabe weg, ungültig bleibt sie rot stehen.
let saveEntry (model: Model) : Model =
    match model.TabMoney |> Option.bind (fun tab -> tab.Entry) with
    | None -> model
    | Some entry ->
        match entryValue entry with
        | None -> model
        | Some value when value = entry.BaseAssigned -> discardEntry model
        | Some value ->
            let currency =
                match model.Month with
                | Success month -> month.ToBeBudgeted.Currency
                | _ -> "EUR"
            let write =
                { CategoryId = entry.CategoryId
                  Month = entry.Month
                  Assigned = { Amount = value; Currency = currency }
                  State = WriteWaiting }
            // Eine neuere Zuweisung ersetzt den Hinweis: Erneut senden nähme sie sonst zurück.
            let target = Some (entry.CategoryId, entry.Month)
            { discardEntry model with
                AssignQueue = enqueueAssign write model.AssignQueue
                OpenRetries = model.OpenRetries |> List.filter (fun r -> r.AssignTarget <> target) }
            |> writeThrough write

/// Was eine Taste in den Feldern des Tabs bewirkt. Die View nimmt nur noch den Fokus.
type FieldKeyAction =
    | KeepField
    /// Enter: die Nachricht, wenn es eine gibt, dann Fokus weg.
    | LeaveField of Msg option
    /// Esc: die Nachricht, dann Fokus weg. Die Taste geht nicht weiter nach oben.
    | CancelField of Msg

let assignKeyAction (key: string) : FieldKeyAction =
    match key with
    | "Enter" -> LeaveField (Some AssignFieldLeft)
    | "Escape" -> CancelField AssignDiscarded
    | _ -> KeepField

/// Gesendet wird allein über den Knopf.
let amountKeyAction (key: string) : FieldKeyAction =
    match key with
    | "Enter" -> LeaveField None
    | "Escape" -> CancelField TransferAmountReset
    | _ -> KeepField

/// Der Warn-Toast, wenn beim Verlassen eine ungültige Eingabe wegfällt.
let invalidEntryText = "Zugewiesen nicht gespeichert: keine gültige Rechnung"

/// Was aus der Eingabe wurde.
type LeaveOutcome =
    | NothingOpen
    | Unchanged
    | Queued
    | DroppedInvalid

/// Inspektor verlassen, vor der Änderung: wie `saveEntry`, nur fällt eine ungültige Eingabe weg. Holen
/// und Abgeben bleiben unberührt, ihre Form stirbt mit dem Schlüssel.
let leaveInspector (model: Model) : Model * LeaveOutcome =
    match model.TabMoney |> Option.bind (fun tab -> tab.Entry) with
    | None -> model, NothingOpen
    | Some entry ->
        match entryValue entry with
        | None -> discardEntry model, DroppedInvalid
        | Some value when value = entry.BaseAssigned -> discardEntry model, Unchanged
        | Some _ -> saveEntry model, Queued

/// Was das Verlassen meldet.
let leaveToast (outcome: LeaveOutcome) : ExternalMsg =
    match outcome with
    | DroppedInvalid -> ShowToast (invalidEntryText, ToastWarning)
    | NothingOpen
    | Unchanged
    | Queued -> NoOp

/// Startet den Kopf der Schlange, wenn keiner läuft. Gibt die Zuweisung für den Aufrufer zurück.
let startNextAssign (model: Model) : Model * AssignWrite option =
    match model.AssignQueue with
    | ({ State = WriteWaiting } as head) :: rest ->
        let running = { head with State = WriteRunning }
        { model with AssignQueue = running :: rest }, Some running
    | _ -> model, None

let private dropRunning (model: Model) : Model =
    { model with AssignQueue = model.AssignQueue |> List.filter (fun w -> w.State <> WriteRunning) }

// ============================================
// Berichte und Offene Verschiebung
// ============================================

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

/// Eine Zeile heißt: eine Seite ist Zu verteilen, es ging ums Zuweisen.
let private failureHeadline (report: ApplyReport) : string =
    if report.Lines.Length = 1 then "Nicht zugewiesen" else "Nicht vollständig verschoben"

let private failureReason (report: ApplyReport) : string option =
    report.Lines
    |> List.tryPick (fun l ->
        match l.Outcome with
        | Failed reason -> Some reason
        | _ -> None)

let private failureToast (report: ApplyReport) (reason: string) : ExternalMsg =
    ShowToastWithAction (
        $"{failureHeadline report}: {reason}",
        ToastError,
        { Label = retryMoveLabel; Kind = RetryBudgetMove report.ApplicationId }
    )

/// Der Toast zu einem Bericht: eine fehlgeschlagene Zeile bietet "Erneut senden" an.
let moveToast (report: ApplyReport) : ExternalMsg =
    match failureReason report with
    | Some reason -> failureToast report reason
    | None when report.Lines |> List.exists (fun l -> l.Outcome = ChangedInYnab) ->
        ShowToast ("In YNAB geändert, nichts überschrieben", ToastWarning)
    | None -> ShowToast (moveSummaryText report, ToastSuccess)

/// Zuweisen im Feld zeigt keinen Erfolgs-Toast: die Zahl in der Liste ist die Antwort. Ist der Bericht
/// nicht mehr offen, weil eine neuere Zuweisung wartet, gibt es nichts erneut zu senden.
let private assignToast (report: ApplyReport) (model: Model) : ExternalMsg =
    let stillOpen = model.OpenRetries |> List.exists (fun r -> r.ApplicationId = report.ApplicationId)
    match failureReason report with
    | Some reason when stillOpen -> failureToast report reason
    | Some reason -> ShowToast ($"{failureHeadline report}: {reason}", ToastError)
    | None -> NoOp

/// Kategorie und Monat einer Zuweisung: Eine Zeile heißt, es ging ums Zuweisen.
let private assignTarget (report: ApplyReport) : (System.Guid * string) option =
    match report.Lines with
    | [ line ] -> Some (line.CategoryId, report.Month)
    | _ -> None

/// Merkt sich einen Bericht mit fehlgeschlagener Zeile; ist nichts mehr offen, verschwindet er.
/// Der Hinweis auf der Seite hängt daran, der Toast läuft nach fünf Sekunden ab.
let noteMoveReport (month: string) (report: ApplyReport) (model: Model) : Model =
    let target = assignTarget report
    let others =
        model.OpenRetries
        |> List.filter (fun r ->
            r.ApplicationId <> report.ApplicationId && (target.IsNone || r.AssignTarget <> target))
    let superseded =
        model.AssignQueue |> List.exists (fun w -> Some (w.CategoryId, w.Month) = target)
    match failureReason report with
    | Some _ when superseded -> { model with OpenRetries = others }
    | Some reason ->
        let entry =
            { ApplicationId = report.ApplicationId
              Headline = failureHeadline report
              MonthLabel = monthLabel month
              Summary = moveSummaryText report
              Reason = reason
              State = RetryReady
              AssignTarget = target }
        { model with OpenRetries = others @ [ entry ] }
    | None -> { model with OpenRetries = others }

/// Antwort auf eine Zuweisung. Die Schlange läuft weiter. `true`: das war die letzte, der Monat wird
/// einmal neu gelesen, auch nach einem Fehler, damit der echte Stand dasteht.
let applyAssignAnswered (result: Result<ApplyReport, string>) (model: Model) : Model * ExternalMsg * bool =
    let rest = dropRunning model
    let noted, external =
        match result with
        | Ok report ->
            let noted = noteMoveReport report.Month report rest
            noted, assignToast report noted
        | Error message -> rest, ShowToast ($"Nicht zugewiesen: {message}", ToastError)
    if List.isEmpty noted.AssignQueue then awaitFreshMonth noted, external, true
    else noted, external, false

/// Der Aufruf brach ab. Der Wert lässt sich neu tippen, weil Zuweisen absolut ist.
let applyAssignCallLost (message: string) (model: Model) : Model * ExternalMsg * bool =
    let rest = dropRunning model
    let external = ShowToast ($"Ob zugewiesen wurde, ist unklar: {message}. Der Stand wird gleich neu gelesen.", ToastError)
    if List.isEmpty rest.AssignQueue then awaitFreshMonth rest, external, true
    else rest, external, false

// ============================================
// Fehlbetrag-Knopf
// ============================================

/// Was der Fehlbetrag-Knopf zeigt.
type MissingAction =
    | NothingMissing
    /// Der Knopf wirkt: von Zu verteilen an X.
    | AssignMissing of Money
    /// Zu verteilen reicht nicht: der Knopf nennt den Rest, ohne Wirkung.
    | OnlyLeft of Money

let missingAction (month: BudgetMonth) (category: BudgetCategory) : MissingAction =
    match missingAmount category with
    | None -> NothingMissing
    | Some missing when month.ToBeBudgeted.Amount >= missing.Amount -> AssignMissing missing
    | Some _ -> OnlyLeft { month.ToBeBudgeted with Amount = max 0m month.ToBeBudgeted.Amount }

/// "12,30 zuweisen" oder "Nur noch 60,20 zu verteilen".
let missingActionText (action: MissingAction) : string =
    let plain (m: Money) = Money.formatAmountText false false m.Amount m.Currency
    match action with
    | NothingMissing -> ""
    | AssignMissing amount -> $"{plain amount} zuweisen"
    | OnlyLeft rest -> $"Nur noch {plain rest} zu verteilen"

/// Darf der Fehlbetrag-Knopf senden? Sein Betrag steht im Monat. Solange der veraltet ist, buchte ein
/// zweiter Tipp denselben Fehlbetrag noch einmal.
let canCover (model: Model) : bool =
    canSend model && model.Freshness = MonthRead

/// Der Warn-Toast, wenn der Fehlbetrag-Knopf eine offene Eingabe verwirft.
let coverDroppedEntryText = "Eingabe verworfen: der Fehlbetrag wurde zugewiesen"

/// Verwirft der Knopf etwas, das geschrieben worden wäre? Eine ungültige Rechnung zählt mit.
let coverDropsEntry (model: Model) : bool =
    model.TabMoney
    |> Option.bind (fun tab -> tab.Entry)
    |> Option.exists (fun entry -> entryValue entry <> Some entry.BaseAssigned)

/// Der Knopf: eine offene Eingabe fällt weg, genau ein Auftrag geht hinaus. Die Sperre prüft der
/// Aufrufer (`canCover`).
let confirmMissing (model: Model) : Model * (string * MoveRequest) option =
    match model.TabMoney, model.Month with
    | Some tab, Success month ->
        match findCategory month tab.CategoryId |> Option.map (missingAction month) with
        | Some (AssignMissing amount) ->
            let request = { Source = ToBeBudgetedSide; Target = CategorySide tab.CategoryId; Amount = amount }
            { model with
                MoveInFlight = MoveCallRunning
                TabMoney = Some { tab with Entry = None; Transfer = { tab.Transfer with Submission = Editing } } },
            Some (month.Month, request)
        | _ -> model, None
    | _ -> model, None

// ============================================
// Holen und Abgeben
// ============================================

/// Die Vorbelegung: das fehlende Minus, nur in Holen. Liest den aktuellen Monat.
let private transferPrefill (month: BudgetMonth) (tab: MoneyTabState) : decimal option =
    match tab.Mode, findCategory month tab.CategoryId with
    | TakeMode, Some category when category.Available.Amount < 0m -> Some -category.Available.Amount
    | _ -> None

/// Der Text im Betragsfeld: die Eingabe, sonst die Vorbelegung.
let transferAmountText (month: BudgetMonth) (tab: MoneyTabState) : string =
    match tab.Transfer.AmountInput, transferPrefill month tab with
    | Some text, _ -> text
    | None, Some amount -> formatSignedAmount amount
    | None, None -> ""

/// Der Stand, zu dem das Betragsfeld rechnet: die Vorbelegung, sonst 0.
let private transferStand (month: BudgetMonth) (tab: MoneyTabState) : decimal =
    transferPrefill month tab |> Option.defaultValue 0m

/// Der Betrag. Das Feld rechnet. Nur über 0 ist einer.
let transferAmount (month: BudgetMonth) (tab: MoneyTabState) : decimal option =
    evaluateAmount (transferStand month tab) (transferAmountText month tab) |> Option.filter (fun amount -> amount > 0m)

/// Steht im Betragsfeld etwas, das keine Rechnung ist? Leer zählt nicht, 0 und Negatives sind Rechnungen.
let transferAmountInvalid (month: BudgetMonth) (tab: MoneyTabState) : bool =
    match tab.Transfer.AmountInput with
    | Some text when text.Trim () <> "" -> (evaluateAmount (transferStand month tab) text).IsNone
    | _ -> false

/// Name der Gegenseite.
let counterpartName (month: BudgetMonth) (side: MoveSide) : string =
    match side with
    | ToBeBudgetedSide -> CategoryPicker.toBeBudgetedLabel
    | CategorySide id -> findCategory month id |> Option.map (fun c -> c.Name) |> Option.defaultValue ""

/// Quelle und Ziel: Holen zieht von der Gegenseite nach X, Abgeben umgekehrt.
let private sides (tab: MoneyTabState) : MoveSide option * MoveSide option =
    let x = Some (CategorySide tab.CategoryId)
    match tab.Mode with
    | GiveMode -> x, tab.Transfer.Counterpart
    | AssignMode
    | TakeMode -> tab.Transfer.Counterpart, x

/// Eine Zeile `von` oder `nach` mit dem Stand danach.
type TransferEnd = { Label: string; After: Money }

let private standOf (month: BudgetMonth) (side: MoveSide) : Money option =
    match side with
    | ToBeBudgetedSide -> Some month.ToBeBudgeted
    | CategorySide id -> findCategory month id |> Option.map (fun c -> c.Available)

/// `von` und `nach` mit dem Stand danach. Ohne gültigen Betrag stehen die heutigen Stände, negativ
/// ist gültig.
let transferEnds (month: BudgetMonth) (tab: MoneyTabState) : TransferEnd option * TransferEnd option =
    let amount = transferAmount month tab |> Option.defaultValue 0m
    let endOf (delta: decimal) (side: MoveSide option) =
        side
        |> Option.bind (fun s ->
            standOf month s
            |> Option.map (fun stand ->
                { Label = counterpartName month s; After = { stand with Amount = stand.Amount + delta } }))
    let source, target = sides tab
    endOf -amount source, endOf amount target

/// Der Auftrag an den Server; `None`, solange etwas fehlt.
let transferRequest (month: BudgetMonth) (tab: MoneyTabState) : MoveRequest option =
    match sides tab, transferAmount month tab with
    | (Some source, Some target), Some amount ->
        Some { Source = source; Target = target; Amount = { month.ToBeBudgeted with Amount = amount } }
    | _ -> None

/// Darf der Knopf gedrückt werden?
let canTransfer (model: Model) (month: BudgetMonth) (tab: MoneyTabState) : bool =
    canSend model && (transferRequest month tab).IsSome

/// "20,00 € aus Kleidung holen", "20,00 € an Kleidung abgeben". Solange etwas fehlt, die bloße Handlung.
let transferButtonText (month: BudgetMonth) (tab: MoneyTabState) : string =
    let verb = if tab.Mode = GiveMode then "Abgeben" else "Holen"
    match transferAmount month tab, tab.Transfer.Counterpart with
    | Some amount, Some side ->
        let text = moneyText { month.ToBeBudgeted with Amount = amount }
        let name = counterpartName month side
        if tab.Mode = GiveMode then $"{text} an {name} abgeben" else $"{text} aus {name} holen"
    | _ -> verb

/// "Kleidung hat 30,00 € frei", wenn der Betrag über dem freien Betrag der Quelle liegt. Keine Sperre.
/// Zu verteilen hat keinen freien Betrag.
let transferWarning (month: BudgetMonth) (tab: MoneyTabState) : string option =
    match fst (sides tab), transferAmount month tab with
    | Some (CategorySide id), Some amount ->
        findCategory month id
        |> Option.bind (fun source ->
            let free = freeAmount source
            if amount > free.Amount then
                Some $"{source.Name} hat {moneyText { free with Amount = max 0m free.Amount }} frei"
            else
                None)
    | _ -> None

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

/// "Zuletzt verwendet" für den Picker. Scheitert die Abfrage, fehlt der Abschnitt ohne Meldung.
let recentCounterparts (model: Model) : YnabCategoryId list =
    match model.RecentCounterparts with
    | Success ids -> ids |> List.map YnabCategoryId
    | _ -> []

/// Beim Öffnen des Pickers wird neu gelesen, eine vorhandene Liste bleibt bis zur Antwort stehen.
let requestRecentCounterparts (model: Model) : Model =
    match model.RecentCounterparts with
    | Success _ -> model
    | _ -> { model with RecentCounterparts = Loading }

let applyRecentCounterparts (result: Result<System.Guid list, string>) (model: Model) : Model =
    match result with
    | Ok ids -> { model with RecentCounterparts = Success ids }
    | Error message -> { model with RecentCounterparts = Failure message }

/// Aus der Wahl im Picker wird die Gegenseite. Ohne Kategorie und Konto gibt es dort nicht.
let counterpartOf (choice: CategoryPicker.PickerChoice) : MoveSide option =
    match choice with
    | CategoryPicker.ChoseToBeBudgeted -> Some ToBeBudgetedSide
    | CategoryPicker.ChoseCategory (Some (YnabCategoryId id)) -> Some (CategorySide id)
    | _ -> None

/// Ändert die Form, außer während des Aufrufs. Eine Absage gilt der alten Form und fällt weg.
let updateTransfer (change: TransferForm -> TransferForm) (model: Model) : Model =
    if inspectorBusy model then model
    else withTab (fun tab -> { tab with Transfer = { change tab.Transfer with Submission = Editing } }) model

/// Der Knopf. Gibt den Auftrag zurück, die Sperre prüft der Aufrufer (`canSend`): Beim Doppeltipp ginge
/// das Geld sonst zweimal, denn der Server rechnet relativ. Mit dem Auftrag kommt der Monat, den die
/// Seite zeigt: dorthin schreibt der Server.
let confirmTransfer (model: Model) : Model * (string * MoveRequest) option =
    match model.TabMoney, model.Month with
    | Some tab, Success month ->
        match transferRequest month tab with
        | Some request ->
            { model with
                MoveInFlight = MoveCallRunning
                TabMoney = Some { tab with Transfer = { tab.Transfer with Submission = Editing } } },
            Some (month.Month, request)
        | None -> model, None
    | _ -> model, None

let private resetTransfer (model: Model) : Model =
    withTab (fun tab -> { tab with Transfer = emptyTransfer }) model

/// Antwort auf einen Auftrag aus dem Tab. `Error`: nichts geschrieben, die Eingaben bleiben und der Tab
/// nennt den Grund. Ist er weg (neu betreten, Kategorie weggefallen), nennt ein Toast den Grund.
/// Mit Bericht ist die Form zurückgesetzt, der Modus bleibt, der Monat wird neu gelesen (`true`).
let applyMoveAnswered (result: Result<ApplyReport, string>) (model: Model) : Model * ExternalMsg * bool =
    let finished = { model with MoveInFlight = NoMoveCall }
    match result, model.TabMoney with
    | Ok report, _ ->
        awaitFreshMonth (noteMoveReport report.Month report (resetTransfer (withReport report finished))),
        moveToast report,
        true
    | Error message, Some _ ->
        let rejection = if model.MoveInFlight = MoveCallRunningAway then RejectedAway message else Rejected message
        withTab (fun tab -> { tab with Transfer = { tab.Transfer with Submission = rejection } }) finished,
        NoOp,
        false
    | Error message, None -> finished, ShowToast ($"Nicht verschoben: {message}", ToastError), false

/// Der Aufruf brach ab. Weil der Server relativ rechnet, wäre ein zweiter Versuch im Formular
/// gefährlich: es wird zurückgesetzt, und der Monat wird neu gelesen.
let applyMoveCallLost (message: string) (model: Model) : Model * ExternalMsg =
    awaitMonth (resetTransfer { model with MoveInFlight = NoMoveCall }), ShowToast ($"Ob verschoben wurde, ist unklar: {message}. Der Stand wird gleich neu gelesen.", ToastError)

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
    | Ok report -> awaitFreshMonth (noteMoveReport report.Month report (withReport report model)), moveToast report
    | Error message ->
        let reopened =
            model.OpenRetries
            |> List.map (fun r -> if r.ApplicationId = applicationId then { r with State = RetryReady } else r)
        awaitMonth { model with OpenRetries = reopened }, ShowToast ($"Erneut senden gescheitert: {message}", ToastError)
