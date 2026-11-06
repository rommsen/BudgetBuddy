---
id: budgeting-hbhzf
title: "Fehlbetrag aus BB: Targets anwenden und die Anzeige rechnen mit BBs Fehlbetrag gegen das Soll, Rückbau der Parallelrechnung"
status: backlog
type: feature
context: budgeting
created: 2026-11-06
completed:
depends_on: [budgeting-eh8xe]
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
- Deploy nur zusammen mit budgeting-eh8xe.
