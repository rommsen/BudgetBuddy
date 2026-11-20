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
open Fable.Core
open Shared.Domain
open Types

module ListKeys = Client.DesignSystem.ListKeys

/// Wie viele Buchungen eine Seite traegt (AC5, ledger-b1xwe). Ein Modul-Wert statt
/// eines Literals: `RunLoadEntriesPage` und die Tests teilen sich diese eine Zahl.
let pageSize = 100

/// Der Konto-Filter der Liste: "Alle Konten" ist ein eigener Eintrag, kein Schalter
/// (Entscheidung bei der Verfeinerung, siehe Task-Notes) — er traegt die Summe aller
/// Konten und steht immer zuerst in der Kontostaende-Leiste.
type AccountFilter =
    | AllAccounts
    | OneAccount of Guid

/// Der Filter, mit dem ein Eintrag aus „Zu tun“ das Kassenbuch öffnet (ledger-s1weh).
type QuickFilterPreset =
    /// Chip „Zu prüfen“.
    | PresetNeedsReview
    /// Chip „Rückfrage Lena“.
    | PresetAskLena

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

/// Der committete Zustand der Such-Filter (ADR 0036). Mehrfachauswahl bei
/// der Kategorie (Oder-Verknüpfung), sonst je Kriterium höchstens ein Wert.
type Filters = {
    CategoryIds: Guid Set
    DateRange: DateRangeFilter option
    /// Die freie Suche (ledger-d9z3n): Teiltext in Payee, Memo oder Kategoriename, auf dem Server.
    /// Sie ersetzt die früheren Filter Payee und Memo.
    Search: string option
    /// Höhe ohne Vorzeichen, von und bis (ADR 0095).
    AmountMin: decimal option
    AmountMax: decimal option
    /// Umschalter „Alle | Ausgaben | Einnahmen“ unter dem Betrag (ADR 0095), wirkt auch ohne Betrag.
    AmountDirection: AmountDirection
    /// Chip "Ohne Kategorie" (ADR 0048): reiner Toggle wie `OnlyNeedsReview`. Zählt
    /// nicht zu `hasAnyFilter`; `ClearAllFilters` setzt ihn zurück (ledger-d9z3n).
    WithoutCategory: bool
    /// Chip "Rückfrage Lena (n)" (ADR 0056, Punkt 6): reiner Toggle wie `WithoutCategory`.
    /// Zählt nicht zu `hasAnyFilter`; `ClearAllFilters` setzt ihn zurück (ledger-d9z3n).
    OnlyAskLena: bool
}

let noFilters : Filters = {
    CategoryIds = Set.empty
    DateRange = None
    Search = None
    AmountMin = None
    AmountMax = None
    AmountDirection = AllDirections
    WithoutCategory = false
    OnlyAskLena = false
}

/// Was im Text eines Betrags-Suchfelds steht (ledger-dn4vy): nichts, ein lesbarer Betrag oder etwas
/// Unlesbares. Die Unterscheidung braucht der Filter: leer nimmt ihn weg, unlesbar lässt ihn stehen.
type AmountFilterText =
    | AmountTextEmpty
    | AmountTextValue of decimal
    | AmountTextUnreadable

/// Liest den Text eines Betrags-Suchfelds: leer (nach `Trim()`) ist `AmountTextEmpty`, sonst ein
/// geparster Wert, deutsches Komma wie Punkt gleichermaßen akzeptiert (Roman tippt üblicherweise
/// Komma). Der Filter vergleicht die Höhe ohne Vorzeichen (ADR 0095), ein Vorzeichen ist deshalb
/// unlesbar: die Richtung kommt aus dem Umschalter „Alle | Ausgaben | Einnahmen“. Ein nicht lesbarer Text (z. B. "abc") ist `AmountTextUnreadable`, statt eine Ausnahme zu
/// werfen: die Eingabe bleibt am Feld sichtbar (`AmountMinDraft`/`AmountMaxDraft`) und gilt dort als
/// ungültig, der Filter bleibt unverändert (`amountFilterValue`).
let parseAmountFilterText (text: string) : AmountFilterText =
    let trimmed = if isNull text then "" else text.Trim()
    if trimmed = "" then
        AmountTextEmpty
    else
        match
            Decimal.TryParse(
                trimmed.Replace(",", "."),
                System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture
            )
        with
        | true, value -> AmountTextValue value
        | false, _ -> AmountTextUnreadable

/// Der Text eines Betragsfelds im Filter ohne Vorzeichen (ADR 0095): „-“, „+“ und das typografische
/// Minus fallen weg, der Rest bleibt, wie er getippt wurde.
let withoutAmountSign (text: string) : string =
    if isNull text then "" else text.Replace("-", "").Replace("+", "").Replace("\u2212", "")

/// Der Wert einer Filtergrenze nach einer Eingabe: leer nimmt die Grenze weg, ein lesbarer Betrag
/// gilt, ein unlesbarer lässt die bisherige Grenze `current` stehen (ledger-dn4vy).
let amountFilterValue (current: decimal option) (text: string) : decimal option =
    match parseAmountFilterText text with
    | AmountTextEmpty -> None
    | AmountTextValue value -> Some value
    | AmountTextUnreadable -> current

/// Der Entwurf eines Betragsfelds entspricht der übernommenen Grenze `committed` (ledger-awf5b): leer gegen
/// keine Grenze, ein lesbarer Wert gegen dieselbe Grenze als Dezimalvergleich (12,5 = 12.50). Ein unlesbarer
/// Text entspricht nie, er steht als Fehler am Feld und gilt als laufende Eingabe.
let amountDraftMatches (committed: decimal option) (draft: string) : bool =
    match parseAmountFilterText draft, committed with
    | AmountTextEmpty, None -> true
    | AmountTextValue value, Some limit -> value = limit
    | _ -> false

/// Das Betragsfeld des Filters ist ungültig: sein Text ist weder leer noch ein lesbarer Betrag. Gilt
/// sofort beim Tippen, nicht erst nach der Verzögerung.
let amountFilterTextInvalid (text: string) : bool =
    parseAmountFilterText text = AmountTextUnreadable

/// Der Anzeigetext eines geparsten Betrags im Suchfeld, wenn ein Popover neu
/// geöffnet wird (Rückrichtung von `parseAmountFilterText`). Immer mit Punkt
/// (Invariant Culture): reiner Bearbeitungstext, keine Geld-Anzeige (die
/// benutzt `Money.formatAmountText`).
let formatAmountDraft (value: decimal) : string =
    value.ToString(System.Globalization.CultureInfo.InvariantCulture)

let hasCategoryFilter (f: Filters) = not (Set.isEmpty f.CategoryIds)
let hasDateFilter (f: Filters) = f.DateRange.IsSome
let hasSearch (f: Filters) = f.Search.IsSome
let hasAmountFilter (f: Filters) = f.AmountMin.IsSome || f.AmountMax.IsSome
let hasDirectionFilter (f: Filters) = f.AmountDirection <> AllDirections

/// Die Namen der gewählten Kategorien in Listenreihenfolge der Kategorien; unbekannte Ids entfallen. Eine Stelle
/// für die Chips am Handy (`activeFilters`) und die Filterleiste am PC.
let selectedCategoryNames (categories: YnabCategory list) (ids: Guid Set) : string list =
    categories
    |> List.filter (fun c -> let (YnabCategoryId g) = c.Id in ids.Contains g)
    |> List.map (fun c -> c.Name)

/// Ist mindestens einer der Such-Filter oder die Suche aktiv? Steuert, ob die Summenzeile
/// über der Liste steht (AC „Summenzeile erscheint genau dann..."). Der Chip
/// "Zu prüfen (n)" und der Schalter "Gelöschte zeigen" zählen bewusst nicht mit:
/// beide sind eigene, ältere Bedienelemente außerhalb dieser Filter.
let hasAnyFilter (f: Filters) : bool =
    hasCategoryFilter f || hasDateFilter f || hasSearch f || hasAmountFilter f || hasDirectionFilter f

/// Steht die Summenzeile über der Liste? Eigenes Prädikat neben `hasAnyFilter` (ledger-v9rq3):
/// die Chips "Zu prüfen" und "Ohne Kategorie" zeigen Treffer und Summe wie die fünf
/// Such-Filter, bleiben aber außerhalb von `hasAnyFilter`, weil "Alle Filter löschen" und der
/// Leerzustand "Kein Treffer" an dessen Bedeutung hängen.
let hasSummaryFilter (onlyNeedsReview: bool) (f: Filters) : bool =
    hasAnyFilter f || onlyNeedsReview || f.WithoutCategory

let private plainDate (d: DateTime) : string =
    d.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture)

let private plainAmount (value: decimal) : string =
    (formatAmountDraft value).Replace(".", ",")

/// Die Richtung als Wort (Chip und Summenzeile): „Ausgaben“, „Einnahmen“, bei „Alle“ nichts.
let directionText (direction: AmountDirection) : string option =
    match direction with
    | AllDirections -> None
    | OnlyOutflows -> Some "Ausgaben"
    | OnlyInflows -> Some "Einnahmen"

/// Die Wahl im Umschalter „Alle | Ausgaben | Einnahmen“, in dieser Reihenfolge (ADR 0095).
let directionOptions : (AmountDirection * string) list =
    [ AllDirections, "Alle"; OnlyOutflows, "Ausgaben"; OnlyInflows, "Einnahmen" ]

/// Der Filter in Worten für die PC-Summenzeile (Tafeln Ablauf 1 und 2): Konto, Chips,
/// Kategorien, Zeitraum, Suche, Betrag, getrennt durch " · ". `scope` ist der
/// Kontoname oder "alle Konten"; `categoryNames` die Namen der gewählten Kategorien.
let filterPlainText (scope: string) (onlyNeedsReview: bool) (categoryNames: string list) (f: Filters) : string =
    [ yield scope
      if onlyNeedsReview then yield "zu prüfen"
      if f.WithoutCategory then yield "ohne Kategorie"
      if not (List.isEmpty categoryNames) then yield String.Join(", ", categoryNames)
      match f.DateRange with
      | Some r ->
          match r.Quick, r.From, r.To with
          | Some Last12Months, _, _ -> yield "letzte 12 Monate"
          | Some ThisMonth, _, _ -> yield "dieser Monat"
          | Some LastMonth, _, _ -> yield "letzter Monat"
          | Some ThisYear, _, _ -> yield "dieses Jahr"
          | None, Some a, Some b -> yield $"{plainDate a} bis {plainDate b}"
          | None, Some a, None -> yield $"ab {plainDate a}"
          | None, None, Some b -> yield $"bis {plainDate b}"
          | None, None, None -> ()
      | None -> ()
      match f.Search with
      | Some text -> yield $"Suche: {text}"
      | None -> ()
      match f.AmountMin, f.AmountMax with
      | Some a, Some b -> yield $"Betrag {plainAmount a} bis {plainAmount b}"
      | Some a, None -> yield $"Betrag ab {plainAmount a}"
      | None, Some b -> yield $"Betrag bis {plainAmount b}"
      | None, None -> ()
      match directionText f.AmountDirection with
      | Some text -> yield text
      | None -> () ]
    |> String.concat " · "

/// Ein gesetzter Filter als Chip unter Suche und Filter-Knopf (ledger-d9z3n), in der Reihenfolge
/// der Chips. Jeder trägt ein „×“, das genau diesen Filter zurücknimmt.
[<RequireQualifiedAccess>]
type ActiveFilter =
    | NeedsReview
    | AskLena
    | WithoutCategory
    | Category
    | Date
    | Amount
    /// Die Richtung „Ausgaben“ oder „Einnahmen“ (ADR 0095), hinter dem Betrag.
    | Direction
    /// Eine andere Sortierung als „Datum, neueste zuerst“.
    | Sort
    /// „Gelöschte zeigen“ ist an.
    | WithDeleted

let private shortDate (d: DateTime) : string =
    d.ToString("dd.MM.yy", System.Globalization.CultureInfo.InvariantCulture)

/// Der Zeitraum kurz (Chip und Zeile „Zeitraum“ im Filter-Sheet): der Name der Schnellwahl,
/// sonst der freie Bereich.
let dateRangeShortText (range: DateRangeFilter) : string =
    match range.Quick, range.From, range.To with
    | Some quick, _, _ -> dateQuickLabel quick
    | None, Some a, Some b -> $"{shortDate a} bis {shortDate b}"
    | None, Some a, None -> $"ab {shortDate a}"
    | None, None, Some b -> $"bis {shortDate b}"
    | None, None, None -> "alle"

/// Der Betragsfilter als Bereich für den Chip: „Betrag 5 bis 50 €“, „Betrag ab 5 €“, „Betrag bis 50 €“.
let amountRangeText (f: Filters) : string option =
    match f.AmountMin, f.AmountMax with
    | Some a, Some b -> Some $"Betrag {plainAmount a} bis {plainAmount b} €"
    | Some a, None -> Some $"Betrag ab {plainAmount a} €"
    | None, Some b -> Some $"Betrag bis {plainAmount b} €"
    | None, None -> None

/// Der Text des Kategorie-Chips: ein Name, bei mehreren die Anzahl. `categoryNames` sind die Namen
/// der gewählten Kategorien; ist ein Name unbekannt (Kategorien noch nicht geladen), steht die Anzahl.
let categoryFilterChipText (selectedCount: int) (categoryNames: string list) : string =
    match categoryNames with
    | [ name ] when selectedCount = 1 -> $"Kategorie: {name}"
    | _ -> $"Kategorie: {selectedCount}"

/// Der Name des Lena-Filters (ADR 0056, ledger-rb3xw): gleich im Filter-Sheet, am Chip am PC und am aktiven Chip.
/// Auch die Marke in der Zeile zeigt „Lena“ (Vorleser „Rückfrage Lena“); Toast und Historie („Lena gefragt“) nennen den Zustand.
let askLenaFilterLabel = "Lena"

/// Die gesetzten Filter als Chips (ledger-d9z3n), in fester Reihenfolge: „Zu prüfen“, „Lena“,
/// „Ohne Kategorie“, Kategorie, Zeitraum, Betrag, Richtung, Sortierung, „mit Gelöschten“. `sortLabel` ist der
/// Text einer anderen Sortierung als der Standard, sonst `None`. Die Suche steht im Feld, nicht hier.
let activeFilterChips
    (onlyNeedsReview: bool)
    (includeDeleted: bool)
    (sortLabel: string option)
    (categoryNames: string list)
    (f: Filters)
    : (ActiveFilter * string) list =
    [ if onlyNeedsReview then ActiveFilter.NeedsReview, "Zu prüfen"
      if f.OnlyAskLena then ActiveFilter.AskLena, askLenaFilterLabel
      if f.WithoutCategory then ActiveFilter.WithoutCategory, "Ohne Kategorie"
      if hasCategoryFilter f then ActiveFilter.Category, categoryFilterChipText (Set.count f.CategoryIds) categoryNames
      match f.DateRange with
      | Some range -> ActiveFilter.Date, dateRangeShortText range
      | None -> ()
      match amountRangeText f with
      | Some text -> ActiveFilter.Amount, text
      | None -> ()
      match directionText f.AmountDirection with
      | Some text -> ActiveFilter.Direction, text
      | None -> ()
      match sortLabel with
      | Some label -> ActiveFilter.Sort, label
      | None -> ()
      if includeDeleted then ActiveFilter.WithDeleted, "mit Gelöschten" ]

/// Die Zahl am Knopf „Filter (n)“: so viele Chips stehen darunter. Die Suche zählt nicht, sie steht
/// sichtbar im Feld daneben (Prototyp).
let filterButtonText (count: int) : string =
    if count > 0 then $"Filter ({count})" else "Filter"

/// Welcher Leerzustand gilt für eine leere Liste (ledger-v9rq3).
[<RequireQualifiedAccess>]
type EmptyListKind =
    /// Nur "Zu prüfen" aktiv: es gibt nichts mehr zu prüfen.
    | AllReviewed
    /// Nur "Ohne Kategorie" aktiv: alles hat eine Kategorie.
    | AllCategorized
    /// Filter aktiv, aber kein Treffer.
    | NoMatch
    /// Kein Filter: das Konto hat keine Buchungen.
    | NoEntries

let emptyListKind (onlyNeedsReview: bool) (f: Filters) : EmptyListKind =
    if hasAnyFilter f then EmptyListKind.NoMatch
    elif onlyNeedsReview && f.WithoutCategory then EmptyListKind.NoMatch
    elif onlyNeedsReview then EmptyListKind.AllReviewed
    elif f.WithoutCategory then EmptyListKind.AllCategorized
    else EmptyListKind.NoEntries

/// Die Aktion im leeren Zustand (ledger-rb3xw, styleguide §7.12): „Filter löschen“ bei einem Filter ohne Treffer,
/// „Abrufen“ ohne jeden Filter (Import direkt ins Kassenbuch, ADR 0093).
[<RequireQualifiedAccess>]
type EmptyListAction =
    | ClearFilters
    | Fetch

/// Welche Aktion ein leerer Zustand anbietet. „Alles geprüft“ und „Alles kategorisiert“ sind erledigt, ohne Aktion.
let emptyListAction (kind: EmptyListKind) : EmptyListAction option =
    match kind with
    | EmptyListKind.NoMatch -> Some EmptyListAction.ClearFilters
    | EmptyListKind.NoEntries -> Some EmptyListAction.Fetch
    | EmptyListKind.AllReviewed
    | EmptyListKind.AllCategorized -> None

/// Gründe, aus denen die Kategorien mit diesen Settings gar nicht geladen werden (ledger-vq2g8).
/// `Client.State` legt sie in `Categories = Failure`, der Picker zeigt sie statt des
/// allgemeinen Fehlersatzes.
let categoriesNoYnab = "YNAB ist in den Settings nicht eingerichtet"
let categoriesNoBudget = "Kein Standard-Budget in den Settings gewählt"

/// Ist dieser Fehlergrund eine fehlende Einrichtung in den Settings?
let isCategoriesSetupGap (reason: string) : bool =
    reason = categoriesNoYnab || reason = categoriesNoBudget

/// "Zuletzt verwendet" im Kategorie-Filter: höchstens so viele Kategorien, je Gerät gemerkt.
let maxRecentCategories = 5

/// Die gerade angewendeten Kategorien rücken nach vorn, Doppelte entfallen, gekappt auf
/// `maxRecentCategories`. Eine leere Auswahl ändert nichts.
let pushRecentCategories (applied: Guid Set) (recent: Guid list) : Guid list =
    if Set.isEmpty applied then recent
    else
        (Set.toList applied @ recent) |> List.distinct |> List.truncate maxRecentCategories

/// Eine im Kategorie-Picker vergebene Kategorie rückt an die Spitze derselben Liste, die der
/// Kategorie-Filter nutzt (ledger-d4msf). "Ohne Kategorie" (`None`) ändert die Liste nicht.
let rememberPickedCategory (picked: YnabCategoryId option) (recent: Guid list) : Guid list =
    match picked with
    | Some (YnabCategoryId id) -> pushRecentCategories (Set.singleton id) recent
    | None -> recent

/// Die gemerkten Kategorien als Ids für den Kategorie-Picker
let recentPickerIds (recent: Guid list) : YnabCategoryId list =
    recent |> List.map YnabCategoryId

let serializeRecentCategories (ids: Guid list) : string =
    ids |> List.map string |> String.concat ","

let parseRecentCategories (text: string) : Guid list =
    if String.IsNullOrWhiteSpace text then []
    else
        text.Split(',')
        |> Array.toList
        |> List.choose (fun part ->
            match Guid.TryParse(part.Trim()) with
            | true, g -> Some g
            | _ -> None)
        |> List.distinct
        |> List.truncate maxRecentCategories

/// "Übernehmen" in der Kategorienauswahl ist gesperrt, solange der Entwurf dem aktiven Filter gleicht.
let categoryApplyDisabled (draft: Guid Set) (applied: Guid Set) : bool = draft = applied

let recentCategoriesStorageKey = "bb.ledger.recentCategoryFilter"

[<Emit("window.localStorage.getItem($0)")>]
let private storageGet (key: string) : string = jsNative

[<Emit("window.localStorage.setItem($0, $1)")>]
let private storageSet (key: string) (value: string) : unit = jsNative

/// Liest die gemerkten Kategorien aus dem Browser; ohne Browser (Tests) oder bei gesperrtem
/// Speicher bleibt die Liste leer.
let readRecentCategories () : Guid list =
    try
        parseRecentCategories (storageGet recentCategoriesStorageKey)
    with _ ->
        []

let writeRecentCategories (ids: Guid list) : unit =
    try
        storageSet recentCategoriesStorageKey (serializeRecentCategories ids)
    with _ ->
        ()

/// Die Chips der Filterleiste, in fester Reihenfolge (PC, Handoff Screen 2.9).
/// `NeedsReviewChip` ist der bestehende Chip "Zu prüfen (n)" (ADR 0032): er zählt
/// zur Chip-Reihenfolge dazu (Handoff: "„Zu prüfen (7)" rutscht dahinter"), aber
/// nicht zu `hasAnyFilter`.
type ChipId =
    | NeedsReviewChip
    | AskLenaChip
    | CategoryChip
    | WithoutCategoryChip
    | DateChip
    | AmountChip

/// Was das Filter-Sheet unter 1024 px zeigt (ledger-npqcb, Styleguide §7.15): die Filter oder an ihrer Stelle eine
/// Auswahl. Sheets werden nicht gestapelt; Kategorie, Zeitraum und Sortieren ersetzen den Inhalt und führen nach der
/// Wahl oder mit „Zurück“ zu den Filtern. Abgeleitet aus `FilterSheetOpen`, `OpenPopover` und `SortSheetOpen`
/// (`Components.Ledger.State.filterSheetView`), die auch die Entwürfe der Auswahl tragen.
type FilterSheetView =
    | FilterSheetClosed
    | FilterSheetFilters
    | FilterSheetCategory
    | FilterSheetDate
    | FilterSheetSort

let fixedChipOrder : ChipId list =
    [ NeedsReviewChip; AskLenaChip; WithoutCategoryChip; CategoryChip; DateChip; AmountChip ]

/// Steht „Filter löschen“ in der Filterleiste am PC (ledger-cnaz3, Prototyp Version 17 `ledgerDesk`)? Sobald ein
/// Filter, einer der drei Umschalter oder die Suche gesetzt ist.
let clearFiltersChipVisible (onlyNeedsReview: bool) (f: Filters) : bool =
    hasAnyFilter f || onlyNeedsReview || f.OnlyAskLena || f.WithoutCategory

/// Der Text des Chips „Kategorie“ am PC (ledger-cnaz3): ohne Auswahl „Kategorie“, mit einer „Kategorie: <Name>“,
/// mit mehreren der erste Name und die Zahl der übrigen („Kategorie: Strom +2“).
let categoryChipText (names: string list) : string =
    match names with
    | [] -> "Kategorie"
    | [ name ] -> $"Kategorie: {name}"
    | first :: rest -> $"Kategorie: {first} +{List.length rest}"

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

/// Der Zustand von "Erneut senden" am Hinweis "Noch nicht in YNAB" (ledger-cks06), solange
/// der Push läuft. Ein gescheiterter Push steht an der Buchung (`PushToYnabFailure`).
type PushRetryRowState =
    /// Der Push dieser einen Buchung ist unterwegs.
    | PushSending

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

/// Die Beschriftung der Aktionsfläche hinter der Zeile beim Wischen nach rechts.
let swipeActionLabel = "Geprüft"

/// Die Beschriftung der Toast-Aktion.
let undoToastLabel = "Rückgängig"

/// Der Knopf einer Meldung nach einem gescheiterten Laden (ledger-gwr9j): „Erneut“.
let retryLabel = "Erneut"

/// Der Ton einer Meldung zur Art eines Toasts von heute.
let feedbackToneOf (toastType: ToastType) : FeedbackTone =
    match toastType with
    | ToastSuccess -> FeedbackOk
    | ToastWarning -> FeedbackWarning
    | ToastError -> FeedbackError
    | ToastInfo -> FeedbackInfo

/// Eine Meldung ohne Knopf.
let feedbackNote (tone: FeedbackTone) (action: string) (detail: string option) : FeedbackNote =
    { Tone = tone; Action = action; Detail = detail; Act = None }

/// Die Meldung nach dem Wisch (ledger-gwr9j, Skizze 22): „✓ Geprüft“, darunter der Payee (ohne Payee die Anzeige der
/// Liste „ohne Payee“), dazu „Rückgängig“ mit dem Vorzustand aus der Antwort.
let swipeReviewedFeedback (payee: string option) (entryId: Guid) (previous: Shared.Domain.ReviewMarkState) : FeedbackNote =
    { Tone = FeedbackOk
      Action = "Geprüft"
      Detail = Some (payee |> Option.defaultValue "ohne Payee")
      Act = Some { Label = undoToastLabel; Kind = UndoReviewMark (entryId, previous) } }

/// Ein Befehl ist gescheitert: rot, oben was nicht ging, darunter der Grund.
let failedFeedback (action: string) (reason: string) : FeedbackNote =
    feedbackNote FeedbackError action (Some reason)

/// „Rückgängig nicht möglich“ mit Grund, rot.
let undoFailedFeedback (reason: string) : FeedbackNote =
    failedFeedback "Rückgängig nicht möglich" reason

/// Der Grund, aus dem jede Aktion an einer wartenden Buchung gesperrt ist („doppelt?“, ADR 0093
/// Punkt 6), oder `None`, wenn sie nicht wartet. Erster Zweig jeder Sperre im Kassenbuch. Rein.
let heldLockReason (entry: LedgerEntryView) : string option =
    entry.PossibleDuplicate |> Option.map (fun _ -> possibleDuplicateLockedText)

/// Wirkt der Wisch auf diese Zeile? Nur eine zu prüfende, nicht in YNAB gelöschte Zeile
/// nimmt den Prüfvermerk an; auf einer geprüften Zeile bleibt es bei `NoChange`, ohne
/// Aufruf und ohne Toast.
let swipeAppliesTo (entry: LedgerEntryView) : bool =
    entry.NeedsReview && not entry.DeletedInYnab && (heldLockReason entry).IsNone

let reviewMarkHintTitle = "Prüfvermerk nicht in YNAB"

/// Texte zu "Erneut senden" am Hinweis "Noch nicht in YNAB" (ledger-cks06).
let pushRetrySentToastText = "Buchung an YNAB gesendet"
let pushRetryFailedToastTitle = "Noch nicht in YNAB"

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

/// Grund der Hinweisleiste bei einem offenen Ersatz ohne Fehlschlag (Absturz zwischen Senden und
/// Fehlschlag, ledger-f8bjm). Derselbe Satz wie `replacementPendingText` auf dem Server.
let categoryReplacementPendingReason =
    "Der Ersatz ist in YNAB noch nicht bestätigt. Erneut senden oder den Spiegel abwarten."

let private replacementPending (entry: LedgerEntryView) : bool =
    entry.SplitLocks |> Option.exists (fun l -> l.ReplacementPending)

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
    | _, Some reason when replacementPending entry ->
        Some { Title = categoryReplacementTitle; Reason = reason; RetryLabel = Some "Erneut senden" }
    | _, Some reason -> Some { Title = categoryHintTitle; Reason = reason; RetryLabel = Some "Erneut senden" }
    // ledger-f8bjm: ein offener Ersatz ohne Fehlschlag; "Erneut senden" schickt nur das POST.
    | _, None when replacementPending entry ->
        Some { Title = categoryReplacementTitle; Reason = categoryReplacementPendingReason; RetryLabel = Some "Erneut senden" }
    | _, None -> None

// ============================================
// Bestätigung am Feld im Detail (ledger-p5y9s, Regel R3 Platz 1, ADR 0098)
// ============================================

/// Unter welchem Feld des Details die Bestätigung steht.
type DetailNoteField =
    /// Unter „Kategorie“: Kategorie, Übertrag und eine aufgehobene Aufteilung.
    | AtCategory
    /// Unter den Zeilen der Aufteilung (`Detail.splitCard`).
    | AtSplits
    /// Am Betrag oben im Detail (Sheet „Betrag“, Aufteilung mit neuem Betrag, ADR 0092).
    | AtAmount
    /// Unter dem Memo-Block (ledger-enjex).
    | AtMemo
    /// Unter der Zeile „Payee“ (ledger-enjex).
    | AtPayee
    /// Unter dem Knopf „Lena fragen“ in der Knopfzeile (ledger-enjex). Ohne Text leuchtet nur der Knopf.
    | AtLena

/// Die eine Bestätigung im offenen Detail (Sheet am Handy, Panel am PC): sie steht unter dem Feld, das sich geändert
/// hat, ohne Zeitlimit, bis die nächste Aktion im Detail sie ersetzt oder das Detail schließt. Schließt es mit
/// „Rückgängig“, wandert sie in die Leiste (am PC in den Toast), dafür trägt sie Aktion und Detail der Leiste mit.
type DetailNote = {
    EntryId: Guid
    Field: DetailNoteField
    Tone: FeedbackTone
    /// Der Text unter dem Feld („Geändert“, „Nicht geändert: Grund“), ohne Zeichen.
    Text: string
    /// Die Rücknahme; dieselbe Art wie früher im Toast (`UndoCategory`, `UndoTransfer`, `UndoSplit`).
    Undo: ToastActionKind option
    /// Obere Zeile der Leiste, wenn die Meldung keinen Platz im Detail hat.
    BarAction: string
    /// Untere Zeile der Leiste (Payee oder Grund).
    BarDetail: string option
    /// Leuchtet das Feld noch? Nur nach einer Änderung, nicht bei einem Fehler.
    Flashing: bool
    /// Die Nummer der Bestätigung: das Ende des Leuchtens gilt nur für die eigene.
    Version: int
}

/// So lange leuchtet das Feld (`Feedback.flash`, Keyframes `feedbackFlash`, 0,7 s). Ohne Bewegung steht der Ring
/// so lange (`prefers-reduced-motion` in styles.css).
let detailNoteFlashMs = 700

/// Die zweite Zeile der Leiste nach einer Änderung: der Payee, ohne Payee die Anzeige der Liste.
let private notePayee (payee: string option) : string option =
    Some (payee |> Option.filter (fun p -> not (String.IsNullOrWhiteSpace p)) |> Option.defaultValue "ohne Payee")

let private changeNote entryId field tone text undo barAction barDetail : DetailNote =
    { EntryId = entryId
      Field = field
      Tone = tone
      Text = text
      Undo = undo
      BarAction = barAction
      BarDetail = barDetail
      Flashing = true
      Version = 0 }

/// Ein Fehler am Feld: rot, ohne Leuchten und ohne Rückgängig. `action` ist die obere Zeile der Leiste
/// („Kategorie nicht geändert“), `fieldText` der Anfang unter dem Feld („Nicht geändert“).
let failedDetailNote (entryId: Guid) (field: DetailNoteField) (fieldText: string) (action: string) (reason: string) : DetailNote =
    { changeNote entryId field FeedbackError $"{fieldText}: {reason}" None action (Some reason) with Flashing = false }

/// „Rückgängig nicht möglich: Grund“ rot am Feld (Zeile 2621, 2672, 3826).
let undoFailedDetailNote (entryId: Guid) (field: DetailNoteField) (reason: string) : DetailNote =
    failedDetailNote entryId field "Rückgängig nicht möglich" "Rückgängig nicht möglich" reason

/// Eine YNAB-Warnung ohne Änderung in BB (Kategorie unverändert, Versand gescheitert): gelb am Feld, ohne Leuchten.
let notInYnabDetailNote (entryId: Guid) (field: DetailNoteField) (reason: string) : DetailNote =
    { changeNote entryId field FeedbackWarning $"Nicht in YNAB: {reason}" None categoryHintTitle (Some reason) with
        Flashing = false }

/// Nach einer Änderung: „✓ <ok>“, bei gescheitertem Versand gelb „<ok>, nicht in YNAB: Grund“.
let private changedNote
    entryId field (okText: string) (barAction: string) (payee: string option) (sendFailure: string option) undo
    : DetailNote =
    match sendFailure with
    | Some reason ->
        changeNote entryId field FeedbackWarning $"{okText}, nicht in YNAB: {reason}" undo $"{barAction}, nicht in YNAB" (Some reason)
    | None -> changeNote entryId field FeedbackOk okText undo barAction (notePayee payee)

/// Kategorie geändert (Tabelle der Task, Zeile 2577/2578): „✓ Geändert“ unter „Kategorie“ mit „Rückgängig“; ohne
/// neue Kategorie „✓ Entfernt“. In der Leiste „Kategorie geändert“ bzw. „Kategorie entfernt“ mit dem Payee.
let categoryChangedNote
    (entryId: Guid) (payee: string option) (removed: bool) (sendFailure: string option) (undo: ToastActionKind)
    : DetailNote =
    if removed then changedNote entryId AtCategory "Entfernt" "Kategorie entfernt" payee sendFailure (Some undo)
    else changedNote entryId AtCategory "Geändert" "Kategorie geändert" payee sendFailure (Some undo)

/// Übertrag gesetzt (Zeile 2663): „✓ Gegenbuchung angelegt“ unter „Kategorie“ mit „Rückgängig“. In der Leiste
/// „Übertrag auf <Konto>“ mit dem Payee.
let transferSetNote
    (entryId: Guid) (account: string option) (payee: string option) (sendFailure: string option) (undo: ToastActionKind)
    : DetailNote =
    let barAction =
        match account with
        | Some name -> $"Übertrag auf {name}"
        | None -> "Übertrag gesetzt"
    changedNote entryId AtCategory "Gegenbuchung angelegt" barAction payee sendFailure (Some undo)

/// Aufteilung gespeichert (Zeile 3759/3761): „✓ Aufgeteilt in n Zeilen“ unter den Split-Zeilen, „✓ Aufteilung
/// aufgehoben“ unter „Kategorie“ (dann gibt es keine Split-Zeilen mehr). „Rückgängig“ nur, wo der Toast es hatte.
let splitSavedNote
    (entryId: Guid) (text: string) (unsplit: bool) (payee: string option) (sendFailure: string option) (undo: ToastActionKind option)
    : DetailNote =
    changedNote entryId (if unsplit then AtCategory else AtSplits) text text payee sendFailure undo

/// Betrag geändert: „✓ Geändert“ am Betrag. Aus dem Sheet „Betrag“ mit „Rückgängig“ (`UndoAmount`, ledger-enjex),
/// mit Aufteilung ohne (ADR 0092). In der Leiste „Betrag geändert“ mit dem neuen Betrag.
let amountChangedNote (entryId: Guid) (amountText: string) (sendFailure: string option) (undo: ToastActionKind option) : DetailNote =
    match sendFailure with
    | Some reason -> changeNote entryId AtAmount FeedbackWarning $"Geändert, nicht in YNAB: {reason}" undo "Betrag geändert, nicht in YNAB" (Some reason)
    | None -> changeNote entryId AtAmount FeedbackOk "Geändert" undo "Betrag geändert" (Some amountText)

/// Nach „Rückgängig“ mit gescheitertem Versand: gelb am Feld, ohne neues Rückgängig. `hintTitle` ist die obere Zeile
/// der Leiste, falls das Detail zur Antwort schon zu ist („Memo nicht in YNAB“).
let restoredNotInYnabNoteFor (hintTitle: string) (entryId: Guid) (field: DetailNoteField) (reason: string) : DetailNote =
    { changeNote entryId field FeedbackWarning $"Zurückgesetzt, nicht in YNAB: {reason}" None hintTitle (Some reason) with
        Flashing = true }

// ============================================
// Lena fragen im Detail (ledger-xfjcp, ADR 0056)
// ============================================

/// Der Zustand der Flag-Aktion einer Buchung, solange ein Aufruf läuft oder der Befehl selbst
/// scheiterte. Ein offener Fehlschlag beim Zurückschreiben nach YNAB steht an der Buchung
/// (`FlagWriteBackFailure`), nicht hier.
type FlagRowState =
    /// Der Befehl (Zuweisen, Zurücknehmen, Rückgängig oder Erneut senden) ist unterwegs.
    | FlagSending
    /// Der Befehl selbst scheiterte (unbekannte oder in YNAB gelöschte Buchung, Netz zum
    /// eigenen Server). Das Flag blieb unverändert.
    | FlagCommandFailed of message: string

/// Der Hinweis "Flag nicht in YNAB" im Detail (Muster `CategoryHint`).
type FlagHint = {
    Title: string
    Reason: string
    /// `None`, wenn nichts Offenes zu senden ist (der Befehl selbst scheiterte).
    RetryLabel: string option
}

let flagHintTitle = "Flag nicht in YNAB"

/// Welchen Hinweis trägt die Buchung? Ein Befehlsfehler hat Vorrang, danach der offene
/// Fehlschlag an der Buchung. Rein, wie `categoryHintFor`.
let flagHintFor (rowState: FlagRowState option) (entry: LedgerEntryView) : FlagHint option =
    match rowState, entry.FlagWriteBackFailure with
    | Some (FlagCommandFailed message), failure ->
        Some {
            Title = "Flag nicht geändert"
            Reason = message
            RetryLabel = failure |> Option.map (fun _ -> "Erneut senden")
        }
    | _, Some reason -> Some { Title = flagHintTitle; Reason = reason; RetryLabel = Some "Erneut senden" }
    | _, None -> None

/// Die Aktion „Lena fragen“ in der Knopfzeile des Details (ADR 0056, Punkt 2, ledger-zfj78): "Lena fragen" bei einer Buchung
/// ohne Flag oder mit einem anderen, "Rückfrage zurücknehmen" bei *Rückfrage Lena*.
/// `DisabledReason`: der Grund, aus dem sie nicht wirkt (in YNAB gelöscht, Farbe fehlt).
type LenaAction = {
    Label: string
    Target: LenaFlagTarget
    DisabledReason: string option
}

let lenaDeletedReason = "In YNAB gelöscht, das Flag lässt sich nicht ändern"

/// Der Grund, wenn die Lena-Einstellung nicht geladen werden konnte (ledger-vq2g8). Vorher
/// nannte die Sperre dann "In den Settings die Flag-Farbe für Lena wählen", obwohl die Farbe
/// gewählt war.
let lenaColorLoadFailedReason = "Die Lena-Farbe konnte nicht geladen werden"

/// Die gewählte Lena-Farbe, sobald die Einstellung geladen ist.
let lenaColorOf (lenaColor: RemoteData<string option>) : string option =
    match lenaColor with
    | Success color -> color
    | _ -> None

/// Warum "Lena fragen" ohne Farbe gesperrt ist, oder `None` mit Farbe: ein Ladefehler nennt
/// sich selbst, sonst fehlt die Farbe (oder ist noch nicht geladen).
let lenaColorBlockReason (lenaColor: RemoteData<string option>) : string option =
    match lenaColor with
    | Success (Some _) -> None
    | Failure _ -> Some lenaColorLoadFailedReason
    | _ -> Some lenaColorMissingReason

let lenaActionFor (lenaColor: RemoteData<string option>) (entry: LedgerEntryView) : LenaAction =
    let label, target =
        match entry.Flag with
        | AskLena -> "Rückfrage zurücknehmen", WithdrawLena
        | _ -> "Lena fragen", AssignLena
    let reason =
        if (heldLockReason entry).IsSome then heldLockReason entry
        elif entry.DeletedInYnab then Some lenaDeletedReason
        // Zurücknehmen braucht die Farbe nicht, nur das Zuweisen.
        elif target = AssignLena then lenaColorBlockReason lenaColor
        else None
    { Label = label; Target = target; DisabledReason = reason }

/// Deutet ein rohes Flag wie der Lesepfad (`Ledger.QueryHandlers.ReadModel.flagOf`): der Name
/// "Lena" (getrimmt, ohne Groß-/Kleinschreibung) ist *Rückfrage Lena*, jedes andere gesetzte
/// Flag *Markiert*. Der Client braucht es, um die Zeile nach einem Flag-Befehl lokal
/// nachzuziehen.
let flagOfState (state: FlagState) : LedgerFlag =
    let isLena =
        match state.Name with
        | Some name -> String.Equals(name.Trim(), lenaFlagName, StringComparison.OrdinalIgnoreCase)
        | None -> false
    if isLena then AskLena
    elif state.Color.IsSome || state.Name.IsSome then Marked
    else NoFlag

/// Der Toast nach "Lena fragen" bzw. "Rückfrage zurücknehmen".
let lenaFlagToastText (target: LenaFlagTarget) : string =
    match target with
    | AssignLena -> "Lena gefragt"
    | WithdrawLena -> "Rückfrage zurückgenommen"

/// Der Toast nach "Rückgängig".
let flagRestoredToastText : string = "Flag zurückgesetzt"

/// Hatte die Buchung vor „Lena fragen“ ein fremdes Flag (etwa rot aus YNAB)? Nur dann braucht es ein eigenes
/// „Rückgängig“: „Rückfrage zurücknehmen“ ließe die Buchung ohne Flag (Entscheidung Roman 2026-11-19, Punkt 1).
let hadForeignFlag (target: LenaFlagTarget) (previous: FlagState) : bool =
    target = AssignLena && (previous.Color.IsSome || previous.Name.IsSome)

/// Lena fragen bzw. Rückfrage zurücknehmen im Detail (ledger-enjex): der Knopf wechselt die Beschriftung und leuchtet,
/// er ist selbst das Rückgängig, darunter steht kein Text (`Text` leer). Bei einem vorherigen fremden Flag steht unter
/// dem Knopf „✓ Lena gefragt“ mit „Rückgängig“, das das alte Flag wiederherstellt (`UndoFlag` mit `Previous`). Gelb
/// bei gescheitertem Versand: „Lena gefragt, nicht in YNAB: Grund“.
let lenaFlagNote
    (entryId: Guid) (target: LenaFlagTarget) (previous: FlagState) (payee: string option) (sendFailure: string option)
    : DetailNote =
    let undo = if hadForeignFlag target previous then Some (UndoFlag (entryId, previous)) else None
    let text = lenaFlagToastText target
    match sendFailure, undo with
    | Some reason, _ -> changeNote entryId AtLena FeedbackWarning $"{text}, nicht in YNAB: {reason}" undo $"{text}, nicht in YNAB" (Some reason)
    | None, Some _ -> changeNote entryId AtLena FeedbackOk text undo text (notePayee payee)
    | None, None -> changeNote entryId AtLena FeedbackOk "" None text (notePayee payee)

// ============================================
// Memo bearbeiten im Detail (ledger-fa1fp, ADR 0062)
// ============================================

/// Der Zustand der Memo-Aktion einer Buchung, solange ein Aufruf läuft oder der Befehl selbst
/// scheiterte. Ein offener Fehlschlag beim Zurückschreiben nach YNAB steht an der Buchung
/// (`MemoWriteBackFailure`), nicht hier.
type MemoRowState =
    /// Der Befehl (Speichern, Rückgängig oder Erneut senden) ist unterwegs.
    | MemoSending
    /// Der Befehl selbst scheiterte (unbekannte oder in YNAB gelöschte Buchung, zu langes Memo,
    /// Netz zum eigenen Server). Das Memo blieb unverändert.
    | MemoCommandFailed of message: string

/// Das offene Eingabefeld des Memos im Detail: die Buchung und der Text, den Roman gerade tippt.
type MemoEdit = {
    MemoEntryId: Guid
    MemoDraft: string
}

/// Der Hinweis "Memo nicht in YNAB" im Detail (Muster `FlagHint`).
type MemoHint = {
    Title: string
    Reason: string
    /// `None`, wenn nichts Offenes zu senden ist (der Befehl selbst scheiterte).
    RetryLabel: string option
}

let memoHintTitle = "Memo nicht in YNAB"

/// Welchen Hinweis trägt die Buchung? Ein Befehlsfehler hat Vorrang, danach der offene
/// Fehlschlag an der Buchung. Rein, wie `flagHintFor`.
let memoHintFor (rowState: MemoRowState option) (entry: LedgerEntryView) : MemoHint option =
    match rowState, entry.MemoWriteBackFailure with
    | Some (MemoCommandFailed message), failure ->
        Some {
            Title = "Memo nicht geändert"
            Reason = message
            RetryLabel = failure |> Option.map (fun _ -> "Erneut senden")
        }
    | _, Some reason -> Some { Title = memoHintTitle; Reason = reason; RetryLabel = Some "Erneut senden" }
    | _, None -> None

/// Das Memo der Buchung als Text im Eingabefeld und in der Zeile: Leerraum zählt als "kein Memo".
let memoTextOf (entry: LedgerEntryView) : string option = memoKey entry.Memo

/// Die Aktion der Memo-Zeile im Detail (Tafeln 5.2 und 7.8): "Memo hinzufügen" ohne Memo,
/// sonst "Bearbeiten". `DisabledReason`: der Grund, aus dem sie nicht wirkt (in YNAB gelöscht).
type MemoAction = {
    Label: string
    DisabledReason: string option
}

let memoDeletedReason = "In YNAB gelöscht, das Memo lässt sich nicht ändern"

let memoActionFor (entry: LedgerEntryView) : MemoAction =
    { Label = (if (memoTextOf entry).IsSome then "Bearbeiten" else "Memo hinzufügen")
      DisabledReason =
        match heldLockReason entry with
        | Some reason -> Some reason
        | None -> if entry.DeletedInYnab then Some memoDeletedReason else None }

/// Der Entwurf aus der Eingabe des mehrzeiligen Memo-Felds (design-system-x045q): YNAB-Memos sind einzeilig, jeder
/// eingefügte Zeilenumbruch (`\r\n`, `\n`, `\r`) wird zu einem Leerzeichen. Das Feld bricht nur optisch um.
let memoDraftOfInput (input: string) : string =
    input.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ")

/// Warum das Eingabefeld sich nicht speichern lässt (zu lang), sonst `None`. Dieselbe Regel wie
/// auf dem Server (`Shared.Domain.normalizeMemo`).
let memoDraftProblem (draft: string) : string option =
    match normalizeMemo draft with
    | Error reason -> Some reason
    | Ok _ -> None

/// Ändert der Entwurf das Memo der Buchung? Gleiches Memo (auch mit Leerraum drumherum) ist
/// keine Änderung: Speichern schließt das Feld, ohne den Server zu rufen.
let memoDraftChangesEntry (draft: string) (entry: LedgerEntryView) : bool =
    match normalizeMemo draft with
    | Ok target -> target <> memoTextOf entry
    | Error _ -> false

/// Der Toast nach dem Speichern: gesetzt, geändert oder entfernt.
let memoToastText (previous: string option) (current: string option) : string =
    match previous, current with
    | None, Some _ -> "Memo gesetzt"
    | Some _, None -> "Memo entfernt"
    | _ -> "Memo geändert"

/// Der Toast nach "Rückgängig".
let memoRestoredToastText : string = "Memo zurückgesetzt"

// ============================================
// Betrag bearbeiten im Detail (ledger-m7mye, ADR 0092)
// ============================================

/// Der Zustand der Betrags-Aktion einer Buchung, solange ein Aufruf läuft oder der Befehl selbst
/// scheiterte. Ein offener Fehlschlag beim Zurückschreiben nach YNAB steht an der Buchung
/// (`AmountWriteBackFailure`), nicht hier.
type AmountRowState =
    | AmountSending
    | AmountCommandFailed of message: string

/// Das offene Sheet "Betrag": die Buchung, die Richtung, der Betrag ohne Vorzeichen, wie
/// Roman ihn tippt, und der Feldfehler unter dem Betragsfeld nach einem abgelehnten Speichern
/// (ledger-q42pg). Tippen löscht ihn.
type AmountEdit = {
    AmountEntryId: Guid
    AmountIsOutflow: bool
    AmountDraft: string
    AmountFieldError: string option
}

/// Der Hinweis "Betrag nicht in YNAB" im Detail (Muster `MemoHint`).
type AmountHint = {
    Title: string
    Reason: string
    RetryLabel: string option
}

let amountHintTitle = "Betrag nicht in YNAB"

/// Welchen Hinweis trägt die Buchung? Ein Befehlsfehler hat Vorrang, danach der offene
/// Fehlschlag an der Buchung. Rein, wie `memoHintFor`.
let amountHintFor (rowState: AmountRowState option) (entry: LedgerEntryView) : AmountHint option =
    match rowState, entry.AmountWriteBackFailure with
    | Some (AmountCommandFailed message), failure ->
        Some {
            Title = "Betrag nicht geändert"
            Reason = message
            RetryLabel = failure |> Option.map (fun _ -> "Erneut senden")
        }
    | _, Some reason -> Some { Title = amountHintTitle; Reason = reason; RetryLabel = Some "Erneut senden" }
    | _, None -> None

/// Warum der Betrag kein Knopf ist (ADR 0092 Punkt 3 und 4), oder `None`: der Grund aus dem
/// Lesepfad (gelöscht, abgeglichen, noch nicht in YNAB, Übertrag, Split mit Übertrags-Zeile), bei
/// einer aufgeteilten Buchung zusätzlich die Sperre ihrer Zeilen (Tracking-Konto, fremde
/// `import_id`): der Betrag geht dort nur mit einer neuen Aufteilung. Rein, wie `categoryLockFor`.
let amountLockFor (entry: LedgerEntryView) : string option =
    match entry.AmountLockReason with
    | _ when (heldLockReason entry).IsSome -> heldLockReason entry
    | Some reason -> Some reason
    | None when not (List.isEmpty entry.Splits) -> Components.Ledger.SplitEditor.lockReasonFor entry
    | None -> None

/// Das Sheet "Betrag" vorbelegt aus der Buchung: Richtung aus dem Vorzeichen, Betrag ohne
/// Vorzeichen und ohne Tausenderpunkt ("42,80").
let amountEditFor (entry: LedgerEntryView) : AmountEdit =
    { AmountEntryId = entry.Id
      AmountIsOutflow = entry.Amount.Amount < 0m
      AmountDraft = formatAmountForInput entry.Amount.Amount
      AmountFieldError = None }

/// Der Feldfehler unter dem Betragsfeld bei leerem Betrag oder 0 (ledger-q42pg).
let amountMissingText = "Betrag fehlt oder ist 0"

/// Die Id des Feldfehlers, auf die das Betragsfeld per `aria-describedby` verweist.
let amountFieldErrorId = "amount-input-error"

/// Das Betragsfeld im Sheet oder Dialog "Betrag".
let amountFieldSelector = "[data-testid='amount-input'] input"

/// Der Hinweis im Sheet bei einer aufgeteilten Buchung (Prototyp).
let amountSplitNote =
    "Die Buchung ist aufgeteilt. Nach dem Speichern passt du die Aufteilung an den neuen Betrag an."

/// Die Zeile unter dem Betragsfeld (Prototyp).
let amountYnabNote = "Geht auch an YNAB. Bei einer importierten Buchung bleibt die Bank-Referenz erhalten."

/// Die Beschriftung des Betrags-Knopfs im Kopf (Screenreader und Tooltip).
let amountEditLabel = "Betrag bearbeiten"

/// Was "Speichern" im Sheet "Betrag" tut. Rein, damit die Entscheidung ohne Proxy testbar ist.
type AmountSaveStep =
    /// Leer, 0 oder unlesbar: Feldfehler unter dem Feld, das Sheet bleibt offen.
    | AmountSaveRejected
    /// Gleicher Betrag: zurück ins Detail, kein Aufruf.
    | AmountSaveUnchanged
    /// Aufgeteilte Buchung: der Split-Editor öffnet mit dem neuen Betrag (mit Vorzeichen).
    | AmountSaveOpensSplitEditor of Money
    /// Gewöhnliche Buchung: `setEntryAmount` mit dieser Anfrage.
    | AmountSaveSends of AmountChangeRequest

/// Liest den Entwurf (ohne Vorzeichen; ein getipptes Minus zählt nicht) und entscheidet den
/// Schritt nach "Speichern".
let amountSaveStep (edit: AmountEdit) (entry: LedgerEntryView) : AmountSaveStep =
    match parseAmountInput (edit.AmountDraft.Trim().TrimStart('-')) with
    | None -> AmountSaveRejected
    | Some magnitude when magnitude <= 0m -> AmountSaveRejected
    | Some magnitude ->
        let signed = if edit.AmountIsOutflow then -magnitude else magnitude
        if signed = entry.Amount.Amount then AmountSaveUnchanged
        elif not (List.isEmpty entry.Splits) then
            AmountSaveOpensSplitEditor { entry.Amount with Amount = signed }
        else
            AmountSaveSends
                { Amount = Some magnitude
                  Direction = (if edit.AmountIsOutflow then AmountDirectionOutflow else AmountDirectionInflow) }

/// Die Anfrage, die einen Betrag mit Vorzeichen wiederherstellt ("Rückgängig").
let amountRequestOf (amount: decimal) : AmountChangeRequest =
    { Amount = Some (abs amount)
      Direction = (if amount < 0m then AmountDirectionOutflow else AmountDirectionInflow) }

/// Der Toast nach dem Ändern (Prototyp: "Betrag geändert · −45,00 €").
let amountToastText (amount: Money) : string =
    $"Betrag geändert · {Client.DesignSystem.Money.formatAmountText false true amount.Amount amount.Currency}"

/// Der Toast nach "Rückgängig".
let amountRestoredToastText : string = "Betrag zurückgesetzt"

// ============================================
// Payee ändern im Detail (ledger-fxt09)
// ============================================

/// Der Zustand der Payee-Aktion einer Buchung, solange ein Aufruf läuft oder der Befehl selbst
/// scheiterte. Ein offener Fehlschlag beim Zurückschreiben nach YNAB steht an der Buchung
/// (`PayeeWriteBackFailure`), nicht hier.
type PayeeRowState =
    | PayeeSending
    | PayeeCommandFailed of message: string

/// Die offene Payee-Auswahl: die Buchung und der Feldfehler unter dem Suchfeld nach einem
/// abgelehnten Payee (leer, zu lang, vom Server abgelehnt). Den Suchtext hält die Komponente.
type PayeeEdit = {
    PayeeEntryId: Guid
    PayeeFieldError: string option
}

/// Der Hinweis "Payee nicht in YNAB" im Detail (Muster `MemoHint`).
type PayeeHint = {
    Title: string
    Reason: string
    RetryLabel: string option
}

let payeeHintTitle = "Payee nicht in YNAB"

/// Welchen Hinweis trägt die Buchung? Ein Befehlsfehler beim erneuten Senden hat Vorrang, danach
/// der offene Fehlschlag an der Buchung. Rein, wie `memoHintFor`.
let payeeHintFor (rowState: PayeeRowState option) (entry: LedgerEntryView) : PayeeHint option =
    match rowState, entry.PayeeWriteBackFailure with
    | Some (PayeeCommandFailed message), Some _ ->
        Some { Title = payeeHintTitle; Reason = message; RetryLabel = Some "Erneut senden" }
    | _, Some reason -> Some { Title = payeeHintTitle; Reason = reason; RetryLabel = Some "Erneut senden" }
    | _, None -> None

/// Warum sich der Payee nicht ändern lässt, oder `None` (dieselbe Regel wie der Befehl:
/// zuerst die wartende Buchung, ADR 0093 Punkt 6, dann `Shared.Domain.payeeLockReason`).
let payeeLockFor (entry: LedgerEntryView) : string option =
    match heldLockReason entry with
    | Some reason -> Some reason
    | None -> payeeLockReason entry.DeletedInYnab entry.LinkedToYnab entry.IsTransfer

/// Die Beschriftung des Pfeils an der Payee-Zeile (Screenreader und Tooltip).
let payeeEditLabel = "Payee ändern"

/// Der Toast nach dem Ändern (§7.13): "Payee geändert · Rewe".
let payeeToastText (payee: string) : string = $"Payee geändert · {payee}"

/// Der Toast nach "Payee entfernen" (ledger-pkrwd, §7.13).
let payeeRemovedToastText : string = "Payee entfernt"

/// Der Toast nach "Rückgängig".
let payeeRestoredToastText : string = "Payee zurückgesetzt"

// ============================================
// Bestätigung am Feld für Memo, Payee und Lena (ledger-enjex, R3 Platz 1, ADR 0098)
// ============================================

/// Memo gespeichert: unter „Memo“ „✓ Gesetzt“, „✓ Geändert“ oder „✓ Entfernt“ mit „Rückgängig“ (`UndoMemo`); gelb
/// „…, nicht in YNAB: Grund“. In der Leiste „Memo gesetzt“ usw. mit dem Payee.
let memoChangedNote
    (entryId: Guid) (previous: string option) (current: string option) (payee: string option) (sendFailure: string option)
    : DetailNote =
    let okText =
        match previous, current with
        | None, Some _ -> "Gesetzt"
        | Some _, None -> "Entfernt"
        | _ -> "Geändert"
    changedNote entryId AtMemo okText (memoToastText previous current) payee sendFailure (Some (UndoMemo (entryId, previous)))

/// Payee geändert oder entfernt: unter „Payee“ „✓ Geändert“ bzw. „✓ Entfernt“ mit „Rückgängig“ (`UndoPayee`, auch
/// ohne alten Payee, ledger-pkrwd). In der Leiste „Payee geändert“ mit dem neuen Payee bzw. „Payee entfernt“.
let payeeChangedNote
    (entryId: Guid) (previous: string option) (current: string option) (sendFailure: string option)
    : DetailNote =
    let undo = Some (UndoPayee (entryId, previous))
    let okText, barAction = if current.IsSome then "Geändert", "Payee geändert" else "Entfernt", payeeRemovedToastText
    match sendFailure with
    | Some reason ->
        changeNote entryId AtPayee FeedbackWarning $"{okText}, nicht in YNAB: {reason}" undo $"{barAction}, nicht in YNAB" (Some reason)
    | None -> changeNote entryId AtPayee FeedbackOk okText undo barAction current

/// Der Pfeil der Payee-Zeile im Detail: dorthin kehrt der Fokus zurück, wenn die Auswahl schließt.
let payeeRowSelector = "[data-testid='detail-payee-arrow']"

/// Was ein Übernehmen in der Payee-Auswahl tut. Rein, damit die Entscheidung ohne Proxy testbar ist.
type PayeeSaveStep =
    /// Leer oder zu lang: Feldfehler unter dem Suchfeld, die Auswahl bleibt offen.
    | PayeeSaveRejected of reason: string
    /// Derselbe Payee (getrimmt, ohne Groß- und Kleinschreibung): zurück ins Detail, kein Aufruf.
    | PayeeSaveUnchanged
    /// `setEntryPayee` mit dem getrimmten Payee, `None` entfernt ihn (ledger-pkrwd).
    | PayeeSaveSends of payee: string option

let payeeSaveStep (target: string option) (entry: LedgerEntryView) : PayeeSaveStep =
    match normalizePayeeTarget target with
    | Error reason -> PayeeSaveRejected reason
    | Ok payee when payeeKey payee = payeeKey entry.Payee -> PayeeSaveUnchanged
    | Ok payee -> PayeeSaveSends payee

/// Wann der Pfeil an der Kategorie wirkt und wann nicht (ADR 0055 Punkt 7).
type CategoryLock =
    | CategoryEditable
    | CategoryLockedDeleted
    /// Die Buchung wartet als unsicheres Duplikat (ADR 0093 Punkt 6).
    | CategoryLockedHeld
    /// Split-Buchung: der Pfeil öffnet den Split-Editor (ledger-pafkp).
    | CategoryOpensSplitEditor
    /// Split-Buchung, deren Zeilen sich nicht ändern lassen (abgeglichen, Tracking-Konto,
    /// fremde `import_id`): der Pfeil bleibt ohne Wirkung, der Grund steht im Tooltip.
    | CategoryLockedSplit of reason: string

let categoryLockFor (entry: LedgerEntryView) : CategoryLock =
    if (heldLockReason entry).IsSome then CategoryLockedHeld
    elif entry.DeletedInYnab then CategoryLockedDeleted
    elif not (List.isEmpty entry.Splits) then
        match Components.Ledger.SplitEditor.lockReasonFor entry with
        | Some reason -> CategoryLockedSplit reason
        | None -> CategoryOpensSplitEditor
    // Ein Übertrag zwischen Budget-Konten ist änderbar (ledger-qvjvs): ein Konto wechselt das
    // Ziel, eine Kategorie macht ihn wieder zur normalen Buchung.
    else CategoryEditable

/// Der Hinweistext am gesperrten Pfeil; `None`, wenn die Kategorie änderbar ist.
let categoryLockHint (lock: CategoryLock) : string option =
    match lock with
    | CategoryEditable -> None
    | CategoryLockedDeleted -> Some "In YNAB gelöscht, die Kategorie lässt sich nicht ändern"
    | CategoryLockedHeld -> Some possibleDuplicateLockedText
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

// ============================================
// Regel aus der Buchung (ledger-hg5m5)
// ============================================

/// Das Angebot „Regel für „<Payee>“ anlegen?“ im Detail nach einer Kategorie-Änderung. Erst fragt der Server, ob
/// die Regeln die neue Kategorie schon geben (`Checking`); nur wenn nicht, steht das Angebot da (`Offered`). Beides
/// gehört zur Buchung im offenen Detail und zu genau dieser Kategorie.
type RuleOffer =
    | RuleOfferChecking of entryId: Guid * categoryId: YnabCategoryId
    | RuleOfferShown of entryId: Guid * categoryId: YnabCategoryId

module RuleOffer =
    let entryId (offer: RuleOffer) =
        match offer with
        | RuleOfferChecking (id, _) | RuleOfferShown (id, _) -> id

    let categoryId (offer: RuleOffer) =
        match offer with
        | RuleOfferChecking (_, c) | RuleOfferShown (_, c) -> c

/// Der Payee einer Buchung für eine Regel, ohne Payee keiner.
let rulePayeeOf (entry: LedgerEntryView) : string option =
    entry.Payee |> Option.map (fun p -> p.Trim()) |> Option.filter (fun p -> p <> "")

/// Die Kategorie einer Buchung für „Dann Kategorie“: nur eine eindeutige, nicht bei Aufteilung oder Übertrag.
let ruleCategoryOf (entry: LedgerEntryView) : YnabCategoryId option =
    if not (List.isEmpty entry.Splits) || entry.IsTransfer then None
    else entry.CategoryId |> Option.map YnabCategoryId

/// Payee und Kategorie, mit denen das Regel-Formular aus dieser Buchung vorbelegt wird. Ohne Payee gibt es keinen
/// Eintrag „Regel anlegen“.
let ruleSeedFor (entry: LedgerEntryView) : (string * YnabCategoryId option) option =
    rulePayeeOf entry |> Option.map (fun payee -> payee, ruleCategoryOf entry)

/// Die Faltung der Suche, dieselbe wie `Core.searchFold` auf dem Server (ADR 0037): `ToLowerInvariant`, das
/// auch Umlaute faltet. Der Server-Baustein liegt in `src/Server` und ist hier nicht erreichbar.
let searchFold (text: string) : string =
    if isNull text then "" else text.ToLowerInvariant()

/// Trifft die freie Suche die Buchung? Dieselbe Regel wie `buildFilterClause` auf dem Server (ledger-d9z3n):
/// Teiltext in Payee, Memo oder Kategoriename, auch im Namen einer Split-Zeile. Ohne Suche passt jede.
let private searchMatches (search: string option) (entry: LedgerEntryView) : bool =
    match search with
    | None -> true
    | Some text ->
        let needle = searchFold text
        let contains (value: string option) =
            value |> Option.exists (fun v -> (searchFold v).Contains needle)
        let splitNames =
            entry.Splits
            |> List.choose (fun s ->
                match s.Target with
                | ToCategory (_, name) -> Some name
                | _ -> None)
        contains entry.Payee
        || contains entry.Memo
        || contains entry.CategoryName
        || splitNames |> List.exists (Some >> contains)

/// Trifft der Betragsfilter die Buchung? Dieselbe Regel wie `amountMatches` auf dem Server (ADR 0095): die
/// Grenzen gelten für die Höhe ohne Vorzeichen, die Richtung trennt Ausgaben (unter 0) und Einnahmen (über 0).
let private amountMatches (filters: Filters) (amount: decimal) : bool =
    let height = abs amount
    let directionMatches =
        match filters.AmountDirection with
        | AllDirections -> true
        | OnlyOutflows -> amount < 0m
        | OnlyInflows -> amount > 0m
    directionMatches
    && (filters.AmountMin |> Option.forall (fun min -> height >= min))
    && (filters.AmountMax |> Option.forall (fun max -> height <= max))

/// Passt die Buchung nach der Änderung noch zu den aktiven Filtern? Suche, Betrag und Richtung und der Chip
/// "Rückfrage Lena" sind Und-Bedingungen. Ohne Kategorie-Filter und ohne Chip "Ohne Kategorie" passt sie bei
/// der Kategorie immer, mit beiden gilt Oder (ADR 0048). "Ohne Kategorie" trifft keinen Übertrag und keinen Split.
let rowMatchesFilters (filters: Filters) (entry: LedgerEntryView) : bool =
    let hasCategoryFilter = not (Set.isEmpty filters.CategoryIds)
    // Der Chip "Rückfrage Lena" ist eine Und-Bedingung (ADR 0056, Punkt 6).
    if filters.OnlyAskLena && entry.Flag <> AskLena then
        false
    elif not (searchMatches filters.Search entry) || not (amountMatches filters entry.Amount.Amount) then
        false
    elif not hasCategoryFilter && not filters.WithoutCategory then
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

/// Der Toast nach "Rückgängig" eines Übertrags; ohne bekannten Kontonamen nur der Satz.
let transferUndoneToastText (name: string option) : string =
    match name with
    | Some n -> $"Übertrag zurückgesetzt: {n}"
    | None -> "Übertrag zurückgesetzt"

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

/// Welche Taste ein Klick auf eine Zeile am PC trägt (ledger-cnaz3, §7.7): Strg oder Cmd nimmt die Zeile in die
/// Auswahl oder heraus, Umschalt wählt den Bereich ab dem Anker. Ein Klick ohne Taste öffnet das Detail.
type RowClickModifier =
    | ToggleRow
    | RangeToRow

/// Die Form des Buchungs-Details (ADR 0053 Punkt 1): ein Seitenpanel rechts neben der
/// Liste, oder ein BottomSheet über ihr (Handy, PC-Kompaktliste, und wo die Restbreite für
/// keine Tabellenstufe neben dem Panel reicht).
type DetailForm =
    | DetailPanel
    | DetailSheet

/// Die Form von „Neue Buchung“ außerhalb des Kassenbuchs (Übersicht, ledger-s1weh): ab 1024 px ein Dialog in
/// der Mitte, darunter das Sheet. Im Kassenbuch gilt `DetailForm` (Panel rechts oder Sheet).
type CaptureShell =
    | CaptureDialog
    | CaptureSheet

/// Wie die Liste bedient wird (ADR 0053 Punkt 6): die Breite wählt nur das Layout, die Gesten
/// folgen dem Eingabegerät. `TableRows` ist die Tabelle (ab `md`), `CompactMouse` die schmale
/// Kompaktliste mit Maus (Kästchen, Markierung, alle Tasten), `CompactTouch` die Kompaktliste
/// mit Finger (langes Drücken, Wisch). Die Ansicht misst und meldet es mit `ListInteractionChanged`.
type ListInteraction =
    | TableRows
    | CompactMouse
    | CompactTouch

// ============================================
// Durchgehen Karte für Karte (ledger-v6s7c, ADR 0086, Styleguide §7.4)
// ============================================

/// Was „Rückgängig“ in der Zeile der letzten Aktion zurücknimmt (ledger-pz5wv): eine Beschreibung, keine Funktion,
/// wie `ToastActionKind`. Der Vorzustand kommt aus der Antwort des Befehls.
type ReviewUndo =
    /// „Passt“: `restoreReviewMark` mit dem Vorzustand, die Karte kommt von rechts zurück.
    | UndoConfirmed of Guid * Shared.Domain.ReviewMarkState
    /// „Später“: kein Aufruf, die Karte kommt von links zurück.
    | UndoSkipped of Guid
    /// „Lena fragen“: `restoreFlag` mit dem Vorzustand.
    | UndoLenaAsked of Guid * Shared.Domain.FlagState
    /// „Ist doppelt, verwerfen“: `restoreDiscardedDuplicate`.
    | UndoDiscarded of Guid
    /// Payee auf der Karte: der vorige Payee (auch keiner).
    | UndoPayeeChanged of Guid * previous: string option

/// Was „Erneut“ in der Zeile noch einmal schickt: derselbe Befehl für dieselbe Buchung.
type ReviewRetry =
    | RetryConfirm of Guid
    | RetryConfirmNew of Guid
    | RetryDiscard of Guid
    | RetryLena of Guid
    | RetryPayee of Guid * payee: string option
    | RetryUndo of ReviewUndo
    /// Kategorie auf der Karte (ledger-mf2bg): dieselbe Kategorie noch einmal.
    | RetryCategory of Guid * YnabCategoryId option
    /// Übertrag auf der Karte: dasselbe Konto noch einmal.
    | RetryTransfer of Guid * YnabAccountId
    /// „Rückgängig“ am Feld der Karte: dieselbe Rücknahme noch einmal (`UndoCategory`, `UndoTransfer`).
    | RetryFieldUndo of ToastActionKind

/// Der Knopf der Zeile.
type ReviewLastAct =
    | LastUndo of ReviewUndo
    | LastRetry of ReviewRetry
    /// „Rückgängig“ einer Bestätigung aus „Details“, die beim Schließen in die Zeile kam (`handOverDetailNote`): dieselbe
    /// Rücknahme wie am Feld (`detailNoteUndoMsg`).
    | LastFieldUndo of ToastActionKind

/// Welche Aktion die Zeile meldet.
type ReviewLastKind =
    | LastConfirmed
    | LastSkipped
    | LastLenaAsked
    | LastDiscarded
    | LastConfirmedNew
    | LastPayeeChanged
    /// Nur als Fehler: „Rückgängig nicht möglich“.
    | LastUndone
    /// Kategorie auf der Karte (ledger-mf2bg): nur als Warnung oder Fehler, die Bestätigung steht am Feld.
    | LastCategoryChanged
    /// Übertrag auf der Karte: nur als Warnung oder Fehler.
    | LastTransferSet
    /// Eine Bestätigung aus „Details“ (Kategorie, Memo, Betrag, Payee, Lena, Aufteilung), die beim Schließen in die Zeile
    /// kam: Ton, Aktion und zweiter Teil wie in der Leiste (`detailNoteBar`).
    | LastFromDetail of tone: FeedbackTone * action: string * detail: string

/// Wie die Aktion ausging: erledigt, erledigt, aber nicht in YNAB (gelb, mit Grund), gescheitert (rot, mit Grund).
type ReviewLastOutcome =
    | LastDone
    | LastNotInYnab of reason: string
    | LastFailed of reason: string

/// Die letzte Aktion im Durchgehen (ledger-pz5wv, ADR 0098 Ausnahme Durchgehen): sie steht unter „Passt, weiter“, bis
/// zur nächsten Aktion oder bis man die Seite verlässt. Gleiche Aktionen werden nicht zusammengefasst.
type ReviewLast = {
    EntryId: Guid
    Kind: ReviewLastKind
    /// Der Payee der Buchung (nach „Payee geändert“ der neue), `None` ohne Payee.
    Payee: string option
    Outcome: ReviewLastOutcome
    Act: ReviewLastAct option
}

/// Ein Befehl der Karte, dessen Antwort über einen gemeinsamen Handler kommt (Flag, Payee, Rücknahmen). Nur diese
/// Antworten schreiben die Zeile; dieselbe Antwort aus „Details“ meldet sich dort.
type ReviewAwait =
    | AwaitLena of Guid
    | AwaitPayee of Guid * payee: string option
    | AwaitUndo of ReviewUndo

/// Die Bestätigung am Feld „Kategorie“ der Karte (ledger-mf2bg, R3 Punkt 1): „<Name> · <Betrag> verfügbar“,
/// „Kategorie entfernt“ oder „Gegenbuchung angelegt“, daneben „Rückgängig“. Sie bleibt, bis die Karte wechselt oder die
/// Kategorie wieder geändert wird.
type ReviewFieldNote = {
    EntryId: Guid
    /// Der Text vor dem Betrag: der Name der Kategorie, „Kategorie entfernt“ oder „Gegenbuchung angelegt“.
    Text: string
    /// Was die Kategorie danach verfügbar hat, schon formatiert („84,20 €“); `None` ohne bekannten Betrag.
    Available: string option
    /// Die Rücknahme, dieselbe Art wie früher im Toast (`UndoCategory`, `UndoTransfer`).
    Undo: ToastActionKind
    /// Leuchtet das Feld noch (`detailNoteFlashMs`)?
    Flashing: bool
    /// Wann sie kam (Zähler der Sitzung): für Taste Z und das Ende des Leuchtens.
    Stamp: int
}

/// Von welcher Seite eine zurückgeholte Karte hereinkommt (Skizze `enter-back-l`/`enter-back-r`).
type ReviewReturnSide =
    | ReturnFromLeft
    | ReturnFromRight

/// Das laufende Durchgehen. Die gezeigte Karte ist die Buchung in `DetailFor`: so laufen Kategorie,
/// Übertrag, Payee, Memo und Lena über dieselben Befehle wie im Detail.
type ReviewSession = {
    /// Die Schlange beim Öffnen, in fester Folge: „i von n“ springt nicht, auch wenn Buchungen
    /// inzwischen geprüft sind.
    Queue: RemoteData<Guid list>
    /// Der Stand jeder Buchung der Schlange, wie er beim Laden oder zuletzt gelesen wurde.
    Snapshots: Map<Guid, LedgerEntryView>
    /// Die Stelle der gezeigten Karte, 0-basiert; die Länge der Schlange heißt Endansicht.
    Index: int
    /// „Passt, weiter“ läuft für diese Buchung.
    Confirming: Guid option
    /// „Ist neu“ oder „Ist doppelt, verwerfen“ läuft für diese Buchung.
    Deciding: Guid option
    /// Die Vorschläge zum Payee der Karte, ohne die aktuelle Kategorie.
    Suggestions: YnabCategoryId list
    /// Das Buchungs-Detail („Details“) steht über der Karte.
    DetailShown: bool
    /// Die aufgelösten Treffer der Duplikat-Karten: die Buchung im Kassenbuch je `MatchEntryId`.
    Matches: Map<Guid, LedgerEntryView>
    /// Die Zeile der letzten Aktion unter „Passt, weiter“; ohne steht der Wisch-Hinweis.
    Last: ReviewLast option
    /// Befehle der Karte, deren Antwort die Zeile schreibt (`ReviewAwait`).
    Awaiting: Set<ReviewAwait>
    /// Die gezeigte Karte kam per „Rückgängig“ zurück, von dieser Seite (nur für die Einblendung).
    ReturnFrom: ReviewReturnSide option
    /// Die Bestätigung am Feld „Kategorie“ der gezeigten Karte (ledger-mf2bg); beim Kartenwechsel leer.
    FieldNote: ReviewFieldNote option
    /// Zähler der Sitzung: jede Zeile der letzten Aktion und jede Bestätigung am Feld bekommt die nächste Nummer.
    Clock: int
    /// Die Nummer der jetzigen Zeile der letzten Aktion (`Last`).
    LastStamp: int
}

/// Ein frisches Durchgehen: die Schlange lädt.
let reviewStarting : ReviewSession =
    { Queue = Loading
      Snapshots = Map.empty
      Index = 0
      Confirming = None
      Deciding = None
      Suggestions = []
      DetailShown = false
      Matches = Map.empty
      Last = None
      Awaiting = Set.empty
      ReturnFrom = None
      FieldNote = None
      Clock = 0
      LastStamp = 0 }

/// Die Seite, auf der die Karte nach „Rückgängig“ wieder hereinkommt: „Passt“ ging nach rechts, alles andere nach links.
let reviewReturnSideOf (undo: ReviewUndo) : ReviewReturnSide =
    match undo with
    | UndoConfirmed _ -> ReturnFromRight
    | UndoSkipped _ | UndoLenaAsked _ | UndoDiscarded _ | UndoPayeeChanged _ -> ReturnFromLeft

/// Das Rückgängig, das die Zeile anbietet (für Taste Z und „Letzte rückgängig“), sonst `None`.
let reviewUndoOffered (review: ReviewSession) : ReviewUndo option =
    match review.Last with
    | Some { Act = Some (LastUndo undo) } -> Some undo
    | _ -> None

/// Was Taste Z zurücknimmt (ledger-mf2bg): die Kategorie am Feld oder die Zeile unter „Passt, weiter“.
type ReviewUndoTarget =
    | UndoAtField of ToastActionKind
    | UndoAtLine of ReviewUndo

/// Die jüngste Aktion mit Rückgängig: die Bestätigung am Feld, wenn sie jünger ist als die Zeile unter dem Knopf (oder
/// die Zeile kein Rückgängig anbietet), sonst die Zeile. Ohne beides `None`. Rein.
let reviewUndoTarget (review: ReviewSession) : ReviewUndoTarget option =
    match review.FieldNote, reviewUndoOffered review with
    | Some note, Some undo -> if note.Stamp > review.LastStamp then Some (UndoAtField note.Undo) else Some (UndoAtLine undo)
    | Some note, None -> Some (UndoAtField note.Undo)
    | None, Some undo -> Some (UndoAtLine undo)
    | None, None -> None

/// Was die Zeile der letzten Aktion zeigt: Ton, Aktion (nie gekürzt), zweiter Teil (Payee oder Grund, gekürzt mit „…“)
/// und die Beschriftung des Knopfs.
type ReviewLastLine = {
    Tone: FeedbackTone
    Action: string
    Detail: string
    Button: string option
}

/// Die Zeile der letzten Aktion (ledger-pz5wv, Tabelle in Abschnitt 1): „✓ Passt · Payee · Rückgängig“, gelb
/// „! Passt, nicht in YNAB · Grund · Rückgängig“, rot „! Nicht geprüft · Grund · Erneut“. Ohne Payee „ohne Payee“. Rein.
let reviewLastLine (last: ReviewLast) : ReviewLastLine =
    let doneText =
        match last.Kind with
        | LastConfirmed -> "Passt"
        | LastSkipped -> "Später"
        | LastLenaAsked -> "Lena gefragt"
        | LastDiscarded -> "Verworfen"
        | LastConfirmedNew -> "Als neu übernommen"
        | LastPayeeChanged -> "Payee geändert"
        | LastUndone -> "Rückgängig"
        | LastCategoryChanged -> "Kategorie geändert"
        | LastTransferSet -> "Übertrag gesetzt"
        | LastFromDetail (_, action, _) -> action
    let failedText =
        match last.Kind with
        | LastConfirmed -> "Nicht geprüft"
        | LastSkipped -> "Nicht übersprungen"
        | LastLenaAsked -> "Flag nicht geändert"
        | LastDiscarded -> "Nicht verworfen"
        | LastConfirmedNew -> "Nicht übernommen"
        | LastPayeeChanged -> "Payee nicht geändert"
        | LastUndone -> "Rückgängig nicht möglich"
        | LastCategoryChanged -> "Kategorie nicht geändert"
        | LastTransferSet -> "Übertrag nicht gesetzt"
        | LastFromDetail (_, action, _) -> action
    let button =
        match last.Act with
        | Some (LastUndo _) | Some (LastFieldUndo _) -> Some "Rückgängig"
        | Some (LastRetry _) -> Some "Erneut"
        | None -> None
    match last.Kind, last.Outcome with
    | LastFromDetail (tone, action, detail), _ -> { Tone = tone; Action = action; Detail = detail; Button = button }
    | _, LastDone ->
        { Tone = FeedbackOk; Action = doneText; Detail = last.Payee |> Option.defaultValue "ohne Payee"; Button = button }
    | _, LastNotInYnab reason ->
        { Tone = FeedbackWarning; Action = $"{doneText}, nicht in YNAB"; Detail = reason; Button = button }
    | _, LastFailed reason ->
        { Tone = FeedbackError; Action = failedText; Detail = reason; Button = button }

/// Die Art der Karte: die normale oder die Duplikat-Karte einer wartenden Buchung (ADR 0093).
type ReviewCardKind =
    | NormalCard
    | DuplicateCard

let reviewCardKindOf (entry: LedgerEntryView) : ReviewCardKind =
    if entry.PossibleDuplicate.IsSome then DuplicateCard else NormalCard

/// Hat die Buchung ein Ziel, ist „Passt“ frei: eine Kategorie, ein Übertrag oder eine Aufteilung
/// (Sinn von „Ohne Kategorie“, ADR 0048). Ohne Ziel steht „Erst Kategorie wählen“.
let reviewHasTarget (entry: LedgerEntryView) : bool =
    entry.CategoryId.IsSome || entry.CategoryName.IsSome || entry.IsTransfer || not (List.isEmpty entry.Splits)

/// Muss die Karte beim Erreichen übersprungen werden? Inzwischen geprüft, gelöscht oder unbekannt.
let reviewSkipsEntry (entry: LedgerEntryView option) : bool =
    match entry with
    | Some e -> not e.NeedsReview || e.DeletedInYnab
    | None -> true

/// Ab wie viel Pixeln Wischen eine Entscheidung ist (Prototyp `touchend`).
let reviewSwipeThresholdPx = 90.0

type ReviewSwipe =
    | SwipeConfirm
    | SwipeSkip
    | SwipeNothing

/// Die Entscheidung am Ende eines Wischs auf der Karte: mehr als 90 px nach rechts „Passt“, mehr als
/// 90 px nach links „Später“, dazwischen nichts. Nur ein waagrechter Wisch zählt (`abs dx > abs dy`):
/// eine schräge Scrollbewegung mit 90 px seitlich setzt keinen Prüfvermerk. Rein.
let reviewSwipeOf (dx: float) (dy: float) : ReviewSwipe =
    if abs dx <= abs dy then SwipeNothing
    elif dx > reviewSwipeThresholdPx then SwipeConfirm
    elif dx < -reviewSwipeThresholdPx then SwipeSkip
    else SwipeNothing

/// Bis wohin die Karte ohne Ziel nach rechts dem Finger 1:1 folgt; danach geht sie zäh mit (Skizze ledger-vf5h6 `eff`).
let reviewSwipeStiffFromPx = 72.0

/// Wie weit die Karte beim Wischen steht, aus dem Weg des Fingers: 1:1, ohne Ziel nach rechts ab 72 px zäh
/// (72 + Rest × 0,12), damit sie nie bis zur Grenze kommt. Rein.
let reviewSwipeOffset (hasTarget: bool) (dx: float) : float =
    if hasTarget || dx <= reviewSwipeStiffFromPx then dx
    else reviewSwipeStiffFromPx + (dx - reviewSwipeStiffFromPx) * 0.12

/// Ist die Fläche unter der Karte eingerastet? Genau dann, wenn `reviewSwipeOf` beim Loslassen eine Entscheidung
/// träfe, nach rechts nur mit Ziel (ohne Ziel rastet nichts ein). Rein.
let reviewSwipeArmed (hasTarget: bool) (dx: float) (dy: float) : bool =
    match reviewSwipeOf dx dy with
    | SwipeConfirm -> hasTarget
    | SwipeSkip -> true
    | SwipeNothing -> false

/// Was die Fläche unter der Karte bei diesem Weg zeigt: rechts mit Ziel Haken über „Passt“, rechts ohne Ziel nur
/// das gelbe „!“, links Uhr über „Später“; eingerastet nach `reviewSwipeArmed`. Ohne Weg nichts. Rein.
let reviewSwipeSurface (hasTarget: bool) (dx: float) (dy: float) : Client.DesignSystem.ReviewCard.SwipeSurface =
    let armed = reviewSwipeArmed hasTarget dx dy
    if dx > 0.0 then
        if hasTarget then Client.DesignSystem.ReviewCard.SurfaceConfirm armed
        else Client.DesignSystem.ReviewCard.SurfaceNoTarget
    elif dx < 0.0 then Client.DesignSystem.ReviewCard.SurfaceSkip armed
    else Client.DesignSystem.ReviewCard.SurfaceRest

/// Was eine Taste im Durchgehen auslöst (PC, Styleguide §7.8 Punkt 6).
type ReviewKeyAction =
    | KeyConfirm
    | KeySkip
    | KeyOpenCategory
    | KeyLena
    | KeyLeave
    /// Z: nimmt die letzte Aktion der Zeile zurück (ledger-pz5wv).
    | KeyUndo

/// Die Tastenbelegung des Durchgehens: Enter passt (nur mit Ziel, nicht auf der Duplikat-Karte),
/// S später, K öffnet die Kategorieauswahl und L fragt Lena bzw. nimmt die Rückfrage zurück (beide
/// nicht auf der Duplikat-Karte), Esc führt zurück, Z nimmt die letzte Aktion zurück, wenn die Zeile ein
/// Rückgängig anbietet (`undoOffered`), auch in der Endansicht. Ohne Karte (Endansicht) sonst nur Esc. Rein.
let reviewKeyAction (card: (ReviewCardKind * bool) option) (undoOffered: bool) (key: string) : ReviewKeyAction option =
    match key, card with
    | "Escape", _ -> Some KeyLeave
    | ("z" | "Z"), _ -> if undoOffered then Some KeyUndo else None
    | _, None -> None
    | "Enter", Some (NormalCard, true) -> Some KeyConfirm
    | ("s" | "S"), Some _ -> Some KeySkip
    | ("k" | "K"), Some (NormalCard, _) -> Some KeyOpenCategory
    | ("l" | "L"), Some (NormalCard, _) -> Some KeyLena
    | _ -> None

/// Die Bestätigung am Feld nach einer Kategorie auf der Karte (ledger-mf2bg): Name und was die Kategorie danach
/// verfügbar hat („Bahn“, „84,20 €“), ohne bekannten Betrag nur der Name, ohne Kategorie „Kategorie entfernt“. Rein.
let reviewCategoryNoteText (name: string option) (available: Money option) : string * string option =
    match name, available with
    | Some n, Some a -> n, Some (Client.DesignSystem.Money.formatAmountText false true a.Amount a.Currency)
    | Some n, None -> n, None
    | None, _ -> "Kategorie entfernt", None

/// Die Bestätigung am Feld nach einem Übertrag auf der Karte; das Feld zeigt „→ <Konto>“.
let reviewTransferNoteText = "Gegenbuchung angelegt"

/// Der Toast nach dem Memo auf der Karte.
let reviewMemoToastText (current: string option) : string =
    match current with
    | Some _ -> "Memo gespeichert"
    | None -> "Memo entfernt"

let reviewConfirmedNewText = "Als neu übernommen, geht nach YNAB"
let reviewDiscardedText = "Verworfen, nicht nach YNAB gegangen"
let reviewRestoredText = "Steht wieder zur Entscheidung"

/// Die Zeile der Endansicht: wie viele Buchungen die Schlange hatte (Einzahl „1 Buchung“).
let reviewDoneText (count: int) : string =
    let tail = "Neue erscheinen nach dem nächsten Import."
    if count = 0 then tail
    elif count = 1 then $"1 Buchung durchgegangen. {tail}"
    else $"{count} Buchungen durchgegangen. {tail}"

/// „i von n“ im Kopf; in der Endansicht steht nichts.
let reviewProgressText (index: int) (count: int) : string option =
    if index < count then Some $"{index + 1} von {count}" else None

/// Die Füllung des Fortschrittsbalkens in Prozent: (i−1)/n, voll in der Endansicht.
let reviewProgressPercent (index: int) (count: int) : float =
    if count <= 0 || index >= count then 100.0 else float index / float count * 100.0

/// Der laufende Sammellauf der Auswahl (ledger-sjbvc), je Sammelaktion ein Fall. Ihr Rückgängig
/// läuft im Fall der Aktion. Es läuft höchstens einer: alle Sammelaktionen und ihr Rückgängig
/// sind gesperrt, solange einer läuft.
type BatchRun =
    | ReviewMarkBatchRun
    | CategoryBatchRun
    | LenaBatchRun

/// Was an der Liste gerade lädt: „Weitere laden“ oder das Nachladen im Hintergrund (ledger-ct3cx). Beides
/// läuft nie zugleich, ein Zustand statt zweier Felder kennt nur die gültigen Kombinationen (ledger-pz028).
type ListActivity =
    /// Nichts läuft und nichts wartet.
    | ListIdle
    /// „Weitere laden“ läuft: die bisherige Liste bleibt stehen, nur ein Skeleton steht am Ende (AC5).
    | LoadingMorePage
    /// Das Nachladen kam, während „Weitere laden“ lief: es wartet auf dessen Antwort und holt danach
    /// den ganzen Ausschnitt samt der neuen Seite.
    | LoadingMoreThenRefresh
    /// Das Nachladen im Hintergrund läuft. `basis` sind die Zeilen beim Start: ändert ein Einzelbefehl (Kategorie,
    /// Prüfvermerk, Memo, Lena, Betrag, Payee, Split, Übertrag) bis zur Antwort eine Zeile, ist die
    /// Antwort älter als die Liste und wird verworfen. Solange ruht „Weitere laden“: eine angehängte
    /// Seite aus dem alten Ausschnitt passte nicht zur neuen Liste.
    | Refreshing of basis: LedgerEntryView list

/// Der Abruf der Kategorien (ledger-pfhdq, Vorbild `ListActivity`).
type CategoriesFetch =
    /// Kein Abruf läuft.
    | CategoriesIdle
    /// Ein Abruf läuft, gestartet mit dieser `CategoriesGeneration`. Ein neuer Start ersetzt ihn.
    | CategoriesFetching of generation: int

/// Wie weit die Zeile am Platz einer gelöschten Buchung ist (ledger-k68sd, Regel R3). `DeletedRowShown`: gelöscht,
/// mit „Rückgängig“. `DeletedRowRestoring`: das Wiederherstellen läuft. `DeletedRowRestoreFailed`: rot mit Grund,
/// „Rückgängig“ bleibt zum erneuten Versuch.
type DeletedRowPhase =
    | DeletedRowShown
    | DeletedRowRestoring
    | DeletedRowRestoreFailed of reason: string

/// Die Zeile am Platz einer in BB gelöschten Buchung, bis zur nächsten Aktion (ledger-k68sd). `Entry` ist der Stand
/// vor dem Löschen; die Zeile steht vor `Before` (der Buchung, die in der Liste danach kam) oder, fehlt sie, an
/// `Index`. So bleibt der Platz, auch wenn das Nachladen die Buchung aus der Liste nimmt.
type DeletedEntryRow = {
    Entry: LedgerEntryView
    Index: int
    Before: Guid option
    Phase: DeletedRowPhase
}

type Model = {
    /// Die Konten samt Vorauswahl des Servers.
    Accounts: RemoteData<LedgerAccountsView>

    /// Die Kontostaende aller Konten in einem Aufruf (ledger-b1xwe AC2). Unabhaengig
    /// von `Entries`: sie kommen nie aus der geladenen Liste.
    Balances: RemoteData<LedgerAccountBalanceView list>

    /// Zählt die Starts von `LoadBalances` (ledger-pfhdq). `BalancesLoaded` trägt die Nummer seines
    /// Starts; eine Antwort auf einen älteren Start fällt weg, Erfolg wie Fehler.
    BalancesGeneration: int

    /// Das Nachladen der Kontostände ist gescheitert, während Stände standen (ledger-pfhdq). Die Beträge
    /// bleiben, darüber steht „Kontostände konnten nicht aktualisiert werden“. Die nächste erfolgreiche
    /// Antwort löscht den Grund, ein neuer Start nicht (sonst blinkt der Hinweis bei „Erneut laden“).
    BalancesRefreshError: string option

    /// Der gerade aktive Filter. `None`, solange nichts geladen ist oder das
    /// Kassenbuch leer ist (kein Konto zum Filtern).
    SelectedFilter: AccountFilter option

    /// Die je Gerät gemerkte Sicht (ledger-ybmqc), beim `init` aus dem Gerätespeicher
    /// gelesen. Sie gilt nur für die Vorauswahl beim Öffnen des Kassenbuchs.
    RememberedFilter: AccountFilter option
    /// Die Buchung der Route `#/ledger/entry/<id>`, die gerade nachgeladen wird
    /// (budgeting-m1bce). Eine Antwort zählt nur, solange sie hier steht.
    PendingRouteEntry: Guid option

    /// Ist der Konto-Wechsler (BottomSheet am Handy) offen? (ledger-ybmqc)
    AccountSheetOpen: bool

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

    /// Die Buchung, deren Detail gerade offen steht (Panel am PC, Sheet am Handy und in der
    /// Kompaktliste, ledger-q1hw2, ADR 0053). Eine Momentaufnahme: die Ansicht liest den
    /// aktuellen Stand aus der Liste, wenn die Buchung dort steht (`detailEntryOf`).
    DetailFor: LedgerEntryView option

    /// In welcher Form das Detail aufgeht. Die Ansicht misst die Tabellenbreite und meldet
    /// die Form mit `DetailFormChanged`; sie hängt nur von der Breite ohne Panel ab und
    /// kippt deshalb nicht hin und her, wenn das Panel selbst Platz nimmt.
    DetailForm: DetailForm

    /// Das offene Memo-Eingabefeld im Detail (ledger-fa1fp, ADR 0062): höchstens eines, es
    /// gehört zur Buchung im offenen Detail und schließt mit ihm.
    MemoEdit: MemoEdit option

    /// Der Zustand der Memo-Aktion je Buchung (ledger-fa1fp): unterwegs oder Befehl gescheitert.
    MemoRows: Map<Guid, MemoRowState>

    /// Das offene Sheet "Betrag" (ledger-m7mye, ADR 0092): höchstens eines, für die Buchung im
    /// offenen Detail.
    AmountEdit: AmountEdit option

    /// Der Zustand der Betrags-Aktion je Buchung (ledger-m7mye): unterwegs oder Befehl gescheitert.
    AmountRows: Map<Guid, AmountRowState>

    /// Die offene Payee-Auswahl (ledger-fxt09): höchstens eine, für die Buchung im offenen Detail.
    PayeeEdit: PayeeEdit option

    /// Der Zustand der Payee-Aktion je Buchung (ledger-fxt09): unterwegs oder Befehl gescheitert.
    PayeeRows: Map<Guid, PayeeRowState>

    /// Die Payees des ganzen Kassenbuchs für die Payee-Auswahl, häufigste zuerst
    /// (`getLedgerPayeeSuggestions` ohne Filter). Lädt beim ersten Öffnen und nach jeder Änderung neu.
    PayeeChoices: RemoteData<LedgerPayeeSuggestion list>

    /// Die Lade-Nummer der Payee-Auswahl: jede Änderung eines Payees zählt sie weiter, eine ältere
    /// Ladeantwort gilt dann nicht mehr.
    PayeeChoicesGeneration: int

    /// Wie die Liste bei der gemessenen Breite und dem Eingabegerät bedient wird (ledger-m6zpd).
    /// Die Ansicht meldet es mit `ListInteractionChanged`; der Fußzeilen-Hinweis liest nur dieses Feld.
    ListInteraction: ListInteraction

    /// Die Historie der offenen Buchung.
    History: RemoteData<LedgerHistoryLine list>

    /// Die Lade-Nummer der Historie: jedes gestartete Laden zählt sie weiter, nur die Antwort des
    /// zuletzt gestarteten gilt (ledger-nc708, Vorbild `HistoryGeneration` im Budget).
    HistoryGeneration: int
    /// Die Buchung, deren Historie im Detail aufgeklappt ist (ledger-zfj78). Zu beim Öffnen des Details: ein Wechsel
    /// auf eine andere Buchung zeigt sie zu, `closeDetail` und `showDetail` setzen `None`.
    HistoryOpenFor: Guid option

    /// Zeitpunkt und Ergebnis des zuletzt gelaufenen Spiegels.
    LastMirrorRun: RemoteData<MirrorRunStatus option>

    /// Zählt die Starts von `LoadMirrorStatus` (ledger-pfhdq). Nur die Antwort auf den zuletzt
    /// gestarteten Abruf gilt; eine ältere fällt weg und startet keinen Spiegel-Lauf.
    MirrorStatusGeneration: int

    /// Laeuft gerade ein Spiegel-Lauf? Die Liste bleibt waehrenddessen stehen:
    /// Roman wartet nie auf den Spiegel (ADR 0014).
    MirrorRunning: bool

    /// Läuft der Spiegel im Hintergrund (Frische-Kriterium, ADR 0014) statt nach „Jetzt spiegeln“? Dann meldet er sich
    /// nur, wenn der Meldungsplatz frei ist (`PostFeedbackIfFree`, ledger-gwr9j).
    MirrorInBackground: bool

    /// Der Scrollstand der Buchungen beim Wechsel zum Tab „Regeln“ (categorization-n3sxw). Der Tab „Buchungen“
    /// baut sich beim Zurückwechseln neu auf und setzt ihn wieder. Er gilt nur für den Weg Regeln → Buchungen:
    /// verlässt Roman die Regeln zu einem anderen Ziel, wird er verworfen.
    BookingsScroll: float option

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

    /// Der laufende Abruf der Kategorien samt Nummer seines Starts. Während er läuft, bleibt der
    /// alte Stand sichtbar (`Categories` bleibt `Success`), und `shouldReloadCategories` startet
    /// keinen zweiten (ledger-pfhdq).
    CategoriesFetch: CategoriesFetch

    /// Steigt bei jedem Start eines Kategorien-Abrufs und bei jeder lokalen Fortschreibung von
    /// Available (`shiftAvailable`, ledger-pfhdq). `CategoriesLoaded` trägt die Nummer seines Starts;
    /// eine Antwort mit anderer Nummer setzt weder `Categories` noch `CategoriesLoadedAt`.
    CategoriesGeneration: int

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
    /// Zuletzt im Kategorie-Filter angewendete Kategorien, neueste zuerst (je Gerät gemerkt).
    RecentCategories: Guid list

    /// Der Entwurf des Zeitraum-Filters, solange die Zeitraum-Auswahl offen ist.
    DateDraft: DateRangeFilter
    /// Ist die Eingabe in Von/Bis ungültig (z. B. "31.02.2026")? Solange eine Grenze
    /// ungültig ist, bleibt "Übernehmen" gesperrt.
    DateDraftFromInvalid: bool
    DateDraftToInvalid: bool

    /// Der Text im Suchfeld (ledger-d9z3n); ein neuer Tastendruck erhöht `SearchVersion`
    /// und verwirft eine noch laufende, ältere Verzögerung (Debounce-Muster wie
    /// `SyncFlow.State`s `PendingPayeeVersions`, hier ohne Map: es gibt nur ein
    /// Suchfeld je Seite). Erst nach der Verzögerung gilt er als `Filters.Search`.
    SearchDraft: string
    SearchVersion: int

    /// Ist das Filter-Sheet unter 1024 px offen (ledger-d9z3n)? Ist dazu die Kategorie- oder Zeitraum-Auswahl
    /// (`OpenPopover`) oder die Sortierung (`SortSheetOpen`) offen, steht sie im Sheet an Stelle der Filter
    /// (ledger-npqcb, `filterSheetView`).
    FilterSheetOpen: bool

    /// Der Entwurf des Betrags-Suchfelds (Von/Bis), mit derselben
    /// Debounce-Versionierung wie `SearchDraft`.
    AmountMinDraft: string
    AmountMaxDraft: string
    AmountVersion: int

    /// Der Zustand der Prüfvermerk-Schaltfläche je Buchung (ledger-r777c): läuft ein
    /// Aufruf oder scheiterte der Befehl. Zeilen ohne Eintrag sind im Ruhezustand.
    ReviewMarkRows: Map<Guid, ReviewMarkRowState>

    /// Der Sende-Zustand von "Erneut senden" am Hinweis "Noch nicht in YNAB" je Buchung
    /// (ledger-cks06). Zeilen ohne Eintrag sind im Ruhezustand.
    PushRetryRows: Map<Guid, PushRetryRowState>

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

    /// Welcher Sammel-Aufruf der Auswahl gerade läuft ("Geprüft", "Kategorie setzen", "Lena
    /// fragen" bzw. "Rückfrage zurücknehmen", oder ihr Rückgängig). Solange einer läuft, sind
    /// alle Sammelaktionen der Leiste gesperrt, ein zweiter Klick löst keinen zweiten Aufruf aus
    /// (ledger-sjbvc).
    BatchRunning: BatchRun option

    /// Die Fokuszeile der Tastaturnavigation am PC (ledger-rjb8w, `keyboardCursor` aus
    /// Handoff Ablauf 7). `None`, bis die erste Taste sie setzt. Sie überlebt das
    /// Neuladen der Liste; fällt die Zeile danach aus der Liste, springt der Fokus auf die
    /// erste Zeile.
    FocusedRow: Guid option

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

    /// Das Quick-Add-Konto aus den Einstellungen (ADR 0004), Ziel des Übertrags bei "Barabhebung" im
    /// Split-Editor (ledger-s8t3s). Der Root füllt es aus seinen Settings (ledger-pz028); `Success None`, wenn
    /// keins eingestellt ist.
    QuickAddAccount: RemoteData<YnabAccountId option>

    /// Die Konten des Budgets für das Übertrag-Ziel je Zeile; der Editor zeigt davon die offenen
    /// Budget-Konten (`openOnBudgetAccounts`).
    TransferTargets: RemoteData<YnabAccount list>

    /// Das offene Formular "Neue Buchung" (ledger-at63w), `None` wenn zu. Am PC im Panel, am Handy
    /// als Sheet (dieselbe Form wie das Detail, `DetailForm`). Höchstens eines von Detail und Formular.
    Capture: Capture.CaptureState option

    /// Buchungen, deren "Rückgängig" nach dem Anlegen gerade läuft: ein zweiter Tipp schickt nichts.
    DiscardingCaptured: Set<Guid>

    /// Buchungen, deren "Buchung löschen" gerade läuft (ledger-z6sya): ein zweiter Tipp schickt nichts.
    DeletingEntries: Set<Guid>

    /// Buchungen, deren "Rückgängig" nach dem Löschen gerade läuft: ein zweiter Tipp schickt nichts.
    RestoringEntries: Set<Guid>

    /// Die Zeile am Platz der zuletzt gelöschten Buchung (ledger-k68sd), `None` ohne. Endet mit der nächsten Aktion
    /// (`State.endsDeletedRow`), einem Neuaufbau der Liste und beim Verlassen der Seite.
    DeletedRow: DeletedEntryRow option

    /// Warum „Buchung löschen“ scheiterte, rot unter dem Knopf im offenen Detail dieser Buchung (ledger-k68sd).
    DeleteFailure: (Guid * string) option

    /// Die Vorschläge zum Payee für den offenen Picker (höchstens drei, häufigste zuerst).
    /// Leer, bis die Antwort da ist, bei einem Fehler und ohne Payee.
    CategorySuggestions: YnabCategoryId list

    /// Der Zustand der Kategorie je Buchung (Aufruf läuft, Befehl scheiterte).
    CategoryRows: Map<Guid, CategoryRowState>

    // ============================================
    // Lena fragen im Detail (ledger-xfjcp, ADR 0056)
    // ============================================

    /// Der Zustand der Flag-Aktion je Buchung (Aufruf läuft, Befehl scheiterte).
    FlagRows: Map<Guid, FlagRowState>

    /// Die Flag-Farbe, die Roman in den Settings für "Lena" gewählt hat, als Ladezustand der
    /// Einstellung (`Success None`: keine Farbe gewählt). Ohne Farbe ist "Lena fragen" gesperrt,
    /// ein Ladefehler nennt einen eigenen Grund (ledger-vq2g8).
    LenaFlagColor: RemoteData<string option>

    /// Die Zahl der Buchungen mit Flag "Lena" im aktiven Konto-Filter, wie die zuletzt geladene
    /// Seite sie gemeldet hat (unabhängig vom Chip "Rückfrage Lena").
    AskLenaCount: int
    /// Die Zahl der Buchungen ohne Kategorie im Konto (`LedgerEntryPage.WithoutCategoryCount`),
    /// hinter dem Schalter „Ohne Kategorie“ im Filter-Sheet (ledger-d9z3n).
    WithoutCategoryCount: int

    /// Die Karte "Budget nach dem Wechsel", solange sie steht.
    CategoryCard: CategoryChangeCardState option
    CategoryCardVersion: int

    /// Das Angebot „Regel anlegen?“ nach einer Kategorie-Änderung im Detail (ledger-hg5m5).
    RuleOffer: RuleOffer option

    /// Die eine Bestätigung am Feld im offenen Detail (ledger-p5y9s, R3 Platz 1).
    DetailNote: DetailNote option

    /// Zählt die Bestätigungen am Feld; jede neue bekommt die nächste Nummer (Ende des Leuchtens).
    DetailNoteVersion: int

    /// Zeilen, die nach einer Änderung nicht mehr zum aktiven Filter passen. Sie bleiben
    /// ausgegraut stehen (ADR 0055, Nachtrag). Neuladen, Filter- und Sortierwechsel leeren die Menge.
    FilteredOut: Set<Guid>

    /// Hat eine Änderung den Sortierschlüssel verschoben (`SortByCategory`)? Dann ruht das
    /// Nachladen, bis "Neu laden" die Liste ab Seite 1 neu aufsetzt.
    OrderStale: bool

    /// Zählt jeden Neuaufbau der Buchungsliste (infrastructure-r3tw7). Listenabfragen tragen
    /// die Generation ihres Starts mit. Eine Antwort einer älteren Generation gehört zu einem
    /// Filter oder einer Sortierung, die nicht mehr gilt, und wird verworfen.
    EntriesGeneration: int

    /// Zählt jedes Nachladen im Hintergrund (ledger-ct3cx): nach einem Spiegel-Lauf, einem Abruf
    /// oder „Rückgängig“ auf ein Verwerfen holt die Liste denselben Ausschnitt neu, statt neu
    /// aufzubauen. Die Antwort trägt diese Zahl und die `EntriesGeneration` ihres Starts; ein
    /// späteres Nachladen oder ein Neuaufbau macht sie ungültig. Eine eigene Zahl, weil ein
    /// Hochzählen der Generation laufende Summen- und Payee-Abfragen verwerfen würde.
    RefreshGeneration: int

    /// Was an der Liste lädt: „Weitere laden“ oder das Nachladen im Hintergrund (ledger-ct3cx, ledger-pz028).
    /// Anders als `Entries = Loading` (Erstladen) bleibt die bisherige Liste dabei stehen.
    ListActivity: ListActivity

    /// Das laufende Durchgehen (ledger-v6s7c), `None` außerhalb der Seite.
    Review: ReviewSession option

    /// Zählt jedes Öffnen des Durchgehens. Die Abfrage der Schlange trägt die Generation ihres Starts
    /// mit; die späte Antwort eines früheren Öffnens setzt die laufende Sitzung nicht auf Karte 1 zurück.
    /// Am Modell und nicht in `ReviewSession`, weil das Verlassen die Sitzung verwirft.
    ReviewGeneration: int

    /// Buchungen, deren „Rückgängig“ nach dem Verwerfen gerade läuft: ein zweiter Tipp schickt nichts.
    RestoringDuplicates: Set<Guid>
}

/// Ist Von oder Bis des Zeitraum-Entwurfs ungültig eingegeben? Dann ist "Übernehmen"
/// gesperrt, und `ApplyDateFilter` ändert den Filter nicht.
let dateDraftHasError (model: Model) : bool =
    model.DateDraftFromInvalid || model.DateDraftToInvalid

/// Steht das Detail als Seitenpanel offen? Nur dann folgt es der Markierung und bleiben die
/// Listentasten an (ledger-q1hw2); ein offenes Sheet verdeckt die Liste und sperrt sie.
let detailPanelOpen (model: Model) : bool =
    model.DetailFor.IsSome && model.DetailForm = DetailPanel

/// Die Schlange des Durchgehens, sobald sie geladen ist.
let reviewQueueOf (model: Model) : Guid list option =
    match model.Review with
    | Some { Queue = Success ids } -> Some ids
    | _ -> None

/// Die gezeigte Karte des Durchgehens (ledger-v6s7c): die Buchung in `DetailFor` an der Stelle
/// `Index` der Schlange. `None` außerhalb des Durchgehens, beim Laden und in der Endansicht.
let reviewCardOf (model: Model) : LedgerEntryView option =
    match model.Review, reviewQueueOf model with
    | Some review, Some ids when review.Index < List.length ids ->
        model.DetailFor |> Option.filter (fun d -> d.Id = ids.[review.Index])
    | _ -> None

/// Ist diese Buchung die gezeigte Karte des Durchgehens?
let isReviewCard (model: Model) (entryId: Guid) : bool =
    reviewCardOf model |> Option.exists (fun e -> e.Id = entryId)

/// Wohin die Route `#/ledger/entry/<id>` verlassen wurde (budgeting-m1bce).
type RouteLeftFor =
    /// Die Liste bleibt sichtbar und mit ihr der Konto-Filter.
    | LedgerList
    /// Das Kassenbuch ist verlassen: der Filter des Sprungs war nur ein Abstecher.
    | OtherPage

/// Der Konto-Filter, unter dem die Buchung einer Route steht: "Alle Konten" und ihr eigenes Konto
/// bleiben, sonst ihr Konto.
let filterForEntry (current: AccountFilter option) (entry: LedgerEntryView) : AccountFilter =
    match current with
    | Some AllAccounts -> AllAccounts
    | _ -> OneAccount entry.AccountId

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

/// Steht das Buchungs-Detail dieser Buchung offen? Im Durchgehen nur, wenn „Details“ über der Karte stehen.
let detailShows (model: Model) (entryId: Guid) : bool =
    model.DetailFor |> Option.exists (fun d -> d.Id = entryId)
    && (match model.Review with
        | Some review -> review.DetailShown
        | None -> true)

/// Wo der Payee im Durchgehen öffnet (ledger-hme9k): mit offenen „Details“ im Detail (Inhalt des Sheets, am PC ein
/// Dialog), sonst als eigene Ebene über der Karte. Nie beides übereinander. Das Memo öffnet seit ledger-wg42w nur noch
/// in „Details“; auf der Karte steht es nur zum Lesen.
[<RequireQualifiedAccess>]
type ReviewEditorPlace =
    | OverCard
    | InDetail

let reviewEditorPlace (review: ReviewSession) : ReviewEditorPlace =
    if review.DetailShown then ReviewEditorPlace.InDetail else ReviewEditorPlace.OverCard

/// Steht das Detail-Sheet im Durchgehen am Handy offen (ledger-hme9k Punkt 5)? Nur mit Karte und offenen „Details“.
/// Das Sheet selbst steht immer im DOM und fährt über `IsOpen` ein und aus; vorher hing es erst mit „Details“ ein und
/// beim Schließen aus, deshalb fuhr es weder ein noch aus.
let reviewDetailSheetOpen (card: LedgerEntryView option) (review: ReviewSession) : bool =
    card.IsSome && review.DetailShown

/// Darf das Angebot „Regel anlegen?“ stehen bleiben (ledger-hg5m5)? Nur solange das Detail seiner Buchung offen ist
/// und die Buchung noch einen Payee und genau die angebotene Kategorie hat (kein Rückgängig, keine Aufteilung, kein
/// Übertrag dazwischen).
let ruleOfferStays (model: Model) (offer: RuleOffer) : bool =
    let entryId = RuleOffer.entryId offer
    detailShows model entryId
    && (match detailEntryOf model with
        | Some entry when entry.Id = entryId ->
            (rulePayeeOf entry).IsSome && ruleCategoryOf entry = Some (RuleOffer.categoryId offer)
        | _ -> false)

/// Das Angebot im Detail dieser Buchung: der Payee für „Regel für „<Payee>“ anlegen?“. Nur wenn der Server gesagt
/// hat, dass keine Regel die Kategorie schon gibt.
let ruleOfferFor (model: Model) (entry: LedgerEntryView) : string option =
    match model.RuleOffer with
    | Some (RuleOfferShown (entryId, _) as offer) when entryId = entry.Id && ruleOfferStays model offer -> rulePayeeOf entry
    | _ -> None

/// Der Satz des Angebots.
let ruleOfferText (payee: string) : string = $"Regel für „{payee}“ anlegen?"


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
        // Eine ausgegraute Ausnahme ist schon aus `TotalCount` und `TotalAmount` heraus (`applyFilterEffect`).
        let rows = entries |> List.filter (fun e -> excluded.Contains e.Id && not (model.FilteredOut.Contains e.Id))
        { Count = max 0 (model.TotalCount - List.length rows)
          Amount = { model.TotalAmount with Amount = model.TotalAmount.Amount - sumOf rows } }

/// Läuft eine nicht leere Auswahl? Nur dann zeigt die Summenzeile die Auswahl, und am PC
/// erscheint die Leiste.
let hasSelection (model: Model) : bool = (selectionTotals model).Count > 0

/// Die gewählten geladenen Buchungen in Listenreihenfolge (ledger-cnaz3): Grundlage der Zusammenfassung im Panel.
/// Unter `AllExcept` sind es nur die geladenen; ob weitere fehlen, sagt `selectionTotals` gegen diese Liste.
let selectedEntries (model: Model) : LedgerEntryView list =
    let selection = model.Selection
    model.Entries |> RemoteData.withDefault [] |> List.filter (fun e -> isSelected selection e.Id && not (model.FilteredOut.Contains e.Id))

/// Die eine gewählte Buchung, wenn genau eine geladene gewählt ist (ledger-cnaz3): dann zeigt das Panel am PC ihr
/// Detail. Unter `AllExcept` nur, wenn der Treffer nach den Ausnahmen genau eine Buchung meint und sie geladen ist;
/// dieselbe Zahl, nach der die Leiste `SingleSelectionBar` zeigt.
let singleSelectedEntry (model: Model) : LedgerEntryView option =
    match model.Selection with
    | Picked ids when Set.count ids = 1 ->
        let id = Set.minElement ids
        model.Entries |> RemoteData.withDefault [] |> List.tryFind (fun e -> e.Id = id)
    | AllExcept _ when (selectionTotals model).Count = 1 ->
        match selectedEntries model with
        | [ entry ] -> Some entry
        | _ -> None
    | _ -> None

/// Die Zeilen, die man wählen kann (ledger-cnaz3): geladen und nicht ausgegraut. Nur sie tragen ein Häkchen, deshalb
/// nimmt kein Klick, kein Bereich und keine Taste eine andere in `Picked`.
let selectableEntries (model: Model) : LedgerEntryView list =
    model.Entries |> RemoteData.withDefault [] |> List.filter (fun e -> not (model.FilteredOut.Contains e.Id))

/// Kann die Zeile gewählt werden (steht sie in `selectableEntries`)? Wächter jeder Nachricht, die eine Id in die
/// Auswahl nimmt: eine nur über die Route geöffnete Buchung (Fokuszeile ohne Listenzeile) oder eine ausgegraute
/// trägt kein Häkchen.
let isSelectableEntry (model: Model) (id: Guid) : bool =
    selectableEntries model |> List.exists (fun e -> e.Id = id)

/// Das Suchfeld des Kassenbuchs (ledger-d9z3n): Test-Kennung des Rahmens und das Feld darin, Ziel der Taste „/“.
let ledgerSearchTestId = "ledger-search"
let ledgerSearchInputSelector = "[data-testid='ledger-search'] input"

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
/// `TotalCount`: leer, Akzent-Strich (Teilauswahl) oder Haken (ganzer Treffer).
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

/// Die Art des Toasts nach "Geprüft" für die Auswahl: Warnung, sobald etwas nicht
/// geklappt hat (nicht in YNAB, nicht gesendet), Info ohne Änderung, sonst Erfolg.
let reviewBatchToastType (outcome: Shared.Domain.ReviewMarkBatchOutcome) : ToastType =
    let anySendFailed =
        outcome.Changed
        |> List.exists (fun c ->
            match c.WriteBack with
            | Shared.Domain.ReviewMarkSendFailed _ -> true
            | _ -> false)
    if anySendFailed || outcome.FailedCount > 0 || outcome.HeldCount > 0 then ToastWarning
    elif List.isEmpty outcome.Changed then ToastInfo
    else ToastSuccess

/// Die Meldung nach "Geprüft" für die Auswahl (Handoff 1.12, ledger-gwr9j): oben "n geprüft", darunter bei Bedarf
/// die Zahlen ohne Änderung, nicht in YNAB, vielleicht doppelt und nicht an YNAB gesendet; "Rückgängig", wenn sich
/// etwas geändert hat. Der Ton kommt aus `reviewBatchToastType`.
let reviewBatchFeedback (outcome: Shared.Domain.ReviewMarkBatchOutcome) : FeedbackNote =
    let changed = List.length outcome.Changed
    let sendFailed =
        outcome.Changed
        |> List.filter (fun c ->
            match c.WriteBack with
            | Shared.Domain.ReviewMarkSendFailed _ -> true
            | _ -> false)
        |> List.length
    let notes =
        [ if outcome.UnchangedCount > 0 then $"{outcome.UnchangedCount} ohne Änderung"
          if outcome.FailedCount > 0 then $"{outcome.FailedCount} nicht in YNAB"
          if outcome.HeldCount > 0 then $"{outcome.HeldCount} vielleicht doppelt"
          if sendFailed > 0 then $"{sendFailed} nicht an YNAB gesendet" ]
    { Tone = feedbackToneOf (reviewBatchToastType outcome)
      Action = (if changed = 0 then "Keine geprüft" else $"{Client.DesignSystem.Money.formatCount changed} geprüft")
      Detail = (if List.isEmpty notes then None else Some (String.concat ", " notes))
      Act =
        if changed = 0 then None
        else
            let items : Shared.Domain.ReviewMarkRestoreItem list =
                outcome.Changed |> List.map (fun c -> { EntryId = c.EntryId; Previous = c.Previous })
            Some { Label = undoToastLabel; Kind = UndoReviewMarkBatch items } }

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
      if has Shared.Domain.SkipPossibleDuplicate then "vielleicht doppelt"
      if outcome.UnchangedCount > 0 then "schon dort" ]

/// Die Buchungen des Stapels, deren Weg nach YNAB gescheitert ist, mit ihrem Grund.
let categoryBatchSendFailures (outcome: Shared.Domain.CategoryBatchOutcome) : string list =
    outcome.Changed
    |> List.choose (fun c ->
        match c.WriteBack with
        | Shared.Domain.CategorySendFailed reason -> Some reason
        | _ -> None)

/// Die Art des Toasts: Warnung, sobald der Weg nach YNAB gescheitert ist, Info ohne Änderung,
/// sonst Erfolg.
let categoryBatchToastType (outcome: Shared.Domain.CategoryBatchOutcome) : ToastType =
    if not (List.isEmpty (categoryBatchSendFailures outcome)) then ToastWarning
    elif List.isEmpty outcome.Changed then ToastInfo
    else ToastSuccess

/// Wie viele Buchungen der Weg nach YNAB betrifft, als Zusatz zum ersten Grund: " (bei n Buchungen)" ab zwei.
let private sendFailureText (title: string) (failures: string list) : string option =
    match failures with
    | [] -> None
    | [ reason ] -> Some $"{title}: {reason}"
    | reason :: rest -> Some $"{title}: {reason} (bei {List.length rest + 1} Buchungen)"

/// Die Meldung nach "Kategorie setzen" für die Auswahl (ledger-gwr9j): oben "n Buchungen nach <Kategorie> verschoben"
/// (bei `None`: "Kategorie bei n Buchungen entfernt"), darunter bei Bedarf "m übersprungen (Split, Übertrag, gelöscht)"
/// und "Kategorie nicht in YNAB: <erster Grund>"; "Rückgängig", wenn sich etwas geändert hat.
let categoryBatchFeedback (targetName: string option) (outcome: Shared.Domain.CategoryBatchOutcome) : FeedbackNote =
    let changed = List.length outcome.Changed
    let countText = Client.DesignSystem.Money.formatCount changed
    let head =
        match changed, targetName with
        | 0, _ -> "Keine Buchung geändert"
        | 1, Some name -> $"1 Buchung nach {name} verschoben"
        | _, Some name -> $"{countText} Buchungen nach {name} verschoben"
        | 1, None -> "Kategorie bei 1 Buchung entfernt"
        | _, None -> $"Kategorie bei {countText} Buchungen entfernt"
    let skipped = categoryBatchSkippedCount outcome
    let notes =
        [ if skipped > 0 then
              let reasonText = String.concat ", " (categoryBatchSkipReasons outcome)
              yield $"{Client.DesignSystem.Money.formatCount skipped} übersprungen ({reasonText})"
          yield! sendFailureText categoryHintTitle (categoryBatchSendFailures outcome) |> Option.toList ]
    { Tone = feedbackToneOf (categoryBatchToastType outcome)
      Action = head
      Detail = (if List.isEmpty notes then None else Some (String.concat ". " notes))
      Act = (if changed = 0 then None else Some { Label = undoToastLabel; Kind = UndoCategoryBatch outcome.Changed }) }

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

// ============================================
// "Lena fragen" für die ganze Auswahl (ledger-y54gr)
// ============================================

/// Beschriftung der Sammelaktion "Lena fragen" mit der Zahl der gewählten Buchungen.
let selectionLenaLabel (count: int) : string =
    $"Lena fragen ({Client.DesignSystem.Money.formatCount count})"

/// Beschriftung der Sammelaktion "Rückfrage zurücknehmen" mit der Zahl der gewählten Buchungen.
let selectionWithdrawLabel (count: int) : string =
    $"Rückfrage zurücknehmen ({Client.DesignSystem.Money.formatCount count})"

/// Warum "Lena fragen" in der Leiste gesperrt ist, oder `None`: ohne gesetzte Flag-Farbe
/// (ADR 0056, Punkt 1) oder wenn die Einstellung nicht geladen werden konnte (ledger-vq2g8).
/// Zurücknehmen braucht die Farbe nicht.
let selectionLenaDisabledReason (lenaColor: RemoteData<string option>) : string option =
    lenaColorBlockReason lenaColor

/// Die Buchungen des Stapels, deren Weg nach YNAB gescheitert ist, mit ihrem Grund.
let flagBatchSendFailures (outcome: Shared.Domain.FlagBatchOutcome) : string list =
    outcome.Changed
    |> List.choose (fun c ->
        match c.WriteBack with
        | Shared.Domain.FlagSendFailed reason -> Some reason
        | _ -> None)

/// Die Art des Toasts: Warnung, sobald etwas nicht geklappt hat (nicht in YNAB, nicht gesendet),
/// Info ohne Änderung, sonst Erfolg.
let flagBatchToastType (outcome: Shared.Domain.FlagBatchOutcome) : ToastType =
    if not (List.isEmpty (flagBatchSendFailures outcome)) || outcome.FailedCount > 0 || outcome.HeldCount > 0 then ToastWarning
    elif List.isEmpty outcome.Changed then ToastInfo
    else ToastSuccess

/// Die Meldung nach "Lena fragen" bzw. "Rückfrage zurücknehmen" für die Auswahl (ledger-gwr9j): oben "n Buchungen an
/// Lena gefragt" bzw. "Rückfrage bei n Buchungen zurückgenommen", darunter bei Bedarf die Zahlen ohne Änderung, nicht in
/// YNAB und vielleicht doppelt sowie "Flag nicht in YNAB: <erster Grund>"; "Rückgängig", wenn sich etwas geändert hat.
let flagBatchFeedback (target: Shared.Domain.LenaFlagTarget) (outcome: Shared.Domain.FlagBatchOutcome) : FeedbackNote =
    let changed = List.length outcome.Changed
    let count = Client.DesignSystem.Money.formatCount changed
    let head =
        match target, changed with
        | Shared.Domain.AssignLena, 0 -> "Keine Buchung an Lena gefragt"
        | Shared.Domain.AssignLena, 1 -> "1 Buchung an Lena gefragt"
        | Shared.Domain.AssignLena, _ -> $"{count} Buchungen an Lena gefragt"
        | Shared.Domain.WithdrawLena, 0 -> "Keine Rückfrage zurückgenommen"
        | Shared.Domain.WithdrawLena, 1 -> "Rückfrage bei 1 Buchung zurückgenommen"
        | Shared.Domain.WithdrawLena, _ -> $"Rückfrage bei {count} Buchungen zurückgenommen"
    let counts =
        [ if outcome.UnchangedCount > 0 then $"{Client.DesignSystem.Money.formatCount outcome.UnchangedCount} ohne Änderung"
          if outcome.FailedCount > 0 then $"{Client.DesignSystem.Money.formatCount outcome.FailedCount} nicht in YNAB"
          if outcome.HeldCount > 0 then $"{Client.DesignSystem.Money.formatCount outcome.HeldCount} vielleicht doppelt" ]
    let notes =
        [ if not (List.isEmpty counts) then yield String.concat ", " counts
          yield! sendFailureText flagHintTitle (flagBatchSendFailures outcome) |> Option.toList ]
    { Tone = feedbackToneOf (flagBatchToastType outcome)
      Action = head
      Detail = (if List.isEmpty notes then None else Some (String.concat ". " notes))
      Act = (if changed = 0 then None else Some { Label = undoToastLabel; Kind = UndoFlagBatch outcome.Changed }) }

/// Die Meldung nach dem gemeinsamen "Rückgängig" einer Sammelaktion (ledger-gwr9j): „✓ Zurückgesetzt“, darunter
/// was („Kategorie“ oder „Flag“) bei wie vielen Buchungen.
let batchRestoredFeedback (what: string) (count: int) : FeedbackNote =
    let noun = if count = 1 then "Buchung" else "Buchungen"
    feedbackNote FeedbackOk "Zurückgesetzt" (Some $"{what} bei {Client.DesignSystem.Money.formatCount count} {noun}")

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
/// Liste ↓ die erste und ↑ die letzte (§7.8 Punkt 1, design-system-gcpws). `None` nur bei leerer Liste.
let private clampedFocusIndex (entries: LedgerEntryView list) (focus: Guid option) (delta: int) : int option =
    match entries with
    | [] -> None
    | _ ->
        match focus |> Option.bind (fun id -> entries |> List.tryFindIndex (fun e -> e.Id = id)) with
        | Some index -> Some (max 0 (min (List.length entries - 1) (index + delta)))
        | None -> Some (if delta < 0 then List.length entries - 1 else 0)

/// Wohin der Fokus nach einer Pfeiltaste wandert. Am Listenende und am Listenanfang
/// bleibt er stehen.
let movedFocus (entries: LedgerEntryView list) (focus: Guid option) (delta: int) : Guid option =
    clampedFocusIndex entries focus delta |> Option.map (fun i -> (List.item i entries).Id)

/// Wohin die Markierung mit Umschalt+↑↓ wandert (ledger-cnaz3): zur nächsten wählbaren Zeile in Richtung `delta`,
/// gezählt ab der Stelle der Markierung in der vollen Liste. So springt sie von einer ausgegrauten Zeile nicht an den
/// Anfang. Ohne wählbare Zeile in dieser Richtung bleibt eine wählbare Markierung stehen, eine ausgegraute ergibt
/// `None`. Ohne Markierung in der Liste wie `movedFocus` über die wählbaren Zeilen.
let movedSelectableFocus
    (entries: LedgerEntryView list)
    (isSelectable: LedgerEntryView -> bool)
    (focus: Guid option)
    (delta: int)
    : Guid option =
    match focus |> Option.bind (fun id -> entries |> List.tryFindIndex (fun e -> e.Id = id)) with
    | None -> movedFocus (entries |> List.filter isSelectable) focus delta
    | Some index ->
        let current = List.item index entries
        let inDirection =
            if delta > 0 then entries |> List.skip (index + 1)
            elif delta < 0 then entries |> List.take index |> List.rev
            else []
        let ahead = inDirection |> List.filter isSelectable
        match ahead with
        | [] -> if isSelectable current then Some current.Id else None
        | _ -> Some (List.item (min (abs delta) (List.length ahead) - 1) ahead).Id

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
    /// Konten nach einem Spiegel-Lauf neu laden (ledger-ct3cx). Anders als `LoadAccounts`
    /// (Seitenaufruf) baut die Antwort die Liste nur neu auf, wenn sich der aufgelöste
    /// Konto-Filter ändert oder noch keine Liste geladen ist; sonst lädt sie im Hintergrund nach.
    | RefreshAccounts
    | AccountsRefreshed of Result<LedgerAccountsView, string>

    // Kontostaende
    | LoadBalances
    /// Antwort auf `LoadBalances` mit der `BalancesGeneration` ihres Starts (ledger-pfhdq).
    | BalancesLoaded of int * Result<LedgerAccountBalanceView list, string>

    // Konto-Filter
    | SelectFilter of AccountFilter
    /// Konto aus der Seitenleiste oder der Übersicht, bevor das Kassenbuch betreten wird (design-system-k6kfw):
    /// setzt und merkt den Filter, ohne zu laden. Das Betreten lädt Konten und danach die Buchungen.
    | PresetFilter of AccountFilter
    /// Ein Eintrag aus „Zu tun“ der Übersicht (ledger-s1weh), bevor das Kassenbuch betreten wird: alle Konten mit
    /// genau diesem Filter, die übrigen Filter leer. Lädt nicht, wie `PresetFilter`.
    | PresetQuickFilter of QuickFilterPreset
    /// Konto-Wechsler am Handy (ledger-ybmqc): Titel antippen öffnet, Schließen oder Wahl schließt.
    | OpenAccountSheet
    | CloseAccountSheet
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
    | EntriesPageLoaded of generation: int * page: int * Result<LedgerEntryPage, string>
    /// Der Beobachter am Listenende ist sichtbar geworden: naechste Seite nachladen,
    /// wenn gerade keine laeuft und es noch etwas zu holen gibt.
    | LoadMoreEntries
    /// Nachladen im Hintergrund (ledger-ct3cx): dieselbe Filterung und Sortierung ab Zeile 0,
    /// so viele Zeilen wie geladen sind. Auswahl, Seiten und Scrollstand bleiben. Ohne geladene
    /// Liste baut es neu auf. Rein; die Abfrage steckt hinter `RunRefreshEntries`.
    | RefreshEntries
    | RunRefreshEntries
    | EntriesRefreshed of generation: int * refresh: int * Result<LedgerEntryPage, string>
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

    // ============================================
    // Memo bearbeiten im Detail (ledger-fa1fp, ADR 0062)
    // ============================================
    // Dieselbe Teilung wie bei der Kategorie (ADR 0020): `Start...` ist rein, `Run...` ruft den Proxy.

    /// "Bearbeiten" bzw. "Memo hinzufügen": öffnet das Eingabefeld mit dem heutigen Memo.
    | StartMemoEdit of Guid
    /// Der Text im Eingabefeld hat sich geändert.
    | MemoEditChanged of string
    /// "Abbrechen" (oder Esc): schließt das Feld ohne Änderung.
    | CancelMemoEdit
    /// "Speichern": ein zu langer Text und ein unveränderter Text rufen den Server nicht.
    | StartSaveMemo of Guid
    | RunSetMemo of Guid * string
    | MemoSet of Guid * Result<MemoOutcome, string>
    /// "Rückgängig" im Toast: stellt das Memo vor dem Speichern wieder her.
    | StartUndoMemo of Guid * string option
    | RunUndoMemo of Guid * string option
    | MemoRestored of Guid * Result<MemoOutcome, string>
    /// "Erneut senden" an der Hinweisleiste "Memo nicht in YNAB".
    | StartRetryMemo of Guid
    | RunRetryMemo of Guid
    | MemoRetried of Guid * Result<MemoWriteBack, string>

    // ============================================
    // Betrag bearbeiten im Detail (ledger-m7mye, ADR 0092)
    // ============================================

    /// Der Betrag im Kopf: öffnet das Sheet "Betrag", vorbelegt aus der Buchung.
    | OpenAmountEdit of Guid
    /// "Ausgabe" (true) oder "Einnahme" (false).
    | AmountDirectionChanged of bool
    | AmountDraftChanged of string
    | CancelAmountEdit
    | StartSaveAmount of Guid
    /// Nach einem abgelehnten Speichern: Fokus ins Betragsfeld, auch nach einem Tipp auf "Ausgabe" oder
    /// "Einnahme", und der Feldfehler rollt ins Bild (ledger-q42pg).
    | RevealAmountFieldError
    | RunSetAmount of Guid * AmountChangeRequest
    | AmountSet of Guid * Result<AmountOutcome, string>
    /// "Rückgängig" im Toast: der Betrag vor dem Speichern (mit Vorzeichen).
    | StartUndoAmount of Guid * decimal
    | RunUndoAmount of Guid * decimal
    | AmountRestored of Guid * Result<AmountOutcome, string>
    /// "Erneut senden" an der Hinweisleiste "Betrag nicht in YNAB".
    | StartRetryAmount of Guid
    | RunRetryAmount of Guid
    | AmountRetried of Guid * Result<AmountWriteBack, string>

    // ============================================
    // Payee ändern im Detail (ledger-fxt09)
    // ============================================

    /// Ein Tipp auf die Payee-Zeile: öffnet die Payee-Auswahl.
    | OpenPayeeEdit of Guid
    | RunLoadPayeeChoices
    | PayeeChoicesLoaded of generation: int * Result<LedgerPayeeSuggestion list, string>
    /// Schließen der Auswahl (Esc, Schließen-Knopf): zurück ins Detail, ohne Änderung.
    | CancelPayeeEdit
    /// Der Fokus kehrt auf die Payee-Zeile im Detail zurück (nach dem Schließen der Auswahl).
    | RestorePayeeRowFocus
    /// Getippt im Suchfeld: nimmt einen Feldfehler zurück.
    | PayeeSearchEdited
    /// Ein Vorschlag, "„…“ übernehmen" oder "Payee entfernen" (`None`, ledger-pkrwd): übernimmt
    /// sofort (§7.1).
    | StartSavePayee of Guid * string option
    | RunSetPayee of Guid * string option
    | PayeeSet of Guid * Result<PayeeOutcome, string>
    /// "Rückgängig" im Toast: derselbe Befehl mit dem alten Payee.
    | StartUndoPayee of Guid * string option
    | RunUndoPayee of Guid * string option
    | PayeeRestored of Guid * Result<PayeeOutcome, string>
    /// "Erneut senden" an der Hinweisleiste "Payee nicht in YNAB".
    | StartRetryPayee of Guid
    | RunRetryPayee of Guid
    | PayeeRetried of Guid * Result<PayeeWriteBack, string>
    /// Speichern im Split-Editor nach dem Sheet "Betrag": Betrag und Zeilen zusammen.
    | RunSaveSplitsWithAmount of Guid * TransactionSplit list * Money
    | SplitsWithAmountSaved of Guid * TransactionSplit list * Money * Result<CategoryChangeOutcome, string>
    /// Die Ansicht meldet, wie die Liste bei Breite und Eingabegerät bedient wird (ledger-m6zpd).
    | ListInteractionChanged of ListInteraction
    | LoadHistory of Guid
    /// Die Historie der Buchung `Guid` mit der Lade-Nummer beim Start (`historyAnswer`). Kommt sie nach
    /// einem Wechsel der Buchung oder nach einem neueren Laden an, gilt sie nicht mehr.
    | HistoryLoaded of Guid * int * Result<LedgerHistoryLine list, string>
    /// „Historie (n)“ im Detail auf- oder zuklappen (ledger-zfj78).
    | ToggleHistory

    // Spiegel
    | LoadMirrorStatus
    /// Antwort auf `LoadMirrorStatus` mit der `MirrorStatusGeneration` ihres Starts (ledger-pfhdq).
    | MirrorStatusLoaded of int * Result<MirrorRunStatus option, string>
    /// "Jetzt spiegeln" gedrueckt: setzt den Hinweis, stoesst den Lauf an.
    | StartMirror
    | RunMirror
    | MirrorFinished of Result<MirrorRunSummary, string>

    // Tab „Regeln“ (categorization-n3sxw)
    /// Der Tab „Regeln“ wurde aus den Buchungen heraus gewählt: merkt deren Scrollstand.
    | BookingsScrollRemembered of float
    /// Der gemerkte Scrollstand ist gesetzt oder hinfällig (Regeln zu einem anderen Ziel verlassen).
    | BookingsScrollDropped

    // ============================================
    // Filterleiste und Summenzeile (ledger-j8xq4)
    // ============================================

    /// Die YNAB-Kategorien für die Kategorienauswahl laden (unrein: der Aufruf
    /// braucht das konfigurierte Budget und steckt deshalb im Haupt-`State.fs`,
    /// analog zu `Rules`s `LoadCategories`).
    | LoadCategories
    /// Antwort eines Abrufs mit der `CategoriesGeneration` seines Starts (ledger-pfhdq).
    | CategoriesLoaded of int * Result<YnabCategory list, string>
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

    /// Filter-Knopf am Payee im Detail (ADR 0053): setzt die Suche auf den Payee
    /// (ledger-d9z3n, sofort, ohne Verzögerung); das Detail bleibt offen.
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

    /// Tippen im Suchfeld (ledger-d9z3n): der Text steht sofort im Feld, die Suche gilt nach der
    /// Verzögerung (`CommitSearch` mit der Version des Tastendrucks).
    | SetSearchDraft of string
    | CommitSearch of string * int
    | RemoveSearch

    /// Der Knopf „Filter“ unter 1024 px öffnet das Filter-Sheet (ledger-d9z3n).
    | OpenFilterSheet
    /// „n zeigen“, Esc, „×“ oder Tipp daneben: das Sheet schließt samt einer offenen Auswahl darin, die Filter bleiben
    /// stehen; ein Entwurf der Auswahl wird verworfen (ledger-npqcb).
    | CloseFilterSheet
    /// „Zurück“ in einer Auswahl im Filter-Sheet (ledger-npqcb): zeigt wieder die Filter, ohne etwas zu ändern.
    | BackToFilterSheet

    | SetAmountMinDraft of string
    | SetAmountMaxDraft of string
    | CommitAmountFilter of decimal option * decimal option * int
    | RemoveAmountFilter
    /// Umschalter „Alle | Ausgaben | Einnahmen“ (ADR 0095); `AllDirections` nimmt die Richtung zurück.
    | SetAmountDirection of AmountDirection
    /// „×“ am PC-Chip „Betrag“: nimmt Betrag und Richtung zurück, beide stehen in seinem Popover.
    | RemoveAmountAndDirection

    /// „Filter löschen“ am Chip und im Filter-Sheet (ledger-d9z3n): setzt alle Filter zurück, auch
    /// „Zu prüfen“, „Lena“, „Ohne Kategorie“, Sortierung, „Gelöschte zeigen“ und die Suche, und
    /// lädt ab Seite 0 neu. Das Konto bleibt.
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
    /// Klick auf "Erneut senden" am Hinweis "Noch nicht in YNAB" (ledger-cks06).
    | StartRetryPush of Guid
    | RunRetryPush of Guid
    | PushRetried of Guid * Result<PushToYnabWriteBack, string>

    // ============================================
    // Wisch nach rechts: geprüft, mit Rückgängig (ledger-th36h)
    // ============================================

    /// Wisch nach rechts an einer Karte (Handy). Wirkt nur auf eine zu prüfende Zeile.
    | StartSwipeReviewed of Guid
    | RunSwipeReviewed of Guid
    /// Antwort auf die Geste; der Payee ist festgehalten, bevor die Zeile die Liste
    /// verlässt (zweite Zeile der Meldung).
    | SwipeReviewedDone of Guid * payee: string option * Result<ReviewMarkOutcome, string>
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
    /// Ein Konto im Picker des Details (ledger-qvjvs): die Buchung wird ein Übertrag auf dieses
    /// Konto. `undo` heißt: der Aufruf ist die Rücknahme aus dem Toast, ohne neues "Rückgängig".
    | StartSetEntryTransfer of Guid * YnabAccountId * undo: bool
    | RunSetEntryTransfer of Guid * YnabAccountId * undo: bool
    | EntryTransferSet of Guid * YnabAccountId * undo: bool * Result<CategoryChangeOutcome, string>
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
    // ============================================
    // Neue Buchung (ledger-at63w)
    // ============================================
    /// "+ Buchung": öffnet das Formular und lädt die Vorbelegung. Ein Konto im Filter des Kassenbuchs ist vorbelegt.
    | OpenCapture
    /// "+ Buchung" auf der Übersicht (ledger-ea70s): wie `OpenCapture`, aber ohne Kontofilter, das Quick-Add-Konto
    /// ist vorbelegt.
    | OpenCaptureUnfiltered
    | CloseCapture
    | RunLoadCaptureDefaults
    /// "Erneut versuchen" nach einer gescheiterten Vorbelegung.
    | RetryCaptureDefaults
    | CaptureDefaultsLoaded of Result<LedgerBookingDefaults, string>
    | SetCaptureDirection of isOutflow: bool
    | SetCaptureAmount of string
    | SetCapturePayee of string
    /// Das Payee-Feld verliert den Fokus: die Kategorie zum Payee wird geholt.
    | CommitCapturePayee
    | RunLoadCapturePayeeCategory of payee: string
    | CapturePayeeCategoryLoaded of payee: string * Result<YnabCategoryId option, string>
    | SetCaptureDate of DateTime
    | SetCaptureMemo of string
    | ApplyCaptureTemplate of LedgerBookingTemplate
    | OpenCapturePicker of Capture.CapturePicker
    | CloseCapturePicker
    | ChooseCaptureAccount of Guid
    | ChooseCaptureTarget of LedgerBookingTarget option
    /// "Buchung anlegen": prüft im Client und startet den Aufruf, gesperrt solange er läuft.
    | SubmitCapture
    /// Abgelehntes "Buchung anlegen" wegen des Betrags: Fokus ins Betragsfeld, Meldung ins Bild (ledger-vf6z5).
    | RevealCaptureAmountError
    | RunSubmitCapture of LedgerBookingRequest
    | CaptureSubmitted of Result<LedgerBookingCreated, string>
    /// "Rückgängig" an der Rückmeldung nach dem Anlegen.
    | StartDiscardCaptured of Guid
    | RunDiscardCaptured of Guid
    | CapturedDiscarded of Guid * Result<unit, string>
    /// "Buchung löschen" im Detail (ledger-z6sya, ADR 0092).
    | StartDeleteEntry of Guid
    | RunDeleteEntry of Guid
    | EntryDeleteFinished of Guid * Result<unit, string>
    /// "Rückgängig" an der Rückmeldung nach dem Löschen.
    | StartRestoreDeletedEntry of Guid
    | RunRestoreDeletedEntry of Guid
    | EntryRestoreFinished of Guid * Result<unit, string>
    /// Das Kassenbuch ist verlassen (andere Seite als Liste, Buchung und Regeln): die Zeile am Platz endet (ledger-k68sd).
    | LedgerLeft
    | TransferTargetsLoaded of Result<YnabAccount list, string>
    | AddSplitLine
    | RemoveSplitLine of int
    | SetSplitLineAmount of int * string
    | SetSplitLineMemo of int * string
    /// Knopf "Rest" an der Zeile.
    | FillSplitLineRest of int
    /// Knopf "Barabhebung": belegt die erste Zeile ohne Ziel mit dem Übertrag aufs Bar-Konto
    /// oder hängt eine an. Das Ziel löst die Ansicht aus Quick-Add-Konto und Konto-Liste auf.
    | StartSplitCashback of SplitTarget option
    /// Das Quick-Add-Konto aus den Settings des Root übernehmen (der Root fängt die Nachricht ab).
    | LoadQuickAddAccount
    | OpenSplitTargetPicker of int
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
    /// `basis` ist die Zeile beim Start der Abfrage. Hat sie sich seitdem geändert, ist die
    /// Antwort veraltet.
    | EntryRefreshed of Guid * basis: LedgerEntryView option * Result<LedgerEntryView option, string>
    /// Die Route `#/ledger/entry/<id>` wurde betreten: Buchung holen, auch wenn sie nicht in der
    /// geladenen Liste steht (budgeting-m1bce).
    | OpenRouteEntry of Guid
    | RouteEntryLoaded of Guid * Result<LedgerEntryView option, string>
    /// Die Route wurde verlassen: die Buchung bleibt beim nächsten Besuch nicht offen.
    | LeaveRouteEntry of RouteLeftFor
    | RunRetryCategorization of Guid
    | CategorizationRetried of Guid * Result<CategoryWriteBack, string>

    // ============================================
    // Lena fragen im Detail (ledger-xfjcp, ADR 0056)
    // ============================================
    // Dieselbe Teilung wie bei der Kategorie (ADR 0020): `Start...` ist rein, `Run...` ruft den Proxy.

    /// Der Chip "Rückfrage Lena (n)" wurde angetippt: kippt `Filters.OnlyAskLena` und lädt ab Seite 0 neu.
    | ToggleOnlyAskLena
    /// Lädt die Flag-Farbe aus den Settings (ein Aufruf beim Betreten der Seite).
    | LoadLenaFlagSettings
    | LenaFlagSettingsLoaded of Result<LenaFlagSettings, string>
    /// "Lena fragen" bzw. "Rückfrage zurücknehmen" im Detail.
    | StartSetLenaFlag of Guid * LenaFlagTarget
    | RunSetLenaFlag of Guid * LenaFlagTarget
    | LenaFlagSet of Guid * LenaFlagTarget * Result<FlagOutcome, string>
    /// "Lena fragen" bzw. "Rückfrage zurücknehmen" in der Auswahl-Leiste (ledger-y54gr): sperrt
    /// die Sammelaktionen und stößt den Aufruf an (nur mit nicht leerer Auswahl, bei
    /// "Lena fragen" nur mit gesetzter Flag-Farbe).
    | StartLenaBatch of LenaFlagTarget
    /// Der Sammel-Aufruf mit der Auswahl und dem Ziel.
    | RunLenaBatch of ReviewMarkBatchSelection * LenaFlagTarget
    | LenaBatchDone of LenaFlagTarget * Result<FlagBatchOutcome, string>
    /// Klick auf "Rückgängig" im Toast: stellt Farbe und Name jeder geänderten Buchung her.
    | StartUndoFlagBatch of FlagBatchChange list
    | RunUndoFlagBatch of FlagRestoreItem list
    | FlagBatchRestored of FlagRestoreItem list * Result<FlagBatchOutcome, string>
    /// "Rückgängig" im Toast: stellt Farbe und Name des Vorzustands her.
    | StartUndoFlag of Guid * FlagState
    | RunUndoFlag of Guid * FlagState
    | FlagRestored of Guid * FlagState * Result<FlagOutcome, string>
    /// "Erneut senden" an der Hinweisleiste "Flag nicht in YNAB".
    | StartRetryFlag of Guid
    | RunRetryFlag of Guid
    | FlagRetried of Guid * Result<FlagWriteBack, string>
    /// Die Karte "Budget nach dem Wechsel" läuft ab (nur die mit dieser Version).
    | CategoryCardExpired of version: int
    /// Das Leuchten des Felds der Bestätigung mit dieser Nummer ist vorbei (ledger-p5y9s).
    | DetailNoteFlashEnded of version: int

    // Regel aus der Buchung (ledger-hg5m5)
    /// „Regel anlegen“ im Detail, auf der Karte beim Durchgehen oder „Anlegen“ im Angebot.
    | RuleFromEntryRequested of Guid
    /// Fragt den Server, ob die Regeln Payee und Memo schon diese Kategorie geben.
    | RunCheckRuleOffer of Guid * payee: string * memo: string * YnabCategoryId
    | RuleOfferChecked of Guid * YnabCategoryId * Result<YnabCategoryId option, string>
    /// Das × am Angebot.
    | RuleOfferDismissed
    /// Trefferzahl und Summe neu holen, solange die Reihenfolge veraltet ist.
    | RunRefreshTotals
    | TotalsRefreshed of generation: int * Result<LedgerEntryPage, string>

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
    /// Klick auf eine Zeile mit Strg/Cmd (`ToggleRow`) oder Umschalt (`RangeToRow`) am PC (ledger-cnaz3, §7.7):
    /// Strg/Cmd nimmt die Zeile in die Auswahl oder heraus, Umschalt wählt den Bereich ab dem Anker.
    | ClickRowWithModifier of Guid * RowClickModifier
    /// Umschalt+↑↓ (ledger-cnaz3): die Markierung rückt eine Zeile weiter, beide Zeilen kommen in die Auswahl.
    | ExtendSelection of delta: int

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
    /// Antwort, mit den Vorzuständen, auf die die Zeilen zurückgehen.
    | ReviewMarkBatchRestored of ReviewMarkRestoreItem list * Result<ReviewMarkBatchOutcome, string>

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
    /// Taste L (ledger-cnaz3): fragt Lena für die Auswahl oder, ohne Auswahl, für die markierte Zeile.
    | LenaKeyPressed
    /// Taste „/“ (ledger-cnaz3): der Fokus springt in die Suche.
    | FocusSearch

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
    /// Esc mit dem Fokus in einem Feld: wie `EscapePressed`, nur verlässt es in „Neue Buchung“ erst das Feld
    /// (Roman, 2026-11-15, `captureEscapeFor`).
    | EscapePressedInField
    /// Verlässt das Feld mit dem Fokus (Esc im Feld von „Neue Buchung“). Nur dieser Schritt berührt das DOM.
    | BlurFocusedField

    // ============================================
    // Durchgehen (ledger-v6s7c)
    // ============================================

    /// Die Seite „Durchgehen“ öffnet: die Schlange lädt neu.
    | ReviewOpened
    /// Die Seite wird verlassen: Karte, Picker und Detail schließen.
    | ReviewLeft
    /// Lädt die Schlange; die Zahl ist die Generation des Öffnens (`ReviewGeneration`).
    | RunLoadReviewQueue of generation: int
    | ReviewQueueLoaded of generation: int * Result<LedgerEntryView list, string>
    /// Die gezeigte Karte wird neu gelesen; eine inzwischen geprüfte oder gelöschte übersprungen.
    | RunReviewRefresh of Guid
    | ReviewEntryRefreshed of Guid * basis: LedgerEntryView option * Result<LedgerEntryView option, string>
    | RunReviewSuggestions of Guid * string
    | ReviewSuggestionsLoaded of Guid * Result<YnabCategoryId list, string>
    /// Die schon vorhandene Buchung zur Duplikat-Karte wird gelesen.
    | RunReviewMatch of Guid
    | ReviewMatchLoaded of Guid * Result<LedgerEntryView option, string>
    /// „Passt, weiter“: der Prüfvermerk, danach die nächste Karte.
    | ReviewConfirm
    | RunReviewConfirm of Guid
    | ReviewConfirmed of Guid * Result<ReviewMarkOutcome, string>
    /// „Später“: weiter, die Buchung bleibt ungeprüft.
    | ReviewSkip
    /// „Lena fragen“ (weiter) bzw. „Rückfrage zurück“ (bleibt).
    | ReviewLena
    /// K: die Kategorieauswahl der Karte.
    | ReviewOpenCategory
    /// Ein Vorschlag unter dem Feld.
    | ReviewPickSuggestion of YnabCategoryId
    /// „Ist neu“ auf der Duplikat-Karte.
    | ReviewConfirmNew
    | RunReviewConfirmNew of Guid
    | ReviewConfirmedNew of Guid * Result<PushToYnabWriteBack, string>
    /// „Ist doppelt, verwerfen“ auf der Duplikat-Karte.
    | ReviewDiscard
    | RunReviewDiscard of Guid
    | ReviewDiscarded of Guid * Result<unit, string>
    /// „Rückgängig“ nach dem Verwerfen: die Buchung wartet wieder, ihre Karte steht wieder da.
    | StartRestoreDiscardedDuplicate of Guid
    | RunRestoreDiscardedDuplicate of Guid
    | DiscardedDuplicateRestored of Guid * Result<unit, string>
    /// „Details“: das Buchungs-Detail über der Karte.
    | ReviewShowDetails
    /// Ende eines Wischs auf der Karte, mit dem Weg in Pixeln (waagrecht, senkrecht).
    | ReviewSwiped of dx: float * dy: float
    /// Eine Taste am PC (ohne Fokus in einem Feld).
    | ReviewKey of ReviewKeyAction
    /// „Rückgängig“ in der Zeile der letzten Aktion, Taste Z oder „Letzte rückgängig“ in der Endansicht.
    | ReviewUndoLast
    /// „Erneut“ in der Zeile der letzten Aktion.
    | ReviewRetryLast
    /// „Rückgängig“ an der Bestätigung unter dem Feld „Kategorie“ der Karte (ledger-mf2bg).
    | ReviewUndoField
    /// Das Leuchten des Felds „Kategorie“ der Karte endet, nur für die Bestätigung mit dieser Nummer.
    | ReviewFieldFlashEnded of stamp: int

/// Was Esc bei offenem Formular „Neue Buchung“ tut, im Dialog wie im Panel: erst die offene Auswahl darin schließen,
/// dann das Formular wie „Abbrechen“. Das Popover der Kategorie behandelt Esc selbst und hält das Ereignis an. Während
/// „Buchung anlegen“ läuft, bleibt das Formular offen (`CloseCapture` prüft `Saving`).
let captureEscapeMsg (capture: Capture.CaptureState) : Msg =
    if capture.Picker.IsSome then CloseCapturePicker else CloseCapture

/// Was Esc in „Neue Buchung“ tut, je nachdem, ob der Fokus in einem Feld steht.
type CaptureEscape =
    /// Nur das Feld verlassen, das Formular bleibt offen.
    | LeaveField
    /// Eine Stufe schließen (`captureEscapeMsg`).
    | CaptureStep of Msg

/// Esc aus einem Feld verwirft den Entwurf nicht: es verlässt erst das Feld, erst das nächste Esc schließt
/// „Neue Buchung“ (Roman, 2026-11-15, nach dem Review zu design-system-gcpws). Eine offene Auswahl im Formular schließt
/// Esc wie bisher auch aus ihrem Suchfeld.
let captureEscapeFor (fromField: bool) (capture: Capture.CaptureState) : CaptureEscape =
    if fromField && capture.Picker.IsNone then LeaveField else CaptureStep (captureEscapeMsg capture)

/// Was die Kassenbuch-Liste kann (§7.8): Mehrfachauswahl, keine Tabs im Panel, eine Suche, in die nicht getippt
/// wird, weil G, L, J und K belegt sind; gesucht wird mit „/“ (F9, design-system-gcpws).
let ledgerListTraits : ListKeys.Traits =
    { MultiSelect = true; Tabs = false; Search = true; TypeToSearch = false }

/// Die eigenen Tasten der Kassenbuch-Liste vor der gemeinsamen Regel (F9): J/K bewegen den Fokus, G setzt
/// „Geprüft“, Umschalt+G „Ungeprüft“, L fragt Lena, Enter öffnet das Detail der markierten Zeile (ledger-q1hw2), die
/// Leertaste wählt sie (ledger-w4vte). Nur ohne Feldfokus; Enter und Leertaste auf einem Knopf oder Link bleiben
/// dessen eigene Aktivierung, sonst löste ein fokussierter Knopf zweimal aus (ADR 0053).
let private ledgerOwnKey (tagName: string) (key: string) : Msg option =
    match key with
    | "j" | "J" -> Some (MoveFocus 1)
    | "k" | "K" -> Some (MoveFocus -1)
    | "g" -> Some (ReviewKeyPressed Reviewed)
    | "G" -> Some (ReviewKeyPressed Unreviewed)
    | "l" | "L" -> Some LenaKeyPressed
    | "Enter" when not (isActivatable tagName) -> Some OpenFocusedDetail
    | " " when not (isActivatable tagName) -> Some ToggleFocusedSelected
    | _ -> None

/// Die Tastenbelegung der Kassenbuch-Liste (PC): erst die eigenen Tasten (`ledgerOwnKey`), dann die gemeinsame Regel
/// aller Listen mit Panel (`ListKeys.actionFor`, design-system-gcpws): ↑↓ bewegen den Fokus (ohne Fokus ab der ersten
/// bzw. letzten Zeile, das entscheidet `MoveFocus`), Umschalt+↑↓ erweitert die Auswahl, „/“ springt in die Suche,
/// aus der Suche (`inSearch`) wirken die Pfeile auf die Liste (ADR 0087 Punkt 18), Esc schließt eine Stufe, auch aus
/// einem Feld. Welche Stufe, entscheidet die Kette in `EscapePressed` (Popover, Picker, Bearbeitung, neue Buchung,
/// Detail, Auswahl); für die Regel gelten Panel und Auswahl deshalb als offen. Mit Strg, Meta oder Alt löst nichts aus.
let keyCommandFor
    (tagName: string)
    (isContentEditable: bool)
    (inSearch: bool)
    (ctrlOrMeta: bool)
    (alt: bool)
    (shift: bool)
    (key: string)
    : Msg option =
    let command = ctrlOrMeta || alt
    let focus =
        if inSearch then ListKeys.InSearch
        elif isTypingTarget tagName isContentEditable then ListKeys.InTextField
        else ListKeys.Elsewhere
    let own =
        if command || focus <> ListKeys.Elsewhere then None else ledgerOwnKey tagName key
    match own with
    | Some msg -> Some msg
    | None ->
        let action =
            ListKeys.actionFor
                ledgerListTraits
                { Key = key
                  Focus = focus
                  Command = command
                  Shift = shift
                  Handled = false
                  LayerOpen = false
                  RowSelected = true
                  PanelOpen = true
                  SelectionActive = true }
        match action with
        | ListKeys.FirstRow
        | ListKeys.StepRow ListKeys.Forward -> Some (MoveFocus 1)
        | ListKeys.LastRow
        | ListKeys.StepRow ListKeys.Backward -> Some (MoveFocus -1)
        | ListKeys.ExtendSelection ListKeys.Forward -> Some (ExtendSelection 1)
        | ListKeys.ExtendSelection ListKeys.Backward -> Some (ExtendSelection -1)
        | ListKeys.FocusSearch -> Some FocusSearch
        | ListKeys.ClosePanel
        | ListKeys.ClearSelection -> Some (if focus = ListKeys.InTextField then EscapePressedInField else EscapePressed)
        | ListKeys.StepTab _
        | ListKeys.TypeIntoSearch
        | ListKeys.Leave -> None

/// Wohin eine Taste am PC auf Kassenbuch und Durchgehen geht (ledger-hg5m5).
type PageKeyTarget<'action> =
    /// Die Seite darunter behandelt die Taste.
    | PageAction of 'action
    /// Esc über dem offenen Regel-Formular: eine Stufe zurück zur Buchung.
    | RuleFormBack

/// Ein offenes Regel-Formular aus einer Buchung liegt über Liste und Karte und bekommt die Tasten zuerst (§7.8
/// Punkt 6): ↑↓, J/K, G, Enter und Leertaste wirken nicht auf die verdeckte Seite, Esc führt genau eine Stufe zurück
/// zur Buchung. Ohne Formular gilt `pageAction`, die Taste der Seite. Rein.
let routePageKey (ruleFormOpen: bool) (modifier: bool) (key: string) (pageAction: 'action option) : PageKeyTarget<'action> option =
    if not ruleFormOpen then pageAction |> Option.map PageAction
    elif key = "Escape" && not modifier then Some RuleFormBack
    else None

/// Die Tasten der Kassenbuch-Liste mit Blick auf ein offenes Regel-Formular (`keyCommandFor`, `routePageKey`).
let ledgerKeyFor
    (ruleFormOpen: bool)
    (tagName: string)
    (isContentEditable: bool)
    (inSearch: bool)
    (ctrlOrMeta: bool)
    (alt: bool)
    (shift: bool)
    (key: string)
    : PageKeyTarget<Msg> option =
    routePageKey ruleFormOpen (ctrlOrMeta || alt) key (keyCommandFor tagName isContentEditable inSearch ctrlOrMeta alt shift key)

/// Was die Seite nach oben meldet.
/// Scheiterte mit dieser Nachricht die laufende Abfrage der Route `#/ledger/entry/<id>`?
let routeEntryLoadFailed (pendingRoute: Guid option) (msg: Msg) : bool =
    match msg with
    | RouteEntryLoaded (entryId, Ok None)
    | RouteEntryLoaded (entryId, Error _) -> pendingRoute = Some entryId
    | _ -> false

type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    /// Ein Toast mit Schaltfläche (ledger-th36h: "Rückgängig").
    | ShowToastWithAction of string * ToastType * ToastAction
    /// Eine Meldung mit getrennter Aktion und Detail für den einen Meldungsplatz (ledger-gwr9j, ADR 0098).
    | PostFeedback of FeedbackNote
    /// Dieselbe Meldung, aber nur, wenn der Platz frei ist oder seine Meldung keinen Knopf trägt: für Meldungen ohne
    /// Aktion des Nutzers (Spiegel im Hintergrund). Eine Meldung mit „Rückgängig“ bleibt bis zur nächsten Aktion.
    | PostFeedbackIfFree of FeedbackNote
    /// Das Durchgehen ist zu Ende oder wird verlassen: zurück zur Übersicht (ledger-v6s7c).
    | LeaveReview
    /// Das Regel-Formular für diese Buchung öffnen, vorbelegt mit Payee und Kategorie (ledger-hg5m5).
    | OpenRuleForEntry of entryId: Guid * payee: string * categoryId: YnabCategoryId option

// ============================================
// Meldungen der Liste (ledger-gwr9j, ADR 0098)
// ============================================

/// Der Spiegel-Lauf läuft: gedämpft, ohne Zeichen, bis zum Ergebnis (Wort „Spiegeln“, Entscheidung Roman).
let mirrorRunningFeedback : FeedbackNote = feedbackNote FeedbackRunning "Spiegeln läuft …" None

/// Die Zusammenfassung eines Spiegel-Laufs für die zweite Zeile: „3 neu, 1 geändert“, Teile mit 0 entfallen, ohne
/// jede Änderung „nichts Neues“.
let mirrorSummaryDetail (summary: MirrorRunSummary) : string =
    let parts =
        [ if summary.NewEntries > 0 then $"{summary.NewEntries} neu"
          if summary.ChangedEntries > 0 then $"{summary.ChangedEntries} geändert"
          if summary.DeletedEntries > 0 then $"{summary.DeletedEntries} gelöscht" ]
    if List.isEmpty parts then "nichts Neues" else String.concat ", " parts

/// „✓ Gespiegelt“ mit der Zusammenfassung.
let mirrorDoneFeedback (summary: MirrorRunSummary) : FeedbackNote =
    feedbackNote FeedbackOk "Gespiegelt" (Some (mirrorSummaryDetail summary))

/// „Spiegeln gescheitert“ mit Grund und „Erneut“ (startet den Lauf neu).
let mirrorFailedFeedback (reason: string) : FeedbackNote =
    { failedFeedback "Spiegeln gescheitert" reason with Act = Some { Label = retryLabel; Kind = RetryMirror } }

/// Die nächste Seite kam nicht: rot mit Grund und „Erneut“ (lädt sie noch einmal).
let loadMoreFailedFeedback (reason: string) : FeedbackNote =
    { failedFeedback "Nicht nachgeladen" reason with Act = Some { Label = retryLabel; Kind = RetryLoadMoreEntries } }

/// Das Nachladen im Hintergrund scheiterte, die Liste steht: gelb mit Grund und „Erneut“.
let refreshFailedFeedback (reason: string) : FeedbackNote =
    { feedbackNote FeedbackWarning "Liste nicht aktualisiert" (Some reason) with
        Act = Some { Label = retryLabel; Kind = RetryRefreshEntries } }

/// Die Meldung nach "Buchung anlegen" (ledger-gwr9j, Skizze 27): „✓ Angelegt“ mit „Rückgängig“; scheiterte der Weg
/// nach YNAB, gelb „Angelegt, nicht in YNAB“ mit dem Grund und ohne Rückgängig.
let createdFeedback
    (created: LedgerBookingCreated)
    (payee: string option)
    (amount: decimal option)
    (inShownAccount: bool)
    : FeedbackNote =
    match created.PushFailure with
    | None ->
        { Tone = FeedbackOk
          Action = "Angelegt"
          Detail = Some (Capture.createdDetail payee created.AccountName amount inShownAccount)
          Act = Some { Label = undoToastLabel; Kind = UndoCapturedBooking created.EntryId } }
    | Some reason -> feedbackNote FeedbackWarning "Angelegt, nicht in YNAB" (Some reason)

/// Der Prüfvermerk steht in BB, YNAB hat ihn nicht: gelb mit Grund. Nach dem Wisch „Geprüft, nicht in YNAB“, nach
/// „Rückgängig“ der Titel des Hinweises an der Zeile, weil die Buchung dann wieder zu prüfen ist.
let reviewMarkNotInYnabFeedback (afterUndo: bool) (reason: string) : FeedbackNote =
    feedbackNote FeedbackWarning (if afterUndo then reviewMarkHintTitle else "Geprüft, nicht in YNAB") (Some reason)

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
    (fetch: CategoriesFetch)
    (loadedAt: DateTime option)
    (now: DateTime)
    : bool =
    let fetching =
        match fetch with
        | CategoriesFetching _ -> true
        | CategoriesIdle -> false
    if fetching || RemoteData.isLoading categories then false
    else
        match loadedAt with
        | None -> true
        | Some at -> now - at > categoriesFreshness

/// Nachlade-Entscheidung über dem Kassenbuch-Modell.
let categoriesReloadDue (model: Model) (now: DateTime) : bool =
    shouldReloadCategories model.Categories model.CategoriesFetch model.CategoriesLoadedAt now

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

/// Der Name des Übertrags-Ziels für Detail und Toast (ledger-qvjvs): aus den Konten des Budgets,
/// sonst aus den Kassenbuch-Konten. `None`, wenn keine Liste das Konto kennt.
let transferAccountName (model: Model) (accountId: Guid) : string option =
    let fromTargets =
        match model.TransferTargets with
        | Success accounts -> accounts |> List.tryFind (fun a -> a.Id = YnabAccountId accountId) |> Option.map (fun a -> a.Name)
        | _ -> None
    let fromLedger () =
        match model.Accounts with
        | Success view ->
            view.Accounts
            |> List.tryFind (fun a -> a.YnabAccountId = Some (YnabAccountId accountId))
            |> Option.map (fun a -> a.Name)
        | _ -> None
    fromTargets |> Option.orElseWith fromLedger

/// Die Kategorie-Zeile eines Übertrags im Detail: "Übertrag → <Konto>", ohne bekannten Namen
/// nur "Übertrag" (ledger-qvjvs, Prototyp Version 17).
let transferCategoryLabel (model: Model) (entry: LedgerEntryView) : string option =
    if entry.CategoryId.IsSome || not (List.isEmpty entry.Splits) || not entry.IsTransfer then
        None
    else
        match entry.TransferAccountId |> Option.bind (transferAccountName model) with
        | Some name -> Some $"Übertrag → {name}"
        | None -> Some "Übertrag"

/// Das Quick-Add-Konto, wenn es bekannt ist. Solange es lädt oder gescheitert ist, hat die Übertrag-Zeile der
/// Barabhebung kein Ziel und bleibt wählbar (ledger-pz028).
let quickAddAccountOf (model: Model) : YnabAccountId option =
    match model.QuickAddAccount with
    | Success account -> account
    | NotAsked | Loading | Failure _ -> None

/// Läuft ein Nachladen im Hintergrund (ledger-ct3cx)? Solange ruht „Weitere laden“.
let refreshRunning (model: Model) : bool =
    match model.ListActivity with
    | Refreshing _ -> true
    | ListIdle | LoadingMorePage | LoadingMoreThenRefresh -> false

/// Läuft „Weitere laden“ (auch mit wartendem Nachladen)? Solange steht das Skeleton am Ende der Liste.
let nextPageLoading (model: Model) : bool =
    match model.ListActivity with
    | LoadingMorePage | LoadingMoreThenRefresh -> true
    | ListIdle | Refreshing _ -> false

// ============================================
// Bestätigung am Feld: wohin und was beim Schließen (ledger-p5y9s, ADR 0098)
// ============================================

/// Wohin die Rückmeldung einer Aktion im Detail geht (Kategorie, Übertrag, Aufteilung, Memo, Betrag, Payee, Lena).
type DetailNoteRoute =
    /// Das Detail dieser Buchung steht offen (Sheet oder Panel, im Durchgehen mit „Details“): unter dem Feld.
    | NoteAtField
    /// Die Karte des Durchgehens ohne „Details“: wie bisher (ledger-pz5wv regelt sie).
    | NoteOnReviewCard
    /// Das Detail ist zu, als die Antwort kam: in die Leiste, am PC in den Toast.
    | NoteInBar

/// Wohin die Rückmeldung zu dieser Buchung jetzt geht. Rein.
let detailNoteRoute (model: Model) (entryId: Guid) : DetailNoteRoute =
    if detailShows model entryId then NoteAtField
    elif isReviewCard model entryId then NoteOnReviewCard
    else NoteInBar

/// Die Bestätigung als Meldung der Leiste (am PC der Toast): Aktion, Payee oder Grund, „Rückgängig“ wie am Feld.
let detailNoteBar (note: DetailNote) : FeedbackNote =
    { Tone = note.Tone
      Action = note.BarAction
      Detail = note.BarDetail
      Act = note.Undo |> Option.map (fun kind -> { Label = undoToastLabel; Kind = kind }) }

/// Die Bestätigung aus „Details“ als Zeile unter „Passt, weiter“ (ledger-pz5wv Abschnitt 5): Text wie in der Leiste,
/// „Rückgängig“ wie am Feld. Rein.
let withDetailNoteLine (note: DetailNote) (review: ReviewSession) : ReviewSession =
    let line =
        { EntryId = note.EntryId
          Kind = LastFromDetail (note.Tone, note.BarAction, note.BarDetail |> Option.defaultValue "")
          Payee = None
          Outcome = LastDone
          Act = note.Undo |> Option.map LastFieldUndo }
    let stamp = review.Clock + 1
    { review with Last = Some line; Clock = stamp; LastStamp = stamp }

/// Detail schließt mit offener Bestätigung (Entscheidung Roman 2026-11-19): steht ihre Buchung nicht mehr im Detail,
/// endet sie. Trägt sie „Rückgängig“, wandert sie in die Leiste (am PC in den Toast), außer der Schritt meldet selbst
/// etwas (eine neue Aktion ersetzt sie). Im Durchgehen gibt es keinen Toast (ledger-pz5wv Abschnitt 5): dort wandert sie
/// in die Zeile unter „Passt, weiter“, mit „Rückgängig“ wie am Feld. Ohne „Rückgängig“ (Fehler, Warnung) endet sie
/// still. Rein.
let handOverDetailNote (model: Model, external: ExternalMsg) : Model * ExternalMsg =
    match model.DetailNote with
    | Some note when not (detailShows model note.EntryId) ->
        let cleared = { model with DetailNote = None }
        match external, note.Undo, cleared.Review with
        | NoOp, Some _, Some review -> { cleared with Review = Some (withDetailNoteLine note review) }, NoOp
        | NoOp, Some _, None -> cleared, PostFeedback (detailNoteBar note)
        | _ -> cleared, external
    | _ -> model, external

/// Die Bestätigung unter diesem Feld der Buchung, falls eine steht.
let detailNoteAt (model: Model) (entryId: Guid) (field: DetailNoteField) : DetailNote option =
    model.DetailNote |> Option.filter (fun note -> note.EntryId = entryId && note.Field = field)

/// Die Nachricht hinter „Rückgängig“ am Feld: dieselbe wie früher im Toast (`State.toastActionMsg`).
let detailNoteUndoMsg (kind: ToastActionKind) : Msg option =
    match kind with
    | UndoCategory (entryId, previous) -> Some (StartUndoCategory (entryId, previous))
    | UndoTransfer (entryId, account) -> Some (StartSetEntryTransfer (entryId, account, true))
    | UndoSplit (entryId, restore) -> Some (StartUndoSplit (entryId, restore))
    | UndoMemo (entryId, previous) -> Some (StartUndoMemo (entryId, previous))
    | UndoAmount (entryId, previous) -> Some (StartUndoAmount (entryId, previous))
    | UndoPayee (entryId, previous) -> Some (StartUndoPayee (entryId, previous))
    | UndoFlag (entryId, previous) -> Some (StartUndoFlag (entryId, previous))
    | _ -> None
