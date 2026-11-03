---
id: budgeting-r7t3k
title: "Budget-Seite: Rückkehr aus dem Kassenbuch und Schließen von Targets ohne Sprung"
status: backlog
type: bug
context: budgeting
created: 2026-11-02
completed:
depends_on: [budgeting-p4n6r]
blocks: []
tags: [budget, inspektor]
related_adrs: []
related_research: []
prior_art: [budgeting-p4n6r]
---

## Why
budgeting-p4n6r beruhigt nur das Blättern. Die Rückkehr aus dem Kassenbuch (`PageEntered` mit `KeptPage`, `State.fs`) und `CloseTargets` nach einem Lauf (`closeTargets` in `Targets.fs`) gehen über `awaitMonth` und setzen `Month = Loading`: Liste und Inspektor verschwinden kurz, Fokus und Scrollposition gehen verloren.

## What
Beide Wege lassen einen geladenen Monat stehen und lesen ihn still neu, wie nach einer Verschiebung: Monat bleibt `Success`, Antwort über den `Reloaded`-Weg (`reloadCmd`, `applyReloaded`). Scheitert das Lesen, bleibt die Liste und ein Warn-Toast nennt den Grund (Verhalten von `applyReloaded`). War der Monat nicht geladen (`Loading`, `Failure`), bleibt der heutige Ladeweg mit Fehlerkarte und Wiederholen.

## Acceptance criteria
Alle Szenarien als Client-`update`-Tests unter .NET (Doktrin Punkt 1 und 5: ob Liste und Inspektor stehen bleiben, folgt aus `Month`; dafür ist kein Browser-Test erlaubt).

**kaputt (Regressionstests, vor dem Fix rot)**
- [ ] GIVEN geladener Monat, Kategorie im Tab Buchungen, Sprung ins Kassenbuch WHEN `PageEntered` THEN `Month` bleibt `Success` mit dem alten Stand, ein stilles Neu-Lesen ist unterwegs, und die Buchungen der Kategorie sind wieder fällig und werden geholt.
- [ ] GIVEN Targets angewendet (`ApplyDone`) WHEN `CloseTargets` THEN `Month` bleibt `Success`, ein stilles Neu-Lesen ist unterwegs, und seine Antwort setzt die neuen Beträge.

**intakt**
- [ ] GIVEN Rückkehr aus dem Kassenbuch WHEN das stille Neu-Lesen scheitert THEN die Liste bleibt mit dem alten Stand und es kommt ein Warn-Toast.
- [ ] GIVEN Seite neu betreten ohne Rückkehr (`FreshEntry`) WHEN `PageEntered` THEN beginnt die Seite wie bisher beim laufenden Monat ohne Auswahl, Tab Geld.
- [ ] GIVEN Monat nicht geladen (`Failure`) WHEN Rückkehr aus dem Kassenbuch THEN normaler Ladeweg mit Fehlerkarte und Wiederholen.
- [ ] GIVEN Targets-Lauf läuft noch (`Applying`) WHEN `CloseTargets` THEN der Dialog bleibt offen, nichts wird gelesen.

**Sonstiges**
- [ ] Scrollposition und Fokus der Liste bleiben nach der Rückkehr aus dem Kassenbuch erhalten. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Verfeinert am 2026-11-03. Die Browser-Kriterien der Erfassung sind in Client-`update`-Tests übersetzt; Scrollposition ist Browser-Verhalten und bleibt bis infrastructure-b3t9k beim Blick von Roman.
