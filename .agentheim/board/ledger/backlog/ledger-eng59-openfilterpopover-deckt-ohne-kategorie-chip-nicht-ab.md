---
id: ledger-eng59
title: OpenFilterPopover deckt den Chip "Ohne Kategorie" nicht ab (Warnung FS0025)
status: backlog
type: bug
context: ledger
created: 2026-10-19
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`dotnet build` meldet seit Einführung des Chips "Ohne Kategorie" eine Warnung:
`src/Client/Components/Ledger/State.fs(333,19): warning FS0025: Unvollständige
Musterübereinstimmungen ... "WithoutCategoryChip"`. Im Work-Lauf vom 2026-10-18 bei jedem
Build aufgetaucht, aus keinem Diff dieses Laufs.

## What
Im `update`-Zweig `OpenFilterPopover chip` fehlt der Fall `WithoutCategoryChip` im
`match chip with`. Heute löst der Chip vermutlich `ToggleWithoutCategory` aus
(`View.fs` um Zeile 1116) und öffnet kein Popover, der Fall wird also wohl nie erreicht.
Kommt er doch an (Tastatur, künftiger Code), wirft der übersetzte Code zur Laufzeit eine
Match-Ausnahme. Den Fall ausdrücklich behandeln und die Warnung beseitigen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-19 — raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Hinweis für die Verfeinerung: Bug-Protokoll verlangt einen
Regressionstest (Reducer-Test auf `OpenFilterPopover WithoutCategoryChip`, der Pfad baut
keinen Api-Aufruf). Danach sollte `dotnet build` 0 Warnungen melden.
