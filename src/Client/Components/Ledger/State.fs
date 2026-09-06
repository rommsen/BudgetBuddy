/// Der Reduzierer der Kassenbuch-Seite (ledger-q5w9c).
///
/// Alle Zustandsaenderungen laufen durch `update`, alle Seiteneffekte durch `Cmd`.
/// Die Seite blockiert nie hinter dem Spiegel: waehrend eines Laufs bleibt die
/// vorhandene Liste stehen und wird erst danach neu geladen (ADR 0014).
module Components.Ledger.State

open Elmish
open Components.Ledger.Types
open Types

/// Ein frisches, leeres Kassenbuch. Geladen wird beim Betreten der Seite
/// (`UrlChanged` im Haupt-State), nicht beim Start der App: das Kassenbuch soll
/// keine Abfrage ausloesen, solange niemand hineinschaut.
let init () : Model * Cmd<Msg> =
    let model = {
        Accounts = NotAsked
        SelectedAccountId = None
        Entries = NotAsked
        HistoryFor = None
        History = NotAsked
        LastMirrorRun = NotAsked
        MirrorRunning = false
    }
    model, Cmd.none

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
        // die Seite entscheidet das nicht selbst.
        let selected = accounts.DefaultAccountId
        let model' = { model with Accounts = Success accounts; SelectedAccountId = selected }
        match selected with
        | Some _ -> { model' with Entries = Loading }, Cmd.ofMsg LoadEntries, NoOp
        | None -> { model' with Entries = NotAsked }, Cmd.none, NoOp

    | AccountsLoaded (Error message) ->
        { model with Accounts = Failure message }, Cmd.none, NoOp

    | SelectAccount accountId ->
        // Ein Kontowechsel schliesst ein offenes Historie-Sheet: es gehoerte zu einer
        // Buchung, die gleich nicht mehr in der Liste steht.
        { model with
            SelectedAccountId = Some accountId
            Entries = Loading
            HistoryFor = None
            History = NotAsked },
        Cmd.ofMsg LoadEntries,
        NoOp

    // ============================================
    // Buchungen
    // ============================================
    | LoadEntries ->
        match model.SelectedAccountId with
        | None -> model, Cmd.none, NoOp
        | Some accountId ->
            let cmd =
                Cmd.OfAsync.either
                    Api.ledger.getLedgerEntriesForAccount
                    accountId
                    (Ok >> EntriesLoaded)
                    (fun ex -> EntriesLoaded (Error ex.Message))
            { model with Entries = Loading }, cmd, NoOp

    | EntriesLoaded (Ok entries) ->
        { model with Entries = Success entries }, Cmd.none, NoOp

    | EntriesLoaded (Error message) ->
        // Der Fehler bleibt an der Liste stehen (ErrorDisplay), statt als Toast
        // wegzublenden — die Seite waere sonst still leer.
        { model with Entries = Failure message }, Cmd.none, NoOp

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
        { model with MirrorRunning = false },
        Cmd.batch [ Cmd.ofMsg LoadEntries; Cmd.ofMsg LoadMirrorStatus ],
        ShowToast (mirrorSummaryText summary, ToastSuccess)

    | MirrorFinished (Error message) ->
        // Ein Fehler wird sichtbar gemacht, nie still geschluckt (ADR 0014): als
        // Toast jetzt und als Lauf-Status dauerhaft.
        { model with MirrorRunning = false },
        Cmd.ofMsg LoadMirrorStatus,
        ShowToast ($"Spiegel-Lauf gescheitert: {message}", ToastError)
