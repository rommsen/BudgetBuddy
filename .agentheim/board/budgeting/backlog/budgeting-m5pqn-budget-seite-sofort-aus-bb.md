---
id: budgeting-m5pqn
title: "Budget-Seite sofort da: BB liest den Monat aus seinem eigenen Stand statt bei jedem Aufruf live aus YNAB"
status: backlog
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: [design-system-001]
blocks: []
tags: [budget-seite, lesemodell, performance, ynab]
related_adrs: [0065, 0083]
related_research: []
prior_art: [budgeting-zkbbd]
---

## Why
Roman (2026-11-20): „Budget-Seite ist nervig, dass die Budgets immer erst geladen werden müssen. Die Seite muss
instant da sein.“ Auf die Rückfrage, ob ein zuletzt bekannter Stand mit Abgleich im Hintergrund reicht: „Wir hatten
doch entschieden, dass Budgets führend sind. Also immer zuerst BB.“ (budgeting-zkbbd: BB führt das Budget, YNAB wird
Spiegel.)

## What
Heute fragt jeder Aufruf der Budget-Seite YNAB live ab:
- `getBudgetMonth` → `Budgeting.QueryHandlers.budgetMonthWithLedger` → `CommandHandlers.observeMonthWith` holt den
  Monat aus YNAB (Zuweisungen, verfügbar, Kategorien), dazu `targetRulesBestEffort`.
- Der Rahmen (`QueryHandlers.frame`) holt Monatsliste und Kategorie-Reihenfolge aus YNAB.

Ziel: Die Seite rendert aus BBs eigener Datenbank, ohne auf YNAB zu warten. Was BB schon führt (Zuweisen,
Verschieben, Targets, Pausen als Ereignisse in `events`, Aktivität aus dem Kassenbuch, Deaktivierung und Reihenfolge
der Kategorien), kommt aus BB. Was BB heute noch aus YNAB übernimmt, wird in BB gehalten (Lesemodell) und vom Spiegel
nachgezogen, nicht im Lesepfad der Seite geholt.

## Acceptance criteria
- To be defined during refinement.

## Notes
Offen für die Verfeinerung (Architekt, eventuell ADR):
- Welche Werte liefert YNAB heute noch, die BB nicht selbst führt (Monatsliste, Kategorie-Liste und Gruppen,
  „Zu verteilen“, Vortrag, verfügbar aus Monaten vor der Übernahme)? Je Wert: führt BB ihn künftig selbst oder hält
  BB eine Kopie, die der Spiegel nachzieht?
- Wann zieht der Spiegel YNAB nach (Intervall wie beim Kassenbuch-Spiegel, nach jedem Schreiben, beim Öffnen im
  Hintergrund)? Was zeigt die Seite, wenn YNAB etwas anderes sagt als BB (ADR 0065: BB hält fest, was es geschrieben
  hat)?
- Client: Bleibt der geladene Monat beim Wechsel zwischen Orten im Model, damit die Rückkehr gar nicht neu lädt?
- Messbares Kriterium für „sofort“: etwa kein YNAB-Aufruf im Lesepfad von `getBudgetMonth` und Rahmen (Handler-Test
  mit zählendem Port, null Aufrufe).
- Bezug: budgeting-bk6tq (Vortrag ab dem ersten Tag) und budgeting-wnkgj (Budget angleichen) im Backlog berühren
  dieselben Werte.
