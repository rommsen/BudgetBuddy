---
id: ledger-q1hw2
title: "Buchungs-Detail: Herkunft, Historie und \"noch nicht in YNAB\" (Sheet am Handy, Seitenpanel am PC)"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-b1xwe, design-system-001]
blocks: []
tags: [frontend, kassenbuch, scheibe]
related_adrs: [0013, 0021]
related_research: []
prior_art: [ledger-q5w9c, ledger-fsz0a]
---

## Why
Das ist, was nur BB kann und YNAB nicht hat: woher eine Buchung stammt, was sich wann und
woher geändert hat, und ob sie überhaupt schon in YNAB angekommen ist. Heute steckt die
Historie in einem eigenen Sheet, das nur am Handy gedacht ist; am PC fehlt eine Form, die
neben der Liste stehen bleibt.

## What
Ein Detail je Buchung, am Handy als BottomSheet, am PC als **Seitenpanel rechts**, damit
die Liste sichtbar bleibt und man mit den Pfeiltasten zur nächsten Buchung geht.

- Kopf: Betrag, Payee, Konto, Datum.
- Felder lesend: Kategorie oder Splits, Memo. (Bearbeiten hängt an der v2-Entscheidung.)
- **Herkunft** als Badge, **Historie** als Zeitleiste mit den sprechenden Sätzen aus
  `getEntryHistory` (ADR 0013), älteste zuerst.
- **"Noch nicht in YNAB"** als Hinweis mit Grund, wenn die Buchung keine
  YNAB-Verknüpfung trägt (Push ausstehend oder gescheitert, ADR 0021). Ein "Erneut
  senden" wäre schreibend und gehört nicht in diese Scheibe.
- Das bisherige Historie-Sheet geht darin auf.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief: `docs/ux/kassenbuch/briefs.md`,
Ablauf 7. Roman hat zu Ablauf 7 noch nicht gesagt, was er am Handy anders erwartet als am
PC; bei der Verfeinerung nachholen.
