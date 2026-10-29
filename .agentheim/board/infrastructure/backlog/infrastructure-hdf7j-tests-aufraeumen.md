---
id: infrastructure-hdf7j
title: "Tests aufräumen nach der Test-Doktrin: Tautologien, schwache Quelltext-Tests, geteilte Kalendertage"
status: backlog
type: refactor
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-cbr0r]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Rund 15 Tautologien gehäuft in alten Comdirect-/YNAB-Dateien, schwache Teilstring-Prüfungen auf View-Verdrahtung und Klassenkonstanten, Tagesgrenzen-Tests teilen Kalendertage (Audit D, B1 N7 bis N9, C B10, B11, B19).

## What
Tautologien löschen, schwache Quelltext-Tests durch Verhaltenstests ersetzen oder löschen, Tagesgrenzen-Tests mit eigener DB oder injizierter Uhr.

## Acceptance criteria
- [ ] Die im Bericht D gelisteten Tautologien sind entfernt oder begründet behalten.
- [ ] Tagesgrenzen-Tests laufen unabhängig vom Kalendertag anderer Tests.

## Notes
Migrationsschritt M14. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.

