---
id: budgeting-f8w2n
title: "Target und Pause sperren sich nur je Kategorie mit sichtbarem Grund, Meldung ohne Target nach Schritt 1, Testlücken aus den Reviews zu c2t9w"
status: done
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

## Outcome

Target und Pause sperren sich gegenseitig nur noch je Kategorie (ADR 0080 Punkt 5). Ein Speichern bei Ausflug lässt "Target pausieren" bei Urlaub frei, eine Pause bei Ausflug lässt Speichern und Entfernen bei Urlaub frei; in derselben Kategorie bleibt die Sperre. Es läuft weiter nur ein Target-Aufruf und nur eine Pause zugleich, weil `TargetCall` und `PauseCall` je einen Platz haben.

- `src/Client/Components/Budget/Pause.fs`: `targetCallFor` (vorher `targetCallRunning`, global) prüft die Kategorie, neu `pauseCallFor`, `PauseWaitsForTargetText` ("Wartet, bis das Target gespeichert ist.") und `pauseWaitHint` (nur wenn ein Knopf steht und ein Target-Aufruf derselben Kategorie sperrt).
- `src/Client/Components/Budget/TargetEdit.fs`: `targetWaits` nimmt die Kategorie und fragt `Pause.pauseCallFor`; neu `TargetWaitsForPauseText` ("Wartet, bis die Pause gespeichert ist."), `saveWaitHint` (nur wenn ohne die Pause gespeichert werden könnte, `saveableApartFromPause`) und `removeWaitHint` (nur wenn kein Target-Aufruf mitsperrt). Die doppelte Leerzeile vor `start` ist weg.
- Views: `PauseView.fs` zeigt den Satz unter dem Pause-Knopf (Tab Geld und Tab Target, `ListRoles.secondary`, testid `target-pause-wait`), `TargetEditView.fs` unter "Target speichern" (`target-save-wait`) und "Target entfernen" (`target-remove-wait`, Container jetzt `flex flex-col gap-2` wie beim Speichern).
- `src/Shared/Budgeting.fs`: `targetPartlyText` mit `DateHeldBack NoTargetAfterFirst` ergibt "Schritt 1 an YNAB gesendet, YNAB zeigt danach kein Target. Datum 01.06.2027 nicht gesetzt."; die anderen Gründe behalten ihren Kopf.
- Tests: `src/Tests/TargetChangeTests.fs` (Satz ohne "angelegt" und "geändert" für Anlegen und Wechsel, Rundlauf `goalTypeText (YnabClient.Decoders.targetKind code) = code` für NEED, TB, TBD, MF, DEBT, XYZ; `targetKind` war über `Server.YnabClient.Decoders` schon erreichbar, keine Änderung nötig). `src/Tests/TargetChangeClientTests.fs`: der bestehende `askWalkTests` hat jetzt einen Tab statt `InTargetTab` und das Ereignis "Tab Verlauf" über `PageKeyPressed (StepTab Previous)` aus dem Tab Target (im Tab Historie zählt das Lesen der Historie nicht als gesendeter Aufruf), prüft je Schritt `.Busy` von Pause-, Entfernen- und Speichern-Knopf und die drei Sätze gegen das Orakel. Neue Liste `perCategoryTests` mit zwei Kategorien: Speichern bei A sperrt Pause bei B nicht, Pause bei A sperrt Speichern und Entfernen bei B nicht (beide Aufrufe laufen zugleich), Pause bei A lässt den Knopf von B nicht drehen, Satz nur bei alleiniger Sperre durch die Pause. Der Client-Test zum zurückgehaltenen Schritt 2 nimmt den Satz jetzt aus `targetPartlyText`.

Mutanten (Produktionszeile ändern, laufen lassen, exakt zurücksetzen), alle rot:
- M1 `| TargetSaving _ -> true` in `targetCallFor` (die alte globale Sperre): rot in "GIVEN Speichern bei Ausflug läuft WHEN Urlaub gezeigt ...".
- M2 `targetBusy model || Pause.pauseBusy model` in `targetWaits` (alte globale Sperre): rot in "GIVEN Pause bei Ausflug läuft WHEN Urlaub gezeigt ...".
- M3 `away = model.Tab = MoneyTab` in `syncTargetForm`: rot im Walk der Rückfragen.
- M4 `Busy = false` in `pauseButtonState`: rot im Walk.
- M5 `Busy = pauseBusy model` in `pauseButtonState`: rot in "Eine laufende Pause bei Ausflug lässt den Pause-Knopf von Urlaub nicht drehen".
- M6 `pauseWaitHint` ohne Bedingung `targetCallFor`: rot im Walk und in zwei Einzeltests.
- M7 `saveWaitHint` ohne `saveableApartFromPause`: rot im Walk und im Einzeltest zum Satz.
- M8 `removeWaitHint` ohne `not (targetBusy model)`: rot im Einzeltest zum Satz. (Eine erste Fassung mit zusätzlich `not (confirmOpen form)` überlebte, weil neben einer laufenden Pause keine Rückfrage offen sein kann; die Bedingung ist entfernt.)
- M9 Zweig `DateHeldBack NoTargetAfterFirst` in `targetPartlyText` entfernt: rot.
- M10 `TargetBalanceByDate -> "TB"` in `goalTypeText`: rot im Rundlauf-Test.

Abweichung vom QA-Punkt "laufende Pause bei einer anderen Kategorie lässt den Knopf der gezeigten nicht drehen und nicht gesperrt": "nicht drehen" ist getestet; "nicht gesperrt" gilt nur für die gegenseitige Sperre Target gegen Pause (Entscheidung Punkt 1). Pause gegen Pause bleibt global gesperrt, weil `PauseCall` nur einen Aufruf hält; ein zweiter Aufruf überschriebe den ersten, seine Antwort und sein Neulesen gingen verloren. Folgeaufgabe budgeting-n7h3x im Backlog.

`./verify.sh` im Worktree: VERIFY: PASS (dotnet 3801 grün, node 300 grün). README budgeting per README_DELTA nachgezogen (Gate `check:readme-delta` Exit 0).
