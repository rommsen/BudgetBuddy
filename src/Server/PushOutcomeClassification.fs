/// Die Outcome-Klassifikation eines Comdirect-Import-Push-Versuchs (normaler Import-Push
/// wie Force-Import): welche Buchung gilt als importiert, als Duplikat oder als
/// gescheitert, und ob die Session danach abgeschlossen werden darf. Vorher lag dieser
/// Block zweimal (fast) identisch inline im `IEntityApi`-Record in `Server.Api`, unter
/// `dotnet test` nicht erreichbar (ledger-na3rq). Pure, kein HTTP, kein
/// `SyncSessionManager`.
module Server.PushOutcomeClassification

open Shared.Domain
open Server.YnabClient

/// Ergebnis der Klassifikation. `SessionMayComplete` ist ein Feld dieses Records, keine
/// an der Aufrufstelle wiederholte Bedingung: eine Buchung mit `LedgerWriteFailed` darf
/// die Session an keiner der beiden Aufrufstellen (`importToYnab`, `forceImportDuplicates`)
/// abschließen lassen (ADR 0021). Bug-Fix: `forceImportDuplicates` prüfte bisher nur, ob
/// keine kategorisierte Buchung mehr offen ist, ohne `LedgerWriteFailed`-Outcomes dieses
/// Versuchs zu berücksichtigen.
type PushOutcomeClassification = {
    UpdatedTransactions: SyncTransaction list
    DuplicateTransactionIds: TransactionId list
    LedgerWriteFailureSummary: (int * string) option
    SessionMayComplete: bool
}

/// Wahr, wenn eine Buchung noch offen ist (kategorisiert, aber weder importiert noch
/// übersprungen noch neu/unbearbeitet). Dieselbe Fallunterscheidung, die
/// `forceImportDuplicates` vor der Extraktion (ledger-na3rq) als `allCategorizedImported`
/// über die ganze Session laufen ließ: `Pending` blockiert bewusst nicht (frisch geholt,
/// noch nie in Bearbeitung).
let private isOpenCategorized (tx: SyncTransaction) : bool =
    match tx.Status with
    | AutoCategorized | ManualCategorized | NeedsAttention -> true
    | Imported | Skipped | Pending -> false

/// Klassifiziert das Ergebnis eines Comdirect-Import-Push-Versuchs. Dieselbe Funktion für
/// den normalen Import-Push und für Force-Import (`Server.Api`, jeweils mit ihren eigenen
/// Eingaben, ledger-na3rq).
///
/// Je Buchung aus `transactionsInBatch`:
/// - zukunftsdatiert (`prepared.FutureDatedTransactionIds`): nie gesendet, bleibt
///   unverändert.
/// - abgelehnter Transfer (`prepared.RejectedTransferTransactionIds`): nie gesendet, wird
///   als `RejectedByYnab (UnknownRejection ...)` markiert.
/// - `Linked`/`AlreadyImported`: gilt als importiert (`Status = Imported`,
///   `YnabImportStatus = YnabImported`).
/// - `WrittenNotLinked` mit einer von YNAB gemeldeten Duplikat-ImportId: Duplikat,
///   `YnabImportStatus = RejectedByYnab (DuplicateImportId ...)`.
/// - `WrittenNotLinked` ohne gemeldete Duplikat-ImportId (nicht auswertbare Antwort) oder
///   `LedgerWriteFailed`: kein pauschaler Erfolg, die Buchung bleibt unverändert stehen.
///
/// `SessionMayComplete` ist wahr, wenn dieser Versuch weder ein Duplikat noch ein
/// gescheitertes Kassenbuch-Schreiben hinterlassen hat, UND (nur relevant, wenn
/// `forceImport` wahr ist) weder eine der Buchungen dieses Batches noch eine Buchung aus
/// `otherSessionTransactions` offen und kategorisiert geblieben ist.
///
/// `forceImport` und `otherSessionTransactions` bilden absichtlich zwei unterschiedliche
/// Abschluss-Regeln pro Aufrufstelle nach, weil die Batches selbst unterschiedlich
/// bedeuten: `importToYnab`s Batch ist immer schon "alles gerade Offene" der Session, eine
/// darin verbliebene zukunftsdatierte oder nicht auswertbare Buchung ist erwartetes
/// Verhalten (wird beim nächsten Lauf erneut versucht) und blockiert bewusst nicht
/// (bestehendes Verhalten, siehe Tests). `forceImportDuplicates`s Batch ist dagegen eine
/// von Roman explizit ausgewählte Teilmenge; bleibt eine davon nach dem gezielten Versuch
/// trotzdem offen (zukunftsdatiert, nicht auswertbar, Duplikat, gescheitertes Schreiben)
/// oder bleibt anderswo in der Session noch etwas Kategorisiertes offen, darf die Session
/// nicht abschließen (die ganze Session, nicht nur der Force-Batch, ADR 0021; genau die
/// Regel, die `allCategorizedImported` vor der Extraktion über die ganze Session prüfte).
let classifyPushOutcomes
    (transactionsInBatch: SyncTransaction list)
    (prepared: PrepareResult)
    (pushResult: Ledger.CommandHandlers.ComdirectPushResult)
    (forceImport: bool)
    (otherSessionTransactions: SyncTransaction list)
    : PushOutcomeClassification =
    let importIdByTxId =
        prepared.ToSend |> List.map (fun p -> p.TransactionId, p.ImportId) |> Map.ofList
    let duplicateImportIdSet =
        match pushResult.SendResult with
        | Some (Ok sent) -> sent.DuplicateImportIds |> Set.ofList
        | _ -> Set.empty
    let futureDatedSet = prepared.FutureDatedTransactionIds |> Set.ofList
    let rejectedTransferTxIdSet = prepared.RejectedTransferTransactionIds |> Set.ofList

    // Duplikate: nur was YNAB tatsächlich als Duplikat meldete, über die von uns selbst
    // gesetzte ImportId zugeordnet (keine "BB:{txId}"-Rekonstruktion nötig).
    let duplicateTransactionIds =
        pushResult.Outcomes
        |> Map.toList
        |> List.choose (fun (txId, outcome) ->
            match outcome with
            | Ledger.CommandHandlers.WrittenNotLinked _ ->
                match Map.tryFind txId importIdByTxId with
                | Some importId when duplicateImportIdSet.Contains importId -> Some txId
                | _ -> None
            | _ -> None)

    // Ein gescheitertes Kassenbuch-Schreiben darf nie still bleiben: die Buchung wurde
    // nicht gesendet, zählt also nicht als importiert, und die Session darf nicht als
    // abgeschlossen gelten, solange so eine Buchung offen ist.
    let ledgerWriteFailureSummary =
        Ledger.CommandHandlers.summarizeLedgerWriteFailures pushResult

    let updatedTransactions =
        transactionsInBatch
        |> List.map (fun tx ->
            let txId = tx.Transaction.Id
            if futureDatedSet.Contains txId then
                // Zukunftsdatierte Buchungen wurden nie gesendet, bleiben also
                // unverändert und nie "Imported".
                tx
            elif rejectedTransferTxIdSet.Contains txId then
                // Eine Transfer-Split-Zeile ließ sich zu keiner YNAB-Transfer-Payee
                // auflösen (die Buchung wurde nicht gesendet).
                { tx with
                    YnabImportStatus =
                        RejectedByYnab (UnknownRejection (Some "No YNAB transfer payee for the split transfer account")) }
            else
                match Map.tryFind txId pushResult.Outcomes with
                | Some (Ledger.CommandHandlers.Linked _) ->
                    { tx with Status = Imported; YnabImportStatus = YnabImported }
                | Some (Ledger.CommandHandlers.WrittenNotLinked _) ->
                    match Map.tryFind txId importIdByTxId with
                    | Some importId when duplicateImportIdSet.Contains importId ->
                        { tx with YnabImportStatus = RejectedByYnab (DuplicateImportId importId) }
                    | _ ->
                        // Nicht auswertbare Antwort: kein pauschaler Erfolg, Zustand
                        // bleibt unverändert; die Kassenbuch-Buchung steht ohne
                        // ynab-Verknüpfung, erkennbar "unbekannt".
                        tx
                | Some (Ledger.CommandHandlers.AlreadyImported _) ->
                    // Schon eine mit YNAB verknüpfte Buchung für diese ImportId (ADR
                    // 0021 Punkt 3): kein zweiter Versuch nötig, gilt als importiert.
                    { tx with Status = Imported; YnabImportStatus = YnabImported }
                | Some (Ledger.CommandHandlers.LedgerWriteFailed _) ->
                    // Das Kassenbuch-Schreiben ist gescheitert, die Buchung wurde nicht
                    // gesendet.
                    tx
                | None -> tx)

    // Nur für Force-Import relevant (siehe Doc-Kommentar): eine Buchung außerhalb dieses
    // Batches, die noch offen und kategorisiert ist, oder eine Buchung dieses Batches
    // selbst, die trotz des gezielten Versuchs offen und kategorisiert geblieben ist
    // (zukunftsdatiert, nicht auswertbar, Duplikat, gescheitertes Schreiben), darf die
    // Session nicht abschließen lassen. Das ist genau die Regel, die
    // `allCategorizedImported` vor der Extraktion über die ganze Session prüfte.
    let sessionWideOpenTransactionBlocksForceImport =
        forceImport
        && (otherSessionTransactions |> List.exists isOpenCategorized
            || updatedTransactions |> List.exists isOpenCategorized)

    {
        UpdatedTransactions = updatedTransactions
        DuplicateTransactionIds = duplicateTransactionIds
        LedgerWriteFailureSummary = ledgerWriteFailureSummary
        SessionMayComplete =
            duplicateTransactionIds.IsEmpty
            && ledgerWriteFailureSummary.IsNone
            && not sessionWideOpenTransactionBlocksForceImport
    }
