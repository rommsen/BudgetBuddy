---
id: ledger-vgfw3
title: "Kassenbuch-Filter im Lesepfad: Kategorie, Zeitraum, Payee, Betrag und Memo serverseitig filtern, zählen und summieren"
status: done
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-b1xwe]
blocks: [ledger-j8xq4]
tags: [backend, kassenbuch, scheibe, lesepfad]
related_adrs: [0017, 0029, 0036]
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

## Verifier note (iteration 1)

**REASONS:**
- Akzeptanzkriterium "Zeitraum filtert einschließlich der Randtage" ist vom Produktionscode
  für echte Daten NICHT erfüllt. `src/Server/Ledger/Persistence.fs:632-636` vergleicht
  `date <= @dateTo` (und `:626-630` `date >= @dateFrom`) als rohen Text und bindet
  `dateToText d.Date`, also `DateTime.ToString("o")`. Dieses Format ist Kind-abhängig und
  NICHT gleich lang: `DateTimeKind.Utc` hängt `Z` an, `Local` `+02:00`, `Unspecified` nichts
  (nachgemessen: `2026-06-12T00:00:00.0000000Z` vs. `...0000000` vs. `...0000000+02:00`).
  Jede vom Spiegel erzeugte Buchung — der Großteil des ~14.000-Buchungen-Bestands — liegt
  MIT `Z` vor, weil `Server/YnabObservations.fs:116` `DateTime.SpecifyKind(date, DateTimeKind.Utc)`
  macht und `Persistence.fs:304` `date = dateToText entry.Date` schreibt. Unter SQLites
  BINARY-Kollation ist `'…0000000Z' <= '…0000000'` falsch und `'…0000000Z' <= '…0000000+02:00'`
  ebenfalls (mit sqlite3 nachgeprüft). Ein aus einem nicht-Utc-DateTime gebauter `DateTo`-Rand
  verliert also still jede gespiegelte Buchung genau am Bis-Tag. Der symmetrische Fall ist
  genauso kaputt: schickt der Client einen Utc-Rand, schließt `DateFrom` die Comdirect-/
  Quick-Add-Buchungen am Von-Tag aus (die kommen aus `DateTime.Parse row.booking_date`,
  `src/Server/Persistence.fs:851`, also ohne `Z`). Bei gemischten Kinds in der `date`-Spalte
  macht KEINE Wahl des Client-Kinds beide Ränder richtig.
- Der AC5-Test widerlegt das nicht: `src/Tests/LedgerPageTests.fs:1454` sät über
  `seedEntryFull` mit `DateTime(2026, 6, 12)`-Literalen (Kind = Unspecified) — genau das
  Kind, das der Spiegel-Schreibpfad nie erzeugt — und baut den Rand aus denselben Literalen.
  Beide Seiten tragen damit identische Suffixe, der Randvergleich kann nie fehlschlagen.
  Genau das Risiko, das das nicht-leere `TDD_SKIPPED` benannt hat. Die Suite selbst ist echt
  grün (940 bestanden, 7 übersprungen, 0 Fehler, vom Verifier selbst gelaufen) — ein latenter
  Defekt, kein fehlschlagender Test.
- ADR 0036 Decision 5 beruht auf einer Fehllesung der zitierten ADR. Sie behauptet, ADR 0017
  Punkt 4 lege ein Zeitstempel-Format "fest, gleich lang und ohne Vorzeichen" fest (dieselbe
  Formulierung steht als Code-Kommentar in `Persistence.fs:621-623`). ADR 0017 Punkt 4 sagt
  nur "ISO-8601-Roundtrip-Text (`"o"` plus `RoundtripKind`)" — Roundtrip ist genau die
  Eigenschaft, die die Textlänge Kind-abhängig macht. Die falsche Prämisse ist das, was den
  direkten SQL-Textvergleich überhaupt rechtfertigt; der ADR-Text muss deshalb zusammen mit
  dem Code korrigiert werden, nicht nur der Code.

**SUGGESTED_FIX:** Den Zeitraum-Vergleich so normalisieren, dass er nicht mehr vom
Kind-Suffix des gespeicherten Werts abhängt (z. B. beidseitig auf den Datumsanteil / die
ersten 10 Zeichen vergleichen, oder Rand und gespeicherten Text auf eine kanonische Form
bringen). Dazu einen Test, der mindestens eine Randtag-Buchung so sät, wie es der Spiegel tut
(`DateTime.SpecifyKind(date, DateTimeKind.Utc)`), plus eine ohne Kind, und für einen Von-/
Bis-Tag-Rand prüft, dass beide im Treffer erscheinen. ADR 0036 Decision 5 (und den passenden
Kommentar in `buildFilterClause`) korrigieren, so dass dort nicht mehr steht, ADR 0017
garantiere einen gleich langen, vorzeichenfreien Zeitstempel-Text.

**Vom Verifier ausdrücklich als in Ordnung befunden (nicht erneut anfassen):** Scope sauber
(7 Dateien, kein `.agentheim/`-Pfad, keine Task-Datei, `Client/Components/Ledger/State.fs`
nur die sechs neutralen Felder); die vier Zählwege plus `loadPayeeSuggestions` teilen sich
echt eine `EntryFilter`/`buildFilterClause`; `countNeedsReviewEntries` ungefiltert zu lassen
ist in ADR 0036 §6 und im README-Delta dokumentiert und deckt sich mit der bestehenden
"Zu prüfen"-Chip-Definition; der Betrags-Filter wird nirgends per SQL verglichen (kein
`CAST(amount AS REAL)`, F#-seitiges `amountMatches` über `loadAllMatching`); die zwei neuen
Indizes liegen im bestehenden idempotenten `CREATE INDEX IF NOT EXISTS`-Block; Tests laufen
gegen die In-Memory-/Test-DB; AC2/AC3/AC6/AC7/AC8 sind echt falsifizierend.

**Schwächer, aber akzeptiert:** AC1 prüft den ungefilterten Pfad gegen die eigene Saat statt
gegen eine aufgezeichnete Vorher-Grundlinie (das Regressionsgewicht tragen die unveränderten
bestehenden Paging-/Deleted-/Needs-Review-Tests, die jetzt alle durch `buildFilterClause`
laufen); AC4s Zehn-mal-0,10-Fall überlebte auch eine REAL-Summe, die bei der Rückwandlung
nach decimal auf 1,00 rundet, beweist die Exaktheit also weniger scharf als sein Kommentar
behauptet.

**Nicht blockierende Randbeobachtung:** `loadPayeeSuggestions` gruppiert Payees
groß-/kleinschreibungs-empfindlich (SQL `GROUP BY payee` / F# `List.countBy id`), während der
Payee-Filter `COLLATE NOCASE` vergleicht — "Rewe" und "REWE" erscheinen als zwei Vorschläge,
die beide dieselbe Treffermenge liefern.

**ITERATION_HINT:** likely-fixable

## Outcome

Der Lesepfad des Kassenbuchs filtert jetzt nach Kategorie (Mehrfachauswahl, Vereinigung),
Zeitraum (Von/Bis, beide Randtage eingeschlossen), Payee (exakt, ohne Gross-/Kleinschreibung),
Betrag (Von/Bis, exakt in F#) und Memo (Teiltext, ohne Gross-/Kleinschreibung) — als
Und-Verknuepfung, ueber alle vier Zaehlwege (`loadEntriesPage`, `countEntries`,
`loadAmountsForQuery`, `countDeletedEntries`) und die neue Payee-Vorschlagsabfrage
(`loadPayeeSuggestions`) hinweg, gebuendelt in `Ledger.Persistence.EntryFilter` und der
gemeinsamen `buildFilterClause` (`src/Server/Ledger/Persistence.fs`). `countNeedsReviewEntries`
bleibt bewusst unfiltriert (ADR 0036 §6). Der Betrag wird nie per SQL verglichen, sondern ueber
`loadAllMatching` plus `amountMatches` in F# nachgefiltert (ADR 0017); die Summe entsteht ueber
`sumMoney`, nie per SQL-`SUM`. `LedgerEntryPageQuery`/`LedgerPayeeSuggestionQuery`
(`src/Shared/Domain.fs`) tragen die fuenf Filterfelder, `Ledger.QueryHandlers.toEntryFilter`
baut daraus den gemeinsamen `EntryFilter`.

**Iteration 2** korrigiert einen vom Verifier gefundenen latenten Defekt: der
Zeitraum-Randvergleich verglich in Iteration 1 den vollen ISO-8601-Roundtrip-Text
(`date >= @dateFrom` / `<= @dateTo`, gebunden mit `DateTime.ToString("o")`) direkt in SQL.
Dieses Format ist `DateTimeKind`-abhaengig unterschiedlich lang (`Utc` haengt `Z` an, `Local`
eine Zonenverschiebung, `Unspecified` nichts); die `date`-Spalte mischt beide Formen (der
YNAB-Spiegel schreibt `Utc`, Comdirect-/Quick-Add-Zeilen kommen ohne Kind), sodass ein
Randtag je nach Kind-Kombination still aus dem Treffer fiel. Der Vergleich in
`buildFilterClause` greift jetzt nur noch den Datumsanteil ab (`substr(date, 1, 10)` gegen
`d.Date.ToString("yyyy-MM-dd")`) — bei jedem Kind identisch, weil der Kind-Suffix erst nach
den ersten zehn Zeichen beginnt. Der AC5-Test (`src/Tests/LedgerPageTests.fs:1454`) saeht jetzt
zusaetzlich zu den bestehenden Randtag-Buchungen (Kind `Unspecified`) je eine Randtag-Buchung
mit `DateTime.SpecifyKind(date, DateTimeKind.Utc)`, also genau wie der Spiegel schreibt; der
Test schlug gegen den Iteration-1-Code nachweislich fehl (`page.TotalCount`: erwartet 5,
tatsaechlich 4 — rot bestaetigt, bevor der Fix angewendet wurde, dann gruen). ADR 0036 Decision
5 und der zugehoerige Code-Kommentar sind korrigiert: sie behaupteten faelschlich, ADR 0017
Punkt 4 lege ein "festes, gleich langes, vorzeichenfreies" Zeitstempel-Format fest; tatsaechlich
sagt Punkt 4 nur "ISO-8601-Roundtrip-Text", und Roundtrip ist genau die Eigenschaft, die die
Laenge Kind-abhaengig macht.

Nicht angefasst (vom Verifier ausdruecklich freigegeben): Scope (dieselben 7 Dateien wie
Iteration 1), die gemeinsame `EntryFilter`/`buildFilterClause` ueber alle Zaehlwege,
`countNeedsReviewEntries` ungefiltert, der Betragsfilter nie in SQL, die zwei neuen Indizes im
bestehenden `CREATE INDEX IF NOT EXISTS`-Block, AC2/AC3/AC6/AC7/AC8.

Nicht behoben, als Backlog-Eintrag `ledger-h4qtx` gemeldet (Verifier-Randbeobachtung,
nicht blockierend): `loadPayeeSuggestions` gruppiert Payees gross-/kleinschreibungs-
empfindlich, waehrend der Payee-Filter `COLLATE NOCASE` vergleicht.

Tests: `dotnet build` und `dotnet test src/Tests/Tests.fsproj` beide gruen, 940 bestanden /
7 uebersprungen (.env-gated) / 0 fehlgeschlagen — unveraendert gegenueber der Baseline nach
Iteration 1, weil der AC5-Test in-place erweitert statt zusaetzlich angelegt wurde.

Schluesseldateien:
- `src/Server/Ledger/Persistence.fs` — `buildFilterClause`, insbesondere der
  Zeitraum-Vergleich (Zeilen um 621-644)
- `src/Tests/LedgerPageTests.fs` — AC5-Test (Zeile ~1454), jetzt mit gemischtem
  `DateTimeKind`
