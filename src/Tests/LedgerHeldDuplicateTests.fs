module LedgerHeldDuplicateTests

// Eine Buchung wartet als unsicheres Duplikat (banking-import-d0fey, ADR 0093 Punkt 3, 5 und 6).
//
//  * reine Domäne: Codec, `apply`, Replay, die Entscheidung über eine schon bekannte Buchung;
//  * Lesen gegen In-Memory-SQLite: Kontostand, Kategorie-Aktivität, Buchung nicht in YNAB, Zu prüfen;
//  * jeder schreibende Befehl lehnt sie ab, Sammelbefehle überspringen sie. Die Ports scheitern laut,
//    wenn ein Befehl trotzdem YNAB ruft.

open System

do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers

let private run (work: Async<'T>) = Async.RunSynchronously work
let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private at = DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc)
let private freshAccount () = LedgerAccountId (Guid.NewGuid())
let private category = { CategoryId = YnabCategoryId (Guid.NewGuid()); CategoryName = "Lebensmittel" }

let private ev (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

let private heldPayload : EntryHeldAsPossibleDuplicatePayload =
    { Reason = "In YNAB steht eine Buchung mit gleichem Betrag am 10.11.2026 bei Markt."
      MatchYnabTransactionId = "ynab-treffer"
      MatchEntryId = Some (newEntryId ())
      Match = { Date = DateTime(2026, 11, 10); Amount = eur -50m; Payee = Some "Markt"; CategoryName = Some "Lebensmittel" } }

let private imported (account: LedgerAccountId) (importId: string) : LedgerEventBody =
    EntryImportedFromComdirect {
        AccountId = account
        ImportId = importId
        ComdirectReference = importId
        Date = DateTime(2026, 11, 11)
        Amount = eur -50m
        Payee = Some "Markt"
        Memo = None
        Cleared = Uncleared
        Approved = false
        FlagColor = None
        FlagName = None
    }

/// Das Log einer wartenden Buchung: Entstehung, Regel mit Kategorie, Zurückhalten.
let private heldLogIn (category: CategoryRef) (account: LedgerAccountId) (entryId: EntryId) : LedgerEvent list =
    [ ev entryId ComdirectImport (imported account $"BB:{Guid.NewGuid():N}")
      ev entryId (Rule (RuleId (Guid.NewGuid()))) (EntryCategorizedByRule { RuleName = "Markt"; Category = Some category; Splits = []; PayeeOverride = None })
      ev entryId ComdirectImport (EntryHeldAsPossibleDuplicate heldPayload) ]

let private heldLog = heldLogIn category

/// Schreibt eine wartende Buchung auf ein eigenes Konto, mit eigener Kategorie.
let private seedHeldIn (category: CategoryRef) : LedgerAccountId * EntryId =
    initializeLedgerSchema ()
    let account = freshAccount ()
    let entryId = newEntryId ()
    appendAndApply (heldLogIn category account entryId) |> run
    account, entryId

let private seedHeld () = seedHeldIn category

let private eventCount (entryId: EntryId) = loadEventsForEntry entryId |> run |> List.length

/// Ein Port, der nie gerufen werden darf.
let private noYnab (name: string) : 'a = failtest $"{name}: kein YNAB-Aufruf für eine wartende Buchung"

let private splitTools : SplitWriteTools =
    { IsTrackingAccount = fun _ -> noYnab "IsTrackingAccount"
      PutCategory = fun _ _ -> noYnab "PutCategory"
      PutSplits = fun _ _ -> noYnab "PutSplits"
      Delete = fun _ -> noYnab "Delete"
      Create = fun _ -> noYnab "Create"
      TriggerMirror = ignore }

let private transferTools : TransferWriteTools =
    { Accounts = fun () -> noYnab "Accounts"
      Push = fun _ _ -> noYnab "TransferPush" }

[<Tests>]
let domainTests =
    testList "Wartende Buchung: Domäne" [

        testCase "EntryHeldAsPossibleDuplicate trägt Typname, Schema-Version und übersteht die Codec-Rundreise" <| fun () ->
            let body = EntryHeldAsPossibleDuplicate heldPayload
            Expect.equal (eventTypeName body) "EntryHeldAsPossibleDuplicate" "Typname"
            match decodePayload (eventTypeName body) (schemaVersion body) (encodePayload body) with
            | Ok decoded -> Expect.equal decoded body "Rundreise"
            | Error e -> failtest e
            let withoutMatch = EntryHeldAsPossibleDuplicate { heldPayload with MatchEntryId = None; Match = { heldPayload.Match with Payee = None; CategoryName = None } }
            Expect.equal (decodePayload (eventTypeName withoutMatch) 1 (encodePayload withoutMatch)) (Ok withoutMatch) "ohne bekannten Treffer"

        testCase "apply setzt die Wartemarke, sie verhindert eine Verknüpfung mit YNAB" <| fun () ->
            let entryId = newEntryId ()
            let entry = replay (heldLog (freshAccount ()) entryId) |> Option.get
            Expect.equal entry.PossibleDuplicate (Some heldPayload) "wartet"
            Expect.isTrue (isHeldAsPossibleDuplicate entry) "Prüffunktion"
            Expect.throws
                (fun () -> apply (Some entry) (ev entryId ComdirectImport (EntryLinkedToYnab { YnabTransactionId = "ynab-x" })) |> ignore)
                "EntryLinkedToYnab auf einer wartenden Buchung ist eine Invariantenverletzung"

        testCase "eine schon verknüpfte Buchung lässt sich nicht zurückhalten" <| fun () ->
            let entryId = newEntryId ()
            let linked =
                replay [ ev entryId ComdirectImport (imported (freshAccount ()) "BB:x")
                         ev entryId ComdirectImport (EntryLinkedToYnab { YnabTransactionId = "ynab-x" }) ]
            Expect.throws
                (fun () -> apply linked (ev entryId ComdirectImport (EntryHeldAsPossibleDuplicate heldPayload)) |> ignore)
                "Zurückhalten nach dem Push"

        testCase "Replay: das Log einer wartenden Buchung ergibt denselben Zustand wie die Zustandstabelle" <| fun () ->
            let _, entryId = seedHeld ()
            let fromTable = loadEntry entryId |> run
            let fromLog = loadEventsForEntry entryId |> run |> List.map (fun r -> r.Event) |> replay
            Expect.equal fromLog fromTable "gleich"
            Expect.isSome (fromTable |> Option.bind (fun e -> e.PossibleDuplicate)) "die Marke steht auch in der Tabelle"

        testCase "Wiederholter Abruf: die Entscheidung über eine schon bekannte Buchung" <| fun () ->
            let entryId = newEntryId ()
            let account = freshAccount ()
            let base' = [ ev entryId ComdirectImport (imported account "BB:y") ]
            let decide events = replay events |> Option.get |> knownImportDecision
            Expect.equal (decide (heldLog account entryId)) SkipKnownImport "wartend"
            Expect.equal
                (decide (base' @ [ ev entryId UserInBb (EntryDiscardedInBb { YnabTransactionId = "nie" }) ]))
                SkipKnownImport
                "verworfen"
            Expect.equal
                (decide (base' @ [ ev entryId ComdirectImport (EntryLinkedToYnab { YnabTransactionId = "ynab-y" }) ]))
                AlreadyLinkedImport
                "verknüpft"
            Expect.equal
                (decide (base' @ [ ev entryId ComdirectImport (EntryPushToYnabFailed { Reason = "Timeout" }) ]))
                SendImportAgain
                "Push gescheitert"
            Expect.equal (decide base') SendImportAgain "Ausgang unbekannt"
    ]

[<Tests>]
let readTests =
    testSequenced <| testList "Wartende Buchung: Lesen" [

        testCase "sie zählt im Kontostand, in der Aktivität ihrer Kategorie und in Zu prüfen, nicht in Buchung nicht in YNAB" <| fun () ->
            let own = { CategoryId = YnabCategoryId (Guid.NewGuid()); CategoryName = "Eigene" }
            let account, entryId = seedHeldIn own
            let (LedgerAccountId accountGuid) = account
            let (EntryId entryGuid) = entryId
            let balanceInputs =
                loadAmountsForBalances () |> run
                |> List.filter (fun (id, _, _) -> id = accountGuid)
                |> List.map (fun (id, amount, deleted) -> { AccountId = id; Amount = amount; DeletedInYnab = deleted })
            Expect.equal
                (ledgerBalancesByAccount "EUR" balanceInputs |> Map.tryFind accountGuid)
                (Some (eur -50m))
                "Kontostand"
            let activity =
                Ledger.QueryHandlers.getCategoryMonthlyActivity own.CategoryId [ DateTime(2026, 11, 1) ] |> run
            Expect.equal activity [ DateTime(2026, 11, 1), eur -50m ] "Aktivität der Kategorie"
            let notInYnab = Ledger.QueryHandlers.getEntriesNotInYnab () |> run
            Expect.isFalse (notInYnab |> List.exists (fun e -> e.EntryId = entryGuid)) "nicht unter Buchung nicht in YNAB"
            Expect.equal (countNeedsReviewEntries (Some account) |> run) 1 "zu prüfen"
            let view = Ledger.QueryHandlers.getLedgerEntry entryGuid |> run |> Option.get
            Expect.equal
                view.PossibleDuplicate
                (Some { Reason = heldPayload.Reason
                        MatchDate = DateTime(2026, 11, 10)
                        MatchAmount = eur -50m
                        MatchPayee = Some "Markt"
                        MatchCategoryName = Some "Lebensmittel"
                        MatchEntryId = heldPayload.MatchEntryId |> Option.map (fun (EntryId id) -> id) })
                "die Sicht trägt Grund und Treffer"
    ]

/// Jeder schreibende Befehl mit einer wartenden Buchung: Fehler mit dem Satz aus ADR 0093 Punkt 6, kein
/// Ereignis, kein YNAB-Aufruf (die Ports scheitern sonst laut).
let private commands : (string * (EntryId -> Async<Result<unit, string>>)) list =
    let ignoreOk (work: Async<Result<'T, string>>) = async { let! r = work in return r |> Result.map ignore }
    [ "setReviewMark", fun id -> setReviewMark at (fun _ _ _ -> noYnab "ReviewMarkPush") id Reviewed |> ignoreOk
      "setEntryCategory", fun id -> setEntryCategory at (fun _ _ -> noYnab "CategoryPush") transferTools id None |> ignoreOk
      "setEntrySplits", fun id -> setEntrySplits at splitTools id [] |> ignoreOk
      "setEntryTransfer", fun id -> setEntryTransfer at transferTools id (YnabAccountId (Guid.NewGuid())) |> ignoreOk
      "setLenaFlag", fun id -> setLenaFlag at (fun _ _ -> noYnab "FlagPush") (Some "purple") id AssignLena |> ignoreOk
      "setEntryMemo", fun id -> setEntryMemo at (fun _ _ -> noYnab "MemoPush") id "neu" |> ignoreOk
      "setEntryAmount", fun id -> setEntryAmount at (fun _ _ -> noYnab "AmountPush") id -40m |> ignoreOk
      "deleteEntry", fun id -> deleteEntry at (fun _ -> noYnab "Delete") ignore id |> ignoreOk
      "retryPushToYnab", fun id -> retryPushToYnab at loadEntryOrigin (fun () -> noYnab "Spiegel") (fun _ -> noYnab "EntryPush") ignore id |> ignoreOk
      "retryReviewMarkWriteBack", fun id -> retryReviewMarkWriteBack at (fun _ _ _ -> noYnab "ReviewMarkPush") id |> ignoreOk
      "retryCategorizationWriteBack", fun id -> retryCategorizationWriteBack at (fun _ _ -> noYnab "CategoryPush") id |> ignoreOk
      "retryCategorizationOrSplitWriteBack", fun id ->
          retryCategorizationOrSplitWriteBack at (fun _ _ -> noYnab "CategoryPush") splitTools transferTools id |> ignoreOk
      "retryFlagWriteBack", fun id -> retryFlagWriteBack at (fun _ _ -> noYnab "FlagPush") id |> ignoreOk
      "retryMemoWriteBack", fun id -> retryMemoWriteBack at (fun _ _ -> noYnab "MemoPush") id |> ignoreOk
      "retryAmountWriteBack", fun id -> retryAmountWriteBack at (fun _ _ -> noYnab "AmountPush") splitTools id |> ignoreOk
      // ledger-fxt09 (Payee ändern): ADR 0093 Punkt 6 nennt den Payee ausdrücklich.
      "setEntryPayee", fun id -> setEntryPayee at (fun _ _ -> noYnab "PayeePush") id (Some "Neuer Payee") |> ignoreOk
      "retryPayeeWriteBack", fun id -> retryPayeeWriteBack at (fun _ _ -> noYnab "PayeePush") id |> ignoreOk ]

[<Tests>]
let commandTests =
    testSequenced <| testList "Wartende Buchung: jeder schreibende Befehl lehnt sie ab" [
        for name, command in commands do
            testCase $"{name} lehnt eine wartende Buchung mit dem Satz aus ADR 0093 ab" <| fun () ->
                let _, entryId = seedHeld ()
                let before = eventCount entryId
                Expect.equal (command entryId |> run) (Error possibleDuplicateLockedText) "der Satz"
                Expect.equal (eventCount entryId) before "kein Ereignis"
    ]

[<Tests>]
let batchTests =
    testSequenced <| testList "Wartende Buchung: Sammelbefehle überspringen sie mit eigenem Grund" [

        testCase "setReviewMarkBatch zählt sie als vielleicht doppelt" <| fun () ->
            let _, entryId = seedHeld ()
            let (EntryId id) = entryId
            match setReviewMarkBatch at (fun _ -> noYnab "ReviewMarkBatchPush") (BatchPicked [ id ]) |> run with
            | Ok outcome ->
                Expect.equal (outcome.HeldCount, outcome.FailedCount, outcome.Changed) (1, 0, []) "eigener Grund"
            | Error e -> failtest e
            Expect.equal (eventCount entryId) 3 "kein Ereignis"

        testCase "setEntryCategoryBatch überspringt sie als vielleicht doppelt" <| fun () ->
            let _, entryId = seedHeld ()
            let (EntryId id) = entryId
            match setEntryCategoryBatch at (fun _ -> noYnab "CategoryBatchPush") (BatchPicked [ id ]) None |> run with
            | Ok outcome -> Expect.equal (outcome.Skipped, outcome.Changed) ([ SkipPossibleDuplicate ], []) "eigener Grund"
            | Error e -> failtest e
            Expect.equal (eventCount entryId) 3 "kein Ereignis"

        testCase "setLenaFlagBatch zählt sie als vielleicht doppelt" <| fun () ->
            let _, entryId = seedHeld ()
            let (EntryId id) = entryId
            match setLenaFlagBatch at (fun _ -> noYnab "FlagBatchPush") (Some "purple") (BatchPicked [ id ]) AssignLena |> run with
            | Ok outcome -> Expect.equal (outcome.HeldCount, outcome.FailedCount, outcome.Changed) (1, 0, []) "eigener Grund"
            | Error e -> failtest e
            Expect.equal (eventCount entryId) 3 "kein Ereignis"
    ]
