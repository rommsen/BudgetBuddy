/// Modell und Nachrichten der Kassenbuch-Seite (ledger-q5w9c, seitenweise Liste und
/// Kontostaende-Leiste ledger-b1xwe).
///
/// Die Seite liest nur: Kontostaende, Buchungen seitenweise (ein Konto oder alle),
/// die Historie einer Buchung und den Status des letzten Spiegel-Laufs. Geschrieben
/// wird über den manuellen Auslöser "Jetzt spiegeln" (ADR 0014, Punkt 3) und über das
/// Frische-Kriterium (ADR 0014, Punkt 2, ledger-h4p6c): lädt die Seite einen zu alten
/// Lauf-Status (Seitenaufruf oder PWA-Vordergrund), stößt sie denselben Hintergrund-
/// Lauf an, den auch "Jetzt spiegeln" nutzt. Der Lauf nach Push gehört nicht hierher:
/// er passiert serverseitig (`Ledger.CommandHandlers.triggerMirrorInBackground`), ohne
/// dass diese Seite geöffnet sein muss.
module Components.Ledger.Types

open System
open Shared.Domain
open Types

/// Wie viele Buchungen eine Seite traegt (AC5, ledger-b1xwe). Ein Modul-Wert statt
/// eines Literals: `RunLoadEntriesPage` und die Tests teilen sich diese eine Zahl.
let pageSize = 100

/// Der Konto-Filter der Liste: "Alle Konten" ist ein eigener Eintrag, kein Schalter
/// (Entscheidung bei der Verfeinerung, siehe Task-Notes) — er traegt die Summe aller
/// Konten und steht immer zuerst in der Kontostaende-Leiste.
type AccountFilter =
    | AllAccounts
    | OneAccount of Guid

// ============================================
// Filterleiste und Summenzeile (ledger-j8xq4)
// ============================================
// Die fünf Such-Filter aus `ledger-vgfw3` (ADR 0036) bekommen hier ihre Bedienung:
// Chips, Kategorienauswahl mit Mehrfachauswahl (Click-Commit, ADR 0005/Styleguide
// 7.1), Zeitraum-Schnellwahl, Payee-Vorschläge, Memo- und Betrags-Suchfelder.

/// Die Schnellwahl der Zeitraum-Auswahl (Handoff Screen 2.5/2.12).
type DateQuick =
    | ThisMonth
    | LastMonth
    | ThisYear
    | Last12Months

/// Kurzform der Schnellwahl für den Chip ("12 Monate", Handoff-Tabelle
/// "Chip-Beschriftung im Aktivzustand").
let dateQuickLabel (quick: DateQuick) : string =
    match quick with
    | ThisMonth -> "Dieser Monat"
    | LastMonth -> "Letzter Monat"
    | ThisYear -> "Dieses Jahr"
    | Last12Months -> "12 Monate"

/// Reine Entscheidung: welchen Von/Bis-Bereich eine Schnellwahl einträgt (Handoff
/// 2.5: "Die Schnellwahl füllt Von/Bis mit"). `today` kommt von außen, damit die
/// Funktion ohne Systemzeit-Mocking testbar bleibt (wie `formatEntryDate`).
let quickDateRange (today: DateTime) (quick: DateQuick) : DateTime * DateTime =
    match quick with
    | ThisMonth -> DateTime(today.Year, today.Month, 1), today.Date
    | LastMonth ->
        let firstOfThisMonth = DateTime(today.Year, today.Month, 1)
        let lastOfLastMonth = firstOfThisMonth.AddDays(-1.0)
        DateTime(lastOfLastMonth.Year, lastOfLastMonth.Month, 1), lastOfLastMonth
    | ThisYear -> DateTime(today.Year, 1, 1), today.Date
    | Last12Months -> today.Date.AddMonths(-12), today.Date

/// Der Zeitraum-Filter im Entwurf wie im committeten Zustand: `Quick` ist gesetzt,
/// solange eine Schnellwahl zuletzt gewählt wurde (für den Chip-Kurztext); Von/Bis
/// von Hand geändert setzt `Quick` auf `None`, ohne Von/Bis zu löschen.
type DateRangeFilter = {
    Quick: DateQuick option
    From: DateTime option
    To: DateTime option
}

let emptyDateRange : DateRangeFilter = { Quick = None; From = None; To = None }

/// Der committete Zustand aller fünf Such-Filter (ADR 0036). Mehrfachauswahl bei
/// der Kategorie (Oder-Verknüpfung), sonst je Kriterium höchstens ein Wert.
type Filters = {
    CategoryIds: Guid Set
    DateRange: DateRangeFilter option
    Payee: string option
    Memo: string option
    AmountMin: decimal option
    AmountMax: decimal option
    /// Chip "Ohne Kategorie" (ADR 0048): reiner Toggle wie `OnlyNeedsReview`. Zählt
    /// nicht zu `hasAnyFilter` und bleibt von `ClearAllFilters` unberührt.
    WithoutCategory: bool
}

let noFilters : Filters = {
    CategoryIds = Set.empty
    DateRange = None
    Payee = None
    Memo = None
    AmountMin = None
    AmountMax = None
    WithoutCategory = false
}

/// Liest den Text eines Betrags-Suchfelds als `decimal option`: leer (nach
/// `Trim()`) ist `None`, sonst ein geparster Wert, deutsches Komma wie Punkt
/// gleichermaßen akzeptiert (Roman tippt üblicherweise Komma). Ein nicht lesbarer
/// Text (z. B. "abc") liefert ebenfalls `None`, statt eine Ausnahme zu werfen:
/// die Eingabe bleibt am Feld selbst sichtbar (`AmountMinDraft`/`AmountMaxDraft`),
/// nur der Filter bleibt dann unverändert.
let parseAmountFilterText (text: string) : decimal option =
    let trimmed = text.Trim()
    if trimmed = "" then
        None
    else
        match
            Decimal.TryParse(
                trimmed.Replace(",", "."),
                System.Globalization.NumberStyles.AllowDecimalPoint ||| System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture
            )
        with
        | true, value -> Some value
        | false, _ -> None

/// Der Anzeigetext eines geparsten Betrags im Suchfeld, wenn ein Popover neu
/// geöffnet wird (Rückrichtung von `parseAmountFilterText`). Immer mit Punkt
/// (Invariant Culture): reiner Bearbeitungstext, keine Geld-Anzeige (die
/// benutzt `Money.formatAmountText`).
let formatAmountDraft (value: decimal) : string =
    value.ToString(System.Globalization.CultureInfo.InvariantCulture)

let hasCategoryFilter (f: Filters) = not (Set.isEmpty f.CategoryIds)
let hasDateFilter (f: Filters) = f.DateRange.IsSome
let hasPayeeFilter (f: Filters) = f.Payee.IsSome
let hasMemoFilter (f: Filters) = f.Memo.IsSome
let hasAmountFilter (f: Filters) = f.AmountMin.IsSome || f.AmountMax.IsSome

/// Ist mindestens einer der fünf Such-Filter aktiv? Steuert, ob die Summenzeile
/// über der Liste steht (AC „Summenzeile erscheint genau dann..."). Der Chip
/// "Zu prüfen (n)" und der Schalter "Gelöschte zeigen" zählen bewusst nicht mit:
/// beide sind eigene, ältere Bedienelemente außerhalb dieser fünf Filter.
let hasAnyFilter (f: Filters) : bool =
    hasCategoryFilter f || hasDateFilter f || hasPayeeFilter f || hasMemoFilter f || hasAmountFilter f

/// Die Chips der Filterleiste, in fester Reihenfolge (PC, Handoff Screen 2.9).
/// `NeedsReviewChip` ist der bestehende Chip "Zu prüfen (n)" (ADR 0032): er zählt
/// zur Chip-Reihenfolge dazu (Handoff: "„Zu prüfen (7)" rutscht dahinter"), aber
/// nicht zu `hasAnyFilter`.
type ChipId =
    | NeedsReviewChip
    | CategoryChip
    | WithoutCategoryChip
    | DateChip
    | PayeeChip
    | MemoChip
    | AmountChip

let fixedChipOrder : ChipId list =
    [ NeedsReviewChip; CategoryChip; WithoutCategoryChip; DateChip; PayeeChip; MemoChip; AmountChip ]

/// Ob jeder Chip gerade aktiv ist: reiner Datensatz statt eines Funktionswerts,
/// damit `orderChipsForDisplay` unit-testbar bleibt, ohne ein Model zu bauen.
type ChipActivity = {
    NeedsReview: bool
    Category: bool
    WithoutCategory: bool
    Date: bool
    Payee: bool
    Memo: bool
    Amount: bool
}

let private isChipActive (activity: ChipActivity) (chip: ChipId) : bool =
    match chip with
    | NeedsReviewChip -> activity.NeedsReview
    | CategoryChip -> activity.Category
    | WithoutCategoryChip -> activity.WithoutCategory
    | DateChip -> activity.Date
    | PayeeChip -> activity.Payee
    | MemoChip -> activity.Memo
    | AmountChip -> activity.Amount

let chipActivityOf (onlyNeedsReview: bool) (filters: Filters) : ChipActivity = {
    NeedsReview = onlyNeedsReview
    Category = hasCategoryFilter filters
    WithoutCategory = filters.WithoutCategory
    Date = hasDateFilter filters
    Payee = hasPayeeFilter filters
    Memo = hasMemoFilter filters
    Amount = hasAmountFilter filters
}

/// Die Chip-Reihenfolge (AC „Am Handy wandern aktive Chips nach vorn"): am Handy
/// aktive Chips zuerst (in ihrer bisherigen Reihenfolge), dann die inaktiven, auch
/// in ihrer bisherigen Reihenfolge (stabile Partition). Am PC bleibt die feste
/// Reihenfolge unabhängig vom Zustand, nur die Farbe wechselt (Handoff).
let orderChipsForDisplay (isMobile: bool) (activity: ChipActivity) : ChipId list =
    if not isMobile then
        fixedChipOrder
    else
        let active, inactive = fixedChipOrder |> List.partition (isChipActive activity)
        active @ inactive

// ============================================
// Prüfvermerk an der Zeile (ledger-r777c, ADR 0050)
// ============================================

/// Der Zustand der Schaltfläche einer Zeile, solange ein Aufruf läuft oder der
/// Befehl selbst scheiterte. Ein offener Fehlschlag beim Zurückschreiben nach YNAB
/// gehört nicht hierher: er steht an der Buchung (`ReviewMarkWriteBackFailure`).
type ReviewMarkRowState =
    /// Der Befehl (Setzen oder Erneut senden) ist unterwegs.
    | ReviewMarkSending
    /// Der Befehl selbst scheiterte (unbekannte oder in YNAB gelöschte Buchung, Netz
    /// zum eigenen Server). Der Vermerk blieb unverändert.
    | ReviewMarkCommandFailed of message: string

/// Die Aktion der Schaltfläche einer Zeile: "Geprüft" für eine zu prüfende Buchung,
/// "Ungeprüft" für eine geprüfte. Rein, damit die Beschriftung ohne React testbar ist.
type ReviewMarkAction = {
    Label: string
    Target: ReviewMarkTarget
}

let reviewMarkActionFor (entry: LedgerEntryView) : ReviewMarkAction =
    if entry.NeedsReview then { Label = "Geprüft"; Target = Reviewed }
    else { Label = "Ungeprüft"; Target = Unreviewed }

/// Der Hinweis "Prüfvermerk nicht in YNAB" an einer Zeile (ADR 0050, Punkt 11).
type ReviewMarkHint = {
    Title: string
    /// Der Grund, wie der Server ihn lieferte, oder die Meldung des gescheiterten Befehls.
    Reason: string
    /// Die Beschriftung der Schaltfläche; `None`, wenn kein Erneut senden möglich ist
    /// (der Befehl selbst scheiterte, es gibt nichts Offenes zu senden).
    RetryLabel: string option
}

let reviewMarkHintTitle = "Prüfvermerk nicht in YNAB"

/// Welchen Hinweis trägt die Zeile? Ein Befehlsfehler (`ReviewMarkCommandFailed`) hat
/// Vorrang, danach der offene Fehlschlag an der Buchung. Während `ReviewMarkSending`
/// bleibt der bisherige Hinweis stehen.
let reviewMarkHintFor (rowState: ReviewMarkRowState option) (entry: LedgerEntryView) : ReviewMarkHint option =
    match rowState, entry.ReviewMarkWriteBackFailure with
    | Some (ReviewMarkCommandFailed message), failure ->
        // Scheiterte "Erneut senden" selbst, bleibt der offene Fehlschlag und mit ihm
        // die Schaltfläche stehen.
        Some {
            Title = "Prüfvermerk nicht gesetzt"
            Reason = message
            RetryLabel = failure |> Option.map (fun _ -> "Erneut senden")
        }
    | _, Some reason ->
        Some { Title = reviewMarkHintTitle; Reason = reason; RetryLabel = Some "Erneut senden" }
    | _, None -> None

type Model = {
    /// Die Konten samt Vorauswahl des Servers.
    Accounts: RemoteData<LedgerAccountsView>

    /// Die Kontostaende aller Konten in einem Aufruf (ledger-b1xwe AC2). Unabhaengig
    /// von `Entries`: sie kommen nie aus der geladenen Liste.
    Balances: RemoteData<LedgerAccountBalanceView list>

    /// Der gerade aktive Filter. `None`, solange nichts geladen ist oder das
    /// Kassenbuch leer ist (kein Konto zum Filtern).
    SelectedFilter: AccountFilter option

    /// Die bereits geladenen Buchungen des aktiven Filters, neueste zuerst, ueber
    /// alle bisher geladenen Seiten hinweg zusammengefuehrt. In YNAB geloeschte
    /// Buchungen sind enthalten und werden gekennzeichnet, nie herausgefiltert.
    Entries: RemoteData<LedgerEntryView list>

    /// Der Seitenzeiger der naechsten zu ladenden Seite (0-basiert).
    NextPage: int

    /// Zahl der Zeilen, die lokal aus den geladenen Seiten entfernt wurden, seit die Liste
    /// zuletzt neu angestoßen wurde (ledger-be1yh). Geht als `LocallyRemoved` in die
    /// Nachladeanfrage, damit der Server keine Buchung an der Seitengrenze überspringt.
    LocallyRemoved: int

    /// Die Gesamtzahl der Treffer des aktiven Filters, wie die zuletzt geladene Seite
    /// sie gemeldet hat.
    TotalCount: int

    /// Die Summe über den gesamten Treffer (nicht die geladene Seite), wie die
    /// zuletzt geladene Seite sie gemeldet hat (ledger-j8xq4, Summenzeile).
    TotalAmount: Money

    /// Gibt es noch weitere Seiten zu laden? Reine Ableitung aus geladener Anzahl
    /// gegen `TotalCount`, nach jeder Seite neu gesetzt.
    HasMoreEntries: bool

    /// Zeigt die Liste in YNAB gelöschte Buchungen mit (ledger-p3n8k)? Standardmäßig
    /// `false`: die Liste blendet sie aus, ihre Summe entspricht dann dem
    /// Kontostand. Geht in die nächste `getLedgerEntriesPage`-Abfrage ein.
    IncludeDeleted: bool

    /// Die Zahl der in YNAB gelöschten Buchungen im aktiven Filter, wie die zuletzt
    /// geladene Seite sie gemeldet hat (unabhängig von `IncludeDeleted`, AC2).
    DeletedCount: int

    /// Zeigt die Liste nur zu prüfende Buchungen (ADR 0032)? Standardmäßig `false`.
    /// Geht in die nächste `getLedgerEntriesPage`-Abfrage ein, wie `IncludeDeleted`.
    OnlyNeedsReview: bool

    /// Die Zahl der zu prüfenden Buchungen im aktiven Filter, wie die zuletzt
    /// geladene Seite sie gemeldet hat (unabhängig von `OnlyNeedsReview`).
    NeedsReviewCount: int

    /// Laeuft gerade das Nachladen einer weiteren Seite? Anders als `Entries = Loading`
    /// (Erstladen): waehrend des Nachladens bleibt die bisherige Liste stehen, nur ein
    /// Skeleton steht am Ende (AC5).
    LoadingMore: bool

    /// Die Buchung, deren Historie gerade im Sheet steht.
    HistoryFor: LedgerEntryView option

    /// Die Historie der offenen Buchung.
    History: RemoteData<LedgerHistoryLine list>

    /// Zeitpunkt und Ergebnis des zuletzt gelaufenen Spiegels.
    LastMirrorRun: RemoteData<MirrorRunStatus option>

    /// Laeuft gerade ein Spiegel-Lauf? Die Liste bleibt waehrenddessen stehen:
    /// Roman wartet nie auf den Spiegel (ADR 0014).
    MirrorRunning: bool

    // ============================================
    // Filterleiste und Summenzeile (ledger-j8xq4)
    // ============================================

    /// Alle YNAB-Kategorien für die Kategorienauswahl. Geladen einmal über den
    /// bestehenden `getCategories`-Weg (kein neuer Spiegel-Lauf, kein neuer
    /// Endpunkt, AC „Available je Kategorie"): der Aufruf braucht das konfigurierte
    /// YNAB-Budget, deshalb steckt er im Haupt-`State.fs` (wie `Rules`s
    /// `LoadCategories`), nicht hinter dem Fable.Remoting-Proxy dieses Moduls.
    Categories: RemoteData<YnabCategory list>

    /// Der committete Zustand aller fünf Such-Filter.
    Filters: Filters

    /// Welcher Filter-Chip gerade sein Popover/Sheet offen hat, `None` sonst. Nur
    /// einer zugleich (Handoff: "ein Muster für Kategorie, Zeitraum und Payee").
    OpenPopover: ChipId option

    /// Der Entwurf der Kategorien-Mehrfachauswahl, solange die Kategorienauswahl
    /// offen ist (Click-Commit, ADR 0005/Styleguide 7.1): erst „Übernehmen"
    /// schreibt ihn nach `Filters.CategoryIds`; „Esc"/Klick daneben/„Schließen"
    /// verwirft ihn kommentarlos, `Filters` bleibt unverändert.
    CategoryDraft: Guid Set
    CategorySearch: string
    HiddenCategoriesExpanded: bool

    /// Der Entwurf des Zeitraum-Filters, solange die Zeitraum-Auswahl offen ist.
    DateDraft: DateRangeFilter
    /// Ist die Eingabe in Von/Bis ungültig (z. B. "31.02.2026")? Solange eine Grenze
    /// ungültig ist, bleibt "Übernehmen" gesperrt.
    DateDraftFromInvalid: bool
    DateDraftToInvalid: bool

    /// Die Payee-Vorschläge des aktuellen Treffers (ohne den Payee-Filter selbst),
    /// für die Payee-Auswahl. Payee ist Einzelauswahl: ein Klick filtert sofort,
    /// kein Entwurf/Übernehmen nötig.
    PayeeSuggestions: RemoteData<LedgerPayeeSuggestion list>
    /// Der getippte Text, filtert `PayeeSuggestions` nur clientseitig (dieselbe
    /// Vorschlagsliste, keine neue Abfrage je Tastendruck).
    PayeeSearch: string

    /// Der Entwurf des Memo-Suchfelds; ein neuer Tastendruck erhöht `MemoVersion`
    /// und verwirft eine noch laufende, ältere Verzögerung (Debounce-Muster wie
    /// `SyncFlow.State`s `PendingPayeeVersions`, hier ohne Map: es gibt nur ein
    /// Memo-Feld je Seite, keins je Buchung).
    MemoDraft: string
    MemoVersion: int

    /// Der Entwurf des Betrags-Suchfelds (Von/Bis), mit derselben
    /// Debounce-Versionierung wie `MemoDraft`.
    AmountMinDraft: string
    AmountMaxDraft: string
    AmountVersion: int

    /// Der Zustand der Prüfvermerk-Schaltfläche je Buchung (ledger-r777c): läuft ein
    /// Aufruf oder scheiterte der Befehl. Zeilen ohne Eintrag sind im Ruhezustand.
    ReviewMarkRows: Map<Guid, ReviewMarkRowState>
}

/// Ist Von oder Bis des Zeitraum-Entwurfs ungültig eingegeben? Dann ist "Übernehmen"
/// gesperrt, und `ApplyDateFilter` ändert den Filter nicht.
let dateDraftHasError (model: Model) : bool =
    model.DateDraftFromInvalid || model.DateDraftToInvalid

/// Die Nachrichten der Seite.
///
/// Jeder Ausloeser, der den Fable.Remoting-Proxy anfasst, ist absichtlich in zwei
/// Nachrichten geteilt: eine **reine**, die nur den Zustand setzt, und eine
/// **unreine**, die den Aufruf absetzt. Damit ist der interessante Uebergang
/// testbar, ohne den Proxy anzufassen, der unter .NET nicht existiert.
type Msg =
    // Konten
    | LoadAccounts
    | AccountsLoaded of Result<LedgerAccountsView, string>

    // Kontostaende
    | LoadBalances
    | BalancesLoaded of Result<LedgerAccountBalanceView list, string>

    // Konto-Filter
    | SelectFilter of AccountFilter

    // Buchungen, seitenweise
    | LoadEntries
    | RunLoadEntriesPage of page: int
    | EntriesPageLoaded of page: int * Result<LedgerEntryPage, string>
    /// Der Beobachter am Listenende ist sichtbar geworden: naechste Seite nachladen,
    /// wenn gerade keine laeuft und es noch etwas zu holen gibt.
    | LoadMoreEntries
    /// Der Schalter "gelöschte Buchungen zeigen" wurde betätigt (ledger-p3n8k):
    /// kippt `IncludeDeleted` und lädt die Liste ab Seite 0 neu.
    | ToggleIncludeDeleted
    /// Der Chip "Zu prüfen (n)" wurde angetippt (ADR 0032): kippt `OnlyNeedsReview`
    /// und lädt die Liste ab Seite 0 neu. Derselbe rein/unrein-Schnitt wie
    /// `ToggleIncludeDeleted` (ADR 0020): der Proxy-Aufruf steckt hinter
    /// `RunLoadEntriesPage`, nicht in diesem Zweig.
    | ToggleOnlyNeedsReview
    /// Der Chip "Ohne Kategorie" wurde angetippt (ADR 0048): kippt
    /// `Filters.WithoutCategory` und lädt ab Seite 0 neu, wie `ToggleOnlyNeedsReview`.
    | ToggleWithoutCategory

    // Historie einer Buchung
    | OpenHistory of LedgerEntryView
    | CloseHistory
    | LoadHistory of Guid
    | HistoryLoaded of Result<LedgerHistoryLine list, string>

    // Spiegel
    | LoadMirrorStatus
    | MirrorStatusLoaded of Result<MirrorRunStatus option, string>
    /// "Jetzt spiegeln" gedrueckt: setzt den Hinweis, stoesst den Lauf an.
    | StartMirror
    | RunMirror
    | MirrorFinished of Result<MirrorRunSummary, string>

    // ============================================
    // Filterleiste und Summenzeile (ledger-j8xq4)
    // ============================================

    /// Die YNAB-Kategorien für die Kategorienauswahl laden (unrein: der Aufruf
    /// braucht das konfigurierte Budget und steckt deshalb im Haupt-`State.fs`,
    /// analog zu `Rules`s `LoadCategories`).
    | LoadCategories
    | CategoriesLoaded of Result<YnabCategory list, string>

    /// Ein Filter-Chip öffnet sein Popover/Sheet; setzt den passenden Entwurf aus
    /// dem committeten Zustand vor.
    | OpenFilterPopover of ChipId
    /// Esc, Klick daneben oder „Schließen": verwirft den Entwurf, `Filters` bleibt
    /// unverändert.
    | CloseFilterPopover

    | SetCategorySearch of string
    | ToggleCategoryDraft of Guid
    | ToggleHiddenCategoriesExpanded
    | ResetCategoryDraft
    | ApplyCategoryFilter
    | RemoveCategoryFilter

    | SetDateQuick of DateQuick
    | SetDateDraftFrom of Client.DesignSystem.DateField.DateEntry
    | SetDateDraftTo of Client.DesignSystem.DateField.DateEntry
    | ResetDateDraft
    | ApplyDateFilter
    | RemoveDateFilter

    | SetPayeeSearch of string
    | LoadPayeeSuggestions
    | PayeeSuggestionsLoaded of Result<LedgerPayeeSuggestion list, string>
    /// Einzelauswahl: filtert sofort (kein „Übernehmen").
    | SelectPayeeFilter of string
    | RemovePayeeFilter

    | SetMemoDraft of string
    | CommitMemoFilter of string * int
    | RemoveMemoFilter

    | SetAmountMinDraft of string
    | SetAmountMaxDraft of string
    | CommitAmountFilter of decimal option * decimal option * int
    | RemoveAmountFilter

    /// „Alle Filter löschen": setzt alle fünf Such-Filter zurück, lädt ab Seite 0
    /// neu. Der Chip "Zu prüfen (n)" und der Schalter "Gelöschte zeigen" bleiben
    /// unberührt, sie gehören nicht zu den fünf Filtern.
    | ClearAllFilters

    // ============================================
    // Prüfvermerk setzen und zurückschreiben (ledger-r777c, ADR 0050)
    // ============================================
    // Dieselbe Teilung wie bei den Filtern (ADR 0020): `Start...` ist rein und setzt
    // den Zeilenzustand, `Run...` ruft den Proxy auf.

    /// Klick auf "Geprüft" oder "Ungeprüft": Zeile auf "sendet", dann `RunSetReviewMark`.
    | StartSetReviewMark of Guid * ReviewMarkTarget
    | RunSetReviewMark of Guid * ReviewMarkTarget
    | ReviewMarkSet of Guid * ReviewMarkTarget * Result<ReviewMarkOutcome, string>
    /// Klick auf "Erneut senden" am Hinweis "Prüfvermerk nicht in YNAB".
    | StartRetryReviewMark of Guid
    | RunRetryReviewMark of Guid
    | ReviewMarkRetried of Guid * Result<ReviewMarkWriteBack, string>

/// Was die Seite nach oben meldet.
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType

/// Die Zusammenfassung eines Spiegel-Laufs als Satz.
let mirrorSummaryText (summary: MirrorRunSummary) : string =
    $"Spiegel-Lauf fertig: {summary.NewEntries} neu, {summary.ChangedEntries} geändert, {summary.DeletedEntries} gelöscht"

/// Wie lange ein Spiegel-Lauf gilt, bevor er beim Seitenaufruf oder beim PWA-Vordergrund
/// einen neuen Hintergrund-Lauf auslöst (ADR 0014, Frische-Kriterium).
let freshnessThreshold = TimeSpan.FromMinutes 5.0

/// Reine Entscheidung: ist der letzte Lauf älter als die Frische-Schwelle, oder gab es
/// noch nie einen? `now` kommt von außen (der Aufrufer trägt `DateTime.UtcNow`), damit
/// diese Funktion ohne Uhr testbar bleibt.
let isMirrorStale (now: DateTime) (lastRun: MirrorRunStatus option) : bool =
    match lastRun with
    | None -> true
    | Some status -> now - status.RanAt > freshnessThreshold
