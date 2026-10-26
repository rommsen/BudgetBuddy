---
id: ledger-vqvq6
title: "Playwright-Wächter ledger-split-cashback wartet auf die Settings-Antwort statt fest 400 ms"
status: backlog
type: chore
context: ledger
created: 2026-10-26
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Der Verifier von ledger-s8t3s hat angemerkt: `scripts/ledger-split-cashback.test.mjs` wartet mit einem festen `waitForTimeout` von 400 ms (`settle`) auf die Settings-Antwort. Das kann je nach Last wackeln.

## What
Das feste Warten durch ein Warten auf die Antwort (z. B. `waitForResponse` auf `getSettings`) oder auf ein sichtbares Element ersetzen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-26, raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Herkunft: nicht blockierender Hinweis des Verifiers von ledger-s8t3s.
