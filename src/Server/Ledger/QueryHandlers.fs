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

    /// Zahlen im Kassenbuch werden deutsch geschrieben (Tausenderpunkt,
    /// Dezimalkomma). Das Format ist hier selbst gebaut, ohne Kulturdaten: der
    /// Server läuft im Alpine-Container ohne ICU, dort gibt es "de-DE" nicht und
    /// `CultureInfo.GetCultureInfo "de-DE"` wirft (Historie lieferte HTTP 500,
    /// 2026-10-20). So lautet ein Historien-Satz auf jeder Maschine gleich.
    let private germanNumbers =
        let n = NumberFormatInfo.InvariantInfo.Clone() :?> NumberFormatInfo
        n.NumberDecimalSeparator <- ","
        n.NumberGroupSeparator <- "."
        NumberFormatInfo.ReadOnly n

    let private formatMoney (m: Money) =
        $"""{m.Amount.ToString("N2", germanNumbers)} {m.Currency}"""

    let private formatDate (d: DateTime) = d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)

    /// Die Herkunft hinter einem Actor (ADR 0013, Punkt 3).
    let originOf (actor: Actor) : LedgerOrigin =
        match actor with
        | YnabMirror -> MirroredFromYnab
        | ComdirectImport -> ImportedFromComdirect
        | Rule _ -> DecidedByRule
        | UserInBb -> DecidedInBb
        | QuickAdd -> AddedViaQuickAdd
        | OrderIdMatch _ -> MatchedByOrderId

    /// Deutet Farbe und Name eines rohen YNAB-Flags (ADR 0032): der Name "Lena" (nach
    /// Trim, ohne Beachtung der Groß-/Kleinschreibung) ist *Rückfrage Lena*, jedes
    /// andere gesetzte Flag (auch nur eine Farbe ohne Namen) ist *Markiert*, sonst
    /// *kein Flag*. Rein, ohne I/O.
    let flagOf (flagColor: string option) (flagName: string option) : LedgerFlag =
        let isLena =
            match flagName with
            | Some name -> String.Equals(name.Trim(), "Lena", StringComparison.OrdinalIgnoreCase)
            | None -> false

        if isLena then AskLena
        elif flagColor.IsSome || flagName.IsSome then Marked
        else NoFlag

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
    /// ersten Ereignisses, nicht im Zustand. `categoryGroups` ist die Zuordnung
    /// Kategorie-Id -> Gruppenname (ADR 0039, ledger-h6ptn): einmal je Abfrage
    /// geladen (`Ledger.Persistence.loadCategoryGroups`), hier nur nachgeschlagen.
    /// Die Funktion bleibt rein: kein I/O, die Zuordnung kommt als Parameter herein.
    ///
    /// `categoryNames` ist die Zuordnung Kategorie-Id -> heutiger Name (ADR 0044,
    /// `Ledger.Persistence.loadCategoryNames`): Kopf und `ToCategory`-Split-Ziele werden
    /// live aufgelöst, mit Rückfall auf den gespeicherten Namen bei fehlendem oder leerem
    /// Treffer.
    let toEntryView
        (categoryNames: Map<Guid, string>)
        (categoryGroups: Map<Guid, string>)
        (origin: LedgerOrigin)
        (entry: Entry)
        : LedgerEntryView =
        let liveName categoryId (stored: string) =
            Ledger.Persistence.liveCategoryName categoryNames categoryId stored
        let (EntryId id) = entry.Id
        let (LedgerAccountId accountId) = entry.AccountId
        let categoryGroupName =
            entry.Category
            |> Option.bind (fun c -> Ledger.Persistence.categoryGroupOf categoryGroups c.CategoryId)
        let splitCategoryGroupNames =
            entry.Splits
            |> List.choose (fun s ->
                match s.Target with
                | ToCategory (categoryId, _) ->
                    Ledger.Persistence.categoryGroupOf categoryGroups categoryId
                    |> Option.filter (String.IsNullOrWhiteSpace >> not)
                    |> Option.map (fun group -> (let (YnabCategoryId id) = categoryId in id), group)
                | _ -> None)
            |> Map.ofList
        {
            Id = id
            AccountId = accountId
            Date = entry.Date
            Amount = entry.Amount
            Payee = entry.Payee
            Memo = entry.Memo
            CategoryName = entry.Category |> Option.map (fun c -> liveName c.CategoryId c.CategoryName)
            CategoryId = entry.Category |> Option.map (fun c -> let (YnabCategoryId id) = c.CategoryId in id)
            CategoryGroupName = categoryGroupName
            SplitCategoryGroupNames = splitCategoryGroupNames
            Splits =
                entry.Splits
                |> List.map (fun s ->
                    match s.Target with
                    | ToCategory (categoryId, name) ->
                        { s with Target = ToCategory (categoryId, liveName categoryId name) }
                    | _ -> s)
            Origin = origin
            DeletedInYnab = entry.DeletedInYnab
            NeedsReview = needsReview entry
            Flag = flagOf entry.FlagColor entry.FlagName
            IsTransfer = entry.TransferAccountId.IsSome || entry.TransferEntryId.IsSome
            ReviewMarkWriteBackFailure = entry.ReviewMarkWriteBackFailure
            CategorizationWriteBackFailure = entry.CategorizationWriteBackFailure |> Option.map (fun f -> f.Reason)
            LinkedToYnab = entry.LinkedToYnab
            PushToYnabFailure = entry.PushToYnabFailure
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
            | Some o, Some n when o.CategoryId <> n.CategoryId ->
                [ $"Kategorie in YNAB geändert: {o.CategoryName} → {n.CategoryName}" ]
            // Gleiche Id, anderer Name, Splits unverändert: ein vor ADR 0044 erzeugtes
            // Umbenennungs-Ereignis, ehrlich beschriftet statt als Umkategorisierung.
            | Some o, Some n
                when o.CategoryName <> n.CategoryName
                     && categorizationIdentity None p.OldSplits = categorizationIdentity None p.NewSplits ->
                [ $"Kategorie in YNAB umbenannt: {o.CategoryName} → {n.CategoryName}" ]
            | None, Some n -> [ $"Kategorie in YNAB gesetzt: {n.CategoryName}" ]
            | Some o, None -> [ $"Kategorie in YNAB entfernt: {o.CategoryName}" ]
            | _ -> []

        let splitPart =
            match p.OldSplits, p.NewSplits with
            | [], [] -> []
            | [], newSplits -> [ $"In YNAB in {List.length newSplits} Split-Zeilen aufgeteilt" ]
            | _, [] -> [ "Split in YNAB aufgehoben" ]
            | oldSplits, newSplits when
                categorizationIdentity None oldSplits <> categorizationIdentity None newSplits ->
                [ $"Splits in YNAB geändert: {List.length oldSplits} → {List.length newSplits} Zeilen" ]
            | _ -> []

        let parts = categoryPart @ splitPart
        if List.isEmpty parts then "Kategorisierung in YNAB geändert" else String.Join("; ", parts)

    /// Sätze für die Prüfvermerk-Beobachtungen (ADR 0032). Wortlaut aus dem Task
    /// festgelegt, nicht erfunden.
    let private describeApprovalChange (p: EntryApprovalChangeObservedInYnabPayload) =
        if p.NewApproved then "In YNAB genehmigt" else "Genehmigung in YNAB zurückgenommen"

    let private describeFlagChange (p: EntryFlagChangeObservedInYnabPayload) =
        match flagOf p.NewFlagColor p.NewFlagName with
        | AskLena -> "Flag „Lena“ in YNAB gesetzt"
        | Marked -> "Flag in YNAB gesetzt"
        | NoFlag -> "Flag in YNAB entfernt"

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

    /// Der Satz folgt aus Alt und Neu, nicht aus der Absicht der Geste (ADR 0050, Punkt 3).
    let private describeReviewMarkChanged (p: EntryReviewMarkChangedInBbPayload) =
        match p.OldApproved, p.NewApproved with
        | false, true -> "In BB als geprüft markiert"
        | true, false -> "In BB: Prüfvermerk entfernt"
        | _ -> $"In BB: Status geändert: {clearedLabel p.OldCleared} → {clearedLabel p.NewCleared}"

    let private describeLinkedToYnab (p: EntryLinkedToYnabPayload) =
        $"Mit YNAB verknüpft: {p.YnabTransactionId}"

    /// Die Payload traegt bewusst keinen Kontonamen der Gegenbuchung (nur ihre eigene
    /// Buchungs-Id und die YNAB-Transaction-Id, ueber die aufgeloest wurde, ledger-t7v2q
    /// Katalog-Eintrag): ein Nachschlagen des Namens wuerde `describe` unrein machen
    /// (I/O in einer reinen Funktion). Der Satz bleibt deshalb ohne Kontonamen.
    let private describeTransferCounterpart (_: EntryTransferCounterpartObservedInYnabPayload) =
        "Als Übertrag mit einer anderen Buchung verbunden"

    /// Der Betrag der Split-Zeile steht im Satz, weil zwei Übertrags-Zeilen derselben
    /// Buchung sich sonst in der Historie nicht unterscheiden lassen (ADR 0046).
    let private describeSplitTransferCounterpart
        (p: EntrySplitTransferCounterpartObservedInYnabPayload)
        =
        $"Split-Zeile über {formatMoney p.SplitAmount} als Übertrag mit einer anderen Buchung verbunden"

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
        | EntryApprovalChangeObservedInYnab p -> describeApprovalChange p
        | EntryFlagChangeObservedInYnab p -> describeFlagChange p
        | EntryTransferCounterpartObservedInYnab p -> describeTransferCounterpart p
        | EntrySplitTransferCounterpartObservedInYnab p -> describeSplitTransferCounterpart p
        | EntryDeletionObservedInYnab -> "In YNAB gelöscht"
        | EntryImportedFromComdirect p -> describeImportedFromComdirect p
        | EntryCategorizedByRule p -> describeCategorizedByRule p
        | EntryRecategorized p -> describeRecategorized p
        | CashExpenseAdded p -> describeCashExpenseAdded p
        | EntryLinkedToYnab p -> describeLinkedToYnab p
        | EntryReviewMarkChangedInBb p -> describeReviewMarkChanged p
        | EntryReviewMarkWriteBackFailed p -> $"Prüfvermerk nicht in YNAB: {p.Reason}"
        | EntryReviewMarkWrittenBackToYnab -> "Prüfvermerk in YNAB angekommen"
        | EntryCategorizationWriteBackFailed p -> $"Kategorie nicht in YNAB: {p.Reason}"
        | EntryCategorizationWrittenBackToYnab -> "Kategorie in YNAB angekommen"
        | EntryPushToYnabFailed p -> $"Push nach YNAB gescheitert: {p.Reason}"

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
        let! categoryGroups = Ledger.Persistence.loadCategoryGroups ()
        let! categoryNames = Ledger.Persistence.loadCategoryNames ()

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

                ReadModel.toEntryView categoryNames categoryGroups origin entry)
    }

/// Baut das `Ledger.Persistence.EntryFilter` aus den Such-Feldern einer
/// `LedgerEntryPageQuery` (ledger-vgfw3, ADR 0036) — die gemeinsame Grundlage fuer
/// `getLedgerEntriesPage` und `getLedgerPayeeSuggestions`, damit beide nie
/// auseinanderlaufen koennen.
let private toEntryFilter (query: LedgerEntryPageQuery) : Ledger.Persistence.EntryFilter =
    Ledger.Persistence.entryFilterOfPageQuery query

/// Eine Seite der Buchungsliste (ledger-b1xwe): ein Konto (`query.AccountId`) oder,
/// wenn `None`, die Gesamtsicht ueber alle Konten. Die Summe deckt den gesamten
/// Treffer ab, nicht die geladene Seite (ADR 0017, AC1) — dafuer laedt diese Funktion
/// zusaetzlich zur Seite alle Betraege des Treffers und summiert in F#.
///
/// `query.IncludeDeleted` (ledger-p3n8k): standardmäßig `false`, dann bleiben in
/// YNAB gelöschte Buchungen aus Seite, Trefferzahl und Summe außen vor, sodass die
/// sichtbare Liste sich zum Kontostand summiert. `DeletedCount` wird unabhängig
/// davon immer geliefert (AC2), damit die Seite "N gelöschte Buchungen
/// ausgeblendet" auch zeigen kann, während sie ausgeblendet sind.
///
/// `query.OnlyNeedsReview` (ADR 0032): standardmäßig `false`, dann bleiben nur zu
/// prüfende Buchungen (`approved = false` oder `cleared = Uncleared`) im Treffer.
/// `NeedsReviewCount` wird unabhängig davon immer geliefert, für den Chip
/// "Zu prüfen (n)": dasselbe Muster wie `DeletedCount`.
///
/// Die fünf Such-Filter (Kategorie/Zeitraum/Payee/Betrag/Memo, ledger-vgfw3, ADR
/// 0036) wirken zusätzlich auf Seite, Trefferzahl, Summe UND `DeletedCount` — sonst
/// zeigt die Summenzeile eine Summe, die nicht zur Trefferzahl gehört. Bewusst NICHT
/// auf `NeedsReviewCount`: der Chip "Zu prüfen (n)" ist ein fester Zähler über das
/// Konto, keine Trefferzahl der laufenden Suche.
///
/// `query.Sort` (ADR 0051) bestimmt nur die Reihenfolge der Seite (`loadEntriesPage`). Die
/// Sortierung ist kein Filter und geht in keinen Zählweg ein: Trefferzahl, Summe,
/// `DeletedCount` und `NeedsReviewCount` sind für jede Sortierung gleich.
let getLedgerEntriesPage (query: LedgerEntryPageQuery) : Async<LedgerEntryPage> =
    async {
        let accountId = query.AccountId |> Option.map LedgerAccountId
        let pageSize = max 1 query.PageSize
        let pageIndex = max 0 query.Page
        // Lokal entfernte Zeilen verschieben den Versatz nach vorn (ledger-be1yh).
        let offset = max 0 (pageIndex * pageSize - max 0 query.LocallyRemoved)
        let filter = toEntryFilter query

        let! pageEntries = Ledger.Persistence.loadEntriesPage accountId offset pageSize filter (LedgerSort.orDefault query.Sort)
        let! totalCount = Ledger.Persistence.countEntries accountId filter
        let! totalAmounts = Ledger.Persistence.loadAmountsForQuery accountId filter
        let! deletedCount = Ledger.Persistence.countDeletedEntries accountId filter
        let! needsReviewCount = Ledger.Persistence.countNeedsReviewEntries accountId
        let! origins = Ledger.Persistence.loadEntryOrigins ()
        let! categoryGroups = Ledger.Persistence.loadCategoryGroups ()
        let! categoryNames = Ledger.Persistence.loadCategoryNames ()

        let views =
            pageEntries
            |> List.map (fun entry ->
                let (EntryId id) = entry.Id
                let origin =
                    origins |> Map.tryFind id |> Option.map ReadModel.originOf |> Option.defaultValue MirroredFromYnab
                ReadModel.toEntryView categoryNames categoryGroups origin entry)

        return {
            Entries = views
            TotalCount = totalCount
            TotalAmount = sumMoney "EUR" totalAmounts
            DeletedCount = deletedCount
            NeedsReviewCount = needsReviewCount
        }
    }

/// Die Payees eines Filters samt ihrer Anzahl **innerhalb dieses Filters**
/// (ledger-vgfw3 AC, ADR 0036): Grundlage der Payee-Auswahl. `query` trägt nie
/// `query.Payee` selbst — der ist ja gerade das, was hier zur Auswahl steht.
let getLedgerPayeeSuggestions (query: LedgerPayeeSuggestionQuery) : Async<LedgerPayeeSuggestion list> =
    async {
        let accountId = query.AccountId |> Option.map LedgerAccountId
        let filter : Ledger.Persistence.EntryFilter = {
            IncludeDeleted = query.IncludeDeleted
            OnlyNeedsReview = query.OnlyNeedsReview
            WithoutCategory = query.WithoutCategory
            CategoryIds = query.CategoryIds
            DateFrom = query.DateFrom
            DateTo = query.DateTo
            Payee = None
            Memo = query.Memo
            AmountMin = query.AmountMin
            AmountMax = query.AmountMax
        }
        let! suggestions = Ledger.Persistence.loadPayeeSuggestions accountId filter
        return suggestions |> List.map (fun (payee, count) -> { Payee = payee; Count = count })
    }

/// Die Vorschläge zum Payee für den Kategorie-Picker (ledger-sfab0): höchstens drei
/// Kategorien, die der Payee bisher hatte, häufigste zuerst, ohne die aktuelle. Ein leerer
/// Payee ergibt keine Vorschläge.
let getPayeeCategorySuggestions (payee: string) (current: YnabCategoryId option) : Async<YnabCategoryId list> =
    async {
        if String.IsNullOrWhiteSpace payee then
            return []
        else
            let exclude = current |> Option.map (fun (YnabCategoryId id) -> id)
            let! ids = Ledger.Persistence.loadPayeeCategorySuggestions (payee.Trim()) exclude 3
            return ids |> List.map YnabCategoryId
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
