---
id: ledger-vqxvc
title: "Build-Warnung FS0025: OpenFilterPopover deckt den neuen Chip WithoutCategoryChip nicht ab"
status: backlog
type: bug
context: ledger
created: 2026-10-18
depends_on: []
blocks: []
tags: [frontend, kassenbuch, warning]
related_adrs: [0048]
related_research: []
prior_art: [ledger-saqsk]
---

## Why

`ledger-saqsk` hat den Chip-Fall `WithoutCategoryChip` eingeführt. Der Match über `chip` im
Zweig `OpenFilterPopover` in `src/Client/Components/Ledger/State.fs` (Zeile 333) deckt ihn
nicht ab: `dotnet build` meldet seitdem `warning FS0025` (unvollständige
Musterübereinstimmung). Der Chip hat kein Popover und dürfte `OpenFilterPopover` nie
auslösen; sollte es doch passieren, wirft der Reducer eine `MatchFailureException`. Vorher
war der Build warnungsfrei.

## What

Den Fall wie `NeedsReviewChip` behandeln (`| WithoutCategoryChip -> model`), damit der
Build wieder ohne Warnung läuft. Dabei die übrigen Matches über den Chip-Typ im Client auf
denselben Fehler prüfen.

## Acceptance criteria

- [ ] `dotnet build` meldet 0 Warnungen.
- [ ] Reducer-Test: `OpenFilterPopover WithoutCategoryChip` lässt das Model unverändert und wirft nicht (ohne Api-Cmd, siehe Reducer-Test-Regel).
- [ ] `dotnet test` grün.

## Notes

Gefunden beim Abschluss-Build auf main am 2026-10-18 22:2x. Der Verifier von `ledger-saqsk` hat die Warnung nicht gemeldet.
