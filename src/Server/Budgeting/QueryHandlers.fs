/// Abfragen des Budgets (budgeting-g8s3w, ADR 0074). YNAB kommt als Port herein, die Befehle in
/// `CommandHandlers` brauchen den Rahmen nicht und bleiben davon unberührt.
module Server.Budgeting.QueryHandlers

open System
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain

/// Die Ursache eines Lesefehlers bleibt im Server-Log, der Client bekommt nur den Text der Meldung.
let private logReadFailure (what: string) (ex: exn) : unit =
    eprintfn "%s gescheitert: %s" what ex.Message

/// Liest einen Monat für die Budget-Seite, mit den BB-Pausen des Monats (ADR 0079). Eine Pause gilt nur
/// für ihren Monat, der Folgemonat liest seine eigenen (ADR 0073 Punkt 3).
let budgetMonth (port: CommandHandlers.YnabPort) (monthKey: string) : Async<YnabResult<BudgetMonth>> =
    async {
        match! port.FetchMonth monthKey with
        | Error e -> return Error e
        | Ok month ->
            let! paused = Budgeting.Persistence.loadPausedCategories monthKey
            return Ok (toBudgetMonth paused month)
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
    LoadPauseEvents: Guid -> string -> Async<HistoryEntry list>
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

/// Der Tab Historie für Kategorie und Monat (Monatserster): die Bewegungen und Pause-Ereignisse der
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
                        let! events = port.LoadPauseEvents categoryId monthKey
                        return Ok events
                    with ex ->
                        logReadFailure "Pause-Ereignisse lesen" ex
                        return Error "Pause-Ereignisse nicht lesbar"
                }
            match pauses with
            | Error message -> return Error message
            | Ok pauses ->
                let months = spendingMonths firstDay
                try
                    let! sums = port.LoadMonthlyActivity categoryId months
                    let entries = mergeHistoryEntries found pauses
                    return Ok { Entries = entries; Spending = spendingOf months sums }
                with ex ->
                    logReadFailure "Ausgaben aus dem Kassenbuch lesen" ex
                    return Error "Ausgaben aus dem Kassenbuch nicht lesbar"
    }

/// Die echten Lese-Quellen: Budget-Bewegungen und Kassenbuch (Lese-Naht ledger an budgeting).
let liveHistoryPort : HistoryPort =
    { LoadMovements = Budgeting.Persistence.loadMovements
      LoadPauseEvents = Budgeting.Persistence.loadPauseEvents
      LoadMonthlyActivity =
        fun categoryId months -> Ledger.QueryHandlers.getCategoryMonthlyActivity (YnabCategoryId categoryId) months }
