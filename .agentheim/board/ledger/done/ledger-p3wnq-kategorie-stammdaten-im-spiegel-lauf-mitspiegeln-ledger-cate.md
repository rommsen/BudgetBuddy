---
id: ledger-p3wnq
title: "Kategorie-Stammdaten im Spiegel-Lauf mitspiegeln (ledger_categories)"
status: done
type: feature
context: ledger
created: 2026-09-20
completed:
depends_on: []
blocks: [ledger-h6ptn]
tags: [backend, kassenbuch, spiegel, persistence]
related_adrs: [0013, 0014, 0018, 0030, 0039]
related_research: []
prior_art: [ledger-t7v2q]
---

## Why

Die Kassenbuch-Liste soll die Kategorie am PC voll qualifiziert zeigen ("Alltag:
Lebensmittel", Entwurf `docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`,
1.9 Zeile 260). Die Kategoriegruppe steht nur an `YnabCategory.GroupName`
(`src/Shared/Domain.fs:429`), befüllt aus YNABs `/categories` bzw. den `category_groups`
in `/budgets/{id}` (`src/Server/YnabClient.fs:55-78`, `243-249`). YNABs
**Transaktions**-JSON trägt sie nicht: `observationDecoder`
(`src/Server/YnabObservations.fs:170-194`) liest `category_id` und `category_name`, mehr
gibt es dort nicht.

ADR 0039 hat entschieden, dass die Gruppe nicht ins Ereignis kommt, sondern im Lesepfad
aufgelöst wird, und zwar gegen gespiegelte Stammdaten in SQLite, damit der
Kassenbuch-Lesepfad netzfrei bleibt. Dieses Item legt diese Stammdaten an. Es ist die
Vorarbeit für `ledger-h6ptn` und für sich allein noch nicht sichtbar.

Muster ist `ledger_accounts` (`src/Server/Ledger/Persistence.fs:106`): YNAB-Stammdaten
werden schon heute in eine eigene Tabelle gespiegelt, nicht in Ereignisse geschrieben.

## What

### 1. Tabelle und Persistence

- Neue Tabelle im bestehenden Schema-Init von `src/Server/Ledger/Persistence.fs` (die
  `CREATE TABLE IF NOT EXISTS`-Blöcke stehen ab Zeile 106):

  ```sql
  CREATE TABLE IF NOT EXISTS ledger_categories (
      ynab_category_id TEXT PRIMARY KEY,
      name             TEXT NOT NULL,
      group_name       TEXT NOT NULL,
      updated_at       TEXT NOT NULL
  )
  ```

  `ledger_entries` bekommt **keine** neue Spalte. Keine SQLite-Spezialitäten, kein
  `json_extract` (ADR 0013 Punkt 8).
- Upsert-Funktion für eine `YnabCategory list`, nach dem Muster des
  `ON CONFLICT(id) DO UPDATE SET` für `ledger_accounts` (`Persistence.fs:331`). Ein
  geänderter Gruppenname überschreibt, es entsteht keine zweite Zeile.
- **Nur Upsert, nie Löschen.** Kategorien, die YNAB nicht mehr liefert (in YNAB gelöscht),
  bleiben mit ihrem letzten bekannten Gruppennamen stehen. Das ist Absicht: eine Buchung,
  deren Kategorie inzwischen gelöscht ist, soll ihre Beschriftung behalten statt sie zu
  verlieren. Ein `DELETE` der nicht mehr gelieferten Zeilen wäre die schlechtere Variante,
  weil er alte Buchungen rückwirkend entqualifiziert. Die Tabelle ist damit eine
  wachsende Sammlung aller je gesehenen Kategorien, nicht ein exaktes Abbild des heutigen
  Katalogs; für den Zweck (Beschriftung nachschlagen) ist genau das richtig.
- Ladefunktion, die die Zuordnung als `Map<Guid, string>` (Kategorie-Id auf Gruppenname)
  liefert. Sie ist das, was `ledger-h6ptn` im Lesepfad benutzt; hier wird sie nur angelegt
  und getestet.
- Zeitpunkte als exakter ISO-Text wie überall im `ledger`, nie als SQLite-Datumstyp.

### 2. Abruf im Spiegel-Lauf

- `MirrorFetchers` (`src/Server/Ledger/CommandHandlers.fs:31-38`) bekommt ein drittes Feld
  `FetchCategories: unit -> Async<YnabResult<YnabCategory list>>`.
- Verdrahtet in `mirrorYnabBudget` mit `Server.YnabClient.getCategories token budgetId`.
  Die Funktion existiert bereits (`src/Server/YnabClient.fs:226`) und flacht die
  `category_groups` schon auf `YnabCategory.GroupName` aus. Bewusst **ohne**
  `applyReadyToAssign`: dieses Item braucht nur Id, Name und Gruppe, nicht den
  Available-Betrag.
- **Voller Abruf je Lauf, kein Delta.** `getCategories` nimmt kein
  `last_knowledge_of_server` entgegen (Signatur `token -> YnabBudgetId -> ...`) und holt
  `/budgets/{id}/categories` immer vollständig. Das ist dieselbe Entscheidung, die ADR
  0018 für die Konten getroffen hat, und sie passt zur Upsert-Logik: ein Vollabruf plus
  Upsert ist selbstheilend, ein Delta wäre es nur mit zusätzlichem Gedächtnis. Der
  Katalog hat die Größenordnung einiger Dutzend Kategorien, das kostet nichts.
- Aufruf in `runMirror` **nach** der Beobachtungsschleife und **nach**
  `saveServerKnowledge`, so gekapselt, dass weder ein `Error` noch eine Ausnahme aus diesem
  Abruf den Lauf auf `MirrorRunFailed` zieht (ADR 0039 Punkt 4). Die Reihenfolge ist nicht
  kosmetisch: stünde der Abruf vorn und bekäme ein 429, scheiterte ein Lauf an etwas, das
  er für seine eigentliche Arbeit nicht braucht.
- Der Abruf zählt in **keinen** der Lauf-Zähler (`newEntries`, `changedEntries`,
  `deletedEntries`). Er beschreibt keine Änderung an einer Buchung.

### 3. Test-Fixtures aufräumen

Das dritte Feld in `MirrorFetchers` zwingt alle Lauf-Fixtures an. Dabei einen
Default-Record für `MirrorFetchers` einziehen, damit das nächste Feld eine Zeile kostet
statt zwölf. Betroffen: `src/Tests/LedgerMirrorTests.fs` ab Zeile 510 sowie 918, 1133,
1167 (Zeilenangaben vor der Änderung, per Grep nachprüfen).

## Acceptance criteria

- [ ] `src/Server/Ledger/Events.fs` ist unverändert: kein neues Feld in `CategoryRef`,
      keine neue Schema-Version, kein neuer Zweig in `decodePayload`. Per Diff belegt.
- [ ] Die Tabelle `ledger_categories` wird vom Schema-Init angelegt, auch auf einer
      bestehenden Datenbank, die sie noch nicht hat (`CREATE TABLE IF NOT EXISTS`).
      Belegt durch einen Test gegen In-Memory-SQLite (ADR 0030).
- [ ] Ein Upsert schreibt eine Liste von Kategorien; ein zweiter Upsert derselben
      `ynab_category_id` mit geändertem `group_name` überschreibt den Wert und lässt die
      Zeilenzahl der Tabelle unverändert.
- [ ] Ein Upsert mit einer Liste, die eine zuvor gespeicherte Kategorie **nicht** mehr
      enthält (in YNAB gelöscht), lässt deren Zeile unverändert stehen: die Funktion
      löscht nie.
- [ ] Die Ladefunktion liefert für eine gefüllte Tabelle eine `Map` mit genau den
      gespeicherten Kategorie-Ids, und für eine leere Tabelle eine leere `Map` statt eines
      Fehlers.
- [ ] Ein Spiegel-Lauf mit erfolgreichem `FetchCategories` füllt `ledger_categories` mit
      allen gelieferten Kategorien.
- [ ] Ein Spiegel-Lauf, dessen `FetchCategories` `Error` liefert, endet trotzdem `Ok` und
      hat die Buchungen des Laufs geschrieben; `ledger_categories` behält ihren vorherigen
      Stand (also nicht leer, wenn vorher etwas drinstand).
- [ ] Dasselbe, wenn `FetchCategories` eine Ausnahme wirft statt `Error` zu liefern.
- [ ] Ein Lauf ohne Buchungsänderungen meldet dieselben Werte in `newEntries`,
      `changedEntries` und `deletedEntries` wie vor dieser Änderung: der Kategorie-Abruf
      verändert keinen Zähler.
- [ ] Ein zweiter Lauf über dieselben Daten schreibt kein zusätzliches Ereignis und keine
      neue laufende Nummer (die bestehende Idempotenz-Zusage des Laufs bleibt unberührt).
- [ ] Alle Tests gegen In-Memory-SQLite, nie gegen die Entwicklungsdatenbank (ADR 0030).
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

`YnabClient.getCategories` liefert heute auch die interne Gruppe
`"Internal Master Category"` mit der Kategorie `"Inflow: Ready to Assign"`
(`src/Shared/Domain.fs:442`, `456`). Sie landet mit in der Tabelle; das ist unschädlich,
weil die Buchungen im Kassenbuch sie nicht als Kategorie tragen. Kein Filtern, damit die
Tabelle einfach das spiegelt, was YNAB liefert.

`YnabCategory` hat heute kein `Hidden`-Feld. Versteckte Kategorien landen deshalb ohne
Kennzeichen in der Tabelle. Für die Gruppenbeschriftung ist das egal; wer das Kennzeichen
braucht (`ledger-j8xq4` klappt versteckte Kategorien im Picker ein), muss `YnabCategory`
erweitern, und das gehört dorthin, nicht hierher.

Nach einem frischen Deploy ohne gelaufenen Spiegel ist die Tabelle leer. Das ist der
vorgesehene Zustand, nicht ein Fehler: `ledger-h6ptn` fällt dann auf den Blattnamen zurück.

Offen für später, ausdrücklich nicht hier zu entscheiden: ob der Kategorie-Abruf ein
Frische-Kriterium bekommt (für jetzt: jeder Lauf, eine Mechanik statt zwei, ADR 0039 Punkt
6).

**Was diese Tabelle ausdrücklich nicht ist:** eine Quelle für Kategorie-Picker. Weil sie
nie löscht, enthält sie auch in YNAB gelöschte Kategorien. Ein Picker, der daraus
gespeist würde, böte Roman Kategorien an, die es nicht mehr gibt. Die
`YnabClient.getCategories`-Aufrufe in `src/Server/Api.fs` bleiben deshalb, wo sie sind.

## Verifier note (iteration 1)

**VERDICT: FAIL** — `likely-fixable`. Zwei Punkte, beide klein. Alles andere ist geprüft und in
Ordnung (Liste unten), damit die nächste Runde nicht daran churnt.

**REASONS:**

- **Akzeptanzkriterium 5 ist nur zur Hälfte belegt.** "für eine leere Tabelle eine leere `Map`
  statt eines Fehlers" hat keinen Test. Der angebotene Ersatz (`src/Tests/LedgerTests.fs`,
  testCase "loadCategoryGroups liefert für eine nie gespeicherte Kategorie-Id keinen Eintrag,
  keinen Fehler") prüft einen fehlenden Schlüssel in einer **gefüllten** Map, nicht die leere
  Tabelle. Die Begründung des Workers ("mit der geteilten In-Memory-Test-DB nicht isoliert
  herstellbar") trifft nicht zu: der Verifier hat den Test in einer Arbeitskopie außerhalb des
  Worktrees eingefügt, sechs Zeilen: `DELETE FROM ledger_categories` über
  `global.Persistence.activeConnectionString()`, also **dasselbe Muster, das der Worker drei
  Tests weiter oben im Zeilenzähl-Test selbst benutzt**, dann
  `Expect.isTrue (Map.isEmpty (loadCategoryGroups () |> Async.RunSynchronously))`. Ergebnis: 953
  statt 952 erfolgreich, kein anderer Test gestört. Beide betroffenen Listen
  (`ledgerPersistenceTests` Z. 825, `mirrorRunTests` Z. 551) sind `testSequenced`, ein Rennen mit
  den Spiegel-Tests ist ausgeschlossen. "Durch Konstruktion mitbewiesen" ist genau die
  Begründung, die der Verifikations-Check nicht gelten lässt.
- **Nebenbefund, in derselben Runde mitzunehmen:** `src/Tests/LedgerMirrorTests.fs`, neuer
  testCase "ein Lauf mit Buchungen und erfolgreichem Kategorie-Abruf ...", enthält in einer neu
  hinzugefügten Assertion-Meldung "keine Aenderung durch den Kategorie-Abruf" eine Umschreibung,
  während die Nachbarzeile im selben Test "keine Löschung durch den Kategorie-Abruf" korrekt
  schreibt. Die Budget-Ids `kategorie-abruf-zaehler`/`-1` sind Bezeichner, nicht Prosa, und
  werden nicht beanstandet.

**SUGGESTED_FIX:** Den Leer-Tabelle-Test für `loadCategoryGroups` nachziehen (DELETE über
`activeConnectionString()` innerhalb der testCase-Closure, ADR 0033), und in derselben Runde
"Aenderung" auf "Änderung" setzen. **Der weite `try` in `refreshCategories` ist geprüft und
bewusst nicht beanstandet, daran nichts ändern.**

**ITERATION_HINT:** likely-fixable

### Geprüft und in Ordnung (nicht nacharbeiten)

- **Der weite `try` (ein Schreibfehler wird wie ein Abruffehler geschluckt): bewusst akzeptiert,
  kein ADR nötig.** Der Fang deckt auch `upsertCategories` ab. Das ist hier richtig: die
  beobachtbare Folge eines geschluckten Schreibfehlers ist exakt die, die ADR 0039 Punkt 4 schon
  zusagt (Tabelle behält ihren letzten Stand, Zeilen zeigen nur den Blattnamen, Lauf bleibt
  `Ok`). Ein engerer `try` nur um den Abruf würde den Lauf gegen eine SQLite-Ausnahme (locked,
  disk full) kippen lassen, die mit seiner eigentlichen Arbeit nichts zu tun hat, also gegen
  genau die Fragilität, die Punkt 4 vermeiden will. Kein ADR-Gegenstand: keine eigene Abwägung
  mit eigener Folge, nur ein Schritt weiter auf derselben Linie. Ein Satz im README-Bullet
  ("auch ein fehlgeschlagenes Schreiben lässt den Lauf `Ok`, sichtbar nur auf stdout") wäre eine
  faire Ergänzung, ist aber nicht Bedingung.
- **Die vier Fehlermodus-Kriterien, mutationsgeprüft** (alle Mutationen in einer rsync-Kopie
  unter dem Scratchpad, der Worktree nie angefasst, `git status` dort leer):
  - `try/with` entfernt plus `failwith` im `Error`-Zweig → **beide** Fehlermodus-Tests rot. Sie
    diskriminieren also.
  - Aufruf `do! refreshCategories fetchers.FetchCategories` entfernt → der Erfolgs-Test rot.
  - `DELETE FROM ledger_categories` vor dem Upsert eingebaut → der Nie-Löschen-Test rot.
  - Der Ausnahme-Test wirft wirklich (`async { return failwith ... }` wertet beim Ausführen aus,
    nicht beim Bauen), bestätigt durch Mutation 1.
- **Wichtiger Befund zur Transportverlust-Frage, der eine Annahme des Conductors korrigiert:**
  genau der Rest des abgebrochenen Laufs, `failwith $"TEMP-BRUCH: {e}"` im `Error`-Zweig **bei
  bestehendem** `try/with`, bleibt **grün**. Der Catch-all hätte ihn geschluckt. Die Annahme, er
  hätte vier Kriterien gebrochen, gilt nur für eine Fassung ohne den inneren `try`. Kein
  aktueller Defekt, aber es heißt: **Fehler im `Error`-Zweig und im `Ok`-Zweig sind vom Testnetz
  nicht unterscheidbar, abgedeckt ist nur ihre beobachtbare Wirkung.**
- **Kriterium 1 unabhängig bestätigt:** `git diff f33ff51 HEAD -- src/Server/Ledger/Events.fs`
  ist leer; kein `+`-Treffer für `CategoryRef`, `decodePayload` oder `SchemaVersion` im ganzen
  Diff.
- **Schema und Upsert:** `CREATE TABLE IF NOT EXISTS ledger_categories` im bestehenden
  Schema-Block (`createLedgerSchemaOn`, `initializeLedgerSchema` bleibt dünne Hülle darüber);
  Upsert per `ON CONFLICT(ynab_category_id) DO UPDATE SET ... excluded.*`, kein
  Delete-then-Insert, kein `json_extract`, kein SQLite-Datumstyp
  (`updated_at = dateToText DateTime.UtcNow`, `"o"` invariant, wie im Rest des Kassenbuchs).
- **Die Fixture-Umarbeitung ist harmlos** (die subtilste Gefahr des Diffs): `Run.fetchers` und
  `Run.failingTransactions` setzen weiterhin **beide** alten Felder explizit und erben von
  `emptyMirrorFetchers` nur `FetchCategories`. Alle übrigen Fixture-Stellen leiten schon vorher
  über `{ Run.fetchers with ... }` ab. **Kein Test hat still leere Eingaben statt echter Daten
  bekommen.** Außerhalb der Tests gibt es nur eine `MirrorFetchers`-Konstruktionsstelle
  (`mirrorYnabBudget`).
- **Rückgabeform:** `Map<Guid, string>`, Schlüssel der rohe `Guid` aus `YnabCategoryId`; passt zu
  dem, was `ledger-h6ptn` im Lesepfad in der Hand hält. Randnotiz ohne Kriterium:
  `upsertCategories` hat einen ungetesteten Frühausstieg `if List.isEmpty categories then ()`.
- **Zuschnitt:** vier Dateien, alle im Auftrag. `src/Tests/LedgerTests.fs` ist der richtige Ort,
  die neuen Tests stehen direkt neben den bestehenden `ledger_accounts`-Tests. Nichts aus
  `src/Client/**`, kein `.agentheim/`-Pfad, keine Config-Drift.
- **`ADRS_WRITTEN: none` ist richtig.** ADR 0039 Punkte 3, 4 und 6 decken Tabelle,
  Upsert-nie-Löschen, Vollabruf, bestes Bemühen und die Stellung am Laufende ab. ADR 0018 und
  ADR 0013 Punkt 8 eingehalten.
- **Testlauf:** `dotnet test` im Worktree, Exit 0, 956 erfolgreich, 7 übersprungen (die
  `.env`-gegateten Integrationstests), 0 Fehler. Kein `ptestCase`/`ftestCase`, keine deaktivierte
  Assertion, kein `TEMP`/`TODO`/`HACK`/`FIXME` in `+`-Zeilen, keine Gedankenstriche. Außer dem
  einen Umlaut-Nit kein weiterer Rest des abgebrochenen Laufs gefunden.
- **README-Delta** entspricht dem, was der Diff einführt; das Repo-Gate stand auf Exit 0.

## Outcome

Die Kategorie-Stammdaten werden jetzt wie die Konten gespiegelt (ADR 0039): `ledger_categories`
(`ynab_category_id`, `name`, `group_name`, `updated_at`) im bestehenden Schema-Init
(`createLedgerSchemaOn`, aufgerufen von `initializeLedgerSchema`), eine Upsert-Funktion
`upsertCategories` (`ON CONFLICT(ynab_category_id) DO UPDATE SET ... excluded.*`, nie `DELETE`)
und eine Ladefunktion `loadCategoryGroups`, die die Zuordnung Kategorie-Id auf Gruppenname als
`Map<Guid, string>` liefert, leer statt Fehler, wenn nichts gespeichert ist.

`MirrorFetchers` bekam ein drittes Feld `FetchCategories`; `emptyMirrorFetchers` als
Default-Record macht die Testfixturen robust gegen künftige weitere Felder. `runMirror` ruft den
Abruf über `refreshCategories` **nach** der Beobachtungsschleife und **nach**
`saveServerKnowledge` auf; ein `Error` wie eine geworfene Ausnahme werden dort abgefangen und nur
geloggt, der Lauf bleibt `Ok` und die Zähler bleiben unberührt. Punkt 5 aus ADR 0039 (Lesepfad
qualifiziert, Anzeige setzt zusammen) ist ausdrücklich nicht Teil dieser Aufgabe, das ist
`ledger-h6ptn`.

`dotnet build` 0 Warnungen, `dotnet test` 957 erfolgreich, 7 übersprungen, 0 Fehler.

**Der weite `try` in `refreshCategories`, bewusst so.** Der Fang deckt auch `upsertCategories`
ab, ein fehlgeschlagenes **Schreiben** wird also wie ein fehlgeschlagener **Abruf** geschluckt.
ADR 0039 Punkt 4 macht wörtlich nur den Abruf zum besten Bemühen. Der Verifier hat das geprüft
und akzeptiert: die beobachtbare Folge ist exakt die, die Punkt 4 ohnehin zusagt (Tabelle behält
ihren letzten Stand, Zeilen zeigen nur den Blattnamen, Lauf bleibt `Ok`), und ein engerer `try`
nur um den Abruf würde den Lauf gegen eine unbeteiligte SQLite-Ausnahme (locked, disk full)
kippen lassen, also gegen genau die Fragilität, die Punkt 4 vermeiden will. Kein ADR-Gegenstand:
keine eigene Abwägung mit eigener Folge, nur ein Schritt weiter auf derselben Linie. Als faire
Ergänzung steht die Folge jetzt im README-Bullet: auch ein fehlgeschlagenes Schreiben lässt den
Lauf `Ok`, sichtbar nur auf stdout.

**Iteration 1 wurde verworfen (FAIL), aus einem schmalen Grund.** Akzeptanzkriterium 5 verlangt
für eine leere Tabelle eine leere `Map` statt eines Fehlers. Dafür gab es keinen Test; der
angebotene Ersatz prüfte einen fehlenden Schlüssel in einer **gefüllten** Map. Die Begründung,
eine isolierte leere Tabelle sei mit der geteilten, akkumulierenden In-Memory-Test-DB nicht
herstellbar, hat der Verifier nicht diskutiert, sondern widerlegt, indem er den Test schrieb:
sechs Zeilen, `DELETE FROM ledger_categories` über `activeConnectionString()`, also dasselbe
Muster, das der Worker drei Tests weiter oben selbst benutzt. "Durch Konstruktion mitbewiesen"
ist genau die Begründung, die ein Verifikations-Gate nicht gelten lassen kann, weil sie sich
nicht falsifizieren lässt.

**Iteration 2 (PASS)** zieht den Test nach und korrigiert eine Umschreibung in einer
Assertion-Meldung. Genau zwei Hunks, sonst nichts; `git diff HEAD~1 HEAD -- src/Server/` ist
leer, der weite `try` also nachweislich unangetastet.

**Die entscheidende Prüfung des neuen Tests.** Er öffnet eine eigene Verbindung über
`activeConnectionString()` und löscht darüber, während `loadCategoryGroups` über
`Core.getConnection` liest. Unter `USE_MEMORY_DB=true` teilen sich zwei Verbindungen dieselbe
In-Memory-Datenbank aber nur, wenn der Connection-String das hergibt; landete das `DELETE` in
einer anderen Datenbank, wäre der Test vakuum-grün. Der Verifier hat beide Richtungen mutiert:
eine Kategorie **vor** dem `DELETE` eingesetzt plus eine Vorprüfung, dass die Map vorher nicht
leer ist, beides grün (das `DELETE` trifft also wirklich die gelesene Datenbank, beide lösen auf
`Data Source=:memory:;Mode=Memory;Cache=Shared` auf); und das `DELETE` entfernt bei belassenem
Einsatz, worauf genau dieser Test rot wird. Der Test belegt also, was er behauptet, und er
diskriminiert.

Zusätzlich mutationsgeprüft (Iteration 1, weiterhin gültig): `try/with` entfernt plus `failwith`
im `Error`-Zweig macht **beide** Fehlermodus-Tests rot; der Aufruf von `refreshCategories`
entfernt macht den Erfolgs-Test rot; ein `DELETE` vor dem Upsert macht den Nie-Löschen-Test rot.

### Drei Korrekturen und Hinweise, die zum Protokoll gehören

1. **Eine Behauptung aus Iteration 1 ist widerlegt.** Der abgebrochene Vorlauf hatte in
   `refreshCategories` einen Scratch-Zustand `failwith $"TEMP-BRUCH: {e}"` im `Error`-Zweig
   hinterlassen, und der Wiederholungslauf berichtete, dieser hätte die vier
   Fehlschlag-Kriterien gebrochen. Der Verifier hat es nachgestellt: **bei bestehendem
   `try/with` bleibt genau dieser Rest grün**, der Catch-all hätte ihn geschluckt. Die Aussage
   gilt nur für eine Fassung ohne den inneren `try`. Das Entfernen war trotzdem richtig, Scratch
   Code soll nicht ausliefern. Der bleibende Befund ist ein anderer und wichtiger: **Fehler im
   `Error`-Zweig und im `Ok`-Zweig sind vom Testnetz nicht unterscheidbar, abgedeckt ist nur
   ihre beobachtbare Wirkung.**
2. **`TESTS_ADDED` war mit 9 zu niedrig.** Der kumulative Diff fügt **10** `testCase`-Deklarationen
   hinzu und entfernt keine; der Worker hat den Stand aus Iteration 1 nicht hochgezählt. Der
   Runner belegt den Zuwachs unabhängig (956 auf 957). Hier mit 10 verbucht.
3. **Der README-Bullet steht am Ende des Abschnitts `## Ubiquitous language`** (hinter dem
   Such-Filter-Eintrag), nicht unmittelbar hinter `Spiegel-Lauf`, wie die Worker-Prosa sagte. Das
   ist keine Fehlplatzierung, sondern die Funktionsweise von `append`: der Einfügepunkt ist das
   Abschnittsende. Das Glossar-Ende ist eine legitime Stelle, das Gate stand auf Exit 0.

Nicht getestet und bewusst so: der Frühausstieg `if List.isEmpty categories then ()` in
`upsertCategories`. Kein Kriterium hängt daran, und die beobachtbare Wirkung ist mit wie ohne
Wächter dieselbe, weil ein Dapper-Multi-Exec über eine leere Liste selbst ein No-op ist. Ein Test
dafür wäre Polsterung.
