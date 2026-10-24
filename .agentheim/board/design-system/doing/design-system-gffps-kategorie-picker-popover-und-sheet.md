---
id: design-system-gffps
title: "Design-System: Kategorie-Picker mit Einfachauswahl als Popover am PC und BottomSheet am Handy, mit Suche, Vorschlägen zum Payee, Gruppen, Zeile Ohne Kategorie, Available rechts und Stand-Zeile, dazu die Karte Budget nach dem Wechsel"
status: doing
type: feature
context: design-system
created: 2026-10-24
completed:
depends_on: [design-system-001]
blocks: [ledger-sfab0]
tags: [frontend, design-system, picker, kategorie]
related_adrs: [0005, 0049, 0053, 0055]
related_research: []
prior_art: [design-system-bz0gn, design-system-zahv9]
---

## Why
"Kategorie ändern" (ADR 0054) braucht einen Picker für eine Kategorie (Einfachauswahl, Klick
übernimmt sofort). Das Design-System hat `CategoryMultiSelect` (Popover, Mehrfach, Übernehmen)
und `BottomSheet.categoryPicker` (Sheet, Einfach, nur Handy-Ablauf mit Payee-Kopf). Am PC fehlt
ein Einfach-Popover, und der Sheet-Picker ist an den Import-Ablauf gebunden. ADR 0053: ein
Picker für Detail und Leiste. Gestaltung: Tafel Ablauf 4, Screens 4.2, 4.3, 4.10 (`docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`).

## What
Ein Design-System-Baustein `CategoryPicker` (Datei neben `CategoryMultiSelect.fs`), der auf
`PickerParts` aufbaut:

1. **Einfachauswahl, Klick übernimmt sofort** (kein Übernehmen-Knopf). `OnSelect` bekommt
   `YnabCategoryId option` (`None` = "Ohne Kategorie").
2. **Zwei Hüllen, ein Inhalt:** Popover am Auslöser (PC, Mechanik wie `FilterPopover`) und
   BottomSheet (Handy, bestehende `BottomSheet.view`-Hülle). Der Inhalt ist derselbe
   (Suche, optionaler oberer Abschnitt **VORGESCHLAGEN**, Gruppenliste, "Alle Kategorien"-Kopf
   nach `PickerParts.showAllCategoriesHeader`). Der Abschnitt VORGESCHLAGEN (Roman, 2026-10-24)
   zeigt Kategorien, die der Payee im Kassenbuch bisher hatte: höchstens drei, häufigste
   zuerst, ohne die aktuelle Kategorie. Der Baustein rendert nur die übergebene Liste
   (`Suggested: YnabCategoryId list`); wer sie berechnet, steht in `ledger-sfab0`. Der alte
   Abschnitt "Zuletzt verwendet" entfällt.
3. **Zeile "Ohne Kategorie"** oben, wählbar. Aktuelle Kategorie der Buchung ist markiert.
4. **Available rechts** je Zeile (`PickerParts.amountSlotClass`, grün über null, rot unter
   null, gedämpft bei null), nur wenn `Available` gesetzt ist. Versteckte Kategorien
   eingeklappt unter "Versteckt (n)" wie in `CategoryMultiSelect`. Interne YNAB-Kategorien
   erscheinen nicht (die Liste liefert der Aufrufer).
5. **Fußzeile "Stand ..."** aus einer vom Aufrufer gelieferten Beschriftung (optional).
6. **Tastatur:** Pfeile bewegen, Enter wählt, Esc schließt nur den Picker (ADR 0053,
   Punkt 5). Suchfeld bekommt den Fokus beim Öffnen; am Handy 16 px Schrift (Input-Regel).
7. **Karte "Budget nach dem Wechsel"** als eigener rein darstellender Baustein
   (`CategoryChangeCard`, Tafel 4.3): Überschrift BUDGET NACH DEM WECHSEL, je Zeile Kategorie-
   Label, alter Wert durchgestrichen, Pfeil, neuer Wert. Eingabe: `Lines: { Label: string; OldText: string option; NewText: string option } list`
   (fertig formatierte Texte, das Design-System kennt weder Ledger noch Geld-Formatierung).
   Leere Liste rendert nichts. Sichtbarkeit und Dauer steuert der Aufrufer (`ledger-sfab0`).
8. Eintrag im Styleguide (`standards/frontend/styleguide.md`) und in der Galerie
   `/styleguide` (`src/Client/Components/Styleguide/View.fs`).

## Acceptance criteria
- [ ] Reine Funktionen getestet: Filtern nach Suchtext (Gruppe und Name, ohne Beachtung der
      Groß/Kleinschreibung, invariant), Gruppierung, Vorschläge ohne Dubletten, ohne
      unbekannte Ids, ohne die aktuelle Kategorie und auf höchstens drei gekürzt (Reihenfolge
      der Eingabe bleibt), Versteckt-Abschnitt, Markierung der aktuellen Kategorie. Eine leere
      Vorschlagsliste rendert keinen Abschnitt VORGESCHLAGEN.
- [ ] Ansichts-Test (Render): `None` rendert die Zeile "Ohne Kategorie"; `Available`
      gesetzt rendert den Betrag mit der Farbklasse nach Vorzeichen, `None` rendert keinen
      Betragsslot-Inhalt; die Stand-Zeile erscheint nur mit Beschriftung.
- [ ] Ansichts-Test: Klick auf eine Zeile ruft `OnSelect (Some id)` genau einmal, Klick auf
      "Ohne Kategorie" ruft `OnSelect None`; kein Übernehmen-Knopf im DOM.
- [ ] Tastatur-Test: Pfeil, Enter und Esc wie beschrieben.
- [ ] Ansichts-Test `CategoryChangeCard`: zwei Zeilen rendern Label, durchgestrichenen alten und
      neuen Wert; eine Zeile ohne Zahlen rendert nur das Label; leere Liste rendert nichts.
- [ ] Zwei Hüllen: gleiche Zeilen im Popover (Maus-Layout) und im Sheet (Handy-Layout).
- [ ] `npm run guard:classes` und `dotnet test` grün; Galerie rendert den Picker mit
      Beispieldaten.
- [ ] Der Picker wirkt am Handy ruhig und die Beträge stehen bündig rechts. [human-eye]
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` grün
      (Wächter aus `design-system-wfhxj`, sobald integriert).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Verfeinert am 2026-10-24 (Sammel-Modeling, Ablauf 4): "Zuletzt verwendet" ersetzt durch
VORGESCHLAGEN zum Payee, Karte `CategoryChangeCard` als Baustein hinzugefügt (Roman, 2026-10-24).
Handoff: `docs/ux/kassenbuch/handoff-ablauf-4-kategorie-und-split.md`.

Entworfen am 2026-10-24 (Sammel-Modeling) als Scheibe (a). Ziel-Lage: todo. Kein Bezug zum
Ledger-Zustand: der Aufrufer liefert die Liste (`YnabCategory`-Felder: Id, Gruppe, Name,
Available, Hidden). Dateien: neue Datei in `src/Client/DesignSystem/`, `Server`-fsproj nicht
betroffen, `Client.fsproj` (Compile-Reihenfolge), `Styleguide/View.fs`, `PickerParts.fs`
(nur falls ein Baustein fehlt). Überschneidung mit anderen Tasks: gering.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).
