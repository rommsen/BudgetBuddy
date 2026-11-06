---
id: budgeting-pf8n7
title: "Fehlbetrag mit Fälligkeit: die laufende Periode beginnt im Monat der vorigen Fälligkeit, bei der ersten Fälligkeit im Anlegemonat des Ziels"
status: done
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

## Outcome

Die Parallelrechnung zählt die Ausgaben der Periode jetzt ab dem Monat der vorigen Fälligkeit, einschließlich. Bei der ersten Fälligkeit eines Ziels zählt sie ab dem Anlegemonat. Targets anwenden, "fehlt" und die Vorschau rechnen weiter mit YNABs `goal_under_funded`. An Geldentscheidungen hat sich nichts geändert.

**Regel** (`src/Server/Budgeting/Domain.fs`):
- `periodMonthsBefore rhythm createdIn month due` hat den neuen Parameter `createdIn: string option` ("YYYY-MM-01"). Die Periode beginnt im Monat der vorigen Fälligkeit (`dueFrom` um einen Schritt zurück) und endet wie bisher vor dem gelesenen Monat. Wird das gespeicherte Datum für den gelesenen Monat nicht verschoben (`stepsUntil` = 0, also erste Fälligkeit) und ist `createdIn` lesbar, beginnt sie im Anlegemonat. Ein unlesbarer Anlegemonat zählt wie keiner. Ohne Rhythmus gibt es weiter keine Monate.
- `periodMonthsOf target month` gibt `target.CreatedIn` weiter. `underfunded` nimmt die Monate jetzt über `periodMonthsOf`, damit `periodMonthsNeeded` und die Rechnung immer dieselben Monate meinen.

**Daten:** `Target` (`src/Shared/Budgeting.fs`) hat das neue Feld `CreatedIn: string option`. Der gemeinsame Decoder `goalTarget` (`src/Server/YnabClient.fs`, für Monats- und Kategorie-Antwort) liest `goal_creation_month`. Fehlt das Feld, ist der Wert `None`. Alle Target-Literale in den Tests und die Fixtures in `scripts/budget-money-tab.test.mjs`, `budget-page.test.mjs`, `targets-apply.test.mjs` und `target-change.test.mjs` haben `CreatedIn: null` bzw. `None` bekommen.

**Tests** (`src/Tests/UnderfundedComparisonTests.fs`, neue Liste "Periode ab voriger Fälligkeit und Anlegemonat", 6 Fälle, Beträge erfunden, Kommentar nennt den dritten Prod-Vergleich vom 2026-11-06):
- F2: Eine Ausgabe im Monat der vorigen Fälligkeit zählt als gedeckt (0). Vor dem Fix ergab das 10.
- F3: Ein Ziel vor seiner ersten Fälligkeit zählt ab dem Anlegemonat (0). Vor dem Fix ergab das 10.
- Intakt und Grenzen: ohne Anlegemonat gilt die vorige Fälligkeit, bei verschobenem Datum zählt der Anlegemonat nicht, Anlegemonat im Vormonat, im gelesenen Monat, danach oder unlesbar, Fälligkeit im gelesenen Monat, ohne Rhythmus keine Ausgaben, auch mit Anlegemonat.
- Vor dem Fix waren 5 der 6 Fälle rot. Nur "ohne Rhythmus" war grün, und das soll so bleiben.
- Die b9869-Tests sind an die neue Grenze angepasst und nicht gelöscht: A2, A5, A7, Grenzen von `periodMonthsBefore` (neu: "vorige Fälligkeit im Vormonat" und "vorige Fälligkeit im gelesenen Monat" bei wöchentlichem Rhythmus), Zuflüsse, fehlender Monat, Vergleich, `periodMonthsNeeded` und der Handler-Test, der jetzt 2032-03 bis 2032-10 liest. Der Test "Ausgaben im Monat vor der vorigen Fälligkeit zählen nicht" ersetzt "Ausgaben vor Beginn der laufenden Periode zählen nicht": eine Ausgabe im Februar bei voriger Fälligkeit im März zählt nicht.
- Decoder (`src/Tests/TargetChangeTests.fs`): Der bestehende Test der reinen Funktion `categoryTargetDecoder` liest jetzt `goal_creation_month` (Some "2026-05-01"), ohne das Feld ergibt er `None`. Gegenprobe: Liefert der Decoder immer `None`, wird der Test rot.
- `./verify.sh`: VERIFY: PASS (dotnet 4147 grün, 13 übersprungen nur "(requires .env)", node 311 grün).

**Für die Integration von budgeting-eh8xe (geparkt), muss dort nachgezogen werden:**
- Die Target-Regel von eh8xe und ihr Codec brauchen `CreatedIn` ("YYYY-MM-01", optional; ein Ereignis ohne das Feld wird als `None` gelesen).
- `TargetTakenOverFromYnab` übernimmt `CreatedIn` aus `goal_creation_month` (also aus `Target.CreatedIn` des YNAB-Targets).
- `TargetSet` (Ändern in BB) behält das `CreatedIn` des bisherigen Targets. Ein in BB neu angelegtes Target bekommt seinen Anlegemonat (Monat des Anlegens), sonst `None`.
- Der Weg vom Regelzustand zurück zum `Target`, das `periodMonthsOf` bekommt, muss `CreatedIn` setzen. Sonst rechnet BB nach eh8xe wieder ohne Anlegemonat, und F3 wird wieder ungleich.
- Literale von `Target` in eh8xe-Tests brauchen `CreatedIn = None`.

**Offen:** Den Prod-Vergleich nach dem Deploy (höchstens der erklärte Refill-Rest F1) muss Roman ansehen [human-eye]. F1 bleibt bei budgeting-hbhzf.

**Override (C):** `/review-agents` vorschlagen, mit `qa-milestone-reviewer` (Tests geändert) und `backend-code-quality` (`src/Server/**`).

**Kann-Funde:** keine.
