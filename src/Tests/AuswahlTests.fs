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

[<Tests>]
let auswahlTests = tests
