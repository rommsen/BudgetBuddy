---
id: categorization-q3960
title: "Ein gesetzter PayeeOverride einer Regel lässt sich nicht mehr entfernen"
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
Verletzte Regel: .agentheim/knowledge/contexts/categorization/README.md, Ubiquitous language "PayeeOverride"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1

## What
Fundstelle: `src/Server/Api.fs:597`

`Option.orElse existing.PayeeOverride` behält den alten Wert, weil der Client bei leerem Feld `None` schickt (src/Client/Components/Rules/State.fs:258). `RuleUpdateRequest.PayeeOverride` muss unverändert, setzen und entfernen unterscheiden. Regressionstest auf API-Adapter-Ebene: Regel mit Override, Update mit leerem Override, danach ohne Override; dazu der intakt-Fall (Aktivieren lässt den Override stehen). Vom Conductor am Code bestätigt.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
