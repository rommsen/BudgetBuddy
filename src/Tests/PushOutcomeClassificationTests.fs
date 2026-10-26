/// Testet `Server.PushOutcomeClassification.classifyPushOutcomes`, die reine
/// Outcome-Klassifikation eines Comdirect-Import-Push-Versuchs (ledger-na3rq): welche
/// Buchung als importiert, Duplikat oder gescheitert gilt, und ob die Session danach
/// abgeschlossen werden darf (`SessionMayComplete`). Vorher lag dieser Block zweimal
/// (fast) identisch inline im `IEntityApi`-Record in `Server.Api`, unter `dotnet test`
/// nicht erreichbar, ohne HTTP und ohne `SyncSessionManager`.
///
/// Zentraler Bug-Regressionstest weiter unten: `forceImportDuplicates` prüfte bisher nur,
/// ob keine kategorisierte Buchung mehr offen ist (`allCategorizedImported`), ohne
/// `LedgerWriteFailed`-Outcomes dieses Versuchs zu berücksichtigen. Die Session konnte
/// dadurch fälschlich abgeschlossen werden, obwohl eine Buchung wegen eines
/// gescheiterten Kassenbuch-Schreibens offen geblieben ist (Verstoß gegen ADR 0021).
module PushOutcomeClassificationTests

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.CommandHandlers
open Server.YnabClient
open Server.PushOutcomeClassification

module private Fixtures =
    let accountId = YnabAccountId (Guid.Parse "b2c3d4e5-f607-8901-bcde-f12345678901")
    let today = DateTime(2026, 9, 21)

    let makeTx (id: string) (status: TransactionStatus) : SyncTransaction = {
        Transaction = {
            Id = TransactionId id
            BookingDate = today
            Amount = { Amount = -9.99m; Currency = "EUR" }
            Payee = Some "Rewe"
            Memo = "Einkauf"
            Reference = $"REF-{id}"
            RawData = "{}"
        }
        Status = status
        CategoryId = None
        CategoryName = None
        MatchedRuleId = None
        PayeeOverride = None
        ExternalLinks = []
        UserNotes = None
        DuplicateStatus = NotDuplicate (emptyDetectionDetails $"REF-{id}")
        YnabImportStatus = NotAttempted
        Splits = None
        SuggestedByOrderId = None
        ReviewSteps = []
        AskLena = false
    }

    /// Baut ein echtes `PrepareResult` über `prepareTransactions`, damit ImportId und
    /// ToSend nicht von Hand nachgebaut werden müssen (Muster wie
    /// `ForceImportCandidateTests`).
    let prepare (forceNewImportId: bool) (txs: SyncTransaction list) : PrepareResult =
        prepareTransactions today accountId Map.empty forceNewImportId txs

    let importIdFor (prepared: PrepareResult) (txId: TransactionId) : string =
        prepared.ToSend |> List.find (fun p -> p.TransactionId = txId) |> fun p -> p.ImportId

[<Tests>]
let classifyPushOutcomesTests =
    testList "Server.PushOutcomeClassification.classifyPushOutcomes" [

        testCase "Linked markiert die Buchung als importiert, Session darf abschließen" <| fun () ->
            let tx = Fixtures.makeTx "tx-linked" AutoCategorized
            let prepared = Fixtures.prepare false [ tx ]
            let entryId = newEntryId ()
            let pushResult = {
                Outcomes = Map.ofList [ tx.Transaction.Id, Linked (entryId, "ynab-tx-1") ]
                SendResult = None
            }

            let result = classifyPushOutcomes [ tx ] prepared pushResult false []

            let updated = result.UpdatedTransactions |> List.exactlyOne
            Expect.equal updated.Status Imported "eine verknüpfte Buchung muss als importiert gelten"
            Expect.equal updated.YnabImportStatus YnabImported "eine verknüpfte Buchung trägt YnabImported"
            Expect.isEmpty result.DuplicateTransactionIds "keine Duplikate bei einer einzelnen verknüpften Buchung"
            Expect.isNone result.LedgerWriteFailureSummary "kein gescheitertes Schreiben bei einer verknüpften Buchung"
            Expect.isTrue result.SessionMayComplete "ohne Duplikat und ohne gescheitertes Schreiben darf die Session abschließen"

        testCase "AlreadyImported markiert die Buchung als importiert, ohne erneuten Versand" <| fun () ->
            let tx = Fixtures.makeTx "tx-already" ManualCategorized
            let prepared = Fixtures.prepare false [ tx ]
            let entryId = newEntryId ()
            let pushResult = {
                Outcomes = Map.ofList [ tx.Transaction.Id, AlreadyImported entryId ]
                SendResult = None
            }

            let result = classifyPushOutcomes [ tx ] prepared pushResult false []

            let updated = result.UpdatedTransactions |> List.exactlyOne
            Expect.equal updated.Status Imported "eine schon verknüpfte Buchung (ADR 0021 Punkt 3) muss als importiert gelten"
            Expect.isTrue result.SessionMayComplete "AlreadyImported allein blockiert den Abschluss nicht"

        testCase "WrittenNotLinked mit gemeldeter Duplikat-ImportId markiert die Buchung als Duplikat und blockiert den Abschluss" <| fun () ->
            let tx = Fixtures.makeTx "tx-dup" AutoCategorized
            let prepared = Fixtures.prepare false [ tx ]
            let entryId = newEntryId ()
            let importId = Fixtures.importIdFor prepared tx.Transaction.Id
            let pushResult = {
                Outcomes = Map.ofList [ tx.Transaction.Id, WrittenNotLinked entryId ]
                SendResult = Some (Ok { emptyCreateResult with DuplicateImportIds = [ importId ] })
            }

            let result = classifyPushOutcomes [ tx ] prepared pushResult false []

            Expect.equal result.DuplicateTransactionIds [ tx.Transaction.Id ] "die Buchung muss als Duplikat gemeldet werden"
            let updated = result.UpdatedTransactions |> List.exactlyOne
            Expect.equal updated.YnabImportStatus (RejectedByYnab (DuplicateImportId importId)) "die Buchung trägt die gemeldete Duplikat-ImportId"
            Expect.equal updated.Status AutoCategorized "der Kategorisierungs-Status einer Duplikat-Buchung bleibt unverändert"
            Expect.isFalse result.SessionMayComplete "ein Duplikat darf den Abschluss blockieren"

        testCase "WrittenNotLinked ohne gemeldete Duplikat-ImportId (nicht auswertbare Antwort) bleibt unverändert und blockiert den Abschluss jetzt auch auf dem normalen Import-Push" <| fun () ->
            // Regressionstest (ledger-fgw2q): vorher blieb SessionMayComplete auf dem
            // normalen Import-Push (`forceImport = false`) wahr, obwohl keine Buchung
            // bestätigt mit YNAB verknüpft wurde und Roman nichts von der nicht
            // auswertbaren Antwort erfuhr.
            let tx = Fixtures.makeTx "tx-unparseable" AutoCategorized
            let prepared = Fixtures.prepare false [ tx ]
            let entryId = newEntryId ()
            let pushResult = {
                Outcomes = Map.ofList [ tx.Transaction.Id, WrittenNotLinked entryId ]
                SendResult = Some (Ok { emptyCreateResult with ResponseUnparseable = true })
            }

            let result = classifyPushOutcomes [ tx ] prepared pushResult false []

            Expect.isEmpty result.DuplicateTransactionIds "keine Duplikat-Meldung ohne gemeldete Duplikat-ImportId"
            Expect.equal result.UnresolvedWrittenNotLinkedTransactionIds [ tx.Transaction.Id ] "die Buchung gilt als nicht auswertbar"
            let updated = result.UpdatedTransactions |> List.exactlyOne
            Expect.equal updated.YnabImportStatus NotAttempted "der Zustand bleibt unverändert, kein pauschaler Erfolg"
            Expect.isFalse result.SessionMayComplete "eine nicht auswertbare Antwort blockiert den Abschluss auch auf dem normalen Import-Push"

        testCase "eine nicht auswertbare Antwort blockiert den Abschluss auch auf dem Force-Pfad über das neue Feld" <| fun () ->
            // Die Buchung ist hier als Pending dem Force-Offen-Test entzogen, sodass nur
            // die neue Bedingung den Abschluss blockieren kann.
            let tx = Fixtures.makeTx "tx-unparseable-force" Pending
            let prepared = Fixtures.prepare false [ tx ]
            let pushResult = {
                Outcomes = Map.ofList [ tx.Transaction.Id, WrittenNotLinked (newEntryId ()) ]
                SendResult = Some (Ok { emptyCreateResult with ResponseUnparseable = true })
            }

            let result = classifyPushOutcomes [ tx ] prepared pushResult true []

            Expect.equal result.UnresolvedWrittenNotLinkedTransactionIds [ tx.Transaction.Id ] "die Buchung gilt als nicht auswertbar"
            Expect.isFalse result.SessionMayComplete "die neue Bedingung gilt auf beiden Pfaden"

        testCase "ein echtes Duplikat ist nicht unresolved" <| fun () ->
            let tx = Fixtures.makeTx "tx-dup2" AutoCategorized
            let prepared = Fixtures.prepare false [ tx ]
            let importId = Fixtures.importIdFor prepared tx.Transaction.Id
            let pushResult = {
                Outcomes = Map.ofList [ tx.Transaction.Id, WrittenNotLinked (newEntryId ()) ]
                SendResult = Some (Ok { emptyCreateResult with DuplicateImportIds = [ importId ] })
            }
            let result = classifyPushOutcomes [ tx ] prepared pushResult false []
            Expect.isEmpty result.UnresolvedWrittenNotLinkedTransactionIds "ein gemeldetes Duplikat ist keine nicht auswertbare Antwort"

        testCase "pushFailureError liefert für eine nicht auswertbare Antwort YnabImportFailed mit eigenem Text" <| fun () ->
            // Der Fable.Remoting-Proxy macht `importToYnab` unter dotnet test unerreichbar,
            // deshalb ist die Fehlerabbildung eine pure Funktion.
            let tx = Fixtures.makeTx "tx-unparseable-err" AutoCategorized
            let prepared = Fixtures.prepare false [ tx ]
            let pushResult = {
                Outcomes = Map.ofList [ tx.Transaction.Id, WrittenNotLinked (newEntryId ()) ]
                SendResult = Some (Ok { emptyCreateResult with ResponseUnparseable = true })
            }
            let result = classifyPushOutcomes [ tx ] prepared pushResult false []

            match pushFailureError result with
            | Some (SyncError.YnabImportFailed (n, msg)) ->
                Expect.equal n 1 "eine Buchung betroffen"
                Expect.stringContains msg "nicht auswertbar" "eigener, unterscheidbarer Text"
            | other -> failtestf "erwartet YnabImportFailed, bekam %A" other

        testCase "pushFailureError ist None, wenn nichts fehlschlug" <| fun () ->
            let tx = Fixtures.makeTx "tx-ok" AutoCategorized
            let prepared = Fixtures.prepare false [ tx ]
            let pushResult = {
                Outcomes = Map.ofList [ tx.Transaction.Id, AlreadyImported (newEntryId ()) ]
                SendResult = None
            }
            let result = classifyPushOutcomes [ tx ] prepared pushResult false []
            Expect.isNone (pushFailureError result) "kein Fehler"

        testCase "LedgerWriteFailed blockiert den Abschluss auch ohne Duplikate" <| fun () ->
            // Regressionstest (Akzeptanzkriterium): ein Ergebnis mit mindestens einem
            // LedgerWriteFailed-Outcome ergibt SessionMayComplete = false, auch wenn
            // DuplicateTransactionIds leer ist.
            let tx = Fixtures.makeTx "tx-failed" AutoCategorized
            let prepared = Fixtures.prepare false [ tx ]
            let pushResult = {
                Outcomes = Map.ofList [ tx.Transaction.Id, LedgerWriteFailed "SQLite ist gesperrt" ]
                SendResult = None
            }

            let result = classifyPushOutcomes [ tx ] prepared pushResult false []

            Expect.isEmpty result.DuplicateTransactionIds "dieser Fall hat keine Duplikate"
            Expect.isSome result.LedgerWriteFailureSummary "ein gescheitertes Kassenbuch-Schreiben muss zusammengefasst werden"
            Expect.isFalse result.SessionMayComplete "ein gescheitertes Kassenbuch-Schreiben muss den Abschluss blockieren, auch ohne Duplikate"
            let updated = result.UpdatedTransactions |> List.exactlyOne
            Expect.equal updated.Status AutoCategorized "eine gescheiterte Buchung bleibt unverändert, sie wurde nie gesendet"

        testCase "Force-Import-Bug (rot vor dem Fix): ein LedgerWriteFailed-Outcome blockiert den Abschluss, auch wenn alle anderen Buchungen dieses Versuchs schon importiert sind und die gescheiterte Buchung selbst nicht kategorisiert offen ist" <| fun () ->
            // Regressionstest für den benannten Force-Import-Bug: `forceImportDuplicates`
            // prüfte bisher nur, ob keine kategorisierte Buchung mehr offen ist
            // (`allCategorizedImported`/`isOpenCategorized` über ALLE
            // Session-Transaktionen), und ließ die Session trotz eines gescheiterten
            // Kassenbuch-Schreibens abschließen, weil dieser Check `LedgerWriteFailed` gar
            // nicht kannte.
            //
            // Verifier-Fund (Iteration 2): mit `NeedsAttention` als Status der gescheiterten
            // Buchung war dieser Test nicht rot vor dem Fix, weil `NeedsAttention` schon von
            // der ALTEN Regel (`isOpenCategorized`) als offen galt und den Abschluss allein
            // deshalb blockierte, unabhängig vom neuen `ledgerWriteFailureSummary.IsNone`.
            // `Pending` ("frisch geholt, noch nicht kategorisiert") gilt in
            // `isOpenCategorized` bewusst NICHT als offen (siehe Doc-Kommentar dort), ist
            // aber ein für `classifyPushOutcomes` erreichbarer Status: `importToYnab`
            // filtert `toImport` auf `AutoCategorized | ManualCategorized | NeedsAttention |
            // Pending` (`Server.Api.fs`, Kategorie ist optional), und
            // `forceImportDuplicates`s `transactionsToForce` filtert `allTransactions` nur
            // über die übergebenen `TransactionId`, ohne Status-Einschränkung; eine
            // `Pending`-Buchung, die beim Kassenbuch-Schreiben gescheitert ist, kann also
            // unverändert in einem Force-Batch landen. Mit `Pending` fängt NUR die neue
            // `ledgerWriteFailureSummary.IsNone`-Bedingung diesen Fall, nicht die alte
            // Regel; der Test ist damit rot vor dem Fix. Zwei Buchungen dieses simulierten
            // Force-Import-Versuchs sind bereits importiert (Linked/AlreadyImported), also
            // "sonst vollständig importiert"; die dritte (Pending) scheitert beim
            // Kassenbuch-Schreiben. SessionMayComplete muss trotzdem false sein.
            let txAlreadyImported = Fixtures.makeTx "tx-force-1" ManualCategorized
            let txLinked = Fixtures.makeTx "tx-force-2" AutoCategorized
            let txFailed = Fixtures.makeTx "tx-force-3" Pending
            let batch = [ txAlreadyImported; txLinked; txFailed ]
            let prepared = Fixtures.prepare true batch
            let entryId1 = newEntryId ()
            let entryId2 = newEntryId ()
            let pushResult = {
                Outcomes =
                    Map.ofList [
                        txAlreadyImported.Transaction.Id, AlreadyImported entryId1
                        txLinked.Transaction.Id, Linked (entryId2, "ynab-force-2")
                        txFailed.Transaction.Id, LedgerWriteFailed "Datenbank nicht erreichbar"
                    ]
                SendResult = Some (Ok emptyCreateResult)
            }

            let result = classifyPushOutcomes batch prepared pushResult true []

            let updatedById =
                result.UpdatedTransactions |> List.map (fun tx -> tx.Transaction.Id, tx) |> Map.ofList
            Expect.equal (Map.find txAlreadyImported.Transaction.Id updatedById).Status Imported
                "die erste Buchung war schon importiert"
            Expect.equal (Map.find txLinked.Transaction.Id updatedById).Status Imported
                "die zweite Buchung wurde in diesem Versuch verknüpft"
            Expect.equal (Map.find txFailed.Transaction.Id updatedById).Status Pending
                "die gescheiterte Buchung bleibt in ihrem ursprünglichen Status (Pending) stehen, sie wurde nie gesendet"
            Expect.isSome result.LedgerWriteFailureSummary
                "das gescheiterte Kassenbuch-Schreiben muss erkannt werden, obwohl die anderen beiden Buchungen fertig sind"
            Expect.isFalse result.SessionMayComplete
                "Bug-Fix: Force-Import darf die Session nicht abschließen, solange eine Buchung wegen eines gescheiterten Kassenbuch-Schreibens offen geblieben ist, auch wenn diese Buchung selbst (Pending) von der alten Regel nicht als offen galt"

        testCase "Force-Import: eine offen gebliebene Buchung außerhalb des Force-Batches blockiert den Abschluss (Verifier-Fund, Iteration 1)" <| fun () ->
            // Regressionstest für die von Iteration 1 übersehene Lücke: Roman wählt bei
            // Force-Import gezielt eine Teilmenge (`transactionIds`), nicht die ganze
            // Session. Bleibt eine Buchung AUSSERHALB dieser Teilmenge kategorisiert offen
            // (hier: ein aus einem vorherigen, gescheiterten Kassenbuch-Schreiben
            // übrig gebliebenes `NeedsAttention`), darf die Session trotz eines für sich
            // genommen vollständig erfolgreichen Force-Batches nicht abschließen.
            let forcedTx = Fixtures.makeTx "tx-forced" AutoCategorized
            let openElsewhereTx = Fixtures.makeTx "tx-open-elsewhere" NeedsAttention
            let prepared = Fixtures.prepare true [ forcedTx ]
            let entryId = newEntryId ()
            let pushResult = {
                Outcomes = Map.ofList [ forcedTx.Transaction.Id, Linked (entryId, "ynab-forced") ]
                SendResult = Some (Ok emptyCreateResult)
            }

            let result = classifyPushOutcomes [ forcedTx ] prepared pushResult true [ openElsewhereTx ]

            Expect.isEmpty result.DuplicateTransactionIds "der Force-Batch selbst hat kein Duplikat"
            Expect.isNone result.LedgerWriteFailureSummary "der Force-Batch selbst hat kein gescheitertes Schreiben"
            let updated = result.UpdatedTransactions |> List.exactlyOne
            Expect.equal updated.Status Imported "die geforcte Buchung selbst wurde erfolgreich verknüpft"
            Expect.isFalse result.SessionMayComplete
                "eine kategorisiert offen gebliebene Buchung außerhalb des Force-Batches muss den Abschluss blockieren"

        testCase "Force-Import: eine im Batch selbst offen gebliebene zukunftsdatierte Buchung blockiert den Abschluss (bestehendes Force-Verhalten wiederhergestellt)" <| fun () ->
            // Vor der Extraktion prüfte `forceImportDuplicates` `allCategorizedImported`
            // über die ganze (bereits aktualisierte) Session, also auch über den
            // Force-Batch selbst: eine zukunftsdatierte oder nicht auswertbare Buchung
            // blieb kategorisiert und blockierte den Abschluss. Anders als beim normalen
            // Import-Push (siehe Test oben) ist das auf dem Force-Pfad gewollt: Roman hat
            // genau diese Buchung gezielt zum erneuten Versuch ausgewählt.
            let baseTx = Fixtures.makeTx "tx-force-future" AutoCategorized
            let futureTx =
                { baseTx with
                    Transaction = { baseTx.Transaction with BookingDate = Fixtures.today.AddDays(5.0) } }
            let prepared = Fixtures.prepare true [ futureTx ]
            let pushResult = { Outcomes = Map.empty; SendResult = None }

            let result = classifyPushOutcomes [ futureTx ] prepared pushResult true []

            let updated = result.UpdatedTransactions |> List.exactlyOne
            Expect.equal updated.Status AutoCategorized "die zukunftsdatierte Buchung wird auch beim Force-Versuch nicht als importiert markiert"
            Expect.isFalse result.SessionMayComplete
                "eine im Force-Batch selbst offen gebliebene zukunftsdatierte Buchung muss den Abschluss blockieren (bestehendes Force-Verhalten)"

        testCase "zukunftsdatierte Buchungen bleiben unverändert und blockieren den Abschluss nicht (normaler Import-Push)" <| fun () ->
            let baseTx = Fixtures.makeTx "tx-future" AutoCategorized
            let futureTx =
                { baseTx with
                    Transaction = { baseTx.Transaction with BookingDate = Fixtures.today.AddDays(5.0) } }
            let prepared = Fixtures.prepare false [ futureTx ]
            let pushResult = { Outcomes = Map.empty; SendResult = None }

            let result = classifyPushOutcomes [ futureTx ] prepared pushResult false []

            let updated = result.UpdatedTransactions |> List.exactlyOne
            Expect.equal updated.YnabImportStatus NotAttempted "eine zukunftsdatierte Buchung wurde nie gesendet, bleibt unverändert"
            Expect.equal updated.Status AutoCategorized "eine zukunftsdatierte Buchung wird nicht als importiert markiert"
            Expect.isTrue result.SessionMayComplete "eine zukunftsdatierte Buchung blockiert den Abschluss auf dem normalen Import-Push nicht (bestehendes Verhalten)"

        testCase "eine abgelehnte Transfer-Zeile wird als solche markiert, unabhängig von den Outcomes" <| fun () ->
            // `RejectedTransferTransactionIds` kommt aus `prepareTransactions`, nicht aus
            // `pushResult.Outcomes` (die Buchung wurde nie gesendet). Hier direkt über ein
            // `PrepareResult` mit einer Transaktions-Id in `RejectedTransferTransactionIds`
            // simuliert, ohne einen echten Transfer aufzubauen.
            let tx = Fixtures.makeTx "tx-rejected-transfer" AutoCategorized
            let prepared = { Fixtures.prepare false [ tx ] with RejectedTransferTransactionIds = [ tx.Transaction.Id ] }
            let pushResult = { Outcomes = Map.empty; SendResult = None }

            let result = classifyPushOutcomes [ tx ] prepared pushResult false []

            let updated = result.UpdatedTransactions |> List.exactlyOne
            Expect.equal
                updated.YnabImportStatus
                (RejectedByYnab (UnknownRejection (Some "No YNAB transfer payee for the split transfer account")))
                "eine abgelehnte Transfer-Zeile muss als solche markiert werden"
            Expect.isTrue result.SessionMayComplete "eine abgelehnte Transfer-Zeile blockiert den Abschluss auf dem normalen Import-Push nicht (bestehendes Verhalten)"

        testCase "Force-Import markiert eine abgelehnte Transfer-Zeile und ein gemeldetes Duplikat wie der normale Push, blockiert aber den Abschluss (deliberate Verhaltensänderung, siehe Task-Outcome)" <| fun () ->
            // Vor der Extraktion ließ der Force-Pfad eine abgelehnte Transfer-Zeile und ein
            // gemeldetes Duplikat unmarkiert stehen (nur `Linked`/`AlreadyImported` wurden
            // umgesetzt). Die Extraktion markiert jetzt beide Fälle wie beim normalen
            // Import-Push (`RejectedByYnab`), eine bewusste Vereinheitlichung: Roman sieht
            // auf dem Force-Pfad denselben Grund wie auf dem normalen Pfad. Der Abschluss
            // bleibt in beiden Fällen ohnehin blockiert (Duplikat bzw. offen gebliebene
            // Kategorisierung).
            let dupTx = Fixtures.makeTx "tx-force-dup" AutoCategorized
            let transferTx = Fixtures.makeTx "tx-force-transfer" ManualCategorized
            let batch = [ dupTx; transferTx ]
            let prepared =
                { Fixtures.prepare true batch with
                    RejectedTransferTransactionIds = [ transferTx.Transaction.Id ] }
            let importId = Fixtures.importIdFor prepared dupTx.Transaction.Id
            let entryId = newEntryId ()
            let pushResult = {
                Outcomes = Map.ofList [ dupTx.Transaction.Id, WrittenNotLinked entryId ]
                SendResult = Some (Ok { emptyCreateResult with DuplicateImportIds = [ importId ] })
            }

            let result = classifyPushOutcomes batch prepared pushResult true []

            let updatedById =
                result.UpdatedTransactions |> List.map (fun tx -> tx.Transaction.Id, tx) |> Map.ofList
            Expect.equal
                (Map.find dupTx.Transaction.Id updatedById).YnabImportStatus
                (RejectedByYnab (DuplicateImportId importId))
                "ein von YNAB gemeldetes Duplikat wird auf dem Force-Pfad markiert wie auf dem normalen Pfad"
            Expect.equal
                (Map.find transferTx.Transaction.Id updatedById).YnabImportStatus
                (RejectedByYnab (UnknownRejection (Some "No YNAB transfer payee for the split transfer account")))
                "eine abgelehnte Transfer-Zeile wird auf dem Force-Pfad markiert wie auf dem normalen Pfad"
            Expect.isFalse result.SessionMayComplete
                "beide Buchungen bleiben offen (Duplikat bzw. weiterhin kategorisiert), der Abschluss bleibt blockiert"
    ]
