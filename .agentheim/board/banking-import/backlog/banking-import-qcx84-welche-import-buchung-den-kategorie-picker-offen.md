---
id: banking-import-qcx84
title: "Welche Import-Buchung den Kategorie-Picker offen hat, liegt außerhalb des Models"
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
Verletzte Regel: CLAUDE.md MVU (alle Zustandsänderungen über update); .claude/rules/elmish-state.md

## What
Fundstelle: `src/Client/Components/SyncFlow/Views/TransactionList.fs:66`

`React.useState` statt Model; ein Session-Wechsel kann den Picker nicht schließen. `CategoryPickerFor: TransactionId option` ins SyncFlow-Model, wie `ActivePicker` im Split.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
