---
id: infrastructure-gwvw1
title: "Override (C) einführen und BC-README-Übergaben für Bug-Szenarien und Test-Doktrin"
status: todo
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-dvy29, infrastructure-cbr0r]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Souls E4, E10, E11: Review-Agents als Session-End-Empfehlung, Bug-Tasks mit kaputt-/intakt-Szenario im Refine.

## What
Eine Zeile in CLAUDE.md: nach jedem work-Lauf passende Review-Agents vorschlagen und im Session-End-Eintrag nennen, nicht automatisch starten. BC-READMEs: Bug-Szenario-Regel und Verweis auf die Test-Doktrin.

## Acceptance criteria
- [ ] CLAUDE.md enthält die Override-(C)-Regel.
- [ ] Jede BC-README nennt die Bug-Szenario-Regel.

## Notes
Migrationsschritt M11. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt.
