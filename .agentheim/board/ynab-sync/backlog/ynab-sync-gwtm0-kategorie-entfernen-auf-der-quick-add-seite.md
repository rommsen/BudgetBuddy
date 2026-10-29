---
id: ynab-sync-gwtm0
title: "Kategorie entfernen auf der Quick-Add-Seite ist 42 px breit und eine Textglyphe"
status: backlog
type: bug
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
Verletzte Regel: standards/frontend/styleguide.md §4 (Touch-Targets), §5 (Icons)

## What
Fundstelle: `src/Client/Views/QuickAddPage.fs:177`

Feldhöhe etwa 44 px (styles.css:2428, 2447). Feld und Knopf auf 48 px, `Icons.x`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
