---
id: budgeting-f8w2n
title: "Target und Pause sperren sich nur je Kategorie mit sichtbarem Grund, Meldung ohne Target nach Schritt 1, Testlücken aus den Reviews zu c2t9w"
status: doing
type: bug
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-d7r4k]
blocks: []
tags: [budget, target, pause, test, review]
related_adrs: [0080]
related_research: []
prior_art: [budgeting-c2t9w, budgeting-g2ty9, budgeting-f4k9n]
---

## Why
Die Reviews zu budgeting-c2t9w (Commit 51ccabd) melden keinen Muss-Befund, aber fünf Sollte-Befunde. Roman 2026-11-04: selbständig modellieren und arbeiten, was geht. Das ist die letzte Fix-Task dieser Kette, danach starten keine Reviews mehr.

## What

### 1. Sperren je Kategorie mit sichtbarem Grund (Frontend-Review)
Heute gelten `pauseLocked` (`src/Client/Components/Budget/Pause.fs`) und `targetWaits` (`TargetEdit.fs`) über alle Kategorien: ein laufendes Speichern bei A sperrt "Target pausieren" bei B, eine Pause bei A sperrt Speichern und Entfernen bei B. Im Tab Geld sieht man keinen Grund, der Spinner steht im Tab Target.

Entscheidung: Die gegenseitige Sperre gilt nur für dieselbe Kategorie (die des laufenden Aufrufs). Ist "Target pausieren" wegen eines laufenden Target-Aufrufs derselben Kategorie gesperrt, steht darunter "Wartet, bis das Target gespeichert ist." (`ListRoles.secondary`); sind Speichern oder Entfernen wegen einer laufenden Pause derselben Kategorie gesperrt, steht darunter "Wartet, bis die Pause gespeichert ist." Beide Sätze als Konstanten, die Sichtbarkeit als reine Funktion.

### 2. Meldung ohne Target nach Schritt 1 (Backend-Review)
`targetPartlyText` (`src/Shared/Budgeting.fs`) beginnt bei `DateHeldBack NoTargetAfterFirst` mit "Target angelegt" oder "Target geändert", obwohl YNAB danach kein Target zeigt. Entscheidung: in diesem Fall lautet der Satz "Schritt 1 an YNAB gesendet, YNAB zeigt danach kein Target. Datum 01.06.2027 nicht gesetzt." Die beiden anderen Gründe behalten ihren Kopf. Test in `src/Tests/TargetChangeTests.fs` (etwa Zeile 771) anpassen.

### 3. Testlücken (QA-Review)
- Walk `askWalkTests` (`src/Tests/TargetChangeClientTests.fs`): Ereignis "Tab Verlauf" über `PageKeyPressed (StepTab Previous)` aus dem Tab Target ergänzen (Orakel wie "Tab Geld"). Die Mutante `away = model.Tab = MoneyTab` in `syncTargetForm` muss rot werden.
- Walk prüft je Schritt auch `.Busy` von `pauseButtonState` und `removeButtonState` gegen `PausePending` und `RemovePending`. Einzeltest: laufende Pause bei einer anderen Kategorie lässt den Knopf der gezeigten nicht drehen und (nach Punkt 1) nicht gesperrt.
- Rundlauf-Test: für "NEED", "TB", "TBD", "MF", "DEBT", "XYZ" gilt `goalTypeText (YnabClient.targetKind code) = code`.

## Acceptance criteria
- [ ] Walk mit zwei Kategorien oder Einzeltests: ein Target-Aufruf bei A sperrt Pause bei B nicht und umgekehrt; bei derselben Kategorie bleibt die gegenseitige Sperre, nie laufen zwei Aufrufe derselben Kategorie gleichzeitig. Mit der heutigen globalen Sperre rot.
- [ ] Reine Funktion für die Hinweissätze, getestet: Satz nur bei Sperre durch den Aufruf derselben Kategorie.
- [ ] Reiner Test: `NoTargetAfterFirst` ergibt den Satz ohne "angelegt" und "geändert".
- [ ] Walk mit "Tab Verlauf" per Pfeiltaste; Mutante `away = model.Tab = MoneyTab` rot (im Outcome).
- [ ] Walk prüft `.Busy`; Mutanten `Busy = false` und `Busy = pauseBusy model` rot (im Outcome).
- [ ] Rundlauf-Test für `goalTypeText` und `targetKind`.
- [ ] README budgeting (Target-Änderung, "Höchstens ein Aufruf") per README_DELTA auf "je Kategorie" nachgezogen.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Nicht parallel zu anderen Arbeiten im Target-Formular. Screenreader außerhalb des Umfangs.
