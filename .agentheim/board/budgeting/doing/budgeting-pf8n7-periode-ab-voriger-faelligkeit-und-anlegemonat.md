---
id: budgeting-pf8n7
title: "Fehlbetrag mit Fälligkeit: die laufende Periode beginnt im Monat der vorigen Fälligkeit, bei der ersten Fälligkeit im Anlegemonat des Ziels"
status: doing
type: bug
context: budgeting
created: 2026-11-06
completed:
depends_on: [budgeting-b9869]
blocks: [budgeting-hbhzf]
tags: [targets, fehlbetrag, parallelrechnung]
related_adrs: [0084]
related_research: []
prior_art: [budgeting-r4kqm, budgeting-b9869]
---

## Why
Dritter Prod-Vergleich nach budgeting-b9869 (2026-11-06): im laufenden Monat 97 von 100 gleich. Eine
Prüfung aller 32 NEED-Targets mit Fälligkeit gegen YNABs `goal_under_funded` (lesender Abruf von
`/categories`, gespeicherte Aktivität, nur Zahlen ausgewertet) ergab:

| Regel für den Periodenbeginn | gleich |
|---|---|
| Monat nach der vorigen Fälligkeit (heute, b9869) | 26 von 32 |
| Monat der vorigen Fälligkeit, einschließlich | 30 von 32 |

Die zweite Regel macht kein heute gleiches Target ungleich. Die beiden Reste:
- F3: Ziel angelegt im Mai 2026, erste Fälligkeit August 2027 (jährlich). Die erste Periode beginnt im
  **Anlegemonat** (hier 15 Monate). Mit den Ausgaben aus dem Mai (−97,99) ergibt BB 0 wie YNAB.
- F1 ("Refill up to"): Rest 0,25 € statt 0, weil YNAB Ausgaben brutto zählt (`goal_overall_funded`
  1.619 € bei Netto-Aktivität −305 €). Erklärt, bleibt hier offen (Datenquelle: budgeting-hbhzf).

## What
- `periodMonthsBefore` (src/Server/Budgeting/Domain.fs): Die laufende Periode beginnt im Monat der
  vorigen Fälligkeit (einschließlich), nicht im Monat danach. Sie endet wie bisher vor dem gelesenen
  Monat.
- **Erste Periode:** Wurde das gespeicherte Datum für den gelesenen Monat nicht verschoben (es ist die
  erste Fälligkeit des Ziels, `dueFrom` ändert nichts) und ist das Anlegedatum des Ziels bekannt, beginnt
  die Periode im **Anlegemonat** (einschließlich).
- Dafür liest BB `goal_creation_month` mit (YnabClient-Decoder für Monat und Kategorie) und führt es am
  Target als optionales Feld (z. B. `CreatedIn: string option`, "YYYY-MM-01"). Fehlt es, gilt die Regel
  ohne Anlegemonat.
- Nur die Parallelrechnung nutzt die Rechnung. Nichts, was Geld entscheidet, ändert sich.

## Acceptance criteria
- [ ] GIVEN ein jährliches Ziel mit Ausgabe im Monat der vorigen Fälligkeit, WHEN gerechnet wird, THEN
      zählt diese Ausgabe als gedeckt (Fall F2 mit erfundenen Beträgen: 0).
- [ ] GIVEN ein Ziel, angelegt vor der rechnerisch vorigen Fälligkeit, dessen gespeichertes Datum die erste
      Fälligkeit ist, WHEN gerechnet wird, THEN beginnt die Periode im Anlegemonat (Fall F3: 0).
- [ ] Ohne Anlegemonat und bei verschobenem Datum gilt "ab vorige Fälligkeit"; ohne Rhythmus zählen keine
      Ausgaben (je ein Test, Grenzen nach Doktrin Punkt 4).
- [ ] Die bestehenden Tests zu b9869 werden an die neue Grenze angepasst, nicht gelöscht; ein Test zeigt,
      dass eine Ausgabe im Monat **vor** der vorigen Fälligkeit nicht zählt.
- [ ] Decoder: `goal_creation_month` wird gelesen, ein fehlendes Feld ergibt `None` (Test der reinen
      Decoder-Funktion).
- [ ] Fixtures in `scripts/*.test.mjs` werden mitgezogen, falls ein Record aus `src/Shared` ein Feld
      bekommt.
- [ ] Nach dem Deploy (allein, vor budgeting-eh8xe) zeigt der Prod-Vergleich im laufenden Monat höchstens
      den erklärten Refill-Rest (F1). [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- **Konflikt mit budgeting-eh8xe (geparkt):** eh8xe führt Targets als BB-Regel mit eigenem Codec
  (`TargetTakenOverFromYnab`, `TargetSet`). Das neue Feld muss beim Integrieren von eh8xe in die Regel und
  den Codec (Übernahme aus `goal_creation_month`, `TargetSet` behält es), sonst rechnet BB nach eh8xe
  wieder ohne Anlegemonat. Der Conductor nimmt das in die Integration von eh8xe auf.
- Entscheidung Roman 2026-11-06: "Regel prüfen, dann fixen".
