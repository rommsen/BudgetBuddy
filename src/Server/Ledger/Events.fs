/// Die Sprache der Kassenbuch-Historie (ADR 0013).
///
/// Verbindlicher Ereignis-Katalog, modelliert mit Roman am 2026-09-06 und gepflegt in
/// `.agentheim/knowledge/contexts/ledger/README.md` unter "Ereignisse". Hier stehen ausschliesslich
/// Umschlag, Actor, das Ereignis-DU und die Payload-Records samt JSON-Codecs. Kein I/O.
module Ledger.Events

open System
open System.Globalization
open Thoth.Json.Net
open Shared.Domain

// ============================================
// Identitaeten (ADR 0015)
// ============================================

/// Die eigene, stabile Id einer Buchung im Kassenbuch. Erzeugt mit
/// `Guid.CreateVersion7()` beim ersten Ereignis, danach nie geaendert (ADR 0015).
type EntryId = EntryId of Guid

/// Die eigene Id eines Kontos im Kassenbuch (ADR 0016). Fremd-Ids wie die
/// `YnabAccountId` haengen als Verknuepfung daran, sind aber nicht die Id selbst.
type LedgerAccountId = LedgerAccountId of Guid

/// Erzeugt eine neue, zeitsortierbare Buchungs-Id (ADR 0015).
let newEntryId () = EntryId (Guid.CreateVersion7())

/// Erzeugt eine neue, zeitsortierbare Konto-Id (ADR 0015/0016).
let newLedgerAccountId () = LedgerAccountId (Guid.CreateVersion7())

// ============================================
// Actor (Herkunft jeder Aenderung)
// ============================================

/// Woher eine Aenderung stammt. Pflichtfeld auf jedem Ereignis (ADR 0013, Punkt 3).
/// "UserInBb" statt "Roman": Single-User-Modell, die Frau nutzt mit.
type Actor =
    /// Der Spiegel hat die Aenderung in YNAB gesehen.
    | YnabMirror
    /// Die Buchung kam aus dem Comdirect-Import.
    | ComdirectImport
    /// Eine Kategorisierungs-Regel hat entschieden.
    | Rule of RuleId
    /// Ein Mensch hat in BB entschieden.
    | UserInBb
    /// Die Buchung wurde ueber Quick Add erfasst.
    | QuickAdd
    /// Eine Kategorie stammt aus dem Order-ID-Abgleich beim Abruf (Vorschlag aus der
    /// YNAB-Historie derselben Amazon-Bestellung, `ReviewActor.ByOrderIdMatch`). Kein
    /// Regel-Treffer und keine Entscheidung Romans für DIESE Buchung (Entscheidung
    /// Roman, 2026-09-11, categorization-gdrx8). Anzeigename "Order-ID-Abgleich".
    | OrderIdMatch of orderId: string

// ============================================
// Bausteine der Payloads
// ============================================

/// Der Cleared-Status einer Buchung, wie YNAB ihn fuehrt.
type ClearedState =
    | Uncleared
    | Cleared
    | Reconciled

/// Kategorie zum Beobachtungszeitpunkt: Id und Name (ADR 0014 verlangt den Namen
/// im Snapshot, damit die Historie ohne Nachschlagen lesbar bleibt).
type CategoryRef = {
    CategoryId: YnabCategoryId
    CategoryName: string
}

// ============================================
// Beobachtungs-Familie
// ============================================
// Der Spiegel hat in YNAB etwas gesehen. Dieser Slice definiert die Typen;
// erzeugt werden sie vom Spiegel-Lauf (ledger-m8r2v).

/// Erstbeobachtung: die Buchung wurde zum ersten Mal in YNAB gesehen. Voller Snapshot.
///
/// `Approved`/`FlagColor`/`FlagName` sind Schema-Version 2 (ADR 0032, Punkt 4): ein
/// gespeichertes Ereignis in Version 1 kennt diese Felder nicht und gilt beim Lesen
/// als `Approved = true`, ohne Flag (`Codec.decodeObservedV1`).
type EntryObservedInYnabPayload = {
    AccountId: LedgerAccountId
    YnabTransactionId: string
    ImportId: string option
    Date: DateTime
    Amount: Money
    Payee: string option
    Memo: string option
    Category: CategoryRef option
    Splits: TransactionSplit list
    Cleared: ClearedState
    /// Gegenkonto eines Uebertrags, wie YNAB es ausweist.
    TransferAccountId: YnabAccountId option
    /// Die YNAB-Transaction-Id der Gegenbuchung (`transfer_transaction_id`).
    TransferYnabTransactionId: string option
    /// YNABs `approved` (nie null). Teil des Prüfvermerks neben `Cleared` (ADR 0032).
    Approved: bool
    /// YNABs `flag_color`, roh. Die Deutung passiert im Lesepfad (ADR 0032).
    FlagColor: string option
    /// YNABs `flag_name`, roh.
    FlagName: string option
}

/// Kopfdaten in YNAB geaendert, auch die Verschiebung auf ein anderes Konto.
type EntryDetailsChangeObservedInYnabPayload = {
    OldDate: DateTime
    NewDate: DateTime
    OldAmount: Money
    NewAmount: Money
    OldPayee: string option
    NewPayee: string option
    OldMemo: string option
    NewMemo: string option
    OldAccountId: LedgerAccountId
    NewAccountId: LedgerAccountId
}

/// Kategorie oder Splits in YNAB geaendert.
type EntryCategorizationChangeObservedInYnabPayload = {
    OldCategory: CategoryRef option
    NewCategory: CategoryRef option
    OldSplits: TransactionSplit list
    NewSplits: TransactionSplit list
}

/// Cleared-Status in YNAB geaendert. Eigenes Ereignis, damit die Kassenbuch-Seite
/// dieses Rauschen ausblenden kann.
type EntryClearedStateChangeObservedInYnabPayload = {
    OldCleared: ClearedState
    NewCleared: ClearedState
}

/// `approved` in YNAB geändert (ADR 0032): die eine Hälfte des Prüfvermerks neben
/// `Cleared`. Eigenes Ereignis, analog zu `EntryClearedStateChangeObservedInYnab`.
type EntryApprovalChangeObservedInYnabPayload = {
    OldApproved: bool
    NewApproved: bool
}

/// `flag_color`/`flag_name` in YNAB geändert (ADR 0032). Roh, wie YNAB sie liefert;
/// die Deutung (Rückfrage Lena, Markiert, kein Flag) passiert im Lesepfad.
type EntryFlagChangeObservedInYnabPayload = {
    OldFlagColor: string option
    OldFlagName: string option
    NewFlagColor: string option
    NewFlagName: string option
}

/// Die Gegenbuchung eines Uebertrags ist gefunden. YNAB fuehrt beide Seiten ueber
/// `transfer_transaction_id`; der Spiegel loest diese Fremd-Id auf die eigene
/// Buchungs-Id auf (ADR 0015). Wechselseitig, aber je Seite ein eigenes Ereignis:
/// der Umschlag traegt genau eine Buchungs-Id.
type EntryTransferCounterpartObservedInYnabPayload = {
    /// Die Kassenbuch-Id der Gegenbuchung.
    CounterpartEntryId: EntryId
    /// YNABs `transfer_transaction_id`, ueber die aufgeloest wurde.
    CounterpartYnabTransactionId: string
}

// ============================================
// Entscheidungs-Familie
// ============================================
// BB hat selbst etwas festgelegt. Payloads modelliert mit dem tactical-modeler
// (2026-09-11), Reihenfolge und `EntryLinkedToYnab` aus ADR 0021.

/// Comdirect-Buchung beim Push angelegt. Traegt bewusst keine Kategorie: jede Buchung
/// entsteht kategorielos, die Kategorisierung folgt als eigenes Ereignis in derselben
/// Sequenz (Regel oder Roman).
type EntryImportedFromComdirectPayload = {
    AccountId: LedgerAccountId
    ImportId: string
    ComdirectReference: string
    Date: DateTime
    Amount: Money
    Payee: string option
    Memo: string option
    Cleared: ClearedState
    /// Immer `false` beim Schreiben (ADR 0032, Schema-Version 2): der Push sendet
    /// kein `approved`, YNAB legt die Buchung unbestätigt an. Version 1 ohne dieses
    /// Feld gilt beim Lesen als `true` (`Codec.decodeImportedFromComdirectV1`).
    Approved: bool
}

/// Eine Kategorisierungs-Regel hat entschieden. Nur auf einer noch unkategorisierten
/// Buchung zulaessig (Invariante in `apply`); der Actor traegt die `RuleId`.
type EntryCategorizedByRulePayload = {
    RuleName: string
    Category: CategoryRef option
    Splits: TransactionSplit list
    PayeeOverride: string option
}

/// Roman hat in BB kategorisiert oder umkategorisiert (Actor `UserInBb`).
type EntryRecategorizedPayload = {
    OldCategory: CategoryRef option
    NewCategory: CategoryRef option
    OldSplits: TransactionSplit list
    NewSplits: TransactionSplit list
    OldPayee: string option
    NewPayee: string option
}

/// Quick-Add-Buchung angelegt, vor dem Push (Actor `QuickAdd`). Keine ImportId
/// (ADR 0004): Quick Add wird ueber die Push-Antwort verknuepft, nicht ueber Dedup.
type CashExpenseAddedPayload = {
    AccountId: LedgerAccountId
    Date: DateTime
    Amount: Money
    Payee: string option
    Memo: string option
    Category: CategoryRef option
    Cleared: ClearedState
    /// Immer `false` beim Schreiben (ADR 0032, Schema-Version 2): Quick Add sendet
    /// kein `approved` an YNAB. Version 1 ohne dieses Feld gilt beim Lesen als `true`
    /// (`Codec.decodeCashExpenseAddedV1`).
    Approved: bool
}

/// Die YNAB-Id ist bekannt geworden: aus der Push-Antwort (Actor `ComdirectImport`
/// bzw. `QuickAdd`) oder weil der Spiegel eine unverknuepfte Buchung ueber die ImportId
/// gefunden hat (Actor `YnabMirror`). ADR 0021: eigenes Ereignis, sonst koennte Replay
/// die Verknuepfung nicht aus dem Log herleiten.
type EntryLinkedToYnabPayload = { YnabTransactionId: string }

/// Der fachliche Inhalt eines Ereignisses.
type LedgerEventBody =
    | EntryObservedInYnab of EntryObservedInYnabPayload
    | EntryDetailsChangeObservedInYnab of EntryDetailsChangeObservedInYnabPayload
    | EntryCategorizationChangeObservedInYnab of EntryCategorizationChangeObservedInYnabPayload
    | EntryClearedStateChangeObservedInYnab of EntryClearedStateChangeObservedInYnabPayload
    | EntryApprovalChangeObservedInYnab of EntryApprovalChangeObservedInYnabPayload
    | EntryFlagChangeObservedInYnab of EntryFlagChangeObservedInYnabPayload
    /// Die Gegenbuchung eines Uebertrags ist gefunden (ADR 0015). Beobachtung, nicht
    /// Entscheidung (ADR 0013): BB uebersetzt nur eine Fremd-Id in die eigene
    /// Buchungs-Id, festgestellt hat YNAB, dass beide Buchungen eine Bewegung sind.
    | EntryTransferCounterpartObservedInYnab of EntryTransferCounterpartObservedInYnabPayload
    /// In YNAB geloescht gesehen. Nur Umschlag, keine Payload; der Zustand behaelt die
    /// Buchung und setzt das Flag `DeletedInYnab`.
    | EntryDeletionObservedInYnab
    | EntryImportedFromComdirect of EntryImportedFromComdirectPayload
    | EntryCategorizedByRule of EntryCategorizedByRulePayload
    | EntryRecategorized of EntryRecategorizedPayload
    | CashExpenseAdded of CashExpenseAddedPayload
    | EntryLinkedToYnab of EntryLinkedToYnabPayload

// ============================================
// Entscheidungs-Familie: noch reservierter Name
// ============================================

/// Der Name des letzten noch unmodellierten Entscheidungs-Ereignisses, reserviert damit
/// die Sprache vollstaendig ist. Bewusst ein eigenes DU und **nicht** Teil von
/// `LedgerEventBody`: die Payload wird erst modelliert, wenn ihr Slice (Bewertung des
/// Wertpapierkontos) sie braucht. Wird in diesem Slice weder erzeugt noch gespeichert.
type ReservedDecisionEventName = ValuationRecorded

// ============================================
// Umschlag
// ============================================

/// Ein Ereignis, wie es geschrieben wird: Buchungs-Id, Zeitpunkt (UTC), Actor, Inhalt.
/// Die laufende Nummer vergibt der Store beim Anhaengen, deshalb steht sie hier nicht;
/// sie gehoert zum gespeicherten Ereignis (`RecordedLedgerEvent`).
type LedgerEvent = {
    EntryId: EntryId
    OccurredAt: DateTime
    Actor: Actor
    Body: LedgerEventBody
}

/// Ein gespeichertes Ereignis: der volle Umschlag aus dem Log, inklusive laufender
/// Nummer je Buchung (ab 1), Ereignistyp-Name und Schema-Version (ADR 0013, Punkt 7).
type RecordedLedgerEvent = {
    Sequence: int
    EventType: string
    SchemaVersion: int
    Event: LedgerEvent
}

/// Der gespeicherte Typname eines Ereignisses. Einmal gespeichert, aendert ein
/// Typname nie mehr seine Bedeutung (ADR 0013, Punkt 7).
let eventTypeName (body: LedgerEventBody) : string =
    match body with
    | EntryObservedInYnab _ -> "EntryObservedInYnab"
    | EntryDetailsChangeObservedInYnab _ -> "EntryDetailsChangeObservedInYnab"
    | EntryCategorizationChangeObservedInYnab _ -> "EntryCategorizationChangeObservedInYnab"
    | EntryClearedStateChangeObservedInYnab _ -> "EntryClearedStateChangeObservedInYnab"
    | EntryApprovalChangeObservedInYnab _ -> "EntryApprovalChangeObservedInYnab"
    | EntryFlagChangeObservedInYnab _ -> "EntryFlagChangeObservedInYnab"
    | EntryTransferCounterpartObservedInYnab _ -> "EntryTransferCounterpartObservedInYnab"
    | EntryDeletionObservedInYnab -> "EntryDeletionObservedInYnab"
    | EntryImportedFromComdirect _ -> "EntryImportedFromComdirect"
    | EntryCategorizedByRule _ -> "EntryCategorizedByRule"
    | EntryRecategorized _ -> "EntryRecategorized"
    | CashExpenseAdded _ -> "CashExpenseAdded"
    | EntryLinkedToYnab _ -> "EntryLinkedToYnab"

/// Die Schema-Version, mit der ein Ereignis heute geschrieben wird. Aendert sich die
/// Payload-Form eines Typs, kommt eine neue Version dazu; alte gespeicherte Ereignisse
/// bleiben unangetastet lesbar (ADR 0013, Punkt 7). `EntryObservedInYnab`,
/// `EntryImportedFromComdirect` und `CashExpenseAdded` schreiben seit ADR 0032 Version 2
/// (`approved`, bei `EntryObservedInYnab` zusätzlich Flag); Version 1 bleibt lesbar.
let schemaVersion (body: LedgerEventBody) : int =
    match body with
    | EntryObservedInYnab _ -> 2
    | EntryDetailsChangeObservedInYnab _ -> 1
    | EntryCategorizationChangeObservedInYnab _ -> 1
    | EntryClearedStateChangeObservedInYnab _ -> 1
    | EntryApprovalChangeObservedInYnab _ -> 1
    | EntryFlagChangeObservedInYnab _ -> 1
    | EntryTransferCounterpartObservedInYnab _ -> 1
    | EntryDeletionObservedInYnab -> 1
    | EntryImportedFromComdirect _ -> 2
    | EntryCategorizedByRule _ -> 1
    | EntryRecategorized _ -> 1
    | CashExpenseAdded _ -> 2
    | EntryLinkedToYnab _ -> 1

// ============================================
// JSON-Codecs
// ============================================
// Payload als JSON-Text (ADR 0013, Punkt 8). Handgeschriebene Codecs statt
// Auto-Codecs, damit die gespeicherte Form stabil bleibt und beim Versionswechsel
// bewusst erweitert wird.

module Codec =

    /// Zeitpunkte als ISO-8601-Roundtrip-Text. Bewusst nicht `Encode.datetime` /
    /// `Decode.datetimeUtc`: das Paar verschiebt einen Zeitpunkt ohne Zeitzone um den
    /// lokalen Offset, womit ein Replay andere Ticks liefern wuerde als die
    /// Zustandstabelle. "o" plus `RoundtripKind` ist tickgenau.
    let encodeDate (d: DateTime) =
        Encode.string (d.ToString("o", CultureInfo.InvariantCulture))

    let decodeDate : Decoder<DateTime> =
        Decode.string
        |> Decode.andThen (fun s ->
            match DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) with
            | true, d -> Decode.succeed d
            | false, _ -> Decode.fail $"Kein ISO-8601-Zeitpunkt: {s}")

    let encodeMoney (m: Money) =
        Encode.object [
            "amount", Encode.decimal m.Amount
            "currency", Encode.string m.Currency
        ]

    let decodeMoney : Decoder<Money> =
        Decode.object (fun get -> {
            Amount = get.Required.Field "amount" Decode.decimal
            Currency = get.Required.Field "currency" Decode.string
        })

    let encodeClearedState (c: ClearedState) =
        match c with
        | Uncleared -> Encode.string "Uncleared"
        | Cleared -> Encode.string "Cleared"
        | Reconciled -> Encode.string "Reconciled"

    let decodeClearedState : Decoder<ClearedState> =
        Decode.string
        |> Decode.andThen (function
            | "Uncleared" -> Decode.succeed Uncleared
            | "Cleared" -> Decode.succeed Cleared
            | "Reconciled" -> Decode.succeed Reconciled
            | other -> Decode.fail $"Unbekannter Cleared-Status: {other}")

    let encodeCategory (c: CategoryRef) =
        let (YnabCategoryId id) = c.CategoryId
        Encode.object [
            "categoryId", Encode.guid id
            "categoryName", Encode.string c.CategoryName
        ]

    let decodeCategory : Decoder<CategoryRef> =
        Decode.object (fun get -> {
            CategoryId = get.Required.Field "categoryId" Decode.guid |> YnabCategoryId
            CategoryName = get.Required.Field "categoryName" Decode.string
        })

    let encodeSplitTarget (t: SplitTarget) =
        match t with
        | ToCategory (YnabCategoryId id, name) ->
            Encode.object [
                "kind", Encode.string "category"
                "categoryId", Encode.guid id
                "categoryName", Encode.string name
            ]
        | ToTransfer (YnabAccountId id, name) ->
            Encode.object [
                "kind", Encode.string "transfer"
                "accountId", Encode.guid id
                "accountName", Encode.string name
            ]
        | Uncategorized ->
            // Eine unkategorisierte Split-Zeile, wie YNAB sie kennt. Eigener
            // Diskriminator statt einer leeren Kategorie: die Zeile bedeutet
            // "ohne Ziel", nicht "Ziel unbekannt".
            Encode.object [ "kind", Encode.string "uncategorized" ]

    let decodeSplitTarget : Decoder<SplitTarget> =
        Decode.field "kind" Decode.string
        |> Decode.andThen (function
            | "category" ->
                Decode.object (fun get ->
                    ToCategory (
                        get.Required.Field "categoryId" Decode.guid |> YnabCategoryId,
                        get.Required.Field "categoryName" Decode.string))
            | "transfer" ->
                Decode.object (fun get ->
                    ToTransfer (
                        get.Required.Field "accountId" Decode.guid |> YnabAccountId,
                        get.Required.Field "accountName" Decode.string))
            | "uncategorized" -> Decode.succeed Uncategorized
            | other -> Decode.fail $"Unbekanntes Split-Ziel: {other}")

    let encodeSplit (s: TransactionSplit) =
        Encode.object [
            "target", encodeSplitTarget s.Target
            "amount", encodeMoney s.Amount
            "memo", (match s.Memo with Some m -> Encode.string m | None -> Encode.nil)
        ]

    let decodeSplit : Decoder<TransactionSplit> =
        Decode.object (fun get -> {
            Target = get.Required.Field "target" decodeSplitTarget
            Amount = get.Required.Field "amount" decodeMoney
            Memo = get.Optional.Field "memo" Decode.string
        })

    let encodeSplits (splits: TransactionSplit list) =
        splits |> List.map encodeSplit |> Encode.list

    let decodeSplits : Decoder<TransactionSplit list> = Decode.list decodeSplit

    let private encodeOption (encoder: 'T -> JsonValue) (value: 'T option) =
        match value with
        | Some v -> encoder v
        | None -> Encode.nil

    let private encodeOptionalString = encodeOption Encode.string

    let private encodeEntryId (EntryId id) = Encode.guid id
    let private decodeEntryId : Decoder<EntryId> = Decode.guid |> Decode.map EntryId
    let private encodeAccountId (LedgerAccountId id) = Encode.guid id
    let private decodeAccountId = Decode.guid |> Decode.map LedgerAccountId

    // --- Payloads ---

    let encodeObserved (p: EntryObservedInYnabPayload) =
        Encode.object [
            "accountId", encodeAccountId p.AccountId
            "ynabTransactionId", Encode.string p.YnabTransactionId
            "importId", encodeOptionalString p.ImportId
            "date", encodeDate p.Date
            "amount", encodeMoney p.Amount
            "payee", encodeOptionalString p.Payee
            "memo", encodeOptionalString p.Memo
            "category", encodeOption encodeCategory p.Category
            "splits", encodeSplits p.Splits
            "cleared", encodeClearedState p.Cleared
            "transferAccountId",
                encodeOption (fun (YnabAccountId id) -> Encode.guid id) p.TransferAccountId
            "transferYnabTransactionId", encodeOptionalString p.TransferYnabTransactionId
            "approved", Encode.bool p.Approved
            "flagColor", encodeOptionalString p.FlagColor
            "flagName", encodeOptionalString p.FlagName
        ]

    /// Schema-Version 2 (ADR 0032): mit `approved`/`flagColor`/`flagName`.
    let decodeObserved : Decoder<EntryObservedInYnabPayload> =
        Decode.object (fun get -> {
            AccountId = get.Required.Field "accountId" decodeAccountId
            YnabTransactionId = get.Required.Field "ynabTransactionId" Decode.string
            ImportId = get.Optional.Field "importId" Decode.string
            Date = get.Required.Field "date" decodeDate
            Amount = get.Required.Field "amount" decodeMoney
            Payee = get.Optional.Field "payee" Decode.string
            Memo = get.Optional.Field "memo" Decode.string
            Category = get.Optional.Field "category" decodeCategory
            Splits = get.Optional.Field "splits" decodeSplits |> Option.defaultValue []
            Cleared = get.Required.Field "cleared" decodeClearedState
            TransferAccountId =
                get.Optional.Field "transferAccountId" Decode.guid |> Option.map YnabAccountId
            TransferYnabTransactionId =
                get.Optional.Field "transferYnabTransactionId" Decode.string
            Approved = get.Required.Field "approved" Decode.bool
            FlagColor = get.Optional.Field "flagColor" Decode.string
            FlagName = get.Optional.Field "flagName" Decode.string
        })

    /// Schema-Version 1 (ADR 0032, Punkt 3): ohne `approved`/`flagColor`/`flagName`.
    /// Ein Bestandsereignis in dieser Form gilt beim Lesen als genehmigt, ohne Flag.
    let decodeObservedV1 : Decoder<EntryObservedInYnabPayload> =
        Decode.object (fun get -> {
            AccountId = get.Required.Field "accountId" decodeAccountId
            YnabTransactionId = get.Required.Field "ynabTransactionId" Decode.string
            ImportId = get.Optional.Field "importId" Decode.string
            Date = get.Required.Field "date" decodeDate
            Amount = get.Required.Field "amount" decodeMoney
            Payee = get.Optional.Field "payee" Decode.string
            Memo = get.Optional.Field "memo" Decode.string
            Category = get.Optional.Field "category" decodeCategory
            Splits = get.Optional.Field "splits" decodeSplits |> Option.defaultValue []
            Cleared = get.Required.Field "cleared" decodeClearedState
            TransferAccountId =
                get.Optional.Field "transferAccountId" Decode.guid |> Option.map YnabAccountId
            TransferYnabTransactionId =
                get.Optional.Field "transferYnabTransactionId" Decode.string
            Approved = true
            FlagColor = None
            FlagName = None
        })

    let encodeDetailsChange (p: EntryDetailsChangeObservedInYnabPayload) =
        Encode.object [
            "oldDate", encodeDate p.OldDate
            "newDate", encodeDate p.NewDate
            "oldAmount", encodeMoney p.OldAmount
            "newAmount", encodeMoney p.NewAmount
            "oldPayee", encodeOptionalString p.OldPayee
            "newPayee", encodeOptionalString p.NewPayee
            "oldMemo", encodeOptionalString p.OldMemo
            "newMemo", encodeOptionalString p.NewMemo
            "oldAccountId", encodeAccountId p.OldAccountId
            "newAccountId", encodeAccountId p.NewAccountId
        ]

    let decodeDetailsChange : Decoder<EntryDetailsChangeObservedInYnabPayload> =
        Decode.object (fun get -> {
            OldDate = get.Required.Field "oldDate" decodeDate
            NewDate = get.Required.Field "newDate" decodeDate
            OldAmount = get.Required.Field "oldAmount" decodeMoney
            NewAmount = get.Required.Field "newAmount" decodeMoney
            OldPayee = get.Optional.Field "oldPayee" Decode.string
            NewPayee = get.Optional.Field "newPayee" Decode.string
            OldMemo = get.Optional.Field "oldMemo" Decode.string
            NewMemo = get.Optional.Field "newMemo" Decode.string
            OldAccountId = get.Required.Field "oldAccountId" decodeAccountId
            NewAccountId = get.Required.Field "newAccountId" decodeAccountId
        })

    let encodeCategorizationChange (p: EntryCategorizationChangeObservedInYnabPayload) =
        Encode.object [
            "oldCategory", encodeOption encodeCategory p.OldCategory
            "newCategory", encodeOption encodeCategory p.NewCategory
            "oldSplits", encodeSplits p.OldSplits
            "newSplits", encodeSplits p.NewSplits
        ]

    let decodeCategorizationChange : Decoder<EntryCategorizationChangeObservedInYnabPayload> =
        Decode.object (fun get -> {
            OldCategory = get.Optional.Field "oldCategory" decodeCategory
            NewCategory = get.Optional.Field "newCategory" decodeCategory
            OldSplits = get.Optional.Field "oldSplits" decodeSplits |> Option.defaultValue []
            NewSplits = get.Optional.Field "newSplits" decodeSplits |> Option.defaultValue []
        })

    let encodeClearedChange (p: EntryClearedStateChangeObservedInYnabPayload) =
        Encode.object [
            "oldCleared", encodeClearedState p.OldCleared
            "newCleared", encodeClearedState p.NewCleared
        ]

    let decodeClearedChange : Decoder<EntryClearedStateChangeObservedInYnabPayload> =
        Decode.object (fun get -> {
            OldCleared = get.Required.Field "oldCleared" decodeClearedState
            NewCleared = get.Required.Field "newCleared" decodeClearedState
        })

    let encodeApprovalChange (p: EntryApprovalChangeObservedInYnabPayload) =
        Encode.object [
            "oldApproved", Encode.bool p.OldApproved
            "newApproved", Encode.bool p.NewApproved
        ]

    let decodeApprovalChange : Decoder<EntryApprovalChangeObservedInYnabPayload> =
        Decode.object (fun get -> {
            OldApproved = get.Required.Field "oldApproved" Decode.bool
            NewApproved = get.Required.Field "newApproved" Decode.bool
        })

    let encodeFlagChange (p: EntryFlagChangeObservedInYnabPayload) =
        Encode.object [
            "oldFlagColor", encodeOptionalString p.OldFlagColor
            "oldFlagName", encodeOptionalString p.OldFlagName
            "newFlagColor", encodeOptionalString p.NewFlagColor
            "newFlagName", encodeOptionalString p.NewFlagName
        ]

    let decodeFlagChange : Decoder<EntryFlagChangeObservedInYnabPayload> =
        Decode.object (fun get -> {
            OldFlagColor = get.Optional.Field "oldFlagColor" Decode.string
            OldFlagName = get.Optional.Field "oldFlagName" Decode.string
            NewFlagColor = get.Optional.Field "newFlagColor" Decode.string
            NewFlagName = get.Optional.Field "newFlagName" Decode.string
        })

    let encodeTransferCounterpart (p: EntryTransferCounterpartObservedInYnabPayload) =
        Encode.object [
            "counterpartEntryId", encodeEntryId p.CounterpartEntryId
            "counterpartYnabTransactionId", Encode.string p.CounterpartYnabTransactionId
        ]

    let decodeTransferCounterpart : Decoder<EntryTransferCounterpartObservedInYnabPayload> =
        Decode.object (fun get -> {
            CounterpartEntryId = get.Required.Field "counterpartEntryId" decodeEntryId
            CounterpartYnabTransactionId = get.Required.Field "counterpartYnabTransactionId" Decode.string
        })

    let encodeImportedFromComdirect (p: EntryImportedFromComdirectPayload) =
        Encode.object [
            "accountId", encodeAccountId p.AccountId
            "importId", Encode.string p.ImportId
            "comdirectReference", Encode.string p.ComdirectReference
            "date", encodeDate p.Date
            "amount", encodeMoney p.Amount
            "payee", encodeOptionalString p.Payee
            "memo", encodeOptionalString p.Memo
            "cleared", encodeClearedState p.Cleared
            "approved", Encode.bool p.Approved
        ]

    /// Schema-Version 2 (ADR 0032): mit `approved`.
    let decodeImportedFromComdirect : Decoder<EntryImportedFromComdirectPayload> =
        Decode.object (fun get -> {
            AccountId = get.Required.Field "accountId" decodeAccountId
            ImportId = get.Required.Field "importId" Decode.string
            ComdirectReference = get.Required.Field "comdirectReference" Decode.string
            Date = get.Required.Field "date" decodeDate
            Amount = get.Required.Field "amount" decodeMoney
            Payee = get.Optional.Field "payee" Decode.string
            Memo = get.Optional.Field "memo" Decode.string
            Cleared = get.Required.Field "cleared" decodeClearedState
            Approved = get.Required.Field "approved" Decode.bool
        })

    /// Schema-Version 1: ohne `approved`. Gilt beim Lesen als genehmigt (ADR 0032).
    let decodeImportedFromComdirectV1 : Decoder<EntryImportedFromComdirectPayload> =
        Decode.object (fun get -> {
            AccountId = get.Required.Field "accountId" decodeAccountId
            ImportId = get.Required.Field "importId" Decode.string
            ComdirectReference = get.Required.Field "comdirectReference" Decode.string
            Date = get.Required.Field "date" decodeDate
            Amount = get.Required.Field "amount" decodeMoney
            Payee = get.Optional.Field "payee" Decode.string
            Memo = get.Optional.Field "memo" Decode.string
            Cleared = get.Required.Field "cleared" decodeClearedState
            Approved = true
        })

    let encodeCategorizedByRule (p: EntryCategorizedByRulePayload) =
        Encode.object [
            "ruleName", Encode.string p.RuleName
            "category", encodeOption encodeCategory p.Category
            "splits", encodeSplits p.Splits
            "payeeOverride", encodeOptionalString p.PayeeOverride
        ]

    let decodeCategorizedByRule : Decoder<EntryCategorizedByRulePayload> =
        Decode.object (fun get -> {
            RuleName = get.Required.Field "ruleName" Decode.string
            Category = get.Optional.Field "category" decodeCategory
            Splits = get.Optional.Field "splits" decodeSplits |> Option.defaultValue []
            PayeeOverride = get.Optional.Field "payeeOverride" Decode.string
        })

    let encodeRecategorized (p: EntryRecategorizedPayload) =
        Encode.object [
            "oldCategory", encodeOption encodeCategory p.OldCategory
            "newCategory", encodeOption encodeCategory p.NewCategory
            "oldSplits", encodeSplits p.OldSplits
            "newSplits", encodeSplits p.NewSplits
            "oldPayee", encodeOptionalString p.OldPayee
            "newPayee", encodeOptionalString p.NewPayee
        ]

    let decodeRecategorized : Decoder<EntryRecategorizedPayload> =
        Decode.object (fun get -> {
            OldCategory = get.Optional.Field "oldCategory" decodeCategory
            NewCategory = get.Optional.Field "newCategory" decodeCategory
            OldSplits = get.Optional.Field "oldSplits" decodeSplits |> Option.defaultValue []
            NewSplits = get.Optional.Field "newSplits" decodeSplits |> Option.defaultValue []
            OldPayee = get.Optional.Field "oldPayee" Decode.string
            NewPayee = get.Optional.Field "newPayee" Decode.string
        })

    let encodeCashExpenseAdded (p: CashExpenseAddedPayload) =
        Encode.object [
            "accountId", encodeAccountId p.AccountId
            "date", encodeDate p.Date
            "amount", encodeMoney p.Amount
            "payee", encodeOptionalString p.Payee
            "memo", encodeOptionalString p.Memo
            "category", encodeOption encodeCategory p.Category
            "cleared", encodeClearedState p.Cleared
            "approved", Encode.bool p.Approved
        ]

    /// Schema-Version 2 (ADR 0032): mit `approved`.
    let decodeCashExpenseAdded : Decoder<CashExpenseAddedPayload> =
        Decode.object (fun get -> {
            AccountId = get.Required.Field "accountId" decodeAccountId
            Date = get.Required.Field "date" decodeDate
            Amount = get.Required.Field "amount" decodeMoney
            Payee = get.Optional.Field "payee" Decode.string
            Memo = get.Optional.Field "memo" Decode.string
            Category = get.Optional.Field "category" decodeCategory
            Cleared = get.Required.Field "cleared" decodeClearedState
            Approved = get.Required.Field "approved" Decode.bool
        })

    /// Schema-Version 1: ohne `approved`. Gilt beim Lesen als genehmigt (ADR 0032).
    let decodeCashExpenseAddedV1 : Decoder<CashExpenseAddedPayload> =
        Decode.object (fun get -> {
            AccountId = get.Required.Field "accountId" decodeAccountId
            Date = get.Required.Field "date" decodeDate
            Amount = get.Required.Field "amount" decodeMoney
            Payee = get.Optional.Field "payee" Decode.string
            Memo = get.Optional.Field "memo" Decode.string
            Category = get.Optional.Field "category" decodeCategory
            Cleared = get.Required.Field "cleared" decodeClearedState
            Approved = true
        })

    let encodeLinkedToYnab (p: EntryLinkedToYnabPayload) =
        Encode.object [ "ynabTransactionId", Encode.string p.YnabTransactionId ]

    let decodeLinkedToYnab : Decoder<EntryLinkedToYnabPayload> =
        Decode.object (fun get -> {
            YnabTransactionId = get.Required.Field "ynabTransactionId" Decode.string
        })

/// Serialisiert die Payload eines Ereignisses als JSON-Text.
let encodePayload (body: LedgerEventBody) : string =
    let json =
        match body with
        | EntryObservedInYnab p -> Codec.encodeObserved p
        | EntryDetailsChangeObservedInYnab p -> Codec.encodeDetailsChange p
        | EntryCategorizationChangeObservedInYnab p -> Codec.encodeCategorizationChange p
        | EntryClearedStateChangeObservedInYnab p -> Codec.encodeClearedChange p
        | EntryApprovalChangeObservedInYnab p -> Codec.encodeApprovalChange p
        | EntryFlagChangeObservedInYnab p -> Codec.encodeFlagChange p
        | EntryTransferCounterpartObservedInYnab p -> Codec.encodeTransferCounterpart p
        | EntryDeletionObservedInYnab -> Encode.object []
        | EntryImportedFromComdirect p -> Codec.encodeImportedFromComdirect p
        | EntryCategorizedByRule p -> Codec.encodeCategorizedByRule p
        | EntryRecategorized p -> Codec.encodeRecategorized p
        | CashExpenseAdded p -> Codec.encodeCashExpenseAdded p
        | EntryLinkedToYnab p -> Codec.encodeLinkedToYnab p
    Encode.toString 0 json

/// Liest eine gespeicherte Payload zurueck. Typname und Schema-Version zusammen
/// bestimmen den Decoder; unbekannte Kombinationen sind ein Fehler, nie ein Rateversuch.
let decodePayload (eventType: string) (version: int) (payload: string) : Result<LedgerEventBody, string> =
    match eventType, version with
    | "EntryObservedInYnab", 1 ->
        Decode.fromString Codec.decodeObservedV1 payload |> Result.map EntryObservedInYnab
    | "EntryObservedInYnab", 2 ->
        Decode.fromString Codec.decodeObserved payload |> Result.map EntryObservedInYnab
    | "EntryDetailsChangeObservedInYnab", 1 ->
        Decode.fromString Codec.decodeDetailsChange payload |> Result.map EntryDetailsChangeObservedInYnab
    | "EntryCategorizationChangeObservedInYnab", 1 ->
        Decode.fromString Codec.decodeCategorizationChange payload |> Result.map EntryCategorizationChangeObservedInYnab
    | "EntryClearedStateChangeObservedInYnab", 1 ->
        Decode.fromString Codec.decodeClearedChange payload |> Result.map EntryClearedStateChangeObservedInYnab
    | "EntryApprovalChangeObservedInYnab", 1 ->
        Decode.fromString Codec.decodeApprovalChange payload |> Result.map EntryApprovalChangeObservedInYnab
    | "EntryFlagChangeObservedInYnab", 1 ->
        Decode.fromString Codec.decodeFlagChange payload |> Result.map EntryFlagChangeObservedInYnab
    | "EntryTransferCounterpartObservedInYnab", 1 ->
        Decode.fromString Codec.decodeTransferCounterpart payload |> Result.map EntryTransferCounterpartObservedInYnab
    | "EntryDeletionObservedInYnab", 1 -> Ok EntryDeletionObservedInYnab
    | "EntryImportedFromComdirect", 1 ->
        Decode.fromString Codec.decodeImportedFromComdirectV1 payload |> Result.map EntryImportedFromComdirect
    | "EntryImportedFromComdirect", 2 ->
        Decode.fromString Codec.decodeImportedFromComdirect payload |> Result.map EntryImportedFromComdirect
    | "EntryCategorizedByRule", 1 ->
        Decode.fromString Codec.decodeCategorizedByRule payload |> Result.map EntryCategorizedByRule
    | "EntryRecategorized", 1 ->
        Decode.fromString Codec.decodeRecategorized payload |> Result.map EntryRecategorized
    | "CashExpenseAdded", 1 ->
        Decode.fromString Codec.decodeCashExpenseAddedV1 payload |> Result.map CashExpenseAdded
    | "CashExpenseAdded", 2 ->
        Decode.fromString Codec.decodeCashExpenseAdded payload |> Result.map CashExpenseAdded
    | "EntryLinkedToYnab", 1 ->
        Decode.fromString Codec.decodeLinkedToYnab payload |> Result.map EntryLinkedToYnab
    | t, v -> Error $"Unbekannter Ereignistyp '{t}' in Schema-Version {v}"

// ============================================
// Actor: Persistenzform
// ============================================

/// Der Actor wird in zwei Spalten gespeichert (Art und optionale RuleId), damit sich
/// spaeter ohne JSON-Funktionen nach Herkunft filtern laesst (ADR 0013, Punkt 8).
let actorToColumns (actor: Actor) : string * string option =
    match actor with
    | YnabMirror -> "YnabMirror", None
    | ComdirectImport -> "ComdirectImport", None
    | Rule (RuleId id) -> "Rule", Some (id.ToString())
    | UserInBb -> "UserInBb", None
    | QuickAdd -> "QuickAdd", None
    // Die zweite Spalte ist frei benutzbares Text, kein reiner Guid-Slot: der
    // Order-ID-Abgleich legt hier die Bestell-Id ab statt einer RuleId.
    | OrderIdMatch orderId -> "OrderIdMatch", Some orderId

let actorFromColumns (kind: string) (ruleId: string option) : Result<Actor, string> =
    match kind, ruleId with
    | "YnabMirror", _ -> Ok YnabMirror
    | "ComdirectImport", _ -> Ok ComdirectImport
    | "Rule", Some id -> Ok (Rule (RuleId (Guid.Parse id)))
    | "Rule", None -> Error "Actor 'Rule' ohne RuleId gespeichert"
    | "UserInBb", _ -> Ok UserInBb
    | "QuickAdd", _ -> Ok QuickAdd
    | "OrderIdMatch", Some orderId -> Ok (OrderIdMatch orderId)
    | "OrderIdMatch", None -> Error "Actor 'OrderIdMatch' ohne OrderId gespeichert"
    | other, _ -> Error $"Unbekannter Actor: {other}"
