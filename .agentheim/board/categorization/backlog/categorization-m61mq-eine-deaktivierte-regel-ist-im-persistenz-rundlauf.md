---
id: categorization-m61mq
title: "Eine deaktivierte Regel ist im Persistenz-Rundlauf nicht abgesichert"
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
Verletzte Regel: .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1

## What
Fundstelle: `src/Tests/PersistenceTypeConversionTests.fs:81`

Rundlauf mit `Enabled = false` (src/Server/Persistence.fs:356 und 400), dazu PayeeOverride und Priority, damit eine abgeschaltete Regel nach einem Neustart nicht wieder greift.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
