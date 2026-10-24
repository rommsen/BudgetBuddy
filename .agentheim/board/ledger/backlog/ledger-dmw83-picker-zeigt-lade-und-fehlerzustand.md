---
id: ledger-dmw83
title: Kategorie-Picker zeigt "Kategorien werden geladen" bzw. "Kategorien konnten nicht geladen werden" mit "Erneut versuchen", statt einer leeren Liste ohne Erklärung
status: backlog
type: bug
context: ledger
created: 2026-10-24
completed:
depends_on: []
blocks: []
tags: [captured, kategorie, picker, frontend]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Live-Befund 2026-10-24: Waren die Kategorien nicht geladen, zeigte der Picker nur "Ohne
Kategorie". Ein Ladefehler sah aus wie "es gibt keine Kategorien", und es gab keinen Weg,
es erneut zu versuchen.

## What
Bei `Categories = Loading` zeigt der Kategorie-Picker "Kategorien werden geladen", bei
`Failure` "Kategorien konnten nicht geladen werden" mit "Erneut versuchen". Mit
Regressionstest.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-24: raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Berührt vermutlich auch den DS-Baustein `CategoryPicker` (Props brauchen
einen Ladezustand) und damit design-system und `/styleguide`.
