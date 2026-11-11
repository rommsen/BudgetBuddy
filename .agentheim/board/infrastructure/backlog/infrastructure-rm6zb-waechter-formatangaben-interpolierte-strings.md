---
id: infrastructure-rm6zb
title: "Wächter gegen Formatangaben in interpolierten Strings unter src/Client und src/Shared"
status: backlog
type: chore
context: infrastructure
created: 2026-11-11
completed:
depends_on: []
blocks: []
tags: [captured, laufend, fable, waechter]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Fable übersetzt `$"{x:D2}"` nicht, im Client entsteht `'%P(D2)'`. Die .NET-Tests bleiben grün, Browser-Tests mit
gemockter API merken es nicht. Erstes Vorkommen: Fix af94b183 (der Monat der Übersicht ging nach design-system-k6kfw
als `'%P(D4)-%P(D2)-01'` an den Server, die Karte „Monat“ war in der Produktion kaputt).

## What
Ein Skript im Stil von `scripts/token-guard.mjs`, das in `npm run test:scripts` läuft und Formatangaben in
interpolierten Strings unter `src/Client` und `src/Shared` ablehnt. Gegenbeweis eingebaut: Fixtures, die anschlagen
müssen (`$"{x:D2}"`, `$"{a.B:N2}"`), und solche, die durchgehen müssen (`$"{x}"`, `{{` als Escape, Doppelpunkt
im Text außerhalb von Klammern).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-11-11 (aus `learn`, von Roman freigegeben) — raw, unrefined. Needs a
`modeling` refine pass before it can be promoted. Die Regel steht in `.claude/rules/fable-pitfalls.md`, Abschnitt
„Formatangaben in interpolierten Strings“, samt Grep-Check.
