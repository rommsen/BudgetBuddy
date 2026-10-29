---
id: categorization-aemz9
title: "Kategoriewechsel einer Regel speichert bei YNAB-Fehler den alten Kategorienamen"
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
Befund aus `/architecture-review categorization` am 2026-10-29, Schwere **hoch**.
Verletzte Regel: .claude/rules/query-safety.md, "Kein stiller Default"

## What
Fundstelle: `src/Server/Api.fs:628`

Antwortet YNAB beim Ändern der Kategorie nicht, speichert `updateRule` die neue `CategoryId` mit dem alten `CategoryName`; die Sync-Ansicht zeigt danach über `classifyTransactions` (src/Server/RulesEngine.fs:240) den falschen Namen. Die Änderung mit einer deutschen Fehlermeldung ablehnen. Vom Conductor am Code bestätigt.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
