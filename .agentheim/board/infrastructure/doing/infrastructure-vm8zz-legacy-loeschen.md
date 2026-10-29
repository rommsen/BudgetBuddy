---
id: infrastructure-vm8zz
title: "Legacy löschen: fsharp-*-Skills, tailscale-deploy, web-animation-design, backlog.md, MILESTONE-PLAN, reviews, template, legacy, next-step, salvage"
status: doing
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-psdcb]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Alle genannten Teile ohne Nutzung seit Monaten (Audit A Abschnitt 9). Souls E5.

## What
Vor dem Löschen nicht herleitbares Wissen aus `fsharp-*`, `tailscale-deploy` und `standards/` retten (Ziel: Test-Doktrin, BC-README, CLAUDE.md-Entwurf); jeder gerettete Bezeichner per grep gegen den Code geprüft.

## Acceptance criteria
- [ ] Die genannten Dateien und Ordner sind entfernt; `rules.yml` bleibt (Produktdaten).
- [ ] Eine Liste des geretteten Wissens mit Zielort liegt im Task-Outcome.
- [ ] Kein Verweis in CLAUDE.md, Rules, Agents oder Skripten zeigt auf Gelöschtes (grep).

## Notes
Migrationsschritt M9. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.

