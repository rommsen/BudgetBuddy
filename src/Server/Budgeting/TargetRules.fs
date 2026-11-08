/// Die Targets in BB (ADR 0084 Punkte 5 bis 7, budgeting-eh8xe): die einmalige Übernahme aus YNAB, die Faltung
/// des Streams `budgeting:target:<categoryId>` je Monat und Target ändern gegen BBs Stand. Rein, ohne I/O.
module Server.Budgeting.TargetRules

open System
open Shared.Budgeting
open Budgeting.Events

// ============================================
// Übernahme aus YNAB
// ============================================

/// Warum BB ein Goal nicht übernimmt, `None` bei einer übernehmbaren Art. Übernommen werden die in Romans Budget
/// genutzten Arten (ADR 0084 Punkt 6): NEED, TB, TBD und MF. DEBT und eine unbekannte Art nicht.
let notTakenOverReason (target: Target) : string option =
    match target.Kind with
    | Need
    | TargetBalance
    | TargetBalanceByDate
    | MonthlyFunding -> None
    | Debt -> Some "DEBT übernimmt BB nicht"
    | OtherKind goalType -> Some $"Unbekannte Art {goalType}"

/// Ist das Goal übernehmbar?
let isTakeable (target: Target) : bool = (notTakenOverReason target).IsNone

/// Führt dieses Ereignis BBs Regel? Übernahme, gesetzt und entfernt (`EventTypes.targetRules`). Liegt eins davon
/// im Target-Stream, führt BB das Target der Kategorie, und YNABs Goal wird nicht mehr übernommen.
let leadsTarget (event: BudgetEvent) : bool =
    match event with
    | BudgetEvent.TargetTakenOverFromYnab _
    | BudgetEvent.TargetSet _
    | BudgetEvent.TargetRemoved _ -> true
    | _ -> false

/// Ein Goal, das BB nicht übernommen hat, mit Grund. Wird gemeldet (Server-Log).
type NotTakenOver = {
    CategoryId: Guid
    CategoryName: string
    Reason: string
}

/// Was eine Übernahme festhält: je Kategorie ein `TargetTakenOverFromYnab` und die nicht übernommenen Goals.
type TargetTakeoverPlan = {
    /// Kategorie und Ereignis für ihren Target-Stream.
    Events: (Guid * BudgetEvent) list
    NotTakenOver: NotTakenOver list
}

/// Die Übernahme der Goals aus `/categories` (ADR 0084 Punkt 5): je Kategorie mit übernehmbarem Goal genau ein
/// `TargetTakenOverFromYnab` mit Regel und rohem `goal_type`. `led` sind die Kategorien, deren Target BB schon
/// führt (`leadsTarget`): sie bekommen nichts, so schreibt ein zweiter Lauf nichts. Eine Kategorie ohne Goal
/// bekommt nichts, ein nicht übernehmbares Goal steht in `NotTakenOver`.
let planTargetTakeover (led: Set<Guid>) (goals: YnabCategoryGoal list) : TargetTakeoverPlan =
    let pending =
        goals
        |> List.choose (fun g ->
            match g.Target with
            | Some target when not (led.Contains g.Id) -> Some (g, target)
            | _ -> None)
    { Events =
        pending
        |> List.filter (fun (_, target) -> isTakeable target)
        |> List.map (fun (g, target) ->
            g.Id,
            BudgetEvent.TargetTakenOverFromYnab
                { CategoryName = g.Name.Trim()
                  Rule = target
                  YnabGoalType = goalTypeText target.Kind })
      NotTakenOver =
        pending
        |> List.choose (fun (g, target) ->
            notTakenOverReason target
            |> Option.map (fun reason -> { CategoryId = g.Id; CategoryName = g.Name.Trim(); Reason = reason })) }

/// Die Kategorien des gelesenen Monats, die auf die Übernahme warten: YNAB zeigt für sie ein übernehmbares Goal,
/// und sie stehen nicht in `skip` (Target in BB geführt, oder eine frühere Übernahme fand für sie nichts). Ein
/// vergangener Monat wartet nie (`month.Month` vor `fromMonth`, "YYYY-MM-01"): sein Goal muss nicht mehr in
/// `/categories` stehen, sonst fragte jedes Lesen erneut (Review-Fund eh8xe). Nur wenn die Menge nicht leer ist,
/// fragt BB `/categories` (ein YNAB-Aufruf).
let awaitingTakeover (skip: Set<Guid>) (fromMonth: string) (month: YnabMonth) : Set<Guid> =
    if String.CompareOrdinal(month.Month, fromMonth) < 0 then
        Set.empty
    else
        month.Categories
        |> List.filter (fun c -> not (skip.Contains c.Id) && c.Target |> Option.exists isTakeable)
        |> List.map (fun c -> c.Id)
        |> Set.ofList

// ============================================
// Faltung
// ============================================

/// BBs Regel eines Targets im Monat `monthId` ("YYYY-MM"), aus den Ereignissen seines Streams in Stream-Reihenfolge.
/// Die Übernahme gilt für alle Monate und ist der Startstand; ein gesetztes oder entferntes Target gilt ab seinem
/// `EffectiveFrom`. So zeigt ein vergangener Monat die damals gültige Regel (ADR 0084 Punkt 7). Andere Ereignisse,
/// auch der Altbestand `YnabGoalChangedFromBb`, ändern nichts (ADR 0084 Punkt 4: der Zustand beginnt bei der
/// Übernahme).
let ruleAt (monthId: string) (events: BudgetEvent list) : Target option =
    let applies (effectiveFrom: string) = String.CompareOrdinal(effectiveFrom, monthId) <= 0
    events
    |> List.fold
        (fun state event ->
            match event with
            | BudgetEvent.TargetTakenOverFromYnab p -> Some p.Rule
            | BudgetEvent.TargetSet p when applies p.EffectiveFrom -> Some p.Rule
            | BudgetEvent.TargetRemoved p when applies p.EffectiveFrom -> None
            | _ -> state)
        None

/// Der Stand nach einem Ereignis, ohne Blick auf den Monat: Grundlage der Historie ("vorher" und "nachher").
let private step (state: Target option) (event: BudgetEvent) : Target option =
    match event with
    | BudgetEvent.TargetTakenOverFromYnab p -> Some p.Rule
    | BudgetEvent.TargetSet p -> Some p.Rule
    | BudgetEvent.TargetRemoved _ -> None
    | _ -> state

/// Je Ereignis des Streams der Stand davor und danach, in Stream-Reihenfolge.
let withStates (events: BudgetEvent list) : (BudgetEvent * Target option * Target option) list =
    events
    |> List.mapFold (fun state event -> let after = step state event in (event, state, after), after) None
    |> fst

/// Was eine Änderung in BB tat, aus dem Stand davor und danach (ADR 0084 Punkt 2: "Angelegt" und "50 → 60" aus
/// der vorigen Faltung, ohne eigene Typen). Ohne vorigen Stand angelegt, mit anderer Art oder anderem Rhythmus
/// gewechselt, sonst geändert. Ein Rhythmus, den BB nicht anlegt, gilt als geändert.
let changeKindBetween (before: Target option) (after: Target option) : TargetChangeKind =
    match before, after with
    | _, None -> TargetChangeKind.TargetRemoved
    | None, Some a ->
        match frequencyOfRhythm a.Rhythm with
        | Some f -> TargetCreated f
        | None -> TargetEdited
    | Some b, Some a when b.Kind <> a.Kind || b.Rhythm <> a.Rhythm ->
        match frequencyOfRhythm a.Rhythm with
        | Some f -> TargetKindChanged f
        | None -> TargetEdited
    | Some _, Some _ -> TargetEdited

// ============================================
// Target ändern (ADR 0080, Nachtrag ADR 0084)
// ============================================

/// Was aus einem Auftrag Target ändern wird.
type TargetChangePlan =
    /// BB steht schon auf dem Wunsch: nichts festzuhalten. Trägt den Stand.
    | TargetUnchanged of Target option
    /// Das gezeigte Target ist nicht BBs Stand: nichts festzuhalten. Trägt BBs Stand.
    | ShownOutdated of Target option
    /// Ein `TargetSet` mit der vollständigen Regel.
    | SetRule of Target
    /// Ein `TargetRemoved`.
    | RemoveRule

/// Plant Target anlegen, ändern, die Art wechseln oder entfernen gegen BBs Stand `current` (die Faltung im
/// laufenden Monat). Weicht das gezeigte Target davon ab, ist nichts festzuhalten (`ShownOutdated` mit BBs
/// Stand). Die Regel nach dem Wunsch baut `targetAfter`, ihr Fehler ist eine Ablehnung. Eine neue Regel
/// (Anlegen, Wechsel der Art) hat ihren Anlegemonat im Monat `month` ("YYYY-MM-01"), ab dem sie gilt; eine
/// geänderte behält den bisherigen (budgeting-pf8n7, die erste Periode beginnt dort). Rein.
let planTargetChange (month: string) (current: Target option) (request: TargetChangeRequest) : Result<TargetChangePlan, string> =
    if current <> request.Shown then
        Ok (ShownOutdated current)
    else
        match request.Wanted, current with
        | None, None -> Ok (TargetUnchanged None)
        | None, Some _ -> Ok RemoveRule
        | Some draft, _ ->
            let isNew = (newRuleFrequency current draft).IsSome
            targetAfter current draft
            |> Result.map (fun after -> if isNew then { after with CreatedIn = Some month } else after)
            |> Result.map (fun after -> if Some after = current then TargetUnchanged current else SetRule after)

// ============================================
// Targets nach YNAB (budgeting-e2z7v, Nachtrag ADR 0084 vom 2026-11-08, ADR 0072)
// ============================================

/// Die Aufrufe eines Schreibens nach YNAB und was YNABs Goal danach nicht zeigt.
type GoalWritePlan = {
    /// In dieser Reihenfolge zu senden; leer heißt, YNAB steht schon auf BBs Target.
    Calls: Server.YnabClient.GoalPatch list
    Gaps: YnabGoalGap list
}

/// Der Rhythmus, den BB in YNAB anlegen kann, am nächsten an BBs Rhythmus: alle n Wochen wöchentlich, alle n Jahre
/// jährlich, alle n Monate und einmalig monatlich (über die API entsteht weder ein Abstand über 1 noch ein Ziel
/// ohne Wiederholung, ADR 0072 Nachtrag 2026-11-04).
let private nearestFrequency (rhythm: TargetRhythm) : TargetFrequency =
    match rhythm with
    | Weeks _ -> Weekly
    | Years _ -> Yearly
    | Months _
    | NoRhythm -> Monthly

/// Was YNABs Goal `inYnab` von BBs Regel nicht zeigt. Verglichen werden nur Felder, die BB nicht schreiben kann:
/// Art, Rhythmus, Wochentag und Tag (ADR 0072 Punkte 2 und 4). Betrag, Datum und "Auffüllen bis" schreibt BB,
/// eine Abweichung dort ist keine Lücke. Ohne Goal in YNAB keine Lücke. Rein.
let goalGaps (rule: Target) (inYnab: Target option) : YnabGoalGap list =
    match inYnab with
    | None -> []
    | Some ynab ->
        [ if rule.Kind <> ynab.Kind then KindDiffers (rule.Kind, ynab.Kind)
          if rule.Rhythm <> ynab.Rhythm then RhythmDiffers (rule.Rhythm, ynab.Rhythm)
          match rule.Rhythm, rule.Day, ynab.Day with
          | Weeks _, Some _, None -> WeekdayMissing
          | Weeks _, Some day, Some other when other <> day -> WeekdayDiffers other
          | Weeks _, _, _ -> ()
          | _, Some _, None -> DayMissing
          | _ -> () ]

/// Plant das Schreiben von BBs Regel `rule` (`None`: kein Target) nach YNAB, gegen YNABs Goal `inYnab` (`None`: kein
/// Goal). Rein.
///
/// - Ohne Target: `goal_target: null`, wenn YNAB ein Goal hat, sonst nichts.
/// - Gleiche Art und gleicher Rhythmus wie in YNAB: nur die geänderten Felder `goal_target`, `goal_target_date`
///   und `goal_needs_whole_amount` in einem Aufruf, ohne `goal_frequency` (ADR 0072 Punkt 3). So bleiben Tag,
///   Wochentag und eine Art, die BB nicht anlegen kann, in YNAB erhalten.
/// - Sonst ersetzt `goal_frequency` YNABs Goal (NEED mit dem nächsten Rhythmus, `nearestFrequency`): Aufruf 1 mit
///   `goal_target`, `goal_frequency` und `goal_needs_whole_amount`, bei einem Datum Aufruf 2 mit `goal_target_date`
///   (ADR 0072 Punkt 5, beides in einem Aufruf lehnt YNAB ab). Tag und Wochentag gehen in YNAB dabei verloren.
///
/// `Gaps` vergleicht BBs Regel mit dem Goal, das YNAB danach zeigt (`goalGaps`).
let planGoalWrite (inYnab: Target option) (rule: Target option) : GoalWritePlan =
    let empty = Server.YnabClient.emptyGoalPatch
    match rule, inYnab with
    | None, None -> { Calls = []; Gaps = [] }
    | None, Some _ -> { Calls = [ { empty with GoalTarget = Server.YnabClient.SendNull } ]; Gaps = [] }
    | Some r, Some y when y.Kind = r.Kind && y.Rhythm = r.Rhythm ->
        let whole =
            match r.NeedsWholeAmount with
            | Some w when y.NeedsWholeAmount <> Some w -> Some w
            | _ -> None
        let patch =
            { empty with
                GoalTarget = (if y.Goal.Amount <> r.Goal.Amount then Server.YnabClient.SendValue (toMilliunits r.Goal) else Server.YnabClient.Unsent)
                TargetDate =
                    match r.Date with
                    | _ when y.Date = r.Date -> Server.YnabClient.Unsent
                    | Some date -> Server.YnabClient.SendValue date
                    | None -> Server.YnabClient.SendNull
                NeedsWholeAmount = whole }
        let after = { y with Goal = r.Goal; Date = r.Date; NeedsWholeAmount = (if whole.IsSome then whole else y.NeedsWholeAmount) }
        { Calls = (if patch = empty then [] else [ patch ]); Gaps = goalGaps r (Some after) }
    | Some r, _ ->
        let frequency = nearestFrequency r.Rhythm
        let first =
            { empty with
                GoalTarget = Server.YnabClient.SendValue (toMilliunits r.Goal)
                Frequency = Some frequency
                NeedsWholeAmount = r.NeedsWholeAmount }
        let dated = r.Date |> Option.map (fun date -> { empty with TargetDate = Server.YnabClient.SendValue date }) |> Option.toList
        let after =
            { Goal = r.Goal
              Rhythm = rhythmOfFrequency frequency
              Kind = Need
              Date = r.Date
              NeedsWholeAmount = r.NeedsWholeAmount
              Day = None
              CreatedIn = r.CreatedIn }
        { Calls = first :: dated; Gaps = goalGaps r (Some after) }

/// Die letzte Target-Entscheidung eines Streams: Übernahme, gesetzt oder entfernt, mit ihrer `event_id` und der Regel
/// danach (`None` nach dem Entfernen). `None` ohne Entscheidung (etwa nur Altbestand).
let latestDecision (events: (Guid * BudgetEvent) list) : (Guid * Target option) option =
    events
    |> List.fold
        (fun state (eventId, event) ->
            match event with
            | BudgetEvent.TargetTakenOverFromYnab p -> Some (eventId, Some p.Rule)
            | BudgetEvent.TargetSet p -> Some (eventId, Some p.Rule)
            | BudgetEvent.TargetRemoved _ -> Some (eventId, None)
            | _ -> state)
        None

/// Eine Target-Entscheidung, die noch nicht in YNAB ist.
type OpenTargetWrite = {
    DecisionEventId: Guid
    /// Die Regel der Entscheidung, `None` bei einem entfernten Target.
    Rule: Target option
    /// Grund des letzten Fehlschlags, ohne Ergebnis `TargetNotSentText`.
    Reason: string
}

/// Die offene Schreibung eines Target-Streams (Ereignisse mit `event_id` in Stream-Reihenfolge): die letzte
/// Entscheidung `TargetSet` oder `TargetRemoved`, solange kein `TargetWrittenToYnab` auf sie verweist. Die letzte
/// Entscheidung gewinnt, ein Ergebnis zu einer älteren zählt nicht. Die Übernahme ist aus YNAB gelesen und gilt als
/// in YNAB. Eine Entscheidung ohne Ergebnis ist offen ("noch nicht gesendet"), wie eine fehlgeschlagene. Rein.
let openTargetWrite (events: (Guid * BudgetEvent) list) : OpenTargetWrite option =
    let openFor (decisionEventId: Guid) (rule: Target option) =
        Some { DecisionEventId = decisionEventId; Rule = rule; Reason = TargetNotSentText }
    events
    |> List.fold
        (fun (state: OpenTargetWrite option) (eventId, event) ->
            match event, state with
            | BudgetEvent.TargetTakenOverFromYnab _, _ -> None
            | BudgetEvent.TargetSet p, _ -> openFor eventId (Some p.Rule)
            | BudgetEvent.TargetRemoved _, _ -> openFor eventId None
            | BudgetEvent.TargetWrittenToYnab r, Some o when o.DecisionEventId = r.DecisionEventId -> None
            | BudgetEvent.TargetWriteToYnabFailed (r, reason), Some o when o.DecisionEventId = r.DecisionEventId ->
                Some { o with Reason = reason }
            | _ -> state)
        None

/// Das Target einer Kategorie gegenüber YNAB für die Seite: offen heißt "Nicht in YNAB" ohne Lücken (gegen ein
/// veraltetes Goal sagten sie nichts); sonst die Lücken gegen YNABs Goal aus dem gelesenen Monat. `None`, wenn
/// nichts zu melden ist. Rein.
let targetYnabState (rule: Target option) (inYnab: Target option) (openWrite: OpenTargetWrite option) : TargetYnabState option =
    match openWrite, rule with
    | Some o, _ -> Some { NotInYnab = Some o.Reason; Gaps = [] }
    | None, Some r ->
        match goalGaps r inYnab with
        | [] -> None
        | gaps -> Some { NotInYnab = None; Gaps = gaps }
    | None, None -> None
