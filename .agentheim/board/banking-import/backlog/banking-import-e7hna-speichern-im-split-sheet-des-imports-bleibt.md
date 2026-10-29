---
id: banking-import-e7hna
title: "Speichern im Split-Sheet des Imports bleibt während des Aufrufs aktiv, ein Doppeltipp schickt zwei Aufrufe"
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
Verletzte Regel: standards/frontend/overview.md, Schaltflächen zeigen den Ladezustand und sind gesperrt

## What
Fundstelle: `src/Client/Components/SyncFlow/Views/SplitSheet.fs:173`

`SaveSplits` (src/Client/Components/SyncFlow/State.fs:698) setzt keinen Speicherzustand. RemoteData-Speicherzustand in `SplitEditState`, DS-Button mit `IsLoading`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
