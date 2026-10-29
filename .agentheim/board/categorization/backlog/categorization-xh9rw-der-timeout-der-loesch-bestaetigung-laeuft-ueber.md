---
id: categorization-xh9rw
title: "Der Timeout der Lösch-Bestätigung läuft über Cmd.OfAsync.perform"
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
Verletzte Regel: .claude/rules/elmish-state.md, "Cmd.OfAsync.perform"

## What
Fundstelle: `src/Client/Components/Rules/State.fs:88`

Über `Cmd.OfAsync.either` oder `Cmd.OfPromise` mit Fehlerfall auslösen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
