---
id: design-system-zpq7t
title: "ADR 0011 nennt positive Available-Beträge grün, Code und Styleguide färben sie neutral"
status: backlog
type: decision
context: design-system
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
Verletzte Regel: .agentheim/knowledge/decisions/0011-category-available-in-assignment-pickers.md Decision 3 gegen src/Client/DesignSystem/Money.fs:225 und standards/frontend/styleguide.md §2

## What
Fundstelle: `src/Client/DesignSystem/BottomSheet.fs:36`

Ergänzungs-ADR, die auf die ruhige Picker-Zeile verweist; Doc-Kommentar ("green >0"), src/Client/Views/QuickAddPage.fs:97 und concepts/category-picker.md Z. 39 nachziehen. Für den Menschen am Bildschirm: die offene Abnahme des Available-Betrags am Handy mit echten Werten schließt die neutrale Färbung ein.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
