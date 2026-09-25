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

/// Das Status-Kennzeichen "Zu prüfen" der PC-Statusspalte (ADR 0032, Entscheidung
/// Roman 2026-09-19, `ledger-qns4j`): dieselbe Quelle wie der Punkt am linken
/// Zeilenrand der Handy-Zeile (`entry.NeedsReview`), am PC aber als Badge mit Text
/// statt als Punkt: "Ungeprüft-Kennzeichen ist Pink (Punkt am Handy, Badge am PC)"
/// aus dem Ablauf-1-Handoff.
let private needsReviewStatusBadge () =
    Badge.view {
        Badge.defaultProps with
            Text = "Zu prüfen"
            Variant = Badge.Warning
            Size = Badge.Small
    }

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

/// Der Betrag einer Zeile: gedämpft und durchgestrichen, wenn die Buchung in YNAB
/// gelöscht ist (AC3, ledger-p3n8k), sonst die gewohnte rot/grüne Betragsfarbe.
let private entryMoney (entry: LedgerEntryView) =
    if entry.DeletedInYnab then
        Money.transactionDeleted entry.Amount.Amount entry.Amount.Currency
    else
        Money.transactionWithCurrency entry.Amount.Amount entry.Amount.Currency

/// Das Konto-Kuerzel einer Zeile in der Gesamtsicht (AC4).
let private accountBadge (abbreviation: string) =
    Badge.view {
        Badge.defaultProps with
            Text = abbreviation
            Variant = Badge.Neutral
            Size = Badge.Small
    }

/// Die Kategorie-Beschriftung einer Buchung (AC1/AC2, ledger-w9skm): der
/// Kategoriename, bei einer aufgeteilten Buchung ohne eigene Kategorie die Anzahl
/// der Split-Zeilen, sonst der Platzhaltertext für eine fehlende Kategorie (siehe
/// Rückfallzeile unten). Reine Funktion, geteilt zwischen Handy-Zeile
/// (`entryRow`) und PC-Tabelle (`entryTableRow`), damit beide Ansichten denselben
/// Text zeigen (siehe `LedgerViewTests`; der Platzhaltertext steht bewusst nur an
/// dieser einen Stelle im Modul, ledger-n3pqw).
///
/// Zeigt bewusst nur den Blattnamen, nicht "Gruppe: Name": `CategoryRef`
/// (`Server/Ledger/Events.fs`) traegt keine Kategoriegruppe, nur `CategoryName`
/// als einzelnen String — die volle Qualifizierung aus dem Handoff kaeme erst
/// durch einen eigenen Zug durch Events/ReadModel (siehe Backlog-Item, Notes
/// dieser Aufgabe hatten das als Risiko benannt).
let categoryLabel (entry: LedgerEntryView) : string =
    match entry.CategoryName with
    | Some name -> name
    | None when not (List.isEmpty entry.Splits) -> $"{List.length entry.Splits} Split-Zeilen"
    | None -> "ohne Kategorie"

/// Der Payee-Text einer Buchung: der Payee, sonst der Platzhaltertext für eine
/// fehlende Angabe (siehe Rückfallzeile unten). Reine Funktion, analog zu
/// `categoryLabel` geteilt zwischen Handy-Zeile (`entryRow`) und PC-Tabelle
/// (`entryTableRow`), damit beide Ansichten denselben Platzhaltertext zeigen
/// (ledger-n3pqw, Lücke 9 aus dem QA-Review: vorher stand dieser Platzhalter an
/// zwei Stellen im File, je einmal pro Ansicht).
let payeeLabel (entry: LedgerEntryView) : string =
    entry.Payee |> Option.defaultValue "ohne Payee"

/// Die Kategorie-Farbrolle (AC1, ledger-w9skm, Handoff 1.1/1.9: `#a855f7` an
/// beiden Stellen). Über den Token `Tokens.Colors.neonPurple`, kein Hex-Literal
/// im View-Code; als eigener Wert ausgelagert und geteilt zwischen Handy-Zeile
/// und PC-Tabelle, damit beide dieselbe Farbe verwenden und der Regressionstest
/// prueft, dass hier tatsaechlich der Token steht statt eines Literals.
let categoryColorClassName : string = Tokens.Colors.neonPurple

/// Klassen der Handy-Listenzeile (AC3, ledger-w9skm): flache Zeile mit einer
/// Unterkante als Trenner statt einer Karte mit Rahmen und Eckenradius (Handoff
/// 1.1: `padding:13px 16px`, Unterkante `1px solid #1c1c38`). Als eigener Wert
/// ausgelagert, weil `dotnet test` (kein Fable/React-Laufzeitkontext) das
/// gerenderte `Html.button` selbst nicht pruefen kann; der Klassen-String ist
/// aber reiner Text und wird von `entryRow` unveraendert als `prop.className`
/// gesetzt — der Regressionstest in `LedgerViewTests` prueft also exakt das,
/// was am DOM-Knoten landet: kein `rounded-*`, kein umlaufender `border` mehr
/// (das war die Karte aus dem Befund), stattdessen `border-b`.
let entryRowClassName : string =
    "w-full text-left px-4 py-3 transition-colors touch-manipulation " +
    // `hover:bg-surface-hover` steht bewusst als Literal: Tailwind scannt den
    // Quelltext nach ganzen Klassennamen, eine zusammengesetzte Variante
    // (`hover:` + Token) fiele beim Scan durch und wuerde nie erzeugt.
    $"{Tokens.Backgrounds.dark} hover:bg-surface-hover " +
    $"border-b {Tokens.Borders.subtle}"

/// Eine Zeile der Buchungsliste am Handy. `accountAbbreviationOf` ist nur in der
/// Gesamtsicht gesetzt (AC4): das Konto steht dann als Kuerzel-Badge in der Zeile,
/// in der Konto-Sicht fehlt es ganz.
let private entryRow
    (dispatch: Msg -> unit)
    (accountAbbreviationOf: (Guid -> string) option)
    (entry: LedgerEntryView)
    =
    Html.button [
        prop.key (string entry.Id)
        prop.className entryRowClassName
        prop.onClick (fun _ -> dispatch (OpenHistory entry))
        prop.children [
            Html.div [
                // Der Punkt am linken Zeilenrand für ungeprüfte Buchungen (ADR 0032):
                // eine schmale, immer vorhandene Spalte, damit geprüfte und
                // ungeprüfte Zeilen nicht gegeneinander verrutschen.
                prop.className "flex items-start gap-2"
                prop.children [
                    Html.div [
                        prop.className "w-2 pt-1.5 shrink-0"
                        prop.children [ if entry.NeedsReview then Badge.dot Badge.Warning ]
                    ]
                    Html.div [
                        prop.className "min-w-0 flex-1"
                        prop.children [
                            Primitives.HStack.spaceBetween [
                                Html.div [
                                    prop.className "min-w-0 flex-1"
                                    prop.children [
                                        Html.div [
                                            prop.className $"{Tokens.FontSizes.body} {Tokens.Colors.textPrimary} truncate"
                                            prop.text (payeeLabel entry)
                                        ]
                                        // Zeile 2 (AC2, ledger-w9skm): Kategorie in der
                                        // Kategorie-Farbrolle (AC1) zusammen mit dem Memo,
                                        // nicht mehr mit dem Datum verschmolzen — das Datum
                                        // steht seither nur noch im `dateGroupHeader` der
                                        // Gruppe.
                                        Html.div [
                                            prop.className "flex items-center gap-2 mt-0.5 min-w-0"
                                            prop.children [
                                                Html.span [
                                                    prop.className $"{Tokens.FontSizes.xs} {categoryColorClassName} shrink-0"
                                                    prop.text (categoryLabel entry)
                                                ]
                                                match entry.Memo |> Option.filter (String.IsNullOrWhiteSpace >> not) with
                                                | Some memo ->
                                                    Html.span [
                                                        prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} truncate min-w-0"
                                                        prop.text memo
                                                    ]
                                                | None -> ()
                                            ]
                                        ]
                                        Html.div [
                                            prop.className "flex flex-wrap items-center gap-1.5 mt-1.5"
                                            prop.children [
                                                match accountAbbreviationOf with
                                                | Some abbreviationOf -> accountBadge (abbreviationOf entry.AccountId)
                                                | None -> ()
                                                originBadge entry.Origin
                                                flagBadge entry.Flag
                                                if entry.DeletedInYnab then deletedBadge ()
                                            ]
                                        ]
                                    ]
                                ]
                                Html.div [
                                    prop.className "shrink-0 pl-3"
                                    prop.children [ entryMoney entry ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

/// Der Gruppenkopf eines Datums in der Handy-Liste (AC6).
let private dateGroupHeader (date: DateTime) =
    Html.div [
        prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono} uppercase tracking-wider px-3 pt-3 pb-1"
        prop.text (formatDate date)
    ]

/// Eine Zeile der Buchungsliste am PC, als Tabellenzeile mit denselben Feldern.
/// Eine Zelle, die auf eine Zeile gekuerzt wird (Payee, Kategorie, Memo): die
/// Tabelle hat feste Spaltenbreiten (`FixedLayout`), ohne Kuerzung wuerde langer
/// Text sonst umbrechen und die Zeile hoeher machen als jede andere (AC4/AC5).
let private tdTruncated (text: string) =
    Table.cell { Table.defaultCellProps with IsTruncate = true } [ Html.text text ]

/// Die Kategorie-Zelle der PC-Tabelle (AC1, ledger-w9skm): derselbe gekuerzte
/// `Table.cell` wie `tdTruncated`, aber der Text steckt in einem eigenen `span`
/// mit der Kategorie-Farbrolle statt in `Table.cell`s Standard-Textfarbe. Ein
/// `span` mit eigener `color`-Klasse gewinnt gegenueber der vererbten Farbe der
/// `<td>`, unabhaengig von der Reihenfolge im kompilierten CSS (anders als eine
/// zusaetzliche Klasse auf der Zelle selbst, siehe ADR 0031).
let private tdTruncatedCategory (text: string) =
    Table.cell { Table.defaultCellProps with IsTruncate = true } [
        Html.span [ prop.className categoryColorClassName; prop.text text ]
    ]

/// Breite der Status-Spalte in der PC-Tabelle: der Handoff-Wert aus Screen 1.9
/// (Entscheidung Roman 2026-09-19, `ledger-qns4j`). Die vorherige Verbreiterung auf
/// 208px (ledger-x0yga, Verifier-Befund 2) war ausdrücklich ein Pflaster gegen
/// zwei umbrechende Kennzeichen; die Lösung liegt jetzt bei den Kennzeichen selbst
/// (kurze Beschriftung bzw. Symbol statt Text, siehe `needsReviewStatusBadge` und
/// `deletedStatusIcon`), nicht mehr bei der Spaltenbreite. Die 80px Differenz
/// gehen automatisch an Payee und Memo zurück: beide sind `Table.th` ohne eigene
/// `Width`, teilen sich also den frei gewordenen Platz als `1fr`-Spalten.
let private statusColumnWidth = "128"

/// Der Betrag als eigene Zelle ueber die Money-Komponente statt als reiner Text:
/// nur so tragen PC-Tabellen-Betraege dieselbe Farbsemantik (rot/gruen) und
/// Ziffernschrift wie die Handy-Liste (AC1/AC2, ledger-x0yga); gedämpft und
/// durchgestrichen für eine in YNAB gelöschte Buchung (AC3, ledger-p3n8k).
let private tdMoney (entry: LedgerEntryView) =
    Table.cell { Table.defaultCellProps with Align = Some "right" } [ entryMoney entry ]

let private entryTableRow
    (dispatch: Msg -> unit)
    (showAccountColumn: bool)
    (accountAbbreviationOf: Guid -> string)
    (entry: LedgerEntryView)
    =
    // Der Punkt am linken Zeilenrand (ADR 0032) wird an der PC-Tabellenzeile als
    // schmaler linker Rahmen nachgebildet: dasselbe Muster wie `Table.row`s eigenes
    // `IsSelected` (`border-l-2 border-l-neon-teal`), nur für "zu prüfen" statt
    // "ausgewählt". `Table.trClickable` deckt keinen eigenen `ClassName` ab, deshalb
    // direkt `Table.row` mit `OnClick`.
    Table.row
        { Table.defaultRowProps with
            OnClick = Some (fun () -> dispatch (OpenHistory entry))
            ClassName = Some (if entry.NeedsReview then "border-l-2 border-l-neon-pink" else "") }
        [
            Table.td (formatDate entry.Date)
            if showAccountColumn then Table.td (accountAbbreviationOf entry.AccountId)
            tdTruncated (payeeLabel entry)
            tdTruncatedCategory (categoryLabel entry)
            tdTruncated (entry.Memo |> Option.defaultValue "")
            tdMoney entry
            Table.tdContent [
                Html.div [
                    // `flex-nowrap`, nicht `flex-wrap`: die Kennzeichen sollen die
                    // Spalte in die Breite gehen lassen, nicht die Zeile in die Höhe
                    // (AC5, ledger-qns4j, siehe `statusColumnWidth`). Kein
                    // `originBadge` mehr (AC3): die Herkunft steht nur noch in der
                    // Historie (`historyLine`) und, sobald gebaut, im Detail
                    // (`ledger-q1hw2`): die Statusspalte trägt nur noch Status.
                    prop.className "flex flex-nowrap items-center gap-1"
                    prop.children [
                        if entry.NeedsReview then needsReviewStatusBadge ()
                        flagStatusBadge entry.Flag
                        if entry.DeletedInYnab then deletedStatusIcon ()
                    ]
                ]
            ]
        ]

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

/// Der Status des letzten Spiegel-Laufs als Unterzeile der Seite. Ein Fehler steht
/// hier genauso sichtbar wie ein Erfolg (ADR 0014).
let private lastRunSubtitle (status: RemoteData<MirrorRunStatus option>) : string option =
    match status with
    | Success (Some run) ->
        let outcome =
            match run.Outcome with
            | MirrorRunSucceeded summary ->
                $"{summary.NewEntries} neu, {summary.ChangedEntries} geändert, {summary.DeletedEntries} gelöscht"
            | MirrorRunFailed message -> $"gescheitert: {message}"
        Some $"Letzter Spiegel: {formatDateTime run.RanAt} — {outcome}"
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
    (entries: LedgerEntryView list)
    =
    Primitives.Responsive.mobileOnly [
        Html.div [
            prop.children (
                groupByDate entries
                |> List.collect (fun (date, group) ->
                    dateGroupHeader date :: (group |> List.map (entryRow dispatch accountAbbreviationOf)))
            )
        ]
    ]

let private entryListDesktop
    (dispatch: Msg -> unit)
    (showAccountColumn: bool)
    (accountAbbreviationOf: Guid -> string)
    (entries: LedgerEntryView list)
    =
    Primitives.Responsive.desktopOnly [
        Table.view
            // `FixedLayout`: feste Spaltenanteile statt Content-getriebener Breite
            // (AC5) — ohne sie zwingt ein langer Payee/Kategorie-Text den Betrag aus
            // dem sichtbaren Bereich, wie im Ausgangszustand. Spaltenraster nach dem
            // Claude-Design-Handoff (`docs/ux/kassenbuch/handoff-ablauf-1-...md`,
            // Screen 1.9): `96px 1fr 200px 1fr 132px 128px` fuer Datum, Payee,
            // Kategorie, Memo, Betrag, Status (das Kaestchen `44px` davor gehoert zu
            // `ledger-r1d2f`, nicht zu dieser Aufgabe). Payee und Memo bleiben ohne
            // Breitenvorgabe und teilen sich den Rest zu gleichen Teilen (`1fr` je
            // Spalte), wie im Handoff. Die Status-Spalte weicht vom Handoff-Wert
            // (128px) ab — siehe `statusColumnWidth`.
            // `Responsive = false`: `Sticky` and the wrapper's `overflow-x-auto`
            // don't mix (see `TableProps.Responsive` doc) — without this the sticky
            // header positions itself against the wrapper instead of the page and
            // ends up floating mid-list. `ClassName` reapplies the wrapper's own
            // decorative classes directly on the `<table>` so it keeps its border.
            // `Variant = Hover`: pins today's look. `defaultTableProps.Variant` is
            // `ZebraHover`, but `Zebra` used to be the DaisyUI class `table-zebra`,
            // inert without DaisyUI (design-system-fq8dn, ADR 0031) — this list's
            // reference screenshots (`docs/ux/kassenbuch/screenshots/`) show no
            // stripe. Now that `Zebra` actually renders, the implicit default would
            // add one here; explicit `Hover` keeps only what was already visible.
            { Table.defaultTableProps with
                Variant = Table.Hover
                Sticky = true
                FixedLayout = true
                Responsive = false
                ClassName = Some "rounded-xl border border-border-subtle bg-surface-card" }
            [
                Table.thead [
                    Table.thWidth "Datum" "96"
                    if showAccountColumn then Table.thWidth "Konto" "72"
                    Table.th "Payee"
                    Table.thWidth "Kategorie" "200"
                    Table.th "Memo"
                    Table.headerCell
                        { Table.defaultHeaderCell with
                            Text = "Betrag"
                            Align = Some "right"
                            Width = Some "132" }
                    Table.thWidth "Status" statusColumnWidth
                ]
                Table.tbody (
                    entries |> List.map (entryTableRow dispatch showAccountColumn accountAbbreviationOf)
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

    | Success [] ->
        Card.emptyState
            (Icons.banknotes Icons.XL Icons.Default)
            "Keine Buchungen"
            "Für diesen Filter steht noch nichts im Kassenbuch."
            (Some (Button.secondary "Jetzt spiegeln" (fun () -> dispatch StartMirror)))

    | Success entries ->
        Html.div [
            entryListMobile dispatch (if isAllAccounts then Some abbreviationOf else None) entries
            entryListDesktop dispatch isAllAccounts abbreviationOf entries

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

/// Der Chip "Zu prüfen (n)" über der Liste (ADR 0032), in Konto- und Gesamtsicht
/// immer sichtbar (anders als `deletedToggleRow`, die erst ab einem Treffer
/// erscheint): das ist Romans häufigster Handy-Ablauf, kein Randfall. Aktiv gefärbt
/// (Warning/pink, gefüllt), sonst neutral/soft. Antippen kippt `OnlyNeedsReview`
/// (ADR 0020: der Proxy-Aufruf steckt hinter `RunLoadEntriesPage`, nicht hier).
let private needsReviewChip (dispatch: Msg -> unit) (model: Model) =
    Html.button [
        prop.className "self-start"
        prop.onClick (fun _ -> dispatch ToggleOnlyNeedsReview)
        prop.children [
            Badge.view {
                Badge.defaultProps with
                    Text = needsReviewChipLabel model.NeedsReviewCount
                    Variant = Badge.Warning
                    Style = (if model.OnlyNeedsReview then Badge.Filled else Badge.Soft)
            }
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

let view (model: Model) (dispatch: Msg -> unit) =
    let accounts = model.Accounts |> RemoteData.toOption |> Option.map (fun a -> a.Accounts) |> Option.defaultValue []
    let balances = model.Balances |> RemoteData.toOption |> Option.defaultValue []
    let isAllAccounts = model.SelectedFilter = Some AllAccounts

    Primitives.Container.view [
        PageHeader.gradientWithActions
            "Kassenbuch"
            (lastRunSubtitle model.LastMirrorRun)
            [
                Button.primaryLoading
                    (if model.MirrorRunning then "Spiegel läuft…" else "Jetzt spiegeln")
                    model.MirrorRunning
                    (fun () -> dispatch StartMirror)
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

                    Html.div [
                        prop.className "flex-1 min-w-0"
                        prop.children [
                            Primitives.Stack.md [
                                if model.MirrorRunning then
                                    // Der Lauf blockiert die Liste nicht: sie bleibt
                                    // stehen, der Hinweis steht daneben (ADR 0014).
                                    Loading.inlineWithText "Spiegel läuft — die Liste bleibt so lange stehen."

                                needsReviewChip dispatch model
                                deletedToggleRow dispatch model
                                entryList dispatch accounts isAllAccounts model
                            ]
                        ]
                    ]
                ]
            ]

        historySheet model dispatch
    ]
