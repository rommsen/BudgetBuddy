---
id: design-system-q7rkm
title: DS-Bausteine für ruhige Listen (Money neutral, Table-Zellrollen, Container ohne Breitenbegrenzung, schlanker PageHeader)
status: backlog
type: feature
context: design-system
created: 2026-10-18
completed:
depends_on: []
blocks: [ledger-n4vte]
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
ADR 0049 (Ruhige Listen) legt fest, dass Farbe in wiederkehrenden Zeilen ein Signal ist.
Beträge und Kategorien bleiben neutral, die Hierarchie entsteht über Größe und Gewicht.
Die DS-Bausteine geben heute andere Standardwerte vor: `Table.td` setzt keine
Schriftgröße (16px grau), `Money.view` färbt immer rot oder grün mit Glow, `Container.view`
begrenzt auf `max-w-7xl`. Ohne eigene Varianten baut jede View ihre Ausnahme selbst.

## What
- `Money` bekommt eine neutrale Listenvariante: `text-primary`, Mono 500, `tabular-nums`,
  positiv Teal, kein Glow.
- `Table` bekommt Zellrollen mit den Werten aus der Styleguide-Tabelle "Ruhige Listen"
  (§2): Payee (primär), Datum, Kategorie (sekundär), Memo (gedämpft), Betrag
  (rechtsbündig).
- `Primitives.Container` bekommt eine Variante ohne Breitenbegrenzung.
- `PageHeader` bekommt eine schlanke Variante für Listen-Seiten.
- Die `/styleguide`-Route zeigt die neuen Varianten.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-18 — raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Quellen: `.agentheim/knowledge/decisions/0049-ruhige-listen-farbe-als-signal.md`,
`standards/frontend/styleguide.md` §2 "Ruhige Listen".
