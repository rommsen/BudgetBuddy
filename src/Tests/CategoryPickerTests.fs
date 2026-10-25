module CategoryPickerTests

// design-system-gffps: Kategorie-Picker mit Einfachauswahl und Karte "Budget nach dem Wechsel".
// Feliz-Elemente lassen sich unter .NET nicht inspizieren. Die Regeln (Filtern, Gruppieren,
// Vorschläge, Tastatur, Auswahl, Karteninhalt) liegen deshalb in öffentlichen reinen
// Funktionen und werden mit Werten geprüft; die Verdrahtung der Ansicht prüfen
// Quelltext-Zusicherungen, die echte Darstellung der Playwright-Wächter in scripts/.

open System
open System.IO
open Expecto
open Shared.Domain
open Client.DesignSystem
open Client.DesignSystem.CategoryPicker

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readDs (file: string) =
    File.ReadAllText(Path.Combine(repoRoot, "src", "Client", "DesignSystem", file))

let private idOf (n: int) = YnabCategoryId (Guid.Parse $"00000000-0000-0000-0000-{n:D12}")

let private cat (n: int) (group: string) (name: string) (hidden: bool) : YnabCategory =
    { Id = idOf n
      Name = name
      GroupName = group
      Available = Some { Amount = 10m; Currency = "EUR" }
      Hidden = hidden }

let private categories =
    [ cat 1 "Alltag" "Lebensmittel" false
      cat 2 "Alltag" "Haushalt" false
      cat 3 "Wohnen" "Möbel" false
      cat 4 "Wohnen" "Nebenkosten" false
      cat 5 "Fixkosten" "Altes Sparziel" true ]

let private names (cs: YnabCategory list) = cs |> List.map (fun c -> c.Name)

let private tests =
    testList "Kategorie-Picker (gffps)" [
        testList "Filtern" [
            test "findet Name und Gruppe ohne Beachtung der Groß- und Kleinschreibung" {
                Expect.isTrue (matchesSearch "LEBENS" categories.[0]) "Name, groß"
                Expect.isTrue (matchesSearch "alltag" categories.[1]) "Gruppe, klein"
                Expect.isFalse (matchesSearch "möbel" categories.[0]) "kein Treffer"
            }
            test "Leerer oder nur aus Leerzeichen bestehender Suchtext passt auf alles" {
                Expect.isTrue (matchesSearch "" categories.[0]) "leer"
                Expect.isTrue (matchesSearch "   " categories.[0]) "Leerzeichen"
            }
            test "Suche wirkt auf Gruppen und auf den Versteckt-Abschnitt" {
                let l = layout "spar" None [] categories
                Expect.isEmpty l.Groups "sichtbare Gruppen"
                Expect.equal (names l.Hidden) [ "Altes Sparziel" ] "versteckter Treffer"
                Expect.equal l.HiddenCount 1 "Zahl im Kopf bleibt die Gesamtzahl"
            }
        ]

        testList "Gruppierung und Versteckt" [
            test "Gruppen in Reihenfolge der Eingabe, versteckte nicht darunter" {
                let l = layout "" None [] categories
                Expect.equal (l.Groups |> List.map fst) [ "Alltag"; "Wohnen" ] "Gruppen"
                Expect.equal (names (snd l.Groups.[0])) [ "Lebensmittel"; "Haushalt" ] "Alltag"
                Expect.equal (names l.Hidden) [ "Altes Sparziel" ] "versteckt"
                Expect.equal l.HiddenCount 1 "Zahl"
            }
            test "Ohne versteckte Kategorien zählt der Kopf null" {
                let l = layout "" None [] (categories |> List.filter (fun c -> not c.Hidden))
                Expect.equal l.HiddenCount 0 "keine versteckten"
            }
            test "Versteckte Zeilen laufen in der Tastaturfolge nur aufgeklappt mit" {
                let l = layout "" None [] categories
                Expect.equal (List.length (navigableRows false l)) 5 "zu: Ohne Kategorie und vier sichtbare"
                Expect.equal (List.length (navigableRows true l)) 6 "auf: plus die versteckte"
            }
        ]

        testList "Vorschläge" [
            test "ohne Dubletten, ohne unbekannte Ids, ohne die aktuelle Kategorie, Reihenfolge bleibt" {
                let result =
                    suggestedCategories categories (Some (idOf 2)) [ idOf 3; idOf 3; idOf 99; idOf 2; idOf 1 ]
                Expect.equal (names result) [ "Möbel"; "Lebensmittel" ] "Vorschläge"
            }
            test "auf höchstens drei gekürzt" {
                let result = suggestedCategories categories None [ idOf 4; idOf 3; idOf 2; idOf 1 ]
                Expect.equal (names result) [ "Nebenkosten"; "Möbel"; "Haushalt" ] "drei, häufigste zuerst"
            }
            test "eine leere Vorschlagsliste rendert keinen Abschnitt und keinen Alle-Kategorien-Kopf" {
                let l = layout "" None [] categories
                Expect.isEmpty l.Suggested "kein Abschnitt VORGESCHLAGEN"
                Expect.isFalse l.ShowAllHeader "kein Kopf"
            }
            test "mit Vorschlägen steht der Kopf Alle Kategorien, bei einer Suche entfällt beides" {
                let l = layout "" None [ idOf 3 ] categories
                Expect.equal (names l.Suggested) [ "Möbel" ] "Abschnitt"
                Expect.isTrue l.ShowAllHeader "Kopf"
                let searching = layout "mö" None [ idOf 3 ] categories
                Expect.isEmpty searching.Suggested "Suche blendet Vorschläge aus"
                Expect.isFalse searching.ShowAllHeader "Suche blendet Kopf aus"
            }
            test "Vorschlagszeile trägt Gruppe und Name" {
                Expect.equal (suggestedLabel categories.[2]) "Wohnen: Möbel" "Beschriftung"
            }
        ]

        testList "Aktuelle Kategorie und Ohne Kategorie" [
            test "Markierung nur an der aktuellen Kategorie" {
                Expect.isTrue (isCurrentCategory (Some (idOf 2)) categories.[1]) "aktuell"
                Expect.isFalse (isCurrentCategory (Some (idOf 2)) categories.[0]) "andere"
                Expect.isFalse (isCurrentCategory None categories.[0]) "ohne Kategorie"
            }
            test "Die Zeile Ohne Kategorie steht oben und ist wählbar mit None" {
                let l = layout "" None [] categories
                Expect.isTrue l.ShowWithout "Zeile da"
                let rows = navigableRows false l
                Expect.equal (List.head rows) WithoutCategory "erste Zeile"
                Expect.equal (choiceOf WithoutCategory) None "None"
            }
            test "Bei einer Suche steht die Zeile nur, wenn sie passt" {
                Expect.isFalse (layout "möbel" None [] categories).ShowWithout "kein Treffer"
                Expect.isTrue (layout "ohne" None [] categories).ShowWithout "Treffer"
            }
            test "Wählen einer Kategorie meldet genau ihre Id" {
                Expect.equal (choiceOf (Category categories.[2])) (Some (idOf 3)) "Some id"
            }
        ]

        testList "Tastatur" [
            let rows = navigableRows false (layout "" None [] categories)
            test "Pfeil runter beginnt oben und bleibt am Ende stehen" {
                Expect.equal (keyAction "ArrowDown" rows None false) (MoveTo 0) "von nichts"
                Expect.equal (keyAction "ArrowDown" rows (Some 1) false) (MoveTo 2) "weiter"
                Expect.equal (keyAction "ArrowDown" rows (Some 4) false) (MoveTo 4) "Ende"
            }
            test "Pfeil hoch beginnt unten und bleibt oben stehen" {
                Expect.equal (keyAction "ArrowUp" rows None false) (MoveTo 4) "von nichts"
                Expect.equal (keyAction "ArrowUp" rows (Some 2) false) (MoveTo 1) "zurück"
                Expect.equal (keyAction "ArrowUp" rows (Some 0) false) (MoveTo 0) "Anfang"
            }
            test "Enter wählt die markierte Zeile, Ohne Kategorie als None" {
                Expect.equal (keyAction "Enter" rows (Some 0) false) (Choose None) "Ohne Kategorie"
                Expect.equal (keyAction "Enter" rows (Some 3) false) (Choose (Some (idOf 3))) "Möbel"
            }
            test "Enter ohne Markierung tut nichts, bei aktiver Suche wählt es den ersten Treffer" {
                Expect.equal (keyAction "Enter" rows None false) NoAction "ohne Suche"
                let found = navigableRows false (layout "möbel" None [] categories)
                Expect.equal (keyAction "Enter" found None true) (Choose (Some (idOf 3))) "erster Treffer"
            }
            test "Esc schließt nur den Picker" {
                Expect.equal (keyAction "Escape" rows (Some 2) false) CloseOnly "Esc"
            }
            test "andere Tasten und leere Listen bewirken nichts" {
                Expect.equal (keyAction "a" rows None false) NoAction "Buchstabe"
                Expect.equal (keyAction "ArrowDown" [] None false) NoAction "leer"
            }
        ]

        testList "Ansicht (Quelltext)" [
            test "Zeilen kommen aus PickerParts.row, der Betrag aus dem Betragsslot (Money.available)" {
                let src = readDs "CategoryPicker.fs"
                Expect.stringContains src "PickerParts.row" "Zeile"
                Expect.stringContains src "c.Available |> Option.map" "Available nur wenn gesetzt"
                Expect.stringContains PickerParts.nameClass "truncate" "Name gekürzt"
                Expect.stringContains PickerParts.amountSlotClass "text-right" "Betrag rechtsbündig"
            }
            test "Einfachauswahl: Klick übernimmt sofort, kein Übernehmen-Knopf im Baustein" {
                let src = readDs "CategoryPicker.fs"
                Expect.stringContains src "props.OnSelect (Some c.Id)" "Klick meldet Some id"
                Expect.stringContains src "props.OnSelect None" "Klick meldet None"
                Expect.isFalse (src.Contains "applyButton") "kein Übernehmen"
                Expect.isFalse (src.Contains "PickerParts.footer") "kein Footer mit Übernehmen"
            }
            test "Zwei Hüllen, ein Inhalt: Popover über FilterPopover, Sheet über BottomSheet" {
                let src = readDs "CategoryPicker.fs"
                Expect.stringContains src "FilterPopover.view" "Popover"
                Expect.stringContains src "BottomSheet.view sheetProps" "Sheet"
                Expect.stringContains src "BottomSheet.viewLayered sheetProps" "Sheet über dem Detail"
                // Popover, Popover der Auswahl (ledger-w29y6) und Sheet rendern denselben Content.
                Expect.equal (src.Split("Content {").Length - 1) 3 "alle Hüllen rendern denselben Content"
            }
            test "Esc im Picker bleibt im Picker" {
                let src = readDs "CategoryPicker.fs"
                Expect.stringContains src "CloseOnly ->" "Esc behandelt"
                Expect.stringContains src "e.stopPropagation ()" "Esc bubbelt nicht weiter"
            }
            test "Neue Datei steht in der Kompilierreihenfolge hinter CategoryMultiSelect" {
                let fsproj = File.ReadAllText(Path.Combine(repoRoot, "src", "Client", "Client.fsproj"))
                let multi = fsproj.IndexOf "DesignSystem/CategoryMultiSelect.fs"
                let picker = fsproj.IndexOf "DesignSystem/CategoryPicker.fs"
                Expect.isTrue (multi >= 0 && picker > multi) "Reihenfolge"
            }
        ]

        testList "CategoryChangeCard" [
            let open' = CategoryChangeCard.hasContent
            test "zwei Zeilen mit Zahlen zeigen alten und neuen Wert" {
                let l1 : CategoryChangeCard.ChangeLine =
                    { Label = "Alltag: Haushalt"; OldText = Some "412,10 €"; NewText = Some "494,00 €" }
                Expect.equal (CategoryChangeCard.lineParts l1) (CategoryChangeCard.Transition (Some "412,10 €", Some "494,00 €")) "alt und neu"
                Expect.isTrue (open' [ l1; { l1 with Label = "Wohnen: Möbel" } ]) "Karte hat Inhalt"
            }
            test "eine Zeile ohne Zahlen zeigt nur das Label" {
                let line : CategoryChangeCard.ChangeLine = { Label = "Wohnen: Möbel"; OldText = None; NewText = None }
                Expect.equal (CategoryChangeCard.lineParts line) CategoryChangeCard.LabelOnly "nur Label"
            }
            test "eine leere Liste rendert nichts" {
                Expect.isFalse (open' []) "leer"
            }
            test "alter Wert durchgestrichen und gedämpft, neuer Wert in Hauptfarbe ohne Glow" {
                let has (needle: string) (cls: string) = cls.Split(' ') |> Array.contains needle
                Expect.isTrue (has "line-through" CategoryChangeCard.oldValueClass) CategoryChangeCard.oldValueClass
                Expect.isTrue (has Tokens.Colors.textMuted CategoryChangeCard.oldValueClass) CategoryChangeCard.oldValueClass
                Expect.isFalse (has "line-through" CategoryChangeCard.newValueClass) CategoryChangeCard.newValueClass
                Expect.isTrue (has Tokens.Colors.textPrimary CategoryChangeCard.newValueClass) CategoryChangeCard.newValueClass
                Expect.isFalse (CategoryChangeCard.newValueClass.Contains "glow") "kein Glow"
            }
        ]

        testList "Lade- und Fehlerzustand (dmw83)" [
            // Befund vom 2026-10-24: Waren die Kategorien nicht geladen, zeigte der Picker nur
            // "Ohne Kategorie". Ein Ladefehler sah aus wie "es gibt keine Kategorien".
            let noRows (l: Layout) =
                not l.ShowWithout && l.Suggested.IsEmpty && l.Groups.IsEmpty && l.HiddenCount = 0 && l.Hidden.IsEmpty

            test "lädt ohne Liste: keine Zeilen, auch nicht Ohne Kategorie, dafür der Ladehinweis" {
                let l = layoutFor PickerLoading "" None [] []
                Expect.isTrue (noRows l) "keine Zeilen"
                Expect.equal l.Notice (Some LoadingNotice) "Ladehinweis"
                Expect.equal loadingText "Kategorien werden geladen" "Text"
            }
            test "lädt ohne Liste: eine Suche ändert nichts" {
                let l = layoutFor PickerLoading "ohne" None [] []
                Expect.isTrue (noRows l) "keine Zeilen"
                Expect.equal l.Notice (Some LoadingNotice) "Ladehinweis"
            }
            test "fehlgeschlagen: keine Zeilen, Fehlerhinweis mit Erneut versuchen" {
                let l = layoutFor PickerFailed "" None [] []
                Expect.isTrue (noRows l) "keine Zeilen"
                Expect.equal l.Notice (Some FailedNotice) "Fehlerhinweis"
                Expect.equal failedText "Kategorien konnten nicht geladen werden" "Text"
                Expect.equal retryLabel "Erneut versuchen" "Knopf"
            }
            test "lädt mit vorhandener Liste: gleich dem Zustand bereit" {
                let loading = layoutFor PickerLoading "" None [] categories
                let ready = layoutFor PickerReady "" None [] categories
                Expect.equal loading ready "gleiches Layout"
                Expect.isNone loading.Notice "kein Hinweis"
                Expect.isFalse (List.isEmpty loading.Groups) "Liste sichtbar"
            }
            test "bereit mit leerer Liste zeigt weiter Ohne Kategorie" {
                let l = layoutFor PickerReady "" None [] []
                Expect.isTrue l.ShowWithout "Zeile bleibt"
                Expect.isNone l.Notice "kein Hinweis"
            }
            test "layout ohne Ladezustand entspricht bereit" {
                Expect.equal (layout "" None [] categories) (layoutFor PickerReady "" None [] categories) "gleich"
            }
            test "Die Hülle zeigt Spinner und Knopf aus dem Design-System und ruft OnRetry" {
                let src = readDs "CategoryPicker.fs"
                Expect.stringContains src "Loading.spinner" "Spinner"
                Expect.stringContains src "Button.secondary retryLabel (fun () -> props.OnRetry ())" "Knopf löst OnRetry aus"
            }
        ]

        testList "Styleguide" [
            test "Galerie und geschriebener Styleguide führen den Picker" {
                let gallery = File.ReadAllText(Path.Combine(repoRoot, "src", "Client", "Components", "Styleguide", "View.fs"))
                Expect.stringContains gallery "CategoryPicker.popover" "Popover in der Galerie"
                Expect.stringContains gallery "CategoryPicker.sheet" "Sheet in der Galerie"
                Expect.stringContains gallery "CategoryChangeCard.view" "Karte in der Galerie"
                Expect.stringContains gallery "CategoryPicker.PickerLoading" "Zustand lädt in der Galerie"
                Expect.stringContains gallery "CategoryPicker.PickerFailed" "Zustand fehlgeschlagen in der Galerie"
                let guide = File.ReadAllText(Path.Combine(repoRoot, "standards", "frontend", "styleguide.md"))
                Expect.stringContains guide "CategoryPicker" "Eintrag im Styleguide"
                Expect.stringContains guide "CategoryChangeCard" "Karte im Styleguide"
            }
        ]
    ]

[<Tests>]
let categoryPickerTests = tests
