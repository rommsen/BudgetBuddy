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

/// Die neuesten Buchungen samt den Zählern „zu prüfen“ und „Lena“ über alle Konten, als Anfrage `request`.
let private recentCmd (request: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.ledger.getLedgerEntriesPage)
        recentQuery
        (fun page -> RecentLoaded (request, Ok page))
        (fun ex -> RecentLoaded (request, Error ex.Message))

/// Was „Zu tun“ außer Monat und Zählern braucht: die Buchungen nicht in YNAB, den Stand des Imports und die
/// Lena-Farbe (ledger-s1weh). Die Sync-Historie bringt höchstens zehn Sitzungen.
let private todoSourcesCmd (notInYnabRequest: int) : Cmd<Msg> =
    Cmd.batch [
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getEntriesNotInYnab)
            ()
            (fun entries -> NotInYnabLoaded (notInYnabRequest, Ok entries))
            (fun ex -> NotInYnabLoaded (notInYnabRequest, Error ex.Message))
        Cmd.OfAsync.either
            (Retry.reading Api.sync.getSyncHistory)
            10
            (fun sessions -> SyncHistoryLoaded (System.DateTime.UtcNow, Ok sessions))
            (fun ex -> SyncHistoryLoaded (System.DateTime.UtcNow, Error ex.Message))
        Cmd.OfAsync.either
            (Retry.reading Api.settings.getLenaFlagSettings)
            ()
            (Ok >> LenaFlagLoaded)
            (fun ex -> LenaFlagLoaded (Error ex.Message))
    ]

/// Geladene Werte bleiben stehen, bis neue da sind: sonst flackern Seitenleiste und Monat beim Betreten.
let private reloading (rd: RemoteData<'a>) : RemoteData<'a> =
    match rd with
    | Success _ -> rd
    | _ -> Loading

/// Der reine Teil von `Entered`: welche Stränge laden. Neueste Buchungen und Buchungen nicht in YNAB bekommen eine
/// neue Anfrage-Nummer.
let beginEnter (today: System.DateTime) (model: Model) : Model =
    { model with
        Today = today
        Month = reloading model.Month
        Recent = reloading model.Recent
        RecentRequest = model.RecentRequest + 1
        NotInYnab = reloading model.NotInYnab
        NotInYnabRequest = model.NotInYnabRequest + 1
        Accounts = reloading model.Accounts
        Balances = reloading model.Balances }

/// Der reine Teil von `CountsNeeded`: nur die neuesten Buchungen samt Zählern, mit neuer Anfrage-Nummer.
let beginCounts (model: Model) : Model =
    { model with Recent = reloading model.Recent; RecentRequest = model.RecentRequest + 1 }

let beginAccounts (model: Model) : Model =
    { model with Accounts = reloading model.Accounts; Balances = reloading model.Balances }

/// Die Antworten, rein. Ein spätes Scheitern ersetzt keinen geladenen Stand. Eine Antwort auf eine überholte Anfrage
/// (Nummer älter als die jüngste) bleibt ohne Wirkung.
let applyAnswer (msg: Msg) (model: Model) : Model =
    match msg with
    | RecentLoaded (request, _) when request <> model.RecentRequest -> model
    | NotInYnabLoaded (request, _) when request <> model.NotInYnabRequest -> model
    | MonthLoaded (Ok month) -> { model with Month = Success month }
    | MonthLoaded (Error message) -> { model with Month = RemoteData.lateFailure message model.Month }
    | RecentLoaded (_, Ok page) ->
        { model with
            Recent = Success page.Entries
            NeedsReviewCount = page.NeedsReviewCount
            AskLenaCount = page.AskLenaCount }
    | RecentLoaded (_, Error message) -> { model with Recent = RemoteData.lateFailure message model.Recent }
    | AccountsLoaded (Ok accounts) -> { model with Accounts = Success accounts }
    | AccountsLoaded (Error message) -> { model with Accounts = RemoteData.lateFailure message model.Accounts }
    | BalancesLoaded (Ok balances) -> { model with Balances = Success balances }
    | BalancesLoaded (Error message) -> { model with Balances = RemoteData.lateFailure message model.Balances }
    | NotInYnabLoaded (_, Ok entries) -> { model with NotInYnab = Success entries }
    | NotInYnabLoaded (_, Error message) -> { model with NotInYnab = RemoteData.lateFailure message model.NotInYnab }
    | SyncHistoryLoaded (now, Ok sessions) ->
        { model with ImportStand = lastImportAt sessions |> Option.map (importStandText now) }
    // Ohne Historie fehlt nur der Stand in Zeile 2 von Eintrag 1.
    | SyncHistoryLoaded (_, Error _) -> model
    | LenaFlagLoaded (Ok settings) -> { model with LenaFlagColor = settings.SavedColor }
    | LenaFlagLoaded (Error _) -> model
    | Entered _
    | ReloadRequested
    | SidebarNeedsAccounts
    | CountsNeeded -> model

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
        let entered = beginEnter today model
        entered,
        Cmd.batch [ monthCmd; recentCmd entered.RecentRequest; todoSourcesCmd entered.NotInYnabRequest; loadAccountsCmd () ]
    | SidebarNeedsAccounts -> beginAccounts model, loadAccountsCmd ()
    | CountsNeeded ->
        let counting = beginCounts model
        counting, recentCmd counting.RecentRequest
    | answer -> applyAnswer answer model, Cmd.none
