---
id: 0051
title: Kassenbuch-Sortierung gehört zur Seitenanfrage und nicht zum EntryFilter, Betrag und Kategorie sortieren in F#
scope: ledger
status: accepted
date: 2026-10-20
supersedes: []
superseded_by: []
related_tasks: [ledger-bn913, ledger-r72re, ledger-skpcx]
related_adrs: [0017, 0036, 0037, 0038, 0039, 0045, 0048, 0050]
related_research: []
---

# ADR 0051: Kassenbuch-Sortierung gehört zur Seitenanfrage und nicht zum EntryFilter

## Context
Das Kassenbuch ist fest nach `date DESC, id DESC` sortiert. Roman will nach Datum, Betrag,
Payee, Kategorie und (in der Gesamtsicht) Konto sortieren, serverseitig, weil die Liste
seitenweise lädt (`ledger-b1xwe`). Die Entscheidungen über Felder, Bedienung und Tagesköpfe
stehen in `ledger-bn913`. Dieser ADR hält fest, was daran den Lesepfad und die
Auswahlauflösung bindet.

Ausgangslage im Code:

- `LedgerEntryPageQuery` trägt Filter und Seite. Der Filter geht über `EntryFilter` in
  `buildFilterClause` und damit in alle Zählwege (ADR 0036). Dieselbe Anfrage reist als
  `BatchAllExcept` im Stapel-Befehl "Geprüft für die Auswahl" (ADR 0050) und löst dort den
  Treffer auf, den der Client nie geladen hat.
- Der Betrag liegt als Dezimal-Text (ADR 0017). ADR 0017 hält ausdrücklich fest, dass ein
  Sortieren nach Betrag "in F# sortiert" wird, sobald der Fall auftritt. `CAST(amount AS
  REAL)` bleibt ausgeschlossen.
- Der angezeigte Kategorie-Name entsteht erst im Lesepfad: `ReadModel.toEntryView` ersetzt den
  gespeicherten Namen durch den aktuellen aus `ledger_categories` (`liveName`, ADR 0044)
  und die PC-Tabelle stellt die Gruppe voran (ADR 0039, `categoryLabelQualified`). Die Spalte
  `ledger_entries.category_name` kann nach einer Umbenennung veraltet sein.
- Der Container läuft im Globalization-Invariant-Modus (ADR 0037, Context 4). Kulturabhängiges
  Vergleichen steht dort nicht zur Verfügung.

## Decision
1. **Vertrag.** In `Shared.Domain` entstehen `LedgerSortField` (`SortByDate | SortByAmount |
   SortByPayee | SortByCategory | SortByAccount`), `LedgerSortDirection` (`SortAsc |
   SortDesc`) und `LedgerSort = { Field; Direction }` samt `LedgerSort.defaultSort`
   (Datum, absteigend). `LedgerEntryPageQuery` bekommt das Feld `Sort: LedgerSort`. Die
   Namen sind mit Absicht von `Table.SortDirection` (Design-System, `Ascending |
   Descending | Unsorted`) verschieden, damit ein `open` beider Module nichts verdeckt. Die
   Union ist geschlossen: Fable.Remoting lehnt einen unbekannten Fall beim Deserialisieren ab,
   eine eigene Validierungsfunktion kommt nicht dazu.
2. **Die Sortierung ist kein Teil von `EntryFilter`.** Sie wirkt nur auf `loadEntriesPage`
   (neuer Parameter `sort: LedgerSort`). `countEntries`, `loadAmountsForQuery`,
   `countDeletedEntries`, `countNeedsReviewEntries` und `loadPayeeSuggestions` nehmen sie
   nicht an: Trefferzahl, Summe und Vorschläge sind damit von der Reihenfolge unabhängig,
   ohne dass jemand daran denken muss. `entryFilterOfPageQuery` übernimmt `Sort` nicht.
   `LedgerPayeeSuggestionQuery` bekommt kein `Sort`.
3. **Auswahlauflösung ist sortierungsfrei.** `BatchAllExcept` trägt weiterhin eine
   `LedgerEntryPageQuery` (und damit ein `Sort`), aber `resolveReviewMarkBatchSelection`
   ruft `loadEntriesPage` mit `LedgerSort.defaultSort` auf und liest das `Sort` der Anfrage
   nicht. Die aufgelöste Menge ist eine Menge, ihre Reihenfolge trägt keine Bedeutung. Das
   hält auch den teuren F#-Pfad (Punkt 4) aus dem Stapel-Befehl heraus.
4. **Zwei Pfade.** Datum, Payee und Konto sortieren in SQL (`ORDER BY` in `loadEntriesPage`
   mit `LIMIT/OFFSET`). Betrag und Kategorie sortieren in F#: `loadAllMatching` lädt die
   durch die übrigen Kriterien eingeschränkte Menge (wie beim Betragsfilter, ADR 0036
   Punkt 3), sortiert mit einem Vergleich, der den Schlüssel aus Punkt 5 und den Tiebreaker
   aus Punkt 6 kennt, und `safePage` schneidet die Seite. Gründe: der Betrag ist Text
   (ADR 0017), und der Kategorie-Name entsteht erst im Lesepfad aus `ledger_categories`;
   eine SQL-Sortierung nach `category_name` würde eine umbenannte Kategorie an der alten
   Stelle zeigen. Wer den Pfad wählt, entscheidet eine Funktion (`sort.Field` ist `SortByAmount`
   oder `SortByCategory`, oder `hasAmountFilter`), nicht der Aufrufer.
5. **Schlüssel und leere Werte.**
   - Datum: die Spalte `date` wie bisher (kein `substr`), `id` in derselben Richtung.
   - Betrag: der vorzeichenbehaftete `decimal`. Aufsteigend steht der kleinste Wert, also die
     größte Ausgabe, zuerst. Währung geht nicht in den Vergleich ein (das Kassenbuch summiert
     ohnehin nur EUR).
   - Payee: gefalteter Text (`bb_lower` in SQL, ADR 0037), binärer Vergleich. Leer ist
     `payee IS NULL OR trim(payee) = ''`.
   - Konto: gefalteter Kontoname aus `ledger_accounts` (korrelierte Teilabfrage im
     `ORDER BY`, kein `JOIN`, weil `buildFilterClause` mit unqualifizierten Spaltennamen
     arbeitet und `id` sonst mehrdeutig wäre). Ein Konto ist nie leer.
   - Kategorie: vier feste Ränge in beiden Richtungen: (0) Buchung mit eigener Kategorie,
     (1) Split-Buchung, (2) Übertrag, (3) ohne Kategorie. Die Zuordnung folgt der Rangfolge
     von `categoryLabel` (eigene Kategorie vor Split vor Übertrag vor "ohne Kategorie"), damit
     die Sortierung zeigt, was die Zelle zeigt. Innerhalb von Rang 0 entscheidet der
     gefaltete, voll qualifizierte Name (`Gruppe: Name`, ohne Gruppe nur der Name, wie
     `categoryLabelQualified`) in der gewählten Richtung. Die Ränge 1 bis 3 haben keinen
     eigenen Schlüssel: Split-Zeilen-Kategorien (ADR 0038, 0045) gehen nicht in die
     Sortierung ein, ein Übertrag gilt als Ziel (ADR 0048), nicht als fehlende Kategorie.
   - **Leere Werte stehen in beiden Richtungen am Ende.** Bei Payee sind das Buchungen ohne
     Payee, bei Kategorie die Ränge 1 bis 3. Begründung: wer absteigend sortiert, will die
     Z-Werte sehen und keine Blöcke fehlender Angaben vorweg. Wer die offenen Fälle sucht,
     hat dafür den Chip "Ohne Kategorie".
   - Strings vergleichen ordinal auf dem gefalteten Text. Umlaute stehen damit hinter `z`
     (`Ärztehaus` hinter `Zalando`). Der Container läuft invariant (ADR 0037), eine
     Transliteration wäre eine eigene Entscheidung (ADR 0037 Punkt 5). Die Grenze gilt für
     SQL- und F#-Pfad gleich.
6. **Tiebreaker.** Datum: `id` in der Richtung des Datums (aufsteigend `date ASC, id ASC`).
   Alle anderen Felder: bei gleichem Schlüssel `date DESC, id DESC`, unabhängig von der
   gewählten Richtung. Damit ist jede Sortierung eine Totalordnung, und innerhalb eines
   Payees oder einer Kategorie steht die neueste Buchung oben.
7. **`LocallyRemoved` bleibt unverändert** (`Page * PageSize - LocallyRemoved`). Die Formel
   trägt für jede Totalordnung, solange die lokal entfernten Zeilen die Treffermenge
   verlassen, ohne die Ordnung der übrigen zu ändern. Das gilt für alles, was es heute gibt:
   der Prüfvermerk ändert nur `approved` und `cleared`, keinen Sortierschlüssel. **Grenze:**
   Ein künftiger Schreibweg, der den Schlüssel der aktiven Sortierung ändert (Kategorie
   ändern bei Kategorie-Sortierung, `ledger-q1hw2`), verschiebt die Zeile im Server-Resultat,
   ohne dass der Client es weiß, und das Nachladen verliert oder verdoppelt dann Zeilen an der
   Seitengrenze. Der Client hält die geänderte Zeile an ihrem Platz ("Kategoriewechsel
   sortiert nie um"), darf danach aber nicht mit dem alten Versatz weiterladen. Dieser Weg
   muss beim Bau bestimmen, wie die Liste danach neu aufsetzt (Neuladen der aktiven
   Sortierung ab Seite 1 mit sichtbarem Hinweis). Bis dahin ist die Grenze in `ledger-q1hw2`
   zu übernehmen.
8. **Kein neuer Index.** Datum trägt `idx_ledger_entries_date`. Payee, Konto und Kategorie
   sortieren über Ausdrücke (`bb_lower`, Teilabfrage, F#-Schlüssel), die kein Index trägt, und
   der Payee-Filter läuft aus demselben Grund schon heute ohne Index (ADR 0037, Context 5).
   Statt eines Index auf Verdacht gilt eine Messung bei 14.000 Buchungen (Schwelle und Ablauf in
   `ledger-r72re`). Überschreitet ein Feld die Schwelle, ist die Entscheidung neu zu treffen,
   nicht mit einem Index zu raten.
9. **Client.** Die Sortierung lebt im Modell als `Sort: LedgerSort`. Die Startrichtung beim
   Wechsel auf ein anderes Feld ist: Datum absteigend, Betrag aufsteigend (größte Ausgabe
   zuerst), Payee, Kategorie und Konto aufsteigend; ein Klick auf das aktive Feld dreht die
   Richtung. Die Anfrage trägt die wirksame Sortierung (`effectiveSort`): ist `SortByAccount`
   gewählt und ein einzelnes Konto aktiv, gilt `LedgerSort.defaultSort`, das Modell behält die
   Wahl für die Rückkehr in die Gesamtsicht.

## Consequences
### Positive
- Kein Zählweg kann von der Sortierung abhängen, weil sie nicht in `EntryFilter` steht.
- Die Auswahlauflösung für "Geprüft" bleibt billig und reihenfolgefrei.
- Betrag und Kategorie bleiben ohne Schemaänderung und ohne Gleitkomma sortierbar (ADR 0017).
- Eine Umbenennung einer Kategorie (ADR 0044) wirkt sofort auf die Sortierung.

### Negative
- Betrag und Kategorie laden je Seitenanfrage die ganze Treffermenge (rund 14.000 Zeilen) und
  sortieren sie, auch beim Nachladen jeder weiteren Seite. Die Messung in `ledger-r72re`
  begrenzt das.
- Umlaute sortieren hinter `z`.
- Die Grenze aus Punkt 7 verschiebt eine Pflicht auf `ledger-q1hw2`.

### Neutral
- Der Wire-Vertrag wächst um ein Pflichtfeld. Alle Stellen, die eine `LedgerEntryPageQuery`
  als Record-Literal bauen (Client `buildPageQuery`, Testdateien), setzen `Sort`.

## Alternatives considered
- **`Sort` in `EntryFilter`.** Verworfen: jeder Zählweg und die Payee-Vorschläge hingen dann
  an einem Feld, das sie nicht brauchen, und die Auswahlauflösung zöge die Sortierung mit.
- **`CAST(amount AS REAL)` im `ORDER BY`.** Verworfen: ADR 0017 schließt die Umwandlung
  aus, auch wenn sie bei Centbeträgen die Reihenfolge nicht verfälschen würde.
- **Sortierschlüssel als Spalte (`amount_sort`, `category_label`) pflegen.** Verworfen:
  Migration über 14.000 Zeilen und ein zweiter Ort, der bei Kategorie-Umbenennung nachgezogen
  werden müsste (ADR 0044). Bleibt die Rückfallebene, falls die Messung eine Grenze reißt.
- **Kategorie nach `category_name` in SQL.** Verworfen: veralteter Name nach Umbenennung, und
  die Gruppe fehlt in der Spalte.
- **Leere Werte bei absteigender Sortierung vorn.** Verworfen, siehe Punkt 5.
