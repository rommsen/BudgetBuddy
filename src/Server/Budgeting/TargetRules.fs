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
            let isNew =
                match current with
                | None -> true
                | Some target -> (targetKindChange target draft).IsSome
            targetAfter current draft
            |> Result.map (fun after -> if isNew then { after with CreatedIn = Some month } else after)
            |> Result.map (fun after -> if Some after = current then TargetUnchanged current else SetRule after)
