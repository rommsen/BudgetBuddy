---
id: infrastructure-q7d2m
title: "Alte Planungsdokumente unter docs/ mit Verweisen auf gelöschte Agents und standards aufräumen"
status: backlog
type: chore
context: infrastructure
created: 2026-10-29
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
Je Datei entscheiden: löschen (historisch, ohne Leser) oder Verweise auf den heutigen Stand ziehen (`.claude/rules/test-doctrine.md`, `standards/README.md`). Auch `README.md` im Repo-Wurzelverzeichnis verlinkt laut infrastructure-vm8zz nicht existierende `docs/`-Dateien.

## Acceptance criteria
- [ ] Kein Verweis unter `docs/` und in `README.md` auf gelöschte `standards/`-Dateien, `red-test-fixer`, `code-reviewer` oder `docs/MILESTONE-PLAN.md` (grep).