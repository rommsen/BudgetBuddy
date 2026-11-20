module LedgerRetryPushTests

// "Erneut senden" am Hinweis "Noch nicht in YNAB" (ledger-cks06): Push genau dieser einen
// nicht verknüpften Buchung, Verknüpfung bei Erfolg, Fehlschlag mit Grund und Zeitpunkt,
// Ablehnung ohne Push bei verknüpften oder gelöschten Buchungen. Außerdem hält ein Test
// fest, dass der Spiegel-Lauf eine nicht verknüpfte Buchung nicht selbst pusht.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Persistence
open Ledger.CommandHandlers

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private accountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000c5606")
let private at = DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc)
let private noTrigger () = ()

/// Legt eine Quick-Add-Buchung mit eigenem Payee an und liefert ihre Id.
let private newEntry (label: string) : EntryId =
    let entryId = newEntryId ()
    let payload : CashExpenseAddedPayload = {
        AccountId = accountId
        Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
        Amount = eur -4.50m
        Payee = Some $"cks06-{label}-{Guid.NewGuid()}"
        Memo = None
        Category = None
        Cleared = Uncleared
        Approved = false
        ImportId = None
    }
    appendAndApply [ { EntryId = entryId; OccurredAt = at; Actor = QuickAdd; Body = CashExpenseAdded payload } ]
    |> Async.RunSynchronously
    entryId

let private append (entryId: EntryId) (body: LedgerEventBody) =
    appendAndApply [ { EntryId = entryId; OccurredAt = at; Actor = QuickAdd; Body = body } ]
    |> Async.RunSynchronously

/// Ein Push, der seine Aufrufe mitzählt (die Id der gesendeten Buchung).
let private countingPush (result: YnabResult<string>) =
    let calls = ResizeArray<EntryId>()
    let push : EntryPush =
        fun entry ->
            calls.Add entry.Id
            async.Return result
    calls, push

/// Ein Spiegel-Lauf vor „Erneut senden“, der nichts verknüpft (ledger-ay81j).
let private mirrorFindsNothing () : Async<Result<MirrorRunSummary, string>> =
    async.Return (Ok { NewEntries = 0; ChangedEntries = 0; DeletedEntries = 0; ServerKnowledge = 0L })

let private retry (push: EntryPush) (entryId: EntryId) =
    retryPushToYnab (at.AddHours 2.0) loadEntryOrigin mirrorFindsNothing push noTrigger entryId |> Async.RunSynchronously

let private eventTypesOf (entryId: EntryId) =
    loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)

[<Tests>]
let retryPushTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    // Geteilt: die In-Memory-DB. Die DB-Listen laufen nacheinander in dieser Gruppe, parallel zum Rest
    // (`TestGroups.InMemoryDb`).
    testSequencedGroup TestGroups.InMemoryDb
    <| testList "Ledger Erneut senden (Noch nicht in YNAB)" [

        testCase "Erfolg: pusht genau diese Buchung und verknüpft sie, der Fehlschlag ist weg" <| fun () ->
            let entryId = newEntry "ok"
            let other = newEntry "andere"
            append entryId (EntryPushToYnabFailed { Reason = "Netzwerkfehler: Timeout" })
            let calls, push = countingPush (Ok "ynab-cks06-1")
            Expect.equal (retry push entryId) (Ok PushToYnabSent) "gesendet"
            Expect.equal (List.ofSeq calls) [ entryId ] "genau ein Push, für genau diese Buchung"
            let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.isTrue entry.LinkedToYnab "verknüpft"
            Expect.isNone entry.PushToYnabFailure "Fehlschlag gelöscht"
            let links = linksForEntry entryId |> Async.RunSynchronously
            Expect.equal (currentYnabTransactionId links) (Some "ynab-cks06-1") "YNAB-Id gesetzt"
            let otherEntry = loadEntry other |> Async.RunSynchronously |> Option.get
            Expect.isFalse otherEntry.LinkedToYnab "andere Buchung unberührt"

        testCase "Fehlschlag: speichert Grund und neuen Zeitpunkt, bleibt nicht verknüpft" <| fun () ->
            let entryId = newEntry "fail"
            append entryId (EntryPushToYnabFailed { Reason = "alter Grund" })
            let _, push = countingPush (Error (YnabError.NetworkError "Timeout"))
            let expected = ynabErrorText (YnabError.NetworkError "Timeout")
            Expect.equal (retry push entryId) (Ok (PushToYnabFailed expected)) "Fehlschlag gemeldet"
            let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.isFalse entry.LinkedToYnab "nicht verknüpft"
            Expect.equal
                entry.PushToYnabFailure
                (Some { Reason = expected; FailedAt = at.AddHours 2.0 })
                "neuer Grund und neuer Zeitpunkt ersetzen den alten"

        testCase "eine schon verknüpfte Buchung wird abgelehnt, ohne Push" <| fun () ->
            let entryId = newEntry "linked"
            append entryId (EntryLinkedToYnab { YnabTransactionId = "ynab-cks06-2" })
            let calls, push = countingPush (Ok "ynab-cks06-zweite")
            Expect.isError (retry push entryId) "abgelehnt"
            Expect.isEmpty calls "kein Push"
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryLinkedToYnab" ] "keine neuen Ereignisse"

        testCase "eine in YNAB gelöschte Buchung wird abgelehnt, ohne Push" <| fun () ->
            let entryId = newEntry "deleted"
            append entryId EntryDeletionObservedInYnab
            let calls, push = countingPush (Ok "ynab-cks06-3")
            Expect.isError (retry push entryId) "abgelehnt"
            Expect.isEmpty calls "kein Push"

        testCase "eine unbekannte Buchung wird abgelehnt, ohne Push" <| fun () ->
            let calls, push = countingPush (Ok "ynab-cks06-4")
            Expect.isError (retry push (newEntryId ())) "abgelehnt"
            Expect.isEmpty calls "kein Push"

        testCase "der Spiegel-Auslöser läuft einmal nach Erfolg, nie nach Fehlschlag oder Ablehnung" <| fun () ->
            // ledger-bk3q8: der Auslöser war in diesen Tests nie gezählt (`noTrigger`).
            let mutable triggers = 0
            let trigger () = triggers <- triggers + 1
            let run (push: EntryPush) (entryId: EntryId) =
                retryPushToYnab (at.AddHours 2.0) loadEntryOrigin mirrorFindsNothing push trigger entryId |> Async.RunSynchronously
            let _, failing = countingPush (Error (YnabError.NetworkError "Timeout"))
            Expect.equal (run failing (newEntry "auslöser-fehlschlag")) (Ok (PushToYnabFailed (ynabErrorText (YnabError.NetworkError "Timeout")))) "Fehlschlag"
            Expect.equal triggers 0 "kein Auslöser nach Fehlschlag"
            let _, ok = countingPush (Ok $"ynab-bk3q8-{Guid.NewGuid()}")
            Expect.isError (run ok (newEntryId ())) "Ablehnung"
            Expect.equal triggers 0 "kein Auslöser nach Ablehnung"
            Expect.equal (run ok (newEntry "auslöser-erfolg")) (Ok PushToYnabSent) "Erfolg"
            Expect.equal triggers 1 "genau ein Auslöser nach Erfolg"

        testCase "Verknüpfung und Fehlschlag aus Erneut senden tragen den Actor Roman in BB" <| fun () ->
            let actorsAfter (result: YnabResult<string>) =
                let entryId = newEntry "actor"
                let _, push = countingPush result
                retry push entryId |> ignore
                loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType, r.Event.Actor)
            Expect.equal
                (actorsAfter (Ok $"ynab-bk3q8-{Guid.NewGuid()}"))
                [ "CashExpenseAdded", QuickAdd; "EntryLinkedToYnab", UserInBb ]
                "Verknüpfung mit UserInBb"
            Expect.equal
                (actorsAfter (Error (YnabError.NetworkError "Timeout")))
                [ "CashExpenseAdded", QuickAdd; "EntryPushToYnabFailed", UserInBb ]
                "Fehlschlag mit UserInBb"
    ]

// ============================================
// Erneut senden unter der Schreib-Sperre (ADR 0055 Punkt 6, ledger-kfd5m)
// ============================================

[<Tests>]
let retryPushWriteGuardTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    // Geteilt: die In-Memory-DB (`TestGroups.InMemoryDb`), dazu die prozessweite Schreib-Sperre: jeder Fall hält
    // einen Spiegel-Lauf fest, und Listen, die über alle Buchungen zählen, sähen seine Buchungen.
    testSequencedGroup TestGroups.InMemoryDb
    <| testList "Ledger Erneut senden (Schreib-Sperre)" [

        testCase "Erneut senden während eines Spiegel-Laufs pusht und verknüpft erst nach dem Lauf" <| fun () ->
            // ledger-kfd5m: `retryPushToYnab` lief ohne Schreib-Sperre. Ein Lauf zwischen
            // Push und `EntryLinkedToYnab` sah die YNAB-Buchung ohne Partner und legte eine
            // zweite Buchung an.
            let entryId = newEntry "sperre"
            append entryId (EntryPushToYnabFailed { Reason = "Netzwerkfehler: Timeout" })
            let calls, push = countingPush (Ok $"ynab-kfd5m-{Guid.NewGuid()}")

            let result =
                LedgerWriteGuardProbe.commandWaitsForMirror
                    (fun () ->
                        Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryPushToYnabFailed" ] "noch keine Verknüpfung während des Laufs"
                        Expect.isEmpty calls "noch kein Push")
                    (retryPushToYnab (at.AddHours 2.0) loadEntryOrigin mirrorFindsNothing push noTrigger entryId)

            Expect.equal result (Ok PushToYnabSent) "gesendet"
            Expect.equal
                (eventTypesOf entryId)
                [ "CashExpenseAdded"; "EntryPushToYnabFailed"; "EntryLinkedToYnab" ]
                "Verknüpfung erst nach dem Lauf"
            Expect.equal calls.Count 1 "ein Push"

        testCase "der Spiegel-Auslöser nach Erneut senden läuft erst nach Freigabe der Sperre" <| fun () ->
            // ledger-kfd5m: `triggerMirror` sitzt hinter dem gesperrten Abschnitt. Ein
            // Auslöser, der synchron einen gesperrten Lauf abwartet, darf nicht blockieren.
            let entryId = newEntry "auslöser"
            let _, push = countingPush (Ok $"ynab-kfd5m-{Guid.NewGuid()}")
            let trigger = LedgerWriteGuardProbe.SyncMirrorTrigger(2000)

            LedgerWriteGuardProbe.serialized (fun () ->
                let run = Async.StartAsTask(retryPushToYnab (at.AddHours 2.0) loadEntryOrigin mirrorFindsNothing push trigger.Trigger entryId)
                Expect.isTrue (run.Wait 10000) "Befehl beendet")

            Expect.equal trigger.FinishedInTime (Some true) "der Lauf des Auslösers endet in der Frist, die Sperre ist schon frei"
    ]

// ============================================
// Erneut senden einer offenen Erfassung: erst der Spiegel (ledger-ay81j, ADR 0021 Nachtrag 2026-11-13)
// ============================================

/// Ein mit YNAB verknüpftes Kassenbuch-Konto, wie nach einem Spiegel-Lauf.
let private mirroredAccount () : Ledger.Domain.LedgerAccount * YnabAccountId =
    let ynab = YnabAccountId (Guid.NewGuid())
    let account : Ledger.Domain.LedgerAccount =
        { Id = LedgerAccountId (Guid.NewGuid()); Name = $"Bar-{Guid.NewGuid()}"; Kind = Ledger.Domain.CashAccount
          Link = Ledger.Domain.YnabMirrored ynab; Closed = false }
    saveAccount account |> Async.RunSynchronously
    account, ynab

/// Eine Quick-Add-Buchung auf `account` mit offenem Fehlschlag (Zeitüberschreitung).
let private openCapture (account: LedgerAccountId) (payee: string) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        { EntryId = entryId; OccurredAt = at; Actor = QuickAdd
          Body =
            CashExpenseAdded {
                AccountId = account; Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc); Amount = eur -4.50m
                Payee = Some payee; Memo = None; Category = None; Cleared = Uncleared; Approved = false; ImportId = None } }
        { EntryId = entryId; OccurredAt = at; Actor = QuickAdd; Body = EntryPushToYnabFailed { Reason = "Zeitüberschreitung" } }
    ]
    |> Async.RunSynchronously
    entryId

/// Der echte Spiegel-Lauf über gefälschte Abrufe: YNAB hält genau `observations` auf `ynab`.
let private mirrorSeeing (ynab: YnabAccountId) (observations: Server.YnabObservations.YnabObservation list) () =
    let fetchers = {
        emptyMirrorFetchers with
            FetchAccounts =
                fun _ -> async.Return (Ok { Accounts = [ { AccountId = ynab; Name = "Bar"; Closed = false; Deleted = false } ]; ServerKnowledge = 3L })
            FetchTransactions = fun _ -> async.Return (Ok { Observations = observations; ServerKnowledge = 3L })
    }
    runMirror (at.AddHours 1.0) fetchers (YnabBudgetId $"budget-ay81j-{Guid.NewGuid()}")

let private observed (ynabTxId: string) (ynab: YnabAccountId) (payee: string) : Server.YnabObservations.YnabObservation = {
    YnabTransactionId = ynabTxId; AccountId = ynab; ImportId = None
    Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc); Amount = eur -4.50m; Payee = Some payee; Memo = None
    Category = None; Splits = []; SplitTransfers = []; Cleared = Uncleared; TransferAccountId = None
    TransferYnabTransactionId = None; MatchedYnabTransactionId = None; Deleted = false; Approved = false
    FlagColor = None; FlagName = None }

let private fakeSettings : string -> Async<string option> =
    let map = Map [ "ynab_token", "token-kuenstlich"; "ynab_default_budget_id", "budget-kuenstlich" ]
    fun key -> async.Return (Map.tryFind key map)

[<Tests>]
let retryOpenCaptureTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    // Geteilt: die In-Memory-DB. Die DB-Listen laufen nacheinander in dieser Gruppe, parallel zum Rest
    // (`TestGroups.InMemoryDb`).
    testSequencedGroup TestGroups.InMemoryDb
    <| testList "Ledger Erneut senden einer offenen Erfassung (ledger-ay81j)" [

        testCase "(a) der Spiegel verknüpft die Erfassung: kein Push, gesendet" <| fun () ->
            // Verhindert eine zweite YNAB-Buchung, wenn ein früherer Push ohne import_id ankam.
            let account, ynab = mirroredAccount ()
            let payee = $"ay81j-{Guid.NewGuid()}"
            let entryId = openCapture account.Id payee
            let ynabTxId = $"ynab-ay81j-{Guid.NewGuid()}"
            let calls, push = countingPush (Ok "ynab-nie")
            let mutable triggers = 0
            let result =
                retryPushToYnab (at.AddHours 2.0) loadEntryOrigin (mirrorSeeing ynab [ observed ynabTxId ynab payee ]) push (fun () -> triggers <- triggers + 1) entryId
                |> Async.RunSynchronously
            Expect.equal result (Ok PushToYnabSent) "gesendet"
            Expect.isEmpty calls "kein Push"
            Expect.equal (findEntryByLink Ledger.Domain.YnabSystem ynabTxId |> Async.RunSynchronously) (Some entryId) "der Spiegel hat verknüpft"
            Expect.equal triggers 0 "kein weiterer Auslöser"

        testCase "(b) scheitert der Spiegel, kein Push, kein Ereignis und der Satz für den Toast" <| fun () ->
            let account, _ = mirroredAccount ()
            let entryId = openCapture account.Id $"ay81j-{Guid.NewGuid()}"
            let calls, push = countingPush (Ok "ynab-nie")
            for failing in [ (fun () -> async.Return (Error "HTTP 503")); (fun () -> async { return failwith "weg" }) ] do
                let result = retryPushToYnab (at.AddHours 2.0) loadEntryOrigin failing push noTrigger entryId |> Async.RunSynchronously
                Expect.equal result (Error retryMirrorUnavailableText) "der Satz aus What 3"
            Expect.isEmpty calls "kein Push"
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryPushToYnabFailed" ] "kein Ereignis"

        testCase "(c) findet der Spiegel nichts, genau ein Push ohne import_id" <| fun () ->
            let account, ynab = mirroredAccount ()
            let payee = $"ay81j-{Guid.NewGuid()}"
            let entryId = openCapture account.Id payee
            let sent = ResizeArray<string option>()
            let create importId _ _ _ _ _ _ =
                sent.Add importId
                async.Return (Ok $"ynab-ay81j-{Guid.NewGuid()}")
            let push = Server.Api.entryPushWith fakeSettings create |> Async.RunSynchronously
            let other = observed $"ynab-ay81j-{Guid.NewGuid()}" ynab "jemand anderes"
            let result =
                retryPushToYnab (at.AddHours 2.0) loadEntryOrigin (mirrorSeeing ynab [ other ]) push noTrigger entryId |> Async.RunSynchronously
            Expect.equal result (Ok PushToYnabSent) "gesendet"
            Expect.equal (List.ofSeq sent) [ None ] "genau ein Push, ohne import_id"

        testCase "(d) ein Comdirect-Import ruft den Spiegel nicht" <| fun () ->
            let account, _ = mirroredAccount ()
            let entryId = newEntryId ()
            let reference = $"ay81j-{Guid.NewGuid():N}".Substring(0, 20)
            appendAndApply [
                { EntryId = entryId; OccurredAt = at; Actor = ComdirectImport
                  Body =
                    EntryImportedFromComdirect {
                        AccountId = account.Id; ImportId = $"BB:{reference}"; ComdirectReference = reference
                        Date = at.Date; Amount = eur -9m; Payee = Some "Markt"; Memo = None
                        Cleared = Uncleared; Approved = false; FlagColor = None; FlagName = None } }
            ]
            |> Async.RunSynchronously
            let calls, push = countingPush (Ok $"ynab-ay81j-{Guid.NewGuid()}")
            let mutable mirrors = 0
            let mirror () =
                mirrors <- mirrors + 1
                async.Return (Error "darf nicht laufen")
            let result = retryPushToYnab (at.AddHours 2.0) loadEntryOrigin mirror push noTrigger entryId |> Async.RunSynchronously
            Expect.equal result (Ok PushToYnabSent) "gesendet"
            Expect.equal mirrors 0 "kein Spiegel"
            Expect.equal calls.Count 1 "ein Push"

        testCase "(e) ein Comdirect-Import, dessen import_id YNAB schon kennt (409): gesendet, kein Fehlschlag, der Auslöser läuft" <| fun () ->
            // Die verlorene Antwort eines Comdirect-Pushs: 409 heißt angekommen (ledger-p3vq8), nicht gescheitert.
            let account, _ = mirroredAccount ()
            let entryId = newEntryId ()
            let reference = $"ay81j-{Guid.NewGuid():N}".Substring(0, 20)
            appendAndApply [
                { EntryId = entryId; OccurredAt = at; Actor = ComdirectImport
                  Body =
                    EntryImportedFromComdirect {
                        AccountId = account.Id; ImportId = $"BB:{reference}"; ComdirectReference = reference
                        Date = at.Date; Amount = eur -9m; Payee = Some "Markt"; Memo = None
                        Cleared = Uncleared; Approved = false; FlagColor = None; FlagName = None } }
                { EntryId = entryId; OccurredAt = at; Actor = ComdirectImport; Body = EntryPushToYnabFailed { Reason = "Zeitüberschreitung" } }
            ]
            |> Async.RunSynchronously
            let calls, push = countingPush (Error YnabError.ImportIdAlreadyInYnab)
            let mutable triggers = 0
            let result =
                retryPushToYnab (at.AddHours 2.0) loadEntryOrigin mirrorFindsNothing push (fun () -> triggers <- triggers + 1) entryId
                |> Async.RunSynchronously
            Expect.equal result (Ok PushToYnabSent) "gesendet"
            Expect.equal calls.Count 1 "ein Push"
            Expect.equal triggers 1 "der Spiegel-Auslöser läuft"
            Expect.equal (eventTypesOf entryId) [ "EntryImportedFromComdirect"; "EntryPushToYnabFailed" ] "kein neuer Fehlschlag"

        testCase "Live: Erneut senden des Servers fragt bei einer Erfassung zuerst YNAB, ohne Einrichtung kein Push" <| fun () ->
            // Belegt die Verdrahtung in `Api.fs` (`retryPushToYnabWith entryPush mirrorYnabBudget`): die
            // Test-Datenbank hat kein YNAB-Token, der echte Spiegel scheitert, also kein Push und kein Ereignis.
            // Ohne den Spiegel-Port schriebe der Push ein `EntryPushToYnabFailed` („No YNAB token configured“).
            // Mit Token erreichte der echte Spiegel YNAB; der Fall läuft nur ohne.
            Expect.isNone (global.Persistence.Settings.getSetting "ynab_token" |> Async.RunSynchronously) "kein Token in der Test-Datenbank"
            let account, _ = mirroredAccount ()
            let entryId = openCapture account.Id $"ay81j-{Guid.NewGuid()}"
            let (EntryId guid) = entryId
            let result = Server.Api.ledgerApi.retryPushToYnab guid |> Async.RunSynchronously
            Expect.equal result (Error retryMirrorUnavailableText) "der Satz aus What 3"
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryPushToYnabFailed" ] "kein Ereignis"
    ]
