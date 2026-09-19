---
id: ledger-vgfw3
title: "Kassenbuch-Filter im Lesepfad: Kategorie, Zeitraum, Payee, Betrag und Memo serverseitig filtern, zählen und summieren"
status: todo
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-b1xwe]
blocks: [ledger-j8xq4]
tags: [backend, kassenbuch, scheibe, lesepfad]
related_adrs: [0017, 0029]
related_research: []
prior_art: [ledger-b1xwe, ledger-p3n8k, ledger-d4kx8]
---

## Why
Zweite lesende Scheibe. Deckt Romans häufigsten Arbeitsablauf am Kassenbuch ab: Buchungen
finden. Verteilung aus seiner Beschreibung (2026-09-11): Kategorie etwa 70 bis 80 %,
Zeitraum etwa 15 %, Payee, Betrag und Memo je etwa 5 %.

Zwei Dinge sollen besser sein als in YNAB-Web: mehrere Kategorien auf einmal (YNAB kann
nur eine), und der Filter muss am Handy genauso gut gehen. Roman nutzt ihn dort heute nie,
weil er zu umständlich ist.

**Diese Aufgabe ist die Server-Hälfte.** Die Filterleiste, die Kategorienauswahl mit
Mehrfachauswahl, das Popover am PC und die Summenzeile sind `ledger-j8xq4`. Geteilt am
2026-09-19 beim Verfeinern, weil beide Hälften zusammen deutlich mehr sind, als ein
Worker in einem Zug sauber abliefert, und weil die Server-Hälfte für sich testbar ist,
ohne dass eine einzige Schaltfläche existiert.

## What

`getLedgerEntriesPage` kennt heute nur `AccountId`, `Page`, `PageSize`, `IncludeDeleted`
und `OnlyNeedsReview` (`LedgerEntryPageQuery`, `src/Shared/Domain.fs:909-915`). Keiner
der fünf geforderten Filter existiert. Diese Aufgabe baut sie im Lesepfad.

1. **`LedgerEntryPageQuery` um die fünf Filter erweitern:** Kategorien (Menge, nicht
   einzeln), Zeitraum (von/bis), Payee, Betrag, Memo. Kategorie ist ausdrücklich eine
   **Mehrfachauswahl**; das ist der Punkt, an dem BB YNAB schlägt, und muss deshalb bis
   in die Abfrage durchgehalten werden, nicht erst im Client zusammengesetzt.
2. **Alle vier Zählwege ziehen mit:** `loadEntriesPage`, `countEntries`,
   `loadAmountsForQuery` und die Zählung der ausgeblendeten gelöschten Buchungen müssen
   denselben Filter anwenden. Sonst zeigt die Summenzeile eine Summe, die nicht zur
   Trefferzahl gehört.
3. **Summe bleibt in F#**, nie per SQL-`SUM` (ADR 0017): Beträge liegen als exakter
   Dezimal-Text, `Shared.Domain.sumMoney` summiert. Die Filterung ändert daran nichts.
4. **Payee-Vorschläge mit Anzahl über den Treffer:** eine eigene Lesefunktion, die zu
   einem gegebenen Filter die häufigsten Payees samt Anzahl liefert. Ausdrücklich über
   den **gesamten** Treffer, nicht über die geladene Seite und nicht über alle Buchungen
   (Handoff Ablauf 2, Screens 2.7 und 2.13: „Vorschläge aus den 148 Treffern").
5. **Das Verhalten bei gelöschten Buchungen bleibt, wie es ist** (`ledger-p3n8k`,
   `ledger-d4kx8`): ohne `IncludeDeleted` fallen sie aus Seite, Trefferzahl und Summe;
   mit `IncludeDeleted` zählen sie in Trefferzahl und Summe mit, der Kontostand klammert
   sie immer aus. Die neuen Filter dürfen diese Regel nicht aufweichen.

Nicht Teil dieser Aufgabe: Chips, Picker, Popover, Summenzeile, Debounce, das Verhalten
„Zeile passt nicht mehr zum Filter". Alles `ledger-j8xq4`.

## Acceptance criteria
- [ ] `LedgerEntryPageQuery` trägt die fünf Filter, Kategorie als Menge. Ein Aufruf ohne
      gesetzte Filter liefert exakt dasselbe Ergebnis wie heute (Rückwärtskompatibilität,
      durch einen Test gegen den bestehenden Bestand belegt).
- [ ] Ein Kategorie-Filter mit **zwei** Kategorien liefert die Vereinigung beider, nicht
      den Schnitt und nicht nur die erste.
- [ ] Trefferzahl und Summe gelten über den **gesamten** Treffer, nicht über die geladene
      Seite: bei Seitengröße 2 und fünf Treffern meldet die Abfrage `TotalCount = 5` und
      die Summe aller fünf.
- [ ] Die Summe wird in F# über `sumMoney` gebildet, nicht per SQL-`SUM` (ADR 0017).
      Durch einen Test mit Beträgen belegt, die als Gleitkommazahl driften würden.
- [ ] Zeitraum filtert einschließlich der Randtage (eine Buchung genau am Von- und eine
      genau am Bis-Datum sind im Treffer).
- [ ] Payee-, Memo- und Betragsfilter sind einzeln und in Kombination wirksam; mehrere
      gesetzte Filter wirken als Und-Verknüpfung.
- [ ] Die Payee-Vorschlagsabfrage liefert zu einem Filter die Payees mit ihrer Anzahl
      **innerhalb dieses Treffers**. Belegt durch einen Fall, in dem ein Payee außerhalb
      des Filters häufiger vorkommt als innerhalb und trotzdem die kleinere Zahl
      erscheint.
- [ ] Die Regel für gelöschte Buchungen gilt unverändert weiter, auch mit gesetzten
      Filtern: ohne `IncludeDeleted` draußen, mit `IncludeDeleted` in Trefferzahl und
      Summe, im Kontostand nie.
- [ ] Tests laufen gegen In-Memory-SQLite, nie gegen die Entwicklungsdatenbank (ADR 0030).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12, geteilt in Server- und Oberflächen-Hälfte am
2026-09-19. Brief mit Schrittfolge: `docs/ux/kassenbuch/briefs.md`, Ablauf 2 und 3.
Übergabepaket mit den fertigen Screens:
`docs/ux/kassenbuch/handoff-ablauf-2-buchungen-finden.md`.

Der Chip „Zu prüfen" gehört nicht hierher: `OnlyNeedsReview` existiert bereits in der
Abfrage (`ledger-kdnnz`). Der Available-Betrag je Kategorie kommt aus `ledger-kwwxz`.

**Beim Bauen zu beachten:** Der Bestand liegt bei rund 14.000 Buchungen. Ein Filter, der
erst nach dem Laden aller Zeilen greift, erfüllt die Kriterien zwar, trägt aber nicht.
Die Filterung gehört in die Abfrage, nicht hinter sie.
