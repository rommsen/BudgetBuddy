/// Die Kassenbuch-Seite (ledger-q5w9c, seitenweise Liste und Kontostaende-Leiste
/// ledger-b1xwe): Kontostaende oben, darunter die Buchungsliste eines Kontos oder
/// aller Konten, mit Herkunfts-Badge, Historie je Buchung und dem manuellen
/// "Jetzt spiegeln".
///
/// Ausschliesslich Design-System-Komponenten und Tokens (Styleguide-Gate
/// `design-system-001`); keine Inline-Farben, kein selbstgebautes Overlay — die
/// Historie liegt im `BottomSheet` des DS, das die Visual-Viewport- und
/// Click-Commit-Mechanik aus ADR 0005 bereits mitbringt.
module Components.Ledger.View

open System
open Fable.Core
open Feliz
open Shared.Domain
open Types
open Components.Ledger.Types
open Client.DesignSystem

/// Der Split-Editor: Bausteine im Design-System, Rechnung und Entwurf im Kassenbuch. Die Namen
/// sind getrennt, weil beide Module `SplitEditor` heißen.
module SplitUi = Client.DesignSystem.SplitEditor
module SplitLogic = Components.Ledger.SplitEditor

// ============================================
// Formatierung
// ============================================

/// Die Datumsformate hier sind feste numerische Format-Strings ("dd.MM.yyyy",
/// "HH:mm"): Punkt und Doppelpunkt kommen aus dem Format, nicht aus der Kultur.
/// Deshalb genügt die invariante Kultur, im Browser (Fable) wie unter `dotnet test`,
/// das wie der Server-Container ohne Kulturdaten läuft (`InvariantGlobalization` in
/// Tests.fsproj, dort gibt es "de-DE" nicht). Bewusst *keine* gemeinsame
/// Modul-Konstante: ein Top-Level-`let`-Wert würde beim ersten Zugriff das gesamte
/// Modul initialisieren (F# "startup code"), inklusive der `Badge.view`-Werte weiter
/// unten in dieser Datei; unter `dotnet test` (kein Fable/Feliz-Laufzeitkontext)
/// reißt das eine bestehende `Client.DesignSystem.Badge`-Feliz-Prop in eine
/// `InvalidCastException`. Der Name bleibt, weil das Ergebnis deutsch geschrieben ist.
let private germanCulture () = System.Globalization.CultureInfo.InvariantCulture

/// Volles Datum, fuer die Historie/das Detail (AC3: "das volle Datum bleibt im
/// Detail sichtbar").
let private formatDateFull (d: DateTime) = d.ToString("dd.MM.yyyy", germanCulture ())

let private formatDateTime (d: DateTime) = d.ToLocalTime().ToString("dd.MM.yyyy HH:mm", germanCulture ())

/// Das Datum einer Buchung in der Liste (AC3): `Heute`, `Gestern`, sonst `08.09.`
/// ohne Jahr. Pure Funktion mit explizitem `today`, damit sie ohne Systemzeit-Mocking
/// testbar ist (`LedgerViewTests`); `formatDate` bindet sie an `DateTime.Today` fuer
/// die eigentliche Seite.
let formatEntryDate (today: DateTime) (d: DateTime) : string =
    match (d.Date - today.Date).Days with
    | 0 -> "Heute"
    | -1 -> "Gestern"
    | _ -> d.ToString("dd.MM.", germanCulture ())

let private formatDate (d: DateTime) = formatEntryDate DateTime.Today d

/// Tagesgruppen und Tagessummen gibt es nur bei Sortierung nach Datum, in beiden
/// Richtungen (ledger-bn913). Jede andere Sortierung macht die Liste flach.
let usesDayGroups (sort: LedgerSort) : bool = sort.Field = SortByDate

/// Das Datum in Zeile 2 der Handy-Zeile: nur in der flachen Liste, vorn vor Kontokürzel und
/// Kategorie (ledger-bn913). In der gruppierten Liste steht es im Tageskopf.
let mobileRowDateText (today: DateTime) (flat: bool) (entry: LedgerEntryView) : string option =
    if flat then Some (formatEntryDate today entry.Date) else None

/// Die Sortierrichtung des Kopfs `field` für die Tabelle: nur der aktive Kopf trägt eine.
let headerSortDirection (sort: LedgerSort) (field: LedgerSortField) : Table.SortDirection =
    if sort.Field <> field then Table.Unsorted
    else
        match sort.Direction with
        | SortAsc -> Table.Ascending
        | SortDesc -> Table.Descending

// ============================================
// Reine Bausteine (unit-testbar ohne DOM, siehe LedgerViewTests)
// ============================================

/// Ein kurzes Kuerzel aus dem Kontonamen fuer die Gesamtsicht (AC4): das erste Wort,
/// bis zu vier Zeichen, gross geschrieben. Es gibt kein eigenes Kuerzel-Feld im
/// Modell — dieses Kuerzel ist eine Anzeige-Ableitung der Seite, kein Fachbegriff.
let accountAbbreviation (name: string) : string =
    let trimmed = name.Trim()
    if trimmed = "" then
        "?"
    else
        let firstWord =
            trimmed.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.tryHead
            |> Option.defaultValue trimmed
        firstWord.ToUpperInvariant().Substring(0, min 4 firstWord.Length)

/// Der Kalendertag einer Buchung als Schlüssel der Tagesgruppe: Jahr, Monat, Tag ohne
/// Uhrzeit und ohne Kind. Die Spalte `date` mischt Kinds (Spiegel `Utc`, Comdirect und
/// Quick-Add ohne Kind). Im Browser macht `DateTime.Parse` aus einem Wert mit `Z` Kind Local
/// (in Berlin 01:00), ein Wert ohne Suffix bleibt auf 00:00; Fable vergleicht die Wanduhrzeit,
/// derselbe Tag ergibt so zwei ungleiche Werte. `Year`/`Month`/`Day` lesen bei jedem Kind den
/// angezeigten Tag (ledger-pnjz3).
let entryDay (d: DateTime) : DateTime = DateTime(d.Year, d.Month, d.Day)

/// Die Tagesgruppen der Liste bei Sortierung nach Datum, für Handy-Liste und PC-Tabelle
/// (AC6, ledger-pnjz3): jeder Kalendertag genau einmal, die Tage in der Richtung der
/// Sortierung, die Zeilen eines Tages in Listenreihenfolge. Jede Gruppe hat mindestens eine
/// Zeile, es gibt also keinen leeren Tageskopf. Der Tag ist zugleich der React-Key des Kopfs
/// (`group-yyyy-MM-dd`); trug ein Tag zwei Gruppen, ließ React bei doppeltem Key alte Köpfe
/// als verwaiste Knoten im DOM stehen (Romans Blick 2026-11-14).
let groupByDate (direction: LedgerSortDirection) (entries: LedgerEntryView list) : (DateTime * LedgerEntryView list) list =
    let groups = entries |> List.groupBy (fun e -> entryDay e.Date)
    match direction with
    | SortDesc -> groups |> List.sortByDescending fst
    | SortAsc -> groups |> List.sortBy fst

/// Was Handy-Liste und PC-Tabelle zeigen: bei Sortierung nach Datum die Tagesgruppen in der
/// Richtung der Sortierung, sonst `None` für die flache Liste ohne Tagesköpfe. Beide Ansichten
/// rufen nur diese Funktion auf, damit die Richtung an einer Stelle steht.
let dayGroups (sort: LedgerSort) (entries: LedgerEntryView list) : (DateTime * LedgerEntryView list) list option =
    if usesDayGroups sort then Some (groupByDate sort.Direction entries) else None

/// Die Tagessumme einer Datumsgruppe (ADR 0049, Punkt 8; `ledger-n4vte`): die Summe
/// der *sichtbaren* Zeilen dieser Gruppe. Eine in YNAB gelöschte Buchung zählt nur
/// mit, wenn sie eingeblendet ist (`includeDeleted`, Entscheidung `ledger-d4kx8`):
/// dieselbe Regel wie die Summenzeile über der Liste. Rein, damit PC-Tabelle und
/// Handy-Liste dieselbe Zahl zeigen und sie ohne DOM testbar ist.
let daySum (includeDeleted: bool) (group: LedgerEntryView list) : Money =
    group
    |> List.filter (fun e -> includeDeleted || not e.DeletedInYnab)
    |> List.map (fun e -> e.Amount)
    |> sumMoney "EUR"

/// Der Text der Tagessumme im Gruppenkopf: Eingang mit `+`, Ausgabe mit `-`
/// (Zielbild "+3.774,00 €", "-54,52 €").
let daySumText (sum: Money) : string =
    Money.formatAmountText (sum.Amount > 0m) true sum.Amount sum.Currency

/// Der Satz über dem Schalter "gelöschte Buchungen zeigen" (ledger-p3n8k AC2,
/// erweitert um Entscheidung Roman 2026-09-18 / `ledger-d4kx8`, Variante 2).
///
/// Ausgeblendet (Standard): nennt nur, wie viele gelöschte Buchungen fehlen, wie
/// bisher ("N gelöschte Buchungen ausgeblendet").
///
/// Eingeblendet: gelöschte Buchungen zählen dann in Trefferzahl UND Summe mit (am
/// Code ändert sich dabei nichts, `getLedgerEntriesPage` tut das schon). Die Zeile
/// sagt das jetzt, im Stil "13 Buchungen, davon 1 in YNAB gelöscht", statt nur
/// "eingeblendet" zu melden: sonst bliebe unklar, dass sie in der Summe mitzählen.
/// Singular/Plural richtet sich hier nach der Trefferzahl (`totalCount`), nicht nach
/// der Zahl der gelöschten.
let deletedCountText (totalCount: int) (deletedCount: int) (includeDeleted: bool) : string =
    if includeDeleted then
        let noun = if totalCount = 1 then "Buchung" else "Buchungen"
        $"{totalCount} {noun}, davon {deletedCount} in YNAB gelöscht"
    else
        let noun = if deletedCount = 1 then "gelöschte Buchung" else "gelöschte Buchungen"
        $"{deletedCount} {noun} ausgeblendet"

/// Der Text des Chips "Zu prüfen (n)" (ADR 0032). Reine Funktion, unit-testbar ohne
/// Fable/Badge-Kontext.
let needsReviewChipLabel (count: int) : string = $"Zu prüfen ({count})"

/// Was eine Karte der Kontostände-Leiste an der Stelle des Betrags zeigt (ledger-vq2g8):
/// den geladenen Stand, einen Platzhalter, solange die Stände laden, oder einen Strich,
/// wenn der Abruf scheiterte. Ein erfundener Nullwert war vorher von einem echten Stand
/// nicht zu unterscheiden.
type CardBalance =
    | BalanceShown of Money
    | BalancePending
    | BalanceUnavailable

/// Eine Karte der Kontostände-Leiste: ein Filter (Konto oder "Alle Konten"), ihr
/// Anzeigename und ihr Stand.
type AccountCard = {
    Filter: AccountFilter
    Label: string
    Balance: CardBalance
}

/// Baut die Karten der Kontostände-Leiste (AC3 ledger-b1xwe): "Alle Konten" steht immer
/// zuerst und trägt die Summe aller Konten, danach jedes Konto mit seinem eigenen Stand.
/// Die Karten stehen in jedem Ladezustand, weil sie auch der Kontofilter sind. Nur eine
/// erfolgreiche Antwort liefert Beträge; ein Konto ohne Eintrag darin hat keine Buchungen
/// und damit den Stand Null (ledger-vq2g8).
let buildAccountCards (accounts: LedgerAccountView list) (balances: RemoteData<LedgerAccountBalanceView list>) : AccountCard list =
    let balanceOf, total =
        match balances with
        | Success loaded ->
            let ofAccount (accountId: Guid) =
                loaded
                |> List.tryFind (fun b -> b.AccountId = accountId)
                |> Option.map (fun b -> b.Balance)
                |> Option.defaultValue { Amount = 0m; Currency = "EUR" }
                |> BalanceShown
            ofAccount, BalanceShown (sumMoney "EUR" (loaded |> List.map (fun b -> b.Balance)))
        | Failure _ -> (fun _ -> BalanceUnavailable), BalanceUnavailable
        | NotAsked | Loading -> (fun _ -> BalancePending), BalancePending

    let accountCards =
        accounts
        |> List.map (fun a -> { Filter = OneAccount a.Id; Label = a.Name; Balance = balanceOf a.Id })

    { Filter = AllAccounts; Label = "Alle Konten"; Balance = total } :: accountCards

/// Die Zeile über der Kontostände-Leiste, wenn der Abruf scheiterte (ledger-vq2g8,
/// Entscheidung Roman 2026-11-07, Variante A): Text, Beschriftung des Knopfs und die
/// Nachricht, die er schickt.
type BalancesNotice = {
    Text: string
    RetryLabel: string
    Retry: Msg
}

let balancesFailedText = "Kontostände konnten nicht geladen werden"

/// Das Nachladen scheiterte, die alten Beträge stehen weiter (ledger-pfhdq, Entscheidung Roman 2026-11-19, Variante A).
let balancesRefreshFailedText = "Kontostände konnten nicht aktualisiert werden"

/// `refreshError` ist `BalancesRefreshError` des Modells: gesetzt, wenn das Nachladen bei stehenden Ständen scheiterte.
let balancesNotice
    (balances: RemoteData<LedgerAccountBalanceView list>)
    (refreshError: string option)
    : BalancesNotice option =
    match balances, refreshError with
    | Failure _, _ -> Some { Text = balancesFailedText; RetryLabel = "Erneut laden"; Retry = LoadBalances }
    | Success _, Some _ -> Some { Text = balancesRefreshFailedText; RetryLabel = "Erneut laden"; Retry = LoadBalances }
    | _ -> None

// ============================================
// Bausteine
// ============================================

/// Kennzeichen einer in YNAB geloeschten Buchung. Sie bleibt stehen, zaehlt aber
/// nicht mehr zum Kontostand (ADR 0013).
let private deletedBadge () =
    Badge.view {
        Badge.defaultProps with
            Text = "in YNAB gelöscht"
            Variant = Badge.Error
            Size = Badge.Small
    }

/// Das Zustands-Kennzeichen einer Zeile (Handy-Zeile, Payee-Zelle der Tabelle) in der Rangfolge: in YNAB
/// gelöscht vor wartend („doppelt?“, statt „Nicht in YNAB“) vor noch nicht in YNAB vor den
/// übrigen Zuständen (Zu prüfen, Flag).
type RowStatusMark =
    | RowDeleted
    | RowPossibleDuplicate
    | RowNotInYnab
    | RowOtherStates

let rowStatusMark (entry: LedgerEntryView) : RowStatusMark =
    if entry.DeletedInYnab then RowDeleted
    elif entry.PossibleDuplicate.IsSome then RowPossibleDuplicate
    elif not entry.LinkedToYnab then RowNotInYnab
    else RowOtherStates

/// Die Kennzeichen hinter dem Namen in der Payee-Zelle der PC-Tabelle (ledger-cnaz3, Prototyp Version 17
/// `ledgerDesk`), vorher in der Spalte Status: das Flag (Lena-Marke oder „Markiert“), dann der Zustand in der
/// Rangfolge von `rowStatusMark`: „doppelt?“ in Gelb, „nicht in YNAB“ in Rot oder das Gelöscht-Zeichen. „zu
/// prüfen“ ist kein Wort mehr, nur die gelbe Kante der Zeile.
type PayeeMark =
    | PayeeFlag of LedgerFlag
    | PayeePossibleDuplicate
    | PayeeNotInYnab
    | PayeeDeleted

let payeeMarks (entry: LedgerEntryView) : PayeeMark list =
    [ if entry.Flag <> NoFlag then PayeeFlag entry.Flag
      match rowStatusMark entry with
      | RowPossibleDuplicate -> PayeePossibleDuplicate
      | RowNotInYnab -> PayeeNotInYnab
      | RowDeleted -> PayeeDeleted
      | RowOtherStates -> () ]

/// Trägt die volle Bedeutung eines reinen Symbol-Kennzeichens der Payee-Zelle nach (`payeeMarkView`), als
/// Hover-Hinweis (`title`) und für assistive Technologie (`aria-label`, ledger-qns4j).
let private withAccessibleName (fullMeaning: string) (badge: ReactElement) : ReactElement =
    Html.span [
        prop.title fullMeaning
        prop.custom ("aria-label", fullMeaning)
        prop.children [ badge ]
    ]

/// Das Gelöscht-Zeichen in der Payee-Zelle der PC-Tabelle: ein Symbol statt des Texts aus `deletedBadge`, damit
/// der Payee-Name Platz behält; `withAccessibleName` trägt „in YNAB gelöscht“ nach. Die Handy-Zeile behält den Text.
let private deletedStatusIcon () =
    withAccessibleName "in YNAB gelöscht" (
        Badge.view {
            Badge.defaultProps with
                Text = ""
                Icon = Some (Icons.trash Icons.XS Icons.Error)
                Variant = Badge.Error
                Size = Badge.Small
        })

/// *Rückfrage Lena* in einer Zeile des Kassenbuchs, am Handy und am PC gleich: Flag-Symbol in der Lena-Farbe mit
/// „Lena“ (ADR 0087 Punkt 8, wie der Filter seit ledger-rb3xw), der volle Name für Hover und Vorleser.
/// Eine Funktion und kein Wert: ein ReactElement auf Modulebene bricht unter .NET den Typinitialisierer.
let private askLenaRowMark () : ReactElement =
    withAccessibleName "Rückfrage Lena" (Badge.lenaMark "Lena")

/// Das Kennzeichen eines gesetzten Flags (ADR 0032) in der Handy-Zeile (`entryRow`): *Rückfrage Lena* als
/// `askLenaRowMark`, ohne farbiges Badge; *Markiert* neutral mit Namen. `NoFlag` zeigt nichts.
let private flagBadge (flag: LedgerFlag) : ReactElement =
    match flag, flagLabel flag with
    | _, None -> Html.none
    | AskLena, Some _ -> askLenaRowMark ()
    | _, Some label -> Badge.view { Badge.defaultProps with Text = label; Variant = Badge.Neutral; Size = Badge.Small }

/// Das Flag in der Payee-Zelle der PC-Tabelle (Prototyp Version 17 `ledgerDesk`): *Rückfrage Lena* als
/// `askLenaRowMark`, *Markiert* als neutrales Symbol mit Namen für Hover und Vorleser.
let private flagStatusBadge (flag: LedgerFlag) : ReactElement =
    match flag with
    | NoFlag -> Html.none
    | AskLena -> askLenaRowMark ()
    | Marked ->
        withAccessibleName "Markiert" (
            Badge.view {
                Badge.defaultProps with
                    Text = ""
                    Icon = Some (Icons.flag Icons.XS Icons.Muted)
                    Variant = Badge.Neutral
                    Size = Badge.Small
            })

/// Der Betrag einer Zeile über die Listenvariante (ADR 0049, ADR 0087): neutral, auch ein Eingang,
/// ohne Glow. Gedämpft und durchgestrichen, wenn die Buchung in YNAB gelöscht ist
/// (AC3, ledger-p3n8k).
let private entryMoney (entry: LedgerEntryView) =
    if entry.DeletedInYnab then
        Money.transactionDeleted entry.Amount.Amount entry.Amount.Currency
    else
        Money.listAmount entry.Amount.Amount entry.Amount.Currency

/// Die Kategorie-Beschriftung einer Buchung, als Blattname (AC1/AC2,
/// ledger-w9skm): der Kategoriename, bei einer aufgeteilten Buchung ohne eigene
/// Kategorie die Anzahl der Split-Zeilen, sonst der Platzhaltertext für eine
/// fehlende Kategorie (siehe Rückfallzeile unten). Reine Funktion, von der
/// Handy-Zeile (`entryRow`) benutzt (siehe `LedgerViewTests`; der Platzhaltertext
/// steht bewusst nur an dieser einen Stelle im Modul, ledger-n3pqw).
///
/// Die PC-Tabelle (`entryTableRow`) benutzt stattdessen `categoryLabelQualified`
/// unten: seit ADR 0039 zwei benannte Funktionen über derselben Quelle statt
/// eines zweiten Zweigs in den Views, weil der Entwurf für beide Ansichten
/// unterschiedliche Formen verlangt (Blattname am Handy, volle Qualifizierung am
/// PC).
let categoryLabel (entry: LedgerEntryView) : string =
    match entry.CategoryName with
    | Some name -> name
    | None when not (List.isEmpty entry.Splits) -> $"{List.length entry.Splits} Split-Zeilen"
    | None when entry.IsTransfer -> "Übertrag"
    | None -> "ohne Kategorie"

/// Ob eine Buchung wirklich "ohne Kategorie" ist, also ein offener Fall (ADR 0048):
/// weder Kategorie noch Split noch Übertrag. Nur dann färbt die Liste die Kategorie
/// Gelb (ADR 0087 Punkt 3); ein Übertrag trägt ein Ziel und bleibt neutral.
let isWithoutCategory (entry: LedgerEntryView) : bool =
    entry.CategoryName.IsNone && List.isEmpty entry.Splits && not entry.IsTransfer

/// Die gemeinsame ": "-Zusammensetzung (ADR 0045 Punkt 4): "Gruppe: Blatt" bei bekannter,
/// nicht leerer Gruppe, sonst unverändert der Blattname. Ein leerer Blattname (nach
/// `Trim()`) wird nie qualifiziert, es gibt kein "Gruppe: " ohne Blatt, kein führendes
/// ": " und keinen erfundenen Gruppennamen. Kopf-Kategorie (`categoryLabelQualified`)
/// und Split-Zeilen im Detail (`splitTargetLabel`) benutzen dieselbe Funktion.
let qualify (groupName: string option) (leafName: string) : string =
    match groupName with
    | Some group when not (String.IsNullOrWhiteSpace group) && not (String.IsNullOrWhiteSpace leafName) ->
        $"{group.Trim()}: {leafName}"
    | _ -> leafName

/// Die Kategorie-Beschriftung voll qualifiziert ("Gruppe: Name", Handoff 1.9 Zeile
/// 260), für die PC-Tabelle (ADR 0039, ledger-h6ptn). `CategoryGroupName` kommt
/// aus dem Lesepfad (`ledger_categories`, `ledger-p3wnq`), nicht aus dem
/// Ereignis-Log: fehlt sie (Kategorie nie gespiegelt) oder ist sie nach `Trim()`
/// leer, fällt diese Funktion auf `categoryLabel` zurück, nie ein führendes
/// ": " und nie ein erfundener Gruppenname. Fehlt jede eigene Kategorie (Split
/// oder Platzhaltertext), liefert `categoryLabel` unverändert dasselbe Ergebnis,
/// weil es dann nichts zu qualifizieren gibt.
let categoryLabelQualified (entry: LedgerEntryView) : string =
    match entry.CategoryName with
    | Some name -> qualify entry.CategoryGroupName name
    | None -> categoryLabel entry

/// Die zwei Formen einer Listenzeile: die Zeile der PC-Tabelle und die Zeile am Handy.
type ListRowForm =
    | TableRow
    | PhoneRow

/// Die Kategorie in der Liste (ADR 0039): die PC-Tabelle qualifiziert ("Gruppe: Name"), die Handy-Zeile
/// zeigt den Blattnamen. infrastructure-t5m2b: aus beiden Views gezogen, damit die Wahl unter .NET prüfbar ist.
let listCategoryLabel (form: ListRowForm) (entry: LedgerEntryView) : string =
    match form with
    | TableRow -> categoryLabelQualified entry
    | PhoneRow -> categoryLabel entry

/// Der Payee-Text einer Buchung: der Payee, sonst der Platzhaltertext für eine
/// fehlende Angabe (siehe Rückfallzeile unten). Reine Funktion, analog zu
/// `categoryLabel` geteilt zwischen Handy-Zeile (`entryRow`) und PC-Tabelle
/// (`entryTableRow`), damit beide Ansichten denselben Platzhaltertext zeigen
/// (ledger-n3pqw, Lücke 9 aus dem QA-Review: vorher stand dieser Platzhalter an
/// zwei Stellen im File, je einmal pro Ansicht).
/// Steht statt eines Payees der Platzhalter (ledger-pkrwd)? Dann in `text-muted`, in Liste, Tabelle, Detail
/// und Karte. Leer oder nur Leerzeichen zählt als kein Payee.
let payeeMissing (entry: LedgerEntryView) : bool =
    entry.Payee |> Option.forall String.IsNullOrWhiteSpace

let payeeLabel (entry: LedgerEntryView) : string =
    if payeeMissing entry then "ohne Payee" else entry.Payee.Value

/// Die Beschriftung des Menüeintrags "Nach <Payee> filtern" (ledger-rfq5p). `None`, wenn
/// es nichts zu filtern gibt: Buchung ohne Payee (leer oder nur Leerzeichen).
let payeeMenuLabel (entry: LedgerEntryView) : string option =
    entry.Payee
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.map (fun payee -> $"Nach {payee.Trim()} filtern")

/// Die Beschriftung des Menüeintrags "Nach <Kategorie> filtern" (ledger-rfq5p). `Some`
/// nur bei gesetzter `CategoryId` und leerer Split-Liste: ein Split zeigt keine einzelne
/// Kategorie (auch ein ausgeblendeter Split mit einer Kategorie, ADR 0038), ein Übertrag
/// ohne Kategorie hat nichts zu filtern. Der Text gleicht der PC-Zelle.
let categoryMenuLabel (entry: LedgerEntryView) : string option =
    match entry.CategoryId, entry.Splits with
    | Some _, [] -> Some $"Nach {categoryLabelQualified entry} filtern"
    | _ -> None

/// Die Klassen der Kategorie in einer Listenzeile (ADR 0049 Punkt 4): die
/// sekundäre Rolle (12px, `text-secondary`), nicht mehr violett. Nur "ohne
/// Kategorie" ist Gelb, weil es ein offener Fall ist (ADR 0087 Punkt 3). Geteilt zwischen
/// Handy-Zeile und PC-Tabelle.
let categoryClassName (entry: LedgerEntryView) : string =
    if isWithoutCategory entry then
        $"{Tokens.FontSizes.xs} {Tokens.Colors.attention}"
    else
        Tokens.ListRoles.secondary

/// Klassen der Handy-Listenzeile (AC3, ledger-w9skm): flache Zeile mit einer
/// Unterkante als Trenner statt einer Karte mit Rahmen und Eckenradius. Der
/// Innenabstand kommt aus der Listenrolle (`mobileRowPadding`, ADR 0049). Als
/// eigener Wert ausgelagert, weil `dotnet test` (kein Fable/React-Laufzeitkontext)
/// das gerenderte `Html.button` selbst nicht prüfen kann; der Klassen-String ist
/// aber reiner Text und wird von `entryRow` unverändert als `prop.className` gesetzt.
let entryRowClassName : string =
    $"w-full text-left {Tokens.ListRoles.mobileRowPadding} transition-colors touch-manipulation " +
    // `hover:bg-surface-hover` steht bewusst als Literal: Tailwind scannt den
    // Quelltext nach ganzen Klassennamen, eine zusammengesetzte Variante
    // (`hover:` + Token) fiele beim Scan durch und würde nie erzeugt.
    $"{Tokens.Backgrounds.dark} hover:bg-surface-hover " +
    $"border-b {Tokens.Borders.subtle}"

/// Dieselbe Zeile mit der Statuskante für "zu prüfen" (ADR 0049 Punkt 7): die Kante
/// ist ein inset-Schatten, die Zellinhalte verrutschen nicht.
let entryRowReviewClassName : string =
    $"{entryRowClassName} {Tokens.ListRoles.statusEdge}"

/// Die Handy-Zeile im Auswahl-Modus, wenn sie gewählt ist (ledger-r1d2f): derselbe Zuschnitt,
/// aber mit blassem Akzent als Grund statt der Kartenfarbe. Der Karten-Grund liegt im Rahmen
/// der Zeile, der Akzent darüber bleibt durchscheinend.
let entryRowSelectedClassName : string =
    $"w-full text-left {Tokens.ListRoles.mobileRowPadding} transition-colors touch-manipulation " +
    $"{Tokens.ListRoles.selectedFill} border-b {Tokens.Borders.subtle}"

/// Die Kante links einer Zeile in der Kompaktliste mit Maus und in der Tabelle: der Anker der
/// Spanne trägt die Akzent-Kante, eine Buchung "zu prüfen" die gelbe Statuskante. Beide Kanten
/// sind ein Schatten; trifft beides zu, steht ein Schatten mit beiden Kanten da (Gelb außen,
/// Akzent innen), sonst überschriebe der eine den anderen (§2 „Ruhige Listen“, ledger-ef068).
let rowEdgeClassName (isAnchor: bool) (needsReview: bool) : string option =
    match isAnchor, needsReview with
    | true, true -> Some Tokens.ListRoles.statusEdgeWithAnchor
    | true, false -> Some Tokens.ListRoles.selectedEdge
    | false, true -> Some Tokens.ListRoles.statusEdge
    | false, false -> None

/// Der Zustand einer Kompaktzeile mit Maus (ledger-m6zpd): gewählt, Anker der Spanne, von der
/// Tastatur markiert.
type MouseRowInfo = { Selected: bool; IsAnchor: bool; IsFocused: bool }

/// Klassen des Containers einer Kompaktzeile mit Maus. Er trägt Grund, Unterkante und die
/// Kanten, damit Kästchen, Zeile und Schaltfläche gemeinsam hervorgehoben sind: gewählt
/// blasser Akzent, markiert die Fokus-Darstellung der Tabellenzeile (`Table.focusedRowClass`),
/// sonst der Kartengrund mit Hover. Die Kante kommt aus `rowEdgeClassName` wie in der Tabelle:
/// Anker und "zu prüfen" zugleich tragen beide Kanten.
let mouseRowContainerClassName (info: MouseRowInfo) (needsReview: bool) : string =
    [ "group flex items-stretch transition-colors"
      $"border-b {Tokens.Borders.subtle}"
      if info.Selected then Tokens.ListRoles.selectedFill
      elif info.IsFocused then Table.focusedRowClass
      else $"{Tokens.Backgrounds.dark} hover:bg-surface-hover"
      yield! Option.toList (rowEdgeClassName info.IsAnchor needsReview) ]
    |> String.concat " "

/// Ein Kennzeichen in Zeile 2 der Handy-Zeile, das nicht schrumpfen darf.
let private keepWidth (child: ReactElement) =
    Html.span [ prop.className "shrink-0"; prop.children [ child ] ]

/// Der Hinweis "Prüfvermerk nicht in YNAB" mit Grund und "Erneut senden" (ADR 0050,
/// Punkt 11). Ein Wort in Gelb und der Grund gedämpft: er fällt auf, ohne die ruhige
/// Liste (ADR 0049) zu stören. Die Schaltfläche sendet den aktuellen Stand erneut.
let private reviewMarkHintView
    (dispatch: Msg -> unit)
    (rowState: ReviewMarkRowState option)
    (entryId: Guid)
    (hint: ReviewMarkHint)
    : ReactElement =
    let sending = (rowState = Some ReviewMarkSending)

    Html.div [
        prop.role "status"
        prop.className "flex items-center gap-2 min-w-0"
        prop.children [
            Html.span [
                prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.attention} font-medium shrink-0"
                prop.text hint.Title
            ]
            Truncate.text Tokens.ListRoles.muted hint.Reason
            match hint.RetryLabel with
            | Some label ->
                Button.view {
                    Button.defaultProps with
                        Text = label
                        Variant = Button.Ghost
                        Size = Button.Small
                        IsLoading = sending
                        ClassName = Some "shrink-0"
                        OnClick = fun () -> dispatch (StartRetryReviewMark entryId)
                }
            | None -> ()
        ]
    ]

/// Die Wörter in Zeile 2 der Handy-Zeile (ledger-d9z3n, Prototyp `txRow`): „nicht in YNAB“, „doppelt?“ und
/// „zu prüfen“ unter dem Betrag in Gelb. „nicht in YNAB“ in Gelb wie die Leiste „Noch nicht in YNAB“ im Detail
/// (ADR 0087, Roman 2026-11-15; der Prototyp hat Rot).
let rowNotInYnabText = "nicht in YNAB"
let rowPossibleDuplicateText = "doppelt?"
let rowNeedsReviewText = "zu prüfen"

/// Ein Zustandswort in Zeile 2 der Handy-Zeile, klein, in Gelb für „braucht dich“ (ADR 0087 Punkte 3 und 4).
let private rowAttentionWord (text: string) : ReactElement =
    Html.span [ prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.attention} whitespace-nowrap"; prop.text text ]

/// Was ein Klick auf eine Zeile am PC auslöst (§7.7, ledger-cnaz3): Strg oder Cmd nimmt sie in die Auswahl oder
/// heraus, Umschalt wählt den Bereich ab dem Anker, ein Klick ohne Taste öffnet das Detail wie bisher.
let rowClickMsg (entry: LedgerEntryView) (keys: Table.RowClickKeys) : Msg =
    if keys.CtrlOrMeta then ClickRowWithModifier (entry.Id, ToggleRow)
    elif keys.Shift then ClickRowWithModifier (entry.Id, RangeToRow)
    else OpenDetail entry

/// Eine Zeile der Buchungsliste am Handy, zweizeilig (ADR 0049 Punkt 6): Zeile 1
/// Payee und Betrag, Zeile 2 Kategorie und Memo mit den Kennzeichen. Beide Zeilen
/// haben eine feste Höhe (`h-5`), damit der Abstand zwischen ihnen in jeder Zeile
/// gleich ist, auch wenn Zeile 2 ein Kennzeichen trägt. `accountAbbreviationOf` ist
/// nur in der Gesamtsicht gesetzt (AC4): das Konto steht dann als Kürzel-Badge in
/// Zeile 2. Die Herkunft steht nicht mehr in der Zeile, nur noch in der Historie.
let private entryRow
    (onClick: Browser.Types.MouseEvent -> unit)
    (selected: bool option)
    (mouse: MouseRowInfo option)
    (accountNameOf: (Guid -> string) option)
    (flatDate: bool)
    (entry: LedgerEntryView)
    =
    // `mouse`: die Zeile der Kompaktliste mit Maus (ledger-m6zpd). Sie ist kein Knopf, sondern
    // ein `div` mit Klick wie die `tr` der Tabelle: ein Knopf behielte nach dem Klick den
    // DOM-Fokus, und Enter und Leertaste wären dann seine eigene Aktivierung (`isActivatable`)
    // statt der Tasten der markierten Zeile. Grund und Kanten trägt der Container.
    // `selected`: `None` im Normalbetrieb, `Some` im Auswahl-Modus (die Zeile schaltet dann
    // die Auswahl, ledger-r1d2f). Eine gewählte Zeile tauscht den Grund gegen blasser Akzent.
    let rowClass =
        match selected, entry.NeedsReview with
        | Some true, true -> $"{entryRowSelectedClassName} {Tokens.ListRoles.statusEdge}"
        | Some true, false -> entryRowSelectedClassName
        | _, true -> entryRowReviewClassName
        | _, false -> entryRowClassName

    let element : IReactProperty list -> ReactElement =
        if mouse.IsSome then (fun props -> Html.div props) else (fun props -> Html.button props)
    element [
        // `min-w-0 flex-1`: die Zeile teilt sich den Platz mit der Schaltfläche daneben
        // (`entryRowWithReviewMark`) und darf dafür schrumpfen.
        match mouse with
        | Some _ ->
            prop.className $"w-full text-left {Tokens.ListRoles.mobileRowPadding} cursor-pointer min-w-0 flex-1"
        | None -> prop.className $"{rowClass} min-w-0 flex-1"
        prop.onClick onClick
        match selected with
        | Some isOn ->
            prop.role "checkbox"
            prop.custom ("aria-checked", (if isOn then "true" else "false"))
        | None -> ()
        prop.children [
            Html.div [
                prop.className Tokens.ListRoles.mobileLine1
                prop.children [
                    Truncate.text
                        (if payeeMissing entry then Tokens.ListRoles.primaryMissing else Tokens.ListRoles.primary)
                        (payeeLabel entry)
                    Html.span [ prop.className "shrink-0"; prop.children [ entryMoney entry ] ]
                ]
            ]
            Html.div [
                prop.className Tokens.ListRoles.mobileLine2
                prop.children [
                    match mobileRowDateText DateTime.Today flatDate entry with
                    | Some text -> Html.span [ prop.className $"{Tokens.ListRoles.muted} shrink-0"; prop.text text ]
                    | None -> ()
                    // Das Konto als gedämpfter Text, nur in „Alle Konten“ (ledger-d9z3n, Prototyp `txRow`).
                    match accountNameOf with
                    | Some nameOf -> Truncate.text $"{Tokens.ListRoles.muted} shrink-0 max-w-1/3" (nameOf entry.AccountId)
                    | None -> ()
                    // Die Kategorie darf schrumpfen, damit „zu prüfen“ auch bei 320 px im Bild bleibt (ledger-d9z3n).
                    Truncate.text $"{categoryClassName entry} min-w-0 max-w-3/5" (listCategoryLabel PhoneRow entry)
                    if entry.Flag <> NoFlag then keepWidth (flagBadge entry.Flag)
                    // Das Memo gibt zuerst Platz ab (`shrink-8`), die Kategorie erst danach.
                    match entry.Memo |> Option.filter (String.IsNullOrWhiteSpace >> not) with
                    | Some memo ->
                        Truncate.text $"{Tokens.ListRoles.muted} shrink-8" memo
                    | None -> ()
                    match rowStatusMark entry with
                    | RowDeleted -> keepWidth (deletedBadge ())
                    | RowNotInYnab -> keepWidth (rowAttentionWord rowNotInYnabText)
                    | RowPossibleDuplicate -> keepWidth (rowAttentionWord rowPossibleDuplicateText)
                    | RowOtherStates -> ()
                    // „zu prüfen“ steht unter dem Betrag, zur gelben Kante (Prototyp `txRow`).
                    if entry.NeedsReview then
                        Html.span [
                            prop.custom ("data-testid", "row-needs-review")
                            prop.className $"ml-auto shrink-0 pl-2 {Tokens.ListRoles.missing}"
                            prop.text rowNeedsReviewText
                        ]
                ]
            ]
        ]
    ]

let private entryRowWithReviewMark
    (dispatch: Msg -> unit)
    (accountNameOf: (Guid -> string) option)
    (rowState: ReviewMarkRowState option)
    (selected: bool option)
    (mouse: MouseRowInfo option)
    (flatDate: bool)
    (entry: LedgerEntryView)
    =
    // Außen ein Container: die Zeile ist ein `button` (öffnet das Detail), das Kästchen der Maus-Zeile liegt
    // daneben und nicht darin (ein Button im Button ist ungültig). Der Hinweis "Prüfvermerk nicht in YNAB"
    // steht darunter. Eine Prüf-Schaltfläche hat keine Zeile mehr (ledger-d9z3n unter 1024 px, ledger-cnaz3
    // ab 1024 px): geprüft wird per Wisch, mit G, im Detail oder in der Auswahl.
    Html.div [
        prop.key (string entry.Id)
        prop.children [
            Html.div [
                match mouse with
                | Some info ->
                    prop.className ("group " + mouseRowContainerClassName info entry.NeedsReview)
                    if info.IsFocused then
                        prop.custom ("aria-selected", "true")
                        prop.custom ("data-keyboard-focus", "true")
                | None -> prop.className $"flex items-stretch {Tokens.Backgrounds.dark}"
                prop.children [
                    // Mit Maus steht links immer das Kästchen (ohne Hover sichtbar), wie in der
                    // Tabelle: Klick wählt, mit Umschalt die Spanne; es entsteht kein Auswahl-Modus.
                    match mouse with
                    | Some info ->
                        Html.div [
                            prop.className "shrink-0 flex items-center pl-5 pr-3"
                            prop.children [
                                MultiSelect.boxButton
                                    (if info.Selected then MultiSelect.Checked else MultiSelect.Unchecked)
                                    $"{payeeLabel entry} auswählen"
                                    (fun shift -> dispatch (ClickRowCheckbox (entry.Id, shift)))
                            ]
                        ]
                    | None -> ()
                    // Im Auswahl-Modus steht links das Kästchen und die Zeile schaltet die
                    // Auswahl statt das Detail zu öffnen (ledger-r1d2f).
                    match selected with
                    | Some isOn ->
                        // Das Kästchen ist rein dargestellt (`aria-hidden`); der Container um es
                        // trägt den Tipp, mindestens 44 px breit, und schaltet wie die Zeile
                        // daneben (ledger-w4vte).
                        Html.div [
                            prop.onClick (fun _ -> dispatch (ToggleRowSelected entry.Id))
                            prop.className (
                                "shrink-0 flex items-center min-w-11 pl-4 pr-3 border-b " +
                                $"{Tokens.Borders.subtle} " +
                                (if isOn then Tokens.ListRoles.selectedFill else Tokens.Backgrounds.dark)
                            )
                            prop.children [ MultiSelect.box (if isOn then MultiSelect.Checked else MultiSelect.Unchecked) true ]
                        ]
                    | None -> ()
                    entryRow
                        (fun e ->
                            match selected, mouse with
                            | Some _, _ -> dispatch (ToggleRowSelected entry.Id)
                            // Mit Maus wie die Tabellenzeile: Strg/Cmd und Umschalt wählen (§7.7, ledger-cnaz3).
                            | None, Some _ ->
                                dispatch (rowClickMsg entry { CtrlOrMeta = e.ctrlKey || e.metaKey; Shift = e.shiftKey })
                            | None, None -> dispatch (OpenDetail entry))
                        selected
                        mouse
                        accountNameOf
                        flatDate
                        entry
                ]
            ]
            match reviewMarkHintFor rowState entry with
            | Some hint ->
                Html.div [
                    prop.className $"px-4 py-1.5 {Tokens.Backgrounds.dark} border-b {Tokens.Borders.subtle}"
                    prop.children [ reviewMarkHintView dispatch rowState entry.Id hint ]
                ]
            | None -> ()
        ]
    ]

/// Der Gruppenkopf eines Datums in der Handy-Liste (AC6): Datum links, Tagessumme
/// rechts (ADR 0049 Punkt 8), bündig mit den Beträgen der Zeilen darunter.
let private dateGroupHeader (date: DateTime) (sum: Money) =
    Html.div [
        prop.key ("group-" + date.ToString("yyyy-MM-dd"))
        prop.className "flex items-center justify-between px-4 pt-4 pb-1.5"
        prop.children [
            Html.span [ prop.className Tokens.ListRoles.groupHeader; prop.text (formatDate date) ]
            Html.span [ prop.className Tokens.ListRoles.date; prop.text (daySumText sum) ]
        ]
    ]

/// Eine Zelle der PC-Tabelle, deren Text auf eine Zeile gekürzt wird (Payee,
/// Kategorie, Memo): die Tabelle hat feste Spaltenbreiten (`FixedLayout`), ohne
/// Kürzung würde langer Text umbrechen und die Zeile höher machen als jede andere. `title` trägt den ganzen Text.
let private truncatedRoleCell (role: Table.CellRole) (title: string) (children: ReactElement list) =
    Table.roleCell role [ Html.div (Truncate.props "" title @ [ prop.children children ]) ]

/// Dieselbe Zelle mit reinem Text, `title` und Text sind derselbe Wert.
let private truncatedTextRoleCell (role: Table.CellRole) (text: string) =
    Table.roleCell role [ Html.div (Truncate.textProps "" text) ]

/// Die Kategorie-Zelle der PC-Tabelle (ADR 0049 Punkt 4): sekundäre Rolle, nur "ohne
/// Kategorie" in Gelb. Der `span` mit eigener Farbklasse gewinnt gegenüber der
/// Zellfarbe, unabhängig von der Reihenfolge im kompilierten CSS (ADR 0031). Reiner Text:
/// die Zellen sind keine eigenen Klickziele, ein Klick ist ein Zeilenklick (ADR 0053
/// Punkt 3); das Filtern liegt als Knopf im Detail.
let private categoryCell (entry: LedgerEntryView) =
    let label = listCategoryLabel TableRow entry
    truncatedRoleCell Table.Secondary label [ Html.span [ prop.className (categoryClassName entry); prop.text label ] ]

/// Der Betrag als eigene Zelle: rechtsbündig über `Table.amountCell` (Listenvariante,
/// ADR 0049); gedämpft und durchgestrichen für eine in YNAB gelöschte Buchung.
let private tdMoney (entry: LedgerEntryView) =
    if entry.DeletedInYnab then
        Table.cell { Table.defaultCellProps with Align = Some "right" } [ entryMoney entry ]
    else
        Table.amountCell entry.Amount.Amount entry.Amount.Currency

/// Klassen einer PC-Zeile mit Auswahl: gewählt trägt blasser Akzent als Grund, der Anker der
/// Spanne zusätzlich die Akzent-Kante links, eine Buchung "zu prüfen" die gelbe Statuskante, beides
/// über `rowEdgeClassName`.
let selectableRowClassName (selected: bool) (isAnchor: bool) (needsReview: bool) : string =
    [ Tokens.ListRoles.rowHeight
      if selected then Tokens.ListRoles.selectedFill
      yield! Option.toList (rowEdgeClassName isAnchor needsReview) ]
    |> String.concat " "

/// Die Kästchen-Zelle einer PC-Zeile: ein Klick schaltet die Auswahl (mit Shift die
/// Spanne), er öffnet nicht die Historie. Der Abstand links in Pixeln wie im Kopf (`Table.calmHeaderContentCell`).
let private selectBoxCell (dispatch: Msg -> unit) (selected: bool) (entry: LedgerEntryView) : ReactElement =
    Html.td [
        prop.className "pl-[20px] pr-0 py-0"
        prop.children [
            MultiSelect.boxButton
                (if selected then MultiSelect.Checked else MultiSelect.Unchecked)
                $"{payeeLabel entry} auswählen"
                (fun shift -> dispatch (ClickRowCheckbox (entry.Id, shift)))
        ]
    ]

/// Die Kopfzelle der Kästchen-Spalte: das Kopfzeilen-Kästchen über den ganzen Treffer.
let private selectHeaderCell (dispatch: Msg -> unit) (state: HeaderCheckState) : ReactElement =
    Table.calmHeaderContentCell [
        MultiSelect.boxButton
            (match state with
             | HeaderEmpty -> MultiSelect.Unchecked
             | HeaderPartial -> MultiSelect.Partial
             | HeaderChecked -> MultiSelect.Checked)
            "Alle Buchungen des Treffers auswählen"
            (fun _ -> dispatch ToggleHeaderCheckbox)
    ]

/// Breite einer Tabellenspalte: feste Pixel oder "Rest" (die letzte flexible Spalte).
type ColumnWidth =
    | FixedPx of int
    | Rest

/// Die Spalten der PC-Tabelle in ihrer Reihenfolge (K13).
type LedgerColumn =
    | SelectColumn
    | DateColumn
    | AccountColumn
    | PayeeColumn
    | CategoryColumn
    | MemoColumn
    | AmountColumn

/// Kopf einer Spalte der PC-Tabelle: Text, Ausrichtung und das Sortierfeld, wenn der Kopf
/// sortiert. Memo sortiert nicht; das Auswahl-Kästchen hat einen eigenen Kopf. Die Spalte Status
/// entfällt seit ledger-cnaz3: ihre Kennzeichen stehen in der Payee-Zelle.
type TableHeader =
    { HeaderText: string
      HeaderAlign: string option
      HeaderSortField: LedgerSortField option }

let tableHeaderFor (column: LedgerColumn) : TableHeader option =
    let header label align field = Some { HeaderText = label; HeaderAlign = align; HeaderSortField = field }
    match column with
    | SelectColumn -> None
    | DateColumn -> header "Datum" None (Some SortByDate)
    | AccountColumn -> header "Konto" None (Some SortByAccount)
    | PayeeColumn -> header "Payee" None (Some SortByPayee)
    | CategoryColumn -> header "Kategorie" None (Some SortByCategory)
    | MemoColumn -> header "Memo" None None
    | AmountColumn -> header "Betrag" (Some "right") (Some SortByAmount)

/// Was die PC-Ansicht bei einer Tabellenbreite zeigt: die Tabelle mit den sichtbaren
/// Spalten samt Breiten, oder die kompakte Liste (dieselbe wie am Handy).
type TableLayout =
    | CompactList
    | Columns of (LedgerColumn * ColumnWidth) list

/// Kürzel des Kontos vor dem Payee: nur in der Gesamtsicht und nur, wenn die Konto-Spalte fehlt.
let payeeAccountAbbreviation (isAllAccounts: bool) (columns: LedgerColumn list) (abbreviation: string) : string option =
    if isAllAccounts && not (List.contains AccountColumn columns) then Some abbreviation else None

/// Ein Kennzeichen der Payee-Zelle (`payeeMarks`).
let private payeeMarkView (mark: PayeeMark) : ReactElement =
    match mark with
    | PayeeFlag flag -> flagStatusBadge flag
    | PayeePossibleDuplicate -> rowAttentionWord rowPossibleDuplicateText
    | PayeeNotInYnab -> rowAttentionWord rowNotInYnabText
    | PayeeDeleted -> deletedStatusIcon ()

/// Die Payee-Zelle der PC-Tabelle. Ist die Konto-Spalte ausgeblendet (schmales Fenster
/// in der Gesamtsicht), steht das Kontokürzel klein vor dem Payee in derselben Zelle,
/// als eigenes Element neben dem Payee-Text (ledger-qh9qx). Reiner Text, kein eigenes
/// Klickziel (ADR 0053 Punkt 3).
let private payeeCell (accountAbbreviation: string option) (entry: LedgerEntryView) =
    let abbreviation =
        match accountAbbreviation with
        | Some abbreviation ->
            Html.span [ prop.className $"{Tokens.ListRoles.muted} mr-1.5 shrink-0"; prop.text abbreviation ]
        | None -> Html.none
    let payee = payeeLabel entry
    // Hinter dem Namen die Kennzeichen (ledger-cnaz3); der Name kürzt sich, die Kennzeichen behalten ihre Breite.
    Table.roleCell Table.Payee [
        Html.div [
            prop.className "flex min-w-0 items-center gap-2"
            prop.children [
                abbreviation
                Truncate.text (if payeeMissing entry then $"min-w-0 {Tokens.Colors.textMuted}" else "min-w-0") payee
                // Die gelbe Kante sagt „zu prüfen“ nur dem Auge (§2 Ruhige Listen).
                if entry.NeedsReview then Html.span [ prop.className "sr-only"; prop.text "zu prüfen" ]
                for mark in payeeMarks entry do
                    keepWidth (payeeMarkView mark)
            ]
        ]
    ]

let private entryTableRow
    (dispatch: Msg -> unit)
    (columns: LedgerColumn list)
    (isAllAccounts: bool)
    (accountAbbreviationOf: Guid -> string)
    (accountNameOf: Guid -> string)
    (selected: bool)
    (isAnchor: bool)
    (isFocused: bool)
    (isFilteredOut: bool)
    (entry: LedgerEntryView)
    =
    // "Zu prüfen" ist die Statuskante (ADR 0049 Punkt 7), sichtbar ohne Wort, für Screenreader mit `sr-only`. Die Kante
    // setzt `selectableRowClassName` zusammen mit dem Anker, nicht `StatusEdge`: zwei Schatten überschrieben sich.
    // `Table.trClickable` kennt weder Kante noch Zeilenhöhe, deshalb direkt `Table.row`. Eine Prüf-Schaltfläche hat
    // die Zeile nicht mehr, auch nicht bei Hover oder Fokus (Roman, 2026-11-14).
    Table.row
        { Table.defaultRowProps with
            OnClickWithKeys = Some (fun keys -> dispatch (rowClickMsg entry keys))
            IsFocused = isFocused
            // Ausgegraut in `text-muted` bei voller Deckkraft (`.filtered-out-text`, ledger-dn4vy).
            ClassName =
                Some (
                    "group " + selectableRowClassName selected isAnchor entry.NeedsReview
                    + (if isFilteredOut then " filtered-out-text" else "")
                ) }
        [
            selectBoxCell dispatch selected entry
            Table.roleCell Table.DateCell [ Html.text (formatDate entry.Date) ]
            if List.contains AccountColumn columns then
                truncatedTextRoleCell Table.Muted (accountNameOf entry.AccountId)
            payeeCell (payeeAccountAbbreviation isAllAccounts columns (accountAbbreviationOf entry.AccountId)) entry
            categoryCell entry
            if List.contains MemoColumn columns then
                truncatedTextRoleCell Table.Muted (entry.Memo |> Option.defaultValue "")
            tdMoney entry
        ]

/// Die Zeile unter einer Buchung mit dem Hinweis "Prüfvermerk nicht in YNAB" (PC): eine
/// Zeile über die ganze Tabellenbreite, nur wenn ein Hinweis ansteht.
let private entryHintRow
    (dispatch: Msg -> unit)
    (columnCount: int)
    (rowState: ReviewMarkRowState option)
    (entry: LedgerEntryView)
    : ReactElement option =
    reviewMarkHintFor rowState entry
    |> Option.map (fun hint ->
        Html.tr [
            prop.key ("hint-" + string entry.Id)
            prop.children [
                Html.td [
                    prop.colSpan columnCount
                    prop.className $"{Tokens.ListRoles.cellPaddingX} pb-2 pt-0"
                    prop.children [ reviewMarkHintView dispatch rowState entry.Id hint ]
                ]
            ]
        ])

/// Die Zeile unter einer ausgegrauten Buchung (PC): der Vermerk, dass sie nicht mehr zum
/// Filter passt.
let private filteredOutNoteRow (columnCount: int) (entry: LedgerEntryView) : ReactElement =
    Html.tr [
        prop.key ("filtered-" + string entry.Id)
        prop.custom ("data-testid", "filtered-out-note")
        prop.children [
            Html.td [
                prop.colSpan columnCount
                prop.className $"{Tokens.ListRoles.cellPaddingX} pb-2 pt-0 {Tokens.ListRoles.muted}"
                prop.text filteredOutNote
            ]
        ]
    ]

/// Gruppenkopf eines Datums in der PC-Tabelle: Datum links über den ersten Spalten,
/// Tagessumme in der Betragsspalte (`Table.calmGroupRow`, ADR 0049 Punkt 8).
let private dateGroupRow (columnsBeforeAmount: int) (date: DateTime) (sum: Money) =
    Table.calmGroupRow
        ("group-" + date.ToString("yyyy-MM-dd"))
        columnsBeforeAmount
        (formatDate date)
        (daySumText sum)

/// Was an der Stelle des Betrags steht (ledger-vq2g8): der Stand, ein Platzhalter beim Laden
/// oder ein Strich nach einem Fehler.
let private cardBalanceElement (isActive: bool) (balance: CardBalance) : ReactElement =
    match balance with
    | BalanceShown money -> Money.accountBalanceFor isActive money.Amount money.Currency
    | BalancePending -> Loading.amountSkeleton ()
    | BalanceUnavailable -> Money.balanceUnavailable ()

/// „Erneut laden“ bei gescheiterten Kontoständen, über der Liste auf allen Breiten (ledger-d9z3n). Der Aufrufer legt
/// den Rahmen fest.
let private balancesNoticeView
    (dispatch: Msg -> unit)
    (balances: RemoteData<LedgerAccountBalanceView list>)
    (refreshError: string option)
    =
    balancesNotice balances refreshError
    |> Option.map (fun n -> ErrorDisplay.cardCompactWith n.RetryLabel n.Text (Some (fun () -> dispatch n.Retry)))

/// Der Untertitel der Seite nach einem Spiegel-Lauf, kurz (`ledger-n4vte`):
/// "Gespiegelt heute 22:24". Ein gescheiterter Lauf steht genauso sichtbar da wie
/// ein Erfolg (ADR 0014). Reine Funktion mit explizitem `today`, damit sie ohne
/// Systemzeit testbar ist.
let mirrorRunSubtitle (today: DateTime) (run: MirrorRunStatus) : string =
    let local = run.RanAt.ToLocalTime()
    let day =
        match (local.Date - today.Date).Days with
        | 0 -> "heute"
        | -1 -> "gestern"
        | _ -> local.ToString("dd.MM.", germanCulture ())
    let time = local.ToString("HH:mm", germanCulture ())

    match run.Outcome with
    | MirrorRunSucceeded _ -> $"Gespiegelt {day} {time}"
    | MirrorRunFailed message -> $"Spiegeln gescheitert {day} {time}: {message}"

/// Der Status des letzten Spiegel-Laufs als Unterzeile der Seite.
let private lastRunSubtitle (status: RemoteData<MirrorRunStatus option>) : string option =
    match status with
    | Success (Some run) -> Some (mirrorRunSubtitle DateTime.Today run)
    | Success None -> Some "Noch kein Spiegel-Lauf."
    | Failure message -> Some $"Lauf-Status nicht lesbar: {message}"
    | Loading | NotAsked -> None

// ============================================
// Detail einer Buchung (ledger-q1hw2, ADR 0053)
// ============================================
// Ein Detail je Buchung: Seitenpanel rechts am PC, BottomSheet (ADR 0005) am Handy, in der
// Kompaktliste und wo neben dem Panel keine Tabellenstufe mehr passt. Der Inhalt ist für
// beide Formen derselbe (`detailContent`); die Bausteine stehen in `Client.DesignSystem.Detail`.

/// Die Anzeige-Reihenfolge der Historie (ledger-h9tq2, Entscheidung Roman
/// 2026-09-18): die neueste Zeile steht oben. `getEntryHistory` liefert aufsteigend
/// (älteste zuerst, ADR 0013), die Drehung sitzt bewusst nur hier in der Anzeige,
/// nicht in der Abfrage oder im Lesepfad. Die laufende Nummer je Zeile bleibt die
/// aus dem Ereignis-Log und zählt dadurch in der Liste von oben nach unten abwärts.
let historyLinesForDisplay (lines: LedgerHistoryLine list) : LedgerHistoryLine list = List.rev lines

/// Das Kürzel der Herkunft in einer Zeile der Historie (§7.11, ledger-zfj78): wer den Eintrag schrieb, neutral und
/// kurz. In BB entschieden und über Quick Add erfasst steht als BB, weil auch Lena in BB bucht (Roman, 2026-11-15);
/// der Order-ID-Abgleich steht als ABGLEICH.
let historyWhoTag (origin: LedgerOrigin) : string =
    match origin with
    | DecidedInBb
    | AddedViaQuickAdd -> "BB"
    | AddedByBob -> "BOB"
    | MirroredFromYnab -> "YNAB"
    | DecidedByRule -> "REGEL"
    | ImportedFromComdirect -> "IMPORT"
    | MatchedByOrderId -> "ABGLEICH"

/// Der Zeitpunkt einer Zeile der Historie als `TT.MM. HH:MM` in Ortszeit (§7.11).
let historyWhenText (occurredAt: DateTime) : string =
    occurredAt.ToLocalTime().ToString("dd.MM. HH:mm", germanCulture ())

/// Der Betrag rechts in einer Zeile der Historie (§7.11): der Betrag des Eintrags mit Vorzeichen und Währung wie die
/// Split-Zeilen, ohne Betrag leer (`LedgerHistoryLine.Amount`, ledger-zfj78).
let historyAmountText (line: LedgerHistoryLine) : string option =
    line.Amount |> Option.map (fun m -> Money.formatAmountText false true m.Amount m.Currency)

/// Die Einträge der Historie nach §7.11, neueste oben, mit dem Betrag des Eintrags rechts, falls er einen trägt.
let historyItems (lines: LedgerHistoryLine list) : HistoryList.Item list =
    lines
    |> historyLinesForDisplay
    |> List.map (fun line ->
        { Key = string line.Sequence
          When = historyWhenText line.OccurredAt
          Who = historyWhoTag line.Origin
          Text = line.Description
          Amount = historyAmountText line })

/// Die Beschriftung der aufklappbaren Zeile: „Historie (n)“, solange die Zahl noch nicht feststeht nur „Historie“.
let historyToggleLabel (history: RemoteData<LedgerHistoryLine list>) : string =
    match history with
    | Success lines -> $"Historie ({List.length lines})"
    | _ -> "Historie"

/// Ist die Historie dieser Buchung im Detail aufgeklappt? Beim Öffnen des Details ist sie zu (ledger-zfj78).
let historyOpen (model: Model) (entry: LedgerEntryView) : bool =
    model.HistoryOpenFor = Some entry.Id

/// Die Beschriftung des Filter-Knopfs am Payee (ADR 0053): der volle Name, keine Kürzung.
/// `None` unter denselben Bedingungen wie `payeeMenuLabel`: kein Payee zum Filtern.
let payeeFilterLabel (entry: LedgerEntryView) : string option =
    payeeMenuLabel entry
    |> Option.bind (fun _ -> entry.Payee)
    |> Option.map (fun payee -> $"Nur Buchungen von {payee.Trim()} zeigen")

/// Die Beschriftung des Filter-Knopfs an der Kategorie: `None` bei Split und bei einer
/// Buchung ohne Kategorie (`categoryMenuLabel`, auch Übertrag ohne Kategorie).
let categoryFilterLabel (entry: LedgerEntryView) : string option =
    categoryMenuLabel entry
    |> Option.map (fun _ -> $"Nur Buchungen in {categoryLabelQualified entry} zeigen")

/// Die Hinweisleiste des Details, höchstens eine, in dieser Rangfolge: in YNAB gelöscht,
/// noch nicht in YNAB, Prüfvermerk nicht in YNAB.
type DetailNotice =
    /// Rot, ohne Aktion. Der Satz trägt den Löschzeitpunkt aus der Historie, wenn sie da ist.
    | DeletedNotice of reason: string
    /// Gelb, ohne Aktion: die Buchung wartet als unsicheres Duplikat (ADR 0093 Punkt 3).
    | PossibleDuplicateNotice
    /// Gelb, mit "Erneut senden" (sofortiger Push): der Push nach YNAB steht aus oder scheiterte.
    | NotInYnabNotice of reason: string
    /// Gelb, mit "Erneut senden", wenn etwas zu senden ist.
    | ReviewMarkNotice of ReviewMarkHint
    /// Gelb, mit "Erneut senden": die Kategorie ist in BB gesetzt, aber nicht in YNAB
    /// (ADR 0055, Punkt 4).
    | CategoryNotice of CategoryHint
    /// Gelb, mit "Erneut senden": das Flag ist in BB gesetzt, aber nicht in YNAB (ADR 0056,
    /// Punkt 5). `busy`: der Aufruf läuft.
    | FlagNotice of FlagHint * busy: bool
    /// Gelb, mit "Erneut senden": das Memo ist in BB gesetzt, aber nicht in YNAB (ADR 0062).
    /// `busy`: der Aufruf läuft.
    | MemoNotice of MemoHint * busy: bool
    /// Gelb, mit "Erneut senden": der Betrag ist in BB geändert, aber nicht in YNAB (ADR 0092).
    /// `busy`: der Aufruf läuft.
    | AmountNotice of AmountHint * busy: bool
    /// Gelb, mit "Erneut senden": der Payee ist in BB geändert, aber nicht in YNAB (ledger-fxt09).
    /// `busy`: der Aufruf läuft.
    | PayeeNotice of PayeeHint * busy: bool

/// Die gelbe Leiste einer wartenden Buchung (Abweichung (e) zum Prototyp Version 17).
let possibleDuplicateNoticeBadge = "Vielleicht doppelt."
let possibleDuplicateNoticeReason = "Geht erst nach deiner Entscheidung nach YNAB."

/// Der Satz der roten Leiste. Den Zeitpunkt liefert die jüngste Historie-Zeile "In YNAB
/// gelöscht"; ohne geladene Historie steht der Satz ohne Zeit da.
let deletedNoticeReason (history: RemoteData<LedgerHistoryLine list>) : string =
    let deletedAt =
        match history with
        | Success lines ->
            lines
            |> List.filter (fun l -> l.Description.StartsWith("In YNAB gelöscht", StringComparison.Ordinal))
            |> List.tryLast
            |> Option.map (fun l -> l.OccurredAt)
        | _ -> None
    match deletedAt with
    | Some at -> $"Gelöscht in YNAB am {formatDateTime at}. Zählt nicht zum Kontostand."
    | None -> "Gelöscht in YNAB. Zählt nicht zum Kontostand."

/// Der Satz der orangen Leiste "Noch nicht in YNAB": mit gescheitertem Push Zeitpunkt und
/// Grund, sonst, dass noch nichts gesendet wurde. Der Spiegel-Lauf pusht nichts (er liest
/// nur aus YNAB), deshalb verspricht der Satz keinen Versuch beim nächsten Lauf (ledger-cks06).
let notInYnabNoticeReason (failure: LedgerPushFailure option) : string =
    match failure with
    | Some f ->
        let reason = f.Reason.Trim().TrimEnd('.')
        $"Push gescheitert am {formatDateTime f.FailedAt}: {reason}. Erneut senden schickt die Buchung sofort."
    | None -> "Noch nicht an YNAB gesendet."

let detailNoticeFor
    (rowState: ReviewMarkRowState option)
    (history: RemoteData<LedgerHistoryLine list>)
    (entry: LedgerEntryView)
    : DetailNotice option =
    if entry.DeletedInYnab then
        Some (DeletedNotice (deletedNoticeReason history))
    elif entry.PossibleDuplicate.IsSome then
        Some PossibleDuplicateNotice
    elif not entry.LinkedToYnab then
        Some (NotInYnabNotice (notInYnabNoticeReason entry.PushToYnabFailure))
    else
        reviewMarkHintFor rowState entry |> Option.map ReviewMarkNotice

/// Wie `detailNoticeFor`, mit der Kategorie: gelöscht, noch nicht in YNAB und der
/// Prüfvermerk haben Vorrang, danach "Kategorie nicht in YNAB" (höchstens eine Leiste).
let detailNoticeWithCategory
    (rowState: ReviewMarkRowState option)
    (categoryRow: CategoryRowState option)
    (history: RemoteData<LedgerHistoryLine list>)
    (entry: LedgerEntryView)
    : DetailNotice option =
    match detailNoticeFor rowState history entry with
    | Some notice -> Some notice
    | None -> categoryHintFor categoryRow entry |> Option.map CategoryNotice

/// Wie `detailNoticeWithCategory`, mit dem Flag: alle bisherigen Hinweise haben Vorrang, danach
/// "Flag nicht in YNAB" (höchstens eine Leiste).
let detailNoticeWithFlag
    (rowState: ReviewMarkRowState option)
    (categoryRow: CategoryRowState option)
    (flagRow: FlagRowState option)
    (history: RemoteData<LedgerHistoryLine list>)
    (entry: LedgerEntryView)
    : DetailNotice option =
    match detailNoticeWithCategory rowState categoryRow history entry with
    | Some notice -> Some notice
    | None -> flagHintFor flagRow entry |> Option.map (fun hint -> FlagNotice (hint, (flagRow = Some FlagSending)))

/// Wie `detailNoticeWithFlag`, mit dem Memo: alle bisherigen Hinweise haben Vorrang, danach
/// "Memo nicht in YNAB" (höchstens eine Leiste, ADR 0062).
let detailNoticeWithMemo
    (rowState: ReviewMarkRowState option)
    (categoryRow: CategoryRowState option)
    (flagRow: FlagRowState option)
    (memoRow: MemoRowState option)
    (history: RemoteData<LedgerHistoryLine list>)
    (entry: LedgerEntryView)
    : DetailNotice option =
    match detailNoticeWithFlag rowState categoryRow flagRow history entry with
    | Some notice -> Some notice
    | None -> memoHintFor memoRow entry |> Option.map (fun hint -> MemoNotice (hint, (memoRow = Some MemoSending)))

/// Wie `detailNoticeWithMemo`, mit dem Betrag: alle bisherigen Hinweise haben Vorrang, danach
/// "Betrag nicht in YNAB" (höchstens eine Leiste, ADR 0092).
let detailNoticeWithAmount
    (rowState: ReviewMarkRowState option)
    (categoryRow: CategoryRowState option)
    (flagRow: FlagRowState option)
    (memoRow: MemoRowState option)
    (amountRow: AmountRowState option)
    (history: RemoteData<LedgerHistoryLine list>)
    (entry: LedgerEntryView)
    : DetailNotice option =
    match detailNoticeWithMemo rowState categoryRow flagRow memoRow history entry with
    | Some notice -> Some notice
    | None -> amountHintFor amountRow entry |> Option.map (fun hint -> AmountNotice (hint, (amountRow = Some AmountSending)))

/// Wie `detailNoticeWithAmount`, mit dem Payee: alle bisherigen Hinweise haben Vorrang, danach
/// "Payee nicht in YNAB" (höchstens eine Leiste, je Feld ein eigener Hinweis).
let detailNoticeWithPayee
    (rowState: ReviewMarkRowState option)
    (categoryRow: CategoryRowState option)
    (flagRow: FlagRowState option)
    (memoRow: MemoRowState option)
    (amountRow: AmountRowState option)
    (payeeRow: PayeeRowState option)
    (history: RemoteData<LedgerHistoryLine list>)
    (entry: LedgerEntryView)
    : DetailNotice option =
    match detailNoticeWithAmount rowState categoryRow flagRow memoRow amountRow history entry with
    | Some notice -> Some notice
    | None -> payeeHintFor payeeRow entry |> Option.map (fun hint -> PayeeNotice (hint, (payeeRow = Some PayeeSending)))

/// Die Nachricht der Aktion in der Hinweisleiste: die Prüfvermerk-Leiste ("Erneut senden",
/// `retryReviewMarkWriteBack` aus ledger-r777c) und die Kategorie-Leiste
/// (`retryCategorizationWriteBack`) haben eine, ebenso "Noch nicht in YNAB"
/// (`retryPushToYnab`, ledger-cks06); gelöscht nicht.
let detailNoticeActionMsg (entryId: Guid) (notice: DetailNotice) : Msg option =
    match notice with
    | ReviewMarkNotice hint when hint.RetryLabel.IsSome -> Some (StartRetryReviewMark entryId)
    | CategoryNotice hint when hint.RetryLabel.IsSome -> Some (StartRetryCategorization entryId)
    | FlagNotice (hint, _) when hint.RetryLabel.IsSome -> Some (StartRetryFlag entryId)
    | MemoNotice (hint, _) when hint.RetryLabel.IsSome -> Some (StartRetryMemo entryId)
    | AmountNotice (hint, _) when hint.RetryLabel.IsSome -> Some (StartRetryAmount entryId)
    | PayeeNotice (hint, _) when hint.RetryLabel.IsSome -> Some (StartRetryPayee entryId)
    | NotInYnabNotice _ -> Some (StartRetryPush entryId)
    | _ -> None

/// Die Nachricht des Prüf-Knopfs im Detail: an setzt `Reviewed`, aus `Unreviewed`, über denselben
/// Weg wie die Schaltfläche an der Zeile (`StartSetReviewMark`).
let reviewedSwitchMsg (entryId: Guid) (on: bool) : Msg =
    StartSetReviewMark (entryId, (if on then Reviewed else Unreviewed))

/// Der Prüf-Knopf im Detail (ledger-zfj78, Prototyp Version 17): ersetzt den Schalter „Geprüft“.
type DetailReviewButton = {
    Label: string
    /// Hauptaktion (gefüllt im Akzent) bei einer ungeprüften Buchung, sonst neutral.
    Primary: bool
    /// Was ein Tipp setzt: dieselbe Nachricht wie zuvor der Schalter (`reviewedSwitchMsg`).
    Msg: Msg
}

/// Text, Art und Wirkung des Prüf-Knopfs je Stand: ungeprüft „Geprüft“ als Hauptaktion, geprüft „Als ungeprüft
/// markieren“ neutral.
let detailReviewButtonFor (entry: LedgerEntryView) : DetailReviewButton =
    if entry.NeedsReview then
        { Label = "Geprüft"; Primary = true; Msg = reviewedSwitchMsg entry.Id true }
    else
        { Label = "Als ungeprüft markieren"; Primary = false; Msg = reviewedSwitchMsg entry.Id false }

let private noticeView
    (dispatch: Msg -> unit)
    (rowState: ReviewMarkRowState option)
    (categoryRow: CategoryRowState option)
    (pushRow: PushRetryRowState option)
    (entry: LedgerEntryView)
    (notice: DetailNotice)
    : ReactElement =
    // Gelbe Leiste mit "Erneut senden", der Laufzustand kommt mit dem Hinweis (Flag, Memo, Betrag, Payee).
    let retryNotice (title: string) (reason: string) (retryLabel: string option) (busy: bool) =
        Detail.notice
            { Tone = Detail.NoticeYellow
              Badge = title
              Reason = reason
              Action =
                match retryLabel, detailNoticeActionMsg entry.Id notice with
                | Some label, Some msg -> Some { Label = label; Busy = busy; Disabled = false; OnClick = fun () -> dispatch msg }
                | _ -> None }
    match notice with
    | DeletedNotice reason ->
        Detail.notice { Tone = Detail.NoticeRed; Badge = "In YNAB gelöscht"; Reason = reason; Action = None }
    | PossibleDuplicateNotice ->
        Detail.notice
            { Tone = Detail.NoticeYellow
              Badge = possibleDuplicateNoticeBadge
              Reason = possibleDuplicateNoticeReason
              Action = None }
    | NotInYnabNotice reason ->
        Detail.notice
            { Tone = Detail.NoticeYellow
              Badge = "Noch nicht in YNAB"
              Reason = reason
              Action =
                detailNoticeActionMsg entry.Id notice
                |> Option.map (fun msg ->
                    { Label = "Erneut senden"
                      Busy = (pushRow = Some PushSending)
                      Disabled = false
                      OnClick = fun () -> dispatch msg }) }
    | ReviewMarkNotice hint ->
        Detail.notice
            { Tone = Detail.NoticeYellow
              Badge = hint.Title
              Reason = hint.Reason
              Action =
                match hint.RetryLabel, detailNoticeActionMsg entry.Id notice with
                | Some label, Some msg ->
                    Some
                        { Label = label
                          Busy = (rowState = Some ReviewMarkSending)
                          Disabled = false
                          OnClick = fun () -> dispatch msg }
                | _ -> None }
    | CategoryNotice hint ->
        Detail.notice
            { Tone = Detail.NoticeYellow
              Badge = hint.Title
              Reason = hint.Reason
              Action =
                match hint.RetryLabel, detailNoticeActionMsg entry.Id notice with
                | Some label, Some msg ->
                    Some
                        { Label = label
                          Busy = (categoryRow = Some CategorySending)
                          Disabled = false
                          OnClick = fun () -> dispatch msg }
                | _ -> None }
    | FlagNotice (hint, busy) -> retryNotice hint.Title hint.Reason hint.RetryLabel busy
    | MemoNotice (hint, busy) -> retryNotice hint.Title hint.Reason hint.RetryLabel busy
    | AmountNotice (hint, busy) -> retryNotice hint.Title hint.Reason hint.RetryLabel busy
    | PayeeNotice (hint, busy) -> retryNotice hint.Title hint.Reason hint.RetryLabel busy

/// Die Beschriftung einer Split-Zeile: Kategorie, Übertrag oder offen.
///
/// Eine `ToCategory`-Zeile erscheint voll qualifiziert ("Gruppe: Name", ADR 0045), wenn
/// `groupNames` (`LedgerEntryView.SplitCategoryGroupNames`) ihre Kategorie-Id kennt.
let splitTargetLabel (groupNames: Map<Guid, string>) (split: TransactionSplit) : string =
    match split.Target with
    | ToCategory (YnabCategoryId id, name) -> qualify (Map.tryFind id groupNames) name
    | ToTransfer (_, accountName) -> $"Übertrag: {accountName}"
    | Uncategorized -> "kein Ziel"

/// Die Zeilen der Split-Feldzeile: Ziel und Betrag.
let splitLineTexts (entry: LedgerEntryView) : (string * string) list =
    entry.Splits
    |> List.map (fun split ->
        splitTargetLabel entry.SplitCategoryGroupNames split, Money.formatAmountText false true split.Amount.Amount split.Amount.Currency)

let private accountNameOf (model: Model) (entry: LedgerEntryView) : string =
    match model.Accounts with
    | Success view ->
        view.Accounts
        |> List.tryFind (fun a -> a.Id = entry.AccountId)
        |> Option.map (fun a -> a.Name)
        |> Option.defaultValue "Konto"
    | _ -> "Konto"

/// Der Wochentag kurz wie im Prototyp (`toLocaleDateString('de-DE', {weekday:'short'})`): „Mo.“ bis „So.“.
let private weekdayShort (date: DateTime) : string =
    match date.DayOfWeek with
    | DayOfWeek.Monday -> "Mo."
    | DayOfWeek.Tuesday -> "Di."
    | DayOfWeek.Wednesday -> "Mi."
    | DayOfWeek.Thursday -> "Do."
    | DayOfWeek.Friday -> "Fr."
    | DayOfWeek.Saturday -> "Sa."
    | _ -> "So."

/// Der Tag im Kopf des Details nach `dLabel` (Prototyp Version 17, Abweichung (c), ledger-zfj78): „Heute“, „Gestern“,
/// sonst Wochentag kurz und `TT.MM.`, etwa „Fr., 06.11.“; aus einem anderen Jahr als `today` mit Jahr, „Fr., 06.11.2025“.
/// `today` ist der Kalendertag in Europe/Berlin (`Client.BerlinDate.today`).
let detailDayText (today: DateTime) (date: DateTime) : string =
    match (date.Date - today.Date).Days with
    | 0 -> "Heute"
    | -1 -> "Gestern"
    | _ ->
        let dayMonth = date.ToString("dd.MM.", germanCulture ())
        let year = if date.Year = today.Year then "" else string date.Year
        weekdayShort date + ", " + dayMonth + year

/// Die Zeile unter dem Payee im Kopf: „<Tag> · <Konto>“ (ledger-zfj78).
let detailSubtitleFor (today: DateTime) (accountName: string) (entry: LedgerEntryView) : string =
    $"{detailDayText today entry.Date} · {accountName}"

/// Der Inhalt unter „Historie (n)“: Laden, Fehler mit „Erneut versuchen“, Leerzustand oder die Zeilen nach §7.11.
let private historyView (dispatch: Msg -> unit) (entry: LedgerEntryView) (history: RemoteData<LedgerHistoryLine list>) =
    match history with
    | NotAsked -> Html.none
    | Loading -> Loading.centered "Historie wird geladen…"
    | Failure message ->
        ErrorDisplay.cardCompact
            $"Historie konnte nicht geladen werden: {message}"
            (Some (fun () -> dispatch (LoadHistory entry.Id)))
    | Success [] -> HistoryList.empty "Noch keine Einträge"
    | Success lines -> HistoryList.list "detail-history-list" (historyItems lines)

/// Eine in YNAB gelöschte Buchung ist im Detail ein Lesezustand (ledger-q1hw2): gedämpfter
/// Betrag und Text, kein Prüf-Knopf; „alle zeigen“ bleibt.
let detailReadOnly (entry: LedgerEntryView) : bool = entry.DeletedInYnab

/// Zeigt das Detail den Prüf-Knopf? Nicht bei einer in YNAB gelöschten Buchung (sie nimmt keinen
/// Prüfvermerk an) und nicht bei einer wartenden (ADR 0093 Punkt 6).
let detailReviewSwitchVisible (entry: LedgerEntryView) : bool =
    not (detailReadOnly entry) && (heldLockReason entry).IsNone

/// Bietet das Detail "Buchung löschen" an (ledger-z6sya, ADR 0092 Punkt 1)? Nicht bei
/// abgeglichenen, schon gelöschten und Noch nicht in YNAB verknüpften Buchungen (ADR 0091 Punkt 3).
let detailDeleteVisible (entry: LedgerEntryView) : bool =
    not entry.DeletedInYnab && not entry.Reconciled && entry.LinkedToYnab

/// Der Grund, aus dem „Buchung löschen“ für diese Buchung scheiterte, oder `None` (ledger-k68sd).
let deleteFailureFor (model: Model) (entry: LedgerEntryView) : string option =
    model.DeleteFailure |> Option.filter (fun (id, _) -> id = entry.Id) |> Option.map snd

/// Der Satz unter „Buchung löschen“ nach einem Fehler.
let deleteFailureText (reason: string) : string = $"Nicht gelöscht: {reason}"

/// Die Nachricht des Betrags-Knopfs im Kopf, oder `None`, wenn er nichts öffnen darf
/// (`State.amountEditOpenable`, ledger-m7mye, ADR 0092).
/// Die Nachricht eines Tipps auf die Payee-Zeile: öffnet die Payee-Auswahl, wenn der Payee sich
/// ändern lässt und gerade kein Aufruf läuft, sonst nichts (ledger-fxt09).
let payeeEditMsg (model: Model) (entry: LedgerEntryView) : Msg option =
    if Components.Ledger.State.payeeEditOpenable model entry then Some (OpenPayeeEdit entry.Id) else None

let amountEditMsg (model: Model) (entry: LedgerEntryView) : Msg option =
    if Components.Ledger.State.amountEditOpenable model entry then Some (OpenAmountEdit entry.Id)
    else None

/// Der Kopf des Details im Panel: Payee als Titel (oder „ohne Payee“ gedämpft), darunter „<Tag> · <Konto>“,
/// rechts Schließen (ledger-zfj78). Im Sheet stellt `BottomSheet.view` Titel und Unterzeile.
let private detailTitleHead (today: DateTime) (model: Model) (entry: LedgerEntryView) (onClose: (unit -> unit) option) =
    Detail.titleHead (payeeLabel entry) (payeeMissing entry) (detailSubtitleFor today (accountNameOf model entry) entry) onClose

/// Die Betragszeile: der Betrag als Knopf, der das Sheet "Betrag" öffnet; gesperrt steht er ohne Pfeil, mit dem
/// Grund darunter (ADR 0092). Rechts der Prüfstand, nicht bei einer in YNAB gelöschten Buchung: sie ist ein
/// Lesezustand ohne Prüf-Knopf, ein „zu prüfen“ daneben verspräche eine Aktion, die es nicht gibt.
/// Der Ton der Bestätigung am Feld im Design-System.
let private noteTone (tone: FeedbackTone) : Feedback.Tone =
    match tone with
    | FeedbackOk -> Feedback.Tone.Ok
    | FeedbackWarning -> Feedback.Tone.Warning
    | FeedbackError -> Feedback.Tone.Error
    | FeedbackRunning -> Feedback.Tone.Running
    | FeedbackInfo -> Feedback.Tone.Info

/// Die Bestätigung unter einem Feld des Details (ledger-p5y9s, R3 Platz 1): „✓ Geändert · Rückgängig“, gelb bei
/// einer YNAB-Warnung, rot bei einem Fehler. „Rückgängig“ schickt dieselbe Nachricht wie früher der Toast.
let private noteFieldName (field: DetailNoteField) : string =
    match field with
    | AtCategory -> "category"
    | AtSplits -> "splits"
    | AtAmount -> "amount"
    | AtMemo -> "memo"
    | AtPayee -> "payee"
    | AtLena -> "lena"

/// Text und „Rückgängig“ der Bestätigung (`Feedback.fieldNoteWith`), mit `data-testid="detail-note"` und dem Feld.
/// `asRow`: als eigene Zeile unter einem Feld (mit Einzug und Linie), sonst in der Knopfzeile unter dem Knopf.
let private detailNoteLine (dispatch: Msg -> unit) (asRow: bool) (note: DetailNote) : ReactElement =
    let act =
        note.Undo
        |> Option.bind detailNoteUndoMsg
        |> Option.map (fun msg -> ({ Label = undoToastLabel; OnClick = fun () -> dispatch msg } : Feedback.Act))
    Html.div [
        prop.custom ("data-testid", "detail-note")
        prop.custom ("data-note-field", noteFieldName note.Field)
        if asRow then prop.className $"px-4 detail-inset py-1.5 border-b {Tokens.Borders.subtle}"
        prop.children [ Feedback.fieldNoteWith (noteTone note.Tone) note.Text act ]
    ]

let private detailNoteView (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) (field: DetailNoteField) : ReactElement =
    match detailNoteAt model entry.Id field with
    | Some note when note.Text <> "" ->
        detailNoteLine dispatch true note
    | _ -> Html.none

/// Leuchtet das Feld der Bestätigung gerade (`Feedback.flash`)?
let private noteFlashing (model: Model) (entry: LedgerEntryView) (field: DetailNoteField) : bool =
    detailNoteAt model entry.Id field |> Option.exists (fun note -> note.Flashing)

let private detailAmountRow (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) =
    let amountElement =
        match amountLockFor entry with
        | Some reason -> Detail.amountLocked entry.Amount.Amount entry.Amount.Currency (detailReadOnly entry) reason
        | None ->
            Detail.amountButton
                entry.Amount.Amount
                entry.Amount.Currency
                amountEditLabel
                (amountEditMsg model entry |> Option.map (fun msg -> fun () -> dispatch msg))
    React.fragment [
        Html.div [
            prop.className ("rounded-md" + (if noteFlashing model entry AtAmount then " " + Feedback.flash else ""))
            prop.children [
                Detail.amountRow amountElement (if detailReadOnly entry then Html.none else Detail.reviewStatus entry.NeedsReview)
            ]
        ]
        detailNoteView dispatch model entry AtAmount
    ]

/// Der Kopf über dem Split-Editor: der Gesamtbetrag des Editors ohne Knopf. Kommt der Editor aus
/// dem Sheet "Betrag", steht dort schon der neue Betrag (ledger-m7mye).
let private splitEditorHead
    (today: DateTime)
    (model: Model)
    (entry: LedgerEntryView)
    (editor: SplitLogic.SplitEditorState)
    (onClose: (unit -> unit) option)
    =
    Detail.head
        (Detail.amount editor.Total.Amount editor.Total.Currency (detailReadOnly entry))
        (detailSubtitleFor today (accountNameOf model entry) entry)
        onClose

/// Wo der Kategorie-Picker aufgeht: am PC als Popover im Panel, am Handy und in der
/// Kompaktliste als Inhalt des Detail-Sheets, der das Detail ersetzt (ledger-ea70s, §7.15).
type CategoryPickerPlacement =
    | PickerPopover
    | PickerSheet

let categoryPickerPlacement (form: DetailForm) : CategoryPickerPlacement =
    match form with
    | DetailPanel -> PickerPopover
    | DetailSheet -> PickerSheet

/// Die Fußzeile des Pickers: "Stand vor 3 Min", fehlt, solange kein Abruf gelang.
let categoryStandLabel (model: Model) (now: DateTime) : string option =
    model.CategoriesLoadedAt |> Option.map (fun at -> availableAgeLabel at now)

/// Ladezustand des Pickers aus dem Abruf der Kategorien (ledger-dmw83): ohne Liste lädt er
/// (auch vor dem ersten Abruf), nach einem Fehler bietet er "Erneut versuchen". Fehlt die
/// Einrichtung in den Settings, reicht er ihren Satz an den Picker weiter; jeder andere
/// Fehler zeigt den allgemeinen Satz.
let categoryLoadState (categories: RemoteData<YnabCategory list>) : CategoryPicker.PickerLoadState =
    match categories with
    | NotAsked | Loading -> CategoryPicker.PickerLoading
    | Failure reason when isCategoriesSetupGap reason -> CategoryPicker.PickerFailed (Some reason)
    | Failure _ -> CategoryPicker.PickerFailed None
    | Success _ -> CategoryPicker.PickerReady

/// Die Zeilen der Karte "Budget nach dem Wechsel" für diese Buchung; leer, wenn die Karte
/// nicht steht oder zu einer anderen Buchung gehört.
let categoryCardLinesFor (model: Model) (entry: LedgerEntryView) : CategoryPicker.CategoryChangeCard.ChangeLine list =
    match model.CategoryCard with
    | Some card when card.EntryId = entry.Id ->
        card.Lines |> List.map (fun r -> { Label = r.Label; OldText = r.OldText; NewText = r.NewText })
    | _ -> []

/// Die Nachricht des Klicks auf den Pfeil oder die Kategorie-Zeile: öffnet den Picker, ein
/// zweiter Klick schließt ihn. Gesperrte Buchungen tun nichts.
let categoryArrowMsg (model: Model) (entry: LedgerEntryView) : Msg option =
    match categoryLockFor entry with
    | CategoryEditable ->
        if model.CategoryPickerFor = Some entry.Id then Some CloseCategoryPicker
        else Some (OpenCategoryPicker entry.Id)
    // Bei einer Split-Buchung öffnet der Pfeil den Split-Editor (ledger-pafkp).
    | CategoryOpensSplitEditor -> Some (OpenSplitEditor entry.Id)
    | _ -> None

/// Die Kategorie im Kopf des Details (Tafel 4.9, ledger-fa1fp): am PC voll qualifiziert
/// ("Gruppe: Name", ADR 0039), im Sheet am Handy kurz (der Blattname). Split-Zeilen im Detail
/// bleiben voll qualifiziert (`splitTargetLabel`, ADR 0045): dort wäre die Kurzform mehrdeutig,
/// weil zwei Zeilen denselben Blattnamen in verschiedenen Gruppen tragen können.
let detailCategoryLabel (form: DetailForm) (entry: LedgerEntryView) : string =
    // Eine aufgeteilte Buchung heißt im Detail „Aufgeteilt“, ihre Zeilen stehen in der Karte darunter
    // (Prototyp Version 17, ledger-zfj78).
    if entry.CategoryName.IsNone && not (List.isEmpty entry.Splits) then "Aufgeteilt" else
    match form with
    | DetailPanel -> categoryLabelQualified entry
    | DetailSheet -> categoryLabel entry

/// Die Nachricht einer Wahl im Picker des Details (ledger-qvjvs): ein Konto macht die Buchung
/// zum Übertrag (`setEntryTransfer`), eine Kategorie geht weiter über `setEntryCategory`.
let detailPickerMsg (entryId: Guid) (choice: CategoryPicker.PickerChoice) : Msg option =
    match choice with
    | CategoryPicker.ChoseAccount account -> Some (StartSetEntryTransfer (entryId, account, false))
    | CategoryPicker.ChoseCategory id -> Some (StartSetEntryCategory (entryId, id))
    | CategoryPicker.ChoseToBeBudgeted -> None

/// Die wählbaren Übertrag-Ziele: die offenen Budget-Konten des Budgets (`openOnBudgetAccounts`),
/// ohne das eigene Konto. Solange die Liste nicht geladen ist, ist sie leer; den Grund dafür zeigt
/// `transferTargetsLoadState`.
let transferTargetsExcluding (own: YnabAccountId option) (model: Model) : YnabAccount list =
    match model.TransferTargets with
    | Success accounts -> openOnBudgetAccounts accounts |> List.filter (fun a -> Some a.Id <> own)
    | _ -> []

/// Die Übertrag-Ziele einer Buchung: ohne das Konto, auf dem sie steht.
let transferTargetsFor (model: Model) (entry: LedgerEntryView) : YnabAccount list =
    let own =
        match model.Accounts with
        | Success view ->
            view.Accounts
            |> List.tryFind (fun a -> a.Id = entry.AccountId)
            |> Option.bind (fun a -> a.YnabAccountId)
        | _ -> None
    transferTargetsExcluding own model

/// Der Ladezustand der Übertrag-Ziele für den Picker im Split-Editor (ledger-vq2g8): bei
/// `Failure` meldet der Konten-Abschnitt den Fehler, beim Laden den Ladezustand. Vorher war die
/// leere Liste von `transferTargetsFor` still, Übertrag-Ziele fehlten ohne Hinweis.
let transferTargetsLoadState (targets: RemoteData<YnabAccount list>) : CategoryPicker.PickerLoadState =
    match targets with
    | Success _ -> CategoryPicker.PickerReady
    | Failure _ -> CategoryPicker.PickerFailed None
    | NotAsked | Loading -> CategoryPicker.PickerLoading

/// Was alle Picker im Kassenbuch teilen (Detail, Split, Auswahl-Leiste, Neue Buchung): die Kategorien
/// ohne interne, "Ohne Kategorie", Stand, Laden und "Erneut versuchen"; ohne Konten, ohne Auswahl.
/// Jeder Ort setzt mit `with`, was er anders braucht.
let private basePickerProps (dispatch: Msg -> unit) (model: Model) : CategoryPicker.CategoryPickerProps =
    { Categories = model.Categories |> RemoteData.toOption |> Option.defaultValue [] |> withoutInternalCategories
      Current = None
      Suggested = []
      Recent = recentPickerIds model.RecentCategories
      Accounts = []
      CurrentAccount = None
      AccountsLoad = CategoryPicker.PickerReady
      OfferWithout = true
      ToBeBudgeted = None
      StandLabel = categoryStandLabel model DateTime.Now
      LoadState = categoryLoadState model.Categories
      OnRetry = fun () -> dispatch LoadCategories
      OnSelect = ignore
      OnClose = ignore }

/// Die Props des Pickers im Detail: Kategorien und, wie im Split, die Konten des Budgets im
/// Abschnitt "Übertrag auf Konto" (ledger-qvjvs). Bei einem Übertrag ist sein Ziel angehakt.
let detailPickerProps (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) : CategoryPicker.CategoryPickerProps =
    { basePickerProps dispatch model with
        Current = entry.CategoryId |> Option.map YnabCategoryId
        Suggested = model.CategorySuggestions
        Accounts = transferTargetsFor model entry
        CurrentAccount = entry.TransferAccountId |> Option.map YnabAccountId
        AccountsLoad = transferTargetsLoadState model.TransferTargets
        OnSelect = fun choice -> detailPickerMsg entry.Id choice |> Option.iter dispatch
        OnClose = fun () -> dispatch CloseCategoryPicker }

/// Die Kategorie-Zeile mit Pfeil, Picker (Popover oder Sheet) und, nach einer Änderung,
/// der Karte "Budget nach dem Wechsel" darunter.
let private categoryField (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) (valueColor: string) : ReactElement =
    let pickerOpen = model.CategoryPickerFor = Some entry.Id
    let lock = categoryLockFor entry
    let arrowMsg = categoryArrowMsg model entry
    let onArrow = fun () -> arrowMsg |> Option.iter dispatch
    let pickerProps = detailPickerProps dispatch model entry
    React.fragment [
        Html.div [
            // Das Popover hängt unter dieser Zeile (`position: relative`, wie bei den Chips). Nach einer Änderung
            // leuchtet die Zeile kurz (ledger-p5y9s).
            prop.className ("relative" + (if noteFlashing model entry AtCategory then " " + Feedback.flash else ""))
            prop.children [
                Detail.fieldRowWith
                    (if arrowMsg.IsSome then Some onArrow else None)
                    "detail-category"
                    "Kategorie"
                    (Detail.valueText
                        valueColor
                        (transferCategoryLabel model entry |> Option.defaultValue (detailCategoryLabel model.DetailForm entry)))
                    (Some (
                        React.fragment [
                            // „alle zeigen“ setzt wie zuvor der Trichter den Kategorie-Filter (ADR 0053, ledger-zfj78).
                            match categoryFilterLabel entry, entry.CategoryId with
                            | Some label, Some categoryId ->
                                Detail.showAllLink label (fun () -> dispatch (FilterByRowCategory categoryId))
                            | _ -> ()
                            Detail.pickerArrow categoryArrowLabel pickerOpen (categoryLockHint lock) onArrow
                        ]
                    ))
                match categoryPickerPlacement model.DetailForm with
                | PickerPopover -> CategoryPicker.popoverInPanel pickerOpen pickerProps
                // Im Sheet ersetzt die Auswahl den Inhalt (`detailSheetView`, §7.15).
                | PickerSheet -> ()
            ]
        ]
        detailNoteView dispatch model entry AtCategory
        match categoryCardLinesFor model entry with
        | [] -> ()
        | lines ->
            Html.div [
                prop.custom ("data-testid", "category-change-card")
                prop.className $"px-4 detail-inset detail-line py-3 border-b {Tokens.Borders.subtle}"
                prop.children [ CategoryPicker.CategoryChangeCard.view lines ]
            ]
    ]

/// Der Grund, aus dem das Feld Split gesperrt ist. Eine in YNAB gelöschte Buchung nennt ihren
/// eigenen Hinweis, sonst kommt der Grund aus den Sperren, die der Lesepfad liefert.
let splitLockTextFor (entry: LedgerEntryView) : string option =
    if (heldLockReason entry).IsSome then heldLockReason entry
    elif entry.DeletedInYnab then Some "In YNAB gelöscht, die Buchung lässt sich nicht aufteilen"
    else SplitLogic.lockReasonFor entry

/// Die Nachricht des festen Knopfs im Feld Split ("Aufteilen" bzw. "Ändern"): öffnet den Editor,
/// wenn die Sperren der Buchung es erlauben, sonst nichts (ledger-pafkp).
let splitActionMsg (entry: LedgerEntryView) : Msg option =
    if (splitLockTextFor entry).IsSome then None else Some (OpenSplitEditor entry.Id)

/// Die Nachricht des Knopfs "Lena fragen" bzw. "Rückfrage zurücknehmen" in der Knopfzeile:
/// `None`, wenn sie gesperrt ist oder gerade ein Aufruf läuft (ledger-xfjcp, ADR 0056).
let lenaActionMsg (model: Model) (entry: LedgerEntryView) : Msg option =
    let action = lenaActionFor model.LenaFlagColor entry
    if action.DisabledReason.IsSome || (model.FlagRows |> Map.tryFind entry.Id) = Some FlagSending then None
    else Some (StartSetLenaFlag (entry.Id, action.Target))

/// Die Knopfzeile unter den Feldern (Prototyp Version 17 `.btnrow`, ledger-zfj78): „Aufteilen“ (bei einer
/// aufgeteilten Buchung „Ändern“) öffnet den Split-Editor und ersetzt das Feld Split; „Lena fragen“ mit Flagge bzw.
/// „Rückfrage zurücknehmen“ ersetzt die Flag-Zeile. Gesperrte Knöpfe nennen ihren Grund darunter.
let private detailActionRow
    (dispatch: Msg -> unit)
    (model: Model)
    (flagRow: FlagRowState option)
    (entry: LedgerEntryView)
    : ReactElement =
    let splitLock = splitLockTextFor entry
    let lena = lenaActionFor model.LenaFlagColor entry
    // Die Flagge in der Farbe aus den Einstellungen (`Badge.lenaFlagScope` an der Hülle, ADR 0087 Punkt 8).
    let flagIcon = Some (Icons.flag Icons.SM Icons.LenaFlag)
    Detail.actionRowWith
        (Badge.lenaFlagScope (lenaColorOf model.LenaFlagColor))
        [ Detail.rowAction
              "detail-split-action"
              (SplitLogic.splitActionLabel entry)
              None
              splitLock
              false
              (fun () -> splitActionMsg entry |> Option.iter dispatch)
          // Nach „Lena fragen“ wechselt der Knopf die Beschriftung und leuchtet kurz (ledger-enjex): er ist selbst die
          // Bestätigung und das Rückgängig. Die Hülle trägt das Leuchten, der Knopf bleibt derselbe (Fokus).
          Html.div [
              prop.custom ("data-testid", "detail-flag-action-wrap")
              prop.className ("flex rounded-lg" + (if noteFlashing model entry AtLena then " " + Feedback.flash else ""))
              prop.children [
                  Detail.rowAction
                      "detail-flag-action"
                      lena.Label
                      flagIcon
                      lena.DisabledReason
                      (flagRow = Some FlagSending)
                      (fun () -> lenaActionMsg model entry |> Option.iter dispatch)
              ]
          ] ]
        [ match splitLock with
          | Some text -> "detail-split-lock-hint", text
          | None -> ()
          match lena.DisabledReason with
          | Some reason -> "detail-flag-lock-hint", reason
          | None -> () ]
        // Unter dem Knopf nur bei vorherigem fremdem Flag „✓ Lena gefragt · Rückgängig“, gelb bei einem YNAB-Fehler,
        // rot bei einem abgelehnten Befehl (wie die Sperrgründe).
        [ match detailNoteAt model entry.Id AtLena with
          | Some note when note.Text <> "" -> detailNoteLine dispatch false note
          | _ -> () ]

/// Die Nachricht der Memo-Aktion ("Memo hinzufügen" bzw. "Bearbeiten"): `None`, wenn sie
/// gesperrt ist oder gerade ein Aufruf läuft (ledger-fa1fp, ADR 0062).
let memoActionMsg (model: Model) (entry: LedgerEntryView) : Msg option =
    let action = memoActionFor entry
    if action.DisabledReason.IsSome || (model.MemoRows |> Map.tryFind entry.Id) = Some MemoSending then None
    else Some (StartMemoEdit entry.Id)

/// Der Text der Memo-Aktion ohne Memo (Prototyp Version 17, Skizze ledger-vf5h6): „hinzufügen ›“.
let memoEmptyText = "hinzufügen"

/// Das Eingabefeld des Memos, wenn es für diese Buchung offen ist.
let memoEditFor (model: Model) (entry: LedgerEntryView) : MemoEdit option =
    model.MemoEdit |> Option.filter (fun edit -> edit.MemoEntryId = entry.Id)

/// Das Eingabefeld des Memos für diese Buchung (mehrzeilig, design-system-x045q): am PC im Panel an der Stelle des
/// Blocks, am Handy als Inhalt des Detail-Sheets (`DetailBottomSheet`).
let private memoEditorFor
    (dispatch: Msg -> unit)
    (memoRow: MemoRowState option)
    (entry: LedgerEntryView)
    (edit: MemoEdit)
    : ReactElement =
    let problem =
        match memoDraftProblem edit.MemoDraft, memoRow with
        | Some problem, _ -> Some problem
        | None, Some (MemoCommandFailed message) -> Some message
        | None, _ -> None
    let busy = (memoRow = Some MemoSending)
    Detail.memoEditor
        { Value = edit.MemoDraft
          OnChange = fun text -> dispatch (MemoEditChanged text)
          OnSave = fun () -> dispatch (StartSaveMemo entry.Id)
          OnCancel = fun () -> dispatch CancelMemoEdit
          Busy = busy
          Problem = problem
          MaxLength = ynabMemoMaxLength
          SaveDisabled = busy || (memoDraftProblem edit.MemoDraft).IsSome }

/// Das Memo im Detail als Block (Skizze ledger-vf5h6 Bild 11, design-system-x045q): „Memo“ links oben, rechts
/// „Bearbeiten ›“ bzw. „hinzufügen ›“, darunter der ganze Text über die volle Breite; ohne Memo nur die Kopfzeile.
/// Ist das Feld offen, steht an dieser Stelle das Eingabefeld (am PC im Panel). Ist die Aktion gesperrt (in YNAB
/// gelöscht), nennt ein Text unter dem Memo den Grund.
let private memoField
    (dispatch: Msg -> unit)
    (model: Model)
    (memoRow: MemoRowState option)
    (entry: LedgerEntryView)
    (textColor: string)
    : ReactElement =
    match memoEditFor model entry with
    | Some edit -> memoEditorFor dispatch memoRow entry edit
    | None ->
        let action = memoActionFor entry
        let openMsg = memoActionMsg model entry
        let memo = memoTextOf entry
        // Die ganze Fläche und die Aktion öffnen das Eingabefeld. Die Aktion trägt den Namen („Memo hinzufügen“
        // bzw. „Bearbeiten“), sichtbar „hinzufügen ›“ bzw. „Bearbeiten ›“; gesperrt oder während eines Aufrufs
        // wirkt sie nicht.
        Detail.fieldBlock
            (openMsg |> Option.map (fun msg -> fun () -> dispatch msg))
            "detail-memo"
            "Memo"
            (Detail.fieldBlockAction
                "detail-memo-action"
                (if memo.IsSome then "Bearbeiten" else memoEmptyText)
                action.Label
                (if openMsg.IsSome then None else Some (action.DisabledReason |> Option.defaultValue action.Label))
                (fun () -> openMsg |> Option.iter dispatch))
            [ match memo with
              | Some text -> Detail.blockText textColor text
              | None -> ()
              match action.DisabledReason with
              | Some reason -> Detail.blockLockHint "detail-memo-lock-hint" reason
              | None -> () ]

/// Die externen Links des Buchungs-Details als (Beschriftung, Adresse) (ledger-rjhwp). Leer
/// für eine Buchung ohne Treffer; ein fehlendes Feld (`null`) gilt defensiv als leer. Client und
/// Server werden zusammen ausgeliefert, das Feld kommt nur vom Server zum Client.
let detailExternalLinks (entry: LedgerEntryView) : (string * string) list =
    if isNull (box entry.ExternalLinks) then []
    else entry.ExternalLinks |> List.map (fun link -> link.Label, link.Url)

/// Die Link-Zeilen von Karte und Detail als (Titel, Nummer, Adresse): eine Amazon-Bestellung heißt „Bestellung bei
/// Amazon“ mit der Nummer darunter, die übrigen Links tragen ihre Beschriftung (Abweichung (d)).
let reviewLinks (entry: LedgerEntryView) : (string * string option * string) list =
    let prefix = "Bestellung "
    detailExternalLinks entry
    |> List.map (fun (label, url) ->
        if label.StartsWith prefix then "Bestellung bei Amazon", Some (label.Substring prefix.Length), url
        else label, None, url)

/// Die Link-Zeilen unter dem Payee (ledger-zfj78): „Bestellung bei …“ mit der Nummer darunter und „öffnen“. Sheet
/// und Seitenpanel teilen sich `detailContent`, damit auch diese Zeilen in beiden dieselben sind.
let externalLinksField (entry: LedgerEntryView) : ReactElement list =
    reviewLinks entry |> List.map (fun (title, detail, url) -> Detail.externalLinkRow title detail url)

// ============================================
// Regel aus der Buchung (ledger-hg5m5)
// ============================================

/// Das offene Regel-Formular für die gezeigte Buchung, `true` am Handy (Sheet), `false` am PC (Panel, Dialog).
type EntryRuleFormSlot = bool -> EntryRuleForm option

let noEntryRuleForm : EntryRuleFormSlot = fun _ -> None

let ruleFromEntryLabel = "Regel anlegen"

/// Der feste Eintrag „Regel anlegen“ unter den Feldern des Details; ohne Payee keiner.
let private ruleFromEntryRow (dispatch: Msg -> unit) (entry: LedgerEntryView) : ReactElement =
    let openForm = fun () -> dispatch (RuleFromEntryRequested entry.Id)
    Detail.fieldRowWith
        (Some openForm)
        "detail-rule"
        ruleFromEntryLabel
        Html.none
        (Some (Detail.forwardArrow "detail-rule-arrow" ruleFromEntryLabel None openForm))

/// Das Angebot unter der Kategorie nach einer Änderung: „Regel für „<Payee>“ anlegen?“ mit „Anlegen“ und ×.
let private ruleOfferRow (dispatch: Msg -> unit) (entry: LedgerEntryView) (payee: string) : ReactElement =
    Detail.offerRow
        "rule-offer"
        (ruleOfferText payee)
        "Anlegen"
        (fun () -> dispatch (RuleFromEntryRequested entry.Id))
        "Angebot ausblenden"
        (fun () -> dispatch RuleOfferDismissed)

/// Der Kopf des Formulars im Panel: „Zurück“ zur Buchung und der Titel (§7.15).
let private ruleFormHead (form: EntryRuleForm) : ReactElement =
    Html.div [
        prop.className $"flex items-center gap-2 px-4 py-2 border-b {Tokens.Borders.subtle}"
        prop.children [
            PageHeader.backButtonFor (Some "Zurück zur Buchung") form.OnBack
            Html.h2 [ prop.className $"min-w-0 truncate {Tokens.FontSizes.lg} font-semibold {Tokens.Colors.textPrimary}"; prop.text form.Title ]
        ]
    ]

/// Alles unter dem Kopf, in der Ordnung des Prototyps Version 17 (ledger-zfj78): Betrag mit Prüfstand,
/// Hinweisleiste, Prüf-Knopf, die Zeilen Kategorie (mit Split-Karte und Regel-Angebot), Memo, Payee, Bestell-Link
/// und „Regel anlegen“, die Knopfzeile Aufteilen und Lena, die aufklappbare Historie, die Herkunft und „Buchung
/// löschen“. Panel und Sheet teilen ihn; den Kopf mit dem Payee stellt die Hülle.
let private detailContent (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) : ReactElement list =
    let rowState = model.ReviewMarkRows |> Map.tryFind entry.Id
    let categoryRow = model.CategoryRows |> Map.tryFind entry.Id
    let flagRow = model.FlagRows |> Map.tryFind entry.Id
    let memoRow = model.MemoRows |> Map.tryFind entry.Id
    let amountRow = model.AmountRows |> Map.tryFind entry.Id
    let payeeRow = model.PayeeRows |> Map.tryFind entry.Id
    let muted = detailReadOnly entry
    let textColor = if muted then Tokens.Colors.textMuted else Tokens.Colors.textPrimary
    let payeeOpenMsg = payeeEditMsg model entry

    [ detailAmountRow dispatch model entry

      match detailNoticeWithPayee rowState categoryRow flagRow memoRow amountRow payeeRow model.History entry with
      | Some notice -> noticeView dispatch rowState categoryRow (model.PushRetryRows |> Map.tryFind entry.Id) entry notice
      | None -> ()

      // Eine in YNAB gelöschte oder wartende Buchung nimmt keinen Prüfvermerk an: kein Knopf. Der Knopf schickt
      // dieselbe Nachricht wie zuvor der Schalter; Rückmeldung und Rückgängig bleiben (ledger-zfj78).
      if detailReviewSwitchVisible entry then
          let button = detailReviewButtonFor entry
          Detail.reviewButton button.Label button.Primary (rowState = Some ReviewMarkSending) (fun () -> dispatch button.Msg)

      Html.div [
          prop.className $"mt-1 border-t {Tokens.Borders.subtle}"
          prop.children [
              // Kategorie: voll qualifiziert am PC (ADR 0039), „alle zeigen“ setzt den Kategorie-Filter, der Pfeil
              // öffnet den Picker (ledger-sfab0). Filtern und Ändern sind getrennte Ziele (ADR 0053).
              categoryField
                  dispatch
                  model
                  entry
                  (if muted then Tokens.Colors.textMuted
                   elif isWithoutCategory entry then Tokens.Colors.attention
                   else Tokens.Colors.textPrimary)

              match splitLineTexts entry with
              | [] -> ()
              | lines ->
                  Html.div [
                      prop.className (if noteFlashing model entry AtSplits then Feedback.flash else "")
                      prop.children [ Detail.splitCard lines ]
                  ]

              detailNoteView dispatch model entry AtSplits

              match ruleOfferFor model entry with
              | Some payee -> ruleOfferRow dispatch entry payee
              | None -> ()

              // Memo und Payee leuchten nach einer Änderung, darunter steht die Bestätigung (ledger-enjex).
              Html.div [
                  prop.className (if noteFlashing model entry AtMemo then Feedback.flash else "")
                  prop.children [ memoField dispatch model memoRow entry textColor ]
              ]

              detailNoteView dispatch model entry AtMemo

              // Payee: Name, „alle zeigen“ (setzt die Suche, ledger-d9z3n) und der Pfeil zur Payee-Auswahl
              // (ledger-fxt09). Ein Tipp auf die Zeile öffnet ebenfalls die Auswahl.
              Html.div [
                  prop.className (if noteFlashing model entry AtPayee then Feedback.flash else "")
                  prop.children [
                      Detail.fieldRowWith
                          (payeeOpenMsg |> Option.map (fun msg -> fun () -> dispatch msg))
                          "detail-payee"
                          "Payee"
                          (Detail.valueText
                              (if muted || payeeMissing entry then Tokens.Colors.textMuted else Tokens.Colors.textPrimary)
                              (payeeLabel entry))
                          (Some (
                              React.fragment [
                                  match payeeFilterLabel entry, entry.Payee with
                                  | Some label, Some payee -> Detail.showAllLink label (fun () -> dispatch (FilterByRowPayee payee))
                                  | _ -> ()
                                  Detail.forwardArrow
                                      "detail-payee-arrow"
                                      payeeEditLabel
                                      (if payeeOpenMsg.IsSome then None else Some (payeeLockFor entry |> Option.defaultValue payeeEditLabel))
                                      (fun () -> payeeOpenMsg |> Option.iter dispatch)
                              ]
                          ))
                  ]
              ]

              detailNoteView dispatch model entry AtPayee

              yield! externalLinksField entry

              if (ruleSeedFor entry).IsSome then ruleFromEntryRow dispatch entry
          ]
      ]

      detailActionRow dispatch model flagRow entry

      Detail.historySection
          (historyToggleLabel model.History)
          (historyOpen model entry)
          (fun () -> dispatch ToggleHistory)
          (historyView dispatch entry model.History)

      Detail.originLine $"Herkunft: {originLabel entry.Origin}"

      // Ganz unten wie im Prototyp (Version 17): "Buchung löschen" in Rot ohne Fläche, klein, volle Breite.
      if detailDeleteVisible entry then
          Detail.block "detail-delete" [] [
              Button.view {
                  Button.defaultProps with
                      Text = "Buchung löschen"
                      Variant = Button.DangerOutline
                      Size = Button.Small
                      IsBusy = model.DeletingEntries.Contains entry.Id
                      ClassName = Some "w-full"
                      OnClick = fun () -> dispatch (StartDeleteEntry entry.Id)
              }
              // Scheitert das Löschen, steht der Grund rot unter dem Knopf, das Detail bleibt offen (ledger-k68sd).
              match deleteFailureFor model entry with
              | Some reason ->
                  Html.div [
                      prop.custom ("data-testid", "detail-delete-error")
                      prop.children [ ErrorDisplay.inline' (deleteFailureText reason) ]
                  ]
              | None -> ()
          ] ]

/// Das Detail als Seitenpanel (PC). Gerendert in der Spalte der Liste (`FilterBarColumn`).
// ============================================
// Split-Editor (ledger-pafkp, ADR 0058, Handoff Ablauf 4)
// ============================================

let private isSavingSplit (model: Model) (editor: SplitLogic.SplitEditorState) : bool =
    (model.CategoryRows |> Map.tryFind editor.EntryId) = Some CategorySending

/// Gesamtbetrag und Rest über den Zeilen. Beträge ohne Vorzeichen, wie die Eingabe; der Rest ist
/// offen (gelb), zu viel verteilt (rot) oder ausgeglichen (neutral), ledger-ng110.
let splitSummaryProps (editor: SplitLogic.SplitEditorState) : SplitUi.SummaryProps =
    let rest = SplitLogic.remainder editor
    let text (value: decimal) = Money.formatAmountText false true value editor.Total.Currency
    { TotalText = text (abs editor.Total.Amount)
      RestText = text rest
      Rest =
        if rest > 0m then SplitUi.RestOpen
        elif rest < 0m then SplitUi.RestOver
        else SplitUi.RestBalanced }

let private lineCategoryId (line: SplitLogic.SplitDraftLine) : YnabCategoryId option =
    match line.Target with
    | Some (ToCategory (id, _)) -> Some id
    | _ -> None

let private lineAccountId (line: SplitLogic.SplitDraftLine) : YnabAccountId option =
    match line.Target with
    | Some (ToTransfer (id, _)) -> Some id
    | _ -> None

/// Der Picker einer Zeile: dieselben Props wie im Kategorie-Feld, ohne Vorschläge zum Payee, dazu
/// die wählbaren Übertrag-Konten.
let private splitTargetPickerProps
    (dispatch: Msg -> unit)
    (model: Model)
    (entry: LedgerEntryView)
    (index: int)
    (line: SplitLogic.SplitDraftLine)
    : CategoryPicker.CategoryPickerProps =
    { basePickerProps dispatch model with
        Current = lineCategoryId line
        Recent = []
        Accounts = transferTargetsFor model entry
        CurrentAccount = lineAccountId line
        AccountsLoad = transferTargetsLoadState model.TransferTargets
        OnSelect =
            fun choice ->
                match choice with
                | CategoryPicker.ChoseCategory id -> dispatch (SelectSplitCategory (index, id))
                | CategoryPicker.ChoseAccount id -> dispatch (SelectSplitAccount (index, id))
                | CategoryPicker.ChoseToBeBudgeted -> ()
        OnClose = fun () -> dispatch CloseSplitPicker }

/// Der Inhalt des Editors, für Panel und Sheet derselbe: Summe, Zeilen, "Zeile hinzufügen". Der
/// Ziel-Picker je Zeile (Kategorie oder Konto) ist am PC ein Popover unter der Zeile, am Handy ersetzt er
/// den Inhalt des Sheets (`detailSheetView`, §7.15).
let private splitEditorBody
    (dispatch: Msg -> unit)
    (model: Model)
    (entry: LedgerEntryView)
    (editor: SplitLogic.SplitEditorState)
    : ReactElement list =
    let saving = isSavingSplit model editor
    let placement = categoryPickerPlacement model.DetailForm
    let lineCount = List.length editor.Lines
    [ yield SplitUi.summary (splitSummaryProps editor)
      for index, line in List.indexed editor.Lines do
          yield
              Html.div [
                  prop.key (string index)
                  prop.className "relative"
                  prop.children [
                      SplitUi.line
                          { Index = index
                            TargetLabel = SplitLogic.targetLabel line
                            TargetChosen = line.Target.IsSome
                            AmountText = line.AmountText
                            AmountError =
                                if SplitLogic.lineAmountInvalid line.AmountText then Some Capture.amountInvalidText else None
                            Memo = line.Memo
                            Disabled = saving
                            CanRemove = lineCount > 1
                            OnPickTarget = fun () -> dispatch (OpenSplitTargetPicker index)
                            OnAmount = fun text -> dispatch (SetSplitLineAmount (index, text))
                            OnMemo = fun text -> dispatch (SetSplitLineMemo (index, text))
                            OnRest = fun () -> dispatch (FillSplitLineRest index)
                            OnRemove = fun () -> dispatch (RemoveSplitLine index) }
                      match placement with
                      | PickerPopover ->
                          CategoryPicker.popoverInPanel
                              (editor.Picker = SplitLogic.SplitTargetPickerFor index)
                              (splitTargetPickerProps dispatch model entry index line)
                      | PickerSheet -> ()
                  ]
              ]
      yield
          SplitUi.lineActions
              (SplitLogic.canAddLine editor && not saving)
              (fun () -> dispatch AddSplitLine)
              (not saving)
              (fun () ->
                  dispatch (
                      StartSplitCashback (SplitLogic.cashbackTarget (quickAddAccountOf model) (transferTargetsFor model entry))
                  )) ]

/// Der Editor im offenen Detail dieser Buchung, sonst `None`.
let splitEditorFor (model: Model) (entry: LedgerEntryView) : SplitLogic.SplitEditorState option =
    model.SplitEditor |> Option.filter (fun e -> e.EntryId = entry.Id)

/// Das Detail als Seitenpanel (PC). Gerendert in der Spalte der Liste (`FilterBarColumn`). Mit
/// offenem Editor ersetzt er die Felder; Kopf und Hülle bleiben, die Fußleiste trägt "Abbrechen"
/// und "Speichern".
// ============================================
// Payee-Auswahl (ledger-fxt09)
// ============================================

/// Die offene Payee-Auswahl, wenn sie zu dieser Buchung gehört.
let payeeEditOf (model: Model) (entry: LedgerEntryView) : PayeeEdit option =
    model.PayeeEdit |> Option.filter (fun edit -> edit.PayeeEntryId = entry.Id)

/// Die Eigenschaften der Payee-Auswahl für eine Buchung: Payee, Vorschläge des Kassenbuchs und
/// die Rückrufe. `focusSearch`: am PC der Fokus in der Suche, am Handy nicht (§7.9).
let payeePickerProps (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) (edit: PayeeEdit) (focusSearch: bool) : PayeePicker.PayeePickerProps =
    { Current = entry.Payee
      Suggestions =
        model.PayeeChoices |> RemoteData.toOption |> Option.defaultValue [] |> List.map (fun s -> s.Payee, s.Count)
      Loading = (match model.PayeeChoices with Loading -> true | _ -> false)
      LoadError = (match model.PayeeChoices with Failure message -> Some message | _ -> None)
      FieldError = edit.PayeeFieldError
      FocusSearch = focusSearch
      Busy = (model.PayeeRows |> Map.tryFind entry.Id) = Some PayeeSending
      OnPick = fun payee -> dispatch (StartSavePayee (entry.Id, payee))
      OnSearchChanged = fun () -> dispatch PayeeSearchEdited
      OnClose = fun () -> dispatch CancelPayeeEdit }

/// Am PC öffnet die Payee-Auswahl als Dialog über der Liste (§7.6, Auswahl), das Panel bleibt stehen.
let private payeeDialog (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) : ReactElement =
    match payeeEditOf model entry with
    | None -> Html.none
    | Some edit ->
        Modal.view
            { Modal.defaultProps with
                IsOpen = true
                OnClose = fun () -> dispatch CancelPayeeEdit
                Size = Modal.Large
                Title = Some "Payee"
                Subtitle = Some (payeeLabel entry) }
            [ Modal.body [ PayeePicker.view (payeePickerProps dispatch model entry edit (Viewport.isFinePointer ())) ] ]

// ============================================
// Sheet "Betrag" (ledger-m7mye, ADR 0092, Prototyp Version 17)
// ============================================

/// Das offene Sheet "Betrag", wenn es zu dieser Buchung gehört.
let amountEditOf (model: Model) (entry: LedgerEntryView) : AmountEdit option =
    model.AmountEdit |> Option.filter (fun edit -> edit.AmountEntryId = entry.Id)

/// Ausgabe oder Einnahme über einem Betragsfeld ohne Vorzeichen (Sheet "Betrag", "Neue Buchung"). Beide
/// Segmente halten den Fokus im Feld (`KeepsFocus`, design-system-ak97z), am Handy bleibt die Tastatur offen.
let private directionSegments (testId: string) (isOutflow: bool) (onChange: bool -> unit) : ReactElement =
    Segmented.view
        testId
        "Ausgabe oder Einnahme"
        [ { Segmented.segment "Ausgabe" isOutflow (fun () -> onChange true) with KeepsFocus = true }
          { Segmented.segment "Einnahme" (not isOutflow) (fun () -> onChange false) with KeepsFocus = true } ]

/// Der Inhalt des Sheets "Betrag": Ausgabe oder Einnahme, das Betragsfeld ohne Vorzeichen
/// (Zifferntastatur `decimal`, rechtsbündig, Mono, groß), bei einer aufgeteilten Buchung der
/// Hinweis, darunter der Satz zu YNAB. Handy (Sheet) und PC (Dialog) teilen ihn. Ein abgelehntes
/// Speichern zeigt den Feldfehler direkt unter dem Feld (`ErrorDisplay.inline'`, Styleguide §5,
/// ledger-q42pg), das Feld verweist per `aria-describedby` darauf, bekommt den Fokus und der Fehler
/// rollt ins Bild (`RevealAmountFieldError`).
let private amountFormBody (dispatch: Msg -> unit) (entry: LedgerEntryView) (edit: AmountEdit) : ReactElement list =
    let invalid = edit.AmountFieldError.IsSome
    [ directionSegments "amount-direction" edit.AmountIsOutflow (fun isOutflow -> dispatch (AmountDirectionChanged isOutflow))
      Html.div [
          prop.custom ("data-testid", "amount-input")
          prop.children [
              Input.text
                  { Input.textInputDefaults with
                      Value = edit.AmountDraft
                      OnChange = fun text -> dispatch (AmountDraftChanged text)
                      Placeholder = "0,00"
                      Size = Input.Large
                      InputMode = Some "decimal"
                      AriaLabel = Some "Betrag"
                      DescribedBy = (if invalid then Some amountFieldErrorId else None)
                      AriaInvalid = invalid
                      ClassName = Some (Input.amountFieldClass true invalid)
                      OnKeyDown =
                        Some (fun e ->
                            if e.key = "Enter" then
                                e.preventDefault ()
                                dispatch (StartSaveAmount entry.Id)) }
              match edit.AmountFieldError with
              | Some message ->
                  Html.div [
                      prop.id amountFieldErrorId
                      prop.custom ("data-testid", amountFieldErrorId)
                      prop.children [ ErrorDisplay.inline' message ]
                  ]
              | None -> ()
          ]
      ]
      if not (List.isEmpty entry.Splits) then ErrorDisplay.notice amountSplitNote
      Html.p [ prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted}"; prop.text amountYnabNote ] ]

/// "Abbrechen" und "Speichern" des Sheets "Betrag", nebeneinander in voller Breite.
let private amountButtons (dispatch: Msg -> unit) (busy: bool) (entry: LedgerEntryView) : ReactElement =
    Html.div [
        prop.className "grid w-full grid-cols-2 gap-3"
        prop.children [
            Button.view
                { Button.defaultProps with
                    Text = "Abbrechen"
                    // Im Fuß neben der Hauptaktion ohne Füllung (Styleguide §2, ledger-gy77x).
                    Variant = Button.Outline
                    IsDisabled = busy
                    ClassName = Some "w-full"
                    OnClick = fun () -> dispatch CancelAmountEdit }
            // "Speichern" nimmt dem Betragsfeld den Fokus nicht: nach einem abgelehnten Speichern
            // bleibt die Tastatur am Handy offen und Roman tippt gleich weiter (ledger-q42pg).
            Button.view
                { Button.defaultProps with
                    Text = "Speichern"
                    Variant = Button.Primary
                    IsBusy = busy
                    ClassName = Some "w-full"
                    KeepsFocus = true
                    OnClick = fun () -> dispatch (StartSaveAmount entry.Id) }
        ]
    ]

/// Der Fuß des Sheets "Betrag". Lehnt der Server das Speichern ab, steht der Grund rot über den Knöpfen (R3 Platz 2,
/// `Feedback.sheetLine`, ledger-enjex), ohne Toast; das Sheet bleibt mit dem Entwurf offen.
let private amountFooter (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) : ReactElement =
    let row = model.AmountRows |> Map.tryFind entry.Id
    Html.div [
        prop.className "flex w-full flex-col gap-2"
        prop.children [
            match row with
            | Some (AmountCommandFailed message) ->
                Feedback.sheetLine { Tone = Feedback.Tone.Error; Action = "Nicht geändert"; Detail = Some message; Act = None }
            | _ -> ()
            amountButtons dispatch (row = Some AmountSending) entry
        ]
    ]

/// Am PC öffnet "Betrag" als Dialog über der Liste, das Panel bleibt rechts stehen (Prototyp).
let private amountDialog (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) : ReactElement =
    match amountEditOf model entry with
    | None -> Html.none
    | Some edit ->
        Modal.view
            { Modal.defaultProps with
                IsOpen = true
                OnClose = fun () -> dispatch CancelAmountEdit
                Size = Modal.Large
                Title = Some "Betrag"
                Subtitle = Some (payeeLabel entry) }
            [ Modal.body [
                  Html.div [
                      prop.custom ("data-testid", "amount-form")
                      prop.className "flex flex-col gap-4"
                      prop.children (amountFormBody dispatch entry edit)
                  ]
              ]
              Modal.footer [ amountFooter dispatch model entry ] ]

let private detailPanelView (today: DateTime) (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) : ReactElement =
    match splitEditorFor model entry with
    | Some editor ->
        let saving = isSavingSplit model editor
        Detail.panel
            $"Aufteilen {payeeLabel entry}"
            // × führt zurück ins Detail wie Esc und „Abbrechen“ (ledger-hme9k).
            [ splitEditorHead today model entry editor (Some (fun () -> dispatch (Components.Ledger.State.detailCloseStep model entry)))
              // Die Fußleiste steht im scrollenden Bereich, am unteren Rand haftend: bei wenigen Zeilen
              // folgt sie dem Inhalt, bei vielen bleibt sie sichtbar. Als eigene Zeile unter dem
              // Panel läge sie bei einer kurzen Liste unterhalb des Fensters.
              Detail.scrollBody (
                  [ yield Detail.sectionLabel "AUFTEILEN"
                    yield! splitEditorBody dispatch model entry editor
                    yield
                        SplitUi.footer
                            editor.SaveError
                            // Ohne `not saving`: sonst gälte `IsBusy` mit `IsDisabled` als `disabled` ohne Spinner.
                            (SplitLogic.canSave editor)
                            saving
                            (fun () -> dispatch CloseSplitEditor)
                            (fun () -> dispatch StartSaveSplits) ]
              ) ]
    | None ->
        React.fragment [
            Detail.panel
                $"Detail {payeeLabel entry}"
                [ detailTitleHead today model entry (Some (fun () -> dispatch (Components.Ledger.State.detailCloseStep model entry)))
                  Detail.scrollBody (detailContent dispatch model entry) ]
            amountDialog dispatch model entry
            payeeDialog dispatch model entry
        ]

/// Das Detail als BottomSheet (Handy, Kompaktliste, zu schmales Fenster für das Panel).
/// Das Sheet bringt den Kopf mit: der Payee als Titel, „<Tag> · <Konto>“ darunter und Schließen
/// (ledger-zfj78); der Betrag steht als erste Zeile im Inhalt. Mit offenem Editor heißt es "Aufteilen" mit Payee und Datum als Unterzeile, der
/// Schließen-Knopf kehrt zum Detail zurück, und "Speichern" klebt unten (kein "Abbrechen").
/// Payee, Betrag, Memo, Kategorie, Aufteilen und das Regel-Formular ersetzen den Inhalt desselben Sheets (§7.15): kein
/// zweites Sheet, nichts fährt beim Wechsel ein oder aus, die Abdunklung bleibt (ledger-hme9k). Beim Wechsel weg vom
/// Detail merkt sich das Sheet die Scrollposition und setzt sie bei der Rückkehr wieder (§7.10).
[<ReactComponent>]
let private DetailBottomSheet (today: DateTime, dispatch: Msg -> unit, model: Model, ruleForm: EntryRuleFormSlot, open': bool) : ReactElement =
    let body = React.useRef<Browser.Types.HTMLElement option> None
    // Die Stelle im Detail, mit der Buchung, zu der sie gehört.
    let saved = React.useRef<(Guid * float) option> None
    let entry = detailEntryOf model
    let editor = entry |> Option.bind (splitEditorFor model)
    // Das Regel-Formular aus der Buchung (ledger-hg5m5) ersetzt den Inhalt wie die Auswahlen darunter.
    let rule = if open' && entry.IsSome then ruleForm true else None
    // Das Sheet "Betrag" ersetzt den Inhalt des Detail-Sheets (kein zweites Sheet darüber, §7.15);
    // Schließen kehrt ins Detail zurück, wie beim Split-Editor.
    let amountEdit = entry |> Option.bind (amountEditOf model)
    // Die Payee-Auswahl ersetzt ebenso den Inhalt (§7.15, ledger-fxt09).
    let payeeEdit = entry |> Option.bind (payeeEditOf model)
    // Das Memo-Feld ersetzt ebenso den Inhalt (§7.15, design-system-x045q): Titel „Memo“, Payee darunter.
    let memoEdit = entry |> Option.bind (memoEditFor model)
    // Auch die Kategorie-Auswahl des Details und das Ziel einer Split-Zeile ersetzen den Inhalt (§7.15,
    // ledger-ea70s); Wahl oder Schließen führen zurück, der Entwurf steht im Modell und bleibt.
    let categoryPicker =
        entry
        |> Option.bind (fun e ->
            match editor with
            | Some ed ->
                match ed.Picker with
                | SplitLogic.SplitTargetPickerFor index ->
                    List.tryItem index ed.Lines |> Option.map (splitTargetPickerProps dispatch model e index)
                | SplitLogic.NoSplitPicker -> None
            | None when model.CategoryPickerFor = Some e.Id && amountEdit.IsNone && payeeEdit.IsNone && memoEdit.IsNone ->
                Some (detailPickerProps dispatch model e)
            | None -> None)
    let showsDetail =
        open' && entry.IsSome && rule.IsNone && categoryPicker.IsNone && payeeEdit.IsNone && amountEdit.IsNone && memoEdit.IsNone && editor.IsNone
    let entryId = entry |> Option.map (fun e -> e.Id)
    React.useLayoutEffect (
        (fun () ->
            match body.current with
            | Some el when showsDetail ->
                // Zurück im Detail derselben Buchung: die Stelle von vorher.
                match saved.current, entryId with
                | Some (id, top), Some current when id = current -> el.scrollTop <- top
                | _ -> ()
                let remember (_: Browser.Types.Event) = saved.current <- entryId |> Option.map (fun id -> id, el.scrollTop)
                el.addEventListener ("scroll", remember)
                React.createDisposable (fun () -> el.removeEventListener ("scroll", remember))
            | Some el ->
                // Ein anderer Inhalt beginnt oben; ein geschlossenes Sheet vergisst die Stelle.
                if open' then el.scrollTop <- 0.0 else saved.current <- None
                React.createDisposable ignore
            | None -> React.createDisposable ignore),
        [| box showsDetail; box entryId; box open' |]
    )
    BottomSheet.viewWithBody
        body
        { IsOpen = open' && entry.IsSome
          OnClose =
            (fun () ->
                match rule, categoryPicker, entry with
                | Some form, _, _ -> form.OnBack ()
                | None, Some picker, _ -> picker.OnClose ()
                | None, None, Some e -> dispatch (Components.Ledger.State.detailCloseStep model e)
                | None, None, None -> ())
          Title =
            (match rule, categoryPicker with
             | Some form, _ -> form.Title
             | None, Some picker -> CategoryPicker.sheetTitle picker.Accounts
             | None, None ->
                 if payeeEdit.IsSome then "Payee"
                 elif amountEdit.IsSome then "Betrag"
                 elif memoEdit.IsSome then "Memo"
                 elif editor.IsSome then "Aufteilen"
                 // Das Detail trägt den Payee als Titel (Prototyp Version 17, ledger-zfj78).
                 else entry |> Option.map payeeLabel |> Option.defaultValue "Buchung")
          Subtitle =
            (match entry, editor with
             | _ when rule.IsSome -> None
             | Some e, _ when categoryPicker.IsSome ->
                 Some $"{payeeLabel e} · {Money.formatAmountText false true e.Amount.Amount e.Amount.Currency}"
             | Some e, _ when payeeEdit.IsSome -> Some (payeeLabel e)
             | Some e, _ when amountEdit.IsSome -> Some (payeeLabel e)
             | Some e, _ when memoEdit.IsSome -> Some (payeeLabel e)
             | Some e, Some _ -> Some $"{payeeLabel e} · {formatDateFull e.Date}"
             | Some e, None -> Some (detailSubtitleFor today (accountNameOf model e) e)
             | _ -> None)
          Footer =
            (match entry, amountEdit, editor with
             | _ when rule.IsSome -> rule.Value.Footer
             | _ when categoryPicker.IsSome -> None
             | _ when payeeEdit.IsSome -> None
             | _ when memoEdit.IsSome -> None
             | Some e, Some _, _ -> Some [ amountFooter dispatch model e ]
             | _, None, Some ed ->
                 let saving = isSavingSplit model ed
                 Some [ SplitUi.sheetFooter ed.SaveError (SplitLogic.canSave ed) saving (fun () -> dispatch StartSaveSplits) ]
             | _ -> None) }
        // Der Inhalt steht nur im DOM, solange das Sheet offen ist: ein geschlossenes Sheet bliebe
        // sonst als verstecktes Doppel neben dem Panel stehen.
        [ match entry, amountEdit, editor, categoryPicker with
          | Some _, _, _, _ when rule.IsSome -> yield! rule.Value.Body
          | Some _, _, _, Some picker when open' -> CategoryPicker.sheetContent picker
          | Some entry, _, _, _ when open' && payeeEdit.IsSome ->
              PayeePicker.view (payeePickerProps dispatch model entry payeeEdit.Value (Viewport.isFinePointer ()))
          | Some entry, None, _, _ when open' && memoEdit.IsSome ->
              memoEditorFor dispatch (model.MemoRows |> Map.tryFind entry.Id) entry memoEdit.Value
          | Some entry, Some edit, _, _ when open' ->
              Html.div [
                  prop.custom ("data-testid", "amount-form")
                  prop.className "flex flex-col gap-4"
                  prop.children (amountFormBody dispatch entry edit)
              ]
          | Some entry, None, Some ed, _ when open' -> yield! splitEditorBody dispatch model entry ed
          | Some entry, None, None, _ when open' -> yield! detailContent dispatch model entry
          | _ -> () ]

/// Das Detail-Sheet am Handy; `ruleForm` füllt es mit dem Regel-Formular, solange es offen ist (ledger-hg5m5).
let private detailSheetView (today: DateTime) (dispatch: Msg -> unit) (model: Model) (ruleForm: EntryRuleFormSlot) (open': bool) : ReactElement =
    DetailBottomSheet (today, dispatch, model, ruleForm, open')

/// Die Hülle des Regel-Formulars am Handy: ein Sheet, Schließen führt zur Buchung zurück (§7.15). Immer gerendert
/// (`viewHeld`): mit `None` fährt es aus und hält dabei Titel, Inhalt und Fuß (§6, §7.10).
let private ruleFormSheet (form: EntryRuleForm option) : ReactElement =
    match form with
    | Some f ->
        BottomSheet.viewHeld
            { IsOpen = true; OnClose = f.OnBack; Title = f.Title; Subtitle = None; Footer = f.Footer }
            f.Body
    | None -> BottomSheet.viewHeld { IsOpen = false; OnClose = ignore; Title = ""; Subtitle = None; Footer = None } []

/// Die Hülle des Regel-Formulars am PC über einer Seite ohne Liste (Durchgehen, §7.6): ein Dialog.
let private ruleFormDialog (form: EntryRuleForm) : ReactElement =
    Modal.view
        { Modal.defaultProps with IsOpen = true; OnClose = form.OnBack; Title = Some form.Title }
        [ Modal.bodyNoPadding form.Body ]

/// Das Panel am PC, mit offenem Regel-Formular dieses statt des Details: es ersetzt den Inhalt und führt mit „Zurück“
/// zur Buchung (ledger-hg5m5).
let private detailPanelOrRuleForm (today: DateTime) (dispatch: Msg -> unit) (model: Model) (ruleForm: EntryRuleFormSlot) (entry: LedgerEntryView) : ReactElement =
    match ruleForm false with
    | Some form -> Detail.panel form.Title [ ruleFormHead form; Detail.scrollBody form.Body ]
    | None -> detailPanelView today dispatch model entry

// ============================================
// Nachladen beim Scrollen (AC5)
// ============================================
// Minimale IntersectionObserver-Bindung (nicht Teil von Fable.Browser.Dom, analog zu
// `Viewport.fs`s visualViewport-Bindung): sobald der Beobachter am Listenende
// sichtbar wird, ruft er `onIntersect`. Kein `root`-Parameter — die Seite scrollt im
// Fenster, nicht in einem eigenen Container.
[<Emit(
    "(function(el, cb) { var o = new IntersectionObserver(function(entries) { entries.forEach(function(e){ if (e.isIntersecting) cb(); }); }); o.observe(el); return function() { o.disconnect(); }; })($0, $1)"
)>]
let private observeIntersection (element: Browser.Types.HTMLElement) (onIntersect: unit -> unit) : (unit -> unit) =
    jsNative

/// Ein unsichtbarer Beobachter am Listenende. `active` steuert, ob gerade beobachtet
/// wird: waehrend eines laufenden Nachladens oder ohne weitere Seiten wird nicht
/// beobachtet, damit kein zweiter Aufruf ausgeloest wird, bevor der erste fertig ist.
[<ReactComponent>]
let private ScrollSentinel (onReachEnd: unit -> unit) (active: bool) =
    let elementRef = React.useRef<Browser.Types.HTMLElement option> None

    React.useEffect (
        (fun () ->
            match elementRef.current with
            | Some element when active ->
                let disconnect = observeIntersection element onReachEnd
                { new IDisposable with
                    member _.Dispose() = disconnect () }
            | _ ->
                { new IDisposable with
                    member _.Dispose() = () }),
        [| box active |]
    )

    Html.div [ prop.ref elementRef; prop.className "h-1" ]

// ============================================
// Tastatur (ledger-rjb8w)
// ============================================

[<Emit("(function(el) { el.scrollIntoView({ block: 'nearest' }); })($0)")>]
let private scrollIntoViewNearest (element: Browser.Types.Element) : unit = jsNative

/// Die Tastenbelegung am Dokument (PC): übersetzt Tastenereignisse über `keyCommandFor` in
/// Nachrichten. Ein Ereignis aus einem Eingabefeld oder mit Strg/Meta/Alt bleibt unberührt,
/// ebenso eines, das schon jemand behandelt hat. Folgt der Fokus einer anderen Zeile, rollt
/// die Liste sie ins Bild. Über einem offenen Regel-Formular gilt nur Esc, eine Stufe zurück (ledger-hg5m5).
[<ReactComponent>]
let private KeyboardNavigation (focusedRow: Guid option) (dispatch: Msg -> unit) (ruleForm: EntryRuleForm option) =
    let dispatchRef = React.useRef dispatch
    dispatchRef.current <- dispatch
    let ruleFormRef = React.useRef ruleForm
    ruleFormRef.current <- ruleForm

    React.useEffect (
        (fun () ->
            let handler (e: Browser.Types.Event) =
                let ke = e :?> Browser.Types.KeyboardEvent
                let target = ke.target :?> Browser.Types.HTMLElement
                let tagName = if isNull (box target) then "" else target.tagName
                let editable = not (isNull (box target)) && target.isContentEditable
                // Aus der Suche wirken die Pfeile auf die Liste (ADR 0087 Punkt 18, ledger-cnaz3).
                let inSearch =
                    not (isNull (box target)) && (target.closest ($"[data-testid='{ledgerSearchTestId}']")).IsSome
                if not ke.defaultPrevented then
                    let form = ruleFormRef.current
                    match ledgerKeyFor form.IsSome tagName editable inSearch (ke.ctrlKey || ke.metaKey) ke.altKey ke.shiftKey ke.key with
                    | Some (PageAction msg) ->
                        ke.preventDefault ()
                        dispatchRef.current msg
                    | Some RuleFormBack ->
                        ke.preventDefault ()
                        form |> Option.iter (fun f -> f.OnBack ())
                    | None -> ()

            Browser.Dom.document.addEventListener ("keydown", handler)
            { new IDisposable with
                member _.Dispose() = Browser.Dom.document.removeEventListener ("keydown", handler) }),
        [||]
    )

    React.useEffect (
        (fun () ->
            match focusedRow with
            | Some _ ->
                let element = Browser.Dom.document.querySelector "[data-keyboard-focus='true']"
                if not (isNull element) then scrollIntoViewNearest element
            | None -> ()),
        [| box focusedRow |]
    )

    Html.none

/// Ein Teil eines Eintrags der Tastenleiste: eine Taste (als Tastenkästchen) oder ein Wort.
type KeyHintPart =
    | HintKey of string
    | HintWord of string

/// Die Tastenleiste am PC (ledger-cnaz3, Prototyp Version 17 `.keys`), solange keine Auswahl läuft: die Einträge
/// des Prototyps und dazu „J K“, „Enter Detail“ und „⇧+G ungeprüft“ (BB). Rein, damit die Liste unter .NET
/// prüfbar ist.
let keyboardHintItems : KeyHintPart list list =
    [ [ HintKey "↑"; HintKey "↓"; HintKey "J"; HintKey "K"; HintWord "bewegen" ]
      [ HintKey "Enter"; HintWord "Detail" ]
      [ HintKey "G"; HintWord "geprüft" ]
      [ HintKey "⇧"; HintWord "+"; HintKey "G"; HintWord "ungeprüft" ]
      [ HintKey "L"; HintWord "Lena fragen" ]
      [ HintKey "Leertaste"; HintWord "oder"; HintKey "⇧"; HintWord "+"; HintKey "↓"; HintWord "auswählen" ]
      [ HintKey "/"; HintWord "suchen, Pfeile wirken auch dort" ]
      [ HintKey "Esc"; HintWord "schließen" ] ]

/// Ein Eintrag als Klartext (für Tests und den Vorleser): Tasten und Wörter mit Leerzeichen, „+“ ohne.
let keyboardHintItemText (item: KeyHintPart list) : string =
    item
    |> List.map (function HintKey k -> k | HintWord w -> w)
    |> String.concat " "
    |> fun text -> text.Replace(" + ", "+")

/// Die Leiste beginnt rechts von Rail (72 px) und Seitenleiste (232 px), design-system-k6kfw; ab 1024 px endet sie
/// am Panel (Panelbreite und der rechte Seitenrand `px-6`, ledger-cnaz3). Die Tasten stehen als Tastenkästchen (`ReviewCard.kbd`), die Wörter gedämpft.
let private keyboardHint () : ReactElement =
    Html.div [
        prop.custom ("data-testid", "ledger-keys")
        prop.className (
            $"hidden md:flex flex-wrap fixed bottom-0 left-0 {Tokens.NavWidths.fixedLeft} right-0 {Tokens.PanelWidths.fixedRight} z-30 items-center gap-x-4 gap-y-1.5 " +
            $"{Tokens.Backgrounds.surface} border-t {Tokens.Borders.subtle} px-5 py-2.5 {Tokens.FontSizes.xs} {Tokens.Colors.textMuted}"
        )
        prop.children [
            for item in keyboardHintItems ->
                Html.span [
                    prop.key (keyboardHintItemText item)
                    prop.className "inline-flex items-center gap-1 whitespace-nowrap"
                    prop.children [
                        for part in item ->
                            match part with
                            | HintKey k -> ReviewCard.kbd k
                            | HintWord w -> Html.span [ prop.text w ]
                    ]
                ]
        ]
    ]

// ============================================
// Seite
// ============================================

/// Die Karte der Handy-Liste mit der Wisch-Geste (ledger-th36h): Wisch nach rechts
/// markiert eine zu prüfende Buchung als geprüft, Wisch nach links bleibt unbelegt
/// (Handoff Ablauf 1, Screen 1.2). Eine schon geprüfte oder in YNAB gelöschte Zeile
/// bekommt keine Geste. Der barrierefreie Weg ist der Schalter im Detail.
let private swipeableEntryRow
    (dispatch: Msg -> unit)
    (accountNameOf: (Guid -> string) option)
    (rowState: ReviewMarkRowState option)
    (flatDate: bool)
    (entry: LedgerEntryView)
    =
    let row = entryRowWithReviewMark dispatch accountNameOf rowState None None flatDate entry
    if swipeAppliesTo entry then
        Html.div [
            prop.key (string entry.Id)
            prop.children [
                Swipe.SwipeableRowRight {
                    ActionLabel = swipeActionLabel
                    ActionIcon = Some (Icons.check Icons.XS Icons.Success)
                    ActionClass = "reviewed"
                    OnCommit = (fun () -> dispatch (StartSwipeReviewed entry.Id))
                    // Deckender Grund, damit die Aktionsfläche erst beim Wischen sichtbar wird.
                    Children = Html.div [ prop.className Tokens.Backgrounds.dark; prop.children [ row ] ]
                }
            ]
        ]
    else
        row

/// Wie lange ein Finger auf einer Zeile liegen muss, bis der Auswahl-Modus beginnt.
[<Literal>]
let private LongPressMs = 500

/// Wie weit sich der Finger bewegen darf, bevor das Drücken als Wischen oder Scrollen gilt.
[<Literal>]
let private LongPressSlopPx = 10.0

/// Die Hülle um eine Handy-Zeile, die langes Drücken erkennt (ledger-r1d2f). Nur Touch.
/// Bewegt sich der Finger über die Toleranz, ist es ein Wisch oder Scrollen und der
/// Zeitgeber fällt. Nach einem ausgelösten langen Drücken schluckt die Hülle den Klick, der
/// beim Loslassen folgen kann (sonst würde die eben gewählte Zeile gleich wieder abgewählt).
/// Die Hülle bleibt im Auswahl-Modus stehen (nur `enabled` fällt), damit dieser Klick
/// dieselbe Hülle trifft, die das Drücken erkannt hat.
[<ReactComponent>]
let private LongPressRow (enabled: bool) (onLongPress: unit -> unit) (child: ReactElement) =
    let timer = React.useRef<float option> None
    let startX = React.useRef 0.0
    let startY = React.useRef 0.0
    let fired = React.useRef false

    let cancel () =
        match timer.current with
        | Some handle ->
            Browser.Dom.window.clearTimeout handle
            timer.current <- None
        | None -> ()

    React.useEffectOnce (fun () -> { new IDisposable with member _.Dispose() = cancel () })

    Html.div [
        prop.className "row-longpress"
        prop.onPointerDown (fun e ->
            fired.current <- false
            cancel ()
            if enabled && e.pointerType = "touch" then
                startX.current <- e.clientX
                startY.current <- e.clientY
                timer.current <-
                    Some (
                        Browser.Dom.window.setTimeout (
                            (fun () ->
                                timer.current <- None
                                fired.current <- true
                                Viewport.vibrate 10
                                onLongPress ()),
                            LongPressMs
                        )
                    ))
        prop.onPointerMove (fun e ->
            if timer.current.IsSome then
                if abs (e.clientX - startX.current) > LongPressSlopPx || abs (e.clientY - startY.current) > LongPressSlopPx then
                    cancel ())
        prop.onPointerUp (fun _ -> cancel ())
        prop.onPointerCancel (fun _ -> cancel ())
        prop.onContextMenu (fun e -> e.preventDefault ())
        prop.custom (
            "onClickCapture",
            fun (e: Browser.Types.MouseEvent) ->
                if fired.current then
                    fired.current <- false
                    e.stopPropagation ()
                    e.preventDefault ()
        )
        prop.children [ child ]
    ]

/// Wie eine Zeile der Handy-Liste bedient wird. Im Auswahl-Modus mit Kästchen; mit Maus
/// (ledger-m6zpd) mit Kästchen, Markierung und Hover-Schaltfläche, ohne Wisch; mit einer
/// Auswahl ohne Modus (das Fenster ist mit bestehender Auswahl von der Tabelle auf die
/// Kompaktliste geschrumpft) weder Wisch noch Schaltfläche; sonst mit Wisch.
type MobileRowMode =
    | MobileSelectionRow
    | MobileMouseRow
    | MobilePlainRow
    | MobileSwipeRow

let mobileRowMode (selectionMode: bool) (mouse: bool) (selectionActive: bool) : MobileRowMode =
    if selectionMode then MobileSelectionRow
    elif mouse then MobileMouseRow
    elif selectionActive then MobilePlainRow
    else MobileSwipeRow

/// Startet langes Drücken den Auswahl-Modus? Nicht im Modus selbst und nicht mit Maus
/// (auch nicht am Touchscreen eines Laptops).
let mobileRowLongPress (selectionMode: bool) (mouse: bool) : bool = not selectionMode && not mouse

/// Eine Zeile der Handy-Liste: im Normalbetrieb mit Wisch-Geste, im Auswahl-Modus mit
/// Kästchen und ohne Wisch (ein Wisch wäre dort überraschend, ledger-r1d2f). In beiden Fällen
/// unter derselben Hülle für das lange Drücken.
/// Der Name in der Zeile am Platz einer gelöschten Buchung: der Payee, ohne Payee `None` („Buchung gelöscht“,
/// ledger-k68sd).
let deletedRowName (entry: LedgerEntryView) : string option =
    entry.Payee |> Option.filter (fun p -> not (String.IsNullOrWhiteSpace p))

/// Die Phase des DS-Bausteins zur Zeile am Platz. „Rückgängig“ bleibt auch nach einem Fehler (ledger-k68sd).
let deletedRowPhase (undo: Feedback.Act) (phase: DeletedRowPhase) : Feedback.RemovedPhase =
    match phase with
    | DeletedRowShown -> Feedback.RemovedPhase.Removed undo
    | DeletedRowRestoring -> Feedback.RemovedPhase.Restoring
    | DeletedRowRestoreFailed reason -> Feedback.RemovedPhase.RestoreFailed (reason, undo)

/// Die Zeile „„Payee“ gelöscht · Rückgängig“ am Platz der Buchung (`Feedback.removedRowIn`, Regel R3).
let private deletedEntryRow (dispatch: Msg -> unit) (row: DeletedEntryRow) : ReactElement =
    let undo : Feedback.Act = { Label = undoToastLabel; OnClick = fun () -> dispatch (StartRestoreDeletedEntry row.Entry.Id) }
    let phase = deletedRowPhase undo row.Phase
    match deletedRowName row.Entry with
    | Some name -> Feedback.removedRowIn name phase
    | None -> Feedback.removedRowUnnamed "Buchung" phase

/// Am Handy: die Zeile in der Liste, mit Abstand zu den Nachbarn (ledger-k68sd).
let private deletedEntryRowMobile (dispatch: Msg -> unit) (row: DeletedEntryRow) : ReactElement =
    Html.div [
        prop.key ("deleted-" + string row.Entry.Id)
        prop.custom ("data-testid", "ledger-deleted-row")
        prop.className $"border-b {Tokens.Borders.subtle} px-3 py-1.5"
        prop.children [ deletedEntryRow dispatch row ]
    ]

/// Am PC: eine Tabellenzeile über alle Spalten (ledger-k68sd).
let private deletedEntryTableRow (dispatch: Msg -> unit) (columnCount: int) (row: DeletedEntryRow) : ReactElement =
    Html.tr [
        prop.key ("deleted-" + string row.Entry.Id)
        prop.custom ("data-testid", "ledger-deleted-row")
        prop.children [
            Html.td [
                prop.colSpan columnCount
                prop.className $"{Tokens.ListRoles.cellPaddingX} py-1.5"
                prop.children [ deletedEntryRow dispatch row ]
            ]
        ]
    ]

/// Die Tagessumme ohne die Zeile am Platz: die gelöschte Buchung zählt nicht mehr (ledger-k68sd).
let daySumWithout (deletedId: Guid option) (includeDeleted: bool) (group: LedgerEntryView list) : Money =
    daySum includeDeleted (group |> List.filter (fun e -> Some e.Id <> deletedId))

let private mobileEntryRow
    (dispatch: Msg -> unit)
    (accountNameOf: (Guid -> string) option)
    (selectionMode: bool)
    (selectionActive: bool)
    (selection: Selection)
    (mouse: MouseRowInfo option)
    (rowState: ReviewMarkRowState option)
    (isFilteredOut: bool)
    (flatDate: bool)
    (entry: LedgerEntryView)
    =
    let inner =
        match mobileRowMode selectionMode mouse.IsSome selectionActive with
        | MobileSelectionRow ->
            entryRowWithReviewMark dispatch accountNameOf rowState (Some (isSelected selection entry.Id)) None flatDate entry
        | MobileMouseRow ->
            entryRowWithReviewMark dispatch accountNameOf rowState None mouse flatDate entry
        | MobilePlainRow ->
            entryRowWithReviewMark dispatch accountNameOf rowState None None flatDate entry
        | MobileSwipeRow ->
            swipeableEntryRow dispatch accountNameOf rowState flatDate entry
    let row = LongPressRow (mobileRowLongPress selectionMode mouse.IsSome) (fun () -> dispatch (StartSelectionMode entry.Id)) inner
    React.keyedFragment (
        string entry.Id,
        [ if isFilteredOut then
              // Ausgegraut (Filter-Nichttreffer): die Zeile bleibt stehen, der Vermerk nennt den Grund.
              Html.div [
                  prop.custom ("data-testid", "filtered-out-row")
                  // `text-muted` bei voller Deckkraft (`.filtered-out-text`): `opacity-50` fiel unter 4,5:1 (ledger-dn4vy).
                  prop.className "filtered-out-text"
                  prop.children [
                      row
                      Html.div [
                          prop.custom ("data-testid", "filtered-out-note")
                          prop.className $"px-4 pb-1.5 {Tokens.Backgrounds.dark} border-b {Tokens.Borders.subtle} {Tokens.ListRoles.muted}"
                          prop.text filteredOutNote
                      ]
                  ]
              ]
          else
              row ]
    )

let private entryListMobile
    (dispatch: Msg -> unit)
    (accountNameOf: (Guid -> string) option)
    (deletedRow: DeletedEntryRow option)
    (includeDeleted: bool)
    (reviewRows: Map<Guid, ReviewMarkRowState>)
    (selectionMode: bool)
    (selectionActive: bool)
    (selection: Selection)
    (alwaysVisible: bool)
    (listInteraction: ListInteraction)
    (anchor: Guid option)
    (focusedRow: Guid option)
    (filteredOut: Set<Guid>)
    (sort: LedgerSort)
    (entries: LedgerEntryView list)
    =
    // `listInteraction`: nur `CompactMouse` gibt der Zeile Kästchen und Markierung (ledger-m6zpd).
    // `alwaysVisible`: die kompakte Liste ersetzt die Tabelle auch ab `md`, wenn die
    // Tabellenbreite für die Mindestspalten nicht reicht (`visibleColumns`).
    let wrap (children: ReactElement list) =
        if alwaysVisible then Html.div children else Primitives.Responsive.mobileOnly children

    let deletedId = deletedRow |> Option.map (fun row -> row.Entry.Id)
    wrap [
        let row (flat: bool) (entry: LedgerEntryView) =
          match deletedRow with
          | Some deleted when deleted.Entry.Id = entry.Id -> deletedEntryRowMobile dispatch deleted
          | _ ->
            mobileEntryRow
                dispatch
                accountNameOf
                selectionMode
                selectionActive
                selection
                (if listInteraction = CompactMouse then
                     Some { Selected = isSelected selection entry.Id
                            IsAnchor = (anchor = Some entry.Id)
                            IsFocused = (focusedRow = Some entry.Id) }
                 else
                     None)
                (reviewRows |> Map.tryFind entry.Id)
                (filteredOut.Contains entry.Id)
                flat
                entry

        Html.div [
            prop.children (
                match dayGroups sort entries with
                | Some groups ->
                    groups
                    |> List.collect (fun (date, group) ->
                        dateGroupHeader date (daySumWithout deletedId includeDeleted group)
                        :: (group |> List.map (row false)))
                | None -> entries |> List.map (row true)
            )
        ]
    ]

/// Breite der Kästchen-Spalte der PC-Tabelle (Handoff Ablauf 3: 44px).
let selectColumnWidth = 44

let private dateColumnWidth = 96
/// Die Konto-Spalte nennt den Kontonamen (Prototyp Version 17 `ledgerDesk`, 110 px), gekürzt mit `title`.
let private accountColumnWidth = 110
let private amountColumnWidth = 132

/// Mindestbreiten der flexiblen Spalten (Richtwerte aus ledger-qh9qx). Keine sichtbare
/// Spalte wird schmaler; reicht die Breite nicht, fällt erst Memo, dann Konto weg.
let payeeMinWidth = 140
let categoryMinWidth = 120
let memoMinWidth = 120

/// Mindestbreite einer Spalte in Pixeln.
let minColumnWidth (column: LedgerColumn) : int =
    match column with
    | SelectColumn -> selectColumnWidth
    | DateColumn -> dateColumnWidth
    | AccountColumn -> accountColumnWidth
    | PayeeColumn -> payeeMinWidth
    | CategoryColumn -> categoryMinWidth
    | MemoColumn -> memoMinWidth
    | AmountColumn -> amountColumnWidth

/// Die Spaltenstufen von breit nach schmal (ADR 0049 bleibt Maßstab: ruhig statt eng):
/// alle Spalten, ohne Memo, ohne Memo und Konto. Das Konto fällt nur in der Gesamtsicht
/// weg, in einem Konto gibt es die Spalte ohnehin nicht.
let private columnStages (allowMemo: bool) (showAccountColumn: bool) : LedgerColumn list list =
    let withAccount (cols: LedgerColumn list) =
        if showAccountColumn then
            [ SelectColumn; DateColumn; AccountColumn ] @ cols
        else
            [ SelectColumn; DateColumn ] @ cols

    let tail = [ AmountColumn ]

    [ if allowMemo then withAccount ([ PayeeColumn; CategoryColumn; MemoColumn ] @ tail)
      withAccount ([ PayeeColumn; CategoryColumn ] @ tail)
      if showAccountColumn then
          [ SelectColumn; DateColumn; PayeeColumn; CategoryColumn ] @ tail ]

let private minTotal (columns: LedgerColumn list) : int =
    columns |> List.sumBy minColumnWidth

/// Reine Entscheidung: welche Spalten bei welcher Breite sichtbar sind. `tableWidth` ist
/// die gemessene Breite des Tabellencontainers in Pixeln, `showAccountColumn` die
/// Gesamtsicht. Die erste Stufe, deren Mindestbreiten (inklusive) in `tableWidth` passen,
/// gewinnt; passt keine, gilt `CompactList`. Den Überschuss über die Mindestbreiten
/// teilen Payee (40 %) und Kategorie (30 %) bevor Memo (oder, ohne Memo, die Kategorie)
/// den Rest nimmt.
let private visibleColumnsWith (allowMemo: bool) (tableWidth: float) (showAccountColumn: bool) : TableLayout =
    let stage =
        columnStages allowMemo showAccountColumn
        |> List.tryFind (fun columns -> float (minTotal columns) <= tableWidth)

    match stage with
    | None -> CompactList
    | Some columns ->
        let extra = tableWidth - float (minTotal columns)
        let hasMemo = List.contains MemoColumn columns

        let width (column: LedgerColumn) =
            match column with
            | PayeeColumn -> FixedPx (payeeMinWidth + int (floor (extra * 0.4)))
            | CategoryColumn when hasMemo -> FixedPx (categoryMinWidth + int (floor (extra * 0.3)))
            | CategoryColumn
            | MemoColumn -> Rest
            | other -> FixedPx (minColumnWidth other)

        Columns [ for column in columns -> column, width column ]

let visibleColumns (tableWidth: float) (showAccountColumn: bool) : TableLayout =
    visibleColumnsWith true tableWidth showAccountColumn

/// Reine Entscheidung (ADR 0053 Punkt 6, ledger-m6zpd): wie die Liste bedient wird. Die Tabelle
/// (`Columns`) ist immer `TableRows`. Die Kompaktliste ist `CompactMouse`, wenn der primäre
/// Zeiger fein ist (`pointer: fine`) und das Fenster mindestens `md` breit ist; sonst
/// `CompactTouch`. Unter `md` bleibt es Touch, weil PC-Leiste und Hinweis erst ab `md` da sind.
let listInteractionFor (layout: TableLayout) (finePointer: bool) (desktopViewport: bool) : ListInteraction =
    match layout with
    | Columns _ -> TableRows
    | CompactList -> if finePointer && desktopViewport then CompactMouse else CompactTouch

/// Was ein Bedienmodus der Liste zeigt und erlaubt.
type ListInteractionTraits =
    { CheckboxVisible: bool
      FocusMarkVisible: bool
      GesturesEnabled: bool
      FooterHint: bool }

let listInteractionTraits (interaction: ListInteraction) : ListInteractionTraits =
    match interaction with
    | TableRows ->
        { CheckboxVisible = true; FocusMarkVisible = true; GesturesEnabled = false; FooterHint = true }
    | CompactMouse ->
        { CheckboxVisible = true; FocusMarkVisible = true; GesturesEnabled = false; FooterHint = true }
    | CompactTouch ->
        { CheckboxVisible = false; FocusMarkVisible = false; GesturesEnabled = true; FooterHint = false }

/// Was das Panel der Liste bei dieser Fensterbreite an Breite nimmt: das Panel (`Tokens.PanelWidths.panelWidthFor`,
/// dieselbe Funktion setzt die CSS-Variable) und 4 px Abstand, damit die Filterleiste (`-mx-1`, Platz für den
/// Fokus-Ring) nicht unter das Panel reicht (Layout-Sweep, Invariante 6). Die Spalte hält ihn als
/// `Tokens.PanelWidths.columnInset` (design-system-gcpws).
let panelColumnPadding (windowWidth: float) : float =
    Tokens.PanelWidths.panelWidthFor windowWidth + Tokens.PanelWidths.columnInsetGap

/// Ab dieser Fensterbreite darf die Spalte Memo stehen (Roman, 2026-11-14, ledger-cnaz3).
let memoMinWindowWidth : float = 1500.0

/// Die Spalten am PC (ab 1024 px, ledger-cnaz3): rechts steht immer das Panel, die Tabelle bekommt die
/// gemessene Breite der Spalte abzüglich des Panels. Memo nur ab `memoMinWindowWidth` Fensterbreite und nur,
/// wenn die Stufe mit Memo neben dem Panel passt; darunter entfällt sie ohne Ersatz. Reicht die Breite für
/// keine Stufe, gilt die Kompaktliste.
let visibleColumnsBesidePanel (windowWidth: float) (columnWidth: float) (showAccountColumn: bool) : TableLayout =
    visibleColumnsWith (windowWidth >= memoMinWindowWidth) (columnWidth - panelColumnPadding windowWidth) showAccountColumn

/// Breite einer Spalte in Pixeln bei gegebener Tabellenbreite: `Rest` ist, was nach den
/// festen Spalten übrig bleibt.
let columnPixelWidth (tableWidth: float) (columns: (LedgerColumn * ColumnWidth) list) (column: LedgerColumn) : float option =
    let fixedPx = columns |> List.sumBy (fun (_, w) -> match w with FixedPx px -> float px | Rest -> 0.0)

    columns
    |> List.tryFind (fun (c, _) -> c = column)
    |> Option.map (fun (_, w) -> match w with FixedPx px -> float px | Rest -> tableWidth - fixedPx)

let private col (width: ColumnWidth) =
    Html.col [
        match width with
        | FixedPx px -> prop.style [ style.width (length.px px) ]
        | Rest -> ()
    ]

let private entryListDesktop
    (dispatch: Msg -> unit)
    (columns: (LedgerColumn * ColumnWidth) list)
    (isAllAccounts: bool)
    (accountAbbreviationOf: Guid -> string)
    (accountNameOf: Guid -> string)
    (deletedRow: DeletedEntryRow option)
    (includeDeleted: bool)
    (reviewRows: Map<Guid, ReviewMarkRowState>)
    (headerState: HeaderCheckState)
    (selection: Selection)
    (anchor: Guid option)
    (focusedRow: Guid option)
    (filteredOut: Set<Guid>)
    (sort: LedgerSort)
    (entries: LedgerEntryView list)
    =
    // Spalten vor dem Betrag: alle sichtbaren außer dem Betrag (der letzten).
    let visible = columns |> List.map fst
    let columnsBeforeAmount = List.length visible - 1

    let sortHeader (text: string) (align: string option) (field: LedgerSortField) =
        Table.calmSortableHeaderCell text align (headerSortDirection sort field) (fun () -> dispatch (SetSort field))

    let deletedId = deletedRow |> Option.map (fun row -> row.Entry.Id)
    let bodyRows (entry: LedgerEntryView) =
      match deletedRow with
      | Some deleted when deleted.Entry.Id = entry.Id -> deletedEntryTableRow dispatch (List.length visible) deleted
      | _ ->
        let rowState = reviewRows |> Map.tryFind entry.Id
        React.keyedFragment (
            string entry.Id,
            [ entryTableRow
                  dispatch
                  visible
                  isAllAccounts
                  accountAbbreviationOf
                  accountNameOf
                  (isSelected selection entry.Id)
                  (anchor = Some entry.Id)
                  (focusedRow = Some entry.Id)
                  (filteredOut.Contains entry.Id)
                  entry
              if filteredOut.Contains entry.Id then filteredOutNoteRow (List.length visible) entry
              match entryHintRow dispatch (List.length visible) rowState entry with
              | Some hintRow -> hintRow
              | None -> () ]
        )

    Primitives.Responsive.desktopOnly [
        Table.view
            // `FixedLayout`: feste Spaltenanteile statt Content-getriebener Breite
            // (AC5), sonst zwingt ein langer Payee/Kategorie-Text den Betrag aus dem
            // sichtbaren Bereich. Die Anteile stehen in einer `colgroup`: Payee ca.
            // 30 %, Kategorie ca. 26 %, Memo bekommt den Rest. So werden Inhalte wie
            // "Transfer : Tagesgeld ING" oder "Alltag: Essen/Einkaufen" nicht mehr
            // gekürzt, solange die Memo-Spalte leer ist (`ledger-n4vte`).
            // `Responsive = false`: `Sticky` and the wrapper's `overflow-x-auto`
            // don't mix (see `TableProps.Responsive` doc) — without this the sticky
            // header positions itself against the wrapper instead of the page and
            // ends up floating mid-list. `ClassName` reapplies the wrapper's own
            // decorative classes directly on the `<table>` so it keeps its border.
            // `Variant = Hover`: kein Zebra-Streifen (design-system-fq8dn, ADR 0031).
            { Table.defaultTableProps with
                Variant = Table.Hover
                Sticky = true
                FixedLayout = true
                Responsive = false
                ClassName = Some "rounded-xl border border-border-subtle bg-surface-card" }
            [
                Html.colgroup [ for (_, width) in columns -> col width ]
                Table.thead [
                    for column in visible do
                        match column, tableHeaderFor column with
                        | SelectColumn, _ -> selectHeaderCell dispatch headerState
                        | _, Some { HeaderText = label; HeaderAlign = align; HeaderSortField = Some field } -> sortHeader label align field
                        | _, Some { HeaderText = label; HeaderAlign = align; HeaderSortField = None } -> Table.calmHeaderCell label align
                        | _, None -> ()
                ]
                Table.tbody (
                    match dayGroups sort entries with
                    | Some groups ->
                        groups
                        |> List.collect (fun (date, group) ->
                            dateGroupRow columnsBeforeAmount date (daySumWithout deletedId includeDeleted group)
                            :: (group |> List.map bodyRows))
                    | None -> entries |> List.map bodyRows
                )
            ]
    ]

/// Das Konto für den Klartext-Filter: Kontoname oder "alle Konten".
let accountScopeText (accounts: LedgerAccountView list) (selected: AccountFilter option) : string =
    match selected with
    | Some (OneAccount id) ->
        accounts |> List.tryFind (fun a -> a.Id = id) |> Option.map (fun a -> a.Name) |> Option.defaultValue "alle Konten"
    | _ -> "alle Konten"

/// Text der Karte "Alles geprüft" nach dem letzten Prüfen (ledger-v9rq3).
let allReviewedText (scope: string) : string =
    if scope = "alle Konten" then "In allen Konten steht nichts mehr zu prüfen." else $"In {scope} steht nichts mehr zu prüfen."

/// Satz im leeren Zustand „Kein Treffer“ (ledger-d9z3n, §7.12).
let noMatchText = "Keine Buchung passt zu Suche und Filtern."

/// Der Knopf eines leeren Zustands; welche Aktion gilt, entscheidet `emptyListAction` (ledger-rb3xw).
let private emptyListButton (dispatch: Msg -> unit) (fetch: unit -> unit) (kind: EmptyListKind) : ReactElement option =
    emptyListAction kind
    |> Option.map (function
        | EmptyListAction.ClearFilters -> Button.secondary "Filter löschen" (fun () -> dispatch ClearAllFilters)
        | EmptyListAction.Fetch -> Button.secondary "Abrufen" fetch)

let private entryList
    (dispatch: Msg -> unit)
    (fetch: unit -> unit)
    (accounts: LedgerAccountView list)
    (isAllAccounts: bool)
    (handyForm: bool)
    (layout: TableLayout)
    (model: Model)
    =
    let abbreviationOf =
        let byId =
            accounts |> List.map (fun a -> a.Id, accountAbbreviation a.Name) |> Map.ofList
        fun (id: Guid) -> byId |> Map.tryFind id |> Option.defaultValue "?"
    // Die Handy-Zeile und die Konto-Spalte der Tabelle nennen das Konto beim Namen (ledger-d9z3n, ledger-cnaz3); das
    // Kürzel steht nur vor dem Payee, wenn die Konto-Spalte fehlt.
    let accountNameOf =
        let byId = accounts |> List.map (fun a -> a.Id, a.Name) |> Map.ofList
        fun (id: Guid) -> byId |> Map.tryFind id |> Option.defaultValue "?"

    // Die gelöschte Buchung steht als Zeile an ihrem Platz, auch wenn das Nachladen sie aus der Liste nahm (ledger-k68sd).
    match model.Entries |> RemoteData.map (Components.Ledger.State.withDeletedPlaced model.DeletedRow) with
    | NotAsked ->
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Noch kein Konto"
            "Sobald ein Spiegel-Lauf gelaufen ist, stehen hier die Konten und ihre Buchungen."
            (Some (Button.primary "Jetzt spiegeln" (fun () -> dispatch StartMirror)))

    | Loading when handyForm -> Loading.txListSkeleton 6

    | Loading ->
        Html.div [
            Primitives.Responsive.mobileOnly [ Loading.txListSkeleton 6 ]
            Primitives.Responsive.desktopOnly [ Loading.tableSkeleton 6 6 ]
        ]

    | Failure message ->
        ErrorDisplay.cardWithTitle
            "Buchungen konnten nicht geladen werden"
            message
            (Some (fun () -> dispatch LoadEntries))

    | Success [] when emptyListKind model.OnlyNeedsReview model.Filters = EmptyListKind.AllReviewed ->
        // Nach dem letzten Prüfen fehlt nichts: kein "Jetzt spiegeln" (ledger-v9rq3).
        let scope = accountScopeText accounts model.SelectedFilter
        // Eine Aufgabenliste, die leer ist, weil alles erledigt ist (§5 Card, ledger-ef068).
        Card.allDone "ledger-all-reviewed" "Alles geprüft" (allReviewedText scope)

    | Success [] when emptyListKind model.OnlyNeedsReview model.Filters = EmptyListKind.AllCategorized ->
        let scope = accountScopeText accounts model.SelectedFilter
        Card.allDone
            "ledger-all-categorized"
            "Alles kategorisiert"
            (if scope = "alle Konten" then "In allen Konten hat jede Buchung eine Kategorie." else $"In {scope} hat jede Buchung eine Kategorie.")

    | Success [] when emptyListKind model.OnlyNeedsReview model.Filters = EmptyListKind.NoMatch ->
        // AC "Leerzustand ohne Treffer": nennt eine nächste Handlung, statt nur
        // Leere zu melden. „Filter löschen“ nimmt alle Filter und die Suche zurück, das Konto bleibt
        // (ledger-d9z3n, §7.12). Ohne jeden Filter (Zweig darunter) bietet der Zustand den Abruf an.
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Kein Treffer"
            noMatchText
            (emptyListButton dispatch fetch EmptyListKind.NoMatch)

    | Success [] ->
        // Kein Filter, das Konto hat keine Buchungen: §7.12 wörtlich, „Abrufen“ ist derselbe Abruf wie im Kopf
        // (`LedgerTabs.Fetch`, ADR 0093, ledger-rb3xw).
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Keine Buchungen"
            "Buchungen erscheinen hier nach dem nächsten Import."
            (emptyListButton dispatch fetch EmptyListKind.NoEntries)

    | Success entries ->
        // Die Spalten wählt der Aufrufer (`FilterBarColumn`): am PC neben dem Panel, das immer steht (ledger-cnaz3).
        let sort = Components.Ledger.State.effectiveSort model

        Html.div [
            entryListMobile
                dispatch
                (if isAllAccounts then Some accountNameOf else None)
                model.DeletedRow
                model.IncludeDeleted
                model.ReviewMarkRows
                model.SelectionMode
                (hasSelection model)
                (selectionForRows model)
                (layout = CompactList)
                model.ListInteraction
                model.SelectionAnchor
                model.FocusedRow
                model.FilteredOut
                sort
                entries
            match layout with
            | CompactList -> ()
            | Columns columns ->
                entryListDesktop
                    dispatch
                    columns
                    isAllAccounts
                    abbreviationOf
                    accountNameOf
                    model.DeletedRow
                    model.IncludeDeleted
                    model.ReviewMarkRows
                    (headerCheckState model)
                    (selectionForRows model)
                    model.SelectionAnchor
                    model.FocusedRow
                    model.FilteredOut
                    sort
                    entries

            if nextPageLoading model then
                Html.div [
                    prop.className "mt-2"
                    prop.children [
                        Primitives.Responsive.mobileOnly [ Loading.txListSkeleton 2 ]
                        Primitives.Responsive.desktopOnly [ Loading.tableSkeleton (if isAllAccounts then 7 else 6) 2 ]
                    ]
                ]

            if model.HasMoreEntries then
                ScrollSentinel (fun () -> dispatch LoadMoreEntries) (model.HasMoreEntries && model.ListActivity = ListIdle)
        ]

// ============================================
// Filterleiste und Summenzeile (ledger-j8xq4)
// ============================================
// Chips, Kategorienauswahl mit Mehrfachauswahl (Click-Commit, ADR 0005/Styleguide
// 7.1), Zeitraum-Schnellwahl, Payee-Vorschläge, Memo-/Betrags-Suchfelder und die
// Summenzeile über der Liste. Beide Geräte teilen sich denselben Auswahl-Inhalt
// (`*PickerBody`); nur die Chrome darum unterscheidet sich: `FilterPopover` am PC
// (`filterChrome`), das Filter-Sheet am Handy (`filterSheet`). Welche Form gerendert
// wird, entscheidet `Viewport.isInspectorPanelViewport()` zur Renderzeit, NICHT der übliche
// CSS-Doppel-Render-Trick (`Primitives.Responsive.mobileOnly`/`desktopOnly`): ein
// `BottomSheet` portalt immer nach `document.body`, unabhängig von einem
// versteckten Wrapper um die Aufrufstelle, ein doppelter Render würde also zwei
// echte Overlays für denselben Zustand erzeugen.

/// Dünner Wrapper um `CategoryMultiSelect.view` (herausgezogen in design-system-bz0gn):
/// baut den Props-Record aus Model und dispatch.
let private categoryPickerBody (dispatch: Msg -> unit) (model: Model) (categories: YnabCategory list) =
    CategoryMultiSelect.view {
        Categories = categories
        Selected = model.CategoryDraft
        Search = model.CategorySearch
        HiddenExpanded = model.HiddenCategoriesExpanded
        Recent =
            model.RecentCategories
            |> List.choose (fun g -> categories |> List.tryFind (fun c -> let (YnabCategoryId id) = c.Id in id = g))
        ApplyDisabled = categoryApplyDisabled model.CategoryDraft model.Filters.CategoryIds
        OnToggle = fun guid -> dispatch (ToggleCategoryDraft guid)
        OnSearchChange = fun text -> dispatch (SetCategorySearch text)
        OnToggleHiddenExpanded = fun () -> dispatch ToggleHiddenCategoriesExpanded
        OnReset = fun () -> dispatch ResetCategoryDraft
        OnApply = fun () -> dispatch ApplyCategoryFilter
    }

/// Ein Knopf der Schnellwahl Zeitraum (`Segmented`, ledger-dn4vy): gewählt mit `aria-pressed`.
let private dateQuickSegment (dispatch: Msg -> unit) (model: Model) (quick: DateQuick) : Segmented.Segment =
    Segmented.segment (dateQuickLabel quick) (model.DateDraft.Quick = Some quick) (fun () -> dispatch (SetDateQuick quick))

/// Feldbeschriftung der Filter-Popover: 12px gedämpft (`ListRoles.muted`)
let private fieldLabel (label: string) (child: ReactElement) : ReactElement =
    Html.div [
        prop.className "space-y-1.5"
        prop.children [
            Html.label [ prop.className Tokens.ListRoles.muted; prop.text label ]
            child
        ]
    ]

let private dateDraftInput
    (label: string)
    (value: DateTime option)
    (invalid: bool)
    (onChange: DateField.DateEntry -> unit)
    =
    fieldLabel label (
        DateField.view { Value = value; OnChange = onChange; Invalid = invalid; Disabled = false; ReadOnly = false; Label = label }
    )

/// Die Zeitraum-Auswahl (AC: Schnellwahl trägt Von/Bis sichtbar ein).
let private datePickerBody (dispatch: Msg -> unit) (model: Model) =
    Html.div [
        prop.className "flex flex-col gap-4"
        prop.children [
            // Der Kopf wie beim Sortieren (ledger-ef068).
            BottomSheet.sectionTitle "Schnellwahl"
            // Zwei Reihen: vier Texte nebeneinander passen bei 320 px nicht sicher (ledger-dn4vy).
            Segmented.viewColumns
                "date-quick"
                "Schnellwahl"
                Segmented.TwoColumns
                [ for quick in [ ThisMonth; LastMonth; ThisYear; Last12Months ] -> dateQuickSegment dispatch model quick ]
            // Am schmalen Handy untereinander: nebeneinander schnitte das Feld den Platzhalter ab (infrastructure-nx14s).
            Html.div [
                prop.className "grid grid-cols-1 gap-3 sm:grid-cols-2 md:grid-cols-1"
                prop.children [
                    dateDraftInput "Von" model.DateDraft.From model.DateDraftFromInvalid (fun v -> dispatch (SetDateDraftFrom v))
                    dateDraftInput "Bis" model.DateDraft.To model.DateDraftToInvalid (fun v -> dispatch (SetDateDraftTo v))
                ]
            ]
            Primitives.HStack.spaceBetween [
                Button.ghost "Zurücksetzen" (fun () -> dispatch ResetDateDraft)
                PickerParts.applyButtonWhen (dateDraftHasError model) 0 (fun () -> dispatch ApplyDateFilter)
            ]
        ]
    ]

/// Betragsfeld des Filters mit sichtbarem Euro-Hinweis rechts im Feld (ledger-dn4vy): Zifferntastatur
/// (`decimal`), Mono, 48 px und 16 px am Handy (`Input.Medium`), benannt als „Betrag von“ bzw. „Betrag bis“.
/// Ein unlesbarer Text gilt sofort beim Tippen als ungültig und nennt den Grund am Feld.
let private amountField (name: string) (value: string) (onChange: string -> unit) (placeholder: string) : ReactElement =
    let invalid = amountFilterTextInvalid value
    let hintId = "amount-filter-hint-" + (name.Replace(" ", "-").ToLowerInvariant())
    Html.div [
        prop.className "space-y-1"
        prop.children [
            Html.div [
                prop.className "relative"
                prop.children [
                    Input.text { Input.textInputDefaults with
                                    Value = value
                                    OnChange = onChange
                                    Placeholder = placeholder
                                    InputMode = Some "decimal"
                                    AriaLabel = Some name
                                    State = (if invalid then Input.Error Capture.amountInvalidText else Input.Normal)
                                    AriaInvalid = invalid
                                    DescribedBy = (if invalid then Some hintId else None)
                                    ClassName = Some (Input.amountFieldClass false invalid + " pr-8") }
                    Html.span [
                        prop.className $"absolute inset-y-0 right-3 flex items-center pointer-events-none {Tokens.ListRoles.muted}"
                        prop.text "€"
                    ]
                ]
            ]
            if invalid then
                Html.div [ prop.id hintId; prop.children [ ErrorDisplay.inline' Capture.amountInvalidText ] ]
        ]
    ]

/// Der Umschalter „Alle | Ausgaben | Einnahmen“ unter dem Betrag (ADR 0095): die Felder nehmen kein
/// Vorzeichen an, die Richtung kommt von hier. Wirkt sofort und auch ohne Betrag.
let private amountDirectionSegments (testId: string) (direction: AmountDirection) (dispatch: Msg -> unit) : ReactElement =
    Segmented.view
        testId
        "Richtung"
        [ for value, label in directionOptions ->
              Segmented.segment label (direction = value) (fun () -> dispatch (SetAmountDirection value)) ]

/// Das Betrags-Suchfeld, Von/Bis (AC: Suchfeld mit kurzer Verzögerung beim Tippen), darunter die Richtung.
let private amountPickerBody (dispatch: Msg -> unit) (model: Model) =
    Html.div [
        prop.className "space-y-3"
        prop.children [
            Html.div [
                prop.className "grid grid-cols-2 gap-3"
                prop.children [
                    fieldLabel "Von" (amountField "Betrag von" model.AmountMinDraft (fun text -> dispatch (SetAmountMinDraft text)) "z. B. 5")
                    fieldLabel "Bis" (amountField "Betrag bis" model.AmountMaxDraft (fun text -> dispatch (SetAmountMaxDraft text)) "z. B. 50")
                ]
            ]
            amountDirectionSegments "amount-filter-direction" model.Filters.AmountDirection dispatch
        ]
    ]

let private chipTitle (chip: ChipId) : string =
    match chip with
    | NeedsReviewChip -> ""
    | AskLenaChip -> ""
    | CategoryChip -> "Kategorie"
    | WithoutCategoryChip -> "Ohne Kategorie"
    | DateChip -> "Zeitraum"
    | AmountChip -> "Betrag"

/// Breite des PC-Popovers: Kategorie 460px, sonst 320px (K5).
let popoverWidth (chip: ChipId) : string =
    match chip with
    | CategoryChip -> FilterPopover.categoryWidthClass
    | _ -> FilterPopover.defaultWidthClass

let private popoverBody (dispatch: Msg -> unit) (model: Model) (categories: YnabCategory list) (chip: ChipId) : ReactElement =
    match chip with
    | NeedsReviewChip
    | AskLenaChip
    | WithoutCategoryChip -> Html.none
    | CategoryChip -> categoryPickerBody dispatch model categories
    | DateChip -> datePickerBody dispatch model
    | AmountChip -> amountPickerBody dispatch model

/// Die Chrome um einen Popover-Inhalt am PC: `FilterPopover`. Die Chipleiste steht nur
/// ab 1024 px (`filterChipBar`), darunter stehen Kategorie und Zeitraum im Filter-Sheet
/// an Stelle der Filter (ledger-npqcb, `filterSheet`).
let private filterChrome (dispatch: Msg -> unit) (chip: ChipId) (isOpen: bool) (body: ReactElement) : ReactElement =
    FilterPopover.view
        { IsOpen = isOpen; OnClose = (fun () -> dispatch CloseFilterPopover); WidthClassName = popoverWidth chip }
        [ body ]

let private categoryChipLabel (filters: Filters) : string = $"Kategorie: {Set.count filters.CategoryIds}"

let private dateChipLabel (filters: Filters) : string =
    filters.DateRange
    |> Option.bind (fun r -> r.Quick)
    |> Option.map dateQuickLabel
    |> Option.defaultValue "Zeitraum"

let chipLabel (model: Model) (chip: ChipId) : string =
    match chip with
    | NeedsReviewChip -> needsReviewChipLabel model.NeedsReviewCount
    | AskLenaChip -> askLenaFilterLabel
    | CategoryChip -> if hasCategoryFilter model.Filters then categoryChipLabel model.Filters else "Kategorie"
    | WithoutCategoryChip -> "Ohne Kategorie"
    | DateChip -> if hasDateFilter model.Filters then dateChipLabel model.Filters else "Zeitraum"
    | AmountChip -> "Betrag"

let private isChipActive (model: Model) (chip: ChipId) : bool =
    match chip with
    | NeedsReviewChip -> model.OnlyNeedsReview
    | AskLenaChip -> model.Filters.OnlyAskLena
    | CategoryChip -> hasCategoryFilter model.Filters
    | WithoutCategoryChip -> model.Filters.WithoutCategory
    | DateChip -> hasDateFilter model.Filters
    | AmountChip -> hasAmountFilter model.Filters || hasDirectionFilter model.Filters

let private removeChipMsg (chip: ChipId) : Msg option =
    match chip with
    | NeedsReviewChip -> None
    | AskLenaChip -> None
    | CategoryChip -> Some RemoveCategoryFilter
    | WithoutCategoryChip -> None
    | DateChip -> Some RemoveDateFilter
    | AmountChip -> Some RemoveAmountAndDirection

let private chipOnClick (dispatch: Msg -> unit) (chip: ChipId) : unit -> unit =
    match chip with
    | NeedsReviewChip -> fun () -> dispatch ToggleOnlyNeedsReview
    | AskLenaChip -> fun () -> dispatch ToggleOnlyAskLena
    | WithoutCategoryChip -> fun () -> dispatch ToggleWithoutCategory
    | other -> fun () -> dispatch (OpenFilterPopover other)

/// Der Text eines Chips der Filterleiste am PC (ledger-cnaz3, Prototyp Version 17): die Umschalter ohne Zahl (sie
/// steht klein dahinter, `pcChipCount`), `askLenaFilterLabel` für den Lena-Filter, die Kategorie mit Namen.
let pcChipLabel (model: Model) (categoryNames: string list) (chip: ChipId) : string =
    match chip with
    | NeedsReviewChip -> "Zu prüfen"
    | AskLenaChip -> askLenaFilterLabel
    | WithoutCategoryChip -> "Ohne Kategorie"
    | CategoryChip -> categoryChipText categoryNames
    | DateChip
    | AmountChip -> chipLabel model chip

/// Die Zahl hinter einem Umschalter am PC: „Zu prüfen 4“, „Lena 2“, „Ohne Kategorie 1“.
let pcChipCount (model: Model) (chip: ChipId) : int option =
    match chip with
    | NeedsReviewChip -> Some model.NeedsReviewCount
    | AskLenaChip -> Some model.AskLenaCount
    | WithoutCategoryChip -> Some model.WithoutCategoryCount
    | CategoryChip
    | DateChip
    | AmountChip -> None

/// Ein Chip samt seiner Chrome (Sheet/Popover), wenn er eine eigene Auswahl hat.
let private filterChip (dispatch: Msg -> unit) (model: Model) (categories: YnabCategory list) (chip: ChipId) : ReactElement =
    let chipElement =
        ChipBar.chipWith (pcChipCount model chip) (chip = CategoryChip) {
            Label = pcChipLabel model (selectedCategoryNames categories model.Filters.CategoryIds) chip
            IsActive = isChipActive model chip
            OnClick = chipOnClick dispatch chip
            OnRemove = removeChipMsg chip |> Option.map (fun msg -> fun () -> dispatch msg)
        }
    match chip with
    | NeedsReviewChip
    | AskLenaChip
    | WithoutCategoryChip -> chipElement
    | _ ->
        let isOpen = model.OpenPopover = Some chip
        ChipBar.chipWithPopover chipElement (filterChrome dispatch chip isOpen (popoverBody dispatch model categories chip))

/// Der Sortier-Chip steht nur, wenn die Liste als Handy-Liste gerendert wird.
let sortChipVisible (layout: TableLayout) : bool = layout = CompactList

/// Der Sortier-Chip am Ende der Filterleiste samt Sheet (ledger-skpcx). Er steht nur,
/// wenn die Liste als Handy-Liste gerendert wird (`visibleColumns` liefert `CompactList`);
/// dann ist er die einzige Chrome dieser Art (ADR 0043). Aktiv gefärbt, wenn die wirksame
/// Sortierung von der Standardsortierung abweicht. Ohne "×", kein Teil von `hasAnyFilter`.
/// Die Optionen der Sortierung, nach Feld gruppiert: Inhalt des Sortier-Sheets am Sortier-Chip und der Auswahl
/// „Sortieren“ im Filter-Sheet (ledger-npqcb).
let private sortOptionRows (dispatch: Msg -> unit) (model: Model) (isAllAccounts: bool) : ReactElement list =
    let sort = Components.Ledger.State.effectiveSort model
    let options = Components.Ledger.State.sortOptions isAllAccounts
    [ for (field, group) in options |> List.groupBy (fun ((s: LedgerSort), _) -> s.Field) do
          React.keyedFragment (
              Components.Ledger.State.sortFieldName field,
              [ BottomSheet.sectionTitle (Components.Ledger.State.sortFieldName field)
                for (optionSort, label) in group do
                    React.keyedFragment (
                        label,
                        [ BottomSheet.optionRow label (optionSort = sort) (fun () -> dispatch (SetSortExactly optionSort)) ]
                    ) ]
          ) ]

let private sortSheet (dispatch: Msg -> unit) (model: Model) (isAllAccounts: bool) : ReactElement =
    // Aus dem Filter-Sheet heraus steht die Sortierung in diesem (ledger-npqcb); hier nur der Sortier-Chip.
    BottomSheet.view
            { IsOpen = model.SortSheetOpen && not model.FilterSheetOpen
              OnClose = (fun () -> dispatch CloseSortSheet)
              Title = "Sortieren"
              Subtitle = None
              Footer = None }
            (sortOptionRows dispatch model isAllAccounts)

let private sortChip (dispatch: Msg -> unit) (model: Model) (isAllAccounts: bool) : ReactElement =
    let sort = Components.Ledger.State.effectiveSort model
    let chipElement =
        ChipBar.chip {
            Label = Components.Ledger.State.sortChipLabel sort
            IsActive = Components.Ledger.State.sortChipActive sort
            OnClick = (fun () -> dispatch OpenSortSheet)
            OnRemove = None
        }
    ChipBar.chipWithPopover chipElement (sortSheet dispatch model isAllAccounts)

/// Das Suchfeld (ledger-d9z3n): Teiltext in Payee, Memo und Kategoriename, mit Verzögerung beim Tippen.
let private ledgerSearchField (dispatch: Msg -> unit) (model: Model) : ReactElement =
    Input.searchField ledgerSearchTestId "Buchungen durchsuchen" "Suchen…" model.SearchDraft (fun text -> dispatch (SetSearchDraft text))

/// Die Filterleiste selbst: Reihenfolge und "Alle Filter löschen" (AC: „Alle Filter
/// löschen" sitzt am Ende, erscheint nur, wenn mindestens ein Filter aktiv ist).
/// `elementRef` (dritte Iteration, seit der vierten Iteration ein
/// Callback-Ref statt eines Objekt-Refs, siehe `ChipBar.bar`): an
/// `ChipBar.bar` durchgereicht, damit `FilterBarColumn` unten die
/// tatsächlich gerenderte Höhe messen kann.
let private filterChipBar
    (dispatch: Msg -> unit)
    (model: Model)
    (categories: YnabCategory list)
    (showSortChip: bool)
    (isAllAccounts: bool)
    (elementRef: (Browser.Types.Element -> unit) option)
    : ReactElement =
    // Prototyp Version 17 `ledgerDesk`: die Chips, „Filter löschen“ als Chip, sobald etwas gesetzt ist, und das
    // Suchfeld rechtsbündig am Ende (ledger-cnaz3). Die Leiste bricht um.
    ChipBar.bar
        elementRef
        ((fixedChipOrder |> List.map (filterChip dispatch model categories))
         @ (if showSortChip then [ sortChip dispatch model isAllAccounts ] else [])
         @ (if clearFiltersChipVisible model.OnlyNeedsReview model.Filters then
                [ ChipBar.chip { Label = "Filter löschen"; IsActive = false; OnClick = (fun () -> dispatch ClearAllFilters); OnRemove = None } ]
            else [])
         @ [ Input.searchFieldCompact ledgerSearchTestId "Buchungen durchsuchen" "Suchen…" model.SearchDraft (fun text -> dispatch (SetSearchDraft text)) ])

// ============================================
// Kassenbuch unter 1024 px: Kontowahl, Suche, Filter-Knopf, gesetzte Filter (ledger-d9z3n)
// ============================================

/// Der Saldo neben dem Kontonamen im Auswahlfeld: Mono, klein, gedämpft (Prototyp), negativ Rot (§2). Beim Laden und nach
/// einem Fehler steht nichts (die Zeile „Kontostände konnten nicht geladen werden“ nennt den Fehler).
let private accountFieldBalance (balance: CardBalance) : ReactElement option =
    match balance with
    | BalanceShown money ->
        Some (Money.balanceInline "ledger-account-field-balance" true $"{Tokens.FontSizes.xs} whitespace-nowrap" money.Amount money.Currency)
    | BalancePending
    | BalanceUnavailable -> None

/// Die eine Kontowahl unter 1024 px (ledger-d9z3n, §7.5): ein Auswahlfeld „Konto“ mit Saldo, das ein Sheet
/// „Konto“ öffnet: „Alle Konten“ mit Gesamtsaldo, danach jedes Konto mit Saldo. Ein Tipp wählt und schließt.
let private accountField
    (dispatch: Msg -> unit)
    (model: Model)
    (accounts: LedgerAccountView list)
    (balances: RemoteData<LedgerAccountBalanceView list>)
    : ReactElement =
    if List.isEmpty accounts then
        Html.none
    else
        let cards = buildAccountCards accounts balances
        let active = cards |> List.tryFind (fun c -> Some c.Filter = model.SelectedFilter)
        React.fragment [
            Input.selectField
                "ledger-account-field"
                "Konto"
                (active |> Option.map (fun c -> c.Label) |> Option.defaultValue "Alle Konten")
                (active |> Option.bind (fun c -> accountFieldBalance c.Balance))
                model.AccountSheetOpen
                (fun () -> dispatch OpenAccountSheet)
            BottomSheet.view
                { IsOpen = model.AccountSheetOpen
                  OnClose = (fun () -> dispatch CloseAccountSheet)
                  Title = "Konto"
                  Subtitle = None
                  Footer = None }
                [ Html.div [
                    prop.className $"flex flex-col overflow-hidden rounded-xl border {Tokens.Borders.subtle} {Tokens.Backgrounds.dark}"
                    prop.custom ("data-testid", "ledger-account-sheet")
                    prop.children [
                        for card in cards ->
                            let isActive = model.SelectedFilter = Some card.Filter
                            React.keyedFragment (
                                card.Label,
                                [ Money.accountChoiceRow
                                      "ledger-account-choice"
                                      card.Label
                                      // Saldo in Primärtext auf jeder Zeile, negativ rot (Prototyp).
                                      (cardBalanceElement true card.Balance)
                                      isActive
                                      (fun () -> dispatch (SelectFilter card.Filter)) ]
                            )
                    ]
                  ] ]
        ]

/// Die Werte der Zeilen im Filter-Sheet: Kategorie „alle“ oder die gewählten Namen, Zeitraum „alle“ oder kurz.
let filterSheetCategoryValue (categoryNames: string list) (selectedCount: int) : string =
    match categoryNames with
    | [] when selectedCount = 0 -> "alle"
    | [] -> $"{selectedCount} gewählt"
    | names -> String.Join(", ", names)

let filterSheetDateValue (filters: Filters) : string =
    filters.DateRange |> Option.map dateRangeShortText |> Option.defaultValue "alle"

/// Der Text des Hauptknopfs im Fuß des Filter-Sheets: „n zeigen“ mit der Trefferzahl, beim Laden nur „Zeigen“.
let filterSheetShowLabel (entries: RemoteData<LedgerEntryView list>) (totalCount: int) : string =
    match entries with
    | Success _ -> $"{Money.formatCount totalCount} zeigen"
    | _ -> "Zeigen"

/// Das Filter-Sheet (ledger-d9z3n, Prototyp `sheetHtml` Zweig `filter`): Schalter „Zu prüfen“, „Lena“,
/// „Ohne Kategorie“ mit Anzahl, Zeilen „Kategorie“ und „Zeitraum“, „Betrag von“ und „bis“, Zeile „Sortieren“ und
/// „Gelöschte zeigen“, wenn es gelöschte Buchungen gibt. Jede Änderung wirkt sofort. Kategorie, Zeitraum und
/// Sortieren ersetzen den Inhalt des Sheets, mit „Zurück“ oben (ledger-npqcb, §7.15, `filterSheetView`).
/// Der Knopf unten links im Filter-Sheet heißt wie der Chip „Filter löschen“, ein Wort für dieselbe Wirkung
/// (Roman, 2026-11-15).
let filterSheetClearLabel = "Filter löschen"

/// „Zurück“ oben in einer Auswahl des Filter-Sheets (ledger-npqcb): zu den Filtern, ohne Änderung.
let private filterSheetBack (dispatch: Msg -> unit) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "filter-sheet-back")
        prop.className "mb-2"
        prop.children [ PageHeader.backButtonFor (Some "Zurück zu den Filtern") (fun () -> dispatch BackToFilterSheet) ]
    ]

let private filterSheet (dispatch: Msg -> unit) (model: Model) (categories: YnabCategory list) (isAllAccounts: bool) : ReactElement =
    // Die Auswahlen wählen auf dem echten `click` (Übernehmen, `optionRow`), danach folgt kein synthetischer Klick
    // mehr, der die Filter an derselben Stelle treffen könnte.
    let view = Components.Ledger.State.filterSheetView model
    let names = selectedCategoryNames categories model.Filters.CategoryIds
    let sort = Components.Ledger.State.effectiveSort model
    let footer =
        Html.div [
            prop.className "grid w-full grid-cols-2 gap-3"
            prop.children [
                Button.view
                    { Button.defaultProps with
                        Text = filterSheetClearLabel
                        Variant = Button.Outline
                        ClassName = Some "w-full"
                        OnClick = fun () -> dispatch ClearAllFilters }
                Button.view
                    { Button.defaultProps with
                        Text = filterSheetShowLabel model.Entries model.TotalCount
                        Variant = Button.Primary
                        ClassName = Some "w-full"
                        OnClick = fun () -> dispatch CloseFilterSheet }
            ]
        ]
    let choice (testId: string) (body: ReactElement) =
        Html.div [
            prop.custom ("data-testid", testId)
            prop.children [ filterSheetBack dispatch; body ]
        ]
    BottomSheet.view
        { IsOpen = view <> FilterSheetClosed
          OnClose = (fun () -> dispatch CloseFilterSheet)
          Title =
            (match view with
             | FilterSheetCategory -> chipTitle CategoryChip
             | FilterSheetDate -> chipTitle DateChip
             | FilterSheetSort -> "Sortieren"
             | FilterSheetClosed
             | FilterSheetFilters -> "Filter")
          Subtitle = None
          Footer = (if view = FilterSheetFilters then Some [ footer ] else None) }
        [ match view with
          | FilterSheetCategory -> choice "filter-sheet-category" (categoryPickerBody dispatch model categories)
          | FilterSheetDate -> choice "filter-sheet-date" (datePickerBody dispatch model)
          | FilterSheetSort ->
              choice "filter-sheet-sort" (Html.div [ prop.children (sortOptionRows dispatch model isAllAccounts) ])
          | FilterSheetClosed
          | FilterSheetFilters ->
              Html.div [
                prop.custom ("data-testid", "ledger-filter-sheet")
                prop.className "flex flex-col"
                prop.children [
                    Input.countSwitchRow "filter-needs-review" "Zu prüfen" (Some model.NeedsReviewCount) model.OnlyNeedsReview (fun _ -> dispatch ToggleOnlyNeedsReview)
                    Input.countSwitchRow "filter-ask-lena" askLenaFilterLabel (Some model.AskLenaCount) model.Filters.OnlyAskLena (fun _ -> dispatch ToggleOnlyAskLena)
                    Input.countSwitchRow "filter-without-category" "Ohne Kategorie" (Some model.WithoutCategoryCount) model.Filters.WithoutCategory (fun _ -> dispatch ToggleWithoutCategory)
                    Input.valueRow
                        "filter-category"
                        "Kategorie"
                        (filterSheetCategoryValue names (Set.count model.Filters.CategoryIds))
                        (hasCategoryFilter model.Filters)
                        (fun () -> dispatch (OpenFilterPopover CategoryChip))
                    Input.valueRow
                        "filter-date"
                        "Zeitraum"
                        (filterSheetDateValue model.Filters)
                        (hasDateFilter model.Filters)
                        (fun () -> dispatch (OpenFilterPopover DateChip))
                    Html.div [
                        prop.className $"space-y-3 py-3 border-b {Tokens.Borders.subtle}"
                        prop.children [
                            Html.div [
                                prop.className "grid grid-cols-2 gap-3"
                                prop.children [
                                    fieldLabel "Betrag von" (amountField "Betrag von" model.AmountMinDraft (fun text -> dispatch (SetAmountMinDraft text)) "0,00")
                                    fieldLabel "bis" (amountField "Betrag bis" model.AmountMaxDraft (fun text -> dispatch (SetAmountMaxDraft text)) "0,00")
                                ]
                            ]
                            amountDirectionSegments "filter-sheet-direction" model.Filters.AmountDirection dispatch
                        ]
                    ]
                    Input.valueRow
                        "filter-sort"
                        "Sortieren"
                        (Components.Ledger.State.sortValueText sort)
                        (sort <> LedgerSort.defaultSort)
                        (fun () -> dispatch OpenSortSheet)
                    if model.DeletedCount > 0 || model.IncludeDeleted then
                        Input.countSwitchRow "filter-deleted" "Gelöschte zeigen" (Some model.DeletedCount) model.IncludeDeleted (fun _ -> dispatch ToggleIncludeDeleted)
                ]
              ]
        ]

/// Der Knopf „Filter“ neben der Suche: mit gesetzten Filtern „Filter (n)“ in Akzentrahmen und Akzentschrift.
let private filterButton (dispatch: Msg -> unit) (model: Model) (count: int) : ReactElement =
    Button.view
        { Button.defaultProps with
            Text = filterButtonText count
            Icon = Some (Icons.filter Icons.SM Icons.Default)
            Variant = (if count > 0 then Button.OutlineSelected else Button.Outline)
            AriaExpanded = Some model.FilterSheetOpen
            ClassName = Some "shrink-0 min-h-12"
            OnClick = fun () -> dispatch OpenFilterSheet }

/// Die gesetzten Filter als Chips mit „×“ (§7.5): sie brechen um, die Zeile wischt nicht seitlich. Ohne gesetzten
/// Filter gibt es die Zeile nicht. Ein Tipp auf den Chip öffnet das Filter-Sheet, „×“ nimmt genau diesen Filter zurück.
let private activeFilterChipRow (dispatch: Msg -> unit) (chips: (ActiveFilter * string) list) : ReactElement =
    match chips with
    | [] -> Html.none
    | _ ->
        Html.div [
            prop.custom ("data-testid", "ledger-active-filters")
            prop.className "flex flex-wrap gap-2"
            prop.children [
                for (filter, label) in chips ->
                    React.keyedFragment (
                        string filter,
                        [ ChipBar.chip
                              { Label = label
                                IsActive = true
                                OnClick = (fun () -> dispatch OpenFilterSheet)
                                OnRemove = Some (fun () -> dispatch (Components.Ledger.State.removeActiveFilterMsg filter)) } ]
                    )
            ]
        ]

/// Kontowahl, Suche mit Filter-Knopf und die gesetzten Filter unter 1024 px, samt ihrer Sheets (ledger-d9z3n).
let private handyControls
    (dispatch: Msg -> unit)
    (model: Model)
    (accounts: LedgerAccountView list)
    (balances: RemoteData<LedgerAccountBalanceView list>)
    (categories: YnabCategory list)
    (isAllAccounts: bool)
    : ReactElement =
    let chips = Components.Ledger.State.activeFilters model
    Html.div [
        prop.custom ("data-testid", "ledger-handy-controls")
        prop.className "flex flex-col gap-3"
        prop.children [
            accountField dispatch model accounts balances
            Html.div [
                prop.className "flex items-center gap-2"
                prop.children [
                    ledgerSearchField dispatch model
                    filterButton dispatch model (List.length chips)
                ]
            ]
            activeFilterChipRow dispatch chips
            filterSheet dispatch model categories isAllAccounts
            sortSheet dispatch model isAllAccounts
        ]
    ]

/// Zustand der Summenzeile: ohne Filter nichts, beim Laden ein Platzhalter, danach
/// Trefferzahl und Summe (auch bei 0 Treffern). Ein Ladefehler zeigt keine Zeile, weil
/// Zahl und Summe dann nicht zum Filter passen.
[<RequireQualifiedAccess>]
type SummaryState =
    | Hidden
    | Loading
    | Ready

let summaryState (model: Model) : SummaryState =
    if not (hasSummaryFilter model.OnlyNeedsReview model.Filters) then
        SummaryState.Hidden
    else
        match model.Entries with
        | Success _ -> SummaryState.Ready
        | Loading
        | NotAsked -> SummaryState.Loading
        | Failure _ -> SummaryState.Hidden

/// "0 Buchungen", "1 Buchung", "12.116 Buchungen"
let summaryCountText (count: int) : string =
    let noun = if count = 1 then "Buchung" else "Buchungen"
    $"{Money.formatCount count} {noun}"

/// Text der Summenzeile: am PC Trefferzahl und Filter im Klartext, am Handy nur die Trefferzahl.
let summaryLineText (desktop: bool) (count: int) (plainText: unit -> string) : string =
    if desktop then $"{summaryCountText count} · {plainText ()}" else summaryCountText count

/// Die Summenzeile über der Liste (AC: erscheint genau dann, wenn mindestens ein
/// Filter aktiv ist; Trefferzahl und Summe über den ganzen Treffer, nicht die Seite
/// (beide kommen unverändert vom Server, `model.TotalCount`/`model.TotalAmount`).
/// Ohne Karte, eine schlanke Zeile; beim Laden bleibt sie als Skeleton stehen.

let private summaryRow (accounts: LedgerAccountView list) (categories: YnabCategory list) (model: Model) : ReactElement =
    match summaryState model with
    | SummaryState.Hidden -> Html.none
    | state ->
        Html.div [
            prop.className $"flex items-center justify-between gap-3 px-1 h-6"
            prop.children [
                match state with
                | SummaryState.Loading ->
                    Html.div [ prop.className "w-28"; prop.children [ Loading.line "100" ] ]
                    Html.div [ prop.className "w-20"; prop.children [ Loading.line "100" ] ]
                | _ ->
                    Html.span [
                        prop.className Tokens.ListRoles.secondary
                        prop.custom ("data-testid", "ledger-summary-text")
                        prop.text (
                            summaryLineText (Viewport.isInspectorPanelViewport ()) model.TotalCount (fun () ->
                                filterPlainText
                                    (accountScopeText accounts model.SelectedFilter)
                                    model.OnlyNeedsReview
                                    (selectedCategoryNames categories model.Filters.CategoryIds)
                                    model.Filters)
                        )
                    ]
                    Money.listAmount model.TotalAmount.Amount model.TotalAmount.Currency
            ]
        ]

/// Die Summenzeile im Auswahl-Zustand (ledger-r1d2f): "Auswahl: n von N Buchungen" mit der
/// Summe der Auswahl und der Akzent-Kante. Sie ersetzt den Treffer-Zustand, solange die Auswahl
/// nicht leer ist, und steht auch ohne aktiven Filter da. Anzahl und Summe liefert nur
/// `selectionTotals`.
let private selectionSummaryRow (model: Model) : ReactElement =
    let totals = selectionTotals model
    MultiSelect.summaryRow (selectionSummaryText totals.Count model.TotalCount) totals.Amount

/// Welche Summenzeile über der Liste steht.
type SummaryRowKind =
    | SelectionSummary
    | HitSummary

let summaryRowKind (model: Model) : SummaryRowKind =
    if hasSelection model then SelectionSummary else HitSummary

/// Summenzeile über der Liste: die Auswahl, solange sie nicht leer ist, sonst der Treffer.
let private summaryOrSelectionRow (accounts: LedgerAccountView list) (categories: YnabCategory list) (model: Model) : ReactElement =
    match summaryRowKind model with
    | SelectionSummary -> selectionSummaryRow model
    | HitSummary -> summaryRow accounts categories model

/// Steht der Tastaturhinweis da? Nur mit geladenen Zeilen und ohne Auswahl, an deren Stelle
/// die Leiste tritt. In der Kompaktliste nur mit Maus (ledger-m6zpd): das Modellfeld
/// `ListInteraction` trägt, was die Ansicht gemessen hat, so bleibt die Funktion rein.
let keyboardHintVisible (model: Model) : bool =
    match model.Entries with
    | Success (_ :: _) -> (listInteractionTraits model.ListInteraction).FooterHint && not (hasSelection model)
    | _ -> false

/// Die feste Leiste unten am PC (Auswahl-Leiste oder Tastatur-Hinweis) schreibt ihre gemessene Höhe als
/// `--ledger-bottom-bar-height` auf das Dokument; die Rückmeldungen stehen darüber (`.toast-above-bar` in
/// `styles.css`, §7.13, ledger-cnaz3). Die
/// Leiste bricht um und wächst mit der Schrift, eine feste Höhe passte dann nicht (Layout-Sweep, Zustand
/// kassenbuch-viele-konten-auswahl). Gemessen wird das erste Kind der Hülle, die Hülle selbst hat keine Höhe,
/// weil die Leiste `fixed` ist. Der Aufrufer gibt jeder Leiste einen eigenen Schlüssel, damit ein Wechsel
/// einen neuen Knoten bindet.
[<ReactComponent>]
let private MeasuredBar (variable: string) (scope: IReactProperty list) (bar: ReactElement) =
    let disconnect = React.useRef<(unit -> unit) option> None
    let measure =
        React.useCallback (
            (fun (element: Browser.Types.Element) ->
                disconnect.current |> Option.iter (fun stop -> stop ())
                disconnect.current <- None
                if not (isNull (box element)) then
                    disconnect.current <- Some (Viewport.mirrorChildHeight variable element)),
            [| box variable |]
        )
    // `scope`: Attribute der Hülle, etwa `data-lena-flag` für die Farbe des Flags an „Lena fragen“.
    Html.div [ prop.ref measure; yield! scope; prop.children [ bar ] ]

/// Die feste Leiste unten am PC, gemessen nach `--ledger-bottom-bar-height`.
let private MeasuredBottomBar (scope: IReactProperty list) (bar: ReactElement) =
    MeasuredBar "--ledger-bottom-bar-height" scope bar

/// Die Props des Pickers der Auswahl-Leiste ("Kategorie setzen" für mehrere): nur Kategorien,
/// keine Konten (Roman, ledger-qvjvs: ein Übertrag geht nur einzeln im Detail).
let batchPickerProps (dispatch: Msg -> unit) (model: Model) : CategoryPicker.CategoryPickerProps =
    { basePickerProps dispatch model with
        OnSelect = CategoryPicker.categoryChoice (fun choice -> dispatch (StartCategoryBatch choice))
        OnClose = fun () -> dispatch CloseBatchCategoryPicker }

/// Sperre und Ladezustand der Auswahl-Leiste (ledger-sjbvc).
type SelectionBarRun =
    { /// Läuft ein Sammelaufruf, sind alle Sammelaktionen gesperrt.
      Locked: bool
      ReviewBusy: bool
      CategoryBusy: bool
      /// Der Ladezustand steht an "Lena fragen", auch beim Zurücknehmen und beim Rückgängig.
      LenaBusy: bool }

/// Leitet aus dem laufenden Sammellauf ab, welche Schaltfläche lädt und ob die Leiste sperrt.
/// Ein Rückgängig lädt an der Schaltfläche seiner Aktion. Rein.
let selectionBarRun (running: BatchRun option) : SelectionBarRun =
    { Locked = running.IsSome
      ReviewBusy = (running = Some ReviewMarkBatchRun)
      CategoryBusy = (running = Some CategoryBatchRun)
      LenaBusy = (running = Some LenaBatchRun) }

// ============================================
// Zusammenfassung der Auswahl (ledger-cnaz3, §7.7, Prototyp Version 17 `selSummary`)
// ============================================

/// Eine Zeile „Nach Kategorie“: Name und Summe, `WithoutCategory` für „ohne Kategorie“ (Gelb).
type SelectionCategoryLine = {
    Label: string
    Amount: decimal
    WithoutCategory: bool
}

/// Was die Zusammenfassung über die gewählten geladenen Buchungen sagt. In YNAB gelöschte Buchungen zählen mit,
/// gehen aber in keine Summe ein (dieselbe Regel wie `selectionTotals`).
type SelectionSummary = {
    Count: int
    Sum: decimal
    Outflows: decimal
    Inflows: decimal
    ToReview: int
    ByCategory: SelectionCategoryLine list
}

/// Der Name einer Zeile „Nach Kategorie“ für eine Buchung ohne Split: die Kategorie wie im Kopf des Details, ein
/// Übertrag als „Übertrag“, sonst „ohne Kategorie“.
let private summaryCategoryKey (entry: LedgerEntryView) : string * bool =
    if isWithoutCategory entry then "ohne Kategorie", true else categoryLabelQualified entry, false

/// Die Zusammenfassung der Auswahl, rein: Summe, Ausgaben, Einnahmen, Anzahl zu prüfen und die Aufteilung nach
/// Kategorie. Eine Split-Buchung geht mit ihren Zeilen ein (eine Zeile ohne Ziel als „ohne Kategorie“). Sortiert
/// von der größten Ausgabe zur größten Einnahme; gleiche Beträge nach Name.
let selectionSummaryOf (entries: LedgerEntryView list) : SelectionSummary =
    let counted = entries |> List.filter (fun e -> not e.DeletedInYnab)
    let amounts = counted |> List.map (fun e -> e.Amount.Amount)
    let parts =
        counted
        |> List.collect (fun entry ->
            if List.isEmpty entry.Splits then
                let label, missing = summaryCategoryKey entry
                [ (label, missing), entry.Amount.Amount ]
            else
                entry.Splits
                |> List.map (fun split ->
                    match split.Target with
                    | Uncategorized -> ("ohne Kategorie", true), split.Amount.Amount
                    | _ -> (splitTargetLabel entry.SplitCategoryGroupNames split, false), split.Amount.Amount))
    let byCategory =
        parts
        |> List.groupBy fst
        |> List.map (fun ((label, missing), items) ->
            { Label = label; Amount = items |> List.sumBy snd; WithoutCategory = missing })
        |> List.sortBy (fun line -> line.Amount, line.Label)
    { Count = List.length entries
      Sum = List.sum amounts
      Outflows = amounts |> List.filter (fun a -> a < 0m) |> List.sum
      Inflows = amounts |> List.filter (fun a -> a > 0m) |> List.sum
      ToReview = entries |> List.filter (fun e -> e.NeedsReview) |> List.length
      ByCategory = byCategory }

/// Kopf der Zusammenfassung: „n Buchungen ausgewählt“.
let selectionSummaryTitle (count: int) : string =
    $"{Money.formatCount count} Buchungen ausgewählt"

let selectionSummaryHint = "Umschalt-Klick wählt einen Bereich, Strg- oder Cmd-Klick einzelne"
let selectionAllReviewedLabel = "Alle geprüft"

/// Der Satz unter der Karte, wenn die Auswahl mehr Buchungen meint als geladen sind („alle“ über das
/// Kopfzeilen-Kästchen): Summe und Anzahl stimmen (`selectionTotals`), die Aufteilung kennt nur die geladenen.
let selectionPartialNote (selectedCount: int) (loadedCount: int) : string option =
    if loadedCount < selectedCount then
        Some $"Ausgaben, Einnahmen und Kategorien aus den {Money.formatCount loadedCount} geladenen Buchungen."
    else
        None

/// Steht „Rückfrage zurücknehmen“ in der Zusammenfassung? Nur, wenn eine gewählte Buchung gefragt ist (BB). Unter
/// „alle“ kennt die Liste nur die geladenen; dann reicht auch der Zähler `AskLenaCount` vom Server.
let selectionOffersWithdraw (model: Model) : bool =
    let loadedAsked = selectedEntries model |> List.exists (fun e -> e.Flag = AskLena)
    match model.Selection with
    | AllExcept _ -> loadedAsked || model.AskLenaCount > 0
    | _ -> loadedAsked

/// Was das Panel am PC zeigt (ledger-cnaz3, ADR 0087 Punkte 16 und 17): „Neue Buchung“, ab zwei gewählten Buchungen
/// die Zusammenfassung, sonst das Detail der offenen Buchung oder den leeren Zustand. Rein.
type PanelContent =
    | PanelCapture
    | PanelSelectionSummary
    | PanelDetail
    | PanelEmpty

let panelContentFor (model: Model) : PanelContent =
    if model.Capture.IsSome then PanelCapture
    elif (selectionTotals model).Count >= 2 then PanelSelectionSummary
    elif (detailEntryOf model).IsSome then PanelDetail
    else PanelEmpty

/// Der Satz im leeren Panel (§7.12, Prototyp `panelDesk`).
let emptyPanelText = "Buchung wählen, um Details zu sehen."

/// Die Leiste am PC je Auswahl (ledger-cnaz3): ohne Auswahl die Tastenleiste, bei einer Buchung die Leiste mit allen
/// Aktionen, ab zwei nur Anzahl, Summe und „Auswahl aufheben“ (die Aktionen stehen im Panel). Rein.
type DesktopBar =
    | KeysBar
    | SingleSelectionBar
    | CompactSelectionBar
    | NoDesktopBar

let desktopBarFor (model: Model) : DesktopBar =
    match (selectionTotals model).Count with
    | 0 -> if keyboardHintVisible model then KeysBar else NoDesktopBar
    | 1 -> SingleSelectionBar
    | _ -> CompactSelectionBar

/// Die Lena-Aktionen der Auswahl (ledger-y54gr): „Lena fragen“ braucht die Flag-Farbe aus den Settings, sonst ist
/// sie gesperrt und nennt den Grund; zurücknehmen braucht sie nicht. Am PC ohne Zahl im Text (Prototyp), am Handy
/// mit der Zahl der Auswahl.
let private lenaActions (dispatch: Msg -> unit) (model: Model) (withCount: bool) : MultiSelect.LenaAction * MultiSelect.LenaAction =
    let totals = selectionTotals model
    let run = selectionBarRun model.BatchRunning
    { Label = (if withCount then selectionLenaLabel totals.Count else "Lena fragen")
      IsBusy = run.LenaBusy
      IsLocked = run.Locked
      Reason = selectionLenaDisabledReason model.LenaFlagColor
      OnClick = fun () -> dispatch (StartLenaBatch AssignLena) },
    { Label = (if withCount then selectionWithdrawLabel totals.Count else "Rückfrage zurücknehmen")
      IsBusy = false
      IsLocked = run.Locked
      Reason = None
      OnClick = fun () -> dispatch (StartLenaBatch WithdrawLena) }

/// „Kategorie setzen“ der Auswahl mit seinem Picker: `popover` am PC (über der Leiste oder im Panel), sonst das
/// Sheet am Handy.
let private categoryAction (dispatch: Msg -> unit) (model: Model) (popover: CategoryPicker.CategoryPickerProps -> ReactElement option) : MultiSelect.CategoryAction =
    let totals = selectionTotals model
    let run = selectionBarRun model.BatchRunning
    let pickerProps = batchPickerProps dispatch model
    { Label = selectionCategoryLabel
      IsBusy = run.CategoryBusy
      IsLocked = run.Locked
      OnClick = fun () -> dispatch ToggleBatchCategoryPicker
      Popover = popover pickerProps |> Option.defaultValue Html.none
      Sheet =
        (match popover pickerProps with
         | Some _ -> Html.none
         | None ->
            CategoryPicker.selectionSheet
                model.BatchCategoryPickerOpen
                (Some (selectionPickerSubtitle totals.Count totals.Amount))
                pickerProps) }

/// Die Zusammenfassung der Auswahl im Panel (ledger-cnaz3, §7.7): Anzahl, Summe, Ausgaben, Einnahmen, „Davon zu
/// prüfen“, die Aufteilung nach Kategorie und die Sammelaktionen („Alle geprüft“ primär).
let private selectionSummaryPanel (dispatch: Msg -> unit) (model: Model) : ReactElement =
    let totals = selectionTotals model
    let entries = selectedEntries model
    let summary = selectionSummaryOf entries
    let currency = model.TotalAmount.Currency
    let money (amount: decimal) : Money = { Amount = amount; Currency = currency }
    let moneyText (amount: decimal) = Money.formatAmountText false true amount currency
    let run = selectionBarRun model.BatchRunning
    let lena, withdraw = lenaActions dispatch model false
    Detail.panel
        "Auswahl"
        (MultiSelect.summaryPanel {
            Title = selectionSummaryTitle totals.Count
            Hint = selectionSummaryHint
            SumLabel = "Summe"
            Sum = totals.Amount
            Figures =
                [ "Ausgaben", moneyText summary.Outflows
                  "Einnahmen", moneyText summary.Inflows
                  "Davon zu prüfen", Money.formatCount summary.ToReview ]
            CategoryHeading = "Nach Kategorie"
            Categories =
                summary.ByCategory
                |> List.map (fun line -> { MultiSelect.SummaryLine.Label = line.Label; Amount = money line.Amount; Missing = line.WithoutCategory })
            Note = selectionPartialNote totals.Count summary.Count
            ReviewedLabel = selectionAllReviewedLabel
            ReviewedBusy = run.ReviewBusy
            Locked = run.Locked
            OnReviewed = fun () -> dispatch StartReviewMarkBatch
            Category =
                categoryAction dispatch model (fun props -> Some (CategoryPicker.selectionPopoverInPanel model.BatchCategoryPickerOpen props))
            Lena = lena
            Withdraw = (if selectionOffersWithdraw model then Some withdraw else None)
            ClearLabel = selectionClearLabel
            OnClear = fun () -> dispatch ClearSelection
        })

/// Die Auswahl-Leiste am unteren Rand (ledger-r1d2f), mit der einen primären Aktion
/// "Geprüft" (ledger-nhwrt). Am Handy erscheint sie im Auswahl-Modus und ersetzt die Tab-Bar
/// (zwei Reihen); am PC steht sie, solange die Auswahl nicht leer ist (`desktopBarFor`): bei einer
/// Buchung mit allen Aktionen, ab zwei nur mit Anzahl, Summe und „Auswahl aufheben“ (ledger-cnaz3).
/// Sie ist `fixed` und steht deshalb außerhalb der Seitenhülle mit der Eintrittsanimation
/// (`animate-page-enter` setzt einen Transform, in dem `fixed` an der Hülle statt am Fenster
/// haftet): der Haupt-`View.fs` rendert sie neben den Toasts.
let selectionBars (dispatch: Msg -> unit) (model: Model) : ReactElement =
    let totals = selectionTotals model
    let countText = selectionBarCountText totals.Count
    let run = selectionBarRun model.BatchRunning
    let locked = run.Locked
    // Popover am PC, Sheet am Handy: nie beide, denn das Sheet portalt in den Body (ADR 0043).
    let desktop = Viewport.isDesktopViewport ()
    let category =
        categoryAction dispatch model (fun props ->
            if desktop then Some (CategoryPicker.selectionPopover model.BatchCategoryPickerOpen props) else None)
    React.fragment [
        if model.SelectionMode then
            let lena, withdraw = lenaActions dispatch model true
            // Gemessen nach `--ledger-mobile-bar-height`: die Meldungsleiste steht direkt darüber
            // (`.feedback-dock-selection`, design-system-gkded).
            React.keyedFragment (
                "selection-bar-mobile",
                [ MeasuredBar
                      "--ledger-mobile-bar-height"
                      []
                      (MultiSelect.barMobile
                          countText
                          totals.Amount
                          selectionAllLabel
                          (isAllSelected model.Selection)
                          (fun () -> dispatch ToggleSelectAll)
                          selectionReviewedLabel
                          run.ReviewBusy
                          locked
                          (fun () -> dispatch StartReviewMarkBatch)
                          category
                          lena
                          withdraw
                          selectionClearLabel
                          (fun () -> dispatch ClearSelection)) ]
            )
        match desktopBarFor model with
        | SingleSelectionBar ->
            let lena, withdraw = lenaActions dispatch model false
            React.keyedFragment (
                "selection-bar",
                // Die Farbe des Flags an „Lena fragen“ (die Leiste steht außerhalb der Seite).
                [ MeasuredBottomBar
                      (Badge.lenaFlagScope (lenaColorOf model.LenaFlagColor))
                      (MultiSelect.barDesktop
                          Tokens.PanelWidths.fixedRight
                          countText
                          totals.Amount
                          selectionReviewedLabel
                          run.ReviewBusy
                          locked
                          (fun () -> dispatch StartReviewMarkBatch)
                          category
                          lena
                          withdraw
                          selectionClearLabel
                          selectionClearKeyHint
                          (fun () -> dispatch ClearSelection)) ]
            )
        | CompactSelectionBar ->
            React.keyedFragment (
                "selection-bar-compact",
                [ MeasuredBottomBar [] (
                      MultiSelect.barDesktopCompact
                          Tokens.PanelWidths.fixedRight
                          countText
                          totals.Amount
                          selectionClearLabel
                          selectionClearKeyHint
                          (fun () -> dispatch ClearSelection)
                  ) ]
            )
        | KeysBar -> React.keyedFragment ("keyboard-hint", [ MeasuredBottomBar [] (keyboardHint ()) ])
        | NoDesktopBar -> ()
    ]

/// Je ein Abstandshalter in Höhe der Leiste unter der Liste, damit die letzte Zeile
/// erreichbar bleibt: sie ist nie dauerhaft verdeckt.
let private selectionBarSpacers (model: Model) : ReactElement =
    React.fragment [
        if model.SelectionMode then Html.div [ prop.className "md:hidden h-52"; prop.ariaHidden true ]
        // Die Leiste bricht neben Seitenleiste und Panel mehrzeilig um: der Abstandshalter ist so hoch, wie
        // `MeasuredBottomBar` sie misst (`--ledger-bottom-bar-height`), vor der ersten Messung 7 rem (ledger-cnaz3).
        if hasSelection model || keyboardHintVisible model then
            Html.div [
                prop.className "hidden md:block"
                prop.style [ style.custom ("height", "var(--ledger-bottom-bar-height, 7rem)") ]
                prop.ariaHidden true
            ]
    ]

/// Steht "+ Buchung" am Handy in der Meldungsleiste (design-system-gkded)? Nicht bei offenem Formular und nicht im
/// Auswahl-Modus (dort steht die Auswahl-Leiste).
let captureFabVisible (model: Model) : bool =
    model.Capture.IsNone && not model.SelectionMode

/// Die Hinweisleiste über der Liste, wenn eine Kategorieänderung den Sortierschlüssel verschoben
/// hat (ADR 0055, Punkt 9). Sie steht nur bei `OrderStale`; "Neu laden" setzt die Liste ab
/// Seite 1 mit der aktiven Sortierung neu auf.
let private orderStaleBar (dispatch: Msg -> unit) (model: Model) : ReactElement =
    if not model.OrderStale then
        Html.none
    else
        Html.div [
            prop.custom ("data-testid", "order-stale-bar")
            prop.className $"overflow-hidden rounded-xl border {Tokens.Borders.subtle}"
            prop.children [
                Detail.notice
                    { Tone = Detail.NoticeYellow
                      Badge = "Liste nicht mehr aktuell"
                      Reason = orderStaleText
                      Action =
                        Some
                            { Label = orderStaleActionLabel
                              Busy = RemoteData.isLoading model.Entries
                              Disabled = false
                              OnClick = fun () -> dispatch LoadEntries } }
            ]
        ]

/// Zeile über der Liste, solange es gelöschte Buchungen im Filter gibt (AC2,
/// ledger-p3n8k): Anzahl-Satz plus Schalter. Ohne gelöschte Buchungen im Filter
/// steht hier nichts (ein Schalter ohne Wirkung wäre nur Rauschen).
let private deletedToggleRow (dispatch: Msg -> unit) (model: Model) =
    if model.DeletedCount <= 0 then
        Html.none
    else
        Html.div [
            prop.className $"flex items-center justify-between gap-3 px-1 {Tokens.FontSizes.xs} {Tokens.Colors.textMuted}"
            prop.children [
                Html.span [ prop.text (deletedCountText model.TotalCount model.DeletedCount model.IncludeDeleted) ]
                Input.toggle model.IncludeDeleted (fun _ -> dispatch ToggleIncludeDeleted) (Some "Gelöschte zeigen")
            ]
        ]

// ============================================
// Neue Buchung (ledger-at63w)
// ============================================

let captureTitle = "Neue Buchung"
let captureSubmitLabel = "Buchung anlegen"
let captureCancelLabel = "Abbrechen"
let captureOpenLabel = "Buchung"
let captureRecentLabel = "Zuletzt"
let captureAccountPickerTitle = "Konto wählen"

/// Das Konto des Formulars als Kassenbuch-Konto, wenn es bekannt ist.
let captureAccountOf (model: Model) (form: Capture.CaptureForm) : LedgerAccountView option =
    match model.Accounts, form.AccountId with
    | Success view, Some id -> view.Accounts |> List.tryFind (fun a -> a.Id = id)
    | _ -> None

/// Der Text im Feld "Kategorie": der Name der Kategorie oder "Übertrag → Konto", `None` ohne Ziel.
let captureTargetLabel (model: Model) (target: LedgerBookingTarget option) : string option =
    match target with
    | Some (BookOnCategory id) ->
        model.Categories
        |> RemoteData.toOption
        |> Option.bind (List.tryFind (fun c -> c.Id = id))
        |> Option.map (fun c -> c.Name)
        |> Option.orElse (Some "Kategorie")
    | Some (BookAsTransfer account) ->
        let name =
            model.TransferTargets
            |> RemoteData.toOption
            |> Option.bind (List.tryFind (fun a -> a.Id = account))
            |> Option.map (fun a -> a.Name)
            |> Option.defaultValue "Konto"
        Some $"Übertrag → {name}"
    | None -> None

/// Die Nachricht einer Wahl im Picker des Formulars: eine Kategorie, "Ohne Kategorie" oder ein
/// Konto (Übertrag). "Zu verteilen" bietet das Formular nicht an.
let capturePickerMsg (choice: CategoryPicker.PickerChoice) : Msg option =
    match choice with
    | CategoryPicker.ChoseAccount account -> Some (ChooseCaptureTarget (Some (BookAsTransfer account)))
    | CategoryPicker.ChoseCategory id -> Some (ChooseCaptureTarget (id |> Option.map BookOnCategory))
    | CategoryPicker.ChoseToBeBudgeted -> None

/// Der Picker des Formulars: die Ziel-Konten sind die offenen Budget-Konten ohne das gewählte Konto.
let private capturePickerProps (dispatch: Msg -> unit) (model: Model) (form: Capture.CaptureForm) : CategoryPicker.CategoryPickerProps =
    { basePickerProps dispatch model with
        Current = (match form.Target with Some (BookOnCategory id) -> Some id | _ -> None)
        Accounts = transferTargetsExcluding (captureAccountOf model form |> Option.bind (fun a -> a.YnabAccountId)) model
        CurrentAccount = (match form.Target with Some (BookAsTransfer a) -> Some a | _ -> None)
        AccountsLoad = transferTargetsLoadState model.TransferTargets
        OnSelect = fun choice -> capturePickerMsg choice |> Option.iter dispatch
        OnClose = fun () -> dispatch CloseCapturePicker }

/// Der Saldo eines Kontos für das Konto-Feld und die Konto-Auswahl.
let private captureBalanceOf (model: Model) (accountId: Guid) : Money option =
    match model.Balances with
    | Success balances -> balances |> List.tryFind (fun b -> b.AccountId = accountId) |> Option.map (fun b -> b.Balance)
    | _ -> None

/// Die Konto-Auswahl: jedes wählbare Konto mit Saldo, das gewählte markiert. Ohne Konten sagt sie, warum: lädt
/// (Spinner mit Text), Ladefehler oder kein wählbares Konto (ledger-vf6z5, `State.captureAccountChoice`).
let private captureAccountList (dispatch: Msg -> unit) (model: Model) (capture: Capture.CaptureState) : ReactElement =
    let choice = State.captureAccountChoice model capture
    let accounts =
        match choice with
        | Capture.BookableAccounts accounts -> accounts
        | _ -> []
    // Kurze Auswahl ohne Suche (§7.9, F7): Fokus auf dem gewählten Konto, ↑↓ und Enter.
    PickerParts.ShortChoice "capture-account-list" [
            match choice with
            | Capture.CaptureAccountsLoading ->
                yield Html.div [
                    prop.role "status"
                    prop.className $"flex items-center gap-2 px-3 py-2 {Tokens.FontSizes.sm} {Tokens.Colors.textMuted}"
                    prop.children [ Loading.spinner Loading.SM Loading.Default; Html.span [ prop.text Capture.accountsLoadingText ] ]
                ]
            | Capture.CaptureAccountsFailed ->
                yield Html.div [ prop.className "px-3 py-2"; prop.children [ ErrorDisplay.inline' Capture.accountsFailedText ] ]
            | Capture.NoBookableAccount ->
                yield Html.p [ prop.className $"px-3 py-2 {Tokens.FontSizes.sm} {Tokens.Colors.textMuted}"; prop.text Capture.noBookableAccountText ]
            | Capture.BookableAccounts _ -> ()
            for account in accounts ->
                let isActive = capture.Form.AccountId = Some account.Id
                let pick = fun () -> dispatch (ChooseCaptureAccount account.Id)
                React.keyedFragment (
                    account.Id,
                    // Dieselbe Zeile wie im Sheet „Konto“ (ledger-zf6zg): Haken und Akzentrahmen am gewählten Konto,
                    // Saldo in Primärtext, negativ rot; ohne bekannten Saldo bleibt die Stelle leer.
                    [ Money.accountChoiceRow
                          "capture-account-choice"
                          account.Name
                          (match captureBalanceOf model account.Id with
                           | Some balance -> cardBalanceElement true (BalanceShown balance)
                           | None -> Html.none)
                          isActive
                          pick ]
                )
    ]

/// Wo das Formular steht: im Panel rechts (Kassenbuch am PC), im Dialog (Übersicht am PC, ledger-s1weh) oder im
/// Sheet. Im Panel und im Dialog öffnen Konto und Kategorie an Ort und Stelle, im Sheet ersetzen sie den Inhalt
/// (§7.15, ledger-ea70s).
type private CapturePlace =
    | CaptureInPanel
    | CaptureInDialog
    | CaptureInSheet

/// Die Felder des Formulars von oben nach unten wie im Prototyp: Ausgabe/Einnahme, Betrag,
/// Zuletzt, Konto, Kategorie oder Konto, Payee, Datum und Memo, dazu Fehler. `place`: im Panel
/// öffnen die Auswahlen im Panel, im Sheet ersetzen sie den Inhalt (`captureSheetView`).
let private captureFields (dispatch: Msg -> unit) (model: Model) (capture: Capture.CaptureState) (place: CapturePlace) : ReactElement list =
    let form = capture.Form
    let account = captureAccountOf model form
    let accountOpen = capture.Picker = Some Capture.CaptureAccountPicker
    let targetOpen = capture.Picker = Some Capture.CaptureTargetPicker
    let amountInvalid = capture.AmountError.IsSome
    let label (text: string) =
        Html.span [ prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted}"; prop.text text ]
    [
        directionSegments "capture-direction" form.IsOutflow (fun isOutflow -> dispatch (SetCaptureDirection isOutflow))

        // Betrag ohne Vorzeichen: die Zifferntastatur zu `decimal` hat je nach App kein Minus, die
        // Richtung kommt aus Ausgabe oder Einnahme.
        Html.div [
            prop.custom ("data-testid", "capture-amount")
            prop.children [
                Input.text
                    { Input.textInputDefaults with
                        Value = form.AmountText
                        OnChange = fun text -> dispatch (SetCaptureAmount text)
                        Placeholder = "0,00 €"
                        Size = Input.Large
                        InputMode = Some "decimal"
                        AriaLabel = Some "Betrag"
                        // Am PC beim Öffnen gleich tippen (wie vorher Quick Add, ledger-ea70s); am Handy kein
                        // Autofokus, die Tastatur verdeckte sonst Zuletzt und Konto.
                        AutoFocus = Viewport.isFinePointer ()
                        DescribedBy = (if amountInvalid then Some Capture.amountErrorId else None)
                        AriaInvalid = amountInvalid
                        ClassName = Some (Input.amountFieldClass true amountInvalid) }
                // Der Fehler steht am Feld (Styleguide §5, ledger-vf6z5); `RevealCaptureAmountError` rollt ihn ins Bild.
                match capture.AmountError with
                | Some message ->
                    Html.div [
                        prop.id Capture.amountErrorId
                        prop.custom ("data-testid", Capture.amountErrorId)
                        prop.children [ ErrorDisplay.inline' message ]
                    ]
                | None -> ()
            ]
        ]

        let recent =
            match capture.Defaults with
            | Success defaults -> Capture.templatesFor capture.Form.AccountId defaults
            | _ -> []
        match recent with
        | _ :: _ ->
            Html.div [
                prop.className "flex flex-col gap-1.5"
                prop.custom ("data-testid", "capture-recent")
                prop.children [
                    label captureRecentLabel
                    Html.div [
                        prop.className "flex flex-wrap gap-2"
                        prop.children [
                            for template in recent ->
                                React.keyedFragment (
                                    Capture.templateKey template,
                                    [ BottomSheet.chipButtonTouch
                                          (Capture.templateLabel template)
                                          (fun () -> dispatch (ApplyCaptureTemplate template)) ]
                                )
                        ]
                    ]
                ]
            ]
        | [] -> ()

        Html.div [
            prop.className "relative"
            prop.children [
                Input.pickerField
                    "capture-account"
                    "Konto"
                    (account |> Option.map (fun a -> a.Name))
                    "wählen"
                    (account
                     |> Option.bind (fun a -> captureBalanceOf model a.Id)
                     |> Option.map (fun b -> Money.accountBalance b.Amount b.Currency))
                    accountOpen
                    (fun () -> dispatch (if accountOpen then CloseCapturePicker else OpenCapturePicker Capture.CaptureAccountPicker))
                // `pickerField` ist ein Knopf, `aria-invalid` gilt dort nicht: die Meldung trägt `role="alert"`.
                match capture.AccountError with
                | Some message ->
                    Html.div [
                        prop.custom ("data-testid", "capture-account-error")
                        prop.children [ ErrorDisplay.inline' message ]
                    ]
                | None -> ()
                if place <> CaptureInSheet && accountOpen then
                    Html.div [
                        prop.className $"mt-1 rounded-lg border {Tokens.Borders.default'} {Tokens.Backgrounds.elevated} p-1"
                        prop.children [ captureAccountList dispatch model capture ]
                    ]
            ]
        ]

        Html.div [
            prop.className "relative"
            prop.children [
                Input.pickerField
                    "capture-target"
                    "Kategorie"
                    (captureTargetLabel model form.Target)
                    "wählen"
                    None
                    targetOpen
                    (fun () -> dispatch (if targetOpen then CloseCapturePicker else OpenCapturePicker Capture.CaptureTargetPicker))
                match place with
                | CaptureInPanel -> CategoryPicker.popoverInPanel targetOpen (capturePickerProps dispatch model form)
                // Im scrollenden Körper des Dialogs rückt das Popover beim Öffnen ins Bild (FilterPopover.viewRevealed).
                | CaptureInDialog -> CategoryPicker.popoverInPanelRevealed targetOpen (capturePickerProps dispatch model form)
                | CaptureInSheet -> ()
            ]
        ]

        Html.label [
            prop.className "flex flex-col gap-1.5"
            prop.children [
                label "Payee"
                Input.text
                    { Input.textInputDefaults with
                        Value = form.Payee
                        OnChange = fun text -> dispatch (SetCapturePayee text)
                        OnBlur = Some (fun _ -> dispatch CommitCapturePayee)
                        Placeholder = "optional"
                        ClassName = Some "w-full" }
            ]
        ]

        // Am Handy steht Memo unter dem Datum (ledger-yhemh, in ledger-ea70s): nebeneinander war das Memo zu schmal.
        // Ab md nebeneinander wie in der Skizze.
        Html.div [
            prop.className "grid grid-cols-1 gap-3 md:grid-cols-2"
            prop.children [
                Html.div [
                    prop.className "flex min-w-0 flex-col gap-1.5"
                    prop.custom ("data-testid", "capture-date")
                    prop.children [
                        // "Heute" und "Gestern" stehen in der Zeile der Bezeichnung: ein Tipp setzt das Datum.
                        Html.div [
                            prop.className "flex items-center justify-between gap-2"
                            prop.children [
                                label "Datum"
                                match capture.Defaults with
                                | Success defaults ->
                                    Html.div [
                                        prop.className "flex items-center gap-3"
                                        prop.children [
                                            Button.quietText "capture-date-today" "Heute" (fun () -> dispatch (SetCaptureDate defaults.Today))
                                            Button.quietText
                                                "capture-date-yesterday"
                                                "Gestern"
                                                (fun () -> dispatch (SetCaptureDate (defaults.Today.AddDays -1.0)))
                                        ]
                                    ]
                                | _ -> ()
                            ]
                        ]
                        DateField.view
                            { Value = Some form.Date
                              OnChange =
                                fun entry ->
                                    match entry with
                                    | DateField.ValidDate d -> dispatch (SetCaptureDate d)
                                    | _ -> ()
                              Invalid = false
                              Disabled = false
                              ReadOnly = false
                              Label = "Datum" }
                    ]
                ]
                Html.label [
                    prop.className "flex min-w-0 flex-col gap-1.5"
                    prop.children [
                        // Ab md gleich hoch wie die Zeile "Datum" mit "Heute" und "Gestern", damit die Felder bündig stehen.
                        Html.div [ prop.className "flex items-center md:min-h-9"; prop.children [ label "Memo" ] ]
                        Input.text
                            { Input.textInputDefaults with
                                Value = form.Memo
                                OnChange = fun text -> dispatch (SetCaptureMemo text)
                                Placeholder = "optional"
                                ClassName = Some "w-full" }
                    ]
                ]
            ]
        ]

        // Fehler vom Server bleiben gesammelt über "Buchung anlegen": `validateLedgerBooking` liefert sie ohne Feldbezug.
        match capture.ServerErrors with
        | [] -> ()
        | errors ->
            Html.div [
                prop.custom ("data-testid", "capture-errors")
                prop.role "alert"
                prop.className "flex flex-col gap-1"
                prop.children [ for e in errors -> ErrorDisplay.inline' e ]
            ]
    ]

/// Der Inhalt des Formulars je nach Ladezustand der Vorbelegung.
let private captureBody (dispatch: Msg -> unit) (model: Model) (capture: Capture.CaptureState) (place: CapturePlace) : ReactElement list =
    match capture.Defaults with
    | Success _ -> captureFields dispatch model capture place
    | Failure message ->
        [ ErrorDisplay.cardWithTitle "Vorbelegung konnte nicht geladen werden" message (Some (fun () -> dispatch RetryCaptureDefaults)) ]
    | NotAsked | Loading -> [ Loading.centered "Formular wird vorbereitet…" ]

let private captureSubmitButton (dispatch: Msg -> unit) (capture: Capture.CaptureState) (fullWidth: bool) : ReactElement =
    Button.view
        { Button.defaultProps with
            Text = captureSubmitLabel
            Variant = Button.Primary
            // Beschäftigt statt gesperrt, damit der Fokus am Knopf bleibt (Styleguide §5); ein zweiter
            // Tipp tut nichts (`SubmitCapture` prüft `Saving`).
            IsBusy = capture.Saving
            IsDisabled = not (RemoteData.isSuccess capture.Defaults)
            ClassName = (if fullWidth then Some "w-full" else None)
            OnClick = fun () -> dispatch SubmitCapture }

let private captureCancelButton (dispatch: Msg -> unit) (capture: Capture.CaptureState) (fullWidth: bool) : ReactElement =
    Button.view
        { Button.defaultProps with
            Text = captureCancelLabel
            Variant = Button.Outline
            IsDisabled = capture.Saving
            ClassName = (if fullWidth then Some "w-full" else None)
            OnClick = fun () -> dispatch CloseCapture }

/// Das Formular im Panel rechts (PC): Kopf mit Titel und Schließen, die Felder, unten haftend
/// "Abbrechen" und "Buchung anlegen".
let private capturePanelView (dispatch: Msg -> unit) (model: Model) (capture: Capture.CaptureState) : ReactElement =
    Detail.panel
        captureTitle
        [ Detail.formHead captureTitle "Formular schließen" capture.Saving (fun () -> dispatch CloseCapture)
          Detail.scrollBody [
              Html.div [
                  prop.custom ("data-testid", "capture-form")
                  prop.className "flex flex-col gap-3 px-4 py-3"
                  prop.children (captureBody dispatch model capture CaptureInPanel)
              ]
              // Wie im Prototyp direkt unter den Feldern, nicht am unteren Rand des Panels: das Panel reicht
              // ohne Scrollen der Seite unter den Fensterrand.
              Html.div [
                  prop.className $"grid grid-cols-2 gap-3 border-t {Tokens.Borders.default'} px-4 py-3"
                  prop.children [ captureCancelButton dispatch capture true; captureSubmitButton dispatch capture true ]
              ]
          ] ]

/// Die Unterzeile der Auswahlen aus dem Formular im Sheet (Skizze `capAcct`).
let captureChoiceSubtitle = "Für die neue Buchung"

/// Das Formular als Sheet (Handy, schmales Fenster). Die Auswahl von Konto und Kategorie ersetzt den Inhalt des
/// Sheets (§7.15, ledger-ea70s); Wahl oder Schließen führt zum Formular zurück, die Eingaben stehen im Modell und
/// bleiben.
let private captureSheetView (dispatch: Msg -> unit) (model: Model) (capture: Capture.CaptureState option) (open': bool) : ReactElement =
    let isOpen = open' && capture.IsSome
    let picker = capture |> Option.bind (fun c -> if isOpen then c.Picker else None)
    BottomSheet.view
        { IsOpen = isOpen
          OnClose = fun () -> dispatch (if picker.IsSome then CloseCapturePicker else CloseCapture)
          Title =
            (match picker, capture with
             | Some Capture.CaptureAccountPicker, _ -> captureAccountPickerTitle
             | Some Capture.CaptureTargetPicker, Some c -> CategoryPicker.sheetTitle (capturePickerProps dispatch model c.Form).Accounts
             | _ -> captureTitle)
          Subtitle = (if picker.IsSome then Some captureChoiceSubtitle else None)
          Footer =
            match picker, capture with
            | None, Some c ->
                Some [ Html.div [
                           prop.className "grid w-full grid-cols-2 gap-3"
                           prop.children [ captureCancelButton dispatch c true; captureSubmitButton dispatch c true ]
                       ] ]
            | _ -> None }
        [ match capture with
          | Some c when isOpen ->
              match c.Picker with
              | Some Capture.CaptureAccountPicker ->
                  // Nach der Wahl steht das Formular an derselben Stelle: ein später synthetischer Klick soll dort
                  // nichts treffen (ADR 0005, wie `CategoryPicker.sheetContent`).
                  let choosing msg =
                      match msg with
                      | ChooseCaptureAccount _ -> Viewport.swallowNextClick 350
                      | _ -> ()
                      dispatch msg
                  captureAccountList choosing model c
              | Some Capture.CaptureTargetPicker -> CategoryPicker.sheetContent (capturePickerProps dispatch model c.Form)
              | None ->
                  Html.div [
                      prop.custom ("data-testid", "capture-form")
                      prop.className "flex flex-col gap-4"
                      prop.children (captureBody dispatch model c CaptureInSheet)
                  ]
          | _ -> () ]

/// Die Form des Formulars außerhalb des Kassenbuchs (ledger-s1weh): ab 1024 px (`panelViewport`, dieselbe Schwelle
/// wie das Panel im Kassenbuch, §7.6) ein Dialog in der Mitte, darunter das Sheet.
let captureShellFor (panelViewport: bool) : CaptureShell =
    if panelViewport then CaptureDialog else CaptureSheet

/// Esc am Dokument, solange der Dialog „Neue Buchung“ offen ist: auch wenn der Fokus noch auf „Buchung“ im Kopf
/// der Übersicht steht. Aus einem Feld verlässt Esc erst das Feld (`captureEscapeFor`). Ein Ereignis, das schon jemand behandelt hat (Popover der Kategorie), bleibt unberührt.
[<ReactComponent>]
let private CaptureDialogEscape (capture: Capture.CaptureState) (dispatch: Msg -> unit) =
    let latest = React.useRef (capture, dispatch)
    latest.current <- (capture, dispatch)
    React.useEffect (
        (fun () ->
            let onKey (event: Browser.Types.Event) =
                let e = event :?> Browser.Types.KeyboardEvent
                if e.key = "Escape" && not e.defaultPrevented then
                    let current, send = latest.current
                    e.preventDefault ()
                    let target = e.target :?> Browser.Types.HTMLElement
                    let fromField = not (isNull target) && isTypingTarget target.tagName target.isContentEditable
                    match captureEscapeFor fromField current with
                    | LeaveField -> target.blur ()
                    | CaptureStep step -> send step
            Browser.Dom.document.addEventListener ("keydown", onKey)
            React.createDisposable (fun () -> Browser.Dom.document.removeEventListener ("keydown", onKey))),
        [||]
    )
    Html.none

/// Das Formular als Dialog in der Mitte (Übersicht ab 1024 px, ledger-s1weh). Felder, Kontoliste und Popover der
/// Kategorie wie im Panel des Kassenbuchs; Esc, Hintergrund und Schließen schließen wie „Abbrechen“.
let private captureDialogView (dispatch: Msg -> unit) (model: Model) (capture: Capture.CaptureState) : ReactElement =
    React.fragment [
        CaptureDialogEscape capture dispatch
        Modal.view
            { Modal.defaultProps with
                IsOpen = true
                OnClose = fun () -> dispatch CloseCapture
                Size = Modal.Large
                Title = Some captureTitle }
            [ Modal.body [
                  Html.div [
                      prop.custom ("data-testid", "capture-form")
                      prop.className "flex flex-col gap-3"
                      prop.children (captureBody dispatch model capture CaptureInDialog)
                  ]
              ]
              Modal.footer [
                  Html.div [
                      prop.className "grid w-full grid-cols-2 gap-3"
                      prop.children [ captureCancelButton dispatch capture true; captureSubmitButton dispatch capture true ]
                  ]
              ] ]
    ]

/// Das Formular außerhalb des Kassenbuchs (Übersicht, ledger-s1weh): ab 1024 px als Dialog, darunter als Sheet.
/// Die Breite wird einmal je Render gelesen (wie `isInspectorPanelViewport` sonst, ADR 0043): beide Formen
/// portalen nach `document.body`, deshalb steht genau eine im DOM.
let captureSheet (dispatch: Msg -> unit) (model: Model) : ReactElement =
    match captureShellFor (Viewport.isInspectorPanelViewport ()), model.Capture with
    | CaptureDialog, Some capture -> captureDialogView dispatch model capture
    | CaptureDialog, None -> Html.none
    | CaptureSheet, _ -> captureSheetView dispatch model model.Capture true

// Wie `Viewport.observeHeight`, nur für die Breite: die Tabellenbreite entscheidet, welche
// Spalten sichtbar sind (`visibleColumns`, ledger-qh9qx).
[<Emit(
    "(function(el, cb) { var o = new ResizeObserver(function() { cb(el.getBoundingClientRect().width); }); o.observe(el); return function() { o.disconnect(); }; })($0, $1)"
)>]
let private observeWidth (element: Browser.Types.Element) (onResize: float -> unit) : (unit -> unit) =
    jsNative

/// Breite, die gilt, bevor der erste Messwert da ist (Tabellenbreite bei 1440px).
let private assumedTableWidth = 1090.0

/// Die Spalte mit Filterleiste, Summenzeile, Lösch-Umschalter und Liste (dritte
/// Iteration, ledger-j8xq4, Verifier-Fund): der sticky Tabellenkopf
/// (`.table-sticky-thead thead`, `styles.css`) braucht die tatsächlich gerenderte
/// Höhe der sticky Filterleiste darüber, nicht eine geschätzte Konstante, sonst
/// kollidieren beide, sobald die Leiste bei aktivem Filter zweizeilig umbricht
/// (`md:flex-wrap`, `ChipBar.bar`) oder auf einer schmaleren Breite anders hoch
/// ausfällt als angenommen. Ein `ResizeObserver` auf der Leiste selbst schreibt
/// ihre Höhe als CSS-Variable `--ledger-filter-bar-height` auf diese Spalte;
/// `styles.css` liest sie mit einem Fallback von `0px`, sodass jede sticky
/// Tabelle ohne eine solche Spalte darüber (der dokumentierte Vertrag von
/// `Table.fs`s `Sticky`-Feld) unverändert direkt unter der Navbar klebt.
///
/// Vierte Iteration, Verifier-Fund: die Kinder dieser Spalte sind eine
/// keylose Liste, an deren erster Stelle bedingt der Spiegel-Lauf-Hinweis
/// steht. Wird `model.MirrorRunning` wahr, NACHDEM diese Komponente schon
/// gemountet war, verschiebt sich die Leiste von Kindposition 0 auf 1; React
/// vergibt den DOM-Knoten an Position 0 an den (typgleichen) Hinweis-`div`
/// weiter und legt für die Leiste an Position 1 einen neuen Knoten an. Ein
/// Objekt-Ref (`React.useRef`) bekäme diesen neuen Knoten zwar korrekt
/// zugewiesen, aber ein `useEffect` mit `[||]`-Abhängigkeiten, das den
/// `ResizeObserver` beim allerersten Mount an den DAMALIGEN Knoten bindet,
/// bemerkt den späteren Wechsel nie: er beobachtet danach den wiederverwendeten
/// Knoten weiter, der jetzt der Hinweis ist, nicht die Leiste. Der
/// Callback-Ref unten löst das, weil React ihn bei JEDEM Knotenwechsel neu
/// aufruft (mit dem neuen Knoten, oder mit `null` beim Unmount): er trennt
/// zuerst einen zuvor gemerkten Observer, dann beobachtet er den neuen
/// Knoten, falls einer da ist.
[<ReactComponent>]
let private FilterBarColumn
    (today: DateTime)
    (dispatch: Msg -> unit)
    (model: Model)
    (categories: YnabCategory list)
    (accounts: LedgerAccountView list)
    (balances: RemoteData<LedgerAccountBalanceView list>)
    (isAllAccounts: bool)
    (ruleForm: EntryRuleFormSlot)
    (fetch: unit -> unit)
    =
    let barHeight, setBarHeight = React.useState 0.0
    // 0 = noch nicht gemessen, dann gilt `assumedTableWidth`.
    let measuredWidth, setMeasuredWidth = React.useState 0.0
    let tableWidth = if measuredWidth > 0.0 then measuredWidth else assumedTableWidth
    // Unter 1024 px gilt die Handy-Form (ledger-d9z3n): Kontowahl, Suche, Filter-Knopf, Handy-Liste, Detail als
    // Sheet. Zur Renderzeit gelesen; die Spalte zeichnet bei jeder Größenänderung neu (`observeWidth`).
    let handyForm = not (Viewport.isInspectorPanelViewport ())
    // Ab 1024 px steht rechts immer das Panel (ledger-cnaz3, §7.6): die Tabelle bekommt die Breite daneben, Memo erst
    // ab 1500 px Fensterbreite; reicht die Breite für keine Stufe, steht die Kompaktliste. Das Panel reserviert seinen
    // Platz als Innenabstand, die gemessene Außenbreite der Spalte bleibt gleich.
    let tableLayout =
        if handyForm then CompactList
        else visibleColumnsBesidePanel Browser.Dom.window.innerWidth tableWidth isAllAccounts
    let detailForm = if handyForm then DetailSheet else DetailPanel
    let panelShown = not handyForm
    React.useEffect (
        (fun () -> if model.DetailForm <> detailForm then dispatch (DetailFormChanged detailForm)),
        [| box detailForm; box model.DetailForm |]
    )
    // Bedienung der Liste: Layout aus der Breite, dazu Zeigerart und Fensterbreite (zur
    // Renderzeit gelesen, ein Gerätewechsel im Betrieb wird nicht nachgeführt). Der Hinweis
    // und sein Abstandshalter lesen nur das Modellfeld (ledger-m6zpd).
    let listInteraction =
        listInteractionFor tableLayout (Viewport.isFinePointer ()) (Viewport.isDesktopViewport ())
    React.useEffect (
        (fun () -> if model.ListInteraction <> listInteraction then dispatch (ListInteractionChanged listInteraction)),
        [| box listInteraction; box model.ListInteraction |]
    )
    let disconnectRef = React.useRef<(unit -> unit) option> None
    let widthDisconnectRef = React.useRef<(unit -> unit) option> None

    let columnCallbackRef =
        React.useCallback (
            (fun (element: Browser.Types.Element) ->
                match widthDisconnectRef.current with
                | Some disconnect ->
                    disconnect ()
                    widthDisconnectRef.current <- None
                | None -> ()

                if not (isNull (box element)) then
                    widthDisconnectRef.current <- Some (observeWidth element setMeasuredWidth)),
            [||]
        )

    let barCallbackRef =
        React.useCallback (
            (fun (element: Browser.Types.Element) ->
                match disconnectRef.current with
                | Some disconnect ->
                    disconnect ()
                    disconnectRef.current <- None
                | None -> ()

                if not (isNull (box element)) then
                    disconnectRef.current <- Some (Viewport.observeHeight element setBarHeight)),
            [||]
        )

    Html.div [
        prop.className "flex-1 min-w-0 relative"
        prop.ref columnCallbackRef
        prop.style [ style.custom ("--ledger-filter-bar-height", sprintf "%.0fpx" barHeight) ]
        prop.children [
            // Das Sheet portalt nach `document.body`; es steht hier, weil die Form hier bekannt ist.
            detailSheetView today dispatch model ruleForm (model.DetailFor.IsSome && detailForm = DetailSheet)
            captureSheetView dispatch model model.Capture (detailForm = DetailSheet)
            if panelShown then
                match panelContentFor model, model.Capture, detailEntryOf model with
                | PanelCapture, Some capture, _ -> capturePanelView dispatch model capture
                | PanelSelectionSummary, _, _ -> selectionSummaryPanel dispatch model
                | PanelDetail, _, Some entry -> detailPanelOrRuleForm today dispatch model ruleForm entry
                | _ -> Detail.emptyPanel emptyPanelText
            Html.div [
                // Neben dem Panel macht die Spalte Platz; die Mindesthöhe hält das haftende
                // Panel auch bei kurzer Liste im Bild.
                prop.className (if panelShown then $"{Tokens.PanelWidths.columnInset} md:min-h-[calc(100vh-7rem)]" else "")
                prop.children [
            Primitives.Stack.md [
                // Der Lauf blockiert die Liste nicht: sie bleibt
                // stehen, der Hinweis steht daneben (ADR 0014). Ohne Lauf hält
                // `Html.none` den Platz: die Kinder sind keylos, ein wegfallender
                // Hinweis verschöbe sonst jede folgende Stelle, React baute die
                // Liste neu auf und die gescrollte Zeile spränge (ledger-ct3cx).
                if model.MirrorRunning then
                    Loading.inlineWithText "Spiegel läuft. Die Liste bleibt so lange stehen."
                else
                    Html.none

                // Eine Stelle für beide Formen, damit die keylosen Kinder ihre Plätze behalten.
                if handyForm then
                    handyControls dispatch model accounts balances categories isAllAccounts
                else
                    filterChipBar dispatch model categories (sortChipVisible tableLayout) isAllAccounts (Some barCallbackRef)
                summaryOrSelectionRow accounts categories model
                // Unter 1024 px steht „Gelöschte zeigen“ im Filter-Sheet.
                if handyForm then Html.none else deletedToggleRow dispatch model
                orderStaleBar dispatch model
                entryList dispatch fetch accounts isAllAccounts handyForm tableLayout model
                selectionBarSpacers model
            ]
                ]
            ]
        ]
    ]

/// Der Tab „Regeln“ (categorization-n3sxw): Untertitel, Knöpfe im Kopf und Inhalt kommen vom Regel-Tab, der Root
/// reicht sie herein (das Kassenbuch kennt den Regel-Baustein nicht).
type RulesTabParts = {
    Subtitle: string option
    Actions: ReactElement list
    Content: ReactElement
}

/// Welcher Tab im Kassenbuch offen ist.
type LedgerTab =
    | BookingsTab
    | RulesTab of RulesTabParts

/// Die Tabs „Buchungen“ und „Regeln“ (ADR 0087 Punkt 9): der offene Tab und die beiden Wechsel (Routen).
type LedgerTabs = {
    Active: LedgerTab
    ShowBookings: unit -> unit
    ShowRules: unit -> unit
    /// „Abrufen“ im Kopf (ledger-d9z3n): derselbe Abruf wie auf der Übersicht.
    Fetch: unit -> unit
    /// Die Zahl „Zu prüfen“ im ganzen Kassenbuch, wie die Übersicht sie kennt (ledger-cnaz3, „n durchgehen“).
    OverviewReviewCount: int
    /// „n durchgehen“ im Kopf (ledger-cnaz3): öffnet das Durchgehen.
    StartReview: unit -> unit
}

/// Was im Kopf des Kassenbuchs steht, ohne Inhalt (categorization-n3sxw). Rein, damit die Zusicherungen unter .NET
/// prüfbar sind: wo „+ Buchung“ steht und dass „Jetzt spiegeln“ auf beiden Tabs bleibt (Abweichung (g) der Task).
type LedgerHeaderAction =
    /// „Abrufen“ als Symbolknopf, auf „Buchungen“ unter 1024 px (ledger-d9z3n).
    | FetchIcon
    /// „Jetzt spiegeln“ als Symbolknopf: auf „Buchungen“ unter 1024 px, auf „Regeln“ bis 1279 px.
    | MirrorIcon
    /// „Jetzt spiegeln“ mit Text: auf „Buchungen“ ab 1024 px, auf „Regeln“ ab `xl`.
    | MirrorButton
    /// „Abrufen“ mit Symbol und Text, auf „Buchungen“ ab 1024 px (ledger-cnaz3).
    | FetchButton
    /// „n durchgehen“, auf „Buchungen“ ab 1024 px und nur bei n > 0 (ledger-cnaz3).
    | ReviewButton of count: int
    /// „+ Buchung“, ab 1024 px (darunter der FAB).
    | CaptureBooking
    /// Die Aktionen des Tabs selbst, auf „Regeln“ die Hauptaktion „+ Neue Regel“.
    | TabActions

/// Die Kopf-Aktionen je Tab, in der Reihenfolge der Kopfzeile. Eine Hauptaktion je Ansicht: auf „Buchungen“
/// „+ Buchung“, auf „Regeln“ „+ Neue Regel“ aus `TabActions`, dort gibt es kein „+ Buchung“. Auf „Buchungen“ am PC
/// (ledger-cnaz3): „Jetzt spiegeln“, „Abrufen“, „n durchgehen“ (nur bei n > 0) und „+ Buchung“; die Symbolknöpfe
/// stehen nur unter 1024 px.
let headerActionsFor (onRules: bool) (reviewCount: int) : LedgerHeaderAction list =
    if onRules then [ MirrorIcon; MirrorButton; TabActions ]
    else
        [ FetchIcon; MirrorIcon; MirrorButton; FetchButton
          if reviewCount > 0 then ReviewButton reviewCount
          CaptureBooking ]

/// Der Text von „n durchgehen“.
let reviewButtonLabel (count: int) : string = $"{Money.formatCount count} durchgehen"

/// Die Zahl hinter „n durchgehen“: die zu prüfenden Buchungen im ganzen Kassenbuch. In „Alle Konten“ mit geladener
/// Liste ist es die Zahl des Kassenbuchs selbst (sie folgt jedem Prüfen sofort), in einem Konto die der Übersicht.
let headerReviewCount (isAllAccounts: bool) (entriesLoaded: bool) (ledgerCount: int) (overviewCount: int) : int =
    if isAllAccounts && entriesLoaded then ledgerCount else overviewCount

/// Die Zeile unter dem Titel am PC (ledger-cnaz3, Prototyp Version 17 `ledgerDesk`): das Konto oder „Alle Konten“,
/// sein Saldo (fehlt, solange die Stände laden oder nicht lesbar sind) und dahinter der Stand des Spiegels (BB).
type LedgerHeaderLine = {
    Account: string
    Balance: Money option
    Mirror: string option
}

let ledgerHeaderLine
    (accounts: LedgerAccountView list)
    (balances: RemoteData<LedgerAccountBalanceView list>)
    (selected: AccountFilter option)
    (mirror: string option)
    : LedgerHeaderLine =
    let cards = buildAccountCards accounts balances
    let active =
        cards
        |> List.tryFind (fun c -> Some c.Filter = selected)
        |> Option.orElse (List.tryHead cards)
    { Account = active |> Option.map (fun c -> c.Label) |> Option.defaultValue "Alle Konten"
      Balance =
        active
        |> Option.bind (fun c -> match c.Balance with BalanceShown money -> Some money | _ -> None)
      Mirror = mirror }

/// Die Zeile als Element: am PC „Konto · Saldo · Gespiegelt …“ mit dem Saldo in Mono, unter 1024 px nur der Stand
/// des Spiegels (Konto und Saldo stehen dort im Auswahlfeld „Konto“).
let private ledgerHeaderLineView (line: LedgerHeaderLine) : ReactElement =
    Html.span [
        prop.custom ("data-testid", "ledger-header-line")
        prop.children [
            Html.span [
                prop.className "hidden lg:inline"
                prop.children [
                    Html.span [ prop.text line.Account ]
                    match line.Balance with
                    | Some money ->
                        Html.span [ prop.text " · " ]
                        Money.balanceInline "ledger-header-balance" false "" money.Amount money.Currency
                    | None -> ()
                    match line.Mirror with
                    | Some _ -> Html.span [ prop.text " · " ]
                    | None -> ()
                ]
            ]
            match line.Mirror with
            | Some text -> Html.span [ prop.text text ]
            | None -> ()
        ]
    ]

/// Springt ohne Animation (`scroll-behavior: smooth` der Seite gilt hier nicht): die Liste soll stehen wie vorher.
[<Emit("window.scrollTo({ top: $0, behavior: 'instant' })")>]
let private scrollWindowTo (y: float) : unit = jsNative

/// Setzt den gemerkten Scrollstand (`BookingsScroll` im Modell), sobald die Buchungen wieder stehen (nach dem Aufbau,
/// vor dem Zeichnen), und verbraucht ihn.
[<ReactComponent>]
let private BookingsScrollRestore (scroll: float option) (dispatch: Msg -> unit) =
    React.useLayoutEffectOnce (fun () ->
        match scroll with
        | Some y ->
            scrollWindowTo y
            dispatch BookingsScrollDropped
        | None -> ())
    Html.none

let private tabsBar (tabs: LedgerTabs) (dispatch: Msg -> unit) =
    let onBookings = match tabs.Active with BookingsTab -> true | RulesTab _ -> false
    Html.div [
        prop.className "md:max-w-[320px] mb-3"
        prop.children [
            Tabs.bar "ledger-tabs" "Kassenbuch" [
                { Label = "Buchungen"; IsActive = onBookings; OnClick = tabs.ShowBookings }
                { Label = "Regeln"
                  IsActive = not onBookings
                  OnClick =
                    fun () ->
                        if onBookings then dispatch (BookingsScrollRemembered Browser.Dom.window.scrollY)
                        tabs.ShowRules () }
            ]
        ]
    ]

let view (model: Model) (dispatch: Msg -> unit) (tabs: LedgerTabs) (ruleForm: EntryRuleFormSlot) =
    // Der Berliner Kalendertag einmal je Rendern der Seite, für die Unterzeile des Details (`detailSubtitleFor`).
    let today = Client.BerlinDate.today ()
    let accounts = model.Accounts |> RemoteData.toOption |> Option.map (fun a -> a.Accounts) |> Option.defaultValue []
    let balances = model.Balances
    let isAllAccounts = model.SelectedFilter = Some AllAccounts
    let categories = model.Categories |> RemoteData.toOption |> Option.defaultValue []

    // „Jetzt spiegeln“ bleibt im Kopf, auch auf dem Tab „Regeln“ (Abweichung (g)): am Handy als Symbol-Knopf, am PC
    // mit Text. Was wo steht, legt `headerActionsFor` fest. Auf „Regeln“ steht ab 1024 px das Panel daneben; bis 1279 px
    // ist die Listenspalte zu schmal für Titel, „Jetzt spiegeln“ mit Text und „+ Neue Regel“ (Sweep `regeln` @ 1024,
    // Invariante 5). Dort bleibt es bis `xl` der Symbol-Knopf, wie die Karte statt der Tabelle (Abweichung (f)).
    let headerActions (tabActions: ReactElement list) =
        let onRules = match tabs.Active with RulesTab _ -> true | BookingsTab -> false
        // Auf „Buchungen“ gilt die Handy-Form bis 1023 px (ledger-d9z3n).
        let mirrorIconClass = if onRules then "xl:hidden" else "lg:hidden"
        let mirrorButtonClass = if onRules then "hidden xl:inline-flex" else "hidden lg:inline-flex"
        let reviewCount =
            headerReviewCount
                (model.SelectedFilter = Some AllAccounts)
                (match model.Entries with Success _ -> true | _ -> false)
                model.NeedsReviewCount
                tabs.OverviewReviewCount
        headerActionsFor onRules reviewCount
        |> List.collect (function
            // Symbolknöpfe wie auf der Übersicht (ledger-gy77x): ohne Rand, Symbol 22 px, der Name über `aria-label`.
            // Die Hülle blendet sie ab `lg` bzw. `xl` aus.
            | FetchIcon ->
                [ Html.div [
                    prop.className "lg:hidden"
                    prop.children [ PageHeader.iconButton "Abrufen" Icons.fetch tabs.Fetch ]
                  ] ]
            | MirrorIcon ->
                [ Html.div [
                    prop.className mirrorIconClass
                    prop.children [
                        PageHeader.iconButtonBusy "Jetzt spiegeln" Icons.sync model.MirrorRunning (fun () -> dispatch StartMirror)
                    ]
                  ] ]
            | MirrorButton ->
                [ Button.view {
                    Button.defaultProps with
                        Text = if model.MirrorRunning then "Spiegel läuft…" else "Jetzt spiegeln"
                        Variant = Button.Secondary
                        // `IsBusy` statt `IsLoading`: der Fokus bleibt am Knopf, ein zweiter Druck tut nichts
                        // (`StartMirror` läuft während eines Spiegels ins Leere, ledger-gy77x).
                        IsBusy = model.MirrorRunning
                        ClassName = Some mirrorButtonClass
                        OnClick = (fun () -> dispatch StartMirror)
                  } ]
            | FetchButton ->
                [ Button.view {
                    Button.defaultProps with
                        Text = "Abrufen"
                        Icon = Some (Icons.fetch Icons.SM Icons.Default)
                        Variant = Button.Secondary
                        ClassName = Some "hidden lg:inline-flex"
                        OnClick = tabs.Fetch
                  } ]
            | ReviewButton count ->
                [ Button.view {
                    Button.defaultProps with
                        Text = reviewButtonLabel count
                        Variant = Button.Secondary
                        ClassName = Some "hidden lg:inline-flex"
                        OnClick = tabs.StartReview
                  } ]
            | CaptureBooking ->
                [ Button.view {
                    Button.defaultProps with
                        Text = captureOpenLabel
                        Icon = Some (Icons.plus Icons.SM Icons.Default)
                        Variant = Button.Primary
                        AriaLabel = Some captureTitle
                        ClassName = Some "hidden lg:inline-flex"
                        OnClick = (fun () -> dispatch OpenCapture)
                  } ]
            | TabActions -> tabActions)

    // Ruhige Liste (ADR 0049 Punkt 9): schlanker Seitenkopf, Liste füllt die Breite.
    // Die Lena-Farbe steht nur im Flag-Symbol (ADR 0087 Punkt 8, `data-lena-flag` in styles.css). Der
    // Rahmen ist `contents`, er ändert das Layout nicht.
    Html.div [
      prop.className "contents"
      yield! Badge.lenaFlagScope (lenaColorOf model.LenaFlagColor)
      prop.children [
       Primitives.Container.full [
        match tabs.Active with
        | RulesTab parts ->
            // Ab 1024 px steht rechts das Panel: der Kopf endet wie die Liste links davon, „+ Neue Regel“ steht am
            // rechten Rand der Listenspalte (Prototyp `rhead`). Der Untertitel nur am PC.
            Html.div [
                prop.className Tokens.PanelWidths.headInset
                prop.children [
                    PageHeader.view
                        { PageHeader.defaultProps with
                            Title = "Kassenbuch"
                            Subtitle = parts.Subtitle
                            SubtitleDesktopOnly = true
                            Actions = headerActions parts.Actions
                            TitleStyle = PageHeader.Calm }
                ]
            ]
            tabsBar tabs dispatch
            parts.Content
        | BookingsTab ->
            // Unter dem Titel am PC Konto und Saldo, dahinter der Stand des Spiegels (ledger-cnaz3, Abweichung (a)).
            PageHeader.view
                { PageHeader.defaultProps with
                    Title = "Kassenbuch"
                    SubtitleView =
                        Some (ledgerHeaderLineView (ledgerHeaderLine accounts balances model.SelectedFilter (lastRunSubtitle model.LastMirrorRun)))
                    Actions = headerActions []
                    TitleStyle = PageHeader.Calm }
            tabsBar tabs dispatch

            match model.Accounts with
            | Failure message ->
                ErrorDisplay.cardWithTitle "Konten konnten nicht geladen werden" message (Some (fun () -> dispatch LoadAccounts))
            | Loading -> Loading.centered "Kassenbuch wird geöffnet…"
            | NotAsked | Success _ ->
                // Unter 1024 px gibt es keine Konten-Spalte mehr (ledger-d9z3n): die Kontowahl steht als Feld
                // in der Spalte, ab 1024 px in der Seitenleiste. Der Hinweis zu gescheiterten Ständen steht oben.
                match balancesNoticeView dispatch balances model.BalancesRefreshError with
                | Some card when not (List.isEmpty accounts) -> Html.div [ prop.className "pb-3"; prop.children [ card ] ]
                | _ -> ()
                Html.div [
                    prop.className "flex flex-col"
                    prop.children [ FilterBarColumn today dispatch model categories accounts balances isAllAccounts ruleForm tabs.Fetch ]
                ]
                BookingsScrollRestore model.BookingsScroll dispatch

            KeyboardNavigation model.FocusedRow dispatch (ruleForm (not (Viewport.isInspectorPanelViewport ())))
       ]
      ]
    ]

// ============================================
// Durchgehen Karte für Karte (ledger-v6s7c, Prototyp Version 17 `review()`)
// ============================================

/// Der Wert im Feld „Kategorie“ der Karte.
type ReviewCategoryValue =
    | ReviewCategoryNamed of string
    /// Eine aufgeteilte Buchung: „Aufgeteilt“ ohne Pfeil, geändert wird im Detail.
    | ReviewSplit
    /// Ohne Ziel: gedämpft-gelb „Kategorie wählen“.
    | ReviewMissing

/// Der Wert des Felds: die Kategorie, „→ <Konto>“ bei einem Übertrag, „Aufgeteilt“ bei einer Aufteilung.
let reviewCategoryValue (model: Model) (entry: LedgerEntryView) : ReviewCategoryValue =
    if not (List.isEmpty entry.Splits) then ReviewSplit
    elif entry.IsTransfer && entry.CategoryId.IsNone then
        let name = entry.TransferAccountId |> Option.bind (transferAccountName model) |> Option.defaultValue "Konto"
        ReviewCategoryNamed $"→ {name}"
    else
        match entry.CategoryName with
        | Some name -> ReviewCategoryNamed name
        | None -> ReviewMissing

/// „<Tag> · <Konto> · <Herkunft>“ oben auf der Karte; der Tag wie in der Kassenbuch-Liste.
let reviewMetaText (today: DateTime) (accountName: string) (entry: LedgerEntryView) : string =
    $"{formatEntryDate today entry.Date} · {accountName} · {originLabel entry.Origin}"

/// Die Vorschläge unter dem Feld: höchstens drei, ohne die aktuelle Kategorie, nur bekannte Kategorien.
let reviewSuggestionChips (model: Model) (entry: LedgerEntryView) : YnabCategory list =
    let categories = model.Categories |> RemoteData.toOption |> Option.defaultValue []
    let current = entry.CategoryId |> Option.map YnabCategoryId
    match model.Review with
    | Some review ->
        review.Suggestions
        |> List.filter (fun id -> Some id <> current)
        |> List.choose (fun id -> categories |> List.tryFind (fun c -> c.Id = id))
        |> List.truncate 3
    | None -> []

/// Die Zeile der schon vorhandenen Buchung auf der Duplikat-Karte als (Payee, „<Tag> · <Konto> · <Kategorie>“,
/// Betrag): die aufgelöste Kassenbuch-Buchung, ohne sie der Schnappschuss aus dem Ereignis (Konto dann das der
/// wartenden Buchung).
let reviewMatchRow
    (today: DateTime)
    (accountName: Guid -> string)
    (held: LedgerEntryView)
    (resolved: LedgerEntryView option)
    (duplicate: PossibleDuplicateView)
    : string * string * string =
    let money (m: Money) = Money.formatAmountText false true m.Amount m.Currency
    match resolved with
    | Some e ->
        payeeLabel e, $"{formatEntryDate today e.Date} · {accountName e.AccountId} · {categoryLabel e}", money e.Amount
    | None ->
        let payee = duplicate.MatchPayee |> Option.filter (fun p -> p.Trim() <> "") |> Option.defaultValue "ohne Payee"
        let category = duplicate.MatchCategoryName |> Option.defaultValue "ohne Kategorie"
        payee, $"{formatEntryDate today duplicate.MatchDate} · {accountName held.AccountId} · {category}", money duplicate.MatchAmount

/// Die Tastenleiste am PC (Prototyp `renderDesk`).
let reviewKeys : (string * string) list =
    [ "Enter", "passt"; "S", "später"; "K", "Kategorie"; "L", "Lena"; "Z", "rückgängig"; "Esc", "zurück" ]

let reviewSwipeHintText = "Wischen: rechts „Passt“, links „Später“"
/// Während eines Wischs nach rechts ohne Kategorie in der Zeile unter dem Knopf (ledger-s2cg3, statt eines Toasts).
let reviewNeedsTargetFootText = "Erst Kategorie wählen"

/// Die Zeile der letzten Aktion als Meldung des DS (`Feedback.line`, ledger-pz5wv): Text aus `reviewLastLine`, der
/// Knopf schickt „Rückgängig“ bzw. „Erneut“ der Zeile.
let reviewLastMessage (dispatch: Msg -> unit) (last: ReviewLast) : Feedback.Message =
    let line = reviewLastLine last
    { Tone =
        (match line.Tone with
         | FeedbackOk -> Feedback.Tone.Ok
         | FeedbackWarning -> Feedback.Tone.Warning
         | FeedbackError -> Feedback.Tone.Error
         | FeedbackRunning -> Feedback.Tone.Running
         | FeedbackInfo -> Feedback.Tone.Info)
      Action = line.Action
      Detail = Some line.Detail
      Act =
        line.Button
        |> Option.map (fun label ->
            { Label = label
              OnClick =
                fun () ->
                    match last.Act with
                    | Some (LastRetry _) -> dispatch ReviewRetryLast
                    | _ -> dispatch ReviewUndoLast }) }

/// Was unter dem Hauptknopf steht: während eines Wischs ohne Ziel „Erst Kategorie wählen“, sonst die letzte Aktion,
/// ohne sie `idle` (Wisch-Hinweis am Handy, leer am PC und unter der Duplikat-Karte).
let private reviewFoot (dispatch: Msg -> unit) (review: ReviewSession) (needsTarget: bool) (idle: ReviewCard.FootLine) : ReviewCard.FootLine =
    if needsTarget then ReviewCard.FootNeed reviewNeedsTargetFootText
    else
        match review.Last with
        | Some last -> ReviewCard.FootLast (reviewLastMessage dispatch last)
        | None -> idle

/// Der Weg, auf dem Knöpfe und Tasten die Karte am Handy hinausfliegen lassen (Skizze ledger-vf5h6 `leave`).
type ReviewLeaveSlot = IRefValue<(ReviewCard.SwipeLeave -> (unit -> unit) -> unit) option>

/// Wie eine Taste die Karte verlässt: S nach links mit eingerasteter Fläche, L nach links ohne Fläche, nur wenn Lena
/// gefragt wird (nicht beim Zurücknehmen, nicht gesperrt). Enter fliegt über `Confirming`, alles andere bleibt. Rein.
let reviewLeaveOf (model: Model) (action: ReviewKeyAction) : ReviewCard.SwipeLeave option =
    match action, reviewCardOf model with
    | KeySkip, Some _ -> Some ReviewCard.LeaveSkip
    | KeyLena, Some card when reviewCardKindOf card = NormalCard ->
        let lena = lenaActionFor model.LenaFlagColor card
        if lena.Target = AssignLena && lena.DisabledReason.IsNone then Some ReviewCard.LeaveQuiet else None
    | _ -> None

/// Führt `action` aus; steht die Bühne (Handy), fliegt die Karte vorher hinaus.
let private leaveThen (slot: ReviewLeaveSlot) (kind: ReviewCard.SwipeLeave option) (action: unit -> unit) =
    match kind, slot.current with
    | Some k, Some leave -> leave k action
    | _ -> action ()
let reviewLenaHintText = "ist gefragt. Die Buchung bleibt offen, bis Lena oder du sie prüft."

/// Liegt etwas über der Karte, das Tasten selbst behandelt (Picker, Payee, Memo, Betrag, Split)? Dann
/// gelten die Tasten des Durchgehens nicht (§7.8 Punkt 6).
let reviewOverlayOpen (model: Model) : bool =
    model.CategoryPickerFor.IsSome || model.PayeeEdit.IsSome || model.MemoEdit.IsSome
    || model.AmountEdit.IsSome || model.SplitEditor.IsSome

/// Was eine Taste im Durchgehen auslöst, aus dem Ereignis: nichts aus einem Eingabefeld, mit Strg, Meta
/// oder Alt, bei offener Auswahl über der Karte; Enter auf einem Knopf bleibt dessen eigene Aktivierung. Mit
/// offenem Detail gilt nur Esc (schließt das Detail). Rein.
let reviewKeyFor (model: Model) (tagName: string) (isContentEditable: bool) (modifier: bool) (key: string) : ReviewKeyAction option =
    if reviewOverlayOpen model || modifier || isTypingTarget tagName isContentEditable then None
    elif key = "Enter" && isActivatable tagName then None
    else
        let card =
            match reviewCardOf model with
            | Some entry when not (model.Review |> Option.exists (fun r -> r.DetailShown)) ->
                Some (reviewCardKindOf entry, reviewHasTarget entry)
            | _ -> None
        match model.Review with
        | Some r when r.DetailShown -> if key = "Escape" then Some KeyLeave else None
        | Some r -> reviewKeyAction card (reviewUndoTarget r).IsSome key
        | None -> reviewKeyAction card false key

/// Die Tasten des Durchgehens mit Blick auf ein offenes Regel-Formular (`reviewKeyFor`, `routePageKey`). Rein.
let reviewKeyRoute
    (model: Model)
    (ruleFormOpen: bool)
    (tagName: string)
    (isContentEditable: bool)
    (modifier: bool)
    (key: string)
    : PageKeyTarget<ReviewKeyAction> option =
    routePageKey ruleFormOpen modifier key (reviewKeyFor model tagName isContentEditable modifier key)

[<ReactComponent>]
let private ReviewKeyboard (model: Model) (dispatch: Msg -> unit) (ruleForm: EntryRuleForm option) (leave: ReviewLeaveSlot) =
    let modelRef = React.useRef model
    modelRef.current <- model
    let dispatchRef = React.useRef dispatch
    dispatchRef.current <- dispatch
    let ruleFormRef = React.useRef ruleForm
    ruleFormRef.current <- ruleForm
    React.useEffect (
        (fun () ->
            let handler (e: Browser.Types.Event) =
                let ke = e :?> Browser.Types.KeyboardEvent
                let target = ke.target :?> Browser.Types.HTMLElement
                let tagName = if isNull (box target) then "" else target.tagName
                let editable = not (isNull (box target)) && target.isContentEditable
                if not ke.defaultPrevented then
                    let form = ruleFormRef.current
                    match reviewKeyRoute modelRef.current form.IsSome tagName editable (ke.ctrlKey || ke.metaKey || ke.altKey) ke.key with
                    | Some (PageAction action) ->
                        ke.preventDefault ()
                        leaveThen leave (reviewLeaveOf modelRef.current action) (fun () -> dispatchRef.current (ReviewKey action))
                    | Some RuleFormBack ->
                        ke.preventDefault ()
                        form |> Option.iter (fun f -> f.OnBack ())
                    | None -> ()
            Browser.Dom.document.addEventListener ("keydown", handler)
            { new IDisposable with
                member _.Dispose() = Browser.Dom.document.removeEventListener ("keydown", handler) }),
        [||]
    )
    Html.none

/// Der Name eines Kontos des Kassenbuchs.
let private reviewAccountName (model: Model) (accountId: Guid) : string =
    match model.Accounts with
    | Success view ->
        view.Accounts |> List.tryFind (fun a -> a.Id = accountId) |> Option.map (fun a -> a.Name) |> Option.defaultValue "Konto"
    | _ -> "Konto"

let private amountText (m: Money) = Money.formatAmountText false true m.Amount m.Currency

/// Die normale Karte mit den Knöpfen darunter. Am Handy steht sie auf der Bühne (`ReviewCard.SwipeStage`): sie füllt
/// den Platz bis zu den Knöpfen, wischt mit Fläche darunter, und unter „Passt, weiter“ steht die Zeile mit fester Höhe.
[<ReactComponent>]
let private ReviewNormalCard (dispatch: Msg -> unit) (model: Model) (review: ReviewSession) (entry: LedgerEntryView) (desk: bool) (leave: ReviewLeaveSlot) =
    let today = DateTime.Today
    // Ein Wisch nach rechts ohne Kategorie läuft: Satz in der Zeile, Feld in Gelb (Skizze `needHint`).
    let needsTarget, setNeedsTarget = React.useState false
    let pickerOpen = model.CategoryPickerFor = Some entry.Id && not review.DetailShown
    let pickerProps = detailPickerProps dispatch model entry
    let hasTarget = reviewHasTarget entry
    let lena = lenaActionFor model.LenaFlagColor entry
    let categoryBusy = (model.CategoryRows |> Map.tryFind entry.Id) = Some CategorySending
    // Die Bestätigung am Feld nach Kategorie oder Übertrag auf der Karte (ledger-mf2bg).
    let fieldNote = review.FieldNote |> Option.filter (fun n -> n.EntryId = entry.Id)
    let flashing = fieldNote |> Option.exists (fun n -> n.Flashing)
    let field =
        match reviewCategoryValue model entry with
        | ReviewSplit -> ReviewCard.field "review-category" "Kategorie" (Some "Aufgeteilt") "" false None
        | value ->
            let shown = match value with ReviewCategoryNamed n -> Some n | _ -> None
            let state =
                if needsTarget && not hasTarget then ReviewCard.FieldNeed
                elif flashing then ReviewCard.FieldFlash
                else ReviewCard.FieldRest
            ReviewCard.fieldIn state "review-category" "Kategorie" shown "Kategorie wählen" pickerOpen (Some (fun () -> dispatch ReviewOpenCategory))
    let content = [
            ReviewCard.meta (reviewMetaText today (reviewAccountName model entry.AccountId) entry)
            ReviewCard.payee
                (payeeLabel entry)
                (payeeMissing entry)
                payeeEditLabel
                (payeeEditMsg model entry |> Option.map (fun msg -> fun () -> dispatch msg))
            ReviewCard.amount (amountText entry.Amount)
            Html.div [
                prop.className "relative"
                prop.children [
                    field
                    if desk then CategoryPicker.popoverWithWidth "w-full" pickerOpen pickerProps
                ]
            ]
            match fieldNote with
            | Some note ->
                ReviewCard.fieldNote note.Text note.Available { Label = "Rückgängig"; OnClick = fun () -> dispatch ReviewUndoField }
            | None -> ()
            if entry.CategoryFromOrderId then ReviewCard.caption "review-order-id" "aus Bestellnummer"
            match reviewSuggestionChips model entry with
            | [] -> ()
            | chips ->
                Html.div [
                    prop.custom ("data-testid", "review-suggestions")
                    prop.className "flex flex-col gap-1.5"
                    prop.children [
                        ReviewCard.caption "review-suggested-label" "Vorgeschlagen"
                        Html.div [
                            prop.className "flex flex-wrap gap-1.5"
                            prop.children [
                                for c in chips ->
                                    Html.span [
                                        prop.key (string c.Id)
                                        prop.children [ BottomSheet.chipButtonTouch c.Name (fun () -> dispatch (ReviewPickSuggestion c.Id)) ]
                                    ]
                            ]
                        ]
                    ]
                ]
            for (title, detail, url) in reviewLinks entry do ReviewCard.linkRow title detail url
            // Das Memo nur lesen (ledger-wg42w, C3): bearbeitet wird es in „Details“; ohne Memo kein Block.
            match memoTextOf entry with
            | Some memo -> ReviewCard.memoBlock "review-memo" memo
            | None -> ()
            if entry.Flag = AskLena then
                ReviewCard.note "review-lena-note" [
                    Html.span [ prop.className "inline-flex shrink-0 pt-0.5"; prop.children [ Icons.flagSolid Icons.XS Icons.LenaFlag ] ]
                    Html.span [ ReviewCard.noteName "Lena"; Html.text $" {reviewLenaHintText}" ]
                ]
            // „Regel anlegen“ wie im Detail (ledger-hg5m5), ohne Payee keiner; neben „Details“, damit „Passt, weiter“
            // am Handy im Bild bleibt.
            if (ruleSeedFor entry).IsSome then
                ReviewCard.linksWithDetails
                    "review-rule"
                    ruleFromEntryLabel
                    (fun () -> dispatch (RuleFromEntryRequested entry.Id))
                    (fun () -> dispatch ReviewShowDetails)
            else
                ReviewCard.detailsLink (fun () -> dispatch ReviewShowDetails)
        ]
    let cardProps = [ prop.custom ("data-testid", "review-card") ]
    React.fragment [
        if desk then ReviewCard.card false cardProps content
        else
            ReviewCard.SwipeStage
                { Enabled = true
                  CardKey = string entry.Id
                  Holding = review.Confirming = Some entry.Id
                  OffsetOf = reviewSwipeOffset hasTarget
                  SurfaceOf = reviewSwipeSurface hasTarget
                  OnRelease = fun dx dy -> dispatch (ReviewSwiped (dx, dy))
                  OnSurface = fun surface -> setNeedsTarget (surface = ReviewCard.SurfaceNoTarget)
                  Leave = leave
                  ReturnFrom =
                    review.ReturnFrom
                    |> Option.map (function ReturnFromLeft -> ReviewCard.ReturnLeft | ReturnFromRight -> ReviewCard.ReturnRight)
                  Card = ReviewCard.cardFill false cardProps content }
        Html.div [
            prop.className "grid grid-cols-2 gap-2"
            prop.children [
                Button.view
                    { Button.defaultProps with
                        // Text und Flagge wie im Detail (`detailActionRow`): „Lena fragen“ bzw. „Rückfrage zurücknehmen“
                        // aus `lenaActionFor`, das Umriss-Flag in der Farbe aus den Einstellungen (ledger-gy77x).
                        Text = lena.Label
                        Variant = Button.Secondary
                        Size = Button.Choice
                        // „zurücknehmen“ ist bei 320 px breiter als die halbe Karte: es trennt statt über den Rand zu
                        // laufen.
                        ClassName = Some "min-h-[52px] hyphens-auto"
                        FullWidth = true
                        Icon = Some (Icons.flag Icons.SM Icons.LenaFlag)
                        IsDisabled = lena.DisabledReason.IsSome
                        Title = lena.DisabledReason
                        IsBusy = (model.FlagRows |> Map.tryFind entry.Id) = Some FlagSending
                        // „Lena fragen“ fliegt nach links ohne Fläche, „Rückfrage zurücknehmen“ lässt die Karte stehen.
                        OnClick = fun () -> leaveThen leave (reviewLeaveOf model KeyLena) (fun () -> dispatch ReviewLena) }
                Button.view
                    { Button.defaultProps with
                        Text = "Später"
                        Variant = Button.Secondary
                        Size = Button.Choice
                        ClassName = Some "min-h-[52px]"
                        FullWidth = true
                        OnClick = fun () -> leaveThen leave (Some ReviewCard.LeaveSkip) (fun () -> dispatch ReviewSkip) }
            ]
        ]
        // Der Grund der Sperre steht als Text da, nicht nur im `title` (ledger-dn4vy, wie im Detail).
        match lena.DisabledReason with
        | Some reason -> ReviewCard.meta reason
        | None -> ()
        let primary =
            Button.view
                { Button.defaultProps with
                    Text = (if hasTarget then "Passt, weiter" else "Erst Kategorie wählen")
                    Variant = Button.Primary
                    Size = Button.Choice
                    FullWidth = true
                    Glow = false
                    ClassName = Some "min-h-[54px]"
                    IsDisabled = not hasTarget || categoryBusy
                    IsBusy = review.Confirming = Some entry.Id
                    OnClick = fun () -> dispatch ReviewConfirm }
        // Knopf und Zeile ohne Abstand dazwischen: die Zeile trägt ihn selbst (Skizze `.ph-foot`). Am PC steht die Zeile der
        // letzten Aktion ebenso unter „Passt, weiter“ (ledger-pz5wv), ohne Aktion leer mit derselben Höhe.
        Html.div [
            prop.className "flex shrink-0 flex-col"
            prop.children [
                primary
                ReviewCard.foot (
                    reviewFoot dispatch review (needsTarget && not hasTarget) (if desk then ReviewCard.FootEmpty else ReviewCard.FootHint reviewSwipeHintText)
                )
            ]
        ]
        // Kategorieauswahl am Handy: das Sheet über der Seite (die Karte ist kein Sheet).
        if not desk then
            CategoryPicker.sheet pickerOpen (Some $"{payeeLabel entry} · {amountText entry.Amount}") pickerProps
    ]

/// Die Duplikat-Karte einer wartenden Buchung (ADR 0093): gelber Rand, der Treffer im Kassenbuch, „Ist neu“
/// und „Ist doppelt, verwerfen“. Kein Wischen. Am Handy füllt sie den Platz, darunter steht die leere Zeile, damit die
/// Knöpfe über alle Kartenarten an derselben Stelle stehen.
let private reviewDuplicateCard (dispatch: Msg -> unit) (model: Model) (review: ReviewSession) (entry: LedgerEntryView) (duplicate: PossibleDuplicateView) (desk: bool) =
    let today = DateTime.Today
    let resolved = duplicate.MatchEntryId |> Option.bind (fun id -> review.Matches |> Map.tryFind id)
    let matchPayee, matchMeta, matchAmount = reviewMatchRow today (reviewAccountName model) entry resolved duplicate
    let deciding = review.Deciding = Some entry.Id
    React.fragment [
        (if desk then ReviewCard.card else ReviewCard.cardFill) true [ prop.custom ("data-testid", "review-duplicate-card") ] [
            ReviewCard.metaAttention "Möglicherweise doppelt · noch nicht in YNAB"
            ReviewCard.payee (payeeLabel entry) (payeeMissing entry) "" None
            ReviewCard.amount (amountText entry.Amount)
            ReviewCard.metaSecondary (reviewMetaText today (reviewAccountName model entry.AccountId) entry)
            ReviewCard.sectionHead "Schon im Kassenbuch"
            ReviewCard.matchRow matchPayee matchMeta matchAmount
            ReviewCard.caption "review-duplicate-reason" duplicate.Reason
        ]
        Html.div [
            prop.className "grid grid-cols-2 gap-2"
            prop.children [
                Button.view
                    { Button.defaultProps with
                        Text = "Ist neu"
                        Variant = Button.Secondary
                        Size = Button.Choice
                        ClassName = Some "min-h-[52px]"
                        FullWidth = true
                        IsDisabled = deciding
                        OnClick = fun () -> dispatch ReviewConfirmNew }
                Button.view
                    { Button.defaultProps with
                        Text = "Ist doppelt, verwerfen"
                        Variant = Button.Primary
                        Size = Button.Choice
                        ClassName = Some "min-h-[52px]"
                        FullWidth = true
                        Glow = false
                        IsBusy = deciding
                        OnClick = fun () -> dispatch ReviewDiscard }
            ]
        ]
        // Die Zeile der letzten Aktion steht auch unter der Duplikat-Karte (ledger-pz5wv), ohne Aktion leer.
        ReviewCard.foot (reviewFoot dispatch review false ReviewCard.FootEmpty)
    ]

/// Payee der Karte: am Handy ein Sheet, am PC ein Dialog.
let private reviewEditors (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) (desk: bool) =
    let payeeEdit = payeeEditOf model entry
    React.fragment [
        if desk then
            payeeDialog dispatch model entry
        else
            BottomSheet.view
                { IsOpen = payeeEdit.IsSome
                  OnClose = fun () -> dispatch CancelPayeeEdit
                  Title = "Payee"
                  Subtitle = Some (payeeLabel entry)
                  Footer = None }
                [ match payeeEdit with
                  | Some edit -> PayeePicker.view (payeePickerProps dispatch model entry edit (Viewport.isFinePointer ()))
                  | None -> () ]
    ]

/// Die Endansicht: „Alles geprüft“ in Grün, die Zahl der durchgegangenen Buchungen, „Zur Übersicht“. Bietet die letzte
/// Aktion ein Rückgängig, steht darunter „Letzte rückgängig“ ohne Füllung (Skizze 13, ledger-pz5wv); war sie eine
/// Warnung, steht ihre gelbe Zeile darunter, sonst ginge die Warnung verloren.
let private reviewEnd (dispatch: Msg -> unit) (review: ReviewSession) (count: int) (onBack: unit -> unit) =
    Html.div [
        prop.custom ("data-testid", "review-end")
        prop.className "flex flex-col items-center gap-3"
        prop.children [
            Card.sheetMessage "review-end-message" true "Alles geprüft" (reviewDoneText count)
            Button.view { Button.defaultProps with Text = "Zur Übersicht"; Variant = Button.Primary; OnClick = onBack }
            if (reviewUndoOffered review).IsSome then
                Button.view
                    { Button.defaultProps with
                        Text = "Letzte rückgängig"
                        Variant = Button.Outline
                        OnClick = fun () -> dispatch ReviewUndoLast }
            match review.Last with
            | Some last when (reviewLastLine last).Tone <> FeedbackOk ->
                Html.div [
                    prop.className "w-full max-w-[420px] px-4"
                    // Ohne eigenen Knopf: „Letzte rückgängig“ darüber nimmt sie schon zurück.
                    prop.children [ Feedback.line { reviewLastMessage dispatch last with Act = None } ]
                ]
            | _ -> ()
        ]
    ]

/// Die Seite „Durchgehen“ unter der Übersicht (Styleguide §7.4): Kopf mit „Zurück“ und „i von n“, Fortschritt,
/// Karte und Knöpfe; am PC als Spalte von höchstens 620 px mit Tastenleiste.
[<ReactComponent>]
let ReviewPage (model: Model) (dispatch: Msg -> unit) (onBack: unit -> unit) (ruleForm: EntryRuleFormSlot) =
    let desk = Viewport.isInspectorPanelViewport ()
    let today = Client.BerlinDate.today ()
    // Das Regel-Formular aus der Buchung (ledger-hg5m5): mit Detail ersetzt es dessen Inhalt, sonst steht es über der
    // Karte, am PC als Dialog, am Handy als Sheet. Nach Anlegen oder Abbrechen steht dieselbe Karte da.
    let openRuleForm = ruleForm (not desk)
    let leave : ReviewLeaveSlot = React.useRef None
    let form = if desk then DetailPanel else DetailSheet
    React.useEffect ((fun () -> if model.DetailForm <> form then dispatch (DetailFormChanged form)), [| box form; box model.DetailForm |])
    match model.Review with
    | None -> Html.none
    | Some review ->
        let count = reviewQueueOf model |> Option.map List.length |> Option.defaultValue 0
        let card = reviewCardOf model
        let header =
            PageHeader.top
                (Some (PageHeader.backButtonFor (Some "Zurück zur Übersicht") onBack))
                "Durchgehen"
                None
                [ match reviewProgressText review.Index count with
                  | Some text when card.IsSome ->
                      Html.span [ prop.custom ("data-testid", "review-position"); prop.className $"{Tokens.Fonts.mono} {Tokens.FontSizes.xs} {Tokens.Colors.textMuted}"; prop.text text ]
                  | _ -> () ]
        let body =
            match review.Queue with
            | NotAsked | Loading -> [ ReviewCard.skeleton () ]
            | Failure message ->
                [ ErrorDisplay.cardWithTitle "Buchungen konnten nicht geladen werden" message (Some (fun () -> dispatch ReviewOpened)) ]
            | Success _ ->
                [ ReviewCard.progress (reviewProgressPercent review.Index count) (reviewProgressText review.Index count)
                  match card with
                  | Some entry ->
                      match entry.PossibleDuplicate with
                      | Some duplicate -> reviewDuplicateCard dispatch model review entry duplicate desk
                      | None -> ReviewNormalCard dispatch model review entry desk leave
                      // Mit offenem Detail öffnet der Payee dort (Inhalt des Sheets, am PC ein Dialog), keine zweite
                      // Ebene darüber (ledger-hme9k). Das Memo bearbeitet nur „Details“ (ledger-wg42w).
                      if reviewEditorPlace review = ReviewEditorPlace.OverCard then reviewEditors dispatch model entry desk
                  // Ohne Karte steht die Endansicht: `update` zeigt nach jedem Schritt eine Karte oder überspringt
                  // eine Buchung ohne Stand bis hinter die letzte (`showReviewCardAt`).
                  | None -> reviewEnd dispatch review count onBack ]
        let detail =
            match card with
            | Some entry when desk && review.DetailShown -> detailPanelOrRuleForm today dispatch model ruleForm entry
            | Some _ when desk ->
                match openRuleForm with
                | Some f -> ruleFormDialog f
                | None -> Html.none
            | _ when desk -> Html.none
            // Am Handy steht das Detail-Sheet immer im DOM und fährt über `IsOpen` ein und aus (§6, §7.10).
            | _ -> detailSheetView today dispatch model ruleForm (reviewDetailSheetOpen card review)
        // Am Handy steht das Regel-Sheet über der Karte immer im DOM und fährt über `IsOpen` ein und aus; mit offenem
        // Detail ersetzt das Formular dessen Inhalt (`detailSheetView`).
        let ruleSheetOverCard =
            if desk then Html.none
            else ruleFormSheet (if card.IsSome && not review.DetailShown then openRuleForm else None)
        Html.div [
            prop.custom ("data-testid", "review-page")
            // Am Handy genau so hoch wie der sichtbare Bereich (`review-page-fit`): die Seite scrollt nicht, die Karte nimmt
            // den Rest (ledger-s2cg3).
            prop.className (
                if desk then "relative flex min-h-full flex-col" + (if review.DetailShown then $" {Tokens.PanelWidths.columnInsetPlain}" else "")
                else "review-page-fit relative flex flex-col"
            )
            yield! Badge.lenaFlagScope (lenaColorOf model.LenaFlagColor)
            prop.children [
                Html.div [
                    prop.className "mx-auto flex min-h-0 w-full max-w-[620px] flex-1 flex-col gap-3"
                    prop.children [ header; yield! body ]
                ]
                if desk && card.IsSome then
                    Html.div [ prop.className "mx-auto mt-4 w-full max-w-[620px]"; prop.children [ ReviewCard.keyBar reviewKeys ] ]
                detail
                ruleSheetOverCard
                // Über dem Regel-Formular gelten die Tasten des Durchgehens nicht, Esc führt zur Karte zurück (`reviewKeyRoute`).
                ReviewKeyboard model dispatch openRuleForm leave
            ]
        ]
