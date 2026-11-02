/// Abfragen des Budgets (budgeting-g8s3w, ADR 0074). YNAB kommt als Port herein, die Befehle in
/// `CommandHandlers` brauchen den Rahmen nicht und bleiben davon unberührt.
module Server.Budgeting.QueryHandlers

open System
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain

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
