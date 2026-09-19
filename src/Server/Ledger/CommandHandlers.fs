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
    MirrorGuard.runGuarded (fun () -> runMirror now fetchers budgetId)

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
/// danach `push` auf und verknüpft bei Erfolg mit `EntryLinkedToYnab` (ADR 0021): `push`
/// liefert die YNAB-Transaction-Id aus der Antwort des Einzel-Posts, die einzige Stelle,
/// an der eine Quick-Add-Buchung ohne ImportId verknüpft werden kann (ADR 0004, ADR
/// 0015). Scheitert der Push, bleibt die Kassenbuch-Buchung unverändert stehen, kein
/// Rollback (ADR 0021, Punkt 2); der Fehler wird durchgereicht, die Buchung bleibt ohne
/// YNAB-Verknüpfung bis zu einem erneuten Push oder dem nächsten Spiegel-Lauf.
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
                Actor = QuickAdd
                Body = EntryLinkedToYnab { YnabTransactionId = ynabTransactionId }
            }
            do! Ledger.Persistence.appendAndApply [ linked ]
            triggerMirror ()
            return Ok entryId
        | Error err ->
            return Error err
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
    Payee: string option
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
                            recordComdirectPush {
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
                                Memo = candidate.Memo
                                PushedAt = now
                                ReviewSteps = candidate.ReviewSteps
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
