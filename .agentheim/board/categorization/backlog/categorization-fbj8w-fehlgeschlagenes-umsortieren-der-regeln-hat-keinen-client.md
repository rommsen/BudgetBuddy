---
id: categorization-fbj8w
title: "Fehlgeschlagenes Umsortieren der Regeln hat keinen Client-update-Test"
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
Verletzte Regel: .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1; .agentheim/board/categorization/done/cat-001-rule-reorder-ui.md, AC "Bei Fehler: Toast + Reload"

## What
Fundstelle: `src/Client/Components/Rules/State.fs:187`

`update`-Test für `RulesReordered (Error _)`: Rules wird Loading, Cmd lädt neu, Toast. Dazu der No-op-Zweig von `MoveRule` an der obersten Regel (State.fs:164). Beide berühren den Remoting-Proxy nicht.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
