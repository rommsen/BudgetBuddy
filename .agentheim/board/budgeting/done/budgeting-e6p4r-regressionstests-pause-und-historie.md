---
id: budgeting-e6p4r
title: "Benannte Regressionstests: Pause-Antwort während eines Monatswechsels, Zurück aus der allgemeinen Historie"
status: done
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

## Outcome

Nur Tests, kein Produktionscode.

- `src/Tests/TargetPauseClientTests.fs`: zwei neue GIVEN/WHEN/THEN-Tests mit Bug-Kommentar zu budgeting-c6vn4 (Q1). Ablauf `SetTargetPause`, `SelectMonth` nächster Monat, `Loaded` nächster Monat, `SelectMonth` laufender Monat (Vorbedingung `monthChanging` geprüft), dann
  - "GIVEN Pause läuft, Wechsel in den nächsten Monat und zurück WHEN die Pause-Antwort kommt THEN wird der Monat noch einmal gelesen": `MonthGeneration` steigt, Cmd nicht leer.
  - "GIVEN Pause läuft, Wechsel in den nächsten Monat und zurück WHEN der Aufruf abbricht THEN wird der Monat noch einmal gelesen und der Toast sagt es": `MonthGeneration` steigt, Cmd nicht leer, Toast `pauseCallLostText true`.
- `src/Tests/BudgetGeneralHistoryClientTests.fs`: der Test "Zurück geht nach dem Knopf Historie einen Schritt zurück, per Link geöffnet zur Budget-Seite" läuft jetzt den echten Ablauf `OpenGeneralHistory`, `PageLeft`, `GeneralHistoryEntered`, Antwort, dann `LeaveGeneralHistory` ergibt `StepBackToBudgetPage`. Intakt-Fall: `OpenGeneralHistory`, `GeneralHistoryAbandoned`, `GeneralHistoryEntered`, dann ergibt Zurück `ShowBudgetPage`. Die Prüfungen per Link und über die App bleiben.

Gegenproben (Produktionszeile kurz geändert, Test gelaufen, Datei exakt zurückgesetzt):
- M1, wie im Kriterium verlangt: `|| monthChanging model` in `pauseNeedsReread` (`src/Client/Components/Budget/Pause.fs`) gestrichen: grün, auch der Walk. Abweichung vom Kriterium: im erreichbaren Zustand deckt `generationMoved` den Fall, denn jeder `SelectMonth` hebt die Generation seit dem Start des Aufrufs. Für `TargetPauseCallLost` gibt es diesen Term gar nicht, `pauseCallLostRereads` ist nur `shownMonth model = Some month`.
- M2, der tatsächliche Stand vor dem Fix aus 9faa461: `&& not (monthChanging model)` in `pauseNeedsReread` wieder eingefügt: rot, der neue Antwort-Test und der Walk.
- M3, der Stand vor dem Fix beim Abbruch: `pauseCallLostRereads` mit `&& not (monthChanging model)`: rot, der neue Abbruch-Test und der Walk.
- H1: `enterGeneralHistory` setzt `ReturnPending = false`: rot, der neue Zurück-Test (und "Zurück zur Budget-Seite: Monat und Auswahl bleiben erhalten"). Der alte Test ohne `GeneralHistoryEntered` konnte diesen Mutanten nicht sehen.
- H2: `abandonGeneralHistory` ohne Wirkung (`withHistory model id`): rot, der Intakt-Fall im neuen Test (und "Verlässt man die Historie auf eine andere Seite …").

Die Aussage "kein Überlebender" gilt nur für M2, M3, H1, H2. M1 überlebt, siehe Folge-Task.

`./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3778 grün, node 300 grün), im ersten Lauf, ohne den wackligen `[layout]`-Test.
