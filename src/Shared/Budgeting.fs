/// Die Sprache des Budgets (Context `budgeting`): Monat, Zu verteilen, Kategorien nach
/// Gruppe mit Zugewiesen, Aktivität und Verfügbar. Die Beträge stammen live aus YNAB und
/// werden nirgends gespeichert (budgeting-k7wmd).
module Shared.Budgeting

open System
open Shared.Domain

/// Welcher Monat gezeigt wird: der laufende oder der nächste (Budgetieren am 30. für den 1.).
/// Auf dem Draht ein Schlüssel: "current" oder "next" (`monthChoiceKey`, `parseMonthChoice`).
type BudgetMonthChoice =
    | CurrentMonth
    | NextMonth

let monthChoiceKey (choice: BudgetMonthChoice) : string =
    match choice with
    | CurrentMonth -> "current"
    | NextMonth -> "next"

/// Liest den Schlüssel vom Draht. Alles andere als "current" und "next" ist ungültig.
let parseMonthChoice (key: string) : Result<BudgetMonthChoice, string> =
    match key with
    | "current" -> Ok CurrentMonth
    | "next" -> Ok NextMonth
    | other -> Error $"Ungültiger Monat: '{other}'. Erlaubt sind 'current' und 'next'."

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
    /// Pausiert (YNAB `goal_snoozed_at` gesetzt): der Fehlbetrag zählt in diesem Monat nicht.
    Snoozed: bool
    Hidden: bool
    Deleted: bool
    Internal: bool
}

/// Der Monat roh aus YNAB: Zu verteilen und alle Kategorien in YNABs Reihenfolge.
type YnabMonth = {
    /// Erster des Monats als "YYYY-MM-01".
    Month: string
    /// Zu verteilen (YNAB `to_be_budgeted`).
    ToBeBudgeted: Money
    Categories: YnabMonthCategory list
}

/// Eine angezeigte Kategorie. Felder wie `YnabMonthCategory`, ohne die Filter-Flags.
/// `Underfunded` (Fehlbetrag) und `Snoozed` (Pausiert) braucht "Targets anwenden"
/// (budgeting-h2zfn); die Seite zeigt sie nicht.
type BudgetCategory = {
    Id: Guid
    Name: string
    Assigned: Money
    Activity: Money
    Available: Money
    Underfunded: Money option
    Snoozed: bool
}

/// Eine Kategoriegruppe mit ihren sichtbaren Kategorien, in YNABs Reihenfolge.
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
