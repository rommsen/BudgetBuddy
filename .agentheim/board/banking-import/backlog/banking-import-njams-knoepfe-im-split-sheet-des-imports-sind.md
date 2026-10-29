---
id: banking-import-njams
title: "Knöpfe im Split-Sheet des Imports sind schmaler als 48 px und nutzen Textglyphen"
status: backlog
type: bug
context: banking-import
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
Verletzte Regel: standards/frontend/styleguide.md §4 (Touch-Targets), §5 (Icons)

## What
Fundstelle: `src/Client/Components/SyncFlow/Views/SplitSheet.fs:97`

"Zeile entfernen" 38 px breit, "Rest" 38 px hoch, Transfer 42 px (styles.css:2652, 2671, 2685), "✕" und "+" als Glyphen (auch Zeile 205). Auf 48 px und `Icons.x`/`Icons.plus`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
