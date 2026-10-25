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
open Types
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
    CategoryId = None
    CategoryGroupName = None
    SplitCategoryGroupNames = Map.empty
    Splits = []
    Origin = MirroredFromYnab
    DeletedInYnab = false
    NeedsReview = false
    Flag = NoFlag
    IsTransfer = false
    ReviewMarkWriteBackFailure = None
    CategorizationWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
    SplitLocks = None
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
let private qualifyTests =
    testList "qualify (ledger-b7qmx, ADR 0045)" [
        test "bekannte, nicht leere Gruppe ergibt Gruppe: Blatt" {
            Expect.equal (qualify (Some "Alltag") "Lebensmittel") "Alltag: Lebensmittel" "qualifiziert"
            Expect.equal (qualify (Some "  Alltag ") "Lebensmittel") "Alltag: Lebensmittel" "Gruppe wird getrimmt"
        }
        test "ohne Gruppe oder mit leerer Gruppe bleibt der Blattname" {
            Expect.equal (qualify None "Lebensmittel") "Lebensmittel" "keine Gruppe"
            Expect.equal (qualify (Some "") "Lebensmittel") "Lebensmittel" "leere Gruppe"
            Expect.equal (qualify (Some "   ") "Lebensmittel") "Lebensmittel" "Gruppe nur Leerzeichen"
        }
        test "ein leerer Blattname wird nie qualifiziert" {
            Expect.equal (qualify (Some "Alltag") "") "" "kein Gruppe: ohne Blatt"
            Expect.equal (qualify (Some "Alltag") "  ") "  " "auch nach Trim leer"
        }
    ]

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

/// ADR 0049 Punkt 4 (`ledger-n4vte`): die Kategorie steht in der sekundären Rolle,
/// nicht mehr violett (vorher `categoryColorClassName`, `ledger-w9skm`). Nur "ohne
/// Kategorie" bleibt Orange; ein Übertrag (ADR 0048) trägt ein Ziel und bleibt
/// neutral. Handy-Zeile und PC-Tabelle teilen `categoryClassName`.
let private categoryColorTests =
    testList "categoryClassName (ledger-n4vte, ADR 0049)" [

        test "Eine Buchung mit Kategorie nutzt die sekundäre Rolle, nicht Violett" {
            let entry = { entryView (accountId 1) (DateTime(2026, 3, 10)) "Rewe" with CategoryName = Some "Essen" }
            Expect.equal (categoryClassName entry) Client.DesignSystem.Tokens.ListRoles.secondary
                "Kategorie in text-secondary, 12px"
            Expect.isFalse ((categoryClassName entry).Contains Client.DesignSystem.Tokens.Colors.neonPurple)
                "Kein Violett mehr in der Liste"
        }

        test "Ohne Kategorie bleibt Orange" {
            let entry = entryView (accountId 1) (DateTime(2026, 3, 10)) "Amazon"
            Expect.isTrue (isWithoutCategory entry) "Weder Kategorie noch Split noch Übertrag"
            Expect.stringContains (categoryClassName entry) Client.DesignSystem.Tokens.Colors.neonOrange
                "Ein offener Fall bleibt Orange"
        }

        test "Ein Übertrag ist nicht 'ohne Kategorie' (ADR 0048), in Label und Farbe" {
            let entry = { entryView (accountId 1) (DateTime(2026, 3, 10)) "Transfer : Tagesgeld ING" with IsTransfer = true }
            Expect.isFalse (isWithoutCategory entry) "Ein Übertrag trägt ein Ziel"
            Expect.notEqual (categoryLabel entry) "ohne Kategorie" "Handy-Label"
            Expect.notEqual (categoryLabelQualified entry) "ohne Kategorie" "PC-Label"
            Expect.equal (categoryClassName entry) Client.DesignSystem.Tokens.ListRoles.secondary
                "Neutral statt Orange"
        }

        test "Eine Split-Buchung ist nicht 'ohne Kategorie'" {
            let entry = { entryView (accountId 1) (DateTime(2026, 3, 10)) "Rewe" with Splits = [ splitLine -5m; splitLine -5m ] }
            Expect.isFalse (isWithoutCategory entry) "Die Split-Zeilen sind das Ziel"
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

/// Entfernt jede Zeile, deren getrimmter Anfang `//` ist (deckt `///`-Doc-Kommentare
/// und `//`-Zeilenkommentare ab; Blockkommentare `(* *)` kommen in `View.fs` nicht vor
/// und werden nicht behandelt). Grenzt eine Platzhalter-Zählung auf Code ein: gemeint
/// ist "der Platzhalter lebt an genau einer Stelle im Modul", eine Zählung über den
/// ganzen Dateitext misst aber zusätzlich die Prosa (ledger-nnn47, Fund aus
/// `ledger-n3pqw` Iteration 1). Eine Zeile, deren getrimmter Anfang kein `//` ist,
/// zählt weiterhin mit, auch wenn sie selbst einen `//`-Kommentar am Ende trägt.
let private withoutCommentLines (source: string) : string =
    source.Split('\n')
    |> Array.filter (fun line -> not (line.TrimStart().StartsWith("//")))
    |> String.concat "\n"

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

/// Regression für ledger-n3pqw, Lücke 8, fortgeführt für `ledger-n4vte`: nichts band
/// die Orange-Rolle von "ohne Kategorie" an `--color-neon-orange`. Benennt jemand die
/// CSS-Variable um, erzeugt Tailwind die Utility nicht mehr, und der offene Fall
/// fiele still in die Grundfarbe zurück.
let private categoryColorCssBindingTests =
    testList "Farbrolle 'ohne Kategorie' an --color-neon-orange gebunden (ledger-n4vte)" [

        test "styles.css deklariert die CSS-Variable, auf die der Token zeigt" {
            let css = readStylesCss ()
            let variableName = cssVariableNameFromColorToken Client.DesignSystem.Tokens.Colors.neonOrange

            let m = Regex.Match(css, Regex.Escape(variableName) + @"\s*:\s*([^;]+);")

            Expect.isTrue m.Success
                $"styles.css muss eine @theme-Deklaration für {variableName} enthalten, sonst fällt die Tailwind-Utility für 'ohne Kategorie' still aus."
        }
    ]

/// `withoutCommentLines` selbst gegen synthetischen Quelltext festgenagelt, damit
/// er nicht nur indirekt über die beiden Zähl-Tests unten geprüft ist.
let private withoutCommentLinesTests =
    testList "withoutCommentLines (ledger-nnn47)" [

        test "Das Literal in einer ///-Zeile und einer //-Zeile fällt weg, in einer Codezeile bleibt es (genau 1)" {
            let source =
                String.concat "\n" [
                    "/// Kommentar nennt \"ohne Payee\""
                    "// Kommentar nennt \"ohne Payee\" ebenfalls"
                    "let x = \"ohne Payee\""
                ]
            Expect.equal (countOccurrences (withoutCommentLines source) "ohne Payee") 1
                "Nur die Codezeile zählt, beide Kommentarzeilen fallen weg"
        }

        test "Dieselbe Quelle ohne die Codezeile ergibt 0" {
            let source =
                String.concat "\n" [
                    "/// Kommentar nennt \"ohne Payee\""
                    "// Kommentar nennt \"ohne Payee\" ebenfalls"
                ]
            Expect.equal (countOccurrences (withoutCommentLines source) "ohne Payee") 0
                "Beide verbliebenen Zeilen sind Kommentare, nichts bleibt übrig"
        }

        test "Ein Trailing-Kommentar hinter Code zählt weiterhin mit, die Zeile beginnt getrimmt nicht mit //" {
            let source = "let x = \"ohne Payee\" // Kommentar dahinter"
            Expect.equal (countOccurrences (withoutCommentLines source) "ohne Payee") 1
                "Die Zeile ist eine Codezeile mit angehängtem Kommentar, sie fällt nicht weg"
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
            let view = readLedgerView () |> withoutCommentLines
            Expect.equal (countOccurrences view "ohne Kategorie") 1
                "Ein wieder eingebauter abweichender Platzhalter in nur einer der beiden Ansichten würde hier auffallen. Kommentarzeilen zählen bewusst nicht mit (ledger-nnn47): gemeint ist der Code, nicht die Prosa."
        }

        test "'ohne Payee' steht genau einmal, in payeeLabel" {
            let view = readLedgerView () |> withoutCommentLines
            Expect.equal (countOccurrences view "ohne Payee") 1
                "Vor der Einführung von payeeLabel standen hier zwei Vorkommen (je eines pro Ansicht, entryRow und entryTableRow). Kommentarzeilen zählen bewusst nicht mit (ledger-nnn47)."
        }

        test "entryRow und entryTableRow teilen payeeLabel; die Kategorie-Funktion unterscheidet sich bewusst (ADR 0039)" {
            let view = readLedgerView ()
            let entryRowBody = sliceFunctionBody view "let private entryRow"
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"

            Expect.stringContains entryRowBody "payeeLabel entry"
                "entryRow muss payeeLabel entry aufrufen, sonst weicht die Handy-Zeile von der PC-Tabelle ab."
            Expect.stringContains entryTableRowBody "payeeCell"
                "entryTableRow muss die Payee-Zelle rendern."
            Expect.stringContains (sliceFunctionBody view "let private payeeCell") "payeeLabel entry"
                "payeeCell muss payeeLabel entry aufrufen, sonst weicht die PC-Tabelle von der Handy-Zeile ab."

            // Seit ledger-h6ptn (ADR 0039) unterscheiden sich die beiden Ansichten
            // bewusst: die Handy-Zeile bleibt beim Blattnamen, die PC-Tabelle
            // qualifiziert voll. Der Unterschied ist über zwei benannte Funktionen
            // ausgedrückt, nicht über einen Zweig in einer gemeinsamen Funktion.
            Expect.stringContains entryRowBody "categoryLabel entry"
                "entryRow muss categoryLabel entry aufrufen (Blattname)."
            Expect.isFalse (entryRowBody.Contains "categoryLabelQualified")
                "entryRow zeigt bewusst nur den Blattnamen, keine Qualifizierung."
            Expect.stringContains (sliceFunctionBody view "let private categoryCell") "categoryLabelQualified entry"
                "Die Kategoriezelle der PC-Tabelle muss categoryLabelQualified entry aufrufen (volle Qualifizierung)."
        }

        /// Regression für ledger-qns4j, AC3: die Statusspalte der PC-Tabelle trug
        /// bisher `originBadge` (Herkunft) statt Status, genau der gemeldete
        /// Befund ("Die Statusspalte trägt Herkunft, nicht Status"). Die Handy-Zeile
        /// zeigte die Herkunft weiterhin inline; seit `ledger-n4vte` (ADR 0049) tut
        /// sie das nicht mehr. Die Herkunft steht seit ledger-q1hw2 als eigene Zeile im
        /// Detail (`detailContent`, `originLabel entry.Origin`).
        ///
        /// Das hier ist ein Quelltext-Wächter (schnell, ohne Browser); die von AC3
        /// verlangte Zusicherung "über die gerenderte Zeile, nicht nur durch das
        /// Lesen des Diffs" liefert `scripts/ledger-status-column.test.mjs`: dort
        /// prüft eine echte, im Browser gerenderte Tabellenzeile, dass kein
        /// Herkunfts-Text (z. B. "YNAB-Spiegel") in der Statusspalte auftaucht.
        test "Weder entryRow noch entryTableRow rufen originBadge auf, das Detail zeigt die Herkunft" {
            let view = readLedgerView ()
            let entryRowBody = sliceFunctionBody view "let private entryRow"
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"

            Expect.isFalse (entryRowBody.Contains "originBadge")
                "Die Handy-Zeile rendert die Herkunft nicht mehr inline (ledger-n4vte): sie steht in der Historie."
            Expect.stringContains (sliceFunctionBody view "let private detailContent") "originLabel entry.Origin"
                "Die Herkunft bleibt im Detail sichtbar."
            Expect.isFalse (entryTableRowBody.Contains "originBadge entry.Origin")
                "Die PC-Statusspalte darf keinen originBadge mehr rendern (AC3, ledger-qns4j): Status statt Herkunft."
        }

        test "Beide Ansichten speisen die Kategorie-Rolle aus demselben Wert (categoryClassName)" {
            let view = readLedgerView ()
            let entryRowBody = sliceFunctionBody view "let private entryRow"
            let categoryCellBody = sliceFunctionBody view "let private categoryCell"
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"

            Expect.stringContains entryRowBody "categoryClassName entry"
                "Die Handy-Zeile muss die Kategorie-Rolle über categoryClassName setzen."
            Expect.stringContains categoryCellBody "categoryClassName entry"
                "Die PC-Tabellenzelle muss dieselbe Rolle über categoryClassName setzen."
            Expect.stringContains entryTableRowBody "categoryCell entry"
                "Die PC-Tabelle muss ihre Kategoriezelle über categoryCell rendern, sonst verliert sie die Rolle."
            Expect.isFalse (view.Contains "categoryColorClassName")
                "Das violette categoryColorClassName ist weg (ADR 0049)."
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

        /// Die colgroup baut seit ledger-qh9qx auf den sichtbaren Spalten aus
        /// `visibleColumns` (Mindestbreiten statt Prozentanteile); die Kopfzellen der
        /// ruhigen Tabelle (`Table.calmHeaderCell`) tragen keine Breite. Quelltext-
        /// Wächter; die tatsächliche Breite misst `scripts/ledger-status-column.test.mjs`.
        test "colgroup und Köpfe richten sich nach den sichtbaren Spalten (Quelltext-Wächter)" {
            let view = readLedgerView ()
            let desktopBody = sliceFunctionBody view "let private entryListDesktop"
            Expect.stringContains desktopBody "for (_, width) in columns -> col width"
                "Die colgroup baut auf den Breiten aus visibleColumns."
            Expect.stringContains desktopBody "sortHeader \"Payee\" None SortByPayee"
                "Sortierbarer Spaltenkopf der ruhigen Tabelle (ledger-bn913)."
            Expect.stringContains desktopBody "if List.contains MemoColumn visible then Table.calmHeaderCell \"Memo\""
                "Der Memo-Kopf fällt mit der Memo-Spalte weg."
        }

        test "needsReviewStatusBadge zeigt das kleine Wort 'zu prüfen' in Pink, kein Badge mehr (ADR 0049 Punkt 7)" {
            let view = readLedgerView ()
            let badgeBody = sliceFunctionBody view "let private needsReviewStatusBadge"
            Expect.stringContains badgeBody "prop.text \"zu prüfen\""
                "Ein kleines Wort in der Statusspalte statt des Badges."
            Expect.stringContains badgeBody "Tokens.Colors.neonPink"
                "Dieselbe Pink-Rolle wie die Statuskante."
            Expect.isFalse (badgeBody.Contains "Badge.view {")
                "Kein Badge mehr."
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

// ============================================
// Ruhige Listen (ledger-n4vte, ADR 0049)
// ============================================

let private daySumTests =
    testList "daySum und daySumText (ledger-n4vte, ADR 0049 Punkt 8)" [

        let day = DateTime(2026, 10, 18)
        let withAmount (amount: decimal) = { entryView (accountId 1) day "X" with Amount = eur amount }
        let deleted (amount: decimal) = { withAmount amount with DeletedInYnab = true }

        test "Die Tagessumme ist die Summe der Zeilen der Gruppe" {
            let group = [ withAmount -48.12m; withAmount -6.40m ]
            Expect.equal (daySum false group) (eur -54.52m) "Zwei Ausgaben"
        }

        test "Eingang und Ausgabe saldieren sich" {
            let group = [ withAmount 3412.80m; withAmount -37.45m ]
            Expect.equal (daySum false group) (eur 3375.35m) "Gehalt minus Amazon"
        }

        test "Eine ausgeblendete gelöschte Buchung zählt nicht mit (ledger-d4kx8)" {
            let group = [ withAmount -10m; deleted -500m ]
            Expect.equal (daySum false group) (eur -10m) "Gelöschte sind nicht sichtbar, also nicht in der Summe"
        }

        test "Eine eingeblendete gelöschte Buchung zählt mit (ledger-d4kx8)" {
            let group = [ withAmount -10m; deleted -500m ]
            Expect.equal (daySum true group) (eur -510m) "Eingeblendet zählt sie wie in der Summenzeile"
        }

        test "Eine leere Gruppe ergibt null in Euro" {
            Expect.equal (daySum false []) (eur 0m) "Nichts zu summieren"
        }

        test "Der Text trägt für einen Eingang ein Plus, für eine Ausgabe ein Minus und die Währung" {
            let positive = daySumText (eur 3774m)
            let negative = daySumText (eur -54.52m)
            Expect.isTrue (positive.StartsWith "+") "Eingang mit +"
            Expect.isTrue (negative.StartsWith "-") "Ausgabe mit -"
            Expect.stringContains positive "€" "Währung"
        }
    ]

let private mirrorRunSubtitleTests =
    testList "mirrorRunSubtitle (ledger-n4vte)" [

        let today = DateTime(2026, 10, 18, 9, 0, 0, DateTimeKind.Local)
        let ranAt (date: DateTime) = DateTime(date.Year, date.Month, date.Day, 22, 24, 0, DateTimeKind.Local)
        let ok =
            MirrorRunSucceeded { NewEntries = 1; ChangedEntries = 0; DeletedEntries = 0; ServerKnowledge = 1L }

        test "Ein Lauf von heute heißt 'Gespiegelt heute 22:24'" {
            Expect.equal (mirrorRunSubtitle today { RanAt = ranAt today; Outcome = ok }) "Gespiegelt heute 22:24" "Heute"
        }

        test "Ein Lauf von gestern nennt 'gestern'" {
            Expect.equal
                (mirrorRunSubtitle today { RanAt = ranAt (today.AddDays -1.0); Outcome = ok })
                "Gespiegelt gestern 22:24"
                "Gestern"
        }

        test "Ein älterer Lauf nennt das Datum ohne Jahr" {
            Expect.equal
                (mirrorRunSubtitle today { RanAt = ranAt (DateTime(2026, 10, 3)); Outcome = ok })
                "Gespiegelt 03.10. 22:24"
                "Datum"
        }

        test "Ein gescheiterter Lauf steht genauso sichtbar da (ADR 0014)" {
            let text = mirrorRunSubtitle today { RanAt = ranAt today; Outcome = MirrorRunFailed "Token abgelaufen" }
            Expect.stringContains text "gescheitert" "Fehler benannt"
            Expect.stringContains text "Token abgelaufen" "Grund genannt"
        }
    ]

/// Quelltext-Zusicherungen zur Umstellung (`ledger-n4vte`). `dotnet test` kann die
/// Seite nicht rendern (kein Fable/React-Kontext, siehe Modulkopf); der Quelltext ist
/// das Prüfobjekt, wie bei den Wächtern oben. Das gerenderte Ergebnis belegen die
/// Screenshots unter `docs/ux/kassenbuch/screenshots/`.
let private calmListSourceTests =
    testList "Kassenbuch als ruhige Liste (ledger-n4vte, ADR 0049)" [

        test "Die Seite nutzt weder PageHeader.gradientWithActions noch Container.view mit Breitenbegrenzung" {
            let view = readLedgerView () |> withoutCommentLines
            let body = sliceFunctionBody view "let view (model: Model)"
            Expect.isFalse (view.Contains "gradientWithActions") "Kein Gradient-Titel"
            Expect.isFalse (body.Contains "Container.view") "Keine Breitenbegrenzung"
            Expect.stringContains body "Primitives.Container.full" "Container ohne Breitenbegrenzung"
            Expect.stringContains body "PageHeader.calm" "Schlanker Seitenkopf"
        }

        test "Jetzt spiegeln bleibt erreichbar, am Handy als Symbol-Knopf mit Titel" {
            let view = readLedgerView ()
            let body = sliceFunctionBody view "let view (model: Model)"
            Expect.stringContains body "Title = Some \"Jetzt spiegeln\"" "Symbol-Knopf trägt den Namen als Titel"
            Expect.stringContains body "StartMirror" "Löst weiterhin den Lauf aus"
            Expect.isFalse (body.Contains "primaryLoading") "Nicht mehr der große orange Knopf"
        }

        test "Beträge laufen über die Listenvariante, gelöschte bleiben bei transactionDeleted" {
            let view = readLedgerView ()
            let body = sliceFunctionBody view "let private entryMoney"
            Expect.stringContains body "Money.listAmount" "Listenvariante"
            Expect.stringContains body "Money.transactionDeleted" "Gelöschte bleiben gedämpft und durchgestrichen"
            Expect.isFalse ((sliceFunctionBody view "let private entryRow").Contains "Money.transactionWithCurrency")
                "Die rot/grüne Variante ist aus den Zeilen raus"
            Expect.isFalse ((sliceFunctionBody view "let private tdMoney").Contains "Money.transactionWithCurrency")
                "Die rot/grüne Variante ist aus der Tabelle raus"
        }

        test "Der PC-Betrag läuft über Table.amountCell, rechtsbündig mit dem Spaltenkopf" {
            let view = readLedgerView ()
            Expect.stringContains (sliceFunctionBody view "let private tdMoney") "Table.amountCell" "Betragsrolle"
            Expect.stringContains (sliceFunctionBody view "let private entryListDesktop")
                "sortHeader \"Betrag\" (Some \"right\") SortByAmount" "Kopf rechtsbündig"
        }

        test "Zu prüfen ist in beiden Viewports die Statuskante, kein Punkt und kein Rahmen mehr" {
            let view = readLedgerView () |> withoutCommentLines
            let rowBody = sliceFunctionBody view "let private entryRow"
            let tableRowBody = sliceFunctionBody view "let private entryTableRow"
            Expect.stringContains tableRowBody "StatusEdge = entry.NeedsReview" "PC: Statuskante"
            Expect.stringContains rowBody "entryRowReviewClassName" "Handy: Statuskante"
            Expect.stringContains view "$\"{entryRowClassName} {Tokens.ListRoles.statusEdge}\"" "Dieselbe Kante wie die Tabelle"
            Expect.isFalse (view.Contains "Badge.dot") "Der Punkt ist weg"
            Expect.isFalse (view.Contains "border-l-neon-pink") "Der Rahmen links ist weg"
        }

        test "Konto-Kürzel, Flag und Gelöscht-Kennzeichen stehen in Zeile 2 der Handy-Zeile" {
            let view = readLedgerView ()
            let rowBody = sliceFunctionBody view "let private entryRow"
            let secondLine = rowBody.Substring(rowBody.IndexOf "Tokens.ListRoles.mobileLine2")
            Expect.stringContains secondLine "accountBadge" "Konto-Kürzel"
            Expect.stringContains secondLine "flagBadge entry.Flag" "Flag"
            Expect.stringContains secondLine "deletedBadge ()" "Gelöscht-Kennzeichen"
            Expect.stringContains rowBody "Tokens.ListRoles.mobileLine1" "Zeile 1"
        }

        test "Beide Zeilen der Handy-Zeile haben dieselbe feste Höhe und gleichen Abstand in jeder Zeile" {
            let l1 = Client.DesignSystem.Tokens.ListRoles.mobileLine1
            let l2 = Client.DesignSystem.Tokens.ListRoles.mobileLine2
            Expect.stringContains l1 "h-5" "Zeile 1 fest 20px"
            Expect.stringContains l2 "h-5" "Zeile 2 fest 20px, auch mit Badge"
            Expect.stringContains l2 "mt-0.5" "Abstand zwischen den Zeilen"
        }

        test "Beide Viewports zeigen die Tagessumme im Gruppenkopf" {
            let view = readLedgerView ()
            Expect.stringContains (sliceFunctionBody view "let private dateGroupHeader") "daySumText sum" "Handy"
            Expect.stringContains (sliceFunctionBody view "let private dateGroupRow") "daySumText sum" "PC"
            Expect.stringContains (sliceFunctionBody view "let private entryListMobile") "daySum includeDeleted group" "Handy summiert"
            Expect.stringContains (sliceFunctionBody view "let private entryListDesktop") "daySum includeDeleted group" "PC summiert"
        }

        test "Die PC-Tagessumme steht in der Betragsspalte, nicht unter der Statusspalte" {
            let view = readLedgerView ()
            let body = sliceFunctionBody view "let private dateGroupRow"
            Expect.stringContains body "Table.calmGroupRow" "Gemeinsamer Gruppenkopf des Design-Systems"
            Expect.stringContains body "columnsBeforeAmount" "Datum über allen Spalten vor dem Betrag"
        }

        test "Kontostände laufen über die ruhigen Money-Bausteine" {
            let view = readLedgerView ()
            Expect.stringContains (sliceFunctionBody view "let private accountCardView") "Money.accountBalance" "Handy-Karte"
            Expect.stringContains (sliceFunctionBody view "let private accountListRowView") "Money.accountListRow" "PC-Liste"
        }

        test "Negativer Kontostand ist Red, positiver neutral" {
            Expect.stringContains (Client.DesignSystem.Money.accountBalanceClass false -173.28m) Client.DesignSystem.Tokens.Colors.neonRed "negativ"
            Expect.isFalse
                ((Client.DesignSystem.Money.accountBalanceClass false 5040.62m).Contains Client.DesignSystem.Tokens.Colors.neonRed)
                "positiv nicht Red"
        }

        test "Ein positiver Listenbetrag ist Teal, ein negativer text-primary" {
            Expect.stringContains (Client.DesignSystem.Money.listAmountClass 4000m) Client.DesignSystem.Tokens.Colors.neonTeal "Eingang"
            Expect.stringContains (Client.DesignSystem.Money.listAmountClass -48.12m) Client.DesignSystem.Tokens.Colors.textPrimary "Ausgabe"
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

// ============================================
// Filterleiste und Summenzeile: reine Bausteine (ledger-j8xq4)
// ============================================
// Neue testList am Dateiende (Konvention dieses Batches): kein Eingriff in
// bestehende Listen, die ein paralleler Task (ledger-nnn47) in dieser Datei anfasst.

let private chipOrderingTests =
    testList "Chip-Reihenfolge (AC: aktive Chips wandern am Handy nach vorn)" [

        test "Am PC bleibt die feste Reihenfolge, egal welche Chips aktiv sind" {
            let activity = { NeedsReview = true; Category = true; WithoutCategory = false; Date = false; Payee = false; Memo = false; Amount = false }
            Expect.equal (orderChipsForDisplay false activity) fixedChipOrder "PC ändert die Reihenfolge nie"
        }

        test "Ohne aktiven Filter bleibt auch am Handy die feste Reihenfolge" {
            let activity = { NeedsReview = false; Category = false; WithoutCategory = false; Date = false; Payee = false; Memo = false; Amount = false }
            Expect.equal (orderChipsForDisplay true activity) fixedChipOrder "Nichts zu ordnen, wenn nichts aktiv ist"
        }

        test "Am Handy wandert ein aktiver Kategorie-Filter vor das inaktive 'Zu prüfen' (Handoff Screen 2.4)" {
            let activity = { NeedsReview = false; Category = true; WithoutCategory = false; Date = false; Payee = false; Memo = false; Amount = false }
            Expect.equal
                (orderChipsForDisplay true activity)
                [ CategoryChip; NeedsReviewChip; WithoutCategoryChip; DateChip; PayeeChip; MemoChip; AmountChip ]
                "Kategorie ist aktiv, das inaktive 'Zu prüfen' rutscht dahinter"
        }

        test "Zwei aktive Chips (inkl. 'Zu prüfen') behalten ihre bisherige Reihenfolge zueinander" {
            let activity = { NeedsReview = true; Category = true; WithoutCategory = false; Date = false; Payee = false; Memo = false; Amount = false }
            Expect.equal
                (orderChipsForDisplay true activity)
                [ NeedsReviewChip; CategoryChip; WithoutCategoryChip; DateChip; PayeeChip; MemoChip; AmountChip ]
                "Beide aktiv: die stabile Partition ändert ihre gegenseitige Reihenfolge nicht"
        }

        test "Mehrere aktive Chips behalten am Handy ihre bisherige Reihenfolge untereinander" {
            // Handoff 2.8: "Café Lotte" x, "Kategorie: 2" x, dann "Alle Filter löschen":
            // die aktiven Chips selbst bleiben in ihrer festen Reihenfolge zueinander,
            // nur alle inaktiven rutschen gemeinsam dahinter.
            let activity = { NeedsReview = false; Category = true; WithoutCategory = false; Date = false; Payee = true; Memo = false; Amount = false }
            Expect.equal
                (orderChipsForDisplay true activity)
                [ CategoryChip; PayeeChip; NeedsReviewChip; WithoutCategoryChip; DateChip; MemoChip; AmountChip ]
                "Kategorie vor Payee (ihre feste Reihenfolge), beide vor den inaktiven"
        }
    ]

let private quickDateRangeTests =
    testList "Zeitraum-Schnellwahl: Von/Bis (Handoff 2.5: 'Die Schnellwahl füllt Von/Bis mit')" [

        let today = DateTime(2026, 9, 25)

        test "Dieser Monat: vom Monatsersten bis heute" {
            Expect.equal (quickDateRange today ThisMonth) (DateTime(2026, 9, 1), DateTime(2026, 9, 25)) "1.9. bis 25.9."
        }

        test "Letzter Monat: ganzer Vormonat" {
            Expect.equal (quickDateRange today LastMonth) (DateTime(2026, 8, 1), DateTime(2026, 8, 31)) "1.8. bis 31.8."
        }

        test "Letzter Monat über einen Jahreswechsel hinweg" {
            let januar = DateTime(2026, 1, 15)
            Expect.equal (quickDateRange januar LastMonth) (DateTime(2025, 12, 1), DateTime(2025, 12, 31)) "Dezember des Vorjahres"
        }

        test "Dieses Jahr: vom 1. Januar bis heute" {
            Expect.equal (quickDateRange today ThisYear) (DateTime(2026, 1, 1), DateTime(2026, 9, 25)) "1.1. bis 25.9."
        }

        test "Letzte 12 Monate: heute minus 12 Monate bis heute" {
            Expect.equal (quickDateRange today Last12Months) (DateTime(2025, 9, 25), DateTime(2026, 9, 25)) "Rollierendes Jahr"
        }

        test "dateQuickLabel liefert die Chip-Kurzform" {
            Expect.equal (dateQuickLabel Last12Months) "12 Monate" "Handoff-Kurzform für den Chip"
        }
    ]

let private amountFilterTextTests =
    testList "Betrags-Suchfeld: parseAmountFilterText / formatAmountDraft" [

        test "Leerer Text ist kein Filter" {
            Expect.equal (parseAmountFilterText "") None "Leer heißt: kein Wert"
            Expect.equal (parseAmountFilterText "   ") None "Nur Leerzeichen zählt auch als leer"
        }

        test "Punkt und Komma werden beide als Dezimaltrennzeichen gelesen" {
            Expect.equal (parseAmountFilterText "12.50") (Some 12.50m) "Punkt (Invariant Culture)"
            Expect.equal (parseAmountFilterText "12,50") (Some 12.50m) "Komma (Roman tippt deutsch)"
        }

        test "Ein negativer Betrag bleibt negativ" {
            Expect.equal (parseAmountFilterText "-9,80") (Some -9.80m) "Vorzeichen bleibt erhalten"
        }

        test "Unlesbarer Text liefert None statt eine Ausnahme zu werfen" {
            Expect.equal (parseAmountFilterText "abc") None "Kein Absturz bei falscher Eingabe"
        }

        test "formatAmountDraft schreibt mit Punkt (Invariant Culture), die Rückrichtung von parseAmountFilterText" {
            Expect.equal (formatAmountDraft 12.5m) "12.5" "Punkt, kein Komma"
        }
    ]

let private filtersTests =
    testList "Filters: hasAnyFilter und die einzelnen Kriterien" [

        test "noFilters hat keinen aktiven Filter" {
            Expect.isFalse (hasAnyFilter noFilters) "Frisch: nichts aktiv"
        }

        test "Ein gesetzter Kategorie-Filter zählt als aktiv" {
            let filters = { noFilters with CategoryIds = Set.ofList [ Guid.NewGuid() ] }
            Expect.isTrue (hasCategoryFilter filters) "Kategorie ist gesetzt"
            Expect.isTrue (hasAnyFilter filters) "Und zählt zu 'irgendein Filter aktiv'"
        }

        test "Ein gesetzter Zeitraum zählt als aktiv" {
            let filters = { noFilters with DateRange = Some { Quick = Some ThisYear; From = Some DateTime.Today; To = Some DateTime.Today } }
            Expect.isTrue (hasAnyFilter filters) "Zeitraum ist gesetzt"
        }

        test "Payee, Memo und Betrag zählen je einzeln als aktiv" {
            Expect.isTrue (hasAnyFilter { noFilters with Payee = Some "Café Lotte" }) "Payee"
            Expect.isTrue (hasAnyFilter { noFilters with Memo = Some "Kuchen" }) "Memo"
            Expect.isTrue (hasAnyFilter { noFilters with AmountMin = Some 5m }) "Betrag von"
            Expect.isTrue (hasAnyFilter { noFilters with AmountMax = Some 5m }) "Betrag bis"
        }
    ]

// ---------------------------------------------------------------------------
// Filterleiste und Summenzeile nach ADR 0049 (ledger-m23nz)
// ---------------------------------------------------------------------------

let private filterBarModel () : Model = fst (Components.Ledger.State.init ())

let private filteredModel (entries: RemoteData<LedgerEntryView list>) : Model =
    let m = filterBarModel ()
    { m with Filters = { m.Filters with CategoryIds = Set.ofList [ Guid.NewGuid() ] }; Entries = entries }

let private mustContain (source: string) (literal: string) =
    Expect.isTrue (source.Contains literal) $"Ledger/View.fs sollte \"{literal}\" enthalten"

let private mustNotContain (source: string) (literal: string) =
    Expect.isFalse (source.Contains literal) $"Ledger/View.fs sollte \"{literal}\" nicht enthalten"

/// Der Quelltext eines Top-Level-Bindings: von `let [private] name` bis zum nächsten
/// Top-Level-`let`, `type` oder Kommentarblock in Spalte 0.
let private bindingBody (source: string) (name: string) : string =
    let m = Regex.Match(source, $@"(?m)^let (private )?{name}\b")
    Expect.isTrue m.Success $"Binding {name} nicht gefunden"
    let rest = source.Substring m.Index
    let next = Regex.Match(rest.Substring 1, @"(?m)^(let |type |///|//|\[<)")
    if next.Success then rest.Substring(0, next.Index + 1) else rest

let private filterBarRuhigTests =
    testList "Filterleiste und Summenzeile (ledger-m23nz)" [

        test "popoverWidth: Kategorie 460px, Payee 340px, sonst 320px" {
            Expect.equal (popoverWidth CategoryChip) "w-[min(92vw,460px)]" "Kategorie"
            Expect.equal (popoverWidth PayeeChip) "w-[min(92vw,340px)]" "Payee"
            for chip in [ DateChip; MemoChip; AmountChip ] do
                Expect.equal (popoverWidth chip) "w-[min(92vw,320px)]" $"{chip}"
        }

        test "Das Styleguide-Demo des FilterPopover nutzt die Breite der Seite" {
            let demo = File.ReadAllText(Path.Combine(repoRoot, "src", "Client", "Components", "Styleguide", "View.fs"))
            Expect.isTrue (demo.Contains "FilterPopover.categoryWidthClass") "Demo nimmt die DS-Breite"
            Expect.isFalse (demo.Contains "WidthClassName = \"w-[min(92vw,320px)]\"") "kein eigener Wert mehr"
        }

        test "payeeSuggestionsHeading nutzt Tausenderpunkt" {
            Expect.equal (payeeSuggestionsHeading 12116) "Vorschläge aus den 12.116 Treffern" "12.116"
            Expect.equal (payeeSuggestionsHeading 7) "Vorschläge aus den 7 Treffern" "7"
        }

        test "Ledger/View.fs formatiert Zahlen nicht mit string" {
            let src = readLedgerView ()
            mustNotContain src "string model.TotalCount"
            mustNotContain src "string s.Count"
            mustContain src "Money.formatCount s.Count"
        }

        test "Payee-Zeile: Name gekürzt über PickerParts, Zahl in einem shrink-0-Bereich" {
            let body = bindingBody (readLedgerView ()) "payeePickerBody"
            mustContain body "PickerParts.name s.Payee"
            mustContain body "shrink-0"
            Expect.isTrue (Client.DesignSystem.PickerParts.nameClass.Contains "truncate") "nameClass trägt truncate"
        }

        test "memoPickerBody und amountPickerBody nutzen Input.Small, kein Input.textSimple" {
            let src = readLedgerView ()
            for name in [ "memoPickerBody"; "amountPickerBody"; "amountField" ] do
                let body = bindingBody src name
                mustNotContain body "Input.textSimple"
            mustContain (bindingBody src "memoPickerBody") "Input.Small"
            let amount = bindingBody src "amountField"
            mustContain amount "Input.Small"
            mustContain amount "\"€\""
        }

        test "Von und Bis tragen die 12px-Rolle ListRoles.muted" {
            let src = readLedgerView ()
            mustContain (bindingBody src "fieldLabel") "Tokens.ListRoles.muted"
            mustContain (bindingBody src "amountPickerBody") "fieldLabel \"Von\""
            mustContain (bindingBody src "datePickerBody") "dateDraftInput \"Von\""
        }

        test "datePickerBody: Übernehmen ohne Button.primary, mit PickerParts.applyButtonClass" {
            let body = bindingBody (readLedgerView ()) "datePickerBody"
            mustNotContain body "Button.primary"
            mustContain body "PickerParts.applyButtonWhen (dateDraftHasError model)"
        }

        test "Datumsfelder in Input.Small-Maßen (über den DS-Baustein DateField)" {
            let src = readLedgerView ()
            mustContain (bindingBody src "dateDraftInput") "DateField.view"
            mustNotContain (bindingBody src "dateDraftInput") "prop.type'"
            mustNotContain src "prop.type' \"date\""
            mustNotContain (bindingBody src "dateDraftInput") "DateTime.TryParse"
            Expect.equal (Client.DesignSystem.Input.sizeToClass Client.DesignSystem.Input.Small) "text-base md:text-sm px-3 py-2 min-h-[36px]" "gleiche Maße wie Input.Small"
        }

        test "summaryState: ohne Filter Hidden, auch während des Ladens" {
            let m = { filterBarModel () with Entries = Loading }
            Expect.equal (summaryState m) SummaryState.Hidden "ohne Filter"
        }

        test "summaryState: gefiltert und Loading ist Loading" {
            Expect.equal (summaryState (filteredModel Loading)) SummaryState.Loading "Laden"
        }

        test "summaryState: gefiltert und Success ist Ready, auch bei 0 Treffern" {
            Expect.equal (summaryState (filteredModel (Success []))) SummaryState.Ready "0 Treffer"
        }

        test "summaryState: gefiltert und Failure zeigt keine Zeile" {
            Expect.equal (summaryState (filteredModel (Failure "x"))) SummaryState.Hidden "Fehler"
        }

        test "summaryCountText: 0, 1 und 12116" {
            Expect.equal (summaryCountText 0) "0 Buchungen" "0"
            Expect.equal (summaryCountText 1) "1 Buchung" "1"
            Expect.equal (summaryCountText 12116) "12.116 Buchungen" "12.116"
        }

        test "summaryRow: ohne Karte, Betrag über Money.listAmount, Skeleton beim Laden" {
            let body = bindingBody (readLedgerView ()) "summaryRow"
            mustNotContain body "Card.standard"
            mustNotContain body "Money.transactionWithCurrency"
            mustContain body "Money.listAmount"
            mustContain body "Loading.line"
            mustContain body "summaryState model"
        }

        test "Summenzeile: Summe 0 und negative Summe ohne Rot und Grün" {
            for amount in [ 0m; -125.40m; 300m ] do
                let cls = Client.DesignSystem.Money.listAmountClass amount
                Expect.isFalse (cls.Contains "neon-red") $"{amount}: {cls}"
                Expect.isFalse (cls.Contains "neon-green") $"{amount}: {cls}"
        }

        test "Detail-Zeitleiste: kein FontSizes.body, die Schrift folgt den Listenrollen (ADR 0049)" {
            let detail =
                File.ReadAllText(
                    Path.Combine(Path.GetDirectoryName ledgerViewPath, "..", "..", "DesignSystem", "Detail.fs"))
            mustNotContain detail "FontSizes.body"
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
          qualifyTests
          categoryLabelQualifiedTests
          categoryColorTests
          categoryColorCssBindingTests
          withoutCommentLinesTests
          sharedHelpersAndNoColorLiteralsTests
          statusColumnTests
          accountStripStickyTests
          entryRowClassNameTests
          daySumTests
          mirrorRunSubtitleTests
          calmListSourceTests
          accountCardsTests
          deletedCountTextTests
          needsReviewChipLabelTests
          historyLinesForDisplayTests
          moduleStartupTests
          chipOrderingTests
          quickDateRangeTests
          amountFilterTextTests
          filtersTests
          filterBarRuhigTests ]
