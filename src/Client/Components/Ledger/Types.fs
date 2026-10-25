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

/// Ist die Buchung mit diesem Prüfvermerk noch zu prüfen? Dieselbe Regel wie am Server
/// (`Ledger.Domain.needsReview`): nicht bestätigt oder noch offen (`Uncleared`).
let needsReviewOf (state: ReviewMarkState) : bool =
    not state.IsApproved || state.ClearedState = ReviewUncleared

/// Der Text des Toasts nach der Wisch-Geste: "<Payee> geprüft". Ohne Payee steht die
/// Anzeige der Liste ("ohne Payee") da.
let swipeReviewedToastText (payee: string option) : string =
    let name = payee |> Option.defaultValue "ohne Payee"
    $"{name} geprüft"

/// Die Beschriftung der Aktionsfläche hinter der Zeile beim Wischen nach rechts.
let swipeActionLabel = "Geprüft"

/// Die Beschriftung der Toast-Aktion.
let undoToastLabel = "Rückgängig"

/// Wirkt der Wisch auf diese Zeile? Nur eine zu prüfende, nicht in YNAB gelöschte Zeile
/// nimmt den Prüfvermerk an; auf einer geprüften Zeile bleibt es bei `NoChange`, ohne
/// Aufruf und ohne Toast.
let swipeAppliesTo (entry: LedgerEntryView) : bool =
    entry.NeedsReview && not entry.DeletedInYnab

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

// ============================================
// Kategorie ändern im Detail (ledger-sfab0, ADR 0055)
// ============================================

/// Eine Zeile der Karte "Budget nach dem Wechsel" (Tafel Ablauf 4, 4.3).
type CategoryChangeHintRow = { Label: string; OldText: string option; NewText: string option }

/// Der Zustand der Kategorie einer Zeile, solange ein Aufruf läuft oder der Befehl selbst
/// scheiterte. Ein offener Fehlschlag beim Zurückschreiben nach YNAB steht an der Buchung
/// (`CategorizationWriteBackFailure`), nicht hier.
type CategoryRowState =
    /// Der Befehl (Ändern, Rückgängig oder Erneut senden) ist unterwegs.
    | CategorySending
    /// Der Befehl selbst scheiterte (unbekannte oder in YNAB gelöschte Buchung, Netz zum
    /// eigenen Server). Die Kategorie blieb unverändert.
    | CategoryCommandFailed of message: string

/// Der Hinweis "Kategorie nicht in YNAB" im Detail (Muster `ReviewMarkHint`).
type CategoryHint = {
    Title: string
    Reason: string
    /// `None`, wenn nichts Offenes zu senden ist (der Befehl selbst scheiterte).
    RetryLabel: string option
}

let categoryHintTitle = "Kategorie nicht in YNAB"

/// Titel der Hinweisleiste, wenn das DELETE in YNAB gelang und die Neuanlage noch aussteht
/// (ADR 0058, ledger-pafkp).
let categoryReplacementTitle = "In YNAB gelöscht, Neuanlage offen"

/// Welchen Hinweis trägt die Buchung? Ein Befehlsfehler hat Vorrang, danach der offene
/// Fehlschlag an der Buchung. Rein, wie `reviewMarkHintFor`.
let categoryHintFor (rowState: CategoryRowState option) (entry: LedgerEntryView) : CategoryHint option =
    match rowState, entry.CategorizationWriteBackFailure with
    | Some (CategoryCommandFailed message), failure ->
        Some {
            Title = "Kategorie nicht geändert"
            Reason = message
            RetryLabel = failure |> Option.map (fun _ -> "Erneut senden")
        }
    | _, Some reason when entry.SplitLocks |> Option.exists (fun l -> l.ReplacementPending) ->
        Some { Title = categoryReplacementTitle; Reason = reason; RetryLabel = Some "Erneut senden" }
    | _, Some reason -> Some { Title = categoryHintTitle; Reason = reason; RetryLabel = Some "Erneut senden" }
    | _, None -> None

/// Wann der Pfeil an der Kategorie wirkt und wann nicht (ADR 0055 Punkt 7).
type CategoryLock =
    | CategoryEditable
    | CategoryLockedDeleted
    /// Übertrag zwischen Budget-Konten ohne Kategorie: YNAB lässt keine zu.
    | CategoryLockedTransfer
    /// Split-Buchung: der Pfeil öffnet den Split-Editor (ledger-pafkp).
    | CategoryOpensSplitEditor
    /// Split-Buchung, deren Zeilen sich nicht ändern lassen (abgeglichen, Tracking-Konto,
    /// fremde `import_id`): der Pfeil bleibt ohne Wirkung, der Grund steht im Tooltip.
    | CategoryLockedSplit of reason: string

let categoryLockFor (entry: LedgerEntryView) : CategoryLock =
    if entry.DeletedInYnab then CategoryLockedDeleted
    elif not (List.isEmpty entry.Splits) then
        match Components.Ledger.SplitEditor.lockReasonFor entry with
        | Some reason -> CategoryLockedSplit reason
        | None -> CategoryOpensSplitEditor
    elif entry.IsTransfer && entry.CategoryId.IsNone then CategoryLockedTransfer
    else CategoryEditable

/// Der Hinweistext am gesperrten Pfeil; `None`, wenn die Kategorie änderbar ist.
let categoryLockHint (lock: CategoryLock) : string option =
    match lock with
    | CategoryEditable -> None
    | CategoryLockedDeleted -> Some "In YNAB gelöscht, die Kategorie lässt sich nicht ändern"
    | CategoryLockedTransfer -> Some "Übertrag zwischen Budget-Konten, YNAB lässt hier keine Kategorie zu"
    | CategoryOpensSplitEditor -> None
    | CategoryLockedSplit reason -> Some reason

/// Die Beschriftung des Pfeils an der Kategorie (Tooltip und Screenreader).
let categoryArrowLabel = "Kategorie ändern"

/// Die Karte "Budget nach dem Wechsel" für genau eine Buchung. `Version` unterscheidet die
/// Karten nacheinander, damit ein später eintreffender Ablauf nur seine eigene schließt.
type CategoryChangeCardState = {
    EntryId: Guid
    Lines: CategoryChangeHintRow list
    Version: int
}

/// Wie lange die Karte steht (Millisekunden).
let categoryCardDurationMs = 6000

/// Passt die Buchung nach der Änderung noch zu den aktiven Kategorie-Filtern? Ohne
/// Kategorie-Filter und ohne Chip "Ohne Kategorie" passt sie immer. Mit beiden gilt Oder
/// (ADR 0048). "Ohne Kategorie" trifft keinen Übertrag und keinen Split.
let rowMatchesFilters (filters: Filters) (entry: LedgerEntryView) : bool =
    let hasCategoryFilter = not (Set.isEmpty filters.CategoryIds)
    if not hasCategoryFilter && not filters.WithoutCategory then
        true
    else
        let inCategories =
            hasCategoryFilter
            && (match entry.CategoryId with
                | Some id -> filters.CategoryIds.Contains id
                | None -> false)
        let withoutCategory =
            filters.WithoutCategory
            && entry.CategoryId.IsNone
            && not entry.IsTransfer
            && List.isEmpty entry.Splits
        inCategories || withoutCategory

/// Der Toast nach einer Änderung (ohne Beträge, die stehen in der Karte).
let categoryChangedToastText (newName: string option) (previous: CategoryChoice option) : string =
    match newName, previous with
    | Some name, _ -> $"Kategorie geändert: {name}"
    | None, Some old -> $"Kategorie entfernt: {old.CategoryName}"
    | None, None -> "Kategorie entfernt"

/// Der Toast nach "Rückgängig".
let categoryRestoredToastText (name: string option) : string =
    let label = name |> Option.defaultValue "ohne Kategorie"
    $"Kategorie zurückgesetzt: {label}"

/// Der Vermerk an einer Zeile, die nach einer Änderung nicht mehr zum Filter passt.
let filteredOutNote = "passt nicht mehr zum Filter"

/// Hinweisleiste über der Liste, wenn eine Änderung den Sortierschlüssel verschoben hat.
let orderStaleText = "Reihenfolge oder Treffer haben sich geändert."
let orderStaleActionLabel = "Neu laden"

// ============================================
// Auswahl mit Summe (ledger-r1d2f)
// ============================================

/// Die Auswahl ist eine Menge über den Treffer, keine Liste geladener Ids: `AllExcept`
/// meint den ganzen Treffer außer den genannten Zeilen, auch die noch nicht geladenen.
/// `Picked` kennt nur geladene Zeilen (man kann nur antippen, was man sieht). Abwählen
/// kann man ebenfalls nur geladene Zeilen, deshalb sind auch die Ausnahmen geladen.
type Selection =
    | NoSelection
    | Picked of Set<Guid>
    | AllExcept of Set<Guid>

/// Die Form des Buchungs-Details (ADR 0053 Punkt 1): ein Seitenpanel rechts neben der
/// Liste, oder ein BottomSheet über ihr (Handy, PC-Kompaktliste, und wo die Restbreite für
/// keine Tabellenstufe neben dem Panel reicht).
type DetailForm =
    | DetailPanel
    | DetailSheet

/// Wie die Liste bedient wird (ADR 0053 Punkt 6): die Breite wählt nur das Layout, die Gesten
/// folgen dem Eingabegerät. `TableRows` ist die Tabelle (ab `md`), `CompactMouse` die schmale
/// Kompaktliste mit Maus (Kästchen, Markierung, alle Tasten), `CompactTouch` die Kompaktliste
/// mit Finger (langes Drücken, Wisch). Die Ansicht misst und meldet es mit `ListInteractionChanged`.
type ListInteraction =
    | TableRows
    | CompactMouse
    | CompactTouch

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

    /// Die gewählte Sortierung der Liste (ledger-bn913, ADR 0051). Standard
    /// `LedgerSort.defaultSort`. Lebt nur im Client-Zustand, übersteht Konto- und
    /// Filterwechsel und wird nicht gespeichert. Gesendet und gezeigt wird
    /// `effectiveSort`, nicht dieses Feld unmittelbar.
    Sort: LedgerSort

    /// Ist das Sortier-Sheet am Handy offen (ledger-skpcx)? Es hat keinen Popover-Platz in
    /// `OpenPopover`, weil dieses nur Filter-Chips kennt.
    SortSheetOpen: bool

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

    /// Die Buchung, deren Detail gerade offen steht (Panel am PC, Sheet am Handy und in der
    /// Kompaktliste, ledger-q1hw2, ADR 0053). Eine Momentaufnahme: die Ansicht liest den
    /// aktuellen Stand aus der Liste, wenn die Buchung dort steht (`detailEntryOf`).
    DetailFor: LedgerEntryView option

    /// In welcher Form das Detail aufgeht. Die Ansicht misst die Tabellenbreite und meldet
    /// die Form mit `DetailFormChanged`; sie hängt nur von der Breite ohne Panel ab und
    /// kippt deshalb nicht hin und her, wenn das Panel selbst Platz nimmt.
    DetailForm: DetailForm

    /// Wie die Liste bei der gemessenen Breite und dem Eingabegerät bedient wird (ledger-m6zpd).
    /// Die Ansicht meldet es mit `ListInteractionChanged`; der Fußzeilen-Hinweis liest nur dieses Feld.
    ListInteraction: ListInteraction

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

    /// Zeitpunkt des letzten erfolgreichen `getCategories`-Abrufs (ledger-kwwxz,
    /// ADR 0055 Punkt 8). `None`, solange noch nie einer gelang. Ein Fehler beim
    /// Nachladen lässt ihn und `Categories` stehen.
    CategoriesLoadedAt: DateTime option

    /// Läuft gerade ein Abruf der Kategorien? Hält den alten Stand sichtbar
    /// (`Categories` bleibt `Success`) und verhindert einen zweiten Abruf.
    CategoriesRefreshing: bool

    /// `LoadCategories` kam, als die Settings noch nicht da waren (`NotAsked`,
    /// `Loading` oder `Failure`, ledger-k9tq7). `Categories` steht dann auf `Loading`;
    /// `SettingsLoaded (Ok …)` startet den Abruf, `SettingsLoaded (Error …)` macht
    /// `Categories` zu `Failure`.
    CategoriesAwaitSettings: bool

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

    /// Buchungen, die eine Wisch-Geste aus der Liste genommen hat (Chip "Zu prüfen"),
    /// samt ihrem Platz in der Liste (ledger-th36h). "Rückgängig" setzt sie dorthin
    /// zurück. `resetEntries` leert die Karte, eine neu geladene Liste kennt sie nicht mehr.
    SwipedAway: Map<Guid, LedgerEntryView * int>

    /// Die laufende Auswahl (ledger-r1d2f). Konto-, Filter- oder Chipwechsel leert sie,
    /// denn sie bezieht sich auf einen Treffer.
    Selection: Selection

    /// Der Anker der Shift-Klick-Spanne am PC: die zuletzt einfach angeklickte Zeile.
    SelectionAnchor: Guid option

    /// Ist der Auswahl-Modus am Handy an (Kästchen sichtbar, Leiste statt Tab-Bar)?
    /// Er beginnt mit langem Drücken und endet nur mit "Auswahl aufheben": auch mit
    /// leerer Auswahl bleibt er an.
    SelectionMode: bool

    /// Steht der Kategorie-Picker der Auswahl offen (ledger-w29y6)? Eigenes Feld, damit
    /// `CategoryPickerFor` dem Detail bleibt. Die Auswahl bleibt während der Wahl bestehen.
    BatchCategoryPickerOpen: bool

    /// Läuft gerade ein Sammel-Aufruf "Kategorie setzen" (oder seine Rücknahme)? Dann sind
    /// beide Sammelaktionen der Leiste gesperrt.
    CategoryBatchRunning: bool

    /// Die Fokuszeile der Tastaturnavigation am PC (ledger-rjb8w, `keyboardCursor` aus
    /// Handoff Ablauf 7). `None`, bis die erste Taste sie setzt. Sie überlebt das
    /// Neuladen der Liste; fällt die Zeile danach aus der Liste, springt der Fokus auf die
    /// erste Zeile.
    FocusedRow: Guid option

    /// Läuft gerade ein Stapel-Aufruf "Geprüft" für die Auswahl (ledger-nhwrt)? Dann ist die
    /// Aktion in der Leiste gesperrt, ein zweiter Klick löst keinen zweiten Aufruf aus.
    ReviewMarkBatchRunning: bool

    // ============================================
    // Kategorie ändern im Detail (ledger-sfab0)
    // ============================================

    /// Die Buchung, für die der Kategorie-Picker offen steht (immer die des Details).
    CategoryPickerFor: Guid option

    // ============================================
    // Split-Editor im Detail (ledger-pafkp, ADR 0058)
    // ============================================

    /// Der offene Split-Editor (immer für die Buchung im Detail), samt Entwurf. `None`, wenn zu.
    SplitEditor: Components.Ledger.SplitEditor.SplitEditorState option

    /// Die Konten des Budgets für das Übertrag-Ziel je Zeile; der Editor zeigt davon die offenen
    /// Budget-Konten (`openOnBudgetAccounts`).
    TransferTargets: RemoteData<YnabAccount list>

    /// Die Vorschläge zum Payee für den offenen Picker (höchstens drei, häufigste zuerst).
    /// Leer, bis die Antwort da ist, bei einem Fehler und ohne Payee.
    CategorySuggestions: YnabCategoryId list

    /// Der Zustand der Kategorie je Buchung (Aufruf läuft, Befehl scheiterte).
    CategoryRows: Map<Guid, CategoryRowState>

    /// Die Karte "Budget nach dem Wechsel", solange sie steht.
    CategoryCard: CategoryChangeCardState option
    CategoryCardVersion: int

    /// Zeilen, die nach einer Änderung nicht mehr zum aktiven Filter passen. Sie bleiben
    /// ausgegraut stehen (ADR 0055, Nachtrag). Neuladen, Filter- und Sortierwechsel leeren die Menge.
    FilteredOut: Set<Guid>

    /// Hat eine Änderung den Sortierschlüssel verschoben (`SortByCategory`)? Dann ruht das
    /// Nachladen, bis "Neu laden" die Liste ab Seite 1 neu aufsetzt.
    OrderStale: bool
}

/// Ist Von oder Bis des Zeitraum-Entwurfs ungültig eingegeben? Dann ist "Übernehmen"
/// gesperrt, und `ApplyDateFilter` ändert den Filter nicht.
let dateDraftHasError (model: Model) : bool =
    model.DateDraftFromInvalid || model.DateDraftToInvalid

/// Steht das Detail als Seitenpanel offen? Nur dann folgt es der Markierung und bleiben die
/// Listentasten an (ledger-q1hw2); ein offenes Sheet verdeckt die Liste und sperrt sie.
let detailPanelOpen (model: Model) : bool =
    model.DetailFor.IsSome && model.DetailForm = DetailPanel

/// Der aktuelle Stand der Buchung im Detail: die Zeile aus der geladenen Liste, wenn sie
/// dort steht (Prüfvermerk und Fehlschlag sind dort aktuell), sonst die Momentaufnahme vom
/// Öffnen. Nach einem Sortierwechsel kann die Buchung auf den geladenen Seiten fehlen; das
/// Detail zeigt sie dann weiter.
let detailEntryOf (model: Model) : LedgerEntryView option =
    model.DetailFor
    |> Option.map (fun snapshot ->
        match model.Entries with
        | Success entries -> entries |> List.tryFind (fun e -> e.Id = snapshot.Id) |> Option.defaultValue snapshot
        | _ -> snapshot)


// ============================================
// Rechnen mit der Auswahl (ledger-r1d2f), rein
// ============================================

/// Gilt die Zeile als gewählt? Unter `AllExcept` auch eine eben nachgeladene.
let isSelected (selection: Selection) (id: Guid) : bool =
    match selection with
    | NoSelection -> false
    | Picked ids -> ids.Contains id
    | AllExcept excluded -> not (excluded.Contains id)

/// Anzahl und Summe der Auswahl.
type SelectionTotals = {
    Count: int
    Amount: Money
}

/// Anzahl und Summe der Auswahl, ohne neuen Server-Aufruf. `Picked`: die gewählten
/// geladenen Zeilen. `AllExcept`: `TotalCount` und `TotalAmount` des Treffers minus die
/// Ausnahmen. In YNAB gelöschte Buchungen zählen in keinem Fall zur Summe, dieselbe
/// Regel wie bei `TotalAmount` (siehe `applyReviewMarkResult`). Summenzeile und Leiste
/// lesen nur diese Funktion.
let selectionTotals (model: Model) : SelectionTotals =
    let entries = model.Entries |> RemoteData.withDefault []
    let currency = model.TotalAmount.Currency
    let sumOf (rows: LedgerEntryView list) =
        rows |> List.filter (fun e -> not e.DeletedInYnab) |> List.sumBy (fun e -> e.Amount.Amount)
    match model.Selection with
    | NoSelection -> { Count = 0; Amount = { Amount = 0m; Currency = currency } }
    | Picked ids ->
        let rows = entries |> List.filter (fun e -> ids.Contains e.Id)
        { Count = List.length rows; Amount = { Amount = sumOf rows; Currency = currency } }
    | AllExcept excluded ->
        let rows = entries |> List.filter (fun e -> excluded.Contains e.Id)
        { Count = max 0 (model.TotalCount - List.length rows)
          Amount = { model.TotalAmount with Amount = model.TotalAmount.Amount - sumOf rows } }

/// Läuft eine nicht leere Auswahl? Nur dann zeigt die Summenzeile die Auswahl, und am PC
/// erscheint die Leiste.
let hasSelection (model: Model) : bool = (selectionTotals model).Count > 0

/// Die Auswahl, wie die Zeilen sie zeigen: eine Zeile, die nicht mehr zum Filter passt
/// (ausgegraut), ist nie gewählt, auch nicht über "alle auswählen" (ledger-sfab0).
let selectionForRows (model: Model) : Selection =
    if Set.isEmpty model.FilteredOut then
        model.Selection
    else
        match model.Selection with
        | NoSelection -> NoSelection
        | Picked ids ->
            let ids' = Set.difference ids model.FilteredOut
            if Set.isEmpty ids' then NoSelection else Picked ids'
        | AllExcept excluded -> AllExcept (Set.union excluded model.FilteredOut)

/// Zustand des Kopfzeilen-Kästchens am PC, abgeleitet aus der Anzahl der Auswahl gegen
/// `TotalCount`: leer, Teal-Strich (Teilauswahl) oder Haken (ganzer Treffer).
type HeaderCheckState =
    | HeaderEmpty
    | HeaderPartial
    | HeaderChecked

let headerCheckState (model: Model) : HeaderCheckState =
    let count = (selectionTotals model).Count
    if count <= 0 then HeaderEmpty
    elif model.TotalCount > 0 && count >= model.TotalCount then HeaderChecked
    else HeaderPartial

/// Der Auswahl-Text der Summenzeile: "Auswahl: 12 von 34 Buchungen".
let selectionSummaryText (count: int) (totalCount: int) : string =
    let noun = if totalCount = 1 then "Buchung" else "Buchungen"
    $"Auswahl: {Client.DesignSystem.Money.formatCount count} von {Client.DesignSystem.Money.formatCount totalCount} {noun}"

/// "12 ausgewählt" in der Auswahl-Leiste.
let selectionBarCountText (count: int) : string =
    $"{Client.DesignSystem.Money.formatCount count} ausgewählt"

let selectionClearLabel = "Auswahl aufheben"

/// Der Tastenhinweis neben "Auswahl aufheben" in der PC-Leiste (ledger-w4vte).
let selectionClearKeyHint = "Esc aufheben"
let selectionAllLabel = "Alle"

/// Die primäre Aktion der Auswahl-Leiste (ledger-nhwrt): setzt "Geprüft" für die Auswahl.
let selectionReviewedLabel = "Geprüft"

/// Der Satz des Toasts nach "Geprüft" für die Auswahl (Handoff 1.12): "n Buchungen geprüft",
/// bei Bedarf mit den Zahlen ohne Änderung, nicht in YNAB und nicht an YNAB gesendet.
let reviewBatchToastText (outcome: Shared.Domain.ReviewMarkBatchOutcome) : string =
    let changed = List.length outcome.Changed
    let sendFailed =
        outcome.Changed
        |> List.filter (fun c ->
            match c.WriteBack with
            | Shared.Domain.ReviewMarkSendFailed _ -> true
            | _ -> false)
        |> List.length
    let head =
        match changed with
        | 0 -> "Keine Buchung geprüft"
        | 1 -> "1 Buchung geprüft"
        | n -> $"{Client.DesignSystem.Money.formatCount n} Buchungen geprüft"
    let notes =
        [ if outcome.UnchangedCount > 0 then $"{outcome.UnchangedCount} ohne Änderung"
          if outcome.FailedCount > 0 then $"{outcome.FailedCount} nicht in YNAB"
          if sendFailed > 0 then $"{sendFailed} nicht an YNAB gesendet" ]
    if List.isEmpty notes then head else head + " (" + String.concat ", " notes + ")"

/// Die Art des Toasts nach "Geprüft" für die Auswahl: Warnung, sobald etwas nicht
/// geklappt hat (nicht in YNAB, nicht gesendet), Info ohne Änderung, sonst Erfolg.
let reviewBatchToastType (outcome: Shared.Domain.ReviewMarkBatchOutcome) : ToastType =
    let anySendFailed =
        outcome.Changed
        |> List.exists (fun c ->
            match c.WriteBack with
            | Shared.Domain.ReviewMarkSendFailed _ -> true
            | _ -> false)
    if anySendFailed || outcome.FailedCount > 0 then ToastWarning
    elif List.isEmpty outcome.Changed then ToastInfo
    else ToastSuccess

// ============================================
// "Kategorie setzen" für die ganze Auswahl (ledger-w29y6)
// ============================================

/// Die zweite Sammelaktion der Auswahl-Leiste (sekundär): öffnet den Kategorie-Picker.
let selectionCategoryLabel = "Kategorie setzen"

/// Die Unterzeile des Pickers am Handy: "12 Buchungen · -340,20 €", analog zu
/// "Payee · Betrag" im Einzelweg.
let selectionPickerSubtitle (count: int) (amount: Shared.Domain.Money) : string =
    let noun = if count = 1 then "Buchung" else "Buchungen"
    let sum = Client.DesignSystem.Money.formatAmountText false true amount.Amount amount.Currency
    $"{Client.DesignSystem.Money.formatCount count} {noun} · {sum}"

/// Wie viele Buchungen der Sammel-Befehl übersprungen hat: Ziel gleich Ist und die nicht
/// änderbaren zusammen.
let categoryBatchSkippedCount (outcome: Shared.Domain.CategoryBatchOutcome) : int =
    outcome.UnchangedCount + List.length outcome.Skipped

/// Die Gründe der übersprungenen Buchungen für den Toast, ohne Wiederholung und in fester
/// Reihenfolge: "Split, Übertrag, gelöscht". Ziel gleich Ist heißt "schon dort".
let categoryBatchSkipReasons (outcome: Shared.Domain.CategoryBatchOutcome) : string list =
    let has reason = List.contains reason outcome.Skipped
    [ if has Shared.Domain.SkipSplit || has Shared.Domain.SkipSplitNotAdjusted then "Split"
      if has Shared.Domain.SkipTransfer then "Übertrag"
      if has Shared.Domain.SkipDeletedInYnab then "gelöscht"
      if has Shared.Domain.SkipNotFound then "nicht gefunden"
      if outcome.UnchangedCount > 0 then "schon dort" ]

/// Die Buchungen des Stapels, deren Weg nach YNAB gescheitert ist, mit ihrem Grund.
let categoryBatchSendFailures (outcome: Shared.Domain.CategoryBatchOutcome) : string list =
    outcome.Changed
    |> List.choose (fun c ->
        match c.WriteBack with
        | Shared.Domain.CategorySendFailed reason -> Some reason
        | _ -> None)

/// Der Satz des Toasts nach "Kategorie setzen" für die Auswahl: "n Buchungen nach <Kategorie>
/// verschoben" (bei `None`: "Kategorie bei n Buchungen entfernt"), bei Bedarf mit
/// ", m übersprungen (Split, Übertrag, gelöscht)" und dem Hinweis "Kategorie nicht in YNAB"
/// samt erstem Grund, wenn der Weg nach YNAB bei Buchungen gescheitert ist.
let categoryBatchToastText (targetName: string option) (outcome: Shared.Domain.CategoryBatchOutcome) : string =
    let changed = List.length outcome.Changed
    let countText = Client.DesignSystem.Money.formatCount changed
    let head =
        match changed, targetName with
        | 0, _ -> "Keine Buchung geändert"
        | 1, Some name -> $"1 Buchung nach {name} verschoben"
        | n, Some name -> $"{Client.DesignSystem.Money.formatCount n} Buchungen nach {name} verschoben"
        | 1, None -> "Kategorie bei 1 Buchung entfernt"
        | _, None -> $"Kategorie bei {countText} Buchungen entfernt"
    let skipped = categoryBatchSkippedCount outcome
    let withSkipped =
        if skipped > 0 then
            let reasons = categoryBatchSkipReasons outcome
            let reasonText = String.concat ", " reasons
            $"{head}, {Client.DesignSystem.Money.formatCount skipped} übersprungen ({reasonText})"
        else
            head
    match categoryBatchSendFailures outcome with
    | [] -> withSkipped
    | [ reason ] -> $"{withSkipped}. {categoryHintTitle}: {reason}"
    | reason :: rest ->
        $"{withSkipped}. {categoryHintTitle}: {reason} (bei {List.length rest + 1} Buchungen)"

/// Die Art des Toasts: Warnung, sobald der Weg nach YNAB gescheitert ist, Info ohne Änderung,
/// sonst Erfolg.
let categoryBatchToastType (outcome: Shared.Domain.CategoryBatchOutcome) : ToastType =
    if not (List.isEmpty (categoryBatchSendFailures outcome)) then ToastWarning
    elif List.isEmpty outcome.Changed then ToastInfo
    else ToastSuccess

/// Die Gruppen der Rücknahme: die geänderten Buchungen nach ihrer vorherigen Kategorie
/// gruppiert (`None`: ohne Kategorie), in der Reihenfolge des ersten Auftretens. Je Gruppe
/// geht ein Sammel-Befehl hinaus, also ein PATCH.
let categoryBatchUndoGroups (changes: Shared.Domain.CategoryBatchChange list) : (CategoryChoice option * Guid list) list =
    changes
    |> List.map (fun c -> c.Previous, c.EntryId)
    |> List.fold
        (fun (order: CategoryChoice option list, groups: Map<CategoryChoice option, Guid list>) (previous, id) ->
            let order' = if List.contains previous order then order else order @ [ previous ]
            order', groups |> Map.add previous ((groups |> Map.tryFind previous |> Option.defaultValue []) @ [ id ]))
        ([], Map.empty)
    |> fun (order, groups) -> order |> List.map (fun key -> key, groups.[key])

/// Ist ein Filter aktiv, der die Kategorie betrifft (Kategorie-Chips oder "Ohne Kategorie")?
let categoryFilterActive (filters: Filters) : bool =
    not (Set.isEmpty filters.CategoryIds) || filters.WithoutCategory

/// Ist "Alle" in der Handy-Leiste aktiv? Nur beim ganzen Treffer ohne Ausnahme.
let isAllSelected (selection: Selection) : bool =
    match selection with
    | AllExcept excluded -> Set.isEmpty excluded
    | _ -> false

/// Wählt eine Zeile aus bzw. ab. Eine leere `Picked`-Menge wird zu `NoSelection`.
let toggleSelected (id: Guid) (selection: Selection) : Selection =
    match selection with
    | NoSelection -> Picked (Set.singleton id)
    | Picked ids ->
        let ids' = if ids.Contains id then Set.remove id ids else Set.add id ids
        if Set.isEmpty ids' then NoSelection else Picked ids'
    | AllExcept excluded ->
        AllExcept (if excluded.Contains id then Set.remove id excluded else Set.add id excluded)

/// Wählt die Zeilen der Spanne dazu; unter `AllExcept` nimmt es sie aus den Ausnahmen.
let addToSelection (ids: Guid list) (selection: Selection) : Selection =
    match selection with
    | NoSelection -> Picked (Set.ofList ids)
    | Picked current -> Picked (Set.union current (Set.ofList ids))
    | AllExcept excluded -> AllExcept (Set.difference excluded (Set.ofList ids))

/// Die geladenen Zeilen zwischen zwei Ids, beide eingeschlossen, in Listenreihenfolge.
/// Kennt die Liste eine der beiden nicht mehr, ist die Spanne leer.
let rangeBetween (entries: LedgerEntryView list) (fromId: Guid) (toId: Guid) : Guid list =
    match entries |> List.tryFindIndex (fun e -> e.Id = fromId), entries |> List.tryFindIndex (fun e -> e.Id = toId) with
    | Some a, Some b ->
        let lo, hi = min a b, max a b
        entries |> List.skip lo |> List.take (hi - lo + 1) |> List.map (fun e -> e.Id)
    | _ -> []

/// Eine Zeile verlässt die Liste (Prüfvermerk bei aktivem Chip): sie fällt aus `Picked`
/// bzw. aus den Ausnahmen. `TotalCount` und `TotalAmount` führt der Aufrufer nach, so
/// bleiben Anzahl und Summe der Auswahl stimmig.
let removeFromSelection (id: Guid) (selection: Selection) : Selection =
    match selection with
    | NoSelection -> NoSelection
    | Picked ids ->
        let ids' = Set.remove id ids
        if Set.isEmpty ids' then NoSelection else Picked ids'
    | AllExcept excluded -> AllExcept (Set.remove id excluded)

// ============================================
// Tastaturfokus (ledger-rjb8w), rein
// ============================================

/// Der Index der Zeile hinter dem Fokus; ohne Fokus oder mit einem Fokus außerhalb der
/// Liste die erste. `None` nur bei leerer Liste.
let private clampedFocusIndex (entries: LedgerEntryView list) (focus: Guid option) (delta: int) : int option =
    match entries with
    | [] -> None
    | _ ->
        match focus |> Option.bind (fun id -> entries |> List.tryFindIndex (fun e -> e.Id = id)) with
        | Some index -> Some (max 0 (min (List.length entries - 1) (index + delta)))
        | None -> Some 0

/// Wohin der Fokus nach einer Pfeiltaste wandert. Am Listenende und am Listenanfang
/// bleibt er stehen.
let movedFocus (entries: LedgerEntryView list) (focus: Guid option) (delta: int) : Guid option =
    clampedFocusIndex entries focus delta |> Option.map (fun i -> (List.item i entries).Id)

/// Der Fokus nach dem Nachladen: liegt er noch in der Liste, bleibt er; sonst springt er auf
/// die erste Zeile. Ohne Fokus bleibt es dabei (die Liste zeigt ihn erst nach der ersten Taste).
let reconcileFocus (entries: LedgerEntryView list) (focus: Guid option) : Guid option =
    match focus with
    | None -> None
    | Some id when entries |> List.exists (fun e -> e.Id = id) -> focus
    | Some _ -> entries |> List.tryHead |> Option.map (fun e -> e.Id)

/// Der Fokus, nachdem die Zeile `removedId` die Liste verlassen hat: die Zeile, die an ihre
/// Stelle nachrückt, am Listenende die neue letzte. Liegt der Fokus woanders, bleibt er.
let focusAfterRemoval (before: LedgerEntryView list) (removedId: Guid) (focus: Guid option) : Guid option =
    if focus <> Some removedId then
        focus
    else
        let after = before |> List.filter (fun e -> e.Id <> removedId)
        match before |> List.tryFindIndex (fun e -> e.Id = removedId) with
        | Some index when not (List.isEmpty after) -> Some (List.item (min index (List.length after - 1)) after).Id
        | _ -> None

/// Die Zeile nach `id` (das Ziel des Sprungs nach G); am Listenende die Zeile selbst.
let nextFocusAfter (entries: LedgerEntryView list) (id: Guid) : Guid =
    match entries |> List.tryFindIndex (fun e -> e.Id = id) with
    | Some index when index + 1 < List.length entries -> (List.item (index + 1) entries).Id
    | _ -> id

/// Fängt ein Eingabeelement die Taste ab? In Suchfeldern, Filtern und Auswahllisten tippt
/// G einen Buchstaben, die Pfeiltasten gehören dem Feld.
let isTypingTarget (tagName: string) (isContentEditable: bool) : bool =
    isContentEditable
    || (match (if isNull tagName then "" else tagName).ToUpperInvariant() with
        | "INPUT" | "TEXTAREA" | "SELECT" -> true
        | _ -> false)

/// Aktiviert Enter dieses Element selbst (Knopf, Link, Aufklappzeile)? Dann gehört die Taste
/// ihm und öffnet kein Detail.
let isActivatable (tagName: string) : bool =
    match (if isNull tagName then "" else tagName).ToUpperInvariant() with
    | "BUTTON" | "A" | "SUMMARY" -> true
    | _ -> false

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
    /// Klick auf einen sortierbaren Spaltenkopf (ledger-bn913): dasselbe Feld dreht die
    /// Richtung, ein anderes setzt Feld und Startrichtung. Lädt ab Seite 0 neu.
    | SetSort of LedgerSortField
    /// Tipp auf eine Zeile im Sortier-Sheet am Handy (ledger-skpcx): setzt Feld und
    /// Richtung genau so, ohne Umschalten, schließt das Sheet und lädt ab Seite 0 neu.
    | SetSortExactly of LedgerSort
    | OpenSortSheet
    | CloseSortSheet

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

    // Detail einer Buchung (ledger-q1hw2, ADR 0053)
    /// Klick auf eine Zeile oder Tipp am Handy: markiert die Zeile (`FocusedRow`), öffnet das
    /// Detail für genau sie und lädt ihre Historie. Im Auswahl-Modus am Handy öffnet nichts.
    | OpenDetail of LedgerEntryView
    | CloseDetail
    /// Die Ansicht meldet, in welcher Form das Detail bei der gemessenen Breite aufgeht.
    | DetailFormChanged of DetailForm
    /// Die Ansicht meldet, wie die Liste bei Breite und Eingabegerät bedient wird (ledger-m6zpd).
    | ListInteractionChanged of ListInteraction
    | LoadHistory of Guid
    /// Die Historie der Buchung `Guid`; kommt sie nach einem Wechsel der Buchung an, gilt sie nicht mehr.
    | HistoryLoaded of Guid * Result<LedgerHistoryLine list, string>

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
    /// Lädt die Kategorien nur, wenn der Stand älter als zwei Minuten ist
    /// (`shouldReloadCategories`). Auslöser: Öffnen des Pickers (ledger-kwwxz,
    /// verdrahtet in `ledger-sfab0`).
    | EnsureCategoriesFresh

    /// Ein Filter-Chip öffnet sein Popover/Sheet; setzt den passenden Entwurf aus
    /// dem committeten Zustand vor.
    | OpenFilterPopover of ChipId
    /// Esc, Klick daneben oder „Schließen": verwirft den Entwurf, `Filters` bleibt
    /// unverändert.
    | CloseFilterPopover

    /// Filter-Knopf am Payee im Detail (ADR 0053): setzt `Filters.Payee` wie
    /// `SelectPayeeFilter`; das Detail bleibt offen.
    | FilterByRowPayee of string
    /// Filter-Knopf an der Kategorie im Detail: die Kategorienauswahl besteht danach aus
    /// genau dieser Kategorie, "Ohne Kategorie" ist aus; das Detail bleibt offen.
    | FilterByRowCategory of Guid

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

    // ============================================
    // Wisch nach rechts: geprüft, mit Rückgängig (ledger-th36h)
    // ============================================

    /// Wisch nach rechts an einer Karte (Handy). Wirkt nur auf eine zu prüfende Zeile.
    | StartSwipeReviewed of Guid
    | RunSwipeReviewed of Guid
    /// Antwort auf die Geste; der Payee-Name ist festgehalten, bevor die Zeile die Liste
    /// verlässt (Text des Toasts).
    | SwipeReviewedDone of Guid * string * Result<ReviewMarkOutcome, string>
    /// Klick auf "Rückgängig" im Toast: stellt den Vorzustand aus der Antwort wieder her.
    | StartUndoReviewMark of Guid * ReviewMarkState
    | RunUndoReviewMark of Guid * ReviewMarkState
    | ReviewMarkRestored of Guid * ReviewMarkState * Result<ReviewMarkOutcome, string>

    // ============================================
    // Kategorie ändern im Detail (ledger-sfab0, ADR 0055)
    // ============================================
    // Dieselbe Teilung wie beim Prüfvermerk (ADR 0020): `Start...` ist rein, `Run...` ruft
    // den Proxy auf.

    /// Pfeil an der Kategorie im Detail: öffnet den Picker, wenn die Buchung änderbar ist.
    | OpenCategoryPicker of Guid
    | CloseCategoryPicker
    /// Lädt die Vorschläge zum Payee (ein Aufruf je Öffnung).
    | RunLoadCategorySuggestions of Guid * payee: string * current: YnabCategoryId option
    | CategorySuggestionsLoaded of Guid * Result<YnabCategoryId list, string>
    /// Klick im Picker.
    | StartSetEntryCategory of Guid * YnabCategoryId option
    | RunSetEntryCategory of Guid * YnabCategoryId option
    | EntryCategorySet of Guid * YnabCategoryId option * Result<CategoryChangeOutcome, string>
    /// "Rückgängig" im Toast: der alte Stand geht über denselben Schreibweg zurück.
    | StartUndoCategory of Guid * CategoryChoice option
    | RunUndoCategory of Guid * CategoryChoice option
    | EntryCategoryRestored of Guid * CategoryChoice option * Result<CategoryChangeOutcome, string>
    /// "Erneut senden" an der Hinweisleiste "Kategorie nicht in YNAB".
    | StartRetryCategorization of Guid

    // ============================================
    // Split-Editor im Detail (ledger-pafkp, ADR 0058)
    // ============================================
    // Dieselbe Teilung wie bei der Kategorie (ADR 0020): `Start...` ist rein, `Run...` ruft den Proxy.

    /// "Aufteilen", "Ändern" oder der Pfeil an der Kategorie einer Split-Buchung: öffnet den
    /// Editor, wenn die Sperren es erlauben.
    | OpenSplitEditor of Guid
    /// "Abbrechen", Esc oder Schließen des Details: verwirft den Entwurf.
    | CloseSplitEditor
    | RunLoadTransferTargets
    | TransferTargetsLoaded of Result<YnabAccount list, string>
    | AddSplitLine
    | RemoveSplitLine of int
    | SetSplitLineAmount of int * string
    | SetSplitLineMemo of int * string
    /// Knopf "Rest" an der Zeile.
    | FillSplitLineRest of int
    | OpenSplitCategoryPicker of int
    | OpenSplitAccountPicker of int
    | CloseSplitPicker
    /// Wahl im Kategorie-Picker einer Zeile; `None` ist "Ohne Kategorie".
    | SelectSplitCategory of int * YnabCategoryId option
    | SelectSplitAccount of int * YnabAccountId
    /// "Speichern" im Editor.
    | StartSaveSplits
    | RunSaveSplits of Guid * TransactionSplit list
    | SplitsSaved of Guid * TransactionSplit list * Result<CategoryChangeOutcome, string>
    /// "Rückgängig" im Toast: stellt über denselben Schreibweg die Zeilen vor dem Speichern her.
    | StartUndoSplit of Guid * TransactionSplit list
    | RunUndoSplit of Guid * TransactionSplit list
    | SplitsRestored of Guid * TransactionSplit list * Result<CategoryChangeOutcome, string>
    /// Holt eine Buchung neu (nach einem gescheiterten Split-Versand), um die offene Neuanlage zu sehen.
    | RunRefreshEntry of Guid
    | EntryRefreshed of Guid * Result<LedgerEntryView option, string>
    | RunRetryCategorization of Guid
    | CategorizationRetried of Guid * Result<CategoryWriteBack, string>
    /// Die Karte "Budget nach dem Wechsel" läuft ab (nur die mit dieser Version).
    | CategoryCardExpired of version: int
    /// Trefferzahl und Summe neu holen, solange die Reihenfolge veraltet ist.
    | RunRefreshTotals
    | TotalsRefreshed of Result<LedgerEntryPage, string>

    // ============================================
    // Auswahl mit Summe (ledger-r1d2f)
    // ============================================

    /// Langes Drücken auf eine Zeile (Handy): startet den Modus mit genau dieser Zeile.
    | StartSelectionMode of Guid
    /// Tipp auf Zeile oder Kästchen im Modus (Handy): wählt aus bzw. ab.
    | ToggleRowSelected of Guid
    /// Klick auf das Kästchen einer Zeile (PC). Mit Shift wählt er die Spanne zwischen
    /// Anker und Zeile, ohne Shift wählt er die Zeile aus bzw. ab und setzt den Anker.
    | ClickRowCheckbox of Guid * shift: bool
    /// "Alle" in der Handy-Leiste: der ganze Treffer, nochmal leert die Auswahl.
    | ToggleSelectAll
    /// Kopfzeilen-Kästchen (PC): leer oder Strich wählt den ganzen Treffer, Haken leert.
    | ToggleHeaderCheckbox
    /// "Auswahl aufheben": leert die Auswahl und beendet den Modus.
    | ClearSelection

    // ============================================
    // "Geprüft" für die ganze Auswahl (ledger-nhwrt)
    // ============================================

    /// "Geprüft" in der Auswahl-Leiste: sperrt die Aktion und stößt den Stapel-Aufruf an.
    | StartReviewMarkBatch
    /// Der Stapel-Aufruf mit der Auswahl (Ids oder Treffer-Anfrage plus Ausnahmen).
    | RunReviewMarkBatch of ReviewMarkBatchSelection
    | ReviewMarkBatchDone of Result<ReviewMarkBatchOutcome, string>
    /// Klick auf "Rückgängig" im Toast: stellt die Vorzustände des Stapels wieder her.
    | StartUndoReviewMarkBatch of ReviewMarkRestoreItem list
    | RunUndoReviewMarkBatch of ReviewMarkRestoreItem list
    | ReviewMarkBatchRestored of Result<ReviewMarkBatchOutcome, string>

    // ============================================
    // Tastaturfokus und Taste G (ledger-rjb8w, PC)
    // ============================================

    /// Pfeil auf/ab oder K/J: bewegt die Fokuszeile um `delta` Zeilen (+1 ab, -1 auf).
    | MoveFocus of delta: int
    /// Taste G (Geprüft) oder Umschalt+G (Ungeprüft) auf der Fokuszeile.
    | ReviewKeyPressed of ReviewMarkTarget
    /// Enter auf der markierten Zeile (ledger-q1hw2): öffnet das Detail.
    | OpenFocusedDetail
    /// Leertaste auf der markierten Zeile (ledger-w4vte): nimmt sie in die Auswahl oder
    /// heraus, wie ein Klick auf ihr Kästchen.
    | ToggleFocusedSelected

    // ============================================
    // "Kategorie setzen" für die ganze Auswahl (ledger-w29y6)
    // ============================================

    /// "Kategorie setzen" in der Leiste: öffnet den Picker (nur mit nicht leerer Auswahl),
    /// ein zweiter Klick schließt ihn.
    | ToggleBatchCategoryPicker
    | CloseBatchCategoryPicker
    /// Wahl im Picker (`None`: ohne Kategorie): sperrt die Aktionen und stößt den Aufruf an.
    | StartCategoryBatch of YnabCategoryId option
    /// Der Sammel-Aufruf mit der Auswahl und der Ziel-Kategorie.
    | RunCategoryBatch of ReviewMarkBatchSelection * YnabCategoryId option
    | CategoryBatchDone of YnabCategoryId option * Result<CategoryBatchOutcome, string>
    /// Klick auf "Rückgängig" im Toast: ein Sammel-Befehl je Gruppe gleicher vorheriger Kategorie.
    | StartUndoCategoryBatch of CategoryBatchChange list
    | RunUndoCategoryBatch of (CategoryChoice option * Guid list) list
    /// Die Antworten der Gruppen der Rücknahme, je mit der Kategorie, auf die sie zurückstellte.
    | CategoryBatchRestored of (CategoryChoice option * Result<CategoryBatchOutcome, string>) list

    /// Esc (ledger-q1hw2, Kette ledger-w4vte): schließt genau eine Stufe, erst das offene
    /// Popover, dann das Detail, dann die Auswahl.
    | EscapePressed

/// Die Tastenbelegung der Kassenbuch-Liste (PC): Pfeil ab/auf und J/K bewegen den Fokus,
/// G setzt "Geprüft", Umschalt+G "Ungeprüft", Enter öffnet das Detail der markierten Zeile
/// und Esc schließt eine Stufe (ledger-q1hw2), die Leertaste wählt die markierte Zeile
/// (ledger-w4vte). Ein Tastenereignis aus einem Eingabefeld oder mit
/// Strg, Meta oder Alt löst nichts aus. Enter und Leertaste auf einem Knopf oder Link bleiben
/// dessen eigene Aktivierung: sonst löste ein fokussierter Knopf zweimal aus (ADR 0053).
let keyCommandFor
    (tagName: string)
    (isContentEditable: bool)
    (ctrlOrMeta: bool)
    (alt: bool)
    (key: string)
    : Msg option =
    if isTypingTarget tagName isContentEditable || ctrlOrMeta || alt then
        None
    else
        match key with
        | "ArrowDown" | "j" | "J" -> Some (MoveFocus 1)
        | "ArrowUp" | "k" | "K" -> Some (MoveFocus -1)
        | "g" -> Some (ReviewKeyPressed Reviewed)
        | "G" -> Some (ReviewKeyPressed Unreviewed)
        | "Enter" when not (isActivatable tagName) -> Some OpenFocusedDetail
        | " " when not (isActivatable tagName) -> Some ToggleFocusedSelected
        | "Escape" -> Some EscapePressed
        | _ -> None

/// Was die Seite nach oben meldet.
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    /// Ein Toast mit Schaltfläche (ledger-th36h: "Rückgängig").
    | ShowToastWithAction of string * ToastType * ToastAction

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

// ============================================
// Available je Kategorie frisch halten (ledger-kwwxz, ADR 0055 Punkt 8)
// ============================================

/// Ab diesem Alter lädt der Picker-Auslöser und der PWA-Vordergrund die Kategorien neu.
let categoriesFreshness = TimeSpan.FromMinutes 2.0

/// Reine Nachlade-Entscheidung. Nie bei laufendem Abruf; sonst bei fehlendem Stand oder
/// einem Stand älter als zwei Minuten.
let shouldReloadCategories
    (categories: RemoteData<YnabCategory list>)
    (refreshing: bool)
    (loadedAt: DateTime option)
    (now: DateTime)
    : bool =
    if refreshing || RemoteData.isLoading categories then false
    else
        match loadedAt with
        | None -> true
        | Some at -> now - at > categoriesFreshness

/// Nachlade-Entscheidung über dem Kassenbuch-Modell.
let categoriesReloadDue (model: Model) (now: DateTime) : bool =
    shouldReloadCategories model.Categories model.CategoriesRefreshing model.CategoriesLoadedAt now

let private sameMonth (a: DateTime) (b: DateTime) = a.Year = b.Year && a.Month = b.Month

/// Schreibt Available nach einer Kategorieänderung lokal fort. Gilt nur für Buchungen im
/// Kalendermonat von `today` (YNABs `balance` ist der laufende Monat). Die alte Kategorie
/// verliert den Betrag der Buchung (Ausgabe −50 erhöht um 50), die neue bekommt ihn
/// (Ausgabe −50 senkt um 50). Kategorien ohne Available und "ohne Kategorie" (`None`)
/// bleiben unberührt. Der nächste Abruf überschreibt.
let shiftAvailable
    (entryDate: DateTime, amount: decimal, oldCategoryId: YnabCategoryId option, newCategoryId: YnabCategoryId option, today: DateTime)
    (categories: YnabCategory list)
    : YnabCategory list =
    if not (sameMonth entryDate today) then categories
    else
        categories
        |> List.map (fun c ->
            let delta =
                (if oldCategoryId = Some c.Id then -amount else 0m)
                + (if newCategoryId = Some c.Id then amount else 0m)
            match c.Available with
            | Some a when delta <> 0m -> { c with Available = Some { a with Amount = a.Amount + delta } }
            | _ -> c)

/// Zeilen der Karte: zuerst die alte, dann die neue Kategorie. Mit Zahlen nur für
/// Buchungen im laufenden Monat und Kategorien mit Available; sonst nur das Label.
/// "Ohne Kategorie" und unbekannte Ids ergeben keine Zeile.
let categoryChangeHint
    (entryDate: DateTime, amount: decimal, oldCategoryId: YnabCategoryId option, newCategoryId: YnabCategoryId option, today: DateTime)
    (categories: YnabCategory list)
    : CategoryChangeHintRow list =
    let shifted = shiftAvailable (entryDate, amount, oldCategoryId, newCategoryId, today) categories
    let text (m: Money) = Client.DesignSystem.Money.formatAmountText false true m.Amount m.Currency
    let row (id: YnabCategoryId option) =
        id
        |> Option.bind (fun i -> categories |> List.tryFind (fun c -> c.Id = i))
        |> Option.map (fun before ->
            let label = $"{before.GroupName}: {before.Name}"
            if sameMonth entryDate today then
                let after = shifted |> List.find (fun c -> c.Id = before.Id)
                { Label = label; OldText = before.Available |> Option.map text; NewText = after.Available |> Option.map text }
            else
                { Label = label; OldText = None; NewText = None })
    [ row oldCategoryId; row newCategoryId ] |> List.choose id

/// Fußzeile des Pickers: "Stand gerade eben", "Stand vor 3 Min" bis eine Stunde,
/// danach die Uhrzeit ("Stand 10:42").
let availableAgeLabel (loadedAt: DateTime) (now: DateTime) : string =
    let age = now - loadedAt
    let two (n: int) = (string n).PadLeft(2, '0')
    if age < TimeSpan.FromMinutes 1.0 then "Stand gerade eben"
    elif age < TimeSpan.FromHours 1.0 then $"Stand vor {int age.TotalMinutes} Min"
    else $"Stand {two loadedAt.Hour}:{two loadedAt.Minute}"
