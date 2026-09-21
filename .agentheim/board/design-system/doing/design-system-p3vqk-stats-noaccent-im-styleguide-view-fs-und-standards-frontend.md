---
id: design-system-p3vqk
title: "Stats.NoAccent im Styleguide/View.fs und standards/frontend/styleguide.md nachziehen"
status: doing
type: chore
context: design-system
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [design-system, styleguide, stats]
related_adrs: [0035]
related_research: []
prior_art: [ledger-w9skm]
---

## Why

`ledger-w9skm` hat `Stats.fs` um einen siebten `StatAccent`-Fall `NoAccent`
erweitert (ADR 0035): der Gradient-Akzentstreifen von `Stats.view` lässt sich
jetzt ganz abschalten statt nur umgefärbt zu werden. Analog zu
`design-system-t3vqr` (derselbe Befund für `Money.accountBalance`/
`accountListRow` aus `ledger-x0yga`): die lebende Stilrichtlinie kennt die neue
Variante noch nicht.

## What

- `standards/frontend/styleguide.md`: `NoAccent` im Stats-Abschnitt erwähnen
  (kurz, analog zu den bestehenden `Accent`-Werten).
- `src/Client/Components/Styleguide/View.fs`: die Stats-Demo-Karten um ein
  Beispiel mit `Accent = NoAccent` ergänzen, damit die `/styleguide`-Route
  weiterhin *alle* real existierenden Varianten zeigt (das ist ihr
  Existenzgrund: "weil sie die echten Komponenten rendert, kann sie nicht
  driften").

## Acceptance criteria

- [ ] `standards/frontend/styleguide.md` nennt `NoAccent` im Stats-Abschnitt.
- [ ] `/styleguide` zeigt eine Stats-Karte ohne Akzentstreifen.
- [ ] `dotnet build`, `npm run build` grün.

## Notes

Kein funktionaler Code-Pfad betroffen; reine Dokumentations-/Demo-Lücke.