---
id: categorization-s2cdw
title: "Kategorie-Ids in YnabTransaction und OrderIdSuggestion sind nackte Strings statt YnabCategoryId"
status: backlog
type: refactor
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
Verletzte Regel: .claude/rules/semantic-messaging.md, Fachbegriffe als Typen

## What
Fundstelle: `src/Shared/Domain.fs:507`

Auch `OrderIdSuggestion.CategoryId` (src/Server/OrderIdMatcher.fs:9). Der Decoder in src/Server/YnabClient.fs:165 liest mit `Decode.guid |> YnabCategoryId`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
