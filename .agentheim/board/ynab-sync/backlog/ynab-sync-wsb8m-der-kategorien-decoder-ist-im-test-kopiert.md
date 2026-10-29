---
id: ynab-sync-wsb8m
title: "Der Kategorien-Decoder ist im Test kopiert statt aus dem Produktionscode aufgerufen"
status: backlog
type: chore
context: ynab-sync
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
Verletzte Regel: .claude/rules/test-doctrine.md, Kein Test für Punkt 6

## What
Fundstelle: `src/Tests/YnabClientTests.fs:448`

Den Inline-Decoder aus `getCategories` (src/Server/YnabClient.fs:399-410) nach `Decoders` ziehen und den echten testen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
