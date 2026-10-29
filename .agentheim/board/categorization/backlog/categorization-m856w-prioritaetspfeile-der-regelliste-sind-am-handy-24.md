---
id: categorization-m856w
title: "Prioritätspfeile der Regelliste sind am Handy 24 mal 20 px groß und liegen neben dem Zeilen-Tap"
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
Befund aus `/architecture-review categorization` am 2026-10-29, Schwere **hoch**.
Verletzte Regel: standards/frontend/styleguide.md §4 (Touch-Targets mindestens 48 px)

## What
Fundstelle: `src/Client/Components/Rules/View.fs:94`

Ein knapp verfehlter Tipp öffnet den Bearbeiten-Dialog (Zeile 211/212) statt die Regel zu verschieben. Unterhalb `md` `TouchTargets.minSize`, etwa über `Button.iconButton`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
