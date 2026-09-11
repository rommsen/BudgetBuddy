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

let private describeYnabError (error: YnabError) : string =
    match error with
    | YnabError.Unauthorized message -> $"YNAB-Token abgelehnt: {message}"
    | YnabError.BudgetNotFound budgetId -> $"Budget {budgetId} in YNAB nicht gefunden"
    | YnabError.AccountNotFound accountId -> $"Konto {accountId} in YNAB nicht gefunden"
    | YnabError.CategoryNotFound categoryId -> $"Kategorie {categoryId} in YNAB nicht gefunden"
    | YnabError.RateLimitExceeded seconds ->
        $"YNAB-Rate-Limit erreicht, Lauf abgebrochen (fruehestens in {seconds}s erneut)"
    | YnabError.NetworkError message -> $"YNAB nicht erreichbar: {message}"
    | YnabError.InvalidResponse message -> $"YNAB-Antwort nicht lesbar: {message}"

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
            | Error error -> return! finish (Error (describeYnabError error))
            | Ok accountBatch ->
                let! resolution = syncAccounts accountBatch.Accounts

                match! fetchers.FetchTransactions knowledge with
                | Error error -> return! finish (Error (describeYnabError error))
                | Ok batch ->
                    let mutable newEntries = 0
                    let mutable changedEntries = 0
                    let mutable deletedEntries = 0
                    let mutable failure = None

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

                                let events =
                                    observe
                                        { EntryId = entryId; AccountId = ledgerAccountId; ObservedAt = now }
                                        existing
                                        obs

                                do! Ledger.Persistence.appendAndApply events

                                if events |> List.exists (fun e ->
                                        match e.Body with
                                        | EntryObservedInYnab _ -> true
                                        | _ -> false) then
                                    newEntries <- newEntries + 1

                                if events |> List.exists (fun e ->
                                        match e.Body with
                                        | EntryDetailsChangeObservedInYnab _
                                        | EntryCategorizationChangeObservedInYnab _
                                        | EntryClearedStateChangeObservedInYnab _ -> true
                                        | _ -> false) then
                                    changedEntries <- changedEntries + 1

                                if events |> List.exists (fun e -> e.Body = EntryDeletionObservedInYnab) then
                                    deletedEntries <- deletedEntries + 1

                    match failure with
                    | Some message -> return! finish (Error message)
                    | None ->
                        // Erst wenn alle Beobachtungen verarbeitet sind, ruecken wir das
                        // Gedaechtnis vor: ein abgebrochener Lauf holt beim naechsten Mal
                        // dasselbe Delta erneut, was dank Idempotenz folgenlos ist.
                        do! saveServerKnowledge budgetId batch.ServerKnowledge

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

/// Schreibt zuerst die Kassenbuch-Buchung (`CashExpenseAdded`, Actor `QuickAdd`), ruft
/// danach `push` auf und verknuepft bei Erfolg mit `EntryLinkedToYnab` (ADR 0021): `push`
/// liefert die YNAB-Transaction-Id aus der Antwort des Einzel-Posts, die einzige Stelle,
/// an der eine Quick-Add-Buchung ohne ImportId verknuepft werden kann (ADR 0004, ADR
/// 0015). Scheitert der Push, bleibt die Kassenbuch-Buchung unveraendert stehen, kein
/// Rollback (ADR 0021, Punkt 2); der Fehler wird durchgereicht, die Buchung bleibt ohne
/// YNAB-Verknuepfung bis zu einem erneuten Push oder dem naechsten Spiegel-Lauf.
///
/// `now` und `push` kommen von aussen, damit der Ablauf ohne Uhr und ohne Netz testbar
/// ist (Muster wie `MirrorFetchers` beim Spiegel-Lauf).
let addCashExpense
    (now: DateTime)
    (push: unit -> Async<YnabResult<string>>)
    (input: CashExpenseInput)
    : Async<YnabResult<EntryId>> =
    async {
        let entryId = newEntryId ()
        let cashExpenseAdded = {
            EntryId = entryId
            OccurredAt = now
            Actor = QuickAdd
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
                }
        }
        do! Ledger.Persistence.appendAndApply [ cashExpenseAdded ]

        match! push () with
        | Ok ynabTransactionId ->
            let linked = {
                EntryId = entryId
                OccurredAt = now
                Actor = QuickAdd
                Body = EntryLinkedToYnab { YnabTransactionId = ynabTransactionId }
            }
            do! Ledger.Persistence.appendAndApply [ linked ]
            return Ok entryId
        | Error err ->
            return Error err
    }

/// Verdrahtet den Lauf mit dem echten YNAB-Client und den gespeicherten Einstellungen.
/// Der manuelle Ausloeser ("Jetzt spiegeln"); Frische-Kriterium und Lauf nach Push sind
/// Folge-Arbeit (ADR 0014).
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
            }

            return! runMirror DateTime.UtcNow fetchers budgetId
    }
