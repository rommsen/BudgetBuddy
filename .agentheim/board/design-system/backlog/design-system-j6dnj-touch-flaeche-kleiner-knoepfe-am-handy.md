---
id: design-system-j6dnj
title: "Touch-Fläche der kleinen Knöpfe am Handy auf mindestens 48 px bringen (Split-Editor: Barabhebung, Zeile hinzufügen)"
status: backlog
type: bug
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
Der Verifier von ledger-s8t3s hat gemessen: "Barabhebung" und "Zeile hinzufügen" im Split-Editor des Kassenbuchs sind am Handy nur 36 px hoch. Der Styleguide (`standards/frontend/styleguide.md` §4) verlangt mobil mindestens 48 px.

## What
Die Höhe kommt aus der DS-Größe `Button.Small` (`min-h-[36px] md:min-h-0`). Touch-Fläche der betroffenen Knöpfe am Handy auf mindestens 48 px bringen, entweder im Split-Editor oder in der DS-Größe selbst (dann alle Verwendungen von Small mitprüfen).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-26, raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Herkunft: nicht blockierender Hinweis des Verifiers von ledger-s8t3s.
