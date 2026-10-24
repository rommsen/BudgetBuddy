---
id: design-system-gffps
title: "Design-System: Kategorie-Picker mit Einfachauswahl als Popover am PC und BottomSheet am Handy, mit Suche, Vorschlägen zum Payee, Gruppen, Zeile Ohne Kategorie, Available rechts und Stand-Zeile, dazu die Karte Budget nach dem Wechsel"
status: done
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

## Verifier note (iteration 1)

**REASONS:**
- Check 1, Kriterium 2 (Ansichts-Test): kein Test prüft am gerenderten Picker die Farbklasse des Betrags nach Vorzeichen (positiv, negativ, null; die Galerie-Daten haben alle drei), keiner, dass "Altes Sparziel" (Available = None) nach dem Aufklappen von Versteckt einen leeren Slot hat, und der Fall ohne Stand-Zeile wird nie gerendert. Die Quelltext-Zusicherung `stringContains src "| None -> ()"` in CategoryPickerTests.fs trägt nicht, weil der Text mehrfach in CategoryPicker.fs steht.
- Check 1, Kriterium 3: "genau einmal" ist nicht belegt; niemand klickt die Zeile "Ohne Kategorie" mit der Maus (None nur über Pfeil plus Enter); die .NET-Tests suchen nur `props.OnSelect None` im Quelltext.
- Zur Info: `dotnet build` 0 Warnungen, Schreibregel-grep sauber, `BottomSheet.view` unverändert im Verhalten; übrige Läufe in diesem Durchgang nicht gelaufen.

**SUGGESTED_FIX:** Playwright-Tests in `scripts/category-picker.test.mjs` an der echten Sektion 26: Farbklasse von `Money.available` für eine positive, negative und Null-Zeile; Versteckt aufklappen, Slot von "Altes Sparziel" leer; Mausklick auf "Ohne Kategorie" und Ergebnis prüfen; Zähler für OnSelect in der Galerie-Demo (z. B. data-Attribut), damit "genau einmal" prüfbar ist; Picker-Variante ohne StandLabel in der Galerie mit Test, dass die Stand-Zeile fehlt. Die Zusicherung auf `"| None -> ()"` streichen oder ersetzen.

**ITERATION_HINT:** likely-fixable

## Verifier note (iteration 2)

**REASONS:**
- Check 2: der serielle Lauf `node --test --test-concurrency=1 scripts/*.test.mjs` endete mit Exit 1 (184 von 185). Rot war "Handy (390): Sheet mit denselben Zeilen, 16 px Suchschrift, Betragsslots bündig" (`scripts/category-picker.test.mjs:198`, `AssertionError: Trefferfläche 43.99993896484375`): `geometry.minRow >= 44` vergleicht einen Float aus `getBoundingClientRect` exakt mit 44.
- Zeitabhängig: gemessen wird direkt nach `.bottom-sheet.active`, das Sheet steckt noch in der 450-ms-Feder-Transition (`src/Client/styles.css` 1637 bis 1656). Einzeln lief die Datei 3 von 3 grün, unter Last rot.
- Zur Info grün: `dotnet build` 0 Warnungen, `dotnet test` 1735/7/0, `npm run build`, `guard:classes`, `guard:tokens`; die Lücken aus Iteration 1 (Kriterien 2 und 3) sind am echten Galerie-Element belegt.

**SUGGESTED_FIX:** Messung im Handy-390-Test stabil machen: Ende der Transition abwarten (`transitionend` oder Rechteck über zwei Animation-Frames unverändert) oder gerundet bzw. mit Subpixel-Toleranz vergleichen; danach den vollen seriellen `test:scripts`-Lauf wiederholen.

**ITERATION_HINT:** likely-fixable

## Outcome

Neuer DS-Baustein `src/Client/DesignSystem/CategoryPicker.fs` (Compile-Zeile direkt hinter `CategoryMultiSelect.fs`), aufgebaut auf `PickerParts`, `FilterPopover` und `BottomSheet`.

**Picker.** `CategoryPicker.popover isOpen props` (PC, Mechanik von `FilterPopover`, Breite `categoryWidthClass`) und `CategoryPicker.sheet isOpen subtitle props` bzw. `sheetLayered` (Handy, über dem Detail auf `layer-2`) rendern denselben privaten `Content`. Props: `Categories`, `Current`, `Suggested` (Ids, der Baustein kürzt auf drei, ohne Dubletten, ohne unbekannte Ids, ohne die aktuelle Kategorie), `StandLabel: string option`, `OnSelect: YnabCategoryId option -> unit`, `OnClose`. Aufbau: Suchfeld (sticky), Zeile "Ohne Kategorie", Abschnitt Vorgeschlagen (Zeilenlabel "Gruppe: Name"), "Alle Kategorien" nur über Vorgeschlagen und nicht bei Suche, Gruppen, "Versteckt (n)" eingeklappt, Stand-Zeile. Available rechts über `PickerParts.row` und `Money.available`, aktuelle Kategorie mit Haken und `aria-selected`. Klick übernimmt sofort (kein Übernehmen-Knopf). Tastatur: Pfeile bewegen (klemmen an den Enden), Enter wählt (ohne Markierung bei aktiver Suche den ersten Treffer), Esc schließt nur den Picker und bubbelt nicht weiter. Fokus im Suchfeld beim Öffnen (Sheet nur mit Maus-Zeiger, sonst käme die Tastatur). Das Sheet setzt `swallowNextClick` und Haptik wie `BottomSheet.categoryPicker`.

**Reine Funktionen** (öffentlich, mit Werten getestet): `matchesSearch`, `layout`, `suggestedCategories`, `suggestedLabel`, `isCurrentCategory`, `showWithoutRow`, `navigableRows`, `choiceOf`, `keyAction`.

**Karte.** `CategoryPicker.CategoryChangeCard` (verschachteltes Modul): `ChangeLine { Label; OldText; NewText }`, `view` (leere Liste rendert `Html.none`), `lineParts`, `oldValueClass` (Mono, durchgestrichen, gedämpft), `newValueClass` (Mono, Hauptfarbe, kein Glow). Aus Card-Tokens, ohne Hex und ohne Maße aus den Tafeln.

**BottomSheet.** Kleine Erweiterung: `BottomSheet.viewLayered` (privates `Elevated`-Feld im Internal-Record, `layer-2` an Overlay und Sheet), `view` bleibt unverändert im Verhalten.

**Galerie und Styleguide.** `/styleguide` Sektion "26. Kategorie-Picker (Einfachauswahl)" (Popover, Sheet, Karte, Beispieldaten) direkt hinter der Sektion 19, ohne `data-ds-card` (der Export-Wächter kennt eine feste Kartenliste). `standards/frontend/styleguide.md` hat einen Absatz zum Picker und zur Karte unter "Auswahl".

**Tests.** `src/Tests/CategoryPickerTests.fs` (31 Tests, nach `RulesReorderTests.fs` eingehängt): Filtern, Gruppierung, Versteckt, Vorschläge, Markierung, Ohne Kategorie, Tastatur, Quelltext-Zusicherungen, Karte, Styleguide-Eintrag. Feliz-Elemente sind unter .NET nicht inspizierbar, deshalb prüft der neue Playwright-Wächter `scripts/category-picker.test.mjs` (9 Tests, Teil von `test:scripts`) die echte Darstellung in der Galerie: Fokus im Suchfeld, Ohne Kategorie oben, Vorgeschlagen und Alle Kategorien, Stand-Zeile nur mit Beschriftung, kein Übernehmen-Knopf, Betragsslots rechtsbündig (alle gleiche rechte Kante) am PC und bei 390 px, Klick übernimmt und schließt, Pfeil und Enter, Esc, Suche (MIETE) blendet Vorgeschlagen aus, Sheet mit 16 px Suchschrift und Trefferfläche mindestens 44 px, Karte mit zwei durchgestrichenen alten Werten.

**Läufe (alle im Worktree).** `dotnet build`: 0 Warnungen, 0 Fehler. `dotnet test`: 1735 bestanden, 7 übersprungen (.env), 0 Fehler. `npm run build`: grün. `npm run guard:classes`: keine undefinierten Klassen. `npm run guard:tokens`: grün. `node --test --test-concurrency=1 scripts/*.test.mjs`: 185 von 185 grün. README_DELTA gegen `npm run check:readme-delta` geprüft (Exit 0).

**Hinweise für ledger-sfab0.** Der Aufrufer wickelt das Popover in einen `relative`-Wrapper am Feld; `Suggested` und `StandLabel` kommen vom Aufrufer. Die Karte erscheint nur, wenn der Aufrufer sie rendert. Sichtprüfung am Handy (Screenshot des offenen Sheets bei 390 px) war ruhig, Beträge bündig; das Urteil zu den human-eye-Kriterien bleibt bei Roman.

**Iteration 2 (Verifier-Befund zu Kriterium 2 und 3).** Alles am echten, gerenderten Element der Galerie-Sektion 26 in `scripts/category-picker.test.mjs`: (1) Farbklasse des Betrags je Zeile: Lebensmittel (positiv) `text-text-primary`, Freizeit (negativ) `text-neon-red`, Sonstiges (null) `text-text-muted`. (2) "Versteckt (1)" aufklappen: die Zeile "Altes Sparziel" (Available = None) hat keinen Betragsslot und keinen Betrag, Gegenprobe Miete hat den Slot. (3) Mausklick auf "Ohne Kategorie": Badge zeigt das Ergebnis, danach ist die Zeile `aria-selected`. (4) "Genau einmal": die Galerie-Demo zählt `OnSelect`-Aufrufe in einem Zähler (`data-ds-demo="category-picker-select-count"`, über eine Ref, damit auch doppelte Aufrufe zählen); der Test prüft pro Klick genau +1, für eine Kategoriezeile und für Ohne Kategorie. (5) Zweite Galerie-Variante "Popover ohne Stand-Zeile" (`StandLabel = None`): dort fehlt die Stand-Zeile, in der Variante mit Beschriftung steht sie genau einmal. (6) Die schwache Quelltext-Zusicherung auf `| None -> ()` in `CategoryPickerTests.fs` ist gestrichen (die Stand-Zeile deckt jetzt der Playwright-Test ab). Rot-Prüfung durch kurzes Zurückdrehen: ein doppelter `OnSelect None` lässt den Einmal-Test mit 3 statt 2 scheitern; ein Betragsslot für Kategorien ohne Available lässt den Versteckt-Test scheitern; eine immer gerenderte Stand-Zeile lässt den Stand-Test scheitern (danach wiederhergestellt). Der Farbtest prüft die echte Klasse von `Money.available` am Picker, nicht per Zurückdrehen. Geänderte Dateien dieser Iteration: `scripts/category-picker.test.mjs`, `src/Client/Components/Styleguide/View.fs`, `src/Tests/CategoryPickerTests.fs`. Läufe: `dotnet build` 0 Warnungen, `dotnet test` 1735 bestanden (7 übersprungen), `npm run build`, `guard:classes`, `guard:tokens` grün, `node --test --test-concurrency=1 scripts/*.test.mjs` 185 von 185 grün.

**Iteration 3 (Zeitfehler im Handy-Test).** Im Test "Handy (390): Sheet mit denselben Zeilen ..." wird erst gemessen, wenn die Lage des Sheets über fünf aufeinanderfolgende Animationsframes unverändert ist (`waitForFunction` mit `polling: "raf"`), denn die 450 ms lange Feder-Einfahrt verschob die Zeilenhöhe unter Last auf 43,99994. Die Trefferfläche wird mit 0,5 px Toleranz gegen 44 geprüft (`>= 43.5`, mit Kommentar), die Absicht von 44 px bleibt. Nur `scripts/category-picker.test.mjs` geändert. Läufe: `dotnet build` 0 Warnungen, `dotnet test` 1735 bestanden (7 übersprungen), die serielle Suite `node --test --test-concurrency=1 scripts/*.test.mjs` dreimal: 185 von 185 grün, 184 von 185 (einziger Fehler: Seitenladetimeout in `ledger-selection.test.mjs`, nicht aus dieser Task; die Datei einzeln wiederholt 2 von 2 grün), 185 von 185 grün. Schreibregel-Selbstcheck gegen 95092a5: keine Treffer.
