---
id: categorization-cgq35
title: "Regelseite am Handy und per Tastatur"
status: backlog
type: bug
context: categorization
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
Gruppe 2 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **hoch**. Trefferflächen unter 48 px, ein Menü, das nur über Fokus öffnet, ein zentriertes Modal am Handy und Texte, die dem Styleguide nicht folgen.

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Client/Components/Rules/View.fs:94` | standards/frontend/styleguide.md §4 (Touch-Targets mindestens 48 px) | hoch | Prioritätspfeile der Regelliste sind am Handy 24 mal 20 px groß und liegen neben dem Zeilen-Tap.
- `src/Client/Components/Rules/View.fs:569` | standards/frontend/styleguide.md §4 und §5 (Button, Icons) | mittel | Der Overflow-Knopf der Regelseite ist 32 px groß und hat keine Beschriftung.
- `src/Client/Components/Rules/View.fs:577 und :583` | standards/frontend/styleguide.md §5 (FilterPopover, BottomSheet, Button: jede klickbare Aktion), §7 | mittel | Das Menü der Regelseite öffnet nur über Fokus und seine Einträge sind nicht per Tastatur bedienbar.
- `src/Client/Components/Rules/View.fs:294` | standards/frontend/styleguide.md §5 (Modal nur Desktop, mobile Auswahl über BottomSheet) | mittel | Der Regel-Dialog läuft am Handy als zentriertes Modal mit Dropdown für die Kategorie.
- `src/Client/Components/Rules/View.fs:427` | standards/frontend/styleguide.md §8 (UI-Texte deutsch) | mittel | Englischer Platzhalter im Regel-Dialog.
- `src/Client/Components/Rules/View.fs:400` | styleguide §5 ErrorDisplay.warning | niedrig | selbst gebauter Warnkasten "Keine Kategorien geladen" durch `ErrorDisplay.warning` ersetzen.
- `src/Client/Components/Rules/View.fs:311` | styleguide §8 konsistente Begriffe | niedrig | "z.B." (311, 368 bis 370) zu "z. B.", "..." (393, 472, 648) zu "…".
- `src/Client/Components/Rules/View.fs:476` | styleguide §8 Buttons als Verb | niedrig | "Test" zu "Testen".
- `src/Client/Components/Rules/View.fs:648` | styleguide §6 Skeletons | niedrig | `NotAsked` und `Loading` zeigen dasselbe Skeleton.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-30, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn der Owner das will.
