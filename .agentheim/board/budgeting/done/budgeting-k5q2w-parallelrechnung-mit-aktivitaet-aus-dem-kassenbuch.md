---
id: budgeting-k5q2w
title: "Parallelrechnung: die Ausgaben der Periode kommen aus dem Kassenbuch, category_month_activity entfällt"
status: done
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

## Outcome

Die Ausgaben der Periode für die Parallelrechnung kommen jetzt aus dem Kassenbuch. YNABs festgehaltene Monatsaktivität wird dafür nicht mehr verwendet.

- `src/Server/Budgeting/QueryHandlers.fs`: neuer Port `LedgerActivityPort` (`LoadMonthlyActivity`, `LoadFirstEntryDate`) und `liveLedgerActivityPort` (`Ledger.QueryHandlers.getMonthlyActivityByCategory`, `Ledger.Persistence.loadFirstEntryDate`). Der reine Adapter `periodActivityFromLedger` macht daraus die Form `Map<Guid, Map<string, Money>>`, die `compareUnderfunded` erwartet. Ein Monat ohne Buchung zählt dabei als 0. Ein Monat vor dem Monat der ersten Buchung fehlt, und ohne jede Buchung fehlt jeder Monat. Ohne Periodenmonat wird nichts gelesen. `runUnderfundedComparison` fängt Fehler beim Lesen des Kassenbuchs ab und schreibt den Grund ins Server-Log ("Parallelrechnung Fehlbetrag"). Es wird dann nichts gespeichert, und die Antwort bleibt gleich. `budgetMonthWithLedger` nimmt den Port als Parameter, `budgetMonthWith` ist dieselbe Funktion mit dem Live-Port.
- `src/Server/Budgeting/Domain.fs`: `periodCategoriesNeeded` liefert die Kategorien, deren Target Periodenmonate braucht. Die Formel ist unverändert.
- `src/Server/Ledger/Persistence.fs`: `loadFirstEntryDate`, `MIN(substr(date, 1, 10))` über nicht gelöschte Buchungen.
- Rückbau: `recordMonthActivityWith`, `loadMonthActivitiesWith`, `CategoryMonthActivityRow`, `MonthActivityUnreadableText`, das Schema der Tabelle und der Aufruf in `CommandHandlers.observeUnguarded` sind entfernt. `initializeBudgetingSchema` löscht `category_month_activity` nur, wenn die Tabelle in `sqlite_master` noch existiert. Ein `DROP TABLE IF EXISTS` bei jedem Aufruf hat die geteilte In-Memory-DB im parallelen Testlauf blockiert: der Lauf hing ohne Fortschritt, weil das DROP auch ohne Tabelle das Schema sperrt.
- Live-Anschluss des Ports (Doktrin Punkt 3): `liveLedgerActivityPort` wird nur in `budgetMonthWith` angeschlossen, `budgetMonth` ruft `budgetMonthWith` auf, der Endpoint ruft `budgetMonth` auf (`src/Server/Api.fs` Zeile 1739, unverändert). Den Nachweis liefern die beiden Handler-Tests über `budgetMonthWith` mit echten Kassenbuch-Buchungen. Gegenprobe: mit `LoadMonthlyActivity = fun _ _ -> async { return Map.empty }` im Live-Port werden genau diese beiden Tests rot (Mutantenliste: nur dieser eine Mutant).
- Tests in `src/Tests/UnderfundedComparisonTests.fs`: Adapter auf reiner Ebene (Monat ohne Buchung zählt 0; Monat vor der ersten Buchung fehlt, der Monat der ersten Buchung zählt; ohne Buchung fehlt jeder Monat; ein fehlender Monat führt über `compareUnderfunded` zu "nicht gerechnet" mit `PeriodActivityMissingText`, mit Gegenprobe). Handler-Tests mit In-Memory-SQLite und Kassenbuch-Buchungen: Ausgaben ohne je gelesene frühere Monate zählen als gedeckt, mit genau `FetchMonth` plus einmal `/categories`; eine Ausgabe, die nach dem Lesen der früheren Monate ins Kassenbuch kam, zählt mit; scheitert der Kassenbuch-Port, ist die Antwort dieselbe und nichts wird gespeichert; `loadFirstEntryDate` ohne gelöschte Buchung. Dazu ein Schema-Test: die Tabelle wird entfernt, auch bei wiederholtem Aufruf. Beide Handler-Tests sind Regressionstests: vorher war das Ergebnis "nicht gerechnet" bzw. ein Betrag von 2 statt 0. Entfernt wurden die Tests für Festhalten und Laden von `category_month_activity`.
- Split-Anteil, Erstattung und gelöschte Buchung: der Adapter nutzt `getMonthlyActivityByCategory` direkt, deshalb gelten die vorhandenen Tests in `src/Tests/TargetCheckQueryTests.fs`.
- Targets anwenden, "fehlt" und die Vorschau sind nicht berührt und rechnen weiter mit YNABs `goal_under_funded`.
- `./verify.sh`: VERIFY: PASS (dotnet 4140 grün, node 311 grün).
- Offen [human-eye]: der fünfte Vergleich in Prod nach dem Deploy zusammen mit budgeting-eh8xe.
- Kann-Funde: ADR 0084, Nachtrag "budgeting-r4kqm und budgeting-b9869" nennt noch `category_month_activity` als Quelle. Dafür braucht es einen Nachtrag zu budgeting-k5q2w (Quelle Kassenbuch, nur Monate vor der ersten Buchung fehlen); das ist Sache des Conductors oder von Modeling. Der Grund für einen Lesefehler steht im Log nur als allgemeines "Parallelrechnung Fehlbetrag", ohne eigenen Text für das Kassenbuch.

### Integration (2026-11-06)
- Verifier Iteration 1: FAIL nur wegen ADR 0084 (der Nachtrag zu r4kqm und b9869 nannte noch `category_month_activity`). Code und Tests PASS, `./verify.sh` im zweiten Lauf `VERIFY: PASS` (dotnet 4140, node 311; der erste Lauf scheiterte am bekannten Timer-Test aus infrastructure-mshaf). Behoben bei der Integration mit dem Nachtrag "budgeting-k5q2w, Quelle der Ausgaben" zu ADR 0084, ohne erneute Iteration.
- Kann-Fund (eigener Log-Text für Lesefehler im Kassenbuch) steht in budgeting-h9c3w.
