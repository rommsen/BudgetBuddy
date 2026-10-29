---
id: categorization-sh0cp
title: "Der Regel-Export startet den Download per JS.eval direkt im update"
status: backlog
type: refactor
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
Verletzte Regel: .claude/rules/elmish-state.md, "Side Effects NUR über Cmd"

## What
Fundstelle: `src/Client/Components/Rules/State.fs:334`

Download in eine Funktion ziehen und über `Cmd.ofEffect` oder `Cmd.OfFunc.attempt` ausführen, ohne `eval`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
