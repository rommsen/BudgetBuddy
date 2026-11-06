---
id: budgeting-k7r2w
title: "Targets prüfen zählt eine neue Kategorie erst ab ihrer ersten Buchung im Kassenbuch, statt die Monate davor als 0 € mitzuzählen. Unter 3 gezählten Monaten steht sie unter Passt mit „neu seit <Monat>“."
status: doing
type: bug
context: budgeting
created: 2026-11-06
completed:
depends_on: [budgeting-c4ve4]
blocks: []
tags: [budget, target, pruefen, kassenbuch]
related_adrs: [0082]
related_research: []
prior_art: [budgeting-c4ve4, budgeting-q4v8n]
---

## Why
Roman am 2026-11-06 zur Zeile Strom (Screenshot in Prod): „das gibt es ja noch gar keine zwölf Monate. Deswegen rechnet der da jetzt einen zwölf Monatschnitt, der gar keinen Sinn ergibt. Weil seit es das Budget gibt, hat es immer gepasst.“ Die Zeile zeigt „Ø 56 € im Monat“, „1 von 12 Monaten über dem Target“ und „60 € übernehmen“ bei einem Target von 127 € monatlich.

Ursache: `spendingOf` in `src/Server/Budgeting/QueryHandlers.fs` füllt die 12 vollen Monate vor dem laufenden mit 0 € auf, `targetCheckRow` in `src/Shared/Budgeting.fs` teilt durch alle gezählten Monate. Die Monate, bevor es die Kategorie gab, drücken den Schnitt. YNAB liefert kein Anlegedatum einer Kategorie, die einzige Quelle ist die erste Buchung im Kassenbuch.

## What
Entscheidungen von Roman (2026-11-06, AskUserQuestion):

1. **Zählbeginn ist die erste Buchung der Kategorie überhaupt** im Kassenbuch (nicht nur im 12-Monats-Fenster). Der Server liest sie für alle sichtbaren Kategorien in einer Abfrage (kein N+1, `IN @Ids` und `GROUP BY`, gelöschte Buchungen und Splits so behandelt wie in `loadCategoriesEntries`). Sie kommt als neues Feld in `TargetCheckSource` (etwa `FirstBooking: string option`, Monatserster „YYYY-MM-01“ oder Tag, der Worker wählt und begründet).
   - Gezählt werden die vollen Monate ab dem späteren der beiden Monate: Monat der ersten Buchung (einschließlich) und Monat nach der letzten Target-Änderung (wie heute `s.Month > changed`).
   - Liegt die erste Buchung vor dem Fenster, ändert sich nichts. Eine alte, selten genutzte Kategorie mit einer Buchung neulich bleibt also, wie sie ist (etwa unter „Gibst du nie aus?“).
   - Ohne jede Buchung bleibt es wie heute (12 Monate mit 0 €, „Gibst du nie aus?“ möglich).
2. **Weniger gezählte Monate als nötig** (dieselbe Schwelle wie `tooFew`: 3, bei einem längeren Rhythmus dessen Länge) seit der ersten Buchung: Gruppe Passt mit einer neuen `FitsNote` und dem Zusatz in Teal „neu seit Juli 2026“ (Monat ausgeschrieben, Monat der ersten Buchung). Gleiche Farbe und Stelle wie „neu gesetzt am …“.
   - Erklären sowohl die erste Buchung als auch eine Target-Änderung die zu wenigen Monate, zeigt die Zeile den Zusatz zum späteren der beiden Ereignisse.
3. **Balken:** Monate vor der ersten Buchung als `NotCounted` (dunkel), wie heute die Monate vor einer Target-Änderung. Die Zahl der Balken bleibt wie heute.
4. **Texte:** Ø, „x von N Monaten über dem Target“, Vorschlag und Panel-Satz rechnen nur mit den gezählten Monaten (folgt aus `CountedMonths`, bitte prüfen). Der Erklärsatz im Seitenkopf (`TargetCheck.fs`, „… höchstens 12, ab der letzten Änderung des Targets.“) nennt auch die erste Buchung, Vorschlag: „… höchstens 12, ab der ersten Buchung oder der letzten Änderung des Targets.“
5. **ADR 0082** bekommt einen Nachtrag 2026-11-06: Zählbeginn erste Buchung, neue FitsNote, Vorrang beim Zusatz.

Erwartung für den Strom-Fall nach dem Fix: rund 5 gezählte Monate, 1 davon über 127 €, Ø rund 134 €. Damit gilt nicht „mehr“ (dafür müsste die Hälfte der Monate darüber liegen), die Zeile steht unter Passt.

## Acceptance criteria
- [ ] Regressionstest auf Domain-Ebene (`targetCheckRow`), rot vor dem Fix: GIVEN Target 127 € monatlich, erste Buchung im 6. Monat des Fensters, davor keine Buchung, danach Ausgaben um 130 € mit einem Monat deutlich darüber; WHEN eingeordnet; THEN `CountedMonths` beginnt beim Monat der ersten Buchung, Ø über diese Monate, Gruppe Passt, kein Vorschlag.
- [ ] Intakt-Szenario: GIVEN erste Buchung vor dem Fenster, im Fenster nur eine Buchung im vorletzten Monat; THEN 12 gezählte Monate und Gruppe „Gibst du nie aus?“ wie heute.
- [ ] Intakt-Szenario: GIVEN keine Buchung überhaupt (`FirstBooking = None`); THEN Einordnung wie heute.
- [ ] Erste Buchung im vorletzten vollen Monat (2 gezählte Monate): Gruppe Passt, `Note` ist die neue FitsNote, `fitsNoteText` ergibt „neu seit <Monat> <Jahr>“ mit ausgeschriebenem Monat.
- [ ] Jahres-Target (`Years 1`) mit erster Buchung im 9. Monat des Fensters: Passt mit „neu seit …“, nicht „mehr“.
- [ ] Vorrang: Target-Änderung nach der ersten Buchung und beide zu kurz: der Zusatz nennt die Änderung. Erste Buchung nach der Änderung: der Zusatz nennt die erste Buchung. Je ein Test.
- [ ] Grenze: der Monat der ersten Buchung zählt mit, der Monat davor nicht (Test mit genau 3 Monaten ab erster Buchung: wird eingeordnet, nicht „neu seit“).
- [ ] `checkBars`: Monate vor der ersten Buchung haben den Ton `NotCounted`.
- [ ] Server: die erste Buchung je Kategorie kommt aus einer Abfrage für alle Kategorien. Handler-Test mit In-Memory-SQLite: Kategorie mit Buchungen vor dem Fenster, Kategorie mit erster Buchung im Fenster, Kategorie nur in einem Split, gelöschte Buchung zählt nicht, Kategorie ohne Buchung.
- [ ] `withWrittenTarget` behält die erste Buchung (Test).
- [ ] Fixtures in `scripts/target-check.test.mjs` und `scripts/budget-page.test.mjs` tragen das neue Feld. Kein neuer Browser-Test, das Verhalten ist unter .NET prüfbar.
- [ ] Erklärsatz im Kopf nennt die erste Buchung.
- [ ] ADR 0082 hat den Nachtrag, die README von budgeting beschreibt die Regel.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Zeile Strom in Prod steht unter Passt oder zeigt einen Schnitt über die Monate seit der ersten Buchung. [human-eye]

## Notes
- Die Skizze (`budgeting-c4ve4`) kennt diesen Fall nicht. Der Zusatz „neu seit …“ folgt Form und Farbe von „neu gesetzt am …“ (Teal, ADR 0075 Nachtrag), daher keine Abweichung von der Skizze.
- Prüfregel heute: `targetCheckRow`, `tooFew`, `checkBars`, `withWrittenTarget` in `src/Shared/Budgeting.fs`; Zeilentexte in `src/Client/Components/Budget/TargetCheck.fs`.
- Ein neues Pflichtfeld in `TargetCheckSource` bricht Playwright-Fixtures erst zur Laufzeit (CLAUDE.md, Laufzeitfallen).
- Monatsnamen: vorhandene Funktion für ausgeschriebene Monate suchen (`monthLabel` o. ä.), nicht neu bauen.
