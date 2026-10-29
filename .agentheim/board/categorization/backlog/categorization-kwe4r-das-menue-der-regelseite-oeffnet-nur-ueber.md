---
id: categorization-kwe4r
title: "Das Menü der Regelseite öffnet nur über Fokus und seine Einträge sind nicht per Tastatur bedienbar"
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
Verletzte Regel: standards/frontend/styleguide.md §5 (FilterPopover, BottomSheet, Button: jede klickbare Aktion), §7

## What
Fundstelle: `src/Client/Components/Rules/View.fs:577 und :583`

Das Menü hängt an `group-focus-within`; am Handy ist "Regeln neu laden" nur dort erreichbar. Die Einträge (Zeilen 583, 594, 604) sind `Html.a` ohne href. MVU-Zustand mit `FilterPopover` am PC und `BottomSheet.optionRow` am Handy, Einträge als Buttons. Für den Menschen am Bildschirm: öffnet ein Tipp auf "⋮" in iPhone Safari das Menü überhaupt?

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
