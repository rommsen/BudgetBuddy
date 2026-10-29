---
id: infrastructure-jy5ca
title: "Serena entfernen: MCP-Eintrag, .serena, Freigaben, Rule und CLAUDE.md-Pflichtblock"
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
Subagents 0 Serena-Aufrufe, 2.036 Bash-Zugriffe auf .fs; `search_for_pattern` existiert im Kontext claude-code nicht (Audit A, geprüft). Souls E2.

## What
MCP-Eintrag in `~/.claude.json` (Owner), `.serena/`, Serena-Freigaben in `.claude/settings.local.json`, `.claude/rules/serena-tools.md`, CLAUDE.md:1-27.

## Acceptance criteria
- [ ] Keine `mcp__serena__*`-Tools in einer frischen Session.
- [ ] CLAUDE.md und Rules erwähnen Serena nicht mehr.

## Notes
Migrationsschritt M8. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt.
