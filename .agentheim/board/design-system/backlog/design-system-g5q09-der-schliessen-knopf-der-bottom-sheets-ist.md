---
id: design-system-g5q09
title: "Der Schließen-Knopf der Bottom Sheets ist ein eigenes Inline-SVG"
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
Verletzte Regel: standards/frontend/styleguide.md §5 Icons (keine rohen svg inline)

## What
Fundstelle: `src/Client/DesignSystem/BottomSheet.fs:110`

`closeIcon` durch `Icons.x SM Default` ersetzen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
