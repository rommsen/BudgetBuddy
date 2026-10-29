---
id: ynab-sync-x708m
title: "Die Quick-Add-Seite baut den Formularzustand in der View und schickt ihn ungeprüft ins update"
status: backlog
type: refactor
context: ynab-sync
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
Verletzte Regel: CLAUDE.md MVU; .claude/rules/semantic-messaging.md (Absicht statt technischer Setter)

## What
Fundstelle: `src/Client/Views/QuickAddPage.fs:83`

`UpdateQuickAdd of QuickAddFormState` (src/Client/State.fs:652), auch für Kategorie-Wahl und Picker (Zeilen 172, 179, 262). Fachliche Msgs wie `QuickAddCategoryChosen of YnabCategoryId`, `QuickAddCategoryCleared`, `QuickAddAmountEntered`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
