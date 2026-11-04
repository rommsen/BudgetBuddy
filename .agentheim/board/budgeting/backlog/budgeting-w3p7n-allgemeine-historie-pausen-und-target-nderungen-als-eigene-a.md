---
id: budgeting-w3p7n
title: "Allgemeine Historie: Pausen und Target-Änderungen als eigene Arten?"
status: backlog
type: feature
context: budgeting
created: 2026-11-04
completed:
depends_on: [budgeting-h7q4v, budgeting-r3wq8]
blocks: []
tags: [budget, historie]
related_adrs: [0079, 0080]
related_research: []
prior_art: [budgeting-h7q4v, budgeting-t6p8z, budgeting-k8wq3]
---

## Why
budgeting-h7q4v zeigt in `#/budget/history` nur die drei Bewegungsarten (Targets anwenden, Verschieben, Zuweisen). Pausen und Target-Änderungen stehen nur im Tab Historie je Kategorie und Monat. Für "Was ist passiert" über alle Kategorien könnten sie fehlen.

## What
Entscheiden (Roman), ob die allgemeine Historie Pausen und Target-Änderungen zeigt. Falls ja: zwei Filter-Arten dazu ("Pausen", "Target-Änderungen"), je Ereignis eine Zeile (Zeitpunkt, Kategorie, Text wie im Tab Historie, Monat; Pause ohne Stand, Target-Änderung mit Stand), dafür Abfragen über `target_pause_events` und `target_changes` ohne Kategorie und Monat und ein erweiterter Endpoint oder ein zweiter.

## Acceptance criteria
- [ ] Entscheidung von Roman steht in der Task.
- [ ] Falls ja: (Domain) Ereignisse zu Zeilen und gemischte Reihenfolge mit den Durchläufen, Unit-Tests.
- [ ] Falls ja: Abfrage gegen In-Memory-SQLite über alle Monate, Zeile in `RemotingEndpointNegativeTests.fs` bei neuem Endpoint.
- [ ] Falls ja: Filter im Client-`update` getestet.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Erst nach budgeting-r3wq8, das den Pause-Code (PauseIntent, PauseLifted, DB-Text "Aufgehoben") umbaut.