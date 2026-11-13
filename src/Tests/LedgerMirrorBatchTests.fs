module LedgerMirrorBatchTests

// ledger-r8h19: Der Spiegel-Lauf lädt Verknüpfungen und Buchungen eines Batches einmal statt je
// Beobachtung. Der Charakterisierungstest hält fest, welche Ereignisse ein Batch mit allen
// Fällen schreibt, die einander im selben Lauf sehen müssen: neue Buchung, Treffer über die
// YNAB-Id, Treffer über die ImportId, Übertragspaar im selben Batch (die zweite Seite findet die
// erste, die eben erst entstanden ist) und die Lösch-Beobachtung einer ersetzten Split-Id
// (ADR 0058 Punkt 5). Er lief vor dem Umbau grün und läuft danach unverändert.

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Server.YnabObservations

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }

let private lebensmittel = {
    CategoryId = YnabCategoryId (Guid.Parse "0195d0b0-0000-7000-8000-0000000000c1")
    CategoryName = "Lebensmittel r8h19"
}

let private haushalt = {
    CategoryId = YnabCategoryId (Guid.Parse "0195d0b0-0000-7000-8000-0000000000c2")
    CategoryName = "Haushalt r8h19"
}

// Buchungsdatum weit in der Vergangenheit, damit die geteilte In-Memory-DB die neueste-zuerst-Seiten
// anderer Tests nicht verschiebt.
let private bookingDate = DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)

let private at (day: int) = DateTime(2026, 11, day, 6, 0, 0, DateTimeKind.Utc)

let private observationOn (account: YnabAccountId) (ynabTxId: string) : YnabObservation = {
    YnabTransactionId = ynabTxId
    AccountId = account
    ImportId = None
    Date = bookingDate
    Amount = eur -21.99m
    Payee = Some "Rewe"
    Memo = None
    Category = Some lebensmittel
    Splits = []
    Cleared = Uncleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    MatchedYnabTransactionId = None
    SplitTransfers = []
    Deleted = false
    Approved = true
    FlagColor = None
    FlagName = None
}

let private fetchersFor (accounts: YnabAccountId list) (observations: YnabObservation list) : MirrorFetchers = {
    emptyMirrorFetchers with
        FetchAccounts =
            fun _ ->
                async.Return (
                    Ok {
                        Accounts =
                            accounts
                            |> List.mapi (fun i a -> { AccountId = a; Name = $"Konto r8h19 {i + 1}"; Closed = false; Deleted = false })
                        ServerKnowledge = 1L
                    })
        FetchTransactions = fun _ -> async.Return (Ok { Observations = observations; ServerKnowledge = 1L })
}

let private runOk (day: int) (budget: YnabBudgetId) (fetchers: MirrorFetchers) : MirrorRunSummary =
    match runMirror (at day) fetchers budget |> Async.RunSynchronously with
    | Ok summary -> summary
    | Error err -> failtest $"Lauf sollte gelingen, war: {err}"

let private eventTypesOf (entryId: EntryId) : string list =
    loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)

let private entryOf (system: LinkSystem) (value: string) : EntryId =
    match findEntryByLink system value |> Async.RunSynchronously with
    | Some id -> id
    | None -> failtest $"keine Buchung zu {value}"

[<Tests>]
let tests =
    testSequenced
    <| testList "Spiegel-Lauf: ein Batch mit allen Fällen (ledger-r8h19, Charakterisierung)" [
        testCase "neue Buchung, Treffer über YNAB-Id und ImportId, Übertragspaar und ersetzte Id schreiben dieselben Ereignisse" <| fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            let tag = Guid.NewGuid().ToString("N").Substring(0, 8)
            let giro = YnabAccountId (Guid.NewGuid())
            let tagesgeld = YnabAccountId (Guid.NewGuid())
            let budget = YnabBudgetId $"budget-r8h19-{tag}"
            let fetchers = fetchersFor [ giro; tagesgeld ]
            let id (name: string) = $"r8h19-{tag}-{name}"

            // Vorlauf: Konten anlegen, die Buchung "bekannt" und die später ersetzte Buchung.
            runOk 2 budget (fetchers [ observationOn giro (id "bekannt"); observationOn giro (id "alt") ]) |> ignore
            let ledgerGiro =
                match findAccountByYnabAccountId giro |> Async.RunSynchronously with
                | Some account -> account.Id
                | None -> failtest "Konto fehlt nach dem Vorlauf"

            // Die ersetzte Split-Id (ADR 0058): "alt" ist abgelöst, "neu" gilt.
            let replaced = entryOf YnabSystem (id "alt")
            appendAndApply [
                { EntryId = replaced
                  OccurredAt = at 2
                  Actor = UserInBb
                  Body = EntryReplacedInYnab { OldYnabTransactionId = id "alt"; NewYnabTransactionId = id "neu"; NewImportId = None } }
            ]
            |> Async.RunSynchronously

            // Eine Comdirect-Buchung, die YNAB über die ImportId zurückmeldet.
            let imported = newEntryId ()
            appendAndApply [
                { EntryId = imported
                  OccurredAt = at 2
                  Actor = ComdirectImport
                  Body =
                    EntryImportedFromComdirect {
                        AccountId = ledgerGiro
                        ImportId = id "import"
                        ComdirectReference = id "ref"
                        Date = bookingDate
                        Amount = eur -21.99m
                        Payee = Some "Rewe"
                        Memo = None
                        Cleared = Uncleared
                        Approved = false
                        FlagColor = None
                        FlagName = None
                    } }
            ]
            |> Async.RunSynchronously

            let known = entryOf YnabSystem (id "bekannt")
            let batch = [
                observationOn giro (id "frisch")
                { observationOn giro (id "bekannt") with Category = Some haushalt }
                { observationOn giro (id "per-import") with ImportId = Some (id "import") }
                { observationOn giro (id "ab") with
                    TransferAccountId = Some tagesgeld
                    TransferYnabTransactionId = Some (id "an") }
                { observationOn tagesgeld (id "an") with
                    Amount = eur 21.99m
                    TransferAccountId = Some giro
                    TransferYnabTransactionId = Some (id "ab") }
                { observationOn giro (id "alt") with Deleted = true }
            ]
            let before (entryId: EntryId) = eventTypesOf entryId |> List.length
            let knownBefore, importedBefore, replacedBefore = before known, before imported, before replaced

            let summary = runOk 3 budget (fetchers batch)

            let added (entryId: EntryId) (countBefore: int) = eventTypesOf entryId |> List.skip countBefore
            Expect.equal (summary.NewEntries, summary.ChangedEntries, summary.DeletedEntries) (3, 2, 0) "Zähler: frisch, ab, an neu; bekannt und per-import geändert; nichts gelöscht"
            Expect.equal (eventTypesOf (entryOf YnabSystem (id "frisch"))) [ "EntryObservedInYnab" ] "neue Buchung"
            Expect.equal (added known knownBefore) [ "EntryCategorizationChangeObservedInYnab" ] "Treffer über die YNAB-Id"
            Expect.equal (entryOf YnabSystem (id "per-import")) imported "die ImportId findet die Comdirect-Buchung"
            Expect.equal
                (added imported importedBefore)
                [ "EntryLinkedToYnab"; "EntryCategorizationChangeObservedInYnab"; "EntryApprovalChangeObservedInYnab" ]
                "Treffer über die ImportId: Verknüpfung und Beobachtung"
            let ab, an = entryOf YnabSystem (id "ab"), entryOf YnabSystem (id "an")
            Expect.equal (eventTypesOf ab) [ "EntryObservedInYnab"; "EntryTransferCounterpartObservedInYnab" ] "Übertrag, erste Seite"
            Expect.equal (eventTypesOf an) [ "EntryObservedInYnab"; "EntryTransferCounterpartObservedInYnab" ] "Übertrag, zweite Seite"
            let loaded (entryId: EntryId) = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.equal (loaded ab).TransferEntryId (Some an) "erste Seite zeigt auf die zweite"
            Expect.equal (loaded an).TransferEntryId (Some ab) "zweite Seite zeigt auf die erste"
            Expect.equal (added replaced replacedBefore) [] "die Lösch-Beobachtung der ersetzten Id schreibt nichts"
            Expect.isFalse (loaded replaced).DeletedInYnab "die ersetzte Buchung bleibt bestehen"
    ]

// ============================================
// Offene Erfassung im Lauf: nur ein eindeutiges Paar verknüpft (ledger-ay81j)
// ============================================

/// Eine in BB erfasste, offene Buchung auf `account` mit denselben Werten wie `observationOn`.
let private openCapture (account: LedgerAccountId) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        { EntryId = entryId; OccurredAt = at 3; Actor = QuickAdd
          Body =
            CashExpenseAdded {
                AccountId = account; Date = bookingDate; Amount = eur -21.99m; Payee = Some "Rewe"; Memo = None
                Category = Some lebensmittel; Cleared = Uncleared; Approved = true; ImportId = None } }
    ]
    |> Async.RunSynchronously
    entryId

/// Führt den Lauf aus und liefert, was er auf die Konsole schrieb (die Log-Zeilen aus What 2).
let private runCapturingLog (day: int) (budget: YnabBudgetId) (fetchers: MirrorFetchers) : MirrorRunSummary * string =
    let original = Console.Out
    use writer = new IO.StringWriter()
    Console.SetOut writer
    try
        let summary = runOk day budget fetchers
        summary, writer.ToString()
    finally
        Console.SetOut original

[<Tests>]
let openCaptureTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())
    let setup () =
        let tag = Guid.NewGuid().ToString("N").Substring(0, 8)
        let giro = YnabAccountId (Guid.NewGuid())
        let budget = YnabBudgetId $"budget-ay81j-{tag}"
        runOk 2 budget (fetchersFor [ giro ] []) |> ignore
        let ledgerGiro =
            match findAccountByYnabAccountId giro |> Async.RunSynchronously with
            | Some account -> account.Id
            | None -> failtest "Konto fehlt nach dem Vorlauf"
        tag, giro, budget, ledgerGiro
    let isOpen (entryId: EntryId) = not (loadEntry entryId |> Async.RunSynchronously |> Option.get).LinkedToYnab

    testSequenced
    <| testList "Spiegel-Lauf: offene Erfassung (ledger-ay81j)" [
        testCase "ein eindeutiges Paar wird verknüpft, observe vergleicht danach gegen den verknüpften Stand" <| fun () ->
            let tag, giro, budget, ledgerGiro = setup ()
            let entryId = openCapture ledgerGiro
            let summary = runOk 3 budget (fetchersFor [ giro ] [ observationOn giro $"ay81j-{tag}-eins" ])
            Expect.equal summary.NewEntries 0 "keine neue Buchung"
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryLinkedToYnab" ] "nur die Verknüpfung, keine Änderung"
            Expect.equal (entryOf YnabSystem $"ay81j-{tag}-eins") entryId "die YNAB-Id zeigt auf die Erfassung"

        testCase "zwei passende Erfassungen zu einer YNAB-Buchung: keine Verknüpfung, Log-Zeile, beide bleiben offen" <| fun () ->
            let tag, giro, budget, ledgerGiro = setup ()
            let first, second = openCapture ledgerGiro, openCapture ledgerGiro
            let ynabId = $"ay81j-{tag}-eine"
            let summary, log = runCapturingLog 3 budget (fetchersFor [ giro ] [ observationOn giro ynabId ])
            Expect.equal summary.NewEntries 1 "die YNAB-Buchung wird eine neue Buchung"
            Expect.isTrue (isOpen first && isOpen second) "beide Erfassungen bleiben offen"
            Expect.stringContains log $"Offene Erfassung nicht verknüpft: mehrere Buchungen in BB passen zu YNAB-Buchung {ynabId}" "Log-Zeile"

        testCase "zwei passende YNAB-Buchungen zu einer Erfassung: keine Verknüpfung, Log-Zeile, die Erfassung bleibt offen" <| fun () ->
            let tag, giro, budget, ledgerGiro = setup ()
            let entryId = openCapture ledgerGiro
            let summary, log =
                runCapturingLog 3 budget (fetchersFor [ giro ] [ observationOn giro $"ay81j-{tag}-a"; observationOn giro $"ay81j-{tag}-b" ])
            Expect.equal summary.NewEntries 2 "beide YNAB-Buchungen werden neue Buchungen"
            Expect.isTrue (isOpen entryId) "die Erfassung bleibt offen"
            Expect.stringContains log $"Offene Erfassung nicht verknüpft: mehrere YNAB-Buchungen passen zu Buchung {entryId}" "Log-Zeile"
    ]
