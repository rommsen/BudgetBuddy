---
id: banking-import-bqhd3
title: "Der Kategorie-Picker im Import zeigt nie Vorschläge zum Payee"
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
Verletzte Regel: standards/frontend/styleguide.md §2 "Auswahl", Abschnitt Vorgeschlagen

## What
Fundstelle: `src/Client/Components/SyncFlow/Views/TransactionList.fs:315`

`suggestedCats` ist fest `[]`. Vorschläge aus Regeln oder Payee-Historie übergeben, spätestens beim Umzug auf `CategoryPicker` (`Suggested`).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
