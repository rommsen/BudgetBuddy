---
id: categorization-pk04j
title: "Sammel-Kategorisierung überträgt die Kategorie nicht auf Buchungen mit derselben Order-Id"
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
Verletzte Regel: .agentheim/knowledge/contexts/categorization/README.md, "Order-ID-Matching", Key command PropagateOrderId

## What
Fundstelle: `src/Server/Api.fs:1077`

`categorizeTransaction` propagiert (Zeile 1019), `bulkCategorize` nicht; unbekannte `txIds` fallen still weg (Zeile 1090). Nach dem Setzen `OrderIdMatcher.propagateInSession` mit gemeinsamem `now`, unbekannte Ids melden.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
