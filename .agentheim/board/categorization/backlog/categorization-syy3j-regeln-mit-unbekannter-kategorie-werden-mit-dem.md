---
id: categorization-syy3j
title: "Regeln mit unbekannter Kategorie werden mit dem Platzhalternamen Unknown gespeichert"
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
Verletzte Regel: .claude/rules/query-safety.md, "Kein stiller Default"; .claude/rules/semantic-messaging.md, "Kein Befehls-DU ohne Aufrufer"

## What
Fundstelle: `src/Server/Api.fs:550`

`createRule` und `updateRule` (Zeilen 550, 551, 626) speichern "Unknown". Bei unbekannter Kategorie `RulesError.CategoryNotFound` liefern (heute nie erzeugt), bei YNAB-Fehler dessen Meldung.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
