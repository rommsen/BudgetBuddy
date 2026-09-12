/// Der Lesepfad des Kassenbuchs: die Sichten, aus denen die Kassenbuch-Seite besteht.
///
/// Zwei Haelften, bewusst getrennt:
/// * `ReadModel` ist **rein** — Zustand und Ereignisse hinein, Sichten und Saetze
///   heraus. Hier steht die Sprache der Historie (ADR 0013): ein Ereignis wird zu
///   einem Satz wie "Kategorie in YNAB geändert: Lebensmittel → Restaurant", nie zu
///   einem Typnamen. Diese Haelfte ist ohne Datenbank testbar.
/// * Die Query-Funktionen darunter holen den Zustand ueber `Ledger.Persistence` und
///   reichen ihn durch `ReadModel`. Gelesen wird die Zustandstabelle, nicht das Log —
///   das Log wird nur fuer die Historie einer einzelnen Buchung angefasst (ADR 0013).
///
/// Der Kontostand wird in F# summiert (`Shared.Domain.ledgerBalance`), nie per
/// SQL-`SUM`: die Betraege liegen als exakter Dezimal-Text (ADR 0017).
module Ledger.QueryHandlers

open System
open System.Globalization
open Shared.Domain
open Ledger.Events
open Ledger.Domain

/// Das Kern-Persistenz-Modul (Settings). Alias, weil der einfache Name `Persistence`
/// hier auf `Ledger.Persistence` zeigen wuerde.
module Core = global.Persistence

// ============================================
// Reine Abbildung: Zustand und Ereignisse -> Sichten
// ============================================

module ReadModel =

    /// Zahlen im Kassenbuch werden deutsch geschrieben. Feste Kultur statt der des
    /// Servers, damit ein Historien-Satz auf jeder Maschine gleich lautet.
    let private de = CultureInfo.GetCultureInfo "de-DE"

    let private formatMoney (m: Money) =
        $"""{m.Amount.ToString("N2", de)} {m.Currency}"""

    let private formatDate (d: DateTime) = d.ToString("dd.MM.yyyy", de)

    /// Die Herkunft hinter einem Actor (ADR 0013, Punkt 3).
    let originOf (actor: Actor) : LedgerOrigin =
        match actor with
        | YnabMirror -> MirroredFromYnab
        | ComdirectImport -> ImportedFromComdirect
        | Rule _ -> DecidedByRule
        | UserInBb -> DecidedInBb
        | QuickAdd -> AddedViaQuickAdd
        | OrderIdMatch _ -> MatchedByOrderId

    let private accountKindOf (kind: AccountKind) : LedgerAccountKindView =
        match kind with
        | BankAccount -> LedgerBankAccount
        | CashAccount -> LedgerCashAccount
        | SecuritiesAccount -> LedgerSecuritiesAccount

    /// Ein Konto als Sicht. Die Verknuepfung (ADR 0016) wird auf die YNAB-Id
    /// heruntergebrochen; ein BB-eigenes Konto hat schlicht keine.
    let toAccountView (account: LedgerAccount) : LedgerAccountView =
        let (LedgerAccountId id) = account.Id
        {
            Id = id
            Name = account.Name
            Kind = accountKindOf account.Kind
            YnabAccountId =
                match account.Link with
                | YnabMirrored ynabId -> Some ynabId
                | BbOwned -> None
            Closed = account.Closed
        }

    /// Welches Konto die Seite ohne Zutun zeigt: das gespiegelte YNAB-Standardkonto,
    /// sonst das erste. Ohne Konten gibt es nichts vorzuwaehlen.
    let defaultAccountId
        (ynabDefault: YnabAccountId option)
        (accounts: LedgerAccountView list)
        : Guid option =
        let mirrored =
            ynabDefault
            |> Option.bind (fun wanted ->
                accounts |> List.tryFind (fun a -> a.YnabAccountId = Some wanted))

        match mirrored with
        | Some account -> Some account.Id
        | None -> accounts |> List.tryHead |> Option.map (fun a -> a.Id)

    /// Eine Buchung als Sicht. Die Herkunft kommt von aussen: sie steckt im Actor des
    /// ersten Ereignisses, nicht im Zustand.
    let toEntryView (origin: LedgerOrigin) (entry: Entry) : LedgerEntryView =
        let (EntryId id) = entry.Id
        let (LedgerAccountId accountId) = entry.AccountId
        {
            Id = id
            AccountId = accountId
            Date = entry.Date
            Amount = entry.Amount
            Payee = entry.Payee
            Memo = entry.Memo
            CategoryName = entry.Category |> Option.map (fun c -> c.CategoryName)
            Splits = entry.Splits
            Origin = origin
            DeletedInYnab = entry.DeletedInYnab
        }

    // --- Die Sprache der Historie ---

    let private payeeOr (p: string option) = p |> Option.defaultValue "ohne Payee"
    let private memoOr (m: string option) = m |> Option.defaultValue "ohne Memo"

    let private clearedLabel (c: ClearedState) =
        match c with
        | Uncleared -> "offen"
        | Cleared -> "bestätigt"
        | Reconciled -> "abgeglichen"

    let private describeObserved (p: EntryObservedInYnabPayload) =
        let head = $"Erstmals in YNAB gesehen: {payeeOr p.Payee}, {formatMoney p.Amount}"
        let category =
            match p.Category with
            | Some c -> [ $"Kategorie {c.CategoryName}" ]
            | None -> []
        let splits =
            if List.isEmpty p.Splits then [] else [ $"{List.length p.Splits} Split-Zeilen" ]
        String.Join(", ", head :: category @ splits)

    let private describeDetailsChange (p: EntryDetailsChangeObservedInYnabPayload) =
        let parts =
            [
                if p.OldDate <> p.NewDate then
                    $"Datum in YNAB geändert: {formatDate p.OldDate} → {formatDate p.NewDate}"
                if p.OldAmount <> p.NewAmount then
                    $"Betrag in YNAB geändert: {formatMoney p.OldAmount} → {formatMoney p.NewAmount}"
                if p.OldPayee <> p.NewPayee then
                    $"Payee in YNAB geändert: {payeeOr p.OldPayee} → {payeeOr p.NewPayee}"
                if p.OldMemo <> p.NewMemo then
                    $"Memo in YNAB geändert: {memoOr p.OldMemo} → {memoOr p.NewMemo}"
                if p.OldAccountId <> p.NewAccountId then
                    "Buchung in YNAB auf ein anderes Konto verschoben"
            ]
        // Ein Kopfdaten-Ereignis ohne sichtbaren Unterschied kann es nach `observe`
        // nicht geben; falls doch, bleibt der Satz allgemein statt leer.
        if List.isEmpty parts then "Kopfdaten in YNAB geändert" else String.Join("; ", parts)

    let private describeCategorizationChange (p: EntryCategorizationChangeObservedInYnabPayload) =
        let categoryPart =
            match p.OldCategory, p.NewCategory with
            | Some o, Some n when o.CategoryName <> n.CategoryName || o.CategoryId <> n.CategoryId ->
                [ $"Kategorie in YNAB geändert: {o.CategoryName} → {n.CategoryName}" ]
            | None, Some n -> [ $"Kategorie in YNAB gesetzt: {n.CategoryName}" ]
            | Some o, None -> [ $"Kategorie in YNAB entfernt: {o.CategoryName}" ]
            | _ -> []

        let splitPart =
            match p.OldSplits, p.NewSplits with
            | [], [] -> []
            | [], newSplits -> [ $"In YNAB in {List.length newSplits} Split-Zeilen aufgeteilt" ]
            | _, [] -> [ "Split in YNAB aufgehoben" ]
            | oldSplits, newSplits when oldSplits <> newSplits ->
                [ $"Splits in YNAB geändert: {List.length oldSplits} → {List.length newSplits} Zeilen" ]
            | _ -> []

        let parts = categoryPart @ splitPart
        if List.isEmpty parts then "Kategorisierung in YNAB geändert" else String.Join("; ", parts)

    let private describeImportedFromComdirect (p: EntryImportedFromComdirectPayload) =
        $"Comdirect-Import angelegt: {payeeOr p.Payee}, {formatMoney p.Amount}"

    let private describeCategorizedByRule (p: EntryCategorizedByRulePayload) =
        let target =
            match p.Category with
            | Some c -> $"Kategorie {c.CategoryName}"
            | None when not (List.isEmpty p.Splits) -> $"{List.length p.Splits} Split-Zeilen"
            | None -> "ohne Kategorie"
        $"""Regel „{p.RuleName}“ hat kategorisiert: {target}"""

    let private describeRecategorized (p: EntryRecategorizedPayload) =
        let categoryPart =
            match p.OldCategory, p.NewCategory with
            | Some o, Some n when o.CategoryName <> n.CategoryName || o.CategoryId <> n.CategoryId ->
                [ $"Kategorie geändert: {o.CategoryName} → {n.CategoryName}" ]
            | None, Some n -> [ $"Kategorie gesetzt: {n.CategoryName}" ]
            | Some o, None -> [ $"Kategorie entfernt: {o.CategoryName}" ]
            | _ -> []

        let splitPart =
            match p.OldSplits, p.NewSplits with
            | [], [] -> []
            | [], newSplits -> [ $"In {List.length newSplits} Split-Zeilen aufgeteilt" ]
            | _, [] -> [ "Split aufgehoben" ]
            | oldSplits, newSplits when oldSplits <> newSplits ->
                [ $"Splits geändert: {List.length oldSplits} → {List.length newSplits} Zeilen" ]
            | _ -> []

        let parts = categoryPart @ splitPart
        if List.isEmpty parts then
            "In BB umkategorisiert"
        else
            $"""In BB umkategorisiert: {String.Join("; ", parts)}"""

    let private describeCashExpenseAdded (p: CashExpenseAddedPayload) =
        let category =
            match p.Category with
            | Some c -> $", Kategorie {c.CategoryName}"
            | None -> ""
        $"Quick Add erfasst: {payeeOr p.Payee}, {formatMoney p.Amount}{category}"

    let private describeLinkedToYnab (p: EntryLinkedToYnabPayload) =
        $"Mit YNAB verknüpft: {p.YnabTransactionId}"

    /// Ein Ereignis als Satz in der Sprache des Kassenbuchs (ADR 0013). Die Payloads
    /// tragen die alten und neuen **Namen**, deshalb braucht dieser Satz kein
    /// Nachschlagen.
    let describe (body: LedgerEventBody) : string =
        match body with
        | EntryObservedInYnab p -> describeObserved p
        | EntryDetailsChangeObservedInYnab p -> describeDetailsChange p
        | EntryCategorizationChangeObservedInYnab p -> describeCategorizationChange p
        | EntryClearedStateChangeObservedInYnab p ->
            $"Status in YNAB geändert: {clearedLabel p.OldCleared} → {clearedLabel p.NewCleared}"
        | EntryDeletionObservedInYnab -> "In YNAB gelöscht"
        | EntryImportedFromComdirect p -> describeImportedFromComdirect p
        | EntryCategorizedByRule p -> describeCategorizedByRule p
        | EntryRecategorized p -> describeRecategorized p
        | CashExpenseAdded p -> describeCashExpenseAdded p
        | EntryLinkedToYnab p -> describeLinkedToYnab p

    /// Ein gespeichertes Ereignis als Historien-Zeile.
    let toHistoryLine (recorded: RecordedLedgerEvent) : LedgerHistoryLine = {
        Sequence = recorded.Sequence
        OccurredAt = recorded.Event.OccurredAt
        Origin = originOf recorded.Event.Actor
        Description = describe recorded.Event.Body
    }

// ============================================
// Sichten (mit I/O)
// ============================================

/// Die Konten des Kassenbuchs samt Vorauswahl. Vorausgewaehlt ist das Konto, das dem
/// YNAB-Standardkonto entspricht (`ynab_default_account_id`), sonst das erste.
let getLedgerAccounts () : Async<LedgerAccountsView> =
    async {
        let! accounts = Ledger.Persistence.loadAccounts ()
        let! defaultAccountSetting = Core.Settings.getSetting "ynab_default_account_id"

        let ynabDefault =
            defaultAccountSetting
            |> Option.bind (fun text ->
                match Guid.TryParse text with
                | true, id -> Some (YnabAccountId id)
                | false, _ -> None)

        let views = accounts |> List.map ReadModel.toAccountView

        return {
            Accounts = views
            DefaultAccountId = ReadModel.defaultAccountId ynabDefault views
        }
    }

/// Die Buchungen eines Kontos, neueste zuerst. Geloeschte Buchungen bleiben in der
/// Liste (mit Kennzeichen) — das Kassenbuch verliert nichts (ADR 0013).
let getLedgerEntriesForAccount (accountId: Guid) : Async<LedgerEntryView list> =
    async {
        let! entries = Ledger.Persistence.loadEntriesForAccount (LedgerAccountId accountId)
        let! origins = Ledger.Persistence.loadEntryOrigins ()

        return
            entries
            |> List.map (fun entry ->
                let (EntryId id) = entry.Id
                // Ohne Ereignis im Log gibt es keine Herkunft zu zeigen; der Spiegel
                // ist der einzige Erzeuger in v1, also ist er die ehrliche Annahme.
                let origin =
                    origins
                    |> Map.tryFind id
                    |> Option.map ReadModel.originOf
                    |> Option.defaultValue MirroredFromYnab

                ReadModel.toEntryView origin entry)
    }

/// Eine Seite der Buchungsliste (ledger-b1xwe): ein Konto (`query.AccountId`) oder,
/// wenn `None`, die Gesamtsicht ueber alle Konten. Die Summe deckt den gesamten
/// Treffer ab, nicht die geladene Seite (ADR 0017, AC1) — dafuer laedt diese Funktion
/// zusaetzlich zur Seite alle Betraege des Treffers und summiert in F#.
let getLedgerEntriesPage (query: LedgerEntryPageQuery) : Async<LedgerEntryPage> =
    async {
        let accountId = query.AccountId |> Option.map LedgerAccountId
        let pageSize = max 1 query.PageSize
        let pageIndex = max 0 query.Page

        let! pageEntries = Ledger.Persistence.loadEntriesPage accountId pageIndex pageSize
        let! totalCount = Ledger.Persistence.countEntries accountId
        let! totalAmounts = Ledger.Persistence.loadAmountsForQuery accountId
        let! origins = Ledger.Persistence.loadEntryOrigins ()

        let views =
            pageEntries
            |> List.map (fun entry ->
                let (EntryId id) = entry.Id
                let origin =
                    origins |> Map.tryFind id |> Option.map ReadModel.originOf |> Option.defaultValue MirroredFromYnab
                ReadModel.toEntryView origin entry)

        return {
            Entries = views
            TotalCount = totalCount
            TotalAmount = sumMoney "EUR" totalAmounts
        }
    }

/// Die Kontostaende aller Konten in einem Durchlauf (ledger-b1xwe AC2): unabhaengig
/// davon, welche Seite der Buchungsliste gerade geladen ist, weil sie ueber alle
/// gespeicherten Buchungen gebildet werden, nicht ueber den geladenen Ausschnitt.
let getLedgerAccountBalances () : Async<LedgerAccountBalanceView list> =
    async {
        let! accounts = Ledger.Persistence.loadAccounts ()
        let! raw = Ledger.Persistence.loadAmountsForBalances ()

        let inputs =
            raw
            |> List.map (fun (accountId, amount, deleted) ->
                { AccountId = accountId; Amount = amount; DeletedInYnab = deleted })

        let balances = ledgerBalancesByAccount "EUR" inputs

        return
            accounts
            |> List.map (fun account ->
                let (LedgerAccountId id) = account.Id
                {
                    AccountId = id
                    Balance = balances |> Map.tryFind id |> Option.defaultValue { Amount = 0m; Currency = "EUR" }
                })
    }

/// Die Historie einer Buchung, aelteste Zeile zuerst. Das ist die eine Stelle, an der
/// die Seite das Ereignis-Log liest.
let getEntryHistory (entryId: Guid) : Async<LedgerHistoryLine list> =
    async {
        let! events = Ledger.Persistence.loadEventsForEntry (EntryId entryId)
        return events |> List.map ReadModel.toHistoryLine
    }
