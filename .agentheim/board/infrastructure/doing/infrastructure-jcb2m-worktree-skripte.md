---
id: infrastructure-jcb2m
title: "Worktree-Skripte: node_modules-Symlink anlegen und vor git worktree remove wieder löschen"
status: doing
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-dvy29]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Lehre aus Souls und Memory: Worker brauchen node_modules im Worktree; Teardown mit Symlink scheitert oder löscht zu viel.

## What
`scripts/worktree-prep.sh` und `scripts/worktree-teardown.sh` nach Souls-Vorlage.

## Acceptance criteria
- [ ] Prep legt den Symlink an, `./verify.sh` läuft danach im Worktree.
- [ ] Teardown löscht den Symlink vor `git worktree remove`, Schritte nicht per `&&` verkettet; das Haupt-`node_modules` bleibt erhalten.

## Notes
Migrationsschritt M4. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.

