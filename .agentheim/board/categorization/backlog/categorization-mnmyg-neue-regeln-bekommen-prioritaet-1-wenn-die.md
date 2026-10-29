---
id: categorization-mnmyg
title: "Neue Regeln bekommen Priorität 1, wenn die Regelliste nicht geladen ist"
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
Verletzte Regel: .agentheim/knowledge/contexts/categorization/README.md, Aggregate Rule "sinnvolle Priority"

## What
Fundstelle: `src/Client/Components/Rules/State.fs:260`

Der Client berechnet die Priorität selbst; ist `Rules` nicht `Success`, kollidiert die neue Regel und landet unten. Priorität serverseitig in `createRule` vergeben oder Anlegen sperren, solange `Rules` nicht `Success` ist.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
