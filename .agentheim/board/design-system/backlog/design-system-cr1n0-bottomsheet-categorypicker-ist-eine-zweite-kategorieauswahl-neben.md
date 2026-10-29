---
id: design-system-cr1n0
title: "BottomSheet.categoryPicker ist eine zweite Kategorieauswahl neben CategoryPicker, ohne Tastatur und ohne Ohne Kategorie"
status: backlog
type: refactor
context: design-system
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Befund aus `/architecture-review categorization` am 2026-10-29, Schwere **mittel**.
Verletzte Regel: standards/frontend/styleguide.md §2 "Auswahl" (Einfachauswahl über CategoryPicker.sheet/popover, Pfeile und Enter), §5

## What
Fundstelle: `src/Client/DesignSystem/BottomSheet.fs:439 und :410`

Es fehlen "Ohne Kategorie", `LoadState`, "Versteckt (n)", Tastatur und am PC das Popover; die Zeilen sind `div`s mit `onClick`. Aufrufer src/Client/Components/SyncFlow/Views/TransactionList.fs:339, SplitSheet.fs:223 und src/Client/Views/QuickAddPage.fs:256 auf `CategoryPicker` umstellen, danach `categoryPicker*` entfernen; die dreifache Abbildung auf `CategoryPickerOption` (TransactionList.fs:322, SplitSheet.fs:129, QuickAddPage.fs:99) fällt dabei zusammen. Für den Menschen am Bildschirm: reicht am PC das 520 px breite Sheet unten, oder ist das Popover nötig?

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
