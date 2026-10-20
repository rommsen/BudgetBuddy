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

/// Fasst aufeinanderfolgende Buchungen desselben Datums zu einer Gruppe zusammen, in
/// der Reihenfolge der Liste (neueste zuerst) — die Datums-Gruppenkoepfe der
/// Handy-Ansicht (AC6). Bewusst kein globales `List.groupBy`: das wuerde gleiche
/// Datumswerte zusammenziehen, auch wenn sie in der Liste nicht benachbart sind, und
/// die Sortierung der Server-Seite (neueste zuerst) verlassen.
let groupByDate (entries: LedgerEntryView list) : (DateTime * LedgerEntryView list) list =
    entries
    |> List.fold
        (fun acc entry ->
            match acc with
            | (date, items) :: rest when date = entry.Date -> (date, entry :: items) :: rest
            | _ -> (entry.Date, [ entry ]) :: acc)
        []
    |> List.rev
    |> List.map (fun (date, items) -> date, List.rev items)

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

/// Eine Karte der Kontostaende-Leiste: ein Filter (Konto oder "Alle Konten"), ihr
/// Anzeigename und ihr Stand.
type AccountCard = {
    Filter: AccountFilter
    Label: string
    Balance: Money
}

/// Baut die Karten der Kontostaende-Leiste (AC3): "Alle Konten" steht immer zuerst
/// und traegt die Summe aller Konten, danach jedes Konto mit seinem eigenen Stand.
/// Ein Konto ohne (noch geladenen) Stand zeigt Null statt eines Fehlers.
let buildAccountCards (accounts: LedgerAccountView list) (balances: LedgerAccountBalanceView list) : AccountCard list =
    let balanceOf (accountId: Guid) =
        balances
        |> List.tryFind (fun b -> b.AccountId = accountId)
        |> Option.map (fun b -> b.Balance)
        |> Option.defaultValue { Amount = 0m; Currency = "EUR" }

    let total = sumMoney "EUR" (balances |> List.map (fun b -> b.Balance))

    let accountCards =
        accounts
        |> List.map (fun a -> { Filter = OneAccount a.Id; Label = a.Name; Balance = balanceOf a.Id })

    { Filter = AllAccounts; Label = "Alle Konten"; Balance = total } :: accountCards

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

/// Das Status-Kennzeichen "zu prüfen" der PC-Statusspalte (ADR 0032, ADR 0049 Punkt
/// 7): ein kleines Wort in Pink statt eines Badges. Die eigentliche Markierung ist
/// die Statuskante am Zeilenrand (`Table.RowProps.StatusEdge`); das Wort ist der
/// Zusatz, der sie benennt. Quelle bleibt `entry.NeedsReview`.
let private needsReviewStatusBadge () =
    Html.span [
        prop.className $"{Tokens.FontSizes.microPlus} {Tokens.Colors.neonPink} whitespace-nowrap"
        prop.text "zu prüfen"
    ]

/// Trägt die volle Bedeutung eines gekürzten oder reinen Symbol-Kennzeichens der
/// PC-Statusspalte nach (`flagStatusBadge`, `deletedStatusIcon`), als Hover-Hinweis
/// (`title`) und für assistive Technologie (`aria-label`): Verifier-Befund
/// Iteration 2, `ledger-qns4j`. `Badge.view` (Client.DesignSystem.Badge) kennt weder
/// `title` noch `aria-label` als eigenes Prop; ein minimaler Wrapper hier ist der
/// kleinere Eingriff gegenüber einer Erweiterung der von der ganzen App geteilten
/// DS-Komponente um ein Feld, das nur dieser eine Aufrufer braucht.
let private withAccessibleName (fullMeaning: string) (badge: ReactElement) : ReactElement =
    Html.span [
        prop.title fullMeaning
        prop.custom ("aria-label", fullMeaning)
        prop.children [ badge ]
    ]

/// Das Ausnahme-Kennzeichen "in YNAB gelöscht" in der PC-Statusspalte, als Symbol
/// statt als Text (Entscheidung Roman 2026-09-19): der volle Text aus `deletedBadge`
/// passt neben "Zu prüfen" nicht mehr in die auf 128px zurückgesetzte Spalte
/// (`statusColumnWidth`). Romans Vorgabe war ausdrücklich, die Beschriftungen zu
/// kürzen oder durch Symbole zu ersetzen, statt die Spalte wieder zu verbreitern.
/// Die Handy-Zeile (`entryRow`) behält den vollen Text über `deletedBadge`, dort
/// ist keine Spaltenbreite zu wahren. `withAccessibleName` trägt "in YNAB gelöscht"
/// als `title`/`aria-label` nach, weil das Symbol allein keinen Text mehr zeigt.
let private deletedStatusIcon () =
    withAccessibleName "in YNAB gelöscht" (
        Badge.view {
            Badge.defaultProps with
                Text = ""
                Icon = Some (Icons.trash Icons.XS Icons.Error)
                Variant = Badge.Error
                Size = Badge.Small
        })

/// Das Kennzeichen eines gesetzten Flags (ADR 0032): *Rückfrage Lena* violett,
/// *Markiert* orange. `NoFlag` zeigt nichts. Voller Wortlaut, für die Handy-Zeile
/// (`entryRow`), dort ist keine Spaltenbreite zu wahren.
let private flagBadge (flag: LedgerFlag) : ReactElement =
    match flagLabel flag with
    | None -> Html.none
    | Some label ->
        let variant = match flag with AskLena -> Badge.Purple | _ -> Badge.Orange
        Badge.view { Badge.defaultProps with Text = label; Variant = variant; Size = Badge.Small }

/// Das Kennzeichen eines gesetzten Flags in der PC-Statusspalte (Entscheidung Roman
/// 2026-09-19, `ledger-qns4j`): derselbe volle Wortlaut aus `flagBadge` ("Rückfrage
/// Lena") passt zusammen mit `needsReviewStatusBadge` ("Zu prüfen") nicht mehr in die
/// auf 128px zurückgesetzte Spalte (`statusColumnWidth`), genau der Rückfall auf das
/// Pflaster-Verhalten aus `What`, Punkt 3, den der Verifier von Iteration 1
/// zurecht bemängelt hat. *Rückfrage Lena* bekommt hier die gekürzte Fassung "Lena?"
/// (Romans Vorgabe: "Beschriftungen gekürzt oder durch Symbole ersetzt").
///
/// *Markiert* (Iteration 3: der Verifier von Iteration 2 hat vorgerechnet, dass
/// "Zu prüfen" + der Text "Markiert" die 128px-Zelle noch um 3px waagerecht
/// überläuft, scrollWidth 131 gegen clientWidth 128) wird deshalb, statt weiter
/// gekürzt, komplett zum Symbol wie `deletedStatusIcon` (`Icons.flag`, orange,
/// dieselbe Farbrolle wie der bisherige Text-Badge): das schafft deutlich mehr
/// Reserve als eine weitere Kürzung des Wortlauts und folgt demselben, schon
/// etablierten Muster. `withAccessibleName` trägt "Markiert" als `title`/
/// `aria-label` nach, weil das Symbol allein keinen Text mehr zeigt.
///
/// Gemessen gegen die echte, tatsächlich gerenderte PC-Tabelle
/// (Vite-Entwicklungsserver, echtes Tailwind-CSS) in
/// `scripts/ledger-status-column.test.mjs`: zwei Kennzeichen nebeneinander in der
/// 128px breiten Statuszelle, ohne Umbruch und ohne höhere Zeile, für jede der drei
/// Kombinationen "Zu prüfen"+"Lena?", "Zu prüfen"+Ausnahme-Symbol und
/// "Zu prüfen"+"Markiert"-Symbol.
let private flagStatusBadge (flag: LedgerFlag) : ReactElement =
    match flag with
    | NoFlag -> Html.none
    | AskLena ->
        withAccessibleName "Rückfrage Lena" (
            Badge.view { Badge.defaultProps with Text = "Lena?"; Variant = Badge.Purple; Size = Badge.Small })
    | Marked ->
        withAccessibleName "Markiert" (
            Badge.view {
                Badge.defaultProps with
                    Text = ""
                    Icon = Some (Icons.flag Icons.XS Icons.Warning)
                    Variant = Badge.Orange
                    Size = Badge.Small
            })

/// Der Betrag einer Zeile über die Listenvariante (ADR 0049): neutral, Eingang Teal,
/// ohne Glow. Gedämpft und durchgestrichen, wenn die Buchung in YNAB gelöscht ist
/// (AC3, ledger-p3n8k).
let private entryMoney (entry: LedgerEntryView) =
    if entry.DeletedInYnab then
        Money.transactionDeleted entry.Amount.Amount entry.Amount.Currency
    else
        Money.listAmount entry.Amount.Amount entry.Amount.Currency

/// Das Konto-Kuerzel einer Zeile in der Gesamtsicht (AC4).
let private accountBadge (abbreviation: string) =
    Badge.view {
        Badge.defaultProps with
            Text = abbreviation
            Variant = Badge.Neutral
            Size = Badge.Small
    }

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
/// Orange (ADR 0049 Punkt 4); ein Übertrag trägt ein Ziel und bleibt neutral.
let isWithoutCategory (entry: LedgerEntryView) : bool =
    entry.CategoryName.IsNone && List.isEmpty entry.Splits && not entry.IsTransfer

/// Die Kategorie-Beschriftung voll qualifiziert ("Gruppe: Name", Handoff 1.9 Zeile
/// 260), für die PC-Tabelle (ADR 0039, ledger-h6ptn). `CategoryGroupName` kommt
/// aus dem Lesepfad (`ledger_categories`, `ledger-p3wnq`), nicht aus dem
/// Ereignis-Log: fehlt sie (Kategorie nie gespiegelt) oder ist sie nach `Trim()`
/// leer, fällt diese Funktion auf `categoryLabel` zurück, nie ein führendes
/// ": " und nie ein erfundener Gruppenname. Fehlt jede eigene Kategorie (Split
/// oder Platzhaltertext), liefert `categoryLabel` unverändert dasselbe Ergebnis,
/// weil es dann nichts zu qualifizieren gibt.
let categoryLabelQualified (entry: LedgerEntryView) : string =
    match entry.CategoryName, entry.CategoryGroupName with
    | Some name, Some group when not (String.IsNullOrWhiteSpace group) -> $"{group.Trim()}: {name}"
    | _ -> categoryLabel entry

/// Der Payee-Text einer Buchung: der Payee, sonst der Platzhaltertext für eine
/// fehlende Angabe (siehe Rückfallzeile unten). Reine Funktion, analog zu
/// `categoryLabel` geteilt zwischen Handy-Zeile (`entryRow`) und PC-Tabelle
/// (`entryTableRow`), damit beide Ansichten denselben Platzhaltertext zeigen
/// (ledger-n3pqw, Lücke 9 aus dem QA-Review: vorher stand dieser Platzhalter an
/// zwei Stellen im File, je einmal pro Ansicht).
let payeeLabel (entry: LedgerEntryView) : string =
    entry.Payee |> Option.defaultValue "ohne Payee"

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
/// Kategorie" bleibt Orange, weil es ein offener Fall ist. Geteilt zwischen
/// Handy-Zeile und PC-Tabelle.
let categoryClassName (entry: LedgerEntryView) : string =
    if isWithoutCategory entry then
        $"{Tokens.FontSizes.xs} {Tokens.Colors.neonOrange}"
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
/// aber mit blassem Teal als Grund statt der Kartenfarbe. Der Karten-Grund liegt im Rahmen
/// der Zeile, das Teal darüber bleibt durchscheinend.
let entryRowSelectedClassName : string =
    $"w-full text-left {Tokens.ListRoles.mobileRowPadding} transition-colors touch-manipulation " +
    $"bg-neon-teal/[0.07] border-b {Tokens.Borders.subtle}"

/// Ein Kennzeichen in Zeile 2 der Handy-Zeile, das nicht schrumpfen darf.
let private keepWidth (child: ReactElement) =
    Html.span [ prop.className "shrink-0"; prop.children [ child ] ]

/// Der Tooltip (`title`) der Prüf-Schaltfläche: nennt die Aktion (ledger-mhe2s).
let reviewMarkTooltip (target: ReviewMarkTarget) : string =
    match target with
    | Reviewed -> "Als geprüft markieren"
    | Unreviewed -> "Als ungeprüft markieren"

/// Die Klassen der Prüf-Schaltfläche (ledger-mhe2s). Immer neutral (`text-text-secondary`,
/// bei Hover `text-text-primary`), nie Teal: Grün ist ein Signal (ADR 0049), und der Haken
/// neben "zu prüfen" las sich wie "erledigt". Mit `hoverOnly` (PC-Zeile) ist sie ohne
/// Zeilen-Hover, Fokus in der Zeile oder eigenen Tastaturfokus unsichtbar, behält aber
/// ihre Breite (`opacity-0`, kein `hidden`). `rowFocused` ist die Zeile des Tastaturcursors
/// (ledger-rjb8w). Am Handy (`hoverOnly = false`) bleibt sie sichtbar.
let reviewMarkButtonClassName (hoverOnly: bool) (rowFocused: bool) : string =
    let visibility =
        if not hoverOnly then ""
        elif rowFocused then "opacity-100"
        else "opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 focus-visible:opacity-100"
    "inline-flex shrink-0 items-center justify-center rounded-lg h-11 w-11 md:h-7 md:w-7 " +
    "text-text-secondary hover:text-text-primary hover:bg-surface-hover " +
    "transition-colors touch-manipulation disabled:opacity-50 disabled:cursor-not-allowed " +
    visibility

/// Die Schaltfläche "Geprüft" / "Ungeprüft" einer Zeile (ledger-r777c, ADR 0050): ein
/// Symbol (Haken für "Geprüft", Pfeil zurück für "Ungeprüft") mit der Beschriftung als
/// nur vorgelesenem Text, damit die Statusspalte der PC-Tabelle (128px) nicht
/// überläuft. Am Handy 44px Trefferfläche, am PC 28px. Der Klick öffnet nicht die
/// Historie der Zeile (`stopPropagation`). Ein laufender Aufruf sperrt die Schaltfläche.
let private reviewMarkButton
    (dispatch: Msg -> unit)
    (hoverOnly: bool)
    (rowFocused: bool)
    (rowState: ReviewMarkRowState option)
    (entry: LedgerEntryView)
    : ReactElement =
    let action = reviewMarkActionFor entry
    let sending = (rowState = Some ReviewMarkSending)
    let icon =
        match action.Target with
        | Reviewed -> Icons.check Icons.SM Icons.Default
        | Unreviewed -> Icons.undo Icons.SM Icons.Default

    Html.button [
        prop.type'.button
        prop.disabled sending
        prop.title (reviewMarkTooltip action.Target)
        prop.className (reviewMarkButtonClassName hoverOnly rowFocused)
        prop.onClick (fun e ->
            e.stopPropagation ()
            dispatch (StartSetReviewMark (entry.Id, action.Target)))
        prop.children [
            icon
            Html.span [ prop.className "sr-only"; prop.text action.Label ]
        ]
    ]

/// Der Hinweis "Prüfvermerk nicht in YNAB" mit Grund und "Erneut senden" (ADR 0050,
/// Punkt 11). Ein Wort in Orange und der Grund gedämpft: er fällt auf, ohne die ruhige
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
                prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.neonOrange} font-medium shrink-0"
                prop.text hint.Title
            ]
            Html.span [
                prop.className $"{Tokens.ListRoles.muted} truncate min-w-0"
                prop.text hint.Reason
            ]
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

/// Eine Zeile der Buchungsliste am Handy, zweizeilig (ADR 0049 Punkt 6): Zeile 1
/// Payee und Betrag, Zeile 2 Kategorie und Memo mit den Kennzeichen. Beide Zeilen
/// haben eine feste Höhe (`h-5`), damit der Abstand zwischen ihnen in jeder Zeile
/// gleich ist, auch wenn Zeile 2 ein Kennzeichen trägt. `accountAbbreviationOf` ist
/// nur in der Gesamtsicht gesetzt (AC4): das Konto steht dann als Kürzel-Badge in
/// Zeile 2. Die Herkunft steht nicht mehr in der Zeile, nur noch in der Historie.
let private entryRow
    (onClick: unit -> unit)
    (selected: bool option)
    (accountAbbreviationOf: (Guid -> string) option)
    (flatDate: bool)
    (entry: LedgerEntryView)
    =
    // `selected`: `None` im Normalbetrieb, `Some` im Auswahl-Modus (die Zeile schaltet dann
    // die Auswahl, ledger-r1d2f). Eine gewählte Zeile tauscht den Grund gegen blasses Teal.
    let rowClass =
        match selected, entry.NeedsReview with
        | Some true, true -> $"{entryRowSelectedClassName} {Tokens.ListRoles.statusEdge}"
        | Some true, false -> entryRowSelectedClassName
        | _, true -> entryRowReviewClassName
        | _, false -> entryRowClassName

    Html.button [
        // `min-w-0 flex-1`: die Zeile teilt sich den Platz mit der Schaltfläche daneben
        // (`entryRowWithReviewMark`) und darf dafür schrumpfen.
        prop.className $"{rowClass} min-w-0 flex-1"
        prop.onClick (fun _ -> onClick ())
        match selected with
        | Some isOn ->
            prop.role "checkbox"
            prop.custom ("aria-checked", (if isOn then "true" else "false"))
        | None -> ()
        prop.children [
            Html.div [
                prop.className Tokens.ListRoles.mobileLine1
                prop.children [
                    Html.span [
                        prop.className $"{Tokens.ListRoles.primary} truncate min-w-0"
                        prop.text (payeeLabel entry)
                    ]
                    Html.span [ prop.className "shrink-0"; prop.children [ entryMoney entry ] ]
                ]
            ]
            Html.div [
                prop.className Tokens.ListRoles.mobileLine2
                prop.children [
                    match mobileRowDateText DateTime.Today flatDate entry with
                    | Some text -> Html.span [ prop.className $"{Tokens.ListRoles.muted} shrink-0"; prop.text text ]
                    | None -> ()
                    match accountAbbreviationOf with
                    | Some abbreviationOf -> keepWidth (accountBadge (abbreviationOf entry.AccountId))
                    | None -> ()
                    Html.span [
                        prop.className $"{categoryClassName entry} truncate shrink-0 max-w-3/5"
                        prop.text (categoryLabel entry)
                    ]
                    match entry.Memo |> Option.filter (String.IsNullOrWhiteSpace >> not) with
                    | Some memo ->
                        Html.span [
                            prop.className $"{Tokens.ListRoles.muted} truncate min-w-0"
                            prop.text memo
                        ]
                    | None -> ()
                    if entry.Flag <> NoFlag then keepWidth (flagBadge entry.Flag)
                    if entry.DeletedInYnab then keepWidth (deletedBadge ())
                ]
            ]
        ]
    ]

let private entryRowWithReviewMark
    (dispatch: Msg -> unit)
    (accountAbbreviationOf: (Guid -> string) option)
    (rowState: ReviewMarkRowState option)
    (selected: bool option)
    (flatDate: bool)
    (entry: LedgerEntryView)
    =
    // Außen ein Container: die Zeile ist ein `button` (öffnet das Detail), die
    // Schaltfläche "Geprüft"/"Ungeprüft" liegt daneben und nicht darin (ein Button im
    // Button ist ungültig). Der Hinweis "Prüfvermerk nicht in YNAB" steht darunter.
    Html.div [
        prop.key (string entry.Id)
        prop.children [
            Html.div [
                prop.className $"flex items-stretch {Tokens.Backgrounds.dark}"
                prop.children [
                    // Im Auswahl-Modus steht links das Kästchen und die Zeile schaltet die
                    // Auswahl statt das Detail zu öffnen (ledger-r1d2f).
                    match selected with
                    | Some isOn ->
                        Html.div [
                            prop.className (
                                "shrink-0 flex items-center pl-4 pr-3 border-b " +
                                $"{Tokens.Borders.subtle} " +
                                (if isOn then "bg-neon-teal/[0.07]" else Tokens.Backgrounds.dark)
                            )
                            prop.children [ MultiSelect.box (if isOn then MultiSelect.Checked else MultiSelect.Unchecked) true ]
                        ]
                    | None -> ()
                    entryRow
                        (fun () ->
                            match selected with
                            | Some _ -> dispatch (ToggleRowSelected entry.Id)
                            | None -> dispatch (OpenDetail entry))
                        selected
                        accountAbbreviationOf
                        flatDate
                        entry
                    // Eine in YNAB gelöschte Buchung nimmt keinen Prüfvermerk an (wie am PC).
                    // Im Auswahl-Modus entfällt die Schaltfläche: die Leiste und die Zeilen
                    // schreiben nichts (ledger-r1d2f).
                    if not entry.DeletedInYnab && selected.IsNone then
                        Html.div [
                            prop.className $"shrink-0 flex items-center px-1 {Tokens.Backgrounds.dark} border-b {Tokens.Borders.subtle}"
                            prop.children [ reviewMarkButton dispatch false false rowState entry ]
                        ]
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
/// Kürzung würde langer Text umbrechen und die Zeile höher machen als jede andere.
let private truncatedRoleCell (role: Table.CellRole) (child: ReactElement) =
    Table.roleCell role [ Html.div [ prop.className "truncate"; prop.children [ child ] ] ]

/// Die Kategorie-Zelle der PC-Tabelle (ADR 0049 Punkt 4): sekundäre Rolle, nur "ohne
/// Kategorie" in Orange. Der `span` mit eigener Farbklasse gewinnt gegenüber der
/// Zellfarbe, unabhängig von der Reihenfolge im kompilierten CSS (ADR 0031). Reiner Text:
/// die Zellen sind keine eigenen Klickziele, ein Klick ist ein Zeilenklick (ADR 0053
/// Punkt 3); das Filtern liegt als Knopf im Detail.
let private categoryCell (entry: LedgerEntryView) =
    truncatedRoleCell
        Table.Secondary
        (Html.span [ prop.className (categoryClassName entry); prop.text (categoryLabelQualified entry) ])

/// Der Betrag als eigene Zelle: rechtsbündig über `Table.amountCell` (Listenvariante,
/// ADR 0049); gedämpft und durchgestrichen für eine in YNAB gelöschte Buchung.
let private tdMoney (entry: LedgerEntryView) =
    if entry.DeletedInYnab then
        Table.cell { Table.defaultCellProps with Align = Some "right" } [ entryMoney entry ]
    else
        Table.amountCell entry.Amount.Amount entry.Amount.Currency

/// Klassen einer PC-Zeile mit Auswahl: gewählt trägt blasses Teal als Grund, der Anker der
/// Spanne zusätzlich die Teal-Kante links. Die Kante teilt sich den Schatten mit der
/// Statuskante "zu prüfen": der Anker gewinnt (er ist die Zeile, auf die der Blick
/// gerade zielt).
let selectableRowClassName (selected: bool) (isAnchor: bool) : string =
    [ Tokens.ListRoles.rowHeight
      if selected then "bg-neon-teal/[0.07]"
      if isAnchor then "shadow-[inset_2px_0_0_0_var(--color-neon-teal)]" ]
    |> String.concat " "

/// Die Kästchen-Zelle einer PC-Zeile: ein Klick schaltet die Auswahl (mit Shift die
/// Spanne), er öffnet nicht die Historie.
let private selectBoxCell (dispatch: Msg -> unit) (selected: bool) (entry: LedgerEntryView) : ReactElement =
    Html.td [
        prop.className "pl-5 pr-0 py-0"
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
    | StatusColumn

/// Was die PC-Ansicht bei einer Tabellenbreite zeigt: die Tabelle mit den sichtbaren
/// Spalten samt Breiten, oder die kompakte Liste (dieselbe wie am Handy).
type TableLayout =
    | CompactList
    | Columns of (LedgerColumn * ColumnWidth) list

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
    Table.roleCell
        Table.Payee
        [ Html.div [
              prop.className "truncate"
              prop.children [ abbreviation; Html.span [ prop.text (payeeLabel entry) ] ]
          ] ]

let private entryTableRow
    (dispatch: Msg -> unit)
    (columns: LedgerColumn list)
    (isAllAccounts: bool)
    (accountAbbreviationOf: Guid -> string)
    (rowState: ReviewMarkRowState option)
    (selected: bool)
    (isAnchor: bool)
    (isFocused: bool)
    (entry: LedgerEntryView)
    =
    // "Zu prüfen" ist die Statuskante (`StatusEdge`, ADR 0049 Punkt 7), nicht mehr
    // ein Rahmen links. `Table.trClickable` kennt weder Kante noch Zeilenhöhe,
    // deshalb direkt `Table.row` mit denselben Klassen wie `Table.calmRow`.
    Table.row
        { Table.defaultRowProps with
            OnClick = Some (fun () -> dispatch (OpenDetail entry))
            StatusEdge = entry.NeedsReview
            IsFocused = isFocused
            ClassName = Some ("group " + selectableRowClassName selected isAnchor) }
        [
            selectBoxCell dispatch selected entry
            Table.roleCell Table.DateCell [ Html.text (formatDate entry.Date) ]
            if List.contains AccountColumn columns then
                Table.roleCell Table.Muted [ Html.text (accountAbbreviationOf entry.AccountId) ]
            payeeCell
                (if isAllAccounts && not (List.contains AccountColumn columns) then
                     Some (accountAbbreviationOf entry.AccountId)
                 else
                     None)
                entry
            categoryCell entry
            if List.contains MemoColumn columns then
                truncatedRoleCell Table.Muted (Html.text (entry.Memo |> Option.defaultValue ""))
            tdMoney entry
            Table.cell { Table.defaultCellProps with Align = Some "right" } [
                Html.div [
                    // `flex-nowrap`, nicht `flex-wrap`: die Kennzeichen sollen die
                    // Spalte in die Breite gehen lassen, nicht die Zeile in die Höhe
                    // (AC5, ledger-qns4j, siehe `statusColumnWidth`). Kein
                    // `originBadge` (AC3): die Herkunft steht nur noch im Detail
                    // (`detailContent`): die Statusspalte trägt nur Status.
                    prop.className "flex flex-nowrap items-center justify-end gap-1"
                    prop.children [
                        if entry.NeedsReview then needsReviewStatusBadge ()
                        flagStatusBadge entry.Flag
                        if entry.DeletedInYnab then deletedStatusIcon ()
                        // Eine in YNAB gelöschte Buchung nimmt keinen Prüfvermerk mehr an
                        // (der Server lehnt sie ab), also bekommt sie keine Schaltfläche.
                        if not entry.DeletedInYnab then reviewMarkButton dispatch true isFocused rowState entry
                    ]
                ]
            ]
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

/// Gruppenkopf eines Datums in der PC-Tabelle: Datum links über den ersten Spalten,
/// Tagessumme in der Betragsspalte (`Table.calmGroupRow`, ADR 0049 Punkt 8).
let private dateGroupRow (columnsBeforeAmount: int) (date: DateTime) (sum: Money) =
    Table.calmGroupRow
        ("group-" + date.ToString("yyyy-MM-dd"))
        columnsBeforeAmount
        (formatDate date)
        (daySumText sum)

/// Eine Karte der Handy-Kontostaende-Leiste (AC6): waagerecht scrollbare
/// `Stats`-Kachel, Betrag ueber `Money.accountBalance` (Farbsemantik + Ziffernschrift
/// statt reinem Text). Die PC-Ansicht nutzt seit `ledger-x0yga` `Money.accountListRow`
/// statt dieser Karte — eine hohe Kachel mit Farbstreifen passt fuer eine Kennzahl,
/// nicht fuer eine scanbare Kontoliste.
let private accountCardView (dispatch: Msg -> unit) (selected: AccountFilter option) (card: AccountCard) =
    Stats.view {
        Stats.defaultProps with
            Label = card.Label
            ValueElement = Some (Money.accountBalance card.Balance.Amount card.Balance.Currency)
            Size = Stats.Compact
            // AC4 (ledger-w9skm): keine Farbverlaufsstreifen mehr — `Stats.view`
            // zeichnet ihn sonst per `defaultProps.Accent = Gradient` (siehe
            // `Stats.fs`), unabhaengig davon, was die Seite hier setzt.
            Accent = Stats.NoAccent
            IsActive = selected = Some card.Filter
            OnClick = Some (fun () -> dispatch (SelectFilter card.Filter))
    }

/// Eine Zeile der PC-Kontostaende-Liste (AC6): kompakt, Name links, Betrag rechts,
/// aktives Konto hervorgehoben — siehe `Money.accountListRow`.
let private accountListRowView (dispatch: Msg -> unit) (selected: AccountFilter option) (card: AccountCard) =
    Money.accountListRow
        card.Label
        card.Balance.Amount
        card.Balance.Currency
        (selected = Some card.Filter)
        (fun () -> dispatch (SelectFilter card.Filter))

let private accountStrip
    (dispatch: Msg -> unit)
    (accounts: LedgerAccountView list)
    (balances: LedgerAccountBalanceView list)
    (selected: AccountFilter option)
    =
    if List.isEmpty accounts then
        Html.none
    else
        let cards = buildAccountCards accounts balances
        Html.div [
            // Breite nach Handoff Screen 1.9 (Konten-Spalte: 268px). Kein eigener
            // Hintergrund (der Entwurf setzt dort `surface-app`, also denselben
            // Grund wie die Seite) — die Spalte hebt sich nur ueber die rechte
            // Kante ab.
            //
            // `ledger-account-strip` (styles.css, AC1/AC2 ledger-qns4j): haftet beim
            // Scrollen unter der fixen Navbar, wie `.table-sticky-thead`. Ohne sie
            // scrollte die Spalte mit weg und die Kontostände verschwanden nach
            // wenigen Buchungen. Eine `max-height` plus eigener `overflow-y: auto`
            // sorgt dafür, dass eine Kontoliste, die höher als der sichtbare
            // Bereich ist (AC2, z. B. zwanzig Konten), in sich scrollt statt den
            // Seiteninhalt abzuschneiden oder die Haftung zu verlieren. Nur ab `md`
            // wirksam (die Klasse selbst ist per `@media` in styles.css auf md+
            // begrenzt): am Handy zeigt dieselbe Zelle die waagerechte Kartenleiste
            // ohne Sticky.
            prop.className $"md:w-[268px] md:shrink-0 md:border-r {Tokens.Borders.subtle} md:pr-3 ledger-account-strip"
            prop.children [
                Primitives.Responsive.mobileOnlyFlex [
                    Primitives.ScrollContainer.horizontal [
                        Html.div [
                            prop.className "flex gap-3 pb-1"
                            prop.children (
                                cards
                                |> List.map (fun card ->
                                    Html.div [
                                        prop.key (string card.Label)
                                        prop.className "min-w-[9.5rem]"
                                        prop.children [ accountCardView dispatch selected card ]
                                    ])
                            )
                        ]
                    ]
                ]
                Primitives.Responsive.desktopOnly [
                    Html.div [
                        prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono} uppercase tracking-wider px-3 pb-1"
                        prop.text "Konten"
                    ]
                    Html.div [
                        prop.className "flex flex-col gap-0.5"
                        prop.children (cards |> List.map (accountListRowView dispatch selected))
                    ]
                ]
            ]
        ]

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

/// Ist die Zeile ein Fehlschlag oder die Löschung? Dann ist der Punkt rot, gleich welcher
/// Urheber sie schrieb. Der Satz kommt aus `QueryHandlers.describe`; die Anfänge dort sind
/// die einzige Quelle.
let isFailureOrDeletionLine (line: LedgerHistoryLine) : bool =
    let text = line.Description
    text.StartsWith("In YNAB gelöscht", StringComparison.Ordinal)
    || text.StartsWith("Prüfvermerk nicht in YNAB", StringComparison.Ordinal)
    || text.StartsWith("Push nach YNAB gescheitert", StringComparison.Ordinal)

/// Die Punktfarbe einer Zeile der Zeitleiste, nach Urheber (Handoff Ablauf 7): Teal Import,
/// Violett Regel, Grau YNAB-Spiegel, Grün eigene Prüfung in BB, Rot Fehlschläge und Löschung.
let historyDotFor (line: LedgerHistoryLine) : Detail.DotColor =
    if isFailureOrDeletionLine line then
        Detail.DotRed
    else
        match line.Origin with
        | ImportedFromComdirect -> Detail.DotTeal
        | DecidedByRule
        | MatchedByOrderId -> Detail.DotPurple
        | MirroredFromYnab -> Detail.DotGrey
        | DecidedInBb -> Detail.DotGreen
        | AddedViaQuickAdd -> Detail.DotOrange

/// Die Zeitleiste aus den Zeilen der Historie, neueste oben.
let historyTimelineItems (lines: LedgerHistoryLine list) : Detail.TimelineItem list =
    lines
    |> historyLinesForDisplay
    |> List.map (fun line ->
        { Key = string line.Sequence
          Dot = historyDotFor line
          Text = line.Description
          Stamp = $"{formatDateTime line.OccurredAt} · {originLabel line.Origin}" })

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
    /// Orange, ohne Aktion: der Push nach YNAB steht aus oder scheiterte.
    | NotInYnabNotice of reason: string
    /// Orange, mit "Erneut senden", wenn etwas zu senden ist.
    | ReviewMarkNotice of ReviewMarkHint

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
/// Grund, sonst, dass noch nichts gesendet wurde.
let notInYnabNoticeReason (failure: LedgerPushFailure option) : string =
    match failure with
    | Some f ->
        let reason = f.Reason.Trim().TrimEnd('.')
        $"Push gescheitert am {formatDateTime f.FailedAt}: {reason}. BB versucht es beim nächsten Lauf erneut."
    | None -> "Noch nicht an YNAB gesendet."

let detailNoticeFor
    (rowState: ReviewMarkRowState option)
    (history: RemoteData<LedgerHistoryLine list>)
    (entry: LedgerEntryView)
    : DetailNotice option =
    if entry.DeletedInYnab then
        Some (DeletedNotice (deletedNoticeReason history))
    elif not entry.LinkedToYnab then
        Some (NotInYnabNotice (notInYnabNoticeReason entry.PushToYnabFailure))
    else
        reviewMarkHintFor rowState entry |> Option.map ReviewMarkNotice

/// Die Nachricht der Aktion in der Hinweisleiste: nur die Prüfvermerk-Leiste hat eine
/// ("Erneut senden", `retryReviewMarkWriteBack` aus ledger-r777c); gelöscht und "Noch nicht
/// in YNAB" haben keine Aktion.
let detailNoticeActionMsg (entryId: Guid) (notice: DetailNotice) : Msg option =
    match notice with
    | ReviewMarkNotice hint when hint.RetryLabel.IsSome -> Some (StartRetryReviewMark entryId)
    | _ -> None

/// Die Nachricht des Geprüft-Schalters: an setzt `Reviewed`, aus `Unreviewed`, über denselben
/// Weg wie die Schaltfläche an der Zeile (`StartSetReviewMark`).
let reviewedSwitchMsg (entryId: Guid) (on: bool) : Msg =
    StartSetReviewMark (entryId, (if on then Reviewed else Unreviewed))

let private noticeView
    (dispatch: Msg -> unit)
    (rowState: ReviewMarkRowState option)
    (entry: LedgerEntryView)
    (notice: DetailNotice)
    : ReactElement =
    match notice with
    | DeletedNotice reason ->
        Detail.notice { Tone = Detail.NoticeRed; Badge = "In YNAB gelöscht"; Reason = reason; Action = None }
    | NotInYnabNotice reason ->
        Detail.notice { Tone = Detail.NoticeOrange; Badge = "Noch nicht in YNAB"; Reason = reason; Action = None }
    | ReviewMarkNotice hint ->
        Detail.notice
            { Tone = Detail.NoticeOrange
              Badge = hint.Title
              Reason = hint.Reason
              Action =
                match hint.RetryLabel, detailNoticeActionMsg entry.Id notice with
                | Some label, Some msg ->
                    Some
                        { Label = label
                          Busy = (rowState = Some ReviewMarkSending)
                          OnClick = fun () -> dispatch msg }
                | _ -> None }

/// Die Beschriftung einer Split-Zeile: Kategorie, Übertrag oder offen.
let splitTargetLabel (split: TransactionSplit) : string =
    match split.Target with
    | ToCategory (_, name) -> name
    | ToTransfer (_, accountName) -> $"Übertrag: {accountName}"
    | Uncategorized -> "kein Ziel"

/// Die Zeilen der Split-Feldzeile: Ziel und Betrag.
let splitLineTexts (entry: LedgerEntryView) : (string * string) list =
    entry.Splits
    |> List.map (fun split ->
        splitTargetLabel split, Money.formatAmountText false true split.Amount.Amount split.Amount.Currency)

let private accountNameOf (model: Model) (entry: LedgerEntryView) : string =
    match model.Accounts with
    | Success view ->
        view.Accounts
        |> List.tryFind (fun a -> a.Id = entry.AccountId)
        |> Option.map (fun a -> a.Name)
        |> Option.defaultValue "Konto"
    | _ -> "Konto"

/// Die Zeile unter "Konto · Datum" im Kopf: voller Datumsstil statt der relativen Form der Liste.
let detailSubtitle (accountName: string) (entry: LedgerEntryView) : string =
    $"{accountName} · {formatDateFull entry.Date}"

let private historyView (dispatch: Msg -> unit) (entry: LedgerEntryView) (history: RemoteData<LedgerHistoryLine list>) =
    match history with
    | NotAsked -> Html.none
    | Loading -> Loading.centered "Historie wird geladen…"
    | Failure message ->
        ErrorDisplay.cardCompact
            $"Historie konnte nicht geladen werden: {message}"
            (Some (fun () -> dispatch (LoadHistory entry.Id)))
    | Success [] -> Detail.timelineEmpty "Noch keine Einträge"
    | Success lines -> Detail.timeline (historyTimelineItems lines)

/// Der Kopf des Details: Betrag, Payee mit Filter-Knopf, "Konto · Datum", Schließen.
let private detailHead (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) (onClose: (unit -> unit) option) =
    Detail.head
        (Detail.amount entry.Amount.Amount entry.Amount.Currency entry.DeletedInYnab)
        (payeeLabel entry)
        (detailSubtitle (accountNameOf model entry) entry)
        (match payeeFilterLabel entry, entry.Payee with
         | Some label, Some payee -> Some (Detail.filterButton label (fun () -> dispatch (FilterByRowPayee payee)))
         | _ -> None)
        onClose

/// Alles unter dem Kopf: Hinweisleiste, Felder, Geprüft-Schalter, Herkunft, Historie.
let private detailContent (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) : ReactElement list =
    let rowState = model.ReviewMarkRows |> Map.tryFind entry.Id
    let muted = entry.DeletedInYnab
    let textColor = if muted then Tokens.Colors.textMuted else Tokens.Colors.textPrimary

    [ match detailNoticeFor rowState model.History entry with
      | Some notice -> noticeView dispatch rowState entry notice
      | None -> ()

      // Kategorie: lesend, am PC voll qualifiziert (ADR 0039). Rechts bleibt Platz für den
      // Pfeil des Kategorie-Pickers ab v2 (ledger-k2rbn); jetzt nur der Filter-Knopf.
      Detail.fieldRow
          "detail-category"
          "Kategorie"
          (Detail.valueText
              (if muted then Tokens.Colors.textMuted
               elif isWithoutCategory entry then Tokens.Colors.neonOrange
               else Tokens.Colors.textPrimary)
              (categoryLabelQualified entry))
          (match categoryFilterLabel entry, entry.CategoryId with
           | Some label, Some categoryId ->
               Some (Detail.filterButton label (fun () -> dispatch (FilterByRowCategory categoryId)))
           | _ -> None)

      Detail.fieldRow
          "detail-split"
          "Split"
          (match splitLineTexts entry with
           | [] -> Detail.valueText Tokens.Colors.textMuted "Kein Split"
           | lines -> Detail.splitLines lines)
          None

      Detail.fieldRow
          "detail-memo"
          "Memo"
          (match entry.Memo |> Option.filter (String.IsNullOrWhiteSpace >> not) with
           | Some memo -> Detail.valueText textColor memo
           | None -> Detail.valueText Tokens.Colors.textMuted "Kein Memo")
          None

      // Eine in YNAB gelöschte Buchung nimmt keinen Prüfvermerk an: kein Schalter.
      if not entry.DeletedInYnab then
          Detail.reviewedSwitch
              "Geprüft"
              (not entry.NeedsReview)
              (rowState = Some ReviewMarkSending)
              (fun on -> dispatch (reviewedSwitchMsg entry.Id on))

      Detail.originRow (
          Badge.view {
              Badge.defaultProps with
                  Text = originLabel entry.Origin
                  Variant = Badge.Info
                  Size = Badge.Small
          }
      )

      Detail.sectionLabel "HISTORIE"
      historyView dispatch entry model.History ]

/// Das Detail als Seitenpanel (PC). Gerendert in der Spalte der Liste (`FilterBarColumn`).
let private detailPanelView (dispatch: Msg -> unit) (model: Model) (entry: LedgerEntryView) : ReactElement =
    Detail.panel
        $"Detail {payeeLabel entry}"
        [ detailHead dispatch model entry (Some (fun () -> dispatch CloseDetail))
          Detail.scrollBody (detailContent dispatch model entry) ]

/// Das Detail als BottomSheet (Handy, Kompaktliste, zu schmales Fenster für das Panel).
/// Das Sheet bringt Titel und Schließen-Knopf mit; der Kopf mit Betrag und Payee steht
/// im Inhalt.
let private detailSheetView (dispatch: Msg -> unit) (model: Model) (open': bool) : ReactElement =
    let entry = detailEntryOf model
    BottomSheet.view
        { IsOpen = open' && entry.IsSome
          OnClose = fun () -> dispatch CloseDetail
          Title = "Buchung"
          Subtitle = None
          Footer = None }
        // Der Inhalt steht nur im DOM, solange das Sheet offen ist: ein geschlossenes Sheet bliebe
        // sonst als verstecktes Doppel neben dem Panel stehen.
        [ match entry with
          | Some entry when open' ->
              detailHead dispatch model entry None
              yield! detailContent dispatch model entry
          | _ -> () ]

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
/// die Liste sie ins Bild. Enter und Esc sind nicht belegt (ledger-q1hw2).
[<ReactComponent>]
let private KeyboardNavigation (focusedRow: Guid option) (dispatch: Msg -> unit) =
    let dispatchRef = React.useRef dispatch
    dispatchRef.current <- dispatch

    React.useEffect (
        (fun () ->
            let handler (e: Browser.Types.Event) =
                let ke = e :?> Browser.Types.KeyboardEvent
                let target = ke.target :?> Browser.Types.HTMLElement
                let tagName = if isNull (box target) then "" else target.tagName
                let editable = not (isNull (box target)) && target.isContentEditable
                if not ke.defaultPrevented then
                    match keyCommandFor tagName editable (ke.ctrlKey || ke.metaKey) ke.altKey ke.key with
                    | Some msg ->
                        ke.preventDefault ()
                        dispatchRef.current msg
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

/// Der Tastaturhinweis am PC (Handoff Ablauf 1, 1.9): eine Fußzeile, solange keine
/// Auswahl läuft. Tasten in Mono, der Rest gedämpft.
let keyboardHintText = "↑ ↓ bewegen · G geprüft · Umschalt+G ungeprüft · Enter Detail öffnen"

/// Der Hinweis bei offenem Panel (ledger-q1hw2): die Tasten, die dann wirken.
let keyboardHintPanelText = "↑ ↓ bewegen · G geprüft · Esc Panel schließen"

/// Der Hinweis zum Zustand: bei offenem Seitenpanel der Panel-Text, sonst der Grundtext.
let keyboardHintTextFor (model: Model) : string =
    if detailPanelOpen model then keyboardHintPanelText else keyboardHintText

let private keyboardHint (text: string) : ReactElement =
    Html.div [
        prop.className (
            $"hidden md:flex fixed bottom-0 left-0 right-0 z-30 items-center gap-2 {Tokens.Backgrounds.surface} " +
            $"border-t {Tokens.Borders.subtle} px-5 py-3 {Tokens.FontSizes.xs} {Tokens.Colors.textMuted}"
        )
        prop.children [ Html.span [ prop.className Tokens.Fonts.mono; prop.text text ] ]
    ]

// ============================================
// Seite
// ============================================

/// Die Karte der Handy-Liste mit der Wisch-Geste (ledger-th36h): Wisch nach rechts
/// markiert eine zu prüfende Buchung als geprüft, Wisch nach links bleibt unbelegt
/// (Handoff Ablauf 1, Screen 1.2). Eine schon geprüfte oder in YNAB gelöschte Zeile
/// bekommt keine Geste. Die Schaltfläche an der Zeile bleibt der barrierefreie Weg.
let private swipeableEntryRow
    (dispatch: Msg -> unit)
    (accountAbbreviationOf: (Guid -> string) option)
    (rowState: ReviewMarkRowState option)
    (flatDate: bool)
    (entry: LedgerEntryView)
    =
    let row = entryRowWithReviewMark dispatch accountAbbreviationOf rowState None flatDate entry
    if swipeAppliesTo entry then
        Html.div [
            prop.key (string entry.Id)
            prop.children [
                Swipe.SwipeableRowRight {
                    ActionLabel = swipeActionLabel
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

/// Eine Zeile der Handy-Liste: im Normalbetrieb mit Wisch-Geste, im Auswahl-Modus mit
/// Kästchen und ohne Wisch (ein Wisch wäre dort überraschend, ledger-r1d2f). In beiden Fällen
/// unter derselben Hülle für das lange Drücken.
let private mobileEntryRow
    (dispatch: Msg -> unit)
    (accountAbbreviationOf: (Guid -> string) option)
    (selectionMode: bool)
    (selection: Selection)
    (rowState: ReviewMarkRowState option)
    (flatDate: bool)
    (entry: LedgerEntryView)
    =
    let inner =
        if selectionMode then
            entryRowWithReviewMark dispatch accountAbbreviationOf rowState (Some (isSelected selection entry.Id)) flatDate entry
        else
            swipeableEntryRow dispatch accountAbbreviationOf rowState flatDate entry
    React.keyedFragment (
        string entry.Id,
        [ LongPressRow (not selectionMode) (fun () -> dispatch (StartSelectionMode entry.Id)) inner ]
    )

let private entryListMobile
    (dispatch: Msg -> unit)
    (accountAbbreviationOf: (Guid -> string) option)
    (includeDeleted: bool)
    (reviewRows: Map<Guid, ReviewMarkRowState>)
    (selectionMode: bool)
    (selection: Selection)
    (alwaysVisible: bool)
    (sort: LedgerSort)
    (entries: LedgerEntryView list)
    =
    // `alwaysVisible`: die kompakte Liste ersetzt die Tabelle auch ab `md`, wenn die
    // Tabellenbreite für die Mindestspalten nicht reicht (`visibleColumns`).
    let wrap (children: ReactElement list) =
        if alwaysVisible then Html.div children else Primitives.Responsive.mobileOnly children

    wrap [
        let row (flat: bool) (entry: LedgerEntryView) =
            mobileEntryRow
                dispatch
                accountAbbreviationOf
                selectionMode
                selection
                (reviewRows |> Map.tryFind entry.Id)
                flat
                entry

        Html.div [
            prop.children (
                if usesDayGroups sort then
                    groupByDate entries
                    |> List.collect (fun (date, group) ->
                        dateGroupHeader date (daySum includeDeleted group)
                        :: (group |> List.map (row false)))
                else
                    entries |> List.map (row true)
            )
        ]
    ]

/// Breite der Kästchen-Spalte der PC-Tabelle (Handoff Ablauf 3: 44px).
let selectColumnWidth = 44

/// Breite der Status-Spalte in der PC-Tabelle: der Handoff-Wert aus Screen 1.9
/// (Entscheidung Roman 2026-09-19, `ledger-qns4j`).
let private statusColumnWidth = "128"

let private dateColumnWidth = 96
let private accountColumnWidth = 72
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
    | StatusColumn -> int statusColumnWidth

/// Die Spaltenstufen von breit nach schmal (ADR 0049 bleibt Maßstab: ruhig statt eng):
/// alle Spalten, ohne Memo, ohne Memo und Konto. Das Konto fällt nur in der Gesamtsicht
/// weg, in einem Konto gibt es die Spalte ohnehin nicht.
let private columnStages (allowMemo: bool) (showAccountColumn: bool) : LedgerColumn list list =
    let withAccount (cols: LedgerColumn list) =
        if showAccountColumn then
            [ SelectColumn; DateColumn; AccountColumn ] @ cols
        else
            [ SelectColumn; DateColumn ] @ cols

    let tail = [ AmountColumn; StatusColumn ]

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

/// Die Breite, die das Seitenpanel der Tabelle nimmt (ledger-q1hw2).
let detailPanelWidth : float = float Detail.panelWidthPx

/// Die Spalten neben einem offenen Seitenpanel: die Tabellenbreite ohne Panel abzüglich
/// seiner 420 Pixel, und Memo fällt immer weg. Die übrigen Stufen entscheidet dieselbe
/// Regel wie sonst, so dass in schmalen Fenstern auch die Konto-Spalte weicht.
let visibleColumnsWithPanel (tableWidth: float) (showAccountColumn: bool) : TableLayout =
    visibleColumnsWith false (tableWidth - detailPanelWidth) showAccountColumn

/// Die Form des Details bei der Tabellenbreite OHNE Panel (ADR 0053): ein Panel nur, wenn
/// die Liste schon als Tabelle steht und neben dem Panel noch eine Tabellenstufe passt;
/// sonst ein Sheet. Weil die Breite ohne Panel gemessen wird, kippt die Form nicht, wenn
/// das Panel selbst Platz nimmt.
let detailFormFor (tableWidth: float) (showAccountColumn: bool) : DetailForm =
    match visibleColumns tableWidth showAccountColumn, visibleColumnsWithPanel tableWidth showAccountColumn with
    | Columns _, Columns _ -> DetailPanel
    | _ -> DetailSheet

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
    (includeDeleted: bool)
    (reviewRows: Map<Guid, ReviewMarkRowState>)
    (headerState: HeaderCheckState)
    (selection: Selection)
    (anchor: Guid option)
    (focusedRow: Guid option)
    (sort: LedgerSort)
    (entries: LedgerEntryView list)
    =
    // Spalten vor dem Betrag: alle sichtbaren außer Betrag und Status.
    let visible = columns |> List.map fst
    let columnsBeforeAmount = List.length visible - 2

    let sortHeader (text: string) (align: string option) (field: LedgerSortField) =
        Table.calmSortableHeaderCell text align (headerSortDirection sort field) (fun () -> dispatch (SetSort field))

    let bodyRows (entry: LedgerEntryView) =
        let rowState = reviewRows |> Map.tryFind entry.Id
        React.keyedFragment (
            string entry.Id,
            [ entryTableRow
                  dispatch
                  visible
                  isAllAccounts
                  accountAbbreviationOf
                  rowState
                  (isSelected selection entry.Id)
                  (anchor = Some entry.Id)
                  (focusedRow = Some entry.Id)
                  entry
              match entryHintRow dispatch (columnsBeforeAmount + 2) rowState entry with
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
                    selectHeaderCell dispatch headerState
                    sortHeader "Datum" None SortByDate
                    if List.contains AccountColumn visible then sortHeader "Konto" None SortByAccount
                    sortHeader "Payee" None SortByPayee
                    sortHeader "Kategorie" None SortByCategory
                    if List.contains MemoColumn visible then Table.calmHeaderCell "Memo" None
                    sortHeader "Betrag" (Some "right") SortByAmount
                    Table.calmHeaderCell "Status" (Some "right")
                ]
                Table.tbody (
                    if usesDayGroups sort then
                        groupByDate entries
                        |> List.collect (fun (date, group) ->
                            dateGroupRow columnsBeforeAmount date (daySum includeDeleted group)
                            :: (group |> List.map bodyRows))
                    else
                        entries |> List.map bodyRows
                )
            ]
    ]

let private entryList
    (dispatch: Msg -> unit)
    (accounts: LedgerAccountView list)
    (isAllAccounts: bool)
    (tableWidth: float)
    (panelOpen: bool)
    (model: Model)
    =
    let abbreviationOf =
        let byId =
            accounts |> List.map (fun a -> a.Id, accountAbbreviation a.Name) |> Map.ofList
        fun (id: Guid) -> byId |> Map.tryFind id |> Option.defaultValue "?"

    match model.Entries with
    | NotAsked ->
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Noch kein Konto"
            "Sobald ein Spiegel-Lauf gelaufen ist, stehen hier die Konten und ihre Buchungen."
            (Some (Button.primary "Jetzt spiegeln" (fun () -> dispatch StartMirror)))

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

    | Success [] when hasAnyFilter model.Filters ->
        // AC "Leerzustand ohne Treffer": nennt eine nächste Handlung, statt nur
        // Leere zu melden, anders als der Fall ohne jeden Filter (Zweig darunter),
        // der auf den fehlenden Spiegel-Lauf hinweist.
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Kein Treffer"
            "Filter lockern oder alle Filter löschen."
            (Some (Button.secondary "Alle Filter löschen" (fun () -> dispatch ClearAllFilters)))

    | Success [] ->
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Keine Buchungen"
            "Für diesen Filter steht noch nichts im Kassenbuch."
            (Some (Button.secondary "Jetzt spiegeln" (fun () -> dispatch StartMirror)))

    | Success entries ->
        // Bei offenem Panel gilt die Breite abzüglich Panel und ohne Memo; `tableWidth` ist die
        // Breite ohne Panel (die Spalte reserviert den Platz als Innenabstand).
        let layout =
            if panelOpen then visibleColumnsWithPanel tableWidth isAllAccounts
            else visibleColumns tableWidth isAllAccounts
        let sort = Components.Ledger.State.effectiveSort model

        Html.div [
            entryListMobile
                dispatch
                (if isAllAccounts then Some abbreviationOf else None)
                model.IncludeDeleted
                model.ReviewMarkRows
                model.SelectionMode
                model.Selection
                (layout = CompactList)
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
                    model.IncludeDeleted
                    model.ReviewMarkRows
                    (headerCheckState model)
                    model.Selection
                    model.SelectionAnchor
                    model.FocusedRow
                    sort
                    entries

            if model.LoadingMore then
                Html.div [
                    prop.className "mt-2"
                    prop.children [
                        Primitives.Responsive.mobileOnly [ Loading.txListSkeleton 2 ]
                        Primitives.Responsive.desktopOnly [ Loading.tableSkeleton (if isAllAccounts then 7 else 6) 2 ]
                    ]
                ]

            if model.HasMoreEntries then
                ScrollSentinel (fun () -> dispatch LoadMoreEntries) (model.HasMoreEntries && not model.LoadingMore)
        ]

// ============================================
// Filterleiste und Summenzeile (ledger-j8xq4)
// ============================================
// Chips, Kategorienauswahl mit Mehrfachauswahl (Click-Commit, ADR 0005/Styleguide
// 7.1), Zeitraum-Schnellwahl, Payee-Vorschläge, Memo-/Betrags-Suchfelder und die
// Summenzeile über der Liste. Beide Geräte teilen sich denselben Popover-Inhalt
// (`*PickerBody`); nur die Chrome darum unterscheidet sich (`filterChrome`):
// BottomSheet am Handy, `FilterPopover` am PC. Welche Chrome gerendert wird,
// entscheidet `Viewport.isDesktopViewport()` zur Renderzeit, NICHT der übliche
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
        OnToggle = fun guid -> dispatch (ToggleCategoryDraft guid)
        OnSearchChange = fun text -> dispatch (SetCategorySearch text)
        OnToggleHiddenExpanded = fun () -> dispatch ToggleHiddenCategoriesExpanded
        OnReset = fun () -> dispatch ResetCategoryDraft
        OnApply = fun () -> dispatch ApplyCategoryFilter
    }

let private dateQuickButton (dispatch: Msg -> unit) (model: Model) (quick: DateQuick) =
    let isSelected = model.DateDraft.Quick = Some quick
    Html.button [
        prop.type' "button"
        prop.className (
            "rounded-full py-2 text-xs font-semibold border transition-colors "
            + (if isSelected then $"bg-neon-teal/10 border-neon-teal {Tokens.Colors.neonTeal}"
               else $"{Tokens.Backgrounds.dark} {Tokens.Borders.default'} {Tokens.Colors.textSecondary}"))
        prop.onClick (fun _ -> dispatch (SetDateQuick quick))
        prop.text (dateQuickLabel quick)
    ]

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
        DateField.view { Value = value; OnChange = onChange; Invalid = invalid; Label = label }
    )

/// Die Zeitraum-Auswahl (AC: Schnellwahl trägt Von/Bis sichtbar ein).
let private datePickerBody (dispatch: Msg -> unit) (model: Model) =
    Html.div [
        prop.className "flex flex-col gap-4"
        prop.children [
            Html.div [
                prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono} uppercase tracking-wider"
                prop.text "Schnellwahl"
            ]
            Html.div [
                prop.className "grid grid-cols-2 gap-2"
                prop.children [
                    dateQuickButton dispatch model ThisMonth
                    dateQuickButton dispatch model LastMonth
                    dateQuickButton dispatch model ThisYear
                    dateQuickButton dispatch model Last12Months
                ]
            ]
            Html.div [
                prop.className "grid grid-cols-2 gap-3 md:grid-cols-1"
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

/// Überschrift der Payee-Vorschläge, die Zahl mit Tausenderpunkt
let payeeSuggestionsHeading (totalCount: int) : string =
    $"Vorschläge aus den {Money.formatCount totalCount} Treffern"

/// Die Payee-Auswahl (AC: Vorschläge kommen mit Anzahl aus dem aktuellen Treffer;
/// Einzelauswahl, ein Klick filtert sofort, kein "Übernehmen").
let private payeePickerBody (dispatch: Msg -> unit) (model: Model) =
    let allSuggestions = model.PayeeSuggestions |> RemoteData.toOption |> Option.defaultValue []
    let filtered =
        allSuggestions
        |> List.filter (fun s ->
            String.IsNullOrWhiteSpace model.PayeeSearch
            || s.Payee.ToLowerInvariant().Contains(model.PayeeSearch.Trim().ToLowerInvariant()))

    Html.div [
        prop.className "flex flex-col gap-3"
        prop.children [
            PickerParts.searchField model.PayeeSearch (fun text -> dispatch (SetPayeeSearch text)) "Payee suchen…"
            Html.div [
                prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono} uppercase tracking-wider"
                prop.text (payeeSuggestionsHeading model.TotalCount)
            ]
            match model.PayeeSuggestions with
            | Loading -> Loading.centered "Vorschläge werden geladen…"
            | Failure message -> ErrorDisplay.inline' message
            | NotAsked -> Html.none
            | Success _ ->
                Html.div [
                    prop.className "flex flex-col max-h-64 overflow-y-auto"
                    prop.children [
                        for s in filtered do
                            Html.button [
                                prop.key s.Payee
                                prop.type' "button"
                                prop.className "flex items-center justify-between gap-3 min-w-0 py-2.5 text-left"
                                prop.onClick (fun _ -> dispatch (SelectPayeeFilter s.Payee))
                                prop.children [
                                    PickerParts.name s.Payee
                                    Html.span [
                                        prop.className $"{Tokens.Fonts.mono} {Tokens.Colors.textMuted} {Tokens.FontSizes.xs} tabular-nums shrink-0"
                                        prop.text (Money.formatCount s.Count)
                                    ]
                                ]
                            ]
                    ]
                ]
            Html.p [
                prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted}"
                prop.text "Ein Tipp auf den Vorschlag filtert sofort: Payee ist immer nur einer."
            ]
        ]
    ]

/// Das Memo-Suchfeld (AC: Suchfeld mit kurzer Verzögerung beim Tippen).
let private memoPickerBody (dispatch: Msg -> unit) (model: Model) =
    Html.div [
        prop.className "flex flex-col gap-3"
        prop.children [
            Input.text { Input.textInputDefaults with
                            Value = model.MemoDraft
                            OnChange = (fun text -> dispatch (SetMemoDraft text))
                            Placeholder = "Memo durchsuchen…"
                            Size = Input.Small }
        ]
    ]

/// Betragsfeld mit sichtbarem Euro-Hinweis rechts im Feld
let private amountField (value: string) (onChange: string -> unit) (placeholder: string) : ReactElement =
    Html.div [
        prop.className "relative"
        prop.children [
            Input.text { Input.textInputDefaults with
                            Value = value
                            OnChange = onChange
                            Placeholder = placeholder
                            Size = Input.Small
                            ClassName = Some "pr-8" }
            Html.span [
                prop.className $"absolute inset-y-0 right-3 flex items-center pointer-events-none {Tokens.ListRoles.muted}"
                prop.text "€"
            ]
        ]
    ]

/// Das Betrags-Suchfeld, Von/Bis (AC: Suchfeld mit kurzer Verzögerung beim Tippen).
let private amountPickerBody (dispatch: Msg -> unit) (model: Model) =
    Html.div [
        prop.className "grid grid-cols-2 gap-3"
        prop.children [
            fieldLabel "Von" (amountField model.AmountMinDraft (fun text -> dispatch (SetAmountMinDraft text)) "z. B. 5")
            fieldLabel "Bis" (amountField model.AmountMaxDraft (fun text -> dispatch (SetAmountMaxDraft text)) "z. B. 50")
        ]
    ]

let private chipTitle (chip: ChipId) : string =
    match chip with
    | NeedsReviewChip -> ""
    | CategoryChip -> "Kategorie"
    | WithoutCategoryChip -> "Ohne Kategorie"
    | DateChip -> "Zeitraum"
    | PayeeChip -> "Payee"
    | MemoChip -> "Memo"
    | AmountChip -> "Betrag"

/// Breite des PC-Popovers: Kategorie 460px, Payee 340px, sonst 320px (K5).
let popoverWidth (chip: ChipId) : string =
    match chip with
    | CategoryChip -> FilterPopover.categoryWidthClass
    | PayeeChip -> FilterPopover.payeeWidthClass
    | _ -> FilterPopover.defaultWidthClass

let private popoverBody (dispatch: Msg -> unit) (model: Model) (categories: YnabCategory list) (chip: ChipId) : ReactElement =
    match chip with
    | NeedsReviewChip
    | WithoutCategoryChip -> Html.none
    | CategoryChip -> categoryPickerBody dispatch model categories
    | DateChip -> datePickerBody dispatch model
    | PayeeChip -> payeePickerBody dispatch model
    | MemoChip -> memoPickerBody dispatch model
    | AmountChip -> amountPickerBody dispatch model

/// Die Chrome um einen Popover-Inhalt: BottomSheet am Handy, `FilterPopover` am
/// PC. Siehe Modul-Kommentar oben, warum das zur Renderzeit entschieden wird,
/// statt beide Varianten gleichzeitig (versteckt) zu montieren.
let private filterChrome (dispatch: Msg -> unit) (chip: ChipId) (isOpen: bool) (body: ReactElement) : ReactElement =
    if Viewport.isDesktopViewport () then
        FilterPopover.view
            { IsOpen = isOpen; OnClose = (fun () -> dispatch CloseFilterPopover); WidthClassName = popoverWidth chip }
            [ body ]
    else
        BottomSheet.view
            { IsOpen = isOpen; OnClose = (fun () -> dispatch CloseFilterPopover); Title = chipTitle chip; Subtitle = None; Footer = None }
            [ body ]

let private categoryChipLabel (filters: Filters) : string = $"Kategorie: {Set.count filters.CategoryIds}"

let private dateChipLabel (filters: Filters) : string =
    filters.DateRange
    |> Option.bind (fun r -> r.Quick)
    |> Option.map dateQuickLabel
    |> Option.defaultValue "Zeitraum"

let private chipLabel (model: Model) (chip: ChipId) : string =
    match chip with
    | NeedsReviewChip -> needsReviewChipLabel model.NeedsReviewCount
    | CategoryChip -> if hasCategoryFilter model.Filters then categoryChipLabel model.Filters else "Kategorie"
    | WithoutCategoryChip -> "Ohne Kategorie"
    | DateChip -> if hasDateFilter model.Filters then dateChipLabel model.Filters else "Zeitraum"
    | PayeeChip -> model.Filters.Payee |> Option.defaultValue "Payee"
    | MemoChip -> model.Filters.Memo |> Option.defaultValue "Memo"
    | AmountChip -> "Betrag"

let private isChipActive (model: Model) (chip: ChipId) : bool =
    match chip with
    | NeedsReviewChip -> model.OnlyNeedsReview
    | CategoryChip -> hasCategoryFilter model.Filters
    | WithoutCategoryChip -> model.Filters.WithoutCategory
    | DateChip -> hasDateFilter model.Filters
    | PayeeChip -> hasPayeeFilter model.Filters
    | MemoChip -> hasMemoFilter model.Filters
    | AmountChip -> hasAmountFilter model.Filters

let private removeChipMsg (chip: ChipId) : Msg option =
    match chip with
    | NeedsReviewChip -> None
    | CategoryChip -> Some RemoveCategoryFilter
    | WithoutCategoryChip -> None
    | DateChip -> Some RemoveDateFilter
    | PayeeChip -> Some RemovePayeeFilter
    | MemoChip -> Some RemoveMemoFilter
    | AmountChip -> Some RemoveAmountFilter

let private chipOnClick (dispatch: Msg -> unit) (chip: ChipId) : unit -> unit =
    match chip with
    | NeedsReviewChip -> fun () -> dispatch ToggleOnlyNeedsReview
    | WithoutCategoryChip -> fun () -> dispatch ToggleWithoutCategory
    | other -> fun () -> dispatch (OpenFilterPopover other)

/// Ein Chip samt seiner Chrome (Sheet/Popover), wenn er eine eigene Auswahl hat.
let private filterChip (dispatch: Msg -> unit) (model: Model) (categories: YnabCategory list) (chip: ChipId) : ReactElement =
    let chipElement =
        ChipBar.chip {
            Label = chipLabel model chip
            IsActive = isChipActive model chip
            OnClick = chipOnClick dispatch chip
            OnRemove = removeChipMsg chip |> Option.map (fun msg -> fun () -> dispatch msg)
        }
    match chip with
    | NeedsReviewChip
    | WithoutCategoryChip -> chipElement
    | _ ->
        let isOpen = model.OpenPopover = Some chip
        ChipBar.chipWithPopover chipElement (filterChrome dispatch chip isOpen (popoverBody dispatch model categories chip))

/// Der Sortier-Chip am Ende der Filterleiste samt Sheet (ledger-skpcx). Er steht nur,
/// wenn die Liste als Handy-Liste gerendert wird (`visibleColumns` liefert `CompactList`);
/// dann ist er die einzige Chrome dieser Art (ADR 0043). Aktiv gefärbt, wenn die wirksame
/// Sortierung von der Standardsortierung abweicht. Ohne "×", kein Teil von `hasAnyFilter`.
let private sortChip (dispatch: Msg -> unit) (model: Model) (isAllAccounts: bool) : ReactElement =
    let sort = Components.Ledger.State.effectiveSort model
    let chipElement =
        ChipBar.chip {
            Label = Components.Ledger.State.sortChipLabel sort
            IsActive = sort <> LedgerSort.defaultSort
            OnClick = (fun () -> dispatch OpenSortSheet)
            OnRemove = None
        }
    let options = Components.Ledger.State.sortOptions isAllAccounts
    let sheet =
        BottomSheet.view
            { IsOpen = model.SortSheetOpen
              OnClose = (fun () -> dispatch CloseSortSheet)
              Title = "Sortieren"
              Subtitle = None
              Footer = None }
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
    ChipBar.chipWithPopover chipElement sheet

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
    let isDesktop = Viewport.isDesktopViewport ()
    let activity = chipActivityOf model.OnlyNeedsReview model.Filters
    let order = orderChipsForDisplay (not isDesktop) activity
    ChipBar.bar
        elementRef
        ((order |> List.map (filterChip dispatch model categories))
         @ (if showSortChip then [ sortChip dispatch model isAllAccounts ] else [])
         @ (if hasAnyFilter model.Filters then [ ChipBar.clearAllButton (fun () -> dispatch ClearAllFilters) ] else []))

/// Zustand der Summenzeile: ohne Filter nichts, beim Laden ein Platzhalter, danach
/// Trefferzahl und Summe (auch bei 0 Treffern). Ein Ladefehler zeigt keine Zeile, weil
/// Zahl und Summe dann nicht zum Filter passen.
[<RequireQualifiedAccess>]
type SummaryState =
    | Hidden
    | Loading
    | Ready

let summaryState (model: Model) : SummaryState =
    if not (hasAnyFilter model.Filters) then
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

/// Die Summenzeile über der Liste (AC: erscheint genau dann, wenn mindestens ein
/// Filter aktiv ist; Trefferzahl und Summe über den ganzen Treffer, nicht die Seite
/// (beide kommen unverändert vom Server, `model.TotalCount`/`model.TotalAmount`).
/// Ohne Karte, eine schlanke Zeile; beim Laden bleibt sie als Skeleton stehen.
let private summaryRow (model: Model) : ReactElement =
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
                        prop.text (summaryCountText model.TotalCount)
                    ]
                    Money.listAmount model.TotalAmount.Amount model.TotalAmount.Currency
            ]
        ]

/// Die Summenzeile im Auswahl-Zustand (ledger-r1d2f): "Auswahl: n von N Buchungen" mit der
/// Summe der Auswahl und der Teal-Kante. Sie ersetzt den Treffer-Zustand, solange die Auswahl
/// nicht leer ist, und steht auch ohne aktiven Filter da. Anzahl und Summe liefert nur
/// `selectionTotals`.
let private selectionSummaryRow (model: Model) : ReactElement =
    let totals = selectionTotals model
    MultiSelect.summaryRow (selectionSummaryText totals.Count model.TotalCount) totals.Amount

/// Summenzeile über der Liste: die Auswahl, wenn eine läuft, sonst der Treffer.
let private summaryOrSelectionRow (model: Model) : ReactElement =
    if hasSelection model then selectionSummaryRow model else summaryRow model

/// Steht der Tastaturhinweis da? Nur mit geladenen Zeilen und ohne Auswahl, an deren Stelle
/// die Leiste tritt.
let keyboardHintVisible (model: Model) : bool =
    match model.Entries with
    | Success (_ :: _) -> not (hasSelection model)
    | _ -> false

/// Die Auswahl-Leiste am unteren Rand (ledger-r1d2f), mit der einen primären Aktion
/// "Geprüft" (ledger-nhwrt). Am Handy
/// erscheint sie im Auswahl-Modus und ersetzt die Tab-Bar (zwei Reihen), am PC, solange die
/// Auswahl nicht leer ist (eine Reihe, ohne "Alle"). Sie ist `fixed` und steht deshalb
/// außerhalb der Seitenhülle mit der Eintrittsanimation (`animate-page-enter` setzt einen
/// Transform, in dem `fixed` an der Hülle statt am Fenster haftet): der Haupt-`View.fs`
/// rendert sie neben den Toasts.
let selectionBars (dispatch: Msg -> unit) (model: Model) : ReactElement =
    let totals = selectionTotals model
    let countText = selectionBarCountText totals.Count
    React.fragment [
        if model.SelectionMode then
            MultiSelect.barMobile
                countText
                totals.Amount
                selectionAllLabel
                (isAllSelected model.Selection)
                (fun () -> dispatch ToggleSelectAll)
                selectionReviewedLabel
                model.ReviewMarkBatchRunning
                (fun () -> dispatch StartReviewMarkBatch)
                selectionClearLabel
                (fun () -> dispatch ClearSelection)
        if totals.Count > 0 then
            MultiSelect.barDesktop
                countText
                totals.Amount
                selectionReviewedLabel
                model.ReviewMarkBatchRunning
                (fun () -> dispatch StartReviewMarkBatch)
                selectionClearLabel
                (fun () -> dispatch ClearSelection)
        elif keyboardHintVisible model then
            keyboardHint (keyboardHintTextFor model)
    ]

/// Je ein Abstandshalter in Höhe der Leiste unter der Liste, damit die letzte Zeile
/// erreichbar bleibt: sie ist nie dauerhaft verdeckt.
let private selectionBarSpacers (model: Model) : ReactElement =
    React.fragment [
        if model.SelectionMode then Html.div [ prop.className "md:hidden h-40"; prop.ariaHidden true ]
        if hasSelection model then Html.div [ prop.className "hidden md:block h-20"; prop.ariaHidden true ]
        elif keyboardHintVisible model then Html.div [ prop.className "hidden md:block h-12"; prop.ariaHidden true ]
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

// Minimale ResizeObserver-Bindung (nicht Teil von Fable.Browser.Dom, analog zu
// `observeIntersection` oben und `Viewport.fs`s visualViewport-Bindung): meldet die
// aktuelle Rand-zu-Rand-Höhe des beobachteten Elements bei jeder Größenänderung.
// Bewusst `getBoundingClientRect().height` statt `entries[0].contentRect.height`:
// Letztere ist die Content-Box ohne Padding, hier `py-1` (8px) auf `ChipBar.bar`,
// und genau diese Differenz reichte aus, um den Tabellenkopf wieder in die
// Leiste hineinragen zu lassen (per Browsertest gefunden). `getBoundingClientRect()`
// liefert dieselbe Rand-zu-Rand-Höhe, die auch der Tabellenkopf-Test unten misst.
[<Emit(
    "(function(el, cb) { var o = new ResizeObserver(function() { cb(el.getBoundingClientRect().height); }); o.observe(el); return function() { o.disconnect(); }; })($0, $1)"
)>]
let private observeHeight (element: Browser.Types.Element) (onResize: float -> unit) : (unit -> unit) =
    jsNative

// Wie `observeHeight`, nur für die Breite: die Tabellenbreite entscheidet, welche
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
    (dispatch: Msg -> unit)
    (model: Model)
    (categories: YnabCategory list)
    (accounts: LedgerAccountView list)
    (isAllAccounts: bool)
    =
    let barHeight, setBarHeight = React.useState 0.0
    // 0 = noch nicht gemessen, dann gilt `assumedTableWidth`.
    let measuredWidth, setMeasuredWidth = React.useState 0.0
    let tableWidth = if measuredWidth > 0.0 then measuredWidth else assumedTableWidth
    // Die Form des Details hängt nur von der Breite OHNE Panel ab (`detailFormFor`): das
    // Panel reserviert seinen Platz als Innenabstand, die gemessene Außenbreite bleibt gleich.
    let detailForm = detailFormFor tableWidth isAllAccounts
    let panelOpen = model.DetailFor.IsSome && detailForm = DetailPanel
    React.useEffect (
        (fun () -> if model.DetailForm <> detailForm then dispatch (DetailFormChanged detailForm)),
        [| box detailForm; box model.DetailForm |]
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
                    disconnectRef.current <- Some (observeHeight element setBarHeight)),
            [||]
        )

    Html.div [
        prop.className "flex-1 min-w-0 relative"
        prop.ref columnCallbackRef
        prop.style [ style.custom ("--ledger-filter-bar-height", sprintf "%.0fpx" barHeight) ]
        prop.children [
            // Das Sheet portalt nach `document.body`; es steht hier, weil die Form hier bekannt ist.
            detailSheetView dispatch model (model.DetailFor.IsSome && detailForm = DetailSheet)
            match detailEntryOf model with
            | Some entry when panelOpen -> detailPanelView dispatch model entry
            | _ -> ()
            Html.div [
                // Neben dem Panel macht die Spalte Platz; die Mindesthöhe hält das haftende
                // Panel auch bei kurzer Liste im Bild.
                prop.className (if panelOpen then "md:pr-[420px] md:min-h-[calc(100vh-7rem)]" else "")
                prop.children [
            Primitives.Stack.md [
                if model.MirrorRunning then
                    // Der Lauf blockiert die Liste nicht: sie bleibt
                    // stehen, der Hinweis steht daneben (ADR 0014).
                    Loading.inlineWithText "Spiegel läuft — die Liste bleibt so lange stehen."

                filterChipBar dispatch model categories (visibleColumns tableWidth isAllAccounts = CompactList) isAllAccounts (Some barCallbackRef)
                summaryOrSelectionRow model
                deletedToggleRow dispatch model
                entryList dispatch accounts isAllAccounts tableWidth panelOpen model
                selectionBarSpacers model
            ]
                ]
            ]
        ]
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    let accounts = model.Accounts |> RemoteData.toOption |> Option.map (fun a -> a.Accounts) |> Option.defaultValue []
    let balances = model.Balances |> RemoteData.toOption |> Option.defaultValue []
    let isAllAccounts = model.SelectedFilter = Some AllAccounts
    let categories = model.Categories |> RemoteData.toOption |> Option.defaultValue []

    // Ruhige Liste (ADR 0049 Punkt 9): schlanker Seitenkopf, Liste füllt die Breite.
    // "Jetzt spiegeln" bleibt erreichbar, aber ohne den großen orangen Knopf: am
    // Handy als Symbol-Knopf in der Kopfzeile, am PC als sekundärer Knopf.
    Primitives.Container.full [
        PageHeader.calm
            "Kassenbuch"
            (lastRunSubtitle model.LastMirrorRun)
            [
                Button.view {
                    Button.defaultProps with
                        Icon = Some (Icons.sync Icons.SM Icons.Default)
                        Variant = Button.Secondary
                        IsLoading = model.MirrorRunning
                        Title = Some "Jetzt spiegeln"
                        ClassName = Some "md:hidden"
                        OnClick = (fun () -> dispatch StartMirror)
                }
                Button.view {
                    Button.defaultProps with
                        Text = if model.MirrorRunning then "Spiegel läuft…" else "Jetzt spiegeln"
                        Variant = Button.Secondary
                        IsLoading = model.MirrorRunning
                        ClassName = Some "hidden md:inline-flex"
                        OnClick = (fun () -> dispatch StartMirror)
                }
            ]

        match model.Accounts with
        | Failure message ->
            ErrorDisplay.cardWithTitle "Konten konnten nicht geladen werden" message (Some (fun () -> dispatch LoadAccounts))
        | Loading -> Loading.centered "Kassenbuch wird geöffnet…"
        | NotAsked | Success _ ->
            Html.div [
                prop.className "flex flex-col md:flex-row md:items-start md:gap-6 gap-4"
                prop.children [
                    accountStrip dispatch accounts balances model.SelectedFilter

                    FilterBarColumn dispatch model categories accounts isAllAccounts
                ]
            ]

        KeyboardNavigation model.FocusedRow dispatch
    ]
