---
id: infrastructure-jy5ca
title: "Serena entfernen: MCP-Eintrag, .serena, Freigaben, Rule und CLAUDE.md-Pflichtblock"
status: done
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

## Outcome

Repo-Teil durch den Conductor (Commit d204816): `.serena/` (Projekt-Config und sechs Memories mit allgemeinem, aus dem Code ablesbarem Projektüberblick) gelöscht, `.claude/rules/serena-tools.md` gelöscht, Pflichtblock und Abschnitt "Tool Priority: Serena First" aus CLAUDE.md entfernt, Freigabe `mcp__serena__get_symbols_overview` aus `.claude/settings.local.json` entfernt. Owner-Teil: `claude mcp remove serena -s local` (projektlokaler Eintrag in `~/.claude.json` für BudgetBuddy entfernt; der Eintrag für `/Users/romansachse/src/both` bleibt unberührt).

Prüfung: `~/.claude.json` hat für BudgetBuddy keinen Serena-Eintrag mehr; CLAUDE.md, Rules und beide Settings-Dateien erwähnen Serena nicht mehr (grep). Ob eine frische Session keine `mcp__serena__*`-Tools mehr anbietet, zeigt sich beim nächsten Session-Start. Verbleibende Erwähnungen in `standards/global/claude-tools.md` und `.claude/agents/review-code.md` räumt M10 (infrastructure-cbr0r) ab.
