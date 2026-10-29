---
id: banking-import-e4e1z
title: "Die Auswahl aller sichtbaren Import-Zeilen nutzt eine ungetestete Kopie des Filters"
status: backlog
type: refactor
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
Verletzte Regel: .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 2

## What
Fundstelle: `src/Client/Components/SyncFlow/State.fs:42`

Private zweite Kopie von `filterTransactions` (State.fs:439, 476), getestet ist nur die View-Kopie (src/Tests/SyncFlowViewTests.fs:228). Auf die getestete Funktion umstellen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
