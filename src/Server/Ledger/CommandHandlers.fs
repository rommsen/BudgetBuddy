/// Der Spiegel-Lauf: das Kommando `MirrorYnabBudget` (ADR 0013, ADR 0014).
///
/// Hier laeuft die unreine Seite: abrufen, Buchung suchen, Konto aufloesen, schreiben.
/// Der Vergleich selbst ist rein und liegt in `Ledger.Domain.observe`; geschrieben wird
/// ausschliesslich ueber `Ledger.Persistence.appendAndApply`, den einen Schreibpfad
/// (ADR 0013, Punkt 5).
module Ledger.CommandHandlers

open System
open Shared.Domain
open Server.YnabObservations
open Ledger.Events
open Ledger.Domain

/// Das Kern-Persistenz-Modul (Settings, Verbindungen). Alias, weil der einfache Name
/// `Persistence` hier auf `Ledger.Persistence` zeigen wuerde.
module Core = global.Persistence

/// Alias statt `open`: `Server.YnabClient` führt viele Namen (Payees, Kategorien,
/// Budgets, ...), ein voller `open` würde unbeabsichtigt Namen dieser Datei
/// verschatten. Gebraucht für die vorbereitete Buchung und das Push-Ergebnis
/// (Teil 1/Teil 2, ADR 0021).
module Ynab = Server.YnabClient

// ============================================
// Abrufe (injiziert)
// ============================================

/// Die Abrufe, die ein Lauf braucht. Injiziert statt fest verdrahtet, damit der Lauf
/// ohne Netz und ohne YNAB-Token testbar ist.
type MirrorFetchers = {
    /// Konten des Budgets. Der Parameter ist das zuletzt gesehene `server_knowledge`;
    /// der Lauf uebergibt hier bewusst `None` (siehe `runMirror`).
    FetchAccounts: int64 option -> Async<YnabResult<YnabAccountObservationBatch>>
    /// Buchungen des Budgets, ab dem uebergebenen `server_knowledge`. `None` ist die
    /// Erstbefuellung (Vollabruf).
    FetchTransactions: int64 option -> Async<YnabResult<YnabObservationBatch>>
    /// Kategorie-Stammdaten des Budgets, immer voll (kein `server_knowledge`-Parameter:
    /// `getCategories` kennt keinen, ADR 0039 Punkt 3). Bestes Bemühen: weder ein
    /// `Error` noch eine Ausnahme aus diesem Abruf darf den Lauf scheitern lassen
    /// (ADR 0039 Punkt 4).
    FetchCategories: unit -> Async<YnabResult<YnabCategory list>>
}

/// Standard-Fetchers für Testfixturen: jedes Feld liefert eine leere, erfolgreiche
/// Antwort. Ein Test überschreibt nur das Feld, das er tatsächlich braucht, statt
/// den ganzen Record auszuschreiben (Notes zu ledger-p3wnq).
let emptyMirrorFetchers: MirrorFetchers = {
    FetchAccounts = fun _ -> async { return Ok { Accounts = []; ServerKnowledge = 0L } }
    FetchTransactions = fun _ -> async { return Ok { Observations = []; ServerKnowledge = 0L } }
    FetchCategories = fun () -> async { return Ok [] }
}

// ============================================
// Gedaechtnis des Spiegels (Settings)
// ============================================

/// Der Settings-Schluessel des zuletzt gesehenen `server_knowledge` je Budget (ADR 0014).
let serverKnowledgeKey (YnabBudgetId budgetId: YnabBudgetId) =
    $"ledger_mirror_server_knowledge_{budgetId}"

[<Literal>]
let lastMirrorRunKey = "ledger_mirror_last_run"

let loadServerKnowledge (budgetId: YnabBudgetId) : Async<int64 option> =
    async {
        let! stored = Core.Settings.getSetting (serverKnowledgeKey budgetId)
        return
            stored
            |> Option.bind (fun text ->
                match Int64.TryParse(text, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture) with
                | true, value -> Some value
                | false, _ -> None)
    }

let private saveServerKnowledge (budgetId: YnabBudgetId) (knowledge: int64) : Async<unit> =
    Core.Settings.setSetting
        (serverKnowledgeKey budgetId)
        (knowledge.ToString(Globalization.CultureInfo.InvariantCulture))
        false

// Lauf-Status als JSON-Text in den Settings. Handgeschriebene Codecs wie im
// Ereignis-Log: die gespeicherte Form soll sich nicht hinter einem Auto-Codec
// unbemerkt aendern.
module private RunStatusCodec =
    open Thoth.Json.Net

    let encode (status: MirrorRunStatus) =
        Encode.object [
            "ranAt", Codec.encodeDate status.RanAt
            match status.Outcome with
            | MirrorRunSucceeded summary ->
                "outcome", Encode.string "succeeded"
                "newEntries", Encode.int summary.NewEntries
                "changedEntries", Encode.int summary.ChangedEntries
                "deletedEntries", Encode.int summary.DeletedEntries
                "serverKnowledge", Encode.int64 summary.ServerKnowledge
            | MirrorRunFailed message ->
                "outcome", Encode.string "failed"
                "message", Encode.string message
        ]

    let decoder : Decoder<MirrorRunStatus> =
        Decode.object (fun get ->
            let ranAt = get.Required.Field "ranAt" Codec.decodeDate
            let outcome =
                match get.Required.Field "outcome" Decode.string with
                | "succeeded" ->
                    MirrorRunSucceeded {
                        NewEntries = get.Required.Field "newEntries" Decode.int
                        ChangedEntries = get.Required.Field "changedEntries" Decode.int
                        DeletedEntries = get.Required.Field "deletedEntries" Decode.int
                        ServerKnowledge = get.Required.Field "serverKnowledge" Decode.int64
                    }
                | _ -> MirrorRunFailed (get.Required.Field "message" Decode.string)
            { RanAt = ranAt; Outcome = outcome })

/// Der zuletzt gelaufene Spiegel-Lauf. `None`, solange nie einer lief.
let loadLastMirrorRun () : Async<MirrorRunStatus option> =
    async {
        let! stored = Core.Settings.getSetting lastMirrorRunKey
        return
            stored
            |> Option.bind (fun text ->
                match Thoth.Json.Net.Decode.fromString RunStatusCodec.decoder text with
                | Ok status -> Some status
                | Error _ -> None)
    }

let private saveLastMirrorRun (status: MirrorRunStatus) : Async<unit> =
    Core.Settings.setSetting
        lastMirrorRunKey
        (Thoth.Json.Net.Encode.toString 0 (RunStatusCodec.encode status))
        false

// ============================================
// Der Lauf
// ============================================

/// Legt die gespiegelten Konten an oder zieht Name und Status nach und liefert die
/// Auflösung YNAB-Konto -> Kassenbuch-Konto. Ein in YNAB geloeschtes Konto gilt als
/// geschlossen; das Kassenbuch loescht auch hier nichts.
let private syncAccounts
    (accounts: YnabAccountObservation list)
    : Async<Map<YnabAccountId, LedgerAccountId>> =
    async {
        let mutable resolution = Map.empty

        for observed in accounts do
            let! existing = Ledger.Persistence.findAccountByYnabAccountId observed.AccountId

            let account =
                match existing with
                | Some account ->
                    { account with Name = observed.Name; Closed = observed.Closed || observed.Deleted }
                | None ->
                    {
                        Id = newLedgerAccountId ()
                        Name = observed.Name
                        // v1 spiegelt ausschliesslich Bankkonten (ADR 0016); Bar und
                        // Wertpapierkonto entstehen nicht aus dem Spiegel.
                        Kind = BankAccount
                        Link = YnabMirrored observed.AccountId
                        Closed = observed.Closed || observed.Deleted
                    }

            do! Ledger.Persistence.saveAccount account
            resolution <- Map.add observed.AccountId account.Id resolution

        return resolution
    }

/// Spiegelt die Kategorie-Stammdaten, bestes Bemühen (ADR 0039, Punkt 4): weder ein
/// `Error` des Abrufs noch eine Ausnahme darf den Lauf scheitern lassen, der seine
/// eigentliche Arbeit (Buchungen) an dieser Stelle bereits erledigt hat. Schlägt der
/// Abruf fehl, behält `ledger_categories` ihren letzten Stand.
let private refreshCategories
    (fetchCategories: unit -> Async<YnabResult<YnabCategory list>>)
    : Async<unit> =
    async {
        try
            match! fetchCategories () with
            | Ok categories -> do! Ledger.Persistence.upsertCategories categories
            | Error e ->
                printfn "Kategorie-Spiegel übersprungen (bestes Bemühen, ADR 0039 Punkt 4): %s" (ynabErrorText e)
        with ex ->
            printfn "Kategorie-Spiegel übersprungen (bestes Bemühen, ADR 0039 Punkt 4): %s" ex.Message
    }

/// Sucht die Buchung zu einer Beobachtung: erst ueber die YNAB-Id, dann ueber die
/// ImportId, sonst ist sie neu (ADR 0015, Match-Reihenfolge). Wurde ueber die ImportId
/// gematcht, traegt der Lauf die YNAB-Id als Verknuepfung nach, damit der naechste Lauf
/// direkt beim ersten Schritt trifft.
let private resolveEntry (obs: YnabObservation) : Async<EntryId * bool> =
    async {
        match! Ledger.Persistence.findEntryByLink YnabSystem obs.YnabTransactionId with
        | Some entryId -> return entryId, true
        | None ->
            let! byImportId =
                match obs.ImportId with
                | Some importId -> Ledger.Persistence.findEntryByLink ImportIdSystem importId
                | None -> async.Return None

            match byImportId with
            | Some entryId ->
                do! Ledger.Persistence.linkEntry entryId YnabSystem obs.YnabTransactionId
                return entryId, true
            | None -> return newEntryId (), false
    }

/// Verknuepft die Gegenbuchung eines Uebertrags, nachdem die Beobachtung selbst
/// geschrieben ist (ADR 0015, `EntryTransferCounterpartObservedInYnab`, ledger-t7v2q).
///
/// Bedingungen, alle erfuellt, sonst passiert nichts (kein Fehler, der Normalfall):
/// die Beobachtung ist nicht gelöscht, trägt eine `transfer_transaction_id`, der
/// Lookup über die YNAB-Verknüpfung findet die Gegenbuchung im Kassenbuch, und die
/// aufgelöste Gegenbuchungs-Id weicht von der eigenen ab (fünfter Fall, ledger-v5hkt:
/// ein Lookup über BBs eigene Verknüpfungstabelle kann auf die gerade eben
/// beobachtete Buchung selbst zeigen, auch ohne dass YNAB das so meint, weil ADR 0015
/// nur System plus Wert eindeutig macht, nicht Buchung plus System). Beide
/// Zustaende werden geladen; je Seite entsteht nur dann ein Ereignis, wenn ihr
/// `TransferEntryId` noch nicht auf die jeweils andere Buchung zeigt (Idempotenz sitzt
/// hier, im Erzeuger, nicht in `apply`). Das heilt auch den halbseitigen Fall: kommt die
/// Gegenseite erst in einem spaeteren Lauf, traegt dieser Aufruf die Verknuepfung auf
/// BEIDEN Buchungen nach, obwohl nur eine der beiden in diesem Lauf tatsaechlich
/// beobachtet wurde. Die ein oder zwei entstehenden Ereignisse gehen in einen
/// `appendAndApply`-Aufruf (eine Transaktion).
let private linkTransferCounterpart
    (now: DateTime)
    (entryId: EntryId)
    (obs: YnabObservation)
    : Async<unit> =
    async {
        if obs.Deleted then
            return ()
        else
            match obs.TransferYnabTransactionId with
            | None -> return ()
            | Some counterpartYnabTxId ->
                match! Ledger.Persistence.findEntryByLink YnabSystem counterpartYnabTxId with
                | None ->
                    // Kein Treffer ist der Normalfall: die Gegenbuchung ist im
                    // Kassenbuch (noch) nicht bekannt.
                    return ()
                | Some counterpartId when counterpartId = entryId ->
                    // Selbstbezug: der Lookup über die eigene Verknüpfungstabelle
                    // zeigt auf die gerade eben beobachtete Buchung selbst. Kein
                    // Ereignis bauen, denn `apply` (Domain.fs) lehnt das laut ab und
                    // würde den ganzen Lauf abbrechen (ADR 0014: ein abgebrochener
                    // Lauf holt beim nächsten Mal dasselbe Delta erneut, der
                    // Selbstbezug in den Daten verschwindet nicht von allein).
                    // Stilles Überspringen statt Abbruch: die Verknüpfung ist
                    // ohnehin nur ein Nachtrag, kein Beobachtungsergebnis.
                    let (EntryId id) = entryId
                    printfn
                        "Übertrags-Verknüpfung übersprungen: Buchung %O zeigt auf sich selbst (YNAB-Transaktion %s)"
                        id
                        obs.YnabTransactionId
                    return ()
                | Some counterpartId ->
                    let! thisEntry = Ledger.Persistence.loadEntry entryId
                    let! counterpartEntry = Ledger.Persistence.loadEntry counterpartId

                    let linkEvent (side: EntryId) (counterpart: EntryId) (counterpartYnabTransactionId: string) =
                        {
                            EntryId = side
                            OccurredAt = now
                            Actor = YnabMirror
                            Body =
                                EntryTransferCounterpartObservedInYnab {
                                    CounterpartEntryId = counterpart
                                    CounterpartYnabTransactionId = counterpartYnabTransactionId
                                }
                        }

                    let events =
                        [
                            match thisEntry with
                            | Some e when e.TransferEntryId <> Some counterpartId ->
                                linkEvent entryId counterpartId counterpartYnabTxId
                            | _ -> ()

                            match counterpartEntry with
                            | Some e when e.TransferEntryId <> Some entryId ->
                                linkEvent counterpartId entryId obs.YnabTransactionId
                            | _ -> ()
                        ]

                    do! Ledger.Persistence.appendAndApply events
    }

/// Verknüpft die Gegenbuchungen der Übertrags-Split-Zeilen einer Buchung (ADR 0046,
/// `EntrySplitTransferCounterpartObservedInYnab`, ledger-h4nxz). Läuft in einem Nachlauf
/// nach der Beobachtungs-Schleife, weil nur die Split-Seite die Fremd-Id trägt: liegt die
/// Gegenbuchung im selben Batch hinter der Split-Buchung, fände ein Aufruf innerhalb der
/// Schleife sie nicht, und kein künftiges Delta brächte beide erneut vorbei.
///
/// Je Zeile mit `transfer_transaction_id`: kein `findEntryByLink`-Treffer ist der
/// Normalfall (kein Ereignis, kein Fehler), ein Selbstbezug wird still übersprungen.
/// Ein Ereignis auf der Split-Buchung nur, wenn ihre Map den Schlüssel noch nicht auf
/// diese Gegenbuchung zeigen hat, eines auf der Gegenbuchung (bestehendes
/// `EntryTransferCounterpartObservedInYnab`) nur, wenn deren `TransferEntryId` noch nicht
/// auf die Split-Buchung zeigt. Alle Ereignisse einer Beobachtung gehen in einen
/// `appendAndApply`-Aufruf. Die Zähler des Laufs bleiben unberührt.
let private linkSplitTransferCounterparts
    (now: DateTime)
    (entryId: EntryId)
    (obs: YnabObservation)
    : Async<unit> =
    async {
        if not obs.Deleted then
            let events = ResizeArray<LedgerEvent>()

            for split in obs.SplitTransfers do
                match split.TransferYnabTransactionId with
                | None -> ()
                | Some counterpartYnabTxId ->
                    match! Ledger.Persistence.findEntryByLink YnabSystem counterpartYnabTxId with
                    | None -> ()
                    | Some counterpartId when counterpartId = entryId ->
                        let (EntryId id) = entryId
                        printfn
                            "Split-Übertrags-Verknüpfung übersprungen: Buchung %O zeigt auf sich selbst (YNAB-Transaktion %s)"
                            id
                            obs.YnabTransactionId
                    | Some counterpartId ->
                        let! thisEntry = Ledger.Persistence.loadEntry entryId
                        let! counterpartEntry = Ledger.Persistence.loadEntry counterpartId

                        match thisEntry with
                        | Some e when
                            Map.tryFind split.YnabSubtransactionId e.SplitTransferCounterparts
                            <> Some counterpartId ->
                            events.Add {
                                EntryId = entryId
                                OccurredAt = now
                                Actor = YnabMirror
                                Body =
                                    EntrySplitTransferCounterpartObservedInYnab {
                                        YnabSubtransactionId = split.YnabSubtransactionId
                                        CounterpartEntryId = counterpartId
                                        CounterpartYnabTransactionId = counterpartYnabTxId
                                        SplitAmount = split.Amount
                                    }
                            }
                        | _ -> ()

                        match counterpartEntry with
                        | Some e when e.TransferEntryId <> Some entryId ->
                            events.Add {
                                EntryId = counterpartId
                                OccurredAt = now
                                Actor = YnabMirror
                                Body =
                                    EntryTransferCounterpartObservedInYnab {
                                        CounterpartEntryId = entryId
                                        CounterpartYnabTransactionId = obs.YnabTransactionId
                                    }
                            }
                        | _ -> ()

            if events.Count > 0 then
                do! Ledger.Persistence.appendAndApply (List.ofSeq events)
    }

/// Ist diese Beobachtung die Lösch-Beobachtung einer abgelösten YNAB-Id (ADR 0058, Punkt 5)?
/// Abgelöst ist eine Id, wenn ein Ersatz sie festgehalten hat (Verknüpfung
/// `YnabSupersededSystem`) oder wenn das DELETE gelungen und die Neuanlage noch offen ist
/// (`YnabDeletedTransactionId` am offenen Fehlschlag). Beides lässt `DeletedInYnab` aus.
let private isSupersededDeletion (existing: Entry option) (obs: YnabObservation) : Async<bool> =
    async {
        if not obs.Deleted then
            return false
        else
            let pendingDeletion =
                existing
                |> Option.bind (fun e -> e.CategorizationWriteBackFailure)
                |> Option.bind (fun f -> f.YnabDeletedTransactionId)
            if pendingDeletion = Some obs.YnabTransactionId then
                return true
            else
                let! superseded = Ledger.Persistence.findEntryByLink YnabSupersededSystem obs.YnabTransactionId
                return superseded.IsSome
    }

/// Ein Spiegel-Lauf: Beobachtungen holen, Konten nachziehen, je Beobachtung die Buchung
/// suchen, den Snapshot gegen den Zustand vergleichen und nur bei Unterschied Ereignisse
/// schreiben. Am Ende steht das `server_knowledge` des Laufs in den Settings und der
/// Lauf-Status ist abrufbar — auch (und gerade) im Fehlerfall (ADR 0014).
///
/// `now` und die Abrufe kommen von aussen; die Funktion selbst kennt weder Uhr noch HTTP.
let runMirror
    (now: DateTime)
    (fetchers: MirrorFetchers)
    (budgetId: YnabBudgetId)
    : Async<Result<MirrorRunSummary, string>> =
    async {
        let finish (result: Result<MirrorRunSummary, string>) =
            async {
                let outcome =
                    match result with
                    | Ok summary -> MirrorRunSucceeded summary
                    | Error message -> MirrorRunFailed message

                do! saveLastMirrorRun { RanAt = now; Outcome = outcome }
                return result
            }

        try
            let! knowledge = loadServerKnowledge budgetId

            // Konten bewusst **vollstaendig**, ohne `last_knowledge_of_server`: ein
            // Konten-Delta laesst unveraenderte Konten weg, und eine Buchung kann auf
            // genau so ein Konto zeigen. Der Request kostet gleich viel (ADR 0014,
            // Rate-Limit-Rechnung: ein Lauf = zwei Requests).
            match! fetchers.FetchAccounts None with
            | Error error -> return! finish (Error (ynabErrorText error))
            | Ok accountBatch ->
                let! resolution = syncAccounts accountBatch.Accounts

                match! fetchers.FetchTransactions knowledge with
                | Error error -> return! finish (Error (ynabErrorText error))
                | Ok batch ->
                    let mutable newEntries = 0
                    let mutable changedEntries = 0
                    let mutable deletedEntries = 0
                    let mutable failure = None
                    // Beobachtungen mit Übertrags-Split-Zeilen, für den Nachlauf (ADR 0046).
                    let splitTransferObservations = ResizeArray<EntryId * YnabObservation>()

                    for obs in batch.Observations do
                        if failure.IsNone then
                            match Map.tryFind obs.AccountId resolution with
                            | None ->
                                let (YnabAccountId accountId) = obs.AccountId
                                failure <-
                                    Some
                                        $"Buchung {obs.YnabTransactionId} zeigt auf das YNAB-Konto {accountId}, das der Konten-Abruf nicht kennt"
                            | Some ledgerAccountId ->
                                let! entryId, known = resolveEntry obs

                                let! existing =
                                    if known then Ledger.Persistence.loadEntry entryId
                                    else async.Return None

                                // ADR 0058, Punkt 5: das Löschen der ersetzten Id ist BBs eigenes
                                // Werk und darf die Buchung nicht als "in YNAB gelöscht" markieren.
                                let! supersededDeletion = isSupersededDeletion existing obs

                                let events =
                                    if supersededDeletion then []
                                    else
                                        observe
                                            { EntryId = entryId; AccountId = ledgerAccountId; ObservedAt = now }
                                            existing
                                            obs

                                do! Ledger.Persistence.appendAndApply events

                                // Uebertrags-Verknuepfung (ADR 0015): erst nachdem die
                                // Beobachtung selbst steht, sonst findet der Lookup die
                                // gerade eben geschriebene Buchung nicht. Zaehlt bewusst
                                // NICHT in changedEntries/newEntries/deletedEntries: die
                                // Zaehler beschreiben, was sich in YNAB geaendert hat,
                                // die Verknuepfung ist BBs eigener Nachtrag.
                                do! linkTransferCounterpart now entryId obs

                                if not (List.isEmpty obs.SplitTransfers) then
                                    splitTransferObservations.Add((entryId, obs))

                                if events |> List.exists (fun e ->
                                        match e.Body with
                                        | EntryObservedInYnab _ -> true
                                        | _ -> false) then
                                    newEntries <- newEntries + 1

                                if events |> List.exists (fun e ->
                                        match e.Body with
                                        | EntryDetailsChangeObservedInYnab _
                                        | EntryCategorizationChangeObservedInYnab _
                                        | EntryClearedStateChangeObservedInYnab _
                                        | EntryApprovalChangeObservedInYnab _
                                        | EntryFlagChangeObservedInYnab _ -> true
                                        | _ -> false) then
                                    changedEntries <- changedEntries + 1

                                if events |> List.exists (fun e -> e.Body = EntryDeletionObservedInYnab) then
                                    deletedEntries <- deletedEntries + 1

                    match failure with
                    | Some message -> return! finish (Error message)
                    | None ->
                        // Nachlauf (ADR 0046): jetzt sind alle Buchungen des Batches
                        // geschrieben, die Gegenbuchung ist auffindbar, egal wo sie liegt.
                        for (entryId, obs) in splitTransferObservations do
                            do! linkSplitTransferCounterparts now entryId obs

                        // Erst wenn alle Beobachtungen verarbeitet sind, ruecken wir das
                        // Gedaechtnis vor: ein abgebrochener Lauf holt beim naechsten Mal
                        // dasselbe Delta erneut, was dank Idempotenz folgenlos ist.
                        do! saveServerKnowledge budgetId batch.ServerKnowledge

                        // Nach der eigentlichen Arbeit, bestes Bemühen (ADR 0039,
                        // Punkt 4): ein 429 aus diesem Abruf darf einen Lauf nicht
                        // kippen, der seine Buchungen schon geschrieben hat.
                        do! refreshCategories fetchers.FetchCategories

                        return!
                            finish (
                                Ok {
                                    NewEntries = newEntries
                                    ChangedEntries = changedEntries
                                    DeletedEntries = deletedEntries
                                    ServerKnowledge = batch.ServerKnowledge
                                })
        with ex ->
            return! finish (Error $"Spiegel-Lauf abgebrochen: {ex.Message}")
    }

/// Die Schreib-Sperre (ADR 0055, Punkt 6): der Spiegel-Lauf (Abruf und `observe`) und die
/// Kategorie-Befehle (Ereignis und Push) laufen nicht ineinander. Sonst kann ein Lauf, der
/// zwischen Ereignis und PUT abruft, den alten YNAB-Stand als Änderung beobachten und die
/// BB-Kategorie zurückdrehen. Ein Befehl wartet höchstens auf einen laufenden Lauf (oder
/// einen anderen Befehl). `internal`, damit ein Test die Sperre halten kann
/// (`InternalsVisibleTo "Tests"`).
module internal WriteGuard =
    let private gate = new System.Threading.SemaphoreSlim(1, 1)

    /// Führt `work` aus, sobald die Sperre frei ist, und gibt sie danach wieder frei,
    /// auch bei einer Ausnahme.
    let exclusive (work: Async<'a>) : Async<'a> =
        async {
            do! gate.WaitAsync() |> Async.AwaitTask
            try
                return! work
            finally
                gate.Release() |> ignore
        }

// ============================================
// Einzel-Flug-Schutz: zwei gleichzeitige Auslöser ergeben nur einen Lauf
// ============================================
//
// ADR 0014 Punkt 3 verlangt, dass ein bereits laufender Spiegel nicht ein zweites Mal
// gestartet wird. Der Client-Zustand (`MirrorRunning`) hält das nur pro Tab; zwei
// Browser-Tabs oder ein Seiten-Öffnen, das mit einem Post-Push-Auslöser
// zusammentrifft, müssen serverseitig geteilt werden (ledger-h4p6c, Task-Nachtrag).
// `runGuarded` hält den gerade laufenden Task fest: ein zweiter Aufruf während eines
// Laufs startet keinen zweiten YNAB-Abruf, sondern wartet auf denselben Task und teilt
// sein Ergebnis (single flight). Die Freigabe steht in `finally`, damit sie auch nach
// einem fehlgeschlagenen oder abgebrochenen Lauf greift: der nächste Auslöser darf es
// dann erneut versuchen, statt dauerhaft zu blockieren (ledger-h4p6c, Verifier-Fund
// Iteration 1). `internal` statt `private`, damit `runGuarded` selbst mit einem
// werfenden Lauf getestet werden kann (`InternalsVisibleTo "Tests"`), ohne den Umweg
// über `runMirror`s eigenes Fehlerhandling.
module internal MirrorGuard =
    open System.Threading.Tasks

    let private gate = obj ()
    let mutable private inFlight : Task<Result<MirrorRunSummary, string>> option = None

    let runGuarded
        (run: unit -> Async<Result<MirrorRunSummary, string>>)
        : Async<Result<MirrorRunSummary, string>> =
        async {
            let task =
                lock gate (fun () ->
                    match inFlight with
                    | Some running -> running
                    | None ->
                        let started = Async.StartAsTask(run ())
                        inFlight <- Some started
                        started)

            try
                return! Async.AwaitTask task
            finally
                // Nur der Aufrufer, dessen Task noch der aktuelle ist, räumt den
                // Platzhalter wieder ab: ein späterer, schon neuerer Lauf darf dadurch
                // nicht versehentlich verworfen werden. `finally` statt eines Schritts
                // nach `let!`, damit die Freigabe auch bei einem fehlgeschlagenen oder
                // abgebrochenen Task läuft, nicht nur beim normalen Abschluss.
                lock gate (fun () ->
                    match inFlight with
                    | Some current when obj.ReferenceEquals(current, task) -> inFlight <- None
                    | _ -> ())
        }

/// `runMirror`, aber mit dem Einzel-Flug-Schutz: der Auslöser für alle drei
/// Wege aus ADR 0014 (Seitenaufruf/Vordergrund über den manuellen Endpunkt, Lauf nach
/// Push, "Jetzt spiegeln").
let runMirrorGuarded
    (now: DateTime)
    (fetchers: MirrorFetchers)
    (budgetId: YnabBudgetId)
    : Async<Result<MirrorRunSummary, string>> =
    MirrorGuard.runGuarded (fun () -> WriteGuard.exclusive (runMirror now fetchers budgetId))

// ============================================
// Quick Add: Kassenbuch zuerst, dann Push, dann Verknuepfung (ADR 0021)
// ============================================

/// Die Eingabe einer Quick-Add-Buchung: Konto "Bar" (als Kassenbuch-Konto, nicht als
/// YNAB-Konto), Betrag, Payee, Memo, Kategorie. Das Aufloesen des Kassenbuch-Kontos aus
/// dem konfigurierten YNAB-Quick-Add-Konto ist Sache des Aufrufers (`Server.Api`).
type CashExpenseInput = {
    AccountId: LedgerAccountId
    Date: DateTime
    Amount: Money
    Payee: string option
    Memo: string option
    Category: CategoryRef option
}

/// Der Grund, wenn YNAB den Versand beantwortet hat, die Buchung aber nicht in der
/// Antwort angelegt war: nicht auswertbare Antwort, Duplikat oder sonst nicht angelegt.
let describeNotCreatedReason (importId: string) (result: Ynab.TransactionCreateResult) : string =
    if result.ResponseUnparseable then "Die Antwort von YNAB war nicht auswertbar"
    elif result.DuplicateImportIds |> List.contains importId then "YNAB hat die Buchung als Duplikat abgelehnt"
    else "YNAB hat die Buchung nicht angelegt"

/// Schreibt zuerst die Kassenbuch-Buchung (`CashExpenseAdded`, Actor `actor`: `QuickAdd`
/// für die eigene Seite, `Bob` für die `/bot/`-Schnittstelle, ADR 0066), ruft
/// danach `push` auf und verknüpft bei Erfolg mit `EntryLinkedToYnab` (ADR 0021): `push`
/// liefert die YNAB-Transaction-Id aus der Antwort des Einzel-Posts, die einzige Stelle,
/// an der eine Quick-Add-Buchung ohne ImportId verknüpft werden kann (ADR 0004, ADR
/// 0015). Scheitert der Push, bleibt die Kassenbuch-Buchung unverändert stehen, kein
/// Rollback (ADR 0021, Punkt 2); der Fehler wird durchgereicht, die Buchung bleibt ohne
/// YNAB-Verknüpfung bis zu einem erneuten Push oder dem nächsten Spiegel-Lauf. Der
/// Fehlschlag steht danach als `EntryPushToYnabFailed` mit Grund im Log (ledger-rynk5).
///
/// Nach erfolgreichem Push stößt `triggerMirror` den Spiegel an (ADR 0014 Punkt 2):
/// erst wenn `EntryLinkedToYnab` geschrieben ist, nie davor (Task-Nachtrag, ADR 0021),
/// sonst sähe ein dazwischenkommender Lauf die YNAB-Buchung ungematcht und legte eine
/// zweite Buchung an (Quick Add trägt keine ImportId, ADR 0004).
///
/// `now`, `push` und `triggerMirror` kommen von außen, damit der Ablauf ohne Uhr, ohne
/// Netz und ohne Spiegel-Lauf testbar ist (Muster wie `MirrorFetchers` beim
/// Spiegel-Lauf).
let addCashExpense
    (actor: Actor)
    (now: DateTime)
    (push: unit -> Async<YnabResult<string>>)
    (triggerMirror: unit -> unit)
    (input: CashExpenseInput)
    : Async<YnabResult<EntryId>> =
    async {
        let entryId = newEntryId ()
        let cashExpenseAdded = {
            EntryId = entryId
            OccurredAt = now
            Actor = actor
            Body =
                CashExpenseAdded {
                    AccountId = input.AccountId
                    Date = input.Date
                    Amount = input.Amount
                    Payee = input.Payee
                    Memo = input.Memo
                    Category = input.Category
                    // Manuelle Eintraege sind immer uncleared (ADR 0004, Punkt 4).
                    Cleared = Uncleared
                    // Quick Add sendet kein `approved`; YNAB legt die Buchung
                    // unbestätigt an (ADR 0032).
                    Approved = false
                }
        }
        do! Ledger.Persistence.appendAndApply [ cashExpenseAdded ]

        match! push () with
        | Ok ynabTransactionId ->
            let linked = {
                EntryId = entryId
                OccurredAt = now
                Actor = actor
                Body = EntryLinkedToYnab { YnabTransactionId = ynabTransactionId }
            }
            do! Ledger.Persistence.appendAndApply [ linked ]
            triggerMirror ()
            return Ok entryId
        | Error err ->
            do!
                Ledger.Persistence.appendAndApply [
                    {
                        EntryId = entryId
                        OccurredAt = now
                        Actor = actor
                        Body = EntryPushToYnabFailed { Reason = ynabErrorText err }
                    }
                ]
            return Error err
    }

// ============================================
// Erneut senden: Push einer nicht verknüpften Buchung (ledger-cks06, ADR 0021)
// ============================================

/// Der Einzel-Push einer Kassenbuch-Buchung nach YNAB, injiziert wie `addCashExpense`s
/// `push`: bekommt den aktuellen Stand der Buchung und liefert die YNAB-Transaction-Id
/// aus der Antwort.
type EntryPush = Entry -> Async<YnabResult<string>>

/// Die Flag-Farbe, die ein Push dieser Buchung mitsendet (ynab-sync-d9e51): die Farbe des
/// BB-Stands, ohne Flag keine. "Erneut senden" nach einem gescheiterten Import-Push schickt
/// so die Rückfrage Lena mit, mit der die Buchung entstanden ist. Nur die Farbe, nie der Name
/// (ADR 0056).
let pushFlagColorOf (entry: Entry) : string option = entry.FlagColor

/// Der Grund, wenn die Buchung zu keinem YNAB-Konto gehört (BB-eigenes Konto).
let pushNotPossibleReason = "Das Konto der Buchung ist nicht mit YNAB verbunden"

/// "Erneut senden" am Hinweis "Noch nicht in YNAB" (ledger-cks06): pusht genau diese
/// eine Buchung über denselben Weg wie die Neuanlage. Erfolg schreibt `EntryLinkedToYnab`
/// (löst den offenen Fehlschlag auf) und stößt den Spiegel an, ein Fehlschlag schreibt
/// `EntryPushToYnabFailed` mit Grund (ersetzt den alten Fehlschlag, Zeitpunkt `now`).
/// Eine unbekannte, in YNAB gelöschte oder schon verknüpfte Buchung wird abgelehnt, ohne
/// dass `push` aufgerufen wird (eine zweite YNAB-Buchung wäre die Folge).
let retryPushToYnab
    (now: DateTime)
    (push: EntryPush)
    (triggerMirror: unit -> unit)
    (entryId: EntryId)
    : Async<Result<PushToYnabWriteBack, string>> =
    async {
        match! Ledger.Persistence.loadEntry entryId with
        | None -> return Error "Buchung nicht gefunden"
        | Some entry when entry.DeletedInYnab -> return Error "Die Buchung ist in YNAB gelöscht"
        | Some entry when entry.LinkedToYnab -> return Error "Die Buchung ist schon in YNAB"
        | Some entry ->
            match! push entry with
            | Ok ynabTransactionId ->
                do!
                    Ledger.Persistence.appendAndApply [
                        {
                            EntryId = entryId
                            OccurredAt = now
                            Actor = UserInBb
                            Body = EntryLinkedToYnab { YnabTransactionId = ynabTransactionId }
                        }
                    ]
                triggerMirror ()
                return Ok PushToYnabSent
            | Error err ->
                let reason = ynabErrorText err
                do!
                    Ledger.Persistence.appendAndApply [
                        {
                            EntryId = entryId
                            OccurredAt = now
                            Actor = UserInBb
                            Body = EntryPushToYnabFailed { Reason = reason }
                        }
                    ]
                return Ok (PushToYnabFailed reason)
    }

// ============================================
// Import-Push: Kassenbuch zuerst, dann YNAB, dann Verknüpfung (ADR 0021, Teil 2)
// ============================================

/// Eine zu importierende Comdirect-Buchung, wie sie für den Kassenbuch-Schreibpfad vor
/// dem Push nötig ist: das vorbereitete YNAB-Request-Objekt (`Ynab.prepareTransactions`,
/// trägt TransactionId und ImportId) plus die Rohdaten und die Review-Spur, aus denen
/// `recordComdirectPush` die Ereignisfolge baut.
type ComdirectImportCandidate = {
    Prepared: Ynab.PreparedTransaction
    ComdirectReference: string
    Date: DateTime
    Amount: Money
    /// Wie im tatsächlich gesendeten Request (`Prepared.Request.PayeeName`), also mit
    /// einem zu diesem Zeitpunkt schon aktiven Override.
    Payee: string option
    /// Der rohe, von der Bank gelieferte Payee (`BankTransaction.Payee`), unabhängig
    /// von jedem Override. Fallback in `recordComdirectPush`, wenn ein Review-Schritt
    /// keinen Override (mehr) trägt (ledger-jkb0f).
    RawPayee: string option
    Memo: string option
    ReviewSteps: ReviewStep list
    /// Die ImportId, über die nach einer schon bestehenden Kassenbuch-Buchung gesucht
    /// wird, falls sie von `Prepared.ImportId` abweicht (Force-Import, ledger-fq3kd):
    /// der normale Push schreibt die Buchung vor YNABs Duplikat-Meldung bereits mit der
    /// alten, deterministischen ImportId (`Shared.Domain.generateImportId`); Force-
    /// Import sendet danach mit einer neuen ImportId (`forceNewImportId = true`, nur für
    /// YNABs Dedup), muss die bestehende Buchung aber über die alte ImportId finden.
    /// `None` (Normalfall, Import-Push): Suche und Versand teilen sich dieselbe
    /// ImportId, wie bisher.
    ExistingEntryImportId: string option
}

/// Wie eine einzelne Buchung aus einem Push-Versuch hervorgegangen ist.
type ComdirectPushOutcome =
    /// Kassenbuch geschrieben, an YNAB gesendet und über die Push-Antwort verknüpft.
    | Linked of EntryId * ynabTransactionId: string
    /// Kassenbuch geschrieben und gesendet, aber (noch) nicht verknüpft: Push
    /// fehlgeschlagen, YNAB lehnte als Duplikat ab, oder die Antwort war nicht
    /// auswertbar (ADR 0021 Punkt 2, kein Rollback).
    | WrittenNotLinked of EntryId
    /// Schon eine mit YNAB verknüpfte Buchung für diese ImportId (ADR 0021 Punkt 3):
    /// kein zweites Entstehen, diese Buchung wurde nicht erneut gesendet. Eine Buchung
    /// mit dieser ImportId, aber noch OHNE `ynab`-Verknüpfung (vorheriger Push-Versuch
    /// gescheitert, bevor verknüpft wurde) ist hier NICHT gemeint: die läuft als
    /// `Linked`/`WrittenNotLinked` über dieselbe Buchungs-Id erneut durch Senden und
    /// Verknüpfen (kein zweites Entstehen, aber ein erneuter Versand).
    | AlreadyImported of EntryId
    /// Das Kassenbuch-Schreiben ist gescheitert: nicht gesendet (Task-Notes, Schritt 7b).
    | LedgerWriteFailed of string

/// Ergebnis eines Import-Push-Versuchs: die Herkunft je Buchung, plus das rohe
/// Sende-Ergebnis (`None`, wenn nichts zu senden war). Der Aufrufer (`Server.Api`)
/// braucht `SendResult` zusätzlich zu `Outcomes`, um z. B. einen Netzwerkfehler oder
/// eine nicht auswertbare Antwort als eigenen API-Fehler zu melden, obwohl die
/// Kassenbuch-Buchungen unverändert stehen bleiben (ADR 0021 Punkt 2).
type ComdirectPushResult = {
    Outcomes: Map<TransactionId, ComdirectPushOutcome>
    SendResult: YnabResult<Ynab.TransactionCreateResult> option
}

/// Kassenbuch zuerst, dann Push, dann Verknüpfung (ADR 0021), für den Comdirect-
/// Import-Push. `push` postet genau die übergebene, vorbereitete Liste
/// (`Ynab.sendPrepared`) und ist injiziert, damit der Ablauf ohne Netz testbar ist
/// (Muster wie `addCashExpense`/`MirrorFetchers`).
///
/// Je Buchung: existiert schon eine Buchung mit ihrer ImportId UND ist sie schon mit
/// YNAB verknüpft, wird sie übersprungen (kein zweites Entstehen, `AlreadyImported`,
/// nicht erneut gesendet). Existiert die Buchung schon, ist aber noch nicht verknüpft
/// (ein vorheriger Push-Versuch scheiterte zwischen Schreiben und Verknüpfen), wird sie
/// NICHT neu angelegt, aber trotzdem gesendet und bei Erfolg verknüpft. Existiert noch
/// keine Buchung, schreibt `appendAndApply` die Ereignisfolge aus `recordComdirectPush`.
/// Gesendet wird nur, was geschrieben (oder wiedergefunden) wurde
/// (Task-Notes, Schritt 8). Nach einer erfolgreichen Antwort wird jede tatsächlich
/// angelegte Buchung mit `EntryLinkedToYnab` verknüpft; ein Push-Fehler, ein Duplikat
/// oder eine nicht auswertbare Antwort lassen die geschriebenen Buchungen unverändert
/// und unverknüpft stehen (kein Rollback).
///
/// `triggerMirror` stößt nach einem tatsächlich erfolgreichen Sende-Versuch den
/// Spiegel an (ADR 0014 Punkt 2), erst nachdem alle `EntryLinkedToYnab`-Verknüpfungen
/// dieses Versuchs geschrieben sind. Ohne Sendung (nichts Neues, alles schon
/// `AlreadyImported`) oder bei einem gescheiterten Sende-Versuch bleibt der Auslöser
/// aus: der nächste Spiegel-Lauf (Frische-Kriterium oder ein erneuter Push) holt das
/// nach.
let pushComdirectImports
    (now: DateTime)
    (push: Ynab.PreparedTransaction list -> Async<YnabResult<Ynab.TransactionCreateResult>>)
    (triggerMirror: unit -> unit)
    (accountId: LedgerAccountId)
    (candidates: ComdirectImportCandidate list)
    : Async<ComdirectPushResult> =
    async {
        // Kategorie-Namen-Resolver (ledger-jkb0f): einmal vorab geladen (Muster wie
        // `loadCategoryGroups`, ADR 0039), damit `recordComdirectPush` einen frischeren
        // Namen liefern kann als der Review-Schritt selbst kennt, ohne je Buchung
        // erneut nachzuschlagen.
        let! categoryNames = Ledger.Persistence.loadCategoryNames ()
        let resolveCategoryName (YnabCategoryId id) = Map.tryFind id categoryNames

        let! writeResults =
            candidates
            |> List.map (fun candidate ->
                async {
                    let prepared = candidate.Prepared
                    // Force-Import (ledger-fq3kd): sucht über die alte, deterministische
                    // ImportId, sendet aber mit der neuen (`ExistingEntryImportId`,
                    // gesetzt vom Aufrufer). Normalfall: beide sind dieselbe ImportId.
                    let searchImportId = candidate.ExistingEntryImportId |> Option.defaultValue prepared.ImportId

                    match! Ledger.Persistence.findEntryByLink ImportIdSystem searchImportId with
                    | Some entryId ->
                        // Schon eine Buchung für diese ImportId (ADR 0021 Punkt 3): kein
                        // zweites Entstehen. Ob sie erneut gesendet werden muss, hängt
                        // aber davon ab, ob sie schon mit YNAB verknüpft ist. Ohne
                        // `ynab`-Verknüpfung ist ein vorheriger Push-Versuch gescheitert
                        // (Netzwerkfehler, Timeout, 429, 400), bevor die Verknüpfung
                        // geschrieben wurde: dann muss der zweite Versuch trotzdem senden
                        // und verknüpfen, sonst erreicht die Buchung YNAB nie (Verifier-
                        // Fund, Iteration 2; ADR 0021 "bis ein erneuter Push ... sie über
                        // die ImportId verknüpft" setzt genau das voraus).
                        let! links = Ledger.Persistence.linksForEntry entryId
                        if links |> List.exists (fun l -> l.System = YnabSystem) then
                            return prepared.TransactionId, Choice1Of3 entryId
                        else
                            return prepared.TransactionId, Choice2Of3 entryId
                    | None ->
                        let entryId = newEntryId ()
                        let events =
                            recordComdirectPush resolveCategoryName {
                                EntryId = entryId
                                AccountId = accountId
                                // Die neu geschriebene Buchung trägt dieselbe ImportId,
                                // über die sie gesucht wurde (`searchImportId`), nicht
                                // zwingend `prepared.ImportId`: im Force-Fall wäre das
                                // sonst die neue, nur für YNABs Dedup erzeugte ImportId,
                                // unter der ein späterer Versuch (mit wieder einer neuen
                                // ImportId) diese Buchung nie wiederfände und erneut eine
                                // zweite anlegen würde (AC "zweiter Force-Import-Versuch
                                // erzeugt keine zweite Buchung", ledger-fq3kd).
                                ImportId = searchImportId
                                ComdirectReference = candidate.ComdirectReference
                                Date = candidate.Date
                                Amount = candidate.Amount
                                Payee = candidate.Payee
                                RawPayee = candidate.RawPayee
                                Memo = candidate.Memo
                                PushedAt = now
                                ReviewSteps = candidate.ReviewSteps
                                // Rückfrage Lena beim Import (ynab-sync-d9e51): die Farbe, die der
                                // Create-Body trägt, steht schon in der Entstehung (Kassenbuch
                                // zuerst, ADR 0021), nicht erst nach dem Push.
                                LenaFlagColor = prepared.Request.FlagColor
                            }

                        try
                            do! Ledger.Persistence.appendAndApply events
                            return prepared.TransactionId, Choice2Of3 entryId
                        with ex ->
                            return prepared.TransactionId, Choice3Of3 ex.Message
                })
            |> Async.Sequential

        let alreadyImported =
            writeResults
            |> Array.choose (function txId, Choice1Of3 entryId -> Some (txId, entryId) | _ -> None)
        let written =
            writeResults
            |> Array.choose (function txId, Choice2Of3 entryId -> Some (txId, entryId) | _ -> None)
        let failed =
            writeResults
            |> Array.choose (function txId, Choice3Of3 msg -> Some (txId, msg) | _ -> None)

        let writtenMap = written |> Map.ofArray
        let preparedByTxId =
            candidates |> List.map (fun c -> c.Prepared.TransactionId, c.Prepared) |> Map.ofList
        let toSend =
            written |> Array.choose (fun (txId, _) -> Map.tryFind txId preparedByTxId) |> Array.toList

        let! sendResultOpt =
            if toSend.IsEmpty then async.Return None else async { let! r = push toSend in return Some r }
        let sendResult = sendResultOpt |> Option.defaultValue (Ok Ynab.emptyCreateResult)

        let outcomes =
            match sendResult with
            | Error _ ->
                // ADR 0021 Punkt 2: kein Rollback bei Push-Fehler. Alles, was
                // geschrieben wurde, bleibt stehen, nur eben unverknüpft.
                writtenMap |> Map.map (fun _ entryId -> WrittenNotLinked entryId)
            | Ok result ->
                let ynabIdByTxId =
                    result.Created |> List.map (fun c -> c.TransactionId, c.YnabTransactionId) |> Map.ofList
                writtenMap
                |> Map.map (fun txId entryId ->
                    match Map.tryFind txId ynabIdByTxId with
                    | Some ynabId -> Linked (entryId, ynabId)
                    | None -> WrittenNotLinked entryId)

        let! linkedOutcomes =
            outcomes
            |> Map.toList
            |> List.map (fun (txId, outcome) ->
                async {
                    match outcome with
                    | Linked (entryId, ynabId) ->
                        do!
                            Ledger.Persistence.appendAndApply [
                                {
                                    EntryId = entryId
                                    OccurredAt = now
                                    Actor = ComdirectImport
                                    Body = EntryLinkedToYnab { YnabTransactionId = ynabId }
                                }
                            ]
                        return txId, outcome
                    | WrittenNotLinked entryId ->
                        // Gescheiterter Versand: Grund und Zeit im Log festhalten, die Buchung
                        // bleibt unverknüpft stehen (ADR 0021 Punkt 2, ledger-rynk5).
                        let reason =
                            match sendResult with
                            | Error err -> ynabErrorText err
                            | Ok result ->
                                let importId =
                                    Map.tryFind txId preparedByTxId
                                    |> Option.map (fun p -> p.ImportId)
                                    |> Option.defaultValue ""
                                describeNotCreatedReason importId result
                        do!
                            Ledger.Persistence.appendAndApply [
                                {
                                    EntryId = entryId
                                    OccurredAt = now
                                    Actor = ComdirectImport
                                    Body = EntryPushToYnabFailed { Reason = reason }
                                }
                            ]
                        return txId, outcome
                    | _ -> return txId, outcome
                })
            |> Async.Sequential

        // Nach dem Push (ADR 0014 Punkt 2): nur wenn tatsächlich etwas gesendet und
        // die Antwort auswertbar war, nicht bei einem leeren Versuch oder einem
        // Sende-Fehler (Task-Nachtrag zu ADR 0021: alle `EntryLinkedToYnab` dieses
        // Versuchs stehen zu diesem Zeitpunkt schon im Log, `linkedOutcomes` ist fertig).
        match sendResultOpt with
        | Some (Ok _) -> triggerMirror ()
        | _ -> ()

        let outcomeMap =
            [
                yield! alreadyImported |> Array.map (fun (txId, entryId) -> txId, AlreadyImported entryId)
                yield! linkedOutcomes
                yield! failed |> Array.map (fun (txId, msg) -> txId, LedgerWriteFailed msg)
            ]
            |> Map.ofList

        return { Outcomes = outcomeMap; SendResult = sendResultOpt }
    }

/// Fasst die `LedgerWriteFailed`-Fälle aus einem Push-Ergebnis zusammen: Anzahl und eine
/// zusammengefasste Fehlermeldung. `None`, wenn keine Buchung gescheitert ist. Pure,
/// extrahiert damit `Server.Api.importToYnab` das Sichtbarmachen eines gescheiterten
/// Kassenbuch-Schreibens (Session nicht abschließen, Fehler melden statt still
/// schlucken) ohne HTTP testen kann (Verifier-Fund, Iteration 3).
let summarizeLedgerWriteFailures (result: ComdirectPushResult) : (int * string) option =
    let failures =
        result.Outcomes
        |> Map.toList
        |> List.choose (fun (_, outcome) -> match outcome with LedgerWriteFailed msg -> Some msg | _ -> None)
    if failures.IsEmpty then None else Some (failures.Length, String.concat "; " failures)

/// Verdrahtet den Lauf mit dem echten YNAB-Client und den gespeicherten Einstellungen.
/// Die eine Stelle, durch die alle drei Auslöser aus ADR 0014 laufen: der manuelle
/// Knopf ("Jetzt spiegeln"), das Frische-Kriterium (Seitenaufruf/Vordergrund, über
/// denselben Endpunkt vom Client angestoßen) und der Lauf nach Push
/// (`triggerMirrorInBackground`). Der Einzel-Flug-Schutz (`runMirrorGuarded`) sorgt
/// dafür, dass zwei gleichzeitige Auslöser nur einen YNAB-Abruf auslösen.
let mirrorYnabBudget () : Async<Result<MirrorRunSummary, string>> =
    async {
        let! token = Core.Settings.getSetting "ynab_token"
        let! budget = Core.Settings.getSetting "ynab_default_budget_id"

        match token, budget with
        | None, _ -> return Error "Kein YNAB-Token hinterlegt"
        | _, None -> return Error "Kein Standard-Budget gewaehlt"
        | Some token, Some budget ->
            let budgetId = YnabBudgetId budget
            let fetchers = {
                FetchAccounts = fun knowledge -> Server.YnabClient.getAccountsDelta token budgetId knowledge
                FetchTransactions =
                    fun knowledge -> Server.YnabClient.getTransactionsDelta token budgetId knowledge
                FetchCategories = fun () -> Server.YnabClient.getCategories token budgetId
            }

            return! runMirrorGuarded DateTime.UtcNow fetchers budgetId
    }

/// Der Async-Rumpf von `triggerMirrorInBackground`, mit `run` injiziert: testbar ohne
/// echten Spiegel-Lauf und ohne `Async.Start`, sodass ein Test synchron auf das Ergebnis
/// warten kann. Fängt jede Ausnahme aus `run` ab, einschließlich einer, die schon vor
/// `runMirror`s eigenem try geworfen wird (z. B. `Core.Settings.getSetting`) oder aus
/// einem fehlgeschlagenen Task im Einzel-Flug-Schutz kommt: `Async.Start` lässt
/// Ausnahmen sonst unbehandelt auf dem Thread-Pool landen und kann den Prozess
/// mitreißen (Verifier-Fund Iteration 1). Best effort wird der Fehler zusätzlich als
/// Lauf-Status gespeichert (ADR 0014: Fehler sichtbar, nicht verschluckt); scheitert
/// auch das (z. B. weil die Datenbank selbst der Grund war), bleibt wenigstens die
/// Log-Ausgabe.
let runMirrorInBackground (run: unit -> Async<Result<MirrorRunSummary, string>>) : Async<unit> =
    async {
        try
            let! _ = run ()
            return ()
        with ex ->
            printfn "Spiegel-Lauf im Hintergrund fehlgeschlagen: %s" ex.Message
            try
                do! saveLastMirrorRun { RanAt = DateTime.UtcNow; Outcome = MirrorRunFailed ex.Message }
            with _ -> ()
    }

/// Stößt `mirrorYnabBudget` im Hintergrund an, ohne auf sein Ende zu warten: der
/// Aufrufer (die Push-Antwort an Roman) soll nicht auf einen weiteren YNAB-Roundtrip
/// warten (ADR 0014, "Roman wartet nie auf den Spiegel"). Der Einzel-Flug-Schutz in
/// `mirrorYnabBudget` sorgt dafür, dass ein gleichzeitiger zweiter Auslöser (z. B.
/// ein Seitenaufruf kurz danach) denselben Lauf teilt statt einen zweiten anzustoßen.
let triggerMirrorInBackground () : unit =
    Async.Start (runMirrorInBackground mirrorYnabBudget)

// ============================================
// Prüfvermerk setzen und nach YNAB zurückschreiben (ADR 0050, ledger-r777c)
// ============================================

/// Der Push des Prüfvermerks, injiziert wie `push` bei `addCashExpense`: bekommt die
/// YNAB-Transaction-Id, `approved` und `cleared` (nur wenn geändert, nie `reconciled`).
/// Der Aufrufer (`Server.Api`) bindet Token und Budget ein
/// (`Ynab.updateReviewMark`).
type ReviewMarkPush = string -> bool -> Ynab.ReviewMarkCleared option -> Async<YnabResult<unit>>

let private toReviewClearedState (c: ClearedState) : ReviewClearedState =
    match c with
    | Uncleared -> ReviewUncleared
    | Cleared -> ReviewCleared
    | Reconciled -> ReviewReconciled

let private fromReviewClearedState (c: ReviewClearedState) : ClearedState =
    match c with
    | ReviewUncleared -> Uncleared
    | ReviewCleared -> Cleared
    | ReviewReconciled -> Reconciled

let private toReviewMarkState (approved: bool, cleared: ClearedState) : ReviewMarkState =
    { IsApproved = approved; ClearedState = toReviewClearedState cleared }

/// Der Grund, wenn die Buchung noch keine YNAB-Verknüpfung hat (ADR 0050, Punkt 12).
let reviewMarkNotLinkedReason = "noch nicht in YNAB verknüpft"

/// Der `cleared`-Wert für den Push: nur, wenn er sich ändert, und nie `reconciled`
/// (ADR 0050, Punkt 6).
let private clearedForPush (oldCleared: ClearedState) (newCleared: ClearedState) : Ynab.ReviewMarkCleared option =
    if oldCleared = newCleared then None
    else
        match newCleared with
        | Cleared -> Some Ynab.SendCleared
        | Uncleared -> Some Ynab.SendUncleared
        | Reconciled -> None

/// Die aktuelle YNAB-Id aus den Verknüpfungen einer Buchung: die YNAB-Verknüpfung, die
/// nicht als abgelöst festgehalten ist (ADR 0058, Punkt 5). Nach einem Ersatz zeigen zwei
/// YNAB-Verknüpfungen auf die Buchung, nur die neue gilt. Rein.
let currentYnabTransactionId (links: EntryLink list) : string option =
    let superseded =
        links |> List.filter (fun l -> l.System = YnabSupersededSystem) |> List.map (fun l -> l.Value) |> Set.ofList
    links
    |> List.tryPick (fun l ->
        if l.System = YnabSystem && not (Set.contains l.Value superseded) then Some l.Value else None)

let private tryYnabTransactionId (entryId: EntryId) : Async<string option> =
    async {
        let! links = Ledger.Persistence.linksForEntry entryId
        return currentYnabTransactionId links
    }

let private reviewMarkEvent (entryId: EntryId) (now: DateTime) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = now; Actor = actor; Body = body }

/// Schickt den Prüfvermerk und schreibt bei einem Fehlschlag das Ereignis. Liefert das
/// Ergebnis des Zurückschreibens. Bei Erfolg schreibt diese Funktion kein Ereignis
/// (ADR 0050, Punkt 5), das tut nur das erneute Senden (`retryReviewMarkWriteBack`).
let private sendReviewMark
    (now: DateTime)
    (push: ReviewMarkPush)
    (entryId: EntryId)
    (approved: bool)
    (cleared: Ynab.ReviewMarkCleared option)
    : Async<ReviewMarkWriteBack> =
    async {
        let fail (reason: string) =
            async {
                do!
                    Ledger.Persistence.appendAndApply [
                        reviewMarkEvent entryId now UserInBb (EntryReviewMarkWriteBackFailed { Reason = reason })
                    ]
                return ReviewMarkSendFailed reason
            }

        match! tryYnabTransactionId entryId with
        | None -> return! fail reviewMarkNotLinkedReason
        | Some ynabTransactionId ->
            match! push ynabTransactionId approved cleared with
            | Ok () -> return ReviewMarkSent
            | Error err -> return! fail (ynabErrorText err)
    }

/// Gemeinsamer Ablauf für `setReviewMark` und `restoreReviewMark`: lädt die Buchung,
/// lehnt gelöschte ab, bestimmt den Zielzustand (`resolveTarget`), schreibt bei einer
/// Änderung zuerst `EntryReviewMarkChangedInBb` und ruft danach den Push (ADR 0050,
/// Punkt 3 bis 5, 12). Ziel gleich Ist: kein Ereignis, kein Push, `ReviewMarkUnchanged`.
let private changeReviewMark
    (now: DateTime)
    (push: ReviewMarkPush)
    (entryId: EntryId)
    (resolveTarget: Entry -> bool * ClearedState)
    : Async<Result<ReviewMarkOutcome, string>> =
    async {
        match! Ledger.Persistence.loadEntry entryId with
        | None -> return Error "Buchung nicht gefunden"
        | Some entry when entry.DeletedInYnab -> return Error "Die Buchung ist in YNAB gelöscht"
        | Some entry ->
            let previous = reviewMarkOf entry
            let newApproved, newCleared = resolveTarget entry
            let oldApproved, oldCleared = previous

            if newApproved = oldApproved && newCleared = oldCleared then
                return
                    Ok {
                        Previous = toReviewMarkState previous
                        Change = ReviewMarkUnchanged
                        WriteBack = ReviewMarkNotSent
                    }
            else
                do!
                    Ledger.Persistence.appendAndApply [
                        reviewMarkEvent entryId now UserInBb (
                            EntryReviewMarkChangedInBb {
                                OldApproved = oldApproved
                                NewApproved = newApproved
                                OldCleared = oldCleared
                                NewCleared = newCleared
                            }
                        )
                    ]

                let! writeBack = sendReviewMark now push entryId newApproved (clearedForPush oldCleared newCleared)

                return
                    Ok {
                        Previous = toReviewMarkState previous
                        Change = ReviewMarkChanged
                        WriteBack = writeBack
                    }
    }

/// "Geprüft" / "Ungeprüft" (ADR 0050). Kassenbuch zuerst, dann der Push; kein Rollback.
let setReviewMark
    (now: DateTime)
    (push: ReviewMarkPush)
    (entryId: EntryId)
    (target: ReviewMarkTarget)
    : Async<Result<ReviewMarkOutcome, string>> =
    changeReviewMark now push entryId (reviewMarkTarget target)

/// "Rückgängig": stellt `approved` und `cleared` des Vorzustands her. Fasst `Reconciled`
/// nie an (ADR 0050, Punkt 2 und 10).
let restoreReviewMark
    (now: DateTime)
    (push: ReviewMarkPush)
    (entryId: EntryId)
    (previous: ReviewMarkState)
    : Async<Result<ReviewMarkOutcome, string>> =
    let previousState = previous.IsApproved, fromReviewClearedState previous.ClearedState
    changeReviewMark now push entryId (reviewMarkRestoreTarget previousState)

/// "Erneut senden" am Hinweis "Prüfvermerk nicht in YNAB" (ADR 0050, Punkt 9): sendet
/// den aktuellen Stand. Erfolg schreibt `EntryReviewMarkWrittenBackToYnab` (Actor
/// `UserInBb`), ein erneuter Fehlschlag ersetzt den Grund. Ohne offenen Fehlschlag tut
/// der Befehl nichts.
let retryReviewMarkWriteBack
    (now: DateTime)
    (push: ReviewMarkPush)
    (entryId: EntryId)
    : Async<Result<ReviewMarkWriteBack, string>> =
    async {
        match! Ledger.Persistence.loadEntry entryId with
        | None -> return Error "Buchung nicht gefunden"
        | Some entry when entry.DeletedInYnab -> return Error "Die Buchung ist in YNAB gelöscht"
        | Some entry when entry.ReviewMarkWriteBackFailure.IsNone -> return Ok ReviewMarkNotSent
        | Some entry ->
            // Der Ist-Stand geht hinaus; `cleared` nur, wenn er nicht Reconciled ist.
            let cleared =
                match entry.Cleared with
                | Cleared -> Some Ynab.SendCleared
                | Uncleared -> Some Ynab.SendUncleared
                | Reconciled -> None

            match! tryYnabTransactionId entryId with
            | None ->
                do!
                    Ledger.Persistence.appendAndApply [
                        reviewMarkEvent entryId now UserInBb (
                            EntryReviewMarkWriteBackFailed { Reason = reviewMarkNotLinkedReason }
                        )
                    ]
                return Ok (ReviewMarkSendFailed reviewMarkNotLinkedReason)
            | Some ynabTransactionId ->
                match! push ynabTransactionId entry.Approved cleared with
                | Ok () ->
                    do!
                        Ledger.Persistence.appendAndApply [
                            reviewMarkEvent entryId now UserInBb EntryReviewMarkWrittenBackToYnab
                        ]
                    return Ok ReviewMarkSent
                | Error err ->
                    let reason = ynabErrorText err
                    do!
                        Ledger.Persistence.appendAndApply [
                            reviewMarkEvent entryId now UserInBb (EntryReviewMarkWriteBackFailed { Reason = reason })
                        ]
                    return Ok (ReviewMarkSendFailed reason)
    }

// ============================================
// Prüfvermerk für eine ganze Auswahl, ein Sammel-PATCH (ledger-nhwrt)
// ============================================

/// Der Sammel-Push des Prüfvermerks, injiziert wie `ReviewMarkPush`: bekommt je geänderter
/// Buchung YNAB-Id, `approved` und `cleared` (nur wenn geändert, nie `reconciled`) und
/// liefert die Ids, die YNAB nicht als gespeichert nennt. Ein `Error` trifft den ganzen
/// Stapel (`Ynab.updateReviewMarkBatch`).
type ReviewMarkBatchPush = Ynab.ReviewMarkBatchItem list -> Async<YnabResult<string list>>

/// Der Grund für eine Buchung, die YNAB in der Antwort des Sammel-PATCH nicht nennt.
let reviewMarkNotSavedReason = "YNAB hat die Buchung im Sammelaufruf nicht gespeichert"

/// Löst die Auswahl des Clients in Buchungs-Ids auf. `BatchPicked`: genau diese Ids.
/// `BatchAllExcept`: der Treffer der Seitenanfrage (alle Seiten, derselbe Filter wie
/// `getLedgerEntriesPage`) ohne die Ausnahmen, auch die Buchungen, die der Client nie
/// geladen hat.
let resolveReviewMarkBatchSelection (selection: ReviewMarkBatchSelection) : Async<EntryId list> =
    async {
        match selection with
        | BatchPicked ids -> return ids |> List.distinct |> List.map EntryId
        | BatchAllExcept (query, excluded) ->
            let accountId = query.AccountId |> Option.map LedgerAccountId
            let filter = Ledger.Persistence.entryFilterOfPageQuery query
            // Das `Sort` der Anfrage wird nicht gelesen (ADR 0051 Punkt 3): die aufgelöste Menge
            // hat keine Reihenfolge, und der teure F#-Pfad bleibt aus dem Stapel-Befehl heraus.
            let! entries = Ledger.Persistence.loadEntriesPage accountId 0 Int32.MaxValue filter LedgerSort.defaultSort
            let excludedSet = Set.ofList excluded
            return
                entries
                |> List.map (fun e -> e.Id)
                |> List.filter (fun (EntryId id) -> not (excludedSet.Contains id))
    }

type private BatchCandidate = {
    EntryId: EntryId
    Previous: bool * ClearedState
    Target: bool * ClearedState
    YnabTransactionId: string
}

/// Gemeinsamer Ablauf für `setReviewMarkBatch` und `restoreReviewMarkBatch`: prüft jede
/// Buchung wie `changeReviewMark` (gelöscht oder nicht verknüpft: gescheitert, ohne
/// Ereignis; Ziel gleich Ist: unverändert, ohne Ereignis), schreibt für jede geänderte
/// Buchung ein eigenes `EntryReviewMarkChangedInBb` und sendet danach EINEN Sammel-PATCH.
/// Scheitert er, trägt jede Buchung des PATCH `EntryReviewMarkWriteBackFailed` mit
/// demselben Grund; nennt die Antwort einzelne nicht gespeicherte Buchungen, gelten nur
/// diese als gescheitert. Ohne geänderte Buchung geht kein PATCH hinaus.
let private changeReviewMarkBatch
    (now: DateTime)
    (push: ReviewMarkBatchPush)
    (targets: (EntryId * (Entry -> bool * ClearedState)) list)
    : Async<Result<ReviewMarkBatchOutcome, string>> =
    async {
        let candidates = ResizeArray<BatchCandidate>()
        let mutable unchanged = 0
        let mutable failed = 0

        for entryId, resolveTarget in targets do
            match! Ledger.Persistence.loadEntry entryId with
            | None -> failed <- failed + 1
            | Some entry when entry.DeletedInYnab -> failed <- failed + 1
            | Some entry ->
                let previous = reviewMarkOf entry
                let target = resolveTarget entry
                if target = previous then
                    unchanged <- unchanged + 1
                else
                    match! tryYnabTransactionId entryId with
                    | None -> failed <- failed + 1
                    | Some ynabId ->
                        candidates.Add {
                            EntryId = entryId
                            Previous = previous
                            Target = target
                            YnabTransactionId = ynabId
                        }

        let changed = candidates |> Seq.toList

        if List.isEmpty changed then
            return Ok { Changed = []; UnchangedCount = unchanged; FailedCount = failed }
        else
            do!
                Ledger.Persistence.appendAndApply [
                    for c in changed ->
                        reviewMarkEvent c.EntryId now UserInBb (
                            EntryReviewMarkChangedInBb {
                                OldApproved = fst c.Previous
                                NewApproved = fst c.Target
                                OldCleared = snd c.Previous
                                NewCleared = snd c.Target
                            }
                        )
                ]

            let items : Ynab.ReviewMarkBatchItem list =
                [ for c in changed ->
                    { YnabTransactionId = c.YnabTransactionId
                      Approved = fst c.Target
                      Cleared = clearedForPush (snd c.Previous) (snd c.Target) } ]

            let! pushed = push items

            let reasonOf (c: BatchCandidate) : string option =
                match pushed with
                | Ok notSaved when notSaved |> List.contains c.YnabTransactionId -> Some reviewMarkNotSavedReason
                | Ok _ -> None
                | Error err -> Some (ynabErrorText err)

            let writeBacks = changed |> List.map (fun c -> c, reasonOf c)

            do!
                Ledger.Persistence.appendAndApply [
                    for c, reason in writeBacks do
                        match reason with
                        | Some r -> reviewMarkEvent c.EntryId now UserInBb (EntryReviewMarkWriteBackFailed { Reason = r })
                        | None -> ()
                ]

            let (outcomes: ReviewMarkBatchChange list) =
                [ for c, reason in writeBacks ->
                    let (EntryId id) = c.EntryId
                    { EntryId = id
                      Previous = toReviewMarkState c.Previous
                      WriteBack =
                        (match reason with
                         | Some r -> ReviewMarkSendFailed r
                         | None -> ReviewMarkSent) } ]

            return Ok { Changed = outcomes; UnchangedCount = unchanged; FailedCount = failed }
    }

/// "Geprüft" für die ganze Auswahl (ledger-nhwrt): löst die Auswahl auf dem Server auf
/// und setzt `Reviewed` je Buchung, mit einem Sammel-PATCH.
let setReviewMarkBatch
    (now: DateTime)
    (push: ReviewMarkBatchPush)
    (selection: ReviewMarkBatchSelection)
    : Async<Result<ReviewMarkBatchOutcome, string>> =
    async {
        let! ids = resolveReviewMarkBatchSelection selection
        return! changeReviewMarkBatch now push [ for id in ids -> id, reviewMarkTarget Reviewed ]
    }

/// Ein "Rückgängig" für den ganzen Stapel: stellt für jede genannte Buchung den Vorzustand
/// wieder her (Logik von `restoreReviewMark`, `Reconciled` bleibt unberührt), je ein
/// Ereignis und ein Sammel-PATCH. Buchungen, die schon im Vorzustand stehen, bleiben
/// unangetastet.
let restoreReviewMarkBatch
    (now: DateTime)
    (push: ReviewMarkBatchPush)
    (items: ReviewMarkRestoreItem list)
    : Async<Result<ReviewMarkBatchOutcome, string>> =
    changeReviewMarkBatch now push [
        for item in items ->
            let previousState = item.Previous.IsApproved, fromReviewClearedState item.Previous.ClearedState
            EntryId item.EntryId, reviewMarkRestoreTarget previousState
    ]

// ============================================
// Kategorie ändern und nach YNAB zurückschreiben (ADR 0055, ledger-k2rbn)
// ============================================

/// Der Push der Kategorie, injiziert wie `ReviewMarkPush`: bekommt die YNAB-Transaction-Id
/// und die Kategorie (`None` heißt "ohne Kategorie", im Body `category_id = null`). Der
/// Aufrufer (`Server.Api`) bindet Token und Budget ein (`Ynab.updateCategory`).
type CategoryPush = string -> YnabCategoryId option -> Async<YnabResult<unit>>

/// Der Grund-Text für den Hinweis "Kategorie nicht in YNAB". Wie
/// `ynabErrorText`, nur die Ablehnung nennt die Kategorie.
let describeCategoryPushError (error: YnabError) : string =
    match error with
    | YnabError.InvalidResponse msg -> $"YNAB hat die Kategorie abgelehnt: {msg}"
    | other -> ynabErrorText other

/// Der Grund, wenn die Buchung noch keine YNAB-Verknüpfung hat (ADR 0055, Punkt 4).
let categoryNotLinkedReason = reviewMarkNotLinkedReason

let private categoryFailureEventWith
    (entryId: EntryId)
    (now: DateTime)
    (reason: string)
    (baselineCategory: CategoryRef option, baselineSplits: TransactionSplit list)
    (ynabDeleted: string option)
    : LedgerEvent =
    reviewMarkEvent entryId now UserInBb (
        EntryCategorizationWriteBackFailed {
            Reason = reason
            BaselineCategory = baselineCategory
            BaselineSplits = baselineSplits
            YnabDeletedTransactionId = ynabDeleted
        }
    )

let private categoryFailureEvent entryId now reason baseline =
    categoryFailureEventWith entryId now reason baseline None

/// Die Baseline eines neuen Fehlschlags: bei einem schon offenen die erste, sonst der
/// Stand vor der Änderung (ADR 0055, Punkt 4).
let private categoryBaseline (entry: Entry) (before: CategoryRef option * TransactionSplit list) =
    match entry.CategorizationWriteBackFailure with
    | Some failure -> failure.BaselineCategory, failure.BaselineSplits
    | None -> before

/// Schickt die Kategorie. Scheitert der Push (oder fehlt die Verknüpfung), schreibt die
/// Funktion `EntryCategorizationWriteBackFailed`. Bei Erfolg schreibt sie kein Ereignis
/// (ADR 0055, Punkt 2), außer ein früherer Fehlschlag war offen: dann ist er mit dem
/// Erfolg beendet (`EntryCategorizationWrittenBackToYnab`).
let private pushCategory
    (now: DateTime)
    (push: CategoryPush)
    (entry: Entry)
    (baseline: CategoryRef option * TransactionSplit list)
    (target: YnabCategoryId option)
    : Async<CategoryWriteBack> =
    async {
        let entryId = entry.Id

        let fail (reason: string) =
            async {
                do! Ledger.Persistence.appendAndApply [ categoryFailureEvent entryId now reason baseline ]
                return CategorySendFailed reason
            }

        match! tryYnabTransactionId entryId with
        | None -> return! fail categoryNotLinkedReason
        | Some ynabTransactionId ->
            match! push ynabTransactionId target with
            | Ok () ->
                if entry.CategorizationWriteBackFailure.IsSome then
                    do!
                        Ledger.Persistence.appendAndApply [
                            reviewMarkEvent entryId now UserInBb EntryCategorizationWrittenBackToYnab
                        ]
                return CategorySent
            | Error err -> return! fail (describeCategoryPushError err)
    }

let private categoryChoiceOf (names: Map<Guid, string>) (category: CategoryRef) : CategoryChoice =
    {
        CategoryId = category.CategoryId
        CategoryName = Ledger.Persistence.liveCategoryName names category.CategoryId category.CategoryName
    }

/// Warum die Kategorie einer Buchung nicht änderbar ist (ADR 0055, Punkt 7), oder `None`.
/// Die eine Quelle der Prüfung: `categoryNotChangeableReason` (Text für den Einzelweg) und
/// `setEntryCategoryBatch` (Überspring-Grund) lesen beide diese Funktion.
let private categoryNotChangeable (entry: Entry) : CategorySkipReason option =
    if entry.DeletedInYnab then Some SkipDeletedInYnab
    elif not (List.isEmpty entry.Splits) then Some SkipSplit
    elif
        entry.CategorizationWriteBackFailure
        |> Option.exists (fun f -> not (List.isEmpty f.BaselineSplits))
    then
        // YNAB hält noch den alten Split (oder gar keine Buchung): eine Kategorie würde
        // dort still ignoriert (ADR 0058, Punkt 4). Erst "Erneut senden" im Split-Editor.
        Some SkipSplitNotAdjusted
    elif entry.Category.IsNone && (entry.TransferAccountId.IsSome || entry.TransferEntryId.IsSome) then
        Some SkipTransfer
    else None

/// Der Text zu einem Grund, warum die Kategorie einer Buchung nicht änderbar ist.
let private categoryNotChangeableText (reason: CategorySkipReason) : string =
    match reason with
    | SkipDeletedInYnab -> "Die Buchung ist in YNAB gelöscht"
    | SkipSplit -> "Die Buchung ist aufgeteilt, die Kategorien ändert der Split-Editor"
    | SkipSplitNotAdjusted -> "Der Split der Buchung ist in YNAB noch nicht angepasst, bitte erneut senden"
    | SkipTransfer -> "Ein Übertrag zwischen Budget-Konten hat keine Kategorie"
    | SkipNotFound -> "Buchung nicht gefunden"

let private categoryNotChangeableReason (entry: Entry) : string option =
    categoryNotChangeable entry |> Option.map categoryNotChangeableText

/// Der Befehl `setEntryCategory` (ADR 0055): lädt die Buchung, lehnt Unzulässiges ab
/// (Fehler, kein Ereignis), schreibt bei einer Änderung zuerst `EntryRecategorized`
/// (Actor `UserInBb`) und ruft danach den Push. Ziel gleich Ist (`categorizationIdentity`):
/// kein Ereignis, kein Push, `CategoryUnchanged`. Läuft unter der Schreib-Sperre
/// (`WriteGuard`), nie während eines Spiegel-Laufs.
let setEntryCategory
    (now: DateTime)
    (push: CategoryPush)
    (entryId: EntryId)
    (target: YnabCategoryId option)
    : Async<Result<CategoryChangeOutcome, string>> =
    WriteGuard.exclusive (
        async {
            match! Ledger.Persistence.loadEntry entryId with
            | None -> return Error "Buchung nicht gefunden"
            | Some entry ->
                match categoryNotChangeableReason entry with
                | Some reason -> return Error reason
                | None ->
                    let! names = Ledger.Persistence.loadCategoryNames ()

                    let targetRef : Result<CategoryRef option, string> =
                        match target with
                        | None -> Ok None
                        | Some categoryId ->
                            let (YnabCategoryId id) = categoryId
                            match Map.tryFind id names with
                            | Some name -> Ok (Some { CategoryId = categoryId; CategoryName = name })
                            | None -> Error "Unbekannte Kategorie"

                    match targetRef with
                    | Error reason -> return Error reason
                    | Ok newCategory ->
                        let previous = entry.Category |> Option.map (categoryChoiceOf names)

                        if categorizationIdentity entry.Category entry.Splits = categorizationIdentity newCategory [] then
                            return Ok { Previous = previous; Change = CategoryUnchanged; WriteBack = CategoryNotSent }
                        else
                            do!
                                Ledger.Persistence.appendAndApply [
                                    reviewMarkEvent entryId now UserInBb (
                                        EntryRecategorized {
                                            OldCategory = entry.Category
                                            NewCategory = newCategory
                                            OldSplits = []
                                            NewSplits = []
                                            OldPayee = entry.Payee
                                            NewPayee = entry.Payee
                                        }
                                    )
                                ]

                            let baseline = categoryBaseline entry (entry.Category, entry.Splits)
                            let! writeBack = pushCategory now push entry baseline target
                            return Ok { Previous = previous; Change = CategoryChanged; WriteBack = writeBack }
        }
    )

// ============================================
// Kategorie für eine ganze Auswahl, ein Sammel-PATCH (ledger-w29y6)
// ============================================

/// Der Sammel-Push der Kategorie, injiziert wie `ReviewMarkBatchPush`: bekommt je
/// geänderter, verknüpfter Buchung YNAB-Id und Ziel-Kategorie und liefert die Ids, die YNAB
/// nicht als gespeichert nennt. Ein `Error` trifft den ganzen Stapel (`Ynab.updateCategoryBatch`).
type CategoryBatchPush = Ynab.CategoryBatchItem list -> Async<YnabResult<string list>>

/// Der Grund für eine Buchung, die YNAB in der Antwort des Sammel-PATCH nicht nennt.
let categoryNotSavedReason = "YNAB hat die Buchung im Sammelaufruf nicht gespeichert"

/// Setzt die Kategorie (oder "ohne Kategorie") für die ganze Auswahl. Eine unbekannte
/// Ziel-Kategorie lehnt den ganzen Befehl ab (Fehler, kein Ereignis). Je Buchung wie
/// `setEntryCategory`: nicht änderbar (`categoryNotChangeable`) oder Ziel gleich Ist
/// (`categorizationIdentity`) wird übersprungen, ohne Ereignis und ohne PATCH. Für jede
/// geänderte Buchung steht ein eigenes `EntryRecategorized`, danach geht EIN Sammel-PATCH
/// mit den verknüpften hinaus (ohne verknüpfte kein PATCH). Eine nicht verknüpfte Buchung
/// bekommt `EntryCategorizationWriteBackFailed` (Grund `categoryNotLinkedReason`), scheitert
/// der PATCH, trägt jede Buchung darin einen Fehlschlag mit der Baseline aus
/// `categoryBaseline`; nennt die Antwort einzelne nicht gespeicherte Ids, gelten nur diese
/// als gescheitert. Erfolg bei offenem früheren Fehlschlag schreibt
/// `EntryCategorizationWrittenBackToYnab`. Läuft unter der Schreib-Sperre (`WriteGuard`).
let setEntryCategoryBatch
    (now: DateTime)
    (push: CategoryBatchPush)
    (selection: ReviewMarkBatchSelection)
    (target: YnabCategoryId option)
    : Async<Result<CategoryBatchOutcome, string>> =
    WriteGuard.exclusive (
        async {
            let! names = Ledger.Persistence.loadCategoryNames ()

            let targetRef : Result<CategoryRef option, string> =
                match target with
                | None -> Ok None
                | Some categoryId ->
                    let (YnabCategoryId id) = categoryId
                    match Map.tryFind id names with
                    | Some name -> Ok (Some { CategoryId = categoryId; CategoryName = name })
                    | None -> Error "Unbekannte Kategorie"

            match targetRef with
            | Error reason -> return Error reason
            | Ok newCategory ->
                let! ids = resolveReviewMarkBatchSelection selection
                let candidates = ResizeArray<Entry>()
                let skipped = ResizeArray<CategorySkipReason>()
                let mutable unchanged = 0

                for entryId in ids do
                    match! Ledger.Persistence.loadEntry entryId with
                    | None -> skipped.Add SkipNotFound
                    | Some entry ->
                        match categoryNotChangeable entry with
                        | Some reason -> skipped.Add reason
                        | None ->
                            if categorizationIdentity entry.Category entry.Splits = categorizationIdentity newCategory [] then
                                unchanged <- unchanged + 1
                            else
                                candidates.Add entry

                let changed = candidates |> Seq.toList

                if List.isEmpty changed then
                    return Ok { Changed = []; UnchangedCount = unchanged; Skipped = skipped |> Seq.toList }
                else
                    do!
                        Ledger.Persistence.appendAndApply [
                            for entry in changed ->
                                reviewMarkEvent entry.Id now UserInBb (
                                    EntryRecategorized {
                                        OldCategory = entry.Category
                                        NewCategory = newCategory
                                        OldSplits = []
                                        NewSplits = []
                                        OldPayee = entry.Payee
                                        NewPayee = entry.Payee
                                    }
                                )
                        ]

                    let linked = ResizeArray<Entry * string>()
                    let unlinked = ResizeArray<Entry>()

                    for entry in changed do
                        match! tryYnabTransactionId entry.Id with
                        | Some ynabId -> linked.Add((entry, ynabId))
                        | None -> unlinked.Add entry

                    let linked = linked |> Seq.toList

                    let! pushed =
                        if List.isEmpty linked then
                            async { return Ok [] }
                        else
                            push [ for _, ynabId in linked -> ({ YnabTransactionId = ynabId; CategoryId = target }: Ynab.CategoryBatchItem) ]

                    let reasonOfLinked (ynabId: string) : string option =
                        match pushed with
                        | Ok notSaved when notSaved |> List.contains ynabId -> Some categoryNotSavedReason
                        | Ok _ -> None
                        | Error err -> Some (describeCategoryPushError err)

                    let results : (Entry * string option) list =
                        [ for entry in changed ->
                            match linked |> List.tryFind (fun (e, _) -> e.Id = entry.Id) with
                            | Some (_, ynabId) -> entry, reasonOfLinked ynabId
                            | None -> entry, Some categoryNotLinkedReason ]

                    do!
                        Ledger.Persistence.appendAndApply [
                            for entry, reason in results do
                                match reason with
                                | Some r ->
                                    let baseline = categoryBaseline entry (entry.Category, entry.Splits)
                                    categoryFailureEvent entry.Id now r baseline
                                | None ->
                                    if entry.CategorizationWriteBackFailure.IsSome then
                                        reviewMarkEvent entry.Id now UserInBb EntryCategorizationWrittenBackToYnab
                        ]

                    let outcomes : CategoryBatchChange list =
                        [ for entry, reason in results ->
                            let (EntryId id) = entry.Id
                            { EntryId = id
                              Previous = entry.Category |> Option.map (categoryChoiceOf names)
                              WriteBack =
                                (match reason with
                                 | Some r -> CategorySendFailed r
                                 | None -> CategorySent) } ]

                    return Ok { Changed = outcomes; UnchangedCount = unchanged; Skipped = skipped |> Seq.toList }
        }
    )

/// "Erneut senden" am Hinweis "Kategorie nicht in YNAB" (ADR 0055, Punkt 4): sendet den
/// aktuellen BB-Stand. Erfolg schreibt `EntryCategorizationWrittenBackToYnab` (Actor
/// `UserInBb`), ein erneuter Fehlschlag ersetzt den Grund (die Baseline bleibt). Ohne
/// offenen Fehlschlag tut der Befehl nichts (`CategoryNotSent`).
let retryCategorizationWriteBack
    (now: DateTime)
    (push: CategoryPush)
    (entryId: EntryId)
    : Async<Result<CategoryWriteBack, string>> =
    WriteGuard.exclusive (
        async {
            match! Ledger.Persistence.loadEntry entryId with
            | None -> return Error "Buchung nicht gefunden"
            | Some entry when entry.DeletedInYnab -> return Error "Die Buchung ist in YNAB gelöscht"
            | Some entry when entry.CategorizationWriteBackFailure.IsNone -> return Ok CategoryNotSent
            | Some entry when not (List.isEmpty entry.Splits) ->
                return Error "Die Buchung ist aufgeteilt, die Kategorien ändert der Split-Editor"
            | Some entry ->
                let baseline = categoryBaseline entry (entry.Category, entry.Splits)
                let target = entry.Category |> Option.map (fun c -> c.CategoryId)
                let! writeBack = pushCategory now push entry baseline target
                return Ok writeBack
        }
    )

// ============================================
// Split-Schreibweg (ADR 0058, ledger-kxme4)
// ============================================

/// Die YNAB-Aufrufe des Split-Schreibwegs, injiziert wie `CategoryPush`. Der Aufrufer
/// (`Server.Api`) bindet Token und Budget ein; ein Test ersetzt sie durch Aufzeichner.
type SplitWriteTools = {
    /// Ist das Konto (YNAB-Id) ein Tracking-Konto? Auf Tracking-Konten gibt es keine Splits.
    IsTrackingAccount: YnabAccountId -> Async<YnabResult<bool>>
    /// Eine Zeile mit Kategorie auf einer in YNAB unaufgeteilten Buchung: gewöhnlicher
    /// Kategorie-PUT (wie `setEntryCategory`).
    PutCategory: CategoryPush
    /// Aufteilen einer in YNAB unaufgeteilten Buchung per PUT (ADR 0058, Punkt 1). `Error`
    /// auch dann, wenn die Antwort keine `subtransactions` trägt.
    PutSplits: string -> TransactionSplit list -> Async<YnabResult<unit>>
    /// DELETE der alten Buchung (ADR 0058, Punkt 2, erster Schritt).
    Delete: string -> Async<YnabResult<unit>>
    /// POST der Ersatzbuchung (zweiter Schritt), liefert die neue YNAB-Id.
    Create: Ynab.SplitReplacement -> Async<YnabResult<string>>
}

/// Das längste `import_id`, das YNAB annimmt.
[<Literal>]
let ImportIdMaxLength = 36

let private replacementSuffix = System.Text.RegularExpressions.Regex(@"^(.*):r(\d+)$")

/// Zerlegt eine `import_id` in Stamm und Zahl der bisherigen Ersätze (`BB:abc:r2` ist
/// `BB:abc` und 2; ohne Suffix ist die Zahl 0). Rein.
let private splitReplacementSuffix (importId: string) : string * int =
    let m = replacementSuffix.Match importId
    if m.Success then m.Groups.[1].Value, int m.Groups.[2].Value else importId, 0

/// Die aktuelle `import_id` einer Buchung: unter ihren `import_id`-Verknüpfungen die mit der
/// höchsten Ersatz-Zahl (ADR 0058, Punkt 2). Rein.
let currentImportId (links: EntryLink list) : string option =
    links
    |> List.filter (fun l -> l.System = ImportIdSystem)
    |> List.sortBy (fun l -> snd (splitReplacementSuffix l.Value))
    |> List.tryLast
    |> Option.map (fun l -> l.Value)

/// Die `import_id` der Ersatzbuchung: die aktuelle plus `:r<n>`, n ist die Zahl der
/// bisherigen Ersätze plus 1 (ADR 0058, Punkt 2). Ohne `import_id` keine. Rein.
let replacementImportId (current: string option) : string option =
    current
    |> Option.map (fun id ->
        let stem, replaced = splitReplacementSuffix id
        $"{stem}:r{replaced + 1}")

/// Die Form einer Split-Anfrage nach der Prüfung: eine Kategorie (Aufheben oder
/// gewöhnliche Kategorie) oder mindestens zwei Zeilen.
type SplitRequest =
    | SingleLine of CategoryRef option
    | MultipleLines of TransactionSplit list

let private formatAmount (value: decimal) : string =
    value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)

/// Prüft die Zeilen einer Split-Anfrage gegen den Buchungsbetrag (ADR 0058, Punkt 1; die
/// Invarianten stehen in `Shared.Domain.mkSplits`, ADR 0006). Leere Liste: abgelehnt. Eine
/// Zeile: Kategorie oder unkategorisiert, voller Betrag; eine einzelne Übertrags-Zeile ist
/// abgelehnt. Zwei oder mehr: `mkSplits`. Rein. Die Kategorie-Namen prüft der Befehl danach
/// gegen den Bestand (`SingleLine` trägt hier nur die Id mit dem übergebenen Namen).
let classifySplitLines (total: Money) (lines: TransactionSplit list) : Result<SplitRequest, string> =
    match lines with
    | [] -> Error "Es fehlen die Zeilen des Splits"
    | [ single ] ->
        if single.Amount.Currency <> total.Currency || abs (single.Amount.Amount - total.Amount) > 0.01m then
            Error "Eine einzelne Zeile muss den ganzen Buchungsbetrag tragen"
        else
            match single.Target with
            | ToTransfer _ -> Error "Eine einzelne Übertrags-Zeile ist kein Split"
            | ToCategory (id, name) -> Ok (SingleLine (Some { CategoryId = id; CategoryName = name }))
            | Uncategorized -> Ok (SingleLine None)
    | many ->
        match mkSplits total many with
        | Ok splits -> Ok (MultipleLines splits)
        | Error (SumMismatch (expected, actual)) ->
            Error $"Die Summe der Zeilen ({formatAmount actual}) weicht vom Betrag ({formatAmount expected}) ab"
        | Error CurrencyMismatch -> Error "Die Zeilen haben nicht dieselbe Währung wie die Buchung"
        | Error (TooFewLines _) -> Error "Ein Split braucht mindestens zwei Zeilen"

/// Setzt die Namen der Kategorien aus dem Bestand ein und lehnt unbekannte ab.
let private resolveSplitNames
    (names: Map<Guid, string>)
    (request: SplitRequest)
    : Result<CategoryRef option * TransactionSplit list, string> =
    let live (YnabCategoryId id as categoryId) (name: string) =
        match Map.tryFind id names with
        | Some current -> Some (categoryId, Ledger.Persistence.liveCategoryName names categoryId name)
        | None -> None

    match request with
    | SingleLine None -> Ok (None, [])
    | SingleLine (Some category) ->
        match live category.CategoryId category.CategoryName with
        | Some (id, name) -> Ok (Some { CategoryId = id; CategoryName = name }, [])
        | None -> Error "Unbekannte Kategorie"
    | MultipleLines lines ->
        let resolved =
            lines
            |> List.map (fun line ->
                match line.Target with
                | ToCategory (id, name) ->
                    match live id name with
                    | Some (resolvedId, resolvedName) ->
                        Ok { line with Target = ToCategory (resolvedId, resolvedName) }
                    | None -> Error "Unbekannte Kategorie"
                | _ -> Ok line)
        match resolved |> List.tryPick (function Error e -> Some e | Ok _ -> None) with
        | Some e -> Error e
        | None -> Ok (None, resolved |> List.choose (function Ok l -> Some l | Error _ -> None))

/// Warum eine Buchung nicht aufgeteilt oder in ihrem Split geändert werden kann (ADR 0058,
/// Punkt 6 und 7), oder `None`. Rein.
let splitNotChangeableReason (entry: Entry) : string option =
    if entry.DeletedInYnab then Some "Die Buchung ist in YNAB gelöscht"
    elif entry.Cleared = Reconciled then Some "Abgeglichene Buchungen lassen sich nicht aufteilen"
    elif entry.Category.IsNone
         && List.isEmpty entry.Splits
         && (entry.TransferAccountId.IsSome || entry.TransferEntryId.IsSome) then
        Some "Ein Übertrag zwischen Budget-Konten lässt sich nicht aufteilen"
    else None

/// Die Texte der Sperren, vom Befehl und vom Lesepfad gemeinsam benutzt.
let trackingSplitReason = "Auf einem Tracking-Konto lassen sich Buchungen nicht aufteilen"

let foreignImportIdChangeReason =
    "Eine Split-Buchung mit einer import_id aus YNAB lässt sich nur aufteilen, nicht ändern oder aufheben"

/// Die Sperren des Split-Schreibwegs für den Lesepfad (ledger-pafkp), nach denselben Regeln wie
/// `setEntrySplits`: lokale Sperren aus `splitNotChangeableReason`, Tracking-Konto, und eine
/// Fremd-`import_id` (sperrt nur Ändern und Aufheben, und nur solange das DELETE nicht schon
/// gelungen ist; bei einer unaufgeteilten Buchung heißt das: eine Aufteilung ließe sich danach
/// nicht mehr zurücknehmen, der Client lässt "Rückgängig" dann weg). `isTracking` und
/// `importId` kommen von außen (Konten-Abruf, Verknüpfungen), die Funktion bleibt rein.
/// `None`, wenn nichts gesperrt und nichts offen ist.
let splitLocksOf (isTracking: bool) (importId: string option) (entry: Entry) : SplitLocks option =
    let cannotSplit =
        match splitNotChangeableReason entry with
        | Some reason -> Some reason
        | None -> if isTracking then Some trackingSplitReason else None

    let pendingDeleted =
        entry.CategorizationWriteBackFailure
        |> Option.bind (fun f -> f.YnabDeletedTransactionId)

    let foreign =
        pendingDeleted.IsNone
        && (match importId with
            | Some id -> not (id.StartsWith $"{ImportIdPrefix}:")
            | None -> false)

    let cannotChange =
        match cannotSplit with
        | Some reason -> Some reason
        | None -> if foreign then Some foreignImportIdChangeReason else None

    if cannotSplit.IsNone && cannotChange.IsNone && pendingDeleted.IsNone then
        None
    else
        Some {
            CannotSplitReason = cannotSplit
            CannotChangeReason = cannotChange
            ReplacementPending = pendingDeleted.IsSome
        }

/// Der Grund-Text für den Hinweis "Kategorie nicht in YNAB" beim Split-Schreibweg.
let describeSplitPushError (error: YnabError) : string =
    match error with
    | YnabError.InvalidResponse msg -> $"YNAB hat die Aufteilung abgelehnt: {msg}"
    | other -> ynabErrorText other

let private clearedToYnab (cleared: ClearedState) : string =
    match cleared with
    | Uncleared -> "uncleared"
    | Cleared -> "cleared"
    | Reconciled -> "reconciled"

/// Das YNAB-Konto eines Kassenbuch-Kontos, wenn es ein gespiegeltes ist.
let private ynabAccountOf (accountId: LedgerAccountId) : Async<YnabAccountId option> =
    async {
        let! accounts = Ledger.Persistence.loadAccounts ()
        return
            accounts
            |> List.tryFind (fun a -> a.Id = accountId)
            |> Option.bind (fun a ->
                match a.Link with
                | YnabMirrored id -> Some id
                | BbOwned -> None)
    }

/// Schickt den BB-Stand einer Buchung (`entry`, nach dem Ereignis) nach YNAB. Der Weg
/// folgt dem, was YNAB vermutlich hält (die Baseline, ADR 0055 Punkt 4): ohne Split dort
/// ein PUT (Kategorie oder Aufteilen), mit Split dort DELETE und POST (ADR 0058). Scheitert
/// ein Schritt, schreibt die Funktion `EntryCategorizationWriteBackFailed`; ist das DELETE
/// gelungen und das POST gescheitert, trägt der Fehlschlag die gelöschte Id, und
/// "Erneut senden" ruft nur noch das POST (`pendingDeleted`). Erfolg beendet einen vorher
/// offenen Fehlschlag (`EntryCategorizationWrittenBackToYnab`), ein Ersatz schreibt
/// zusätzlich `EntryReplacedInYnab`.
let private pushSplitState
    (now: DateTime)
    (tools: SplitWriteTools)
    (entry: Entry)
    (baseline: CategoryRef option * TransactionSplit list)
    (hadOpenFailure: bool)
    (pendingDeleted: string option)
    : Async<CategoryWriteBack> =
    async {
        let entryId = entry.Id

        let fail (reason: string) (deleted: string option) =
            async {
                do! Ledger.Persistence.appendAndApply [ categoryFailureEventWith entryId now reason baseline deleted ]
                return CategorySendFailed reason
            }

        let succeed (extra: LedgerEvent list) =
            async {
                let ended =
                    if hadOpenFailure then
                        [ reviewMarkEvent entryId now UserInBb EntryCategorizationWrittenBackToYnab ]
                    else
                        []
                let events = extra @ ended
                if not (List.isEmpty events) then
                    do! Ledger.Persistence.appendAndApply events
                return CategorySent
            }

        match! tryYnabTransactionId entryId with
        | None -> return! fail categoryNotLinkedReason None
        | Some currentId ->
            if List.isEmpty (snd baseline) then
                let! result =
                    if List.isEmpty entry.Splits then
                        tools.PutCategory currentId (entry.Category |> Option.map (fun c -> c.CategoryId))
                    else
                        tools.PutSplits currentId entry.Splits
                match result with
                | Ok () -> return! succeed []
                | Error err -> return! fail (describeSplitPushError err) None
            else
                let! deleteResult =
                    match pendingDeleted with
                    | Some _ -> async.Return (Ok ())
                    | None -> tools.Delete currentId

                match deleteResult with
                | Error err -> return! fail (describeSplitPushError err) None
                | Ok () ->
                    let oldId = defaultArg pendingDeleted currentId
                    let newlyDeleted = if pendingDeleted.IsNone then Some oldId else None
                    let! links = Ledger.Persistence.linksForEntry entryId
                    let newImportId = replacementImportId (currentImportId links)

                    match! ynabAccountOf entry.AccountId with
                    | None -> return! fail "Das Konto der Buchung ist nicht mit YNAB verknüpft" newlyDeleted
                    | Some ynabAccount ->
                        let request : Ynab.SplitReplacement = {
                            AccountId = ynabAccount
                            Date = entry.Date
                            Amount = entry.Amount
                            Payee = entry.Payee
                            Memo = entry.Memo
                            FlagColor = entry.FlagColor
                            Cleared = clearedToYnab entry.Cleared
                            Approved = entry.Approved
                            ImportId = newImportId
                            CategoryId = entry.Category |> Option.map (fun c -> c.CategoryId)
                            Splits = entry.Splits
                        }

                        match! tools.Create request with
                        | Ok newId ->
                            return!
                                succeed [
                                    reviewMarkEvent entryId now UserInBb (
                                        EntryReplacedInYnab {
                                            OldYnabTransactionId = oldId
                                            NewYnabTransactionId = newId
                                            NewImportId = newImportId
                                        }
                                    )
                                ]
                        | Error err -> return! fail (describeSplitPushError err) newlyDeleted
    }

/// Der Befehl `setEntrySplits` (ADR 0058): lädt die Buchung, lehnt Unzulässiges ab (Fehler,
/// kein Ereignis, kein Schreib-Aufruf an YNAB), schreibt bei einer Änderung zuerst
/// `EntryRecategorized` (Actor `UserInBb`, mit `NewSplits` bzw. beim Aufheben `NewCategory`)
/// und ruft danach den Push (`pushSplitState`). Ziel gleich Ist: kein Ereignis, kein Push.
/// Läuft unter der Schreib-Sperre (`WriteGuard`), nie während eines Spiegel-Laufs.
let setEntrySplits
    (now: DateTime)
    (tools: SplitWriteTools)
    (entryId: EntryId)
    (lines: TransactionSplit list)
    : Async<Result<CategoryChangeOutcome, string>> =
    WriteGuard.exclusive (
        async {
            match! Ledger.Persistence.loadEntry entryId with
            | None -> return Error "Buchung nicht gefunden"
            | Some entry ->
                match splitNotChangeableReason entry with
                | Some reason -> return Error reason
                | None ->
                    match classifySplitLines entry.Amount lines with
                    | Error reason -> return Error reason
                    | Ok request ->
                        let! names = Ledger.Persistence.loadCategoryNames ()

                        match resolveSplitNames names request with
                        | Error reason -> return Error reason
                        | Ok (newCategory, newSplits) ->
                            let! links = Ledger.Persistence.linksForEntry entryId
                            let previous = entry.Category |> Option.map (categoryChoiceOf names)

                            match currentYnabTransactionId links with
                            | None -> return Error "Die Buchung ist noch nicht mit YNAB verknüpft"
                            | Some _ ->
                                let baseline = categoryBaseline entry (entry.Category, entry.Splits)
                                let pendingDeleted =
                                    entry.CategorizationWriteBackFailure
                                    |> Option.bind (fun f -> f.YnabDeletedTransactionId)
                                // YNAB hält die Buchung (vermutlich) aufgeteilt: Ändern und
                                // Aufheben laufen über DELETE und POST (ADR 0058, Punkt 2).
                                let replaceRoute = not (List.isEmpty (snd baseline))

                                if categorizationIdentity entry.Category entry.Splits
                                   = categorizationIdentity newCategory newSplits then
                                    return Ok { Previous = previous; Change = CategoryUnchanged; WriteBack = CategoryNotSent }
                                else
                                    let foreignImportId =
                                        match currentImportId links with
                                        | Some id when not (id.StartsWith $"{ImportIdPrefix}:") -> Some id
                                        | _ -> None

                                    let tooLongImportId =
                                        replacementImportId (currentImportId links)
                                        |> Option.exists (fun id -> id.Length > ImportIdMaxLength)

                                    if replaceRoute && pendingDeleted.IsNone && foreignImportId.IsSome then
                                        return Error foreignImportIdChangeReason
                                    elif replaceRoute && tooLongImportId then
                                        return
                                            Error
                                                $"Die import_id wäre mit dem Suffix länger als {ImportIdMaxLength} Zeichen"
                                    else
                                        let! ynabAccount = ynabAccountOf entry.AccountId

                                        let! tracking =
                                            match ynabAccount with
                                            | Some account -> tools.IsTrackingAccount account
                                            | None -> async.Return (Ok false)

                                        match tracking with
                                        | Ok true -> return Error trackingSplitReason
                                        | Error err ->
                                            return Error $"Das Konto konnte nicht geprüft werden: {describeSplitPushError err}"
                                        | Ok false ->
                                            do!
                                                Ledger.Persistence.appendAndApply [
                                                    reviewMarkEvent entryId now UserInBb (
                                                        EntryRecategorized {
                                                            OldCategory = entry.Category
                                                            NewCategory = newCategory
                                                            OldSplits = entry.Splits
                                                            NewSplits = newSplits
                                                            OldPayee = entry.Payee
                                                            NewPayee = entry.Payee
                                                        }
                                                    )
                                                ]

                                            let! updated = Ledger.Persistence.loadEntry entryId
                                            let! writeBack =
                                                pushSplitState
                                                    now
                                                    tools
                                                    (Option.get updated)
                                                    baseline
                                                    entry.CategorizationWriteBackFailure.IsSome
                                                    pendingDeleted
                                            return Ok { Previous = previous; Change = CategoryChanged; WriteBack = writeBack }
        }
    )

/// "Erneut senden" am Hinweis "Kategorie nicht in YNAB", für Kategorie und Split zugleich
/// (ADR 0055 Punkt 4, ADR 0058 Punkt 3). Hält YNAB (laut Baseline) keinen Split und trägt die
/// Buchung keinen, ist es der Kategorie-Weg (`retryCategorizationWriteBack`); sonst der
/// Split-Weg: PUT zum Aufteilen, oder bei einem Split in YNAB DELETE und POST, bzw. nur das
/// POST, wenn das DELETE schon gelungen ist.
let retryCategorizationOrSplitWriteBack
    (now: DateTime)
    (categoryPush: CategoryPush)
    (tools: SplitWriteTools)
    (entryId: EntryId)
    : Async<Result<CategoryWriteBack, string>> =
    WriteGuard.exclusive (
        async {
            match! Ledger.Persistence.loadEntry entryId with
            | None -> return Error "Buchung nicht gefunden"
            | Some entry when entry.DeletedInYnab -> return Error "Die Buchung ist in YNAB gelöscht"
            | Some entry ->
                match entry.CategorizationWriteBackFailure with
                | None -> return Ok CategoryNotSent
                | Some failure ->
                    let baseline = categoryBaseline entry (entry.Category, entry.Splits)
                    if List.isEmpty failure.BaselineSplits && List.isEmpty entry.Splits then
                        let target = entry.Category |> Option.map (fun c -> c.CategoryId)
                        let! writeBack = pushCategory now categoryPush entry baseline target
                        return Ok writeBack
                    else
                        let! writeBack =
                            pushSplitState now tools entry baseline true failure.YnabDeletedTransactionId
                        return Ok writeBack
        }
    )

// ============================================
// Lena fragen: Flag setzen und nach YNAB zurückschreiben (ADR 0056, ledger-xfjcp)
// ============================================

/// Der Push des Flags, injiziert wie `CategoryPush`: bekommt die YNAB-Transaction-Id und die
/// Farbe (`None` heißt "kein Flag", im Body `flag_color = null`). Der Name geht nie hinaus
/// (ADR 0056, Punkt 4). Der Aufrufer (`Server.Api`) bindet Token und Budget ein
/// (`Ynab.updateFlag`).
type FlagPush = string -> string option -> Async<YnabResult<unit>>

/// Der Grund-Text für den Hinweis "Flag nicht in YNAB". Wie `ynabErrorText`,
/// nur die Ablehnung nennt das Flag.
let describeFlagPushError (error: YnabError) : string =
    match error with
    | YnabError.InvalidResponse msg -> $"YNAB hat das Flag abgelehnt: {msg}"
    | other -> ynabErrorText other

/// Der Grund, wenn die Buchung noch keine YNAB-Verknüpfung hat (ADR 0056, Punkt 5).
let flagNotLinkedReason = reviewMarkNotLinkedReason

let private flagPairOf (entry: Entry) : string option * string option = entry.FlagColor, entry.FlagName

let private flagStateOf (entry: Entry) : FlagState = { Color = entry.FlagColor; Name = entry.FlagName }

/// Die Baseline eines neuen Fehlschlags: bei einem schon offenen die erste, sonst der Stand
/// vor der Änderung (ADR 0056, Nachtrag).
let private flagBaseline (entry: Entry) (before: string option * string option) =
    match entry.FlagWriteBackFailure with
    | Some failure -> failure.BaselineFlagColor, failure.BaselineFlagName
    | None -> before

/// Schickt die Farbe. Scheitert der Push (oder fehlt die Verknüpfung), schreibt die Funktion
/// `EntryFlagWriteBackFailed`. Bei Erfolg schreibt sie kein Ereignis (ADR 0056, Punkt 4),
/// außer ein früherer Fehlschlag war offen: dann ist er mit dem Erfolg beendet
/// (`EntryFlagWrittenBackToYnab`).
let private pushFlag
    (now: DateTime)
    (push: FlagPush)
    (entry: Entry)
    (baseline: string option * string option)
    (color: string option)
    : Async<FlagWriteBack> =
    async {
        let entryId = entry.Id

        let fail (reason: string) =
            async {
                let baselineColor, baselineName = baseline
                do!
                    Ledger.Persistence.appendAndApply [
                        reviewMarkEvent entryId now UserInBb (
                            EntryFlagWriteBackFailed {
                                Reason = reason
                                BaselineFlagColor = baselineColor
                                BaselineFlagName = baselineName
                            }
                        )
                    ]
                return FlagSendFailed reason
            }

        match! tryYnabTransactionId entryId with
        | None -> return! fail flagNotLinkedReason
        | Some ynabTransactionId ->
            match! push ynabTransactionId color with
            | Ok () ->
                if entry.FlagWriteBackFailure.IsSome then
                    do!
                        Ledger.Persistence.appendAndApply [
                            reviewMarkEvent entryId now UserInBb EntryFlagWrittenBackToYnab
                        ]
                return FlagSent
            | Error err -> return! fail (describeFlagPushError err)
    }

/// Gemeinsamer Ablauf für `setLenaFlag` und `restoreFlag`: lädt die Buchung, lehnt gelöschte
/// ab, bestimmt das Ziel (`resolveTarget`: `Error` lehnt ab, `Ok None` heißt nichts zu tun),
/// schreibt bei einer Änderung zuerst `EntryFlagChangedInBb` und ruft danach den Push
/// (ADR 0056, Punkt 2 bis 5). Ziel gleich Ist: kein Ereignis, kein Push, `FlagUnchanged`.
/// Läuft unter der Schreib-Sperre (`WriteGuard`), nie während eines Spiegel-Laufs.
let private changeFlag
    (now: DateTime)
    (push: FlagPush)
    (entryId: EntryId)
    (resolveTarget: Entry -> Result<(string option * string option) option, string>)
    : Async<Result<FlagOutcome, string>> =
    WriteGuard.exclusive (
        async {
            match! Ledger.Persistence.loadEntry entryId with
            | None -> return Error "Buchung nicht gefunden"
            | Some entry when entry.DeletedInYnab -> return Error "Die Buchung ist in YNAB gelöscht"
            | Some entry ->
                match resolveTarget entry with
                | Error reason -> return Error reason
                | Ok None ->
                    return Ok { Previous = flagStateOf entry; Change = FlagUnchanged; WriteBack = FlagNotSent }
                | Ok (Some (newColor, newName)) when (newColor, newName) = flagPairOf entry ->
                    return Ok { Previous = flagStateOf entry; Change = FlagUnchanged; WriteBack = FlagNotSent }
                | Ok (Some (newColor, newName)) ->
                    do!
                        Ledger.Persistence.appendAndApply [
                            reviewMarkEvent entryId now UserInBb (
                                EntryFlagChangedInBb {
                                    OldFlagColor = entry.FlagColor
                                    OldFlagName = entry.FlagName
                                    NewFlagColor = newColor
                                    NewFlagName = newName
                                }
                            )
                        ]

                    let baseline = flagBaseline entry (flagPairOf entry)
                    let! writeBack = pushFlag now push entry baseline newColor
                    return Ok { Previous = flagStateOf entry; Change = FlagChanged; WriteBack = writeBack }
        }
    )

/// Das Ziel-Flag einer Buchung für "Lena fragen" bzw. "Rückfrage zurücknehmen" (ADR 0056):
/// `Error` lehnt ab (fehlende Farbe), `Ok None` heißt nichts zu tun, `Ok (Some (Farbe, Name))`
/// ist das neue Flag. Die eine Quelle für den Einzelweg `setLenaFlag` und den Stapel
/// `setLenaFlagBatch`.
let private lenaFlagTargetOf
    (lenaColor: string option)
    (target: LenaFlagTarget)
    (entry: Entry)
    : Result<(string option * string option) option, string> =
    let isLena = isLenaFlagName entry.FlagName
    match target with
    | AssignLena ->
        match lenaColor with
        | Some color when List.contains color ynabFlagColors ->
            if isLena then Ok None else Ok (Some (Some color, Some lenaFlagName))
        | _ -> Error lenaColorMissingReason
    | WithdrawLena -> if isLena then Ok (Some (None, None)) else Ok None

/// "Lena fragen" und "Rückfrage zurücknehmen" (ADR 0056). `lenaColor` ist die Einstellung
/// `lena_flag_color`.
///
/// `AssignLena`: ohne gültige Farbe abgelehnt (Fehler, kein Ereignis); deutet der Ist-Stand
/// schon als *Rückfrage Lena* (Flag-Name "Lena"), nichts zu tun; sonst schreibt der Befehl
/// Farbe und Name "Lena" und überschreibt ein anderes Flag (*Markiert*). `WithdrawLena`:
/// schreibt "kein Flag", aber nur, wenn die Buchung *Rückfrage Lena* ist; ein anderes Flag
/// bleibt stehen, weil "Rückfrage zurücknehmen" nur die Lena-Rückfrage meint.
let setLenaFlag
    (now: DateTime)
    (push: FlagPush)
    (lenaColor: string option)
    (entryId: EntryId)
    (target: LenaFlagTarget)
    : Async<Result<FlagOutcome, string>> =
    changeFlag now push entryId (lenaFlagTargetOf lenaColor target)

/// "Rückgängig": stellt Farbe und Name des Vorzustands her, auch ein anderes Flag wie
/// *Markiert* (ADR 0056, Punkt 2). Der Push sendet nur die Farbe.
let restoreFlag
    (now: DateTime)
    (push: FlagPush)
    (entryId: EntryId)
    (previous: FlagState)
    : Async<Result<FlagOutcome, string>> =
    changeFlag now push entryId (fun _ -> Ok (Some (previous.Color, previous.Name)))

/// "Erneut senden" am Hinweis "Flag nicht in YNAB" (ADR 0056, Punkt 5): sendet die Farbe des
/// aktuellen BB-Stands. Erfolg schreibt `EntryFlagWrittenBackToYnab` (Actor `UserInBb`), ein
/// erneuter Fehlschlag ersetzt den Grund (die Baseline bleibt). Ohne offenen Fehlschlag tut
/// der Befehl nichts (`FlagNotSent`).
let retryFlagWriteBack
    (now: DateTime)
    (push: FlagPush)
    (entryId: EntryId)
    : Async<Result<FlagWriteBack, string>> =
    WriteGuard.exclusive (
        async {
            match! Ledger.Persistence.loadEntry entryId with
            | None -> return Error "Buchung nicht gefunden"
            | Some entry when entry.DeletedInYnab -> return Error "Die Buchung ist in YNAB gelöscht"
            | Some entry when entry.FlagWriteBackFailure.IsNone -> return Ok FlagNotSent
            | Some entry ->
                let baseline = flagBaseline entry (flagPairOf entry)
                let! writeBack = pushFlag now push entry baseline entry.FlagColor
                return Ok writeBack
        }
    )

// ============================================
// Memo bearbeiten und nach YNAB zurückschreiben (ADR 0062, ledger-fa1fp)
// ============================================

/// Der Push des Memos, injiziert wie `FlagPush`: bekommt die YNAB-Transaction-Id und das Memo
/// (`None` heißt "kein Memo", im Body `memo = null`). Der Aufrufer (`Server.Api`) bindet Token
/// und Budget ein (`Ynab.updateMemo`).
type MemoPush = string -> string option -> Async<YnabResult<unit>>

/// Der Grund-Text für den Hinweis "Memo nicht in YNAB". Wie `describeFlagPushError`.
let describeMemoPushError (error: YnabError) : string =
    match error with
    | YnabError.InvalidResponse msg -> $"YNAB hat das Memo abgelehnt: {msg}"
    | other -> ynabErrorText other

/// Der Grund, wenn die Buchung noch keine YNAB-Verknüpfung hat (ADR 0062).
let memoNotLinkedReason = reviewMarkNotLinkedReason

/// Die Baseline eines neuen Fehlschlags: bei einem schon offenen die erste, sonst der Stand
/// vor der Änderung (ADR 0062).
let private memoBaseline (entry: Entry) (before: string option) : string option =
    match entry.MemoWriteBackFailure with
    | Some failure -> failure.BaselineMemo
    | None -> before

/// Schickt das Memo. Scheitert der Push (oder fehlt die Verknüpfung), schreibt die Funktion
/// `EntryMemoWriteBackFailed`. Bei Erfolg schreibt sie kein Ereignis, außer ein früherer
/// Fehlschlag war offen: dann ist er mit dem Erfolg beendet (`EntryMemoWrittenBackToYnab`).
let private pushMemo
    (now: DateTime)
    (push: MemoPush)
    (entry: Entry)
    (baseline: string option)
    (memo: string option)
    : Async<MemoWriteBack> =
    async {
        let entryId = entry.Id

        let fail (reason: string) =
            async {
                do!
                    Ledger.Persistence.appendAndApply [
                        reviewMarkEvent entryId now UserInBb (
                            EntryMemoWriteBackFailed { Reason = reason; BaselineMemo = baseline }
                        )
                    ]
                return MemoSendFailed reason
            }

        match! tryYnabTransactionId entryId with
        | None -> return! fail memoNotLinkedReason
        | Some ynabTransactionId ->
            match! push ynabTransactionId memo with
            | Ok () ->
                if entry.MemoWriteBackFailure.IsSome then
                    do!
                        Ledger.Persistence.appendAndApply [
                            reviewMarkEvent entryId now UserInBb EntryMemoWrittenBackToYnab
                        ]
                return MemoSent
            | Error err -> return! fail (describeMemoPushError err)
    }

/// Memo setzen, ändern oder leeren (ADR 0062) und "Rückgängig" (derselbe Befehl mit dem alten
/// Memo). Die Eingabe läuft durch `normalizeMemo`: getrimmt, leer heißt "kein Memo", zu lang
/// wird abgelehnt (kein Ereignis, kein Push). Gelöschte und unbekannte Buchungen werden
/// abgelehnt. Ziel gleich Ist: weder Ereignis noch Push (`MemoUnchanged`). Sonst schreibt der
/// Befehl zuerst `EntryMemoChangedInBb` und ruft danach den Push (Reihenfolge wie ADR 0050,
/// Punkt 5). Läuft unter der Schreib-Sperre (`WriteGuard`), nie während eines Spiegel-Laufs.
let setEntryMemo
    (now: DateTime)
    (push: MemoPush)
    (entryId: EntryId)
    (text: string)
    : Async<Result<MemoOutcome, string>> =
    match normalizeMemo text with
    | Error reason -> async { return Error reason }
    | Ok newMemo ->
        WriteGuard.exclusive (
            async {
                match! Ledger.Persistence.loadEntry entryId with
                | None -> return Error "Buchung nicht gefunden"
                | Some entry when entry.DeletedInYnab -> return Error "Die Buchung ist in YNAB gelöscht"
                | Some entry ->
                    let previous = memoKey entry.Memo
                    if newMemo = previous then
                        return
                            Ok {
                                PreviousMemo = previous
                                CurrentMemo = previous
                                MemoChange = MemoUnchanged
                                MemoWriteBack = MemoNotSent
                            }
                    else
                        do!
                            Ledger.Persistence.appendAndApply [
                                reviewMarkEvent entryId now UserInBb (
                                    EntryMemoChangedInBb { OldMemo = previous; NewMemo = newMemo }
                                )
                            ]

                        let baseline = memoBaseline entry previous
                        let! writeBack = pushMemo now push entry baseline newMemo
                        return
                            Ok {
                                PreviousMemo = previous
                                CurrentMemo = newMemo
                                MemoChange = MemoChanged
                                MemoWriteBack = writeBack
                            }
            }
        )

/// "Erneut senden" am Hinweis "Memo nicht in YNAB" (ADR 0062): sendet das Memo des aktuellen
/// BB-Stands. Erfolg schreibt `EntryMemoWrittenBackToYnab` (Actor `UserInBb`), ein erneuter
/// Fehlschlag ersetzt den Grund (die Baseline bleibt). Ohne offenen Fehlschlag tut der Befehl
/// nichts (`MemoNotSent`).
let retryMemoWriteBack
    (now: DateTime)
    (push: MemoPush)
    (entryId: EntryId)
    : Async<Result<MemoWriteBack, string>> =
    WriteGuard.exclusive (
        async {
            match! Ledger.Persistence.loadEntry entryId with
            | None -> return Error "Buchung nicht gefunden"
            | Some entry when entry.DeletedInYnab -> return Error "Die Buchung ist in YNAB gelöscht"
            | Some entry when entry.MemoWriteBackFailure.IsNone -> return Ok MemoNotSent
            | Some entry ->
                let baseline = memoBaseline entry (memoKey entry.Memo)
                let! writeBack = pushMemo now push entry baseline (memoKey entry.Memo)
                return Ok writeBack
        }
    )

// ============================================
// Lena fragen für eine ganze Auswahl, ein Sammel-PATCH (ledger-y54gr)
// ============================================

/// Der Sammel-Push des Flags, injiziert wie `CategoryBatchPush`: bekommt je geänderter,
/// verknüpfter Buchung YNAB-Id und Ziel-Farbe und liefert die Ids, die YNAB nicht als
/// gespeichert nennt. Ein `Error` trifft den ganzen Stapel (`Ynab.updateFlagBatch`).
type FlagBatchPush = Ynab.FlagBatchItem list -> Async<YnabResult<string list>>

/// Der Grund für eine Buchung, die YNAB in der Antwort des Sammel-PATCH nicht nennt.
let flagNotSavedReason = "YNAB hat die Buchung im Sammelaufruf nicht gespeichert"

/// Gemeinsamer Ablauf für `setLenaFlagBatch` und `restoreFlagBatch`: prüft jede Buchung wie
/// `changeFlag` (unbekannt oder in YNAB gelöscht: gescheitert, ohne Ereignis; Ziel gleich
/// Ist: unverändert, ohne Ereignis), schreibt für jede geänderte Buchung ein eigenes
/// `EntryFlagChangedInBb` und sendet danach EINEN Sammel-PATCH mit den verknüpften. Eine
/// nicht verknüpfte Buchung bekommt `EntryFlagWriteBackFailed` (Grund `flagNotLinkedReason`),
/// scheitert der PATCH, trägt jede Buchung darin einen Fehlschlag mit der Baseline aus
/// `flagBaseline`; nennt die Antwort einzelne nicht gespeicherte Ids, gelten nur diese als
/// gescheitert. Erfolg bei offenem früheren Fehlschlag schreibt `EntryFlagWrittenBackToYnab`.
/// Ohne geänderte Buchung geht kein PATCH hinaus. Läuft unter der Schreib-Sperre.
let private changeFlagBatch
    (now: DateTime)
    (push: FlagBatchPush)
    (targets: (EntryId * (Entry -> (string option * string option) option)) list)
    : Async<Result<FlagBatchOutcome, string>> =
    WriteGuard.exclusive (
        async {
            let candidates = ResizeArray<Entry * (string option * string option)>()
            let mutable unchanged = 0
            let mutable failed = 0

            for entryId, resolveTarget in targets do
                match! Ledger.Persistence.loadEntry entryId with
                | None -> failed <- failed + 1
                | Some entry when entry.DeletedInYnab -> failed <- failed + 1
                | Some entry ->
                    match resolveTarget entry with
                    | None -> unchanged <- unchanged + 1
                    | Some target when target = flagPairOf entry -> unchanged <- unchanged + 1
                    | Some target -> candidates.Add((entry, target))

            let changed = candidates |> Seq.toList

            if List.isEmpty changed then
                return Ok { Changed = []; UnchangedCount = unchanged; FailedCount = failed }
            else
                do!
                    Ledger.Persistence.appendAndApply [
                        for entry, (newColor, newName) in changed ->
                            reviewMarkEvent entry.Id now UserInBb (
                                EntryFlagChangedInBb {
                                    OldFlagColor = entry.FlagColor
                                    OldFlagName = entry.FlagName
                                    NewFlagColor = newColor
                                    NewFlagName = newName
                                }
                            )
                    ]

                let linked = ResizeArray<Entry * string option * string>()

                for entry, (newColor, _) in changed do
                    match! tryYnabTransactionId entry.Id with
                    | Some ynabId -> linked.Add((entry, newColor, ynabId))
                    | None -> ()

                let linked = linked |> Seq.toList

                let! pushed =
                    if List.isEmpty linked then
                        async { return Ok [] }
                    else
                        push [ for _, color, ynabId in linked -> ({ YnabTransactionId = ynabId; FlagColor = color }: Ynab.FlagBatchItem) ]

                let reasonOfLinked (ynabId: string) : string option =
                    match pushed with
                    | Ok notSaved when notSaved |> List.contains ynabId -> Some flagNotSavedReason
                    | Ok _ -> None
                    | Error err -> Some (describeFlagPushError err)

                let results : (Entry * string option) list =
                    [ for entry, _ in changed ->
                        match linked |> List.tryFind (fun (e, _, _) -> e.Id = entry.Id) with
                        | Some (_, _, ynabId) -> entry, reasonOfLinked ynabId
                        | None -> entry, Some flagNotLinkedReason ]

                do!
                    Ledger.Persistence.appendAndApply [
                        for entry, reason in results do
                            match reason with
                            | Some r ->
                                let baselineColor, baselineName = flagBaseline entry (flagPairOf entry)
                                reviewMarkEvent entry.Id now UserInBb (
                                    EntryFlagWriteBackFailed {
                                        Reason = r
                                        BaselineFlagColor = baselineColor
                                        BaselineFlagName = baselineName
                                    }
                                )
                            | None ->
                                if entry.FlagWriteBackFailure.IsSome then
                                    reviewMarkEvent entry.Id now UserInBb EntryFlagWrittenBackToYnab
                    ]

                let outcomes : FlagBatchChange list =
                    [ for entry, reason in results ->
                        let (EntryId id) = entry.Id
                        { EntryId = id
                          Previous = flagStateOf entry
                          WriteBack =
                            (match reason with
                             | Some r -> FlagSendFailed r
                             | None -> FlagSent) } ]

                return Ok { Changed = outcomes; UnchangedCount = unchanged; FailedCount = failed }
        }
    )

/// "Lena fragen" bzw. "Rückfrage zurücknehmen" für die ganze Auswahl (ledger-y54gr). Je
/// Buchung gilt die Regel von `setLenaFlag` (`lenaFlagTargetOf`). `AssignLena` ohne gültige
/// Farbe lehnt den ganzen Befehl ab (Fehler, kein Ereignis). `lenaColor` ist die Einstellung
/// `lena_flag_color`.
let setLenaFlagBatch
    (now: DateTime)
    (push: FlagBatchPush)
    (lenaColor: string option)
    (selection: ReviewMarkBatchSelection)
    (target: LenaFlagTarget)
    : Async<Result<FlagBatchOutcome, string>> =
    async {
        match target, lenaColor with
        | AssignLena, Some color when not (List.contains color ynabFlagColors) -> return Error lenaColorMissingReason
        | AssignLena, None -> return Error lenaColorMissingReason
        | _ ->
            let! ids = resolveReviewMarkBatchSelection selection
            let resolve (entry: Entry) =
                match lenaFlagTargetOf lenaColor target entry with
                | Ok t -> t
                | Error _ -> None
            return! changeFlagBatch now push [ for id in ids -> id, resolve ]
    }

/// Ein "Rückgängig" für den ganzen Stapel: stellt für jede genannte Buchung Farbe und Name
/// des Vorzustands wieder her (Logik von `restoreFlag`), je ein Ereignis und ein Sammel-PATCH.
/// Buchungen, die schon im Vorzustand stehen, bleiben unangetastet.
let restoreFlagBatch
    (now: DateTime)
    (push: FlagBatchPush)
    (items: FlagRestoreItem list)
    : Async<Result<FlagBatchOutcome, string>> =
    changeFlagBatch now push [
        for item in items -> EntryId item.EntryId, (fun _ -> Some (item.Previous.Color, item.Previous.Name))
    ]

// ============================================
// Einstellung "Lena fragen": die Flag-Farbe (ADR 0056, Punkt 1)
// ============================================

/// Schlüssel der Einstellung: die YNAB-Flag-Farbe, die Roman "Lena" genannt hat.
let lenaFlagColorSettingKey = "lena_flag_color"

/// Speichert die Farbe für "Lena fragen". `None` löscht die Einstellung. Nur die sechs
/// YNAB-Flag-Farben sind erlaubt (`Server.Validation.validateLenaFlagColor`).
let saveLenaFlagColor (color: string option) : Async<Result<unit, string>> =
    async {
        match Server.Validation.validateLenaFlagColor color with
        | Error message -> return Error message
        | Ok None ->
            do! Core.Settings.deleteSetting lenaFlagColorSettingKey
            return Ok ()
        | Ok (Some valid) ->
            do! Core.Settings.setSetting lenaFlagColorSettingKey valid false
            return Ok ()
    }
