---
id: design-system-btwhg
title: Input.Small zeigt am Handy 14px statt 16px (.input-field überstimmt text-base)
status: done
type: bug
context: design-system
created: 2026-10-19
depends_on: []
blocks: []
tags: [frontend, ios, input]
related_adrs: [0005]
related_research: []
prior_art: [design-system-zahv9, design-system-m7z9q]
---

## Why
Beim Messen des Datumsfelds (design-system-m7z9q) zeigte sich: `.input-field` in `src/Client/styles.css` (~678) setzt `font-size: 14px` ohne Layer. Die Tailwind-Klassen aus `Input.sizeToClass Small` (`text-base md:text-sm`) liegen im Utility-Layer und verlieren dagegen. Am Handy (390 px) misst ein `Input.Small` 14 px statt 16 px, iOS zoomt beim Fokus ein (Regel aus ADR 0005). Betroffen sind alle Aufrufer von `Input.text` mit `Size = Small` (Suchfelder der Auswahlen, Memo, Betrag). Das Datumsfeld umgeht es lokal mit `text-base! md:text-sm!`.

## What
- Ursache beheben (z. B. `font-size` aus `.input-field` entfernen oder `Input.Small` mit `!`-Varianten ausstatten) und prüfen, dass Medium/Large (Inline 16) und andere `.input-field`-Nutzer unverändert aussehen.
- Danach kann `DateField.textSizeClass` entfallen.

## Acceptance criteria
- [ ] Playwright misst bei 390x844 in einem `Input.Small`-Feld (Kategorie-Suche) 16px und bei 1440x900 14px.
- [ ] `Input.usesInlineFontSize Small` bleibt falsch.
- [ ] Medium und Large messen bei 390x844 und 1440x900 dieselbe Schriftgröße wie vor dem Fix (Playwright, je ein Feld).
- [ ] `DateField.textSizeClass` ist entfernt, das Datumsfeld misst bei 390x844 weiterhin 16px und bei 1440x900 14px.
- [ ] `npm run guard:classes`, `npm run build`, `npm run test:scripts`, `dotnet build`, `dotnet test` grün.
## Verifier note (iteration 1)

**REASONS:**
- Kriterium 1 nur halb abgedeckt: der neue Test in `scripts/ledger-filter-bar.test.mjs` (etwa Zeilen 272 bis 301) misst die echte Kategorie-Suche nur am PC (14px). Bei 390x844 misst er ein selbst gebautes `<input>` mit von Hand kopierten Klassen aus `Input.sizeToClass`. Der Test bliebe grün, wenn `sizeToClass Small`, `searchFieldTextClass` oder ein zusätzlicher `ClassName` am Handy nicht mehr 16px ergäben; genau das ist das Kernsymptom (iOS-Zoom, ADR 0005).
- Das echte Feld ist am Handy erreichbar: `mobileCategoryChip()` öffnet ein BottomSheet (`src/Client/Components/Ledger/View.fs:1278`, `filterChrome`), darin `CategoryMultiSelect.view` mit `PickerParts.searchField` = `Input.text { Size = Input.Small }` und Platzhalter "Kategorie suchen…". Andere Tests der Datei nutzen `mobileSheet()` schon so (Zeilen 392 bis 409).

**SUGGESTED_FIX:** Im btwhg-Test auf `mobilePage` den Kategorie-Chip öffnen (`mobileCategoryChip().click()`), die berechnete Schriftgröße von `mobileSheet().getByPlaceholder("Kategorie suchen…")` lesen, "16px" zusichern, das Sheet wie in den anderen Handy-Tests per Overlay-Klick schließen. Die synthetische Messung Small/Medium/Large darf als Zusatz für Medium und Large bleiben.

**ITERATION_HINT:** likely-fixable

## Outcome

Ursache: `.input-field` in `src/Client/styles.css` setzte `font-size: 14px` ohne Layer und überstimmte damit `text-base md:text-sm` aus `Input.sizeToClass Small`. Fix: die Zeile ist entfernt (mit Kommentar). Alle Nutzer von `input-field` (Input.text, Textarea, searchable Select, Dropdown-Button; keine weiteren im Client) tragen eine eigene `text-*`-Klasse, Medium und Large zusätzlich das Inline-fontSize 16, deshalb ändern sie sich nicht. `DateField.textSizeClass` ist entfernt, das Feld nutzt nur noch `Input.Small` mit `w-full`. `Input.usesInlineFontSize Small` bleibt false (bestehender Test).

Tests: neuer Playwright-Test in `scripts/ledger-filter-bar.test.mjs`. Er misst Small/Medium/Large per injiziertem Feld mit den Klassen aus `sizeToClass` bei 1440x900 und 390x844 (Zusatzabdeckung für Medium und Large) und das echte Kategorie-Suchfeld (Input.Small): am PC im Popover 14px, am Handy im BottomSheet 16px (Iteration 2, nach dem Verifier-Hinweis). Der bestehende Datumsfeld-Test misst weiter 14px/16px. Expecto-Test in `AuswahlTests.fs` ersetzt den alten textSizeClass-Test (Feld ohne Sonderweg, `.input-field` ohne font-size). Rot-Probe: mit wiederhergestelltem `font-size: 14px` ist die Handy-Messung des echten Felds rot (14px statt 16px), danach CSS zurückgesetzt.

Läufe (Iteration 2): dotnet build 0 Warnungen/0 Fehler; dotnet test 1391 bestanden, 7 übersprungen, 0 Fehler; npm run build ok; guard:classes ok; test:scripts 135/135 im Wiederholungslauf (ein Lauf davor zeigte 2 Ausreißer unter Last, der nächste Lauf grün; die Wächter laufen parallel und sind unter Last gelegentlich unruhig).
