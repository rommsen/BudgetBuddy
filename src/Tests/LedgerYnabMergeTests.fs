module LedgerYnabMergeTests

// In YNAB zusammengelegte Buchungen (ledger-nkwqb, ADR 0015 Nachtrag 2026-11-13) und die
// Vollabruf-Generation des Spiegels (ADR 0014 Nachtrag 2026-11-13).
//
// Gruppen:
//  * Decoder: `matched_transaction_id` in der Beobachtung;
//  * reine Domäne: `mergeObservedInYnab` mit dem Grund jeder Ablehnung, `apply`, `knownImportDecision`,
//    Codec, Historien-Sätze;
//  * die Abgleiche des Laufs vor der Schleife (`planMergesObservedInYnab`, `absorbedYnabIdsOf`);
//  * der Spiegel-Lauf gegen In-Memory-SQLite mit Fake-Abrufen (Regressionstest des Vorfalls);
//  * die Folgen für Abruf, Löschen und Wiederherstellen;
//  * die Vollabruf-Generation.
//
// Alle Ids, Beträge, Payees und Referenzen sind erfunden (öffentliches Repo).

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

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private day (d: int) = DateTime(2026, 10, d, 0, 0, 0, DateTimeKind.Utc)
let private at (d: int) = DateTime(2026, 10, d, 6, 0, 0, DateTimeKind.Utc)

let private freizeit = {
    CategoryId = YnabCategoryId (Guid.Parse "0195e000-0000-7000-8000-00000000f0c1")
    CategoryName = "Freizeit"
}

let private observation (ynabId: string) (account: YnabAccountId) : YnabObservation = {
    YnabTransactionId = ynabId
    AccountId = account
    ImportId = None
    Date = day 5
    Amount = eur -250.00m
    Payee = Some "Transfer : Sparkonto"
    Memo = None
    Category = None
    Splits = []
    SplitTransfers = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    MatchedYnabTransactionId = None
    Deleted = false
    Approved = true
    FlagColor = None
    FlagName = None
}

// ============================================
// Bausteine der reinen Domäne
// ============================================

let private pureAccount = LedgerAccountId (Guid.Parse "0195e000-0000-7000-8000-00000000f001")

let private observedEntry (ynabId: string) : Entry =
    let entryId = newEntryId ()
    apply None {
        EntryId = entryId
        OccurredAt = at 5
        Actor = YnabMirror
        Body =
            EntryObservedInYnab {
                AccountId = pureAccount
                YnabTransactionId = ynabId
                ImportId = None
                Date = day 5
                Amount = eur -250.00m
                Payee = Some "Transfer : Sparkonto"
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
    }

let private importedEntry (reference: string) : Entry =
    let entryId = newEntryId ()
    apply None {
        EntryId = entryId
        OccurredAt = at 7
        Actor = ComdirectImport
        Body =
            EntryImportedFromComdirect {
                AccountId = pureAccount
                ImportId = $"BB:{reference}"
                ComdirectReference = reference
                Date = day 6
                Amount = eur -250.00m
                Payee = Some "Sparplan Beispielbank"
                Memo = None
                Cleared = Uncleared
                Approved = false
                FlagColor = None
                FlagName = None
            }
    }

let private applyBody (entry: Entry) (actor: Actor) (body: LedgerEventBody) : Entry =
    apply (Some entry) { EntryId = entry.Id; OccurredAt = at 8; Actor = actor; Body = body }

let private linksOf (entry: Entry) (pairs: (LinkSystem * string) list) : EntryLink list =
    pairs |> List.map (fun (system, value) -> { EntryId = entry.Id; System = system; Value = value })

/// Bleibende Buchung (A, in YNAB ein Übertrag), aufgenommene Buchung (B, Comdirect-Import) und
/// die Beobachtung von A mit Matched-Id B.
let private pureScenario () =
    let survivor = observedEntry "ynab-a"
    let absorbed = applyBody (importedEntry "ref-0001") ComdirectImport (EntryLinkedToYnab { YnabTransactionId = "ynab-b" })
    let candidate = {
        Entry = absorbed
        Links = linksOf absorbed [ YnabSystem, "ynab-b"; ImportIdSystem, "BB:ref-0001"; ComdirectSystem, "ref-0001" ]
    }
    let obs = {
        observation "ynab-a" (YnabAccountId Guid.Empty) with
            MatchedYnabTransactionId = Some "ynab-b"
            TransferAccountId = Some (YnabAccountId (Guid.Parse "0195e000-0000-7000-8000-00000000f0a2"))
    }
    survivor, absorbed, candidate, obs

let private merge (obs: YnabObservation) (survivor: Entry) (candidate: MergeCandidate option) =
    mergeObservedInYnab (at 8) obs survivor candidate

/// Der Grund, aus dem `mergeObservedInYnab` ablehnt, oder `None`, wenn es zusammenlegt.
let private rejectionOf (obs: YnabObservation) (survivor: Entry) (candidate: MergeCandidate option) =
    match merge obs survivor candidate with
    | Ok _ -> None
    | Error rejection -> Some rejection

let private mergedIntoPayload (survivor: Entry) : EntryMergedIntoOtherObservedInYnabPayload = {
    SurvivorEntryId = survivor.Id
    SurvivorYnabTransactionId = "ynab-a"
    AbsorbedYnabTransactionId = "ynab-b"
    SurvivorPayee = Some "Transfer : Sparkonto"
    SurvivorDate = day 5
}

let private mergedFromPayload (absorbed: Entry) : EntryMergedFromOtherObservedInYnabPayload = {
    AbsorbedEntryId = absorbed.Id
    AbsorbedYnabTransactionId = "ynab-b"
    AbsorbedComdirectReference = Some "ref-0001"
    AbsorbedPayee = Some "Sparplan Beispielbank"
    AbsorbedDate = day 6
    AbsorbedAmount = eur -250.00m
}

// ============================================
// Decoder
// ============================================

[<Tests>]
let decoderTests =
    testList "Zusammenlegung in YNAB: Decoder" [
        testCase "matched_transaction_id wird gelesen, fehlend oder null ergibt None" <| fun () ->
            let tx (id: string) (matched: string) =
                $"""{{"id":"{id}","date":"2026-10-05","amount":-250000,"cleared":"cleared","approved":false,
                     "account_id":"0195e000-0000-7000-8000-00000000f0a1"{matched}}}"""
            let json =
                $"""{{"data":{{"transactions":[{tx "mit" ",\"matched_transaction_id\":\"ynab-b\""},
                                              {tx "null" ",\"matched_transaction_id\":null"},
                                              {tx "ohne" ""}],"server_knowledge":7}}}}"""
            match Decode.fromString Decoders.observationBatchDecoder json with
            | Error e -> failtest $"Batch sollte sich lesen lassen: {e}"
            | Ok batch ->
                let matchedOf id =
                    (batch.Observations |> List.find (fun o -> o.YnabTransactionId = id)).MatchedYnabTransactionId
                Expect.equal (matchedOf "mit") (Some "ynab-b") "matched_transaction_id gelesen"
                Expect.equal (matchedOf "null") None "null ergibt None"
                Expect.equal (matchedOf "ohne") None "fehlendes Feld ergibt None"
    ]

// ============================================
// Reine Domäne
// ============================================

[<Tests>]
let domainTests =
    testList "Zusammenlegung in YNAB: Domäne" [

        testCase "Matched-Id auf eine andere Buchung: erst das Ereignis der aufgenommenen, dann das der bleibenden" <| fun () ->
            let survivor, absorbed, candidate, obs = pureScenario ()
            match merge obs survivor (Some candidate) with
            | Ok [ first; second ] ->
                Expect.equal first.EntryId absorbed.Id "erst die aufgenommene Buchung"
                Expect.equal first.Body (EntryMergedIntoOtherObservedInYnab (mergedIntoPayload survivor)) "Payload aufgenommen"
                Expect.equal second.EntryId survivor.Id "dann die bleibende Buchung"
                Expect.equal second.Body (EntryMergedFromOtherObservedInYnab (mergedFromPayload absorbed)) "Payload bleibend"
                Expect.equal first.Actor YnabMirror "Actor"
                Expect.equal second.Actor YnabMirror "Actor"
                Expect.equal first.OccurredAt second.OccurredAt "gleicher Zeitpunkt"
                Expect.isEmpty (linksFrom first @ linksFrom second) "keine Verknüpfung wandert"
            | other -> failtestf "zwei Ereignisse erwartet, war %A" other

        // ledger-nnd2f: zusammengelegt wird nur ein Comdirect-Import in einen Übertrag.
        testCase "Übertrag, aufgenommene ohne comdirect-Verknüpfung (Barbuchung mit importid), wird abgelehnt" <| fun () ->
            let survivor, absorbed, _, obs = pureScenario ()
            let cash = { Entry = absorbed; Links = linksOf absorbed [ YnabSystem, "ynab-b"; ImportIdSystem, "BB:bar-0001" ] }
            Expect.equal (rejectionOf obs survivor (Some cash)) (Some NotTransferIntoComdirectImport) "kein Merge ohne Comdirect-Import"

        testCase "Comdirect-Import, bleibende aber kein Übertrag, wird abgelehnt" <| fun () ->
            let survivor, _, candidate, obs = pureScenario ()
            Expect.equal
                (rejectionOf { obs with TransferAccountId = None } survivor (Some candidate))
                (Some NotTransferIntoComdirectImport)
                "kein Merge ohne Übertrag"

        testCase "Matched-Id unbekannt wird abgelehnt" <| fun () ->
            let survivor, _, _, obs = pureScenario ()
            Expect.equal (rejectionOf obs survivor None) (Some MatchedIdUnknown) "keine Kandidatin"

        testCase "ohne Matched-Id gibt es keinen Abgleich" <| fun () ->
            let survivor, _, candidate, obs = pureScenario ()
            Expect.equal
                (rejectionOf { obs with MatchedYnabTransactionId = None } survivor (Some candidate))
                (Some NoMatchObserved)
                "kein Abgleich"

        testCase "Matched-Id auf dieselbe Buchung wird abgelehnt" <| fun () ->
            let survivor, _, _, obs = pureScenario ()
            // Mit `comdirect`-Verknüpfung, damit die Selbst-Prüfung greift und nicht schon die Herkunft.
            let self =
                { Entry = survivor
                  Links = linksOf survivor [ YnabSystem, "ynab-a"; YnabSystem, "ynab-b"; ComdirectSystem, "ref-0009" ] }
            Expect.equal (rejectionOf obs survivor (Some self)) (Some SameEntry) "keine Zusammenlegung mit sich selbst"

        testCase "Matched-Id nur als ynabsuperseded bekannt wird abgelehnt" <| fun () ->
            let survivor, absorbed, _, obs = pureScenario ()
            // Mit `comdirect`-Verknüpfung, damit die Prüfung der aktuellen `ynab`-Verknüpfung greift.
            let onlySuperseded =
                { Entry = absorbed
                  Links = linksOf absorbed [ YnabSupersededSystem, "ynab-b"; YnabSystem, "ynab-neu"; ComdirectSystem, "ref-0001" ] }
            Expect.equal (rejectionOf obs survivor (Some onlySuperseded)) (Some MatchedIdUnknown) "nur abgelöst"
            let both =
                { Entry = absorbed
                  Links =
                    linksOf absorbed [
                        YnabSystem, "ynab-b"; YnabSupersededSystem, "ynab-b"; YnabSystem, "ynab-neu"; ComdirectSystem, "ref-0001" ] }
            Expect.equal
                (rejectionOf obs survivor (Some both))
                (Some MatchedIdUnknown)
                "alte Id nach einem Ersatz, auch als ynab gespeichert"

        testCase "schon aufgenommene Buchung wird nicht noch einmal aufgenommen" <| fun () ->
            let survivor, absorbed, candidate, obs = pureScenario ()
            let already = applyBody absorbed YnabMirror (EntryMergedIntoOtherObservedInYnab (mergedIntoPayload survivor))
            Expect.equal
                (rejectionOf obs survivor (Some { candidate with Entry = already }))
                (Some AlreadyMerged)
                "zweiter Lauf, idempotent"
            let intoOther =
                applyBody absorbed YnabMirror (EntryMergedIntoOtherObservedInYnab (mergedIntoPayload (observedEntry "ynab-c")))
            Expect.equal
                (rejectionOf obs survivor (Some { candidate with Entry = intoOther }))
                (Some AbsorbedNotEligible)
                "in eine andere aufgenommen"

        testCase "aufgenommene Buchung schon gelöscht (in YNAB, in BB, verworfen) wird abgelehnt" <| fun () ->
            let survivor, absorbed, candidate, obs = pureScenario ()
            let deletedInYnab = applyBody absorbed YnabMirror EntryDeletionObservedInYnab
            let deletedInBb =
                applyBody absorbed UserInBb (
                    EntryDeletedInBb {
                        YnabTransactionId = "ynab-b"; Date = day 6; Amount = eur -250.00m; Payee = None; Memo = None
                        Category = None; Splits = []; TransferAccountId = None; FlagColor = None; Cleared = Uncleared
                        Approved = false; ImportId = Some "BB:ref-0001" })
            let discarded = applyBody absorbed UserInBb (EntryDiscardedInBb { YnabTransactionId = "ynab-b" })
            for gone, label in [ deletedInYnab, "in YNAB"; deletedInBb, "in BB"; discarded, "verworfen" ] do
                Expect.equal (rejectionOf obs survivor (Some { candidate with Entry = gone })) (Some AbsorbedNotEligible) label

        testCase "aufgenommene Buchung mit offenem Ersatz wird abgelehnt" <| fun () ->
            let survivor, absorbed, candidate, obs = pureScenario ()
            let pending =
                applyBody absorbed UserInBb (
                    EntryReplacementSentToYnab { OldYnabTransactionId = "ynab-b"; NewImportId = "BB:ref-0001:r1" })
            Expect.isSome pending.PendingReplacement "Vorbedingung: Ersatz offen"
            Expect.equal (rejectionOf obs survivor (Some { candidate with Entry = pending })) (Some AbsorbedNotEligible) "offener Ersatz"

        testCase "bleibende Buchung schon gelöscht oder aufgenommen wird abgelehnt" <| fun () ->
            let survivor, _, candidate, obs = pureScenario ()
            let deleted = applyBody survivor YnabMirror EntryDeletionObservedInYnab
            let other = observedEntry "ynab-c"
            let mergedAway =
                applyBody survivor YnabMirror (
                    EntryMergedIntoOtherObservedInYnab { mergedIntoPayload other with AbsorbedYnabTransactionId = "ynab-a" })
            Expect.equal (rejectionOf obs deleted (Some candidate)) (Some SurvivorNotEligible) "bleibende gelöscht"
            Expect.equal (rejectionOf obs mergedAway (Some candidate)) (Some SurvivorNotEligible) "bleibende selbst aufgenommen"

        testCase "gelöschte Beobachtung legt nicht zusammen" <| fun () ->
            let survivor, _, candidate, obs = pureScenario ()
            Expect.equal (rejectionOf { obs with Deleted = true } survivor (Some candidate)) (Some NoMatchObserved) "gelöscht"

        testCase "abgeglichen (Reconciled) auf einer Seite: die Zusammenlegung gilt trotzdem" <| fun () ->
            let survivor, absorbed, candidate, obs = pureScenario ()
            let reconciled (e: Entry) =
                applyBody e YnabMirror (EntryClearedStateChangeObservedInYnab { OldCleared = e.Cleared; NewCleared = Reconciled })
            Expect.isNone (rejectionOf obs (reconciled survivor) (Some candidate)) "bleibende abgeglichen"
            Expect.isNone (rejectionOf obs survivor (Some { candidate with Entry = reconciled absorbed })) "aufgenommene abgeglichen"

        testCase "die aufgenommene Buchung gilt als gelöscht, trägt die Marke, und ihre offenen Fehlschläge enden" <| fun () ->
            let survivor, absorbed, _, _ = pureScenario ()
            let withFailures =
                [ EntryCategorizationWriteBackFailed {
                      Reason = "YNAB nicht erreichbar"; BaselineCategory = None; BaselineSplits = []
                      BaselineTransferAccountId = None; YnabDeletedTransactionId = None }
                  EntryFlagWriteBackFailed { Reason = "YNAB nicht erreichbar"; BaselineFlagColor = None; BaselineFlagName = None }
                  EntryMemoWriteBackFailed { Reason = "YNAB nicht erreichbar"; BaselineMemo = None }
                  EntryAmountWriteBackFailed { Reason = "YNAB nicht erreichbar"; BaselineAmount = eur -250.00m }
                  EntryPayeeWriteBackFailed { Reason = "YNAB nicht erreichbar"; BaselinePayee = None }
                  EntryReviewMarkWriteBackFailed { Reason = "YNAB nicht erreichbar" }
                  EntryPushToYnabFailed { Reason = "YNAB nicht erreichbar" } ]
                |> List.fold (fun e body -> applyBody e UserInBb body) absorbed
            Expect.isSome withFailures.CategorizationWriteBackFailure "Vorbedingung: Kategorie-Fehlschlag"
            Expect.isSome withFailures.PushToYnabFailure "Vorbedingung: Push-Fehlschlag"
            let merged = applyBody withFailures YnabMirror (EntryMergedIntoOtherObservedInYnab (mergedIntoPayload survivor))
            Expect.isTrue merged.DeletedInYnab "zählt nirgends"
            Expect.equal merged.MergedIntoEntryId (Some survivor.Id) "Marke"
            Expect.isTrue merged.LinkedToYnab "LinkedToYnab bleibt"
            Expect.isNone merged.CategorizationWriteBackFailure "Kategorie"
            Expect.isNone merged.FlagWriteBackFailure "Flag"
            Expect.isNone merged.MemoWriteBackFailure "Memo"
            Expect.isNone merged.AmountWriteBackFailure "Betrag"
            Expect.isNone merged.PayeeWriteBackFailure "Payee"
            Expect.isNone merged.ReviewMarkWriteBackFailure "Prüfvermerk"
            Expect.isNone merged.PushToYnabFailure "Push"

        testCase "die bleibende Buchung ändert durch die Zusammenlegung ihren Zustand nicht" <| fun () ->
            let survivor, absorbed, _, _ = pureScenario ()
            Expect.equal
                (applyBody survivor YnabMirror (EntryMergedFromOtherObservedInYnab (mergedFromPayload absorbed)))
                survivor
                "nur Historie"

        testCase "eine wartende Buchung oder eine Zusammenlegung mit sich selbst wird beim Anwenden verweigert" <| fun () ->
            let survivor, absorbed, _, _ = pureScenario ()
            let held =
                applyBody (importedEntry "ref-0002") ComdirectImport (
                    EntryHeldAsPossibleDuplicate {
                        Reason = "gleicher Betrag"; MatchYnabTransactionId = "ynab-x"; MatchEntryId = None
                        Match = { Date = day 6; Amount = eur -250.00m; Payee = None; CategoryName = None } })
            Expect.throwsT<InvalidOperationException>
                (fun () -> applyBody held YnabMirror (EntryMergedIntoOtherObservedInYnab (mergedIntoPayload survivor)) |> ignore)
                "wartende Buchung"
            Expect.throwsT<InvalidOperationException>
                (fun () -> applyBody absorbed YnabMirror (EntryMergedIntoOtherObservedInYnab (mergedIntoPayload absorbed)) |> ignore)
                "aufgenommen in sich selbst"
            Expect.throwsT<InvalidOperationException>
                (fun () -> applyBody survivor YnabMirror (EntryMergedFromOtherObservedInYnab (mergedFromPayload survivor)) |> ignore)
                "bleibend aus sich selbst"

        testCase "ein erneuter Import überspringt eine aufgenommene Buchung wie eine verworfene" <| fun () ->
            let survivor, absorbed, _, _ = pureScenario ()
            Expect.equal (knownImportDecision absorbed) AlreadyLinkedImport "Vorbedingung: verknüpft"
            let merged = applyBody absorbed YnabMirror (EntryMergedIntoOtherObservedInYnab (mergedIntoPayload survivor))
            Expect.equal (knownImportDecision merged) SkipKnownImport "zusammengelegt gilt wie verworfen"

        testCase "beide Ereignisse tragen Typname und Version 1 und überstehen das Speichern und Lesen" <| fun () ->
            let survivor, absorbed, _, _ = pureScenario ()
            let bodies = [
                EntryMergedIntoOtherObservedInYnab (mergedIntoPayload survivor)
                EntryMergedIntoOtherObservedInYnab { mergedIntoPayload survivor with SurvivorPayee = None }
                EntryMergedFromOtherObservedInYnab (mergedFromPayload absorbed)
                EntryMergedFromOtherObservedInYnab
                    { mergedFromPayload absorbed with AbsorbedComdirectReference = None; AbsorbedPayee = None }
            ]
            Expect.equal
                (bodies |> List.map eventTypeName |> List.distinct)
                [ "EntryMergedIntoOtherObservedInYnab"; "EntryMergedFromOtherObservedInYnab" ]
                "Typnamen"
            for body in bodies do
                Expect.equal (schemaVersion body) 1 "Version"
                Expect.equal (decodePayload (eventTypeName body) 1 (encodePayload body)) (Ok body) $"Rundreise {eventTypeName body}"

        testCase "Historie: die Sätze beider Seiten samt Rückfällen" <| fun () ->
            let survivor, absorbed, _, _ = pureScenario ()
            let describe = Ledger.QueryHandlers.ReadModel.describe
            let into = mergedIntoPayload survivor
            let from = mergedFromPayload absorbed
            Expect.equal
                (describe (EntryMergedIntoOtherObservedInYnab into))
                "In YNAB mit Transfer : Sparkonto vom 05.10.2026 zusammengelegt"
                "aufgenommen mit Payee"
            Expect.equal
                (describe (EntryMergedIntoOtherObservedInYnab { into with SurvivorPayee = None }))
                "In YNAB mit der Buchung vom 05.10.2026 zusammengelegt"
                "aufgenommen ohne Payee"
            Expect.equal
                (describe (EntryMergedFromOtherObservedInYnab from))
                "Import ref-0001 in YNAB hierher zusammengelegt"
                "bleibend mit Referenz"
            Expect.equal
                (describe (EntryMergedFromOtherObservedInYnab { from with AbsorbedComdirectReference = None }))
                "Buchung Sparplan Beispielbank vom 06.10.2026 über -250,00 EUR in YNAB hierher zusammengelegt"
                "bleibend ohne Referenz"
            Expect.equal
                (describe (EntryMergedFromOtherObservedInYnab { from with AbsorbedComdirectReference = None; AbsorbedPayee = None }))
                "Buchung vom 06.10.2026 über -250,00 EUR in YNAB hierher zusammengelegt"
                "bleibend ohne Referenz und ohne Payee"
    ]

// ============================================
// Abgleiche des Laufs (rein, vor der Schleife)
// ============================================

/// Die Abgleiche eines Batches aus der reinen Lage: `survivors` ordnet YNAB-Ids der bleibenden
/// Buchung zu (fehlt sie, entsteht sie im Lauf), `candidates` die Matched-Ids der Kandidatin.
let private plan
    (observations: YnabObservation list)
    (survivors: (string * Entry) list)
    (candidates: (string * MergeCandidate) list)
    =
    let survivors = Map.ofList survivors
    let candidates = Map.ofList candidates
    planMergesObservedInYnab
        observations
        (fun obs -> Map.tryFind obs.YnabTransactionId survivors |> Option.map ExistingSurvivor |> Option.defaultValue NewSurvivor)
        (fun matchedId -> Map.tryFind matchedId candidates)

let private rejectionsOf (decisions: MergeDecision list) =
    decisions |> List.map (fun d -> d.SurvivorYnabTransactionId, d.Rejection)

[<Tests>]
let planTests =
    testList "Zusammenlegung in YNAB: Abgleiche des Laufs" [

        testCase "ein zugelassener Abgleich nimmt das Ziel auf" <| fun () ->
            let survivor, _, candidate, obs = pureScenario ()
            let decisions = plan [ obs ] [ "ynab-a", survivor ] [ "ynab-b", candidate ]
            Expect.equal (rejectionsOf decisions) [ "ynab-a", None ] "zugelassen"
            Expect.equal (absorbedYnabIdsOf decisions) (Set.singleton "ynab-b") "das Ziel wird aufgenommen"

        testCase "eine bleibende Buchung, die erst im Lauf entsteht, kann zusammenlegen" <| fun () ->
            let _, _, candidate, obs = pureScenario ()
            let decisions = plan [ obs ] [] [ "ynab-b", candidate ]
            Expect.equal (rejectionsOf decisions) [ "ynab-a", None ] "zugelassen"
            Expect.equal (absorbedYnabIdsOf decisions) (Set.singleton "ynab-b") "das Ziel wird aufgenommen"

        testCase "ist die bleibende Buchung gelöscht, bleibt das Ziel im Lauf" <| fun () ->
            // Review zu ledger-nnd2f: vorher prüfte die Vorprüfung nur die aufgenommene Seite.
            let survivor, _, candidate, obs = pureScenario ()
            let deleted = applyBody survivor YnabMirror EntryDeletionObservedInYnab
            let decisions = plan [ obs ] [ "ynab-a", deleted ] [ "ynab-b", candidate ]
            Expect.equal (rejectionsOf decisions) [ "ynab-a", Some SurvivorNotEligible ] "abgelehnt"
            Expect.isEmpty (absorbedYnabIdsOf decisions) "nichts wird aufgenommen"

        testCase "löst die Beobachtung über die ImportId auf das Ziel selbst auf, bleibt das Ziel im Lauf" <| fun () ->
            let _, absorbed, candidate, obs = pureScenario ()
            let decisions = plan [ obs ] [ "ynab-a", absorbed ] [ "ynab-b", candidate ]
            Expect.equal (rejectionsOf decisions) [ "ynab-a", Some SameEntry ] "abgelehnt"
            Expect.isEmpty (absorbedYnabIdsOf decisions) "nichts wird aufgenommen"

        testCase "eine unbekannte Matched-Id nimmt nichts auf" <| fun () ->
            let survivor, _, _, obs = pureScenario ()
            let decisions = plan [ obs ] [ "ynab-a", survivor ] []
            Expect.equal (rejectionsOf decisions) [ "ynab-a", Some MatchedIdUnknown ] "abgelehnt"
            Expect.isEmpty (absorbedYnabIdsOf decisions) "nichts wird aufgenommen"

        testCase "ein Abgleich ohne Übertrag nimmt nichts auf" <| fun () ->
            let survivor, _, candidate, obs = pureScenario ()
            let decisions = plan [ { obs with TransferAccountId = None } ] [ "ynab-a", survivor ] [ "ynab-b", candidate ]
            Expect.equal (rejectionsOf decisions) [ "ynab-a", Some NotTransferIntoComdirectImport ] "abgelehnt"
            Expect.isEmpty (absorbedYnabIdsOf decisions) "nichts wird aufgenommen"

        testCase "zeigen zwei Beobachtungen gegenseitig aufeinander, nimmt keine die andere auf" <| fun () ->
            let survivor, absorbed, candidate, obs = pureScenario ()
            let back = { observation "ynab-b" (YnabAccountId Guid.Empty) with MatchedYnabTransactionId = Some "ynab-a" }
            let survivorCandidate = { Entry = survivor; Links = linksOf survivor [ YnabSystem, "ynab-a" ] }
            let decisions =
                plan [ obs; back ] [ "ynab-a", survivor; "ynab-b", absorbed ] [ "ynab-b", candidate; "ynab-a", survivorCandidate ]
            Expect.equal (rejectionsOf decisions) [ "ynab-a", Some MutualOrMany; "ynab-b", Some MutualOrMany ] "beide abgelehnt"
            Expect.isEmpty (absorbedYnabIdsOf decisions) "nichts wird aufgenommen"

        testCase "zeigen zwei Beobachtungen auf dieselbe Id, nimmt keine sie auf" <| fun () ->
            let survivor, _, candidate, obs = pureScenario ()
            let other = { obs with YnabTransactionId = "ynab-c" }
            let decisions = plan [ obs; other ] [ "ynab-a", survivor ] [ "ynab-b", candidate ]
            Expect.equal (rejectionsOf decisions) [ "ynab-a", Some MutualOrMany; "ynab-c", Some MutualOrMany ] "beide abgelehnt"
            Expect.isEmpty (absorbedYnabIdsOf decisions) "nichts wird aufgenommen"

        testCase "in einer Kette legt nur die Beobachtung zusammen, die selbst nicht Ziel ist" <| fun () ->
            // C zeigt auf A, A auf B: A ist selbst Ziel und bleibt, nur A wird in C aufgenommen.
            let survivor, _, candidate, obs = pureScenario ()
            let c = { obs with YnabTransactionId = "ynab-c"; MatchedYnabTransactionId = Some "ynab-a" }
            let survivorCandidate =
                { Entry = survivor; Links = linksOf survivor [ YnabSystem, "ynab-a"; ComdirectSystem, "ref-0002" ] }
            let decisions = plan [ obs; c ] [ "ynab-a", survivor ] [ "ynab-b", candidate; "ynab-a", survivorCandidate ]
            Expect.equal (rejectionsOf decisions) [ "ynab-a", Some MutualOrMany; "ynab-c", None ] "nur C legt zusammen"
            Expect.equal (absorbedYnabIdsOf decisions) (Set.singleton "ynab-a") "nur A wird aufgenommen"

        testCase "gelöschte Beobachtungen und solche ohne Matched-Id ergeben keinen Abgleich" <| fun () ->
            let survivor, _, candidate, obs = pureScenario ()
            let decisions =
                plan
                    [ { obs with Deleted = true }; { obs with YnabTransactionId = "ynab-d"; MatchedYnabTransactionId = None } ]
                    [ "ynab-a", survivor ]
                    [ "ynab-b", candidate ]
            Expect.isEmpty decisions "kein Abgleich"
    ]

// ============================================
// Spiegel-Lauf (In-Memory-SQLite, Fake-Abrufe)
// ============================================

module private Run =

    let accountsOf (observations: YnabObservation list) =
        observations
        |> List.map (fun o -> o.AccountId)
        |> List.distinct
        |> List.mapi (fun i accountId -> { AccountId = accountId; Name = $"Konto {i + 1}"; Closed = false; Deleted = false })

    let fetchers (knowledge: int64) (observations: YnabObservation list) : MirrorFetchers = {
        emptyMirrorFetchers with
            FetchAccounts = fun _ -> async.Return (Ok { Accounts = accountsOf observations; ServerKnowledge = knowledge })
            FetchTransactions = fun _ -> async.Return (Ok { Observations = observations; ServerKnowledge = knowledge })
    }

    let run (d: int) (budgetId: YnabBudgetId) (fetchers: MirrorFetchers) =
        match runMirror (at d) fetchers budgetId |> Async.RunSynchronously with
        | Ok summary -> summary
        | Error err -> failtest $"Lauf sollte gelingen, war: {err}"

/// Die Lage des Vorfalls, mit erfundenen Daten: Übertrag A vom Spiegel als E1 beobachtet, der
/// Comdirect-Import E2 mit `importid` und `comdirect`, verknüpft mit B.
type private Incident = {
    Budget: YnabBudgetId
    Account: LedgerAccountId
    YnabAccount: YnabAccountId
    A: YnabObservation
    YnabB: string
    Reference: string
    E1: EntryId
    E2: EntryId
}

let private incident () : Incident =
    let suffix = Guid.NewGuid().ToString("N").Substring(0, 8)
    let ynabAccount = YnabAccountId (Guid.NewGuid())
    let account = newLedgerAccountId ()
    saveAccount { Id = account; Name = "Girokonto"; Kind = BankAccount; Link = YnabMirrored ynabAccount; Closed = false }
    |> Async.RunSynchronously
    let budget = YnabBudgetId $"budget-nkwqb-{suffix}"
    let a =
        { observation $"ynab-a-{suffix}" ynabAccount with
            TransferAccountId = Some (YnabAccountId (Guid.NewGuid())) }
    Run.run 5 budget (Run.fetchers 10L [ a ]) |> ignore
    let e1 = findEntryByLink YnabSystem a.YnabTransactionId |> Async.RunSynchronously |> Option.get

    let reference = $"ref-{suffix}"
    let ynabB = $"ynab-b-{suffix}"
    let e2 = newEntryId ()
    appendAndApply [
        { EntryId = e2; OccurredAt = at 7; Actor = ComdirectImport
          Body =
            EntryImportedFromComdirect {
                AccountId = account; ImportId = $"BB:{reference}"; ComdirectReference = reference
                Date = day 6; Amount = eur -250.00m; Payee = Some "Sparplan Beispielbank"; Memo = None
                Cleared = Uncleared; Approved = false; FlagColor = None; FlagName = None } }
        { EntryId = e2; OccurredAt = at 7; Actor = ComdirectImport; Body = EntryLinkedToYnab { YnabTransactionId = ynabB } }
    ]
    |> Async.RunSynchronously
    { Budget = budget; Account = account; YnabAccount = ynabAccount; A = a; YnabB = ynabB; Reference = reference; E1 = e1; E2 = e2 }

/// Was YNAB nach dem Abgleich liefert: A mit Matched-Id B, Kategorie aus der Payee-Zuordnung,
/// ungeprüft; B mit geändertem Payee (wie im Vorfall).
let private matchedBatch (i: Incident) : YnabObservation list = [
    { i.A with MatchedYnabTransactionId = Some i.YnabB; Category = Some freizeit; Approved = false }
    { observation i.YnabB i.YnabAccount with
        ImportId = Some $"BB:{i.Reference}"; Date = day 6; Payee = Some "Sparplan Beispielbank geändert"; Cleared = Uncleared
        Approved = false }
]

/// Die Lage aus dem Probelauf von ledger-nkwqb (ledger-nnd2f), mit erfundenen Daten: eine von Hand
/// in YNAB erfasste Barbuchung A (kein Übertrag), vom Spiegel als E1 beobachtet, und sieben Tage
/// später eine Barbuchung E2 von Bob mit `importid`, ohne `comdirect`, verknüpft mit B.
let private cashIncident () : Incident =
    let suffix = Guid.NewGuid().ToString("N").Substring(0, 8)
    let ynabAccount = YnabAccountId (Guid.NewGuid())
    let account = newLedgerAccountId ()
    saveAccount { Id = account; Name = "Bargeld"; Kind = BankAccount; Link = YnabMirrored ynabAccount; Closed = false }
    |> Async.RunSynchronously
    let budget = YnabBudgetId $"budget-nnd2f-{suffix}"
    let a =
        { observation $"ynab-a-{suffix}" ynabAccount with
            Date = day 3; Amount = eur -12.50m; Payee = Some "Bäckerei Beispiel" }
    Run.run 3 budget (Run.fetchers 10L [ a ]) |> ignore
    let e1 = findEntryByLink YnabSystem a.YnabTransactionId |> Async.RunSynchronously |> Option.get

    let importId = $"BB:bar-{suffix}"
    let ynabB = $"ynab-b-{suffix}"
    let e2 = newEntryId ()
    appendAndApply [
        { EntryId = e2; OccurredAt = at 10; Actor = Bob
          Body =
            CashExpenseAdded {
                AccountId = account; Date = day 10; Amount = eur -12.50m; Payee = Some "Bäckerei Beispiel"
                Memo = None; Category = None; Cleared = Uncleared; Approved = false; ImportId = Some importId } }
        { EntryId = e2; OccurredAt = at 10; Actor = Bob; Body = EntryLinkedToYnab { YnabTransactionId = ynabB } }
    ]
    |> Async.RunSynchronously
    { Budget = budget; Account = account; YnabAccount = ynabAccount; A = a; YnabB = ynabB; Reference = importId; E1 = e1; E2 = e2 }

let private typesOf (entryId: EntryId) = loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)
let private entryOf (entryId: EntryId) = loadEntry entryId |> Async.RunSynchronously |> Option.get
let private linksOfEntry (entryId: EntryId) = linksForEntry entryId |> Async.RunSynchronously
/// Die Ereignisse der beiden Buchungen des Vorfalls. Bewusst nicht das ganze Log: parallele
/// Tests schreiben in dieselbe In-Memory-DB.
let private incidentEventCount (i: Incident) = (typesOf i.E1).Length + (typesOf i.E2).Length
let private entriesOnAccount (i: Incident) = loadEntriesForAccount i.Account |> Async.RunSynchronously |> List.length

/// Die Zahl der Buchungen, die die Kassenbuchseite für das Konto zeigt (gelöschte und aufgenommene
/// ausgeschlossen). `loadEntriesForAccount` filtert `deleted_in_ynab` nicht.
let private visibleEntriesOnAccount (i: Incident) : int =
    let (LedgerAccountId id) = i.Account
    let query : LedgerEntryPageQuery = {
        AccountId = Some id; Page = 0; PageSize = 100; LocallyRemoved = 0; IncludeDeleted = false
        OnlyNeedsReview = false; OnlyAskLena = false; WithoutCategory = false; CategoryIds = []
        DateFrom = None; DateTo = None; Search = None; AmountMin = None; AmountMax = None; AmountDirection = AllDirections
        Sort = LedgerSort.defaultSort }
    (Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously).TotalCount

let private balanceOf (account: LedgerAccountId) : decimal =
    let (LedgerAccountId id) = account
    Ledger.QueryHandlers.getLedgerAccountBalances ()
    |> Async.RunSynchronously
    |> List.find (fun b -> b.AccountId = id)
    |> fun b -> b.Balance.Amount

[<Tests>]
let mirrorRunTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testSequenced
    <| testList "Zusammenlegung in YNAB: Spiegel-Lauf" [

        testCase "von YNAB abgeglichene Buchung bleibt nicht doppelt im Kassenbuch" <| fun () ->
            // ledger-nkwqb: YNAB legte den Comdirect-Import B still mit dem selbst angelegten
            // Übertrag A zusammen. Vor dem Fix las der Spiegel `matched_transaction_id` nicht, E2
            // blieb eine eigene Zeile und zählte im Kontostand doppelt.
            let i = incident ()
            Expect.equal (balanceOf i.Account) -500.00m "Vorbedingung: vor dem Abgleich zählen beide"
            let linksBefore = linksOfEntry i.E2

            Run.run 8 i.Budget (Run.fetchers 20L (matchedBatch i)) |> ignore

            Expect.equal (List.last (typesOf i.E2)) "EntryMergedIntoOtherObservedInYnab" "E2 aufgenommen"
            Expect.contains (typesOf i.E1) "EntryMergedFromOtherObservedInYnab" "E1 bleibt"
            // Ein Anhängen ist eine Transaktion: die beiden Positionen im Log liegen direkt
            // hintereinander, auch wenn parallele Tests in dieselbe In-Memory-DB schreiben.
            let all = EventStore.loadContext "ledger" |> Async.RunSynchronously
            let find name (EntryId id) =
                all |> List.find (fun r -> r.EventType = name && r.StreamId.EndsWith (id.ToString()))
            let into = find "EntryMergedIntoOtherObservedInYnab" i.E2
            let from = find "EntryMergedFromOtherObservedInYnab" i.E1
            Expect.equal from.Position (into.Position + 1L) "beide Ereignisse in einem Anhängen, erst die aufgenommene"
            Expect.equal into.OccurredAt from.OccurredAt "gleicher Zeitpunkt"

            Expect.equal (balanceOf i.Account) -250.00m "E2 zählt nicht mehr im Kontostand"
            Expect.isFalse
                (typesOf i.E2 |> List.contains "EntryDetailsChangeObservedInYnab")
                "kein Kopfdaten-Ereignis aus der Beobachtung von B auf E2"
            Expect.equal (linksOfEntry i.E2) linksBefore "Verknüpfungen von E2 unverändert"
            Expect.equal (entryOf i.E2).MergedIntoEntryId (Some i.E1) "Marke auf E2"
            Expect.equal (entryOf i.E1).Category (Some freizeit) "YNABs Kategorie auf E1 nachgezogen"

            let before = incidentEventCount i
            let summary = Run.run 9 i.Budget (Run.fetchers 21L (matchedBatch i))
            Expect.equal (incidentEventCount i) before "ein zweiter Lauf mit demselben Batch schreibt kein Ereignis"
            Expect.equal (summary.NewEntries, summary.ChangedEntries, summary.DeletedEntries) (0, 0, 0) "nichts gezählt"

        testCase "intakt: ohne matched_transaction_id keine Zusammenlegung, E2 zählt weiter" <| fun () ->
            let i = incident ()
            let batch = matchedBatch i |> List.map (fun o -> { o with MatchedYnabTransactionId = None })

            Run.run 8 i.Budget (Run.fetchers 20L batch) |> ignore

            let types = typesOf i.E1 @ typesOf i.E2
            Expect.isFalse (types |> List.exists (fun t -> t.StartsWith "EntryMerged")) "kein Merge-Ereignis"
            Expect.isNone (entryOf i.E2).MergedIntoEntryId "nicht aufgenommen"
            Expect.equal (balanceOf i.Account) -500.00m "E2 zählt weiter"

        testCase "von YNAB verschluckte Barbuchung bleibt im Kassenbuch stehen" <| fun () ->
            // ledger-nnd2f: YNAB glich die Barbuchung E2 von Bob (mit `importid`) mit einer eine Woche
            // älteren, von Hand erfassten Barbuchung A gleichen Payees und Betrags ab. Es waren zwei
            // Zahlungen. Vor dem Fix folgte der Spiegel dem Abgleich blind und nahm E2 auf.
            let i = cashIncident ()
            Expect.equal (balanceOf i.Account) -25.00m "Vorbedingung: vor dem Abgleich zählen beide"

            Run.run 11 i.Budget (Run.fetchers 20L [ { i.A with MatchedYnabTransactionId = Some i.YnabB } ]) |> ignore

            let types = typesOf i.E1 @ typesOf i.E2
            Expect.isFalse (types |> List.exists (fun t -> t.StartsWith "EntryMerged")) "kein Merge-Ereignis"
            Expect.isNone (entryOf i.E2).MergedIntoEntryId "E2 nicht aufgenommen"
            Expect.equal (balanceOf i.Account) -25.00m "E2 zählt weiter im Kontostand"
            Expect.equal (visibleEntriesOnAccount i) 2 "beide Buchungen stehen in der Liste"

        testCase "das Ziel eines abgelehnten Abgleichs wird weiter beobachtet" <| fun () ->
            // ledger-nnd2f (Kann-Punkt 103 aus ledger-k4n2s): übersprungen wird nur eine Buchung, die
            // tatsächlich aufgenommen wird.
            let i = cashIncident ()
            let batch = [
                { i.A with MatchedYnabTransactionId = Some i.YnabB }
                { observation i.YnabB i.YnabAccount with
                    ImportId = Some i.Reference; Date = day 10; Amount = eur -12.50m
                    Payee = Some "Bäckerei Beispiel"; Memo = Some "Brötchen"; Cleared = Uncleared; Approved = false }
            ]

            Run.run 11 i.Budget (Run.fetchers 20L batch) |> ignore

            Expect.contains (typesOf i.E2) "EntryDetailsChangeObservedInYnab" "die Änderung an E2 wird beobachtet"
            Expect.equal (entryOf i.E2).Memo (Some "Brötchen") "Memo nachgezogen"
            Expect.isNone (entryOf i.E2).MergedIntoEntryId "E2 nicht aufgenommen"

        testCase "ist die bleibende Buchung in BB gelöscht, wird das Ziel weiter beobachtet" <| fun () ->
            // Review zu ledger-nnd2f: die Vorprüfung vor der Schleife sah nur die aufgenommene Seite an.
            // War die bleibende Buchung gelöscht, lehnte die Schleife den Merge ab, das Ziel war aber
            // schon übersprungen: YNABs Änderung an B kam nie auf E2 an (ADR 0015 Nachtrag, Punkt 5).
            let i = incident ()
            appendAndApply [
                { EntryId = i.E1; OccurredAt = at 7; Actor = UserInBb
                  Body =
                    EntryDeletedInBb {
                        YnabTransactionId = i.A.YnabTransactionId; Date = day 5; Amount = eur -250.00m
                        Payee = Some "Transfer : Sparkonto"; Memo = None; Category = None; Splits = []
                        TransferAccountId = i.A.TransferAccountId; FlagColor = None; Cleared = Cleared
                        Approved = true; ImportId = None } }
            ]
            |> Async.RunSynchronously

            Run.run 8 i.Budget (Run.fetchers 20L (matchedBatch i)) |> ignore

            let types = typesOf i.E1 @ typesOf i.E2
            Expect.isFalse (types |> List.exists (fun t -> t.StartsWith "EntryMerged")) "kein Merge-Ereignis"
            Expect.isNone (entryOf i.E2).MergedIntoEntryId "E2 nicht aufgenommen"
            Expect.contains (typesOf i.E2) "EntryDetailsChangeObservedInYnab" "die Änderung an B wird auf E2 beobachtet"
            Expect.equal (entryOf i.E2).Payee (Some "Sparplan Beispielbank geändert") "Payee nachgezogen"

        testCase "zeigen zwei Beobachtungen gegenseitig aufeinander, gibt es keinen Merge" <| fun () ->
            let i = incident ()
            let batch =
                matchedBatch i
                |> List.map (fun o ->
                    if o.YnabTransactionId = i.YnabB then { o with MatchedYnabTransactionId = Some i.A.YnabTransactionId } else o)

            Run.run 8 i.Budget (Run.fetchers 20L batch) |> ignore

            let types = typesOf i.E1 @ typesOf i.E2
            Expect.isFalse (types |> List.exists (fun t -> t.StartsWith "EntryMerged")) "kein Merge-Ereignis"
            Expect.isNone (entryOf i.E2).MergedIntoEntryId "nicht aufgenommen"

        testCase "zeigen zwei Beobachtungen auf dieselbe Id, gibt es keinen Merge" <| fun () ->
            let i = incident ()
            let third =
                { observation $"{i.A.YnabTransactionId}-c" i.YnabAccount with MatchedYnabTransactionId = Some i.YnabB }

            Run.run 8 i.Budget (Run.fetchers 20L (matchedBatch i @ [ third ])) |> ignore

            Expect.isFalse (typesOf i.E2 |> List.exists (fun t -> t.StartsWith "EntryMerged")) "kein Merge-Ereignis"

        testCase "ein erneuter Abruf derselben Reference bucht nichts und sendet nichts" <| fun () ->
            let i = incident ()
            Run.run 8 i.Budget (Run.fetchers 20L (matchedBatch i)) |> ignore
            let before = incidentEventCount i, entriesOnAccount i
            let mutable pushes = 0
            let push (_: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
                pushes <- pushes + 1
                async.Return (Ok emptyCreateResult)
            let request : YnabTransactionRequest = {
                AccountId = "irrelevant"; Date = "2026-10-06"; Amount = -250000; PayeeName = "Sparplan Beispielbank"
                Memo = $"Ref: {i.Reference}"; Cleared = "uncleared"; ImportId = $"BB:{i.Reference}"; CategoryId = None
                Subtransactions = None; FlagColor = None }
            let candidate : ComdirectImportCandidate = {
                Prepared = { TransactionId = TransactionId "tx-nkwqb"; ImportId = $"BB:{i.Reference}"; Request = request }
                ComdirectReference = i.Reference; Date = day 6; Amount = eur -250.00m; Payee = Some "Sparplan Beispielbank"
                RawPayee = Some "Sparplan Beispielbank"; Memo = None; ReviewSteps = []; Hold = None }

            let result = pushComdirectImports (at 9) push ignore i.Account [ candidate ] |> Async.RunSynchronously

            Expect.equal (Map.tryFind (TransactionId "tx-nkwqb") result.Outcomes) (Some (SkippedKnownImport i.E2)) "übersprungen"
            Expect.equal pushes 0 "nichts gesendet"
            Expect.equal (incidentEventCount i, entriesOnAccount i) before "nichts gebucht"

        testCase "Löschen und Wiederherstellen lehnen eine aufgenommene Buchung ohne YNAB-Aufruf ab" <| fun () ->
            let i = incident ()
            Run.run 8 i.Budget (Run.fetchers 20L (matchedBatch i)) |> ignore
            let before = incidentEventCount i
            let mutable calls = 0
            let deleteInYnab (_: string) = calls <- calls + 1; async.Return (Ok ())
            let createInYnab (_: SplitReplacement) = calls <- calls + 1; async.Return (Ok "ynab-neu")
            let mutable triggers = 0
            let trigger () = triggers <- triggers + 1

            let deleted = deleteEntry (at 9) deleteInYnab trigger i.E2 |> Async.RunSynchronously
            let restored = restoreDeletedEntry (at 9) createInYnab trigger i.E2 |> Async.RunSynchronously

            Expect.equal deleted (Error entryDeleteDeletedText) "Löschen: schon gelöscht"
            Expect.equal restored (Error "Die Buchung ist in YNAB mit einer anderen zusammengelegt.") "Wiederherstellen"
            Expect.equal calls 0 "kein YNAB-Aufruf"
            Expect.equal triggers 0 "kein Spiegel-Auslöser"
            Expect.equal (incidentEventCount i) before "kein Ereignis"
    ]

// ============================================
// Vollabruf-Generation (ADR 0014 Nachtrag 2026-11-13)
// ============================================

/// Abrufe, die jedes `server_knowledge`-Argument von `FetchTransactions` aufzeichnen. `failFirst`
/// lässt den ersten Abruf scheitern.
type private RecordingFetchers(knowledge: int64, failFirst: bool) =
    member val Calls : int64 option list = [] with get, set
    member this.Fetchers : MirrorFetchers = {
        emptyMirrorFetchers with
            FetchTransactions =
                fun arg ->
                    this.Calls <- this.Calls @ [ arg ]
                    if failFirst && this.Calls.Length = 1 then
                        async.Return (Error (YnabError.NetworkError "Netz weg"))
                    else
                        async.Return (Ok { Observations = []; ServerKnowledge = knowledge })
    }

let private uniqueBudget (name: string) = YnabBudgetId $"budget-nkwqb-gen-{name}-{Guid.NewGuid():N}"

let private markerOf (budget: YnabBudgetId) =
    global.Persistence.Settings.getSetting (fullReadGenerationKey budget) |> Async.RunSynchronously

[<Tests>]
let fullReadGenerationTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testSequenced
    <| testList "Spiegel: Vollabruf-Generation" [

        testCase "Knowledge gespeichert, Marker fehlt: der Lauf liest alles, der Folgelauf das Delta" <| fun () ->
            let budget = uniqueBudget "fehlt"
            global.Persistence.Settings.setSetting (serverKnowledgeKey budget) "50" false |> Async.RunSynchronously
            let recording = RecordingFetchers(60L, false)

            runMirror (at 8) recording.Fetchers budget |> Async.RunSynchronously |> ignore
            runMirror (at 9) recording.Fetchers budget |> Async.RunSynchronously |> ignore

            Expect.equal recording.Calls [ None; Some 60L ] "erst Vollabruf, dann Delta ab dem ersten Lauf"
            Expect.equal (markerOf budget) (Some (string currentFullReadGeneration)) "Marker steht"

        testCase "scheitert der erste Lauf, bleibt der Marker leer und der nächste liest wieder alles" <| fun () ->
            let budget = uniqueBudget "scheitert"
            global.Persistence.Settings.setSetting (serverKnowledgeKey budget) "50" false |> Async.RunSynchronously
            let recording = RecordingFetchers(60L, true)

            let first = runMirror (at 8) recording.Fetchers budget |> Async.RunSynchronously
            Expect.isError first "Vorbedingung: der erste Lauf scheitert"
            Expect.isNone (markerOf budget) "kein Marker nach einem gescheiterten Lauf"
            runMirror (at 9) recording.Fetchers budget |> Async.RunSynchronously |> ignore

            Expect.equal recording.Calls [ None; None ] "der zweite Lauf liest wieder alles"

        testCase "Erstbefüllung setzt den Marker, der Folgelauf arbeitet mit dem Delta" <| fun () ->
            let budget = uniqueBudget "erst"
            let recording = RecordingFetchers(70L, false)

            runMirror (at 8) recording.Fetchers budget |> Async.RunSynchronously |> ignore
            Expect.equal (markerOf budget) (Some (string currentFullReadGeneration)) "Marker nach der Erstbefüllung"
            runMirror (at 9) recording.Fetchers budget |> Async.RunSynchronously |> ignore

            Expect.equal recording.Calls [ None; Some 70L ] "Erstbefüllung, dann Delta"
    ]
