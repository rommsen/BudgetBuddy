---
id: categorization-p7s85
title: "Ergebnis von Regel testen ist ein Anzeigetext mit Emoji, den die View wieder zerlegt"
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
Verletzte Regel: .claude/rules/elmish-state.md, RemoteData-Muster

## What
Fundstelle: `src/Client/Components/Rules/Types.fs:16`

`TestResult: string option` wird in View.fs:483-493 per `StartsWith("✅")` geparst, ein Ladezustand fehlt. `TestResult: RemoteData<bool>`, die View entscheidet per Match über Farbe und Icon.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
