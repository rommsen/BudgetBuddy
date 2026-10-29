---
id: categorization-xn2sb
title: "Regel testen meldet ein ungültiges Muster als kein Treffer, und der Treffer-Fall ist ungetestet"
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
Verletzte Regel: standards/backend/overview.md, "konkrete Meldungen"; .claude/rules/test-doctrine.md, Kein Test für Punkt 6 (Gegenprobe)

## What
Fundstelle: `src/Server/Api.fs:699 und :716`

`testRule` macht aus einem Kompilierfehler `false`. Rückgabe `Result<bool, string>` mit der Kompiliermeldung. Tests: Contains "REWE" auf "REWE Markt" ergibt true, ein Exact-Fall mit Payee.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
