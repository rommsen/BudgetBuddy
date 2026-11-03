/// Abfragen des Budgets (budgeting-g8s3w, ADR 0074). YNAB kommt als Port herein, die Befehle in
/// `CommandHandlers` brauchen den Rahmen nicht und bleiben davon unberührt.
module Server.Budgeting.QueryHandlers

open System
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain

/// Liest einen Monat für die Budget-Seite.
let budgetMonth (port: CommandHandlers.YnabPort) (monthKey: string) : Async<YnabResult<BudgetMonth>> =
    async {
        match! port.FetchMonth monthKey with
        | Error e -> return Error e
        | Ok month -> return Ok (toBudgetMonth month)
    }

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
        with _ ->
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
    LoadMovements: Budgeting.Persistence.MovementFilter -> Async<Result<HistoryMovement list, string>>
    /// Kategorie und Monatserste (aufsteigend): die Netto-Aktivität der Monate mit Buchungen.
    LoadMonthlyActivity: Guid -> DateTime list -> Async<(DateTime * Money) list>
}

/// Die Bewegungen zum Filter. Grundlage der Historie je Kategorie und der allgemeinen (budgeting-h7q4v).
let movements (port: HistoryPort) (filter: Budgeting.Persistence.MovementFilter) : Async<Result<HistoryMovement list, string>> =
    async {
        try
            return! port.LoadMovements filter
        with _ ->
            return Error "Budget-Bewegungen nicht lesbar"
    }

/// Der Tab Historie für Kategorie und Monat (Monatserster): die Bewegungen der Kategorie im Monat und
/// die Ausgaben der 6 Monate bis einschließlich dieses Monats.
let categoryHistory (port: HistoryPort) (categoryId: Guid) (firstDay: DateTime) : Async<Result<CategoryHistory, string>> =
    async {
        let filter : Budgeting.Persistence.MovementFilter =
            { CategoryId = Some categoryId; Month = Some (monthKeyOf firstDay); Origin = None }
        match! movements port filter with
        | Error message -> return Error message
        | Ok found ->
            let months = spendingMonths firstDay
            try
                let! sums = port.LoadMonthlyActivity categoryId months
                return Ok { Movements = found; Spending = spendingOf months sums }
            with _ ->
                return Error "Ausgaben aus dem Kassenbuch nicht lesbar"
    }

/// Die echten Lese-Quellen: Budget-Bewegungen und Kassenbuch (Lese-Naht ledger an budgeting).
let liveHistoryPort : HistoryPort =
    { LoadMovements = Budgeting.Persistence.loadMovements
      LoadMonthlyActivity =
        fun categoryId months -> Ledger.QueryHandlers.getCategoryMonthlyActivity (YnabCategoryId categoryId) months }
