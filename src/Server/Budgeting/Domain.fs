/// Reine Regeln des Budgets (Context `budgeting`, budgeting-k7wmd): welcher YNAB-Monat zu
/// "dieser" oder "nächster" gehört und wie die rohen Kategorien eines Monats zu Gruppen
/// werden. Kein I/O.
module Server.Budgeting.Domain

open System
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
