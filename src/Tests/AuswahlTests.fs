module AuswahlTests

// design-system-zahv9 (ADR 0049, ADR 0005): ruhige Bausteine für Auswahlen.
// Feliz-Elemente lassen sich unter .NET nicht inspizieren, deshalb liegen die Regeln
// in öffentlichen reinen Funktionen; die übrigen Zusicherungen lesen den Quelltext.

open System.IO
open Expecto
open Client.DesignSystem
open Client.DesignSystem.Tokens

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

let private readDs (file: string) = readRepo [ "src"; "Client"; "DesignSystem"; file ]

let private has (needle: string) (cls: string) = cls.Split(' ') |> Array.contains needle

let private contains (needle: string) (text: string) = text.Contains needle

let private tests =
    testList "Auswahlen (zahv9)" [
        testList "Money.availableClass" [
            for amount, color in [ 125m, Colors.textPrimary; 0m, Colors.textMuted; -3m, "text-neon-red" ] do
                test $"{amount}: genau die Farbe {color}, Mono 500, kein Glow" {
                    let cls = Money.availableClass amount
                    Expect.isTrue (has color cls) cls
                    for needed in [ "font-mono"; "font-medium"; "tabular-nums" ] do
                        Expect.isTrue (has needed cls) $"{needed} fehlt in {cls}"
                    for forbidden in [ "neon-green"; "neon-teal"; "font-semibold"; "font-bold"; "glow"; "shadow" ] do
                        Expect.isFalse (cls.Contains forbidden) $"{forbidden} in {cls}"
                }
        ]

        testList "Picker-Bausteine" [
            test "CategoryMultiSelect und BottomSheet nutzen Zeile und Gruppenkopf, nicht Money.available" {
                for file in [ "CategoryMultiSelect.fs"; "BottomSheet.fs" ] do
                    let src = readDs file
                    Expect.isTrue (contains "PickerParts.row" src) $"{file}: PickerParts.row"
                    Expect.isTrue (contains "PickerParts.groupHeader" src) $"{file}: PickerParts.groupHeader"
                    Expect.isFalse (contains "Money.available" src) $"{file}: Money.available"
            }
            test "Zeilenname: ListRoles.primary, truncate; Betragsbereich schrumpft nicht" {
                Expect.isTrue (contains ListRoles.primary PickerParts.nameClass) PickerParts.nameClass
                Expect.isTrue (has "truncate" PickerParts.nameClass) PickerParts.nameClass
                Expect.isTrue (has "shrink-0" PickerParts.amountSlotClass) PickerParts.amountSlotClass
                Expect.isTrue (has "w-24" PickerParts.amountSlotClass) PickerParts.amountSlotClass
            }
            test "Checkbox-Variante: Label in ListRoles.primary und truncate, nur eine Farbklasse" {
                let cls = Input.checkboxListLabelClass
                Expect.isTrue (contains ListRoles.primary cls) cls
                Expect.isTrue (has "truncate" cls) cls
                let colors = cls.Split(' ') |> Array.filter (fun c -> c.StartsWith "text-text-")
                Expect.equal colors.Length 1 cls
            }
            test "Gruppenkopf beider Picker nutzt ListRoles.groupHeader" {
                Expect.isTrue (contains ListRoles.groupHeader PickerParts.groupHeaderClass) PickerParts.groupHeaderClass
            }
            test "BottomSheet.sectionTitle läuft über den Gruppenkopf" {
                Expect.isTrue (contains "PickerParts.groupHeader title" (readDs "BottomSheet.fs")) "sectionTitle"
            }
            test "Alle Kategorien: nein ohne Vorschläge und Zuletzt verwendet" {
                Expect.isFalse (PickerParts.showAllCategoriesHeader false false false) "ohne"
            }
            test "Alle Kategorien: nein bei aktiver Suche" {
                Expect.isFalse (PickerParts.showAllCategoriesHeader true true true) "Suche"
            }
            test "Alle Kategorien: ja mit Vorschlägen" {
                Expect.isTrue (PickerParts.showAllCategoriesHeader true false false) "Vorschläge"
            }
            test "Alle Kategorien: ja mit Zuletzt verwendet" {
                Expect.isTrue (PickerParts.showAllCategoriesHeader false true false) "Zuletzt"
            }
        ]

        testList "Footer" [
            test "Übernehmen ohne Auswahl ohne Zahl" {
                Expect.equal (PickerParts.applyLabel 0) "Übernehmen" ""
            }
            test "Übernehmen mit drei Auswahlen" {
                Expect.equal (PickerParts.applyLabel 3) "Übernehmen (3)" ""
            }
            test "Knopf: einzeilig, kein Glow" {
                let cls = PickerParts.applyButtonClass
                Expect.isTrue (has "whitespace-nowrap" cls) cls
                Expect.isFalse (cls.Contains "glow") cls
                Expect.isFalse (cls.Contains "shadow") cls
            }
        ]

        testList "Input.Small" [
            test "Small: text-base md:text-sm und 36px Mindesthöhe" {
                let cls = Input.sizeToClass Input.Small
                Expect.isTrue (has "text-base" cls) cls
                Expect.isTrue (has "md:text-sm" cls) cls
                Expect.isTrue (has "min-h-[36px]" cls) cls
            }
            test "Inline-fontSize nur für Medium und Large" {
                // Regression: das Inline-style.fontSize 16 schlug die Tailwind-Größe
                // `md:text-sm`, ein Small-Feld blieb am PC bei 16px.
                Expect.isFalse (Input.usesInlineFontSize Input.Small) "Small"
                Expect.isTrue (Input.usesInlineFontSize Input.Medium) "Medium"
                Expect.isTrue (Input.usesInlineFontSize Input.Large) "Large"
            }
            test "Sheet-Suchfeld: 16px, ab md 14px, höchstens ein Fokusrand" {
                let css = readRepo [ "src"; "Client"; "styles.css" ]
                let start = css.IndexOf ".sheet-search input {"
                let block = css.Substring(start, 1400)
                Expect.isTrue (contains "font-size: 16px" block) "16px"
                Expect.isTrue (contains "@media (min-width: 768px)" block) "md"
                Expect.isTrue (contains "font-size: 14px" block) "14px"
                let focusStart = css.IndexOf ".sheet-search input:focus"
                let focusBlock = css.Substring(focusStart, css.IndexOf("}", focusStart) - focusStart)
                Expect.isFalse (contains "box-shadow: 0" focusBlock) "kein Ring"
                Expect.isTrue (contains "outline: none" focusBlock) "kein Outline-Ring"
            }
        ]

        testList "searchableSelect" [
            let options = [ "a", "Essen: Restaurant"; "b", "Essen: Einkauf"; "c", "Frei"; "d", "Auto: Tanken" ]
            test "gruppiert ohne Präfix, Label ohne Doppelpunkt in Gruppe ohne Kopf" {
                let groups = Input.groupOptions options
                Expect.equal
                    groups
                    [ "Essen", [ "a", "Restaurant"; "b", "Einkauf" ]
                      "", [ "c", "Frei" ]
                      "Auto", [ "d", "Tanken" ] ]
                    "Gruppen"
            }
            test "Suche findet über den Gruppennamen" {
                Expect.equal (Input.filterOptionsBySearch "ESSEN" options |> List.map fst) [ "a"; "b" ] ""
            }
            test "Suche findet über den Namen" {
                Expect.equal (Input.filterOptionsBySearch "tank" options |> List.map fst) [ "d" ] ""
            }
            test "leere Suche lässt alles stehen" {
                Expect.equal (Input.filterOptionsBySearch "  " options) options ""
            }
            test "Optionen: 15px Handy, 14px PC wie die Picker-Zeile" {
                Expect.isTrue (contains ListRoles.primarySize (Input.searchableOptionClass false false)) ""
            }
            test "deutsche Texte, keine englischen" {
                let input = readDs "Input.fs"
                let form = readDs "Form.fs"
                for en in [ "Type to search"; "No matches found" ] do
                    Expect.isFalse (contains en input) en
                Expect.isFalse (contains "Please fill in" input) "Please fill in"
                Expect.isFalse (contains "Please fill in" form) "Please fill in"
                Expect.isTrue (contains "\"Suchen…\"" input) "Suchen…"
                Expect.isTrue (contains "\"Keine Treffer\"" input) "Keine Treffer"
                Expect.isTrue (contains "Bitte ausfüllen:" form) "Bitte ausfüllen:"
            }
            test "Hinweistext fehlender Pflichtfelder" {
                Expect.equal (Form.missingFieldsText [ "Name"; "Muster" ]) "Bitte ausfüllen: Name, Muster" ""
            }
        ]

        testList "Styleguide" [
            test "§7.1: Suchfelder ab 16px unterhalb md" {
                let sg = readRepo [ "standards"; "frontend"; "styleguide.md" ]
                Expect.isTrue (contains "unterhalb `md`" sg) "unterhalb md"
            }
            test "Styleguide beschreibt die Auswahl" {
                let sg = readRepo [ "standards"; "frontend"; "styleguide.md" ]
                for term in [ "Auswahl"; "Gruppenkopf"; "Verfügbar"; "Suchfeld"; "Footer" ] do
                    Expect.isTrue (contains term sg) term
            }
        ]
    ]

// design-system-m7z9q: deutsches Datumsfeld
let private dt (y: int) (m: int) (d: int) = System.DateTime(y, m, d)

let private dateFieldTests =
    testList "Deutsches Datumsfeld (m7z9q)" [
        testList "parseGermanDate" [
            for input, expected in
                [ "07.10.2026", Some (dt 2026 10 7)
                  "7.10.2026", Some (dt 2026 10 7)
                  " 7.10.2026 ", Some (dt 2026 10 7)
                  "29.02.2028", Some (dt 2028 2 29)
                  "31.02.2026", None
                  "29.02.2026", None
                  "31.04.2026", None
                  "2026-10-07", None
                  "07.10.26", None
                  "07.13.2026", None
                  "00.10.2026", None
                  "07.10.2026.1", None
                  "a.b.cccc", None
                  "" , None ] do
                test $"'{input}'" {
                    Expect.equal (DateField.parseGermanDate input) expected input
                }
        ]
        testList "formatGermanDate" [
            test "führende Nullen" {
                Expect.equal (DateField.formatGermanDate (dt 2026 10 7)) "07.10.2026" "Format"
                Expect.equal (DateField.formatGermanDate (dt 2026 1 1)) "01.01.2026" "Format"
            }
            test "Rundlauf: Monatsanfang, Monatsende, Schalttag, Jahreswechsel" {
                for d in [ dt 2026 10 1; dt 2026 10 31; dt 2028 2 29; dt 2026 12 31; dt 2027 1 1 ] do
                    Expect.equal (d |> DateField.formatGermanDate |> DateField.parseGermanDate) (Some d) (string d)
            }
        ]
        testList "Brücke zum nativen Feld" [
            test "germanToIso" {
                Expect.equal (DateField.germanToIso "07.10.2026") (Some "2026-10-07") "gültig"
                Expect.equal (DateField.germanToIso "31.02.2026") None "ungültig"
                Expect.equal (DateField.germanToIso "") None "leer"
            }
            test "isoToGerman" {
                Expect.equal (DateField.isoToGerman "2026-10-07") "07.10.2026" "gültig"
                Expect.equal (DateField.isoToGerman "kein Datum") "kein Datum" "ungültig bleibt"
                Expect.equal (DateField.isoToGerman "2026-02-31") "2026-02-31" "unmöglich bleibt"
                Expect.equal (DateField.isoToGerman "") "" "leer bleibt"
            }
        ]
        testList "classifyDateText" [
            test "leer und Leerzeichen" {
                Expect.equal (DateField.classifyDateText "") DateField.EmptyDate "leer"
                Expect.equal (DateField.classifyDateText "   ") DateField.EmptyDate "Leerzeichen"
            }
            test "gültig und ungültig" {
                Expect.equal (DateField.classifyDateText "7.10.2026") (DateField.ValidDate (dt 2026 10 7)) "gültig"
                Expect.equal (DateField.classifyDateText "31.02.2026") (DateField.InvalidDate "31.02.2026") "ungültig"
            }
        ]
        testList "syncedText (Eingangswert zieht den Text nach)" [
            test "Schnellwahl von außen überschreibt den Text" {
                Expect.equal (DateField.syncedText (Some (dt 2026 10 1)) false "") "01.10.2026" "gefüllt"
                Expect.equal (DateField.syncedText (Some (dt 2026 10 1)) false "5.5.2025") "01.10.2026" "ersetzt"
            }
            test "eigene Schreibweise 7.10.2026 bleibt stehen" {
                Expect.equal (DateField.syncedText (Some (dt 2026 10 7)) false "7.10.2026") "7.10.2026" "bleibt"
            }
            test "Tippschritte bei Fehler bleiben, auch mit altem Eingangswert" {
                Expect.equal (DateField.syncedText (Some (dt 2026 10 1)) true "07.1") "07.1" "bleibt"
                Expect.equal (DateField.syncedText None true "07.1") "07.1" "bleibt"
            }
            test "Zurücksetzen leert Text, auch einen ungültigen ohne Fehlermeldung" {
                Expect.equal (DateField.syncedText None false "07.10.2026") "" "gültiger Text"
                Expect.equal (DateField.syncedText None false "31.02.2026") "" "ungültiger Text"
                Expect.equal (DateField.syncedText None false "") "" "leer"
            }
        ]
        testList "Quelltext" [
            test "kein DateTime.TryParse, DateTime.Parse oder CultureInfo im Feld" {
                let src = readDs "DateField.fs"
                for forbidden in [ "TryParse"; "DateTime.Parse"; "CultureInfo" ] do
                    // Kommentare dürfen die Wörter nennen, Code nicht
                    let code = src.Split('\n') |> Array.filter (fun l -> not (l.TrimStart().StartsWith "///"))
                    Expect.isFalse (code |> Array.exists (fun l -> l.Contains forbidden)) forbidden
            }
            test "kein Inline-fontSize; Small bleibt klassenbasiert" {
                Expect.isFalse (contains "fontSize" (readDs "DateField.fs")) "fontSize"
                Expect.isFalse (Input.usesInlineFontSize Input.Small) "Small ohne Inline-Größe"
                Expect.isTrue (contains "Size = Input.Small" (readDs "DateField.fs")) "Input.Small"
            }
            test "Schrift 16px am Handy, 14px am PC, per important gegen .input-field" {
                Expect.equal DateField.textSizeClass "text-base! md:text-sm!" "Klassen"
                Expect.isTrue (contains "textSizeClass" (readDs "DateField.fs")) "im Feld verwendet"
            }
            test "Kalender-Knopf nutzt showPicker mit focus/click als Rückfall" {
                let src = readDs "DateField.fs"
                Expect.isTrue (contains "showPicker" src) "showPicker"
                Expect.isTrue (contains "el.focus ()" src) "focus"
                Expect.isTrue (contains "el.click ()" src) "click"
            }
            test "Der Hinweis ist deutsch" {
                Expect.equal DateField.invalidHint "Datum als TT.MM.JJJJ" "Hinweis"
            }
            test "Styleguide nennt das Datumsfeld, die Karte zeigt es" {
                Expect.isTrue (contains "Datumsfeld" (readRepo [ "standards"; "frontend"; "styleguide.md" ])) "styleguide.md"
                Expect.isTrue (contains "DateField.view" (readRepo [ "src"; "Client"; "Components"; "Styleguide"; "View.fs" ])) "Styleguide-Karte"
            }
        ]
        testList "Übernehmen gesperrt (PickerParts)" [
            test "gesperrter Zustand ist gedämpft ohne Hover und Druckeffekt" {
                let off = PickerParts.applyButtonClassFor true
                Expect.isTrue (has "opacity-50" off) off
                Expect.isTrue (has "cursor-not-allowed" off) off
                Expect.isFalse (off.Contains "hover:") off
                Expect.isFalse (off.Contains "active:") off
            }
            test "freigegeben ist es der normale Knopf" {
                Expect.equal (PickerParts.applyButtonClassFor false) PickerParts.applyButtonClass "normal"
            }
            test "applyButtonWhen setzt disabled" {
                let src = readDs "PickerParts.fs"
                Expect.isTrue (contains "prop.disabled disabled" src) "disabled"
            }
        ]
    ]

[<Tests>]
let auswahlTests = tests

[<Tests>]
let datumsfeldTests = dateFieldTests
