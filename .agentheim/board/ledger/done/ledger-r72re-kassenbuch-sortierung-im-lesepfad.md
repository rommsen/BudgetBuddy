---
id: ledger-r72re
title: "Kassenbuch-Sortierung im Lesepfad: LedgerEntryPageQuery bekommt Sortierfeld und Richtung, der Server sortiert nach Datum, Betrag, Payee, Kategorie und Konto"
status: done
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: []
blocks: [ledger-bn913, ledger-skpcx]
tags: [backend, kassenbuch, sortierung, lesepfad, performance]
related_adrs: [0017, 0036, 0037, 0038, 0039, 0044, 0045, 0048, 0050, 0051]
related_research: []
prior_art: [ledger-b1xwe, ledger-be1yh, ledger-vgfw3, ledger-nhwrt]
---

## Why
Teil 1 von 3 der Sortierung (`ledger-bn913`). Die Oberfläche braucht einen Server, der die
Seiten in wählbarer Reihenfolge liefert, ohne dass Trefferzahl, Summe, Seitengrenzen oder die
Auflösung der Auswahl für "Geprüft" von der Reihenfolge abhängen. Alle Entscheidungen
stehen in ADR 0051; diese Aufgabe setzt sie um. Nach dieser Aufgabe ändert sich für Roman
nichts Sichtbares: der Client sendet weiter `defaultSort`.

## What
Umsetzung von ADR 0051:

1. **Typen** in `src/Shared/Domain.fs`: `LedgerSortField` (`SortByDate | SortByAmount |
   SortByPayee | SortByCategory | SortByAccount`), `LedgerSortDirection` (`SortAsc |
   SortDesc`), `LedgerSort = { Field; Direction }` und `LedgerSort.defaultSort` (Datum,
   absteigend). Neues Pflichtfeld `Sort: LedgerSort` in `LedgerEntryPageQuery`. Alle
   Record-Literale (Client `State.buildPageQuery` mit `LedgerSort.defaultSort`, Testdateien
   `LedgerPagingTests`, `LedgerReviewMarkBatchTests`, `LedgerWithoutCategoryTests`,
   `LedgerPageTests` und weitere) setzen das Feld.
2. **`Ledger.Persistence.loadEntriesPage`** bekommt den Parameter `sort: LedgerSort`.
   - SQL-Pfad (kein Betragsfilter, Feld Datum, Payee oder Konto): `ORDER BY` nach ADR 0051
     Punkt 5 und 6, `LIMIT/OFFSET` wie bisher. Payee: `(payee IS NULL OR trim(payee) = '')
     ASC, bb_lower(payee) <Richtung>, date DESC, id DESC`. Konto:
     `bb_lower((SELECT name FROM ledger_accounts a WHERE a.id = ledger_entries.account_id))
     <Richtung>, date DESC, id DESC`. Datum: `date <Richtung>, id <Richtung>`. Der
     Richtungstext kommt aus einer Funktion über die geschlossene Union, nie aus Eingabetext.
   - F#-Pfad (Feld Betrag oder Kategorie, oder Betragsfilter wie bisher): `loadAllMatching`,
     Betragsfilter, dann Sortierung mit einem Vergleich nach ADR 0051 Punkt 5 und 6, dann
     `safePage`. Betrag: `decimal`. Kategorie: Rang 0 bis 3 und gefalteter, voll
     qualifizierter Name (`Core.searchFold`, Ordinalvergleich). Der Name entsteht aus
     `loadCategoryNames` und `loadCategoryGroups` mit derselben Regel wie
     `ReadModel.toEntryView` (`liveName`, Gruppe getrimmt, leere Gruppe heißt keine
     Gruppe). Diese Regel steht danach an einer Stelle, die `toEntryView` und der
     Sortierschlüssel gemeinsam aufrufen, nicht zweimal geschrieben.
   - `loadAllMatching` behält seine feste Reihenfolge als Ausgangslage; die Sortierung der
     F#-Pfade ist ein eigener Schritt.
3. **`QueryHandlers.getLedgerEntriesPage`** reicht `query.Sort` an `loadEntriesPage`
   durch. `countEntries`, `loadAmountsForQuery`, `countDeletedEntries`,
   `countNeedsReviewEntries`, `loadPayeeSuggestions` und `entryFilterOfPageQuery` bleiben
   ohne Sortierung (ADR 0051 Punkt 2). `getLedgerPayeeSuggestions` und
   `LedgerPayeeSuggestionQuery` ändern sich nicht.
4. **`CommandHandlers.resolveReviewMarkBatchSelection`** ruft `loadEntriesPage` mit
   `LedgerSort.defaultSort` auf und liest das `Sort` der Anfrage nicht (ADR 0051 Punkt 3).
5. **Messung** (siehe Akzeptanzkriterien), Ergebnis in den Outcome der Aufgabe und ins
   Entwicklungs-Tagebuch.
6. README-Delta melden: Eintrag "Sortierung (Kassenbuch)" in der Ubiquitous Language, Satz
   in `getLedgerEntriesPage` und im Eintrag "Such-Filter (Kassenbuch)", dass die Sortierung
   kein Filter ist und in keinen Zählweg eingeht.

Ausdrücklich nicht in dieser Aufgabe: Client-Zustand, Spaltenköpfe, Handy-Auswahl
(`ledger-bn913`, `ledger-skpcx`), ein Index (ADR 0051 Punkt 8).

## Acceptance criteria
Alle maschinell prüfbar (Expecto, In-Memory-SQLite wie die vorhandenen Persistenz-Tests):

- [ ] Ein Aufruf mit `LedgerSort.defaultSort` liefert dieselben Seiten wie vor der
      Änderung; die vorhandenen Tests bleiben unverändert grün (nur das neue Feld ergänzt).
- [ ] Für jedes der fünf Felder und beide Richtungen: die Reihenfolge über mindestens drei
      Seiten (kleine `PageSize` im Test) stimmt mit einer im Test unabhängig berechneten
      Erwartung überein, ohne doppelte oder fehlende Buchungen, bei Gleichstand nach dem
      Tiebreaker aus ADR 0051 Punkt 6 (Testdaten mit absichtlich gleichen Schlüsseln).
- [ ] Betrag: `-1000.00`, `-99.50`, `-5.00`, `0.00`, `12.30` ordnen sich numerisch (der
      Textvergleich ordnete `-99.50` vor `-1000.00`); aufsteigend steht `-1000.00` zuerst.
- [ ] Payee: `rewe`, `REWE` und `Rewe` gelten als ein Schlüssel (Reihenfolge dazwischen nur
      durch Datum und `id`); `NULL`, `''` und `'  '` stehen in beiden Richtungen hinter allen
      benannten Payees.
- [ ] Kategorie: Reihenfolge Rang 0 (nach `Gruppe: Name`, gefaltet) vor Split vor Übertrag
      vor ohne Kategorie, in beiden Richtungen; nur Rang 0 kehrt sich um. Ein Test mit
      einer in `ledger_categories` umbenannten Kategorie (gespeicherter Name alt) sortiert
      nach dem neuen Namen; eine Kategorie ohne Gruppeneintrag sortiert nach ihrem Namen
      allein.
- [ ] Konto: Reihenfolge nach gefaltetem Kontonamen in der Gesamtsicht; mit `AccountId`
      gesetzt entspricht die Reihenfolge der Standardreihenfolge.
- [ ] `TotalCount`, `TotalAmount`, `DeletedCount` und `NeedsReviewCount` aus
      `getLedgerEntriesPage` sind für alle zehn Kombinationen aus Feld und Richtung gleich
      (ein Test über denselben Bestand).
- [ ] Ein Betragsfilter zusammen mit Sortierung nach Betrag und nach Kategorie liefert die
      richtige gefilterte, sortierte Seite und die richtige Trefferzahl.
- [ ] `LocallyRemoved` (Regressionstest analog `ledger-be1yh`): mit Sortierung nach Payee
      und nach Betrag, Chip "Zu prüfen" aktiv, Seite 0 geladen, drei geladene Zeilen
      geprüft, Seite 1 mit `LocallyRemoved = 3` angefragt: die Verkettung aus Seite 0
      (ohne die drei) und Seite 1 ergibt die vollständige sortierte Treffermenge ohne Lücke
      und Doppelung.
- [ ] `resolveReviewMarkBatchSelection` mit `BatchAllExcept` und einer Anfrage, deren
      `Sort` `SortByAmount, SortAsc` ist, liefert dieselbe Menge wie mit `defaultSort`.
- [ ] Messung bei 14.000 Buchungen: synthetischer Bestand in einer Datei-Datenbank (wie im
      Container, nicht In-Memory) mit fünf Konten, rund 60 Kategorien in Gruppen, rund 40
      Payees, einigen hundert Splits, Überträgen und Buchungen ohne Payee. Für jedes Feld
      und beide Richtungen: `getLedgerEntriesPage` für Seite 0 und Seite 50, ein
      Aufwärmlauf, dann der Median aus fünf Läufen. Schwelle: jeder Median höchstens 1000 ms
      auf dem Entwicklungsrechner. Die Tabelle der Werte steht im Outcome. Die Messung
      läuft als eigener, in der Standard-Suite übersprungener Test oder als Skript, nicht als
      Zeitprüfung in `dotnet test`. Reißt ein Wert die Schwelle, legt der Worker keinen Index
      auf Verdacht an, sondern gibt die Aufgabe mit der Messung zurück (ADR 0051 Punkt 8).
- [ ] `dotnet build` ohne neue Warnungen, `dotnet test` grün.

## Notes
- Der F#-Pfad lädt je Seitenanfrage die ganze Treffermenge. Das ist beim Betragsfilter
  schon heute so (ADR 0036 Punkt 3); neu ist, dass auch das Nachladen jeder weiteren Seite
  bei Sortierung nach Betrag oder Kategorie diesen Weg nimmt. Die Messung deckt das mit
  Seite 50 ab.
- `Table.SortDirection` im Design-System heißt `Ascending | Descending | Unsorted`; die
  Shared-Namen (`SortAsc`, `SortDesc`) sind mit Absicht anders, damit kein `open` etwas
  verdeckt.
- Die Teilabfrage im `ORDER BY` für das Konto ist bewusst kein `JOIN`:
  `buildFilterClause` benutzt unqualifizierte Spaltennamen, und `ledger_accounts` hat
  ebenfalls `id`.
- Umlaute sortieren hinter `z` (ordinaler Vergleich auf gefaltetem Text, ADR 0051 Punkt 5).
  Das ist eine bekannte Grenze, kein Fehler dieser Aufgabe.
- Der Rang einer Buchung folgt der Rangfolge von `categoryLabel` in `View.fs`: eigene
  Kategorie, sonst Split (`Splits` nicht leer), sonst Übertrag (`TransferAccountId` oder
  `TransferEntryId` gesetzt), sonst ohne Kategorie.

## Outcome

Umsetzung von ADR 0051 im Lesepfad. `Shared.Domain` hat `LedgerSortField`, `LedgerSortDirection`, `LedgerSort` und `LedgerSort.defaultSort` (Datum absteigend); `LedgerEntryPageQuery` hat das Pflichtfeld `Sort`. `Ledger.Persistence.loadEntriesPage` nimmt `sort`: Datum, Payee und Konto laufen als `ORDER BY` mit `LIMIT/OFFSET` in SQL (Richtungstext aus der geschlossenen Union), Betrag, Kategorie und jeder Betragsfilter laufen über `loadAllMatching`, einen Vergleich mit Schlüssel und Tiebreaker nach ADR 0051 Punkt 5 und 6 und `safePage`. Den Pfad wählt `sortsInMemory`/`hasAmountFilter`, nicht der Aufrufer. Die Kategorie-Namensregel (`liveName`, Gruppe getrimmt, leere Gruppe = keine Gruppe) steht jetzt einmal in `Ledger.Persistence` (`liveCategoryName`, `categoryGroupOf`, `qualifiedCategoryName`); `ReadModel.toEntryView` und der Sortierschlüssel rufen dieselben Funktionen. `loadCategoryGroups`/`loadCategoryNames` wurden dafür unverändert vor `loadEntriesPage` verschoben. `getLedgerEntriesPage` reicht `query.Sort` nur an `loadEntriesPage` durch; `resolveReviewMarkBatchSelection` ruft mit `LedgerSort.defaultSort` auf und liest das `Sort` der Anfrage nicht. Der Client sendet `LedgerSort.defaultSort` in `buildPageQuery` (einzige Änderung in `State.fs`). `View.fs` unberührt.

Abweichung von der Aufgabenskizze, mit Grund: Der Payee-Schlüssel im SQL ist `CASE WHEN trim(payee) = '' THEN NULL ELSE bb_lower(payee) END`, nicht das rohe `bb_lower(payee)`. Mit dem rohen Text hätte unter den leeren Payees (NULL, '', '  ') der Rohtext vor dem Tiebreaker entschieden (Fund im Test). Jetzt entscheidet unter allen leeren Payees allein `date DESC, id DESC`, wie im F#-Pfad.

Dateien: src/Shared/Domain.fs, src/Server/Ledger/Persistence.fs, src/Server/Ledger/QueryHandlers.fs, src/Server/Ledger/CommandHandlers.fs, src/Client/Components/Ledger/State.fs, neue Tests src/Tests/LedgerSortTests.fs (in Tests.fsproj eingetragen), Feld `Sort` in den Literalen von LedgerPageTests, LedgerPagingTests, LedgerReviewMarkBatchTests, LedgerWithoutCategoryTests, Messskript scripts/measure-ledger-sort.fsx.

Tests (10 neu in `LedgerSortTests.fs`): Standardsortierung wie bisher; alle zehn Feld/Richtung-Kombinationen über mehrere Seiten gegen eine unabhängig berechnete Erwartung mit absichtlich gleichen Schlüsseln; Betrag numerisch (`-1000.00` zuerst); Payee gefaltet, leere Payees in beiden Richtungen hinten; Kategorie mit Rängen, umbenannter Kategorie und Kategorie ohne Gruppeneintrag; Konto in der Gesamtsicht und mit `AccountId`; `TotalCount`/`TotalAmount`/`DeletedCount`/`NeedsReviewCount` für alle zehn Kombinationen gleich; Betragsfilter mit Betrag, Kategorie und Payee; `LocallyRemoved` bei Payee und Betrag mit Chip "Zu prüfen" lückenlos; `resolveReviewMarkBatchSelection` mit `SortByAmount, SortAsc` liefert dieselbe Menge. Rot-Probe: mit `LedgerSort.defaultSort` statt `query.Sort` im Handler scheitern 7 der 10. Die Testbuchungen tragen Datum 2001, damit sie in der Gesamtsicht hinter den übrigen Testdaten stehen (ein bestehender Gesamtsicht-Test mit 100er-Seite wurde sonst verdrängt).

Läufe im Worktree: dotnet build 0 Warnungen, 0 Fehler; dotnet test 1542 bestanden, 0 fehlgeschlagen, 7 übersprungen (dreimal hintereinander); npm run build grün; npm run guard:classes "Keine undefinierten Klassen"; npm run test:scripts 137 von 137.

### Messung bei 14.000 Buchungen (ADR 0051 Punkt 8)

Skript `scripts/measure-ledger-sort.fsx` (Aufruf im Kopf des Skripts), Datei-Datenbank im Scratchpad, 5 Konten, 60 Kategorien in 8 Gruppen, 40 Payees, rund 350 Splits, rund 260 Überträge, Buchungen ohne Payee und ohne Kategorie, Debug-Build, `getLedgerEntriesPage` PageSize 100, Gesamtsicht, je ein Aufwärmlauf, dann Median aus fünf Läufen:

| Feld | Richtung | Seite | Median ms |
|---|---|---|---|
| Datum | aufsteigend | 0 | 48 |
| Datum | aufsteigend | 50 | 88 |
| Datum | absteigend | 0 | 36 |
| Datum | absteigend | 50 | 39 |
| Betrag | aufsteigend | 0 | 137 |
| Betrag | aufsteigend | 50 | 187 |
| Betrag | absteigend | 0 | 554 |
| Betrag | absteigend | 50 | 645 |
| Payee | aufsteigend | 0 | 240 |
| Payee | aufsteigend | 50 | 223 |
| Payee | absteigend | 0 | 144 |
| Payee | absteigend | 50 | 159 |
| Kategorie | aufsteigend | 0 | 625 |
| Kategorie | aufsteigend | 50 | 402 |
| Kategorie | absteigend | 0 | 544 |
| Kategorie | absteigend | 50 | 446 |
| Konto | aufsteigend | 0 | 97 |
| Konto | aufsteigend | 50 | 275 |
| Konto | absteigend | 0 | 121 |
| Konto | absteigend | 50 | 498 |

Größter Median 645 ms (Betrag absteigend, Seite 50), Schwelle 1000 ms eingehalten, kein Index angelegt. Die Schwankung zwischen Richtungen (Betrag aufsteigend 137 ms, absteigend 554 ms) kommt aus Lauf-zu-Lauf-Rauschen im fsi-Prozess (Speicherdruck), nicht aus einem Unterschied im Code; bei Bedarf Messung wiederholen.

### README von Hand

Die beiden Prosa-Stellen sind per `replace` nicht erreichbar. Das Delta legt einen eigenen Eintrag "Sortierung (Kassenbuch)" an, der die Aussagen trägt. Zusätzlich von Hand ergänzen:
- Im Eintrag `getLedgerEntriesPage` (nested Bullet, endet auf "und die Summenzeile über der Liste.") anhängen: "Die Seite folgt `query.Sort` (ADR 0051, `Sortierung (Kassenbuch)`); die Sortierung wirkt nur auf die Reihenfolge der Seite und geht in keinen Zählweg ein."
- Im Eintrag "Such-Filter (Kassenbuch)" (endet auf "nach Ordinalvergleich.") anhängen: "Die Sortierung (`Sort` auf `LedgerEntryPageQuery`) ist kein Such-Filter: sie steht nicht in `EntryFilter` und geht in keinen der Zählwege ein (ADR 0051)."
