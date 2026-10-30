---
id: infrastructure-gmx27
title: "learn-Skill global anlegen, Auto-Memory triagieren und abschalten"
status: done
type: chore
context: infrastructure
created: 2026-10-29
completed: 2026-10-30
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
- [x] `learn` ist in beiden Projekten verfügbar, nur eine Kopie existiert.
- [x] Jeder Memory-Eintrag hat ein Ziel (Wächter, Skript, README, Upstream, entfällt) und ist dort angekommen.
- [ ] Eine frische Session lädt kein Auto-Memory mehr (`/memory`).

## Notes
Migrationsschritt M7. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt.

## Outcome

Triage nach `.agentheim/knowledge/research/harness-audit/triage-ergebnis.md`, vom Owner am 2026-10-30 freigegeben. Jeder Memory-Eintrag hat ein Ziel (W, R, U oder X):
- R1 steht in `docs/bot-api.md`, R2 in der infrastructure-README unter "Deploy prüfen", R3 in `.claude/rules/test-doctrine.md` Punkt 1.
- Zehn neue Upstream-Entwürfe B1 bis B10 stehen in `harness-audit/upstream-issues.md`, als Ergänzung zur Souls-Liste. Der Rest ist schon durch Wächter oder Skripte abgedeckt oder entfällt.

`learn` liegt global unter `~/.claude/skills/learn/SKILL.md`. Er ist projektneutral: Gate-Skript aus BC-README oder CLAUDE.md statt fest `verify.sh`, keine Lessons-Datei. Die Souls-Kopie unter `.claude/skills/learn/` entfällt, die Souls-Session ist informiert.

`autoMemoryEnabled: false` steht in `.claude/settings.json`, wie in Souls. Der Memory-Ordner ist nach `~/.claude/projects/-Users-romansachse-src-BudgetBuddy/memory.archiv-2026-10-30/` verschoben.

Nicht geprüft: AC 3 (eine frische Session lädt kein Auto-Memory). Dafür braucht es eine neue Session, die Prüfung gehört in die Baseline-Messung.
- AC 1 erfüllt am 2026-10-30: Souls hat seine Kopie entfernt (328d4a4), es gibt nur noch `~/.claude/skills/learn/`.
