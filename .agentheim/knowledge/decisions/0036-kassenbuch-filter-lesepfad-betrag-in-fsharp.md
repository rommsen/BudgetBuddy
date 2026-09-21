---
id: 0036
title: Kassenbuch-Suchfilter über eine gemeinsame EntryFilter/WHERE-Klausel, Betrag in F# nachgefiltert, Zeitraum kind-unabhängig verglichen
scope: ledger
status: accepted
date: 2026-09-19
supersedes: []
superseded_by: []
related_tasks: [ledger-vgfw3]
related_adrs: [0013, 0017, 0029]
related_research: []
---

# ADR 0036: Kassenbuch-Suchfilter über eine gemeinsame EntryFilter/WHERE-Klausel, Betrag in F# nachgefiltert, Zeitraum kind-unabhängig verglichen

## Context
`ledger-vgfw3` erweitert `getLedgerEntriesPage` um fünf Suchfilter (Kategorie als
Mehrfachauswahl, Zeitraum, Payee, Betrag, Memo). Vier Lesefunktionen tragen heute schon
dieselben Kriterien (Konto, `IncludeDeleted`, `OnlyNeedsReview`) getrennt aus:
`loadEntriesPage`, `countEntries`, `loadAmountsForQuery`, `countDeletedEntries`. Dazu kommt
eine neue fünfte, `loadPayeeSuggestions`, für die Payee-Vorschläge samt Anzahl **innerhalb
des aktuellen Treffers**. Fünf zusätzliche Filter, unabhängig in fünf Funktionen
nachgezogen, laufen fast sicher irgendwann auseinander — genau das Risiko, das die
Task-Vorgabe benennt ("Sonst zeigt die Summenzeile eine Summe, die nicht zur Trefferzahl
gehört"). Der Bestand liegt bei rund 14.000 Buchungen; ein Filter, der erst nach dem Laden
aller Zeilen greift, wäre korrekt, aber nicht tragfähig.

Zwei der fünf Filter widersprechen sich in ihrer SQL-Tauglichkeit: der Betrag liegt laut
ADR 0017 als exakter Dezimal-Text vor, ein SQL-Vergleich (auch über
`CAST(amount AS REAL)`) würde genau die Rundungsdrift riskieren, die ADR 0017 ausschließt.
Der Zeitraum liegt ebenfalls als Text vor (ADR 0017, Punkt 4: "ISO-8601-Roundtrip-Text
(`"o"` plus `RoundtripKind`)") — Iteration 1 dieser Aufgabe nahm fälschlich an, dieses
Format sei "fest, gleich lang und ohne Vorzeichen", und verglich `date` deshalb direkt als
Text gegen `dateToText d.Date`. Ein Verifier hat das widerlegt: Roundtrip-Text ist
Kind-abhängig unterschiedlich lang (`Utc` hängt `Z` an, `Local` eine Zonenverschiebung,
`Unspecified` nichts), und die `date`-Spalte enthält beide Formen gemischt — der YNAB-Spiegel
schreibt `Utc` (`YnabObservations.fs`, `DateTime.SpecifyKind(date, DateTimeKind.Utc)`),
Comdirect- und Quick-Add-Zeilen kommen aus `DateTime.Parse` ohne Kind. Unter SQLites
BINARY-Kollation verliert ein Randvergleich dadurch still Buchungen genau am Grenztag, je
nachdem, mit welchem Kind der Client-seitige Rand gebaut wird — keine Wahl des
Client-Kinds macht beide Ränder zugleich richtig, weil der Bestand selbst gemischt ist.

## Decision
1. **Eine gemeinsame `EntryFilter`-Aufzeichnung plus `buildFilterClause`** in
   `Ledger.Persistence`: Konto, `IncludeDeleted`, `OnlyNeedsReview`, `CategoryIds`,
   `DateFrom`/`DateTo`, `Payee`, `Memo` fließen in eine WHERE-Klausel plus Dapper-Parameter,
   die `loadEntriesPage`, `countEntries`, `loadAmountsForQuery`, `countDeletedEntries` und
   `loadPayeeSuggestions` alle gemeinsam aufrufen. Sie können dadurch nicht mehr
   auseinanderlaufen; ein neuer Filter, der `buildFilterClause` nicht durchläuft, fällt beim
   Lesen des Codes sofort auf.
2. **Kategorie ist eine Mehrfachauswahl**: `CategoryIds: Guid list`, SQL `category_id IN
   @categoryIds`. Trifft nur die einfache Kategorie-Spalte, nicht Split-Zeilen (die liegen
   als JSON-Text; Queries dürfen keine SQLite-JSON-Funktionen benutzen, ADR 0013 Punkt 8).
   Eine Buchung, deren einzige Kategorie in einem Split steckt, findet der Filter also nicht
   — bewusst in Kauf genommen für den weit häufigeren Fall der einfachen Kategorie.
3. **Betrag bleibt außerhalb der SQL-Klausel** (ADR 0017): `AmountMin`/`AmountMax` sind Teil
   von `EntryFilter`, aber `buildFilterClause` ignoriert sie. Stattdessen lädt
   `loadAllMatching` bei gesetztem Betragsfilter die volle (durch die übrigen Kriterien
   bereits eingeschränkte) Treffermenge und filtert in F# (`amountMatches`) nach. Ohne
   Betragsfilter benutzen alle fünf Lesefunktionen weiterhin ihre eigene gezielte
   SQL-Abfrage (LIMIT/OFFSET, COUNT, Projektion) statt `loadAllMatching`, sonst würde jede
   Abfrage ohne Not alle rund 14.000 Buchungen laden.
4. **Zeitraum vergleicht nur den Datumsanteil, kind-unabhängig**: `substr(date, 1, 10) >=
   @dateFrom` / `<= @dateTo`, mit `@dateFrom`/`@dateTo` als `d.Date.ToString("yyyy-MM-dd")`.
   Die ersten zehn Zeichen eines ISO-8601-Roundtrip-Textes sind bei jedem `DateTimeKind`
   identisch — der Kind-Suffix beginnt erst danach —, der Vergleich ist damit unabhängig
   davon, mit welchem Kind eine Buchung geschrieben wurde. `substr` ist gewöhnliches SQL,
   keine SQLite-Spezialität (ADR 0013 Punkt 8 verbietet nur `json_extract` und Vergleichbares,
   nicht String-Funktionen). Die Alternative, den Schreibpfad auf ein einheitliches
   `DateTimeKind` zu normalisieren, wurde erwogen und verworfen (siehe Alternatives): sie
   wäre eine Migrationsfrage für rund 14.000 Bestandszeilen, keine bloße Vergleichs-Korrektur,
   und hätte den eigentlichen Fehler (der Vergleich, nicht die Schreibweise) nicht behoben.
5. **~~Korrigiert (Iteration 2)~~**: frühere Fassung dieser Decision behauptete, ADR 0017
   Punkt 4 lege für Zeitstempel ein Format fest, das "fest, gleich lang und ohne Vorzeichen"
   sei, und leitete daraus einen direkten SQL-Textvergleich (`date >= @dateFrom`) ab. Das war
   eine Fehllesung: ADR 0017 Punkt 4 sagt nur "ISO-8601-Roundtrip-Text (`"o"` plus
   `RoundtripKind`)" — Roundtrip ist genau die Eigenschaft, die die Textlänge
   Kind-abhängig macht (siehe Context). Die falsche Prämisse hat den direkten Textvergleich
   überhaupt erst gerechtfertigt; Decision 4 oben ersetzt sie durch den
   kind-unabhängigen `substr`-Vergleich.
6. **`countNeedsReviewEntries` bleibt ungefiltert.** Der Chip "Zu prüfen (n)" ist ein fester
   Zähler über das Konto (analog zum bestehenden `OnlyNeedsReview`-Verhalten vor dieser
   Aufgabe), keine Trefferzahl der laufenden Suche. Die fünf neuen Filter wirken deshalb
   bewusst nicht auf ihn, anders als auf `countDeletedEntries`, das die Summenzeile stützt.
7. **Payee-Vorschläge zählen über den gesamten Treffer**, nicht über die geladene Seite und
   nicht über alle Buchungen: `loadPayeeSuggestions` teilt sich `buildFilterClause` mit den
   vier Zählwegen, mit `filter.Payee` erwartungsgemäß auf `None` (der Aufrufer übergibt nie
   einen bereits gewählten Payee-Filter an sich selbst).

## Consequences
### Positive
- Die vier Zählwege und die Payee-Vorschläge können nicht mehr auseinanderlaufen: ein neuer
  Filter, der `buildFilterClause` umgeht, fällt beim Lesen sofort auf.
- Der Zeitraum-Filter ist jetzt unabhängig davon korrekt, mit welchem `DateTimeKind` eine
  Buchung geschrieben wurde — trifft den überwiegenden, gemischten Bestand, nicht nur den
  Testfall mit einheitlichem Kind.
- Der Betragsfilter riskiert keine Gleitkomma-Drift (ADR 0017 bleibt vollständig gewahrt).
- Bei rund 14.000 Buchungen bleibt jede Abfrage ohne Betragsfilter eine gezielte, indizierte
  SQL-Abfrage statt eines Ladens aller Zeilen.

### Negative
- `substr(date, 1, 10) >= @dateFrom` ist für SQLite kein sargable Prädikat auf dem
  bestehenden Index `idx_ledger_entries_date` — ein Zeitraum-Filter kann diesen Index nicht
  für einen Bereichs-Scan nutzen, anders als der (fehlerhafte) direkte Vergleich zuvor. Bei
  rund 14.000 Zeilen ist ein Tabellen-Scan mit `substr`-Prädikat weiterhin schnell genug
  (siehe Verifikation); wird das Volumen deutlich größer, ist die Antwort eine gepflegte
  reine Datumsspalte (z. B. eine generierte Spalte `date_only`) mit eigenem Index, nicht die
  Rückkehr zum direkten Textvergleich.
- Ein Betragsfilter erzwingt weiterhin das Laden der vollen (durch die übrigen Kriterien
  bereits eingeschränkten) Treffermenge, wie in ADR 0017 vorgesehen.
- Zwei parallele, sich in Teilen überschneidende `EntryFilter`-Aufbauten in
  `QueryHandlers.toEntryFilter` und `getLedgerPayeeSuggestions` (letzterer zwingt `Payee =
  None`) — bewusst getrennt gehalten, weil sie sich in genau diesem einen Feld
  unterscheiden.

### Neutral
- `loadPayeeSuggestions` gruppiert Payees über `GROUP BY payee` (SQL-Pfad) bzw.
  `List.countBy id` (F#-Pfad bei Betragsfilter) groß-/kleinschreibungs-empfindlich, während
  der Payee-Filter selbst `COLLATE NOCASE` vergleicht. "Rewe" und "REWE" erscheinen dadurch
  als zwei Vorschläge, die beide dieselbe Treffermenge liefern würden. Nicht korrigiert in
  dieser Aufgabe (siehe Backlog `ledger-h4qtx`): eine korrekte Lösung müsste zusätzlich
  festlegen, welche Schreibweise als Vorschlag angezeigt wird, das ist mehr als eine
  Ein-Zeilen-Änderung.
- **Nachtrag (ledger-k3wqn, ADR 0037):** die oben beschriebene `COLLATE NOCASE`-Faltung
  des Payee-Filters ist entfallen. Payee und Memo vergleichen jetzt über die
  anwendungsdefinierte SQL-Funktion `bb_lower`, die auch Umlaute faltet; `COLLATE NOCASE`
  faltet nur ASCII A-Z und wird von `LIKE` ohnehin ignoriert. `loadPayeeSuggestions`
  selbst gruppiert unverändert groß-/kleinschreibungs-empfindlich, das bleibt
  `ledger-h4qtx`.

## Alternatives considered
- **Zeitpunkte beim Schreiben auf ein einheitliches `DateTimeKind` (z. B. immer `Utc`)
  normalisieren**, statt den Lesevergleich zu ändern: hätte den fehlerhaften Vergleich zwar
  auch behoben, wäre aber eine Migrationsfrage für rund 14.000 Bestandszeilen gewesen (jede
  gemischt-Kind geschriebene Zeile müsste neu geschrieben werden) und damit eine größere,
  eigenständige Entscheidung — nicht die naheliegende Korrektur eines Lesevergleichs.
  Verworfen für diese Aufgabe; bliebe eine Option, falls der `substr`-Vergleich je zum
  Performance-Problem wird.
- **`CAST(amount AS REAL)` für den Betragsfilter**: verworfen, exakt die Rundungsdrift, die
  ADR 0017 ausschließt.
- **Jede Lesefunktion filtert eigenständig, ohne gemeinsame `buildFilterClause`**: verworfen,
  das Risiko des Auseinanderlaufens (Task-Vorgabe) ist genau das, was diese Aufgabe
  verhindern soll.