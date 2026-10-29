---
id: infrastructure-q7v2m
title: "Übrige Quelltext-Tests auf View-Verdrahtung durch update- oder Browser-Tests ersetzen"
status: backlog
type: refactor
context: infrastructure
created: 2026-10-29
depends_on: [infrastructure-hdf7j]
blocks: []
tags: [harness, tests]
related_adrs: []
related_research: []
prior_art: [infrastructure-hdf7j]
---

## Why
Bericht D (Abschnitt 3) zählt rund 140 Tests, die View-Quelltext per Teilstring lesen, vor allem in `src/Tests/LedgerViewTests.fs`, `LedgerDetailTests.fs`, `LedgerSplitEditorClientTests.fs`, `LedgerCategoryChangeClientTests.fs`, `LedgerFilterButtonTests.fs` und `LedgerKeyboardTests.fs`. Sie fixieren Bezeichner und Formatierung: ein gleichwertiges Refactoring macht sie rot, ein Fehler mit gleichem Text lässt sie grün. infrastructure-hdf7j hat nur die namentlich genannten Fälle bearbeitet.

## What
Je Datei die Quelltext-Tests sichten. Was ein Browser-Wächter oder ein `update`-Test schon abdeckt, löschen. Was nicht abgedeckt ist, auf die billigste Verhaltensebene heben (Doktrin Punkt 1). Repo-weite Invarianten mit Gegenprobe bleiben.

## Acceptance criteria
- [ ] Für jede der genannten Dateien ist jeder Quelltext-Test gelöscht, ersetzt oder mit einer Zeile Begründung behalten.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.