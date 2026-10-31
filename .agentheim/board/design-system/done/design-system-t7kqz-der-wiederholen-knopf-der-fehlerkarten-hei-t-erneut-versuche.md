---
id: design-system-t7kqz
title: "Der Wiederholen-Knopf der Fehlerkarten heißt Erneut versuchen statt Try Again"
status: done
type: bug
context: design-system
created: 2026-10-31
completed:
depends_on: []
blocks: []
tags: [microcopy, fehlerzustand]
related_adrs: []
related_research: []
prior_art: [infrastructure-r3tw7]
---

## Why

`ErrorDisplay.cardWithTitle` in `src/Client/DesignSystem/ErrorDisplay.fs` beschriftet den Knopf mit "Try Again", auch `heroSimple` tut das. Die UI ist deutsch, Task infrastructure-r3tw7 nennt den Knopf "Erneut versuchen". Dort blieb der Text, weil die Task das Verhalten wie bisher verlangt.

## What

Prüfen, welche Varianten in `ErrorDisplay.fs` "Try Again" zeigen, und sie einheitlich auf "Erneut versuchen" umstellen. Wächter ziehen mit, darunter `scripts/ledger-read-retry.test.mjs` und `scripts/budget-page.test.mjs`.

## Acceptance criteria

- [ ] Keine Fehlerkarte im Client zeigt mehr "Try Again".
- [ ] Die Wächter suchen den neuen Text.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
## Outcome

Mit den Review-Fixes zu budgeting-m1bce erledigt (Commit 66a7c32): alle Varianten in `ErrorDisplay.fs` zeigen "Fehler" und "Erneut versuchen", die Wächter suchen den neuen Text. `./verify.sh` PASS. Ein eigener Verifier-Lauf fand nicht statt, geprüft hat das Gate.
