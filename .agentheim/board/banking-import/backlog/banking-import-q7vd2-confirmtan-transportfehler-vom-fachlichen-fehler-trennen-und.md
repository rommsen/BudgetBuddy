---
id: banking-import-q7vd2
title: "confirmTan: Transportfehler vom fachlichen Fehler trennen und den Serverzustand bis zum Ende nachlesen"
status: backlog
type: bug
context: banking-import
created: 2026-10-31
completed:
depends_on: [banking-import-cyg94]
blocks: []
tags: [frontend, sync]
related_adrs: [0071]
related_research: []
prior_art: [banking-import-cyg94]
---

## Why
Der Exception-Zweig von `confirmTan` in `src/Client/Components/SyncFlow/State.fs` wird zu `Error TanTimeout`. `TanConfirmed (Error _)` liest die Sitzung zwar neu, aber der Toast meldet einen Timeout, ein noch laufender Abruf wird nicht weiter nachgelesen, und ohne Sitzung bleibt nur der Toast. Serverseitig ist das erledigt: `confirmTanWith` fängt Ausnahmen ab, die Sitzung bleibt nicht mehr im Abruf stehen. Offen ist nur der Client.

## What
Dieselbe Behandlung wie `SyncStartInterrupted` für `confirmTan`: das befristete Nachlesen über `StartRecovery` und `InterruptedStartReadDue` mitnutzen. Eine erneute Bestätigung oder Challenge entsteht nie automatisch.

## Acceptance criteria
- [ ] Kaputt: GIVEN die Antwort von `confirmTan` geht verloren und der Server ruft noch ab WHEN der Abruf endet THEN die Prüfung erscheint ohne Neuladen der Seite.
- [ ] Intakt: GIVEN `confirmTan` liefert einen fachlichen Fehler THEN bleibt die Anzeige wie bisher.
- [ ] GIVEN die Antwort geht verloren und der Server hat keine Sitzung THEN bleibt ein Fehler sichtbar.
- [ ] GIVEN die Antwort geht verloren und der Server hat bis zum Ablauf der Frist kein Ergebnis THEN bleibt ein Fehler sichtbar.