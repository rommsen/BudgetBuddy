---
id: infrastructure-w7d2c
title: "YnabClient.createTransactions hat keinen Aufrufer mehr und fliegt raus"
status: backlog
type: chore
context: infrastructure
created: 2026-11-01
completed:
depends_on: []
blocks: []
tags: [aufraeumen, ynab]
related_adrs: []
related_research: []
prior_art: [infrastructure-p747v]
---

## Why
Beim Umstellen auf den Berliner Kalendertag (infrastructure-p747v) fiel auf: `createTransactions` in `src/Server/YnabClient.fs` wird im Repo nirgends aufgerufen. `importToYnab` und `forceImportDuplicates` nutzen `prepareTransactions` und `sendPrepared` einzeln. Der Kommentar über der Funktion behauptet noch etwas anderes.

## What
Prüfen, dass es keinen Aufrufer gibt, dann die Funktion entfernen und die Kommentare nachziehen, die sie nennen (`YnabClient.fs` beim `PrepareResult`, `YnabClientTests.fs`).

## Acceptance criteria
- [ ] `grep -rn 'createTransactions' src` trifft keinen Produktionscode mehr.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.