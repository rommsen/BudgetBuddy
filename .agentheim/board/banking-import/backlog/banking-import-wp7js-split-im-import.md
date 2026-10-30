---
id: banking-import-wp7js
title: "Split im Import"
status: backlog
type: refactor
context: banking-import
created: 2026-10-30
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Gruppe 5 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **mittel**. Das Split-Sheet des Imports baut die Design-System-Bausteine nach, sperrt Speichern nicht und wird im Filter falsch eingeordnet.

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Client/Components/SyncFlow/Views/SplitSheet.fs:173` | standards/frontend/overview.md, Schaltflächen zeigen den Ladezustand und sind gesperrt | mittel | Speichern im Split-Sheet des Imports bleibt während des Aufrufs aktiv, ein Doppeltipp schickt zwei Aufrufe.
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs:49 und :168` | standards/frontend/styleguide.md §2 (primäre Aktion über Button.primary), §5 (vorhandene DS-Komponente statt Inline-Feliz) | mittel | Das Split-Sheet des Imports baut die Split-Bausteine des Design Systems nach.
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs:97` | standards/frontend/styleguide.md §4 (Touch-Targets), §5 (Icons) | mittel | Knöpfe im Split-Sheet des Imports sind schmaler als 48 px und nutzen Textglyphen.
- `src/Client/Components/SyncFlow/Views/TransactionList.fs:22` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1; .agentheim/knowledge/contexts/categorization/README.md, Szenarien und Tests | mittel | Aufgeteilte Buchungen landen im Import-Filter unter ohne Kategorie.
- `src/Client/Components/SyncFlow/State.fs:42` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 2 | mittel | Die Auswahl aller sichtbaren Import-Zeilen nutzt eine ungetestete Kopie des Filters.
- `src/Client/Components/SyncFlow/Views/SplitSheet.fs:52` | standards/frontend/overview.md prop.key | niedrig | stabile Id je `SplitDraftLine` statt Index-Key.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-30, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn der Owner das will.
