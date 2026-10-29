---
id: categorization-a510t
title: "Bei gleicher Priorität ist offen, welche Regel gewinnt, und die Reihenfolge ist ungetestet"
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
Verletzte Regel: .agentheim/knowledge/contexts/categorization/README.md, "Priority: die erste passende Regel gewinnt"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1

## What
Fundstelle: `src/Server/Persistence.fs:364`

`ORDER BY priority DESC` ohne Zweitschlüssel, `importRules` und `createRule` erzeugen Gleichstände. Festen Zweitschlüssel oder Ablehnung doppelter Priorität. In-Memory-SQLite-Test: `updatePriorities [a; b; c]`, `getAllRules` liefert a, b, c, `classifyTransactions` wählt a.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
