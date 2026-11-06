/// BB führt das Zugewiesen je (Monat, Kategorie) (ADR 0084, Nachtrag zur Verfeinerung von qm5x9 und dgrp7,
/// budgeting-dgrp7): die Faltung des Monats-Streams zu Soll, bestätigtem Stand und offener Rückschreibung, die
/// Übernahme aus YNAB, die Echo-Regel und die Planung von "erneut senden". Rein, ohne I/O; Uhr, YNAB-Stand und
/// Ereignisse kommen als Parameter.
module Server.Budgeting.Assignments

open System
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain
open Budgeting.Events

/// Die Faltung eines Monats: je Kategorie Soll, bestätigter Stand und offene Rückschreibung. Ohne Übernahme im
/// Stream ist alles Historie und `Categories` leer.
type MonthFold = {
    Categories: Map<Guid, AssignmentState>
}

/// Der Stand einer Kategorie; ohne Eintrag Soll und bestätigter Stand 0 (`unassigned`).
let stateOf (fold: MonthFold) (categoryId: Guid) : AssignmentState =
    fold.Categories |> Map.tryFind categoryId |> Option.defaultValue unassigned

/// Die Zeilen eines Entscheidungsereignisses (Zuweisen, Verschieben je Seite, Anwenden je Zeile), leer für
/// jedes andere.
let decisionLines (event: BudgetEvent) : BudgetLine list =
    match event with
    | BudgetEvent.AssignmentChangedInBb p -> [ p.Line ]
    | BudgetEvent.MoneyMoved p -> [ p.From; p.To ]
    | BudgetEvent.TargetsApplied p -> p.Lines
    | _ -> []

let private isTakeover (event: BudgetEvent) =
    match event with
    | BudgetEvent.BudgetTakenOverFromYnab _ -> true
    | _ -> false

let private update (categoryId: Guid) (change: AssignmentState -> AssignmentState) (categories: Map<Guid, AssignmentState>) =
    let state = categories |> Map.tryFind categoryId |> Option.defaultValue unassigned
    Map.add categoryId (change state) categories

/// Die Wirkung eines Ereignisses auf die Faltung (Tabelle im Task budgeting-dgrp7). `eventId` ist die
/// `event_id`; nur eine Entscheidung braucht sie, sie wird zur offenen Rückschreibung.
let private applyEvent (categories: Map<Guid, AssignmentState>) (eventId: Guid, event: BudgetEvent) =
    match event with
    | BudgetEvent.BudgetTakenOverFromYnab p ->
        p.Categories
        |> List.fold
            (fun acc c -> Map.add c.CategoryId { Assigned = c.Assigned; Confirmed = c.Assigned; Open = None } acc)
            categories
    | BudgetEvent.AssignmentChangedInBb _
    | BudgetEvent.MoneyMoved _
    | BudgetEvent.TargetsApplied _ ->
        decisionLines event
        |> List.fold
            (fun acc l -> acc |> update l.CategoryId (fun s -> { s with Assigned = l.Assigned; Open = Some eventId }))
            categories
    | BudgetEvent.AssignmentWrittenBackToYnab r ->
        categories
        |> update r.CategoryId (fun s ->
            if s.Open = Some r.DecisionEventId then { s with Confirmed = s.Assigned; Open = None } else s)
    | BudgetEvent.AssignmentWriteBackWithdrawn r ->
        categories
        |> update r.CategoryId (fun s ->
            if s.Open = Some r.DecisionEventId then { s with Assigned = s.Confirmed; Open = None } else s)
    | BudgetEvent.AssignmentChangeObservedInYnab p ->
        Map.add p.CategoryId { Assigned = p.Assigned; Confirmed = p.Assigned; Open = None } categories
    // Fehlgeschlagen und ersetzt ändern nichts: ein Fehlschlag lässt die Rückschreibung offen, das Ersetzen
    // schließt die Beobachtung im selben Append.
    | BudgetEvent.AssignmentWriteBackFailed _
    | BudgetEvent.AssignmentWriteBackSupersededInYnab _
    | BudgetEvent.TargetPaused _
    | BudgetEvent.TargetPauseLifted _
    | BudgetEvent.YnabGoalChangedFromBb _
    | BudgetEvent.YnabGoalStepWrittenToYnab _
    | BudgetEvent.YnabGoalStepWriteFailed _
    | BudgetEvent.YnabGoalStepSupersededInYnab _ -> categories

/// Faltet die Ereignisse eines Monats-Streams in Stream-Reihenfolge ab dem jüngsten Übernahme-Ereignis.
/// Alles davor ist Historie und wirkt nicht (ADR 0084 Punkt 4).
let foldMonth (events: (Guid * BudgetEvent) list) : MonthFold =
    match events |> List.tryFindIndexBack (snd >> isTakeover) with
    | None -> { Categories = Map.empty }
    | Some index -> { Categories = events |> List.skip index |> List.fold applyEvent Map.empty }

// ============================================
// Übernahme
// ============================================

/// Zeilen von Entscheidungen, deren Rückschreibung noch offen ist: ohne Ergebnis oder zuletzt fehlgeschlagen.
let private openDecisionLines (events: (Guid * BudgetEvent) list) : WriteBackRef list =
    let latest =
        events
        |> List.fold
            (fun acc (_, event) ->
                match event with
                | BudgetEvent.AssignmentWrittenBackToYnab r
                | BudgetEvent.AssignmentWriteBackSupersededInYnab r
                | BudgetEvent.AssignmentWriteBackWithdrawn r -> Map.add (r.DecisionEventId, r.CategoryId) true acc
                | BudgetEvent.AssignmentWriteBackFailed (r, _) -> Map.add (r.DecisionEventId, r.CategoryId) false acc
                | _ -> acc)
            Map.empty
    events
    |> List.collect (fun (eventId, event) ->
        decisionLines event
        |> List.filter (fun l -> not (latest |> Map.tryFind (eventId, l.CategoryId) |> Option.defaultValue false))
        |> List.map (fun l -> { DecisionEventId = eventId; CategoryId = l.CategoryId }))

/// Im Stream liegt schon ein `BudgetTakenOverFromYnab` (ADR 0084 Punkt 5: Version 1 ist kein Merkmal).
let hasTakeover (events: (Guid * BudgetEvent) list) : bool =
    events |> List.exists (snd >> isTakeover)

/// Die Übernahme eines Monats aus dem gerade gelesenen YNAB-Stand, wenn im Stream noch keine liegt: ein
/// `BudgetTakenOverFromYnab` mit allen Kategorien außer `internal` (auch ausgeblendete und gelöschte), Werte
/// ungerundet, dahinter je offener Rückschreibung einer migrierten Entscheidung ein
/// `AssignmentWriteBackWithdrawn`. Liegt schon eine Übernahme im Stream, nichts.
let takeoverEvents (monthId: string) (events: (Guid * BudgetEvent) list) (raw: YnabMonth) : BudgetEvent list =
    if hasTakeover events then
        []
    else
        let takeover =
            BudgetEvent.BudgetTakenOverFromYnab
                { Month = monthId
                  Categories =
                    raw.Categories
                    |> List.filter (fun c -> not c.Internal)
                    |> List.map (fun c ->
                        { CategoryId = c.Id
                          CategoryName = c.Name.Trim()
                          Assigned = toMilliunits c.Assigned }) }
        takeover :: (openDecisionLines events |> List.map BudgetEvent.AssignmentWriteBackWithdrawn)

// ============================================
// Echo-Regel
// ============================================

/// Die Echo-Regel je Kategorie (ADR 0084 Punkt 3, Nachtrag; Vorbild Kassenbuch `Ledger/Domain.fs`). YNAB
/// zeigt das Soll: mit offener Rückschreibung ist sie geschrieben, ohne ist nichts. YNAB zeigt den bestätigten
/// Stand bei offener Rückschreibung: sie bleibt offen (ausstehend oder fehlgeschlagen). Sonst eine
/// Beobachtung, und eine offene Rückschreibung ist in YNAB ersetzt.
let echoOf (monthId: string) (categoryId: Guid) (categoryName: string) (state: AssignmentState) (ynab: int64) : BudgetEvent list =
    match state.Open with
    | _ when ynab = state.Assigned ->
        state.Open
        |> Option.map (fun decision -> BudgetEvent.AssignmentWrittenBackToYnab { DecisionEventId = decision; CategoryId = categoryId })
        |> Option.toList
    | Some _ when ynab = state.Confirmed -> []
    | _ ->
        [ BudgetEvent.AssignmentChangeObservedInYnab
              { Month = monthId
                CategoryId = categoryId
                CategoryName = categoryName
                PreviousAssigned = state.Assigned
                Assigned = ynab }
          match state.Open with
          | Some decision -> BudgetEvent.AssignmentWriteBackSupersededInYnab { DecisionEventId = decision; CategoryId = categoryId }
          | None -> () ]

/// Die Echo-Regel über alle Kategorien des YNAB-Monats außer `internal`. Eine Kategorie, die in der
/// YNAB-Antwort fehlt, ergibt nichts.
let echoEvents (monthId: string) (fold: MonthFold) (raw: YnabMonth) : BudgetEvent list =
    raw.Categories
    |> List.filter (fun c -> not c.Internal)
    |> List.collect (fun c -> echoOf monthId c.Id (c.Name.Trim()) (stateOf fold c.Id) (toMilliunits c.Assigned))

/// Vergleicht einen unter der Monatssperre gelesenen YNAB-Monat mit dem Stream: Übernahme, falls nötig, dann
/// die Echo-Regel. Liefert die anzuhängenden Ereignisse (ohne Befehls-Id, in dieser Reihenfolge) und die
/// Faltung danach. Übernahme und Echo tragen keine `event_id`, die Faltung braucht sie nicht.
let observe (monthId: string) (events: (Guid * BudgetEvent) list) (raw: YnabMonth) : BudgetEvent list * MonthFold =
    let withoutId (event: BudgetEvent) = Guid.Empty, event
    let takeover = takeoverEvents monthId events raw
    let afterTakeover = events @ (takeover |> List.map withoutId)
    let echo = echoEvents monthId (foldMonth afterTakeover) raw
    let added = takeover @ echo
    added, foldMonth (afterTakeover @ (echo |> List.map withoutId))

/// Eine neue Entscheidung auf Kategorien mit offener Rückschreibung zieht die alte zurück: je solcher Kategorie
/// ein `AssignmentWriteBackWithdrawn`, angehängt im selben Append hinter der Entscheidung.
let withdrawalsFor (fold: MonthFold) (categoryIds: Guid list) : WriteBackRef list =
    categoryIds
    |> List.distinct
    |> List.choose (fun id -> (stateOf fold id).Open |> Option.map (fun decision -> { DecisionEventId = decision; CategoryId = id }))

// ============================================
// Erneut senden
// ============================================

/// Grund einer Zeile, deren Kategorie YNAB nicht mehr liefert.
[<Literal>]
let CategoryNotFoundText = "Kategorie in YNAB nicht gefunden"

/// Grund der Quell-Zeile einer Verschiebung, wenn das Ziel im Monat fehlt.
[<Literal>]
let SourceHeldBackText = "Ziel in YNAB nicht gefunden, Quelle nicht gesendet"

/// Eine Zeile einer Entscheidung beim erneuten Senden, nach `observe`.
type RetryLine = {
    CategoryId: Guid
    /// Das jüngste Ergebnis der Zeile.
    Latest: LineOutcome option
    /// Ist diese Entscheidung die offene Rückschreibung der Kategorie?
    Open: bool
    /// Liefert YNAB die Kategorie noch?
    InYnab: bool
}

/// Was mit einer Zeile beim erneuten Senden geschieht.
type RetryAction =
    /// Nichts: geschlossen, oder bei einer Verschiebung hinter einer gescheiterten Quelle.
    | Leave
    /// Das Soll senden. Bei einer Verschiebung geht das Ziel nur hinaus, wenn die Quelle geschrieben ist.
    | Send
    /// Ungesendet schließen.
    | Close of WriteBackOutcome

let private isLost (line: RetryLine) =
    not line.Open && line.Latest <> Some Written

/// Plant "erneut senden" für die Zeilen einer Entscheidung in Sendereihenfolge. Offen ist eine Zeile nur, wenn
/// ihre Entscheidung die offene Rückschreibung der Kategorie ist; alles andere bleibt. Fehlt die Kategorie in
/// YNAB, schließt die Zeile fehlgeschlagen. Bei einer Verschiebung (`inOrder`) gilt die Paarregel aus ADR 0069
/// Punkt 3: Ist das Ziel verloren, wird die Quelle zurückgezogen, fehlt es in YNAB, bleibt die Quelle
/// ungesendet fehlgeschlagen. Ist die Quelle verloren, wird das Ziel zurückgezogen, ihr Soll fällt auf den
/// bestätigten Stand. Scheitert die Quelle in diesem Lauf, bleibt das Ziel unberührt (das entscheidet der
/// Ablauf beim Senden).
let planRetry (inOrder: bool) (lines: RetryLine list) : RetryAction list =
    let single (line: RetryLine) =
        if not line.Open then Leave
        elif not line.InYnab then Close (WriteBackFailed CategoryNotFoundText)
        else Send
    match inOrder, lines with
    | true, [ source; target ] ->
        let sourceAction =
            match single source with
            | Send when not target.Open && target.Latest = Some ChangedInYnab -> Close Withdrawn
            | Send when target.Open && not target.InYnab -> Close (WriteBackFailed SourceHeldBackText)
            | action -> action
        let targetAction =
            match sourceAction with
            | _ when not target.Open -> Leave
            | Close Withdrawn -> Close Withdrawn
            | Close (WriteBackFailed _) ->
                // Die Quelle schließt ungesendet fehlgeschlagen: sie fehlt in YNAB, oder ihr Ziel fehlt. Fehlgeschlagen
                // schließt die Rückschreibung nicht, sie bleibt bewusst offen, und jeder weitere Lauf hängt erneut
                // `Failed` an. Ohne geschriebene Quelle geht das Ziel nicht hinaus; fehlt es selbst in YNAB, bekommt
                // es seinen eigenen Grund.
                match single target with
                | Close outcome -> Close outcome
                | Send
                | Leave -> Leave
            | _ when isLost source -> Close Withdrawn
            | _ -> single target
        [ sourceAction; targetAction ]
    | _ -> lines |> List.map single
