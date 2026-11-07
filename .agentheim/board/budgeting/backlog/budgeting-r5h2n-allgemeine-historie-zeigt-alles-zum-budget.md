---
id: budgeting-r5h2n
title: "Allgemeine Historie zeigt alles, was das Budget betrifft, auch Target-Änderungen und Pausen"
status: backlog
type: feature
context: budgeting
created: 2026-11-07
completed:
depends_on: []
blocks: []
tags: [budget, historie, targets, pause]
related_adrs: [0080, 0084]
related_research: []
prior_art: [budgeting-h7q4v, budgeting-c6vn4, budgeting-t6p8z, budgeting-k8wq3]
---

## Why
Roman am 2026-11-07: Er hat eine Target-Änderung in der allgemeinen Historie (`#/budget/history`) gesucht und nicht gefunden. Dort stehen nach Design (budgeting-h7q4v) nur Budget-Bewegungen; Target-Änderungen und Pausen stehen nur im Tab Historie je Kategorie, und dort im Monat ihres `effectiveFrom`.

Romans Wunsch: "es soll alles was mit budgets zu tun hat da rein".

## What (roh, zu verfeinern)
Die allgemeine Historie zeigt neben den Bewegungen auch:
- Target-Änderungen (`TargetSet`, `TargetRemoved`, Altbestand `YnabGoalChangedFromBb` mit Stand je Schritt), Texte wie im Tab Historie ("Target 50,00 € → 60,00 €", "Target entfernt").
- Pausen (`TargetPaused`, `TargetPauseLifted`), ohne Betrag und ohne Stand.

## Offene Fragen für refine
- Gehört `TargetTakenOverFromYnab` (Startstand, keine Änderung) hinein, oder nur die Übernahme der Zuweisungen wie heute?
- Filter: neue Chips "Targets" und "Pausen", oder eine gemeinsame Art "Targets" für beides?
- Durchlauf-Zeile: eine Target-Änderung ist eine eigene Zeile, kein Durchlauf. Wie heißt sie in `HistoryRun`, und welcher Stand gilt?
- Satz in der README "Pausen und Target-Änderungen stehen nicht darin" fällt weg.
- Braucht es eine Skizze, oder reicht die bestehende Zeilenform?
- `getBudgetHistory` liest heute alle Bewegungen ohne Grenze. Mit allen Target-Streams und Pause-Ereignissen: Menge prüfen, eventuell Grenze oder Seiten.
- Gibt es weitere Budget-Ereignisse, die Roman erwartet (etwa Ausblenden der Leiste Monatserster, das heute Zustand ist und kein Ereignis, ADR 0083)?

## Notes
- Keine echten Beträge oder Kategorienamen in Tests und Doku (öffentliches Repo).
