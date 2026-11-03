---
id: infrastructure-q7r4m
title: "Strukturtests auf View-Verdrahtung in CategoryPickerTests und RulesListTests nach Doktrin Punkt 7 ablösen"
status: backlog
type: chore
context: infrastructure
created: 2026-11-03
completed:
depends_on: []
blocks: []
tags: [test, doktrin]
related_adrs: []
related_research: []
prior_art: [infrastructure-t5m2b]
---

## Why
Bei infrastructure-t5m2b aufgefallen: `src/Tests/CategoryPickerTests.fs` (etwa "Zeilen kommen aus PickerParts.row") und die Quelltext-Liste in `src/Tests/RulesListTests.fs` lesen Quelltext, um View-Verdrahtung zu prüfen. Doktrin Punkt 7 verbietet neue View-Verdrahtung per Teilstring.

## What
Je Test prüfen: Verhaltenszugang über eine reine Funktion oder Client-`update` schaffen und den Strukturtest löschen, oder begründen, warum er eine repo-weite Invariante ist.

## Acceptance criteria
- [ ] Kein Strukturtest auf View-Verdrahtung mehr in beiden Dateien, oder je verbleibendem Test eine Begründung nach Doktrin 7.
- [ ] Je gelöschtem Test eine Mutante, die den Ersatz rot macht.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.