---
id: budgeting-hbhzf
title: "Fehlbetrag aus BB: Targets anwenden und die Anzeige rechnen mit BBs Fehlbetrag gegen das Soll, Rückbau der Parallelrechnung"
status: backlog
type: feature
context: budgeting
created: 2026-11-06
completed:
depends_on: [budgeting-eh8xe, budgeting-r4kqm, budgeting-b9869, budgeting-pf8n7, budgeting-k5q2w]
blocks: [budgeting-m3x7f]
tags: [targets, fehlbetrag, rueckbau, fuehrung]
related_adrs: [0083, 0084]
related_research: []
prior_art: [budgeting-v7kfw, budgeting-dgrp7]
---

## Why
Nach budgeting-eh8xe führt BB die Targets, die Goals in YNAB sind eingefroren. YNABs
`goal_under_funded` rechnet dann mit veralteten Goals. "Targets anwenden" und die Anzeige "fehlt"
brauchen BBs eigenen Fehlbetrag, gerechnet gegen das Soll aus der Faltung (budgeting-dgrp7).

Teil 2 von 2 der Teilung von budgeting-eh8xe (2026-11-06).

## What
- Der Fehlbetrag je Kategorie kommt aus der reinen Funktion `underfunded` (budgeting-v7kfw) über
  BBs Target-Faltung und das Soll, statt aus `goal_under_funded`.
- "Targets anwenden" nutzt BBs Fehlbetrag. Die Ablehnung bei offener Rückschreibung aus dgrp7
  entfällt, weil gegen das Soll gerechnet wird.
- Die Anzeige "fehlt" auf der Budget-Seite nutzt BBs Fehlbetrag.
- Rückbau der Parallelrechnung: `underfunded_comparisons`, `saveUnderfundedComparisons*`,
  `compareUnderfunded` und das Speichern beim Lesen eines Monats.

## Acceptance criteria
- [ ] GIVEN eine Kategorie mit in BB geändertem Target, WHEN "Targets anwenden" läuft, THEN weist
      es BBs Fehlbetrag zu, nicht YNABs `goal_under_funded` (Handler-Test mit abweichendem
      YNAB-Wert).
- [ ] GIVEN eine offene Rückschreibung im Monat, WHEN "Targets anwenden" läuft, THEN rechnet es
      gegen das Soll und lehnt nicht ab.
- [ ] Die Zeile zeigt "fehlt" aus BBs Fehlbetrag (Test der Projektion).
- [ ] `underfunded_comparisons` wird nicht mehr geschrieben; `compareUnderfunded` und
      `saveUnderfundedComparisons*` sind aus Code und Tests verschwunden.
- [ ] Die Umschaltung ist im Outcome als von Roman freigegeben vermerkt, mit der Toleranz aus den
      Notes.
- [ ] Die Fixtures in `scripts/*.test.mjs` werden im selben Schritt an geänderte Typen aus
      `src/Shared` angepasst.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- **Toleranz offen (Owner).** Bedingung für die Umschaltung nach ADR 0084: Parallelrechnung aus
  budgeting-v7kfw und Toleranz von Roman. Stand 2026-11-06: In Prod läuft die Parallelrechnung erst
  seit dem Deploy von dgrp7 (09:25), `underfunded_comparisons` ist leer. Sie füllt sich, wenn ein
  Monat auf der Budget-Seite gelesen wird. Die Daten müssen vor dem Deploy von budgeting-eh8xe
  entstehen, danach weichen YNABs Goals absichtlich ab und der Vergleich sagt nichts mehr.
- **Toleranz (Roman, 2026-11-06):** Umschalten, wenn jede Abweichung im laufenden Monat einen
  bekannten Grund hat (etwa ein YNAB-Sonderfall). Die Liste wird einmal mit Roman durchgegangen,
  das Ergebnis kommt hierher. Roman öffnet dafür in Prod den laufenden und zwei, drei frühere Monate.
- **Vergleich 2026-11-06 (Mai bis Dezember, 100 Kategorien mit Target):** "Nicht gerechnet" nur bei
  monatlichen Targets in vergangenen Monaten, YNAB liefert dort keinen Wert (erklärt, YNAB-Sonderfall).
  Alle Abweichungen betreffen "Mit Fälligkeit" und gehen auf einen BB-Fehler zurück: budgeting-r4kqm.
  Nach dessen Deploy wird erneut verglichen.
- **Vor dem Umschalten zu klären (Review zu budgeting-b9869, Backend W1):** Die Aktivität eines Monats in
  `category_month_activity` gilt für immer, sobald sie einmal gelesen wurde. Wurde ein Monat zuletzt vor
  seinem Ende gelesen, zählt BB nur einen Teil seiner Ausgaben und rechnet einen falschen Betrag statt
  "nicht gerechnet". Bob bucht bis 31 Tage zurück, Import und Spiegel bringen ältere Buchungen nach.
  Optionen: `read_at` prüfen (Monat gilt als fehlend, solange er nicht nach Monatsende plus 31 Tagen
  gelesen wurde) oder die Aktivität aus dem Kassenbuch nehmen
  (`Ledger.QueryHandlers.getMonthlyActivityByCategory`, immer aktuell, ohne YNAB). **Entschieden von Roman
  am 2026-11-06: Kassenbuch.** Umgesetzt in zwei Schritten: budgeting-k5q2w stellt die Parallelrechnung um
  und baut `category_month_activity` zurück, danach fünfter Vergleich in Prod, erst dann schaltet diese Task um.
- **Vierter Vergleich 2026-11-06 (nach budgeting-pf8n7, November 2026, Monate ab Dezember 2024 gelesen):** 99
  von 100 gleich, keine Abweichung, eine Kategorie "nicht gerechnet" (erste Periode vor Dezember 2024). Von
  Roman als erfüllte Toleranz angenommen.
- ~~Nach der Integration von budgeting-eh8xe (Review zu b9869): Reihenfolge in `observeUnguarded`, Name
  `logReadFailure`.~~ Entfällt mit dem Rückbau von `category_month_activity` in budgeting-k5q2w.
- Deploy nur zusammen mit budgeting-eh8xe.
