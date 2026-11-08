---
id: budgeting-t7dka
title: "Targets anwenden übergeht in BB deaktivierte Kategorien"
status: backlog
type: bug
context: budgeting
created: 2026-11-08
completed:
depends_on: [budgeting-sn2sn]
blocks: []
tags: [kategorien, targets]
related_adrs: [0088]
related_research: []
prior_art: [budgeting-sn2sn]
---

## Why
Seit budgeting-sn2sn kann eine Kategorie in BB deaktiviert werden. Die Vorschau und das Anwenden von "Targets
anwenden" bauen den Monat aber über `toBudgetMonthWith` ohne `applyDeactivation` (`CommandHandlers.preparedMonth`,
`previewWithLedger`). Hat eine deaktivierte Kategorie noch ein Target mit Fehlbetrag, weist BB ihr Geld zu, obwohl
sie in der Budget-Liste fehlt. Das verfälscht Geld (Backlog-Disziplin: eigenes Ticket).

## What
- Vorschau und Anwenden lassen in BB deaktivierte Kategorien weg, auch wenn sie im Monat Aktivität oder Verfügbares
  haben (dann stehen sie zwar in der Liste, sollen aber kein Geld über Targets bekommen), oder sie stehen getrennt
  wie pausierte unter "übersprungen". Welche Variante, entscheidet die Verfeinerung.

## Acceptance criteria
- [ ] Handler-Test: eine deaktivierte Kategorie mit Target und Fehlbetrag erscheint nicht in den Zeilen der Vorschau.
- [ ] Handler-Test: Anwenden schreibt für sie nichts nach YNAB.

## Notes
- Deaktivierter Zustand: `Budgeting.Persistence.loadCategoryLayoutWith`, Regel `applyDeactivation` in `src/Shared/Budgeting.fs`.
- Parallel lief budgeting-e2z7v an den Targets; Konflikte in `src/Server/Budgeting/CommandHandlers.fs` beachten.