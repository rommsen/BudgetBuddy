---
id: ledger-vqxvc
title: "Build-Warnung FS0025: OpenFilterPopover deckt den neuen Chip WithoutCategoryChip nicht ab"
status: done
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

Aus dem dismissten Duplikat `ledger-eng59` übernommen: der Chip löst heute
`ToggleWithoutCategory` aus (`src/Client/Components/Ledger/View.fs` um Zeile 1116) und
öffnet kein Popover, der fehlende Fall wird also im Normalbetrieb nicht erreicht.

## Outcome

In `src/Client/Components/Ledger/State.fs` deckt der Match in `OpenFilterPopover` jetzt `WithoutCategoryChip -> model` ab (Umschalter ohne Popover, ADR 0048). Weitere Matches über ChipId im Client (View.fs) waren bereits vollständig. Regressionstest in `src/Tests/LedgerPageTests.fs`: OpenFilterPopover WithoutCategoryChip wirft nicht, setzt OpenPopover, lässt Filters unverändert und liefert kein Cmd.

Läufe: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 1280 bestanden, 7 übersprungen, 0 Fehler; `npm run build` ok; `npm run guard:classes` ok; `npm run test:scripts` 120/120 (ein erster Lauf hatte einen einmaligen Seiten-Timeout, der Wiederholungslauf war grün).
