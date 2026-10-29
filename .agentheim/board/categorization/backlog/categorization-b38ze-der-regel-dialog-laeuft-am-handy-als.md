---
id: categorization-b38ze
title: "Der Regel-Dialog läuft am Handy als zentriertes Modal mit Dropdown für die Kategorie"
status: backlog
type: refactor
context: categorization
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
Verletzte Regel: standards/frontend/styleguide.md §5 (Modal nur Desktop, mobile Auswahl über BottomSheet)

## What
Fundstelle: `src/Client/Components/Rules/View.fs:294`

Kategorie am Handy über `CategoryPicker.sheet` statt `searchableSelect` (Zeile 384), Formular als Sheet oder `Modal.fullScreen`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
