module LedgerMirrorTests

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Thoth.Json.Net
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Server.YnabObservations
open Server.YnabClient

// ============================================
// Bausteine
// ============================================

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }

let private ledgerAccount = LedgerAccountId (Guid.Parse "0195d000-0000-7000-8000-000000000001")
let private otherLedgerAccount = LedgerAccountId (Guid.Parse "0195d000-0000-7000-8000-000000000002")

let private ynabAccount = YnabAccountId (Guid.Parse "0195d000-0000-7000-8000-0000000000a1")

let private lebensmittel = {
    CategoryId = YnabCategoryId (Guid.Parse "0195d000-0000-7000-8000-0000000000c1")
    CategoryName = "Lebensmittel"
}

let private haushalt = {
    CategoryId = YnabCategoryId (Guid.Parse "0195d000-0000-7000-8000-0000000000c2")
    CategoryName = "Haushalt"
}

let private observation (ynabTxId: string) : YnabObservation = {
    YnabTransactionId = ynabTxId
    AccountId = ynabAccount
    ImportId = None
    Date = DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -21.99m
    Payee = Some "Rewe"
    Memo = Some "Wocheneinkauf"
    Category = Some lebensmittel
    Splits = []
    Cleared = Uncleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Deleted = false
    Approved = true
    FlagColor = None
    FlagName = None
}

let private context (entryId: EntryId) : ObservationContext = {
    EntryId = entryId
    AccountId = ledgerAccount
    ObservedAt = DateTime(2026, 2, 4, 7, 0, 0, DateTimeKind.Utc)
}

/// Der Zustand, den eine Erstbeobachtung erzeugt — die Ausgangslage fuer jeden
/// Vergleichstest.
let private stateFrom (entryId: EntryId) (obs: YnabObservation) : Entry =
    match observe (context entryId) None obs with
    | [ first ] -> apply None first
    | events -> failtestf "Erstbeobachtung sollte genau ein Ereignis liefern, waren %d" events.Length

// ============================================
// observe: Snapshot gegen Zustand (rein)
// ============================================

[<Tests>]
let observeTests =
    testList "Ledger Spiegel (observe)" [

        testCase "unbekannte Buchung wird zur Erstbeobachtung mit vollem Snapshot" <| fun () ->
            let entryId = newEntryId ()
            let obs = { observation "ynab-neu" with ImportId = Some "BB:abc" }

            match observe (context entryId) None obs with
            | [ { Body = EntryObservedInYnab payload; Actor = actor; EntryId = eventEntryId } ] ->
                Expect.equal actor YnabMirror "Beobachtungen tragen den Actor YnabMirror"
                Expect.equal eventEntryId entryId "Ereignis haengt an der uebergebenen Buchungs-Id"
                Expect.equal payload.AccountId ledgerAccount "auf das aufgeloeste Kassenbuch-Konto"
                Expect.equal payload.YnabTransactionId "ynab-neu" "YNAB-Id im Snapshot"
                Expect.equal payload.ImportId (Some "BB:abc") "ImportId im Snapshot"
                Expect.equal payload.Amount obs.Amount "Betrag im Snapshot"
                Expect.equal payload.Category obs.Category "Kategorie mit Namen im Snapshot"
                Expect.equal payload.Cleared obs.Cleared "Cleared im Snapshot"
                Expect.equal payload.Approved obs.Approved "Prüfvermerk im Snapshot"
                Expect.equal payload.FlagColor obs.FlagColor "Flag-Farbe im Snapshot"
                Expect.equal payload.FlagName obs.FlagName "Flag-Name im Snapshot"
            | events -> failtestf "genau eine Erstbeobachtung erwartet, waren %A" events

        testCase "unveraenderte Beobachtung erzeugt kein Ereignis" <| fun () ->
            let entryId = newEntryId ()
            let obs = observation "ynab-gleich"
            let state = stateFrom entryId obs

            Expect.isEmpty
                (observe (context entryId) (Some state) obs)
                "gleiche Daten sind kein Delta (ADR 0015, Idempotenz)"

        testCase "geaenderte Kategorie erzeugt genau ein Kategorisierungs-Ereignis" <| fun () ->
            let entryId = newEntryId ()
            let obs = observation "ynab-kat"
            let state = stateFrom entryId obs

            match observe (context entryId) (Some state) { obs with Category = Some haushalt } with
            | [ { Body = EntryCategorizationChangeObservedInYnab payload } ] ->
                Expect.equal payload.OldCategory (Some lebensmittel) "alte Kategorie"
                Expect.equal payload.NewCategory (Some haushalt) "neue Kategorie"
            | events -> failtestf "genau ein Kategorisierungs-Ereignis erwartet, waren %A" events

        testCase "geaenderte Kopfdaten und Cleared ergeben zwei Ereignisse in fester Reihenfolge" <| fun () ->
            let entryId = newEntryId ()
            let obs = observation "ynab-mehrfach"
            let state = stateFrom entryId obs

            let changed = { obs with Amount = eur -25.00m; Cleared = Cleared }

            match observe (context entryId) (Some state) changed with
            | [ { Body = EntryDetailsChangeObservedInYnab details }
                { Body = EntryClearedStateChangeObservedInYnab cleared } ] ->
                Expect.equal details.OldAmount (eur -21.99m) "alter Betrag"
                Expect.equal details.NewAmount (eur -25.00m) "neuer Betrag"
                Expect.equal cleared.NewCleared Cleared "neuer Cleared-Status"
            | events -> failtestf "Kopfdaten- vor Cleared-Ereignis erwartet, waren %A" events

        testCase "Verschieben auf ein anderes Konto ist eine Kopfdaten-Aenderung" <| fun () ->
            let entryId = newEntryId ()
            let obs = observation "ynab-umgezogen"
            let state = stateFrom entryId obs
            let ctx = { context entryId with AccountId = otherLedgerAccount }

            match observe ctx (Some state) obs with
            | [ { Body = EntryDetailsChangeObservedInYnab payload } ] ->
                Expect.equal payload.OldAccountId ledgerAccount "altes Konto"
                Expect.equal payload.NewAccountId otherLedgerAccount "neues Konto"
            | events -> failtestf "genau ein Kopfdaten-Ereignis erwartet, waren %A" events

        testCase "Loeschung in YNAB erzeugt ein Loeschungs-Ereignis, danach keins mehr" <| fun () ->
            let entryId = newEntryId ()
            let obs = observation "ynab-geloescht"
            let state = stateFrom entryId obs
            let deletedObs = { obs with Deleted = true }

            match observe (context entryId) (Some state) deletedObs with
            | [ { Body = EntryDeletionObservedInYnab } as event ] ->
                let afterDeletion = apply (Some state) event
                Expect.isTrue afterDeletion.DeletedInYnab "Zustand traegt das Flag"
                Expect.equal afterDeletion.Amount state.Amount "die Buchung bleibt lesbar"
                Expect.isEmpty
                    (observe (context entryId) (Some afterDeletion) deletedObs)
                    "eine erneut gesehene Loeschung ist kein neues Ereignis"
            | events -> failtestf "genau ein Loeschungs-Ereignis erwartet, waren %A" events

        testCase "geloeschte Buchung, die das Kassenbuch nie sah, erzeugt kein Ereignis" <| fun () ->
            let entryId = newEntryId ()
            let obs = { observation "ynab-nie-gesehen" with Deleted = true }

            Expect.isEmpty
                (observe (context entryId) None obs)
                "eine Buchung anlegen, nur um sie als geloescht zu markieren, waere Rauschen"

        testCase "geänderter Prüfvermerk erzeugt genau ein Genehmigungs-Ereignis" <| fun () ->
            // AC7 (ledger-kdnnz): approved geändert, sonst nichts.
            let entryId = newEntryId ()
            let obs = observation "ynab-approved"
            let state = stateFrom entryId obs

            match observe (context entryId) (Some state) { obs with Approved = false } with
            | [ { Body = EntryApprovalChangeObservedInYnab payload } ] ->
                Expect.isTrue payload.OldApproved "alter Stand: genehmigt"
                Expect.isFalse payload.NewApproved "neuer Stand: zurückgenommen"
            | events -> failtestf "genau ein Genehmigungs-Ereignis erwartet, waren %A" events

        testCase "geändertes Flag erzeugt genau ein Flag-Ereignis" <| fun () ->
            let entryId = newEntryId ()
            let obs = observation "ynab-flag"
            let state = stateFrom entryId obs

            match observe (context entryId) (Some state) { obs with FlagColor = Some "purple"; FlagName = Some "Lena" } with
            | [ { Body = EntryFlagChangeObservedInYnab payload } ] ->
                Expect.isNone payload.OldFlagColor "vorher kein Flag"
                Expect.equal payload.NewFlagColor (Some "purple") "neue Farbe"
                Expect.equal payload.NewFlagName (Some "Lena") "neuer Name"
            | events -> failtestf "genau ein Flag-Ereignis erwartet, waren %A" events

        testCase "unveränderter Prüfvermerk und unverändertes Flag erzeugen kein Ereignis" <| fun () ->
            // AC6: Idempotenz gilt auch für die beiden neuen Gruppen.
            let entryId = newEntryId ()
            let obs = { observation "ynab-flag-gleich" with FlagColor = Some "red"; FlagName = None }
            let state = stateFrom entryId obs

            Expect.isEmpty
                (observe (context entryId) (Some state) obs)
                "gleicher Prüfvermerk und gleiches Flag sind kein Delta"

        testCase "Kopfdaten-, Genehmigungs- und Flag-Änderung ergeben drei Ereignisse in fester Reihenfolge" <| fun () ->
            // AC7: mehrere Gruppen ändern sich gleichzeitig, Reihenfolge nach ADR 0032
            // (Kopfdaten, Kategorisierung, Cleared, Genehmigung, Flag).
            let entryId = newEntryId ()
            let obs = observation "ynab-mehrere-neue-gruppen"
            let state = stateFrom entryId obs

            let changed =
                { obs with
                    Amount = eur -30.00m
                    Approved = false
                    FlagColor = Some "orange"
                    FlagName = None }

            match observe (context entryId) (Some state) changed with
            | [ { Body = EntryDetailsChangeObservedInYnab _ }
                { Body = EntryApprovalChangeObservedInYnab approval }
                { Body = EntryFlagChangeObservedInYnab flag } ] ->
                Expect.isFalse approval.NewApproved "Genehmigung zurückgenommen"
                Expect.equal flag.NewFlagColor (Some "orange") "Flag-Farbe gesetzt"
            | events -> failtestf "Kopfdaten-, Genehmigungs- und Flag-Ereignis in dieser Reihenfolge erwartet, waren %A" events

        testCase "frisch gepushte Buchung erzeugt beim ersten Spiegel-Lauf kein Prüf-Ereignis" <| fun () ->
            // AC8: der Push selbst sendet `Approved = false`; sieht der Spiegel danach
            // dieselbe unbestätigte Buchung, ist das kein Delta.
            let entryId = newEntryId ()
            let pushed =
                apply None {
                    EntryId = entryId
                    OccurredAt = DateTime(2026, 2, 6, 8, 0, 0, DateTimeKind.Utc)
                    Actor = ComdirectImport
                    Body =
                        EntryImportedFromComdirect {
                            AccountId = ledgerAccount
                            ImportId = "BB:push-1"
                            ComdirectReference = "comdirect-push-1"
                            Date = DateTime(2026, 2, 6, 0, 0, 0, DateTimeKind.Utc)
                            Amount = eur -12.00m
                            Payee = Some "dm"
                            Memo = None
                            Cleared = Uncleared
                            Approved = false
                        }
                }
            Expect.isFalse pushed.Approved "die gepushte Buchung ist zunächst unbestätigt"

            let firstMirrorObservation =
                { observation "BB:push-1-ynab" with
                    Payee = Some "dm"
                    Memo = None
                    Category = None
                    Amount = eur -12.00m
                    Date = pushed.Date
                    Cleared = Uncleared
                    Approved = false }

            Expect.isEmpty
                (observe (context entryId) (Some pushed) firstMirrorObservation)
                "der erste Spiegel-Lauf nach dem Push sieht denselben unbestätigten Stand"

        testCase "Uebertrags-Verknuepfungen werden in der Erstbeobachtung mitgefuehrt" <| fun () ->
            let entryId = newEntryId ()
            let gegenkonto = YnabAccountId (Guid.Parse "0195d000-0000-7000-8000-0000000000a2")
            let obs =
                { observation "ynab-uebertrag-a" with
                    Category = None
                    TransferAccountId = Some gegenkonto
                    TransferYnabTransactionId = Some "ynab-uebertrag-b" }

            match observe (context entryId) None obs with
            | [ { Body = EntryObservedInYnab payload } ] ->
                Expect.equal payload.TransferAccountId (Some gegenkonto) "Gegenkonto mitgefuehrt"
                Expect.equal
                    payload.TransferYnabTransactionId
                    (Some "ynab-uebertrag-b")
                    "transfer_transaction_id mitgefuehrt"
            | events -> failtestf "genau eine Erstbeobachtung erwartet, waren %A" events
    ]

// ============================================
// Decoder gegen ein JSON-Fixture
// ============================================

module private Fixture =
    /// Ein Delta wie YNAB es liefert: eine Split-Buchung (Kategorie-, Transfer- und
    /// unkategorisierte Zeile plus eine geloeschte Zeile), eine Uebertrags-Buchung und
    /// eine geloeschte Buchung.
    let transactionsDelta = """
    {
      "data": {
        "transactions": [
          {
            "id": "tx-split",
            "date": "2026-02-03",
            "amount": -21990,
            "memo": "Einkauf mit Abhebung",
            "cleared": "cleared",
            "approved": true,
            "flag_color": null,
            "flag_name": null,
            "account_id": "0195d000-0000-7000-8000-0000000000a1",
            "account_name": "Comdirect Giro",
            "payee_name": "Rewe",
            "category_id": null,
            "category_name": "Split (Multiple Categories)...",
            "transfer_account_id": null,
            "transfer_transaction_id": null,
            "import_id": "BB:abc123",
            "deleted": false,
            "subtransactions": [
              {
                "id": "sub-1",
                "amount": -11990,
                "memo": "Lebensmittel",
                "payee_name": "Rewe",
                "category_id": "0195d000-0000-7000-8000-0000000000c1",
                "category_name": "Lebensmittel",
                "transfer_account_id": null,
                "deleted": false
              },
              {
                "id": "sub-2",
                "amount": -5000,
                "memo": null,
                "payee_name": "Transfer : Bar",
                "category_id": null,
                "category_name": null,
                "transfer_account_id": "0195d000-0000-7000-8000-0000000000a3",
                "transfer_transaction_id": "tx-bar",
                "deleted": false
              },
              {
                "id": "sub-3",
                "amount": -5000,
                "memo": "noch offen",
                "payee_name": null,
                "category_id": null,
                "category_name": null,
                "transfer_account_id": null,
                "deleted": false
              },
              {
                "id": "sub-4",
                "amount": -1000,
                "memo": "verworfen",
                "category_id": "0195d000-0000-7000-8000-0000000000c2",
                "category_name": "Haushalt",
                "transfer_account_id": null,
                "deleted": true
              }
            ]
          },
          {
            "id": "tx-transfer",
            "date": "2026-02-04",
            "amount": -100000,
            "memo": null,
            "cleared": "uncleared",
            "approved": false,
            "flag_color": "purple",
            "flag_name": "Lena",
            "account_id": "0195d000-0000-7000-8000-0000000000a1",
            "payee_name": "Transfer : ING",
            "category_id": null,
            "category_name": null,
            "transfer_account_id": "0195d000-0000-7000-8000-0000000000a2",
            "transfer_transaction_id": "tx-transfer-gegen",
            "deleted": false
          },
          {
            "id": "tx-weg",
            "date": "2026-01-30",
            "amount": -1234,
            "memo": null,
            "cleared": "reconciled",
            "approved": true,
            "account_id": "0195d000-0000-7000-8000-0000000000a1",
            "payee_name": "Aldi",
            "category_id": null,
            "deleted": true
          }
        ],
        "server_knowledge": 4711
      }
    }
    """

    let accountsDelta = """
    {
      "data": {
        "accounts": [
          {
            "id": "0195d000-0000-7000-8000-0000000000a1",
            "name": "Comdirect Giro",
            "type": "checking",
            "on_budget": true,
            "closed": false,
            "balance": 123450,
            "deleted": false
          },
          {
            "id": "0195d000-0000-7000-8000-0000000000a4",
            "name": "Altes Konto",
            "type": "savings",
            "on_budget": false,
            "closed": true,
            "balance": 0,
            "deleted": false
          }
        ],
        "server_knowledge": 815
      }
    }
    """

[<Tests>]
let decoderTests =
    testList "Ledger Spiegel (Decoder)" [

        testCase "Transaktions-Delta wird vollstaendig dekodiert" <| fun () ->
            match Decode.fromString Decoders.observationBatchDecoder Fixture.transactionsDelta with
            | Error err -> failtest $"Delta nicht dekodierbar: {err}"
            | Ok batch ->
                Expect.equal batch.ServerKnowledge 4711L "server_knowledge der Antwort"
                Expect.equal batch.Observations.Length 3 "drei Beobachtungen"

                let split = batch.Observations |> List.find (fun o -> o.YnabTransactionId = "tx-split")
                Expect.equal split.Amount (eur -21.99m) "Milliunits werden zu Euro"
                Expect.equal split.ImportId (Some "BB:abc123") "ImportId gelesen"
                Expect.equal split.Payee (Some "Rewe") "Payee-Name gelesen"
                Expect.equal split.Cleared Cleared "Cleared-Status gelesen"
                Expect.isNone split.Category "eine Split-Buchung hat keine eigene Kategorie"
                Expect.equal split.Date (DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc)) "Datum gelesen"
                Expect.isFalse split.Deleted "nicht geloescht"
                Expect.isTrue split.Approved "approved: true gelesen"
                Expect.isNone split.FlagColor "flag_color: null gelesen"
                Expect.isNone split.FlagName "flag_name: null gelesen"

                Expect.equal split.Splits.Length 3 "die geloeschte Split-Zeile faellt weg"
                Expect.equal
                    (split.Splits |> List.map (fun s -> s.Target))
                    [
                        ToCategory (lebensmittel.CategoryId, "Lebensmittel")
                        ToTransfer (
                            YnabAccountId (Guid.Parse "0195d000-0000-7000-8000-0000000000a3"),
                            "Bar")
                        Uncategorized
                    ]
                    "Kategorie-Zeile, Transfer-Zeile ohne Payee-Prefix, unkategorisierte Zeile"
                Expect.equal
                    (split.Splits |> List.sumBy (fun s -> s.Amount.Amount))
                    split.Amount.Amount
                    "die unkategorisierte Zeile zaehlt beim Betrag voll mit"

                let transfer = batch.Observations |> List.find (fun o -> o.YnabTransactionId = "tx-transfer")
                Expect.equal
                    transfer.TransferAccountId
                    (Some (YnabAccountId (Guid.Parse "0195d000-0000-7000-8000-0000000000a2")))
                    "transfer_account_id gelesen"
                Expect.equal
                    transfer.TransferYnabTransactionId
                    (Some "tx-transfer-gegen")
                    "transfer_transaction_id gelesen"
                Expect.isFalse transfer.Approved "approved: false gelesen"
                Expect.equal transfer.FlagColor (Some "purple") "flag_color gelesen"
                Expect.equal transfer.FlagName (Some "Lena") "flag_name gelesen"

                let geloescht = batch.Observations |> List.find (fun o -> o.YnabTransactionId = "tx-weg")
                Expect.isTrue geloescht.Deleted "deleted: true gelesen"

        testCase "unbekannter Cleared-Status scheitert laut" <| fun () ->
            let json = """{"data":{"transactions":[{"id":"x","date":"2026-02-03","amount":-1,"cleared":"schwebend","account_id":"0195d000-0000-7000-8000-0000000000a1"}],"server_knowledge":1}}"""
            Expect.isError
                (Decode.fromString Decoders.observationBatchDecoder json)
                "ein unbekannter Status wird nicht geraten (Conformist, ADR 0014)"

        testCase "Konten-Delta wird dekodiert" <| fun () ->
            match Decode.fromString Decoders.accountObservationBatchDecoder Fixture.accountsDelta with
            | Error err -> failtest $"Konten-Delta nicht dekodierbar: {err}"
            | Ok batch ->
                Expect.equal batch.ServerKnowledge 815L "server_knowledge der Antwort"
                Expect.equal batch.Accounts.Length 2 "zwei Konten"
                let giro = batch.Accounts |> List.find (fun a -> a.Name = "Comdirect Giro")
                Expect.isFalse giro.Closed "offenes Konto"
                let alt = batch.Accounts |> List.find (fun a -> a.Name = "Altes Konto")
                Expect.isTrue alt.Closed "geschlossenes Konto kommt mit"
    ]

// ============================================
// Spiegel-Lauf (In-Memory-SQLite, injizierte Abrufe)
// ============================================

module private Run =
    let private accountsOf (observations: YnabObservation list) =
        observations
        |> List.map (fun o -> o.AccountId)
        |> List.distinct
        |> List.mapi (fun i accountId -> {
            AccountId = accountId
            Name = $"Konto {i + 1}"
            Closed = false
            Deleted = false
        })

    /// Abrufe, die genau die uebergebenen Beobachtungen liefern, samt der Konten, die
    /// sie brauchen.
    let fetchers (knowledge: int64) (observations: YnabObservation list) : MirrorFetchers = {
        FetchAccounts =
            fun _ ->
                async.Return (Ok { Accounts = accountsOf observations; ServerKnowledge = knowledge })
        FetchTransactions =
            fun _ -> async.Return (Ok { Observations = observations; ServerKnowledge = knowledge })
    }

    let failingTransactions (error: YnabError) (observations: YnabObservation list) : MirrorFetchers = {
        FetchAccounts =
            fun _ -> async.Return (Ok { Accounts = accountsOf observations; ServerKnowledge = 1L })
        FetchTransactions = fun _ -> async.Return (Error error)
    }

    let at (day: int) = DateTime(2026, 2, day, 6, 0, 0, DateTimeKind.Utc)

    /// Jeder Lauf-Test bekommt sein eigenes Budget, damit die Laeufe sich nicht ueber
    /// den gemeinsamen Settings-Schluessel in die Quere kommen.
    let budget (name: string) = YnabBudgetId $"budget-{name}"

    let run (day: int) (budgetId: YnabBudgetId) (fetchers: MirrorFetchers) =
        runMirror (at day) fetchers budgetId |> Async.RunSynchronously

    let expectOk (result: Result<MirrorRunSummary, string>) =
        match result with
        | Ok summary -> summary
        | Error err -> failtest $"Lauf sollte gelingen, war: {err}"

[<Tests>]
let mirrorRunTests =
    // initializeLedgerSchema()/initializeDatabase() laufen erst, wenn ein Testfall
    // tatsächlich ausgeführt wird, nicht schon beim Modul-Laden (ADR 0030, infra-sdad8).
    // Beide sind idempotent (CREATE TABLE IF NOT EXISTS); der wiederholte Aufruf pro
    // Testfall kostet nur eine überflüssige Prüfung.
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testSequenced
    <| testList "Ledger Spiegel-Lauf (mirrorYnabBudget)" [

        testCase "Erstlauf legt Konto und Buchung an und merkt sich das server_knowledge" <| fun () ->
            let budgetId = Run.budget "erstlauf"
            let ynabAcc = YnabAccountId (Guid.NewGuid())
            let obs = { observation "erstlauf-1" with AccountId = ynabAcc }

            let summary = Run.run 4 budgetId (Run.fetchers 100L [ obs ]) |> Run.expectOk

            Expect.equal summary.NewEntries 1 "eine neue Buchung"
            Expect.equal summary.ChangedEntries 0 "keine Aenderung"
            Expect.equal summary.DeletedEntries 0 "keine Loeschung"
            Expect.equal summary.ServerKnowledge 100L "server_knowledge der Antwort"

            let account =
                match findAccountByYnabAccountId ynabAcc |> Async.RunSynchronously with
                | None -> failtest "das gespiegelte Konto wurde nicht angelegt"
                | Some account -> account

            Expect.equal account.Link (YnabMirrored ynabAcc) "Konto ist an YNAB verknuepft"
            Expect.equal account.Kind BankAccount "v1 kennt nur Bankkonten"

            match findEntryByLink YnabSystem "erstlauf-1" |> Async.RunSynchronously with
            | None -> failtest "die Buchung ist nicht ueber ihre YNAB-Id auffindbar"
            | Some entryId ->
                let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
                Expect.equal entry.Amount obs.Amount "Betrag im Zustand"
                Expect.equal entry.AccountId account.Id "auf das aufgeloeste Kassenbuch-Konto"

            Expect.equal
                (loadServerKnowledge budgetId |> Async.RunSynchronously)
                (Some 100L)
                "server_knowledge je Budget persistiert"

        testCase "zweiter Lauf ohne Aenderung erzeugt kein Ereignis" <| fun () ->
            let budgetId = Run.budget "idempotent"
            let obs = { observation "idempotent-1" with AccountId = YnabAccountId (Guid.NewGuid()) }

            Run.run 4 budgetId (Run.fetchers 10L [ obs ]) |> Run.expectOk |> ignore
            let entryId = findEntryByLink YnabSystem "idempotent-1" |> Async.RunSynchronously |> Option.get
            let afterFirst = loadEventsForEntry entryId |> Async.RunSynchronously

            // Vollabruf mit denselben Daten: genau der Fall "server_knowledge verloren".
            let second = Run.run 5 budgetId (Run.fetchers 11L [ obs ]) |> Run.expectOk

            Expect.equal second.NewEntries 0 "keine neue Buchung"
            Expect.equal second.ChangedEntries 0 "keine Aenderung"
            Expect.equal
                (loadEventsForEntry entryId |> Async.RunSynchronously |> List.length)
                afterFirst.Length
                "die Erstbefuellung ist idempotent (ADR 0015)"

        testCase "Kategorie-Aenderung in YNAB erzeugt ein Ereignis auf derselben Buchungs-Id" <| fun () ->
            let budgetId = Run.budget "kategorie"
            let obs = { observation "kategorie-1" with AccountId = YnabAccountId (Guid.NewGuid()) }

            Run.run 4 budgetId (Run.fetchers 1L [ obs ]) |> Run.expectOk |> ignore
            let entryId = findEntryByLink YnabSystem "kategorie-1" |> Async.RunSynchronously |> Option.get

            let summary =
                Run.run 5 budgetId (Run.fetchers 2L [ { obs with Category = Some haushalt } ])
                |> Run.expectOk

            Expect.equal summary.ChangedEntries 1 "eine geaenderte Buchung"
            Expect.equal summary.NewEntries 0 "keine neue Buchung"

            let events = loadEventsForEntry entryId |> Async.RunSynchronously
            Expect.equal
                (events |> List.map (fun e -> e.EventType))
                [ "EntryObservedInYnab"; "EntryCategorizationChangeObservedInYnab" ]
                "genau ein Aenderungs-Ereignis auf derselben Buchung"

            let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.equal entry.Category (Some haushalt) "Zustand traegt die neue Kategorie"

        testCase "Prüfvermerk-Änderung in YNAB zählt als geänderte Buchung im Lauf-Status" <| fun () ->
            // Task-Schritt 6 (ADR 0032): ein Lauf, der nur Approved oder Flag ändert,
            // zählt die Buchung trotzdem als geändert.
            let budgetId = Run.budget "pruefvermerk"
            let obs = { observation "pruefvermerk-1" with AccountId = YnabAccountId (Guid.NewGuid()) }

            Run.run 4 budgetId (Run.fetchers 1L [ obs ]) |> Run.expectOk |> ignore

            let summary =
                Run.run 5 budgetId (Run.fetchers 2L [ { obs with Approved = false } ]) |> Run.expectOk

            Expect.equal summary.ChangedEntries 1 "eine geänderte Buchung, obwohl nur der Prüfvermerk sich ändert"
            Expect.equal summary.NewEntries 0 "keine neue Buchung"

        testCase "Flag-Änderung in YNAB zählt als geänderte Buchung im Lauf-Status" <| fun () ->
            let budgetId = Run.budget "flag"
            let obs = { observation "flag-1" with AccountId = YnabAccountId (Guid.NewGuid()) }

            Run.run 4 budgetId (Run.fetchers 1L [ obs ]) |> Run.expectOk |> ignore

            let summary =
                Run.run 5 budgetId (Run.fetchers 2L [ { obs with FlagColor = Some "purple"; FlagName = Some "Lena" } ])
                |> Run.expectOk

            Expect.equal summary.ChangedEntries 1 "eine geänderte Buchung, obwohl nur das Flag sich ändert"

        testCase "Loeschung in YNAB setzt das Flag, loescht aber nichts" <| fun () ->
            let budgetId = Run.budget "loeschung"
            let obs = { observation "loeschung-1" with AccountId = YnabAccountId (Guid.NewGuid()) }

            Run.run 4 budgetId (Run.fetchers 1L [ obs ]) |> Run.expectOk |> ignore
            let entryId = findEntryByLink YnabSystem "loeschung-1" |> Async.RunSynchronously |> Option.get

            let summary =
                Run.run 5 budgetId (Run.fetchers 2L [ { obs with Deleted = true } ]) |> Run.expectOk

            Expect.equal summary.DeletedEntries 1 "eine geloeschte Buchung"

            let entry = loadEntry entryId |> Async.RunSynchronously
            Expect.isSome entry "die Buchung steht weiter im Kassenbuch"
            Expect.isTrue entry.Value.DeletedInYnab "mit dem Loeschungs-Flag"
            Expect.equal entry.Value.Amount obs.Amount "und weiter lesbaren Zahlen"
            Expect.contains
                (loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun e -> e.EventType))
                "EntryDeletionObservedInYnab"
                "die Loeschung steht im Log"

        testCase "ein Uebertrag kommt als zwei Buchungen mit mitgefuehrter Verknuepfung an" <| fun () ->
            let budgetId = Run.budget "uebertrag"
            let kontoA = YnabAccountId (Guid.NewGuid())
            let kontoB = YnabAccountId (Guid.NewGuid())

            let hin =
                { observation "uebertrag-hin" with
                    AccountId = kontoA
                    Amount = eur -1000.00m
                    Category = None
                    TransferAccountId = Some kontoB
                    TransferYnabTransactionId = Some "uebertrag-her" }

            let her =
                { observation "uebertrag-her" with
                    AccountId = kontoB
                    Amount = eur 1000.00m
                    Category = None
                    TransferAccountId = Some kontoA
                    TransferYnabTransactionId = Some "uebertrag-hin" }

            let summary = Run.run 4 budgetId (Run.fetchers 1L [ hin; her ]) |> Run.expectOk
            Expect.equal summary.NewEntries 2 "zwei Buchungen, eine je Konto (ADR 0015)"

            let entryHin = findEntryByLink YnabSystem "uebertrag-hin" |> Async.RunSynchronously |> Option.get
            let entryHer = findEntryByLink YnabSystem "uebertrag-her" |> Async.RunSynchronously |> Option.get
            Expect.notEqual entryHin entryHer "zwei verschiedene Buchungen"

            let entryA = loadEntry entryHin |> Async.RunSynchronously |> Option.get
            let entryB = loadEntry entryHer |> Async.RunSynchronously |> Option.get
            Expect.notEqual entryA.AccountId entryB.AccountId "je Konto eine Buchung"
            Expect.isNone entryA.TransferEntryId "die Aufloesung zur Gegen-Buchung ist v2 (ADR 0015)"

            match loadEventsForEntry entryHin |> Async.RunSynchronously with
            | [ { Event = { Body = EntryObservedInYnab payload } } ] ->
                Expect.equal payload.TransferAccountId (Some kontoB) "Gegenkonto mitgefuehrt"
                Expect.equal
                    payload.TransferYnabTransactionId
                    (Some "uebertrag-her")
                    "Gegenbuchungs-Id mitgefuehrt"
            | events -> failtestf "genau eine Erstbeobachtung erwartet, waren %A" events

        testCase "Matching nimmt die YNAB-Id vor der ImportId" <| fun () ->
            let budgetId = Run.budget "matching"
            let ynabAcc = YnabAccountId (Guid.NewGuid())
            let importId = $"BB:{Guid.NewGuid():N}"

            // Zwei Buchungen: die erste traegt beide Fremd-Ids, die zweite nur die
            // ImportId der ersten waere ein Fehlgriff — sie hat ihre eigene YNAB-Id.
            let ersteObs =
                { observation "matching-ynab-1" with AccountId = ynabAcc; ImportId = Some importId }

            Run.run 4 budgetId (Run.fetchers 1L [ ersteObs ]) |> Run.expectOk |> ignore
            let ersteEntry =
                findEntryByLink YnabSystem "matching-ynab-1" |> Async.RunSynchronously |> Option.get

            // Zweiter Lauf: dieselbe YNAB-Id, aber eine andere ImportId. Wuerde die
            // ImportId zuerst greifen, entstuende eine neue Buchung.
            let summary =
                Run.run
                    5
                    budgetId
                    (Run.fetchers 2L [ { ersteObs with ImportId = Some $"BB:{Guid.NewGuid():N}"; Memo = Some "neu" } ])
                |> Run.expectOk

            Expect.equal summary.NewEntries 0 "die YNAB-Id hat gematcht, keine zweite Buchung"
            Expect.equal summary.ChangedEntries 1 "dieselbe Buchung wurde geaendert"
            Expect.equal
                (findEntryByLink YnabSystem "matching-ynab-1" |> Async.RunSynchronously)
                (Some ersteEntry)
                "die YNAB-Id zeigt weiter auf dieselbe Buchung"

        testCase "ohne YNAB-Id matcht die ImportId und die YNAB-Id wird nachgetragen" <| fun () ->
            let budgetId = Run.budget "importid"
            let ynabAcc = YnabAccountId (Guid.NewGuid())
            let importId = $"BB:{Guid.NewGuid():N}"

            let ersteObs =
                { observation "importid-alt" with AccountId = ynabAcc; ImportId = Some importId }
            Run.run 4 budgetId (Run.fetchers 1L [ ersteObs ]) |> Run.expectOk |> ignore
            let entryId = findEntryByLink YnabSystem "importid-alt" |> Async.RunSynchronously |> Option.get

            // YNAB hat die Buchung neu angelegt (andere Transaction-Id), die ImportId
            // ist dieselbe: das ist dieselbe Buchung (ADR 0015, Schritt 2).
            let summary =
                Run.run 5 budgetId (Run.fetchers 2L [ { ersteObs with YnabTransactionId = "importid-neu" } ])
                |> Run.expectOk

            Expect.equal summary.NewEntries 0 "keine zweite Buchung fuer dieselbe ImportId"
            Expect.equal
                (findEntryByLink YnabSystem "importid-neu" |> Async.RunSynchronously)
                (Some entryId)
                "die neue YNAB-Id wurde derselben Buchung nachgetragen"

        testCase "Rate-Limit bricht den Lauf ab und steht im Lauf-Status" <| fun () ->
            let budgetId = Run.budget "ratelimit"
            let obs = { observation "ratelimit-1" with AccountId = YnabAccountId (Guid.NewGuid()) }

            match Run.run 6 budgetId (Run.failingTransactions (YnabError.RateLimitExceeded 60) [ obs ]) with
            | Ok _ -> failtest "ein 429 darf keinen erfolgreichen Lauf ergeben"
            | Error message ->
                Expect.stringContains (message.ToLowerInvariant()) "rate" "der Fehler benennt das Rate-Limit"

            Expect.isNone
                (findEntryByLink YnabSystem "ratelimit-1" |> Async.RunSynchronously)
                "ein abgebrochener Lauf schreibt keine Buchung"
            Expect.isNone
                (loadServerKnowledge budgetId |> Async.RunSynchronously)
                "ein abgebrochener Lauf merkt sich kein server_knowledge"

            match loadLastMirrorRun () |> Async.RunSynchronously with
            | None -> failtest "der Lauf-Status fehlt"
            | Some status ->
                Expect.equal status.RanAt (Run.at 6) "Zeitpunkt des Laufs"
                match status.Outcome with
                | MirrorRunFailed message ->
                    Expect.stringContains (message.ToLowerInvariant()) "rate" "Fehlertext im Status"
                | MirrorRunSucceeded _ -> failtest "der Status muesste den Fehler zeigen"

        testCase "ein gelungener Lauf hinterlaesst seinen Status" <| fun () ->
            let budgetId = Run.budget "status"
            let obs = { observation "status-1" with AccountId = YnabAccountId (Guid.NewGuid()) }

            Run.run 7 budgetId (Run.fetchers 42L [ obs ]) |> Run.expectOk |> ignore

            match loadLastMirrorRun () |> Async.RunSynchronously with
            | None -> failtest "der Lauf-Status fehlt"
            | Some status ->
                Expect.equal status.RanAt (Run.at 7) "Zeitpunkt des Laufs"
                match status.Outcome with
                | MirrorRunSucceeded summary ->
                    Expect.equal summary.NewEntries 1 "eine neue Buchung im Status"
                    Expect.equal summary.ServerKnowledge 42L "server_knowledge im Status"
                | MirrorRunFailed message -> failtest $"der Lauf sollte gelingen, war: {message}"

        testCase "das gemerkte server_knowledge geht in den naechsten Abruf" <| fun () ->
            let budgetId = Run.budget "knowledge"
            let obs = { observation "knowledge-1" with AccountId = YnabAccountId (Guid.NewGuid()) }
            let seen = ResizeArray<int64 option>()

            let recording (knowledge: int64) : MirrorFetchers =
                let inner = Run.fetchers knowledge [ obs ]
                { inner with
                    FetchTransactions =
                        fun lastKnowledge ->
                            seen.Add lastKnowledge
                            inner.FetchTransactions lastKnowledge }

            Run.run 4 budgetId (recording 500L) |> Run.expectOk |> ignore
            Run.run 5 budgetId (recording 501L) |> Run.expectOk |> ignore

            Expect.equal
                (List.ofSeq seen)
                [ None; Some 500L ]
                "erst Erstbefuellung ohne Knowledge, dann Delta ab dem gemerkten Stand"

        testCase "Quick-Add-Buchung wird vom naechsten Spiegel-Lauf ueber die YNAB-Id gefunden, keine zweite Buchung" <| fun () ->
            // ADR 0021/ledger-tdnpk: addCashExpense verknuepft die Quick-Add-Buchung schon
            // ueber die Push-Antwort. Der naechste Spiegel-Lauf muss sie darum ueber die
            // YNAB-Id treffen (ADR 0015, Match-Reihenfolge) und darf keine zweite Buchung
            // fuer dieselbe Bar-Ausgabe anlegen.
            let budgetId = Run.budget "quickadd-mirror"
            let quickAddYnabAccount = YnabAccountId (Guid.NewGuid())
            let ledgerAccountId = newLedgerAccountId ()
            saveAccount {
                Id = ledgerAccountId
                Name = "Bar"
                Kind = CashAccount
                Link = YnabMirrored quickAddYnabAccount
                Closed = false
            }
            |> Async.RunSynchronously

            let input : CashExpenseInput = {
                AccountId = ledgerAccountId
                Date = DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -6.50m
                Payee = Some "Kiosk"
                Memo = None
                Category = None
            }
            let ynabTxId = "quickadd-1"
            let push () : Async<YnabResult<string>> = async.Return (Ok ynabTxId)

            let entryId =
                match addCashExpense (Run.at 3) push (fun () -> ()) input |> Async.RunSynchronously with
                | Ok entryId -> entryId
                | Error err -> failtest $"addCashExpense sollte gelingen, war: {err}"

            // Unveraenderte Daten unter derselben YNAB-Id: der Lauf darf nichts Neues sehen.
            // `Approved = false` wie beim Push (ADR 0032): Quick Add sendet kein
            // `approved`, sonst sähe der Lauf hier fälschlich eine Genehmigung.
            let obs = {
                observation ynabTxId with
                    AccountId = quickAddYnabAccount
                    Date = input.Date
                    Amount = input.Amount
                    Payee = input.Payee
                    Memo = input.Memo
                    Category = None
                    Approved = false
            }
            let summary = Run.run 4 budgetId (Run.fetchers 99L [ obs ]) |> Run.expectOk

            Expect.equal summary.NewEntries 0 "keine neue Buchung fuer die schon verknuepfte Quick-Add-Buchung"
            Expect.equal summary.ChangedEntries 0 "unveraenderte Daten ergeben kein Aenderungs-Ereignis"
            Expect.equal
                (findEntryByLink YnabSystem ynabTxId |> Async.RunSynchronously)
                (Some entryId)
                "die YNAB-Id zeigt weiterhin auf die Quick-Add-Buchung"
            Expect.hasLength
                (loadEntriesForAccount ledgerAccountId |> Async.RunSynchronously)
                1
                "genau eine Buchung auf dem Bar-Konto"

        testCase "per Import-Push angelegte Buchung wird vom nächsten Spiegel-Lauf über die YNAB-Id gefunden, keine zweite Buchung" <| fun () ->
            // ADR 0021/ledger-qvt84: pushComdirectImports verknüpft die Buchung schon
            // über die Push-Antwort. Der nächste Spiegel-Lauf muss sie über die
            // YNAB-Id treffen (ADR 0015, Match-Reihenfolge) und darf keine zweite
            // Buchung für denselben Comdirect-Import anlegen.
            let budgetId = Run.budget "import-mirror"
            let comdirectYnabAccount = YnabAccountId (Guid.NewGuid())
            let ledgerAccountId = newLedgerAccountId ()
            saveAccount {
                Id = ledgerAccountId
                Name = "Comdirect Giro"
                Kind = BankAccount
                Link = YnabMirrored comdirectYnabAccount
                Closed = false
            }
            |> Async.RunSynchronously

            let txId = "tx-import-mirror"
            let importId = "BB:tximportmirror"
            let date = DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc)
            let amount = eur -19.90m

            // Das tatsächlich vorbereitete Request-Objekt, wie `prepareTransactions`
            // (`Server.YnabClient`) es für den Push bauen würde: PayeeName mit
            // "Unknown"-Fallback, Memo über `buildMemoWithReference` (immer mit
            // ", Ref: <Referenz>"-Suffix, nie das rohe Bank-Memo unverändert).
            let bankMemo = "Kartenzahlung"
            let reference = "ref-import-mirror"
            let sentMemo = buildMemoWithReference bankMemo reference
            let sentPayeeName = "Rossmann"
            let request : YnabTransactionRequest = {
                AccountId = "irrelevant"
                Date = "2026-02-09"
                Amount = -19900
                PayeeName = sentPayeeName
                Memo = sentMemo
                Cleared = "uncleared"
                ImportId = importId
                CategoryId = None
                Subtransactions = None
            }

            // Wie `Server.Api.importToYnab` (Verifier-Fund, Iteration 3): Payee/Memo der
            // Kassenbuch-Entstehung kommen aus dem gesendeten Request, nicht aus den
            // rohen Bank-Feldern.
            let candidate : ComdirectImportCandidate = {
                Prepared = { TransactionId = TransactionId txId; ImportId = importId; Request = request }
                ComdirectReference = reference
                Date = date
                Amount = amount
                Payee = Some request.PayeeName
                Memo = Some request.Memo
                ReviewSteps = []
                ExistingEntryImportId = None
            }
            let ynabTxId = "import-push-1"
            let push (prepared: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
                async.Return (
                    Ok {
                        emptyCreateResult with
                            Created =
                                prepared
                                |> List.map (fun p ->
                                    ({ TransactionId = p.TransactionId; ImportId = p.ImportId; YnabTransactionId = ynabTxId }
                                        : CreatedTransactionResult))
                    })

            let entryId =
                match
                    pushComdirectImports (Run.at 3) push (fun () -> ()) ledgerAccountId [ candidate ]
                    |> Async.RunSynchronously
                    |> fun r -> Map.tryFind (TransactionId txId) r.Outcomes
                with
                | Some (Linked (entryId, _)) -> entryId
                | other -> failtest $"pushComdirectImports sollte Linked liefern, war {other}"

            // Die Beobachtung des Spiegel-Laufs ist das, was YNAB tatsächlich hat: die
            // gesendeten Request-Werte, nicht von Hand nachgebaute rohe Bank-Werte
            // (Verifier-Fund, Iteration 3, sonst deckt der Test die Memo/Payee-Abweichung
            // nicht auf).
            // `Approved = false` wie beim Push (ADR 0032): der Comdirect-Import sendet
            // kein `approved`, sonst sähe der Lauf hier fälschlich eine Genehmigung.
            let obs = {
                observation ynabTxId with
                    AccountId = comdirectYnabAccount
                    ImportId = Some importId
                    Date = date
                    Amount = amount
                    Payee = Some request.PayeeName
                    Memo = Some request.Memo
                    Category = None
                    Approved = false
            }
            let summary = Run.run 4 budgetId (Run.fetchers 99L [ obs ]) |> Run.expectOk

            Expect.equal summary.NewEntries 0 "keine neue Buchung für die schon verknüpfte Import-Buchung"
            Expect.equal summary.ChangedEntries 0 "unveränderte Daten ergeben kein Änderungs-Ereignis"
            Expect.equal
                (findEntryByLink YnabSystem ynabTxId |> Async.RunSynchronously)
                (Some entryId)
                "die YNAB-Id zeigt weiterhin auf die per Import-Push angelegte Buchung"
            Expect.hasLength
                (loadEntriesForAccount ledgerAccountId |> Async.RunSynchronously)
                1
                "genau eine Buchung auf dem Comdirect-Konto"

            // Explizit: kein Kopfdaten-Ereignis aus dem Spiegel-Lauf im Log. Nur die
            // aggregierte `ChangedEntries`-Zahl zu prüfen hätte das falsche
            // `EntryDetailsChangeObservedInYnab` verdecken können, wenn gleichzeitig ein
            // anderer Zähler kompensiert hätte.
            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
            Expect.equal
                (recorded |> List.map (fun r -> r.EventType))
                [ "EntryImportedFromComdirect"; "EntryLinkedToYnab" ]
                "der Spiegel-Lauf hängt kein EntryDetailsChangeObservedInYnab an, weil Memo/Payee bereits übereinstimmen"
    ]

// ============================================
// Einzel-Flug-Schutz: zwei gleichzeitige Auslöser ergeben nur einen Lauf
// (ledger-h4p6c, ADR 0014 Punkt 3, Task-Nachtrag: hält auch serverseitig, nicht nur
// im Client-Zustand)
// ============================================

[<Tests>]
let mirrorGuardTests =
    // Siehe mirrorRunTests oben: Initialisierung erst innerhalb der Testausführung,
    // nicht beim Modul-Laden (ADR 0030, infra-sdad8).
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testSequenced
    <| testList "Ledger Spiegel: Einzel-Flug-Schutz (runMirrorGuarded)" [

        testCase "zwei gleichzeitige Auslöser teilen sich einen Lauf, nur ein YNAB-Abruf" <| fun () ->
            let budgetId = Run.budget "guard-concurrent"
            let obs = { observation "guard-concurrent-1" with AccountId = YnabAccountId (Guid.NewGuid()) }
            let baseFetchers = Run.fetchers 1L [ obs ]
            let mutable fetchCount = 0
            let fetchers : MirrorFetchers = {
                baseFetchers with
                    FetchTransactions =
                        fun knowledge ->
                            async {
                                fetchCount <- fetchCount + 1
                                // Künstliche Verzögerung, damit der zweite Auslöser
                                // garantiert während des ersten Laufs ankommt, sonst
                                // wäre der Test von der Ausführungsreihenfolge des
                                // Thread-Pools abhängig, statt den Schutz selbst zu
                                // prüfen.
                                do! Async.Sleep 100
                                return! baseFetchers.FetchTransactions knowledge
                            }
            }

            let results =
                Async.Parallel [
                    runMirrorGuarded (Run.at 3) fetchers budgetId
                    runMirrorGuarded (Run.at 3) fetchers budgetId
                ]
                |> Async.RunSynchronously

            Expect.equal fetchCount 1 "nur ein YNAB-Abruf trotz zwei gleichzeitiger Auslöser"
            for result in results do
                match result with
                | Ok summary -> Expect.equal summary.NewEntries 1 "beide Auslöser teilen das Ergebnis desselben Laufs"
                | Error err -> failtest $"beide Auslöser sollten gelingen, war: {err}"

        testCase "zwei nacheinander abgeschlossene Läufe teilen sich nichts" <| fun () ->
            let budgetId = Run.budget "guard-sequential"
            let obs = { observation "guard-sequential-1" with AccountId = YnabAccountId (Guid.NewGuid()) }
            let baseFetchers = Run.fetchers 1L [ obs ]
            let mutable fetchCount = 0
            let fetchers : MirrorFetchers = {
                baseFetchers with
                    FetchTransactions =
                        fun knowledge ->
                            async {
                                fetchCount <- fetchCount + 1
                                return! baseFetchers.FetchTransactions knowledge
                            }
            }

            runMirrorGuarded (Run.at 3) fetchers budgetId |> Async.RunSynchronously |> ignore
            runMirrorGuarded (Run.at 4) fetchers budgetId |> Async.RunSynchronously |> ignore

            Expect.equal fetchCount 2 "ein Lauf nach dem anderen ist kein gleichzeitiger Auslöser"

        testCase "ein fehlgeschlagener Lauf gibt den Einzel-Flug-Schutz wieder frei" <| fun () ->
            // Verifier-Fund Iteration 1: `MirrorGuard.runGuarded` räumte `inFlight` nur
            // beim normalen Abschluss ab. Wirft der Lauf-Task, blieb der Platzhalter
            // stehen, und jeder spätere Aufrufer bekam denselben, kaputten Task zurück,
            // dauerhaft, bis zum Neustart. Direkter Zugriff auf `MirrorGuard` (jetzt
            // `internal`), weil `runMirror` selbst so gut wie jeden Fehler intern
            // abfängt: dieser Test prüft den Schutz unabhängig davon.
            let mutable runCount = 0
            let failingRun () : Async<Result<MirrorRunSummary, string>> =
                runCount <- runCount + 1
                async { return failwith "geplanter Fehler im Testlauf" }
            let succeedingRun () : Async<Result<MirrorRunSummary, string>> =
                runCount <- runCount + 1
                async {
                    return Ok {
                        NewEntries = 0
                        ChangedEntries = 0
                        DeletedEntries = 0
                        ServerKnowledge = 1L
                    }
                }

            Expect.throws
                (fun () -> MirrorGuard.runGuarded failingRun |> Async.RunSynchronously |> ignore)
                "der erste Lauf wirft, wie im Testlauf geplant"

            let result = MirrorGuard.runGuarded succeedingRun |> Async.RunSynchronously

            Expect.equal runCount 2 "der zweite Aufruf startet einen frischen Lauf, nicht den kaputten Platzhalter"
            match result with
            | Ok _ -> ()
            | Error err -> failtest $"der zweite Lauf sollte gelingen, war: {err}"
    ]

// ============================================
// Hintergrund-Start: eine Ausnahme darf den Prozess nicht mitreißen
// (ledger-h4p6c, Verifier-Fund Iteration 1; `triggerMirrorInBackground` nutzt
// `Async.Start`, das unbehandelte Ausnahmen sonst auf dem Thread-Pool verschluckt)
// ============================================

[<Tests>]
let runMirrorInBackgroundTests =
    // Siehe mirrorRunTests oben: Initialisierung erst innerhalb der Testausführung,
    // nicht beim Modul-Laden (ADR 0030, infra-sdad8).
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testList "Ledger Spiegel: Hintergrund-Start (runMirrorInBackground)" [

        testCase "eine Ausnahme aus dem Lauf propagiert nicht und landet als fehlgeschlagener Lauf-Status" <| fun () ->
            let failingRun () : Async<Result<MirrorRunSummary, string>> =
                async { return failwith "geplanter Fehler beim Hintergrund-Start" }

            // Propagiert `runMirrorInBackground` die Ausnahme doch, wirft schon dieser
            // Aufruf, nicht erst eine Assertion danach.
            runMirrorInBackground failingRun |> Async.RunSynchronously

            match loadLastMirrorRun () |> Async.RunSynchronously with
            | Some { Outcome = MirrorRunFailed message } ->
                Expect.stringContains
                    message
                    "geplanter Fehler beim Hintergrund-Start"
                    "der Fehlertext aus dem Lauf landet im Lauf-Status (ADR 0014: sichtbar, nicht verschluckt)"
            | other -> failtest $"ein fehlgeschlagener Lauf-Status wurde erwartet, war: {other}"

        testCase "ein erfolgreicher Lauf propagiert unauffällig" <| fun () ->
            let succeedingRun () : Async<Result<MirrorRunSummary, string>> =
                async {
                    return Ok {
                        NewEntries = 0
                        ChangedEntries = 0
                        DeletedEntries = 0
                        ServerKnowledge = 1L
                    }
                }

            // Kein Wurf ist hier schon die Aussage des Tests.
            runMirrorInBackground succeedingRun |> Async.RunSynchronously
    ]
