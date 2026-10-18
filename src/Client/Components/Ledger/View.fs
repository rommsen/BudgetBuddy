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

/// `CultureInfo("de-DE")` funktioniert bei Datumsformaten (anders als bei
/// Geldbetraegen, siehe `Money.formatAmountText`) auch in der nach JavaScript
/// uebersetzten Fassung, weil `DateTime.ToString` mit festem Format-String keine
/// kulturabhaengige Zifferngruppierung braucht. Bewusst *keine* gemeinsame
/// Modul-Konstante: ein Top-Level-`let`-Wert wuerde beim ersten Zugriff das gesamte
/// Modul initialisieren (F# "startup code"), inklusive der `Badge.view`-Werte weiter
/// unten in dieser Datei — unter `dotnet test` (kein Fable/Feliz-Laufzeitkontext)
/// reisst das eine bereits bestehende `Client.DesignSystem.Badge`-Feliz-Prop in eine
/// `InvalidCastException`. Jede Funktion baut ihre eigene `CultureInfo`-Instanz, das
/// haelt `formatEntryDate` unit-testbar ohne diese Kaskade auszuloesen.
let private germanCulture () = System.Globalization.CultureInfo("de-DE")

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

/// Die Herkunft einer Buchung als Badge. Der Spiegel ist die haeufigste Herkunft und
/// bleibt deshalb ruhig (Info); die uebrigen Herkuenfte sind selten und duerfen
/// auffallen.
let private originBadge (origin: LedgerOrigin) =
    let variant =
        match origin with
        | MirroredFromYnab -> Badge.Info
        | ImportedFromComdirect -> Badge.Neutral
        | DecidedByRule -> Badge.Purple
        | DecidedInBb -> Badge.Success
        | AddedViaQuickAdd -> Badge.Orange
        | MatchedByOrderId -> Badge.Warning

    Badge.view {
        Badge.defaultProps with
            Text = originLabel origin
            Variant = variant
            Size = Badge.Small
    }

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

/// Ein Kennzeichen in Zeile 2 der Handy-Zeile, das nicht schrumpfen darf.
let private keepWidth (child: ReactElement) =
    Html.span [ prop.className "shrink-0"; prop.children [ child ] ]

/// Eine Zeile der Buchungsliste am Handy, zweizeilig (ADR 0049 Punkt 6): Zeile 1
/// Payee und Betrag, Zeile 2 Kategorie und Memo mit den Kennzeichen. Beide Zeilen
/// haben eine feste Höhe (`h-5`), damit der Abstand zwischen ihnen in jeder Zeile
/// gleich ist, auch wenn Zeile 2 ein Kennzeichen trägt. `accountAbbreviationOf` ist
/// nur in der Gesamtsicht gesetzt (AC4): das Konto steht dann als Kürzel-Badge in
/// Zeile 2. Die Herkunft steht nicht mehr in der Zeile, nur noch in der Historie.
let private entryRow
    (dispatch: Msg -> unit)
    (accountAbbreviationOf: (Guid -> string) option)
    (entry: LedgerEntryView)
    =
    let rowClass = if entry.NeedsReview then entryRowReviewClassName else entryRowClassName

    Html.button [
        prop.key (string entry.Id)
        prop.className rowClass
        prop.onClick (fun _ -> dispatch (OpenHistory entry))
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
/// Zellfarbe, unabhängig von der Reihenfolge im kompilierten CSS (ADR 0031).
let private categoryCell (entry: LedgerEntryView) =
    truncatedRoleCell
        Table.Secondary
        (Html.span [ prop.className (categoryClassName entry); prop.text (categoryLabelQualified entry) ])

/// Breite der Status-Spalte in der PC-Tabelle: der Handoff-Wert aus Screen 1.9
/// (Entscheidung Roman 2026-09-19, `ledger-qns4j`).
let private statusColumnWidth = "128"

/// Der Betrag als eigene Zelle: rechtsbündig über `Table.amountCell` (Listenvariante,
/// ADR 0049); gedämpft und durchgestrichen für eine in YNAB gelöschte Buchung.
let private tdMoney (entry: LedgerEntryView) =
    if entry.DeletedInYnab then
        Table.cell { Table.defaultCellProps with Align = Some "right" } [ entryMoney entry ]
    else
        Table.amountCell entry.Amount.Amount entry.Amount.Currency

let private entryTableRow
    (dispatch: Msg -> unit)
    (showAccountColumn: bool)
    (accountAbbreviationOf: Guid -> string)
    (entry: LedgerEntryView)
    =
    // "Zu prüfen" ist die Statuskante (`StatusEdge`, ADR 0049 Punkt 7), nicht mehr
    // ein Rahmen links. `Table.trClickable` kennt weder Kante noch Zeilenhöhe,
    // deshalb direkt `Table.row` mit denselben Klassen wie `Table.calmRow`.
    Table.row
        { Table.defaultRowProps with
            OnClick = Some (fun () -> dispatch (OpenHistory entry))
            StatusEdge = entry.NeedsReview
            ClassName = Some Tokens.ListRoles.rowHeight }
        [
            Table.roleCell Table.DateCell [ Html.text (formatDate entry.Date) ]
            if showAccountColumn then Table.roleCell Table.Muted [ Html.text (accountAbbreviationOf entry.AccountId) ]
            truncatedRoleCell Table.Payee (Html.text (payeeLabel entry))
            categoryCell entry
            truncatedRoleCell Table.Muted (Html.text (entry.Memo |> Option.defaultValue ""))
            tdMoney entry
            Table.cell { Table.defaultCellProps with Align = Some "right" } [
                Html.div [
                    // `flex-nowrap`, nicht `flex-wrap`: die Kennzeichen sollen die
                    // Spalte in die Breite gehen lassen, nicht die Zeile in die Höhe
                    // (AC5, ledger-qns4j, siehe `statusColumnWidth`). Kein
                    // `originBadge` (AC3): die Herkunft steht nur noch in der
                    // Historie (`historyLine`): die Statusspalte trägt nur Status.
                    prop.className "flex flex-nowrap items-center justify-end gap-1"
                    prop.children [
                        if entry.NeedsReview then needsReviewStatusBadge ()
                        flagStatusBadge entry.Flag
                        if entry.DeletedInYnab then deletedStatusIcon ()
                    ]
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
// Historie (BottomSheet nach ADR 0005)
// ============================================

/// Die Anzeige-Reihenfolge der Historie (ledger-h9tq2, Entscheidung Roman
/// 2026-09-18): die neueste Zeile steht oben. `getEntryHistory` liefert aufsteigend
/// (älteste zuerst, ADR 0013), die Drehung sitzt bewusst nur hier in der Anzeige,
/// nicht in der Abfrage oder im Lesepfad. Die laufende Nummer je Zeile bleibt die
/// aus dem Ereignis-Log und zählt dadurch in der Liste von oben nach unten abwärts.
let historyLinesForDisplay (lines: LedgerHistoryLine list) : LedgerHistoryLine list = List.rev lines

let private historyLine (line: LedgerHistoryLine) =
    Html.div [
        prop.key (string line.Sequence)
        prop.className $"py-3 border-b {Tokens.Borders.subtle} last:border-b-0"
        prop.children [
            Primitives.HStack.spaceBetween [
                Html.span [
                    prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono}"
                    prop.text (formatDateTime line.OccurredAt)
                ]
                originBadge line.Origin
            ]
            Html.p [
                prop.className $"{Tokens.FontSizes.body} {Tokens.Colors.textPrimary} mt-1"
                prop.text line.Description
            ]
        ]
    ]

let private historySheet (model: Model) (dispatch: Msg -> unit) =
    let title =
        model.HistoryFor
        |> Option.bind (fun e -> e.Payee)
        |> Option.defaultValue "Buchung"

    let subtitle =
        // Volles Datum im Detail (AC3), nicht die relative Form der Liste.
        model.HistoryFor
        |> Option.map (fun e -> $"{formatDateFull e.Date} · Historie")

    BottomSheet.view
        {
            IsOpen = model.HistoryFor.IsSome
            OnClose = fun () -> dispatch CloseHistory
            Title = title
            Subtitle = subtitle
            Footer = None
        }
        [
            match model.History with
            | NotAsked -> Html.none
            | Loading -> Loading.centered "Historie wird geladen…"
            | Failure message ->
                ErrorDisplay.cardCompact
                    $"Historie konnte nicht geladen werden: {message}"
                    (model.HistoryFor |> Option.map (fun e -> fun () -> dispatch (LoadHistory e.Id)))
            | Success [] ->
                Html.p [
                    prop.className $"{Tokens.FontSizes.body} {Tokens.Colors.textMuted} py-6 text-center"
                    prop.text "Für diese Buchung ist noch nichts festgehalten."
                ]
            | Success lines ->
                Html.div [
                    prop.children (lines |> historyLinesForDisplay |> List.map historyLine)
                ]
        ]

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
// Seite
// ============================================

let private entryListMobile
    (dispatch: Msg -> unit)
    (accountAbbreviationOf: (Guid -> string) option)
    (includeDeleted: bool)
    (entries: LedgerEntryView list)
    =
    Primitives.Responsive.mobileOnly [
        Html.div [
            prop.children (
                groupByDate entries
                |> List.collect (fun (date, group) ->
                    dateGroupHeader date (daySum includeDeleted group)
                    :: (group |> List.map (entryRow dispatch accountAbbreviationOf)))
            )
        ]
    ]

/// Eine Spalte der `colgroup`: `None` heißt "Rest" (Memo).
let private col (width: IStyleAttribute option) =
    Html.col [ match width with Some w -> prop.style [ w ] | None -> () ]

let private entryListDesktop
    (dispatch: Msg -> unit)
    (showAccountColumn: bool)
    (accountAbbreviationOf: Guid -> string)
    (includeDeleted: bool)
    (entries: LedgerEntryView list)
    =
    // Spalten vor dem Betrag: Datum, [Konto], Payee, Kategorie, Memo.
    let columnsBeforeAmount = if showAccountColumn then 5 else 4

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
                Html.colgroup [
                    col (Some (style.width (length.px 96)))
                    if showAccountColumn then col (Some (style.width (length.px 72)))
                    col (Some (style.width (length.percent 30)))
                    col (Some (style.width (length.percent 26)))
                    col None
                    col (Some (style.width (length.px 132)))
                    col (Some (style.width (length.px (int statusColumnWidth))))
                ]
                Table.thead [
                    Table.calmHeaderCell "Datum" None
                    if showAccountColumn then Table.calmHeaderCell "Konto" None
                    Table.calmHeaderCell "Payee" None
                    Table.calmHeaderCell "Kategorie" None
                    Table.calmHeaderCell "Memo" None
                    Table.calmHeaderCell "Betrag" (Some "right")
                    Table.calmHeaderCell "Status" (Some "right")
                ]
                Table.tbody (
                    groupByDate entries
                    |> List.collect (fun (date, group) ->
                        dateGroupRow columnsBeforeAmount date (daySum includeDeleted group)
                        :: (group
                            |> List.map (fun entry ->
                                React.keyedFragment (
                                    string entry.Id,
                                    [ entryTableRow dispatch showAccountColumn accountAbbreviationOf entry ]
                                ))))
                )
            ]
    ]

let private entryList
    (dispatch: Msg -> unit)
    (accounts: LedgerAccountView list)
    (isAllAccounts: bool)
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
        Html.div [
            entryListMobile dispatch (if isAllAccounts then Some abbreviationOf else None) model.IncludeDeleted entries
            entryListDesktop dispatch isAllAccounts abbreviationOf model.IncludeDeleted entries

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

let private dateDraftInput (label: string) (value: DateTime option) (onChange: DateTime option -> unit) =
    Input.groupSimple label (
        Html.input [
            prop.type' "date"
            prop.className "w-full bg-surface-input border border-border-default rounded-lg px-3 py-2 text-sm text-text-primary"
            prop.value (value |> Option.map (fun d -> d.ToString("yyyy-MM-dd")) |> Option.defaultValue "")
            prop.onChange (fun (text: string) ->
                if String.IsNullOrWhiteSpace text then
                    onChange None
                else
                    match DateTime.TryParse text with
                    | true, d -> onChange (Some d)
                    | false, _ -> onChange None)
        ]
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
                prop.className "grid grid-cols-2 gap-3"
                prop.children [
                    dateDraftInput "Von" model.DateDraft.From (fun v -> dispatch (SetDateDraftFrom v))
                    dateDraftInput "Bis" model.DateDraft.To (fun v -> dispatch (SetDateDraftTo v))
                ]
            ]
            Primitives.HStack.spaceBetween [
                Button.ghost "Zurücksetzen" (fun () -> dispatch ResetDateDraft)
                Button.primary "Übernehmen" (fun () -> dispatch ApplyDateFilter)
            ]
        ]
    ]

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
            Input.textSimple model.PayeeSearch (fun text -> dispatch (SetPayeeSearch text)) "Payee suchen…"
            Html.div [
                prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono} uppercase tracking-wider"
                prop.text $"Vorschläge aus den {model.TotalCount} Treffern"
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
                                prop.className "flex items-center justify-between py-2.5 text-left"
                                prop.onClick (fun _ -> dispatch (SelectPayeeFilter s.Payee))
                                prop.children [
                                    Html.span [ prop.className $"{Tokens.FontSizes.body} {Tokens.Colors.textPrimary}"; prop.text s.Payee ]
                                    Html.span [
                                        prop.className $"{Tokens.Fonts.mono} {Tokens.Colors.textMuted} {Tokens.FontSizes.xs} tabular-nums"
                                        prop.text (string s.Count)
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
        prop.children [ Input.textSimple model.MemoDraft (fun text -> dispatch (SetMemoDraft text)) "Memo durchsuchen…" ]
    ]

/// Das Betrags-Suchfeld, Von/Bis (AC: Suchfeld mit kurzer Verzögerung beim Tippen).
let private amountPickerBody (dispatch: Msg -> unit) (model: Model) =
    Html.div [
        prop.className "grid grid-cols-2 gap-3"
        prop.children [
            Input.groupSimple "Von" (Input.textSimple model.AmountMinDraft (fun text -> dispatch (SetAmountMinDraft text)) "z. B. 5")
            Input.groupSimple "Bis" (Input.textSimple model.AmountMaxDraft (fun text -> dispatch (SetAmountMaxDraft text)) "z. B. 50")
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

let private popoverWidth (chip: ChipId) : string =
    match chip with
    | PayeeChip -> "w-[min(92vw,340px)]"
    | _ -> "w-[min(92vw,320px)]"

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
    (elementRef: (Browser.Types.Element -> unit) option)
    : ReactElement =
    let isDesktop = Viewport.isDesktopViewport ()
    let activity = chipActivityOf model.OnlyNeedsReview model.Filters
    let order = orderChipsForDisplay (not isDesktop) activity
    ChipBar.bar
        elementRef
        ((order |> List.map (filterChip dispatch model categories))
         @ (if hasAnyFilter model.Filters then [ ChipBar.clearAllButton (fun () -> dispatch ClearAllFilters) ] else []))

/// Die Summenzeile über der Liste (AC: erscheint genau dann, wenn mindestens ein
/// Filter aktiv ist; Trefferzahl und Summe über den ganzen Treffer, nicht die Seite
/// (beide kommen unverändert vom Server, `model.TotalCount`/`model.TotalAmount`).
let private summaryRow (model: Model) : ReactElement =
    if not (hasAnyFilter model.Filters) then
        Html.none
    else
        Card.standard [
            Primitives.HStack.spaceBetween [
                Html.span [
                    prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textSecondary}"
                    prop.text (
                        let noun = if model.TotalCount = 1 then "Buchung" else "Buchungen"
                        $"{model.TotalCount} {noun}")
                ]
                Money.transactionWithCurrency model.TotalAmount.Amount model.TotalAmount.Currency
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
    let disconnectRef = React.useRef<(unit -> unit) option> None

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
        prop.className "flex-1 min-w-0"
        prop.style [ style.custom ("--ledger-filter-bar-height", sprintf "%.0fpx" barHeight) ]
        prop.children [
            Primitives.Stack.md [
                if model.MirrorRunning then
                    // Der Lauf blockiert die Liste nicht: sie bleibt
                    // stehen, der Hinweis steht daneben (ADR 0014).
                    Loading.inlineWithText "Spiegel läuft — die Liste bleibt so lange stehen."

                filterChipBar dispatch model categories (Some barCallbackRef)
                summaryRow model
                deletedToggleRow dispatch model
                entryList dispatch accounts isAllAccounts model
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

        historySheet model dispatch
    ]
