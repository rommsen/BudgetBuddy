---
id: design-system-yw9bs
title: "Die Chips Vorgeschlagen und Zuletzt verwendet im Kategorie-Sheet sind etwa 33 px hoch"
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
Verletzte Regel: standards/frontend/styleguide.md §4 (Touch-Targets)

## What
Fundstelle: `src/Client/DesignSystem/BottomSheet.fs:71`

`.chip` padding 8 px, 13 px Schrift (styles.css:1728); `min-height: 44px` wie bei `optionRow`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
