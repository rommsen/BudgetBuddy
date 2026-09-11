/// Das Zustandsmodell des Kassenbuchs und die reine Faltung der Historie darauf.
///
/// Rein, ohne I/O (ADR 0013, Punkt 6): der Zustand ist aus dem Ereignis-Log ableitbar,
/// `apply` ist die einzige Stelle, die diese Ableitung kennt. `Ledger.Persistence`
/// schreibt Log und Zustand mit genau dieser Funktion in einer Transaktion fort, ein
/// Replay faltet dieselbe Funktion ueber das gespeicherte Log.
module Ledger.Domain

open System
open Shared.Domain
open Ledger.Events
open Server.YnabObservations

// ============================================
// Konto (reiner Zustand, keine Historie in v1)
// ============================================

/// Die Art eines Kontos (ADR 0016). Orthogonal zur Verknuepfung; in v1 wird nur
/// `BankAccount` benutzt, die anderen Arten sind modelliert, aber ohne Instanz.
type AccountKind =
    | BankAccount
    | CashAccount
    | SecuritiesAccount

/// Die Verknuepfung eines Kontos (ADR 0016): gespiegelt aus YNAB oder BB-eigen.
/// "BB-eigen" hat in v1 keine Instanz, bleibt aber Teil des Modells.
type AccountLink =
    | YnabMirrored of YnabAccountId
    | BbOwned

/// Ein Konto im Kassenbuch. Kein Kontostand als Spalte: der Stand ist die Summe der
/// nicht geloeschten Buchungen und damit eine Query.
type LedgerAccount = {
    Id: LedgerAccountId
    Name: string
    Kind: AccountKind
    Link: AccountLink
    Closed: bool
}

// ============================================
// Fremd-Ids als Verknuepfungen (ADR 0015)
// ============================================

/// Das Fremdsystem, aus dem eine Verknuepfung stammt.
type LinkSystem =
    /// Die von YNAB vergebene Transaction-Id.
    | YnabSystem
    /// Die von BB erzeugte ImportId (YNAB-Dedup).
    | ImportIdSystem
    /// Die Comdirect-Reference. Wird mitgefuehrt, nie gematcht (ADR 0015).
    | ComdirectSystem

/// Eine Fremd-Id, die auf genau eine Buchung zeigt. Je System und Wert eindeutig.
type EntryLink = {
    EntryId: EntryId
    System: LinkSystem
    Value: string
}

let linkSystemToString =
    function
    | YnabSystem -> "ynab"
    | ImportIdSystem -> "importid"
    | ComdirectSystem -> "comdirect"

let linkSystemFromString =
    function
    | "ynab" -> Ok YnabSystem
    | "importid" -> Ok ImportIdSystem
    | "comdirect" -> Ok ComdirectSystem
    | other -> Error $"Unbekanntes Fremdsystem: {other}"

// ============================================
// Buchung (Zustandsmodell)
// ============================================

/// Der aktuelle Stand einer Buchung, abgeleitet aus ihrem Ereignis-Log.
type Entry = {
    Id: EntryId
    AccountId: LedgerAccountId
    Date: DateTime
    Amount: Money
    Payee: string option
    Memo: string option
    Category: CategoryRef option
    Splits: TransactionSplit list
    Cleared: ClearedState
    /// Die Gegenbuchung eines Uebertrags (Buchung zu Buchung, ADR 0015). Bleibt in v1
    /// leer: der Spiegel sieht nur YNABs `transfer_transaction_id`, das Aufloesen zur
    /// eigenen Buchungs-Id ist Arbeit eines spaeteren Slices.
    TransferEntryId: EntryId option
    /// In YNAB geloescht gesehen. Die Buchung bleibt im Kassenbuch stehen.
    DeletedInYnab: bool
}

// ============================================
// Faltung
// ============================================

/// Ist dieses Ereignis eines der drei Entstehungs-Ereignisse? Genau eines davon darf
/// die erste Zeile einer Buchung sein (Notes des Katalogs, umgesetzt in `apply`).
let private isCreationEvent (body: LedgerEventBody) : bool =
    match body with
    | EntryObservedInYnab _
    | EntryImportedFromComdirect _
    | CashExpenseAdded _ -> true
    | _ -> false

/// Kategorie und Splits schliessen sich aus (ADR 0006/0019 gelten auch fuer die
/// Entscheidungs-Familie): eine Buchung traegt entweder eine Top-Level-Kategorie oder
/// Splits, nie beides gleichzeitig. Gesetzte Splits muessen ausserdem die
/// `mkSplits`-Invarianten aus ADR 0006 erfuellen (mindestens zwei Zeilen,
/// vorzeichenrichtige Summe gleich dem Buchungsbetrag, eine Waehrung); `mkSplits`
/// selbst ist die einzige Stelle, die diese Regeln kennt, `apply` fragt sie nur ab.
let private requireCategoryXorSplits
    (eventName: string)
    (total: Money)
    (category: CategoryRef option)
    (splits: TransactionSplit list)
    =
    if category.IsSome && not (List.isEmpty splits) then
        invalidOp $"{eventName}: Kategorie und Splits schliessen sich aus"
    if not (List.isEmpty splits) then
        match mkSplits total splits with
        | Ok _ -> ()
        | Error err -> invalidOp $"{eventName}: Splits verletzen die mkSplits-Invarianten ({err})"

/// Faltet ein Ereignis auf den Zustand einer Buchung. Rein, ohne I/O.
///
/// Eine Erstbeobachtung erzeugt die Buchung; jedes andere Ereignis setzt eine
/// vorhandene Buchung voraus. Ein Aenderungs-Ereignis ohne vorherige Buchung ist ein
/// kaputtes Log und wird laut, nicht still, abgelehnt. Genau eines der drei
/// Entstehungs-Ereignisse (`EntryObservedInYnab`, `EntryImportedFromComdirect`,
/// `CashExpenseAdded`) darf die erste Zeile sein; ein zweites Entstehungs-Ereignis auf
/// derselben Buchung schlaegt ebenso laut fehl.
let apply (existing: Entry option) (event: LedgerEvent) : Entry =
    let requireExisting () =
        match existing with
        | Some e -> e
        | None ->
            let (EntryId id) = event.EntryId
            invalidOp
                $"Ereignis '{eventTypeName event.Body}' fuer Buchung {id} ohne vorangehende Erstbeobachtung"

    if isCreationEvent event.Body && existing.IsSome then
        let (EntryId id) = event.EntryId
        invalidOp
            $"zweites Entstehungs-Ereignis '{eventTypeName event.Body}' fuer Buchung {id}: eine Buchung entsteht genau einmal"

    match event.Body with
    | EntryObservedInYnab p ->
        {
            Id = event.EntryId
            AccountId = p.AccountId
            Date = p.Date
            Amount = p.Amount
            Payee = p.Payee
            Memo = p.Memo
            Category = p.Category
            Splits = p.Splits
            Cleared = p.Cleared
            TransferEntryId = None
            DeletedInYnab = false
        }

    | EntryDetailsChangeObservedInYnab p ->
        let e = requireExisting ()
        { e with
            Date = p.NewDate
            Amount = p.NewAmount
            Payee = p.NewPayee
            Memo = p.NewMemo
            AccountId = p.NewAccountId }

    | EntryCategorizationChangeObservedInYnab p ->
        let e = requireExisting ()
        { e with Category = p.NewCategory; Splits = p.NewSplits }

    | EntryClearedStateChangeObservedInYnab p ->
        let e = requireExisting ()
        { e with Cleared = p.NewCleared }

    | EntryDeletionObservedInYnab ->
        let e = requireExisting ()
        { e with DeletedInYnab = true }

    | EntryImportedFromComdirect p ->
        {
            Id = event.EntryId
            AccountId = p.AccountId
            Date = p.Date
            Amount = p.Amount
            Payee = p.Payee
            Memo = p.Memo
            // Kategorielos: die Kategorisierung folgt als eigenes Ereignis (Regel oder
            // Roman) in derselben Sequenz.
            Category = None
            Splits = []
            Cleared = p.Cleared
            TransferEntryId = None
            DeletedInYnab = false
        }

    | EntryCategorizedByRule p ->
        let e = requireExisting ()
        if e.Category.IsSome || not (List.isEmpty e.Splits) then
            let (EntryId id) = event.EntryId
            invalidOp $"EntryCategorizedByRule auf bereits kategorisierter Buchung {id}"
        requireCategoryXorSplits "EntryCategorizedByRule" e.Amount p.Category p.Splits
        { e with
            Category = p.Category
            Splits = p.Splits
            Payee = p.PayeeOverride |> Option.orElse e.Payee }

    | EntryRecategorized p ->
        let e = requireExisting ()
        requireCategoryXorSplits "EntryRecategorized" e.Amount p.NewCategory p.NewSplits
        { e with
            Category = p.NewCategory
            Splits = p.NewSplits
            Payee = p.NewPayee }

    | CashExpenseAdded p ->
        {
            Id = event.EntryId
            AccountId = p.AccountId
            Date = p.Date
            Amount = p.Amount
            Payee = p.Payee
            Memo = p.Memo
            Category = p.Category
            Splits = []
            Cleared = p.Cleared
            TransferEntryId = None
            DeletedInYnab = false
        }

    | EntryLinkedToYnab _ ->
        // Die YNAB-Verknuepfung lebt in der Fremd-Id-Tabelle (ADR 0015), nicht als Feld
        // auf `Entry`; `linksFrom` stiftet die Verknuepfung. `apply` selbst verlangt nur,
        // dass die Buchung schon entstanden ist.
        requireExisting ()

/// Die Fremd-Ids, die ein Ereignis mitbringt. Rein: der Store schreibt sie beim
/// Anhaengen mit, ohne selbst zu wissen, welches Ereignis welche Verknuepfung stiftet.
let linksFrom (event: LedgerEvent) : EntryLink list =
    match event.Body with
    | EntryObservedInYnab p ->
        [
            { EntryId = event.EntryId; System = YnabSystem; Value = p.YnabTransactionId }
            match p.ImportId with
            | Some importId ->
                { EntryId = event.EntryId; System = ImportIdSystem; Value = importId }
            | None -> ()
        ]
    | EntryImportedFromComdirect p ->
        [
            { EntryId = event.EntryId; System = ImportIdSystem; Value = p.ImportId }
            { EntryId = event.EntryId; System = ComdirectSystem; Value = p.ComdirectReference }
        ]
    | EntryLinkedToYnab p ->
        [ { EntryId = event.EntryId; System = YnabSystem; Value = p.YnabTransactionId } ]
    | _ -> []

/// Faltet eine ganze Historie zu einem Zustand. Die Ereignisse muessen in ihrer
/// gespeicherten Reihenfolge kommen.
let replay (events: LedgerEvent list) : Entry option =
    events |> List.fold (fun state ev -> Some (apply state ev)) None

// ============================================
// Beobachtung: Snapshot gegen Zustand (ADR 0013, Punkt 4)
// ============================================

/// Was der Spiegel dem reinen Vergleich mitgeben muss: welche Buchung gemeint ist,
/// auf welchem Kassenbuch-Konto sie liegt und wann beobachtet wurde. Das Suchen der
/// Buchung (Fremd-Id, ADR 0015), das Aufloesen des Kontos und die Uhr sind Arbeit des
/// Spiegel-Laufs; `observe` selbst bleibt rein.
type ObservationContext = {
    EntryId: EntryId
    AccountId: LedgerAccountId
    ObservedAt: DateTime
}

/// Vergleicht einen YNAB-Snapshot mit dem Zustand der Buchung und sagt, welche
/// Ereignisse daraus folgen. Rein, ohne I/O.
///
/// Die Trennlinie aus ADR 0013, Punkt 4: `ynab-sync` liefert Beobachtungen, der
/// `ledger` macht daraus semantische Delta-Ereignisse. Unveraenderte Daten ergeben
/// **kein** Ereignis (ADR 0015, Idempotenz); mehrere geaenderte Gruppen ergeben mehrere
/// Ereignisse, in der Reihenfolge Kopfdaten, Kategorisierung, Cleared.
let observe (ctx: ObservationContext) (existing: Entry option) (obs: YnabObservation) : LedgerEvent list =
    let event body = {
        EntryId = ctx.EntryId
        OccurredAt = ctx.ObservedAt
        Actor = YnabMirror
        Body = body
    }

    match existing, obs.Deleted with
    // In YNAB geloescht und im Kassenbuch nie gesehen: es gibt nichts zu erzaehlen.
    // Eine Buchung nur anzulegen, um sie sofort als geloescht zu markieren, waere
    // Rauschen ohne Aussage.
    | None, true -> []

    | Some entry, true ->
        if entry.DeletedInYnab then [] else [ event EntryDeletionObservedInYnab ]

    | None, false ->
        [
            event (
                EntryObservedInYnab {
                    AccountId = ctx.AccountId
                    YnabTransactionId = obs.YnabTransactionId
                    ImportId = obs.ImportId
                    Date = obs.Date
                    Amount = obs.Amount
                    Payee = obs.Payee
                    Memo = obs.Memo
                    Category = obs.Category
                    Splits = obs.Splits
                    Cleared = obs.Cleared
                    TransferAccountId = obs.TransferAccountId
                    TransferYnabTransactionId = obs.TransferYnabTransactionId
                })
        ]

    | Some entry, false ->
        let detailsChanged =
            entry.Date <> obs.Date
            || entry.Amount <> obs.Amount
            || entry.Payee <> obs.Payee
            || entry.Memo <> obs.Memo
            || entry.AccountId <> ctx.AccountId

        let categorizationChanged =
            entry.Category <> obs.Category || entry.Splits <> obs.Splits

        let clearedChanged = entry.Cleared <> obs.Cleared

        [
            if detailsChanged then
                event (
                    EntryDetailsChangeObservedInYnab {
                        OldDate = entry.Date
                        NewDate = obs.Date
                        OldAmount = entry.Amount
                        NewAmount = obs.Amount
                        OldPayee = entry.Payee
                        NewPayee = obs.Payee
                        OldMemo = entry.Memo
                        NewMemo = obs.Memo
                        OldAccountId = entry.AccountId
                        NewAccountId = ctx.AccountId
                    })

            if categorizationChanged then
                event (
                    EntryCategorizationChangeObservedInYnab {
                        OldCategory = entry.Category
                        NewCategory = obs.Category
                        OldSplits = entry.Splits
                        NewSplits = obs.Splits
                    })

            if clearedChanged then
                event (
                    EntryClearedStateChangeObservedInYnab {
                        OldCleared = entry.Cleared
                        NewCleared = obs.Cleared
                    })
        ]
