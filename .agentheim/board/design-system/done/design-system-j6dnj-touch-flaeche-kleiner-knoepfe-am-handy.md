---
id: design-system-j6dnj
title: "Touch-Fläche der kleinen Knöpfe am Handy auf mindestens 48 px bringen: die DS-Größe Button.Small wird mobil 48 px hoch (Split-Editor: Barabhebung, Zeile hinzufügen und alle übrigen Small-Knöpfe)"
status: done
type: bug
context: design-system
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: []
tags: [captured, touch, button, mobile]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Der Verifier von ledger-s8t3s hat gemessen: "Barabhebung" und "Zeile hinzufügen" im Split-Editor des Kassenbuchs sind am Handy nur 36 px hoch. Der Styleguide (`standards/frontend/styleguide.md` §4, `TouchTargets.minSize`) verlangt mobil mindestens 48 px für alle Tappables.

## What
Die Höhe kommt aus der DS-Größe `Button.Small` in `src/Client/DesignSystem/Button.fs` (`text-xs px-3 py-1.5 min-h-[36px] md:min-h-0`). Entscheidung Roman (2026-10-27): die Korrektur geht in die DS-Größe selbst, nicht nur in den Split-Editor. Mobil wird Small mindestens 48 px hoch, ab `md` bleibt es wie heute (`md:min-h-0`). Das betrifft alle Verwendungen von `Button.Small`, auch `Button.iconButton` (Bearbeiten, Löschen) und die rund 26 Stellen in DesignSystem, Kassenbuch und Import.

Icon-only-Knöpfe (`iconButton`) brauchen am Handy auch 48 px Breite, sonst bleibt die Fläche schmal.

## Acceptance criteria
- [ ] `Button.Small` trägt mobil eine Mindesthöhe von 48 px und ab `md` keine (Klassen-String in `Button.fs`; Unit-Test oder Browser-Wächter prüft die Klassen).
- [ ] Icon-only-Knöpfe aus `Button.iconButton` sind mobil mindestens 48 × 48 px.
- [ ] Browser-Wächter bei 390 px Breite: "Barabhebung" und "Zeile hinzufügen" im Split-Editor des Kassenbuchs messen per `getBoundingClientRect` jeweils mindestens 48 px Höhe.
- [ ] Browser-Wächter bei Desktop-Breite (≥ 768 px): dieselben Knöpfe behalten ihre bisherige Höhe (kein Anwachsen auf 48 px).
- [ ] Alle Verwendungen von `Button.Small` sind am Handy durchgesehen; Stellen, an denen 48 px das Layout sprengen (Tabellenzeilen, Modals, Badges), sind im Outcome genannt und behoben oder als Folge-Task gemeldet.
- [ ] Der lebende Styleguide (`/styleguide`) zeigt die Small-Größe weiterhin; die bestehenden Wächter bleiben grün (`npm run test:scripts`).

## Notes
Herkunft: nicht blockierender Hinweis des Verifiers von ledger-s8t3s. Verfeinert am 2026-10-27.
Verwandt: design-system-y2bdj (Styleguide zeigt `SplitEditor.lineActions`), design-system-bym4m (Split-Ziel-Picker). Alle drei liegen im Split-Editor; j6dnj ändert nur `Button.fs` und kann parallel laufen.

## Outcome

`Button.Small` trägt jetzt `min-h-[48px] md:min-h-0` (vorher `min-h-[36px]`). Neu in `src/Client/DesignSystem/Button.fs`: `sizeToClass` ist öffentlich (testbar), und `iconOnlyClass size text` liefert für Small ohne Text `min-w-[48px] md:min-w-0`, damit `Button.iconButton`, `editButton` und `deleteButton` mobil 48 × 48 px groß sind. `view` hängt die Klasse als eigenes Binding `iconOnly` an (ein zusammengesetzter `sizeClass`-String hätte den Tailwind-Klassen-Wächter als dynamisch ungelöst gemeldet).

Tests: `src/Tests/ButtonSizeTests.fs` (4 Expecto-Tests, Klassenstrings), `scripts/touch-target-small-button.test.mjs` (2 Playwright-Tests am echten Kassenbuch-Split-Editor mit gemockten Ledger-Antworten). Mobil bei 390 px (isMobile, hasTouch): "Zeile hinzufügen" und "Barabhebung" messen per `getBoundingClientRect` mindestens 48 px. Desktop bei 1440 px: beide unter 44 px (wachsen nicht). Der Mobil-Test war mit `min-h-[36px]` rot und ist mit 48 px grün.

Durchgesehene `Button.Small`-Stellen (Quelltext-Review, plus die grünen Layout-Wächter in `test:scripts`):
- `DesignSystem/SplitEditor.fs`: Übertrag-Knopf (Icon-only, Zeile ~128), "Rest"-Knopf (~155), Entfernen-Knopf (Icon-only, ~163), `addLineButton` (~198, im selben Lauf von design-system-y2bdj entfernt), `lineActions` mit "Zeile hinzufügen" und "Barabhebung" (~216, ~224). Zeilen liegen in `flex items-center gap-2` bzw. `flex-wrap`, wachsen nur in der Höhe, kein Umbruchproblem. Die Datei gehört y2bdj, ich habe sie nicht angefasst.
- `DesignSystem/MultiSelect.fs`: "Geprüft", Kategorie, zwei Lena-Knöpfe (Zeilen 245 bis 254) in der Auswahlleiste. Wachsen nur in der Höhe, die Wächter ledger-selection und ledger-lena-batch bleiben grün.
- `DesignSystem/Detail.fs` (~177): Aktionsknopf im Hinweiskasten, nur höher.
- `Components/Ledger/View.fs` (~541): Wiederholen-Knopf im Hinweis, nur höher.
- `Components/SyncFlow/Views/TransactionList.fs` (~108): "Importieren" in der Aktionsleiste, nur höher.
- Die Komfortfunktionen `iconButton`, `editButton`, `deleteButton` haben keine Aufrufer außerhalb von `Button.fs`.
Kein Layout-Bruch gefunden, daher kein Folge-Task. Nicht betroffen (eigene Größen, nicht `Button.Small`): `Input.Small`, `Badge.Small`, der kompakte Picker-Knopf in `PickerParts.fs` und der Kalender-Knopf in `DateField.fs`, alle mit eigenem `min-h-[36px]`.

Pflichtläufe im Worktree: `dotnet build` 0 Warnungen, 0 Fehler. `dotnet test` 2430 bestanden, 7 übersprungen (YNAB-Integration ohne .env), 0 Fehler. `npm run build` grün. `npm run guard:classes` grün. `npm run guard:tokens` grün. `npm run test:scripts` 280/280 grün.
