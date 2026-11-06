---
id: budgeting-b9869
title: "Fehlbetrag mit Fälligkeit: Ausgaben seit Beginn der laufenden Periode zählen als gedeckt, wie bei YNAB"
status: done
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

## Outcome

Der Fehlbetrag "Mit Fälligkeit" zählt jetzt wie YNAB die Ausgaben der laufenden Periode als gedeckt. Die Rechnung gilt weiter nur in der Parallelrechnung. Targets anwenden, "fehlt" und die Vorschau rechnen unverändert mit YNABs `goal_under_funded`.

**Reine Regel** (`src/Server/Budgeting/Domain.fs`):
- `underfunded target month assigned carryover activity earlierActivity`: Der neue letzte Parameter ist die Aktivität der Kategorie in früheren Monaten (`Map<"YYYY-MM-01", Money>`). Zweig `DueDateRule`: `ceil((Ziel − (Übertrag + Ausgaben)) / Monate) − Zugewiesen`, unten bei 0. Kein I/O.
- `periodMonthsBefore rhythm month due`: Das sind die Monate der laufenden Periode vor dem gelesenen Monat, vom Monat nach der vorigen Fälligkeit (`dueFrom` um einen Schritt zurück, wieder vom gespeicherten Datum aus gerechnet) bis zum Vormonat. Ohne Rhythmus ist die Liste leer. `periodMonthsOf target month` liefert dieselben Monate nur für "Mit Fälligkeit".
- Ausgaben = max 0 (−Summe der Aktivität dieser Monate). Fehlt ein Monat, kommt `NotComputed PeriodActivityMissingText` ("Aktivität der Periode fehlt") zurück.
- `dueFrom` ist nur intern umgebaut (gemeinsame Hilfen `rhythmShift` und `stepsUntil`), das Verhalten ist gleich. `underfundedRuleOf` und `remainingMonths` sind unverändert.
- `compareUnderfunded month earlierActivity` nimmt die Aktivität je Kategorie entgegen.

**Zustand** (`src/Server/Budgeting/Persistence.fs`): Die Tabelle `category_month_activity (month, category_id, activity_milliunits, read_at)` wird in `initializeBudgetingSchema` mit `CREATE TABLE IF NOT EXISTS` angelegt. `recordMonthActivityWith connect readAt ynabMonth` ersetzt die Zeilen des Monats in einer Transaktion. `loadMonthActivitiesWith connect months` liest in einer Abfrage (`WHERE month IN @months`, Row-Typ `CategoryMonthActivityRow` mit `[<CLIMutable>]`, keine Optionsfelder).

**Füllen:** `CommandHandlers.observeUnguarded` hält nach der Beobachtung die Aktivität des eben gelesenen Monats fest. Ein Fehler dabei geht nur ins Log. Diesen Weg nehmen die Budget-Seite (`observeMonth`), alle Befehle und die Übernahme im Hintergrund (`ensureTakenOver`). Es gibt keinen zusätzlichen YNAB-Aufruf. Die Aktivität wird erst nach der Beobachtung festgehalten, damit Tests, die die erste Verbindung scheitern lassen, weiter die Beobachtung treffen.
**Lesen:** `QueryHandlers.budgetMonthWith` sammelt die Periodenmonate aller Targets und lädt sie in einer Abfrage aus der Datenbank. Danach läuft `compareUnderfunded`. YNAB wird nie nach früheren Monaten gefragt.

**Wie frühere Monate gefüllt werden (wichtig für den Prod-Vergleich):** Der TakeoverWorker liest nur Monate, die noch nicht übernommen sind. Schon übernommene Monate füllt er nicht nach. In Prod ist `category_month_activity` nach dem Deploy deshalb leer. Roman muss die Monate der laufenden Perioden einmal auf der Budget-Seite öffnen (bei jährlichen Targets bis zu 11 Monate zurück, also 2025-12 bis 2026-10) und **danach** den laufenden Monat 2026-11. Bis dahin stehen die jährlichen Targets mit Fälligkeit als "Nicht gerechnet / Aktivität der Periode fehlt" in der Liste, nie mit einem falschen Betrag. Ein automatisches Nachfüllen durch den Worker würde `takeoverStep` ändern. Das habe ich wegen budgeting-eh8xe bewusst nicht gebaut (siehe Kann-Funde).

**Belegt und angenommen:**
- Belegt durch A2 und A7 (und A5, wenn man die Zahlen wie im Test rekonstruiert: Übertrag 0, nichts zugewiesen, 3 Monate): "gedeckt = Übertrag + Ausgaben früherer Periodenmonate" ergibt YNABs 0. In allen drei Fällen ist die Aktivität im gelesenen Monat 0 (Kontostand = Übertrag + Zugewiesen).
- **Abweichung vom Task, begründet:** Der Task nimmt den gelesenen Monat in die Periode auf. Ich zähle ihn nicht mit. Der Übertrag ist der Stand zu Beginn des Monats, Ausgaben im gelesenen Monat verringern ihn nicht. YNABs `goal_overall_funded` (= Kontostand + Ausgaben) bleibt bei einer Ausgabe im laufenden Monat gleich. Zählte BB sie zusätzlich, rechnete BB doppelt und verlangte zu wenig. Die Befund-Zahlen enthalten keine Ausgabe im gelesenen Monat, deshalb ist das eine Annahme. Der Prod-Vergleich prüft sie an Kategorien mit Ausgaben im laufenden Monat.
- Angenommen: Die Periode beginnt im Monat nach der vorigen Fälligkeit. Der Befund zeigt keinen Periodenwechsel. Ob YNAB stattdessen ab dem Anlegen des Ziels zählt (Ziel jünger als ein Rhythmus), ist offen.
- Angenommen: Zuflüsse werden über die Periode summiert, nicht je Monat abgeschnitten, und das Ergebnis ist unten bei 0. Kein Fall im Befund hat einen Zufluss.
- Angenommen: Es gilt für "Set aside another" und "Refill up to" gleich (A2 ist Refill, belegt). Für Wochen-Rhythmus und TBD mit Rhythmus gibt es keine Daten.

**Tests** (`src/Tests/UnderfundedComparisonTests.fs`, 13 neu, alle Beträge erfunden):
- Reine Funktion, Liste "Ausgaben der laufenden Periode": A2, A5, A7 als Regressionstests mit Befund-Kommentar, Periodengrenzen von `periodMonthsBefore`, Ausgaben vor dem Periodenbeginn, Zuflüsse, ohne Rhythmus, fehlender Monat.
- Vergleich: je Kategorie die Aktivität, einmal gleich und einmal mit Grund.
- Handler mit zählendem Port: Nach dem Lesen von April bis Oktober macht das Lesen im November genau einen `FetchMonth`, und das Ergebnis ist gleich YNAB.
- Ablage: Fehlender Monat speichert "Nicht gerechnet" mit Grund und Formel, mit einem Aufruf. Ein zweites Lesen überschreibt den Monat. Laden nur der gefragten Monate. Übernahme über `ensureTakenOverWith` hält die Aktivität fest (Weg des TakeoverWorker).
- Der `compute`-Helfer der bestehenden Tests gibt jedem nötigen Periodenmonat Aktivität 0, also Verhalten wie vorher.
- Mutante "Ausgaben nicht addiert" geprüft: 6 Tests rot (A2, A5, A7, Zuflüsse, Vergleich, Handler).
- `./verify.sh`: VERIFY: PASS (dotnet 4136 grün, 13 übersprungen nur "(requires .env)", node 311 grün).

**Für den Conductor:**
- Ein erster Gate-Lauf blieb in Expecto hängen. Derselbe Hänger trat mit der Tests.dll von `main` (09:05) auf, an denselben Tests (CmdTiming-, YnabClient-Fake-Handler-, Netz-Wächter-Tests), bei Last über 20. Er ist also nicht von dieser Änderung. Drei hängende Testprozesse laufen deshalb noch: PID 4290 (mit `verify.sh` PID 4216), 12565 und 13601, alle `dotnet .../Tests.dll`. Ich durfte sie nicht beenden. Bitte aufräumen.
- Override (C): Reviews vorschlagen: `qa-milestone-reviewer` (Tests), `backend-code-quality` (src/Server).

**Kann-Funde:**
- Der TakeoverWorker könnte übernommene Monate ohne Aktivitätszustand einmal nachlesen (höchstens ein Aufruf je Minute). Dann müsste Roman keine Monate von Hand öffnen. Das würde `takeoverStep` und `ensureTakenOverWith` ändern, die budgeting-eh8xe ebenfalls anfasst.
- Die Aktivität fehlt je Kategorie: Eine Kategorie, die YNAB in einem früheren Monat nicht liefert, ergibt dort "Nicht gerechnet".
