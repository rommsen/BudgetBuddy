---
id: infrastructure-gfxn4
title: "autoMode in ~/.claude/settings.json gliedern: neutrale Org-Zeilen plus Blöcke je Projekt"
status: backlog
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`autoMode.environment` beschreibt nur BudgetBuddy (Trusted repo, Deploy-Remote, .env-Secrets) und gilt für alle Projekte (Audit A, abgleich-souls Abschnitt 3). Owner 2026-10-29: teilweise global.

## What
Vorher in der Claude-Code-Doku prüfen, ob projektlokale `autoMode`-Einträge zusammengeführt werden. Danach gliedern: projektneutrale Zeilen global, BudgetBuddy- und Souls-Blöcke mit Pfadbezug (oder projektlokal, falls unterstützt).

## Acceptance criteria
- [ ] Kein globaler Satz nennt BudgetBuddy als einziges trusted repo.
- [ ] BudgetBuddy-spezifische soft_deny-Einträge (`git push deploy`, `deploy-rules.sh --clear`) wirken weiter.

## Notes
Migrationsschritt G. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt. Änderung außerhalb des Repos, nur durch oder mit dem Owner.
