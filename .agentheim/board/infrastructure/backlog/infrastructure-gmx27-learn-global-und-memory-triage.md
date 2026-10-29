---
id: infrastructure-gmx27
title: "learn-Skill global anlegen, Auto-Memory triagieren und abschalten"
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
Souls E8 und fest: Auto-Memory aus. Owner 2026-10-29: learn global. Die Memory-Detaildatei hat 48 KB, überwiegend Conductor-Wissen zu agentheim 0.9.2 bis 0.9.4 (Audit A).

## What
`~/.claude/skills/learn/` aus der Souls-Vorlage, projektneutral (Gate-Kommando aus dem BC-README statt `verify.sh` fest). Mit der Souls-Session abstimmen, ob deren Kopie entfällt. Memory nach Tabelle 4 in Bericht A verteilen; Conductor-Wissen ins gemeinsame Upstream-Sammel-Issue. Danach `autoMemoryEnabled: false` und Memory-Ordner archivieren.

## Acceptance criteria
- [ ] `learn` ist in beiden Projekten verfügbar, nur eine Kopie existiert.
- [ ] Jeder Memory-Eintrag hat ein Ziel (Wächter, Skript, README, Upstream, entfällt) und ist dort angekommen.
- [ ] Eine frische Session lädt kein Auto-Memory mehr (`/memory`).

## Notes
Migrationsschritt M7. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt.
