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
    testList "categoryLabel (ledger-w9skm)" [

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
/// (`categoryLabel`, `payeeLabel`, `originBadge`) benutzen. Vor dem Umbau stand
/// "ohne Payee" zweimal im File (je einmal pro Ansicht); dieser Test war vor der
/// Einführung von `payeeLabel` rot mit dem gemeldeten Fund von 2 statt 1.
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

        test "entryRow und entryTableRow rufen beide categoryLabel, payeeLabel und originBadge auf" {
            let view = readLedgerView ()
            let entryRowBody = sliceFunctionBody view "let private entryRow"
            let entryTableRowBody = sliceFunctionBody view "let private entryTableRow"

            for helperCall in [ "categoryLabel entry"; "payeeLabel entry"; "originBadge entry.Origin" ] do
                Expect.stringContains entryRowBody helperCall
                    $"entryRow muss {helperCall} aufrufen, sonst weicht die Handy-Zeile von der PC-Tabelle ab."
                Expect.stringContains entryTableRowBody helperCall
                    $"entryTableRow muss {helperCall} aufrufen, sonst weicht die PC-Tabelle von der Handy-Zeile ab."
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
            Expect.stringContains entryTableRowBody "tdTruncatedCategory (categoryLabel entry)"
                "Die PC-Tabelle muss ihre Kategoriezelle über tdTruncatedCategory rendern, sonst verliert sie die Farbe (fällt zurück auf tdTruncated)."
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
          categoryColorTests
          categoryColorCssBindingTests
          sharedHelpersAndNoColorLiteralsTests
          entryRowClassNameTests
          accountCardsTests
          deletedCountTextTests
          needsReviewChipLabelTests
          historyLinesForDisplayTests
          moduleStartupTests ]
