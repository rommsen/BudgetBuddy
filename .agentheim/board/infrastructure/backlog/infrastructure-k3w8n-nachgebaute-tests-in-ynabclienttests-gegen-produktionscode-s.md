---
id: infrastructure-k3w8n
title: "Nachgebaute Tests in YnabClientTests gegen Produktionscode stellen"
status: backlog
type: chore
context: infrastructure
created: 2026-11-05
completed:
depends_on: []
blocks: []
tags: [aufraeumen, tests, ynab]
related_adrs: []
related_research: []
prior_art: [infrastructure-w7d2c]
---

## Why
Beim Entfernen von `createTransactions` (infrastructure-w7d2c) fiel auf: weitere Tests in `src/Tests/YnabClientTests.fs` bilden Produktionslogik im Test nach, statt sie aufzurufen (Test-Doktrin Punkt 6). Betroffen sind in "Transaction Conversion Tests" etwa "uses PayeeOverride when provided", "memo with reference fits within 300 char limit", "whitespace in memo is compressed", "filters out skipped transactions", "uncategorized transactions pass filter" und "transaction dated today is not filtered out", dazu die "Milliunits Conversion Tests", die nur Arithmetik im Test rechnen.

## What
Jeden dieser Tests prüfen: ist das Verhalten schon gegen `prepareTransactions`, `buildTransactionRequest`, `buildMemoWithReference`, `resolvePayeeName` oder `Shared.Budgeting.toMilliunits` getestet, fliegt er raus; sonst wird er auf die Produktionsfunktion umgestellt.

## Acceptance criteria
- [ ] Kein Test in `YnabClientTests.fs` bildet Produktionslogik nach, ohne Produktionscode aufzurufen.
- [ ] Das Outcome nennt je entferntem Test den Test, der sein Verhalten abdeckt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.