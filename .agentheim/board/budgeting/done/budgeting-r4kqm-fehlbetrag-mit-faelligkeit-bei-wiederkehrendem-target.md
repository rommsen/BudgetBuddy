---
id: budgeting-r4kqm
title: "Fehlbetrag mit Fälligkeit: BB rechnet bei wiederkehrenden Targets bis zur nächsten Fälligkeit statt mit dem gespeicherten, vergangenen Datum"
status: done
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

## Outcome

BB rechnet den Fehlbetrag "Mit Fälligkeit" bei wiederkehrenden Targets jetzt bis zur nächsten Fälligkeit.

- Neue reine Funktion `dueFrom rhythm month due` in `src/Server/Budgeting/Domain.fs`: Bei `Weeks n`, `Months n` oder `Years n` wird das gespeicherte Datum um k Rhythmus-Schritte verschoben (immer vom gespeicherten Datum aus, damit ein Monatsende nicht wandert), bis es im gelesenen Monat oder danach liegt. Ohne Rhythmus oder mit unlesbarem Datum bleibt das gespeicherte Datum. `underfunded` ruft im Zweig `DueDateRule` jetzt `remainingMonths month (dueFrom target.Rhythm month due)`. `underfundedRuleOf`, `remainingMonths` und der Wochen-Zweig (`WeeklyRule`) sind unverändert, damit der Merge mit budgeting-eh8xe klein bleibt.
- Tests in `src/Tests/UnderfundedComparisonTests.fs`, Liste "Mit Fälligkeit", 8 neu: Regressionstest mit Kommentar zum Befund (Datum im Vormonat, `Years 1` und `Months 12`, je 12 Monate bis zur Fälligkeit), Befund-Fall (gedeckt ergibt 0, Folgemonat ohne Zuweisung ergibt den Monatsanteil), Fälligkeit mit Rhythmus im gelesenen Monat, Wochen-Rhythmus im gelesenen Monat und darüber hinaus (`Weeks 6`, zwei Monate), Datum in der Zukunft mit Rhythmus unverändert, vergangenes Datum ohne Rhythmus (TBD) bleibt 1 Monat. Vor dem Fix waren 4 davon rot (beide Regressionstests, Befund-Fall, Wochen über den Monat hinaus). Alle Beträge erfunden.
- `./verify.sh`: VERIFY: PASS (dotnet 4123 grün, node 311 grün).

Wie die Periodenregel bestimmt wurde: aus den Zahlen des Befunds, ohne Web-Recherche in der YNAB-Doku. Die Reihe 67,41 / 59,92 / … / 22,47 (Mai bis November, je 7,49 weniger, Dezember 22,47 bei YNAB 7,49) passt genau zu einem Jahresziel von 12 × 7,49 = 89,88 mit Fälligkeit im Februar und jährlichem Rhythmus: mit der verschobenen Fälligkeit Februar 2027 ergibt die bestehende Formel ((Betrag − Übertrag) / verbleibende Monate, beide Monate eingeschlossen, aufgerundet, − Zugewiesen) in jedem Monat Mai bis November 0 und im Dezember (3 Monate, Rest 22,47) 7,49, also YNABs Werte. Die Monatszählung mit Einschluss beider Monate ist dadurch bestätigt. "Fälligkeit in 11 Monaten" aus dem Akzeptanzkriterium ist als Datum 11 Monate nach dem gelesenen Monat umgesetzt, das sind 12 gezählte Monate.

Bewusst nicht geändert: Der Startstand der Periode. Die Formel nutzt weiter den ganzen Übertrag. Die Befund-Zahlen liegen alle in einer Periode (März 2026 bis Februar 2027) und zeigen keinen Periodenwechsel, deshalb gibt es keinen Beleg für eine andere Abgrenzung.

Hypothesen, die der nächste Prod-Vergleich bestätigen muss:
- Nach einem Periodenwechsel ohne Ausgabe (Übertrag ≥ Betrag) rechnet BB 0. YNAB könnte bei "Set aside another" (`goal_needs_whole_amount` true) für die neue Periode erneut den vollen Anteil verlangen. Abweichungen im Monat nach einer Fälligkeit wären darauf zu prüfen.
- Die im Task genannten Spiegelbild-Fälle (BB 0, YNAB > 0) sind mit diesem Fix nicht sicher erklärt; sie könnten genau der Fall oben sein.
- Ob YNAB bei Wochen-Rhythmus mit Datum ebenso verschiebt, ist nicht belegt (in den Befund-Daten nicht vorhanden); umgesetzt nach der Regel aus dem Task.

Kann-Funde: keine.
