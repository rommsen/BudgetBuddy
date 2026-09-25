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
open Server.MemoFormatting
open Server.PayeeFormatting

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
    /// YNABs `approved` (ADR 0032): die eine Hälfte des Prüfvermerks neben
    /// `Cleared`. Bestandsereignisse ohne dieses Feld (Schema-Version 1) gelten als
    /// `true` (`Ledger.Events.Codec.decodeObservedV1` und Geschwister).
    Approved: bool
    /// YNABs `flag_color`/`flag_name`, roh (ADR 0032). Die Deutung (Rückfrage Lena,
    /// Markiert, kein Flag) ist Sache des Lesepfads (`Ledger.QueryHandlers.ReadModel`).
    FlagColor: string option
    FlagName: string option
}

/// Ist eine Buchung "zu prüfen"? Wahr, wenn sie in YNAB nicht genehmigt ist oder ihr
/// Cleared-Status noch offen ist (ADR 0032): Roman markiert eine Buchung erst als
/// geprüft, wenn beides erledigt ist; `Reconciled` zählt wie `Cleared` als geprüft.
let needsReview (entry: Entry) : bool =
    not entry.Approved || entry.Cleared = Uncleared

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
            Approved = p.Approved
            FlagColor = p.FlagColor
            FlagName = p.FlagName
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

    | EntryApprovalChangeObservedInYnab p ->
        let e = requireExisting ()
        { e with Approved = p.NewApproved }

    | EntryFlagChangeObservedInYnab p ->
        let e = requireExisting ()
        { e with FlagColor = p.NewFlagColor; FlagName = p.NewFlagName }

    | EntryTransferCounterpartObservedInYnab p ->
        // Kein zweites Entstehungs-Ereignis: eine unbekannte Buchung ist ein kaputtes
        // Log (`requireExisting`). Eine abweichende bestehende Verknuepfung wird NICHT
        // abgelehnt (das letzte Ereignis gewinnt): YNAB kann einen Uebertrag aufheben
        // und neu verbinden. Nur die Selbstverknuepfung ist laut abgelehnt, sie kann
        // nur aus einem Lookup-Fehler stammen. Idempotenz sitzt im Erzeuger (dem
        // Spiegel-Lauf), nicht hier: `apply` bleibt eine reine Faltung ohne
        // Sonderwissen ueber Wiederholungen.
        //
        // ledger-v5hkt: der Erzeuger (`linkTransferCounterpart`,
        // CommandHandlers.fs) fängt den Selbstbezug jetzt VOR dem Bau dieses
        // Ereignisses ab und überspringt ihn still. Trifft der `invalidOp` unten
        // trotzdem, ist das kein Datenfehler mehr, sondern ein Programmierfehler im
        // Erzeuger, und darf laut sein.
        let e = requireExisting ()
        if p.CounterpartEntryId = event.EntryId then
            let (EntryId id) = event.EntryId
            invalidOp $"Uebertrags-Verknuepfung fuer Buchung {id} zeigt auf sich selbst"
        { e with TransferEntryId = Some p.CounterpartEntryId }

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
            Approved = p.Approved
            FlagColor = None
            FlagName = None
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
            Approved = p.Approved
            FlagColor = None
            FlagName = None
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
// Import-Push: Kassenbuch-Ereignisse aus einer Comdirect-Buchung und ihrer
// Review-Spur bauen (ADR 0021, ledger-qvt84)
// ============================================

/// Was `recordComdirectPush` braucht, um die Ereignisfolge einer Comdirect-Buchung zu
/// bauen: die Rohdaten der Bank-Buchung (Kassenbuch-Konto, ImportId, Comdirect-
/// Reference, Datum, Betrag, Payee/Memo wie tatsächlich im YNAB-Request, plus der rohe
/// Bank-Payee als eigenes Feld, `RawPayee`, ledger-jkb0f) plus die Review-Spur
/// (`Shared.Domain.ReviewStep list`, categorization-gdrx8), älteste zuerst. `PushedAt`
/// ist der Zeitpunkt der Entstehung (`EntryImportedFromComdirect`); jeder Review-Schritt
/// behält seinen eigenen, ursprünglichen Zeitpunkt (`ReviewStep.At`), nicht `PushedAt`.
type ComdirectPushInput = {
    EntryId: EntryId
    AccountId: LedgerAccountId
    ImportId: string
    ComdirectReference: string
    Date: DateTime
    Amount: Money
    /// Der Payee, wie er tatsächlich im YNAB-Request steht (`PreparedTransaction.Request.PayeeName`,
    /// also mit einem zu diesem Zeitpunkt schon aktiven Override, falls einer gesetzt
    /// war). Das ist die Entstehung, kein roher Bankwert (ledger-jkb0f: dieser
    /// Kommentar behauptete das bisher fälschlich, siehe `RawPayee` für den echten
    /// Rohwert).
    Payee: string option
    /// Der rohe, von der Bank gelieferte Payee, unabhängig von jedem Override
    /// (`BankTransaction.Payee`). Fallback für `toEvent`, wenn ein Review-Schritt
    /// keinen Override (mehr) trägt: ohne diesen rohen Wert würde ein entfernter
    /// Override fälschlich auf den vorherigen Override zurückfallen statt auf die
    /// eigentliche Bankangabe (ledger-jkb0f).
    RawPayee: string option
    /// Das Memo, wie es tatsächlich im YNAB-Request steht.
    Memo: string option
    PushedAt: DateTime
    ReviewSteps: ReviewStep list
}

/// Baut aus einer Comdirect-Buchung und ihrer Review-Spur die geordnete Ereignisfolge,
/// mit der sie im Kassenbuch entsteht (Task-Notes, Schritt 6): `EntryImportedFromComdirect`
/// (Actor `ComdirectImport`, immer `Uncleared`: der Push selbst sendet nie einen
/// anderen Cleared-Status), danach je Review-Schritt ein Kategorisierungs-Ereignis mit
/// dem ursprünglichen Zeitpunkt aus `ReviewStep.At`:
///   - `ByRule` -> `EntryCategorizedByRule` (Actor `Rule`), RuleName kommt direkt aus
///     dem Schritt (`ReviewStep.By`), kein Nachschlagen nötig.
///   - `ByUser` -> `EntryRecategorized` (Actor `UserInBb`).
///   - `ByOrderIdMatch orderId` -> `EntryRecategorized` (Actor `OrderIdMatch orderId`):
///     dieselbe Kategorisierungs-Semantik wie eine Umkategorisierung, nur mit einem
///     anderen Urheber (Entscheidung Roman, 2026-09-11, siehe Task-Notes).
/// `Old*`-Felder der Recategorized-Ereignisse verfolgen den Stand des jeweils
/// vorherigen Schritts (leer/`None` für den ersten); `NewPayee` kommt aus
/// `Server.PayeeFormatting.resolvePayeeName`, derselben Funktion, die
/// `YnabClient.buildTransactionRequest` für `PayeeName` benutzt: `PayeeOverride`,
/// sonst der rohe Bank-Payee (`input.RawPayee`), sonst der feste YNAB-Text "Unknown",
/// nie der Override des vorherigen Schritts (ledger-jkb0f: sonst bliebe ein entfernter
/// Override fälschlich stehen, statt auf die Bankangabe zurückzufallen, und ohne den
/// gemeinsamen "Unknown"-Fallback wiche `NewPayee = None` von dem ab, was YNAB
/// tatsächlich sendet, sobald weder Override noch Bank-Payee existieren). Splits
/// übernehmen dieselbe Memo-Kürzung
/// (`Server.MemoFormatting.truncateSplitMemo`), die `YnabClient` beim Senden
/// tatsächlich anwendet, sonst zeigt das Ereignis ein ungekürztes Memo, das YNAB nie
/// bekommen hat. Kategorie-Namen kommen zuerst vom injizierten `resolveCategoryName`
/// (Muster wie der injizierte `push`-Parameter in `pushComdirectImports`), erst wenn
/// der keinen Namen kennt, fällt `categoryRefOf` auf `step.CategoryName` zurück; so
/// bekommt eine Buchung auch dann eine vollständige `CategoryRef`, wenn der
/// Review-Schritt selbst keinen (mehr aktuellen) Namen trägt.
/// Rein: kein I/O, keine Uhr, `resolveCategoryName` ist ein reiner Lookup gegen eine
/// vom Aufrufer schon geladene Map. Ob eine ImportId schon verknüpft ist (kein
/// zweites Entstehen) und das eigentliche Schreiben ist Sache des Aufrufers
/// (`Ledger.CommandHandlers`, ADR 0021 Punkt 3).
let recordComdirectPush
    (resolveCategoryName: YnabCategoryId -> string option)
    (input: ComdirectPushInput)
    : LedgerEvent list =
    let creation = {
        EntryId = input.EntryId
        OccurredAt = input.PushedAt
        Actor = ComdirectImport
        Body =
            EntryImportedFromComdirect {
                AccountId = input.AccountId
                ImportId = input.ImportId
                ComdirectReference = input.ComdirectReference
                Date = input.Date
                Amount = input.Amount
                Payee = input.Payee
                Memo = input.Memo
                Cleared = Uncleared
                // Der Push sendet kein `approved`; YNAB legt die Buchung unbestätigt
                // an (ADR 0032).
                Approved = false
            }
    }

    let categoryRefOf (step: ReviewStep) : CategoryRef option =
        match step.Category with
        | Some catId ->
            match resolveCategoryName catId |> Option.orElse step.CategoryName with
            | Some name -> Some { CategoryId = catId; CategoryName = name }
            | None -> None
        | None -> None

    // Roman kann eine Buchung erst aufteilen und danach über den Kategorie-Chip
    // "Aufgeteilt" eine Kategorie wählen; `categorizeTransaction` setzt dann `CategoryId`,
    // lässt `Splits` aber stehen (`src/Server/Api.fs`). Ein Review-Schritt kann also
    // Kategorie UND mindestens zwei Split-Zeilen zugleich tragen. `buildTransactionRequest`
    // (`YnabClient.fs`) sendet in diesem Fall nur die Splits, nie eine `category_id` auf
    // der Buchung selbst. Dieselbe Regel gilt hier: die Kassenbuch-Ereignisse müssen zu
    // dem passen, was YNAB tatsächlich bekommt, sonst lehnt `apply`s Kategorie/Splits-
    // Ausschluss (ADR 0006/0019) das Ereignis ab, obwohl genau dieser Zustand gepusht
    // wird (Verifier-Fund, Iteration 3).
    let resolveCategoryVsSplits (category: CategoryRef option) (splits: TransactionSplit list) =
        if splits.Length >= 2 then None, splits else category, splits

    let toEvent (prevCategory, prevSplits, prevPayee) (step: ReviewStep) =
        let rawSplits =
            step.Splits
            |> Option.defaultValue []
            |> List.map (fun split -> { split with Memo = split.Memo |> Option.map truncateSplitMemo })
        let category, splits = resolveCategoryVsSplits (categoryRefOf step) rawSplits
        // Dieselbe Auflösung wie `YnabClient.buildTransactionRequest` (`Server.PayeeFormatting.resolvePayeeName`),
        // nicht auf `prevPayee`: `step.PayeeOverride = None` heißt, DIESER Schritt hat
        // (mehr) keinen Override, nicht "unverändert gegenüber dem letzten Schritt"
        // (ledger-jkb0f). Der gemeinsame Resolver liefert auch den Fallback auf den
        // festen YNAB-Text "Unknown", wenn weder Override noch Bank-Payee existieren,
        // sonst würde ein Review-Schritt ohne Override und ohne Bank-Payee `NewPayee =
        // None` schreiben, obwohl YNAB tatsächlich "Unknown" bekommt (Verifier-Fund,
        // Iteration 1).
        let newPayee = Some (resolvePayeeName step.PayeeOverride input.RawPayee)

        let recategorizedBody actor =
            actor,
            EntryRecategorized {
                OldCategory = prevCategory
                NewCategory = category
                OldSplits = prevSplits
                NewSplits = splits
                OldPayee = prevPayee
                NewPayee = newPayee
            }

        let actor, body =
            match step.By with
            | ByRule (ruleId, ruleName) ->
                Rule ruleId,
                EntryCategorizedByRule {
                    RuleName = ruleName
                    Category = category
                    Splits = splits
                    PayeeOverride = step.PayeeOverride
                }
            | ByUser -> recategorizedBody UserInBb
            | ByOrderIdMatch orderId -> recategorizedBody (OrderIdMatch orderId)

        let event = { EntryId = input.EntryId; OccurredAt = step.At; Actor = actor; Body = body }
        event, (category, splits, newPayee)

    let stepEvents, _ =
        input.ReviewSteps
        |> List.fold
            (fun (events, state) step ->
                let event, newState = toEvent state step
                events @ [ event ], newState)
            ([], (None, [], input.Payee))

    creation :: stepEvents

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
                    Approved = obs.Approved
                    FlagColor = obs.FlagColor
                    FlagName = obs.FlagName
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

        let approvalChanged = entry.Approved <> obs.Approved

        let flagChanged = entry.FlagColor <> obs.FlagColor || entry.FlagName <> obs.FlagName

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

            if approvalChanged then
                event (
                    EntryApprovalChangeObservedInYnab {
                        OldApproved = entry.Approved
                        NewApproved = obs.Approved
                    })

            if flagChanged then
                event (
                    EntryFlagChangeObservedInYnab {
                        OldFlagColor = entry.FlagColor
                        OldFlagName = entry.FlagName
                        NewFlagColor = obs.FlagColor
                        NewFlagName = obs.FlagName
                    })
        ]
