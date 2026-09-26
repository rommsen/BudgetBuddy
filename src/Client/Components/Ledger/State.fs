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
        PayeeSuggestions = NotAsked
        PayeeSearch = ""
        MemoDraft = ""
        MemoVersion = 0
        AmountMinDraft = ""
        AmountMaxDraft = ""
        AmountVersion = 0
    }
    model, Cmd.none

/// Der Zustand einer frisch angestossenen Buchungsliste: Seite 0, nichts geladen.
/// Von `LoadEntries` und `SelectFilter` geteilt — beide starten die Liste neu.
let private resetEntries (model: Model) : Model = {
    model with
        Entries = Loading
        NextPage = 0
        TotalCount = 0
        TotalAmount = { model.TotalAmount with Amount = 0m }
        HasMoreEntries = false
        LoadingMore = false
}

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
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
            // Die fuenf Such-Filter (Kategorie/Zeitraum/Payee/Betrag/Memo, ledger-vgfw3)
            // sind Server-seitig da (ADR 0036); die Chipleiste, die sie setzt
            // (`model.Filters`), ist ledger-j8xq4.
            let query : LedgerEntryPageQuery = {
                AccountId = accountId
                Page = page
                PageSize = pageSize
                IncludeDeleted = model.IncludeDeleted
                OnlyNeedsReview = model.OnlyNeedsReview
                CategoryIds = model.Filters.CategoryIds |> Set.toList
                DateFrom = model.Filters.DateRange |> Option.bind (fun d -> d.From)
                DateTo = model.Filters.DateRange |> Option.bind (fun d -> d.To)
                Payee = model.Filters.Payee
                Memo = model.Filters.Memo
                AmountMin = model.Filters.AmountMin
                AmountMax = model.Filters.AmountMax
            }
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
                { model with DateDraft = model.Filters.DateRange |> Option.defaultValue emptyDateRange }
            | PayeeChip ->
                { model with PayeeSearch = "" }
            | MemoChip ->
                { model with MemoDraft = model.Filters.Memo |> Option.defaultValue "" }
            | AmountChip ->
                { model with
                    AmountMinDraft = model.Filters.AmountMin |> Option.map formatAmountDraft |> Option.defaultValue ""
                    AmountMaxDraft = model.Filters.AmountMax |> Option.map formatAmountDraft |> Option.defaultValue "" }
            | NeedsReviewChip -> model
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
        { model with DateDraft = { Quick = Some quick; From = Some from_; To = Some to_ } }, Cmd.none, NoOp

    | SetDateDraftFrom from_ ->
        { model with DateDraft = { model.DateDraft with Quick = None; From = from_ } }, Cmd.none, NoOp

    | SetDateDraftTo to_ ->
        { model with DateDraft = { model.DateDraft with Quick = None; To = to_ } }, Cmd.none, NoOp

    | ResetDateDraft ->
        { model with DateDraft = emptyDateRange }, Cmd.none, NoOp

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
        resetEntries { model with Filters = noFilters }, Cmd.ofMsg LoadEntries, NoOp
