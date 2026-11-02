---
id: budgeting-v4ht9
title: "ADRs zur Budget-Seite: Targets über BB, Pause nur in BB, freie Monate, Farbrollen mit Gelb und neutralen Knöpfen"
status: backlog
type: decision
context: budgeting
created: 2026-11-02
completed:
depends_on: [budgeting-q7m2c]
blocks: [budgeting-g8s3w]
tags: [budget, adr, target, styleguide]
related_adrs: [0063, 0065, 0069]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: []
---

## Why
Das Gesamtkonzept enthält Entscheidungen, die über einen Task hinausgehen und ohne ADR im Code verloren gingen. Das Ergebnis der Probe budgeting-q7m2c fließt ein.

## What
- **Targets über BB:** YNAB führt, BB bearbeitet und zieht nach. Nur Arten, die YNAB kennt. Beantwortet die offene Frage "Wer führt die Targets?" für die Spiegel-Phase.
- **Pause nur in BB:** Je Kategorie und Monat, gilt wenn in BB oder YNAB gesetzt, endet mit dem Monat. Ausnahme von "BB entscheidet, YNAB wird nachgezogen", weil `goal_snoozed_at` nicht schreibbar ist.
- **Freie Monate:** Monatswert statt `BudgetMonthChoice`, Grenzen aus YNAB, Fähigkeiten je Monatsart.
- **Farbrollen im Styleguide:** Gelb als neuer Token (offen, kein Fehler), neutrale Standard-Knöpfe statt Teal, Teal für Ausgewähltes und Navigation, Ausnahme für Rot und Gelb in einer Budget-Zeile, kein Pink auf der Budget-Seite.

## Acceptance criteria
- [ ] Je Thema ein ADR unter `.agentheim/knowledge/decisions/` mit Kontext, Entscheidung und Folgen.
- [ ] Vision und README budgeting verweisen bei den offenen Fragen auf die ADRs.

## Notes
Inhalte: `docs/ux/budget/konzept.md`. Umsetzung des Styleguides liegt in budgeting-g8s3w.
