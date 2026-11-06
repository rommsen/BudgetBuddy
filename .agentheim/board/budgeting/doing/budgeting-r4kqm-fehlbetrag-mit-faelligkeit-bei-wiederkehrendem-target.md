---
id: budgeting-r4kqm
title: "Fehlbetrag mit Fälligkeit: BB rechnet bei wiederkehrenden Targets bis zur nächsten Fälligkeit statt mit dem gespeicherten, vergangenen Datum"
status: doing
type: bug
context: budgeting
created: 2026-11-06
completed:
depends_on: [budgeting-v7kfw]
blocks: [budgeting-hbhzf]
tags: [targets, fehlbetrag, parallelrechnung]
related_adrs: [0084]
related_research: []
prior_art: [budgeting-v7kfw]
---

## Why
Die Parallelrechnung aus budgeting-v7kfw (Prod, Monate 2026-05 bis 2026-12, gelesen am
2026-11-06) zeigt: Jede Abweichung zwischen BBs Fehlbetrag und YNABs `goal_under_funded` betrifft die
Regel "Mit Fälligkeit" (NEED mit Datum), im laufenden Monat 25 von 100 Kategorien. Alle anderen
Regeln sind gleich. Nach Romans Toleranz (budgeting-hbhzf) darf BB erst umschalten, wenn jede
Abweichung erklärt ist. Ohne Fix würde "Targets anwenden" bei diesen Kategorien zu viel zuweisen.

Befund (anonymisiert, eine Kategorie): BB 67,41 / 59,92 / … / 22,47 von Mai bis November, je Monat
7,49 weniger; YNAB 0 (gedeckt). Im Dezember BB wieder 22,47, YNAB 7,49. BB verlangt also den ganzen
Rest bis zum Ziel auf einmal, und `remainingMonths` liefert offenbar 1, weil die Fälligkeit aus BBs
Sicht erreicht oder vorbei ist. Einzelne Kategorien zeigen das Spiegelbild (BB 0, YNAB > 0).

Vermutete Ursache, noch nicht am Datum belegt (die Prod-Kopie wurde nach der Auswertung gelöscht):
Bei wiederkehrenden Zielen (`goal_cadence`, in BB `TargetRhythm` aus `targetRhythm` in
`src/Server/YnabClient.fs`) liegt `goal_target_date` in der Vergangenheit, und YNAB rechnet über den
Rhythmus bis zur nächsten Fälligkeit weiter. `underfunded` in `src/Server/Budgeting/Domain.fs`
(`DueDateRule due`) nutzt nur das Datum und ignoriert den Rhythmus.

## What
- Die Fälligkeit für die Formel "Mit Fälligkeit" ist bei einem Target mit Rhythmus die erste
  Fälligkeit ab dem gelesenen Monat: das gespeicherte Datum, so oft um den Rhythmus (Monate, Jahre)
  verschoben, bis es im gelesenen Monat oder danach liegt. Ohne Rhythmus bleibt das gespeicherte Datum.
- Liegt die verschobene Fälligkeit nach dem gelesenen Monat, rechnet BB wie YNAB nur mit dem Betrag
  der laufenden Periode (Startstand der Periode statt des ganzen Übertrags seit Beginn, falls die
  Daten das zeigen). Wie YNAB die Periode genau abgrenzt (`goal_needs_whole_amount`, "Set aside" oder
  "Refill"), klärt der Worker an YNABs Doku und an den Zahlen oben; die Entscheidung kommt ins
  Outcome.
- Nur die reine Funktion und ihre Tests. Kein neues Ereignis, kein Schema, keine UI.

## Acceptance criteria
- [ ] GIVEN ein NEED-Target mit Datum im Vormonat und Rhythmus "alle 12 Monate", WHEN der Fehlbetrag
      im laufenden Monat gerechnet wird, THEN rechnet BB bis zur Fälligkeit in 11 Monaten (Test der
      reinen Funktion), nicht mit 1 verbleibenden Monat.
- [ ] Ein Fall wie im Befund: Ziel, das YNAB als gedeckt meldet (monatlicher Anteil zugewiesen),
      ergibt in BB 0; derselbe Fall im Folgemonat ohne Zuweisung ergibt den monatlichen Anteil.
- [ ] Ein NEED-Target mit Datum in der Zukunft und ohne Rhythmus rechnet unverändert (bestehende Tests
      bleiben grün).
- [ ] Die Fälligkeit im gelesenen Monat selbst und ein Rhythmus in Wochen sind je mit einem Test
      abgedeckt (Grenzen nach Doktrin Punkt 4).
- [ ] Regressionstest-Kommentar nennt den Befund aus der Parallelrechnung (Doktrin Punkt 8), der Test
      ist vor dem Fix rot.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- **Allein deploybar und vor budgeting-eh8xe zu deployen.** Die Funktion speist heute nur die
  Parallelrechnung (`underfunded_comparisons`), keine Entscheidung. Nach dem Deploy öffnet Roman die
  Monate erneut, der Conductor vergleicht wieder. Erst wenn jede Abweichung erklärt ist, darf
  budgeting-hbhzf umschalten. Nach dem Deploy von budgeting-eh8xe ist der Vergleich wertlos, weil
  die YNAB-Goals dann absichtlich abweichen.
- budgeting-eh8xe ändert parallel dieselbe Funktion (Wochentag). Wird r4kqm zuerst integriert, löst
  der Merge von eh8xe den Rest.
- Entscheidung Roman 2026-11-06: Bug-Task sofort, vor eh8xe.
