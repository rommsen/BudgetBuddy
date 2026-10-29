---
id: categorization-e8e56
title: "Payee- und Memo-Regeln haben keine Gegenprobe gegen das jeweils andere Feld"
status: backlog
type: chore
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
Verletzte Regel: .claude/rules/test-doctrine.md, Kein Test für Punkt 6 (Gegenprobe)

## What
Fundstelle: `src/Tests/RulesEngineTests.fs:157`

"Classify matches Payee field" und "Memo field" (Zeile 176) blieben grün, wenn `getMatchText` (src/Server/RulesEngine.fs:181) den Combined-Text liefert. Je ein Negativfall: Payee-Regel trifft Memo-Text nicht und umgekehrt.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
