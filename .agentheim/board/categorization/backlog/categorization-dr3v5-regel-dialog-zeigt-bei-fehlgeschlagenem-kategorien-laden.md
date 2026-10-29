---
id: categorization-dr3v5
title: "Regel-Dialog zeigt bei fehlgeschlagenem Kategorien-Laden oder Deep-Link auf /rules eine leere Liste mit falschem Hinweis"
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
Verletzte Regel: .claude/rules/elmish-state.md, "Boolean Flags für Async State", Failure anzeigen; standards/frontend/styleguide.md §8 (Ursache benennen)

## What
Fundstelle: `src/Client/Components/Rules/State.fs:201`

`CategoriesLoaded (Error _)` wird verworfen, und src/Client/State.fs:550-566 lädt nicht, solange `Settings` nicht `Success` ist. Der Dialog sagt dann "Bitte zuerst YNAB konfigurieren" (src/Client/Components/Rules/View.fs:404). `Categories` als RemoteData, Failure mit Wiederholen, Laden nachholen, sobald die Einstellungen da sind.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
