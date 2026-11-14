module LedgerDuplicateDecisionTests

// Eine wartende Buchung (unsicheres Duplikat) übernehmen oder verwerfen (ledger-r7h2w, ADR 0093 Punkt 4 bis 7).
//
//  * reine Domäne: Codec der drei Ereignisse, `apply` samt Invarianten, die Entscheidung eines erneuten Abrufs;
//  * die Befehle `confirmEntryIsNew`, `discardDuplicateEntry` und `restoreDiscardedDuplicate` gegen In-Memory-SQLite,
//    Ports als Funktionswerte, die laut scheitern, wenn ein Befehl YNAB nicht rufen darf;
//  * der Push nach „Ist neu“ über das echte `Server.Api.entryPushWith` (die `import_id` aus dem Kassenbuch);
//  * Replay mit allen drei Typen, Schreib-Sperre.

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
let private at = DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc)

let private ev (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

let private heldPayload : EntryHeldAsPossibleDuplicatePayload =
    { Reason = "In YNAB steht eine Buchung mit gleichem Betrag am 11.11.2026 bei Markt."
      MatchYnabTransactionId = "ynab-treffer-r7h2w"
      MatchEntryId = Some (newEntryId ())
      Match = { Date = DateTime(2026, 11, 11); Amount = eur -50m; Payee = Some "Markt"; CategoryName = Some "Lebensmittel" } }

let private imported (account: LedgerAccountId) (reference: string) : LedgerEventBody =
    EntryImportedFromComdirect {
        AccountId = account
        ImportId = $"BB:{reference}"
        ComdirectReference = reference
        Date = DateTime(2026, 11, 12)
        Amount = eur -50m
        Payee = Some "Markt"
        Memo = None
        Cleared = Uncleared
        Approved = false
        FlagColor = None
        FlagName = None
    }

/// Ein eigenes, mit YNAB verbundenes Konto je Fall.
let private freshAccount () : LedgerAccountId * YnabAccountId =
    initializeLedgerSchema ()
    let account = LedgerAccountId (Guid.NewGuid())
    let ynab = YnabAccountId (Guid.NewGuid())
    saveAccount { Id = account; Name = "Girokonto"; Kind = BankAccount; Link = YnabMirrored ynab; Closed = false } |> run
    account, ynab

/// Legt eine importierte Buchung an, `more` hängt weitere Ereignisse an. Liefert Konto, Reference und Id.
let private seed (more: EntryId -> LedgerEventBody list) : LedgerAccountId * string * EntryId =
    let account, _ = freshAccount ()
    let reference = $"r7h2w-{Guid.NewGuid():N}"
    let entryId = newEntryId ()
    let events = ev entryId ComdirectImport (imported account reference) :: (more entryId |> List.map (ev entryId ComdirectImport))
    appendAndApply events |> run
    account, reference, entryId

let private seedHeldWith (payload: EntryHeldAsPossibleDuplicatePayload) =
    seed (fun _ -> [ EntryHeldAsPossibleDuplicate payload ])

let private seedHeld () = seedHeldWith heldPayload

let private typesOf (entryId: EntryId) = loadEventsForEntry entryId |> run |> List.map (fun r -> r.EventType)

let private entryOf (entryId: EntryId) = loadEntry entryId |> run |> Option.get

/// Ein Port, der nie gerufen werden darf.
let private noYnab (name: string) : 'a = failtest $"{name}: kein YNAB-Aufruf erwartet"

let private noPush : EntryPush = fun _ -> noYnab "EntryPush"

let private balanceOf (account: LedgerAccountId) : Money option =
    let (LedgerAccountId accountGuid) = account
    loadAmountsForBalances () |> run
    |> List.filter (fun (id, _, _) -> id = accountGuid)
    |> List.map (fun (id, amount, deleted) -> { AccountId = id; Amount = amount; DeletedInYnab = deleted })
    |> ledgerBalancesByAccount "EUR"
    |> Map.tryFind accountGuid

let private isNotInYnab (EntryId id) =
    Ledger.QueryHandlers.getEntriesNotInYnab () |> run |> List.exists (fun e -> e.EntryId = id)

let private pageIdsOf (account: LedgerAccountId) (includeDeleted: bool) : Guid list =
    let (LedgerAccountId id) = account
    let query : LedgerEntryPageQuery =
        { AccountId = Some id
          Page = 0
          PageSize = 100
          LocallyRemoved = 0
          IncludeDeleted = includeDeleted
          OnlyNeedsReview = false
          OnlyAskLena = false
          WithoutCategory = false
          CategoryIds = []
          DateFrom = None
          DateTo = None
          Search = None
          AmountMin = None
          AmountMax = None
          AmountDirection = AllDirections
          Sort = LedgerSort.defaultSort }
    (Ledger.QueryHandlers.getLedgerEntriesPage query |> run).Entries |> List.map (fun e -> e.Id)

let private discardedPayload : EntryDiscardedAsDuplicateInBbPayload =
    { MatchedYnabTransactionId = Some heldPayload.MatchYnabTransactionId; MatchedEntryId = heldPayload.MatchEntryId }

// ============================================
// Domäne
// ============================================

[<Tests>]
let domainTests =
    testList "Wartende Buchung entscheiden: Domäne" [

        testCase "die drei Ereignisse tragen Typname und Version 1 und überstehen die Codec-Rundreise" <| fun () ->
            let bodies =
                [ EntryConfirmedAsNewInBb
                  EntryDiscardedAsDuplicateInBb discardedPayload
                  EntryDiscardedAsDuplicateInBb { MatchedYnabTransactionId = None; MatchedEntryId = None }
                  EntryDiscardRevokedInBb ]
            Expect.equal
                (bodies |> List.map eventTypeName |> List.distinct)
                [ "EntryConfirmedAsNewInBb"; "EntryDiscardedAsDuplicateInBb"; "EntryDiscardRevokedInBb" ]
                "Typnamen"
            for body in bodies do
                Expect.equal (schemaVersion body) 1 "Version"
                Expect.equal (decodePayload (eventTypeName body) 1 (encodePayload body)) (Ok body) $"Rundreise {eventTypeName body}"

        testCase "apply: Ist neu hebt die Wartemarke auf, Verwerfen löscht sie, Rückgängig lässt sie wieder warten" <| fun () ->
            let account = LedgerAccountId (Guid.NewGuid())
            let entryId = newEntryId ()
            let held =
                replay [ ev entryId ComdirectImport (imported account "dom")
                         ev entryId ComdirectImport (EntryHeldAsPossibleDuplicate heldPayload) ]
                |> Option.get
            let confirmed = apply (Some held) (ev entryId UserInBb EntryConfirmedAsNewInBb)
            Expect.isNone confirmed.PossibleDuplicate "wartet nicht mehr"
            Expect.isFalse confirmed.DeletedInYnab "zählt"
            let discarded = apply (Some held) (ev entryId UserInBb (EntryDiscardedAsDuplicateInBb discardedPayload))
            Expect.isTrue discarded.DeletedInYnab "zählt nirgends"
            Expect.isTrue (isDiscardedAsDuplicate discarded) "verworfen"
            let revoked = apply (Some discarded) (ev entryId UserInBb EntryDiscardRevokedInBb)
            Expect.equal revoked held "wartet wieder wie vorher"

        testCase "apply: Ist neu und Verwerfen nur bei wartender, Rückgängig nur bei verworfener Buchung" <| fun () ->
            let account = LedgerAccountId (Guid.NewGuid())
            let entryId = newEntryId ()
            let plain = replay [ ev entryId ComdirectImport (imported account "inv") ] |> Option.get
            let held = apply (Some plain) (ev entryId ComdirectImport (EntryHeldAsPossibleDuplicate heldPayload))
            let discarded = apply (Some held) (ev entryId UserInBb (EntryDiscardedAsDuplicateInBb discardedPayload))
            let linked = apply (Some plain) (ev entryId ComdirectImport (EntryLinkedToYnab { YnabTransactionId = "ynab-x" }))
            let throws entry body label =
                Expect.throws (fun () -> apply (Some entry) (ev entryId UserInBb body) |> ignore) label
            throws plain EntryConfirmedAsNewInBb "Ist neu ohne Warten"
            throws discarded EntryConfirmedAsNewInBb "Ist neu nach dem Verwerfen"
            throws linked EntryConfirmedAsNewInBb "Ist neu einer verknüpften"
            throws plain (EntryDiscardedAsDuplicateInBb discardedPayload) "Verwerfen ohne Warten"
            throws discarded (EntryDiscardedAsDuplicateInBb discardedPayload) "zweites Verwerfen"
            throws held EntryDiscardRevokedInBb "Rückgängig ohne Verwerfen"
            throws plain EntryDiscardRevokedInBb "Rückgängig einer gewöhnlichen Buchung"
            Expect.throws
                (fun () -> apply None (ev entryId UserInBb EntryConfirmedAsNewInBb) |> ignore)
                "unbekannte Buchung"

        testCase "erneuter Abruf: verworfen und wieder wartend übersprungen, übernommen und verknüpft schon importiert" <| fun () ->
            let account = LedgerAccountId (Guid.NewGuid())
            let entryId = newEntryId ()
            let held =
                replay [ ev entryId ComdirectImport (imported account "abruf")
                         ev entryId ComdirectImport (EntryHeldAsPossibleDuplicate heldPayload) ]
                |> Option.get
            let discarded = apply (Some held) (ev entryId UserInBb (EntryDiscardedAsDuplicateInBb discardedPayload))
            let revoked = apply (Some discarded) (ev entryId UserInBb EntryDiscardRevokedInBb)
            let confirmed = apply (Some held) (ev entryId UserInBb EntryConfirmedAsNewInBb)
            let linked = apply (Some confirmed) (ev entryId UserInBb (EntryLinkedToYnab { YnabTransactionId = "ynab-neu" }))
            Expect.equal (knownImportDecision discarded) SkipKnownImport "verworfen"
            Expect.equal (knownImportDecision revoked) SkipKnownImport "wieder wartend"
            Expect.equal (knownImportDecision linked) AlreadyLinkedImport "übernommen und verknüpft"

        testCase "das Payload des Verwerfens: eine leere YNAB-Id des Treffers wird None" <| fun () ->
            Expect.equal (discardedAsDuplicatePayloadOf heldPayload) discardedPayload "mit Treffer"
            Expect.equal
                (discardedAsDuplicatePayloadOf { heldPayload with MatchYnabTransactionId = ""; MatchEntryId = None })
                { MatchedYnabTransactionId = None; MatchedEntryId = None }
                "Treffer nicht wiedergefunden"
    ]

// ============================================
// Befehle
// ============================================

/// Der echte Einzel-Push (`Server.Api.entryPushWith`) mit einem Fake-POST, der die gesendete `import_id` mitschreibt.
let private recordingPush (answer: YnabResult<string>) =
    let sent = ResizeArray<string option>()
    let create importId _ _ _ _ _ (_: ManualTransactionRequest) =
        async {
            sent.Add importId
            return answer
        }
    let settings = Map [ "ynab_token", "tok"; "ynab_default_budget_id", "bud" ]
    let push = Server.Api.entryPushWith (fun k -> async.Return (Map.tryFind k settings)) create |> run
    sent, push

[<Tests>]
let commandTests =
    testSequenced <| testList "Wartende Buchung entscheiden: Befehle" [

        testCase "Ist neu mit gelungenem Push: Entscheidung, genau ein POST mit BB:<Reference>, Verknüpfung" <| fun () ->
            let account, reference, entryId = seedHeld ()
            let sent, push = recordingPush (Ok "ynab-neu-1")
            let mutable triggers = 0
            let result = confirmEntryIsNew at push (fun () -> triggers <- triggers + 1) entryId |> run
            Expect.equal result (Ok PushToYnabSent) "gesendet"
            Expect.equal
                (typesOf entryId)
                [ "EntryImportedFromComdirect"; "EntryHeldAsPossibleDuplicate"; "EntryConfirmedAsNewInBb"; "EntryLinkedToYnab" ]
                "Ereignisse"
            let recorded = loadEventsForEntry entryId |> run
            Expect.equal (recorded |> List.map (fun r -> r.Event.Actor) |> List.skip 2) [ UserInBb; UserInBb ] "Actor"
            Expect.equal (List.ofSeq sent) [ Some $"BB:{reference}" ] "genau ein POST mit der import_id"
            Expect.equal triggers 1 "Spiegel danach"
            let entry = entryOf entryId
            Expect.isFalse (isHeldAsPossibleDuplicate entry) "wartet nicht mehr"
            Expect.isTrue entry.LinkedToYnab "verknüpft"
            Expect.equal (balanceOf account) (Some (eur -50m)) "zählt im Kontostand"
            Expect.equal (countNeedsReviewEntries (Some account) |> run) 1 "bleibt zu prüfen"

        testCase "Ist neu mit gescheitertem Push: die Entscheidung bleibt, der Fehlschlag steht unter Buchung nicht in YNAB" <| fun () ->
            let _, _, entryId = seedHeld ()
            let sent, push = recordingPush (Error (YnabError.NetworkError "weg"))
            let result = confirmEntryIsNew at push (fun () -> failtest "kein Spiegel nach Fehlschlag") entryId |> run
            match result with
            | Ok (PushToYnabFailed reason) -> Expect.isNonEmpty reason "Grund"
            | other -> failtestf "Fehlschlag mit Grund erwartet: %A" other
            Expect.equal sent.Count 1 "ein Versuch"
            Expect.equal
                (typesOf entryId |> List.skip 2)
                [ "EntryConfirmedAsNewInBb"; "EntryPushToYnabFailed" ]
                "kein Rollback"
            Expect.isTrue (isNotInYnab entryId) "Buchung nicht in YNAB"
            Expect.isFalse (isHeldAsPossibleDuplicate (entryOf entryId)) "wartet nicht mehr"

        testCase "Ist neu mit 409: die import_id steht schon in YNAB, angekommen ohne Fehlschlag, ein Auslöser" <| fun () ->
            // ledger-xqqnv: derselbe Ausgang wie bei „Erneut senden“ und der Barbuchung.
            let _, _, entryId = seedHeld ()
            let _, push = recordingPush (Error ImportIdAlreadyInYnab)
            let mutable triggers = 0
            let result = confirmEntryIsNew at push (fun () -> triggers <- triggers + 1) entryId |> run
            Expect.equal result (Ok PushToYnabSent) "gesendet"
            Expect.equal
                (typesOf entryId |> List.skip 2)
                [ "EntryConfirmedAsNewInBb" ]
                "die Entscheidung, kein EntryPushToYnabFailed"
            Expect.equal triggers 1 "ein Auslöser"

        testCase "Ist doppelt, verwerfen: Ereignis ohne YNAB, die Buchung zählt nirgends" <| fun () ->
            let account, _, entryId = seedHeld ()
            let (EntryId entryGuid) = entryId
            Expect.equal (countNeedsReviewEntries (Some account) |> run) 1 "vorher zu prüfen"
            Expect.equal (discardDuplicateEntry at entryId |> run) (Ok ()) "verworfen"
            let recorded = loadEventsForEntry entryId |> run |> List.last
            Expect.equal recorded.Event.Body (EntryDiscardedAsDuplicateInBb discardedPayload) "Payload mit Treffer"
            Expect.equal recorded.Event.Actor UserInBb "Actor"
            Expect.equal (countNeedsReviewEntries (Some account) |> run) 0 "nicht zu prüfen"
            Expect.equal (balanceOf account |> Option.defaultValue (eur 0m)) (eur 0m) "nicht im Kontostand"
            Expect.isFalse (isNotInYnab entryId) "nicht unter Buchung nicht in YNAB"
            Expect.isFalse (pageIdsOf account false |> List.contains entryGuid) "fehlt ohne gelöschte zeigen"
            Expect.isTrue (pageIdsOf account true |> List.contains entryGuid) "steht unter gelöschte zeigen"

        testCase "Ist doppelt, verwerfen ohne wiedergefundenen Treffer: das Payload trägt keine leere YNAB-Id" <| fun () ->
            let _, _, entryId = seedHeldWith { heldPayload with MatchYnabTransactionId = ""; MatchEntryId = None }
            Expect.equal (discardDuplicateEntry at entryId |> run) (Ok ()) "verworfen"
            Expect.equal
                (loadEventsForEntry entryId |> run |> List.last).Event.Body
                (EntryDiscardedAsDuplicateInBb { MatchedYnabTransactionId = None; MatchedEntryId = None })
                "ohne Treffer"

        testCase "Rückgängig des Verwerfens: die Buchung wartet wieder und zählt wieder in zu prüfen" <| fun () ->
            let account, _, entryId = seedHeld ()
            discardDuplicateEntry at entryId |> run |> ignore
            Expect.equal (restoreDiscardedDuplicate at entryId |> run) (Ok ()) "zurückgeholt"
            Expect.equal (List.last (typesOf entryId)) "EntryDiscardRevokedInBb" "Ereignis"
            let entry = entryOf entryId
            Expect.isTrue (isAwaitingDuplicateDecision entry) "wartet wieder"
            Expect.equal (countNeedsReviewEntries (Some account) |> run) 1 "zu prüfen"
            Expect.equal (balanceOf account) (Some (eur -50m)) "zählt wieder im Kontostand"

        testCase "die Historie nennt jede Entscheidung" <| fun () ->
            let _, _, entryId = seedHeld ()
            discardDuplicateEntry at entryId |> run |> ignore
            restoreDiscardedDuplicate at entryId |> run |> ignore
            let _, push = recordingPush (Ok "ynab-historie")
            confirmEntryIsNew at push ignore entryId |> run |> ignore
            let (EntryId id) = entryId
            let lines = Ledger.QueryHandlers.getEntryHistory id |> run |> List.map (fun l -> l.Description)
            Expect.contains lines $"Vielleicht doppelt, zurückgehalten: {heldPayload.Reason}" "zurückgehalten"
            for line in [ "Als doppelt verworfen"; "Verwerfen zurückgenommen"; "Als neu übernommen" ] do
                Expect.contains lines line line

        testCase "Replay mit allen drei Typen ergibt denselben Zustand wie die Zustandstabelle" <| fun () ->
            let _, _, entryId = seedHeld ()
            discardDuplicateEntry at entryId |> run |> ignore
            restoreDiscardedDuplicate at entryId |> run |> ignore
            let _, push = recordingPush (Error (YnabError.NetworkError "weg"))
            confirmEntryIsNew at push ignore entryId |> run |> ignore
            let fromLog = loadEventsForEntry entryId |> run |> List.map (fun r -> r.Event) |> replay
            Expect.equal fromLog (loadEntry entryId |> run) "gleich"
            Expect.containsAll
                (typesOf entryId)
                [ "EntryConfirmedAsNewInBb"; "EntryDiscardedAsDuplicateInBb"; "EntryDiscardRevokedInBb" ]
                "alle drei im Log"
    ]

/// Je Regel ein Fall: Befehl, Buchung, erwarteter Satz. Kein Ereignis, kein YNAB-Aufruf.
[<Tests>]
let refusalTests =
    let confirm id = confirmEntryIsNew at noPush (fun () -> failtest "kein Spiegel") id |> run |> Result.map ignore
    let discard id = discardDuplicateEntry at id |> run
    let restore id = restoreDiscardedDuplicate at id |> run
    let plain () = let _, _, id = seed (fun _ -> []) in id
    let linked () = let _, _, id = seed (fun _ -> [ EntryLinkedToYnab { YnabTransactionId = $"ynab-{Guid.NewGuid():N}" } ]) in id
    let discarded () =
        let _, _, id = seedHeld ()
        discardDuplicateEntry at id |> run |> ignore
        id
    let held () = let _, _, id = seedHeld () in id
    let cases =
        [ "Ist neu: nicht wartend", confirm, plain, duplicateNotAwaitingText
          "Ist neu: verknüpft", confirm, linked, duplicateAlreadyInYnabText
          "Ist neu: schon verworfen", confirm, discarded, duplicateAlreadyDiscardedText
          "Verwerfen: nicht wartend", discard, plain, duplicateNotAwaitingText
          "Verwerfen: verknüpft", discard, linked, duplicateAlreadyInYnabText
          "Verwerfen: schon verworfen", discard, discarded, duplicateAlreadyDiscardedText
          "Rückgängig: nicht verworfen", restore, held, duplicateNotDiscardedText ]
    testSequenced <| testList "Wartende Buchung entscheiden: Ablehnungen" [
        for name, command, arrange, text in cases do
            testCase name <| fun () ->
                let entryId = arrange ()
                let before = typesOf entryId
                Expect.equal (command entryId) (Error text) "der Satz"
                Expect.equal (typesOf entryId) before "kein Ereignis"
        testCase "unbekannte Buchung: alle drei Befehle lehnen ab" <| fun () ->
            initializeLedgerSchema ()
            for command in [ confirm; discard; restore ] do
                Expect.equal (command (newEntryId ())) (Error "Buchung nicht gefunden") "unbekannt"
    ]

[<Tests>]
let writeGuardTests =
    testSequenced <| testList "Wartende Buchung entscheiden: Schreib-Sperre" [
        testCase "Ist neu während eines Spiegel-Laufs schreibt und pusht erst nach dem Lauf, der Spiegel danach" <| fun () ->
            global.Persistence.initializeDatabase ()
            let _, _, entryId = seedHeld ()
            let before = typesOf entryId
            let sent, push = recordingPush (Ok "ynab-sperre")
            let mutable triggers = 0
            let result =
                LedgerWriteGuardProbe.commandWaitsForMirror
                    (fun () ->
                        Expect.equal (typesOf entryId) before "noch kein Ereignis während des Laufs"
                        Expect.equal sent.Count 0 "noch kein POST"
                        Expect.equal triggers 0 "noch kein Spiegel")
                    (confirmEntryIsNew at push (fun () -> triggers <- triggers + 1) entryId)
            Expect.equal result (Ok PushToYnabSent) "gesendet"
            Expect.equal triggers 1 "Spiegel nach der Freigabe"

        testCase "der Spiegel-Auslöser nach Ist neu läuft erst nach Freigabe der Sperre" <| fun () ->
            // ledger-vfr4h: `triggers = 0` während des Laufs gilt auch bei einem Auslöser im gesperrten
            // Abschnitt, weil der Befehl dort noch auf die Sperre wartet. Ein synchroner Auslöser, der
            // einen gesperrten Lauf abwartet, deckt die Stelle auf.
            global.Persistence.initializeDatabase ()
            let _, _, entryId = seedHeld ()
            let _, push = recordingPush (Ok "ynab-sperre-ausloeser")
            let trigger = LedgerWriteGuardProbe.SyncMirrorTrigger(2000)
            LedgerWriteGuardProbe.serialized (fun () ->
                let run = Async.StartAsTask(confirmEntryIsNew at push trigger.Trigger entryId)
                Expect.isTrue (run.Wait 10000) "Befehl beendet"
                Expect.equal run.Result (Ok PushToYnabSent) "gesendet")
            Expect.equal trigger.FinishedInTime (Some true) "der Lauf des Auslösers endet in der Frist, die Sperre ist schon frei"

        testCase "Verwerfen und Rückgängig warten auf einen Spiegel-Lauf" <| fun () ->
            global.Persistence.initializeDatabase ()
            let _, _, entryId = seedHeld ()
            let before = typesOf entryId
            let waits command =
                LedgerWriteGuardProbe.commandWaitsForMirror
                    (fun () -> Expect.equal (typesOf entryId) before "noch kein Ereignis während des Laufs")
                    command
            Expect.equal (waits (discardDuplicateEntry at entryId)) (Ok ()) "verworfen"
            let afterDiscard = typesOf entryId
            Expect.equal
                (LedgerWriteGuardProbe.commandWaitsForMirror
                    (fun () -> Expect.equal (typesOf entryId) afterDiscard "noch kein Ereignis während des Laufs")
                    (restoreDiscardedDuplicate at entryId))
                (Ok ())
                "zurückgeholt"
    ]
