---
id: categorization-b2576
title: "Regel-Import läuft ohne Transaktion und ohne die Regel-Validierung"
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
Verletzte Regel: .claude/rules/query-safety.md, mehrere Schreibvorgänge in einer Transaktion; standards/backend/overview.md, "Validierung an der API-Grenze"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1

## What
Fundstelle: `src/Server/Api.fs:665 bis :690`

`importRules` fügt einzeln ein (halber Import bei Fehler) und prüft nur die Kompilierbarkeit, nicht `validateRuleCreateRequest`, CategoryId und CategoryName. `insertRules` in einer Transaktion, Validierung je Regel mit gesammelten Fehlern. Test: Rundlauf exportRules, importRules; eine ungültige Regel lehnt den ganzen Import ab.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
