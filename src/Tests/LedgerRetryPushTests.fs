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

        testCase "der nächste Spiegel-Lauf pusht nichts und lässt die Buchung unverknüpft" <| fun () ->
            // Der Lauf liest nur aus YNAB (`MirrorFetchers` kennt keinen Schreibaufruf); eine
            // Buchung ohne Verknüpfung bleibt danach unverknüpft. Der Satz der Leiste darf
            // deshalb keinen Retry im Lauf versprechen (ledger-cks06).
            let entryId = newEntry "mirror"
            append entryId (EntryPushToYnabFailed { Reason = "Netzwerkfehler: Timeout" })
            let budgetId = YnabBudgetId "budget-cks06"
            runMirror (at.AddDays 1.0) emptyMirrorFetchers budgetId |> Async.RunSynchronously |> ignore
            let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.isFalse entry.LinkedToYnab "weiter unverknüpft"
            Expect.isSome entry.PushToYnabFailure "Fehlschlag steht weiter"
    ]
