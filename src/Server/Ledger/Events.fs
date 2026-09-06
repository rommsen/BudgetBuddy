/// Die Sprache der Kassenbuch-Historie (ADR 0013).
///
/// Verbindlicher Ereignis-Katalog, modelliert mit Roman am 2026-09-06 und gepflegt in
/// `.agentheim/contexts/ledger/README.md` unter "Ereignisse". Hier stehen ausschliesslich
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

/// Der fachliche Inhalt eines Ereignisses.
type LedgerEventBody =
    | EntryObservedInYnab of EntryObservedInYnabPayload
    | EntryDetailsChangeObservedInYnab of EntryDetailsChangeObservedInYnabPayload
    | EntryCategorizationChangeObservedInYnab of EntryCategorizationChangeObservedInYnabPayload
    | EntryClearedStateChangeObservedInYnab of EntryClearedStateChangeObservedInYnabPayload
    /// In YNAB geloescht gesehen. Nur Umschlag, keine Payload; der Zustand behaelt die
    /// Buchung und setzt das Flag `DeletedInYnab`.
    | EntryDeletionObservedInYnab

// ============================================
// Entscheidungs-Familie: reservierte Namen
// ============================================

/// Die Namen der Entscheidungs-Ereignisse (BB hat etwas festgelegt), reserviert damit
/// die Sprache vollstaendig ist. Bewusst ein eigenes DU und **nicht** Teil von
/// `LedgerEventBody`: die Payloads werden erst in den jeweiligen spaeteren Slices
/// modelliert, und ein Payload waere hier erfunden. Sobald ein Slice seine Payload
/// festlegt, wandert der Name als vollwertiger Fall nach `LedgerEventBody`.
/// Wird in diesem Slice weder erzeugt noch gespeichert.
type ReservedDecisionEventName =
    | EntryImportedFromComdirect
    | EntryCategorizedByRule
    | EntryRecategorized
    | CashExpenseAdded
    | ValuationRecorded

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
    | EntryDeletionObservedInYnab -> "EntryDeletionObservedInYnab"

/// Die Schema-Version, mit der ein Ereignis heute geschrieben wird. Aendert sich die
/// Payload-Form eines Typs, kommt eine neue Version dazu; alte gespeicherte Ereignisse
/// bleiben unangetastet lesbar (ADR 0013, Punkt 7).
let schemaVersion (body: LedgerEventBody) : int =
    match body with
    | EntryObservedInYnab _ -> 1
    | EntryDetailsChangeObservedInYnab _ -> 1
    | EntryCategorizationChangeObservedInYnab _ -> 1
    | EntryClearedStateChangeObservedInYnab _ -> 1
    | EntryDeletionObservedInYnab -> 1

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
        ]

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

/// Serialisiert die Payload eines Ereignisses als JSON-Text.
let encodePayload (body: LedgerEventBody) : string =
    let json =
        match body with
        | EntryObservedInYnab p -> Codec.encodeObserved p
        | EntryDetailsChangeObservedInYnab p -> Codec.encodeDetailsChange p
        | EntryCategorizationChangeObservedInYnab p -> Codec.encodeCategorizationChange p
        | EntryClearedStateChangeObservedInYnab p -> Codec.encodeClearedChange p
        | EntryDeletionObservedInYnab -> Encode.object []
    Encode.toString 0 json

/// Liest eine gespeicherte Payload zurueck. Typname und Schema-Version zusammen
/// bestimmen den Decoder; unbekannte Kombinationen sind ein Fehler, nie ein Rateversuch.
let decodePayload (eventType: string) (version: int) (payload: string) : Result<LedgerEventBody, string> =
    match eventType, version with
    | "EntryObservedInYnab", 1 ->
        Decode.fromString Codec.decodeObserved payload |> Result.map EntryObservedInYnab
    | "EntryDetailsChangeObservedInYnab", 1 ->
        Decode.fromString Codec.decodeDetailsChange payload |> Result.map EntryDetailsChangeObservedInYnab
    | "EntryCategorizationChangeObservedInYnab", 1 ->
        Decode.fromString Codec.decodeCategorizationChange payload |> Result.map EntryCategorizationChangeObservedInYnab
    | "EntryClearedStateChangeObservedInYnab", 1 ->
        Decode.fromString Codec.decodeClearedChange payload |> Result.map EntryClearedStateChangeObservedInYnab
    | "EntryDeletionObservedInYnab", 1 -> Ok EntryDeletionObservedInYnab
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

let actorFromColumns (kind: string) (ruleId: string option) : Result<Actor, string> =
    match kind, ruleId with
    | "YnabMirror", _ -> Ok YnabMirror
    | "ComdirectImport", _ -> Ok ComdirectImport
    | "Rule", Some id -> Ok (Rule (RuleId (Guid.Parse id)))
    | "Rule", None -> Error "Actor 'Rule' ohne RuleId gespeichert"
    | "UserInBb", _ -> Ok UserInBb
    | "QuickAdd", _ -> Ok QuickAdd
    | other, _ -> Error $"Unbekannter Actor: {other}"
