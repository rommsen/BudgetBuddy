/// Der Reduzierer der Kassenbuch-Seite (ledger-q5w9c, seitenweise Liste und
/// Kontostaende-Leiste ledger-b1xwe).
///
/// Alle Zustandsaenderungen laufen durch `update`, alle Seiteneffekte durch `Cmd`.
/// Die Seite blockiert nie hinter dem Spiegel: waehrend eines Laufs bleibt die
/// vorhandene Liste stehen und wird erst danach neu geladen (ADR 0014).
module Components.Ledger.State

open Elmish
open Shared.Domain
open Components.Ledger.Types
open Types

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
        HasMoreEntries = false
        LoadingMore = false
        HistoryFor = None
        History = NotAsked
        LastMirrorRun = NotAsked
        MirrorRunning = false
    }
    model, Cmd.none

/// Der Zustand einer frisch angestossenen Buchungsliste: Seite 0, nichts geladen.
/// Von `LoadEntries` und `SelectFilter` geteilt — beide starten die Liste neu.
let private resetEntries (model: Model) : Model = {
    model with
        Entries = Loading
        NextPage = 0
        TotalCount = 0
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
            let query : LedgerEntryPageQuery = { AccountId = accountId; Page = page; PageSize = pageSize }
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
            HasMoreEntries = List.length entries < entryPage.TotalCount
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
        { model with LastMirrorRun = Success status }, Cmd.none, NoOp

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
