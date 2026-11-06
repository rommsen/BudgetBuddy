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

/// Der Stand einer Kategorie in einem Monat, wie BB ihn aus seinen Ereignissen faltet (ADR 0084, Nachtrag
/// zur Verfeinerung von qm5x9 und dgrp7). Beträge in Milliunits.
type AssignmentState = {
    /// Das Soll: was BB als Zugewiesen führt.
    Assigned: int64
    /// Der bestätigte Stand: was YNAB zuletzt bestätigt hat. Baseline der Echo-Regel.
    Confirmed: int64
    /// Die offene Rückschreibung: die Entscheidung, deren Wert noch nicht in YNAB bestätigt ist. Höchstens
    /// eine; Soll ungleich bestätigtem Stand nur mit ihr.
    Open: Guid option
}

/// Eine Kategorie ohne Eintrag in der Faltung: Soll und bestätigter Stand 0, nichts offen.
let unassigned : AssignmentState = { Assigned = 0L; Confirmed = 0L; Open = None }

let private toBudgetCategory (assignments: Map<Guid, AssignmentState> option) (pausedInBb: Set<Guid>) (c: YnabMonthCategory) : BudgetCategory =
    let state = assignments |> Option.map (fun m -> m |> Map.tryFind c.Id |> Option.defaultValue unassigned)
    { Id = c.Id
      Name = c.Name.Trim()
      // Zugewiesen ist BBs Soll; Aktivität, Verfügbar und Fehlbetrag bleiben YNABs Werte (entschieden von Roman).
      Assigned =
        match state with
        | Some s -> { Amount = decimal s.Assigned / 1000m; Currency = c.Assigned.Currency }
        | None -> c.Assigned
      Activity = c.Activity
      Available = c.Available
      Underfunded = c.Underfunded
      Target = c.Target
      // Eine BB-Pause ohne Target wirkt nicht und wird nicht gemeldet (budgeting-r3wq8).
      Pause = pauseSourceOf c.Snoozed (c.Target.IsSome && pausedInBb.Contains c.Id)
      OpenWriteBack = state |> Option.bind (fun s -> s.Open) }

/// Gruppiert die sichtbaren Kategorien nach Kategoriegruppe. Die Folge ist die der Monatsantwort
/// und damit ungeordnet (ADR 0074 Punkt 6): `toBudgetMonth` dient den Server-Abläufen, die Seite
/// ordnet mit `orderGroups`. Eine Gruppe ohne sichtbare Kategorie erscheint nicht.
///
/// `pausedInBb` sind die Kategorien, deren Target in BB für diesen Monat pausiert ist. Hier und nur
/// hier kommen YNAB-Monat und BB-Pause zusammen (ADR 0079 Punkt 3). Eine Id, die im Monat fehlt,
/// ändert nichts.
///
/// `assignments` ist BBs Faltung des Monats (budgeting-dgrp7): mit ihr zeigt Zugewiesen das Soll und eine
/// offene Rückschreibung ihre Markierung; eine Kategorie ohne Eintrag hat Soll 0. `None` zeigt YNABs
/// Zugewiesen ohne Markierung, etwa wenn das Lesen die Faltung nicht festhalten konnte.
let toBudgetMonthWith (assignments: Map<Guid, AssignmentState> option) (pausedInBb: Set<Guid>) (month: YnabMonth) : BudgetMonth =
    let groups =
        month.Categories
        |> List.filter isVisible
        |> List.fold
            (fun (order: string list, byGroup: Map<string, BudgetCategory list>) c ->
                let name = c.GroupName.Trim()
                let order' = if Map.containsKey name byGroup then order else name :: order
                let existing = byGroup |> Map.tryFind name |> Option.defaultValue []
                order', Map.add name (toBudgetCategory assignments pausedInBb c :: existing) byGroup)
            ([], Map.empty)
        |> fun (order, byGroup) ->
            order
            |> List.rev
            |> List.map (fun name -> { Name = name; Categories = byGroup[name] |> List.rev })

    { Month = month.Month
      ToBeBudgeted = month.ToBeBudgeted
      Groups = groups }

/// Der Monat mit YNABs Zugewiesen, ohne BBs Faltung (Targets prüfen, Abläufe ohne Soll).
let toBudgetMonth (pausedInBb: Set<Guid>) (month: YnabMonth) : BudgetMonth =
    toBudgetMonthWith None pausedInBb month

// ============================================
// Targets anwenden (budgeting-h2zfn)
// ============================================

/// Der Anfang der Ablehnung von Targets anwenden bei offener Rückschreibung, dahinter die Kategorien.
[<Literal>]
let TargetsOpenWriteBackText =
    "Targets anwenden geht erst, wenn alle Zuweisungen in YNAB sind. Bitte zuerst erneut senden. Noch nicht in YNAB:"

/// Die Vorschau von "Targets anwenden" aus dem Monatsmodell. Aufgenommen wird jede Kategorie
/// mit Fehlbetrag größer 0 und ohne pausiertes Target; neu = Soll + Fehlbetrag (BB rechnet keine
/// Targets, der Fehlbetrag ist YNABs). Hat eine solche Kategorie eine offene Rückschreibung, lehnt die
/// Funktion ab und nennt sie (budgeting-dgrp7). Pausierte Kategorien mit Fehlbetrag
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

    // Bis budgeting-eh8xe ist der Fehlbetrag YNABs `goal_under_funded`, gerechnet auf YNABs Zugewiesen. Bei
    // offener Rückschreibung weicht das Soll davon ab, Soll plus Fehlbetrag wäre falsch (ADR 0084, Nachtrag).
    let notInYnab =
        lines |> List.filter (fun l -> categories |> List.exists (fun (_, c) -> c.Id = l.CategoryId && c.OpenWriteBack.IsSome))

    let offGrid =
        lines |> List.tryFind (fun l -> not (isOnTenMilliunits l.OldAssigned && isOnTenMilliunits l.NewAssigned))

    match notInYnab, offGrid with
    | _ :: _, _ ->
        let names = notInYnab |> List.map (fun l -> l.CategoryName) |> String.concat ", "
        Error $"{TargetsOpenWriteBackText} {names}."
    | [], Some l ->
        Error $"Der Betrag für {l.CategoryName} liegt nicht auf 10 Milliunits (0,01 €) und würde beim Schreiben gerundet. BB wendet deshalb nichts an."
    | [], None ->
        let currency = month.ToBeBudgeted.Currency
        let total = lines |> List.sumBy (fun l -> l.Underfunded.Amount)
        Ok
            { Month = month.Month
              Lines = lines
              Skipped = skipped
              Total = { Amount = total; Currency = currency }
              ToBeBudgeted = month.ToBeBudgeted
              ToBeBudgetedAfter = { Amount = month.ToBeBudgeted.Amount - total; Currency = currency }
              // Die Vorschau ist kein Befehl, die Id setzt der Client beim Übernehmen.
              CommandId = None }

// ============================================
// Target pausieren (budgeting-t6p8z, ADR 0073, ADR 0079)
// ============================================

/// Ein Pause-Ereignis (`TargetPaused`, `TargetPauseLifted`). Der Stand je Kategorie und Monat ist das jüngste. Ein Name je
/// Ereignis (budgeting-r3wq8): `PauseLifted` hieß in der früheren Spalte "Aufgehoben" und heißt in der Historie
/// `HistoryEntry.PauseLifted`.
[<RequireQualifiedAccess>]
type PauseKind =
    | Paused
    | PauseLifted

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

/// Stimmt die frisch geplante Vorschau mit der gezeigten überein? Verglichen werden die Zeilen
/// (Kategorie, altes und neues Zugewiesen) und Zu verteilen; Namen und Reihenfolge der
/// pausierten Zeilen zählen nicht.
let previewsMatch (shown: TargetPreview) (fresh: TargetPreview) : bool =
    let key (p: TargetPreview) =
        p.Lines |> List.map (fun l -> l.CategoryId, l.OldAssigned, l.NewAssigned)
    shown.Month = fresh.Month && shown.ToBeBudgeted = fresh.ToBeBudgeted && key shown = key fresh

/// Der frühere Text der Herkunft einer Budget-Bewegung, gelesen von der Budget-Migration und in Meldungen.
let originText (origin: MovementOrigin) : string =
    match origin with
    | TargetsApplied -> "Targets anwenden"
    | MoneyMoved -> "Verschieben"
    | MoneyAssigned -> "Zuweisen"
    | ObservedInYnab -> "In YNAB zugewiesen"
    | TakenOverFromYnab -> "Aus YNAB übernommen"

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
    | MoneyAssigned
    | ObservedInYnab
    | TakenOverFromYnab -> false

/// Steht die Zeile noch aus? Fehlgeschlagen oder ohne Stand (der Lauf brach vor ihr ab).
let isOpenLine (latest: LineOutcome option) : bool =
    match latest with
    | None
    | Some (Failed _) -> true
    | Some Written
    | Some ChangedInYnab -> false

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

/// Die Herkunft eines Verschiebens: zwischen zwei Kategorien eine Verschiebung, mit Zu verteilen auf einer
/// Seite ein Zuweisen. Die eine Regel für Planung und Befehls-Id.
let moveOriginOf (request: MoveRequest) : MovementOrigin =
    match request.Source, request.Target with
    | CategorySide _, CategorySide _ -> MoneyMoved
    | ToBeBudgetedSide, _
    | _, ToBeBudgetedSide -> MoneyAssigned

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
            { Origin = moveOriginOf request
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

/// Die Monatserste von Targets prüfen (budgeting-p2zr6, ADR 0082 Punkt 7): die 12 vollen Monate vor dem
/// Monat von `today`, aufsteigend, der laufende nie. Vor dem Januar des Jahres 1 werden es weniger.
let targetCheckMonths (today: DateTime) : DateTime list =
    let index = today.Year * 12 + today.Month - 1
    [ for back in 12 .. -1 .. 1 do
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

// ============================================
// Fehlbetrag in BB, Parallelrechnung (budgeting-v7kfw, ADR 0084 Punkt 7)
// ============================================

/// Der Fehlbetrag, wie BB ihn rechnet: gerechnet mit Betrag oder nicht gerechnet mit Grund.
type UnderfundedResult =
    | Computed of Money
    | NotComputed of reason: string

/// Nach welcher Formel BB den Fehlbetrag eines Targets rechnet (Zuordnungstabelle in budgeting-v7kfw).
type UnderfundedRule =
    /// NEED mit Datum oder TBD: aufrunden auf Cent von (Betrag − Übertrag) / verbleibende Monate, dann − Zugewiesen.
    | DueDateRule of due: string
    /// NEED "Refill up to" monatlich ohne Datum: Betrag − (Übertrag + Zugewiesen).
    | RefillRule
    /// NEED monatlich ohne Datum oder MF: Betrag − Zugewiesen.
    | MonthlyRule
    /// NEED wöchentlich mit Wochentag (0 Sonntag bis 6 Samstag): Vorkommen im Monat × Betrag − Zugewiesen.
    | WeeklyRule of day: int
    /// TB: Betrag − Verfügbar.
    | BalanceRule

/// Der Name der Formel, so wie die Abweichungsliste ihn zeigt.
let underfundedRuleText (rule: UnderfundedRule) : string =
    match rule with
    | DueDateRule _ -> "Mit Fälligkeit"
    | RefillRule -> "Auffüllen bis"
    | MonthlyRule -> "Monatlich"
    | WeeklyRule _ -> "Wöchentlich"
    | BalanceRule -> "Zielstand"

/// Die Formel zu einem Target, in der Reihenfolge der Zuordnungstabelle geprüft. `Error` trägt den Grund,
/// warum BB dieses Target nicht rechnet.
let underfundedRuleOf (target: Target) : Result<UnderfundedRule, string> =
    match target.Kind, target.Date with
    | Need, Some due
    | TargetBalanceByDate, Some due -> Ok (DueDateRule due)
    | TargetBalanceByDate, None -> Error "TBD ohne Datum"
    | Need, None ->
        match target.Rhythm, target.NeedsWholeAmount, target.Day with
        | Months 1, Some false, _ -> Ok RefillRule
        | Months 1, _, _ -> Ok MonthlyRule
        | Weeks 1, _, Some day when day >= 0 && day <= 6 -> Ok (WeeklyRule day)
        | Weeks 1, _, Some day -> Error $"NEED wöchentlich mit unbekanntem Wochentag {day}"
        | Weeks 1, _, None -> Error "NEED wöchentlich ohne Wochentag"
        | Weeks n, _, _ -> Error $"NEED alle {n} Wochen"
        | Months n, _, _ -> Error $"NEED alle {n} Monate ohne Datum"
        | Years n, _, _ -> Error $"NEED alle {n} Jahre ohne Datum"
        | NoRhythm, _, _ -> Error "NEED ohne Rhythmus"
    | MonthlyFunding, _ -> Ok MonthlyRule
    | TargetBalance, _ -> Ok BalanceRule
    | Debt, _ -> Error "DEBT rechnet BB nicht"
    | OtherKind goalType, _ -> Error $"Unbekannte Target-Art {goalType}"

let private monthIndexOf (text: string) : int option =
    // "YYYY-MM-DD" und "YYYY-MM-01": nur Jahr und Monat zählen.
    match DateTime.TryParseExact(text, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None) with
    | true, day -> Some (day.Year * 12 + day.Month - 1)
    | false, _ -> None

/// Die verbleibenden Monate vom gelesenen Monat bis zum Monat der Fälligkeit, beide eingeschlossen.
/// Eine Fälligkeit im gelesenen Monat oder davor ergibt 1.
let remainingMonths (month: string) (due: string) : Result<int, string> =
    match monthIndexOf month, monthIndexOf due with
    | Some m, Some d -> Ok (max 1 (d - m + 1))
    | None, _ -> Error $"Monat {month} nicht lesbar"
    | _, None -> Error $"Fälligkeit {due} nicht lesbar"

/// Wie oft der Wochentag `day` (0 Sonntag bis 6 Samstag, wie `DayOfWeek`) im Monat vorkommt.
let weekdayCount (month: string) (day: int) : Result<int, string> =
    match monthIndexOf month with
    | None -> Error $"Monat {month} nicht lesbar"
    | Some index ->
        let year = index / 12
        let monthNumber = index % 12 + 1
        [ 1 .. DateTime.DaysInMonth(year, monthNumber) ]
        |> List.filter (fun d -> int (DateTime(year, monthNumber, d).DayOfWeek) = day)
        |> List.length
        |> Ok

/// Auf den Cent aufgerundet. Nur für die Formel mit Fälligkeit, damit der Betrag auf 10 Milliunits liegt.
let private ceilingToCent (amount: decimal) : decimal =
    Math.Ceiling(amount * 100m) / 100m

/// BBs Fehlbetrag eines Targets im gelesenen Monat `month` ("YYYY-MM-01"), ohne I/O (ADR 0084 Punkt 7).
/// Übertrag = Verfügbar − Zugewiesen − Aktivität, also Verfügbar = Übertrag + Zugewiesen + Aktivität. Ein
/// negatives Ergebnis ist 0. Eine Pause spielt hier keine Rolle, ihre Wirkung entscheidet der Aufrufer.
let underfunded (target: Target) (month: string) (assigned: Money) (carryover: Money) (activity: Money) : UnderfundedResult =
    let goal = target.Goal.Amount
    let available = carryover.Amount + assigned.Amount + activity.Amount
    let amount =
        match underfundedRuleOf target with
        | Error reason -> Error reason
        | Ok (DueDateRule due) ->
            remainingMonths month due
            |> Result.map (fun months -> ceilingToCent ((goal - carryover.Amount) / decimal months) - assigned.Amount)
        | Ok RefillRule -> Ok (goal - (carryover.Amount + assigned.Amount))
        | Ok MonthlyRule -> Ok (goal - assigned.Amount)
        | Ok (WeeklyRule day) -> weekdayCount month day |> Result.map (fun count -> decimal count * goal - assigned.Amount)
        | Ok BalanceRule -> Ok (goal - available)
    match amount with
    | Ok value -> Computed { Amount = max 0m value; Currency = target.Goal.Currency }
    | Error reason -> NotComputed reason

/// Das Ergebnis einer Zeile der Abweichungsliste.
type ComparisonOutcome =
    | Equal
    | Deviation
    | NotCompared

/// Der Text des Ergebnisses, so steht er in `underfunded_comparisons.outcome`.
let comparisonOutcomeText (outcome: ComparisonOutcome) : string =
    match outcome with
    | Equal -> "Gleich"
    | Deviation -> "Abweichung"
    | NotCompared -> "Nicht gerechnet"

/// Der Grund, wenn YNAB zu einem Target keinen Fehlbetrag liefert.
[<Literal>]
let YnabWithoutValueText = "YNAB ohne Wert"

/// Eine Zeile der Abweichungsliste: BBs Fehlbetrag neben YNABs `goal_under_funded` für eine Kategorie.
type UnderfundedComparison = {
    Month: string
    CategoryId: Guid
    CategoryName: string
    /// Die Target-Art (`goal_type`).
    Kind: TargetKind
    /// Die Formel, nach der BB rechnet. `None`, wenn BB das Target nicht rechnet.
    Rule: UnderfundedRule option
    Outcome: ComparisonOutcome
    Bb: Money option
    Ynab: Money option
    /// BB − YNAB, nur wenn beide Werte da sind.
    Difference: Money option
    /// Der Grund bei "Nicht gerechnet".
    Reason: string option
    /// Pausiert in BB oder YNAB (`isPaused`).
    Paused: bool
}

/// Die Abweichungsliste eines Monats: je sichtbarer Kategorie mit Target eine Zeile, auch pausierte, weil
/// YNAB den Fehlbetrag auch für sie liefert. Verglichen wird genau, ohne Toleranz. Rein, ohne I/O.
let compareUnderfunded (month: BudgetMonth) : UnderfundedComparison list =
    month.Groups
    |> List.collect (fun g -> g.Categories)
    |> List.choose (fun c ->
        c.Target
        |> Option.map (fun target ->
            let carryover = { c.Available with Amount = c.Available.Amount - c.Assigned.Amount - c.Activity.Amount }
            let bb = underfunded target month.Month c.Assigned carryover c.Activity
            let rule = underfundedRuleOf target |> Result.toOption
            let row =
                { Month = month.Month
                  CategoryId = c.Id
                  CategoryName = c.Name
                  Kind = target.Kind
                  Rule = rule
                  Outcome = NotCompared
                  Bb = None
                  Ynab = c.Underfunded
                  Difference = None
                  Reason = None
                  Paused = isPaused c }
            match bb, c.Underfunded with
            | NotComputed reason, _ -> { row with Reason = Some reason }
            | Computed value, None -> { row with Bb = Some value; Reason = Some YnabWithoutValueText }
            | Computed value, Some ynab ->
                { row with
                    Outcome = (if value.Amount = ynab.Amount then Equal else Deviation)
                    Bb = Some value
                    Difference = Some { value with Amount = value.Amount - ynab.Amount } }))

/// Die Zeile im Server-Log je gelesenem Monat: Monat und Anzahl gleich, abweichend, nicht gerechnet.
/// Beträge stehen nur in der Tabelle.
let comparisonSummary (month: string) (rows: UnderfundedComparison list) : string =
    let count outcome = rows |> List.filter (fun r -> r.Outcome = outcome) |> List.length
    $"Parallelrechnung Fehlbetrag {month}: {count Equal} gleich, {count Deviation} abweichend, {count NotCompared} nicht gerechnet"
