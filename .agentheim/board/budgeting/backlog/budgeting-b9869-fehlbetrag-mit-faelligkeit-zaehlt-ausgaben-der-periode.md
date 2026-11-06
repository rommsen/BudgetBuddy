---
id: budgeting-b9869
title: "Fehlbetrag mit Fälligkeit: Ausgaben seit Beginn der laufenden Periode zählen als gedeckt, wie bei YNAB"
status: backlog
type: bug
context: budgeting
created: 2026-11-06
completed:
depends_on: [budgeting-r4kqm]
blocks: [budgeting-hbhzf]
tags: [targets, fehlbetrag, parallelrechnung]
related_adrs: [0084]
related_research: []
prior_art: [budgeting-v7kfw, budgeting-r4kqm]
---

## Why
Zweiter Prod-Vergleich nach budgeting-r4kqm (Monate 2025-12 bis 2026-11, gelesen 2026-11-06): im
laufenden Monat 92 gleich, 8 Abweichungen, alle "Mit Fälligkeit", alle NEED mit jährlichem Rhythmus,
BB verlangt Geld, YNAB 0. Ein lesender Abruf von `GET /categories` (nur `goal_*`-Felder, ohne Namen)
zeigt die Ursache: YNAB zählt Geld, das in der laufenden Zielperiode schon ausgegeben wurde, als
gedeckt (`goal_overall_funded`). BB zählt nur den Übertrag. Beispiele (anonym):

| Fall | Ziel | goal_overall_funded | Kontostand | BB | YNAB |
|---|---|---|---|---|---|
| A2 | 66 | 46,31 | 34,51 | 2,36 | 0 |
| A5 | 235 | 253,10 (100 %) | 0 | 78,34 | 0 |
| A7 | 330 | 330 (100 %) | 2,36 | 327,64 | 0 |

Kategorien ohne Ausgabe in der Periode stehen auf "Gleich" (dort Kontostand = `goal_overall_funded`).
Ohne Fix würde "Targets anwenden" nach budgeting-hbhzf Geld doppelt zuweisen.

## What (zu verfeinern)
- `underfunded` im Zweig "Mit Fälligkeit" rechnet mit "gedeckt in der Periode" = Übertrag plus
  Ausgaben seit Beginn der laufenden Periode (die vorige Fälligkeit, also das um einen
  Rhythmus-Schritt zurückverschobene Datum aus `dueFrom`), statt nur mit dem Übertrag.
- Offen für die Verfeinerung:
  - Datenquelle der Ausgaben je Kategorie und Monat: Aktivität aus YNABs Monaten (heute live
    gelesen) oder BBs Kassenbuch. Nach ADR 0084 bleibt Aktivität vorerst YNABs Wert.
  - Wie YNAB "Refill up to" (`goal_needs_whole_amount = false`) gegenüber "Set aside another"
    behandelt; Fall A1 (Refill, `goal_overall_funded` weit über dem Ziel) prüfen.
  - Ob Zuflüsse (positive Aktivität) mitzählen.

## Acceptance criteria (Entwurf)
- [ ] GIVEN ein jährliches NEED-Target mit Ausgaben in früheren Monaten der laufenden Periode, WHEN
      der Fehlbetrag gerechnet wird, THEN zählen diese Ausgaben als gedeckt; die Fälle A2, A5, A7
      (mit erfundenen Beträgen nachgebaut) ergeben 0.
- [ ] Ausgaben vor Beginn der laufenden Periode zählen nicht.
- [ ] Nach dem Deploy (allein, vor budgeting-eh8xe) zeigt der Prod-Vergleich im laufenden Monat keine
      unerklärte Abweichung mehr.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- **Reihenfolge-Problem:** Der Vergleich in Prod ist nur aussagekräftig, solange budgeting-eh8xe
  nicht deployt ist. eh8xe wird gerade gebaut und darf erst mit budgeting-hbhzf in Prod. Dieser Fix
  muss deshalb vor eh8xe nach Prod, oder aus einem Zweig ohne eh8xe deployt werden.
- Erfasst vom Conductor nach Romans Freigabe des lesenden YNAB-Abrufs, 2026-11-06.
