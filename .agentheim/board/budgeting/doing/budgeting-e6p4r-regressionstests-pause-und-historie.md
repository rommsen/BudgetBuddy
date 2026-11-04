---
id: budgeting-e6p4r
title: "Benannte Regressionstests: Pause-Antwort während eines Monatswechsels, Zurück aus der allgemeinen Historie"
status: doing
type: chore
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [budget, test, pause, historie]
related_adrs: []
related_research: []
prior_art: [budgeting-c6vn4, budgeting-h7q4v, budgeting-r3wq8]
---

## Why
Aus budgeting-h9c3w (QA-Review zu c6vn4, Punkte 14 und 15). Ein echter Bug aus c6vn4 (Q1) ist nur im Walk bei Tiefe 6 sichtbar, ohne benannten Regressionstest mit Bug-Kommentar (Doktrin Punkt 8). Der Test "Zurück geht einen Schritt zurück" prüft nur zwei Nachrichten.

## What
Nur Tests, kein Produktionscode (ändert sich Produktionscode, weil ein Test einen Fehler zeigt, gilt Doktrin Punkt 8 und das Outcome nennt es).

1. `src/Tests/TargetPauseClientTests.fs`: GIVEN/WHEN/THEN `SetTargetPause`, `SelectMonth` nächster Monat, `Loaded` nächster Monat, `SelectMonth` laufender Monat, dann `TargetPauseAnswered`: `MonthGeneration` steigt (der Monat wird neu gelesen). Dasselbe für `TargetPauseCallLost` mit dem Toast aus `pauseCallLostText true`. Kommentar nennt den Bug aus c6vn4 Q1.
2. `src/Tests/BudgetGeneralHistoryClientTests.fs` (etwa Zeile 186): echter Ablauf `OpenGeneralHistory`, `PageLeft`, `GeneralHistoryEntered`, Antwort, dann `LeaveGeneralHistory` geht einen Schritt zurück. Intakt-Fall: `GeneralHistoryAbandoned` und danach der Link führt auf `ShowBudgetPage`.

## Acceptance criteria
- [ ] Test 1 für `TargetPauseAnswered` und für `TargetPauseCallLost`; Gegenprobe: `|| monthChanging model` in der Bedingung gestrichen macht beide rot (im Outcome nennen, Mutantenliste genau diese).
- [ ] Test 2 mit vollständigem Ablauf und Intakt-Fall; Gegenprobe im Outcome.
- [ ] Testnamen als deutsche Sätze, Task-Ids in Kommentaren.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
