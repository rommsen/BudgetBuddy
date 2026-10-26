---
id: design-system-y2bdj
title: "Lebender Styleguide zeigt die Knopfzeile des Split-Editors mit Barabhebung (SplitEditor.lineActions)"
status: backlog
type: chore
context: design-system
created: 2026-10-26
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Mit ledger-s8t3s kam die DS-Funktion `SplitEditor.lineActions` (Zeile hinzufügen plus Barabhebung) dazu. Der lebende Styleguide `/styleguide` rendert aber nur `SplitEditor.addLineButton` und bildet die echte Knopfzeile damit nicht ab.

## What
In `src/Client/Components/Styleguide/View.fs` (um Zeile 1159, Galerie-Karte des Split-Editors) die Knopfzeile `SplitEditor.lineActions` zeigen statt nur `addLineButton`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-26, raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Herkunft: nicht blockierender Hinweis des Verifiers von ledger-s8t3s.
