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
