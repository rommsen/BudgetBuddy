/// Der Contract, den `ynab-sync` an das Kassenbuch abgibt (ADR 0014).
///
/// Eine **Beobachtung** ist rein datentragend: der volle Snapshot einer YNAB-Buchung zum
/// Zeitpunkt des Spiegel-Laufs plus das `deleted`-Flag. `ynab-sync` urteilt nicht, ob
/// etwas abweicht; ob und wie aus Beobachtungen Ereignisse werden, entscheidet der
/// `ledger` (`Ledger.Domain.observe`). Deshalb steht hier kein Ereignis-Vokabular.
///
/// Kein I/O: nur Typen und Thoth-Decoder. Den HTTP-Abruf macht `Server.YnabClient`.
///
/// Zur Einordnung im Kompilat: die Datei liegt bewusst zwischen `Ledger/Events.fs` und
/// `Ledger/Domain.fs`. Sie leiht sich aus `Ledger.Events` genau zwei Wertobjekte,
/// `ClearedState` und `CategoryRef` — beide beschreiben YNABs eigenes Modell (Cleared-
/// Status, Kategorie mit Namen, ADR 0014) und sind kein Ereignis-Vokabular. Alles
/// andere kommt aus `Shared.Domain`.
module Server.YnabObservations

open System
open System.Globalization
open Thoth.Json.Net
open Shared.Domain
open Ledger.Events

// ============================================
// Contract
// ============================================

/// Eine Übertrags-Split-Zeile, wie YNAB sie meldet (ADR 0046). Bewusst getrennt von
/// `TransactionSplit`: die Fremd-Id gehört nicht in den Snapshot-Vergleich der Splits.
type ObservedSplitTransfer = {
    /// YNABs Id der Split-Zeile (Subtransaction-`id`), die Adresse der Zeile.
    YnabSubtransactionId: string
    TransferAccountId: YnabAccountId
    /// Die YNAB-Transaction-Id der Gegenbuchung (`transfer_transaction_id` der Zeile).
    TransferYnabTransactionId: string option
    Amount: Money
}

/// Eine gesehene YNAB-Buchung, vollstaendig, ohne Wertung (ADR 0014).
type YnabObservation = {
    /// Die von YNAB vergebene Transaction-Id. Der erste Matching-Schluessel (ADR 0015).
    YnabTransactionId: string
    /// Das YNAB-Konto der Buchung. Der Spiegel loest es auf das Kassenbuch-Konto auf.
    AccountId: YnabAccountId
    /// BBs ImportId, falls die Buchung ueber den Push entstand. Zweiter Matching-Schluessel.
    ImportId: string option
    Date: DateTime
    Amount: Money
    /// YNABs `payee_name`.
    Payee: string option
    Memo: string option
    /// Kategorie mit Namen, damit die Historie ohne Nachschlagen lesbar bleibt.
    Category: CategoryRef option
    /// Die Split-Zeilen (YNABs `subtransactions`), geloeschte Zeilen ausgelassen.
    Splits: TransactionSplit list
    /// Die Übertrags-Zeilen der Splits mit Subtransaction-Id und Fremd-Id (ADR 0046),
    /// gelöschte Zeilen ausgelassen. Nicht Teil des Splits-Vergleichs in `observe`.
    SplitTransfers: ObservedSplitTransfer list
    Cleared: ClearedState
    /// Gegenkonto eines Uebertrags (`transfer_account_id`).
    TransferAccountId: YnabAccountId option
    /// Die YNAB-Transaction-Id der Gegenbuchung (`transfer_transaction_id`).
    TransferYnabTransactionId: string option
    /// In YNAB geloescht (`deleted: true` im Delta).
    Deleted: bool
    /// YNABs `approved` (nie null, ADR 0032): Teil des Prüfvermerks neben `Cleared`.
    Approved: bool
    /// YNABs `flag_color` (red, orange, yellow, green, blue, purple oder null), roh.
    FlagColor: string option
    /// YNABs `flag_name`, roh. Die Deutung ("Rückfrage Lena", "Markiert") passiert im
    /// Lesepfad des Kassenbuchs, nicht hier (ADR 0032).
    FlagName: string option
}

/// Das Ergebnis eines Transaktions-Abrufs: die Beobachtungen des Laufs und das
/// `server_knowledge` der Antwort, mit dem der naechste Lauf sein Delta holt.
type YnabObservationBatch = {
    Observations: YnabObservation list
    ServerKnowledge: int64
}

/// Ein gesehenes YNAB-Konto. Reine Daten wie die Buchungs-Beobachtung.
type YnabAccountObservation = {
    AccountId: YnabAccountId
    Name: string
    Closed: bool
    Deleted: bool
}

/// Das Ergebnis eines Konten-Abrufs.
type YnabAccountObservationBatch = {
    Accounts: YnabAccountObservation list
    ServerKnowledge: int64
}

// ============================================
// Decoder
// ============================================

module Decoders =

    let private milliunitsToMoney (milliunits: int64) : Money =
        { Amount = decimal milliunits / 1000m; Currency = "EUR" }

    /// YNAB benennt die auto-erzeugte Uebertrags-Payee "Transfer : <Konto>". Fuer die
    /// Transfer-Split-Zeile brauchen wir den Kontonamen, nicht den Payee-Namen; der
    /// Prefix wird deshalb abgeschnitten, wenn er da ist.
    let transferAccountName (payeeName: string option) : string =
        match payeeName with
        | None -> ""
        | Some name ->
            let prefix = "Transfer :"
            if name.StartsWith(prefix, StringComparison.Ordinal) then
                name.Substring(prefix.Length).Trim()
            else
                name

    /// YNABs `date` ist ein reiner Kalendertag ohne Zeitzone. Bewusst **nicht**
    /// `Decode.datetimeUtc`: das liest "2026-02-03" als Ortszeit und schiebt den Tag um
    /// den lokalen Offset zurück (in MEZ auf den 2. Februar, 23:00). Ein Kassenbuch,
    /// das Buchungstage auditierbar führt, darf diesen Versatz nicht haben. Geteilt mit
    /// `Server.YnabClient.Decoders.transactionDecoder` (ynab-r9k4t): derselbe Bug betraf
    /// auch dort den Datumsteil, keine zweite Kopie dieses Decoders.
    let ynabDateDecoder : Decoder<DateTime> =
        Decode.string
        |> Decode.andThen (fun text ->
            match
                DateTime.TryParseExact(
                    text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None)
            with
            | true, date -> Decode.succeed (DateTime.SpecifyKind(date, DateTimeKind.Utc))
            | false, _ -> Decode.fail $"Kein YNAB-Datum (yyyy-MM-dd): {text}")

    let clearedDecoder : Decoder<ClearedState> =
        Decode.string
        |> Decode.andThen (function
            | "uncleared" -> Decode.succeed Uncleared
            | "cleared" -> Decode.succeed Cleared
            | "reconciled" -> Decode.succeed Reconciled
            | other -> Decode.fail $"Unbekannter YNAB-Cleared-Status: {other}")

    /// Eine Split-Zeile, wie YNAB sie liefert, noch ohne Kassenbuch-Form.
    type private RawSubtransaction = {
        Id: string
        TransferTransactionId: string option
        Deleted: bool
        Amount: Money
        Memo: string option
        TransferAccountId: Guid option
        CategoryId: Guid option
        CategoryName: string option
        PayeeName: string option
    }

    let private rawSubtransactionDecoder : Decoder<RawSubtransaction> =
        Decode.object (fun get -> {
            Id = get.Required.Field "id" Decode.string
            TransferTransactionId = get.Optional.Field "transfer_transaction_id" Decode.string
            Deleted = get.Optional.Field "deleted" Decode.bool |> Option.defaultValue false
            Amount = get.Required.Field "amount" Decode.int64 |> milliunitsToMoney
            Memo = get.Optional.Field "memo" Decode.string
            TransferAccountId = get.Optional.Field "transfer_account_id" Decode.guid
            CategoryId = get.Optional.Field "category_id" Decode.guid
            CategoryName = get.Optional.Field "category_name" Decode.string
            PayeeName = get.Optional.Field "payee_name" Decode.string
        })

    /// Die Split-Zeilen einer Buchung. Geloeschte Zeilen fallen weg. Uebertrags-Zeile vor
    /// Kategorie-Zeile: eine Transfer-Subtransaction traegt nie eine Kategorie. Eine
    /// Zeile ohne beides ist eine **unkategorisierte Split-Zeile** und kommt als solche
    /// an — sie zaehlt beim Betrag voll mit und bricht keinen Lauf ab.
    /// Die rohe Liste wird einmal dekodiert und zweimal projiziert: auf die Splits
    /// (unverändert) und auf die Übertrags-Zeilen mit Subtransaction-Id (ADR 0046).
    let subtransactionsDecoder : Decoder<TransactionSplit list * ObservedSplitTransfer list> =
        Decode.list rawSubtransactionDecoder
        |> Decode.map (fun allRaws ->
            let raws = allRaws |> List.filter (fun r -> not r.Deleted)

            let splitTransfers =
                raws
                |> List.choose (fun raw ->
                    raw.TransferAccountId
                    |> Option.map (fun accountId -> {
                        YnabSubtransactionId = raw.Id
                        TransferAccountId = YnabAccountId accountId
                        TransferYnabTransactionId = raw.TransferTransactionId
                        Amount = raw.Amount
                    }))

            let splits =
                raws
                |> List.map (fun raw ->
                let target =
                    match raw.TransferAccountId, raw.CategoryId with
                    | Some accountId, _ ->
                        ToTransfer (YnabAccountId accountId, transferAccountName raw.PayeeName)
                    | None, Some categoryId ->
                        ToCategory (
                            YnabCategoryId categoryId,
                            raw.CategoryName |> Option.defaultValue "")
                    | None, None -> Uncategorized

                { Target = target; Amount = raw.Amount; Memo = raw.Memo })

            splits, splitTransfers)

    let observationDecoder : Decoder<YnabObservation> =
        Decode.object (fun get ->
            let categoryId = get.Optional.Field "category_id" Decode.guid
            let categoryName = get.Optional.Field "category_name" Decode.string

            let splits, splitTransfers =
                get.Optional.Field "subtransactions" subtransactionsDecoder
                |> Option.defaultValue ([], [])

            {
                YnabTransactionId = get.Required.Field "id" Decode.string
                AccountId = get.Required.Field "account_id" Decode.guid |> YnabAccountId
                ImportId = get.Optional.Field "import_id" Decode.string
                Date = get.Required.Field "date" ynabDateDecoder
                Amount = get.Required.Field "amount" Decode.int64 |> milliunitsToMoney
                Payee = get.Optional.Field "payee_name" Decode.string
                Memo = get.Optional.Field "memo" Decode.string
                Category =
                    match categoryId with
                    | Some id ->
                        Some {
                            CategoryId = YnabCategoryId id
                            CategoryName = categoryName |> Option.defaultValue ""
                        }
                    | None -> None
                Splits = splits
                SplitTransfers = splitTransfers
                Cleared = get.Required.Field "cleared" clearedDecoder
                TransferAccountId =
                    get.Optional.Field "transfer_account_id" Decode.guid |> Option.map YnabAccountId
                TransferYnabTransactionId =
                    get.Optional.Field "transfer_transaction_id" Decode.string
                Deleted = get.Optional.Field "deleted" Decode.bool |> Option.defaultValue false
                Approved = get.Required.Field "approved" Decode.bool
                FlagColor = get.Optional.Field "flag_color" Decode.string
                FlagName = get.Optional.Field "flag_name" Decode.string
            })

    /// `GET /budgets/{id}/transactions[?last_knowledge_of_server=N]`
    let observationBatchDecoder : Decoder<YnabObservationBatch> =
        Decode.field "data" (
            Decode.object (fun get -> {
                Observations = get.Required.Field "transactions" (Decode.list observationDecoder)
                ServerKnowledge = get.Required.Field "server_knowledge" Decode.int64
            }))

    let accountObservationDecoder : Decoder<YnabAccountObservation> =
        Decode.object (fun get -> {
            AccountId = get.Required.Field "id" Decode.guid |> YnabAccountId
            Name = get.Required.Field "name" Decode.string
            Closed = get.Optional.Field "closed" Decode.bool |> Option.defaultValue false
            Deleted = get.Optional.Field "deleted" Decode.bool |> Option.defaultValue false
        })

    /// `GET /budgets/{id}/accounts[?last_knowledge_of_server=N]`
    let accountObservationBatchDecoder : Decoder<YnabAccountObservationBatch> =
        Decode.field "data" (
            Decode.object (fun get -> {
                Accounts = get.Required.Field "accounts" (Decode.list accountObservationDecoder)
                ServerKnowledge = get.Required.Field "server_knowledge" Decode.int64
            }))
