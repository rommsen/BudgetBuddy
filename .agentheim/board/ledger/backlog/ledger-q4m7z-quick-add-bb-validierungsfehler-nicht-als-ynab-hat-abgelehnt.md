---
id: ledger-q4m7z
title: "Quick-Add: BB-Validierungsfehler nicht als \"YNAB hat abgelehnt\" melden"
status: backlog
type: bug
context: ledger
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [meldungen, quick-add]
related_adrs: []
related_research: []
prior_art: [budgeting-b5n8q]
---

## Why
Gefunden bei budgeting-b5n8q. `addManualTransaction` in `src/Server/Api.fs` (etwa Zeile 436) gibt Fehler aus `Validation.validateManualTransaction` als `YnabError.InvalidResponse` zurück. `ynabErrorText` macht daraus "YNAB hat abgelehnt: ...", obwohl BB selbst abgelehnt hat. Soll BB führend werden, muss die Meldung den Ablehnenden richtig nennen.

## What
Die Validierungsfehler als `YnabError.RefusedByBb` (eingeführt in budgeting-b5n8q) melden. Vorher prüfen, ob der Client oder Bob (`/bot/`) den Fall `InvalidResponse` auswertet, und welcher BC Quick-Add besitzt (ledger oder ynab-sync).

## Acceptance criteria
- [ ] API-Adapter-Test: ungültiger Quick-Add liefert `RefusedByBb`, der Nutzertext enthält kein "YNAB hat abgelehnt" und nennt alle Fehler.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.