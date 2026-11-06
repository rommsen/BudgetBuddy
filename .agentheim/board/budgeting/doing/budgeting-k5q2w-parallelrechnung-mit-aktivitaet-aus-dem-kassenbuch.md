---
id: budgeting-k5q2w
title: "Parallelrechnung: die Ausgaben der Periode kommen aus dem Kassenbuch, category_month_activity entfällt"
status: doing
type: feature
context: budgeting
created: 2026-11-06
completed:
depends_on: [budgeting-eh8xe, budgeting-pf8n7]
blocks: [budgeting-hbhzf]
tags: [targets, fehlbetrag, parallelrechnung, kassenbuch]
related_adrs: [0083, 0084]
related_research: []
prior_art: [budgeting-b9869, budgeting-pf8n7, budgeting-p2zr6]
---

## Why
Schritt 1 von 2 vor der Umschaltung auf BBs Fehlbetrag (budgeting-hbhzf ist Schritt 2). Entschieden von
Roman am 2026-11-06: Die Aktivität für die **Ausgaben der Periode** kommt aus BBs Kassenbuch statt aus
YNABs Monatsaktivität.

Bisher hält BB die Aktivität jedes gelesenen Monats als Zustand fest (`category_month_activity`,
budgeting-b9869). Das hat zwei Schwächen. Ein Monat, den BB nie gelesen hat, fehlt, und der Fehlbetrag
ist "nicht gerechnet" (im vierten Vergleich eine Kategorie, deren erste Periode vor Dezember 2024
beginnt). Ein Monat, der zuletzt vor seinem Ende gelesen wurde, gilt für immer mit einem Teil seiner
Ausgaben (Review zu b9869, Backend W1). Das Kassenbuch ist immer aktuell und reicht in Prod bis Dezember
2018 zurück (14221 Buchungen, Stand 2026-11-06).

Die Parallelrechnung bleibt in diesem Schritt stehen. Nach dem Deploy (zusammen mit budgeting-eh8xe)
vergleicht Roman ein fünftes Mal, ob BBs Fehlbetrag mit Aktivität aus dem Kassenbuch weiter zu YNAB
passt. Erst danach schaltet budgeting-hbhzf um.

## What
- `QueryHandlers.runUnderfundedComparison` bekommt die Aktivität der Periodenmonate
  (`periodMonthsNeeded`) aus dem Kassenbuch: `Ledger.QueryHandlers.getMonthlyActivityByCategory` für
  alle Kategorien mit Target in einer Abfrage, wie Targets prüfen (`liveTargetCheckPort`). Ein Adapter
  formt das Ergebnis in die Form, die `compareUnderfunded` heute nimmt (`Map<Guid, Map<string, Money>>`,
  Monat "YYYY-MM-01").
- **Lückenlos:** Ein angefragter Monat ohne Buchung der Kategorie zählt als 0. Nur ein Monat vor der
  ersten Buchung des Kassenbuchs fehlt (der Fehlbetrag ist dann wie bisher "nicht gerechnet" mit
  `PeriodActivityMissingText`). Die erste Buchung liest eine eigene kleine Abfrage (`MIN(date)` über
  nicht gelöschte Buchungen) oder sie steckt im selben Lesen; ohne Buchungen fehlt jeder Monat.
- Die Formel bleibt: netto wie heute (Erstattungen mindern, Zuflüsse senken die Ausgaben nicht unter 0).
- **Rückbau** `category_month_activity`: `recordMonthActivityWith` und der Aufruf in
  `CommandHandlers.observeUnguarded`, `loadMonthActivitiesWith`, die Row und das Schema. Die Tabelle
  wird in `initializeBudgetingSchema` mit `DROP TABLE IF EXISTS category_month_activity` entfernt
  (wiederholbar, reiner abgeleiteter Zustand ohne Geldentscheidung, ADR 0083).
- Kommentare und README, die `category_month_activity` oder "Aktivität früherer Monate aus der
  Datenbank" nennen, nachziehen.

## Acceptance criteria
- [ ] GIVEN ein Ziel "Mit Fälligkeit" mit Ausgaben in der laufenden Periode im Kassenbuch und ohne je
      gelesene frühere Monate, WHEN der Monat gelesen wird, THEN zählt die Parallelrechnung diese Ausgaben
      als gedeckt (Handler-Test über `budgetMonthWith` mit In-Memory-SQLite und Kassenbuch-Buchungen;
      vorher "nicht gerechnet").
- [ ] GIVEN eine Ausgabe, die nach dem letzten Lesen eines früheren Monats ins Kassenbuch kam, WHEN der
      laufende Monat gelesen wird, THEN zählt sie mit.
- [ ] GIVEN ein Periodenmonat ohne Buchung der Kategorie, WHEN gerechnet wird, THEN zählt er als 0.
      GIVEN ein Periodenmonat vor der ersten Buchung des Kassenbuchs, THEN "nicht gerechnet" mit
      `PeriodActivityMissingText`. Beide auf der billigsten Ebene, die den Adapter trifft.
- [ ] Split-Anteil, Erstattung und gelöschte Buchung verhalten sich wie in `getMonthlyActivityByCategory`
      (kein neuer Test dafür nötig, wenn der Adapter genau diese Funktion nutzt; die vorhandenen Tests in
      `src/Tests/TargetCheckQueryTests.fs` gelten).
- [ ] Das Lesen eines Monats ruft YNAB weiterhin genau einmal (`FetchMonth`), plus höchstens einmal
      `/categories` für die Target-Übernahme (vorhandener Test in `UnderfundedComparisonTests.fs`
      angepasst).
- [ ] Scheitert das Lesen des Kassenbuchs, steht der Grund im Server-Log, und die Antwort an den Client
      ist dieselbe wie ohne Parallelrechnung.
- [ ] `category_month_activity` wird nicht mehr geschrieben oder gelesen, `recordMonthActivityWith` und
      `loadMonthActivitiesWith` sind aus Code und Tests verschwunden, das Schema entfernt die Tabelle
      wiederholbar.
- [ ] Targets anwenden, "fehlt" und die Vorschau rechnen weiter mit YNABs `goal_under_funded` (das
      schaltet erst budgeting-hbhzf um).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] [human-eye] Fünfter Vergleich in Prod nach dem Deploy: Roman öffnet nur den laufenden Monat
      (frühere Monate müssen nicht mehr geöffnet werden). Jede Abweichung hat einen bekannten Grund.

## Notes
- Deploy nur zusammen mit budgeting-eh8xe (liegt integriert auf `main`, `b2f4b8f`). Nach dem Deploy
  bis zur Umschaltung durch hbhzf kein Target in BB ändern: "fehlt" und Targets anwenden rechnen bis
  dahin mit YNABs eingefrorenem Goal. Der Vergleich bleibt aussagekräftig, solange BBs Regeln dem
  übernommenen Stand entsprechen.
- Mit dem Rückbau entfallen die beiden offenen Punkte aus hbhzf zu `observeUnguarded` (Reihenfolge
  "Beobachtung vor Aktivität", Name `logReadFailure` für einen Schreibfehler).
- Refill-Fall F1 (YNAB brutto, BB netto) bleibt erklärt und außerhalb dieser Task.
- `getMonthlyActivityByCategory` nimmt `DateTime`-Monatserste; `periodMonthsNeeded` liefert
  "YYYY-MM-01". Die Umrechnung gehört in den Adapter, die Domäne bleibt rein.
