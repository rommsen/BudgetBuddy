---
id: banking-import-cf7b3
title: "Aufgeteilte Buchungen landen im Import-Filter unter ohne Kategorie"
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
Verletzte Regel: .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1; .agentheim/knowledge/contexts/categorization/README.md, Szenarien und Tests

## What
Fundstelle: `src/Client/Components/SyncFlow/Views/TransactionList.fs:22`

`filterTransactions` hat keinen Fall für CategoryId None mit Splits Some, Badge und Chip zeigen sie seit ynab-004 als erledigt. Sollverhalten mit Roman klären, dann Testfall.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
