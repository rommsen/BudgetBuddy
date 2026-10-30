---
id: infrastructure-q7d2m
title: "Alte Planungsdokumente unter docs/ mit Verweisen auf gelöschte Agents und standards aufräumen"
status: done
type: chore
context: infrastructure
created: 2026-10-29
completed: 2026-10-30
depends_on: [infrastructure-cbr0r]
blocks: []
tags: [harness]
related_adrs: []
related_research: [harness-audit]
prior_art: [infrastructure-vm8zz, infrastructure-cbr0r]
---

## Why
Nach M9 und M10 verweisen `docs/MIGRATION-PLAN-SKILLS.md` (viele `standards/...`-Dateien, Skills), `docs/LEARNINGS.md` (qa-milestone-reviewer mit Übergabe an red-test-fixer, MILESTONE-PLAN) und `docs/FRONTEND-ARCHITECTURE-MILESTONES.md` (Milestone-QA-Ablauf) auf gelöschte Dateien und Agents. Sie lagen außerhalb des Auftrags von M10.

## What
Entschieden je Datei:

- `docs/MIGRATION-PLAN-SKILLS.md`: löschen. Plan vom Dezember 2025, abgeschlossen oder überholt, 29 Verweise auf `standards/`-Dateien, kein Verweis von außen.
- `docs/FRONTEND-ARCHITECTURE-MILESTONES.md`: löschen. Alle Milestones sind von Dezember 2025 und erledigt. Verweise gibt es nur noch aus dem eingefrorenen `diary/development.md`.
- `docs/LEARNINGS.md`: behalten, Abschnitt "Agent Workflows That Work" kürzen: `red-test-fixer` und die Übergabe dorthin entfallen, der Verweis auf `/docs/MILESTONE-PLAN.md` (Zeile 313) wird zu "in einer Milestone-Datei" oder entfällt.
- `README.md`, Tabelle "Documentation": die sechs toten Links ersetzen durch das, was es gibt (`standards/README.md`, `standards/frontend/overview.md`, `standards/backend/overview.md`, `docs/bot-api.md`). Zeilen ohne Gegenstück entfallen.

`docs/UI-REFACTORING-MILESTONES.md` bleibt: sein Kopf markiert es schon als historisch, und `diary/development.md` wird nicht angefasst.

## Acceptance criteria
- [ ] `docs/MIGRATION-PLAN-SKILLS.md` und `docs/FRONTEND-ARCHITECTURE-MILESTONES.md` sind gelöscht.
- [ ] `grep -rnE 'red-test-fixer|code-reviewer|MILESTONE-PLAN' docs README.md` findet nichts.
- [ ] Jede Pfadangabe `standards/....md` unter `docs/` und in `README.md` zeigt auf eine existierende Datei.
- [ ] Jeder relative Link in `README.md` zeigt auf eine existierende Datei.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Refine am 2026-10-30. Gelöschtes bleibt in der Git-Historie. `README.md` ist englisch und bleibt es.

## Outcome

`docs/MIGRATION-PLAN-SKILLS.md` und `docs/FRONTEND-ARCHITECTURE-MILESTONES.md` sind gelöscht. Vor dem Löschen geprüft: Verweise gab es außerhalb von `.agentheim/` nur noch aus dem eingefrorenen `diary/development.md`, das unverändert bleibt.

`docs/LEARNINGS.md`: Im Abschnitt "Agent Workflows That Work" entfallen `red-test-fixer` und die Übergabe dorthin. In "Milestone-Based Development" heißt es jetzt "in a milestone file" statt `/docs/MILESTONE-PLAN.md`.

`README.md`, Tabelle "Documentation": Die sechs toten Links sind ersetzt durch vier Zeilen auf `standards/README.md`, `standards/frontend/overview.md`, `standards/backend/overview.md` und `docs/bot-api.md`. Persistence, Deployment und Tailscale hatten kein Gegenstück und entfallen.

Prüfung: Der grep aus den Akzeptanzkriterien findet nichts, alle `standards/....md`-Angaben unter `docs/` und in `README.md` sowie alle relativen Links in `README.md` zeigen auf existierende Dateien. `./verify.sh` meldet `VERIFY: PASS` (dotnet 2695 grün, node 329 grün).
