---
id: categorization-w32b3
title: "Fehlermeldungen der Regeln und Splits erscheinen beim Nutzer auf Englisch"
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
Verletzte Regel: standards/backend/overview.md, "konkrete Meldungen auf Deutsch"

## What
Fundstelle: `src/Server/Api.fs:49`

`rulesErrorToString` (Zeilen 49 bis 53), Split-Fehler in `splitTransaction` (1107 bis 1111), `compileRule` (src/Server/RulesEngine.fs:147) und die Regel-Validierung (src/Server/Validation.fs:13, 20, 26) auf Deutsch umstellen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
