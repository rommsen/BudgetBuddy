---
id: infrastructure-v3tqk
title: "Wächter ledger-read-retry flackert bei getLedgerPayeeSuggestions unter Last"
status: backlog
type: bug
context: infrastructure
created: 2026-11-01
completed:
depends_on: []
blocks: []
tags: [tests, playwright, flackern]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`scripts/ledger-read-retry.test.mjs:223` ("getLedgerPayeeSuggestions (/ledger) übersteht einen Abbruch") war in einem vollen `./verify.sh`-Lauf rot ("keine Wiederholung" nach 5 s) und ohne Codeänderung im nächsten grün. Das Gate wird dadurch unzuverlässig. Die Vermutung ist ein Lastrennen gegen die feste Frist, nachgewiesen ist sie nicht.

## What
Die Ursache klären und den Test auf eine Bedingung warten lassen statt auf eine feste Frist (Test-Doktrin Punkt 5).

## Acceptance criteria
- [ ] Die Ursache steht im Outcome, mit Beleg.
- [ ] Der Test bleibt in zehn vollen `npm run test:scripts`-Läufen grün.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
