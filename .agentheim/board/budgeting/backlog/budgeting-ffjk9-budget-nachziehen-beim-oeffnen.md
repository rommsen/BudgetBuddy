---
id: budgeting-ffjk9
title: "Nachziehen beim Öffnen: Budget-Seite und Übersicht zeigen sofort BBs Stand und gleichen danach still mit YNAB ab (syncBudgetMonth, Frische 60 s und 5 min, spätere Monate veraltet)"
status: backlog
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: [budgeting-m5pqn, budgeting-yx51n]
blocks: []
tags: [budget-seite, lesemodell, ynab, spiegel]
related_adrs: [0014, 0065, 0084, 0101]
related_research: []
prior_art: []
---

## Why
Teil 3 von 4 nach ADR 0101 (Ursprung budgeting-m5pqn). Ohne Beobachten im Lesepfad braucht BB einen anderen Ort, an
dem es Änderungen in YNAB sieht (ADR 0101 Punkt 6).

## What
- Endpoint `syncBudgetMonth` (Monat): beobachtet, wenn der Stand älter als 60 s oder als veraltet markiert ist; liest
  den Rahmen nach, wenn er älter als 5 min ist; liefert den Monat nur, wenn sich etwas geändert hat, sonst
  „unverändert“.
- Ein Budget-Befehl auf Monat M markiert die Monate nach M als veraltet.
- Nachziehen und Befehle auf demselben Monat laufen unter `MonthGuard` nacheinander. Die Echo-Regel aus ADR 0084
  Punkt 3 bleibt: eine Beobachtung gewinnt, das Soll wird YNABs Wert.
- Ausgelöst nach dem sofortigen Lesen beim Öffnen von Budget-Seite und Übersicht, gestartet wie
  `triggerMirrorInBackground` (ADR 0014), ohne Timer. Rate höchstens etwa 84/h, mit dem Kassenbuch-Spiegel (48/h)
  unter 200.
- Die Target-Übernahme, die heute beim Lesen läuft (`targetRulesBestEffort`), läuft hier nach bestem Bemühen.
- Client: nach `MonthLoaded` `syncBudgetMonth` aufrufen und still tauschen (kein `Loading`, kein Toast), ebenso in der
  Übersicht (`src/Client/Components/Overview/State.fs`). Scheitert das Nachziehen, steht der Hinweis „YNAB-Stand von
  <Uhrzeit>“ (F4).

## Acceptance criteria
- [ ] Handler-Test mit zählendem Port und eingespeister Uhr: Stand 59 s alt ergibt null Aufrufe, 61 s einen
      `FetchMonth`, ein veralteter Stand einen, unabhängig vom Alter.
- [ ] Handler-Test: bei unverändertem YNAB-Stand liefert `syncBudgetMonth` „unverändert“; hat YNAB ein anderes
      Zugewiesen, steht `AssignmentChangeObservedInYnab` im Stream und der Monat kommt mit YNABs Wert als Soll.
- [ ] Handler-Test: ein Zuweisen in M markiert M+1 und M+2 als veraltet, M−1 nicht.
- [ ] Handler-Test: Nachziehen und Befehl auf demselben Monat laufen in beiden Reihenfolgen nacheinander
      (`MonthGuard`); keine Reihenfolge erzeugt eine falsche Beobachtung, kein älterer Stand überschreibt einen
      neueren.
- [ ] Client-`update` unter .NET, Aufzählung aller Reihenfolgen von `MonthLoaded`, Sync-Antwort, `SelectMonth` und
      `PageLeft` (Doktrin Punkt 20, Vorbild `moveWays`): eine Sync-Antwort für einen anderen als den gezeigten Monat
      wird verworfen; „geändert“ tauscht Beträge, ohne dass `Month` je `Loading` wird; eine offene Eingabe im Feld
      Zugewiesen bleibt erhalten.
- [ ] Reine Funktion für den Hinweistext; Client-`update`-Test: ein Fehlschlag des Nachziehens zeigt
      „YNAB-Stand von <Uhrzeit>“, ein Erfolg zeigt nichts.
- [ ] Zeile in `RemotingEndpointNegativeTests` (ungültiger Monat, fehlende Einrichtung), Aufruf in
      `RemotingPipelineTests`.
- [ ] README budgeting nachgezogen, Delta mit `npm run check:readme-delta` geprüft.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
- Frage an Roman vor dem Promote, F4: Hinweis „YNAB-Stand von <Uhrzeit>“ nur, wenn das Nachziehen gescheitert ist,
  sonst nichts zeigen. Empfehlung: ja. Ort und Form des Hinweises gegen `standards/frontend/styleguide.md` prüfen
  (Hinweis am Ort, kein Toast).
- Der `TakeoverWorker` bleibt auf die Übernahme beschränkt.
