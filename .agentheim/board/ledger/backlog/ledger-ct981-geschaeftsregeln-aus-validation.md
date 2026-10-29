---
id: ledger-ct981
title: "Bot-Geschäftsregeln aus Validation.fs in die Ledger-Domain verschieben (Tagesgrenze, Kategorie-Auflösung, Zeitzone)"
status: backlog
type: refactor
context: ledger
created: 2026-10-29
completed:
depends_on: [infrastructure-ky2kh]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`src/Server/Validation.fs:160-229`, `:336-372` enthalten Geschäftsregeln und `failwith` (Audit B1 M4, M5, B2, C B14).

## What
Regeln als pure Funktionen in die Ledger-Domain, Zeitzone als Abhängigkeit, kein `failwith`.

## Acceptance criteria
- [ ] Validation.fs enthält nur Eingabeprüfung.
- [ ] Bestehende Bot-Tests bleiben grün, Domain-Wächter aus M6 ist grün.

## Notes
Migrationsschritt M15. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.

