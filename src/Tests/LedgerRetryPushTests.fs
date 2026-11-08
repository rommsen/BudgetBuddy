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

let private retry (push: EntryPush) (entryId: EntryId) =
    retryPushToYnab (at.AddHours 2.0) push noTrigger entryId |> Async.RunSynchronously

let private eventTypesOf (entryId: EntryId) =
    loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)

[<Tests>]
let retryPushTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testSequenced
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
                retryPushToYnab (at.AddHours 2.0) push trigger entryId |> Async.RunSynchronously
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

    testSequenced
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
                    (retryPushToYnab (at.AddHours 2.0) push noTrigger entryId)

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
                let run = Async.StartAsTask(retryPushToYnab (at.AddHours 2.0) push trigger.Trigger entryId)
                Expect.isTrue (run.Wait 10000) "Befehl beendet")

            Expect.equal trigger.FinishedInTime (Some true) "der Lauf des Auslösers endet in der Frist, die Sperre ist schon frei"
    ]
