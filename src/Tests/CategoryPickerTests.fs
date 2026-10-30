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

let private account (n: int) (name: string) : YnabAccount =
    { Id = YnabAccountId (Guid.Parse $"00000000-0000-0000-0001-{n:D12}")
      Name = name
      Balance = { Amount = 0m; Currency = "EUR" }
      OnBudget = true
      Closed = false }

let private accounts = [ account 1 "Bar"; account 2 "Tagesgeld" ]

/// Ein Zielname, der auch in einer Kategorie steckt: "Bar" trifft Konto und Kategorie.
let private withBarCategory = categories @ [ cat 6 "Alltag" "Bar-Auslagen" false ]

let private targetProps : CategoryPickerProps =
    { Categories = withBarCategory
      Current = None
      Suggested = [ idOf 1 ]
      Recent = [ idOf 2 ]
      Accounts = accounts
      CurrentAccount = None
      OfferWithout = true
      StandLabel = None
      LoadState = PickerReady
      OnRetry = ignore
      OnSelect = ignore
      OnClose = ignore }

let private accountNames (l: Layout) = l.Accounts |> List.map (fun a -> a.Name)

let private hasNoCategories (l: Layout) =
    Expect.isFalse l.ShowWithout "keine Zeile Ohne Kategorie"
    Expect.isEmpty l.Suggested "keine Vorschläge"
    Expect.isEmpty l.Recent "nichts unter Zuletzt verwendet"
    Expect.isEmpty l.Groups "keine Gruppen"
    Expect.equal l.HiddenCount 0 "kein Abschnitt Versteckt"
    Expect.isFalse l.ShowAllHeader "kein Kopf Alle Kategorien"

// design-system-bym4m: ein Picker für das Ziel einer Split-Zeile, Kategorie oder Konto.
let private targetTests =
    testList "Ziel-Picker: Kategorie oder Konto (bym4m)" [
        test "ohne Suche stehen die Konten als eigener Abschnitt unter den Kategorien" {
            let l = layoutForPicker targetProps ""
            Expect.equal (accountNames l) [ "Bar"; "Tagesgeld" ] "alle Konten"
            Expect.isNonEmpty l.Groups "Kategorien bleiben"
            let rows = navigableRows false l
            Expect.equal (List.rev rows |> List.take 2 |> List.rev) [ Account accounts.[0]; Account accounts.[1] ] "Konten zuletzt"
        }
        test "aufgeklappte versteckte Kategorien stehen vor den Konten" {
            let rows = navigableRows true (layoutForPicker targetProps "")
            let lastCategory = rows |> List.findIndexBack (function Category _ -> true | _ -> false)
            let firstAccount = rows |> List.findIndex (function Account _ -> true | _ -> false)
            Expect.isLessThan lastCategory firstAccount "Reihenfolge wie im Rendern"
        }
        test "die Suche nach einem Kontonamen findet das Konto neben passenden Kategorien" {
            let l = layoutForPicker targetProps "bar"
            Expect.equal (accountNames l) [ "Bar" ] "Konto"
            Expect.equal (l.Groups |> List.collect snd |> names) [ "Bar-Auslagen" ] "Kategorie"
        }
        test "die Konto-Zeile heißt → Name" {
            Expect.equal (accountLabel accounts.[0]) "→ Bar" "Übertrag-Zeile"
        }
        test "Konto: allein zeigt nur die Konten" {
            let l = layoutForPicker targetProps "Konto:"
            Expect.equal (accountNames l) [ "Bar"; "Tagesgeld" ] "alle Konten"
            hasNoCategories l
        }
        test "Konto: mit Suchtext zeigt nur die passenden Konten" {
            for search in [ "Konto: Bar"; "konto:bar"; "KONTO:   BAR"; "  Konto: bar  " ] do
                let l = layoutForPicker targetProps search
                Expect.equal (accountNames l) [ "Bar" ] search
                hasNoCategories l
        }
        test "Konto: ohne passendes Konto lässt keine Zeile übrig" {
            Expect.isEmpty (navigableRows true (layoutForPicker targetProps "Konto: xyz")) "keine Zeile"
        }
        test "Konten erscheinen nie unter Vorgeschlagen oder Zuletzt verwendet" {
            let l = layoutForPicker targetProps ""
            Expect.equal (names l.Suggested) [ "Lebensmittel" ] "nur Kategorien"
            Expect.equal (names l.Recent) [ "Haushalt" ] "nur Kategorien"
        }
        test "ohne Konten bleibt es ein reiner Kategorie-Picker, auch mit dem Präfix" {
            let plain = { targetProps with Accounts = [] }
            Expect.isEmpty (layoutForPicker plain "").Accounts "keine Konten"
            Expect.equal (navigableRows false (layoutForPicker plain "")) (navigableRows false (layoutForRecent PickerReady "" None [ idOf 1 ] [ idOf 2 ] withBarCategory)) "wie bisher"
            Expect.equal (searchScope [] "Konto: Bar") (AllTargets "Konto: Bar") "kein Präfix"
            let konto = { plain with Categories = [ cat 7 "Banken" "Konto: Gebühren" false ] }
            Expect.equal ((layoutForPicker konto "konto:").Groups |> List.collect snd |> names) [ "Konto: Gebühren" ] "sucht weiter in Kategorien"
        }
        test "ein Ladehinweis nimmt die Kategorien, die Konten bleiben wählbar" {
            let l = layoutForPicker { targetProps with Categories = []; LoadState = PickerFailed } ""
            Expect.equal l.Notice (Some FailedNotice) "Hinweis"
            Expect.equal (accountNames l) [ "Bar"; "Tagesgeld" ] "Konten"
        }
        test "ohne OfferWithout fehlt die Zeile Ohne Kategorie" {
            Expect.isFalse (layoutForPicker { targetProps with OfferWithout = false } "").ShowWithout "Import-Split"
        }
        test "die Pfeiltasten erreichen die Konto-Zeilen, Enter wählt das Konto" {
            let rows = navigableRows false (layoutForPicker targetProps "")
            let last = List.length rows - 1
            Expect.equal (keyAction "ArrowUp" rows None false) (MoveTo last) "letzte Zeile ist ein Konto"
            Expect.equal (keyAction "Enter" rows (Some last) false) (Choose (ChoseAccount accounts.[1].Id)) "Konto"
            let found = navigableRows false (layoutForPicker targetProps "Konto: tages")
            Expect.equal (keyAction "Enter" found None true) (Choose (ChoseAccount accounts.[1].Id)) "erster Treffer"
        }
        test "ein Übertrag hakt nicht Ohne Kategorie an" {
            Expect.isTrue (isWithoutCurrent None None) "ohne Ziel"
            Expect.isFalse (isWithoutCurrent None (Some accounts.[0].Id)) "Übertrag"
            Expect.isFalse (isWithoutCurrent (Some (idOf 1)) None) "Kategorie"
        }
        test "Aufrufer ohne Konten bekommen nur die Kategorie-Wahl" {
            let mutable got = []
            let select = categoryChoice (fun id -> got <- id :: got)
            select (ChoseCategory (Some (idOf 1)))
            select (ChoseCategory None)
            select (ChoseAccount accounts.[0].Id)
            Expect.equal got [ None; Some (idOf 1) ] "Konto kommt nicht an"
        }
        test "Suchfeld und Titel nennen Konten nur, wenn es welche gibt" {
            Expect.equal (searchPlaceholder []) "Kategorie suchen…" "ohne"
            Expect.equal (searchPlaceholder accounts) "Kategorie oder Konto suchen…" "mit"
            Expect.equal (sheetTitle []) "Kategorie wählen" "ohne"
            Expect.equal (sheetTitle accounts) "Ziel wählen" "mit"
        }
    ]

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
                Expect.equal (choiceOf WithoutCategory) (ChoseCategory None) "None"
            }
            test "Bei einer Suche steht die Zeile nur, wenn sie passt" {
                Expect.isFalse (layout "möbel" None [] categories).ShowWithout "kein Treffer"
                Expect.isTrue (layout "ohne" None [] categories).ShowWithout "Treffer"
            }
            test "Wählen einer Kategorie meldet genau ihre Id" {
                Expect.equal (choiceOf (Category categories.[2])) (ChoseCategory (Some (idOf 3))) "Some id"
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
                Expect.equal (keyAction "Enter" rows (Some 0) false) (Choose (ChoseCategory None)) "Ohne Kategorie"
                Expect.equal (keyAction "Enter" rows (Some 3) false) (Choose (ChoseCategory (Some (idOf 3)))) "Möbel"
            }
            test "Enter ohne Markierung tut nichts, bei aktiver Suche wählt es den ersten Treffer" {
                Expect.equal (keyAction "Enter" rows None false) NoAction "ohne Suche"
                let found = navigableRows false (layout "möbel" None [] categories)
                Expect.equal (keyAction "Enter" found None true) (Choose (ChoseCategory (Some (idOf 3)))) "erster Treffer"
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
                Expect.stringContains src "props.OnSelect (ChoseCategory (Some c.Id))" "Klick meldet Some id"
                Expect.stringContains src "props.OnSelect (ChoseCategory None)" "Klick meldet None"
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

[<Tests>]
let categoryPickerTargetTests = targetTests
