/// Abfragen des Budgets (budgeting-g8s3w, ADR 0074). YNAB kommt als Port herein, die Befehle in
/// `CommandHandlers` brauchen den Rahmen nicht und bleiben davon unberührt.
module Server.Budgeting.QueryHandlers

open System
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain

/// Die Ursache eines Lesefehlers bleibt im Server-Log, der Client bekommt nur den Text der Meldung.
let private logReadFailure (what: string) (ex: exn) : unit =
    Budgeting.Persistence.logReadFailure what ex.Message

/// Liest einen Monat über `fetchMonth`, mit den BB-Pausen des Monats (ADR 0079). Eine Pause gilt nur
/// für ihren Monat, der Folgemonat liest seine eigenen (ADR 0073 Punkt 3).
let budgetMonthOf (fetchMonth: string -> Async<YnabResult<YnabMonth>>) (monthKey: string) : Async<YnabResult<BudgetMonth>> =
    async {
        match! fetchMonth monthKey with
        | Error e -> return Error e
        | Ok month ->
            match! Budgeting.Persistence.loadPausedCategories monthKey with
            | Error message -> return Error (YnabError.LocalReadFailed message)
            | Ok paused -> return Ok (toBudgetMonth paused month)
    }

/// Liest einen Monat für die Budget-Seite über den YNAB-Port (`budgetMonthOf`).
let budgetMonth (port: CommandHandlers.YnabPort) (monthKey: string) : Async<YnabResult<BudgetMonth>> =
    budgetMonthOf port.FetchMonth monthKey

/// Wie viele Kategorien "Zuletzt verwendet" liest. Mehr als der Picker zeigt: X und ausgeblendete
/// Kategorien fallen dort noch heraus.
[<Literal>]
let RecentMoveLimit = 10

/// "Zuletzt verwendet" ohne Datenbank: `load` liest die Bewegungen.
let recentMoveCategoriesWith (load: int -> Async<Guid list>) : Async<Result<Guid list, string>> =
    async {
        try
            let! recent = load RecentMoveLimit
            return Ok recent
        with ex ->
            logReadFailure "Zuletzt verwendet lesen" ex
            return Error "Zuletzt verwendete Kategorien nicht lesbar"
    }

/// "Zuletzt verwendet": lokal, kostet kein YNAB-Limit.
let recentMoveCategories () : Async<Result<Guid list, string>> =
    recentMoveCategoriesWith Budgeting.Persistence.loadRecentMoveCategories

/// Die beiden Lese-Quellen des Rahmens.
type BudgetFramePort = {
    FetchMonths: unit -> Async<YnabResult<string list>>
    FetchCategoryOrder: unit -> Async<YnabResult<Guid list>>
}

/// Liest den Rahmen. Scheitert eine Quelle, ist das Ergebnis dieser Fehler.
let frame (port: BudgetFramePort) (today: DateOnly) : Async<YnabResult<BudgetFrame>> =
    async {
        match! port.FetchMonths () with
        | Error e -> return Error e
        | Ok months ->
            match! port.FetchCategoryOrder () with
            | Error e -> return Error e
            | Ok order -> return Ok (toBudgetFrame today months order)
    }

/// Die Lese-Quellen der Historie (budgeting-dyn8g): die Bewegungen aus `budget_movements` und die
/// Monatssummen aus dem Kassenbuch. Beide lesen nur die Datenbank, kein YNAB.
type HistoryPort = {
    LoadMovements: MovementFilter -> Async<Result<HistoryMovement list, string>>
    /// Kategorie und Monatserste (aufsteigend): die Netto-Aktivität der Monate mit Buchungen.
    LoadMonthlyActivity: Guid -> DateTime list -> Async<(DateTime * Money) list>
    /// Kategorie und Monat ("YYYY-MM-01"): die Pause-Ereignisse, neueste zuerst (budgeting-t6p8z).
    LoadPauseEvents: Guid -> string -> Async<Result<HistoryEntry list, string>>
    /// Kategorie und Monat: die Target-Änderungen, neueste zuerst (budgeting-k8wq3).
    LoadTargetChanges: Guid -> string -> Async<Result<HistoryTargetChange list, string>>
}

/// Die Bewegungen zum Filter. Grundlage der Historie je Kategorie und der allgemeinen (budgeting-h7q4v).
let movements (port: HistoryPort) (filter: MovementFilter) : Async<Result<HistoryMovement list, string>> =
    async {
        try
            return! port.LoadMovements filter
        with ex ->
            logReadFailure "Budget-Bewegungen lesen" ex
            return Error "Budget-Bewegungen nicht lesbar"
    }

/// Der Tab Historie für Kategorie und Monat (Monatserster): die Bewegungen, Pause-Ereignisse und
/// Target-Änderungen der
/// Kategorie im Monat, gemischt und neueste zuerst, und die Ausgaben der 6 Monate bis einschließlich
/// dieses Monats.
let categoryHistory (port: HistoryPort) (categoryId: Guid) (firstDay: DateTime) : Async<Result<CategoryHistory, string>> =
    async {
        let monthKey = monthKeyOf firstDay
        let filter : MovementFilter =
            { CategoryId = Some categoryId; Month = Some monthKey; Origin = None }
        match! movements port filter with
        | Error message -> return Error message
        | Ok found ->
            let! pauses =
                async {
                    try
                        return! port.LoadPauseEvents categoryId monthKey
                    with ex ->
                        logReadFailure "Pause-Ereignisse lesen" ex
                        return Error Budgeting.Persistence.PauseEventsUnreadableText
                }
            let! changes =
                async {
                    try
                        return! port.LoadTargetChanges categoryId monthKey
                    with ex ->
                        logReadFailure "Target-Änderungen lesen" ex
                        return Error Budgeting.Persistence.TargetChangesUnreadableText
                }
            match pauses, changes with
            | Error message, _
            | _, Error message -> return Error message
            | Ok pauses, Ok changes ->
                let months = spendingMonths firstDay
                let! sums =
                    async {
                        try
                            let! sums = port.LoadMonthlyActivity categoryId months
                            return Ok sums
                        with ex ->
                            logReadFailure "Ausgaben aus dem Kassenbuch lesen" ex
                            return Error "Ausgaben aus dem Kassenbuch nicht lesbar"
                    }
                // Das Zusammenführen steht außerhalb des `try`: ein Fehler darin ist kein Lesefehler.
                match sums with
                | Error message -> return Error message
                | Ok sums ->
                    let entries = mergeHistory found pauses changes
                    return Ok { Entries = entries; Spending = spendingOf months sums }
    }

/// Die allgemeine Historie (budgeting-h7q4v): alle Bewegungen aller Monate und Kategorien, neueste
/// zuerst. Zu Durchläufen fasst sie der Client zusammen.
let budgetHistory (port: HistoryPort) : Async<Result<HistoryMovement list, string>> =
    movements port { CategoryId = None; Month = None; Origin = None }

/// Die echten Lese-Quellen: Budget-Bewegungen und Kassenbuch (Lese-Naht ledger an budgeting).
let liveHistoryPort : HistoryPort =
    { LoadMovements = Budgeting.Persistence.loadMovements
      LoadPauseEvents = Budgeting.Persistence.loadPauseEvents
      LoadTargetChanges = Budgeting.Persistence.loadTargetChanges
      LoadMonthlyActivity =
        fun categoryId months -> Ledger.QueryHandlers.getCategoryMonthlyActivity (YnabCategoryId categoryId) months }

// ============================================
// Targets prüfen (budgeting-p2zr6, ADR 0082)
// ============================================

/// Die Lese-Quellen von Targets prüfen: der laufende Monat aus YNAB, die Monatssummen aller sichtbaren
/// Kategorien aus dem Kassenbuch in einer Abfrage und die letzte Target-Änderung je Kategorie. Kein Schreiben.
type TargetCheckPort = {
    FetchMonth: string -> Async<YnabResult<YnabMonth>>
    /// Kategorien und Monatserste (aufsteigend): je Kategorie die Netto-Aktivität der Monate mit Buchungen.
    LoadMonthlyActivity: Guid list -> DateTime list -> Async<Map<Guid, (DateTime * Money) list>>
    /// Je Kategorie die jüngste geschriebene Target-Änderung.
    LoadLastTargetChanges: unit -> Async<Result<Map<Guid, TargetChangeMark>, string>>
}

/// Die Meldung, wenn die Ausgaben aus dem Kassenbuch nicht lesbar sind (wie im Tab Historie).
[<Literal>]
let SpendingUnreadableText = "Ausgaben aus dem Kassenbuch nicht lesbar"

/// Die Rohdaten von Targets prüfen für den laufenden Monat (`today`, Berliner Kalendertag): die sichtbaren
/// Kategorien aus YNAB (dieselbe Filterung wie die Budget-Seite), die 12 vollen Monate vor dem laufenden aus
/// einer Kassenbuch-Abfrage und die letzte Änderung je Kategorie. Jeder Lesefehler kommt als `Error` mit Text.
let targetCheck (port: TargetCheckPort) (today: DateOnly) : Async<Result<TargetCheckData, string>> =
    async {
        let monthKey = currentMonthKey today
        match! budgetMonthOf port.FetchMonth monthKey with
        | Error e -> return Error (ynabErrorText e)
        | Ok month ->
            let categories =
                month.Groups |> List.collect (fun g -> g.Categories |> List.map (fun c -> g.Name, c))
            let months = targetCheckMonths (today.ToDateTime TimeOnly.MinValue)
            let ids = categories |> List.map (fun (_, c) -> c.Id)
            let! sums =
                async {
                    try
                        let! sums = port.LoadMonthlyActivity ids months
                        return Ok sums
                    with ex ->
                        logReadFailure "Ausgaben aus dem Kassenbuch lesen" ex
                        return Error SpendingUnreadableText
                }
            let! marks =
                async {
                    try
                        return! port.LoadLastTargetChanges ()
                    with ex ->
                        logReadFailure "Letzte Target-Änderungen lesen" ex
                        return Error Budgeting.Persistence.TargetChangesUnreadableText
                }
            match sums, marks with
            | Error message, _
            | _, Error message -> return Error message
            | Ok sums, Ok marks ->
                let sources : TargetCheckSource list =
                    categories
                    |> List.map (fun (groupName, c) ->
                        { CategoryId = c.Id
                          Name = c.Name
                          GroupName = groupName
                          Target = c.Target
                          Spending = spendingOf months (sums |> Map.tryFind c.Id |> Option.defaultValue [])
                          LastChange = marks |> Map.tryFind c.Id })
                return
                    Ok
                        { Month = monthKey
                          Today = today.ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
                          Categories = sources }
    }

/// Die echten Lese-Quellen von Targets prüfen: der Monat über `fetchMonth` (YNAB), Kassenbuch und
/// `target_changes` aus der Datenbank.
let liveTargetCheckPort (fetchMonth: string -> Async<YnabResult<YnabMonth>>) : TargetCheckPort =
    { FetchMonth = fetchMonth
      LoadMonthlyActivity = Ledger.QueryHandlers.getMonthlyActivityByCategory
      LoadLastTargetChanges = Budgeting.Persistence.loadLastTargetChanges }
