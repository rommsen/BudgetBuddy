module LedgerViewTests

// Reine Bausteine der Kassenbuch-Seite (ledger-b1xwe): Konto-Kuerzel, Datums-Gruppierung
// und die Kontostaende-Karten. Getestet wie in `SyncFlowViewTests.fs`: die reine Logik
// direkt, ohne DOM/React zu rendern — das ist unter `dotnet test` ohnehin nicht moeglich
// (Fable-only), die reine Logik aber schon.

open System
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open Expecto
open Shared.Domain
open Components.Ledger.Types
open Components.Ledger.View

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private accountId (n: int) = Guid.Parse $"0195cc00-0000-7000-8000-0000000000{n:D2}"

let private entryView (accId: Guid) (date: DateTime) (payee: string) : LedgerEntryView = {
    Id = Guid.NewGuid()
    AccountId = accId
    Date = date
    Amount = eur -10m
    Payee = Some payee
    Memo = None
    CategoryName = None
    CategoryGroupName = None
    Splits = []
    Origin = MirroredFromYnab
    DeletedInYnab = false
    NeedsReview = false
    Flag = NoFlag
}

let private splitLine (amount: decimal) : TransactionSplit = {
    Target = ToCategory(YnabCategoryId(Guid.NewGuid()), "Egal")
    Amount = eur amount
    Memo = None
}

let private historyLineAt (sequence: int) : LedgerHistoryLine = {
    Sequence = sequence
    OccurredAt = DateTime(2026, 3, 1).AddDays(float sequence)
    Origin = MirroredFromYnab
    Description = $"Ereignis {sequence}"
}

let private accountView (n: int) (name: string) : LedgerAccountView = {
    Id = accountId n
    Name = name
    Kind = LedgerBankAccount
    YnabAccountId = None
    Closed = false
}

let private abbreviationTests =
    testList "accountAbbreviation" [

        test "Nimmt das erste Wort, bis zu vier Zeichen, gross geschrieben" {
            Expect.equal (accountAbbreviation "ING Extra") "ING" "Kurzes erstes Wort bleibt ganz"
            Expect.equal (accountAbbreviation "Girokonto Comdirect") "GIRO" "Langes erstes Wort wird auf vier Zeichen gekappt"
            Expect.equal (accountAbbreviation "Bar") "BAR" "Einzelnes Wort"
        }

        test "Leerer oder nur-Leerzeichen-Name ergibt ein Platzhalter-Kuerzel, keinen Crash" {
            Expect.equal (accountAbbreviation "   ") "?" "Kein IndexOutOfRange auf leerem Namen"
            Expect.equal (accountAbbreviation "") "?" "Auch der ganz leere String"
        }
    ]

let private groupByDateTests =
    testList "groupByDate" [

        test "Aufeinanderfolgende Buchungen desselben Datums werden zu einer Gruppe" {
            let d1 = DateTime(2026, 3, 10)
            let d2 = DateTime(2026, 3, 9)
            let entries =
                [ entryView (accountId 1) d1 "Rewe"
                  entryView (accountId 1) d1 "Edeka"
                  entryView (accountId 1) d2 "Aldi" ]

            let groups = groupByDate entries
            Expect.equal (groups |> List.map fst) [ d1; d2 ] "Zwei Gruppen, in Listenreihenfolge"
            Expect.equal (groups |> List.item 0 |> snd |> List.length) 2 "Erste Gruppe traegt beide Maerz-10-Buchungen"
            Expect.equal
                (groups |> List.item 0 |> snd |> List.map (fun e -> e.Payee))
                [ Some "Rewe"; Some "Edeka" ]
                "Reihenfolge innerhalb der Gruppe bleibt erhalten"
        }

        test "Dasselbe Datum an nicht benachbarter Stelle bildet zwei getrennte Gruppen" {
            // Bewusst: die Liste ist server-sortiert (neueste zuerst), ein
            // globales groupBy wuerde diese Sortierung verlassen.
            let d = DateTime(2026, 3, 10)
            let other = DateTime(2026, 3, 9)
            let entries = [ entryView (accountId 1) d "A"; entryView (accountId 1) other "B"; entryView (accountId 1) d "C" ]
            let groups = groupByDate entries
            Expect.equal (List.length groups) 3 "Drei Gruppen, nicht zwei: das zweite d-10 ist nicht benachbart"
        }

        test "Leere Liste ergibt keine Gruppen" {
            Expect.equal (groupByDate []) [] "Nichts zu gruppieren"
        }
    ]

let private formatEntryDateTests =
    testList "formatEntryDate" [

        test "Dasselbe Datum wie heute ergibt 'Heute'" {
            let today = DateTime(2026, 9, 12)
            Expect.equal (formatEntryDate today today) "Heute" "Kalendertag-Vergleich, nicht Uhrzeit"
        }

        test "Heute mit einer Uhrzeit gt Null zaehlt trotzdem als 'Heute'" {
            // Regression: ein reiner Zeitstempel-Vergleich (statt .Date) wuerde hier
            // faelschlich einen Tagesunterschied sehen.
            let today = DateTime(2026, 9, 12)
            let withTime = DateTime(2026, 9, 12, 23, 59, 0)
            Expect.equal (formatEntryDate today withTime) "Heute" "Nur das Kalenderdatum zaehlt"
        }

        test "Ein Tag vor heute ergibt 'Gestern'" {
            let today = DateTime(2026, 9, 12)
            let yesterday = DateTime(2026, 9, 11)
            Expect.equal (formatEntryDate today yesterday) "Gestern" "Ein Tag Differenz"
        }

        test "Aeltere Daten stehen als TT.MM. ohne Jahr" {
            let today = DateTime(2026, 9, 12)
            let older = DateTime(2026, 9, 8)
            Expect.equal (formatEntryDate today older) "08.09." "Kein Jahr in der Liste"
        }

        test "Zukuenftige Daten sind ebenfalls TT.MM., kein 'Heute'/'Gestern'" {
            let today = DateTime(2026, 9, 12)
            let future = DateTime(2026, 9, 13)
            Expect.equal (formatEntryDate today future) "13.09." "Nur -1 und 0 Tage sind Sonderfaelle"
        }

        test "Jahreswechsel: 'Gestern' bleibt korrekt ueber den 1. Januar hinweg" {
            let today = DateTime(2027, 1, 1)
            let yesterday = DateTime(2026, 12, 31)
            Expect.equal (formatEntryDate today yesterday) "Gestern" "DateTime.Date-Differenz, kein Tag-des-Jahres-Vergleich"
        }
    ]

let private categoryLabelTests =
    testList "categoryLabel (ledger-w9skm, ledger-h6ptn)" [

        test "Eine Buchung mit Kategorie zeigt den Kategorienamen" {
            let entry = { entryView (accountId 1) (DateTime(2026, 9, 12)) "REWE" with CategoryName = Some "Lebensmittel" }
            Expect.equal (categoryLabel entry) "Lebensmittel" "Kategoriename direkt"
        }

        test "Eine aufgeteilte Buchung ohne eigene Kategorie zeigt die Anzahl der Split-Zeilen" {
            let entry =
                { entryView (accountId 1) (DateTime(2026, 9, 12)) "REWE" with
                    CategoryName = None
                    Splits = [ splitLine -5m; splitLine -3m ] }
            Expect.equal (categoryLabel entry) "2 Split-Zeilen" "Split-Zeilenanzahl statt Kategoriename"
        }

        test "Ohne Kategorie und ohne Splits steht 'ohne Kategorie'" {
            let entry = { entryView (accountId 1) (DateTime(2026, 9, 12)) "Amazon Marketplace" with CategoryName = None }
            Expect.equal (categoryLabel entry) "ohne Kategorie" "Platzhaltertext"
        }
    ]

/// Die PC-Tabelle qualifiziert die Kategorie voll (Handoff 1.9 Zeile 260,
/// "Alltag: Lebensmittel"), die Handy-Zeile bleibt beim Blattnamen (Handoff 1.1
/// Zeile 104-112). `categoryLabelQualified` ist eine eigene, reine Funktion über
/// derselben Quelle wie `categoryLabel`, kein zweiter Zweig in den Views
/// (ADR 0039, ledger-h6ptn).
let private categoryLabelQualifiedTests =
    testList "categoryLabelQualified (ledger-h6ptn, ADR 0039)" [

        test "Gruppe und Name vorhanden ergibt 'Gruppe: Name'" {
            let entry =
                { entryView (accountId 1) (DateTime(2026, 9, 12)) "REWE" with
                    CategoryName = Some "Lebensmittel"
                    CategoryGroupName = Some "Alltag" }
            Expect.equal (categoryLabelQualified entry) "Alltag: Lebensmittel" "Volle Qualifizierung wie im Handoff"
        }

        test "Gruppe None fällt auf den Blattnamen zurück" {
            let entry =
                { entryView (accountId 1) (DateTime(2026, 9, 12)) "REWE" with
                    CategoryName = Some "Lebensmittel"
                    CategoryGroupName = None }
            Expect.equal (categoryLabelQualified entry) "Lebensmittel" "Keine gespiegelte Kategorie, also nur der Blattname"
        }

        test "Gruppe leer oder nur Leerzeichen fällt auf den Blattnamen zurück, kein führendes ': '" {
            for leereGruppe in [ Some ""; Some "   " ] do
                let entry =
                    { entryView (accountId 1) (DateTime(2026, 9, 12)) "REWE" with
                        CategoryName = Some "Lebensmittel"
                        CategoryGroupName = leereGruppe }
                Expect.equal (categoryLabelQualified entry) "Lebensmittel" "Eine leere Gruppe zählt wie keine Gruppe"
        }

        test "Keine Kategorie, aber Splits zeigt weiterhin die Split-Zeilenanzahl" {
            let entry =
                { entryView (accountId 1) (DateTime(2026, 9, 12)) "REWE" with
                    CategoryName = None
                    CategoryGroupName = None
                    Splits = [ splitLine -5m; splitLine -3m ] }
            Expect.equal (categoryLabelQualified entry) "2 Split-Zeilen" "Keine Kategorie, also nichts zu qualifizieren"
        }

        test "Keine Kategorie und keine Splits zeigt 'ohne Kategorie'" {
            let entry =
                { entryView (accountId 1) (DateTime(2026, 9, 12)) "Amazon Marketplace" with
                    CategoryName = None
                    CategoryGroupName = None }
            Expect.equal (categoryLabelQualified entry) "ohne Kategorie" "Platzhaltertext, wie categoryLabel"
        }

        test "categoryLabel und categoryLabelQualified liefern bei gesetzter Gruppe unterschiedliche Ergebnisse" {
            let entry =
                { entryView (accountId 1) (DateTime(2026, 9, 12)) "REWE" with
                    CategoryName = Some "Lebensmittel"
                    CategoryGroupName = Some "Alltag" }
            Expect.equal (categoryLabel entry) "Lebensmittel" "Handy-Zeile bleibt beim Blattnamen"
            Expect.equal (categoryLabelQualified entry) "Alltag: Lebensmittel" "PC-Tabelle qualifiziert voll"
            Expect.notEqual (categoryLabel entry) (categoryLabelQualified entry)
                "Der Unterschied ist über zwei benannte Funktionen ausgedrückt, nicht über Zweige in den Views"
        }
    ]

/// Regression fuer ledger-w9skm: die Kategorie war vorher in der normalen
/// Textfarbe gesetzt (kein eigener Token), obwohl der Handoff durchgehend
/// `#a855f7` vorschreibt (Screens 1.1 und 1.9). Dieser Test prueft, dass die
/// Handy-Zeile und die PC-Tabelle denselben Farb-Token verwenden und dass
/// dieser Token tatsaechlich auf die Kategorie-Farbrolle zeigt, nicht auf ein
/// Hex-Literal im View-Code.
let private categoryColorTests =
    testList "categoryColorClassName (ledger-w9skm)" [

        test "Zeigt auf die Kategorie-Farbrolle des Tokens-Layers, kein Hex-Literal" {
            Expect.equal categoryColorClassName Client.DesignSystem.Tokens.Colors.neonPurple
                "Derselbe Token wie in styles.css fuer #a855f7 (--color-neon-purple)"
        }
    ]

// ============================================
// Quelltext-Zusicherungen (ledger-n3pqw, Lücken 8 und 9 des QA-Reviews)
//
// Nach dem Muster von `FontFaceRegressionTests.fs` (liest styles.css als Text) und
// `BackendPortGuardTests.fs` (liest Program.fs als Text): `dotnet test` hat keinen
// Fable/React-Laufzeitkontext, kann also `entryRow`/`entryTableRow` nicht rendern.
// Der Quelltext selbst ist hier das ehrliche Prüfobjekt.
// ============================================

let private repoRoot =
    let testDir = __SOURCE_DIRECTORY__
    Directory.GetParent(Directory.GetParent(testDir).FullName).FullName

let private stylesCssPath = Path.Combine(repoRoot, "src", "Client", "styles.css")
let private ledgerViewPath = Path.Combine(repoRoot, "src", "Client", "Components", "Ledger", "View.fs")

let private readStylesCss () = File.ReadAllText(stylesCssPath)
let private readLedgerView () = File.ReadAllText(ledgerViewPath)

/// Leitet aus einem Farb-Token wie `"text-neon-purple"` den Namen der zugehörigen
/// CSS-Variable ab (`"--color-neon-purple"`). Bewusst aus dem Token abgeleitet statt
/// im Test hingeschrieben (Lücke 8): benennt jemand `Tokens.Colors.neonPurple` um,
/// ohne `styles.css` mitzuziehen, wird der Test rot, statt sich selbst zu bestätigen.
let private cssVariableNameFromColorToken (token: string) : string =
    if not (token.StartsWith "text-") then
        failwithf "Erwartetes Präfix 'text-' fehlt im Farb-Token '%s' (Tokens.fs umgebaut?)" token
    let suffix = token.Substring("text-".Length)
    $"--color-{suffix}"

/// Zählt, wie oft `needle` in `haystack` vorkommt (nicht überlappend).
let private countOccurrences (haystack: string) (needle: string) : int =
    let rec go (fromIdx: int) (acc: int) =
        let i = haystack.IndexOf(needle, fromIdx)
        if i = -1 then acc else go (i + needle.Length) (acc + 1)
    go 0 0

/// Schneidet den Rumpf EINER CSS-Regel aus dem Quelltext, ab ihrem Selektor
/// (`"{selector} {"`, exakt) bis zur ersten schließenden Klammer danach. Bewusst
/// naiv (keine verschachtelten Klammern erwartet): reicht für die Regeln in
/// `styles.css`, die diese Datei prüft (`.ledger-account-strip` u. ä.), auch wenn
/// sie selbst innerhalb eines `@media`-Blocks liegen: die äußere Klammer des
/// `@media`-Blocks wird dabei nicht mitgenommen. Ohne diesen Zuschnitt prüften
/// frühere Fassungen dieser Datei nur, ob eine Eigenschaft IRGENDWO in styles.css
/// vorkommt (z. B. "position: sticky;", das steht auch in `.table-sticky-thead`),
/// was schon vor der eigentlichen Änderung wahr war und daher nichts belegte
/// (Verifier-Befund, Iteration 1 von ledger-qns4j).
let private sliceCssRuleBody (css: string) (selector: string) : string =
    let header = $"{selector} {{"
    let startIdx = css.IndexOf(header)
    if startIdx = -1 then
        failwithf "CSS-Selektor '%s' nicht gefunden (umbenannt oder verschoben?)" selector
    let bodyStart = startIdx + header.Length
    let closeIdx = css.IndexOf("}", bodyStart)
    if closeIdx = -1 then
        failwithf "Keine schließende Klammer für '%s' gefunden" selector
    css.Substring(startIdx, closeIdx + 1 - startIdx)

/// Schneidet den Rumpf einer Top-Level-Funktion aus dem Quelltext: von ihrem Kopf
/// (`functionHeader`, z. B. `"let private entryRow"`) bis zum nächsten `let` auf
/// Spalte 0. Findet der Test den Funktionskopf nicht, schlägt er mit einer
/// benannten Meldung fehl statt still einen leeren Bereich zu liefern (Muster:
/// `captureScreenshot`-Zusicherung in `scripts/screenshot-route.test.mjs`).
let private sliceFunctionBody (source: string) (functionHeader: string) : string =
    let startIdx = source.IndexOf(functionHeader)
    if startIdx = -1 then
        failwithf "Funktionskopf '%s' nicht gefunden (umbenannt oder verschoben?)" functionHeader
    let searchFrom = startIdx + functionHeader.Length
    let nextLetIdx = source.IndexOf("\nlet ", searchFrom)
    if nextLetIdx = -1 then
        source.Substring(startIdx)
    else
        source.Substring(startIdx, nextLetIdx - startIdx)

/// Regression für ledger-n3pqw, Lücke 8: nichts band `text-neon-purple` an
/// `--color-neon-purple: #a855f7`. Benennt jemand die CSS-Variable um, erzeugt
/// Tailwind die Utility nicht mehr, die Kategorie-Farbe fällt still aus der
/// Ausgabe, und `categoryColorTests` oben bliebe grün (der vergleicht nur mit
/// seiner eigenen Definition).
let private categoryColorCssBindingTests =
    testList "Kategorie-Farbe an --color-neon-purple gebunden (ledger-n3pqw)" [

        test "styles.css deklariert die CSS-Variable, auf die der Token zeigt, mit dem Wert aus dem Entwurf" {
            let css = readStylesCss ()
            let variableName = cssVariableNameFromColorToken Client.DesignSystem.Tokens.Colors.neonPurple

            let m =
                Regex.Match(css, Regex.Escape(variableName) + @"\s*:\s*([^;]+);")

            Expect.isTrue m.Success
                $"styles.css muss eine @theme-Deklaration für {variableName} enthalten, sonst fällt die Tailwind-Utility für die Kategorie-Farbe still aus."
            Expect.equal (m.Groups.[1].Value.Trim()) "#a855f7"
                "Die Kategorie-Farbrolle muss #a855f7 bleiben (Handoff 1.1 und 1.9); eine stille Änderung der Palette fällt hierüber auf."
        }
    ]

/// Regression für ledger-n3pqw, Lücke 9: nichts pinnte, dass Handy-Zeile
/// (`entryRow`) und PC-Tabelle (`entryTableRow`) dieselben geteilten Helfer
/// (`categoryLabel`, `payeeLabel`) benutzen. Vor dem Umbau stand "ohne Payee"
/// zweimal im File (je einmal pro Ansicht); dieser Test war vor der Einführung von
/// `payeeLabel` rot mit dem gemeldeten Fund von 2 statt 1.
let private sharedHelpersAndNoColorLiteralsTests =
    testList "Handy-Zeile und PC-Tabelle teilen ihre Helfer (ledger-n3pqw)" [

        test "Keine Farbliterale in der Kassenbuch-Ansicht: 'text-neon-' kommt nicht vor" {
            let view = readLedgerView ()
            Expect.isFalse (view.Contains "text-neon-")
                "Farbklassen laufen ausschließlich über Tokens.Colors.* (ADR 0031), kein direktes text-neon-* im View-Code."
        }

        test "'ohne Kategorie' steht genau einmal, in categoryLabel" {
            let view = readLedgerView ()
            Expect.equal (countOccurrences view "ohne Kategorie") 1
                "Ein wieder eingebauter abweichender Platzhalter in nur einer der beiden Ansichten würde hier auffallen."
        }

        test "'ohne Payee' steht genau einmal, in payeeLabel" {
            let view = readLedgerView ()
            Expect.equal (countOccurrences view "ohne Payee") 1
                "Vor der Einführung von payeeLabel standen hier zwei Vorkommen (je eines pro Ansicht, entryRow und entryTableRow)."
        }

        test "entryRow und entryTableRow teilen payeeLabel; die Kategorie-Funktion unterscheidet sich bewusst (ADR 0039)" {
            let view = readLedgerView ()
            let entryRowBody = sliceFunctionBody view "let private entryRow"
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"

            Expect.stringContains entryRowBody "payeeLabel entry"
                "entryRow muss payeeLabel entry aufrufen, sonst weicht die Handy-Zeile von der PC-Tabelle ab."
            Expect.stringContains entryTableRowBody "payeeLabel entry"
                "entryTableRow muss payeeLabel entry aufrufen, sonst weicht die PC-Tabelle von der Handy-Zeile ab."

            // Seit ledger-h6ptn (ADR 0039) unterscheiden sich die beiden Ansichten
            // bewusst: die Handy-Zeile bleibt beim Blattnamen, die PC-Tabelle
            // qualifiziert voll. Der Unterschied ist über zwei benannte Funktionen
            // ausgedrückt, nicht über einen Zweig in einer gemeinsamen Funktion.
            Expect.stringContains entryRowBody "categoryLabel entry"
                "entryRow muss categoryLabel entry aufrufen (Blattname)."
            Expect.isFalse (entryRowBody.Contains "categoryLabelQualified")
                "entryRow zeigt bewusst nur den Blattnamen, keine Qualifizierung."
            Expect.stringContains entryTableRowBody "categoryLabelQualified entry"
                "entryTableRow muss categoryLabelQualified entry aufrufen (volle Qualifizierung)."
        }

        /// Regression für ledger-qns4j, AC3: die Statusspalte der PC-Tabelle trug
        /// bisher `originBadge` (Herkunft) statt Status, genau der gemeldete
        /// Befund ("Die Statusspalte trägt Herkunft, nicht Status"). Die Handy-Zeile
        /// zeigt die Herkunft weiterhin inline (unverändert), nur die PC-Tabelle
        /// verliert sie aus der Statusspalte; sie bleibt über die Historie
        /// erreichbar (`historyLine` ruft `originBadge` weiterhin auf).
        ///
        /// Das hier ist ein Quelltext-Wächter (schnell, ohne Browser); die von AC3
        /// verlangte Zusicherung "über die gerenderte Zeile, nicht nur durch das
        /// Lesen des Diffs" liefert `scripts/ledger-status-column.test.mjs`: dort
        /// prüft eine echte, im Browser gerenderte Tabellenzeile, dass kein
        /// Herkunfts-Text (z. B. "YNAB-Spiegel") in der Statusspalte auftaucht.
        test "entryRow ruft weiterhin originBadge auf, entryTableRow nicht mehr" {
            let view = readLedgerView ()
            let entryRowBody = sliceFunctionBody view "let private entryRow"
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"

            Expect.stringContains entryRowBody "originBadge entry.Origin"
                "Die Handy-Zeile zeigt die Herkunft weiterhin inline, das ändert diese Aufgabe nicht."
            Expect.isFalse (entryTableRowBody.Contains "originBadge entry.Origin")
                "Die PC-Statusspalte darf keinen originBadge mehr rendern (AC3, ledger-qns4j): Status statt Herkunft."
        }

        test "Beide Ansichten speisen die Kategorie-Farbrolle aus demselben Wert (categoryColorClassName)" {
            let view = readLedgerView ()
            let entryRowBody = sliceFunctionBody view "let private entryRow"
            let tdTruncatedCategoryBody = sliceFunctionBody view "let private tdTruncatedCategory"
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"

            Expect.stringContains entryRowBody "categoryColorClassName"
                "Die Handy-Zeile muss die Kategorie-Farbrolle über categoryColorClassName einfärben."
            Expect.stringContains tdTruncatedCategoryBody "categoryColorClassName"
                "Die PC-Tabellenzelle muss dieselbe Kategorie-Farbrolle über categoryColorClassName einfärben."
            Expect.stringContains entryTableRowBody "tdTruncatedCategory (categoryLabelQualified entry)"
                "Die PC-Tabelle muss ihre Kategoriezelle über tdTruncatedCategory rendern, sonst verliert sie die Farbe (fällt zurück auf tdTruncated)."
        }
    ]

/// PC-Statusspalte (AC4/AC5/AC6, ledger-qns4j, Entscheidung Roman 2026-09-19): nur
/// noch Status statt Herkunft, Quelle bleibt `entry.NeedsReview` (kein neuer
/// Zustand), Spaltenbreite zurück auf den Handoff-Wert 128px, zwei Kennzeichen
/// nebeneinander ohne Umbruch. `dotnet test` hat keinen Fable/React-Laufzeitkontext
/// (siehe Modulkopf), die Zusicherungen laufen deshalb wie oben über den
/// Funktionsrumpf im Quelltext.
let private statusColumnTests =
    testList "PC-Statusspalte zeigt Status statt Herkunft (ledger-qns4j)" [

        test "statusColumnWidth ist auf den Handoff-Wert 128 zurückgesetzt (AC6)" {
            // `statusColumnWidth` ist `private`, deshalb über den Quelltext geprüft
            // wie die übrigen Rumpf-Zusicherungen in dieser Datei.
            let view = readLedgerView ()
            Expect.stringContains view "let private statusColumnWidth = \"128\""
                "Screen 1.9 sieht 128px vor; die 208px waren ein Pflaster von ledger-x0yga."
        }

        /// Quelltext-Wächter (schnell, ohne Browser). Beweist für sich allein noch
        /// nicht, dass die 80px tatsächlich bei Payee/Memo landen. Payee/Memo
        /// waren schon vor dieser Aufgabe ohne `Table.thWidth` (sie hatten die
        /// 1fr-Aufteilung bereits seit `ledger-x0yga`). Die tatsächliche
        /// Spaltenraster-Messung ("Belegt am Spaltenraster, nicht nur am
        /// geänderten Literal") liefert `scripts/ledger-status-column.test.mjs`:
        /// eine echte, `table-layout: fixed` gerenderte Tabelle, an der die
        /// gerenderten Breiten von Datum/Kategorie/Betrag/Status UND die Summe aus
        /// Payee+Memo gemessen werden.
        test "Payee und Memo bleiben ohne eigene Spaltenbreite (AC6, Quelltext-Wächter)" {
            let view = readLedgerView ()
            let desktopBody = sliceFunctionBody view "let private entryListDesktop"
            Expect.stringContains desktopBody "Table.th \"Payee\""
                "Payee darf keine eigene Table.thWidth tragen, sonst blieben die freigewordenen 80px ungenutzt."
            Expect.stringContains desktopBody "Table.th \"Memo\""
                "Memo darf keine eigene Table.thWidth tragen, aus demselben Grund."
            Expect.isFalse (desktopBody.Contains "Table.thWidth \"Payee\"")
                "Payee mit fester Breite würde die 1fr-Aufteilung mit Memo verhindern."
            Expect.isFalse (desktopBody.Contains "Table.thWidth \"Memo\"")
                "Memo mit fester Breite würde die 1fr-Aufteilung mit Payee verhindern."
        }

        test "needsReviewStatusBadge zeigt den Text 'Zu prüfen' als Warning-Badge (ADR 0032, AC4)" {
            let view = readLedgerView ()
            let badgeBody = sliceFunctionBody view "let private needsReviewStatusBadge"
            Expect.stringContains badgeBody "Text = \"Zu prüfen\""
                "Der Wortlaut aus dem Handoff und der Chip-Beschriftung (needsReviewChipLabel)."
            Expect.stringContains badgeBody "Variant = Badge.Warning"
                "Dieselbe Farbsemantik wie der Chip und der Punkt am Handy (Pink/Warning)."
        }

        test "entryTableRow rendert needsReviewStatusBadge nur bei entry.NeedsReview, kein neuer Zustand (AC4)" {
            let view = readLedgerView ()
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"
            Expect.stringContains entryTableRowBody "if entry.NeedsReview then needsReviewStatusBadge ()"
                "Die Quelle bleibt entry.NeedsReview (ADR 0032); eine geprüfte Buchung trägt kein Badge."
        }

        test "entryTableRow rendert deletedStatusIcon nur bei entry.DeletedInYnab" {
            let view = readLedgerView ()
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"
            Expect.stringContains entryTableRowBody "if entry.DeletedInYnab then deletedStatusIcon ()"
                "Das Ausnahme-Kennzeichen bleibt an entry.DeletedInYnab gebunden, wie zuvor an deletedBadge()."
        }

        /// Regression für AC5: zwei Kennzeichen nebeneinander dürfen die Spalte
        /// nicht in die Höhe treiben. `flex-nowrap` ist die notwendige (aber nicht
        /// hinreichende) Bedingung dafür, dass zwei gleichzeitig gesetzte
        /// Kennzeichen nicht umbrechen: sie stand schon vor dieser Aufgabe hier
        /// (ledger-x0yga, das ursprüngliche Pflaster) und beweist allein nichts.
        /// Die tatsächliche Breitenpassung (128px-Spalte, zwei echte Kennzeichen
        /// nebeneinander, unveränderte Zeilenhöhe) misst
        /// `scripts/ledger-status-column.test.mjs` gegen die echte, im Browser
        /// gerenderte Tabelle (Vite-Entwicklungsserver, echtes Tailwind-CSS):
        /// `dotnet test` hat keinen Browser und keine Pixelbreiten.
        test "Status-Kennzeichen der PC-Tabelle bleiben ohne Zeilenumbruch nebeneinander (AC5)" {
            let view = readLedgerView ()
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"
            Expect.stringContains entryTableRowBody "flex flex-nowrap items-center"
                "flex-wrap würde zwei Kennzeichen in zwei Zeilen umbrechen und die Zeile höher machen als jede andere."
        }

        /// Regression für den Verifier-Fund der Iteration 1: der volle Wortlaut aus
        /// `flagBadge` ("Rückfrage Lena") passt zusammen mit "Zu prüfen" nicht mehr
        /// in die 128px-Spalte, genau der Rückfall auf das in `What`, Punkt 3,
        /// benannte Pflaster-Verhalten. `entryTableRow` muss deshalb die gekürzte
        /// `flagStatusBadge` verwenden, nicht `flagBadge`; die Handy-Zeile
        /// (`entryRow`) behält den vollen Wortlaut, dort ist keine Spaltenbreite zu
        /// wahren.
        test "entryTableRow verwendet die gekürzte flagStatusBadge, nicht die volltextige flagBadge (AC5)" {
            let view = readLedgerView ()
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"
            Expect.stringContains entryTableRowBody "flagStatusBadge entry.Flag"
                "Die Statusspalte muss die gekürzte Fassung rendern, sonst überläuft sie neben 'Zu prüfen' wieder."
            Expect.isFalse (entryTableRowBody.Contains "flagBadge entry.Flag")
                "flagBadge (voller Wortlaut) darf in der PC-Statusspalte nicht mehr aufgerufen werden."

            let entryRowBody = sliceFunctionBody view "let private entryRow"
            Expect.stringContains entryRowBody "flagBadge entry.Flag"
                "Die Handy-Zeile behält den vollen Wortlaut über flagBadge, dort ist keine Spaltenbreite zu wahren."
        }

        /// `flagStatusBadge` selbst: gekürzter Wortlaut für *Rückfrage Lena*
        /// ("Lena?" statt "Rückfrage Lena"), mit der vollen Bedeutung als
        /// title/aria-label nachgetragen (Verifier-Befund Iteration 2); `NoFlag`
        /// zeigt nichts (wie `flagBadge`).
        test "flagStatusBadge kürzt Rückfrage Lena mit voller Bedeutung für assistive Technologie, NoFlag bleibt unverändert" {
            let view = readLedgerView ()
            let body = sliceFunctionBody view "let private flagStatusBadge"
            Expect.stringContains body "Text = \"Lena?\""
                "Der volle Wortlaut 'Rückfrage Lena' passt neben 'Zu prüfen' nicht mehr in 128px."
            Expect.stringContains body "withAccessibleName \"Rückfrage Lena\""
                "Der gekürzte Wortlaut 'Lena?' muss die volle Bedeutung als title/aria-label nachtragen."
            Expect.stringContains body "| NoFlag -> Html.none"
                "Kein Flag zeigt kein Kennzeichen, wie bei flagBadge."
        }

        /// Regression Iteration 3: der Verifier von Iteration 2 hat vorgerechnet,
        /// dass "Zu prüfen" + der Text "Markiert" die 128px-Statuszelle noch um 3px
        /// waagerecht überläuft (scrollWidth 131 > clientWidth 128). *Markiert*
        /// wird deshalb, wie schon `deletedStatusIcon`, zu einem reinen Symbol statt
        /// Text: die primäre Coverage dafür ist der Browser-Test in
        /// `scripts/ledger-status-column.test.mjs` (AC5), dieser Test ist nur der
        /// Quelltext-Wächter dazu.
        test "flagStatusBadge zeigt Markiert als Symbol statt Text, mit voller Bedeutung für assistive Technologie" {
            let view = readLedgerView ()
            let body = sliceFunctionBody view "let private flagStatusBadge"
            Expect.isFalse (body.Contains "Text = \"Markiert\"")
                "Der volle Text 'Markiert' passt zusammen mit 'Zu prüfen' nicht mehr in 128px (scrollWidth 131 > clientWidth 128)."
            Expect.stringContains body "withAccessibleName \"Markiert\""
                "Ohne Text muss die volle Bedeutung 'Markiert' als title/aria-label nachgetragen werden."
            Expect.stringContains body "Icons.flag"
                "Ein Symbol ersetzt den Text (Entscheidung Roman: 'gekürzt oder durch Symbole ersetzt')."
        }

        /// `withAccessibleName` selbst: setzt `title` UND `aria-label`, nicht nur
        /// eines von beiden (Verifier-Befund Iteration 2: "Hover-Hinweis UND für
        /// assistive Technologie").
        test "withAccessibleName trägt title und aria-label nach" {
            let view = readLedgerView ()
            let body = sliceFunctionBody view "let private withAccessibleName"
            Expect.stringContains body "prop.title fullMeaning"
                "Der Hover-Hinweis (title) muss die volle Bedeutung tragen."
            Expect.stringContains body "aria-label"
                "Assistive Technologie braucht ein aria-label mit der vollen Bedeutung."
        }

        /// Regression: `deletedStatusIcon` ersetzt den vollen Text durch ein Symbol
        /// (Entscheidung Roman 2026-09-19), damit "Zu prüfen" plus das
        /// Ausnahme-Kennzeichen zusammen in die 128px-Spalte passen. Die
        /// Handy-Zeile behält weiterhin den vollen Text über `deletedBadge`.
        test "deletedStatusIcon zeigt kein Text-Kennzeichen mehr, nur ein Symbol mit voller Bedeutung für assistive Technologie" {
            let view = readLedgerView ()
            let iconBadgeBody = sliceFunctionBody view "let private deletedStatusIcon"
            Expect.stringContains iconBadgeBody "Text = \"\""
                "Der volle Text 'in YNAB gelöscht' passt neben 'Zu prüfen' nicht mehr in 128px."
            Expect.stringContains iconBadgeBody "Icon = Some"
                "Ein Symbol ersetzt den Text (Entscheidung Roman: 'gekürzt oder durch Symbole ersetzt')."
            Expect.stringContains iconBadgeBody "withAccessibleName \"in YNAB gelöscht\""
                "Ohne Text muss die volle Bedeutung 'in YNAB gelöscht' als title/aria-label nachgetragen werden."

            let entryRowBody = sliceFunctionBody view "let private entryRow"
            Expect.stringContains entryRowBody "deletedBadge ()"
                "Die Handy-Zeile behält den vollen Text, dort ist keine Spaltenbreite zu wahren."
        }
    ]

/// Konten-Spalte am PC bleibt beim senkrechten Scrollen sichtbar (AC1/AC2,
/// ledger-qns4j): sie war nie `sticky`, seit der Tabellenkopf klebt (ledger-x0yga)
/// fiel das auf. `dotnet test` kann das tatsächliche Scrollverhalten nicht
/// prüfen (kein Fable/React/Browser-Kontext); dafür rendert und misst
/// `scripts/ledger-status-column.test.mjs` (echter Chromium, gemockte API-Antworten,
/// über zwanzig Konten) tatsächlich, ob die Spalte beim Scrollen stehen bleibt und
/// eine zu hohe Liste in sich scrollt. Diese Tests hier prüfen nur, dass die
/// CSS-Regel, auf der jene Messung aufsetzt, tatsächlich die erwarteten
/// Eigenschaften trägt, als Quelltext-Wächter, nicht als Ersatz für die Messung
/// (Verifier-Befund, Iteration 1: die vorherige Fassung suchte "position: sticky;"
/// irgendwo in der ganzen Datei, wo es schon vor dieser Aufgabe stand, z. B. in
/// `.table-sticky-thead`, und belegte dadurch nichts).
let private accountStripStickyTests =
    testList "Konten-Spalte haftet beim Scrollen (ledger-qns4j, AC1/AC2)" [

        test "accountStrip trägt die Sticky-Klasse ledger-account-strip" {
            let view = readLedgerView ()
            let accountStripBody = sliceFunctionBody view "let private accountStrip"
            Expect.stringContains accountStripBody "ledger-account-strip"
                "Ohne die Klasse scrollt die Konten-Spalte mit der Seite weg (der gemeldete Befund)."
        }

        test "Die Regel .ledger-account-strip selbst (nicht irgendeine andere Stelle in styles.css) haftet unter der Navbar (AC1)" {
            let css = readStylesCss ()
            let ruleBody = sliceCssRuleBody css ".ledger-account-strip"
            Expect.stringContains ruleBody "position: sticky;"
                "Sticky-Positionierung, direkt in dieser Regel, nicht nur irgendwo in der Datei (z. B. .table-sticky-thead)."
            Expect.stringContains ruleBody "top: 64px;"
                "Direkt unter der 64px hohen fixen Navbar, direkt in dieser Regel."
        }

        test "Die Regel .ledger-account-strip selbst lässt eine zu hohe Kontoliste in sich scrollen (AC2)" {
            let css = readStylesCss ()
            let ruleBody = sliceCssRuleBody css ".ledger-account-strip"
            Expect.stringContains ruleBody "overflow-y: auto;"
                "Ohne eigenes Scrollen würde eine lange Kontoliste den Seiteninhalt abschneiden oder über den Rand laufen."
            Expect.stringContains ruleBody "max-height: calc(100vh - 64px);"
                "Die Höhen-Obergrenze ist die sichtbare Fensterhöhe abzüglich der Navbar, sonst greift overflow-y nie."
        }
    ]

/// Regression fuer ledger-w9skm: die Handy-Zeile war eine Karte (`rounded-lg`,
/// umlaufender `border`), der Entwurf sieht aber eine flache Listenzeile mit nur
/// einer Unterkante vor (Handoff 1.1: `padding:13px 16px`, Unterkante
/// `1px solid #1c1c38`). `dotnet test` kann das gerenderte `Html.button` nicht
/// pruefen (kein Fable/React-Laufzeitkontext, siehe Modulkopf), wohl aber den
/// Klassen-String, den `entryRow` unveraendert als `prop.className` setzt.
let private entryRowClassNameTests =
    testList "entryRowClassName (ledger-w9skm)" [

        test "Kein Eckenradius mehr (vorher rounded-lg, die Karte aus dem Befund)" {
            Expect.isFalse (entryRowClassName.Contains "rounded") "Eine flache Zeile hat keinen Radius"
        }

        test "Kein umlaufender Rahmen mehr, nur eine Unterkante" {
            Expect.isFalse (entryRowClassName.Contains "border ") "border (alle Seiten) war die Karte"
            Expect.stringContains entryRowClassName "border-b" "Unterkante als Trenner"
            Expect.stringContains entryRowClassName Client.DesignSystem.Tokens.Borders.subtle
                "Dieselbe gedaempfte Trennfarbe wie sonst im Design-System"
        }
    ]

let private accountCardsTests =
    testList "buildAccountCards" [

        test "'Alle Konten' steht zuerst und traegt die Summe aller Kontostaende" {
            let accounts = [ accountView 1 "ING"; accountView 2 "Comdirect" ]
            let balances =
                [ { AccountId = accountId 1; Balance = eur 100m }
                  { AccountId = accountId 2; Balance = eur -20m } ]

            let cards = buildAccountCards accounts balances
            Expect.equal (cards |> List.map (fun c -> c.Filter)) [ AllAccounts; OneAccount (accountId 1); OneAccount (accountId 2) ]
                "Alle Konten zuerst, danach die Konten in ihrer Reihenfolge"
            Expect.equal (cards |> List.head).Balance (eur 80m) "100 - 20"
        }

        test "Ein Konto ohne geladenen Stand zeigt Null, keinen Fehler" {
            let accounts = [ accountView 1 "ING" ]
            let cards = buildAccountCards accounts []
            Expect.equal (cards |> List.item 1).Balance (eur 0m) "Kein Stand geladen: Null statt Crash"
            Expect.equal (cards |> List.head).Balance (eur 0m) "Auch 'Alle Konten' ist dann Null"
        }
    ]

/// Entscheidung Roman, 2026-09-18 (`ledger-d4kx8`, Variante 2): eingeblendet zählen
/// gelöschte Buchungen in Trefferzahl UND Summe (Code bleibt, nur der Entwurf wird
/// angepasst). Die Zeile über der Liste sagt das jetzt explizit, im Stil "13
/// Buchungen, davon 1 in YNAB gelöscht". Ausgeblendet bleibt der Satz aus
/// `ledger-p3n8k` unverändert.
let private deletedCountTextTests =
    testList "deletedCountText" [

        test "Ausgeblendet nennt Zahl und Zustand, Singular bei genau einer Buchung" {
            Expect.equal (deletedCountText 0 1 false) "1 gelöschte Buchung ausgeblendet" "Singular"
            Expect.equal (deletedCountText 0 3 false) "3 gelöschte Buchungen ausgeblendet" "Plural"
        }

        test "Eingeblendet nennt die Trefferzahl und wie viele davon gelöscht sind" {
            Expect.equal
                (deletedCountText 13 1 true)
                "13 Buchungen, davon 1 in YNAB gelöscht"
                "Beispiel aus der Entscheidung"
        }

        test "Eingeblendet: Singular bei genau einer Buchung insgesamt" {
            Expect.equal
                (deletedCountText 1 1 true)
                "1 Buchung, davon 1 in YNAB gelöscht"
                "Die Trefferzahl bekommt Singular, nicht die Zahl der gelöschten"
        }
    ]

let private needsReviewChipLabelTests =
    testList "needsReviewChipLabel" [

        test "Zeigt die Zahl in Klammern, auch bei Null (ADR 0032)" {
            Expect.equal (needsReviewChipLabel 0) "Zu prüfen (0)" "Kein Sonderfall für Null"
            Expect.equal (needsReviewChipLabel 7) "Zu prüfen (7)" "Die Zahl aus dem Server-Wert"
        }
    ]

let private historyLinesForDisplayTests =
    testList "historyLinesForDisplay" [

        test "Zeigt die neueste Zeile oben, der Lesepfad liefert aber aufsteigend" {
            // ledger-h9tq2, Entscheidung Roman 2026-09-18: die Drehung sitzt in der
            // Anzeige. Ohne `List.rev` in `historyLinesForDisplay` bliebe die Liste
            // aufsteigend und dieser Test scheiterte: [1; 2; 3] statt [3; 2; 1].
            let ascending = [ historyLineAt 1; historyLineAt 2; historyLineAt 3 ]
            let displayed = historyLinesForDisplay ascending
            Expect.equal
                (displayed |> List.map (fun l -> l.Sequence))
                [ 3; 2; 1 ]
                "Neueste Zeile (höchste Sequenznummer) zuerst"
        }

        test "Die laufende Nummer je Zeile bleibt die aus dem Ereignis-Log, sie wird nicht neu vergeben" {
            let ascending = [ historyLineAt 1; historyLineAt 2; historyLineAt 3 ]
            let displayed = historyLinesForDisplay ascending
            Expect.equal
                (displayed |> List.map (fun l -> l.Sequence))
                (ascending |> List.map (fun l -> l.Sequence) |> List.rev)
                "Dieselben Sequenznummern wie im Log, nur die Reihenfolge dreht sich"
        }

        test "Eine leere Historie bleibt leer" {
            Expect.equal (historyLinesForDisplay []) [] "Nichts zu drehen"
        }
    ]

let private moduleStartupTests =
    testList "Modul-Startup (design-system-bq4tz)" [

        test "Der CLR-Typinitialisierer von Components.Ledger.View wirft keine TypeInitializationException" {
            // Regression: `deletedBadge` war ein parameterloser Top-Level-`let`-Wert, also
            // ein Feld. Ein Feldzugriff löst unter .NET F#s Modul-Startup-Code aus, der
            // ALLE Top-Level-Werte des Moduls initialisiert - hier `Badge.view { Badge.defaultProps
            // with ... }`, was unter echtem .NET (kein Fable/Feliz-Laufzeitkontext) eine
            // InvalidCastException wirft (ein Tuple lässt sich nicht auf Feliz.IReactProperty
            // casten). `deletedBadge` ist privat, deshalb erzwingt dieser Test den
            // Typinitialisierer direkt per Reflection (RunClassConstructor) statt ihn
            // über einen öffentlichen Aufruf indirekt zu triggern. Das bleibt auch nach dem
            // Fix ein sinnvoller Wächter: er scheitert wieder, sollte je ein neuer eager
            // ausgewerteter Top-Level-Wert in dieses Modul einziehen, unabhängig davon, ob es
            // wieder `deletedBadge` heißt oder anders.
            let startupType =
                Assembly.GetAssembly(typeof<Model>).GetType("<StartupCode$Client>.$Components.Ledger.View")
            RuntimeHelpers.RunClassConstructor(startupType.TypeHandle)
            Expect.isTrue true "Der Modul-Start lief durch, ohne dass ein Feld eager eine Feliz-Komponente gerendert hat"
        }
    ]

[<Tests>]
let ledgerViewTests =
    testList
        "Kassenbuch-Seite: View-Bausteine"
        [ abbreviationTests
          groupByDateTests
          formatEntryDateTests
          categoryLabelTests
          categoryLabelQualifiedTests
          categoryColorTests
          categoryColorCssBindingTests
          sharedHelpersAndNoColorLiteralsTests
          statusColumnTests
          accountStripStickyTests
          entryRowClassNameTests
          accountCardsTests
          deletedCountTextTests
          needsReviewChipLabelTests
          historyLinesForDisplayTests
          moduleStartupTests ]
