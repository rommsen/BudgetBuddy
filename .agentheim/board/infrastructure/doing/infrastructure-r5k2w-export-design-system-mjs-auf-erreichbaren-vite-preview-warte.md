---
id: infrastructure-r5k2w
title: "export-design-system.mjs: auf erreichbaren vite preview warten statt fester 5 Sekunden"
status: doing
type: bug
context: infrastructure
created: 2026-10-29
depends_on: []
blocks: []
tags: [harness, flake]
related_adrs: []
related_research: [harness-audit]
prior_art: [infrastructure-dvy29]
---

## Why
`startPreviewServer` in `scripts/export-design-system.mjs` wartet auf die Zeile "Local:" von `vite preview` höchstens 5 Sekunden und macht danach ohne Prüfung weiter. Unter Last (mehrere gleichzeitige Gate-Läufe) ist der Server dann noch nicht erreichbar, `export-design-system.test.mjs` scheitert mit `ERR_CONNECTION_REFUSED`. Ein Fehler-Exit von vite nach Ablauf der 5 Sekunden wird zudem nicht mehr beachtet. verify.sh umgeht das mit einem Lock um test:scripts (infrastructure-dvy29); der Fehler selbst besteht weiter.

## What
Wie in `scripts/screenshot-route.mjs` per echtem TCP-Connect (waitForPort) auf den Port warten, mit großzügigem Timeout, und laut scheitern, wenn der Server nicht kommt oder vorher endet.

## Acceptance criteria
- [ ] Der Export wartet, bis der Preview-Port einen TCP-Connect annimmt, und wirft nach Ablauf des Timeouts einen sprechenden Fehler.
- [ ] Ein vorzeitiges Ende von vite preview führt zu einem Fehler statt zu einem späteren Verbindungsfehler.
- [ ] Regressionstest, der ohne den Fix rot ist (z.B. verzögert startender Server).