---
id: banking-import-qrdh4
title: "Speicherzustand des Regel-Formulars im Zuweisungs-Dialog ist ein Bool-Flag"
status: backlog
type: refactor
context: banking-import
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
Verletzte Regel: .claude/rules/elmish-state.md, "Boolean Flags für Async State"

## What
Fundstelle: `src/Client/Components/SyncFlow/Types.fs:72`

`InlineRuleFormState.IsSaving: bool` durch einen RemoteData-Zustand ersetzen (gleiche Art wie im Regel-Dialog).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
