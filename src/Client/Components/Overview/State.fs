module Components.Overview.State

open System
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

/// Die angepinnten Kategorien (budgeting-w9yt4), nur BBs Datenbank.
let private pinsCmd () : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.budget.getPinnedCategories)
        ()
        PinsLoaded
        (fun ex -> PinsLoaded (Error ex.Message))

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
        Balances = reloading model.Balances
        Pins = reloading model.Pins
        PinSheet = None }

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
    | PinsLoaded (Ok pins) -> { model with Pins = Success pins }
    | PinsLoaded (Error message) -> { model with Pins = RemoteData.lateFailure message model.Pins }
    | PinsNeeded
    | PinToggled _
    | PinMovedUp _
    | PinsSaved _
    | PinSheetOpened
    | PinSheetClosed
    | PinSearchChanged _
    | PinMarkMoved _
    | PinMarkedToggled
    | CategoryOrderLoaded _
    | Entered _
    | ReloadRequested
    | SidebarNeedsAccounts
    | CountsNeeded -> model

/// Was ein Schritt an den Pins außer dem Zustand auslöst. Die Cmds baut `update`: sie berühren den Remoting-Proxy.
type PinEffect =
    | NoPinEffect
    /// Die ganze Liste speichern, mit Nummer der Anfrage und dem Stand davor für den Rücksprung.
    | SavePins of request: int * previous: Guid list * pins: Guid list
    /// BBs Reihenfolge der Kategorien holen (erstes Öffnen des Sheets).
    | LoadCategoryOrder

/// Ändert die Pins sofort in der Anzeige und speichert die ganze Liste. Ohne geladene Pins geschieht nichts: eine
/// Liste aus dem Nichts überschriebe die gespeicherte.
let private changePins (change: Guid list -> Guid list) (model: Model) : Model * PinEffect =
    match model.Pins with
    | Success pins ->
        let next = change pins
        if next = pins then
            model, NoPinEffect
        else
            let request = model.PinsRequest + 1
            { model with Pins = Success next; PinsRequest = request }, SavePins (request, pins, next)
    | _ -> model, NoPinEffect

/// Die Zeilen des offenen Sheets.
let private sheetRows (model: Model) : Shared.Budgeting.BudgetCategory list =
    match model.PinSheet, model.Month with
    | Some sheet, Success month -> pinSheetRows model.CategoryOrder sheet.Query month
    | _ -> []

/// Der reine Teil der Pins: Anpinnen, Lösen, Ordnen, das Sheet und die Antworten. Scheitert die jüngste
/// Speicher-Anfrage, springt die Anzeige auf den Stand davor zurück; eine überholte ändert nichts.
let pinStep (msg: Msg) (model: Model) : Model * PinEffect =
    match msg with
    | PinToggled categoryId -> changePins (togglePin categoryId) model
    | PinMovedUp categoryId ->
        match model.Month, model.Pins with
        | Success month, Success pins ->
            let shown = visiblePins pins month |> List.map (fun c -> c.Id) |> Set.ofList
            changePins (movePinUp categoryId shown) model
        | _ -> model, NoPinEffect
    | PinsSaved (request, previous, Error _) when request = model.PinsRequest -> { model with Pins = Success previous }, NoPinEffect
    | PinsSaved _ -> model, NoPinEffect
    | PinSheetOpened ->
        { model with PinSheet = Some { Query = ""; Marked = 0 } },
        (if model.CategoryOrder.IsNone then LoadCategoryOrder else NoPinEffect)
    | PinSheetClosed -> { model with PinSheet = None }, NoPinEffect
    | PinSearchChanged query ->
        { model with PinSheet = model.PinSheet |> Option.map (fun _ -> { Query = query; Marked = 0 }) }, NoPinEffect
    | PinMarkMoved delta ->
        let count = (sheetRows model).Length
        { model with
            PinSheet =
                model.PinSheet
                |> Option.map (fun sheet -> { sheet with Marked = max 0 (min (count - 1) (sheet.Marked + delta)) }) },
        NoPinEffect
    | PinMarkedToggled ->
        match model.PinSheet with
        | Some sheet ->
            match sheetRows model |> List.tryItem sheet.Marked with
            | Some category -> changePins (togglePin category.Id) model
            | None -> model, NoPinEffect
        | None -> model, NoPinEffect
    | CategoryOrderLoaded (Ok frame) -> { model with CategoryOrder = Some frame.CategoryOrder }, NoPinEffect
    // Ohne BBs Reihenfolge gilt die Folge des Monats; beim nächsten Öffnen fragt das Sheet erneut.
    | CategoryOrderLoaded (Error _) -> model, NoPinEffect
    | _ -> applyAnswer msg model, NoPinEffect

/// Die Rückmeldung zu einer Antwort auf das Speichern der Pins, gemessen am Modell vor der Antwort: nur die jüngste
/// gescheiterte Anfrage meldet sich, eine überholte nicht.
let pinSaveNotice (msg: Msg) (model: Model) : string option =
    match msg with
    | PinsSaved (request, _, Error reason) when request = model.PinsRequest -> Some (pinsNotSavedText reason)
    | _ -> None

let private pinEffectCmd (effect: PinEffect) : Cmd<Msg> =
    match effect with
    | NoPinEffect -> Cmd.none
    | SavePins (request, previous, pins) ->
        Cmd.OfAsync.either
            (fun ids -> Api.budget.setPinnedCategories ids)
            pins
            (fun result -> PinsSaved (request, previous, result))
            (fun ex -> PinsSaved (request, previous, Error ex.Message))
    | LoadCategoryOrder ->
        Cmd.OfAsync.either
            (Retry.reading Api.budget.getBudgetFrame)
            ()
            CategoryOrderLoaded
            (fun ex -> CategoryOrderLoaded (Error ex.Message))

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
        Cmd.batch [ monthCmd; recentCmd entered.RecentRequest; todoSourcesCmd entered.NotInYnabRequest; loadAccountsCmd (); pinsCmd () ]
    | SidebarNeedsAccounts -> beginAccounts model, loadAccountsCmd ()
    | CountsNeeded ->
        let counting = beginCounts model
        counting, recentCmd counting.RecentRequest
    | PinsNeeded -> { model with Pins = reloading model.Pins }, pinsCmd ()
    | PinToggled _
    | PinMovedUp _
    | PinsSaved _
    | PinSheetOpened
    | PinSheetClosed
    | PinSearchChanged _
    | PinMarkMoved _
    | PinMarkedToggled
    | CategoryOrderLoaded _ ->
        let stepped, effect = pinStep msg model
        stepped, pinEffectCmd effect
    | answer -> applyAnswer answer model, Cmd.none
