/// Reine Regeln des Budgets (Context `budgeting`, budgeting-k7wmd): welcher YNAB-Monat zu
/// "dieser" oder "nächster" gehört und wie die rohen Kategorien eines Monats zu Gruppen
/// werden. Kein I/O.
module Server.Budgeting.Domain

open System
open Shared.Domain
open Shared.Budgeting

/// "YYYY-MM-01" des gewählten Monats, bezogen auf `today`. Der nächste Monat nach Dezember
/// ist der Januar des Folgejahres.
let monthKey (today: DateTime) (choice: BudgetMonthChoice) : string =
    let first = DateTime(today.Year, today.Month, 1)
    let month =
        match choice with
        | CurrentMonth -> first
        | NextMonth -> first.AddMonths 1
    month.ToString("yyyy-MM-01", Globalization.CultureInfo.InvariantCulture)

/// Soll die Kategorie angezeigt werden? Ausgeblendete, gelöschte und interne Kategorien
/// (Inflow: Ready to Assign) fallen weg.
let isVisible (category: YnabMonthCategory) : bool =
    not category.Hidden && not category.Deleted && not category.Internal

let private toBudgetCategory (c: YnabMonthCategory) : BudgetCategory =
    { Id = c.Id
      Name = c.Name.Trim()
      Assigned = c.Assigned
      Activity = c.Activity
      Available = c.Available
      Underfunded = c.Underfunded
      Snoozed = c.Snoozed }

/// Gruppiert die sichtbaren Kategorien nach Kategoriegruppe. Gruppen und Kategorien behalten
/// YNABs Reihenfolge (die Gruppe steht dort, wo ihre erste sichtbare Kategorie steht). Eine
/// Gruppe ohne sichtbare Kategorie erscheint nicht.
let toBudgetMonth (month: YnabMonth) : BudgetMonth =
    let groups =
        month.Categories
        |> List.filter isVisible
        |> List.fold
            (fun (order: string list, byGroup: Map<string, BudgetCategory list>) c ->
                let name = c.GroupName.Trim()
                let order' = if Map.containsKey name byGroup then order else name :: order
                let existing = byGroup |> Map.tryFind name |> Option.defaultValue []
                order', Map.add name (toBudgetCategory c :: existing) byGroup)
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
                    if c.Snoozed then
                        lines,
                        { CategoryId = c.Id; CategoryName = c.Name; Underfunded = under } :: skipped
                    else
                        let line =
                            { CategoryId = c.Id
                              CategoryName = c.Name
                              GroupName = groupName
                              OldAssigned = c.Assigned
                              Underfunded = under
                              NewAssigned = { Amount = c.Assigned.Amount + under.Amount; Currency = c.Assigned.Currency } }
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

/// Stimmt die frisch geplante Vorschau mit der gezeigten überein? Verglichen werden die Zeilen
/// (Kategorie, altes und neues Zugewiesen) und Zu verteilen; Namen und Reihenfolge der
/// pausierten Zeilen zählen nicht.
let previewsMatch (shown: TargetPreview) (fresh: TargetPreview) : bool =
    let key (p: TargetPreview) =
        p.Lines |> List.map (fun l -> l.CategoryId, l.OldAssigned, l.NewAssigned)
    shown.Month = fresh.Month && shown.ToBeBudgeted = fresh.ToBeBudgeted && key shown = key fresh

/// Woher eine Budget-Bewegung stammt (ADR 0065, ADR 0069).
type MovementOrigin =
    | TargetsApplied
    /// Zwischen zwei Kategorien.
    | MoneyMoved
    /// Eine Seite ist Zu verteilen.
    | MoneyAssigned

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
// Geld verschieben (budgeting-afd8h, ADR 0069)
// ============================================


/// Eine Kategorie einer Verschiebung: neu = alt ∓ Betrag.
type MoveLine = {
    CategoryId: Guid
    CategoryName: string
    OldAssigned: Money
    NewAssigned: Money
}

/// Was aus einem Auftrag wird. `Lines` stehen in Sendereihenfolge: erst die Quelle, dann das Ziel.
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
