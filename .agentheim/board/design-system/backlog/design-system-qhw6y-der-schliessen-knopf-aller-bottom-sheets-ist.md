---
id: design-system-qhw6y
title: "Der Schließen-Knopf aller Bottom Sheets ist 32 px groß"
status: backlog
type: bug
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
Verletzte Regel: standards/frontend/styleguide.md §4 (Touch-Targets mindestens 48 px)

## What
Fundstelle: `src/Client/DesignSystem/BottomSheet.fs:186`

`.sheet-close` 32 mal 32 px (styles.css:1678), auch Zeilen 318 und 553. Trefferfläche über Padding oder Pseudo-Element auf 48 px, Optik bleibt.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
