---
id: ledger-yhemh
title: "Neue Buchung am Handy: Memo steht neben dem Datum statt darunter"
status: backlog
type: bug
context: ledger
created: 2026-11-13
completed:
depends_on: []
blocks: []
tags: [captured, frontend, layout]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Romans Test am Handy (2026-11-13, Galaxy S25 Ultra, 412 px): Im Sheet „Neue Buchung“ stehen Datum (mit „Heute“,
„Gestern“ und Kalender) und Memo nebeneinander. Das Raster ist fest zweispaltig (`grid grid-cols-2` in
`src/Client/Components/Ledger/View.fs`, Erfassung aus ledger-at63w). „Memo steht neben Datum, das sollte nicht sein.“

## What
Am Handy Memo unter dem Datum; ab breiterem Bildschirm (Dialog am PC) bleibt es nebeneinander, sofern die Skizze
das so zeigt.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured raw, unrefined. Regressionstest als Zustand im Layout-Sweep (Doktrin Punkt 8).
