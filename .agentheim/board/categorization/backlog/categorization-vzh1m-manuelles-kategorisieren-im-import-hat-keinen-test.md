---
id: categorization-vzh1m
title: "Manuelles Kategorisieren im Import hat keinen Test auf API-Adapter-Ebene"
status: backlog
type: chore
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
Verletzte Regel: .agentheim/knowledge/contexts/categorization/README.md, "Review-Spur" und Szenarien und Tests; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1

## What
Fundstelle: `src/Server/Api.fs:974`

Für `categorizeTransaction` mit echter Sitzung in `testSequenced`: Status ManualCategorized, `SuggestedByOrderId` geleert, ein ByUser-Schritt, Propagation auf das Geschwister mit gleicher Order-Id im Rückgabewert und im SyncSessionManager. Die geteilte In-Memory-DB darf dabei keinen `ynab_token` tragen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
