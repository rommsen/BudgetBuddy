---
id: design-system-vf94m
title: "Toast am Handy oben statt unten: der Toast mit Rückgängig verdeckt heute den Knopf „+ Buchung“ (Styleguide §7.13)"
status: done
type: bug
context: design-system
created: 2026-11-12
completed: 2026-11-19
depends_on: [design-system-001-styleguide, ledger-vf5h6]
blocks: []
tags: [review, captured, frontend]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Review-Lauf 2026-11-12 (a3f7eb1d..982bdf79), Fund von `design-quality`, außerhalb des geprüften Diffs, schon länger so. Am
Handy steht der Toast (etwa „Kategorie geändert … Rückgängig“) unten und verdeckt den schwebenden Knopf „+ Buchung“
(`ledger-capture-fab`). Styleguide §7.13 verlangt, dass ein Toast am Handy oben steht und nie ein Bedienelement verdeckt.
Roman hat am 2026-11-12 ein eigenes Ticket gewählt.

## What
Toast-Position am Handy nach §7.13; betrifft alle Seiten mit Toast und schwebendem Knopf (Kassenbuch, Übersicht).
Belege: Screenshots `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/ledger-dn4vy/vorher-ausgegraut-412.png` und `nachher-ausgegraut-412.png`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured raw, unrefined. Verfeinern mit `/agentheim:modeling`. Regressionstest als Invariante im Layout-Sweep (Toast verdeckt kein Bedienelement), mit Gegenbeweis.

Verknüpft 2026-11-19: hängt an der Toast-Regel aus der Design-Exploration ledger-vf5h6 (Durchgehen am Handy).

## Outcome
Abgelöst 2026-11-19 durch design-system-ytgnc (Rückmeldung am Handy ohne Toast, Regel R3 aus ledger-vf5h6): am Handy gibt
es keinen Toast mehr, „+ Buchung“ sitzt in der Meldungsleiste. Der Regressionstest (Sweep-Invariante „keine Rückmeldung
verdeckt ein Bedienelement“, mit Gegenbeweis) ist dort Kriterium. Kein Code in dieser Task.
