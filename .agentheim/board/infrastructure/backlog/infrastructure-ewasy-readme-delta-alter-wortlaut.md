---
id: infrastructure-ewasy
title: README-Delta-Gate meldet alten Wortlaut, der nach einem replace anderswo in der README stehen bleibt
status: backlog
type: chore
context: infrastructure
created: 2026-11-08
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Ein README_DELTA ersetzt einen Bullet, aber andere Bullets, `## Ereignisse`, `## Key commands`, `## Key events`,
Open questions oder Verweise nennen dieselbe Regel noch im alten Wortlaut. Der Verifier lehnt ab. Das kam im
work-Lauf 2026-11-08 viermal vor (ledger-qvjvs, budgeting-e2z7v, ledger-at63w zweimal), vorher bei budgeting-eh8xe
und im Protokoll als "README-Bullets Push-Fehlschlag und Schreib-Sperre nennen dieselbe Regel und sind nicht
nachgezogen".

## What
`scripts/readme-delta-check.mjs` bekommt ein siebtes Kriterium: Begriffe in Backticks und Sätze in „…“, die ein
replace aus `expected` entfernt und die nicht im `body` stehen, dürfen sonst in der README nicht mehr vorkommen.
Sonst Exit 1 mit Fundstelle. Gegenbeweis in der zugehörigen `.test.mjs`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-08 aus dem `learn`-Lauf nach dem Review-Lauf e83f2d00..ad18f0c9, raw,
unrefined. Bis das Kriterium gebaut ist, gilt die geschärfte Prosa-Regel in CLAUDE.md, Abschnitt README-Deltas.
