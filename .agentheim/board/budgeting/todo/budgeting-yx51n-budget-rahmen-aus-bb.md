---
id: budgeting-yx51n
title: "Budget-Rahmen aus BB: Monatsfenster und Kategorie-Platzierung als YNAB-Stand, der Rahmen der Budget-Seite liest ohne YNAB-Aufruf"
status: todo
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: []
blocks: [budgeting-ffjk9]
tags: [budget-seite, lesemodell, performance, ynab]
related_adrs: [0074, 0076, 0088, 0101]
related_research: []
prior_art: []
---

## Why
Teil 2 von 4 nach ADR 0101 (Ursprung budgeting-m5pqn, Roman 2026-11-20: „Die Seite muss instant da sein. Immer zuerst
BB.“). Der Rahmen (`Budgeting.QueryHandlers.frame`) kostet heute je Seitenaufruf zwei YNAB-Aufrufe: Monatsliste
(`GET /months`) und Kategorie-Reihenfolge (`getCategoriesPlaced`).

## What
- Zustand `YnabMonthWindow` (Fenster der Monate) und `YnabCategoryCatalog` (Platzierung der Kategorien), geschrieben
  beim Nachlesen des Rahmens; Schema über `initializeDatabase`, wiederholbar (ADR 0083: Zustand).
- `QueryHandlers.frame` liest nur die DB, dazu BBs Layout wie heute.
- Eine bestätigte BB-Zuweisung ungleich 0 im letzten Monat erweitert das Fenster um einen Monat (ADR 0074: die Grenze
  nicht selbst rechnen, sonst 404).
- Ohne Stand wird einmal live gelesen und festgehalten.
- Kategorien verwalten und Picker (`getCategories`) bleiben live, nicht Teil dieser Task.

## Acceptance criteria
- [ ] Handler-Test mit zählendem `BudgetFramePort`: `frame` mit gehaltenem Stand ruft `FetchMonths` und
      `FetchCategoryOrder` null mal.
- [ ] Handler-Test: ohne Stand je ein Aufruf, danach null.
- [ ] Domain-Test: eine bestätigte Zuweisung ungleich 0 im letzten Monat öffnet den nächsten; eine Zuweisung 0, eine
      nicht bestätigte (fehlgeschlagene) und eine in einem früheren Monat öffnen keinen.
- [ ] Handler-Test: eine neue Kategorie im Katalog steht am Ende ihrer Gruppe (ADR 0088 Punkt 3).
- [ ] README budgeting nachgezogen, Delta mit `npm run check:readme-delta` geprüft.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
- Unabhängig von budgeting-m5pqn baubar. Das Nachziehen des Rahmens (ab 5 min Alter) gehört zu budgeting-ffjk9.
