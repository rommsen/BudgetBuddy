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
