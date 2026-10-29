---
id: infrastructure-psdcb
title: "Subagents auf Opus erzwingen und Baseline-Replay der drei Referenz-Tasks mit dem heutigen Harness"
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
77 Worker-Dispatches liefen ohne `model` auf Sonnet (Bericht A). Vor jeder Harness-Änderung braucht es eine Baseline, sonst ist keine Verschlankung messbar (Souls E1, E12).

## What
`.claude/settings.json` mit env `CLAUDE_CODE_SUBAGENT_MODEL=opus` und `CLAUDE_CODE_SUBAGENT_MODEL_FORCE=1`. `scripts/baseline-replay.sh` nach Vorlage `souls/scripts/baseline-replay.sh`, angepasst an BudgetBuddy. Baseline der Referenz-Tasks `ledger-ssmp3`, `design-system-j6dnj`, `ledger-ybm5x` im Zustand vor `work` auf dem Eltern-Commit, Ergebnis verworfen.

## Acceptance criteria
- [ ] `.claude/settings.json` existiert und enthält den env-Block; ein neu gestarteter Subagent meldet `claude-opus-*` im Transkript.
- [ ] `scripts/baseline-replay.sh <task-id>` legt einen Wegwerf-Worktree auf dem Eltern-Commit an und räumt ihn danach wieder ab (Symlinks zuerst).
- [ ] Für jede der drei Referenz-Tasks liegt ein Messprotokoll vor: Verifier-Verdict und Iterationen, Gate-Ergebnis, Input- und Output-Tokens, Laufzeit.

## Notes
Migrationsschritt M0. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt. Die Baseline-Läufe startet der Conductor; das Skript selbst kann ein Worker bauen, falls der Task gesplittet wird.

Stand 2026-10-29 (Conductor): `.claude/settings.json` mit Opus-env und `scripts/baseline-replay.sh` sind angelegt und per `--dry-run` geprüft (ledger-ssmp3: f1862dd -> cfb05ff, design-system-j6dnj: 7b82ca8 -> 897b13c, ledger-ybm5x: db6edad -> a0a8cab; unbekannte Id und Pfad außerhalb `replay-*` brechen mit Exit 1 ab). Offen: die drei Baseline-Läufe. Die env wirkt erst in einer neuen Claude-Code-Session.
