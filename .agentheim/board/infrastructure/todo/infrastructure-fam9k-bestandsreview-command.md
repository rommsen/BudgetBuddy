---
id: infrastructure-fam9k
title: "Bestandsreview-Command nach dem umgebauten Souls-/architecture-review"
status: todo
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-cbr0r]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Die schweren Befunde lagen außerhalb des einzelnen Diffs bzw. kamen aus Reviews ohne Regeln (Audit Zusammenfassung Abschnitt 2). Souls M18.

## What
`.claude/commands/architecture-review.md` nach Souls-Vorlage: Parameter BC oder Dimension, liest nur die neuen Regeln, repariert nichts, legt Befunde per quick-capture an.

## Acceptance criteria
- [ ] Der Command existiert und läuft für einen BC ohne Codeänderung.
- [ ] Befunde landen als Backlog-Tasks im richtigen BC.

## Notes
Migrationsschritt M18. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Erster Lauf je BC durch den Conductor nach M10.
