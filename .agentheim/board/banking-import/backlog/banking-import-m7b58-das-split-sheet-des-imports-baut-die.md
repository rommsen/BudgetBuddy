---
id: banking-import-m7b58
title: "Das Split-Sheet des Imports baut die Split-Bausteine des Design Systems nach"
status: backlog
type: refactor
context: banking-import
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
Verletzte Regel: standards/frontend/styleguide.md §2 (primäre Aktion über Button.primary), §5 (vorhandene DS-Komponente statt Inline-Feliz)

## What
Fundstelle: `src/Client/Components/SyncFlow/Views/SplitSheet.fs:49 und :168`

`DesignSystem/SplitEditor.fs` enthält Summe, Zeile, Rest, Zeile hinzufügen und Fußleiste. SplitSheet baut sie mit eigenen Klassen nach, "Speichern" ist eine Teal-Fläche (styles.css:2606). Auf die SplitEditor-Bausteine umstellen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
