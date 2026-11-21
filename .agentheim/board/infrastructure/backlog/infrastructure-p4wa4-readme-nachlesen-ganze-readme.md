---
id: infrastructure-p4wa4
title: README-Delta-Gate, Liste „ganz nachlesen“ über die ganze README
status: backlog
type: chore
context: infrastructure
created: 2026-11-21
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Wiederholter Fehler (learn 2026-11-21, von Roman freigegeben): Ein README_DELTA ersetzt die Bullets, um die es geht. Ein
anderer Bullet, der dasselbe Verhalten mitbeschreibt (meist das große Seiten-Bullet wie **Budget-Seite**), bleibt mit dem
alten Satz stehen. Prüfung 7 sucht nur seltene Begriffe aus dem ersetzten Text. Die Liste „ganz nachlesen“
(`findRereadEntries`, `REREAD_SECTIONS` in `scripts/readme-delta-check.mjs`) sieht nur Einträge unter Key commands, Key
events und Ereignisse.

Fundstellen: budgeting-yarty und budgeting-h2brj (18.11., Iteration 1), budgeting-m5pqn (21.11., Iteration 1 und 2),
budgeting-eh8xe (06.11., Iteration 2 und 3). Die damalige Regel in CLAUDE.md (10d9e48) hat nicht gegriffen.

## What
Die Liste „ganz nachlesen“ geht über alle Abschnitte der README. Ein Bullet landet darin, wenn er mit einem ersetzten
Bullet einen seltenen Backtick-Begriff teilt oder dessen fetten Kopf nennt (als **Kopf** oder als Wort), ausgenommen die
ersetzten Bullets selbst. Der Exit-Code bleibt unverändert. Der Worker beantwortet jeden Eintrag im OUTCOME mit „passt“
oder „nachgezogen“.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-21 — raw, unrefined. Needs a `modeling` refine pass before it can be promoted.

Vorschläge für die Kriterien (aus learn):
- Gegenbeweise in `scripts/readme-delta-check.test.mjs`.
- Die Deltas von budgeting-m5pqn (Iteration 1), budgeting-yarty und budgeting-h2brj nachgespielt: die Liste nennt jeweils
  den Bullet, der stehen blieb.
- Rauschen an einem großen Delta gezählt und im OUTCOME genannt.
- CLAUDE.md, Abschnitt „README-Deltas (Agentheim)“, im selben Schritt nachgezogen: „Die Liste „ganz nachlesen“ nennt jeden
  Bullet der README, der einen seltenen Begriff mit einem ersetzten Bullet teilt oder dessen fetten Kopf nennt“ statt der
  Beschränkung auf `## Key commands`, `## Key events` und `## Ereignisse`.
