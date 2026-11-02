---
id: budgeting-v4ht9
title: "ADRs zur Budget-Seite: Targets über BB, Pause nur in BB, freie Monate, Farbrollen mit Gelb und neutralen Knöpfen"
status: done
type: decision
context: budgeting
created: 2026-11-02
completed:
depends_on: [budgeting-q7m2c]
blocks: [budgeting-g8s3w]
tags: [budget, adr, target, styleguide]
related_adrs: [0063, 0065, 0069, 0072, 0073, 0074, 0075]
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

## Verifier note (iteration 1)
ADR 0073 legt fest, die Pause sei keine Budget-Bewegung; das steht nicht im Konzept und widerspricht dem Prototyp. Die Aufzeichnung gehört wie in ADR 0072 zu budgeting-t6p8z. Das README_DELTA lässt die alte offene Frage (README Zeile 102) unverändert neben der neuen stehen und muss sie per replace ablösen. ADR 0074: "einen Weg zurück gibt es nicht" auf "keinen Weg zurück gefunden" abschwächen, "Die Seite zeigt Romans Reihenfolge" an die ausstehende Bestätigung binden. Outcome und budgeting-w4k9r sind überholt, der Vision-Verweis geschieht direkt auf main.

## Outcome

Vier ADRs festgehalten, die Entscheidungen stammen von Roman (`docs/ux/budget/konzept.md`), die Grundlagen aus budgeting-q7m2c:

- `0072-targets-ueber-bb-ynab-fuehrt.md`: YNAB führt, BB bearbeitet und zieht nach, nur geänderte Felder, `goal_frequency` nur beim echten Wechsel der Art mit Warnung.
- `0073-pause-nur-in-bb.md`: Pause je Kategorie und Monat in BB, bewusste Ausnahme von "BB entscheidet, YNAB wird nachgezogen".
- `0074-freie-monate-aus-dem-ynab-fenster.md`: Monatswert statt `BudgetMonthChoice`, Fenster aus `GET /months`, Fähigkeiten je Monatsart. Die Reihenfolge aus `GET /categories` steht dort bedingt, weil Romans Bestätigung noch aussteht.
- `0075-farbrollen-gelb-und-neutrale-knoepfe.md`: Gelb, neutrale Knöpfe, Teal, Ergänzung zu ADR 0049.

Die Nummern sind vorläufig. Die Querverweise im Text nennen 0072 und 0074 und müssen beim Umnummerieren mitgehen.

README budgeting und Vision wurden von Hand auf ADR 0072 umgestellt, weil das Gate ein replace in "Open questions" ablehnt.

Festgelegt wurde auch, was das Konzept nur nahelegt: Eine YNAB-Pause lässt sich in BB nicht aufheben (ADR 0073). Bewusst offen: Aufzeichnung und Fehlschlag von Target-Änderungen liegen bei budgeting-t6p8z, die Umsetzung von `Button.secondary` bei budgeting-g8s3w.
