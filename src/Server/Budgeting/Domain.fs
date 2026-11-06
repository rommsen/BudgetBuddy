/// Reine Regeln des Budgets (Context `budgeting`, budgeting-k7wmd): der laufende Monat, der Rahmen
/// der Seite und wie die rohen Kategorien eines Monats zu Gruppen werden. Kein I/O.
module Server.Budgeting.Domain

open System
open Shared.Domain
open Shared.Budgeting

/// Der Monatserste als "YYYY-MM-01". Tag und Uhrzeit spielen keine Rolle.
let monthKeyOf (day: DateTime) : string =
    day.ToString("yyyy-MM-01", Globalization.CultureInfo.InvariantCulture)

/// Der Kalendertag, auf den sich "dieser Monat" bezieht. Der Server läuft nicht zwingend in
/// Berliner Zeit: mit `DateTime.Today` wäre am Monatsersten bis 02:00 Uhr noch der Vormonat "dieser Monat".
/// Budget-Seite und Übernahme im Hintergrund nutzen ihn beide.
let berlinToday (berlinZone: TimeZoneInfo) (utcNow: DateTime) : DateOnly =
    DateOnly.FromDateTime(Ledger.Bot.Domain.berlinDayOf berlinZone utcNow)

/// Der laufende Monat als "YYYY-MM-01". `today` ist ein Kalendertag ohne Uhrzeit, in Berlin zu
/// bestimmen (`berlinToday`).
let currentMonthKey (today: DateOnly) : string =
    monthKeyOf (today.ToDateTime TimeOnly.MinValue)

/// Der Rahmen der Budget-Seite. Die Antwort von `GET /months` trägt keine verlässliche Folge,
/// deshalb wird aufsteigend sortiert.
let toBudgetFrame (today: DateOnly) (months: string list) (order: Guid list) : BudgetFrame =
    { CurrentMonth = currentMonthKey today
      Months = List.sort months
      CategoryOrder = order
      MonthStartHidden = [] }

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

// ============================================
// Targets anwenden (budgeting-h2zfn)
// ============================================

/// Die Vorschau von "Targets anwenden" aus dem Monatsmodell. Aufgenommen wird jede Kategorie
/// mit Fehlbetrag größer 0 und ohne pausiertes Target; neu = Soll + Fehlbetrag. Der Fehlbetrag ist BBs,
/// gerechnet gegen das Soll (`toBudgetMonthWith`, budgeting-hbhzf), deshalb gilt das auch bei offener
/// Rückschreibung. Pausierte Kategorien mit Fehlbetrag
/// größer 0 stehen getrennt in `Skipped`. Fehlbetrag 0 oder unbekannt fällt weg. Zu verteilen
/// danach darf negativ sein. Liegt ein Betrag nicht auf 10 Milliunits, lehnt die Funktion ab,
/// weil YNAB sonst still rundet. Rein, ohne I/O.
let planTargetApplication (month: BudgetMonth) : Result<TargetPreview, string> =
    let lines, skipped = targetApplicationLines month

    let offGrid =
        lines |> List.tryFind (fun l -> not (isOnTenMilliunits l.OldAssigned && isOnTenMilliunits l.NewAssigned))

    match offGrid with
    | Some l ->
        Error $"Der Betrag für {l.CategoryName} liegt nicht auf 10 Milliunits (0,01 €) und würde beim Schreiben gerundet. BB wendet deshalb nichts an."
    | None ->
        let empty =
            { Month = month.Month
              Lines = []
              Skipped = skipped
              Total = { Amount = 0m; Currency = month.ToBeBudgeted.Currency }
              ToBeBudgeted = month.ToBeBudgeted
              ToBeBudgetedAfter = month.ToBeBudgeted
              // Die Vorschau ist kein Befehl, die Id setzt der Client beim Übernehmen.
              CommandId = None }
        Ok (previewWithLines empty lines)

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
/// Target. Doppeltes Pausieren und Aufheben ohne BB-Pause ergeben kein Ereignis. Rein, ohne I/O.
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
              After = (if pausedAfter then Some PausedInBb else None) }

/// Gilt die gezeigte Vorschau noch gegen die frisch geplante? Die gezeigte trägt nur die gewählten Zeilen
/// (budgeting-m3x7f): jede muss in der frischen mit Kategorie, altem und neuem Zugewiesen stehen, und Zu verteilen
/// davor muss gleich sein. Abgewählte oder neu hinzugekommene Zeilen der frischen zählen nicht, sie werden nicht
/// geschrieben. Ohne gewählte Zeile gilt sie nicht. Namen, Targets und pausierte Zeilen zählen nicht.
let previewsMatch (shown: TargetPreview) (fresh: TargetPreview) : bool =
    let key (l: TargetLine) = l.CategoryId, l.OldAssigned, l.NewAssigned
    let freshKeys = fresh.Lines |> List.map key |> Set.ofList
    shown.Month = fresh.Month
    && shown.ToBeBudgeted = fresh.ToBeBudgeted
    && not (List.isEmpty shown.Lines)
    && shown.Lines |> List.forall (fun l -> freshKeys.Contains (key l))

/// Die Zeilen der frischen Vorschau, die die gezeigte gewählt hat, in der Reihenfolge der frischen.
let chosenLines (shown: TargetPreview) (fresh: TargetPreview) : TargetLine list =
    let chosen = shown.Lines |> List.map (fun l -> l.CategoryId) |> Set.ofList
    fresh.Lines |> List.filter (fun l -> chosen.Contains l.CategoryId)

/// Der frühere Text der Herkunft einer Budget-Bewegung, gelesen von der Budget-Migration und in Meldungen.
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
// Fehlbetrag in BB (budgeting-v7kfw, budgeting-hbhzf, ADR 0084 Punkt 7)
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

let private dayOf (text: string) : DateTime option =
    match DateTime.TryParseExact(text, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None) with
    | true, day -> Some day
    | false, _ -> None

/// Ein Rhythmus als Verschiebung um k Schritte, immer vom gespeicherten Datum aus, damit ein Monatsende (31.)
/// nicht wandert. `None` ohne Rhythmus.
let private rhythmShift (rhythm: TargetRhythm) : (DateTime -> int -> DateTime) option =
    match rhythm with
    | Weeks n when n > 0 -> Some (fun date k -> date.AddDays(float (7 * n * k)))
    | Months n when n > 0 -> Some (fun date k -> date.AddMonths(n * k))
    | Years n when n > 0 -> Some (fun date k -> date.AddYears(n * k))
    | _ -> None

let private monthIndexOfDay (day: DateTime) : int = day.Year * 12 + day.Month - 1

/// Wie viele Rhythmus-Schritte vom gespeicherten Datum bis zur ersten Fälligkeit im Monat `monthIndex` oder danach.
let private stepsUntil (shift: DateTime -> int -> DateTime) (date: DateTime) (monthIndex: int) : int =
    Seq.initInfinite id |> Seq.find (fun k -> monthIndexOfDay (shift date k) >= monthIndex)

/// Die Fälligkeit für die Formel "Mit Fälligkeit" im gelesenen Monat `month` (budgeting-r4kqm). Bei einem
/// Target mit Rhythmus ist es die erste Fälligkeit ab dem gelesenen Monat: das gespeicherte Datum, so oft
/// um den Rhythmus verschoben, bis es im gelesenen Monat oder danach liegt. YNAB lässt das gespeicherte
/// Datum eines wiederkehrenden Ziels in der Vergangenheit stehen und rechnet bis zur nächsten Fälligkeit.
/// Ohne Rhythmus oder mit unlesbarem Datum bleibt das gespeicherte Datum.
let dueFrom (rhythm: TargetRhythm) (month: string) (due: string) : string =
    match dayOf due, rhythmShift rhythm, monthIndexOf month with
    | Some date, Some shift, Some monthIndex ->
        (shift date (stepsUntil shift date monthIndex)).ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
    | _ -> due

/// Die Monate der laufenden Periode vor dem gelesenen Monat `month` (budgeting-b9869), aufsteigend als
/// "YYYY-MM-01". Die Periode beginnt im Monat der vorigen Fälligkeit, einschließlich (die Fälligkeit aus
/// `dueFrom`, um einen Rhythmus-Schritt zurück, budgeting-pf8n7). Ist das gespeicherte Datum die erste
/// Fälligkeit (`dueFrom` verschiebt es nicht) und der Anlegemonat `createdIn` ("YYYY-MM-01") lesbar, beginnt
/// die Periode im Anlegemonat. "Nicht verschoben heißt erste Fälligkeit" ist eine Annahme über YNAB: sie gilt,
/// solange YNAB das gespeicherte Datum eines wiederkehrenden Ziels stehen lässt (budgeting-r4kqm). Der gelesene Monat gehört nicht dazu: seine Aktivität steckt nicht im
/// Übertrag, ihre Ausgaben zählten sonst doppelt. Ohne Rhythmus oder mit unlesbarem Datum: keine Monate.
let periodMonthsBefore (rhythm: TargetRhythm) (createdIn: string option) (month: string) (due: string) : string list =
    match dayOf due, rhythmShift rhythm, monthIndexOf month with
    | Some date, Some shift, Some monthIndex ->
        let steps = stepsUntil shift date monthIndex
        let start =
            match steps, createdIn |> Option.bind monthIndexOf with
            | 0, Some created -> created
            | _ -> monthIndexOfDay (shift date (steps - 1))
        [ start .. monthIndex - 1 ]
        |> List.map (fun index -> DateTime(index / 12, index % 12 + 1, 1).ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture))
    | _ -> []

/// Die früheren Monate, deren Aktivität BB für den Fehlbetrag eines Targets im gelesenen Monat braucht: nur
/// bei "Mit Fälligkeit" mit Rhythmus, sonst keine.
let periodMonthsOf (target: Target) (month: string) : string list =
    match underfundedRuleOf target with
    | Ok (DueDateRule due) -> periodMonthsBefore target.Rhythm target.CreatedIn month due
    | _ -> []

/// Der Grund, wenn für einen Monat der laufenden Periode keine Aktivität vorliegt: er liegt vor der ersten
/// Buchung des Kassenbuchs (budgeting-k5q2w).
[<Literal>]
let PeriodActivityMissingText = "Aktivität der Periode fehlt"

/// Die Ausgaben der Periode: minus die Summe der Aktivität in `months`, Zuflüsse senken sie nicht unter 0.
/// `earlierActivity` ist die Aktivität der Kategorie je Monat ("YYYY-MM-01"). Fehlt ein Monat: `Error`.
let private periodSpending (months: string list) (earlierActivity: Map<string, Money>) : Result<decimal, string> =
    let found = months |> List.choose (fun m -> earlierActivity |> Map.tryFind m)
    if found.Length < months.Length then
        Error PeriodActivityMissingText
    else
        Ok (max 0m (-(found |> List.sumBy (fun a -> a.Amount))))

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
/// `earlierActivity` ist die Aktivität der Kategorie in früheren Monaten ("YYYY-MM-01"). "Mit Fälligkeit" zählt
/// wie YNAB die Ausgaben der laufenden Periode als gedeckt (budgeting-b9869): gedeckt = Übertrag + Ausgaben der
/// Periode vor dem gelesenen Monat (`periodMonthsOf`, mit Anlegemonat). Fehlt dafür ein Monat, ist das Ergebnis nicht gerechnet.
let underfunded
    (target: Target)
    (month: string)
    (assigned: Money)
    (carryover: Money)
    (activity: Money)
    (earlierActivity: Map<string, Money>)
    : UnderfundedResult =
    let goal = target.Goal.Amount
    let available = carryover.Amount + assigned.Amount + activity.Amount
    let amount =
        match underfundedRuleOf target with
        | Error reason -> Error reason
        | Ok (DueDateRule due) ->
            periodSpending (periodMonthsOf target month) earlierActivity
            |> Result.bind (fun spent ->
                remainingMonths month (dueFrom target.Rhythm month due)
                |> Result.map (fun months ->
                    ceilingToCent ((goal - (carryover.Amount + spent)) / decimal months) - assigned.Amount))
        | Ok RefillRule -> Ok (goal - (carryover.Amount + assigned.Amount))
        | Ok MonthlyRule -> Ok (goal - assigned.Amount)
        | Ok (WeeklyRule day) -> weekdayCount month day |> Result.map (fun count -> decimal count * goal - assigned.Amount)
        | Ok BalanceRule -> Ok (goal - available)
    match amount with
    | Ok value -> Computed { Amount = max 0m value; Currency = target.Goal.Currency }
    | Error reason -> NotComputed reason

/// Die früheren Monate, deren Aktivität der Fehlbetrag im Monat `month` braucht (budgeting-b9869): die
/// Periodenmonate aller Targets (`periodMonthsOf`), jeder Monat einmal, aufsteigend. Hängt nur an BBs Targets und am Monat,
/// damit die Ausgaben vor dem Monatsmodell geladen werden können (budgeting-hbhzf). Rein, ohne I/O.
let periodMonthsNeeded (targets: Map<Guid, Target>) (month: string) : string list =
    targets
    |> Map.toList
    |> List.collect (fun (_, t) -> periodMonthsOf t month)
    |> List.distinct
    |> List.sort

/// Die Kategorien, deren Target Periodenmonate braucht (`periodMonthsOf`, budgeting-k5q2w), jede einmal. Rein.
let periodCategoriesNeeded (targets: Map<Guid, Target>) (month: string) : Guid list =
    targets
    |> Map.toList
    |> List.filter (fun (_, t) -> not (List.isEmpty (periodMonthsOf t month)))
    |> List.map fst

let private firstOfMonth (monthKey: string) : DateTime =
    DateTime.ParseExact(monthKey, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)

/// Formt die Monatssummen aus dem Kassenbuch in die Aktivität je Kategorie und Monat ("YYYY-MM-01"), die
/// `toBudgetMonthWith` nimmt (budgeting-k5q2w). Lückenlos: ein Monat ohne Buchung der Kategorie steht mit 0
/// da. Nur ein Monat vor dem Monat der ersten Buchung (`firstEntry`) fehlt, ohne Buchung fehlt jeder. Rein.
let periodActivityFromLedger
    (categoryIds: Guid list)
    (months: string list)
    (firstEntry: DateTime option)
    (sums: Map<Guid, (DateTime * Money) list>)
    : Map<Guid, Map<string, Money>> =
    let covered =
        match firstEntry with
        | None -> []
        | Some first ->
            let start = DateTime(first.Year, first.Month, 1)
            months |> List.filter (fun m -> firstOfMonth m >= start)
    categoryIds
    |> List.distinct
    |> List.map (fun id ->
        let byMonth = sums |> Map.tryFind id |> Option.defaultValue [] |> Map.ofList
        id,
        covered
        |> List.map (fun m -> m, byMonth |> Map.tryFind (firstOfMonth m) |> Option.defaultValue { Amount = 0m; Currency = TargetCurrency })
        |> Map.ofList)
    |> Map.ofList

let private toBudgetCategory
    (month: string)
    (assignments: Map<Guid, AssignmentState> option)
    (periodActivity: Map<Guid, Map<string, Money>> option)
    (targets: Map<Guid, Target>)
    (pausedInBb: Set<Guid>)
    (c: YnabMonthCategory)
    : BudgetCategory =
    let target = targets |> Map.tryFind c.Id
    let state = assignments |> Option.map (fun m -> m |> Map.tryFind c.Id |> Option.defaultValue unassigned)
    // Zugewiesen ist BBs Soll; Aktivität und Verfügbar bleiben YNABs Werte (entschieden von Roman).
    let assigned =
        match state with
        | Some s -> euroOfMilliunits s.Assigned
        | None -> c.Assigned
    // Der Übertrag kommt live aus YNABs Stand, unabhängig vom Soll (ADR 0084 Punkt 7).
    let carryover = { c.Available with Amount = c.Available.Amount - c.Assigned.Amount - c.Activity.Amount }
    // BBs Fehlbetrag gegen das Soll (budgeting-hbhzf). Nicht gerechnet heißt kein Fehlbetrag, wie ohne Target.
    let underfundedOf (rule: Target) =
        periodActivity
        |> Option.bind (fun activity ->
            let earlier = activity |> Map.tryFind c.Id |> Option.defaultValue Map.empty
            match underfunded rule month assigned carryover c.Activity earlier with
            | Computed value -> Some value
            | NotComputed _ -> None)
    { Id = c.Id
      Name = c.Name.Trim()
      Assigned = assigned
      Activity = c.Activity
      Available = c.Available
      Underfunded = target |> Option.bind underfundedOf
      // Das Target ist BBs Regel für diesen Monat, YNABs Goal zählt nicht (budgeting-eh8xe, ADR 0084 Punkt 6).
      Target = target
      // Eine BB-Pause ohne Target wirkt nicht und wird nicht gemeldet (budgeting-r3wq8). Eine Pause in YNAB wirkt
      // nicht (budgeting-eh8xe, Nachtrag ADR 0079).
      Pause = (if target.IsSome && pausedInBb.Contains c.Id then Some PausedInBb else None)
      OpenWriteBack = state |> Option.bind (fun s -> s.Open) }

/// Gruppiert die sichtbaren Kategorien nach Kategoriegruppe. Die Folge ist die der Monatsantwort
/// und damit ungeordnet (ADR 0074 Punkt 6): `toBudgetMonth` dient den Server-Abläufen, die Seite
/// ordnet mit `orderGroups`. Eine Gruppe ohne sichtbare Kategorie erscheint nicht.
///
/// `pausedInBb` sind die Kategorien, deren Target in BB für diesen Monat pausiert ist, `targets` BBs Regeln
/// im Monat (budgeting-eh8xe). Hier und nur hier kommen YNAB-Monat, BB-Pause und Target zusammen (ADR 0079
/// Punkt 3). Eine Id, die im Monat fehlt, ändert nichts.
///
/// `assignments` ist BBs Faltung des Monats (budgeting-dgrp7): mit ihr zeigt Zugewiesen das Soll und eine
/// offene Rückschreibung ihre Markierung; eine Kategorie ohne Eintrag hat Soll 0. `None` zeigt YNABs
/// Zugewiesen ohne Markierung, etwa wenn das Lesen die Faltung nicht festhalten konnte.
///
/// `periodActivity` sind die Ausgaben der Periodenmonate je Kategorie aus dem Kassenbuch (`periodMonthsNeeded`,
/// `periodActivityFromLedger`). Damit trägt jede Kategorie mit Target BBs Fehlbetrag (`underfunded`, gegen
/// Zugewiesen, budgeting-hbhzf); rechnet BB ihn nicht, ist er `None`. `None` statt der Ausgaben heißt: kein
/// Fehlbetrag in keiner Zeile, etwa wenn das Kassenbuch nicht lesbar war.
let toBudgetMonthWith
    (assignments: Map<Guid, AssignmentState> option)
    (periodActivity: Map<Guid, Map<string, Money>> option)
    (targets: Map<Guid, Target>)
    (pausedInBb: Set<Guid>)
    (month: YnabMonth)
    : BudgetMonth =
    let groups =
        month.Categories
        |> List.filter isVisible
        |> List.fold
            (fun (order: string list, byGroup: Map<string, BudgetCategory list>) c ->
                let name = c.GroupName.Trim()
                let order' = if Map.containsKey name byGroup then order else name :: order
                let existing = byGroup |> Map.tryFind name |> Option.defaultValue []
                order', Map.add name (toBudgetCategory month.Month assignments periodActivity targets pausedInBb c :: existing) byGroup)
            ([], Map.empty)
        |> fun (order, byGroup) ->
            order
            |> List.rev
            |> List.map (fun name -> { Name = name; Categories = byGroup[name] |> List.rev })

    { Month = month.Month
      ToBeBudgeted = month.ToBeBudgeted
      Groups = groups }

/// Der Monat mit YNABs Zugewiesen, ohne BBs Faltung und ohne Ausgaben der Periode (Targets prüfen, das den
/// Fehlbetrag nicht nutzt). Ein Target, das Periodenmonate braucht, bleibt hier ohne Fehlbetrag.
let toBudgetMonth (targets: Map<Guid, Target>) (pausedInBb: Set<Guid>) (month: YnabMonth) : BudgetMonth =
    toBudgetMonthWith None (Some Map.empty) targets pausedInBb month

// ============================================
// Übernahme im Hintergrund (budgeting-dkjc5, ADR 0084 Punkt 5)
// ============================================

/// Die Reihenfolge, in der der Hintergrunddienst Monate übernimmt: der laufende Monat (Berliner Kalendermonat),
/// dann die künftigen aus `GET /months` aufsteigend, dann die älteren absteigend. Schon übernommene Monate fallen
/// heraus. Fehlt der laufende Monat in `months`, steht er trotzdem vorn. Alle Monate als "YYYY-MM-01".
let takeoverQueue (currentMonth: string) (months: string list) (takenOver: Set<string>) : string list =
    let later = months |> List.filter (fun m -> m > currentMonth) |> List.distinct |> List.sort
    let earlier = months |> List.filter (fun m -> m < currentMonth) |> List.distinct |> List.sortDescending
    currentMonth :: later @ earlier |> List.filter (fun m -> not (takenOver.Contains m))

/// Was bei einem gescheiterten Schritt schiefging. Die Fälle können Meldungen mit Daten tragen; ins Log kommt nur
/// der Fall- oder Typname (`TakeoverWorker.logLine`).
[<RequireQualifiedAccess>]
type TakeoverFailure =
    /// YNAB antwortet mit einem anderen Fehler als 401 und 429.
    | Ynab of YnabError
    /// Die übernommenen Monate waren lokal nicht lesbar.
    | TakenOverUnreadable
    /// Der Schritt warf eine Ausnahme dieses Typs.
    | Crashed of typeName: string

/// Was ein Schritt des Hintergrunddienstes ergab.
[<RequireQualifiedAccess>]
type TakeoverOutcome =
    /// Der Monat ist übernommen (oder lag beim Prüfen unter der Sperre schon übernommen im Stream).
    | TookOver of month: string
    /// YNAB kennt den Monat nicht (404). Er wird in diesem Durchlauf übersprungen.
    | MonthNotOpen of month: string
    /// Kein Monat mehr offen. Der nächste Durchlauf holt die Liste neu.
    | NothingLeft
    /// YNAB meldet 429.
    | RateLimited
    /// YNAB meldet 401: das Token gilt nicht (mehr).
    | Unauthorized
    /// Token oder Budget fehlen in den Settings.
    | NotConfigured
    /// Etwas anderes ging schief. Scheiterte die Übernahme eines Monats, überspringt der Durchlauf ihn.
    | Failed of TakeoverFailure

/// Die Wartezeit nach einem Schritt (budgeting-dkjc5, Takt von Roman entschieden): nach einem Monatsaufruf eine
/// Minute, also höchstens 60 Monatsaufrufe je Stunde; nach 429, 401, einem Fehler oder fehlenden Settings 15 Minuten;
/// ist nichts mehr offen, 6 Stunden. Die Wartezeit aus `RateLimitExceeded` zählt nicht.
let nextDelay (outcome: TakeoverOutcome) : TimeSpan =
    match outcome with
    | TakeoverOutcome.TookOver _
    | TakeoverOutcome.MonthNotOpen _ -> TimeSpan.FromMinutes 1.0
    | TakeoverOutcome.RateLimited
    | TakeoverOutcome.Unauthorized
    | TakeoverOutcome.Failed _
    | TakeoverOutcome.NotConfigured -> TimeSpan.FromMinutes 15.0
    | TakeoverOutcome.NothingLeft -> TimeSpan.FromHours 6.0
