---
id: categorization-kpsp8
title: "Dieselbe Regeldatei lässt sich kein zweites Mal importieren, weil die View die Datei selbst liest"
status: backlog
type: bug
context: categorization
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Befund aus `/architecture-review categorization` am 2026-10-29, Schwere **mittel**.
Verletzte Regel: .claude/rules/elmish-state.md, "Side Effects NUR über Cmd"

## What
Fundstelle: `src/Client/Components/Rules/View.fs:613`

Der `FileReader` läuft in der View, `input.value` wird nicht zurückgesetzt. Die View dispatcht nur `ImportFileChosen of File`, das Lesen läuft als Cmd, danach das Input zurücksetzen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
