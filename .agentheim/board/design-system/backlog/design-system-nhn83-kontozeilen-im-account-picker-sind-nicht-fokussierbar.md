---
id: design-system-nhn83
title: "Kontozeilen im Account-Picker sind nicht fokussierbar und kürzen lange Namen nicht"
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
Verletzte Regel: standards/frontend/styleguide.md §2 "Auswahl" (PickerParts.row mit truncate), Zugänglichkeit

## What
Fundstelle: `src/Client/DesignSystem/BottomSheet.fs:586`

`Html.button` mit `PickerParts.row`/`PickerParts.name`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
