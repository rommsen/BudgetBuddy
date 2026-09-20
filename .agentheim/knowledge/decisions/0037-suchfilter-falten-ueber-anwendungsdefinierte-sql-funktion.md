---
id: 0037
title: Groß-/Kleinschreibung der Kassenbuch-Suchfilter über eine anwendungsdefinierte SQL-Funktion falten, ohne Normalform-Spalte
scope: ledger
status: accepted
date: 2026-09-20
supersedes: []
superseded_by: []
related_tasks: [ledger-k3wqn]
related_adrs: [0008, 0013, 0030, 0036]
related_research: []
---

# ADR 0037: Groß-/Kleinschreibung der Kassenbuch-Suchfilter über eine anwendungsdefinierte SQL-Funktion falten, ohne Normalform-Spalte

## Context
Der Payee- und der Memo-Filter des Kassenbuchs (ADR 0036, `Ledger.Persistence.buildFilterClause`)
vergleichen über `COLLATE NOCASE`. SQLites NOCASE faltet ausschließlich ASCII A-Z: `müller`
findet `Müller GmbH`, aber nicht `MÜLLER GMBH`. Comdirect und YNAB liefern einen großen Teil
der Payees und Verwendungszwecke in Großbuchstaben, der Filter versagt also gerade im
Normalfall, und zwar lautlos: das Ergebnis sieht aus wie "es gibt nichts". Das README
verspricht seit `ledger-vgfw3` ausdrücklich "ohne Beachtung der Groß-/Kleinschreibung".
Roman hat am 2026-09-20 entschieden, den Fehler zu beheben statt die Einschränkung
festzuschreiben.

Gemessen wurde gegen die tatsächlich gebundene Fassung (Microsoft.Data.Sqlite 9.0.13,
transitiv `SQLitePCLRaw.bundle_e_sqlite3` 2.1.10, SQLite 3.46.1), nicht angenommen:

1. **Kein ICU.** `PRAGMA compile_options` enthält kein `ENABLE_ICU`, `icu_load_collation`
   existiert nicht. Das Laufzeit-Image (`mcr.microsoft.com/dotnet/aspnet:9.0-alpine`, das
   Dockerfile installiert nur `curl`) müsste dafür ein eigenes natives SQLite tragen.
2. **SQLites `lower()`/`upper()` sind ASCII-only.** `lower('MÜLLER GMBH')` ergibt
   `'mÜller gmbh'`. Eine Normalform-Spalte ließe sich damit nicht per SQL befüllen, und eine
   `GENERATED`-Spalte scheidet aus demselben Grund aus.
3. **`LIKE` ignoriert Kollationen vollständig.** Eine eigene Kollation (`CreateCollation`)
   repariert den Payee-Gleichheitsvergleich nachweislich, den Memo-Teiltext prinzipiell
   nicht. Ein Index über eine solche Kollation koppelt zusätzlich jeden Schreibweg an die
   Registrierung: ein `INSERT` von einer Connection ohne registrierte Kollation schlägt hart
   fehl.
4. **Der Container läuft im Globalization-Invariant-Modus.** Das Basis-Image setzt
   `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`, die Entwicklung auf macOS läuft unter
   `de-DE`. `ToLowerInvariant` bildet in beiden Fällen identisch und voll unicodefähig ab
   (`MÜLLER GMBH` zu `müller gmbh`, `STRAßE` zu `straße`). `String.Normalize(FormD)` dagegen
   zerlegt im Invariant-Modus **nicht** mehr: `"Müller".Normalize(FormD).Length` ist lokal 7
   und invariant 6. Ein auf `Normalize` gebauter Entwurf wäre lokal grün und im Container
   still eine Nulloperation.
5. **Es gibt heute keinen Index auf `payee`.** Die einzigen drei Indizes auf
   `ledger_entries` stehen auf `account_id`, `category_id`, `date`. Der Payee-Filter läuft
   bereits jetzt als Scan innerhalb des Kontos. Die Sorge "ein Funktionsaufruf um die Spalte
   herum zerstört die Indexfähigkeit" betrifft also eine Indexfähigkeit, die nicht existiert.

## Decision
1. **Eine anwendungsdefinierte SQL-Skalarfunktion `bb_lower`.** Sie wird in
   `Persistence.getConnection` (`src/Server/Persistence.fs`) auf **jeder** ausgegebenen
   Connection registriert, vor `Open()`, und ruft `Persistence.searchFold`: schlicht
   `ToLowerInvariant`, `null` durchgereicht, damit `bb_lower(NULL)` SQL-`NULL` bleibt und
   Buchungen ohne Payee die Gleichheit wie bisher nicht treffen. `getConnection` ist der eine
   Flaschenhals, weil ADR 0008 jeder Operation eine frische Connection zuweist.
2. **Beide Textfilter benutzen sie, `COLLATE NOCASE` entfällt restlos.** In
   `Ledger.Persistence.buildFilterClause`: `bb_lower(payee) = @payee` und
   `bb_lower(memo) LIKE @memo`, mit der Parameterseite über `Core.searchFold` vorgefaltet.
   **Beide Seiten des Vergleichs laufen durch dieselbe Funktion.** Faltet nur die Spalte,
   findet jeder Suchtext mit einem Großbuchstaben still nichts, also derselbe Fehler in neuer
   Verkleidung. `buildFilterClause` faltet den Filterwert deshalb selbst, statt sich auf den
   Aufrufer zu verlassen; `EntryFilter.Payee`/`Memo` bleiben der rohe Eingabetext.
3. **Ein Mechanismus für beide Felder.** Payee und Memo stehen in der Oberfläche
   nebeneinander und versprechen dasselbe. Eine Kollation hätte Payee repariert und Memo
   kaputt gelassen (Context 3), also zwei Felder mit unterschiedlicher Wahrheit.
4. **Kein Schema-Wechsel, kein Backfill, keine Änderung am Schreibpfad.** Die Faltung
   passiert beim Lesen. `entryParams`, `upsertEntrySql`, `appendAndApply`,
   `initializeLedgerSchema` und `migrateLedgerEntryColumns` bleiben unangetastet, ebenso
   `Ledger.Domain.Entry` und die Ereignis-Payloads. Es gibt damit weder eine neue
   Schema-Version noch ein neues Ereignis: es ändert sich kein Fakt, den das Log tragen
   müsste.
5. **Groß-/Kleinschreibung ja, Diakritika nein.** `müller` findet `MÜLLER`. `mueller` findet
   `Müller` nicht, `STRASSE` findet `Straße` nicht. `String.Normalize` kommt in dieser
   Lösung nicht vor (Context 4). Eine Transliteration bräuchte eine handgepflegte
   Abbildungstabelle und eine eigene Sprachentscheidung ("ae" oder "a"), das ist eine eigene
   Entscheidung und kein Nebenprodukt eines Bugfixes.
6. **`loadPayeeSuggestions` bleibt unverändert.** Es erbt die reparierte Klausel über
   `buildFilterClause`, sein `GROUP BY payee` gruppiert weiterhin
   groß-/kleinschreibungs-empfindlich. Das ist `ledger-h4qtx`, und dort ist zusätzlich zu
   entscheiden, welche Schreibweise angezeigt wird. ADR 0036 (Neutral) hält das bereits fest.

## Consequences
### Positive
- Payee und Memo verhalten sich gleich und treffen den realen, überwiegend großgeschriebenen
  Bestand.
- Am Bestand wird nichts angefasst: keine Migration, kein Backfill, kein halbfertiger Zustand
  bei einem Abbruch, und ein Revert genügt als Rollback. Die Datei-Datenbank unter
  `~/my_apps/budgetbuddy/` bleibt byte-gleich zu einer, die der alte Stand geschrieben hätte.
- Der Ausfallmodus ist laut. Eine Connection ohne Registrierung wirft
  `no such function: bb_lower` beim ersten Filterlauf, sie zählt nie still falsch. Gemessen.
- Im gesamten Repo konstruieren nur drei Stellen eine `SqliteConnection`:
  `Persistence.fs:82` (Keep-alive-Anker), `Persistence.fs:118` (der registrierte
  Flaschenhals) und `LedgerTests.fs:1130` (eigene Drei-Spalten-Tabelle, kein Filter). Mehr
  Angriffsfläche gibt es nicht.
- `scripts/import-rules.fsx` bleibt funktionsfähig: es fasst `ledger_entries` nicht an, und
  kein Index hängt von der Registrierung ab. Genau hier wäre die Kollations-Variante
  zerbrochen.
- Der Replay-Pflichttest bleibt gültig und unangetastet, weil keine Zustandsinformation
  hinzukommt.

### Negative
- Beide Filter bleiben ein Scan innerhalb des Kontos, mit einem verwalteten Rückruf je
  geprüfter Zeile. Gemessen gegen 14.000 Zeilen: Payee-Filter 8,6 ms, Memo-Filter 10,6 ms
  (ungefilterter Count 0,2 ms, `COLLATE NOCASE` 0,8 ms). Der Aufschlag fällt nur an, wenn
  tatsächlich Text eingetippt wurde: `bb_lower` steht nur dann im SQL. Dieselbe Abwägung wie
  ADR 0036 Decision 4 für `substr(date, 1, 10)`, mit demselben Ergebnis.
- **Ein Ausdrucksindex ist nicht möglich und auch nicht nachrüstbar.** `CREATE INDEX … ON
  ledger_entries(bb_lower(payee))` scheitert mit
  `non-deterministic functions prohibited in index expressions`, und zwar auch dann, wenn die
  Funktion als deterministisch registriert wurde: SQLite lässt in Index-Ausdrücken nur
  eingebaute Funktionen zu. Wer den Index braucht, braucht eine gepflegte Normalform-Spalte
  (siehe Alternatives). Niemand soll es versuchen und dann rätseln.
- Eine SQLite-Besonderheit mehr im Lesepfad. ADR 0013 Punkt 8 hält `json_extract` und
  Vergleichbares aus dem **Gespeicherten** heraus, damit ein Datenbankwechsel billig bleibt.
  `bb_lower` berührt das Schema nicht und übersetzt sich auf Postgres in dessen eingebautes,
  unicodefähiges `lower()`: ein Token an zwei Stellen. Der Geist der Regel bleibt gewahrt.
- `%` und `_` im Memo-Suchtext wirken weiterhin als `LIKE`-Platzhalter, unverändert gegenüber
  vorher. Keine Regression, aber auch keine Reparatur.

### Neutral
- `STRAßE` wird gefunden (`ToLowerInvariant` bildet es auf `straße` ab), `STRASSE` nicht:
  .NET faltet ß in keine Richtung auf ss. Der Unterschied liegt an der SS-Schreibung, nicht
  an der Groß-/Kleinschreibung. Steht im README-Eintrag **Such-Filter (Kassenbuch)**.
- `ledger-h4qtx` (Payee-Vorschläge gruppieren) wird durch diese Entscheidung nicht blockiert,
  danach aber billiger und korrekter: `GROUP BY bb_lower(payee)` im SQL-Pfad und
  `Core.searchFold` als Gruppierungsschlüssel im F#-Pfad stehen dann bereit. Der dort heute
  vorgeschlagene Weg `GROUP BY payee COLLATE NOCASE` trüge denselben ASCII-Defekt.
- Ab grob 100.000 Buchungen, oder sobald ein gefilterter Seitenaufruf messbar über 200 ms
  braucht (das Scheiterkriterium der Vision ist "langsamer oder umständlicher als
  YNAB-Web"), lautet die Antwort `payee_folded` als gepflegte Spalte mit Index plus Backfill,
  und `bb_lower` bleibt daneben für Memo bestehen. Nie die Rückkehr zu `COLLATE NOCASE`.

## Alternatives considered
- **SQLites ICU-Erweiterung**: nicht einkompiliert (Context 1), bräuchte ein eigenes natives
  SQLite-Build im Alpine-Image, für einen Filter auf 14.000 Zeilen.
- **Benutzerdefinierte Kollation über `CreateCollation`**: repariert nachweislich nur den
  Gleichheitsvergleich, nicht `LIKE`, dessen Faltung in `like()` auf ASCII fest verdrahtet
  ist. Ein Index über die Kollation würde zusätzlich jeden Schreibweg an die Registrierung
  koppeln, auch den rohen Connection-Test in `LedgerTests.fs:1130` und jedes künftige Skript.
- **F#-seitige Faltung über `loadAllMatching`**: würde bei jedem gesetzten Payee- oder
  Memo-Filter die volle Treffermenge laden, was ADR 0036 Decision 3 ausdrücklich nur dem
  Betragsfilter zugesteht ("sonst würde jede Abfrage ohne Not alle rund 14.000 Buchungen
  laden"), und würde den Zweig verbreitern, den `ledger-r7dm2` gerade als fast ungetestet
  anmahnt.
- **Gepflegte Normalform-Spalten `payee_normalized`/`memo_normalized`** im Schreibpfad, mit
  Index auf Payee: der einzige Weg zu einem indizierten Payee-Filter, und als abgeleitete
  Spalte auch ohne Ereignis zulässig (sie ist eine totale, deterministische, reine Funktion
  einer Spalte, die selbst schon aus dem Log gefaltet ist, also `f(fold(log))`, anders als
  `approved` in ADR 0032, das ein neuer Fakt aus YNAB war). Verworfen für diese Aufgabe, weil
  sie zwei `ALTER TABLE`, einen F#-Backfill über 14.000 Zeilen (SQL kann nicht falten,
  Context 2), eine zweite Stelle im Schreibpfad und eine neue Invariante kostet, die still
  verrotten kann ("`payee_normalized` ist immer `fold(payee)`"), also genau die Fehlerklasse,
  die dieser Bug ist. Gekauft wird dafür ein Index auf einer Spalte, die heute keinen hat
  (Context 5) und die ohnehin stets zusammen mit `account_id` gefiltert wird, während
  `memo LIKE '%…%'` auch damit Scan bliebe. Bleibt die dokumentierte Antwort für deutlich
  größeres Volumen.
- **Die Einschränkung festschreiben und ehrlich benennen**: verworfen per Entscheidung Roman
  vom 2026-09-20. Der Bestand ist überwiegend großgeschrieben, der Filter versagt also im
  Normalfall, nicht im Randfall.
