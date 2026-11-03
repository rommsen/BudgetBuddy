/// Die Sprache des Budgets (Context `budgeting`): Monat, Zu verteilen, Kategorien nach
/// Gruppe mit Zugewiesen, Aktivität und Verfügbar. Die Beträge stammen live aus YNAB und
/// werden nirgends gespeichert (budgeting-k7wmd).
module Shared.Budgeting

open System
open Shared.Domain

/// Wie ein Monat zum laufenden steht (ADR 0074). Der Monat selbst ist ein String "YYYY-MM-01",
/// wie `BudgetMonth.Month`: Client und Server tauschen ihn direkt aus.
type MonthKind =
    | PastMonth
    | CurrentMonth
    | FutureMonth

/// Die Monatsart aus dem Vergleich mit dem laufenden Monat. "YYYY-MM-01" sortiert ordinal wie der
/// Kalender. Der laufende Monat kommt als Parameter: Server `currentMonthKey`, Client `BudgetFrame`.
let monthKind (current: string) (month: string) : MonthKind =
    let order = compare month current
    if order < 0 then PastMonth
    elif order = 0 then CurrentMonth
    else FutureMonth

/// Geht "Targets anwenden" in dieser Monatsart? In vergangenen Monaten nicht (ADR 0074 Punkt 5).
/// Gilt an der API-Grenze und für den Knopf.
let targetsAllowed (kind: MonthKind) : bool =
    match kind with
    | PastMonth -> false
    | CurrentMonth
    | FutureMonth -> true

/// In welchem Abstand ein Target wiederkehrt, in YNABs Kadenz gelesen (budgeting-kjsht).
/// Die Zahl ist der Abstand: `Months 1` ist monatlich, `Months 3` alle 3 Monate.
type TargetRhythm =
    | Weeks of every: int
    | Months of every: int
    | Years of every: int
    /// Keine Kadenz: nur der Betrag wird gezeigt.
    | NoRhythm

/// Das Target einer Kategorie, wie YNAB es liefert (`goal_target` und Kadenz). BB zeigt es nur
/// an und rechnet nicht damit (ADR 0065): was fehlt, sagt der Fehlbetrag von YNAB.
type Target = {
    Goal: Money
    Rhythm: TargetRhythm
}

/// Eine Kategorie so, wie YNABs Monats-Endpunkt sie liefert (vor dem Gruppieren).
/// Ausgeblendete, gelöschte und interne Kategorien (Inflow: Ready to Assign) sind hier noch
/// enthalten und werden erst von `Server.Budgeting.Domain.toBudgetMonth` weggelassen.
type YnabMonthCategory = {
    Id: Guid
    Name: string
    GroupName: string
    /// Zugewiesen (YNAB `budgeted`).
    Assigned: Money
    /// Aktivität (YNAB `activity`).
    Activity: Money
    /// Verfügbar (YNAB `balance`).
    Available: Money
    /// Fehlbetrag (YNAB `goal_under_funded`): `None` ohne Target, `Some 0` wenn erfüllt.
    Underfunded: Money option
    /// Das Target: `None`, wenn YNAB keins liefert.
    Target: Target option
    /// Pausiert (YNAB `goal_snoozed_at` gesetzt): der Fehlbetrag zählt in diesem Monat nicht.
    Snoozed: bool
    Hidden: bool
    Deleted: bool
    Internal: bool
}

/// Der Monat roh aus YNAB: Zu verteilen und alle Kategorien. Die Folge der Antwort ist ungeordnet und
/// je Monat anders (ADR 0074 Punkt 6), sie trägt keine Bedeutung.
type YnabMonth = {
    /// Erster des Monats als "YYYY-MM-01".
    Month: string
    /// Zu verteilen (YNAB `to_be_budgeted`).
    ToBeBudgeted: Money
    Categories: YnabMonthCategory list
}

/// Eine angezeigte Kategorie. Felder wie `YnabMonthCategory`, ohne die Filter-Flags.
/// `Underfunded` (Fehlbetrag) und `Snoozed` (Pausiert) braucht "Targets anwenden"
/// (budgeting-h2zfn); die Seite zeigt sie zusammen mit dem Target an (budgeting-kjsht).
type BudgetCategory = {
    Id: Guid
    Name: string
    Assigned: Money
    Activity: Money
    Available: Money
    Underfunded: Money option
    Target: Target option
    Snoozed: bool
}

/// Eine Kategoriegruppe mit ihren sichtbaren Kategorien. Vom Server in der ungeordneten Folge der
/// Monatsantwort, die Seite ordnet sie mit `orderGroups`.
type BudgetGroup = {
    Name: string
    Categories: BudgetCategory list
}

/// Der Monat, wie die Budget-Seite ihn zeigt.
type BudgetMonth = {
    /// Erster des Monats als "YYYY-MM-01".
    Month: string
    ToBeBudgeted: Money
    Groups: BudgetGroup list
}

/// Der Rahmen der Budget-Seite (budgeting-g8s3w, ADR 0074): liegt je Seitenaufruf im Client, damit
/// ein Monatswechsel nur eine Anfrage kostet.
type BudgetFrame = {
    /// Der laufende Monat "YYYY-MM-01" nach Berliner Kalender.
    CurrentMonth: string
    /// Das Monatsfenster aus `GET /months`, aufsteigend.
    Months: string list
    /// Alle Kategorie-Ids flach, Gruppe für Gruppe aus `GET /categories`.
    CategoryOrder: Guid list
}

/// Ordnet Gruppen und Kategorien nach dem Rang in `order` (ADR 0074 Punkt 6). Der Rang einer Gruppe
/// ist der kleinste ihrer Kategorien. Ohne Rang steht eine Kategorie am Ende ihrer Gruppe und eine
/// Gruppe am Ende der Liste, jeweils nach Name, weil die Monatsfolge springt. Es fällt nichts weg.
let orderGroups (order: Guid list) (groups: BudgetGroup list) : BudgetGroup list =
    let ranks = order |> List.mapi (fun index id -> id, index) |> Map.ofList
    let rankOf (category: BudgetCategory) = ranks |> Map.tryFind category.Id
    let sortKey (rank: int option) (name: string) =
        match rank with
        | Some r -> 0, r, ""
        | None -> 1, 0, name
    groups
    |> List.map (fun group ->
        { group with
            Categories = group.Categories |> List.sortBy (fun c -> sortKey (rankOf c) c.Name) })
    |> List.sortBy (fun group ->
        // Die Kategorien stehen schon nach Rang, die erste trägt den kleinsten.
        sortKey (group.Categories |> List.tryHead |> Option.bind rankOf) group.Name)

/// Fehlt: BBs Antwort je Kategorie, auch ohne Target. Das Größere aus negativem Verfügbar und
/// Fehlbetrag, `None` ohne Betrag über 0. Bei einem pausierten Target zählt der Fehlbetrag nicht,
/// negatives Verfügbar bleibt (ADR 0073 Punkt 4).
let missingAmount (c: BudgetCategory) : Money option =
    let negative = if c.Available.Amount < 0m then -c.Available.Amount else 0m
    let underfunded =
        match c.Underfunded with
        | Some under when not c.Snoozed -> under.Amount
        | _ -> 0m
    let amount = if underfunded > negative then underfunded else negative
    if amount > 0m then Some { c.Available with Amount = amount } else None

/// Frei: Verfügbar minus offener Fehlbetrag (budgeting-c5w2n). Ein pausiertes Target sperrt nichts,
/// ohne Target ist alles Verfügbare frei. Grundlage der Warnung beim Holen und Abgeben.
let freeAmount (c: BudgetCategory) : Money =
    match c.Underfunded with
    | Some under when not c.Snoozed -> { c.Available with Amount = c.Available.Amount - under.Amount }
    | _ -> c.Available

// ============================================
// Targets anwenden (budgeting-h2zfn)
// ============================================

/// Milliunits eines Betrags (YNAB rechnet in Tausendsteln). Ein Betrag mit mehr als drei
/// Nachkommastellen wird gerundet; Beträge aus YNAB haben nie mehr.
let toMilliunits (money: Money) : int64 =
    int64 (Math.Round(money.Amount * 1000m, MidpointRounding.AwayFromZero))

/// YNAB nimmt Zuweisungen nur auf 10 Milliunits genau an (12345 wurde 12350). Ein Betrag
/// dazwischen wäre eine stille Rundung und wird deshalb abgelehnt. Rechnet ohne Multiplikation,
/// damit ein zu großer Betrag nicht überläuft (budgeting-nfrds).
let isOnTenMilliunits (money: Money) : bool =
    money.Amount % 0.01m = 0m

/// Eine Kategorie der Vorschau: neu = altes Zugewiesen + Fehlbetrag.
type TargetLine = {
    CategoryId: Guid
    CategoryName: string
    GroupName: string
    OldAssigned: Money
    Underfunded: Money
    NewAssigned: Money
    /// Nur zur Anzeige; zählt nicht beim Vergleich der Vorschau (`previewsMatch`).
    Target: Target option
}

/// Eine Kategorie, deren Target pausiert ist: wird nicht angewendet, aber ausgewiesen.
type SkippedLine = {
    CategoryId: Guid
    CategoryName: string
    Underfunded: Money
}

/// Die Vorschau von "Targets anwenden". Schreibt nichts.
type TargetPreview = {
    /// Erster des Monats als "YYYY-MM-01".
    Month: string
    Lines: TargetLine list
    Skipped: SkippedLine list
    /// Summe der Fehlbeträge (= Summe der Erhöhungen).
    Total: Money
    /// Zu verteilen vor dem Anwenden.
    ToBeBudgeted: Money
    /// Zu verteilen nach dem Anwenden (Zu verteilen minus Summe), darf negativ sein.
    ToBeBudgetedAfter: Money
}

/// Stand einer Zeile einer Anwendung (das jüngste Ergebnis).
type LineOutcome =
    /// Nach YNAB geschrieben.
    | Written
    /// Nicht geschrieben, mit Grund (auch Rate-Limit). Erneut sendbar.
    | Failed of reason: string
    /// YNAB stand weder auf dem alten noch auf dem Zielwert: YNAB gewinnt, nichts geschrieben.
    | ChangedInYnab

type ApplyLineResult = {
    CategoryId: Guid
    CategoryName: string
    OldAssigned: Money
    NewAssigned: Money
    Outcome: LineOutcome
}

/// Ausgang einer Anwendung oder eines erneuten Sendens.
type ApplyReport = {
    ApplicationId: string
    Month: string
    Lines: ApplyLineResult list
    /// Zu verteilen, wie YNAB es nach dem Lauf meldet (`None`, wenn das erneute Lesen scheiterte).
    ActualToBeBudgeted: Money option
}

/// Antwort auf "Übernehmen": entweder ist der Stand gleich geblieben und es wurde
/// angewendet, oder YNAB steht anders als in der Vorschau und es wurde nichts geschrieben.
type ApplyResult =
    | Applied of ApplyReport
    | PreviewChanged of TargetPreview

/// Anzahl der Zeilen im Zustand `Written`.
let writtenCount (report: ApplyReport) : int =
    report.Lines |> List.filter (fun l -> l.Outcome = Written) |> List.length

/// Anzahl der Zeilen im Zustand `Failed`.
let failedCount (report: ApplyReport) : int =
    report.Lines |> List.filter (fun l -> match l.Outcome with Failed _ -> true | _ -> false) |> List.length

/// Ist dieser Monat der letzte im Fenster? `months` steht aufsteigend.
let isLastMonth (months: string list) (month: string) : bool =
    List.tryLast months = Some month

/// Kann dieser Bericht das Fenster geöffnet haben? Eine geschriebene Zuweisung ungleich 0 im letzten
/// Monat öffnet den nächsten (ADR 0074). Nur dann liest der Client den Rahmen neu.
let windowMayHaveGrown (months: string list) (report: ApplyReport) : bool =
    isLastMonth months report.Month
    && report.Lines |> List.exists (fun l -> l.Outcome = Written && l.NewAssigned.Amount <> 0m)

// ============================================
// Geld verschieben (budgeting-afd8h, ADR 0069)
// ============================================

/// Eine Seite einer Verschiebung: eine Kategorie oder Zu verteilen.
type MoveSide =
    | ToBeBudgetedSide
    | CategorySide of Guid

/// Der Auftrag des Clients. Keine Vorschau: wer 20 € verschiebt, meint 20 €, der Server rechnet
/// relativ zum frisch gelesenen Monat.
type MoveRequest = {
    Source: MoveSide
    Target: MoveSide
    /// Positiv, in Euro.
    Amount: Money
}

/// Das Feld Zugewiesen (budgeting-c5w2n, ADR 0078): der fertige Wert, absolut. Der Auftrag trägt
/// keinen erwarteten alten Stand, der eingegebene Wert gilt.
type AssignRequest = {
    CategoryId: Guid
    /// Das neue Zugewiesen in Euro, negativ ist gültig.
    Assigned: Money
}

// ============================================
// Historie je Kategorie (budgeting-dyn8g)
// ============================================

/// Woher eine Budget-Bewegung stammt (ADR 0065, ADR 0069). Die Historie zeigt und filtert danach.
type MovementOrigin =
    | TargetsApplied
    /// Zwischen zwei Kategorien.
    | MoneyMoved
    /// Eine Seite ist Zu verteilen.
    | MoneyAssigned

/// Eine festgehaltene Budget-Bewegung, wie die Historie sie liest.
type HistoryMovement = {
    MovementId: Guid
    /// Die Gruppen-Id des Durchlaufs (`application_id`, ADR 0069 Punkt 1).
    ApplicationId: Guid
    /// Zeitpunkt des Festhaltens, UTC.
    OccurredAt: DateTime
    Origin: MovementOrigin
    /// "YYYY-MM-01".
    Month: string
    CategoryId: Guid
    CategoryName: string
    OldAssigned: Money
    NewAssigned: Money
    /// Bei einer Verschiebung die Kategorie auf der anderen Seite (dieselbe Gruppen-Id), sonst `None`.
    Counterpart: string option
    /// Das jüngste Ergebnis. `None`: es ist noch keins angehängt.
    Latest: LineOutcome option
}

/// Die Netto-Aktivität einer Kategorie in einem Monat aus dem Kassenbuch (Erstattungen mindern,
/// gelöschte Buchungen zählen nicht, Splits mit ihrem Anteil). Ausgaben sind negativ wie bei YNAB.
type MonthSpending = {
    /// "YYYY-MM-01".
    Month: string
    Activity: Money
}

/// Antwort der Historie für Kategorie und Monat.
type CategoryHistory = {
    /// Die Bewegungen der Kategorie im Monat, neueste zuerst.
    Movements: HistoryMovement list
    /// Die 6 Monate bis einschließlich des gezeigten, aufsteigend. Monate ohne Buchung stehen mit 0 da.
    Spending: MonthSpending list
}

/// Der Stand einer Bewegung in der Historie: ihr jüngstes Ergebnis oder noch offen.
type HistoryStatus =
    | HistoryWritten
    | HistoryFailed of reason: string
    | HistoryChangedInYnab
    /// Kein Ergebnis angehängt: der Lauf brach vor dieser Zeile ab oder läuft noch.
    | HistoryOpen

/// Eine Zeile im Tab Historie, aus Sicht der Kategorie der Bewegung.
type HistoryLine = {
    MovementId: Guid
    OccurredAt: DateTime
    /// Was sich am Zugewiesen der Kategorie geändert hat: neu minus alt.
    Change: Money
    /// Der Text hinter dem Betrag: "nach Urlaub", "von Lebensmittel", "zugewiesen",
    /// "zurück nach Zu verteilen", "Targets anwenden".
    Text: string
    Status: HistoryStatus
}

let private historyStatus (latest: LineOutcome option) : HistoryStatus =
    match latest with
    | Some Written -> HistoryWritten
    | Some (Failed reason) -> HistoryFailed reason
    | Some ChangedInYnab -> HistoryChangedInYnab
    | None -> HistoryOpen

/// Bewegung zu Zeile. Die Richtung folgt aus dem Vorzeichen von neu minus alt: Bei einer Verschiebung
/// geht Geld nach der Gegenseite oder kommt von ihr, bei Zuweisen kommt es aus Zu verteilen oder geht
/// dorthin zurück.
let historyLine (m: HistoryMovement) : HistoryLine =
    let change = { m.NewAssigned with Amount = m.NewAssigned.Amount - m.OldAssigned.Amount }
    let incoming = change.Amount >= 0m
    let text =
        match m.Origin, m.Counterpart with
        | TargetsApplied, _ -> "Targets anwenden"
        | MoneyAssigned, _ -> if incoming then "zugewiesen" else "zurück nach Zu verteilen"
        | MoneyMoved, Some other -> if incoming then $"von {other}" else $"nach {other}"
        | MoneyMoved, None -> "verschoben"
    { MovementId = m.MovementId
      OccurredAt = m.OccurredAt
      Change = change
      Text = text
      Status = historyStatus m.Latest }

let private lastSundayUtc (year: int) (month: int) : DateTime =
    let last = DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc)
    last.AddDays(-(float (int last.DayOfWeek)))

/// Ein UTC-Zeitpunkt als Berliner Wanduhrzeit, unabhängig von der Zeitzone des Rechners oder
/// Browsers. Fable kennt `TimeZoneInfo` nicht, deshalb stehen die EU-Regeln hier: Sommerzeit (UTC+2)
/// vom letzten Sonntag im März bis zum letzten Sonntag im Oktober, jeweils 01:00 UTC, sonst UTC+1.
let berlinTime (instant: DateTime) : DateTime =
    let utc = if instant.Kind = DateTimeKind.Local then instant.ToUniversalTime() else instant
    let at (d: DateTime) = DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, DateTimeKind.Utc)
    let moment = at utc
    let summerStart = (lastSundayUtc utc.Year 3).AddHours 1.0
    let summerEnd = (lastSundayUtc utc.Year 10).AddHours 1.0
    let offset = if moment >= summerStart && moment < summerEnd then 2.0 else 1.0
    let local = moment.AddHours offset
    DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second, DateTimeKind.Unspecified)

/// Der Zeitpunkt einer Zeile in Berliner Zeit: "03.10. 11:30".
let historyTimeText (instant: DateTime) : string =
    let t = berlinTime instant
    let two (n: int) = (string n).PadLeft(2, '0')
    $"{two t.Day}.{two t.Month}. {two t.Hour}:{two t.Minute}"

/// Der Aufbau von Verfügbar: Vortrag aus dem Vormonat, dazu Zugewiesen und Aktivität.
type AvailableBuildup = {
    CarryOver: Money
    Assigned: Money
    Activity: Money
    Available: Money
}

/// Rechnet den Vortrag aus dem geladenen Monat zurück: Verfügbar minus Zugewiesen minus Aktivität.
/// Keine Anfrage, YNABs Werte bleiben, wie sie sind (ADR 0065).
let availableBuildup (c: BudgetCategory) : AvailableBuildup =
    { CarryOver = { c.Available with Amount = c.Available.Amount - c.Assigned.Amount - c.Activity.Amount }
      Assigned = c.Assigned
      Activity = c.Activity
      Available = c.Available }

/// Ein Balken der Ausgaben der letzten 6 Monate.
type SpendingBar = {
    /// "YYYY-MM-01".
    Month: string
    /// Ausgaben, also die Netto-Aktivität mit umgekehrtem Vorzeichen. Überwiegen Erstattungen, ist der
    /// Wert negativ.
    Spending: Money
    /// Höhe in Prozent des größten Balkens, 0 bis 100. Ein Monat ohne Ausgaben hat 0.
    Percent: int
    /// Der laufende Monat: Er ist noch nicht vorbei.
    Incomplete: bool
}

/// Die Balken aus den Monatssummen. `currentMonth` ist der laufende Monat ("YYYY-MM-01").
let spendingBars (currentMonth: string) (spending: MonthSpending list) : SpendingBar list =
    let spent (s: MonthSpending) = -s.Activity.Amount
    let highest = spending |> List.map spent |> List.fold max 0m
    spending
    |> List.map (fun s ->
        let amount = spent s
        let percent =
            if highest <= 0m || amount <= 0m then 0
            else int (Math.Round(amount / highest * 100m, MidpointRounding.AwayFromZero))
        { Month = s.Month
          Spending = { s.Activity with Amount = amount }
          Percent = percent
          Incomplete = monthKind currentMonth s.Month = CurrentMonth })
