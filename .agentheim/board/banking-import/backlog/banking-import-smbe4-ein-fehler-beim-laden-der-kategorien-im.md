---
id: banking-import-smbe4
title: "Ein Fehler beim Laden der Kategorien im Import lässt alle Kategorie-Picker stumm leer"
status: backlog
type: bug
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
Verletzte Regel: .claude/rules/elmish-state.md, Failure anzeigen, RemoteData

## What
Fundstelle: `src/Client/Components/SyncFlow/State.fs:851`

Import-, Split- und Quick-Add-Picker beziehen ihre Daten von hier (src/Client/View.fs:117). `Categories` als RemoteData, Failure im Picker oder als Toast.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
