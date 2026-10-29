---
id: categorization-h88qx
title: "Ablehnung eines kaputten Regex beim Anlegen und Ändern einer Regel ist ungetestet"
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
Verletzte Regel: .agentheim/knowledge/contexts/categorization/README.md, Aggregate Rule (bei Regex kompilierbar); .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 4

## What
Fundstelle: `src/Server/Api.fs:535 und :604`

API-Adapter-Test: `createRule` mit "[kaputt" und PatternType Regex liefert `InvalidPattern` und legt nichts an; dasselbe für `updateRule` mit geändertem Pattern.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
