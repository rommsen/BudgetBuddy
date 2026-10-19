/// Der Reduzierer der Kassenbuch-Seite (ledger-q5w9c, seitenweise Liste und
/// Kontostaende-Leiste ledger-b1xwe).
///
/// Alle Zustandsaenderungen laufen durch `update`, alle Seiteneffekte durch `Cmd`.
/// Die Seite blockiert nie hinter dem Spiegel: waehrend eines Laufs bleibt die
/// vorhandene Liste stehen und wird erst danach neu geladen (ADR 0014).
module Components.Ledger.State

open System
open Elmish
open Shared.Domain
open Components.Ledger.Types
open Types
open Client

module DateField = Client.DesignSystem.DateField

/// Ein frisches, leeres Kassenbuch. Geladen wird beim Betreten der Seite
/// (`UrlChanged` im Haupt-State), nicht beim Start der App: das Kassenbuch soll
/// keine Abfrage ausloesen, solange niemand hineinschaut.
let init () : Model * Cmd<Msg> =
    let model = {
        Accounts = NotAsked
        Balances = NotAsked
        SelectedFilter = None
        Entries = NotAsked
        NextPage = 0
        LocallyRemoved = 0
        TotalCount = 0
        TotalAmount = { Amount = 0m; Currency = "EUR" }
        HasMoreEntries = false
        LoadingMore = false
        IncludeDeleted = false
        DeletedCount = 0
        OnlyNeedsReview = false
        NeedsReviewCount = 0
        HistoryFor = None
        History = NotAsked
        LastMirrorRun = NotAsked
        MirrorRunning = false
        Categories = NotAsked
        Filters = noFilters
        OpenPopover = None
        CategoryDraft = Set.empty
        CategorySearch = ""
        HiddenCategoriesExpanded = false
        DateDraft = emptyDateRange
        DateDraftFromInvalid = false
        DateDraftToInvalid = false
        PayeeSuggestions = NotAsked
        PayeeSearch = ""
        MemoDraft = ""
        MemoVersion = 0
        AmountMinDraft = ""
        AmountMaxDraft = ""
        AmountVersion = 0
        ReviewMarkRows = Map.empty
        SwipedAway = Map.empty
        Selection = NoSelection
        SelectionAnchor = None
        SelectionMode = false
        FocusedRow = None
        ReviewMarkBatchRunning = false
    }
    model, Cmd.none

/// Der Zustand einer frisch angestossenen Buchungsliste: Seite 0, nichts geladen.
/// Von `LoadEntries` und `SelectFilter` geteilt: beide starten die Liste neu. Eine neue
/// Liste ist ein neuer Treffer: die Auswahl bezog sich auf den alten und endet mit ihm
/// (Konto-, Filter- und Chipwechsel laufen alle hierdurch, ledger-r1d2f).
let private resetEntries (model: Model) : Model = {
    model with
        Entries = Loading
        NextPage = 0
        LocallyRemoved = 0
        TotalCount = 0
        TotalAmount = { model.TotalAmount with Amount = 0m }
        HasMoreEntries = false
        LoadingMore = false
        SwipedAway = Map.empty
        Selection = NoSelection
        SelectionAnchor = None
        SelectionMode = false
}

/// Die Seitenanfrage an den Server. Rein, damit der Versatz testbar ist. Die fünf
/// Such-Filter (Kategorie/Zeitraum/Payee/Betrag/Memo, ledger-vgfw3) sind Server-seitig
/// da (ADR 0036). `LocallyRemoved` stammt aus dem Modell: jede Aktion, die Zeilen lokal
/// aus der Liste nimmt, zählt dort hoch, und die Anfrage gleicht den Versatz damit aus
/// (ledger-be1yh), sonst fehlt die Buchung an der Seitengrenze.
let buildPageQuery (model: Model) (accountId: Guid option) (page: int) : LedgerEntryPageQuery = {
    AccountId = accountId
    Page = page
    PageSize = pageSize
    LocallyRemoved = model.LocallyRemoved
    IncludeDeleted = model.IncludeDeleted
    OnlyNeedsReview = model.OnlyNeedsReview
    WithoutCategory = model.Filters.WithoutCategory
    CategoryIds = model.Filters.CategoryIds |> Set.toList
    DateFrom = model.Filters.DateRange |> Option.bind (fun d -> d.From)
    DateTo = model.Filters.DateRange |> Option.bind (fun d -> d.To)
    Payee = model.Filters.Payee
    Memo = model.Filters.Memo
    AmountMin = model.Filters.AmountMin
    AmountMax = model.Filters.AmountMax
}

/// Trägt das Ergebnis eines Prüfvermerk-Befehls in die geladene Liste ein (ledger-r777c):
/// setzt `NeedsReview` (wenn `needsReview` gesetzt ist) und den offenen Fehlschlag der
/// Zeile, passt den Zähler "Zu prüfen (n)" an und nimmt die Zeile aus der Liste, wenn der
/// Chip "Zu prüfen" aktiv ist und sie nicht mehr zu prüfen ist. Der Zeilenzustand
/// (Laden/Fehler) wird dabei zurückgesetzt. Rein, ohne Proxy.
let private applyReviewMarkResult
    (entryId: Guid)
    (needsReview: bool option)
    (failureOf: LedgerEntryView -> string option)
    (model: Model)
    : Model =
    let cleared = { model with ReviewMarkRows = model.ReviewMarkRows |> Map.remove entryId }

    match cleared.Entries with
    | Success entries ->
        match entries |> List.tryFind (fun e -> e.Id = entryId) with
        | None -> cleared
        | Some entry ->
            let newNeedsReview = needsReview |> Option.defaultValue entry.NeedsReview
            let updated = { entry with NeedsReview = newNeedsReview; ReviewMarkWriteBackFailure = failureOf entry }
            let countDelta =
                match entry.NeedsReview, newNeedsReview with
                | true, false -> -1
                | false, true -> 1
                | _ -> 0
            // Eine Zeile mit offenem Fehlschlag bleibt stehen, auch wenn sie nicht mehr zu
            // prüfen ist: sie trägt den Hinweis "Prüfvermerk nicht in YNAB". Der Zähler
            // folgt trotzdem dem BB-Stand (ADR 0050, Punkt 11).
            let leavesList = cleared.OnlyNeedsReview && not newNeedsReview && updated.ReviewMarkWriteBackFailure.IsNone
            let remaining =
                if leavesList then entries |> List.filter (fun e -> e.Id <> entryId)
                else entries |> List.map (fun e -> if e.Id = entryId then updated else e)
            let totalCount = if leavesList then max 0 (cleared.TotalCount - 1) else cleared.TotalCount
            let totalAmount =
                if leavesList && not entry.DeletedInYnab then
                    { cleared.TotalAmount with Amount = cleared.TotalAmount.Amount - entry.Amount.Amount }
                else
                    cleared.TotalAmount
            { cleared with
                Entries = Success remaining
                NeedsReviewCount = max 0 (cleared.NeedsReviewCount + countDelta)
                TotalCount = totalCount
                TotalAmount = totalAmount
                LocallyRemoved = (if leavesList then cleared.LocallyRemoved + 1 else cleared.LocallyRemoved)
                // Eine Zeile, die die Liste verlässt, verlässt auch die Auswahl (ledger-r1d2f).
                Selection = (if leavesList then removeFromSelection entryId cleared.Selection else cleared.Selection)
                SelectionAnchor = (if leavesList && cleared.SelectionAnchor = Some entryId then None else cleared.SelectionAnchor)
                // Liegt der Fokus auf der Zeile, die geht, rückt er auf die nach (ledger-rjb8w).
                FocusedRow = (if leavesList then focusAfterRemoval entries entryId cleared.FocusedRow else cleared.FocusedRow)
                HasMoreEntries = List.length remaining < totalCount }
    | _ -> cleared

/// Der offene Fehlschlag der Zeile nach einem Prüfvermerk-Befehl: der Grund aus der
/// Antwort, `None` nach erfolgreichem Versand; ohne Änderung und ohne Versand bleibt ein
/// bisheriger Fehlschlag stehen.
let private writeBackFailureOf (outcome: ReviewMarkOutcome) (entry: LedgerEntryView) : string option =
    match outcome.WriteBack with
    | ReviewMarkSendFailed reason -> Some reason
    | ReviewMarkSent -> None
    | ReviewMarkNotSent -> if outcome.Change = ReviewMarkUnchanged then entry.ReviewMarkWriteBackFailure else None

/// Ergebnis der Wisch-Geste (ledger-th36h): trägt den Prüfvermerk wie die Schaltfläche
/// ein und merkt sich eine Zeile, die dadurch die Liste verlässt, samt Platz, damit
/// "Rückgängig" sie zurückholen kann. Rein, ohne Proxy.
let private applySwipeReviewed (entryId: Guid) (outcome: ReviewMarkOutcome) (model: Model) : Model =
    let before =
        match model.Entries with
        | Success entries ->
            entries |> List.tryFindIndex (fun e -> e.Id = entryId) |> Option.map (fun i -> i, List.item i entries)
        | _ -> None
    let applied = applyReviewMarkResult entryId (Some false) (writeBackFailureOf outcome) model
    match before, applied.Entries with
    | Some (index, entry), Success remaining when not (remaining |> List.exists (fun e -> e.Id = entryId)) ->
        { applied with SwipedAway = applied.SwipedAway |> Map.add entryId (entry, index) }
    | _ -> applied

/// Setzt eine von der Geste entfernte Zeile an ihren Platz zurück und rechnet Anzahl,
/// Summe und Zähler "Zu prüfen" nach. Ohne gemerkte Zeile (die Liste wurde neu geladen)
/// bleibt das Modell, wie es ist. Rein.
let private reinsertSwipedAway (entryId: Guid) (model: Model) : Model =
    match model.SwipedAway |> Map.tryFind entryId, model.Entries with
    | Some (entry, index), Success entries when not (entries |> List.exists (fun e -> e.Id = entryId)) ->
        let at = min index (List.length entries)
        let before, after = List.splitAt at entries
        let restored = before @ [ entry ] @ after
        let totalCount = model.TotalCount + 1
        // Die Zeile ist wieder in der Liste: der Seitenversatz darf sie nicht mehr
        // abziehen, sonst lädt die nächste Seite diese Buchung doppelt (ledger-be1yh).
        { model with
            LocallyRemoved = max 0 (model.LocallyRemoved - 1)
            Entries = Success restored
            SwipedAway = model.SwipedAway |> Map.remove entryId
            TotalCount = totalCount
            TotalAmount =
                if entry.DeletedInYnab then model.TotalAmount
                else { model.TotalAmount with Amount = model.TotalAmount.Amount + entry.Amount.Amount }
            NeedsReviewCount = model.NeedsReviewCount + (if entry.NeedsReview then 1 else 0)
            HasMoreEntries = List.length restored < totalCount }
    | _ -> model

/// Ergebnis von "Rückgängig": holt die Zeile zurück, falls die Geste sie aus der Liste
/// nahm, und setzt dann den Vorzustand ein, den der Client mitgeschickt hat. Der Zähler
/// "Zu prüfen" folgt dem BB-Stand. Rein.
let private applyUndoReviewMark
    (entryId: Guid)
    (previous: ReviewMarkState)
    (outcome: ReviewMarkOutcome)
    (model: Model)
    : Model =
    let reinserted = reinsertSwipedAway entryId model
    applyReviewMarkResult entryId (Some (needsReviewOf previous)) (writeBackFailureOf outcome) reinserted

/// Die Auswahl als Stapel-Auftrag an den Server (ledger-nhwrt). `Picked`: die Ids der
/// gewählten Zeilen. `AllExcept`: der ganze Treffer als Seitenanfrage (derselbe Filter wie
/// die Liste, Seite 0, ohne lokalen Versatz) plus die abgewählten Ids; der Server löst ihn
/// auf, weil der Client die Ids nicht geladener Buchungen nicht kennt. `None` ohne Auswahl.
let reviewMarkBatchSelection (model: Model) : ReviewMarkBatchSelection option =
    let accountId =
        match model.SelectedFilter with
        | Some (OneAccount id) -> Some id
        | _ -> None
    match model.Selection with
    | NoSelection -> None
    | Picked ids when Set.isEmpty ids -> None
    | Picked ids -> Some (BatchPicked (Set.toList ids))
    | AllExcept excluded ->
        let query = { buildPageQuery model accountId 0 with LocallyRemoved = 0 }
        Some (BatchAllExcept (query, Set.toList excluded))

/// Trägt das Ergebnis von "Geprüft" für die Auswahl in die geladene Liste ein: die Auswahl
/// endet (samt Auswahl-Modus), jede geänderte geladene Zeile ist nicht mehr zu prüfen und
/// trägt ihren Fehlschlag, der Zähler "Zu prüfen" sinkt um die Zahl der geänderten
/// Buchungen. Bei aktivem Chip verlassen die Zeilen ohne Fehlschlag die Liste
/// (`LocallyRemoved` steigt um ihre Zahl, Treffer und Summe sinken). Das zweite Ergebnis
/// sagt, ob die Liste neu zu laden ist: bei aktivem Chip waren geänderte Buchungen nie
/// geladen (`AllExcept`), der Versatz der nächsten Seite stimmt dann nur nach dem Neuladen.
/// Rein, ohne Proxy.
let private applyReviewMarkBatch (outcome: ReviewMarkBatchOutcome) (model: Model) : Model * bool =
    let changedCount = List.length outcome.Changed
    let ended = {
        model with
            Selection = NoSelection
            SelectionAnchor = None
            SelectionMode = false
            ReviewMarkBatchRunning = false
            NeedsReviewCount = max 0 (model.NeedsReviewCount - changedCount)
    }
    match ended.Entries with
    | Success entries ->
        let changeById = outcome.Changed |> List.map (fun c -> c.EntryId, c) |> Map.ofList
        let failureOf (c: ReviewMarkBatchChange) =
            match c.WriteBack with
            | ReviewMarkSendFailed reason -> Some reason
            | _ -> None
        let updated =
            entries
            |> List.map (fun e ->
                match changeById |> Map.tryFind e.Id with
                | Some c -> { e with NeedsReview = false; ReviewMarkWriteBackFailure = failureOf c }
                | None -> e)
        let loadedChanged = entries |> List.filter (fun e -> changeById |> Map.containsKey e.Id) |> List.length
        let unloadedChanged = changedCount - loadedChanged
        let leaving =
            if ended.OnlyNeedsReview then
                updated |> List.filter (fun e -> changeById |> Map.containsKey e.Id && e.ReviewMarkWriteBackFailure.IsNone)
            else
                []
        let leavingIds = leaving |> List.map (fun e -> e.Id) |> Set.ofList
        let remaining = updated |> List.filter (fun e -> not (leavingIds.Contains e.Id))
        let leavingAmount = leaving |> List.filter (fun e -> not e.DeletedInYnab) |> List.sumBy (fun e -> e.Amount.Amount)
        let focus =
            leaving
            |> List.fold (fun (state: Guid option * LedgerEntryView list) e ->
                let focus', list = state
                focusAfterRemoval list e.Id focus', list |> List.filter (fun x -> x.Id <> e.Id))
                (ended.FocusedRow, entries)
            |> fst
        let totalCount =
            if ended.OnlyNeedsReview then max 0 (ended.TotalCount - List.length leaving - unloadedChanged)
            else ended.TotalCount
        let model' = {
            ended with
                Entries = Success remaining
                TotalCount = totalCount
                TotalAmount = { ended.TotalAmount with Amount = ended.TotalAmount.Amount - leavingAmount }
                LocallyRemoved = ended.LocallyRemoved + List.length leaving
                FocusedRow = focus
                HasMoreEntries = List.length remaining < totalCount
        }
        model', (ended.OnlyNeedsReview && unloadedChanged > 0)
    | _ -> ended, false

/// Sind die Tasten der Liste gerade aus? Ein offenes Filter-Popover oder die Historie
/// gehören der Tastatur.
let private keyboardBlocked (model: Model) : bool =
    model.OpenPopover.IsSome || model.HistoryFor.IsSome

/// Nimmt die Zeile den Prüfvermerk per Taste nicht an? In der Auswahl, ohne Schaltfläche
/// (in YNAB gelöscht) und während ein Aufruf läuft.
let private reviewKeyBlocked (model: Model) (entry: LedgerEntryView) : bool =
    model.SelectionMode
    || hasSelection model
    || entry.DeletedInYnab
    || (model.ReviewMarkRows |> Map.tryFind entry.Id) = Some ReviewMarkSending

let rec update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with

    // ============================================
    // Konten
    // ============================================
    | LoadAccounts ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.getLedgerAccounts
                ()
                (Ok >> AccountsLoaded)
                (fun ex -> AccountsLoaded (Error ex.Message))
        { model with Accounts = Loading }, cmd, NoOp

    | AccountsLoaded (Ok accounts) ->
        // Die Vorauswahl kommt vom Server (YNAB-Standardkonto, sonst das erste) —
        // die Seite entscheidet das nicht selbst. Eine bereits getroffene Auswahl
        // (auch "Alle Konten") ueberlebt ein Neuladen, solange das gewaehlte Konto
        // noch existiert: sonst springt die Seite nach jedem Spiegel-Lauf zurueck
        // auf das Standardkonto, waehrend Roman einen anderen Filter ansieht.
        let filter =
            match model.SelectedFilter with
            | Some AllAccounts -> Some AllAccounts
            | Some (OneAccount id) when accounts.Accounts |> List.exists (fun a -> a.Id = id) ->
                Some (OneAccount id)
            | _ -> accounts.DefaultAccountId |> Option.map OneAccount
        let model' = { model with Accounts = Success accounts; SelectedFilter = filter }
        match filter with
        | Some _ -> resetEntries model', Cmd.ofMsg LoadEntries, NoOp
        | None -> { model' with Entries = NotAsked }, Cmd.none, NoOp

    | AccountsLoaded (Error message) ->
        { model with Accounts = Failure message }, Cmd.none, NoOp

    // ============================================
    // Kontostaende
    // ============================================
    | LoadBalances ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.getLedgerAccountBalances
                ()
                (Ok >> BalancesLoaded)
                (fun ex -> BalancesLoaded (Error ex.Message))
        { model with Balances = Loading }, cmd, NoOp

    | BalancesLoaded (Ok balances) ->
        { model with Balances = Success balances }, Cmd.none, NoOp

    | BalancesLoaded (Error message) ->
        { model with Balances = Failure message }, Cmd.none, NoOp

    // ============================================
    // Konto-Filter
    // ============================================
    | SelectFilter filter ->
        // Ein Filterwechsel schliesst ein offenes Historie-Sheet: es gehoerte zu
        // einer Buchung, die gleich nicht mehr in der Liste steht. Die
        // Scroll-Position wird bewusst NICHT gemerkt (Task-Notes) — alle anderen
        // Zustaende (Kontostaende, Lauf-Status) bleiben unangetastet stehen.
        { (resetEntries model) with
            SelectedFilter = Some filter
            HistoryFor = None
            History = NotAsked },
        Cmd.ofMsg LoadEntries,
        NoOp

    // ============================================
    // Buchungen, seitenweise
    // ============================================
    | LoadEntries -> resetEntries model, Cmd.ofMsg (RunLoadEntriesPage 0), NoOp

    | RunLoadEntriesPage page ->
        match model.SelectedFilter with
        | None -> model, Cmd.none, NoOp
        | Some filter ->
            let accountId = match filter with AllAccounts -> None | OneAccount id -> Some id
            let query = buildPageQuery model accountId page
            let cmd =
                Cmd.OfAsync.either
                    Api.ledger.getLedgerEntriesPage
                    query
                    (fun result -> EntriesPageLoaded (page, Ok result))
                    (fun ex -> EntriesPageLoaded (page, Error ex.Message))
            model, cmd, NoOp

    | EntriesPageLoaded (page, Ok entryPage) ->
        let entries =
            if page = 0 then
                entryPage.Entries
            else
                (model.Entries |> RemoteData.withDefault []) @ entryPage.Entries
        { model with
            Entries = Success entries
            // Der Fokus überlebt das Nachladen; fällt seine Zeile aus der Liste, springt er
            // auf die erste (ledger-rjb8w).
            FocusedRow = reconcileFocus entries model.FocusedRow
            NextPage = page + 1
            TotalCount = entryPage.TotalCount
            TotalAmount = entryPage.TotalAmount
            HasMoreEntries = List.length entries < entryPage.TotalCount
            DeletedCount = entryPage.DeletedCount
            NeedsReviewCount = entryPage.NeedsReviewCount
            LoadingMore = false },
        Cmd.none,
        NoOp

    | EntriesPageLoaded (0, Error message) ->
        // Der Fehler bleibt an der Liste stehen (ErrorDisplay), statt als Toast
        // wegzublenden — die Seite waere sonst still leer.
        { model with Entries = Failure message; LoadingMore = false }, Cmd.none, NoOp

    | EntriesPageLoaded (_, Error message) ->
        // Ein Fehler beim Nachladen loescht die bereits sichtbare Liste nicht: sie
        // bleibt stehen, nur der Hinweis erscheint als Toast.
        { model with LoadingMore = false },
        Cmd.none,
        ShowToast ($"Weitere Buchungen konnten nicht geladen werden: {message}", ToastError)

    | LoadMoreEntries ->
        // Reiner Wächter: kein zweiter Nachlade-Aufruf waehrend einer laeuft, keiner
        // ohne weitere Seiten, keiner ohne eine erfolgreich geladene erste Seite.
        if model.LoadingMore || not model.HasMoreEntries || not (RemoteData.isSuccess model.Entries) then
            model, Cmd.none, NoOp
        else
            { model with LoadingMore = true }, Cmd.ofMsg (RunLoadEntriesPage model.NextPage), NoOp

    | ToggleIncludeDeleted ->
        // Reine Zustandsänderung plus `Cmd.ofMsg LoadEntries` (dieselbe Teilung wie
        // beim Filterwechsel `SelectFilter`): der Proxy-Aufruf steckt erst hinter
        // `RunLoadEntriesPage`, nicht in diesem Zweig (ADR 0020).
        { (resetEntries model) with IncludeDeleted = not model.IncludeDeleted }, Cmd.ofMsg LoadEntries, NoOp

    | ToggleOnlyNeedsReview ->
        // Reine Zustandsänderung plus `Cmd.ofMsg LoadEntries` (derselbe Schnitt wie
        // `ToggleIncludeDeleted`/ADR 0020): der Proxy-Aufruf steckt erst hinter
        // `RunLoadEntriesPage`, nicht in diesem Zweig.
        { (resetEntries model) with OnlyNeedsReview = not model.OnlyNeedsReview }, Cmd.ofMsg LoadEntries, NoOp

    | ToggleWithoutCategory ->
        // Wie `ToggleOnlyNeedsReview`: reine Zustandsänderung plus `LoadEntries` (ADR 0020).
        let filters = { model.Filters with WithoutCategory = not model.Filters.WithoutCategory }
        { (resetEntries model) with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    // ============================================
    // Historie einer Buchung
    // ============================================
    | OpenHistory entry ->
        { model with HistoryFor = Some entry; History = Loading },
        Cmd.ofMsg (LoadHistory entry.Id),
        NoOp

    | CloseHistory ->
        { model with HistoryFor = None; History = NotAsked }, Cmd.none, NoOp

    | LoadHistory entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.getEntryHistory
                entryId
                (Ok >> HistoryLoaded)
                (fun ex -> HistoryLoaded (Error ex.Message))
        { model with History = Loading }, cmd, NoOp

    | HistoryLoaded (Ok lines) ->
        { model with History = Success lines }, Cmd.none, NoOp

    | HistoryLoaded (Error message) ->
        { model with History = Failure message }, Cmd.none, NoOp

    // ============================================
    // Spiegel
    // ============================================
    | LoadMirrorStatus ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.getLastMirrorRun
                ()
                (Ok >> MirrorStatusLoaded)
                (fun ex -> MirrorStatusLoaded (Error ex.Message))
        { model with LastMirrorRun = Loading }, cmd, NoOp

    | MirrorStatusLoaded (Ok status) ->
        // Frische-Kriterium (ADR 0014): ist der geladene Lauf-Status älter als die
        // Schwelle (oder gab es noch nie einen), stößt dieselbe Seite denselben
        // Hintergrund-Lauf an wie der manuelle Knopf, nur ohne den zusätzlichen
        // Nutzer-Klick. Läuft bereits ein Lauf, greift dieselbe Doppeldruck-Sperre wie
        // bei `StartMirror`: kein zweiter Auslöser aus diesem Tab. Der serverseitige
        // Einzel-Flug-Schutz (`Ledger.CommandHandlers.runMirrorGuarded`) hält zusätzlich
        // gegen einen zweiten Tab oder einen zeitgleichen Lauf-nach-Push.
        let model' = { model with LastMirrorRun = Success status }
        if not model.MirrorRunning && isMirrorStale DateTime.UtcNow status then
            { model' with MirrorRunning = true }, Cmd.ofMsg RunMirror, NoOp
        else
            model', Cmd.none, NoOp

    | MirrorStatusLoaded (Error message) ->
        { model with LastMirrorRun = Failure message }, Cmd.none, NoOp

    | StartMirror ->
        // Doppeldruck-Sperre: ein zweiter Druck waehrend eines Laufs tut nichts.
        // Die Liste bleibt stehen — sie wird erst nach dem Lauf neu geladen.
        if model.MirrorRunning then
            model, Cmd.none, NoOp
        else
            { model with MirrorRunning = true }, Cmd.ofMsg RunMirror, NoOp

    | RunMirror ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.mirrorYnabBudget
                ()
                MirrorFinished
                (fun ex -> MirrorFinished (Error ex.Message))
        model, cmd, NoOp

    | MirrorFinished (Ok summary) ->
        // Konten und Kontostaende neu laden, nicht nur die Buchungen: der erste Lauf
        // auf einem leeren Kassenbuch legt die Konten ueberhaupt erst an, und ein
        // spaeterer Lauf kann ein in YNAB neu angelegtes Konto oder geaenderte
        // Staende mitbringen. `AccountsLoaded` zieht die Buchungen des gewaehlten
        // Filters selbst nach (Regression ledger-kt0wq).
        { model with MirrorRunning = false },
        Cmd.batch [ Cmd.ofMsg LoadAccounts; Cmd.ofMsg LoadBalances; Cmd.ofMsg LoadMirrorStatus ],
        ShowToast (mirrorSummaryText summary, ToastSuccess)

    | MirrorFinished (Error message) ->
        // Ein Fehler wird sichtbar gemacht, nie still geschluckt (ADR 0014): als
        // Toast jetzt und als Lauf-Status dauerhaft.
        { model with MirrorRunning = false },
        Cmd.ofMsg LoadMirrorStatus,
        ShowToast ($"Spiegel-Lauf gescheitert: {message}", ToastError)

    // ============================================
    // Filterleiste und Summenzeile (ledger-j8xq4)
    // ============================================

    | LoadCategories ->
        // Der eigentliche Aufruf braucht das konfigurierte YNAB-Budget und läuft
        // deshalb im Haupt-`State.fs` (analog zu `Components.Rules.State`s
        // `LoadCategories`), nicht hinter dem Proxy dieses Moduls.
        { model with Categories = Loading }, Cmd.none, NoOp

    | CategoriesLoaded (Ok categories) ->
        { model with Categories = Success categories }, Cmd.none, NoOp

    | CategoriesLoaded (Error message) ->
        { model with Categories = Failure message }, Cmd.none, NoOp

    | OpenFilterPopover chip ->
        let model' =
            match chip with
            | CategoryChip ->
                { model with
                    CategoryDraft = model.Filters.CategoryIds
                    CategorySearch = ""
                    HiddenCategoriesExpanded = false }
            | DateChip ->
                { model with
                    DateDraft = model.Filters.DateRange |> Option.defaultValue emptyDateRange
                    DateDraftFromInvalid = false
                    DateDraftToInvalid = false }
            | PayeeChip ->
                { model with PayeeSearch = "" }
            | MemoChip ->
                { model with MemoDraft = model.Filters.Memo |> Option.defaultValue "" }
            | AmountChip ->
                { model with
                    AmountMinDraft = model.Filters.AmountMin |> Option.map formatAmountDraft |> Option.defaultValue ""
                    AmountMaxDraft = model.Filters.AmountMax |> Option.map formatAmountDraft |> Option.defaultValue "" }
            | NeedsReviewChip -> model
            // Ein Umschalter ohne Popover (ADR 0048): es gibt keinen Entwurf zu setzen.
            | WithoutCategoryChip -> model
        let cmd = if chip = PayeeChip then Cmd.ofMsg LoadPayeeSuggestions else Cmd.none
        { model' with OpenPopover = Some chip }, cmd, NoOp

    | CloseFilterPopover ->
        // Esc, Klick daneben oder "Schließen": der Entwurf wird verworfen, `Filters`
        // bleibt unangetastet (AC "der vorherige Filterzustand bleibt unverändert").
        { model with OpenPopover = None }, Cmd.none, NoOp

    // -------- Kategorie (Mehrfachauswahl, Click-Commit) --------

    | SetCategorySearch text ->
        { model with CategorySearch = text }, Cmd.none, NoOp

    | ToggleCategoryDraft categoryId ->
        let draft =
            if model.CategoryDraft.Contains categoryId then Set.remove categoryId model.CategoryDraft
            else Set.add categoryId model.CategoryDraft
        { model with CategoryDraft = draft }, Cmd.none, NoOp

    | ToggleHiddenCategoriesExpanded ->
        { model with HiddenCategoriesExpanded = not model.HiddenCategoriesExpanded }, Cmd.none, NoOp

    | ResetCategoryDraft ->
        { model with CategoryDraft = Set.empty }, Cmd.none, NoOp

    | ApplyCategoryFilter ->
        let filters = { model.Filters with CategoryIds = model.CategoryDraft }
        resetEntries { model with Filters = filters; OpenPopover = None }, Cmd.ofMsg LoadEntries, NoOp

    | RemoveCategoryFilter ->
        let filters = { model.Filters with CategoryIds = Set.empty }
        resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    // -------- Zeitraum (Schnellwahl schreibt Von/Bis mit) --------

    | SetDateQuick quick ->
        let from_, to_ = quickDateRange DateTime.Today quick
        { model with
            DateDraft = { Quick = Some quick; From = Some from_; To = Some to_ }
            DateDraftFromInvalid = false
            DateDraftToInvalid = false }, Cmd.none, NoOp

    | SetDateDraftFrom entry ->
        let draft = { model.DateDraft with Quick = None }
        match entry with
        | DateField.ValidDate d ->
            { model with DateDraft = { draft with From = Some d }; DateDraftFromInvalid = false }, Cmd.none, NoOp
        | DateField.EmptyDate ->
            { model with DateDraft = { draft with From = None }; DateDraftFromInvalid = false }, Cmd.none, NoOp
        | DateField.InvalidDate _ ->
            { model with DateDraft = draft; DateDraftFromInvalid = true }, Cmd.none, NoOp

    | SetDateDraftTo entry ->
        let draft = { model.DateDraft with Quick = None }
        match entry with
        | DateField.ValidDate d ->
            { model with DateDraft = { draft with To = Some d }; DateDraftToInvalid = false }, Cmd.none, NoOp
        | DateField.EmptyDate ->
            { model with DateDraft = { draft with To = None }; DateDraftToInvalid = false }, Cmd.none, NoOp
        | DateField.InvalidDate _ ->
            { model with DateDraft = draft; DateDraftToInvalid = true }, Cmd.none, NoOp

    | ResetDateDraft ->
        { model with DateDraft = emptyDateRange; DateDraftFromInvalid = false; DateDraftToInvalid = false }, Cmd.none, NoOp

    | ApplyDateFilter when dateDraftHasError model ->
        // Ungültige Eingabe in Von oder Bis: der Filter bleibt, wie er ist.
        model, Cmd.none, NoOp

    | ApplyDateFilter ->
        let range =
            if model.DateDraft.From.IsNone && model.DateDraft.To.IsNone then None
            else Some model.DateDraft
        let filters = { model.Filters with DateRange = range }
        resetEntries { model with Filters = filters; OpenPopover = None }, Cmd.ofMsg LoadEntries, NoOp

    | RemoveDateFilter ->
        let filters = { model.Filters with DateRange = None }
        resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    // -------- Payee (Einzelauswahl, filtert sofort) --------

    | SetPayeeSearch text ->
        { model with PayeeSearch = text }, Cmd.none, NoOp

    | LoadPayeeSuggestions ->
        match model.SelectedFilter with
        | None -> { model with PayeeSuggestions = Success [] }, Cmd.none, NoOp
        | Some filter ->
            let accountId = match filter with AllAccounts -> None | OneAccount id -> Some id
            let query : LedgerPayeeSuggestionQuery = {
                AccountId = accountId
                IncludeDeleted = model.IncludeDeleted
                OnlyNeedsReview = model.OnlyNeedsReview
                WithoutCategory = model.Filters.WithoutCategory
                CategoryIds = model.Filters.CategoryIds |> Set.toList
                DateFrom = model.Filters.DateRange |> Option.bind (fun d -> d.From)
                DateTo = model.Filters.DateRange |> Option.bind (fun d -> d.To)
                Memo = model.Filters.Memo
                AmountMin = model.Filters.AmountMin
                AmountMax = model.Filters.AmountMax
            }
            let cmd =
                Cmd.OfAsync.either
                    Api.ledger.getLedgerPayeeSuggestions
                    query
                    (Ok >> PayeeSuggestionsLoaded)
                    (fun ex -> PayeeSuggestionsLoaded (Error ex.Message))
            { model with PayeeSuggestions = Loading }, cmd, NoOp

    | PayeeSuggestionsLoaded (Ok suggestions) ->
        { model with PayeeSuggestions = Success suggestions }, Cmd.none, NoOp

    | PayeeSuggestionsLoaded (Error message) ->
        { model with PayeeSuggestions = Failure message }, Cmd.none, NoOp

    | SelectPayeeFilter payee ->
        let filters = { model.Filters with Payee = Some payee }
        resetEntries { model with Filters = filters; OpenPopover = None }, Cmd.ofMsg LoadEntries, NoOp

    | RemovePayeeFilter ->
        let filters = { model.Filters with Payee = None }
        resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    // -------- Memo (Suchfeld mit Verzögerung) --------

    | SetMemoDraft text ->
        let newVersion = model.MemoVersion + 1
        { model with MemoDraft = text; MemoVersion = newVersion },
        Debounce.delayedDefault (CommitMemoFilter (text, newVersion)),
        NoOp

    | CommitMemoFilter (text, version) ->
        if version <> model.MemoVersion then
            // Eine spätere Eingabe hat diese Verzögerung schon überholt.
            model, Cmd.none, NoOp
        else
            let trimmed = text.Trim()
            let filters = { model.Filters with Memo = (if trimmed = "" then None else Some trimmed) }
            resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    | RemoveMemoFilter ->
        let filters = { model.Filters with Memo = None }
        resetEntries { model with Filters = filters; MemoDraft = "" }, Cmd.ofMsg LoadEntries, NoOp

    // -------- Betrag (Suchfeld mit Verzögerung, Von/Bis) --------

    | SetAmountMinDraft text ->
        let newVersion = model.AmountVersion + 1
        { model with AmountMinDraft = text; AmountVersion = newVersion },
        Debounce.delayedDefault (CommitAmountFilter (parseAmountFilterText text, parseAmountFilterText model.AmountMaxDraft, newVersion)),
        NoOp

    | SetAmountMaxDraft text ->
        let newVersion = model.AmountVersion + 1
        { model with AmountMaxDraft = text; AmountVersion = newVersion },
        Debounce.delayedDefault (CommitAmountFilter (parseAmountFilterText model.AmountMinDraft, parseAmountFilterText text, newVersion)),
        NoOp

    | CommitAmountFilter (min_, max_, version) ->
        if version <> model.AmountVersion then
            model, Cmd.none, NoOp
        else
            let filters = { model.Filters with AmountMin = min_; AmountMax = max_ }
            resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    | RemoveAmountFilter ->
        let filters = { model.Filters with AmountMin = None; AmountMax = None }
        resetEntries { model with Filters = filters; AmountMinDraft = ""; AmountMaxDraft = "" }, Cmd.ofMsg LoadEntries, NoOp

    | ClearAllFilters ->
        // Der Chip "Ohne Kategorie" bleibt stehen (wie "Zu prüfen"): er gehört nicht zu
        // den fünf Such-Filtern (ADR 0048).
        resetEntries { model with Filters = { noFilters with WithoutCategory = model.Filters.WithoutCategory } },
        Cmd.ofMsg LoadEntries,
        NoOp

    // ============================================
    // Prüfvermerk setzen und zurückschreiben (ledger-r777c, ADR 0050)
    // ============================================

    | StartSetReviewMark (entryId, target) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
        Cmd.ofMsg (RunSetReviewMark (entryId, target)),
        NoOp

    | RunSetReviewMark (entryId, target) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setReviewMark entryId)
                target
                (fun result -> ReviewMarkSet (entryId, target, result))
                (fun ex -> ReviewMarkSet (entryId, target, Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkSet (entryId, target, Ok outcome) ->
        // Nach "Geprüft" ist nichts mehr zu prüfen (approved, cleared mindestens bestätigt),
        // nach "Ungeprüft" ist die Buchung wieder zu prüfen (approved = false). Ziel gleich
        // Ist ändert daran nichts. Der Fehlschlag kommt aus der Antwort; ohne Änderung und
        // ohne Versand bleibt ein offener Fehlschlag stehen.
        let needsReview = Some (target = Unreviewed)
        applyReviewMarkResult entryId needsReview (writeBackFailureOf outcome) model, Cmd.none, NoOp

    | ReviewMarkSet (entryId, _, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        NoOp

    | StartRetryReviewMark entryId ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
        Cmd.ofMsg (RunRetryReviewMark entryId),
        NoOp

    | RunRetryReviewMark entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.retryReviewMarkWriteBack
                entryId
                (fun result -> ReviewMarkRetried (entryId, result))
                (fun ex -> ReviewMarkRetried (entryId, Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkRetried (entryId, Ok writeBack) ->
        let failureOf (_: LedgerEntryView) =
            match writeBack with
            | ReviewMarkSendFailed reason -> Some reason
            | ReviewMarkSent | ReviewMarkNotSent -> None
        applyReviewMarkResult entryId None failureOf model, Cmd.none, NoOp

    | ReviewMarkRetried (entryId, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        NoOp

    // ============================================
    // Wisch nach rechts: geprüft, mit Rückgängig (ledger-th36h)
    // ============================================

    | StartSwipeReviewed entryId ->
        // Nur eine zu prüfende Zeile, die nicht schon sendet. Eine geprüfte Zeile bleibt
        // bei `NoChange`: kein Aufruf, kein Toast.
        let entry =
            match model.Entries with
            | Success entries -> entries |> List.tryFind (fun e -> e.Id = entryId)
            | _ -> None
        match entry with
        | Some e when swipeAppliesTo e && (model.ReviewMarkRows |> Map.tryFind entryId) <> Some ReviewMarkSending ->
            { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
            Cmd.ofMsg (RunSwipeReviewed entryId),
            NoOp
        | _ -> model, Cmd.none, NoOp

    | RunSwipeReviewed entryId ->
        let payee =
            match model.Entries with
            | Success entries -> entries |> List.tryFind (fun e -> e.Id = entryId) |> Option.bind (fun e -> e.Payee)
            | _ -> None
        let text = swipeReviewedToastText payee
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setReviewMark entryId)
                Reviewed
                (fun result -> SwipeReviewedDone (entryId, text, result))
                (fun ex -> SwipeReviewedDone (entryId, text, Error ex.Message))
        model, cmd, NoOp

    | SwipeReviewedDone (entryId, text, Ok outcome) ->
        let model' = applySwipeReviewed entryId outcome model
        let external =
            match outcome.WriteBack with
            | ReviewMarkSendFailed reason -> ShowToast ($"{reviewMarkHintTitle}: {reason}", ToastError)
            | _ when outcome.Change = ReviewMarkUnchanged -> NoOp
            | _ ->
                ShowToastWithAction (
                    text,
                    ToastSuccess,
                    { Label = undoToastLabel; Kind = UndoReviewMark (entryId, outcome.Previous) }
                )
        model', Cmd.none, external

    | SwipeReviewedDone (entryId, _, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        ShowToast ($"Prüfvermerk nicht gesetzt: {message}", ToastError)

    | StartUndoReviewMark (entryId, previous) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
        Cmd.ofMsg (RunUndoReviewMark (entryId, previous)),
        NoOp

    | RunUndoReviewMark (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.restoreReviewMark entryId)
                previous
                (fun result -> ReviewMarkRestored (entryId, previous, result))
                (fun ex -> ReviewMarkRestored (entryId, previous, Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkRestored (entryId, previous, Ok outcome) ->
        let model' = applyUndoReviewMark entryId previous outcome model
        let external =
            match outcome.WriteBack with
            | ReviewMarkSendFailed reason -> ShowToast ($"{reviewMarkHintTitle}: {reason}", ToastError)
            | _ -> NoOp
        model', Cmd.none, external

    | ReviewMarkRestored (entryId, _, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    // ============================================
    // Auswahl mit Summe (ledger-r1d2f)
    // ============================================

    | StartSelectionMode entryId ->
        { model with
            SelectionMode = true
            Selection = Picked (Set.singleton entryId)
            SelectionAnchor = None },
        Cmd.none,
        NoOp

    | ToggleRowSelected entryId ->
        // Abwählen der letzten Zeile beendet den Modus nicht: nur "Auswahl aufheben" tut das.
        { model with Selection = toggleSelected entryId model.Selection }, Cmd.none, NoOp

    | ClickRowCheckbox (entryId, shift) ->
        let entries = model.Entries |> RemoteData.withDefault []
        let span =
            match shift, model.SelectionAnchor with
            | true, Some anchor -> rangeBetween entries anchor entryId
            | _ -> []
        if List.isEmpty span then
            // Einfacher Klick (oder Shift ohne brauchbaren Anker): wählt aus bzw. ab, setzt den Anker.
            { model with
                Selection = toggleSelected entryId model.Selection
                SelectionAnchor = Some entryId },
            Cmd.none,
            NoOp
        else
            { model with Selection = addToSelection span model.Selection }, Cmd.none, NoOp

    | ToggleSelectAll ->
        let selection = if isAllSelected model.Selection then NoSelection else AllExcept Set.empty
        { model with Selection = selection; SelectionAnchor = None }, Cmd.none, NoOp

    | ToggleHeaderCheckbox ->
        let selection =
            match headerCheckState model with
            | HeaderChecked -> NoSelection
            | HeaderEmpty | HeaderPartial -> AllExcept Set.empty
        { model with Selection = selection; SelectionAnchor = None }, Cmd.none, NoOp

    | ClearSelection ->
        { model with Selection = NoSelection; SelectionAnchor = None; SelectionMode = false }, Cmd.none, NoOp

    // ============================================
    // "Geprüft" für die ganze Auswahl (ledger-nhwrt)
    // ============================================

    | StartReviewMarkBatch ->
        // Nur mit Auswahl und ohne laufenden Aufruf; der Proxy-Aufruf steckt hinter `Run...`.
        match reviewMarkBatchSelection model with
        | Some selection when not model.ReviewMarkBatchRunning ->
            { model with ReviewMarkBatchRunning = true }, Cmd.ofMsg (RunReviewMarkBatch selection), NoOp
        | _ -> model, Cmd.none, NoOp

    | RunReviewMarkBatch selection ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.setReviewMarkBatch
                selection
                ReviewMarkBatchDone
                (fun ex -> ReviewMarkBatchDone (Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkBatchDone (Ok outcome) ->
        let model', reload = applyReviewMarkBatch outcome model
        let toastType = reviewBatchToastType outcome
        let text = reviewBatchToastText outcome
        let external =
            if List.isEmpty outcome.Changed then
                ShowToast (text, toastType)
            else
                let items =
                    outcome.Changed |> List.map (fun c -> { EntryId = c.EntryId; Previous = c.Previous })
                ShowToastWithAction (text, toastType, { Label = undoToastLabel; Kind = UndoReviewMarkBatch items })
        model', (if reload then Cmd.ofMsg LoadEntries else Cmd.none), external

    | ReviewMarkBatchDone (Error message) ->
        { model with ReviewMarkBatchRunning = false },
        Cmd.none,
        ShowToast ($"Prüfvermerk nicht gesetzt: {message}", ToastError)

    | StartUndoReviewMarkBatch items -> model, Cmd.ofMsg (RunUndoReviewMarkBatch items), NoOp

    | RunUndoReviewMarkBatch items ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.restoreReviewMarkBatch
                items
                ReviewMarkBatchRestored
                (fun ex -> ReviewMarkBatchRestored (Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkBatchRestored (Ok outcome) ->
        // Viele der Buchungen waren unter `AllExcept` nie geladen: die Liste lädt neu, statt
        // Zeilen einzeln zurückzustecken. Das setzt auch Seitenversatz und Zähler auf den
        // Stand des Servers.
        let sendFailed =
            outcome.Changed
            |> List.tryPick (fun c ->
                match c.WriteBack with
                | ReviewMarkSendFailed reason -> Some reason
                | _ -> None)
        let external =
            match sendFailed with
            | Some reason -> ShowToast ($"{reviewMarkHintTitle}: {reason}", ToastError)
            | None -> NoOp
        resetEntries model, Cmd.ofMsg LoadEntries, external

    | ReviewMarkBatchRestored (Error message) ->
        model, Cmd.none, ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    // ============================================
    // Tastaturfokus und Taste G (ledger-rjb8w, PC)
    // ============================================

    | MoveFocus delta ->
        if keyboardBlocked model then
            model, Cmd.none, NoOp
        else
            let entries = model.Entries |> RemoteData.withDefault []
            { model with FocusedRow = movedFocus entries model.FocusedRow delta }, Cmd.none, NoOp

    | ReviewKeyPressed target ->
        let entries = model.Entries |> RemoteData.withDefault []
        let focused =
            model.FocusedRow
            |> Option.bind (fun id -> entries |> List.tryFind (fun e -> e.Id = id))
        match focused with
        // Im Auswahl-Modus ist G aus: unklar, ob es die Fokuszeile oder die Auswahl meint.
        | Some entry when not (keyboardBlocked model) && not (reviewKeyBlocked model entry) ->
            // Derselbe Weg wie der Klick auf die Schaltfläche (Fehlschlag-Hinweis inbegriffen);
            // der Fokus springt sofort auf die nächste Zeile.
            update (StartSetReviewMark (entry.Id, target)) { model with FocusedRow = Some (nextFocusAfter entries entry.Id) }
        | _ -> model, Cmd.none, NoOp
