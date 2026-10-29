---
id: categorization-jnyk8
title: "Sammel-Kategorisieren, Aufteilen und Aufteilung entfernen haben keine API-Adapter-Tests"
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
Verletzte Regel: .agentheim/knowledge/contexts/categorization/README.md, "Review-Spur"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 und Kein Test für Punkt 6

## What
Fundstelle: `src/Server/Api.fs:1054, :1095, :1125; src/Tests/OrderIdMatcherTests.fs:352`

Je ein Happy Path für `bulkCategorize`, `splitTransaction` (CategoryId wird None) und `clearSplit` (Pending, Splits None) mit ByUser-Schritt. Der Test in OrderIdMatcherTests.fs:352 bildet clearSplit im Test nach; auf die echten Endpunkte umstellen oder löschen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
