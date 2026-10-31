module LedgerPushFailureTests

// Gescheiterter Push nach YNAB als Ereignis (ledger-rynk5, ADR 0021 Punkt 2, Nachtrag).
//
//  * reine Domäne: Codec, `apply`, Replay, Lesemodell, Historie;
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall, ADR 0030)
//    mit injiziertem Push, der nie YNAB erreicht.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Server.YnabClient

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }

let private accountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000c1")
let private at = DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc)
let private later = DateTime(2026, 10, 19, 11, 30, 0, DateTimeKind.Utc)
let private noTrigger () = ()

let private eventAt (time: DateTime) (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent = {
    EntryId = entryId
    OccurredAt = time
    Actor = actor
    Body = body
}

let private observedPayload (ynabTxId: string) : EntryObservedInYnabPayload = {
    AccountId = accountId
    YnabTransactionId = ynabTxId
    ImportId = None
    Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -12.00m
    Payee = Some "Rewe"
    Memo = None
    Category = None
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = None
    FlagName = None
}

let private cashAdded : CashExpenseAddedPayload = {
    AccountId = accountId
    Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -3.00m
    Payee = Some "Kiosk"
    Memo = None
    Category = None
    Cleared = Uncleared
    Approved = false
}

/// Eine Comdirect-Buchung vor der Verknüpfung (Zustand per `apply`).
let private comdirectEntry () : EntryId * Entry =
    let entryId = newEntryId ()
    let created =
        eventAt at entryId QuickAdd (CashExpenseAdded cashAdded)
    entryId, apply None created

let private failedEvent (time: DateTime) (entryId: EntryId) (reason: string) =
    eventAt time entryId ComdirectImport (EntryPushToYnabFailed { Reason = reason })

let private linkedEvent (actor: Actor) (entryId: EntryId) =
    eventAt later entryId actor (EntryLinkedToYnab { YnabTransactionId = "ynab-x" })

let private viewOf (e: Entry) =
    Ledger.QueryHandlers.ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab e

// ============================================
// Reine Domäne
// ============================================

[<Tests>]
let pushFailureDomainTests =
    testList "Ledger Push-Fehlschlag (Domäne)" [

        testCase "Codec-Rundreise und Ereignis-Name, Schema-Version 1" <| fun () ->
            let body = EntryPushToYnabFailed { Reason = "Netzwerkfehler: größer als erwartet" }
            Expect.equal (eventTypeName body) "EntryPushToYnabFailed" "Name"
            Expect.equal (schemaVersion body) 1 "Version"
            Expect.equal (decodePayload "EntryPushToYnabFailed" 1 (encodePayload body)) (Ok body) "Rundreise"

        testCase "apply hält Grund und Zeitpunkt, die Buchung ist noch nicht verknüpft" <| fun () ->
            let entryId, e0 = comdirectEntry ()
            Expect.isFalse e0.LinkedToYnab "vor der Verknüpfung"
            Expect.isNone e0.PushToYnabFailure "nichts offen"
            let failed = apply (Some e0) (failedEvent at entryId "Timeout")
            Expect.equal failed.PushToYnabFailure (Some { Reason = "Timeout"; FailedAt = at }) "Grund und Zeit"
            Expect.isFalse failed.LinkedToYnab "Fehlschlag verknüpft nicht"

        testCase "ein zweiter Fehlschlag ersetzt Grund und Zeitpunkt des ersten" <| fun () ->
            let entryId, e0 = comdirectEntry ()
            let first = apply (Some e0) (failedEvent at entryId "Timeout")
            let second = apply (Some first) (failedEvent later entryId "HTTP 429")
            Expect.equal second.PushToYnabFailure (Some { Reason = "HTTP 429"; FailedAt = later }) "ersetzt"

        testCase "EntryLinkedToYnab löst den Fehlschlag auf, auch mit Actor YnabMirror" <| fun () ->
            for actor in [ ComdirectImport; QuickAdd; YnabMirror ] do
                let entryId, e0 = comdirectEntry ()
                let failed = apply (Some e0) (failedEvent at entryId "Timeout")
                let linked = apply (Some failed) (linkedEvent actor entryId)
                Expect.isNone linked.PushToYnabFailure $"aufgelöst durch {actor}"
                Expect.isTrue linked.LinkedToYnab $"verknüpft durch {actor}"

        testCase "Replay ergibt denselben Zustand: Fehlschlag offen" <| fun () ->
            let entryId = newEntryId ()
            let events = [
                eventAt at entryId QuickAdd (CashExpenseAdded cashAdded)
                failedEvent at entryId "Timeout"
                failedEvent later entryId "HTTP 429"
            ]
            let folded = events |> List.fold (fun s ev -> Some (apply s ev)) None
            Expect.equal (replay events) folded "Replay gleich Faltung"
            Expect.equal (replay events |> Option.get).PushToYnabFailure (Some { Reason = "HTTP 429"; FailedAt = later }) "offen"

        testCase "Replay ergibt denselben Zustand: Fehlschlag aufgelöst" <| fun () ->
            let entryId = newEntryId ()
            let events = [
                eventAt at entryId QuickAdd (CashExpenseAdded cashAdded)
                failedEvent at entryId "Timeout"
                linkedEvent YnabMirror entryId
            ]
            let folded = events |> List.fold (fun s ev -> Some (apply s ev)) None
            Expect.equal (replay events) folded "Replay gleich Faltung"
            let final = replay events |> Option.get
            Expect.isNone final.PushToYnabFailure "aufgelöst"
            Expect.isTrue final.LinkedToYnab "verknüpft"

        testCase "eine vom Spiegel gesehene Buchung ist von Anfang an verknüpft" <| fun () ->
            let entryId = newEntryId ()
            let e = apply None (eventAt at entryId YnabMirror (EntryObservedInYnab (observedPayload "ynab-m")))
            Expect.isTrue e.LinkedToYnab "Spiegel"
            Expect.isTrue (viewOf e).LinkedToYnab "Sicht"

        testCase "LedgerEntryView: LinkedToYnab und PushToYnabFailure folgen dem Zustand" <| fun () ->
            let entryId, e0 = comdirectEntry ()
            Expect.isFalse (viewOf e0).LinkedToYnab "Comdirect-Buchung vor der Verknüpfung"
            Expect.isNone (viewOf e0).PushToYnabFailure "nichts offen"
            let failed = apply (Some e0) (failedEvent at entryId "Timeout")
            Expect.equal
                (viewOf failed).PushToYnabFailure
                (Some { Reason = "Timeout"; FailedAt = at })
                "Grund und Zeitpunkt des Ereignisses"
            let linked = apply (Some failed) (linkedEvent ComdirectImport entryId)
            Expect.isTrue (viewOf linked).LinkedToYnab "nach EntryLinkedToYnab"
            Expect.isNone (viewOf linked).PushToYnabFailure "Fehlschlag aufgelöst"

        testCase "Historie: Push nach YNAB gescheitert mit Grund" <| fun () ->
            Expect.equal
                (Ledger.QueryHandlers.ReadModel.describe (EntryPushToYnabFailed { Reason = "Netzwerkfehler: Timeout" }))
                "Push nach YNAB gescheitert: Netzwerkfehler: Timeout"
                "Satz"

        testCase "Gründe nennen die Fehlerart" <| fun () ->
            Expect.stringContains (ynabErrorText (YnabError.NetworkError "Timeout")) "Netzwerkfehler" "Netz"
            Expect.stringContains (ynabErrorText (YnabError.RateLimitExceeded 30)) "429" "Limit"
            Expect.stringContains (ynabErrorText (YnabError.Unauthorized "x")) "401" "Zugang"
    ]

// ============================================
// Befehle (In-Memory-SQLite)
// ============================================

module private Fixtures =
    let request (importId: string) : YnabTransactionRequest = {
        AccountId = "11111111-1111-1111-1111-111111111111"
        Date = "2026-03-01"
        Amount = -1740
        PayeeName = "dm"
        Memo = ""
        Cleared = "uncleared"
        ImportId = importId
        CategoryId = None
        Subtransactions = None
        FlagColor = None
    }

    let candidate (txId: string) (importId: string) : ComdirectImportCandidate = {
        Prepared = { TransactionId = TransactionId txId; ImportId = importId; Request = request importId }
        ComdirectReference = $"ref-{txId}"
        Date = DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        Amount = eur -17.40m
        Payee = Some "dm"
        RawPayee = Some "dm"
        Memo = None
        ReviewSteps = []
        ExistingEntryImportId = None
    }

    let pushResult (result: YnabResult<TransactionCreateResult>) (_: PreparedTransaction list) =
        async.Return result

    let ids () =
        let txId = $"tx-{Guid.NewGuid()}"
        txId, $"import-{txId}"

let private eventTypesOf (entryId: EntryId) : string list =
    loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)

let private runPush (result: YnabResult<TransactionCreateResult>) (txId: string) (importId: string) =
    pushComdirectImports
        (DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc))
        (Fixtures.pushResult result)
        noTrigger
        accountId
        [ Fixtures.candidate txId importId ]
    |> Async.RunSynchronously

let private writtenEntryId (result: ComdirectPushResult) (txId: string) : EntryId =
    match Map.tryFind (TransactionId txId) result.Outcomes with
    | Some (WrittenNotLinked entryId) -> entryId
    | other -> failtest $"erwartete WrittenNotLinked, war {other}"

let private expectFailureEvent (entryId: EntryId) (reasonPart: string) =
    let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
    Expect.equal
        (recorded |> List.map (fun r -> r.EventType))
        [ "EntryImportedFromComdirect"; "EntryPushToYnabFailed" ]
        "genau ein Fehlschlag-Ereignis, keine Verknüpfung"
    Expect.equal (recorded |> List.map (fun r -> r.Event.Actor)) [ ComdirectImport; ComdirectImport ] "Actor des Push-Wegs"
    match (recorded |> List.last).Event.Body with
    | EntryPushToYnabFailed p -> Expect.stringContains p.Reason reasonPart "Grund nennt die Fehlerart"
    | other -> failtest $"unerwartetes Ereignis {other}"
    let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
    Expect.isSome entry.PushToYnabFailure "Zustand trägt den Fehlschlag"
    Expect.isFalse entry.LinkedToYnab "nicht verknüpft"
    Expect.equal
        (Some entry)
        (replay (recorded |> List.map (fun r -> r.Event)))
        "gespeicherter Zustand gleich Replay"

[<Tests>]
let pushFailureCommandTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Ledger Push-Fehlschlag (Befehle)" [

        testCase "Netzfehler: die geschriebene Buchung trägt genau ein EntryPushToYnabFailed" <| fun () ->
            let txId, importId = Fixtures.ids ()
            let result = runPush (Error (YnabError.NetworkError "Timeout")) txId importId
            expectFailureEvent (writtenEntryId result txId) "Netzwerkfehler"

        testCase "HTTP 429: der Grund nennt die Fehlerart" <| fun () ->
            let txId, importId = Fixtures.ids ()
            let result = runPush (Error (YnabError.RateLimitExceeded 30)) txId importId
            expectFailureEvent (writtenEntryId result txId) "429"

        testCase "nicht auswertbare Antwort: der Grund nennt die Fehlerart" <| fun () ->
            let txId, importId = Fixtures.ids ()
            let result = runPush (Ok { emptyCreateResult with ResponseUnparseable = true }) txId importId
            expectFailureEvent (writtenEntryId result txId) "nicht auswertbar"

        testCase "Duplikat: der Grund nennt das Duplikat" <| fun () ->
            let txId, importId = Fixtures.ids ()
            let result = runPush (Ok { emptyCreateResult with DuplicateImportIds = [ importId ] }) txId importId
            expectFailureEvent (writtenEntryId result txId) "Duplikat"

        testCase "erfolgreicher Push schreibt kein Fehlschlag-Ereignis" <| fun () ->
            let txId, importId = Fixtures.ids ()
            let created : CreatedTransactionResult =
                { TransactionId = TransactionId txId; ImportId = importId; YnabTransactionId = $"ynab-{txId}" }
            let result = runPush (Ok { emptyCreateResult with Created = [ created ] }) txId importId
            match Map.tryFind (TransactionId txId) result.Outcomes with
            | Some (Linked (entryId, _)) ->
                Expect.equal (eventTypesOf entryId) [ "EntryImportedFromComdirect"; "EntryLinkedToYnab" ] "nur Verknüpfung"
                let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
                Expect.isTrue entry.LinkedToYnab "verknüpft"
            | other -> failtest $"erwartete Linked, war {other}"

        testCase "eine bereits verknüpfte Buchung (AlreadyImported) bekommt kein Fehlschlag-Ereignis" <| fun () ->
            let txId, importId = Fixtures.ids ()
            let created : CreatedTransactionResult =
                { TransactionId = TransactionId txId; ImportId = importId; YnabTransactionId = $"ynab-{txId}" }
            runPush (Ok { emptyCreateResult with Created = [ created ] }) txId importId |> ignore
            let second = runPush (Error (YnabError.NetworkError "Timeout")) txId importId
            match Map.tryFind (TransactionId txId) second.Outcomes with
            | Some (AlreadyImported entryId) ->
                Expect.equal (eventTypesOf entryId) [ "EntryImportedFromComdirect"; "EntryLinkedToYnab" ] "unverändert"
            | other -> failtest $"erwartete AlreadyImported, war {other}"

        testCase "erneuter Versuch nach Fehlschlag mit Erfolg löst den Fehlschlag auf" <| fun () ->
            let txId, importId = Fixtures.ids ()
            let first = runPush (Error (YnabError.NetworkError "Timeout")) txId importId
            let entryId = writtenEntryId first txId
            let created : CreatedTransactionResult =
                { TransactionId = TransactionId txId; ImportId = importId; YnabTransactionId = $"ynab-{txId}" }
            runPush (Ok { emptyCreateResult with Created = [ created ] }) txId importId |> ignore
            let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.isNone entry.PushToYnabFailure "aufgelöst"
            Expect.isTrue entry.LinkedToYnab "verknüpft"

        testCase "addCashExpense mit scheiterndem Push schreibt CashExpenseAdded, dann EntryPushToYnabFailed" <| fun () ->
            let payee = $"Kiosk-{Guid.NewGuid()}"
            let input : CashExpenseInput = {
                AccountId = accountId
                Date = DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -3.20m
                Payee = Some payee
                Memo = None
                Category = None
            }
            let push () : Async<YnabResult<string>> = async.Return (Error (YnabError.NetworkError "Timeout"))
            match addCashExpense QuickAdd at push noTrigger input |> Async.RunSynchronously with
            | Error _ -> ()
            | Ok _ -> failtest "der Push-Fehler wird durchgereicht"
            let entry =
                loadEntriesForAccount accountId
                |> Async.RunSynchronously
                |> List.filter (fun e -> e.Payee = Some payee)
                |> List.exactlyOne
            let recorded = loadEventsForEntry entry.Id |> Async.RunSynchronously
            Expect.equal
                (recorded |> List.map (fun r -> r.EventType))
                [ "CashExpenseAdded"; "EntryPushToYnabFailed" ]
                "erst Kassenbuch, dann Fehlschlag"
            Expect.equal (recorded |> List.map (fun r -> r.Event.Actor)) [ QuickAdd; QuickAdd ] "Actor Quick Add"
            Expect.equal
                entry.PushToYnabFailure
                (Some { Reason = ynabErrorText (YnabError.NetworkError "Timeout"); FailedAt = at })
                "Grund und Zeitpunkt"

        testCase "addCashExpense mit gelingendem Push schreibt kein Fehlschlag-Ereignis" <| fun () ->
            let input : CashExpenseInput = {
                AccountId = accountId
                Date = DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -4.20m
                Payee = Some "Kiosk"
                Memo = None
                Category = None
            }
            let push () : Async<YnabResult<string>> = async.Return (Ok $"ynab-{Guid.NewGuid()}")
            match addCashExpense QuickAdd at push noTrigger input |> Async.RunSynchronously with
            | Ok entryId ->
                Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryLinkedToYnab" ] "nur Verknüpfung"
            | Error err -> failtest $"sollte gelingen, war {err}"
    ]

[<Tests>]
let pushFailureMigrationTests =
    testList "Ledger Push-Fehlschlag (Migration)" [

        testCase "Migration trägt linked_to_ynab aus der Fremd-Id-Tabelle nach" <| fun () ->
            use conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
            conn.Open()
            let exec (sql: string) =
                use cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, conn)
                cmd.ExecuteNonQuery() |> ignore
            exec "CREATE TABLE ledger_entries (id TEXT PRIMARY KEY, account_id TEXT NOT NULL, deleted_in_ynab INTEGER NOT NULL DEFAULT 0)"
            exec "CREATE TABLE ledger_entry_links (entry_id TEXT NOT NULL, system TEXT NOT NULL, value TEXT NOT NULL)"
            exec "INSERT INTO ledger_entries (id, account_id) VALUES ('mit-link', 'k'), ('ohne-link', 'k')"
            exec "INSERT INTO ledger_entry_links VALUES ('mit-link', 'ynab', 'y-1'), ('ohne-link', 'importid', 'i-1')"

            migrateLedgerEntryColumns conn

            use read = new Microsoft.Data.Sqlite.SqliteCommand("SELECT id, linked_to_ynab, push_failure_reason FROM ledger_entries ORDER BY id", conn)
            use reader = read.ExecuteReader()
            Expect.isTrue (reader.Read()) "Zeile mit-link"
            Expect.equal (reader.GetString 0, reader.GetInt64 1) ("mit-link", 1L) "verknüpft"
            Expect.isTrue (reader.IsDBNull 2) "kein Fehlschlag"
            Expect.isTrue (reader.Read()) "Zeile ohne-link"
            Expect.equal (reader.GetString 0, reader.GetInt64 1) ("ohne-link", 0L) "nur ImportId, nicht verknüpft"
            reader.Close()

            // Ein zweiter Lauf (weiterer Prozessstart) wirft nicht und lässt den Backfill-Stand stehen.
            migrateLedgerEntryColumns conn
            use again = new Microsoft.Data.Sqlite.SqliteCommand("SELECT linked_to_ynab FROM ledger_entries WHERE id = 'mit-link'", conn)
            Expect.equal (again.ExecuteScalar() :?> int64) 1L "bleibt verknüpft"
    ]
