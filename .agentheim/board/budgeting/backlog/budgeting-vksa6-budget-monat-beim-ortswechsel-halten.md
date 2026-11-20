---
id: budgeting-vksa6
title: "Budget-Seite hält den Monat beim Ortswechsel: Rückkehr aus Kassenbuch, Übersicht oder Einstellungen zeigt den geladenen Monat sofort und liest still nach"
status: backlog
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: [budgeting-m5pqn]
blocks: []
tags: [budget-seite, client, performance]
related_adrs: [0101]
related_research: []
prior_art: [budgeting-r7t3k]
---

## Why
Teil 4 von 4 nach ADR 0101 (Ursprung budgeting-m5pqn, Roman 2026-11-20: „Die Seite muss instant da sein.“). Heute setzt
jedes normale Betreten (`FreshEntry`, `Inspector.enterPage`, `Month.beginLoad`) `Month = Loading`, auch wenn der Monat
schon im Model liegt. Dann steht eine Ladeanzeige da, selbst wenn der Server sofort antwortet.

## What
- Bei `FreshEntry` bleibt ein geladener Monat stehen, wenn er der zu zeigende Monat ist (laufender Monat). Danach kommt
  ein stilles Nachlesen (`QuietReread`) und `syncBudgetMonth` (sobald budgeting-ffjk9 gebaut ist).
- Der Rest von `enterPage` (Suche, Filter, Auswahl, Tab zurücksetzen) bleibt.
- Ein anderer Monat wird wie heute geladen.

## Acceptance criteria
- [ ] Client-`update` unter .NET: `PageEntered` mit geladenem laufendem Monat lässt `Month` auf `Success` mit
      unveränderten Gruppen und schickt ein stilles Nachlesen; mit geladenem anderem Monat (etwa Dezember gewählt,
      dann verlassen) wird der laufende Monat neu geladen (`Loading`).
- [ ] Client-`update`: Aufzählung aller Reihenfolgen von `PageEntered`, `PageLeft`, `SelectMonth` und Antworten des
      stillen Nachlesens (Doktrin Punkt 20): keine Reihenfolge zeigt eine Antwort für einen verlassenen Monat.
- [ ] Bestehende `[flow]`-Tests der Budget-Seite mit Ladezustand angepasst, kein neuer Browser-Test (Doktrin
      Punkt 1, unter .NET prüfbar).
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.
- [ ] Seitenwechsel Kassenbuch → Budget am Handy wirkt sofort, ohne Ladeanzeige. [human-eye]

## Notes
- Hängt nur an budgeting-m5pqn (schneller Lesepfad). Wird budgeting-ffjk9 später gebaut, bekommt das Betreten dort
  den Aufruf von `syncBudgetMonth`; die Aufzählung in budgeting-ffjk9 nimmt `PageEntered` mit auf.
- Prior art: budgeting-r7t3k (Rückkehr aus dem Kassenbuch).
