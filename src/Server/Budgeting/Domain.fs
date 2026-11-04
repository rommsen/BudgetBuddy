/// Reine Regeln des Budgets (Context `budgeting`, budgeting-k7wmd): der laufende Monat, der Rahmen
/// der Seite und wie die rohen Kategorien eines Monats zu Gruppen werden. Kein I/O.
module Server.Budgeting.Domain

open System
open Shared.Domain
open Shared.Budgeting

/// Der Monatserste als "YYYY-MM-01". Tag und Uhrzeit spielen keine Rolle.
let monthKeyOf (day: DateTime) : string =
    day.ToString("yyyy-MM-01", Globalization.CultureInfo.InvariantCulture)

/// Der laufende Monat als "YYYY-MM-01". `today` ist ein Kalendertag ohne Uhrzeit, in Berlin zu
/// bestimmen (`Server.Api.berlinToday`).
let currentMonthKey (today: DateOnly) : string =
    monthKeyOf (today.ToDateTime TimeOnly.MinValue)

/// Der Rahmen der Budget-Seite. Die Antwort von `GET /months` trägt keine verlässliche Folge,
/// deshalb wird aufsteigend sortiert.
let toBudgetFrame (today: DateOnly) (months: string list) (order: Guid list) : BudgetFrame =
    { CurrentMonth = currentMonthKey today
      Months = List.sort months
      CategoryOrder = order }

/// Soll die Kategorie angezeigt werden? Ausgeblendete, gelöschte und interne Kategorien
/// (Inflow: Ready to Assign) fallen weg.
let isVisible (category: YnabMonthCategory) : bool =
    not category.Hidden && not category.Deleted && not category.Internal

let private toBudgetCategory (pausedInBb: Set<Guid>) (c: YnabMonthCategory) : BudgetCategory =
    { Id = c.Id
      Name = c.Name.Trim()
      Assigned = c.Assigned
      Activity = c.Activity
      Available = c.Available
      Underfunded = c.Underfunded
      Target = c.Target
      Pause = pauseSourceOf c.Snoozed (pausedInBb.Contains c.Id) }

/// Gruppiert die sichtbaren Kategorien nach Kategoriegruppe. Die Folge ist die der Monatsantwort
/// und damit ungeordnet (ADR 0074 Punkt 6): `toBudgetMonth` dient den Server-Abläufen, die Seite
/// ordnet mit `orderGroups`. Eine Gruppe ohne sichtbare Kategorie erscheint nicht.
///
/// `pausedInBb` sind die Kategorien, deren Target in BB für diesen Monat pausiert ist. Hier und nur
/// hier kommen YNAB-Monat und BB-Pause zusammen (ADR 0079 Punkt 3). Eine Id, die im Monat fehlt,
/// ändert nichts.
let toBudgetMonth (pausedInBb: Set<Guid>) (month: YnabMonth) : BudgetMonth =
    let groups =
        month.Categories
        |> List.filter isVisible
        |> List.fold
            (fun (order: string list, byGroup: Map<string, BudgetCategory list>) c ->
                let name = c.GroupName.Trim()
                let order' = if Map.containsKey name byGroup then order else name :: order
                let existing = byGroup |> Map.tryFind name |> Option.defaultValue []
                order', Map.add name (toBudgetCategory pausedInBb c :: existing) byGroup)
            ([], Map.empty)
        |> fun (order, byGroup) ->
            order
            |> List.rev
            |> List.map (fun name -> { Name = name; Categories = byGroup[name] |> List.rev })

    { Month = month.Month
      ToBeBudgeted = month.ToBeBudgeted
      Groups = groups }

// ============================================
// Targets anwenden (budgeting-h2zfn)
// ============================================

/// Die Vorschau von "Targets anwenden" aus dem Monatsmodell. Aufgenommen wird jede Kategorie
/// mit Fehlbetrag größer 0 und ohne pausiertes Target; neu = altes Zugewiesen + Fehlbetrag
/// (BB rechnet keine Targets, der Fehlbetrag ist YNABs). Pausierte Kategorien mit Fehlbetrag
/// größer 0 stehen getrennt in `Skipped`. Fehlbetrag 0 oder unbekannt fällt weg. Zu verteilen
/// danach darf negativ sein. Liegt ein Betrag nicht auf 10 Milliunits, lehnt die Funktion ab,
/// weil YNAB sonst still rundet. Rein, ohne I/O.
let planTargetApplication (month: BudgetMonth) : Result<TargetPreview, string> =
    let categories = month.Groups |> List.collect (fun g -> g.Categories |> List.map (fun c -> g.Name, c))

    let lines, skipped =
        categories
        |> List.fold
            (fun (lines, skipped) (groupName, c) ->
                match c.Underfunded with
                | Some under when under.Amount > 0m ->
                    if isPaused c then
                        lines,
                        { CategoryId = c.Id; CategoryName = c.Name; Underfunded = under } :: skipped
                    else
                        let line =
                            { CategoryId = c.Id
                              CategoryName = c.Name
                              GroupName = groupName
                              OldAssigned = c.Assigned
                              Underfunded = under
                              NewAssigned = { Amount = c.Assigned.Amount + under.Amount; Currency = c.Assigned.Currency }
                              Target = c.Target }
                        line :: lines, skipped
                | _ -> lines, skipped)
            ([], [])

    let lines = List.rev lines
    let skipped = List.rev skipped

    let offGrid =
        lines |> List.tryFind (fun l -> not (isOnTenMilliunits l.OldAssigned && isOnTenMilliunits l.NewAssigned))

    match offGrid with
    | Some l ->
        Error $"Der Betrag für {l.CategoryName} liegt nicht auf 10 Milliunits (0,01 €). YNAB würde ihn runden, deshalb wird nichts angewendet."
    | None ->
        let currency = month.ToBeBudgeted.Currency
        let total = lines |> List.sumBy (fun l -> l.Underfunded.Amount)
        Ok
            { Month = month.Month
              Lines = lines
              Skipped = skipped
              Total = { Amount = total; Currency = currency }
              ToBeBudgeted = month.ToBeBudgeted
              ToBeBudgetedAfter = { Amount = month.ToBeBudgeted.Amount - total; Currency = currency } }

// ============================================
// Target pausieren (budgeting-t6p8z, ADR 0073, ADR 0079)
// ============================================

/// Ein Ereignis in `target_pause_events`. Der Stand je Kategorie und Monat ist das jüngste.
type PauseKind =
    | Paused
    | Resumed

/// Der Text in `target_pause_events.kind`.
let pauseKindText (kind: PauseKind) : string =
    match kind with
    | Paused -> "Pausiert"
    | Resumed -> "Fortgesetzt"

/// Liest die Spalte. Ein unbekannter Text wird gemeldet, nicht auf einen Standard gelegt.
let pauseKindOfText (text: string) : Result<PauseKind, string> =
    match [ Paused; Resumed ] |> List.tryFind (fun k -> pauseKindText k = text) with
    | Some kind -> Ok kind
    | None -> Error $"Unbekannte Art eines Pause-Ereignisses: {text}"

/// Was aus einem Auftrag Pausieren oder Aufheben wird.
type PausePlan = {
    /// Die Kategorie, wie der frisch gelesene Monat sie zeigt.
    Category: BudgetCategory
    /// Das festzuhaltende Ereignis. `None`: der Stand ist schon so, es entsteht keine Zeile.
    Event: PauseKind option
    /// Die Pause der Kategorie danach.
    After: PauseSource option
}

/// Plant Pausieren (`Paused = true`) oder Aufheben (`false`) im übergebenen Monat. Die Monatsart prüft
/// der Ablauf vorher. Abgelehnt werden eine im Monat nicht sichtbare Kategorie und eine Kategorie ohne
/// Target. Doppeltes Pausieren und Aufheben ohne BB-Pause ergeben kein Ereignis. Aufheben gilt nur der
/// BB-Pause, eine Pause aus YNAB bleibt (ADR 0073 Punkt 2). Rein, ohne I/O.
let planPause (month: BudgetMonth) (request: TargetPauseRequest) : Result<PausePlan, string> =
    let category =
        month.Groups
        |> List.collect (fun g -> g.Categories)
        |> List.tryFind (fun c -> c.Id = request.CategoryId)
    match category with
    | None ->
        Error $"Die Kategorie {request.CategoryId} ist im Monat {month.Month} nicht sichtbar (ausgeblendet oder nicht vorhanden)."
    | Some c when c.Target.IsNone -> Error $"{c.Name} hat kein Target. Pausieren geht nur mit Target."
    | Some c ->
        let inBb = pausedInBb c.Pause
        let event =
            match request.Paused, inBb with
            | true, false -> Some Paused
            | false, true -> Some Resumed
            | true, true
            | false, false -> None
        Ok
            { Category = c
              Event = event
              After = pauseSourceOf (pausedInYnab c.Pause) request.Paused }

// ============================================
// Target ändern und entfernen (budgeting-k8wq3, ADR 0072, ADR 0080)
// ============================================

/// Lehnt ab, was budgeting-k8wq3 noch nicht kann, bevor YNAB gelesen wird: einen Rhythmuswechsel und
/// das Anlegen (`Shown = None` mit Wunsch). Rein.
let targetChangeSupported (request: TargetChangeRequest) : Result<unit, string> =
    match request.Shown, request.Wanted with
    | _, Some { NewRhythm = SetRhythm _ } -> Error TargetRhythmLaterText
    | None, Some _ -> Error TargetCreateLaterText
    | _ -> Ok ()

/// Die Patches vom gezeigten zum gewünschten Target (ADR 0072 Punkt 3, ADR 0080 Punkt 3): nur Felder, die
/// abweichen. Betrag ist nur `goal_target`, Datum nur `goal_target_date`, Entfernen `goal_target: null`.
/// Nie `goal_frequency`, nie `goal_needs_whole_amount`. Ohne Abweichung eine leere Liste. Gilt für jede
/// Art gleich (ADR 0072 Punkt 2). Rein.
let planTargetWrite (shown: Target) (wanted: TargetDraft option) : Result<TargetPatch list, string> =
    match wanted with
    | None -> Ok [ { GoalTarget = Clear; TargetDate = Keep; Frequency = None } ]
    | Some { NewRhythm = SetRhythm _ } -> Error TargetRhythmLaterText
    | Some draft ->
        let goal =
            if toMilliunits draft.NewGoal = toMilliunits shown.Goal then Keep
            else SetTo (toMilliunits draft.NewGoal)
        let date =
            match shown.Date, draft.NewDate with
            | before, after when before = after -> Keep
            | _, Some date -> SetTo date
            | _, None -> Clear
        match goal, date with
        | Keep, Keep -> Ok []
        | _ -> Ok [ { GoalTarget = goal; TargetDate = date; Frequency = None } ]

/// Die Art der Target-Änderung aus dem Wunsch: kein Wunsch heißt entfernen.
let targetChangeKindOf (wanted: TargetDraft option) : TargetChangeKind =
    match wanted with
    | None -> TargetRemoved
    | Some _ -> TargetEdited

/// Der Text in `target_changes.change_kind`.
let targetChangeKindText (kind: TargetChangeKind) : string =
    match kind with
    | TargetEdited -> "Geändert"
    | TargetRemoved -> "Entfernt"

/// Liest die Spalte. Ein unbekannter Text wird gemeldet, nicht auf einen Standard gelegt.
let targetChangeKindOfText (text: string) : Result<TargetChangeKind, string> =
    match [ TargetEdited; TargetRemoved ] |> List.tryFind (fun k -> targetChangeKindText k = text) with
    | Some kind -> Ok kind
    | None -> Error $"Unbekannte Art einer Target-Änderung: {text}"

/// Stimmt die frisch geplante Vorschau mit der gezeigten überein? Verglichen werden die Zeilen
/// (Kategorie, altes und neues Zugewiesen) und Zu verteilen; Namen und Reihenfolge der
/// pausierten Zeilen zählen nicht.
let previewsMatch (shown: TargetPreview) (fresh: TargetPreview) : bool =
    let key (p: TargetPreview) =
        p.Lines |> List.map (fun l -> l.CategoryId, l.OldAssigned, l.NewAssigned)
    shown.Month = fresh.Month && shown.ToBeBudgeted = fresh.ToBeBudgeted && key shown = key fresh

/// Der Text in `budget_movements.origin`. Geschrieben stehen diese Wörter in der Datenbank.
let originText (origin: MovementOrigin) : string =
    match origin with
    | TargetsApplied -> "Targets anwenden"
    | MoneyMoved -> "Verschieben"
    | MoneyAssigned -> "Zuweisen"

/// Liest die Spalte. Ein unbekannter Text wird gemeldet, nicht auf eine Herkunft abgebildet.
let originOfText (text: string) : Result<MovementOrigin, string> =
    match [ TargetsApplied; MoneyMoved; MoneyAssigned ] |> List.tryFind (fun o -> originText o = text) with
    | Some origin -> Ok origin
    | None -> Error $"Unbekannte Herkunft einer Budget-Bewegung: {text}"

/// Hängen die Zeilen eines Durchlaufs aneinander? Nur bei der Verschiebung: Quelle vor Ziel
/// (ADR 0069 Punkt 3). Bei Targets anwenden steht jede Zeile für sich.
let sendsInOrder (origin: MovementOrigin) : bool =
    match origin with
    | MoneyMoved -> true
    | TargetsApplied
    | MoneyAssigned -> false

/// Steht die Zeile noch aus? Fehlgeschlagen oder ohne Stand (der Lauf brach vor ihr ab).
let isOpenLine (latest: LineOutcome option) : bool =
    match latest with
    | None
    | Some (Failed _) -> true
    | Some Written
    | Some ChangedInYnab -> false

/// Was beim erneuten Senden einer fehlgeschlagenen Zeile mit dem Live-Wert zu tun ist.
type RetryDecision =
    /// YNAB steht auf dem alten Wert: den Zielwert (erneut) setzen.
    | SendTarget
    /// YNAB steht schon auf dem Zielwert: nichts senden, die Zeile ist geschrieben.
    | AlreadyAtTarget
    /// YNAB steht weder auf dem alten noch auf dem Zielwert: YNAB gewinnt (ADR 0055).
    | YnabWins

/// Entscheidung beim erneuten Senden. Das Ziel hat Vorrang vor dem alten Wert, falls beide
/// gleich sind (Fehlbetrag 0 kommt in einer Anwendung nicht vor).
let decideRetry (baseline: Money) (target: Money) (live: Money) : RetryDecision =
    if toMilliunits live = toMilliunits target then AlreadyAtTarget
    elif toMilliunits live = toMilliunits baseline then SendTarget
    else YnabWins

// ============================================
// Geld verschieben und zuweisen (ADR 0069, ADR 0078)
// ============================================


/// Eine Kategorie eines Auftrags: bei Verschieben neu = alt ∓ Betrag, bei Zuweisen der gesetzte Wert.
type MoveLine = {
    CategoryId: Guid
    CategoryName: string
    OldAssigned: Money
    NewAssigned: Money
}

/// Was aus einem Auftrag wird. `Lines` stehen in Sendereihenfolge: erst die Quelle, dann das Ziel.
/// Zuweisen hat genau eine Zeile.
type MovePlan = {
    Origin: MovementOrigin
    Lines: MoveLine list
}

/// Plant eine Verschiebung relativ zum übergebenen Monat. Zwischen zwei Kategorien entstehen zwei
/// Zeilen, mit Zu verteilen auf einer Seite eine, denn Zu verteilen ergibt sich aus den
/// Zuweisungen. Ein negatives neues Zugewiesen ist gültig (die Kategorie lebt vom Vortrag), eine
/// Sperre gegen negatives Verfügbar gibt es nicht. Abgelehnt werden Betrag kleiner gleich 0,
/// Beträge neben dem 10-Milliunits-Raster, gleiche Quelle und Ziel und im Monat fehlende
/// Kategorien; alle Gründe werden gesammelt. Rein, ohne I/O.
let planMove (month: BudgetMonth) (request: MoveRequest) : Result<MovePlan, string> =
    let categories = month.Groups |> List.collect (fun g -> g.Categories)

    let resolve side =
        match side with
        | ToBeBudgetedSide -> Ok None
        | CategorySide id ->
            match categories |> List.tryFind (fun c -> c.Id = id) with
            | Some c -> Ok (Some c)
            | None -> Error $"Die Kategorie {id} fehlt im Monat {month.Month}."

    let source = resolve request.Source
    let target = resolve request.Target
    let amount = request.Amount.Amount

    let line (sign: decimal) (c: BudgetCategory) : MoveLine =
        { CategoryId = c.Id
          CategoryName = c.Name
          OldAssigned = c.Assigned
          NewAssigned = { c.Assigned with Amount = c.Assigned.Amount + sign * amount } }

    let lines =
        match source, target with
        | Ok s, Ok t ->
            (s |> Option.map (line -1m) |> Option.toList) @ (t |> Option.map (line 1m) |> Option.toList)
        | _ -> []

    let errors =
        [ if amount <= 0m then "Der Betrag muss größer als 0 sein."
          elif not (isOnTenMilliunits request.Amount) then "Der Betrag muss auf den Cent genau sein."
          if request.Source = request.Target then "Quelle und Ziel müssen verschieden sein."
          match source with
          | Error e -> e
          | Ok _ -> ()
          match target with
          | Error e -> e
          | Ok _ -> ()
          for l in lines do
              if not (isOnTenMilliunits l.NewAssigned) then
                  $"Der Betrag für {l.CategoryName} liegt nicht auf 10 Milliunits (0,01 €). YNAB würde ihn runden, deshalb wird nichts verschoben." ]

    if List.isEmpty errors then
        Ok
            { Origin = if lines.Length = 2 then MoneyMoved else MoneyAssigned
              Lines = lines }
    else
        Error (String.concat " " (List.distinct errors))

/// Plant das Setzen eines Zugewiesen (ADR 0078): der gelesene Wert ist der alte, der eingegebene gilt.
/// Steht der Monat schon auf dem Wert, gibt es nichts zu tun (`None`). Ein negatives neues Zugewiesen
/// ist gültig. Abgelehnt werden eine im Monat fehlende Kategorie und Beträge neben dem
/// 10-Milliunits-Raster, alle Gründe gesammelt. Rein, ohne I/O.
let planAssign (month: BudgetMonth) (request: AssignRequest) : Result<MovePlan option, string> =
    let category =
        month.Groups
        |> List.collect (fun g -> g.Categories)
        |> List.tryFind (fun c -> c.Id = request.CategoryId)
    let errors =
        [ if category.IsNone then $"Die Kategorie {request.CategoryId} fehlt im Monat {month.Month}."
          if not (isOnTenMilliunits request.Assigned) then "Der Betrag muss auf den Cent genau sein." ]
    match category with
    | Some c when List.isEmpty errors ->
        if c.Assigned.Amount = request.Assigned.Amount then
            Ok None
        else
            let line =
                { CategoryId = c.Id
                  CategoryName = c.Name
                  OldAssigned = c.Assigned
                  NewAssigned = { c.Assigned with Amount = request.Assigned.Amount } }
            Ok (Some { Origin = MoneyAssigned; Lines = [ line ] })
    | _ -> Error (String.concat " " errors)

// ============================================
// Historie je Kategorie (budgeting-dyn8g)
// ============================================

/// Die Monatsersten der Ausgaben-Balken: die 6 Monate bis einschließlich `shown`, aufsteigend. Vor
/// dem Januar des Jahres 1 gibt es keinen Monat, dort werden es weniger.
let spendingMonths (shown: DateTime) : DateTime list =
    let index = shown.Year * 12 + shown.Month - 1
    [ for back in SpendingMonthCount - 1 .. -1 .. 0 do
          let i = index - back
          if i >= 12 then
              DateTime(i / 12, i % 12 + 1, 1) ]

/// Welche Bewegungen die Historie liest (budgeting-dyn8g). `None` heißt: ohne diese Einschränkung.
/// budgeting-h7q4v liest so alle Kategorien und filtert nach Herkunft.
type MovementFilter = {
    CategoryId: Guid option
    /// "YYYY-MM-01".
    Month: string option
    Origin: MovementOrigin option
}

/// Die Monatssummen zu den Monaten der Balken. Ein Monat ohne Summe steht mit 0 da.
let spendingOf (months: DateTime list) (sums: (DateTime * Money) list) : MonthSpending list =
    let byMonth = Map.ofList sums
    months
    |> List.map (fun m ->
        { Month = monthKeyOf m
          Activity = byMonth |> Map.tryFind m |> Option.defaultValue { Amount = 0m; Currency = "EUR" } })

/// Bewegungen und Pause-Ereignisse einer Kategorie gemischt, neueste zuerst (budgeting-t6p8z). Beide
/// Listen kommen schon neueste zuerst. Bei gleichem Zeitpunkt steht die Bewegung vorn, die Sortierung
/// ist stabil.
let mergeHistoryEntries (movements: HistoryMovement list) (pauses: HistoryEntry list) : HistoryEntry list =
    (movements |> List.map Movement) @ pauses |> List.sortByDescending historyEntryTime

/// Dazu die Target-Änderungen (budgeting-k8wq3), ebenso neueste zuerst. Bei gleichem Zeitpunkt stehen
/// Bewegungen und Pausen vorn.
let mergeHistoryWithTargetChanges (entries: HistoryEntry list) (changes: HistoryTargetChange list) : HistoryEntry list =
    entries @ (changes |> List.map TargetChanged) |> List.sortByDescending historyEntryTime
