---
id: budgeting-b9869
title: "Fehlbetrag mit Fälligkeit: Ausgaben seit Beginn der laufenden Periode zählen als gedeckt, wie bei YNAB"
status: doing
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

## What
- `underfunded` im Zweig "Mit Fälligkeit" rechnet mit **gedeckt in der Periode** = Übertrag plus
  Ausgaben seit Beginn der laufenden Periode: `ceil((Ziel − (Übertrag + Ausgaben)) / Monate) − Zugewiesen`,
  unten bei 0. Belegt an den YNAB-Werten (Ausgaben = `goal_overall_funded − balance`): A2 Übertrag
  29,58 + Ausgaben 11,80 → (66 − 41,38) / 5 = 4,93 − 4,93 = 0; A7 0 + 327,64 → (330 − 327,64) / 1 −
  2,36 = 0. Gilt für "Set aside another" und "Refill up to" gleich (A2 ist Refill, A3 bis A8 Set aside).
- **Laufende Periode:** vom Monat nach der vorigen Fälligkeit (`dueFrom` um einen Rhythmus-Schritt
  zurück) bis einschließlich des gelesenen Monats. Ohne Rhythmus beginnt sie nie (Ausgaben zählen
  dann nicht, Verhalten wie heute).
- **Ausgaben** = −Summe der Aktivität der Kategorie in den Monaten der Periode, nur wenn negativ
  (Zuflüsse senken den Betrag nicht unter 0). Der Wert ist eine Annahme aus den Zahlen; YNABs Doku
  sagt nichts Genaues. Der Worker nennt im Outcome, was belegt und was angenommen ist.
- **Datenquelle (Entscheidung Roman 2026-11-06): YNABs Monatsaktivität.** Grenze: YNAB erlaubt
  200 Aufrufe je Stunde. Das Lesen eines Monats darf nicht bis zu 11 frühere Monate live nachladen.
  Vorschlag: die Aktivität je Monat und Kategorie als **Zustand** festhalten (ADR 0083, kein
  Ereignis, Tabelle mit `CREATE TABLE IF NOT EXISTS`), überschrieben bei jedem Lesen eines Monats
  (Budget-Seite und `TakeoverWorker`, die ohnehin `/months/{m}` lesen). Fehlt ein Monat der Periode im
  Zustand, rechnet BB "Nicht gerechnet" mit Grund "Aktivität der Periode fehlt" statt eines falschen
  Betrags. Der Worker darf einen gleichwertigen Weg wählen, wenn er die Grenze einhält, und begründet
  ihn im Outcome.
- Die reine Funktion bekommt die Ausgaben als Parameter (kein I/O in `Domain.fs`).

## Acceptance criteria
- [ ] GIVEN ein jährliches NEED-Target mit Ausgaben in früheren Monaten der laufenden Periode, WHEN
      der Fehlbetrag gerechnet wird, THEN zählen diese Ausgaben als gedeckt; die Fälle A2, A5 und A7
      (Beträge erfunden, Verhältnisse wie oben) ergeben 0 (Test der reinen Funktion).
- [ ] Ausgaben vor Beginn der laufenden Periode zählen nicht; Zuflüsse senken die Ausgaben nicht
      unter 0; ohne Rhythmus ändert sich nichts (je ein Test).
- [ ] Fehlt die Aktivität eines Monats der Periode, ist das Ergebnis "Nicht gerechnet" mit Grund,
      kein Betrag (Test).
- [ ] Das Lesen eines Monats ruft YNAB nicht öfter auf als heute (Handler-Test zählt die Aufrufe des
      Ports).
- [ ] Die Parallelrechnung nutzt die neue Rechnung; die Abweichungsliste speichert den Grund.
- [ ] Nach dem Deploy (allein, vor budgeting-eh8xe) zeigt der Prod-Vergleich im laufenden Monat keine
      unerklärte Abweichung mehr. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- **Reihenfolge-Problem:** Der Vergleich in Prod ist nur aussagekräftig, solange budgeting-eh8xe
  nicht deployt ist. eh8xe wird gerade gebaut und darf erst mit budgeting-hbhzf in Prod. Dieser Fix
  muss deshalb vor eh8xe nach Prod, oder aus einem Zweig ohne eh8xe deployt werden.
- Erfasst vom Conductor nach Romans Freigabe des lesenden YNAB-Abrufs, 2026-11-06.
- Verfeinert vom Conductor 2026-11-06 nach Romans Entscheidung (Quelle YNAB-Monatsaktivität, eh8xe
  parken bis zum Vergleich). Der Prod-Vergleich danach läuft wie bei r4kqm: Roman öffnet die Monate,
  der Conductor wertet aus.
