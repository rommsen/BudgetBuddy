---
id: categorization-qjmrm
title: "Ein Netzwerkfehler beim Regel testen erscheint als ungültiges Muster"
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
Verletzte Regel: .claude/rules/elmish-state.md, Failure korrekt anzeigen

## What
Fundstelle: `src/Client/Components/Rules/State.fs:238`

Die Ausnahme nicht auf `RulesError.InvalidPattern` abbilden, sondern wie in den übrigen Zweigen auf einen Datenbank- oder Netzwerkfall.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
