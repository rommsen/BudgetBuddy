---
id: budgeting-q8m2v
title: "Allgemeine Historie begrenzen oder nach Monat laden"
status: backlog
type: feature
context: budgeting
created: 2026-11-04
completed:
depends_on: []
blocks: []
tags: [budget, historie, performance]
related_adrs: [0065]
related_research: []
prior_art: [budgeting-h7q4v, budgeting-c6vn4]
---

## Why
`BudgetApi.getBudgetHistory` liefert jede Budget-Bewegung seit Beginn der Aufzeichnung, ohne Grenze (Backend-Review zu budgeting-h7q4v, Punkt 3). Bei einem Haushalt ist das heute klein, wächst aber mit jedem Targets-Lauf um eine Zeile je Kategorie. Der Client bildet daraus bei jedem Betreten alle Durchläufe.

## What
Die allgemeine Historie lädt begrenzt: etwa die letzten N Durchläufe mit "Ältere laden" oder je Monat. Zu klären: Grenze nach Durchläufen (Gruppen-Id) statt nach Bewegungen, damit ein Targets-Durchlauf nie halb geladen ist; ob der Server dann `HistoryRun` statt `HistoryMovement` liefert.

## Acceptance criteria
- [ ] Die Antwort von `getBudgetHistory` ist begrenzt, ein Durchlauf kommt immer ganz.
- [ ] Ältere Durchläufe sind erreichbar.
- [ ] Der Vertragskommentar an `getBudgetHistory` nennt die Grenze.
- [ ] Negativzeile und Playwright-Fixtures ziehen mit.

## Notes
Aus budgeting-c6vn4 (A8).