---
id: budgeting-k7r2w
title: "Targets prüfen zählt eine neue Kategorie erst ab ihrer ersten Buchung im Kassenbuch, statt die Monate davor als 0 € mitzuzählen. Unter 3 gezählten Monaten steht sie unter Passt mit „neu seit <Monat>“."
status: done
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
- [x] Regressionstest auf Domain-Ebene (`targetCheckRow`), rot vor dem Fix: GIVEN Target 127 € monatlich, erste Buchung im 6. Monat des Fensters, davor keine Buchung, danach Ausgaben um 130 € mit einem Monat deutlich darüber; WHEN eingeordnet; THEN `CountedMonths` beginnt beim Monat der ersten Buchung, Ø über diese Monate, Gruppe Passt, kein Vorschlag.
- [x] Intakt-Szenario: GIVEN erste Buchung vor dem Fenster, im Fenster nur eine Buchung im vorletzten Monat; THEN 12 gezählte Monate und Gruppe „Gibst du nie aus?“ wie heute.
- [x] Intakt-Szenario: GIVEN keine Buchung überhaupt (`FirstBooking = None`); THEN Einordnung wie heute.
- [x] Erste Buchung im vorletzten vollen Monat (2 gezählte Monate): Gruppe Passt, `Note` ist die neue FitsNote, `fitsNoteText` ergibt „neu seit <Monat> <Jahr>“ mit ausgeschriebenem Monat.
- [x] Jahres-Target (`Years 1`) mit erster Buchung im 9. Monat des Fensters: Passt mit „neu seit …“, nicht „mehr“.
- [x] Vorrang: Target-Änderung nach der ersten Buchung und beide zu kurz: der Zusatz nennt die Änderung. Erste Buchung nach der Änderung: der Zusatz nennt die erste Buchung. Je ein Test.
- [x] Grenze: der Monat der ersten Buchung zählt mit, der Monat davor nicht (Test mit genau 3 Monaten ab erster Buchung: wird eingeordnet, nicht „neu seit“).
- [x] `checkBars`: Monate vor der ersten Buchung haben den Ton `NotCounted`.
- [x] Server: die erste Buchung je Kategorie kommt aus einer Abfrage für alle Kategorien. Handler-Test mit In-Memory-SQLite: Kategorie mit Buchungen vor dem Fenster, Kategorie mit erster Buchung im Fenster, Kategorie nur in einem Split, gelöschte Buchung zählt nicht, Kategorie ohne Buchung.
- [x] `withWrittenTarget` behält die erste Buchung (Test).
- [x] Fixtures in `scripts/target-check.test.mjs` und `scripts/budget-page.test.mjs` tragen das neue Feld. Kein neuer Browser-Test, das Verhalten ist unter .NET prüfbar.
- [x] Erklärsatz im Kopf nennt die erste Buchung.
- [x] ADR 0082 hat den Nachtrag, die README von budgeting beschreibt die Regel.
- [x] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Zeile Strom in Prod steht unter Passt oder zeigt einen Schnitt über die Monate seit der ersten Buchung. [human-eye]

## Notes
- Die Skizze (`budgeting-c4ve4`) kennt diesen Fall nicht. Der Zusatz „neu seit …“ folgt Form und Farbe von „neu gesetzt am …“ (Teal, ADR 0075 Nachtrag), daher keine Abweichung von der Skizze.
- Prüfregel heute: `targetCheckRow`, `tooFew`, `checkBars`, `withWrittenTarget` in `src/Shared/Budgeting.fs`; Zeilentexte in `src/Client/Components/Budget/TargetCheck.fs`.
- Ein neues Pflichtfeld in `TargetCheckSource` bricht Playwright-Fixtures erst zur Laufzeit (CLAUDE.md, Laufzeitfallen).
- Monatsnamen: vorhandene Funktion für ausgeschriebene Monate suchen (`monthLabel` o. ä.), nicht neu bauen.

## Outcome

Targets prüfen zählt eine neue Kategorie jetzt erst ab ihrer ersten Buchung im Kassenbuch. Die Monate davor gehen nicht mehr mit 0 € in Ø, "x von N Monaten" und Vorschlag ein. `./verify.sh`: VERIFY: PASS (dotnet 4209 grün, node 316 grün).

**Domäne** (`src/Shared/Budgeting.fs`):
- `TargetCheckSource.FirstBooking: string option` ist der Berliner Tag "YYYY-MM-DD" der ersten Buchung überhaupt, `None` ohne Buchung. Ich habe den Tag gewählt und nicht den Monatsersten: er passt zu `TargetChangeMark` (auch ein Tag), und der Vorrang zwischen erster Buchung und Änderung lässt sich damit auf den Tag genau entscheiden. Den Monat leitet `targetCheckRow` über `monthOfDay` ab.
- `targetCheckRow` zählt die vollen Monate ab dem späteren Beginn: Monat nach der letzten Änderung oder Monat der ersten Buchung (einschließlich). Liegt die erste Buchung vor dem Fenster, bleibt alles wie bisher. Ohne Buchung ebenso.
- Neue `FitsNote` `NewSince of month` ("YYYY-MM-01"). `fitsNoteText` ergibt "neu seit September 2026". Die Note greift nur, wenn die erste Buchung Monate des Fensters abschneidet, sonst kommt sie nicht vor. Die Schwelle ist dieselbe wie bisher (3, bei längerem Rhythmus P). Erklären erste Buchung und Änderung beide die zu wenigen Monate, nennt der Zusatz das spätere Ereignis nach Tag. Bei gleichem Tag gewinnt die Änderung.
- `checkBars` brauchte keine Änderung: Monate außerhalb von `CountedMonths` sind schon `NotCounted`. Die Zahl der Balken bleibt 12. Ø, "x von N Monaten über dem Target", Vorschlag und Panel-Satz rechnen über `CountedMonths` bzw. `n`, ich habe das geprüft.
- `monthLabel` ist aus `src/Client/Components/Budget/Month.fs` nach `Shared.Budgeting` gezogen, damit `fitsNoteText` den Monat ausschreiben kann (dieselbe Funktion, nichts neu gebaut, `BudgetPageTests` deckt sie weiter ab).

**Server**:
- `Ledger.Persistence.loadFirstEntryDaysByCategory` liest für alle sichtbaren Kategorien in einer Abfrage `substr(MIN(date), 1, 10)` je Kategorie, über `category_id IN @categoryIds` und `ledger_entry_split_categories` (`UNION ALL`, `GROUP BY`), ohne `deleted_in_ynab`.
- `Ledger.QueryHandlers.getFirstBookingDayByCategory` reicht das weiter. Der Port `TargetCheckPort` hat das neue Feld `LoadFirstBookings`. Ein Lesefehler kommt als `SpendingUnreadableText`.

**Client**: `checkIntroText` lautet jetzt "… höchstens 12, ab der ersten Buchung oder der letzten Änderung des Targets." Der Zusatz läuft über denselben `noteClass` wie "neu gesetzt am …" (Teal).

**Tests**:
- `src/Tests/TargetCheckTests.fs`, Liste "ab der ersten Buchung (budgeting-k7r2w)": Regression Strom (vor dem Fix rot, damals "weniger" mit Vorschlag), erste Buchung vor dem Fenster, keine Buchung, vorletzter Monat mit "neu seit September 2026", jährlich mit Juli, beide Vorrang-Fälle, Grenze mit 3 Monaten. Dazu `withWrittenTarget` behält die erste Buchung.
- `src/Tests/TargetCheckClientTests.fs`: Mini-Balken vor der ersten Buchung `NotCounted`.
- `src/Tests/TargetCheckQueryTests.fs`: SQLite-Test der Abfrage (vor dem Fenster, im Fenster, nur Split, gelöschte zählt nicht, ohne Buchung) sowie Handler-Test (Tag an der Kategorie, ein Aufruf für alle). Lesefehler und Endpoint sind ergänzt.

**Fixtures**: `scripts/target-check.test.mjs` hat `FirstBooking: null`. `scripts/budget-page.test.mjs` enthält keine `TargetCheckSource`-Fixture (geprüft mit grep), dort war nichts nachzuziehen. Es gibt keinen neuen Browser-Test.

**Offen** [human-eye]: Die Zeile Strom in Prod nach dem Deploy ansehen.

**ADR 0082:** Nachtrag 2026-11-06 (budgeting-k7r2w) angehängt: Zählbeginn erste Buchung, FitsNote `NewSince`, Vorrang beim Zusatz, Balken.
