---
id: infrastructure-w7d2c
title: "YnabClient.createTransactions hat keinen Aufrufer mehr und fliegt raus"
status: doing
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

Dazu gehören zwei nachgebaute Tests in `src/Tests/YnabClientTests.fs`, die Logik aus `createTransactions` im Test nachbilden statt Produktionscode aufzurufen (Test-Doktrin Punkt 6): "Simulate the conversion logic from createTransactions" (um Zeile 667) und "Replicate the filter logic from createTransactions" (um Zeile 853). Sie fliegen raus, wenn dasselbe Verhalten schon über `prepareTransactions` getestet ist (Teil 1, ledger-qvt84: Zukunftsdatum, ImportId). Fehlt dort ein Fall, wird er als Test gegen `prepareTransactions` ergänzt statt den Nachbau zu behalten.

## Acceptance criteria
- [ ] `grep -rn 'createTransactions' src` trifft keinen Produktionscode mehr; verbleibende Treffer sind nur Kommentare, die den heutigen Weg (`prepareTransactions`, `sendPrepared`) richtig beschreiben.
- [ ] Die beiden nachgebauten Tests sind entfernt oder durch Tests gegen `prepareTransactions` ersetzt; das Outcome nennt für jeden, welcher bestehende oder neue Test sein Verhalten abdeckt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Reines Aufräumen, kein fachliches Verhalten ändert sich. Kein Regressionstest nötig.
- Läuft im Nachtlauf parallel zu budgeting-dgrp7 und ledger-z71ka; berührt nur `src/Server/YnabClient.fs` und `src/Tests/YnabClientTests.fs`.
