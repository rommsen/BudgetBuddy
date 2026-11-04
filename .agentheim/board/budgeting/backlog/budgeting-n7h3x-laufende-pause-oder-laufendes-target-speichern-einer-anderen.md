---
id: budgeting-n7h3x
title: "Laufende Pause oder laufendes Target-Speichern einer anderen Kategorie sperrt den gleichen Knopf ohne sichtbaren Grund"
status: backlog
type: bug
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-f8w2n]
blocks: []
tags: [budget, target, pause]
related_adrs: [0080]
related_research: []
prior_art: [budgeting-f8w2n, budgeting-c2t9w]
---

## Why
Nach budgeting-f8w2n sperren sich Target und Pause nur noch je Kategorie. Gleichartige Aufrufe sperren weiter über alle Kategorien: läuft die Pause bei A, ist "Target pausieren" bei B gesperrt; läuft Speichern bei A, sind Speichern und Entfernen bei B gesperrt. Der Spinner steht bei A, bei B sieht man keinen Grund. Ursache: `PauseCall` und `TargetCall` (`src/Client/Components/Budget/Types.fs`) halten je nur einen laufenden Aufruf; ein zweiter überschriebe den ersten, dessen Antwort, Meldung und Neulesen (`pauseStartGeneration`, `applyPauseAnswered`) gingen verloren. Der QA-Review zu c2t9w hatte "nicht gesperrt" auch für eine Pause bei einer anderen Kategorie erwartet.

## What
Entscheiden: entweder Aufrufe je Kategorie halten (Map statt eines Platzes, mit allen Folgen für Antwort, Abbruch und Neulesen) oder die globale Sperre behalten und unter dem gesperrten Knopf einen Satz zeigen (etwa "Wartet, bis die Pause bei <Kategorie> gespeichert ist.").

## Acceptance criteria
- [ ] Entscheidung festgehalten (README budgeting, bei Map ein Nachtrag zu ADR 0080 Punkt 5).
- [ ] Bei gesperrtem Knopf ist der Grund sichtbar oder der Knopf ist nicht mehr gesperrt, getestet als reine Funktion und im bestehenden Walk.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Screenreader außerhalb des Umfangs.