---
id: categorization-mq0pf
title: "Der Overflow-Knopf der Regelseite ist 32 px groß und hat keine Beschriftung"
status: backlog
type: bug
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
Verletzte Regel: standards/frontend/styleguide.md §4 und §5 (Button, Icons)

## What
Fundstelle: `src/Client/Components/Rules/View.fs:569`

Textglyphe "⋮" ohne `ariaLabel`. DS-Button mit Icon, `ariaLabel` "Weitere Aktionen" und `TouchTargets.minSize`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
