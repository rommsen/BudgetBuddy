/// Testet `Server.Api.buildComdirectImportCandidates`, den einzigen Ort, der
/// `ComdirectImportCandidate.ExistingEntryImportId` setzt (ledger-w6tbq).
///
/// `ledger-fq3kd` hat Force-Import so umgebaut, dass es die schon bestehende
/// Kassenbuch-Buchung über ihre alte, deterministische ImportId wiederfindet und neu
/// verknüpft, statt eine zweite "Comdirect-Import"-Buchung anzulegen. Was dabei bis
/// hierher ungetestet blieb: die API-Schicht (`src/Server/Api.fs`), die diese alte
/// ImportId selbst ableitet (`Shared.Domain.generateImportId`). Driftet diese Ableitung
/// je von der, die `Ynab.prepareTransactions` beim normalen Push tatsächlich vergibt,
/// findet Force-Import die bestehende Buchung nicht mehr und legt still eine zweite
/// an — und alle bisherigen Tests bleiben grün, weil keiner die beiden Ableitungen
/// gegeneinander hält.
///
/// Deshalb baut kein Test hier eine erwartete ImportId von Hand: beide Seiten jedes
/// Vergleichs kommen aus echten `prepareTransactions`-Aufrufen. Ein Fable.Remoting-Proxy
/// ist nicht .NET-testbar, deshalb testet dieses Modul die reine Funktion, die
/// `forceImportDuplicates` jetzt nutzt (Muster wie `pushComdirectImports` in
/// `LedgerTests.fs`).
module ForceImportCandidateTests

open System
open Expecto
open Shared.Domain
open Server.YnabClient
open Server.Api

module private Fixtures =
    let accountId = YnabAccountId (Guid.Parse "a1b2c3d4-e5f6-7890-abcd-ef1234567890")
    let today = DateTime(2026, 9, 21)

    /// Wie `PrepareFixtures.makeTx` in `YnabClientTests.fs`, mit einem eigenen Payee
    /// und einer eigenen Reference, damit ein Test den rohen Comdirect-Wert vom
    /// tatsächlich gesendeten `p.Request`-Wert unterscheiden kann.
    let makeTx (id: string) (payee: string option) (memo: string) (reference: string) : SyncTransaction = {
        Transaction = {
            Id = TransactionId id
            BookingDate = today
            Amount = { Amount = -12.34m; Currency = "EUR" }
            Payee = payee
            Memo = memo
            Reference = reference
            RawData = "{}"
        }
        Status = AutoCategorized
        CategoryId = None
        CategoryName = None
        MatchedRuleId = None
        PayeeOverride = None
        ExternalLinks = []
        UserNotes = None
        DuplicateStatus = NotDuplicate (emptyDetectionDetails reference)
        YnabImportStatus = NotAttempted
        Splits = None
        SuggestedByOrderId = None
        ReviewSteps = []
        AskLena = false
    }

[<Tests>]
let buildComdirectImportCandidatesTests =
    testList "Server.Api.buildComdirectImportCandidates" [

        testCase "Force-Import sucht über die ImportId, die der normale Push tatsächlich vergibt" <| fun () ->
            // Zentrales Kriterium: kein von Hand geschriebener erwarteter Wert. Beide
            // Seiten stammen aus echten prepareTransactions-Aufrufen, damit eine Drift
            // zwischen generateImportId und dem normalen prepareTransactions-Zweig
            // (z. B. Bindestriche, die generateImportId behält, oder ein geändertes
            // Präfix) hier auffällt statt still in Produktion zu passieren.
            //
            // Drei Transaktionen mit verschiedenen TransactionId, nicht eine: mit nur
            // einer Transaktion beweist der Test die Zuordnung über
            // `Map.find p.TransactionId byTransactionId` (Api.fs) nicht. Ein
            // `Map.find`, das durch `List.head transactions` ersetzt wird, zerstört
            // genau diese Zuordnung und bliebe mit einer einzigen Transaktion
            // unentdeckt (Verifier-Fund, Iteration 1); mit dreien wird der Vergleich
            // je TransactionId geführt, also fällt die Mutation auf.
            let txs = [
                Fixtures.makeTx "tx-fq3kd-1" (Some "dm") "Einkauf" "REF-1"
                Fixtures.makeTx "tx-fq3kd-2" (Some "Rewe") "Wocheneinkauf" "REF-2"
                Fixtures.makeTx "tx-fq3kd-3" (Some "Aral") "Tanken" "REF-3"
            ]

            let normalPrepared =
                prepareTransactions Fixtures.today Fixtures.accountId Map.empty false txs
            let normalCandidates = buildComdirectImportCandidates false txs normalPrepared

            let forcedPrepared =
                prepareTransactions Fixtures.today Fixtures.accountId Map.empty true txs
            let forcedCandidates = buildComdirectImportCandidates true txs forcedPrepared

            let normalByTransactionId =
                normalCandidates |> List.map (fun c -> c.Prepared.TransactionId, c) |> Map.ofList
            let forcedByTransactionId =
                forcedCandidates |> List.map (fun c -> c.Prepared.TransactionId, c) |> Map.ofList

            for tx in txs do
                let (TransactionId rawId) = tx.Transaction.Id
                let normalCandidate = Map.find tx.Transaction.Id normalByTransactionId
                let forcedCandidate = Map.find tx.Transaction.Id forcedByTransactionId
                Expect.equal
                    forcedCandidate.ExistingEntryImportId
                    (Some normalCandidate.Prepared.ImportId)
                    $"Force-Import muss für TransactionId {rawId} über genau die ImportId suchen, mit der der normale Push die bestehende Buchung geschrieben hat"
                // Bindet die Map.find-Zuordnung selbst mit ein: ExistingEntryImportId
                // hängt nur an p.TransactionId, nicht an tx. Erst ComdirectReference
                // (kommt aus tx, nicht aus p) macht sichtbar, ob buildComdirectImportCandidates
                // wirklich die eigene, nicht irgendeine rohe SyncTransaction zu p
                // findet (Verifier-Beleg: Map.find durch List.head ersetzt).
                Expect.equal
                    normalCandidate.ComdirectReference
                    tx.Transaction.Reference
                    $"ComdirectReference muss zur eigenen TransactionId {rawId} gehören, nicht zu einer fremden (Map.find-Zuordnung)"

            // Kriterium 3: der normale Pfad sucht für keine der drei Transaktionen.
            Expect.isTrue
                (normalCandidates |> List.forall (fun c -> c.ExistingEntryImportId = None))
                "der normale Import-Push darf für keine der drei Transaktionen über eine abweichende ImportId suchen"

        testCase "die gesendete und die gesuchte ImportId unterscheiden sich beim Force-Import" <| fun () ->
            // Fängt eine Vereinfachung ab, die beide ImportIds gleichsetzt: dann sendet
            // Force-Import wieder mit der alten ImportId und läuft direkt in YNABs
            // eigene Duplikat-Erkennung (ADR 0021), statt sie gezielt zu umgehen.
            let tx = Fixtures.makeTx "tx-unterschied" (Some "Rewe") "Wocheneinkauf" "REF-2"

            let forcedPrepared =
                prepareTransactions Fixtures.today Fixtures.accountId Map.empty true [ tx ]
            let forcedCandidate =
                buildComdirectImportCandidates true [ tx ] forcedPrepared |> List.exactlyOne

            Expect.notEqual
                forcedCandidate.Prepared.ImportId
                forcedCandidate.ExistingEntryImportId.Value
                "die gesendete ImportId (neu, nur für YNABs Dedup) darf nicht mit der gesuchten (alt, deterministisch) übereinstimmen"
            Expect.stringStarts
                forcedCandidate.Prepared.ImportId
                $"{Shared.Domain.ImportIdPrefix}:"
                "auch die neu erzeugte ImportId trägt weiterhin das BB-Präfix"

        testCase "Payee und Memo kommen aus dem vorbereiteten Request, die Rohdatenfelder bleiben roh" <| fun () ->
            // Verifier-Fund (ledger-qvt84, Iteration 3): schriebe die Entstehung die
            // rohen Bank-Felder statt p.Request, sähe der nächste Spiegel-Lauf ein
            // falsches Kopfdaten-Ereignis. `PayeeOverride` erzeugt hier bewusst einen
            // rohen Payee, der vom aufgelösten `Request.PayeeName` abweicht
            // (`buildTransactionRequest`: `PayeeOverride |> Option.orElse Transaction.Payee`),
            // damit ein Rückfall auf das rohe Feld sichtbar auffiele. Die rohe
            // Comdirect-Reference bleibt trotzdem im eigenen Feld ComdirectReference
            // unverändert stehen.
            //
            // Kriterium 5 ("die Rohdatenfelder bleiben roh"): BookingDate weicht bewusst
            // von Fixtures.today ab und ReviewSteps ist bewusst nicht leer. Ein
            // Vertauschen dieser Felder in buildComdirectImportCandidates fiele mit
            // einer Fixture, die zufällig mit today oder [] übereinstimmt, nicht auf
            // (Verifier-Fund, Iteration 1); mit einem abweichenden Wert schon.
            let rawReference = "REF-4711"
            let rawPayee = "Rohes Comdirect-Payee"
            let rawBookingDate = DateTime(2026, 9, 18) // bewusst ungleich Fixtures.today
            let rawReviewStep : ReviewStep = {
                At = DateTime(2026, 9, 17)
                By = ByUser
                Category = None
                CategoryName = None
                Splits = None
                PayeeOverride = None
            }
            let baseTx = Fixtures.makeTx "tx-payee" (Some rawPayee) "Rohes Memo" rawReference
            let tx =
                { baseTx with
                    PayeeOverride = Some "Romans Override"
                    Transaction = { baseTx.Transaction with BookingDate = rawBookingDate }
                    ReviewSteps = [ rawReviewStep ] }

            let prepared =
                prepareTransactions Fixtures.today Fixtures.accountId Map.empty false [ tx ]
            let c = buildComdirectImportCandidates false [ tx ] prepared |> List.exactlyOne

            Expect.equal c.Payee (Some c.Prepared.Request.PayeeName)
                "Payee muss aus dem vorbereiteten Request kommen"
            Expect.equal c.Memo (Some c.Prepared.Request.Memo)
                "Memo muss aus dem vorbereiteten Request kommen"
            Expect.notEqual c.Payee (Some rawPayee)
                "der rohe Comdirect-Payee darf nicht direkt übernommen werden, wenn ein Override gesetzt ist"
            Expect.stringContains c.Memo.Value ("Ref: " + rawReference)
                "Memo trägt den Referenz-Suffix, den buildMemoWithReference anhängt"
            Expect.equal c.ComdirectReference rawReference
                "die rohe Comdirect-Reference bleibt unverändert im eigenen Feld"
            Expect.equal c.Date tx.Transaction.BookingDate
                "Date muss die rohe BookingDate bleiben, nicht das an prepareTransactions übergebene today"
            Expect.equal c.Amount tx.Transaction.Amount
                "Amount muss der rohe Transaktionsbetrag bleiben"
            Expect.equal c.ReviewSteps tx.ReviewSteps
                "ReviewSteps müssen unverändert aus der rohen SyncTransaction übernommen werden"
    ]
