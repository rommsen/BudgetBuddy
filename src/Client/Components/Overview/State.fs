module Components.Overview.State

open Elmish
open Client
open Types
open Components.Overview.Types

#if FABLE_COMPILER
[<Fable.Core.Emit("new Date().toLocaleDateString('en-CA', { timeZone: 'Europe/Berlin' })")>]
let private berlinDateText () : string = Fable.Core.Util.jsNative
#endif

/// Der heutige Kalendertag in Europe/Berlin, unabhängig von der Zeitzone des Geräts. Unter .NET (Tests) die
/// Ortszeit, die Tests laufen mit `TZ=Europe/Berlin`.
let berlinToday () : System.DateTime =
#if FABLE_COMPILER
    match System.DateTime.TryParse(berlinDateText ()) with
    | true, day -> day.Date
    | _ -> System.DateTime.Today
#else
    System.DateTime.Today
#endif

let private loadAccountsCmd () : Cmd<Msg> =
    Cmd.batch [
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getLedgerAccounts)
            ()
            (Ok >> AccountsLoaded)
            (fun ex -> AccountsLoaded (Error ex.Message))
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getLedgerAccountBalances)
            ()
            (Ok >> BalancesLoaded)
            (fun ex -> BalancesLoaded (Error ex.Message))
    ]

/// Geladene Werte bleiben stehen, bis neue da sind: sonst flackern Seitenleiste und Monat beim Betreten.
let private reloading (rd: RemoteData<'a>) : RemoteData<'a> =
    match rd with
    | Success _ -> rd
    | _ -> Loading

/// Der reine Teil von `Entered` und `SidebarNeedsAccounts`: welche Stränge laden.
let beginEnter (today: System.DateTime) (model: Model) : Model =
    { model with
        Today = today
        Month = reloading model.Month
        Recent = reloading model.Recent
        Accounts = reloading model.Accounts
        Balances = reloading model.Balances }

let beginAccounts (model: Model) : Model =
    { model with Accounts = reloading model.Accounts; Balances = reloading model.Balances }

/// Die Antworten, rein. Ein spätes Scheitern ersetzt keinen geladenen Stand.
let applyAnswer (msg: Msg) (model: Model) : Model =
    match msg with
    | MonthLoaded (Ok month) -> { model with Month = Success month }
    | MonthLoaded (Error message) -> { model with Month = RemoteData.lateFailure message model.Month }
    | RecentLoaded (Ok entries) -> { model with Recent = Success entries }
    | RecentLoaded (Error message) -> { model with Recent = RemoteData.lateFailure message model.Recent }
    | AccountsLoaded (Ok accounts) -> { model with Accounts = Success accounts }
    | AccountsLoaded (Error message) -> { model with Accounts = RemoteData.lateFailure message model.Accounts }
    | BalancesLoaded (Ok balances) -> { model with Balances = Success balances }
    | BalancesLoaded (Error message) -> { model with Balances = RemoteData.lateFailure message model.Balances }
    | Entered _
    | ReloadRequested
    | SidebarNeedsAccounts -> model

let rec update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | ReloadRequested -> update (Entered model.Today) model
    | Entered today ->
        let monthCmd =
            Cmd.OfAsync.either
                (Retry.reading Api.budget.getBudgetMonth)
                (monthKey today)
                MonthLoaded
                (fun ex -> MonthLoaded (Error ex.Message))
        let recentCmd =
            Cmd.OfAsync.either
                (Retry.reading Api.ledger.getLedgerEntriesPage)
                recentQuery
                (fun page -> RecentLoaded (Ok page.Entries))
                (fun ex -> RecentLoaded (Error ex.Message))
        beginEnter today model, Cmd.batch [ monthCmd; recentCmd; loadAccountsCmd () ]
    | SidebarNeedsAccounts -> beginAccounts model, loadAccountsCmd ()
    | answer -> applyAnswer answer model, Cmd.none
