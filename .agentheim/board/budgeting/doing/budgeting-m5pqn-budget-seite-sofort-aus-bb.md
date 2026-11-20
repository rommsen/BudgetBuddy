---
id: budgeting-m5pqn
title: "Budget-Monat aus BB: YNAB-Stand je Monat festhalten, getBudgetMonth liest nur die Datenbank, Verfügbar und Zu verteilen leitet BB aus Soll, Kassenbuch und Stand ab"
status: doing
type: feature
context: budgeting
created: 2026-11-20
completed:
depends_on: []
blocks: [budgeting-ffjk9, budgeting-vksa6]
tags: [budget-seite, lesemodell, performance, ynab]
related_adrs: [0065, 0074, 0076, 0083, 0084, 0088, 0101]
related_research: []
prior_art: [budgeting-zkbbd]
---

## Why
Roman (2026-11-20): „Budget-Seite ist nervig, dass die Budgets immer erst geladen werden müssen. Die Seite muss
instant da sein.“ Auf die Rückfrage, ob ein zuletzt bekannter Stand mit Abgleich im Hintergrund reicht: „Wir hatten
doch entschieden, dass Budgets führend sind. Also immer zuerst BB.“ (budgeting-zkbbd).

Heute fragt jeder Aufruf YNAB live: `getBudgetMonth` → `budgetMonthWithLedger` → `observeMonthWith` (`GET /months/{m}`)
und `targetRulesBestEffort` (`GET /categories`). Der Rahmen kostet zwei weitere Aufrufe (budgeting-yx51n).

Teil 1 von 4 nach ADR 0101: dieser Monat, budgeting-yx51n Rahmen, budgeting-ffjk9 Nachziehen beim Öffnen,
budgeting-vksa6 Monat beim Ortswechsel halten.

## What
Quellen je Wert (ADR 0101):

| Wert | Künftig |
|---|---|
| Zugewiesen | BB-Soll (Faltung `budgeting:month:<YYYY-MM>`), wie heute |
| Aktivität | BB, aus dem Kassenbuch (`getMonthlyActivityByCategory`); vor dem ersten Kassenbuch-Monat aus dem Stand |
| Vortrag (intern) | Stand, beim Nachziehen aus einem YNAB-Monat berechnet |
| Verfügbar | abgeleitet: Vortrag + Soll + Aktivität |
| Zu verteilen | Stand minus Summe (Soll minus YNAB-Zugewiesen) |
| Name, Gruppe, ausgeblendet, gelöscht, `internal` | Stand (ADR 0088: YNAB bleibt Quelle der Liste) |
| Goal-Vergleich (`TargetInYnab`) | Stand |
| Target, Pause, offene Rückschreibung, Deaktiviert | BB, wie heute |

- Zustand `YnabMonthSnapshot` (ADR 0083: Zustand, kein Ereignis), Schema über `initializeDatabase`, wiederholbar,
  Beträge als Milliunits `int64`. In der UI heißt er „YNAB-Stand“.
- Einziger Schreiber ist `observeUnguarded` (`src/Server/Budgeting/CommandHandlers.fs`, aufgerufen von
  `observeMonthWith` und `ensureTakenOverWith`), in derselben Transaktion wie Übernahme und Beobachtungen. Damit füllen
  ihn auch alle Befehle und der `TakeoverWorker`.
- `budgetMonthWithLedger` liest Faltung, Stand, Kassenbuch, Pausen, Regeln, offene Target-Schreibungen und Layout und
  ruft weder `observeMonthWith` noch `targetRulesBestEffort` mit YNAB-Port; `withTargetInYnab` liest das Goal aus dem
  Stand.
- Reine Funktionen in `Server.Budgeting.Domain` für Vortrag, Verfügbar und Zu verteilen.
- Ein Monat ohne Stand wird einmal beobachtet wie heute (F3), danach nie mehr im Lesepfad.
- Neues Feld `CopiedAt: DateTime option` in `BudgetMonth`.
- Die Echo-Regel aus ADR 0084 Punkt 3 bleibt unverändert, sie läuft künftig beim Nachziehen (budgeting-ffjk9).

## Acceptance criteria
- [ ] Handler-Test mit zählendem `YnabPort` (alle Felder zählen): `budgetMonthWithLedger` für einen Monat mit Stand
      macht null Aufrufe; mit einem Port, der bei jedem Aufruf wirft, kommt der Monat trotzdem.
- [ ] Handler-Test: ein Monat ohne Stand wird genau einmal beobachtet (ein `FetchMonth`), das zweite Lesen macht null
      Aufrufe.
- [ ] Handler-Test: nach `observeMonthWith` steht der Stand in der DB, nach einem Befehl (Zuweisen) ebenso; scheitert
      das Festhalten, wird weder Stand noch Beobachtung geschrieben (eine Transaktion).
- [ ] Domain-Test: ohne offene Rückschreibung und mit Kassenbuch gleich YNAB sind Verfügbar und Zu verteilen gleich
      den Werten des Stands, für jede Kategorie.
- [ ] Domain-Test: Soll 50 bei Stand-Zugewiesen 30 ergibt Verfügbar +20 und Zu verteilen −20; eine Verschiebung
      zwischen zwei Kategorien lässt Zu verteilen unverändert.
- [ ] Domain-Test: der Vortrag ist bei jedem Soll gleich; eine Kassenbuch-Buchung von −12 ändert Verfügbar um −12.
- [ ] Domain-Test: für einen Monat vor der ersten Kassenbuch-Buchung kommt die Aktivität aus dem Stand.
- [ ] Handler-Test: der Fehlbetrag rechnet mit dem Vortrag aus dem Stand, die bisherigen Fehlbetrag-Fälle bleiben grün.
- [ ] Live-Port-Stellen nach Doktrin Punkt 3 aufgelistet (`Api.budgetMonthFor`, `budgetApiWith`, Übersicht) und je
      per Verhalten oder Strukturwächter mit Gegenbeweis belegt.
- [ ] Pipeline- und Negativtests für `getBudgetMonth` grün; Fixtures in `scripts/*.test.mjs` tragen `CopiedAt` im
      selben Schritt.
- [ ] README budgeting nachgezogen (ADR 0101 ist angenommen, die Nachträge an 0065, 0076 und 0084 stehen schon), Delta mit `npm run check:readme-delta` geprüft.
- [ ] `./verify.sh` endet mit `VERIFY: PASS`.

## Notes
- Entschieden (Roman, 2026-11-20, alle Empfehlungen, ADR 0101 angenommen): F1 Vortrag und Zu verteilen vorerst
  als YNAB-Stand, F2 Aktivität aus dem Kassenbuch (eine in BB erfasste Buchung senkt Verfügbar sofort), F3 Monat
  ohne Stand einmal beobachten, F5 Verfügbar sofort abgeleitet, der dgrp7-Satz fällt.
- Konflikt der Spezialisten: der Architekt wollte die Aktivität aus dem Stand (sonst gemischte Quellen im Vortrag);
  gefolgt wurde dem tactical-modeler, weil der Vortrag beim Nachziehen aus einem einzigen YNAB-Monat gerechnet wird.
- Die PATCH-Antwort von YNAB wird nicht gebraucht (`YnabClient.setAssignedWith` dekodiert zu `unit`).
- Risiko: die Target-Übernahme beim Lesen entfällt; sie läuft künftig beim Nachziehen (budgeting-ffjk9) und vor
  Befehlen.
- Später, nicht ohne Roman: BB rechnet Vortrag und Zu verteilen selbst (Anker bei der Übernahme, Überziehungsregeln,
  Parallelrechnung). Dafür klären, ob Roman in YNAB Kreditkarten-Konten nutzt.
- budgeting-wnkgj (Budget angleichen) soll die abgeleiteten Gruppensummen nutzen und nach dieser Task gebaut werden.
