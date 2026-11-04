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
      // Eine BB-Pause ohne Target wirkt nicht und wird nicht gemeldet (budgeting-r3wq8).
      Pause = pauseSourceOf c.Snoozed (c.Target.IsSome && pausedInBb.Contains c.Id) }

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
        Error $"Der Betrag für {l.CategoryName} liegt nicht auf 10 Milliunits (0,01 €) und würde beim Schreiben gerundet. BB wendet deshalb nichts an."
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

/// Ein Ereignis in `target_pause_events`. Der Stand je Kategorie und Monat ist das jüngste. Ein Name je
/// Ereignis (budgeting-r3wq8): `PauseLifted` heißt in der Spalte "Aufgehoben" und in der Historie
/// `HistoryEntry.PauseLifted`.
[<RequireQualifiedAccess>]
type PauseKind =
    | Paused
    | PauseLifted

/// Der Text in `target_pause_events.kind`. Geschrieben wird nur dieser Text.
let pauseKindText (kind: PauseKind) : string =
    match kind with
    | PauseKind.Paused -> "Pausiert"
    | PauseKind.PauseLifted -> "Aufgehoben"

/// Der frühere Text für `PauseLifted`. Lokale Dev-Datenbanken tragen ihn noch, gelesen wird er weiter,
/// geschrieben nie mehr.
[<Literal>]
let LegacyPauseLiftedText = "Fortgesetzt"

/// Liest die Spalte. Ein unbekannter Text wird gemeldet, nicht auf einen Standard gelegt.
let pauseKindOfText (text: string) : Result<PauseKind, string> =
    match [ PauseKind.Paused; PauseKind.PauseLifted ] |> List.tryFind (fun k -> pauseKindText k = text) with
    | Some kind -> Ok kind
    | None when text = LegacyPauseLiftedText -> Ok PauseKind.PauseLifted
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

/// Die Ablehnung, wenn die Kategorie im Monat nicht sichtbar ist. Ohne Guid und Monatsschlüssel, die
/// Meldung liest ein Mensch (budgeting-r3wq8).
[<Literal>]
let CategoryNotVisibleText = "Die Kategorie ist in diesem Monat nicht sichtbar (ausgeblendet oder nicht vorhanden)."

/// Plant Pausieren oder Aufheben im übergebenen Monat. Die Monatsart prüft der Ablauf vorher.
/// `pausedInBbRaw` sind die gespeicherten BB-Pausen des Monats, auch die ohne Target: `toBudgetMonth`
/// meldet eine BB-Pause ohne Target nicht, sie wirkt nicht, aufheben lässt sie sich trotzdem
/// (budgeting-r3wq8). Abgelehnt werden eine im Monat nicht sichtbare Kategorie und Pausieren ohne
/// Target. Doppeltes Pausieren und Aufheben ohne BB-Pause ergeben kein Ereignis. Aufheben gilt nur der
/// BB-Pause, eine Pause aus YNAB bleibt (ADR 0073 Punkt 2). Rein, ohne I/O.
let planPause (month: BudgetMonth) (pausedInBbRaw: Set<Guid>) (request: TargetPauseRequest) : Result<PausePlan, string> =
    let category =
        month.Groups
        |> List.collect (fun g -> g.Categories)
        |> List.tryFind (fun c -> c.Id = request.CategoryId)
    match category with
    | None -> Error CategoryNotVisibleText
    | Some c when request.Intent = PauseTarget && c.Target.IsNone ->
        Error $"{c.Name} hat kein Target. Pausieren geht nur mit Target."
    | Some c ->
        let inBb = pausedInBbRaw.Contains c.Id
        let event, pausedAfter =
            match request.Intent with
            | PauseTarget -> (if inBb then None else Some PauseKind.Paused), true
            | LiftPause -> (if inBb then Some PauseKind.PauseLifted else None), false
        Ok
            { Category = c
              Event = event
              After = pauseSourceOf (pausedInYnab c.Pause) pausedAfter }

// ============================================
// Target ändern und entfernen (budgeting-k8wq3, ADR 0072, ADR 0080)
// ============================================

/// Die Patches vom gezeigten zum gewünschten Target (ADR 0072 Punkt 3, ADR 0080 Punkt 3): nur Felder, die
/// abweichen. Betrag ist nur `goal_target`, Datum nur `goal_target_date`, Entfernen `goal_target: null`. Nie
/// `goal_needs_whole_amount`. Ohne Abweichung eine leere Liste. Gilt für jede Art gleich (ADR 0072 Punkt 2).
///
/// Anlegen (`shown = None`) und ein echter Wechsel der Art (`targetKindChange`, budgeting-xp46a) senden
/// `goal_target` mit `goal_frequency`. Ein Datum im Entwurf folgt dann als eigener zweiter Patch nur mit
/// `goal_target_date`: YNAB lehnt beides in einem Aufruf ab, belegt für jährlich (ADR 0072 Punkt 5), zwei
/// Aufrufe sind für alle Rhythmen gefahrlos. Anlegen ohne Rhythmus ist ein Fehler. Rein.
let planTargetWrite (shown: Target option) (wanted: TargetDraft option) : Result<TargetPatch list, string> =
    let replacing (frequency: TargetFrequency) (draft: TargetDraft) =
        [ { GoalTarget = SetTo (toMilliunits draft.NewGoal); TargetDate = Keep; Frequency = Some frequency }
          match draft.NewDate with
          | Some date -> { GoalTarget = Keep; TargetDate = SetTo date; Frequency = None }
          | None -> () ]
    match shown, wanted with
    | None, None -> Ok []
    | Some _, None -> Ok [ { GoalTarget = Clear; TargetDate = Keep; Frequency = None } ]
    | None, Some { NewRhythm = KeepRhythm } -> Error TargetRhythmMissingText
    | None, Some ({ NewRhythm = SetRhythm frequency } as draft) -> Ok (replacing frequency draft)
    | Some target, Some draft ->
        match targetKindChange target draft with
        | Some frequency -> Ok (replacing frequency draft)
        | None ->
            let goal =
                if toMilliunits draft.NewGoal = toMilliunits target.Goal then Keep
                else SetTo (toMilliunits draft.NewGoal)
            let date =
                match target.Date, draft.NewDate with
                | before, after when before = after -> Keep
                | _, Some date -> SetTo date
                | _, None -> Clear
            match goal, date with
            | Keep, Keep -> Ok []
            | _ -> Ok [ { GoalTarget = goal; TargetDate = date; Frequency = None } ]

/// Darf der nächste Schritt hinaus, nach dieser Antwort von YNAB auf den vorigen Patch (ADR 0080 Punkt 4)?
/// Setzte der vorige `goal_frequency`, nur wenn die Antwort den neuen Rhythmus zeigt: sonst gehörte das
/// Datum an ein Target, das nicht das gewünschte ist. Ohne `goal_frequency` immer. Rein.
let continueAfter (after: Target option) (previous: TargetPatch) : bool =
    match previous.Frequency with
    | None -> true
    | Some frequency -> after |> Option.exists (fun t -> t.Kind = Need && t.Rhythm = rhythmOfFrequency frequency)

/// Warum der nächste Schritt nach `continueAfter` nicht hinausgeht, aus YNABs Antwort und dem gewünschten
/// Rhythmus: kein Target, keine NEED-Art oder ein anderer Rhythmus (budgeting-c2t9w).
let heldBackCause (after: Target option) (frequency: TargetFrequency) : TargetHeldBackCause =
    match after with
    | None -> NoTargetAfterFirst
    | Some t when t.Kind <> Need -> OtherKindAfterFirst t.Kind
    | Some t -> OtherRhythmAfterFirst (t.Rhythm, frequency)

/// Ein Patch ohne gesetztes Feld. BB sendet ihn nie, der Ablauf lehnt ihn vor dem Senden ab.
let targetPatchIsEmpty (patch: TargetPatch) : bool =
    patch.GoalTarget = Keep && patch.TargetDate = Keep && patch.Frequency.IsNone

/// Die Ablehnung eines leeren Patches, BBs eigener Text.
[<Literal>]
let EmptyTargetPatchText = "Am Target ist nichts geändert, es gibt nichts zu senden."

/// Die Art der Target-Änderung aus dem gezeigten Stand und dem ersten Patch: kein Wunsch heißt entfernen,
/// `goal_frequency` ohne gezeigtes Target anlegen, mit gezeigtem die Art wechseln.
let targetChangeKindOf (shown: Target option) (wanted: TargetDraft option) (first: TargetPatch) : TargetChangeKind =
    match wanted, first.Frequency, shown with
    | None, _, _ -> TargetRemoved
    | Some _, Some frequency, None -> TargetCreated frequency
    | Some _, Some frequency, Some _ -> TargetKindChanged frequency
    | Some _, None, _ -> TargetEdited

/// Der Rhythmus in `target_changes.new_frequency`: "monthly", "weekly", "yearly".
let frequencyColumnText (frequency: TargetFrequency) : string =
    match frequency with
    | Weekly -> "weekly"
    | Monthly -> "monthly"
    | Yearly -> "yearly"

/// Die Texte in `target_changes.change_kind` und `new_frequency`.
let targetChangeKindText (kind: TargetChangeKind) : string * string option =
    match kind with
    | TargetEdited -> "Geändert", None
    | TargetRemoved -> "Entfernt", None
    | TargetCreated f -> "Angelegt", Some (frequencyColumnText f)
    | TargetKindChanged f -> "Art gewechselt", Some (frequencyColumnText f)

/// Liest beide Spalten. Ein unbekannter Text oder ein fehlender Rhythmus wird gemeldet, nicht auf einen
/// Standard gelegt.
let targetChangeKindOfText (text: string) (frequency: string option) : Result<TargetChangeKind, string> =
    let frequencyOf () =
        match frequency |> Option.bind (fun f -> [ Weekly; Monthly; Yearly ] |> List.tryFind (fun x -> frequencyColumnText x = f)) with
        | Some f -> Ok f
        | None ->
            let seen = frequency |> Option.defaultValue "keiner"
            Error $"Unbekannter Rhythmus einer Target-Änderung: {seen}"
    match text with
    | "Geändert" -> Ok TargetEdited
    | "Entfernt" -> Ok TargetRemoved
    | "Angelegt" -> frequencyOf () |> Result.map TargetCreated
    | "Art gewechselt" -> frequencyOf () |> Result.map TargetKindChanged
    | _ -> Error $"Unbekannte Art einer Target-Änderung: {text}"

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

/// Bewegungen, Pause-Ereignisse und Target-Änderungen einer Kategorie gemischt, neueste zuerst
/// (budgeting-t6p8z, budgeting-k8wq3, zusammengelegt in budgeting-h7q4v). Alle drei Listen kommen schon
/// neueste zuerst. Ein stabiles Sortieren: bei gleichem Zeitpunkt steht die Bewegung vor der Pause und
/// die Pause vor der Target-Änderung.
let mergeHistory (movements: HistoryMovement list) (pauses: HistoryEntry list) (changes: HistoryTargetChange list) : HistoryEntry list =
    (movements |> List.map Movement) @ pauses @ (changes |> List.map TargetChanged)
    |> List.sortByDescending historyEntryTime
