---
id: 0038
title: Split-Zeilen-Kategorien über eine abgeleitete Indextabelle, in derselben Transaktion geführt
scope: ledger
status: accepted
date: 2026-09-20
supersedes: []
superseded_by: []
related_tasks: [ledger-xk9wq]
related_adrs: [0006, 0013, 0017, 0019, 0036]
related_research: []
---

# ADR 0038: Split-Zeilen-Kategorien über eine abgeleitete Indextabelle, in derselben Transaktion geführt

## Context
ADR 0036 (Punkt 2) hat den Kategorie-Filter des Kassenbuchs auf `category_id IN
@categoryIds` gelegt und die Lücke dabei benannt und bewusst offen gelassen: eine
aufgeteilte Buchung trägt in `ledger_entries.category_id` ein `NULL`, ihre Kategorien
stecken als JSON-Text in der Spalte `splits`, und ADR 0013 Punkt 8 verbietet
SQLite-JSON-Funktionen in Abfragen. Eine Buchung, deren einzige Kategorie in einer
Split-Zeile steht, findet der Filter deshalb nicht.

Das ist keine fehlende Zeile in einer langen Liste, sondern eine falsche Antwort. Die
Kategorie-Auswahl der Oberfläche kommt aus `getCategories` gegen YNAB
(`src/Shared/Api.fs`), nicht aus dem Bestand des Kassenbuchs: Roman kann also eine
Kategorie wählen, die es nur in Split-Zeilen gibt, und bekommt darauf eine leere Liste mit
Summe 0,00. Die Frage war gültig, die Antwort ist falsch, und nichts an der Oberfläche sagt
das.

Dazu kommen Häufigkeit und Breite. Der Kategorie-Filter ist nach Romans Schätzung der
Filter, den er in 70 bis 80 Prozent der Fälle setzt. Und er sitzt in `buildFilterClause`,
wirkt damit auf alle fünf Lesewege (`loadEntriesPage`, `countEntries`,
`loadAmountsForQuery`, `countDeletedEntries`, `loadPayeeSuggestions`). Sie irren alle
gleichsinnig: Liste, Trefferzahl und Summe passen zueinander und schließen nur dieselben
Buchungen aus. Es gibt keine sichtbare Inkonsistenz, an der der Fehler auffiele; er meldet
still zu wenig.

Zwei Wege standen zur Wahl (Task `ledger-xk9wq`): eine normalisierte Nebentabelle, oder ein
F#-seitiger Nachfilter analog zum Betragsfilter aus ADR 0036. Der zweite Weg scheitert an
der Vorgabe desselben Tasks, den häufigsten Fall nicht auf das unbedingte Laden aller rund
14.000 Buchungen zu zwingen: ohne Nebentabelle weiß SQL nicht, welche Buchung welche
Split-Kategorie trägt, man muss sie laden, um es zu erfahren. Der Nachfilter würde den
häufigsten Filter also zwangsläufig auf `loadAllMatching` umleiten. Dazu kommt, dass genau
diese Gabelung (fünf Funktionen, je zwei Implementierungen, geprüft ist im Wesentlichen nur
`TotalCount`) im Backlog bereits als Testschuld steht (`ledger-r7dm2`, Punkt 1); sie zum
Normalpfad zu machen, wäre die falsche Richtung.

## Decision

1. **Eine abgeleitete Indextabelle `ledger_entry_split_categories` (`category_id`,
   `entry_id`), nur für Split-Zeilen-Kategorien.** Sie enthält je Buchung eine Zeile pro
   verschiedener Kategorie, die in einer ihrer Split-Zeilen vorkommt. `ToTransfer`- und
   `Uncategorized`-Zeilen (ADR 0006, ADR 0019) erzeugen keine Zeile: der Filter sucht
   Kategorien, ein Übertrag ist keine. Die einfache Kategorie der Buchung bleibt allein in
   `ledger_entries.category_id` und wandert bewusst NICHT mit hinein (siehe Alternatives):
   der heute korrekte und getestete Teil des Filters behält seinen Codepfad.

2. **Die Ableitung ist eine reine Funktion.** `Ledger.Domain.splitCategoryIds : Entry ->
   Guid list` liest `entry.Splits`, wählt die `ToCategory`-Ziele aus und entdoppelt. Kein
   I/O, damit ohne Datenbank testbar und identisch für Schreibpfad und Neuaufbau.

3. **Geführt wird sie in derselben Transaktion wie Log und Zustand** (ADR 0013 Punkt 5).
   Die Schreibstelle sitzt in `Ledger.Persistence.appendAndApply`, unmittelbar hinter dem
   einen `upsertEntrySql`-Aufruf, der `ledger_entries` fortschreibt (er hat im ganzen Repo
   genau einen Aufrufer). Geschrieben wird immer `DELETE` plus `INSERT` der abgeleiteten
   Zeilen, auch wenn die Buchung keine Splits hat: sonst bliebe eine Indexzeile stehen,
   nachdem eine aufgeteilte Buchung in YNAB wieder zu einer einfachen wurde. Ein Fehler an
   irgendeiner Stelle nimmt wie bisher alles zurück.

4. **Das Verhältnis zu ADR 0013 Punkt 8 ist Erfüllung, nicht Ausnahme.** Das Verbot gilt
   Abfragen, die SQLite-eigene Funktionen benutzen, um in den JSON-Text hineinzusehen. Hier
   sieht niemand in den JSON-Text hinein: F# leitet aus dem bereits dekodierten `Entry` ab,
   die Abfrage liest eine gewöhnliche zweispaltige Tabelle mit gewöhnlichem SQL. Ein
   späterer Wechsel auf Postgres (ADR 0013 Punkt 9) bleibt damit so billig wie zuvor,
   während ein `json_extract`-Filter ihn teurer gemacht hätte.

5. **Das Verhältnis zu ADR 0013 Punkt 6 ist Zugehörigkeit, nicht Widerspruch.**
   Append-only gilt dem Ereignis-Log `ledger_entry_events`, sonst nichts. `ledger_entries`
   wird bereits per Upsert überschrieben; die Indextabelle gehört zu demselben ableitbaren
   Zustandsmodell und darf wie dieses überschrieben werden. Die Pflicht, die daraus folgt,
   ist dieselbe wie für `ledger_entries`: sie muss jederzeit vollständig aus dem Log neu
   baubar sein, und ein Test muss das belegen.

6. **Der Bestand kommt über einen vollständigen Neuaufbau, nicht über eine Wanderung.**
   `Ledger.Persistence.rebuildSplitCategoryIndex (conn: SqliteConnection) : int` leert die
   Tabelle und baut sie aus `ledger_entries` neu auf; sie läuft am Ende von
   `initializeLedgerSchema`, direkt hinter `migrateLedgerEntryColumns`, und nimmt wie diese
   die Connection als Parameter, damit ein Test sie isoliert fahren kann. Idempotent ist sie
   durch ihre Form: ein zweiter Aufruf erzeugt denselben Inhalt. Sie ist billig, weil sie
   ihre Kandidaten über das gewöhnliche SQL-Prädikat `splits <> '[]'` einschränkt (der
   Encoder schreibt für eine Buchung ohne Splits exakt diesen Text; ein Test nagelt das
   fest) und damit nur die wenigen aufgeteilten Buchungen zu Indexzeilen macht.
   Das Ereignis-Log wird dabei weder gelesen noch geschrieben. Ein Versions- oder
   Markierungs-Mechanismus, der den Neuaufbau auf genau einen Start beschränkt, wäre
   zusätzliche Infrastruktur ohne heutigen Gegenwert; er bleibt die Antwort, falls die Zahl
   der Split-Buchungen je so groß wird, dass der Neuaufbau beim Start auffällt (Grenze
   benannt unter Consequences).

7. **Die Filter-Erweiterung ist eine OR-Verknüpfung mit einer nicht-korrelierten
   Unterabfrage**, vollständig geklammert:

   ```sql
   AND (category_id IN @categoryIds
        OR id IN (SELECT entry_id FROM ledger_entry_split_categories
                  WHERE category_id IN @splitCategoryIds))
   ```

   Die Klammer ist tragend: `countDeletedEntries` und `loadPayeeSuggestions` hängen hinter
   die gebaute Klausel weitere `AND`-Glieder. Die Unterabfrage ist bewusst nicht korreliert
   (`id IN (SELECT ...)` statt `EXISTS (... WHERE sc.entry_id = ledger_entries.id)`): eine
   korrelierte Form muss je Zeile erneut ausgewertet werden und blockiert die
   OR-Optimierung, die nicht korrelierte wird einmal ausgewertet und erlaubt SQLite, beide
   Seiten des `OR` über je einen Index zu bedienen. Dieselbe Liste geht unter zwei
   Parameternamen hinein, statt sich darauf zu verlassen, dass Dapper einen doppelt
   vorkommenden Listen-Parameter im ganzen Kommandotext erweitert.

8. **Performance und Indexnutzung bei rund 14.000 Buchungen.** Die Indextabelle trägt ihren
   Primärschlüssel in der Reihenfolge `(category_id, entry_id)`. SQLite legt dafür einen
   Index an, der die Unterabfrage aus Punkt 7 vollständig abdeckt: gesucht wird nach
   `category_id`, geliefert wird `entry_id`, die Tabelle selbst wird nie angefasst. Ein
   zweiter Index auf `entry_id` bedient das `DELETE` des Schreibpfads, damit dieses ein
   Indexzugriff bleibt und kein Durchlauf der Indextabelle wird (der Spiegel schreibt pro
   Lauf tausende Ereignisse). Die Lesekosten steigen damit von einem Indexzugriff auf zwei;
   der häufigste Fall (Kategorie-Filter ohne Betragsfilter) bleibt eine gezielte Abfrage mit
   `LIMIT`/`OFFSET` beziehungsweise `COUNT`, niemand lädt alle Buchungen. Die Größe der
   Indextabelle liegt in der Größenordnung der aufgeteilten Buchungen, also Hunderte, nicht
   Zehntausende. Nachweis statt Behauptung: ein Test liest `EXPLAIN QUERY PLAN` der gebauten
   Abfrage und verlangt, dass `ledger_entry_split_categories` darin per `SEARCH` (nicht
   `SCAN`) und ohne `CORRELATED` erscheint. Geprüft wird dabei ausschließlich der
   **Zugriffspfad**, nie der SQL-Text: ein Test, der die erzeugte Klausel als Zeichenkette
   vergleicht, bricht bei jeder Umformulierung und wird dann entschärft statt repariert.
   `buildFilterClause` bleibt deshalb `private`, und die Prüf-Affordanz gibt den Plan
   zurück, nicht das SQL. Die bereits in ADR 0036 vermerkte Einschränkung bleibt unberührt:
   sobald zusätzlich ein Zeitraum gesetzt ist, verhindert `substr(date, 1, 10)` ohnehin
   einen Bereichs-Scan über `idx_ledger_entries_date`, der Kategorie-Teil ist dann nicht der
   begrenzende Faktor.

9. **Die Korrektur wirkt auf alle fünf Lesewege, weil sie in `buildFilterClause` sitzt.**
   `Entries`, `TotalCount`, `TotalAmount`, `DeletedCount` und die Payee-Vorschläge zählten
   bisher gleichsinnig zu wenig, sobald eine Split-Buchung über ihre Split-Kategorie gesucht
   wurde; sie zählen nach dieser Änderung alle richtig. `NeedsReviewCount` war und bleibt
   unberührt, weil `countNeedsReviewEntries` bewusst gar keinen `EntryFilter` nimmt (ADR
   0036 Punkt 6). Auch der Betragsfilter-Zweig ist mit erledigt: `loadAllMatching` ruft
   dieselbe `buildFilterClause` auf. Es entsteht ausdrücklich KEINE neue Gabelung neben der
   aus `hasAmountFilter` (siehe `ledger-r7dm2`, Punkt 1).

## Consequences

### Positive
- Der häufigste Filter findet ab jetzt auch aufgeteilte Buchungen, und zwar in Liste,
  Trefferzahl, Summe, Gelöscht-Zähler und Payee-Vorschlägen gleichermaßen.
- Eine Änderung an einer Stelle (`buildFilterClause`) repariert fünf Lesewege und beide
  Zweige der `hasAmountFilter`-Gabelung. Die Zahl der Implementierungen wächst nicht.
- Der Lesepfad bleibt gewöhnliches, indiziertes SQL ohne SQLite-Eigenheiten; ADR 0013
  Punkt 8 und 9 bleiben vollständig gewahrt.
- Die Indextabelle ist aus dem Log vollständig neu baubar und wird genau dadurch geprüft:
  ein Test vergleicht ihren Inhalt mit dem, was `replayAll` ergibt, in derselben Rolle wie
  der Pflicht-Replay-Test aus ADR 0013 Punkt 6.

### Negative
- Ein zweites Schreibziel im Schreibpfad. Es liegt in derselben Transaktion, kann also nicht
  halb geschrieben werden, aber es ist eine weitere Stelle, die jemand vergessen kann, wenn
  er einen zweiten Weg schafft, `ledger_entries` zu schreiben. Heute gibt es genau einen
  (`upsertEntrySql` in `appendAndApply`); der Drift-Test aus Punkt 5 ist die Absicherung
  dagegen, nicht die Disziplin.
- `initializeLedgerSchema` macht beim Start mehr als vorher, und zwar bei **jedem** Start
  statt einmalig. Die Annahme dahinter wird hier benannt statt stillschweigend getragen:
  der Neuaufbau liest einmal alle rund 14.000 Zeilen von `ledger_entries` (das Prädikat
  `splits <> '[]'` ist nicht indizierbar, es ist ein Durchlauf) und schreibt danach die
  Indexzeilen der aufgeteilten Buchungen, deren Zahl in den **Hunderten** erwartet wird. In
  diesen Größenordnungen liegt der Beitrag zur Startzeit im Millisekundenbereich und
  verschwindet neben dem ersten Spiegel-Lauf, der ohnehin über das Netz geht. Die Annahme
  kippt, wenn eine der beiden Zahlen um eine Größenordnung wächst: grob ab etwa 100.000
  Buchungen im Durchlauf oder mehr als etwa 10.000 aufgeteilten Buchungen im Schreibteil.
  Woran man es merkt, bevor es weh tut: `rebuildSplitCategoryIndex` liefert die Zahl der
  geschriebenen Indexzeilen zurück, sie ist der Frühindikator; spürbar würde es als
  Verzögerung zwischen Containerstart und der ersten Antwort der Kassenbuch-Seite. Die
  Antwort wäre dann der Versions- oder Markierungs-Mechanismus aus Decision Punkt 6, nicht
  der Verzicht auf den Index.
- Der Neuaufbau hängt daran, dass der Encoder für eine Buchung ohne Splits exakt `[]`
  schreibt. Das ist eine Abhängigkeit von einer Serialisierungs-Kleinigkeit; sie ist per
  Test festgenagelt, aber sie existiert.

### Neutral
- `idx_ledger_entries_category` bleibt bestehen und bedient weiter die linke Seite des `OR`.
- **Ausdrücklich nicht abgedeckt, damit die Grenze dokumentiert ist statt vergessen:** eine
  Buchung, deren Split-Zeilen alle `Uncategorized` oder `ToTransfer` sind, ist fachlich
  unkategorisiert und fällt durch jeden Kategorie-Filter, weil es keine Kategorie gibt, nach
  der man sie suchen könnte. Die Indextabelle hilft dort nicht, sie bliebe für solche
  Buchungen leer. Der Weg dahin wäre ein eigener Filter "ohne Kategorie" in der
  Filterleiste, vermerkt an `ledger-j8xq4`, und ein abgeleitetes Kennzeichen wäre dafür die
  billigere Form als ein weiterer Index.
- Eine Buchung mit zwei Split-Zeilen in derselben Kategorie erzeugt eine Indexzeile, nicht
  zwei (`List.distinct` in `splitCategoryIds`, zusätzlich durch den Primärschlüssel
  erzwungen). Für die Treffermenge ist das folgenlos; es hält die Tabelle klein und macht
  eine spätere Verwendung per `JOIN` ungefährlich.
- Die Oberfläche braucht keine Änderung: die Kategorie-Auswahl kommt ohnehin aus YNAB
  (`getCategories`) und bot die betroffenen Kategorien schon immer an, nur ohne Ergebnis.
- ADR 0036 wird nicht ersetzt, sondern ergänzt (dieselbe Form, in der ADR 0019 ADR 0006
  ergänzt): Punkt 2 dort beschreibt weiterhin korrekt, was `ledger-vgfw3` gebaut hat.

## Alternatives considered

- **F#-seitiger Nachfilter über `loadAllMatching`, analog zum Betragsfilter (ADR 0036
  Punkt 3):** verworfen aus zwei unabhängigen Gründen. Erstens verletzt er die Vorgabe des
  Tasks, den häufigsten Fall nicht auf das unbedingte Laden aller rund 14.000 Buchungen zu
  zwingen; anders als beim Betrag, der selten gesetzt wird, ist Kategorie der
  70-bis-80-Prozent-Fall. Zweitens macht er die `hasAmountFilter`-Gabelung, die laut
  `ledger-r7dm2` Punkt 1 fünf Funktionen doppelt implementiert und davon im Wesentlichen
  eine prüft, zum Normalpfad. Der Betragsfilter rechtfertigt seine Sonderbehandlung durch
  ADR 0017 (exakter Dezimal-Text, kein SQL-Vergleich möglich); für Kategorien gibt es keinen
  solchen Grund, nur ein Schema, das man ändern kann.
- **Hybrid: nur die aufgeteilten Buchungen laden (`WHERE splits <> '[]'`), ihre Ids in F#
  bestimmen und als `OR id IN @ids` in die Abfrage geben:** verworfen. Kommt ohne
  Schemaänderung und ohne zweite Schreibstelle aus, verlagert die Kosten aber von einem
  Schreibvorgang in jede einzelne Abfrage, und zwar in alle fünf Lesefunktionen; die Kosten
  wachsen mit der Zahl der Split-Buchungen und damit über die Jahre unbegrenzt, und die
  Id-Liste wandert als Parameterliste in das SQL, wo SQLite eine Obergrenze kennt. Eine
  zwischengespeicherte Fassung davon wäre dieselbe Indextabelle, nur ohne Transaktion.
- **Eine Indextabelle für alle Kategorien** (einfache und Split-Kategorien in einer Tabelle,
  der Filter wird ein einziges `id IN (SELECT ...)` ohne `OR`): die schönere Klausel und der
  bessere Ausführungsplan, verworfen wegen des Risikos. Sie stellt den heute korrekten,
  getesteten und häufig benutzten einfachen Kategorie-Filter auf eine frisch gebaute Tabelle
  um; ein Fehler dort verliert Buchungen, die heute gefunden werden. Mit der
  split-only-Tabelle kann ein Fehler im neuen Code nur bewirken, dass eine Split-Buchung
  weiterhin nicht gefunden wird, also der Status quo. Neu zu bewerten, falls der `OR`-Plan
  je zum Problem wird.
- **`json_extract` auf der `splits`-Spalte:** verworfen, ADR 0013 Punkt 8, ohne weitere
  Diskussion.
