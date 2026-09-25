---
id: ledger-xk9wq
title: "Split-Zeilen-Kategorien über eine abgeleitete Indextabelle in den Kassenbuch-Kategorie-Filter holen"
status: done
type: feature
context: ledger
created: 2026-09-19
completed:
depends_on: [ledger-vgfw3, ledger-k3wqn]
blocks: []
tags: [backend, kassenbuch, filter, splits]
related_adrs: [0006, 0013, 0019, 0036, 0038]
related_research: []
prior_art: [ledger-vgfw3]
---

## Why

`ledger-vgfw3` baut den Kategorie-Filter im Lesepfad (`LedgerEntryPageQuery.CategoryIds`)
gegen die `category_id`-Spalte der Zustandstabelle (ADR 0036 Punkt 2). Eine aufgeteilte
Buchung (Splits, ADR 0006) trägt dort `NULL`, ihre Kategorien stecken als JSON-Text in der
Spalte `splits`. ADR 0013 Punkt 8 verbietet SQLite-JSON-Funktionen in Abfragen. Eine
Buchung, deren einzige Kategorie in einem Split steckt, findet der Filter also nicht.

**Das ist keine fehlende Zeile in einer langen Liste, sondern eine falsche Antwort.** Die
Kategorie-Auswahl der Oberfläche kommt aus `getCategories` gegen YNAB
(`src/Shared/Api.fs`), nicht aus dem Bestand des Kassenbuchs. Roman kann also eine
Kategorie wählen, die es nur in Split-Zeilen gibt, und bekommt darauf eine leere Liste mit
Summe 0,00. Die Frage war gültig, die Antwort ist falsch, und nichts an der Oberfläche sagt
das.

Dazu kommen Häufigkeit und Breite. Roman setzt den Kategorie-Filter nach eigener Schätzung
in 70 bis 80 Prozent der Fälle. Und der Filter sitzt in
`Ledger.Persistence.buildFilterClause`, wirkt damit auf alle fünf Lesewege: `Entries`,
`TotalCount`, `TotalAmount`, `DeletedCount` und die Payee-Vorschläge zählen alle
gleichsinnig zu wenig. Weil sie gleichsinnig irren, widersprechen sie sich nie, und der
Fehler fällt nicht auf. `NeedsReviewCount` ist nicht betroffen: `countNeedsReviewEntries`
nimmt bewusst gar keinen `EntryFilter` (ADR 0036 Punkt 6).

## What

Der Weg ist entschieden (ADR 0038): eine abgeleitete Indextabelle, die nur
Split-Zeilen-Kategorien führt, in derselben Transaktion gepflegt wie Log und Zustand. Der
Filter bleibt reines, indexfähiges SQL. Der F#-seitige Nachfilter ist verworfen, weil er
den häufigsten Fall auf `loadAllMatching` zwingen würde, was dieser Task ausschließt.

Alle Codeänderungen liegen in `src/Server/Ledger/Domain.fs` und
`src/Server/Ledger/Persistence.fs` plus Tests. `QueryHandlers.fs` wird nicht angefasst:
`toEntryFilter` reicht die Kategorie-Ids bereits durch, nur die Klausel war zu eng.

1. **`src/Server/Ledger/Domain.fs`**: reine Funktion
   `splitCategoryIds : Entry -> Guid list`. Wählt aus `entry.Splits` die `ToCategory`-Ziele
   aus, entdoppelt (`List.distinct`), überspringt `ToTransfer` und `Uncategorized`
   (ADR 0006, ADR 0019). Kein I/O.
2. **`src/Server/Ledger/Persistence.fs`, `initializeLedgerSchema`**: neue Tabelle
   `ledger_entry_split_categories (category_id TEXT NOT NULL, entry_id TEXT NOT NULL,
   PRIMARY KEY (category_id, entry_id))` plus
   `CREATE INDEX IF NOT EXISTS idx_ledger_entry_split_categories_entry ON
   ledger_entry_split_categories(entry_id)`. Die Spaltenreihenfolge im Primärschlüssel ist
   tragend (deckende Suche nach `category_id`, Rückgabe `entry_id`), der zweite Index
   bedient das `DELETE` des Schreibpfads.
3. **`src/Server/Ledger/Persistence.fs`, neuer privater Helfer
   `writeSplitCategoryIndex (conn) (tx) (entry)`**: erst `DELETE` aller Zeilen dieser
   Buchung, dann `INSERT` je Kategorie aus `splitCategoryIds`. Immer beides, auch bei leerer
   Split-Liste, sonst bleibt eine Zeile stehen, nachdem eine aufgeteilte Buchung wieder zu
   einer einfachen wurde.
4. **`src/Server/Ledger/Persistence.fs`, `appendAndApply` (Zeile ~433)**: Aufruf von
   `writeSplitCategoryIndex` unmittelbar hinter dem einen `conn.Execute(upsertEntrySql,
   entryParams newState, tx)`, auf derselben Connection und in derselben Transaktion
   (ADR 0013 Punkt 5). Das ist die einzige Stelle im Repo, die `ledger_entries` schreibt.
5. **`src/Server/Ledger/Persistence.fs`, `rebuildSplitCategoryIndex (conn: SqliteConnection)
   : int`**: leert die Tabelle und baut sie aus `ledger_entries` neu auf, Kandidaten über
   `WHERE splits <> '[]'` eingeschränkt. Nimmt die Connection als Parameter wie
   `migrateLedgerEntryColumns`, wird am Ende von `initializeLedgerSchema` direkt hinter
   diesem gerufen. Liefert die Zahl der geschriebenen Indexzeilen. Liest und schreibt das
   Ereignis-Log nicht. Muss nach `rowToEntry` (Zeile ~244) und nach `writeSplitCategoryIndex`
   im Modul stehen, F# kennt keine Vorwärtsreferenz.
6. **`src/Server/Ledger/Persistence.fs`, `buildFilterClause`, Kategorie-Zweig
   (Zeilen ~610-619)**: ersetzt `AND category_id IN @categoryIds` durch die geklammerte
   OR-Form `AND (category_id IN @categoryIds OR id IN (SELECT entry_id FROM
   ledger_entry_split_categories WHERE category_id IN @splitCategoryIds))`. Die äußere
   Klammer ist tragend (`countDeletedEntries` und `loadPayeeSuggestions` hängen weitere
   `AND`-Glieder an). Die Unterabfrage bleibt nicht korreliert, kein `EXISTS` mit
   Rückverweis auf `ledger_entries.id`. Dieselbe Id-Liste geht unter zwei Parameternamen
   hinein. Die Payee- und Memo-Zweige derselben Funktion bleiben unangetastet (sie gehören
   `ledger-k3wqn`, das vorher läuft).
7. **`src/Server/Ledger/Persistence.fs`, `explainEntryFilterPlan (accountId) (filter) :
   string list`**: gibt `EXPLAIN QUERY PLAN` der gebauten Filter-Abfrage zeilenweise zurück,
   damit AC12 prüfbar wird. Nur für die Prüfung, kein Aufrufer im Produktivpfad. Gleiche
   Rolle wie das Herausziehen von `accentBarClassName` in `ledger-w9skm`: dieses Projekt
   zieht Entscheidungen so weit heraus, dass sie etwas rot machen können.
   Sie beantwortet ausdrücklich die Frage "welchen Zugriffspfad nimmt diese Abfrage", nicht
   "wie lautet das SQL". Der Rückgabewert ist deshalb der Plan, nicht die Klausel, und
   `buildFilterClause` bleibt `private`. Eine Variante, die den SQL-Text nach außen gibt,
   ist nicht zulässig: sie lädt zu einem Test ein, der bei jeder Umformulierung bricht und
   dann entschärft statt repariert wird.
8. **`src/Tests/LedgerPageTests.fs`**: Seed-Helfer `seedSplitEntry` neben `seedEntryFull`
   (Zeile ~1083, das heute `Splits = []` fest verdrahtet). Der Gesamtbetrag muss aus den
   Split-Zeilen gebildet werden, sonst lehnt `apply` die Splits über `mkSplits` ab
   (ADR 0006). Neue Tests in die bestehende Gruppe "Fünf Such-Filter im Lesepfad"
   (ab Zeile ~1387).

## Acceptance criteria

- [ ] AC1 Liste: Eine Buchung mit Splits, deren einzige Kategorie in einer Split-Zeile
      steckt (`category_id` der Buchung ist `NULL`), erscheint in `Entries`, wenn diese
      Kategorie in `CategoryIds` steht.
- [ ] AC2 `TotalCount`: Dieselbe Buchung zählt in der Trefferzahl mit. Test mit drei
      Buchungen (eine Split-Buchung mit Kategorie X, eine einfache mit X, eine mit Y)
      liefert bei Filter auf X `TotalCount = 2`.
- [ ] AC3 `TotalAmount`: Die Summe desselben Treffers enthält den Betrag der Split-Buchung,
      exakt in F# gebildet (kein SQL-`SUM`, ADR 0017).
- [ ] AC4 `DeletedCount`: Eine in YNAB gelöschte Split-Buchung mit Kategorie X wird bei
      Filter auf X in `DeletedCount` gezählt, unabhängig von `IncludeDeleted`.
- [ ] AC5 Payee-Vorschläge: `getLedgerPayeeSuggestions` mit `CategoryIds = [X]` enthält den
      Payee der Split-Buchung.
- [ ] AC6 `NeedsReviewCount` bleibt ungefiltert: ein Kategorie-Filter, der die Treffermenge
      verkleinert, ändert `NeedsReviewCount` nicht (Gegenprobe zu ADR 0036 Punkt 6).
- [ ] AC7 Keine veralteten Indexzeilen: Wandern die Splits einer Buchung per
      `EntryCategorizationChangeObservedInYnab` von Kategorie X nach Y, findet der Filter
      sie unter X nicht mehr und unter Y schon. Wird aus derselben Buchung wieder eine
      einfache Buchung ohne Splits, findet kein Kategorie-Filter sie mehr über X.
- [ ] AC8 Ableitung: `splitCategoryIds` liefert für zwei Split-Zeilen derselben Kategorie
      genau einen Eintrag, und für eine Buchung, deren Zeilen ausschließlich `ToTransfer`
      oder `Uncategorized` sind, eine leere Liste. Eine solche Buchung wird von keinem
      Kategorie-Filter gefunden.
- [ ] AC9 Backfill und Bestand: `rebuildSplitCategoryIndex` stellt nach einem vollständigen
      `DELETE FROM ledger_entry_split_categories` den Index so wieder her, dass der Filter
      die Split-Buchung wieder findet. Ein zweiter Aufruf liefert denselben Rückgabewert und
      dieselbe Treffermenge. Bereits gespeicherte Buchungen kommen damit allein aus
      `ledger_entries` nach, ohne dass das Ereignis-Log gelesen, neu geschrieben oder
      migriert wird.
- [ ] AC10 Keine Drift: Der Inhalt von `ledger_entry_split_categories` stimmt mit dem
      überein, was `splitCategoryIds` auf das Ergebnis von `replayAll ()` angewandt ergibt
      (Gegenprobe im Sinne von ADR 0013 Punkt 6).
- [ ] AC11 Backfill-Prädikat festgenagelt: Eine über `appendAndApply` geschriebene Buchung
      ohne Splits hat in der Spalte `splits` exakt den Text `[]`.
- [ ] AC12 Performance, Zugriffspfad: `explainEntryFilterPlan` liefert bei gesetztem
      Kategorie-Filter einen Plan, in dem `ledger_entry_split_categories` vorkommt und per
      `SEARCH` bedient wird, und der weder `SCAN ledger_entry_split_categories` noch
      `CORRELATED` enthält. Geprüft wird ausschließlich der **Zugriffspfad**, nie der
      SQL-Text: ein Test, der die erzeugte WHERE-Klausel als Zeichenkette vergleicht, ist
      nicht zulässig, er bricht bei jeder Umformulierung und wird dann entschärft statt
      repariert. Geprüft wird auf Teilstrings des Plans, nicht auf seinen Volltext (die
      Formulierung ist SQLite-versionsabhängig). Zeigt sich im Versuch, dass SQLite trotz
      Index anders plant, wird das Kriterium auf sein Minimum zurückgenommen:
      `ledger_entry_split_categories` kommt im Plan vor, die Split-Prüfung findet also in
      der Abfrage statt und nicht dahinter. Eine Abweichung davon gehört in die
      Outcome-Notiz.
- [ ] AC13 Performance, häufigster Fall bleibt vom Ladepfad weg: In
      `src/Server/Ledger/Persistence.fs` gibt es weiterhin genau fünf Aufrufe von
      `loadAllMatching` (die Definition bei Zeile ~673 nicht mitgezählt) und genau fünf
      Vorkommen von `if hasAmountFilter filter then`. Quelltext-Zusicherung nach dem Muster
      von `FontFaceRegressionTests.fs` und `BackendPortGuardTests.fs`.
- [ ] AC14 Performance, Arbeitsumfang: Bei 1.000 Buchungen auf einem Konto, von denen drei
      die gesuchte Split-Kategorie tragen, liefert `getLedgerEntriesPage` `TotalCount = 3`.
      Läuft der Testfall über etwa zwei Sekunden, wird die Zahl gesenkt und die tatsächlich
      benutzte in der Outcome-Notiz vermerkt.
- [ ] AC15 Kombination mit dem Betragsfilter: Kategorie-Filter plus gesetzter Betragsfilter
      findet dieselbe Split-Buchung (der `loadAllMatching`-Zweig ruft dieselbe
      `buildFilterClause`).
- [ ] AC16 Die acht bestehenden Filter-Tests aus `ledger-vgfw3`
      (`src/Tests/LedgerPageTests.fs`, Testgruppe "Fünf Such-Filter im Lesepfad", AC1 bis
      AC8) sind im Diff unverändert und grün.
- [ ] AC17 Doku: `.agentheim/knowledge/contexts/ledger/README.md` beschreibt den
      Kategorie-Filter unter "Such-Filter (Kassenbuch)" nicht mehr als "trifft nur
      `category_id`", sondern als "einfache Kategorie und Split-Zeilen-Kategorien
      (ADR 0038)", und führt `rebuildSplitCategoryIndex` unter "Migration bestehender
      Zustandstabellen" neben `migrateLedgerEntryColumns`.
- [ ] AC18 `dotnet build` und `dotnet test` grün.

## Notes

Entdeckt und bewusst nicht gelöst bei `ledger-vgfw3` (Server-Hälfte der Kassenbuch-Filter),
Scope-Grenze laut ADR 0036 Punkt 2 und Alternatives-Abschnitt. Der Weg ist in ADR 0038
entschieden, die verworfene Alternative dort begründet.

**Zum `depends_on` auf `ledger-k3wqn`:** das ist eine Sequenzierungs-Kante gegen einen
Merge-Konflikt, keine fachliche Abhängigkeit. `ledger-k3wqn` (Umlaut-Faltung) ändert die
Payee- und Memo-Zweige derselben Funktion `buildFilterClause` und registriert dafür eine
anwendungsdefinierte SQL-Funktion `bb_lower` in `Persistence.getConnection`. Es fasst weder
Schema noch Migration an, überschneidet sich mit dieser Aufgabe also nur textuell in einer
Funktion. Läuft es zuerst, setzt diese Aufgabe ihren Kategorie-Zweig in eine bereits
stabile Fassung ein.

**Nicht Teil dieser Aufgabe:** die Testschuld des Betragsfilter-Zweigs aus `ledger-r7dm2`
Punkt 1. AC15 nimmt davon ein Stück mit, weil es ohnehin anfällt; der Rest bleibt dort.
Ebenso nicht hier: ein Filter "ohne Kategorie" für Buchungen, deren Split-Zeilen alle
`Uncategorized` sind. Das ist eine Produktfrage für die Filterleiste, vermerkt an
`ledger-j8xq4`.

**Stolpersteine für den Worker:** `seedEntryFull` verdrahtet `Splits = []` fest, ein eigener
Seed-Helfer ist Pflicht. `apply` erzwingt die `mkSplits`-Invarianten (mindestens zwei
Zeilen, vorzeichenrichtige Summe gleich dem Buchungsbetrag, eine Währung). Dapper von F#
aus verschluckt sich gelegentlich an `conn.Execute(sql, null, tx)`, dann hilft
`(null: obj)` oder ein leeres `DynamicParameters()`. `EXPLAIN QUERY PLAN` liefert vier
Spalten, die interessante ist `detail`; notfalls über einen `[<CLIMutable>]`-Zeilentyp
lesen statt über `Query<string>`. Der Betragsfilter-Zweig sitzt vollständig in
`Persistence.fs` (`hasAmountFilter` 580, `amountMatches` 586, `loadAllMatching` 673,
`safePage` 683, Gabelungen bei 700/723/741/791/814), nicht in `QueryHandlers.fs`.

## Outcome

Der Kategorie-Filter des Kassenbuchs (`Ledger.Persistence.buildFilterClause`) findet jetzt auch Buchungen, deren einzige Kategorie in einer Split-Zeile steckt, nicht mehr nur die einfache `category_id`-Spalte (ADR 0038, gebaut auf dem Server-Teil aus `ledger-vgfw3`).

**Domäne** (`src/Server/Ledger/Domain.fs:120`): die reine Funktion `splitCategoryIds : Entry -> Guid list` liest `entry.Splits`, wählt die `ToCategory`-Ziele aus und entdoppelt (`List.distinct`); `ToTransfer`- und `Uncategorized`-Zeilen (ADR 0006, ADR 0019) erzeugen keinen Eintrag. Kein I/O.

**Persistenz** (`src/Server/Ledger/Persistence.fs`):
- Neue Tabelle `ledger_entry_split_categories (category_id, entry_id)` mit Primärschlüssel in dieser Reihenfolge (deckende Suche nach `category_id`) plus einem zweiten Index auf `entry_id` fürs `DELETE` des Schreibpfads.
- Neuer privater Helfer `writeSplitCategoryIndex` (Zeile 381): `DELETE` aller Zeilen einer Buchung, dann `INSERT` je Kategorie aus `splitCategoryIds`, immer beides. Aufgerufen aus `appendAndApply` unmittelbar hinter dem `ledger_entries`-Upsert, auf derselben Connection und in derselben Transaktion (ADR 0013 Punkt 5).
- `rebuildSplitCategoryIndex (conn) : int` (Zeile 509): leert die Tabelle und baut sie aus `ledger_entries` neu auf (Kandidaten über `WHERE splits <> '[]'`), liefert die Zahl der geschriebenen Zeilen. Läuft am Ende von `initializeLedgerSchema` (Zeile 537), direkt hinter `migrateLedgerEntryColumns`; liest und schreibt das Ereignis-Log nicht.
- `buildFilterClause`, Kategorie-Zweig (Zeile ~709): `AND category_id IN @categoryIds` wurde zur geklammerten OR-Form `AND (category_id IN @categoryIds OR id IN (SELECT entry_id FROM ledger_entry_split_categories WHERE category_id IN @splitCategoryIds))` (Zeile 724), nicht korreliert, mit derselben Id-Liste unter zwei Parameternamen. Payee-/Memo-Zweige (`ledger-k3wqn`) unangetastet.
- `explainEntryFilterPlan (accountId) (filter) : string list` (Zeile 785): gibt `EXPLAIN QUERY PLAN` der gebauten Filter-Abfrage zeilenweise zurück, nur für die Prüfung (AC12); `buildFilterClause` bleibt `private`.

**Tests**: 16 neue Testfälle.
- `src/Tests/LedgerTests.fs`: vier reine Domain-Tests für `splitCategoryIds` (Entdopplung, ausschließlich ToTransfer/Uncategorized → leere Liste, ohne Splits → leere Liste, mehrere verschiedene Kategorien).
- `src/Tests/LedgerPageTests.fs`: zwölf Persistence-Tests in der bestehenden Gruppe "Kassenbuch-Seite: seitenweise Liste und Kontostaende (Persistence)", eigenes Konto je Testfall (W..AF, fortlaufend hinter dem bisherigen V). Neuer Seed-Helfer `seedSplitEntry`/`seedDeletedSplitEntry` (Splits, Gesamtbetrag aus der Split-Summe gebildet). Abgedeckt: AC1-AC3 (Liste/TotalCount/TotalAmount einer Split-Buchung), AC4 (DeletedCount), AC5 (Payee-Vorschläge), AC6 (NeedsReviewCount bleibt ungefiltert), AC7 (Kategorie wandert X→Y, dann Buchung wird wieder einfach: keine veraltete Indexzeile), AC15 (Kombination mit Betragsfilter), AC9 (Neuaufbau nach vollständigem Löschen, Idempotenz), AC10 (kein Drift gegen `splitCategoryIds` über `replayAll()`, über den gesamten Bestand geprüft via Rohzugriff auf `ledger_entry_split_categories`), AC11 (Backfill-Prädikat `splits = '[]'` festgenagelt), AC12 (Zugriffsplan: Indextabelle per SEARCH, kein SCAN, keine CORRELATED-Unterabfrage; Plan enthält tatsächlich `SEARCH ledger_entry_split_categories`, kein Fallback auf das Minimalkriterium nötig), AC13 (Quelltext-Zusicherung: weiterhin genau fünf `loadAllMatching`-Aufrufe und fünf `hasAmountFilter`-Gabelungen), AC14 (1000 Buchungen, drei mit der gesuchten Split-Kategorie, `TotalCount = 3`; Seeding plus Abfrage lief in allen Beobachtungen deutlich unter 200ms, weit unter der 2s-Grenze, keine Reduktion der Anzahl nötig). AC16: die acht bestehenden Filter-Tests aus `ledger-vgfw3` wurden nicht angefasst (Diff enthält für sie ausschließlich Kontext, keine Änderung) und sind weiterhin grün, weil die OR-Erweiterung der Klausel rückwärtskompatibel ist.

**AC17 (Doku), Teilerfüllung**: Der README_DELTA-Block oben aktualisiert die "Such-Filter (Kassenbuch)"-Bullet unter "Ubiquitous language" wie gefordert (lokal gegen das echte Gate geprüft: `node scripts/readme-delta-check.mjs --bc ledger --delta <datei>` liefert Exit 0). Der zweite Teil von AC17 ("führt rebuildSplitCategoryIndex unter 'Migration bestehender Zustandstabellen' neben migrateLedgerEntryColumns") ließ sich über die mechanisierte README_DELTA-Grammatik NICHT abbilden: der betroffene Absatz im Abschnitt "## Ereignisse" ist Fließtext ohne "- **Begriff**"-Bullet-Form, und das Gate lehnt dort sowohl `replace` ("Anker ... trägt kein Bullet") als auch `append` ("Einfügepunkt liegt hinter Fließtext, nicht hinter einem Bullet") ab. Das ist genau der in `CLAUDE.md` vorgesehene Fall ("Ein rotes Gate heißt: den README-Block von Hand platzieren, nicht das Delta blind anwenden"). Für die Handplatzierung, wörtlich:

Alt (im Abschnitt "## Ereignisse", Unterabschnitt "Migration bestehender Zustandstabellen"):
```
**Migration bestehender Zustandstabellen** (ADR 0032):
`Ledger.Persistence.migrateLedgerEntryColumns` fügt `approved`, `flag_color` und
`flag_name` einer vor diesen Spalten angelegten Tabelle nach; bestehende Zeilen bekommen
`approved = 1` und kein Flag. Die Funktion nimmt die Connection als Parameter, damit sie
sowohl aus `initializeLedgerSchema` als auch aus einem Test gegen eine isolierte
Verbindung laufen kann; ein zweiter Aufruf bleibt folgenlos (SQLite kennt kein
`ADD COLUMN IF NOT EXISTS`, die Migration fängt den Duplikat-Fehler ab).
```

Neu:
```
**Migration bestehender Zustandstabellen** (ADR 0032, ADR 0038):
`Ledger.Persistence.migrateLedgerEntryColumns` fügt `approved`, `flag_color` und
`flag_name` einer vor diesen Spalten angelegten Tabelle nach; bestehende Zeilen bekommen
`approved = 1` und kein Flag. Die Funktion nimmt die Connection als Parameter, damit sie
sowohl aus `initializeLedgerSchema` als auch aus einem Test gegen eine isolierte
Verbindung laufen kann; ein zweiter Aufruf bleibt folgenlos (SQLite kennt kein
`ADD COLUMN IF NOT EXISTS`, die Migration fängt den Duplikat-Fehler ab). Direkt danach
läuft `Ledger.Persistence.rebuildSplitCategoryIndex`: sie leert die abgeleitete
Indextabelle `ledger_entry_split_categories` und baut sie aus dem Bestand von
`ledger_entries` neu auf (Kandidaten über `WHERE splits <> '[]'`), damit der
Kategorie-Filter auch Split-Zeilen-Kategorien findet (ADR 0038). Nimmt ebenfalls die
Connection als Parameter, ist idempotent durch ihre Form und liest wie schreibt das
Ereignis-Log nicht.
```

**Tests**: `dotnet build` (Lösung) und `dotnet test` (aus dem Worktree-Root) beide grün, 1008 bestanden / 7 übersprungen (.env-gated) / 0 fehlgeschlagen.

Schlüsseldateien:
- `src/Server/Ledger/Domain.fs:120`: `splitCategoryIds`
- `src/Server/Ledger/Persistence.fs:381,509,537,709-728,785`: `writeSplitCategoryIndex`, `rebuildSplitCategoryIndex`, `initializeLedgerSchema`, `buildFilterClause`-Kategorie-Zweig, `explainEntryFilterPlan`
- `src/Tests/LedgerTests.fs`: `splitCategoryIdsTests` (vor `recordComdirectPushTests`)
- `src/Tests/LedgerPageTests.fs`: neue Testgruppe "Split-Zeilen-Kategorien im Kategorie-Filter" am Ende der Persistence-Testliste, Seed-Helfer `seedSplitEntry`/`seedDeletedSplitEntry` vor `catGuid`

Diary-Notiz: Was: Split-Zeilen-Kategorien fließen jetzt in den Kassenbuch-Kategorie-Filter ein (neue Indextabelle, in derselben Transaktion geführt, aus dem Bestand neu baubar). Dateien: `Domain.fs`, `Persistence.fs`, `LedgerPageTests.fs`, `LedgerTests.fs`. Warum: `ledger-vgfw3` hatte den Kategorie-Filter bewusst auf die einfache Kategorie beschränkt (ADR 0036 Punkt 2); Roman setzt diesen Filter in 70-80% der Fälle, eine Split-Buchung fiel bisher still durch. Tests: 16 neu, 1008/1008 gesamt grün (7 übersprungen, .env-gated).

**Nachtrag Conductor (Integration):** Den Migrations-Absatz im Abschnitt "Ereignisse" habe ich wie oben vorgegeben von Hand in die ledger-README gesetzt. In der Testmeldung zu AC9 habe ich den als Gedankenstrich benutzten Bindestrich durch einen Doppelpunkt ersetzt.
