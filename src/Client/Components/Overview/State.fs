module Components.Overview.State

open System
open Elmish
open Client
open Types
open Components.Overview.Types

/// Der heutige Kalendertag in Europe/Berlin (`Client.BerlinDate.today`).
let berlinToday () : System.DateTime = Client.BerlinDate.today ()

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

/// Die angepinnten Kategorien (budgeting-w9yt4), nur BBs Datenbank, als Lesen Nummer `read`.
let private pinsCmd (read: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.budget.getPinnedCategories)
        ()
        (fun result -> PinsLoaded (read, result))
        (fun ex -> PinsLoaded (read, Error ex.Message))

/// Geladene Werte bleiben stehen, bis neue da sind: sonst flackern Seitenleiste und Monat beim Betreten.
let private reloading (rd: RemoteData<'a>) : RemoteData<'a> =
    match rd with
    | Success _ -> rd
    | _ -> Loading

/// Der reine Teil von `PinsNeeded`: die Pins laden mit neuer Lese-Nummer.
let beginPins (model: Model) : Model =
    { model with Pins = reloading model.Pins; PinsRead = model.PinsRead + 1 }

/// Der reine Teil von `Entered`: welche Stränge laden. Neueste Buchungen, Buchungen nicht in YNAB und Pins bekommen
/// eine neue Anfrage-Nummer.
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
        PinsRead = model.PinsRead + 1
        PinSheet = None }

/// Der reine Teil von `ReloadRequested`: wie das Betreten, mit dem Tag von dort. Ein offenes Sheet „Angepinnt“ bleibt
/// offen: „Erneut versuchen“ steht auch darin.
let beginReload (model: Model) : Model =
    { beginEnter model.Today model with PinSheet = model.PinSheet }

/// Der reine Teil von `CountsNeeded`: nur die neuesten Buchungen samt Zählern, mit neuer Anfrage-Nummer.
let beginCounts (model: Model) : Model =
    { model with Recent = reloading model.Recent; RecentRequest = model.RecentRequest + 1 }

let beginAccounts (model: Model) : Model =
    { model with Accounts = reloading model.Accounts; Balances = reloading model.Balances }

/// Die Antworten, rein. Ein spätes Scheitern ersetzt keinen geladenen Stand. Eine Antwort auf eine überholte Anfrage
/// (Nummer älter als die jüngste) bleibt ohne Wirkung. Gelesene Pins zählen nicht, solange ein Speichern läuft: die
/// Antwort kann den Stand vor oder nach dem Speichern tragen.
let applyAnswer (msg: Msg) (model: Model) : Model =
    match msg with
    | RecentLoaded (request, _) when request <> model.RecentRequest -> model
    | NotInYnabLoaded (request, _) when request <> model.NotInYnabRequest -> model
    | PinsLoaded (read, _) when read <> model.PinsRead || model.PinsSaving.IsSome -> model
    | MonthLoaded (Ok month) -> { model with Month = Success month }
    | MonthLoaded (Error message) -> { model with Month = RemoteData.lateFailure message model.Month }
    // Das Nachziehen (budgeting-ffjk9) tauscht still, nur für den gezeigten Monat. Ein Fehlschlag lässt den Stand
    // stehen; die Übersicht zeigt dazu keinen Hinweis.
    | MonthSynced (monthKey, Ok (Shared.Budgeting.MonthChanged month)) ->
        let shown = match model.Month with Success (m: Shared.Budgeting.BudgetMonth) -> Some m.Month | _ -> None
        if shown = Some monthKey && month.Month = monthKey then { model with Month = Success month } else model
    | MonthSynced (_, Ok Shared.Budgeting.MonthUnchanged)
    | MonthSynced (_, Error _) -> model
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
    // Das erste Laden setzt den Vergleich fürs Leuchten: was schon angepinnt war, ist nicht neu.
    | PinsLoaded (_, Ok pins) ->
        { model with Pins = Success pins; PinsConfirmed = pins; PinsSeen = model.PinsSeen |> Option.orElse (Some pins) }
    | PinsLoaded (_, Error message) -> { model with Pins = RemoteData.lateFailure message model.Pins }
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
    | PinFlashEnded _
    | Entered _
    | ReloadRequested
    | SidebarNeedsAccounts
    | CountsNeeded -> model

/// Was ein Schritt an den Pins außer dem Zustand auslöst. Die Cmds baut `update`: sie berühren den Remoting-Proxy.
type PinEffect =
    | NoPinEffect
    /// Die ganze Liste speichern.
    | SavePins of pins: Guid list
    /// BBs Reihenfolge der Kategorien holen (erstes Öffnen des Sheets).
    | LoadCategoryOrder

/// Speichert die angezeigte Liste, wenn kein Speichern läuft und sie vom bestätigten Stand abweicht. Läuft eines, geht
/// die Liste nach dessen Antwort hinterher.
let private saveShown (model: Model) : Model * PinEffect =
    match model.PinsSaving, model.Pins with
    | None, Success pins when pins <> model.PinsConfirmed -> { model with PinsSaving = Some pins }, SavePins pins
    | _ -> model, NoPinEffect

/// Ändert die Pins sofort in der Anzeige und speichert die ganze Liste. Ein laufendes Lesen ist damit überholt. Ohne
/// geladene Pins geschieht nichts: eine Liste aus dem Nichts überschriebe die gespeicherte.
let private changePins (change: Guid list -> Guid list) (model: Model) : Model * PinEffect =
    match model.Pins with
    | Success pins ->
        let next = change pins
        if next = pins then model, NoPinEffect
        else saveShown { model with Pins = Success next; PinsRead = model.PinsRead + 1 }
    | _ -> model, NoPinEffect

/// Die Zeilen des offenen Sheets.
let private sheetRows (model: Model) : Shared.Budgeting.BudgetCategory list =
    match model.PinSheet, model.Month with
    | Some sheet, Success month -> pinSheetRows model.CategoryOrder sheet.Query month
    | _ -> []

/// Die Übersicht zeigt die Pins: was seit dem letzten Anzeigen neu angepinnt ist, leuchtet (budgeting-p3sk0). Ohne
/// geladene Pins bleibt alles, wie es ist.
let notePinsShown (model: Model) : Model =
    match model.Pins with
    | Success now ->
        let ids = model.PinsSeen |> Option.map (fun seen -> newlyPinned seen now) |> Option.defaultValue Set.empty
        { model with
            PinsSeen = Some now
            PinFlash =
                if ids.IsEmpty then { model.PinFlash with Ids = Set.empty }
                else { Ids = ids; Count = model.PinFlash.Count + 1 } }
    | _ -> model

/// Beendet die Nachricht den Satz „Nicht gespeichert: …“? Jede Aktion an den Pins: umschalten, ordnen, das Sheet öffnen
/// oder schließen, suchen. Die Pfeile der Markierung zeigen nur.
let private endsPinError (msg: Msg) : bool =
    match msg with
    | PinToggled _
    | PinMovedUp _
    | PinMarkedToggled
    | PinSheetOpened
    | PinSheetClosed
    | PinSearchChanged _ -> true
    | _ -> false

/// Der reine Teil der Pins: Anpinnen, Lösen, Ordnen, das Sheet und die Antworten. Scheitert das Speichern ohne neuere
/// Änderung, springt die Anzeige auf den bestätigten Stand zurück. Kam inzwischen eine Änderung, geht sie als ganze
/// Liste hinterher, ob das Speichern davor gelang oder nicht.
let pinStep (msg: Msg) (model: Model) : Model * PinEffect =
    let model = if endsPinError msg then { model with PinError = None } else model
    match msg with
    | PinToggled categoryId -> changePins (togglePin categoryId) model
    | PinMovedUp categoryId ->
        match model.Month, model.Pins with
        | Success month, Success pins ->
            let shown = visiblePins pins month |> List.map (fun c -> c.Id) |> Set.ofList
            changePins (movePinUp categoryId shown) model
        | _ -> model, NoPinEffect
    | PinsSaved result ->
        match model.PinsSaving with
        | None -> model, NoPinEffect
        | Some sent ->
            match result with
            | Ok () -> saveShown { model with PinsConfirmed = sent; PinsSaving = None }
            | Error reason when model.Pins = Success sent ->
                { model with
                    Pins = Success model.PinsConfirmed
                    PinsSaving = None
                    PinError = Some (pinsNotSavedText reason) },
                NoPinEffect
            | Error _ -> saveShown { model with PinsSaving = None }
    | PinSheetOpened ->
        { model with PinSheet = Some { Query = ""; Marked = 0 } },
        (if model.CategoryOrder.IsNone then LoadCategoryOrder else NoPinEffect)
    // Nach dem Schließen leuchtet, was im Sheet neu angepinnt wurde.
    | PinSheetClosed -> notePinsShown { model with PinSheet = None }, NoPinEffect
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
        match pinSheetMarked model with
        | Some category -> changePins (togglePin category.Id) model
        | None -> model, NoPinEffect
    | CategoryOrderLoaded (Ok frame) -> { model with CategoryOrder = Some frame.CategoryOrder }, NoPinEffect
    // Ohne BBs Reihenfolge gilt die Folge des Monats; beim nächsten Öffnen fragt das Sheet erneut.
    | CategoryOrderLoaded (Error _) -> model, NoPinEffect
    | PinFlashEnded count when count = model.PinFlash.Count -> { model with PinFlash = { model.PinFlash with Ids = Set.empty } }, NoPinEffect
    | PinFlashEnded _ -> model, NoPinEffect
    | _ -> applyAnswer msg model, NoPinEffect

/// Der Satz zu einer Antwort auf das Speichern der Pins, gemessen am Modell vor der Antwort: ein Scheitern meldet sich,
/// wenn die Anzeige zurückspringt. Wartet eine neuere Änderung, geht sie hinterher und meldet sich selbst. `pinStep`
/// setzt ihn in `PinError`; die Wurzel liest ihn hier, um ihn bei offenem Kategorie-Detail dorthin zu setzen.
let pinSaveNotice (msg: Msg) (model: Model) : string option =
    match msg, model.PinsSaving with
    | PinsSaved (Error reason), Some sent when model.Pins = Success sent -> Some (pinsNotSavedText reason)
    | _ -> None

let private pinEffectCmd (effect: PinEffect) : Cmd<Msg> =
    match effect with
    | NoPinEffect -> Cmd.none
    | SavePins pins ->
        Cmd.OfAsync.either
            (fun ids -> Api.budget.setPinnedCategories ids)
            pins
            PinsSaved
            (fun ex -> PinsSaved (Error ex.Message))
    | LoadCategoryOrder ->
        Cmd.OfAsync.either
            (Retry.reading Api.budget.getBudgetFrame)
            ()
            CategoryOrderLoaded
            (fun ex -> CategoryOrderLoaded (Error ex.Message))

/// Wie lange neu angepinnte Zeilen leuchten: 0,7 s Animation (`.feedback-flash`), danach nimmt die App die Klasse ab.
/// Mit `prefers-reduced-motion` steht der Ring so lange ohne Bewegung.
let pinFlashMs = 900

/// Der Zeitgeber, der ein neues Leuchten beendet.
let pinFlashCmd (count: int) : Cmd<Msg> = Client.Debounce.delayed pinFlashMs (PinFlashEnded count)

let private flashCmd (before: Model) (after: Model) : Cmd<Msg> =
    if after.PinFlash.Count <> before.PinFlash.Count then pinFlashCmd after.PinFlash.Count else Cmd.none

/// Was Betreten und „Erneut laden“ laden, für das Modell nach `beginEnter` oder `beginReload`.
let private enterCmd (entered: Model) : Cmd<Msg> =
    Cmd.batch [
        Cmd.OfAsync.either
            (Retry.reading Api.budget.getBudgetMonth)
            (monthKey entered.Today)
            MonthLoaded
            (fun ex -> MonthLoaded (Error ex.Message))
        recentCmd entered.RecentRequest
        todoSourcesCmd entered.NotInYnabRequest
        loadAccountsCmd ()
        pinsCmd entered.PinsRead
    ]

/// Das stille Nachziehen nach dem Lesen des Monats (budgeting-ffjk9), wie auf der Budget-Seite.
let private syncCmd (monthKey: string) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun key -> Retry.reading Api.budget.syncBudgetMonth key)
        monthKey
        (fun result -> MonthSynced (monthKey, result))
        (fun ex -> MonthSynced (monthKey, Error ex.Message))

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | MonthLoaded (Ok month) -> applyAnswer msg model, syncCmd month.Month
    | ReloadRequested ->
        let reloading = beginReload model
        reloading, enterCmd reloading
    | Entered today ->
        let entered = beginEnter today (notePinsShown model)
        entered, Cmd.batch [ enterCmd entered; flashCmd model entered ]
    | SidebarNeedsAccounts -> beginAccounts model, loadAccountsCmd ()
    | CountsNeeded ->
        let counting = beginCounts model
        counting, recentCmd counting.RecentRequest
    | PinsNeeded ->
        let reading = beginPins model
        reading, pinsCmd reading.PinsRead
    | PinToggled _
    | PinMovedUp _
    | PinsSaved _
    | PinSheetOpened
    | PinSheetClosed
    | PinSearchChanged _
    | PinMarkMoved _
    | PinMarkedToggled
    | CategoryOrderLoaded _
    | PinFlashEnded _ ->
        let stepped, effect = pinStep msg model
        stepped, Cmd.batch [ pinEffectCmd effect; flashCmd model stepped ]
    | answer -> applyAnswer answer model, Cmd.none
