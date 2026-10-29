---
id: categorization-k4xp9
title: "Unbekannte PatternType- und TargetField-Werte aus der Datenbank werden still zu Regex bzw. Combined"
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
Verletzte Regel: .claude/rules/query-safety.md, "Kein stiller Default in Konvertern"

## What
Fundstelle: `src/Server/Persistence.fs:330 und :341`

`patternTypeFromString` und `targetFieldFromString` sollen bei unbekanntem Wert mit deutscher Meldung fehlschlagen statt einen Default zu liefern.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
